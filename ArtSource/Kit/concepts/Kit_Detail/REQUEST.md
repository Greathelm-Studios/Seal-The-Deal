<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-12
  Prompt    : Alex: "lets actually work on assets now like proper ones" -> upgrade the kit, concepts first
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : Brief to Codex for detail concepts of the existing kit. The kit is clean blocks that match
              the approved office direction; this asks what each family looks like as finished game art
              (edges, trims, joints, hardware, surfaces), so Alex can pick a level before it is modelled.
-->
# Kit detail pass — request to Codex

**From:** Alex (environment artist), drafted by Claude · **Date:** 2026-11-12 · **Status:** Waiting for Codex

Close the Deal: two law firms race up facing glass office towers and fight across a 12 m street gap through the windows.
The office kit is built and in Unity, modelled to the approved direction **White Trays + Cobalt Portal** ([../Office/DECISION.md](../Office/DECISION.md)).
Right now every piece is a clean block with flat colour. Alex wants the pieces to read as **proper finished game art**. Before anything is modelled, we need concepts showing what that means for each family.

Use your **image generation tool**. Paint close and mid views as finished concept art: lit, designed, believable materials.

## Read and look at first

- `ArtSource/Kit/concepts/Office/` — the approved paintings (`01_White_Trays/*`, `02_Cobalt_Portal/*`) and `DECISION.md`. **This is the target look; stay in it.**
- `ArtSource/Kit/review/unity_office/*.png` — the kit as it is in Unity today. Your paintings show these same pieces, finished.
- `ArtSource/Kit/README.md` — the pieces, their sizes and materials. `ArtSource/Kit/kit_pieces.py` has every dimension.
- `ArtSource/Kit/concepts/Office/refs/art_bible.txt` — sections 09, 10, 11, 18 and 20 above all.
- `docs/systems/GLASS.md` — the glass now has a stylised sheen (two hard diagonal light streaks, no mirror). Show glass that way.

## The rule that matters most

**Detail must come from shape, edge and material, not from clutter.** The bible: "Large forms first; graphic accents second; micro-detail last." "No greeble, no cyberpunk." "A surface should not become interesting because of a 4K texture full of scratches."
So "proper" means: crisp bevels that catch light, real trim profiles and shadow gaps, believable joints between panels, honest hardware (hinges, handles, grilles) where an object really has it, and materials that read as what they are (satin aluminium, painted plaster, acoustic tile, carpet tile, laminate, glass). Very light wear only where hands touch.

## Hard constraints (unchanged from the kit)

1. **Envelopes, pivots and snap faces don't move.** Detail is recessed, or stands proud at most 15 mm, and never on a snap end, the floor contact or the soffit contact.
2. **Game dimensions are fixed:** 0.6 m sill (lowered today), 4.3 m floor to soffit, 0.2 m slab, 1.2 × 2.4 m doors, 4 m bays, the stair radii.
3. **Glass stays near-clear.** Nothing may put bars across a standing figure between knee and head height.
4. **Palette:** Paper White dominant, Silver Grey, Deep Navy trim, Corporate Cobalt in big blocks only, cyan as tiny indicators only, red only on the locked stair door.
5. Name any new material (e.g. `Metal_Satin`, `Laminate_White`, `Rubber_Black`) as a **proposal**.

## What to deliver

For each family below, **two levels side by side** on one sheet, so Alex can pick how far to go:

- **A — Refined:** the current forms with bevels, clean trims, joints and correct materials. Minimal new geometry.
- **B — Rich:** A plus the functional detail the real object would have (hardware, fixings, grilles, nosings, panel joints), still restrained and readable from 12 m.

Per family: one **sheet** (16:9, as large as the tool allows) with A and B as close-up hero views (1–3 m from the piece, 1.7 m eye or the natural angle), plus one **mid view** of A and B in the room so the detail can be judged at play distance. Label A and B on the sheet.

The families, in priority order (stop after any and hand back; earlier matter most):

1. **Façade** — `Window_4m` (0.6 m sill with the navy tray pocket, navy rails, glass), `Column_Facade` and the corner columns with their navy street-face slot. Include a view from the rival's side.
2. **Walls and door** — `Wall_Solid_*` with the navy skirting shadow gap and soffit reveal, `Wall_Door_4m` with its reveal, `Door_Leaf` (hinges, handle, kick plate, room sign), the cobalt portal walls.
3. **Stair** — `Stair_Steps` (open aluminium treads and helical handrail), the white `Stair_Column`, the curved glass cage, and `Stair_LockedDoor` (the red full-height door: lock, hinges, warning graphics).
4. **Ceiling and floor** — the 0.5 m tile grid, `Ceiling_LightStrip`, carpet tiles, the slab edge seen through the façade.
5. **Glass partition** — `Partition_Glass_*`: navy base and top tracks, the pane, the corner where two partitions meet.

Also deliver:

- `contact_sheet.png` — all the sheets on one page, labelled.
- `README.md` — per family and per level: each detail, its size in mm, which existing or proposed material it uses, and whether it changes a collider (it shouldn't). Say which level you'd build per family and why, in one or two sentences. Count your image tries per sheet.

If an image breaks a constraint (mullions across the glass, greeble, grime, mirror glass, wrong palette, a moved snap face), regenerate it.

## Where to write

Only inside `ArtSource/Kit/concepts/Kit_Detail/` (one folder per family: `01_Facade/`, `02_Walls_Door/`, `03_Stair/`, `04_Ceiling_Floor/`, `05_Partition/`). Don't edit or delete anything else; other sessions are working in this repo. Run no git commands that change state.
Hand back after the five families; Alex picks a level per family before anything is modelled.
