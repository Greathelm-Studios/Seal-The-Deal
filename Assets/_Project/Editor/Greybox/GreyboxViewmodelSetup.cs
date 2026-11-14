// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "set up an fps viewmodel with the characters model we are using"
//  Modified  : Claude Fable 5.2, 2026-11-13 — the prefab is now laid out the NorseFPS way at
//              Alex's request: a gun mount holding the pistol and a grip target, a shoulder
//              anchor, and the arm bones the solve needs, instead of a gun on the hand bone.
//  Modified  : Claude Fable 5.2, 2026-11-14 — the arm's bones and finger joints are now
//              written into a HandGrip on the arms prefab, which the body other players see
//              reads at spawn (Harvey: "have the actual playermodel follow the first person
//              view model").
//  Modified  : Claude Fable 5.2, 2026-11-14 — wires the left arm too and adds a GripSupport
//              seat for a two-handed hold (Harvey: "add a left and right hand system and a
//              two handed system").
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Builds the first-person arms prefab from the character's own model (same
//              skeleton, same toon materials, arm geometry only) with a gun mount holding the pistol,
//              and puts PlayerViewmodel on the player prefab. Kept apart from
//              GreyboxSceneSetup so the viewmodel can be rebuilt without that file growing.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using CloseTheDeal.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CloseTheDeal.Editor.Greybox
{
    /// <summary>
    /// Writes <c>ViewmodelArms.prefab</c> and the arm-only meshes it uses, and wires the
    /// viewmodel onto the player prefab. Menu-invoked only (AGENTS.md §4).
    /// </summary>
    /// <remarks>
    /// WHY the arms are cut out here and not in Blender: the cut meshes keep the model's own
    /// vertices, bone weights and bind poses, so they drop onto the same skeleton with the
    /// same materials and outlines and nothing has to be re-exported or re-rigged (this rig
    /// has a 100x scale trap on every Blender round trip, see CHARACTER_ANIMATION.md). When
    /// the character model changes, running the menu item again re-cuts them.
    /// The alternative, hiding the body with a shader or by scaling bones to zero, either
    /// needs a second set of materials or collapses the arms with the spine they hang from.
    /// </remarks>
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

        /// <summary>A Mixamo arm bone's name: the prefix, the side, then the part ("Arm", "ForeArm", "Hand", "HandIndex1" and so on).</summary>
        static string ArmBone(bool left, string part) => BonePrefix + (left ? "Left" : "Right") + part;

        /// <summary>Joints in one finger of the Mixamo skeleton, numbered 1 to 3 from the base.</summary>
        const int JointsPerFinger = 3;

        /// <summary>
        /// A vertex belongs to an arm when more than this share of its skin weight is on arm
        /// bones. Half puts the cut where the upper arm takes over from the shoulder.
        /// </summary>
        const float ArmWeightShare = 0.5f;

        /// <summary>
        /// Where the right wrist sits relative to the middle of the pistol's grip, in metres
        /// (right, up, forward in the gun's space), and how the hand is turned, in degrees.
        /// The wrist is behind the grip, a little to the right and level with its lower half;
        /// the hand is turned 4 degrees inward and 6 degrees up, so it rises to the grip the
        /// way the forearm does, the palm lies against the grip's right side, and the fingers
        /// close round the front onto the left side with the index finger in the trigger guard.
        /// WHY these and not more inward turn: the hand has to point nearly the way the forearm
        /// does or the wrist is bent. An earlier 35 degree turn wrapped the grip well but left
        /// the wrist cocked; the wrap now comes from tighter finger curls instead. Found by
        /// rendering the pose from above, both sides, behind and the eye, and measuring the
        /// angle between forearm and hand (about 6 degrees).
        /// A starting value only: move the GripHand object in the prefab and the tool leaves
        /// the prefab alone from then on.
        /// </summary>
        static readonly Vector3 GripWristPosition = new(0.03f, 0.005f, -0.055f);
        static readonly Vector3 GripWristTurn = new(-6f, -4f, 0f);

        /// <summary>
        /// Where the left wrist sits for a two-handed hold, relative to the middle of the
        /// pistol's grip, in metres (right, up, forward in the gun's space), and how that hand
        /// is turned, in degrees. The left hand comes in from the left and below and cups the
        /// front of the grip over the right hand's fingers. Found by rendering the pose from
        /// above, the left and the eye. A starting value only: move the GripSupport object in
        /// the prefab and the tool leaves the prefab alone from then on.
        /// </summary>
        static readonly Vector3 SupportWristPosition = new(-0.04f, -0.02f, -0.045f);
        static readonly Vector3 SupportWristTurn = new(-12f, 28f, 0f);

        /// <summary>
        /// Size of the pistol in the hand, as a fraction of the model's real size. The model
        /// is a real-size 20 cm pistol; this character's hand is 6 cm across the knuckles
        /// where a real one is about 8, so at full size the grip stood out below the fist.
        /// </summary>
        const float PistolHandScale = 0.75f;

        [MenuItem("Close the Deal/Greybox/Rebuild Viewmodel")]
        public static void RebuildViewmodel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Greybox] Stop Play Mode before rebuilding the viewmodel.");
                return;
            }

            GreyboxSceneSetup.EnsureLayers();
            Ensure(true);
            GreyboxSceneSetup.RebuildPlayerPrefabIfOwned();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Names the layer the arms are drawn on.</summary>
        internal static void EnsureLayer()
        {
            GreyboxSceneSetup.EnsureLayer(ViewmodelLayer, PlayerViewmodel.LayerName);
        }

        /// <summary>
        /// Makes sure the arms prefab exists and returns it.
        /// </summary>
        /// <param name="rebuild">True rewrites the prefab if the tool still owns it; false only builds it when missing.</param>
        /// <returns>The arms prefab's component, or null when the character model or clips are missing.</returns>
        internal static ViewmodelArms Ensure(bool rebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ViewmodelArms>(ArmsPrefabPath);
            if (existing != null && (!rebuild || !GeneratedAssetGuard.MayOverwrite(ArmsPrefabPath)))
                return existing;

            return BuildArmsPrefab();
        }

        /// <summary>Adds the viewmodel component to a player being built by <see cref="GreyboxSceneSetup"/>.</summary>
        /// <param name="playerRoot">Root object of the player under construction.</param>
        /// <param name="motor">That player's motor.</param>
        /// <param name="actions">The Input System actions asset; may be null, and then the stand-in attack trigger does nothing.</param>
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

        // ---- Arms prefab ---------------------------------------------------------------------

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
            shoulder.localPosition = profile.ShoulderPosition;

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

            // The clips are Generic (CHARACTER_ANIMATION.md, "The clips"): with the humanoid
            // avatar left on, Unity would retarget them and throw the hips metres away.
            animator.avatar = null;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            // WHY: the arms are hidden from every camera but their own, and the default
            // culling would stop the bones moving whenever another camera rendered last.
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

        /// <summary>
        /// Fills in one arm's settings (a <see cref="HandGrip"/> field) on a component from a
        /// copy of the character that is still in its bind pose: that side's arm bones, the
        /// knuckle frame, the finger joints and each joint's open-hand turn. The curls and the
        /// elbow swing keep the defaults written in <see cref="HandGrip"/>.
        /// </summary>
        /// <param name="owner">The component that has the field. The caller applies it.</param>
        /// <param name="field">Name of the <see cref="HandGrip"/> field on that component.</param>
        /// <param name="model">Root of the character copy, in its bind pose (an open hand).</param>
        /// <param name="left">True for the left arm, false for the right.</param>
        /// <returns>False when the model lacks that arm or a knuckle bone on it; nothing usable was written.</returns>
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

        /// <summary>Fills a joint list on a component with the three joints of each named finger of one hand, and records each joint's open-hand turn.</summary>
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

        /// <summary>
        /// Leaves only arm geometry on the copy: every skinned mesh is swapped for its arm-only
        /// cut (or removed when it has no arm in it), and everything else that draws or
        /// animates the face is removed.
        /// </summary>
        static void StripToArms(GameObject model)
        {
            foreach (CharacterEyes eyes in model.GetComponentsInChildren<CharacterEyes>(true))
                Object.DestroyImmediate(eyes);

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is not SkinnedMeshRenderer skinned)
                {
                    // Eye cards and anything else rigid on the head.
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
                // The third-person body already casts this player's shadow.
                skinned.shadowCastingMode = ShadowCastingMode.Off;
                // WHY: the bounds baked into the renderer are the whole body's around the
                // hips. A few centimetres from the camera that box can fall outside the view
                // while the hands are in it, and the arms would blink out.
                skinned.updateWhenOffscreen = true;
            }
        }

        /// <summary>
        /// Copies a skinned mesh keeping only the triangles whose three corners are all skinned
        /// mostly to arm bones. Saved as an asset beside the character. Null when none are.
        /// </summary>
        /// <remarks>
        /// The copy keeps every vertex, so bone weights, bind poses and (for outline meshes)
        /// the smoothed normals stay exactly as the character's. The unused vertices cost a
        /// little memory; rebuilding the vertex list would save it and risk the outline data.
        /// </remarks>
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

            // Written over the existing asset when there is one, so its GUID (and the prefab's
            // reference to it) survives a re-cut.
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

        /// <summary>Upper arm, forearm, hand and fingers on either side. The shoulder (collar) bone is left with the body.</summary>
        static bool IsArmBone(string boneName)
        {
            return boneName.Contains("Arm") || boneName.Contains("Hand");
        }

        /// <summary>Hangs the pistol model on the gun mount. Its own origin is the middle of the grip and its muzzle is +Z, so it needs no offset.</summary>
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

        // ---- Clips ---------------------------------------------------------------------------

        /// <summary>
        /// Gives the clip file the import settings its clips need (once) and returns its clips by name.
        /// </summary>
        /// <remarks>
        /// Same settings as the body's STDMC_Moves.fbx: Generic, no avatar, hierarchy kept,
        /// the rig's own scale, Loop Time on every <c>_Loop</c> clip. A file already set to
        /// Generic is taken as configured and left alone, so hand changes to it survive.
        /// </remarks>
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

        // ---- Helpers -------------------------------------------------------------------------

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
