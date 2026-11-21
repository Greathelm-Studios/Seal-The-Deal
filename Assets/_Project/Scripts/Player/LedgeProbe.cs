using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>How a climb ends: standing on top of something deep, or carried over something thin.</summary>
    public enum MantleKind : byte
    {
        Climb = 0,
        Vault = 1
    }

    /// <summary>Why a climb the player asked for did not happen.</summary>
    public enum MantleRefusal : byte
    {
        None = 0,
        /// <summary>There is something to climb, but its top is above the reach.</summary>
        TooHigh = 1,
        /// <summary>The top is in reach, but there is no space for a body above or on it.</summary>
        NoRoom = 2
    }

    /// <summary>What the body is asking about: where it stands, which way it wants to go and how far it can reach.</summary>
    public struct LedgeQuery
    {
        public Vector3 Feet;
        /// <summary>Unit, level direction the player is pushing.</summary>
        public Vector3 Direction;
        public float Radius;
        public float BodyHeight;
        public float MinHeight;
        public float MaxHeight;
        public float Reach;
        public LayerMask Mask;
    }

    public struct LedgeResult
    {
        public bool Found;
        public MantleRefusal Refusal;
        public MantleKind Kind;
        /// <summary>Where the feet end up: on top if it is deep enough to stand on, else just past its near edge at top height.</summary>
        public Vector3 Target;
    }

    /// <summary>
    /// Finds something ahead of a body that can be climbed or vaulted, from static physics
    /// queries only, so the owner's prediction and the host reach the same answer.
    /// </summary>
    public static class LedgeProbe
    {
        const float Inset = 0.05f;
        const float EdgeInset = 0.1f;
        const float SlopeLimit = 50f;
        const float GlancingDot = 0.5f;
        const float SameSurface = 0.15f;

        public static LedgeResult Find(in LedgeQuery q)
        {
            if (!FindFace(q, out float faceDistance))
                return default;

            // Straight down onto the top, just inside the near face. A ray that starts inside
            // something solid sees nothing, which is exactly "taller than the reach".
            Vector3 edge = q.Feet + q.Direction * (faceDistance + EdgeInset);
            Vector3 above = new(edge.x, q.Feet.y + q.MaxHeight + Inset, edge.z);
            if (!Physics.Raycast(above, Vector3.down, out RaycastHit top, q.MaxHeight - q.MinHeight + Inset, q.Mask, QueryTriggerInteraction.Ignore))
                return Refuse(MantleRefusal.TooHigh);

            if (Vector3.Angle(top.normal, Vector3.up) > SlopeLimit)
                return Refuse(MantleRefusal.NoRoom);

            float topY = top.point.y;
            Vector3 risen = new(q.Feet.x, topY + Inset, q.Feet.z);
            Vector3 over = edge + q.Direction * (q.Radius + Inset);
            over.y = topY;
            if (BodyBlocked(risen, q) || BodyBlocked(over + Vector3.up * Inset, q))
                return Refuse(MantleRefusal.NoRoom);

            bool deep = Physics.Raycast(over + Vector3.up * SameSurface, Vector3.down, out RaycastHit under, SameSurface * 2f, q.Mask, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(under.point.y - topY) <= SameSurface;

            return new LedgeResult { Found = true, Kind = deep ? MantleKind.Climb : MantleKind.Vault, Target = over };
        }

        static bool FindFace(in LedgeQuery q, out float faceDistance)
        {
            faceDistance = 0f;
            float castRadius = q.Radius - Inset;
            Vector3 low = q.Feet + Vector3.up * (q.MinHeight + castRadius);
            Vector3 high = q.Feet + Vector3.up * Mathf.Max(q.BodyHeight - q.Radius, q.MinHeight + castRadius);
            if (!Physics.CapsuleCast(low, high, castRadius, q.Direction, out RaycastHit face, q.Reach, q.Mask, QueryTriggerInteraction.Ignore))
                return false;

            // Running along a wall is not running at it. A hit on a level surface has no side to judge by.
            Vector3 into = new(-face.normal.x, 0f, -face.normal.z);
            if (into.sqrMagnitude > 0.01f && Vector3.Dot(into.normalized, q.Direction) < GlancingDot)
                return false;

            faceDistance = face.distance + castRadius;
            return true;
        }

        static bool BodyBlocked(Vector3 feet, in LedgeQuery q)
        {
            float castRadius = q.Radius - Inset;
            Vector3 low = feet + Vector3.up * q.Radius;
            Vector3 high = feet + Vector3.up * Mathf.Max(q.BodyHeight - q.Radius, q.Radius);
            return Physics.CheckCapsule(low, high, castRadius, q.Mask, QueryTriggerInteraction.Ignore);
        }

        static LedgeResult Refuse(MantleRefusal why) => new() { Refusal = why };
    }
}
