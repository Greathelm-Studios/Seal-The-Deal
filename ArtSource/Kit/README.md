# Office snap kit

**Status:** Current · **Last updated:** 2026-11-12

The modular kit for office floors: slabs, walls, doorway, window bays, façade columns, glass partitions, the spiral stair, its locked door and a ceiling light.
All pieces come from one script, [kit_pieces.py](kit_pieces.py), so they share one set of grid, height and material values.
They are modelled to the office direction Alex approved on 2026-11-12, **White Trays + Cobalt Portal** ([decision](concepts/Office/DECISION.md), [concepts](concepts/Office/contact_sheet.png)).
`Assets/_Project/Editor/Art/KitImport.cs` brings them into Unity; the pieces added on 2026-11-12 have not been through it yet.

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

## Checks run

- Every piece passes `mdl.mjs check` with no errors (2026-11-12, 26 pieces). The warnings are long thin faces: on thin boxes (rails, panes, slab edges), and where the shadow-gap cuts slice the flat-shaded walls. They are flat faces, so they shade correctly; they would only matter for automatic LODs, which the kit doesn't use. The locked door's lock plates touch its faces without a boolean union (a "faces cut through each other" warning).
- `mdl.mjs verify` passes on every FBX: sizes, triangles and materials read back as built.
- `assembly_test.py` passes all 9 checks: façade, side wall, corner, floor and ceiling continuous with no gaps; walls meeting the soffit; door head closed; the flight rising one floor per turn and arriving flush with the next floor.
- [interior_scene.py](interior_scene.py) dresses the whole floor in the approved pieces and renders it to `review/interior/`, including the concepts' stair close-up and rival views.
- In Unity, `Close the Deal > Art > Build Kit Office Scene` ([KitOfficeScene.cs](../../Assets/_Project/Editor/Art/KitOfficeScene.cs)) lays the same floor out three high from the kit prefabs, with the rival façade and the office look, in `Assets/_Project/Scenes/KitOffice.unity`. Its captures are in `review/unity_office/`. Not yet walked in play mode.
- **Not yet checked in Unity** for the 2026-11-12 pieces: `KitImport.cs` lists them, their materials and textures, but it hasn't been run (it rebuilds `KitTest.unity`, which was open with unsaved work). Import scale, colliders, the emissive strip and the textures under the game's lighting are unverified.

## What the floor template would change to match the grid

The kit snaps to a 4 m grid; the current greybox template doesn't fully sit on one. These are proposals for whoever owns the floor template builder ([TOWER.md](../../docs/systems/TOWER.md)), not changes made here:

1. **Main floor depth 14 m = a 2 m front strip plus three 4 m rows.** The strip goes at the windows, so the stairwell module (z −6 to −14) lines up with the grid.
2. **Stair centre at z −10.0, not −10.05.** The template's 1.2 m walkway behind the cage becomes 1.25 m.
3. **Spine rooms 8 / 12 / 8 m instead of 30 / 40 / 30 % (8.4 / 11.2 / 8.4 m),** so the dividers land on grid lines at x ±6.
4. **Spine doorways 1.2 × 2.4 m with a wall above,** instead of the template's full-height 1.2 m slots. The 2.4 m height is a proposal.
5. **Façade end columns flush with the side walls,** which the template already does.
