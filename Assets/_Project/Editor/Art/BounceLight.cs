using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Places fake bounce spotlights on the sun patches of the open scene.</summary>
    public static class BounceLight
    {
        const string GroupName = "Bounce Lights";
        const string KitRootName = "Kit Office";

        const int Bays = 10;
        const float Bay = 4f;
        const float RoomWidth = 40f, RoomDepth = 32f;
        const float SillHeight = 0.6f;
        const float ClearHeight = 4.3f;
        const float WallClearance = 0.5f;

        const float Intensity = 2.5f;
        const float Range = 7f;
        const float OuterAngle = 160f;
        const float InnerAngle = 40f;
        const float LiftOffFloor = 0.15f;
        static readonly Color BounceColour = new Color(0.86f, 0.91f, 1f);

        [MenuItem("Close the Deal/Art/Add Bounce Light")]
        static void AddFromMenu() => Add(askFirst: true);

        /// <summary>Rebuilds the bounce lights in the active scene.</summary>
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

            // Facade faces +Z; sunlight enters only when travelling down and toward -Z in kit space.
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
