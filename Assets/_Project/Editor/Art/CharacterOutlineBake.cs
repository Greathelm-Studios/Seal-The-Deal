using System.Collections.Generic;
using System.IO;
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Bakes smoothed tangent-space contour normals into UV3 for selected character models.</summary>
    public static class CharacterOutlineBake
    {
        /// <summary>UV channel holding smoothed outline normals (TEXCOORD3).</summary>
        public const int Channel = 3;

        const string SubFolder = "Outline";
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

        /// <summary>Bakes every mesh in the model at <paramref name="modelPath"/>.</summary>
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

                // Update in place to preserve the baked mesh GUID referenced by prefabs.
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

        /// <summary>Returns the baked mesh for a source mesh, or null if not yet baked.</summary>
        public static Mesh FindBaked(string modelPath, string sourceMeshName)
        {
            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<Mesh>($"{folder}/{SubFolder}/{sourceMeshName}_Outline.asset");
        }

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
                if (smooth.sqrMagnitude < 0.5f)
                    smooth = normals[i];

                Vector3 normal = normals[i];
                // Without tangents, fall back to (0,0,1) so the shader uses the vertex's own normal.
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
