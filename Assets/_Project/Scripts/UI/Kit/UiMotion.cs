using System.Collections.Generic;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>Something the motion driver ticks while it is moving. Return false once settled to stop being ticked.</summary>
    public interface IUiAnimated
    {
        /// <summary>Advance by dt seconds of unscaled time. False when there is nothing left to animate.</summary>
        bool Tick(float dt);
    }

    /// <summary>
    /// A critically damped (or, for the refusal shake, underdamped) spring on one value. Interruptible:
    /// change Target mid-flight and it keeps its velocity. Feel table §2.2: k = (2π/T)², c = 4πζ/T, mass 1.
    /// </summary>
    public struct UiSpring
    {
        public float Value;
        public float Velocity;
        public float Target;

        const float SettleDistance = 0.01f;
        const float SettleSpeed = 0.05f;
        const float MaxStep = 1f / 240f;

        public UiSpring(float value)
        {
            Value = value;
            Target = value;
            Velocity = 0f;
        }

        public bool Settled => Mathf.Abs(Value - Target) < SettleDistance && Mathf.Abs(Velocity) < SettleSpeed;

        public void Snap(float value)
        {
            Value = value;
            Target = value;
            Velocity = 0f;
        }

        /// <summary>Steps the spring. Returns true while it is still moving. Sub-steps keep it stable at low frame rates.</summary>
        public bool Step(float dt, UiSpringTuning tuning)
        {
            if (Settled)
            {
                Value = Target;
                Velocity = 0f;
                return false;
            }

            float response = Mathf.Max(0.01f, tuning.Response);
            float stiffness = (2f * Mathf.PI / response) * (2f * Mathf.PI / response);
            float damping = 4f * Mathf.PI * tuning.Damping / response;
            while (dt > 0f)
            {
                float h = Mathf.Min(dt, MaxStep);
                float accel = -stiffness * (Value - Target) - damping * Velocity;
                Velocity += accel * h;
                Value += Velocity * h;
                dt -= h;
            }

            return true;
        }
    }

    /// <summary>A timed move from one value to another along a cubic-bezier easing curve.</summary>
    public struct UiTween
    {
        public float From;
        public float To;
        public float Elapsed;
        public float Duration;
        public float Delay;
        public Vector4 Ease;

        public bool Running => Elapsed < Delay + Duration;

        public void Start(float from, float to, float duration, Vector4 ease, float delay = 0f)
        {
            From = from;
            To = to;
            Duration = duration;
            Delay = delay;
            Ease = ease;
            Elapsed = 0f;
        }

        public void Snap(float value)
        {
            From = value;
            To = value;
            Duration = 0f;
            Delay = 0f;
            Elapsed = 0f;
        }

        /// <summary>Advances and returns the current value.</summary>
        public float Step(float dt)
        {
            Elapsed += dt;
            return Value;
        }

        public float Value
        {
            get
            {
                if (Duration <= 0f)
                    return To;
                float t = Mathf.Clamp01((Elapsed - Delay) / Duration);
                return Mathf.LerpUnclamped(From, To, UiMotion.Bezier(Ease, t));
            }
        }
    }

    /// <summary>
    /// The one motion driver every kit component uses. Components register while they move and are ticked
    /// on unscaled time (menus run while the game runs), then drop out when settled. No allocation per frame.
    /// A capture tool can pause real time and step it by hand (Paused + Tick), so motion can be filmed frame by frame.
    /// </summary>
    public static class UiMotion
    {
        static readonly List<IUiAnimated> Active = new(64);
        static readonly HashSet<IUiAnimated> Members = new();
        static UiMotionDriver _driver;
        static bool _ticking;

        /// <summary>Seconds of unscaled time the driver has run. Idle life (breathing, pulses) reads this.</summary>
        public static float Now { get; private set; }

        /// <summary>Seconds since the player last touched a key, the mouse or a pad.</summary>
        public static float IdleSeconds => Now - LastInputTime;

        public static float LastInputTime { get; private set; }

        /// <summary>When set, real time stops driving motion and only Tick moves it. For the capture tool.</summary>
        public static bool Paused { get; set; }

        /// <summary>Reduce motion: every slide and zoom becomes a fade, the shake and idle life stop. Read from the saved setting.</summary>
        public static bool Reduced => UiSettings.ReducedMotion;

        /// <summary>Starts ticking a component until its Tick returns false. Safe to call every time something changes.</summary>
        public static void Wake(IUiAnimated item)
        {
            if (!Members.Add(item))
                return;
            Active.Add(item);
            EnsureDriver();
        }

        /// <summary>Stops ticking a component (it is being disabled). During a tick it is dropped when the tick ends.</summary>
        public static void Sleep(IUiAnimated item)
        {
            if (!Members.Remove(item) || _ticking)
                return;
            Active.Remove(item);
        }

        /// <summary>
        /// Isolates an animated UI subtree on its own sub-Canvas (without adding a GraphicRaycaster)
        /// so moving or fading it does not rebuild the parent HUD canvas.
        /// </summary>
        public static Canvas EnsureSubCanvas(GameObject go)
        {
            if (go == null)
                return null;
            if (!go.TryGetComponent(out Canvas canvas))
            {
                canvas = go.AddComponent<Canvas>();
                canvas.overrideSorting = false;
            }

            return canvas;
        }

        /// <summary>Marks that the player did something, which stops idle life until they stop again.</summary>
        public static void NoteInput() => LastInputTime = Now;

        /// <summary>
        /// Advances every moving component by dt seconds. A component may wake or sleep others (or itself) while
        /// ticking, so a finished one is found again by identity before it is removed.
        /// </summary>
        public static void Tick(float dt)
        {
            Now += dt;
            _ticking = true;
            int count = Active.Count; // Anything woken during this tick starts next frame.
            for (int i = 0; i < count; i++)
            {
                IUiAnimated item = Active[i];
                if (!Members.Contains(item))
                    continue; // Put to sleep earlier in this tick.
                bool destroyed = item is Object unityObject && unityObject == null;
                if (destroyed || !item.Tick(dt))
                    Members.Remove(item);
            }

            _ticking = false;
            Compact();
        }

        /// <summary>Drops everything no longer a member, keeping order. No allocation.</summary>
        static void Compact()
        {
            int write = 0;
            for (int read = 0; read < Active.Count; read++)
            {
                IUiAnimated item = Active[read];
                if (Members.Contains(item))
                    Active[write++] = item;
            }

            Active.RemoveRange(write, Active.Count - write);
        }

        static void EnsureDriver()
        {
            if (_driver != null || !Application.isPlaying)
                return;
            var go = new GameObject("UiMotionDriver") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<UiMotionDriver>();
        }

        /// <summary>CSS-style cubic-bezier easing: control points (x1, y1, x2, y2), t in 0..1.</summary>
        public static float Bezier(Vector4 c, float t)
        {
            if (t <= 0f)
                return 0f;
            if (t >= 1f)
                return 1f;

            // Solve x(u) = t for u by Newton steps, then return y(u).
            float u = t;
            for (int i = 0; i < 6; i++)
            {
                float x = CubicAt(c.x, c.z, u) - t;
                float dx = CubicSlope(c.x, c.z, u);
                if (Mathf.Abs(x) < 1e-4f)
                    break;
                if (Mathf.Abs(dx) < 1e-5f)
                    break;
                u = Mathf.Clamp01(u - x / dx);
            }

            return CubicAt(c.y, c.w, u);
        }

        static float CubicAt(float p1, float p2, float u)
        {
            float inv = 1f - u;
            return 3f * inv * inv * u * p1 + 3f * inv * u * u * p2 + u * u * u;
        }

        static float CubicSlope(float p1, float p2, float u)
        {
            float inv = 1f - u;
            return 3f * inv * inv * p1 + 6f * inv * u * (p2 - p1) + 3f * u * u * (1f - p2);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active.Clear();
            Members.Clear();
            _driver = null;
            _ticking = false;
            Now = 0f;
            LastInputTime = 0f;
            Paused = false;
        }
    }
}
