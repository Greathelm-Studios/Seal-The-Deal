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

        /// <summary>Every machine. A player took the item.</summary>
        public event Action<Pickup, ItemCarrier> Taken;
        /// <summary>Every machine. The item left a player's hands, and how.</summary>
        public event Action<Pickup, ReleaseKind> Released;
        /// <summary>Every machine. The thrown item hit something at this speed, in metres per second.</summary>
        public event Action<Pickup, float> Impact;
        /// <summary>Every machine. The item is back where it started.</summary>
        public event Action<Pickup> Returned;

        public ItemDefinition Definition => _definition;
        public CarryProfile Rules => _rules;
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
                _visual.GetLocalPositionAndRotation(out _visualRestPosition, out _visualRestRotation);

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
            if (_carrier == null)
                return;

            ItemCarrier old = _carrier;
            _carrier = null;
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

            _carrier.GetHoldPose(_definition, false, out Vector3 position, out Quaternion rotation);
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
            _body.angularVelocity = kind == ReleaseKind.Thrown ? rotation * Vector3.right * (_definition.ThrowTumble * TwoPi) : Vector3.zero;
            _body.WakeUp();

            _live = kind == ReleaseKind.Thrown;
            if (_live)
            {
                _thrower = releasedBy;
                _liveUntil = Time.time + _rules.LiveThrowSeconds;
                _throwerSafeUntil = Time.time + _rules.ThrowerIgnoreSeconds;
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

            if (_definition.ReturnsAfterUsedSeconds <= 0f)
            {
                ServerDropFromHands();
                Despawn();
                return;
            }

            ServerSendAway(_definition.ReturnsAfterUsedSeconds);
        }

        void ServerSendAway(float returnAfterSeconds)
        {
            ServerDropFromHands();
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

        /// <summary>If a player still holds it, takes it out of their hands without throwing it anywhere.</summary>
        void ServerDropFromHands()
        {
            if (_holder.Value == null)
                return;

            _holder.Value = null;
            ApplyState();
            ObserversReleased(ReleaseKind.Knocked);
        }

        void ServerTeleport(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            _body.position = position;
            _body.rotation = rotation;
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

            if (_body.position.y < _homePosition.y - _rules.FallReturnDistance)
                ServerSendAway(_rules.FallReturnSeconds);
        }

        /// <summary>Keeps the solid part at the carrier's hands. A carrier that has gone (left the game) drops it where it is.</summary>
        void FollowHolder()
        {
            if (_carrier == null || !_carrier.IsSpawned)
            {
                ServerDropFromHands();
                return;
            }

            _carrier.GetHoldPose(_definition, false, out Vector3 position, out Quaternion rotation);
            _body.MovePosition(position);
            _body.MoveRotation(rotation);
        }

        // ---- Host: a thrown item landing ------------------------------------------------------

        void OnCollisionEnter(Collision collision)
        {
            if (!_live || !IsServerStarted)
                return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < _rules.MinImpactSpeed)
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

            float shove = Mathf.Min(speed * _body.mass * _rules.ShovePerKgMps, _rules.MaxShoveSpeed);
            motor.ApplyKnockback((direction + Vector3.up * ShoveLift).normalized * shove, _rules.ShoveControlLossSeconds);
            return true;
        }

        bool Hurt(IDamageable target, float speed, Vector3 point, Vector3 direction)
        {
            if (target == null)
                return false;

            float damage = (speed - _rules.MinImpactSpeed) * _body.mass * _rules.DamagePerKgMps;
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

            if (away != _shownAway)
            {
                _shownAway = away;
                if (_visual != null)
                    _visual.gameObject.SetActive(!away);
            }

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
                StartIgnoring(previous.Capsule, _rules.ThrowerIgnoreSeconds);
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
            bool solid = !_away.Value && (!held || _definition.CoverWhileCarried);

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
            if (_visual == null)
                return;

            _visual.GetLocalPositionAndRotation(out _blendFromPosition, out _blendFromRotation);
            _blend = 1f;
        }

        /// <summary>
        /// Visual only, and only running while there is something to show: the item in a
        /// player's hands, the short ease from the hands back onto the solid part after
        /// letting go, or the wait before the thrower can be touched by it again.
        /// </summary>
        void LateUpdate()
        {
            if (_carrier != null)
            {
                if (_visual != null)
                {
                    _carrier.GetHoldPose(_definition, true, out Vector3 position, out Quaternion rotation);
                    _visual.SetPositionAndRotation(position + rotation * _visualRestPosition, rotation * _visualRestRotation);
                }
                return;
            }

            if (_blend > 0f)
            {
                _blend = Mathf.Max(0f, _blend - Time.deltaTime / ReleaseBlendSeconds);
                float t = 1f - _blend;
                _visual.SetLocalPositionAndRotation(
                    Vector3.Lerp(_blendFromPosition, _visualRestPosition, t),
                    Quaternion.Slerp(_blendFromRotation, _visualRestRotation, t));
            }

            if (_ignoring && Time.time >= _ignoreUntil)
                StopIgnoring();

            if (_blend <= 0f && !_ignoring)
                enabled = false;
        }
    }
}
