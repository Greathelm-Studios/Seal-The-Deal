using System.Collections.Generic;
using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloseTheDeal.Editor.UI.UiKitAssets;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>Builds TitleScreen, SettingsScreen, ConfirmDialog, SessionPanel, ProblemPanel, InGameMenu, and KitCanvas prefabs.</summary>
    internal static class UiKitScreens
    {
        internal static GameObject Title, Settings, Confirm, Session, Problem, GameMenu, Canvas;

        const float SillY = 664f;
        const float WindowInset = 112f, WindowTop = 88f, WindowBottom = 36f;

        internal static void Build(bool rebuild)
        {
            Title = Ensure(ScreenFolder + "/TitleScreen.prefab", BuildTitle, rebuild);
            Settings = Ensure(ScreenFolder + "/SettingsScreen.prefab", UiKitSettingsBuilder.Build, rebuild);
            Confirm = Ensure(ScreenFolder + "/ConfirmDialog.prefab", BuildConfirm, rebuild);
            Session = Ensure(ScreenFolder + "/SessionPanel.prefab", UiKitSessionBuilder.BuildSession, rebuild);
            Problem = Ensure(ScreenFolder + "/ProblemPanel.prefab", UiKitSessionBuilder.BuildProblem, rebuild);
            GameMenu = Ensure(ScreenFolder + "/InGameMenu.prefab", UiKitGameMenuBuilder.Build, rebuild);
            Canvas = Ensure(ScreenFolder + "/KitCanvas.prefab", BuildCanvas, rebuild);
        }

        /// <summary>
        /// Rebuilds only TitleScreen.prefab from BuildTitle, with the kit's pieces as they are (not rebuilt). The kit
        /// canvas and Lane_Integration nest it, so they pick the change up. For working on the title in small steps.
        /// </summary>
        [MenuItem("Seal the Deal/UI/Rebuild Title Screen")]
        static void RebuildTitleScreen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[UI] Stop Play mode first, then rebuild the title.");
                return;
            }

            UiTokens tokens = EnsureTokens();
            UiKitArt.Ensure(tokens);
            UiKitComponents.Primary = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/Button_Primary.prefab");
            UiKitComponents.Secondary = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/Button_Secondary.prefab");
            UiKitComponents.Quiet = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/Button_Quiet.prefab");
            UiKitComponents.StatusLine = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/StatusLine.prefab");
            string path = ScreenFolder + "/TitleScreen.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                Greybox.GeneratedAssetGuard.MarkGenerated(existing);   // its last edit was the font tool's; the builder now makes the same faces
            Title = Ensure(path, BuildTitle, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[UI] Title screen rebuilt from UiKitScreens.BuildTitle.");
        }

        static GameObject Ensure(string path, System.Func<GameObject> build, bool rebuild)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return existing != null && !rebuild ? existing : SavePrefab(build(), path);
        }

        static GameObject BuildCanvas()
        {
            var go = new GameObject("KitUI", typeof(RectTransform)) { layer = 5 };
            var canvas = go.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            var stack = go.AddComponent<UiScreenStack>();

            var title = Instance<TitleScreen>(Title, go.transform, "TitleScreen");
            var settings = Instance<SettingsScreen>(Settings, go.transform, "SettingsScreen");
            var quit = Instance<UiConfirmDialog>(Confirm, go.transform, "QuitDialog");
            var session = Instance<SessionPanel>(Session, go.transform, "SessionPanel");
            var problem = Instance<ProblemPanel>(Problem, go.transform, "ProblemPanel");
            var gameMenu = Instance<InGameMenuScreen>(GameMenu, go.transform, "InGameMenu");
            session.gameObject.SetActive(false);
            problem.gameObject.SetActive(false);
            gameMenu.gameObject.SetActive(false);
            Set(gameMenu, "_settingsScreen", settings);
            Set(gameMenu, "_confirm", quit);
            Instance<UiFocusFrame>(UiKitComponents.FocusFrame, go.transform, "FocusFrame");
            title.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);
            quit.gameObject.SetActive(false);
            Set(title, "_settingsScreen", settings);
            Set(title, "_quitDialog", quit);
            Set(settings.GetComponentInChildren<VideoPage>(true), "_dialog", quit);
            Set(settings.GetComponentInChildren<ControlsPage>(true), "_dialog", quit);

            var root = go.AddComponent<UiRoot>();
            Set(root, "_tokens", Tokens);
            Set(root, "_stack", stack);
            Set(root, "_title", title);
            Set(root, "_session", session);
            Set(root, "_problem", problem);
            Set(root, "_gameMenu", gameMenu);
            return go;
        }

        internal static RectTransform ScreenRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var root = (RectTransform)go.transform;
            Stretch(root);
            go.AddComponent<CanvasGroup>();
            return root;
        }

        internal static void PaperField(RectTransform root)
        {
            Img(Fill(root, "Paper"), UiKitArt.White, Tokens.Paper, raycast: true);
            Img(Fill(root, "Grid"), UiKitArt.Grid, Tokens.Grid, Image.Type.Tiled);
        }

        internal static void WireScreen(UiScreen screen, RectTransform content, List<CanvasGroup> reveal, Selectable first)
        {
            Set(screen, "_tokens", Tokens);
            Set(screen, "_content", content);
            SetArray(screen, "_reveal", reveal.ToArray());
            Set(screen, "_firstFocus", first);
            if (content.TryGetComponent(out CanvasGroup group))
                Set(screen, "_contentGroup", group);
        }

        internal static T Place<T>(GameObject prefab, Transform parent, string name, float x, float y) where T : Component
        {
            var c = Instance<T>(prefab, parent, name);
            var rect = (RectTransform)c.transform;
            Vector2 size = rect.sizeDelta;
            UiKitAssets.Place(rect, x, y, size.x, size.y);
            return c;
        }

        internal static void SetLabel(Component control, string text)
        {
            TMP_Text label = control.transform.Find("Plate/Label")?.GetComponent<TMP_Text>();
            if (label != null)
                label.text = text;
        }

        internal static void SetText(Component part, string child, string text)
        {
            TMP_Text label = part.transform.Find(child)?.GetComponent<TMP_Text>();
            if (label != null)
                label.text = text;
        }

        static GameObject BuildTitle()
        {
            UiTokens t = Tokens;
            RectTransform root = ScreenRoot("TitleScreen");
            PaperField(root);
            SunOnPaper(root);
            RectTransform sky = SkyWindow(root, out RectTransform slot, out GameObject staticSky);
            RectTransform frame = DesignFrame(root, "Content");
            frame.gameObject.AddComponent<CanvasGroup>();
            var reveal = new List<CanvasGroup>();

            RectTransform lockup = RevealLine(frame, "Lockup");
            reveal.Add(lockup.GetComponent<CanvasGroup>());
            // Refs 08 and 05: one large thin title and a small line under it, low on the white. Nothing else in the lockup.
            // The box is taller than the type: Saira's line is taller than its capitals, and TMP hides a line that
            // doesn't fit its box.
            Label(lockup, "Title", 112f, 724f, 700f, 96f, "SEAL THE DEAL", t.Title, t.Navy);
            Label(lockup, "Terms", 112f, 818f, 380f, 28f, "Terms and Conditions Apply.", t.Body, t.Ink2);
            TextMeshProUGUI version = Label(lockup, "Version", 552f, 818f, 200f, 28f, "v0.1.0 · Early Access", t.Caption, t.Ink3, TextAlignmentOptions.MidlineRight);

            RectTransform modeLine = RevealLine(frame, "Mode");
            reveal.Add(modeLine.GetComponent<CanvasGroup>());
            // One short connection line (ref 08's small text block). No section labels, no key hints: it's a menu.
            TextMeshProUGUI mode = Label(modeLine, "ModeText", 112f, 872f, 640f, 28f, "Starting...", t.Body, t.Navy);

            // The status line stays for what the player must know (busy, a refusal); TitleScreen hides it when idle.
            RectTransform statusGroup = RevealLine(frame, "Status");
            reveal.Add(statusGroup.GetComponent<CanvasGroup>());
            var status = Place<UiStatusLine>(UiKitComponents.StatusLine, statusGroup, "StatusLine", 112f, 912f);
            ((RectTransform)status.transform).sizeDelta = new Vector2(640f, 52f);
            ((RectTransform)status.transform.Find("Text")).sizeDelta = new Vector2(618f, 52f);

            RectTransform menuLine = RevealLine(frame, "Menu");
            reveal.Add(menuLine.GetComponent<CanvasGroup>());
            RectTransform column = At(menuLine, "Column", 1388f, 744f, 420f, 256f);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = t.SpaceS;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var host = Instance<UiButton>(UiKitComponents.Secondary, column, "HostGame");
            var join = Instance<UiButton>(UiKitComponents.Secondary, column, "JoinOnThisPc");
            var settings = Instance<UiButton>(UiKitComponents.Secondary, column, "Settings");
            var quit = Instance<UiButton>(UiKitComponents.Secondary, column, "QuitToDesktop");
            foreach (var row in new[] { host, join, settings, quit })
                AddV3FocusMark(row);
            SetLabel(host, "Host a game");
            SetLabel(join, "Join on this PC");
            SetLabel(settings, "Settings");
            SetLabel(quit, "Quit to desktop");
            join.Description = "Join a game hosted by another copy of Seal the Deal running on this PC.";
            settings.Description = "Controls, video, audio and accessibility. Kept on this PC.";
            quit.Description = "Close Seal the Deal. Your settings are already saved.";


            var screen = root.gameObject.AddComponent<TitleScreen>();
            WireScreen(screen, frame, reveal, host);
            Set(screen, "_wipe", sky);
            Set(screen, "_wipeGroup", sky.GetComponent<CanvasGroup>());
            Set(screen, "_backdropSlot", slot);
            Set(screen, "_staticSky", staticSky);
            Set(screen, "_host", host);
            Set(screen, "_joinLocal", join);
            Set(screen, "_settings", settings);
            Set(screen, "_quit", quit);
            Set(screen, "_mode", mode);
            Set(screen, "_status", status);
            SetArray(screen, "_versionLines", new Object[] { version });
            Set(screen, "_menuGroup", menuLine.GetComponent<CanvasGroup>());
            SetArray(screen, "_centreGroups", new Object[] { statusGroup.GetComponent<CanvasGroup>() });
            ApplyTitleArtwork(root);
            return root.gameObject;
        }

        [MenuItem("Seal the Deal/UI/Apply V3 Title Artwork")]
        public static void ApplyV3Title()
        {
            const string path = ScreenFolder + "/TitleScreen.prefab";
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            var root = (RectTransform)scope.prefabContentsRoot.transform;
            EnsureTokens();
            if (root.Find("Content/V3Artwork") != null)
            {
                var column = (RectTransform)root.Find("Content/Menu/Column");
                var screen = root.GetComponent<TitleScreen>();
                var secondary = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/Button_Secondary.prefab");
                // Earlier title rows carry artwork overrides. Replace just those four nested instances.
                foreach (var row in column.GetComponentsInChildren<UiButton>(true))
                {
                    if (row.transform.Find("Plate/V3Selected") == null)
                    {
                        AddV3FocusMark(row);
                        continue;
                    }
                    var replacement = Instance<UiButton>(secondary, column, row.name);
                    replacement.transform.SetSiblingIndex(row.transform.GetSiblingIndex());
                    AddV3FocusMark(replacement);
                    SetLabel(replacement, row.Fx.Label.text);
                    replacement.Description = row.Description;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.Fx.Label);
                    var fields = new[] { "_host", "_joinLocal", "_settings", "_quit", "_firstFocus" };
                    var serialized = new SerializedObject(screen);
                    foreach (string field in fields)
                    {
                        var property = serialized.FindProperty(field);
                        if (property.objectReferenceValue == row)
                            property.objectReferenceValue = replacement;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Object.DestroyImmediate(row.gameObject);
                }
                return;
            }
            UiKitArt.Ensure(Tokens);
            ApplyTitleArtwork(root);
        }

        internal static void AddV3FocusMark(UiButton row)
        {
            if (row.Fx.V3FocusMark != null)
                return;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KitFolder + "/Sprites/Kit_TitleV3Chevron.png");
            if (sprite == null)
                throw new System.InvalidOperationException("Missing V3 menu chevron sprite.");
            var rect = Node(row.Fx.Plate, "V3FocusMark");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(12f, 18f);
            rect.anchoredPosition = new Vector2(-22f, 0f);
            row.Fx.V3FocusMark = Img(rect, sprite, Color.white);
            row.Fx.V3FocusMark.enabled = false;
            var label = row.Fx.Label.rectTransform;
            label.offsetMax = new Vector2(-44f, label.offsetMax.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(row);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
        }

        static void ApplyTitleArtwork(RectTransform root)
        {
            var t = Tokens;
            var frame = (RectTransform)root.Find("Content");
            var artwork = AssetDatabase.LoadAssetAtPath<Sprite>(ScreenFolder + "/TitleV3Artwork.png");
            if (artwork == null)
                throw new System.InvalidOperationException("The V3 title artwork must be imported as a sprite.");
            Img(Fill(frame, "V3Artwork"), artwork, Color.white).transform.SetAsFirstSibling();
            root.Find("SunOnPaper").gameObject.SetActive(false);
            root.Find("Grid").gameObject.SetActive(false);
            root.Find("Paper").GetComponent<Image>().color = new Color32(251, 251, 252, 255);
            var sky = (RectTransform)root.Find("SkyWindow");
            sky.anchorMin = sky.anchorMax = new Vector2(0.5f, 0.5f);
            sky.pivot = new Vector2(0.5f, 1f);
            sky.sizeDelta = new Vector2(1920, SillY);
            sky.anchoredPosition = new Vector2(0, 540);

            var title = frame.Find("Lockup/Title").GetComponent<TMP_Text>();
            UiKitAssets.Place(title.rectTransform, 196, 548, 696, 146);
            title.text = "seal the deal";
            title.fontSize = 92;
            title.fontStyle = FontStyles.Normal;
            title.characterSpacing = 6;
            var terms = frame.Find("Lockup/Terms").GetComponent<TMP_Text>();
            UiKitAssets.Place(terms.rectTransform, 196, 687, 680, 45);
            terms.fontSize = 19;
            var version = frame.Find("Lockup/Version").GetComponent<TMP_Text>();
            UiKitAssets.Place(version.rectTransform, 1490, 30, 318, 38);
            version.fontSize = 13;
            UiKitAssets.Place((RectTransform)frame.Find("Mode/ModeText"), 184, 778, 960, 46);
            Box(frame.Find("Mode"), "ConnectionMark", 160, 790, 10, 10, t.Cobalt, UiKitArt.Outline1);
            UiKitAssets.Place((RectTransform)frame.Find("Status/StatusLine"), 160, 826, 640, 52);

            var menu = frame.Find("Menu");
            var caption = Label(menu, "MainMenu", 1388, 626, 420, 40, "[ main menu ]", t.Caption, t.Ink3);
            caption.fontSize = 13;
            var column = (RectTransform)menu.Find("Column");
            UiKitAssets.Place(column, 1388, 668, 420, 278);
            column.GetComponent<VerticalLayoutGroup>().spacing = 18;

            UiKitComponents.KeyHint = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/KeyHint.prefab");
            Hint(frame, "SelectHint", 112, 983, "select", "UI/Submit");
            Hint(frame, "BackHint", 274, 983, "back", "UI/Cancel");
            Hint(frame, "MoveHint", 424, 983, "move", keyboard: "↑↓", padControl: "leftStick");
        }

        internal static UiKeyHint Hint(Transform parent, string name, float x, float y, string label, string action = "",
            string keyboard = "", string padControl = "", float width = 160f)
        {
            var hint = Place<UiKeyHint>(UiKitComponents.KeyHint, parent, name, x, y);
            ((RectTransform)hint.transform).sizeDelta = new Vector2(width, 40f);
            SetText(hint, "Label", label);
            var labelRect = (RectTransform)hint.transform.Find("Label");
            labelRect.sizeDelta = new Vector2(Mathf.Max(40f, width - 58f), labelRect.sizeDelta.y);
            string usage = action == "UI/Cancel" ? "*/{Cancel}" : action == "UI/Submit" ? "*/{Submit}" : string.Empty;
            SetText(hint, "Key", keyboard.Length > 0 ? keyboard : UiGlyphs.Name(usage, UiDeviceKind.KeyboardMouse));
            SetString(hint, "_action", action);
            SetString(hint, "_keyboardText", keyboard);
            SetString(hint, "_padControl", padControl);
            return hint;
        }

        static void SunOnPaper(RectTransform root)
        {
            UiTokens t = Tokens;
            RectTransform band = Node(root, "SunOnPaper");
            band.anchorMin = new Vector2(0f, 0f);
            band.anchorMax = new Vector2(1f, 0.5f);
            band.pivot = new Vector2(0.5f, 0f);
            band.offsetMin = Vector2.zero;
            band.offsetMax = new Vector2(0f, 540f - SillY);
            band.gameObject.AddComponent<RectMask2D>();
            Img(Fill(band, "Shade"), UiKitArt.White, UiTokens.WithAlpha(t.Silver, 0.22f));
            Slab(band, "SlabA", -520f, 380f);
            Slab(band, "SlabB", 260f, 300f);
            Img(Fill(band, "Grid"), UiKitArt.Grid, UiTokens.WithAlpha(t.Grid, 0.8f), Image.Type.Tiled);
        }

        static void Slab(RectTransform band, string name, float x, float width)
        {
            RectTransform slab = Node(band, name);
            slab.anchorMin = slab.anchorMax = new Vector2(0.5f, 1f);
            slab.pivot = new Vector2(0.5f, 1f);
            slab.sizeDelta = new Vector2(width, 900f);
            slab.anchoredPosition = new Vector2(x, 40f);
            slab.localRotation = Quaternion.Euler(0f, 0f, 32f);
            Img(slab, UiKitArt.White, Tokens.Paper);
        }

        static RectTransform SkyWindow(RectTransform root, out RectTransform slot, out GameObject staticSky)
        {
            UiTokens t = Tokens;
            RectTransform sky = Node(root, "SkyWindow");
            sky.anchorMin = new Vector2(0f, 0.5f);
            sky.anchorMax = new Vector2(1f, 1f);
            sky.pivot = new Vector2(0.5f, 1f);
            sky.offsetMin = new Vector2(0f, 540f - SillY);
            sky.offsetMax = Vector2.zero;
            sky.gameObject.AddComponent<CanvasGroup>();

            // One window, no bars (Adam, 2026-11-15): the live sky inside a rounded pane (radius 20) inset from the
            // edges, as in mockup v3 and ref 02's sky windows. A soft shadow under it and a light glass rim on it give
            // the depth the frame bars used to; wider screens widen the window, so they show more sky.
            RectTransform shade = Fill(sky, "WindowShadow");
            shade.offsetMin = new Vector2(WindowInset - 24f, WindowBottom - 24f - 8f);
            shade.offsetMax = new Vector2(-WindowInset + 24f, -WindowTop + 24f - 8f);
            Img(shade, UiKitArt.Shadow20, UiTokens.WithAlpha(t.Navy, 0.14f));

            RectTransform window = Fill(sky, "Window");
            window.offsetMin = new Vector2(WindowInset, WindowBottom);
            window.offsetMax = new Vector2(-WindowInset, -WindowTop);
            Img(window, UiKitArt.Round20, Color.white);
            window.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            slot = Fill(window, "BackdropSlot");
            // Only one sky: lane 8's live one (its own traced clouds and the towers). This flat gradient shows only
            // where the live sky can't run (no sky material in the scene).
            RectTransform fallback = Fill(window, "StaticSky");
            Img(fallback, UiKitArt.Sky, Color.white);
            staticSky = fallback.gameObject;

            // The glass: a bright inner rim and a hairline outside it, drawn over the sky, not masked by it.
            RectTransform rim = Fill(sky, "GlassRim");
            rim.offsetMin = window.offsetMin;
            rim.offsetMax = window.offsetMax;
            Img(rim, UiKitArt.Ring20, UiTokens.WithAlpha(Color.white, 0.85f));
            Img(Fill(rim, "Hairline", -1f), UiKitArt.Ring20, t.Hair);
            return sky;
        }

        internal static RectTransform ConfirmationPlate(Transform frame)
        {
            const float width = 640f, height = 258f;
            var plate = Box(frame, "Pane", 1168f, 752f, width, height, UiTokens.WithAlpha(Color.white, 0.94f)).rectTransform;
            Img(Fill(plate, "Edge"), UiKitArt.Outline1, UiTokens.WithAlpha(Tokens.Cobalt, 0.5f));
            Box(plate, "TopLeft", -3f, -3f, 6f, 6f, Tokens.Cobalt);
            Box(plate, "TopRight", width - 3f, -3f, 6f, 6f, Tokens.Cobalt);
            Box(plate, "BottomLeft", -3f, height - 3f, 6f, 6f, Tokens.Cobalt);
            Box(plate, "BottomRight", width - 3f, height - 3f, 6f, 6f, Tokens.Cobalt);
            return plate;
        }

        static GameObject BuildConfirm()
        {
            UiTokens t = Tokens;
            RectTransform root = ScreenRoot("ConfirmDialog");
            Image veil = Img(Fill(root, "Veil"), UiKitArt.White, UiTokens.WithAlpha(t.Paper, t.VeilAlpha), raycast: true);
            RectTransform frame = DesignFrame(root, "Content");
            var reveal = new List<CanvasGroup>();

            ConfirmationPlate(frame);

            RectTransform words = RevealLine(frame, "Words");
            reveal.Add(words.GetComponent<CanvasGroup>());
            TextMeshProUGUI title = Label(words, "Question", 1200f, 756f, 580f, 66f, "Quit to desktop?", t.Item, t.Navy);
            title.fontSize = 26f;
            title.richText = false;
            title.textWrappingMode = TextWrappingModes.Normal;
            title.enableAutoSizing = true;
            title.fontSizeMin = t.Item.Size;
            title.fontSizeMax = 26f;
            TextMeshProUGUI body = Label(words, "Body", 1200f, 821f, 580f, 46f, "Your settings are saved.", t.Body, t.Ink2);
            body.richText = false;
            body.textWrappingMode = TextWrappingModes.Normal;

            RectTransform actions = RevealLine(frame, "Actions");
            reveal.Add(actions.GetComponent<CanvasGroup>());
            RectTransform hint = (RectTransform)Hint(actions, "EscHint", 1200f, 948f, "Stay", "UI/Cancel").transform;
            var confirm = Place<UiButton>(UiKitComponents.Secondary, actions, "Confirm", 1250f, 868f);
            ((RectTransform)confirm.transform).sizeDelta = new Vector2(266f, t.SecondaryHeight);
            SetLabel(confirm, "Quit to desktop");
            var stay = Place<UiButton>(UiKitComponents.Primary, actions, "Stay", 1588f, 868f);
            ((RectTransform)stay.transform).sizeDelta = new Vector2(188f, t.SecondaryHeight);
            SetLabel(stay, "Stay");
            AddV3FocusMark(confirm);
            AddV3FocusMark(stay);
            confirm.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = stay };
            stay.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = confirm };

            var dialog = root.gameObject.AddComponent<UiConfirmDialog>();
            WireScreen(dialog, frame, reveal, stay);
            SetValue(dialog, "_isDialog", 1f);
            Set(dialog, "_veil", veil);
            Set(dialog, "_title", title);
            Set(dialog, "_body", body);
            Set(dialog, "_confirm", confirm);
            Set(dialog, "_stay", stay);
            Set(dialog, "_cancelHint", hint.Find("Label").GetComponent<TMP_Text>());
            return root.gameObject;
        }
    }
}
