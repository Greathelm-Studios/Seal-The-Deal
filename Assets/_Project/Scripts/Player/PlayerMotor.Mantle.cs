using FishNet.Managing.Timing;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Climbing and vaulting. A climb starts two ways: jumping into something climbable while
    /// holding a direction, or pressing jump on the floor in front of it. The body rises to
    /// the top, moves across, and leaves with part of the speed it came in with: standing on a
    /// deep top (a desk, a floor edge), or carried over a thin one (the window sill).
    /// </summary>
    public sealed partial class PlayerMotor
    {
        /// <param name="pressed">The player pressed jump for this, so a refusal is worth telling them about and standing still aims where they face.</param>
        bool TryStartMantle(MoveInput input, bool pressed)
        {
            Vector3 direction;
            if (input.Move.sqrMagnitude >= 0.25f)
                direction = WishDirection(input).normalized;
            else if (pressed)
                direction = Quaternion.Euler(0f, input.Yaw, 0f) * Vector3.forward;
            else
                return false;

            LedgeResult ledge = LedgeProbe.Find(new LedgeQuery
            {
                Feet = _rigidbody.position,
                Direction = direction,
                Radius = _capsule.radius,
                BodyHeight = _capsule.height,
                MinHeight = _profile.MinLedgeHeight,
                MaxHeight = _profile.MaxLedgeHeight,
                Reach = _profile.LedgeReach,
                Mask = _groundMask
            });

            if (!ledge.Found)
            {
                if (pressed && ledge.Refusal != MantleRefusal.None)
                {
                    _counters.Refusals++;
                    _counters.LastRefusal = ledge.Refusal;
                }
                return false;
            }

            float speedIn = Planar(_rigidbody.linearVelocity).magnitude * _profile.MantleSpeedKeep;
            // A vault has no floor at the far end to stop on; it must come out fast enough to clear.
            float speedOut = ledge.Kind == MantleKind.Vault ? Mathf.Max(speedIn, _profile.WalkSpeed) : speedIn;

            Enter(MoveState.Mantling);
            _mantle = new MantleData { Target = ledge.Target, Exit = direction * speedOut, Phase = MantleRise, Kind = ledge.Kind };
            _stateTicksLeft = TimeManager.TimeToTicks(_profile.MantleDuration * MantleTimeoutMultiplier, TickRounding.RoundUp);
            _counters.Mantles++;
            // Grabbing a ledge from the air is not a landing, and neither is arriving on top.
            _counters.InAir = false;
            _body.Velocity(Vector3.zero);
            return true;
        }

        void MantleStep(float dt)
        {
            // Something got in the way (a prop slid onto the ledge): let go rather than hang there.
            if (_stateTicksLeft == 0)
            {
                Enter(MoveState.Airborne);
                return;
            }

            _stateTicksLeft--;

            // Fixed pace: the tallest climb takes the profile duration, shorter ones less.
            float pace = (_profile.MaxLedgeHeight + _profile.LedgeReach + _capsule.radius) / _profile.MantleDuration;
            float cancelGravity = -Physics.gravity.y * dt;
            Vector3 position = _rigidbody.position;

            if (_mantle.Phase == MantleRise)
            {
                float rise = _mantle.Target.y + ProbeInset - position.y;
                if (rise > ArriveDistance)
                {
                    _body.Velocity(Vector3.up * (Mathf.Min(pace, rise / dt) + cancelGravity));
                    return;
                }

                _mantle.Phase = MantleStepOnto;
            }

            Vector3 across = Planar(_mantle.Target - position);
            float remaining = across.magnitude;
            if (remaining <= ArriveDistance)
            {
                FinishMantle();
                return;
            }

            // Never slower across the top than the player ran in: a climb must not feel like a stop.
            Vector3 step = across / remaining * Mathf.Min(Mathf.Max(pace, _mantle.Exit.magnitude), remaining / dt);
            _body.Velocity(new Vector3(step.x, cancelGravity, step.z));
        }

        /// <summary>On top of something deep the player is standing and can run on; past something thin they are in the air beyond it.</summary>
        void FinishMantle()
        {
            MantleData done = _mantle;
            Enter(done.Kind == MantleKind.Climb ? MoveState.Grounded : MoveState.Airborne);
            _body.Velocity(done.Exit);
        }
    }
}
