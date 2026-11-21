using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static CloseTheDeal.Editor.UI.UiKitAssets;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// Adds the UI kit to a scene: builds the tokens asset (once, then never overwritten), the kit's sprites and
    /// prefabs (each left alone once a person edits it), and in the open scene a "KitUI" canvas with the screen
    /// stack, the title, settings and confirm screens and the focus marks, plus the kit crosshair in the game
    /// menu's HUD. It only adds: an existing KitUI or kit crosshair is left as it is. Guide: docs/systems/UI_KIT.md.
    ///
    /// Menu: Close the Deal > Lanes > Set Up UI (the open scene; save it yourself).
    /// Shell: -executeMethod CloseTheDeal.Editor.UI.UiKitSetup.SetUpFromCommandLine (Lane_Integration.unity, saved).
    /// Set Up Scene, Set Up Integration and Make Open Scene Playable reach it through GreyboxUiSetup.Ensure.
    /// </summary>
    public static class UiKitSetup
    {
        internal const string IntegrationScene = "Assets/_Project/Scenes/Lanes/Lane_Integration.unity";
        const string CanvasName = "KitUI";
        const string CrosshairName = "KitCrosshair";

        [MenuItem("Close the Deal/Lanes/Set Up UI")]
        public static void SetUpFromMenu()
        {
            BuildAssets();
            if (AddToOpenScene())
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// Replaces the open scene's KitUI with a fresh copy of the kit canvas prefab, after asking (it throws away
        /// anything changed on that KitUI in the scene). For after a kit prefab was rebuilt.
        /// </summary>
        [MenuItem("Close the Deal/Lanes/Replace UI In Open Scene")]
        public static void ReplaceFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Replace UI", "This rebuilds the kit's prefabs (any a person edited are skipped), deletes KitUI in the open scene and adds a fresh one. Changes made to KitUI in this scene are lost; the match lane's section in the in-game menu is carried across; other objects are untouched.", "Replace", "Cancel"))
                return;
            ParkMatchSection(); // out of KitUI before its prefabs are rebuilt under it
            BuildAssets(true); // the screens are rebuilt only when asked: this is the ask
            if (Replace())
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Shell variant: rebuilds the kit's untouched assets, then Replace UI in Lane_Integration.unity, no question asked.</summary>
        public static void ReplaceFromCommandLine()
        {
            EditorSceneManager.OpenScene(IntegrationScene, OpenSceneMode.Single);
            ParkMatchSection(); // out of KitUI before its prefabs are rebuilt under it
            BuildAssets(true);
            if (!Replace())
            {
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        static bool Replace()
        {
            if (Object.FindAnyObjectByType<GameMenu>(FindObjectsInactive.Include) == null)
            {
                Debug.LogError("[UI] No game menu in the open scene, so KitUI was left as it is.");
                return false;
            }

            UiRoot old = Object.FindAnyObjectByType<UiRoot>(FindObjectsInactive.Include);
            if (old != null)
            {
                ParkMatchSection();
                Object.DestroyImmediate(old.gameObject);
            }

            return AddToOpenScene();
        }

        /// <summary>Shell entry: builds the assets and adds the kit to Lane_Integration.unity, then saves it.</summary>
        public static void SetUpFromCommandLine()
        {
            BuildAssets();
            EditorSceneManager.OpenScene(IntegrationScene, OpenSceneMode.Single);
            if (AddToOpenScene())
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Tokens, sprites, component and screen prefabs; never overwrites a file a person edited. Screen prefabs nest
        /// the component prefabs, and Unity gives a rebuilt nest new internal ids (a large diff for no change), so the
        /// screens are rebuilt only when missing unless asked (Replace UI does).
        /// </summary>
        public static void BuildAssets(bool rebuildScreens = false)
        {
            UiTokens tokens = EnsureTokens();
            UiKitArt.Ensure(tokens);
            UiKitComponents.Build();
            UiKitScreens.Build(rebuildScreens);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Called by GreyboxUiSetup.Ensure whenever a scene gets its game menu, so every playable scene has the kit.</summary>
        public static void EnsureInOpenScene()
        {
            EnsureAssets();
            AddToOpenScene();
        }

        /// <summary>
        /// Builds the kit's assets only when they are missing: other lanes' scene setups reach this through
        /// GreyboxUiSetup.Ensure, and shouldn't rebuild the kit as a side effect. Set Up UI refreshes it.
        /// </summary>
        static void EnsureAssets()
        {
            LoadPrefabsIfNeeded();
            if (Tokens == null || UiKitScreens.Canvas == null || UiKitComponents.Crosshair == null)
                BuildAssets();
        }

        /// <summary>Adds what is missing to the open scene. True when something was added.</summary>
        public static bool AddToOpenScene()
        {
            GameMenu menu = Object.FindAnyObjectByType<GameMenu>(FindObjectsInactive.Include);
            if (menu == null)
            {
                Debug.LogError("[UI] No game menu in the open scene. Run Close the Deal > Greybox > Set Up Scene (or the lane's own setup) first.");
                return false;
            }

            LoadPrefabsIfNeeded();
            ReportTitleSky();
            bool added = EnsureEventSystem();
            added |= EnsureCrosshair(menu);
            // The movement lane's feed (speed, spread, hits) on the kit crosshair; Lane_Integration lacked it (review #563).
            added |= CloseTheDeal.Editor.Lanes.MovementLaneSetup.EnsureCrosshairFeed();
            added |= EnsureMenuFade(menu);
            added |= EnsureMenuHint(menu);
            added |= EnsureNetworkReadout(menu);

            UiRoot existing = Object.FindAnyObjectByType<UiRoot>(FindObjectsInactive.Include);
            if (existing != null)
                return RelinkMenu(existing, menu) | MoveMatchSection(existing, menu) | added;

            UiRoot root = BuildCanvas(menu);
            MoveMatchSection(root, menu);
            Debug.Log("[UI] Added the UI kit (title, settings, quit confirm, in-game menu, focus marks) to the scene.");
            return true;
        }

        // ---- The match lane's section in the in-game menu ---------------------------------------------------

        /// <summary>Agreed with lane 4 (AgentHub #529/#530): their tool builds into this path once it exists.</summary>
        internal const string MatchSlotPath = "InGameMenu/Content/MatchSlot";
        const string MatchSectionName = "MatchSection";

        /// <summary>
        /// The match lane's section (where the match stands, Start / Rematch) was built into the playtest card, which
        /// the kit's in-game menu replaces: the same object moves, untouched, into the menu's MatchSlot. Nothing is
        /// rebuilt; its MatchMenuPanel keeps every reference. Only when the slot is empty.
        /// </summary>
        static bool MoveMatchSection(UiRoot root, GameMenu menu)
        {
            Transform slot = root.transform.Find(MatchSlotPath);
            if (slot == null || slot.childCount > 0)
                return false;
            Transform section = menu.transform.Find("Menu/Card/" + MatchSectionName) ?? menu.transform.Find(MatchSectionName);
            if (section == null)
                return false;
            section.SetParent(slot, false);
            Debug.Log("[UI] Moved the match lane's section from the playtest card into the in-game menu (" + MatchSlotPath + ").");
            return true;
        }

        /// <summary>
        /// Before KitUI's prefabs are rebuilt or KitUI is replaced, the match lane's section leaves it (back to the old
        /// card), so a rebuilt slot can't orphan it and the fresh KitUI takes it back.
        /// </summary>
        static void ParkMatchSection()
        {
            UiRoot old = Object.FindAnyObjectByType<UiRoot>(FindObjectsInactive.Include);
            GameMenu menu = Object.FindAnyObjectByType<GameMenu>(FindObjectsInactive.Include);
            Transform slot = old != null ? old.transform.Find(MatchSlotPath) : null;
            if (slot == null || menu == null)
                return;
            Transform card = menu.transform.Find("Menu/Card");
            for (int i = slot.childCount - 1; i >= 0; i--)
                slot.GetChild(i).SetParent(card != null ? card : menu.transform, false);
        }

        // ---- HUD pieces on the kit ---------------------------------------------------------------------

        /// <summary>The "Esc · Menu" hint as the kit's key hint (Esc on keys, Menu / Options on a pad); the greybox chip is switched off.</summary>
        static bool EnsureMenuHint(GameMenu menu)
        {
            Transform hud = menu.transform.Find("Hud");
            if (hud == null || hud.Find(MenuHintName) != null || UiKitComponents.KeyHint == null)
                return false;

            var hint = Instance<UiKeyHint>(UiKitComponents.KeyHint, hud, MenuHintName);
            Pin((RectTransform)hint.transform, Vector2.up, new Vector2(24f, -24f), new Vector2(136f, 40f));
            Img((RectTransform)hint.transform, AssetDatabase.LoadAssetAtPath<Sprite>(UiKitArt.Folder + "/Kit_Round16.png"), Tokens.Paper);
            SetLabelText(hint.transform, "Label", "Menu");
            SetLabelText(hint.transform, "Key", "Esc");
            SetString(hint, "_keyboardText", "Esc");
            SetString(hint, "_padControl", "start");
            SwitchOff(hud, "MenuHint");
            return true;
        }

        /// <summary>
        /// The network readout on a kit plate (navy on white), with its own readout component; the greybox chip and its
        /// component are switched off, not deleted or rewired. Show network stats still hides it (the plate is the
        /// text's parent).
        /// </summary>
        static bool EnsureNetworkReadout(GameMenu menu)
        {
            Transform hud = menu.transform.Find("Hud");
            if (hud == null || hud.Find(ReadoutName) != null || UiKitComponents.Plate == null)
                return false;

            // The readout hides its text's parent (the plate), so it lives on a holder above the plate: hidden, it can
            // still hear Show network stats come back on.
            RectTransform holder = Node(hud, ReadoutName);
            Pin(holder, Vector2.up, new Vector2(24f, -90f), new Vector2(500f, 84f));
            var plate = Instance<RectTransform>(UiKitComponents.Plate, holder, "Plate");
            Stretch(plate);
            TextMeshProUGUI text = Label(plate, "Text", 16f, 10f, 468f, 64f, string.Empty, Tokens.Caption, Tokens.Navy, TextAlignmentOptions.TopLeft, true);
            text.fontSize = 15f;
            var readout = holder.gameObject.AddComponent<PredictionDebugHud>();
            Set(readout, "_text", text);

            foreach (PredictionDebugHud old in menu.GetComponents<PredictionDebugHud>())
            {
                old.enabled = false;
                EditorUtility.SetDirty(old);
            }

            SwitchOff(hud, "NetworkReadout");
            return true;
        }

        const string MenuHintName = "KitMenuHint";
        const string ReadoutName = "KitNetworkReadout";

        static void Pin(RectTransform rect, Vector2 corner, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        static void SetLabelText(Transform under, string child, string text)
        {
            var label = under.Find(child)?.GetComponent<TMP_Text>();
            if (label != null)
                label.text = text;
        }

        static void SwitchOff(Transform hud, string name)
        {
            Transform old = hud.Find(name);
            if (old != null)
                old.gameObject.SetActive(false);
        }

        /// <summary>
        /// The title shows lane 8's live sky by default (TitleSky adds itself at run time and draws the scene's own sky
        /// material). A scene with no sky material gets the flat fallback instead; say so, with the fix.
        /// </summary>
        static void ReportTitleSky()
        {
            if (RenderSettings.skybox == null)
                Debug.LogWarning("[UI] This scene has no sky material, so the title shows the flat fallback sky instead of the live one. Close the Deal > Art > Apply Office Look gives it the game's sky.");
            else
                Debug.Log("[UI] The title will show this scene's sky (" + RenderSettings.skybox.name + ") live behind its window frame.");
        }

        /// <summary>Loads what earlier runs built. Unity's null (a deleted asset) counts as missing, so it reloads.</summary>
        static void LoadPrefabsIfNeeded()
        {
            if (Tokens == null)
                Tokens = AssetDatabase.LoadAssetAtPath<UiTokens>(TokensPath);
            if (UiKitScreens.Canvas == null)
                UiKitScreens.Canvas = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenFolder + "/KitCanvas.prefab");
            if (UiKitComponents.Crosshair == null)
                UiKitComponents.Crosshair = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/Crosshair.prefab");
            if (UiKitComponents.KeyHint == null)
                UiKitComponents.KeyHint = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/KeyHint.prefab");
            if (UiKitComponents.Plate == null)
                UiKitComponents.Plate = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/Plate.prefab");
        }

        static UiRoot BuildCanvas(GameMenu menu)
        {
            var root = Instance<UiRoot>(UiKitScreens.Canvas, null, CanvasName);
            Set(root, "_menu", menu);
            return root;
        }

        /// <summary>
        /// An existing kit is left as it is, except that a game menu rebuilt by Rebuild Game UI is a new object:
        /// a kit still pointing at the old (deleted) one is pointed at the new one.
        /// </summary>
        static bool RelinkMenu(UiRoot root, GameMenu menu)
        {
            var so = new SerializedObject(root);
            if (so.FindProperty("_menu").objectReferenceValue != null)
            {
                Debug.Log("[UI] The scene already has the UI kit; left it as it is.");
                return false;
            }

            Set(root, "_menu", menu);
            Debug.Log("[UI] Pointed the UI kit at the rebuilt game menu.");
            return true;
        }

        /// <summary>The kit crosshair replaces the greybox squares (which are switched off, not deleted).</summary>
        static bool EnsureCrosshair(GameMenu menu)
        {
            Transform hud = menu.transform.Find("Hud");
            if (hud == null || hud.GetComponentInChildren<UiCrosshair>(true) != null || UiKitComponents.Crosshair == null)
                return false;

            var crosshair = Instance<RectTransform>(UiKitComponents.Crosshair, hud, CrosshairName);
            crosshair.anchorMin = crosshair.anchorMax = crosshair.pivot = new Vector2(0.5f, 0.5f);
            crosshair.anchoredPosition = Vector2.zero;
            crosshair.SetAsFirstSibling();
            foreach (string old in new[] { "Crosshair", "CrosshairRing" })
            {
                Transform t = hud.Find(old);
                if (t != null)
                    t.gameObject.SetActive(false);
            }

            return true;
        }

        /// <summary>The playtest card fades in instead of cutting in, until the in-game menu slice replaces it.</summary>
        static bool EnsureMenuFade(GameMenu menu)
        {
            Transform card = menu.transform.Find("Menu");
            if (card == null || card.GetComponent<UiFadeIn>() != null)
                return false;
            var fade = card.gameObject.AddComponent<UiFadeIn>();
            Set(fade, "_tokens", Tokens);
            return true;
        }

        static bool EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
                return false;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
            return true;
        }
    }
}
