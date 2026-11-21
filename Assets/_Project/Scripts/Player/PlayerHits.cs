using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Host-authoritative entry point for player hit reactions, relaying local-space impacts to every client's ragdoll and <see cref="HitFeed"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHits : NetworkBehaviour
    {
        [Tooltip("This player's ragdoll, which shows the flinch. Set by the tool that adds this part to the player.")]
        [SerializeField] PlayerRagdoll _ragdoll;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Temporary stand-in shooting slack (throwaway until real weapons set PlayerViewmodel.Driven).
        const float StandInEyeSlack = 1.5f;
        static readonly RaycastHit[] StandInHits = new RaycastHit[16];
#endif

        PlayerMotor _motor;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            if (_ragdoll != null && _ragdoll.KnocksDown)
                Debug.LogWarning("[PlayerHits] The player's hit profile has a knock-down strength above 0. Knock-down is not built for players; set Knock Down Impulse to 0 on that profile.", this);
        }

        /// <summary>Host-only entry point: broadcasts a confirmed hit to every machine to trigger visual flinch and <see cref="HitFeed"/>.</summary>
        [Server]
        public void Strike(HitInfo hit)
        {
            if (hit.Impulse <= 0f)
                return;

            hit.Point = transform.InverseTransformPoint(hit.Point);
            ShowStrike(hit);
        }

        // ExcludeServer + RunLocally runs immediately on host and avoids FishNet dropping duplicate self-delivery.
        [ObserversRpc(ExcludeServer = true, RunLocally = true)]
        void ShowStrike(HitInfo hit)
        {
            Transform body = _ragdoll != null ? _ragdoll.transform : transform;
            hit.Point = body.TransformPoint(hit.Point);
            if (_ragdoll != null)
                _ragdoll.Hit(hit);
            HitFeed.Raise(hit, transform);
        }

        /// <summary>Throwaway stand-in hook from <c>PlayerViewmodel.ReadTestInput</c> asking the host to re-cast and validate a shot or swing.</summary>
        public void RequestStandInHit(Vector3 eye, Vector3 aim, HitType kind)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (IsOwner)
                CheckStandInHit(eye, aim, kind);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Throwaway stand-in raycast (no lag compensation); delete when real weapons arrive.
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
