using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// The reception desk. Press interact to ready up or take it back. When everyone is ready the
    /// host instead holds interact to start the run; a quick tap still takes the host's ready
    /// back. If a firm is empty or the firms are uneven, the first hold arms a confirm (the
    /// prompt says why) and a second hold within a few seconds starts the run anyway, when the
    /// lobby rules allow a short-handed start. The desk asks the host; the roster decides.
    /// </summary>
    public sealed class ReceptionDesk : LobbyInteractable
    {
        [Tooltip("The desk bell or button that dips when used. Optional; a child made for this, never a placed part of the desk.")]
        [SerializeField] Transform _bell;

        [Tooltip("How far the bell dips when pressed, in metres.")]
        [Range(0f, 0.05f)] [SerializeField] float _bellDip = 0.012f;

        /// <summary>Raised when the host's first hold arms a short-handed confirm (the reason). For a sound and the board.</summary>
        public static event System.Action<StartBlock> OnConfirmArmed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            OnConfirmArmed = null;
        }

        enum Mode { None, Ready, Unready, HostStart, WaitForHost, RunStarted }

        // Prompts are fixed strings so nothing is built per frame (AGENTS.md §6).
        const string KeyReady = "F   Ready up";
        const string KeyUnready = "F   Take back your ready";
        const string KeyStart = "Hold F   Start the run";
        const string PadReady = "Y   Ready up";
        const string PadUnready = "Y   Take back your ready";
        const string PadStart = "Hold Y   Start the run";
        const string KeyWaiting = "F   Take back your ready   (waiting for the host)";
        const string PadWaiting = "Y   Take back your ready   (waiting for the host)";
        const string Started = "The run has started";
        const float BellSeconds = 0.12f;
        const float TapSeconds = 0.25f;

        bool _startSent;
        bool _holdingStart;
        float _held;
        float _bellTime = -1f;
        Vector3 _bellRest;
        float _confirmUntil = -1f;
        string _confirmKeyPrompt;
        string _confirmPadPrompt;

        void Awake()
        {
            if (_bell != null)
                _bellRest = _bell.localPosition;
        }

        bool ConfirmArmed => _confirmUntil > Time.unscaledTime;

        Mode Current()
        {
            LobbyRoster roster = LobbyRoster.Instance;
            if (roster == null || !roster.TryGetLocal(out LobbyMember me))
                return Mode.None;
            if (roster.RunInProgress)
                return Mode.RunStarted;
            if (me.State == MemberState.Fitting)
                return Mode.None;
            if (me.State != MemberState.Ready)
                return Mode.Ready;
            if (!roster.IsLocalHost)
                return roster.Evaluate(out _) == StartState.Unavailable ? Mode.Unready : Mode.WaitForHost;
            return roster.Evaluate(out _) != StartState.Unavailable ? Mode.HostStart : Mode.Unready;
        }

        // Indexed by Mode, so the prompt is a lookup, not a branch per state.
        static readonly string[] KeyPrompts = { null, KeyReady, KeyUnready, KeyStart, KeyWaiting, Started };
        static readonly string[] PadPrompts = { null, PadReady, PadUnready, PadStart, PadWaiting, Started };

        public override string Prompt(bool gamepad)
        {
            Mode mode = Current();
            if (mode == Mode.HostStart && ConfirmArmed)
                return gamepad ? _confirmPadPrompt : _confirmKeyPrompt;
            return (gamepad ? PadPrompts : KeyPrompts)[(int)mode];
        }

        public override float HoldProgress
        {
            get
            {
                LobbyRoster roster = LobbyRoster.Instance;
                if (!_holdingStart || roster == null)
                    return -1f;
                return Mathf.Clamp01(_held / roster.Rules.StartHoldSeconds);
            }
        }

        public override void OnPress()
        {
            _held = 0f;
            _startSent = false;
            Mode mode = Current();
            _holdingStart = mode == Mode.HostStart;
            if (mode == Mode.Ready || mode == Mode.Unready || mode == Mode.WaitForHost)
            {
                LobbyRoster.Instance.RequestReady(mode == Mode.Ready);
                Ring();
            }
        }

        public override void OnHold(float heldSeconds)
        {
            _held = heldSeconds;
            if (!_holdingStart || _startSent)
                return;

            LobbyRoster roster = LobbyRoster.Instance;
            if (roster == null || heldSeconds < roster.Rules.StartHoldSeconds)
                return;

            _startSent = true;
            _holdingStart = false;
            StartOrArm(roster);
            Ring();
        }

        /// <summary>A full hold: start now, or (short-handed) arm the confirm, or on the second hold start anyway.</summary>
        void StartOrArm(LobbyRoster roster)
        {
            StartState state = roster.Evaluate(out StartBlock reason);
            if (state == StartState.Ready)
            {
                roster.RequestStart(false);
                return;
            }

            if (state != StartState.NeedsConfirm)
                return;

            if (ConfirmArmed)
            {
                _confirmUntil = -1f;
                roster.RequestStart(true);
                return;
            }

            ArmConfirm(roster, reason);
        }

        void ArmConfirm(LobbyRoster roster, StartBlock reason)
        {
            // Built once per arm, not per frame.
            string why = reason == StartBlock.NeedBothFirms
                ? roster.Rules.FirmName(roster.SmallerFirm()) + " has nobody."
                : "The firms are uneven.";
            _confirmKeyPrompt = why + "   Hold F again to start anyway";
            _confirmPadPrompt = why + "   Hold Y again to start anyway";
            _confirmUntil = Time.unscaledTime + roster.Rules.ConfirmSeconds;
            OnConfirmArmed?.Invoke(reason);
        }

        public override void OnRelease(float heldSeconds)
        {
            // A quick tap in start mode means "not ready after all". A longer hold let go early is a
            // start the host thought better of, and changes nothing.
            if (_holdingStart && !_startSent && heldSeconds < TapSeconds && LobbyRoster.Instance != null)
            {
                _confirmUntil = -1f;
                LobbyRoster.Instance.RequestReady(false);
                Ring();
            }

            _holdingStart = false;
            _held = 0f;
        }

        public override void OnLost()
        {
            _holdingStart = false;
            _held = 0f;
        }

        void Ring()
        {
            if (_bell != null)
                _bellTime = 0f;
        }

        void Update()
        {
            if (_bellTime < 0f)
                return;

            _bellTime += Time.deltaTime;
            float t = Mathf.Clamp01(_bellTime / BellSeconds);
            _bell.localPosition = _bellRest + Vector3.down * (_bellDip * Mathf.Sin(t * Mathf.PI));
            if (t >= 1f)
            {
                _bellTime = -1f;
                _bell.localPosition = _bellRest;
            }
        }
    }
}
