using TMPro;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The in-game match readout: clock, each firm's briefcase and checkpoint, the countdown,
    /// the key prompt, and one-off lines such as why the roof did nothing. Text changes only
    /// when the match raises an event; Update only fades and pops what is already showing.
    /// Greybox look, built by the lane setup tool. Design: docs/systems/MATCH.md.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] MatchDirector _match;
        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _clockText;
        [SerializeField] TMP_Text _ownText;
        [SerializeField] UnityEngine.UI.Image _ownSwatch;
        [SerializeField] TMP_Text _rivalText;
        [SerializeField] UnityEngine.UI.Image _rivalSwatch;
        [SerializeField] GameObject _countdownChip;
        [SerializeField] TMP_Text _countdownText;
        [SerializeField] CanvasGroup _noticeGroup;
        [SerializeField] TMP_Text _noticeText;
        [SerializeField] GameObject _promptChip;
        [SerializeField] TMP_Text _promptText;

        [Header("Motion")]
        [Tooltip("Seconds a message takes to fade in. Keep it under 0.3 so it feels like an answer.")]
        [SerializeField] float _noticeFadeIn = 0.12f;

        [Tooltip("Seconds a message takes to fade out once its time is up.")]
        [SerializeField] float _noticeFadeOut = 0.25f;

        [Tooltip("How much bigger each countdown number starts before it settles, as a fraction. 0.4 = 40% bigger.")]
        [SerializeField] float _countdownPop = 0.4f;

        [Tooltip("Seconds a countdown number takes to settle to its normal size.")]
        [SerializeField] float _countdownSettle = 0.25f;

        float _noticeUntil = -1f;
        float _countdownPopped = -1f;

        void OnEnable()
        {
            _match.OnStarted += RefreshAll;
            _match.OnClosed += Hide;
            _match.OnPhaseChanged += OnPhaseChanged;
            _match.OnCountdownChanged += OnCountdownChanged;
            _match.OnClockChanged += ShowClock;
            _match.OnFirmChanged += OnFirmChanged;
            _match.OnCheckpointSaved += OnCheckpointSaved;
            _match.OnBriefcaseReturning += OnBriefcaseReturning;
            _match.OnArenaChanged += RefreshRows;
            FirmMember.OnLocalChanged += RefreshRows;
            FirmMember.OnNotice += OnNotice;
            FirmMember.OnPromptChanged += ShowPrompt;

            if (_match.IsLive)
                RefreshAll();
            else
                Hide();
        }

        void OnDisable()
        {
            _match.OnStarted -= RefreshAll;
            _match.OnClosed -= Hide;
            _match.OnPhaseChanged -= OnPhaseChanged;
            _match.OnCountdownChanged -= OnCountdownChanged;
            _match.OnClockChanged -= ShowClock;
            _match.OnFirmChanged -= OnFirmChanged;
            _match.OnCheckpointSaved -= OnCheckpointSaved;
            _match.OnBriefcaseReturning -= OnBriefcaseReturning;
            _match.OnArenaChanged -= RefreshRows;
            FirmMember.OnLocalChanged -= RefreshRows;
            FirmMember.OnNotice -= OnNotice;
            FirmMember.OnPromptChanged -= ShowPrompt;
        }

        /// <summary>Motion only, and only while something is showing.</summary>
        void Update()
        {
            if (_noticeUntil >= 0f)
                FadeNotice();
            if (_countdownPopped >= 0f)
                SettleCountdown();
        }

        // ---- Whole readout -------------------------------------------------------------------

        void Hide()
        {
            _root.SetActive(false);
            _noticeUntil = -1f;
            _countdownPopped = -1f;
        }

        void RefreshAll()
        {
            _root.SetActive(true);
            _noticeGroup.alpha = 0f;
            _countdownChip.SetActive(false);
            ShowPrompt(MatchPrompt.None);
            ShowClock(_match.SecondsLeft);
            RefreshRows();
        }

        void OnPhaseChanged(MatchPhase previous, MatchPhase next)
        {
            ShowClock(_match.SecondsLeft);
            RefreshRows();
            if (next != MatchPhase.Countdown)
            {
                _countdownChip.SetActive(false);
                _countdownPopped = -1f;
            }

            if (next == MatchPhase.Running)
                Say("The quarter is open. Get your firm's briefcase to the roof.");
        }

        // ---- Clock and countdown -------------------------------------------------------------

        void ShowClock(int secondsLeft)
        {
            _clockText.text = FormatClock(secondsLeft);
        }

        public static string FormatClock(int seconds)
        {
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        void OnCountdownChanged(int secondsLeft)
        {
            _countdownChip.SetActive(secondsLeft > 0);
            _countdownText.text = secondsLeft.ToString();
            _countdownPopped = Time.unscaledTime;
        }

        void SettleCountdown()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _countdownPopped) / Mathf.Max(0.01f, _countdownSettle));
            float eased = 1f - (1f - t) * (1f - t);
            _countdownText.rectTransform.localScale = Vector3.one * (1f + _countdownPop * (1f - eased));
            if (t >= 1f)
                _countdownPopped = -1f;
        }

        // ---- Firm rows -----------------------------------------------------------------------

        void OnFirmChanged(int firm, FirmStatus previous, FirmStatus next)
        {
            RefreshRows();
        }

        void RefreshRows()
        {
            if (!_match.IsLive)
                return;

            FirmMember local = FirmMember.Local;
            int own = local != null ? local.Firm : 0;
            int rival = 1 - own;
            _ownText.text = Row(own, local != null ? "YOUR FIRM" : "FIRM", local);
            _rivalText.text = Row(rival, local != null ? "RIVAL FIRM" : "FIRM", local);
            _ownSwatch.color = _match.ColourFor(own);
            _rivalSwatch.color = _match.ColourFor(rival);
        }

        string Row(int firm, string tag, FirmMember local)
        {
            FirmStatus status = _match.Firm(firm);
            MatchArena arena = _match.Arena;
            string where = arena.FloorLabel(status.BriefcaseFloor);
            string saved = status.CheckpointFloor > 0 ? arena.FloorLabel(status.CheckpointFloor) : "Reception";
            return $"<size=70%>{tag}</size>  <b>{_match.FirmName(firm)}</b>\nBriefcase: {BriefcaseLine(firm, status, where, local)}\n<size=85%>Comes back to: {saved}</size>";
        }

        static string BriefcaseLine(int firm, FirmStatus status, string where, FirmMember local)
        {
            switch (status.State)
            {
                case BriefcaseState.Carried:
                    if (local != null && status.CarrierId == local.ObjectId)
                        return $"with you, {where}";
                    return local != null && local.Firm == firm ? $"with a colleague, {where}" : $"being carried, {where}";
                case BriefcaseState.Returning:
                    return "left the building, coming back";
                default:
                    return $"lying loose, {where}";
            }
        }

        // ---- Prompt and one-off lines --------------------------------------------------------

        void ShowPrompt(MatchPrompt prompt)
        {
            _promptChip.SetActive(prompt != MatchPrompt.None);
            if (prompt == MatchPrompt.PickUpBriefcase)
                _promptText.text = $"<b>{FirmMember.InteractKeyLabel}</b>  Pick up the briefcase";
            else if (prompt == MatchPrompt.DropBriefcase)
                _promptText.text = $"<b>{FirmMember.DropKeyLabel}</b>  Drop the briefcase";
        }

        void OnNotice(MatchNotice notice)
        {
            FirmMember local = FirmMember.Local;
            if (local == null || !_match.IsLive)
                return;

            FirmStatus own = _match.Firm(local.Firm);
            MatchArena arena = _match.Arena;
            switch (notice)
            {
                case MatchNotice.RoofNeedsBriefcase:
                    Say($"The roof only counts with your firm's briefcase. It is on {arena.FloorLabel(own.BriefcaseFloor)}.");
                    break;
                case MatchNotice.RivalBriefcase:
                    Say("That briefcase belongs to the other firm. Walk into it to kick it away.");
                    break;
                case MatchNotice.FellOut:
                    Say($"You left the building. Back to {arena.FloorLabel(own.CheckpointFloor)}.");
                    break;
                case MatchNotice.BriefcaseKnockedLoose:
                    Say("You dropped the briefcase.");
                    break;
            }
        }

        void OnCheckpointSaved(int firm, int floor)
        {
            FirmMember local = FirmMember.Local;
            string label = _match.Arena.FloorLabel(floor);
            if (local != null && local.Firm == firm)
                Say($"{label} saved. Your firm and its briefcase come back here.");
            else
                Say($"The rival firm reached {label}.");
        }

        void OnBriefcaseReturning(int firm)
        {
            FirmMember local = FirmMember.Local;
            if (local == null || local.Firm != firm)
                return;

            string label = _match.Arena.FloorLabel(_match.Firm(firm).CheckpointFloor);
            Say($"Your briefcase left the building. It comes back to {label}.");
        }

        void Say(string line)
        {
            _noticeText.text = line;
            _noticeUntil = Time.unscaledTime + _match.Profile.NoticeSeconds;
        }

        void FadeNotice()
        {
            bool showing = Time.unscaledTime < _noticeUntil;
            float rate = showing ? _noticeFadeIn : _noticeFadeOut;
            float target = showing ? 1f : 0f;
            _noticeGroup.alpha = Mathf.MoveTowards(_noticeGroup.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, rate));
            if (!showing && _noticeGroup.alpha <= 0f)
                _noticeUntil = -1f;
        }
    }
}
