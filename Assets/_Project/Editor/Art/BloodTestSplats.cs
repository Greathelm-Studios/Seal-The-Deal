// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Sonnet 5.5 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "create a blood splatter shader that fits in within the games artstyle
//              that paints around objects and ontop of them when hit by these attacks."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : A Play Mode dev menu item that fires a few fake hits at whatever the camera is
//              looking at, so the blood can be judged without a second player or a weapon.
// -----------------------------------------------------------------------------
using CloseTheDeal.Effects;
using CloseTheDeal.Player;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Menu: <c>Seal the Deal &gt; Art &gt; Spawn Test Blood (Play Mode)</c>. Fires a pistol shot,
    /// a melee blow and a very hard blow at the surface the camera faces, spread left to right,
    /// each as if a victim stood a metre in front of it. Run it again for different shapes.
    /// </summary>
    public static class BloodTestSplats
    {
        /// <summary>How far in front of the wall the pretend victim stands, metres.</summary>
        const float VictimDistance = 1f;

        /// <summary>Gap between the three test hits along the wall, metres.</summary>
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
