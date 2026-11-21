using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace CloseTheDeal.UI
{
    /// <summary>URP Render Graph feature producing persistent quarter-res _UiGlassBlurTex for CloseTheDeal/UI/Glass.</summary>
    public class UiGlassBlurFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader shader;
        [Tooltip("Gaussian width in quarter-res texels. 1 = sigma ~2.2 texels (~9 px at 1080p).")]
        [Range(0.5f, 3f)] public float radius = 1.6f;

        static readonly int BlurTex = Shader.PropertyToID("_UiGlassBlurTex");
        static readonly int BlurOn = Shader.PropertyToID("_UiGlassBlurOn");
        static readonly int BlurRect = Shader.PropertyToID("_UiGlassBlurRect");
        static readonly int Radius = Shader.PropertyToID("_UiGlassRadius");

        /// <summary>Panes alive in the scene. The passes are skipped when zero: scenes without glass pay nothing.</summary>
        public static int ActivePanes;

        /// <summary>Optional: the one camera whose view the glass blurs (capture tools rendering into a texture).
        /// Null = any game camera that draws to the screen.</summary>
        public static Camera SourceCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCount() => ActivePanes = 0;   // enter Play Mode without a domain reload

        Material _mat;
        Pass _pass;
        RTHandle _persist;

        public override void Create()
        {
            if (shader == null) shader = Shader.Find("Hidden/CloseTheDeal/UiGlassBlur");
            _pass = new Pass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            _pass.requiresIntermediateTexture = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            ref var cd = ref renderingData.cameraData;
            if (cd.cameraType != CameraType.Game || cd.renderType == CameraRenderType.Overlay) return;
            if (cd.camera.cullingMask == 0) return;            // draws no scene (the title sky camera): not a source
            // Only the camera that draws the screen feeds the glass (not mirrors, minimaps or other render
            // textures), unless a tool names its own camera.
            if (SourceCamera != null ? cd.camera != SourceCamera : cd.camera.targetTexture != null) return;
            if (UiGlassBackdrop.Active > 0) return;            // a UI backdrop feeds the glass this frame
            // Off until this frame's publish pass turns it on (in command order), so any skipped or early-out
            // frame falls back to the plain tint instead of sampling a stale texture.
            Shader.SetGlobalFloat(BlurOn, 0f);
            if (shader == null || !UiGlassSettings.BlurAllowed || ActivePanes <= 0) return;
            if (_mat == null) _mat = CoreUtils.CreateEngineMaterial(shader);

            var d = cd.cameraTargetDescriptor;
            d.width = Mathf.Max(d.width / 4, 2);
            d.height = Mathf.Max(d.height / 4, 2);
            d.depthBufferBits = 0;
            d.depthStencilFormat = GraphicsFormat.None;
            d.msaaSamples = 1;
            d.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _persist, d, FilterMode.Bilinear, TextureWrapMode.Clamp,
                name: "_UiGlassBlurPersistent");
            Shader.SetGlobalTexture(BlurTex, _persist);
            Shader.SetGlobalVector(BlurRect, new Vector4(0, 0, 1, 1));
            _pass.Setup(_mat, radius, _persist);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_mat);
            _persist?.Release();
            _persist = null;
        }

        class Pass : ScriptableRenderPass
        {
            Material _m;
            float _radius;
            RTHandle _dest;
            public Pass() { profilingSampler = new ProfilingSampler("UiGlassBlur"); }
            public void Setup(Material m, float radius, RTHandle dest) { _m = m; _radius = radius; _dest = dest; }

            class PublishData { }

            public override void RecordRenderGraph(RenderGraph rg, ContextContainer frameData)
            {
                var res = frameData.Get<UniversalResourceData>();
                var cam = frameData.Get<UniversalCameraData>();
                if (res.isActiveTargetBackBuffer || _dest == null) return;   // nothing sampleable: panes keep the fallback

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
                var dest = rg.ImportTexture(_dest);
                _m.SetFloat(Radius, _radius);

                rg.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(res.activeColorTexture, half, _m, 0), "UiGlass Down 1/2");
                rg.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(half, quarter, _m, 1), "UiGlass Down 1/4");
                rg.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(quarter, tmp, _m, 2), "UiGlass Blur H");
                rg.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(tmp, dest, _m, 3), "UiGlass Blur V");

                // Publish: reads the result (orders it before the canvases) and turns the glass on.
                using (var b = rg.AddRasterRenderPass<PublishData>("UiGlass Publish", out _))
                {
                    b.UseTexture(dest, AccessFlags.Read);
                    b.AllowPassCulling(false);
                    b.AllowGlobalStateModification(true);
                    b.SetRenderFunc((PublishData d, RasterGraphContext c) => c.cmd.SetGlobalFloat(BlurOn, 1f));
                }
            }
        }
    }
}
