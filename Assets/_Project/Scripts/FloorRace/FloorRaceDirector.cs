using System;
using CloseTheDeal.Combat;
using CloseTheDeal.FloorGen;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Net;
using CloseTheDeal.Profile;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace CloseTheDeal.FloorRace
{
    public enum RaceStage : byte
    {
        /// <summary>Before the host starts: players walk the first floor.</summary>
        Idle = 0,
        Playing = 1,
        /// <summary>The lift is full and its doors are closing.</summary>
        Departing = 2,
        /// <summary>The next floor is being built on every machine; players are still on the old one.</summary>
        Building = 3,
        /// <summary>Everyone is on the new floor; the briefcase firm waits behind closed lift doors.</summary>
        HeadStart = 4
    }

    /// <summary>The floor race as a whole. Synced host to everyone, only when it changes.</summary>
    public struct FloorRaceState
    {
        public uint Seed;
        /// <summary>The floor in play, 1 = the ground floor.</summary>
        public byte Floor;
        public RaceStage Stage;
        /// <summary>Host tick at which this stage began.</summary>
        public uint StageTick;
        /// <summary>The firm that took the lift up to this floor, or MatchDirector.NoFirm on floor 1.</summary>
        public byte LiftFirm;
    }

    /// <summary>Host-synced state and rules of the floor race: one briefcase, a hidden lift per generated floor. Design: docs/systems/FLOOR_RACE.md.</summary>
    [DisallowMultipleComponent]
    public sealed partial class FloorRaceDirector : NetworkBehaviour, IMatchRuleset
    {
        const float BuildingMargin = 16f;

        public static FloorRaceDirector Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            HealthCache.Clear();
        }

        /// <summary>Previous state, new state. Raised on every machine.</summary>
        public event Action<FloorRaceState, FloorRaceState> OnStateChanged;

        /// <summary>A floor finished building on this machine.</summary>
        public event Action<BuiltFloor> OnFloorBuilt;

        /// <summary>The local player was paid: amount, new total, the floor it was for.</summary>
        public event Action<int, int, int> OnCashEarned;

        [Tooltip("Tuning for the mode: lift, head start, respawn, loot and cash. Read at runtime, never written.")]
        [SerializeField] FloorRaceProfile _profile;

        [Tooltip("Builds each generated floor from kit prefabs.")]
        [SerializeField] FloorBuilder _builder;

        readonly SyncVar<FloorRaceState> _state = new();

        BuiltFloor _floor;
        BuiltFloor _oldFloor;
        bool _networked;

        public FloorRaceProfile Profile => _profile;
        public FloorRaceState State => _state.Value;
        public RaceStage Stage => _state.Value.Stage;
        public int Floor => _state.Value.Floor;
        public int FloorCount => _profile.Floors.FloorCount;
        public bool IsTopFloor => Floor >= FloorCount;

        /// <summary>The floor in play on this machine, or null before it is built.</summary>
        public BuiltFloor Current => _floor;

        /// <summary>Seconds since the current stage began, by the host's clock.</summary>
        public float StageSeconds
        {
            get
            {
                if (!_networked)
                    return 0f;
                uint now = TimeManager.Tick, began = _state.Value.StageTick;
                return now > began ? (float)((now - began) * TimeManager.TickDelta) : 0f;
            }
        }

        /// <summary>Whole seconds until the briefcase firm's lift doors open; 0 outside the head start.</summary>
        public int HeadStartSecondsLeft => Stage == RaceStage.HeadStart ? Mathf.Max(0, Mathf.CeilToInt(_profile.HeadStartSeconds - StageSeconds)) : 0;

        void Awake()
        {
            Instance = this;
            MatchDirector.Ruleset = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (ReferenceEquals(MatchDirector.Ruleset, this))
                MatchDirector.Ruleset = null;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (_profile == null || _profile.Floors == null || _builder == null)
            {
                Debug.LogError("[Floor Race] The Floor Race object is missing its profile, its floor settings or its floor builder. Run Seal the Deal > Floor Race > Build Scene.");
                enabled = false;
                return;
            }

            _networked = true;
            Health.RespawnsHeld = true;
            _state.OnChange += OnStateSynced;
            TimeManager.OnTick += OnTick;
            PropStream.UseBuilding(BuildingFrame());
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (_networked && !IsServerStarted)
                ShowFloor(_state.Value);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (!_networked)
                return;

            _networked = false;
            Health.RespawnsHeld = false;
            _state.OnChange -= OnStateSynced;
            TimeManager.OnTick -= OnTick;
            PropStream.ClearBuilding();
            _builder.TearDown(_oldFloor);
            _builder.TearDown(_floor);
            _oldFloor = null;
            _floor = null;
        }

        // The prop stream packs positions into this box: the whole plate, every storey, plus room for a thrown prop.
        TowerFrame BuildingFrame()
        {
            FloorGenProfile gen = _profile.Floors;
            float width = gen.Cols * FloorLayout.Cell, depth = gen.Rows * FloorLayout.Cell;
            float top = gen.FloorCount * _builder.FloorPitch;
            var bounds = new Bounds(
                new Vector3(width * 0.5f, top * 0.5f, -depth * 0.5f),
                new Vector3(width + 2f * BuildingMargin, top + 2f * BuildingMargin, depth + 2f * BuildingMargin));
            return TowerFrame.FromBounds(_builder.transform, bounds, _builder.FloorPitch, gen.FloorCount);
        }

        void OnStateSynced(FloorRaceState previous, FloorRaceState next, bool asServer)
        {
            // The host hears SyncVar changes twice (server, then client); handle once.
            if (asServer != IsServerStarted)
                return;

            ShowFloor(next);
            OnStateChanged?.Invoke(previous, next);
        }

        // Builds the state's floor if this machine does not have it yet. Going up one keeps the old floor standing until the move is over.
        void ShowFloor(FloorRaceState state)
        {
            if (state.Seed == 0 || state.Floor == 0)
                return;
            if (_floor != null && _floor.Layout.Seed == state.Seed && _floor.Layout.Floor == state.Floor)
                return;

            bool goingUp = _floor != null && _floor.Layout.Seed == state.Seed && state.Floor == _floor.Layout.Floor + 1;
            _builder.TearDown(_oldFloor);
            _oldFloor = null;
            if (goingUp)
                _oldFloor = _floor;
            else
                _builder.TearDown(_floor);

            FloorLayout layout = FloorGenerator.Generate(state.Seed, state.Floor, _profile.Floors);
            if (!layout.Valid)
                Debug.LogError($"[Floor Race] Floor {state.Floor} of seed {state.Seed} failed its checks: {string.Join(" ", layout.Problems)}");
            _floor = _builder.Build(layout);
            if (_floor != null)
                OnFloorBuilt?.Invoke(_floor);
        }

        void OnTick()
        {
            if (_floor == null)
                return;

            FloorRaceState state = _state.Value;
            float since = StageSeconds;
            float doorSeconds = _profile.DoorSeconds;
            if (_oldFloor != null)
            {
                _oldFloor.ExitDoors?.Set(0f);
                if (state.Stage != RaceStage.Building && (state.Stage != RaceStage.HeadStart || since >= _profile.TeardownSeconds))
                {
                    _builder.TearDown(_oldFloor);
                    _oldFloor = null;
                }
            }

            _floor.ExitDoors?.Set(state.Stage == RaceStage.Departing ? 1f - Mathf.Clamp01(since / doorSeconds) : 1f);
            float arrival = state.Stage == RaceStage.Playing || state.Stage == RaceStage.Departing
                ? (state.Stage == RaceStage.Playing ? Mathf.Clamp01(since / doorSeconds) : 1f)
                : state.Stage == RaceStage.Idle ? 1f : 0f;
            _floor.ArrivalDoors?.Set(arrival);
        }

        /// <summary>The floor players are standing on: the old one while the next is still being built above it.</summary>
        public BuiltFloor StandingFloor => Stage == RaceStage.Building && _oldFloor != null ? _oldFloor : _floor;

        /// <summary>True when this world position is inside the exit lift's car on the floor in play.</summary>
        public bool InExitLift(Vector3 world)
        {
            BuiltFloor floor = StandingFloor;
            if (floor == null || !floor.Layout.HasExitLift)
                return false;

            Vector3 plan = floor.Plan(world);
            FloorLift lift = floor.Layout.ExitLift;
            Vector3 size = _profile.CabinSize;
            bool turned = (lift.DoorSide & 1) == 1;
            float halfX = (turned ? size.z : size.x) * 0.5f, halfY = (turned ? size.x : size.z) * 0.5f;
            float above = plan.z - lift.Level * FloorLayout.StoreyHeight;
            return Mathf.Abs(plan.x - lift.CarX) <= halfX && Mathf.Abs(plan.y - lift.CarY) <= halfY && above > -0.3f && above < size.y;
        }

        /// <summary>How many of a firm's living players are in the exit lift, and how many living players it has. Works on any machine from what it can see.</summary>
        public void CountAboard(int firm, out int aboard, out int living)
        {
            aboard = 0;
            living = 0;
            for (int i = 0; i < FirmMember.All.Count; i++)
            {
                FirmMember member = FirmMember.All[i];
                if (member.Firm != firm || IsDown(member))
                    continue;

                living++;
                if (InExitLift(member.Position))
                    aboard++;
            }
        }

        /// <summary>True when every player of the firm is down on this machine's view of the game (and it has at least one).</summary>
        public bool FirmIsDown(int firm)
        {
            int members = 0;
            for (int i = 0; i < FirmMember.All.Count; i++)
            {
                FirmMember member = FirmMember.All[i];
                if (member.Firm != firm)
                    continue;
                members++;
                if (!IsDown(member))
                    return false;
            }

            return members > 0;
        }

        static bool IsDown(FirmMember member)
        {
            Health health = HealthOf(member);
            return health != null && health.IsDead;
        }

        static readonly System.Collections.Generic.Dictionary<FirmMember, Health> HealthCache = new();

        static Health HealthOf(FirmMember member)
        {
            if (HealthCache.TryGetValue(member, out Health health) && health != null)
                return health;

            // Destroyed members fall out of the cache here, so it never grows past the players seen this session.
            if (HealthCache.Count > 32)
                HealthCache.Clear();
            member.TryGetComponent(out health);
            HealthCache[member] = health;
            return health;
        }

        /// <summary>The name of the room at a world position on the floor in play, or an empty string.</summary>
        public string RoomNameAt(Vector3 world)
        {
            BuiltFloor floor = StandingFloor;
            if (floor == null)
                return string.Empty;

            Vector3 plan = floor.Plan(world);
            FloorRoom room = floor.Layout.RoomAtPoint(plan.x, plan.y);
            return room != null ? room.Name : string.Empty;
        }

        public int CurrentFloor => Floor;

        public bool BriefcaseHidden(BriefcaseStatus status)
        {
            return Floor <= 1 && status.LastCarrierFirm == MatchDirector.NoFirm;
        }

        public string PlaceLabel(BriefcaseStatus status)
        {
            if (BriefcaseHidden(status))
                return "somewhere on floor " + Floor;

            Vector3 at = status.Position;
            if (status.State == BriefcaseState.Carried)
            {
                FirmMember carrier = FirmMember.Find(status.CarrierId);
                if (carrier != null)
                    at = carrier.Position;
            }
            else if (Briefcase.Current != null)
            {
                at = Briefcase.Current.Position;
            }

            string room = RoomNameAt(at);
            return room.Length > 0 ? $"the {room}, floor {Floor}" : "floor " + Floor;
        }

        public string ReasonLine(MatchSnapshot result, int localFirm)
        {
            string how = $"Briefcase delivered to the manager's office after {MatchHud.FormatClock(result.EndSeconds)}.";
            if (localFirm == MatchDirector.NoFirm)
                return how;
            return how + (result.Winner == localFirm ? " That is your firm." : " That is the rival firm.");
        }

        [TargetRpc]
        void TargetCash(NetworkConnection connection, int amount, byte floor)
        {
            int total = ProfileStore.AddCash(amount);
            OnCashEarned?.Invoke(amount, total, floor);
        }
    }
}
