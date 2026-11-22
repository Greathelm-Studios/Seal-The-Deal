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
        /// <summary>
        /// How far up the forearm from the wrist a mesh must reach not to count as a separate cuff ring, metres. WHY:
        /// on STDMC the shirt near the wrist is only a cuff ring (vertices 30-40 mm behind and 30-40 mm past the
        /// wrist, none further up, with long triangles back to the elbow), worn off-centre from the jacket sleeve;
        /// round the modelled hand it stuck out of the top of the sleeve whenever the arm swung (seen in play,
        /// 2026-11-21; the character's own hand shows the same), so on the gun arm every triangle touching the
        /// ring is dropped and the shirt sleeve ends inside the jacket. The jacket and the skin reach right up
        /// the forearm.
        /// </summary>
        const float CuffRingReach = 0.06f;
        /// <summary>
        /// How far up the forearm from the wrist a cuff-ring mesh's sleeve is dropped on the gun arm, metres. WHY
        /// 150 mm: the shirt sleeve also showed through the top of the jacket along the forearm when the arm swung
        /// (seen in play, 2026-11-21); the jacket covers the forearm, which is 190 mm long, so the elbow is kept.
        /// </summary>
        const float InnerSleeveDrop = 0.15f;
        /// <summary>How far from the forearm's bone a vertex may be and still count as part of the sleeve or cuff, metres. WHY 80 mm: the sleeve is at most 52 mm out (measured on STDMC); the shirt's front is much further.</summary>
        const float SleeveReach = 0.08f;
        /// <summary>
        /// Where the jacket sleeve ends on the gun arm, metres up the forearm from the wrist joint. WHY 12 mm: with the
        /// hand's skinning moved to the forearm the sleeve no longer bends at the wrist, and it reached 24 mm past the
        /// joint onto the hand, so the grip hand's palm came out through its wall by up to 35 mm on the gun's left
        /// (measured in play, 2026-11-22). From 12 mm up the hand fits inside. Pulling the end back keeps every
        /// triangle; cutting them opened the sleeve's end.
        /// </summary>
        const float SleeveEnd = 0.012f;
        /// <summary>
        /// How far the jacket's sleeve end is pushed out from the forearm bone on the gun arm, metres, fading to nothing
        /// over <see cref="CuffFlareLength"/>. WHY 7 mm: the bone runs near the top of the sleeve (its wall 15-17 mm
        /// above it), and the grip hand's wrist poked 4-6 mm out through the top 15-25 mm up, showing as a skin
        /// patch from the eye (measured in play, 2026-11-22); 7 mm leaves a little room for the wrist turning.
        /// </summary>
        const float CuffFlare = 0.007f;
        /// <summary>How far up the forearm from <see cref="SleeveEnd"/> the <see cref="CuffFlare"/> fades out, metres. WHY 40 mm: the grip hand's wrist reaches 37 mm up the forearm.</summary>
        const float CuffFlareLength = 0.04f;
        // -0.068 forward: the Stylized Handgun's grip is shallower; at -0.055 the fingers closed in front of it.
        static readonly Vector3 GripWristPosition = new(0.03f, 0.005f, -0.068f);
        static readonly Vector3 GripWristTurn = new(-6f, -4f, 0f);
        static readonly Vector3 SupportWristPosition = new(-0.04f, -0.02f, -0.045f);
        static readonly Vector3 SupportWristTurn = new(-12f, 28f, 0f);
        // 0.9: the Stylized Handgun read too small at 0.75 (Alex's pick from renders, 2026-11-21).
        const float PistolHandScale = 0.9f;
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("Seal the Deal/Greybox/Rebuild Viewmodel")]
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
                Debug.LogWarning("[Greybox] No player prefab at " + PlayerPrefabPath + "; build it with Seal the Deal > Greybox > Rebuild Player Prefab, which adds the viewmodel itself.");
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
            (Mesh[] withOwnHand, Mesh[] withoutOwnHand) = CutRightHand(modelPrefab);
            SetMeshes(serialized.FindProperty("_withOwnHand"), withOwnHand);
            SetMeshes(serialized.FindProperty("_withoutOwnHand"), withoutOwnHand);
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
            // The cut meshes were written as new objects over their files (SaveCut); the prefab as loaded still
            // points at the ones they replaced until it is read back from disk.
            AssetDatabase.ImportAsset(ArmsPrefabPath, ImportAssetOptions.ForceUpdate);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPrefabPath);
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

            bool[] onArm = OnBones(source, armBone);
            return SaveCut(source, source.name + "_Arms", (a, b, c) => onArm[a] && onArm[b] && onArm[c]);
        }

        /// <summary>
        /// The character meshes the right hand moves, and copies of them for weapons that bring a hand modelled
        /// round their grip (the character's own hand is a mitten that crumples when its fingers curl). The skin
        /// mesh (the one with the most triangles wholly on the hand) loses every triangle touching the hand; a mesh
        /// with only a cuff ring round the wrist (<see cref="CuffRingReach"/>) loses every triangle touching it. In every copy what
        /// was skinned to the hand goes to the forearm, so the jacket sleeve ends in a clean tube at the wrist instead
        /// of being dragged into flaps when the hand turns round the grip. The jacket keeps all its triangles:
        /// cutting its hand triangles opened the sleeve's end.
        /// </summary>
        static (Mesh[] with, Mesh[] without) CutRightHand(GameObject character)
        {
            var renderers = new List<SkinnedMeshRenderer>();
            var handMasks = new List<bool[]>();
            var forearms = new List<int>();
            var hands = new List<int>();
            int skin = -1, skinCount = 0;
            foreach (SkinnedMeshRenderer skinned in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = skinned.sharedMesh;
                Transform[] bones = skinned.bones;
                Transform hand = System.Array.Find(bones, b => b != null && b.name == ArmBone(false, "Hand"));
                int forearm = hand != null ? System.Array.IndexOf(bones, hand.parent) : -1;
                if (mesh == null || forearm < 0)
                    continue;

                var handBone = new bool[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    handBone[i] = bones[i] != null && bones[i].IsChildOf(hand);
                if (!TouchesBones(mesh, handBone))
                    continue;

                bool[] onHand = OnBones(mesh, handBone);
                int count = 0;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    int[] all = mesh.GetTriangles(sub);
                    for (int t = 0; t < all.Length; t += 3)
                        if (onHand[all[t]] && onHand[all[t + 1]] && onHand[all[t + 2]])
                            count++;
                }
                if (count > skinCount)
                {
                    skinCount = count;
                    skin = renderers.Count;
                }
                renderers.Add(skinned);
                handMasks.Add(handBone);
                forearms.Add(forearm);
                hands.Add(System.Array.IndexOf(bones, hand));
            }

            var with = new Mesh[renderers.Count];
            var without = new Mesh[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
            {
                Mesh mesh = renderers[i].sharedMesh;
                with[i] = mesh;
                bool[] onHand = OnBones(mesh, handMasks[i]);
                // The skin loses every triangle touching the hand, or the wrist skin pokes out of the sleeve.
                System.Func<int, int, int, bool> keep = (a, b, c) => true;
                System.Action<Mesh> reshape = null;
                if (i == skin)
                    keep = (a, b, c) => !(onHand[a] || onHand[b] || onHand[c]);
                else if (CuffRing(renderers[i], forearms[i], hands[i]) is bool[] ring)
                    keep = (a, b, c) => !(ring[a] || ring[b] || ring[c]);
                else
                {
                    int forearm = forearms[i], handIndex = hands[i];
                    reshape = cut => FitSleeveEnd(cut, forearm, handIndex);
                }
                without[i] = SaveCut(mesh, mesh.name + "_NoRightHand", keep, handMasks[i], forearms[i], reshape);
            }
            return (with, without);
        }

        /// <summary>
        /// The sleeve vertices to drop from a mesh whose sleeve ends in a cuff ring round the right wrist (vertices
        /// near the wrist and none just above <see cref="CuffRingReach"/>): every one within <see cref="InnerSleeveDrop"/>
        /// of the wrist. Null when the mesh has no such ring.
        /// </summary>
        static bool[] CuffRing(SkinnedMeshRenderer renderer, int forearm, int handIndex)
        {
            Matrix4x4[] bind = renderer.sharedMesh.bindposes;
            Matrix4x4 toForearm = bind[forearm];
            Vector3 wristPoint = toForearm.MultiplyPoint3x4(bind[handIndex].inverse.MultiplyPoint3x4(Vector3.zero));
            Vector3 axis = wristPoint.normalized;
            float wrist = wristPoint.magnitude;
            Vector3[] vertices = renderer.sharedMesh.vertices;
            var ring = new bool[vertices.Length];
            bool any = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 f = toForearm.MultiplyPoint3x4(vertices[i]);
                float along = Vector3.Dot(f, axis);
                if ((f - axis * along).magnitude > SleeveReach)
                    continue;
                along -= wrist;
                if (along <= -CuffRingReach && along > -2f * CuffRingReach)
                    return null;
                any |= along > -CuffRingReach;
                ring[i] = along > -InnerSleeveDrop;
            }
            return any ? ring : null;
        }

        /// <summary>
        /// Shapes the jacket's gun-arm sleeve end round a weapon's own hand, in the forearm's bind space: every sleeve
        /// vertex past <see cref="SleeveEnd"/> is pulled back along the forearm onto it, and the last
        /// <see cref="CuffFlareLength"/> is pushed out from the bone by up to <see cref="CuffFlare"/>.
        /// </summary>
        /// <remarks>
        /// Call after <see cref="MoveWeights"/>: the sleeve end then follows the forearm only, so a bind-space move is a
        /// move along the posed forearm. WHY moved rather than cut: cutting the jacket's hand triangles opened the
        /// sleeve's end. The alternative, leaving some of the sleeve end on the hand bone so it bends with the wrist,
        /// lost because that dragged it into flaps over the back of the hand when the hand turned round the grip.
        /// Normals are left as they were; the moves are a few millimetres.
        /// </remarks>
        static void FitSleeveEnd(Mesh mesh, int forearm, int handIndex)
        {
            Matrix4x4[] bind = mesh.bindposes;
            Matrix4x4 toForearm = bind[forearm];
            Matrix4x4 fromForearm = toForearm.inverse;
            Vector3 wristPoint = toForearm.MultiplyPoint3x4(bind[handIndex].inverse.MultiplyPoint3x4(Vector3.zero));
            Vector3 axis = wristPoint.normalized;
            float wrist = wristPoint.magnitude;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 f = toForearm.MultiplyPoint3x4(vertices[i]);
                float along = Vector3.Dot(f, axis);
                Vector3 radial = f - axis * along;
                along -= wrist;
                if (radial.magnitude > SleeveReach || along <= -InnerSleeveDrop)
                    continue;
                // along is measured from the wrist toward the fingers, so "up the forearm" is -along.
                along = Mathf.Min(along, -SleeveEnd);
                float fade = Mathf.SmoothStep(0f, 1f, 1f - (-SleeveEnd - along) / CuffFlareLength);
                radial += radial.normalized * (CuffFlare * fade);
                vertices[i] = fromForearm.MultiplyPoint3x4(axis * (along + wrist) + radial);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        /// <summary>True when any vertex has any skinning on the marked bones.</summary>
        static bool TouchesBones(Mesh mesh, bool[] boneMask)
        {
            foreach (BoneWeight w in mesh.boneWeights)
            {
                if ((boneMask[w.boneIndex0] && w.weight0 > 0f) || (boneMask[w.boneIndex1] && w.weight1 > 0f)
                    || (boneMask[w.boneIndex2] && w.weight2 > 0f) || (boneMask[w.boneIndex3] && w.weight3 > 0f))
                    return true;
            }
            return false;
        }

        /// <summary>Moves every vertex's skinning on the marked bones onto bone <paramref name="to"/>, keeping the weights summing to one.</summary>
        static void MoveWeights(Mesh mesh, bool[] fromBones, int to)
        {
            BoneWeight[] weights = mesh.boneWeights;
            var index = new int[4];
            var weight = new float[4];
            for (int v = 0; v < weights.Length; v++)
            {
                BoneWeight w = weights[v];
                index[0] = w.boneIndex0; index[1] = w.boneIndex1; index[2] = w.boneIndex2; index[3] = w.boneIndex3;
                weight[0] = w.weight0; weight[1] = w.weight1; weight[2] = w.weight2; weight[3] = w.weight3;
                float moved = 0f;
                for (int k = 0; k < 4; k++)
                {
                    if (fromBones[index[k]] && weight[k] > 0f)
                    {
                        moved += weight[k];
                        weight[k] = 0f;
                    }
                }
                if (moved <= 0f)
                    continue;

                int slot = System.Array.IndexOf(index, to);
                if (slot < 0 || weight[slot] <= 0f)
                {
                    slot = System.Array.IndexOf(weight, 0f);
                    index[slot] = to;
                }
                weight[slot] += moved;

                // Unity expects the heaviest influence first.
                for (int a = 0; a < 3; a++)
                    for (int b = a + 1; b < 4; b++)
                        if (weight[b] > weight[a])
                        {
                            (weight[a], weight[b]) = (weight[b], weight[a]);
                            (index[a], index[b]) = (index[b], index[a]);
                        }
                weights[v] = new BoneWeight
                {
                    boneIndex0 = index[0], weight0 = weight[0], boneIndex1 = index[1], weight1 = weight[1],
                    boneIndex2 = index[2], weight2 = weight[2], boneIndex3 = index[3], weight3 = weight[3],
                };
            }
            mesh.boneWeights = weights;
        }

        /// <summary>Which vertices have more than <see cref="ArmWeightShare"/> of their skinning on the marked bones.</summary>
        static bool[] OnBones(Mesh mesh, bool[] boneMask)
        {
            BoneWeight[] weights = mesh.boneWeights;
            var on = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                float share = 0f;
                if (boneMask[w.boneIndex0]) share += w.weight0;
                if (boneMask[w.boneIndex1]) share += w.weight1;
                if (boneMask[w.boneIndex2]) share += w.weight2;
                if (boneMask[w.boneIndex3]) share += w.weight3;
                on[i] = share > ArmWeightShare;
            }
            return on;
        }

        /// <summary>
        /// Saves a copy of <paramref name="source"/> keeping the triangles <paramref name="keep"/> accepts (given
        /// their three vertex indices), optionally with the skinning on <paramref name="moveFrom"/> handed to bone
        /// <paramref name="moveTo"/>, then optionally reshaped by <paramref name="reshape"/>.
        /// </summary>
        static Mesh SaveCut(Mesh source, string name, System.Func<int, int, int, bool> keep, bool[] moveFrom = null, int moveTo = -1, System.Action<Mesh> reshape = null)
        {
            Mesh cut = Object.Instantiate(source);
            cut.name = name;
            if (moveFrom != null)
                MoveWeights(cut, moveFrom, moveTo);
            reshape?.Invoke(cut);
            int kept = 0;
            var triangles = new List<int>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] all = source.GetTriangles(sub);
                triangles.Clear();
                for (int t = 0; t < all.Length; t += 3)
                {
                    if (keep(all[t], all[t + 1], all[t + 2]))
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

            // Written as a new mesh over the same file: the asset keeps its GUID, so prefab references survive a
            // re-cut. WHY not copied into the existing mesh: that left the old cut on the graphics card, and the
            // Editor kept drawing it until restarted (seen 2026-11-21).
            string path = ArmMeshFolder + "/" + cut.name + ".asset";
            AssetDatabase.CreateAsset(cut, path);
            return cut;
        }

        static void SetMeshes(SerializedProperty list, Mesh[] meshes)
        {
            list.arraySize = meshes.Length;
            for (int i = 0; i < meshes.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = meshes[i];
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

            // The hand modelled round this grip (ArtSource/Weapons/pistol_grip_hand.py). Built in the pistol's
            // own space, so it sits at the pistol's origin. The body shows it instead of its own hand
            // (PlayerHeldWeapon.HeldHandName).
            var handModel = AssetDatabase.LoadAssetAtPath<GameObject>(CloseTheDeal.Editor.Art.WeaponToonMaterials.PistolHandPath);
            if (handModel != null)
            {
                var hand = (GameObject)PrefabUtility.InstantiatePrefab(handModel, pistol.transform);
                hand.name = PlayerHeldWeapon.HeldHandName;
                hand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                hand.transform.localScale = Vector3.one;
                SwapToOutlineMeshes(hand, CloseTheDeal.Editor.Art.WeaponToonMaterials.PistolHandPath);
            }
            else
            {
                Debug.LogWarning("[Greybox] No grip hand at " + CloseTheDeal.Editor.Art.WeaponToonMaterials.PistolHandPath + "; the body's own hand holds the pistol.");
            }

            foreach (Renderer renderer in pistol.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off;

            SwapToOutlineMeshes(pistol, PistolPath);
        }

        static void SwapToOutlineMeshes(GameObject model, string modelPath)
        {
            // WHY swap to the outline-baked copies: the gun is low-poly and flat-shaded, and the toon
            // contour splits open at every hard edge unless it has the smoothed normals the bake adds
            // (Seal the Deal > Art > Build Weapon Toon Materials writes them). Missing copies leave the
            // model's own meshes, so the gun still shows, with a broken line.
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh baked = filter.sharedMesh != null
                    ? CloseTheDeal.Editor.Art.CharacterOutlineBake.FindBaked(modelPath, filter.sharedMesh.name)
                    : null;
                if (baked != null)
                    filter.sharedMesh = baked;
            }
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
