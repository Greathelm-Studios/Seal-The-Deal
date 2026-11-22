using CloseTheDeal.Editor.Lanes;
using CloseTheDeal.Integration;
using CloseTheDeal.Items;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Adds loose guns, gun pickup handling and the item prompt to KitOffice. Adds only what is missing; never saves the scene.</summary>
    public static class KitOfficeGunPickups
    {
        const string ScenePath = "Assets/_Project/Scenes/KitOffice.unity";
        const string GunMapPath = "Assets/_Project/Profiles/Integration/GunPickupMap.asset";
        const string ItemFolder = "Assets/_Project/Profiles/Items/";
        const string RootName = "Gun Pickups";

        static readonly (string item, string label, Vector3 at, float yaw)[] DefaultSpots =
        {
            ("Item_Pistol", "Pistol Spot 1", new Vector3(7f, 0f, -3f), 20f),
            ("Item_Pistol", "Pistol Spot 2", new Vector3(7f, 0f, -7f), -30f),
            ("Item_AR15", "AR-15 Spot 1", new Vector3(5.5f, 0f, -3f), 75f),
            ("Item_AR15", "AR-15 Spot 2", new Vector3(5.5f, 0f, -7f), 110f),
        };

        [MenuItem("Seal the Deal/Art/Add Gun Pickups To KitOffice")]
        public static void AddToOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Kit Office] Stop Play Mode first; no gun pickups were added.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning($"[Kit Office] Open {ScenePath} first; the open scene is '{scene.path}', so no gun pickups were added.");
                return;
            }

            var map = AssetDatabase.LoadAssetAtPath<GunPickupMap>(GunMapPath);
            if (map == null)
            {
                Debug.LogError($"[Kit Office] No gun pickup map at {GunMapPath}; run Seal the Deal > Lanes > Set Up Integration first.");
                return;
            }

            GameObject root = FindRoot();
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Add gun pickups");
                Debug.Log($"[Kit Office] Added the '{RootName}' object.");
            }

            GunPickups pickups = Ensure<GunPickups>(root);
            FillMap(pickups, map);
            Ensure<SharedHands>(root);
            LooseGunSpawns spawns = Ensure<LooseGunSpawns>(root);
            if (spawns.GetComponent<NetworkObject>() == null)
            {
                spawns.gameObject.AddComponent<NetworkObject>();
                Debug.Log($"[Kit Office] Added a NetworkObject to {spawns.name}.");
            }

            FillMap(spawns, map);
            AddSpots(spawns, spawns.transform);

            if (Object.FindAnyObjectByType<ItemHud>(FindObjectsInactive.Include) == null)
            {
                PickupsLaneSetup.ItemHudSetup.AddToOpenScene();
                Debug.Log("[Kit Office] Added the item HUD (the pick-up prompt).");
            }
            else
            {
                Debug.Log("[Kit Office] Item HUD already present.");
            }

            try
            {
                Debug.Log(KitOfficeScene.GiveNetworkSceneIds(scene));
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Kit Office] Could not give the new NetworkObject a scene ID: " + e.Message);
            }

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Kit Office] Gun pickups set up. The scene is changed but not saved.");
        }

        static T Ensure<T>(GameObject root) where T : Component
        {
            T found = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (found != null)
            {
                Debug.Log($"[Kit Office] {typeof(T).Name} already present.");
                return found;
            }

            Debug.Log($"[Kit Office] Added {typeof(T).Name}.");
            return root.AddComponent<T>();
        }

        static GameObject FindRoot()
        {
            var pickups = Object.FindAnyObjectByType<GunPickups>(FindObjectsInactive.Include);
            if (pickups != null)
                return pickups.gameObject;
            var spawns = Object.FindAnyObjectByType<LooseGunSpawns>(FindObjectsInactive.Include);
            if (spawns != null)
                return spawns.gameObject;
            var hands = Object.FindAnyObjectByType<SharedHands>(FindObjectsInactive.Include);
            return hands != null ? hands.gameObject : null;
        }

        static void FillMap(Object target, GunPickupMap map)
        {
            var so = new SerializedObject(target);
            SerializedProperty field = so.FindProperty("_map");
            if (field.objectReferenceValue != null)
                return;

            field.objectReferenceValue = map;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Kit Office] Pointed {target.GetType().Name} at the gun pickup map.");
        }

        static void AddSpots(LooseGunSpawns spawns, Transform root)
        {
            var so = new SerializedObject(spawns);
            SerializedProperty list = so.FindProperty("_spots");
            if (list.arraySize > 0)
            {
                Debug.Log($"[Kit Office] Loose gun spots already set ({list.arraySize}); left as they are.");
                return;
            }

            list.arraySize = DefaultSpots.Length;
            for (int i = 0; i < DefaultSpots.Length; i++)
            {
                var spot = new GameObject(DefaultSpots[i].label).transform;
                spot.SetParent(root, false);
                spot.SetPositionAndRotation(DefaultSpots[i].at, Quaternion.Euler(0f, DefaultSpots[i].yaw, 0f));
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Place").objectReferenceValue = spot;
                entry.FindPropertyRelative("Gun").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemFolder + DefaultSpots[i].item + ".asset");
                Debug.Log($"[Kit Office] Added loose gun spot '{DefaultSpots[i].label}'.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
