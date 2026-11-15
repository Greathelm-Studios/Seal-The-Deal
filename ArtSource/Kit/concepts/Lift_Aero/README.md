# AONAGI lift — Aero / Soft Club concepts

**Status:** Target · **Last updated:** 2026-11-15 · **Project:** Seal the Deal

The lift gains a softly rolled silver portal, cyan floor indicators and a pearl cabin with a frosted ceiling and mirror-silver back.
The glass middle storey and capped roof head-house use the same rounded silver framing and restrained aqua accents as the approved office props.
These pictures and dimensions are an art proposal for Alex's review before Blender modelling.

## Files and dimensional authority

| File | Contents |
|---|---|
| [sheet_1_landings.png](sheet_1_landings.png) | Closed 1F landing, part-open door state, RF variant, call panel and indicator details, front elevation |
| [sheet_2_car.png](sheet_2_car.png) | Car cutaway, operating panel, reverse view of the landing doors while docked, plan and section, rail detail |
| [sheet_3_shaft_headhouse.png](sheet_3_shaft_headhouse.png) | 2F glass shaft with opaque car visible, 1F opaque wall, RF head-house and cap, shaft plan and stack section |
| [contact_sheet.png](contact_sheet.png) | Three final sheets, labelled and stacked vertically at native pixel size |
| [generation_prompts.json](generation_prompts.json) | Exact prompts, four image tries, source output paths and final selection |
| [REQUEST.md](REQUEST.md) | Original brief, retained unchanged |

[REQUEST.md](REQUEST.md) governs the fixed sizes.
The current-pieces capture stamps a 2.90 m car height; the brief specifies **2.60 m clear inside** and a **3.70 × 3.55 m outside footprint** instead.
The proposed 60 mm floor and 60 mm ceiling shell below give a 2.72 m overall car height; this is an explicit concept construction choice, not a measured replacement for the captured 2.90 m shell.
Do not scale the car to 2.60 m overall or change its footprint to fit a rendering.
The approved older doors/lift prop sheet is a finish reference; this brief's working dimensions take precedence over that sheet's illustrations.

The PNGs are industrial-design illustrations, not measured orthographic drawings.
Use the dimensions and coordinates here rather than measuring rendered pixels, apparent cap overhang, glass reflections or the illustrated part-open gap.
Small generated lettering is placement reference; use the exact text master below for final labels.
No Unity assets, collision, authored transforms, controls, movement, network ownership or gameplay are changed by this concept delivery.

## Modelling conventions

- All feature sizes and coordinates below are **millimetres**; the fixed-envelope table also gives metres.
- Blender axes are **X = width, Y = depth into the shaft/car, Z = up**; the landing/front is negative Y.
- Coordinates are part centres unless stated as a range, surface or edge.
- `R` denotes radius; `Ø` denotes diameter; dimensions are W × D × H unless another plane is named.
- **G** = geometry; **T** = texture, decal, normal or roughness detail; **M** = material assignment to an existing face.
- Details below about **4 mm**, including grain, shut lines, screw marks, LED dots, arrows and lettering, are T rather than separate meshes or booleans.
- Broad labels remain T even when larger than 4 mm; typography does not need thickness.
- Bevel inward, and fit rolled profiles inside the stated outside envelopes.
- The clear 1400 × 2200 opening is a rectangular keep-clear box; no inner corner roll, trim, hood or sill may reduce it.
- Use primitives, bevelled extrusions and shallow insets with smooth/weighted normals; avoid subdivided sculpting and individually modelled LED dots.
- Keep the two door leaves separate; keep fixed surrounds, glass, car, cap and labels identifiable for the eventual model handoff.
- Reflections, ceiling glow and glass edge tint are materials/lighting, not additional decorative geometry.

### Fixed envelopes

| Piece | Kept envelope / motion |
|---|---|
| Shaft cell | 4000 × 4000 outside plan; side/rear wall build-up 100; 3800 × 3800 clear well before the deeper front landing wall |
| Storeys / stops | Finished landing heights 0 / 4500 / 9000 = 1F / 2F / RF; travel 9000 |
| Lift_Car | Outside plan 3700 × 3550; clear inside height 2600; front faces the landing |
| Lift_Door_Frame_4m_1F / _RF | 4000 × 240 × 4500; centred clear opening 1400 × 2200; existing separate LED label centred at Z2660 |
| Lift_Door_Panel | Exactly two leaves, each 750 × 40 × 2250; closed at centre; each translates 750 sideways |
| Lift_Shaft_Wall_4m | 4000 × 100 × 4500 side/rear wall module; opaque on 1F, glass on 2F |
| RF head-house | 4000 × 4000 outside plan, 4500 tall including cap, starting at roof Z9000; front reuses RF landing dimensions |

### Coordinate frames and assembly check

The shaft assembly origin is the centre of its 4000-square footprint at the 1F finished landing.
Outer shaft faces are X/Y = ±2000; side/rear inside faces are X/Y = ±1900.
The front landing wall spans assembly Y=-2000..-1760.
Its local origin is `(0,-2000,landing height)`, with local Y=0 at its front face and Y=240 at its back.
The side/rear wall tables use a separate module origin at the bottom/front centre of a 4000-wide wall: X±2000, Y0..100, Z0..4500.

The car origin is the centre of its open front edge at its finished floor: local X±1850, Y0..3550, Z0 at the rider's floor.
For the illustrated assembly its front origin is at shaft Y=-1750, so its back is Y1800.
That gives 50 mm each side to the 3800-wide well, 100 mm at the rear, and 10 mm from the front wall's back plane to the car's front edge.
The door-leaf centre plane is front-wall local Y170, or shaft Y=-1830; leaf back face Y=-1810 is 60 mm ahead of the car front.
These are visual layout coordinates for the concept; preserve existing production origins, pivots and collision when the approved visual model is eventually fitted.

### Shared materials

| ID | Palette / starting finish | Uses |
|---|---|---|
| P | Pearl `#F4F8F8`, nonmetallic, roughness 0.20–0.28 | Moulded cladding, rounded shells, button centres, cap |
| S | Silver `#C8D2D9`, metallic, roughness 0.28–0.38 | Hairline-brushed leaves, portal, frames, panel plates, kick strips |
| C | Silver palette, metallic, roughness 0.10–0.16 | Mirror-silver rear panel and restricted rail highlights |
| A | Aqua `#45BFC7`, nonmetallic | Cyan control rings, indicator text and small printed accents |
| F | Frosted aqua `#C6E9E6` / `#8FDCE0`, roughness about 0.28 | Lower glazing ribbon and restrained acrylic edge tint |
| D | Frosted-milk P, softly transmissive with restrained white emission | Large ceiling diffuser; do not turn it into bright cyan strip lighting |
| V | Nearly neutral clear glass, subtle F edge tint, roughness 0.05–0.10 | 2F shaft glazing; car remains legible through it |
| M | Mist `#A8BFD1`, satin nonmetallic, roughness 0.40–0.50 | Pale resin car floor |
| N | Navy `#111B38`, gloss on glass, matte in recesses | Smoked indicator, seals and roof shadow reveal |
| B | Cobalt `#244FB9`, optional tiny print only | Small asset-code detail, not a large colour field |
| H | Hanko red `#C7473E` | AONAGI logo dot only |

Hairline grain, fine joints and seals belong in normal/roughness/colour maps.
Use broad soft reflections on the back panel, not a painted image of a room or transparent cabin wall.
The clear shaft panes and frosted lower ribbon are separate material regions on the same glass; the ribbon is not a second thick pane.

## Sheet 1 — landings

### Lift_Door_Frame_4m_1F

**Keep:** 4000 × 240 × 4500; opening X=-700..700, Z=0..2200 through the full wall; LED label centre `(0, front, 2660)`.
Positions in this section use the front-wall local frame described above.

| Feature | Size and position | Material / method |
|---|---|---|
| Pearl wall cladding | Entire 4000 × 240 × 4500 wall around the keep-clear opening; exposed outer edges R20, all rounding inward | P, G; retain the doorway cutout and full-height broad shoulders |
| Rolled silver portal | 80-wide visible band around both sides and head; outside width 1560, outside top Z2280, side outer edges X±780; Y6..46 profile depth 40; outside upper corners R60; small exposed edge roll R8 directed away from clear opening | S, G from a U-profile extrusion; keep inner clear rectangle 1400 ×2200 |
| Threshold | 1400 ×160 surface region, X±700, Y60..220, at Z0; no raised step | S, M/T on existing floor/sill face; 2 mm track lines T |
| Indicator enclosure | 720 ×24 ×180 capsule, X0, Y4..28, centre Z2660; face outline R90; edge bevel R6 | S, G; does not change existing separate LED-label location |
| Smoked indicator face | 680 ×140 face, centre X0/Z2660, front plane Y10, outline R70; set inside enclosure | N, G surface/M; no physical pixels |
| Active indicator field | 480 ×90, centred X0/Z2660 on smoked face; nominal 3 mm cyan dots, floor code and direction arrow | A, T/emissive label; existing text remains a separate label, not baked into portal mesh |
| 1F call plate | 90 ×12 ×230 capsule, centre X=-980/Z1120, Y4..16; R20 face corners, R4 edge roll | S, G recessed into wall; no approach-side protrusion beyond Y0 |
| Call buttons | Two Ø32 ×6 discs, centres X=-980, Z1160 and 1080; outward faces Y2, backs Y8 | P/S, G; button pockets stay outside doorway |
| Call rings and arrows | Ring outsideØ44, stroke 2, on plate around each button; upper up arrow, lower down arrow, nominal glyphheight 12 | A, T/emissive print; no separate glowing torus |
| Wall seams | 2 mm vertical lines at X±1250, Z75..4425, on pearl face | N low contrast, T |
| Base strip | 35 high, Z0..35 on solid wall shoulders only; silver finish inset into lower face, does not cross the doorway | S, M; 2 mm upper join T |
| Small company mark | 250 ×70 decal region centred X=-1450/Z3700, front plane Y0; Ø60 ring at region's left, wordmarkheight 18 | A/H/N, T; no wall-size brand sign |

**Aero read:** a softly rolled metal portal and smoked cyan display make the doors a recognisable precision product against broad pillowed pearl shoulders.

### Lift_Door_Frame_4m_RF

**Keep:** the same 4000 × 240 × 4500 wall, 1400 ×2200 clear opening, LED centre Z2660 and matching door leaves.
Reuse the 1F features and coordinates except the following changes; cap integration is specified under the head-house.

| Feature | Size and position | Material / method |
|---|---|---|
| RF floor label | Same 480 ×90 active field at Z2660, exact code `RF`; down direction arrow only where needed | A, T/separate label |
| Roof call control | Same 90 ×12 ×230 call plate at X=-980/Z1120, but one Ø32 disc and Ø44 ring centred Z1120; arrow points down | S/P G; A arrow/ring T; do not retain the unused up-button disc |
| Recessed portal / weather lip | Portal shifts rearward to Y24..64 without changing X/Z opening; silver hood 1640 wide ×70 deep ×40 high, X0, Y0..70, Z2280..2320, exposed edge R10 | S, G; hood stays inside 240 wall depth, above the clear opening and inside the fixed wall width |
| Fine drip line | 2 mm line across hood underside near front Y8 | N, T/normal; no extra weather-gutter mechanism |

**Aero read:** the same cyan RF capsule and rounded silver entry remain familiar, with a shallow weather lip rather than an unrelated industrial roof door.

### Lift_Door_Panel — pair

**Keep:** two 750 ×40 ×2250 leaves with existing pivots and 750 horizontal travel per leaf.
Use leaf-local X±375, Y±20, Z0..2250 for the model; assembly closed centres are X=-375 and 375, front-wall local Y170.
Closed extents are X=-750..0 and 0..750: each overlaps its jamb by50, and the top 50 sits above the 2200 clear opening.
Fully open centres are X=-1125 and 1125; inner edges are atX±750, 50 past the clear aperture's X±700 sides.
For the illustrated half-travel state, centres are X±750 after 375 movement each, leaving a750-wide central gap.

| Feature | Size and position | Material / method |
|---|---|---|
| Finished leaf shell | Entire 750 ×40 ×2250 envelope; outer exposed edge bevel R4 contained inside these bounds; broad flat faces | S, G; use soft reflected highlights rather than swelling either face beyond 40 thickness |
| Subtle face inset | 12-wide perimeter material band inside each broad face; nominal 1 mm hairline boundary | S slightly varied roughness, M/T; do not split leaf into multiple moving parts |
| Centre meeting accent | 1 mm strip on each meeting edge of front and rear faces, Z0..2200; combined closed line 2 mm | A, T; no floor-length neon beam, seal mesh or raised strip |
| Lower kick finish | Bottom 100 of each leaf, full 750 width, on both faces | S alternate roughness, M/T; continuous face, no extra thickness |
| Hairline brush | Fine vertical grain over both faces, submillimetre normal/roughness | S, T; no modelled ribs |

The car-door inset on sheet 2 shows the **reverse faces of this same landing pair while docked**.
It is not a second car-door pair or a new drive mechanism.
The narrow jambs and frame hide the leaves' overlap; do not resize leaves to the clear aperture.

**Aero read:** softly finished silver faces, fine grain and a single aqua meeting line feel like early-2000s precision hardware without changing the sliding silhouette.

## Sheet 2 — Lift_Car

**Keep:** outside plan 3700 ×3550; 2600 clear inside height above the finished floor; open front at local Y0.
Proposed shell construction: floorZ=-60..0; cabin wallsZ 0..2600; ceilingZ 2600..2660, giving 2720 overall height.
This construction leaves 3500 clear width between side shells and 3450 depth from open front to rear lining before small rails/control projections.

| Feature | Size and position | Material / method |
|---|---|---|
| Floor shell | 3700 ×3550 ×60, X±1850/Y0..3550/Z=-60..0; front outer edge R6 below finished floor | P/S, G; rider-facing floor stays flat Z0 |
| Side shells | Two 100-thick sides at X=-1850..-1750 and 1750..1850, Y0..3550, Z0..2600; outside front vertical edge R40, cabin-facing corners R20 | P, G; no extra front wall or door cheeks across the open face |
| Rear shell | 3500 wide ×100 deep ×2600 high, X±1750/Y3450..3550/Z0..2600, joined to side shells | P, G; solid opaque back, not glass |
| Mirror-silver back panel | 3400 wide ×2350 high, centreX 0/Z1305, spansZ 130..2480; face atY 3450, lining recessed up to8 into rear shell; outline R30 | C, G face/inset; clean silver mirror response with restrained blur |
| Mirror perimeter | 15-wide silver band inside the panel's stated 3400 ×2350 outline; edge bevel R6; fine 2 mm panel joint | S, G/material border; joint T |
| Ceiling shell | 3700 ×3550 ×60, X±1850/Y0..3550/Z2600..2660, front exposed edge R20 | P/S, G; do not lower its underside below 2600 |
| Ceiling diffuser | 2900 ×2750 ×12 plate, centredX 0/Y1725, undersideZ 2600/top 2612, plan cornersR 80 | D, G; flush underside maintains 2600 clear height |
| Diffuser surround | 20-wide band around diffuser, outer 2940 ×2790, samecentre, outlineR 100; set within ceiling with undersideZ 2600 | S, G/M; 2 mm seam T, no dropped lamp housing |
| Pale resin floor | Visible 3500 ×3450 area, X±1750/Y0..3450, surfaceZ 0 | M, M/T on floor shell; very fine satin grain, no large tiles or raised tread |
| Floor perimeter / entry sill | 20-wide silver material band inside visible floor perimeter; front band spansX±1750/Y0..20 | S, M/T; zero added height and no new lip |
| Wall kick strips | 120 high, Z0..120, full inner side/rear wall lengths; face aligned with lining | S, M/T; 2 mm top seam T |
| Rounded handrail | Ø40 centreline at Z950; side runs X±1690, Y1150..3300, back runY 3390/X=-1600..1600; R90 centreline corner bends join each side to back | S/C, G low-sided swept tube; 40 clearance from tube surface to flat wall |
| Rail end returns | At front of each side run, centreline bends R40 toward wall, finishing atX±1750 andY 1110; no open cut tube end | S, G; keep all geometry within cabin footprint |
| Rail mounts | Ø18 stand-offs, nominal 60 centreline-to-wall, trimmed into rail surface; back atX±1100/Y3450, sides atY 1600/2700 andX±1750, allZ 950 | S, G; Ø35 wall pads 4 thick inset into wall; screwheads 2 mm T |
| Car operating plate | 240 wide alongY ×15 thick alongX ×750 high; Y330..570, X1735..1750, Z800..1550; face cornersR 25 and edgeR 4 | S, G, on right wall near entry; own front faceX 1735 |
| Smoked car display | 150 wide alongY ×50 high, centreY 450/Z1485, recessed 4 into plate face | N, G face; cyan`1F`/`2F`/`RF` label T |
| Three floor buttons | Exactly three Ø36 discs with 4 mm projection, centresY 405 atZ 1380/1270/1160; outward facesX 1731; cyan ringsØ44 with 2 mm stroke | P/S, G buttons; A rings T; labels`1F`/`2F`/`RF` atY 490 on corresponding row |
| Two door-control buttons | TwoØ32 discs, centresY 395 and 505, Z1010, same 4 mm projection; spreading-arrow symbol on first, converging-arrow on second | S, G buttons; N symbols T |
| Control legends | `開 / OPEN` atY 395/Z965 and`閉 / CLOSE` atY 505/Z965 on plate face, nominal 14 high; floor legends 18 high | N, T; first is left and second right in the panel's front-view detail |
| Small panel mark | Ø40 ring centredY 450/Z870 with 8-high AONAGI beneath atZ 830, on plate face | A/H/N, T |
| Cabin joints | 2 mm rear/side lining joins and ceiling perimeter seam | N low contrast, T; no modelled screw pattern or extra access hatches |

The rail ends before the operating panel; do not run it through the button plate.
The large front-open hero is the complete cabin shell; the docked reverse view also includes the landing wall ahead of it.
No bench, plant, handrail lighting, secondary door pair or hoisting equipment is part of this proposal.

**Aero read:** pearl radiused shells, a broad frosted ceiling, mirror-silver back and restrained round cyan controls replace a bare box with a calm 2003 corporate interior.

## Sheet 3 — shaft and head-house

### Lift_Shaft_Wall_4m — 1F opaque variant

**Keep:** 4000 ×100 ×4500; use the wall-module coordinate frame, not the 240-deep landing frame.
This is a side/rear wall without an opening; the door wall is a separate piece.

| Feature | Size and position | Material / method |
|---|---|---|
| Pearl cladding | 4000 ×100 ×4500 outline, exposed edgesR 20 inward; broad uninterrupted face | P, G; retain 100 total wall build-up |
| Silver edge stiles | End 100-wide regions X=-2000..-1900 and 1900..2000, full moduleheight; same 100 depth, edgeR 20 | S, G/M, shared with corner posts in assembly; do not double posts at wall joins |
| Lower/upper trims | 75-high regions Z0..75 and 4425..4500, spanning between corner postsX±1900, full 100 depth | S, G/M within the wall envelope |
| Fine panel joints | 2 mm vertical lines atX±650, Z75..4425 | N low contrast, T; no raised ribs |
| Inset base seam | 2 mm dark line alongZ 75 on the cladding face | N, T; no extra projecting plinth |

**Aero read:** soft pearl panels framed by precise rolled silver ends and narrow trims match the furniture's finished shell-and-rim construction.

### Lift_Shaft_Wall_4m — 2F glass variant

**Keep:** the same 4000 ×100 ×4500 module and overall 4000-square shaft cell.
Glazing fills side/rear walls on2F; the doorway/working landing keeps its fixed opening and leaves.
The sheet's back/side view avoids proposing a new transparent front door.

| Feature | Size and position | Material / method |
|---|---|---|
| Shared corner posts | 100 ×100 square plan, full 4500 height, exposed cornerR 20; assembly centresX/Y=±1950 | S, G; one post per shaft corner, not one overlapping post per module |
| Upper/lower rails | 3800 ×100 ×75 between end posts, centresZ 37.5 and 4462.5; edgeR 12 | S, G; outer bounds remain 4000-square |
| Centre mullion | 30 ×100 ×4350, X0/Y0..100/Z75..4425, exposed edgeR 6 | S, G; one per glass wall |
| Two glass panes | Each 1885 wide ×12 thick ×4350 high; X=-1900..-15 and 15..1900, Y44..56, Z75..4425; edges seated under frame lips | V material, G pane geometry; fine edge bevel 2 mm is normal/shading detail T |
| Lower frosted ribbon | 100 high, Z75..175 across both panes, following their visible width | F, T/material region on glass, not a second pane |
| Glazing seals | 3 mm navy line around pane/frame meeting edge | N, T; no separate gasket mesh |
| Silver exterior car skin | Existing opaque car side/rear faces receive the S finish; no extra outer jacket thickness | S, M/T; shell dimensions from sheet 2 remain 3700 ×3550 |

The car's metal box is visible **through shaft glass**, not through its own back wall.
Keep tint subtle enough to recognise its opaque silver shell; the rendered aqua reflections are not an aqua solid wall or additional diagonal bracing.
No rails, cables, motors or per-floor machinery have been invented for this surface concept.

**Aero read:** slim rounded silver framing, pale clear glazing and a single frosted lower ribbon turn the shaft into the architectural counterpart of the aqua-acrylic office props.

### RF roof head-house and cap / canopy

**Keep:** 4000 ×4000 outside plan; total 4500 high above RF, including cap; top is globalZ 13500.
The front uses the RF landing's4000 ×240 wall,1400 ×2200 opening and indicatorcentre 2660 above the roof.
Positions below are local to the roof atZ 0.
This is the shaft enclosure above the roof, not a passenger stop aboveRF.

| Feature | Size and position | Material / method |
|---|---|---|
| Head-house body | Pearl side/rear cladding toZ 4330 inside 4000-square plan,100 wall build-up; front retains 240 depth; exposed outer vertical cornersR 40 inward | P, G; reuse wall/landing visual language and door keep-clear box |
| Low silver trim | 75 high atZ 0..75, within cladding bounds; same silver side stiles and fine 2 mm seams as opaque wall where visible | S, G/M; fine seams T |
| Shadow reveal | Closed dark inset band,3960 ×3960 plan, Z4330..4360; outer faces 20 back from shaft exterior | N, G/material band; no open mechanical slot |
| Rounded cap / canopy | 4000 ×4000 ×140, Z4360..4500, outer plan cornersR 40, exposed rim edgeR 20; softly crowned top up to8 within the 140 height | P, G; highest point 4500, no overhang beyond 4000-square |
| Cap silver underside rim | 20-high material region Z4360..4380 inside cap outline; fine 2 mm upper join | S, M/T; no added external apron |
| Roof entry and weather lip | Reuse the RF portal, centred aperture, two leaves, single down-button plate and hood 1640 ×70 ×40 atZ 2280..2320 | S/P G; indicator/ring/legends T; no additional door or widened opening |

The cap replaces the upper 170 mm of the body:30 mm reveal plus 140 mm cap.
Do not stack it above an already 4500-tall wall or extend it outside the shaft footprint.
The matching front landing wall's upper region must be resolved as the same cap/reveal, not duplicate overlapping caps.
The small roof patch, ambient office and distant roof rail visible in sheet 3 are context only, not additional assets to model.

**Aero read:** a pearl rounded cap floating above a narrow dark reveal gives the RF enclosure the same soft product silhouette as the approved reception counter, with the familiar silver portal below.

## Exact logo and label master

The only brand is fictional **AONAGI / 青凪総合サービス**.
At nominalØ100, the mark is an aqua ring with 12 mm stroke, a55° lower-right gap and aØ16 red dot centred in that gap; scale the complete mark uniformly.
Keep the dot at lower right; do not mirror it or use red for control indicators.
Use a clean sans-serif and flat print/decal lettering.

| Application | Exact label / arrangement |
|---|---|
| Landing display | `1F`, `2F` or`RF`; cyan dot-matrix floor code and up/down arrow as appropriate |
| 1F call control | Up arrow above down arrow; optional`呼出 / CALL` in a55 ×12 print region below plate atZ 980 |
| RF call control | Single down arrow; optional`屋上 / ROOF` in the same 55 ×12 region |
| Car floor buttons | Exactly`1F`, `2F`, `RF`, top to bottom |
| Left door control in panel detail | Spreading arrows, then`開 / OPEN` |
| Right door control in panel detail | Converging arrows, then`閉 / CLOSE` |
| Small company marks | Open aqua/red-dot ring with`AONAGI` only |

The generated large operating-panel Japanese labels were corrected in try 4; these exact UTF-8 strings remain the production lettering master.
Do not reproduce editorial sheet headings, dimension arrows, material callouts or the catalogue date on the actual model.

## Generation provenance and review

Used the **built-in image_gen tool**, with three new-design calls and one targeted correction: **4 image tries** total.
The six user attachments were supplied as context in their given order: two current-lift views, three approved prop sheets and the approved room look.
The first three calls generated new product designs from text and that context; they did not paint over the blockout captures.
Try 4 edited only the car sheet's large Japanese open/close labels using try 2 as its image target.
Exact prompts and selected source paths are in [generation_prompts.json](generation_prompts.json).

| Try | Purpose | Selected output |
|---|---|---|
| 1 | Original landing sheet | `sheet_1_landings.png` |
| 2 | Original car sheet | Superseded by try 4 because the large panel's Japanese open/close labels were reversed |
| 3 | Original shaft/head-house sheet | `sheet_3_shaft_headhouse.png` |
| 4 | Correct Japanese labels on try 2 | `sheet_2_car.png` |

The three final sheets are native **1672 ×941** PNGs, approximately 16:9; no upscaling or cropping is applied.
The contact sheet is **1768 ×3221** and places the three sheets vertically at 1:1 pixel scale, with a separate caption above each.
It was assembled with PowerShell/.NET `System.Drawing`, without changing the generated product artwork.
All target positions and dimensions here are concept specifications for approval; generated highlights and perspective do not override them.

### Validation record

All four generated candidates were visually reviewed for requested contents, palette, fixed size labels and moving-part count.
The car control-label reversal was identified and corrected in try 4.
Opened the saved contact sheet and checked that all three panels, captions and edges are visible without cropping.
Loaded every final PNG with `System.Drawing.Image.FromFile`: all three sheets decoded as1672 ×941; the contact sheet decoded as1768 ×3221.
Compared each selected source output and project copy with `Get-FileHash -Algorithm SHA256`: all three matched.
Parsed `generation_prompts.json` with `ConvertFrom-Json`:4 tries,3 selected outputs,1 targeted correction.
Checked every relative Markdown link in this guide with `Test-Path`:8 references resolved.
Reviewed the specified coordinates against the fixed envelopes, including the leaf overlap/travel, shell clear height, glass build-up and cap height.
Validation covers the images and handoff files; this concept delivery makes no claim about runtime behaviour.

Repeat the file inspection from the repository root with PowerShell:

```powershell
Add-Type -AssemblyName System.Drawing
$liftDir = 'ArtSource/Kit/concepts/Lift_Aero'
$liftNames = 'sheet_1_landings.png','sheet_2_car.png',
    'sheet_3_shaft_headhouse.png','contact_sheet.png'
foreach ($liftName in $liftNames) {
    $liftImage = [System.Drawing.Image]::FromFile(
        (Resolve-Path -LiteralPath (Join-Path $liftDir $liftName)).Path)
    "$liftName : $($liftImage.Width) x $($liftImage.Height)"
    $liftImage.Dispose()
}
$liftPrompts = Get-Content -Raw -Encoding UTF8 -LiteralPath (
    Join-Path $liftDir 'generation_prompts.json') | ConvertFrom-Json
"Tries: $($liftPrompts.tries.Count)"
"Selected: $(@($liftPrompts.tries | Where-Object selected).Count)"
$liftReadme = Get-Content -Raw -Encoding UTF8 -LiteralPath (
    Join-Path $liftDir 'README.md')
$liftLinks = [regex]::Matches($liftReadme, '\]\(([^)]+)\)')
foreach ($liftLink in $liftLinks) {
    if (-not (Test-Path -LiteralPath (
        Join-Path $liftDir $liftLink.Groups[1].Value))) {
        throw "Missing link: $($liftLink.Groups[1].Value)"
    }
}
foreach ($liftTry in ($liftPrompts.tries | Where-Object selected)) {
    if (Test-Path -LiteralPath $liftTry.source_output) {
        $liftA = (Get-FileHash -Algorithm SHA256 -LiteralPath (
            Join-Path $liftDir $liftTry.file)).Hash
        $liftB = (Get-FileHash -Algorithm SHA256 -LiteralPath (
            $liftTry.source_output)).Hash
        "$($liftTry.file) selected source matches: $($liftA -eq $liftB)"
    }
}
```

Next: Alex reviews the three sheets before Blender remodelling begins.
