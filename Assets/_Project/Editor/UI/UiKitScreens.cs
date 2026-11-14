using System.Collections.Generic;
using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloseTheDeal.Editor.UI.UiKitAssets;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// Builds the slice's screen prefabs in Assets/_Project/UI/Screens from the kit's component prefabs: the title
    /// and main menu (window-frame "Survey" layout, mockup ArtSource/UI/review/00a_hero_survey.png), Settings with
    /// its Accessibility tab (02_settings_04), and the confirm dialog (01_menu_survey_11_quit). Coordinates are
    /// the mockups' 1920 x 1080 pixels; the design frame stays centred and the paper and sky stretch on wider screens.
    /// Scene references (the game menu, the other screens) are wired by the scene setup, not here.
    /// </summary>
    internal static class UiKitScreens
    {
        internal static GameObject Title, Settings, Confirm, Canvas;

        /// <summary>The title lane's band for the title lockup and menu starts at 64% of the frame; the sill sits above it.</summary>
        const float SillY = 664f;

        internal static void Build()
        {
            Title = SavePrefab(BuildTitle(), ScreenFolder + "/TitleScreen.prefab");
            Settings = SavePrefab(BuildSettings(), ScreenFolder + "/SettingsScreen.prefab");
            Confirm = SavePrefab(BuildConfirm(), ScreenFolder + "/ConfirmDialog.prefab");
            Canvas = SavePrefab(BuildCanvas(), ScreenFolder + "/KitCanvas.prefab");
        }

        /// <summary>
        /// The kit's canvas: overlay above the game menu, 1920 x 1080 reference with Expand (the design always fits,
        /// wider or taller screens add paper and sky), the screen stack, the three screens (hidden until pushed) and
        /// the focus marks on top. Its only scene link is the game menu, which UiRoot also finds by itself.
        /// </summary>
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
            Instance<UiFocusFrame>(UiKitComponents.FocusFrame, go.transform, "FocusFrame");
            title.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);
            quit.gameObject.SetActive(false);
            Set(title, "_settingsScreen", settings);
            Set(title, "_quitDialog", quit);

            var root = go.AddComponent<UiRoot>();
            Set(root, "_tokens", Tokens);
            Set(root, "_stack", stack);
            Set(root, "_title", title);
            return go;
        }

        static RectTransform ScreenRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var root = (RectTransform)go.transform;
            Stretch(root);
            go.AddComponent<CanvasGroup>();
            return root;
        }

        /// <summary>Paper with the faint hairline grid: the field every screen sits on.</summary>
        static void PaperField(RectTransform root)
        {
            Img(Fill(root, "Paper"), UiKitArt.White, Tokens.Paper, raycast: true);
            Img(Fill(root, "Grid"), UiKitArt.Grid, Tokens.Grid, Image.Type.Tiled);
        }

        static void WireScreen(UiScreen screen, RectTransform content, List<CanvasGroup> reveal, Selectable first)
        {
            Set(screen, "_tokens", Tokens);
            Set(screen, "_content", content);
            SetArray(screen, "_reveal", reveal.ToArray());
            Set(screen, "_firstFocus", first);
        }

        static T Place<T>(GameObject prefab, Transform parent, string name, float x, float y) where T : Component
        {
            var c = Instance<T>(prefab, parent, name);
            var rect = (RectTransform)c.transform;
            Vector2 size = rect.sizeDelta;
            UiKitAssets.Place(rect, x, y, size.x, size.y);
            return c;
        }

        static void SetLabel(Component control, string text)
        {
            TMP_Text label = control.transform.Find("Plate/Label")?.GetComponent<TMP_Text>();
            if (label != null)
                label.text = text;
        }

        static void SetText(Component part, string child, string text)
        {
            TMP_Text label = part.transform.Find(child)?.GetComponent<TMP_Text>();
            if (label != null)
                label.text = text;
        }

        // ---- Title ------------------------------------------------------------------------------------

        static GameObject BuildTitle()
        {
            UiTokens t = Tokens;
            RectTransform root = ScreenRoot("TitleScreen");
            PaperField(root);
            RectTransform sky = SkyWindow(root, out RectTransform slot, out GameObject staticSky);
            RectTransform frame = DesignFrame(root, "Content");
            var reveal = new List<CanvasGroup>();

            RectTransform lockup = RevealLine(frame, "Lockup");
            reveal.Add(lockup.GetComponent<CanvasGroup>());
            Box(lockup, "Rule", 112f, 712f, 1696f, 1f, t.Black);
            Label(lockup, "Title", 112f, 736f, 700f, 72f, "SEAL THE DEAL", t.Title, t.Navy);
            Box(lockup, "TitleRule", 112f, 810f, 640f, 1f, t.Hair);
            Box(lockup, "TermsBullet", 112f, 828f, 8f, 8f, t.Black);
            Label(lockup, "Terms", 128f, 818f, 380f, 28f, "Terms and Conditions Apply.", t.Body, t.Ink2);
            TextMeshProUGUI version = Label(lockup, "Version", 552f, 818f, 200f, 28f, "v0.1.0 · Early Access", t.Micro, t.Ink3, TextAlignmentOptions.MidlineRight);

            RectTransform modeLine = RevealLine(frame, "Mode");
            reveal.Add(modeLine.GetComponent<CanvasGroup>());
            Box(modeLine, "ModeBullet", 112f, 898f, 8f, 8f, t.Cobalt);
            TextMeshProUGUI mode = Label(modeLine, "ModeText", 132f, 888f, 640f, 28f, "Starting...", t.Body, t.Navy);

            RectTransform statusGroup = RevealLine(frame, "Status");
            reveal.Add(statusGroup.GetComponent<CanvasGroup>());
            var status = Place<UiStatusLine>(UiKitComponents.StatusLine, statusGroup, "StatusLine", 832f, 760f);
            ((RectTransform)status.transform).sizeDelta = new Vector2(440f, 52f);
            ((RectTransform)status.transform.Find("Text")).sizeDelta = new Vector2(418f, 52f);
            TextMeshProUGUI hint = Label(statusGroup, "JoinHint", 854f, 812f, 420f, 56f,
                "To join a friend, accept their invite or choose Join Game on their name in Steam.", t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);

            RectTransform info = RevealLine(frame, "FocusInfo");
            reveal.Add(info.GetComponent<CanvasGroup>());
            TextMeshProUGUI heading = Label(info, "FocusHeading", 854f, 896f, 420f, 24f, "§ HOST GAME", t.Section, t.Navy);
            Box(info, "FocusRule", 854f, 922f, 420f, 1f, t.Hair);
            TextMeshProUGUI body = Label(info, "FocusBody", 854f, 932f, 420f, 56f, string.Empty, t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);

            RectTransform menuLine = RevealLine(frame, "Menu");
            reveal.Add(menuLine.GetComponent<CanvasGroup>());
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
            settings.Description = "Crosshair, motion and the network readout. Kept on this PC.";
            quit.Description = "Close Seal the Deal. Your settings are already saved.";

            RectTransform footer = RevealLine(frame, "Footer");
            reveal.Add(footer.GetComponent<CanvasGroup>());
            CropMarks(footer);
            TextMeshProUGUI footerText = Label(footer, "Microtype", 112f, 1008f, 1200f, 24f,
                "Terms and Conditions Apply.", t.Micro, t.Ink3);
            Box(footer, "Barcode", 1648f, 1012f, 160f, 20f, t.Navy, UiKitArt.Barcode);
            _ = footerText;

            var screen = root.gameObject.AddComponent<TitleScreen>();
            WireScreen(screen, frame, reveal, host);
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
            SetArray(screen, "_versionLines", new Object[] { version });
            Set(screen, "_menuGroup", menuLine.GetComponent<CanvasGroup>());
            _ = sky;
            return root.gameObject;
        }

        /// <summary>
        /// The window frame: sky above the sill, divided by paper mullions into panes (mockup 00a). The sky is a static
        /// gradient until the title lane's live backdrop goes into BackdropSlot, which sits behind the mullions.
        /// </summary>
        static RectTransform SkyWindow(RectTransform root, out RectTransform slot, out GameObject staticSky)
        {
            UiTokens t = Tokens;
            RectTransform sky = Node(root, "SkyWindow");
            sky.anchorMin = new Vector2(0f, 0.5f);
            sky.anchorMax = new Vector2(1f, 1f);
            sky.pivot = new Vector2(0.5f, 0f);
            sky.offsetMin = new Vector2(0f, 540f - SillY);
            sky.offsetMax = Vector2.zero;

            slot = Fill(sky, "BackdropSlot");
            RectTransform fallback = Fill(sky, "StaticSky");
            Img(fallback, UiKitArt.Sky, Color.white);
            staticSky = fallback.gameObject;

            RectTransform mullions = DesignFrame(sky, "Mullions");
            mullions.anchorMin = new Vector2(0.5f, 0f);
            mullions.anchorMax = new Vector2(0.5f, 1f);
            mullions.pivot = new Vector2(0.5f, 0.5f);
            mullions.sizeDelta = new Vector2(1920f, 0f);
            Post(mullions, "MullionLeft", 640f);
            Post(mullions, "MullionRight", 1280f);

            RectTransform transom = Node(sky, "Transom");
            transom.anchorMin = new Vector2(0f, 1f);
            transom.anchorMax = new Vector2(1f, 1f);
            transom.pivot = new Vector2(0.5f, 1f);
            transom.sizeDelta = new Vector2(0f, 40f);
            transom.anchoredPosition = new Vector2(0f, -292f);
            Img(transom, UiKitArt.White, t.Paper);

            RectTransform sill = Node(sky, "Sill");
            sill.anchorMin = new Vector2(0f, 0f);
            sill.anchorMax = new Vector2(1f, 0f);
            sill.pivot = new Vector2(0.5f, 1f);
            sill.sizeDelta = new Vector2(0f, 12f);
            Img(sill, UiKitArt.White, t.Silver);
            return sky;
        }

        static void Post(RectTransform parent, string name, float centreX)
        {
            RectTransform post = Node(parent, name);
            post.anchorMin = new Vector2(0f, 0f);
            post.anchorMax = new Vector2(0f, 1f);
            post.pivot = new Vector2(0.5f, 0.5f);
            post.sizeDelta = new Vector2(44f, 0f);
            post.anchoredPosition = new Vector2(centreX, 0f);
            Img(post, UiKitArt.White, Tokens.Paper);
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

        // ---- Settings -----------------------------------------------------------------------------------

        static GameObject BuildSettings()
        {
            UiTokens t = Tokens;
            RectTransform root = ScreenRoot("SettingsScreen");
            PaperField(root);
            RectTransform frame = DesignFrame(root, "Content");
            var reveal = new List<CanvasGroup>();

            RectTransform head = RevealLine(frame, "Heading");
            reveal.Add(head.GetComponent<CanvasGroup>());
            Box(head, "CrumbBullet", 96f, 34f, 8f, 8f, t.Black);
            Label(head, "Breadcrumb", 116f, 26f, 900f, 24f, "SEAL THE DEAL · SETTINGS · ACCESSIBILITY", t.Micro, t.Ink3);
            Label(head, "Heading", 96f, 78f, 900f, 56f, "SETTINGS", t.Heading, t.Navy);
            Label(head, "Subline", 96f, 134f, 900f, 20f, "PLAYER PREFERENCES · KEPT ON THIS PC", t.Micro, t.Ink3);

            RectTransform tabs = RevealLine(frame, "Tabs");
            reveal.Add(tabs.GetComponent<CanvasGroup>());
            Box(tabs, "TabRule", 96f, 220f, 1120f, 1f, t.Hair);
            var tab = Place<UiButton>(UiKitComponents.Tab, tabs, "AccessibilityTab", 96f, 164f);
            SetLabel(tab, "Accessibility");
            tab.Description = "Crosshair, motion and the network readout. Controls, video and audio follow once their settings exist in the game.";

            RectTransform section = RevealLine(frame, "Section");
            reveal.Add(section.GetComponent<CanvasGroup>());
            var header = Place<RectTransform>(UiKitComponents.SectionHeader, section, "SectionHeader", 96f, 252f);
            SetText(header, "Text", "§ 1  ACCESSIBILITY");

            RectTransform rows = RevealLine(frame, "Rows");
            reveal.Add(rows.GetComponent<CanvasGroup>());
            float y = 300f;
            UiToggle motion = Row<UiToggle>(rows, UiKitComponents.Toggle, "ReduceMotion", "Reduce motion", ref y,
                "Screens fade instead of sliding, nothing shakes and idle motion stops. Sounds still answer.");
            UiStepper style = Row<UiStepper>(rows, UiKitComponents.Stepper, "CrosshairStyle", "Crosshair style", ref y,
                "The crosshair's shape. Left and right change it.");
            UiSwatches colour = Row<UiSwatches>(rows, UiKitComponents.Swatches, "CrosshairColour", "Crosshair colour", ref y,
                "The crosshair's colour. Every choice keeps a dark outline so it reads on white walls and sky.");
            UiSlider size = Row<UiSlider>(rows, UiKitComponents.Slider, "CrosshairSize", "Crosshair size", ref y,
                "From 50% to 200%, in whole pixels so it stays sharp.");
            UiToggle stats = Row<UiToggle>(rows, UiKitComponents.Toggle, "NetworkStats", "Show network stats", ref y,
                "Ping, speed and host corrections in the bottom-left corner during a game.");
            TextMeshProUGUI help = Label(rows, "Help", 112f, y + 12f, 1100f, 52f, string.Empty, t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);

            RectTransform preview = RevealLine(frame, "Preview");
            reveal.Add(preview.GetComponent<CanvasGroup>());
            PreviewTiles(preview);

            RectTransform footer = RevealLine(frame, "Footer");
            reveal.Add(footer.GetComponent<CanvasGroup>());
            var reset = Place<UiButton>(UiKitComponents.Secondary, footer, "ResetToDefaults", 96f, 964f);
            ((RectTransform)reset.transform).sizeDelta = new Vector2(232f, t.SecondaryHeight);
            SetLabel(reset, "Reset to defaults");
            reset.Description = "Puts every setting on this tab back to how the game shipped.";
            var back = Place<UiButton>(UiKitComponents.Secondary, footer, "Back", 344f, 964f);
            ((RectTransform)back.transform).sizeDelta = new Vector2(140f, t.SecondaryHeight);
            SetLabel(back, "Back");
            back.Description = "Back to the title. Everything here is already saved.";
            var status = Place<UiStatusLine>(UiKitComponents.StatusLine, footer, "StatusLine", 512f, 966f);
            var hint = Place<RectTransform>(UiKitComponents.KeyHint, footer, "EscHint", 1660f, 972f);
            SetText(hint, "Key", "Esc");
            SetText(hint, "Label", "Back");
            Label(footer, "Microtype", 96f, 1040f, 1400f, 20f,
                "Settings are kept on this PC as soon as you change them.", t.Micro, t.Ink3);

            LinkSettingsNavigation(tab, new Selectable[] { motion, style, colour, size, stats }, reset, back);

            var screen = root.gameObject.AddComponent<SettingsScreen>();
            WireScreen(screen, frame, reveal, motion);
            Set(screen, "_reducedMotion", motion);
            Set(screen, "_crosshairStyle", style);
            Set(screen, "_crosshairColour", colour);
            Set(screen, "_crosshairSize", size);
            Set(screen, "_networkStats", stats);
            Set(screen, "_reset", reset);
            Set(screen, "_back", back);
            Set(screen, "_status", status);
            Set(screen, "_help", help);
            Set(screen, "_tab", tab);
            return root.gameObject;
        }

        /// <summary>One 64 px settings row: highlight, cobalt rule, name on the left, its control on the right.</summary>
        static T Row<T>(Transform parent, GameObject controlPrefab, string name, string label, ref float y, string description) where T : Component
        {
            UiTokens t = Tokens;
            RectTransform row = At(parent, name + "Row", 96f, y, 1120f, t.RowHeight);
            Image highlight = Img(Fill(row, "Highlight"), UiKitArt.White, UiTokens.WithAlpha(t.HoverTint, 0f));
            RectTransform ruleRect = At(row, "Rule", 0f, 0f, 4f, t.RowHeight);
            Image rule = Img(ruleRect, UiKitArt.White, UiTokens.WithAlpha(t.Cobalt, 0f));
            Label(row, "Name", 16f, 0f, 520f, t.RowHeight, label, t.Item, t.Navy);
            Box(row, "Hairline", 0f, t.RowHeight - 1f, 1120f, 1f, t.Hair);

            var control = Instance<T>(controlPrefab, row, name);
            var rect = (RectTransform)control.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-12f, 0f);

            UiControlFx fx = control is UiControl c ? c.Fx : ((UiSlider)(Component)control).Fx;
            fx.RowHighlight = highlight;
            fx.RowRule = rule;
            if (control is UiControl uc)
                uc.Description = description;
            else if (control is UiSlider slider)
                slider.Description = description;
            EditorUtility.SetDirty(control);
            y += t.RowHeight;
            return control;
        }

        static void LinkSettingsNavigation(Selectable tab, Selectable[] rows, Selectable reset, Selectable back)
        {
            tab.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = rows[0] };
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i == 0 ? tab : rows[i - 1],
                    selectOnDown = i == rows.Length - 1 ? reset : rows[i + 1]
                };
            }

            reset.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = rows[^1], selectOnRight = back };
            back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = rows[^1], selectOnLeft = reset };
        }

        /// <summary>The live crosshair over three world-like grounds: sunlit white, sky and a dark room.</summary>
        static void PreviewTiles(Transform parent)
        {
            UiTokens t = Tokens;
            Box(parent, "PreviewBullet", 1296f, 308f, 8f, 8f, t.Black);
            Label(parent, "PreviewCaption", 1312f, 300f, 400f, 24f, "CURRENT CROSSHAIR · 1:1", t.Caption, t.Navy);
            string[] names = { "Sun", "Sky", "Dark" };
            Color[] grounds = { Color.white, Color.Lerp(t.Cobalt, t.Electric, 0.5f), t.Navy };
            for (int i = 0; i < 3; i++)
            {
                float x = 1296f + i * 128f;
                Box(parent, names[i] + "Tile", x, 336f, 112f, 96f, grounds[i]);
                Box(parent, names[i] + "Keyline", x, 336f, 112f, 96f, t.Hair, UiKitArt.Outline1);
                var crosshair = Instance<RectTransform>(UiKitComponents.Crosshair, parent, names[i] + "Crosshair");
                UiKitAssets.Place(crosshair, x + 16f, 344f, 80f, 80f);
                Label(parent, names[i] + "Label", x, 440f, 112f, 24f, names[i], t.Label, t.Navy);
            }
        }

        // ---- Confirm dialog ---------------------------------------------------------------------------------

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
            var hint = Place<RectTransform>(UiKitComponents.KeyHint, actions, "EscHint", 1200f, 876f);
            SetText(hint, "Key", "Esc");
            SetText(hint, "Label", "Stay");
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
