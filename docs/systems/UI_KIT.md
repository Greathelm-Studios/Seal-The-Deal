# UI KIT

**Status:** Current (slice 2: title window, every settings tab, sound through lane 9's player) · **Last updated:** 2026-11-14

## What the player sees

The game opens on the title: a white window frame with depth (lit mullion faces, silver reveals, a transom shadow, a black-edged sill) over a drawn sky with stepped cel cumulus and the rival firm's tower under trace brackets, sun falling through it in two hard slabs across the paper below. Low on paper: **SEAL THE DEAL**, the status and connection lines, the focused button's explanation, the keys row (Enter / A select, Esc / B back, arrows / stick move), a microtype strip, the build line, barcode and corner ticks, and the menu: **Host game**, **Join on this PC** (local test mode only), **Settings**, **Quit to desktop**.
Every control answers: hovering lifts it and closes its corner brackets, pressing stamps it down, letting go confirms with a small cyan tick, and each of those asks for a sound.
Keys and the pad show a focus ring with snapped brackets and a chevron that glide to the next control; with the mouse those marks stay hidden.
A greyed-out control says why when pressed (amber plate, a shake, the reason in the status line).
Esc / the pad's B goes back; at the title it asks **Quit to desktop?** with **Stay** focused.
**Settings** has four tabs (Q / E or the shoulder buttons switch; the cobalt rule glides to the open tab and the page slides 12 px): Controls (look rows and the key list; rebinding asks before taking a key another action uses), Video (display mode, resolution with a 10-second revert, vertical sync, frame rate limit, quality), Audio (five volumes, a test sound and a live output meter) and Accessibility (as before). The right column always shows the focused row's explanation above the tab's live preview. Changes apply at once and come back after a relaunch.
Full screens change through paper white: the old screen's content fades and its sky rolls up, then the new one's sky rolls down and its lines reveal. No mixed mid-tone.
Once you host or join, the kit's screens fade out and the playtest card (MENU_AND_HUD.md) takes over until the in-game menu slice lands.

## uGUI, not UI Toolkit

The kit is uGUI + TextMeshPro + CanvasScaler.
- The deciding test: lane 7's glass is a URP material. A uGUI `Image` takes any material, so a pane switches to real glass by assigning one material (`UiGlassPane.GlassMaterial`). UI Toolkit panels can't take a custom shader per element in this Unity version; it would need a render-texture detour.
- Everything already in the game (GameMenu, every lane's HUD, the lobby) is uGUI; one system means one focus model and one EventSystem.
- uGUI `Selectable` gives keyboard and pad navigation, Submit and Cancel through the Input System UI module for free.

## How the pieces fit

```
UiTokens.asset ──► every component (colours, type, sizes, motion, repeat timing)
UiSettings (PlayerPrefs) ──Changed──► UiCrosshair, PredictionDebugHud, UiMotion.Reduced
GameMenu ──StateChanged / AtTitle──► UiRoot ──► UiScreenStack ──► TitleScreen ⇄ SettingsScreen, QuitDialog
controls ──► UiControlFx ──► UiMotion (one driver, unscaled time)
controls ──► UiSound.Play(event) ──► Player (lane 9's UiSoundPlayer, once registered)
controls ──OnSelect──► UiFocusFrame (ring + brackets + chevron)
```

- **GameMenu still owns the rule** menu open ⇔ mouse free ⇔ player idle. The kit only takes the "not in a game" state (`HandTitleToKit`).
- **Closing waits for Submit to be let go.** The pad's A is both UI Submit and Jump (and Enter was Attack until the movement lane's rework), so `GameMenu` keeps the menu open until UI Submit reads released (`UiInput.SubmitHeld`), for Back to game, Esc / Start and a player spawning alike. The actions asset is unchanged.

## Tokens

One asset, [UiTokens.asset](../../Assets/_Project/UI/Tokens/UiTokens.asset), designer-owned (never written at runtime; the tool only creates it when missing). Every field has a tooltip.
- Colours: the bible's seven swatches plus Amber `#FF9E1B` (rival and refusals only), a deeper cobalt for the primary press, and **opaque** Ink 2 / Ink 3 / Disabled / Hair (alpha text renders lighter in Linear).
- Glass: plate 0.90, frosted 0.86, clear 0.40, veil 0.60 (paper, never a dark dim).
- Type roles: Title, Heading, Item, Body, Label, Section, Caption, Micro, each with font, size, tracking, style and the **target face** (Saira Expanded display, Archivo SemiExpanded UI, IBM Plex Mono data; fonts_by_pass.md pass A). Every role uses TMP's Liberation Sans until those fonts are licensed and imported.
- Spacing, heights, radius 0, hit padding 8 px.
- Motion: every value in feel_wii_ds.md §2.2 (hover 100 / 70 ms, press 40 ms OUT, release spring ζ 1 T 0.16, focus travel ζ 1 T 0.12, refusal shake ζ 0.25 T 0.08 at 6 px, panels 200 / 140 ms, reveal 16 ms stagger, reduced-motion fade 160 ms, breathing after 3 s, 2.4 s period) and the four easing curves.
- Input: repeat after 0.4 s, then every 0.1 s (applied to the EventSystem). Hover sound on/off (the feel table marks it a proposal).

## Motion

`UiMotion` is the one driver: components `Wake` themselves while moving and drop out when settled; it ticks on unscaled time, allocates nothing per frame, and can be paused and stepped by hand (`Paused`, `Tick`) for captures.
`UiSpring` is the ζ / T spring from the feel table (interruptible: change `Target` mid-flight). `UiTween` runs cubic-bezier curves.
**Reduce motion** (Settings) is the PlayerPrefs key `ui.reducedMotion`, shared with the title lane's `UiTransition`: slides and zooms become fades, the shake and breathing stop, the press stops moving the plate.

## Sound

Lane 9's `UiSoundPlayerAdapter` registers itself as `UiSound.Player` before the first scene loads; the capture run logs each clip played and the mixer group it routes to.

Call `UiSound.Play(UiSoundEvent.X)`; never load a clip. `UiSound.PlayHover()` and `UiSound.PlaySlider(value01)` for the two pitched ones. `UiSound.Played` sees every request (logs, checks).
The sound lane registers `UiSound.Player` (its `UiSoundPlayer` through `IUiSoundPlayer`); until that is on the branch every call is silent.
Events (frozen with lane 9; append only): None, HoverMove, Focus, Select, Confirm, Back, Error, TabChange, ToggleOn, ToggleOff, SliderTick, DropdownOpen, DropdownClose, KeyBindListen, KeyBindSet, PanelOpen, PanelClose, Transition, Notification, LobbyReady, LobbyUnready, CountdownTick, CountdownGo, MatchWon, MatchLost, FloorOpened, BriefcaseTaken, BriefcaseDropped, HandsFull, HitMarker, HitMarkerHead, KillConfirm.

| Kit moment | Event |
|---|---|
| Pointer onto a control (if the token allows) | HoverMove (via PlayHover) |
| Focus moved by keys or pad (never by code or a click) | Focus |
| Press down | Select |
| Confirm (release, Enter, A) | Confirm (Back on Back / Stay) |
| Refused press | Error |
| Toggle | ToggleOn / ToggleOff |
| Stepper or swatch change | Select |
| Slider step | SliderTick (via PlaySlider) |
| Dialog open / close | PanelOpen / PanelClose |
| Screen push / replace | Transition |
| Screen pop | Back |

## Focus and cursor

- One `UiFocusFrame` per kit canvas follows the selected control on a spring; it shows only after a key or pad press and hides when the mouse moves. The first arrow press after the mouse only reveals focus.
- Navigation order is explicit: title top to bottom (a greyed-out Host is skipped); settings tab → rows → Reset ↔ Back; dialogs Confirm ↔ Stay.
- The end of a list nudges the marks (edge bump). Held directions repeat at the token timing.
- Left / right on a stepper, swatch row or slider changes the value instead of moving focus.

## How a lane builds UI with the kit

**A HUD piece:** put it on the game menu's `Hud`; use the `Plate` prefab (white plate, navy text), the type roles and colours from `UiTokens` (reference the asset in a serialized field); redraw on your system's events, never per frame. Need a new component? Ask lane 5 on AgentHub; don't add colours, fonts or panel styles.
**A button or setting:** drop in a prefab from [Assets/_Project/UI/Kit/](../../Assets/_Project/UI/Kit/) and subscribe to its event (`UiButton.Clicked`, `UiToggle.Changed`, `UiStepper.Changed`, `UiSwatches.Changed`, `UiSlider.Changed`). Set `Description` (shown on focus) and `RefusalReason` (shown when pressed greyed out).
**A sound:** `UiSound.Play(UiSoundEvent.FloorOpened)`.
**A screen:** subclass `UiScreen` (override `OnShowing`, `OnCancel`, `OnCovered`), give it a Canvas Group, a content rect and its reveal lines, put it under KitUI and `Push` it on `UiRoot.Stack` (`Pop` goes back, `Replace` swaps the top for one of the same kind). Set `_isDialog` for a dialog over the current screen. `UiConfirmDialog.Configure(title, body, confirm, stay, action)` covers yes / no questions.
**The title's sky:** put the live backdrop under `TitleScreen.BackdropSlot` and call `SetLiveBackdrop(true)` to hide the static sky. Keep the lockup and menu band (y 0.64–0.94 of the centred 16:9 frame) clear.
**Glass:** assign the material on `UiGlassPane` (prefab `GlassPane`); without one it draws the no-blur fallback (paper 86%, hairline edge, offset shadow).

## Settings

| Row | Saved as | What reads it |
|---|---|---|
| Reduce motion | `ui.reducedMotion` | UiMotion, every kit component, lane 8's UiTransition |
| Crosshair style (Dot, Cross, Cross + Dot) | `ui.crosshair.style` | UiCrosshair (HUD and preview) |
| Crosshair colour (Paper White, Cyan, Electric Blue) | `ui.crosshair.colour` | UiCrosshair |
| Crosshair size 50–200% | `ui.crosshair.size` | UiCrosshair, whole pixels |
| Show network stats | `ui.networkStats` | PredictionDebugHud (default **on** while netcode is tested; the inventory's release default is off) |

Controls, Video and Audio:

| Row | Saved as | Notes |
|---|---|---|
| Mouse sensitivity, invert, controller look speed | `input.*` (InputPrefs) | Multipliers of the designer's MovementProfile (never written). Shown only when `InputPrefs.LookHooked` (set by lane 1's PlayerCamera) |
| Key bindings | `input.bindings` (binding-override JSON) | Rebinding cells show only when `InputPrefs.BindingsHooked` (lane 1's PlayerInputReader applies the JSON); until then the list is read-only |
| Display mode, resolution | `video.*` | Applied in a build; the Editor's window is the Editor's |
| Vertical sync, frame rate limit, quality | `video.*` | Applied at launch (UiVideo) |
| Master, Music, Effects, Voice, Interface | `ctd.audio.*` | Lane 9's `UiAudio.SetVolume` |

Left out until their game-side value exists: field of view (the camera keeps its own rest FOV; needs a hook from lane 1), stick dead zone (needs the profile value to show), lean / sprint / crouch toggle (hold only), screen shake (lane 2's shake has no player scale), secretary subtitles (LobbyHud draws them with no switch), play audio in the background, Interface size (no scaler hook), the crosshair editor, pad rebinding.
Changes apply at once (no Apply step; the mockup's Apply / unsaved-changes flow is not used) and are written to disk when Settings closes and when the game quits, so dragging a slider doesn't write the disk on every step.

## Tools

- **Close the Deal > Lanes > Set Up UI**: builds the tokens (once), sprites and component prefabs, and the screen prefabs when missing (each left alone once a person edits it, `GeneratedAssetGuard`), and adds KitUI (an instance of `Screens/KitCanvas.prefab`) and the kit crosshair to the open scene. Only adds.
- **Close the Deal > Lanes > Replace UI In Open Scene** (asks first): swaps the scene's KitUI for a fresh instance of the kit canvas, dropping anything changed on it in that scene. Shell (rebuilds every untouched kit prefab, screens included, then replaces KitUI in Lane_Integration): `UiKitSetup.ReplaceFromCommandLine`; use it after changing the screen builders. Shell: `-executeMethod CloseTheDeal.Editor.UI.UiKitSetup.SetUpFromCommandLine` (Lane_Integration.unity).
- `GreyboxUiSetup.Ensure` adds the kit too (building its assets only if they are missing), so Set Up Scene (Greybox), Set Up Integration and Make Open Scene Playable (KitOffice) get it when they next run. Rebuild Game UI keeps the kit and points it at the new menu.
- Captures: `-executeMethod CloseTheDeal.Editor.UI.UiKitCapture.RunFromShell -uiCaptureOut <dir> -uiCaptureMode capture|persist-write|persist-read|menu-rule -local [-lanePort n]` with the Editor binary, `-batchmode`, **without** `-quit` or `-nographics`. Writes the title and settings at three sizes, a state sheet per screen, the hover → press → push strip, a 30 fps clip sequence, a sound log and PASS / FAIL lines.

## Known limitations

- The sky is a static gradient until lane 8's live backdrop fills the slot; panes use the no-blur fallback until lane 7's material.
- Final typeface: every role is TMP's Liberation Sans (the only font in the repo, origin/main and origin/Alex_Env); the display roles thin it with an SDF material (`Tokens/Type_Thin.mat`).
- Status strings are SteamLobby's current wording, not the inventory's rewrite (§5.2–5.3).
- No hardware cursor yet (feel table §2.4), no pad glyph switching, no rumble.
- The frozen sound list has no edge-bump or "focus stops after a held repeat" event: both are silent.
- Hosting, joining and failure states still use the playtest card.
