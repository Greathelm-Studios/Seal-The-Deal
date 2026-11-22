using System.IO;
using CloseTheDeal.UI.Title;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// Records the title's live sky on its own (the sky camera only: no window bars, no menu) as a JPEG frame
    /// sequence, for the motion mockups in ArtSource/UI/motion. Run it in Play mode with the title screen showing.
    /// Game time is locked to 30 fps while it records (Time.captureFramerate), so clouds, traced squares and
    /// planes move at their real speed whatever the editor's frame rate. Writes nothing to the project:
    /// frames go to Temp/TitleSkyFrames. Turn them into a clip with:
    ///   ffmpeg -framerate 30 -i Temp/TitleSkyFrames/s%04d.jpg -c:v libx264 -pix_fmt yuv420p -crf 16 out.mp4
    /// </summary>
    public static class TitleSkyRecorder
    {
        const int Fps = 30;
        const float Seconds = 12f;
        const int Width = 1920;   // the title's sky slot at 1920 x 1080: everything above the paper band
        const int Height = 664;
        const string Folder = "Temp/TitleSkyFrames";

        static Camera _cam;
        static RenderTexture _rt;
        static Texture2D _read;
        static int _saved;
        static int _lastFrame;
        static int _total;

        [MenuItem("Seal the Deal/UI/Record Title Sky (12 s)")]
        static void Record()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Record Title Sky", "Press Play first, with the title screen showing.", "OK");
                return;
            }
            if (_cam != null)
                return; // already recording

            TitleSky sky = Object.FindAnyObjectByType<TitleSky>();
            Camera cam = sky != null ? sky.GetComponent<Camera>() : null;
            if (cam == null || !cam.enabled)
            {
                EditorUtility.DisplayDialog("Record Title Sky", "The title's live sky isn't showing. Go back to the title screen, then try again.", "OK");
                return;
            }

            string dir = Path.GetFullPath(Folder);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            _cam = cam;
            _rt = new RenderTexture(Width, Height, 16) { name = "Title sky recording" };
            _read = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            _saved = 0;
            _lastFrame = Time.frameCount;
            _total = Mathf.RoundToInt(Fps * Seconds);
            Time.captureFramerate = Fps;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Debug.Log($"[Title sky] Recording {_total} frames at {Width}x{Height} to {dir}");
        }

        static void Tick()
        {
            if (_cam == null)
            {
                Stop("the sky camera went away");
                return;
            }
            if (Time.frameCount == _lastFrame)
                return; // one frame per game frame: game time moves 1/30 s per frame while recording
            _lastFrame = Time.frameCount;

            // Render the sky camera into our own texture at the slot's size, then put it back as it was.
            RenderTexture target = _cam.targetTexture;
            float aspect = _cam.aspect;
            _cam.targetTexture = _rt;
            _cam.aspect = Width / (float)Height;
            _cam.Render();
            _cam.targetTexture = target;
            _cam.aspect = aspect;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = _rt;
            _read.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            _read.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(Folder, $"s{_saved:0000}.jpg"), _read.EncodeToJPG(92));

            if (++_saved >= _total)
                Stop(null);
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
                Stop("Play mode ended");
        }

        static void Stop(string why)
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Time.captureFramerate = 0;
            if (_rt != null)
            {
                _rt.Release();
                Object.DestroyImmediate(_rt);
            }
            if (_read != null)
                Object.DestroyImmediate(_read);
            _cam = null;
            _rt = null;
            _read = null;
            string dir = Path.GetFullPath(Folder);
            if (why == null)
                Debug.Log($"[Title sky] Done: {_saved} frames in {dir}");
            else
                Debug.LogWarning($"[Title sky] Stopped after {_saved} of {_total} frames: {why}. Frames so far are in {dir}");
        }
    }
}
