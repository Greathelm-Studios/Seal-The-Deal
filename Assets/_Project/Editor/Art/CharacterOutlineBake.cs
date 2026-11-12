// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex: "Yes, fix the hair" (the character's contour line broke into shards on
//              hard-edged parts of the mesh)
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Bakes a second, smoothed set of normals into a copy of a character's mesh, for the
//              Character Toon shader's contour to push along. It is its own tool because the fix
//              belongs to the mesh, not to any material: the contour is drawn by pushing the
//              surface outward along its normals, and wherever the artist's normals are split
//              (a hard edge), the pushed surface splits too and the line breaks open.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.IO;
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Bakes contour normals for the selected model(s). Menu:
    /// <c>Close the Deal &gt; Art &gt; Bake Character Outline Normals</c>.
    /// </summary>
    /// <remarks>
    /// For each mesh in a selected model it writes <c>&lt;model folder&gt;/Outline/&lt;mesh&gt;_Outline.asset</c>:
    /// the same mesh, with one extra channel (UV3) holding a smoothed normal per vertex. The model
    /// file and its own normals are untouched, so shading still has its hard edges; only the
    /// contour uses the smoothed set. Point the character's MeshFilter or SkinnedMeshRenderer at
    /// the baked mesh to use it. Re-run after the model is re-exported.
    /// The smoothed normal is stored relative to the vertex's own normal and tangent (tangent
    /// space), not as a fixed direction. WHY: when the character is rigged later, normals and
    /// tangents bend with the bones but UV channels do not; a fixed direction would point the
    /// wrong way as soon as a limb moved.
    /// Alternative considered: an import step that patches every character model automatically.
    /// Rejected because editor generation here runs only from a menu (AGENTS.md §4).
    /// </remarks>
    public static class CharacterOutlineBake
    {
        /// <summary>The UV channel the smoothed normal goes in; the shader reads the same one (TEXCOORD3).</summary>
        public const int Channel = 3;

        const string SubFolder = "Outline";

        /// <summary>
        /// Vertices closer together than this, metres in the model's own units, count as the same
        /// point and share one smoothed normal. WHY 0.00001: a hard edge is two vertices at exactly
        /// the same place, so this only needs to absorb floating-point noise from the exporter.
        /// </summary>
        const float WeldDistance = 0.00001f;

        [MenuItem("Close the Deal/Art/Bake Character Outline Normals")]
        static void BakeSelected()
        {
            int models = 0;
            foreach (Object selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (AssetImporter.GetAtPath(path) is ModelImporter)
                {
                    Debug.Log(Bake(path));
                    models++;
                }
            }

            if (models == 0)
                Debug.LogWarning("[Outline Bake] Select one or more model files (for example an .fbx) in the Project window first.");
        }

        /// <summary>
        /// Bakes every mesh in the model at <paramref name="modelPath"/>.
        /// </summary>
        /// <param name="modelPath">Project path of a model file, e.g. <c>Assets/.../STDMC.fbx</c>.</param>
        /// <returns>A one-line report of what was written and what was left alone.</returns>
        public static string Bake(string modelPath)
        {
            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            string outFolder = folder + "/" + SubFolder;
            if (!AssetDatabase.IsValidFolder(outFolder))
                AssetDatabase.CreateFolder(folder, SubFolder);

            int written = 0, skipped = 0;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                if (asset is not Mesh source)
                    continue;

                string outPath = $"{outFolder}/{source.name}_Outline.asset";
                if (!GeneratedAssetGuard.MayOverwrite(outPath))
                {
                    skipped++;
                    continue;
                }

                // WHY update in place: a new asset would have a new GUID and every prefab using
                // the baked mesh would lose it on each re-bake.
                var baked = AssetDatabase.LoadAssetAtPath<Mesh>(outPath);
                if (baked == null)
                {
                    baked = Object.Instantiate(source);
                    baked.name = source.name + "_Outline";
                    AssetDatabase.CreateAsset(baked, outPath);
                }
                else
                {
                    EditorUtility.CopySerialized(source, baked);
                    baked.name = source.name + "_Outline";
                }

                baked.SetUVs(Channel, SmoothedInTangentSpace(source));
                EditorUtility.SetDirty(baked);
                GeneratedAssetGuard.MarkGenerated(baked);
                written++;
            }

            return $"[Outline Bake] {modelPath}: wrote {written} mesh(es) to {outFolder}, left {skipped} alone (edited by hand).";
        }

        /// <summary>The baked mesh for a source mesh, or null if it has not been baked.</summary>
        /// <param name="modelPath">Project path of the model the mesh came from.</param>
        /// <param name="sourceMeshName">The mesh's name inside the model.</param>
        public static Mesh FindBaked(string modelPath, string sourceMeshName)
        {
            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<Mesh>($"{folder}/{SubFolder}/{sourceMeshName}_Outline.asset");
        }

        /// <summary>
        /// One smoothed normal per vertex: the average of the normals of every vertex at the same
        /// position, expressed in that vertex's own tangent space.
        /// </summary>
        static List<Vector3> SmoothedInTangentSpace(Mesh mesh)
        {
            Vector3[] positions = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            bool hasTangents = tangents.Length == positions.Length;

            var sums = new Dictionary<Vector3Int, Vector3>(positions.Length);
            var keys = new Vector3Int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                keys[i] = Vector3Int.RoundToInt(positions[i] / WeldDistance);
                sums.TryGetValue(keys[i], out Vector3 sum);
                sums[keys[i]] = sum + normals[i];
            }

            var result = new List<Vector3>(positions.Length);
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 smooth = sums[keys[i]].normalized;
                // Opposite normals at one point cancel out; fall back to the vertex's own.
                if (smooth.sqrMagnitude < 0.5f)
                    smooth = normals[i];

                Vector3 normal = normals[i];
                // A mesh with no tangents gets an arbitrary frame; the shader then rebuilds the
                // same arbitrary frame from the same zero tangent, so write the "use my own
                // normal" value instead and let it fall back.
                if (!hasTangents)
                {
                    result.Add(new Vector3(0f, 0f, 1f));
                    continue;
                }

                Vector3 tangent = tangents[i];
                Vector3 bitangent = Vector3.Cross(normal, tangent) * tangents[i].w;
                result.Add(new Vector3(Vector3.Dot(smooth, tangent), Vector3.Dot(smooth, bitangent), Vector3.Dot(smooth, normal)));
            }

            return result;
        }
    }
}
