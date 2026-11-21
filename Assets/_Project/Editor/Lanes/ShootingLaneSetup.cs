using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CloseTheDeal.Combat;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Glass;
using CloseTheDeal.Net;
using CloseTheDeal.Player;
using CloseTheDeal.Props;
using CloseTheDeal.Tower;
using CloseTheDeal.UI;
using CloseTheDeal.Editor.UI;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>
    /// The shooting and breakable-glass lane's one editor file. Three menu items:
    /// "Set Up Shooting" writes the lane's assets, adds the gun and health components to
    /// Player.prefab and builds the test scene; "Add Shooting To Open Scene" gives whatever
    /// scene is open the glass registry and a starting loadout; "Run Shooting Self-Check"
    /// plays the test scene and checks the lane's rules, writing a log and pictures.
    /// Nothing here runs on its own (AGENTS.md §4). Assets a person has edited are left alone.
    /// </summary>
    public static class ShootingLaneSetup
    {
        // ---- Paths ---------------------------------------------------------------------------

        const string ScenePath = "Assets/_Project/Scenes/Lanes/Lane_Shooting.unity";
        const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        const string DoorPrefabPath = "Assets/_Project/Prefabs/Props/LockedDoor.prefab";
        const string ChairPrefabPath = "Assets/_Project/Prefabs/Props/Chair.prefab";
        const string WeaponProfileFolder = "Assets/_Project/Profiles/Weapons";
        const string GlassProfileFolder = "Assets/_Project/Profiles/Glass";
        const string WeaponPrefabFolder = "Assets/_Project/Prefabs/Weapons";
        const string GlassPrefabFolder = "Assets/_Project/Prefabs/Glass";
        const string PistolPath = WeaponProfileFolder + "/Pistol.asset";
        const string RiflePath = WeaponProfileFolder + "/AR15.asset";
        const string CatalogPath = WeaponProfileFolder + "/WeaponCatalog.asset";
        const string GlassProfilePath = GlassProfileFolder + "/DefaultGlass.asset";
        const string CombatSoundsPath = WeaponProfileFolder + "/CombatSounds.asset";
        const string WeaponAudioFolder = "Assets/_Project/Audio/Weapons";
        const string MixerPath = "Assets/_Project/Audio/UI/GameMixer.mixer";
        const string SfxGroup = "Master/SFX";
        const string TestBlastPath = "Assets/_Project/Profiles/TestBlast.asset";
        const string KitGlassPath = "Assets/_Project/Art/Kit/Materials/Glass_Clear.mat";
        const string GreyboxGlassPath = "Assets/_Project/Floors/GreyboxGlass.mat";
        const string LaneGlassPath = GlassPrefabFolder + "/LaneGlass.mat";

        const int PlayerLayer = GreyboxSceneSetup.PlayerLayer;
        const int IgnoreRaycastLayer = 2;

        // ---- Combat HUD ------------------------------------------------------------------------

        const string CombatHudName = "CombatHud";
        const string PlatePrefabPath = "Assets/_Project/UI/Kit/Plate.prefab";
        const string ChevronSpritePath = "Assets/_Project/UI/Kit/Sprites/Kit_Chevron.png";
        const string WhiteSpritePath = "Assets/_Project/UI/Kit/Sprites/Kit_White.png";

        /// <summary>Replaces the open scene's combat HUD with a fresh one from the current builder, after asking. For a scene saved with an older HUD.</summary>
        [MenuItem("Close the Deal/Lanes/Rebuild Combat HUD In Open Scene")]
        static void RebuildCombatHud()
        {
            GameMenu menu = Object.FindAnyObjectByType<GameMenu>();
            Transform old = menu != null ? menu.transform.Find("Hud/" + CombatHudName) : null;
            if (old != null)
            {
                bool ask = !Application.isBatchMode;
                if (ask && !EditorUtility.DisplayDialog("Rebuild the combat HUD?", "This replaces the combat HUD in the open scene, dropping any changes made to it there.", "Rebuild", "Keep it"))
                    return;
                Object.DestroyImmediate(old.gameObject);
            }

            EnsureCombatHud();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// Adds the combat HUD under the game menu's Hud, built from the UI kit: kit plates, the
        /// tokens' type roles and colours, kit sprites. Only adds: a scene that already has one keeps it.
        /// Needs the game menu (Set Up Scene) and the kit (Close the Deal > Lanes > Set Up UI).
        /// </summary>
        static void EnsureCombatHud()
        {
            GameMenu menu = Object.FindAnyObjectByType<GameMenu>();
            Transform hud = menu != null ? menu.transform.Find("Hud") : null;
            var tokens = AssetDatabase.LoadAssetAtPath<UiTokens>(UiKitAssets.TokensPath);
            var plate = AssetDatabase.LoadAssetAtPath<GameObject>(PlatePrefabPath);
            if (hud == null || tokens == null || plate == null)
            {
                Debug.LogWarning("[Shooting Lane] No combat HUD added: the scene needs the game menu (Set Up Scene) and the UI kit (Close the Deal > Lanes > Set Up UI).");
                return;
            }

            if (hud.Find(CombatHudName) != null)
                return;

            var root = (RectTransform)new GameObject(CombatHudName, typeof(RectTransform)).transform;
            root.SetParent(hud, false);
            UiKitAssets.Stretch(root);
            var component = root.gameObject.AddComponent<CombatHud>();
            var so = new SerializedObject(component);
            so.FindProperty("_tokens").objectReferenceValue = tokens;

            BuildHitMarker(root, tokens, so);
            BuildDamageMarks(root, so);
            BuildWeaponColumn(root, tokens, so);
            BuildDownCard(root, tokens, plate, so);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Shooting Lane] Added the combat HUD under the game menu's Hud.");
        }

        static RectTransform Anchored(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static Image Solid(RectTransform rect, Color colour, Sprite sprite = null)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text HudText(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size,
            UiTypeRole role, Color colour, TextAlignmentOptions align, string content)
        {
            return UiKitAssets.Text(Anchored(parent, name, anchor, pivot, position, size), content, role, colour, align);
        }

        static RectTransform PlateAt(Transform parent, GameObject plate, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(plate, parent);
            instance.name = name;
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Four diagonal ticks around the centre, each a navy outline under a core.</summary>
        static void BuildHitMarker(RectTransform root, UiTokens tokens, SerializedObject so)
        {
            RectTransform centre = Anchored(root, "HitMarker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var outlines = new Object[4];
            var cores = new Object[4];
            for (int i = 0; i < 4; i++)
            {
                RectTransform outline = Anchored(centre, "TickOutline" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 4f));
                Solid(outline, tokens.Black);
                outlines[i] = outline;
            }

            for (int i = 0; i < 4; i++)
            {
                RectTransform core = Anchored(centre, "Tick" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 2f));
                Solid(core, tokens.Paper);
                cores[i] = core;
            }

            SetObjects(so, "_tickOutlines", outlines);
            SetObjects(so, "_ticks", cores);
        }

        /// <summary>Chevrons around the centre that point at whoever hit you.</summary>
        static void BuildDamageMarks(RectTransform root, SerializedObject so)
        {
            RectTransform centre = Anchored(root, "DamageMarks", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var chevron = AssetDatabase.LoadAssetAtPath<Sprite>(ChevronSpritePath);
            var marks = new Object[4];
            for (int i = 0; i < 4; i++)
            {
                RectTransform mark = Anchored(centre, "Mark" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 22f));
                marks[i] = Solid(mark, Color.white, chevron);
            }

            SetObjects(so, "_damageMarks", marks);
        }

        /// <summary>One weapon card: live magazine, action feedback and two loadout tabs. Health appears above it only while hurt.</summary>
        static void BuildWeaponColumn(RectTransform root, UiTokens tokens, SerializedObject so)
        {
            var corner = new Vector2(1f, 0f);
            var round = AssetDatabase.LoadAssetAtPath<Sprite>(UiKitArt.Folder + "/Kit_Round16.png");
            var tab = AssetDatabase.LoadAssetAtPath<Sprite>(UiKitArt.Folder + "/Kit_Round8.png");
            const float width = 290f;
            RectTransform weapon = Anchored(root, "WeaponPlate", corner, corner, new Vector2(-24f, 24f), new Vector2(width, 56f));
            Color frost = tokens.Paper;
            frost.a = 0.78f;
            UiKitAssets.Img(weapon, round, frost);

            TMP_Text name = UiKitAssets.Label(weapon, "WeaponName", 16, 3, 137, 50, "PISTOL", tokens.Item, tokens.Navy);
            name.fontSize = 21f;
            TMP_Text ammo = UiKitAssets.Label(weapon, "Ammo", 153, 0, 66, 54, "12", tokens.Heading, tokens.Navy, TextAlignmentOptions.MidlineLeft);
            ammo.fontSize = 42f;
            ammo.characterSpacing = 0f;
            Unclipped(ammo);
            TMP_Text magazine = UiKitAssets.Label(weapon, "Magazine", 219, 14, 65, 36, "/ 12", tokens.Label, tokens.Ink2);
            magazine.fontSize = 22f;

            Image statePlate = UiKitAssets.Box(weapon, "StatusPlate", 124, -43, 166, 36, tokens.HoverTint, tab);
            TMP_Text state = UiKitAssets.Label(weapon, "State", 129, -43, 156, 36, "READY", tokens.Label, tokens.Navy);
            state.alignment = TextAlignmentOptions.Center;
            state.enableAutoSizing = true;
            state.fontSizeMin = 15;
            state.fontSizeMax = 18;
            state.fontSize = 18f;
            RectTransform track = UiKitAssets.At(weapon, "ProgressTrack", 14, 50, width - 28, 2);
            Solid(track, Color.clear);
            RectTransform fill = Anchored(track, "Fill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, track.sizeDelta);
            Solid(fill, tokens.Cobalt);
            fill.localScale = new Vector3(0f, 1f, 1f);

            var slotTexts = new Object[WeaponHolder.SlotCount];
            var slotBars = new Object[WeaponHolder.SlotCount];
            float tabWidth = 146f;
            for (int i = 0; i < WeaponHolder.SlotCount; i++)
            {
                RectTransform row = UiKitAssets.At(weapon, "Slot" + (i + 1), width - tabWidth, -77, tabWidth, 27);
                slotBars[i] = UiKitAssets.Img(row, tab, tokens.Cobalt);
                slotTexts[i] = UiKitAssets.Label(row, "Text", 8, 0, tabWidth - 16, 27, (i + 1) + "  —", tokens.Label, tokens.Navy,
                    TextAlignmentOptions.Center);
                ((TMP_Text)slotTexts[i]).fontSize = 17f;
            }

            RectTransform health = Anchored(root, "HealthPlate", Vector2.zero, Vector2.zero, new Vector2(24f, 24f), new Vector2(244f, 60f));
            UiKitAssets.Img(health, round, UiTokens.WithAlpha(tokens.Paper, 0.92f));
            var group = health.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            UiKitAssets.Label(health, "Label", 16, 6, 160, 28, "HEALTH", tokens.Label, tokens.Navy).fontSize = 21f;
            TMP_Text number = UiKitAssets.Label(health, "Number", 170, 4, 58, 30, "100", tokens.Item, tokens.Navy, TextAlignmentOptions.MidlineRight);
            RectTransform healthTrack = UiKitAssets.At(health, "Track", 16, 39, 212, 8);
            UiKitAssets.Img(healthTrack, tab, tokens.Hair);
            RectTransform healthFill = Anchored(healthTrack, "Fill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, healthTrack.sizeDelta);
            Image healthFillImage = UiKitAssets.Img(healthFill, tab, tokens.Cobalt);

            so.FindProperty("_weaponPlate").objectReferenceValue = weapon;
            so.FindProperty("_ammoText").objectReferenceValue = ammo;
            so.FindProperty("_magazineText").objectReferenceValue = magazine;
            so.FindProperty("_weaponName").objectReferenceValue = name;
            so.FindProperty("_weaponState").objectReferenceValue = state;
            so.FindProperty("_statePlate").objectReferenceValue = statePlate;
            so.FindProperty("_progressFill").objectReferenceValue = fill;
            SetObjects(so, "_slotTexts", slotTexts);
            SetObjects(so, "_slotBars", slotBars);
            so.FindProperty("_healthGroup").objectReferenceValue = group;
            so.FindProperty("_healthFill").objectReferenceValue = healthFill;
            so.FindProperty("_healthFillImage").objectReferenceValue = healthFillImage;
            so.FindProperty("_healthText").objectReferenceValue = number;
        }

        /// <summary>Above the crosshair while knocked out: what happened, and when you are back.</summary>
        static void BuildDownCard(RectTransform root, UiTokens tokens, GameObject plate, SerializedObject so)
        {
            var mid = new Vector2(0.5f, 0.5f);
            RectTransform card = PlateAt(root, plate, "DownCard", mid, mid, new Vector2(0f, 150f), new Vector2(460f, 150f));
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            TMP_Text title = HudText(card, "Title", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(420f, 28f), tokens.Section, tokens.Navy, TextAlignmentOptions.Center, "YOU HAVE BEEN LET GO");
            TMP_Text line = HudText(card, "Line", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(420f, 22f), tokens.Caption, tokens.Ink3, TextAlignmentOptions.Center, "BACK ON THIS FLOOR IN");
            line.fontSize = Mathf.Max(line.fontSize, MinHudCaption);
            so.FindProperty("_downTitle").objectReferenceValue = title;
            so.FindProperty("_downLine").objectReferenceValue = line;
            TMP_Text count = HudText(card, "Count", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(200f, 74f), tokens.Heading, tokens.Navy, TextAlignmentOptions.Center, "5");
            count.fontSize = 56f;
            Unclipped(count);
            so.FindProperty("_downGroup").objectReferenceValue = group;
            so.FindProperty("_downCount").objectReferenceValue = count;
        }

        /// <summary>The kit's text clips with an ellipsis; a big number would vanish whole if its line ran a pixel over its box.</summary>
        static void Unclipped(TMP_Text text)
        {
            text.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>Smallest caption on the HUD, in pixels at 1080p: smaller is unreadable at 1280 x 720.</summary>
        const float MinHudCaption = 15f;

        static void SetObjects(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ---- Test scene measurements (metres) -------------------------------------------------

        const float SlabTop = 3f;
        const float OfficeWidth = 16f;
        const float OfficeDepth = 10f;
        const float WindowZ = 6f;        // each window wall stands this far from the middle of the street
        const float SillHeight = 0.6f;   // same as the tower's façade sill
        const float CeilingHeight = 3f;
        const float BayWidth = 4f;       // same as the tower's pillar spacing
        const float PaneThickness = 0.05f;
        const float SprintSpeed = 9f;    // DefaultMovement.asset's sprint speed
        const float TopSpeed = 24f;      // the movement lane's Max Speed: the fastest a player can go (air strafing)
        const float RunnerHealth = 100000f;

        static readonly Vector3 SpawnA = new(-2f, SlabTop + 0.1f, -9f);
        static readonly Vector3 SpawnB = new(2f, SlabTop + 0.1f, -9f);

        // =======================================================================================
        // Menu: set up
        // =======================================================================================

        [MenuItem("Close the Deal/Lanes/Set Up Shooting")]
        public static void SetUp()
        {
            LaneAssets assets = EnsureAssets();
            PatchPlayerPrefab(assets);
            PatchDoorPrefab();
            BuildScene(assets);
            AssetDatabase.SaveAssets();
            Debug.Log("[Shooting Lane] Set up: assets, Player.prefab and " + ScenePath + ".");
        }

        /// <summary>Gives the open scene what guns and glass need to work in it. Adds only what is missing; the scene is left unsaved for whoever ran it to review.</summary>
        [MenuItem("Close the Deal/Lanes/Add Shooting To Open Scene")]
        public static void AddToOpenScene()
        {
            EnsureAssets();
            EnsureSceneObjects(ReloadAssets());
            EnsureCombatHud();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Shooting Lane] The open scene now has a Glass registry and a Starting Loadout. Review and save it.");
        }

        sealed class LaneAssets
        {
            public WeaponDefinition Pistol;
            public WeaponDefinition Rifle;
            public WeaponCatalog Catalog;
            public GlassProfile Glass;
            public Material PaneMaterial;
            public Material DummyMaterial;
            public CombatSoundSet Sounds;
        }

        // ---- Assets --------------------------------------------------------------------------

        static LaneAssets EnsureAssets()
        {
            EnsureFolder(WeaponProfileFolder);
            EnsureFolder(GlassProfileFolder);
            EnsureFolder(WeaponPrefabFolder);
            EnsureFolder(GlassPrefabFolder);

            Material metal = EnsureMaterial(WeaponPrefabFolder + "/Gunmetal.mat", "Universal Render Pipeline/Lit", new Color(0.13f, 0.14f, 0.16f, 1f), false);
            Material flash = EnsureMaterial(WeaponPrefabFolder + "/MuzzleFlash.mat", "Universal Render Pipeline/Unlit", new Color(1f, 0.85f, 0.35f, 1f), false);
            Material tracerMaterial = EnsureMaterial(WeaponPrefabFolder + "/Tracer.mat", "Universal Render Pipeline/Unlit", new Color(1f, 0.92f, 0.6f, 1f), false);
            Material shardMaterial = EnsureMaterial(GlassPrefabFolder + "/Shard.mat", "Universal Render Pipeline/Particles/Unlit", new Color(0.78f, 0.92f, 1f, 0.9f), true);

            var assets = new LaneAssets
            {
                DummyMaterial = EnsureMaterial(WeaponPrefabFolder + "/TargetDummy.mat", "Universal Render Pipeline/Lit", new Color(0.95f, 0.5f, 0.1f, 1f), false),
                PaneMaterial = AssetDatabase.LoadAssetAtPath<Material>(KitGlassPath)
                    ?? EnsureMaterial(LaneGlassPath, "Universal Render Pipeline/Lit", new Color(0.65f, 0.85f, 0.95f, 0.25f), true)
            };

            LineRenderer tracer = EnsureTracerPrefab(tracerMaterial);
            GameObject pistolView = EnsureGunView(WeaponPrefabFolder + "/Pistol_View.prefab", false, metal, flash);
            GameObject rifleView = EnsureGunView(WeaponPrefabFolder + "/AR15_View.prefab", true, metal, flash);
            ParticleSystem shards = EnsureShardPrefab(shardMaterial);
            ParticleSystem impact = EnsureImpactPrefab(flash);

            assets.Pistol = EnsureWeapon(PistolPath, w =>
            {
                w.DisplayName = "Pistol";
                w.Automatic = false;
                w.RoundsPerMinute = 400f;
                w.Damage = 34f;
                w.Range = 80f;
                w.FalloffStart = 25f;
                w.FalloffEnd = 50f;
                w.FarDamageFraction = 0.75f;
                w.BaseSpread = 0f;
                w.BloomPerShot = 0.12f;
                w.MaxBloom = 0.6f;
                w.BloomRecovery = 8f;
                w.MovingSpreadMultiplier = 1f;
                w.AirborneSpreadMultiplier = 1.15f;
                w.RecoilPattern = new[] { new Vector2(0f, 1.1f), new Vector2(0.2f, 0.9f), new Vector2(-0.2f, 0.9f) };
                w.PatternLoopFrom = 1;
                w.PatternResetSeconds = 0.3f;
                w.KickRecovery = 18f;
                w.MagazineSize = 12;
                w.ReloadSeconds = 1.1f;
                w.EquipSeconds = 0.2f;
                w.HitShove = 0f;
                w.HitControlLoss = 0f;
                w.KillShove = 13f;
                w.ShoveLift = 0.55f;
                w.ViewPrefab = pistolView;
                w.ViewOffset = new Vector3(0.2f, -0.17f, 0.42f);
                w.TracerPrefab = tracer;
                w.TracerSeconds = 0.08f;
                PistolFeel(w);
            });

            assets.Rifle = EnsureWeapon(RiflePath, w =>
            {
                w.DisplayName = "AR-15";
                w.Automatic = true;
                w.RoundsPerMinute = 600f;
                w.Damage = 20f;
                w.Range = 90f;
                w.FalloffStart = 18f;
                w.FalloffEnd = 45f;
                w.FarDamageFraction = 0.65f;
                w.BaseSpread = 0f;
                w.BloomPerShot = 0.06f;
                w.MaxBloom = 0.7f;
                w.BloomRecovery = 6f;
                w.MovingSpreadMultiplier = 1f;
                w.AirborneSpreadMultiplier = 1.25f;
                w.RecoilPattern = RiflePattern();
                w.PatternLoopFrom = 8;
                w.PatternResetSeconds = 0.35f;
                w.KickRecovery = 12f;
                w.MagazineSize = 30;
                w.ReloadSeconds = 1.7f;
                w.EquipSeconds = 0.3f;
                w.HitShove = 0f;
                w.HitControlLoss = 0f;
                w.KillShove = 13f;
                w.ShoveLift = 0.55f;
                w.ViewPrefab = rifleView;
                w.ViewOffset = new Vector3(0.18f, -0.2f, 0.32f);
                w.TracerPrefab = tracer;
                w.TracerSeconds = 0.07f;
                RifleFeel(w);
            });

            FillIfEmpty(assets.Pistol, impact);
            FillIfEmpty(assets.Rifle, impact);

            assets.Catalog = EnsureCatalog(assets.Pistol, assets.Rifle);
            assets.Glass = EnsureGlassProfile(shards, assets.PaneMaterial);
            assets.Sounds = EnsureCombatSounds(assets.Pistol, assets.Rifle);
            // Saved now: opening the test scene next unloads assets nothing references, unsaved changes with them.
            AssetDatabase.SaveAssets();
            return assets;
        }

        /// <summary>The pistol's starting feel: a big snap that is back before the next shot is allowed, a gun that flips hard and settles a little slower.</summary>
        static void PistolFeel(WeaponDefinition w)
        {
            w.SnapUp = 2.2f;
            w.SnapSide = 0.5f;
            w.SnapRecoverySeconds = 0.13f;
            w.ShakeDegrees = 0.25f;
            w.ShakeSeconds = 0.15f;
            w.FovPunch = 1.5f;
            w.ViewPushBack = 0.06f;
            w.ViewFlip = 10f;
            w.ViewRoll = 3f;
            w.ViewTwist = 2.5f;
            w.ViewRecoverySeconds = 0.19f;
            w.FlashSeconds = 0.04f;
        }

        /// <summary>The AR-15's starting feel: a small snap per shot inside the shot interval, so the pattern stays readable under it.</summary>
        static void RifleFeel(WeaponDefinition w)
        {
            w.SnapUp = 0.6f;
            w.SnapSide = 0.25f;
            w.SnapRecoverySeconds = 0.09f;
            w.ShakeDegrees = 0.12f;
            w.ShakeSeconds = 0.12f;
            w.FovPunch = 0.6f;
            w.ViewPushBack = 0.025f;
            w.ViewFlip = 3f;
            w.ViewRoll = 1.2f;
            w.ViewTwist = 1.5f;
            w.ViewRecoverySeconds = 0.12f;
            w.FlashSeconds = 0.03f;
        }

        /// <summary>Gives a gun the impact effect when it has none; a designer's choice is never replaced (AGENTS.md §4).</summary>
        static void FillIfEmpty(WeaponDefinition weapon, ParticleSystem impact)
        {
            if (weapon == null || weapon.ImpactEffect != null || impact == null)
                return;

            weapon.ImpactEffect = impact;
            EditorUtility.SetDirty(weapon);
        }

        /// <summary>The AR-15's starting pattern: eight shots climbing almost straight, then a sweep left and a sweep right that a long burst keeps repeating.</summary>
        static Vector2[] RiflePattern()
        {
            var pattern = new Vector2[24];
            for (int i = 0; i < pattern.Length; i++)
                pattern[i] = i < 8 ? new Vector2(0.06f, 0.5f) : i < 16 ? new Vector2(-0.28f, 0.2f) : new Vector2(0.28f, 0.12f);
            return pattern;
        }

        /// <summary>The lane's assets loaded fresh from disk. EnsureAssets has made sure they exist.</summary>
        static LaneAssets ReloadAssets()
        {
            return new LaneAssets
            {
                Pistol = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(PistolPath),
                Rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RiflePath),
                Catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath),
                Glass = AssetDatabase.LoadAssetAtPath<GlassProfile>(GlassProfilePath),
                Sounds = AssetDatabase.LoadAssetAtPath<CombatSoundSet>(CombatSoundsPath),
                DummyMaterial = AssetDatabase.LoadAssetAtPath<Material>(WeaponPrefabFolder + "/TargetDummy.mat"),
                PaneMaterial = AssetDatabase.LoadAssetAtPath<Material>(KitGlassPath) ?? AssetDatabase.LoadAssetAtPath<Material>(LaneGlassPath)
            };
        }

        /// <summary>
        /// The combat sound set, made when missing, and lane 9's weapon clips filled into it and into
        /// each gun wherever a slot is still empty (a designer's choice is never replaced). The set
        /// plays through the game mixer's SFX group once it exists.
        /// </summary>
        static CombatSoundSet EnsureCombatSounds(WeaponDefinition pistol, WeaponDefinition rifle)
        {
            var set = AssetDatabase.LoadAssetAtPath<CombatSoundSet>(CombatSoundsPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CombatSoundSet>();
                AssetDatabase.CreateAsset(set, CombatSoundsPath);
                Debug.Log("[Shooting Lane] Created " + CombatSoundsPath);
            }

            var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(MixerPath);
            UnityEngine.Audio.AudioMixerGroup[] groups = mixer != null ? mixer.FindMatchingGroups(SfxGroup) : null;
            if (set.Output == null && groups != null && groups.Length > 0)
                set.Output = groups[0];

            set.ImpactWorld = set.ImpactWorld != null ? set.ImpactWorld : Clip("ImpactWorld");
            set.ImpactBody = set.ImpactBody != null ? set.ImpactBody : Clip("ImpactBody");
            set.GlassShatter = set.GlassShatter != null ? set.GlassShatter : Clip("GlassShatter");
            set.Hurt = set.Hurt != null ? set.Hurt : Clip("Hurt");
            set.Death = set.Death != null ? set.Death : Clip("Death");
            set.Respawn = set.Respawn != null ? set.Respawn : Clip("Respawn");
            EditorUtility.SetDirty(set);

            FillGunSounds(pistol, "PistolShot", "ReloadSlide");
            FillGunSounds(rifle, "AR15Shot", "ReloadCharge");
            if (set.Output == null)
                Debug.Log($"[Shooting Lane] Combat sounds have no mixer bus yet: no {SfxGroup} group at {MixerPath}.");
            return set;
        }

        static void FillGunSounds(WeaponDefinition gun, string shot, string reloadEnd)
        {
            if (gun == null)
                return;

            gun.ShotSound = gun.ShotSound != null ? gun.ShotSound : Clip(shot);
            gun.DryFireSound = gun.DryFireSound != null ? gun.DryFireSound : Clip("DryFire");
            gun.DrawSound = gun.DrawSound != null ? gun.DrawSound : Clip("Swap");
            gun.MagazineOutSound = gun.MagazineOutSound != null ? gun.MagazineOutSound : Clip("ReloadMagOut");
            gun.MagazineInSound = gun.MagazineInSound != null ? gun.MagazineInSound : Clip("ReloadMagIn");
            gun.ReloadEndSound = gun.ReloadEndSound != null ? gun.ReloadEndSound : Clip(reloadEnd);
            EditorUtility.SetDirty(gun);
        }

        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{WeaponAudioFolder}/Weapon_{name}.wav");

        static WeaponDefinition EnsureWeapon(string path, Action<WeaponDefinition> defaults)
        {
            var existing = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (existing != null)
                return existing; // A designer's numbers; never rewritten.

            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            defaults(weapon);
            AssetDatabase.CreateAsset(weapon, path);
            Debug.Log("[Shooting Lane] Created " + path);
            return weapon;
        }

        static WeaponCatalog EnsureCatalog(params WeaponDefinition[] weapons)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
                catalog.Weapons = weapons;
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                Debug.Log("[Shooting Lane] Created " + CatalogPath);
                return catalog;
            }

            // An existing catalog is a person's list: say what is missing, never change it.
            foreach (WeaponDefinition weapon in weapons)
            {
                if (catalog.IndexOf(weapon) == WeaponCatalog.None)
                    Debug.LogWarning($"[Shooting Lane] {weapon.name} is not in {CatalogPath}; add it at the end of the list or players can't be given it.");
            }

            return catalog;
        }

        static GlassProfile EnsureGlassProfile(ParticleSystem shards, Material paneMaterial)
        {
            var known = new List<Material>();
            foreach (Material glass in new[] { paneMaterial, AssetDatabase.LoadAssetAtPath<Material>(GreyboxGlassPath), AssetDatabase.LoadAssetAtPath<Material>(KitGlassPath) })
            {
                if (glass != null && !known.Contains(glass))
                    known.Add(glass);
            }

            var profile = AssetDatabase.LoadAssetAtPath<GlassProfile>(GlassProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<GlassProfile>();
                profile.ShardEffect = shards;
                profile.GlassMaterials = known.ToArray();
                AssetDatabase.CreateAsset(profile, GlassProfilePath);
                Debug.Log("[Shooting Lane] Created " + GlassProfilePath);
                return profile;
            }

            // An existing profile is a person's list: say what is missing, never change it.
            foreach (Material glass in known)
            {
                if (Array.IndexOf(profile.GlassMaterials, glass) < 0)
                    Debug.LogWarning($"[Shooting Lane] {glass.name} is not under Glass Materials on {GlassProfilePath}; things drawn with it won't break unless they carry a Glass Pane.");
            }

            return profile;
        }

        static Material EnsureMaterial(string path, string shaderName, Color colour, bool transparent)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Shooting Lane] Shader '{shaderName}' not found; {path} not created.");
                return null;
            }

            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", colour);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static LineRenderer EnsureTracerPrefab(Material material)
        {
            string path = WeaponPrefabFolder + "/Tracer.prefab";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return AssetDatabase.LoadAssetAtPath<LineRenderer>(path);

            var go = new GameObject("Tracer");
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.035f;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return SavePrefab(go, path).GetComponent<LineRenderer>();
        }

        /// <summary>A greybox gun out of boxes: enough to read as a pistol or a rifle in the hand, with the Muzzle and Flash children WeaponView looks for.</summary>
        static GameObject EnsureGunView(string path, bool rifle, Material metal, Material flashMaterial)
        {
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);

            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            Vector3 muzzle;
            if (rifle)
            {
                Part(root, "Receiver", new Vector3(0f, 0.02f, 0.05f), new Vector3(0.05f, 0.08f, 0.34f), 0f, metal);
                Part(root, "Barrel", new Vector3(0f, 0.035f, 0.38f), new Vector3(0.025f, 0.025f, 0.34f), 0f, metal);
                Part(root, "Handguard", new Vector3(0f, 0.03f, 0.3f), new Vector3(0.045f, 0.05f, 0.2f), 0f, metal);
                Part(root, "Stock", new Vector3(0f, 0.01f, -0.23f), new Vector3(0.04f, 0.07f, 0.22f), 0f, metal);
                Part(root, "Magazine", new Vector3(0f, -0.08f, 0.08f), new Vector3(0.03f, 0.14f, 0.06f), -10f, metal);
                Part(root, "Grip", new Vector3(0f, -0.06f, -0.06f), new Vector3(0.03f, 0.1f, 0.04f), 15f, metal);
                muzzle = new Vector3(0f, 0.035f, 0.56f);
            }
            else
            {
                Part(root, "Slide", new Vector3(0f, 0.03f, 0.06f), new Vector3(0.035f, 0.05f, 0.19f), 0f, metal);
                Part(root, "Grip", new Vector3(0f, -0.04f, -0.01f), new Vector3(0.032f, 0.11f, 0.045f), 12f, metal);
                muzzle = new Vector3(0f, 0.03f, 0.16f);
            }

            var muzzlePoint = new GameObject("Muzzle");
            muzzlePoint.transform.SetParent(root.transform, false);
            muzzlePoint.transform.localPosition = muzzle;

            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Flash";
            Object.DestroyImmediate(flash.GetComponent<Collider>());
            flash.transform.SetParent(root.transform, false);
            // A short, narrow burst along the barrel: big enough to read in the corner of the eye, small enough not to hide the target.
            float flashSize = rifle ? 0.05f : 0.035f;
            flash.transform.localPosition = muzzle + new Vector3(0f, 0f, flashSize * 1.2f);
            flash.transform.localScale = new Vector3(flashSize, flashSize, flashSize * 2.6f);
            SetLooks(flash, flashMaterial);

            // The light is what sells a muzzle flash: it lights the hands, the gun and the walls for an instant.
            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(flash.transform, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = rifle ? 6f : 5f;
            light.intensity = rifle ? 2.5f : 3f;
            light.color = new Color(1f, 0.78f, 0.45f, 1f);
            light.shadows = LightShadows.None;

            return SavePrefab(root, path);
        }

        static void Part(GameObject root, string name, Vector3 position, Vector3 size, float tiltDegrees, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(tiltDegrees, 0f, 0f);
            part.transform.localScale = size;
            SetLooks(part, material);
        }

        static void SetLooks(GameObject go, Material material)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null)
                renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>The shard burst: flat tumbling squares that fall under gravity. Emission is off; GlassShards emits a count sized to the pane.</summary>
        static ParticleSystem EnsureShardPrefab(Material material)
        {
            string path = GlassPrefabFolder + "/GlassShards.prefab";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return AssetDatabase.LoadAssetAtPath<ParticleSystem>(path);

            var go = new GameObject("GlassShards");
            var system = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.5f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1.4f;
            main.maxParticles = 600;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.randomDirectionAmount = 0.35f;

            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            spin.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            return SavePrefab(go, path).GetComponent<ParticleSystem>();
        }

        /// <summary>Bullet impact: a quick spray of hot sparks off the surface that fall and fade. Played locally on every machine.</summary>
        static ParticleSystem EnsureImpactPrefab(Material material)
        {
            string path = WeaponPrefabFolder + "/ImpactSparks.prefab";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return AssetDatabase.LoadAssetAtPath<ParticleSystem>(path);

            var go = new GameObject("ImpactSparks");
            var system = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
            main.gravityModifier = 1.5f;
            main.maxParticles = 40;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.01f;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 4f;
            renderer.velocityScale = 0.02f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            return SavePrefab(go, path).GetComponent<ParticleSystem>();
        }

        static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            GeneratedAssetGuard.MarkGenerated(prefab);
            Debug.Log("[Shooting Lane] Wrote " + path);
            return prefab;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ---- Player prefab -------------------------------------------------------------------

        /// <summary>
        /// Adds Health, WeaponHolder and WeaponView to Player.prefab, a pivot the body falls
        /// over on, and a hand point for the gun; and points the motor's left-click test blast
        /// at a profile that does nothing. Safe to run again: it only adds what is missing.
        /// The prefab is handed back to the greybox tool afterwards, so Rebuild Player Prefab
        /// still works; run this menu again after it.
        /// </summary>
        static void PatchPlayerPrefab(LaneAssets assets)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                Debug.LogError("[Shooting Lane] No Player.prefab; run Close the Deal > Greybox > Set Up Scene first.");
                return;
            }

            // Asked before the patch: was the prefab still the greybox tool's own, untouched by a person?
            bool toolOwned = IsToolOwned(PlayerPrefabPath);

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Health health = GetOrAdd<Health>(root);
                WeaponHolder holder = GetOrAdd<WeaponHolder>(root);
                WeaponView view = GetOrAdd<WeaponView>(root);
                CombatSounds sounds = GetOrAdd<CombatSounds>(root);

                Transform lean = FindDeep(root.transform, "Lean");
                // No fall pivot on players: the character rig and its ragdoll own the body's pose
                // (they listen to Health.Died, which carries the throw); dummies fall over by themselves.
                Transform hand = EnsureHandPoint(root.transform, lean);

                // Each value only when it is empty or still at its default (AGENTS.md §4): a person's choice stays.
                var serializedHolder = new SerializedObject(holder);
                FillReference(serializedHolder, "_catalog", assets.Catalog);
                FillMaskIfEverything(serializedHolder, "_solidMask", ~((1 << PlayerLayer) | (1 << IgnoreRaycastLayer)));
                serializedHolder.ApplyModifiedPropertiesWithoutUndo();

                var serializedView = new SerializedObject(view);
                FillReference(serializedView, "_thirdPersonAnchor", hand);
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                var serializedSounds = new SerializedObject(sounds);
                FillReference(serializedSounds, "_set", assets.Sounds);
                serializedSounds.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // Only a prefab that was the tool's stays the tool's; one a person has taken over stays theirs.
            if (toolOwned)
                GeneratedAssetGuard.MarkGenerated(AssetDatabase.LoadMainAssetAtPath(PlayerPrefabPath));
            Debug.Log("[Shooting Lane] Player.prefab has Health, WeaponHolder, WeaponView and CombatSounds.");
        }

        /// <summary>A pivot at the feet between the smoothed visual and the lean pivot, so the whole body can fall flat without touching the motor's own lean.</summary>
        /// <summary>
        /// Where other players see this player's gun: an empty "Hand" point under the lean
        /// pivot, so it leans with the body. Not the rig's hand bone yet: its axes are not
        /// known, and a gun parented to it would point the wrong way until someone checks.
        /// Adds at most one empty object and never moves, renames or rebuilds anything.
        /// </summary>
        static Transform EnsureHandPoint(Transform root, Transform lean)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Hand")
                    return t;
            }

            if (lean == null)
            {
                Debug.LogWarning("[Shooting Lane] Player.prefab has no 'Lean' object; other players won't see this player's gun.");
                return null;
            }

            var hand = new GameObject("Hand") { layer = PlayerLayer };
            hand.transform.SetParent(lean, false);
            // Right side, chest height, a little ahead of the body. The lean pivot sits at the hips (0.9 m).
            hand.transform.localPosition = new Vector3(0.3f, 0.4f, 0.3f);
            return hand.transform;
        }

        static void FillReference(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null && p.objectReferenceValue == null)
                p.objectReferenceValue = value;
        }

        /// <summary>A layer mask left at "everything" (its default) gets the tool's value; any other is someone's choice.</summary>
        static void FillMaskIfEverything(SerializedObject so, string field, int value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null && p.intValue == ~0)
                p.intValue = value;
        }

        /// <summary>Lets guns wear down the locked door, if the greybox tool has made one in this checkout.</summary>
        static void PatchDoorPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath) == null)
            {
                Debug.Log("[Shooting Lane] No LockedDoor.prefab here yet; run this menu again once the greybox tool has made it, or bullets won't hurt doors.");
                return;
            }

            bool toolOwned = IsToolOwned(DoorPrefabPath);
            GameObject root = PrefabUtility.LoadPrefabContents(DoorPrefabPath);
            try
            {
                if (root.GetComponent<DoorDamage>() == null && root.GetComponent<BreakableDoor>() != null)
                {
                    root.AddComponent<DoorDamage>();
                    PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (toolOwned)
                GeneratedAssetGuard.MarkGenerated(AssetDatabase.LoadMainAssetAtPath(DoorPrefabPath));
        }

        /// <summary>
        /// Whether the greybox tool still owns a prefab, asked quietly: the guard's own message
        /// says the tool "left it alone", which reads wrong here because this menu only adds
        /// components. A prefab a person has taken over stays theirs after the patch.
        /// </summary>
        static bool IsToolOwned(string path)
        {
            LogType saved = Debug.unityLogger.filterLogType;
            Debug.unityLogger.filterLogType = LogType.Error;
            try
            {
                return GeneratedAssetGuard.MayOverwrite(path);
            }
            finally
            {
                Debug.unityLogger.filterLogType = saved;
            }
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }

            return null;
        }

        // ---- Test scene ----------------------------------------------------------------------

        /// <summary>
        /// Two offices facing each other across a street, each with a wall of window bays to
        /// the tower's measurements (0.6 m sill, a pillar every 4 m). Target dummies stand in
        /// both. The near office's panes carry a Glass Pane; the far office's are glass only by
        /// their material, the way the greybox tower's are. Rebuilt from scratch, after asking when the scene already exists.
        /// </summary>
        static void BuildScene(LaneAssets assets)
        {
            bool exists = File.Exists(ScenePath);
            if (exists && !Application.isBatchMode && !EditorUtility.DisplayDialog("Rebuild the shooting test scene?",
                    ScenePath + " already exists. Rebuilding replaces everything in it. Assets and Player.prefab are already up to date either way.", "Rebuild", "Keep it"))
            {
                Debug.Log("[Shooting Lane] Kept the existing " + ScenePath + ".");
                return;
            }

            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Shooting Lane] Kept the open scene; the test scene was not rebuilt.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // Opening a new scene unloads assets nothing references yet, which leaves the ones in hand dead; load them again.
            assets = ReloadAssets();

            var range = new GameObject("Range");
            Box(range.transform, "Street", new Vector3(0f, -0.1f, 0f), new Vector3(60f, 0.2f, 50f), null);
            BuildOffice(range.transform, "Near office", -1f, assets, true);
            BuildOffice(range.transform, "Far office", 1f, assets, false);

            // A glass-room wall and a chair to throw through it.
            GameObject partition = Box(range.transform, "Glass room wall", new Vector3(3f, SlabTop + 1.2f, -11.5f), new Vector3(PaneThickness, 2.4f, 3f), assets.PaneMaterial);
            partition.AddComponent<GlassPane>();
            PlaceChair(new Vector3(5f, SlabTop + 0.05f, -11.5f));

            var dummies = new GameObject("Dummies");
            Dummy(dummies.transform, "Dummy Far 1", new Vector3(-6f, SlabTop, 9f), 180f, assets.DummyMaterial);
            Dummy(dummies.transform, "Dummy Far 2", new Vector3(-2f, SlabTop, 8f), 180f, assets.DummyMaterial);
            Dummy(dummies.transform, "Dummy Far 3", new Vector3(2f, SlabTop, 10f), 180f, assets.DummyMaterial);
            Dummy(dummies.transform, "Dummy Far 4", new Vector3(6f, SlabTop, 7f), 180f, assets.DummyMaterial);
            Dummy(dummies.transform, "Dummy Near", new Vector3(-5f, SlabTop, -13f), 0f, assets.DummyMaterial);

            // Two that run along the far roof: one at the sprint speed, one at twice it for slides and jump chains.
            float roof = SlabTop + CeilingHeight + 0.3f;
            Runner(dummies.transform, "Runner Sprint", new Vector3(-6f, roof, 10f), SprintSpeed, assets.DummyMaterial);
            Runner(dummies.transform, "Runner Fast", new Vector3(-6f, roof, 13f), SprintSpeed * 2f, assets.DummyMaterial);
            Runner(dummies.transform, "Runner Top Speed", new Vector3(-6f, roof, 15f), TopSpeed, assets.DummyMaterial);

            GreyboxSceneSetup.EnsurePlayRig(SpawnA, SpawnB);
            EnsureSceneObjects(assets);
            EnsureCombatHud();

            AssignSceneIds(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Shooting Lane] Wrote " + ScenePath);
        }

        /// <summary>
        /// FishNet gives scene NetworkObjects their ids from its own editor callbacks, at most
        /// once every quarter second, so objects made in one go by a tool are left without one
        /// and never spawn. This asks it for every networked object in the scene at once (the
        /// same thing as its Reserialize Scenes menu, for one scene).
        /// </summary>
        static void AssignSceneIds(UnityEngine.SceneManagement.Scene scene)
        {
            MethodInfo create = typeof(NetworkObject).GetMethod("CreateSceneId", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(UnityEngine.SceneManagement.Scene), typeof(bool), typeof(int).MakeByRefType() }, null);
            if (create == null)
            {
                Debug.LogError("[Shooting Lane] FishNet's scene id step has moved; run Fish-Networking > Utility > Reserialize NetworkObjects > Reserialize Scenes on " + ScenePath + ".");
                return;
            }

            var args = new object[] { scene, true, 0 };
            create.Invoke(null, args);
            Debug.Log($"[Shooting Lane] Gave {args[2]} networked objects in the test scene their ids.");
        }

        static void BuildOffice(Transform parent, string name, float side, LaneAssets assets, bool explicitPanes)
        {
            var office = new GameObject(name);
            office.transform.SetParent(parent, false);
            Transform t = office.transform;

            float front = side * WindowZ;                      // the window wall, street side
            float middle = side * (WindowZ + OfficeDepth * 0.5f);
            float back = side * (WindowZ + OfficeDepth);
            float wallY = SlabTop + CeilingHeight * 0.5f;

            Box(t, "Slab", new Vector3(0f, SlabTop - 0.15f, middle), new Vector3(OfficeWidth, 0.3f, OfficeDepth), null);
            Box(t, "Ceiling", new Vector3(0f, SlabTop + CeilingHeight + 0.15f, middle), new Vector3(OfficeWidth, 0.3f, OfficeDepth), null);
            Box(t, "Back wall", new Vector3(0f, wallY, back + side * 0.15f), new Vector3(OfficeWidth, CeilingHeight, 0.3f), null);
            Box(t, "Side wall", new Vector3(OfficeWidth * 0.5f + 0.15f, wallY, middle), new Vector3(0.3f, CeilingHeight, OfficeDepth), null);

            // The open side has a ramp down to the street, so a player who falls out can walk back up.
            const float rampRun = 10f;
            float rampAngle = Mathf.Atan2(SlabTop, rampRun) * Mathf.Rad2Deg;
            GameObject ramp = Box(t, "Ramp", new Vector3(-OfficeWidth * 0.5f - rampRun * 0.5f, SlabTop * 0.5f - 0.1f, middle), new Vector3(Mathf.Sqrt(rampRun * rampRun + SlabTop * SlabTop), 0.2f, 3f), null);
            ramp.transform.localRotation = Quaternion.Euler(0f, 0f, rampAngle);

            float wallZ = front + side * 0.1f;
            Box(t, "Sill", new Vector3(0f, SlabTop + SillHeight * 0.5f, wallZ), new Vector3(OfficeWidth, SillHeight, 0.2f), null);
            const float lintel = 0.3f;
            Box(t, "Lintel", new Vector3(0f, SlabTop + CeilingHeight - lintel * 0.5f, wallZ), new Vector3(OfficeWidth, lintel, 0.2f), null);

            int bays = Mathf.RoundToInt(OfficeWidth / BayWidth);
            const float pillar = 0.3f;
            for (int i = 0; i <= bays; i++)
                Box(t, "Pillar", new Vector3(-OfficeWidth * 0.5f + i * BayWidth, wallY, wallZ), new Vector3(pillar, CeilingHeight, 0.2f), null);

            float paneHeight = CeilingHeight - SillHeight - lintel;
            for (int i = 0; i < bays; i++)
            {
                float x = -OfficeWidth * 0.5f + (i + 0.5f) * BayWidth;
                GameObject pane = Box(t, "Window pane", new Vector3(x, SlabTop + SillHeight + paneHeight * 0.5f, wallZ), new Vector3(BayWidth - pillar, paneHeight, PaneThickness), assets.PaneMaterial);
                if (explicitPanes)
                    pane.AddComponent<GlassPane>();
            }
        }

        static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = centre;
            box.transform.localScale = size;
            if (material != null)
            {
                var renderer = box.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            return box;
        }

        /// <summary>A capsule with Health that falls over when it dies and stands back up five seconds later. A networked scene object, so a client sees the host's numbers.</summary>
        static GameObject Dummy(Transform parent, string name, Vector3 feet, float yaw, Material material)
        {
            var root = new GameObject(name) { layer = PlayerLayer };
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            var fall = new GameObject("Fall") { layer = PlayerLayer };
            fall.transform.SetParent(root.transform, false);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.layer = PlayerLayer;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(fall.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            if (material != null)
                body.GetComponent<MeshRenderer>().sharedMaterial = material;

            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.layer = PlayerLayer;
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(fall.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.45f, 0.4f);
            nose.transform.localScale = new Vector3(0.15f, 0.15f, 0.2f);

            root.AddComponent<NetworkObject>();
            var health = root.AddComponent<Health>();
            var serialized = new SerializedObject(health);
            GreyboxSceneSetup.SetReference(serialized, "_tipPivot", fall.transform);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>A dummy that runs 12 m back and forth and takes a great many hits, for practising on a moving body.</summary>
        static void Runner(Transform parent, string name, Vector3 feet, float speed, Material material)
        {
            GameObject runner = Dummy(parent, name, feet, 0f, material);
            runner.AddComponent<FishNet.Component.Transforming.NetworkTransform>();
            var mover = runner.AddComponent<MovingTarget>();

            var serializedMover = new SerializedObject(mover);
            GreyboxSceneSetup.SetFloat(serializedMover, "_speed", speed);
            serializedMover.ApplyModifiedPropertiesWithoutUndo();

            var serializedHealth = new SerializedObject(runner.GetComponent<Health>());
            GreyboxSceneSetup.SetFloat(serializedHealth, "_maxHealth", RunnerHealth);
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();
        }

        static void PlaceChair(Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChairPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Shooting Lane] No Chair.prefab; the test scene has nothing to throw through the glass-room wall.");
                return;
            }

            var chair = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            chair.name = "Chair (throw me)";
            chair.transform.position = position;
        }

        static void EnsureSceneObjects(LaneAssets assets)
        {
            if (Object.FindAnyObjectByType<GlassRegistry>() == null)
            {
                var go = new GameObject("Glass");
                go.AddComponent<NetworkObject>();
                var registry = go.AddComponent<GlassRegistry>();
                var serialized = new SerializedObject(registry);
                GreyboxSceneSetup.SetReference(serialized, "_profile", assets.Glass);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (Object.FindAnyObjectByType<StartingLoadout>() == null)
            {
                var go = new GameObject("Starting Loadout");
                go.AddComponent<StartingLoadout>().Weapons = new[] { assets.Pistol, assets.Rifle };
            }
        }

        // =======================================================================================
        // Self-check: plays the test scene and checks the lane's rules
        // =======================================================================================
        //
        // From the menu it plays as the host alone. From the command line it can also play as a
        // second copy of the game that joins the first, which is the only way the rules about
        // "seen from a client" get checked:
        //
        //   Unity -batchmode -projectPath <project> -local
        //         -executeMethod CloseTheDeal.Editor.Lanes.ShootingLaneSetup.SelfCheckFromCommandLine
        //         -laneRole host|client -laneOut <folder> [-laneWithClient]
        //
        // The two copies talk through small flag files in the shared output folder.

        const string RoleKey = "ShootingLane.SelfCheck.Role";
        const string OutKey = "ShootingLane.SelfCheck.Out";
        const string WithClientKey = "ShootingLane.SelfCheck.WithClient";
        const string ExitKey = "ShootingLane.SelfCheck.Exit";

        static readonly Stack<IEnumerator> Routines = new();
        static readonly FieldInfo YawField = typeof(PlayerCamera).GetField("_yaw", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo PitchField = typeof(PlayerCamera).GetField("_pitch", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly Collider[] BlastBuffer = new Collider[32];

        static string _role;
        static string _out;
        static int _failures;
        static Camera _sideCamera;
        static Camera _clipCamera;
        static string _clipName;
        static int _clipFrame;
        static int _lastClipGameFrame;

        // What the copy under test heard.
        static int _fired, _dryFired, _reloadStarted, _reloadFinished, _reloadCancelled, _hitConfirmed, _killsConfirmed;
        static int _panesBroken, _deaths, _respawns;
        static float _lastHitDamage;

        [MenuItem("Close the Deal/Lanes/Run Shooting Self-Check (host only)")]
        static void SelfCheckFromMenu()
        {
            StartSelfCheck("host", Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ShootingSelfCheck"), false, false, ScenePath);
        }

        public static void SelfCheckFromCommandLine()
        {
            string role = Arg("-laneRole") ?? "host";
            string output = Arg("-laneOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ShootingSelfCheck");
            StartSelfCheck(role, output, HasFlag("-laneWithClient"), true, Arg("-laneScene") ?? ScenePath);
        }

        /// <summary>Runs Set Up from the command line and quits, so a headless run can build the lane before checking it.</summary>
        public static void SetUpFromCommandLine()
        {
            SetUp();
            EditorApplication.Exit(0);
        }

        static void StartSelfCheck(string role, string output, bool withClient, bool exitWhenDone, string scenePath)
        {
            Directory.CreateDirectory(output);
            SessionState.SetString(RoleKey, role);
            SessionState.SetString(OutKey, output);
            SessionState.SetBool(WithClientKey, withClient);
            SessionState.SetBool(ExitKey, exitWhenDone);
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                SessionState.EraseString(RoleKey);
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            // A HUD capture adds the combat HUD to the opened scene for the run, unsaved, if it lacks one.
            if (role == "hud")
                EnsureCombatHud();
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Entering Play reloads scripts, so the run is picked up again from the session's notes.</summary>
        [InitializeOnLoadMethod]
        static void ResumeSelfCheck()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(RoleKey, "")))
                return;

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlaying)
                Begin();
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // Stopped part-way through by hand: forget the run, or the next Play would pick it up.
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                PlayerInputReader.ScriptedCapture = false;
                Routines.Clear();
                EditorApplication.update -= Step;
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                SessionState.EraseString(RoleKey);
                return;
            }

            if (change == PlayModeStateChange.EnteredPlayMode && Routines.Count == 0)
                Begin();
        }

        static void Begin()
        {
            _role = SessionState.GetString(RoleKey, "host");
            _out = SessionState.GetString(OutKey, "");
            _failures = 0;
            Application.runInBackground = true;
            // Batch players cannot lock a real cursor; scripted weapon input needs gameplay capture.
            PlayerInputReader.ScriptedCapture = true;
            Routines.Clear();
            UsePort();
            Routines.Push(_role == "client" ? ClientRoutine() : _role == "panes" ? PaneCountRoutine() : _role == "hud" ? HudRoutine() : HostRoutine(SessionState.GetBool(WithClientKey, false)));
            EditorApplication.update += Step;
            Log($"=== Shooting self-check, role {_role}, Unity {Application.unityVersion} ===");
        }

        /// <summary>With -lanePort, local test mode uses that port, so a run never meets someone else's game on this PC.</summary>
        static void UsePort()
        {
            if (!ushort.TryParse(Arg("-lanePort"), out ushort port))
                return;

            var tugboat = Object.FindAnyObjectByType<FishNet.Transporting.Tugboat.Tugboat>();
            if (tugboat == null)
                return;

            tugboat.SetPort(port);
            Log($"Local test port {port}.");
        }

        static void Step()
        {
            if (Routines.Count == 0)
                return;

            try
            {
                RecordClipFrame();
                IEnumerator top = Routines.Peek();
                if (!top.MoveNext())
                {
                    Routines.Pop();
                    if (Routines.Count == 0)
                        Finish();
                }
                else if (top.Current is IEnumerator child)
                {
                    Routines.Push(child);
                }
            }
            catch (Exception e)
            {
                Log("EXCEPTION " + e);
                _failures++;
                Routines.Clear();
                Finish();
            }
        }

        static void Finish()
        {
            PlayerInputReader.ScriptedCapture = false;
            EditorApplication.update -= Step;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Log($"=== {_role} finished: {(_failures == 0 ? "all checks passed" : _failures + " check(s) FAILED")} ===");
            bool exit = SessionState.GetBool(ExitKey, false);
            SessionState.EraseString(RoleKey);
            if (exit)
                EditorApplication.Exit(_failures == 0 ? 0 : 1);
            else
                EditorApplication.ExitPlaymode();
        }

        // ---- The host's run ------------------------------------------------------------------

        /// <summary>The copy under test: its player and the parts of it the checks read.</summary>
        sealed class Subject
        {
            public PlayerMotor Me;
            public WeaponHolder Holder;
            public Health Health;
            public GlassRegistry Glass;
        }

        static IEnumerator HostRoutine(bool withClient)
        {
            SteamLobby lobby = null;
            yield return Until(() => (lobby = Object.FindAnyObjectByType<SteamLobby>()) != null && lobby.Ready, 30f, "lobby ready");
            Log($"Lobby local mode: {lobby.LocalMode}");
            lobby.Host();
            yield return Until(() => PlayerMotor.Local != null, 30f, "own player spawned");
            // A copy that failed to host may have joined someone else's game on this PC instead.
            Check("this copy is the host", PlayerMotor.Local.IsServerStarted, $"server started {PlayerMotor.Local.IsServerStarted}");
            if (!PlayerMotor.Local.IsServerStarted)
                throw new InvalidOperationException("Not the host; every later check would test someone else's game.");
            yield return Seconds(1f);

            var s = new Subject { Me = PlayerMotor.Local, Glass = GlassRegistry.Instance };
            s.Holder = s.Me.GetComponent<WeaponHolder>();
            s.Health = s.Me.GetComponent<Health>();
            Listen(s.Holder);
            _sideCamera = MakeSideCamera();

            Check("spawns with both guns", s.Holder.WeaponInSlot(0) != null && s.Holder.WeaponInSlot(1) != null,
                $"slot 1 {Name(s.Holder.WeaponInSlot(0))}, slot 2 {Name(s.Holder.WeaponInSlot(1))}, in hand {Name(s.Holder.ActiveWeapon)} with {s.Holder.AmmoInMagazine} rounds");
            CombatSounds sounds = s.Me.GetComponent<CombatSounds>();
            var set = sounds != null ? new SerializedObject(sounds).FindProperty("_set").objectReferenceValue as CombatSoundSet : null;
            WeaponDefinition pistol = s.Holder.WeaponInSlot(0);
            bool wired = set != null && set.Output != null && set.ImpactWorld != null && pistol != null && pistol.ShotSound != null && pistol.ReloadEndSound != null;
            Check("gun sounds are wired (headless Unity has no audio device, so nothing is heard here)", wired,
                $"player has CombatSounds {sounds != null}, bus {(set != null && set.Output != null ? set.Output.name : "none")}, pistol shot {(pistol != null && pistol.ShotSound != null ? pistol.ShotSound.name : "none")}");
            Check("panes numbered", s.Glass != null && s.Glass.PaneCount == 9, $"{(s.Glass != null ? s.Glass.PaneCount : -1)} panes known (4 with a Glass Pane + 1 glass-room wall + 4 by material), {s.Glass?.BrokenCount} broken");

            StartClip("clip_pistol_and_rifle");
            yield return PistolCheck(s);
            yield return BrokenPaneCheck(s);
            yield return RifleCheck(s);
            StopClip();
            yield return DryFireCheck(s);
            yield return ReloadSwitchCheck(s);
            yield return DeathCheck(s);
            yield return MovingTargetCheck(s.Holder, 9);
            yield return PatternCheck(s);
            yield return MenuGateCheck(s.Holder);
            yield return BlastCheck(s);
            yield return ChairCheck(s.Glass);
            yield return AllocationCheck(s.Holder, new Vector3(-2f, SlabTop + 1.5f, 16f));

            Log($"HOST STATE panes broken {s.Glass.BrokenCount}, dummies {DummyStates()}");
            if (withClient)
                yield return HostWithClient(s.Me, s.Holder, s.Glass);
        }

        /// <summary>One pistol shot through two panes into a dummy, then three more for the kill.</summary>
        static IEnumerator PistolCheck(Subject s)
        {
            Health target = FindDummy("Dummy Far 2");
            Capture("01_before");
            yield return Aim(Chest(target));
            s.Holder.PullTrigger();
            yield return Frames(2);
            Capture("02_pistol_shot");
            yield return Seconds(0.25f);
            Check("pistol shot: host confirms the hit", _fired == 1 && _hitConfirmed == 1 && Mathf.Approximately(target.Current, target.Max - s.Holder.ActiveWeapon.Damage),
                $"fired {_fired}, hit markers {_hitConfirmed} (damage {_lastHitDamage}), dummy health {target.Current}");
            Check("pistol shot: both panes on its path broke", s.Glass.BrokenCount == 2 && _panesBroken == 2, $"{s.Glass.BrokenCount} broken in the registry, {_panesBroken} shatter events here");

            for (int i = 0; i < 2; i++)
            {
                yield return Aim(Chest(target));
                s.Holder.PullTrigger();
                yield return Seconds(0.3f);
            }

            Capture("03_dummy_dead");
            Check("three pistol hits kill the dummy", target.IsDead && _killsConfirmed == 1, $"dead {target.IsDead}, health {target.Current}, kills confirmed {_killsConfirmed}, last attacker client {target.LastAttackerClientId}");
        }

        static IEnumerator BrokenPaneCheck(Subject s)
        {
            int brokenBefore = s.Glass.BrokenCount;
            int shattersBefore = _panesBroken;
            GlassPane brokenPane = FirstPane(true);
            yield return Aim(Chest(FindDummy("Dummy Far 2")));
            s.Holder.PullTrigger();
            yield return Seconds(0.3f);
            brokenPane.ApplyDamage(50f, brokenPane.Centre, Vector3.forward, -1);
            yield return Frames(3);
            Check("shooting an already broken pane does nothing", s.Glass.BrokenCount == brokenBefore && _panesBroken == shattersBefore,
                $"broken {brokenBefore} -> {s.Glass.BrokenCount}, shatter events {shattersBefore} -> {_panesBroken}");
        }

        /// <summary>The AR-15 held down for a second and a half.</summary>
        static IEnumerator RifleCheck(Subject s)
        {
            Health target = FindDummy("Dummy Far 3");
            Check("switch to slot 2", s.Holder.Switch(1), $"in hand {Name(s.Holder.ActiveWeapon)}");
            yield return Seconds(0.7f);
            int firedBefore = _fired;
            int hitsBefore = _hitConfirmed;
            yield return Aim(Chest(target));
            float burstEnds = Time.time + 1.5f;
            bool captured = false;
            while (Time.time < burstEnds)
            {
                s.Holder.PullTrigger();
                if (!captured && Time.time > burstEnds - 1f)
                {
                    Capture("04_rifle_burst");
                    captured = true;
                }

                yield return null;
            }

            yield return Seconds(0.3f);
            int burst = _fired - firedBefore;
            float straight = s.Holder.CurrentSpreadDegrees;
            yield return Seconds(1f);
            float rested = s.Holder.CurrentSpreadDegrees;
            Check("the AR-15's stray builds through a burst and settles after a rest", straight > s.Holder.ActiveWeapon.BaseSpread + 0.1f && Mathf.Approximately(rested, s.Holder.ActiveWeapon.BaseSpread),
                $"stray {straight:0.00} deg straight after the burst, {rested:0.00} deg a second later (rest {s.Holder.ActiveWeapon.BaseSpread:0.00})");
            Check("AR-15 fires at its rate while held", burst >= 14 && burst <= 17, $"{burst} shots in 1.5 s (600 a minute is 15), {_hitConfirmed - hitsBefore} hit the dummy at 19 m with the aim left to climb, dummy health {target.Current}, dead {target.IsDead}");
        }

        static IEnumerator DryFireCheck(Subject s)
        {
            float emptyBy = Time.time + 6f;
            while (s.Holder.AmmoInMagazine > 0 && Time.time < emptyBy)
            {
                s.Holder.PullTrigger();
                yield return null;
            }

            yield return Seconds(0.3f);
            int firedBefore = _fired;
            int dryBefore = _dryFired;
            s.Holder.PullTrigger();
            yield return Frames(3);
            Check("empty magazine dry-fires", s.Holder.AmmoInMagazine == 0 && _fired == firedBefore && _dryFired == dryBefore + 1,
                $"ammo {s.Holder.AmmoInMagazine}, shots {firedBefore} -> {_fired}, dry-fire events {dryBefore} -> {_dryFired}");
        }

        /// <summary>A reload cut off by a switch must fill nothing; a full one fills the magazine once.</summary>
        static IEnumerator ReloadSwitchCheck(Subject s)
        {
            int cancelledBefore = _reloadCancelled;
            bool reloadBegan = s.Holder.Reload();
            yield return Seconds(0.5f);
            s.Holder.Switch(0);
            int pistolAmmo = s.Holder.AmmoInMagazine;
            yield return Seconds(3f); // longer than the rifle's reload
            s.Holder.Switch(1);
            yield return Seconds(0.7f);
            Check("reload cut off by a switch gives no ammo", reloadBegan && _reloadCancelled == cancelledBefore + 1 && s.Holder.AmmoInMagazine == 0,
                $"reload began {reloadBegan}, cancelled events {_reloadCancelled - cancelledBefore}, rifle ammo after 3 s away {s.Holder.AmmoInMagazine}, pistol stayed at {pistolAmmo}");

            int finishedBefore = _reloadFinished;
            s.Holder.Reload();
            yield return Seconds(s.Holder.ActiveWeapon.ReloadSeconds + 0.5f);
            Check("a full reload fills the magazine once", s.Holder.AmmoInMagazine == 30 && _reloadFinished == finishedBefore + 1, $"rifle ammo {s.Holder.AmmoInMagazine}, reload-finished events {_reloadFinished - finishedBefore}");
        }

        /// <summary>Dying in the middle of a reload, then the respawn.</summary>
        static IEnumerator DeathCheck(Subject s)
        {
            s.Holder.Switch(0);
            yield return Seconds(0.5f);
            int cancelledBefore = _reloadCancelled;
            s.Holder.Reload();
            yield return Seconds(0.4f);
            Vector3 diedAt = s.Me.transform.position;
            s.Health.ApplyDamage(999f, diedAt, Vector3.back, -1);
            uint diedTick = s.Me.TimeManager.Tick;
            yield return Frames(3);
            int firedBefore = _fired;
            for (int i = 0; i < 10; i++)
            {
                s.Holder.PullTrigger();
                yield return null;
            }

            Capture("05_dead_view");
            bool blocked = _fired == firedBefore && !s.Holder.Reload() && !s.Holder.Switch(1);
            Check("dead: reload dropped, no firing, no reload, no switch", s.Health.IsDead && _reloadCancelled == cancelledBefore + 1 && blocked,
                $"dead {s.Health.IsDead}, reload cancelled {_reloadCancelled - cancelledBefore}, shots while dead {_fired - firedBefore}");

            float diedTime = Time.time;
            yield return Until(() => !s.Health.IsDead, 8f, "respawn");
            float waited = Time.time - diedTime;
            yield return Seconds(0.6f);
            int pistolAfter = s.Holder.AmmoInMagazine;
            s.Holder.Switch(1);
            yield return Seconds(0.7f);
            Vector3 back = s.Me.transform.position;
            bool whole = Mathf.Approximately(s.Health.Current, s.Health.Max) && pistolAfter == 12 && s.Holder.AmmoInMagazine == 30;
            Check("respawn after 5 s on the same floor with full magazines", waited > 4.5f && waited < 5.6f && whole && Mathf.Abs(back.y - SlabTop) < 0.5f,
                $"waited {waited:0.00} s, health {s.Health.Current}, pistol {pistolAfter}, rifle {s.Holder.AmmoInMagazine}, died at {diedAt}, back at {back}, respawn events {_respawns}");

            firedBefore = _fired;
            s.Holder.PullTrigger();
            yield return Frames(3);
            Check("can fire again after respawn", _fired == firedBefore + 1, $"shots {_fired - firedBefore}");

            uint now = s.Me.TimeManager.Tick;
            Check("a shot rewound to before the respawn can't find the new body", !s.Health.WasAliveAt(diedTick) && s.Health.WasAliveAt(now),
                $"counts as alive at the tick it died ({diedTick}): {s.Health.WasAliveAt(diedTick)}; now ({now}): {s.Health.WasAliveAt(now)}");

            yield return OriginCheck(s);
        }

        /// <summary>Pistol shots at the two runners, aiming at the body as it is drawn on this copy, the way a player tracks a target.</summary>
        static IEnumerator MovingTargetCheck(WeaponHolder holder, int needed)
        {
            if (holder.ActiveSlot != 0)
                holder.Switch(0);
            yield return Seconds(0.5f);

            foreach (string name in new[] { "Runner Sprint", "Runner Fast", "Runner Top Speed" })
            {
                Health runner = FindDummy(name);
                float speed = runner.GetComponent<MovingTarget>().Speed;
                holder.Reload();
                yield return Seconds(holder.ActiveWeapon.ReloadSeconds + 0.6f);

                int hitsBefore = _hitConfirmed;
                int firedBefore = _fired;
                StartClip($"clip_{name.Replace(' ', '_').ToLowerInvariant()}");
                for (int i = 0; i < 10; i++)
                {
                    // Long enough for the previous shot's kick to hold and settle, so every shot starts from a still view.
                    yield return Seconds(0.45f);
                    // Aim at the body as this copy drew it last frame and fire in the same step,
                    // the way a player's click answers the picture on screen.
                    SetAimNow(Chest(runner));
                    LogDrawn(holder, runner);
                    holder.PullTrigger();
                    yield return Frames(1);
                    if (i == 4)
                        Capture("09_" + name.Replace(' ', '_').ToLowerInvariant());
                }

                yield return Seconds(0.6f);
                StopClip();
                int hits = _hitConfirmed - hitsBefore;
                long ping = holder.TimeManager.RoundTripTime;
                Check($"landing pistol shots on {name} at {speed:0} m/s", hits >= needed,
                    $"{hits} of {_fired - firedBefore} confirmed by the host; this copy's round trip {ping} ms, so the host has the runner about {speed * (ping / 1000f + 2f / 60f):0.0} m ahead of where it is drawn here");
            }
        }

        /// <summary>
        /// Client only: ten pistol shots at the host's own player while the host throws it from
        /// side to side at about sprint speed. Players reach a client through prediction, not
        /// the transform sync the dummies use, so they can be drawn at a different delay.
        /// </summary>
        static IEnumerator MovingPlayerCheck(WeaponHolder holder, PlayerMotor me)
        {
            PlayerMotor host = OtherPlayer(me);
            Health target = host.GetComponent<Health>();
            if (holder.ActiveSlot != 0)
                holder.Switch(0);
            yield return Seconds(0.5f);
            holder.Reload();
            yield return Seconds(holder.ActiveWeapon.ReloadSeconds + 0.5f);

            Flag("shoot_host_start");
            yield return Seconds(1.5f);
            int hitsBefore = _hitConfirmed;
            StartClip("clip_moving_player");
            for (int i = 0; i < 10; i++)
            {
                yield return Seconds(0.45f);
                SetAimNow(host.transform.position + Vector3.up * 1.1f);
                LogDrawn(holder, target);
                holder.PullTrigger();
                yield return Frames(1);
            }

            yield return Seconds(0.6f);
            StopClip();
            Flag("shoot_host_done");
            int hits = _hitConfirmed - hitsBefore;
            Check("landing pistol shots on a player thrown side to side (9 m/s at each throw, slowing to a stop)", hits >= 8,
                $"{hits} of 10 confirmed by the host; this copy's round trip {holder.TimeManager.RoundTripTime} ms");
        }

        /// <summary>Host only: throws its own player left and right until the client is done shooting at it, healed between shots so it never dies.</summary>
        static IEnumerator Dodge(PlayerMotor me)
        {
            Health health = me.GetComponent<Health>();

            Teleport(me, new Vector3(-2f, SlabTop + 0.05f, -9f));
            float side = 1f;
            while (!HasFlagFile("shoot_host_done"))
            {
                // Throw towards the middle of the room each time, so the body swings across about 5 m.
                side = me.transform.position.x > -2f ? -1f : 1f;
                me.ApplyKnockback(new Vector3(side * 9f, 0f, 0f), 0.4f);
                health.Restore();
                yield return Seconds(0.55f);
            }

            health.Restore();
        }

        /// <summary>The AR-15's view kick must follow the same path magazine after magazine when the aim is left alone.</summary>
        static IEnumerator PatternCheck(Subject s)
        {
            var first = new List<Vector2>();
            var second = new List<Vector2>();
            Vector3 aimPoint = new(-2f, SlabTop + 1.6f, WindowZ + OfficeDepth);

            if (s.Holder.ActiveSlot != 1)
                s.Holder.Switch(1);
            yield return Seconds(0.6f);
            yield return RecordMagazine(s.Holder, aimPoint, first);
            yield return RecordMagazine(s.Holder, aimPoint, second);

            float worst = 0f;
            int compared = Mathf.Min(first.Count, second.Count);
            for (int i = 0; i < compared; i++)
                worst = Mathf.Max(worst, (first[i] - second[i]).magnitude);

            var path = new System.Text.StringBuilder();
            for (int i = 0; i < first.Count; i += 3)
                path.Append($"#{i + 1} ({first[i].x:0.00}, {first[i].y:0.00}) ");
            Log($"PATTERN view offset at each shot, degrees (right, up): {path}");
            Check("AR-15 recoil pattern repeats across two magazines", compared == 30 && worst < 0.05f,
                $"{first.Count} and {second.Count} shots; largest difference between the two magazines at the same shot {worst:0.000} degrees");
        }

        static IEnumerator RecordMagazine(WeaponHolder holder, Vector3 aimPoint, List<Vector2> offsets)
        {
            holder.Reload();
            yield return Seconds(holder.ActiveWeapon.ReloadSeconds + 2f); // long enough for the view to settle fully
            yield return Aim(aimPoint);
            Transform cam = PlayerCamera.Instance.transform;
            Vector3 startEuler = cam.eulerAngles;

            void Record(ShotInfo shot)
            {
                Vector3 now = cam.eulerAngles;
                offsets.Add(new Vector2(Mathf.DeltaAngle(startEuler.y, now.y), -Mathf.DeltaAngle(startEuler.x, now.x)));
            }

            holder.Fired += Record;
            float giveUp = Time.time + 6f;
            while (holder.AmmoInMagazine > 0 && Time.time < giveUp)
            {
                holder.PullTrigger();
                yield return null;
            }

            yield return Frames(2);
            holder.Fired -= Record;
        }

        /// <summary>A shot may start from the shooter's own eye position only when the host can see it: never through the floor or a wall.</summary>
        static IEnumerator OriginCheck(Subject s)
        {
            MethodInfo trusted = typeof(WeaponHolder).GetMethod("TrustedOrigin", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo eyeOf = typeof(WeaponHolder).GetMethod("HostEye", BindingFlags.Instance | BindingFlags.NonPublic);
            var eye = (Vector3)eyeOf.Invoke(s.Holder, null);
            Vector3 throughFloor = eye + Vector3.down * 2.2f;
            Vector3 clear = eye + s.Me.transform.right * 0.5f;
            var fromFloor = (Vector3)trusted.Invoke(s.Holder, new object[] { throughFloor, 0u });
            var fromClear = (Vector3)trusted.Invoke(s.Holder, new object[] { clear, 0u });
            Check("a shot origin behind the floor slab is refused; a clear one nearby is kept", fromFloor == eye && fromClear == clear,
                $"through the floor -> {(fromFloor == eye ? "the host's eye" : fromFloor.ToString())}; 0.5 m to the side -> {(fromClear == clear ? "kept" : fromClear.ToString())}");
            yield break;
        }

        static IEnumerator BlastCheck(Subject s)
        {
            var blast = AssetDatabase.LoadAssetAtPath<BlastProfile>(TestBlastPath);
            int brokenBefore = s.Glass.BrokenCount;
            Knockback.Explode(new Vector3(6f, SlabTop + 1.5f, WindowZ + 1f), blast, 1 << PlayerLayer, 1 << GreyboxSceneSetup.PropLayer, BlastBuffer);
            yield return Frames(3);
            Capture("06_blast_broke_glass");
            Check("a blast breaks the glass in its radius", s.Glass.BrokenCount > brokenBefore, $"broken {brokenBefore} -> {s.Glass.BrokenCount} (blast radius {blast.Radius} m, 1 m behind a far pane)");
        }

        static IEnumerator MenuGateCheck(WeaponHolder holder)
        {
            Mouse mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            GameMenu menu = Object.FindAnyObjectByType<GameMenu>();

            Cursor.lockState = CursorLockMode.None; // what the open menu does
            yield return Frames(2);
            int firedBefore = _fired;
            int dryBefore = _dryFired;
            _clickSeen = false;
            yield return Click(mouse);
            // The control: the click must have reached the Input System, or "nothing happened" proves nothing.
            Check("menu open (mouse free): the fire button does nothing", _clickSeen && _fired == firedBefore && _dryFired == dryBefore,
                $"click reached the Input System {_clickSeen}, cursor {Cursor.lockState}, shots {_fired - firedBefore}, dry-fires {_dryFired - dryBefore}");

            if (menu != null)
                menu.Resume();
            Cursor.lockState = CursorLockMode.Locked;
            yield return Frames(2);
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Log("NOTE  cursor lock does not hold in this headless run, so the real fire button with the menu closed was not exercised; shots in this run go through WeaponHolder.PullTrigger.");
                yield break;
            }

            firedBefore = _fired;
            yield return Click(mouse);
            Check("menu closed (mouse captured): the fire button fires", _fired == firedBefore + 1, $"cursor {Cursor.lockState}, shots {_fired - firedBefore}");
        }

        static bool _clickSeen;

        static IEnumerator Click(Mouse mouse)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, true));
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                _clickSeen |= mouse.leftButton.isPressed;
            }

            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frames(3);
        }

        static IEnumerator ChairCheck(GlassRegistry glass)
        {
            Prop chair = Object.FindAnyObjectByType<Prop>();
            GlassPane wall = GameObject.Find("Glass room wall").GetComponent<GlassPane>();
            if (chair == null)
            {
                Log("NOTE  no chair in the scene; the thrown-object check was skipped.");
                yield break;
            }

            float mass = chair.GetComponent<Rigidbody>().mass;
            float startX = chair.transform.position.x;
            chair.Throw(new Vector3(-16f, 1.5f, 0f));
            StartClip("clip_chair_through_glass");
            yield return Seconds(0.8f);
            StopClip();
            Capture("07_chair_through_glass");
            Check("a thrown chair breaks the glass-room wall and carries on through", wall.IsBroken && chair.transform.position.x < wall.Centre.x,
                $"wall broken {wall.IsBroken}, chair ({mass} kg) x {startX:0.0} -> {chair.transform.position.x:0.0}, wall at x {wall.Centre.x:0.0}, panes broken {glass.BrokenCount}");
        }

        /// <summary>Bytes the main thread allocates over the same number of frames idle and with the rifle held down. The difference is what firing costs.</summary>
        static IEnumerator AllocationCheck(WeaponHolder holder, Vector3 aimPoint)
        {
            // A fixed number of shots, not frames: the AR-15's full magazine, about three seconds.
            const float window = 3.2f;
            if (holder.ActiveSlot != 1)
                holder.Switch(1);
            yield return Seconds(0.6f);
            holder.Reload();
            yield return Seconds(3f);
            yield return Aim(aimPoint);

            // Warm the tracer pool and everything else that allocates once.
            for (int i = 0; i < 60; i++)
            {
                holder.PullTrigger();
                yield return null;
            }

            holder.Reload();
            yield return Seconds(3f);

            int startFrame = Time.frameCount;
            long before = GC.GetAllocatedBytesForCurrentThread();
            yield return Seconds(window);
            long idle = GC.GetAllocatedBytesForCurrentThread() - before;
            int idleFrames = Time.frameCount - startFrame;

            int firedBefore = _fired;
            startFrame = Time.frameCount;
            float until = Time.time + window;
            before = GC.GetAllocatedBytesForCurrentThread();
            while (Time.time < until)
            {
                holder.PullTrigger();
                yield return null;
            }

            long firing = GC.GetAllocatedBytesForCurrentThread() - before;
            int shots = _fired - firedBefore;
            long perShot = shots > 0 ? (firing - idle) / shots : 0;
            Log($"ALLOC over {window} s: idle {idle} B in {idleFrames} frames, firing {firing} B in {Time.frameCount - startFrame} frames with {shots} shots; difference per shot {perShot} B ({_role}, main thread, in the editor)");
            Check("firing allocates nothing per shot", shots >= 25 && perShot <= 0, $"{perShot} B per shot over {shots} shots");
        }

        /// <summary>With a second copy joined: its shot, a kill that goes out through a window, and what it sees when it joins again.</summary>
        static IEnumerator HostWithClient(PlayerMotor me, WeaponHolder holder, GlassRegistry glass)
        {
            FishNet.InstanceFinder.TimeManager.OnPostTick += LogRunnerTicks;
            Flag("host_ready");
            PlayerMotor other = null;
            yield return Until(() => (other = OtherPlayer(me)) != null, 240f, "the client's player to spawn");
            Health otherHealth = other.GetComponent<Health>();
            Log($"Client joined as client id {other.OwnerId}.");

            yield return Until(() => HasFlagFile("shoot_host_start"), 240f, "the client to start shooting at the host");
            yield return Dodge(me);
            yield return Until(() => HasFlagFile("client_shot_done"), 120f, "the client's shot");
            yield return Seconds(0.5f);
            Log($"HOST STATE after the client's shot: panes broken {glass.BrokenCount}, dummies {DummyStates()}");

            // The window shot: the client's player stands 2 m inside an unbroken near pane with its back to it; the host shoots it from deeper in the room.
            GlassPane window = PaneNear(new Vector3(-6f, SlabTop + 1.5f, -WindowZ));
            Check("the window behind the victim starts whole", window != null && !window.IsBroken, $"pane {window?.Id} broken {window?.IsBroken}");
            Teleport(other, new Vector3(-6f, SlabTop + 0.05f, -WindowZ - 2.2f));
            Teleport(me, new Vector3(-6f, SlabTop + 0.05f, -WindowZ - 7f));
            yield return Seconds(1f);
            if (holder.ActiveSlot != 0)
                holder.Switch(0);
            yield return Seconds(0.6f);
            holder.Reload();
            yield return Seconds(holder.ActiveWeapon.ReloadSeconds + 0.5f);
            yield return Aim(other.transform.position + Vector3.up * 1.1f);

            StartClip("clip_window_kill", _sideCamera);
            int brokenBefore = glass.BrokenCount;
            float deadline = Time.time + 6f;
            while (!otherHealth.IsDead && Time.time < deadline)
            {
                yield return Aim(other.transform.position + Vector3.up * 1.1f);
                holder.PullTrigger();
                yield return Seconds(0.25f);
            }

            float lowest = float.MaxValue;
            float furthest = float.MinValue;
            float until = Time.time + 2f;
            int shot = 0;
            while (Time.time < until)
            {
                Vector3 p = other.transform.position;
                lowest = Mathf.Min(lowest, p.y);
                furthest = Mathf.Max(furthest, p.z);
                if (shot < 3 && Time.time > until - 2f + 0.25f * (shot + 1))
                    Capture("08_window_kill_" + ++shot, _sideCamera);
                yield return null;
            }

            StopClip();
            Check("a kill shot throws the body out through the window", otherHealth.IsDead && window.IsBroken && furthest > -WindowZ && lowest < SlabTop - 1f,
                $"dead {otherHealth.IsDead}, window broken {window.IsBroken} (panes {brokenBefore} -> {glass.BrokenCount}), body got to z {furthest:0.0} (window at {-WindowZ:0.0}) and fell to y {lowest:0.0} (floor at {SlabTop:0.0})");

            yield return Until(() => !otherHealth.IsDead, 8f, "the client's player to respawn");
            yield return Seconds(0.5f);
            Vector3 back = other.transform.position;
            Check("the victim respawns on the floor it died on", otherHealth.LastAttackerClientId == me.OwnerId && Mathf.Abs(back.y - SlabTop) < 0.5f && back.z < -WindowZ, $"back at {back}, health {otherHealth.Current}");
            Log($"HOST STATE after the window kill: panes broken {glass.BrokenCount}, dummies {DummyStates()}");
            Flag("host_kill_done");

            yield return Until(() => HasFlagFile("client_done"), 180f, "the client to finish");
        }

        // ---- Any scene: is its glass breakable? ----------------------------------------------

        /// <summary>Hosts whatever scene was opened, waits for its towers, and checks the glass the registry found blocks and breaks.</summary>
        static IEnumerator PaneCountRoutine()
        {
            SteamLobby lobby = null;
            yield return Until(() => (lobby = Object.FindAnyObjectByType<SteamLobby>()) != null && lobby.Ready, 30f, "lobby ready");
            lobby.Host();
            yield return Until(() => GlassRegistry.Instance != null && (TowerBuilder.Instance == null || TowerBuilder.Instance.IsBuilt), 60f, "towers built");
            yield return Seconds(2f);

            GlassRegistry glass = GlassRegistry.Instance;
            GlassPane[] panes = Object.FindObjectsByType<GlassPane>(FindObjectsSortMode.None);
            int inTowers = 0;
            GlassPane sample = null;
            foreach (GlassPane pane in panes)
            {
                if (TowerBuilder.Instance == null || !pane.transform.IsChildOf(TowerBuilder.Instance.transform))
                    continue;
                inTowers++;
                if (sample == null)
                    sample = pane;
            }

            Log($"PANES registry {glass.PaneCount}, in the towers {inTowers}, scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            Check("this scene's towers contain breakable glass (none means the floor templates have no panes, not a fault here)", inTowers > 0, $"{inTowers} panes in the towers; first: {(sample != null ? sample.transform.parent.name + "/" + sample.name : "none")}");
            if (sample == null)
                yield break;

            Vector3 from = sample.Centre + sample.Normal * 2f;
            bool blocks = Physics.Raycast(from, -sample.Normal, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore) && GlassPane.TryFind(hit.collider, out GlassPane hitPane) && hitPane == sample;
            Check("an intact tower window stops a ray (people and things)", blocks, $"ray from 2 m out hit {(hit.collider != null ? hit.collider.name : "nothing")}");

            int before = glass.BrokenCount;
            sample.ApplyDamage(10f, sample.Centre, -sample.Normal, -1);
            yield return Frames(3);
            bool through = !Physics.Raycast(from, -sample.Normal, out RaycastHit after, 2.2f, ~0, QueryTriggerInteraction.Ignore) || after.collider == null || !GlassPane.TryFind(after.collider, out _);
            Check("a shot tower window breaks for the match and lets things through", sample.IsBroken && glass.BrokenCount == before + 1 && through, $"broken {sample.IsBroken}, registry {before} -> {glass.BrokenCount}");

            // A rematch builds new towers that number their panes the same way: they must come up whole.
            Vector3 where = sample.Centre;
            TowerBuilder.Instance.Rebuild();
            yield return Until(() => TowerBuilder.Instance.IsBuilt, 30f, "the rebuilt towers");
            yield return Seconds(1f);
            GlassPane again = PaneNear(where);
            Check("a rematch's new towers start with every pane whole", glass.BrokenCount == 0 && again != null && !again.IsBroken && HiddenPanes() == 0,
                $"registry {glass.BrokenCount} broken, the same window now broken {again?.IsBroken}, hidden panes {HiddenPanes()}");
        }

        // ---- The client's run ----------------------------------------------------------------

        static IEnumerator ClientRoutine()
        {
            SteamLobby lobby = null;
            yield return Until(() => (lobby = Object.FindAnyObjectByType<SteamLobby>()) != null && lobby.Ready, 30f, "lobby ready");
            yield return Until(() => HasFlagFile("host_ready"), 900f, "the host to be ready");
            TurnOnLatency();
            lobby.JoinLocal();
            yield return Until(() => PlayerMotor.Local != null, 40f, "own player spawned");
            GlassPane.AnyBroken += CountPane;
            yield return Seconds(1.5f);

            var s = new Subject { Me = PlayerMotor.Local, Glass = GlassRegistry.Instance };
            s.Holder = s.Me.GetComponent<WeaponHolder>();
            s.Health = s.Me.GetComponent<Health>();
            Listen(s.Holder);

            yield return ClientJoinAndShotChecks(s);
            yield return MovingTargetCheck(s.Holder, 8);
            yield return MovingPlayerCheck(s.Holder, s.Me);
            yield return AllocationCheck(s.Holder, new Vector3(-2f, SlabTop + 1.5f, 16f));
            Log($"CLIENT STATE after own shot: panes broken {s.Glass.BrokenCount}, dummies {DummyStates()}");
            Flag("client_shot_done");

            int brokenAtLeave = 0;
            string dummiesAtLeave = null;
            yield return ClientDeathCheck(s, (broken, dummies) => { brokenAtLeave = broken; dummiesAtLeave = dummies; });
            yield return ClientRejoinCheck(lobby, brokenAtLeave, dummiesAtLeave);
            GlassPane.AnyBroken -= CountPane;
            Flag("client_done");
        }

        /// <summary>Adds delay to everything this copy sends and receives, to stand in for a real connection.</summary>
        static void TurnOnLatency()
        {
            if (!long.TryParse(Arg("-laneLatency"), out long oneWay) || oneWay <= 0)
                return;

            var simulator = FishNet.InstanceFinder.NetworkManager.TransportManager.LatencySimulator;
            simulator.SetLatency(oneWay);
            simulator.SetEnabled(true);
            Log($"Latency simulator on: {oneWay} ms added each way.");
        }

        /// <summary>What a client sees on joining a match in progress, and its own first shot.</summary>
        static IEnumerator ClientJoinAndShotChecks(Subject s)
        {
            WeaponHolder holder = s.Holder;
            GlassRegistry glass = s.Glass;

            // Everything the host broke before this copy joined must already be broken here, quietly.
            int hidden = HiddenPanes();
            Check("joining after breaks: the same panes are already broken, without shards", glass.BrokenCount > 0 && hidden == glass.BrokenCount && _panesBroken == 0,
                $"registry says {glass.BrokenCount} broken, {hidden} panes hidden here, {_panesBroken} shatter events on joining");
            Log($"CLIENT STATE on joining: panes broken {glass.BrokenCount}, dummies {DummyStates()}, ping {s.Me.TimeManager.RoundTripTime} ms");
            Check("client has both guns", holder.WeaponInSlot(0) != null && holder.WeaponInSlot(1) != null, $"in hand {Name(holder.ActiveWeapon)} with {holder.AmmoInMagazine} rounds");

            // One pistol shot at a dummy the host has not touched, through a near pane and a far pane.
            Health far1 = FindDummy("Dummy Far 1");
            int brokenBefore = glass.BrokenCount;
            float healthBefore = far1.Current;
            yield return Aim(Chest(far1));
            holder.PullTrigger();
            yield return Frames(1);
            Check("client sees its own shot at once", _fired == 1 && holder.AmmoInMagazine == holder.ActiveWeapon.MagazineSize - 1, $"fired {_fired} and ammo {holder.AmmoInMagazine} one frame after the press, before any answer from the host");
            yield return Seconds(0.6f);
            Check("client's shot: the host confirmed it and the damage shows here", _hitConfirmed == 1 && Mathf.Approximately(far1.Current, healthBefore - holder.ActiveWeapon.Damage),
                $"hit markers {_hitConfirmed} (damage {_lastHitDamage}), dummy health here {healthBefore} -> {far1.Current}");
            Check("client's shot: the panes it crossed are broken here", glass.BrokenCount > brokenBefore && HiddenPanes() == glass.BrokenCount && _panesBroken == glass.BrokenCount - brokenBefore,
                $"broken {brokenBefore} -> {glass.BrokenCount}, hidden here {HiddenPanes()}, shatter events here {_panesBroken}");
        }

        /// <summary>The host kills this player out through a window; this copy must hear its death and respawn and come back whole.</summary>
        static IEnumerator ClientDeathCheck(Subject s, Action<int, string> stateAtEnd)
        {
            Health.AnyDied += CountDeath;
            Health.AnyRespawned += CountRespawn;
            yield return Until(() => HasFlagFile("host_kill_done"), 120f, "the host's window kill");
            yield return Seconds(1f);

            Health health = s.Health;
            bool whole = !health.IsDead && Mathf.Approximately(health.Current, health.Max) && s.Holder.AmmoInMagazine == s.Holder.ActiveWeapon.MagazineSize;
            Check("client heard its own death and respawn, and is whole again", _deaths == 1 && _respawns == 1 && whole,
                $"deaths {_deaths}, respawns {_respawns}, health {health.Current}, ammo {s.Holder.AmmoInMagazine}, standing at {s.Me.transform.position}");
            Log($"CLIENT STATE after the window kill: panes broken {s.Glass.BrokenCount}, dummies {DummyStates()}");
            stateAtEnd(s.Glass.BrokenCount, DummyStates());
            Health.AnyDied -= CountDeath;
            Health.AnyRespawned -= CountRespawn;
        }

        /// <summary>Leave and join again: a late joiner must see every break and every dummy as the host has them.</summary>
        static IEnumerator ClientRejoinCheck(SteamLobby lobby, int brokenAtLeave, string dummiesAtLeave)
        {
            lobby.Leave();
            yield return Seconds(2f);
            Check("after leaving, this copy's panes are whole again", HiddenPanes() == 0, $"{HiddenPanes()} hidden");
            _panesBroken = 0;
            lobby.JoinLocal();
            yield return Until(() => PlayerMotor.Local != null, 40f, "own player after rejoining");
            yield return Seconds(1.5f);

            GlassRegistry glass = GlassRegistry.Instance;
            Check("late joiner sees every pane the match has broken, without shards", glass.BrokenCount == brokenAtLeave && HiddenPanes() == brokenAtLeave && _panesBroken == 0,
                $"registry {glass.BrokenCount} (was {brokenAtLeave} when it left), hidden here {HiddenPanes()}, shatter events {_panesBroken}");
            Check("late joiner sees the dummies as the host has them", DummyStates() == dummiesAtLeave, $"{DummyStates()}");
        }

        // ---- Combat HUD: every state, captured at 1080p and 720p ----------------------------

        static readonly Vector2Int[] HudSizes = { new(1920, 1080), new(1280, 720) };

        /// <summary>
        /// Hosts the open scene and walks the combat HUD through its states with real shots where the
        /// scene has targets (Lane_Shooting) and the host's own damage path otherwise, capturing each
        /// state at every size in HudSizes.
        /// </summary>
        static IEnumerator HudRoutine()
        {
            SteamLobby lobby = null;
            yield return Until(() => (lobby = Object.FindAnyObjectByType<SteamLobby>()) != null && lobby.Ready, 30f, "lobby ready");
            lobby.Host();
            yield return Until(() => PlayerMotor.Local != null, 60f, "own player spawned");
            yield return Seconds(2f);
            Check("this copy is the host", PlayerMotor.Local.IsServerStarted, $"server started {PlayerMotor.Local.IsServerStarted}");

            var s = new Subject { Me = PlayerMotor.Local, Glass = GlassRegistry.Instance };
            s.Holder = s.Me.GetComponent<WeaponHolder>();
            s.Health = s.Me.GetComponent<Health>();
            Listen(s.Holder);
            CombatHud hud = Object.FindAnyObjectByType<CombatHud>(FindObjectsInactive.Include);
            Check("the scene has the combat HUD", hud != null, hud != null ? hud.transform.parent.name + "/" + hud.name : "none");
            if (hud == null)
                yield break;

            // The HUD shows only with the menu closed. Headless, the cursor never locks, and the menu
            // reopens whenever the mouse is free; so close it and stop it watching for this run.
            GameMenu menu = Object.FindAnyObjectByType<GameMenu>();
            if (menu != null)
            {
                // Same step: a frame later the menu would have seen the free cursor and reopened.
                menu.Resume();
                menu.enabled = false;
            }

            yield return Seconds(0.5f);
            Check("the HUD is showing", hud.isActiveAndEnabled, $"HUD active {hud.isActiveAndEnabled}");
            int sounds = 0;
            UiSound.Played += e => { if (e == UiSoundEvent.HitMarker || e == UiSoundEvent.HitMarkerHead || e == UiSoundEvent.KillConfirm || e == UiSoundEvent.CountdownTick) sounds++; };

            HudShot("01_idle");
            Health target = Object.FindObjectsByType<Health>(FindObjectsSortMode.None).FirstOrDefault(h => h.GetComponent<PlayerMotor>() == null && h.GetComponent<MovingTarget>() == null);
            if (target != null && s.Holder.WeaponInSlot(0) != null)
                yield return HudShootingStates(s, target);
            else
                Log("NOTE  no target dummy in this scene: hit marker, kill, reload and empty states are captured in Lane_Shooting.");

            yield return HudHurtAndDownStates(s);
            Check("the HUD's sounds were asked for", sounds > 0, $"{sounds} hit, kill or countdown sounds requested");
        }

        static IEnumerator HudShootingStates(Subject s, Health target)
        {
            if (s.Holder.ActiveSlot != 0)
                s.Holder.Switch(0);
            yield return Seconds(0.6f);

            int hits = _hitConfirmed;
            yield return Aim(Chest(target));
            s.Holder.PullTrigger();
            yield return Until(() => _hitConfirmed > hits, 2f, "the host's hit confirmation");
            yield return Frames(2);
            HudShot("02_hit_marker");
            Check("the hit marker shows only after the host confirms", _hitConfirmed > hits, $"confirmations {hits} -> {_hitConfirmed}");

            int kills = _killsConfirmed;
            for (int i = 0; i < 4 && !target.IsDead; i++)
            {
                yield return Seconds(0.3f);
                yield return Aim(Chest(target));
                s.Holder.PullTrigger();
            }

            yield return Until(() => _killsConfirmed > kills, 2f, "the kill confirmation");
            yield return Frames(2);
            HudShot("03_kill_confirm");

            s.Holder.Reload();
            yield return Seconds(s.Holder.ActiveWeapon.ReloadSeconds * 0.5f);
            HudShot("04_reloading");
            yield return Seconds(s.Holder.ActiveWeapon.ReloadSeconds);

            float emptyBy = Time.time + 6f;
            while (s.Holder.AmmoInMagazine > 0 && Time.time < emptyBy)
            {
                yield return Aim(new Vector3(-2f, SlabTop + 1.5f, 16f));
                s.Holder.PullTrigger();
                yield return Seconds(0.2f);
            }

            yield return Seconds(0.4f);
            s.Holder.PullTrigger();
            yield return Frames(3);
            HudShot("05_empty_dry_fire");
            s.Holder.Reload();
            yield return Seconds(s.Holder.ActiveWeapon.ReloadSeconds + 0.5f);
        }

        static IEnumerator HudHurtAndDownStates(Subject s)
        {
            // A hit from the player's right, through the host's own damage path.
            Vector3 chest = s.Me.transform.position + Vector3.up * 1.2f;
            Vector3 fromRight = -s.Me.transform.right;
            s.Health.ApplyHit(38f, chest, fromRight, 1, Vector3.zero, Vector3.zero, 0f);
            yield return Seconds(0.15f);
            HudShot("06_hurt_from_right");
            yield return Seconds(1.5f);
            HudShot("07_hurt_settled");

            s.Health.ApplyDamage(999f, chest, Vector3.back, 1);
            yield return Seconds(0.6f);
            HudShot("08_down");
            yield return Until(() => !s.Health.IsDead, 8f, "respawn");
            yield return Seconds(_hudLinger);
            HudShot("09_back_full_health");
        }

        const float _hudLinger = 3.5f;

        /// <summary>Renders the player's view with the HUD at every capture size.</summary>
        static void HudShot(string name)
        {
            Canvas canvas = Object.FindAnyObjectByType<CombatHud>(FindObjectsInactive.Include)?.GetComponentInParent<Canvas>()?.rootCanvas;
            Camera camera = Camera.main;
            if (canvas == null || camera == null)
                return;

            RenderMode mode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float plane = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.05f;

            foreach (Vector2Int size in HudSizes)
            {
                RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24);
                RenderTexture previousTarget = camera.targetTexture;
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                File.WriteAllBytes(Path.Combine(_out, $"hud_{name}_{size.y}p.png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }

            canvas.renderMode = mode;
            canvas.worldCamera = previousCamera;
            canvas.planeDistance = plane;
        }

        // ---- Self-check helpers --------------------------------------------------------------

        static void Listen(WeaponHolder holder)
        {
            _fired = _dryFired = _reloadStarted = _reloadFinished = _reloadCancelled = _hitConfirmed = _killsConfirmed = 0;
            holder.Fired += shot => _fired++;
            holder.DryFired += () => _dryFired++;
            holder.ReloadStarted += seconds => _reloadStarted++;
            holder.ReloadFinished += () => _reloadFinished++;
            holder.ReloadCancelled += () => _reloadCancelled++;
            holder.HitConfirmed += (point, damage, killed, precision) =>
            {
                _hitConfirmed++;
                _lastHitDamage = damage;
                if (killed)
                    _killsConfirmed++;
            };

            if (_role != "client")
            {
                _panesBroken = 0;
                GlassPane.AnyBroken += CountPane;
                Health.AnyRespawned += CountRespawn;
            }
        }

        static void CountPane(GlassPane pane, Vector3 direction) => _panesBroken++;
        static void CountDeath(Health health, DamageInfo info) => _deaths += health.IsOwner ? 1 : 0;
        static void CountRespawn(Health health) => _respawns += health.IsOwner ? 1 : 0;

        static IEnumerator Until(Func<bool> done, float timeoutSeconds, string what)
        {
            double deadline = EditorApplication.timeSinceStartup + timeoutSeconds;
            while (!done())
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Timed out waiting for " + what);
                yield return null;
            }
        }

        static IEnumerator Seconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
                yield return null;
        }

        static IEnumerator Frames(int frames)
        {
            int until = Time.frameCount + frames;
            while (Time.frameCount < until)
                yield return null;
        }

        /// <summary>Turns the player's own look toward a point, the way the mouse would, and waits for the camera to follow.</summary>
        static IEnumerator Aim(Vector3 point)
        {
            SetAim(point);
            yield return Frames(2);
        }

        static readonly FieldInfo ViewTickField = typeof(WeaponHolder).GetField("_viewTick", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Points the camera at once as well as the look, so a shot fired before the next frame goes there.</summary>
        static void SetAimNow(Vector3 point)
        {
            SetAim(point);
            Transform cam = PlayerCamera.Instance.transform;
            cam.rotation = Quaternion.LookRotation(point - cam.position);
        }

        /// <summary>For measuring lag allowance: the tick this copy says it is showing, and where it draws the runner.</summary>
        static void LogDrawn(WeaponHolder holder, Health runner)
        {
            Vector3 p = runner.transform.position;
            File.AppendAllText(Path.Combine(_out, _role + "_drawn.txt"), $"{runner.name}#{runner.OwnerId}\t{ViewTickField.GetValue(holder)}\t{holder.TimeManager.Tick}\t{p.x:0.000}\n");
        }

        /// <summary>Host only: every tick, where each runner really is.</summary>
        static void LogRunnerTicks()
        {
            var lines = new System.Text.StringBuilder();
            foreach (Health health in Health.All)
            {
                if (health.GetComponent<MovingTarget>() != null || health.GetComponent<PlayerMotor>() != null)
                    lines.Append($"{health.name}#{health.OwnerId}\t{health.TimeManager.Tick}\t{health.transform.position.x:0.000}\n");
            }

            File.AppendAllText(Path.Combine(_out, "host_ticks.txt"), lines.ToString());
        }

        static void SetAim(Vector3 point)
        {
            PlayerCamera cam = PlayerCamera.Instance;
            Vector3 direction = (point - cam.transform.position).normalized;
            YawField.SetValue(cam, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
            PitchField.SetValue(cam, -Mathf.Asin(direction.y) * Mathf.Rad2Deg);
        }

        static void Teleport(PlayerMotor player, Vector3 position)
        {
            var body = player.GetComponent<Rigidbody>();
            body.position = position;
            body.linearVelocity = Vector3.zero;
            player.transform.position = position;
        }

        static PlayerMotor OtherPlayer(PlayerMotor me)
        {
            foreach (PlayerMotor motor in Object.FindObjectsByType<PlayerMotor>(FindObjectsSortMode.None))
            {
                if (motor != me)
                    return motor;
            }

            return null;
        }

        static Health FindDummy(string name) => GameObject.Find(name).GetComponent<Health>();
        static Vector3 Chest(Health body) => body.transform.position + Vector3.up * 1.1f;
        static string Name(WeaponDefinition weapon) => weapon != null ? weapon.DisplayName : "nothing";

        static string DummyStates()
        {
            var parts = new List<string>();
            foreach (Health health in Health.All)
            {
                if (health.GetComponent<PlayerMotor>() == null)
                    parts.Add($"{health.name.Replace("Dummy ", "")}={health.Current:0}{(health.IsDead ? " dead" : "")}");
            }

            parts.Sort(string.CompareOrdinal);
            return string.Join(", ", parts);
        }

        static int HiddenPanes()
        {
            int hidden = 0;
            foreach (GlassPane pane in Object.FindObjectsByType<GlassPane>(FindObjectsSortMode.None))
                hidden += pane.IsBroken && !pane.GetComponent<Renderer>().enabled && !pane.GetComponent<Collider>().enabled ? 1 : 0;
            return hidden;
        }

        static GlassPane FirstPane(bool broken)
        {
            foreach (GlassPane pane in Object.FindObjectsByType<GlassPane>(FindObjectsSortMode.None))
            {
                if (pane.IsBroken == broken)
                    return pane;
            }

            return null;
        }

        static GlassPane PaneNear(Vector3 point)
        {
            GlassPane nearest = null;
            float best = float.MaxValue;
            foreach (GlassPane pane in Object.FindObjectsByType<GlassPane>(FindObjectsSortMode.None))
            {
                float distance = (pane.Centre - point).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = pane;
                }
            }

            return nearest;
        }

        static void Check(string what, bool passed, string detail)
        {
            if (!passed)
                _failures++;
            Log($"{(passed ? "PASS" : "FAIL")}  {what}: {detail}");
        }

        static void Log(string line)
        {
            Debug.Log("[Shooting Self-Check] " + line);
            if (!string.IsNullOrEmpty(_out))
                File.AppendAllText(Path.Combine(_out, _role + ".log"), line + "\n");
        }

        static void Flag(string name) => File.WriteAllText(Path.Combine(_out, name + ".flag"), DateTime.Now.ToString("O"));
        static bool HasFlagFile(string name) => File.Exists(Path.Combine(_out, name + ".flag"));

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }

            return null;
        }

        static bool HasFlag(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        // ---- Pictures ------------------------------------------------------------------------

        static Camera MakeSideCamera()
        {
            var go = new GameObject("Self-check side camera");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 55f;
            go.transform.position = new Vector3(-17f, SlabTop + 3.5f, -1f);
            go.transform.LookAt(new Vector3(-4f, SlabTop + 0.8f, -3f));
            return cam;
        }

        /// <summary>Renders the player's own view (or another camera) to a PNG in the output folder.</summary>
        static void Capture(string name, Camera camera = null)
        {
            byte[] png = Render(camera != null ? camera : Camera.main, 1280, 720, false);
            if (png != null)
                File.WriteAllBytes(Path.Combine(_out, $"{_role}_{name}.png"), png);
        }

        static void StartClip(string name, Camera camera = null)
        {
            _clipCamera = camera;
            _clipFrame = 0;
            _clipName = _role + "_" + name;
            Directory.CreateDirectory(Path.Combine(_out, _clipName));
        }

        static void StopClip()
        {
            Log($"CLIP {_clipName}: {_clipFrame} frames");
            _clipName = null;
        }

        /// <summary>While a clip is on, saves every game frame as a JPG; ffmpeg turns the folder into a video afterwards.</summary>
        static void RecordClipFrame()
        {
            if (_clipName == null || Time.frameCount == _lastClipGameFrame)
                return;

            _lastClipGameFrame = Time.frameCount;
            byte[] jpg = Render(_clipCamera != null ? _clipCamera : Camera.main, 960, 540, true);
            if (jpg != null)
                File.WriteAllBytes(Path.Combine(_out, _clipName, $"{_clipFrame++:0000}_{Time.time:0000.000}.jpg"), jpg);
        }

        static byte[] Render(Camera camera, int width, int height, bool jpg)
        {
            if (camera == null)
                return null;

            RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);

            byte[] bytes = jpg ? image.EncodeToJPG(80) : image.EncodeToPNG();
            Object.DestroyImmediate(image);
            return bytes;
        }
    }
}
