using System.Collections.Generic;
using CloseTheDeal.Glass;
using CloseTheDeal.Player;
using CloseTheDeal.Props;
using CloseTheDeal.Tower;
using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>Host-only blast impulse/damage resolution and shared shot shove velocity math.</summary>
    public static class Knockback
    {
        static readonly HashSet<Object> SeenTargets = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SeenTargets.Clear();
        }

        /// <summary>Throws every player and prop within the profile's radius of the point. Returns how many players were hit.</summary>
        public static int Explode(Vector3 point, BlastProfile blast, LayerMask playerMask, LayerMask propMask, Collider[] buffer)
        {
            return Explode(point, blast, playerMask, propMask, buffer, -1);
        }

        /// <summary>The same, naming who set the blast off so damage and kills are credited. -1 for the world.</summary>
        public static int Explode(Vector3 point, BlastProfile blast, LayerMask playerMask, LayerMask propMask, Collider[] buffer, int attackerClientId)
        {
            int found = Physics.OverlapSphereNonAlloc(point, blast.Radius, buffer, playerMask | propMask, QueryTriggerInteraction.Ignore);
            int playersHit = 0;

            SeenTargets.Clear();
            try
            {
                for (int i = 0; i < found; i++)
                {
                    if (ThrowOne(buffer[i], point, blast, attackerClientId))
                        playersHit++;
                }
            }
            finally
            {
                SeenTargets.Clear();
            }

            GlassRegistry.BreakInRadius(point, blast.Radius);
            return playersHit;
        }

        /// <summary>Velocity for a shove along a shot: the direction with some lift, at the given speed. Zero speed gives no shove.</summary>
        public static Vector3 ShoveVelocity(Vector3 direction, float speed, float lift)
        {
            if (speed <= 0f || direction.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            return (direction.normalized + Vector3.up * lift).normalized * speed;
        }

        /// <summary>Host only. Shoves one player along a shot.</summary>
        public static void Shove(PlayerMotor motor, Vector3 direction, float speed, float lift, float controlLossSeconds)
        {
            Vector3 velocity = ShoveVelocity(direction, speed, lift);
            if (motor != null && velocity != Vector3.zero)
                motor.ApplyKnockback(velocity, controlLossSeconds);
        }

        /// <summary>One thing caught in the blast. True when it was a player.</summary>
        static bool ThrowOne(Collider hit, Vector3 point, BlastProfile blast, int attackerClientId)
        {
            if (hit == null)
                return false;

            PlayerMotor motor = hit.GetComponentInParent<PlayerMotor>();
            Health health = hit.GetComponentInParent<Health>();
            if (motor != null || health != null)
            {
                Object target = motor != null ? (Object)motor : health;
                if (!SeenTargets.Add(target))
                    return false;

                Transform body = motor != null ? motor.transform : health.transform;
                Vector3 chest = body.position + Vector3.up * blast.PushHeight;
                Vector3 velocity = ThrowVelocity(point, chest, blast, blast.Speed);
                float damage = blast.Damage * Strength(point, chest, blast);

                // A living body takes the damage and the throw together; a dead or health-less one is only thrown.
                bool handled = health != null && blast.Damage > 0f
                    && health.ApplyHit(damage, chest, chest - point, attackerClientId, velocity, velocity, blast.ControlLossSeconds);
                if (!handled && health != null && !health.IsDead && health.KnockoutShoveSpeed > 0f
                    && velocity.sqrMagnitude >= health.KnockoutShoveSpeed * health.KnockoutShoveSpeed)
                {
                    handled = health.KnockOut(velocity, Mathf.Max(health.DefaultKnockoutSeconds, blast.ControlLossSeconds), chest);
                }
                if (!handled && motor != null)
                    motor.ApplyKnockback(velocity, blast.ControlLossSeconds);
                return motor != null;
            }

            Prop prop = hit.GetComponentInParent<Prop>();
            if (prop != null && SeenTargets.Add(prop))
                prop.Throw(ThrowVelocity(point, hit.bounds.center, blast, blast.PropSpeed));
            return false;
        }

        /// <summary>Away from the blast with some lift, full speed at the centre, EdgeStrength of it at the edge.</summary>
        static Vector3 ThrowVelocity(Vector3 point, Vector3 target, BlastProfile blast, float speed)
        {
            Vector3 away = target - point;
            float distance = away.magnitude;

            Vector3 direction = distance > 0.01f ? away / distance : Vector3.up;
            direction = (direction + Vector3.up * blast.UpBias).normalized;
            return direction * (speed * Strength(point, target, blast));
        }

        /// <summary>1 at the centre of the blast, EdgeStrength at its edge.</summary>
        static float Strength(Vector3 point, Vector3 target, BlastProfile blast)
        {
            float closeness = 1f - Mathf.Clamp01(Vector3.Distance(point, target) / blast.Radius);
            return Mathf.Lerp(blast.EdgeStrength, 1f, closeness);
        }
    }
}
