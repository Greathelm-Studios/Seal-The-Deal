using CloseTheDeal.Player;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace CloseTheDeal.Lift
{
    /// <summary>Host-synced lift state: departure stop, departure host tick, and active ride flag.</summary>
    public struct LiftRide
    {
        public byte FromStop;
        public uint DepartTick;
        public bool Departed;
    }

    /// <summary>Two-stop lift driven deterministically from a host-synced departure tick. Design: docs/systems/ELEVATOR.md.</summary>
    [DisallowMultipleComponent]
    public sealed class Elevator : NetworkBehaviour
    {
        const int StopCount = 2;
        const int LeavesPerStop = 2;
        const float ScheduleLeadSeconds = 0.25f;
        const int MaxPlayers = 8;

        [Header("Parts (wired by the scene builder)")]
        [Tooltip("The car's body. It must be kinematic: the lift moves it, nothing pushes it.")]
        [SerializeField] Rigidbody _car;
        [Tooltip("The four door leaves: the lower stop's pair first, then the upper stop's pair.")]
        [SerializeField] Transform[] _doorLeaves = new Transform[StopCount * LeavesPerStop];
        [Tooltip("How far each leaf slides when it opens, in metres, in its own local space. One per leaf, same order.")]
        [SerializeField] Vector3[] _doorSlides = new Vector3[StopCount * LeavesPerStop];
        [Tooltip("Where a player waits to call the car: one spot per stop, lower stop first, in front of its doors.")]
        [SerializeField] Transform[] _callPoints = new Transform[StopCount];

        [Header("Ride")]
        [Tooltip("How far the upper stop is above the lower one, in metres. The car's placed position is the lower stop.")]
        [Min(0.1f)] [SerializeField] float _travel = 9f;
        [Tooltip("Top speed of the car in metres per second. Higher = a shorter ride.")]
        [Range(0.5f, 6f)] [SerializeField] float _speed = 2.5f;
        [Tooltip("How quickly the car gets up to speed and slows down, in metres per second squared. Keep it well under gravity (9.8) or riders lift off the floor when the car starts down.")]
        [Range(0.5f, 6f)] [SerializeField] float _acceleration = 2f;
        [Tooltip("How long the doors take to open or close, in seconds.")]
        [Range(0.2f, 3f)] [SerializeField] float _doorSeconds = 0.8f;

        [Header("Who starts a ride")]
        [Tooltip("How long a player must stand in the car, or wait at the other stop, before the lift leaves, in seconds. Also how long riders have to step out before it takes them back.")]
        [Range(0.5f, 10f)] [SerializeField] float _dwellSeconds = 2f;
        [Tooltip("Which layers count as players for starting and calling the lift.")]
        [SerializeField] LayerMask _playerMask;
        [Tooltip("Centre of the space inside the car where a rider stands, in metres from the car's pivot.")]
        [SerializeField] Vector3 _cabinCentre = new Vector3(0f, 1.1f, 0f);
        [Tooltip("Size of that space in metres. Keep it inside the car walls so someone outside the doors does not count as riding.")]
        [SerializeField] Vector3 _cabinSize = new Vector3(3.2f, 2f, 3.2f);
        [Tooltip("How close to a call spot a player must stand to call the car, in metres.")]
        [Range(0.5f, 4f)] [SerializeField] float _callRadius = 1.5f;

        const float PollIntervalSeconds = 0.1f;

        readonly SyncVar<LiftRide> _ride = new();
        readonly Collider[] _hits = new Collider[MaxPlayers * 2];
        readonly Vector3[] _doorClosed = new Vector3[StopCount * LeavesPerStop];
        readonly Vector3[] _doorOpenOffset = new Vector3[StopCount * LeavesPerStop];

        Vector3 _carRest;
        float _accelSeconds;
        float _cruiseSeconds;
        float _rideSeconds;
        float _travelSeconds;
        long _localAheadOfHost;
        bool _haveReconciled;
        bool _restPlaced;
        int _dwellTicks;
        int _pollTicks;

        /// <summary>The stop the car is at or heading to: 0 lower, 1 upper.</summary>
        public int TargetStop => _ride.Value.Departed ? OtherStop(_ride.Value.FromStop) : _ride.Value.FromStop;

        void Awake()
        {
            _carRest = _car.position;
            for (int i = 0; i < _doorLeaves.Length; i++)
            {
                _doorClosed[i] = _doorLeaves[i].localPosition;
                // Slide is authored in leaf space (mirrored pair opens along +X), while localPosition is in parent space.
                _doorOpenOffset[i] = _doorLeaves[i].localRotation * _doorSlides[i];
            }

            float accelDistance = 0.5f * _speed * _speed / _acceleration;
            if (2f * accelDistance >= _travel)
            {
                _accelSeconds = Mathf.Sqrt(_travel / _acceleration);
                _cruiseSeconds = 0f;
            }
            else
            {
                _accelSeconds = _speed / _acceleration;
                _cruiseSeconds = (_travel - 2f * accelDistance) / _speed;
            }
            _travelSeconds = 2f * _accelSeconds + _cruiseSeconds;
            _rideSeconds = 2f * _doorSeconds + _travelSeconds;

            _ride.OnChange += OnRideChanged;
        }

        void OnDestroy()
        {
            _ride.OnChange -= OnRideChanged;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            TimeManager.OnTick += OnTick;
            PredictionManager.OnPreReconcile += OnPreReconcile;
            PredictionManager.OnPreReplicateReplay += OnPreReplicateReplay;
            _restPlaced = false;
            _haveReconciled = false;
            _localAheadOfHost = 0;
            _pollTicks = 0;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            TimeManager.OnTick -= OnTick;
            PredictionManager.OnPreReconcile -= OnPreReconcile;
            PredictionManager.OnPreReplicateReplay -= OnPreReplicateReplay;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Reset ride state per session so the car starts waiting at the lower stop.
            _ride.Value = default;
            _dwellTicks = 0;
            _pollTicks = 0;
        }

        void OnRideChanged(LiftRide previous, LiftRide next, bool asServer)
        {
            _restPlaced = false;
        }

        void OnTick()
        {
            uint hostTick = HostTickNow();
            if (IsServerStarted)
                HostStep(hostTick);
            Place(hostTick, false);
        }

        void OnPreReconcile(uint clientTick, uint serverTick)
        {
            // Reconcile pairs clientTick with the host tick so clients can map LocalTick to host tick.
            _localAheadOfHost = (long)clientTick - serverTick;
            _haveReconciled = true;
        }

        void OnPreReplicateReplay(uint clientTick, uint serverTick)
        {
            // FishNet re-runs physics for past ticks during replay; restore the car to that tick first.
            if (!IsServerStarted)
                Place(serverTick, true);
        }

        uint HostTickNow()
        {
            if (IsServerStarted)
                return TimeManager.Tick;
            if (!_haveReconciled)
                return TimeManager.LocalTickToTick(TimeManager.LocalTick);
            long tick = (long)TimeManager.LocalTick - _localAheadOfHost;
            return tick > 0 ? (uint)tick : 0u;
        }

        void Place(uint hostTick, bool rewind)
        {
            LiftRide ride = _ride.Value;
            bool resting = !ride.Departed || SecondsInto(ride, hostTick + 1) <= 0f || SecondsInto(ride, hostTick) >= _rideSeconds;
            // Early-out once the resting pose is applied so idle ticks do no work.
            if (resting && _restPlaced)
                return;

            if (rewind)
                _car.position = CarPosition(ride, hostTick);
            _car.MovePosition(CarPosition(ride, hostTick + 1));

            for (int i = 0; i < _doorLeaves.Length; i++)
            {
                float open = DoorOpen(ride, i / LeavesPerStop, hostTick + 1);
                _doorLeaves[i].localPosition = _doorClosed[i] + _doorOpenOffset[i] * open;
            }
            _restPlaced = resting;
        }

        void HostStep(uint tick)
        {
            LiftRide ride = _ride.Value;
            if (ride.Departed && SecondsInto(ride, tick) < _rideSeconds)
            {
                _dwellTicks = 0;
                _pollTicks = 0;
                return;
            }

            int pollInterval = Mathf.Max(1, TicksFor(PollIntervalSeconds));
            _pollTicks++;
            if (_pollTicks < pollInterval)
                return;

            _pollTicks = 0;

            int here = TargetStop;
            Vector3 cabin = _car.transform.TransformPoint(_cabinCentre);
            int cabinHits = Physics.OverlapBoxNonAlloc(cabin, _cabinSize * 0.5f, _hits, _car.rotation, _playerMask, QueryTriggerInteraction.Ignore);
            bool riders = HasPlayer(cabinHits);
            // Riders take priority over callers until they step out.
            bool called = !riders && HasPlayer(Physics.OverlapSphereNonAlloc(_callPoints[OtherStop(here)].position, _callRadius, _hits, _playerMask, QueryTriggerInteraction.Ignore));
            if (!riders && !called)
            {
                _dwellTicks = 0;
                return;
            }

            _dwellTicks += pollInterval;
            if (_dwellTicks < TicksFor(_dwellSeconds))
                return;

            _dwellTicks = 0;
            _ride.Value = new LiftRide { FromStop = (byte)here, DepartTick = tick + (uint)TicksFor(ScheduleLeadSeconds), Departed = true };
        }

        bool HasPlayer(int hitCount)
        {
            for (int i = 0; i < hitCount; i++)
            {
                Rigidbody body = _hits[i].attachedRigidbody;
                if (body != null && body.TryGetComponent<PlayerMotor>(out _))
                    return true;
            }
            return false;
        }

        static int OtherStop(int stop) => StopCount - 1 - stop;

        int TicksFor(float seconds) => Mathf.CeilToInt(seconds / (float)TimeManager.TickDelta);

        float SecondsInto(LiftRide ride, uint hostTick)
        {
            return (float)(((long)hostTick - ride.DepartTick) * TimeManager.TickDelta);
        }

        Vector3 CarPosition(LiftRide ride, uint hostTick)
        {
            float height = ride.FromStop == 0 ? 0f : _travel;
            if (ride.Departed)
            {
                float moved = DistanceAfter(SecondsInto(ride, hostTick) - _doorSeconds);
                height = ride.FromStop == 0 ? moved : _travel - moved;
            }
            return _carRest + Vector3.up * height;
        }

        float DistanceAfter(float seconds)
        {
            if (seconds <= 0f)
                return 0f;
            if (seconds >= _travelSeconds)
                return _travel;

            float topSpeed = _acceleration * _accelSeconds;
            float accelDistance = 0.5f * _acceleration * _accelSeconds * _accelSeconds;
            if (seconds < _accelSeconds)
                return 0.5f * _acceleration * seconds * seconds;
            if (seconds < _accelSeconds + _cruiseSeconds)
                return accelDistance + topSpeed * (seconds - _accelSeconds);

            float left = _travelSeconds - seconds;
            return _travel - 0.5f * _acceleration * left * left;
        }

        float DoorOpen(LiftRide ride, int stop, uint hostTick)
        {
            if (!ride.Departed)
                return stop == ride.FromStop ? 1f : 0f;

            float seconds = SecondsInto(ride, hostTick);
            if (stop == ride.FromStop)
                return 1f - Mathf.Clamp01(seconds / _doorSeconds);
            return Mathf.Clamp01((seconds - _doorSeconds - _travelSeconds) / _doorSeconds);
        }
    }
}
