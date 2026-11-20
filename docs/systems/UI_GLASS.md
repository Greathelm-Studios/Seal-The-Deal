<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code), lane 7 "Aero"
  Generated : 2026-11-14
  Prompt    : UI revamp: build pass B's frosted, see-through UI glass for URP with a no-blur fallback.
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The owning doc for the UI glass material (AGENTS.md §7): what the player sees, files, presets,
              measured contrast, frame cost, how to use it, limits.
-->
# UI glass

**Status:** Current (branch `claude/lane-ui-glass`, not merged) · **Last updated:** 2026-11-14

## What the player sees

Menu panes and HUD chips are light, cool glass: the world behind them shows through, softened by a real blur, under a pale paper tint.
Each pane has a hard diagonal sheen streak, a brighter strip across its top, a 1 px light inner edge, a navy hairline border and a 3 px corner.
All text on glass is Deep Navy and sits on top of the pane in its own text object.
With the blur off (quality setting "No blur", a renderer without the feature, or a scene with no glass) every pane switches by itself to a denser plain tint over the sharp world; text stays legible.

Captures (Lane_Integration, 1920 × 1080, panes top to bottom: Frosted, HUD chip, Clear, NoBlur, then lane 5's real `GlassPane.prefab` reading "Back to game"):
- [10_glass_window_glass_Blur.png](../../ArtSource/UI/review/10_glass_window_glass_Blur.png), [..._NoBlur](../../ArtSource/UI/review/10_glass_window_glass_NoBlur.png), scene only [10_glass_window_scene.png](../../ArtSource/UI/review/10_glass_window_scene.png): reception window wall, sky and cloud sea.
- [10_glass_suit_glass_Blur.png](../../ArtSource/UI/review/10_glass_suit_glass_Blur.png), [..._NoBlur](../../ArtSource/UI/review/10_glass_suit_glass_NoBlur.png), [10_glass_suit_scene.png](../../ArtSource/UI/review/10_glass_suit_scene.png): a navy suit filling the frame.
These PNGs live in the shared, untracked `ArtSource/UI/review/` of the main checkout.

## Presets (engine alphas, Linear colour space)

| Preset | Material | Tint alpha (blur on) | No-blur alpha | Use |
|---|---|---|---|---|
| Clear | `Resources/UiGlass/GlassClear.mat` | 14 % | 38 % | Decorative strips and frames. **Never carries text** (fails 4.5:1, see below). |
| Frosted | `GlassFrosted.mat` | 70 % | 80 % | Menus, settings, confirm dialogs: any pane with text. |
| HUD chip | `GlassHudChip.mat` | 45 % | 72 % | Small HUD chips over the world. |
| NoBlur | `GlassNoBlur.mat` | (never blurs) | 72 % | Forced fallback, e.g. a pane that must not cost a blur. |

The SVG mockups (pass B) used higher sRGB alphas for the same look; Unity blends in Linear, where light glass covers more, so these values are lower.

## Measured contrast (from real captures, not swatches)

Method: `UiGlassCapture` renders each pane over the scene twice, without and with its text. Text colour is the darkest pixel of the text box in the second capture; background is the **darkest** pixel of the same box in the first (worst case for navy type). WCAG ratio of the two.

| Pose | Preset | Blur on | No blur |
|---|---|---|---|
| Bright window (sky, cloud sea) | Frosted | **11.21:1** | **12.44:1** |
| | HUD chip | **8.32:1** | **11.79:1** |
| | NoBlur | 13.49:1 | 13.49:1 |
| | Clear | 4.72:1 | 7.76:1 |
| | Kit GlassPane.prefab | **10.29:1** | **10.84:1** |
| Navy suit close-up | Frosted | **11.06:1** | **12.45:1** |
| | HUD chip | **7.45:1** | **11.29:1** |
| | NoBlur | 11.29:1 | 11.29:1 |
| | Clear | **2.98:1 (fails)** | 6.46:1 |
| | Kit GlassPane.prefab | **9.59:1** | **10.84:1** |
| Window, fed from a UI image (`UiGlassBackdrop`) | Frosted | **11.30:1** | |
| | HUD chip | **8.59:1** | |
| | Kit GlassPane.prefab | **10.32:1** | |

Frosted, HUD chip and NoBlur pass 4.5:1 in both captures, blur on and off. Clear fails over the dark suit, which is why it never carries text.
Full table: [10_glass_contrast.md](../../ArtSource/UI/review/10_glass_contrast.md).
Not measured: sun-bloom pointed straight at the sun (the scene's sky never clips to pure white at this exposure), 1280 × 720, ultrawide.

## Frame cost

See the table below (filled by `UiGlassCapture.TimeFromCommandLine`; editor render loop on the M3 Max, GPU synced each frame, so it is an upper bound that includes readback, not a target-hardware figure).

| Resolution | Blur on, ms/frame | No blur, ms/frame | Blur cost |
|---|---|---|---|
| 1920 × 1080 | 4.176 | 4.196 | **within noise** (−0.02 ms) |
| 2560 × 1440 | 5.297 | 5.075 | **~0.2 ms** |

Two 240-frame runs with blur on, averaged, against one with it off. Per-pass GPU recorder times were not reported in batch mode.

The blur reads the camera colour once, writes a half-res and three quarter-res textures (four fullscreen passes, the last three at 1/16 of the pixels). It runs only while at least one `UiGlass` is enabled.

## How it works

```
screen camera: opaques + sky ──▶ UiGlassBlurFeature (BeforeRenderingTransparents)
                                   down ½ ─▶ down ¼ ─▶ gauss H ─▶ gauss V ─▶ persistent texture = global _UiGlassBlurTex, _UiGlassBlurOn = 1
               transparents ──▶ Screen Space - Camera canvas ─┐
               after camera ──▶ Screen Space - Overlay canvas ─┴▶ CloseTheDeal/UI/Glass samples it at the pane's screen UV
UI backdrop (title's live sky RawImage + UiGlassBackdrop) ──▶ blurs the IMAGE instead; scene blur stands down; mapped to the image's rect
no pane alive / "No blur" / feature missing ──▶ passes skipped, _UiGlassBlurOn = 0 ──▶ shader uses the no-blur alpha
```

Which camera feeds the glass: a Game camera that draws to the screen (not overlay cameras in a stack, not render-texture cameras such as mirrors or the title's sky camera), or `UiGlassBlurFeature.SourceCamera` when a tool sets one.

- [UiGlassBlurFeature.cs](../../Assets/_Project/UI/Glass/Scripts/UiGlassBlurFeature.cs): the Render Graph renderer feature, on `Assets/Settings/PC_Renderer.asset` only.
- [UiGlassBlur.shader](../../Assets/_Project/UI/Glass/UiGlassBlur.shader): the four blur passes.
- [UiGlass.shader](../../Assets/_Project/UI/Glass/UiGlass.shader) (`CloseTheDeal/UI/Glass`): the pane.
- [UiGlass.cs](../../Assets/_Project/UI/Glass/Scripts/UiGlass.cs): the component. Put it on a uGUI `Image` (white, no sprite needed); pick a preset; optional mullions (0–4) and a cyan bottom edge-light. It writes the pane's pixel size into uv1 (it turns on TexCoord1 on the canvas) and counts live panes for the feature.
- [UiGlassBackdrop.cs](../../Assets/_Project/UI/Glass/Scripts/UiGlassBackdrop.cs): panes over a UI backdrop blur that image (quarter-size blur of its texture, only while shown). Added by itself at run time to every kit `TitleScreen.BackdropSlot`, where lane 8's live sky goes; no other lane's file is edited. Capture: [10_glass_window_glass_backdrop.png](../../ArtSource/UI/review/10_glass_window_glass_backdrop.png).
- [UiGlassSettings.cs](../../Assets/_Project/UI/Glass/Scripts/UiGlassSettings.cs): the quality setting `Auto / Blur / NoBlur`, saved in PlayerPrefs (`ctd.ui.glassQuality`). The settings screen (lane 5) sets `UiGlassSettings.Current`.
- [UiGlassSetup.cs](../../Assets/_Project/UI/Glass/Editor/UiGlassSetup.cs): **Seal the Deal > UI > Build Glass Materials** (re-run safe), **Seal the Deal > UI > Wire Kit Glass Pane** (puts `GlassFrosted.mat` in lane 5's `UiGlassPane` slot on `Assets/_Project/UI/Kit/GlassPane.prefab` and `GlassHudChip.mat` on `Assets/_Project/UI/Kit/Plate.prefab`, each with a `UiGlass`, Owns Material off; agreed with lane 5 (#469, #542), it takes those prefabs over from lane 5's generator), and `AddFeature(renderer path)`.
- [UiGlassCapture.cs](../../Assets/_Project/UI/Glass/Editor/UiGlassCapture.cs): the captures, the measurement and the timing, from the command line.

## Rules for UI built on it

- Lane 5's kit panes are wired: every pane built from `GlassPane.prefab` (the confirm dialog) is Frosted glass, and every HUD plate built from `Plate.prefab` (lane 4's clock, briefcase tracker, ladder, banners and notice; lane 3's interact prompt and hands slot; lane 4's menu match section) is HUD chip glass. `Lane_Integration` holds them as linked prefab instances, so the scene needs no edit. On the title and settings screens today the panes sit on white (the sill, the paper grid), so there is little to see through; suggested to lane 5 (#498) to let a pane overlap the sky.

- Screen Space - Camera and Screen Space - Overlay canvases both get the blur (the texture outlives the camera's render graph). **The overlay path is not captured**: an overlay canvas never reaches a camera texture, and a batch-mode editor has no screen (`ScreenCapture` returns blank). Only a person playing, or a built player, can see it.
- Text never sits on Clear. Text on Frosted or HUD chip is Deep Navy `#111B38`. Light text on glass is not a used pair.
- Keep a glass canvas's plane distance in front of world geometry (the capture uses 0.08 m with a 0.05 m near clip), or a close wall cuts through the pane.

## Known limits

- The blur sees the opaque scene and sky only, not transparent world objects (window glass sheen, particles) behind the pane.
- PC renderer only. `Mobile_Renderer.asset` has no feature, so mobile quality always gets the fallback.
- The cyan edge-light and mullions use a per-pane material copy (no batching for those panes).
- Not seen in Play Mode, on a client, at 1280 × 720 or at 21:9: the captures are editor renders of the scene with a test canvas. Adam is checking it by playing (no more headless runs, his call 2026-11-14).
- The title and settings screens use neither kit pane today (white sill and paper): no glass there until lane 5 puts a pane over the sky.
