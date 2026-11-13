using System;
using CloseTheDeal.Core;
using CloseTheDeal.Player;
using CloseTheDeal.Props;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Events;

namespace CloseTheDeal.Items
{
    public enum PickupState : byte
    {
        /// <summary>Loose in the level: a host-simulated prop anyone in reach can take.</summary>
        InWorld = 0,
        /// <summary>In a player's hands.</summary>
        Held = 1,
        /// <summary>Out of play: used up or fallen out of the building, waiting to be put back where it started.</summary>
        Away = 2
    }

    public enum ReleaseKind : byte
    {
        Dropped = 0,
        Thrown = 1,
        /// <summary>Set down deliberately by the item's own use, like the wet floor sign.</summary>
        Placed = 2,
        /// <summary>Lost from the hands: the carrier was knocked about, died or left the game.</summary>
        Knocked = 3
    }

    /// <summary>
    /// Makes a prop something a player can pick up, carry, drop and throw. The host owns
    /// every decision: who holds it, when it is let go, what it hits. Everyone else learns
    /// the holder from one synced value and shows the item in that player's hands locally,
    /// so it never lags behind the view. Loose, it is an ordinary host-simulated prop.
    /// Design and ownership: docs/systems/ITEMS.md.
    /// </summary>
    // After PlayerCamera's LateUpdate, so a carried item is placed from this frame's view.
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Prop))]
    public sealed class Pickup : NetworkBehaviour
    {
        [Tooltip("What this item is: its name, weight, how it is held and thrown. Read at runtime, never written.")]
        [SerializeField] ItemDefinition _definition;

        [Tooltip("The shared rules for carrying and for what thrown things do when they land. Read at runtime, never written.")]
        [SerializeField] CarryProfile _rules;

        [Tooltip("The part that is shown in the carrier's hands: the child holding the item's meshes. The solid part stays where the host says; only this moves with the view.")]
        [SerializeField] Transform _visual;

        [Header("Hooks for sound, effects and UI (fire on every machine)")]
        [Tooltip("A player has taken this item.")]
        [SerializeField] UnityEvent _onTaken;
        [Tooltip("The item was dropped, placed or knocked from the hands.")]
        [SerializeField] UnityEvent _onDropped;
        [Tooltip("The item was thrown.")]
        [SerializeField] UnityEvent _onThrown;
        [Tooltip("The thrown item hit something hard. The number is the impact speed in metres per second.")]
        [SerializeField] UnityEvent<float> _onImpact;
        [Tooltip("The item is back where it started after being used up or lost.")]
        [SerializeField] UnityEvent _onReturned;

        const float ReleaseBlendSeconds = 0.12f;
        const float ShoveLift = 0.25f;
        const float TwoPi = Mathf.PI * 2f;

        readonly SyncVar<NetworkObject> _holder = new();
        readonly SyncVar<bool> _away = new();

        Prop _prop;
        Rigidbody _body;
        ItemBehaviour _behaviour;
        Collider[] _colliders;
        int[] _colliderLayers;
        bool[] _colliderEnabled;
        Vector3 _visualRestPosition;
        Quaternion _visualRestRotation;

        // What this machine is currently showing.
        ItemCarrier _carrier;
        bool _shownAway;
        float _blend;
        Vector3 _blendFromPosition;
        Quaternion _blendFromRotation;
        CapsuleCollider _ignored;
        bool _ignoring;
        float _ignoreUntil;

        // Host only.
        Vector3 _homePosition;
        Quaternion _homeRotation;
        bool _live;
        float _liveUntil;
        float _throwerSafeUntil;
        PlayerMotor _thrower;
        int _lastHolderClientId = -1;
        Vector3 _lastVelocity;
        float _returnAt;

        // A prop with no Visual child: its own renderers stay on the solid part, and a copy of
        // them (built the first time it is carried) is what is shown in the hands.
        ItemDefinition _derivedDefinition;
        Renderer[] _ownRenderers;
        bool[] _ownRendererEnabled;
        Transform _handsCopy;
        static CarryProfile _defaultRules;

        /// <summary>Every machine. A player took the item.</summary>
        public event Action<Pickup, ItemCarrier> Taken;
        /// <summary>Every machine. The item left a player's hands, and how.</summary>
        public event Action<Pickup, ReleaseKind> Released;
        /// <summary>Every machine. The thrown item hit something at this speed, in metres per second.</summary>
        public event Action<Pickup, float> Impact;
        /// <summary>Every machine. The item is back where it started.</summary>
        public event Action<Pickup> Returned;

        /// <summary>The hand-made definition if this prefab has one, otherwise one worked out from the prop itself (LooseItemDefinition).</summary>
        public ItemDefinition Definition => _definition != null ? _definition : _derivedDefinition != null ? _derivedDefinition : Derive();

        /// <summary>This prefab's own carry rules, else the players' (CarryProfile.Shared), else the built-in defaults.</summary>
        public CarryProfile Rules => _rules != null ? _rules : CarryProfile.Shared != null ? CarryProfile.Shared : DefaultRules();

        /// <summary>True when nobody wrote a definition for this prop and its name, hold and throw are worked out from its size and mass.</summary>
        public bool IsDerived => _definition == null;
        public Rigidbody Body => _body;
        public ItemCarrier Holder => _carrier;
        public PickupState State => _away.Value ? PickupState.Away : _holder.Value != null ? PickupState.Held : PickupState.InWorld;

        /// <summary>Host only. The client id of whoever held it last, or -1 if nobody has.</summary>
        public int LastHolderClientId => _lastHolderClientId;

        void Awake()
        {
            _prop = GetComponent<Prop>();
            _body = GetComponent<Rigidbody>();
            _behaviour = GetComponent<ItemBehaviour>();

            _colliders = GetComponentsInChildren<Collider>(true);
            _colliderLayers = new int[_colliders.Length];
            _colliderEnabled = new bool[_colliders.Length];
            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliderLayers[i] = _colliders[i].gameObject.layer;
                _colliderEnabled[i] = _colliders[i].enabled;
            }

            if (_visual != null)
            {
                _visual.GetLocalPositionAndRotation(out _visualRestPosition, out _visualRestRotation);
            }
            else
            {
                _visualRestPosition = Vector3.zero;
                _visualRestRotation = Quaternion.identity;
                _ownRenderers = GetComponentsInChildren<Renderer>(true);
                _ownRendererEnabled = new bool[_ownRenderers.Length];
                for (int i = 0; i < _ownRenderers.Length; i++)
                    _ownRendererEnabled[i] = _ownRenderers[i].enabled;
            }

            _holder.OnChange += OnHolderChanged;
            _away.OnChange += OnAwayChanged;
            enabled = false;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            transform.GetPositionAndRotation(out _homePosition, out _homeRotation);
            _lastHolderClientId = -1;
            _live = false;
            TimeManager.OnTick += ServerTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TimeManager.OnTick -= ServerTick;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyState();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            StopIgnoring();
            ItemCarrier old = _carrier;
            _carrier = null;
            _blend = 0f;
            // A pooled item may be spawned again: it must come back drawn where it is, not in old hands.
            if (_visual != null)
            {
                _visual.SetLocalPositionAndRotation(_visualRestPosition, _visualRestRotation);
                _visual.gameObject.SetActive(true);
                _shownAway = false;
            }
            else
            {
                RefreshShown(false);
            }
            if (old != null)
                old.NotifyReleased(this);
        }

        void OnHolderChanged(NetworkObject previous, NetworkObject next, bool asServer) => ApplyState();
        void OnAwayChanged(bool previous, bool next, bool asServer) => ApplyState();

        // ---- Host: taking and letting go ------------------------------------------------------

        /// <summary>Host only. Puts the item in the carrier's hands. False if it is not loose in the level.</summary>
        public bool ServerHold(ItemCarrier carrier)
        {
            if (!IsServerStarted || carrier == null || State != PickupState.InWorld)
                return false;

            _live = false;
            _lastHolderClientId = carrier.OwnerId;
            _holder.Value = carrier.NetworkObject;
            ApplyState();
            if (_behaviour != null)
                _behaviour.ServerOnTaken(carrier);
            return true;
        }

        /// <summary>Host only. Lets go from where the hands are, with the given starting velocity in metres per second.</summary>
        public void ServerRelease(ReleaseKind kind, Vector3 velocity)
        {
            if (!IsServerStarted || _carrier == null)
                return;

            _carrier.GetHoldPose(Definition, false, out Vector3 position, out Quaternion rotation);
            ServerReleaseAt(kind, position, rotation, velocity);
        }

        /// <summary>Host only. Lets go and puts the item at an exact spot, for items that are placed rather than thrown.</summary>
        public void ServerReleaseAt(ReleaseKind kind, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (!IsServerStarted || _carrier == null)
                return;

            PlayerMotor releasedBy = _carrier.Motor;
            _holder.Value = null;
            ApplyState();

            ServerTeleport(position, rotation);
            _body.linearVelocity = velocity;
            _body.angularVelocity = kind == ReleaseKind.Thrown ? rotation * Vector3.right * (Definition.ThrowTumble * TwoPi) : Vector3.zero;
            _body.WakeUp();

            _live = kind == ReleaseKind.Thrown;
            if (_live)
            {
                _thrower = releasedBy;
                _liveUntil = Time.time + Rules.LiveThrowSeconds;
                _throwerSafeUntil = Time.time + Rules.ThrowerIgnoreSeconds;
                _lastVelocity = velocity;
            }

            ObserversReleased(kind);
        }

        /// <summary>
        /// Host only. The item has been used up (it exploded, it ran empty). It leaves play and,
        /// if its definition says so, a fresh one is back where it started after a while.
        /// </summary>
        public void ServerUseUp()
        {
            if (!IsServerStarted)
                return;

            // Used up is not let go: a gun went into the weapon slots, a case went off. The hands
            // empty through the holder change alone, with no drop sound or "released" on any machine.
            if (Definition.ReturnsAfterUsedSeconds <= 0f)
            {
                ServerDropFromHands(false);
                Despawn();
                return;
            }

            ServerSendAway(Definition.ReturnsAfterUsedSeconds, false);
        }

        void ServerSendAway(float returnAfterSeconds, bool announce)
        {
            ServerDropFromHands(announce);
            _live = false;
            _away.Value = true;
            ApplyState();
            ServerTeleport(_homePosition, _homeRotation);
            _returnAt = Time.time + returnAfterSeconds;
        }

        void ServerComeBack()
        {
            ServerTeleport(_homePosition, _homeRotation);
            _away.Value = false;
            ApplyState();
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            if (_behaviour != null)
                _behaviour.ServerOnReturned();
            ObserversReturned();
        }

        /// <summary>
        /// If a player still holds it, takes it out of their hands without throwing it anywhere.
        /// Announced as knocked loose only when it really was (its carrier left); a used-up item is not.
        /// </summary>
        void ServerDropFromHands(bool announce)
        {
            if (_holder.Value == null)
                return;

            _holder.Value = null;
            ApplyState();
            if (announce)
                ObserversReleased(ReleaseKind.Knocked);
        }

        void ServerTeleport(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            _body.position = position;
            _body.rotation = rotation;
            // Switchboard counts a sleeping body as at rest and would not send the jump.
            _body.WakeUp();
        }

        // ---- Host: every tick -----------------------------------------------------------------

        void ServerTick()
        {
            if (_away.Value)
            {
                if (Time.time >= _returnAt)
                    ServerComeBack();
                return;
            }

            if (_holder.Value != null)
            {
                FollowHolder();
                return;
            }

            // A prop at rest costs one sleep check a tick and nothing else.
            if (_body.IsSleeping())
            {
                _live = false;
                return;
            }

            if (_live)
            {
                _lastVelocity = _body.linearVelocity;
                if (Time.time >= _liveUntil)
                    _live = false;
            }

            if (_body.position.y < _homePosition.y - Rules.FallReturnDistance)
                ServerSendAway(Rules.FallReturnSeconds, false);
        }

        /// <summary>Keeps the solid part at the carrier's hands. A carrier that has gone (left the game) drops it where it is.</summary>
        void FollowHolder()
        {
            if (_carrier == null || !_carrier.IsSpawned)
            {
                ServerDropFromHands(true);
                return;
            }

            _carrier.GetHoldPose(Definition, false, out Vector3 position, out Quaternion rotation);
            _body.MovePosition(position);
            _body.MoveRotation(rotation);
        }

        // ---- Host: a thrown item landing ------------------------------------------------------

        void OnCollisionEnter(Collision collision)
        {
            if (!_live || !IsServerStarted)
                return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < Rules.MinImpactSpeed)
                return;

            PlayerMotor motor = collision.collider.GetComponentInParent<PlayerMotor>();
            if (motor != null && motor == _thrower && Time.time < _throwerSafeUntil)
                return;

            ContactPoint contact = collision.GetContact(0);
            Vector3 direction = _lastVelocity.sqrMagnitude > 0.01f ? _lastVelocity.normalized : -contact.normal;
            bool shoved = Shove(motor, speed, direction);
            bool hurt = Hurt(collision.collider.GetComponentInParent<IDamageable>(), speed, contact.point, direction);

            // One throw hurts once: after it lands on someone it is furniture again.
            if (shoved || hurt)
                _live = false;

            ObserversImpact(speed);
        }

        bool Shove(PlayerMotor motor, float speed, Vector3 direction)
        {
            if (motor == null)
                return false;

            float shove = Mathf.Min(speed * _body.mass * Rules.ShovePerKgMps, Rules.MaxShoveSpeed);
            motor.ApplyKnockback((direction + Vector3.up * ShoveLift).normalized * shove, Rules.ShoveControlLossSeconds);
            return true;
        }

        bool Hurt(IDamageable target, float speed, Vector3 point, Vector3 direction)
        {
            if (target == null)
                return false;

            float damage = (speed - Rules.MinImpactSpeed) * _body.mass * Rules.DamagePerKgMps;
            target.ApplyDamage(damage, point, direction, _lastHolderClientId);
            return true;
        }

        // ---- Every machine: showing the state -------------------------------------------------

        [ObserversRpc(RunLocally = true)]
        void ObserversReleased(ReleaseKind kind)
        {
            Released?.Invoke(this, kind);
            if (kind == ReleaseKind.Thrown)
                _onThrown.Invoke();
            else
                _onDropped.Invoke();
        }

        [ObserversRpc(RunLocally = true)]
        void ObserversImpact(float speed)
        {
            Impact?.Invoke(this, speed);
            _onImpact.Invoke(speed);
        }

        [ObserversRpc(RunLocally = true)]
        void ObserversReturned()
        {
            Returned?.Invoke(this);
            _onReturned.Invoke();
        }

        /// <summary>
        /// Brings this machine in line with the synced holder and away flags. Safe to call any
        /// number of times: it only acts on what changed.
        /// </summary>
        void ApplyState()
        {
            NetworkObject holder = _holder.Value;
            ItemCarrier carrier = holder != null ? holder.GetComponent<ItemCarrier>() : null;
            bool away = _away.Value;

            if (carrier != _carrier)
                ChangeCarrier(carrier);

            RefreshShown(away);
            _prop.SetHeldStill(_carrier != null || away);
            RefreshColliders();
            enabled = _carrier != null || _blend > 0f || _ignoring;
        }

        void ChangeCarrier(ItemCarrier next)
        {
            ItemCarrier previous = _carrier;
            _carrier = next;
            StopIgnoring();

            if (previous != null)
            {
                previous.NotifyReleased(this);
                StartReleaseBlend();
                // Enabled first: turning a collider on forgets who it was told to ignore.
                RefreshColliders();
                StartIgnoring(previous.Capsule, Rules.ThrowerIgnoreSeconds);
            }

            if (next != null)
            {
                _blend = 0f;
                next.NotifyHeld(this);
                Taken?.Invoke(this, next);
                _onTaken.Invoke();
            }
        }

        /// <summary>
        /// Loose: solid, on its own layer. Carried: not solid, unless it is a shield, which stays
        /// solid on the player layer (so its carrier's own footing and climbing never see it) and
        /// ignores its carrier's body. Away: not solid.
        /// </summary>
        void RefreshColliders()
        {
            bool held = _carrier != null;
            bool solid = !_away.Value && (!held || Definition.CoverWhileCarried);

            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliders[i].enabled = solid && _colliderEnabled[i];
                _colliders[i].gameObject.layer = held ? ItemLayers.PlayerLayer : _colliderLayers[i];
            }

            if (held && solid)
                SetIgnored(_carrier.Capsule, true);
        }

        void StartIgnoring(CapsuleCollider capsule, float seconds)
        {
            if (capsule == null || seconds <= 0f)
                return;

            _ignored = capsule;
            _ignoring = true;
            _ignoreUntil = Time.time + seconds;
            SetIgnored(capsule, true);
        }

        void StopIgnoring()
        {
            if (!_ignoring)
                return;

            _ignoring = false;
            SetIgnored(_ignored, false);
            _ignored = null;
        }

        void SetIgnored(CapsuleCollider capsule, bool ignore)
        {
            if (capsule == null || !capsule.enabled || !capsule.gameObject.activeInHierarchy)
                return;

            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i].enabled && _colliders[i].gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(_colliders[i], capsule, ignore);
            }
        }

        void StartReleaseBlend()
        {
            Transform shown = Shown;
            if (shown == null)
                return;

            shown.GetLocalPositionAndRotation(out _blendFromPosition, out _blendFromRotation);
            _blend = 1f;
        }

        /// <summary>What moves into the hands: the prefab's Visual child, or the copy made for a prop without one.</summary>
        Transform Shown => _visual != null ? _visual : _handsCopy;

        /// <summary>
        /// Hides the item while it is out of play. For a prop with no Visual child, also swaps
        /// between its own renderers (loose) and the copy in the hands (carried, or easing back).
        /// </summary>
        void RefreshShown(bool away)
        {
            if (_visual != null)
            {
                if (away != _shownAway)
                    _visual.gameObject.SetActive(!away);
                _shownAway = away;
                return;
            }

            _shownAway = away;
            bool inHands = !away && (_carrier != null || _blend > 0f);
            if (inHands && _handsCopy == null)
                _handsCopy = HandsCopy.Build(transform, _ownRenderers);
            if (_handsCopy != null)
                _handsCopy.gameObject.SetActive(inHands);
            ShowOwnRenderers(!away && !inHands);
        }

        /// <summary>Shows or hides a prop's own meshes, leaving any the prefab had switched off switched off.</summary>
        void ShowOwnRenderers(bool shown)
        {
            for (int i = 0; i < _ownRenderers.Length; i++)
            {
                if (_ownRenderers[i] != null)
                    _ownRenderers[i].enabled = _ownRendererEnabled[i] && shown;
            }
        }

        ItemDefinition Derive()
        {
            Bounds size = LooseItemDefinition.LocalBounds(transform, _ownRenderers ?? GetComponentsInChildren<Renderer>(true), _colliders);
            _derivedDefinition = LooseItemDefinition.For(name, _body.mass, size, Rules);
            return _derivedDefinition;
        }

        static CarryProfile DefaultRules()
        {
            if (_defaultRules == null)
            {
                _defaultRules = ScriptableObject.CreateInstance<CarryProfile>();
                _defaultRules.hideFlags = HideFlags.DontSave;
            }

            return _defaultRules;
        }

        /// <summary>
        /// Visual only, and only running while there is something to show: the item in a
        /// player's hands, the short ease from the hands back onto the solid part after
        /// letting go, or the wait before the thrower can be touched by it again.
        /// </summary>
        void LateUpdate()
        {
            Transform shown = Shown;
            if (_carrier != null)
            {
                if (shown != null)
                {
                    _carrier.GetHoldPose(Definition, true, out Vector3 position, out Quaternion rotation);
                    shown.SetPositionAndRotation(position + rotation * _visualRestPosition, rotation * _visualRestRotation);
                }
                return;
            }

            if (_blend > 0f)
            {
                _blend = Mathf.Max(0f, _blend - Time.deltaTime / ReleaseBlendSeconds);
                float t = 1f - _blend;
                if (shown != null)
                    shown.SetLocalPositionAndRotation(
                        Vector3.Lerp(_blendFromPosition, _visualRestPosition, t),
                        Quaternion.Slerp(_blendFromRotation, _visualRestRotation, t));
                if (_blend <= 0f)
                    RefreshShown(_away.Value);
            }

            if (_ignoring && Time.time >= _ignoreUntil)
                StopIgnoring();

            if (_blend <= 0f && !_ignoring)
                enabled = false;
        }
    }
}
