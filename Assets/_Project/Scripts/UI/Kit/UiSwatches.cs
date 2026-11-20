using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// A row of colour swatches; the chosen one carries a navy ring and its name is written beside the row.
    /// Left / right picks the next swatch (no wrap: the end of the row bumps); clicking a swatch picks it.
    /// </summary>
    [AddComponentMenu("Seal the Deal/UI/Swatches")]
    public sealed class UiSwatches : UiControl, IUiStepTarget
    {
        [Tooltip("The swatch squares, left to right.")]
        [SerializeField] Image[] _swatches = Array.Empty<Image>();
        [Tooltip("The ring drawn around each swatch while it is the chosen one, same order.")]
        [SerializeField] Image[] _rings = Array.Empty<Image>();
        [Tooltip("The chosen colour's name, beside the row.")]
        [SerializeField] TMP_Text _name;

        string[] _names = Array.Empty<string>();
        int _index;

        public event Action<int> Changed;

        public int Index => _index;

        public void SetSwatches(Color[] colours, string[] names, int index)
        {
            _names = names ?? Array.Empty<string>();
            for (int i = 0; i < _swatches.Length; i++)
            {
                if (_swatches[i] != null && colours != null && i < colours.Length)
                    _swatches[i].color = colours[i];
            }

            SetWithoutNotify(index);
        }

        public void SetWithoutNotify(int index)
        {
            _index = Mathf.Clamp(index, 0, Mathf.Max(0, _swatches.Length - 1));
            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i] != null)
                    _rings[i].enabled = i == _index;
            }

            if (_name != null)
                _name.text = _index < _names.Length ? _names[_index] : string.Empty;
        }

        protected override bool HandleMove(AxisEventData eventData)
        {
            int direction = eventData.moveDir == MoveDirection.Left ? -1 : eventData.moveDir == MoveDirection.Right ? 1 : 0;
            if (direction == 0)
                return false;
            int next = _index + direction;
            if (next < 0 || next >= _swatches.Length)
            {
                UiFocusFrame.Bump(eventData.moveDir);
                return true;
            }

            Pick(next);
            return true;
        }

        public void Pick(int index)
        {
            if (!IsInteractable())
            {
                Refuse();
                return;
            }

            if (index == _index)
                return;
            SetWithoutNotify(index);
            _fx.Stamp();
            UiSound.PlaySlider(_swatches.Length > 1 ? (float)_index / (_swatches.Length - 1) : 0.5f);   // a value step: the pitched tick
            Changed?.Invoke(_index);
        }

        public void StepFromClick(int value) => Pick(value);
    }
}
