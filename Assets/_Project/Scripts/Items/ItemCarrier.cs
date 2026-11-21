using System;
using CloseTheDeal.Player;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Items
{
    /// <summary>What the owner is looking at, for the pick-up prompt.</summary>
    public enum PromptKind : byte
    {
        None = 0,
        /// <summary>In reach and liftable: "F · Pick up Chair".</summary>
        PickUp = 1,
        /// <summary>In reach but over the weight limit.</summary>
        TooHeavy = 2
    }

    /// <summary>This lane's four inputs, for naming their keys.</summary>
    public enum ItemAction : byte
    {
        Interact = 0,
        Throw = 1,
        Drop = 2,
        Use = 3
    }

    /// <summary>Why the host refused a request, so the UI can say so.</summary>
    public enum DenyReason : byte
    {
        /// <summary>Another player got there first, or it is no longer there.</summary>
        Gone = 0,
        HandsFull = 1,
        TooHeavy = 2,
        TooFar = 3,
        /// <summary>The player is being thrown about and cannot grab anything.</summary>
        KnockedAbout = 4,
        /// <summary>The item cannot be used here, for example nowhere flat to put it down.</summary>
        CannotUseHere = 5,
        /// <summary>This player let go of it a moment ago and cannot snatch it straight back. No text: it passes in under a second.</summary>
        TooSoon = 6
    }

    /// <summary>Host-authoritative player hand slot for carrying, throwing, and using pickups. Design: docs/systems/ITEMS.md.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor), typeof(CapsuleCollider))]
    public sealed class ItemCarrier : NetworkBehaviour
    {
        [Tooltip("Tuning for reach, weight limits and throwing. Read at runtime, never written.")]
        [SerializeField] CarryProfile _profile;

        [Tooltip("The game's Input System actions asset. Only used to hold fire while the hands are full, so throwing never also shoots.")]
        [SerializeField] InputActionAsset _sharedActions;

        [Header("Hooks for sound, effects and UI (fire on every machine)")]
        [Tooltip("This player took something.")]
        [SerializeField] UnityEvent _onTook;
        [Tooltip("This player let go of what they carried, however it happened.")]
        [SerializeField] UnityEvent _onLetGo;

        const string SharedMapName = "Player";
        const string SharedAttackName = "Attack";

        /// <summary>The hands this client controls. Null on a pure host with no player.</summary>
        public static ItemCarrier Local { get; private set; }

        /// <summary>This machine. Local changed: this client's player spawned (the new hands) or left (null). Bind the HUD here instead of checking Local every frame.</summary>
        public static event Action<ItemCarrier> LocalChanged;

        /// <summary>Host only. Raised after a player takes an item (used by GunPickups to equip guns).</summary>
        public static event Action<ItemCarrier, Pickup> ServerItemTaken;

        /// <summary>Every machine. Returns true when another system (e.g. briefcase) occupies the hands for this pickup.</summary>
        public static Func<ItemCarrier, Pickup, bool> HandsTakenElsewhere;

        /// <summary>Owner only. Returns true when another system claims the interact key this frame.</summary>
        public static Func<ItemCarrier, bool> InteractClaimedElsewhere;

        /// <summary>True when the local player last used a gamepad.</summary>
        public static bool UsingPad => Local != null && Local._reader != null && Local._reader.LastUsedGamepad;

        /// <summary>This machine. The player switched between keyboard and mouse (false) and a gamepad (true).</summary>
        public static event Action<bool> UsingPadChanged;

        /// <summary>The key or button for each of this lane's actions, as the HUD should print it.</summary>
        public static string KeyName(ItemAction action) => UsingPad
            ? action switch { ItemAction.Interact => "Y", ItemAction.Throw => "RT", ItemAction.Drop => "DOWN", _ => "LT" }
            : action switch { ItemAction.Interact => "F", ItemAction.Throw => "LMB", ItemAction.Drop => "G", _ => "RMB" };

        /// <summary>Owner only. What the crosshair is on changed. The item is null when the kind is None.</summary>
        public event Action<PromptKind, ItemDefinition> PromptChanged;
        /// <summary>Every machine. What this player carries changed; null means empty hands.</summary>
        public event Action<Pickup> HeldChanged;
        /// <summary>Owner only. The host refused the last request.</summary>
        public event Action<DenyReason> Denied;

        PlayerMotor _motor;
        Rigidbody _rigidbody;
        CapsuleCollider _capsule;
        Transform _viewBody;
        MotorSpeedOverride _speed;
        Pickup _held;
        float _canTakeAt;
        bool _serverTicking;

        // Owner only.
        InputAction _interact;
        InputAction _throw;
        InputAction _drop;
        InputAction _use;
        InputAction _sharedAttack;
        Collider _lookCollider;
        Pickup _lookPickup;
        PlayerInputReader _reader;
        PromptKind _promptKind;
        Pickup _highlighted;
        ItemDefinition _promptItem;

        public Pickup Held => _held;

        /// <summary>Every machine. How much what is carried weighs on the carrier: 0 is weightless (up to the free weight), 1 is at the weight limit.</summary>
        public float Load { get; private set; }
        public PlayerMotor Motor => _motor;
        public CapsuleCollider Capsule => _capsule;
        public CarryProfile Profile => _profile;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _rigidbody = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            _speed = new MotorSpeedOverride(_motor);
            // Loose props with no rules of their own follow the players' rules.
            if (CarryProfile.Shared == null)
                CarryProfile.Shared = _profile;
            enabled = false;
        }

        void OnDestroy()
        {
            _speed.Dispose();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // The smoothed body other players see; the root itself moves once a tick.
            Transform graphics = NetworkObject.GetGraphicalObject();
            _viewBody = graphics != null ? graphics : transform;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            SetServerTick(_held != null);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            SetServerTick(false);
            // Leaving the game, or the host shutting down: whatever is carried stays in the level.
            ServerForceDrop();
        }

        void SetServerTick(bool active)
        {
            if (_serverTicking == active || TimeManager == null)
                return;

            _serverTicking = active;
            if (active)
                TimeManager.OnTick += ServerTick;
            else
                TimeManager.OnTick -= ServerTick;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Editor/Dev builds keep DevCarry owning F/G so both don't act in one frame; release uses ItemCarrier.
            return;
#endif
#pragma warning disable CS0162 // unreachable in Editor and development builds, see above
            Local = this;
            _reader = GetComponent<PlayerInputReader>();
            if (_reader != null)
                _reader.DeviceChanged += OnDeviceChanged;
            CreateInput();
            enabled = true;
            LocalChanged?.Invoke(this);
#pragma warning restore CS0162
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsOwner)
                return;

            enabled = false;
            if (_reader != null)
                _reader.DeviceChanged -= OnDeviceChanged;
            DisposeInput();
            SetPrompt(PromptKind.None, null);
            if (Local == this)
            {
                Local = null;
                LocalChanged?.Invoke(null);
            }
        }

        void CreateInput()
        {
            // Pad north matches shared actions, briefcase, and lobby (west is Reload).
            _interact = Button("Interact", "<Keyboard>/f", "<Gamepad>/buttonNorth");
            _throw = Button("Throw", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            _drop = Button("Drop", "<Keyboard>/g", "<Gamepad>/dpad/down");
            _use = Button("Use Item", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");

            if (_sharedActions != null)
                _sharedAttack = _sharedActions.FindActionMap(SharedMapName)?.FindAction(SharedAttackName);
        }

        void OnDeviceChanged(bool pad)
        {
            UsingPadChanged?.Invoke(pad);
        }

        static InputAction Button(string name, string binding, string padBinding)
        {
            var action = new InputAction(name, InputActionType.Button, binding);
            action.AddBinding(padBinding);
            action.Enable();
            return action;
        }

        void DisposeInput()
        {
            HoldFire(false);
            _sharedAttack = null;
            _interact?.Dispose();
            _throw?.Dispose();
            _drop?.Dispose();
            _use?.Dispose();
            _interact = _throw = _drop = _use = null;
        }

        void HoldFire(bool hold)
        {
            if (_sharedAttack == null)
                return;

            if (hold)
                _sharedAttack.Disable();
            else if (_sharedAttack.actionMap.enabled)
                _sharedAttack.Enable();
        }

        void Update()
        {
            // The mouse is free exactly while the game menu is open: nothing is picked up or thrown then.
            if (!PlayerInputReader.Captured)
            {
                SetPrompt(PromptKind.None, null);
                return;
            }

            if (_held == null)
                UpdateEmptyHands();
            else
                UpdateFullHands();
        }

        void UpdateEmptyHands()
        {
            Pickup target = InteractClaimed() ? null : FindLookTarget();
            bool handsTaken = target != null && HandsTakenElsewhere != null && HandsTakenElsewhere(this, target);
            UpdatePrompt(handsTaken ? null : target);
            if (!_interact.WasPressedThisFrame() || target == null)
                return;

            if (handsTaken)
                Denied?.Invoke(DenyReason.HandsFull);
            else
                LocalTake(target);
        }

        void UpdateFullHands()
        {
            SetPrompt(PromptKind.None, null);
            if (_throw.WasPressedThisFrame())
                LocalThrow(LocalAim());
            else if (_drop.WasPressedThisFrame())
                LocalDrop();
            else if (_use.WasPressedThisFrame())
                LocalUse();
            else if (_interact.WasPressedThisFrame() && !InteractClaimed())
                Denied?.Invoke(DenyReason.HandsFull);
        }

        bool InteractClaimed() => InteractClaimedElsewhere != null && InteractClaimedElsewhere(this);

        Pickup FindLookTarget()
        {
            PlayerCamera cam = PlayerCamera.Instance;
            if (cam == null)
                return null;

            Transform view = cam.transform;
            if (Physics.Raycast(view.position, view.forward, out RaycastHit hit, _profile.Reach, ItemLayers.LookMask, QueryTriggerInteraction.Ignore))
            {
                Pickup direct = PickupFromHit(hit.collider);
                if (IsPickable(direct))
                    return direct;
            }

            if (Physics.SphereCast(view.position, _profile.LookRadius, view.forward, out hit, _profile.Reach, ItemLayers.LookMask, QueryTriggerInteraction.Ignore))
            {
                Pickup near = PickupFromHit(hit.collider);
                if (IsPickable(near))
                    return near;
                return null;
            }

            _lookCollider = null;
            _lookPickup = null;
            return null;
        }

        Pickup PickupFromHit(Collider collider)
        {
            // Looked up again only when the crosshair moves onto a different collider.
            if (collider != _lookCollider)
            {
                _lookCollider = collider;
                Rigidbody rb = collider.attachedRigidbody;
                _lookPickup = rb != null && rb.TryGetComponent(out Pickup p) ? p : null;
            }

            return _lookPickup;
        }

        static bool IsPickable(Pickup pickup) => pickup != null && pickup.State == PickupState.InWorld && pickup.Offered;

        void UpdatePrompt(Pickup target)
        {
            if (target == null)
            {
                SetPrompt(PromptKind.None, null);
                Highlight(null);
                return;
            }

            bool tooHeavy = target.Definition.WeightKg > _profile.MaxCarryWeight;
            Highlight(tooHeavy ? null : target);
            SetPrompt(tooHeavy ? PromptKind.TooHeavy : PromptKind.PickUp, target.Definition);
        }

        void Highlight(Pickup target)
        {
            if (target == _highlighted)
                return;

            if (_highlighted != null)
                _highlighted.ShowLookedAt(false);
            _highlighted = target;
            if (_highlighted != null)
                _highlighted.ShowLookedAt(true);
        }

        void SetPrompt(PromptKind kind, ItemDefinition item)
        {
            if (kind == PromptKind.None)
                Highlight(null);
            if (kind == _promptKind && item == _promptItem)
                return;

            _promptKind = kind;
            _promptItem = item;
            PromptChanged?.Invoke(kind, item);
        }

        Vector3 LocalAim()
        {
            PlayerCamera cam = PlayerCamera.Instance;
            return cam != null ? cam.transform.forward : transform.forward;
        }

        /// <summary>Owner only. Requests the host to pick up <paramref name="target"/>.</summary>
        public void LocalTake(Pickup target)
        {
            if (IsOwner && target != null)
                ServerRequestTake(target.NetworkObject);
        }

        /// <summary>Owner only. Asks the host to throw what is carried along a world direction.</summary>
        public void LocalThrow(Vector3 aim)
        {
            if (IsOwner)
                ServerRequestThrow(aim);
        }

        /// <summary>Owner only. Asks the host to drop what is carried.</summary>
        public void LocalDrop()
        {
            if (IsOwner)
                ServerRequestDrop();
        }

        /// <summary>Owner only. Asks the host to use what is carried: arm it, pull the pin, put it down.</summary>
        public void LocalUse()
        {
            if (IsOwner)
                ServerRequestUse();
        }

        [ServerRpc]
        void ServerRequestTake(NetworkObject item)
        {
            Pickup pickup = item != null ? item.GetComponent<Pickup>() : null;
            DenyReason? refusal = WhyNotTake(pickup);
            if (refusal.HasValue)
            {
                TargetDenied(Owner, refusal.Value);
                return;
            }

            if (!pickup.ServerHold(this))
            {
                TargetDenied(Owner, DenyReason.Gone);
                return;
            }

            ServerItemTaken?.Invoke(this, pickup);
        }

        DenyReason? WhyNotTake(Pickup pickup)
        {
            if (pickup == null || !pickup.IsSpawned || pickup.State != PickupState.InWorld)
                return DenyReason.Gone;
            if (HandsFullFor(pickup))
                return DenyReason.HandsFull;
            if (_motor.State == MoveState.Knocked)
                return DenyReason.KnockedAbout;
            if (pickup.Definition.WeightKg > _profile.MaxCarryWeight)
                return DenyReason.TooHeavy;
            if (Time.time < _canTakeAt)
                return DenyReason.TooSoon;
            if (!pickup.Offered)
                return DenyReason.Gone;

            return InReach(pickup) ? null : DenyReason.TooFar;
        }

        // Doors share the Prop layer, so use LookMask to block grabs through walls, floors, and locked doors.
        bool InReach(Pickup pickup)
        {
            Vector3 eye = transform.position + Vector3.up * _motor.EyeHeight;
            Vector3 nearest = pickup.Body.ClosestPointOnBounds(eye);
            float reach = _profile.Reach + _profile.ReachTolerance;
            if ((nearest - eye).sqrMagnitude > reach * reach)
                return false;

            Vector3 middle = pickup.Body.worldCenterOfMass;
            if (!Physics.Linecast(eye, middle, out RaycastHit first, ItemLayers.LookMask, QueryTriggerInteraction.Ignore))
                return true;
            return IsPickup(first.collider, pickup)
                || (Physics.Linecast(eye, nearest + (middle - nearest) * 0.1f, out RaycastHit edge, ItemLayers.LookMask, QueryTriggerInteraction.Ignore)
                    && IsPickup(edge.collider, pickup));
        }

        static bool IsPickup(Collider collider, Pickup pickup)
        {
            Rigidbody rb = collider.attachedRigidbody;
            return rb != null && rb.TryGetComponent(out Pickup hit) && hit == pickup;
        }

        bool HandsFullFor(Pickup pickup)
        {
            return _held != null || (HandsTakenElsewhere != null && HandsTakenElsewhere(this, pickup));
        }

        [ServerRpc]
        void ServerRequestThrow(Vector3 aim)
        {
            if (_held == null)
                return;

            bool finite = float.IsFinite(aim.x) && float.IsFinite(aim.y) && float.IsFinite(aim.z);
            Vector3 direction = finite && aim.sqrMagnitude > 0.25f ? aim.normalized : transform.forward;
            Vector3 velocity = direction * _held.Definition.ThrowSpeed + _rigidbody.linearVelocity * _profile.InheritVelocity;
            _held.ServerRelease(ReleaseKind.Thrown, velocity, direction);
        }

        [ServerRpc]
        void ServerRequestDrop()
        {
            if (_held != null)
                _held.ServerRelease(ReleaseKind.Dropped, _rigidbody.linearVelocity * _profile.InheritVelocity);
        }

        [ServerRpc]
        void ServerRequestUse()
        {
            if (_held == null)
                return;

            ItemBehaviour behaviour = _held.Behaviour;
            if (behaviour != null)
                behaviour.ServerUse(this);
        }

        [TargetRpc]
        void TargetDenied(NetworkConnection target, DenyReason reason)
        {
            Denied?.Invoke(reason);
        }

        /// <summary>Host only. Tells this player's UI the host refused something, for item uses that can fail.</summary>
        public void ServerDeny(DenyReason reason)
        {
            if (IsServerStarted && Owner.IsValid)
                TargetDenied(Owner, reason);
        }

        /// <summary>Host only. Drops whatever is carried at the carrier's current pose.</summary>
        public void ServerForceDrop()
        {
            if (!IsServerStarted || _held == null)
                return;

            Vector3 away = _rigidbody.linearVelocity + (transform.forward + Vector3.up).normalized * _profile.KnockedDropSpeed;
            _held.ServerRelease(ReleaseKind.Knocked, away);
        }

        void ServerTick()
        {
            if (_held == null)
            {
                SetServerTick(false);
                return;
            }

            if (_profile.DropWhenKnocked && _motor.State == MoveState.Knocked)
                ServerForceDrop();
        }

        /// <summary>World-space carry pose for visuals (view=true) or solid host physics (view=false), pushed back from walls.</summary>
        public void GetHoldPose(ItemDefinition item, bool view, out Vector3 position, out Quaternion rotation)
        {
            GetHoldPose(item, view, !view || IsOwner, out position, out rotation);
        }

        /// <summary>Same as above, optionally skipping the wall-pushback cast (used for remote carriers' visuals).</summary>
        public void GetHoldPose(ItemDefinition item, bool view, bool checkWalls, out Vector3 position, out Quaternion rotation)
        {
            Vector3 eye;
            Quaternion frame;
            PlayerCamera cam = PlayerCamera.Instance;

            if (view && IsOwner && cam != null)
            {
                cam.transform.GetPositionAndRotation(out eye, out frame);
            }
            else
            {
                Transform body = view ? _viewBody : transform;
                eye = body.position + Vector3.up * _motor.EyeHeight;
                frame = Quaternion.Euler(0f, body.eulerAngles.y, 0f);
            }

            ComputeHoldPose(item, eye, frame, checkWalls, out position, out rotation);
        }

        /// <summary>Host only. Spawn pose for a throw, deriving pitch from <paramref name="aimDirection"/>.</summary>
        internal void GetThrowPose(ItemDefinition item, Vector3 aimDirection, out Vector3 position, out Quaternion rotation)
        {
            Vector3 eye = transform.position + Vector3.up * _motor.EyeHeight;
            float horizontal = Mathf.Sqrt(aimDirection.x * aimDirection.x + aimDirection.z * aimDirection.z);
            float pitch = -Mathf.Atan2(aimDirection.y, horizontal) * Mathf.Rad2Deg;
            Quaternion frame = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f);
            ComputeHoldPose(item, eye, frame, true, out position, out rotation);
        }

        static void ComputeHoldPose(ItemDefinition item, Vector3 eye, Quaternion frame, bool checkWalls, out Vector3 position, out Quaternion rotation)
        {
            Vector3 reach = frame * item.HeldOffset;
            if (checkWalls)
            {
                float distance = reach.magnitude;
                if (distance > 0.001f)
                {
                    Vector3 direction = reach / distance;
                    float wall = NearestSolid(eye, item.HoldClearance, direction, distance);
                    if (wall < distance)
                        reach = direction * wall;
                }
            }

            position = eye + reach;
            rotation = frame * Quaternion.Euler(item.HeldEuler);
        }

        static readonly RaycastHit[] HoldHits = new RaycastHit[32];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Local = null;
            LocalChanged = null;
            ServerItemTaken = null;
            HandsTakenElsewhere = null;
            InteractClaimedElsewhere = null;
            UsingPadChanged = null;
            Array.Clear(HoldHits, 0, HoldHits.Length);
        }

        /// <summary>Host only. Enforces a short retake cooldown after releasing an item.</summary>
        internal void ServerNoteLetGo(float seconds)
        {
            _canTakeAt = Time.time + seconds;
        }

        static float NearestSolid(Vector3 eye, float radius, Vector3 direction, float distance)
        {
            int count = Physics.SphereCastNonAlloc(eye, radius, direction, HoldHits, distance, ItemLayers.LookMask, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = HoldHits[i];
                // Ignore distance == 0 overlaps already touching the eye sphere.
                if (hit.distance > 0f && hit.distance < nearest)
                {
                    Rigidbody rb = hit.collider.attachedRigidbody;
                    if (rb == null || !rb.TryGetComponent<Pickup>(out _))
                        nearest = hit.distance;
                }
            }

            return nearest;
        }

        /// <summary>Called by the item on every machine when it lands in these hands.</summary>
        internal void NotifyHeld(Pickup pickup)
        {
            _held = pickup;
            if (IsServerStarted)
                SetServerTick(true);
            ApplyWeight(pickup.Definition.WeightKg);
            if (IsOwner)
                HoldFire(true);
            HeldChanged?.Invoke(pickup);
            _onTook.Invoke();
        }

        /// <summary>Called by the item on every machine when it leaves these hands.</summary>
        internal void NotifyReleased(Pickup pickup)
        {
            if (_held != pickup)
                return;

            _held = null;
            if (IsServerStarted)
                SetServerTick(false);
            Load = 0f;
            _speed.Clear();
            if (IsOwner)
                HoldFire(false);
            HeldChanged?.Invoke(null);
            _onLetGo.Invoke();
        }

        void ApplyWeight(float weightKg)
        {
            Load = Mathf.InverseLerp(_profile.FreeCarryWeight, _profile.MaxCarryWeight, weightKg);
            _speed.Set(Mathf.Lerp(1f, _profile.SpeedAtMaxWeight, Load), Mathf.Lerp(1f, _profile.JumpAtMaxWeight, Load));
        }
    }
}
