# Seal the Deal: UI mockups (pass A "Survey")

**Status:** ready for review, not approved. Mockups only; no Unity UI has changed. Last updated 2026-11-13.

## What was chosen, and why
- **Guide:** the owner's nine references in [References/ui/](../References/ui/README.md) plus art bible §03, §13, §14, §20. Sky and paper white are the two fields; sky sits inside frames (ref 08's window frame is the title screen); nothing is a dark panel.
- **Over the world:** navy type on white plates (90 %), 2 px cobalt rule, hairline edge. Navy on plate is about 14:1 over both the bloom and navy-dark worst cases, which is why plates replaced the first pass's smoked-navy chips.
- **Colour:** the bible's seven swatches plus **Amber #FF9E1B as rival firm and threat** (navy text on an amber plate, 8.2:1). You = cyan ◆, rival = amber ▲: shape and colour, never colour alone.
- **Type:** thin, wide display; neutral sans for UI; mono for numbers (clock and ammo must use the mono: TextMeshPro will not switch on tabular figures). Stand-ins and shipping targets with licences: [research/fonts_by_pass.md](research/fonts_by_pass.md).
- **State is shown by shape:** hover = open corner brackets, focus = ring + snapped brackets + chevron cue, pressed = stamped edge, disabled = dashed outline with the reason in words.
- **Crosshair:** light core with a 1 px near-black edge, drawn in physical pixels; it is visible over white, cobalt and navy (see the HUD crosshair sheet).
- **Game rules drawn:** one shared briefcase, open floor, F to interact, tower = lobby + floors 1–9 + boardroom + roof, 15:00 cap. Rival-progress and checkpoint UI from the GDD are dropped.

## Proposals the owner has not approved
- Amber as rival/threat colour; plate alpha 0.90; Saira Expanded as shipping display face.
- Curtain Wall menu variant (needs a scene blur pass) vs Survey (window frame); owner picks one.
- Drop = G with no pad binding; Interact pad = Y; HUD minimum scale 0.85 ("Interface size" setting).
- Copy written by agents, not from the inventory: listed on each board's DesignNotes.
- Open: "DEAL CLOSED" vs "seal" wording; "CTD-" microtype serials; return point after leaving the building; health bar; tie at the 15:00 cap.

## Files
- Review PNGs: [review/](review/) `00_` kit, `00a_` title frame, `01_menu_*`, `02_settings_*`, `03_hud_*` (each with an `_annotated` copy holding the design notes).
- Generators (the source; re-placing in VectorCraft wipes hand edits): [tools/](tools/README.md) `ctd_ui.py` (library + themes), `make_kit.py`, `make_hero.py`, `make_main_menu.py`, `make_settings.py`, `make_hud.py`, `make_compare.py`.
- VectorCraft documents and exports: `~/UI Art/CloseTheDeal/` (`CTD UI Kit`, `CTD Hero A Survey`, `STD Main Menu …`, `STD Settings`, `STD HUD …`).
- Other directions (B Aero, C Sky, D Seven) live in [passes/](passes/) and are owned by their own lanes.

## What each mockup maps to in code today
- Title and lobby: the menu card in `Assets/_Project/Scripts/UI/GameMenu.cs`, built by `Assets/_Project/Editor/Greybox/GreyboxUiSetup.cs`; every status, failure and join string comes from `Assets/_Project/Scripts/Net/SteamLobby.cs` (codes S1–S20, J1–J4 in [research/ui_inventory.md](research/ui_inventory.md) §1.5).
- In-game menu: the same card's in-game section (the game keeps running).
- HUD: only the crosshair, the Esc chip and `PredictionDebugHud` exist. Briefcase, open floor, clock, health, hands slot, prompts, markers, banners, down/respawn and results all need new code and host-owned synced state.
- Settings: nothing exists; values live in Inspector fields (sensitivity, dead zone, FOV) or ProjectSettings. Rebinding needs the Input System's rebinding plus a save.

## Suggestion: what a Unity implementation session would need (not started)
- A UI-only branch or worktree; claim `GreyboxUiSetup.cs` and the UI scenes on the WORKBOARD first.
- Owner decisions first: menu variant, amber, the open copy questions above, shipping fonts (licences are OFL; the font files must be downloaded with permission).
- uGUI or UI Toolkit choice; CanvasScaler 1920×1080 Expand; HUD bounds to the centre 16:9 on ultrawide; minimum HUD scale 0.85.
- Ship INK2 / INK3 / DISABLED as opaque colours (Linear colour space makes alpha text lighter than the mockups).
- 9-sliced plate, amber plate and keycap sprites; TMP font assets for the three faces; a blur pass only if Curtain Wall is chosen.
- Per HUD element: who owns the data on the host, what is synced, at what rate (AGENTS.md §3), before code.
- Verify with `Close the Deal > Greybox > Capture UI Preview` against these PNGs at 1920×1080 and 1280×720.
