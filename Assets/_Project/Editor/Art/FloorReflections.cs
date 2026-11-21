using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Adds and bakes one box-projected reflection probe per office floor.</summary>
    public static class FloorReflections
    {
        const string ProbeFolder = "Assets/_Project/Art/Look/Probes";
        const string GroupName = "Reflection Probes";
        const string KitRootName = "Kit Office";

        const float RoomWidth = 40f;
        const float RoomDepth = 32f;
        const float ClearHeight = 4.3f;

        const float BoxMargin = 0.4f;
        const float BlendDistance = 0.2f;
        const float CaptureHeight = 1.0f;
        const float CaptureDepthFraction = 0.42f;
        const int Resolution = 512;

        [MenuItem("Close the Deal/Art/Bake Floor Reflections")]
        static void BakeFromMenu() => Bake(askFirst: true);

        /// <summary>Rebuilds and bakes the floor probes in the active scene.</summary>
        public static string Bake(bool askFirst)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Report("[Floor Reflections] Stop Play Mode first: probes are scene objects and Play Mode changes are thrown away.");

            Scene scene = SceneManager.GetActiveScene();
            Transform kit = FindKitRoot(scene);
            List<float> floors = kit != null ? FloorHeights(kit) : new List<float>();
            if (floors.Count == 0)
                return Report($"[Floor Reflections] No '{KitRootName}' root with 'Floor N' children in '{scene.name}'; nothing done.");

            if (askFirst && !EditorUtility.DisplayDialog("Bake Floor Reflections",
                    $"This replaces the '{GroupName}' object in '{scene.name}' with {floors.Count} reflection probes and bakes them to {ProbeFolder}.\n\nThe scene is not saved.",
                    "Bake", "Cancel"))
                return "Cancelled.";

            RemoveOldGroups(scene, kit);
            int flagged = FlagBuildingForProbes(kit);

            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Bake Floor Reflections");
            group.transform.SetParent(kit, false);
            if (!AssetDatabase.IsValidFolder(ProbeFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Look", "Probes");

            int baked = 0;
            for (int i = 0; i < floors.Count; i++)
            {
                ReflectionProbe probe = CreateProbe(group.transform, i, floors[i]);
                string path = $"{ProbeFolder}/{scene.name}_Floor{i}.exr";
                if (Lightmapping.BakeReflectionProbe(probe, path))
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                    baked++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            string turned = kit.rotation == Quaternion.identity ? "" : " WARNING: the building is rotated, and probe boxes cannot rotate, so reflections will be misplaced.";
            return Report($"[Floor Reflections] Baked {baked} of {floors.Count} floor probes in '{scene.name}' to {ProbeFolder} ({flagged} building objects newly flagged for probes). Scene not saved.{turned}");
        }

        static void RemoveOldGroups(Scene scene, Transform kit)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == GroupName)
                    Undo.DestroyObjectImmediate(root);
            }

            Transform old = kit.Find(GroupName);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
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

        static int FlagBuildingForProbes(Transform kit)
        {
            int changed = 0;
            foreach (Renderer renderer in kit.GetComponentsInChildren<Renderer>(true))
            {
                GameObject go = renderer.gameObject;
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & StaticEditorFlags.ReflectionProbeStatic) != 0)
                    continue;
                Undo.RecordObject(go, "Bake Floor Reflections");
                GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.ReflectionProbeStatic);
                changed++;
            }

            return changed;
        }

        static ReflectionProbe CreateProbe(Transform parent, int index, float floorY)
        {
            var go = new GameObject($"Reflection Probe Floor {index}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(RoomWidth * 0.5f, floorY + CaptureHeight, -RoomDepth * CaptureDepthFraction);

            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom;
            probe.boxProjection = true;
            Vector3 roomCentre = new Vector3(RoomWidth * 0.5f, floorY + ClearHeight * 0.5f, -RoomDepth * 0.5f);
            probe.center = roomCentre - go.transform.localPosition;
            probe.size = new Vector3(RoomWidth, ClearHeight, RoomDepth) + Vector3.one * (2f * BoxMargin);
            probe.blendDistance = BlendDistance;
            probe.resolution = Resolution;
            probe.hdr = true;
            return probe;
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
