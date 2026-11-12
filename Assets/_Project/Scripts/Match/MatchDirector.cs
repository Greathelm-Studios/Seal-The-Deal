using System;
using CloseTheDeal.Tower;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The briefcase game mode: two firms, one briefcase each, first to carry theirs onto
    /// their roof wins. This scene object holds the whole match as read-only state for the
    /// HUD and menu, synced from the host only when something changes, with an event for
    /// every change. The host's decisions are in MatchDirector.Host.cs and MatchReferee.
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

        /// <summary>Whole seconds left before the briefcases unlock.</summary>
        public event Action<int> OnCountdownChanged;

        /// <summary>Whole seconds left on the match clock.</summary>
        public event Action<int> OnClockChanged;

        /// <summary>A firm's briefcase or checkpoint changed: firm, before, after.</summary>
        public event Action<int, FirmStatus, FirmStatus> OnFirmChanged;

        public event Action<int> OnBriefcasePickedUp;
        public event Action<int> OnBriefcaseDropped;

        /// <summary>A briefcase left its building and is on its way back to the checkpoint.</summary>
        public event Action<int> OnBriefcaseReturning;

        /// <summary>A briefcase reappeared at its firm's checkpoint.</summary>
        public event Action<int> OnBriefcaseReturned;

        /// <summary>Firm, checkpoint floor.</summary>
        public event Action<int, int> OnCheckpointSaved;

        public event Action<MatchSnapshot> OnMatchEnded;

        /// <summary>The towers were rebuilt: floor names and counts may differ.</summary>
        public event Action OnArenaChanged;

        // ---- Inspector -----------------------------------------------------------------------

        [Tooltip("Tuning for the match: firms, clock, briefcase handling. Read at runtime, never written.")]
        [SerializeField] MatchProfile _profile;

        // ---- Synced state --------------------------------------------------------------------

        readonly SyncVar<MatchSnapshot> _snapshot = new();
        readonly SyncVar<FirmStatus> _firmA = new();
        readonly SyncVar<FirmStatus> _firmB = new();

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

        public FirmStatus Firm(int firm) => firm == 0 ? _firmA.Value : _firmB.Value;

        public FirmDefinition Definition(int firm) => _profile.FirmInTower(firm);

        public string FirmName(int firm)
        {
            FirmDefinition definition = Definition(firm);
            return definition != null ? definition.DisplayName : "Firm " + (firm + 1);
        }

        /// <summary>The player carrying this firm's briefcase on this machine, or null.</summary>
        public FirmMember Carrier(int firm)
        {
            FirmStatus status = Firm(firm);
            return status.State == BriefcaseState.Carried ? FirmMember.Find(status.CarrierId) : null;
        }

        public bool IsCarrying(FirmMember member)
        {
            FirmStatus status = Firm(member.Firm);
            return status.State == BriefcaseState.Carried && status.CarrierId == member.ObjectId;
        }

        /// <summary>How this firm is coloured on this screen: your own firm, the rival, or its own colour when you have no side.</summary>
        public Color ColourFor(int firm)
        {
            FirmMember local = FirmMember.Local;
            if (local == null)
            {
                FirmDefinition definition = Definition(firm);
                return definition != null ? definition.MarkerColour : Color.white;
            }

            return local.Firm == firm ? _profile.OwnFirmColour : _profile.RivalFirmColour;
        }

        /// <summary>What the member's interact or drop key would do right now.</summary>
        public MatchPrompt PromptFor(FirmMember member)
        {
            if (!_live || Phase != MatchPhase.Running)
                return MatchPrompt.None;

            FirmStatus own = Firm(member.Firm);
            if (own.State == BriefcaseState.Carried)
                return own.CarrierId == member.ObjectId ? MatchPrompt.DropBriefcase : MatchPrompt.None;

            return CanGrab(member, member.Firm) ? MatchPrompt.PickUpBriefcase : MatchPrompt.None;
        }

        /// <summary>The firm's briefcase lies loose, within the member's reach, with nothing solid between it and their eye.</summary>
        bool CanGrab(FirmMember member, int firm)
        {
            Briefcase briefcase = Briefcase.Of(firm);
            if (briefcase == null || Firm(firm).State != BriefcaseState.Dropped)
                return false;

            Vector3 centre = briefcase.Centre;
            float range = _profile.PickupRange;
            if ((centre - member.Chest).sqrMagnitude > range * range)
                return false;

            return InReach(member, centre);
        }

        /// <summary>Nothing solid between the member's eye and the point: no reaching or kicking through walls.</summary>
        internal bool InReach(FirmMember member, Vector3 point)
        {
            Vector3 eye = member.Position + Vector3.up * member.Motor.EyeHeight;
            return !Physics.Linecast(eye, point, _solidMask, QueryTriggerInteraction.Ignore);
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
            _firmA.OnChange += OnFirmAChanged;
            _firmB.OnChange += OnFirmBChanged;
            TimeManager.OnPostTick += OnPostTick;

            if (_tower == null)
            {
                Debug.LogError("[Match] No tower in the scene: the match needs the two buildings.");
                return;
            }

            _tower.OnBuilt += OnTowerBuilt;
            _tower.OnCleared += OnTowerCleared;
            if (_tower.IsBuilt)
                _arena.Refresh(_tower);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!_live)
                return;

            RefreshBriefcases();
            OnStarted?.Invoke();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _live = false;
            _snapshot.OnChange -= OnSnapshotChanged;
            _firmA.OnChange -= OnFirmAChanged;
            _firmB.OnChange -= OnFirmBChanged;
            TimeManager.OnPostTick -= OnPostTick;
            if (_tower != null)
            {
                _tower.OnBuilt -= OnTowerBuilt;
                _tower.OnCleared -= OnTowerCleared;
            }

            _arena.Clear();
            OnClosed?.Invoke();
        }

        void OnTowerBuilt()
        {
            _arena.Refresh(_tower);
            OnArenaChanged?.Invoke();
            // The tower can finish building before this object has started as the server; OnStartServer prepares the round then.
            if (IsServerStarted && _referee != null)
                ServerPrepareRound();
        }

        void OnTowerCleared()
        {
            _arena.Clear();
        }

        /// <summary>Briefcases that spawned before the match was live here take up its state now.</summary>
        void RefreshBriefcases()
        {
            for (int firm = 0; firm < FirmCount; firm++)
            {
                Briefcase briefcase = Briefcase.Of(firm);
                if (briefcase != null)
                    briefcase.Register();
            }
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

        void OnFirmAChanged(FirmStatus previous, FirmStatus next, bool asServer) => HandleFirmChanged(0, previous, next, asServer);
        void OnFirmBChanged(FirmStatus previous, FirmStatus next, bool asServer) => HandleFirmChanged(1, previous, next, asServer);

        void HandleFirmChanged(int firm, FirmStatus previous, FirmStatus next, bool asServer)
        {
            if (asServer != IsServerStarted)
                return;

            Briefcase briefcase = Briefcase.Of(firm);
            if (briefcase != null)
                briefcase.Apply(next);

            OnFirmChanged?.Invoke(firm, previous, next);
            if (previous.State != next.State)
                RaiseBriefcaseEvent(firm, previous.State, next.State);
            if (next.CheckpointFloor > previous.CheckpointFloor)
                OnCheckpointSaved?.Invoke(firm, next.CheckpointFloor);
        }

        void RaiseBriefcaseEvent(int firm, BriefcaseState previous, BriefcaseState next)
        {
            switch (next)
            {
                case BriefcaseState.Carried:
                    OnBriefcasePickedUp?.Invoke(firm);
                    break;
                case BriefcaseState.Returning:
                    OnBriefcaseReturning?.Invoke(firm);
                    break;
                default:
                    if (previous == BriefcaseState.Returning)
                        OnBriefcaseReturned?.Invoke(firm);
                    else
                        OnBriefcaseDropped?.Invoke(firm);
                    break;
            }
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
