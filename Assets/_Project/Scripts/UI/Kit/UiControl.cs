using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Shared answers every kit control gives: sounds, focus events, the refusal and the edge bump.</summary>
    public static class UiControlEvents
    {
        /// <summary>A control got focus (keys, pad or a click). Screens show its description.</summary>
        public static event Action<Selectable, string> Focused;

        /// <summary>A control that can't be used right now was pressed. Screens show its reason in the status line.</summary>
        public static event Action<Selectable, string> Refused;

        public static void RaiseFocused(Selectable control, string description) => Focused?.Invoke(control, description);

        public static void RaiseRefused(Selectable control, string reason) => Refused?.Invoke(control, reason);

        /// <summary>The hover answer: a quiet tick, when the tokens allow it (the feel table marks it a proposal).</summary>
        public static void Hover(UiTokens tokens, bool interactable)
        {
            if (interactable && tokens != null && tokens.HoverSound)
                UiSound.PlayHover();
        }

        /// <summary>Focus moved by a key or the pad (never when code moved it, never on a mouse click).</summary>
        public static void FocusSound(BaseEventData eventData)
        {
            if (eventData is AxisEventData)
                UiSound.Play(UiSoundEvent.Focus);
        }

        /// <summary>Moves focus along axis input, skipping non-interactable controls or bumping at list ends.</summary>
        public static void Move(Selectable from, AxisEventData eventData)
        {
            Selectable next = Next(from, eventData.moveDir);
            for (int hops = 0; next != null && !next.IsInteractable() && hops < 8; hops++)
                next = Next(next, eventData.moveDir);

            if (next != null && next.IsInteractable() && next.gameObject.activeInHierarchy)
                eventData.selectedObject = next.gameObject;
            else
                UiFocusFrame.Bump(eventData.moveDir);
        }

        static Selectable Next(Selectable from, MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Up: return from.FindSelectableOnUp();
                case MoveDirection.Down: return from.FindSelectableOnDown();
                case MoveDirection.Left: return from.FindSelectableOnLeft();
                case MoveDirection.Right: return from.FindSelectableOnRight();
                default: return null;
            }
        }

        /// <summary>The first arrow press after using the mouse only shows where focus is; it does not move it.</summary>
        public static bool SwallowFirstMove() => UiInput.JustSwitchedToKeysOrPad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Focused = null;
            Refused = null;
        }
    }

    /// <summary>Base Selectable for kit controls, driving shared visuals and audio via UiControlFx.</summary>
    public abstract class UiControl : Selectable
    {
        [Tooltip("The control's look and motion parts. Filled by the kit builder.")]
        [SerializeField] protected UiControlFx _fx = new();

        [Tooltip("One line telling the player what this does, shown while it has focus.")]
        [TextArea(1, 3)] [SerializeField] string _description;

        [Tooltip("Why it can't be used right now, shown when a player presses it while it is greyed out. Code may replace it.")]
        [SerializeField] string _refusal;

        bool _pointerInside;
        bool _selected;

        public UiControlFx Fx => _fx;

        public string Description
        {
            get => _description;
            set => _description = value;
        }

        public string RefusalReason
        {
            get => _refusal;
            set => _refusal = value;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // Selectables run in edit mode too. Posing the visuals there would be saved into the prefab or scene, and
            // the brackets take their home from where they sit, so every editor save pushed them out further.
            if (!Application.isPlaying)
                return;
            _pointerInside = false;
            _selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            _fx.SetState(IsInteractable(), false, false, _selected, true);
        }

        protected override void OnDisable()
        {
            _fx.Sleep();
            base.OnDisable();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (_fx == null || !Application.isPlaying)
                return;   // edit mode: leave the serialized layout alone (see OnEnable)
            _fx.SetState(state != SelectionState.Disabled, _pointerInside, state == SelectionState.Pressed, _selected, instant);
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
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            if (!IsInteractable())
            {
                Refuse();
                return;
            }

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
            if (HandleMove(eventData))
                return;
            UiControlEvents.Move(this, eventData);
        }

        /// <summary>Left / right on a value control changes the value instead of moving focus. Return true when handled.</summary>
        protected virtual bool HandleMove(AxisEventData eventData) => false;

        /// <summary>A press on a control that can't be used: amber plate, shake, the refusal sound and the reason in words.</summary>
        public void Refuse()
        {
            _fx.Refuse();
            UiSound.Play(UiSoundEvent.Error);
            UiControlEvents.RaiseRefused(this, _refusal);
        }

        /// <summary>For the capture tool: draw this control as if hovered / pressed / focused, without input.</summary>
        public void ShowState(bool hovered, bool pressed, bool focused)
        {
            _pointerInside = hovered;
            _fx.SetState(IsInteractable(), hovered, pressed, focused, false);
        }
    }
}
