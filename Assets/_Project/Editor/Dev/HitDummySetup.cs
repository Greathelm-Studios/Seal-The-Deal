// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "a test dummy in the scene to test animations of being hit by certain
//              powers of weapons (bullets bombs explosions props, etc)", which should "use the
//              idle animations we have"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Builds the hit-test dummy prefab and places it in the open scene, from menu
//              items. Its own file, not part of the greybox tool, because the dummy is a dev
//              aid with no network identity and the greybox tool's files belong to another
//              lane; it borrows that tool's helpers instead of copying them.
// -----------------------------------------------------------------------------
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloseTheDeal.Editor.Dev
{
    /// <summary>
    /// Menu tools for the hit-test dummy: a copy of the player's body that stands in the idle
    /// with the same animator and active ragdoll, but has no motor, no input and no network
    /// identity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs only from its menu items (AGENTS.md §4). The prefab carries the greybox tools'
    /// fingerprint label, so a rebuild leaves it alone once a person has edited it.
    /// </para>
    /// <para>
    /// WHY a prefab plus a "place" menu item rather than an object saved into a scene: the kit
    /// scene is rebuilt from its own builder, which drops anything placed by hand. A dummy can
    /// be put back in one click, or placed by that builder from the same prefab.
    /// </para>
    /// <para>
    /// Alternative considered: instantiating <c>Player.prefab</c> and stripping its network
    /// parts. It would follow the player's prefab automatically, but an unspawned
    /// <see cref="PlayerMotor"/> reports "not on the floor", and removing components from a
    /// nested copy of a networked prefab breaks FishNet's prefab list.
    /// </para>
    /// </remarks>
    public static class HitDummySetup
    {
        /// <summary>Where the dummy prefab is written; beside the other dev-only prefab, the mirror.</summary>
        public const string PrefabPath = "Assets/_Project/Prefabs/Dev/HitDummy.prefab";

        const string CharacterModelPath = "Assets/_Project/Art/Characters/STDMC/STDMC_Toon.prefab";
        const string ProfileFolder = "Assets/_Project/Profiles";
        const string AnimationProfilePath = ProfileFolder + "/DefaultAnimation.asset";
        const string HitProfilePath = ProfileFolder + "/DefaultHitReaction.asset";

        /// <summary>
        /// The dummy's solid shape, metres: the same capsule as the player's
        /// (<c>GreyboxSceneSetup.BodyHeight</c> and <c>BodyRadius</c>, which are private there),
        /// so shots and blasts find it the way they will find a player.
        /// </summary>
        const float BodyHeight = 1.8f, BodyRadius = 0.35f;

        /// <summary>How far in front of the view the dummy is placed, metres. WHY 3: close enough to see the whole 1.8 m body, far enough not to stand inside it.</summary>
        const float PlaceDistance = 3f;

        /// <summary>How far above and below the aimed-at point the floor is looked for, metres. WHY 3 and 10: above reaches from waist height to a normal ceiling without finding the floor above; below covers aiming over a stairwell.</summary>
        const float FloorSearchUp = 3f, FloorSearchDown = 10f;

        /// <summary>Writes the prefab from scratch, unless a person has edited it since the last build.</summary>
        [MenuItem("Close the Deal/Dev/Build Hit Dummy Prefab")]
        public static void BuildPrefab()
        {
            if (GeneratedAssetGuard.MayOverwrite(PrefabPath))
                Build();
        }

        /// <summary>
        /// Puts a dummy in the open scene, standing on the floor in front of the Scene view (or
        /// in front of the game camera while playing) and facing the viewer.
        /// </summary>
        /// <remarks>
        /// Does not save the scene. Outside Play Mode the new object is an ordinary unsaved scene
        /// change that Undo removes; placed while playing, it goes when Play stops.
        /// </remarks>
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

        /// <summary>Where the person is looking from: the game camera while playing, otherwise the last used Scene view.</summary>
        /// <param name="eye">The viewer's position, world space.</param>
        /// <param name="ahead">The way the viewer faces, unit length.</param>
        /// <returns>False when there is neither.</returns>
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

        /// <summary>Writes the prefab. Saving over the existing path keeps its GUID, so placed copies survive a rebuild.</summary>
        /// <returns>The saved prefab, or null when the character model or its skeleton is missing.</returns>
        static GameObject Build()
        {
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (modelPrefab == null)
            {
                Debug.LogError("[HitDummy] Character model missing at " + CharacterModelPath + "; no dummy built.");
                return null;
            }

            int layer = GreyboxSceneSetup.PlayerLayer;
            // WHY the Player layer: the ragdoll's level copies skip that layer, so the dummy's
            // ragdoll does not collide with a copy of its own capsule, and blasts that look for
            // players find it.
            var root = new GameObject("HitDummy") { layer = layer };
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = BodyHeight;
            capsule.radius = BodyRadius;
            capsule.center = new Vector3(0f, BodyHeight * 0.5f, 0f);

            // The animator reads this object's own movement, as it reads the player's smoothed
            // visual; the model hangs below it.
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
            // No motor: the animator then holds the standing idle.
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

            // On the root, beside the capsule, so a shot that strikes the capsule finds it.
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
