using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.UI
{
    /// <summary>Tabbed page switcher with animated underline rule and Q/E or shoulder shortcuts.</summary>
    [AddComponentMenu("Close the Deal/UI/Tab Pages")]
    public sealed class UiTabPages : MonoBehaviour, IUiAnimated
    {
        [Tooltip("The kit's tokens.")]
        [SerializeField] UiTokens _tokens;
        [Tooltip("The tab buttons, left to right.")]
        [SerializeField] UiButton[] _tabs = Array.Empty<UiButton>();
        [Tooltip("The pages, same order as the tabs. Each needs a Canvas Group.")]
        [SerializeField] UiSettingsPage[] _pages = Array.Empty<UiSettingsPage>();
        [Tooltip("The cobalt rule that travels under the open tab.")]
        [SerializeField] RectTransform _rule;

        const float Slide = 12f;

        Action[] _handlers;
        CanvasGroup[] _groups;
        int _current = -1;
        int _leaving = -1;
        int _direction;
        UiTween _in, _out;
        UiSpring _ruleX, _ruleW;
        bool _placed;

        /// <summary>Raised after the open tab changes.</summary>
        public event Action<int> Changed;

        public int Current => Mathf.Max(0, _current);

        public UiSettingsPage CurrentPage => _pages.Length > 0 ? _pages[Current] : null;

        public UiButton CurrentTab => _tabs.Length > 0 ? _tabs[Current] : null;

        public int Count => _pages.Length;

        public UiButton TabAt(int index) => _tabs[Mathf.Clamp(index, 0, _tabs.Length - 1)];

        void Awake()
        {
            _groups = new CanvasGroup[_pages.Length];
            for (int i = 0; i < _pages.Length; i++)
                _groups[i] = _pages[i].GetComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            _handlers ??= new Action[_tabs.Length];
            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                _handlers[i] ??= () => Select(index, true);
                _tabs[i].Clicked += _handlers[i];
            }

            if (_current < 0)
                Select(0, false);
            else
                UiMotion.Wake(this); // Reopened: the open page ticks again (the resolution countdown, the meter).
        }

        void OnDisable()
        {
            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].Clicked -= _handlers[i];
            UiMotion.Sleep(this);
        }

        /// <summary>Q / E and the shoulder buttons. Called by the owning screen while it is on top.</summary>
        public void ReadShortcuts()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            int step = 0;
            if ((keyboard != null && keyboard.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame))
                step = -1;
            else if ((keyboard != null && keyboard.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame))
                step = 1;
            if (step == 0)
                return;

            int next = Current + step;
            if (next < 0 || next >= _pages.Length)
            {
                UiFocusFrame.Bump(step < 0 ? UnityEngine.EventSystems.MoveDirection.Left : UnityEngine.EventSystems.MoveDirection.Right);
                return;
            }

            Select(next, true);
        }

        /// <summary>Opens a tab. With sound and motion when the player chose it; instantly on first show.</summary>
        public void Select(int index, bool animate)
        {
            index = Mathf.Clamp(index, 0, _pages.Length - 1);
            if (index == _current)
                return;

            int previous = _current;
            _direction = previous < 0 ? 0 : (index > previous ? 1 : -1);
            _current = index;
            for (int i = 0; i < _tabs.Length; i++)
                _tabs[i].Fx.Active = i == index;

            if (previous >= 0)
            {
                _pages[previous].Hidden();
                _leaving = previous;
                // Focus on the leaving page would be left on nothing, and focus on the old tab would point at the
                // wrong tab: either way it moves to the new tab.
                GameObject selected = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
                if (selected != null && (selected.transform.IsChildOf(_pages[previous].transform) || selected == _tabs[previous].gameObject))
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(_tabs[index].gameObject);
            }

            UiSettingsPage page = _pages[index];
            page.gameObject.SetActive(true);
            page.ShowSaved();

            bool motion = animate && _tokens != null;
            if (motion)
            {
                _out.Start(1f, 0f, _tokens.PanelOut * 0.8f, _tokens.EaseIn);
                _in.Start(0f, 1f, _tokens.PanelIn * 0.8f, _tokens.EaseOut, _tokens.PanelOut * 0.4f);
                UiSound.Play(UiSoundEvent.TabChange);
            }
            else
            {
                _out.Snap(0f);
                _in.Snap(1f);
                _placed = false;
            }

            for (int i = 0; i < _pages.Length; i++)
            {
                if (i != index && i != _leaving)
                    _pages[i].gameObject.SetActive(false);
            }

            Apply();
            UiMotion.Wake(this);
            Changed?.Invoke(index);
        }

        public bool Tick(float dt)
        {
            _in.Step(dt);
            _out.Step(dt);
            if (_rule != null && _tokens != null)
                StepRule(dt);
            Apply();
            if (!_out.Running && _leaving >= 0 && _leaving != _current)
            {
                _pages[_leaving].gameObject.SetActive(false);
                _leaving = -1;
            }

            if (_current >= 0)
                _pages[_current].Tick(dt);
            return true; // Ticked while the screen shows: the open page may have something live (a meter, a countdown).
        }

        bool StepRule(float dt)
        {
            var tab = (RectTransform)_tabs[Current].transform;
            float x = tab.anchoredPosition.x, w = tab.rect.width;
            if (!_placed || UiMotion.Reduced)
            {
                _ruleX.Snap(x);
                _ruleW.Snap(w);
                _placed = true;
            }
            else
            {
                _ruleX.Target = x;
                _ruleW.Target = w;
                _ruleX.Step(dt, _tokens.PressRelease);
                _ruleW.Step(dt, _tokens.PressRelease);
            }

            _rule.anchoredPosition = new Vector2(_ruleX.Value, _rule.anchoredPosition.y);
            _rule.sizeDelta = new Vector2(_ruleW.Value, _rule.sizeDelta.y);
            return !_ruleX.Settled || !_ruleW.Settled;
        }

        void Apply()
        {
            if (_current < 0)
                return;
            bool reduced = UiMotion.Reduced;
            Place(_current, _in.Value, reduced ? 0f : (1f - _in.Value) * Slide * _direction);
            if (_leaving >= 0 && _leaving != _current)
                Place(_leaving, _out.Value, reduced ? 0f : -(1f - _out.Value) * Slide * _direction);
        }

        void Place(int index, float alpha, float offset)
        {
            CanvasGroup group = _groups != null && index < _groups.Length ? _groups[index] : null;
            if (group != null)
            {
                group.alpha = alpha;
                group.blocksRaycasts = alpha > 0.5f;
            }

            var rect = (RectTransform)_pages[index].transform;
            rect.anchoredPosition = new Vector2(offset, rect.anchoredPosition.y);
        }
    }
}
