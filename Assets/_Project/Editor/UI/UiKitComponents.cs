using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloseTheDeal.Editor.UI.UiKitAssets;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// Builds the kit's component prefabs in Assets/_Project/UI/Kit: buttons (primary, secondary, quiet, tab),
    /// toggle, stepper, swatches, slider, status line, key hint, section header, plate, glass pane, focus frame
    /// and crosshair. A prefab a designer edited is left alone (GeneratedAssetGuard): edit the prefab to take it over.
    /// </summary>
    internal static class UiKitComponents
    {
        internal static GameObject Primary, Secondary, Quiet, Tab, Toggle, Stepper, Swatches, Slider;
        internal static GameObject StatusLine, KeyHint, SectionHeader, Plate, GlassPane, FocusFrame, Crosshair;

        internal static void Build()
        {
            UiTokens t = Tokens;
            Primary = SavePrefab(Button("Button_Primary", UiVariant.Primary, "Host game", 420f, t.PrimaryHeight), KitFolder + "/Button_Primary.prefab");
            Secondary = SavePrefab(Button("Button_Secondary", UiVariant.Secondary, "Settings", 420f, t.SecondaryHeight), KitFolder + "/Button_Secondary.prefab");
            Quiet = SavePrefab(Button("Button_Quiet", UiVariant.Quiet, "Quit to desktop", 420f, 44f), KitFolder + "/Button_Quiet.prefab");
            Tab = SavePrefab(Button("Tab", UiVariant.Tab, "Accessibility", 248f, t.SecondaryHeight), KitFolder + "/Tab.prefab");
            Toggle = SavePrefab(BuildToggle(), KitFolder + "/Toggle.prefab");
            Stepper = SavePrefab(BuildStepper(), KitFolder + "/Stepper.prefab");
            Swatches = SavePrefab(BuildSwatches(), KitFolder + "/Swatches.prefab");
            Slider = SavePrefab(BuildSlider(), KitFolder + "/Slider.prefab");
            StatusLine = SavePrefab(BuildStatusLine(), KitFolder + "/StatusLine.prefab");
            KeyHint = SavePrefab(BuildKeyHint(), KitFolder + "/KeyHint.prefab");
            SectionHeader = SavePrefab(BuildSectionHeader(), KitFolder + "/SectionHeader.prefab");
            Plate = SavePrefab(BuildPane("Plate", false), KitFolder + "/Plate.prefab");
            GlassPane = SavePrefab(BuildPane("GlassPane", true), KitFolder + "/GlassPane.prefab");
            FocusFrame = SavePrefab(BuildFocusFrame(), KitFolder + "/FocusFrame.prefab");
            Crosshair = SavePrefab(BuildCrosshair(), KitFolder + "/Crosshair.prefab");
        }

        // ---- The shared control frame -----------------------------------------------------------

        static RectTransform NewRoot(string name, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        /// <summary>The parts every control has: padded hit area, refusal plate, lift shadow, plate, brackets, tick.</summary>
        static void Frame(RectTransform root, UiControlFx fx, UiVariant variant, bool keyline)
        {
            UiTokens t = Tokens;
            fx.Tokens = t;
            fx.Variant = variant;
            Img(Fill(root, "HitArea", -t.HitPadding), UiKitArt.White, Color.clear, raycast: true);
            fx.RefusalPlate = Img(Fill(root, "RefusalPlate", -4f), UiKitArt.White, UiTokens.WithAlpha(t.Amber, 0f));
            fx.Shadow = Img(Fill(root, "Shadow"), UiKitArt.White, UiTokens.WithAlpha(t.Navy, 0f));
            fx.Plate = Fill(root, "Plate");
            fx.Fill = Img(Fill(fx.Plate, "Fill"), UiKitArt.White, t.Paper);
            if (keyline)
                fx.Outline = Img(Fill(fx.Plate, "Outline"), UiKitArt.Outline1, t.Navy);
            fx.Dashed = Img(Fill(fx.Plate, "Dashed"), UiKitArt.Dashed, t.Disabled, Image.Type.Tiled);
            fx.Dashed.enabled = false;

            RectTransform underline = Node(fx.Plate, "Underline");
            underline.anchorMin = new Vector2(0f, 0f);
            underline.anchorMax = new Vector2(1f, 0f);
            underline.pivot = new Vector2(0f, 0f);
            underline.sizeDelta = new Vector2(0f, 2f);
            underline.anchoredPosition = Vector2.zero;
            fx.Underline = Img(underline, UiKitArt.White, UiTokens.WithAlpha(t.Electric, 0f));

            RectTransform tick = Node(fx.Plate, "ConfirmTick");
            tick.anchorMin = tick.anchorMax = new Vector2(0f, 0.5f);
            tick.sizeDelta = new Vector2(10f, 10f);
            tick.localRotation = Quaternion.Euler(0f, 0f, 45f);
            tick.localScale = Vector3.zero;
            fx.ConfirmTick = Img(tick, UiKitArt.White, UiTokens.WithAlpha(t.Cyan, 0f));

            fx.HoverBrackets = Brackets(root, 4f, 12f, UiTokens.WithAlpha(t.Navy, 0f));
        }

        /// <summary>Four L brackets outside a rect's corners: top-left, top-right, bottom-left, bottom-right.</summary>
        internal static Image[] Brackets(RectTransform parent, float gap, float size, Color colour)
        {
            var result = new Image[4];
            Vector2[] anchors = { new(0f, 1f), new(1f, 1f), new(0f, 0f), new(1f, 0f) };
            string[] names = { "BracketTL", "BracketTR", "BracketBL", "BracketBR" };
            for (int i = 0; i < 4; i++)
            {
                RectTransform b = Node(parent, names[i]);
                b.anchorMin = b.anchorMax = anchors[i];
                b.pivot = new Vector2(0f, 1f);
                b.sizeDelta = new Vector2(size, size);
                float sx = (i & 1) == 0 ? 1f : -1f;
                float sy = i < 2 ? 1f : -1f;
                b.localScale = new Vector3(sx, sy, 1f);
                b.anchoredPosition = new Vector2(-sx * gap, sy * gap);
                result[i] = Img(b, UiKitArt.Bracket, colour);
            }

            return result;
        }

        static TextMeshProUGUI PlateLabel(UiControlFx fx, string text, UiTypeRole role, float left, float right, TextAlignmentOptions align)
        {
            RectTransform rect = Fill(fx.Plate, "Label");
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(-right, 0f);
            TextMeshProUGUI label = Text(rect, text, role, Tokens.Navy, align);
            fx.Label = label;
            return label;
        }

        // ---- Buttons --------------------------------------------------------------------------------

        static GameObject Button(string name, UiVariant variant, string label, float w, float h)
        {
            RectTransform root = NewRoot(name, w, h);
            var button = root.gameObject.AddComponent<UiButton>();
            button.transition = Selectable.Transition.None;
            UiControlFx fx = button.Fx;
            Frame(root, fx, variant, variant == UiVariant.Secondary);
            bool centred = variant == UiVariant.Tab;
            PlateLabel(fx, label, Tokens.Item, variant == UiVariant.Quiet ? 34f : 20f, 20f,
                centred ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft);
            if (variant == UiVariant.Quiet)
            {
                Image bullet = Box(fx.Plate, "Bullet", 16f, h * 0.5f - 4f, 8f, 8f, Tokens.Navy);
                fx.Inks = new Graphic[] { bullet };
            }

            fx.Apply();
            return root.gameObject;
        }

        // ---- Value controls -----------------------------------------------------------------------

        static GameObject BuildToggle()
        {
            RectTransform root = NewRoot("Toggle", 120f, 44f);
            var toggle = root.gameObject.AddComponent<UiToggle>();
            toggle.transition = Selectable.Transition.None;
            UiControlFx fx = toggle.Fx;
            Frame(root, fx, UiVariant.Quiet, false);
            Image box = Box(fx.Plate, "Box", 14f, 11f, 22f, 22f, Tokens.Navy, UiKitArt.Outline2);
            Image check = Box(fx.Plate, "Check", 19f, 16f, 12f, 12f, Tokens.Cobalt);
            TextMeshProUGUI state = PlateLabel(fx, "Off", Tokens.Item, 48f, 8f, TextAlignmentOptions.MidlineLeft);
            fx.Inks = new Graphic[] { box };
            Set(toggle, "_check", check);
            Set(toggle, "_state", state);
            fx.Apply();
            return root.gameObject;
        }

        static GameObject BuildStepper()
        {
            RectTransform root = NewRoot("Stepper", 240f, 48f);
            var stepper = root.gameObject.AddComponent<UiStepper>();
            stepper.transition = Selectable.Transition.None;
            UiControlFx fx = stepper.Fx;
            Frame(root, fx, UiVariant.Secondary, true);
            TextMeshProUGUI value = PlateLabel(fx, "Cross + Dot", Tokens.Item, 44f, 44f, TextAlignmentOptions.Center);
            TextMeshProUGUI left = Label(fx.Plate, "Previous", 0f, 0f, 44f, 48f, "‹", Tokens.Heading, Tokens.Navy, TextAlignmentOptions.Center);
            TextMeshProUGUI right = Label(fx.Plate, "Next", 196f, 0f, 44f, 48f, "›", Tokens.Heading, Tokens.Navy, TextAlignmentOptions.Center);
            left.characterSpacing = right.characterSpacing = 0f;
            fx.Inks = new Graphic[] { left, right };
            ClickZone(fx.Plate, "PreviousZone", 0f, 0f, 44f, 48f, stepper, -1);
            ClickZone(fx.Plate, "NextZone", 196f, 0f, 44f, 48f, stepper, 1);
            Set(stepper, "_value", value);
            fx.Apply();
            return root.gameObject;
        }

        static void ClickZone(Transform parent, string name, float x, float y, float w, float h, UiControl control, int value)
        {
            Image hit = Box(parent, name, x, y, w, h, Color.clear);
            hit.raycastTarget = true;
            var zone = hit.gameObject.AddComponent<UiClickZone>();
            Set(zone, "_control", control);
            SetValue(zone, "_value", value);
        }

        static GameObject BuildSwatches()
        {
            RectTransform root = NewRoot("Swatches", 380f, 48f);
            var swatches = root.gameObject.AddComponent<UiSwatches>();
            swatches.transition = Selectable.Transition.None;
            UiControlFx fx = swatches.Fx;
            Frame(root, fx, UiVariant.Quiet, false);
            PlateLabel(fx, "Paper White", Tokens.Item, 8f, 168f, TextAlignmentOptions.MidlineRight);
            var squares = new Image[3];
            var rings = new Image[3];
            Color[] colours = { Tokens.Paper, Tokens.Cyan, Tokens.Electric };
            for (int i = 0; i < 3; i++)
            {
                float x = 228f + i * 48f;
                rings[i] = Box(fx.Plate, "Ring" + i, x - 6f, 2f, 44f, 44f, Tokens.Navy, UiKitArt.Outline2);
                squares[i] = Box(fx.Plate, "Swatch" + i, x, 8f, 32f, 32f, colours[i]);
                squares[i].raycastTarget = true;
                Box(fx.Plate, "Keyline" + i, x, 8f, 32f, 32f, Tokens.Navy, UiKitArt.Outline1);
                var zone = squares[i].gameObject.AddComponent<UiClickZone>();
                Set(zone, "_control", swatches);
                SetValue(zone, "_value", i);
            }

            SetArray(swatches, "_swatches", squares);
            SetArray(swatches, "_rings", rings);
            Set(swatches, "_name", fx.Label);
            fx.Apply();
            return root.gameObject;
        }

        static GameObject BuildSlider()
        {
            UiTokens t = Tokens;
            RectTransform root = NewRoot("Slider", 420f, 48f);
            var slider = root.gameObject.AddComponent<UiSlider>();
            slider.transition = Selectable.Transition.None;
            UiControlFx fx = slider.Fx;
            Frame(root, fx, UiVariant.Quiet, false);

            Box(fx.Plate, "Track", 16f, 23f, 260f, 2f, t.Hair);
            RectTransform fillArea = At(fx.Plate, "FillArea", 16f, 23f, 260f, 2f);
            RectTransform fill = Fill(fillArea, "Bar");
            Img(fill, UiKitArt.White, t.Cobalt);
            RectTransform handleArea = At(fx.Plate, "HandleArea", 16f, 0f, 260f, 48f);
            RectTransform handle = Node(handleArea, "Handle");
            handle.sizeDelta = new Vector2(8f, 22f);
            Image handleImage = Img(handle, UiKitArt.White, t.Navy, raycast: true);

            Box(fx.Plate, "FieldBox", 300f, 2f, 104f, 44f, t.Navy, UiKitArt.Outline1);
            TextMeshProUGUI field = Label(fx.Plate, "Field", 300f, 2f, 104f, 44f, "100%", t.Label, t.Navy, TextAlignmentOptions.Center);
            fx.Label = field;
            fx.Inks = new Graphic[] { handleImage };

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.wholeNumbers = true;
            slider.minValue = 5f;
            slider.maxValue = 20f;
            slider.SetValueWithoutNotify(10f);
            Set(slider, "_field", field);
            fx.Apply();
            return root.gameObject;
        }

        // ---- Text parts ---------------------------------------------------------------------------

        static GameObject BuildStatusLine()
        {
            RectTransform root = NewRoot("StatusLine", 600f, 52f);
            var line = root.gameObject.AddComponent<UiStatusLine>();
            Image mark = Box(root, "Mark", 0f, 21f, 10f, 10f, Tokens.Cobalt);
            Image outline = Box(root, "MarkOutline", 0f, 21f, 10f, 10f, Tokens.Cobalt, UiKitArt.Outline1);
            outline.enabled = false;
            TextMeshProUGUI text = Label(root, "Text", 22f, 0f, 578f, 52f, string.Empty, Tokens.Body, Tokens.Navy, TextAlignmentOptions.MidlineLeft, true);
            Set(line, "_tokens", Tokens);
            Set(line, "_mark", mark);
            Set(line, "_markOutline", outline);
            Set(line, "_text", text);
            return root.gameObject;
        }

        static GameObject BuildKeyHint()
        {
            RectTransform root = NewRoot("KeyHint", 160f, 40f);
            Box(root, "KeyCap", 0f, 2f, 48f, 36f, Tokens.Navy, UiKitArt.Outline1);
            Label(root, "Key", 0f, 2f, 48f, 36f, "Esc", Tokens.Label, Tokens.Navy, TextAlignmentOptions.Center);
            Label(root, "Label", 58f, 0f, 102f, 40f, "Back", Tokens.Label, Tokens.Ink2);
            return root.gameObject;
        }

        static GameObject BuildSectionHeader()
        {
            RectTransform root = NewRoot("SectionHeader", 1120f, 40f);
            Box(root, "Bullet", 0f, 16f, 8f, 8f, Tokens.Black);
            Label(root, "Text", 20f, 0f, 1100f, 39f, "§ 1  ACCESSIBILITY", Tokens.Section, Tokens.Navy);
            Box(root, "Rule", 0f, 39f, 1120f, 1f, Tokens.Black);
            return root.gameObject;
        }

        static GameObject BuildPane(string name, bool frosted)
        {
            UiTokens t = Tokens;
            RectTransform root = NewRoot(name, 640f, 204f);
            RectTransform shadow = Fill(root, "Shadow");
            shadow.anchoredPosition = new Vector2(0f, -6f);
            Img(shadow, UiKitArt.White, UiTokens.WithAlpha(t.Navy, 0.10f));
            Image pane = Img(Fill(root, "Surface"), UiKitArt.White, UiTokens.WithAlpha(t.Paper, frosted ? t.FrostedAlpha : t.PlateAlpha));
            Img(Fill(root, "Edge"), UiKitArt.Outline1, t.Hair);
            if (frosted)
            {
                RectTransform rule = Node(root, "TopRule");
                rule.anchorMin = new Vector2(0f, 1f);
                rule.anchorMax = new Vector2(1f, 1f);
                rule.pivot = new Vector2(0.5f, 1f);
                rule.sizeDelta = new Vector2(0f, 4f);
                Img(rule, UiKitArt.White, t.Cobalt);
            }

            var glass = root.gameObject.AddComponent<UiGlassPane>();
            Set(glass, "_tokens", t);
            Set(glass, "_pane", pane);
            SetValue(glass, "_frosted", frosted ? 1f : 0f);
            return root.gameObject;
        }

        // ---- Focus marks and crosshair ----------------------------------------------------------------

        static GameObject BuildFocusFrame()
        {
            UiTokens t = Tokens;
            var go = new GameObject("FocusFrame", typeof(RectTransform)) { layer = 5 };
            var root = (RectTransform)go.transform;
            Stretch(root);
            var group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;

            RectTransform frame = Node(root, "Frame");
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(420f, 64f);
            Img(Fill(frame, "Ring"), UiKitArt.Outline2, t.Electric);
            Image[] brackets = Brackets(frame, 4f, 14f, t.Navy);
            RectTransform chevron = Node(frame, "Chevron");
            chevron.anchorMin = chevron.anchorMax = new Vector2(0f, 0.5f);
            chevron.pivot = new Vector2(1f, 0.5f);
            chevron.sizeDelta = new Vector2(40f, 16f);
            chevron.anchoredPosition = new Vector2(-t.SpaceM, 0f);
            Img(chevron, UiKitArt.Chevron, t.Navy);

            var focus = go.AddComponent<UiFocusFrame>();
            Set(focus, "_tokens", t);
            Set(focus, "_frame", frame);
            Set(focus, "_group", group);
            Set(focus, "_ring", frame.Find("Ring").GetComponent<Image>());
            SetArray(focus, "_brackets", new Object[] { brackets[0].rectTransform, brackets[1].rectTransform, brackets[2].rectTransform, brackets[3].rectTransform });
            Set(focus, "_chevron", chevron);
            return go;
        }

        static GameObject BuildCrosshair()
        {
            UiTokens t = Tokens;
            var go = new GameObject("Crosshair", typeof(RectTransform)) { layer = 5 };
            var root = (RectTransform)go.transform;
            root.sizeDelta = new Vector2(80f, 80f);
            string[] arms = { "Up", "Down", "Left", "Right" };
            var outlines = new Object[4];
            var cores = new Object[4];
            Image dotOutline = CentreBlock(root, "DotOutline", t.Black);
            for (int i = 0; i < 4; i++)
                outlines[i] = CentreBlock(root, arms[i] + "Outline", t.Black);
            Image dot = CentreBlock(root, "Dot", t.Paper);
            for (int i = 0; i < 4; i++)
                cores[i] = CentreBlock(root, arms[i], t.Paper);

            var crosshair = go.AddComponent<UiCrosshair>();
            Set(crosshair, "_tokens", t);
            Set(crosshair, "_dotOutline", dotOutline);
            Set(crosshair, "_dot", dot);
            SetArray(crosshair, "_armOutlines", outlines);
            SetArray(crosshair, "_arms", cores);
            crosshair.Draw();
            return go;
        }

        static Image CentreBlock(Transform parent, string name, Color colour)
        {
            RectTransform rect = Node(parent, name);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(4f, 4f);
            return Img(rect, UiKitArt.White, colour);
        }
    }
}
