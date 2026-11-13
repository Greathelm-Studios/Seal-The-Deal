using TMPro;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The in-game match readout: clock, where the briefcase is and who holds it, each firm's
    /// checkpoint, the countdown, the key prompt, and one-off lines such as why the roof did
    /// nothing. Text changes only when the match raises an event; Update only fades and pops
    /// what is already showing. Greybox look, built by the lane setup tool.
    /// Design: docs/systems/MATCH.md.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] MatchDirector _match;
        [SerializeField] GameObject _root;
        [SerializeField] TMP_Text _clockText;
        [SerializeField] TMP_Text _briefcaseText;
        [SerializeField] UnityEngine.UI.Image _briefcaseSwatch;
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
            _match.OnBriefcaseChanged += OnBriefcaseChanged;
            _match.OnCheckpointSaved += OnCheckpointSaved;
            _match.OnBriefcasePickedUp += OnPickedUp;
            _match.OnBriefcaseReturning += OnReturning;
            _match.OnOpenFloorChanged += OnOpenFloorChanged;
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
            _match.OnBriefcaseChanged -= OnBriefcaseChanged;
            _match.OnCheckpointSaved -= OnCheckpointSaved;
            _match.OnBriefcasePickedUp -= OnPickedUp;
            _match.OnBriefcaseReturning -= OnReturning;
            _match.OnOpenFloorChanged -= OnOpenFloorChanged;
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
                Say("The quarter is open. Get the briefcase onto a roof.");
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

        // ---- Rows ----------------------------------------------------------------------------

        void OnBriefcaseChanged(BriefcaseStatus previous, BriefcaseStatus next)
        {
            RefreshBriefcaseRow();
        }

        void RefreshRows()
        {
            if (!_match.IsLive)
                return;

            FirmMember local = FirmMember.Local;
            int own = local != null ? local.Firm : 0;
            int rival = 1 - own;
            _ownText.text = FirmRow(own, local != null ? "YOUR FIRM" : "FIRM");
            _rivalText.text = FirmRow(rival, local != null ? "RIVAL FIRM" : "FIRM");
            _ownSwatch.color = _match.ColourFor(own);
            _rivalSwatch.color = _match.ColourFor(rival);
            RefreshBriefcaseRow();
        }

        string FirmRow(int firm, string tag)
        {
            int checkpoint = _match.CheckpointFloor(firm);
            string back = checkpoint > 0 ? _match.Arena.FloorLabel(checkpoint) : "Reception";
            return $"<size=70%>{tag}</size>  <b>{_match.FirmName(firm)}</b>\n<size=85%>Back to: {back}</size>";
        }

        void RefreshBriefcaseRow()
        {
            if (!_match.IsLive)
                return;

            BriefcaseStatus status = _match.CaseStatus;
            byte holder = status.State == BriefcaseState.Carried ? status.CarrierFirm : MatchDirector.NoFirm;
            _briefcaseSwatch.color = _match.ColourFor(holder);
            string place = _match.PlaceLabel(status.Place, status.Floor);
            string where = status.State == BriefcaseState.Returning ? "Coming back to " + place : place;
            string open = _match.Arena.FloorLabel(_match.OpenFloor);
            _briefcaseText.text = $"<size=70%>BRIEFCASE</size>  <b>{Holder(status)}</b>\n<size=85%>{where}  ·  open up to {open}</size>";
        }

        string Holder(BriefcaseStatus status)
        {
            FirmMember local = FirmMember.Local;
            switch (status.State)
            {
                case BriefcaseState.Carried:
                    if (local != null && status.CarrierId == local.ObjectId)
                        return "You have it";
                    if (local != null)
                        return status.CarrierFirm == local.Firm ? "Your firm has it" : "The rival firm has it";
                    return _match.FirmName(status.CarrierFirm) + " has it";
                case BriefcaseState.Returning:
                    return "Left the buildings";
                default:
                    return "Nobody has it";
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

            switch (notice)
            {
                case MatchNotice.RoofNeedsBriefcase:
                    BriefcaseStatus status = _match.CaseStatus;
                    Say($"The roof only counts with the briefcase. It is on {_match.PlaceLabel(status.Place, status.Floor)}.");
                    break;
                case MatchNotice.FellOut:
                    int checkpoint = _match.CheckpointFloor(local.Firm);
                    Say($"You left the building. Back to {(checkpoint > 0 ? _match.Arena.FloorLabel(checkpoint) : "Reception")}.");
                    break;
                case MatchNotice.BriefcaseKnockedLoose:
                    Say("You dropped the briefcase.");
                    break;
                case MatchNotice.GateRefused:
                    Say($"{_match.Arena.FloorLabel(_match.OpenFloor + 1)} is shut. Whoever carries the briefcase up there opens it for everyone.");
                    break;
            }
        }

        void OnPickedUp(int firm)
        {
            FirmMember local = FirmMember.Local;
            if (local == null || _match.IsCarrying(local))
                return;

            Say(firm == local.Firm ? "Your firm has the briefcase. Keep them climbing." : "The rival firm has the briefcase. Take it off them.");
        }

        void OnCheckpointSaved(int firm, int floor)
        {
            RefreshRows();
            FirmMember local = FirmMember.Local;
            string label = _match.Arena.FloorLabel(floor);
            if (local != null && local.Firm == firm)
                Say($"{label} reached. Your firm comes back here after a fall.");
            else
                Say($"The rival firm reached {label}.");
        }

        void OnOpenFloorChanged(int floor)
        {
            RefreshBriefcaseRow();
            Say($"{_match.Arena.FloorLabel(floor)} is open, in both towers.");
        }

        void OnReturning()
        {
            BriefcaseStatus status = _match.CaseStatus;
            Say($"The briefcase left the buildings. It comes back to {_match.PlaceLabel(status.Place, status.Floor)}.");
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
