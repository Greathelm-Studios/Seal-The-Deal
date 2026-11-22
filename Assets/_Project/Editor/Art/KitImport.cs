using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Imports the office snap kit, verifies colliders/seams, and builds KitTest.unity.</summary>
    public static class KitImport
    {
        const string ExportFolder = "ArtSource/Kit/export";
        const string ModelFolder = "Assets/_Project/Art/Kit/Models";
        const string MaterialFolder = "Assets/_Project/Art/Kit/Materials";
        const string PrefabFolder = "Assets/_Project/Art/Kit/Prefabs";
        const string TextureSource = "ArtSource/Kit/textures";
        const string TextureFolder = "Assets/_Project/Art/Kit/Textures";
        const string TrimMaterialName = "Trim_Aero";
        const string TrimSource = "ArtSource/Kit/trim/build/export";
        const string TrimStem = "TrimBoard_TrimBoard_Low_ID_Pearl_";
        const float TrimEmission = 1.5f;
        const string AeroPlasticShaderName = "Seal the Deal/Aero Plastic";
        const float LightStripEmission = 2f;
        const string ScenePath = "Assets/_Project/Scenes/KitTest.unity";

        const float FloorHeight = 4.5f;
        const float ClearHeight = 4.3f;
        const float SeamStep = 0.02f;
        const float SizeTolerance = 0.001f;
        const int CagePanels = 12;
        const float CageFirst = 15f, CageStep = 30f, CageOpenFrom = 300f;
        static readonly Vector3 StairModule = new Vector3(20f, 0f, 0f);
        static readonly Vector3 StairCentreLocal = new Vector3(6f, 0f, -4f);
        const float TreadRadius = 1.7f;
        const float GlassAlpha = 0.05f;
        const string GlassShaderName = "Seal the Deal/Glass Sheen";
        static readonly Color LouvreGlow = new Color(0.3f, 0.55f, 1.3f);
        const int RenderWidth = 1280, RenderHeight = 720;

        static readonly string[] Pieces =
        {
            "Slab_4x4", "Slab_2x4", "Slab_4x2", "Slab_2x2", "Slab_Stairwell", "Wall_Solid_4m", "Wall_Solid_2m",
            "Wall_Door_4m", "Door_Leaf", "Window_4m", "Window_2m", "Column_Facade", "Column_CornerL",
            "Column_CornerR", "Partition_Glass_4m", "Partition_Glass_2m", "Stair_Flight", "Stair_Column", "Stair_CagePanel",
            "Wall_Solid_4m_Cobalt", "Wall_Solid_2m_Cobalt", "Wall_Solid_4m_PortalHead", "Wall_Door_4m_PortalHead",
            "Stair_Steps", "Stair_LockedDoor", "Ceiling_LightStrip",
            "Wall_Return_2x2", "Wall_Return_2x2_Cobalt", "Wall_Niche_4m", "Counter_Capsule_2m", "Cover_Block_2m",
            "Partition_Frost_4m", "Partition_Frost_2m", "Stair_Straight_2m", "Ramp_2x4", "Guard_Glass_4m",
            "Guard_Glass_2m", "Slab_4x4_LightLine",
            "Sign_Zone_L", "Sign_Zone_R", "Sign_Zone_C", "Sign_Zone_H", "Sign_Zone_A", "Desk_Sculpt", "Chair_Pod",
            "Column_Graphic", "Column_Graphic_Tall", "Workstation_Bench", "Chair_Task", "Storage_Low", "Copier",
            "Desk_Reception", "Guard_Rail_4m", "Guard_Rail_2m", "Partition_Header_4m", "Partition_Header_2m", "Partition_Header_Curve_2x2",
            "Light_Pendant_Linear", "Carpet_Overlay_4x4", "Ceiling_Grid_4x4", "Planter_Box",
            "Water_Server", "Umbrella_Stand", "Vending_Machine", "Slipper_Cabinet", "Phone_Desk",
            "Doc_Tray", "Logo_Aonagi", "Sign_LED_Reception", "Water_Wall",
            "Monitor_Flat", "Keyboard", "Lift_Door_Frame_4m_1F", "Lift_Door_Frame_4m_RF", "Lift_Door_Panel",
            "Lift_Shaft_Wall_4m", "Lift_Car", "Pool_Raised_8x4",
            "Wall_Curtain_4m", "Ceiling_Beams_4x4",
            "Ribbon_Floor_16m", "Ribbon_Ceiling_16m", "Lightbox_Wall_Wave", "Lightbox_Wall_Logo", "Ceiling_Panel_4x4",
            "Partition_Fluted_4m", "Partition_Fluted_2m", "Partition_Louvre_4m", "Partition_Louvre_2m", "Pendant_Sphere",
            "Wall_Door_2m", "Partition_Glass_Door_2m", "Stair_Straight_Half_2m",
            "PC_Tower",
            "Lift_Shaft_Wall_4m_Solid", "Lift_Cap_4x4",
        };

        static readonly HashSet<string> VisualOnly = new HashSet<string>
        {
            "Stair_Steps", "Ceiling_LightStrip", "Sign_Zone_L", "Sign_Zone_R", "Sign_Zone_C", "Sign_Zone_H", "Sign_Zone_A",
            "Light_Pendant_Linear", "Carpet_Overlay_4x4", "Ceiling_Grid_4x4",
            "Logo_Aonagi", "Sign_LED_Reception", "Ceiling_Beams_4x4", "Ribbon_Floor_16m", "Ribbon_Ceiling_16m",
            "Ceiling_Panel_4x4", "Pendant_Sphere",
        };

        static readonly HashSet<string> ColliderOnly = new HashSet<string> { "Stair_Flight" };

        static readonly HashSet<string> Textured = new HashSet<string>
        {
            "Carpet_Dark", "Ceiling_Grid", "Floor_Carpet", "Glass_Fluted", "Glass_Louvre",
            "Wall_PaperWhite", "Trim_Navy", "Frame_Aluminium", "Ceiling_Tile",
        };

        /// <summary>Materials with make_surfaces.py normal and mask maps, and metres per map repeat (1 UV = 1 m).</summary>
        // Walls and soffit repeat at 2 m so the tone drift doesn't line up with the 1 m panel joints.
        static readonly Dictionary<string, float> Surfaces = new Dictionary<string, float>
        {
            { "Wall_PaperWhite", 2f }, { "Ceiling_Tile", 2f }, { "Trim_Navy", 1f }, { "Frame_Aluminium", 1f },
            { "Floor_Carpet", 1f }, { "Carpet_Dark", 1f },
        };

        static readonly HashSet<string> RenderMeshColliders = new HashSet<string> { "Slab_Stairwell", "Stair_Flight" };

        static readonly (string name, string hex, float metallic, float smoothness, bool glass)[] Materials =
        {
            ("Floor_Carpet", "DCE8EC", 0f, 0.96f, false),
            ("Ceiling_Tile", "F0F5F5", 0f, 0.1f, false),
            ("Wall_PaperWhite", "F4F8F8", 0f, 0.85f, false),
            ("Frame_Aluminium", "C8D2D9", 0.85f, 0.65f, false),
            ("Glass_Clear", "B3D9F2", 0f, 0.95f, true),
            ("Door_Leaf", "C9CED3", 0f, 0.4f, false),
            ("Stair_Aluminium", "C9CED3", 0.6f, 0.65f, false),
            ("Trim_Navy", "111B38", 0f, 0.5f, false),
            ("Accent_Cobalt", "244FB9", 0f, 0.94f, false),
            ("Light_Strip", "FFFFFF", 0f, 0.5f, false),
            ("Door_LockedRed", "C0281E", 0f, 0.55f, false),
            ("Glass_Frosted", "C6E9E6", 0f, 0.3f, false),
            ("Glass_Fluted", "DCEBF5", 0f, 0.6f, false),
            ("Glass_Louvre", "CFE6FF", 0f, 0.6f, false),
            ("Carpet_Dark", "2E3648", 0f, 0.06f, false),
            ("Ceiling_Grid", "F0F5F5", 0f, 0.1f, false),
            ("Plant_Leaf", "70AF59", 0f, 0.24f, false),
            ("Accent_Aqua", "45BFC7", 0f, 0.76f, false),
            ("Accent_Mist", "A8BFD1", 0f, 0.4f, false),
            ("Accent_Seal", "C7473E", 0f, 0.35f, false),
            ("Water_Wall", "7ADAD8", 0f, 0.9f, false),
            ("Pool_Water", "7ADAD8", 0f, 0.9f, false),
            (TrimMaterialName, "FFFFFF", 1f, 1f, false),
            (PropFrostName, "8FDCE0", 0f, 0.8f, false),
            (PaleFrostName, "DCEBF5", 0f, 0.75f, false),
        };

        const string PaleFrostName = "Plastic_Frost_Pale";
        const string PropFrostName = "Plastic_Aqua_Frost";
        const float PropFrostAlpha = 0.82f;
        const float FrostAlpha = 0.65f;
        const string WaterShaderName = "Seal the Deal/Aero Water Pane";

        /// <summary>Imports the kit and builds KitTest.unity from the Unity menu.</summary>
        [MenuItem("Seal the Deal/Art/Import Kit and Build Test Scene")]
        public static void RunFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Import Kit and Build Test Scene", "Stop Play Mode first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Run(Path.Combine(Path.GetTempPath(), "CloseTheDealKit"));
        }

        /// <summary>Batch entry point; reads <c>-kitOut &lt;folder&gt;</c> and exits 1 on failure.</summary>
        public static void RunFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string folder = Path.Combine(Path.GetTempPath(), "CloseTheDealKit");
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-kitOut") folder = args[i + 1];
            bool ok = Run(folder);
            EditorApplication.Exit(ok ? 0 : 1);
        }

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

        static Dictionary<string, Material> BuildMaterials()
        {
            Directory.CreateDirectory(MaterialFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var result = new Dictionary<string, Material>();
            foreach (var m in Materials)
            {
                string path = $"{MaterialFolder}/{m.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null && !GeneratedAssetGuard.MayOverwrite(path))
                {
                    result[m.name] = mat;
                    continue;
                }
                if (mat == null)
                {
                    mat = new Material(lit) { name = m.name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                ColorUtility.TryParseHtmlString("#" + m.hex, out Color c);
                if (m.glass)
                {
                    c.a = GlassAlpha;
                    Shader sheen = Shader.Find(GlassShaderName);
                    if (sheen != null)
                    {
                        mat.shader = sheen;
                        mat.renderQueue = -1;
                    }
                    else
                    {
                        Debug.LogWarning($"[Kit Import] Shader '{GlassShaderName}' not found; {m.name} falls back to transparent URP Lit.");
                        MakeTransparent(mat);
                    }
                }
                if (m.name == "Water_Wall" || m.name == "Pool_Water")
                {
                    // Return early so Lit colour/smoothness/texture settings do not overwrite custom water tuning.
                    Shader water = Shader.Find(WaterShaderName);
                    if (water != null)
                    {
                        mat.shader = water;
                        mat.renderQueue = -1;
                        EditorUtility.SetDirty(mat);
                        GeneratedAssetGuard.MarkGenerated(mat);
                        result[m.name] = mat;
                        continue;
                    }
                    Debug.LogWarning($"[Kit Import] Shader '{WaterShaderName}' not found; {m.name} falls back to transparent URP Lit.");
                    c.a = FrostAlpha;
                    MakeTransparent(mat);
                }
                if (m.name == "Glass_Fluted" || m.name == "Glass_Louvre") MakeTransparent(mat);
                if (m.name == "Glass_Frosted")
                {
                    c.a = FrostAlpha;
                    MakeTransparent(mat);
                }
                if (m.name == PropFrostName || m.name == PaleFrostName)
                {
                    c.a = PropFrostAlpha;
                    MakeTransparent(mat);
                }
                if (Textured.Contains(m.name))
                {
                    // Set base tint white so the texture is not multiplied twice.
                    mat.SetTexture("_BaseMap", ImportTexture(m.name));
                    c = Color.white;
                }
                else if (m.name == TrimMaterialName)
                {
                    ApplyTrimSheet(mat);
                }
                else if (!m.glass)
                {
                    mat.SetTexture("_BaseMap", null);
                }
                if (m.name == "Glass_Louvre")
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetTexture("_EmissionMap", ImportTexture(m.name));
                    mat.SetColor("_EmissionColor", LouvreGlow);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
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
                if (Surfaces.TryGetValue(m.name, out float repeat)) ApplySurface(mat, m.name, repeat);
                EditorUtility.SetDirty(mat);
                GeneratedAssetGuard.MarkGenerated(mat);
                result[m.name] = mat;
            }
            return result;
        }

        // URP Lit multiplies mask alpha by _Smoothness, so the slider is 1 and the mask holds absolute values.
        static void ApplySurface(Material mat, string name, float repeatMetres)
        {
            mat.SetTexture("_BumpMap", ImportMap(name + "_Normal", false, true));
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", ImportMap(name + "_Mask", false, false));
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Smoothness", 1f);
            mat.SetTextureScale("_BaseMap", Vector2.one / repeatMetres);
        }

        static Texture2D ImportMap(string file, bool srgb, bool normal)
        {
            Directory.CreateDirectory(TextureFolder);
            string dst = $"{TextureFolder}/{file}.png";
            File.Copy(Path.Combine(TextureSource, file + ".png"), dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(dst);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

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

        static void ApplyTrimSheet(Material mat)
        {
            Shader aero = Shader.Find(AeroPlasticShaderName);
            if (aero != null && mat.shader != aero)
                mat.shader = aero;
            else if (aero == null)
                Debug.LogWarning($"[Kit Import] Shader '{AeroPlasticShaderName}' not found; {mat.name} stays on URP Lit.");
            mat.SetTexture("_BaseMap", ImportTrimMap("AlbedoTransparency", true, false));
            mat.SetTexture("_MetallicGlossMap", ImportTrimMap("MetallicSmoothness", false, false));
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetTexture("_BumpMap", ImportTrimMap("Normal", false, true));
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_EmissionMap", ImportTrimMap("Emission", true, false));
            mat.SetColor("_EmissionColor", Color.white * TrimEmission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        static Texture2D ImportTrimMap(string map, bool srgb, bool normal)
        {
            Directory.CreateDirectory(TextureFolder);
            string dst = $"{TextureFolder}/{TrimMaterialName}_{map}.png";
            File.Copy(Path.Combine(TrimSource, TrimStem + map + ".png"), dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(dst);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

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
            if (!GeneratedAssetGuard.MayOverwrite(dst))
            {
                report.AppendLine($"SKIP  {piece}: {dst} was edited by hand.");
                return true;
            }
            File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);

            var importer = (ModelImporter)AssetImporter.GetAtPath(dst);
            // Exported FBX files already bake units and axes into the mesh.
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
            if (root != null) GeneratedAssetGuard.MarkGenerated(root);
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

        static bool IsAxisAlignedBox(Mesh mesh)
        {
            const float Epsilon = 0.0005f;
            Bounds b = mesh.bounds;
            foreach (Vector3 v in mesh.vertices)
                for (int axis = 0; axis < 3; axis++)
                    if (Mathf.Abs(v[axis] - b.min[axis]) > Epsilon && Mathf.Abs(v[axis] - b.max[axis]) > Epsilon)
                        return false;
            return true;
        }

        static bool BuildPrefab(string piece, StringBuilder report)
        {
            const int BoxIndexCount = 36;
            Directory.CreateDirectory(PrefabFolder);
            string prefabPath = $"{PrefabFolder}/{piece}.prefab";
            if (!GeneratedAssetGuard.MayOverwrite(prefabPath))
            {
                report.AppendLine($"SKIP  {piece}: {prefabPath} was edited by hand.");
                return true;
            }
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
                    if (mesh.GetIndexCount(0) == BoxIndexCount && mesh.subMeshCount == 1 && IsAxisAlignedBox(mesh))
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
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            GeneratedAssetGuard.MarkGenerated(prefab);
            if (colliders == 0 && !VisualOnly.Contains(piece))
            {
                report.AppendLine($"FAIL  {piece}: prefab has no collider");
                return false;
            }
            return true;
        }

        static GameObject Place(Transform parent, string piece, float x, float y, float z, float yaw = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{piece}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        static void BuildTestScene()
        {
            var corner = new GameObject("Facade Corner").transform;
            for (float x = 0f; x <= 4f; x += 4f)
            {
                Place(corner, "Slab_4x2", x, 0f, 0f);
                Place(corner, "Slab_4x4", x, 0f, -2f);
                Place(corner, "Slab_4x2", x, FloorHeight, 0f);
                Place(corner, "Slab_4x4", x, FloorHeight, -2f);
            }
            Place(corner, "Column_CornerL", 0f, 0f, 0f);
            Place(corner, "Column_Facade", 4f, 0f, 0f);
            Place(corner, "Column_CornerR", 8f, 0f, 0f);
            Place(corner, "Window_4m", 0f, 0f, 0f);
            Place(corner, "Window_4m", 4f, 0f, 0f);
            Place(corner, "Wall_Solid_2m", 0f, 0f, -2f, -90f);
            Place(corner, "Wall_Solid_4m", 0f, 0f, -6f, -90f);
            Place(corner, "Wall_Door_4m", 0f, 0f, -6f);
            const float doorLeafX = 1.41f, doorLeafZ = -6.1f;
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
