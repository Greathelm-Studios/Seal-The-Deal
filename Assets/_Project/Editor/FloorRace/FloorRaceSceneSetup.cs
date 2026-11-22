using CloseTheDeal.Combat;
using CloseTheDeal.Editor.Art;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Editor.Lanes;
using CloseTheDeal.Editor.UI;
using CloseTheDeal.FloorRace;
using CloseTheDeal.Integration;
using CloseTheDeal.MatchMode;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.FloorRace
{
    /// <summary>Builds FloorRace.unity: the play rig, the match and its HUD, and the Floor Race object that generates the floors at runtime.</summary>
    public static class FloorRaceSceneSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/FloorRace.unity";
        const string RaceProfilePath = FloorLayoutPreview.ProfileFolder + "/FloorRace.asset";
        const string KitPath = FloorLayoutPreview.ProfileFolder + "/FloorKit.asset";
        const string ItemFolder = "Assets/_Project/Prefabs/Items/";
        const string PropFolder = "Assets/_Project/Prefabs/Props/";
        const string GunMapPath = "Assets/_Project/Profiles/Integration/GunPickupMap.asset";
        const string PistolPath = "Assets/_Project/Profiles/Weapons/Pistol.asset";
        const string RaceObjectName = "Floor Race";
        // Fallback spawns only: the mode places players at the generated floor's starts.
        static readonly Vector3 SpawnA = new(6f, 0.1f, -6f), SpawnB = new(34f, 0.1f, -26f);
        static readonly Vector3 CameraAt = new(20f, 30f, -40f), CameraLook = new(20f, 0f, -16f);
        static readonly (string prefab, int weight)[] DefaultLoot =
        {
            ("Pistol", 3), ("AR15", 2), ("GrenadeCase", 1), ("FireExtinguisher", 1), ("WetFloorSign", 1)
        };

        [MenuItem("Seal the Deal/Floor Race/Build Scene")]
        static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build Floor Race Scene", "Stop Play Mode first.", "OK");
                return;
            }

            bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            if (exists && !EditorUtility.DisplayDialog("Build Floor Race Scene",
                    "FloorRace.unity already exists. Rebuilding replaces everything in it, including anything placed or changed by hand.",
                    "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Debug.Log(BuildAndSave());
        }

        /// <summary>Builds and saves the scene without asking. The menu item asks first.</summary>
        public static string BuildAndSave()
        {
            Debug.Log(FloorBuildMeasure.FillKit());
            FloorRaceProfile profile = EnsureRaceProfile();
            string profilePath = AssetDatabase.GetAssetPath(profile);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera();
            GreyboxSceneSetup.EnsurePlayRig(SpawnA, SpawnB);
            EditorSceneManager.SaveScene(scene, ScenePath);
            OfficeLook.Apply(askFirst: false);

            GameModeLaneSetup.AddMatchWithoutTowers();
            AddStartingLoadout();
            // Health, ammo and hit marks; it leaves the pistol-only loadout above alone.
            ShootingLaneSetup.AddToOpenScene();
            PickupsLaneSetup.BuildPromptCanvas();
            AddIntegration();
            // A new scene unloads unused assets; references taken before it would save as "none".
            AddRace(AssetDatabase.LoadAssetAtPath<FloorRaceProfile>(profilePath));
            FloorRaceHudSetup.AddToOpenScene();
            // Moves the match section (the host's Start button) into the kit's in-game menu.
            UiKitSetup.AddToOpenScene();

            EditorSceneManager.SaveScene(scene, ScenePath);
            string ids = KitOfficeScene.GiveNetworkSceneIds(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return $"[Floor Race] Built {ScenePath}. {ids}";
        }

        static FloorRaceProfile EnsureRaceProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<FloorRaceProfile>(RaceProfilePath);
            if (existing != null)
                return existing;

            FloorRaceProfile profile = FloorLayoutPreview.EnsureAsset<FloorRaceProfile>(RaceProfilePath);
            profile.Floors = FloorLayoutPreview.EnsureGenProfile();
            profile.Loot = new FloorRaceProfile.LootEntry[DefaultLoot.Length];
            for (int i = 0; i < DefaultLoot.Length; i++)
            {
                profile.Loot[i].Prefab = Load(ItemFolder + DefaultLoot[i].prefab + ".prefab");
                profile.Loot[i].Weight = DefaultLoot[i].weight;
            }
            profile.ChairProp = Load(PropFolder + "Chair.prefab");
            profile.MonitorProp = Load(PropFolder + "Monitor.prefab");
            profile.CabinetProp = Load(PropFolder + "Cabinet.prefab");
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        static NetworkObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
            if (prefab == null)
                Debug.LogWarning($"[Floor Race] No networked prefab at {path}; that loot or prop slot is left empty.");
            return prefab;
        }

        static void AddCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            // The player camera takes this object over in play; without a listener here the scene is silent.
            go.AddComponent<AudioListener>();
            go.transform.SetPositionAndRotation(CameraAt, Quaternion.LookRotation(CameraLook - CameraAt));
        }

        static void AddStartingLoadout()
        {
            if (Object.FindAnyObjectByType<StartingLoadout>() != null)
                return;

            var go = new GameObject("Starting Loadout");
            go.AddComponent<StartingLoadout>().Weapons = new[] { AssetDatabase.LoadAssetAtPath<WeaponDefinition>(PistolPath) };
        }

        static void AddIntegration()
        {
            var go = new GameObject("Integration");
            go.AddComponent<SharedHands>();
            var guns = go.AddComponent<GunPickups>();
            var serialized = new SerializedObject(guns);
            GreyboxSceneSetup.SetReference(serialized, "_map", AssetDatabase.LoadAssetAtPath<GunPickupMap>(GunMapPath));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddRace(FloorRaceProfile profile)
        {
            var go = new GameObject(RaceObjectName);
            go.AddComponent<NetworkObject>();
            var builder = go.AddComponent<FloorBuilder>();
            var serializedBuilder = new SerializedObject(builder);
            GreyboxSceneSetup.SetReference(serializedBuilder, "_kit", AssetDatabase.LoadAssetAtPath<FloorKit>(KitPath));
            serializedBuilder.ApplyModifiedPropertiesWithoutUndo();

            var director = go.AddComponent<FloorRaceDirector>();
            var serialized = new SerializedObject(director);
            GreyboxSceneSetup.SetReference(serialized, "_profile", profile);
            GreyboxSceneSetup.SetReference(serialized, "_builder", builder);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
