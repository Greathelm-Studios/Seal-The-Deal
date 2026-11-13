using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// What a character rig needs to animate a player, on every machine: the owner, the host
    /// and everyone watching. Read it from <see cref="PlayerMotor.AnimState"/> once a frame and
    /// feed an Animator; nothing here is ever written back. It is worked out locally from the
    /// body state the host already sends, so it costs no bandwidth and no bones are streamed.
    /// </summary>
    public readonly struct PlayerAnimState
    {
        /// <summary>Speed across the floor, metres per second.</summary>
        public readonly float PlanarSpeed;
        /// <summary>Velocity relative to where the body faces, metres per second: x = to its right, y = forward. Drives a strafe blend.</summary>
        public readonly Vector2 LocalMove;
        public readonly bool Grounded;
        /// <summary>Metres per second, up positive. Rising or falling.</summary>
        public readonly float VerticalSpeed;
        public readonly bool Crouched;
        /// <summary>0 standing to 1 fully crouched, for blending the duck.</summary>
        public readonly float Crouch;
        /// <summary>-1 full lean left to +1 full lean right.</summary>
        public readonly float Lean;
        public readonly bool Mantling;
        /// <summary>Which climb is playing while <see cref="Mantling"/> is true.</summary>
        public readonly MantleKind MantleKind;
        public readonly bool Sprinting;
        /// <summary>Sliding along the floor in a crouch.</summary>
        public readonly bool Sliding;
        /// <summary>Thrown by a blast and not in control.</summary>
        public readonly bool Knocked;

        public PlayerAnimState(Vector3 velocity, Quaternion facing, MoveState state, bool grounded, float crouch, float lean, MantleKind mantleKind, bool sliding, float sprintThreshold)
        {
            Vector3 local = Quaternion.Inverse(facing) * new Vector3(velocity.x, 0f, velocity.z);
            LocalMove = new Vector2(local.x, local.z);
            PlanarSpeed = LocalMove.magnitude;
            VerticalSpeed = velocity.y;
            // From the move state, not the floor probe: on a jump or hop tick the feet still
            // touch while the body is already rising, and a rig must never see that as standing.
            Grounded = state == MoveState.Grounded || (state == MoveState.Knocked && grounded);
            Crouch = crouch;
            Crouched = crouch > 0.5f;
            Lean = lean;
            Mantling = state == MoveState.Mantling;
            MantleKind = mantleKind;
            Knocked = state == MoveState.Knocked;
            Sliding = sliding && state == MoveState.Grounded;
            Sprinting = state == MoveState.Grounded && !Crouched && PlanarSpeed > sprintThreshold;
        }
    }
}
