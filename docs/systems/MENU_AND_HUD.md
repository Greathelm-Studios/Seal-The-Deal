# MENU AND HUD

**Status:** Current · **Last updated:** 2026-11-13 · **Verified against:** the UI kit slice 1 commit

## What happens in the game

The game opens on the UI kit's title screen ([UI_KIT.md](UI_KIT.md)): **Host game**, **Join on this PC** (local test mode only), **Settings** and **Quit to desktop**, the Steam or local mode line and the status line. There are no lobby codes. In a scene without the kit, the playtest card shows instead. Once you host or join, the kit's screens fade out and the playtest card fades in: while connecting it shows the status; in a game it shows the player count, **Invite with the Steam overlay** when the overlay exists, a note that friends can join from your name, **Back to game** and **Leave game**. The controls are listed at the bottom of the card.

In game the menu is gone: the crosshair in the centre (drawn from Settings › Accessibility: style, colour, size), **Esc · Menu** top-left and the network readout bottom-left. **Esc** opens the menu again. While the menu is open the mouse is free and your character stands still, so clicking never moves, jumps or fires.

In a scene with the briefcase match ([MATCH.md](MATCH.md)) the card also carries a match section above the in-game buttons: where the match stands, the result and how it was won, and the host's **Start the match** / **Rematch** (clients read "The host can start a rematch."). The match opens the menu when it ends. The HUD adds the clock (top centre), both firms' briefcase and checkpoint (top right, yours first, cyan and amber), the countdown, the F / G prompt under the crosshair and one-off lines at the bottom.

Greybox: this is a playtest tool, not the final lobby. The final one lives in the reception with the secretary (GDD).

## Rules the menu keeps

- **Menu open ⇔ mouse free ⇔ player idle.** One script (`GameMenu`) owns all three so they can't disagree; others open it only through `GameMenu.Open()` and close it through `Resume()`. If something else frees the mouse (the editor's own Esc, alt-tab), the menu opens to match. The kit's title only takes the "not in a game" state (`HandTitleToKit`, `AtTitle`, `StateChanged`).
- **Closing waits for the confirm button to be let go.** The pad's A is both UI Submit and Jump, so a close (Back to game, a match starting, a player spawning) holds the menu open until UI Submit is released.
- The menu opens by itself whenever you have no player: before joining, after leaving, and when the connection drops.
- Every wait says what it's doing in the status line ("Creating a Steam lobby…", "Joining lobby…", "Connecting to Adam's game…").
- A failed join explains itself in the status line, in plain words: friends-only, full, a version mismatch, or a host that can't be reached.
- Buttons that can't be used are dimmed, not hidden, except whole sections that don't apply in the current mode.
- Keyboard focus lands on the likeliest next button when the menu opens.

## Look

Flat dark card, amber for the one primary action per state, white text with a muted grey for secondary lines. Contrast on the card: text 15.6:1, muted 7.5:1, error 8:1, primary button label 10:1. HUD text sits on dark chips so it reads over the light grey walls. TextMeshPro throughout (essentials committed under `Assets/TextMesh Pro/`).

## Scripts and tools

- [GameMenu.cs](../../Assets/_Project/Scripts/UI/GameMenu.cs): state, buttons, the mouse. Text is rebuilt only on lobby events, never per frame.
- [PredictionDebugHud.cs](../../Assets/_Project/Scripts/UI/PredictionDebugHud.cs): the network readout; see NETCODE.md "What to watch". Hidden by Settings › Accessibility › Show network stats (on by default while netcode is tested).
- [GreyboxUiSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs) `Ensure` and Rebuild Game UI also add the UI kit (Close the Deal > Lanes > Set Up UI) and keep it pointed at the menu.
- [GreyboxUiSetup.cs](../../Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs): builds the whole `GameUI` canvas. **Close the Deal > Greybox > Rebuild Game UI** deletes and rebuilds it after asking, so change layout, copy and colours there rather than in the scene; hand edits in the scene are lost on a rebuild.
- [GreyboxUiPreview.cs](../../Assets/_Project/Editor/Greybox/GreyboxUiPreview.cs): **Capture UI Preview** renders the start, hosting, local and in-game states to PNGs without entering Play Mode.

## Known limitations

- Settings has Controls, Video, Audio and Accessibility ([UI_KIT.md](UI_KIT.md) "Settings"). Mouse sensitivity, invert and key rebinding show once the movement lane's camera and input reader read `InputPrefs`; field of view has no hook yet.
- Hosting, joining, failures and the in-game menu still use the playtest card (dark, greybox look) until the next UI slices.
- No player names over heads and no team indicator yet.
- The playtest card fades in but has no other motion and no sounds.
