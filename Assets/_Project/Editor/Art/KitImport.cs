// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-11
//  Prompt    : "do the Unity import check on the kit, and open the scene"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Brings the office snap kit from ArtSource/Kit/export into the project in one
//              repeatable step: import settings, URP materials, prefabs with colliders, a test
//              scene laid out by the snap convention, and the same seam ray checks that
//              ArtSource/Kit/assembly_test.py runs in Blender, here against Unity's own colliders.
//              It is its own file because it is art pipeline, not greybox generation.
//  Modified  : Claude Opus 5.5, 2026-11-12 — the approved office direction's pieces and materials:
//              cobalt/portal walls, stair steps, locked door, strip light; Trim_Navy, Accent_Cobalt,
//              Light_Strip (emissive), Door_LockedRed; carpet and ceiling tile textures; Stair_Flight
//              keeps its collider but no renderer; visual-only pieces need no collider.
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Imports the office snap kit, checks it and builds <c>KitTest.unity</c>. Menu:
    /// <c>Close the Deal &gt; Art &gt; Import Kit and Build Test Scene</c>. Shell use (with graphics, so not -nographics):
    ///   Unity.exe -batchmode -quit -projectPath &lt;repo&gt; -executeMethod CloseTheDeal.Editor.Art.KitImport.RunFromCommandLine -kitOut &lt;folder&gt;
    /// </summary>
    /// <remarks>
    /// Re-running overwrites the kit's models, materials, prefabs and test scene; it touches nothing
    /// else. The source of truth is ArtSource/Kit (kit_pieces.py); rebuild and re-export there first.
    /// </remarks>
    public static class KitImport
    {
        /// <summary>Kit export folder, relative to the repo root (ArtSource/Kit/build_all.sh writes it).</summary>
        const string ExportFolder = "ArtSource/Kit/export";
        const string ModelFolder = "Assets/_Project/Art/Kit/Models";
        const string MaterialFolder = "Assets/_Project/Art/Kit/Materials";
        const string PrefabFolder = "Assets/_Project/Art/Kit/Prefabs";
        /// <summary>Tile textures drawn by ArtSource/Kit/textures/make_textures.py, relative to the repo root.</summary>
        const string TextureSource = "ArtSource/Kit/textures";
        const string TextureFolder = "Assets/_Project/Art/Kit/Textures";
        /// <summary>Light_Strip emission, HDR multiplier on white. WHY: bright enough to bloom slightly under the
        /// office look's exposure without blowing the ceiling out; tune by eye in KitTest.</summary>
        const float LightStripEmission = 2f;
        const string ScenePath = "Assets/_Project/Scenes/KitTest.unity";

        /// <summary>Floor to floor, metres. Kit FLOOR_H and DefaultTower.asset FloorHeight.</summary>
        const float FloorHeight = 4.5f;
        /// <summary>Floor to slab soffit, metres: floor to floor less the 0.2 m slab.</summary>
        const float ClearHeight = 4.3f;
        /// <summary>Ray spacing along seams, metres. Same as assembly_test.py: finer than any gap a player would notice.</summary>
        const float SeamStep = 0.02f;
        /// <summary>Allowed difference between the Blender manifest size and the imported mesh, metres.</summary>
        const float SizeTolerance = 0.001f;
        /// <summary>Kit cage: 12 panels every 30°, first at 15°, open from 300° (the stair exit).</summary>
        const int CagePanels = 12;
        const float CageFirst = 15f, CageStep = 30f, CageOpenFrom = 300f;
        /// <summary>Stairwell module pivot in the test scene; 20 m clear of the corner test, as in assembly_test.py.</summary>
        static readonly Vector3 StairModule = new Vector3(20f, 0f, 0f);
        /// <summary>Stair centre in the stairwell slab's local frame (kit README).</summary>
        static readonly Vector3 StairCentreLocal = new Vector3(6f, 0f, -4f);
        /// <summary>Tread sample radius, metres: mid-flight between column (0.8) and outer edge (2.6).</summary>
        const float TreadRadius = 1.7f;
        /// <summary>Glass opacity. The Greybox README's read test recommends 5 % for players to read through.</summary>
        const float GlassAlpha = 0.05f;
        /// <summary>Capture size for the review renders, pixels.</summary>
        const int RenderWidth = 1280, RenderHeight = 720;

        static readonly string[] Pieces =
        {
            "Slab_4x4", "Slab_2x4", "Slab_4x2", "Slab_2x2", "Slab_Stairwell", "Wall_Solid_4m", "Wall_Solid_2m",
            "Wall_Door_4m", "Door_Leaf", "Window_4m", "Window_2m", "Column_Facade", "Column_CornerL",
            "Column_CornerR", "Partition_Glass_4m", "Partition_Glass_2m", "Stair_Flight", "Stair_Column", "Stair_CagePanel",
            "Wall_Solid_4m_Cobalt", "Wall_Solid_2m_Cobalt", "Wall_Solid_4m_PortalHead", "Wall_Door_4m_PortalHead",
            "Stair_Steps", "Stair_LockedDoor", "Ceiling_LightStrip",
        };

        /// <summary>Pieces that are seen but never touched, so they have no collider (kit README).</summary>
        static readonly HashSet<string> VisualOnly = new HashSet<string> { "Stair_Steps", "Ceiling_LightStrip" };

        /// <summary>Pieces whose mesh is only a collider: the stair ramp is walked on, Stair_Steps is what is seen.</summary>
        static readonly HashSet<string> ColliderOnly = new HashSet<string> { "Stair_Flight" };

        /// <summary>Materials that take a tile texture from <see cref="TextureSource"/> (file name = material name).</summary>
        static readonly HashSet<string> Textured = new HashSet<string> { "Floor_Carpet", "Ceiling_Tile" };

        /// <summary>Pieces with no collider mesh whose render mesh is the collider, not convex (kit README).</summary>
        static readonly HashSet<string> RenderMeshColliders = new HashSet<string> { "Slab_Stairwell", "Stair_Flight" };

        /// <summary>Kit materials: colour (art bible hex, as in kit_pieces.py), metallic, smoothness (1 − Blender roughness).</summary>
        static readonly (string name, string hex, float metallic, float smoothness, bool glass)[] Materials =
        {
            ("Floor_Carpet", "646E86", 0f, 0.1f, false),
            ("Ceiling_Tile", "F3F4F4", 0f, 0.1f, false),
            ("Wall_PaperWhite", "F3F4F4", 0f, 0.3f, false),
            ("Frame_Aluminium", "C9CED3", 0.8f, 0.65f, false),
            ("Glass_Clear", "B3D9F2", 0f, 0.95f, true),
            ("Door_Leaf", "C9CED3", 0f, 0.4f, false),
            ("Stair_Aluminium", "C9CED3", 0.6f, 0.65f, false),
            ("Trim_Navy", "111B38", 0f, 0.5f, false),
            ("Accent_Cobalt", "244FB9", 0f, 0.5f, false),
            ("Light_Strip", "FFFFFF", 0f, 0.5f, false),
            ("Door_LockedRed", "C0281E", 0f, 0.55f, false),
        };

        [MenuItem("Close the Deal/Art/Import Kit and Build Test Scene")]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Run(Path.Combine(Path.GetTempPath(), "CloseTheDealKit"));
        }

        /// <summary>Batch entry point; reads <c>-kitOut &lt;folder&gt;</c> for the report and renders and exits 1 on any failure.</summary>
        public static void RunFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string folder = Path.Combine(Path.GetTempPath(), "CloseTheDealKit");
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-kitOut") folder = args[i + 1];
            bool ok = Run(folder);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Does the whole import and check. Returns true when every check passed.</summary>
        /// <param name="outFolder">Where the report and PNG renders go (outside the project).</param>
        static bool Run(string outFolder)
        {
            Directory.CreateDirectory(outFolder);
            var report = new StringBuilder();
            bool ok = true;

            Dictionary<string, Material> mats = BuildMaterials();
            foreach (string piece in Pieces) ok &= ImportModel(piece, mats, report);
            AssetDatabase.SaveAssets();
            foreach (string piece in Pieces) ok &= BuildPrefab(piece, report);
            AssetDatabase.SaveAssets();

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            BuildTestScene();
            Physics.SyncTransforms();
            ok &= RunSeamChecks(report);
            Render(outFolder, report);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);

            report.AppendLine(ok ? "KIT IMPORT PASSED" : "KIT IMPORT FAILED");
            string reportPath = Path.Combine(outFolder, "kit_import_report.txt");
            File.WriteAllText(reportPath, report.ToString());
            Debug.Log(report.ToString());
            return ok;
        }

        // ------------------------------------------------------------------ materials

        static Dictionary<string, Material> BuildMaterials()
        {
            Directory.CreateDirectory(MaterialFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var result = new Dictionary<string, Material>();
            foreach (var m in Materials)
            {
                string path = $"{MaterialFolder}/{m.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(lit) { name = m.name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                ColorUtility.TryParseHtmlString("#" + m.hex, out Color c);
                if (m.glass)
                {
                    c.a = GlassAlpha;
                    MakeTransparent(mat);
                }
                if (Textured.Contains(m.name))
                {
                    // WHY white: the texture already holds the final colour; a tint would darken it twice.
                    mat.SetTexture("_BaseMap", ImportTexture(m.name));
                    c = Color.white;
                }
                if (m.name == "Light_Strip")
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.white * LightStripEmission);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                mat.SetColor("_BaseColor", c);
                mat.SetFloat("_Metallic", m.metallic);
                mat.SetFloat("_Smoothness", m.smoothness);
                EditorUtility.SetDirty(mat);
                result[m.name] = mat;
            }
            return result;
        }

        /// <summary>Copies a tile texture in from ArtSource and returns it; repeat wrap so it tiles across pieces.</summary>
        static Texture2D ImportTexture(string name)
        {
            Directory.CreateDirectory(TextureFolder);
            string dst = $"{TextureFolder}/{name}.png";
            File.Copy(Path.Combine(TextureSource, name + ".png"), dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(dst);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        /// <summary>URP Lit transparent, alpha blended, no depth write: the settings the material inspector applies.</summary>
        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }

        // ------------------------------------------------------------------ models

        /// <summary>Copies one FBX in, applies the skill's import settings, remaps materials and checks the meshes.</summary>
        static bool ImportModel(string piece, Dictionary<string, Material> mats, StringBuilder report)
        {
            string src = Path.Combine(ExportFolder, piece, piece + ".fbx");
            string manifest = Path.Combine(ExportFolder, piece, "manifest.json");
            if (!File.Exists(src))
            {
                report.AppendLine($"FAIL  {piece}: no export at {src}; run ArtSource/Kit/build_all.sh");
                return false;
            }
            Directory.CreateDirectory(ModelFolder);
            string dst = $"{ModelFolder}/{piece}.fbx";
            File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);

            var importer = (ModelImporter)AssetImporter.GetAtPath(dst);
            // WHY: the export already baked units and axes into the mesh (blender-modeling unity-import.md).
            importer.globalScale = 1f;
            importer.useFileUnits = true;
            importer.bakeAxisConversion = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.isReadable = false;
            foreach (var m in mats)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.Key), m.Value);
            importer.SaveAndReimport();

            bool ok = true;
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(dst);
            Dictionary<string, Vector3> expected = ReadManifestSizes(manifest);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root.transform && (t.localRotation != Quaternion.identity || t.localScale != Vector3.one))
                {
                    report.AppendLine($"FAIL  {piece}/{t.name}: rotation {t.localEulerAngles} scale {t.localScale}, expected 0 and 1");
                    ok = false;
                }
                var mf = t.GetComponent<MeshFilter>();
                if (mf == null || !expected.TryGetValue(t.name, out Vector3 size)) continue;
                Vector3 got = mf.sharedMesh.bounds.size;
                if (Vector3.Distance(got, size) > SizeTolerance)
                {
                    report.AppendLine($"FAIL  {piece}/{t.name}: size {F(got)}, manifest {F(size)}");
                    ok = false;
                }
                var mr = t.GetComponent<MeshRenderer>();
                if (mr != null && !t.name.EndsWith("_Collider"))
                    foreach (Material sm in mr.sharedMaterials)
                        if (sm == null || !mats.ContainsValue(sm))
                        {
                            report.AppendLine($"FAIL  {piece}/{t.name}: material {(sm ? sm.name : "none")} not remapped to a kit material");
                            ok = false;
                        }
            }
            if (expected.Count == 0)
            {
                report.AppendLine($"FAIL  {piece}: manifest has no sizes");
                ok = false;
            }
            if (ok) report.AppendLine($"ok    {piece}: rotation 0, scale 1, {expected.Count} meshes match the manifest to 1 mm, materials remapped");
            return ok;
        }

        /// <summary>Object sizes from the export manifest, converted from Blender (x, depth, height) to Unity (x, height, depth).</summary>
        static Dictionary<string, Vector3> ReadManifestSizes(string path)
        {
            var sizes = new Dictionary<string, Vector3>();
            if (!File.Exists(path)) return sizes;
            const string num = @"([-\d.eE+]+)";
            var rx = new Regex("\"(Kit_[A-Za-z0-9_]+)\":\\s*\\{\\s*\"size_m\":\\s*\\[\\s*" + num + ",\\s*" + num + ",\\s*" + num);
            foreach (Match m in rx.Matches(File.ReadAllText(path)))
            {
                float x = P(m.Groups[2].Value), y = P(m.Groups[3].Value), z = P(m.Groups[4].Value);
                sizes[m.Groups[1].Value] = new Vector3(x, z, y);
            }
            return sizes;
        }

        // ------------------------------------------------------------------ prefabs

        /// <summary>Prefab per piece: <c>*_Collider</c> meshes become colliders with no renderer.</summary>
        /// <remarks>
        /// A 12-triangle collider mesh becomes a BoxCollider fitted to it (cheaper and exact for the kit's
        /// boxes); anything else a convex MeshCollider, as unity-import.md says. The alternative, convex
        /// MeshColliders everywhere, works too but costs more per contact for no gain on boxes.
        /// </remarks>
        static bool BuildPrefab(string piece, StringBuilder report)
        {
            const int BoxIndexCount = 36;   // 12 triangles: a box
            Directory.CreateDirectory(PrefabFolder);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{piece}.fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            go.name = "Kit_" + piece;
            int colliders = 0;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                GameObject child = mf.gameObject;
                Mesh mesh = mf.sharedMesh;
                if (child.name.EndsWith("_Collider"))
                {
                    if (mesh.GetIndexCount(0) == BoxIndexCount && mesh.subMeshCount == 1)
                    {
                        var box = child.AddComponent<BoxCollider>();
                        box.center = mesh.bounds.center;
                        box.size = mesh.bounds.size;
                    }
                    else
                    {
                        child.AddComponent<MeshCollider>().sharedMesh = mesh;
                        child.GetComponent<MeshCollider>().convex = true;
                    }
                    Object.DestroyImmediate(child.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(mf);
                    colliders++;
                }
                else if (RenderMeshColliders.Contains(piece))
                {
                    child.AddComponent<MeshCollider>().sharedMesh = mesh;
                    colliders++;
                    if (ColliderOnly.Contains(piece)) Object.DestroyImmediate(child.GetComponent<MeshRenderer>());
                }
            }
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{piece}.prefab");
            Object.DestroyImmediate(go);
            if (colliders == 0 && !VisualOnly.Contains(piece))
            {
                report.AppendLine($"FAIL  {piece}: prefab has no collider");
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ test scene

        static GameObject Place(Transform parent, string piece, float x, float y, float z, float yaw = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{piece}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        /// <summary>Same layout as assembly_test.py, placed by grid positions and 90° turns only.</summary>
        static void BuildTestScene()
        {
            var corner = new GameObject("Facade Corner").transform;
            for (float x = 0f; x <= 4f; x += 4f)
            {
                Place(corner, "Slab_4x2", x, 0f, 0f);
                Place(corner, "Slab_4x4", x, 0f, -2f);
                Place(corner, "Slab_4x2", x, FloorHeight, 0f);   // the floor above: its soffit is this room's ceiling
                Place(corner, "Slab_4x4", x, FloorHeight, -2f);
            }
            Place(corner, "Column_CornerL", 0f, 0f, 0f);
            Place(corner, "Column_Facade", 4f, 0f, 0f);
            Place(corner, "Column_CornerR", 8f, 0f, 0f);
            Place(corner, "Window_4m", 0f, 0f, 0f);
            Place(corner, "Window_4m", 4f, 0f, 0f);
            // A side wall running toward -Z: pivot at its far end, turned -90 (kit README).
            Place(corner, "Wall_Solid_2m", 0f, 0f, -2f, -90f);
            Place(corner, "Wall_Solid_4m", 0f, 0f, -6f, -90f);
            Place(corner, "Wall_Door_4m", 0f, 0f, -6f);
            const float doorLeafX = 1.41f, doorLeafZ = -6.1f;   // kit README: leaf hinge in a door wall
            Place(corner, "Door_Leaf", doorLeafX, 0f, doorLeafZ);
            Place(corner, "Partition_Glass_4m", 4f, 0f, -6f);

            var stair = new GameObject("Stairwell").transform;
            Vector3 c = StairModule + StairCentreLocal;
            for (int level = 0; level < 2; level++)
            {
                float y = level * FloorHeight;
                Place(stair, "Slab_Stairwell", StairModule.x, y, StairModule.z);
                Place(stair, "Stair_Flight", c.x, y, c.z);
                Place(stair, "Stair_Steps", c.x, y, c.z);
                Place(stair, "Stair_Column", c.x, y, c.z);
                for (int k = 0; k < CagePanels; k++)
                {
                    float a = CageFirst + CageStep * k;
                    if (a < CageOpenFrom) Place(stair, "Stair_CagePanel", c.x, y, c.z, a);
                }
            }
        }

        // ------------------------------------------------------------------ seam checks (Unity colliders)

        static bool Hit(Vector3 origin, Vector3 dir, float maxDistance, out Vector3 point)
        {
            bool ok = Physics.Raycast(origin, dir.normalized, out RaycastHit h, maxDistance);
            point = h.point;
            return ok;
        }

        static IEnumerable<float> Range(float a, float b, float step)
        {
            int n = Mathf.RoundToInt((b - a) / step);
            for (int i = 0; i <= n; i++) yield return a + i * step;
        }

        /// <summary>The nine checks from assembly_test.py, against colliders instead of render meshes.</summary>
        static bool RunSeamChecks(StringBuilder report)
        {
            const float rayLength = 5f, outside = -1f, gridSample = 0.1f, inset = 0.25f;
            bool allOk = true;
            void Check(string name, List<Vector3> origins, System.Func<Vector3, bool> test)
            {
                var gaps = origins.FindAll(o => !test(o));
                report.AppendLine($"{(gaps.Count == 0 ? "PASS" : "FAIL")}  {name}: {origins.Count - gaps.Count}/{origins.Count} rays as expected");
                for (int i = 0; i < Mathf.Min(gaps.Count, 5); i++) report.AppendLine($"GAP   {name} at {F(gaps[i])}");
                allOk &= gaps.Count == 0;
            }

            var facade = new List<Vector3>();
            foreach (float x in Range(0f, 8f, SeamStep)) facade.Add(new Vector3(x, 0.5f, 1f));
            Check("facade sill solid", facade, o => Hit(o, Vector3.back, rayLength, out var p) && p.z >= -0.22f && p.z <= 0.03f);

            var side = new List<Vector3>();
            foreach (float z in Range(-6f, 0f, SeamStep))
                foreach (float y in new[] { 0.5f, 2f, 4.2f }) side.Add(new Vector3(outside, y, z));
            Check("side wall solid", side, o => Hit(o, Vector3.right, rayLength, out var p) && p.x >= -0.03f && p.x <= 0.21f);

            var diag = new List<Vector3> { new Vector3(outside, 0.5f, 1f), new Vector3(outside, 2f, 1f), new Vector3(outside, 4.2f, 1f) };
            Check("corner closed", diag, o => Hit(o, new Vector3(1f, 0f, -1f), 3f, out _));

            var grid = new List<Vector3>();
            foreach (float x in Range(inset, 8f - inset, gridSample))
                foreach (float z in Range(-6f + inset, -inset, gridSample)) grid.Add(new Vector3(x, 1f, z));
            Check("floor continuous", grid, o => Hit(o, Vector3.down, rayLength, out var p) && Mathf.Abs(p.y) < SizeTolerance);
            Check("ceiling at clear height", grid, o => Hit(o, Vector3.up, rayLength, out var p) && Mathf.Abs(p.y - ClearHeight) < SizeTolerance);

            var soffit = new List<Vector3>();
            foreach (float z in Range(-5.9f, -0.1f, SeamStep)) soffit.Add(new Vector3(outside, ClearHeight - 0.005f, z));
            Check("wall meets soffit", soffit, o => Hit(o, Vector3.right, rayLength, out _));

            var head = new List<Vector3>();
            foreach (float x in Range(1.45f, 2.55f, SeamStep)) head.Add(new Vector3(x, 2.6f, -5f));
            Check("door head closed above opening", head, o => Hit(o, Vector3.back, 2f, out _));

            Vector3 c = StairModule + StairCentreLocal;
            var treads = new List<Vector3>();
            foreach (float a in Range(5f, 355f, 5f))
            {
                float r = a * Mathf.Deg2Rad, h = FloorHeight * a / 360f;
                treads.Add(new Vector3(c.x + Mathf.Sin(r) * TreadRadius, h + 1f, c.z + Mathf.Cos(r) * TreadRadius));
            }
            Check("flight rises one floor per turn", treads, o =>
            {
                float expectedY = o.y - 1f;
                return Hit(o, Vector3.down, 2f, out var p) && Mathf.Abs(p.y - expectedY) < 0.05f;
            });

            var arrive = new List<Vector3> { new Vector3(c.x - TreadRadius * Mathf.Sin(Mathf.Deg2Rad), FloorHeight + 0.5f, c.z + TreadRadius) };
            Check("flight arrives flush with next floor", arrive, o => Hit(o, Vector3.down, 1f, out var p) && Mathf.Abs(p.y - FloorHeight) < 0.05f);
            return allOk;
        }

        // ------------------------------------------------------------------ renders

        /// <summary>Same three views as assembly_test.py, through the scene's camera, then leaves the camera on the inside view.</summary>
        static void Render(string folder, StringBuilder report)
        {
            Camera cam = Camera.main;
            const float fov = 60f;
            cam.fieldOfView = fov;
            var views = new (string name, Vector3 eye, Vector3 target)[]
            {
                ("corner_outside", new Vector3(-5f, 3f, 6f), new Vector3(3f, 2f, -2f)),
                ("stair_inside", StairModule + new Vector3(1f, 1.6f, -1f), StairModule + new Vector3(6f, 1.5f, -4f)),
                ("corner_inside", new Vector3(7f, 1.6f, -5f), new Vector3(0f, 1.6f, 0f)),
            };
            var rt = new RenderTexture(RenderWidth, RenderHeight, 24);
            var image = new Texture2D(RenderWidth, RenderHeight, TextureFormat.RGB24, false);
            foreach (var v in views)
            {
                cam.transform.SetPositionAndRotation(v.eye, Quaternion.LookRotation(v.target - v.eye));
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, RenderWidth, RenderHeight), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                string path = Path.Combine(folder, $"unity_{v.name}.png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                report.AppendLine($"wrote {path}");
            }
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(image);
        }

        static float P(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        static string F(Vector3 v) => v.ToString("F3");
    }
}
