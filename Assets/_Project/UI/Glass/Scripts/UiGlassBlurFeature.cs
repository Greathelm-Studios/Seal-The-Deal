using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// URP (Render Graph) renderer feature: copies the camera colour after the opaque scene and sky, blurs it
    /// at quarter resolution (down, down, gaussian H, gaussian V) and publishes it as the global
    /// _UiGlassBlurTex for CloseTheDeal/UI/Glass. Runs BeforeRenderingTransparents so Screen Space - Camera
    /// canvases (drawn in the transparent pass) can sample it. Cost: see docs/systems/UI_GLASS.md.
    /// Does nothing (and sets _UiGlassBlurOn = 0) on non-game cameras, with quality "No blur", or when no pane is alive.
    /// </summary>
    public class UiGlassBlurFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader shader;
        [Tooltip("Gaussian width in quarter-res texels. 1 = sigma ~2.2 texels (~9 px at 1080p).")]
        [Range(0.5f, 3f)] public float radius = 1.6f;

        static readonly int BlurTex = Shader.PropertyToID("_UiGlassBlurTex");
        static readonly int BlurOn = Shader.PropertyToID("_UiGlassBlurOn");
        static readonly int Radius = Shader.PropertyToID("_UiGlassRadius");

        /// <summary>Panes alive in the scene. The passes are skipped when zero: scenes without glass pay nothing.</summary>
        public static int ActivePanes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCount() => ActivePanes = 0;   // enter Play Mode without a domain reload

        Material _mat;
        Pass _pass;

        public override void Create()
        {
            if (shader == null) shader = Shader.Find("Hidden/CloseTheDeal/UiGlassBlur");
            _pass = new Pass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            _pass.requiresIntermediateTexture = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var type = renderingData.cameraData.cameraType;
            bool game = type == CameraType.Game || type == CameraType.SceneView;
            // Overlay cameras in a stack keep the base camera's blur: one blur per frame, not one per camera.
            if (renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
            // Off until this frame's publish pass turns it on (in command order), so any skipped or early-out
            // frame falls back to the plain tint instead of sampling a stale texture.
            if (game) Shader.SetGlobalFloat(BlurOn, 0f);
            if (!game || shader == null || !UiGlassSettings.BlurAllowed || ActivePanes <= 0) return;
            if (_mat == null) _mat = CoreUtils.CreateEngineMaterial(shader);
            _pass.Setup(_mat, radius);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(_mat);

        class Pass : ScriptableRenderPass
        {
            Material _m;
            float _radius;
            public Pass() { profilingSampler = new ProfilingSampler("UiGlassBlur"); }
            public void Setup(Material m, float radius) { _m = m; _radius = radius; }

            class PublishData { }

            public override void RecordRenderGraph(RenderGraph rg, ContextContainer frameData)
            {
                var res = frameData.Get<UniversalResourceData>();
                var cam = frameData.Get<UniversalCameraData>();
                if (res.isActiveTargetBackBuffer) return;     // nothing sampleable yet; panes keep the fallback

                int w = Mathf.Max(cam.cameraTargetDescriptor.width, 4), h = Mathf.Max(cam.cameraTargetDescriptor.height, 4);
                TextureHandle Make(string n, int div)
                {
                    var d = new TextureDesc(Mathf.Max(w / div, 2), Mathf.Max(h / div, 2))
                    {
                        colorFormat = GraphicsFormat.B10G11R11_UFloatPack32, depthBufferBits = 0,
                        msaaSamples = MSAASamples.None, name = n, filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp, clearBuffer = false
                    };
                    return rg.CreateTexture(d);
                }
                var half = Make("_UiGlassHalf", 2);
                var quarter = Make("_UiGlassQuarter", 4);
                var tmp = Make("_UiGlassTmp", 4);
                _m.SetFloat(Radius, _radius);

                rg.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(res.activeColorTexture, half, _m, 0), "UiGlass Down 1/2");
                rg.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(half, quarter, _m, 1), "UiGlass Down 1/4");
                rg.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(quarter, tmp, _m, 2), "UiGlass Blur H");
                rg.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(tmp, quarter, _m, 3), "UiGlass Blur V");

                // Publish: a pass that reads the result and sets it as a global for the transparent pass.
                using (var b = rg.AddRasterRenderPass<PublishData>("UiGlass Publish", out _))
                {
                    b.UseTexture(quarter, AccessFlags.Read);
                    b.SetGlobalTextureAfterPass(quarter, BlurTex);
                    b.AllowPassCulling(false);
                    b.AllowGlobalStateModification(true);
                    b.SetRenderFunc((PublishData d, RasterGraphContext c) => c.cmd.SetGlobalFloat(BlurOn, 1f));
                }
            }
        }
    }
}
