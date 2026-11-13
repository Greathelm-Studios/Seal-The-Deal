// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "make the dummy and arsenal section be on the ceiling. to access it,
//              you should [take] an elevator" (then: "open rooftop")
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The lift between KitOffice's ground floor and its rooftop range. Its own file and
//              system because nothing else in the game moves the floor under a predicted player:
//              the host decides when a ride starts, and every machine works the car's height out
//              from that one start tick, so the car itself is never sent over the network.
// -----------------------------------------------------------------------------
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace CloseTheDeal.Lift
{
    /// <summary>The host's word on the lift: where the last ride left from and when. Sent only when a ride is scheduled.</summary>
    public struct LiftRide
    {
        /// <summary>Stop the ride leaves from: 0 the lower stop, 1 the upper. Before any ride, the stop the car waits at.</summary>
        public byte FromStop;
        /// <summary>Host tick at which the doors start to close. Meaningless until <see cref="Departed"/> is true.</summary>
        public uint DepartTick;
        /// <summary>False until the first ride is scheduled, so the unsynced default reads as "waiting at the lower stop".</summary>
        public bool Departed;
    }

    /// <summary>
    /// A two-stop lift. Players walk into the car, the doors close, it rides to the other stop and the doors open.
    /// Design, ownership and limits: docs/systems/ELEVATOR.md.
    /// </summary>
    /// <remarks>
    /// <para>Ownership (AGENTS.md §3): the host alone schedules a ride, by writing one <see cref="LiftRide"/>.
    /// Clients never decide anything; they compute the same car height and door positions from the ride's start
    /// tick. Synced: that one value, on change (a few bytes per ride). Nothing is sent per tick.</para>
    /// <para>WHY the car is a function of the tick rather than a streamed transform: the player body is
    /// client-predicted and re-simulated on every reconcile. A streamed car would be in the wrong place during
    /// those replays and the rider would be corrected every tick. A pure function of the tick can be put back
    /// exactly for each replayed tick. The alternative, a closed-door ride where the host teleports the riders,
    /// lost because it cannot carry loose props and hides the ride; it stays the fallback if this jitters.</para>
    /// <para>Call order: the scene builder wires every reference; nothing is resolved at runtime. The car's and
    /// the door leaves' authored positions are read once in Awake and never written back (AGENTS.md §4): the car
    /// and leaves are the declared moving parts.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Elevator : NetworkBehaviour
    {
        /// <summary>Number of stops. Two by design: the smallest lift that does the job.</summary>
        const int StopCount = 2;
        /// <summary>Door leaves per stop: a bi-parting pair.</summary>
        const int LeavesPerStop = 2;
        /// <summary>
        /// Seconds between the host scheduling a ride and the doors starting to move, so the scheduled start
        /// reaches every client before it happens. 0.25 s covers a 250 ms one-way delay, well over a residential link.
        /// </summary>
        const float ScheduleLeadSeconds = 0.25f;
        /// <summary>Most players the overlap checks can report. 8 is the game's player cap (AGENTS.md §2).</summary>
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

        readonly SyncVar<LiftRide> _ride = new();
        readonly Collider[] _hits = new Collider[MaxPlayers];
        readonly Vector3[] _doorClosed = new Vector3[StopCount * LeavesPerStop];

        /// <summary>The car's authored position, world metres: the lower stop. Read once, never written back.</summary>
        Vector3 _carRest;
        /// <summary>Seconds the car spends getting up to speed (and again slowing down), derived from the tuning.</summary>
        float _accelSeconds;
        /// <summary>Seconds at top speed between speeding up and slowing down; 0 on a ride too short to reach it.</summary>
        float _cruiseSeconds;
        /// <summary>Seconds from the doors starting to close to their being fully open at the other stop.</summary>
        float _rideSeconds;
        /// <summary>Seconds the car is actually moving.</summary>
        float _travelSeconds;
        /// <summary>Client only: this machine's tick minus the host tick it stands for, taken from the last reconcile.</summary>
        long _localAheadOfHost;
        bool _haveReconciled;
        /// <summary>True once the car and doors have been put at rest for the current ride, so idle ticks do nothing.</summary>
        bool _restPlaced;
        /// <summary>Host only: ticks a rider or caller has been waiting.</summary>
        int _dwellTicks;

        /// <summary>The stop the car is at or heading to: 0 lower, 1 upper.</summary>
        public int TargetStop => _ride.Value.Departed ? OtherStop(_ride.Value.FromStop) : _ride.Value.FromStop;

        void Awake()
        {
            _carRest = _car.position;
            for (int i = 0; i < _doorLeaves.Length; i++)
                _doorClosed[i] = _doorLeaves[i].localPosition;

            // Speed profile: speed up, hold, slow down. A ride too short to reach top speed has no hold.
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
            // A new session starts with the car waiting at the lower stop, wherever the last one left it.
            _ride.Value = default;
            _dwellTicks = 0;
        }

        void OnRideChanged(LiftRide previous, LiftRide next, bool asServer)
        {
            _restPlaced = false;
        }

        // ---- Ticks ---------------------------------------------------------------------------

        void OnTick()
        {
            uint hostTick = HostTickNow();
            if (IsServerStarted)
                HostStep(hostTick);
            Place(hostTick, false);
        }

        void OnPreReconcile(uint clientTick, uint serverTick)
        {
            // WHY: the reconcile pairs one of this machine's ticks with the host tick that produced the state.
            // The difference is how far ahead this client runs, which turns its own tick into the host's.
            _localAheadOfHost = (long)clientTick - serverTick;
            _haveReconciled = true;
        }

        void OnPreReplicateReplay(uint clientTick, uint serverTick)
        {
            // FishNet is about to re-run physics for a past tick: put the car back where it was then.
            if (!IsServerStarted)
                Place(serverTick, true);
        }

        /// <summary>The host tick this machine's current tick stands for.</summary>
        uint HostTickNow()
        {
            if (IsServerStarted)
                return TimeManager.Tick;
            if (!_haveReconciled)
                return TimeManager.LocalTickToTick(TimeManager.LocalTick);
            long tick = (long)TimeManager.LocalTick - _localAheadOfHost;
            return tick > 0 ? (uint)tick : 0u;
        }

        /// <summary>
        /// Sets the car and doors up for the physics step of host tick <paramref name="hostTick"/>: the step
        /// carries the car to where it is at the next tick, pushing whoever stands on it.
        /// </summary>
        /// <param name="rewind">True in a replay: first put the car where it was at the start of that tick.</param>
        void Place(uint hostTick, bool rewind)
        {
            LiftRide ride = _ride.Value;
            bool resting = !ride.Departed || SecondsInto(ride, hostTick + 1) <= 0f || SecondsInto(ride, hostTick) >= _rideSeconds;
            // Early-out (AGENTS.md §6): a lift standing at a stop with its doors set costs nothing per tick.
            if (resting && _restPlaced)
                return;

            if (rewind)
                _car.position = CarPosition(ride, hostTick);
            _car.MovePosition(CarPosition(ride, hostTick + 1));

            for (int i = 0; i < _doorLeaves.Length; i++)
            {
                float open = DoorOpen(ride, i / LeavesPerStop, hostTick + 1);
                _doorLeaves[i].localPosition = _doorClosed[i] + _doorSlides[i] * open;
            }
            _restPlaced = resting;
        }

        // ---- Host: who starts a ride ---------------------------------------------------------

        void HostStep(uint tick)
        {
            LiftRide ride = _ride.Value;
            if (ride.Departed && SecondsInto(ride, tick) < _rideSeconds)
            {
                _dwellTicks = 0;
                return;
            }

            int here = TargetStop;
            Vector3 cabin = _car.transform.TransformPoint(_cabinCentre);
            bool riders = Physics.OverlapBoxNonAlloc(cabin, _cabinSize * 0.5f, _hits, _car.rotation, _playerMask, QueryTriggerInteraction.Ignore) > 0;
            // Riders win over a caller: the car is theirs until they step out.
            bool called = !riders && Physics.OverlapSphereNonAlloc(_callPoints[OtherStop(here)].position, _callRadius, _hits, _playerMask, QueryTriggerInteraction.Ignore) > 0;
            if (!riders && !called)
            {
                _dwellTicks = 0;
                return;
            }

            _dwellTicks++;
            if (_dwellTicks < TicksFor(_dwellSeconds))
                return;

            _dwellTicks = 0;
            _ride.Value = new LiftRide { FromStop = (byte)here, DepartTick = tick + (uint)TicksFor(ScheduleLeadSeconds), Departed = true };
        }

        // ---- The ride as a function of the tick ----------------------------------------------

        static int OtherStop(int stop) => StopCount - 1 - stop;

        int TicksFor(float seconds) => Mathf.CeilToInt(seconds / (float)TimeManager.TickDelta);

        /// <summary>Seconds since the ride's doors started closing at <paramref name="hostTick"/>; negative before then.</summary>
        float SecondsInto(LiftRide ride, uint hostTick)
        {
            return (float)(((long)hostTick - ride.DepartTick) * TimeManager.TickDelta);
        }

        /// <summary>Where the car's pivot is at <paramref name="hostTick"/>, world metres.</summary>
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

        /// <summary>Metres travelled <paramref name="seconds"/> after the car starts moving: speed up, hold, slow down.</summary>
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

        /// <summary>How open the doors at <paramref name="stop"/> are at <paramref name="hostTick"/>: 0 shut, 1 fully open.</summary>
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
