using System.Collections.Generic;
using System.IO;
using CloseTheDeal.Customisation;
using CloseTheDeal.Editor.Art;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Lobby;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Tower;
using CloseTheDeal.UI;
using CloseTheDeal.Editor.UI;
using FishNet.Object;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>Additive setup for customisation catalog, OutfitCharacter, Player.prefab outfit, and Lane_Lobby.unity.</summary>
    public static class LobbyLaneSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Lanes/Lane_Lobby.unity";
        const string ProfileFolder = "Assets/_Project/Profiles/Customisation";
        const string OptionFolder = ProfileFolder + "/Options";
        const string PrefabFolder = "Assets/_Project/Prefabs/Lobby";
        const string MaterialFolder = PrefabFolder + "/Materials";
        internal const string CharacterPrefabPath = PrefabFolder + "/OutfitCharacter.prefab";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string StdmcPath = "Assets/_Project/Art/Characters/STDMC/STDMC_Toon.prefab";
        const string KitPrefabs = "Assets/_Project/Art/Kit/Prefabs";
        const string KitMaterials = "Assets/_Project/Art/Kit/Materials";
        internal const string CatalogPath = ProfileFolder + "/OutfitCatalog.asset";
        internal const string RulesPath = ProfileFolder + "/LobbyRules.asset";
        internal const string LinesPath = ProfileFolder + "/SecretaryLines.asset";

        const float CharacterHeight = 1.8f;
        const float HipHeight = 0.9f;       // GreyboxSceneSetup: the Lean pivot's height
        const float FloorHeight = 4.5f, SoffitHeight = 4.3f, Bay = 4f;
        const int BaysX = 6, BaysZ = 4;     // 24 m wide, 16 m deep
        const float BackWallZ = -BaysZ * Bay + 0.2f;

        // Art bible §03 swatches.
        static readonly Color Paper = Hex(0xF3F4F4), Silver = Hex(0xC9CED3), Navy = Hex(0x111B38), Cobalt = Hex(0x244FB9);
        static readonly Color Cyan = Hex(0x20D4E6), NearBlack = Hex(0x101116), Ink2 = Hex(0x4A5468), Ink3 = Hex(0x5B6576);

        [MenuItem("Seal the Deal/Lanes/Set Up Lobby")]
        public static void SetUpFromMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Set Up Lobby",
                    "This rebuilds Lane_Lobby.unity from scratch (hand edits in that scene are lost). Profiles, prefabs a person edited and Player.prefab edits are left alone.", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Run();
        }

        /// <summary>Batch entry: -executeMethod CloseTheDeal.Editor.Lanes.LobbyLaneSetup.SetUpFromCommandLine</summary>
        public static void SetUpFromCommandLine()
        {
            Run();
            EditorApplication.Exit(0);
        }

        static void Run()
        {
            EnsureFolders();
            OutfitCatalog catalog = EnsureCatalog();
            LobbyRules rules = EnsureRules();
            SecretaryLines lines = EnsureAsset<SecretaryLines>(LinesPath, FillLines);
            GameObject character = EnsureCharacterPrefab(catalog);
            PatchPlayerPrefab(character);
            BuildScene(catalog, rules, lines, character);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lobby] Set Up Lobby finished: " + ScenePath);
        }

        internal static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/Profiles", "Customisation");
            EnsureFolder(ProfileFolder, "Options");
            EnsureFolder("Assets/_Project/Prefabs", "Lobby");
            EnsureFolder(PrefabFolder, "Materials");
            EnsureFolder("Assets/_Project/Scenes", "Lanes");
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        internal static LobbyRules EnsureRules()
        {
            var rules = EnsureAsset<LobbyRules>(RulesPath, null);
            var so = new SerializedObject(rules);
            SerializedProperty firms = so.FindProperty("_firms");
            if (firms.arraySize > 0)
                return rules;

            string[] guids = AssetDatabase.FindAssets("t:FirmDefinition", new[] { "Assets/_Project/Profiles/Match" });
            if (guids.Length == 0)
            {
                Debug.LogWarning("[Lobby] No firm definitions under Profiles/Match yet, so the board reads 'Firm A' and 'Firm B'. Add them to Lobby Rules > Firms.");
                return rules;
            }

            firms.arraySize = guids.Length;
            for (int i = 0; i < guids.Length; i++)
                firms.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<FirmDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
            so.ApplyModifiedPropertiesWithoutUndo();
            return rules;
        }

        internal static T EnsureAsset<T>(string path, System.Action<T> fill) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            fill?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        struct OptionSpec
        {
            public string Name;
            public Color Base;
            public int Runs;
            public OptionSpec(string name, Color colour, int runs) { Name = name; Base = colour; Runs = runs; }
        }

        internal static OutfitCatalog EnsureCatalog()
        {
            var existing = AssetDatabase.LoadAssetAtPath<OutfitCatalog>(CatalogPath);
            if (existing != null)
                return existing;

            var slots = new List<OutfitSlot>();
            foreach (StdmcSlot s in StdmcSlots)
                slots.Add(MakeSlot(s));

            var catalog = ScriptableObject.CreateInstance<OutfitCatalog>();
            catalog.EditorInit(slots.ToArray());
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        static OutfitSlot MakeSlot(StdmcSlot s)
        {
            var options = new OutfitOption[s.Options.Length];
            for (int i = 0; i < options.Length; i++)
                options[i] = MakeOption(s.Key, s.Options[i]);

            string path = $"{ProfileFolder}/Slot_{s.Key}.asset";
            var slot = AssetDatabase.LoadAssetAtPath<OutfitSlot>(path);
            if (slot != null)
                return slot;
            slot = ScriptableObject.CreateInstance<OutfitSlot>();
            slot.EditorInit(s.DisplayName, s.Materials, options, 0, s.ViewAngle, s.ViewDistance, s.ViewHeight);
            AssetDatabase.CreateAsset(slot, path);
            return slot;
        }

        static OutfitOption MakeOption(string slotKey, OptionSpec spec)
        {
            string path = $"{OptionFolder}/{slotKey}_{spec.Name.Replace(" ", "")}.asset";
            var option = AssetDatabase.LoadAssetAtPath<OutfitOption>(path);
            if (option != null)
                return option;
            option = ScriptableObject.CreateInstance<OutfitOption>();
            option.EditorInit(spec.Name, spec.Base, ShadowOf(spec.Base), ContourOf(spec.Base), spec.Runs);
            AssetDatabase.CreateAsset(option, path);
            return option;
        }

        static Color ShadowOf(Color c) => new(c.r * 0.55f + 0.02f, c.g * 0.58f + 0.03f, c.b * 0.70f + 0.09f, 1f);

        static Color ContourOf(Color c) => c.grayscale > 0.45f ? Navy : Silver;

        // STDMC's body is one mesh with seven materials; which material is which part was found
        // by rendering each one highlighted (docs/systems/CUSTOMISATION.md, "Slots").
        struct StdmcSlot
        {
            public string Key, DisplayName;
            public string[] Materials;
            public float ViewAngle, ViewDistance, ViewHeight;
            public OptionSpec[] Options;
        }

        // Probe (2026-11-12): lambert3 hair + shoes, lambert2 skin, lambert5 shirt, lambert4 jacket
        // + trousers, lambert6 belt, lambert7 buckle, standardSurface1 eyes. Only parts with a material of their own can be a category, so hair,
        // shoes and trousers wait for Alex's split mesh (docs/archive/HANDOVER_lobby.md).
        static readonly StdmcSlot[] StdmcSlots =
        {
            new StdmcSlot
            {
                Key = "Suit", DisplayName = "Suit", ViewAngle = 30f, ViewDistance = 2.5f, ViewHeight = 0.95f, Materials = new[] { "STDMC_lambert4_Toon" },
                Options = new[]
                {
                    new OptionSpec("Charcoal", Hex(0x2C2B2C), 0),
                    new OptionSpec("Deep navy", Navy, 0),
                    new OptionSpec("Near black", NearBlack, 0),
                    new OptionSpec("Silver grey", Silver, 1),
                    new OptionSpec("Corporate cobalt", Cobalt, 3),
                    new OptionSpec("Paper white", Paper, 5)
                }
            },
            new StdmcSlot
            {
                Key = "Shirt", DisplayName = "Shirt", ViewAngle = -25f, ViewDistance = 1.7f, ViewHeight = 1.3f, Materials = new[] { "STDMC_lambert5_Toon" },
                Options = new[]
                {
                    new OptionSpec("White", Hex(0xFFFFFF), 0),
                    new OptionSpec("Pale blue", Hex(0xD3DEF2), 0),
                    new OptionSpec("Silver grey", Silver, 0),
                    new OptionSpec("Deep navy", Navy, 2)
                }
            },
            new StdmcSlot
            {
                Key = "Belt", DisplayName = "Belt", ViewAngle = 55f, ViewDistance = 1.8f, ViewHeight = 0.95f, Materials = new[] { "STDMC_lambert6_Toon" },
                Options = new[]
                {
                    new OptionSpec("Brown leather", Hex(0x6C523C), 0),
                    new OptionSpec("Near black", NearBlack, 0),
                    new OptionSpec("Deep navy", Navy, 0),
                    new OptionSpec("Corporate cobalt", Cobalt, 1)
                }
            }
        };

        internal static void FillLines(SecretaryLines lines)
        {
            lines.EditorInit(
                arrival: new[]
                {
                    "Welcome to Seal the Deal, {name}. You've been signed in with {firm}. Please keep your badge visible.",
                    "Welcome, {name}. Your visitor badge is valid until you fall out of a window.",
                    "{name} is here. I've let the lifts know.",
                    "Good morning, {name}. Reception is not responsible for anything that happens upstairs."
                },
                ready: new[]
                {
                    "{name} is ready. Noted.",
                    "{name} has confirmed attendance. That can't be withdrawn. It can, actually.",
                    "Thank you, {name}. Please wait in the lobby until you're called."
                },
                notReady: new[]
                {
                    "{name} is no longer ready. I've crossed it out.",
                    "{name} has withdrawn. I'll amend the form."
                },
                firmChosen: new[]
                {
                    "You're with {firm} now. Your previous employer has been informed.",
                    "Signed in with {firm}. Please collect your loyalty on the way up."
                },
                firmRefused: new[]
                {
                    "{firm} is fully staffed. Please sign in with the other firm.",
                    "I can't move you to {firm}. The firms would be uneven, and HR would hear about it."
                },
                startBlocked: new[]
                {
                    "Not everyone has signed in yet. Check the board.",
                    "I can't start without everyone. Check the board."
                },
                shortHanded: new[]
                {
                    "{firm} has nobody. That is permitted. Hold again to proceed.",
                    "The firms are not balanced. I will record that you were told. Hold again to proceed."
                },
                start: new[]
                {
                    "The run has started. The lifts are out of order. Please use the stairs.",
                    "Good luck. The roof is that way. Up.",
                    "The meeting has started. Attendance is compulsory."
                });
        }

        internal static GameObject EnsureCharacterPrefab(OutfitCatalog catalog)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (existing != null && !GeneratedAssetGuard.MayOverwrite(CharacterPrefabPath))
                return existing;

            var stdmc = AssetDatabase.LoadAssetAtPath<GameObject>(StdmcPath);
            var root = new GameObject("OutfitCharacter");
            var fit = new GameObject("Fit");
            fit.transform.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(stdmc, fit.transform);
            FitToHeight(model.transform);

            Renderer[] bodies = model.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer body in bodies)
                body.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var view = root.AddComponent<OutfitView>();
            var so = new SerializedObject(view);
            so.FindProperty("_catalog").objectReferenceValue = catalog;
            SetRenderers(so.FindProperty("_bodies"), bodies);
            so.FindProperty("_motionPivot").objectReferenceValue = fit.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CharacterPrefabPath);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(prefab);
            return prefab;
        }

        static Bounds CombinedBounds(Transform model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        static void FillIfEmpty(SerializedProperty property, Object value)
        {
            if (property.objectReferenceValue == null)
                property.objectReferenceValue = value;
        }

        static void SetRenderers(SerializedProperty list, Renderer[] renderers)
        {
            list.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        }

        static void FitToHeight(Transform model)
        {
            Bounds b = CombinedBounds(model);
            float scale = CharacterHeight / Mathf.Max(0.01f, b.size.y);
            model.localScale *= scale;
            b = CombinedBounds(model);
            Vector3 parent = model.parent.position;
            model.localPosition += new Vector3(parent.x - b.center.x, parent.y - b.min.y, parent.z - b.center.z);
        }

        internal static void PatchPlayerPrefab(GameObject character)
        {
            bool toolOwned = GeneratedAssetGuard.MayOverwrite(PlayerPrefabPath);
            bool saved = false;
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform lean = root.transform.Find("Graphics/Lean");
                PlayerOutfit existing = root.GetComponent<PlayerOutfit>();
                if (existing != null && new SerializedObject(existing).FindProperty("_view").objectReferenceValue != null)
                {
                    // Only adds (AGENTS.md §4): an outfit already wired is someone's setup. One case is
                    // reported: the old greybox figure still standing beside a real character model.
                    if (lean != null && lean.Find("Outfit") != null && FindModelBody(lean, out _))
                        Debug.LogWarning("[Lobby] Player.prefab has the old greybox outfit figure (Graphics/Lean/Outfit) beside the character model, so players draw twice. Delete the Outfit child and point PlayerOutfit's View at the model's OutfitView by hand; this tool leaves a wired prefab alone.");
                    else
                        Debug.Log("[Lobby] Player.prefab already has PlayerOutfit with a view; left as it is.");
                    return;
                }

                OutfitView view = FindModelBody(lean, out Transform model) ? AttachToModel(lean, model) : AddOutfitCharacter(lean, character);

                // Explicit null checks: `??` skips Unity's destroyed-object check.
                var player = root.GetComponent<PlayerOutfit>();
                if (player == null)
                    player = root.AddComponent<PlayerOutfit>();
                var so = new SerializedObject(player);
                FillIfEmpty(so.FindProperty("_view"), view);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                saved = true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (saved && toolOwned)
                GeneratedAssetGuard.MarkGenerated(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
        }

        const string MigrationTitle = "Seal the Deal: one figure per player";

        [MenuItem("Seal the Deal/Lanes/Migrations/Player: Outfit on the Real Model (once)")]
        static void MigrateOutfitToModelMenu()
        {
            if (!EditorUtility.DisplayDialog(MigrationTitle,
                    "Player.prefab: delete the greybox figure at Graphics/Lean/Outfit, give Graphics/Lean/Body an OutfitView if it has none, and point PlayerOutfit's View at it. Nothing else changes.",
                    "Migrate", "Cancel"))
                return;
            MigrateOutfitToModel();
        }

        internal static void MigrateOutfitToModel()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform lean = root.transform.Find("Graphics/Lean");
                Transform body = lean != null ? lean.Find("Body") : null;
                var player = root.GetComponent<PlayerOutfit>();
                if (body == null || player == null)
                {
                    Debug.LogError("[Lobby] Migration: Player.prefab has no Graphics/Lean/Body or no PlayerOutfit; nothing changed.");
                    return;
                }

                Transform greybox = lean.Find("Outfit");
                var so = new SerializedObject(player);
                SerializedProperty viewProperty = so.FindProperty("_view");
                var current = viewProperty.objectReferenceValue as OutfitView;
                if (greybox == null && current != null && current.transform == body)
                {
                    Debug.Log("[Lobby] Migration: Player.prefab already wears the outfit on Body; nothing to do.");
                    return;
                }

                if (greybox != null)
                    Object.DestroyImmediate(greybox.gameObject);
                OutfitView view = AttachToModel(lean, body);
                viewProperty.objectReferenceValue = view;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[Lobby] Migration: Player.prefab now wears the outfit on Graphics/Lean/Body; the greybox figure is gone.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool FindModelBody(Transform lean, out Transform model)
        {
            model = null;
            if (lean == null)
                return false;

            foreach (Transform child in lean)
            {
                if (child.name == "Outfit")
                    continue; // The greybox figure from an earlier run of this tool, not a model.

                int materials = 0;
                foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
                    materials += renderer.sharedMaterials.Length;
                if (materials >= 4)
                {
                    model = child;
                    return true;
                }
            }

            return false;
        }

        static OutfitView AttachToModel(Transform lean, Transform model)
        {
            // Only adds (AGENTS.md §4): a greybox figure an earlier run added stays, and is reported.
            if (lean.Find("Outfit") != null)
                Debug.LogWarning("[Lobby] Player.prefab has both a character model and the lobby's greybox outfit figure (Graphics/Lean/Outfit). Delete the Outfit child by hand; this tool won't remove it.");

            var view = model.GetComponent<OutfitView>();
            if (view == null)
                view = model.gameObject.AddComponent<OutfitView>();
            // Only empty values are filled (AGENTS.md §4): a person's choices on the prefab stay.
            var so = new SerializedObject(view);
            FillIfEmpty(so.FindProperty("_catalog"), AssetDatabase.LoadAssetAtPath<OutfitCatalog>(CatalogPath));
            FillIfEmpty(so.FindProperty("_motionPivot"), model); // The model's own root takes the change bob.
            SerializedProperty bodies = so.FindProperty("_bodies");
            if (bodies.arraySize == 0)
                SetRenderers(bodies, model.GetComponentsInChildren<Renderer>(true));
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        static OutfitView AddOutfitCharacter(Transform lean, GameObject character)
        {
            Transform outfit = lean.Find("Outfit");
            if (outfit == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(character, lean);
                instance.name = "Outfit";
                outfit = instance.transform;
            }
            outfit.localPosition = new Vector3(0f, -HipHeight, 0f);
            outfit.localRotation = Quaternion.identity;
            SetLayer(outfit, GreyboxSceneSetup.PlayerLayer);

            HideGreyboxBody(lean, "Body");
            HideGreyboxBody(lean, "Nose");
            return outfit.GetComponent<OutfitView>();
        }

        static void HideGreyboxBody(Transform lean, string name)
        {
            Transform part = lean.Find(name);
            if (part != null && part.TryGetComponent(out MeshRenderer renderer))
                renderer.enabled = false;
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t)
                SetLayer(child, layer);
        }

        static Material _paper, _navy, _cobalt, _silver, _glass, _cyan, _plaza;

        static void BuildScene(OutfitCatalog catalog, LobbyRules rules, SecretaryLines lines, GameObject character)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene unloads unused assets, which turns the references held so far into dead ones
            // (they would serialise as "none"), so take fresh ones from the asset database.
            catalog = AssetDatabase.LoadAssetAtPath<OutfitCatalog>(CatalogPath);
            rules = AssetDatabase.LoadAssetAtPath<LobbyRules>(RulesPath);
            lines = AssetDatabase.LoadAssetAtPath<SecretaryLines>(LinesPath);
            character = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            LoadMaterials();

            var root = new GameObject("Reception").transform;
            Shell(root);
            Lighting(root);
            Outside(root);
            BackWall(root);
            Dressing(root);
            ReceptionDesk desk = Desk(root, lines, rules, character);
            Stand(root, rules, 0, new Vector3(6.5f, 0f, -8.5f));
            Stand(root, rules, 1, new Vector3(17.5f, 0f, -8.5f));
            TailorCorner(root, character);
            AddCamera();

            GreyboxSceneSetup.EnsurePlayRig(new Vector3(10.5f, 0.1f, -3f), new Vector3(13.5f, 0.1f, -3f));
            SpreadSpawns();
            Systems(rules, lines);
            BuildUi(rules);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssignSceneIds(scene);
            OfficeLook.Apply(askFirst: false);
            EditorSceneManager.SaveScene(scene);
        }

        static void AssignSceneIds(Scene scene)
        {
            System.Reflection.MethodInfo create = typeof(NetworkObject).GetMethod("CreateSceneId",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null,
                new[] { typeof(Scene), typeof(bool), typeof(int).MakeByRefType() }, null);
            if (create == null)
            {
                Debug.LogError("[Lobby] This FishNet version has no NetworkObject.CreateSceneId. Run Fish-Networking > Utility > Reserialize NetworkObjects on Lane_Lobby by hand.");
                return;
            }

            object[] arguments = { scene, false, 0 };
            create.Invoke(null, arguments);
            Debug.Log($"[Lobby] FishNet scene ids assigned to {arguments[2]} object(s).");
        }

        internal static void LoadMaterials()
        {
            _paper = KitMaterial("Wall_PaperWhite");
            _navy = KitMaterial("Trim_Navy");
            _cobalt = KitMaterial("Accent_Cobalt");
            _silver = KitMaterial("Frame_Aluminium");
            _glass = KitMaterial("Glass_Clear");
            _cyan = EnsureFlatMaterial("Lobby_Cyan", Cyan);
            _plaza = EnsureFlatMaterial("Lobby_Plaza", Hex(0xB9C0C8));
        }

        static Material KitMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{KitMaterials}/{name}.mat");

        static Material EnsureFlatMaterial(string name, Color colour)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject Kit(Transform parent, string piece, float x, float y, float z, float yaw = 0f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitPrefabs}/{piece}.prefab");
            if (prefab == null)
                throw new FileNotFoundException($"No kit prefab {piece}; run Seal the Deal > Art > Import Kit first.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetLocalPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Box(Transform parent, string name, Vector3 footprint, Vector3 size, Material material, bool solid = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!solid)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = footprint + Vector3.up * (size.y * 0.5f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.isStatic = solid;
            return go;
        }

        static GameObject Cylinder(Transform parent, string name, Vector3 footprint, float diameter, float height, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = footprint + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        internal static void Shell(Transform root)
        {
            Transform floor = Group(root, "Floor"), ceiling = Group(root, "Ceiling"), walls = Group(root, "Walls");
            for (int i = 0; i < BaysX; i++)
            {
                for (int j = 0; j < BaysZ; j++)
                {
                    Kit(floor, "Slab_4x4", i * Bay, 0f, -j * Bay);
                    Kit(ceiling, "Slab_4x4", i * Bay, FloorHeight, -j * Bay);
                }
                // The street side: glass, looking across the gap at the rival tower.
                Kit(walls, "Window_4m", i * Bay, 0f, 0f);
            }

            for (int j = 0; j < BaysZ; j++)
            {
                Kit(walls, "Wall_Solid_4m", 0f, 0f, -(j + 1) * Bay, -90f);
                Kit(walls, "Wall_Solid_4m", BaysX * Bay, 0f, -j * Bay, 90f);
            }
        }

        internal static void Lighting(Transform root)
        {
            Transform lights = Group(root, "Ceiling lights");
            float[] offsets = { 1.25f, 2.75f };
            for (int i = 0; i < BaysX; i++)
            for (int j = 0; j < BaysZ; j++)
            foreach (float ox in offsets)
            foreach (float oz in offsets)
                Kit(lights, "Ceiling_LightStrip", i * Bay + ox, SoffitHeight, -j * Bay - oz);
        }

        static void Outside(Transform root)
        {
            Transform outside = Group(root, "Outside");
            Box(outside, "Plaza", new Vector3(12f, -0.4f, 6f), new Vector3(90f, 0.2f, 70f), _plaza);
            Box(outside, "Our tower above", new Vector3(12f, FloorHeight + 0.21f, -8f), new Vector3(24f, 60f, 16f), _paper, false);
            Box(outside, "Rival tower", new Vector3(12f, -0.2f, 22f), new Vector3(24f, 70f, 16f), _paper, false);
            Box(outside, "Rival band", new Vector3(12f, 3f, 13.95f), new Vector3(24f, 1.2f, 0.1f), _cobalt, false);
            Box(outside, "Rival glass", new Vector3(12f, 0.2f, 13.9f), new Vector3(22f, 2.6f, 0.05f), _glass, false);

            // Invisible kerbs: walking off the plaza ends nowhere useful, so it doesn't happen.
            Transform kerbs = Group(outside, "Kerbs");
            Kerb(kerbs, new Vector3(12f, 0f, 13.5f), new Vector3(60f, 4f, 0.5f));
            Kerb(kerbs, new Vector3(-12f, 0f, 0f), new Vector3(0.5f, 4f, 60f));
            Kerb(kerbs, new Vector3(36f, 0f, 0f), new Vector3(0.5f, 4f, 60f));
        }

        static void Kerb(Transform parent, Vector3 footprint, Vector3 size)
        {
            GameObject go = Box(parent, "Kerb", footprint, size, _paper);
            Object.DestroyImmediate(go.GetComponent<Renderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
        }

        internal static void BackWall(Transform root)
        {
            Transform back = Group(root, "Back wall");
            Kit(back, "Wall_Solid_4m", 0f, 0f, BackWallZ);
            Kit(back, "Wall_Door_4m_PortalHead", 4f, 0f, BackWallZ);
            Kit(back, "Wall_Door_4m_PortalHead", 8f, 0f, BackWallZ);
            Kit(back, "Wall_Solid_4m_Cobalt", 12f, 0f, BackWallZ);
            Kit(back, "Wall_Solid_4m", 16f, 0f, BackWallZ);
            Kit(back, "Wall_Solid_4m", 20f, 0f, BackWallZ);

            for (int lift = 0; lift < 2; lift++)
            {
                float x = 6f + lift * 4f;
                Box(back, "Lift door", new Vector3(x, 0f, BackWallZ - 0.12f), new Vector3(1.2f, 2.4f, 0.06f), _silver);
                Box(back, "Lift seam", new Vector3(x, 0f, BackWallZ - 0.08f), new Vector3(0.02f, 2.4f, 0.02f), _navy, false);
                Label(back, "G", new Vector3(x, 2.75f, BackWallZ + 0.02f), 0.45f, Cyan, TextAlignmentOptions.Center, new Vector2(1f, 0.6f));
            }

            Label(back, "G", new Vector3(2f, 2.0f, BackWallZ + 0.02f), 3.2f, Navy, TextAlignmentOptions.Center, new Vector2(3.5f, 3.6f));
            Label(back, "Reception", new Vector3(2f, 0.55f, BackWallZ + 0.02f), 0.22f, Ink2, TextAlignmentOptions.Center, new Vector2(3.5f, 0.4f));
            Label(back, "Seal the Deal\nBuilding directory\n\nR      Roof\nB      Boardroom\n1–9   Open plan\nG      Reception",
                new Vector3(14f, 2.2f, BackWallZ + 0.02f), 0.24f, Paper, TextAlignmentOptions.TopLeft, new Vector2(2.6f, 2.2f));
        }

        static TextMeshPro Label(Transform parent, string text, Vector3 centre, float size, Color colour, TextAlignmentOptions align, Vector2 box)
        {
            var go = new GameObject("Text " + text.Split('\n')[0]);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = size * 10f;
            tmp.color = colour;
            tmp.alignment = align;
            tmp.rectTransform.sizeDelta = box;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        internal static void Dressing(Transform root)
        {
            Transform props = Group(root, "Dressing");

            Transform cooler = Group(props, "Water cooler");
            cooler.localPosition = new Vector3(23.3f, 0f, -5f);
            Box(cooler, "Cabinet", Vector3.zero, new Vector3(0.36f, 1.0f, 0.36f), _paper);
            Cylinder(cooler, "Bottle", new Vector3(0f, 1.0f, 0f), 0.3f, 0.48f, _glass);
            Box(cooler, "Tap light", new Vector3(0f, 0.7f, -0.185f), new Vector3(0.04f, 0.04f, 0.01f), _cyan, false);

            Transform copier = Group(props, "Copier");
            copier.localPosition = new Vector3(21.5f, 0f, -14.6f);
            Box(copier, "Body", Vector3.zero, new Vector3(1.0f, 1.0f, 0.7f), _silver);
            Box(copier, "Lid", new Vector3(0f, 1.0f, 0f), new Vector3(1.0f, 0.06f, 0.7f), _navy, false);
            Box(copier, "Tray", new Vector3(0.62f, 0.55f, 0f), new Vector3(0.3f, 0.03f, 0.4f), _paper, false);
            Box(copier, "Panel light", new Vector3(-0.3f, 1.0f, 0.3f), new Vector3(0.05f, 0.065f, 0.05f), _cyan, false);

            Box(props, "Bench left", new Vector3(5f, 0f, -1.4f), new Vector3(3f, 0.45f, 0.6f), _paper);
            Box(props, "Bench right", new Vector3(19f, 0f, -1.4f), new Vector3(3f, 0.45f, 0.6f), _paper);
            Box(props, "Entrance mat", new Vector3(12f, 0.005f, -1.6f), new Vector3(4f, 0.01f, 2f), _navy, false);
        }

        internal static ReceptionDesk Desk(Transform root, SecretaryLines lines, LobbyRules rules, GameObject character)
        {
            Transform desk = Group(root, "Reception desk");
            desk.localPosition = new Vector3(12f, 0f, -10f);
            Box(desk, "Front", Vector3.zero, new Vector3(4.2f, 1.1f, 0.7f), _paper);
            Box(desk, "Cobalt band", new Vector3(0f, 0.2f, 0.36f), new Vector3(4.2f, 0.28f, 0.02f), _cobalt, false);
            Box(desk, "Counter", new Vector3(0f, 1.1f, 0f), new Vector3(4.4f, 0.05f, 0.9f), _navy);
            Box(desk, "Screen", new Vector3(-0.9f, 1.15f, -0.25f), new Vector3(0.55f, 0.36f, 0.04f), _navy, false);
            Box(desk, "Phone", new Vector3(0.9f, 1.15f, 0.05f), new Vector3(0.22f, 0.07f, 0.18f), _navy, false);
            Label(desk, "Reception", new Vector3(0f, 0.75f, 0.37f), 0.2f, Navy, TextAlignmentOptions.Center, new Vector2(3f, 0.4f));

            Transform bell = Group(desk, "Bell");
            bell.localPosition = new Vector3(0.2f, 1.15f, 0.25f);
            Cylinder(bell, "Bell top", Vector3.zero, 0.09f, 0.05f, _silver);
            Object.DestroyImmediate(bell.GetComponentInChildren<Collider>());

            var interact = desk.gameObject.AddComponent<ReceptionDesk>();
            var so = new SerializedObject(interact);
            so.FindProperty("_bell").objectReferenceValue = bell;
            so.FindProperty("_focusOffset").vector3Value = new Vector3(0f, 1.15f, 0.2f);
            so.ApplyModifiedPropertiesWithoutUndo();

            Secretary(desk, lines, rules, character);
            return interact;
        }

        static void Secretary(Transform desk, SecretaryLines lines, LobbyRules rules, GameObject character)
        {
            Transform seat = Group(desk, "Secretary");
            seat.localPosition = new Vector3(-0.3f, 0f, -1.0f);
            Transform pivot = Group(seat, "Turn");
            var figure = (GameObject)PrefabUtility.InstantiatePrefab(character, pivot);
            figure.name = "Figure";
            var view = figure.GetComponent<OutfitView>();
            var viewSo = new SerializedObject(view);
            SerializedProperty wears = viewSo.FindProperty("_wearsFromStart");
            int[] dress = { 1, 1, 1 }; // Navy suit, pale-blue shirt, black belt: front-of-house, and not the players' default charcoal.
            wears.arraySize = dress.Length;
            for (int i = 0; i < dress.Length; i++)
                wears.GetArrayElementAtIndex(i).intValue = dress[i];
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            var idle = seat.gameObject.AddComponent<SecretaryIdle>();
            var idleSo = new SerializedObject(idle);
            idleSo.FindProperty("_profile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CloseTheDeal.Player.AnimationProfile>("Assets/_Project/Profiles/DefaultAnimation.asset");
            idleSo.ApplyModifiedPropertiesWithoutUndo();

            var secretary = seat.gameObject.AddComponent<Secretary>();
            var so = new SerializedObject(secretary);
            so.FindProperty("_lines").objectReferenceValue = lines;
            so.FindProperty("_rules").objectReferenceValue = rules;
            so.FindProperty("_turnPivot").objectReferenceValue = pivot;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material FirmSignMaterial(int firm, Color colour)
        {
            string path = $"{MaterialFolder}/Lobby_Firm{firm}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetFloat("_Smoothness", 0.2f);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", colour);
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static void Stand(Transform root, LobbyRules rules, int firm, Vector3 at)
        {
            Transform stand = Group(root, "Sign-in " + rules.FirmName(firm));
            stand.localPosition = at;
            Color firmColour = rules.FirmColour(firm);
            Material firmMaterial = FirmSignMaterial(firm, firmColour);
            Box(stand, "Post", Vector3.zero, new Vector3(0.12f, 1.0f, 0.12f), _navy);
            Box(stand, "Desk top", new Vector3(0f, 1.0f, 0f), new Vector3(0.7f, 0.04f, 0.5f), _navy);
            Box(stand, "Base", Vector3.zero, new Vector3(0.6f, 0.03f, 0.5f), _navy);
            Transform sheet = Box(stand, "Sheet", new Vector3(0f, 1.04f, 0f), new Vector3(0.3f, 0.005f, 0.4f), _paper, false).transform;
            Box(stand, "Sign", new Vector3(0f, 1.3f, -0.6f), new Vector3(1.4f, 1.9f, 0.05f), firmMaterial);
            Color ink = firmColour.grayscale < 0.5f ? Paper : Navy; // Light type on a dark sign, dark type on a light one.
            // A long firm name wraps and shrinks to stay on the sign rather than running off its edge.
            TextMeshPro title = Label(stand, rules.FirmName(firm), new Vector3(0f, 2.7f, -0.57f), 0.32f, ink, TextAlignmentOptions.Center, new Vector2(1.3f, 0.6f));
            title.textWrappingMode = TextWrappingModes.Normal;
            title.enableAutoSizing = true;
            title.fontSizeMin = 1.5f;
            title.fontSizeMax = 3.2f;
            Label(stand, "Sign in here", new Vector3(0f, 2.05f, -0.57f), 0.13f, ink, TextAlignmentOptions.Center, new Vector2(1.3f, 0.3f));

            var signIn = stand.gameObject.AddComponent<FirmSignIn>();
            var so = new SerializedObject(signIn);
            so.FindProperty("_firm").intValue = firm;
            so.FindProperty("_rules").objectReferenceValue = rules;
            so.FindProperty("_sheet").objectReferenceValue = sheet;
            so.FindProperty("_focusOffset").vector3Value = new Vector3(0f, 1.05f, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void TailorCorner(Transform root, GameObject character)
        {
            Transform corner = Group(root, "Tailor's corner");
            Kit(corner, "Partition_Glass_4m", 0.2f, 0f, -8.5f);
            Kit(corner, "Partition_Glass_2m", 4.2f, 0f, -10.5f, -90f);

            Transform mirror = Group(corner, "Mirror");
            mirror.localPosition = new Vector3(0.25f, 0f, -11.5f);
            mirror.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Box(mirror, "Frame", Vector3.zero, new Vector3(1.2f, 2.3f, 0.06f), _navy);
            Box(mirror, "Glass", new Vector3(0f, 0.08f, 0.035f), new Vector3(1.05f, 2.14f, 0.01f), _glass, false);
            var mirrorInteract = mirror.gameObject.AddComponent<FittingRoomMirror>();
            var so = new SerializedObject(mirrorInteract);
            so.FindProperty("_focusOffset").vector3Value = new Vector3(0f, 1.3f, 0.05f);
            so.ApplyModifiedPropertiesWithoutUndo();

            // A floor disc only: with a collider it was a step in front of the mirror that players climbed or got stuck on.
            Object.DestroyImmediate(Cylinder(corner, "Plinth", new Vector3(2.2f, 0f, -11.5f), 1.2f, 0.06f, _paper).GetComponent<Collider>());
            Box(corner, "Rail", new Vector3(2.4f, 1.75f, -14.8f), new Vector3(2.6f, 0.03f, 0.03f), _silver, false);
            Box(corner, "Rail post L", new Vector3(1.15f, 0f, -14.8f), new Vector3(0.04f, 1.78f, 0.04f), _silver);
            Box(corner, "Rail post R", new Vector3(3.65f, 0f, -14.8f), new Vector3(0.04f, 1.78f, 0.04f), _silver);
            Material[] suits = { _navy, _cobalt, _silver, _paper, _navy };
            for (int i = 0; i < suits.Length; i++)
                Box(corner, "Suit on hanger", new Vector3(1.5f + i * 0.45f, 0.85f, -14.8f), new Vector3(0.08f, 0.88f, 0.5f), suits[i], false);

            Label(corner, "Tailoring", new Vector3(2.2f, 2.9f, -8.48f), 0.22f, Navy, TextAlignmentOptions.Center, new Vector2(3f, 0.4f));
        }

        static void AddCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            go.transform.SetPositionAndRotation(new Vector3(12f, 1.6f, -1f), Quaternion.Euler(5f, 180f, 0f));
        }

        internal static void SpreadSpawns()
        {
            var spawner = Object.FindAnyObjectByType<TeamSpawner>();
            Transform group = Group(null, "Lobby spawns");
            var points = new Transform[8];
            for (int i = 0; i < points.Length; i++)
            {
                var p = new GameObject("Spawn " + (i + 1)).transform;
                p.SetParent(group, false);
                p.localPosition = new Vector3(9f + (i % 4) * 2f, 0.1f, -3f - (i / 4) * 1.6f);
                p.localRotation = Quaternion.Euler(0f, 180f, 0f);
                points[i] = p;
            }
            spawner.FallbackSpawns = points;
            EditorUtility.SetDirty(spawner);
        }

        internal static void Systems(LobbyRules rules, SecretaryLines lines)
        {
            var net = new GameObject("Lobby roster");
            net.AddComponent<NetworkObject>();
            var roster = net.AddComponent<LobbyRoster>();
            var so = new SerializedObject(roster);
            so.FindProperty("_rules").objectReferenceValue = rules;
            so.ApplyModifiedPropertiesWithoutUndo();

            var local = new GameObject("Lobby (local)");
            var interactor = local.AddComponent<LobbyInteractor>();
            var iso = new SerializedObject(interactor);
            iso.FindProperty("_rules").objectReferenceValue = rules;
            iso.ApplyModifiedPropertiesWithoutUndo();
            local.AddComponent<FittingRoom>();
            local.AddComponent<LobbyHostControls>();
            local.AddComponent<LobbySounds>();
        }

        internal static void BuildUi(LobbyRules rules)
        {
            CloseTheDeal.Editor.UI.UiKitSetup.BuildAssets();
            UiTokens t = UiKitAssets.Tokens;

            var canvasGo = new GameObject("LobbyUI", typeof(RectTransform)) { layer = 5 };
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // Under the game menu (10) and the kit's title (20).
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasGo.AddComponent<GraphicRaycaster>();
            var stack = canvasGo.AddComponent<UiScreenStack>();

            var hud = canvasGo.AddComponent<LobbyHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("_tokens").objectReferenceValue = t;
            BuildBoard(canvasGo.transform, hudSo, rules, t);
            BuildPrompt(canvasGo.transform, hudSo, t);
            BuildSubtitle(canvasGo.transform, hudSo, t);
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            FittingRoomScreen screen = BuildFittingScreen(canvasGo.transform, stack, t);
            LobbyHostPanel panel = BuildHostPanel(canvasGo.transform, stack, t);
            UiKitAssets.Instance<UiFocusFrame>(CloseTheDeal.Editor.UI.UiKitComponents.FocusFrame, canvasGo.transform, "FocusFrame");

            UiKitAssets.Set(Object.FindAnyObjectByType<FittingRoom>(), "_screen", screen);
            UiKitAssets.Set(Object.FindAnyObjectByType<LobbyHostControls>(), "_panel", panel);
            BuildRemovedNotice(t);
        }

        static void BuildRemovedNotice(UiTokens t)
        {
            var go = new GameObject("LobbyNotice", typeof(RectTransform)) { layer = 5 };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            RectTransform plate = Anchored(go.transform, "Removed", new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(980f, 64f));
            PlateUnder(plate);
            var group = plate.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var line = UiKitAssets.Instance<UiStatusLine>(CloseTheDeal.Editor.UI.UiKitComponents.StatusLine, plate, "Line");
            UiKitAssets.Place((RectTransform)line.transform, 24f, 16f, 932f, ((RectTransform)line.transform).sizeDelta.y);

            var notice = go.AddComponent<LobbyRemovedNotice>();
            UiKitAssets.Set(notice, "_tokens", t);
            UiKitAssets.Set(notice, "_group", group);
            UiKitAssets.Set(notice, "_line", line);
        }

        static RectTransform Anchored(Transform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            RectTransform rect = UiKitAssets.Node(parent, name);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        static void PlateUnder(RectTransform parent)
        {
            var plate = UiKitAssets.Instance<RectTransform>(CloseTheDeal.Editor.UI.UiKitComponents.Plate, parent, "Plate");
            UiKitAssets.Stretch(plate);
            plate.SetAsFirstSibling();
        }

        static void BuildBoard(Transform canvas, SerializedObject hud, LobbyRules rules, UiTokens t)
        {
            // Top left, under the Esc chip: the match HUD owns the top right (briefcase and firm plates).
            RectTransform board = Anchored(canvas, "Ready board", new Vector2(0f, 1f), new Vector2(24f, -76f), new Vector2(460f, 452f));
            PlateUnder(board);
            hud.FindProperty("_board").objectReferenceValue = board.gameObject.AddComponent<CanvasGroup>();

            UiKitAssets.Label(board, "Section", 24f, 16f, 412f, 22f, "RECEPTION  ·  READY BOARD", t.Section, t.Ink3);
            UiKitAssets.Box(board, "Rule", 24f, 46f, 412f, 1f, t.Hair);

            SerializedProperty names = hud.FindProperty("_firmNames");
            SerializedProperty lists = hud.FindProperty("_firmLists");
            SerializedProperty marks = hud.FindProperty("_firmMarks");
            names.arraySize = lists.arraySize = marks.arraySize = 2;
            for (int firm = 0; firm < 2; firm++)
            {
                float top = 60f + firm * 150f;
                marks.GetArrayElementAtIndex(firm).objectReferenceValue = UiKitAssets.Box(board, "Mark", 24f, top + 9f, 12f, 12f, rules.FirmColour(firm));
                names.GetArrayElementAtIndex(firm).objectReferenceValue = UiKitAssets.Label(board, "Firm", 46f, top, 390f, 30f, rules.FirmName(firm), t.Item, t.Navy);
                lists.GetArrayElementAtIndex(firm).objectReferenceValue = UiKitAssets.Label(board, "Members", 46f, top + 34f, 390f, 108f, string.Empty, t.Body, t.Navy, TextAlignmentOptions.TopLeft, true);
            }

            UiKitAssets.Box(board, "Rule", 24f, 364f, 412f, 1f, t.Hair);
            hud.FindProperty("_status").objectReferenceValue = UiKitAssets.Label(board, "Next step", 24f, 374f, 412f, 64f, string.Empty, t.Label, t.Ink2, TextAlignmentOptions.TopLeft, true);
        }

        static void BuildPrompt(Transform canvas, SerializedObject hud, UiTokens t)
        {
            RectTransform prompt = Anchored(canvas, "Prompt", new Vector2(0.5f, 0f), new Vector2(0f, 236f), new Vector2(620f, 64f));
            PlateUnder(prompt);
            hud.FindProperty("_prompt").objectReferenceValue = prompt.gameObject.AddComponent<CanvasGroup>();

            // A key cap drawn from the kit's outline sprite and Label role: the kit's KeyHint is sized
            // for small footer hints, and the prompt's key has to read from across the room.
            RectTransform cap = UiKitAssets.At(prompt, "Key", 20f, 12f, 96f, 40f);
            UiKitAssets.Img(cap, CloseTheDeal.Editor.UI.UiKitArt.Outline1, t.Navy);
            hud.FindProperty("_promptKey").objectReferenceValue = UiKitAssets.Label(cap, "Text", 0f, 0f, 96f, 40f, string.Empty, t.Label, t.Navy, TextAlignmentOptions.Center);
            hud.FindProperty("_promptText").objectReferenceValue = UiKitAssets.Label(prompt, "Line", 132f, 0f, 470f, 64f, string.Empty, t.Item, t.Navy);

            Image fill = UiKitAssets.Box(prompt, "Hold", 0f, 60f, 620f, 4f, t.Cobalt, CloseTheDeal.Editor.UI.UiKitArt.White);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.enabled = false;
            hud.FindProperty("_holdFill").objectReferenceValue = fill;
        }

        static void BuildSubtitle(Transform canvas, SerializedObject hud, UiTokens t)
        {
            RectTransform subtitle = Anchored(canvas, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(1100f, 96f));
            PlateUnder(subtitle);
            hud.FindProperty("_subtitle").objectReferenceValue = subtitle.gameObject.AddComponent<CanvasGroup>();
            UiKitAssets.Box(subtitle, "Bullet", 28f, 20f, 8f, 8f, t.Cobalt);
            UiKitAssets.Label(subtitle, "Speaker", 46f, 12f, 1020f, 22f, "RECEPTION", t.Caption, t.Ink3);
            hud.FindProperty("_subtitleText").objectReferenceValue = UiKitAssets.Label(subtitle, "Line", 28f, 38f, 1044f, 52f, string.Empty, t.Body, t.Navy, TextAlignmentOptions.TopLeft, true);
        }

        static RectTransform ScreenRoot(Transform canvas, string name)
        {
            RectTransform root = UiKitAssets.Fill(canvas, name);
            root.gameObject.AddComponent<CanvasGroup>();
            return root;
        }

        static FittingRoomScreen BuildFittingScreen(Transform canvas, UiScreenStack stack, UiTokens t)
        {
            int count = Mathf.Clamp(AssetDatabase.LoadAssetAtPath<OutfitCatalog>(CatalogPath).SlotCount, 1, OutfitLoadout.MaxSlots);
            RectTransform root = ScreenRoot(canvas, "Fitting room");
            float rowsTop = 150f, rowsBottom = rowsTop + count * t.RowHeight;
            float height = rowsBottom + 300f;
            RectTransform form = Anchored(root, "Form", new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(660f, height));
            PlateUnder(form);
            var reveal = new List<CanvasGroup>();

            RectTransform head = UiKitAssets.RevealLine(form, "Heading");
            reveal.Add(head.GetComponent<CanvasGroup>());
            UiKitAssets.Box(head, "Bullet", 40f, 38f, 8f, 8f, t.Black);
            UiKitAssets.Label(head, "Breadcrumb", 60f, 30f, 560f, 24f, "SEAL THE DEAL  ·  FORM F-1  ·  WARDROBE AMENDMENT", t.Micro, t.Ink3);
            UiKitAssets.Label(head, "Heading", 40f, 62f, 580f, 56f, "FITTING ROOM", t.Heading, t.Navy);
            UiKitAssets.Box(head, "Rule", 40f, 136f, 580f, 1f, t.Hair);

            RectTransform rows = UiKitAssets.RevealLine(form, "Rows");
            reveal.Add(rows.GetComponent<CanvasGroup>());
            var steppers = new UiStepper[count];
            var labels = new TMP_Text[count];
            for (int i = 0; i < count; i++)
                steppers[i] = StepperRow(rows, i, rowsTop + i * t.RowHeight, t, out labels[i]);

            RectTransform foot = UiKitAssets.RevealLine(form, "Footer");
            reveal.Add(foot.GetComponent<CanvasGroup>());
            TMP_Text detail = UiKitAssets.Label(foot, "Detail", 40f, rowsBottom + 14f, 580f, 56f, string.Empty, t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);
            var keep = UiKitAssets.Instance<UiButton>(CloseTheDeal.Editor.UI.UiKitComponents.Primary, foot, "Keep");
            UiKitAssets.Place((RectTransform)keep.transform, 40f, rowsBottom + 84f, 270f, t.PrimaryHeight);
            SetButtonLabel(keep, "Keep outfit");
            keep.Description = "Wear this outfit and save it on this PC. Everyone sees it once you keep it.";
            var putBack = UiKitAssets.Instance<UiButton>(CloseTheDeal.Editor.UI.UiKitComponents.Secondary, foot, "PutBack");
            UiKitAssets.Place((RectTransform)putBack.transform, 326f, rowsBottom + 88f, 200f, t.SecondaryHeight);
            SetButtonLabel(putBack, "Put back");
            putBack.Description = "Go back to what you were wearing.";
            var status = UiKitAssets.Instance<UiStatusLine>(CloseTheDeal.Editor.UI.UiKitComponents.StatusLine, foot, "StatusLine");
            UiKitAssets.Place((RectTransform)status.transform, 40f, rowsBottom + 166f, 580f, ((RectTransform)status.transform).sizeDelta.y);
            TMP_Text hint = UiKitAssets.Label(foot, "Controls", 40f, rowsBottom + 214f, 580f, 56f, string.Empty, t.Caption, t.Ink3, TextAlignmentOptions.TopLeft, true);

            LinkFormNavigation(steppers, keep, putBack);

            var screen = root.gameObject.AddComponent<FittingRoomScreen>();
            WireKitScreen(screen, form, reveal, steppers[0], t);
            UiKitAssets.SetArray(screen, "_rows", steppers);
            UiKitAssets.SetArray(screen, "_rowNames", labels);
            UiKitAssets.Set(screen, "_keep", keep);
            UiKitAssets.Set(screen, "_putBack", putBack);
            UiKitAssets.Set(screen, "_detail", detail);
            UiKitAssets.Set(screen, "_status", status);
            UiKitAssets.Set(screen, "_hint", hint);
            UiKitAssets.Set(screen, "_stack", stack);
            return screen;
        }

        static UiStepper StepperRow(RectTransform parent, int index, float y, UiTokens t, out TMP_Text label)
        {
            RectTransform row = UiKitAssets.At(parent, "Row " + (index + 1), 40f, y, 580f, t.RowHeight);
            Image highlight = UiKitAssets.Img(UiKitAssets.Fill(row, "Highlight"), CloseTheDeal.Editor.UI.UiKitArt.White, UiTokens.WithAlpha(t.HoverTint, 0f));
            Image rule = UiKitAssets.Img(UiKitAssets.At(row, "Rule", 0f, 0f, 4f, t.RowHeight), CloseTheDeal.Editor.UI.UiKitArt.White, UiTokens.WithAlpha(t.Cobalt, 0f));
            label = UiKitAssets.Label(row, "Name", 16f, 0f, 200f, t.RowHeight, string.Empty, t.Item, t.Navy);
            UiKitAssets.Box(row, "Hairline", 0f, t.RowHeight - 1f, 580f, 1f, t.Hair);

            var stepper = UiKitAssets.Instance<UiStepper>(CloseTheDeal.Editor.UI.UiKitComponents.Stepper, row, "Stepper");
            var rect = (RectTransform)stepper.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-12f, 0f);
            stepper.Fx.RowHighlight = highlight;
            stepper.Fx.RowRule = rule;
            stepper.Description = "Left and right change it; you see it at once.";
            EditorUtility.SetDirty(stepper);
            return stepper;
        }

        static void SetButtonLabel(UiButton button, string text)
        {
            if (button.Fx.Label != null)
                button.Fx.Label.text = text;
        }

        static void WireKitScreen(UiScreen screen, RectTransform content, List<CanvasGroup> reveal, Selectable first, UiTokens t)
        {
            UiKitAssets.Set(screen, "_tokens", t);
            UiKitAssets.Set(screen, "_content", content);
            UiKitAssets.SetArray(screen, "_reveal", reveal.ToArray());
            UiKitAssets.Set(screen, "_firstFocus", first);
        }

        static void LinkFormNavigation(UiStepper[] rows, UiButton keep, UiButton putBack)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i == 0 ? (Selectable)keep : rows[i - 1],
                    selectOnDown = i == rows.Length - 1 ? (Selectable)keep : rows[i + 1]
                };
            }

            keep.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = rows[^1], selectOnDown = rows[0], selectOnRight = putBack };
            putBack.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = rows[^1], selectOnDown = rows[0], selectOnLeft = keep };
        }

        const int HostPanelRows = 7;

        static LobbyHostPanel BuildHostPanel(Transform canvas, UiScreenStack stack, UiTokens t)
        {
            RectTransform root = ScreenRoot(canvas, "Host players");
            RectTransform form = Anchored(root, "Form", new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(660f, 760f));
            PlateUnder(form);
            var reveal = new List<CanvasGroup>();

            RectTransform head = UiKitAssets.RevealLine(form, "Heading");
            reveal.Add(head.GetComponent<CanvasGroup>());
            UiKitAssets.Box(head, "Bullet", 40f, 38f, 8f, 8f, t.Black);
            UiKitAssets.Label(head, "Breadcrumb", 60f, 30f, 560f, 24f, "SEAL THE DEAL  ·  RECEPTION  ·  HOST ONLY", t.Micro, t.Ink3);
            UiKitAssets.Label(head, "Heading", 40f, 62f, 580f, 56f, "PLAYERS", t.Heading, t.Navy);
            UiKitAssets.Box(head, "Rule", 40f, 136f, 580f, 1f, t.Hair);

            RectTransform list = UiKitAssets.RevealLine(form, "List");
            reveal.Add(list.GetComponent<CanvasGroup>());
            var rows = new UiButton[HostPanelRows];
            var labels = new TMP_Text[HostPanelRows];
            for (int i = 0; i < HostPanelRows; i++)
            {
                rows[i] = UiKitAssets.Instance<UiButton>(CloseTheDeal.Editor.UI.UiKitComponents.Secondary, list, "Player " + (i + 1));
                UiKitAssets.Place((RectTransform)rows[i].transform, 40f, 156f + i * (t.SecondaryHeight + 8f), 580f, t.SecondaryHeight);
                labels[i] = rows[i].Fx.Label;
                rows[i].Description = "Remove this player from the lobby. You are asked first.";
            }
            TMP_Text empty = UiKitAssets.Label(list, "Empty", 40f, 160f, 580f, 56f, "Nobody else is here yet. Invite a friend from the Esc menu.", t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);

            RectTransform foot = UiKitAssets.RevealLine(form, "Footer");
            reveal.Add(foot.GetComponent<CanvasGroup>());
            var close = UiKitAssets.Instance<UiButton>(CloseTheDeal.Editor.UI.UiKitComponents.Quiet, foot, "Close");
            UiKitAssets.Place((RectTransform)close.transform, 40f, 680f, 200f, t.SecondaryHeight);
            SetButtonLabel(close, "Close");
            close.Description = "Back to reception.";
            UiKitAssets.Label(foot, "Hint", 260f, 690f, 360f, 40f, "Tab or Backspace closes", t.Caption, t.Ink3);

            for (int i = 0; i < HostPanelRows; i++)
            {
                rows[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i == 0 ? (Selectable)close : rows[i - 1],
                    selectOnDown = i == HostPanelRows - 1 ? (Selectable)close : rows[i + 1]
                };
            }
            close.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = rows[^1], selectOnDown = rows[0] };

            var confirm = UiKitAssets.Instance<UiConfirmDialog>(CloseTheDeal.Editor.UI.UiKitScreens.Confirm, canvas, "Remove dialog");
            confirm.gameObject.SetActive(false);

            var panel = root.gameObject.AddComponent<LobbyHostPanel>();
            WireKitScreen(panel, form, reveal, rows[0], t);
            UiKitAssets.SetArray(panel, "_rows", rows);
            UiKitAssets.SetArray(panel, "_rowLabels", labels);
            UiKitAssets.Set(panel, "_empty", empty);
            UiKitAssets.Set(panel, "_close", close);
            UiKitAssets.Set(panel, "_confirm", confirm);
            UiKitAssets.Set(panel, "_stack", stack);
            return panel;
        }

        static Color Hex(int rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
