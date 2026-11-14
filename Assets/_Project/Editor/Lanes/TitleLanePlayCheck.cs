using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CloseTheDeal.UI;
using CloseTheDeal.UI.Title;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>
    /// Sees the title's live sky and the screen transitions running in Play Mode, in the integration scene.
    /// Batch only: -executeMethod CloseTheDeal.Editor.Lanes.TitleLanePlayCheck.RunFromCommandLine [-titleOut folder]
    /// It checks: the live sky attaches and renders while the title shows; its view moves over time; every
    /// transition plays, covers and finishes within a frame or two of its stated length; a call made during one
    /// waits; reduced motion turns a wipe into the short fade and stills the sky; hiding the title stops the sky
    /// camera and puts the game's own sky back. Writes frame strips and play_check.txt. Nothing is saved.
    /// </summary>
    public static class TitleLanePlayCheck
    {
        const string ScenePath = "Assets/_Project/Scenes/Lanes/Lane_Integration.unity";
        const string ActiveKey = "TitlePlayCheck.Active", OutKey = "TitlePlayCheck.Out";
        const string ReducedKey = "TitlePlayCheck.Reduced", ReducedSavedKey = "TitlePlayCheck.ReducedSaved";
        const int W = 960, H = 540;

        static IEnumerator _run;
        static int _failures;
        static string _out;
        static readonly StringBuilder Log = new();
        static Camera _cam;
        static RenderTexture _rt;

        public static void RunFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-titleOut");
            string folder = at >= 0 && at + 1 < args.Length ? args[at + 1] : "ArtSource/UI/passes/c_sky/title/play";
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(OutKey, Path.GetFullPath(folder));
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (SessionState.GetBool(ActiveKey, false))
                EditorApplication.update += Pump;
        }

        static void Pump()
        {
            if (!EditorApplication.isPlaying)
                return;
            _run ??= Run();
            bool more;
            try { more = _run.MoveNext(); }
            catch (Exception e) { Check("the check ran without throwing: " + e, false); more = false; }
            if (!more)
                Finish();
        }

        static void Finish()
        {
            EditorApplication.update -= Pump;
            SessionState.SetBool(ActiveKey, false);
            if (SessionState.GetBool(ReducedSavedKey, false))
            {
                UiSettings.ReducedMotion = SessionState.GetBool(ReducedKey, false);
                SessionState.SetBool(ReducedSavedKey, false);
            }
            Log.AppendLine(_failures == 0 ? "PASS" : $"FAIL ({_failures})");
            File.WriteAllText(Path.Combine(_out, "play_check.txt"), Log.ToString());
            Debug.Log("[TitlePlay]\n" + Log);
            EditorApplication.Exit(_failures == 0 ? 0 : 1);
        }

        static void Check(string what, bool ok)
        {
            Log.AppendLine((ok ? "ok    " : "FAIL  ") + what);
            if (!ok) _failures++;
        }

        static IEnumerable Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        static IEnumerator Run()
        {
            _out = SessionState.GetString(OutKey, "Temp/TitlePlay");
            Application.runInBackground = true;
            foreach (object _ in Wait(1.5f)) yield return null;

            UiRoot root = Object.FindAnyObjectByType<UiRoot>();
            Check("the scene has the UI kit's root", root != null);
            if (root == null) yield break;
            float t0 = Time.realtimeSinceStartup;
            while (!root.Title.isActiveAndEnabled && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Check("the title screen is showing", root.Title.isActiveAndEnabled);
            foreach (object _ in Wait(0.5f)) yield return null;

            var sky = root.GetComponentInChildren<TitleSky>(true);
            Camera skyCam = sky != null ? sky.GetComponent<Camera>() : null;
            Check("the live sky attached itself under the UI root", sky != null);
            if (sky == null) yield break;
            Check("its camera renders while the title shows", skyCam.enabled && skyCam.targetTexture != null);
            Check($"its texture matches the slot ({skyCam.targetTexture?.width} x {skyCam.targetTexture?.height})", skyCam.targetTexture != null && skyCam.targetTexture.width > 100);
            Check("the sky in use is the title's copy (planes every 18 s)", RenderSettings.skybox != null && RenderSettings.skybox.name.EndsWith("(title)"));

            SetUpCapture(root);
            // PlayerPrefs are shared by every checkout on this Mac: remember the player's setting outside the run
            // and put it back in Finish, whatever happens in between.
            SessionState.SetBool(ReducedKey, UiSettings.ReducedMotion);
            SessionState.SetBool(ReducedSavedKey, true);
            UiSettings.ReducedMotion = false;

            // Idle: four frames three seconds apart; the sky camera must have turned between them.
            var idle = new List<Texture2D>();
            Quaternion first = sky.transform.rotation;
            for (int i = 0; i < 4; i++)
            {
                idle.Add(Grab());
                if (i < 3) foreach (object _ in Wait(3f)) yield return null;
            }
            float turned = Quaternion.Angle(first, sky.transform.rotation);
            Check($"the sky view breathes ({turned:F2} degrees in 9 s)", turned > 0.05f);
            Strip("idle_play.png", idle, 4);

            foreach (UiTransitionKind kind in new[] { UiTransitionKind.CloudWipe, UiTransitionKind.PaneSlide, UiTransitionKind.FrameZoom, UiTransitionKind.Fade })
            {
                IEnumerator play = PlayOne(kind, kind.ToString());
                while (play.MoveNext()) yield return null;
            }

            // A second call while one plays waits, so its screen change is still covered.
            float firstDone = -1f, secondCovered = -1f;
            UiTransition.Play(UiTransitionKind.CloudWipe, null, () => firstDone = Time.realtimeSinceStartup);
            UiTransition.Play(UiTransitionKind.PaneSlide, () => secondCovered = Time.realtimeSinceStartup, null);
            t0 = Time.realtimeSinceStartup;
            while (secondCovered < 0f && Time.realtimeSinceStartup - t0 < 3f) yield return null;
            Check($"a call made during a transition waits for it (second covered {secondCovered - firstDone:F3} s after the first ended)", firstDone > 0f && secondCovered >= firstDone);
            while (UiTransition.IsPlaying) yield return null;

            // Reduced motion: the wipe becomes the short fade and the sky holds still.
            UiSettings.ReducedMotion = true;
            Check($"reduced motion makes every kind the fade ({UiTransition.Duration(UiTransitionKind.FrameZoom):F2} s)", Mathf.Approximately(UiTransition.Duration(UiTransitionKind.FrameZoom), 0.12f));
            IEnumerator reduced = PlayOne(UiTransitionKind.CloudWipe, "CloudWipe_reduced");
            while (reduced.MoveNext()) yield return null;
            foreach (object _ in Wait(0.2f)) yield return null;
            Quaternion still = sky.transform.rotation;
            foreach (object _ in Wait(2f)) yield return null;
            Check($"reduced motion stills the sky ({Quaternion.Angle(still, sky.transform.rotation):F3} degrees in 2 s)", Quaternion.Angle(still, sky.transform.rotation) < 0.01f);

            // Leaving the title: the sky camera stops and the game's own sky comes back.
            root.Title.gameObject.SetActive(false);
            yield return null;
            yield return null;
            Check("hiding the title stops the sky camera", !skyCam.enabled);
            Check($"and puts the game's sky back ({RenderSettings.skybox?.name})", RenderSettings.skybox != null && !RenderSettings.skybox.name.EndsWith("(title)"));
            root.Title.gameObject.SetActive(true);
            yield return null;
        }

        /// <summary>Plays one transition for real and records frames, when it covered and when it finished.</summary>
        static IEnumerator PlayOne(UiTransitionKind kind, string label)
        {
            float expected = UiTransition.Duration(kind);
            UiTransition.Timing(kind, out float expectCover, out _);
            float start = Time.realtimeSinceStartup, covered = -1f, done = -1f;
            var frames = new List<Texture2D>();
            var events = UnityEngine.EventSystems.EventSystem.current;
            bool navBefore = events != null && events.sendNavigationEvents;
            UiTransition.Play(kind, () => covered = Time.realtimeSinceStartup - start, () => done = Time.realtimeSinceStartup - start);
            bool navDuring = events != null && events.sendNavigationEvents;
            UiTransitionHost host = null;
            while (done < 0f && Time.realtimeSinceStartup - start < 3f)
            {
                yield return null;
                if (host == null)
                {
                    host = Object.FindAnyObjectByType<UiTransitionHost>();
                    if (host != null) Bind(host);
                }
                frames.Add(Grab());
            }
            frames.Add(Grab());
            if (events != null)
                Check($"{label}: keys and pad are held off while it plays and given back after", navBefore && !navDuring && events.sendNavigationEvents);
            // A frame or two of slack: the coroutine steps once per rendered frame, and grabbing makes frames slow.
            float slack = 0.12f;
            Check($"{label}: covered at {covered:F3} s (stated {expectCover:F2}), done at {done:F3} s (stated {expected:F2}), {frames.Count} frames",
                covered > 0f && done > covered && Mathf.Abs(done - expected) < slack + expected * 0.25f);
            var pick = new List<Texture2D>();
            int n = Mathf.Min(12, frames.Count);
            for (int i = 0; i < n; i++)
                pick.Add(frames[Mathf.RoundToInt(i * (frames.Count - 1) / (float)Mathf.Max(1, n - 1))]);
            foreach (Texture2D f in frames)
                if (!pick.Contains(f)) Object.DestroyImmediate(f);
            Strip($"transition_play_{label}.png", pick, 4);
        }

        static void SetUpCapture(UiRoot root)
        {
            var go = new GameObject("Title play capture camera");
            _cam = go.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.62f, 0.65f, 0.68f);
            _cam.cullingMask = 1 << 5;
            _cam.orthographic = true;
            _cam.enabled = false;
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _cam.targetTexture = _rt;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _cam;
            canvas.planeDistance = 2f;
        }

        /// <summary>The transition overlay draws on the same capture camera, in front of the kit (sort order).</summary>
        static void Bind(UiTransitionHost host)
        {
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _cam;
            canvas.planeDistance = 1f;
            foreach (Transform t in host.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 5;
        }

        static Texture2D Grab()
        {
            Canvas.ForceUpdateCanvases();
            _cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            return tex;
        }

        static void Strip(string name, List<Texture2D> frames, int perRow)
        {
            int cols = Mathf.Min(perRow, frames.Count), rows = Mathf.CeilToInt(frames.Count / (float)cols);
            var sheet = new Texture2D(cols * W / 2, rows * H / 2, TextureFormat.RGB24, false);
            for (int i = 0; i < frames.Count; i++)
            {
                Color[] src = frames[i].GetPixels();
                var half = new Color[(W / 2) * (H / 2)];
                for (int y = 0; y < H / 2; y++)
                for (int x = 0; x < W / 2; x++)
                    half[y * (W / 2) + x] = src[(y * 2) * W + x * 2];
                sheet.SetPixels((i % cols) * W / 2, (rows - 1 - i / cols) * H / 2, W / 2, H / 2, half);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(_out, name), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            foreach (Texture2D f in frames) Object.DestroyImmediate(f);
            Log.AppendLine("wrote " + name);
        }
    }
}
