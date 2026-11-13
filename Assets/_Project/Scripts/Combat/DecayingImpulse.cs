using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// A kick that comes back by itself along an authored curve. Each new kick adds to what is
    /// left of the last one and starts the curve again from the total, so fast shots compound.
    /// The curve runs from 1 at the moment of the kick to 0 at its end; dipping below 0 on the
    /// way gives the overshoot past rest that makes recoil read as physical. The peak is held
    /// for the frame of the kick, so the attack looks the same at any frame rate.
    /// </summary>
    public struct DecayingImpulse
    {
        float _peak;
        float _elapsed;
        float _duration;
        bool _fresh;

        public bool IsActive => _duration > 0f;

        /// <summary>Adds a kick of this size, to come back over this many seconds.</summary>
        public void Add(float amount, float seconds, AnimationCurve curve)
        {
            _peak = Evaluate(curve) + amount;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, seconds);
            _fresh = true;
        }

        /// <summary>Moves time on and returns the kick left now.</summary>
        public float Step(float dt, AnimationCurve curve)
        {
            if (!IsActive)
                return 0f;

            if (_fresh)
                _fresh = false;
            else
                _elapsed += dt;

            if (_elapsed >= _duration)
            {
                Clear();
                return 0f;
            }

            return Evaluate(curve);
        }

        public void Clear()
        {
            _peak = 0f;
            _elapsed = 0f;
            _duration = 0f;
            _fresh = false;
        }

        float Evaluate(AnimationCurve curve)
        {
            if (!IsActive)
                return 0f;

            float t = _elapsed / _duration;
            return _peak * (curve != null && curve.length > 0 ? curve.Evaluate(t) : 1f - t);
        }

        /// <summary>The house recovery shape: holds the peak briefly, drops fast, swings a little past rest and settles.</summary>
        public static AnimationCurve DefaultCurve(float overshoot)
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f),
                new Keyframe(0.12f, 0.85f),
                new Keyframe(0.45f, 0.1f),
                new Keyframe(0.72f, -overshoot, 0f, 0f),
                new Keyframe(1f, 0f, 0f, 0f));
        }
    }
}
