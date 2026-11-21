using System.Reflection;
using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>Applies carry slowdown via a runtime copy of MovementProfile without mutating the authored asset.</summary>
    // WORKAROUND (AGENTS.md §8): swaps PlayerMotor's private _profile field via reflection until a public override method exists.
    public sealed class MotorSpeedOverride
    {
        const string ProfileFieldName = "_profile";

        static readonly FieldInfo ProfileField = typeof(PlayerMotor).GetField(ProfileFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        static bool _warned;

        readonly PlayerMotor _motor;
        MovementProfile _authored;
        MovementProfile _slowed;

        public MotorSpeedOverride(PlayerMotor motor)
        {
            _motor = motor;
        }

        /// <summary>Fractions of the authored speed and jump height, 0–1. Both at 1 is the same as Clear.</summary>
        public void Set(float speedFraction, float jumpFraction)
        {
            if (speedFraction >= 1f && jumpFraction >= 1f)
            {
                Clear();
                return;
            }

            if (!Available())
                return;

            if (_slowed == null)
            {
                _authored = _motor.Profile;
                _slowed = Object.Instantiate(_authored);
                _slowed.name = _authored.name + " (carrying)";
            }

            _slowed.WalkSpeed = _authored.WalkSpeed * speedFraction;
            _slowed.SprintSpeed = _authored.SprintSpeed * speedFraction;
            _slowed.JumpHeight = _authored.JumpHeight * jumpFraction;
            ProfileField.SetValue(_motor, _slowed);
        }

        /// <summary>Back to the authored profile.</summary>
        public void Clear()
        {
            if (_slowed != null && _motor != null && ProfileField != null)
                ProfileField.SetValue(_motor, _authored);
        }

        public void Dispose()
        {
            Clear();
            if (_slowed != null)
                Object.Destroy(_slowed);
            _slowed = null;
        }

        static bool Available()
        {
            if (ProfileField != null)
                return true;

            if (!_warned)
            {
                _warned = true;
                Debug.LogError("[Items] PlayerMotor no longer has the field '" + ProfileFieldName + "', so carrying heavy things cannot slow players. Give PlayerMotor a public profile override and call it from MotorSpeedOverride.");
            }

            return false;
        }
    }
}
