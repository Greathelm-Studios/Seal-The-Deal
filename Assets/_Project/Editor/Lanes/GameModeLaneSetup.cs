using System.IO;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Props;
using CloseTheDeal.Tower;
using CloseTheDeal.UI;
using FishNet.Component.Transforming;
using FishNet.Object;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>
    /// Everything the briefcase game mode needs outside its scripts, from two menu items.
    /// "Set Up GameMode" makes the firm and match assets, the briefcase prefab, the firm
    /// component on Player.prefab and the lane's test scene (two short towers). "Add GameMode
    /// To Open Scene" puts the match, its HUD and its menu section into whichever scene is
    /// open, for any scene that already has the towers and the game UI. Both only add what is
    /// missing; neither rebuilds or retunes anything already there (AGENTS.md §4).
    /// </summary>
    public static class GameModeLaneSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Lanes/Lane_GameMode.unity";
        const string ProfileFolder = "Assets/_Project/Profiles/Match";
        const string PrefabFolder = "Assets/_Project/Prefabs/Match";
        const string MatchProfilePath = ProfileFolder + "/DefaultMatch.asset";
        const string FirmAPath = ProfileFolder + "/Firm_AshcombePryce.asset";
        const string FirmBPath = ProfileFolder + "/Firm_HalloranVoss.asset";
        const string LaneTowerPath = ProfileFolder + "/LaneTower.asset";
        const string DefaultTowerPath = "Assets/_Project/Profiles/DefaultTower.asset";
        const string BriefcasePath = PrefabFolder + "/Briefcase.prefab";
        const string LeatherPath = PrefabFolder + "/BriefcaseLeather.mat";
        const string ShellPath = PrefabFolder + "/BriefcaseShell.mat";
        const string GatePath = PrefabFolder + "/FloorGate.mat";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        const string MatchObjectName = "Match";
        const string TowerObjectName = "Tower";
        const string HudName = "MatchHud";
        const string SectionName = "MatchSection";

        // The greybox menu's palette (GreyboxUiSetup), so the match's parts sit in the same card.
        static readonly Color Text = new(0.95f, 0.95f, 0.96f, 1f);
        static readonly Color Muted = new(0.64f, 0.67f, 0.71f, 1f);
        static readonly Color Primary = new(0.94f, 0.71f, 0.30f, 1f);
        static readonly Color PrimaryText = new(0.10f, 0.08f, 0.02f, 1f);
        static readonly Color Divider = new(0.17f, 0.19f, 0.23f, 1f);
        static readonly Color ChipBack = new(0.05f, 0.06f, 0.08f, 0.78f);

        // Briefcase, metres. Pivot at the centre of its base.
        static readonly Vector3 CaseSize = new(0.46f, 0.32f, 0.12f);
        const float CaseMass = 5f;
        const float TeleportBeyondMetres = 1.5f;
        const byte SendEveryNthTick = 2;

        [MenuItem("Close the Deal/Lanes/Set Up GameMode")]
        public static void SetUp()
        {
            MatchProfile profile = EnsureAssets();
            EnsurePlayerPrefab();
            TowerProfile laneTower = EnsureLaneTower();
            if (laneTower == null)
                return;

            bool created = !File.Exists(ScenePath);
            Scene scene = created
                ? EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single)
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (created)
            {
                EnsureFolder("Assets/_Project/Scenes", "Lanes");
                FrameStreet(laneTower);
            }

            // Opening a scene can unload assets made moments before, which leaves references to
            // them pointing at nothing; load them again from disk before wiring the scene.
            laneTower = AssetDatabase.LoadAssetAtPath<TowerProfile>(LaneTowerPath);
            profile = AssetDatabase.LoadAssetAtPath<MatchProfile>(MatchProfilePath);

            // The street-level spawns are only a fallback; players spawn in their receptions.
            GreyboxSceneSetup.EnsurePlayRig(new Vector3(-3f, 0.1f, 0f), new Vector3(3f, 0.1f, 0f));
            EnsureTower(laneTower);
            AddMatch(profile);

            // Saved first: FishNet derives scene ids from the scene's path, and a new scene has none until then.
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssignSceneIds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[GameMode] Lane scene set up and saved at " + ScenePath);
        }

        [MenuItem("Close the Deal/Lanes/Add GameMode To Open Scene")]
        public static void AddToOpenScene()
        {
            MatchProfile profile = EnsureAssets();
            EnsurePlayerPrefab();
            if (Object.FindAnyObjectByType<TowerBuilder>() == null)
            {
                Debug.LogError("[GameMode] The open scene has no Tower. Run Close the Deal > Greybox > Set Up Scene first.");
                return;
            }

            AddMatch(profile);
            Scene scene = SceneManager.GetActiveScene();
            AssignSceneIds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GameMode] Added the match to " + scene.path);
        }

        // ---- Assets --------------------------------------------------------------------------

        /// <summary>Firms, match profile and briefcase prefab. Existing assets are the designer's and are left exactly as they are; only an empty slot is filled.</summary>
        static MatchProfile EnsureAssets()
        {
            EnsureFolder("Assets/_Project/Profiles", "Match");
            EnsureFolder("Assets/_Project/Prefabs", "Match");

            FirmDefinition firmA = EnsureFirm(FirmAPath, "Ashcombe & Pryce", new Color(0.16f, 0.32f, 0.55f), 0);
            FirmDefinition firmB = EnsureFirm(FirmBPath, "Halloran Voss", new Color(0.52f, 0.14f, 0.18f), 1);
            NetworkObject briefcase = EnsureBriefcasePrefab();

            var profile = AssetDatabase.LoadAssetAtPath<MatchProfile>(MatchProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MatchProfile>();
                profile.Firms = new[] { firmA, firmB };
                profile.BriefcasePrefab = briefcase;
                AssetDatabase.CreateAsset(profile, MatchProfilePath);
                Debug.Log("[GameMode] Created " + MatchProfilePath);
            }
            else if (profile.BriefcasePrefab == null)
            {
                profile.BriefcasePrefab = briefcase;
                EditorUtility.SetDirty(profile);
            }

            return profile;
        }

        static FirmDefinition EnsureFirm(string path, string displayName, Color colour, int tower)
        {
            var firm = AssetDatabase.LoadAssetAtPath<FirmDefinition>(path);
            if (firm != null)
                return firm;

            firm = ScriptableObject.CreateInstance<FirmDefinition>();
            firm.DisplayName = displayName;
            firm.MarkerColour = colour;
            firm.Tower = tower;
            AssetDatabase.CreateAsset(firm, path);
            Debug.Log("[GameMode] Created " + path);
            return firm;
        }

        /// <summary>The lane scene's towers: the default tower's templates and dimensions, cut down to reception, two floors, a checkpoint, the boardroom and the roof.</summary>
        static TowerProfile EnsureLaneTower()
        {
            var lane = AssetDatabase.LoadAssetAtPath<TowerProfile>(LaneTowerPath);
            if (lane != null)
                return lane;

            var standard = AssetDatabase.LoadAssetAtPath<TowerProfile>(DefaultTowerPath);
            if (standard == null || standard.Lobby == null)
            {
                Debug.LogError("[GameMode] No tower profile with floor templates at " + DefaultTowerPath + ". Run Close the Deal > Greybox > Set Up Scene once, then this again.");
                return null;
            }

            lane = Object.Instantiate(standard);
            lane.RandomFloors = 2;
            lane.CheckpointEvery = 2;
            AssetDatabase.CreateAsset(lane, LaneTowerPath);
            Debug.Log("[GameMode] Created " + LaneTowerPath);
            return lane;
        }

        // ---- Briefcase prefab ----------------------------------------------------------------

        /// <summary>Overwrites the briefcase prefab from the tool, only while the tool still owns it (GeneratedAssetGuard). Saving over the path keeps its GUID.</summary>
        [MenuItem("Close the Deal/Lanes/Rebuild GameMode Briefcase")]
        public static void RebuildBriefcase()
        {
            if (!GeneratedAssetGuard.MayOverwrite(BriefcasePath))
                return;

            BuildBriefcasePrefab();
            AssetDatabase.SaveAssets();
        }

        static NetworkObject EnsureBriefcasePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BriefcasePath);
            return existing != null ? existing.GetComponent<NetworkObject>() : BuildBriefcasePrefab();
        }

        /// <summary>
        /// A greybox briefcase: a case in the firm's colour with a dark handle and corners, no
        /// stripes, so it never reads as the pickups lane's banded Grenade Case. Networked like furniture.
        /// </summary>
        static NetworkObject BuildBriefcasePrefab()
        {
            Material leather = EnsureMaterial(LeatherPath, new Color(0.23f, 0.14f, 0.09f));
            Material shell = EnsureMaterial(ShellPath, Color.white);

            var root = new GameObject("Briefcase") { layer = GreyboxSceneSetup.PropLayer };
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, CaseSize.y * 0.5f, 0f);
            collider.size = CaseSize;

            var body = root.AddComponent<Rigidbody>();
            body.mass = CaseMass;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // A kicked case must not tunnel through a 0.2 m wall.

            var visual = new GameObject("Visual") { layer = GreyboxSceneSetup.PropLayer };
            visual.transform.SetParent(root.transform, false);
            Renderer shellPart = Part(visual.transform, "Body", new Vector3(0f, CaseSize.y * 0.5f, 0f), CaseSize, shell);
            Part(visual.transform, "Handle", new Vector3(0f, CaseSize.y + 0.03f, 0f), new Vector3(0.16f, 0.03f, 0.03f), leather);
            Part(visual.transform, "Lid seam", new Vector3(0f, CaseSize.y - 0.04f, 0f), new Vector3(CaseSize.x + 0.01f, 0.015f, CaseSize.z + 0.01f), leather);
            Part(visual.transform, "Clasp left", new Vector3(-0.12f, CaseSize.y - 0.05f, CaseSize.z * 0.5f + 0.006f), new Vector3(0.04f, 0.035f, 0.012f), leather);
            Part(visual.transform, "Clasp right", new Vector3(0.12f, CaseSize.y - 0.05f, CaseSize.z * 0.5f + 0.006f), new Vector3(0.04f, 0.035f, 0.012f), leather);

            root.AddComponent<NetworkObject>();

            // Host-authoritative, sent every other tick while it moves; a jump (put down, sent back) snaps instead of gliding.
            var sync = root.AddComponent<NetworkTransform>();
            var serializedSync = new SerializedObject(sync);
            GreyboxSceneSetup.SetBool(serializedSync, "_clientAuthoritative", false);
            GreyboxSceneSetup.SetInt(serializedSync, "_interval", SendEveryNthTick);
            GreyboxSceneSetup.SetBool(serializedSync, "_synchronizeScale", false);
            GreyboxSceneSetup.SetBool(serializedSync, "_enableTeleport", true);
            GreyboxSceneSetup.SetFloat(serializedSync, "_teleportThreshold", TeleportBeyondMetres);
            serializedSync.ApplyModifiedPropertiesWithoutUndo();

            var briefcase = root.AddComponent<Briefcase>();
            var serialized = new SerializedObject(briefcase);
            GreyboxSceneSetup.SetReference(serialized, "_visual", visual.transform);
            SerializedProperty markers = serialized.FindProperty("_firmMarkers");
            markers.arraySize = 1;
            markers.GetArrayElementAtIndex(0).objectReferenceValue = shellPart;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BriefcasePath);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(prefab);
            Debug.Log("[GameMode] Wrote " + BriefcasePath);
            return prefab.GetComponent<NetworkObject>();
        }

        static Renderer Part(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.layer = GreyboxSceneSetup.PropLayer;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = centre;
            part.transform.localScale = size;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>A frosted see-through panel, so a closed floor can be seen but not reached. Kept clear of red, yellow and the firm colours.</summary>
        static Material EnsureGateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GatePath);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FloorGate" };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(0.86f, 0.9f, 0.95f, 0.45f));
            AssetDatabase.CreateAsset(material, GatePath);
            return material;
        }

        static Material EnsureMaterial(string path, Color colour)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", colour);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---- Player prefab -------------------------------------------------------------------

        /// <summary>
        /// Adds the firm component and the hand a carried briefcase hangs from, if they are
        /// missing. A prefab the greybox tool still owns stays the tool's (re-fingerprinted),
        /// so Rebuild Player Prefab still works on it: run this menu again afterwards.
        /// </summary>
        static void EnsurePlayerPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[GameMode] No Player.prefab. Run Close the Deal > Greybox > Set Up Scene first.");
                return;
            }

            if (prefab.GetComponent<FirmMember>() != null)
                return;

            bool toolOwned = GeneratedAssetGuard.MayOverwrite(PlayerPrefabPath);
            using (var scope = new PrefabUtility.EditPrefabContentsScope(PlayerPrefabPath))
            {
                GameObject root = scope.prefabContentsRoot;
                Transform lean = root.transform.Find("Graphics/Lean");
                Transform parent = lean != null ? lean : root.transform;

                var anchor = new GameObject("CarryAnchor") { layer = root.layer };
                anchor.transform.SetParent(parent, false);
                // At the right hip, the case hanging side-on with its base about half a metre off the floor.
                anchor.transform.localPosition = lean != null ? new Vector3(0.42f, -0.4f, 0.1f) : new Vector3(0.42f, 0.5f, 0.1f);
                anchor.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                var member = root.AddComponent<FirmMember>();
                var serialized = new SerializedObject(member);
                GreyboxSceneSetup.SetReference(serialized, "_carryAnchor", anchor.transform);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (toolOwned)
                GeneratedAssetGuard.MarkGenerated(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
            Debug.Log("[GameMode] Added FirmMember and a carry anchor to " + PlayerPrefabPath);
        }

        // ---- Scene ---------------------------------------------------------------------------

        /// <summary>First time only: the menu's backdrop looks down the street between the two towers.</summary>
        static void FrameStreet(TowerProfile tower)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            cam.transform.position = new Vector3(-tower.Width * 0.5f - 22f, 12f, 0f);
            cam.transform.rotation = Quaternion.Euler(-8f, 90f, 0f);
        }

        static void EnsureTower(TowerProfile profile)
        {
            TowerBuilder builder = Object.FindAnyObjectByType<TowerBuilder>();
            if (builder == null)
            {
                var go = new GameObject(TowerObjectName);
                go.AddComponent<NetworkObject>();
                builder = go.AddComponent<TowerBuilder>();
            }

            FillIfEmpty(builder, "_profile", profile);

            if (builder.GetComponent<PropSpawner>() == null)
                builder.gameObject.AddComponent<PropSpawner>();
        }

        /// <summary>
        /// Gives every networked scene object that has none its FishNet scene id. FishNet does
        /// this itself when a person edits the scene, but not for objects a tool adds in a
        /// headless run, and without it the object never starts on the network. Ids that
        /// already exist are kept. FishNet's own method is internal, hence the reflection.
        /// </summary>
        static void AssignSceneIds(Scene scene)
        {
            System.Reflection.MethodInfo create = typeof(NetworkObject).GetMethod(
                "CreateSceneId",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                null,
                new[] { typeof(Scene), typeof(bool), typeof(int).MakeByRefType() },
                null);
            if (create == null)
            {
                Debug.LogError("[GameMode] This FishNet version has no NetworkObject.CreateSceneId. Run Tools > Fish-Networking > Utility > Reserialize NetworkObjects on the scene by hand.");
                return;
            }

            object[] arguments = { scene, false, 0 };
            create.Invoke(null, arguments);
            Debug.Log($"[GameMode] FishNet scene ids assigned to {arguments[2]} object(s) in {scene.name}.");
        }

        /// <summary>The match object, its HUD and its menu section, each only if missing.</summary>
        static void AddMatch(MatchProfile profile)
        {
            MatchDirector match = Object.FindAnyObjectByType<MatchDirector>();
            if (match == null)
            {
                var go = new GameObject(MatchObjectName);
                go.AddComponent<NetworkObject>();
                match = go.AddComponent<MatchDirector>();
            }

            FillIfEmpty(match, "_profile", profile);

            // The plaza and skybridges between the towers: runtime-built, so no tower geometry changes.
            MatchCrossings crossings = match.GetComponent<MatchCrossings>();
            if (crossings == null)
                crossings = match.gameObject.AddComponent<MatchCrossings>();
            FillIfEmpty(match, "_crossings", crossings);

            // The floor gates, built at runtime at each stair exit; only their look is an asset.
            MatchGates gates = match.GetComponent<MatchGates>();
            if (gates == null)
                gates = match.gameObject.AddComponent<MatchGates>();
            FillIfEmpty(gates, "_gateMaterial", EnsureGateMaterial());
            FillIfEmpty(match, "_gates", gates);

            GameMenu menu = Object.FindAnyObjectByType<GameMenu>();
            if (menu == null)
            {
                Debug.LogError("[GameMode] No game menu in the scene, so the match has no HUD or menu section. Run Close the Deal > Greybox > Set Up Scene first.");
                return;
            }

            EnsureHud(menu, match);
            EnsureMenuSection(menu, match);
        }

        // ---- HUD -----------------------------------------------------------------------------

        static void EnsureHud(GameMenu menu, MatchDirector match)
        {
            Transform hud = menu.transform.Find("Hud");
            if (hud == null || hud.Find(HudName) != null || menu.GetComponent<MatchHud>() != null)
                return;

            var root = new GameObject(HudName, typeof(RectTransform));
            root.transform.SetParent(hud, false);
            Stretch((RectTransform)root.transform);

            // Clock: top centre.
            TMP_Text clock = Chip(root.transform, "Clock", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(150f, 48f), 28f, TextAlignmentOptions.Center, out _);
            clock.fontStyle = FontStyles.Bold;

            // Top right: the briefcase, then the firms, your own first.
            TMP_Text briefcase = FirmRow(root.transform, "Briefcase", new Vector2(-24f, -24f), out UnityEngine.UI.Image briefcaseSwatch);
            TMP_Text own = FirmRow(root.transform, "OwnFirm", new Vector2(-24f, -106f), out UnityEngine.UI.Image ownSwatch);
            TMP_Text rival = FirmRow(root.transform, "RivalFirm", new Vector2(-24f, -188f), out UnityEngine.UI.Image rivalSwatch);

            // Countdown: big, above the crosshair.
            TMP_Text countdown = Chip(root.transform, "Countdown", new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(170f, 170f), 120f, TextAlignmentOptions.Center, out GameObject countdownChip);
            countdown.fontStyle = FontStyles.Bold;

            // Key prompt: just under the crosshair.
            TMP_Text prompt = Chip(root.transform, "Prompt", new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(380f, 44f), 20f, TextAlignmentOptions.Center, out GameObject promptChip);

            // One-off lines: bottom centre.
            TMP_Text notice = Chip(root.transform, "Notice", new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(980f, 52f), 20f, TextAlignmentOptions.Center, out GameObject noticeChip);
            var noticeGroup = noticeChip.AddComponent<CanvasGroup>();
            noticeGroup.alpha = 0f;
            noticeGroup.blocksRaycasts = false;
            noticeGroup.interactable = false;

            var component = menu.gameObject.AddComponent<MatchHud>();
            var so = new SerializedObject(component);
            GreyboxSceneSetup.SetReference(so, "_match", match);
            GreyboxSceneSetup.SetReference(so, "_root", root);
            GreyboxSceneSetup.SetReference(so, "_clockText", clock);
            GreyboxSceneSetup.SetReference(so, "_briefcaseText", briefcase);
            GreyboxSceneSetup.SetReference(so, "_briefcaseSwatch", briefcaseSwatch);
            GreyboxSceneSetup.SetReference(so, "_ownText", own);
            GreyboxSceneSetup.SetReference(so, "_ownSwatch", ownSwatch);
            GreyboxSceneSetup.SetReference(so, "_rivalText", rival);
            GreyboxSceneSetup.SetReference(so, "_rivalSwatch", rivalSwatch);
            GreyboxSceneSetup.SetReference(so, "_countdownChip", countdownChip);
            GreyboxSceneSetup.SetReference(so, "_countdownText", countdown);
            GreyboxSceneSetup.SetReference(so, "_noticeGroup", noticeGroup);
            GreyboxSceneSetup.SetReference(so, "_noticeText", notice);
            GreyboxSceneSetup.SetReference(so, "_promptChip", promptChip);
            GreyboxSceneSetup.SetReference(so, "_promptText", prompt);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Two lines on a dark chip, with a bar of the firm's colour down its left edge.</summary>
        static TMP_Text FirmRow(Transform parent, string name, Vector2 offset, out UnityEngine.UI.Image swatch)
        {
            TMP_Text text = Chip(parent, name, new Vector2(1f, 1f), offset, new Vector2(440f, 72f), 17f, TextAlignmentOptions.MidlineLeft, out GameObject chip);
            text.rectTransform.offsetMin = new Vector2(24f, 0f);

            var bar = new GameObject("Swatch", typeof(RectTransform));
            bar.transform.SetParent(chip.transform, false);
            swatch = bar.AddComponent<UnityEngine.UI.Image>();
            swatch.raycastTarget = false;
            var rect = (RectTransform)bar.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(10f, 0f);
            return text;
        }

        /// <summary>HUD text on a dark translucent chip pinned to a screen corner or the centre, so it reads over bright walls and sky.</summary>
        static TMP_Text Chip(Transform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size, float fontSize, TextAlignmentOptions alignment, out GameObject chip)
        {
            chip = new GameObject(name, typeof(RectTransform));
            chip.transform.SetParent(parent, false);
            var back = chip.AddComponent<UnityEngine.UI.Image>();
            back.color = ChipBack;
            back.raycastTarget = false;
            var rect = (RectTransform)chip.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            TMP_Text text = Label(chip.transform, "Text", string.Empty, fontSize, Text);
            text.alignment = alignment;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(12f, 0f);
            text.rectTransform.offsetMax = new Vector2(-12f, 0f);
            return text;
        }

        // ---- Menu section --------------------------------------------------------------------

        static void EnsureMenuSection(GameMenu menu, MatchDirector match)
        {
            Transform card = menu.transform.Find("Menu/Card");
            if (card == null || card.Find(SectionName) != null || menu.GetComponent<MatchMenuPanel>() != null)
                return;

            var section = new GameObject(SectionName, typeof(RectTransform));
            section.transform.SetParent(card, false);
            var column = section.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            column.spacing = 10f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            // Above the in-game buttons, so the result is the first thing read.
            Transform gameSection = card.Find("GameSection");
            if (gameSection != null)
                section.transform.SetSiblingIndex(gameSection.GetSiblingIndex());

            TMP_Text headline = Label(section.transform, "Headline", string.Empty, 28f, Text);
            headline.fontStyle = FontStyles.Bold;
            Height(headline.gameObject, 36f);

            TMP_Text detail = Label(section.transform, "Detail", string.Empty, 19f, Muted);
            detail.textWrappingMode = TextWrappingModes.Normal;
            detail.overflowMode = TextOverflowModes.Overflow;
            Height(detail.gameObject, 52f);

            UnityEngine.UI.Button action = Button(section.transform, "MatchActionButton", "Start the match", out TMP_Text actionLabel);

            TMP_Text waiting = Label(section.transform, "Waiting", string.Empty, 19f, Muted);
            Height(waiting.gameObject, 28f);

            var rule = new GameObject("Divider", typeof(RectTransform));
            rule.transform.SetParent(section.transform, false);
            var line = rule.AddComponent<UnityEngine.UI.Image>();
            line.color = Divider;
            line.raycastTarget = false;
            Height(rule, 1f);

            var panel = menu.gameObject.AddComponent<MatchMenuPanel>();
            var so = new SerializedObject(panel);
            GreyboxSceneSetup.SetReference(so, "_match", match);
            GreyboxSceneSetup.SetReference(so, "_menu", menu);
            GreyboxSceneSetup.SetReference(so, "_section", section);
            GreyboxSceneSetup.SetReference(so, "_headline", headline);
            GreyboxSceneSetup.SetReference(so, "_detail", detail);
            GreyboxSceneSetup.SetReference(so, "_actionButton", action);
            GreyboxSceneSetup.SetReference(so, "_actionLabel", actionLabel);
            GreyboxSceneSetup.SetReference(so, "_waitingText", waiting);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static UnityEngine.UI.Button Button(Transform parent, string name, string label, out TMP_Text text)
        {
            GameObject go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            go.name = name;
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = null;
            image.color = Primary;

            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.colors = new UnityEngine.UI.ColorBlock
            {
                normalColor = new Color(0.86f, 0.86f, 0.86f, 1f),
                highlightedColor = Color.white,
                pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.4f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            text = go.GetComponentInChildren<TMP_Text>();
            text.text = label;
            text.fontSize = 22;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = -2f;
            text.color = PrimaryText;
            text.raycastTarget = false;
            Height(go, 64f);
            return button;
        }

        // ---- Small helpers -------------------------------------------------------------------

        static TMP_Text Label(Transform parent, string name, string content, float size, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = colour;
            text.richText = true;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        static void Height(GameObject go, float height)
        {
            var element = go.AddComponent<UnityEngine.UI.LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Sets an object slot only when nobody has filled it (AGENTS.md §4).</summary>
        static void FillIfEmpty(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty slot = serialized.FindProperty(field);
            if (slot == null || slot.objectReferenceValue != null)
                return;

            slot.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
