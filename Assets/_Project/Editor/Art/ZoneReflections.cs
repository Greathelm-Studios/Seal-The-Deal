// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Props session for Alex: "gloss surfaces show no highlight ... add reflection probes (box projection
//              per room or zone) so the pearl and chrome pick up the window and ceiling-light highlights"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Bakes a grid of small box-projected reflection probes over each storey of the kit office, so a gloss
//              surface reflects what is really near it. Its own file because FloorReflections bakes one whole-floor
//              probe per storey for the mirror floor (and is being changed on another branch); these sit inside it
//              and win wherever they cover, leaving it as the fallback.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Bakes zone reflection probes: a box-projected probe per <see cref="ZoneSize"/> square of each storey under the
    /// "Kit Office" root. Menu: <c>Close the Deal &gt; Art &gt; Bake Zone Reflections</c>.
    /// </summary>
    /// <remarks>
    /// <para>Run after <see cref="FloorReflections.Bake"/>, which flags the building's renderers Reflection Probe
    /// Static (a probe only photographs flagged objects). KitOfficeScene.BuildAndSave runs both.</para>
    /// <para>WHY a grid and not one probe per room: the L2 rooms are not on one grid and change with the plan; an
    /// 8 m grid follows the structural bays, needs no room list, and at Forward+'s per-pixel probe blending each
    /// pixel takes the nearest bay's photograph. Alternative considered: Screen Space Reflections, which URP does
    /// not have; planar reflections cost a second scene render every frame.</para>
    /// <para>Probes are Custom mode with the cubemap assigned, as <see cref="FloorReflections"/> (no lighting bake
    /// needed). Asks first from the menu; never saves the scene.</para>
    /// </remarks>
    public static class ZoneReflections
    {
        const string ProbeFolder = "Assets/_Project/Art/Look/Probes";
        const string GroupName = "Zone Reflection Probes";
        const string KitRootName = "Kit Office";

        /// <summary>Floor plate, metres (KitOfficeScene's L2 plan: 40 × 32).</summary>
        const float PlateWidth = 40f, PlateDepth = 32f;
        /// <summary>Floor to soffit, metres (KitOfficeScene's clear height).</summary>
        const float ClearHeight = 4.3f;
        /// <summary>Zone size, metres: two structural bays, so a probe's box spans a room or a stretch of ring.</summary>
        const float ZoneSize = 8f;
        /// <summary>How far a box reaches past the floor and soffit, metres: past the 0.2 m slab, as FloorReflections.</summary>
        const float BoxMargin = 0.4f;
        /// <summary>Blend between neighbouring zones, metres. WHY 1: wide enough that a seam between two photographs
        /// taken 8 m apart never shows as a line across a glossy floor.</summary>
        const float BlendDistance = 1f;
        /// <summary>Capture height above each floor, metres: eye height, where the props' gloss is seen from.</summary>
        const float CaptureHeight = 1.5f;
        /// <summary>Cubemap face size, pixels. WHY 256: small zones are seen close but blurred by gloss below mirror
        /// smoothness; 40 probes at 512 would cost four times the memory for little visible gain.</summary>
        const int Resolution = 256;

        [MenuItem("Close the Deal/Art/Bake Zone Reflections")]
        static void BakeFromMenu() => Bake(askFirst: true);

        /// <summary>Rebuilds and bakes the zone probes in the active scene.</summary>
        /// <param name="askFirst">Show a confirmation dialog first. Pass false only from scripts the user asked to run.</param>
        /// <returns>A one-line report, also written to the Console.</returns>
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

            Transform old = kit.Find(GroupName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var group = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(group, "Bake Zone Reflections");
            group.transform.SetParent(kit, false);   // under the building, so the probes move with it

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

        static string Report(string line)
        {
            Debug.Log(line);
            return line;
        }
    }
}
