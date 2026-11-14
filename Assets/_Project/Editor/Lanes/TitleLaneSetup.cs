using System.Collections.Generic;
using System.IO;
using CloseTheDeal.Editor.Art;
using CloseTheDeal.UI.Title;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>
    /// The title lane's editor tools.
    /// Close the Deal > Lanes > Set Up Title Screen builds Scenes/Lanes/Lane_Title.unity: a white window
    /// frame of kit pieces seen from inside and slightly below, dividing the game's own sky (Office Look)
    /// into panes, with the TitleBackdrop idle on it. It only replaces a scene this tool made (its root is
    /// named "Title backdrop"); a scene a person has made at that path is left alone.
    /// Close the Deal > Lanes > Capture Title Screen writes stills (1080p, 720p, 21:9, with the safe areas
    /// drawn) and transition frame strips to ArtSource/UI/passes/c_sky/title/.
    /// Design: docs/systems/UI_TITLE.md.
    /// </summary>
    public static class TitleLaneSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Lanes/Lane_Title.unity";
        const string KitPrefabs = "Assets/_Project/Art/Kit/Prefabs";
        const string RootName = "Title backdrop";
        const string CaptureFolder = "ArtSource/UI/passes/c_sky/title";
        const float Bay = 4f, FloorHeight = 4.5f;

        [MenuItem("Close the Deal/Lanes/Set Up Title Screen")]
        public static void SetUpFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Run();
        }

        /// <summary>Batch entry: -executeMethod CloseTheDeal.Editor.Lanes.TitleLaneSetup.SetUpFromCommandLine</summary>
        public static void SetUpFromCommandLine()
        {
            Run();
            EditorApplication.Exit(0);
        }

        static void Run()
        {
            if (File.Exists(ScenePath) && !MadeByThisTool())
            {
                Debug.LogWarning("[Title] Lane_Title.unity exists and was not made by this tool; left alone.");
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes/Lanes"))
                AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Lanes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(RootName).transform;
            Shell(root);

            // The camera pivot sits inside, low, looking a little up at the window wall.
            var rig = new GameObject("Camera rig").transform;
            rig.SetParent(root, false);
            rig.SetPositionAndRotation(new Vector3(6f, 0.55f, -8.6f), Quaternion.Euler(-9f, 0f, 0f));
            var camGo = new GameObject("Title Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(rig, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root, false);
            sunGo.transform.rotation = Quaternion.Euler(38f, 195f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;

            OfficeLook.Apply(askFirst: false);     // the game's own sky, grade and sun; no new look
            Light lit = RenderSettings.sun != null ? RenderSettings.sun : sun;

            var backdrop = root.gameObject.AddComponent<TitleBackdrop>();
            var so = new SerializedObject(backdrop);
            so.FindProperty("_rig").objectReferenceValue = rig;
            so.FindProperty("_sun").objectReferenceValue = lit;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Title] Set Up Title Screen finished: " + ScenePath);
        }

        static bool MadeByThisTool()
        {
            Scene open = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            bool ours = false;
            foreach (GameObject go in open.GetRootGameObjects())
                if (go.name == RootName) ours = true;
            EditorSceneManager.CloseScene(open, true);
            return ours;
        }

        static void Shell(Transform root)
        {
            Transform floor = Group(root, "Floor"), ceiling = Group(root, "Ceiling"), walls = Group(root, "Walls");
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Kit(floor, "Slab_4x4", i * Bay, 0f, -j * Bay);
                    if (j < 2) Kit(ceiling, "Slab_4x4", i * Bay, FloorHeight, -j * Bay);
                }
                Kit(walls, "Window_4m", i * Bay, 0f, 0f);
            }
            for (int j = 0; j < 3; j++)
            {
                Kit(walls, "Wall_Solid_4m", 0f, 0f, -(j + 1) * Bay, -90f);
                Kit(walls, "Wall_Solid_4m", 3 * Bay, 0f, -j * Bay, 90f);
            }
        }

        static GameObject Kit(Transform parent, string piece, float x, float y, float z, float yaw = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitPrefabs}/{piece}.prefab");
            if (prefab == null) throw new FileNotFoundException($"No kit prefab {piece}; run Close the Deal > Art > Import Kit first.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetLocalPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        // ---- Capture ---------------------------------------------------------------------------

        [MenuItem("Close the Deal/Lanes/Capture Title Screen")]
        public static void CaptureFromMenu() => Capture();

        /// <summary>Batch entry: -executeMethod CloseTheDeal.Editor.Lanes.TitleLaneSetup.CaptureFromCommandLine</summary>
        public static void CaptureFromCommandLine()
        {
            Capture();
            EditorApplication.Exit(0);
        }

        static void Capture()
        {
            Directory.CreateDirectory(CaptureFolder);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ShaderUtil.allowAsyncCompilation = false;
            var backdrop = Object.FindAnyObjectByType<TitleBackdrop>();
            Camera cam = Camera.main;
            foreach (var (name, w, h) in new[] { ("1080p", 1920, 1080), ("720p", 1280, 720), ("21x9", 2560, 1080) })
            {
                backdrop.Pose(0f);
                Texture2D tex = Render(cam, w, h);
                DrawSafeAreas(tex);
                File.WriteAllBytes($"{CaptureFolder}/title_{name}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            // Idle: the same frame at four moments, to see the breathing and the sun.
            var idle = new List<Texture2D>();
            foreach (float s in new[] { 0f, 2.5f, 5f, 7.5f, 30f })
            {
                backdrop.Pose(s);
                idle.Add(Render(cam, 640, 360));
            }
            WriteStrip($"{CaptureFolder}/idle_strip.png", idle, 640, 360);
            backdrop.Pose(0f);

            // Cost of the backdrop: indicative CPU time of a camera render in the editor (not a GPU measurement).
            Object.DestroyImmediate(Render(cam, 1920, 1080));
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 30; i++) Object.DestroyImmediate(Render(cam, 1920, 1080));
            Debug.Log($"[Title] camera render, 1080p, editor CPU side: {clock.Elapsed.TotalMilliseconds / 30:F1} ms per frame (indicative only).");

            // Transitions: eleven frames each over the whole length, over the title frame.
            foreach (UiTransitionKind kind in new[] { UiTransitionKind.CloudWipe, UiTransitionKind.PaneSlide, UiTransitionKind.FrameZoom, UiTransitionKind.Fade })
            {
                var frames = new List<Texture2D>();
                for (int i = 0; i <= 10; i++)
                {
                    UiTransition.Preview(kind, i / 10f, cam);
                    Canvas.ForceUpdateCanvases();
                    frames.Add(Render(cam, 480, 270));
                }
                UiTransition.ClearPreview();
                WriteStrip($"{CaptureFolder}/transition_{kind}.png", frames, 480, 270, 4);
            }
            Debug.Log("[Title] Captures written to " + CaptureFolder);
        }

        static Texture2D Render(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24);
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.aspect = w / (float)h;
            cam.scene = cam.gameObject.scene;
            // WHY two renders: a fresh camera's first frame over just-loaded objects comes out wrong (KitOfficeScene saw it too).
            cam.Render();
            cam.Render();
            cam.targetTexture = previous;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            return tex;
        }

        static void DrawSafeAreas(Texture2D tex)
        {
            float aspect = tex.width / (float)tex.height;
            Outline(tex, TitleSafeAreas.OnScreen(TitleSafeAreas.LowerBand, aspect), new Color(0.15f, 0.8f, 0.9f));
            Outline(tex, TitleSafeAreas.OnScreen(TitleSafeAreas.SkyWindow, aspect), new Color(1f, 0.62f, 0.1f));
            tex.Apply();
        }

        static void Outline(Texture2D tex, Rect r, Color c)
        {
            int x0 = Mathf.RoundToInt(r.xMin * tex.width), x1 = Mathf.RoundToInt(r.xMax * tex.width);
            int y1 = tex.height - Mathf.RoundToInt(r.yMin * tex.height), y0 = tex.height - Mathf.RoundToInt(r.yMax * tex.height);
            for (int t = 0; t < 3; t++)
                for (int x = x0; x <= x1; x++) { tex.SetPixel(x, y0 + t, c); tex.SetPixel(x, y1 - t, c); }
            for (int t = 0; t < 3; t++)
                for (int y = y0; y <= y1; y++) { tex.SetPixel(x0 + t, y, c); tex.SetPixel(x1 - t, y, c); }
        }

        static void WriteStrip(string path, List<Texture2D> frames, int w, int h, int perRow = 0)
        {
            int cols = perRow > 0 ? perRow : frames.Count, rows = Mathf.CeilToInt(frames.Count / (float)cols);
            var sheet = new Texture2D(cols * w, rows * h, TextureFormat.RGB24, false);
            for (int i = 0; i < frames.Count; i++)
                sheet.SetPixels((i % cols) * w, (rows - 1 - i / cols) * h, w, h, frames[i].GetPixels());
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            foreach (Texture2D f in frames) Object.DestroyImmediate(f);
        }
    }
}
