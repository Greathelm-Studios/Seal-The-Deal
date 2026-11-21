using System.Collections.Generic;
using CloseTheDeal.Core;
using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>Host-only radial explosion damage against IDamageable targets.</summary>
    public static class ItemBlast
    {
        static readonly Collider[] Buffer = new Collider[256];
        static readonly List<IDamageable> Hurt = new(16);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            System.Array.Clear(Buffer, 0, Buffer.Length);
            Hurt.Clear();
        }

        /// <summary>
        /// Hurts every IDamageable with a collider inside the radius, once each, full damage at
        /// the centre falling to the edge fraction of it at the rim. Returns how many were hurt.
        /// </summary>
        public static int Damage(Vector3 point, float radius, float centreDamage, float edgeFraction, int attackerClientId)
        {
            int found = Physics.OverlapSphereNonAlloc(point, radius, Buffer, ItemLayers.PlayerMask | ItemLayers.PropMask, QueryTriggerInteraction.Ignore);
            Hurt.Clear();

            for (int i = 0; i < found; i++)
            {
                Collider collider = Buffer[i];
                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || Hurt.Contains(target))
                    continue;

                Hurt.Add(target);
                Vector3 nearest = collider is MeshCollider mesh && !mesh.convex
                    ? collider.ClosestPointOnBounds(point)
                    : collider.ClosestPoint(point);
                Vector3 away = nearest - point;
                float distance = away.magnitude;
                float closeness = 1f - Mathf.Clamp01(distance / radius);
                float amount = centreDamage * Mathf.Lerp(edgeFraction, 1f, closeness);
                target.ApplyDamage(amount, nearest, distance > 0.01f ? away / distance : Vector3.up, attackerClientId);
            }

            int count = Hurt.Count;
            Hurt.Clear();
            return count;
        }
    }
}
