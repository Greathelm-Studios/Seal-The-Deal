using System.Collections.Generic;
using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The briefcase match's in-game readout, drawn with the UI kit (plates, status line, key
    /// hint, tokens): the clock and its cap warning, the briefcase tracker, the open-floor
    /// ladder, the countdown, the floor-opened banner, one-off lines and refusals, the key
    /// prompt, and a marker over the briefcase on screen (pinned to the edge when it is behind
    /// or off screen). Text changes only on match events; motion runs on the kit's driver only
    /// while something moves, and the marker is placed each frame from local positions (no
    /// network). Sounds are MatchSounds'. Built by the lane setup tool. Design: MATCH.md.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour, IUiAnimated
    {
        [Header("Wiring")]
        [SerializeField] MatchDirector _match;
        [SerializeField] UiTokens _tokens;
        [SerializeField] GameObject _root;

        [Header("Clock")]
        [SerializeField] RectTransform _clockPlate;
        [SerializeField] TMP_Text _clockText;
        [SerializeField] TMP_Text _clockCaption;

        [Header("Briefcase tracker")]
        [SerializeField] Image _trackerBar;
        [SerializeField] TMP_Text _trackerHolder;
        [SerializeField] TMP_Text _trackerWhere;

        [Header("Open-floor ladder")]
        [SerializeField] RectTransform _ladderPlate;
        [SerializeField] RectTransform _ladder;
        [Tooltip("A floor anyone may stand on: a filled square.")]
        [SerializeField] Sprite _openSprite;
        [Tooltip("A floor still shut: a hollow square, so open and shut differ in shape as well as colour.")]
        [SerializeField] Sprite _shutSprite;
        [SerializeField] TMP_Text _ladderCaption;
        [Tooltip("One floor's row, copied once per floor when the towers are built. Has a Box (Image), a Label (text) and a Mark (Image) child.")]
        [SerializeField] RectTransform _ladderRow;

        [Header("Countdown")]
        [SerializeField] CanvasGroup _countdownGroup;
        [SerializeField] TMP_Text _countdownText;

        [Header("Banner and lines")]
        [SerializeField] RectTransform _banner;
        [SerializeField] CanvasGroup _bannerGroup;
        [SerializeField] TMP_Text _bannerText;
        [SerializeField] RectTransform _notice;
        [SerializeField] CanvasGroup _noticeGroup;
        [SerializeField] UiStatusLine _noticeLine;

        [Header("Prompt and marker")]
        [SerializeField] GameObject _prompt;
        [SerializeField] TMP_Text _promptKey;
        [SerializeField] TMP_Text _promptLabel;
        [SerializeField] RectTransform _marker;
        [SerializeField] Image _markerFill;
        [SerializeField] Image _markerEdgeImage;

        [Header("Timing")]
        [Tooltip("Seconds a line such as 'The roof only counts with the briefcase' stays up.")]
        [Min(0.5f)] [SerializeField] float _noticeSeconds = 4f;

        [Tooltip("Seconds the floor-opened banner stays up before it slides away.")]
        [Min(0.5f)] [SerializeField] float _bannerSeconds = 2.6f;

        [Tooltip("How close to the screen's edge the briefcase marker still shows, pixels at 1080p; nearer the edge it hides (the UI kit's edge marker takes over there).")]
        [Min(0f)] [SerializeField] float _markerEdge = 48f;

        /// <summary>How far the banner slides in from above, pixels at 1080p.</summary>
        const float BannerDrop = 24f;
        /// <summary>How much bigger each countdown number starts.</summary>
        const float CountdownPop = 0.35f;
        /// <summary>The clock plate's gentle swell once the quarter is closing, as a fraction of its size.</summary>
        const float WarnSwell = 0.04f;

        readonly List<Image> _rowBoxes = new();
        readonly List<TMP_Text> _rowLabels = new();
        readonly List<Image> _rowMarks = new();

        Vector2 _bannerHome;
        Vector2 _noticeHome;
        UiTween _bannerTween;
        UiTween _noticeTween;
        UiTween _countdownTween;
        UiSpring _shake;
        float _bannerUntil = -1f;
        float _noticeUntil = -1f;
        bool _capWarning;
        bool _showingRefusal;
        bool _awake;
        RectTransform _canvasRect;
        Camera _uiCamera;

        // ---- Lifecycle ---------------------------------------------------------------------------

        void Awake()
        {
            _bannerHome = _banner.anchoredPosition;
            _noticeHome = _notice.anchoredPosition;
            _ladderRow.gameObject.SetActive(false);
            Canvas canvas = GetComponentInParent<Canvas>();
            _canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            _uiCamera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        }

        void OnEnable()
        {
            _match.OnStarted += RefreshAll;
            _match.OnClosed += Hide;
            _match.OnPhaseChanged += OnPhaseChanged;
            _match.OnCountdownChanged += OnCountdownChanged;
            _match.OnClockChanged += ShowClock;
            _match.OnCapWarning += OnCapWarning;
            _match.OnBriefcaseChanged += OnBriefcaseChanged;
            _match.OnCheckpointSaved += OnCheckpointSaved;
            _match.OnBriefcasePickedUp += OnPickedUp;
            _match.OnBriefcaseStolen += OnStolen;
            _match.OnBriefcaseReturning += OnReturning;
            _match.OnFloorOpened += OnFloorOpened;
            _match.OnArenaChanged += RebuildLadder;
            FirmMember.OnLocalChanged += RefreshAll;
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
            _match.OnCapWarning -= OnCapWarning;
            _match.OnBriefcaseChanged -= OnBriefcaseChanged;
            _match.OnCheckpointSaved -= OnCheckpointSaved;
            _match.OnBriefcasePickedUp -= OnPickedUp;
            _match.OnBriefcaseStolen -= OnStolen;
            _match.OnBriefcaseReturning -= OnReturning;
            _match.OnFloorOpened -= OnFloorOpened;
            _match.OnArenaChanged -= RebuildLadder;
            FirmMember.OnLocalChanged -= RefreshAll;
            FirmMember.OnNotice -= OnNotice;
            FirmMember.OnPromptChanged -= ShowPrompt;
            UiMotion.Sleep(this);
            _awake = false;
        }

        void Hide()
        {
            _root.SetActive(false);
            _bannerUntil = -1f;
            _noticeUntil = -1f;
        }

        void RefreshAll()
        {
            if (!_match.IsLive)
                return;

            _root.SetActive(true);
            _bannerGroup.alpha = 0f;
            _noticeGroup.alpha = 0f;
            _countdownGroup.alpha = 0f;
            _capWarning = _match.Phase == MatchPhase.Running && _match.SecondsLeft <= _match.Profile.CapWarningSeconds;
            ShowPrompt(MatchPrompt.None);
            ShowClock(_match.SecondsLeft);
            RebuildLadder();
            RefreshTracker();
        }

        void OnPhaseChanged(MatchPhase previous, MatchPhase next)
        {
            _capWarning = false;
            ShowClock(_match.SecondsLeft);
            RefreshTracker();
            RefreshLadder();
            if (next != MatchPhase.Countdown)
                _countdownGroup.alpha = 0f;
            if (next == MatchPhase.Running)
                Say("The quarter is open. Get the briefcase onto a roof.", UiStatusKind.Info);
        }

        // ---- Clock and countdown --------------------------------------------------------------------

        void ShowClock(int secondsLeft)
        {
            _clockText.text = FormatClock(secondsLeft);
            _clockText.color = _capWarning ? _tokens.Amber : _tokens.Navy;
            // Said in words as well as colour.
            _clockCaption.text = _capWarning ? "QUARTER CLOSING" : "QUARTER";
            // A returning case's own countdown ticks with the clock rather than every frame.
            if (_match.IsLive && _match.CaseStatus.State == BriefcaseState.Returning)
                RefreshTracker();
        }

        public static string FormatClock(int seconds)
        {
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        void OnCapWarning(int secondsLeft)
        {
            _capWarning = true;
            ShowClock(secondsLeft);
            Say($"The quarter closes in {FormatClock(secondsLeft)}. If nobody reaches a roof, the firm holding the briefcase wins.", UiStatusKind.Info);
        }

        void OnCountdownChanged(int secondsLeft)
        {
            if (secondsLeft <= 0)
            {
                _countdownGroup.alpha = 0f;
                return;
            }

            _countdownText.text = secondsLeft.ToString();
            _countdownGroup.alpha = 1f;
            _countdownTween.Start(1f + CountdownPop, 1f, _tokens.PanelIn, _tokens.EaseOutExpo);
            WakeUp();
        }

        // ---- Briefcase tracker ----------------------------------------------------------------------

        void OnBriefcaseChanged(BriefcaseStatus previous, BriefcaseStatus next)
        {
            RefreshTracker();
            if (previous.Floor != next.Floor || previous.State != next.State)
                RefreshLadder();
        }

        void RefreshTracker()
        {
            if (!_match.IsLive)
                return;

            BriefcaseStatus status = _match.CaseStatus;
            byte holder = status.State == BriefcaseState.Carried ? status.CarrierFirm : MatchDirector.NoFirm;
            _trackerBar.color = holder == MatchDirector.NoFirm ? _tokens.Silver : _match.ColourFor(holder);
            _trackerHolder.text = Holder(status);
            string place = _match.PlaceLabel(status.Place, status.Floor);
            _trackerWhere.text = status.State == BriefcaseState.Returning
                ? $"Back to {place} in {_match.ReturnSecondsLeft} s"
                : place;
            _markerFill.color = _trackerBar.color;
        }

        string Holder(BriefcaseStatus status)
        {
            FirmMember local = FirmMember.Local;
            switch (status.State)
            {
                case BriefcaseState.Carried:
                    if (local != null && status.CarrierId == local.ObjectId)
                        return "You have the briefcase";
                    if (local != null)
                        return status.CarrierFirm == local.Firm ? "Your firm has it" : "The rival firm has it";
                    return _match.FirmName(status.CarrierFirm) + " has it";
                case BriefcaseState.Returning:
                    return "It left the buildings";
                default:
                    return "Nobody has the briefcase";
            }
        }

        // ---- Open-floor ladder ----------------------------------------------------------------------

        /// <summary>One row per floor, roof at the top, copied from the template row. Only when the towers change.</summary>
        void RebuildLadder()
        {
            MatchArena arena = _match.Arena;
            if (!arena.Ready)
                return;

            int count = arena.FloorCount;
            while (_rowBoxes.Count < count)
            {
                RectTransform row = Instantiate(_ladderRow, _ladder);
                row.gameObject.SetActive(true);
                _rowBoxes.Add(row.Find("Box").GetComponent<Image>());
                _rowLabels.Add(row.Find("Label").GetComponent<TMP_Text>());
                _rowMarks.Add(row.Find("Mark").GetComponent<Image>());
            }

            float height = _ladderRow.sizeDelta.y;
            // The plate grows with the tower: its header, then one row per floor.
            _ladderPlate.sizeDelta = new Vector2(_ladderPlate.sizeDelta.x, -_ladder.anchoredPosition.y + count * height + 12f);
            for (int i = 0; i < _rowBoxes.Count; i++)
            {
                RectTransform row = (RectTransform)_rowBoxes[i].transform.parent;
                bool used = i < count;
                row.gameObject.SetActive(used);
                if (!used)
                    continue;

                // Roof at the top: floor i sits (count - 1 - i) rows down.
                row.anchoredPosition = new Vector2(0f, -(count - 1 - i) * height);
                _rowLabels[i].text = arena.FloorLabel(i);
            }

            RefreshLadder();
        }

        /// <summary>
        /// Open floors filled, shut ones hollow; a square marks the briefcase's floor in its
        /// holder's colour; your firm's checkpoint is named on its row. Shape says it as well as colour.
        /// </summary>
        void RefreshLadder()
        {
            MatchArena arena = _match.Arena;
            if (!arena.Ready || _rowBoxes.Count < arena.FloorCount)
                return;

            int open = _match.OpenFloor;
            BriefcaseStatus status = _match.CaseStatus;
            FirmMember local = FirmMember.Local;
            int checkpoint = local != null ? _match.CheckpointFloor(local.Firm) : -1;
            int caseFloor = status.State == BriefcaseState.Returning ? -1 : status.Floor;
            for (int i = 0; i < arena.FloorCount; i++)
                StyleRow(i, i <= open, i == checkpoint && checkpoint > 0, i == caseFloor);

            _ladderCaption.text = "OPEN TO " + arena.FloorLabel(open).ToUpperInvariant();
        }

        void StyleRow(int floor, bool isOpen, bool respawnHere, bool caseHere)
        {
            _rowBoxes[floor].sprite = isOpen ? _openSprite : _shutSprite;
            _rowBoxes[floor].color = isOpen ? _tokens.Cobalt : _tokens.Ink3;
            _rowLabels[floor].color = isOpen ? _tokens.Navy : _tokens.Ink3;
            string label = _match.Arena.FloorLabel(floor);
            _rowLabels[floor].text = respawnHere ? label + "  · you respawn here" : label;
            _rowMarks[floor].enabled = caseHere;
            if (caseHere)
                _rowMarks[floor].color = _trackerBar.color;
        }

        void OnCheckpointSaved(int firm, int floor)
        {
            RefreshLadder();
            FirmMember local = FirmMember.Local;
            string label = _match.Arena.FloorLabel(floor);
            if (local != null && local.Firm == firm)
                Say($"{label} reached. Your firm comes back here after a fall.", UiStatusKind.Info);
            else
                Say($"The rival firm reached {label}.", UiStatusKind.Info);
        }

        // ---- Banner ---------------------------------------------------------------------------------

        void OnFloorOpened(int floor, int byFirm)
        {
            RefreshLadder();
            // A "shut" line still showing is now wrong: let it go as the banner arrives.
            if (_showingRefusal && _noticeUntil >= 0f)
                _noticeUntil = Time.unscaledTime;
            FirmMember local = FirmMember.Local;
            string who = local == null || byFirm == MatchDirector.NoFirm ? _match.FirmName(byFirm) : byFirm == local.Firm ? "your firm" : "the rival firm";
            _bannerText.text = $"<b>{_match.Arena.FloorLabel(floor).ToUpperInvariant()} OPEN</b>   opened by {who}, in both towers";
            _bannerTween.Start(0f, 1f, _tokens.PanelIn, _tokens.EaseOut);
            _bannerUntil = Time.unscaledTime + _bannerSeconds;
            WakeUp();
        }

        // ---- Lines ----------------------------------------------------------------------------------

        void OnPickedUp(int firm)
        {
            FirmMember local = FirmMember.Local;
            if (local == null || _match.IsCarrying(local))
                return;

            Say(firm == local.Firm ? "Your firm has the briefcase. Keep them climbing." : "The rival firm has the briefcase. Take it off them.", UiStatusKind.Info);
        }

        void OnStolen(int byFirm, int fromFirm)
        {
            FirmMember local = FirmMember.Local;
            if (local != null)
                Say(byFirm == local.Firm ? "Your firm took the briefcase from the rival." : "The rival firm took the briefcase from you.", UiStatusKind.Info);
        }

        void OnReturning()
        {
            BriefcaseStatus status = _match.CaseStatus;
            Say($"The briefcase left the buildings. It comes back to {_match.PlaceLabel(status.Place, status.Floor)}.", UiStatusKind.Info);
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
                    Say($"The roof only counts with the briefcase. It is on {_match.PlaceLabel(status.Place, status.Floor)}.", UiStatusKind.Refusal);
                    break;
                case MatchNotice.FirstGateRefusal:
                    Say($"{_match.Arena.FloorLabel(_match.OpenFloor + 1)} is shut. Nobody climbs higher than the briefcase has been. Whoever carries it up there opens the floor for everyone.", UiStatusKind.Refusal);
                    break;
                case MatchNotice.GateRefused:
                    Say($"{_match.Arena.FloorLabel(_match.OpenFloor + 1)} is shut until the briefcase is carried up.", UiStatusKind.Refusal);
                    break;
                case MatchNotice.FellOut:
                    int checkpoint = _match.CheckpointFloor(local.Firm);
                    Say($"You left the building. Back to {(checkpoint > 0 ? _match.Arena.FloorLabel(checkpoint) : "Reception")}.", UiStatusKind.Info);
                    break;
                case MatchNotice.BriefcaseKnockedLoose:
                    Say("You dropped the briefcase.", UiStatusKind.Info);
                    break;
            }
        }

        /// <summary>Shows one line on the kit's status line; a refusal gets the amber mark and the kit's shake.</summary>
        void Say(string line, UiStatusKind kind)
        {
            _noticeLine.Set(line, kind);
            _showingRefusal = kind == UiStatusKind.Refusal;
            _noticeTween.Start(_noticeGroup.alpha, 1f, _tokens.PanelIn, _tokens.EaseOut);
            _noticeUntil = Time.unscaledTime + _noticeSeconds;
            if (kind == UiStatusKind.Refusal && !UiMotion.Reduced)
            {
                _shake.Snap(_tokens.ErrorShakeDistance);
                _shake.Target = 0f;
            }

            WakeUp();
        }

        // ---- Prompt ---------------------------------------------------------------------------------

        void ShowPrompt(MatchPrompt prompt)
        {
            _prompt.SetActive(prompt != MatchPrompt.None);
            if (prompt == MatchPrompt.PickUpBriefcase)
            {
                _promptKey.text = FirmMember.InteractKeyLabel;
                _promptLabel.text = "Pick up the briefcase";
            }
            else if (prompt == MatchPrompt.DropBriefcase)
            {
                _promptKey.text = FirmMember.DropKeyLabel;
                _promptLabel.text = "Drop the briefcase";
            }
        }

        // ---- Marker (local, per frame while shown) --------------------------------------------------

        /// <summary>
        /// A square over the briefcase on screen, pinned to the edge when the case is off screen
        /// or behind. Hidden from the carrier, while it is away, and when the match isn't live.
        /// Pure local projection: nothing is read from the network here.
        /// </summary>
        void LateUpdate()
        {
            Briefcase briefcase = Briefcase.Current;
            Camera view = Camera.main;
            bool show = MarkerWanted(briefcase, view);
            if (_marker.gameObject.activeSelf != show)
                _marker.gameObject.SetActive(show);
            if (show)
                PlaceMarker(briefcase, view);
        }

        bool MarkerWanted(Briefcase briefcase, Camera view)
        {
            if (!_match.IsLive || briefcase == null || view == null || _canvasRect == null)
                return false;
            FirmMember local = FirmMember.Local;
            return _match.CaseStatus.State != BriefcaseState.Returning && !(local != null && _match.IsCarrying(local));
        }

        /// <summary>
        /// Over the case while it is on screen; hidden when it is off screen or behind. The edge
        /// pin for an off-screen case is the UI kit's shared edge marker (lane 5), not built here.
        /// </summary>
        void PlaceMarker(Briefcase briefcase, Camera view)
        {
            Vector3 screen = view.WorldToScreenPoint(briefcase.Centre + Vector3.up * 0.6f);
            float edge = _markerEdge * (Screen.height / 1080f);
            bool onScreen = screen.z > 0f && screen.x > edge && screen.x < Screen.width - edge && screen.y > edge && screen.y < Screen.height - edge;
            if (_markerFill.enabled != onScreen)
            {
                _markerFill.enabled = onScreen;
                _markerEdgeImage.enabled = onScreen;
            }

            if (!onScreen)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, _uiCamera, out Vector2 local);
            _marker.position = _canvasRect.TransformPoint(local);
        }

        // ---- Motion ---------------------------------------------------------------------------------

        void WakeUp()
        {
            if (_awake)
                return;
            _awake = true;
            UiMotion.Wake(this);
        }

        public bool Tick(float dt)
        {
            bool moving = false;

            if (_countdownTween.Running)
            {
                float scale = _countdownTween.Step(dt);
                _countdownText.rectTransform.localScale = Vector3.one * (UiMotion.Reduced ? 1f : scale);
                moving = true;
            }

            moving |= StepBanner(dt);
            moving |= StepNotice(dt);

            if (_capWarning && _match.Phase == MatchPhase.Running && !UiMotion.Reduced)
            {
                float breathe = 1f + WarnSwell * (0.5f + 0.5f * Mathf.Sin(UiMotion.Now * Mathf.PI * 2f / _tokens.BreathePeriod));
                _clockPlate.localScale = Vector3.one * breathe;
                moving = true;
            }
            else
            {
                _clockPlate.localScale = Vector3.one;
            }

            _awake = moving;
            return moving;
        }

        bool StepBanner(float dt)
        {
            if (_bannerUntil >= 0f && Time.unscaledTime > _bannerUntil && !_bannerTween.Running)
            {
                _bannerTween.Start(_bannerGroup.alpha, 0f, _tokens.PanelOut, _tokens.EaseIn);
                _bannerUntil = -1f;
            }

            if (!_bannerTween.Running)
                return _bannerUntil >= 0f;

            float t = _bannerTween.Step(dt);
            _bannerGroup.alpha = t;
            _banner.anchoredPosition = _bannerHome + Vector2.up * (UiMotion.Reduced ? 0f : (1f - t) * BannerDrop);
            return true;
        }

        bool StepNotice(float dt)
        {
            if (_noticeUntil >= 0f && Time.unscaledTime > _noticeUntil && !_noticeTween.Running)
            {
                _noticeTween.Start(_noticeGroup.alpha, 0f, _tokens.PanelOut, _tokens.EaseIn);
                _noticeUntil = -1f;
            }

            bool moving = false;
            if (_noticeTween.Running)
            {
                _noticeGroup.alpha = _noticeTween.Step(dt);
                moving = true;
            }

            if (!_shake.Settled)
            {
                _shake.Step(dt, _tokens.ErrorShake);
                moving = true;
            }

            _notice.anchoredPosition = _noticeHome + Vector2.right * _shake.Value;
            return moving || _noticeUntil >= 0f;
        }
    }
}
