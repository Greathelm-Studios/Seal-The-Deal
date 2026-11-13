<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-12
  Prompt    : Alex on the kit detail sheets: "i dont like these, theyre very flat", follow the art bible
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : Round-2 brief to Codex. Round 1 answered a brief that asked for millimetre detail, so it
              came back as flat, evenly lit illustrations; this re-aims it at the art bible's large forms,
              graphic colour blocking and staging. It overrides REQUEST.md where they differ.
-->
# Kit detail, round 2 — follow the art bible

**From:** Alex, drafted by Claude · **Date:** 2026-11-12 · Read with [REQUEST.md](REQUEST.md); this wins where they differ.

## Alex's verdict on round 1

**Not these: they're very flat.** Every sheet is the same evenly lit, low-contrast room with a few millimetre details added, so A and B look alike and none of them looks like the game in the art bible.
Part of that is the brief's fault: it asked for 2–3 mm bevels, gaskets and hinges. That is the wrong scale. Throw that framing away.

## Follow the bible, literally

The bible is in `ArtSource/Kit/concepts/Office/refs/art_bible.txt` (the full document is Alex's `Close_the_Deal_Art_Direction_Handover_Bible.docx`), and its four reference plates are `refs/bible_ref_01.png` … `bible_ref_04.png`. **Open and study all four plates and `refs/plate_law_firm_atrium.png` before painting.** Plate 04 is the environment anchor.

What the bible asks for, and round 1 didn't do:

- **"Large forms first. A few confident masses, then graphic colour zones."** "Prefer one memorable shape over ten small details." Detail is 100–500 mm architectural moves: deep reveals, big chamfers and radii, carved profiles, slots you can see from 12 m. Not screws.
- **"Cobalt is structural: large confident blocks."** "A single blue rectangle across multiple floors is stronger than dozens of tiny blue props." Use big cobalt fields and diagonals as in plate 04, on columns, walls and the stair, rationed so white still dominates.
- **"Signage can be oversized and integrated into the building skin."** Oversized floor numbers, department identifiers, thin hairline grids and rules, tiny legal microtype, as in plates 01–03 and the atrium plate. Place them as part of the architecture.
- **"Cyan is an accent: a thin line, indicator, light strip."** A thin cyan light line in the right place (a column head, a tread edge, the soffit reveal) is welcome, once per family.
- **Lighting: "strong blue sky, pale atmospheric perspective, bright architectural whites", "soft world light", bloom on bright edges, cool interior.** Plus the contrast plate 04 gets: raking sun with long hard shadow shapes across white planes and carpet, deep value range, glossy-satin whites that catch light, haze in depth.
- **Depth:** layered views with foreground, middle and background, multiple floors and the rival tower in view, like plate 04 and the atrium plate. Not a single wall seen square-on in flat light.
- **Render quality:** a finished, lit game-environment render (global illumination, specular on satin white, bloom), crisp and graphic. Not a flat vector illustration.
- **Still never:** cyberpunk, neon, greeble, grime, mirror glass, blinds, plants as decoration, bars across a standing figure between knee and head height.

## What to deliver instead

Per family (same five, same folders, files suffixed `_r2`), **two distinct directions**, each with **one idea you could recognise in a thumbnail**, written as a one-line caption on the sheet. Not "refined vs rich". For example, to beat rather than copy:

1. **Façade:** columns carved to a sharp profile with a full-height cobalt face on the street side; or a continuous cyan light line in the head rail across the whole façade.
2. **Walls and door:** a giant floor number and firm identifier set into the spine wall with hairline rules; or cobalt diagonals running wall to wall across the portal.
3. **Stair:** a cobalt band spiralling with the treads, tread edges lit; the red door as a bold graphic panel with a big lock symbol and hazard rule.
4. **Ceiling and floor:** a lit cove or light line in the soffit reveal, cobalt carpet zones marking the walkway to the stair.
5. **Glass partition:** a graphic band on the glass above head height (2.1–2.4 m) carrying the room name and a cobalt stripe; clear glass from knee to head.

For each direction: one **hero** view (eye height 1.7 m, layered, sunlit) and one **rival view** from 12 m across the street showing what it does to the read of three floors. Then a `contact_sheet_r2.png` and a short `README_r2.md`: each direction's idea, the kit pieces it changes, sizes in mm for the moves, new materials as proposals, and which you'd build. Count image tries.

## Constraints (unchanged)

Envelopes, pivots and snap faces don't move; recesses may be as deep as the design wants, proud detail at most 15 mm; 0.6 m sill, 4.3 m to soffit, 1.2 × 2.4 m doors, stair radii fixed. Glass near-clear with the two-streak sheen (`docs/systems/GLASS.md`). Dark suits must read through two panes.

Write only in `ArtSource/Kit/concepts/Kit_Detail/`. No state-changing git commands. Hand back after all five.
