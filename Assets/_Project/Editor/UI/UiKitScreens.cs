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
        const float BarFace = 20f, BarShade = 4f;

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
            Label(lockup, "Strip", 112f, 684f, 1000f, 20f, "SEAL THE DEAL · MAIN MENU · EARLY ACCESS", t.Micro, t.Ink3);
            Label(lockup, "StripRight", 1208f, 684f, 600f, 20f, "TERMS AND CONDITIONS APPLY", t.Micro, t.Ink3, TextAlignmentOptions.MidlineRight);
            Box(lockup, "Rule", 112f, 712f, 1696f, 1f, t.Black);
            Ticks(lockup, 112f, 706f, 1808f);
            Label(lockup, "Title", 112f, 736f, 700f, 72f, "SEAL THE DEAL", t.Title, t.Navy);
            Box(lockup, "TitleRule", 112f, 810f, 640f, 1f, t.Hair);
            Box(lockup, "TermsBullet", 112f, 828f, 8f, 8f, t.Black);
            Label(lockup, "Terms", 128f, 818f, 380f, 28f, "Terms and Conditions Apply.", t.Body, t.Ink2);
            TextMeshProUGUI version = Label(lockup, "Version", 552f, 818f, 200f, 28f, "v0.1.0 · Early Access", t.Caption, t.Ink3, TextAlignmentOptions.MidlineRight);

            RectTransform modeLine = RevealLine(frame, "Mode");
            reveal.Add(modeLine.GetComponent<CanvasGroup>());
            Label(modeLine, "ModeCaption", 112f, 868f, 300f, 18f, "§ 0.2  CONNECTION", t.Section, t.Ink3).fontSize = 12f;
            Box(modeLine, "ModeBullet", 112f, 898f, 8f, 8f, t.Cobalt);
            TextMeshProUGUI mode = Label(modeLine, "ModeText", 132f, 888f, 640f, 28f, "Starting...", t.Body, t.Navy);
            KeyHints(modeLine, 112f, 944f);

            RectTransform statusGroup = RevealLine(frame, "Status");
            reveal.Add(statusGroup.GetComponent<CanvasGroup>());
            Label(statusGroup, "StatusCaption", 832f, 722f, 440f, 18f, "§ 0.1  STATUS", t.Section, t.Ink3).fontSize = 12f;
            var status = Place<UiStatusLine>(UiKitComponents.StatusLine, statusGroup, "StatusLine", 832f, 752f);
            ((RectTransform)status.transform).sizeDelta = new Vector2(440f, 52f);
            ((RectTransform)status.transform.Find("Text")).sizeDelta = new Vector2(418f, 52f);
            TextMeshProUGUI hint = Label(statusGroup, "JoinHint", 854f, 806f, 420f, 56f,
                "To join a friend, accept their invite or choose Join Game on their name in Steam.", t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);

            RectTransform info = RevealLine(frame, "FocusInfo");
            reveal.Add(info.GetComponent<CanvasGroup>());
            TextMeshProUGUI heading = Label(info, "FocusHeading", 854f, 896f, 420f, 24f, "§ HOST GAME", t.Section, t.Navy);
            Box(info, "FocusRule", 854f, 922f, 420f, 1f, t.Hair);
            TextMeshProUGUI body = Label(info, "FocusBody", 854f, 932f, 420f, 56f, string.Empty, t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);

            RectTransform menuLine = RevealLine(frame, "Menu");
            reveal.Add(menuLine.GetComponent<CanvasGroup>());
            Label(menuLine, "MenuCaption", 1388f, 722f, 420f, 18f, "§ 1  MAIN MENU", t.Section, t.Ink3).fontSize = 12f;
            RectTransform column = At(menuLine, "Column", 1388f, 744f, 420f, 256f);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = t.SpaceS;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var host = Instance<UiButton>(UiKitComponents.Primary, column, "HostGame");
            var join = Instance<UiButton>(UiKitComponents.Secondary, column, "JoinOnThisPc");
            var settings = Instance<UiButton>(UiKitComponents.Secondary, column, "Settings");
            var quit = Instance<UiButton>(UiKitComponents.Quiet, column, "QuitToDesktop");
            SetLabel(host, "Host game");
            SetLabel(join, "Join on this PC");
            SetLabel(settings, "Settings");
            SetLabel(quit, "Quit to desktop");
            join.Description = "Join a game hosted by another copy of Seal the Deal running on this PC.";
            settings.Description = "Controls, video, audio and accessibility. Kept on this PC.";
            quit.Description = "Close Seal the Deal. Your settings are already saved.";

            RectTransform footer = RevealLine(frame, "Footer");
            reveal.Add(footer.GetComponent<CanvasGroup>());
            CropMarks(footer);
            Label(footer, "Microtype", 112f, 1036f, 1100f, 20f,
                "SEAL THE DEAL · TERMS AND CONDITIONS APPLY · KEPT ON THIS PC", t.Micro, t.Ink3);
            TextMeshProUGUI build = Label(footer, "Build", 1208f, 1036f, 420f, 20f, string.Empty, t.Micro, t.Ink3, TextAlignmentOptions.MidlineRight);
            Box(footer, "Barcode", 1648f, 1012f, 160f, 20f, t.Navy, UiKitArt.Barcode);

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
            Set(screen, "_joinHint", hint);
            Set(screen, "_focusHeading", heading);
            Set(screen, "_focusBody", body);
            Set(screen, "_build", build);
            SetArray(screen, "_versionLines", new Object[] { version });
            Set(screen, "_menuGroup", menuLine.GetComponent<CanvasGroup>());
            SetArray(screen, "_centreGroups", new Object[] { statusGroup.GetComponent<CanvasGroup>(), info.GetComponent<CanvasGroup>() });
            return root.gameObject;
        }

        static void Ticks(Transform parent, float x0, float y, float x1)
        {
            for (float x = x0; x <= x1; x += 96f)
                Box(parent, "Tick", x, y, 1f, 6f, Tokens.Black);
        }

        static void KeyHints(Transform parent, float x, float y)
        {
            Hint(parent, "SelectHint", x, y, "Select", "UI/Submit");
            Hint(parent, "BackHint", x + 150f, y, "Back", "UI/Cancel");
            Hint(parent, "MoveHint", x + 300f, y, "Move", keyboard: "\u2191\u2193", padControl: "leftStick");
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

            slot = Fill(sky, "BackdropSlot");
            // Only one sky: lane 8's live one (its own traced clouds and the towers). This flat gradient shows only
            // where the live sky can't run (no sky material in the scene).
            RectTransform fallback = Fill(sky, "StaticSky");
            Img(fallback, UiKitArt.Sky, Color.white);
            staticSky = fallback.gameObject;

            RectTransform frame = Node(sky, "Frame");
            Stretch(frame);
            RectTransform transom = Node(frame, "Transom");
            transom.anchorMin = new Vector2(0f, 1f);
            transom.anchorMax = new Vector2(1f, 1f);
            transom.pivot = new Vector2(0.5f, 1f);
            transom.sizeDelta = new Vector2(0f, BarFace);
            transom.anchoredPosition = new Vector2(0f, -302f); // centred where the 40 px bar was
            Img(transom, UiKitArt.White, t.Paper);
            RectTransform transomShade = Node(transom, "Shadow");
            transomShade.anchorMin = new Vector2(0f, 0f);
            transomShade.anchorMax = new Vector2(1f, 0f);
            transomShade.pivot = new Vector2(0.5f, 1f);
            transomShade.sizeDelta = new Vector2(0f, BarShade);
            Img(transomShade, UiKitArt.White, t.Silver);

            RectTransform mullions = DesignFrame(frame, "Mullions");
            mullions.anchorMin = new Vector2(0.5f, 0f);
            mullions.anchorMax = new Vector2(0.5f, 1f);
            mullions.sizeDelta = new Vector2(1920f, 0f);
            Post(mullions, "MullionLeft", 640f);
            Post(mullions, "MullionRight", 1280f);

            RectTransform sill = Node(frame, "Sill");
            sill.anchorMin = new Vector2(0f, 0f);
            sill.anchorMax = new Vector2(1f, 0f);
            sill.pivot = new Vector2(0.5f, 1f);
            sill.sizeDelta = new Vector2(0f, 12f);
            Img(sill, UiKitArt.White, t.Silver);
            RectTransform edge = Node(sill, "Edge");
            edge.anchorMin = new Vector2(0f, 0f);
            edge.anchorMax = new Vector2(1f, 0f);
            edge.pivot = new Vector2(0.5f, 1f);
            edge.sizeDelta = new Vector2(0f, 1f);
            Img(edge, UiKitArt.White, t.Black);
            return sky;
        }

        static void Post(RectTransform parent, string name, float centreX)
        {
            RectTransform post = Node(parent, name);
            post.anchorMin = new Vector2(0f, 0f);
            post.anchorMax = new Vector2(0f, 1f);
            post.pivot = new Vector2(0.5f, 0.5f);
            post.sizeDelta = new Vector2(BarFace, 0f);
            post.anchoredPosition = new Vector2(centreX, 0f);
            Img(post, UiKitArt.White, Tokens.Paper);
            RectTransform reveal = Node(post, "Reveal");
            reveal.anchorMin = new Vector2(1f, 0f);
            reveal.anchorMax = new Vector2(1f, 1f);
            reveal.pivot = new Vector2(0f, 0.5f);
            reveal.sizeDelta = new Vector2(BarShade, 0f);
            Img(reveal, UiKitArt.White, Tokens.Silver);
            RectTransform lit = Node(post, "LitEdge");
            lit.anchorMin = new Vector2(0f, 0f);
            lit.anchorMax = new Vector2(0f, 1f);
            lit.pivot = new Vector2(1f, 0.5f);
            lit.sizeDelta = new Vector2(1f, 0f);
            Img(lit, UiKitArt.White, Tokens.Hair);
        }

        static void CropMarks(Transform parent)
        {
            RectTransform left = At(parent, "CropLeft", 48f, 1004f, 28f, 28f);
            left.localScale = new Vector3(1f, -1f, 1f);
            left.pivot = new Vector2(0f, 0f);
            Img(left, UiKitArt.Bracket, Tokens.Navy);
            RectTransform right = At(parent, "CropRight", 1844f, 1004f, 28f, 28f);
            right.localScale = new Vector3(-1f, -1f, 1f);
            right.pivot = new Vector2(1f, 0f);
            Img(right, UiKitArt.Bracket, Tokens.Navy);
        }

        static GameObject BuildConfirm()
        {
            UiTokens t = Tokens;
            RectTransform root = ScreenRoot("ConfirmDialog");
            Image veil = Img(Fill(root, "Veil"), UiKitArt.White, UiTokens.WithAlpha(t.Paper, t.VeilAlpha), raycast: true);
            RectTransform frame = DesignFrame(root, "Content");
            var reveal = new List<CanvasGroup>();

            var pane = Place<RectTransform>(UiKitComponents.GlassPane, frame, "Pane", 1168f, 752f);
            pane.sizeDelta = new Vector2(640f, 204f);

            RectTransform words = RevealLine(frame, "Words");
            reveal.Add(words.GetComponent<CanvasGroup>());
            TextMeshProUGUI title = Label(words, "Question", 1200f, 784f, 580f, 36f, "Quit to desktop?", t.Item, t.Navy);
            title.fontSize = 26f;
            TextMeshProUGUI body = Label(words, "Body", 1200f, 824f, 580f, 28f, "Your settings are saved.", t.Body, t.Ink2);

            RectTransform actions = RevealLine(frame, "Actions");
            reveal.Add(actions.GetComponent<CanvasGroup>());
            RectTransform hint = (RectTransform)Hint(actions, "EscHint", 1200f, 876f, "Stay", "UI/Cancel").transform;
            var confirm = Place<UiButton>(UiKitComponents.Secondary, actions, "Confirm", 1328f, 868f);
            ((RectTransform)confirm.transform).sizeDelta = new Vector2(188f, t.SecondaryHeight);
            SetLabel(confirm, "Quit to desktop");
            var stay = Place<UiButton>(UiKitComponents.Primary, actions, "Stay", 1588f, 868f);
            ((RectTransform)stay.transform).sizeDelta = new Vector2(188f, t.SecondaryHeight);
            SetLabel(stay, "Stay");
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
