// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "add hit reactions when hit by guns/melee weapons for the players so
//              that when a shooter hits another player, they see a visible reaction in the
//              person; currently theres a shake, which is quite funny but there should be an
//              exaggeration of it."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The one place a player is hit. The host decides a hit and calls Strike; every
//              machine then plays the flinch on that player's ragdoll and tells HitFeed. Its
//              own NetworkBehaviour so PlayerMotor's predicted movement stays untouched by hits.
// -----------------------------------------------------------------------------
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Takes hits on a player and shows them on every machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ownership (AGENTS.md §3):</b> the host owns every hit. Weapon code on the host calls
    /// <see cref="Strike"/>; it sends one reliable message to every machine with the hit (about
    /// 35 bytes, once per hit, nothing per tick). Each machine, the host included, then plays
    /// the reaction on its own copy of this player's ragdoll and raises <see cref="HitFeed"/>.
    /// Clients never decide a hit. The reaction is visual only: it moves the ragdoll, never the
    /// capsule, so movement, prediction and reconciling are not affected.
    /// </para>
    /// <para>
    /// WHY the hit point travels relative to the player: each machine shows the player at a
    /// slightly different place (smoothing, interpolation), and a point in world space could
    /// land on a different body part, or miss the body, on another screen. Relative to the
    /// body it strikes the same part everywhere.
    /// </para>
    /// </remarks>
    /// <example>
    /// From weapon code on the host, after its own raycast struck a player's capsule:
    /// <code>
    /// if (hit.collider.GetComponentInParent&lt;PlayerHits&gt;() is PlayerHits victim)
    ///     victim.Strike(new HitInfo { Point = hit.point, Direction = ray.direction, Impulse = 20f, Type = HitType.Bullet });
    /// </code>
    /// </example>
    [DisallowMultipleComponent]
    public sealed class PlayerHits : NetworkBehaviour
    {
        [Tooltip("This player's ragdoll, which shows the flinch. Set by the tool that adds this part to the player.")]
        [SerializeField] PlayerRagdoll _ragdoll;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// THROWAWAY (stand-in shooting): how far from the host's idea of the shooter's eye the
        /// shooter's own eye may be, metres, before a stand-in shot is ignored. WHY 1.5: the
        /// owner runs ahead of the host by its prediction (about 0.7 m at a sprint on a 100 ms
        /// round trip) and may be leaning (up to about 0.5 m sideways); this covers both with
        /// room to spare and still stops a shot fired from across the map.
        /// </summary>
        const float StandInEyeSlack = 1.5f;

        /// <summary>THROWAWAY: room for every collider along a stand-in shot's ray, the shooter's own included.</summary>
        static readonly RaycastHit[] StandInHits = new RaycastHit[16];
#endif

        PlayerMotor _motor;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            // WHY a warning and not a fix: the profile is the designer's (AGENTS.md §4). A player
            // knocked down would lie where it fell while its capsule walked on; knock-down on
            // players is not built (HIT_REACTIONS.md).
            if (_ragdoll != null && _ragdoll.KnocksDown)
                Debug.LogWarning("[PlayerHits] The player's hit profile has a knock-down strength above 0. Knock-down is not built for players; set Knock Down Impulse to 0 on that profile.", this);
        }

        /// <summary>
        /// Hits this player. Host only: the host has decided the hit; this tells every machine.
        /// </summary>
        /// <param name="hit">Where (world space, on the host), which way, how hard (newton-seconds) and what kind.</param>
        /// <remarks>Every machine, the host included, flinches this player's ragdoll and raises <see cref="HitFeed"/>. Calling it on a client does nothing but log a warning.</remarks>
        [Server]
        public void Strike(HitInfo hit)
        {
            if (hit.Impulse <= 0f)
                return;

            hit.Point = transform.InverseTransformPoint(hit.Point);
            ShowStrike(hit);
        }

        /// <summary>Plays a hit on this machine. Runs on the host straight away and on every client when the message arrives.</summary>
        /// <param name="hit">The hit, with <see cref="HitInfo.Point"/> relative to the player's root.</param>
        // WHY RunLocally: the host plays it at once rather than waiting for its own copy of the
        // message. ExcludeServer then skips sending that copy to the host's own client, which
        // FishNet would otherwise receive and drop (it blocks a RunLocally RPC running twice).
        [ObserversRpc(ExcludeServer = true, RunLocally = true)]
        void ShowStrike(HitInfo hit)
        {
            // The ragdoll follows the smoothed visual, so the point is put back on that.
            Transform body = _ragdoll != null ? _ragdoll.transform : transform;
            hit.Point = body.TransformPoint(hit.Point);
            if (_ragdoll != null)
                _ragdoll.Hit(hit);
            HitFeed.Raise(hit, transform);
        }

        /// <summary>
        /// THROWAWAY (stand-in shooting, see <c>PlayerViewmodel.ReadTestInput</c>): asks the
        /// host to check a stand-in shot or swing this player made and hit whoever it struck.
        /// Call on this player's own machine only.
        /// </summary>
        /// <param name="eye">Where the shot left from, world space: this machine's camera.</param>
        /// <param name="aim">Which way, world space.</param>
        /// <param name="kind">Bullet or Melee; anything else is ignored.</param>
        /// <remarks>Carries only what the player did, never what it struck: the host casts the ray again itself. Delete with the stand-in input when real weapons arrive.</remarks>
        public void RequestStandInHit(Vector3 eye, Vector3 aim, HitType kind)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsOwner)
                CheckStandInHit(eye, aim, kind);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>THROWAWAY: the host's half of <see cref="RequestStandInHit"/>.</summary>
        /// <remarks>
        /// Ignores a shot whose eye is far from where the host has this player, then casts the
        /// same ray from it and strikes the first player it meets (passing through the shooter's
        /// own capsule, which the host may have slightly behind the shooter's view).
        /// Known limit: no lag compensation. The host checks against where players are on the
        /// host now, so a shot at someone moving fast across the view can miss on the host
        /// although it looked like a hit.
        /// </remarks>
        [ServerRpc]
        void CheckStandInHit(Vector3 eye, Vector3 aim, HitType kind)
        {
            if (_motor == null || aim.sqrMagnitude < 0.25f || (kind != HitType.Bullet && kind != HitType.Melee))
                return;

            Vector3 hostEye = _motor.transform.position + Vector3.up * _motor.EyeHeight;
            if ((eye - hostEye).sqrMagnitude > StandInEyeSlack * StandInEyeSlack)
                return;

            bool melee = kind == HitType.Melee;
            Vector3 direction = aim.normalized;
            float reach = melee ? PlayerViewmodel.StandInMeleeReach : PlayerViewmodel.StandInBulletRange;
            int arms = LayerMask.NameToLayer(PlayerViewmodel.LayerName);
            int mask = arms >= 0 ? ~(1 << arms) : ~0;
            int count = Physics.RaycastNonAlloc(eye, direction, StandInHits, reach, mask, QueryTriggerInteraction.Ignore);

            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (StandInHits[i].collider.transform.IsChildOf(transform) || StandInHits[i].distance >= best)
                    continue;
                best = StandInHits[i].distance;
                nearest = i;
            }
            if (nearest < 0)
                return;

            // Only on a hit, once per shot, so the lookup is not a per-frame cost. A wall or the
            // test dummy in front has no PlayerHits and stops the shot.
            RaycastHit struck = StandInHits[nearest];
            var victim = struck.collider.GetComponentInParent<PlayerHits>();
            if (victim == null)
                return;

            victim.Strike(new HitInfo
            {
                Point = struck.point,
                Direction = direction,
                Impulse = melee ? PlayerViewmodel.StandInMeleeImpulse : PlayerViewmodel.StandInBulletImpulse,
                Type = kind
            });
        }
#endif
    }
}
