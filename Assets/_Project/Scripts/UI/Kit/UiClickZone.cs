using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Something a click zone can drive: a stepper's arrows step it, a swatch picks its index.</summary>
    public interface IUiStepTarget
    {
        void StepFromClick(int value);
    }

    /// <summary>Non-focusable click zone (stepper arrows, swatches) that forwards clicks to its parent UiControl.</summary>
    [AddComponentMenu("Close the Deal/UI/Click Zone")]
    public sealed class UiClickZone : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        [Tooltip("The control this zone belongs to.")]
        [SerializeField] UiControl _control;
        [Tooltip("For an arrow: -1 left, +1 right. For a swatch: its index in the row.")]
        [SerializeField] int _value;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_control != null && EventSystem.current != null && _control.IsInteractable())
                EventSystem.current.SetSelectedGameObject(_control.gameObject, eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && _control is IUiStepTarget target)
                target.StepFromClick(_value);
        }
    }
}
