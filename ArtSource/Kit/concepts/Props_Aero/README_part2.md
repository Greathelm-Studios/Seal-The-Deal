# AONAGI computers, doors and lift — part 2

**Status:** Target — concept proposal for artist review  
**Last updated:** 2026-11-15  
**Project:** Seal the Deal

The loose computers have pearl shells, smoky screens, capsule feet and aqua translucent panels that remain readable in flight.
Doors and the lift receive the same restrained 2002 Japanese corporate finish, with their existing openings retained.
These are new concept paintings and proposed modelling dimensions, not remodelled assets or measurements of source meshes.

## Files and source of truth

| File | Contents | Image attempt |
|---|---|---|
| [sheet_5_computers.png](sheet_5_computers.png) | LCD A, compact CRT B, tower, keyboard, mouse/pad, dressed bench scale context | 1 |
| [sheet_6_doors_lift.png](sheet_6_doors_lift.png) | Ordinary wall/leaf, lift landing/roof variation, paired lift leaves, car cutaway and detail studies | 3 |
| [part2_generation_prompts.json](part2_generation_prompts.json) | Exact prompts, reference roles, output mapping and attempt history | All 3 |

Both PNGs are the built-in image tool's native **1672 × 941 pixels**, approximately 16:9.
The prompts requested 2400 × 1350 or the largest native 16:9 output; the selected images were copied unchanged, without upscaling, cropping or repainting.
There were **three image calls total: one for sheet 5, then a sheet 6 generation and one targeted correction**.
Attempt 2 was superseded because the car's small front elevation implied an entry as wide as the cabin.
Attempt 3 identifies the large view as a shell cutaway and shows the retained 1.40 × 2.20 m entry in its small front elevation.
No part-1 deliverables are written or replaced by this part.

[REQUEST_2.md](REQUEST_2.md) controls the requested props and dimensions; [REQUEST.md](REQUEST.md) controls the shared look.
The paintings are appearance references, not scale drawings: the numeric specifications below govern over perspective, drawn seams and apparent panel thickness.
Every added dimension below is a **proposed visual construction**, not an assertion about existing geometry.
Preserve source origins, pivots, opening locations and authored transforms when these concepts are eventually modelled.

### Three reference discrepancies to retain explicitly

1. The current computer sheet repeats a suspended-light silhouette and 2.4 m dimension stamp for both monitor and keyboard; use the written computer sizes instead.
2. The current lift-car sheet stamps **3.70 × 3.55 × 2.90 m overall**, while the brief asks for **3.70 × 3.55 × 2.60 m inside** and also forbids envelope changes.
   The concept labels the requested inside size, but equal inside and outside footprints cannot both describe a shell with thickness.
   **Keep the source exterior envelope; confirm actual interior clear width/depth with Alex before modelling the car, rather than enlarging it to satisfy the inside label.**
   The 300 mm difference in height is not evidence of any particular floor/ceiling thickness.
3. Only the 1F frame is dimensioned in the supplied before sheet: 4.00 × 0.24 × 4.50 m.
   The roof variation shares its visual treatment and 1.40 × 2.20 m aperture; its source wall height/depth must be retained, not inferred from the roof inset painting.

## Modelling conventions

- All feature dimensions and coordinates below are **millimetres**; envelopes in the headings are **metres, W × D × H**, except explicitly labelled panel faces.
- Z is up; X is centred across an object's width; Y runs from its front edge towards its back.
- For loose props Z=0 is their bottom contact plane, not the floor of the room.
- For the bench, walls and car Z=0 is the existing floor/threshold level.
- R means bevel/corner radius; Ø means diameter.
- Build main silhouettes, useful recesses, structural thickness and visible hardware as geometry.
- Put sub-4 mm decorative seams, legends, grain, indicator rings and badges in textures/normal maps; a broad thin sheet or pane may still use simple geometry for its silhouette.
- Fit trim and rounded edges inside the named envelope; never add a bevel by expanding an object, narrow a clear opening, or change collision/interaction behaviour from these pictures.
- The flat painted keys are a texture grid; the highlight that suggests raised keycaps in the painting is not a request for individual key meshes.
- Blue light is a restrained appearance cue here; no new runtime lighting system is specified.

### Palette and part finishes

| Material shorthand | Colour | Finish / use |
|---|---|---|
| Pearl | `#F4F8F8` | Gloss plastic on computer shells; satin wall/leaf cladding |
| Silver | `#C8D2D9` | Brushed metal on fascias, portals and hardware |
| Chrome | `#C8D2D9` | Same silver family, polished on small necks/return hardware |
| Aqua | `#45BFC7` | Tinted trim, restrained cyan indicator emission and printed accents |
| Frosted aqua | `#C6E9E6` | Translucent side panels, vision strips and divider |
| Mist | `#A8BFD1` | Quiet cool pad backing / cabin floor finish |
| Smoke / navy | `#111B38` | Smoked screens, indicator windows, rubber and dark recesses |
| Hanko red | `#C7473E` | Only the tiny dot at the lower-right gap in the AONAGI ring |

Material softness comes from roughness, highlights and broad bevels, not dirt or ornamental gradients.
No real product brand or model name is used.
The AONAGI badge is an open aqua ring with a small red dot in its lower-right gap, applied as a texture rather than raised jewellery.

## Sheet 5 — computers

### Monitor_Flat — option A

**Kept:** panel outer face 0.55 W × 0.34 H; overall height about 0.46.
**Proposed complete visual envelope:** 0.55 × 0.18 × 0.46, including foot; panel housing depth 48.
The 550 × 340 dimension includes the bezel, not just the active screen.

| Part / feature | Size and position | Material | Build |
|---|---|---|---|
| Panel shell | 550 W × 48 D × 340 H; bottom Z120, top Z460; front at Y90; plan/front outer corners R12, edge bevel R5 | Pearl back, silver front bezel | Rounded box with inset front |
| Bezel / screen | Bezel 25 wide on all four edges; visible screen 500 × 290, centred in panel; inset 6 | Silver lip, smoked navy screen | Screen plane and one recess, not a deep black cavity |
| Foot | 240 W × 180 D × 18 H; X0, Y0–180, Z0–18; capsule ends R90 in plan, edge R6 | Pearl top, silver rim, navy underside | One capsule base and simple inset underside |
| Neck | Ø32 × 110 high, centred X0/Y110, Z18–128, overlapping shell at its bottom | Chrome | Cylinder with rounded shoulder |
| Power LED | Ø3 at X245, Z136 on lower-right bezel | Aqua, faint emission | Texture/emissive dot |
| Badge | 12 diameter ring, red dot 2; X0, Z136 on lower bezel | Aqua + hanko red | Flat texture |
| Screen reflection / shutline | One faint broad diagonal streak on screen; shell shutline 1.5 wide around rear cap | Mist highlight / navy seam | Texture or shading, no streak geometry |

**2000s Aero read:** the substantial pearl/silver bezel and chrome neck over a capsule foot replace the modern thin-screen silhouette.

### Monitor_CRT — option B, alternative to A

**Kept comparison:** 0.55 × 0.34 panel face, about 0.46 overall height; similar desktop presence to A.
**Proposed depth:** 0.36, because the compact deep back is the distinguishing alternative.
This is an alternative visual choice, not an extra required computer on every desk.

| Part / feature | Size and position | Material | Build |
|---|---|---|---|
| Deep shell | Maximum 550 W × 360 D × 340 H; Z120–460; front at Y0, rear at Y360; taper to a 410 W × 250 H rear cap; major corners R25, small edges R10 | Pearl | Bevelled box with tapered rear, not an elongated LCD |
| Front lip / screen | 25 wide silver border within 550 × 340 face; 500 × 290 smoked screen with a shallow 5 crown | Silver and navy smoke | Single gently curved screen and front lip |
| Foot / neck | Capsule 280 W × 260 D × 20 H at Y50–310; Ø60 neck from Z20 to Z128 at Y190 | Pearl base, chrome neck, navy contact inset | Capsule + short cylinder |
| Vent bank | Six rounded 65 × 6 slots, centres spaced 12; centred near Y280/Z210 on visible side | Navy recess on pearl | Baked normal/texture; apparent recess only 2 |
| LED / badge | Ø3 LED at X245/Z136; 12 ring at X0/Z136, 2 red dot | Aqua + hanko red | Texture/emission |
| Shell seam | 1.5 line around rear cap, inset 15 from its perimeter | Navy | Texture |

**2000s Aero read:** a compact, pillowed pearl CRT with cool silver trim and discreet cyan power signalling feels like transitional office technology.

### PC_Tower

**Kept envelope:** 0.19 × 0.42 × 0.40; placed on the floor beside a pedestal.

| Part / feature | Size and position | Material | Build |
|---|---|---|---|
| Main case | 190 W × 420 D; shell Z8–400, outer R12; all details within total envelope | Pearl | Rounded box |
| Front fascia | 166 W × 360 H, X0, Z24–384; front face flush at Y0, recess up to 4 | Silver | Inset rounded panel, R10 |
| Side window | 350 D × 330 H, Y35–385, Z45–375, at left X−95; 4 thick, face recessed 2 from case side | Frosted aqua | One tinted panel; no detailed or glowing internals |
| Drive slot | 110 W × 10 H, X0/Z330; rounded ends R5 | Navy slot, silver surround | Shallow recess, not a removable drive model |
| Power button | Ø24 at X0/Z260, front face flush; 2.5 wide ring | Pearl centre, aqua ring | Button disc; ring in texture/emission |
| Front vents | Three 110 × 8 rounded slots at Z54, 76, 98, X0; depth 4 | Navy recess | Boolean recesses or a shared normal map |
| Feet | Four Ø18 × 8 pads; X±70, Y35/385, Z0–8 | Navy rubber | Four simple cylinders, included in height |
| Panel seam | 1.5 wide around side insert | Navy | Texture |

**2000s Aero read:** a pearl appliance body with translucent aqua flank, drive slot and ring power button reads as office equipment rather than a generic crate.

### Keyboard

**Kept envelope:** 0.44 × 0.14; height 0.015 at front to 0.030 at rear.

| Part / feature | Size and position | Material | Build |
|---|---|---|---|
| Wedge shell | 440 W × 140 D; front H15, rear H30; R10 plan corners, R5 side edges; crown at most 2 within height | Pearl | One bevelled wedge |
| Key field | 408 W × 108 D, X0, Y18–126; standard compact office arrangement with numpad, typical normal-key pitch 18 | Pearl keys, mist gaps, navy legends | Flat texture grid / baked shallow normal only |
| Lower trim | 4 high band at Z5–9, inset within shell outline | Aqua | Material band on shell or inset simple band |
| Silver hairline | 1.5 line above trim | Silver | Texture |
| Underside pads | Four 20 × 10 contact patches, centred X±180/Y20 and Y120; included in wedge height | Navy | Texture / shallow normal; no projecting feet |

**2000s Aero read:** the softly wedged white body and aqua/silver lip give familiar office peripherals a finished optimistic product character.

### Mouse and aqua pad

**Kept mouse envelope:** 0.06 × 0.11 × 0.035.
**Proposed pad:** 0.20 × 0.24 × 0.003, as shown in sheet 5.

| Part / feature | Size and position | Material | Build |
|---|---|---|---|
| Mouse dome | 60 W × 110 D × 35 H; rounded R15 ends, gently crowned top; base Z0–4 | Pearl top, navy lower rim | One smooth dome over a flat bottom |
| Wheel | Ø14 × 6 wide, axis along X; centre X0/Y30/Z28, uppermost point Z35 | Silver, fine mist tread | Single cylinder; tread in texture |
| Button seams | 0.6 central seam from Y0 to Y55, curved rear termination; 1 side-shell shutline | Mist / navy | Texture or normal map |
| Mousepad | 200 W × 240 D × 3; plan R18, aqua top with mist/navy underside | Aqua / mist | One simple thin slab or solidified plane |

**2000s Aero read:** a pearl pebble mouse on a quiet aqua pad is soft, glossy and legible without futuristic decoration.

### Workstation_Bench — equipment scale context

**Kept:** 2.40 × 1.60 footprint, worktop Z0.74, divider top Z1.15; two 0.40 × 0.55 × 0.60 pedestals.
The equipment context does not replace the furniture proposal being produced in part 1.
Use that sheet's final furniture spec; the dimensions below explain only this illustration's dressing and shared material cues.

| Part / feature | Size and position | Material | Build |
|---|---|---|---|
| Worktop cue | 2400 × 1600 overall; top Z740; proposed 32 thickness, 8 high rim within it; plan R40 | Pearl with silver perimeter | Existing bench silhouette with softened edge treatment |
| Centre divider cue | 18 thick at Y791–809; top Z1150; proposed pane bottom Z750; 20 wide capsule-ended frame within its silhouette | Frosted aqua, silver frame | Pane and simple perimeter frame |
| Pedestal cue | Each 400 × 550 × 600; use part-1 arrangement; do not infer new placements from perspective | Pearl, silver pulls, navy base gap | Part-1 geometry |
| LCD A placement | Foot centred X0, Y340–520; bottom Z740; monitor overall top Z1200, slightly above the 1150 divider | Materials specified above | Separate loose prop |
| Keyboard placement | X0, Y80–220; bottom Z740 | As above | Separate loose prop |
| Mouse / pad placement | Pad centre X340/Y190, bottom Z740; mouse centred on pad, bottom Z743 | As above | Separate loose props |
| Tower placement | Bottom Z0; place beside a pedestal with at least 25 visual separation, never on desktop | As above | Separate loose floor prop, not fused to bench |

The chair, trays and opposite-side equipment in the context are scale/background dressing; their designs remain owned by part 1.
No authored level placement is changed by these illustrative coordinates.

**2000s Aero read:** pearl/silver equipment sits against the frosted-aqua cover divider as one coherent office product family.

## Sheet 6 — doors and lift

### Wall_Door_4m

**Kept envelope:** 4.00 × 0.20 × 4.30.
**Kept rectangular clear opening:** 1.20 W × 2.40 H, at the existing position.
Coordinates below use the centred opening shown in the supplied before sheet; preserve its actual source origin.

| Part / feature | Size and position | Material | Build |
|---|---|---|---|
| Wall finish | Existing 4000 × 200 × 4300 wall volume | Pearl satin | Existing wall, no new thickness |
| Silver jamb skins | 30 wide bands immediately outside X±600 clear edges; header band Z2400–2430; visible skin recessed into front 20 of wall | Silver | Inset strips/portal, no intrusion into aperture |
| Outer jamb corners | R12 on exposed outer edges; inner passage edges remain at original opening boundary | Silver | Bevel within trim volume, not an arch |
| Bilingual room plate | 100 W × 65 H; centre X−820/Z1500 on front wall, outside jamb | Pearl field, navy text | Texture/decal on wall, no projecting plate geometry |
| Plate wording | `会議室` above `MEETING`; approximately 16 high Japanese and 7 high English | Navy | Texture; re-typeset rather than copying generated glyphs |

**2000s Aero read:** pearl wall planes, softened silver jambs and a quiet bilingual plaque frame a practical office door.

### Door_Leaf

**Kept slab envelope:** 1.18 × 0.04 × 2.38.
Do not enlarge this thickness for handles; use the recessed pull construction below.
In a centred 1200 × 2400 aperture the 1180 × 2380 slab allows 10 on each side/top/bottom; keep the source hinge, swing and pivot.

| Part / feature | Size and position | Material | Build |
|---|---|---|---|
| Main leaf | 1180 W × 40 D × 2380 H; edge R3, front broad corner R8 within slab | Pearl | Rounded slab |
| Vision strip | 160 wide centred X320; runs from leaf Z20 to Z2360, leaving only small end rails; glass 4 thick at mid-depth | Frosted aqua | Single pane in full-height-style opening, no solid aqua block |
| Clearer centre band | Between floor Z350 and Z2100; above/below remain fully frosted | Frosted aqua with reduced frosting in centre | Material/texture mask, no extra pane seam |
| Vision bead | 4 wide around strip, flush with face | Silver | Shared frame geometry or normal map |
| Long bar pull | Ø12 × 600 long, centre X−400/Z1100 in leaf coordinates; bar front flush with front face | Brushed silver | Cylinder plus two simple recessed end supports |
| Pull pocket | 50 W × 660 H × 32 deep, centre matches bar; R10 corners; bar occupies front 12, leaving 20 finger clearance behind | Pearl recess, silver bar | Boolean recess; keeps all hardware within 40 slab thickness |
| Fine assembly line | 1 around bead and pull supports | Navy | Texture |

The painting does not reliably resolve the pull pocket; follow this recessed construction to retain the declared leaf envelope.
The strip is on the opposite side from the handle, as drawn; mirror only to match the authored door handedness.

**2000s Aero read:** the nearly full-height frosted-aqua strip and long silver pull make the pearl slab feel like designed corporate joinery.

### Lift_Door_Frame_4m_1F and roof variation

**Kept 1F envelope from current sheet:** 4.00 × 0.24 × 4.50.
**Kept clear opening:** 1.40 W × 2.20 H.
For the roof variant, retain its source full-storey wall envelope and the same aperture; do not substitute the 1F wall height without checking its source mesh.

| Part / feature | Size and position | Material | Build |
|---|---|---|---|
| Landing wall | Existing 4000 × 240 × 4500 for 1F; existing roof variant volume for roof | Pearl satin | Existing wall |
| Portal bands | 50 wide outside opening edges X±700; header Z2200–2250; skin set into existing front 20 of wall | Brushed silver | Bevelled rectangular strips, outer exposed R15; opening stays rectangular |
| Indicator housing | 1000 W × 260 H × 20 D, X0, centre Z2580; fully recessed within wall skin | Silver rim, smoked navy front | Rounded inset R15 |
| Indicator display | 900 W × 170 H centred in housing; `1F` cyan dot matrix, character height about 96, 4 dots at 7 pitch | Smoke + aqua emission | Flat texture/emissive dots, no individual dot meshes |
| Call panel | 120 W × 220 H × 20 D; centre X−1000/Z1120; flush inset at front wall, R15 corners | Pearl face, silver rim | Single rounded panel |
| Up/down buttons | Two Ø32 discs, X−1000; centres Z1170 and Z1070; cyan rings 2 wide | Pearl/silver centres, aqua rings | Simple button discs, ring and arrow textures |
| Roof variation | Same portal, indicator housing and call panel; display `RF` in same dot-matrix style | Same materials | Surface variant only; retain roof wall and opening geometry |

No new sill, curved opening, moving mechanism or functional call behaviour is proposed.
All trim sits outside the clear opening and inside the existing wall envelope.

**2000s Aero read:** the smoked cyan floor indicator, pearl call panel and softly brushed portal recall restrained early-digital Japanese office lifts.

### Lift_Door_Panel — two bi-parting leaves

**Kept per leaf:** 0.75 × 0.04 × 2.25.
**Closed pair nominal width:** 1.50, covering a 1.40 clear opening by overlapping 50 behind each outer jamb.

| Part / feature | Size and position | Material | Build |
|---|---|---|---|
| Leaf slabs | Each 750 W × 40 D × 2250 H; closed left occupies X−750–0, right X0–750, relative to opening centre | Brushed silver | Two separate bevelled boxes, R3 within thickness |
| Height overlap | Bottom aligned to existing threshold; top at Z2250, 50 hidden above the 2200 opening | Silver | Retain concealed upper overlap, not a taller aperture |
| Meeting seam | Apparent 1 centre line at X0; no extra geometric gap that expands pair width | Navy | Texture/normal at mating edges |
| Aqua etched accent | 2 wide line immediately beside meeting edge, full visible height | Aqua | Texture, not raised strip |
| Brushed grain | Broad restrained vertical metal finish over each leaf | Silver | Roughness/normal texture |

Retain existing slide directions and travel; no new motion timing is specified.
There are no handles on these leaves.

**2000s Aero read:** softly edged silver panels with one fine aqua meeting line add a manufactured finish without decorative clutter.

### Lift_Car — material/detail proposal

**Brief's requested interior:** 3.70 × 3.55 × 2.60.
**Current sheet's stamped overall:** 3.70 × 3.55 × 2.90.
The unresolved footprint distinction is recorded above; **do not grow the source shell**.
The following parts are sized to fit the requested interior and must be checked against the actual inside surfaces before modelling.
Use actual inside W/D in surface-relative coordinates below; the proposed panel and ceiling sizes may need to reduce to fit, never increase the outer shell.

The large view deliberately removes the front shell for visibility; the small front elevation retains the **1.40 × 2.20 entry**.
It is not an open-front 3.7 m-wide lift.

| Part / feature | Size and position | Material | Build |
|---|---|---|---|
| Wall facing | Apply within existing wall volume, no added exterior thickness; broad internal finish corners R20 where the source permits | Pearl satin | Existing walls with inset facing treatment |
| Back mirror panel | Proposed 2800 W × 1900 H, X0; bottom Z400, top Z2300; flush into rear wall | Mirror silver | One plane/material region; broad simple reflected forms in concept only |
| Back rail | Ø35; horizontal span 3000, centre Z950, centreline 60 in front of actual rear wall | Brushed silver | Cylinder; R40 rounded returns into wall at each end |
| Side rail | Right wall, centreline 60 inboard, Z950; runs from Y650 to 100 before rear wall; R40 returns | Brushed silver | Same simple rail profile, not a streamed/animated object |
| Ceiling diffuser | Proposed 3200 W × 2900 D × 12 H, centred in actual ceiling; face at interior ceiling plane, body recessed into existing ceiling | Frosted milk/aqua | One large rounded panel, R40 corners |
| Diffuser frame | 30 wide border around pane, contained in ceiling surface | Silver | Simple inset trim, no new ceiling height |
| Ceiling glow | Uniform soft aqua-white, no bare LED points or neon lines | Frosted aqua / pearl emission | Material appearance, lighting implementation not specified |
| Operating panel | 160 W × 560 H × 20 D; on left wall at Y550, centre Z1180; recess into wall to keep face flush | Brushed silver, pearl inserts | One R15 panel |
| Floor buttons | Three Ø28 discs, vertical centres Z1040, Z1130, Z1220 on panel centreline | Pearl centres, aqua rings 2 wide | Simple discs; ring and numeric legends in texture |
| Small readout | 60 W × 35 H, centre Z1390 on panel | Smoke + aqua | Flat texture/emission |
| Door labels | `開 / OPEN` and `閉 / CLOSE`, two 60 W × 24 H label regions near panel bottom, centres Z950 and Z990 | Pearl fields, navy type | Flat texture, re-typeset exact Japanese glyphs |
| Floor finish | Existing floor top/threshold level; no new raised sill | Mist with restrained cool shading | Material/roughness only |
| Panel shutlines | 1 wide, restrained, at existing panel joins | Navy | Texture/normal, not extra wall partitions |

Hardware positions are visual proposals; retain existing functional controls, pivot, occupancy and navigation arrangements.
The operating panel is separated from the side rail in depth so their proposed surface regions do not overlap.

**2000s Aero read:** pearl walls, a mirror-silver back, rounded handrail and diffuse aqua ceiling bring the clean office-appliance family into the cabin.

## Visual review and static validation

- Visually inspected all three generated images; selected attempt 1 and corrected attempt 3.
- Checked the paintings for the required named studies, A/B monitor distinction, mouse/pad, floor tower, front/side insets, material leaders, bilingual-label locations and kept dimension labels.
- Corrected the car's front inset through the image tool; no scripted image editing was used.
- Read PNG metadata with PowerShell `Add-Type -AssemblyName System.Drawing` and `[System.Drawing.Image]::FromFile(...)`: both selected files are 1672 × 941, with no resampling.
- Compared saved PNGs against their selected source files using `Get-FileHash -Algorithm SHA256 -LiteralPath <file>`: both pairs were byte-identical.
- Parsed the exact prompt log using `Get-Content -Raw -Encoding UTF8 -LiteralPath <file> | ConvertFrom-Json`: three tries, two selected outputs.
- Ran scoped `git -c filter.lfs.process= -c filter.lfs.clean= -c filter.lfs.required=false status --short -- <four part-2 paths>`: the three deliverables and the permitted `part2_` prompt log are present as new files.
- The paintings' fine glyphs and keycap highlights are illustrative; rebuild final label artwork as textures from the exact wording here.
- No game files, authored assets, runtime behaviour, setup or network contracts change, so no system-document update or Unity validation is needed for this concept-only deliverable.

**Next:** Alex reviews the visual proposals; resolve the car inside/outside footprint and verify the roof source envelope before remodelling.
