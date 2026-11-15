# Seal the Deal UI tools

**The game is Seal the Deal (title SEAL THE DEAL); never 'Close the Deal'.** Use `ui.GAME_TITLE` / `ui.GAME_NAME`, never a literal.

`ctd_ui.py` is the shared SVG library for the VectorCraft UI mockups. `make_kit.py` builds the three-board UI kit from it.

```
python3 ArtSource/UI/tools/make_kit.py      # writes svg/kit/a1_*, a2_*, a3_*; checks the layout; prints contrast; previews
```

## References: the guide for every pass (owner, 2026-11-12)
The owner's nine UI references in [ArtSource/References/ui/](../../References/ui/) are "the guide we need for UI". Read their [README](../../References/ui/README.md) and view all nine before drawing. They are other people's work: take the devices, draw everything fresh, never trace or copy their images, text or marks.
- **Sky and paper white are the two fields.** Sky sits inside frames (rounded windows, vertical strips, window panes, discs); the frame is the UI. Ref 08 (white window frame dividing deep-blue sky into panes) is the closest tie to the game's own windows.
- **Glass is light and see-through**, tinted blue-grey, frosted or clear. **No dark panels.** Over the world, legibility comes from navy type on white / frosted-white plates, checked by computed ratio over `sky_bloom` and `navy_dark`.
- **Type is small, thin and wide**, one large thin title; rounded-square techno display, neutral sans captions (see [fonts_by_pass.md](../research/fonts_by_pass.md)).
- **Hairlines, grids, corner brackets, chevron rows, barcodes, dotted halftone screens and outlined empty slots** carry the interface feel. **Black is an accent** (thin outline, one stroke), never a panel colour.
- Ref 07's glossy blobs are the bubbly end the bible warns about: use with care.

## API at a glance
- **Tokens:** `PAPER SILVER NAVY COBALT ELECTRIC CYAN BLACK AMBER`, alpha tokens `INK2 INK3 HAIR ...` as `(hex, alpha)`, `TYPE` roles, `SIZE`, `GLASS`. These are the only colours: no other hex anywhere.
- **Primitives:** `rect line poly path text text_right text_lines group write`. All coordinates are absolute 1920 × 1080 at 1:1. `place()` (transform) is not tracked by the layout checker.
- **Measure:** `text_width` reads advance widths from the installed font files (stdlib TrueType reader; PIL is not installed). `wrap` breaks lines; `glyphs_missing` lists characters a family lacks.
- **Glass:** `glass_pane(kind=...)`. Light kinds (navy text): `plate` (paper 90 %, every HUD surface), `frosted` (paper 86 %, menu panes), `clear` (silver 40 %, see-through frames, **no text**), `amber` (solid, rival / threat). Legacy: `smoked` (navy 95 %, paper text), `cobalt` (96 %, primary button and active tab). `plate()` is a shortcut; `glass_ink(kind)` / `is_light(kind)` give a kind's text colours. Tokens `HUD_GLASS` (`"plate"`) and `WARN_GLASS` (`"amber"`) pick what the HUD components draw on.
- **Controls:** `button toggle slider stepper keybind tab tab_bar settings_row section_header modal_confirm status_line`. Each takes `state` from `STATES`; key-bind cells also take `listening` and `conflict`.
- **HUD:** `hud_chip key_hint interact_prompt stairs_prompt stairs_refusal item_slot briefcase_tracker briefcase_marker open_floor toast crosshair team_marker`.
- **Motifs and backdrops:** `hairline_grid crop_marks microtype_strip trace_brackets floor_numeral title_lockup backdrop`, plus `chevron_row barcode halftone outlined_slot sky_window` (the guide's devices). `trace_brackets` records its squares in `TRACE_BOXES`; make_kit fails a trace that touches text.
- **Checks:** `contrast_table()` gives engine (Linear) and SVG ratios. `preview(svg)` and `preview_many(paths, png)` render an exact-size PNG through Quick Look.
- **Deprecated, kept importable:** `race_tracker item_strip briefcase_chip briefcase_world`.

## Rules
- Corner radius 0. No `<circle>` or `<ellipse>`: discs and halftone dots are square or stepped. Pass A faces: Kohinoor Bangla Light (display), Helvetica Neue (+ UltraLight numerals) and JetBrains Mono.
- No dark panels. Anything over the 3D world is navy type on a white `plate` (or two-tone, like the crosshair) and at least 16 px. Thin weights only on the title and big numerals.
- Amber is never text: navy text on a solid `amber` plate, or an amber mark with a navy keyline. Cyan is a mark with a navy keyline, never text on light. Nothing is set on `clear` glass. make_kit checks all three for pass A.
- Black is an accent: one hairline, a bullet, an amber plate's edge. Never a fill.
- Silver text never in the top 30 % of a smoked pane (legacy kind).
- One `TraceCluster` per screen, never touching text.
- **Interact is F** ("F · Pick up", "F · Open floor 8"); Lean is Q / E; Drop is G. Never draw E as Interact.
- Alpha text tokens are for the mockup only. In Unity (Linear) they render lighter, so ship INK2, INK3 and DISABLED as opaque colours.

## Import quirks hit
- `qlmanage -t -s S` always outputs S × S and aspect-fills a non-square SVG. `preview()` wraps the board in a centred square, then crops back.
- VectorCraft drops `fill-opacity` on `<text>`. `text()` writes the alpha as `opacity` instead.
- SVG collapses a leading space in `<text>`. `text()` strips it and moves x instead.
- Place with `file.place {crop:"crop", rect:[0, y, 1920, 1080]}`. Without `crop:"crop"`, the placement shrinks to the art bounds. Artboard N sits at y = N × 1180.
- The fonts lack ◆ ▲ →. Draw them as paths, or use them only in JetBrains Mono.

## Themes (passes B Aero, C Sky, D Seven)
Pass A "Survey" is the built-in base. Every other pass is a **theme module**: one Python file outside `tools/`, at `ArtSource/UI/passes/<b_aero|c_sky|d_seven>/theme_<name>.py`. Lanes never edit `ctd_ui.py` or `make_kit.py`. The API is frozen from now on: it only grows.

**Select a theme**
- In code: `ui.set_theme("path/to/theme_aero.py")`, or a name (`tools/theme_<name>.py` or `passes/*/theme_<name>.py`), or a module object. `None` or `"survey"` returns to pass A.
- Each call restores the base first, so switching never stacks.
- `ui.theme()` gives the current name; `ui.theme_module()` gives the module.
- From the shell:
  - `python3 ArtSource/UI/tools/make_kit.py --theme ArtSource/UI/passes/b_aero/theme_aero.py --out ArtSource/UI/passes/b_aero/svg --preview-dir ArtSource/UI/passes/b_aero/preview` writes `a1_foundations/ a2_controls/ a3_glass_hud/` and `<name>_<board>.png`.
  - `python3 ArtSource/UI/tools/make_hero.py --theme <same> --out <dir> --preview-dir <dir>` writes `<name>_hero.svg` and `.png`.
  - Both exit 1 if a check fails.

**What a theme module may define** (all optional)

| Name | Effect |
|---|---|
| `NAME`, `PASS_NAME` | Theme id and the board title, e.g. `"B · AERO"`. |
| `TOKENS = {...}` | Overrides any module-level token, and may add new ones, e.g. `"AERO_MIST": "#..."`. Covers swatches, alphas (`SMOKED_A` ...), `SHEEN_BAND`, `STREAK`, families (`DISPLAY UI_FACE UI_THIN DATA`), `TYPE`, `FORCED_WEIGHT`, `SPACE`, `SIZE`, `PALETTE`, `GLASS`, `PROOF_BG`, `TARGET_FACE`, `FONT_FILES_EXTRA`. Derived tokens (LIGHT, INK2/INK3/HAIR*, `*_SVG`, `GLASS`, `PROOF_BG`, `TYPE`, `PALETTE`, `FAMILIES`) are recomputed from the overridden ones unless the theme sets them itself. Every hex token you add counts as allowed colour for the checks. Keep the GLASS kinds `smoked` / `cobalt` / `frosted` and the TYPE role names: the boards use them. |
| `defs(ui) -> str` | Extra `<defs>` content (gradients, patterns, filters). Appended to pass A's defs, unless `REPLACE_DEFS = True`. |
| `OVERRIDES = {"name": fn}` | Replaces ANY public function by name: primitives (`rect text line poly path`), glyphs, motifs (`trace_brackets title_lockup floor_numeral hairline_grid` ...), `glass_pane`, every control and HUD component, `backdrop`, `hero_screen` (the hero composition), `layout_rules` (the theme's own layout checks), `contrast_table` (its own text / background pairs). Inside an override, `ui.base("glass_pane")(...)` calls the pass A version. |
| `BACKDROPS = {"kind": painter}` | Adds or replaces backdrop painters. `painter()` returns markup in 1920 × 1080 space; use it with `ui.backdrop("kind", ...)`. |
| `FAMILIES` | The font families the theme allows. Default: the families in `TYPE`. `make_kit` fails on any other. |
| `ALLOW_RADIUS = True`, `RADIUS = 4` | Opt in to rounded corners: `ui.rect(..., r=RADIUS)` emits rx. Pass A is False, and the check fails on any `rx`. `<circle>` and `<ellipse>` stay banned. |
| `BOARD_NOTES = {"a1_foundations": "Departs from the bible: ..."}` | Line drawn first in that board's DesignNotes. |
| `install(ui)` | Runs last, for anything else. |

**Fonts:** `text_width` measures any installed family. Pinned faces come from `FONT_FILES`; everything else is found by scanning the name tables of /System/Library/Fonts(+/Supplemental), /Library/Fonts and ~/Library/Fonts (.ttc / .ttf / .otf, typographic family plus OS/2 weight). A family with no file is logged once on stderr and listed in `ui.UNMEASURED`, and its widths are estimates.

**Traps, and how the API handles them**
1. Internal calls (`button` → `glass_pane`) look the callee up in module globals at call time. `set_theme` rebinds those globals, so overrides reach every internal call.
   - Never `from ctd_ui import glass_pane` in a theme: always call through `ui.`.
2. Colour defaults are token references (`fill="@NAVY"`), resolved at call time by `ui.tok()`. Write your own defaults the same way. A literal `ui.NAVY` captured at import is frozen.
3. Import-time values (`DEFS`, `GLASS`, `PALETTE`, `TYPE`, backdrops) are rebuilt by `set_theme`. Read them as `ui.GLASS` etc. at call time, never at your module's import.
4. Components draw at absolute 1:1 coordinates and record boxes for the checker. A replaced component should still call `ui.rect` / `ui.text` / `ui.glass_pane`, or `ui._reg("panes", ...)` for a pane, so the layout checks see it.
5. Text over the world stays ≥ 16 px. One `TraceCluster` group per board.

**Minimal theme**
```python
# ArtSource/UI/passes/b_aero/theme_aero.py
NAME, PASS_NAME = "aero", "B · AERO"
TOKENS = {"AERO_MIST": "#DDEBF7", "UI_FACE": "Avenir Next", "SMOKED_A": 0.9}
BOARD_NOTES = {"a3_glass_hud": "Departs from the bible: softer glass highlight."}

def defs(ui):
    return f'<linearGradient id="aeroSheen" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{ui.AERO_MIST}"/></linearGradient>'

def glass_pane(x, y, w, h, kind="smoked", **kw):
    import ctd_ui as ui
    return ui.base("glass_pane")(x, y, w, h, kind, **kw) + ui.rect(x, y, w, h * 0.4, "url(#aeroSheen)", opacity=0.3)

OVERRIDES = {"glass_pane": glass_pane}
```

## Comparison crops (every pass delivers these four PNGs)
Each pass puts four PNGs in `ArtSource/UI/passes/<a_survey|b_aero|c_sky|d_seven>/compare/`. Generate them with:

```
python3 ArtSource/UI/tools/make_compare.py --crops --theme <theme file> --out ArtSource/UI/passes/<lane>/compare
```

That command renders them to this spec, so every pass matches:

| File | Size | Content |
|---|---|---|
| `title.png` | 960 × 240 | The pass's `title_lockup(48, 64, size=72)` on its own field |
| `buttons.png` | 1440 × 160 | Primary button "Host game", 240 wide, in the five states left to right (default, hover, focus, pressed, disabled) at x = 48 + i × 272, top y 40. State name under each (caption, baseline y 140). |
| `briefcase_chip_bloom.png` | 960 × 240 | `backdrop("sky_bloom", crop=(560, 300, 960, 240))`. On it, `briefcase_tracker` "rival" (rival tower, floor 6) at (48, 32) and "you" (your tower, floor 7) at (48, 136). |
| `hero.png` | 960 × 540 | `hero_screen()` scaled to 50 % |

`python3 ArtSource/UI/tools/make_compare.py` builds the A | B | C | D sheet, with rows in the order above. It writes `ArtSource/UI/review/00_compare_passes.png` (3104 × 1227) and `svg/compare/compare_passes.svg`. A missing crop is drawn as a dashed "pending" cell.

## Floors
- The tower ladder has 12 segments: L (lobby), office floors 1–9, B (boardroom), R (roof). Floor N is segment N (`FLOORS`, `floor_name(n)`).
- No number above 9 is ever shown. Above floor 9, write BOARDROOM or ROOF.
- docs/systems/TOWER.md still says 15 floors with separate checkpoint floors. The GDD and the owner win for the mockups.

## Changelog
**2026-11-13: pass A reworked to the owner's UI guide (base visuals changed; API only grew).** What the lanes see if they don't override:
- HUD components (`hud_chip key_hint interact_prompt stairs_prompt item_slot briefcase_tracker briefcase_marker` tag, `open_floor toast`) now draw on a white `plate` with navy / ink-2 text and a 2 px cobalt top rule; warnings, the rival tracker, out of ammo and the stairs refusal draw on a solid `amber` plate with navy text. Set `HUD_GLASS = "smoked"` and `WARN_GLASS = "smoked"` in `TOKENS` to get the old dark chips back (the components switch their ink with the kind).
- New kinds `plate`, `clear`, `amber` (tokens `PLATE_A 0.90`, `CLEAR_A 0.40`, `AMBER_A 1.0`, `PLATE_SVG`, `CLEAR_SVG`, `LIGHT_KINDS`). A theme's own `GLASS` keeps its kinds and gains these. `smoked`, `cobalt` and `frosted` draw exactly as before.
- Pressed states on light are a pushed-in paper face with a stamped navy edge (primary: solid cobalt); no navy fill. The key-conflict cell and its hazard tile are amber plates. `section_header` and kit section bullets are near-black.
- Type: `DISPLAY` is Kohinoor Bangla Light (`DISPLAY_WEIGHT 300`), heading tracked +6, title tracked `TITLE_TRACK` 0.22 em; `hud` 400, `caption` 400, `micro` 300; targets Saira Expanded / Archivo SemiExpanded / IBM Plex Mono. **A theme that sets any family token keeps the pre-guide scale** (display weight 500, title 0.06 em, old role weights), so B, C and D render as before.
- `interact_prompt`, `stairs_prompt`, `stairs_refusal` default `key="F"`.
- `title_lockup` tracks at `TITLE_TRACK`; `cyan_square(..., keyline=None)` takes a keyline for light surfaces.
- `PALETTE` role lines rewritten; new defs `skyDeep skyHaze sheenClear dotScreen dotScreenInk`.
- `contrast_table()` rows now cover plate / amber / frosted / cobalt per colour and per backdrop worst pixel; smoked rows are listed as not used. `glass_ratio(kind, backdrop, txt, linear=True)` takes an alpha text token and an SVG flag.
- `layout_rules` (survey only) adds: no amber or cyan text, no light text on a light pane, no text on clear glass.
- make_kit: every label naming a face, kind or alpha is read from the theme (`TYPE`, `TARGET_FACE`, `GLASS`, `FORCED_WEIGHT`); A3 proves the kinds in the new token `PROOF_KINDS` (pass A: plate, frosted, clear, amber; a theme that sets its own `GLASS` proves its first four kinds; otherwise it inherits pass A's, unless it sets `PROOF_KINDS`) and draws kinds in `NO_TEXT_KINDS` as frames; the crosshair zones and motif cells are painted backdrops or paper, no navy swatch cells. Board items are named as before except A1 motifs (`WindowCell ChevronCell BarcodeCell SlotCell` new, `CyanSquares CobaltBlock` gone) and A3 (`RivalTowerTrace`; the marker cell lost its trace).
- `hero_screen` is the window-frame composition (ref 08 device): sky with stepped cel cumulus in six panes, the rival tower with the trace, title and menu low on white.

