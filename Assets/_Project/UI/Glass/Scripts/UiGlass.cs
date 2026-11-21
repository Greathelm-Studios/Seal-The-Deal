using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Drives CloseTheDeal/UI/Glass panes: writes pixel size into uv1 and tracks active panes for blur.</summary>
    [RequireComponent(typeof(Graphic))]
    [ExecuteAlways]
    [AddComponentMenu("Seal the Deal/UI Glass")]
    public class UiGlass : BaseMeshEffect
    {
        public enum Preset { Clear, Frosted, HudChip, NoBlur }

        [SerializeField] Preset preset = Preset.Frosted;
        [Tooltip("Vertical mullion bars (0-4), for a curtain-wall pane.")]
        [Range(0, 4)] [SerializeField] int mullions;
        [Tooltip("Cyan edge-light along the bottom (focus / you).")]
        [SerializeField] bool edgeLight;
        [Tooltip("Off when another script owns this Graphic's material (e.g. Lane 5's UiGlassPane.GlassMaterial): " +
                 "then UiGlass only writes the pane size and keeps the blur running.")]
        [SerializeField] bool ownsMaterial = true;

        float _lastScale = -1f;

        Material _instance;
        static readonly int Mullions = Shader.PropertyToID("_Mullions");
        static readonly int EdgeLight = Shader.PropertyToID("_EdgeLight");

        public Preset PresetKind { get => preset; set { preset = value; Apply(); } }

        protected override void OnEnable()
        {
            base.OnEnable();
            UiGlassBlurFeature.ActivePanes++;
            if (Application.isPlaying) UiGlassBackdrop.AttachToTitles();   // a title built after scene load
            Apply();
        }

        protected override void OnDisable()
        {
            UiGlassBlurFeature.ActivePanes = Mathf.Max(0, UiGlassBlurFeature.ActivePanes - 1);
            if (_instance != null) { Discard(_instance); _instance = null; }
            base.OnDisable();
        }

        static void Discard(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>Assign the preset material (or a per-pane copy when mullions / edge-light are set).</summary>
        public void Apply()
        {
            var g = graphic;
            if (g == null) return;
            var canvas = g.canvas;
            if (canvas != null) canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            if (!ownsMaterial) { g.SetVerticesDirty(); return; }
            var shared = UiGlassMaterials.Get(preset);
            if (shared == null) return;
            if (mullions == 0 && !edgeLight)
            {
                if (_instance != null) { Discard(_instance); _instance = null; }
                g.material = shared;
            }
            else
            {
                if (_instance == null) _instance = new Material(shared) { hideFlags = HideFlags.HideAndDontSave };
                _instance.CopyPropertiesFromMaterial(shared);
                _instance.SetFloat(Mullions, mullions);
                _instance.SetFloat(EdgeLight, edgeLight ? 0.9f : 0f);
                g.material = _instance;
            }
            g.SetMaterialDirty();
        }

        // The pane size in uv1 is in screen pixels: rebuild it when the canvas scale changes (resolution, scaler).
        void LateUpdate()
        {
            var canvas = graphic != null ? graphic.canvas : null;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            if (!Mathf.Approximately(scale, _lastScale)) { _lastScale = scale; graphic.SetVerticesDirty(); }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var r = ((RectTransform)transform).rect;
            var canvas = graphic.canvas;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            var size = new Vector2(Mathf.Abs(r.width) * scale, Mathf.Abs(r.height) * scale);
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.uv0 = new Vector4(Mathf.Approximately(r.width, 0) ? 0 : (v.position.x - r.xMin) / r.width,
                                    Mathf.Approximately(r.height, 0) ? 0 : 1f - (v.position.y - r.yMin) / r.height, 0, 0);
                v.uv1 = new Vector4(size.x, size.y, 0, 0);
                vh.SetUIVertex(v, i);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            // Apply may destroy the per-pane material copy, which is not allowed inside OnValidate.
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Apply(); };
        }
#endif
    }

    /// <summary>Loads the preset materials from Resources/UiGlass (Clear, Frosted, HudChip, NoBlur).</summary>
    public static class UiGlassMaterials
    {
        static readonly Material[] Cache = new Material[4];
        public static Material Get(UiGlass.Preset p)
        {
            int i = (int)p;
            if (Cache[i] == null) Cache[i] = Resources.Load<Material>("UiGlass/Glass" + p);
            return Cache[i];
        }
    }
}
