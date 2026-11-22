using System.IO;
using CloseTheDeal.Audio;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.Audio
{
    /// <summary>Creates the designer-owned SpatialAudioProfile asset if one does not already exist.</summary>
    public static class SpatialAudioProfileMenu
    {
        const string AssetPath = "Assets/_Project/Resources/Audio/SpatialAudioProfile.asset";

        [MenuItem("Seal the Deal/Audio/Create Spatial Audio Profile")]
        public static void CreateProfile()
        {
            SpatialAudioProfile existing = AssetDatabase.LoadAssetAtPath<SpatialAudioProfile>(AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"[Audio] SpatialAudioProfile already exists at {AssetPath}; left untouched.");
                return;
            }

            string dir = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            SpatialAudioProfile profile = ScriptableObject.CreateInstance<SpatialAudioProfile>();
            AssetDatabase.CreateAsset(profile, AssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
            Debug.Log($"[Audio] Created {AssetPath}.");
        }
    }
}
