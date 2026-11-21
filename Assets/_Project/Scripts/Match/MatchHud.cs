using System.Collections.Generic;
using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.MatchMode
{
    /// <summary>In-match HUD (clock, briefcase tracker, open-floor ladder, notices, and marker) built from the UI kit. Design: docs/systems/MATCH.md.</summary>
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
        [Tooltip("Amber fill behind the clock's text, shown once the cap warning fires. The text stays Navy on it: amber text on the white plate is unreadable (1.7:1).")]
        [SerializeField] Image _clockWarning;

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
        [Tooltip("Shows the highest open floor and its progress ticks in the compact clock strip.")]
        [SerializeField] bool _compactFloors;
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
        [Tooltip("The UI kit's edge marker (lane 5): over the case on screen, pinned to the screen edge with a chevron and the distance when it is off screen or behind you; a diamond while your firm holds it, a triangle for the rival, a square when loose. Empty = this HUD's own square, on screen only.")]
        [SerializeField] UiEdgeMarker _edgeMarker;

        [Header("Timing")]
        [Tooltip("Seconds a line such as 'The roof only counts with the briefcase' stays up.")]
        [Min(0.5f)] [SerializeField] float _noticeSeconds = 4f;

        [Tooltip("Seconds the floor-opened banner stays up before it slides away.")]
        [Min(0.5f)] [SerializeField] float _bannerSeconds = 2.6f;

        [Tooltip("How close to the screen's edge the briefcase marker still shows, pixels at 1080p; nearer the edge it hides (the UI kit's edge marker takes over there).")]
        [Min(0f)] [SerializeField] float _markerEdge = 48f;

        const float BannerDrop = 24f;
        const float CountdownPop = 0.35f;
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
        Color _clockInk, _clockCaptionInk;
        bool _showingRefusal;
        bool _awake;
        RectTransform _canvasRect;
        Camera _uiCamera;
        Camera _camera;
        MatchPrompt _visiblePrompt;
        UiKeyHint _promptHint;
        bool _wired;
        int _ladderOpen = int.MinValue;
        int _ladderCheckpoint = int.MinValue;
        int _ladderCaseFloor = int.MinValue;
        byte _ladderCaseHolder = byte.MaxValue;
        bool _clockScaled;
        bool _noticeShook;

        void Awake()
        {
            // An empty slot means the scene's HUD predates this script: say so once and stand down.
            // Switching off from here runs OnDisable at once, before anything was subscribed.
            _wired = MatchWiring.Complete(this, nameof(_clockWarning), nameof(_markerEdgeImage), nameof(_edgeMarker), nameof(_openSprite), nameof(_shutSprite));
            if (!_wired)
                return;

            if (_clockPlate != null)
                UiMotion.EnsureSubCanvas(_clockPlate.gameObject);
            UiMotion.EnsureSubCanvas(_banner.gameObject);
            UiMotion.EnsureSubCanvas(_notice.gameObject);

            _clockInk = _clockText.color;
            _clockCaptionInk = _clockCaption.color;
            _promptHint = _prompt.GetComponent<UiKeyHint>();
            _bannerHome = _banner.anchoredPosition;
            _noticeHome = _notice.anchoredPosition;
            _ladderRow.gameObject.SetActive(false);
            Canvas canvas = GetComponentInParent<Canvas>();
            _canvasRect = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            _uiCamera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        }

        void OnEnable()
        {
            // Switched back on by something else while still unwired: stay off.
            if (!_wired)
            {
                enabled = false;
                return;
            }

            _match.OnStarted += RefreshAll;
            _match.OnClosed += Hide;
            _match.OnPhaseChanged += OnPhaseChanged;
            _match.OnCountdownChanged += OnCountdownChanged;
            _match.OnClockChanged += ShowClock;
            _match.OnCapWarning += OnCapWarning;
            _match.OnBriefcaseChanged += OnBriefcaseChanged;
            _match.OnCheckpointSaved += OnCheckpointSaved;
            _match.OnBriefcasePickedUp += OnPickedUp;
            _match.OnBriefcaseDropped += OnDropped;
            _match.OnBriefcaseStolen += OnStolen;
            _match.OnBriefcaseReturning += OnReturning;
            _match.OnFloorOpened += OnFloorOpened;
            _match.OnArenaChanged += RebuildLadder;
            FirmMember.OnLocalChanged += RefreshAll;
            FirmMember.OnNotice += OnNotice;
            FirmMember.OnPromptChanged += ShowPrompt;
            UiGlyphs.Changed += RefreshPromptKey;

            if (_match.IsLive)
                RefreshAll();
            else
                Hide();
        }

        void OnDisable()
        {
            if (!_wired)
                return;

            _match.OnStarted -= RefreshAll;
            _match.OnClosed -= Hide;
            _match.OnPhaseChanged -= OnPhaseChanged;
            _match.OnCountdownChanged -= OnCountdownChanged;
            _match.OnClockChanged -= ShowClock;
            _match.OnCapWarning -= OnCapWarning;
            _match.OnBriefcaseChanged -= OnBriefcaseChanged;
            _match.OnCheckpointSaved -= OnCheckpointSaved;
            _match.OnBriefcasePickedUp -= OnPickedUp;
            _match.OnBriefcaseDropped -= OnDropped;
            _match.OnBriefcaseStolen -= OnStolen;
            _match.OnBriefcaseReturning -= OnReturning;
            _match.OnFloorOpened -= OnFloorOpened;
            _match.OnArenaChanged -= RebuildLadder;
            FirmMember.OnLocalChanged -= RefreshAll;
            FirmMember.OnNotice -= OnNotice;
            FirmMember.OnPromptChanged -= ShowPrompt;
            UiGlyphs.Changed -= RefreshPromptKey;
            UiMotion.Sleep(this);
            _awake = false;
        }

        void Hide()
        {
            _root.SetActive(false);
            _bannerUntil = -1f;
            _noticeUntil = -1f;
            ResetIdleTransforms();
        }

        void RefreshAll()
        {
            if (!_match.IsLive || _match.Phase == MatchPhase.Waiting)
            {
                Hide();
                return;
            }

            _root.SetActive(true);
            _bannerGroup.alpha = 0f;
            _noticeGroup.alpha = 0f;
            _countdownGroup.alpha = 0f;
            ResetIdleTransforms();
            _capWarning = _match.Phase == MatchPhase.Running && _match.SecondsLeft <= _match.Profile.CapWarningSeconds;
            ShowPrompt(MatchPrompt.None);
            ShowClock(_match.SecondsLeft);
            RebuildLadder();
            RefreshTracker();
        }

        void ResetIdleTransforms()
        {
            if (_clockScaled && _clockPlate != null)
            {
                _clockPlate.localScale = Vector3.one;
                _clockScaled = false;
            }

            _shake.Snap(0f);
            if (_noticeShook)
            {
                _notice.anchoredPosition = _noticeHome;
                _noticeShook = false;
            }
        }

        void OnPhaseChanged(MatchPhase previous, MatchPhase next)
        {
            if (next == MatchPhase.Waiting)
            {
                Hide();
                return;
            }
            if (!_root.activeSelf)
                RefreshAll();

            _capWarning = false;
            ShowClock(_match.SecondsLeft);
            RefreshTracker();
            RefreshLadder();
            if (next != MatchPhase.Countdown)
                _countdownGroup.alpha = 0f;
            if (next == MatchPhase.Running && !_compactFloors)
                Say("The quarter is open. Get the briefcase onto a roof.", UiStatusKind.Info);
        }

        void ShowClock(int secondsLeft)
        {
            _clockText.text = FormatClock(secondsLeft);
            // Warned by an amber plate, never amber text: Navy reads at 8:1 on amber, amber at 1.7:1 on white.
            _clockText.color = _capWarning ? _tokens.Navy : _clockInk;
            _clockCaption.color = _capWarning ? _tokens.Navy : _clockCaptionInk;
            if (_clockWarning != null)
                _clockWarning.enabled = _capWarning;
            // Said in words as well as colour.
            _clockCaption.text = _capWarning ? "QUARTER CLOSING" : _compactFloors ? string.Empty : "QUARTER";
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
                        return "Carry it to a roof";
                    if (local != null)
                        return status.CarrierFirm == local.Firm ? "Protect your carrier" : "Stop the rival carrier";
                    return _match.FirmName(status.CarrierFirm) + " has it";
                case BriefcaseState.Returning:
                    return "Briefcase returning";
                default:
                    return "Collect the briefcase";
            }
        }

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
            if (!_compactFloors)
                _ladderPlate.sizeDelta = new Vector2(_ladderPlate.sizeDelta.x, -_ladder.anchoredPosition.y + count * height + 12f);
            for (int i = 0; i < _rowBoxes.Count; i++)
            {
                RectTransform row = (RectTransform)_rowBoxes[i].transform.parent;
                bool used = i < count;
                row.gameObject.SetActive(used);
                if (!used)
                    continue;

                row.anchoredPosition = _compactFloors
                    ? new Vector2(i * Mathf.Min(22f, _ladder.rect.width / count), 0)
                    : new Vector2(0f, -(count - 1 - i) * height);
                _rowLabels[i].text = arena.FloorLabel(i);
            }

            _ladderOpen = int.MinValue;
            _ladderCheckpoint = int.MinValue;
            _ladderCaseFloor = int.MinValue;
            _ladderCaseHolder = byte.MaxValue;
            RefreshLadder();
        }

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
            byte holder = status.State == BriefcaseState.Carried ? status.CarrierFirm : MatchDirector.NoFirm;

            bool openChanged = open != _ladderOpen;
            bool checkpointChanged = checkpoint != _ladderCheckpoint;
            bool caseChanged = caseFloor != _ladderCaseFloor || holder != _ladderCaseHolder;
            if (!openChanged && !checkpointChanged && !caseChanged)
                return;

            if (_ladderOpen == int.MinValue)
            {
                for (int i = 0; i < arena.FloorCount; i++)
                    StyleRow(i, i <= open, i == checkpoint && checkpoint > 0, i == caseFloor);
            }
            else
            {
                if (openChanged)
                {
                    int lo = Mathf.Max(0, Mathf.Min(_ladderOpen, open));
                    int hi = Mathf.Min(arena.FloorCount - 1, Mathf.Max(_ladderOpen, open));
                    for (int i = lo; i <= hi; i++)
                        StyleRow(i, i <= open, i == checkpoint && checkpoint > 0, i == caseFloor);
                }

                if (checkpointChanged)
                {
                    if (_ladderCheckpoint > 0 && _ladderCheckpoint < arena.FloorCount)
                        StyleRow(_ladderCheckpoint, _ladderCheckpoint <= open, false, _ladderCheckpoint == caseFloor);
                    if (checkpoint > 0 && checkpoint < arena.FloorCount)
                        StyleRow(checkpoint, checkpoint <= open, true, checkpoint == caseFloor);
                }

                if (caseChanged)
                {
                    if (_ladderCaseFloor >= 0 && _ladderCaseFloor < arena.FloorCount)
                        StyleRow(_ladderCaseFloor, _ladderCaseFloor <= open, _ladderCaseFloor == checkpoint && checkpoint > 0, _ladderCaseFloor == caseFloor);
                    if (caseFloor >= 0 && caseFloor < arena.FloorCount)
                        StyleRow(caseFloor, caseFloor <= open, caseFloor == checkpoint && checkpoint > 0, true);
                }
            }

            if (openChanged)
                _ladderCaption.text = _compactFloors ? arena.FloorLabel(open) : "OPEN TO " + arena.FloorLabel(open).ToUpperInvariant();

            _ladderOpen = open;
            _ladderCheckpoint = checkpoint;
            _ladderCaseFloor = caseFloor;
            _ladderCaseHolder = holder;
        }

        void StyleRow(int floor, bool isOpen, bool respawnHere, bool caseHere)
        {
            _rowBoxes[floor].sprite = isOpen ? _openSprite : _shutSprite;
            _rowBoxes[floor].color = isOpen ? _tokens.Cobalt : _tokens.Ink3;
            _rowLabels[floor].color = isOpen ? _tokens.Navy : _tokens.Ink3;
            string label = _match.Arena.FloorLabel(floor);
            _rowLabels[floor].text = respawnHere ? label + " · Respawn" : label;
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

        void OnFloorOpened(int floor, int byFirm)
        {
            RefreshLadder();
            if (_showingRefusal && _noticeUntil >= 0f)
                _noticeUntil = Time.unscaledTime;
            FirmMember local = FirmMember.Local;
            string who = local == null || byFirm == MatchDirector.NoFirm ? _match.FirmName(byFirm) : byFirm == local.Firm ? "your firm" : "the rival firm";
            _bannerText.text = $"<b>{_match.Arena.FloorLabel(floor).ToUpperInvariant()} OPEN</b>   opened by {who}, in both towers";
            _bannerTween.Start(0f, 1f, _tokens.PanelIn, _tokens.EaseOut);
            _bannerUntil = Time.unscaledTime + _bannerSeconds;
            WakeUp();
        }

        void OnPickedUp(int firm)
        {
            FirmMember local = FirmMember.Local;
            if (local == null)
                return;
            if (_match.IsCarrying(local))
            {
                Say("Carry the briefcase to a roof.", UiStatusKind.Info);
                return;
            }
            Say(firm == local.Firm ? "Your firm has the briefcase. Keep them climbing." : "The rival firm has the briefcase. Take it off them.", UiStatusKind.Info);
        }

        void OnDropped(int firm)
        {
            Say("Briefcase dropped. Collect it.", UiStatusKind.Info);
        }

        void OnStolen(int byFirm, int fromFirm)
        {
            FirmMember local = FirmMember.Local;
            if (local != null)
                Say(byFirm == local.Firm ? "Your firm took the briefcase from the rival." : "The rival firm took the briefcase from you.", UiStatusKind.Info);
            if (_edgeMarker != null)
                _edgeMarker.Flash();
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
                    Say($"{_match.Arena.FloorLabel(_match.OpenFloor + 1)} is shut. Carry the briefcase upstairs to open it for everyone.", UiStatusKind.Refusal);
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

        void ShowPrompt(MatchPrompt prompt)
        {
            _visiblePrompt = prompt;
            _prompt.SetActive(prompt != MatchPrompt.None);
            if (prompt == MatchPrompt.None)
                return;

            _promptLabel.text = prompt == MatchPrompt.PickUpBriefcase ? "Pick up the briefcase" : "Drop the briefcase";
            RefreshPromptKey();
        }

        void RefreshPromptKey()
        {
            if (_visiblePrompt == MatchPrompt.None)
                return;

            bool pickup = _visiblePrompt == MatchPrompt.PickUpBriefcase;
            string path = UiGlyphs.OnPad
                ? (pickup ? FirmMember.InteractPad : FirmMember.DropPad)
                : (pickup ? FirmMember.InteractKey : FirmMember.DropKey);
            string label = UiGlyphs.Name(path, UiGlyphs.Current);
            if (_promptHint != null)
                _promptHint.SetKeyText(label);
            else
                _promptKey.text = label;

            if (_compactFloors)
            {
                // Match the paper to this action and actual key width, centred under the crosshair.
                var words = _promptLabel.rectTransform;
                float width = Mathf.Ceil(_promptLabel.GetPreferredValues(_promptLabel.text).x);
                words.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                var prompt = (RectTransform)_prompt.transform;
                float padding = _promptKey.rectTransform.anchoredPosition.x;
                prompt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, words.anchoredPosition.x + width + padding);
            }
        }

        void LateUpdate()
        {
            Briefcase briefcase = Briefcase.Current;
            Camera view = ResolveCamera();
            bool show = MarkerWanted(briefcase, view);
            if (_edgeMarker != null)
            {
                TrackOnEdgeMarker(show, briefcase, view);
                show = false; // the kit marker does both the on-screen mark and the edge pin
            }
            if (_marker.gameObject.activeSelf != show)
                _marker.gameObject.SetActive(show);
            if (show)
                PlaceMarker(briefcase, view);
        }

        Camera ResolveCamera()
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;
            return _camera;
        }

        bool MarkerWanted(Briefcase briefcase, Camera view)
        {
            if (!_match.IsLive || briefcase == null || view == null || _canvasRect == null)
                return false;
            FirmMember local = FirmMember.Local;
            return _match.CaseStatus.State != BriefcaseState.Returning && !(local != null && _match.IsCarrying(local));
        }

        void TrackOnEdgeMarker(bool show, Briefcase briefcase, Camera view)
        {
            // Under the HUD root, which is off until the match is live: the marker hasn't woken before then.
            if (!_edgeMarker.isActiveAndEnabled)
                return;
            if (!show)
            {
                if (_edgeMarker.Visible)
                    _edgeMarker.Hide();
                return;
            }

            _edgeMarker.Kind = MarkKind(_match.CaseStatus);
            _edgeMarker.Track(view, briefcase.Centre + Vector3.up * 0.6f);
        }

        static UiMarkKind MarkKind(BriefcaseStatus status)
        {
            FirmMember local = FirmMember.Local;
            if (status.State != BriefcaseState.Carried || local == null)
                return UiMarkKind.Loose;
            return status.CarrierFirm == local.Firm ? UiMarkKind.Ours : UiMarkKind.Rival;
        }

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
                _clockScaled = true;
                moving = true;
            }
            else if (_clockScaled)
            {
                _clockPlate.localScale = Vector3.one;
                _clockScaled = false;
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
                _notice.anchoredPosition = _noticeHome + Vector2.right * _shake.Value;
                _noticeShook = true;
                moving = true;
            }
            else if (_noticeShook)
            {
                _notice.anchoredPosition = _noticeHome;
                _noticeShook = false;
            }

            return moving || _noticeUntil >= 0f;
        }
    }
}
