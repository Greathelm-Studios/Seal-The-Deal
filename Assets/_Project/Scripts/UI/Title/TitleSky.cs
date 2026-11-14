using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CloseTheDeal.UI.Title
{
    /// <summary>
    /// The live sky behind the title screen's window frame. In any scene with the UI kit's title screen it adds
    /// itself: a camera that draws only the scene's own sky (the Office Look sky shader: clouds drift, planes
    /// cross), slowly breathing, into a render texture shown in TitleScreen.BackdropSlot. The camera renders only
    /// while the title is on screen, draws no scene geometry and no lights of its own, so it costs nothing in a
    /// game and cannot leak into gameplay. Design: docs/systems/UI_TITLE.md.
    /// </summary>
    public sealed class TitleSky : MonoBehaviour
    {
        [Header("View")]
        [Tooltip("Degrees the view is turned from the sun. 0 = into the sun (its glare washes the panes pale); 150 to 180 = deep blue with the sunlit side of the clouds.")]
        [SerializeField, Range(-180f, 180f)] float _yawFromSun = 155f;
        [Tooltip("Degrees the view looks above the horizon. Higher = more deep blue, less city.")]
        [SerializeField, Range(-10f, 40f)] float _pitchUp = 20f;
        [Tooltip("Vertical field of view in degrees. Wider = more sky in each pane.")]
        [SerializeField, Range(30f, 90f)] float _fieldOfView = 46f;

        [Header("Breathing")]
        [Tooltip("How far the view sways left and right, in degrees. Higher = a more restless sky.")]
        [SerializeField, Range(0f, 5f)] float _swayDegrees = 1.4f;
        [Tooltip("Seconds for one slow sway out and back. Higher = calmer.")]
        [SerializeField, Range(5f, 60f)] float _swaySeconds = 26f;

        [Header("Sky")]
        [Tooltip("Seconds between planes while the title shows (the game uses 40). 0 = leave the sky as it is.")]
        [SerializeField, Range(0f, 120f)] float _planeEverySeconds = 18f;

        TitleScreen _title;
        Camera _camera;
        RawImage _image;
        Canvas _canvas;
        RenderTexture _texture;
        bool _showing;
        Quaternion _base;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryAttach();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttach();

        static void TryAttach()
        {
            UiRoot root = FindAnyObjectByType<UiRoot>(FindObjectsInactive.Include);
            if (root == null || root.Title == null || root.Title.BackdropSlot == null)
                return;
            if (root.GetComponentInChildren<TitleSky>(true) != null)
                return;
            var go = new GameObject("Title sky");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<TitleSky>().Bind(root.Title);
        }

        void Bind(TitleScreen title)
        {
            _title = title;
            _camera = gameObject.AddComponent<Camera>();
            _camera.cullingMask = 0;                      // the sky only: no scene geometry
            _camera.clearFlags = CameraClearFlags.Skybox;
            _camera.fieldOfView = _fieldOfView;
            _camera.depth = -100f;
            _camera.enabled = false;
            var data = gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;             // the game's own grade
            data.renderShadows = false;

            var imageGo = new GameObject("Live sky", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var rt = (RectTransform)imageGo.transform;
            rt.SetParent(title.BackdropSlot, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _image = imageGo.GetComponent<RawImage>();
            _image.raycastTarget = false;
            _image.enabled = false;

            Vector3 sunDir = RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : Vector3.forward;
            float sunYaw = Mathf.Atan2(sunDir.x, sunDir.z) * Mathf.Rad2Deg;
            _base = Quaternion.Euler(-_pitchUp, sunYaw + _yawFromSun, 0f);
            transform.rotation = _base;
        }

        void LateUpdate()
        {
            bool show = _title != null && _title.isActiveAndEnabled && _title.BackdropSlot.gameObject.activeInHierarchy;
            if (show != _showing)
                SetShowing(show);
            if (!_showing)
                return;
            EnsureTexture();
            float t = UiTransition.ReducedMotion ? 0f : Time.unscaledTime * Mathf.PI * 2f / _swaySeconds;
            transform.rotation = _base * Quaternion.Euler(Mathf.Sin(t * 0.5f) * _swayDegrees * 0.3f, Mathf.Sin(t) * _swayDegrees, 0f);
        }

        void SetShowing(bool show)
        {
            _showing = show;
            _camera.enabled = show;
            _image.enabled = show;
            _title.SetLiveBackdrop(show);
            if (show)
                TitleSkyOverride.Acquire(_planeEverySeconds);
            else
                TitleSkyOverride.Release();
        }

        /// <summary>The render texture matches the slot's size on screen, so the sky is drawn 1:1.</summary>
        void EnsureTexture()
        {
            Rect r = _title.BackdropSlot.rect;
            // Canvas units times the root canvas's scale factor = screen pixels (lossyScale is world units in a
            // camera-space canvas, so it can't be used here).
            if (_canvas == null)
                _canvas = _title.BackdropSlot.GetComponentInParent<Canvas>().rootCanvas;
            float scale = _canvas.scaleFactor;
            int w = Mathf.Max(16, Mathf.RoundToInt(r.width * scale));
            int h = Mathf.Max(16, Mathf.RoundToInt(r.height * scale));
            if (_texture != null && _texture.width == w && _texture.height == h)
                return;
            if (_texture != null)
            {
                _camera.targetTexture = null;
                _texture.Release();
                Destroy(_texture);
            }
            _texture = new RenderTexture(w, h, 16) { name = "Title sky" };
            _camera.targetTexture = _texture;
            _camera.aspect = w / (float)h;
            _image.texture = _texture;
        }

        void OnDestroy()
        {
            if (_showing)
                TitleSkyOverride.Release();
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }
    }

    /// <summary>
    /// The one place that swaps the scene's sky for a title-screen copy (more planes). Shared by TitleSky and
    /// TitleBackdrop so neither can restore the other's copy: the first user makes the copy, the last puts the
    /// original back. The sky asset itself is never written.
    /// </summary>
    public static class TitleSkyOverride
    {
        static readonly int PlaneEvery = Shader.PropertyToID("_PlaneEvery");
        static Material _original, _copy;
        static int _users;

        public static void Acquire(float planeEverySeconds)
        {
            if (_users++ > 0)
                return;
            _original = RenderSettings.skybox;
            if (_original == null || planeEverySeconds <= 0f || !_original.HasProperty(PlaneEvery))
                return;
            _copy = new Material(_original) { name = _original.name + " (title)" };
            _copy.SetFloat(PlaneEvery, planeEverySeconds);
            RenderSettings.skybox = _copy;
        }

        public static void Release()
        {
            if (_users == 0 || --_users > 0)
                return;
            if (_copy != null)
            {
                if (RenderSettings.skybox == _copy)
                    RenderSettings.skybox = _original;
                if (Application.isPlaying) Object.Destroy(_copy); else Object.DestroyImmediate(_copy);
            }
            _copy = null;
            _original = null;
        }
    }
}
