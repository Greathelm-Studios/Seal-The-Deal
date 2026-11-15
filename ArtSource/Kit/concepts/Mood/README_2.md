# Seal the Deal — KitOffice mood paintovers, round 2

**Status:** Concept directions for Alex to review · **Date:** 2026-11-15

Eight paintovers made with the **built-in image generation tool**, each using its original source capture as the edit target.
Open [contact_sheet_2.png](contact_sheet_2.png): rows **Source, D, E**; four view columns; six of Alex's images in a labelled reference strip.
Exact prompts and input roles: [generation_prompts_2.json](generation_prompts_2.json).

| View | D · Colour-coded | E · Night tech |
|---|---|---|
| Window hall | [D](D_L1_window_hall.png) | [E](E_L1_window_hall.png) |
| Court / feature stair | [D](D_L3_court_feature_stair.png) | [E](E_L3_court_feature_stair.png) |
| West ring ribbon | [D](D_L4_west_ring_ribbon.png) | [E](E_L4_west_ring_ribbon.png) |
| Desk bench | [D](D_look_bench.png) | [E](E_look_bench.png) |

## D · Colour-coded — changes to build

- Keep bright daylight and glossy white enamel; introduce charcoal floor fields, dark screens and upholstery for value contrast.
- Give Ashcombe & Pryce's west rooms orange wall fields, bumper rails and orange light spilling through glass.
- Give Halloran Voss's east rooms saturated green surfaces and transmitted green light, visible across the court.
- Flood the court and lift lobby with cool blue overhead light and shaped shafts; keep the open feature stair white/blue.
- Use orange-painted steel for enclosed service stairs; these are outside the four captures.
- Use fluted frosted room glass and orange fluted desktop dividers, retaining their existing boundaries.
- Apply giant flat “01”, “04” and “H” graphics; rework the existing hall lightbox with original AONAGI branding.
- Put a slim waterfall against the rear court wall, excluding the door opening and keeping every route clear.
- Cast strong window-grid or louvre shadows; let coloured light and white highlights reflect on the floor.

## E · Night tech — changes to build

- Replace day outside with a night-blue skyline and deep cool ambient light.
- Use dark glossy navy/graphite floors reflecting lightboxes, screens and ceiling lights.
- Cove-light existing white wall panels and ceiling rafts; give existing rounded forms a soft, pillowed pearl finish.
- Turn existing poster frames into white backlit graphic lightboxes.
- Switch the CRTs and desk panels on with white/cyan document and waveform displays.
- Restrict electric cyan-blue to selected screens and graphics; keep floor brightness as reflected light.
- Confine the warm amber note to the distant reception.
- Make the court the brightest volume: spotlight its centre circle and edge-light the existing feature stair.
- Backlight both court capsules and the corridor capsules so their dark silhouettes remain readable.

## Geometry and checks

The court source has **no visible closing table**; its existing circle receives the focal light without adding a table or cover.
The [sources](source/) remain authoritative for construction: generated furniture, glazing and cover contours drift slightly, so these images are colour and lighting references rather than exact geometry drawings.
No Unity assets were changed.

All eight exports and the contact sheet were opened with `[System.Drawing.Image]::FromFile(...)` and visually reviewed.
Hall, court and ring exports are **1600 × 900**; bench exports are **1280 × 720**; the sheet is **3456 × 1536**.
Full generated frames were resized without cropping using System.Drawing; the paintovers themselves came from image generation.
`Get-FileHash -Algorithm SHA256` matched all four source files before and after export and sheet assembly.
