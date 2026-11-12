<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-11
  Prompt    : "Take the blockout office snap kit into art production; phase 1: concept turnarounds through Codex"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The brief Alex hands to Codex for concept turnarounds of the visible kit pieces. It lives
              beside the concepts so the sizes and constraints the drawings answer to stay with them.
-->
# Concept turnarounds for the office snap kit — request to Codex

**From:** Alex (environment artist), drafted by Claude · **Date:** 2026-11-11 · **Status:** Waiting for Codex

Close the Deal is a PvP game: two law firms race up facing glass office towers and fight across a 12 m gap.
We have a blockout snap kit of plain boxes ([../README.md](../README.md), built by [../kit_pieces.py](../kit_pieces.py)).
Its sizes, pivots, colliders and material slots are final. We need concept turnarounds that show what each visible piece should look like as finished art, **inside the envelope it already has**.

Alex approves each concept. Nothing gets modelled until Alex has approved it.

## What to deliver

For each piece below, in priority order (stop after any piece and hand back what you have; earlier pieces matter most):

- **Four views:** front (looking at the room side, −Z), side (looking along the run, from +X), three-quarter (from the room side, above eye height), and a **section** cut through the middle of the run.
- **Dimension marks** on front, side and section, in metres, using the blockout sizes listed per piece. Mark the pivot (a small cross) and floor level (y = 0).
- **A 1.8 m standing figure** in a Deep Navy (#111B38) suit beside the piece in every view, for scale.
- **Flat colour from the palette below**, with one soft key light from above. Draw to a true orthographic scale in front, side and section.
- **A short notes file per piece:** what each detail is, its size, which existing material slot it uses, and anything you think breaks a constraint below.

Output, all under `ArtSource/Kit/concepts/` and nowhere else:

```
concepts/<Piece>/front.png  side.png  three_quarter.png  section.png  notes.md
concepts/<Piece>/sheet.png      (the four views on one sheet, 2048 px wide or more)
```

Make the images however suits you (SVG drawn by script, a throwaway Blender scene, anything). If you use a script or scene, keep it in the piece's folder. **Do not edit `kit_pieces.py`, `build_all.sh`, `assembly_test.py`, anything else in `ArtSource/`, or anything in `Assets/`.**

## Hard constraints (from the kit)

1. **The envelope is fixed.** Snap faces (both ends of a run, the floor contact, the top at the soffit) stay flat and exactly where they are. Pivots, piece splits and colliders don't change.
2. **Detail goes on, not out.** It may be recessed into the envelope, or stand proud by **at most 15 mm**, and then only on the faces a player sees (room side or street side), never on a snap end, top or bottom.
3. **Existing material slots only:** Floor_Carpet, Ceiling_Tile, Wall_PaperWhite, Frame_Aluminium, Glass_Clear, Door_Leaf, Stair_Aluminium. If a detail truly needs a new one (e.g. a lit strip for light fittings, or a cobalt accent), name it in `notes.md` as a **proposal**; don't design around it as if it were agreed.
4. **The 1 m sill, 4.3 m floor to soffit and 0.2 m slab are game dimensions.** Don't redraw them to look better.

## Direction (from the art bible)

- **2001 corporate futurism, played straight.** "A multinational company's 2001 vision of 2025": clean, white, blue, elegant, slightly empty. Closer to an airport or a transport-design render than to sci-fi.
- **Large forms first.** A few confident masses, then colour zones. Small construction detail only where it helps scale, function or readability.
- **No greeble, no cyberpunk.** No random panels, pipes, vents, rivets, neon, grime or scratches. If a detail needs a paragraph to justify it, cut it.
- **Minimal mullions.** Window and partition glass is as close to frameless as the piece allows. Nothing may draw horizontal or vertical bars across a standing figure between knee and head height.
- **Glass for visibility first.** A dark suit must read clearly through **two panes** (its own window and the rival's). Draw glass as near-clear with the faintest cool tint (about 5% opacity in our tests) and no mirror reflection.
- **Pale interiors.** Players read as dark figures against bright white walls; keep walls Paper White, not cream.
- **Cool, quiet base architecture.** The kit is the quiet layer; graphics and hero props come later in authored room sets. A piece must look intentional repeated twenty times.
- **The mundane layer is welcome** where it applies: cheap ceiling tiles, carpet tiles, office strip lights.
- **Use the reference plates for mood only.** Don't copy their layouts, signage, logos or pieces literally.

### Palette (bible §03)

| Swatch | Hex | Use on the kit |
|---|---|---|
| Paper White | #F3F4F4 | Walls, columns, sills, ceiling. The largest area by far |
| Silver Grey | #C9CED3 | Aluminium frames, door leaf, stair metal, quiet separators |
| Deep Navy | #111B38 | Dark anchors: window bands, black trim, the scale figure |
| Corporate Cobalt | #244FB9 | Only in large confident blocks, if at all on base pieces |
| Electric Blue | #2879EF | Rare; not expected on base pieces |
| Cyan | #20D4E6 | Thin lines or indicators only, sparingly |
| Near Black | #101116 | Maximum-contrast trim, sparingly |

Carpet is muted grey-blue, low contrast. Its current value is #8A94A3 (a placeholder).

**One open choice for the window bay:** the bible asks for near-black/navy trim on frames and rails; the kit currently has satin aluminium (Silver Grey). Show the window bay **both ways** (A: aluminium, B: Deep Navy) so Alex can choose.

## The pieces, in priority order

Sizes are metres. Axes as in Unity: X along the run, Y up, Z thickness. The room side is −Z.

### 1. Window bay with façade and corner columns

`Window_4m` (and `Window_2m`), `Column_Facade`, `Column_CornerL` / `Column_CornerR`. Draw a 4 m bay between two façade columns, and one corner column.

- Bay: run 4.0 (2.0) × 0.2 thick × 4.3 high. Pivot front-left corner at floor level.
- Sill: solid, full run, 1.0 high, 0.2 thick. It is cover in play, so it stays solid.
- Rails: 0.05 high × 0.1 deep, at the sill top (y 1.0–1.05) and the head (y 4.25–4.3), running between column faces (x 0.2 to run − 0.2), centred in the wall thickness.
- Pane: one sheet, 0.02 thick, centred in the wall, x 0.2 to run − 0.2, y 1.05 to 4.25 (3.6 × 3.2 m on a 4 m bay). No collider. No intermediate mullion.
- Façade column: 0.4 wide × 0.24 deep × 4.3, centred on the grid point, 0.02 proud of both wall faces.
- Corner column: 0.42 × 0.24 × 4.3, sitting inside the corner, flush with the side wall and 0.02 proud.
- Also show the **street side** (+Z) in the three-quarter view: this is what the rival sees, with the 0.2 slab edge below the sill and above the head.
- Show it with A/B frame colour (see above).

### 2. Solid wall with skirting and slab-edge band

`Wall_Solid_4m`, `Wall_Solid_2m`: run × 0.2 × 4.3, Paper White, opaque. Used for the service spine and side walls.

- **Skirting** at the floor: a recessed shadow gap or a flush band reads more "2001" than a proud moulding. Height and depth in the notes.
- **Band at the top**, where the wall meets the 0.2 slab soffit: a deliberate line that reads as the slab edge carried along the wall (e.g. a Deep Navy band, or a reveal).
- Show how two 4 m walls and a 2 m wall look side by side (do the joints read as intentional?), and an inside corner (two walls overlap on the inside; there is no corner piece).

### 3. Door wall with frame and leaf

`Wall_Door_4m` (three parts: Left, Right, Head) and `Door_Leaf`.

- Wall: 4.0 × 0.2 × 4.3. Opening 1.2 wide × 2.4 high, centred (x 1.4–2.6), from the floor.
- Leaf: 1.18 × 0.04 × 2.38, 0.01 clear of frame and floor. Pivot on the hinge axis at (1.41, 0, −0.1) inside the wall.
- The frame may not narrow the 1.2 × 2.4 opening (the leaf fills it), so it is a surface architrave (≤ 15 mm proud) or a recessed reveal.
- Show the leaf closed and open at 90°. Service rooms sit behind these doors (WC, STORE, UTIL).

### 4. Glass meeting-room partition

`Partition_Glass_4m`, `Partition_Glass_2m`: run × 0.1 × 2.5.

- Base rail and top rail: full run, 0.08 high × 0.1 deep (y 0–0.08 and 2.42–2.5).
- Pane: 0.02 thick, centred, y 0.08 to 2.42, full run. No vertical mullion; if the 4 m run needs a pane joint, show it as a butt joint at 2.0.
- The partition stops at 2.5; the 1.8 m above it to the soffit stays open. Show how it looks against the ceiling.
- Rivals must see the stair entrance through meeting rooms: show the figure seen **through two partitions**.

### 5. Ceiling and carpet

These are the two faces of the slab pieces (`Slab_4x4`, `2x4`, `4x2`, `2x2`, `Slab_Stairwell`): 0.2 thick, top at floor level, carpet on top, ceiling underneath, Paper White edges.

- **Ceiling tile grid** on a module that divides 2 m evenly (0.5 m or 1.0 m), so bays and infills line up. Cheap office tiles are fine.
- **Strip-light fittings** in the tile grid, recessed or surface-mounted, hanging no more than 50 mm below the soffit. Show the layout on one 4 × 4 bay. Lights will need a new emissive slot: propose it in the notes.
- **Carpet** as carpet tiles on the same module. Show two values side by side: the current #8A94A3 and one darker grey-blue. Put the navy figure on each **seen from the floor above** (looking down 45° through the stair opening), so Alex can judge the read.
- Show the slab edge as seen from the street (a section through the façade).

### 6. Spiral stair: visual steps, handrail, glass cage

`Stair_Flight`, `Stair_Column`, `Stair_CagePanel`. Pivot at the stair centre, floor level; 0° toward +Z; the flight turns clockwise.

- Flight: one full clockwise turn rising 4.5 m, treads from radius 0.8 (column face) to 2.6, 0.25 thick under the walking surface. Envelope 5.2 × 5.2 × 4.75.
- **The walking surface stays a ramp** (players are physics capsules). Steps are **visual only**: suggest 24 risers of 0.1875 m (15° each, lining up with the cage's 30° rhythm) whose nosings sit on the ramp line, so feet never visibly float more than half a riser.
- Column: radius 0.8, 4.5 high.
- Cage: 10 floor-to-soffit glass panels, 1.52 × 0.05 × 4.3, at radius 2.75 (0.15 outside the treads), centred at 15°, 45° … 285°; open from 300° to 360° where the flight arrives from below.
- **Handrail:** about 1.0 m above the tread line, either on the column or on the cage's inner face, inside the 0.15 gap. It must not cross a figure's silhouette when the stair is seen from the rival tower.
- The locked door across the stair is a separate prop; leave it out.
- The stair entrance must stay legible from the rival tower: show the three-quarter view from the façade side.

## Scale and units

1 unit = 1 m. Player figure 1.8 m. Doorways 1.2 × 2.4. Floor to floor 4.5. Floor to soffit 4.3.

## Hand-back

When a piece is done, put its folder in place and list it in a short `concepts/STATUS.md` (piece, files, anything you flagged). Alex reviews; Claude checks the sheets against the sizes above and the bible's environment checklist, then models only what Alex approves.
