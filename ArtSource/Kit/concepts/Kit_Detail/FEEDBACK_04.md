<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-12
  Prompt    : Alex on round 4: "keep the aero glossy, no bubbles. not carpet. still doesnt feel right.
              more glass, shiny, more abstract feel. remember its designed for a game"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : Round-5 brief to Codex. Round 4 still read as a realistic matte office with graphics; this
              moves it to a glossy, glass-heavy, abstract game space. Overrides FEEDBACK_03, FEEDBACK_02,
              FEEDBACK_01 and REQUEST.md where they differ.
-->
# Kit detail, round 5: glossy, glass, abstract, built for a game

**From:** Alex, drafted by Claude · **Date:** 2026-11-12 · Read with [FEEDBACK_03.md](FEEDBACK_03.md) and [README_r4.md](README_r4.md). This brief wins where they differ.

## Alex's verdict on round 4

**Still doesn't feel right.** In Alex's words: *keep the Aero glossy, no bubbles. Not carpet. More glass, shiny, a more abstract feel. Remember it's designed for a game.*

Round 4 still reads as a **realistic office photographed in sunlight**: matte surfaces, carpet, ceiling tiles, desks and filing cabinets everywhere, with graphics added. The brief now is a **designed game space**: a stylised, abstract, glossy environment that a 2001 game would build. It is not an architectural visualisation of an office.

## What changes

1. **Glossy Aero is the material language, with no bubbles.** Every major surface has a finish that catches light: high-gloss white lacquer, polished pale floors with soft mirror-blurred reflections of the windows and the figures, glossy cobalt, brushed and polished silver. Sky and window reflections appear on the floor, walls and glass. Specular highlights, bloom on edges, soft cyan-to-white glows in frosted panels. Still **no bubbles, greenery, water droplets, rainbow or pastel gradients, pink chrome or vaporwave**.
2. **No carpet.** Floors are polished: glossy white or pale-grey resin, polished stone-like tiles in a large simple grid, glossy cobalt inlays. You can still mark routes and zones on the floor, as flush glossy colour fields or inlaid light lines, not as carpet. (This would replace the kit's `Floor_Carpet`; list it as a material proposal.)
3. **More glass.** Glass is the second main material after white. Use glass partitions, glass-walled rooms, glass balustrades, glass stair cage, frosted glass panels, glass floor or soffit insets, layered panes that overlap and reflect. Keep it **near-clear with the two-streak sheen**, never mirror glass, and dark suits must still read through two panes. Build the frosted and glowing panels **above 2.1 m or below 0.45 m** so knee-to-head stays clear.
4. **More abstract.** Strip out realistic office clutter: no filing cabinets, printers, monitors on every desk, ceiling tiles or exit signs. Furniture is reduced to a few clean sculptural pieces (white capsule desks, simple chairs) and only where they show scale or cover. Architecture is clean volumes, curves, planes and light lines with almost no surface noise. Think stylised game environment or showroom, closer to bible plates 01–03 (white field, sky inserts, thin linework, sculptural form) than to an office photo. Flat, clean ceiling planes with long light lines or coves, not tiles.
5. **Designed for a game.** Every choice serves the player:
   - **Readability:** dark-suited figures must pop against bright glossy white and glass, from 1.7 m and across the gap. No busy backgrounds behind standing height.
   - **Simple, modular shapes:** large clean surfaces that tile and snap (the kit's 4 m and 2 m modules), simple materials (base colour plus gloss, not photoreal texture). Detail should cost few polygons.
   - **Route and space:** the space has clear lanes, landmarks and cover shapes. The glass makes rooms see-through so players can read threats.
   - **Style fit:** cel-shaded, editorial characters stand inside it (see `refs/suit_reference.png`), so the environment should be simplified and graphic enough to sit with them, not photoreal.
   - Render it as a **stylised in-game screenshot** with clean shading, crisp edges and strong but simple lighting. Not as an arch-viz photo.
6. **Keep:** interior first, round 4's 2001 curves and capsules (they were the best part), storey-scale cobalt, hard sun shapes, and cyan only as a thin line.

## Directions

For each of the five families (façade from inside, walls and door, stair, ceiling and floor, glass partition):

- **A** = round 4's best era idea for that family (generally the B column: sky-lens portal, capsule landing, sky-lens ceiling, frosted lintel, capsule sky bays) pushed to glossy, glass, abstract and game-ready.
- **B** = the most abstract version you can make that still works as playable space.

Don't put the same "32" numeral, red door and helix in every view. Show each family's own idea; one combined view per family is enough.

## Deliver

Same five folders, files suffixed `_r5`: a `sheet_r5.png` per family, with two directions, each with a one-line caption, a hero and a play view at 1.7 m, and a small street inset. Then `contact_sheet_r5.png` and `README_r5.md` covering the idea, kit pieces changed, form moves in mm (recessed or flush, proud at most 15 mm), materials as proposals (gloss level, glass type), which you'd build, and image tries. Add one line per direction on **how it plays**: route, cover and readability. Keep rounds 1–4 untouched.

## Constraints (unchanged)

Envelopes, pivots and snap faces don't move. Recesses may be as deep as the design wants; proud detail at most 15 mm. 0.6 m sill, 4.3 m to soffit, 200 mm slab, 1.2 × 2.4 m doors, stair radii fixed. Glass near-clear with the two-streak sheen (`docs/systems/GLASS.md`). No bars across a standing figure between knee and head height. Still never: cyberpunk, neon, greeble, grime, mirror glass, blinds, decorative plants, bubbles.

Write only in `ArtSource/Kit/concepts/Kit_Detail/`. No state-changing git commands. Hand back after all five.
