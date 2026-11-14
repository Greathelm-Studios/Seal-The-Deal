using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// One screen or dialog in the kit's screen stack. Showing slides and fades it in (24 px, OUT-EXPO) with its
    /// lines revealing one after another; hiding slides it 16 px back and fades (IN), and Reduce motion turns both
    /// into a fade. Input works from the first frame of the show; nothing waits for the animation.
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

        CanvasGroup _group;
        Vector2 _home;
        float[] _revealHome;
        UiTween _alpha;
        UiTween _slide;
        UiTween[] _revealTweens;
        bool _hiding;
        GameObject _lastFocus;

        public bool IsDialog => _isDialog;

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

        internal void Show(bool fromBelow)
        {
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
            _alpha.Start(fromBelow ? _group.alpha : 0f, 1f, duration, _tokens.EaseOut);
            float from = reduced ? 0f : (fromBelow ? -_tokens.PanelSlideIn : _tokens.PanelSlideIn);
            _slide.Start(from, 0f, duration, _tokens.EaseOutExpo);
            StartReveal(reduced);
            Apply();
            UiMotion.Wake(this);
            Refocus();
        }

        internal void Hide(bool toBelow)
        {
            _hiding = true;
            // Raycasts off, but the group stays interactable: switching that off would draw every control greyed out
            // while the screen fades. Focus leaves with the screen instead.
            _group.blocksRaycasts = false;
            ReleaseFocus();
            OnHiding();

            bool reduced = UiMotion.Reduced;
            float duration = reduced ? _tokens.ReducedFade : _tokens.PanelOut;
            _alpha.Start(_group.alpha, 0f, duration, _tokens.EaseIn);
            float to = reduced ? 0f : (toBelow ? _tokens.PanelSlideOut : -_tokens.PanelSlideOut);
            _slide.Start(_slide.Value, to, duration, _tokens.EaseIn);
            UiMotion.Wake(this);
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
        }

        static bool IsUsable(GameObject go) => !go.TryGetComponent(out Selectable s) || s.IsInteractable();

        void StartReveal(bool reduced)
        {
            int max = Mathf.Max(1, _tokens.RevealMaxLines);
            for (int i = 0; i < _reveal.Length; i++)
            {
                float delay = reduced ? 0f : Mathf.Min(i, max - 1) * _tokens.RevealStagger;
                _revealTweens[i].Start(0f, 1f, reduced ? _tokens.ReducedFade : _tokens.RevealDuration, _tokens.EaseOut, delay);
            }
        }

        public bool Tick(float dt)
        {
            bool moving = _alpha.Running || _slide.Running;
            _alpha.Step(dt);
            _slide.Step(dt);
            for (int i = 0; i < _revealTweens.Length; i++)
            {
                moving |= _revealTweens[i].Running;
                _revealTweens[i].Step(dt);
            }

            Apply();
            if (!moving && _hiding)
            {
                _hiding = false;
                gameObject.SetActive(false);
            }

            return moving;
        }

        void Apply()
        {
            _group.alpha = _alpha.Value;
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
        }
    }
}
