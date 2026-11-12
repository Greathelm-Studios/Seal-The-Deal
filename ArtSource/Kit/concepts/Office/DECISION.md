<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-12
  Prompt    : Alex picked "White Trays + Portal" from the office concepts
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : Records which office direction Alex approved and how each concept move maps to kit pieces,
              so the kit, the README and later room sets answer to one written decision, not to a chat.
-->
# Office direction — decision

**2026-11-12 · Alex:** build the kit to **01 White Trays + 02 Cobalt Portal**: White Trays for the façade, walls, glass and ceiling, with Cobalt Portal's cobalt frame on the spine wall behind the stair. Luminous Grid is not taken.

## What that means for the kit

| Concept move | Kit pieces | Notes |
|---|---|---|
| White sill shelves over dark openings, navy column slots | `Window_4m`, `Window_2m`, `Column_*` | Already built to the window-bay White Trays study (Window_Bay_R3); unchanged |
| Quiet white walls with clean shadow lines | `Wall_Solid_*`, `Wall_Door_4m` | 80 mm navy shadow-gap skirting and a 40 mm soffit reveal on both faces; 30 mm reveal round doorways |
| Cobalt portal round the stair | `Wall_Solid_4m_Cobalt`, `Wall_Solid_2m_Cobalt`, `Wall_Solid_4m_PortalHead`, `Wall_Door_4m_PortalHead` | Spine run behind the stair: Cobalt, PortalHead door, Cobalt. Cobalt on the main-floor face only |
| Slender navy glass tracks | `Partition_Glass_*` | Rails Trim_Navy |
| White column, open metal treads, glass drum | `Stair_Column`, `Stair_Steps` (new), `Stair_CagePanel` | Ramp stays the walking collider with its renderer off; panels curved |
| Red locked door across the stair | `Stair_LockedDoor` (new) | Same envelope as the greybox door; a full-height slab, not the concept's short gate, so it can't be jumped |
| Cheap ceiling tiles and strip lights | `Ceiling_LightStrip` (new), `textures/Ceiling_Tile.png` | 0.5 m grid; four strips per 4 m bay |
| Darker grey-blue carpet tiles | `Floor_Carpet` #646E86, `textures/Floor_Carpet.png` | Quarter-turned 0.5 m tiles, low contrast |

New material slots: `Accent_Cobalt`, `Light_Strip`, `Door_LockedRed` (and `Trim_Navy`, already added for the window bay).

**2026-11-12 · Alex:** taller glass, less wall: the façade sill drops from 1 m to **0.6 m** (glass 3.2 → 3.6 m tall). Changed in the kit (`SILL`), `GreyboxTowerSetup.SillHeight` and TOWER.md. The sill is still a ledge and low cover, but less of it: the window fights get more exposed.

Not kit work (room-set layer): the oversized floor number, firm branding, furniture, the copier and water cooler.
