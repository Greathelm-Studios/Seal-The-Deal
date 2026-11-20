using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// A kit button: primary, secondary, quiet or tab (set on its look). Press stamps down on button down with
    /// the Select sound; letting go inside confirms with the Confirm sound and the cyan tick, then runs Clicked.
    /// Enter / the pad's A stamp and confirm at once. A greyed-out button refuses with its reason.
    /// </summary>
    [AddComponentMenu("Seal the Deal/UI/Button")]
    public sealed class UiButton : UiControl, IPointerClickHandler, ISubmitHandler
    {
        [Tooltip("What happens when the player confirms this button. Code usually subscribes to Clicked instead.")]
        [SerializeField] UnityEvent _onClick = new();

        [Tooltip("The sound this button's confirm makes. Back buttons use Back; everything else Confirm.")]
        [SerializeField] UiSoundEvent _confirmSound = UiSoundEvent.Confirm;

        /// <summary>Runs when the button is confirmed (click, Enter, the pad's A).</summary>
        public event Action Clicked;

        public UiSoundEvent ConfirmSound
        {
            get => _confirmSound;
            set => _confirmSound = value;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable())
                return;
            Activate();
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
            Activate();
        }

        /// <summary>Confirms the button as if the player had clicked it.</summary>
        public void Activate()
        {
            _fx.Confirm();
            UiSound.Play(_confirmSound);
            Clicked?.Invoke();
            _onClick.Invoke();
        }
    }
}
