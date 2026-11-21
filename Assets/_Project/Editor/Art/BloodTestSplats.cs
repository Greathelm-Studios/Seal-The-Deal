using CloseTheDeal.Effects;
using CloseTheDeal.Player;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Dev menu item firing three Play Mode test blood hits at the surface in front of the camera.</summary>
    public static class BloodTestSplats
    {
        const float VictimDistance = 1f;
        const float Spacing = 1.8f;

        const float MaxRange = 15f;

        [MenuItem("Seal the Deal/Art/Spawn Test Blood (Play Mode)")]
        static void Spawn()
        {
            Camera cam = Camera.main;
            if (cam == null && SceneView.lastActiveSceneView != null)
                cam = SceneView.lastActiveSceneView.camera;
            if (cam == null)
            {
                Debug.LogWarning("[Blood] No Main Camera or Scene view to aim from.");
                return;
            }

            Vector3 forward = cam.transform.forward;
            var shots = new (HitType type, float impulse)[]
            {
                (HitType.Bullet, 20f),
                (HitType.Melee, 150f),
                (HitType.Melee, 500f),
            };

            for (int i = 0; i < shots.Length; i++)
            {
                Vector3 origin = cam.transform.position + cam.transform.right * ((i - 1) * Spacing);
                if (!Physics.Raycast(origin, forward, out RaycastHit wall, MaxRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    Debug.LogWarning($"[Blood] Test hit {i + 1} found nothing within {MaxRange} m in front of the camera.");
                    continue;
                }

                var hit = new HitInfo
                {
                    Point = wall.point - forward * VictimDistance,
                    Direction = forward,
                    Impulse = shots[i].impulse,
                    Type = shots[i].type,
                };
                BloodSplatSystem.Splash(hit, null);
            }
        }

        [MenuItem("Seal the Deal/Art/Spawn Test Blood (Play Mode)", true)]
        static bool CanSpawn() => Application.isPlaying;
    }
}
