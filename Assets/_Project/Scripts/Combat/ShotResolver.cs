using CloseTheDeal.Glass;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>Where one shot went. Body is set when it stopped on a player or dummy, Solid when it stopped on the world.</summary>
    public struct ShotResult
    {
        /// <summary>Where the shot stopped, or the end of its range.</summary>
        public Vector3 End;
        /// <summary>Metres from the muzzle to End.</summary>
        public float Distance;
        /// <summary>It hit a body or something solid, rather than running out of range.</summary>
        public bool Stopped;
        public Health Body;
        public Collider Solid;
        /// <summary>It stopped on a body above that body's head height.</summary>
        public bool Precision;
        /// <summary>How many panes of glass it crossed on the way. ShotResolver.BreakCrossedPanes breaks them.</summary>
        public int PanesCrossed;
    }

    /// <summary>
    /// Works out where a shot goes. Glass never stops it (GDD): the ray carries on through
    /// every pane and remembers them. Bodies are tested as capsules by maths rather than by
    /// their colliders, so the host can test them where they stood a moment ago, which is
    /// what the shooter was looking at. The host's answer is the only one that deals damage;
    /// a client runs the same trace only to draw its own tracer straight away.
    /// </summary>
    public static class ShotResolver
    {
        public const int MaxPanesPerShot = 8;

        /// <summary>Ticks either side of the shooter's seen tick at which a target still counts as hit (measured: a client's picture is one to two ticks from the tick it reports).</summary>
        const int RewindSlackTicks = 1;

        /// <summary>Step past a pane's surface before looking further, in metres. Under any pane's thickness.</summary>
        const float PaneSkin = 0.002f;
        const float TwoPi = Mathf.PI * 2f;

        static readonly GlassPane[] Panes = new GlassPane[MaxPanesPerShot];

        /// <summary>
        /// Traces one shot. solidMask is the world (never the Player layer). With rewind on,
        /// bodies are tested where they stood on rewindTick; only the host has that history.
        /// </summary>
        public static ShotResult Trace(Vector3 origin, Vector3 direction, float range, int solidMask, Health shooter, bool rewind, uint rewindTick, float headFromHeight)
        {
            var result = new ShotResult();
            float limit = NearestBody(origin, direction, range, shooter, rewind, rewindTick, out Health body, out float bodyFeetY);

            float travelled = 0f;
            bool clear = false;
            for (int i = 0; i <= MaxPanesPerShot && travelled < limit; i++)
            {
                Vector3 from = origin + direction * travelled;
                if (!Physics.Raycast(from, direction, out RaycastHit hit, limit - travelled, solidMask, QueryTriggerInteraction.Ignore))
                {
                    clear = true;
                    break;
                }

                if (!GlassPane.TryFind(hit.collider, out GlassPane pane))
                {
                    result.Solid = hit.collider;
                    result.Stopped = true;
                    result.Distance = travelled + hit.distance;
                    result.End = hit.point;
                    return result;
                }

                Remember(pane, ref result);
                travelled += hit.distance + PaneSkin;
            }

            // More glass than one shot can cross: it stops in the glass rather than flying on unchecked.
            if (!clear && travelled < limit)
            {
                result.Stopped = true;
                result.Distance = travelled;
                result.End = origin + direction * travelled;
                return result;
            }

            result.Body = body;
            result.Stopped = body != null;
            result.Distance = limit;
            result.End = origin + direction * limit;
            result.Precision = body != null && result.End.y - bodyFeetY >= headFromHeight;
            return result;
        }

        /// <summary>Whether nothing solid but glass stands between two points.</summary>
        public static bool ClearPath(Vector3 from, Vector3 to, int solidMask)
        {
            Vector3 line = to - from;
            float length = line.magnitude;
            if (length < 0.001f)
                return true;

            Vector3 direction = line / length;
            float travelled = 0f;
            for (int i = 0; i <= MaxPanesPerShot && travelled < length; i++)
            {
                if (!Physics.Raycast(from + direction * travelled, direction, out RaycastHit hit, length - travelled, solidMask, QueryTriggerInteraction.Ignore))
                    return true;
                if (!GlassPane.TryFind(hit.collider, out _))
                    return false;

                travelled += hit.distance + PaneSkin;
            }

            // Out of glass budget before the end: not proven clear.
            return travelled >= length;
        }

        /// <summary>Host only. Breaks the panes the last traced shot crossed. Call straight after Trace.</summary>
        public static void BreakCrossedPanes(in ShotResult result, Vector3 direction, int attackerClientId)
        {
            for (int i = 0; i < result.PanesCrossed; i++)
            {
                GlassPane pane = Panes[i];
                Panes[i] = null;
                if (pane != null)
                    pane.ApplyDamage(0f, pane.Centre, direction, attackerClientId);
            }
        }

        /// <summary>
        /// A shot's real direction: the aim, strayed by up to spreadDegrees. The stray comes
        /// from the seed alone, so the shooter's game and the host's get the same answer.
        /// </summary>
        public static Vector3 SpreadDirection(Vector3 aim, float spreadDegrees, uint seed)
        {
            if (spreadDegrees <= 0f)
                return aim;

            float off = spreadDegrees * Mathf.Sqrt(Unit(Hash(seed)));
            float roll = 360f * Unit(Hash(seed ^ 0x9e3779b9u));
            Quaternion stray = Quaternion.AngleAxis(roll, Vector3.forward) * Quaternion.AngleAxis(off, Vector3.right);
            return Quaternion.LookRotation(aim) * (stray * Vector3.forward);
        }

        /// <summary>A unit direction as two 16-bit angles. Both ends fire along the unpacked value, so rounding never splits them.</summary>
        public static uint PackDirection(Vector3 direction)
        {
            direction.Normalize();
            float yaw = Mathf.Atan2(direction.x, direction.z);
            float pitch = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f));
            uint y = (uint)Mathf.RoundToInt((yaw + Mathf.PI) / TwoPi * 65535f);
            uint p = (uint)Mathf.RoundToInt((pitch + Mathf.PI * 0.5f) / Mathf.PI * 65535f);
            return (y << 16) | (p & 0xFFFFu);
        }

        public static Vector3 UnpackDirection(uint packed)
        {
            float yaw = (packed >> 16) / 65535f * TwoPi - Mathf.PI;
            float pitch = (packed & 0xFFFFu) / 65535f * Mathf.PI - Mathf.PI * 0.5f;
            float flat = Mathf.Cos(pitch);
            return new Vector3(Mathf.Sin(yaw) * flat, Mathf.Sin(pitch), Mathf.Cos(yaw) * flat);
        }

        static void Remember(GlassPane pane, ref ShotResult result)
        {
            // A pane whose collider is a hollow mesh is met twice, front and back.
            bool repeat = result.PanesCrossed > 0 && Panes[result.PanesCrossed - 1] == pane;
            if (!repeat && result.PanesCrossed < MaxPanesPerShot)
                Panes[result.PanesCrossed++] = pane;
        }

        /// <summary>Distance to the first living body on the ray, or range when there is none.</summary>
        static float NearestBody(Vector3 origin, Vector3 direction, float range, Health shooter, bool rewind, uint rewindTick, out Health body, out float bodyFeetY)
        {
            body = null;
            bodyFeetY = 0f;
            float nearest = range;

            for (int i = 0; i < Health.All.Count; i++)
            {
                Health candidate = Health.All[i];
                if (candidate == shooter || candidate.IsDead)
                    continue;

                if (!rewind)
                {
                    TestBody(candidate, candidate.transform.position, origin, direction, ref nearest, ref body, ref bodyFeetY);
                    continue;
                }

                // Favour the shooter by one tick either side of the tick they saw: the picture on
                // their screen sits between ticks, and is never known closer than that.
                for (int offset = -RewindSlackTicks; offset <= RewindSlackTicks; offset++)
                {
                    uint tick = rewindTick + (uint)offset;
                    if (candidate.WasAliveAt(tick))
                        TestBody(candidate, candidate.PositionAt(tick), origin, direction, ref nearest, ref body, ref bodyFeetY);
                }
            }

            return nearest;
        }

        static void TestBody(Health candidate, Vector3 feet, Vector3 origin, Vector3 direction, ref float nearest, ref Health body, ref float bodyFeetY)
        {
            candidate.CapsuleAt(feet, out Vector3 bottom, out Vector3 top, out float radius);
            if (RayCapsule(origin, direction, nearest, bottom, top, radius, out float distance))
            {
                nearest = distance;
                body = candidate;
                bodyFeetY = feet.y;
            }
        }

        /// <summary>Does the ray pass within radius of the segment, inside maxDistance? distance is roughly where it enters.</summary>
        static bool RayCapsule(Vector3 origin, Vector3 direction, float maxDistance, Vector3 bottom, Vector3 top, float radius, out float distance)
        {
            Vector3 axis = top - bottom;
            Vector3 toOrigin = origin - bottom;
            float axisSqr = Vector3.Dot(axis, axis);
            float along = Vector3.Dot(direction, toOrigin);

            float onRay;
            float onAxis = 0f;
            if (axisSqr < 0.0001f)
            {
                onRay = Mathf.Clamp(-along, 0f, maxDistance);
            }
            else
            {
                float axisDotOrigin = Vector3.Dot(axis, toOrigin);
                float cross = Vector3.Dot(direction, axis);
                float denominator = axisSqr - cross * cross;
                onRay = denominator > 0.0001f ? Mathf.Clamp((cross * axisDotOrigin - along * axisSqr) / denominator, 0f, maxDistance) : 0f;
                onAxis = Mathf.Clamp01((cross * onRay + axisDotOrigin) / axisSqr);
                // The axis point may have been clamped to an end; find the ray point nearest that end.
                onRay = Mathf.Clamp(Vector3.Dot(bottom + axis * onAxis - origin, direction), 0f, maxDistance);
            }

            Vector3 gap = origin + direction * onRay - (bottom + axis * onAxis);
            float gapSqr = gap.sqrMagnitude;
            if (gapSqr > radius * radius)
            {
                distance = maxDistance;
                return false;
            }

            distance = Mathf.Max(0f, onRay - Mathf.Sqrt(radius * radius - gapSqr));
            return true;
        }

        static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }

        static float Unit(uint hash) => (hash & 0xFFFFFFu) / 16777216f;
    }
}
