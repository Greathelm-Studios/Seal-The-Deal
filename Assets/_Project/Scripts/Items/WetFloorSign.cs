using System;
using System.Collections.Generic;
using CloseTheDeal.Player;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.Events;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// The wet floor sign: put it down and the floor around it really is wet. Anyone who runs
    /// across it, from either firm, goes flying; walking is safe. Picking the sign up or
    /// knocking it away clears the trap. The host decides every slip; everyone else is told
    /// one synced flag and draws the puddle.
    /// </summary>
    public sealed class WetFloorSign : ItemBehaviour
    {
        [Tooltip("Size of the wet patch and how hard people slip. Read at runtime, never written.")]
        [SerializeField] WetFloorProfile _profile;

        [Tooltip("The puddle drawn on the floor while the sign is down. Hidden otherwise.")]
        [SerializeField] GameObject _puddle;

        [Header("Hooks for sound, effects and UI (fire on every machine)")]
        [Tooltip("The sign was put down and the floor is wet.")]
        [SerializeField] UnityEvent _onPutDown;
        [Tooltip("The wet floor is safe again: the sign was picked up or knocked away.")]
        [SerializeField] UnityEvent _onCleared;
        [Tooltip("Someone ran across the wet floor and slipped.")]
        [SerializeField] UnityEvent _onSlip;

        const float PlaceProbeHeight = 1f;
        const float PlaceProbeDepth = 2.5f;
        const float FlatEnoughDegrees = 20f;
        const float UprightEnough = 0.7f;

        struct SlipTarget
        {
            public PlayerMotor Motor;
            public float SafeUntil;
        }

        static readonly Collider[] Buffer = new Collider[16];

        readonly SyncVar<bool> _down = new();
        readonly List<SlipTarget> _targets = new(8);
        Vector3 _placedAt;
        bool _ticking;

        /// <summary>Every machine. The sign was put down and the floor is wet.</summary>
        public event Action<WetFloorSign> PutDown;
        /// <summary>Every machine. The trap was cleared.</summary>
        public event Action<WetFloorSign> Cleared;
        /// <summary>Every machine. Someone slipped at this world point.</summary>
        public event Action<WetFloorSign, Vector3> Slipped;

        public bool IsDown => _down.Value;

        protected override void Awake()
        {
            base.Awake();
            _down.OnChange += OnDownChanged;
            if (_puddle != null)
                _puddle.SetActive(false);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (_puddle != null)
                _puddle.SetActive(false);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _down.Value = false;
            _placedAt = Vector3.zero;
            _targets.Clear();
            _ticking = false;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            SetServerTick(false);
            _targets.Clear();
        }

        void SetServerTick(bool active)
        {
            if (_ticking == active || TimeManager == null)
                return;

            _ticking = active;
            if (active)
                TimeManager.OnTick += ServerTick;
            else
                TimeManager.OnTick -= ServerTick;
        }

        // ---- Host ----------------------------------------------------------------------------

        /// <summary>Puts the sign down on flat floor just ahead of the carrier, facing them. Refused where there is no such floor.</summary>
        public override void ServerUse(ItemCarrier user)
        {
            Transform body = user.transform;
            Vector3 eye = body.position + Vector3.up * user.Motor.EyeHeight;
            Vector3 probe = body.position + body.forward * _profile.PlaceDistance + Vector3.up * PlaceProbeHeight;
            int world = ItemLayers.WorldMask;

            bool blocked = Physics.Linecast(eye, probe, world, QueryTriggerInteraction.Ignore);
            if (blocked
                || !Physics.Raycast(probe, Vector3.down, out RaycastHit floor, PlaceProbeDepth, world, QueryTriggerInteraction.Ignore)
                || Vector3.Angle(floor.normal, Vector3.up) > FlatEnoughDegrees)
            {
                user.ServerDeny(DenyReason.CannotUseHere);
                return;
            }

            Quaternion facing = Quaternion.Euler(0f, body.eulerAngles.y + 180f, 0f);
            Item.ServerReleaseAt(ReleaseKind.Placed, floor.point, facing, Vector3.zero);
            _placedAt = floor.point;
            _targets.Clear();
            _down.Value = true;
            SetServerTick(true);
        }

        public override void ServerOnTaken(ItemCarrier by)
        {
            _down.Value = false;
            SetServerTick(false);
            _targets.Clear();
        }

        public override void ServerOnSentAway(bool usedUp)
        {
            _down.Value = false;
            SetServerTick(false);
            _targets.Clear();
        }

        public override void ServerOnReturned()
        {
            _down.Value = false;
            SetServerTick(false);
            _targets.Clear();
        }

        void ServerTick()
        {
            if (!_down.Value)
            {
                SetServerTick(false);
                return;
            }

            // Knocked over, shoved aside or gone: the floor is safe again.
            bool moved = (Item.Body.position - _placedAt).sqrMagnitude > _profile.NudgeTolerance * _profile.NudgeTolerance;
            if (moved || transform.up.y < UprightEnough || Item.State != PickupState.InWorld)
            {
                _down.Value = false;
                SetServerTick(false);
                _targets.Clear();
                return;
            }

            int found = Physics.OverlapSphereNonAlloc(_placedAt, _profile.Radius, Buffer, ItemLayers.PlayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found; i++)
                TrySlip(Buffer[i]);
        }

        void TrySlip(Collider collider)
        {
            Rigidbody body = collider.attachedRigidbody;
            if (body == null || !body.TryGetComponent(out PlayerMotor motor))
                return;

            if (motor.State != MoveState.Grounded || motor.PlanarSpeed <= _profile.SlipAboveSpeed)
                return;

            float now = Time.time;
            int foundIndex = -1;
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                SlipTarget entry = _targets[i];
                if (entry.Motor == null || now >= entry.SafeUntil)
                {
                    int last = _targets.Count - 1;
                    _targets[i] = _targets[last];
                    _targets.RemoveAt(last);
                    if (foundIndex == last)
                        foundIndex = i;
                    continue;
                }

                if (entry.Motor == motor)
                    foundIndex = i;
            }

            if (foundIndex >= 0)
                return;

            Vector3 travel = body.linearVelocity;
            travel.y = 0f;
            _targets.Add(new SlipTarget { Motor = motor, SafeUntil = now + _profile.RepeatSeconds });
            motor.ApplyKnockback(travel.normalized * _profile.SlipSpeed + Vector3.up * _profile.SlipLift, _profile.ControlLossSeconds);
            ObserversSlipped(body.position);
        }

        // ---- Every machine -------------------------------------------------------------------

        void OnDownChanged(bool previous, bool next, bool asServer)
        {
            // On the host this runs twice, as server then as client: answer once.
            if (!ShowsChange(asServer))
                return;

            if (_puddle != null)
                _puddle.SetActive(next);

            if (next == previous)
                return;

            if (next)
            {
                ItemAudio.PlayAt(Item.Sounds, Item.Sounds?.WetFloorDown, transform.position);
                PutDown?.Invoke(this);
                _onPutDown.Invoke();
            }
            else
            {
                Cleared?.Invoke(this);
                _onCleared.Invoke();
            }
        }

        [ObserversRpc(RunLocally = true)]
        void ObserversSlipped(Vector3 point, Channel channel = Channel.Unreliable)
        {
            ItemAudio.PlayAt(Item.Sounds, Item.Sounds?.Slip, point);
            Slipped?.Invoke(this, point);
            _onSlip.Invoke();
        }
    }
}
