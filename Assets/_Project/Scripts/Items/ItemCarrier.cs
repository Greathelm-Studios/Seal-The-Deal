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
        CannotUseHere = 5
    }

    /// <summary>
    /// A player's hands: one slot for a carried thing. The owner looks, presses and asks; the
    /// host decides who gets a contested item, when it is thrown and when it is knocked loose.
    /// A client never gives itself anything. Lives on the player prefab.
    /// Design and ownership: docs/systems/ITEMS.md.
    /// </summary>
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

        /// <summary>
        /// Host only. A player has just taken an item. Guns are granted here: the weapon holder
        /// listens, gives the player the gun from pickup.Definition and calls pickup.ServerUseUp().
        /// </summary>
        public static event Action<ItemCarrier, Pickup> ServerItemTaken;

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

        // Owner only.
        InputAction _interact;
        InputAction _throw;
        InputAction _drop;
        InputAction _use;
        InputAction _sharedAttack;
        Collider _lookCollider;
        Pickup _lookPickup;
        PromptKind _promptKind;
        ItemDefinition _promptItem;

        public Pickup Held => _held;
        public PlayerMotor Motor => _motor;
        public CapsuleCollider Capsule => _capsule;
        public CarryProfile Profile => _profile;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _rigidbody = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            _speed = new MotorSpeedOverride(_motor);
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
            TimeManager.OnTick += ServerTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TimeManager.OnTick -= ServerTick;
            // Leaving the game, or the host shutting down: whatever is carried stays in the level.
            ServerForceDrop();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsOwner)
                return;

            Local = this;
            CreateInput();
            enabled = true;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsOwner)
                return;

            if (Local == this)
                Local = null;
            enabled = false;
            DisposeInput();
            SetPrompt(PromptKind.None, null);
        }

        // ---- Owner: input ---------------------------------------------------------------------

        /// <summary>
        /// This lane's own inputs, defined here until they move into the shared actions asset:
        /// interact, throw, drop and use.
        /// </summary>
        void CreateInput()
        {
            _interact = Button("Interact", "<Keyboard>/f", "<Gamepad>/buttonWest");
            _throw = Button("Throw", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
            _drop = Button("Drop", "<Keyboard>/g", "<Gamepad>/dpad/down");
            _use = Button("Use Item", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");

            if (_sharedActions != null)
                _sharedAttack = _sharedActions.FindActionMap(SharedMapName)?.FindAction(SharedAttackName);
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

        /// <summary>Full hands cannot shoot: the throw button is the fire button.</summary>
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
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                SetPrompt(PromptKind.None, null);
                return;
            }

            if (_held == null)
            {
                Pickup target = FindLookTarget();
                UpdatePrompt(target);
                if (_interact.WasPressedThisFrame() && target != null)
                    LocalTake(target);
                return;
            }

            SetPrompt(PromptKind.None, null);
            if (_throw.WasPressedThisFrame())
                LocalThrow(LocalAim());
            else if (_drop.WasPressedThisFrame())
                LocalDrop();
            else if (_use.WasPressedThisFrame())
                LocalUse();
            else if (_interact.WasPressedThisFrame())
                Denied?.Invoke(DenyReason.HandsFull);
        }

        Pickup FindLookTarget()
        {
            PlayerCamera cam = PlayerCamera.Instance;
            if (cam == null)
                return null;

            Transform view = cam.transform;
            if (!Physics.SphereCast(view.position, _profile.LookRadius, view.forward, out RaycastHit hit, _profile.Reach, ItemLayers.LookMask, QueryTriggerInteraction.Ignore))
            {
                _lookCollider = null;
                _lookPickup = null;
                return null;
            }

            // Looked up again only when the crosshair moves onto a different collider.
            if (hit.collider != _lookCollider)
            {
                _lookCollider = hit.collider;
                _lookPickup = hit.collider.GetComponentInParent<Pickup>();
            }

            return _lookPickup != null && _lookPickup.State == PickupState.InWorld ? _lookPickup : null;
        }

        void UpdatePrompt(Pickup target)
        {
            if (target == null)
            {
                SetPrompt(PromptKind.None, null);
                return;
            }

            bool tooHeavy = target.Definition.WeightKg > _profile.MaxCarryWeight;
            SetPrompt(tooHeavy ? PromptKind.TooHeavy : PromptKind.PickUp, target.Definition);
        }

        void SetPrompt(PromptKind kind, ItemDefinition item)
        {
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

        // ---- Owner: asking the host -----------------------------------------------------------

        /// <summary>Owner only. Asks the host for the item. The answer arrives as HeldChanged or Denied.</summary>
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

        /// <summary>The host's rules for a grab, in the order a player would want to hear them.</summary>
        DenyReason? WhyNotTake(Pickup pickup)
        {
            if (pickup == null || !pickup.IsSpawned || pickup.State != PickupState.InWorld)
                return DenyReason.Gone;
            if (_held != null)
                return DenyReason.HandsFull;
            if (_motor.State == MoveState.Knocked)
                return DenyReason.KnockedAbout;
            if (pickup.Definition.WeightKg > _profile.MaxCarryWeight)
                return DenyReason.TooHeavy;

            Vector3 eye = transform.position + Vector3.up * _motor.EyeHeight;
            Vector3 nearest = pickup.Body.ClosestPointOnBounds(eye);
            float reach = _profile.Reach + _profile.ReachTolerance;
            if ((nearest - eye).sqrMagnitude > reach * reach)
                return DenyReason.TooFar;

            // No grabbing through walls or floors: the building must not stand between the eye and the item.
            if (Physics.Linecast(eye, nearest, ItemLayers.WorldMask, QueryTriggerInteraction.Ignore))
                return DenyReason.TooFar;

            return null;
        }

        [ServerRpc]
        void ServerRequestThrow(Vector3 aim)
        {
            if (_held == null)
                return;

            Vector3 direction = aim.sqrMagnitude > 0.25f ? aim.normalized : transform.forward;
            Vector3 velocity = direction * _held.Definition.ThrowSpeed + _rigidbody.linearVelocity * _profile.InheritVelocity;
            _held.ServerRelease(ReleaseKind.Thrown, velocity);
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

            ItemBehaviour behaviour = _held.GetComponent<ItemBehaviour>();
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

        // ---- Host ----------------------------------------------------------------------------

        /// <summary>
        /// Host only. Whatever is carried is knocked out of the hands and stays in the level.
        /// Called when the carrier is knocked about or leaves; call it on death too.
        /// </summary>
        public void ServerForceDrop()
        {
            if (!IsServerStarted || _held == null)
                return;

            Vector3 away = _rigidbody.linearVelocity + (transform.forward + Vector3.up).normalized * _profile.KnockedDropSpeed;
            _held.ServerRelease(ReleaseKind.Knocked, away);
        }

        void ServerTick()
        {
            if (_held != null && _profile.DropWhenKnocked && _motor.State == MoveState.Knocked)
                ServerForceDrop();
        }

        // ---- Every machine -------------------------------------------------------------------

        /// <summary>
        /// Where a carried item sits, in world space. The view pose is what is drawn: the
        /// owner's own camera, pitch included, or another player's smoothed body. The other is
        /// the host's solid pose, from the ticked body, which is where a release starts from.
        /// Either way a wall in front pushes the item back toward the body.
        /// </summary>
        public void GetHoldPose(ItemDefinition item, bool view, out Vector3 position, out Quaternion rotation)
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

            Vector3 reach = frame * item.HeldOffset;
            float distance = reach.magnitude;
            if (distance > 0.001f)
            {
                Vector3 direction = reach / distance;
                if (Physics.SphereCast(eye, item.HoldClearance, direction, out RaycastHit wall, distance, ItemLayers.WorldMask, QueryTriggerInteraction.Ignore))
                    reach = direction * wall.distance;
            }

            position = eye + reach;
            rotation = frame * Quaternion.Euler(item.HeldEuler);
        }

        /// <summary>Called by the item on every machine when it lands in these hands.</summary>
        internal void NotifyHeld(Pickup pickup)
        {
            _held = pickup;
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
            _speed.Clear();
            if (IsOwner)
                HoldFire(false);
            HeldChanged?.Invoke(null);
            _onLetGo.Invoke();
        }

        /// <summary>Heavier things slow the carrier: full speed up to the free weight, the profile's slowest at the weight limit.</summary>
        void ApplyWeight(float weightKg)
        {
            float load = Mathf.InverseLerp(_profile.FreeCarryWeight, _profile.MaxCarryWeight, weightKg);
            _speed.Set(Mathf.Lerp(1f, _profile.SpeedAtMaxWeight, load), Mathf.Lerp(1f, _profile.JumpAtMaxWeight, load));
        }
    }
}
