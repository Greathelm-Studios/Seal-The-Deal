using System.IO;
using CloseTheDeal.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// Builds the UI glass assets: the four preset materials (Resources/UiGlass) and, only when asked, adds the
    /// blur renderer feature to a renderer asset (a graphics-settings change: name it in the commit subject).
    /// Shell: Unity -batchmode -quit -projectPath <worktree> -executeMethod CloseTheDeal.Editor.UI.UiGlassSetup.BuildMaterialsFromCommandLine
    /// Re-running is safe: materials are updated in place, the feature is added once.
    /// </summary>
    public static class UiGlassSetup
    {
        const string Dir = "Assets/_Project/UI/Glass";
        const string ResDir = Dir + "/Resources/UiGlass";

        [MenuItem("Close the Deal/UI/Build Glass Materials")]
        public static void BuildMaterialsFromMenu() => BuildMaterials();
        public static void BuildMaterialsFromCommandLine() { BuildMaterials(); EditorApplication.Exit(0); }

        // engine (Linear) alphas, re-derived from the SVG mockups: see docs/systems/UI_GLASS.md
        // name, tint alpha, no-blur alpha, streak, spec, highlight, border alpha, blur mix
        static readonly (string name, float tint, float fallback, float streak, float spec, float hi, float border, float mix)[] Presets =
        {
            ("Clear",   0.14f, 0.38f, 0.22f, 0.50f, 0.85f, 0.30f, 1f),   // decorative strips, no text on it
            ("Frosted", 0.70f, 0.80f, 0.22f, 0.22f, 0.95f, 0.38f, 1f),   // menus, modals, panes that carry text
            ("HudChip", 0.45f, 0.72f, 0.16f, 0.22f, 0.95f, 0.38f, 1f),   // small HUD chips over the world
            ("NoBlur",  0.72f, 0.72f, 0.10f, 0.18f, 0.95f, 0.38f, 0f),   // forced fallback preset
        };

        public static void BuildMaterials()
        {
            Directory.CreateDirectory(ResDir);
            var shader = Shader.Find("CloseTheDeal/UI/Glass");
            if (shader == null) { Debug.LogError("UiGlassSetup: shader CloseTheDeal/UI/Glass not found"); return; }
            foreach (var p in Presets)
            {
                string path = $"{ResDir}/Glass{p.name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
                m.shader = shader;
                m.SetFloat("_TintAlpha", p.tint);
                m.SetFloat("_FallbackAlpha", p.fallback);
                m.SetFloat("_StreakAlpha", p.streak);
                m.SetFloat("_SpecAlpha", p.spec);
                m.SetFloat("_Highlight", p.hi);
                m.SetFloat("_BorderAlpha", p.border);
                m.SetFloat("_BlurMix", p.mix);
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("UiGlassSetup: materials built in " + ResDir);
        }

        /// <summary>Adds UiGlassBlurFeature to the renderer asset at path (once).</summary>
        public static void AddFeature(string rendererPath)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (data == null) { Debug.LogError("UiGlassSetup: no renderer at " + rendererPath); return; }
            foreach (var f in data.rendererFeatures)
                if (f is UiGlassBlurFeature) { Debug.Log("UiGlassSetup: feature already on " + rendererPath); return; }
            var feature = ScriptableObject.CreateInstance<UiGlassBlurFeature>();
            feature.name = "UiGlassBlur";
            var so0 = new SerializedObject(feature);
            so0.FindProperty("shader").objectReferenceValue = Shader.Find("Hidden/CloseTheDeal/UiGlassBlur");
            so0.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
            var so = new SerializedObject(data);
            var feats = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            feats.arraySize++;
            feats.GetArrayElementAtIndex(feats.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("UiGlassSetup: added UiGlassBlurFeature to " + rendererPath);
        }

        public static void AddFeatureToPcFromCommandLine()
        {
            AddFeature("Assets/Settings/PC_Renderer.asset");
            EditorApplication.Exit(0);
        }
    }
}
