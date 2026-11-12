// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-11
//  Prompt    : Alex: "do what you recommend, as long as it fits the art direction and that
//              2000s corporate future aesthetic" (recommendation: soft building, graphic people)
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Writes the four starter character materials (suit, shirt, skin, shoe) for the
//              Character Toon shader, so there is something to put on a character the day one
//              exists. It is separate from OfficeLook (scene lighting) and KitImport (the
//              building kit) because characters are a third thing with their own shader.
// -----------------------------------------------------------------------------
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Builds the starter character materials. Menu: <c>Close the Deal &gt; Art &gt; Build Character Toon Materials</c>.
    /// </summary>
    /// <remarks>
    /// Each material is written with <see cref="GeneratedAssetGuard"/>: once someone edits one, the
    /// tool leaves it alone and says so (AGENTS.md §4). Tune the look in the materials, not here.
    /// It touches no scene and no prefab.
    /// Alternative considered: hand-authoring the four .mat files. Rejected because the repo's
    /// other art assets are tool-written and fingerprinted, and an unlabelled file would read as
    /// "made by a person" to every other tool.
    /// </remarks>
    public static class CharacterToonMaterials
    {
        /// <summary>Name the shader declares in CharacterToon.shader.</summary>
        public const string ShaderName = "Close the Deal/Character Toon";

        /// <summary>Folder the materials are written to.</summary>
        public const string MaterialFolder = "Assets/_Project/Art/Toon/Materials";

        /// <summary>Asset path of the navy suit material, for tools that dress a character with it.</summary>
        public const string SuitMaterialPath = MaterialFolder + "/Char_Suit_Navy.mat";

        const string ToonFolder = "Assets/_Project/Art/Toon";

        /// <summary>The shader pass that draws the contour; switched off on materials with no contour.</summary>
        const string ContourLightMode = "SRPDefaultUnlit";

        /// <summary>
        /// Contour width for pale materials, screen pixels. WHY 1.5: the bible wants "selective
        /// contour assistance", not ink (§05). Under 1 px it flickers; 2 px and up starts to read
        /// as a comic outline at close range.
        /// </summary>
        const float PaleContourPixels = 1.5f;

        /// <summary>Highlight size on shoe leather, 0–1. Small: one hard streak, per bible §05 "Leather / polished shoes".</summary>
        const float LeatherHighlightSize = 0.22f;

        /// <summary>Highlight strength on shoe leather, 0–1. Below 1 so the streak is pale grey, not paper white.</summary>
        const float LeatherHighlightStrength = 0.7f;

        // ---- Palette (art bible §03; hexes as quoted in ArtSource/Kit/concepts/Office/REQUEST.md) ----

        /// <summary>Deep Navy #111B38: the suit, and the contour colour.</summary>
        static readonly Color DeepNavy = Hex(0x111B38);
        /// <summary>Suit shadow: Deep Navy pushed darker and bluer, so the cut still reads on a near-black suit.</summary>
        static readonly Color SuitShadow = Hex(0x070B1C);
        /// <summary>Paper White #F3F4F4: the shirt.</summary>
        static readonly Color PaperWhite = Hex(0xF3F4F4);
        /// <summary>Shirt shadow: a pale cobalt-grey. WHY not grey: a grey shadow on white reads as dirt; the bible asks for shadows that "shift slightly colder" (§05).</summary>
        static readonly Color ShirtShadow = Hex(0xB4C2E0);
        /// <summary>Near Black #101116: shoe leather.</summary>
        static readonly Color NearBlack = Hex(0x101116);
        /// <summary>Shoe shadow: Near Black, darker.</summary>
        static readonly Color ShoeShadow = Hex(0x06060A);
        /// <summary>
        /// Skin base. PLACEHOLDER: the bible gives no skin hex, only "clean base tone plus one
        /// controlled cool shadow mass". One neutral mid tone so the shader can be judged; the
        /// real range is an art decision.
        /// </summary>
        static readonly Color SkinBase = Hex(0xE3B49C);
        /// <summary>
        /// Skin shadow. PLACEHOLDER, as above: the base greyed and shifted slightly cool. WHY so
        /// little hue shift: the first try (#B58494, a clear mauve) read as a bruise in the
        /// review renders.
        /// </summary>
        static readonly Color SkinShadow = Hex(0xB8979A);

        [MenuItem("Close the Deal/Art/Build Character Toon Materials")]
        static void BuildFromMenu() => Build();

        /// <summary>
        /// Writes any of the four starter materials that are missing or still untouched.
        /// </summary>
        /// <returns>A one-line report of what was written and what was left alone, also sent to the Console.</returns>
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

        /// <summary>Writes one material unless a person has taken it over. Returns 1 if written, 0 if left alone.</summary>
        static int Write(Shader shader, string name, Color baseColour, Color shadowColour, float contourPixels, float highlightSize)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return 0;

            // WHY update in place instead of delete-and-create: deleting gives the material a new
            // GUID, and the greybox tool calls this on every player rebuild. A new GUID each time
            // would break every prefab and scene that already points at the material.
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
            // WHY: a width of 0 still costs a draw call that collapses onto the surface. Disabling
            // the pass removes the draw. Re-enable it in the material's debug Inspector, or delete
            // the material and re-run this tool, if a contour is wanted later.
            material.SetShaderPassEnabled(ContourLightMode, contourPixels > 0f);
            // WHY: characters are the many-instances case (up to 8 players plus ragdolls).
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
