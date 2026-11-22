using System;
using CloseTheDeal.Tower;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>Host-synced state and event hub for the briefcase match mode. Design: docs/systems/MATCH.md.</summary>
    [DisallowMultipleComponent]
    public sealed partial class MatchDirector : NetworkBehaviour
    {
        public const int FirmCount = TowerBuilder.TeamCount;
        public const byte NoFirm = MatchRules.NoFirm;

        public static MatchDirector Instance { get; private set; }

        /// <summary>Every machine. Returns true while another system (items) occupies this player's hands.</summary>
        public static Func<FirmMember, bool> HandsFull;

        /// <summary>The mode that replaces the two-tower rules in this scene, or null for the towers. Set by the mode in Awake.</summary>
        public static IMatchRuleset Ruleset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            HandsFull = null;
            Ruleset = null;
        }

        /// <summary>The match is live on this machine (hosting or connected).</summary>
        public event Action OnStarted;

        /// <summary>The match ended or disconnected on this machine.</summary>
        public event Action OnClosed;

        /// <summary>Previous phase, new phase.</summary>
        public event Action<MatchPhase, MatchPhase> OnPhaseChanged;

        /// <summary>Whole seconds left before the briefcase unlocks.</summary>
        public event Action<int> OnCountdownChanged;

        /// <summary>Whole seconds left on the match clock.</summary>
        public event Action<int> OnClockChanged;

        /// <summary>Anything about the briefcase changed: before, after.</summary>
        public event Action<BriefcaseStatus, BriefcaseStatus> OnBriefcaseChanged;

        /// <summary>Picked up: the carrier's firm.</summary>
        public event Action<int> OnBriefcasePickedUp;

        /// <summary>Let go: the firm that was carrying it.</summary>
        public event Action<int> OnBriefcaseDropped;

        /// <summary>It left the buildings and is on its way back.</summary>
        public event Action OnBriefcaseReturning;

        /// <summary>It reappeared on a skybridge or the plaza.</summary>
        public event Action OnBriefcaseReturned;

        /// <summary>Highest open floor index rose (shared across both towers).</summary>
        public event Action<int> OnOpenFloorChanged;

        /// <summary>Floor opened (floor index, opening firm).</summary>
        public event Action<int, int> OnFloorOpened;

        /// <summary>Briefcase changed hands between firms (taking firm, previous firm).</summary>
        public event Action<int, int> OnBriefcaseStolen;

        /// <summary>Clock crossed the cap-warning mark (whole seconds left).</summary>
        public event Action<int> OnCapWarning;

        /// <summary>Firm checkpoint floor advanced (firm, checkpoint floor).</summary>
        public event Action<int, int> OnCheckpointSaved;

        public event Action<MatchSnapshot> OnMatchEnded;

        /// <summary>Towers were rebuilt: floor names and counts may differ.</summary>
        public event Action OnArenaChanged;

        [Tooltip("Tuning for the match: firms, clock, briefcase handling. Read at runtime, never written.")]
        [SerializeField] MatchProfile _profile;

        [Tooltip("The plaza and skybridges between the towers. Without them the briefcase spawns on Building A's reception desk and nobody can cross.")]
        [SerializeField] MatchCrossings _crossings;

        [Tooltip("The floor gates: nobody climbs above the highest floor the briefcase has been carried onto. Without them every floor is open.")]
        [SerializeField] MatchGates _gates;

        readonly SyncVar<MatchSnapshot> _snapshot = new();
        readonly SyncVar<BriefcaseStatus> _briefcase = new();
        readonly SyncVar<byte> _checkpointA = new();
        readonly SyncVar<byte> _checkpointB = new();
        readonly SyncVar<FloorOpening> _openFloor = new();

        readonly MatchArena _arena = new();
        TowerBuilder _tower;
        bool _live;
        int _countdownLeft = -1;
        int _secondsLeft = -1;
        bool _capWarned;
        int _announceFromFrame = int.MaxValue;
        bool _startPending;

        // Suppress event banners on initial SyncVar arrival when a client joins mid-match.
        bool Announce => IsServerStarted || Time.frameCount >= _announceFromFrame;
        int _solidMask;

        public MatchProfile Profile => _profile;
        public MatchArena Arena => _arena;

        /// <summary>True while the match exists on this machine (hosting or connected).</summary>
        public bool IsLive => _live;

        public MatchPhase Phase => _snapshot.Value.Phase;
        public MatchSnapshot Snapshot => _snapshot.Value;
        public BriefcaseStatus CaseStatus => _briefcase.Value;

        /// <summary>The highest floor anyone may stand on: the highest the briefcase has been carried onto this match. 0 = the ground floor.</summary>
        public int OpenFloor => _openFloor.Value.Floor;

        /// <summary>Which firm's carrier opened the current open floor, or NoFirm.</summary>
        public int OpenedByFirm => _openFloor.Value.ByFirm;

        /// <summary>Whole seconds until a returning briefcase reappears, worked out locally from its synced return tick; 0 when it isn't returning.</summary>
        public int ReturnSecondsLeft
        {
            get
            {
                BriefcaseStatus status = _briefcase.Value;
                if (status.State != BriefcaseState.Returning)
                    return 0;
                uint now = TimeManager.Tick;
                return now >= status.ReturnTick ? 0 : Mathf.CeilToInt((float)((status.ReturnTick - now) * TimeManager.TickDelta));
            }
        }

        /// <summary>The floor gates, or null in a scene without them.</summary>
        internal MatchGates Gates => _gates;

        /// <summary>True when this scene has floor gates; without them every floor is open and nobody is refused.</summary>
        internal bool HasGates => _gates != null;

        /// <summary>Where the stairwell is in each floor's own space; zero without gates.</summary>
        internal Vector3 StairCentre => _gates != null ? _gates.StairCentre : Vector3.zero;

        /// <summary>The firm's last checkpoint floor; 0 = its reception.</summary>
        public int CheckpointFloor(int firm) => firm == 0 ? _checkpointA.Value : _checkpointB.Value;

        /// <summary>Whole seconds left on the match clock; the full match length before it starts.</summary>
        public int SecondsLeft
        {
            get
            {
                MatchSnapshot snapshot = _snapshot.Value;
                if (snapshot.Phase == MatchPhase.Ended)
                    return Mathf.Max(0, Mathf.CeilToInt(_profile.MatchSeconds) - snapshot.EndSeconds);
                return snapshot.Phase == MatchPhase.Running && _secondsLeft >= 0 ? _secondsLeft : Mathf.CeilToInt(_profile.MatchSeconds);
            }
        }

        public FirmDefinition Definition(int firm) => _profile.FirmInTower(firm);

        public string FirmName(int firm)
        {
            FirmDefinition definition = Definition(firm);
            return definition != null ? definition.DisplayName : "Firm " + (firm + 1);
        }

        /// <summary>The player carrying the briefcase on this machine, or null.</summary>
        public FirmMember Carrier()
        {
            BriefcaseStatus status = _briefcase.Value;
            return status.State == BriefcaseState.Carried ? FirmMember.Find(status.CarrierId) : null;
        }

        public bool IsCarrying(FirmMember member)
        {
            BriefcaseStatus status = _briefcase.Value;
            return status.State == BriefcaseState.Carried && status.CarrierId == member.ObjectId;
        }

        /// <summary>How this firm is coloured on this screen: your own firm, the rival, or its own colour when you have no side.</summary>
        public Color ColourFor(int firm)
        {
            if (firm == NoFirm)
                return _profile.LooseColour;

            FirmMember local = FirmMember.Local;
            if (local == null)
            {
                FirmDefinition definition = Definition(firm);
                return definition != null ? definition.MarkerColour : Color.white;
            }

            return local.Firm == firm ? _profile.OwnFirmColour : _profile.RivalFirmColour;
        }

        /// <summary>Where the briefcase is, in words for the HUD: "Floor 3, your tower", "the skybridge on Checkpoint 1", "the plaza".</summary>
        public string PlaceLabel(byte place, int floor)
        {
            if (Ruleset != null)
                return Ruleset.PlaceLabel(_briefcase.Value);
            if (place == MatchArena.Neutral)
                return floor == 0 ? "the plaza" : "the skybridge on " + _arena.FloorLabel(floor);
            if (place >= FirmCount)
                return "outside the buildings";

            FirmMember local = FirmMember.Local;
            string tower = local == null ? FirmName(place) + "'s tower" : local.Firm == place ? "your tower" : "the rival tower";
            return _arena.FloorLabel(floor) + ", " + tower;
        }

        /// <summary>One-line summary of the match outcome for results UI.</summary>
        public string ReasonLine(MatchSnapshot result, int localFirm)
        {
            if (Ruleset != null)
                return Ruleset.ReasonLine(result, localFirm);

            string clock = MatchHud.FormatClock(result.EndSeconds);
            string floor = _arena.FloorLabel(result.EndFloor);
            string how;
            switch (result.Reason)
            {
                case MatchEndReason.BriefcaseOnRoof:
                    how = $"Briefcase delivered to the roof at {clock}.";
                    break;
                case MatchEndReason.ClockHolding:
                    how = $"The quarter closed at {clock} with the briefcase in their hands, on {floor}.";
                    break;
                case MatchEndReason.ClockLastCarried:
                    how = $"The quarter closed at {clock}. The briefcase lay loose on {floor}; they carried it last.";
                    break;
                default:
                    return $"The quarter closed at {clock} and nobody ever picked the briefcase up.";
            }

            if (localFirm == NoFirm)
                return how;
            return how + (result.Winner == localFirm ? " That is your firm." : " That is the rival firm.");
        }

        /// <summary>What the member's interact or drop key would do right now.</summary>
        public MatchPrompt PromptFor(FirmMember member)
        {
            if (!_live || Phase != MatchPhase.Running)
                return MatchPrompt.None;
            if (IsCarrying(member))
                return MatchPrompt.DropBriefcase;
            if (HandsFull != null && HandsFull(member))
                return MatchPrompt.None;
            return CanGrab(member) ? MatchPrompt.PickUpBriefcase : MatchPrompt.None;
        }

        bool CanGrab(FirmMember member)
        {
            Briefcase briefcase = Briefcase.Current;
            if (briefcase == null || _briefcase.Value.State != BriefcaseState.Dropped)
                return false;

            Vector3 centre = briefcase.Centre;
            float range = _profile.PickupRange;
            if ((centre - member.Chest).sqrMagnitude > range * range)
                return false;

            Vector3 eye = member.Position + Vector3.up * member.Motor.EyeHeight;
            return !Physics.Linecast(eye, centre, _solidMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Layers that block reach, drops and sight: everything but players and loose furniture.</summary>
        public int SolidMask => _solidMask;

        void Awake()
        {
            Instance = this;
            // Only buildings block reach/drop; ignore players and loose furniture.
            _solidMask = ~(LayerMask.GetMask("Player") | LayerMask.GetMask("Prop"));
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (_profile == null)
            {
                Debug.LogError("[Match] The Match object has no match profile. Run Seal the Deal > Lanes > Set Up GameMode, or set Profile on it.");
                enabled = false;
                return;
            }

            _tower = TowerBuilder.Instance;
            _live = true;
            _countdownLeft = -1;
            _secondsLeft = -1;

            _snapshot.OnChange += OnSnapshotChanged;
            _briefcase.OnChange += OnBriefcaseStatusChanged;
            _checkpointA.OnChange += OnCheckpointAChanged;
            _checkpointB.OnChange += OnCheckpointBChanged;
            _openFloor.OnChange += OnOpenFloorSynced;
            TimeManager.OnPostTick += OnPostTick;

            if (Ruleset != null)
                return;

            if (_tower == null)
            {
                Debug.LogError("[Match] No tower in the scene: the match needs the two buildings.");
                return;
            }

            if (_crossings == null)
                Debug.LogWarning("[Match] No MatchCrossings on the Match object: no plaza or skybridges, so the briefcase starts in Building A's reception and nobody can cross.");

            _tower.OnBuilt += OnTowerBuilt;
            _tower.OnCleared += OnTowerCleared;
            if (_tower.IsBuilt)
                RefreshArena();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!_live)
                return;

            if (Briefcase.Current != null)
                Briefcase.Current.Register();
            // FishNet delivers initial SyncVar state after start callbacks in this frame; defer OnStarted.
            _announceFromFrame = Time.frameCount + 1;
            _startPending = true;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _live = false;
            _announceFromFrame = int.MaxValue;
            _startPending = false;
            _snapshot.OnChange -= OnSnapshotChanged;
            _briefcase.OnChange -= OnBriefcaseStatusChanged;
            _checkpointA.OnChange -= OnCheckpointAChanged;
            _checkpointB.OnChange -= OnCheckpointBChanged;
            _openFloor.OnChange -= OnOpenFloorSynced;
            TimeManager.OnPostTick -= OnPostTick;
            if (_tower != null)
            {
                _tower.OnBuilt -= OnTowerBuilt;
                _tower.OnCleared -= OnTowerCleared;
            }

            if (_crossings != null)
                _crossings.Clear();
            if (_gates != null)
                _gates.Clear();
            _arena.Clear();
            OnClosed?.Invoke();
        }

        void OnTowerBuilt()
        {
            RefreshArena();
            // Towers may build before OnStartServer runs; OnStartServer prepares the round then.
            if (IsServerStarted && _referee != null)
                ServerPrepareRound();
        }

        void RefreshArena()
        {
            _arena.Refresh(_tower);
            if (_crossings != null)
                _crossings.Build(_arena, _profile);
            if (_gates != null)
                _gates.Build(_arena, _tower);
            ApplyGates();
            OnArenaChanged?.Invoke();
        }

        void OnTowerCleared()
        {
            if (_crossings != null)
                _crossings.Clear();
            if (_gates != null)
                _gates.Clear();
            _arena.Clear();
        }

        void OnSnapshotChanged(MatchSnapshot previous, MatchSnapshot next, bool asServer)
        {
            // Host hears SyncVar OnChange twice (server then client); handle once.
            if (asServer != IsServerStarted)
                return;

            _countdownLeft = -1;
            _secondsLeft = -1;
            if (previous.Phase == next.Phase)
                return;

            if (!Announce)
                return;

            OnPhaseChanged?.Invoke(previous.Phase, next.Phase);
            if (next.Phase == MatchPhase.Ended)
                OnMatchEnded?.Invoke(next);
        }

        void OnBriefcaseStatusChanged(BriefcaseStatus previous, BriefcaseStatus next, bool asServer)
        {
            if (asServer != IsServerStarted)
                return;

            if (Briefcase.Current != null)
                Briefcase.Current.Apply(next);
            if (previous.CarrierId != next.CarrierId || previous.State != next.State)
                ApplyGates();

            OnBriefcaseChanged?.Invoke(previous, next);
            if (Announce && (previous.State != next.State || previous.CarrierId != next.CarrierId))
                RaiseBriefcaseEvent(previous, next);
        }

        void RaiseBriefcaseEvent(BriefcaseStatus previous, BriefcaseStatus next)
        {
            // Round resets (NoFirm) and post-result return (Ended) are cleanup, not drops.
            bool reset = next.LastCarrierFirm == NoFirm;
            if (previous.State == BriefcaseState.Carried && previous.CarrierFirm != NoFirm && !reset && Phase != MatchPhase.Ended)
                OnBriefcaseDropped?.Invoke(previous.CarrierFirm);

            switch (next.State)
            {
                case BriefcaseState.Carried:
                    RaisePickUp(previous.LastCarrierFirm, next.CarrierFirm);
                    break;
                case BriefcaseState.Returning:
                    OnBriefcaseReturning?.Invoke();
                    break;
                default:
                    if (previous.State == BriefcaseState.Returning)
                        OnBriefcaseReturned?.Invoke();
                    break;
            }
        }

        void OnOpenFloorSynced(FloorOpening previous, FloorOpening next, bool asServer)
        {
            if (asServer != IsServerStarted)
                return;

            ApplyGates();
            if (next.Floor <= previous.Floor || !Announce)
                return;

            OnOpenFloorChanged?.Invoke(next.Floor);
            OnFloorOpened?.Invoke(next.Floor, next.ByFirm);
        }

        // Syncs floor gates to OpenFloor and current carrier; also called when a carrier body spawns late.
        internal void ApplyGates()
        {
            if (_gates == null || !_arena.Ready)
                return;

            _gates.SetOpenFloor(_openFloor.Value.Floor);
            FirmMember carrier = Carrier();
            _gates.SetCarrier(carrier != null ? carrier.Body : null);
        }

        void OnCheckpointAChanged(byte previous, byte next, bool asServer) => HandleCheckpoint(0, previous, next, asServer);
        void OnCheckpointBChanged(byte previous, byte next, bool asServer) => HandleCheckpoint(1, previous, next, asServer);

        void HandleCheckpoint(int firm, byte previous, byte next, bool asServer)
        {
            if (asServer == IsServerStarted && next > previous && Announce)
                OnCheckpointSaved?.Invoke(firm, next);
        }

        internal uint ElapsedTicks(uint tick)
        {
            uint began = _snapshot.Value.PhaseTick;
            return tick > began ? tick - began : 0u;
        }

        void RaisePickUp(byte lastCarrierFirm, byte carrierFirm)
        {
            OnBriefcasePickedUp?.Invoke(carrierFirm);
            if (lastCarrierFirm != NoFirm && lastCarrierFirm != carrierFirm)
                OnBriefcaseStolen?.Invoke(carrierFirm, lastCarrierFirm);
        }

        void OnPostTick()
        {
            if (_startPending && Time.frameCount >= _announceFromFrame)
            {
                _startPending = false;
                OnStarted?.Invoke();
            }

            uint tick = TimeManager.Tick;
            if (IsServerStarted)
            {
                if (Ruleset != null)
                    Ruleset.ServerStep(tick);
                else if (_referee != null)
                    _referee.Step(tick);
            }

            MatchPhase phase = Phase;
            if (phase != MatchPhase.Countdown && phase != MatchPhase.Running)
                return;
            if (phase == MatchPhase.Running && Ruleset != null && !Ruleset.HasClock)
                return;

            float elapsed = (float)(ElapsedTicks(tick) * TimeManager.TickDelta);
            if (phase == MatchPhase.Countdown)
            {
                CountDown(ref _countdownLeft, _profile.CountdownSeconds - elapsed, OnCountdownChanged);
                _capWarned = false;
                return;
            }

            CountDown(ref _secondsLeft, _profile.MatchSeconds - elapsed, OnClockChanged);
            CheckCapWarning();
        }

        void CheckCapWarning()
        {
            float mark = _profile.CapWarningSeconds;
            if (_capWarned || mark <= 0f || _secondsLeft < 0 || _secondsLeft > mark)
                return;

            _capWarned = true;
            if (_secondsLeft > mark - 2)
                OnCapWarning?.Invoke(_secondsLeft);
        }

        static void CountDown(ref int shown, float secondsLeft, Action<int> changed)
        {
            int whole = Mathf.Max(0, Mathf.CeilToInt(secondsLeft));
            if (shown >= 0 && whole >= shown)
                return;

            shown = whole;
            changed?.Invoke(whole);
        }
    }
}
