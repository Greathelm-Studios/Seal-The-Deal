# Current KitOffice diagnosis · L1

**Status:** Current source/capture diagnosis; replacement layouts are Target concepts.

**Last updated:** 2026-11-15 · Prepared for Alex · No Unity changes.

**The floor reads as a collection of kit demonstrations because its obstacles have names but no building functions.**

The approved materials and the feature stair already work as an identity; the missing layer is a believable office address and a dependable circulation hierarchy.

## Evidence boundary

Read the complete [scene builder](../../../../Assets/_Project/Editor/Art/KitOfficeScene.cs), [kit README](../../README.md), [r5 decision](../Kit_Detail/DECISION_r5.md), [r5 handoff](../Kit_Detail/README_r5.md), [movement contract](../../../../docs/systems/PLAYER_MOVEMENT.md) and [GDD](../../../../docs/GDD.md).

Reviewed all **17 supplied captures**, including the seven current L1–L7 labyrinth views, in [source_capture_review.png](source_capture_review.png).

The five A/B/E/G/H views show the **older spiral/tower layout**; they are historical comparisons, not evidence of the current labyrinth's connectivity.

The lightbox, lift, water-wall and roof views are detail context; only L1–L7 show the current floor broadly.

The source builds a **32 × 24m plate**, two office levels at **0 / 4.5m**, an **8 × 12m atrium** at X12–20, Z−4 to−16, and roof at9m.

These are source dimensions, not dimensions estimated from perspective captures; screenshots do not prove collision, routes, balance or movement feel.

## Why it feels randomly placed

| Finding | Concrete evidence | Consequence / direction for L1 |
|---|---|---|
| No recognisable service core | The lift is a single 4m cell beside reception; the builder supplies no grouped WC, riser, plant and enclosed escape-stair accommodation. | The solid centre does not explain itself as a building; group service functions and repeat them vertically. |
| Maze islands are not office rooms | `Island()` closes four rounded returns around a 4 × 4m block; L gets one at(4,−8), R gets two at(22,−8)/(26,−14). | A player cannot infer a room, tenant or service behind these masses; replace their role with identifiable core walls, meeting rooms and attached storage. |
| Districts describe kit families | Builder groups are literally “corridor loop”, “meeting room”, “glass cuts”, “atrium” and “split junction”. | L/R/C/H/A do not yet form equal office districts; make names correspond to workplace areas and their fittings. |
| One bright path dominates | The entire north edge is a 32 × 4m window corridor; both spawns are there, at(4,−2)/(28,−2). | The first spatial instruction is to run at the other spawn along the same edge; interior flanks are secondary and harder to discover. |
| No continuous, recognisable ring | L's island loop, R's split junction and A's alternating panes use different path grammars, divided by the atrium and the X20 wall/door. | Local loops exist, but do not teach a dependable floor-wide circuit; give every branch a return to one recognisable route. |
| A room can become a trap | C's curved glass suite has the east 2m mouth, glazing to the north, and perimeter walls south/west; no second ordinary exit is authored. | A good-looking meeting room lacks a reliable alternate retreat; give every playable enclosed suite two permanent exits. |
| Glass is asked to do several jobs | L4/L6 show glass, high frost, room headers, a solid return and open cells in quick succession. | Visibility, walking access and ballistic cover need separate cues; glass reveals opponents and blocks walking while intact, but never substitutes for hard cover. |
| Furniture does not organise the floor | Six workstation benches are upstairs and one downstairs; C is a meeting suite, while R remains two sealed islands beside an isolated work bench. | There is no clear work-side/meeting-side/service-side hierarchy; use the windows for work and inner/corner suites for meetings. |
| Upper floor teaches another map | The source explicitly makes the upper floor open “with cover over the rooms below”; L7 shows workplace benches where lower-floor walls and islands do not repeat. | The next storey loses the learned landmarks; keep the shell, core, landing ports and primary routes fixed while fit-out varies. |
| Vertical choice is weak | One exposed 2m straight stair links the two office floors; the lift stops at1F and RF and has no upper-office stop. | This is a showcase stair plus a roof shortcut, not a multi-floor arena with independent enclosed flanks. |
| Surface detail outruns topology | L1/L2/L5 repeat glossy returns, ceiling panels, glass grids and ribbons; L3 has the strongest actual spatial landmark. | Preserve the look and stair silhouette; spend added geometry on room purposes, entrances and route termination before more decoration. |

## Keep

**Keep the open feature stair, bright perimeter, white/cobalt masses, pale floors, clear glass and large district graphics.**

L3 already supplies an orienting vertical landmark, and L4 already explains “meeting room” through its furniture and curved glass.

The L1 alternatives change organisation rather than replacing the approved art language.

**Suggestion:** explain every full-height opaque mass as stairs, services, a meeting-room wall or attached office storage; a blank sealed maze island has no useful office story.

## Mismatches and handoff limits

**Game mode:** the GDD still specifies two towers, cross-gap combat, the open spiral and locked stair progression; this brief specifies one building, a shared case and several vertical routes.

The **L1 brief governs these requested concepts**; the GDD remains the implementation contract until Adam reconciles the mode, unlocks, spawn/respawn rules and roof win.

**Lift:** GDD access-card lifts are roadmap; the current art scene has a development lift; L1 reserves a shaft and shows a possible route, without approving a new gameplay feature or floor-skipping rule.

**Movement:** the movement doc gives 6/9m/s, 0.7m capsule width, 1.6m jump and 0.4–1.6m mantle; it does not specify a slide state, so no option requires a slide-only route.

**Loose props:** the kit README counts **115 loose kit objects** in the current office, before rooftop extras, against the stated **16-per-floor simulation cap**; which are actually networked is not established by these captures.

L1 budgets **12 loose objects per floor**; fixed/integrated office dressing must not silently become additional loose physics objects.

**Documentation:** early kit README carpet/dark-floor and “not imported” wording is older than its later polished-white/import notes and current captures; the dated AEROSET/PANLINE notes and current captures are the better evidence for this art pass.

The movement doc's “no ragdoll / procedural animation” limitation also conflicts with the newer shared animation decision; this does not affect the measured movement envelope used here.

**Schedule:** the GDD's October dates are a historical snapshot at this November review; this concept pass does not invent a replacement release schedule.

No board, owning system doc, scene, prefab, kit source or gameplay rule was edited; all task-authored files remain under this folder as requested.

**Next:** review [the L1 comparison](README_L1.md), then carry the game-mode mismatch to Adam before a selected layout is built.
