using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>A control whose click zones answer the mouse passing over them (a stepper's arrows grow under it).</summary>
    public interface IUiZoneHover
    {
        void HoverZone(int value, bool inside);
    }

    /// <summary>
    /// Picks one of a few named choices with bold arrows either side. Left / right steps (it wraps), up / down moves
    /// focus on. Clicking an arrow steps that way. The arrow grows under the mouse and kicks the way the value moved.
    /// </summary>
    [AddComponentMenu("Seal the Deal/UI/Stepper")]
    public sealed class UiStepper : UiControl, IUiStepTarget, IUiZoneHover, IUiAnimated
    {
        [Tooltip("The name of the current choice, between the arrows.")]
        [SerializeField] TMP_Text _value;
        [Tooltip("The left arrow. It kicks left when the choice steps back and grows when the mouse is over it.")]
        [SerializeField] RectTransform _previousArrow;
        [Tooltip("The right arrow. It kicks right when the choice steps on and grows when the mouse is over it.")]
        [SerializeField] RectTransform _nextArrow;
        [Tooltip("How far an arrow kicks when the choice steps, in UI units (1080p pixels). 0 = no kick.")]
        [Min(0f)] [SerializeField] float _kick = 12f;
        [Tooltip("How big an arrow grows while the mouse is over it. 1 = no growth.")]
        [Range(1f, 1.5f)] [SerializeField] float _hoverGrow = 1.2f;
        [Tooltip("How big an arrow pops as it kicks, before settling back. 1 = no pop.")]
        [Range(1f, 1.6f)] [SerializeField] float _kickPop = 1.35f;

        string[] _options = Array.Empty<string>();
        int _index;
        UiSpring _kickPrevious, _kickNext;
        UiSpring _growPrevious = new(1f), _growNext = new(1f);

        /// <summary>Raised with the new choice's index when the player steps.</summary>
        public event Action<int> Changed;

        public int Index => _index;

        public int OptionCount => _options.Length;

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
            Kick(direction);
            UiSound.PlaySlider((float)_index / (_options.Length - 1));   // a value control: the tick, pitched by the option
            Changed?.Invoke(_index);
        }

        public void StepFromClick(int value) => Step(value);

        public void HoverZone(int value, bool inside)
        {
            if (inside && !IsInteractable())
                return;
            float target = inside && !UiMotion.Reduced ? _hoverGrow : 1f;
            if (value < 0)
                _growPrevious.Target = target;
            else
                _growNext.Target = target;
            Animate();
        }

        void Kick(int direction)
        {
            if (UiMotion.Reduced || _kick <= 0f)
                return;
            if (direction < 0)
            {
                _kickPrevious.Snap(-_kick);
                _growPrevious.Value = Mathf.Max(_growPrevious.Value, _kickPop);
            }
            else
            {
                _kickNext.Snap(_kick);
                _growNext.Value = Mathf.Max(_growNext.Value, _kickPop);
            }

            _kickPrevious.Target = _kickNext.Target = 0f;
            Animate();
        }

        void Animate()
        {
            if (Application.isPlaying && _fx.Tokens != null)
                UiMotion.Wake(this);
        }

        public bool Tick(float dt)
        {
            UiSpringTuning tuning = _fx.Tokens.PressRelease;
            bool moving = _kickPrevious.Step(dt, tuning);
            moving |= _kickNext.Step(dt, tuning);
            moving |= _growPrevious.Step(dt, tuning);
            moving |= _growNext.Step(dt, tuning);
            Pose(_previousArrow, _kickPrevious.Value, _growPrevious.Value);
            Pose(_nextArrow, _kickNext.Value, _growNext.Value);
            return moving;
        }

        static void Pose(RectTransform arrow, float x, float scale)
        {
            if (arrow == null)
                return;
            arrow.anchoredPosition = new Vector2(x, 0f);
            arrow.localScale = new Vector3(scale, scale, 1f);
        }

        protected override void OnDisable()
        {
            UiMotion.Sleep(this);
            _kickPrevious.Snap(0f);
            _kickNext.Snap(0f);
            _growPrevious.Snap(1f);
            _growNext.Snap(1f);
            if (Application.isPlaying)
            {
                Pose(_previousArrow, 0f, 1f);
                Pose(_nextArrow, 0f, 1f);
            }

            base.OnDisable();
        }
    }
}
