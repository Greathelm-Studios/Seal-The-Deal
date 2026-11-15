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
`KitOffice.unity` now carries a sign per zone, desks and chairs in the meeting room and upstairs, and a second counter in the junction. The scene it replaced was kept as `KitOffice_PreFixList.unity` until 2026-11-14 and is now only in git history.

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

### Lift, rooftop pool and loose props, 2026-11-13

Agreed with the elevator session (spec: side-session notes, `elevator-rooftop-spec.md`); Alex: "rooftop, elevator beside reception", "make sure its an open roof", "added a pool".

| Piece | What | Collider |
|---|---|---|
| `Lift_Door_Frame_4m_1F`, `_RF` | 4 m pearl landing wall, slab line to soffit, centred 1.4 x 2.2 m opening with a silver portal, a dark indicator ("1F" / "RF 屋上") in aqua, a call panel with up/down arrows | Boxes on left, right, head |
| `Lift_Door_Panel` | One 0.75 x 2.25 m silver leaf; pivot at the bottom of the meeting edge; opens by sliding 0.75 m along its +X. Sits 0.225 m behind the frame face, inside the shaft | Box |
| `Lift_Shaft_Wall_4m` | Full-storey glass shaft wall (slab line to soffit) with a silver base band, head rail and end posts | Box on the frame |
| `Lift_Car` | 3.7 x 3.55 x 2.6 m capsule car, open front (+Z), polished floor with its top at the pivot, pearl sides, glass back with a 1.0 m handrail, light-panel ceiling, header over the opening at 2.2 m | Boxes: floor, sides, back, ceiling, header |
| `Pool_Raised_8x4` | Raised 8 x 4 m basin: 0.5 m pearl rim 0.4 m wide with silver coping, two steps on the front long side, aqua floor, water 0.4 m up (`Pool_Water`, the Aero Water Pane shader's top-down mode) | Boxes on rim and steps; none on the water |
| `Monitor_Flat`, `Keyboard` | The bench's monitors and keyboards as their own pieces | Box |

`KitOffice.unity`: the shaft is the cell x 20-24, z -4 to -8, with doors west into the atrium, stops at 1F and the roof (y 9), and glass on all four sides on the upper floor. The level-1 and roof slabs, the carpet and the ceiling are left out of that cell, the reception desk moved 1 m west and the upstairs planter moved out. `KitOfficeScene.Lift()` places the car and leaves as `Lift/Lift_Car`, `Door_1F_A/B`, `Door_RF_A/B`; the elevator session's `KitOfficeRoof.AddTo` (called at the end of the build) wires them and builds the roof range, its guard rails and the pool.

**Loose (pick up and throw; convex box or hull colliders):** `Monitor_Flat`, `Keyboard`, `Phone_Desk`, `Doc_Tray`, `Chair_Task`, `Chair_Pod`. **Fixed:** everything else, including the bench, `Planter_Box`, `Copier`, `Vending_Machine`, `Storage_Low`, `Water_Server`, the lift pieces (the car and leaves are moved by code, not carried) and the pool. KitOffice places 115 loose kit props: 28 monitors, 28 keyboards, 14 phones, 14 trays, 28 task chairs, 3 pod chairs (the roof range adds its own). That is far over the 16-per-floor netcode cap in docs/systems/PROPS.md: which of them become host-simulated props is the props lane's call.

### Glass and structure, 2026-11-14 (Alex: the AEROSET look, Persona 3's graphic identity "to an extent")

Lessons taken, not assets: glass crossed by thin dense lines, exposed structure, a near-mirror white floor, a stronger sun. Alex's calls: the floor stays white, the sky is unchanged, no bubbles or ivy.

| Piece / change | What | Collider |
|---|---|---|
| `Wall_Curtain_4m` | Floor-to-soffit clear glass on a navy grid: base and head rails, mullions every 1 m, transoms at 2.6 and 3.45 m (nothing across a standing figure). Same footprint as `Wall_Solid_4m` | Box on the frame |
| `Ceiling_Beams_4x4` | Two silver beams along X either side of the slab's light line and a lighter cross beam, hung under a soffit | None |
| `Window_4m`, `_2m` | Navy mullions every metre and the same two transoms | Unchanged |
| Materials | `Floor_Carpet` smoothness 0.85 → 0.96 (still white), `Wall_PaperWhite` 0.70 → 0.85 | |

In `KitOffice.unity` every outer bay with nothing hung on or stood against it is now a curtain wall (26 bays); bays carrying signs, the copier, vending machine, slipper cabinet, storage or the DevMirror stay solid white (`KitOfficeScene.OuterWall`). Beams hang under every soffit outside the tiled work areas, the shaft and the atrium's open middle (55 cells). `OfficeLook.cs`: sun 3.2 → 4.2, ambient 0.6 → 0.5 and a third nearer neutral, a whiter colour filter, saturation 6 → 16, bloom 0.55 → 0.75; the sky is untouched. The scene's unsaved state before this rebuild is kept as `KitOffice_PreAeroset.unity`.

Interior, the same day: `Partition_Glass_*`, `Partition_Frost_*` and `Partition_Header_*` carry navy mullions every metre (stopping under the header band); plain `Wall_Solid_*` walls carry thin navy joint lines on the same grid on both faces (verticals every metre, lines at 2.6 and 3.45 m, 2-3 mm proud), so walls kept solid for cover still read as part of the grid. The cobalt, portal-head, door and niche walls are unchanged. The A/R boundary wall at x 20, z -20 to -24 is now a curtain wall. Unsaved scene state before that rebuild: `KitOffice_PreInteriorGlass.unity`.

### Sign system as textures, 2026-11-15 (Alex: signs "look blockouty ... fonts are basic too ... dont have it modelled, either textured or a decal")

Typefaces, picked by Alex: **Michroma** (wide geometric Latin) and **M PLUS 1** (Japanese, variable weight), both SIL Open Font License, in [fonts/](fonts/) with their licence files. [signs/make_signs.py](signs/make_signs.py) draws every zone sign from one layout spec to a transparent PNG (3.2 x 4.0 m at 320 px/m, supersampled): a 2.05 m Michroma zone letter in a cobalt gradient, a glossy aqua band with the English name and floor code in white, a cobalt hairline, the Japanese name in M PLUS 1 weight 800, and a microtype line. All layers are in one image, so nothing z-fights. The spec sheet is `signs/review/sign_spec.png`.

`Assets/_Project/Editor/Art/KitSigns.cs` imports the textures to `Assets/_Project/Art/Signs/`, makes one transparent URP Lit material each (smoothness 0.85, like the walls) and places each sign as a quad 6 mm off its wall, using the kit's wall-hung pivot. `KitOfficeScene` places the five through it; the modelled `Sign_Zone_*` pieces stay in the kit but are no longer placed, and the mesh-text path (`text_mesh`) is on its way out. Then every other label too (Alex: "convert the rest"): the column wayfinding plates (floor number, arrow pictograms, zone-letter tiles, department names), the company name on the reception desk and the AONAGI wall logo (the ring stays modelled: it is a shape, not text), dot-matrix LED labels for the reception sign and the lift indicators (sampled onto a 6.5 mm LED grid, emissive), and the vending machine, slipper cabinet and water server labels. `make_signs.py` writes them as `Label_*.png` (review sheet `signs/review/label_sheet.png`); `KitSigns.Decorate` adds each piece's quads wherever the builder places it (the `Labels` table, in piece-local metres). `kit_pieces.py` no longer models any text (`MESH_TEXT = False`). Sign and label materials have specular and reflections off: on a transparent quad those showed the clear area as a pale box; the wall's own gloss shows through instead. Unsaved scene state before the label rebuild: `KitOffice_PreLabels.unity`. Unsaved scene state before this rebuild: `KitOffice_PreSignTextures.unity`.

### Ribbons and lightboxes, 2026-11-15 (Alex: PANLINE reference, "matches the visuals we want, but less realistic")

Lessons only, simplified to clean colour plus gloss; the palette, white floor, sky and gentle bloom stay.

| Piece | What | Collider |
|---|---|---|
| `Ribbon_Floor_16m` | A glossy cobalt ribbon 0.5 m wide inlaid 3 mm proud in the floor, with a thin aqua line beside it, swinging 0.8 m either side of a 4 m corridor's middle over one slow 16 m cosine, so pieces chain without a seam. Pivot at its NW corner on the floor | None |
| `Ribbon_Ceiling_16m` | Its partner overhead: a 40 mm lacquered cobalt band 0.2 m under the soffit (below the beams), following the same swing, with a glowing strip along each edge. Pivot at the soffit | None |
| `Lightbox_Wall_Wave`, `Lightbox_Wall_Logo` | A 3.6 x 3.0 m backlit lightbox, rounded silver frame round a recessed diffuser, 80 mm deep. The graphic is a glowing texture (`make_signs.py`: `Lightbox_Wave`, `Lightbox_Logo`, emission 1.1), never modelled | Box |

`KitOffice.unity`: ribbons run the window corridor on both floors (floor and ceiling, two pieces each); the logo lightbox replaces `Logo_Aonagi` and `Sign_LED_Reception` on the reception wall (it carries the ring, the hanko dot, both names and the reception line); wave lightboxes hang on zone R's side of the cobalt wall and in meeting room C. A first try with a full swing every 4 m read as tight loops in perspective, hence 16 m pieces. Unsaved scene state before these rebuilds: `KitOffice_PreRibbons.unity`, `KitOffice_PreRibbons16.unity`.

Then the floor and ceiling, the same day: `Floor_Carpet` now carries a texture (`textures/make_textures.py` `floor_tile`), 1 m white tiles in the floor's own colour with 6 mm cool-grey joints, still at smoothness 0.96; `Ceiling_Panel_4x4` is a floating rounded white panel (3.6 m, 0.35 m corners, 0.16-0.22 m under the soffit) with a glowing cove ring on the soffit that shows in the 0.4 m gap to the next panel. In `KitOffice.unity` the panels replace the beams in every beam cell off the window corridor (39 cells); the corridor keeps its beams under the ceiling ribbon. Unsaved scene state before that rebuild: `KitOffice_PreFloorCeiling.unity`.

### The L2 floor plan, 2026-11-15 (Alex: "this works", then "yes, send it")

`KitOffice.unity` is now the approved **L2 twin-core court** ([decision](concepts/Layout/DECISION_L2.md), [plan](concepts/Layout/L2/plan.png)). It replaces the labyrinth, which Alex found "too confusing" with things "randomly placed" ([diagnosis](concepts/Layout/CURRENT_DIAGNOSIS.md)). The plate is 40 × 32 m on two storeys and has:
- a double-height court in the middle (x 16-24, y 10-22) with the feature flight rising west along its north side, a bridge strip north and a canopy strip south on the upper floor, and glass guards round the void;
- the lift on the north façade (x 18-22), its doors facing south through the lobby onto the court;
- two cores, the east one the west one turned 180°. Each has an enclosed return stair (E1, E2), print and copy (or server and archive), a kitchen (or coffee kitchen), a washroom and a sealed riser, and every room has two doors;
- a continuous ring round both cores, with the cobalt ribbons running down its west and east sides;
- office wings at both ends (legal conference and back office; review conference and claims back office), each room with two doors, firm receptions with logo lightboxes in opposite corners, and window halls with desks north and south.

`KitOfficeScene.cs` writes the plan in the drawings' own coordinates (x east, y south; Unity Z = −y) and builds one firm's half, then turns it for the other. Walls are tiled from 4 m and 2 m pieces, and every door sits on the 2 m grid. That is why three new pieces exist: `Wall_Door_2m` (a 1.2 m doorway between 0.4 m jambs), `Partition_Glass_Door_2m` (the same in the glass partition, with navy jambs and a head rail) and `Stair_Straight_Half_2m` (12 risers over 3.6 m). Two half flights and a scaled slab landing make each enclosed return stair.

The walls repeat on both storeys; the fit-out does not:
- **Ground floor:** the rooms hold counters, pod chairs, the vending machine and the reception desks.
- **Upper floor:** the wing rooms are open-plan work rooms with benches, and reception is a lounge.

**Ceilings, per 4 m cell:**
- The carpeted rooms and desk areas get the tile grid and pendants.
- The ring's ribbon cells get beams.
- A cell a wall cuts through gets the flush tile grid, because a hanging panel would clip the wall.
- Everywhere else gets the floating panels.

`KitOfficeRoof.cs`, `BounceLight.cs` and `FloorReflections.cs` now use the 40 × 32 m plate:
- The roof's dummy pad, arsenal bay and props moved with the south-east corner.
- The lift call spot is now in front of the south-facing doors.
- The DevMirror now runs along the upper floor's south wall.

The scene's unsaved state before the rebuild is kept as `KitOffice_PreL2.unity`.

**Not in the L2 drawings' form yet:**
- The plan has three storeys; KitOffice has two, because the roof range and the lift's travel are built for a 9 m roof.
- The upper bridge is a solid slab strip, not a glass deck.
- There are no stair cages or floor gates (G1, G2, GF).
- The feature flight sits 0.4 m off the drawn line, and some doors moved to the 2 m grid. The back office's ring door moved from y 24 to y 21 so its wall can carry the zone sign.
- The print and server baffles and the lift-lobby stub are left out; the stub would block the lift doors.

**Still open:**
- Screens at the court corners for the long diagonals.
- One building or two towers (Alex and Adam).

The rebuild also resets the scene's NetworkObject IDs, as every rebuild does. Captures (L1-L7 plus two top-down plans, `unity_office_plan_ground.png` and `unity_office_plan_upper.png`) are in `review/unity_office/`. Nothing has been walked in Play Mode.

### Markings system, 2026-11-15 (Alex: "looks good but bland and boring ... follow the structure with purpose and intent")

The L2 build now has the marking system from [MARKINGS.md](concepts/Layout/MARKINGS.md). Every mark has one job. [markings/make_markings.py](markings/make_markings.py) draws them all as textures from `concepts/Layout/L2/layouts.json` (ring, nodes, case, gates, district colours) and [markings/l2_on_kit.json](markings/l2_on_kit.json). That second file records the L2 walls and doors as built on the kit grid. `KitOfficeScene.cs` now reads its walls from it too, so every band stops at a real doorway. Firm names and colours come from the `FirmDefinition` assets: Ashcombe & Pryce (tower 0, the north-west firm) and Halloran Voss.

**Floor:** one 40 m inlay sheet per storey, alpha-clipped with the floor's 0.96 gloss.
- The ring is one unbroken 300 mm navy band with silver edges, with named node pads. N1 in the lift lobby carries the floor number.
- District colours run along each wall's base and stop at every doorway.
- Threshold strips run wall to wall where routes cross into a district: L | A on the west ring, L | H and A | H at x 14, and their turned twins.
- The tenancy line runs down the court's axis from the lift door to the south baffle, in each firm's colour, with "DEMISE LINE ... NO CROSSING WITHOUT APPOINTMENT" microtype.
- The closing-table plinth (concentric rings and a tick scale) marks the case start.
- Upstairs, red gate bands with chevrons mark each stair arrival (E1, E2, the feature flight), and navy and silver edge strips run along the void and the stair holes.

**Ceiling:**
- District bands on the soffit along every solid wall.
- An emissive light cove directly above the ring.
- Floating panels only over rooms and desk areas. Circulation keeps the bare soffit, whose slab light lines run wall to wall.
- The beams, pendants and the 16 m ribbons are gone.

**Walls:**
- A room plate at every door (number, English and Japanese).
- "You are here" evacuation plans of the built floor, turned so the reader's forward is up, at each stair door and in the lobby.
- Floor numbers on every stair landing and arrival.
- One cobalt band along the feature stair's stringer.
- Each firm's wordmark lightbox at its reception. AONAGI's logo lightbox (the landlord) moves to E1 and E2's ring faces.
- Glazing manifestation dots on breakable partition glass only. Curtain walls, windows, guards and the lift shaft stay clear.

`KitSigns.cs` gained a public `Decal` (printed, inlay and light finishes) and `LightboxGraphic`.

**Floor reflections:** the probes are baked with the marks hidden. With them in the bake, the probes gave every shadowed floor and pane a strong cyan cast (Alex: "get rid of that weird cyan stuff").

**Not done from MARKINGS.md:**
- The cobalt band runs on the one feature flight only, because there is no third floor.
- The lift doors keep their LED floor indicators rather than big painted numbers.
- No ribbon landmark per firm entry: a ribbon from the reception wall would cross the ring band, so the ring replaced it.
- The plinth's "CLOSING TABLE" type is small and only readable up close.

**Proposals Alex hasn't seen yet:** the tenancy line and the gate band. Captures: `review/unity_office/unity_office_M1`-`M5*.png` and `unity_office_marks_plan_*.png`. Unsaved scene state before these rebuilds is in `KitOffice_PreMarkings.unity`.

### Networking after a rebuild, 2026-11-15 (Alex: the lift disappears in Play)

`KitOfficeScene.BuildAndSave` now finishes by doing what Harvey's 58c9b4e did by hand:
- It gives every NetworkObject its FishNet scene ID, the same step as Tools > Fish-Networking > Reserialize NetworkObjects.
- It sets the NetworkManager's spawnable prefabs to `Assets/DefaultPrefabObjects.asset`.

Without the IDs, FishNet switches the lift and the roof props off in Play. Without the prefab list, the NetworkManager never initialises at all. Every rebuild used to undo both.

Checked 2026-11-15: after a rebuild, Play hosted a local game, and the lift and all 8 roof props were active and spawned. Riding the lift was not tried.

## Aero office props on the trim sheet (2026-11-15)

Alex: the props looked "bland and roblox like and blockouty". The office furniture, computers and doors are remodelled to the approved concepts ([sheets and spec](concepts/Props_Aero/README.md), [computers, doors and lift](concepts/Props_Aero/README_part2.md); Alex picked the pearl CRT monitor).
They are built by [props_aero.py](props_aero.py), under the same piece names, pivots, envelopes and collider kinds, onto the Painter trim sheet in [trim/](trim/README.md). `build_all.sh` builds them last, through `trim/build_props.sh`.
New piece: `PC_Tower` (0.19 x 0.42 x 0.40, a loose prop with a box collider, front +Z), which sits on the floor beside a pedestal.
`Monitor_Flat` is now the 0.55 x 0.36 x 0.46 CRT (deeper than the old flat panel). Its pivot is still under the foot.
Second pass (Alex: "it doesnt match the references still"): stronger aqua frost on the props (`Plastic_Aqua_Frost`, not the walls' `Glass_Frosted`), softer forms (domed CRT screen, curved chair back with tube arms, bucket pod chair, crowned desk and cabinet tops), and the AONAGI ring, the copier display and printed cans from the trim sheet's Print band.
Third pass, **approved by Alex 2026-11-15**: moulded squircle bodies on every prop (`moulded()` in props_aero.py), the task chair remodelled, the copier rebuilt from its drawing on a matching paper deck (still 1.05 m tall; Alex chose this), and the props' `Trim_Aero` material on the new **Aero Plastic** shader (`Assets/_Project/Art/Plastic/AeroPlastic.shader`: URP Lit plus a lacquer clearcoat, a top-to-underside gradient and a rim; its sliders are on the material). The CRT monitor stays at the second pass, as Alex preferred.

### Lighting for the Aero props, 2026-11-15 (Alex handed the lighting over: props "still don't match the concept sheets")

The props' pearl white (albedo F4F8F8) rendered flat grey-blue with grainy dark halos at every base, and gloss showed no highlights. Three changes:
- **Fill and grade** (`OfficeLook.cs` and the volume profile):
  - Ambient near neutral (F3F4F6 / F2F3F5 / EAECEF), at 0.65 (was 0.5). This was a second step the same day: shaded pearl still read cool-grey after the first.
  - Colour filter FAFBFD (was F7FAFF).
  - Exposure 1.25 (was 1.1); the sky is compensated.
  - White balance −2/−1 (was −6/−3).
  - Shadow tone and lift almost neutral (shadows 0.99/1/1.01; lift 1/1/1.01).
  - Film grain halved, to 0.08.
- **SSAO** (`Assets/Settings/PC_Renderer.asset`, project-wide): intensity 0.45 (was 0.9), radius 0.3 (was 0.45).
- **Reflections:** new `ZoneReflections.cs` bakes 40 box-projected probes on 8 m zones, at eye height, on both floors. KitOffice's build runs it after the floor probes.

Captures: `review/unity_office/unity_office_look_*.png`. Unsaved scene state before the rebuild: `KitOffice_PreLookNeutral.unity`.

### Exposure and light contrast, 2026-11-15 evening (Alex: "overly bright and too evenly lit, hurts my eyes")

Changes, keeping the near-neutral colours so the white props stay white:
- Exposure 1.0 (was 1.25).
- Fill 0.5 (was 0.65).
- Contrast 18 (was 12).
- Bloom threshold 1.5 and intensity 0.2 (were 1.2 and 0.3), so the glossy floor's sun reflections no longer glare.
- Bounce lights 2.5 (was 4).
- `KitOfficeScene` now adds light pools: shadowless warm-white spots under every room and desk-area ceiling panel, wall-washers on the firm and AONAGI lightboxes, and a spot from the roof onto the closing-table plinth. The corridors get none, so they read as the gaps.

A first try at exposure 0.85 and fill 0.42 read dull grey and was backed off. Before and after captures from the same views: `review/unity_office/look_before_after/compare_0.png` and `compare_1.png`. Unsaved scene state before the rebuilds: `KitOffice_PreContrast.unity`.

## Checks run

- Every piece passes `mdl.mjs check` with no errors (2026-11-12, 26 pieces). The warnings are long thin faces: on thin boxes (rails, panes, slab edges), and where the shadow-gap cuts slice the flat-shaded walls. They are flat faces, so they shade correctly; they would only matter for automatic LODs, which the kit doesn't use. The locked door's lock plates touch its faces without a boolean union (a "faces cut through each other" warning).
- `mdl.mjs verify` passes on every FBX: sizes, triangles and materials read back as built.
- `assembly_test.py` passes all 9 checks: façade, side wall, corner, floor and ceiling continuous with no gaps; walls meeting the soffit; door head closed; the flight rising one floor per turn and arriving flush with the next floor.
- [interior_scene.py](interior_scene.py) dresses the whole floor in the approved pieces and renders it to `review/interior/`, including the concepts' stair close-up and rival views.
- In Unity, `Close the Deal > Art > Build Kit Office Scene` ([KitOfficeScene.cs](../../Assets/_Project/Editor/Art/KitOfficeScene.cs)) builds `Assets/_Project/Scenes/KitOffice.unity` as the **office labyrinth** (2026-11-13; the tower floor it used to build is in git history, and so is the last tower version, which was kept as `KitOffice_PreLabyrinth.unity` until 2026-11-14). It is 32 × 24 m on two storeys: a window corridor along the glass façade (north), zone L's corridor loop round a rounded-corner island with the cobalt niche (west), zone H's double-height atrium with the straight stair and glass-guarded upper floor (middle), zone C's meeting room behind frosted glass (south-west), zone A's glass cuts (south), and zone R's split junction of two offset islands and block cover (east). It adds the player rig and the office look; players spawn at the two ends of the window corridor. Captures are in `review/unity_office/` (L1–L7). Checked 2026-11-13: the stair's walking surface rises 0.25 → 4.50 m and meets the upper floor flush; the void guards, island walls and 1.1 m cover stop rays; in Play Mode a hosted player spawned standing on the floor, though in the atrium rather than at a corridor spawn (not yet explained). Walking the routes, the stair and the upper floor has not been tried.
- **Not yet checked in Unity** for the 2026-11-12 pieces: `KitImport.cs` lists them, their materials and textures, but it hasn't been run (it rebuilds `KitTest.unity`, which was open with unsaved work). Import scale, colliders, the emissive strip and the textures under the game's lighting are unverified.

## What the floor template would change to match the grid

The kit snaps to a 4 m grid; the current greybox template doesn't fully sit on one. These are proposals for whoever owns the floor template builder ([TOWER.md](../../docs/systems/TOWER.md)), not changes made here:

1. **Main floor depth 14 m = a 2 m front strip plus three 4 m rows.** The strip goes at the windows, so the stairwell module (z −6 to −14) lines up with the grid.
2. **Stair centre at z −10.0, not −10.05.** The template's 1.2 m walkway behind the cage becomes 1.25 m.
3. **Spine rooms 8 / 12 / 8 m instead of 30 / 40 / 30 % (8.4 / 11.2 / 8.4 m),** so the dividers land on grid lines at x ±6.
4. **Spine doorways 1.2 × 2.4 m with a wall above,** instead of the template's full-height 1.2 m slots. The 2.4 m height is a proposal.
5. **Façade end columns flush with the side walls,** which the template already does.
