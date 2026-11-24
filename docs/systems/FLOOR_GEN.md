# FLOOR_GEN

**Status:** Current · **Last updated:** 2026-11-24

The floor generator for the floor race ([FLOOR_RACE.md](FLOOR_RACE.md)). The "Measured" section says what has been checked and how. Layout drawings: [seed 1, floor 1](../proof/floor_race_2026-11-22/layout_seed1_floor01.png), [seed 77777, floor 10](../proof/floor_race_2026-11-22/layout_seed77777_floor10.png) (grey = furniture, red = open window, yellow = exit lift, orange = arrival lift, green and magenta = the two starts, gold = the briefcase, blue room = the manager's office).

## What the player sees

Every floor of the building is a different office plan made from the KitOffice kit: a corridor loop you can always find again, named rooms off it, window halls along the glass, and a lift hidden somewhere. Floor 10 has the manager's office behind a cobalt portal door. Both players in a match see exactly the same floor.

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
- **Fit-out:** furniture is placed per cell from a small set of patterns per room kind (bench, meeting table, kitchen counter, copier and storage, cover block, planter). Patterns also hold the **authored spots**: where a player may spawn, where loot may lie, where a loose prop stands. Patterns are a code table in `FloorFitOut.cs` for now (see Known limitations).

## The algorithm

Each step draws from one integer random stream seeded from `(seed, floor, attempt)`.

1. **Ring.** Pick a rectangle one or two cells in from each facade. Its border cells are the ring corridor.
2. **Core.** The cells inside the ring. If the block is three or more cells deep, a cross corridor (the court) cuts it so no block is more than two cells thick. A second cross corridor may cut a wide block. Each block is then split into rooms of one to four cells.
3. **Outer band.** The cells between ring and facade are split into rooms along each side. Corner rooms always reach round to touch the ring.
4. **Lifts.** The exit lift takes a corner cell of a room (its door facing into that room, or onto a corridor). From floor 2 up, an arrival lift takes another, far from the exit.
5. **Room kinds and names.** Each room is open plan (a window hall or lounge, no wall to the corridor) or enclosed (open office, meeting room, kitchen, print room, archive), with a wall finish that suits it. Names come from kind and compass position ("North-East Kitchen").
6. **Walls and doors.** Edges are set from the two rooms either side. Then every room is given doors until it has at least two ways out, the first onto a corridor.
7. **Fit-out.** Furniture patterns are placed, skipping any piece that would stand in front of a door.
8. **Walk map.** The floor is rasterised to a 0.5 m grid with every wall and furniture footprint, the furniture grown by a player's radius.
9. **Starts, briefcase, spots.** Chosen on the walk map by real walking distance (below).
10. **Check.** If any guarantee fails, the attempt number goes up and the floor is generated again, up to 24 times. The same attempt wins on every machine because the check is part of the pure function.

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
| Kit pieces instantiated | 350 to 500 | 399 to 441 |
| Generate (pure) | Under 5 ms | 3.4 to 6.8 ms |
| Build | Under 150 ms, hidden by the transition screen | 10.5 to 19.6 ms |
| Draw calls | No number is set in [PERFORMANCE.md](PERFORMANCE.md) | About 2,700, 40% of KitOffice |
| Loose props | 16 or fewer ([PROPS.md](PROPS.md)) | 16 spots offered |
| Loot pickups | 8 or fewer (`FloorRaceProfile`) | Not built yet |
| Realtime lights | None added by the generator | None |

## Files

- [FloorGenTypes.cs](../../Assets/_Project/Scripts/FloorGen/FloorGenTypes.cs): the layout description.
- [FloorGenProfile.cs](../../Assets/_Project/Scripts/FloorGen/FloorGenProfile.cs): the tuning asset, `Assets/_Project/Profiles/FloorRace/FloorGen.asset`.
- [FloorGenerator.cs](../../Assets/_Project/Scripts/FloorGen/FloorGenerator.cs): seed and floor in, layout out.
- [FloorFitOut.cs](../../Assets/_Project/Scripts/FloorGen/FloorFitOut.cs): furniture patterns and authored spots.
- [FloorNav.cs](../../Assets/_Project/Scripts/FloorGen/FloorNav.cs): the walk map, distances and the checks.
- [FloorKit.cs](../../Assets/_Project/Scripts/FloorRace/FloorKit.cs): which kit prefab each piece is, `Assets/_Project/Profiles/FloorRace/FloorKit.asset`. **Seal the Deal > Floor Race > Fill Floor Kit** fills its empty slots by name and never replaces one someone set.
- [FloorBuilder.cs](../../Assets/_Project/Scripts/FloorRace/FloorBuilder.cs): builds and tears down a floor.
- [FloorBuildMeasure.cs](../../Assets/_Project/Editor/FloorRace/FloorBuildMeasure.cs): **Seal the Deal > Floor Race > Measure Floor Build (Play Mode)** writes `Temp/FloorGen/build_report.txt`.
- [FloorLayoutPreview.cs](../../Assets/_Project/Editor/FloorRace/FloorLayoutPreview.cs): **Seal the Deal > Floor Race > Render Layout Previews** draws top-down PNGs of several seeds and floors to `Temp/FloorGen/` and writes a report of every check.

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

## Known limitations

- Furniture patterns are a code table, not designer assets. A designer cannot add a room pattern without a programmer until there is an authoring tool.
- Walls only run on the 4 m cell lines, so there are no 2 m corridors, alcoves or curved pieces.
- Where two walls meet with their thickness on opposite sides, the outer corner shows a 0.2 m notch. The kit has no interior corner post.
- One storey per floor: no stairs, court voids or bridges.
- Labels and signs that KitOffice adds in the Editor (room plates, lift indicators, machine labels) are not placed at runtime.
- Glass does not break on generated floors: the glass registry only indexes the old towers. Partitions stop players and shots; window bays are open.
- No lights are added. A floor is lit by the scene's sun and ambient light only, so rooms away from the windows are flat.
- Floors do not get harder as you climb ([FLOOR_RACE.md](FLOOR_RACE.md), open questions).
