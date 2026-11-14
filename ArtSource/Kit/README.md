# Office snap kit

**Status:** Current · **Last updated:** 2026-11-13

The modular kit for office floors: slabs, walls, doorway, window bays, façade columns, glass partitions, the spiral stair, its locked door and a ceiling light.
All pieces come from one script, [kit_pieces.py](kit_pieces.py), so they share one set of grid, height and material values.
They are modelled to the office direction Alex approved on 2026-11-12, **White Trays + Cobalt Portal** ([decision](concepts/Office/DECISION.md), [concepts](concepts/Office/contact_sheet.png)).
`Assets/_Project/Editor/Art/KitImport.cs` brings them into Unity; the pieces added on 2026-11-12 have not been through it yet.

**The game is moving from twin towers to one office labyrinth** (Alex, 2026-11-12). The kit's detail direction for it is round 5 ([decision](concepts/Kit_Detail/DECISION_r5.md), [concepts](concepts/Kit_Detail/contact_sheet_r5.png), [sizes](concepts/Kit_Detail/README_r5.md)), and the pieces in [Labyrinth pieces](#labyrinth-pieces-round-5) below build it. The tower pieces stay: nothing here removes them, and the game docs (GDD, TOWER.md) still describe the towers until the owner changes them.

## Build

```bash
bash ArtSource/Kit/build_all.sh <path to the blender-modeling skill>
blender -b --factory-startup -P ArtSource/Kit/assembly_test.py -- ArtSource/Kit/build <out folder>
```

`build_all.sh` builds, checks, exports and reads back every piece into `build/` and `export/<piece>/` (one FBX and a manifest each).
`assembly_test.py` snaps the pieces together by the convention alone, fires rays along every seam, prints PASS or FAIL per check and renders the result.
`build/` and `export/` are outputs; rebuild them rather than editing them.

## Snap convention

In Unity terms, 1 unit = 1 m:

| Rule | Value |
|---|---|
| Pivot | The piece's **front-left snap corner, at floor level** (the top of the slab) |
| Run | Along **+X** from the pivot |
| Thickness or depth | Toward **−Z** from the grid line. On the façade that is into the building, as in the floor templates |
| Inside of a room | On each piece's −Z side. A side wall running toward −Z is placed at its far end and turned −90° |
| Grid | 4 m bays, 2 m infills |
| Floor to floor | 4.5 m |
| Slab | 0.2 m thick, hanging below floor level; carpet on top, ceiling underneath |
| Floor to soffit | 4.3 m; walls and columns stop exactly there |
| Stair pieces | Pivot at the stair centre, floor level, 0° toward +Z, clockwise |
| Turns | Multiples of 90° only (cage panels: 15° + 30° steps) |

Corners close without a dedicated wall corner piece: because thickness goes inward, two walls meeting at a corner overlap on the inside.
The façade's end column must be `Column_CornerL` or `Column_CornerR`, not `Column_Facade`. A centred column stands 0.2 m outside the side wall; `assembly_test.py` found this.

## Pieces

Notes for the programmer: **Structural** pieces never move and can be merged (they go under the template's Never Moves list). **Visual only** pieces have no collider.

| Piece | Size (m) | Role | Collider | Materials |
|---|---|---|---|---|
| `Slab_4x4`, `Slab_2x4`, `Slab_4x2`, `Slab_2x2` | bay × bay × 0.2 | Structural | Box | Floor_Carpet, Ceiling_Tile, Wall_PaperWhite |
| `Slab_Stairwell` | 12 × 8 × 0.2, round hole r 2.65 | Structural; stair centre at local (6, 0, −4) | Use the mesh (not convex) | as slabs |
| `Wall_Solid_4m`, `Wall_Solid_2m` | run × 0.2 × 4.3 | Structural, opaque | Box | Wall_PaperWhite |
| `Wall_Door_4m` | 4 × 0.2 × 4.3; opening 1.2 × 2.4, centred | Structural; three parts (Left, Right, Head) | Box per part | Wall_PaperWhite |
| `Door_Leaf` | 1.18 × 0.04 × 2.38 | Moving; pivot on the hinge axis. In a door wall: (1.41, 0, −0.1) | Box | Door_Leaf |
| `Window_4m`, `Window_2m` | run × 0.2 × 4.3 | Structural sill and rails; pane is a separate object | Sill: box. Rails, pane: none | Wall_PaperWhite, Frame_Aluminium, Glass_Clear |
| `Column_Facade` | 0.4 × 0.24 × 4.3, centred on a grid point | Structural | Box | Wall_PaperWhite |
| `Column_CornerL`, `Column_CornerR` | 0.42 × 0.24 × 4.3, inside the corner | Structural | Box | Wall_PaperWhite |
| `Partition_Glass_4m`, `Partition_Glass_2m` | run × 0.1 × 2.5 | Fixed, see-through | Box over the whole wall (on the frame) | Frame_Aluminium, Glass_Clear |
| `Stair_Flight` | 5.2 × 5.2 × 4.75, one full turn | Structural | Use the mesh (not convex), as the template does now | Stair_Aluminium |
| `Stair_Column` | r 0.8 × 4.5 | Structural | Hull | Stair_Aluminium |
| `Stair_CagePanel` | 30° arc at r 2.75, 0.05 thick × 4.3 (bounds 1.44 × 0.14) | Fixed, see-through; 10 copies at 15°, 45° … 285°. Curved, meeting edge to edge | Hull | Glass_Clear |
| `Stair_Steps` | 5.2 × 5.2 × 4.36: 24 open treads, 0.05 thick, plus a handrail at r 2.63 | **Visual only**; place with every `Stair_Flight` and turn the flight's renderer off. Tread tops sit on the ramp line at their middles | None | Stair_Aluminium |
| `Stair_LockedDoor` | 1.75 × 0.2 × 4.2 (0.22 with lock plates) | Moving (networked, breakable). Pivot bottom centre: the same envelope as the greybox `LockedDoor` body, so it can replace that cube | Box | Door_LockedRed, Trim_Navy |
| `Wall_Solid_4m_Cobalt`, `Wall_Solid_2m_Cobalt` | as `Wall_Solid_*` | Structural; main-floor face (the grid-line face, +Z) Accent_Cobalt | Box | Wall_PaperWhite, Trim_Navy, Accent_Cobalt |
| `Wall_Solid_4m_PortalHead`, `Wall_Door_4m_PortalHead` | as `Wall_Solid_4m`, `Wall_Door_4m` | Structural; cobalt band above 3.3 m on the grid-line face | as their plain pieces | Wall_PaperWhite, Trim_Navy, Accent_Cobalt |
| `Ceiling_LightStrip` | 1.5 × 0.2 × 0.04 | **Visual only**; pivot top centre, against the soffit | None | Wall_PaperWhite, Light_Strip |

**Window panes have no collider**, matching the repo's open bays: players can still be blown out of a window. If panes should stop players, that is a gameplay decision, not an art one.
Glass partitions and cage panels do stop players, as the templates' do now. Whether glass stops projectiles (the GDD says it doesn't) is a layer setting for the programmer.

Colours are the art bible's swatches. Floor_Carpet is now #646E86, the approved concepts' darker grey-blue; it still hasn't been tested against dark suits seen from the floor above ([Greybox README](../Greybox/README.md)).
UVs are box-mapped at 1 m per UV unit on every piece, so tiling materials line up across pieces.

### The approved direction, piece by piece

- **Walls** have an 80 mm navy shadow-gap skirting and a 40 mm navy reveal under the soffit on both faces, 15 mm deep; doorways a 30 mm reveal round the opening. The lines run through the snap ends, so they carry on unbroken across a run.
- **The cobalt portal** goes on the spine wall behind the stair (x 8–20 on the 28 m floor): `Wall_Solid_4m_Cobalt`, `Wall_Door_4m_PortalHead` (the STORE door), `Wall_Solid_4m_Cobalt`. That gives cobalt legs, a white field with the door, and a cobalt head band. Use it once per floor; elsewhere walls stay white. The oversized floor number on the white field is a decal for the room-set layer.
- **Glass partitions** have navy rails, like the window rails.
- **The stair** has a white column, open aluminium treads with a handrail over the ramp (the ramp stays the walking surface, hidden), and a curved glass drum.
- **Ceiling lights:** four `Ceiling_LightStrip` per 4 × 4 m bay, centred 1.25 m and 2.75 m into the bay along both axes, so each covers three whole 0.5 m tiles. Keep them 3 m clear of the stair centre.
- **Textures:** [textures/make_textures.py](textures/make_textures.py) draws `Ceiling_Tile.png` (0.5 m grid) and `Floor_Carpet.png` (quarter-turned 0.5 m carpet tiles). Each covers 1 m, one UV unit, so they tile across pieces on the grid.
- **New material slots:** `Accent_Cobalt` #244FB9, `Light_Strip` (white, emissive), `Door_LockedRed` #C0281E (the one warm gameplay accent), alongside `Trim_Navy` #111B38.

## Labyrinth pieces (round 5)

Alex's picks: corridors A (capsule loop), junctions B (split X), rooms A (capsule meeting room), atrium B (folded forum), glass B (transparency cuts); no floor light trails. Finishes are glossy: white lacquer, cobalt, inset silver, a polished pale floor (still named `Floor_Carpet` for the import mapping) and a flat white ceiling with no tile texture.

| Piece | Size (m) | Role | Collider | Materials |
|---|---|---|---|---|
| `Wall_Return_2x2`, `Wall_Return_2x2_Cobalt` | 2 × 2 cell, 0.2 thick × 4.3; outer radius 1.0 | Structural. The rounded outside corner of a maze island: snap ends at (2, 0) running +X and (0, −2) running −Z, like a +X wall and a side wall. Turn 90° per corner. `_Cobalt` paints the curved face | Box per straight leg, three hulls on the curve | Wall_PaperWhite, Trim_Navy (+ Accent_Cobalt) |
| `Wall_Niche_4m` | 4 × 0.2 × 4.3 | Structural; a 0.6 × 3.4 m cobalt capsule niche, 0.06 deep, centred on the grid-line face. The corridor landmark | Box | Wall_PaperWhite, Trim_Navy, Accent_Cobalt |
| `Counter_Capsule_2m` | 2 × 1 × 1.1, round ends | Fixed crouch cover (1.1 m). Pivot bottom centre | Hull | Wall_PaperWhite |
| `Cover_Block_2m` | 2 × 0.6 × 1.1, bevelled | Fixed crouch cover. Pivot bottom centre | Box | Wall_PaperWhite |
| `Partition_Frost_4m`, `Partition_Frost_2m` | as `Partition_Glass_*` | Fixed. Frosted below 0.35 m and above 2.1 m, clear between, so a standing figure still reads through two panes | Box over the whole wall, as `Partition_Glass_*` | Trim_Navy, Glass_Clear, Glass_Frosted |
| `Stair_Straight_2m` | 2 × 7.2 × 4.5 (5.6 with guards) | Structural. 24 risers of 0.1875 m, 0.3 m goings, rising toward −Z from a front-left pivot at floor level; a folded white plate with silver treads and frameless glass guards each side | Hull along the nosings (32°); a hull per guard | Wall_PaperWhite, Frame_Aluminium, Glass_Clear |
| `Ramp_2x4` | 2 × 4 × 1.0 | Structural. 1:4, rising toward −Z, polished top | Hull | Wall_PaperWhite, Floor_Carpet |
| `Guard_Glass_4m`, `Guard_Glass_2m` | run × 0.06 × 1.1 | Fixed. Frameless glass in a 0.12 m silver shoe, for mezzanine and void edges; sits just inside the slab edge | Box on shoe and glass | Frame_Aluminium, Glass_Clear |
| `Slab_4x4_LightLine` | 4 × 4 × 0.2 | Structural. A slab with a 0.2 m lit slot, 0.08 deep, along X under its middle; the slot runs through both ends, so slabs in a row make one line along a route | Box | Floor_Carpet, Ceiling_Tile, Wall_PaperWhite, Light_Strip |

New material slot: `Glass_Frosted` #E6ECF2 (80 % opaque in Unity).

### Fix list, 2026-11-13 (Alex: "still blockout-y")

| Piece / change | What | Collider |
|---|---|---|
| Wall ends (`Wall_Solid_*`, `Wall_Door_4m*` outer ends, `Wall_Niche_4m`) | 12 mm 45° chamfers on the vertical end edges, both faces, in the wall's own gloss white: every 4 m panel meets the next in a V joint that catches the highlight. Snap ends stay flat except the outer 12 mm | Unchanged |
| `Stair_Straight_2m` | Now two folded white stringers with 24 separate 60 mm white treads, each with a 35 mm silver nosing, open underneath; glass guards as before | Hull along the nosings, unchanged |
| `Sign_Zone_L`, `_R`, `_C`, `_H`, `_A` | 2.4 × 3.2 m cobalt panel from 0.6 m to 3.8 m: a 1.7 m white letter, the department name, a hairline rule and a line of microtype (built-in font). 14 mm proud. Pivot at floor level at the panel's low-X edge on the wall face; it reads from +Z | None (visual) |
| `Desk_Sculpt` | 1.6 × 0.8 × 0.74 m waterfall desk, gloss white with a recessed cobalt modesty panel. Pivot bottom centre | Box |
| `Chair_Pod` | Cobalt shell seat and back on a silver swivel stem and disc, seat 0.46 m. Pivot bottom centre, faces +Z | Hull (loose prop: physics is the props lane's) |

### Detail pass, 2026-11-13 (Alex: "follow these", his atrium, office-floor and curved meeting room plates)

Alex's calls for this pass: carpet in work areas with polished walkways, some planters, keep the labyrinth layout.

| Piece | What | Collider |
|---|---|---|
| `Column_Graphic`, `Column_Graphic_Tall` | 0.8 m square column (4.3 / 8.8 m), rounded corners, a cobalt supergraphic band climbing round two faces, a big zone number and four wayfinding lines in navy | Box |
| `Workstation_Bench` | 2.4 × 1.6 m back-to-back bench: four monitors, keyboards, a 1.15 m divider (crouch cover), two pedestals, silver ends | Box |
| `Chair_Task` | Navy task chair on a silver five-star base, faces +Z | Hull (loose prop) |
| `Storage_Low` | 1.6 × 0.45 × 1.0 m white cabinets with navy joints | Box |
| `Copier` | The bible's photocopier: pale body, tray joints, dark lid, silver panel | Box |
| `Desk_Reception` | 4 m reception desk, KELLER MORROW / ATTORNEYS AT LAW, cobalt end block | Box |
| `Guard_Rail_4m`, `_2m` | Glass balustrade between slim silver posts with a 1.1 m handrail | Box on the frame |
| `Partition_Header_4m`, `_2m`, `Partition_Header_Curve_2x2` | Glass meeting-room walls with a white header band from 2.5 m to the soffit; the curve bends round 1.5 m in a 2 × 2 m cell (Alex's concept A) | Box; hulls on the curve |
| `Light_Pendant_Linear` | 2.4 m linear pendant, glowing underside, hung 0.8 m on two wires | None |
| `Carpet_Overlay_4x4` | 8 mm dark carpet layer (`Carpet_Dark`, tile texture) on a slab's polished top | None |
| `Ceiling_Grid_4x4` | Tiled ceiling panel (`Ceiling_Grid`, tile texture) under a soffit | None |
| `Planter_Box` | White box planter with leaf blades (`Plant_Leaf`) | Box on the pot |

`KitOffice.unity` now has: the curved header-glass meeting room in zone C on carpet under a tile grid; tall graphic columns at three atrium corners and two in the window corridor; the reception desk facing the window corridor; workstation benches with task chairs, storage, copiers and planters on carpet with pendants upstairs and in zone R; the posted balustrade round the atrium. The DevMirror and the floor bakes (bounce light, reflection probes) are now part of the scene build, so a rebuild keeps them.

**SSAO** on `Assets/Settings/PC_Renderer.asset`: intensity 0.4 → 0.9, radius 0.3 → 0.45 m, samples medium → high, so corners and wall bases darken and pieces look built rather than placed. A first try at 1.2 / 0.6 m drew grainy dark halos round the islands.
`KitOffice.unity` now carries a sign per zone, desks and chairs in the meeting room and upstairs, and a second counter in the junction. The unsaved scene it replaced is kept as `KitOffice_PreFixList.unity`.

**Kept, not changed, and flagged in README_r5:** the partition's whole-wall collider (breakable glass and walk-through openings belong to the shooting lane), the stair cage and its silver handrail, the full-height red locked door, the spiral as a way up, the 0.6 m window sill. Zone letters (L, R, C, H, A) and sky prints are a decal pass, not modelled.
[labyrinth_scene.py](labyrinth_scene.py) snaps the new pieces into a 16 × 16 m maze section and renders it to `review/labyrinth/`.
In Unity (2026-11-13) the 12 pieces were imported on their own, without rebuilding KitTest: all at rotation 0 and scale 1, sizes matching to 1 mm, materials remapped; the stair's sloped guards and the curved return get mesh colliders, the rest boxes. The same section laid out from the prefabs in a temporary scene is captured in `review/unity_labyrinth/`. Not yet walked in Play Mode.

### Japanese corporate mix, 2026-11-13 (Alex: "do a mix of all 3")

The office is now AONAGI (青凪総合サービス), a Japanese firm. The mix of the current look, Aero (A) and Soft Club (B) is written down in [concepts/Japanese_Aero/DECISION.md](concepts/Japanese_Aero/DECISION.md): palette, what each look contributes, and the kept gameplay rules. Sizes and sign copy are in that folder's README.

| Piece | What | Collider |
|---|---|---|
| `Water_Server`, `Umbrella_Stand`, `Vending_Machine`, `Slipper_Cabinet` | The office's daily life; tall ones only against opaque walls | Box |
| `Phone_Desk`, `Doc_Tray` | White desk phone and document tray, placed on the bench ends | None |
| `Logo_Aonagi`, `Sign_LED_Reception` | Ring logo with the red hanko dot; dot-matrix reception sign | None |
| `Water_Wall` | 1.8 × 2.7 m silver-framed water cassette. The liquid is drawn by the `Close the Deal/Aero Water Pane` shader ([AeroWater.shader](../../Assets/_Project/Art/Water/AeroWater.shader)): a fake tank interior seen through the glass with parallax: sharp caustic filaments on the back wall, colour deepening with depth and path length, swaying light shafts, bubbles at two depths, the bright underside of the surface seen from below, a travelling wave line with empty glass above, two glass streaks. Geometry stays one flat panel | Box |

**Supergraphics, 2026-11-13** (Alex: lettering "larger and more character ... think Mirror's Edge"). `Sign_Zone_*` are no longer panels: each is a 3.2 m wide graphic painted onto the wall, a 2.6 m slanted cobalt zone letter over a leaning aqua block, a cobalt speed stripe across the full width, the department name 0.46 m high in Japanese with English under it, and a navy floor tag. All kit text now comes out at its stated height: `kit_pieces.py` had assumed Noto's capitals are 0.72 of the font size, Blender loads them at 0.257, so every label (signs, columns, reception desk, logo, LED sign, prop labels) was 36 % of its intended size. `text_mesh` also takes a stroke weight and a slant now, and `placed_text` condenses a line that would overrun its surface.

Existing pieces picked up the palette (pearl walls, paler carpet, aqua frost, aqua/mist column skins with 1F/2F wayfinding, bilingual zone signs). CJK text uses Blender's bundled Noto Sans CJK (OFL). `KitImport.cs` gives `Water_Wall` the water shader and leaves its colours alone on re-import, so Inspector tuning survives.

## Checks run

- Every piece passes `mdl.mjs check` with no errors (2026-11-12, 26 pieces). The warnings are long thin faces: on thin boxes (rails, panes, slab edges), and where the shadow-gap cuts slice the flat-shaded walls. They are flat faces, so they shade correctly; they would only matter for automatic LODs, which the kit doesn't use. The locked door's lock plates touch its faces without a boolean union (a "faces cut through each other" warning).
- `mdl.mjs verify` passes on every FBX: sizes, triangles and materials read back as built.
- `assembly_test.py` passes all 9 checks: façade, side wall, corner, floor and ceiling continuous with no gaps; walls meeting the soffit; door head closed; the flight rising one floor per turn and arriving flush with the next floor.
- [interior_scene.py](interior_scene.py) dresses the whole floor in the approved pieces and renders it to `review/interior/`, including the concepts' stair close-up and rival views.
- In Unity, `Close the Deal > Art > Build Kit Office Scene` ([KitOfficeScene.cs](../../Assets/_Project/Editor/Art/KitOfficeScene.cs)) builds `Assets/_Project/Scenes/KitOffice.unity` as the **office labyrinth** (2026-11-13; the tower floor it used to build is in git history, and the last tower version with unsaved work is kept as `KitOffice_PreLabyrinth.unity`). It is 32 × 24 m on two storeys: a window corridor along the glass façade (north), zone L's corridor loop round a rounded-corner island with the cobalt niche (west), zone H's double-height atrium with the straight stair and glass-guarded upper floor (middle), zone C's meeting room behind frosted glass (south-west), zone A's glass cuts (south), and zone R's split junction of two offset islands and block cover (east). It adds the player rig and the office look; players spawn at the two ends of the window corridor. Captures are in `review/unity_office/` (L1–L7). Checked 2026-11-13: the stair's walking surface rises 0.25 → 4.50 m and meets the upper floor flush; the void guards, island walls and 1.1 m cover stop rays; in Play Mode a hosted player spawned standing on the floor, though in the atrium rather than at a corridor spawn (not yet explained). Walking the routes, the stair and the upper floor has not been tried.
- **Not yet checked in Unity** for the 2026-11-12 pieces: `KitImport.cs` lists them, their materials and textures, but it hasn't been run (it rebuilds `KitTest.unity`, which was open with unsaved work). Import scale, colliders, the emissive strip and the textures under the game's lighting are unverified.

## What the floor template would change to match the grid

The kit snaps to a 4 m grid; the current greybox template doesn't fully sit on one. These are proposals for whoever owns the floor template builder ([TOWER.md](../../docs/systems/TOWER.md)), not changes made here:

1. **Main floor depth 14 m = a 2 m front strip plus three 4 m rows.** The strip goes at the windows, so the stairwell module (z −6 to −14) lines up with the grid.
2. **Stair centre at z −10.0, not −10.05.** The template's 1.2 m walkway behind the cage becomes 1.25 m.
3. **Spine rooms 8 / 12 / 8 m instead of 30 / 40 / 30 % (8.4 / 11.2 / 8.4 m),** so the dividers land on grid lines at x ±6.
4. **Spine doorways 1.2 × 2.4 m with a wall above,** instead of the template's full-height 1.2 m slots. The 2.4 m height is a proposal.
5. **Façade end columns flush with the side walls,** which the template already does.
