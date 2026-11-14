// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex: "yes, try it" (fake bounce light, the biggest remaining "Unity look" tell)
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Fakes the light that sun patches throw back into the room. Real time lighting in
//              URP has no bounce, so a bright patch on the floor leaves the ceiling and walls
//              above it unlit, which reads as a game engine. This puts one soft, shadowless,
//              upward spotlight on each sun patch. It is its own tool because the patches depend
//              on the scene's sun and window layout, and must be re-placed when either changes.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Places fake bounce lights on the sun patches of the open scene. Menu:
    /// <c>Close the Deal &gt; Art &gt; Add Bounce Light</c>.
    /// </summary>
    /// <remarks>
    /// Works out where sunlight through each window bay lands on each floor of the <c>Kit Office</c>
    /// root, from the scene sun's direction, and puts one upward spotlight there. The lights live in a
    /// <c>Bounce Lights</c> child of that root, so they follow the building when it is moved, and
    /// the group is rebuilt on each run. Asks first from the menu, and never saves.
    /// Run it after <c>Apply Office Look</c> (which sets the sun) and before
    /// <c>Bake Floor Reflections</c>, so the reflections include the lifted ceiling.
    /// Alternative considered: baked global illumination (Adaptive Probe Volumes). That is real
    /// bounce, but needs mixed lighting, a bake per scene and per floor template, and would need
    /// redoing for every procedurally stacked floor. Fake lights are placed in a second and cost
    /// one shadowless light each in Forward+.
    /// </remarks>
    public static class BounceLight
    {
        const string GroupName = "Bounce Lights";
        const string KitRootName = "Kit Office";

        /// <summary>Façade bays across the floor (KitOfficeScene: 8 × 4 m since the labyrinth, 2026-11-13).</summary>
        const int Bays = 8;
        /// <summary>Bay width along the façade, metres (KitOfficeScene.Bay).</summary>
        const float Bay = 4f;
        /// <summary>Room size, metres (KitOfficeScene's labyrinth: 32 m wide, 24 m deep, since 2026-11-13).</summary>
        const float RoomWidth = 32f, RoomDepth = 24f;
        /// <summary>Top of the window sill, metres above the floor (GreyboxTowerSetup.SillHeight, 0.6 since 2026-11-12).</summary>
        const float SillHeight = 0.6f;
        /// <summary>Floor to ceiling, metres: the window glass runs from the sill to here (KitOfficeScene.ClearHeight).</summary>
        const float ClearHeight = 4.3f;
        /// <summary>Patches closer than this to a wall are pulled in, metres, so a light never sits inside one.</summary>
        const float WallClearance = 0.5f;

        /// <summary>
        /// Brightness of each bounce light (URP spot intensity). WHY 4: at full ambient no strength
        /// showed, because the ceiling was already near-white everywhere. With the ambient at 60%
        /// (OfficeLook.AmbientLevel), 4 gives the ceiling a clear lift over the sun patches that
        /// fades toward the back of the room. Chosen by Alex from a sweep of 2.4 to 5, 2026-11-12.
        /// </summary>
        const float Intensity = 4f;
        /// <summary>How far each light reaches, metres: the ceiling above it and the walls of its bay.</summary>
        const float Range = 7f;
        /// <summary>Cone, degrees. Nearly a hemisphere, so it reads as light thrown up off a surface.</summary>
        const float OuterAngle = 160f;
        /// <summary>Inner cone, degrees. Small, so the light fades out gradually toward its edge.</summary>
        const float InnerAngle = 40f;
        /// <summary>Height above the floor, metres. Just above it, so the floor itself stays unlit by the fake.</summary>
        const float LiftOffFloor = 0.15f;
        /// <summary>The bounce's colour: the sun's near white, cooled by the pale blue-grey floor it comes off.</summary>
        static readonly Color BounceColour = new Color(0.86f, 0.91f, 1f);

        [MenuItem("Close the Deal/Art/Add Bounce Light")]
        static void AddFromMenu() => Add(askFirst: true);

        /// <summary>
        /// Rebuilds the bounce lights in the active scene.
        /// </summary>
        /// <param name="askFirst">Show a confirmation dialog first. Pass false only from scripts the user asked to run.</param>
        /// <returns>A one-line report, also written to the Console.</returns>
        public static string Add(bool askFirst)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Report("[Bounce Light] Stop Play Mode first: lights are scene objects and Play Mode changes are thrown away.");

            Scene scene = SceneManager.GetActiveScene();
            Light sun = FindSun();
            if (sun == null)
                return Report($"[Bounce Light] No directional light in '{scene.name}'; nothing done.");

            Transform kit = FindKitRoot(scene);
            List<float> floors = kit != null ? FloorHeights(kit) : new List<float>();
            if (floors.Count == 0)
                return Report($"[Bounce Light] No '{KitRootName}' root with 'Floor N' children in '{scene.name}'; nothing done.");

            // The direction the sunlight travels, in the building's own space, so the patches are
            // right wherever the building is moved or turned. It only comes in through the façade if
            // it is heading down and into the room (toward -Z; the façade faces +Z, TOWER.md).
            Vector3 travel = kit.InverseTransformDirection(sun.transform.forward);
            if (travel.y >= -0.05f || travel.z >= -0.05f)
                return Report("[Bounce Light] The sun does not shine in through the façade, so there are no sun patches to bounce; nothing done.");

            if (askFirst && !EditorUtility.DisplayDialog("Add Bounce Light",
                    $"This replaces the '{GroupName}' object in '{scene.name}' with one soft light on each sun patch ({Bays} per floor, {floors.Count} floors).\n\nThe scene is not saved.",
                    "Add", "Cancel"))
                return "Cancelled.";

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == GroupName)
                    Undo.DestroyObjectImmediate(root);
            }

            Transform old = kit.Find(GroupName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);

            // WHY under the kit root: the first version put the lights at the scene root, so when
            // Alex moved the building (2026-11-12) they stayed behind. As children they follow it.
            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Add Bounce Light");
            group.transform.SetParent(kit, false);

            int placed = 0;
            float windowMiddle = (SillHeight + ClearHeight) * 0.5f;
            for (int f = 0; f < floors.Count; f++)
            {
                float floorY = floors[f];
                for (int bay = 0; bay < Bays; bay++)
                {
                    // Follow the light from the middle of the bay's glass down to the floor.
                    var throughGlass = new Vector3((bay + 0.5f) * Bay, floorY + windowMiddle, 0f);
                    float distance = windowMiddle / -travel.y;
                    Vector3 patch = throughGlass + travel * distance;
                    if (patch.z < -RoomDepth + WallClearance)
                        continue;
                    patch.x = Mathf.Clamp(patch.x, WallClearance, RoomWidth - WallClearance);
                    patch.y = floorY + LiftOffFloor;

                    var go = new GameObject($"Bounce Floor {f} Bay {bay}");
                    go.transform.SetParent(group.transform, false);
                    go.transform.localPosition = patch;
                    go.transform.rotation = Quaternion.LookRotation(Vector3.up);
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Spot;
                    light.spotAngle = OuterAngle;
                    light.innerSpotAngle = InnerAngle;
                    light.range = Range;
                    light.intensity = Intensity;
                    light.color = BounceColour;
                    light.shadows = LightShadows.None;
                    light.lightmapBakeType = LightmapBakeType.Realtime;
                    placed++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return Report($"[Bounce Light] Placed {placed} bounce lights in '{scene.name}'. Scene not saved. Re-run Bake Floor Reflections so the floor reflects the lifted ceiling.");
        }

        static Light FindSun()
        {
            if (RenderSettings.sun != null)
                return RenderSettings.sun;
            foreach (Light light in Object.FindObjectsByType<Light>())
            {
                if (light.type == LightType.Directional)
                    return light;
            }

            return null;
        }

        static Transform FindKitRoot(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == KitRootName)
                    return root.transform;
            }

            return null;
        }

        /// <summary>Heights of the building's floors, metres, in the building's own space.</summary>
        static List<float> FloorHeights(Transform kit)
        {
            var heights = new List<float>();
            foreach (Transform child in kit)
            {
                if (child.name.StartsWith("Floor "))
                    heights.Add(child.localPosition.y);
            }

            heights.Sort();
            return heights;
        }

        static string Report(string message)
        {
            Debug.Log(message);
            return message;
        }
    }
}
