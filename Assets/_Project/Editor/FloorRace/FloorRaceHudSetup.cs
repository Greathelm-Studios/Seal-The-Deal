using CloseTheDeal.Editor.UI;
using CloseTheDeal.FloorRace;
using CloseTheDeal.MatchMode;
using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.FloorRace
{
    /// <summary>Builds the floor race HUD under the game menu's Hud from UI kit pieces.</summary>
    static class FloorRaceHudSetup
    {
        const string HudName = "FloorRaceHud";
        const string TokensPath = "Assets/_Project/UI/Tokens/UiTokens.asset";
        const string PlatePath = "Assets/_Project/UI/Kit/Plate.prefab";
        const float Margin = 24f, Pad = 16f;
        const float FloorWidth = 280f, FloorHeight = 76f;
        const float GoalWidth = 460f, GoalHeight = 76f;
        const float CashWidth = 460f, CashHeight = 44f;
        const float TrackerBottom = 116f;
        const float TitleSize = 64f;

        public static void AddToOpenScene()
        {
            GameMenu menu = Object.FindAnyObjectByType<GameMenu>();
            Transform hud = menu != null ? menu.transform.Find("Hud") : null;
            var race = Object.FindAnyObjectByType<FloorRaceDirector>();
            var match = Object.FindAnyObjectByType<MatchDirector>();
            var tokens = AssetDatabase.LoadAssetAtPath<UiTokens>(TokensPath);
            var plate = AssetDatabase.LoadAssetAtPath<GameObject>(PlatePath);
            if (hud == null || race == null || match == null || tokens == null || plate == null)
            {
                Debug.LogError("[Floor Race] No HUD added: the scene needs the game menu, the Match, the Floor Race object and the UI kit.");
                return;
            }

            Transform old = hud.Find(HudName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform holder = UiKitAssets.Node(hud, HudName);
            UiKitAssets.Stretch(holder);
            RectTransform root = UiKitAssets.Node(holder, "Root");
            UiKitAssets.Stretch(root);

            // Top centre, where the match HUD's clock sits in the tower mode; this mode has no clock.
            RectTransform floor = Plate(plate, root, "Floor", new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(FloorWidth, FloorHeight));
            TMP_Text floorText = UiKitAssets.Label(floor, "Number", 0f, 8f, FloorWidth, 36f, "FLOOR 1 / 10", tokens.Item, tokens.Navy, TextAlignmentOptions.Midline);
            TMP_Text roomText = UiKitAssets.Label(floor, "Room", 0f, 46f, FloorWidth, 20f, "", tokens.Caption, tokens.Ink3, TextAlignmentOptions.Midline);

            // Top right, under the match HUD's briefcase tracker (88 high at -20).
            RectTransform goal = Plate(plate, root, "Goal", new Vector2(1f, 1f), new Vector2(-Margin, -TrackerBottom), new Vector2(GoalWidth, GoalHeight));
            UiKitAssets.Label(goal, "Caption", Pad, 10f, GoalWidth - 2f * Pad, 18f, "OBJECTIVE", tokens.Caption, tokens.Ink3);
            TMP_Text goalText = UiKitAssets.Label(goal, "Line", Pad, 32f, GoalWidth - 2f * Pad, 32f, "", tokens.Item, tokens.Navy);

            RectTransform cash = Plate(plate, root, "Cash", new Vector2(1f, 1f), new Vector2(-Margin, -TrackerBottom - GoalHeight - 8f), new Vector2(CashWidth, CashHeight));
            TMP_Text cashText = UiKitAssets.Label(cash, "Line", Pad, 6f, CashWidth - 2f * Pad, 32f, "", tokens.Item, tokens.Cobalt);
            CanvasGroup cashGroup = Quiet(cash);

            RectTransform transition = UiKitAssets.Node(root, "Transition");
            UiKitAssets.Stretch(transition);
            UiKitAssets.Img(transition, null, tokens.Navy);
            CanvasGroup transitionGroup = Quiet(transition);
            // The kit's Title face draws in navy whatever the colour, which vanishes on this navy screen.
            UiTypeRole big = tokens.Item;
            big.Size = TitleSize;
            TMP_Text title = Centred(transition, "Title", 60f, 96f, "FLOOR 2", big, tokens.Paper);
            TMP_Text line = Centred(transition, "Line", -30f, 40f, "", tokens.Body, tokens.Silver);

            var component = holder.gameObject.AddComponent<FloorRaceHud>();
            UiKitAssets.Set(component, "_race", race);
            UiKitAssets.Set(component, "_match", match);
            UiKitAssets.Set(component, "_root", root.gameObject);
            UiKitAssets.Set(component, "_floorText", floorText);
            UiKitAssets.Set(component, "_roomText", roomText);
            UiKitAssets.Set(component, "_goalText", goalText);
            UiKitAssets.Set(component, "_cashGroup", cashGroup);
            UiKitAssets.Set(component, "_cashText", cashText);
            UiKitAssets.Set(component, "_transitionGroup", transitionGroup);
            UiKitAssets.Set(component, "_transitionTitle", title);
            UiKitAssets.Set(component, "_transitionLine", line);
            root.gameObject.SetActive(false);
        }

        static RectTransform Plate(GameObject prefab, Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = UiKitAssets.Instance<RectTransform>(prefab, parent, name);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static CanvasGroup Quiet(RectTransform rect)
        {
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        static TMP_Text Centred(RectTransform parent, string name, float y, float height, string content, UiTypeRole role, Color colour)
        {
            TextMeshProUGUI text = UiKitAssets.Label(parent, name, 0f, 0f, 1200f, height, content, role, colour, TextAlignmentOptions.Midline);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            return text;
        }
    }
}
