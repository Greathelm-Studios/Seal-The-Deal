// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Sonnet 5.5 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "create a blood splatter shader that fits in within the games artstyle
//              that paints around objects and ontop of them when hit by these attacks."
//  Modified  : Claude Fable 5.2, 2026-11-15 — also writes the material for the drops in the
//              air (Harvey: "Add a particle effect where blood splatters off of a player").
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Writes the blood material and the blood tuning profile into a Resources folder,
//              where the runtime finds them. Menu-only, and never overwrites one a person edited.
// -----------------------------------------------------------------------------
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Effects;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Builds the blood assets. Menu: <c>Seal the Deal &gt; Art &gt; Build Blood Assets</c>.
    /// </summary>
    /// <remarks>
    /// Both files go in <c>Assets/_Project/Art/Blood/Resources/</c> because the spawner loads
    /// them with Resources.Load, which also keeps the shader in a build. Each is written with
    /// <see cref="GeneratedAssetGuard"/>, so one a person has edited is left alone (AGENTS.md §4).
    /// Touches no scene and no prefab. The blood works without running this (the spawner then uses
    /// the profile defaults and a material made from the shader), but a build needs the material.
    /// </remarks>
    public static class BloodAssets
    {
        const string ShaderName = "Close the Deal/Blood Splat";
        const string BloodFolder = "Assets/_Project/Art/Blood";
        const string ResourcesFolder = BloodFolder + "/Resources";

        /// <summary>Where the material is written.</summary>
        public const string MaterialPath = ResourcesFolder + "/BloodSplat.mat";

        /// <summary>The drop material, loaded at run time by name ("BloodDrop") from Resources.</summary>
        public const string DropMaterialPath = ResourcesFolder + "/BloodDrop.mat";

        const string DropShaderName = "Close the Deal/Blood Drop";

        /// <summary>Where the profile is written.</summary>
        public const string ProfilePath = ResourcesFolder + "/BloodProfile.asset";

        [MenuItem("Seal the Deal/Art/Build Blood Assets")]
        static void BuildFromMenu() => Build();

        /// <summary>Writes the material and the profile if missing or untouched.</summary>
        /// <returns>A one-line report, also sent to the Console.</returns>
        public static string Build()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                string missing = $"[Blood] Shader '{ShaderName}' not found (does BloodSplat.shader have a compile error?); nothing written.";
                Debug.LogError(missing);
                return missing;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder(BloodFolder, "Resources");

            string material = WriteMaterial(shader);
            string profile = WriteProfile();
            string drop = WriteDropMaterial();
            string report = $"[Blood] Material: {material}. Drop material: {drop}. Profile: {profile}.";
            Debug.Log(report);
            return report;
        }

        /// <summary>The material for the drops that fly through the air, beside the splat material.</summary>
        static string WriteDropMaterial()
        {
            Shader shader = Shader.Find(DropShaderName);
            if (shader == null)
                return "shader not found";
            if (!GeneratedAssetGuard.MayOverwrite(DropMaterialPath))
                return "left alone (edited by hand)";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(DropMaterialPath);
            Material material = existing != null ? existing : new Material(shader) { name = "BloodDrop" };
            material.shader = shader;

            var defaults = ScriptableObject.CreateInstance<BloodProfile>();
            material.SetColor("_CutColor", defaults.CutColor);
            Object.DestroyImmediate(defaults);

            if (existing == null)
                AssetDatabase.CreateAsset(material, DropMaterialPath);
            else
                EditorUtility.SetDirty(material);
            GeneratedAssetGuard.MarkGenerated(material);
            return existing == null ? "written" : "refreshed";
        }

        static string WriteMaterial(Shader shader)
        {
            if (!GeneratedAssetGuard.MayOverwrite(MaterialPath))
                return "left alone (edited by hand)";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Material material = existing != null ? existing : new Material(shader) { name = "BloodSplat" };
            material.shader = shader;
            // WHY: every splat is the same cube with its own properties; instancing draws them together.
            material.enableInstancing = true;

            var defaults = ScriptableObject.CreateInstance<BloodProfile>();
            material.SetColor("_Color", defaults.BloodColor);
            material.SetColor("_CutColor", defaults.CutColor);
            Object.DestroyImmediate(defaults);

            if (existing == null)
                AssetDatabase.CreateAsset(material, MaterialPath);
            else
                EditorUtility.SetDirty(material);
            GeneratedAssetGuard.MarkGenerated(material);
            return existing == null ? "written" : "refreshed";
        }

        static string WriteProfile()
        {
            // A profile is designer content: once it exists it is never rebuilt from here.
            if (AssetDatabase.LoadAssetAtPath<BloodProfile>(ProfilePath) != null)
                return "already there, kept";

            var profile = ScriptableObject.CreateInstance<BloodProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            GeneratedAssetGuard.MarkGenerated(profile);
            return "written";
        }
    }
}
