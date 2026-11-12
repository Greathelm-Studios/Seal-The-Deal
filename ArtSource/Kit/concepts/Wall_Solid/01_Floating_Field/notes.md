# 01 / Floating Field

**Status:** Target concept for Alex's review

**Last updated:** 2026-11-11

A broad Paper White wall floats between two deep navy recesses, extending the approved White Trays language into the service spine.

The main field stays continuous across the 4 m, 4 m and 2 m modules, and both bands turn the inside corner.

## Profile and materials

Coordinates in this table use Unity axes, with Y up and the room face at Z -0.20 m.

The opposite room face has the mirrored profile.

| Detail | Height / extent | Recess into each face | Material |
| --- | --- | --- | --- |
| Floor snap cap | Y 0.00-0.02 m | 0 mm | Wall_PaperWhite, #F3F4F4 |
| Navy skirting recess | Y 0.02-0.20 m, 180 mm high | 65 mm | Trim_Navy, #111B38 |
| Uninterrupted white field | Y 0.20-4.08 m, 3.88 m high | 0 mm | Wall_PaperWhite |
| Navy head band | Y 4.08-4.28 m, 200 mm high | 65 mm | Trim_Navy |
| Soffit snap cap | Y 4.28-4.30 m | 0 mm | Wall_PaperWhite |
| Full-depth end lands | X 0.00-0.012 m and run-0.012 to run | 0 mm | Follows the height zones above |

Trim_Navy is proposed for the solid wall's material assignment, using the approved window rail colour and slot name.

The white 200 mm slab above the head is context, and its underside is at Y 4.30 m.

There is no wall light or additional accent material.

## Envelope and joints

| Constraint | Concept geometry |
| --- | --- |
| 4 m piece bounds | X 0-4.00, Y 0-4.30, Z -0.20-0 m |
| 2 m piece bounds | X 0-2.00, Y 0-4.30, Z -0.20-0 m |
| Pivot | Original front-left grid point, (0,0,0) |
| Proud additions | 0 mm |
| Minimum continuous opaque web | 70 mm through the recessed bands |
| End snap faces | Complete flat 0.20 x 4.30 m rectangles |
| Floor and top snap faces | Complete flat run x 0.20 m rectangles |
| Collider requirement | Original run x 0.20 x 4.30 m box |

The 12 mm end lands make a 24 mm-wide flush navy sleeve where two pieces meet.

These sleeves are visible in the close-ups and keep each complete snap end intact.

The white field has no vertical score, bevel or frame at the joint.

For the staged inside corner, the straight run ends at X 10 m and the return occupies X 9.80-10.00 m, Z -4.00-0 m.

Their original wall volumes overlap over a 0.20 x 0.20 m square.

The main fields meet at the visible room corner, and the equally recessed navy bands meet deeper in that overlap.

There is no special corner piece, corner cap, trimming operation or end variant.

## Review

Start with [presentation.png](presentation.png), then [sheet.png](sheet.png) and the enlarged [profile_sheet.png](profile_sheet.png).

The room camera is at 1.70 m and the matte Deep Navy suit is 1.80 m tall.

The skirting close-up is 2.67 m from its target, and the head close-up is 2.79 m from its target.

This is my preferred direction because the long white field and dark head/foot suit twenty repeated modules without adding a competing vertical rhythm.

The visible navy snap sleeves are the main compromise.

The pale field holds the suit silhouette clearly in the inspected room and corner renders.

These render meshes express the visual and snap constraints for concept approval.

Production collider setup, Unity lighting and gameplay require validation during implementation.
