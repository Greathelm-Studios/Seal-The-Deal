using CloseTheDeal.Combat;
using CloseTheDeal.Player;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Dev
{
    /// <summary>Play-Mode menu shortcuts for testing player death ragdoll and knocked-out stun states.</summary>
    public static class PlayerStateDevMenu
    {
        const string KnockOutMenu = "Seal the Deal/Dev/Knock Out Local Player";
        const string KillMenu = "Seal the Deal/Dev/Kill Local Player";

        [MenuItem(KnockOutMenu, true)]
        [MenuItem(KillMenu, true)]
        static bool CanTrigger() => Application.isPlaying && PlayerMotor.Local != null;

        [MenuItem(KnockOutMenu, false, 200)]
        static void KnockOutLocal()
        {
            if (!TryGetHostHealth(out Health health))
                return;

            Transform body = health.transform;
            Vector3 shove = (-body.forward + Vector3.up * 0.45f).normalized * 9f;
            Vector3 chest = body.position + Vector3.up * 1.1f;
            health.KnockOut(shove, -1f, chest);
        }

        [MenuItem(KillMenu, false, 201)]
        static void KillLocal()
        {
            if (!TryGetHostHealth(out Health health))
                return;

            Transform body = health.transform;
            Vector3 direction = (-body.forward + Vector3.up * 0.35f).normalized;
            Vector3 chest = body.position + Vector3.up * 1.1f;
            health.ApplyDamage(9999f, chest, direction, -1);
        }

        static bool TryGetHostHealth(out Health health)
        {
            health = null;
            PlayerMotor local = PlayerMotor.Local;
            if (local == null || !local.TryGetComponent(out health))
                return false;

            if (!health.IsServerStarted)
            {
                Debug.LogWarning("[PlayerStateDevMenu] Run this on the host; death and knockout are host-authoritative.");
                return false;
            }
            return true;
        }
    }
}
