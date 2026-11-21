using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The one set of focus marks on screen: a 2 px ring, four snapped corner brackets and the chevron cue to
    /// the left of the focused control. They travel to the new control on a spring when focus moves (the
    /// selection itself changes on the input frame), show only while the player is on keys or a pad ("hidden
    /// focus" while the mouse is in use), breathe after a few idle seconds, and nudge at the end of a list.
    /// Feel table §2.2 and §2.4. Lives on the kit canvas above every screen; there is one.
    /// </summary>
    [AddComponentMenu("Close the Deal/UI/Focus Frame")]
    public sealed class UiFocusFrame : MonoBehaviour, IUiAnimated
    {
        static UiFocusFrame _instance;

        [Tooltip("The kit's tokens.")]
        [SerializeField] UiTokens _tokens;
        [Tooltip("The rectangle the marks move as one: sized to the focused control plus the ring gap.")]
        [SerializeField] RectTransform _frame;
        [Tooltip("Fades all the marks in and out together.")]
        [SerializeField] CanvasGroup _group;
        [Tooltip("The 2 px ring around the focused control.")]
        [SerializeField] Image _ring;
        [Tooltip("The four snapped corner brackets: top-left, top-right, bottom-left, bottom-right.")]
        [SerializeField] RectTransform[] _brackets = new RectTransform[4];
        [Tooltip("The chevron cue to the left of the focused control.")]
        [SerializeField] RectTransform _chevron;

        readonly Vector3[] _corners = new Vector3[4];
        RectTransform _target;
        RectTransform _parent;
        UiSpring _x, _y, _w, _h, _bumpX, _bumpY;
        UiTween _shown;
        Vector2[] _bracketHome;
        bool _placed;
        bool _wantVisible;

        /// <summary>Points the marks at a newly focused control. Called by every kit control's OnSelect.</summary>
        public static void Follow(Selectable control)
        {
            if (_instance != null && control != null)
                _instance.SetTarget((RectTransform)control.transform);
        }

        /// <summary>Navigation hit the end of a list: the marks nudge that way and settle back.</summary>
        public static void Bump(MoveDirection direction)
        {
            if (_instance == null || _instance._tokens == null || UiMotion.Reduced)
                return;
            float d = _instance._tokens.EdgeBump;
            switch (direction)
            {
                case MoveDirection.Left: _instance._bumpX.Value -= d; break;
                case MoveDirection.Right: _instance._bumpX.Value += d; break;
                case MoveDirection.Up: _instance._bumpY.Value += d; break;
                case MoveDirection.Down: _instance._bumpY.Value -= d; break;
            }

            UiMotion.Wake(_instance);
        }

        /// <summary>
        /// Wakes the marks after a screen puts focus back by code (selecting the object that was already selected
        /// raises no select event, so nothing else would).
        /// </summary>
        public static void WakeUp()
        {
            if (_instance != null)
                UiMotion.Wake(_instance);
        }

        /// <summary>Keeps the marks drawn above every screen (a screen moves itself to the front when it opens).</summary>
        public static void BringToFront()
        {
            if (_instance != null)
                _instance.transform.SetAsLastSibling();
        }

        void Awake()
        {
            UiMotion.EnsureSubCanvas(gameObject);
            _parent = (RectTransform)_frame.parent;
            _bracketHome = new Vector2[_brackets.Length];
            for (int i = 0; i < _brackets.Length; i++)
                _bracketHome[i] = _brackets[i] != null ? _brackets[i].anchoredPosition : Vector2.zero;
        }

        void OnEnable()
        {
            _instance = this;
            _group.alpha = 0f;
            _shown.Snap(0f);
            _placed = false;
            UiMotion.Wake(this);
        }

        void OnDisable()
        {
            UiMotion.Sleep(this);
            if (_instance == this)
                _instance = null;
        }

        void SetTarget(RectTransform target)
        {
            _target = target;
            UiMotion.Wake(this);
        }

        /// <summary>
        /// Ticked while it has a target or is fading: it tracks a target that may itself be sliding with its panel.
        /// With no target and faded out it sleeps; the next selection wakes it. No allocation.
        /// </summary>
        public bool Tick(float dt)
        {
            if (_tokens == null)
                return false;

            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool valid = _target != null && selected == _target.gameObject && _target.gameObject.activeInHierarchy;
            bool visible = valid && UiInput.UsingKeysOrPad;
            if (visible != _wantVisible)
            {
                _wantVisible = visible;
                _shown.Start(_shown.Value, visible ? 1f : 0f, visible ? _tokens.HoverIn : _tokens.HoverOut, visible ? _tokens.EaseOut : _tokens.EaseIn);
            }

            _shown.Step(dt);
            _group.alpha = _shown.Value;
            if (valid && (_wantVisible || _shown.Value > 0.001f))
                Track(dt);
            return visible || _shown.Running;
        }

        void Track(float dt)
        {
            _target.GetWorldCorners(_corners);
            Vector2 min = _parent.InverseTransformPoint(_corners[0]);
            Vector2 max = _parent.InverseTransformPoint(_corners[2]);
            float gap = _tokens.FocusRingGap;
            float x = (min.x + max.x) * 0.5f, y = (min.y + max.y) * 0.5f;
            float w = max.x - min.x + gap * 2f, h = max.y - min.y + gap * 2f;

            if (!_placed || UiMotion.Reduced || _shown.Value <= 0.01f)
            {
                _x.Snap(x); _y.Snap(y); _w.Snap(w); _h.Snap(h);
                _placed = true;
            }
            else
            {
                _x.Target = x; _y.Target = y; _w.Target = w; _h.Target = h;
                _x.Step(dt, _tokens.FocusTravel);
                _y.Step(dt, _tokens.FocusTravel);
                _w.Step(dt, _tokens.FocusTravel);
                _h.Step(dt, _tokens.FocusTravel);
            }

            _bumpX.Step(dt, _tokens.FocusTravel);
            _bumpY.Step(dt, _tokens.FocusTravel);
            _frame.anchoredPosition = new Vector2(_x.Value + _bumpX.Value, _y.Value + _bumpY.Value);
            _frame.sizeDelta = new Vector2(_w.Value, _h.Value);
            Breathe();
        }

        void Breathe()
        {
            float out_ = 0f;
            if (!UiMotion.Reduced && UiMotion.IdleSeconds > _tokens.BreatheDelay)
            {
                float t = (UiMotion.IdleSeconds - _tokens.BreatheDelay) / Mathf.Max(0.1f, _tokens.BreathePeriod);
                out_ = _tokens.BreatheDistance * 0.5f * (1f - Mathf.Cos(t * 2f * Mathf.PI));
            }

            for (int i = 0; i < _brackets.Length; i++)
            {
                if (_brackets[i] == null)
                    continue;
                float sx = (i & 1) == 0 ? -1f : 1f;
                float sy = i < 2 ? 1f : -1f;
                _brackets[i].anchoredPosition = _bracketHome[i] + new Vector2(sx * out_, sy * out_);
            }

            if (_chevron != null)
                _chevron.anchoredPosition = new Vector2(-_tokens.SpaceM - out_, 0f);
        }
    }
}
