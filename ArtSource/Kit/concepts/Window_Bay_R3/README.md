# Window bay / round 3

**Status:** Target concepts for Alex's review · **Last updated:** 2026-11-11

Sunlit white architecture, a saturated cobalt sky, simple block-city context, and dark suits behind two near-clear panes.
These are disposable Blender studies of the window bay and its columns; no production kit asset is supplied or changed.

Start with the [comparison board](comparison.png) and the literal [300 px thumbnail strip](thumbnail_strip.png).

| Direction | One idea | Review | Build notes |
| --- | --- | --- | --- |
| **01 / White Trays — pick** | Whole-floor porcelain shelves float over deep dark reveals. | [Presentation](01_White_Trays/presentation.png), [turnaround](01_White_Trays/sheet.png), [A/B rails](01_White_Trays/rail_finishes.png), [2 m / corners](01_White_Trays/infill_corner_sheet.png) | [Notes](01_White_Trays/notes.md) |
| **02 / Blue Spandrel** | A full metre of glossy cobalt glass wraps the solid sill. | [Presentation](02_Blue_Spandrel/presentation.png), [turnaround](02_Blue_Spandrel/sheet.png), [A/B rails](02_Blue_Spandrel/rail_finishes.png), [2 m / corners](02_Blue_Spandrel/infill_corner_sheet.png) | [Notes](02_Blue_Spandrel/notes.md) |

## What I would build

**White Trays with B / Deep Navy rails.**
The whole-floor white mass and dark opening have the clearest 2001 corporate character while keeping Paper White dominant.
The 120 mm-deep sill pocket and 160 mm pier setback are large enough to explain the elevation from across the street.
Blue Spandrel is the stronger colour alternative and needs the least geometry work, but commits the repeating base kit to much more cobalt.
The third experiment, Split Blades, was culled: at a literal 300 px its paired vertical edges still read too close to White Trays.
Its [raw close-up](03_Split_Blades/close.png), [rival view](03_Split_Blades/rival_B.png), and [notes](03_Split_Blades/notes.md) remain as process evidence; it is not a shortlisted direction.
These are recommendations for Alex's concept approval, not a production design decision.

## Source and fixed sizes

Read in order: `FEEDBACK_02.md`, `FEEDBACK_01.md`, `REQUEST.md`, the kit README, and `kit_pieces.py`.
Round 3's brief governs the design and staging; the kit script governs the actual coordinates.
The attached reference plates informed lighting, palette and architectural confidence only; there are no copied layouts, logos or signage.

| Element | Unity coordinates / metres |
| --- | --- |
| Bay | X 0–4 or 0–2; Y 0–4.30; Z -0.20–0; pivot (0,0,0) |
| Sill envelope | Full run × 0.20 depth × 1.00 height; opaque backing retained behind every recess |
| Rails | X 0.20–run-0.20; Y 1.00–1.05 and 4.25–4.30; Z -0.15–-0.05 |
| Pane | 3.60/1.60 × 3.20 × 0.02; Z -0.11–-0.09; no intermediate mullion |
| Facade column | X -0.20–+0.20 about grid point; Z -0.22–+0.02; Y 0–4.30 |
| CornerL | X -0.02–+0.40 about grid corner; same depth and height |
| CornerR | X -0.40–+0.02 about grid corner; same depth and height |
| Floor / slab | 4.50 floor-to-floor; slab 0.20; floor contact 0, soffit 4.30 |
| Figure | 1.80 m; Deep Navy #111B38, matte, small white shirt V |

Scene axes are X = Unity X, Y = Unity Z, Z = Unity Y, solely for straightforward Blender staging.
This is not the source kit's FBX-export coordinate transform.
Blue pivot crosses mark the original bay snap corner, projected along the sightline where necessary.
Front, side and section are true orthographic views; three-quarter views are shaded orthographic axonometrics.
The section is a 40 mm slice through the bay centre, with blue cut-face fill; it is not a column section.
The close-up shows the actual column profile, and the extra corner sheet shows both handed columns on a 2 m infill.
The turnaround retains the requested room-side three-quarter; the presentation also supplies a street-side three-quarter.

All three directions have 0 mm proud additions.
Side/end snap lands, floor/soffit contacts, original bounds and pane/rail positions are retained.
The carved sills remain opaque over the full 1 m cover height, with at least 80 mm continuous rear backing.
Original box colliders are a constraint on a future visual mesh; these studies do not create or alter colliders.
Flush finish assignments are used instead of the round-2 render-offset skins.

## Staging and glass

The rival camera is exactly 12.00 m beyond the facade grid, at 1.70 m on the middle floor, looking horizontally at the same height.
It sees three 4 m bays across three floors at elevations -4.50, 0 and +4.50 m.
Two suits stand on the middle floor and one upstairs; the upper figure is close enough to its window to show its jacket above the sill.
A second, observer pane sits at street Z +11.69–+11.71 m, between camera and target.
The 1 m cover sill correctly hides the figures' lower bodies in this view.

Each whole closed pane has 95% direct transparency: each of its two faces mixes `1 - sqrt(0.95)` nonreflective, dark-blue diffuse with transparent surface.
Two panes therefore retain 90.25% of the direct image contribution, before the small surface term.
The shader has no mirror component and no pale diffuse film; the dark suits were visually inspected through both panes.
This is a separate concept shader using the `Glass_Clear` slot name, not evidence about the source kit's or Unity's glass material.

The room camera is at 1.70 m, showing three bays, grey-blue carpet tiles, white ceiling grid and placeholder strips.
Hard sun rakes along the street, while an interior wall wash holds a bright Paper White background behind the suits.
The sky has pale horizon haze and a saturated cobalt upper hemisphere; white/blue city masses have no facade detail.
City blocks, carpet, ceilings, lights, figures and the observer pane are staging context, not additional kit concepts.

## Materials proposed for review

- `Trim_Navy`: dark recesses and B rail finish in all directions.
- `Glass_Spandrel`: opaque, glossy backpainted Corporate Cobalt, only in direction 02.
- `Light_Strip`: ceiling-strip staging only; not a luminous window or a new facade feature.

A rails use the existing `Frame_Aluminium` slot and Silver Grey #C9CED3.
B rails use identical geometry with proposed `Trim_Navy` #111B38.
Paper White is #F3F4F4; carpet staging uses the current placeholder #8A94A3.
No new material slot has been added to the actual kit.

## Reproduce

From the repository root, PowerShell:

```powershell
& 'C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe' -b --factory-startup -t 12 -P 'ArtSource/Kit/concepts/Window_Bay_R3/render_concepts.py' -- --force
powershell -NoProfile -ExecutionPolicy Bypass -File 'ArtSource/Kit/concepts/Window_Bay_R3/compose_pages.ps1'
```

Blender 5.3 / Cycles / OptiX / 64 samples / denoising / AgX High Contrast.
Raw final renders are 1600 × 1200; the main boards are 3000 px wide.
`thumbnail_strip.png` contains the two shortlisted, un-cropped rival images, each resampled to exactly 300 × 225 pixels.
The source, all render outputs, logs, projected dimension metadata and OptiX cache remain inside this folder.
There are no `.blend` or FBX exports; the complete reproducible geometry is in the Python source.

Optional render arguments: `--direction 0`, `1`, or `2`; `--shots rival_B,room_B,close`; `--preview` for a 60% / 20-sample preview; `--force` to replace cached PNGs.
Without `--force`, existing PNGs are reused.
`render_metadata.json` is updated after every shot and stores actual camera positions and projected dimension endpoints.

## Review and limitations

The initial previews were rejected during self-review for crowded skyline, weak exterior lighting and an inverted sky gradient; those views were rerendered.
Studio views were also revised for section contrast, and the tray columns gained full-depth side snap lands before final delivery.
The final rival views, room views, close-ups, dimensioned sheets and literal thumbnail strip were visually reviewed.
Suit visibility is a Blender concept result; there was no Unity, Play Mode, Steam, multiplayer or performance validation.
No automated tests were requested or run.

Static/manual verification commands, from the repository root:

```powershell
(Get-ChildItem -LiteralPath 'ArtSource/Kit/concepts/Window_Bay_R3' -Recurse -Filter '*.png').Count
$r3Meta = Get-Content -LiteralPath 'ArtSource/Kit/concepts/Window_Bay_R3/render_metadata.json' -Raw | ConvertFrom-Json
$r3Meta.PSObject.Properties.Name.Count
Select-String -Path 'ArtSource/Kit/concepts/Window_Bay_R3/render_final.log','ArtSource/Kit/concepts/Window_Bay_R3/render_revision_final.log' -Pattern 'Traceback|\| ERROR|^Error:'
$r3Tokens=$null
$r3Errors=$null
[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path 'ArtSource/Kit/concepts/Window_Bay_R3/compose_pages.ps1'),[ref]$r3Tokens,[ref]$r3Errors) | Out-Null
$r3Errors.Count
```

Verified results: 33 raw render entries (including the culled study), 49 PNGs including annotations/boards, zero parser errors, and zero traceback/render-error matches in the final render logs.
The installed Cycles build emits a HIP initialization warning while discovering devices; final rendering uses the available NVIDIA OptiX device.
The early preview logs also show an unavailable default OptiX cache path; the source now directs that cache into this folder.

Only this round-3 directory and a new section in `concepts/STATUS.md` are part of this hand-back.
No state-changing git commands were run.
Work stops at the window bay pending Alex's review.
