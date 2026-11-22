// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-21
//  Prompt    : Alex, on the adapted pistol in the hand: "the shading doesnt match" (the arm is
//              toon-shaded, the gun was plain URP Lit).
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Writes the pistol's materials on the Character Toon shader and maps Pistol.fbx's
//              named materials onto them, so the held gun is drawn like the hand holding it. A
//              separate file from CharacterToonMaterials because weapons are their own asset set
//              with their own palette, and that tool runs on every player rebuild.
// -----------------------------------------------------------------------------
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Builds the pistol's toon materials. Menu: <c>Seal the Deal &gt; Art &gt; Build Weapon Toon Materials</c>.
    /// </summary>
    /// <remarks>
    /// Each material is fingerprinted with <see cref="GeneratedAssetGuard"/>: once someone edits one,
    /// the tool leaves it alone and says so (AGENTS.md §4). Tune the look in the materials. The model's
    /// import settings get one remap per material (the FBX's material name to the toon material); no
    /// scene or prefab is touched.
    /// Alternative considered: URP Lit with the same colours (what the FBX import gives). Rejected:
    /// next to the toon-shaded hand it reads as a different art style (Alex, 2026-11-21).
    /// </remarks>
    public static class WeaponToonMaterials
    {
        /// <summary>The pistol model whose material slots are remapped.</summary>
        public const string PistolPath = "Assets/_Project/Art/Weapons/Models/Pistol.fbx";
        /// <summary>The hand modelled round the pistol's grip, shown instead of the character's own hand.</summary>
        public const string PistolHandPath = "Assets/_Project/Art/Weapons/Models/PistolGripHand.fbx";
        /// <summary>The left hand modelled cupped round the rifle's handguard (pistol_grip_hand.py with MIRROR=1).</summary>
        public const string RifleSupportHandPath = "Assets/_Project/Art/Weapons/Models/RifleSupportHand.fbx";
        /// <summary>The AR-15 model (ArtSource/Weapons/ar15_adapt.py).</summary>
        public const string RiflePath = "Assets/_Project/Art/Weapons/Models/AR15.fbx";
        /// <summary>The character's skin material; the grip hand's material takes its colours, so it matches the arm.</summary>
        const string SkinMaterialPath = "Assets/_Project/Art/Characters/STDMC/Materials/STDMC_lambert2_Toon.mat";
        /// <summary>The grip hand's material name in its FBX.</summary>
        const string HandSkinName = "Hand_Skin";
        /// <summary>Folder the materials are written to.</summary>
        public const string MaterialFolder = "Assets/_Project/Art/Weapons/Materials";
        const string WeaponsFolder = "Assets/_Project/Art/Weapons";
        /// <summary>The Character Toon shader the arm uses.</summary>
        const string ShaderPath = "Assets/_Project/Art/Toon/CharacterToon.shader";
        /// <summary>The shader pass that draws the contour; switched off, as the gun has none.</summary>
        const string ContourLightMode = "SRPDefaultUnlit";

        /// <summary>
        /// Hard highlight size on metal parts, 0–1. WHY: one streak along the silver slide reads as polished
        /// metal rather than grey plastic; matches the leather streak on shoes (0.22).
        /// </summary>
        const float MetalHighlightSize = 0.2f;
        /// <summary>
        /// Contour width, screen pixels. WHY 1.5: the value the character's pale parts use
        /// (CharacterToonMaterials.PaleContourPixels), so the gun's line weight matches the hand holding it.
        /// </summary>
        const float ContourPixels = 1.5f;
        /// <summary>
        /// Contour colour. WHY near-black, not the hand's navy: the frame is deep navy, and a navy line
        /// round it would vanish; a darker ink still reads round the frame and stays clear on silver and aqua.
        /// </summary>
        static readonly Color Ink = Hex(0x05070F);

        /// <summary>Highlight strength on metal, 0–1. Below 1 so the streak is grey, not paper white.</summary>
        const float MetalHighlightStrength = 0.45f;

        // ---- Silver and navy (Alex, 2026-11-21; ArtSource/Weapons/README.md), art bible palette; shadows colder ----
        static readonly Color Silver = Hex(0xC8D2D9);
        static readonly Color SilverShadow = Hex(0x7F8DA3);
        static readonly Color DeepNavy = Hex(0x111B38);
        static readonly Color NavyShadow = Hex(0x070B1C);
        static readonly Color Mist = Hex(0xA8BFD1);
        static readonly Color MistShadow = Hex(0x6E86A6);
        static readonly Color Aqua = Hex(0x45BFC7);
        static readonly Color AquaShadow = Hex(0x23788F);
        static readonly Color HankoRed = Hex(0xC7473E);
        static readonly Color HankoShadow = Hex(0x8A2A33);

        /// <summary>Material names an earlier pistol model used; their remaps and materials are removed.</summary>
        static readonly string[] Retired = { "Pistol_Barrel" };

        [MenuItem("Seal the Deal/Art/Build Weapon Toon Materials")]
        static void BuildFromMenu() => Build();

        /// <summary>Writes the materials and remaps the pistol model onto them.</summary>
        /// <returns>A one-line report, also logged.</returns>
        public static string Build()
        {
            // WHY by file, not Shader.Find(name): on 2026-11-21 the shader file was still named
            // "Close the Deal/Character Toon" while CharacterToonMaterials.ShaderName said "Seal the Deal/...",
            // so a lookup by name found nothing. The file path does not depend on that rename.
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                string missing = $"[Weapon Toon] No shader at {ShaderPath}; nothing written.";
                Debug.LogError(missing);
                return missing;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder(WeaponsFolder, "Materials");

            var importer = AssetImporter.GetAtPath(PistolPath) as ModelImporter;
            int written = 0, total = 0;
            void One(string name, Color baseColour, Color shadowColour, float highlight)
            {
                total++;
                Material material = Write(shader, name, baseColour, shadowColour, highlight, ref written);
                if (importer != null && material != null)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
            }

            One("Pistol_Slide", Silver, SilverShadow, MetalHighlightSize);
            One("Pistol_Frame", DeepNavy, NavyShadow, 0f);
            One("Pistol_Grip", Mist, MistShadow, 0f);
            One("Pistol_Trigger", Silver, SilverShadow, MetalHighlightSize);
            One("Pistol_Sights", Aqua, AquaShadow, 0f);
            One("Pistol_MagBase", Aqua, AquaShadow, 0f);
            One("Pistol_Logo", Aqua, AquaShadow, 0f);
            One("Pistol_Hanko", HankoRed, HankoShadow, 0f);

            foreach (string old in Retired)
            {
                importer?.RemoveRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), old));
                string oldPath = $"{MaterialFolder}/{old}.mat";
                // Only a material this tool still owns is deleted; an edited one is left for a person.
                if (AssetDatabase.LoadAssetAtPath<Material>(oldPath) != null && GeneratedAssetGuard.MayOverwrite(oldPath))
                    AssetDatabase.DeleteAsset(oldPath);
            }

            if (importer != null)
            {
                importer.SaveAndReimport();
                // Smoothed normals for the contour, in a copy of each mesh beside the model
                // (Art/Weapons/Models/Outline/); the viewmodel builder hangs those copies on the gun.
                Debug.Log(CharacterOutlineBake.Bake(PistolPath));
            }

            string hand = MapHand(shader, PistolHandPath, ref written, ref total) + " " + MapHand(shader, RifleSupportHandPath, ref written, ref total);
            string rifle = MapRifle(shader, ref written, ref total);
            string report = $"[Weapon Toon] Wrote {written} of {total} materials to {MaterialFolder}; {total - written} left alone (edited by hand)." +
                            (importer == null ? $" No model at {PistolPath}; nothing remapped." : " Pistol.fbx remapped.") + " " + hand + " " + rifle;
            Debug.Log(report);
            return report;
        }

        /// <summary>
        /// Writes the grip hand's material (the character's skin colours, with the gun's line art) and bakes its
        /// contour normals. WHY its own material and not the skin's: the skin's contour is 3 px, and on a hand
        /// wrapped round a 1.5 px gun it read as heavier line art than the gun (Alex, 2026-11-21).
        /// </summary>
        static string MapHand(Shader shader, string handPath, ref int written, ref int total)
        {
            string file = System.IO.Path.GetFileName(handPath);
            var importer = AssetImporter.GetAtPath(handPath) as ModelImporter;
            if (importer == null)
                return $"No hand at {handPath}.";
            var skin = AssetDatabase.LoadAssetAtPath<Material>(SkinMaterialPath);
            if (skin == null)
                return $"No skin material at {SkinMaterialPath}; grip hand left on its own material.";

            total++;
            Material hand = Write(shader, "Pistol_Hand", skin.GetColor("_BaseColor"), skin.GetColor("_ShadowColor"), 0f, ref written);
            if (hand == null)
                return "Grip hand material missing and not writable.";
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), HandSkinName), hand);
            importer.SaveAndReimport();
            Debug.Log(CharacterOutlineBake.Bake(handPath));
            return $"{file} put on Pistol_Hand (skin colours, gun line art).";
        }

        /// <summary>Writes the AR-15's materials in the pistol's palette, maps AR15.fbx onto them and bakes its contour normals.</summary>
        static string MapRifle(Shader shader, ref int written, ref int total)
        {
            var importer = AssetImporter.GetAtPath(RiflePath) as ModelImporter;
            if (importer == null)
                return $"No rifle at {RiflePath}.";

            (string name, Color colour, Color shadow, float highlight)[] parts =
            {
                ("AR15_Metal", Silver, SilverShadow, MetalHighlightSize),
                ("AR15_Receiver", DeepNavy, NavyShadow, 0f),
                ("AR15_Furniture", Mist, MistShadow, 0f),
                ("AR15_Magazine", Aqua, AquaShadow, 0f),
            };
            foreach (var part in parts)
            {
                total++;
                Material material = Write(shader, part.name, part.colour, part.shadow, part.highlight, ref written);
                if (material != null)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), part.name), material);
            }

            importer.SaveAndReimport();
            Debug.Log(CharacterOutlineBake.Bake(RiflePath));
            return "AR15.fbx remapped.";
        }

        /// <summary>Writes one material unless a person has taken it over; returns it either way (null only if missing and untouchable).</summary>
        static Material Write(Shader shader, string name, Color baseColour, Color shadowColour, float highlight, ref int written)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return material;

            // WHY update in place: a new asset would get a new GUID and break the model's remap.
            bool isNew = material == null;
            if (isNew)
                material = new Material(shader) { name = name };
            else
                material.shader = shader;

            material.SetColor("_BaseColor", baseColour);
            material.SetColor("_ShadowColor", shadowColour);
            material.SetFloat("_HighlightSize", highlight);
            material.SetColor("_HighlightColor", new Color(1f, 1f, 1f, MetalHighlightStrength));
            // Line art round every part, like the hand's (Alex, 2026-11-21: "the gun has no line art").
            // It needs the outline-normal bake (Build bakes it) or the line splits at every hard edge.
            material.SetFloat("_OutlineWidth", ContourPixels);
            material.SetColor("_OutlineColor", Ink);
            material.SetShaderPassEnabled(ContourLightMode, true);
            // WHY: up to 8 players each hold one, plus the first-person copy.
            material.enableInstancing = true;

            if (isNew)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            GeneratedAssetGuard.MarkGenerated(material);
            written++;
            return material;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
