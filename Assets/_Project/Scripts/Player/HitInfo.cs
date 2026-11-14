// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "a test dummy in the scene to test animations of being hit by certain
//              powers of weapons (bullets bombs explosions props, etc)"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The one description of a hit that a body reacts to, so the test bench today and
//              real weapons later feed the same entry point. Its own file because it is the
//              contract between the weapons and the ragdoll and belongs to neither. It sits
//              under Player/ only until the weapons lane says where in Combat/ it should live.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>What kind of thing struck a body. Reactions may differ by kind as well as by strength.</summary>
    public enum HitType : byte
    {
        /// <summary>A shot: a small, sharp push at one point.</summary>
        Bullet = 0,
        /// <summary>A thrown or flying object (a chair, a desk): a heavy push at one point.</summary>
        ThrownProp = 1,
        /// <summary>An explosion: a push on the whole body, away from the blast's centre.</summary>
        Blast = 2
    }

    /// <summary>
    /// One hit on one body: where, which way, how hard and what kind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The body that was hit is not in here; the hit is handed to it. A plain struct of values,
    /// so it can be passed by <c>in</c> without allocating and, later, sent over the network as
    /// it is.
    /// </para>
    /// <para>
    /// <b>Ownership (AGENTS.md §3):</b> for real players the host decides that a hit happened
    /// and what it was; each client then plays the reaction on its own ragdoll. Today only the
    /// local test dummy receives hits, so nothing is synced.
    /// </para>
    /// <para>
    /// WHY an impulse and not damage or a speed: the ragdoll's parts have very different
    /// weights, and an impulse is the one number that throws a forearm far and a chest a
    /// little, as a real hit does. Alternative considered: a velocity change per hit, as
    /// <c>PlayerMotor.ApplyKnockback</c> takes; it suits one capsule but would fling a light
    /// limb and a heavy torso equally.
    /// </para>
    /// </remarks>
    /// <example>
    /// A pistol shot that struck a dummy's chest from the front:
    /// <code>
    /// dummy.Hit(new HitInfo { Point = hitPoint, Direction = shotDirection, Impulse = 20f, Type = HitType.Bullet });
    /// </code>
    /// </example>
    public struct HitInfo
    {
        /// <summary>Where it struck, world space, metres. For a blast, the blast's centre.</summary>
        public Vector3 Point;

        /// <summary>The way the hit was travelling, world space, unit length. For a blast it is only used for a body sitting exactly on the centre.</summary>
        public Vector3 Direction;

        /// <summary>How hard, in newton-seconds delivered to this body (after any falloff the weapon applies). A pistol shot is about 20, a thrown desk about 300, the middle of a blast about 600.</summary>
        public float Impulse;

        /// <summary>A blast's reach, metres. 0 = not a blast: the hit lands on the one part nearest <see cref="Point"/>. Above 0, the impulse is spread over every part within this distance of <see cref="Point"/>, nearer parts taking more.</summary>
        public float Radius;

        /// <summary>What kind of thing it was.</summary>
        public HitType Type;
    }
}
