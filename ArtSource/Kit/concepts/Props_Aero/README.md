# AONAGI office props — Aero / Soft Club concepts

**Status:** Target · **Last updated:** 2026-11-15 · **Project:** Seal the Deal

These are new product-design concepts for the pearl-white, aqua and silver office in the approved room images.
The furniture gains soft silhouettes, translucent inserts and recognisable 2002 equipment details while retaining the envelopes in [REQUEST.md](REQUEST.md).
The pictures are a visual proposal for Alex's review, not approved replacement meshes.

## Files and authority

| File | Contents |
|---|---|
| [sheet_1_workstation.png](sheet_1_workstation.png) | Back-to-back bench, flat monitor, keyboard, corded phone, two-tier tray, task chair and pod chair |
| [sheet_2_desks_storage.png](sheet_2_desks_storage.png) | Sculpt desk, reception, low storage and copier |
| [sheet_3_amenities.png](sheet_3_amenities.png) | Water server, umbrella stand, vending machine, slipper cabinet and planter |
| [sheet_4_light_column.png](sheet_4_light_column.png) | Linear pendant and rounded-square column |
| [contact_sheet.png](contact_sheet.png) | Labelled 2 × 2 overview of these four sheets |
| [generation_prompts_sheets_1_4.json](generation_prompts_sheets_1_4.json) | Exact built-in image-generation prompts, including correction prompts |

Each sheet includes a three-quarter rendering and a small front or side view for each prop, its kept dimensions and material/detail notes.
The images are industrial-design illustrations, not measured orthographic drawings: build the sizes and coordinates below rather than measuring pixels.
Several supplied before-sheet cards contain unrelated objects or disagree with their headings; the request's size table is the envelope authority.
Small generated Japanese lettering is reference placement only; use the exact UTF-8 text specified here when making labels.

## Modelling conventions

- All dimensions below are **millimetres** unless an envelope is explicitly also given in metres.
- Blender axes: **X = width, Y = depth, Z = up**; front is negative Y.
- Origin is at the centre of the ground-contact footprint, Z = 0; for loose desk equipment it is the underside resting on the desk.
- Positions are part centres `(X, Y, Z)` unless a span, face, edge or local offset is stated.
- Work from the outer envelope inward: a bevel, raised cap or inset trim must not increase the required width, depth or height.
- `R` is a corner/edge radius, `Ø` a diameter; plan radii and edge bevels are distinguished where needed.
- **G** means geometry; **T** means texture/decal or baked normal/roughness detail.
- Fine shut lines, screw heads, key legends, grille ribs and print below about **4 mm** are T, not booleans or separate meshes.
- Visible key tops can be baked from a temporary modelling source; the game asset does not need a mesh per key.
- Keep loose equipment separate from the bench; dressing positions below are a concept arrangement, not changes to an authored scene.
- Use bevelled cubes, cylinders, simple extrusions, an occasional shallow boolean and weighted/smoothed normals; avoid subdivision-dependent sculpting.
- All labels are flat print or decals; no extruded letters, flowing water geometry, bubbles or animated decorative effects.
- The geometry here is the visual mesh specification; this concept pass does not alter collision, mass, authority, interaction or generated kit assets.

### Shared materials

| ID | Palette / suggested surface | Use |
|---|---|---|
| P | Pearl `#F4F8F8`; nonmetallic, roughness about 0.20–0.28 | Rounded plastic shells, cladding, pale keys |
| S | Silver `#C8D2D9`; metallic, roughness about 0.28–0.38 | Brushed trim, frames, bases and control hardware |
| C | Same silver palette; metallic, roughness about 0.12–0.18 | Restricted chrome stems and handle highlights |
| A | Aqua `#45BFC7`; nonmetallic, roughness about 0.20–0.30 | Small solid controls, shrouds, printed bands |
| F | Frosted aqua `#C6E9E6`; softly transmissive acrylic, roughness about 0.28 | Dividers, tray baskets, windows in furniture |
| M | Mist `#A8BFD1`; nonmetallic | Slippers and secondary pale plastic |
| N | Navy `#111B38`; roughness about 0.70 on fabric, about 0.16 on smoked screens | Upholstery, rubber, cords, smoked glazing and dark recesses |
| B | Cobalt `#244FB9`; printed accent only | Optional tiny asset-code detail; not a second large colour field |
| L | Leaves `#70AF59`; matte | Planter leaves and stems only |
| H | Hanko red `#C7473E` | AONAGI logo dot only |

Frosted-milk diffuser material uses P with soft transmission rather than adding a new colour.
Keep brushed grain and fabric weave in roughness/normal maps, not geometry.
Do not add visible components behind frosted panels: a subdued dark interior plane is sufficient where a cavity is needed.

### Exact logo and label master

The fictional AONAGI mark is an aqua open ring with a lower-right gap and a red hanko dot in that gap.
For a nominal Ø100 mark, use a 12 mm ring stroke, omit a 55° arc centred toward lower right, and place a Ø16 red dot in the opening; scale the complete mark uniformly for other sizes.
Use navy text, a plain sans-serif and no real manufacturer names or model numbers.
Do not copy render highlights or shadows into the label art.

| Application | Exact text |
|---|---|
| Reception name | `青凪総合サービス` above `AONAGI` |
| Reception end plaque | `受付 / RECEPTION` |
| Copier panel | `複写 / COPY` |
| Water server | `給水 / WATER` |
| Vending header | `自動販売機 / DRINKS` |
| Fictional drink packs | `NAGI`, `MIZU`, `AO` |
| Slipper cabinet | `室内履き / SLIPPERS` |
| Column floor example | `2F` above `審査部` above `REVIEW` |
| Small asset tag | AONAGI ring plus `AONAGI`; optional tiny navy asset number |

## Sheet 1 — workstation and loose equipment

### Workstation_Bench

**Keep:** 2.40 × 1.60 m back-to-back footprint; desktop surface Z = 740; divider top Z = **1150**; exactly two pedestals, each 400 × 550 × 600.

| Feature | Size and position | Material / method |
|---|---|---|
| Paired worktop | Two 2400 × 800 × 32 tops, centres `(0, -400, 724)` and `(0, 400, 724)`; combined plan 2400 × 1600; outer plan corners R40; exposed edge bevel R8; crown rises 2 within the 32 thickness, highest surface Z740 | P, G; avoid doubling an edge along the shared centreline |
| Perimeter rim | 8 high × 4 deep strip on the exposed worktop perimeter, Z712–720; inset from the outer silhouette | S, G; 1 mm joint line T |
| End supports | Two softened slab supports 40 × 1440 × 700 at `(±1180, 0, 358)`; lower edge Z8, upper Z708; vertical edge R15 | P, G; N rubber strips 30 × 600 × 8 at each lower end, Z0–8, within the slab footprint |
| Pedestals | 400 × 550 × 600; centres `(-900, -480, 300)` and `(900, 480, 300)` so opposite users each have one; plan R20, edge bevel R8 | P, G; fronts face their respective user, negative Y for the first and positive Y for the second |
| Three drawer fronts per pedestal | Width368; upper and middle heights170, lower height190; front spans Z420–590, 245–415 and 50–240; 5 mm separations; fronts inset 4 from the pedestal face | P, G for broad fronts; 2 mm fine shut lines T |
| Drawer pulls | Each 120 × 18 capsule recess, 6 deep; centred on each drawer width, 30 below its upper edge; silver insert 110 × 10 inside each recess | S, G; no protrusion outside the pedestal |
| Divider | Frosted plate 2160 × 18 × 410, centred `(0, 0, 945)`, bottom740/top1150; top corners R40 in the XZ profile | F, G; preserve full cover height at the middle, not a tall desk back extending above1150 |
| Divider frame | Silver U-frame 20 wide around top/ends, 26 total depth; frame and plate remain inside X±1100 and Z740–1150 | S, G; no tall corner posts above1150 |
| Cable grommets | Two Ø60 × 6 inset discs, centres `(-850, -680, 737)` and `(850, 680, 737)`; tops flush at740; Ø40 dark centre | S/N, G; cable cut seam below4 mm T |
| Asset tag | 120 × 35 decal on outer left support at X=-1200, Y=-300, Z550, facing outward; Ø25 logo and navy wordmark | A/H/N, T |

**Dressing:** seat-facing side A uses a monitor base at `(0,-150,740)`, keyboard at `(0,-570,740)`, phone at `(-500,-550,740)` and tray at `(850,-500,740)`.
Duplicate the four loose props on side B by rotating the whole arrangement 180° around Z, not by mirroring text or controls.
The two monitors face opposite users; the illustration's little front view is a layout aid, not permission to put both screens on the same side.

**Aero read:** the broad silver-rimmed pearl crown and luminous frosted centre screen make a finished shared workstation before any small equipment is added.

### Monitor_Flat

**Keep:** panel 550 W × 340 H; on a foot; overall height about460.
**Chosen depth:** 200 overall, including stand; panel depth50.

| Feature | Size and position | Material / method |
|---|---|---|
| Panel shell | 550 × 50 × 340, centre `(0,20,290)`, bottom120/top460; front Y=-5; plan/profile corners R10, edge bevel R5 | P, G |
| Screen | 500 × 286 rounded inset, centre `(0,-1,300)`, R6; 4 mm recessed from front bezel surface Y=-5; use a dark backing, not a see-through cavity | N smoked gloss, G surface; reflection streak T/lighting, no bright desktop UI |
| Bezel | Side borders25, top17, bottom37; all contained in the panel; no borderless modern display | P, G |
| Status insert | 180 × 8 capsule, centred `(0,-5,137)` in the lower bezel; 2 mm inset represented in normal map | A, T for the shallow insert; Ø4 cyan LED at X225 on same lower bezel, T |
| Stem | Ø45 × 100, Z20–120, centreline Y35; rounded collar Ø65 × 12 at Z108–120 | S/C, G |
| Foot | Oval 230 × 200 × 20, centre `(0,0,10)`, edge bevel R8; bottom N pad 190 × 155 × 4 inside that height | S/N, G |
| Back detail | Vent bank 300 × 45 on rear at Z330; 12 × 2 rounded slots at 6 pitch, and a 30 × 15 cable exit near stem | N vent pattern T; exit as T or a single shallow inset G |

**Aero read:** a substantial pearl bezel, aqua status strip and oval metal foot place this in the first office-LCD era.

### Keyboard

**Keep:** 440 × 140; permitted total thickness15–30.
**Chosen envelope:** 440 × 140 × 24.

| Feature | Size and position | Material / method |
|---|---|---|
| Wedge shell | 440 × 140 plan, R10; front edge Z0–15, rear edge Z0–24; edge bevel R5 | P, G; simple bevelled wedge |
| Key well | 416 × 112 rounded recess, centre `(0,0)` in plan; top follows wedge, 3 deep | N shadow edge/P interior, G for one recess |
| Key field | Alphanumeric field300 × 100 centred X=-48, number pad90 × 100 at X160; nominal keycaps17 × 17 with 2 mm gaps; legends2–3 high | P, T normal/colour key grid, including shadows and legends; no individual game-mesh keys |
| Aqua function row | A group of six17 × 12 keys across X=-145 to-35 on rear row Y45; normal height matches other keys | A, T |
| Lower trim | 4 high silver seam band around the lower shell at Z4–8, inset1; no wider than440 | S, G or material strip; 1 mm shut line T |
| Feet and cord | Two 35 × 12 × 4 rubber pads underneath near X±150, Y45, contained within total24 height; cable exits rear at `(0,70,10)` | N, G pads; rear socket T; do not add an unbounded trailing cable to this envelope |

**Aero read:** the soft low wedge and restrained aqua keys carry the period through silhouette and colour rather than exposed electronics.

### Phone_Desk

**Keep:** 240 × 220 × 100, including handset and a compact resting cord.

| Feature | Size and position | Material / method |
|---|---|---|
| Base wedge | 218 × 220, centre X11, X=-98..120 and Y=-110..110; front top Z28, rear top Z65; R15 plan corners and R6 edge bevel | P, G |
| Handset | 62 × 185 × 34, along Y; centre `(-54,0,83)`, top100; ends broaden to62, middle grip width40; end radii15 | P, G from three rounded blocks or simple shaped extrusion |
| Cradle inset | 66 × 180 × 6 beneath handset, centre `(-54,0,63)`; 8 mm aqua strip visible along its outer edge | A, G; microphone/speaker holes1.5 mm T |
| Cradle supports | Front saddle50 × 30 × 30 centred `(-54,-70,47)`, rear saddle50 × 30 × 8 at `(-54,70,60)`; R8 edges; bridge the wedge to the cradle undersideZ60 | P, G; intersect supports into the wedge rather than leaving the handset floating |
| LCD | 85 × 32, centred X66, Y55 on the sloped base, inset3 | N, G flat panel; tiny digits T |
| Dial pad | 3 columns × 4 rows, nominal Ø13 keys at X41/66/91 and Y15/-10/-35/-60, following slope; one aqua function key at X105,Y17 | P/A, G for chunky13 mm buttons; glyphs T |
| Cord | Compact dark coil in a 22 × 150 × 30 volume, X=-120..-98, Y=-75..75, Z0..30; Ø2.5 tube, coil outer Ø12, 6 pitch | N; lightweight curve G for overall coil silhouette, bake individual winding if too dense; base plus coil stays exactly240 wide |
| Foot pads | Four Ø12 × 4 pads under base corners, within Z0–4 | N, G |

**Aero read:** the pearl capsule handset, smoked LCD and aqua cradle give the phone a familiar pre-smartphone product identity.

### Doc_Tray

**Keep:** 280 × 340 × 180, exactly two tiers, open at the front.

| Feature | Size and position | Material / method |
|---|---|---|
| Two tray floors | Each 280 × 340 × 6, centres `(0,0,11)` and `(0,0,101)`; plan R10; fronts at Y=-170 | F, G; lower floor Z8–14, upper98–104 |
| Side/rear lips | 6 thick, 66 high above each floor, front cutaway; upper rear/side lip tops170; outer width/depth stay280/340 | F, G; front lip only12 high above floor, R6 edge |
| Tier risers | Two20 × 28 × 180 rear supports at X±125,Y150, centreZ90; profile R8; silver faces visible outside baskets | S, G; all support tops at180 |
| Front hand scoop | 90 wide × 8 deep rounded notch in each12-high front lip, centredX0 | F, G if it affects silhouette |
| Feet | Four Ø12 × 8 pads at X±115,Y±140, bottomZ0 | N, G; optional paper stack is dressing, not a third shelf |

**Aero read:** frosted aqua basket edges and silver stack risers make the two-tier silhouette distinct from a pair of opaque boxes.

### Chair_Task

**Keep:** base about600 wide, seat surface470, back top about1050; five-star base on casters.
**Chosen envelope:** 600 × 600 × 1050, neutral upright pose, no adjustable-height variant implied.

| Feature | Size and position | Material / method |
|---|---|---|
| Seat shell | 480 × 470 × 65, centred `(0,-15,437.5)`, lowest405/highest470; plan R35, exposed shell edge6 thick | P, G |
| Seat cushion | 450 × 435 × 50, centre `(0,-25,445)`, top470; cornerR30, pillow crown8 contained in50 thickness | N matte fabric, G; stitching1.5 mm T |
| Back shell | 460 wide × 50 thick × 565 high; upright near Y190, Z485–1050, top cornersR40; shallow concave curve20 deep | P, G; tilt remains inside600 depth |
| Back cushion | 435 × 45 × 535, on front of shell, bottom495/top1030; roundedR35 and single shallow lumbar waist | N, G; lower horizontal upholstery division2 mm T |
| Arm supports/pads | Two curved pearl supports35 × 200 × 230 at X±245, Y20, Z530; pads50 × 220 × 35, centres `(±245,0,657.5)` | P, G; silver inset30 × 120 on outside of each support, T/material break |
| Lift stem | Ø50, Z70–405; aqua shroud Ø60 × 180 at Z170–350 | C/A, G; narrow height-adjust seam2 mm T |
| Five-star base | Five silver tapered spokes, every72°, hubØ95 × 35 at Z75–110; each spoke45 wide at root,28 at tip, radial span40–260 | S, G; low soft crown and edgeR8 |
| Casters | Exactly five Ø50 wheels,26 wide; wheel centresZ25, swivel axes at radius260; full swivel clearance remains withinØ600 | N wheels/S forks, G; hubsØ12 G or T |

**Aero read:** navy pillowed upholstery in a pearl shell, an aqua lift shroud and a proper caster star replace the blockout pedestal-chair silhouette.

### Chair_Pod

**Keep:** 520 × 500 footprint; seat460; back860; swivel base.

| Feature | Size and position | Material / method |
|---|---|---|
| Continuous bucket shell | Outer maximum520 × 500, seat undersideZ400, back top860; 8 mm wall; back-seat inside bendR40, outer plan cornersR35 | P, G with a simple swept/extruded L profile; no arms |
| Seat pad | 470 × 430 × 40, centre `(0,-20,440)`, top460, R30 corners | N fabric, G |
| Back pad | 450 × 40 × 350, near Y190, Z490–840; soft crown10 contained in40; R35 top corners | N, G; 1.5 mm fabric seam T |
| Swivel stem | Ø50 × 375, Z25–400; rearward centre Y20 | C, G; no lift handle required |
| Disc foot | Ø480 × 25, centre `(0,0,12.5)`, edgeR10; lower insetØ430 × 4 rubber pad within height | S/N, G |
| Shell join | 2 mm shadow seam around pad-shell meeting line; pad appears nested into shell | N, T |

**Aero read:** the compact pearl bucket hugging navy pads above a chrome swivel and silver disc is a reception-chair counterpart to the task chair.

## Sheet 2 — desks and storage

### Desk_Sculpt

**Keep:** 1600 × 800 × 740.

| Feature | Size and position | Material / method |
|---|---|---|
| Crowned top | 1600 × 800 × 32, Z708–740; R40 plan corners, R8 edge bevel; crown2 within32 | P, G |
| Rim | 8 high × 4 deep silver perimeter strip at Z712–720, inset into outer profile | S, G |
| Tapered supports | Two pearl slab legs,720 deep; outer faces atX±780; 90 wide near ground,55 near top; spanZ8–708; inward bow35 centred atZ370; exposed edgesR20 | P, G from a simple profile extrusion; feet do not project beyond1600 × 800 |
| Modesty panel | 1320 × 12 × 360, centred `(0,275,500)`, bottom320/top680; cornerR20 | F, G; four silver20 × 20 brackets at X±620,Z340/660, behind panel |
| Cable grommet | Ø60 × 6 flush disc, centre `(520,270,737)`, Ø40 inner opening | S/N, G; fine split line T |
| Foot pads | Four70 × 100 × 8 pads under support corners, inset from silhouette | N, G |

**Aero read:** gently bowed pearl supports and a translucent hanging modesty panel create a light sculpted desk within the original footprint.

### Desk_Reception

**Keep:** 4000 × 900 × **1100**, with AONAGI name on the front.

| Feature | Size and position | Material / method |
|---|---|---|
| Counter cap | 4000 × 900 × 40, Z1060–1100; R40 plan corners and R12 edge bevel; subtle2 mm crown stays below1100 | P, G |
| Front/ends shell | Front wall spans Y=-450..-415, thickness35, centreY=-432.5; rounded end cheeks insideX±2000, wall35; Z50–1060; exposed edgesR35 | P, G; hollow staff side, no solid four-metre block |
| Aqua front band | 3520 wide × 240 high, centre `(0,-450,290)`, Z170–410; R25 ends; recess8 into front face | F, G; dark cavity plane10 behind for depth, no internal machinery |
| Silver counter rim | 12 high × 6 deep strip, Z1060–1072, follows cap perimeter but inset from outer dimensions | S, G |
| Shadow base | 3880 × 780 × 50 plinth, centreZ25; 25 mm dark shadow recess above/beside inset foot | S plinth/N recess, G; profile inset60 from outer ends |
| Staff worktop | 3800 × 550 × 28, centre `(0,140,726)`, surface740; no extension beyond rearY450 | P, G; staff access remains at rear |
| Front name | Flat printed/decal region1800 × 350 at front Y=-450, centreX=-200,Z770; Ø240 ring atX=-900,Z790, Japanese lettering110 high above AONAGI lettering75 high | A/H/N, T; use exact master text |
| End plaque | 330 × 100 at front-right, centreX1600,Z865; optional backing4 thick recessed4 into shell, R12 | P backing G; `受付 / RECEPTION` N, T |
| Panel joints | Two2 mm vertical joins nearX±1250 on lower shell only | N low-contrast, T |

**Aero read:** pillowed long ends, a broad frosted front inset and precise corporate lettering turn the cover-height counter into a designed reception product.

### Storage_Low

**Keep:** 1600 × 450 × **1000**.

| Feature | Size and position | Material / method |
|---|---|---|
| Carcass/cap | 1600 × 450 plan, outer R25; cap24 high atZ976–1000; side/end walls24; body above25 mm plinth | P, G; cap crown2 contained in24 |
| Header ribbon | 1504 × 72 × 8, centred `(0,-221,924)`, Z888–960; R12 | F, G in front inset |
| Four doors | Each374 × 22 × 804, X centres-570/-190/190/570, frontY=-225, Z64–868; R16 corners; 6 mm spaces between doors | P, G; fine2 mm hinge/seal lines T |
| Finger pulls | Each18 × 90 vertical capsule recess6 deep; at same local door X+130,Z475, inset from front | S lining/N recess, G |
| Plinth | 1530 × 380 × 25, centreZ12.5; creates35 mm plan setback | S, G; N shadow strip8 high just above |
| Rear panel seam | 2 mm rectangular inset line, 40 from rear edges | N, T; no handles on back |

**Aero read:** four softly rounded doors beneath a frosted header and silver foot give the cabinet a coherent pearl-and-glass product rhythm.

### Copier

**Keep:** 620 × 660 × 1050, including scanner lid and controls.

| Feature | Size and position | Material / method |
|---|---|---|
| Main shell | 620 × 660, R25 plan; bodyZ35–930; outer edge bevelR10; lower plinth30 high inside footprint | P/S, G |
| Scanner crown | 600 × 540 × 120, centre `(0,45,990)`, top1050; R25; rear staysY315 within330 | P, G |
| Smoked lid | 500 × 400 × 20, centre `(0,70,1034)`, highest1044; R20; inset6 into scanner crown | N gloss, G; central lid seam2 mm T |
| Control pod | 285 × 145 × 75, centre `(135,-245,925)`; sloped top fromZ950 rear toZ910 front; keep frontY≥-330 and all upper points≤1000 | P outer/S face, G |
| LCD and buttons | LCD105 × 35 on left of pod; aqua startØ35 to its right; eightØ12 neutral keys in2 rows; status LEDØ4 | N LCD/P keys/A start, G for buttons and screen plane; LED, `複写 / COPY`, legends T; no red control |
| Paper exit | 420 × 165 opening at front, centredX=-40,Z735, depth120; R18 corners; floor lip15 thick | N recess/P lip, G with one boolean; 1 mm paper guides T |
| Paper drawers | Two560 × 220 rounded fronts, Z55–275 and285–505, frontY=-330; R15; each aqua finger recess130 × 24 × 8 at local top-45 | P/A, G; 2 mm front seams T |
| Vent bank | 100 × 55 on left side, centreY-140,Z850; five80 × 3 rounded slots at10 pitch | N, T normal/colour |
| Small badge/feet | Ø45 AONAGI mark front-rightX245,Z650; fourØ35 × 10 pads under corners, withinZ0–10 | A/H/N label T; N feet G |

**Aero read:** the smoked scanner crown, sloped physical control pod, aqua grips and broad paper-exit recess read as an early multifunction copier rather than a filing box.

## Sheet 3 — amenities

### Water_Server

**Keep:** 350 × 350 × 1350 including bottle.

| Feature | Size and position | Material / method |
|---|---|---|
| Body | 350 × 350 × 970, R25 plan corners, R12 edge bevel; body base startsZ10, highest bottle collar at970 | P, G |
| Inverted bottle | Ø300 × 380, Z970–1350; top/bottom shoulder radii30, Ø85 neck inside collar; two broad reinforcing ridges12 high and10 proud, included withinØ300 | Translucent A/F, G rotational profile; water surface/refraction soft, no bubbles |
| Neck collar | Ø160 × 30, Z940–970, centred in plan | S, G |
| Silver waist | 20 high × 4 deep band atZ655–675, inset into shell | S, G |
| Dispensing bay | 270 × 290 opening, front atY=-175, Z330–620, depth70, cornersR20 | N smoked interior, G |
| Two taps | 28 × 24 × 50 bodies, X±65, nozzle endsZ480; silver20 × 12 spouts; aqua levers15 × 30 | P/S/A, G; no red/blue hot-cold blocks |
| Drip tray | 260 × 90 × 20, centre `(0,-130,340)`; roundedR12; front flush withY=-175 | A, G; 2 mm grille lines T |
| Label/status | Ø65 ring centredX0,Z830; `給水 / WATER` on160 × 28 below atZ765; cyan status capsule25 × 5 atZ710 | A/H/N, T |
| Feet/rear vents | FourØ30 × 10 feet, centresX±120,Y±120; rear vent patch140 × 100 atZ180, 3 mm rounded-slot graphic | S/N, feet G, vents T |

**Aero read:** an aqua shoulder bottle over a pearl tower, silver waist and smoked twin-tap bay restores the unmistakable water-server outline.

### Umbrella_Stand

**Keep:** stand Ø220 × 550; closed umbrellas reach1000 total, not550 plus1000.

| Feature | Size and position | Material / method |
|---|---|---|
| Cylinder | OuterØ220, 6 wall, Z0–550; silver sleeveZ70–520 | S, G cylinder with low segment count adequate for soft highlight |
| Rolled top rim | OuterØ220, innerØ190, height30, Z520–550, sectionR8 | P, G |
| Drip cup | OuterØ216 × 70, Z0–70, bottom8 thick, R10 lower edge | F, G; inner dark floor atZ12 N |
| Perforations | Three staggered rows of12 slots; each12 wide × 90 high, radius6, row centresZ155/300/445; slot pitch30° | G holes only on visible silhouette/close mesh; otherwise N T alpha/normal on sleeve, never mesh every2 mm grille rib |
| Umbrellas | Two closed folded navy bundles, eachØ65 × 550; shaftsØ8; maximum hook top1000; centresX±45,Y0, bundled clothZ250–800 | N fabric/C shafts, G simple tapered radial folds |
| Hook handles/straps | Hook outer80 × 90, tubeØ12, top1000; roots nearX±45,Z910; straps15 high atZ650 | C/P hooks G; N straps T; keep total carried assembly within aboutØ220 plan |

**Aero read:** a pearl rim and translucent drip cup interrupt the orderly slotted silver cylinder, with hooked closed umbrellas giving it immediate purpose.

### Vending_Machine

**Keep:** 900 × 650 × 1800, all controls recessed inside it.

| Feature | Size and position | Material / method |
|---|---|---|
| Outer shell | 900 × 650 × 1800, R35 plan corners, edge bevelR15; base feet remain withinZ0–30 | P, G |
| Header | 760 × 175, frontY=-325, Z1560–1735, R25; inset6 | A, G; `自動販売機 / DRINKS` with Ø85 ring in a650 × 100 print region, T |
| Display | 570 × 850 smoked rounded window, centredX=-85, Z1075, openingZ650–1500; R25; recessed10; dark interior depth70 | N gloss, G |
| Drink grid | Three rows × four columns; twelve cans/bottlesØ70 × 130 nominal, X=-280/-155/-30/95; row baseZ700/960/1220; row shelves8 thick | P/A/N fictional packaging, G simple low-sided cylinders; brand words/wave stripes T, no real packs or warm colours |
| Selection controls | Twelve aqua capsule buttons36 × 14, below corresponding products atZ670/930/1190 | A, G; product numbers/status dots T |
| Payment module | 150 × 380 plate, centreX315,Z1080, R15; recessed5; small LCD70 × 45 near top, coin recessØ35 below, note slot75 × 12 near bottom | S/N, G for plate and major openings; Ø4 LED and all legends T |
| Pickup bay | 620 × 210 opening atX0,Z335; front recess100 deep; R25, floor lip20 | N/P, G; grille lines2 mm T |
| Feet/base | FourØ60 × 30 feet atX±330,Y±220; low lower-body seam3 mm high atZ85 | S feet G; N seam T |
| Side service detail | 200 × 100 vent bank near rear-right atZ250; 3 mm slots and2 mm door seam | N, T |

**Aero read:** a large smoked product window, physical aqua selection keys and silver payment pod make a rounded white Japanese drinks machine without relying on a real brand.

### Slipper_Cabinet

**Keep:** 900 × 350 × 700, six open cubbies in a3 × 2 grid.

| Feature | Size and position | Material / method |
|---|---|---|
| Outer frame | 900 × 350 × 700, outerR25, edgeR10; sides30 thick; top/header90 high atZ610–700 | P, G |
| Inner cavity | 840 wide, depth300; frontY=-175, backY125; Z40–610 | F back plane/P frame, G |
| Partitions | Two vertical8-thick panels atX±140, Z40–610; one horizontal8-thick shelf centred atZ325; bottom shelf8 thick atZ32–40 | F, G; six clear cells approximately275 W ×280 H |
| Front edge treatment | Shelf/partition exposed edgesR4; no separate decorative strip around each cubby | F, G |
| Base | 850 × 300 × 25, centreZ12.5, R15 | S, G |
| Header plaque | 480 × 60, centre `(0,-175,655)`, R12, recessed3; Ø42 mark at left, `室内履き / SLIPPERS` | P backing G; A/H/N print T |
| Slipper pairs | Six pairs, two slippers per cubby, each95 × 235 × 60; pair width210; centres of pairs atX=-280/0/280, Y=-10; soles sitZ40 or329 | M uppers/P soles, G simple capsule sole plus rounded toe cap; 2 mm sole join T |

**Aero read:** a pearl rounded frame and frosted aqua open grid, dressed with pale slipper pairs, make a small corporate entry amenity rather than generic shelving.

### Planter_Box

**Keep:** pot1000 × 450 × 550; foliage above it.
**Chosen foliage height:** about1200 total; foliage is not part of the pot's550 height.

| Feature | Size and position | Material / method |
|---|---|---|
| Trough shell | 1000 × 450 × 550, R35 plan corners, edgeR12; side walls20; outer base above25 mm recessed foot | P, G |
| Opening/rim | Opening940 × 390, R30, soil planeZ515; top rim20 thick withinZ530–550; outer rim stays within1000 ×450 | P, G |
| Aqua ribbon | 45 high × 6 deep inset band on outer sides, Z455–500, continuing around softened ends | F, G with dark backing; do not make a separate giant glass planter |
| Base foot | 920 × 370 × 25, centreZ12.5, R25; 15 mm dark recess above | S foot/N recess, G |
| Soil | Rounded plane920 × 370 atZ515, below rim, no pebbles | N rough, G plane; soil grain T |
| Foliage | Three clusters atX=-320/0/320,Y0; 6–8 broad leaves per cluster, each80–130 wide ×240–420 long; simple convex midrib bend25–45; tallest leaf tip≤1200 | L, G low-poly two-sided leaf planes, stemØ8; 1 mm veins T; all growth emerges from inside opening |

**Aero read:** a disciplined aqua ribbon and recessed silver foot turn a simple pearl trough into a product, while broad clean leaves echo the approved room plants.

## Sheet 4 — pendant and column

### Light_Pendant_Linear

**Keep:** bar2400 × 70 × 60; its **top is800 below the ceiling**.
Local origin is the bar's bottom centre: barZ0–60, ceiling planeZ860, cables bridge Z60–860.
The hanging assembly is860 high including bar, not800 high including bar.

| Feature | Size and position | Material / method |
|---|---|---|
| Capsule housing | 2400 × 70 × 60, plan endR20, sectionR10; boundsX±1200,Y±35,Z0–60 | S, G bevelled extrusion |
| End caps | Each40 long ×70 deep ×60 high, atX±1180; end contourR20, edgeR10; replace housing ends, do not extend them | P, G |
| Diffuser | 2300 × 50 × 12, centre `(0,0,6)`, undersideZ0; planR15; inset10 from front/rear edges | Frosted-milk P/F, G; restrained soft-white emission, no visible point LED geometry |
| Upper spine | 2280 × 35 × 15, centreZ52.5, highest60, recessed relative to curved sides | S, G |
| Two cables | Ø3, X±800,Y0; each800 long from bar topZ60 to ceilingZ860 | C, G curves because suspension silhouette matters despite sub4 mm diameter |
| Cable ferrules | Ø8 × 12, Z48–60; sockets remain within bar height | S, G |
| Ceiling canopies | EachØ50 × 15 atX±800,Y0, bottomZ845/top860; low rounded pearl disc; cable is partly hidden by canopy | P, G |
| Fine seams | 1 mm cap joins atX±1160 and1 mm diffuser frame line | N low contrast, T |

**Aero read:** a silver capsule extrusion, pearl end caps and frosted-milk underside give the linear light a finished catalogue-product section.

### Column_Graphic

**Keep:** 800 × 800 rounded-square footprint,4300 tall, vertical corner radius exactly60.
It remains a square column; the printed band does not twist or reshape the structure.

| Feature | Size and position | Material / method |
|---|---|---|
| Main cladding | 800 × 800 ×4300; four vertical plan cornersR60; smooth flat face centres; body begins/ends within collars | P, G rounded square, not cylinder |
| Upper/lower collars | Each800 ×800 ×60 with sameR60 outline; Z0–60 and4240–4300; inset silver faces1 mm from main outside plane | S, G; no collar protrudes beyond800 |
| Collar seam | 2 mm dark line atZ60 and4240 | N, T |
| Aqua band | Single450-high printed band wrapping all four faces; front centreline rises fromZ1200 atX=-400 toZ2100 atX400; side continuation joins smoothly, maximum upper edge about2500; close the pattern on rear | A, T; use a UV graphic, not added ribbon geometry |
| Frosted edge | One25 mm pale edge follows the upper side of band | F colour as opaque print, T; no transparency added to structural column |
| Floor code | Front faceY=-400; `2F` letter height280, text centreZ3450; `審査部` height145 atZ3140; `REVIEW` height105 atZ2950; all centredX0 in a560-wide print region | N, T; floor code is example label artwork, not an invented runtime sign system |
| Asset badge | Right faceX400, centredY0,Z2700; Ø90 ring, AONAGI lettering25 high beneath | A/H/N, T |
| Panel joins | Rear-only vertical2 mm seam, centred on rear face | N low contrast, T |

**Aero read:** generous R60 white corners, inset silver collars and one printed aqua sweep carry the room identity without adding architectural bulk.

## Production priorities and interpretation

At a distance, keep the broad pearl/aqua/navy material break and recognisable silhouette before making buttons or labels.
The physical details that should survive a quick flight are the handset, tray tiers, chair base, copier recess, bottle, umbrella hooks, six cubbies and planter rim.
Use the measured envelopes above as modelling bounds; do not enlarge a prop to fit a detail from the picture.
The desk divider, reception and low cabinet remain1150/1100/1000 high respectively.
Rendered reflection streaks and soft shadows are lighting references, not extra pieces to model.

The column band closes around the rear and is a flat graphic; its exact wrap is an art-production layout within the defined band region.
The pendant's diffuser is a broad strip, not a line of individually modelled LEDs.
All hatches, screw heads, fine vents, key legends and labels below4 mm should remain texture work even where the illustration suggests embossed relief.
The suspension cables and phone cord are exceptions only where their silhouette is necessary; simplify them instead of filling the object with tiny geometry.

## Generation provenance

Used the **built-in image_gen tool**, not CLI/API fallback.
There were **6 image-generation tries**: four original sheets and two narrowly targeted corrections.
The eight user-supplied images were inspected as context: four current-prop sheets for replacements, two level images for placement, and two approved rooms for finish and material language.
The original sheets were generated as new designs from text, not edits of the blockout sheets.
The full original and correction prompts are saved in [generation_prompts_sheets_1_4.json](generation_prompts_sheets_1_4.json).

| Try | Purpose | Final file / selection |
|---|---|---|
| 1 | Original workstation sheet | Superseded by try5: pod-chair inset width needed correction from about0.60 to0.52 m |
| 2 | Original desks/storage sheet | Superseded by try6: a tiny copier button needed changing from salmon/red to aqua |
| 3 | Original amenities sheet | `sheet_3_amenities.png` |
| 4 | Original pendant/column sheet | `sheet_4_light_column.png` |
| 5 | Edit of try1; correct only pod-chair inset width | `sheet_1_workstation.png` |
| 6 | Edit of try2; change only small red copier control to aqua | `sheet_2_desks_storage.png` |

`contact_sheet.png` is a labelled layout of the four final raster sheets, not another generated design or a fifth concept proposal.
All four final sheets are the tool's native **1672 × 941** PNGs, approximately16:9; no upscaling or crop was applied.
The contact sheet is **3440 × 2128** and places each final sheet at1:1 pixel scale beneath a separate caption.
It was assembled with PowerShell/.NET `System.Drawing`, without altering the generated product artwork.

### Static and visual validation

- Opened and visually inspected all six generated candidates; retained tries3,4,5,6.
- Confirmed try5 changes the pod side-view width to0.52 m, and try6 changes the tiny red copier button to aqua while retaining red logo dots.
- Opened the saved contact sheet and checked that all four final panels, captions and page edges are visible.
- Loaded every final PNG with `System.Drawing.Image.FromFile`; all four sheets decoded as1672 ×941, and the contact decoded as3440 ×2128.
- Compared `Get-FileHash` SHA256 values of each selected tool output and its project copy: all four matched.
- Checked all18 requested prop headings and all relative Markdown links in this guide; parsed the prompt JSON as4 original prompts plus2 corrections.
- Reviewed the part coordinates against the envelopes, including the phone cord, bench feet, copier controls, pendant drop and recessed column collars.

The file checks can be repeated from the repository root with PowerShell:

```powershell
Add-Type -AssemblyName System.Drawing
$aeroDir = 'ArtSource/Kit/concepts/Props_Aero'
$aeroFiles = 'sheet_1_workstation.png','sheet_2_desks_storage.png',
    'sheet_3_amenities.png','sheet_4_light_column.png','contact_sheet.png'
foreach ($aeroFile in $aeroFiles) {
    $aeroPath = Join-Path $aeroDir $aeroFile
    $aeroImage = [System.Drawing.Image]::FromFile((Resolve-Path $aeroPath))
    "$aeroFile : $($aeroImage.Width) x $($aeroImage.Height)"
    $aeroImage.Dispose()
}
$aeroPrompts = Get-Content -Raw -Encoding UTF8 `
    (Join-Path $aeroDir 'generation_prompts_sheets_1_4.json') | ConvertFrom-Json
"Original prompts: $($aeroPrompts.prompts.Count); corrections: $($aeroPrompts.corrections.Count)"
```

Next: Alex reviews the silhouettes and material breaks in the contact sheet before these concepts are modelled in Blender.
