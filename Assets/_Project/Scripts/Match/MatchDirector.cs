using System;
using CloseTheDeal.Tower;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The briefcase game mode: two firms, one shared briefcase, and the firm whose player
    /// carries it onto a roof wins. This scene object holds the whole match as read-only state
    /// for the HUD and menu, synced from the host only when something changes, with an event
    /// for every change. The host's decisions are in MatchDirector.Host.cs and MatchReferee.
    /// A firm's index is the index of the building it climbs. Design: docs/systems/MATCH.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class MatchDirector : NetworkBehaviour
    {
        public const int FirmCount = TowerBuilder.TeamCount;
        public const byte NoFirm = MatchRules.NoFirm;

        public static MatchDirector Instance { get; private set; }

        // ---- Events (raised on every machine, only on change) --------------------------------

        /// <summary>The match is on this machine: hosting or connected. Read the state now.</summary>
        public event Action OnStarted;

        /// <summary>The match has gone from this machine: left, or the host closed the game.</summary>
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

        /// <summary>Firm, checkpoint floor: where that firm's players now come back after a fall.</summary>
        public event Action<int, int> OnCheckpointSaved;

        public event Action<MatchSnapshot> OnMatchEnded;

        /// <summary>The towers were rebuilt: floor names and counts may differ.</summary>
        public event Action OnArenaChanged;

        // ---- Inspector -----------------------------------------------------------------------

        [Tooltip("Tuning for the match: firms, clock, briefcase handling. Read at runtime, never written.")]
        [SerializeField] MatchProfile _profile;

        [Tooltip("The plaza and skybridges between the towers. Without them the briefcase spawns on Building A's reception desk and nobody can cross.")]
        [SerializeField] MatchCrossings _crossings;

        // ---- Synced state --------------------------------------------------------------------

        readonly SyncVar<MatchSnapshot> _snapshot = new();
        readonly SyncVar<BriefcaseStatus> _briefcase = new();
        readonly SyncVar<byte> _checkpointA = new();
        readonly SyncVar<byte> _checkpointB = new();

        readonly MatchArena _arena = new();
        TowerBuilder _tower;
        bool _live;
        int _countdownLeft = -1;
        int _secondsLeft = -1;
        int _solidMask;

        // ---- Read-only state for HUD and menu ------------------------------------------------

        public MatchProfile Profile => _profile;
        public MatchArena Arena => _arena;

        /// <summary>True while the match exists on this machine (hosting or connected).</summary>
        public bool IsLive => _live;

        public MatchPhase Phase => _snapshot.Value.Phase;
        public MatchSnapshot Snapshot => _snapshot.Value;
        public BriefcaseStatus CaseStatus => _briefcase.Value;

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
            if (place == MatchArena.Neutral)
                return floor == 0 ? "the plaza" : "the skybridge on " + _arena.FloorLabel(floor);
            if (place >= FirmCount)
                return "outside the buildings";

            FirmMember local = FirmMember.Local;
            string tower = local == null ? FirmName(place) + "'s tower" : local.Firm == place ? "your tower" : "the rival tower";
            return _arena.FloorLabel(floor) + ", " + tower;
        }

        /// <summary>What the member's interact or drop key would do right now.</summary>
        public MatchPrompt PromptFor(FirmMember member)
        {
            if (!_live || Phase != MatchPhase.Running)
                return MatchPrompt.None;
            if (IsCarrying(member))
                return MatchPrompt.DropBriefcase;
            return CanGrab(member) ? MatchPrompt.PickUpBriefcase : MatchPrompt.None;
        }

        /// <summary>The briefcase lies loose, within the member's reach, with nothing solid between it and their eye.</summary>
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

        // ---- Lifecycle -----------------------------------------------------------------------

        void Awake()
        {
            Instance = this;
            // The building only: players and loose furniture never block a reach or a drop.
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
                Debug.LogError("[Match] The Match object has no match profile. Run Close the Deal > Lanes > Set Up GameMode, or set Profile on it.");
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
            TimeManager.OnPostTick += OnPostTick;

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
            OnStarted?.Invoke();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _live = false;
            _snapshot.OnChange -= OnSnapshotChanged;
            _briefcase.OnChange -= OnBriefcaseStatusChanged;
            _checkpointA.OnChange -= OnCheckpointAChanged;
            _checkpointB.OnChange -= OnCheckpointBChanged;
            TimeManager.OnPostTick -= OnPostTick;
            if (_tower != null)
            {
                _tower.OnBuilt -= OnTowerBuilt;
                _tower.OnCleared -= OnTowerCleared;
            }

            if (_crossings != null)
                _crossings.Clear();
            _arena.Clear();
            OnClosed?.Invoke();
        }

        void OnTowerBuilt()
        {
            RefreshArena();
            // The tower can finish building before this object has started as the server; OnStartServer prepares the round then.
            if (IsServerStarted && _referee != null)
                ServerPrepareRound();
        }

        /// <summary>Floors, then the plaza and bridges on top of them, then everyone told.</summary>
        void RefreshArena()
        {
            _arena.Refresh(_tower);
            if (_crossings != null)
                _crossings.Build(_arena, _profile);
            OnArenaChanged?.Invoke();
        }

        void OnTowerCleared()
        {
            if (_crossings != null)
                _crossings.Clear();
            _arena.Clear();
        }

        // ---- Changes arriving ----------------------------------------------------------------

        void OnSnapshotChanged(MatchSnapshot previous, MatchSnapshot next, bool asServer)
        {
            // The host hears each change twice, as server and as its own client; act on the first.
            if (asServer != IsServerStarted)
                return;

            _countdownLeft = -1;
            _secondsLeft = -1;
            if (previous.Phase == next.Phase)
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

            OnBriefcaseChanged?.Invoke(previous, next);
            if (previous.State != next.State || previous.CarrierId != next.CarrierId)
                RaiseBriefcaseEvent(previous, next);
        }

        void RaiseBriefcaseEvent(BriefcaseStatus previous, BriefcaseStatus next)
        {
            if (previous.State == BriefcaseState.Carried && previous.CarrierFirm != NoFirm)
                OnBriefcaseDropped?.Invoke(previous.CarrierFirm);

            switch (next.State)
            {
                case BriefcaseState.Carried:
                    OnBriefcasePickedUp?.Invoke(next.CarrierFirm);
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

        void OnCheckpointAChanged(byte previous, byte next, bool asServer) => HandleCheckpoint(0, previous, next, asServer);
        void OnCheckpointBChanged(byte previous, byte next, bool asServer) => HandleCheckpoint(1, previous, next, asServer);

        void HandleCheckpoint(int firm, byte previous, byte next, bool asServer)
        {
            if (asServer == IsServerStarted && next > previous)
                OnCheckpointSaved?.Invoke(firm, next);
        }

        // ---- Clock ---------------------------------------------------------------------------

        /// <summary>Ticks since the current phase began, as this machine sees the host's clock.</summary>
        internal uint ElapsedTicks(uint tick)
        {
            uint began = _snapshot.Value.PhaseTick;
            return tick > began ? tick - began : 0u;
        }

        /// <summary>
        /// Once a tick: the host applies the rules, then every machine works the clock out
        /// from the synced start tick and raises an event when a whole second passes. The
        /// clock itself is never sent.
        /// </summary>
        void OnPostTick()
        {
            uint tick = TimeManager.Tick;
            if (IsServerStarted && _referee != null)
                _referee.Step(tick);

            MatchPhase phase = Phase;
            if (phase != MatchPhase.Countdown && phase != MatchPhase.Running)
                return;

            float elapsed = (float)(ElapsedTicks(tick) * TimeManager.TickDelta);
            if (phase == MatchPhase.Countdown)
                CountDown(ref _countdownLeft, _profile.CountdownSeconds - elapsed, OnCountdownChanged);
            else
                CountDown(ref _secondsLeft, _profile.MatchSeconds - elapsed, OnClockChanged);
        }

        /// <summary>Publishes the whole seconds left when they drop. Only ever counts down within a phase, so a client's clock adjusting never flickers a second back.</summary>
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
