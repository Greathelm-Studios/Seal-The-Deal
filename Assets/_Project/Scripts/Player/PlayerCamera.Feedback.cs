using CloseTheDeal.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// How the view answers movement, local only: one spring the movement events kick (take-off,
    /// landing by fall speed, slide, climb, sprint start, hard stop) moving the camera along its
    /// own axes, never turning it, so aim is untouched; plus gamepad rumble on landing and slide.
    /// Reduce motion (Accessibility) turns the kicks, the slide roll and the field-of-view changes
    /// off. Tuning: the View feel and Gamepad rumble values on MovementProfile.
    /// </summary>
    public sealed partial class PlayerCamera
    {
        Vector3 _kick;
        Vector3 _kickVelocity;
        float _lastSpeed;
        float _hardStopCooldown;
        float _rumbleLeft;
        Gamepad _rumblePad;

        const float HardStopBelow = 1.5f;
        const float HardStopDeceleration = 20f;
        const float HardStopCooldownSeconds = 0.5f;

        /// <summary>The movement kick on the view this frame, in metres along the camera's own axes. The viewmodel may scale it.</summary>
        public Vector3 FeedbackOffset => _kick;

        /// <summary>The slide roll on the view this frame, in degrees.</summary>
        public float FeedbackRoll => _slideRoll;

        static bool Reduced => UiSettings.ReducedMotion;

        void BindFeedback(PlayerMotor motor)
        {
            motor.Landed += OnLanded;
            motor.Jumped += OnJumped;
            motor.SlideChanged += OnSlideChanged;
            motor.MantleStarted += OnMantleStarted;
            motor.SprintChanged += OnSprintChanged;
            _kick = Vector3.zero;
            _kickVelocity = Vector3.zero;
            _lastSpeed = 0f;
        }

        void UnbindFeedback(PlayerMotor motor)
        {
            motor.Landed -= OnLanded;
            motor.Jumped -= OnJumped;
            motor.SlideChanged -= OnSlideChanged;
            motor.MantleStarted -= OnMantleStarted;
            motor.SprintChanged -= OnSprintChanged;
            StopRumble();
        }

        void Kick(Vector3 velocity)
        {
            if (!Reduced)
                _kickVelocity += velocity;
        }

        void OnJumped() => Kick(Vector3.down * _motor.Profile.JumpKick);

        void OnLanded(float fallSpeed)
        {
            MovementProfile p = _motor.Profile;
            Kick(Vector3.down * Mathf.Min(fallSpeed * p.LandKickPerSpeed, p.LandKickMax));
            Rumble(Mathf.Min(fallSpeed * p.RumbleLandPerSpeed, p.RumbleLandMax));
        }

        void OnSlideChanged(bool sliding)
        {
            if (!sliding)
                return;
            Kick(Vector3.down * _motor.Profile.SlideKick);
            Rumble(_motor.Profile.RumbleSlide);
        }

        void OnMantleStarted(MantleKind kind) => Kick(Vector3.down * _motor.Profile.MantleKick);

        void OnSprintChanged(bool sprinting)
        {
            if (sprinting)
                Kick(Vector3.back * _motor.Profile.SprintStartKick);
        }

        /// <summary>Advances the spring (and spots a hard stop); returns the offset to add along the camera's axes.</summary>
        Vector3 StepFeedback(MovementProfile p, float dt)
        {
            float speed = _motor.AnimState.PlanarSpeed;
            _hardStopCooldown -= dt;
            if (dt > 0f && _lastSpeed >= p.HardStopSpeed && speed < HardStopBelow && (_lastSpeed - speed) / dt > HardStopDeceleration && _hardStopCooldown <= 0f && _motor.AnimState.Grounded)
            {
                Kick(Vector3.forward * p.HardStopKick);
                _hardStopCooldown = HardStopCooldownSeconds;
            }
            _lastSpeed = speed;

            StepRumble(dt);
            if (Reduced)
            {
                _kick = Vector3.zero;
                _kickVelocity = Vector3.zero;
                return _kick;
            }

            // A damped spring: stiffness from the frequency, damping as a fraction of critical.
            float omega = 2f * Mathf.PI * Mathf.Max(p.CameraSpringFrequency, 0.1f);
            Vector3 acceleration = -omega * omega * _kick - 2f * p.CameraSpringDamping * omega * _kickVelocity;
            _kickVelocity += acceleration * dt;
            _kick += _kickVelocity * dt;
            return _kick;
        }

        void Rumble(float strength)
        {
            if (strength <= 0f || _input == null || !_input.LastUsedGamepad || Gamepad.current == null)
                return;

            _rumblePad = Gamepad.current;
            _rumblePad.SetMotorSpeeds(strength * 0.6f, strength);
            _rumbleLeft = _motor.Profile.RumbleSeconds;
        }

        void StepRumble(float dt)
        {
            if (_rumbleLeft <= 0f)
                return;
            _rumbleLeft -= dt;
            if (_rumbleLeft <= 0f)
                StopRumble();
        }

        void StopRumble()
        {
            _rumbleLeft = 0f;
            if (_rumblePad != null)
                _rumblePad.SetMotorSpeeds(0f, 0f);
            _rumblePad = null;
        }

        void OnDisable() => StopRumble();
    }
}
