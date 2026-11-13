// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex: "no carpet, i want a shiny floor, i want to try get rid of the 'unity look'"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Gives each office floor of the open scene a box-projected reflection probe and
//              bakes it, so the polished floor mirrors the room (windows, frames, the cobalt wall,
//              the red door) instead of the sky. URP has no screen-space reflections, so probes
//              are the reflection source. It is its own tool because probes belong to a scene's
//              layout and must be re-baked whenever the floors are rebuilt or re-lit.
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
    /// Adds and bakes one reflection probe per office floor. Menu:
    /// <c>Close the Deal &gt; Art &gt; Bake Floor Reflections</c>.
    /// </summary>
    /// <remarks>
    /// Looks for a root object named <c>Kit Office</c> with <c>Floor N</c> children (what
    /// <see cref="KitOfficeScene"/> builds). Puts the probes in a <c>Reflection Probes</c> child of
    /// that root, so they follow the building when it is moved, and replaces that group on each
    /// run. Moving is fine; rotating the building is not, because probe boxes stay world-aligned.
    /// Asks first from the menu, and never saves the scene. Cubemaps are written to
    /// <see cref="ProbeFolder"/>. Run it again after <c>Apply Office Look</c> or a rebuild of the
    /// floors, since a probe is a photograph of the room as it was lit when baked.
    /// Probes use Custom mode with the cubemap assigned directly, rather than Baked mode. WHY:
    /// Baked mode stores the cubemap in the scene's lighting data, which only a full lighting bake
    /// writes; Custom keeps the probes independent of whether anyone has run one.
    /// Alternative considered: planar reflections (a second camera rendering the mirrored room).
    /// They would reflect moving players too, but cost a second render of the scene every frame,
    /// with up to eight players. Probes are free at runtime and reflect everything static.
    /// </remarks>
    public static class FloorReflections
    {
        const string ProbeFolder = "Assets/_Project/Art/Look/Probes";
        const string GroupName = "Reflection Probes";
        const string KitRootName = "Kit Office";

        /// <summary>Main floor plus service spine, metres across x (TOWER.md footprint, KitOfficeScene).</summary>
        const float RoomWidth = 28f;
        /// <summary>Façade strip, three 4 m rows and the 4 m spine, metres along z (KitOfficeScene.Depth).</summary>
        const float RoomDepth = 18f;
        /// <summary>Floor to ceiling, metres (KitOfficeScene.ClearHeight).</summary>
        const float ClearHeight = 4.3f;

        /// <summary>
        /// How far each probe's box reaches past the floor, walls and ceiling, metres. WHY: a probe's
        /// weight fades to nothing at its box edge, and with the box exactly on the room's surfaces
        /// the floor got no probe and mirrored the sky. 0.4 m clears the 0.2 m slab and the walls.
        /// </summary>
        const float BoxMargin = 0.4f;
        /// <summary>Distance over which neighbouring probes blend at box edges, metres.</summary>
        const float BlendDistance = 0.2f;
        /// <summary>Capture height above each floor, metres. Low, because the floor is what reflects.</summary>
        const float CaptureHeight = 1.0f;
        /// <summary>Capture point along z, as a fraction of the depth from the façade: the middle of the main floor.</summary>
        const float CaptureDepthFraction = 0.42f;
        /// <summary>Cubemap face size, pixels. 512 keeps window frames and door edges legible in a near-mirror floor.</summary>
        const int Resolution = 512;

        [MenuItem("Close the Deal/Art/Bake Floor Reflections")]
        static void BakeFromMenu() => Bake(askFirst: true);

        /// <summary>
        /// Rebuilds and bakes the floor probes in the active scene.
        /// </summary>
        /// <param name="askFirst">Show a confirmation dialog first. Pass false only from scripts the user asked to run.</param>
        /// <returns>A one-line report, also written to the Console.</returns>
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

            // WHY under the kit root: the first version put the probes at the scene root, so when
            // Alex moved the building (2026-11-12) they stayed behind and the floor lost its
            // reflections. As children they move with the building.
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

        /// <summary>Removes the probe group from a previous run, whether at the scene root (first version) or under the building.</summary>
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

        /// <summary>
        /// Marks everything under the kit root as seen by reflection probes, and returns how many
        /// objects it changed. WHY: a baked probe only photographs objects with the Reflection Probe
        /// Static flag; without it every probe captured an empty sky and the floor mirrored that.
        /// Only the building is flagged: players and anything else that moves stay out of the bake.
        /// </summary>
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
            // Positions are in the building's own space, so they hold wherever the building is moved.
            go.transform.localPosition = new Vector3(RoomWidth * 0.5f, floorY + CaptureHeight, -RoomDepth * CaptureDepthFraction);

            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom;
            // Box projection: a reflection lands where the reflected thing really is in the room,
            // instead of at infinity, which is what makes a floor read as a floor and not a mirror ball.
            probe.boxProjection = true;
            Vector3 roomCentre = new Vector3(RoomWidth * 0.5f, floorY + ClearHeight * 0.5f, -RoomDepth * 0.5f);
            probe.center = roomCentre - go.transform.localPosition;
            probe.size = new Vector3(RoomWidth, ClearHeight, RoomDepth) + Vector3.one * (2f * BoxMargin);
            probe.blendDistance = BlendDistance;
            probe.resolution = Resolution;
            probe.hdr = true;
            return probe;
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
