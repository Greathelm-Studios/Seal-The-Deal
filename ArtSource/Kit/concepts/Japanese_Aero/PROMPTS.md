# Image-generation prompt record

Date: 2026-11-13.

Mode: built-in image generation; opaque output; no CLI/API fallback.

**9 image tries, 9 completed calls, 8 selected images.**

Each first-pass call used exactly: COMMON + newline + VARIANT + newline + CAPTURE, as transcribed below.
The four originals were the edit targets; no image was merely a loose style reference.

| Try | Edit target | Variant | Selected output |
|---|---|---|---|
| 1 | unity_office_L1_window_corridor.png | A | Superseded by try 9 |
| 2 | unity_office_L1_window_corridor.png | B | unity_office_L1_window_corridor_B.png |
| 3 | unity_office_L3_atrium_stair.png | A | unity_office_L3_atrium_stair_A.png |
| 4 | unity_office_L3_atrium_stair.png | B | unity_office_L3_atrium_stair_B.png |
| 5 | unity_office_L4_room_through_frost.png | A | unity_office_L4_room_through_frost_A.png |
| 6 | unity_office_L4_room_through_frost.png | B | unity_office_L4_room_through_frost_B.png |
| 7 | unity_office_L7_upper_over_atrium.png | A | unity_office_L7_upper_over_atrium_A.png |
| 8 | unity_office_L7_upper_over_atrium.png | B | unity_office_L7_upper_over_atrium_B.png |
| 9 | unity_office_L1_window_corridor_A.png, from try 1 | Local LED refinement | unity_office_L1_window_corridor_A.png |

## COMMON

```text
Use case: style-transfer.
Asset type: production environment concept paintover of an existing Unity game level.
Input image 1 is the EDIT TARGET, not a loose inspiration image. Paint directly over its surfaces. Preserve precisely the original 16:9 composition, camera location, lens, perspective, crop, structural silhouette and screen positions of every wall, column, window, stair, doorway, desk, counter and floor edge. No redesign of the layout, no new architecture, no fisheye or reframing. Preserve the exterior city and cloud view, simply refine the light.
The look is an optimistic 2000–2006 Japanese corporate office: glossy ice white, anodised silver, rounded capsule trim, early flat-panel computers, translucent acrylic details, bilingual Japanese/English signage. This is a practical office in a fast PvP shooter. Keep all existing paths, stair treads, doorways and landings open. Glass must remain almost invisible and unpatterned from 0.35 to 2.1 metres above its adjacent floor; decorative frost only below 0.35 m and above 2.1 m. Never add a horizontal bar, stripe, logo or opaque object across that sightline on glass. Retain existing safety geometry but do not invent rails. Cover tops remain 1.1 m. Keep dark navy character silhouettes in their exact original positions, strongly legible against the interior.
Brand proposal: fictional "青凪総合サービス" with roman wordmark "AONAGI", a simple open aqua ring with one tiny vermilion circular seal. No real company marks. Clean Japanese sans-serif signage, thin silver frames; zone letters remain the existing ones, independent of floor codes. No unreadable decorative microtext, no theme-park Japan, cherry blossoms, paper lanterns, torii, samurai, cyberpunk, holograms, or neon. No labels outside the scene, no watermark. Final output: one full 16:9 paintover only, ideally 1600x900.
```

## VARIANT A

```text
Direction A — AERO: visibly glossy, translucent, refreshing aqua and sky blue with living green. White lacquer #F4FAF8; pale aqua #BCEDE9; deeper aqua #45BFC7; very small sky-blue #568BCB and vermilion #C7473E accents; silver #C8D2D9; navy #172C48. Soft daylight but maintain readable contrast and shadow depth; broad controlled reflections, not mirror glare. Add restrained water/bubble imagery only on opaque signage panels, never on the clear glass sightlines. Rounded translucent acrylic sign edges and soft ambient light, healthy foliage in existing planters. Architectural game concept finish, clean materials and good edge definition, no grungy AO halos.
```

## VARIANT B

```text
Direction B — SOFT CLUB: visibly paler, quieter and less decorated than Aero. Ice-white #F3F6F6, mist-blue #C7D7DF, pearly silver #BAC6CF, pale desaturated blue #A8BFD1, ink navy #24344C. Satin white and brushed silver, softly translucent milky acrylic restricted to approved top/bottom bands, fine type, diffuse hazy daylight with crisp structural edges. Low contrast atmosphere but dark characters remain clear. Very restrained greenery, no bubbly water graphics, no strong cyan, no saturated cobalt blocks, no highly mirrored floor, no excessive bloom. Authentic early 2000s minimalist Japanese corporate office, not a hospital or spaceship.
```

## CAPTURE L1

```text
Specific capture: L1 WINDOW CORRIDOR. Lock the close central square column, the full-height window bank along the left, the big wall behind the dark capsule, and the straight stair on the right. Keep the sharp daylight patches and every route. Replace the old cobalt column wraps with the chosen pale aqua/silver corporate treatment. Put a slim vertical "1F" marker on the opaque column and restyle the existing H sign behind the stairs to exact text "H" / "審問室" / "HEARINGS". On the large existing opaque wall behind the capsule, use a small tasteful AONAGI reception wordmark and open-ring emblem high enough to read above the character, without changing that wall's footprint. Tuck a narrow white water dispenser and a slim umbrella stand against that wall, away from circulation; no large added furniture. Refine the existing planter near the central column. Keep the capsule at the exact original location as a dark navy readability marker.
```

## CAPTURE L3

```text
Specific capture: L3 ATRIUM STAIR. The existing stair crosses diagonally from upper left to lower middle/right; preserve every step, the white folded stringer, the exact diagonal, its width, both landings, the open atrium, the curved wall behind it and all window and column positions. Keep the dark foreground capsule exactly where it is as a navy readability marker. Do not put any plant, furniture or sign onto a step or landing. Restyle the existing cobalt wall panel behind the stair in the new palette, with bilingual opaque signage "H" / "審問室" / "HEARINGS"; retain the existing graphic columns, replacing their blue wraps with pale translucent-looking corporate graphics on the opaque column itself. Floor code on the existing tall column: "1F", and a small wayfinding arrow to "2F". The view remains two storeys, not three. Restyle existing balustrades as near-invisible frameless clear safety glass, with opaque hardware at the low shoe only and no opaque horizontal bar crossing a standing person's body. Ceiling lighting stays in the original positions. A only: turn part of the existing central wall surface into a shallow contained water-wall panel, flush-mounted, aqua and silver, without changing the wall's outline; a small restrained existing-wall planter at its base, out of the route. B only: this panel is milky pearlescent acrylic without running water, greenery confined to one small existing-wall planter. Keep the sky/cloud backdrop.
```

## CAPTURE L4

```text
Specific capture: L4 ROOM THROUGH FROST. Exact original view through existing glass: preserve the huge close header at upper left, the glass vertical edge near centre-left, the full-height doorway/open gap at centre-right, every wall, the capsule counter at centre, the small desk and chair at right, the dark capsule at left-centre, ceiling tile grid and hanging light positions. Keep the counter and desk exactly their original size and location. Do not turn the counter into a large table or add meeting tables. Change the existing opaque cobalt C panel into a clean corporate sign, exact text "C" / "請求部" / "CLAIMS", with a small restrained red seal. The glass is NEAR-CLEAR between 0.35 m and 2.1 m, with frost only below and above; never put a band, rail, logo or bubble pattern across the human or room sightline. A modern silver sliding-screen edge can restyle the existing glass jamb but must remain parked open, no added wall. Add a silver-bezel early-2000s 4:3 flat-panel monitor, white desk phone, small translucent document tray and a neat paper stack on the existing right-hand desk only. A: pale aqua round-ended lacquer counter and translucent aqua trim, small living green plant tucked against the rear wall, subtle sky/water illustration inside the existing opaque C sign only; polished light corridor floor, lighter blue work-area carpet with visible texture. B: satin ice-white counter, pearly silver desk, pale mist-blue carpet and thin type; only one small plant, no water illustration. Preserve the navy capsule's location and readable silhouette, every open route and original framing.
```

## CAPTURE L7

```text
Specific capture: L7 UPPER FLOOR OVER ATRIUM. Lock every foreground desk island, divider, monitor position, keyboard, chair, glass balustrade, atrium void edge, background desk bank, column, ceiling and window to the original geometry and screen position. Keep the foreground desks at 0.74 m, existing central dividers about 1.1 m, no tall added screens. These are Japanese office desk islands with staff facing each other and a manager's station represented by the existing far desk at the head, not an extra desk in a route. Restyle every existing monitor with early-2000s silver 4:3 bezel, substantial foot and dark screen; keep every screen the same bounding size and position. Add tidy translucent document trays, white landline phones, flat folders and short keyboard cables on desktop corners only; retain open work surfaces and legible dark navy task chairs, brushed silver chair bases. Floor stays carpet in the exact existing work-area footprints, polished walkway elsewhere; use a lighter desaturated blue carpet that separates navy figures. Restyle the column wraps with the selected palette and exact opaque-column text "2F" / "審査部" / "REVIEW". The far dark capsule remains exactly where it is. The existing glass atrium barrier is almost invisible, with all opaque hardware confined to the floor shoe, no opaque horizontal handrail across the view of a standing figure. Do not add new architecture, extra balconies, desks, or columns. Tuck a compact white-and-silver unbranded drinks vending machine against the far existing opaque wall beyond the desk bank, off the walkway, plus a small low slipper cabinet near that same far entrance zone only. A: glossy aqua divider caps, healthy foliage in existing planters, clear aqua document trays, a restrained aqua corporate panel on the far vending machine. B: frost-white divider caps, brushed silver, pale mist blue and just one small green plant, no bright aqua blocks. Ceiling light positions and the original atrium view remain unchanged.
```

## Try 9 — local LED refinement

```text
Use case: precise-object-edit. Input image 1 is the EDIT TARGET: the already finished L1 Aero paintover. Make exactly ONE local addition: a slim period-2000–2006 Japanese corporate LED dot-matrix reception sign on the existing solid wall above the AONAGI company logo at back-centre/right. The wall's upper region above the wordmark is empty; mount it there, not on glass, not over the character, not on the foreground column. Approximate physical envelope 1.2 m wide x 0.18 m high x 0.06 m deep, in a thin brushed-silver housing with a dark desaturated blue face. Soft small aqua LED dots form exact text "受付 1F"; a tiny printed silver-housing label under it reads "RECEPTION". It is a modest real office status display, not a television, neon decoration or hologram.
Preserve EVERYTHING else exactly: camera, 16:9 crop, perspective, all walls, stair geometry, clear glass, columns, existing signage and company Japanese text, polished floor reflections and daylight patches, exterior clouds, water cooler, umbrella stand, plant, dark navy capsule position, all object counts, colours, gloss and original composition. Do not resize or move the company logo to accommodate the sign. No other additions or restyling. Output one full 16:9 finished image only.
```

First-pass edit targets: ../../review/unity_office/ plus each original capture filename.

Packaging: full generated frames resized to 1600 × 900, without cropping; contact sheet assembled separately.
