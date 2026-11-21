using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Kind of impact driving a body's hit reaction.</summary>
    public enum HitType : byte
    {
        Bullet = 0,
        ThrownProp = 1,
        Blast = 2,
        Melee = 3
    }

    /// <summary>Value-type description of an impact (world point or blast centre, unit direction, impulse in N·s, radius, kind).</summary>
    public struct HitInfo
    {
        public Vector3 Point;
        public Vector3 Direction;
        public float Impulse;
        /// <summary>Blast radius in metres (0 for a single-part point hit).</summary>
        public float Radius;
        public HitType Type;
    }
}
