using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Blurs a UI backdrop RawImage (e.g. TitleScreen.BackdropSlot) when no 3D scene camera feeds glass.</summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]   // so its count is kept in edit mode too (a stuck count would switch the scene blur off)
    [AddComponentMenu("Seal the Deal/UI Glass Backdrop")]
    public class UiGlassBackdrop : MonoBehaviour
    {
        [Tooltip("The backdrop image. Empty = the first RawImage under this object.")]
        [SerializeField] RawImage source;

        /// <summary>Backdrops visible this frame. The scene blur (UiGlassBlurFeature) stands down while above zero.</summary>
        public static int Active;

        static readonly int BlurTex = Shader.PropertyToID("_UiGlassBlurTex");
        static readonly int BlurOn = Shader.PropertyToID("_UiGlassBlurOn");
        static readonly int BlurRect = Shader.PropertyToID("_UiGlassBlurRect");
        static readonly int Radius = Shader.PropertyToID("_UiGlassRadius");
        static readonly int TexelSize = Shader.PropertyToID("_BlitTexture_TexelSize");
        static readonly Vector3[] Corners = new Vector3[4];

        RenderTexture _half, _quarter, _out;
        RawImage _img;          // cached: looked up once, not every frame
        Canvas _rootCanvas;
        static int _attachFrame = -1;
        Material _mat;
        CommandBuffer _cmd;
        bool _counted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            Active = 0;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachNow() => AttachToTitles();

        static void OnSceneLoaded(Scene s, LoadSceneMode m) => AttachToTitles();

        /// <summary>Adds a backdrop to every kit title screen's BackdropSlot (where the live sky goes).</summary>
        public static void AttachToTitles()
        {
            if (_attachFrame == Time.frameCount) return;   // many panes enabling in one frame search once
            _attachFrame = Time.frameCount;
            foreach (var t in FindObjectsByType<TitleScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.BackdropSlot != null && t.BackdropSlot.GetComponent<UiGlassBackdrop>() == null)
                    t.BackdropSlot.gameObject.AddComponent<UiGlassBackdrop>();
        }

        void OnDisable()
        {
            SetCounted(false);
            Release(ref _half); Release(ref _quarter); Release(ref _out);
            if (_mat != null) { Discard(_mat); _mat = null; }
            _cmd?.Release(); _cmd = null;
        }

        void SetCounted(bool on)
        {
            if (on == _counted) return;
            _counted = on;
            Active += on ? 1 : -1;
            if (!on && Active == 0) Shader.SetGlobalFloat(BlurOn, 0f);
        }

        void LateUpdate() => Refresh();

        /// <summary>Blur the backdrop now and publish it (also called by the capture tool outside Play Mode).</summary>
        public void Refresh()
        {
            if (_img == null || (source != null && _img != source))
                _img = source != null ? source : GetComponentInChildren<RawImage>();
            // the root canvas is cheap to read from the image's own canvas, and follows a re-parent
            _rootCanvas = _img != null && _img.canvas != null ? _img.canvas.rootCanvas : null;
            var img = _img;
            var tex = img != null ? img.texture : null;
            bool visible = tex != null && img.isActiveAndEnabled && img.color.a > 0.01f
                           && _rootCanvas != null && _rootCanvas.isActiveAndEnabled   // a hidden canvas shows nothing
                           && UiGlassSettings.BlurAllowed && UiGlassBlurFeature.ActivePanes > 0;
            SetCounted(visible);
            if (!visible) return;
            if (!EnsureResources(tex.width, tex.height)) { SetCounted(false); return; }

            _cmd.Clear();
            Blit(tex, _half, 0);
            Blit(_half, _quarter, 1);
            Blit(_quarter, _out, 2);   // gaussian H into _out, then V back into _quarter
            Blit(_out, _quarter, 3);
            Graphics.ExecuteCommandBuffer(_cmd);

            Shader.SetGlobalTexture(BlurTex, _quarter);
            Shader.SetGlobalVector(BlurRect, ScreenRect(img.rectTransform, _rootCanvas));
            Shader.SetGlobalFloat(BlurOn, 1f);
        }

        void Blit(Texture src, RenderTexture dst, int pass)
        {
            _cmd.SetRenderTarget(dst);
            _cmd.SetGlobalVector(TexelSize, new Vector4(1f / src.width, 1f / src.height, src.width, src.height));
            Blitter.BlitTexture(_cmd, src, new Vector4(1, 1, 0, 0), _mat, pass);
        }

        bool EnsureResources(int w, int h)
        {
            if (_mat == null)
            {
                var sh = Shader.Find("Hidden/CloseTheDeal/UiGlassBlur");
                if (sh == null) return false;
                _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                _mat.SetFloat(Radius, 1.6f);
            }
            _cmd ??= new CommandBuffer { name = "UiGlassBackdrop" };
            Ensure(ref _half, Mathf.Max(w / 2, 2), Mathf.Max(h / 2, 2));
            Ensure(ref _quarter, Mathf.Max(w / 4, 2), Mathf.Max(h / 4, 2));
            Ensure(ref _out, Mathf.Max(w / 4, 2), Mathf.Max(h / 4, 2));
            return true;
        }

        static void Ensure(ref RenderTexture rt, int w, int h)
        {
            if (rt != null && rt.width == w && rt.height == h) return;
            Release(ref rt);
            rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            { name = "UiGlassBackdrop", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            rt.Create();
        }

        static void Release(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Discard(rt);
            rt = null;
        }

        static void Discard(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>The image's rectangle on screen as (x, y, width, height) in 0..1 of the canvas's pixel rect.</summary>
        static Vector4 ScreenRect(RectTransform rt, Canvas canvas)
        {
            if (canvas == null) return new Vector4(0, 0, 1, 1);
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Rect px = canvas.pixelRect;
            rt.GetWorldCorners(Corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, Corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, Corners[2]);
            float w = Mathf.Max(px.width, 1), h = Mathf.Max(px.height, 1);
            return new Vector4((a.x - px.x) / w, (a.y - px.y) / h, (b.x - a.x) / w, (b.y - a.y) / h);
        }
    }
}
