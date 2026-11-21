using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Bakes a grid of box-projected reflection probes per storey under Kit Office.</summary>
    public static class ZoneReflections
    {
        const string ProbeFolder = "Assets/_Project/Art/Look/Probes";
        const string GroupName = "Zone Reflection Probes";
        const string KitRootName = "Kit Office";

        const float PlateWidth = 40f, PlateDepth = 32f;
        const float ClearHeight = 4.3f;
        const float ZoneSize = 8f;
        const float BoxMargin = 0.4f;
        const float BlendDistance = 1f;
        const float CaptureHeight = 1.5f;
        const int Resolution = 256;

        [MenuItem("Seal the Deal/Art/Bake Zone Reflections")]
        static void BakeFromMenu() => Bake(askFirst: true);

        /// <summary>Rebuilds and bakes the zone probes in the active scene.</summary>
        public static string Bake(bool askFirst)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Report("[Zone Reflections] Stop Play Mode first: probes are scene objects and Play Mode changes are thrown away.");
            Scene scene = SceneManager.GetActiveScene();
            Transform kit = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == KitRootName) kit = root.transform;
            var floors = new List<float>();
            if (kit != null)
                foreach (Transform child in kit)
                    if (child.name.StartsWith("Floor ")) floors.Add(child.localPosition.y);
            if (floors.Count == 0)
                return Report($"[Zone Reflections] No '{KitRootName}' root with 'Floor N' children in '{scene.name}'; nothing done.");
            floors.Sort();
            int cols = Mathf.CeilToInt(PlateWidth / ZoneSize), rows = Mathf.CeilToInt(PlateDepth / ZoneSize);
            if (askFirst && !EditorUtility.DisplayDialog("Bake Zone Reflections",
                    $"This replaces '{GroupName}' in '{scene.name}' with {floors.Count * cols * rows} reflection probes and bakes them to {ProbeFolder}.\n\nThe scene is not saved.",
                    "Bake", "Cancel"))
                return "Cancelled.";

            EnsureFolder(ProbeFolder);

            Transform old = kit.Find(GroupName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Bake Zone Reflections");
            group.transform.SetParent(kit, false);

            int baked = 0, total = 0;
            for (int f = 0; f < floors.Count; f++)
                for (int i = 0; i < cols; i++)
                    for (int j = 0; j < rows; j++)
                    {
                        total++;
                        float x0 = i * ZoneSize, z0 = -j * ZoneSize;
                        float w = Mathf.Min(ZoneSize, PlateWidth - x0), d = Mathf.Min(ZoneSize, PlateDepth - j * ZoneSize);
                        var go = new GameObject($"Zone Probe {f}-{i}-{j}");
                        go.transform.SetParent(group.transform, false);
                        go.transform.localPosition = new Vector3(x0 + w * 0.5f, floors[f] + CaptureHeight, z0 - d * 0.5f);
                        var probe = go.AddComponent<ReflectionProbe>();
                        probe.mode = ReflectionProbeMode.Custom;
                        probe.boxProjection = true;
                        probe.center = new Vector3(0f, ClearHeight * 0.5f - CaptureHeight, 0f);
                        probe.size = new Vector3(w, ClearHeight + 2f * BoxMargin, d);
                        probe.blendDistance = BlendDistance;
                        probe.resolution = Resolution;
                        probe.hdr = true;
                        string path = $"{ProbeFolder}/{scene.name}_Zone{f}_{i}_{j}.exr";
                        if (Lightmapping.BakeReflectionProbe(probe, path))
                        {
                            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                            baked++;
                        }
                    }
            EditorSceneManager.MarkSceneDirty(scene);
            return Report($"[Zone Reflections] Baked {baked} of {total} zone probes in '{scene.name}' to {ProbeFolder}. Scene not saved.");
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }

        static string Report(string line)
        {
            Debug.Log(line);
            return line;
        }
    }
}
