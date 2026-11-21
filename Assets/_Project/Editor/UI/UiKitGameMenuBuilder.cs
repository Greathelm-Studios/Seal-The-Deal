using System.Collections.Generic;
using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CloseTheDeal.Editor.UI.UiKitAssets;
using static CloseTheDeal.Editor.UI.UiKitScreens;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>Builds InGameMenu.prefab with navigation pane, MatchSlot, and reception seat pane.</summary>
    internal static class UiKitGameMenuBuilder
    {
        const float PaneX = 96f, PaneY = 120f, PaneW = 528f, PaneH = 724f;
        const float X = 136f, W = 448f;
        const float SideX = 656f, SideW = 624f;
        const float SeatsY = 340f, SeatsH = 320f; // drawn just under a 196 px match section; the screen moves it to fit the real one
        const int SeatsPerFirm = 4;

        internal static GameObject Build()
        {
            UiTokens t = Tokens;
            RectTransform root = ScreenRoot("InGameMenu");
            RectTransform frame = DesignFrame(root, "Content");
            frame.gameObject.AddComponent<CanvasGroup>();
            var reveal = new List<CanvasGroup>();

            var pane = Place<RectTransform>(UiKitComponents.GlassPane, frame, "Pane", PaneX, PaneY);
            pane.sizeDelta = new Vector2(PaneW, PaneH);

            RectTransform head = RevealLine(frame, "Head");
            reveal.Add(head.GetComponent<CanvasGroup>());
            Box(head, "Bullet", X, 164f, 8f, 8f, t.Black);
            Label(head, "Caption", X + 16f, 156f, 300f, 24f, "§ MENU", t.Section, t.Navy).fontSize = 14f;
            Label(head, "State", X + W - 220f, 156f, 220f, 24f, "game running", t.Caption, t.Ink3, TextAlignmentOptions.MidlineRight);
            Box(head, "Rule", X, 188f, W, 1f, t.Black);
            Label(head, "Line", X, 200f, W, 28f, "The game continues while this is open.", t.Body, t.Ink2);

            RectTransform buttons = RevealLine(frame, "Buttons");
            reveal.Add(buttons.GetComponent<CanvasGroup>());
            RectTransform column = At(buttons, "Column", X, 248f, W, 268f);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = t.SpaceS;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var back = Instance<UiButton>(UiKitComponents.Primary, column, "BackToGame");
            var invite = Instance<UiButton>(UiKitComponents.Secondary, column, "InviteFriends");
            var settings = Instance<UiButton>(UiKitComponents.Secondary, column, "Settings");
            var leave = Instance<UiButton>(UiKitComponents.Secondary, column, "LeaveGame");
            SetLabel(back, "Back to game");
            SetLabel(invite, "Invite friends");
            SetLabel(settings, "Settings");
            SetLabel(leave, "Leave game");
            back.Description = "Close this menu. The game kept running while it was open.";
            invite.Description = "Open Steam's invite list. Friends you invite join straight into this game.";
            settings.Description = "Controls, video, audio and accessibility. The game keeps running behind it.";
            leave.Description = "Go back to the title. Your team carries on without you.";

            RectTransform info = RevealLine(frame, "FocusInfo");
            reveal.Add(info.GetComponent<CanvasGroup>());
            TextMeshProUGUI heading = Label(info, "FocusHeading", X, 476f, W, 24f, "§ BACK TO GAME", t.Section, t.Navy);
            Box(info, "FocusRule", X, 502f, W, 1f, t.Hair);
            TextMeshProUGUI body = Label(info, "FocusBody", X, 512f, W, 84f, back.Description, t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);

            RectTransform sessionLine = RevealLine(frame, "Session");
            reveal.Add(sessionLine.GetComponent<CanvasGroup>());
            Label(sessionLine, "SessionCaption", X, 612f, W, 18f, "§ SESSION", t.Section, t.Ink3).fontSize = 12f;
            var session = Place<UiStatusLine>(UiKitComponents.StatusLine, sessionLine, "SessionLine", X, 636f);
            ((RectTransform)session.transform).sizeDelta = new Vector2(W, 76f);
            ((RectTransform)session.transform.Find("Text")).sizeDelta = new Vector2(W - 22f, 76f);

            RectTransform hints = RevealLine(frame, "Hints");
            reveal.Add(hints.GetComponent<CanvasGroup>());
            Box(hints, "HintRule", X, 764f, W, 1f, t.Hair);
            Hint(hints, "SelectHint", X, 780f, "Select", "UI/Submit");
            Hint(hints, "BackHint", X + 150f, 780f, "Back to game", "UI/Cancel", width: 220f);

            RectTransform slot = At(frame, "MatchSlot", SideX, PaneY, SideW, 400f);
            slot.gameObject.AddComponent<CanvasGroup>();
            reveal.Add(slot.GetComponent<CanvasGroup>());
            var slotLayout = slot.gameObject.AddComponent<VerticalLayoutGroup>();
            slotLayout.childControlWidth = true;
            slotLayout.childControlHeight = true;
            slotLayout.childForceExpandWidth = true;
            slotLayout.childForceExpandHeight = false;
            slotLayout.childAlignment = TextAnchor.UpperLeft;

            RectTransform seats = RevealLine(frame, "Reception");
            reveal.Add(seats.GetComponent<CanvasGroup>());
            RectTransform block = Fill(seats, "Block"); // moved by the screen to sit under the match section (the reveal moves its parent)
            ReceptionRefs refs = BuildReception(block);

            var screen = root.gameObject.AddComponent<InGameMenuScreen>();
            WireScreen(screen, frame, reveal, back);
            SetValue(screen, "_isDialog", 1f);
            Set(screen, "_back", back);
            Set(screen, "_invite", invite);
            Set(screen, "_settings", settings);
            Set(screen, "_leave", leave);
            Set(screen, "_focusHeading", heading);
            Set(screen, "_focusBody", body);
            Set(screen, "_session", session);
            Set(screen, "_matchSlot", slot);
            Set(screen, "_reception", seats.gameObject);
            Set(screen, "_receptionBlock", block);
            Set(screen, "_readyCount", refs.Ready);
            SetArray(screen, "_firmNames", refs.FirmNames.ToArray());
            SetArray(screen, "_firmMarks", refs.FirmMarks.ToArray());
            SetArray(screen, "_seatNames", refs.Names.ToArray());
            SetArray(screen, "_seatStates", refs.States.ToArray());
            SetArray(screen, "_seatMarks", refs.Marks.ToArray());
            SetArray(screen, "_seatRings", refs.Rings.ToArray());
            return root.gameObject;
        }

        sealed class ReceptionRefs
        {
            public TMP_Text Ready;
            public readonly List<Object> FirmNames = new(), FirmMarks = new(), Names = new(), States = new(), Marks = new(), Rings = new();
        }

        static ReceptionRefs BuildReception(RectTransform parent)
        {
            UiTokens t = Tokens;
            var refs = new ReceptionRefs();
            var pane = Place<RectTransform>(UiKitComponents.GlassPane, parent, "Pane", SideX, SeatsY);
            pane.sizeDelta = new Vector2(SideW, SeatsH);

            float x0 = SideX + 32f, inner = SideW - 64f;
            Box(parent, "Bullet", x0, SeatsY + 40f, 8f, 8f, t.Black);
            Label(parent, "Caption", x0 + 16f, SeatsY + 32f, 300f, 24f, "§ RECEPTION", t.Section, t.Navy).fontSize = 14f;
            refs.Ready = Label(parent, "ReadyCount", x0 + inner - 260f, SeatsY + 32f, 260f, 24f, string.Empty, t.Caption, t.Ink3, TextAlignmentOptions.MidlineRight);
            Box(parent, "Rule", x0, SeatsY + 56f, inner, 1f, t.Black);

            float colW = (inner - 16f) / 2f;
            for (int firm = 0; firm < 2; firm++)
            {
                float x = x0 + firm * (colW + 16f);
                refs.FirmMarks.Add(Box(parent, "FirmMark" + firm, x, SeatsY + 80f, 10f, 10f, firm == 0 ? t.Cyan : t.Amber));
                refs.FirmNames.Add(Label(parent, "Firm" + firm, x + 18f, SeatsY + 70f, colW - 18f, 28f, string.Empty, t.Item, t.Navy));
                Box(parent, "FirmRule" + firm, x, SeatsY + 104f, colW, 1f, t.Hair);
                for (int seat = 0; seat < SeatsPerFirm; seat++)
                {
                    float y = SeatsY + 112f + seat * 48f;
                    string n = firm + "_" + seat;
                    refs.Marks.Add(Box(parent, "SeatMark" + n, x, y + 19f, 10f, 10f, t.Cyan));
                    refs.Rings.Add(Box(parent, "SeatRing" + n, x, y + 19f, 10f, 10f, t.Ink3, UiKitArt.Outline1));
                    refs.Names.Add(Label(parent, "SeatName" + n, x + 18f, y, colW - 104f, 48f, "Seat available", t.Body, t.Ink3));
                    refs.States.Add(Label(parent, "SeatState" + n, x + colW - 84f, y, 84f, 48f, string.Empty, t.Caption, t.Ink2, TextAlignmentOptions.MidlineRight));
                }
            }

            return refs;
        }
    }
}
