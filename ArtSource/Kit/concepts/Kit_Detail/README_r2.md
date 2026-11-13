# Office kit detail · Round 2

**Status** · Target for Alex’s review · 12 November 2026

[Contact sheet](contact_sheet_r2.png) · [Round 2 brief](FEEDBACK_01.md) · [Art bible](../Office/refs/art_bible.txt) · [Approved office kit](../Office/DECISION.md)

Each sheet pairs two directions with a sunlit hero at 1,700 mm eye height and a rival view from 12,000 mm across the street covering three floors. These are concept views. Some rival views compress the sill and slab proportions to expose the interiors. Use the dimensions below for modelling and check camera distances and glass readability in Unity.

Keep the kit envelopes, pivots, snap faces and all colliders. Recess cuts into exposed finish faces, leaving the mating lands intact, and limit any new projection to 15 mm. Keep the 600 mm sill, 4,300 mm floor-to-soffit height, 200 mm slab and 1,200 × 2,400 mm service door opening. Keep `Glass_Clear` near-clear with one broad and one thin diagonal sheen streak. Maintain the knee-to-head sightline and use dark-suit readability through two panes as the modelling check. [Kit dimensions](../../README.md) · [Glass specification](../../../../docs/systems/GLASS.md)

## 01 · Façade

[Full-size sheet](01_Facade/sheet_r2.png) · **1 image try**

| Direction | Kit pieces and moves in mm | Materials |
| --- | --- | --- |
| **A · Cobalt knife piers** | `Column_Facade`, `Column_CornerL/R`, against `Window_4m` and `Window_2m` trays. Shape the 160-deep street pocket with a 120-wide angled facet beside a 220-wide cobalt face. Keep the full-depth 20-wide side lands and 140-high head land. Stack selected cobalt faces vertically. | `Accent_Cobalt` on the street face and `Wall_PaperWhite` on the facet. |
| **B · Cyan tray heads** | `Window_4m`, `Window_2m` and matching column head returns. Cut a 150-high × 75-deep pocket into the visible face just below the soffit land. Set a 10-high light line at its rear and align it across the bays. | `Trim_Navy` pocket and `Light_CyanHairline` diffuser (proposal). |

**Build A.** The facet throws a clean shadow by day and the cobalt face gives the tower a vertical signature from across the street.

## 02 · Walls and door

[Full-size sheet](02_Walls_Door/sheet_r2.png) · **3 image tries**

| Direction | Kit pieces and moves in mm | Materials |
| --- | --- | --- |
| **A · Floor index** | `Wall_Solid_*` and the field next to `Wall_Door_4m`, with `Door_Leaf` in silver. Set a 1,700-high numeral, 120-high firm type, 25-high legal copy and a 3-wide datum into the spine composition. Author the floor code and firm lettering at room-set level. | `Wall_PaperWhite`, `Door_Leaf` and `Sign_Navy` flush graphic (proposal). |
| **B · Cobalt portal diagonal** | `Wall_Solid_4m_Cobalt`, `Wall_Door_4m_PortalHead` and `Door_Leaf`. Register a 900-wide diagonal across the 12,000-wide spine run, across the face of the closed leaf and clear of the latch gap. Repeat the complete graphic on each floor. Retain the 30-wide × 15-deep doorway reveal. | `Wall_PaperWhite`, `Door_Leaf` and `Sign_Cobalt` flush graphic (proposal). |

**Build A.** The floor code gives players an immediate landmark and leaves the red stair door visually distinct.

## 03 · Stair

[Full-size sheet](03_Stair/sheet_r2.png) · **1 image try**

| Direction | Kit pieces and moves in mm | Materials |
| --- | --- | --- |
| **A · Cobalt helix** | `Stair_Steps`, including its existing handrail, with `Stair_Column` and `Stair_CagePanel` in context. Wrap the outer 350 of each 50-thick open tread in cobalt within the tread profile. Set a 10-high cyan edge flush into the landing nosing only. Keep radii of 800 at the core, 2,600 at the treads, 2,630 at the handrail and 2,750 at the cage centreline. | `Stair_Aluminium`, `Accent_Cobalt`, `Wall_PaperWhite`, `Glass_Clear` and `Light_CyanHairline` (proposal shared with façade B). |
| **B · Red lock panel** | `Stair_LockedDoor`. Keep the 1,750-wide × 4,200-high × 200-deep body. Apply one 900-wide white lock mark and one 150-wide white diagonal hazard rule flush to the red face. | `Door_LockedRed`, existing `Trim_Navy` lock plate and `Sign_White` flush graphic (proposal). |

**Build B.** The lock mark tells players the route is blocked through the glass and across the street.

## 04 · Ceiling and floor

[Full-size sheet](04_Ceiling_Floor/sheet_r2.png) · **3 image tries**

| Direction | Kit pieces and moves in mm | Materials |
| --- | --- | --- |
| **A · Lit soffit cove** | Interior soffit faces on `Slab_*` and a recessed visual variant of `Ceiling_LightStrip`. Carve a 250-wide × 100-deep cove within the 200 slab depth and conceal a 20-wide cool-white diffuser behind one edge. Carry it across selected bays parallel to the façade, keeping the exterior slab edge and its mating surfaces intact. | `Ceiling_Tile`, `Wall_PaperWhite`, `Light_Strip` and `Trim_CoveSatin` liner (proposal). |
| **B · Cobalt carpet route** | `Slab_*` floor finish and `Slab_Stairwell` approach. Make a 2,000-wide route from the windows to the stair entry using four rows of 500 × 500 tiles. Match the existing quarter-turn pattern and finish height. Keep the 500 × 500 ceiling grid and standard strips. | `Floor_Carpet`, `Ceiling_Tile`, `Light_Strip` and `Floor_Carpet_Cobalt` (proposal). |

**Build B.** The route guides players to the stair with a surface assignment and leaves the ceiling free for the existing fixtures.

## 05 · Glass partition

[Full-size sheet](05_Partition/sheet_r2.png) · **2 image tries**

| Direction | Kit pieces and moves in mm | Materials |
| --- | --- | --- |
| **A · Room-name band** | `Partition_Glass_4m` and `Partition_Glass_2m` at the butt-jointed corner. Carry a 300-high cobalt band at 2,100–2,400 above the floor around the square corner, with 100-high knockout room lettering and a 4-wide rule. Keep both existing 80-high tracks. | `Glass_Clear`, `Trim_Navy` and `GlassPrint_Cobalt` flush print (proposal). |
| **B · Corner brackets** | Same pieces. Set a 1,000-long × 300-high angular mark on each return of the corner, all within the same 2,100–2,400 height range. Place 100-high navy room lettering on the adjacent clear glass within the high band. | `Glass_Clear`, `Trim_Navy`, `GlassPrint_Cobalt` and `GlassPrint_Navy` flush prints (proposals). |

**Build B.** The brackets mark the room corners and keep long runs of glass visually quiet across the gap.

**Image generation** · 10 built-in ImageGen tries for the five family sheets, including five revisions. The contact sheet assembles the selected renders. The prompt set used the ten directions and dimensions above, the matched camera briefs, the art bible’s city and atrium plates, approved office concepts and Unity blockouts. Shared art instructions called for layered views, raking sun and hard shadows, satin paper-white surfaces, large cobalt fields, and clear two-streak glazing.
