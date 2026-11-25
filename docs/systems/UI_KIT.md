# UI Kit

**Status:** Current (wide pass slices 1-3: edge marker, device key hints, live-sky title, settings fixes, hosting / joining / problem panels; partial client-side verification) · **Last updated:** 2026-11-22

## What the player sees

The game opens on the title: a slim white window frame with depth (20 px mullion and transom faces at 1080p with a 4 px silver reveal or shadow, lit edges, a black-edged sill) over lane 8's live sky (the game's own sky, its traced clouds and the towers; a flat gradient where a scene has no sky material), sun falling through it in two hard slabs across the paper below, behind the text. Low on paper: **SEAL THE DEAL**, the status and connection lines, the focused button's explanation, the keys row (Enter / A select, Esc / B back, arrows / stick move), a microtype strip, the build line, barcode and corner ticks, and the menu: **Host game**, **Join on this PC** (local test mode only), **Settings**, **Quit to desktop**.
Every control answers: hovering lifts it and closes its corner brackets, pressing stamps it down, letting go confirms with a small cyan tick, and each of those asks for a sound.
Keys and the pad show a focus ring with snapped brackets and a chevron that glide to the next control; with the mouse those marks stay hidden.
A greyed-out control says why when pressed (amber plate, a shake, the reason in the status line).
Esc / the pad's B goes back; at the title it asks **Quit to desktop?** with **Stay** focused.
**Settings** has four tabs (Q / E or the shoulder buttons switch; the cobalt rule glides to the open tab and the page slides 12 px): Controls (look rows and the key list; rebinding asks before taking a key another listed action uses, and refuses a key kept for an unlisted one such as moving or Help), Video (display mode, resolution with a 10-second revert to the size the screen really had, a saved size clamped to the monitor, vertical sync, frame rate limit, quality), Audio (five volumes, then voice chat's microphone, microphone mode, noise suppression and a mic test, see [VOICE.md](VOICE.md); a test sound and a live output meter) and Accessibility (as before). The right column always shows the focused row's explanation above the tab's live preview; under the rows, **Changed on this tab** lists each value that differs from how the game shipped (or says nothing has), except on Controls and Audio, whose rows fill the page. Changes apply at once and come back after a relaunch.
Screen changes play lane 8's transitions and the change happens while they cover the view: Settings opens and closes behind three white panes that shut like a sash (PaneSlide), hosting, joining and problems arrive behind a stepped cloud wipe (CloudWipe), a game starts with the window frame closing in (FrameZoom), and leaving a game wipes cloud back to the title. Under the cover both screens finish their own motion at once, so the reveal shows the new screen settled. Dialogs (the quit and leave questions, the rebind clash) keep their own slide and fade. Reduce motion turns every transition into a 0.12 s white fade. While a covered change waits (0.13 to 0.14 s) the stack ignores further changes and Esc, and focus is let go so a Submit can't press the old screen's button.
Hosting, joining and loading stay on the title: a **session panel** takes its middle and right columns (the window and lockup stay) and says what is happening in the lobby's own words, with three chevrons stepping while it waits, a track that fills over the 15 s a join waits before giving up (nothing else gets a bar, because nothing else is measurable) and **Cancel** (Esc / B). When a player of ours is in the world, the window closes over the view (lane 8's frame zoom) and the screens clear under it. Anything that goes wrong (every SteamLobby problem: no lobby, closed, friends-only, full, refused, wrong version, unreachable, host closed, host dropped, connection lost, network broken) opens the **problem panel**: an amber plate with the problem and how to fix it, the button that fixes it (Host again, Try again, Open Steam friends, Quit to desktop; greyed with its reason when it can't run) and **Back**. In local mode without Steam the title says friends can't join and how to fix it.
In a game, **Esc** (or the pad's Start) opens the **in-game menu**: a frosted glass pane on the left over the running game (nothing pauses; there is no veil). **§ MENU**, "The game continues while this is open.", then **Back to game**, **Invite friends** (Steam only: hidden in local mode, greyed with its reason when Steam's overlay is off), **Settings** and **Leave game**; under them the focused button's explanation, the session line ("You are hosting. 3 players of 8." or "Hosted by Adam. …") and the keys row. On the right, the match lane's section (where the match stands, the host's Start / Rematch) sits in the pane's match slot, and while the run hasn't started a second glass pane shows **reception**: both firms side by side, four seats each, who sits there, Ready / Not ready / Fitting room, "Seat available" for the rest and how many are ready (read only; the lobby lane owns the ready board and the start). Leave asks first, in the host's words ("End the game for everyone?", or "Close your game?" when alone) or a client's ("Leave Adam's game?"). Esc / B is Back to game from the pane and goes back one step from Settings or the question; Start closes the whole menu from anywhere in it. While it is open the mouse is free and the player stands still, even mid-sprint.
The HUD's **Esc · Menu** hint is the kit's key hint (Menu / Options on a pad), and the network readout sits on a kit plate (navy on white; Settings › Accessibility › Show network stats hides it).

## Ownership and netcode boundary

The UI kit is purely local client-side presentation. User preferences live locally in `PlayerPrefs` (`UiSettings`, `UiVideo`, `InputPrefs`, `UiAudio`, `UiGlassSettings`). `TitleScreen`, `SessionPanel`, `ProblemPanel`, and `InGameMenuScreen` read `SteamLobby` and `LobbyRoster` state and forward button clicks (`Host`, `JoinLocal`, `Invite`, `Leave`, `RetryJoin`), never authoring or syncing network state themselves.

## uGUI, not UI Toolkit

The kit is uGUI + TextMeshPro + CanvasScaler.
- The deciding test: lane 7's glass is a URP material. A uGUI `Image` takes any material, so a pane switches to real glass by assigning one material (`UiGlassPane.GlassMaterial`). UI Toolkit panels can't take a custom shader per element in this Unity version; it would need a render-texture detour.
- Everything already in the game (GameMenu, every lane's HUD, the lobby) is uGUI; one system means one focus model and one EventSystem.
- uGUI `Selectable` gives keyboard and pad navigation, Submit and Cancel through the Input System UI module for free.

## How the pieces fit

```
UiTokens.asset ──► every component (colours, type, sizes, motion, repeat timing)
UiSettings (PlayerPrefs) ──Changed──► UiCrosshair, PredictionDebugHud, UiMotion.Reduced
GameMenu ──StateChanged / OutOfGame──► UiRoot ──► UiScreenStack ──► TitleScreen ⇄ SettingsScreen, QuitDialog
SteamLobby ──Stage / Problem (kept until acted on)──► UiRoot ──► SessionPanel ⇄ ProblemPanel (over the title)
controls ──► UiControlFx ──► UiMotion (one driver, unscaled time)
controls ──► UiSound.Play(event) ──► Player (lane 9's UiSoundPlayer, once registered)
controls ──OnSelect──► UiFocusFrame (ring + brackets + chevron)
```

- **GameMenu still owns the rule** menu open ⇔ mouse free ⇔ player idle. The kit takes every out-of-game state (`HandTitleToKit`, `OutOfGame`): the title, hosting, joining, loading and problems.
- **UiRoot decides the panels from state, not events**: the lobby keeps `Problem` until the player acts on it, so Title → Problem comes out the same whatever order the events arrive in; it reruns on the menu's and the stack's events, never per frame.
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
`UiMotion.EnsureSubCanvas(GameObject)` isolates animated HUD subtrees (`UiCrosshair`, `UiEdgeMarker`, `UiFocusFrame`) on their own nested sub-Canvas (without adding a `GraphicRaycaster`) so moving, scaling, or fading them does not dirty and rebuild the parent HUD canvas.
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
- `UiFocusFrame` ticks while it has an active selected target on keys/pad or while fading (tracking targets that may be sliding with their panel), and returns `false` from `Tick` to sleep in `UiMotion` once faded out or unselected. `Follow`, `Bump`, and `WakeUp` wake it (`WakeUp` is needed when code selects an already-selected object, since uGUI raises no `OnSelect` event).
- Navigation order is explicit: title top to bottom (a greyed-out Host is skipped); settings tab → rows → Reset ↔ Back; dialogs Confirm ↔ Stay.
- The end of a list nudges the marks (edge bump). Held directions repeat at the token timing.
- Left / right on a stepper, swatch row or slider changes the value instead of moving focus.
- A stepper's arrows are bold chevrons (the `Kit_Arrow` sprite, drawn at 8 texels a unit so they stay sharp at 8K). The arrow under the mouse grows (`Hover Grow`, 1.2x), and the arrow on the side the value moved kicks out with a pop and springs back (`Kick`, 12 units; `Kick Pop`, 1.35x; off with Reduce motion), for clicks and keys alike.

## Edge marker

`UiEdgeMarker` (prefab `Kit/EdgeMarker.prefab`) points at something in the world: in view, the mark sits over it; out of view or behind you it waits at the nearest screen edge, 48 px in (Safe Edge), with a chevron toward the target and the distance in metres on the side away from the chevron.
Shape and colour say whose it is, never colour alone: `Ours` cyan diamond, `Rival` amber triangle, `Loose` paper square, each with a navy keyline.
- **Use:** put the prefab under the HUD canvas so its rectangle stretches over the whole canvas (it measures in that canvas's units from the bottom-left), set `Kind`, then once a frame in LateUpdate call `Track(camera, worldPoint)`; it projects with lane 11's `Nameplates.WorldToCanvas.TryProject`. A HUD that projects for itself calls `Place(canvasPoint, inFront, metres)` with TryProject's output (metres < 0 hides the tag). `Hide()` fades it out; `Flash()` pops it once (the rival took it).
- Behind you, the mark always goes to an edge on the side the target is (never into view); dead behind goes to the bottom edge. When a target crosses behind you and its side flips, the mark slides round the bottom edge to the new side in 140 ms instead of jumping (no slide with Reduce motion).
- Placing it each frame allocates nothing; the distance text changes only when the whole metre does. Fades: 120 ms in OUT, 80 ms out IN; flash 320 ms (a dip in opacity with Reduce motion).

## Key hints

`UiGlyphs` is the one table of key and button names. It tracks the device the player used last (`UiInput.Poll`; `Changed` fires only when the kind changes: keyboard and mouse, Xbox, PlayStation, other pad with Xbox names) and turns a binding into the label for it: `F`, `Esc`, `A` / `Cross`, `LB` / `L1`, `Menu` / `Options`. Player-map hints read the player's custom keys (`UiGlyphs.WithRebinds`, a read-only copy; the actions asset is never changed).
`UiKeyHint` on the `KeyHint` prefab is live once it has an action (`Player/Interact`, `UI/Cancel`) or fixed names (keyboard text, a pad control such as `leftShoulder`): the cap shows the key for the device in use, widens to fit and the words move with it; an action with nothing bound on that device shows a dash. Without either it shows its text unchanged, as before. From code: `hint.Bind("Player/Interact", "Pick up", actions)`.
The title's keys row, the settings tab caps and footer, the confirm dialog and the Controls page's pad column all switch with the device.

## How a lane builds UI with the kit

**A HUD piece:** put it on the game menu's `Hud`; use the `Plate` prefab (white plate, navy text), the type roles and colours from `UiTokens` (reference the asset in a serialized field); redraw on your system's events, never per frame. Need a new component? Ask lane 5 on AgentHub; don't add colours, fonts or panel styles.
**A button or setting:** drop in a prefab from [Assets/_Project/UI/Kit/](../../Assets/_Project/UI/Kit/) and subscribe to its event (`UiButton.Clicked`, `UiToggle.Changed`, `UiStepper.Changed`, `UiSwatches.Changed`, `UiSlider.Changed`). Set `Description` (shown on focus) and `RefusalReason` (shown when pressed greyed out).
**A sound:** `UiSound.Play(UiSoundEvent.FloorOpened)`.
**A screen:** subclass `UiScreen` (override `OnShowing`, `OnCancel`, `OnCovered`, and `Through` to name the lane 8 transition that covers its changes; null = none), give it a Canvas Group, a content rect and its reveal lines, put it under KitUI and `Push` it on `UiRoot.Stack` (`Pop` goes back, `Replace` swaps the top for one of the same kind). Set `_isDialog` for a dialog over the current screen. `UiConfirmDialog.Configure(title, body, confirm, stay, action)` covers yes / no questions.
**A window onto the sky:** a screen with one implements `IBackdropScreen` (`BackdropSlot`, `SetLiveBackdrop`); the title does today. **The title's sky:** lane 8's `TitleSky` puts the live sky under `TitleScreen.BackdropSlot` by itself and calls `SetLiveBackdrop(true)`; the flat fallback stays only when the scene has no sky material (Set Up UI says which). Keep the lockup and menu band (y 0.64–0.94 of the centred 16:9 frame) clear.
**Glass:** assign the material on `UiGlassPane` (prefab `GlassPane`); without one it draws the no-blur fallback (paper 86%, hairline edge, offset shadow).

## Settings

| Row | Saved as | What reads it |
|---|---|---|
| Reduce motion | `ui.reducedMotion` | UiMotion, every kit component, lane 8's UiTransition |
| Crosshair style (Dot, Cross, Cross + Dot) | `ui.crosshair.style` | UiCrosshair (HUD and preview) |
| Crosshair colour (Paper White, Cyan, Electric Blue) | `ui.crosshair.colour` | UiCrosshair |
| Crosshair size 50–200% | `ui.crosshair.size` | UiCrosshair, whole pixels |
| Show network stats | `ui.networkStats` | PredictionDebugHud on the HUD's kit plate (default **on** while netcode is tested; the inventory's release default is off) |

Controls, Video and Audio:

| Row | Saved as | Notes |
|---|---|---|
| Mouse sensitivity, invert, controller look speed | `input.*` (InputPrefs) | Multipliers of the designer's MovementProfile (never written). Shown only when `InputPrefs.LookHooked` (set by lane 1's PlayerCamera) |
| Key bindings | `input.bindings` (binding-override JSON) | Rebinding cells show only when `InputPrefs.BindingsHooked` (lane 1's PlayerInputReader applies the JSON); until then the list is read-only |
| Display mode, resolution | `video.*` | Applied in a build; the Editor's window is the Editor's |
| Vertical sync, frame rate limit, quality | `video.*` | Applied at launch (UiVideo) |
| Menu glass (Auto, Blur, No blur) | `ctd.ui.glassQuality` | Lane 7's `UiGlassSettings.Current`; No blur shows the game sharp under a denser tint |
| Master, Music, Effects, Voice, Interface | `ctd.audio.*` | Lane 9's `UiAudio.SetVolume` |

Interact (F) and Drop / Throw (G) have no Controls row: the pickups, firm and lobby code read those keys directly, so a row would rebind nothing. Being unlisted, F and G are refused to other actions ("F is kept for Interact"). They come back when ItemCarrier, FirmMember and LobbyInput read the player's binding.
Left out until their game-side value exists: field of view (the camera keeps its own rest FOV; needs a hook from lane 1), stick dead zone (needs the profile value to show), sprint / crouch toggle (hold only), screen shake (lane 2's shake has no player scale), secretary subtitles (LobbyHud draws them with no switch), play audio in the background, Interface size (no scaler hook), the crosshair editor, pad rebinding.
Changes apply at once (no Apply step; the mockup's Apply / unsaved-changes flow is not used) and are written to disk when Settings closes and when the game quits, so dragging a slider doesn't write the disk on every step.

## Tools

- **Seal the Deal > Lanes > Set Up UI**: builds the tokens (once), sprites and component prefabs, and the screen prefabs when missing (each left alone once a person edits it, `GeneratedAssetGuard`), and adds KitUI (an instance of `Screens/KitCanvas.prefab`) and the kit crosshair to the open scene. Only adds.
- **Seal the Deal > Lanes > Replace UI In Open Scene** (asks first): swaps the scene's KitUI for a fresh instance of the kit canvas, dropping anything changed on it in that scene. Shell (rebuilds every untouched kit prefab, screens included, then replaces KitUI in Lane_Integration): `UiKitSetup.ReplaceFromCommandLine`; use it after changing the screen builders. Shell: `-executeMethod CloseTheDeal.Editor.UI.UiKitSetup.SetUpFromCommandLine` (Lane_Integration.unity).
- `GreyboxUiSetup.Ensure` adds the kit too (building its assets only if they are missing), so Set Up Scene (Greybox), Set Up Integration and Make Open Scene Playable (KitOffice) get it when they next run. Rebuild Game UI keeps the kit and points it at the new menu.
- **Setup guards and reference safety:**
  - `GeneratedAssetGuard` writes an MD5 fingerprint (ignoring `\r` so git CRLF line-ending differences across machines do not look like edits) into asset labels (`greybox-<hash>`), skipping any hand-made or hand-edited asset unless cleared with `Seal the Deal > Greybox > Let the Tool Rebuild Selected`.
  - Screen prefabs nest component prefabs, and rebuilding them generates new internal Unity file IDs (causing noisy YAML diffs for no functional change), so `UiKitSetup.BuildAssets(false)` only builds missing screen prefabs unless `Replace UI` explicitly asks to rebuild them.
  - Before rebuilding or replacing `KitUI`, `UiKitSetup.ParkMatchSection()` temporarily moves the match lane's section back to `GameMenu` so `MatchMenuPanel` keeps its references, then `MoveMatchSection()` moves it into the new `InGameMenu/Content/MatchSlot`.
- Captures: `-executeMethod CloseTheDeal.Editor.UI.UiKitCapture.RunFromShell -uiCaptureOut <dir> -uiCaptureMode capture|persist-write|persist-read|menu-rule -local [-lanePort n]` with the Editor binary, `-batchmode`, **without** `-quit` or `-nographics`. Writes the title and settings at three sizes, a state sheet per screen, the hover → press → push strip, a 30 fps clip sequence, a sound log and PASS / FAIL lines.
- Click and resolution probe: `-uiCaptureMode wide-probe` clicks every settings control through the real hit test (the main run sends events straight to controls, so it can't see a click that never lands), presses the arrow keys on each stepper, and films the title and settings from 1024x768 to 7680x4320 with native-pixel crops at 4K and 8K. `wide-probe-ingame` does the clicks in a hosted game, from the in-game menu, with motion live.
- Kit rebuild from the shell: `-executeMethod CloseTheDeal.Editor.UI.UiKitSetup.BuildAssetsFromCommandLine -quit [-handBack <asset path>]` rebuilds the tokens, sprites and component prefabs without opening a scene; `-handBack` first hands one guarded asset back to the tool (the shell form of Let the Tool Rebuild Selected).

## Known limitations

- Final typeface: every role is TMP's Liberation Sans (the only font in the repo, origin/main and origin/Alex_Env); the display roles thin it with an SDF material (`Tokens/Type_Thin.mat`).
- No hardware cursor yet (feel table §2.4), no rumble.
- The frozen sound list has no edge-bump or "focus stops after a held repeat" event: both are silent.
- Host closed, host dropped and connection lost are drawn and wired but were only checked as stills: they need a second instance. The in-game menu's seats and session line were seen on a host only; nobody has seen them from a client.
- Reception's seats sit under the match section by its height at the moment the menu opens; if the match lane shows or hides its section while the menu is open, the seats keep their place until it reopens.
- On this PC a failed join gives up after LiteNetLib's own ~5 s; the panel still says "Gives up after 15 s" (the lobby's backstop, Steam's timing).
