using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Picks one of a few named choices with ‹ and ›. Left / right steps (it wraps), up / down moves focus on.
    /// Clicking an arrow steps that way.
    /// </summary>
    [AddComponentMenu("Close the Deal/UI/Stepper")]
    public sealed class UiStepper : UiControl, IUiStepTarget
    {
        [Tooltip("The name of the current choice, between the arrows.")]
        [SerializeField] TMP_Text _value;

        string[] _options = Array.Empty<string>();
        int _index;

        /// <summary>Raised with the new choice's index when the player steps.</summary>
        public event Action<int> Changed;

        public int Index => _index;

        public void SetOptions(string[] options, int index)
        {
            _options = options ?? Array.Empty<string>();
            SetWithoutNotify(index);
        }

        public void SetWithoutNotify(int index)
        {
            _index = _options.Length == 0 ? 0 : Mathf.Clamp(index, 0, _options.Length - 1);
            if (_value != null)
                _value.text = _options.Length == 0 ? string.Empty : _options[_index];
        }

        protected override bool HandleMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left)
                Step(-1);
            else if (eventData.moveDir == MoveDirection.Right)
                Step(1);
            else
                return false;
            return true;
        }

        public void Step(int direction)
        {
            if (!IsInteractable())
            {
                Refuse();
                return;
            }

            if (_options.Length < 2)
                return;
            int next = (_index + direction + _options.Length) % _options.Length;
            SetWithoutNotify(next);
            _fx.Stamp();
            UiSound.PlaySlider((float)_index / (_options.Length - 1));   // a value control: the tick, pitched by the option
            Changed?.Invoke(_index);
        }

        public void StepFromClick(int value) => Step(value);
    }
}
