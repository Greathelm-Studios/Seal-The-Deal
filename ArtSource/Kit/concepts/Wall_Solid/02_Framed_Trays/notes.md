# 02 / Framed Trays

**Status:** Target concept for Alex's review

**Last updated:** 2026-11-11

Each module becomes a large inset white tray, framed by full-depth white architecture and a navy perimeter trough.

The broad skirting shelf and crown carry the white slab language along the wall.

## Profile and materials

Coordinates in this table use Unity axes, with Y up and the room face at Z -0.20 m.

The opposite room face has the mirrored profile.

| Detail | Height / extent | Recess into each face | Material |
| --- | --- | --- | --- |
| White skirting shelf | Y 0.00-0.28 m | 0 mm | Wall_PaperWhite, #F3F4F4 |
| White crown under soffit | Y 4.02-4.30 m | 0 mm | Wall_PaperWhite |
| White side frames | First and last 200 mm of each run | 0 mm | Wall_PaperWhite |
| Navy trough backing | X 0.20 to run-0.20, Y 0.28-4.02 m | 65 mm | Trim_Navy, #111B38 |
| Inset white field | X 0.30 to run-0.30, Y 0.40-3.90 m | 40 mm | Wall_PaperWhite |
| Visible vertical trough | 100 mm wide on each side of the field | 65 mm | Trim_Navy |
| Visible head and base troughs | 120 mm high above and below the field | 65 mm | Trim_Navy |

The inset field sits 25 mm ahead of its navy trough and remains 40 mm behind the original room plane.

Trim_Navy is proposed for the solid wall's material assignment, using the approved window rail colour and slot name.

The white 200 mm slab above the crown is context, and its underside is at Y 4.30 m.

There is no wall light or additional accent material.

## Envelope and joints

| Constraint | Concept geometry |
| --- | --- |
| 4 m piece bounds | X 0-4.00, Y 0-4.30, Z -0.20-0 m |
| 2 m piece bounds | X 0-2.00, Y 0-4.30, Z -0.20-0 m |
| Inset white field, 4 m piece | 3.40 x 3.50 m |
| Inset white field, 2 m piece | 1.40 x 3.50 m |
| Pivot | Original front-left grid point, (0,0,0) |
| Proud additions | 0 mm |
| Minimum continuous opaque web | 70 mm behind the navy trough |
| White field thickness | 120 mm |
| End snap faces | Complete flat 0.20 x 4.30 m rectangles |
| Floor and top snap faces | Complete flat run x 0.20 m rectangles |
| Collider requirement | Original run x 0.20 x 4.30 m box |

Two 200 mm side frames meet as one 400 mm white pier at each straight joint.

The 2 m piece uses the same frame dimensions and a narrower white field.

For the staged inside corner, the straight run ends at X 10 m and the return occupies X 9.80-10.00 m, Z -4.00-0 m.

The 200 mm end frames coincide inside the existing 0.20 x 0.20 m overlap.

The white crown and skirting meet continuously around that square, and the inset field borders remain on their respective walls.

There is no special corner piece, corner cap, trimming operation or end variant.

## Review

Start with [presentation.png](presentation.png), then [sheet.png](sheet.png) and the enlarged [profile_sheet.png](profile_sheet.png).

The room camera is at 1.70 m and the matte Deep Navy suit is 1.80 m tall.

The skirting close-up is 2.67 m from its target, and the head close-up is 2.79 m from its target.

The full-module frames create a formal, deliberately panelled service spine.

Twenty repetitions would create a denser vertical rhythm than Floating Field, especially when many 2 m infills are used.

The pale inset field holds the suit silhouette clearly in the inspected room and corner renders.

These render meshes express the visual and snap constraints for concept approval.

Production collider setup, Unity lighting and gameplay require validation during implementation.
