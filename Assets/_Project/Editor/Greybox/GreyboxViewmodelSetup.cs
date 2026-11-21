using System.Collections.Generic;
using CloseTheDeal.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>Builds <c>ViewmodelArms.prefab</c> and cut arm meshes, and adds <see cref="PlayerViewmodel"/> to <c>Player.prefab</c> on request (menu-invoked only).</summary>
    public static class GreyboxViewmodelSetup
    {
        const string CharacterModelPath = "Assets/_Project/Art/Characters/STDMC/STDMC_Toon.prefab";
        const string RigPath = "Assets/_Project/Art/Characters/STDMC/STDMC_Rigged.fbx";
        const string ClipsPath = "Assets/_Project/Art/Characters/STDMC/Animations/STDMC_ViewmodelMoves.fbx";
        const string ArmMeshFolder = "Assets/_Project/Art/Characters/STDMC/Viewmodel";
        const string PistolPath = "Assets/_Project/Art/Weapons/Models/Pistol.fbx";
        const string PrefabFolder = "Assets/_Project/Prefabs/Viewmodel";
        const string ArmsPrefabPath = PrefabFolder + "/ViewmodelArms.prefab";
        const string ProfileFolder = "Assets/_Project/Profiles";
        const string ProfilePath = ProfileFolder + "/DefaultViewmodel.asset";

        /// <summary>Layer 8, the first free one after Player (6) and Prop (7).</summary>
        public const int ViewmodelLayer = 8;

        const string IdleClip = "Pistol_Idle_Loop";
        const string BonePrefix = "mixamorig:";
        const string FoldedUpperArmBone = BonePrefix + "LeftArm";

        static string ArmBone(bool left, string part) => BonePrefix + (left ? "Left" : "Right") + part;

        const int JointsPerFinger = 3;
        const float ArmWeightShare = 0.5f;
        static readonly Vector3 GripWristPosition = new(0.03f, 0.005f, -0.055f);
        static readonly Vector3 GripWristTurn = new(-6f, -4f, 0f);
        static readonly Vector3 SupportWristPosition = new(-0.04f, -0.02f, -0.045f);
        static readonly Vector3 SupportWristTurn = new(-12f, 28f, 0f);
        const float PistolHandScale = 0.75f;
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("Close the Deal/Greybox/Rebuild Viewmodel")]
        public static void RebuildViewmodel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Greybox] Stop Play Mode before rebuilding the viewmodel.");
                return;
            }

            GreyboxSceneSetup.EnsureLayers();
            // Do not rebuild Player.prefab here; other lanes add components to it.
            Ensure(true);
            EnsureOnPlayerPrefab();
            AssetDatabase.SaveAssets();
        }

        static void EnsureOnPlayerPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Greybox] No player prefab at " + PlayerPrefabPath + "; build it with Close the Deal > Greybox > Rebuild Player Prefab, which adds the viewmodel itself.");
                return;
            }

            if (prefab.GetComponent<PlayerViewmodel>() != null)
                return;

            if (!EditorUtility.DisplayDialog(
                    "Add the first-person arms to the player?",
                    "Player.prefab has no viewmodel part. Add it? Nothing else in the prefab is changed.",
                    "Add it", "Leave the prefab alone"))
            {
                Debug.Log("[Greybox] Left " + PlayerPrefabPath + " without a viewmodel, as asked.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var motor = root.GetComponent<PlayerMotor>();
                if (motor == null)
                {
                    Debug.LogWarning("[Greybox] " + PlayerPrefabPath + " has no PlayerMotor; the viewmodel was not added.");
                    return;
                }

                AddTo(root, motor, AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[Greybox] Added the viewmodel to " + PlayerPrefabPath + "; nothing else in it was changed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static void EnsureLayer()
        {
            GreyboxSceneSetup.EnsureLayer(ViewmodelLayer, PlayerViewmodel.LayerName);
        }

        internal static ViewmodelArms Ensure(bool rebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ViewmodelArms>(ArmsPrefabPath);
            if (existing != null && (!rebuild || !GeneratedAssetGuard.MayOverwrite(ArmsPrefabPath)))
                return existing;

            return BuildArmsPrefab();
        }

        internal static void AddTo(GameObject playerRoot, PlayerMotor motor, InputActionAsset actions)
        {
            ViewmodelArms arms = Ensure(false);
            if (arms == null)
            {
                Debug.LogWarning("[Greybox] No viewmodel arms prefab; the player has no first-person arms.");
                return;
            }

            var viewmodel = playerRoot.AddComponent<PlayerViewmodel>();
            var serialized = new SerializedObject(viewmodel);
            GreyboxSceneSetup.SetReference(serialized, "_motor", motor);
            GreyboxSceneSetup.SetReference(serialized, "_armsPrefab", arms);
            GreyboxSceneSetup.SetReference(serialized, "_profile", GreyboxSceneSetup.EnsureAsset<ViewmodelProfile>(ProfileFolder, ProfilePath));
            GreyboxSceneSetup.SetReference(serialized, "_actions", actions);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static ViewmodelArms BuildArmsPrefab()
        {
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (modelPrefab == null)
            {
                Debug.LogError("[Greybox] Character model missing at " + CharacterModelPath + "; no viewmodel built.");
                return null;
            }

            Dictionary<string, AnimationClip> clips = ImportClips();
            EnsureFolder("Assets/_Project/Prefabs", "Viewmodel");
            EnsureFolder("Assets/_Project/Art/Characters/STDMC", "Viewmodel");

            // The root is the eye. PlayerViewmodel moves GunMount and Shoulder under it every
            // frame from the profile; the positions written here are only what the prefab
            // shows when opened.
            var root = new GameObject("ViewmodelArms");
            Transform gunMount = AddChild(root.transform, "GunMount");
            Transform shoulder = AddChild(root.transform, "Shoulder");
            Transform grip = AddChild(gunMount, "GripHand");
            grip.SetLocalPositionAndRotation(GripWristPosition, Quaternion.Euler(GripWristTurn));
            Transform support = AddChild(gunMount, "GripSupport");
            support.SetLocalPositionAndRotation(SupportWristPosition, Quaternion.Euler(SupportWristTurn));
            var profile = GreyboxSceneSetup.EnsureAsset<ViewmodelProfile>(ProfileFolder, ProfilePath);
            gunMount.SetLocalPositionAndRotation(profile.GunPosition, Quaternion.Euler(profile.GunTurn));
            shoulder.localPosition = new Vector3(0.165f, -0.165f, -0.10f);

            // A plain copy, not a nested instance: the arms swap every mesh and drop the eyes,
            // which as prefab overrides would break whenever the character prefab is rebuilt.
            GameObject model = Object.Instantiate(modelPrefab, root.transform);
            model.name = "Arms";
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            StripToArms(model);

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("[Greybox] The character model has no Animator; no viewmodel built.");
                Object.DestroyImmediate(root);
                return null;
            }

            // Clear avatar so generic clips apply exact bone transforms; AlwaysAnimate keeps off-camera bones live.
            animator.avatar = null;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            AddPistol(gunMount);

            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
                part.gameObject.layer = ViewmodelLayer;

            var arms = root.AddComponent<ViewmodelArms>();
            var serialized = new SerializedObject(arms);
            GreyboxSceneSetup.SetReference(serialized, "_gunMount", gunMount);
            GreyboxSceneSetup.SetReference(serialized, "_shoulder", shoulder);
            GreyboxSceneSetup.SetReference(serialized, "_gripTarget", grip);
            GreyboxSceneSetup.SetReference(serialized, "_animator", animator);
            GreyboxSceneSetup.SetReference(serialized, "_rig", animator.transform);
            GreyboxSceneSetup.SetReference(serialized, "_foldedUpperArm", FindDeep(model.transform, FoldedUpperArmBone));
            GreyboxSceneSetup.SetReference(serialized, "_idle", Clip(clips, IdleClip));
            GreyboxSceneSetup.SetReference(serialized, "_supportTarget", support);
            if (!WireGrip(serialized, "_grip", model.transform, false) || !WireGrip(serialized, "_supportGrip", model.transform, true))
            {
                Debug.LogError("[Greybox] The character model is missing arm or knuckle bones; no viewmodel built.");
                Object.DestroyImmediate(root);
                return null;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ArmsPrefabPath);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(prefab);
            Debug.Log("[Greybox] Wrote " + ArmsPrefabPath);
            return prefab.GetComponent<ViewmodelArms>();
        }

        static bool WireGrip(SerializedObject owner, string field, Transform model, bool left)
        {
            Transform upperArm = FindDeep(model, ArmBone(left, "Arm"));
            Transform lowerArm = FindDeep(model, ArmBone(left, "ForeArm"));
            Transform hand = FindDeep(model, ArmBone(left, "Hand"));
            Transform index = FindDeep(model, ArmBone(left, "HandIndex1"));
            Transform middle = FindDeep(model, ArmBone(left, "HandMiddle1"));
            Transform pinky = FindDeep(model, ArmBone(left, "HandPinky1"));
            if (upperArm == null || lowerArm == null || hand == null || index == null || middle == null || pinky == null)
                return false;

            // The hand's knuckle frame in hand-bone space, read from the bind pose: forward runs
            // wrist to middle knuckle, up runs little-finger knuckle to index knuckle. With it
            // the grip target's axes mean the same thing however the FBX turned the hand bone,
            // and on either hand.
            Quaternion knuckleFrame = Quaternion.Inverse(hand.rotation)
                * Quaternion.LookRotation(middle.position - hand.position, index.position - pinky.position);

            owner.FindProperty(field + "._left").boolValue = left;
            GreyboxSceneSetup.SetReference(owner, field + "._upperArm", upperArm);
            GreyboxSceneSetup.SetReference(owner, field + "._lowerArm", lowerArm);
            GreyboxSceneSetup.SetReference(owner, field + "._hand", hand);
            owner.FindProperty(field + "._knuckleFrame").quaternionValue = knuckleFrame;
            var openHand = new List<Quaternion>();
            SetJoints(owner, field + "._triggerFinger", model, left, openHand, "Index");
            SetJoints(owner, field + "._gripFingers", model, left, openHand, "Middle", "Ring", "Pinky");
            SetJoints(owner, field + "._thumb", model, left, openHand, "Thumb");
            SerializedProperty open = owner.FindProperty(field + "._openHand");
            open.arraySize = openHand.Count;
            for (int i = 0; i < openHand.Count; i++)
                open.GetArrayElementAtIndex(i).quaternionValue = openHand[i];
            return true;
        }

        static void SetJoints(SerializedObject arms, string field, Transform model, bool left, List<Quaternion> openHand, params string[] fingers)
        {
            SerializedProperty list = arms.FindProperty(field);
            list.arraySize = fingers.Length * JointsPerFinger;
            int slot = 0;
            foreach (string finger in fingers)
            {
                for (int joint = 1; joint <= JointsPerFinger; joint++, slot++)
                {
                    string name = ArmBone(left, "Hand" + finger + joint);
                    Transform bone = FindDeep(model, name);
                    if (bone == null)
                        Debug.LogWarning($"[Greybox] The character model has no bone '{name}'; that finger joint will not close.");
                    list.GetArrayElementAtIndex(slot).objectReferenceValue = bone;
                    openHand.Add(bone != null ? bone.localRotation : Quaternion.identity);
                }
            }
        }

        static Transform AddChild(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static void StripToArms(GameObject model)
        {
            foreach (CharacterEyes eyes in model.GetComponentsInChildren<CharacterEyes>(true))
                Object.DestroyImmediate(eyes);

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is not SkinnedMeshRenderer skinned)
                {
                    Object.DestroyImmediate(renderer.gameObject);
                    continue;
                }

                Mesh arms = CutArms(skinned);
                if (arms == null)
                {
                    Object.DestroyImmediate(skinned.gameObject);
                    continue;
                }

                skinned.sharedMesh = arms;
                skinned.shadowCastingMode = ShadowCastingMode.Off;
                // Whole-body bounds around the hips fall outside near-eye view, so keep skinning active.
                skinned.updateWhenOffscreen = true;
            }
        }

        static Mesh CutArms(SkinnedMeshRenderer skinned)
        {
            Mesh source = skinned.sharedMesh;
            if (source == null)
                return null;

            Transform[] bones = skinned.bones;
            var armBone = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                armBone[i] = bones[i] != null && IsArmBone(bones[i].name);

            BoneWeight[] weights = source.boneWeights;
            var armVertex = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                float share = 0f;
                if (armBone[w.boneIndex0]) share += w.weight0;
                if (armBone[w.boneIndex1]) share += w.weight1;
                if (armBone[w.boneIndex2]) share += w.weight2;
                if (armBone[w.boneIndex3]) share += w.weight3;
                armVertex[i] = share > ArmWeightShare;
            }

            Mesh cut = Object.Instantiate(source);
            cut.name = source.name + "_Arms";
            int kept = 0;
            var triangles = new List<int>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] all = source.GetTriangles(sub);
                triangles.Clear();
                for (int t = 0; t < all.Length; t += 3)
                {
                    if (armVertex[all[t]] && armVertex[all[t + 1]] && armVertex[all[t + 2]])
                    {
                        triangles.Add(all[t]);
                        triangles.Add(all[t + 1]);
                        triangles.Add(all[t + 2]);
                    }
                }

                cut.SetTriangles(triangles, sub);
                kept += triangles.Count;
            }

            if (kept == 0)
            {
                Object.DestroyImmediate(cut);
                return null;
            }

            // Overwrite in place so existing asset GUIDs and prefab references survive a re-cut.
            string path = ArmMeshFolder + "/" + cut.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(cut, path);
                return cut;
            }

            EditorUtility.CopySerialized(cut, existing);
            Object.DestroyImmediate(cut);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static bool IsArmBone(string boneName)
        {
            return boneName.Contains("Arm") || boneName.Contains("Hand");
        }

        static void AddPistol(Transform gunMount)
        {
            var pistolModel = AssetDatabase.LoadAssetAtPath<GameObject>(PistolPath);
            if (pistolModel == null)
            {
                Debug.LogWarning("[Greybox] Pistol model missing at " + PistolPath + "; the arms hold nothing.");
                return;
            }

            var pistol = (GameObject)PrefabUtility.InstantiatePrefab(pistolModel, gunMount);
            pistol.name = "Pistol";
            pistol.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            pistol.transform.localScale = Vector3.one * PistolHandScale;

            foreach (Renderer renderer in pistol.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Dictionary<string, AnimationClip> ImportClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            var importer = AssetImporter.GetAtPath(ClipsPath) as ModelImporter;
            var rig = AssetImporter.GetAtPath(RigPath) as ModelImporter;
            if (importer == null || rig == null)
            {
                Debug.LogError("[Greybox] Viewmodel clips missing at " + ClipsPath + "; the arms will not move.");
                return clips;
            }

            if (importer.animationType != ModelImporterAnimationType.Generic || importer.clipAnimations.Length == 0)
            {
                importer.globalScale = rig.globalScale;
                importer.useFileScale = rig.useFileScale;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = true;
                importer.preserveHierarchy = true;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.SaveAndReimport();

                ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation take in takes)
                {
                    // Blender exports takes as "Armature|Clip_Name".
                    int bar = take.name.LastIndexOf('|');
                    if (bar >= 0)
                        take.name = take.name.Substring(bar + 1);
                    take.loopTime = take.name.EndsWith("_Loop");
                }

                importer.clipAnimations = takes;
                importer.SaveAndReimport();
            }

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ClipsPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    clips[clip.name] = clip;
            }

            return clips;
        }

        static AnimationClip Clip(Dictionary<string, AnimationClip> clips, string name)
        {
            if (clips.TryGetValue(name, out AnimationClip clip))
                return clip;

            Debug.LogWarning($"[Greybox] Viewmodel clip '{name}' not found in {ClipsPath}.");
            return null;
        }

        static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }

            return null;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
