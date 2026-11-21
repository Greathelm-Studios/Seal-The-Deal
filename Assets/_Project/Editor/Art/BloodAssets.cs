using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Effects;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Builds blood materials and BloodProfile in Resources via Close the Deal > Art > Build Blood Assets.</summary>
    public static class BloodAssets
    {
        const string ShaderName = "Close the Deal/Blood Splat";
        const string BloodFolder = "Assets/_Project/Art/Blood";
        const string ResourcesFolder = BloodFolder + "/Resources";

        public const string MaterialPath = ResourcesFolder + "/BloodSplat.mat";
        public const string DropMaterialPath = ResourcesFolder + "/BloodDrop.mat";
        const string DropShaderName = "Close the Deal/Blood Drop";
        public const string ProfilePath = ResourcesFolder + "/BloodProfile.asset";

        [MenuItem("Close the Deal/Art/Build Blood Assets")]
        static void BuildFromMenu() => Build();

        /// <summary>Writes the blood materials and profile if missing or untouched by hand.</summary>
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
