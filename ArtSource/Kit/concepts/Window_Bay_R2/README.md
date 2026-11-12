# Window bay — round 2

**Status:** Concept proposals for Alex's review · **Last updated:** 2026-11-11

Three competing approaches to the same fixed window kit: continuous street bands, luminous room-side piers, and graphic corner markers.
These are disposable Blender studies, not approved models or exported kit assets.

Start with [comparison.png](comparison.png), then open each direction's `presentation.png` for room/street A/B comparisons and `sheet.png` for the shaded turnaround.

| Direction | Idea | Review pages | Detail notes |
| --- | --- | --- | --- |
| Dark Bands | White piers have deep twin reveals; navy street belts join across them. | [Presentation](01_Dark_Bands/presentation.png), [turnaround](01_Dark_Bands/sheet.png), [2 m / corners](01_Dark_Bands/infill_corner_sheet.png) | [Notes](01_Dark_Bands/notes.md) |
| Lit Piers | Rounded white cheeks surround a deep luminous room-side slot; silver caps float above a dark line. | [Presentation](02_Lit_Piers/presentation.png), [turnaround](02_Lit_Piers/sheet.png), [2 m / corners](02_Lit_Piers/infill_corner_sheet.png) | [Notes](02_Lit_Piers/notes.md) |
| Cobalt Corners | Diagonal cobalt blocks mark corner columns, leaving the field piers white. | [Presentation](03_Cobalt_Corners/presentation.png), [turnaround](03_Cobalt_Corners/sheet.png), [2 m / corners](03_Cobalt_Corners/infill_corner_sheet.png) | [Notes](03_Cobalt_Corners/notes.md) |

## Common geometry and presentation

Source read: `FEEDBACK_01.md`, `REQUEST.md`, `../README.md`, `../kit_pieces.py`, in that order.
Feedback supersedes the earlier quiet/detail limit; new material names below remain proposals.
The kit script is authoritative for local extents and handed corner placement.

| Element | Preserved dimensions, Unity coordinates in metres |
| --- | --- |
| Bay / pivot | X 0–4 or 0–2; Z −0.20–0; Y 0–4.30; pivot (0,0,0), blue cross projected in views |
| Solid sill | Full run × 0.20 deep × 1.00 high; full solid cover is retained, with surface finishes layered over it |
| Rails | X 0.20–run−0.20; Y 1.00–1.05 and 4.25–4.30; Z −0.15–−0.05 |
| Pane | 3.60 or 1.60 wide × 3.20 high × 0.02 deep; Z −0.11–−0.09; no intermediate bars |
| Facade pier | X ±0.20 about grid point; Z −0.22–+0.02; Y 0–4.30 |
| CornerL | X −0.02–+0.40 about grid corner; same depth and height |
| CornerR | X −0.40–+0.02 about grid corner; same depth and height |
| Slab context | 0.20 thick; lower −0.20–0; upper 4.30–4.50; floors repeat every 4.50 |
| Figure | 1.80 high; Deep Navy suit, simple white shirt triangle |

Front, side and mid-bay section are orthographic shaded renders.
The section is a 40 mm slice through the middle of the bay, not through a column; its wide-looking white mass is the sill, not a full-height wall.
The turnaround's three-quarter shows the room side with slab context; `three_quarter.png` is an additional street-side angle.
The extra infill page shows both handed corners against short wall returns on a 2 m bay.
All source render PNGs are 1400 × 950; turnaround pages are 3000 px wide.
Dimensions are projected from scene coordinates, then labelled by `compose_pages.ps1`.
The side and section show the bay pivot's projection along the sightline, rather than a different asset pivot.

Hero cameras are at 1.70 m height, room side, viewing three repeated 4 m modules.
Street cameras are at Unity Z +12.00 m, showing three bays over two floors.
A separate observer pane sits between the street camera and target facade, so figures are viewed through two conceptual glass surfaces.
The 1 m solid sill correctly hides the lower part of the standing figure.
The glass shader mixes 95% transparent with 5% pale diffuse surface, with no reflective component; it is an art comparison, not the production Unity shader.
Simple distant tower masses appear in room views only as mood context, without reference logos or copied layouts.

Rail A uses existing `Frame_Aluminium` in Silver Grey #C9CED3.
Rail B proposes `Trim_Navy` #111B38 for identical geometry.
White is `Wall_PaperWhite` #F3F4F4; carpet context uses `Floor_Carpet` #8A94A3.
New accent and light slots are proposals, not changes to the kit's material contract.
Decorative skins stand at most 1 mm proud of visible faces; caps sit within the sill envelope; reveal recesses leave the sill untouched.
Small radii in these throwaway studies illustrate the visible treatment; production modelling must preserve flat snap lands at top, bottom and module ends.
The 1 mm finish skins are render offsets rather than a proposed extension of snap end faces: use flush material regions where a belt reaches a module end or bottom edge in production.
No runtime code, colliders, snap definitions, source kit files or Unity assets were modified.

## Reproduce

From the repository root, PowerShell:

```powershell
& 'C:/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe' -b --factory-startup -t 12 -P ArtSource/Kit/concepts/Window_Bay_R2/render_concepts.py
powershell -NoProfile -ExecutionPolicy Bypass -File ArtSource/Kit/concepts/Window_Bay_R2/compose_pages.ps1
```

Blender 5.3 / Cycles, 24 samples, denoising, AgX, large cool daylight and soft interior fill.
`render_concepts.py` contains the design specification and all geometry, material and camera settings; no image-generation service or API was used.
`render_metadata.json` records projected dimension endpoints.
The script regenerates every shot; optional `.blend` saves are omitted because Blender's thumbnail writer stalled in this environment.
Existing PNGs are reused on a repeat run; set `$env:R2_FORCE='ALL'` before the Blender command to regenerate them all.

## Review boundaries

Exact kit values were compared directly with the source script; rendered pages were visually inspected.
Static validation: 49 PNGs, 30 source-render metadata entries, zero PowerShell parser errors, and zero Python tracebacks / Blender render-error lines in the completed render logs.
Earlier Blender deprecation notices were wrapped by PowerShell as NativeCommandError; the final cached annotation pass exits 0 with those notices suppressed.

Reproducible inventory and error-log commands (repository root):

```powershell
(Get-ChildItem ArtSource/Kit/concepts/Window_Bay_R2 -Recurse -Filter *.png).Count
$r2Data = Get-Content ArtSource/Kit/concepts/Window_Bay_R2/render_metadata.json -Raw | ConvertFrom-Json
$r2Data.PSObject.Properties.Name.Count
Select-String -Path ArtSource/Kit/concepts/Window_Bay_R2/*.log -Pattern 'Traceback|\| ERROR|^Error:'
$r2Tokens = $null
$r2Errors = $null
[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path 'ArtSource/Kit/concepts/Window_Bay_R2/compose_pages.ps1'), [ref]$r2Tokens, [ref]$r2Errors) | Out-Null
$r2Errors.Count
```
No automated tests, Unity compilation, Play Mode, multiplayer or gameplay checks were run.
Two-pane suit visibility here does not establish production visibility, glass performance or an acceptable final material.
The concept meshes contain separate overlapping finish skins and are not production topology, collision or export deliverables.
The source kit's `Glass_Clear` call specifies base colour and roughness without explicit transparency in that file; these scenes supply a separate concept shader and do not validate the source material's rendering.
Round 1's note that source files were unavailable does not apply to this round.
Alex chooses the architectural direction and A/B finish before any production kit work.
