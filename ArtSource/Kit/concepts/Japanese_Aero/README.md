# Japanese corporate Aero — KitOffice paintovers

**Status:** Target concept; awaiting Alex's selection · **Last updated:** 2026-11-13

Two material and dressing directions painted over the four supplied Unity captures.
A is glossy aqua eco-tech; B is pale silver Soft Club.
The fictional Japanese company, office props and bilingual signs belong to this proposal.
[REQUEST.md](REQUEST.md) is the brief for this concept, including its explicit permission for Aero imagery.

## Review the images

[Open the labelled contact sheet](contact_sheet.png): original, A, B from left to right; L1, L3, L4, L7 from top to bottom.
Each individual paintover is a full-frame **1600 × 900 PNG (16:9)**.

| Capture | A — Aero | B — Soft Club |
|---|---|---|
| L1 — window corridor | [A](unity_office_L1_window_corridor_A.png) | [B](unity_office_L1_window_corridor_B.png) |
| L3 — atrium stair | [A](unity_office_L3_atrium_stair_A.png) | [B](unity_office_L3_atrium_stair_B.png) |
| L4 — meeting room through glass | [A](unity_office_L4_room_through_frost_A.png) | [B](unity_office_L4_room_through_frost_B.png) |
| L7 — upper floor over atrium | [A](unity_office_L7_upper_over_atrium_A.png) | [B](unity_office_L7_upper_over_atrium_B.png) |

**Recommendation: build A.**
Its aqua identity, living green and period office equipment make the Japanese corporate direction clearer than B's nearly neutral treatment.
Keep water to one shallow atrium wall feature and use the controlled gloss values below so reflections remain background information during combat.

## Palette and material targets

Hex values are sRGB base colours, rather than colours sampled from the lit images.
Smoothness is the Unity-style 0–1 gloss control; for an initial Blender match, use roughness = 1 − smoothness.
These are starting art targets, not measured shader values from the generated pictures.
Opacity 1 means opaque; translucency on plastics describes a visual finish, not a change to collision or cover.

| Material / use | A colour | B colour | Smoothness A / B | Metalness | Opacity / translucency |
|---|---|---|---|---|---|
| White wall lacquer, column skins | `#F4FAF8` | `#F3F6F6` | 0.68 / 0.40 | 0 | 1; solid |
| Polished circulation floor | `#DDECF0` | `#DFE6EB` | 0.72 / 0.46 | 0 | 1; broad reflections, no mirror finish |
| Work-area carpet | `#7894AA` | `#8C9DAE` | 0.08 / 0.06 | 0 | 1; short pile, subtle 0.5 m tiles |
| Ceiling / light housings | `#EFF5F5` | `#F0F2F4` | 0.24 / 0.20 | 0 | 1 |
| Clear glass, very light edge tint | `#E8F8F5` | `#F1F7FA` | 0.94 / 0.90 | 0 | Approx. 0.04 / 0.03 surface opacity; no blur across figures |
| Frosted plastic / approved glass frost bands | `#BCEDE9` | `#D8E3E9` | 0.64 / 0.43 | 0 | Plastic 0.55 / 0.65; glass frost bands 0.70; clear sightline remains separate |
| Anodised / brushed silver | `#C8D2D9` | `#BAC6CF` | 0.70 / 0.53 | 0.85 | 1; fine directional brushing |
| Aqua sign faces / plastic caps | `#45BFC7` | `#A8BFD1` | 0.76 / 0.46 | 0 | Opaque on walls; 0.55 on decorative acrylic edges |
| Secondary sky-blue print | `#568BCB` | `#A8BFD1` | 0.45 / 0.30 | 0 | 1; opaque panels only |
| Text, chair fabric, dark-suit reference | `#172C48` | `#24344C` | 0.12 / 0.10 | 0 | 1; keep fabric distinct from shiny silver |
| Hanko / logo seal accent | `#C7473E` | `#BA6960` | 0.35 / 0.28 | 0 | 1; tiny, infrequent printed accent |
| Leaves, light / shadow | `#70AF59` / `#315F45` | `#8FA58B` / `#536C60` | 0.24 / 0.18 | 0 | 1 on leaf faces; cutout edges |
| Water / LED accent | `#7ADAD8` | `#B9CFDA` | 0.90 / 0.50 | 0 | Water is a contained visual layer; LED is a restrained emissive dot pattern |

Keep structural white readable under the existing cool daylight, with real shadow depth rather than dirty black halos.
Reserve richer aqua for opaque sign faces and accessories; tinting every pane aqua would muddy overlapping silhouettes.
Use a static contained water texture / normal treatment for the concept's water wall before considering any animated presentation.

## Kit changes and dimensions

All dimensions are metres, given as width × depth × height unless noted.
**Existing** dimensions below come from the current [kit source](../../kit_pieces.py) and [kit README](../../README.md); they are not measurements inferred from the pictures.
**Proposed** dimensions are modelling targets for new dressing and need fitting against the original scene after Alex chooses a direction.
Use the existing meshes, pivots, snap ends and placements as the dimensional master; generated bevels and curved cladding are finish suggestions.

### Existing pieces to restyle

| Kit piece / family | Existing size | Proposed art change |
|---|---|---|
| `Slab_4x4`, `Slab_2x4`, `Slab_4x2`, `Slab_2x2` | 4 / 2 m bay runs; 0.20 thick | Pale polished circulation surface; retain slab edges and openings |
| `Carpet_Overlay_4x4` | 4 × 4 × 0.008 thick | Lighter blue carpet in the existing work-area footprints |
| `Wall_Solid_4m`, `_2m`, cobalt / portal variants | 4 / 2 × 0.20 × 4.30 | White lacquer and silver reveals; replace cobalt fields with A aqua or B mist blue |
| `Wall_Door_4m`, `Door_Leaf` | Wall 4 × 0.20 × 4.30; leaf 1.18 × 0.04 × 2.38 | Same opening and swing; silver edge and pale face |
| `Wall_Return_2x2` and cobalt variant | 2 × 2 m cell, 0.20 thick, 4.30 high; outer radius 1.0 | Rounded lacquer island ends, same snap envelope |
| `Wall_Niche_4m` | 4 × 0.20 × 4.30; niche 0.60 wide × 0.06 deep × 3.40 high | Aqua / milky plastic insert; no added projecting furniture |
| `Window_4m`, `_2m`, façade column / corner families | 4 / 2 m runs, 0.20 deep, 4.30 high; existing sill retained | Silver trims, near-clear glazing; retain exterior bay rhythm |
| `Column_Graphic`, `_Tall` | 0.80 × 0.80 × 4.30 / 8.80; existing corner radius 0.06 | Aqua or pale-blue wrap graphics on the opaque column; retain the source section, even where the paintover suggests a rounder shell |
| `Partition_Frost_4m`, `_2m` / glass families | 4 / 2 × 0.10 × 2.50 | Silver perimeter, separate frost bands and a near-clear middle |
| `Partition_Header_4m`, `_2m`, `_Curve_2x2` | Existing straight runs / 2 × 2 m curved cell; curve radius 1.50; header from 2.50 to 4.30 high | Pearl-white header, silver jambs; a sliding-screen appearance on the existing edge, not an extra closed panel |
| `Stair_Straight_2m` | 2.00 × 7.20; rise 4.50; guarded envelope 5.60 high | White folded supports, silver tread nosings, clear guards; preserve all 24 risers and landings |
| `Guard_Glass_4m`, `_2m`; existing posted guard family | 4 / 2 m runs, glass 0.06 deep, 1.10 high; glass-family shoe 0.12 high | Frameless visual treatment; retain the barrier footprint and place opaque fittings low, rather than copying bright horizontal bars from a generated image |
| `Counter_Capsule_2m` | 2.00 × 1.00 × 1.10 | Rounded white body, silver base, thin aqua / pearl cap; the L4 counter stays in place |
| `Cover_Block_2m` | 2.00 × 0.60 × 1.10 | Same cover silhouette, white lacquer and subtle silver detail |
| `Desk_Sculpt` | 1.60 × 0.80 × 0.74 | Pearl top, silver edge; add accessories to the existing desk |
| `Workstation_Bench` | 2.40 × 1.60; existing divider top 1.15 | Retain facing pairs and divider height, which is approximately the brief's 1.1 m cover line; restyle the divider instead of creating a taller screen |
| Existing monitor / drawer pedestal | Monitor panel 0.55 wide × 0.34 high; pedestal 0.40 × 0.55 × 0.60 | Silver LCD bezel and substantial foot; dark displays, white / grey keyboards |
| `Chair_Task`, `Chair_Pod` | Task seat 0.47 high; pod 0.52 × 0.50 × 0.86, seat 0.46 high | Navy fabric / shell, brushed-silver base; keep chair positions and silhouettes |
| `Storage_Low`, `Copier` | Storage 1.60 × 0.45 × 1.00; copier 0.62 × 0.66 × 1.05 | Silver handles and panel seams, white bodies; no extra tall opaque storage |
| `Desk_Reception` | 4.00 × 0.90 × 1.10 | Replace the former English firm's identity with AONAGI on the existing desk, wherever it is used; do not add a new desk to L1 |
| `Sign_Zone_L`, `_R`, `_C`, `_H`, `_A` | Face 2.40 wide × 3.20 high, 0.014 proud; bottom 0.60 above floor | Bilingual sign family below; A has water / bubble graphics, B has a thin mist-blue field |
| `Planter_Box` | 1.00 × 0.45 × 0.55 pot | Silver / white pot and readable green leaf clusters; use existing planter positions first |
| `Light_Pendant_Linear`, `Ceiling_Grid_4x4` | Pendant 2.40 × 0.07 × 0.06, drop 0.80; grid 4 × 4, 0.008 thick | Silver housings, soft white light, subtle ceiling grid; retain hanging positions |

### New dressing to model only after selection

| Prop / detail | Proposed size | Placement / construction |
|---|---|---|
| Aqua / pearl column sign skin | 0.55 × 0.012 × 1.50 | Flush to the existing opaque column; floor code lettering about 0.45 high |
| Open-ring reception emblem | Outer diameter 0.40; stroke 0.028; red dot diameter 0.05 | Wall / existing reception face; a small graphic rather than a projecting sculpture |
| Reception LED status sign | 1.20 × 0.06 × 0.18 | Above the L1 wall logo; lowest edge about 2.65 high; dot-matrix face, not a large screen |
| Contained water-wall cassette, A | 1.80 × 0.08 × 2.70 | Fit to the existing opaque curved atrium wall behind the stair; no pool or route intrusion |
| Pearl acrylic replacement, B | Same 1.80 × 0.08 × 2.70 | Same wall location, dry and quietly lit |
| Water server, including bottle | 0.35 × 0.35 × 1.35 | L1 against the reception wall, outside circulation |
| Umbrella stand | 0.22 × 0.22 × 0.55; umbrellas up to 1.00 overall | Beside the server / entrance wall, not against clear sightline glass |
| Drinks vending machine | 0.90 × 0.65 × 1.80 | L7 rear opaque wall; fictional bottle labels, white / silver casing |
| Slipper cabinet | 0.90 × 0.35 × 0.70 | Rear entrance wall beside the vending area; shallow open cubbies or pale doors |
| White desk phone | 0.24 × 0.22 × 0.10 | Existing desk corners; curled handset cord, small buttons |
| Two-tier document tray | 0.28 × 0.34 × 0.18 | Clear aqua A / milky-white B; keep below the workstation cover top |
| Folder / paper stack | 0.24 × 0.32 × 0.03 | A few tidy stacks, not piles that create new cover |
| Supplemental small plant | Pot diameter 0.28, height 0.32; total plant height 1.40 | Against a solid background wall; keep leaves out of routes and clear glass viewing strips |

The upper-floor bench islands already face staff toward one another.
Dress the existing far head station as the manager's position using a phone, document tray and a tidier surface; do not lengthen a bench or add a desk across a passage.
Tall new props belong against opaque walls, so they do not become extra cover in an open route.

## Exact sign copy and fictional company

Use this text for real typesetting; small generated glyphs and micro-lettering in the paintovers are concept approximations.
Use a Japanese sans serif with full glyph coverage, restrained English capitals, and a consistent Latin zone code.
Japanese is primary, English secondary; romanisation is supplied for the rebuild team and does not need to become a third line on every sign.

| Zone | Japanese | Romanisation | English |
|---|---|---|---|
| L | 法務部 | Hōmu-bu | LEGAL |
| R | 審査部 | Shinsa-bu | REVIEW |
| C | 請求部 | Seikyū-bu | CLAIMS |
| H | 審問室 | Shinmon-shitsu | HEARINGS |
| A | 記録保管室 | Kiroku hokan-shitsu | ARCHIVE |

L, R and C retain the brief's department names.
H and A gain **室** to identify a room rather than an activity; 審問室 is an institutional hearing room, appropriate to the fictional legal bureaucracy rather than an ordinary meeting room.
請求部 interprets CLAIMS as a department handling claims / demands, rather than complaints; no insurance-specific remit is added.
Official usage supporting the room terminology: [Fukuoka court building guide, 審問室](https://www.courts.go.jp/fukuoka-h/vc-files/fukuoka-h/file/sougouannai.pdf) and [MHLW form using 診療記録保管室](https://kouseikyoku.mhlw.go.jp/tohoku/000432077.pdf).

| Application | Exact Japanese / code | Romanisation | English |
|---|---|---|---|
| Company legal name | 青凪総合サービス株式会社 | Aonagi Sōgō Sābisu Kabushiki Gaisha | Aonagi Corporate Services Co., Ltd. |
| Reception display name | 青凪総合サービス | Aonagi Sōgō Sābisu | AONAGI |
| Reception LED | 受付 1F | Uketsuke, ikkai | RECEPTION |
| Lower-storey floor marker | 1F / 1階 | Ikkai | FLOOR 1 |
| Upper-storey floor marker | 2F / 2階 | Nikai | FLOOR 2 |
| Meeting-room plaque | 会議室 | Kaigi-shitsu | MEETING ROOM |
| Water-server label | 給水 | Kyūsui | DRINKING WATER |
| Vending area label | 自動販売機 | Jidō hanbaiki | VENDING |
| Umbrella stand label | 傘立て | Kasa-tate | UMBRELLAS |
| Slipper-cabinet label | 室内履き | Shitsunai-baki | INDOOR SLIPPERS |

The proposed company is invented for this concept; the open ring suggests blue water and calm air, with one red circular hanko-like dot at its lower-right opening.
Use the seal sparingly and keep it separate from any existing gameplay state indicators.
**L1, L3, L4 and L7 are capture IDs, not storeys**: these views use 1F and 2F, and do not imply a seventh floor.
A later 3F marker would follow the same typography, without being added to this two-storey capture set.

## Rebuild interpretation and review limits

The paintovers hold the principal camera, room, stair, wall, column, desk and counter positions as a visual comparison; they are not pixel-exact geometry plans.
Image generation rounds some column profiles and simplifies rail hardware, and incidental exterior details vary.
Retain the source kit's measured geometry when translating these surface treatments back to Blender and Unity.

The near-clear viewing strip is 0.35–2.10 above each adjacent floor; frost stays outside it.
Workstation dividers remain existing cover, not decorative full-height frosted partitions.
The navy capsules are the original captures' readability markers; the images do not establish actual suited-player visibility through two panes or from the floor above.
That assessment belongs to the subsequent selected kit in the existing scene.

The water wall, LED display, vending machine and cabinet are art proposals; this delivery introduces no gameplay system or network contract.
Existing system documentation does not need changing for an unselected concept; this guide records the proposed art targets inside the requested folder.

## Generation and file validation

**Image tries: 9 total — 8 first-pass paintovers plus 1 local refinement to L1 A for the reception LED.**
All nine image calls completed; eight final paintovers are delivered, with the refined L1 A selected.
The built-in image generation tool was used throughout, with each original capture supplied as its edit target; the refinement used the generated L1 A.
[PROMPTS.md](PROMPTS.md) records the exact common prompt, variants, capture instructions, refinement and call-to-file mapping.

Generated images were normalised from 1672 × 941 to 1600 × 900 without cropping; no scene content was composited or painted by a script.
The contact sheet is a labelled layout of the original captures and final images, assembled with PowerShell / System.Drawing.
Visual inspection covered all eight outputs for the principal composition, A/B distinction, clear glazing and dark marker presence.
The original scene remains the authority for routes, dimensions and collision.

**Validation results:** 9 PNGs decoded; all eight paintovers are 1600 × 900, the contact sheet is 2480 × 2210, and all nine SHA256 hashes differ.
All 13 relative file links in this README resolved, with zero missing files.
The contact sheet was opened and visually checked after assembly: four labelled rows, original / A / B in the requested order, without cropped thumbnails or clipped labels.
These are artifact checks; no automated tests, Unity, Play Mode or multiplayer validation was run for this concept task.

Exact PowerShell validation command, run from the repository root:

```powershell
Add-Type -AssemblyName System.Drawing
$taskOut = Join-Path (Get-Location) 'ArtSource/Kit/concepts/Japanese_Aero'
$taskStems = @('unity_office_L1_window_corridor','unity_office_L3_atrium_stair','unity_office_L4_room_through_frost','unity_office_L7_upper_over_atrium')
$taskNames = @($taskStems | ForEach-Object { $_+'_A.png'; $_+'_B.png' }) + @('contact_sheet.png')
foreach ($taskName in $taskNames) {
 $taskPath = Join-Path $taskOut $taskName
 $taskImage = [System.Drawing.Image]::FromFile($taskPath)
 try {
  $taskExpectedWidth = 1600; $taskExpectedHeight = 900
  if ($taskName -eq 'contact_sheet.png') { $taskExpectedWidth=2480; $taskExpectedHeight=2210 }
  if ($taskImage.Width -ne $taskExpectedWidth -or $taskImage.Height -ne $taskExpectedHeight) { throw ('Wrong size: '+$taskName) }
  Write-Output ($taskName+' | '+$taskImage.Width+' x '+$taskImage.Height+' | PNG decode OK')
 } finally { $taskImage.Dispose() }
 $taskHash = Get-FileHash -Algorithm SHA256 -LiteralPath $taskPath
 Write-Output ('SHA256 '+$taskHash.Hash)
}
Get-Item -LiteralPath (Join-Path $taskOut 'README.md'),(Join-Path $taskOut 'PROMPTS.md') | Select-Object Name,Length
```

Exact link check, run in Node REPL with `fs` imported from `node:fs/promises` and `path` from `node:path`:

```javascript
var reviewDir = path.join(nodeRepl.cwd,'ArtSource/Kit/concepts/Japanese_Aero');
var readmeText = await fs.readFile(path.join(reviewDir,'README.md'),'utf8');
var relativeTargets = [...readmeText.matchAll(/\]\(([^)]+)\)/g)].map(m=>m[1]).filter(p=>!/^https?:/.test(p));
var brokenTargets=[];
for(var target of relativeTargets){
  try{await fs.access(path.resolve(reviewDir,target));}
  catch{brokenTargets.push(target);}
}
nodeRepl.write({relativeLinksChecked:relativeTargets.length,brokenLinks:brokenTargets,pngCount:(await fs.readdir(reviewDir)).filter(f=>f.endsWith('.png')).length});
```

**Next:** Alex selects A or B before any kit modelling or scene changes.
