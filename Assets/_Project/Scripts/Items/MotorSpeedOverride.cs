using System.Reflection;
using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// Slows one player's walking, sprinting and jumping while they carry something heavy.
    /// The designer's movement profile is never written (AGENTS.md §4): this keeps a runtime
    /// copy with the slower numbers and hands the motor that copy while the load lasts, then
    /// the authored profile back. Because the motor itself reads the slower numbers, the
    /// owner's prediction and the host agree on them.
    ///
    /// WORKAROUND (AGENTS.md §8): PlayerMotor has no public way to take a different profile,
    /// and this lane may not edit it, so the copy is swapped in through its private field.
    /// The real fix is one public method on PlayerMotor that accepts a profile override;
    /// when that exists, Set and Clear call it and the reflection goes. Listed in ITEMS.md.
    /// </summary>
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
