# FLOOR_GEN

**Status:** Current · **Last updated:** 2026-11-24

The floor generator for the floor race ([FLOOR_RACE.md](FLOOR_RACE.md)). The "Measured" section says what has been checked and how. Layout drawings: [seed 1, floor 1](../proof/floor_race_2026-11-22/layout_seed1_floor01.png), [seed 77777, floor 10](../proof/floor_race_2026-11-22/layout_seed77777_floor10.png) (grey = furniture, red = open window, yellow = exit lift, orange = arrival lift, green and magenta = the two starts, gold = the briefcase, blue room = the manager's office).

## What the player sees

Every floor of the building is a different office plan made from the KitOffice kit, in one of four layout types picked by the seed: a corridor loop you can always find again, named rooms off it, window halls along the glass, and a lift hidden somewhere. Each room's name is over its door, service rooms stand out in cobalt with the floor number on them, and desks are dressed as they are in KitOffice. Floor 10 has the manager's office behind a cobalt portal door. Both players in a match see exactly the same floor.

## Rules it follows

- **Same floor on every machine.** A layout is a pure function of `(match seed, floor number)`. The host syncs only those two numbers ([FLOOR_RACE.md](FLOOR_RACE.md)); every machine runs the generator and builds the floor locally. Nothing about a floor is streamed (`AGENTS.md §3`).
- **The kit is never edited.** Generation picks kit prefabs and places them (`AGENTS.md §4`). Which prefab is which piece is listed in one asset, `Assets/_Project/Profiles/FloorRace/FloorKit.asset`, filled by a menu tool.
- **L2's rules are the constraints** ([DECISION_L2.md](../../ArtSource/Kit/concepts/Layout/DECISION_L2.md)): a ring you can find again, every room with two exits, named rooms.

## Kit pieces as tiles

- **Plate:** 10 x 8 cells of 4 m (40 x 32 m, the L2 plate), tunable 8 to 12 by 6 to 10 on `FloorGenProfile`.
- **Cell:** one 4 x 4 m slab bay. A cell belongs to one room, or is a lift shaft.
- **Edge:** the 4 m line between two cells, or between a cell and the outside. An edge is the tile socket. It is one of:

| Edge | Kit pieces | Walk through |
|---|---|---|
| Open | none | Yes, full width |
| Solid wall | `Wall_Solid_4m` | No |
| Solid wall with door | `Wall_Door_4m` (door in the middle) | 1.2 m doorway |
| Glass, frost, fluted or louvre partition | `Partition_*_4m` | No |
| Partition with door | `Partition_Glass_Door_2m` + `Partition_*_2m` (door at 1 m or 3 m) | 1.2 m doorway |
| Office wall and portal door (floor 10) | `Wall_Solid_4m_Cobalt`, `Wall_Door_4m_PortalHead` | Door only |
| Facade: window, curtain wall, solid | `Window_4m` + `Column_Facade`, `Wall_Curtain_4m`, `Wall_Solid_4m` | No. A window bay is open above its 0.6 m sill: you can go out of it and die |
| Lift shaft, lift door | `Lift_Shaft_Wall_4m_Solid`, `Lift_Door_Frame_4m_1F` | Lift door only |

- A wall's thickness goes into the enclosed room, so corridors keep their full 4 m.
- **Lift tile:** one cell holding `Lift_Car` and two `Lift_Door_Panel` leaves, positioned as KitOffice places them (car centre 2.075 m in from the door line, leaves 0.225 m in).
- **Fit-out:** furniture is placed per cell from a small set of patterns per room kind (bench, meeting table, kitchen counter, copier and storage, counter with pod seats, planter). Patterns also hold the **authored spots**: where a player may spawn, where loot may lie, where a loose prop stands. Patterns are a code table in `FloorFitOut.cs` for now (see Known limitations).

## Dressing, matched to KitOffice

Added on 2026-11-24 after generated floors were put beside KitOffice and read as bare. Each rule copies what `KitOfficeScene.cs` does by hand.

- **Ceilings.** Planned for the whole floor in one pass once rooms and walls are known ([the ceiling plan](#the-ceiling-plan)). KitOffice's rules are the model: bare soffit with lit lines over circulation, a light line above the ring, floating panels only over rooms.
- **Room lights.** One shadowless spot under the ceiling of every closed room bay, placed by the ceiling plan, with KitOffice's light-pool values (intensity 6, range 6 m, 100 degree cone, its two firm tints by odd and even floor). The values are on `FloorKit.asset`.
- **Colour.** A room whose front is a solid wall shows `Wall_Solid_4m_Cobalt` to the walkway, and one of those walls per room carries the floor number 2.6 m tall. Bare solid outside walls get a `Lightbox_Wall_Wave` (40 in 100) or a poster (40 in 100), skipped where furniture stands against the wall.
- **Signs.** Each door off a walkway has the room's name over it: navy on white walls, white on the dark rail of a glass door. Each lift's indicator panel shows the floor ("5F"). Numbers and names are TextMesh Pro text in the UI's Saira face, so they are always right for the floor; KitOffice's own number, plate and lift textures say 1F and 2F and are not used.
- **Slogans.** Every coloured wall bay has a pale stripe at 3.95 m with one line from the wall-text list in `ArtSource/Kit/posters/COPY.md`, picked by the seed. The lines are a list on `FloorKit.asset`.
- **The ring band.** A 0.3 m cobalt stripe runs down the middle of the corridor loop, KitOffice's ring marking, so the loop can be found again from any corridor.
- **Daylight on the ceilings.** Where the sun lands inside each window bay, a shadowless upward spot stands in for bounced sunlight, with `BounceLight.cs`'s values (intensity 2.5, range 7 m). It reads the scene's sun, so a scene with none gets none.
- **Glossy floor.** Each floor has one box-projected reflection probe, rendered once when the floor is built, a face a frame. It is only made while **Realtime Reflection Probes** is ticked in Project Settings > Quality. Harvey had it switched on for the PC quality level on 2026-11-24; the Mobile level is still off, so floors are matte there. Unity's one-call cubemap capture was tried first and threw render errors under Forward+ lighting.
- **Piece labels.** The builder adds KitOffice's label panels to pieces as it places them: the dot band on glass partitions, the vending machine, water server and reception desk labels, the lightbox graphic. `Fill Floor Kit` copies the list from `KitSigns` into `FloorKit.asset` when it is empty, with the materials KitOffice's build already wrote to `Art/Signs`.
- **Desks.** A bench comes with keyboards, phones, document trays and PC towers as fixed pieces, and spots for four chairs and four monitors. The 16 loose props are still picked from all the spots on the floor; a chair or monitor spot that misses the cut gets the fixed `Chair_Task` or `Monitor_Flat` instead, so no desk is bare. A fixed chair closes the walk map round it, so nobody spawns inside one.
- **Cover.** The lone `Cover_Block_2m` in the court and the halls, which read as a stray white box, is now a `Counter_Capsule_2m` (the same crouch cover), with pod seats in the halls.
- **The manager's office** adds a wave lightbox behind the desk and two visitor seats.

## Layout types

Added on 2026-11-24. The floor's first random draw picks its type by the four weights on `FloorGen.asset` (40, 20, 20, 20). Every type keeps the ring, the two exits per room and every check; a type only changes where the ring sits and how the rooms round it are cut. [Drawings](../proof/floor_gen_2026-11-24/types_3_layout_drawings.png), [walked](../proof/floor_gen_2026-11-24/types_2_walked.png), [ceilings from below](../proof/floor_gen_2026-11-24/types_1_ceilings_from_below.png).

| Type | Ring | Rooms | Plays as |
|---|---|---|---|
| Core office | One or two bays in from the glass | The profile's own values | The floor as it was before types existed |
| Open plan | Two bays in from the glass | Window rooms up to one bay longer and at least 85 in 100 open; at least 40 in 100 core rooms open | Wide halls of desks, long sightlines, little to hide behind |
| Cellular | One or two bays in | No room longer than two bays; a quarter of the profile's open window rooms and no open core rooms | Small closed rooms, short sightlines, many doors |
| Gallery | On the glass, no window rooms | The whole middle is blocks of rooms cut by cross corridors | The most junctions; the corridor itself has open windows |

- A type changes only the values in the table. The rest of the profile applies to every type, so raising `Open Band Room Percent` still opens up every floor.
- On floor 10 the ring's north side stays two bays in for every type, because the manager's office needs that band.
- A floor is retried with a new type draw when it fails a check, so no type can leave a seed without a floor.
- Rejected for now: a different corridor topology per type (a spine, a figure of eight, twin cores with a court). Each needs the ring check and the fairness checks rewritten for its shape; these four reuse them unchanged.

## The ceiling plan

`FloorCeiling.Plan` runs once per floor, after walls and doors and before furniture. It draws no random numbers, so adding it changed no floor: the same seeds give the same rooms, starts and furniture as before. [Drawing of seed 1, floor 1](../proof/floor_gen_2026-11-24/ceiling_plan_drawing_seed1_floor1.png); before, after and KitOffice side by side are the `ceiling_*` sheets in the same folder.

- **The spine.** One lit line, 0.3 m wide, over the middle of every corridor. Over the ring it is a closed loop, the partner of the cobalt band on the floor. Every cross corridor's line runs into it, so the lit network overhead is the route network. East-west runs own each junction and north-south runs stop at their edge, so nothing overlaps.
- **Lanes.** Open halls and lounges keep the bare soffit with one 0.2 m line down the middle of each bay. A hall's lines follow the corridor it fronts (most shared bays wins), then its window band, then its longer side. A lane carries on through every neighbouring hall bay that runs the same way and stops 0.5 m short of a wall or of a hall whose lanes run across it.
- **Tiles.** Offices, kitchens, print rooms and archives get the flush `Ceiling_Grid_4x4` across the whole room and two rows of 0.2 m strips per bay, 1.25 m and 2.75 m in (the kit README's strip positions, so each sits inside a 0.5 m tile). Rows run the room's longer side, unbroken from 0.5 m off one wall to 0.5 m off the other; a square room follows the corridor it fronts. The first version followed the corridor in every room, which left a room one bay wide and two deep with four stubby rows across it.
- **Panels.** Meeting rooms and the manager's office keep the floating `Ceiling_Panel_4x4`, one per bay.
- **Lights.** One room light at the centre of every Tiles or Panels bay.

Why lines are drawn rather than placed as kit pieces: the kit's `Slab_4x4_LightLine` carries one slot straight through a bay, so it cannot turn a corner, join another line or stop short. With the direction chosen bay by bay, lines met at junctions from different ways, the ring was four separate pieces, and every room bay had its own panel ring (the `Before` column of the sheets). The plan's lines are a list on the layout (`CeilingLines`); `FloorBuilder` draws all of them as one mesh in the kit's `Light_Strip` material 12 mm under the soffit, one draw call a floor. Every bay now has a plain `Slab_4x4` overhead. `KitPiece.Slab_4x4_LightLine` stays in the list unused because `FloorKit.asset` stores prefabs by position in it.

Rejected: KitOffice's own rule of every line running east to west, wall to wall (lines would cut across the north-south corridors, which is what the first dressing pass set out to fix); pendants, beams and ceiling ribbons (KitOffice dropped them on 2026-11-15, [kit README](../../ArtSource/Kit/README.md), "Markings system"); one `Ceiling_LightStrip` prefab per strip (about 150 more objects a floor for the same picture).

`FloorCeiling.Check` lists anything wrong with a plan: a line through a wall or over the wrong kind of ceiling, a corridor link with no spine, a hall bay with no lane, a room bay with no light. The sweep runs it on every floor. It is not part of a floor's own pass or fail, so it can never change which attempt wins.

## The algorithm

Each step draws from one integer random stream seeded from `(seed, floor, attempt)`.

1. **Type and ring.** Pick the layout type, then a rectangle as far in from each facade as the type says. Its border cells are the ring corridor.
2. **Core.** The cells inside the ring. If the block is three or more cells deep, a cross corridor (the court) cuts it so no block is more than two cells thick. A second cross corridor may cut a wide block. Each block is then split into rooms of one to four cells.
3. **Outer band.** The cells between ring and facade are split into rooms along each side. Corner rooms always reach round to touch the ring.
4. **Lifts.** The exit lift takes a corner cell of a room (its door facing into that room, or onto a corridor). From floor 2 up, an arrival lift takes another, far from the exit.
5. **Room kinds and names.** Each room is open plan (a window hall or lounge, no wall to the corridor) or enclosed (open office, meeting room, kitchen, print room, archive), with a wall finish that suits it. Names come from kind and compass position ("North-East Kitchen").
6. **Walls and doors.** Edges are set from the two rooms either side. Then every room is given doors until it has at least two ways out, the first onto a corridor.
7. **Ceiling.** The ceiling plan above: spine, lanes, tiles, panels and room lights. No random numbers.
8. **Fit-out.** Furniture patterns are placed, skipping any piece that would stand in front of a door. Then the loose props are cut to the cap and the rest become fixed pieces, and the wall marks (lightboxes, posters, numbers, names) are chosen.
9. **Walk map.** The floor is rasterised to a 0.5 m grid with every wall and furniture footprint, the furniture grown by a player's radius.
10. **Starts, briefcase, spots.** Chosen on the walk map by real walking distance (below).
11. **Check.** If any guarantee fails, the attempt number goes up and the floor is generated again, up to 24 times. The same attempt wins on every machine because the check is part of the pure function.

## Guarantees and how each is met

| Guarantee | By construction | Checked in code |
|---|---|---|
| One connected walkable area | Every room touches a corridor and has a door onto it; the corridors form one network | Flood fill on the walk map from the first start: every room, door, lift door and spot must be reached. Furniture that breaks this is removed from that room first |
| A findable ring | The ring is a closed loop of open corridor cells | Each ring cell must reach both its ring neighbours without leaving the corridor |
| Every room has two exits | Door pass adds doors until two | Exit count per room |
| Fair start | The second start is picked so both starts are the same walking distance from the exit lift (or the office) within `Fair Start Tolerance` (6 m), and at least `Min Start Apart` (20 m) from each other | Distances re-measured on the final walk map |
| Elevator and briefcase take a search | Floor 1's briefcase spot is at least `Min Case To Lift` (20 m) of walking from the exit lift and the same distance from both starts within the tolerance | Same |
| Enough spawn spots | One spot per walkable cell (about 70 a floor), kept only if reachable | At least `Min Spawn Spots` (24) |
| Loot and prop spots within the caps | Loot spots are spaced at least 6 m apart; prop spots are cut to `Max Props` (16, the cap in [PROPS.md](PROPS.md)) | Count |

A floor that still fails after 24 attempts is used anyway, marked invalid, with the reasons logged. None has been seen in the sweeps below.

## Starts and lifts

- **Floor 1:** two walk-in starts, one per firm. No arrival lift. The briefcase lies at its hidden spot.
- **Floors 2 to 10:** the firm that brought the briefcase up starts inside the **arrival lift**; the other firm starts at the walk-in start. The arrival lift never leaves; its doors open after the head start.
- **Floors 1 to 9:** one **exit lift**, the way up.
- A floor is independent of the floors below it: the arrival lift is not where the last floor's exit lift stood. The ride is a loading screen, not a shaft ([FLOOR_RACE.md](FLOOR_RACE.md)).

## Floor 10's office

Floor 10 is generated like the others with two changes. The north band is forced two cells deep, and one 2 x 2 cell room in it (8 x 8 m) is the manager's office: solid facade behind it, cobalt walls and portal-head doors to the corridor, carpet, and a fixed fit-out (the long desk facing the doors, storage on both side walls, planters in the back corners). There is no exit lift. The office takes the exit lift's place in the fairness checks, and its floor rectangle is the win zone.

## Building it at runtime

`FloorBuilder` (in `Scripts/FloorRace/`) turns a layout into objects: one instantiate per placement under a floor root, plus each lift's car and two door leaves, plus a plain block under the floor standing in for the storeys below. Merging the still pieces with `StaticBatchingUtility.Combine`, as `TowerBuilder` does, is an option that is off (see Measured). Tear-down detaches and destroys the root and any merged meshes. Floors stand 9 m apart in height (floor `n` at `y = (n - 1) x 9`), so the next floor is built above while the old one still stands and nobody falls during the change.

## Cost per floor

The budget, and where the measured numbers below stand against it.

| Thing | Budget | Measured |
|---|---|---|
| Kit pieces instantiated | 350 to 500 before the dressing; not re-set since | 469 to 685 with the dressing |
| Generate (pure) | Under 5 ms | 5.1 to 5.4 ms with layout types (5.4 to 5.6 ms before them); over budget |
| Build | Under 150 ms, hidden by the transition screen | 10.9 to 14.4 ms with layout types |
| Draw calls | No number is set in [PERFORMANCE.md](PERFORMANCE.md) | 2,800 to 3,500 on average with layout types, 4,800 in the worst view |
| Loose props | 16 or fewer ([PROPS.md](PROPS.md)) | 16 spots offered |
| Loot pickups | 8 or fewer (`FloorRaceProfile`) | Not built yet |
| Realtime lights | No number set | One shadowless spot per closed room bay |

## Files

- [FloorGenTypes.cs](../../Assets/_Project/Scripts/FloorGen/FloorGenTypes.cs): the layout description.
- [FloorGenProfile.cs](../../Assets/_Project/Scripts/FloorGen/FloorGenProfile.cs): the tuning asset, `Assets/_Project/Profiles/FloorRace/FloorGen.asset`.
- [FloorGenerator.cs](../../Assets/_Project/Scripts/FloorGen/FloorGenerator.cs): seed and floor in, layout out.
- [FloorCeiling.cs](../../Assets/_Project/Scripts/FloorGen/FloorCeiling.cs): the ceiling plan and its checks.
- [FloorFitOut.cs](../../Assets/_Project/Scripts/FloorGen/FloorFitOut.cs): furniture patterns and authored spots.
- [FloorNav.cs](../../Assets/_Project/Scripts/FloorGen/FloorNav.cs): the walk map, distances and the checks.
- [FloorKit.cs](../../Assets/_Project/Scripts/FloorRace/FloorKit.cs): which kit prefab each piece is, `Assets/_Project/Profiles/FloorRace/FloorKit.asset`. **Seal the Deal > Floor Race > Fill Floor Kit** fills its empty slots by name and never replaces one someone set.
- [FloorBuilder.cs](../../Assets/_Project/Scripts/FloorRace/FloorBuilder.cs): builds and tears down a floor.
- [FloorBuildMeasure.cs](../../Assets/_Project/Editor/FloorRace/FloorBuildMeasure.cs): **Seal the Deal > Floor Race > Measure Floor Build (Play Mode)** writes `Temp/FloorGen/build_report.txt`.
- [FloorLayoutPreview.cs](../../Assets/_Project/Editor/FloorRace/FloorLayoutPreview.cs): **Seal the Deal > Floor Race > Render Layout Previews** draws a layout PNG and a ceiling PNG for several seeds and floors to `Temp/FloorGen/` and writes a report of every check. Unity empties `Temp/` when the Editor starts, so copy out anything worth keeping.

## Measured

**Generator sweep, 2026-11-22, in the Editor** (`Render Layout Previews`, 200 seeds x 10 floors on the 10 x 8 plate):

| What | Result |
|---|---|
| Floors passing every check | 2,000 of 2,000 |
| Passing on the first attempt | 1,757; the worst floor needed 4 attempts |
| Same inputs run twice | 200 of 200 identical, piece for piece |
| Generate time | 3.4 to 6.8 ms a floor on average across three runs, retries included. Around the 5 ms budget; most of it is re-checking connectivity for every piece of furniture |
| Kit pieces a floor | 410 on average, 456 at most |
| Spawn spots | 42 on the worst floor (24 needed) |

Sixteen layouts (four seeds, floors 1, 2, 5 and 10) were drawn and looked at: ring, court, named rooms, lifts, starts and floor 10's office all appear where the rules put them.

**Building, 2026-11-22, Editor Play Mode** (`Measure Floor Build (Play Mode)`: three seeds, floors 1, 5 and 10, in an empty scene with one shadow-casting sun; draw calls read from the Game view with one camera at eye height at four spawn spots a floor):

| What | Pieces left separate (the default) | Still pieces merged |
|---|---|---|
| Build one floor | 10.5 to 19.6 ms | 18.3 to 32.9 ms |
| Tear one down | 2.3 to 5.3 ms | 2.1 to 4.7 ms |
| Objects built | 399 to 441 | same |
| Colliders | 372 to 410 | same |
| Draw calls, average of the four views | 2,549 to 2,902 | 2,017 to 2,265 |
| Draw calls, worst view | 2,949 to 3,235 | 2,379 to 2,544 |
| Set-pass calls, average | 67 to 104 | 72 to 111 |
| Triangles, average | 630,000 to 900,000 | same |

- After 18 builds and tear-downs nothing was left under the builder, and memory held one more mesh than before the run: Unity's built-in cube, loaded once for the block under the floor.
- **For scale:** `KitOffice.unity` read the same way from its own camera draws 6,527 draw calls, 497 set-pass calls and 3.2 million triangles. A generated floor is about 40% of that.
- **Merging is off.** It saves about a fifth of the draw calls and doubles the build time, but it only works in a build if the kit's models are imported with Read/Write on. They are not, and that setting belongs to the kit import. Set-pass calls, which cost more than draw calls with URP's batcher, do not improve.
- A floor has no walls that hide what is behind them from the renderer (no occlusion culling at runtime), so the camera draws most of the floor from anywhere. That is the reason the draw calls are in the thousands.
- One floor (seed 1, floor 1) was built and looked at from two eye-height views and from above: walls, doors, glazing, ceilings and furniture stand where the drawing puts them.
- In the floor race's two-instance run a development build generated and built each floor from the synced seed with the same object count as the Editor host ([FLOOR_RACE.md](FLOOR_RACE.md), "Checked so far"). Players were placed on the floors by script; nobody has walked one by hand, and the numbers above were not measured in a build.

**GPU Resident Drawer, 2026-11-24, Editor Play Mode** (URP's `GPU Resident Drawer` set to Instanced Drawing on `PC_RPAsset` with `BatchRendererGroup Variants` set to Keep All, against the shipped setting of off; same `Measure Floor Build` run, same empty scene with one soft-shadow sun, both runs in one Editor session):

| What | Off (shipped) | On |
|---|---|---|
| Draw calls, average of the four views | 2,351 to 2,778 | 262 to 384 |
| Draw calls, worst view | 2,902 to 3,228 | 321 to 401 |
| Set-pass calls, average | 64 to 104 | 44 to 82 |
| Build one floor | 4.3 to 9.6 ms | 5.2 to 7.4 ms |
| Frame time on one floor (seed 77777, floor 10, one view, median of 300 frames, three runs) | 3.0 to 3.1 ms main thread, 3.1 ms GPU | 3.0 to 3.1 ms main thread, 3.2 ms GPU |

- It cuts draw calls to about an eighth and needs no Read/Write on the kit models. On this PC the frame time did not change, so draw calls are not what limits a generated floor here.
- It is left off. Turning it on changes two project settings and keeps more shader variants in a build; whether it helps on a slower PC or in a build has not been measured.
- Merged pieces are not picked up by it: with both on, draw calls were the same as with both off.
- Counting free walk-map nodes with a running total instead of a scan was tried the same day and did not change generate time (about 3 ms a floor over 2,000 floors either way), so it was not kept. The time is in the flood fill itself.

**The dressing, 2026-11-24, Editor Play Mode.**

| What | Before | With the dressing |
|---|---|---|
| Floors passing every check (200 seeds x 10 floors) | 2,000 of 2,000 | 2,000 of 2,000 |
| Kit pieces a floor, average | 410 | 602 |
| Generate time a floor, average | 2.9 to 3.4 ms | 4.7 to 4.9 ms |
| Build one floor (three seeds, floors 1, 5 and 10) | 4.3 to 9.6 ms | 10.1 to 11.7 ms |
| Colliders | 372 to 404 | 476 to 671 |
| Draw calls, average of four views | 2,351 to 2,778 | 2,704 to 3,477 |
| Triangles, average | 590,000 to 780,000 | 1.1 to 2.2 million |

- The extra generate time is mostly the walk map check for the fixed chairs. Checking every chair on its own took it to 5.5 to 6.8 ms, over budget; checking them all at once, and one by one only on a floor where that fails, brought it to 4.7 to 4.9 ms.
- **Nobody spawns inside furniture.** 80 floors (20 seeds, floors 1, 4, 7 and 10) were built and a standing capsule tested at every spawn and start spot: 0 of 5,217 touched anything at 0.30 m or 0.40 m radius (the player's is 0.35). The same test with the capsule put on chairs and benches hit 497 of 497, so it can fail.
- Frame time was compared by building one floor (seed 77777, floor 10) and switching the new chairs, monitors, desk items, lights and signs off and on in the same session, median of 300 frames. In an empty scene: about 7 ms a frame off and 8 ms on, with GPU time going from about 3 to 5.5 ms. In `FloorRace.unity` with the office look: 15 to 16 ms either way, inside the run-to-run spread. The Editor was slower that afternoon than in the morning's 3 ms readings, so only the off and on pairs compare.
- Looked at: the same five floors (seeds 1, 2026, 4242, 9001 and 77777 on floors 1, 3, 5, 7 and 10) were captured before and after from five views each, beside five KitOffice views, plus close-ups of a door name, a poster, a lightbox, a floor number and a tiled service room. Captures are in `Temp/FloorGen/shots` and `shots2`, not committed.
- The lift indicator, slogans, ring band and a glossy floor (with realtime probes switched on for the capture, then off again) were looked at in captures in `Temp/FloorGen/shots3`.
- **The reflection probe's cost.** In `FloorRace.unity`, the 12 frames after a floor was built averaged 9.0 to 9.1 ms with the probe and 8.7 ms with it removed, worst frame about 15 ms either way (two warm pairs; a first cold run was slower in both build and frames).
- Not checked: a client build (the dressing is built locally from the same layout, like the rest of the floor), and a person walking it.

**The ceiling plan, 2026-11-24, same Editor session before and after** (before = the commit "Hand over the floor generator rework and flag its scope").

| What | Per-bay ceiling | Ceiling plan |
|---|---|---|
| Floors passing every check (200 seeds x 10 floors) | 2,000 of 2,000 | 2,000 of 2,000 |
| Passing on the first attempt, kit pieces a floor | 1,721, 602 | 1,721, 602 (the same floors) |
| Ceiling plans with a fault | not checked | 0 of 2,000 |
| Generate time a floor (edit mode, four runs then three) | 5.42 to 5.47 ms | 5.40 to 5.61 ms |
| Build one floor (seeds 1, 2026, 77777 on floors 1, 5, 10; first cold build left out) | 11.7 to 14.6 ms, 13.4 average | 12.8 to 16.0 ms, 13.8 and 14.6 average over two runs |
| Draw calls, average of four views | 3,264 | 3,063 and 3,093 |
| Draw calls, worst view | 4,895 | 4,625 |
| Lit lines and room lights a floor | none planned, 36 lights | 39 lines in one mesh, 36 lights |
| Left after 18 builds and tear-downs | 0 objects, 0 meshes | 0 objects, 0 meshes |

- Generate time is over the 5 ms budget on this PC today with or without the plan (4.7 to 4.9 ms was read earlier the same day). A reading taken while the Editor was hosting a game was 10 to 11 ms; time it in edit mode.
- Looked at: seed 1 floor 1, seed 4242 floor 5 and seed 77777 floor 10, each from six views (two along corridors, a closed room, an open hall, the court, and straight up at the whole ceiling) before and after, beside six KitOffice views. Sheets are in [proof/floor_gen_2026-11-24/](../proof/floor_gen_2026-11-24/).
- Not checked: a client build, and a person walking it.

**Layout types, 2026-11-24, edit mode sweep and one clean Play Mode run.**

| What | Result |
|---|---|
| Floors passing every check (200 seeds x 10 floors) | 2,000 of 2,000 |
| By type | Core office 829, open plan 382, cellular 375, gallery 414; none failed |
| Needing more than one attempt | 95, 33, 40 and 44; the worst floor needed 4 |
| Ceiling plans with a fault | 0 of 2,000 |
| Generate time a floor (three runs) | 5.07 to 5.38 ms |
| Kit pieces a floor | 564 on average, 948 at most |
| Fewest spawn spots on any floor | 33 (24 needed) |
| Build one floor (seeds 1, 2026, 77777 on floors 1, 5, 10; first cold build left out) | 10.9 to 14.4 ms |
| Draw calls, average of four views, and worst view | 2,803 to 3,521, and 4,800 |

- Every earlier number in this doc was measured on core office floors only, which is all there was.
- A first Play Mode reading of 5,800 to 7,000 draw calls was wrong: a floor left by the capture script stood in the same place. The run above was repeated in a fresh Play Mode session.
- Looked at: one floor of each type as a layout drawing, a ceiling drawing, two eye-height views and the whole ceiling from below.
- Not checked: a client build, a person walking any type, and whether the fairness numbers feel right on an open plan floor, where the starts can see each other sooner.

## Known limitations

- A gallery's cross corridors all run the same way on most floors, because the middle block is cut across its shorter side first.

- The spine, lanes and strips all use one material at one brightness, and a line ends square: there is no end cap or junction piece.
- Generated ceilings have none of KitOffice's district bands along the soffit.

- The reflection is taken once, as the floor is built, so it does not show players, loot or anything that moves.
- Generated floors have the ring band but none of KitOffice's district colours, threshold strips or evacuation plans, which are drawn for its one plan.
- Furniture patterns are a code table, not designer assets. A designer cannot add a room pattern without a programmer until there is an authoring tool.
- Walls only run on the 4 m cell lines, so there are no 2 m corridors, alcoves or curved pieces.
- Where two walls meet with their thickness on opposite sides, the outer corner shows a 0.2 m notch. The kit has no interior corner post.
- One storey per floor: no stairs, court voids or bridges.
- Glass does not break on generated floors: the glass registry only indexes the old towers. Partitions stop players and shots; window bays are open.
- Floors do not get harder as you climb ([FLOOR_RACE.md](FLOOR_RACE.md), open questions).
