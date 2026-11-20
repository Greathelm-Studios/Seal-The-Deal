using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>An on / off switch: a square check box and the word On or Off. Click, Enter or the pad's A flips it.</summary>
    [AddComponentMenu("Seal the Deal/UI/Toggle")]
    public sealed class UiToggle : UiControl, IPointerClickHandler, ISubmitHandler
    {
        [Tooltip("The filled square inside the box, shown while on.")]
        [SerializeField] Graphic _check;
        [Tooltip("The On / Off word beside the box.")]
        [SerializeField] TMP_Text _state;

        bool _on;

        /// <summary>Raised when the player flips it (not when code sets it).</summary>
        public event Action<bool> Changed;

        public bool IsOn => _on;

        /// <summary>Sets the value without a sound or an event: for showing a saved setting.</summary>
        public void SetWithoutNotify(bool on)
        {
            _on = on;
            Draw();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable())
                Flip();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive())
                return;
            if (!IsInteractable())
            {
                Refuse();
                return;
            }

            _fx.Stamp();
            Flip();
        }

        void Flip()
        {
            _on = !_on;
            Draw();
            _fx.Confirm();
            UiSound.Play(_on ? UiSoundEvent.ToggleOn : UiSoundEvent.ToggleOff);
            Changed?.Invoke(_on);
        }

        void Draw()
        {
            if (_check != null)
                _check.enabled = _on;
            if (_state != null)
                _state.text = _on ? "On" : "Off";
        }
    }
}
