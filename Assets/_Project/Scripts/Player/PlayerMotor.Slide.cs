using FishNet.Managing.Timing;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Sliding. Crouch while moving fast on the floor (out of a sprint, a hop or a fall) and
    /// the body drops into a slide instead of a crouch: a kick of speed, almost no grip, a
    /// pull from any downhill under it, and only air-like steering. It ends in a crouch when
    /// it runs out of speed, in a stand when crouch is let go, or in a jump that keeps the
    /// speed it had.
    /// </summary>
    public sealed partial class PlayerMotor
    {
        /// <summary>True while sliding this tick, with <paramref name="planar"/> moved on by the slide.</summary>
        bool SlideStep(MoveInput input, ref Vector3 planar, float dt)
        {
            float speed = planar.magnitude;
            if (!_slide.Active && input.Crouch && speed >= _profile.SlideMinSpeed)
                planar = StartSlide(planar, speed);
            else if (_slide.Active && (!input.Crouch || speed < _profile.SlideEndSpeed))
                _slide.Active = false;

            if (!_slide.Active)
                return false;

            // The floor's tilt, as a level push toward downhill.
            Vector3 normal = _ground.Normal;
            Vector3 downhill = new Vector3(normal.x, 0f, normal.z) * (-Physics.gravity.y * normal.y);

            planar *= Mathf.Max(0f, 1f - _profile.SlideFriction * dt);
            planar += downhill * (_profile.SlideSlopeGain * dt);
            planar = Accelerate(planar, WishDirection(input), _profile.AirStrafeSpeed, _profile.AirAcceleration, dt);
            return true;
        }

        Vector3 StartSlide(Vector3 planar, float speed)
        {
            _slide.Active = true;
            if (_slide.BoostCooldownTicks > 0)
                return planar;

            _slide.BoostCooldownTicks = (ushort)System.Math.Min(TimeManager.TimeToTicks(_profile.SlideBoostCooldown, TickRounding.RoundUp), ushort.MaxValue);
            return planar * ((speed + _profile.SlideBoost) / speed);
        }
    }
}
