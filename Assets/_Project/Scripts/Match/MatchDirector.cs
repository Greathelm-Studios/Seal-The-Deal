using System;
using CloseTheDeal.Tower;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>Host-synced state and event hub for the briefcase: phases, who carries it, pick-up and drop. The mode's rules come from its IMatchRuleset. Design: docs/systems/MATCH.md.</summary>
    [DisallowMultipleComponent]
    public sealed partial class MatchDirector : NetworkBehaviour
    {
        public const int FirmCount = TeamSpawner.TeamCount;
        public const byte NoFirm = byte.MaxValue;

        public static MatchDirector Instance { get; private set; }

        /// <summary>Every machine. Returns true while another system (items) occupies this player's hands.</summary>
        public static Func<FirmMember, bool> HandsFull;

        /// <summary>The mode whose rules this scene plays. Set by the mode in Awake.</summary>
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

        /// <summary>Anything about the briefcase changed: before, after.</summary>
        public event Action<BriefcaseStatus, BriefcaseStatus> OnBriefcaseChanged;

        /// <summary>Picked up: the carrier's firm.</summary>
        public event Action<int> OnBriefcasePickedUp;

        /// <summary>Let go: the firm that was carrying it.</summary>
        public event Action<int> OnBriefcaseDropped;

        /// <summary>Briefcase changed hands between firms (taking firm, previous firm).</summary>
        public event Action<int, int> OnBriefcaseStolen;

        public event Action<MatchSnapshot> OnMatchEnded;

        [Tooltip("Tuning for the match: firms, countdown, briefcase handling. Read at runtime, never written.")]
        [SerializeField] MatchProfile _profile;

        readonly SyncVar<MatchSnapshot> _snapshot = new();
        readonly SyncVar<BriefcaseStatus> _briefcase = new();

        bool _live;
        int _countdownLeft = -1;
        int _announceFromFrame = int.MaxValue;
        bool _startPending;

        // Suppress event banners on initial SyncVar arrival when a client joins mid-match.
        bool Announce => IsServerStarted || Time.frameCount >= _announceFromFrame;
        int _solidMask;

        public MatchProfile Profile => _profile;

        /// <summary>True while the match exists on this machine (hosting or connected).</summary>
        public bool IsLive => _live;

        public MatchPhase Phase => _snapshot.Value.Phase;
        public MatchSnapshot Snapshot => _snapshot.Value;
        public BriefcaseStatus CaseStatus => _briefcase.Value;

        public FirmDefinition Definition(int firm) => _profile.FirmByIndex(firm);

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

        /// <summary>Where the briefcase is, in words for the HUD.</summary>
        public string PlaceLabel() => Ruleset != null ? Ruleset.PlaceLabel(_briefcase.Value) : string.Empty;

        /// <summary>One-line summary of the match outcome for results UI.</summary>
        public string ReasonLine(MatchSnapshot result, int localFirm) => Ruleset != null ? Ruleset.ReasonLine(result, localFirm) : string.Empty;

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
            // Only the building blocks reach and drops; ignore players and loose furniture.
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
                Debug.LogError("[Match] The Match object has no match profile. Run Seal the Deal > Floor Race > Build Scene, or set Profile on it.");
                enabled = false;
                return;
            }
            if (Ruleset == null)
            {
                Debug.LogError("[Match] No game mode in the scene: the Match needs one (the Floor Race object) to supply its rules.");
                enabled = false;
                return;
            }

            _live = true;
            _countdownLeft = -1;
            _snapshot.OnChange += OnSnapshotChanged;
            _briefcase.OnChange += OnBriefcaseStatusChanged;
            TimeManager.OnPostTick += OnPostTick;
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
            if (!_live)
                return;

            _live = false;
            _announceFromFrame = int.MaxValue;
            _startPending = false;
            _snapshot.OnChange -= OnSnapshotChanged;
            _briefcase.OnChange -= OnBriefcaseStatusChanged;
            TimeManager.OnPostTick -= OnPostTick;
            OnClosed?.Invoke();
        }

        void OnSnapshotChanged(MatchSnapshot previous, MatchSnapshot next, bool asServer)
        {
            // Host hears SyncVar OnChange twice (server then client); handle once.
            if (asServer != IsServerStarted)
                return;

            _countdownLeft = -1;
            if (previous.Phase == next.Phase || !Announce)
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
            if (Announce && (previous.State != next.State || previous.CarrierId != next.CarrierId))
                RaiseBriefcaseEvent(previous, next);
        }

        void RaiseBriefcaseEvent(BriefcaseStatus previous, BriefcaseStatus next)
        {
            // A fresh briefcase (NoFirm) and the end of a match are resets, not drops.
            bool reset = next.LastCarrierFirm == NoFirm;
            if (previous.State == BriefcaseState.Carried && previous.CarrierFirm != NoFirm && !reset && Phase != MatchPhase.Ended)
                OnBriefcaseDropped?.Invoke(previous.CarrierFirm);

            if (next.State != BriefcaseState.Carried)
                return;

            OnBriefcasePickedUp?.Invoke(next.CarrierFirm);
            if (previous.LastCarrierFirm != NoFirm && previous.LastCarrierFirm != next.CarrierFirm)
                OnBriefcaseStolen?.Invoke(next.CarrierFirm, previous.LastCarrierFirm);
        }

        internal uint ElapsedTicks(uint tick)
        {
            uint began = _snapshot.Value.PhaseTick;
            return tick > began ? tick - began : 0u;
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
                Ruleset.ServerStep(tick);

            if (Phase != MatchPhase.Countdown)
                return;

            float left = _profile.CountdownSeconds - (float)(ElapsedTicks(tick) * TimeManager.TickDelta);
            int whole = Mathf.Max(0, Mathf.CeilToInt(left));
            if (_countdownLeft >= 0 && whole >= _countdownLeft)
                return;

            _countdownLeft = whole;
            OnCountdownChanged?.Invoke(whole);
        }
    }
}
