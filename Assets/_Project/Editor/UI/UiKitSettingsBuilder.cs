using System.Collections.Generic;
using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static CloseTheDeal.Editor.UI.UiKitAssets;
using static CloseTheDeal.Editor.UI.UiKitScreens;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>Builds SettingsScreen.prefab with Controls, Video, Audio, and Accessibility tabs.</summary>
    internal static class UiKitSettingsBuilder
    {
        const float RowsTop = 300f;
        const float RightX = 1296f;
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        // Interact (F) and Drop / Throw (G) are left out: the pickups, firm and lobby code read those keys directly, so a
        // row would rebind nothing (review #563). Being unlisted, their keys are refused to other actions, as for moving.
        // UI_KIT.md "Settings" says what has to change in those lanes for the rows to work.
        static readonly (string action, string part)[] Bindings =
        {
            ("Jump", null), ("Sprint", null), ("Crouch", null), ("Attack", null),
            ("AltFire", null), ("Reload", null), ("Lean", "negative"), ("Lean", "positive"),
            ("Voice", null),
        };

        internal static GameObject Build()
        {
            UiTokens t = Tokens;
            RectTransform root = ScreenRoot("SettingsScreen");
            PaperField(root);
            RectTransform frame = DesignFrame(root, "Content");
            frame.gameObject.AddComponent<CanvasGroup>();
            var reveal = new List<CanvasGroup>();

            RectTransform head = RevealLine(frame, "Heading");
            reveal.Add(head.GetComponent<CanvasGroup>());
            Box(head, "CrumbBullet", 96f, 34f, 8f, 8f, t.Black);
            TextMeshProUGUI crumb = Label(head, "Breadcrumb", 116f, 26f, 900f, 28f, "SEAL THE DEAL · SETTINGS", t.Micro, t.Ink2);
            crumb.fontSize = t.Body.Size;
            Label(head, "Heading", 96f, 78f, 900f, 56f, "SETTINGS", t.Heading, t.Navy);
            Label(head, "Subline", 96f, 134f, 900f, 28f, "PLAYER PREFERENCES · KEPT ON THIS PC", t.Micro, t.Ink2).fontSize = t.Body.Size;

            RectTransform tabsLine = RevealLine(frame, "Tabs");
            reveal.Add(tabsLine.GetComponent<CanvasGroup>());
            UiButton[] tabs = TabBar(tabsLine, out RectTransform rule);

            RectTransform section = RevealLine(frame, "Section");
            reveal.Add(section.GetComponent<CanvasGroup>());
            var header = Place<RectTransform>(UiKitComponents.SectionHeader, section, "SectionHeader", 96f, 252f);
            TMP_Text sectionText = header.Find("Text").GetComponent<TMP_Text>();

            RectTransform pagesLine = RevealLine(frame, "Pages");
            reveal.Add(pagesLine.GetComponent<CanvasGroup>());
            TextMeshProUGUI help = DetailCard(pagesLine);
            var status = Place<UiStatusLine>(UiKitComponents.StatusLine, pagesLine, "StatusLine", 512f, 966f);
            UiSettingsPage controls = ControlsPage(pagesLine, status);
            UiSettingsPage video = VideoPage(pagesLine);
            UiSettingsPage audio = AudioPage(pagesLine);
            UiSettingsPage access = AccessibilityPage(pagesLine);
            UiSettingsPage[] pages = { controls, video, audio, access };

            RectTransform footer = RevealLine(frame, "Footer");
            reveal.Add(footer.GetComponent<CanvasGroup>());
            var reset = Place<UiButton>(UiKitComponents.Secondary, footer, "ResetToDefaults", 96f, 964f);
            ((RectTransform)reset.transform).sizeDelta = new Vector2(232f, t.SecondaryHeight);
            SetLabel(reset, "Reset to defaults");
            reset.Description = "Puts every setting on this tab back to how the game shipped.";
            var back = Place<UiButton>(UiKitComponents.Secondary, footer, "Back", 344f, 964f);
            ((RectTransform)back.transform).sizeDelta = new Vector2(140f, t.SecondaryHeight);
            SetLabel(back, "Back");
            back.Description = "Return to the previous menu. Your settings are saved.";
            UiKitScreens.Hint(footer, "PreviousTabHint", 1424f, 972f, string.Empty, keyboard: "Q", padControl: "leftShoulder", width: 60f);
            UiKitScreens.Hint(footer, "NextTabHint", 1484f, 972f, "Switch tab", keyboard: "E", padControl: "rightShoulder", width: 180f);
            UiKitScreens.Hint(footer, "BackHint", 1688f, 972f, "Back", "UI/Cancel", width: 120f);

            RectTransform changesLine = RevealLine(frame, "Changes");
            reveal.Add(changesLine.GetComponent<CanvasGroup>());
            RectTransform changes = Fill(changesLine, "Block"); // its own group: the reveal drives the line's
            changes.gameObject.AddComponent<CanvasGroup>();
            Box(changes, "ChangesBullet", 96f, 716f, 8f, 8f, t.Black);
            TextMeshProUGUI changesHeading = Label(changes, "ChangesHeading", 112f, 708f, 700f, 24f, "\u00a7 CHANGED ON THIS TAB \u00b7 0", t.Caption, t.Navy);
            Box(changes, "ChangesRule", 96f, 736f, 1120f, 1f, t.Hair); // below six rows (300 + 6 x 64)
            TextMeshProUGUI changesBody = Label(changes, "ChangesBody", 112f, 752f, 1088f, 180f,
                "Nothing on this tab differs from how the game shipped.", t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);
            changesBody.lineSpacing = 18f;

            var tabPages = frame.gameObject.AddComponent<UiTabPages>();
            Set(tabPages, "_tokens", t);
            SetArray(tabPages, "_tabs", tabs);
            SetArray(tabPages, "_pages", pages);
            Set(tabPages, "_rule", rule);

            var screen = root.gameObject.AddComponent<SettingsScreen>();
            WireScreen(screen, frame, reveal, tabs[0]);
            Set(screen, "_tabs", tabPages);
            Set(screen, "_breadcrumb", crumb);
            Set(screen, "_section", sectionText);
            Set(screen, "_reset", reset);
            Set(screen, "_back", back);
            Set(screen, "_status", status);
            Set(screen, "_help", help);
            Set(screen, "_changes", changes.GetComponent<CanvasGroup>());
            Set(screen, "_changesHeading", changesHeading);
            Set(screen, "_changesBody", changesBody);
            return root.gameObject;
        }

        static UiButton[] TabBar(RectTransform parent, out RectTransform rule)
        {
            UiTokens t = Tokens;
            string[] names = { "Controls", "Video", "Audio", "Accessibility" };
            string[] about =
            {
                "Look and the keys. Pad buttons are named as on the pad you last used.",
                "Display mode, resolution, vertical sync, frame rate, menu glass and quality.",
                "Volume for each part of the game's sound.",
                "Motion, the crosshair and the network readout.",
            };
            UiKitScreens.Hint(parent, "PreviousTab", 96f, 172f, string.Empty, keyboard: "Q", padControl: "leftShoulder", width: 48f);
            const float barLeft = 216f; // leave room between the shoulder keycap and the focus chevrons
            RectTransform bar = At(parent, "TabBar", barLeft, 164f, 4 * 236f, t.SecondaryHeight);
            var tabs = new UiButton[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                tabs[i] = Place<UiButton>(UiKitComponents.Tab, bar, names[i] + "Tab", i * 236f, 0f);
                ((RectTransform)tabs[i].transform).sizeDelta = new Vector2(232f, t.SecondaryHeight);
                SetLabel(tabs[i], names[i]);
                tabs[i].Description = about[i];
            }

            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? tabs[i - 1] : null,
                    selectOnRight = i < tabs.Length - 1 ? tabs[i + 1] : null,
                };
            }

            UiKitScreens.Hint(parent, "NextTab", barLeft + 4 * 236f + 8f, 172f, string.Empty, keyboard: "E", padControl: "rightShoulder", width: 64f);
            Box(parent, "TabHairline", 96f, 220f, 1120f, 1f, t.Hair);
            rule = Node(bar, "Rule");
            rule.anchorMin = rule.anchorMax = new Vector2(0f, 1f);
            rule.pivot = new Vector2(0f, 1f);
            rule.anchoredPosition = new Vector2(0f, -t.SecondaryHeight);
            rule.sizeDelta = new Vector2(232f, 2f);
            Img(rule, UiKitArt.White, t.Cobalt);
            return tabs;
        }

        static TextMeshProUGUI DetailCard(Transform parent)
        {
            UiTokens t = Tokens;
            Box(parent, "DetailBullet", RightX, 308f, 8f, 8f, t.Black);
            Label(parent, "DetailCaption", RightX + 16f, 300f, 500f, 24f, "ABOUT THIS SETTING", t.Caption, t.Navy);
            Box(parent, "DetailRule", RightX, 328f, 528f, 1f, t.Hair);
            return Label(parent, "DetailBody", RightX, 340f, 528f, 80f, string.Empty, t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);
        }

        static RectTransform PageRoot(Transform parent, string name)
        {
            RectTransform page = Fill(parent, name);
            page.gameObject.AddComponent<CanvasGroup>();
            return page;
        }

        static T PreviewCaption<T>(T value, Transform parent, string caption) where T : class
        {
            Box(parent, "PreviewBullet", RightX, 452f, 8f, 8f, Tokens.Black);
            Label(parent, "PreviewCaption", RightX + 16f, 444f, 500f, 24f, caption, Tokens.Caption, Tokens.Navy);
            return value;
        }

        static T Row<T>(Transform parent, GameObject controlPrefab, string name, string label, ref float y, string description, float height = 64f) where T : Component
        {
            UiTokens t = Tokens;
            RectTransform row = At(parent, name + "Row", 96f, y, 1120f, height);
            Image highlight = Img(Fill(row, "Highlight"), UiKitArt.White, UiTokens.WithAlpha(t.HoverTint, 0f));
            Image rule = Img(At(row, "Rule", 0f, 0f, 4f, height), UiKitArt.White, UiTokens.WithAlpha(t.Cobalt, 0f));
            Label(row, "Name", 16f, 0f, 520f, height, label, t.Item, t.Navy);
            Box(row, "Hairline", 0f, height - 1f, 1120f, 1f, t.Hair);

            var control = Instance<T>(controlPrefab, row, name);
            var rect = (RectTransform)control.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-12f, 0f);
            if (control is UiStepper)
                WidenStepper(rect, 300f); // room for "Borderless window", resolutions and microphone names

            UiControlFx fx = control is UiControl c ? c.Fx : ((UiSlider)(Component)control).Fx;
            fx.RowHighlight = highlight;
            fx.RowRule = rule;
            if (control is UiControl uc)
                uc.Description = description;
            else if (control is UiSlider slider)
                slider.Description = description;
            EditorUtility.SetDirty(control);
            y += height;
            return control;
        }

        static void WidenStepper(RectTransform stepper, float width)
        {
            float added = width - stepper.sizeDelta.x;
            stepper.sizeDelta = new Vector2(width, stepper.sizeDelta.y);
            foreach (RectTransform part in stepper.GetComponentsInChildren<RectTransform>(true))
            {
                // Parts pinned to the right edge follow the new width by themselves; moving them too pushes them out.
                if ((part.name != "Next" && part.name != "NextZone") || part.anchorMax.x > 0f)
                    continue;
                part.anchoredPosition += new Vector2(added, 0f);
                EditorUtility.SetDirty(part);
            }
        }

        static void Wire(UiSettingsPage page, string title, Selectable[] rows)
        {
            Set(page, "_tokens", Tokens);
            SetString(page, "_title", title);
            SetArray(page, "_rows", rows);
        }

        static UiSettingsPage ControlsPage(Transform parent, UiStatusLine status)
        {
            UiTokens t = Tokens;
            RectTransform pageRoot = PageRoot(parent, "ControlsPage");
            float y = RowsTop;
            UiSlider sensitivity = Row<UiSlider>(pageRoot, UiKitComponents.Slider, "LookSensitivity", "Mouse sensitivity", ref y,
                "How far the view turns for each move of the mouse. 100% is the game's tuning.");
            UiToggle invert = Row<UiToggle>(pageRoot, UiKitComponents.Toggle, "InvertLook", "Invert vertical look", ref y,
                "Mouse and stick: pushing up looks down.");
            UiSlider stick = Row<UiSlider>(pageRoot, UiKitComponents.Slider, "StickSpeed", "Controller look speed", ref y,
                "How fast the look stick turns the view at full tilt. 100% is the game's tuning.");

            RectTransform keys = Fill(pageRoot, "Keys");
            Label(keys, "ColAction", 112f, y + 2f, 400f, 28f, "ACTION", t.Caption, t.Ink2).fontSize = t.Body.Size;
            Label(keys, "ColKeys", 820f, y + 2f, 180f, 28f, "KEYBOARD", t.Caption, t.Ink2).fontSize = t.Body.Size;
            Label(keys, "ColPad", 1060f, y + 2f, 140f, 28f, "PAD", t.Caption, t.Ink2).fontSize = t.Body.Size;
            y += 32f;

            var rows = new List<Selectable> { sensitivity, invert, stick };
            var bindingRows = new List<ControlsPage.BindingRow>();
            foreach ((string action, string part) in Bindings)
            {
                ControlsPage.BindingRow row = BindingRow(keys, action, part, ref y);
                bindingRows.Add(row);
                rows.Add(row.Button);
            }

            var page = pageRoot.gameObject.AddComponent<ControlsPage>();
            Wire(page, "Controls", rows.ToArray());
            Set(page, "_actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath));
            Set(page, "_sensitivity", sensitivity);
            Set(page, "_invert", invert);
            Set(page, "_stickSpeed", stick);
            Set(page, "_status", status);
            Set(page, "_keys", keys);
            SetValue(page, "_lookHeight", 3 * t.RowHeight);
            SetBindingRows(page, bindingRows);

            PreviewCaption(page, pageRoot, "PAD BUTTONS");
            Label(pageRoot, "PadNote", RightX, 480f, 528f, 120f,
                "Named as on the pad you last used: Xbox names on an Xbox pad, PlayStation names (Cross, Circle, L1) on a PlayStation pad. Esc, B or Circle goes back from any menu.",
                t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);
            return page;
        }

        static ControlsPage.BindingRow BindingRow(Transform parent, string action, string part, ref float y)
        {
            UiTokens t = Tokens;
            const float height = 40f;
            RectTransform row = At(parent, action + part + "Row", 96f, y, 1120f, height);
            Image highlight = Img(Fill(row, "Highlight"), UiKitArt.White, UiTokens.WithAlpha(t.HoverTint, 0f));
            Image rule = Img(At(row, "Rule", 0f, 0f, 4f, height), UiKitArt.White, UiTokens.WithAlpha(t.Cobalt, 0f));
            Label(row, "Name", 16f, 0f, 600f, height, ControlsPageName(action, part), t.Item, t.Navy);
            Box(row, "Hairline", 0f, height - 1f, 1120f, 1f, t.Hair);

            RectTransform capRoot = At(row, "KeyCap", 724f, 3f, 160f, 34f);
            Img(capRoot, UiKitArt.Outline1, t.Navy);
            TextMeshProUGUI cap = Text(Fill(capRoot, "Key"), "—", t.Label, t.Navy, TextAlignmentOptions.Center);

            var button = Instance<UiButton>(UiKitComponents.Secondary, row, "KeyButton");
            UiKitAssets.Place((RectTransform)button.transform, 724f, 2f, 160f, 36f);
            SetLabel(button, "—");
            TMP_Text buttonLabel = button.Fx.Label;
            if (buttonLabel != null)
                buttonLabel.alignment = TextAlignmentOptions.Center;
            button.Fx.RowHighlight = highlight;
            button.Fx.RowRule = rule;
            button.Description = "Press, then press the new key. Esc cancels. A key another action uses asks before moving.";
            EditorUtility.SetDirty(button);

            RectTransform padRoot = At(row, "PadCap", 964f, 3f, 72f, 34f);
            Img(padRoot, UiKitArt.Outline1, t.Navy);
            TextMeshProUGUI pad = Text(Fill(padRoot, "Pad"), "—", t.Label, t.Navy, TextAlignmentOptions.Center);
            y += height;

            return new ControlsPage.BindingRow { Action = action, Part = part, Root = row.gameObject, Button = button, Cap = cap, Pad = pad };
        }

        static string ControlsPageName(string action, string part) => CloseTheDeal.UI.ControlsPage.NameOfAction(action, part);

        static void SetBindingRows(ControlsPage page, List<ControlsPage.BindingRow> rows)
        {
            var so = new SerializedObject(page);
            SerializedProperty list = so.FindProperty("_bindings");
            list.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Action").stringValue = rows[i].Action;
                e.FindPropertyRelative("Part").stringValue = rows[i].Part ?? string.Empty;
                e.FindPropertyRelative("Root").objectReferenceValue = rows[i].Root;
                e.FindPropertyRelative("Button").objectReferenceValue = rows[i].Button;
                e.FindPropertyRelative("Cap").objectReferenceValue = rows[i].Cap;
                e.FindPropertyRelative("Pad").objectReferenceValue = rows[i].Pad;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static UiSettingsPage VideoPage(Transform parent)
        {
            UiTokens t = Tokens;
            RectTransform pageRoot = PageRoot(parent, "VideoPage");
            float y = RowsTop;
            UiStepper mode = Row<UiStepper>(pageRoot, UiKitComponents.Stepper, "DisplayMode", "Display mode", ref y,
                "Full screen takes the monitor; a borderless window looks the same and switches away faster.");
            UiStepper resolution = Row<UiStepper>(pageRoot, UiKitComponents.Stepper, "Resolution", "Resolution", ref y,
                "A new resolution asks to be kept and goes back by itself after 10 seconds, in case the screen goes blank.");
            UiToggle vsync = Row<UiToggle>(pageRoot, UiKitComponents.Toggle, "VSync", "Vertical sync", ref y,
                "Matches frames to the monitor so the picture never tears. Adds a little delay to the mouse.");
            UiStepper cap = Row<UiStepper>(pageRoot, UiKitComponents.Stepper, "FrameCap", "Frame rate limit", ref y,
                "The most frames per second the game draws, with vertical sync off.");
            UiStepper glass = Row<UiStepper>(pageRoot, UiKitComponents.Stepper, "UiGlass", "Menu glass", ref y,
                "Blur softens the game behind menu panes; Auto blurs where this PC's renderer can. No blur shows it sharp under a denser tint, and costs nothing.");
            UiStepper quality = Row<UiStepper>(pageRoot, UiKitComponents.Stepper, "Quality", "Graphics quality", ref y,
                "Lower draws faster on older PCs."); // last: hidden where the platform has one level, leaving no gap

            var page = pageRoot.gameObject.AddComponent<VideoPage>();
            Wire(page, "Video", new Selectable[] { mode, resolution, vsync, cap, glass, quality });
            Set(page, "_mode", mode);
            Set(page, "_resolution", resolution);
            Set(page, "_vsync", vsync);
            Set(page, "_frameCap", cap);
            Set(page, "_quality", quality);
            Set(page, "_glass", glass);

            PreviewCaption(page, pageRoot, "THIS SCREEN NOW");
            Box(pageRoot, "ReadoutFrame", RightX, 480f, 528f, 120f, t.Navy, UiKitArt.Outline1);
            TextMeshProUGUI readout = Label(pageRoot, "Readout", RightX + 20f, 488f, 488f, 104f, string.Empty, t.Heading, t.Navy, TextAlignmentOptions.MidlineLeft, true);
            readout.fontSize = 28f;
            readout.characterSpacing = 4f;
            Set(page, "_readout", readout);
            return page;
        }

        static UiSettingsPage AudioPage(Transform parent)
        {
            UiTokens t = Tokens;
            RectTransform pageRoot = PageRoot(parent, "AudioPage");
            float y = RowsTop;
            string[] names = { "Master volume", "Music", "Effects", "Voice", "Interface" };
            string[] about =
            {
                "Everything the game plays.",
                "The soundtrack.",
                "Guns, blasts, glass, furniture and footsteps.",
                "Other players' voices in voice chat, and the secretary.",
                "Menu and interface sounds.",
            };
            var sliders = new UiSlider[names.Length];
            for (int i = 0; i < names.Length; i++)
                sliders[i] = Row<UiSlider>(pageRoot, UiKitComponents.Slider, "Volume" + i, names[i], ref y, about[i]);

            PreviewCaption(sliders, pageRoot, "OUTPUT LEVEL");
            Box(pageRoot, "MeterTrack", RightX, 484f, 400f, 12f, t.Hair);
            RectTransform bar = At(pageRoot, "MeterBar", RightX, 484f, 0f, 12f);
            Img(bar, UiKitArt.White, t.Cobalt);
            for (int i = 0; i <= 10; i++)
                Box(pageRoot, "MeterTick", RightX + i * 40f, 500f, 1f, 6f, t.Navy);
            var test = Place<UiButton>(UiKitComponents.Secondary, pageRoot, "TestSound", RightX, 528f);
            ((RectTransform)test.transform).sizeDelta = new Vector2(260f, t.SecondaryHeight);
            SetLabel(test, "Play test sound");
            test.Description = "Preview the Master and Interface volume levels.";

            // Voice chat, under the volumes. Nine rows in all: the page hides its changes summary to make room.
            UiStepper microphone = Row<UiStepper>(pageRoot, UiKitComponents.Stepper, "Microphone", "Microphone", ref y,
                "The microphone voice chat records from. System default follows the one Windows is set to.");
            UiStepper micMode = Row<UiStepper>(pageRoot, UiKitComponents.Stepper, "MicMode", "Microphone mode", ref y,
                "Open microphone: always on, and the voice key mutes it. Push to talk: on only while the voice key is held. Off: nobody hears you.");
            UiToggle noise = Row<UiToggle>(pageRoot, UiKitComponents.Toggle, "NoiseSuppression", "Noise suppression", ref y,
                "Takes keyboard, fan and room noise out of your voice before other players hear it.");
            UiToggle micTest = Row<UiToggle>(pageRoot, UiKitComponents.Toggle, "MicTest", "Hear my own voice", ref y,
                "Plays your voice back as other players hear it, to check your microphone. Use headphones. It switches off when you leave this tab.");

            var rows = new List<Selectable>(sliders) { microphone, micMode, noise, micTest, test };
            var page = pageRoot.gameObject.AddComponent<AudioPage>();
            Wire(page, "Audio", rows.ToArray());
            SetArray(page, "_buses", sliders);
            Set(page, "_test", test);
            Set(page, "_meterBar", bar);
            Set(page, "_microphone", microphone);
            Set(page, "_micMode", micMode);
            Set(page, "_noise", noise);
            Set(page, "_micTest", micTest);
            return page;
        }

        static UiSettingsPage AccessibilityPage(Transform parent)
        {
            RectTransform pageRoot = PageRoot(parent, "AccessibilityPage");
            float y = RowsTop;
            UiToggle motion = Row<UiToggle>(pageRoot, UiKitComponents.Toggle, "ReduceMotion", "Reduce motion", ref y,
                "Screens fade instead of sliding, nothing shakes and idle motion stops. Sounds still answer.");
            UiStepper style = Row<UiStepper>(pageRoot, UiKitComponents.Stepper, "CrosshairStyle", "Crosshair style", ref y,
                "The crosshair's shape. Left and right change it.");
            UiSwatches colour = Row<UiSwatches>(pageRoot, UiKitComponents.Swatches, "CrosshairColour", "Crosshair colour", ref y,
                "The crosshair's colour. Every choice keeps a dark outline so it reads on white walls and sky.");
            UiSlider size = Row<UiSlider>(pageRoot, UiKitComponents.Slider, "CrosshairSize", "Crosshair size", ref y,
                "From 50% to 200%, in whole pixels so it stays sharp.");
            UiToggle stats = Row<UiToggle>(pageRoot, UiKitComponents.Toggle, "NetworkStats", "Show network stats", ref y,
                "Ping, speed and host corrections in the bottom-left corner during a game.");

            var page = pageRoot.gameObject.AddComponent<AccessibilityPage>();
            Wire(page, "Accessibility", new Selectable[] { motion, style, colour, size, stats });
            Set(page, "_reducedMotion", motion);
            Set(page, "_crosshairStyle", style);
            Set(page, "_crosshairColour", colour);
            Set(page, "_crosshairSize", size);
            Set(page, "_networkStats", stats);

            PreviewCaption(page, pageRoot, "CURRENT CROSSHAIR · 1:1");
            PreviewTiles(pageRoot);
            return page;
        }

        static void PreviewTiles(Transform parent)
        {
            UiTokens t = Tokens;
            string[] names = { "Sun", "Sky", "Dark" };
            Color[] grounds = { Color.white, Color.Lerp(t.Cobalt, t.Electric, 0.5f), t.Navy };
            for (int i = 0; i < 3; i++)
            {
                float x = RightX + i * 128f;
                Box(parent, names[i] + "Tile", x, 480f, 112f, 96f, grounds[i]);
                Box(parent, names[i] + "Keyline", x, 480f, 112f, 96f, t.Hair, UiKitArt.Outline1);
                var crosshair = Instance<RectTransform>(UiKitComponents.Crosshair, parent, names[i] + "Crosshair");
                UiKitAssets.Place(crosshair, x + 16f, 488f, 80f, 80f);
                Label(parent, names[i] + "Label", x, 584f, 112f, 24f, names[i], t.Label, t.Navy);
            }
        }
    }
}
