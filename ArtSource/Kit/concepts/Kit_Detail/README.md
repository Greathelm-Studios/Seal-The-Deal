# Office kit detail concepts

**Status** Target for Alex’s review

Compare the [contact sheet](contact_sheet.png), then open a family at full size. Each sheet pairs **A · Refined** and **B · Rich**, with a matched close view above a matched view from play distance.

The [approved direction](../Office/DECISION.md), [art bible](../Office/refs/art_bible.txt), [kit geometry](../../kit_pieces.py), [kit documentation](../../README.md) and [glass specification](../../../../docs/systems/GLASS.md) govern the proposals. The views prioritise the interior. The finish takes its cue from the supplied gallery and game interiors, with satin white shells, soft ice-blue fill, and cobalt used in broad fields. The window sheet shows the player-side interior and keeps the exterior to a skyline glimpse.

All measurements below are in **mm**. Values describe modelling targets and retain the existing envelopes, pivots and snap faces. Recesses and bevels stay on exposed faces and end before snap ends, floor contacts and soffit contacts. New projections stay within 15 mm, including hardware. **Every detail retains the existing collider.** The glass sheen uses the existing `Glass_Clear` material with the faint broad and narrow streaks.

## 01 · Window bay and columns

[Full-size sheet](01_Facade/sheet.png) · **6 image attempts**

**Existing dimensions** 4,000 bay width, 600 sill height, 200 sill depth, 160 high × 120 deep street-side tray pocket, 50 high × 100 deep rails, 20 pane. Regular and corner columns are 400 and 420 wide respectively, both 240 deep. The street-face slot is already recessed 160, with 20 side lands and 140 solid at the head.

| Level | Detail and size | Material |
| --- | --- | --- |
| A | 3 bevel on exposed sill and column arrises, including the slot lip | `Wall_PaperWhite` |
| A | 2 rail bevel and 2 recessed bedding line at the pane | `Trim_Navy` |
| B | Add 2 wide × 3 deep inboard sill-cap seam and 2 wide × 2 deep column-return seam | `Wall_PaperWhite`, shadow from the recess |
| B | Add 3 visible glazing gasket, flush inside the rail | `Gasket_Navy` (proposal) |
| B | Add 2 thick folded liner with a 10 return and two 18 × 3 drain slots per bay, entirely inside the existing street-side recess | `Trim_Navy` |

**Build A.** The sill bevel and thin rail give the window a finished edge from inside. The richer details mostly disappear from play distance.

## 02 · Walls and door

[Full-size sheet](02_Walls_Door/sheet.png) · **5 image attempts**

**Existing dimensions** 200 wall depth, 4,300 floor to soffit, 1,200 × 2,400 clear opening, 1,180 × 2,380 × 40 leaf, 10 leaf gap, 80 × 15 skirting reveal, 40 × 15 soffit reveal and 30 × 15 doorway reveal. The portal has two 4,000-wide cobalt leg bays around a 4,000-wide white door bay. The existing 1,000-high cobalt head band stays on the main-floor face only, with the existing 15 wide × 15 deep navy line beneath it.

| Level | Detail and size | Material |
| --- | --- | --- |
| A | 2 bevel on visible wall and reveal edges, 2 on leaf face arris | `Wall_PaperWhite`, `Trim_Navy`, `Door_Leaf` |
| A | 120 × 36 inset finger pull, 12 deep with rim flush | `Frame_Aluminium` |
| A | Keep portal legs and head as full-face fields and preserve the existing head reveal | `Accent_Cobalt`, `Trim_Navy` |
| B | Add 8 visible × 2 thick inner jamb liner, recessed inside the reveal and outside the clear opening | `Frame_Aluminium` |
| B | Add 145 × 54 × 2 flush pull backplate and two 75 × 10 hinge knuckles with up to 5 proud on the visible face | `Frame_Aluminium` |
| B | Add 1,060 × 160 × 2 brushed kick plate, seated flush above the floor gap | `Frame_Aluminium` |
| B | Add 150 × 65 × 2 STORE plaque on the wall beside the door | `Wall_PaperWhite` and `Trim_Navy` palette colours |

**Build B.** The pull, kick plate and sign make the service door legible as a used office fixture, with the portal still carrying the room-scale composition.

## 03 · Stair

[Full-size sheet](03_Stair/sheet.png) · **5 image attempts**

**Existing dimensions** 800 core radius, 2,600 tread radius, 2,630 handrail radius, 2,650 slab opening radius, 2,750 cage radius. Twenty-four 50 thick treads per turn, one 40 diameter helical rail, 50 thick cage glass in 30° panels. Locked door body 1,750 × 4,200 × 200, with the existing 1,200 × 2,400 inner-leaf outline, 15 wide × 10 deep reveal and 120 × 300 × 10 lock plate. The hidden ramp remains the walking surface.

| Level | Detail and size | Material |
| --- | --- | --- |
| A | 3 bevel along tread edges, 2 end radius at the existing rail terminus | `Stair_Aluminium` |
| A | 3 wide × 2 deep recessed seam at the column collar | `Wall_PaperWhite` |
| A | 8 high × 2 deep visual cage shoe above floor contact, inside the existing glass face, and 2 polished edge bevel at panel seams | `Frame_Aluminium`, `Glass_Clear` |
| A | 2 bevel on exposed red face and navy lock-plate edges | `Door_LockedRed`, `Trim_Navy` |
| B | Add 14 wide × 2 deep flush nosing insert, set 12 back from each tread edge, and two 8 diameter × 2 deep recessed fastener marks under each tread | `Tread_Inlay_Navy` (proposal), `Stair_Aluminium` |
| B | Add two 70 × 10 hinge knuckles with up to 5 proud and a 20 diameter × 2 deep inset in the existing lock plate | `Frame_Aluminium`, `Trim_Navy` |
| B | Add a 22 × 24 lock pictogram and 150 × 35 LOCKED marking, flush | `Wall_PaperWhite` palette colour |

**Build B.** The anti-slip inlay gives the open tread a readable edge and the lock pictogram gives the red barrier a clear point of interaction.

## 04 · Ceiling and floor

[Full-size sheet](04_Ceiling_Floor/sheet.png) · **4 image attempts**

**Existing dimensions** 500 × 500 ceiling and carpet tile grid, 200 slab edge, four 1,500 × 200 × 40 strip fixtures per 4,000 × 4,000 bay. The current fixture has a 20 housing lip and a 5 recessed diffuser.

| Level | Detail and size | Material |
| --- | --- | --- |
| A | 10 wide flush visual T-grid, with an acoustic face at the existing 500 module | `Ceiling_Tile`, `Frame_Aluminium` palette colour |
| A | 2 edge bevel on the white fixture lip and clean 5 recess at the opal diffuser | `Wall_PaperWhite`, `Light_Strip` |
| A | 1 wide tonal seam on the existing 500 × 500 carpet grid and a 3 bevel on the exposed slab-edge arris | `Floor_Carpet`, `Wall_PaperWhite` |
| B | Add a 2 wide shadow line alongside the visual grid, 1 wide recessed endcap seam and 3 pitch diffuser rib pattern inside the diffuser | `Ceiling_Tile`, `Wall_PaperWhite`, `Light_Strip` |
| B | Make the quarter turn on each 500 carpet tile subtly visible and add a 4 wide × 3 deep drip line beneath the exposed slab edge, clear of the soffit contact area | `Floor_Carpet`, `Wall_PaperWhite` |

**Build A.** The fluorescent rhythm and large carpet value fields carry the interior. The prismatic ribs and extra seams matter mostly at close range.

## 05 · Glass partition

[Full-size sheet](05_Partition/sheet.png) · **5 image attempts**

**Existing dimensions** 4,000 and 2,000 runs, 2,500 overall height, 100 track width, 80 track height at the base and head, 20 pane. The top track sits 1,800 below the soffit.

| Level | Detail and size | Material |
| --- | --- | --- |
| A | 2 track-face bevel, 1 wide visible line at the 45° track mitre and 1 polished pane-edge bevel | `Trim_Navy`, `Glass_Clear` |
| B | Add 3 visible gasket line and 6 visible × 2 deep stepped glazing-cap edge, inset on the existing track face | `Gasket_Navy` (proposal), `Trim_Navy` |
| B | Add one 6 diameter flush countersunk fixing on the base track face near the mitre, and 2 visible clear seal at the pane corner | `Frame_Aluminium`, `Seal_Clear` (proposal) |

**Build A.** The dark tracks and clear glass already describe the enclosure at play distance. A clean corner is the detail that earns its place.

## Modelling reference

Use the dimensions in this handoff and the source geometry to resolve visual scale in the paintings. The column is Paper White as in `stair_column()`; the kit README’s piece table lists aluminium. The ceiling layout follows the kit README’s four lights per bay; the `ceiling_light()` comment describes two. The approved office paintings show a short red gate, while the current locked door geometry specifies the full-height rectangular barrier used here.

The stair painting enlarges the warning graphic for legibility. Use the size in the table and the approved squared portal layout behind it. The partition painting shows a small fixing in both variants. For modelling, the fixing is B-only and belongs on the base track. The ceiling sheet keeps the rival slab edge out of frame and the table gives its proposed finish.

All five pairs were inspected visually for the A/B views, finishes and glass sightlines. ImageGen produced the paintings and their edges were cropped to exact 16:9. PowerShell and System.Drawing opened the six PNGs and confirmed 1,664 × 936 for each family sheet and 3,552 × 1,568 for the contact sheet.

Next, Alex picks A or B for each family before modelling.
