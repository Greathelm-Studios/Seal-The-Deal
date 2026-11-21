using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Builds the four starter character toon materials.</summary>
    public static class CharacterToonMaterials
    {
        /// <summary>Shader name declared in CharacterToon.shader.</summary>
        public const string ShaderName = "Seal the Deal/Character Toon";

        /// <summary>Folder where starter character materials are written.</summary>
        public const string MaterialFolder = "Assets/_Project/Art/Toon/Materials";

        /// <summary>Asset path of the navy suit material.</summary>
        public const string SuitMaterialPath = MaterialFolder + "/Char_Suit_Navy.mat";

        const string ToonFolder = "Assets/_Project/Art/Toon";
        const string ContourLightMode = "SRPDefaultUnlit";
        const float PaleContourPixels = 1.5f;
        const float LeatherHighlightSize = 0.22f;
        const float LeatherHighlightStrength = 0.7f;

        static readonly Color DeepNavy = Hex(0x111B38);
        static readonly Color SuitShadow = Hex(0x070B1C);
        static readonly Color PaperWhite = Hex(0xF3F4F4);
        static readonly Color ShirtShadow = Hex(0xB4C2E0);
        static readonly Color NearBlack = Hex(0x101116);
        static readonly Color ShoeShadow = Hex(0x06060A);
        static readonly Color SkinBase = Hex(0xE3B49C);
        static readonly Color SkinShadow = Hex(0xB8979A);

        [MenuItem("Seal the Deal/Art/Build Character Toon Materials")]
        static void BuildFromMenu() => Build();

        /// <summary>Writes any of the four starter materials that are missing or untouched.</summary>
        public static string Build()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                string missing = $"[Character Toon] Shader '{ShaderName}' not found; nothing written.";
                Debug.LogError(missing);
                return missing;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder(ToonFolder, "Materials");

            int written = 0;
            written += Write(shader, "Char_Suit_Navy", DeepNavy, SuitShadow, contourPixels: 0f, highlightSize: 0f);
            written += Write(shader, "Char_Shirt_White", PaperWhite, ShirtShadow, PaleContourPixels, highlightSize: 0f);
            written += Write(shader, "Char_Skin", SkinBase, SkinShadow, PaleContourPixels, highlightSize: 0f);
            written += Write(shader, "Char_Shoe_Leather", NearBlack, ShoeShadow, contourPixels: 0f, LeatherHighlightSize);

            const int total = 4;
            string report = $"[Character Toon] Wrote {written} of {total} materials to {MaterialFolder}; {total - written} left alone (edited by hand).";
            Debug.Log(report);
            return report;
        }

        static int Write(Shader shader, string name, Color baseColour, Color shadowColour, float contourPixels, float highlightSize)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return 0;

            // Update in place to keep asset GUIDs stable across player rebuilds.
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew)
                material = new Material(shader) { name = name };
            else
                material.shader = shader;

            material.SetColor("_BaseColor", baseColour);
            material.SetColor("_ShadowColor", shadowColour);
            material.SetColor("_OutlineColor", DeepNavy);
            material.SetFloat("_OutlineWidth", contourPixels);
            material.SetFloat("_HighlightSize", highlightSize);
            material.SetColor("_HighlightColor", new Color(PaperWhite.r, PaperWhite.g, PaperWhite.b, LeatherHighlightStrength));
            // Disable the contour pass when width is 0 to skip the extra draw call.
            material.SetShaderPassEnabled(ContourLightMode, contourPixels > 0f);
            material.enableInstancing = true;

            if (isNew)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            GeneratedAssetGuard.MarkGenerated(material);
            return 1;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
