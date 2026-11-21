using System;
using CloseTheDeal.Core;
using CloseTheDeal.Player;
using CloseTheDeal.Props;
using CloseTheDeal.Tower;
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
        bool _notThrowable;
        Renderer[] _lookRenderers;

        // A client, after a plain carried thing is let go: its solid part is still where it was
        // taken until the host's new pose arrives, so the drawn item waits where it left the hands.
        bool _awaitingPose;
        float _awaitUntil;
        Vector3 _rootAtRelease;
        Vector3 _rootWhileHeld;
        Vector3 _releasedAtPosition;
        Quaternion _releasedAtRotation;
        const float AwaitPoseSeconds = 0.4f;
        Vector3 _lastHoldPosition;
        Quaternion _lastHoldRotation = Quaternion.identity;
        Vector3 _lastVelocity;
        float _returnAt;

        // A prop with no Visual child: its own renderers stay on the solid part, and a copy of
        // them (built the first time it is carried) is what is shown in the hands.
        ItemDefinition _derivedDefinition;
        Renderer[] _ownRenderers;
        bool[] _ownRendererEnabled;
        Transform _handsCopy;
        static CarryProfile _defaultRules;
        bool _quiet;

        // Drawn only: the ease from where it lay into the hands, and the owner's view lag.
        float _takeLeft;
        float _takeSeconds;
        Vector3 _takeFromPosition;
        Quaternion _takeFromRotation;
        Quaternion _swayFrame;
        bool _swaying;

        // Where the item's pivot was drawn in the hands this frame, for the hand seats.
        Vector3 _drawnPosition;
        Quaternion _drawnRotation = Quaternion.identity;

        /// <summary>Every machine. A player took the item.</summary>
        public event Action<Pickup, ItemCarrier> Taken;
        /// <summary>Every machine. The item left a player's hands, and how.</summary>
        public event Action<Pickup, ReleaseKind> Released;
        /// <summary>Every machine. The thrown item hit something at this speed, in metres per second.</summary>
        public event Action<Pickup, float> Impact;
        /// <summary>Every machine. The item is back where it started.</summary>
        public event Action<Pickup> Returned;

        /// <summary>
        /// Host only, static. A thrown item landed on a player or on something that takes damage:
        /// the item, the thrower's client id (-1 if unknown), what it hit, and the damage done
        /// (0 for a shove alone). For match records ("throws landed"). Fires once per throw.
        /// </summary>
        public static event Action<Pickup, int, GameObject, float> ServerThrowLanded;

        /// <summary>The hand-made definition if this prefab has one, otherwise one worked out from the prop itself (LooseItemDefinition).</summary>
        public ItemDefinition Definition => _definition != null ? _definition : _derivedDefinition != null ? _derivedDefinition : Derive();

        /// <summary>This prefab's own carry rules, else the players' (CarryProfile.Shared), else the built-in defaults.</summary>
        public CarryProfile Rules => _rules != null ? _rules : CarryProfile.Shared != null ? CarryProfile.Shared : DefaultRules();

        /// <summary>The sounds this item makes: its definition's own set, else the carry rules' shared one. May be null.</summary>
        public ItemSoundSet Sounds => Definition.Sounds != null ? Definition.Sounds : Rules.Sounds;

        /// <summary>True when nobody wrote a definition for this prop and its name, hold and throw are worked out from its size and mass.</summary>
        public bool IsDerived => _definition == null;
        public Rigidbody Body => _body;
        public ItemCarrier Holder => _carrier;
        internal ItemBehaviour Behaviour => _behaviour;
        public PickupState State => _away.Value ? PickupState.Away : _holder.Value != null ? PickupState.Held : PickupState.InWorld;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ServerThrowLanded = null;
            if (_defaultRules != null)
            {
                DestroyImmediate(_defaultRules);
                _defaultRules = null;
            }
        }

        /// <summary>This machine only: brightens the item while the local player's prompt is about it.</summary>
        public void ShowLookedAt(bool on)
        {
            _lookRenderers ??= LookRenderers();
            LookHighlight.Set(_lookRenderers, on);
        }

        /// <summary>What is drawn for it where it stands: its Visual child's renderers, or its own (never the copy in the hands).</summary>
        Renderer[] LookRenderers() => _visual != null ? _visual.GetComponentsInChildren<Renderer>(true) : _ownRenderers;

        /// <summary>False for a prop marked NotThrowable: nobody is offered it, and the host refuses it.</summary>
        public bool Offered => !_notThrowable;

        /// <summary>Host only. The client id of whoever held it last, or -1 if nobody has.</summary>
        public int LastHolderClientId => _lastHolderClientId;

        void Awake()
        {
            _prop = GetComponent<Prop>();
            _body = GetComponent<Rigidbody>();
            _behaviour = GetComponent<ItemBehaviour>();

            _notThrowable = GetComponent<CloseTheDeal.Props.NotThrowable>() != null;
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
            _holder.Value = null;
            _away.Value = false;
            _lastHolderClientId = -1;
            _live = false;
            _liveUntil = 0f;
            _throwerSafeUntil = 0f;
            _thrower = null;
            _returnAt = 0f;
            _lastVelocity = Vector3.zero;
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
            // Joining: whoever already holds it took it earlier, so no grab sound now.
            _quiet = true;
            ApplyState();
            _quiet = false;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            StopIgnoring();
            ItemCarrier old = _carrier;
            _carrier = null;
            _blend = 0f;
            _awaitingPose = false;
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
            _carrier.ServerNoteLetGo(Rules.RetakeSeconds);
            // Moved to where it is let go before the hands empty, so the ease back onto the solid
            // part starts from the right place (a plain carried thing was left where it was taken).
            ServerTeleport(position, rotation);
            _holder.Value = null;
            ApplyState();
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

            ServerSendAway(Definition.ReturnsAfterUsedSeconds, true);
        }

        /// <summary>Out of play until returnAfterSeconds have passed. usedUp: it was spent (exploded, ran empty), not lost out of the building.</summary>
        void ServerSendAway(float returnAfterSeconds, bool usedUp)
        {
            ServerDropFromHands(false);
            // An armed case or a venting extinguisher stops: nothing goes on happening out of play.
            if (_behaviour != null)
                _behaviour.ServerOnSentAway(usedUp);
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

            if (OutOfTheBuilding(_body.position))
                ServerSendAway(Rules.FallReturnSeconds, false);
        }

        /// <summary>
        /// Host only. Gone from the building: below the ground, or outside both towers' footprints
        /// and fallen below where it started (out of a window, into the street). Carried down
        /// a stairwell or left on a skybridge at its own level, it stays. With no tower built
        /// (a test scene), anything far below where it started counts.
        /// The footprint is the same two-tower layout Switchboard's TowerFrame packs into:
        /// tower-local X within half the width, |Z| from half the gap to the back of the spine.
        /// </summary>
        bool OutOfTheBuilding(Vector3 position)
        {
            CarryProfile rules = Rules;
            TowerBuilder tower = TowerBuilder.Instance;
            if (tower == null || !tower.IsBuilt || tower.Profile == null)
                return position.y < _homePosition.y - rules.FallReturnDistance;

            Vector3 local = tower.transform.InverseTransformPoint(position);
            if (local.y < -rules.BelowGroundMetres)
                return true;

            TowerProfile shape = tower.Profile;
            float side = Mathf.Abs(local.z);
            bool inside = Mathf.Abs(local.x) <= shape.Width * 0.5f
                && side >= shape.Gap * 0.5f
                && side <= shape.Gap * 0.5f + shape.MainDepth + shape.SpineDepth;
            return !inside && position.y < _homePosition.y - rules.OutsideDropMetres;
        }

        /// <summary>Keeps the solid part at the carrier's hands. A carrier that has gone (left the game) drops it where it is.</summary>
        void FollowHolder()
        {
            if (_carrier == null || !_carrier.IsSpawned)
            {
                // Left where the hands last were, not where it was picked up.
                if (!SolidFollowsHands)
                    ServerTeleport(_lastHoldPosition, _lastHoldRotation);
                ServerDropFromHands(true);
                return;
            }

            _carrier.GetHoldPose(Definition, false, out _lastHoldPosition, out _lastHoldRotation);
            if (!SolidFollowsHands)
                return;

            _body.MovePosition(_lastHoldPosition);
            _body.MoveRotation(_lastHoldRotation);
        }

        /// <summary>
        /// Whether the solid part must travel with the carrier. A shield must, to stop shots; a
        /// gadget must, so its sound, smoke and blast are where it is. A plain carried thing has
        /// no colliders on, every machine draws it from its carrier and a release starts from the
        /// hands, so its solid part stays still: Switchboard sees it at rest and sends nothing.
        /// </summary>
        bool SolidFollowsHands => Definition.CoverWhileCarried || _behaviour != null;

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

            LandOn(collision, motor, speed);
            ObserversImpact(speed);
        }

        /// <summary>Shoves a player it hit and hurts anything that takes damage. One throw lands once: after that it is furniture again.</summary>
        void LandOn(Collision collision, PlayerMotor motor, float speed)
        {
            ContactPoint contact = collision.GetContact(0);
            Vector3 direction = _lastVelocity.sqrMagnitude > 0.01f ? _lastVelocity.normalized : -contact.normal;
            bool shoved = Shove(motor, speed, direction);
            float damage = Hurt(collision.collider.GetComponentInParent<IDamageable>(), speed, contact.point, direction);
            if (!shoved && damage < 0f)
                return;

            _live = false;
            Rigidbody struck = collision.collider.attachedRigidbody;
            ServerThrowLanded?.Invoke(this, _lastHolderClientId, struck != null ? struck.gameObject : collision.collider.gameObject, Mathf.Max(0f, damage));
        }

        bool Shove(PlayerMotor motor, float speed, Vector3 direction)
        {
            if (motor == null)
                return false;

            float shove = Mathf.Min(speed * _body.mass * Rules.ShovePerKgMps, Rules.MaxShoveSpeed);
            motor.ApplyKnockback((direction + Vector3.up * ShoveLift).normalized * shove, Rules.ShoveControlLossSeconds);
            return true;
        }

        /// <summary>The damage done, or -1 when what it hit takes no damage.</summary>
        float Hurt(IDamageable target, float speed, Vector3 point, Vector3 direction)
        {
            if (target == null)
                return -1f;

            float damage = (speed - Rules.MinImpactSpeed) * _body.mass * Rules.DamagePerKgMps;
            target.ApplyDamage(damage, point, direction, _lastHolderClientId);
            return damage;
        }

        // ---- Every machine: showing the state -------------------------------------------------

        [ObserversRpc(RunLocally = true)]
        void ObserversReleased(ReleaseKind kind)
        {
            // From where it is drawn (the hands): on a client the solid part may not have caught up yet.
            Transform shown = Shown;
            ItemAudio.PlayAt(Sounds, kind == ReleaseKind.Thrown ? Sounds?.Throw : Sounds?.Drop, shown != null ? shown.position : transform.position);
            Released?.Invoke(this, kind);
            if (kind == ReleaseKind.Thrown)
                _onThrown.Invoke();
            else
                _onDropped.Invoke();
        }

        [ObserversRpc(RunLocally = true)]
        void ObserversImpact(float speed)
        {
            PlayImpact(speed);
            Impact?.Invoke(this, speed);
            _onImpact.Invoke(speed);
        }

        [ObserversRpc(RunLocally = true)]
        void ObserversReturned()
        {
            Returned?.Invoke(this);
            _onReturned.Invoke();
        }

        /// <summary>Heavy things thud, light things clatter; harder hits are louder.</summary>
        void PlayImpact(float speed)
        {
            ItemSoundSet sounds = Sounds;
            if (sounds == null)
                return;

            AudioClip clip = Definition.WeightKg >= sounds.HeavyFromKg ? sounds.ImpactHeavy : sounds.ImpactLight;
            float loudness = Mathf.InverseLerp(Rules.MinImpactSpeed * 0.5f, sounds.FullVolumeImpactSpeed, speed);
            ItemAudio.PlayAt(sounds, clip, transform.position, Mathf.Max(0.2f, loudness));
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
            enabled = _carrier != null || _blend > 0f || _ignoring || _awaitingPose;
        }

        void ChangeCarrier(ItemCarrier next)
        {
            ItemCarrier previous = _carrier;
            _carrier = next;
            StopIgnoring();

            if (previous != null)
            {
                previous.NotifyReleased(this);
                if (!IsServerStarted && !SolidFollowsHands && Shown != null)
                    AwaitHostPose();
                else
                    StartReleaseBlend();
                // Enabled first: turning a collider on forgets who it was told to ignore.
                RefreshColliders();
                StartIgnoring(previous.Capsule, Rules.ThrowerIgnoreSeconds);
            }

            if (next != null)
            {
                _blend = 0f;
                _awaitingPose = false;
                next.NotifyHeld(this);
                ShowLookedAt(false);
                _rootWhileHeld = transform.position;
                StartTake(next);
                if (!_quiet)
                    ItemAudio.PlayAt(Sounds, Sounds?.PickUp, transform.position);
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

        /// <summary>Holds the drawn item where it was let go until the solid part has moved (or a moment has passed), then eases onto it.</summary>
        bool StillAwaitingPose(Transform shown)
        {
            shown.SetPositionAndRotation(_releasedAtPosition, _releasedAtRotation);
            bool arrived = (transform.position - _rootAtRelease).sqrMagnitude > 0.0001f;
            if (!arrived && Time.time < _awaitUntil)
                return true;

            _awaitingPose = false;
            StartReleaseBlend();
            return false;
        }

        void AwaitHostPose()
        {
            Shown.GetPositionAndRotation(out _releasedAtPosition, out _releasedAtRotation);
            // Where the solid part sat while carried (a plain carried thing does not move): if the
            // host's new pose already arrived before the hands emptied, it is not waited for.
            _rootAtRelease = _rootWhileHeld;
            _awaitUntil = Time.time + AwaitPoseSeconds;
            _awaitingPose = true;
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
            bool inHands = !away && (_carrier != null || _blend > 0f || _awaitingPose);
            if (inHands && _handsCopy == null)
                _handsCopy = HandsCopy.Build(transform, _ownRenderers, _ownRendererEnabled);
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

        /// <summary>Starts the ease from where the item lay into the hands. Not for a joiner seeing an item already held.</summary>
        void StartTake(ItemCarrier carrier)
        {
            _swaying = false;
            _takeLeft = 0f;
            if (_quiet)
                return;

            CarryProfile rules = Rules;
            _takeSeconds = Mathf.Lerp(rules.TakeSecondsLight, rules.TakeSecondsHeavy, carrier.Load);
            _takeLeft = _takeSeconds;
            _takeFromPosition = transform.TransformPoint(_visualRestPosition);
            _takeFromRotation = transform.rotation * _visualRestRotation;
        }

        /// <summary>
        /// Where the item is drawn in the hands this frame. For the carrier's own view, it trails
        /// a fast turn by up to a few degrees, further for heavier things; just after a take it
        /// eases in from where it lay.
        /// </summary>
        void ShowInHands(Transform shown)
        {
            _carrier.GetHoldPose(Definition, true, out Vector3 position, out Quaternion rotation);
            Vector3 targetPosition = position + rotation * _visualRestPosition;
            Quaternion targetRotation = rotation * _visualRestRotation;

            PlayerCamera cam = _carrier.IsOwner ? PlayerCamera.Instance : null;
            if (cam != null)
                Sway(cam.transform, ref targetPosition, ref targetRotation);

            if (_takeLeft > 0f)
            {
                _takeLeft = Mathf.Max(0f, _takeLeft - Time.deltaTime);
                float t = 1f - _takeLeft / _takeSeconds;
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                targetPosition = Vector3.LerpUnclamped(_takeFromPosition, targetPosition, eased);
                targetRotation = Quaternion.SlerpUnclamped(_takeFromRotation, targetRotation, eased);
            }

            shown.SetPositionAndRotation(targetPosition, targetRotation);
            _drawnRotation = targetRotation * Quaternion.Inverse(_visualRestRotation);
            _drawnPosition = targetPosition - _drawnRotation * _visualRestPosition;
        }

        /// <summary>
        /// Every machine, while carried: where the carrier's hands go on the item as drawn this
        /// frame, in world space (Definition's seats; a loose prop's are worked out from its size).
        /// A seat's +Y is where the palm faces and +Z where the fingers point. Valid after this
        /// item's LateUpdate (execution order 100); false when nobody holds it.
        /// </summary>
        public bool TryGetHandSeats(out Pose main, out Pose support, out bool twoHanded)
        {
            ItemDefinition item = Definition;
            twoHanded = item.TwoHanded;
            main = Seat(item.MainHandSeat, item.MainHandEuler);
            support = twoHanded ? Seat(item.SupportHandSeat, item.SupportHandEuler) : main;
            return _carrier != null;
        }

        Pose Seat(Vector3 local, Vector3 euler) => new(_drawnPosition + _drawnRotation * local, _drawnRotation * Quaternion.Euler(euler));

        /// <summary>Swings the held pose round the eye so it lags the view's turn, heavier things more.</summary>
        void Sway(Transform view, ref Vector3 position, ref Quaternion rotation)
        {
            CarryProfile rules = Rules;
            Quaternion frame = view.rotation;
            if (!_swaying)
            {
                _swayFrame = frame;
                _swaying = true;
            }

            float follow = Mathf.Lerp(rules.SwayFollowLight, rules.SwayFollowHeavy, _carrier.Load);
            _swayFrame = Quaternion.Slerp(_swayFrame, frame, 1f - Mathf.Exp(-follow * Time.deltaTime));
            _swayFrame = Quaternion.RotateTowards(frame, _swayFrame, rules.MaxSwayDegrees);

            Quaternion lag = _swayFrame * Quaternion.Inverse(frame);
            Vector3 eye = view.position;
            position = eye + lag * (position - eye);
            rotation = lag * rotation;
        }

        /// <summary>Eases the drawn item from where it was let go back onto the solid part.</summary>
        void StepReleaseBlend(Transform shown)
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
                    ShowInHands(shown);
                return;
            }

            if (_awaitingPose && StillAwaitingPose(shown))
                return;

            if (_blend > 0f)
                StepReleaseBlend(shown);

            SleepWhenDone();
        }

        /// <summary>Ends the thrower's grace once it runs out, and stops running once nothing is left to show.</summary>
        void SleepWhenDone()
        {
            if (_ignoring && Time.time >= _ignoreUntil)
                StopIgnoring();

            if (_blend <= 0f && !_ignoring && !_awaitingPose)
                enabled = false;
        }
    }
}
