using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>Headless capture and contrast/timing measurement tool for UI glass presets (docs/systems/UI_GLASS.md).</summary>
    public static class UiGlassCapture
    {
        const int W = 1920, H = 1080;

        public static void RunFromCommandLine()
        {
            string scene = Arg("-glassScene", "Assets/_Project/Scenes/Lanes/Lane_Integration.unity");
            string outDir = Arg("-glassOut", "Temp/glass");
            var poses = new List<(string, Vector3, float, float)>();
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-glassPose")
                {
                    var a = args[i + 1].Split(':'); var v = a[1].Split(',');
                    poses.Add((a[0], new Vector3(F(v[0]), F(v[1]), F(v[2])), F(v[3]), F(v[4])));
                }
            try { Run(scene, outDir, poses); }
            catch (Exception e) { Debug.LogError("UiGlassCapture failed: " + e); EditorApplication.Exit(1); return; }
            EditorApplication.Exit(0);
        }

        static string Arg(string key, string def)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1];
            return def;
        }
        static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        [MenuItem("Close the Deal/UI/Dump Scene For Glass Capture")]
        public static void Dump()
        {
            var sb = new StringBuilder();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                sb.AppendLine($"CAMERA {c.name} pos {c.transform.position} euler {c.transform.eulerAngles} enabled {c.enabled}");
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                string n = r.name.ToLowerInvariant();
                if (n.Contains("window") || n.Contains("glass") || n.Contains("curtain") || n.Contains("suit") || n.Contains("stdmc"))
                    sb.AppendLine($"RENDERER {r.name} center {r.bounds.center} size {r.bounds.size}");
            }
            Debug.Log("GLASSDUMP\n" + sb);
        }

        public static void DumpFromCommandLine()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-glassAddFeature") >= 0)
                UiGlassSetup.AddFeature("Assets/Settings/PC_Renderer.asset");
            EditorSceneManager.OpenScene(Arg("-glassScene", "Assets/_Project/Scenes/Lanes/Lane_Integration.unity"));
            Dump();
            EditorApplication.Exit(0);
        }


        /// <summary>Measures per-frame blur vs no-blur cost at 1080p and 1440p from the command line.</summary>
        public static void TimeFromCommandLine()
        {
            try
            {
                EditorSceneManager.OpenScene(Arg("-glassScene", "Assets/_Project/Scenes/Lanes/Lane_Integration.unity"));
                string outDir = Arg("-glassOut", "Temp/glass");
                Directory.CreateDirectory(outDir);
                var a = Arg("-glassPose", "window:24,1.7,0,-12,270").Split(':')[1].Split(',');
                var camGo = new GameObject("GlassTimingCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 70; cam.nearClipPlane = 0.05f;
                camGo.transform.SetPositionAndRotation(new Vector3(F(a[0]), F(a[1]), F(a[2])), Quaternion.Euler(F(a[3]), F(a[4]), 0));
                UiGlassBlurFeature.SourceCamera = cam;
                foreach (var other in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (other != cam) other.enabled = false;
                var sb = new StringBuilder("| resolution | blur on ms/frame | no blur ms/frame | blur cost ms | GPU ms (recorders) |\n|---|---|---|---|---|\n");
                string[] passes = { "UiGlass Down 1/2", "UiGlass Down 1/4", "UiGlass Blur H", "UiGlass Blur V" };
                foreach (var (w, h) in new[] { (1920, 1080), (2560, 1440) })
                {
                    var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    cam.targetTexture = rt;
                    var canvasGo = BuildCanvas(cam, true, out _);
                    double on = TimeFrames(cam, rt, UiGlassSettings.Quality.Blur, 240);
                    double gpu = 0;
                    var recs = new List<UnityEngine.Profiling.Recorder>();
                    foreach (var n in passes) { var r = UnityEngine.Profiling.Recorder.Get(n); r.enabled = true; recs.Add(r); }
                    TimeFrames(cam, rt, UiGlassSettings.Quality.Blur, 30);
                    foreach (var r in recs) gpu += r.gpuElapsedNanoseconds / 1e6;
                    double off = TimeFrames(cam, rt, UiGlassSettings.Quality.NoBlur, 240);
                    double on2 = TimeFrames(cam, rt, UiGlassSettings.Quality.Blur, 240);
                    double onAvg = (on + on2) / 2;
                    sb.AppendLine($"| {w}x{h} | {onAvg:0.000} | {off:0.000} | {onAvg - off:0.000} | {(gpu > 0 ? gpu.ToString("0.000") : "not reported")} |");
                    UnityEngine.Object.DestroyImmediate(canvasGo);
                    cam.targetTexture = null; rt.Release();
                }
                File.WriteAllText(Path.Combine(outDir, "glass_timing.md"), sb.ToString());
                Debug.Log("GLASSTIMING\n" + sb);
            }
            catch (Exception e) { Debug.LogError("UiGlassCapture timing failed: " + e); EditorApplication.Exit(1); return; }
            EditorApplication.Exit(0);
        }

        static double TimeFrames(Camera cam, RenderTexture rt, UiGlassSettings.Quality q, int frames)
        {
            UiGlassSettings.Current = q;
            var px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            for (int i = 0; i < 20; i++) Sync(cam, rt, px);   // warm-up
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < frames; i++) Sync(cam, rt, px);
            sw.Stop();
            UnityEngine.Object.DestroyImmediate(px);
            return sw.Elapsed.TotalMilliseconds / frames;
        }

        static void Sync(Camera cam, RenderTexture rt, Texture2D px)
        {
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            px.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);   // forces the GPU to finish the frame
            RenderTexture.active = prev;
        }

        static void Run(string scenePath, string outDir, List<(string, Vector3, float, float)> poses)
        {
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(scenePath);
            var camGo = new GameObject("GlassCaptureCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70; cam.nearClipPlane = 0.05f; cam.farClipPlane = 2000;
            cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            UiGlassBlurFeature.SourceCamera = cam;   // it renders into a texture, so name it as the glass's source
            foreach (var other in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (other != cam) other.enabled = false;

            var report = new StringBuilder();
            report.AppendLine("| pose | preset | mode | text px (sRGB) | darkest bg px under text | contrast |");
            report.AppendLine("|---|---|---|---|---|---|");
            foreach (var (name, pos, pitch, yaw) in poses)
            {
                camGo.transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, 0));
                Save(Grab(cam, rt), Path.Combine(outDir, name + "_scene.png"));
                foreach (var mode in new[] { UiGlassSettings.Quality.Blur, UiGlassSettings.Quality.NoBlur })
                {
                    UiGlassSettings.Current = mode;
                    var canvasGo = BuildCanvas(cam, false, out var panes);
                    Canvas.ForceUpdateCanvases();
                    var bgTex = Grab(cam, rt);                           // panes, no text
                    float blurOn = Shader.GetGlobalFloat("_UiGlassBlurOn");
                    UnityEngine.Object.DestroyImmediate(canvasGo);
                    canvasGo = BuildCanvas(cam, true, out panes);
                    Canvas.ForceUpdateCanvases();
                    var txtTex = Grab(cam, rt);
                    Save(txtTex, Path.Combine(outDir, $"{name}_glass_{mode}.png"));
                    foreach (var p in panes)
                    {
                        var (tPx, bgPx, ratio) = Measure(bgTex, txtTex, p.textBox);
                        report.AppendLine($"| {name} | {p.preset} | {mode} (blurOn={blurOn}) | {Hex(tPx)} | {Hex(bgPx)} | {ratio:0.00}:1 |");
                    }
                    UnityEngine.Object.DestroyImmediate(canvasGo);
                    UnityEngine.Object.DestroyImmediate(bgTex); UnityEngine.Object.DestroyImmediate(txtTex);
                }
            }
            // Overlay canvases never reach a camera texture and a batch editor has no screen: only a person playing sees them.
            // Backdrop source: a full-screen RawImage showing the first pose's plain scene, with UiGlassBackdrop on it.
            // The blurred pane must line up with the image behind it (orientation and rect mapping).
            if (poses.Count > 0)
            {
                var (bname, bpos, bpitch, byaw) = poses[0];
                camGo.transform.SetPositionAndRotation(bpos, Quaternion.Euler(bpitch, byaw, 0));
                var sceneTex = Grab(cam, rt);
                UiGlassSettings.Current = UiGlassSettings.Quality.Auto;
                var canvasGo = BuildCanvas(cam, false, out var panes);
                var bd = new GameObject("Backdrop", typeof(RectTransform), typeof(RawImage), typeof(UiGlassBackdrop));
                bd.transform.SetParent(canvasGo.transform, false);
                bd.transform.SetAsFirstSibling();
                var brt = (RectTransform)bd.transform;
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
                bd.GetComponent<RawImage>().texture = sceneTex;
                Canvas.ForceUpdateCanvases();
                bd.GetComponent<UiGlassBackdrop>().Refresh();
                var bgTex = Grab(cam, rt);
                UnityEngine.Object.DestroyImmediate(canvasGo);
                canvasGo = BuildCanvas(cam, true, out panes);
                bd = new GameObject("Backdrop", typeof(RectTransform), typeof(RawImage), typeof(UiGlassBackdrop));
                bd.transform.SetParent(canvasGo.transform, false);
                bd.transform.SetAsFirstSibling();
                brt = (RectTransform)bd.transform;
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
                bd.GetComponent<RawImage>().texture = sceneTex;
                Canvas.ForceUpdateCanvases();
                bd.GetComponent<UiGlassBackdrop>().Refresh();
                var txtTex = Grab(cam, rt);
                Save(txtTex, Path.Combine(outDir, $"{bname}_glass_backdrop.png"));
                foreach (var p in panes)
                {
                    var (tPx, bgPx, ratio) = Measure(bgTex, txtTex, p.textBox);
                    report.AppendLine($"| {bname} | {p.preset} | UI backdrop (UiGlassBackdrop) | {Hex(tPx)} | {Hex(bgPx)} | {ratio:0.00}:1 |");
                }
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(bgTex); UnityEngine.Object.DestroyImmediate(txtTex); UnityEngine.Object.DestroyImmediate(sceneTex);
            }
            // a scene with no panes must not run the passes
            UiGlassSettings.Current = UiGlassSettings.Quality.Auto;
            int before = UiGlassBlurFeature.ActivePanes;
            Grab(cam, rt);
            report.AppendLine($"\nNo panes alive: ActivePanes={before}, _UiGlassBlurOn after render = {Shader.GetGlobalFloat("_UiGlassBlurOn")} (0 = passes skipped)");
            File.WriteAllText(Path.Combine(outDir, "glass_contrast.md"), report.ToString());
            Debug.Log("GLASSREPORT\n" + report);
            UnityEngine.Object.DestroyImmediate(camGo);
            rt.Release();
        }

        class PaneInfo { public string preset; public RectInt textBox; }

        static GameObject BuildCanvas(Camera cam, bool withText, out List<PaneInfo> panes)
        {
            panes = new List<PaneInfo>();
            var go = new GameObject("GlassCaptureCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 0.08f;   // in front of any scene geometry (near clip 0.05)
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(W, H);
            var presets = new[] { UiGlass.Preset.Frosted, UiGlass.Preset.HudChip, UiGlass.Preset.Clear, UiGlass.Preset.NoBlur };
            for (int i = 0; i < presets.Length; i++)
            {
                var pane = new GameObject("Pane" + presets[i], typeof(RectTransform), typeof(Image), typeof(UiGlass));
                pane.transform.SetParent(go.transform, false);
                var rtf = (RectTransform)pane.transform;
                rtf.anchorMin = rtf.anchorMax = new Vector2(0.5f, 0.5f);
                rtf.sizeDelta = new Vector2(560, 120);
                rtf.anchoredPosition = new Vector2(0, 330 - i * 190);
                pane.GetComponent<Image>().color = Color.white;
                pane.GetComponent<UiGlass>().PresetKind = presets[i];
                var tgo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                tgo.transform.SetParent(pane.transform, false);
                var t = tgo.GetComponent<TextMeshProUGUI>();
                t.text = "Pick up · AR-15";
                t.color = new Color32(0x11, 0x1B, 0x38, 255);   // Deep Navy
                t.fontSize = 44; t.alignment = TextAlignmentOptions.Center;
                ((RectTransform)tgo.transform).sizeDelta = new Vector2(540, 100);
                tgo.SetActive(withText);
                // text box in screen px (canvas is 1:1 at 1920x1080, origin bottom-left in Texture2D)
                int cx = W / 2, cy = H / 2 + (int)rtf.anchoredPosition.y;
                panes.Add(new PaneInfo { preset = presets[i].ToString(), textBox = new RectInt(cx - 200, cy - 30, 400, 60) });
            }
            // Lane 5's real kit pane, as the menus use it (wired by UiGlassSetup.WireKitPane)
            var kit = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/Kit/GlassPane.prefab");
            if (kit != null)
            {
                var k = (GameObject)PrefabUtility.InstantiatePrefab(kit, go.transform);
                var krt = (RectTransform)k.transform;
                krt.anchorMin = krt.anchorMax = new Vector2(0.5f, 0.5f);
                krt.pivot = new Vector2(0.5f, 0.5f);
                krt.sizeDelta = new Vector2(560, 120);
                krt.anchoredPosition = new Vector2(0, -430);
                var tgo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                tgo.transform.SetParent(k.transform, false);
                var t = tgo.GetComponent<TextMeshProUGUI>();
                t.text = "Back to game"; t.color = new Color32(0x11, 0x1B, 0x38, 255);
                t.fontSize = 44; t.alignment = TextAlignmentOptions.Center;
                ((RectTransform)tgo.transform).sizeDelta = new Vector2(540, 100);
                tgo.SetActive(withText);
                panes.Add(new PaneInfo { preset = "KitGlassPane", textBox = new RectInt(W / 2 - 160, H / 2 - 430 - 30, 320, 60) });
            }
            return go;
        }

        static Texture2D Grab(Camera cam, RenderTexture rt)
        {
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        static void Save(Texture2D t, string path) => File.WriteAllBytes(path, t.EncodeToPNG());

        static float Lum(Color32 c)
        {
            float L(byte b) { float s = b / 255f; return s <= 0.03928f ? s / 12.92f : Mathf.Pow((s + 0.055f) / 1.055f, 2.4f); }
            return 0.2126f * L(c.r) + 0.7152f * L(c.g) + 0.0722f * L(c.b);
        }
        static string Hex(Color32 c) => $"#{c.r:X2}{c.g:X2}{c.b:X2}";

        static (Color32, Color32, float) Measure(Texture2D bgOnly, Texture2D withText, RectInt box)
        {
            Color32 darkText = new Color32(255, 255, 255, 255), darkBg = new Color32(255, 255, 255, 255);
            var a = bgOnly.GetPixels32(); var b = withText.GetPixels32();
            for (int y = box.y; y < box.yMax; y++)
                for (int x = box.x; x < box.xMax; x++)
                {
                    var pb = a[y * W + x]; var pt = b[y * W + x];
                    if (Lum(pb) < Lum(darkBg)) darkBg = pb;
                    if (Lum(pt) < Lum(darkText)) darkText = pt;
                }
            float l1 = Mathf.Max(Lum(darkBg), Lum(darkText)), l2 = Mathf.Min(Lum(darkBg), Lum(darkText));
            return (darkText, darkBg, (l1 + 0.05f) / (l2 + 0.05f));
        }
    }
}
