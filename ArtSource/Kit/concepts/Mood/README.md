# KitOffice mood paintovers

**Status:** Concept directions for Alex to review · **Date:** 2026-11-15

Twelve paintovers made with the built-in image generation tool, each using its original capture as the edit target.
Open [the contact sheet](contact_sheet.png): rows **Source, A, B, C**; columns follow the table below.
The exact prompts are saved in [generation_prompts.json](generation_prompts.json).

| View | A · Late shift | B · Showroom Aero | C · Golden hour |
|---|---|---|---|
| Window hall | [A](A_L1_window_hall.png) | [B](B_L1_window_hall.png) | [C](C_L1_window_hall.png) |
| Court / feature stair | [A](A_L3_court_feature_stair.png) | [B](B_L3_court_feature_stair.png) | [C](C_L3_court_feature_stair.png) |
| West ring ribbon | [A](A_L4_west_ring_ribbon.png) | [B](B_L4_west_ring_ribbon.png) | [C](C_L4_west_ring_ribbon.png) |
| Desk bench | [A](A_look_bench.png) | [B](B_look_bench.png) | [C](C_look_bench.png) |

## A · Late shift

- Replace daytime outside with deep blue dusk and scattered city window lights.
- Lower the even interior fill; retain readable cool blue shadows on pearl walls.
- Add warm silver desk lamps, court pendants and warm light around existing ceiling rafts.
- Switch CRTs on with aqua document windows and local screen spill.
- Dress desks with papers, binders, mugs, sticky notes, one jacket, a small plant and tucked file boxes.
- Add a glowing unbranded vending machine against the court's rear wall, clear of circulation.
- Stage the central court circle in amber light; use selective warm floor reflections against cool glass.

## B · Showroom Aero

- Keep clear daytime, with controlled pearl highlights instead of clipped white surfaces.
- Use whole colour fields: **L cobalt, A teal, C violet, R sky blue, court aqua**.
- Colour existing wall panels, ceiling raft faces and carpet fields; keep door frames and walkways legible.
- Put large flat AONAGI ring/red-dot and ribbon graphics on walls; carry them onto bench dividers.
- Turn CRTs and graphic lightboxes on with clean aqua light.
- Contrast glossy pearl and enamel with brushed silver, translucent aqua dividers and cobalt upholstery.
- Give the central court circle aqua inlay and a neutral focal pool; keep dressing sparse and ordered.

## C · Golden hour

- Lower the sun to a shallow late-afternoon angle through the existing windows.
- Stretch warm mullion shadows across floors, desks, cover and wall panels.
- Keep unlit walls, ceilings and carpet in cool blue shade with visible detail.
- Catch pearl edges, chair bases, stair treads and silver mullions with gold highlights.
- Add sparse dust motes in beams and light haze in the double-height court.
- Switch screens on quietly and add only a few papers and mugs to desks.
- Let the lit court circle and selective gold reflections provide the focal point.

## Geometry and checks

The court capture contains no visible closing table: its existing central circle carries the focal treatment without adding a new table or cover.
Original [source captures](source/) remain the geometry reference; generated furniture/glazing contours, exterior details and small lettering vary slightly and are not layout instructions.

All PNGs were opened with `[System.Drawing.Image]::FromFile(...)` and visually reviewed.
The nine hall/court/ring exports are **1600 × 900**, the three bench exports **1280 × 720**, and the sheet **2824 × 1824**.
`Get-FileHash -Algorithm SHA256` before and after export/sheet assembly matched for all four sources.
No Unity validation applies to these concept images.

