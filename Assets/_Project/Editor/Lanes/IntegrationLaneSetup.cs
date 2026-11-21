using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CloseTheDeal.Combat;
using CloseTheDeal.Customisation;
using CloseTheDeal.Editor.Art;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Integration;
using CloseTheDeal.Items;
using CloseTheDeal.Lobby;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Player;
using CloseTheDeal.Tower;
using CloseTheDeal.UI;
using FishNet;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>
    /// Builds the one scene where every lane plays together: the game mode's towers, plaza,
    /// skybridges and floor gates, shooting and breaking glass, the pickups, and the lobby's
    /// reception at street level beside the towers, joined by the glue in Scripts/Integration.
    /// It calls each lane's own builder rather than copying it, and only adds: an asset that
    /// exists is never changed (AGENTS.md §4). Design: docs/systems/INTEGRATION.md.
    ///
    /// Menu: Close the Deal > Lanes > Set Up Integration (asks before rebuilding the scene).
    /// Shell: -executeMethod CloseTheDeal.Editor.Lanes.IntegrationLaneSetup.SetUpFromCommandLine
    /// </summary>
    public static class IntegrationLaneSetup
    {
        internal const string ScenePath = "Assets/_Project/Scenes/Lanes/Lane_Integration.unity";
        const string ProfileFolder = "Assets/_Project/Profiles/Integration";
        const string GunMapPath = ProfileFolder + "/GunPickupMap.asset";
        const string LootPath = ProfileFolder + "/FloorLootTable.asset";
        const string WeaponFolder = "Assets/_Project/Profiles/Weapons/";
        const string ItemFolder = "Assets/_Project/Profiles/Items/";
        const string ItemPrefabs = "Assets/_Project/Prefabs/Items/";
        const string PropPrefabs = "Assets/_Project/Prefabs/Props/";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        /// <summary>
        /// The reception stands at street level past the towers' +X end, its glass front looking
        /// back down the street at both towers' bases. The lobby's own builder lays it out from
        /// its window edge (X along the glass, -Z into the room); turned a quarter left, the room
        /// runs 16 m along +X and 24 m along Z, centred on the street. Clear of the 28 m towers.
        /// </summary>
        static readonly Vector3 ReceptionAt = new(20f, 0f, -12f);
        const float ReceptionYaw = -90f;
        static readonly Vector3 FirmStandA = new(6.5f, 0f, -8.5f);
        static readonly Vector3 FirmStandB = new(17.5f, 0f, -8.5f);

        /// <summary>The kit window has no collider above its sill: an unseen pane keeps players waiting in the room.</summary>
        static readonly Vector3 WindowGuardCentre = new(12f, 2.25f, 0.25f);
        static readonly Vector3 WindowGuardSize = new(24f, 4.5f, 0.2f);

        /// <summary>Clear of the game mode's prompt chip just under the crosshair.</summary>
        static readonly Vector2 ItemPromptOffset = new(0f, -140f);

        // ---- Menu ----------------------------------------------------------------------------

        [MenuItem("Close the Deal/Lanes/Set Up Integration")]
        public static void SetUpFromMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Set Up Integration",
                    "Lane_Integration.unity already exists. Rebuild it from scratch? Anything placed or changed in it by hand is lost. Assets and prefabs a person edited are left alone.", "Rebuild", "Keep it"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Build();
        }

        /// <summary>Batch entry: rebuilds the scene without asking. Exit code 0 when it was built.</summary>
        public static void SetUpFromCommandLine()
        {
            bool built = Build();
            EditorApplication.Exit(built ? 0 : 1);
        }

        sealed class LobbyAssets
        {
            public OutfitCatalog Catalog;
            public LobbyRules Rules;
            public SecretaryLines Lines;
            public GameObject Character;
        }

        static bool Build()
        {
            EnsureFolder("Assets/_Project/Profiles", "Integration");
            EnsureGunMap();
            EnsureLootTable();
            Watching("lobby assets", EnsureLobbyAssets);
            Watching("pickups prefabs", PickupsLaneSetup.SetUpPrefabsOnly);
            TowerProfile tower = GameModeLaneSetup.EnsureLaneTower();
            if (tower == null)
                return false;

            string towerPath = AssetDatabase.GetAssetPath(tower);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // A new scene unloads unused assets; references taken before it would save as "none".
            tower = AssetDatabase.LoadAssetAtPath<TowerProfile>(towerPath);

            GameModeLaneSetup.FrameStreet(tower);
            // Street-level spawns are only the fallback when no tower stands; arrivals go to the lobby.
            Watching("greybox play rig", () => GreyboxSceneSetup.EnsurePlayRig(new Vector3(-3f, 0.1f, 0f), new Vector3(3f, 0.1f, 0f)));
            GameModeLaneSetup.EnsureTower(tower);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Watching("game mode", GameModeLaneSetup.AddToOpenScene);
            AddStartingLoadout();
            Watching("shooting", ShootingLaneSetup.AddToOpenScene);
            PickupsLaneSetup.BuildPromptCanvas();
            LowerItemPrompt();
            Transform[] lobbySpawns = BuildReception(LoadLobbyAssets());
            AddIntegration(lobbySpawns);
            RewordMatchMenu();

            EditorSceneManager.SaveScene(scene, ScenePath);
            GameModeLaneSetup.AssignSceneIds(scene);
            OfficeLook.Apply(askFirst: false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Integration] Built " + ScenePath);
            return true;
        }

        // ---- Assets (only created when missing) ----------------------------------------------

        static void EnsureGunMap()
        {
            if (AssetDatabase.LoadAssetAtPath<GunPickupMap>(GunMapPath) != null)
                return;

            var map = ScriptableObject.CreateInstance<GunPickupMap>();
            AssetDatabase.CreateAsset(map, GunMapPath);
            var so = new SerializedObject(map);
            SerializedProperty guns = so.FindProperty("_guns");
            guns.arraySize = 2;
            FillGun(guns.GetArrayElementAtIndex(0), "Item_Pistol", "Pistol", "Pistol");
            FillGun(guns.GetArrayElementAtIndex(1), "Item_AR15", "AR15", "AR15");
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Integration] Created " + GunMapPath);
        }

        static void FillGun(SerializedProperty entry, string item, string weapon, string pickup)
        {
            entry.FindPropertyRelative("Item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemFolder + item + ".asset");
            entry.FindPropertyRelative("Weapon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponFolder + weapon + ".asset");
            entry.FindPropertyRelative("Pickup").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(ItemPrefabs + pickup + ".prefab");
        }

        /// <summary>Floors 1–3 of each tower: a gun, a gadget and a piece of furniture on each; none in the lobby.</summary>
        static void EnsureLootTable()
        {
            if (AssetDatabase.LoadAssetAtPath<FloorLootTable>(LootPath) != null)
                return;

            (string prefab, int floor)[] lines =
            {
                (ItemPrefabs + "Pistol.prefab", 1),
                (ItemPrefabs + "WetFloorSign.prefab", 1),
                (PropPrefabs + "Chair.prefab", 1),
                (ItemPrefabs + "AR15.prefab", 2),
                (ItemPrefabs + "FireExtinguisher.prefab", 2),
                (ItemPrefabs + "CubiclePanel.prefab", 2),
                (ItemPrefabs + "GrenadeCase.prefab", 3),
                (PropPrefabs + "Chair.prefab", 3),
            };

            var table = ScriptableObject.CreateInstance<FloorLootTable>();
            AssetDatabase.CreateAsset(table, LootPath);
            var so = new SerializedObject(table);
            SerializedProperty entries = so.FindProperty("_entries");
            entries.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<NetworkObject>(lines[i].prefab);
                entry.FindPropertyRelative("Floor").intValue = lines[i].floor;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Integration] Created " + LootPath);
        }

        /// <summary>The lobby lane's own asset steps, run before the scene is made (they may create assets).</summary>
        static void EnsureLobbyAssets()
        {
            LobbyLaneSetup.EnsureFolders();
            OutfitCatalog catalog = LobbyLaneSetup.EnsureCatalog();
            LobbyLaneSetup.EnsureRules();
            LobbyLaneSetup.EnsureAsset<SecretaryLines>(LobbyLaneSetup.LinesPath, LobbyLaneSetup.FillLines);
            GameObject character = LobbyLaneSetup.EnsureCharacterPrefab(catalog);
            AddOutfitIfMissing(character);
        }

        /// <summary>
        /// The player prefab is add-only here (AGENTS.md §4): the outfit goes on only when the
        /// player has none. The lobby's patch re-seats an existing outfit (it removes an earlier
        /// figure and re-points the view), which would undo what a person or another tool set,
        /// so a player that already wears an outfit is reported and left alone.
        /// </summary>
        static void AddOutfitIfMissing(GameObject character)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            PlayerOutfit outfit = player != null ? player.GetComponent<PlayerOutfit>() : null;
            if (outfit == null)
            {
                LobbyLaneSetup.PatchPlayerPrefab(character);
                return;
            }

            Object view = new SerializedObject(outfit).FindProperty("_view").objectReferenceValue;
            Debug.Log(view != null
                ? "[Integration] Player.prefab already wears an outfit (PlayerOutfit, view " + view.name + "); left as it is."
                : "[Integration] Player.prefab has PlayerOutfit with no view. Left as it is: set its view on the prefab, or run the lobby lane's own setup to seat one.");
        }

        /// <summary>
        /// Runs one lane's step and says if it changed Player.prefab. This tool never rewrites the
        /// player; a lane step that does is named here so its owner can make it add-only.
        /// </summary>
        static void Watching(string step, Action run)
        {
            string before = File.Exists(PlayerPrefabPath) ? File.ReadAllText(PlayerPrefabPath) : null;
            run();
            AssetDatabase.SaveAssets();
            string after = File.Exists(PlayerPrefabPath) ? File.ReadAllText(PlayerPrefabPath) : null;
            if (before != null && before != after)
                Debug.LogWarning($"[Integration] The {step} step changed {PlayerPrefabPath}. Check the diff before committing it: lane tools must only add to the player (AGENTS.md §4).");
        }

        static LobbyAssets LoadLobbyAssets()
        {
            return new LobbyAssets
            {
                Catalog = AssetDatabase.LoadAssetAtPath<OutfitCatalog>(LobbyLaneSetup.CatalogPath),
                Rules = AssetDatabase.LoadAssetAtPath<LobbyRules>(LobbyLaneSetup.RulesPath),
                Lines = AssetDatabase.LoadAssetAtPath<SecretaryLines>(LobbyLaneSetup.LinesPath),
                Character = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyLaneSetup.CharacterPrefabPath)
            };
        }

        // ---- Scene ---------------------------------------------------------------------------

        /// <summary>Everyone starts with the pistol only; the AR-15 is found on floor 2. Added before the shooting lane's step, which then keeps it.</summary>
        static void AddStartingLoadout()
        {
            if (Object.FindAnyObjectByType<StartingLoadout>() != null)
                return;

            var go = new GameObject("Starting Loadout");
            go.AddComponent<StartingLoadout>().Weapons = new[] { AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponFolder + "Pistol.asset") };
        }

        static void LowerItemPrompt()
        {
            GameObject prompt = GameObject.Find("Item Prompts/Prompt");
            if (prompt != null)
                ((RectTransform)prompt.transform).anchoredPosition = ItemPromptOffset;
        }

        /// <summary>The lobby lane's reception, desk, firm stands and fitting room, its roster and its screens, from its own builder.</summary>
        static Transform[] BuildReception(LobbyAssets lobby)
        {
            LobbyLaneSetup.LoadMaterials();
            var root = new GameObject("Reception").transform;
            root.SetPositionAndRotation(ReceptionAt, Quaternion.Euler(0f, ReceptionYaw, 0f));
            LobbyLaneSetup.Shell(root);
            LobbyLaneSetup.Lighting(root);
            LobbyLaneSetup.BackWall(root);
            LobbyLaneSetup.Dressing(root);
            LobbyLaneSetup.Desk(root, lobby.Lines, lobby.Rules, lobby.Character);
            LobbyLaneSetup.Stand(root, lobby.Rules, 0, FirmStandA);
            LobbyLaneSetup.Stand(root, lobby.Rules, 1, FirmStandB);
            LobbyLaneSetup.TailorCorner(root, lobby.Character);
            AddWindowGuard(root);
            root.gameObject.AddComponent<LobbyRoom>();

            LobbyLaneSetup.SpreadSpawns();
            Transform spawns = GameObject.Find("Lobby spawns").transform;
            spawns.SetParent(root, false);
            LobbyLaneSetup.Systems(lobby.Rules, lobby.Lines);
            LobbyLaneSetup.BuildUi(lobby.Rules);

            var points = new Transform[spawns.childCount];
            for (int i = 0; i < points.Length; i++)
                points[i] = spawns.GetChild(i);
            return points;
        }

        static void AddWindowGuard(Transform root)
        {
            var guard = new GameObject("Window guard (no mesh)");
            guard.transform.SetParent(root, false);
            guard.transform.localPosition = WindowGuardCentre;
            var box = guard.AddComponent<BoxCollider>();
            box.size = WindowGuardSize;
            guard.isStatic = true;
        }

        static void AddIntegration(Transform[] lobbySpawns)
        {
            var go = new GameObject("Integration");
            go.AddComponent<NetworkObject>();
            go.AddComponent<SharedHands>();

            var loot = go.AddComponent<FloorLoot>();
            Set(loot, "_table", AssetDatabase.LoadAssetAtPath<FloorLootTable>(LootPath));

            var guns = go.AddComponent<GunPickups>();
            Set(guns, "_map", AssetDatabase.LoadAssetAtPath<GunPickupMap>(GunMapPath));

            var flow = go.AddComponent<LobbyRunFlow>();
            var so = new SerializedObject(flow);
            SerializedProperty spawns = so.FindProperty("_lobbySpawns");
            spawns.arraySize = lobbySpawns.Length;
            for (int i = 0; i < lobbySpawns.Length; i++)
                spawns.GetArrayElementAtIndex(i).objectReferenceValue = lobbySpawns[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The lobby desk starts the match here, so the menu button says it skips the desk (handy alone, where the desk needs both firms).</summary>
        static void RewordMatchMenu()
        {
            MatchMenuPanel panel = Object.FindAnyObjectByType<MatchMenuPanel>(FindObjectsInactive.Include);
            if (panel == null)
                return;

            var so = new SerializedObject(panel);
            so.FindProperty("_startLabel").stringValue = "Start now, skipping the desk";
            so.FindProperty("_rematchLabel").stringValue = "Rematch now, skipping the desk";
            so.FindProperty("_waitingForStart").stringValue = "Ready up at the reception desk.";
            so.FindProperty("_waitingForRematch").stringValue = "Back to reception soon for the next deal.";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }

    /// <summary>
    /// Plays Lane_Integration by script and writes what happened to a report, like the lanes'
    /// own check runs. Solo: one machine hosting with its own player, every join and edge case.
    /// Host and client: two copies of the game on this computer over the local connection.
    /// Requests go through the same calls the keys make; F and G are also pressed on a made-up
    /// keyboard where the check is about the keys. Nothing here is Steam or a second computer.
    ///
    /// Unity -batchmode -nographics -projectPath (project) -local
    ///       -executeMethod CloseTheDeal.Editor.Lanes.IntegrationLaneRun.RunFromShell
    ///       -integrationRole solo|host|client -integrationOut (folder)
    /// </summary>
    public static class IntegrationLaneRun
    {
        const string ActiveKey = "IntegrationLaneRun.Active";
        const string RoleKey = "IntegrationLaneRun.Role";
        const string OutKey = "IntegrationLaneRun.Out";

        static readonly Stack<IEnumerator> Steps = new();
        static readonly StringBuilder Report = new();
        static readonly FieldInfo YawField = typeof(PlayerCamera).GetField("_yaw", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo PitchField = typeof(PlayerCamera).GetField("_pitch", BindingFlags.Instance | BindingFlags.NonPublic);

        static object _waiting;
        static float _waitUntil;
        static bool _started;
        static string _role;
        static string _out;
        static int _failures;
        static int _passes;
        static Keyboard _keyboard;
        static InputSettings.BackgroundBehavior _savedBackgroundBehavior;
        static InputSettings.EditorInputBehaviorInPlayMode _savedEditorInputBehavior;
        static bool _hasSavedInputSettings;
        static int _countdowns;
        static int _builds;

        [MenuItem("Close the Deal/Lanes/Run Integration Check (solo)")]
        static void RunFromMenu() => Begin("solo", "Temp/IntegrationRun");

        public static void RunFromShell() => Begin(Arg("-integrationRole") ?? "solo", Arg("-integrationOut") ?? "Temp/IntegrationRun");

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        static void Begin(string role, string outFolder)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(IntegrationLaneSetup.ScenePath, OpenSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(RoleKey, role);
            SessionState.SetString(OutKey, outFolder);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (SessionState.GetBool(ActiveKey, false))
                EditorApplication.update += Pump;
        }

        /// <summary>Back in edit mode with the check still marked active means someone pressed Stop
        /// part-way: drop it, or the next ordinary Play in any scene would run the script again.</summary>
        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ActiveKey, false))
                return;

            EditorApplication.update -= Pump;
            SessionState.SetBool(ActiveKey, false);
            PlayerInputReader.ScriptedCapture = false;
            LobbyInput.EditorForceCaptured = false;
            CleanupInputAndState();
            Debug.Log("[IntegrationRun] Stopped before the check finished; it will not resume on the next Play.");
        }

        // ---- The stepper ---------------------------------------------------------------------

        static void Pump()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (!_started)
                StartScript();
            if (!Ready())
                return;

            _waiting = null;
            if (!Step())
                Finish();
        }

        static void StartScript()
        {
            _started = true;
            _role = SessionState.GetString(RoleKey, "solo");
            _out = SessionState.GetString(OutKey, "Temp/IntegrationRun");
            Directory.CreateDirectory(_out);
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            _savedBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            _savedEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _hasSavedInputSettings = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>("Integration check keyboard");
            Steps.Push(_role == "client" ? ClientScript() : _role == "host" ? HostScript() : SoloScript());
        }

        static bool Step()
        {
            while (Steps.Count > 0)
            {
                bool more;
                try
                {
                    more = Steps.Peek().MoveNext();
                }
                catch (Exception e)
                {
                    Check("the script ran to its end", false, e.ToString());
                    return false;
                }

                if (!more)
                {
                    Steps.Pop();
                    continue;
                }

                object current = Steps.Peek().Current;
                if (current is IEnumerator inner)
                {
                    Steps.Push(inner);
                    continue;
                }

                _waiting = current;
                if (current is float seconds)
                    _waitUntil = Time.time + seconds;
                return true;
            }

            return false;
        }

        static bool Ready()
        {
            switch (_waiting)
            {
                case float:
                    return Time.time >= _waitUntil;
                case DateTime at:
                    return DateTime.UtcNow >= at;
                case Until until:
                    return until.Done();
                default:
                    return true;
            }
        }

        sealed class Until
        {
            readonly Func<bool> _condition;
            readonly float _giveUpAt;
            readonly string _what;

            public Until(Func<bool> condition, float timeout, string what)
            {
                _condition = condition;
                _giveUpAt = Time.time + timeout;
                _what = what;
            }

            public bool Done()
            {
                if (_condition())
                    return true;
                if (Time.time < _giveUpAt)
                    return false;

                Check("waited for " + _what, false, "timed out");
                return true;
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Pump;
            SessionState.SetBool(ActiveKey, false);
            PlayerInputReader.ScriptedCapture = false;
            LobbyInput.EditorForceCaptured = false;
            Note($"RESULT: {_failures} failed, {_passes} passed ({_role}).");
            File.WriteAllText(Path.Combine(_out, _role + "_report.txt"), Report.ToString());
            bool passed = _failures == 0;
            CleanupInputAndState();
            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 3);
            else
                EditorApplication.ExitPlaymode();
        }

        static void CleanupInputAndState()
        {
            if (_keyboard != null)
            {
                if (_keyboard.added)
                    InputSystem.RemoveDevice(_keyboard);
                _keyboard = null;
            }
            if (_hasSavedInputSettings)
            {
                InputSystem.settings.backgroundBehavior = _savedBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = _savedEditorInputBehavior;
                _hasSavedInputSettings = false;
            }
            Steps.Clear();
            Report.Clear();
            _waiting = null;
            _waitUntil = 0f;
            _started = false;
            _role = null;
            _out = null;
            _failures = 0;
            _passes = 0;
            _countdowns = 0;
            _builds = 0;
        }

        static void Note(string line)
        {
            Report.AppendLine(line);
            Debug.Log("[IntegrationRun] " + line);
        }

        static void Check(string what, bool ok, string detail = "")
        {
            if (ok)
                _passes++;
            else
                _failures++;
            Note($"{(ok ? "PASS" : "FAIL")}  {what}{(detail.Length > 0 ? "  · " + detail : "")}");
        }

        static string V(Vector3 v) => $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})";

        // ---- Reading the game ----------------------------------------------------------------

        static FirmMember Me => FirmMember.Local;
        static ItemCarrier MyHands => ItemCarrier.Local;
        static WeaponHolder MyGuns => Me != null ? Me.GetComponent<WeaponHolder>() : null;
        static MatchDirector Match => MatchDirector.Instance;
        static LobbyRunFlow Flow => Object.FindAnyObjectByType<LobbyRunFlow>();
        static string Gun(WeaponHolder holder, int slot) => holder.WeaponInSlot(slot) != null ? holder.WeaponInSlot(slot).name : "empty";
        static string Slots(WeaponHolder holder) => Gun(holder, 0) + " / " + Gun(holder, 1);

        static FirmMember Other()
        {
            IReadOnlyList<FirmMember> all = FirmMember.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != Me)
                    return all[i];
            }

            return null;
        }

        static void CountEvents()
        {
            Match.OnPhaseChanged += (_, next) => _countdowns += next == MatchPhase.Countdown ? 1 : 0;
            TowerBuilder.Instance.OnBuilt += () => _builds++;
        }

        /// <summary>The nearest loose pickup of this item to a point, or null.</summary>
        static Pickup NearestLoose(string itemName, Vector3 from)
        {
            Pickup best = null;
            float bestDistance = float.MaxValue;
            foreach (Pickup pickup in Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None))
            {
                float distance = (pickup.transform.position - from).sqrMagnitude;
                if (pickup.IsSpawned && pickup.State == PickupState.InWorld && pickup.Definition.name == itemName && distance < bestDistance)
                {
                    best = pickup;
                    bestDistance = distance;
                }
            }

            return best;
        }

        static int CountLoose(string itemName)
        {
            int count = 0;
            foreach (Pickup pickup in Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                count += pickup.IsSpawned && pickup.Definition.name == itemName ? 1 : 0;
            return count;
        }

        static Vector3 FloorCentre(int tower, int floor) => Match.Arena.FloorPoint(tower, floor, new Vector3(0f, 0f, -7f));

        /// <summary>Floor under the probe and room for a whole player standing there.</summary>
        static bool Fits(Vector3 probe, out Vector3 feet)
        {
            int solid = ~(LayerMask.GetMask("Player") | LayerMask.GetMask("Prop"));
            feet = default;
            if (!Physics.Raycast(probe + Vector3.up * 1.2f, Vector3.down, out RaycastHit ground, 2.5f, solid, QueryTriggerInteraction.Ignore))
                return false;

            feet = ground.point + Vector3.up * 0.05f;
            return !Physics.CheckCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.45f, 0.35f, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);
        }

        /// <summary>A place a player fits on a ring around a point, with a clear line from their eye to it; the point itself in a pinch.</summary>
        static Vector3 StandBeside(Vector3 target, float radius)
        {
            int solid = ~(LayerMask.GetMask("Player") | LayerMask.GetMask("Prop"));
            for (int i = 0; i < 16; i++)
            {
                Vector3 probe = target + Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * radius;
                if (Fits(probe, out Vector3 feet) && !Physics.Linecast(feet + Vector3.up * 1.6f, target, solid, QueryTriggerInteraction.Ignore))
                    return feet;
            }

            return target + Vector3.up * 0.1f;
        }

        /// <summary>An open spot on a floor of a tower, well away from the stairwell.</summary>
        static Vector3 OpenSpot(int tower, int floor)
        {
            for (float x = -8f; x <= 8f; x += 2f)
            {
                for (float z = -3f; z >= -11f; z -= 2f)
                {
                    var local = new Vector3(x, 0f, z);
                    Vector3 spot = Match.Arena.FloorPoint(tower, floor, local);
                    if ((local - Object.FindAnyObjectByType<MatchGates>().StairCentre).sqrMagnitude > 16f && Fits(spot, out Vector3 feet) && Mathf.Abs(feet.y - spot.y) < 0.2f)
                        return feet;
                }
            }

            return FloorCentre(tower, floor) + Vector3.up * 0.1f;
        }

        static void Teleport(FirmMember who, Vector3 feet)
        {
            who.ServerTeleport(feet);
        }

        static void LookAt(Vector3 point)
        {
            PlayerCamera cam = PlayerCamera.Instance;
            Vector3 eye = Me.Position + Vector3.up * Me.Motor.EyeHeight;
            Vector3 direction = (point - eye).normalized;
            YawField.SetValue(cam, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
            PitchField.SetValue(cam, -Mathf.Asin(direction.y) * Mathf.Rad2Deg);
        }

        static void SetCaptured(bool captured)
        {
            PlayerInputReader.ScriptedCapture = captured;
            LobbyInput.EditorForceCaptured = captured;
        }

        /// <summary>A key down for a few frames, then up, on the check's own keyboard.</summary>
        static IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return 0.15f;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return 0.25f;
        }

        static IEnumerator StartGame(bool host)
        {
            var lobby = Object.FindAnyObjectByType<CloseTheDeal.Net.SteamLobby>();
            yield return new Until(() => lobby.Ready, 30f, "the network to be ready");
            if (host)
                lobby.Host();
            else
                yield return KeepJoining(lobby);

            yield return new Until(() => Me != null && MyHands != null, 60f, "this machine's player to spawn");
            CountEvents();
            yield return 1.5f;
        }

        static IEnumerator KeepJoining(CloseTheDeal.Net.SteamLobby lobby)
        {
            float nextTry = 0f;
            yield return new Until(() =>
            {
                if (Me != null)
                    return true;
                if (!lobby.IsActive && Time.time >= nextTry)
                {
                    nextTry = Time.time + 2f;
                    lobby.JoinLocal();
                }

                return false;
            }, 120f, "the client to join");
        }

        /// <summary>Two copies agree on a start time: the next half-minute at least six seconds away.</summary>
        static DateTime SharedStart()
        {
            long seconds = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() + 6;
            return DateTimeOffset.FromUnixTimeSeconds((seconds / 30 + 1) * 30).UtcDateTime;
        }

        // ---- Solo ------------------------------------------------------------------------------

        static IEnumerator SoloScript()
        {
            Note("ROLE solo: one copy hosting with its own player. Nothing here was seen from a second machine.");
            yield return StartGame(true);
            yield return Arrival();
            AllowOneFirmStart();
            yield return DeskStartsOnce();
            yield return new Until(() => Match.Phase == MatchPhase.Running, 15f, "the countdown to end");
            yield return GunsIntoSlots();
            yield return FullSlots();
            yield return BriefcaseAndHands();
            yield return MenuBlocksKeys();
            yield return CarrierDies();
            yield return CarryUpAndWin();
            yield return BackToLobby();
            yield return BothStartPaths();
            yield return DeskRematch();
        }

        static IEnumerator Arrival()
        {
            WeaponHolder guns = MyGuns;
            Check("arrival: spawned in the lobby reception", LobbyRoom.Instance != null && LobbyRoom.Instance.Contains(Me.Position), V(Me.Position));
            Check("arrival: starting guns are the pistol only", Gun(guns, 0) == "Pistol" && Gun(guns, 1) == "empty", Slots(guns));
            Check("arrival: no gun fires in the lobby", WeaponHolder.FireHeld != null && WeaponHolder.FireHeld(guns));
            Check("arrival: the match is waiting", Match.Phase == MatchPhase.Waiting, Match.Phase.ToString());
            Note($"glass: {Object.FindObjectsByType<CloseTheDeal.Glass.GlassPane>(FindObjectsSortMode.None).Length} breakable panes in the scene (tower windows only count if their template's glass has a collider).");
            yield return new Until(() => LobbyRoster.Instance != null && LobbyRoster.Instance.TryGetLocal(out _), 10f, "this player on the ready board");
        }

        /// <summary>The desk needs both firms by the lobby's rules; alone, the run uses a runtime copy that does not. The asset is untouched.</summary>
        static void AllowOneFirmStart()
        {
            LobbyRoster roster = LobbyRoster.Instance;
            LobbyRules copy = Object.Instantiate(roster.Rules);
            typeof(LobbyRules).GetField("_requireBothFirms", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(copy, false);
            typeof(LobbyRoster).GetField("_rules", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(roster, copy);
            Note("Solo only: the roster uses a runtime copy of the lobby rules with 'Require Both Firms' off.");
        }

        static IEnumerator DeskStartsOnce()
        {
            ReceptionDesk desk = Object.FindAnyObjectByType<ReceptionDesk>();
            LobbyRoster roster = LobbyRoster.Instance;
            desk.OnPress();
            yield return new Until(() => roster.TryGetLocal(out LobbyMember m) && m.State == MemberState.Ready, 5f, "ready at the desk");
            Check("desk: ready at the desk", roster.ReadyCount == 1);

            int countdowns = _countdowns, builds = _builds;
            desk.OnPress();
            desk.OnHold(roster.Rules.StartHoldSeconds + 0.05f);
            yield return new Until(() => Match.Phase == MatchPhase.Countdown, 5f, "the countdown");
            yield return 1.5f;
            Check("desk: the hold started the match exactly once", _countdowns - countdowns == 1 && _builds - builds == 1 && Flow.StartsTaken == 1,
                $"countdowns {_countdowns - countdowns}, tower builds {_builds - builds}, lobby starts taken {Flow.StartsTaken}");
            bool spawned = TowerBuilder.Instance.TryGetSpawn(Me.Firm, Me.Slot, out Vector3 reception, out _);
            Check("desk: lobby firm 0 put this player in Building A's reception", Me.Firm == 0 && spawned && (Me.Position - reception).sqrMagnitude < 1f && Match.Arena.PlaceOf(Me.Position, 0f) == 0, V(Me.Position));
            Check("desk: the ready board stands down outside the lobby", LobbyInteractor.Instance.Blocked);
        }

        /// <summary>Takes a pickup by the same call the F key makes, from a spot beside it.</summary>
        static IEnumerator TakeFromBeside(Pickup pickup)
        {
            Teleport(Me, StandBeside(pickup.Body.worldCenterOfMass, 1f));
            yield return 0.3f;
            MyHands.LocalTake(pickup);
            yield return new Until(() => pickup == null || !pickup.IsSpawned || MyHands.Held != null, 3f, "the host to answer the take");
            yield return 0.3f;
        }

        static IEnumerator GunsIntoSlots()
        {
            yield return FireThree(MyGuns);
            yield return PistolRefills(MyGuns);
            yield return RifleIntoSlot(MyGuns);
        }

        static IEnumerator FireThree(WeaponHolder guns)
        {
            for (int i = 0; i < 3; i++)
            {
                guns.PullTrigger();
                yield return 0.25f;
                guns.ReleaseTrigger();
            }

            yield return 0.3f;
        }

        static IEnumerator PistolRefills(WeaponHolder guns)
        {
            int before = guns.AmmoInMagazine;
            Pickup pistol = NearestLoose("Item_Pistol", FloorCentre(0, 1));
            Check("loot: a pistol lies on floor 1 of Building A", pistol != null && Match.Arena.FloorOf(pistol.transform.position) == 1);
            if (pistol == null)
                yield break;

            yield return TakeFromBeside(pistol);
            Check("pistol: taken, gone from the level and never in the hands", (pistol == null || !pistol.IsSpawned) && MyHands.Held == null);
            Check("pistol: already carried, so its magazine was refilled and no second pistol was added", Slots(guns) == "Pistol / empty" && guns.AmmoInMagazine == 12 && before < 12,
                $"slots {Slots(guns)}, rounds {before} → {guns.AmmoInMagazine}");
        }

        static IEnumerator RifleIntoSlot(WeaponHolder guns)
        {
            Pickup rifle = NearestLoose("Item_AR15", FloorCentre(0, 2));
            Check("loot: an AR-15 lies on floor 2 of Building A", rifle != null && Match.Arena.FloorOf(rifle.transform.position) == 2);
            if (rifle == null)
                yield break;

            yield return TakeFromBeside(rifle);
            Check("AR-15: into the free weapon slot, not the hands", Gun(guns, 1) == "AR15" && MyHands.Held == null && (rifle == null || !rifle.IsSpawned), Slots(guns));
        }

        static IEnumerator FullSlots()
        {
            WeaponHolder guns = MyGuns;
            Pickup pistolB = NearestLoose("Item_Pistol", FloorCentre(1, 1));
            if (pistolB != null)
                yield return TakeFromBeside(pistolB);
            Check("full slots, a gun already carried: refilled, slots unchanged", Slots(guns) == "Pistol / AR15" && MyHands.Held == null, Slots(guns));

            // Two of one gun only happens by hand here; it makes the swap rule testable with two guns in the game.
            WeaponDefinition pistol = guns.WeaponInSlot(0);
            guns.Equip(pistol, 1);
            yield return 0.3f;
            int pistolsBefore = CountLoose("Item_Pistol");
            int hand = guns.ActiveSlot;
            Pickup rifleB = NearestLoose("Item_AR15", FloorCentre(1, 2));
            if (rifleB != null)
                yield return TakeFromBeside(rifleB);
            yield return 0.5f;
            Check("full slots, a new gun: it replaces the gun in hand", Gun(guns, hand) == "AR15" && Gun(guns, 1 - hand) == "Pistol" && MyHands.Held == null, Slots(guns));
            Check("full slots, a new gun: the swapped-out pistol drops as a pickup", CountLoose("Item_Pistol") == pistolsBefore + 1, $"{pistolsBefore} → {CountLoose("Item_Pistol")} loose pistols");
        }

        static IEnumerator BriefcaseAndHands()
        {
            yield return TakeBriefcase();
            Check("briefcase: taken", Match.IsCarrying(Me));
            yield return CarryOntoFloorOne();

            Pickup chair = NearestLoose("Item_Chair", Me.Position);
            Check("loot: a chair to try", chair != null);
            if (chair == null)
                yield break;

            DenyReason? denied = null;
            void OnDenied(DenyReason reason) => denied = reason;
            MyHands.Denied += OnDenied;
            Teleport(Me, StandBeside(chair.Body.worldCenterOfMass, 1f));
            yield return 0.3f;
            SetCaptured(true);
            LookAt(chair.Body.worldCenterOfMass);
            yield return 0.2f;
            yield return Press(Key.F);
            Check("hands: F at a chair while carrying the briefcase is refused as hands full", denied == DenyReason.HandsFull && chair.State == PickupState.InWorld && Match.IsCarrying(Me),
                $"refusal {(denied.HasValue ? denied.ToString() : "none")}, chair {chair.State}");

            denied = null;
            MyHands.LocalTake(chair);
            yield return 0.6f;
            Check("hands: the host refuses the same take as hands full", denied == DenyReason.HandsFull && chair.State == PickupState.InWorld, $"refusal {(denied.HasValue ? denied.ToString() : "none")}");

            yield return Press(Key.G);
            yield return 0.3f;
            Check("hands: G drops the briefcase and nothing else", !Match.IsCarrying(Me) && Match.CaseStatus.State == BriefcaseState.Dropped && MyHands.Held == null);

            yield return ChairBlocksBriefcase(chair);
            MyHands.Denied -= OnDenied;
            SetCaptured(false);
        }

        static IEnumerator TakeBriefcase()
        {
            Briefcase briefcase = Briefcase.Current;
            Teleport(Me, StandBeside(briefcase.Centre, 0.9f));
            yield return 0.3f;
            Me.RequestInteract();
            yield return new Until(() => Match.IsCarrying(Me), 3f, "the briefcase");
        }

        /// <summary>The other way round: a chair in the hands, the briefcase in reach.</summary>
        static IEnumerator ChairBlocksBriefcase(Pickup chair)
        {
            MyHands.LocalTake(chair);
            yield return new Until(() => MyHands.Held == chair, 3f, "the chair");
            Teleport(Me, StandBeside(Briefcase.Current.Centre, 0.9f));
            yield return 0.4f;
            Check("hands: with a chair in the hands the briefcase is not offered", Match.PromptFor(Me) == MatchPrompt.None);
            Check("hands: a player carrying a chair cannot fire", WeaponHolder.FireHeld(MyGuns));
            Me.RequestInteract();
            yield return 0.6f;
            Check("hands: the host refuses the briefcase to full hands", !Match.IsCarrying(Me) && MyHands.Held == chair);
            yield return Press(Key.G);
            yield return 0.3f;
            Check("hands: G then drops the chair only", MyHands.Held == null && chair.State == PickupState.InWorld && Match.CaseStatus.State == BriefcaseState.Dropped);
        }

        static IEnumerator MenuBlocksKeys()
        {
            yield return TakeBriefcase();
            GameMenu menu = Object.FindAnyObjectByType<GameMenu>();
            menu.Open();
            SetCaptured(false);
            yield return 0.2f;
            yield return Press(Key.G);
            Check("menu open: G does not drop the briefcase", Match.IsCarrying(Me) && !PlayerInputReader.Captured);

            Pickup chair = NearestLoose("Item_Chair", Me.Position);
            DenyReason? denied = null;
            void OnDenied(DenyReason reason) => denied = reason;
            MyHands.Denied += OnDenied;
            if (chair != null)
                LookAt(chair.Body.worldCenterOfMass);
            yield return Press(Key.F);
            MyHands.Denied -= OnDenied;
            Check("menu open: F does nothing (no request, no refusal)", denied == null && Match.IsCarrying(Me));
            menu.Resume();
            yield return 0.2f;
        }

        static IEnumerator CarrierDies()
        {
            WeaponHolder guns = MyGuns;
            string slots = Slots(guns);
            var health = Me.GetComponent<Health>();
            health.ApplyHit(1000f, Me.Chest, Vector3.forward, -1, Vector3.zero, Vector3.up, 0f);
            yield return 0.5f;
            Check("death: the carrier dropped the briefcase", health.IsDead && !Match.IsCarrying(Me) && Match.CaseStatus.State != BriefcaseState.Carried);
            yield return new Until(() => !health.IsDead, 8f, "the respawn");
            yield return 0.5f;
            Check("death: guns kept, magazines full", Slots(guns) == slots && guns.AmmoInMagazine == guns.ActiveWeapon.MagazineSize, $"{slots} → {Slots(guns)}");
            yield return new Until(() => Match.CaseStatus.State == BriefcaseState.Dropped, 10f, "the briefcase to lie loose");
        }

        static IEnumerator CarryOntoFloorOne()
        {
            int open = Match.OpenFloor;
            Teleport(Me, OpenSpot(Me.Firm, 1));
            yield return new Until(() => Match.OpenFloor >= 1, 3f, "floor 1 to open");
            Check("gate: carrying the briefcase onto floor 1 opened it for everyone", open == 0 && Match.OpenFloor >= 1, $"open floor {open} → {Match.OpenFloor}");
        }

        static IEnumerator CarryUpAndWin()
        {
            yield return TakeBriefcase();
            Teleport(Me, OpenSpot(Me.Firm, Match.Arena.RoofFloor));
            yield return new Until(() => Match.Phase == MatchPhase.Ended, 4f, "the win");
            MatchSnapshot result = Match.Snapshot;
            Check("roof: carrying it onto the roof wins for this firm", Match.Phase == MatchPhase.Ended && result.Winner == Me.Firm && result.Reason == MatchEndReason.BriefcaseOnRoof,
                $"{Match.Phase}, winner {result.Winner}, {result.Reason}");
        }

        static IEnumerator BackToLobby()
        {
            float seconds = (float)typeof(LobbyRunFlow).GetField("_backToLobbySeconds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Flow);
            yield return new Until(() => LobbyRoom.Instance.Contains(Me.Position), seconds + 4f, "everyone back in the lobby");
            yield return 0.5f;
            LobbyRoster roster = LobbyRoster.Instance;
            roster.TryGetLocal(out LobbyMember me);
            Check("after the result: back in the lobby, empty-handed, desk open, ready cleared", LobbyRoom.Instance.Contains(Me.Position) && !Match.IsCarrying(Me) && MyHands.Held == null && !roster.RunInProgress && me.State == MemberState.Browsing,
                $"at {V(Me.Position)}, run in progress {roster.RunInProgress}, {me.State}");
            Check("after the result: the ready board is back", !LobbyInteractor.Instance.Blocked);
        }

        /// <summary>The desk's start and the menu's rematch in the same frame, then both again during the countdown: one match.</summary>
        static IEnumerator BothStartPaths()
        {
            ReceptionDesk desk = Object.FindAnyObjectByType<ReceptionDesk>();
            LobbyRoster roster = LobbyRoster.Instance;
            desk.OnPress();
            yield return new Until(() => roster.ReadyCount == 1, 5f, "ready again");

            var panel = Object.FindAnyObjectByType<MatchMenuPanel>(FindObjectsInactive.Include);
            var button = (CloseTheDeal.UI.UiButton)typeof(MatchMenuPanel).GetField("_action", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            int countdowns = _countdowns, builds = _builds, requested = Flow.StartsRequested, taken = Flow.StartsTaken;
            roster.RequestStart();
            button.Activate();
            yield return 1.5f;
            roster.RequestStart();
            button.Activate();
            yield return 2f;
            Check("both start paths: exactly one match started", _countdowns - countdowns == 1 && _builds - builds == 1, $"countdowns {_countdowns - countdowns}, builds {_builds - builds}");
            Check("both start paths: the desk's start was refused and the desk reopened", Flow.StartsRequested - requested == 1 && Flow.StartsTaken == taken && !roster.RunInProgress,
                $"requested {Flow.StartsRequested - requested}, taken {Flow.StartsTaken - taken}, run in progress {roster.RunInProgress}");
            Check("next match: back to the starting pistol only, full magazine (match 1 ended with two guns)", Slots(MyGuns) == "Pistol / empty" && MyGuns.AmmoInMagazine == MyGuns.ActiveWeapon.MagazineSize, Slots(MyGuns));
        }

        /// <summary>The real rematch path: a result, back to reception, ready, the desk starts the next match.</summary>
        static IEnumerator DeskRematch()
        {
            yield return new Until(() => Match.Phase == MatchPhase.Running, 15f, "the second match to run");
            yield return CarryUpAndWin();
            yield return BackToLobby();
            // Leave this match with a second gun and hurt, so the next start has something to reset.
            var health = Me.GetComponent<Health>();
            MyGuns.Equip(AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Profiles/Weapons/AR15.asset"), 1);
            health.ApplyHit(30f, Me.Chest, Vector3.forward, -1, Vector3.zero, Vector3.zero, 0f);
            yield return 0.3f;
            Note($"before the rematch: {Slots(MyGuns)}, health {health.Current:0}");
            ReceptionDesk desk = Object.FindAnyObjectByType<ReceptionDesk>();
            LobbyRoster roster = LobbyRoster.Instance;
            desk.OnPress();
            yield return new Until(() => roster.ReadyCount == 1, 5f, "ready for the rematch");
            int countdowns = _countdowns, taken = Flow.StartsTaken;
            desk.OnPress();
            desk.OnHold(roster.Rules.StartHoldSeconds + 0.05f);
            yield return new Until(() => Match.Phase == MatchPhase.Countdown, 5f, "the rematch countdown");
            yield return 1f;
            Check("desk rematch: the desk started the next match once, from a result", _countdowns - countdowns == 1 && Flow.StartsTaken - taken == 1, $"countdowns {_countdowns - countdowns}");
            Check("desk rematch: back in this firm's tower reception with the starting pistol and full health", Match.Arena.PlaceOf(Me.Position, 0f) == Me.Firm && Slots(MyGuns) == "Pistol / empty" && health.Current >= health.Max,
                $"{V(Me.Position)}, {Slots(MyGuns)}, health {health.Current:0}");
        }
        // ---- Host and client (two copies on one computer, local connection) --------------------

        static IEnumerator HostScript()
        {
            Note("ROLE host: hosting with its own player; a second copy of the game joins as the client.");
            yield return StartGame(true);
            yield return new Until(() => FirmMember.All.Count == 2, 180f, "the client's player");
            FirmMember them = Other();
            if (them == null)
                yield break;

            DateTime t0 = SharedStart();
            Note("both players in; steps start at " + t0.ToString("HH:mm:ss") + " UTC");
            Check("HOST: the client arrived in the lobby", LobbyRoom.Instance.Contains(them.Position), V(them.Position));

            yield return t0.AddSeconds(2);
            Object.FindAnyObjectByType<ReceptionDesk>().OnPress();
            yield return t0.AddSeconds(5);
            yield return HostStartsFromDesk(them);

            yield return new Until(() => Match.Phase == MatchPhase.Running, 15f, "the match to run");
            Pickup rifle = NearestLoose("Item_AR15", FloorCentre(them.Firm, 2));
            yield return t0.AddSeconds(16);
            if (rifle != null)
                Teleport(them, StandBeside(rifle.Body.worldCenterOfMass, 1f));
            yield return t0.AddSeconds(21);
            var theirGuns = them.GetComponent<WeaponHolder>();
            var theirHands = them.GetComponent<ItemCarrier>();
            Check("HOST: the client's AR-15 pickup went into its weapon slot", Gun(theirGuns, 1) == "AR15" && theirHands.Held == null && (rifle == null || !rifle.IsSpawned), Slots(theirGuns));

            yield return HostWatchesCarriedChair(them, theirHands, t0);
            yield return new Until(() => FirmMember.All.Count == 1, 40f, "the client to leave");
            yield return 1f;
            Check("HOST: the client left mid-match; the match runs on and the lobby forgot them", Match.Phase == MatchPhase.Running && LobbyRoster.Instance.Count == 1);
        }

        static IEnumerator HostStartsFromDesk(FirmMember them)
        {
            LobbyRoster roster = LobbyRoster.Instance;
            yield return new Until(() => roster.ReadyCount == 2, 10f, "both players ready");
            ReceptionDesk desk = Object.FindAnyObjectByType<ReceptionDesk>();
            desk.OnPress();
            desk.OnHold(roster.Rules.StartHoldSeconds + 0.05f);
            yield return new Until(() => Match.Phase == MatchPhase.Countdown, 5f, "the countdown");
            yield return 1f;
            Check("HOST: the desk started the match once", _countdowns == 1 && Flow.StartsTaken == 1);
            Check("HOST: each player is in their own firm's reception", Me.Firm != them.Firm && Match.Arena.PlaceOf(Me.Position, 0f) == Me.Firm && Match.Arena.PlaceOf(them.Position, 0f) == them.Firm,
                $"me firm {Me.Firm} at {V(Me.Position)}; client firm {them.Firm} at {V(them.Position)}");
        }

        static IEnumerator HostWatchesCarriedChair(FirmMember them, ItemCarrier theirHands, DateTime t0)
        {
            Pickup chair = NearestLoose("Item_Chair", FloorCentre(them.Firm, 1));
            yield return t0.AddSeconds(22);
            if (chair != null)
                Teleport(them, StandBeside(chair.Body.worldCenterOfMass, 1f));
            yield return t0.AddSeconds(27);
            // The client takes the chair nearest to it, which may be a neighbour of the one it was put beside.
            chair = theirHands.Held;
            Check("HOST: the client holds a chair", chair != null && chair.Definition.name == "Item_Chair", chair != null ? chair.Definition.name : "nothing");
            Vector3 before = chair != null ? chair.transform.position : Vector3.zero;
            yield return t0.AddSeconds(32);
            float moved = chair != null ? (chair.transform.position - before).magnitude : 0f;
            float fromCarrier = chair != null ? (chair.transform.position - them.Position).magnitude : 99f;
            Check("HOST: the chair the client carries moved with it on the host", moved > 1f && fromCarrier < 2.5f, $"moved {moved:0.0} m, {fromCarrier:0.0} m from the client");
            yield return t0.AddSeconds(35);
            Check("HOST: the client's G dropped it", chair != null && chair.State == PickupState.InWorld && theirHands.Held == null);
        }

        static IEnumerator ClientScript()
        {
            Note("ROLE client: joined a host in another copy of the game. Everything here is what the client saw.");
            yield return StartGame(false);
            yield return new Until(() => FirmMember.All.Count == 2, 60f, "both players");
            DateTime t0 = SharedStart();
            Note("both players in; steps start at " + t0.ToString("HH:mm:ss") + " UTC");
            Check("CLIENT: this copy is not the host", !InstanceFinder.IsServerStarted);
            Check("CLIENT: arrived in the lobby", LobbyRoom.Instance.Contains(Me.Position), V(Me.Position));

            yield return t0.AddSeconds(2);
            Object.FindAnyObjectByType<ReceptionDesk>().OnPress();
            yield return t0.AddSeconds(10);
            Check("CLIENT: the run start reached this machine", Match.Phase == MatchPhase.Countdown || Match.Phase == MatchPhase.Running, Match.Phase.ToString());
            Check("CLIENT: put in its own firm's reception by the host", Me.Firm == 1 && Match.Arena.PlaceOf(Me.Position, 0f) == 1, $"firm {Me.Firm} at {V(Me.Position)}");

            yield return t0.AddSeconds(18);
            Pickup rifle = NearestLoose("Item_AR15", Me.Position);
            if (rifle != null)
                MyHands.LocalTake(rifle);
            yield return t0.AddSeconds(21);
            Check("CLIENT: the AR-15 it took is in its own weapon slot, not its hands", Gun(MyGuns, 1) == "AR15" && MyHands.Held == null, Slots(MyGuns));
            Check("CLIENT: the host's player shows its own guns unchanged", Gun(Other().GetComponent<WeaponHolder>(), 0) == "Pistol");

            yield return ClientCarriesChair(t0);
            InstanceFinder.ClientManager.StopConnection();
            yield return 1f;
        }

        static IEnumerator ClientCarriesChair(DateTime t0)
        {
            yield return t0.AddSeconds(24);
            Pickup chair = NearestLoose("Item_Chair", Me.Position);
            if (chair != null)
                MyHands.LocalTake(chair);
            yield return t0.AddSeconds(27);
            Check("CLIENT: holds the chair", chair != null && MyHands.Held == chair);

            // Walk toward the middle of the floor with the chair, on the check's keyboard, then drop it with G.
            SetCaptured(true);
            Vector3 local = Match.Arena.ToFloorSpace(Me.Position, Me.Firm, 1);
            LookAt(Match.Arena.FloorPoint(Me.Firm, 1, new Vector3(0f, Me.Motor.EyeHeight, local.z)));
            yield return t0.AddSeconds(28);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
            yield return 0.8f;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return t0.AddSeconds(33);
            yield return Press(Key.G);
            yield return t0.AddSeconds(35);
            Check("CLIENT: G dropped the chair", MyHands.Held == null);
            SetCaptured(false);
        }
    }
}
