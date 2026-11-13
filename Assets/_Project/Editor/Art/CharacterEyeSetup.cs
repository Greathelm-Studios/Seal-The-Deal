// AI-GENERATED SOURCE FILE
// Generator : Codex (GPT-6)
// Generated : 2026-11-12
// Prompt    : Give the STDMC player character animated 2D eye cards.
// Reviewed  : NOT YET REVIEWED BY A HUMAN
// Summary   : Additive Prefab Mode tool that fits two cards to STDMC's baked bind-pose skin.
// Modified  : Claude Opus 5.5 (Claude Code), 2026-11-13 — cards 1.4x larger, lower and finer-fitted
//             to match Alex's concept sheet ("make them bigger"), then 1.25x again; closer together and
//             tilted 7 degrees, outer corners up, to match Alex's placement reference.
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Editor.Art
{
    internal static class CharacterEyeSetup
    {
        private const string Menu = "Close the Deal/Art/Add STDMC Eye Cards";
        private const string Character = "Assets/_Project/Art/Characters/STDMC/STDMC_Toon.prefab";
        private const string Skin = "Assets/_Project/Art/Characters/STDMC/Materials/STDMC_lambert2_Toon.mat";
        private const string Assets = "Assets/_Project/Art/Characters/Eyes/";
        private const string Profile = "Assets/_Project/Profiles/DefaultEyes.asset";
        private const string MaterialPath = Assets + "STDMC_Eyes.mat";
        private const string LeftCard = "Eye Card (Viewer Left)";
        private const string RightCard = "Eye Card (Viewer Right)";

        // WHY: the baked skin's front is +Z. Alex's concept sheet puts large eyes at the fringe's
        // lower edge (~1.58 m on this mesh) with strands across them. Alex then asked for them
        // higher than 1.595 m; the pupil sits 0.144 card heights (~10 mm) below the card centre
        // in the eye atlas, so this centre lands it at ~1.597 m, tucked further under the fringe.
        private const float CentreHeight = 1.607f;
        // WHY: ~1.7 eye widths between pupils, measured off Alex's placement reference, so the
        // inner corners sit just either side of the centre strand. Scaled with Width. The cards
        // overlap in the middle, which is fine: only their transparent margins do.
        private const float CentreSpacing = 0.0625f;
        // WHY: 1.75x the first 52 x 40 mm pass: 1.4x ("make them bigger"), then 1.25x more after
        // Alex saw them in game, where the fringe and distance shrink them. Keep
        // Width/Height = 52/40: CharacterEyeCard.shader corrects iris glance with that ratio.
        private const float Width = 0.091f;
        private const float Height = 0.070f;
        // WHY: Alex's placement reference draws each eye as a slash: lid and brow rise about
        // 7 degrees toward the outer corner, so the inner corners dip toward the nose.
        // Positive raises the outer corners; each card is mirrored.
        private const float TiltDegrees = 7f;
        // WHY: subdivisions follow the curved cheek without raising an entire plane into the hair.
        // At the 1.4x card size, 8 segments let the cheek bulge through between samples
        // (skin patches inside the whites, broken lid lines in the close-up render).
        private const int Segments = 16;
        // WHY: 1.5 mm still let the skin through at the inner and outer corners of the larger
        // cards; 3 mm clears it and doesn't read as floating in a three-quarter view.
        private const float SurfaceLift = 0.003f;
        private const float SmallestProjectedTriangle = 1e-10f;

        [MenuItem(Menu, true)]
        private static bool CanAdd() => !EditorApplication.isPlayingOrWillChangePlaymode &&
            PrefabStageUtility.GetCurrentPrefabStage()?.assetPath == Character;

        [MenuItem(Menu)]
        private static void AddInPrefabMode()
        {
            GameObject root = PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot;
            if (root.GetComponentInChildren<CharacterEyes>(true) != null)
            {
                Debug.Log("[CharacterEyes] STDMC already has eye cards. Existing setup was left alone.", root);
                return;
            }

            if (!EditorUtility.DisplayDialog("Add STDMC eye cards",
                    "Add two eye cards under the humanoid head and a driver on the prefab root?", "Add Cards", "Cancel"))
                return;

            if (AddCards(root, true))
                Debug.Log("[CharacterEyes] Added eye cards. Save STDMC_Toon in Prefab Mode to keep them.", root);
        }

        private static bool AddCards(GameObject root, bool recordUndo)
        {
            var avatar = root.GetComponentInChildren<Animator>(true);
            Transform head = avatar != null && avatar.isHuman ? avatar.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head == null)
            {
                Debug.LogError("[CharacterEyes] STDMC needs a valid humanoid head.", root);
                return false;
            }

            // WHY: an existing hierarchy may have been placed or edited by a person. Only add to a bare rig.
            if (root.GetComponentInChildren<CharacterEyes>(true) != null ||
                head.Find(LeftCard) != null || head.Find(RightCard) != null)
            {
                Debug.LogWarning("[CharacterEyes] Existing eye setup was left alone.", root);
                return false;
            }

            Mesh left = AssetDatabase.LoadAssetAtPath<Mesh>(Assets + "STDMC_EyeLeft.asset");
            Mesh right = AssetDatabase.LoadAssetAtPath<Mesh>(Assets + "STDMC_EyeRight.asset");
            if (left == null || right == null)
            {
                Material skin = AssetDatabase.LoadAssetAtPath<Material>(Skin);
                SkinnedMeshRenderer face = System.Array.Find(root.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                    renderer => skin != null && System.Array.IndexOf(renderer.sharedMaterials, skin) >= 0);
                if (face == null)
                {
                    Debug.LogError("[CharacterEyes] Could not locate STDMC's skin mesh.", root);
                    return false;
                }

                var baked = new Mesh();
                try
                {
                    face.BakeMesh(baked);
                    Vector3[] skinVertices = baked.vertices;
                    Matrix4x4 skinToRoot = root.transform.worldToLocalMatrix * face.transform.localToWorldMatrix;
                    for (int i = 0; i < skinVertices.Length; i++)
                        skinVertices[i] = skinToRoot.MultiplyPoint3x4(skinVertices[i]);

                    int[] triangles = baked.triangles;
                    left = FitMissingCard(left, Assets + "STDMC_EyeLeft.asset", CentreSpacing * 0.5f, 0, skinVertices, triangles);
                    right = FitMissingCard(right, Assets + "STDMC_EyeRight.asset", -CentreSpacing * 0.5f, 1, skinVertices, triangles);
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }
            }

            Material material = LoadMaterial();
            if (material == null || left == null || right == null)
                return false;

            EyeProfile profile = AssetDatabase.LoadAssetAtPath<EyeProfile>(Profile);
            if (profile == null)
            {
                if (!CanCreate(Profile))
                    return false;
                profile = ScriptableObject.CreateInstance<EyeProfile>();
                AssetDatabase.CreateAsset(profile, Profile);
                GeneratedAssetGuard.MarkGenerated(profile);
            }

            int undoGroup = Undo.GetCurrentGroup();
            if (recordUndo)
                Undo.SetCurrentGroupName("Add STDMC Eye Cards");

            MeshRenderer leftRenderer = PlaceCard(root.transform, head, LeftCard, CentreSpacing * 0.5f, left, material, recordUndo);
            MeshRenderer rightRenderer = PlaceCard(root.transform, head, RightCard, -CentreSpacing * 0.5f, right, material, recordUndo);
            CharacterEyes eyes = recordUndo ? Undo.AddComponent<CharacterEyes>(root) : root.AddComponent<CharacterEyes>();
            var serialized = new SerializedObject(eyes);
            serialized.FindProperty("_profile").objectReferenceValue = profile;
            SerializedProperty cards = serialized.FindProperty("_cards");
            cards.arraySize = 2;
            cards.GetArrayElementAtIndex(0).objectReferenceValue = leftRenderer;
            cards.GetArrayElementAtIndex(1).objectReferenceValue = rightRenderer;
            serialized.ApplyModifiedProperties();
            if (recordUndo)
                Undo.CollapseUndoOperations(undoGroup);

            return true;
        }

        private static bool CanCreate(string path)
        {
            // WHY: the generated assets are created once. An occupied path of any type belongs to its author.
            if (AssetDatabase.GetMainAssetTypeAtPath(path) == null && !System.IO.File.Exists(path))
                return true;

            Debug.LogWarning($"[CharacterEyes] Left {path} alone. The path is already occupied.");
            return false;
        }

        private static Material LoadMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;

            if (!CanCreate(MaterialPath))
                return null;

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Assets + "CharacterEyeCard.shader");
            Texture lids = AssetDatabase.LoadAssetAtPath<Texture>(Assets + "Placeholder_Lids.png");
            Texture openings = AssetDatabase.LoadAssetAtPath<Texture>(Assets + "Placeholder_Openings.png");
            Texture iris = AssetDatabase.LoadAssetAtPath<Texture>(Assets + "Placeholder_Iris.png");
            if (shader == null || lids == null || openings == null || iris == null)
            {
                Debug.LogError("[CharacterEyes] The eye shader and all three placeholder textures must be imported.");
                return null;
            }

            material = new Material(shader);
            material.SetTexture("_Lids", lids);
            material.SetTexture("_Openings", openings);
            material.SetTexture("_Iris", iris);
            AssetDatabase.CreateAsset(material, MaterialPath);
            GeneratedAssetGuard.MarkGenerated(material);
            return material;
        }

        private static MeshRenderer PlaceCard(Transform root, Transform head, string name, float centreX,
            Mesh mesh, Material material, bool recordUndo)
        {
            var card = new GameObject(name) { layer = GreyboxSceneSetup.PlayerLayer };
            card.transform.SetParent(head, false);
            card.transform.SetPositionAndRotation(root.TransformPoint(new Vector3(centreX, CentreHeight, 0f)), root.rotation);
            card.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = card.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (recordUndo)
                Undo.RegisterCreatedObjectUndo(card, "Add Eye Card");

            return renderer;
        }

        private static Mesh FitMissingCard(Mesh existing, string path, float centreX, int eye, Vector3[] skin, int[] indices)
        {
            if (existing != null || !CanCreate(path))
                return existing;

            Mesh mesh = FitCard(centreX, eye, skin, indices);
            if (mesh != null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                GeneratedAssetGuard.MarkGenerated(mesh);
            }

            return mesh;
        }

        private static Mesh FitCard(float centreX, int eye, Vector3[] skin, int[] indices)
        {
            const int stride = Segments + 1;
            var vertices = new Vector3[stride * stride];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var halves = new Vector2[vertices.Length];
            var triangles = new int[Segments * Segments * 6];
            // WHY: the outer corner of the card at +x is +x; mirror the turn so both outer corners rise.
            float tilt = Mathf.Sign(centreX) * TiltDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(tilt), sin = Mathf.Sin(tilt);
            for (int row = 0; row < stride; row++)
            for (int column = 0; column < stride; column++)
            {
                float u = (float)column / Segments;
                float v = (float)row / Segments;
                float flatX = (0.5f - u) * Width;
                float flatY = (v - 0.5f) * Height;
                // WHY: the grid is turned in the face plane before fitting, rather than rotating
                // the card's transform, so every vertex is still sampled onto the curved skin.
                float x = flatX * cos - flatY * sin;
                float y = flatX * sin + flatY * cos;
                // WHY: the transparent outer edge can reach past the cheek; use the closest
                // inner sample there so that the last strip still follows the skin's depth.
                float depth = FrontDepth(skin, indices, centreX + x, CentreHeight + y);
                for (int step = 1; float.IsNegativeInfinity(depth) && step <= Segments; step++)
                {
                    float inward = Mathf.Lerp(x, 0f, (float)step / Segments);
                    depth = FrontDepth(skin, indices, centreX + inward, CentreHeight + y);
                }

                if (float.IsNegativeInfinity(depth))
                {
                    Debug.LogError($"[CharacterEyes] No skin under the card footprint at height {CentreHeight + y:F3} m.");
                    return null;
                }

                int index = row * stride + column;
                vertices[index] = new Vector3(x, y, depth + SurfaceLift);
                normals[index] = Vector3.forward;
                uv[index] = new Vector2(u, v);
                halves[index] = new Vector2(eye, 0f);

                if (row == Segments || column == Segments)
                    continue;

                int triangle = (row * Segments + column) * 6;
                triangles[triangle] = index;
                triangles[triangle + 1] = index + stride;
                triangles[triangle + 2] = index + 1;
                triangles[triangle + 3] = index + 1;
                triangles[triangle + 4] = index + stride;
                triangles[triangle + 5] = index + stride + 1;
            }

            return new Mesh { vertices = vertices, normals = normals, uv = uv, uv2 = halves, triangles = triangles };
        }

        private static float FrontDepth(Vector3[] skin, int[] indices, float x, float y)
        {
            float front = float.NegativeInfinity;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = skin[indices[i]];
                Vector3 b = skin[indices[i + 1]];
                Vector3 c = skin[indices[i + 2]];
                float determinant = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(determinant) < SmallestProjectedTriangle)
                    continue;

                float u = ((b.y - c.y) * (x - c.x) + (c.x - b.x) * (y - c.y)) / determinant;
                float v = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / determinant;
                float w = 1f - u - v;
                if (u >= 0f && v >= 0f && w >= 0f)
                    front = Mathf.Max(front, u * a.z + v * b.z + w * c.z);
            }

            return front;
        }
    }
}
