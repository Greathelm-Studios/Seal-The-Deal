using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloseTheDeal.Editor.Dev
{
    /// <summary>Menu-invoked setup for <c>HitDummy.prefab</c> and placing an unsaved dummy in the open scene.</summary>
    public static class HitDummySetup
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Dev/HitDummy.prefab";

        const string CharacterModelPath = "Assets/_Project/Art/Characters/STDMC/STDMC_Toon.prefab";
        const string ProfileFolder = "Assets/_Project/Profiles";
        const string AnimationProfilePath = ProfileFolder + "/DefaultAnimation.asset";
        const string HitProfilePath = ProfileFolder + "/DefaultHitReaction.asset";

        const float BodyHeight = 1.8f, BodyRadius = 0.35f;
        const float PlaceDistance = 3f;
        const float FloorSearchUp = 3f, FloorSearchDown = 10f;

        /// <summary>Builds <c>HitDummy.prefab</c> if unedited since generation.</summary>
        [MenuItem("Close the Deal/Dev/Build Hit Dummy Prefab")]
        public static void BuildPrefab()
        {
            if (GeneratedAssetGuard.MayOverwrite(PrefabPath))
                Build();
        }

        /// <summary>Places a dummy on the floor in front of the active camera (scene is not saved).</summary>
        [MenuItem("Close the Deal/Dev/Place Hit Dummy")]
        public static void Place()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                if (Application.isPlaying)
                {
                    Debug.LogError("[HitDummy] No prefab yet. Stop playing and run Close the Deal > Dev > Build Hit Dummy Prefab.");
                    return;
                }
                prefab = Build();
                if (prefab == null)
                    return;
            }

            if (!FindViewer(out Vector3 eye, out Vector3 ahead))
            {
                Debug.LogError("[HitDummy] No Scene view or game camera to place the dummy in front of.");
                return;
            }

            Vector3 flat = Vector3.ProjectOnPlane(ahead, Vector3.up);
            flat = flat.sqrMagnitude > 0f ? flat.normalized : Vector3.forward;
            Vector3 spot = eye + flat * PlaceDistance;
            // Everything solid but players, the same rule the player's own feet use.
            int solid = ~(1 << GreyboxSceneSetup.PlayerLayer);
            if (Physics.Raycast(spot + Vector3.up * FloorSearchUp, Vector3.down, out RaycastHit floor, FloorSearchUp + FloorSearchDown, solid, QueryTriggerInteraction.Ignore))
                spot = floor.point;
            else
                Debug.LogWarning("[HitDummy] Found no floor under the spot; the dummy is at the viewer's height and will hang there.");

            var dummy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            dummy.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(-flat, Vector3.up));
            if (!Application.isPlaying)
            {
                Undo.RegisterCreatedObjectUndo(dummy, "Place Hit Dummy");
                EditorSceneManager.MarkSceneDirty(dummy.scene);
            }
            Selection.activeGameObject = dummy;
            Debug.Log("[HitDummy] Placed at " + spot.ToString("F2") + ". The scene is not saved.", dummy);
        }

        static bool FindViewer(out Vector3 eye, out Vector3 ahead)
        {
            Camera camera = Application.isPlaying ? Camera.main : null;
            if (camera == null && SceneView.lastActiveSceneView != null)
                camera = SceneView.lastActiveSceneView.camera;
            if (camera == null)
            {
                eye = ahead = Vector3.zero;
                return false;
            }
            eye = camera.transform.position;
            ahead = camera.transform.forward;
            return true;
        }

        static GameObject Build()
        {
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (modelPrefab == null)
            {
                Debug.LogError("[HitDummy] Character model missing at " + CharacterModelPath + "; no dummy built.");
                return null;
            }

            int layer = GreyboxSceneSetup.PlayerLayer;
            // Player layer prevents RagdollWorld from copying the dummy's own capsule as a level collider.
            var root = new GameObject("HitDummy") { layer = layer };
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = BodyHeight;
            capsule.radius = BodyRadius;
            capsule.center = new Vector3(0f, BodyHeight * 0.5f, 0f);

            var graphics = new GameObject("Graphics") { layer = layer };
            graphics.transform.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, graphics.transform);
            model.name = "Body";
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
                part.gameObject.layer = layer;

            var avatar = graphics.GetComponentInChildren<Animator>();
            if (avatar == null || avatar.avatar == null || !avatar.avatar.isHuman)
            {
                Debug.LogError("[HitDummy] The character model has no humanoid Animator; no dummy built.");
                Object.DestroyImmediate(root);
                return null;
            }

            AnimationProfile profile = GreyboxSceneSetup.EnsureAsset<AnimationProfile>(ProfileFolder, AnimationProfilePath);
            var animator = graphics.AddComponent<PlayerAnimator>();
            var serializedAnimator = new SerializedObject(animator);
            GreyboxSceneSetup.SetReference(serializedAnimator, "_profile", profile);
            GreyboxSceneSetup.SetReference(serializedAnimator, "_avatar", avatar);
            serializedAnimator.ApplyModifiedPropertiesWithoutUndo();

            var ragdoll = graphics.AddComponent<PlayerRagdoll>();
            var serializedRagdoll = new SerializedObject(ragdoll);
            GreyboxSceneSetup.SetReference(serializedRagdoll, "_profile", profile);
            GreyboxSceneSetup.SetReference(serializedRagdoll, "_animator", animator);
            GreyboxSceneSetup.SetReference(serializedRagdoll, "_avatar", avatar);
            GreyboxSceneSetup.SetInt(serializedRagdoll, "_solid", ~(1 << layer));
            GreyboxSceneSetup.SetReference(serializedRagdoll, "_hitProfile", GreyboxSceneSetup.EnsureAsset<HitReactionProfile>(ProfileFolder, HitProfilePath));
            serializedRagdoll.ApplyModifiedPropertiesWithoutUndo();

            var dummy = root.AddComponent<HitDummy>();
            var serializedDummy = new SerializedObject(dummy);
            GreyboxSceneSetup.SetReference(serializedDummy, "_ragdoll", ragdoll);
            GreyboxSceneSetup.SetInt(serializedDummy, "_floor", ~(1 << layer));
            serializedDummy.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(prefab);
            Debug.Log("[HitDummy] Wrote " + PrefabPath);
            return prefab;
        }
    }
}
