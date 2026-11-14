using CloseTheDeal.UI;
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
            if (!EditorUtility.DisplayDialog("Replace UI", "This deletes KitUI in the open scene and adds a fresh one from the kit prefabs. Changes made to KitUI in this scene are lost; the prefabs and other objects are untouched.", "Replace", "Cancel"))
                return;
            if (Replace())
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Shell variant: rebuilds the kit's untouched assets, then Replace UI in Lane_Integration.unity, no question asked.</summary>
        public static void ReplaceFromCommandLine()
        {
            BuildAssets(true);
            EditorSceneManager.OpenScene(IntegrationScene, OpenSceneMode.Single);
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
                Object.DestroyImmediate(old.gameObject);
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
            bool added = EnsureEventSystem();
            added |= EnsureCrosshair(menu);
            added |= EnsureMenuFade(menu);

            UiRoot existing = Object.FindAnyObjectByType<UiRoot>(FindObjectsInactive.Include);
            if (existing != null)
                return RelinkMenu(existing, menu) | added;

            BuildCanvas(menu);
            Debug.Log("[UI] Added the UI kit (title, settings, quit confirm, focus marks) to the scene.");
            return true;
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
        }

        static void BuildCanvas(GameMenu menu)
        {
            var root = Instance<UiRoot>(UiKitScreens.Canvas, null, CanvasName);
            Set(root, "_menu", menu);
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
