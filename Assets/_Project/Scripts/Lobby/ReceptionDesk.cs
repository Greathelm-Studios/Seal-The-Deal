using UnityEngine;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// The reception desk. Press interact to ready up or take it back. When everyone is ready
    /// and the start rule is met, the host instead holds interact to start the run; a tap still
    /// takes the host's ready back. The desk asks the host; the roster decides.
    /// </summary>
    public sealed class ReceptionDesk : LobbyInteractable
    {
        [Tooltip("The desk bell or button that dips when used. Optional; a child made for this, never a placed part of the desk.")]
        [SerializeField] Transform _bell;

        [Tooltip("How far the bell dips when pressed, in metres.")]
        [Range(0f, 0.05f)] [SerializeField] float _bellDip = 0.012f;

        enum Mode { None, Ready, Unready, HostStart, RunStarted }

        // Prompts are fixed strings so nothing is built per frame (AGENTS.md §6).
        const string KeyReady = "F   Ready up";
        const string KeyUnready = "F   Take back your ready";
        const string KeyStart = "Hold F   Start the run";
        const string PadReady = "Y   Ready up";
        const string PadUnready = "Y   Take back your ready";
        const string PadStart = "Hold Y   Start the run";
        const string Started = "The run has started";
        const float BellSeconds = 0.12f;
        const float TapSeconds = 0.25f;

        bool _startSent;
        bool _holdingStart;
        float _held;
        float _bellTime = -1f;
        Vector3 _bellRest;

        void Awake()
        {
            if (_bell != null)
                _bellRest = _bell.localPosition;
        }

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
            if (roster.IsLocalHost && roster.WhyNotStart() == StartBlock.None)
                return Mode.HostStart;
            return Mode.Unready;
        }

        public override string Prompt(bool gamepad)
        {
            switch (Current())
            {
                case Mode.Ready: return gamepad ? PadReady : KeyReady;
                case Mode.Unready: return gamepad ? PadUnready : KeyUnready;
                case Mode.HostStart: return gamepad ? PadStart : KeyStart;
                case Mode.RunStarted: return Started;
                default: return null;
            }
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
            if (mode == Mode.Ready || mode == Mode.Unready)
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
            roster.RequestStart();
            Ring();
        }

        public override void OnRelease(float heldSeconds)
        {
            // A quick tap in start mode means "not ready after all". A longer hold let go early is a
            // start the host thought better of, and changes nothing.
            if (_holdingStart && !_startSent && heldSeconds < TapSeconds && LobbyRoster.Instance != null)
            {
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
