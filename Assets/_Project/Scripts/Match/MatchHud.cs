using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.MatchMode
{
    /// <summary>In-match HUD for the briefcase (tracker, countdown, notices, prompts, marker) built from the UI kit. Design: docs/systems/MATCH.md.</summary>
    public sealed class MatchHud : MonoBehaviour, IUiAnimated
    {
        [Header("Wiring")]
        [SerializeField] MatchDirector _match;
        [SerializeField] UiTokens _tokens;
        [SerializeField] GameObject _root;

        [Header("Briefcase tracker")]
        [SerializeField] Image _trackerBar;
        [SerializeField] TMP_Text _trackerHolder;
        [SerializeField] TMP_Text _trackerWhere;

        [Header("Countdown")]
        [SerializeField] CanvasGroup _countdownGroup;
        [SerializeField] TMP_Text _countdownText;

        [Header("Lines")]
        [SerializeField] RectTransform _notice;
        [SerializeField] CanvasGroup _noticeGroup;
        [SerializeField] UiStatusLine _noticeLine;

        [Header("Prompt and marker")]
        [SerializeField] GameObject _prompt;
        [SerializeField] TMP_Text _promptKey;
        [SerializeField] TMP_Text _promptLabel;
        [Tooltip("Sizes the prompt's plate to its words and key, centred under the crosshair.")]
        [SerializeField] bool _fitPrompt;
        [SerializeField] RectTransform _marker;
        [SerializeField] Image _markerFill;
        [SerializeField] Image _markerEdgeImage;
        [Tooltip("The UI kit's edge marker: over the case on screen, pinned to the screen edge with a chevron and the distance when it is off screen or behind you; a diamond while your firm holds it, a triangle for the rival, a square when loose. Empty = this HUD's own square, on screen only.")]
        [SerializeField] UiEdgeMarker _edgeMarker;

        [Header("Timing")]
        [Tooltip("Seconds a line such as 'You dropped the briefcase' stays up.")]
        [Min(0.5f)] [SerializeField] float _noticeSeconds = 4f;

        [Tooltip("How close to the screen's edge the briefcase marker still shows, pixels at 1080p; nearer the edge it hides (the UI kit's edge marker takes over there).")]
        [Min(0f)] [SerializeField] float _markerEdge = 48f;

        const float CountdownPop = 0.35f;

        Vector2 _noticeHome;
        UiTween _noticeTween;
        UiTween _countdownTween;
        UiSpring _shake;
        float _noticeUntil = -1f;
        bool _awake;
        RectTransform _canvasRect;
        Camera _uiCamera;
        Camera _camera;
        MatchPrompt _visiblePrompt;
        UiKeyHint _promptHint;
        bool _wired;
        bool _noticeShook;

        void Awake()
        {
            // An empty slot means the scene's HUD predates this script: say so once and stand down.
            // Switching off from here runs OnDisable at once, before anything was subscribed.
            _wired = MatchWiring.Complete(this, nameof(_markerEdgeImage), nameof(_edgeMarker));
            if (!_wired)
                return;

            UiMotion.EnsureSubCanvas(_notice.gameObject);
            _promptHint = _prompt.GetComponent<UiKeyHint>();
            _noticeHome = _notice.anchoredPosition;
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
            _match.OnBriefcaseChanged += OnBriefcaseChanged;
            _match.OnBriefcasePickedUp += OnPickedUp;
            _match.OnBriefcaseDropped += OnDropped;
            _match.OnBriefcaseStolen += OnStolen;
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
            _match.OnBriefcaseChanged -= OnBriefcaseChanged;
            _match.OnBriefcasePickedUp -= OnPickedUp;
            _match.OnBriefcaseDropped -= OnDropped;
            _match.OnBriefcaseStolen -= OnStolen;
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
            _noticeGroup.alpha = 0f;
            _countdownGroup.alpha = 0f;
            ResetIdleTransforms();
            ShowPrompt(MatchPrompt.None);
            RefreshTracker();
        }

        void ResetIdleTransforms()
        {
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

            RefreshTracker();
            if (next != MatchPhase.Countdown)
                _countdownGroup.alpha = 0f;
        }

        public static string FormatClock(int seconds)
        {
            return $"{seconds / 60:00}:{seconds % 60:00}";
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
        }

        void RefreshTracker()
        {
            if (!_match.IsLive)
                return;

            BriefcaseStatus status = _match.CaseStatus;
            byte holder = status.State == BriefcaseState.Carried ? status.CarrierFirm : MatchDirector.NoFirm;
            _trackerBar.color = holder == MatchDirector.NoFirm ? _tokens.Silver : _match.ColourFor(holder);
            _trackerHolder.text = Holder(status);
            _trackerWhere.text = _match.PlaceLabel();
            _markerFill.color = _trackerBar.color;
        }

        string Holder(BriefcaseStatus status)
        {
            if (status.State != BriefcaseState.Carried)
                return MatchDirector.Ruleset != null && MatchDirector.Ruleset.BriefcaseHidden(status) ? "Find the briefcase" : "Collect the briefcase";

            FirmMember local = FirmMember.Local;
            if (local != null && status.CarrierId == local.ObjectId)
                return "You have the briefcase";
            if (local != null)
                return status.CarrierFirm == local.Firm ? "Protect your carrier" : "Stop the rival carrier";
            return _match.FirmName(status.CarrierFirm) + " has it";
        }

        void OnPickedUp(int firm)
        {
            FirmMember local = FirmMember.Local;
            if (local == null || _match.IsCarrying(local))
                return;
            Say(firm == local.Firm ? "Your firm has the briefcase." : "The rival firm has the briefcase. Take it off them.", UiStatusKind.Info);
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

        void OnNotice(MatchNotice notice)
        {
            if (FirmMember.Local == null || !_match.IsLive)
                return;

            switch (notice)
            {
                case MatchNotice.FellOut:
                    Say("You fell out of the building.", UiStatusKind.Info);
                    break;
                case MatchNotice.BriefcaseKnockedLoose:
                    Say("You dropped the briefcase.", UiStatusKind.Info);
                    break;
            }
        }

        void Say(string line, UiStatusKind kind)
        {
            _noticeLine.Set(line, kind);
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

            if (_fitPrompt)
            {
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
            if (MatchDirector.Ruleset != null && MatchDirector.Ruleset.BriefcaseHidden(_match.CaseStatus))
                return false;
            FirmMember local = FirmMember.Local;
            return !(local != null && _match.IsCarrying(local));
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

            moving |= StepNotice(dt);
            _awake = moving;
            return moving;
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
