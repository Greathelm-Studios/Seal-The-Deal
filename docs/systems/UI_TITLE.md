# Title screen backdrop and screen transitions

**Status:** Current (seen running in a headless Play Mode check; not yet played by a person, on a gamepad or on a client) · **Last updated:** 2026-11-13

The title screen looks out through a white window frame, seen from inside and a little below, at the game's own sky: deep blue, stepped flat clouds, a plane and its contrail, the towers.
The lower third is calm and white, left clear for the title lockup and menu.
Moving between screens uses a stepped cloud wipe, a sash of white panes, or, on starting a game, the window frame closing over the view.

Look reference: pass C "Sky" in `ArtSource/UI/passes/c_sky/` (mockup only).
This lane adds no new look: the sky is `Close the Deal/Office Sky` through Office Look, the frame is kit pieces.

## Files

| What | Where |
|---|---|
| Live sky in the kit's title screen | `Assets/_Project/Scripts/UI/Title/TitleSky.cs` |
| Idle life of the standalone set, and safe areas | `Assets/_Project/Scripts/UI/Title/TitleBackdrop.cs` |
| Transitions | `Assets/_Project/Scripts/UI/Title/UiTransition.cs` |
| Scene and capture tool | `Assets/_Project/Editor/Lanes/TitleLaneSetup.cs`; scene `Assets/_Project/Scenes/Lanes/Lane_Title.unity` |
| Capture output | `ArtSource/UI/passes/c_sky/title/` |

## The live sky in the title screen (`TitleSky`)

The UI kit's title screen (lane 5, `TitleScreen`) draws the white window frame, sill and menu itself and leaves a `BackdropSlot` behind the panes.
`TitleSky` fills it in every scene that has the kit's `UiRoot`: it adds itself after each scene load (no setup, nothing to place), puts a `RawImage` at the back of the slot and calls `TitleScreen.SetLiveBackdrop(true)` to hide the kit's static gradient.
Its camera draws only the scene's own sky (culling mask "nothing", so no scene geometry and no lights of its own), with the game's post-processing, into a render texture the size of the slot in screen pixels.
It renders only while the title screen is shown; in a game it is disabled, so it costs nothing and nothing leaks into gameplay.
The view faces 155 degrees away from the sun (facing it washed the panes out with sun glare), 20 degrees up, 46 degree field of view, and sways 1.4 degrees over 26 s (still with reduced motion).
While shown, the sky material is swapped for a copy that sends a plane every 18 s; the original is put back when the title hides.

## The standalone set

`Close the Deal > Lanes > Set Up Title Screen` (batch: `-executeMethod CloseTheDeal.Editor.Lanes.TitleLaneSetup.SetUpFromCommandLine`) builds `Lane_Title.unity`: three `Window_4m` bays with floor and ceiling `Slab_4x4`, two side walls, a camera pivot 9 m back and 0.55 m up looking 9 degrees up, a sun, then Office Look for the sky, ambient and grade.
It only replaces a scene whose root is named "Title backdrop" (its own); any other scene at that path is left alone.
The rival tower is the sky's own foreground towers; there is no real geometry across the gap.
Hard sun slabs on the floor come from the sun's shadows through the window mullions, not from painted shapes.

## Idle life (`TitleBackdrop`)

| What moves | How | Default |
|---|---|---|
| Camera pivot | slow sine drift and sway on a child pivot; the camera is never touched | 3.5 cm over a 9 s breath, 0.3 degrees |
| Sun | swings either side of its set angle, so the sun slabs creep | 2.5 degrees over 60 s |
| Clouds | the sky shader's own drift (Cloud drift on the material) | unchanged |
| Planes and contrail | the sky shader; this screen asks a runtime copy of the material for one every 18 s (the game uses 40) | 18 s |

The sky material is never written: a copy is used while the component is enabled and the original is put back when it is disabled.
Unscaled time, so the idle runs while the game is paused.
With reduced motion on (`UiTransition.ReducedMotion`) the camera and sun hold still.

## Safe areas (agreed with the UI lane on AgentHub; the kit's title follows them)

Fractions of the screen from the top left, measured inside a centred 16:9 frame so they hold on 21:9 (`TitleSafeAreas.OnScreen(rect, aspect)`).

- **Lower band** x 0.06 to 0.94, y 0.64 to 0.94: the title lockup and the menu go here and nowhere above it.
- **Sky window** x 0.14 to 0.86, y 0.04 to 0.60: keep free of UI.

## Transitions (`UiTransition.Play(kind, onCovered, onDone)`)

`onCovered` fires when the screen is fully hidden: swap screens there. `onDone` fires when the overlay is gone.
A second call while one plays is ignored and both its callbacks still fire at once, so a screen stack never waits on a dropped callback.
Unscaled time; the overlay blocks clicks while it plays and builds itself on first use.

| Kind | Look | Cover + reveal | Easing |
|---|---|---|---|
| `CloudWipe` | stepped flat cloud cells (Paper, Milk, Haze at the leading edge) sweep left to right, then away | 0.14 + 0.14 s = 0.28 s | out cubic in, in-out cubic out |
| `PaneSlide` | three white panes with navy edges drop in from above and below alternately, then leave the other way | 0.13 + 0.13 s = 0.26 s | same |
| `FrameZoom` | the window frame's hole closes in on the view with a mullion cross, then the white clears; `ZoomProgress(0..1)` lets the lobby camera push in with it | 0.55 + 0.30 s = 0.85 s | ease-in cover, in-out reveal |
| `Fade` | plain short fade | 0.06 + 0.06 s = 0.12 s | linear |

Reduced motion turns every kind into `Fade`. `UiTransition.ReducedMotion` is the kit's `UiSettings.ReducedMotion` (Settings > Accessibility > Reduce motion), so there is one switch.

Calls agreed with lane 5: Host (game start) = `FrameZoom`; Settings push and pop = `PaneSlide`; any other screen replace = `CloudWipe`; the quit confirm uses the kit's own panel motion.
The two kinds the player starts are under 300 ms; only the game start is longer.

## Known limitations

- `ArtSource/UI/research/feel_wii_ds.md` had not landed when this was written, so the timings above are this lane's own and the easing is plain cubic, not the kit's motion table. Align them when it lands.
- `FrameZoom` closes a frame over the view; the camera push is the caller's, through `ZoomProgress`. Nothing here loads the lobby.
- The tower across the gap is painted by the sky and does not parallax.
- Not played by a person, on a gamepad, or on a client.

## Checking it

`-executeMethod CloseTheDeal.Editor.Lanes.TitleLanePlayCheck.RunFromCommandLine [-titleOut folder]` (batch, with graphics) enters Play Mode in `Lane_Integration.unity` and checks, writing `play_check.txt` and frame strips (default `ArtSource/UI/passes/c_sky/title/play/`):
the live sky attaches and renders while the title shows; its view moves; each transition covers and finishes near its stated length; a call made during one waits; reduced motion turns a wipe into the fade and stills the sky; hiding the title stops the sky camera and restores the game's sky.
Last run 2026-11-14: all pass. Measured: CloudWipe covered 0.128 s, done 0.268 s; PaneSlide 0.099 / 0.234; FrameZoom 0.518 / 0.818; Fade 0.031 / 0.093 (the capture makes frames slow, so each phase lands a frame early).

Transitions advance by real time capped at a 30 fps frame and start at 0 on their first frame: before that, the slow frame a press usually lands on skipped the whole cover phase (measured: PaneSlide covered at 0.001 s), and the screen change popped.
