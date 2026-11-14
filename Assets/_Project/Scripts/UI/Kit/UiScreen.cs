using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// One screen or dialog in the kit's screen stack. A dialog slides and fades in over the screen below, behind a
    /// paper veil. Full screens change through paper white, never through a muddy mix of the two: the old screen's
    /// content fades and its sky window rolls up (its paper stays), then the new screen's paper settles over it
    /// (paper on paper, unseen), its sky rolls down and its lines reveal one after another. Reduce motion turns the
    /// slides and the roll into fades. Input works from the first frame of the show; only the visuals wait.
    /// Subclasses fill in what the screen says and does. Design: docs/systems/UI_KIT.md "Screens".
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UiScreen : MonoBehaviour, IUiAnimated
    {
        [Tooltip("The kit's tokens.")]
        [SerializeField] protected UiTokens _tokens;
        [Tooltip("The part that slides. The screen's own rectangle (and its veil) stays put.")]
        [SerializeField] RectTransform _content;
        [Tooltip("Lines that reveal one after another as the screen opens, top to bottom. Each needs a Canvas Group.")]
        [SerializeField] CanvasGroup[] _reveal = new CanvasGroup[0];
        [Tooltip("What gets focus when the screen opens: the safe or most likely choice.")]
        [SerializeField] protected Selectable _firstFocus;
        [Tooltip("A dialog sits over the screen below it (which stays visible, behind a paper veil). A full screen hides the one below.")]
        [SerializeField] bool _isDialog;
        [Tooltip("The paper veil behind a dialog. Leave empty on a full screen.")]
        [SerializeField] Image _veil;
        [Tooltip("Fades the content (everything but the paper field) of a full screen. On the content rect.")]
        [SerializeField] CanvasGroup _contentGroup;
        [Tooltip("Optional: a sky window that rolls down from the top as the screen opens and back up as it closes.")]
        [SerializeField] RectTransform _wipe;
        [Tooltip("Fades the sky window instead of rolling it, with Reduce motion on.")]
        [SerializeField] CanvasGroup _wipeGroup;

        CanvasGroup _group;
        Vector2 _home;
        float[] _revealHome;
        UiTween _alpha;
        UiTween _slide;
        UiTween[] _revealTweens;
        UiTween _contentAlpha;
        UiTween _roll;
        float _hideLeft;
        bool _hiding;
        GameObject _lastFocus;

        public bool IsDialog => _isDialog;

        /// <summary>
        /// Lane 8's transition the stack plays when this screen is opened over another or closed back to it (the change
        /// happens while the view is covered). Null: the screen's own slide and fade only (dialogs, the quit question).
        /// </summary>
        public virtual CloseTheDeal.UI.Title.UiTransitionKind? Through => null;

        /// <summary>The sound the stack plays as this screen opens: PanelOpen for a dialog, Transition for a screen.</summary>
        public virtual UiSoundEvent OpenSound => _isDialog ? UiSoundEvent.PanelOpen : UiSoundEvent.Transition;

        /// <summary>A subclass's own motion, stepped with the screen's: return true while it is still moving.</summary>
        protected virtual bool TickExtra(float dt) => false;

        /// <summary>Runs after the screen has applied its motion each step: a subclass layers its own on top.</summary>
        protected virtual void AfterApply() { }

        /// <summary>Asks for the screen's motion to be stepped (a subclass started its own tween).</summary>
        protected void WakeMotion() => UiMotion.Wake(this);

        /// <summary>The stack this screen is on, set when pushed.</summary>
        public UiScreenStack Stack { get; internal set; }

        protected virtual void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_content != null)
                _home = _content.anchoredPosition;
            _revealTweens = new UiTween[_reveal.Length];
            _revealHome = new float[_reveal.Length];
            for (int i = 0; i < _reveal.Length; i++)
                _revealHome[i] = _reveal[i] != null ? ((RectTransform)_reveal[i].transform).anchoredPosition.y : 0f;
        }

        protected virtual void OnDisable() => UiMotion.Sleep(this);

        /// <summary>Back / Esc / the pad's B while this screen is on top. Default: close it.</summary>
        public virtual void OnCancel()
        {
            if (Stack != null)
                Stack.Pop();
        }

        /// <summary>Called as the screen starts to show, before the first frame. Refresh what it says here.</summary>
        protected virtual void OnShowing() { }

        /// <summary>Called as the screen starts to hide.</summary>
        protected virtual void OnHiding() { }

        /// <summary>
        /// Opens the screen. For a full screen, delay is how long the screen below takes to clear its content: this one
        /// waits that long before its paper settles, so the change goes through paper. Input is live at once.
        /// </summary>
        internal void Show(bool fromBelow, float delay = 0f)
        {
            bool stillShowing = gameObject.activeSelf && _hiding; // Brought back mid-hide: carry on from where it is.
            if (stillShowing)
                delay = 0f;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UiFocusFrame.BringToFront();
            _hiding = false;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            OnCovered(false);
            OnShowing();

            bool reduced = UiMotion.Reduced;
            float duration = reduced ? _tokens.ReducedFade : _tokens.PanelIn;
            if (_isDialog)
            {
                _alpha.Start(fromBelow ? _group.alpha : 0f, 1f, duration, _tokens.EaseOut);
                _contentAlpha.Snap(1f);
            }
            else
            {
                _alpha.Start(stillShowing ? _group.alpha : 0f, 1f, _tokens.HoverOut, _tokens.EaseOut, delay); // paper over paper: unseen, never a cut
                _contentAlpha.Start(stillShowing ? _contentAlpha.Value : 0f, 1f, duration, _tokens.EaseOut, delay);
                _roll.Start(stillShowing ? _roll.Value : 0f, 1f, duration * 1.2f, _tokens.EaseOutExpo, delay);
            }

            float from = reduced ? 0f : (fromBelow ? -_tokens.PanelSlideIn : _tokens.PanelSlideIn);
            _slide.Start(from, 0f, duration, _tokens.EaseOutExpo, _isDialog ? 0f : delay);
            StartReveal(reduced, _isDialog ? 0f : delay);
            Apply();
            UiMotion.Wake(this);
            Refocus();
        }

        /// <summary>Hides the screen. alone: nothing takes its place (the game takes over), so its paper fades too.</summary>
        internal void Hide(bool toBelow, bool alone = false)
        {
            _hiding = true;
            // Raycasts off, but the group stays interactable: switching that off would draw every control greyed out
            // while the screen fades. Focus leaves with the screen instead.
            _group.blocksRaycasts = false;
            ReleaseFocus();
            OnHiding();

            bool reduced = UiMotion.Reduced;
            float duration = reduced ? _tokens.ReducedFade : _tokens.PanelOut;
            if (_isDialog)
                _alpha.Start(_group.alpha, 0f, duration, _tokens.EaseIn);
            else
            {
                _contentAlpha.Start(_contentAlpha.Value, 0f, duration, _tokens.EaseIn);
                _roll.Start(_roll.Value, 0f, duration, _tokens.EaseIn);
                if (alone)
                    _alpha.Start(_group.alpha, 0f, duration, _tokens.EaseIn, duration * 0.5f);
            }

            float to = reduced ? 0f : (toBelow ? _tokens.PanelSlideOut : -_tokens.PanelSlideOut);
            _slide.Start(_slide.Value, to, duration, _tokens.EaseIn);
            // A full screen keeps its paper until the next screen's paper has settled over it.
            _hideLeft = duration + (_isDialog ? 0f : _tokens.HoverOut + _tokens.HoverOut);
            UiMotion.Wake(this);
        }

        /// <summary>
        /// Ends the screen's show or hide motion at once (a hiding screen switches off). The stack calls it for a change
        /// made under one of lane 8's transitions, so the reveal shows the new screen finished, not still sliding in.
        /// </summary>
        internal void Settle()
        {
            _alpha.Snap(_alpha.To);
            _slide.Snap(_slide.To);
            _contentAlpha.Snap(_contentAlpha.To);
            _roll.Snap(_roll.To);
            for (int i = 0; i < _revealTweens.Length; i++)
                _revealTweens[i].Snap(_revealTweens[i].To);
            Apply();
            if (_hiding)
            {
                _hiding = false;
                _hideLeft = 0f;
                gameObject.SetActive(false);
            }
        }

        /// <summary>How long a full screen takes to clear its content when it hides: the next screen waits this long.</summary>
        internal float ClearTime => _isDialog ? 0f : (UiMotion.Reduced ? _tokens.ReducedFade : _tokens.PanelOut);

        void ApplyRoll()
        {
            if (_wipe == null)
                return;
            bool reduced = UiMotion.Reduced;
            float t = Mathf.Clamp01(_roll.Value);
            _wipe.localScale = new Vector3(1f, reduced ? 1f : Mathf.Max(0.0001f, t), 1f);
            if (_wipeGroup != null)
                _wipeGroup.alpha = reduced ? t : 1f;
        }

        /// <summary>A dialog went up over this screen: stop taking input, keep showing.</summary>
        internal void Cover()
        {
            _group.blocksRaycasts = false;
            ReleaseFocus();
            OnCovered(true);
        }

        void ReleaseFocus()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || !selected.transform.IsChildOf(transform))
                return;
            _lastFocus = selected;
            EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>A dialog went up over this screen (true) or closed (false). The title hides its menu under the dialog.</summary>
        protected virtual void OnCovered(bool covered) { }

        /// <summary>The dialog over this screen closed: take input again and put focus back where it was.</summary>
        internal void Uncover()
        {
            _group.blocksRaycasts = true;
            OnCovered(false);
            OnShowing();
            Refocus();
        }

        /// <summary>Puts focus on what the player last used here, else on the first-focus control.</summary>
        public void Refocus()
        {
            if (EventSystem.current == null)
                return;
            GameObject target = _lastFocus != null && _lastFocus.activeInHierarchy && IsUsable(_lastFocus) ? _lastFocus : null;
            if (target == null && _firstFocus != null && _firstFocus.gameObject.activeInHierarchy)
                target = _firstFocus.gameObject;
            if (target != null)
                EventSystem.current.SetSelectedGameObject(target);
            UiFocusFrame.WakeUp();
        }

        static bool IsUsable(GameObject go) => !go.TryGetComponent(out Selectable s) || s.IsInteractable();

        void StartReveal(bool reduced, float delay)
        {
            int max = Mathf.Max(1, _tokens.RevealMaxLines);
            for (int i = 0; i < _reveal.Length; i++)
            {
                float stagger = reduced ? 0f : Mathf.Min(i, max - 1) * _tokens.RevealStagger;
                _revealTweens[i].Start(0f, 1f, reduced ? _tokens.ReducedFade : _tokens.RevealDuration, _tokens.EaseOut, delay + stagger);
            }
        }

        public bool Tick(float dt)
        {
            bool moving = _alpha.Running || _slide.Running || _contentAlpha.Running || _roll.Running;
            moving |= TickExtra(dt);
            _alpha.Step(dt);
            _slide.Step(dt);
            _contentAlpha.Step(dt);
            _roll.Step(dt);
            for (int i = 0; i < _revealTweens.Length; i++)
            {
                moving |= _revealTweens[i].Running;
                _revealTweens[i].Step(dt);
            }

            Apply();
            if (_hiding)
            {
                _hideLeft -= dt;
                moving |= _hideLeft > 0f;
                if (_hideLeft <= 0f && !moving)
                {
                    _hiding = false;
                    gameObject.SetActive(false);
                }
            }

            return moving;
        }

        void Apply()
        {
            _group.alpha = _alpha.Value;
            if (_contentGroup != null)
                _contentGroup.alpha = _contentAlpha.Value;
            ApplyRoll();
            if (_content != null)
                _content.anchoredPosition = _home + new Vector2(_slide.Value, 0f);
            if (_veil != null)
                _veil.color = UiTokens.WithAlpha(_tokens.Paper, _tokens.VeilAlpha);

            bool reduced = UiMotion.Reduced;
            for (int i = 0; i < _reveal.Length; i++)
            {
                CanvasGroup line = _reveal[i];
                if (line == null)
                    continue;
                float t = _revealTweens[i].Value;
                line.alpha = t;
                var rect = (RectTransform)line.transform;
                Vector2 p = rect.anchoredPosition;
                p.y = _revealHome[i] - (reduced ? 0f : (1f - t) * _tokens.RevealRise);
                rect.anchoredPosition = p;
            }

            AfterApply();
        }
    }
}
