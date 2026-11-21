using System.Collections.Generic;
using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CloseTheDeal.Editor.UI.UiKitAssets;
using static CloseTheDeal.Editor.UI.UiKitScreens;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// Builds the two panels that sit over the title's middle and right columns (the window and the title lockup
    /// stay): the session panel (hosting, joining, loading, with an honest progress track and Cancel) and the problem
    /// panel (an amber plate naming what went wrong and the button that fixes it, plus Back). Mockups:
    /// ArtSource/UI/review/01_menu_survey_03_hosting, 04_joining, 07_failed_full, 08_failed_friends. Coordinates are
    /// the title's 1920 x 1080 frame. Both are dialogs without a veil: the title stays readable behind them.
    /// </summary>
    internal static class UiKitSessionBuilder
    {
        const float Left = 992f;
        const float Width = 816f;

        internal static GameObject BuildSession()
        {
            UiTokens t = Tokens;
            RectTransform root = ScreenRoot("SessionPanel");
            RectTransform frame = DesignFrame(root, "Content");
            frame.gameObject.AddComponent<CanvasGroup>();
            var reveal = new List<CanvasGroup>();

            RectTransform head = RevealLine(frame, "Head");
            reveal.Add(head.GetComponent<CanvasGroup>());
            TMP_Text caption = Caption(head, out TMP_Text state);

            RectTransform statusLine = RevealLine(frame, "Status");
            reveal.Add(statusLine.GetComponent<CanvasGroup>());
            var status = Place<UiStatusLine>(UiKitComponents.StatusLine, statusLine, "StatusLine", Left, 764f);
            ((RectTransform)status.transform).sizeDelta = new Vector2(Width, 52f);
            ((RectTransform)status.transform.Find("Text")).sizeDelta = new Vector2(Width - 22f, 52f);

            RectTransform progress = RevealLine(frame, "Progress");
            reveal.Add(progress.GetComponent<CanvasGroup>());
            var chevrons = new Graphic[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform chevron = At(progress, "Step" + i, Left + i * 14f, 834f, 12f, 16f);
                chevrons[i] = Img(chevron, UiKitArt.Pointer, t.Hair);
                ((Image)chevrons[i]).preserveAspect = true;
            }

            Box(progress, "Track", Left + 56f, 841f, Width - 56f, 2f, t.Hair);
            RectTransform fill = At(progress, "Fill", 0f, 0f, 0f, 2f);
            fill.SetParent(progress.Find("Track"), false);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = new Vector2(0f, 0f);
            Img(fill, UiKitArt.White, t.Cobalt);
            TMP_Text detail = Label(progress, "Detail", Left, 858f, Width, 20f, "WAITING FOR STEAM", t.Caption, t.Ink3);

            RectTransform actions = RevealLine(frame, "Actions");
            reveal.Add(actions.GetComponent<CanvasGroup>());
            var cancel = Place<UiButton>(UiKitComponents.Secondary, actions, "Cancel", Left, 904f);
            ((RectTransform)cancel.transform).sizeDelta = new Vector2(240f, t.SecondaryHeight);
            SetLabel(cancel, "Cancel");
            Hint(actions, "CancelHint", Left + 264f, 912f, "Cancel", "UI/Cancel");
            cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit };

            var panel = root.gameObject.AddComponent<SessionPanel>();
            WireScreen(panel, frame, reveal, cancel);
            SetValue(panel, "_isDialog", 1f);
            Set(panel, "_caption", caption);
            Set(panel, "_state", state);
            Set(panel, "_status", status);
            Set(panel, "_fill", fill);
            SetArray(panel, "_chevrons", chevrons);
            Set(panel, "_detail", detail);
            Set(panel, "_cancel", cancel);
            return root.gameObject;
        }

        internal static GameObject BuildProblem()
        {
            UiTokens t = Tokens;
            RectTransform root = ScreenRoot("ProblemPanel");
            RectTransform frame = DesignFrame(root, "Content");
            frame.gameObject.AddComponent<CanvasGroup>();
            var reveal = new List<CanvasGroup>();

            RectTransform head = RevealLine(frame, "Head");
            reveal.Add(head.GetComponent<CanvasGroup>());
            TMP_Text caption = Caption(head, out TMP_Text state);
            state.text = "action needed";

            RectTransform notice = RevealLine(frame, "Notice");
            reveal.Add(notice.GetComponent<CanvasGroup>());
            Box(notice, "Plate", Left, 760f, Width, 124f, t.Amber);
            Box(notice, "Keyline", Left, 760f, Width, 124f, t.Navy, UiKitArt.Outline1);
            Box(notice, "Mark", Left + 24f, 784f, 12f, 12f, t.Navy); // the refusal mark: a navy square on amber
            TextMeshProUGUI headline = Label(notice, "Headline", Left + 52f, 772f, Width - 76f, 36f, "Unable to join. The game is full (8 players).", t.Item, t.Navy);
            headline.fontSize = 22f;
            headline.overflowMode = TextOverflowModes.Ellipsis;
            TextMeshProUGUI body = Label(notice, "Body", Left + 52f, 812f, Width - 76f, 60f,
                "A seat opens when a player leaves. Try again in a moment, or host your own game.", t.Body, t.Navy, TextAlignmentOptions.TopLeft, true);

            RectTransform actions = RevealLine(frame, "Actions");
            reveal.Add(actions.GetComponent<CanvasGroup>());
            var fix = Place<UiButton>(UiKitComponents.Primary, actions, "Fix", Left, 904f);
            ((RectTransform)fix.transform).sizeDelta = new Vector2(300f, t.SecondaryHeight);
            SetLabel(fix, "Try again");
            var back = Place<UiButton>(UiKitComponents.Secondary, actions, "Back", Left + 324f, 904f);
            ((RectTransform)back.transform).sizeDelta = new Vector2(180f, t.SecondaryHeight);
            SetLabel(back, "Back");
            Hint(actions, "BackHint", Left + 528f, 912f, "Back", "UI/Cancel");

            var panel = root.gameObject.AddComponent<ProblemPanel>();
            WireScreen(panel, frame, reveal, fix);
            SetValue(panel, "_isDialog", 1f);
            Set(panel, "_caption", caption);
            Set(panel, "_headline", headline);
            Set(panel, "_body", body);
            Set(panel, "_fix", fix);
            Set(panel, "_back", back);
            return root.gameObject;
        }

        /// <summary>§ caption on the left, a short state word on the right, a black rule under both.</summary>
        static TMP_Text Caption(Transform parent, out TMP_Text state)
        {
            UiTokens t = Tokens;
            Box(parent, "Bullet", Left, 728f, 8f, 8f, t.Black);
            TextMeshProUGUI caption = Label(parent, "Caption", Left + 16f, 720f, 600f, 24f, "§ 1  JOIN GAME", t.Section, t.Navy);
            caption.fontSize = 14f;
            state = Label(parent, "State", Left + Width - 300f, 720f, 300f, 24f, "connecting", t.Caption, t.Ink3, TextAlignmentOptions.MidlineRight);
            Box(parent, "Rule", Left, 748f, Width, 1f, t.Black);
            return caption;
        }
    }
}
