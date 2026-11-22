using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>One shot's kick of the gun in the hand, as the weapon authored it. Degrees, metres and seconds.</summary>
    public readonly struct GunKick
    {
        /// <summary>Muzzle flip up, in degrees.</summary>
        public readonly float Flip;
        /// <summary>Push back toward the eye, in metres.</summary>
        public readonly float PushBack;
        /// <summary>Roll, in degrees, always the same way.</summary>
        public readonly float Roll;
        /// <summary>Sideways twist, in degrees, already signed for the side the view snapped to.</summary>
        public readonly float Twist;
        /// <summary>How long the gun takes to settle, in seconds.</summary>
        public readonly float Seconds;
        /// <summary>Settle shape from 1 (the shot) to 0 (rest); a dip below 0 overshoots. Null = a plain ease-out.</summary>
        public readonly AnimationCurve Curve;

        public GunKick(float flip, float pushBack, float roll, float twist, float seconds, AnimationCurve curve)
        {
            Flip = flip;
            PushBack = pushBack;
            Roll = roll;
            Twist = twist;
            Seconds = seconds;
            Curve = curve;
        }

        /// <summary>The same kick at a fraction of its size (a dry-fire click, say).</summary>
        public GunKick Scaled(float fraction)
        {
            return new GunKick(Flip * fraction, PushBack * fraction, Roll * fraction, Twist * fraction, Seconds, Curve);
        }
    }
}
