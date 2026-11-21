using System.Collections.Generic;
using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CloseTheDeal.Editor.UI.UiKitAssets;
using static CloseTheDeal.Editor.UI.UiKitScreens;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// Builds the in-game menu on the Settings paper grid, with navigation on the left
    /// and the live match section and reception seats on the right.
    /// </summary>
    internal static class UiKitGameMenuBuilder
    {
        const float PaneY = 120f;
        const float X = 136f, W = 448f;
        const float SideX = 656f, SideW = 624f;
        const float SeatsY = 340f; // drawn just under a 196 px match section; the screen moves it to fit the real one
        const int SeatsPerFirm = 4;

        internal static GameObject Build()
        {
            UiTokens t = Tokens;
            RectTransform root = ScreenRoot("InGameMenu");
            RectTransform frame = DesignFrame(root, "Content");
            frame.gameObject.AddComponent<CanvasGroup>();
            var reveal = new List<CanvasGroup>();

            RectTransform head = RevealLine(frame, "Head");
            reveal.Add(head.GetComponent<CanvasGroup>());
            Box(head, "Bullet", X, 164f, 8f, 8f, t.Black);
            Label(head, "Caption", X + 16f, 156f, 300f, 24f, "§ MENU", t.Section, t.Navy).fontSize = 14f;
            Label(head, "State", X + W - 220f, 156f, 220f, 24f, "game running", t.Caption, t.Ink3, TextAlignmentOptions.MidlineRight);
            Box(head, "Rule", X, 188f, W, 1f, t.Black);
            Label(head, "Line", X, 200f, W, 28f, "The game continues while this is open.", t.Body, t.Ink2);

            RectTransform buttons = RevealLine(frame, "Buttons");
            reveal.Add(buttons.GetComponent<CanvasGroup>());
            RectTransform column = At(buttons, "Column", X + 40f, 248f, W - 40f, 268f);
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
            foreach (var action in new[] { back, invite, settings, leave })
                AddV3FocusMark(action);
            SetLabel(back, "Back to game");
            SetLabel(invite, "Invite friends");
            SetLabel(settings, "Settings");
            SetLabel(leave, "Leave game");
            back.Description = "Close this menu and return to the office.";
            invite.Description = "Open Steam's invite list. Friends you invite join straight into this game.";
            settings.Description = "Controls, video, audio and accessibility. The game keeps running behind it.";
            leave.Description = "Go back to the title. Your team carries on without you.";

            RectTransform info = RevealLine(frame, "FocusInfo");
            reveal.Add(info.GetComponent<CanvasGroup>());
            TextMeshProUGUI heading = Label(info, "FocusHeading", X, 540f, W, 24f, "§ BACK TO GAME", t.Section, t.Navy);
            Box(info, "FocusRule", X, 566f, W, 1f, t.Hair);
            TextMeshProUGUI body = Label(info, "FocusBody", X, 576f, W, 72f, back.Description, t.Body, t.Ink2, TextAlignmentOptions.TopLeft, true);

            RectTransform sessionLine = RevealLine(frame, "Session");
            reveal.Add(sessionLine.GetComponent<CanvasGroup>());
            Label(sessionLine, "SessionCaption", X, 660f, W, 18f, "§ SESSION", t.Section, t.Ink3).fontSize = 12f;
            var session = Place<UiStatusLine>(UiKitComponents.StatusLine, sessionLine, "SessionLine", X, 684f);
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
            ApplyMenuStyle(root, t);
            return root.gameObject;
        }

        // Shared by the builder and the one-time upgrade of existing screen instances.
        internal static void ApplyMenuStyle(RectTransform root, UiTokens t)
        {
            var frame = (RectTransform)root.Find("Content");
            if (root.Find("Paper") == null)
            {
                var paper = Img(Fill(root, "Paper"), null, t.Paper, raycast: true);
                paper.transform.SetAsFirstSibling();
                var grid = AssetDatabase.LoadAssetAtPath<Sprite>(UiKitArt.Folder + "/Kit_Grid.png");
                Img(Fill(root, "Grid"), grid, t.Grid, Image.Type.Tiled).transform.SetSiblingIndex(1);
            }

            var head = frame.Find("Head");
            var caption = head.Find("Caption").GetComponent<TMP_Text>();
            caption.text = "SEAL THE DEAL  ·  SESSION";
            caption.fontSize = t.Body.Size;
            UiKitAssets.Place(caption.rectTransform, 116, 26, 780, 28);
            UiKitAssets.Place((RectTransform)head.Find("Bullet"), 96, 34, 8, 8);
            if (head.Find("Heading") == null)
                Label(head, "Heading", 96, 78, 1050, 60, "MATCH MENU", t.Heading, t.Navy);
            UiKitAssets.Place((RectTransform)head.Find("Line"), 96, 145, 1050, 36);
            var title = head.Find("Heading").GetComponent<TMP_Text>();
            UiKitAssets.Place(title.rectTransform, 96, 65, 1050, 84);
            title.overflowMode = TextOverflowModes.Overflow;
            var screen = root.GetComponent<InGameMenuScreen>();
            Set(screen, "_pageTitle", title);
            Set(screen, "_runningNotice", head.Find("Line").gameObject);
            head.Find("State").gameObject.SetActive(false);
            UiKitAssets.Place((RectTransform)head.Find("Rule"), 96, 216, 1728, 1);

            var buttons = frame.Find("Buttons");
            UiKitAssets.Place((RectTransform)buttons.Find("Column"), 96, 270, 458, 300);
            UiKitAssets.Place((RectTransform)frame.Find("FocusInfo/FocusHeading"), 96, 588, 458, 28);
            UiKitAssets.Place((RectTransform)frame.Find("FocusInfo/FocusRule"), 96, 625, 458, 1);
            UiKitAssets.Place((RectTransform)frame.Find("FocusInfo/FocusBody"), 96, 643, 458, 94);
            UiKitAssets.Place((RectTransform)frame.Find("Session/SessionCaption"), 96, 770, 458, 28);
            UiKitAssets.Place((RectTransform)frame.Find("Session/SessionLine"), 96, 810, 458, 86);
            UiKitAssets.Place((RectTransform)frame.Find("Hints/HintRule"), 96, 928, 1728, 1);
            UiKitAssets.Place((RectTransform)frame.Find("Hints/SelectHint"), 96, 965, 150, 42);
            UiKitAssets.Place((RectTransform)frame.Find("Hints/BackHint"), 263, 965, 240, 42);

            UiKitAssets.Place((RectTransform)frame.Find("MatchSlot"), 720, 263, 1104, 400);
            var block = frame.Find("Reception/Block");
            // Keep the dynamic match slot height. Place children relative to the same design frame.
            float left = 720, right = 1824;
            UiKitAssets.Place((RectTransform)block.Find("Bullet"), left, 524, 8, 8);
            UiKitAssets.Place((RectTransform)block.Find("Caption"), left + 20, 514, 500, 32);
            block.Find("Caption").GetComponent<TMP_Text>().fontSize = t.Body.Size;
            UiKitAssets.Place((RectTransform)block.Find("ReadyCount"), right - 360, 514, 360, 32);
            UiKitAssets.Place((RectTransform)block.Find("Rule"), left, 553, right - left, 1);
            for (int firm = 0; firm < 2; firm++)
            {
                float x = left + firm * 574;
                UiKitAssets.Place((RectTransform)block.Find("FirmMark" + firm), x, 587, 9, 9);
                UiKitAssets.Place((RectTransform)block.Find("Firm" + firm), x + 21, 571, 508, 42);
                UiKitAssets.Place((RectTransform)block.Find("FirmRule" + firm), x, 618, 530, 1);
                for (int seat = 0; seat < SeatsPerFirm; seat++)
                {
                    string id = firm + "_" + seat;
                    float y = 633 + seat * 56;
                    UiKitAssets.Place((RectTransform)block.Find("SeatMark" + id), x, y + 10, 8, 8);
                    UiKitAssets.Place((RectTransform)block.Find("SeatRing" + id), x, y + 10, 8, 8);
                    UiKitAssets.Place((RectTransform)block.Find("SeatName" + id), x + 21, y, 506, 28);
                    UiKitAssets.Place((RectTransform)block.Find("SeatState" + id), x + 21, y + 27, 506, 24);
                }
            }
        }

        sealed class ReceptionRefs
        {
            public TMP_Text Ready;
            public readonly List<Object> FirmNames = new(), FirmMarks = new(), Names = new(), States = new(), Marks = new(), Rings = new();
        }

        /// <summary>Reception's seats on their own glass pane: both firms side by side, four seats each.</summary>
        static ReceptionRefs BuildReception(RectTransform parent)
        {
            UiTokens t = Tokens;
            var refs = new ReceptionRefs();
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
                    refs.Marks.Add(Box(parent, "SeatMark" + n, x, y + 8f, 10f, 10f, t.Cyan));
                    refs.Rings.Add(Box(parent, "SeatRing" + n, x, y + 8f, 10f, 10f, t.Ink3, UiKitArt.Outline1));
                    var name = Label(parent, "SeatName" + n, x + 18f, y, colW - 18f, 25f, "Seat available", t.Body, t.Ink3);
                    name.richText = false;
                    refs.Names.Add(name);
                    refs.States.Add(Label(parent, "SeatState" + n, x + 18f, y + 25f, colW - 18f, 20f, string.Empty, t.Caption, t.Ink2));
                }
            }

            return refs;
        }
    }
}
