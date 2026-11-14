using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// A kit slider with a number field: uGUI's Slider (drag, click on the track, left / right steps) with the
    /// kit's look, focus answers and a tick per step whose pitch follows the value. Always whole steps.
    /// </summary>
    [AddComponentMenu("Close the Deal/UI/Slider")]
    public sealed class UiSlider : Slider
    {
        [Tooltip("The control's look and motion parts. Filled by the kit builder.")]
        [SerializeField] UiControlFx _fx = new();
        [Tooltip("The number field beside the track.")]
        [SerializeField] TMP_Text _field;
        [Tooltip("One line telling the player what this does, shown while it has focus.")]
        [TextArea(1, 3)] [SerializeField] string _description;
        [Tooltip("Each step is worth this much in the number field (10 = steps of 10).")]
        [SerializeField] float _displayScale = 1f;
        [Tooltip("Written after the number, e.g. %.")]
        [SerializeField] string _suffix = string.Empty;

        bool _pointerInside;
        bool _selected;
        int _shownStep = int.MinValue;

        public UiControlFx Fx => _fx;

        /// <summary>The value as the player reads it (step x display scale), e.g. 100 for 100%.</summary>
        public int DisplayValue => Mathf.RoundToInt(value * _displayScale);

        /// <summary>Raised with the display value when the player moves it (not when code sets it).</summary>
        public event Action<int> Changed;

        public string Description
        {
            get => _description;
            set => _description = value;
        }

        public void Configure(float displayScale, string suffix)
        {
            _displayScale = displayScale;
            _suffix = suffix;
            _shownStep = int.MinValue;
            DrawField();
        }

        public void SetDisplayWithoutNotify(int display)
        {
            SetValueWithoutNotify(display / Mathf.Max(0.0001f, _displayScale));
            DrawField();
        }

        protected override void Awake()
        {
            base.Awake();
            wholeNumbers = true;
            transition = Transition.None;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            onValueChanged.AddListener(OnMoved);
            _pointerInside = false;
            _selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            _fx.SetState(IsInteractable(), false, false, _selected, true);
            DrawField();
        }

        protected override void OnDisable()
        {
            onValueChanged.RemoveListener(OnMoved);
            _fx.Sleep();
            base.OnDisable();
        }

        void OnMoved(float v)
        {
            DrawField();
            UiSound.PlaySlider(normalizedValue);
            Changed?.Invoke(DisplayValue);
        }

        void DrawField()
        {
            int step = Mathf.RoundToInt(value);
            if (_field == null || step == _shownStep)
                return;
            _shownStep = step;
            _field.SetText("{0}" + _suffix, DisplayValue);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (_fx == null)
                return;
            _fx.SetState(state != SelectionState.Disabled, _pointerInside, state == SelectionState.Pressed, _selected, instant || !Application.isPlaying);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            base.OnPointerEnter(eventData);
            UiControlEvents.Hover(_fx.Tokens, IsInteractable());
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            base.OnPointerExit(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && IsInteractable())
                UiSound.Play(UiSoundEvent.Select);
            base.OnPointerDown(eventData);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            base.OnSelect(eventData);
            UiControlEvents.FocusSound(eventData);
            UiControlEvents.RaiseFocused(this, _description);
            UiFocusFrame.Follow(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            base.OnDeselect(eventData);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (UiControlEvents.SwallowFirstMove())
                return;
            bool horizontal = eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right;
            if (horizontal && IsInteractable())
            {
                float before = value;
                base.OnMove(eventData);
                if (Mathf.Approximately(before, value))
                    UiFocusFrame.Bump(eventData.moveDir);
                return;
            }

            UiControlEvents.Move(this, eventData);
        }

        /// <summary>For the capture tool: draw this control as if hovered / pressed / focused, without input.</summary>
        public void ShowState(bool hovered, bool pressed, bool focused)
        {
            _pointerInside = hovered;
            _fx.SetState(IsInteractable(), hovered, pressed, focused, false);
        }
    }
}
