// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "a test dummy in the scene to test animations of being hit by certain
//              powers of weapons (bullets bombs explosions props, etc)"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Marks an object as the hit-test dummy and gives weapons and the test bench one
//              place to hit it. Its own component so a shot's raycast can find "something that
//              takes hits" on the dummy's root without knowing about the ragdoll underneath.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// A stand-in body for judging hit reactions: the player's character, standing in the idle
    /// on the same animator and active ragdoll, with no motor, no input and no network identity.
    /// </summary>
    /// <remarks>
    /// Local and unsynced: a dummy placed in a networked game reacts only on the screen of the
    /// client that hit it (AGENTS.md §3). It is a development aid, not a gameplay object. Built
    /// by <c>Close the Deal &gt; Dev &gt; Build Hit Dummy Prefab</c>.
    /// </remarks>
    public sealed class HitDummy : MonoBehaviour
    {
        [Tooltip("The dummy's ragdoll, which shows the reaction. Set by the tool that builds the prefab.")]
        [SerializeField] PlayerRagdoll _ragdoll;

        [Tooltip("What counts as floor when the dummy follows its knocked-down body: everything solid in the level, but not players.")]
        [SerializeField] LayerMask _floor = ~0;

        /// <summary>How far above the fallen body's hips the search for the floor starts, and how far down it looks, metres. WHY 0.5 and 3: the start clears a body lying on a desk or a step; the reach covers a body still in the air after a blast without finding the storey below (floors are 4.5 m apart).</summary>
        const float FloorSearchUp = 0.5f, FloorSearchDown = 3f;

        /// <summary>
        /// While the body is knocked down, keeps the dummy itself (its capsule, and the pose its
        /// body will rise into) on the floor under where the body lies, so it gets up where it
        /// landed instead of being dragged back to where it stood.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Runs before the animator and the ragdoll (default order, theirs are 100 and 110).
        /// One raycast a frame, and only while down; allocates nothing.
        /// </para>
        /// <para>
        /// WHY the dummy follows its ragdoll when a player's ragdoll must never move the
        /// player: the dummy has no motor and no network identity, so its own transform is the
        /// only thing that says where it is. For a real player the capsule stays in charge and
        /// the host decides where it goes (AGENTS.md §3). Alternative considered: leaving the
        /// dummy where it stood and pulling the body back to it; from across a room the body
        /// would slide metres along the floor as it rose.
        /// </para>
        /// </remarks>
        void LateUpdate()
        {
            if (_ragdoll == null || !_ragdoll.Down)
                return;

            Vector3 hips = _ragdoll.HipsPosition;
            Vector3 feet = new Vector3(hips.x, transform.position.y, hips.z);
            if (Physics.Raycast(hips + Vector3.up * FloorSearchUp, Vector3.down, out RaycastHit floor, FloorSearchUp + FloorSearchDown, _floor, QueryTriggerInteraction.Ignore))
                feet.y = floor.point.y;
            transform.position = feet;
        }

        /// <summary>
        /// Hits the dummy.
        /// </summary>
        /// <param name="hit">Where, which way, how hard (newton-seconds) and what kind.</param>
        /// <remarks>Main thread; allocates nothing. Does nothing outside Play Mode, where the ragdoll does not exist.</remarks>
        /// <example>
        /// From a weapon's raycast:
        /// <code>
        /// if (hit.collider.TryGetComponent(out HitDummy dummy))
        ///     dummy.Hit(new HitInfo { Point = hit.point, Direction = ray.direction, Impulse = 20f, Type = HitType.Bullet });
        /// </code>
        /// </example>
        public void Hit(in HitInfo hit)
        {
            if (_ragdoll != null)
                _ragdoll.Hit(hit);
        }
    }
}
