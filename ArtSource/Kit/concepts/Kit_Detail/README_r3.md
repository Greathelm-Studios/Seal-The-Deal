# Office kit detail · Round 3

**Status:** Target for Alex’s review · **Last updated:** 2026-11-12

[Contact sheet](contact_sheet_r3.png) · [Governing brief](FEEDBACK_02.md) · [Exact prompt set](prompts_r3.json) · [200 px direction check](thumbnail_check_r3.png)

Five families, ten directions, twenty concept views, generated with the built-in ImageGen tool.
The tool returned each family sheet at 1,672 × 941 px; the assembled contact sheet is 3,840 × 2,160 px.
Each sheet puts A on the left and B on the right, hero above and rival below, with a one-line caption for each direction.
The hero brief is 1,700 mm eye height; the rival brief is 12,000 mm across the street, covering three floors.
These are visual concepts, not measured drawings or engine captures: the dimensions below govern modelling, rather than measurements inferred from painted perspective.
Camera distance, mesh bounds and two-pane visibility have not been measured in Unity.

The main reference is [bible plate 04](../Office/refs/bible_ref_04.png): folded white masses, whole cobalt faces, dark slots and a strong three-floor rhythm.
The other three bible plates informed restrained graphic composition, and the [atrium plate](../Office/refs/plate_law_firm_atrium.png) informed materials and depth.
Round 2’s [contact sheet](contact_sheet_r2.png) and [README](README_r2.md), the approved office paintings, the Unity office captures, the kit dimensions and glass specification were reviewed before generation.
[FEEDBACK_02.md](FEEDBACK_02.md) takes precedence over the earlier briefs.

## Modelling boundaries

All moves below are proposed visual variants inside the existing outer envelopes, with the original pivots, snap ends, floor/soffit contact lands and colliders retained.
No new projection is needed; the permitted ceiling remains 15 mm, away from snap and contact faces.
Preserve the 4,000 mm bay, 600 mm façade sill, 4,300 mm floor-to-soffit height, 4,500 mm storey, 200 mm slab and 1,200 × 2,400 mm service opening.
Recess depths are measured inward from the original exposed face; leave the opposite finish intact and do not combine opposing deep cuts through the same thin piece.
Keep end lands and route recesses continuous across module seams without cutting the mating surfaces.
The [kit README](../../README.md) and [kit source](../../kit_pieces.py) supply the existing envelopes and assembly dimensions.

The partition remains 2,500 mm high × 100 mm deep, with 1,800 mm of air above it; “storey-scale” does not mean increasing its envelope.
Keep all partition glass between 450 and 2,100 mm unobstructed.
The carpet stays level through the room; a partition base is a narrow vertical frame, never a raised room platform.
The stair helix is cut into the solid core, rather than made opaque on cage glass, to retain the outer walking annulus sightline.
Keep the existing stair core/tread/rail/cage radii of 800 / 2,600 / 2,630 / 2,750 mm.

Keep [Glass_Clear](../../../../docs/systems/GLASS.md) near-clear, with one broad and one thin translucent diagonal streak.
Near-black values belong in cut reveals and grazing edges, without making the panes mirror-dark.
Dark figures are visible in the concept views; this is a visual review of still images, not proof of gameplay readability through two panes.

## 01 · Façade

[Sheet](01_Facade/sheet_r3.png) · **2 image tries**

| Direction and idea | Kit pieces changed | Proposed form moves, mm | Materials |
| --- | --- | --- | --- |
| **A · Folded cobalt piers** — a sharp white knife exposes a cobalt pocket, repeated vertically. | `Column_Facade`, `Column_CornerL/R`, head finish of `Window_4m/2m`. | Retain 400 × 240 × 4,300 column bounds; street pocket 160 deep, from 660 to 4,160 high, 20 side lands and 140 head land; 280-wide cobalt back and an angled white return. Turn the field onto a 200-high head zone within the original bay envelope, cutting at most 80 inward. | Existing `Wall_PaperWhite`, `Accent_Cobalt`, `Trim_Navy`; `Glass_Clear` unchanged. |
| **B · Stacked shadow slots** — white floors become sharp bands over black undercuts. | Street edges of `Slab_*`, head finish of `Window_4m/2m`, corner returns. | Inside the 200 slab, cut an 80-high × 120-deep undercut with an inclined remaining lip; retain the original leading plane and top skin. Head recess 100 high × 80 deep at 4,200–4,300, preserving the contact land. A 10-high cyan line sits at the rear of one selected reveal; a whole cobalt end-bay return provides the single large colour field. | Existing white, cobalt and navy; **proposal:** `Light_CyanHairline`. |

**Build A:** the folded pier is the clearer silhouette change without reworking every slab.
B is the bolder horizontal alternative, with its depth taken from shadow rather than oversized beams.

## 02 · Walls and door

[Sheet](02_Walls_Door/sheet_r3.png) · **1 image try**

| Direction and idea | Kit pieces changed | Proposed form moves, mm | Materials |
| --- | --- | --- | --- |
| **A · Carved floor index** — the floor code becomes architectural negative space round a corner. | `Wall_Solid_*`, adjacent `Wall_Door_4m` finish; room-set floor-code composition; `Door_Leaf`. | Floor code 4,200 high × approximately 3,600 wide, 50 clear at top/bottom, cut 120 into the 200 wall. Cobalt pocket backs wrap the return; white lands remain at the original face. Keep the 1,200 × 2,400 service opening and 40-thick silver leaf; any handle cup cuts only 12 into the leaf. | Existing `Wall_PaperWhite`, `Accent_Cobalt`, `Trim_Navy`, `Door_Leaf`. |
| **B · Excavated Z** — one diagonal void links wall, portal composition and ceiling. | `Wall_Solid_*_Cobalt`, `Wall_Door_4m_PortalHead`, adjoining wall faces and underside of `Slab_*`; leaf unchanged. | Across the 12,000-wide spine composition, a 1,500-wide diagonal pocket cuts 120 into the wall with folded white sides. Its upper return meets the portal head and a 2,400-wide ceiling pocket, 100 upward into the 200 slab. Preserve the service door opening and latch clearance; carve the fixed surround, not through the moving leaf. | Existing white, cobalt, navy and silver; **proposal:** `Light_CyanHairline`, 10 wide at one rear high edge. |

**Build A:** it turns navigation into the memorable mass and keeps the service door ordinary.
Choose B if a single abstract architectural mark is preferred to the explicit floor code.

## 03 · Stair

[Sheet](03_Stair/sheet_r3.png) · **2 image tries**

| Direction and idea | Kit pieces changed | Proposed form moves, mm | Materials |
| --- | --- | --- | --- |
| **A · Carved red stop slab** — a deep lock and hazard groove make the blocked route the hero. | `Stair_LockedDoor`; one existing spine face as supporting context. | Keep the 1,750 × 4,200 × 200 door body and original pivot. Lock-shaped pocket 1,000 wide × 1,500 high × 65 deep, white at the pocket back, 30 edge bevel. One 180-wide × 45-deep diagonal hazard groove turns onto the door side inside the envelope; keep its back behind the original face. Existing navy lock hardware stays within the original allowance. | Existing `Door_LockedRed`, `Wall_PaperWhite`, `Trim_Navy`; one subordinate `Accent_Cobalt` wall pocket. |
| **B · Excavated cobalt helix** — the solid white core itself becomes the three-floor landmark. | `Stair_Column` visual surface only; steps, rail and cage retained. | A 1,000-high helical channel turns once per 4,500 rise, 160 inward from radius 800 to 640, with 40 bevels. Uncut white lands preserve the original radius and top/bottom contact faces. Phase the channel across the storey seam while leaving mating faces intact. No opaque band on the cage; no cobalt shelf outside the core; plain full-height red leaf remains. | Existing `Wall_PaperWhite`, `Accent_Cobalt`, `Trim_Navy`, `Stair_Aluminium`, `Glass_Clear`, `Door_LockedRed`. |

**Build B:** it is the strongest new landmark in the set and changes the three-floor read without screening the walking annulus.
A is the stronger local locked-route signal; the two concepts are alternatives here, not a recommendation to stack both hero treatments.

## 04 · Ceiling and floor

[Sheet](04_Ceiling_Floor/sheet_r3.png) · **2 image tries**

| Direction and idea | Kit pieces changed | Proposed form moves, mm | Materials |
| --- | --- | --- | --- |
| **A · Floor-to-wall route** — carpet, overhead void and a whole wall return become one route. | Floor finish and underside of `Slab_*`, `Slab_Stairwell` approach, existing spine return; recessed visual variant of `Ceiling_LightStrip`. | A flush 2,000-wide route uses four 500-wide carpet rows. Above it, a 2,000-wide × 120-deep upward soffit slot leaves 80 of the slab above. A 10-wide line sits at its rear. At the stair, the route climbs the existing 4,000 × 4,300 wall face with a 120-deep return cut; no step in the floor. | Existing `Floor_Carpet`, `Ceiling_Tile`, white, navy and cobalt; **proposals:** `Floor_Carpet_Cobalt`, `Light_CyanHairline`. |
| **B · Tilted white coffers** — shallow sliced planes cast a strong sawtooth shadow. | Underside of `Slab_*`, with existing end-wall finish. | Pockets 2,000 wide ramp upward from 4,300 to 4,440 over 2,000 run: 140 maximum cut, about 4° slope, 60 minimum skin. White lands remain at the original 4,300 soffit, with the exterior slab edge still 200 total. Keep the quiet 500 tile grid outside the relief and one whole cobalt end face/return; a 10-wide cyan line is limited to one end. | Existing `Ceiling_Tile`, `Wall_PaperWhite`, `Floor_Carpet`, `Accent_Cobalt`, `Trim_Navy`; **proposal:** `Light_CyanHairline`. |

**Build A:** the connected floor, void and wall gives players a stronger route than the round-2 carpet stripe.
B is the more sculptural ceiling option and needs careful control of slab depth when modelled.

## 05 · Glass partition

[Sheet](05_Partition/sheet_r3.png) · **3 image tries**

| Direction and idea | Kit pieces changed | Proposed form moves, mm | Materials |
| --- | --- | --- | --- |
| **A · Folded cobalt corner frame** — a carved L-shaped head makes the clear enclosure a volume. | `Partition_Glass_4m/2m` header/base visual variants at the butt-jointed corner. | Upper 400-high mass at 2,100–2,500 wraps the 4,000 front and 2,000 return; carve 65 into the existing 100 depth, leaving 35 back skin. A white knife/chevron stays at the original plane, with cobalt behind it. Lower vertical band is 250 high × 100 deep; no opaque element in the 450–2,100 sightline. | Existing `Accent_Cobalt`, `Wall_PaperWhite`, `Trim_Navy`, `Glass_Clear`. |
| **B · Carved white room box** — white negative cuts frame one cobalt interior face. | Same partition pieces plus the existing rear `Wall_Solid_4m_Cobalt` face. | Upper white 400 at 2,100–2,500 has a 70-deep folded cut, retaining 30 skin. Lower white vertical plinth is 450 high, cut at most 60 into its 100 depth, retaining 40; its narrow top remains level. Existing 4,000 × 4,300 rear spine face is cobalt with a 120-deep return. Floor stays continuous and the open air above the partition remains 1,800. | Existing white, cobalt, navy and near-clear glass; **proposal:** `Light_CyanHairline`, 10 wide in one high pocket. |

**Build B:** the white carved enclosure and cobalt interior are the clearest volume change, with the standing figure placed against a pale side return.
Keep its 2,500 height; do not turn the painted framing into a full-height room or a raised deck.

## Generation and review

**10 built-in ImageGen tries:** façade 2, walls/door 1, stair 2, ceiling/floor 2, partition 3.
The five revisions corrected façade proportions, inward stair relief, soffit depth, partition height/floor level, and partition presentation.
The [prompt set](prompts_r3.json) contains the exact initial and revision prompts and the selected output for each family.
The contact sheet and thumbnail check are deterministic layouts of the selected renders; they do not repaint their contents.

Reviewed all twenty selected views at sheet size and all ten direction columns at 200 px width.
Checked distinct A/B silhouettes and value patterns, hard shadow edges, cobalt hierarchy, clear figure sightlines, three-floor repetition and absence of plants, grime and decorative bars.
Small relief depths, painted proportions, exact projection, glass sheen strength and camera distances remain concept approximations; the numerical targets above govern the models.
No Unity assets or system behaviour changed, so the existing system docs need no update.
Round 1, round 2 and the briefs are retained unchanged.

Read-only checks: `Get-FileHash -Algorithm SHA256` compared all 17 pre-existing brief/round-1/round-2 files with the starting hashes; `[System.Drawing.Image]::FromFile(...)` checked each final PNG’s dimensions.
No automated tests, Play Mode, multiplayer or performance checks were run.

