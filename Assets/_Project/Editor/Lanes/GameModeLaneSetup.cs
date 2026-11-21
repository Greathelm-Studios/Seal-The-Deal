using System.IO;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Editor.UI;
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
    /// <summary>Additive setup for briefcase game mode assets, briefcase prefab, Player.prefab, and lane scene.</summary>
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
        const string BeaconPath = PrefabFolder + "/BriefcaseBeacon.mat";
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

        [MenuItem("Seal the Deal/Lanes/Set Up GameMode")]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Set Up GameMode", "Stop Play Mode first.", "OK");
                else
                    Debug.LogWarning("[GameMode] Stop Play Mode first.");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

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

        [MenuItem("Seal the Deal/Lanes/Add GameMode To Open Scene")]
        public static void AddToOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Add GameMode To Open Scene", "Stop Play Mode first.", "OK");
                else
                    Debug.LogWarning("[GameMode] Stop Play Mode first.");
                return;
            }
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog(
                    "Add GameMode To Open Scene",
                    "This adds the Match, HUD and menu section (if missing) to the open scene and saves it.",
                    "Add and Save", "Cancel"))
                return;

            MatchProfile profile = EnsureAssets();
            EnsurePlayerPrefab();
            if (Object.FindAnyObjectByType<TowerBuilder>() == null)
            {
                Debug.LogError("[GameMode] The open scene has no Tower. Run Seal the Deal > Greybox > Set Up Scene first.");
                return;
            }

            AddMatch(profile);
            Scene scene = SceneManager.GetActiveScene();
            AssignSceneIds(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GameMode] Added the match to " + scene.path);
        }

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
                GeneratedAssetGuard.MarkGenerated(profile);
                Debug.Log("[GameMode] Created " + MatchProfilePath);
            }
            else if (profile.BriefcasePrefab == null)
            {
                profile.BriefcasePrefab = briefcase;
                EditorUtility.SetDirty(profile);
            }

            // Filled only when empty: a designer's choice of beacon (or none) is kept.
            if (profile.BeaconMaterial == null)
            {
                profile.BeaconMaterial = EnsureBeaconMaterial();
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
            GeneratedAssetGuard.MarkGenerated(firm);
            Debug.Log("[GameMode] Created " + path);
            return firm;
        }

        internal static TowerProfile EnsureLaneTower()
        {
            var lane = AssetDatabase.LoadAssetAtPath<TowerProfile>(LaneTowerPath);
            if (lane != null)
                return lane;

            var standard = AssetDatabase.LoadAssetAtPath<TowerProfile>(DefaultTowerPath);
            if (standard == null || standard.Lobby == null)
            {
                Debug.LogError("[GameMode] No tower profile with floor templates at " + DefaultTowerPath + ". Run Seal the Deal > Greybox > Set Up Scene once, then this again.");
                return null;
            }

            lane = Object.Instantiate(standard);
            lane.RandomFloors = 2;
            lane.CheckpointEvery = 2;
            AssetDatabase.CreateAsset(lane, LaneTowerPath);
            GeneratedAssetGuard.MarkGenerated(lane);
            Debug.Log("[GameMode] Created " + LaneTowerPath);
            return lane;
        }

        /// <summary>Overwrites the briefcase prefab from the tool, only while the tool still owns it (GeneratedAssetGuard). Saving over the path keeps its GUID.</summary>
        [MenuItem("Seal the Deal/Lanes/Rebuild GameMode Briefcase")]
        public static void RebuildBriefcase()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Rebuild GameMode Briefcase", "Stop Play Mode first.", "OK");
                else
                    Debug.LogWarning("[GameMode] Stop Play Mode first.");
                return;
            }
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
            GeneratedAssetGuard.MarkGenerated(material);
            return material;
        }

        static Material EnsureBeaconMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BeaconPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Seal the Deal/Briefcase Beacon");
            if (shader == null)
            {
                Debug.LogWarning("[GameMode] The briefcase beacon shader is missing; no beacon.");
                return null;
            }

            material = new Material(shader) { name = "BriefcaseBeacon" };
            AssetDatabase.CreateAsset(material, BeaconPath);
            GeneratedAssetGuard.MarkGenerated(material);
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
            GeneratedAssetGuard.MarkGenerated(material);
            return material;
        }

        static void EnsurePlayerPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[GameMode] No Player.prefab. Run Seal the Deal > Greybox > Set Up Scene first.");
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

        internal static void FrameStreet(TowerProfile tower)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            cam.transform.position = new Vector3(-tower.Width * 0.5f - 22f, 12f, 0f);
            cam.transform.rotation = Quaternion.Euler(-8f, 90f, 0f);
        }

        internal static void EnsureTower(TowerProfile profile)
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

        internal static void AssignSceneIds(Scene scene)
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

            // The match's sounds, from the shared UI sound set.
            if (match.GetComponent<MatchSounds>() == null)
                match.gameObject.AddComponent<MatchSounds>();

            GameMenu menu = Object.FindAnyObjectByType<GameMenu>();
            if (menu == null)
            {
                Debug.LogError("[GameMode] No game menu in the scene, so the match has no HUD or menu section. Run Seal the Deal > Greybox > Set Up Scene first.");
                return;
            }

            EnsureHud(menu, match);
            EnsureMenuSection(menu, match);
        }

        const string KitFolder = "Assets/_Project/UI/Kit";
        const string KitTokensPath = "Assets/_Project/UI/Tokens/UiTokens.asset";

        sealed class Kit
        {
            public UiTokens Tokens;
            public GameObject Plate, StatusLine, KeyHint, Primary;
            public Sprite White, Outline;

            public static Kit Load()
            {
                var kit = new Kit
                {
                    Tokens = AssetDatabase.LoadAssetAtPath<UiTokens>(KitTokensPath),
                    Plate = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/Plate.prefab"),
                    StatusLine = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/StatusLine.prefab"),
                    KeyHint = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/KeyHint.prefab"),
                    Primary = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/Button_Primary.prefab"),
                    White = AssetDatabase.LoadAssetAtPath<Sprite>(KitFolder + "/Sprites/Kit_White.png"),
                    Outline = AssetDatabase.LoadAssetAtPath<Sprite>(KitFolder + "/Sprites/Kit_Outline1.png")
                };
                bool complete = kit.Tokens != null && kit.Plate != null && kit.StatusLine != null && kit.KeyHint != null && kit.Primary != null && kit.White != null && kit.Outline != null;
                if (!complete)
                    Debug.LogError("[GameMode] The UI kit is missing pieces under " + KitFolder + ". Run Seal the Deal > Lanes > Set Up UI first.");
                return complete ? kit : null;
            }
        }

        static void EnsureHud(GameMenu menu, MatchDirector match)
        {
            Transform hud = menu.transform.Find("Hud");
            if (hud == null)
                return;

            MatchHud existing = menu.GetComponent<MatchHud>();
            if (OnKit(existing))
            {
                // A kit HUD from before the marker's edge image was wired: fill only that slot.
                Transform edge = hud.Find(HudName + "/Marker/Edge");
                if (edge != null)
                    FillIfEmpty(existing, "_markerEdgeImage", edge.GetComponent<UnityEngine.UI.Image>());
                AddClockWarningIfMissing(existing, hud);
                AddEdgeMarkerIfMissing(existing, hud);
                return;
            }

            Kit kit = Kit.Load();
            if (kit == null)
                return;

            Transform old = hud.Find(HudName);
            if (old != null)
            {
                Debug.Log("[GameMode] Replacing the pre-kit match HUD this tool built.");
                Object.DestroyImmediate(old.gameObject);
            }

            MatchHud component = existing != null ? existing : menu.gameObject.AddComponent<MatchHud>();
            BuildHud(kit, hud, component, match);
        }

        static void BuildHud(Kit kit, Transform hud, MatchHud component, MatchDirector match)
        {
            UiTokens t = kit.Tokens;
            RectTransform root = UiKitAssets.Node(hud, HudName);
            UiKitAssets.Stretch(root);
            var so = new SerializedObject(component);
            void Wire(string field, Object value) => so.FindProperty(field).objectReferenceValue = value;

            // Clock: top centre.
            RectTransform clock = PlateAt(kit, root, "Clock", new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(240f, 84f));
            Wire("_clockPlate", clock);
            Wire("_clockCaption", UiKitAssets.Label(clock, "Caption", 0f, 8f, 240f, 18f, "QUARTER", t.Caption, t.Ink2, TextAlignmentOptions.Center));
            Wire("_clockText", UiKitAssets.Label(clock, "Time", 0f, 26f, 240f, 52f, "15:00", t.Heading, t.Navy, TextAlignmentOptions.Center));
            Wire("_clockWarning", ClockWarning(kit, clock));
            Wire("_edgeMarker", EdgeMarker(root));

            // Briefcase tracker: top right, a bar in the holder's colour down its left edge.
            RectTransform tracker = PlateAt(kit, root, "Tracker", new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(460f, 88f));
            Wire("_trackerBar", UiKitAssets.Box(tracker, "Bar", 0f, 0f, 8f, 88f, t.Silver, kit.White));
            UiKitAssets.Label(tracker, "Caption", 24f, 10f, 420f, 18f, "BRIEFCASE", t.Caption, t.Ink2);
            Wire("_trackerHolder", UiKitAssets.Label(tracker, "Holder", 24f, 28f, 420f, 28f, string.Empty, t.Item, t.Navy));
            Wire("_trackerWhere", UiKitAssets.Label(tracker, "Where", 24f, 58f, 420f, 22f, string.Empty, t.Label, t.Ink2));

            // Open-floor ladder: left middle, roof at the top. Rows are copied from the template at run time.
            RectTransform ladder = PlateAt(kit, root, "Ladder", new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(300f, 200f));
            Wire("_ladderPlate", ladder);
            Wire("_ladderCaption", UiKitAssets.Label(ladder, "Caption", 16f, 12f, 268f, 18f, "OPEN TO RECEPTION", t.Caption, t.Navy));
            RectTransform rows = UiKitAssets.At(ladder, "Rows", 16f, 40f, 268f, 0f);
            Wire("_ladder", rows);
            RectTransform row = UiKitAssets.At(rows, "Row", 0f, 0f, 268f, 24f);
            UiKitAssets.Box(row, "Box", 0f, 6f, 12f, 12f, t.Cobalt, kit.White);
            UiKitAssets.Label(row, "Label", 22f, 0f, 220f, 24f, "Floor", t.Label, t.Navy);
            UiKitAssets.Box(row, "Mark", 250f, 6f, 12f, 12f, t.Silver, kit.White);
            Wire("_ladderRow", row);
            Wire("_openSprite", kit.White);
            Wire("_shutSprite", kit.Outline);

            // Countdown: large, above the crosshair.
            RectTransform countdown = Anchored(UiKitAssets.Node(root, "Countdown"), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(240f, 120f));
            countdown.localScale = Vector3.one * 2f;
            var countdownGroup = countdown.gameObject.AddComponent<CanvasGroup>();
            countdownGroup.alpha = 0f;
            countdownGroup.blocksRaycasts = false;
            Wire("_countdownGroup", countdownGroup);
            Wire("_countdownText", UiKitAssets.Label(countdown, "Number", 0f, 0f, 240f, 120f, "5", t.Title, t.Navy, TextAlignmentOptions.Center));

            // Floor-opened banner: under the clock.
            RectTransform banner = PlateAt(kit, root, "Banner", new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(760f, 56f));
            Wire("_banner", banner);
            Wire("_bannerGroup", Group(banner));
            TMP_Text bannerText = UiKitAssets.Label(banner, "Text", 20f, 0f, 720f, 56f, string.Empty, t.Item, t.Navy, TextAlignmentOptions.Center);
            bannerText.richText = true;
            Wire("_bannerText", bannerText);

            // One-off lines and refusals: the kit's status line on a plate, low centre.
            RectTransform notice = PlateAt(kit, root, "Notice", new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(820f, 64f));
            Wire("_notice", notice);
            Wire("_noticeGroup", Group(notice));
            var line = UiKitAssets.Instance<UiStatusLine>(kit.StatusLine, notice, "Line");
            UiKitAssets.Place((RectTransform)line.transform, 20f, 6f, 780f, 52f);
            TMP_Text lineText = line.transform.Find("Text").GetComponent<TMP_Text>();
            lineText.rectTransform.sizeDelta = new Vector2(758f, lineText.rectTransform.sizeDelta.y);
            lineText.textWrappingMode = TextWrappingModes.Normal;
            Wire("_noticeLine", line);

            // Key prompt: under the crosshair.
            var hint = (RectTransform)UiKitAssets.Instance<RectTransform>(kit.KeyHint, root, "Prompt");
            Anchored(hint, new Vector2(0.5f, 0.5f), new Vector2(0f, -84f), new Vector2(380f, 40f));
            RectTransform hintLabel = (RectTransform)hint.Find("Label");
            hintLabel.sizeDelta = new Vector2(320f, hintLabel.sizeDelta.y);
            Wire("_prompt", hint.gameObject);
            Wire("_promptKey", hint.Find("Key").GetComponent<TMP_Text>());
            Wire("_promptLabel", hintLabel.GetComponent<TMP_Text>());
            hint.gameObject.SetActive(false);

            // The marker over the briefcase on screen.
            RectTransform marker = Anchored(UiKitAssets.Node(root, "Marker"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            Wire("_markerFill", UiKitAssets.Img(UiKitAssets.Fill(marker, "Fill", 3f), kit.White, t.Silver));
            Wire("_markerEdgeImage", UiKitAssets.Img(UiKitAssets.Fill(marker, "Edge"), kit.Outline, t.Navy));
            Wire("_marker", marker);

            Wire("_match", match);
            Wire("_tokens", t);
            Wire("_root", root.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();
            ApplyV3MatchHud(root);
        }

        /// <summary>Applies the V3 match header and floor list to an authored match HUD; keeps its bindings and remaining HUD intact.</summary>
        internal static void ApplyV3MatchHud(Transform root)
        {
            UiTokens t = AssetDatabase.LoadAssetAtPath<UiTokens>(KitTokensPath);
            RectTransform clock = (RectTransform)root.Find("Clock");
            RectTransform tracker = (RectTransform)root.Find("Tracker");
            RectTransform ladder = (RectTransform)root.Find("Ladder");
            var top = new Vector2(0.5f, 1f);
            ladder.gameObject.SetActive(false);
            tracker.gameObject.SetActive(false);
            var hud = new SerializedObject(root.GetComponentInParent<MatchHud>(true));
            hud.FindProperty("_compactFloors").boolValue = true;
            hud.ApplyModifiedPropertiesWithoutUndo();

            Anchored(clock, top, new Vector2(0, -18), new Vector2(144, 57));
            foreach (var name in new[] { "Shadow", "Edge", "Surface", "HudFace" })
            {
                var layer = clock.Find(name);
                if (layer != null)
                    layer.gameObject.SetActive(false);
            }
            MatchText(clock, "Caption", 0, 0, 144, 17, 13, t.Navy, TextAlignmentOptions.Center);
            MatchText(clock, "Time", 0, 7, 144, 48, 31, Color.white, TextAlignmentOptions.Center);
            var time = clock.Find("Time").GetComponent<TMP_Text>();
            time.characterSpacing = 0f;
            const string clockInk = "Assets/_Project/UI/Fonts/HUD Clock.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(clockInk);
            if (material == null)
            {
                material = new Material(time.fontSharedMaterial);
                material.EnableKeyword("OUTLINE_ON");
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
                material.SetColor(ShaderUtilities.ID_OutlineColor, t.Navy);
                AssetDatabase.CreateAsset(material, clockInk);
            }
            time.fontSharedMaterial = material;
            ConfigureBriefcaseArrows(root.Find("BriefcaseEdgeMarker"));

            var prompt = (RectTransform)root.Find("Prompt");
            MatchSurface(prompt, t.Paper, "KeyCap");
            prompt.Find("HudFace").SetAsFirstSibling();
            UiKitAssets.Place((RectTransform)prompt.Find("KeyCap"), 12, 5, 48, 30);
            MatchText(prompt, "Key", 12, 5, 48, 30, 18, t.Navy, TextAlignmentOptions.Center);
            MatchText(prompt, "Label", 70, 0, 290, 40, 20, t.Navy);
            foreach (var part in new[] { "KeyCap", "Key", "Label" })
            {
                var rect = (RectTransform)prompt.Find(part);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                if (rect.TryGetComponent<TMP_Text>(out var label))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }

            var banner = (RectTransform)root.Find("Banner");
            Anchored(banner, new Vector2(0.5f, 1f), new Vector2(0, -98), new Vector2(670, 60));
            MatchSurface(banner, t.CobaltDeep, "Text");
            MatchText(banner, "Text", 16, 0, 638, 60, 24, Color.white, TextAlignmentOptions.Center);

            var notice = (RectTransform)root.Find("Notice");
            Anchored(notice, new Vector2(0.5f, 0f), new Vector2(0, 54), new Vector2(540, 70));
            MatchSurface(notice, t.Paper, "Line");
            var line = (RectTransform)notice.Find("Line");
            UiKitAssets.Place(line, 0, 0, 540, 70);
            UiKitAssets.Place((RectTransform)line.Find("Mark"), 12, 18, 3, 34);
            UiKitAssets.Place((RectTransform)line.Find("MarkOutline"), 12, 18, 3, 34);
            MatchText(line, "Text", 26, 2, 498, 66, 20, t.Navy);
        }

        static void ConfigureBriefcaseArrows(Transform marker)
        {
            if (marker == null)
                return;
            var body = marker.Find("Body");
            var pointer = (RectTransform)body.Find("Pointer");
            var arrow = pointer.GetComponent<UnityEngine.UI.Image>();
            arrow.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiKitArt.Folder + "/Kit_TitleV3Chevron.png");
            arrow.preserveAspect = true;
            pointer.sizeDelta = new Vector2(17, 25);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pointer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(arrow);
            var partner = body.Find("OppositeChevron");
            if (partner == null)
                partner = Object.Instantiate(pointer, body, false);
            partner.name = "OppositeChevron";
            partner.localRotation = Quaternion.Euler(0, 0, 180);
            var component = marker.GetComponent<UiEdgeMarker>();
            UiKitAssets.Set(component, "_oppositeChevron", partner.GetComponent<UnityEngine.UI.Image>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        static void MatchSurface(RectTransform panel, Color colour, string before)
        {
            var face = (RectTransform)panel.Find("HudFace");
            if (face == null)
            {
                face = UiKitAssets.Fill(panel, "HudFace");
                UiKitAssets.Img(face, null, colour);
                face.SetSiblingIndex(panel.Find(before).GetSiblingIndex());
            }
            var image = face.GetComponent<UnityEngine.UI.Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiKitArt.Folder + "/Kit_Round16.png");
            image.type = UnityEngine.UI.Image.Type.Sliced;
            colour.a = 0.88f;
            image.color = colour;
            foreach (var name in new[] { "Surface", "Shadow", "Edge" })
            {
                var layer = panel.Find(name);
                if (layer != null)
                    layer.gameObject.SetActive(false);
            }
        }

        static void MatchText(Transform parent, string path, float x, float y, float w, float h,
            float size, Color colour, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var text = parent.Find(path).GetComponent<TMP_Text>();
            UiKitAssets.Place(text.rectTransform, x, y, w, h);
            text.fontSize = size;
            text.color = colour;
            text.alignment = alignment;
        }

        static bool OnKit(Component component)
        {
            return component != null && new SerializedObject(component).FindProperty("_tokens").objectReferenceValue != null;
        }

        static RectTransform PlateAt(Kit kit, Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var plate = UiKitAssets.Instance<RectTransform>(kit.Plate, parent, name);
            return Anchored(plate, anchor, position, size);
        }

        static RectTransform Anchored(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static CanvasGroup Group(RectTransform rect)
        {
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        static void EnsureMenuSection(GameMenu menu, MatchDirector match)
        {
            Transform card = menu.transform.Find("Menu/Card");
            if (card == null)
                return;

            MatchMenuPanel existing = menu.GetComponent<MatchMenuPanel>();
            if (OnKit(existing))
                return;

            Kit kit = Kit.Load();
            if (kit == null)
                return;

            Transform old = card.Find(SectionName);
            int index = old != null ? old.GetSiblingIndex() : -1;
            if (old != null)
            {
                Debug.Log("[GameMode] Replacing the pre-kit match section this tool built.");
                Object.DestroyImmediate(old.gameObject);
            }

            UiTokens t = kit.Tokens;
            var section = UiKitAssets.Instance<RectTransform>(kit.Plate, card, SectionName);
            var layout = section.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = 196f;
            Transform gameSection = card.Find("GameSection");
            section.SetSiblingIndex(index >= 0 ? index : gameSection != null ? gameSection.GetSiblingIndex() : card.childCount - 1);

            MatchMenuPanel panel = existing != null ? existing : menu.gameObject.AddComponent<MatchMenuPanel>();
            WireMenuSection(kit, section, panel, menu, match);
        }

        static void WireMenuSection(Kit kit, RectTransform section, MatchMenuPanel panel, GameMenu menu, MatchDirector match)
        {
            UiTokens t = kit.Tokens;
            TMP_Text headline = UiKitAssets.Label(section, "Headline", 20f, 14f, 584f, 30f, string.Empty, t.Item, t.Navy);
            headline.fontStyle = FontStyles.Bold;
            var reason = UiKitAssets.Instance<UiStatusLine>(kit.StatusLine, section, "Reason");
            UiKitAssets.Place((RectTransform)reason.transform, 20f, 48f, 584f, 64f);
            TMP_Text reasonText = reason.transform.Find("Text").GetComponent<TMP_Text>();
            reasonText.rectTransform.sizeDelta = new Vector2(562f, 64f);
            reasonText.textWrappingMode = TextWrappingModes.Normal;
            var action = UiKitAssets.Instance<UiButton>(kit.Primary, section, "MatchActionButton");
            RectTransform actionRect = (RectTransform)action.transform;
            UiKitAssets.Place(actionRect, 20f, 120f, 584f, t.PrimaryHeight);
            TMP_Text actionLabel = action.transform.Find("Plate/Label")?.GetComponent<TMP_Text>();
            if (section.parent.name == "MatchSlot")
                StyleMatchSummary(section, t);


            var so = new SerializedObject(panel);
            so.FindProperty("_match").objectReferenceValue = match;
            so.FindProperty("_menu").objectReferenceValue = menu;
            so.FindProperty("_tokens").objectReferenceValue = t;
            so.FindProperty("_section").objectReferenceValue = section.gameObject;
            so.FindProperty("_headline").objectReferenceValue = headline;
            so.FindProperty("_reason").objectReferenceValue = reason;
            so.FindProperty("_action").objectReferenceValue = action;
            so.FindProperty("_actionLabel").objectReferenceValue = actionLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void StyleMatchSummary(RectTransform section, UiTokens t)
        {
            foreach (var name in new[] { "Edge", "Surface", "Shadow" })
            {
                var layer = section.Find(name);
                if (layer != null)
                    layer.gameObject.SetActive(false);
            }
            var title = section.Find("Headline").GetComponent<TMP_Text>();
            title.font = t.Heading.Font;
            title.fontStyle = t.Heading.Style;
            title.characterSpacing = t.Heading.Tracking;
            title.fontSize = 33;
            title.enableAutoSizing = true;
            title.fontSizeMax = 33;
            title.fontSizeMin = 22;
            UiKitAssets.Place(title.rectTransform, 0, 0, 1060, 59);
            UiKitAssets.Place((RectTransform)section.Find("Reason"), 0, 67, 1060, 62);
            UiKitAssets.Place((RectTransform)section.Find("Reason/Text"), 22, 0, 1038, 62);
            var button = section.Find("MatchActionButton").GetComponent<UiButton>();
            UiKitAssets.Place((RectTransform)button.transform, 0, 138, 530, t.PrimaryHeight);
            UiKitScreens.AddV3FocusMark(button);
        }
        static UnityEngine.UI.Image ClockWarning(Kit kit, Transform clock)
        {
            Transform caption = clock.Find("Caption");
            RectTransform fill = UiKitAssets.Fill(clock, "Warning");
            if (caption != null)
                fill.SetSiblingIndex(caption.GetSiblingIndex());
            UnityEngine.UI.Image image = UiKitAssets.Img(fill, kit.White, kit.Tokens.Amber);
            image.enabled = false;
            return image;
        }

        const string EdgeMarkerPrefabPath = "Assets/_Project/UI/Kit/EdgeMarker.prefab";

        static UiEdgeMarker EdgeMarker(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EdgeMarkerPrefabPath);
            if (prefab == null)
                return null;

            UiEdgeMarker marker = UiKitAssets.Instance<UiEdgeMarker>(prefab, parent, "BriefcaseEdgeMarker");
            UiKitAssets.Stretch((RectTransform)marker.transform);
            return marker;
        }

        static void AddEdgeMarkerIfMissing(MatchHud hud, Transform hudRoot)
        {
            var serialized = new SerializedObject(hud);
            if (serialized.FindProperty("_edgeMarker").objectReferenceValue != null)
                return;

            Transform root = hudRoot.Find(HudName);
            if (root == null)
                return;

            Transform stale = root.Find("BriefcaseEdgeMarker");
            UiEdgeMarker marker = stale != null ? stale.GetComponent<UiEdgeMarker>() : EdgeMarker(root);
            if (marker != null)
                FillIfEmpty(hud, "_edgeMarker", marker);
        }

        static void AddClockWarningIfMissing(MatchHud hud, Transform hudRoot)
        {
            var serialized = new SerializedObject(hud);
            if (serialized.FindProperty("_clockWarning").objectReferenceValue != null)
                return;

            Transform clock = hudRoot.Find(HudName + "/Clock");
            Kit kit = Kit.Load();
            if (clock == null || kit == null)
                return;

            Transform stale = clock.Find("Warning");
            UnityEngine.UI.Image image = stale != null ? stale.GetComponent<UnityEngine.UI.Image>() : ClockWarning(kit, clock);
            FillIfEmpty(hud, "_clockWarning", image);
        }

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
