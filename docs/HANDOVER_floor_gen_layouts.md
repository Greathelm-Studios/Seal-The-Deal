# Handover: floor generator ceilings, layout types, two storeys, bigger floors

**Status:** Target · **Last updated:** 2026-11-24 · **From:** Harvey / Claude Code

Move this to `docs/archive/` with a one-line outcome when the work lands or is dropped (AGENTS.md §7).

## Goal

A generated floor should read as a real, designed office the way `KitOffice.unity` does, and be big and tangled enough that finding the lift is a search.
Harvey walked floor 1 in the Editor on 2026-11-24 and asked for four things, in his words:

1. "Lights and other roof pieces are placed without sequencing or thought, we need an algorithm or tool that properly places roofing."
2. "The tool should work by at least generating proper and well adjusted tilesets, so that the offices all feel realistic with different layout types that the game defaults to at random."
3. "Two floored offices is also good."
4. "Making it more of a maze that can get you lost helps with the fact a team needs to find the elevator, so the levels need to be bigger in scope."

His screenshots are in [proof/floor_gen_2026-11-24/](proof/floor_gen_2026-11-24/): `harvey_1` and `harvey_5` are the generated floor, `harvey_2` to `harvey_4` are KitOffice.
Ignore the black capsules in KitOffice; Harvey says they are tests.

## Where it stands

Everything below is committed on `harvey-netcode` and pushed. Read [systems/FLOOR_GEN.md](systems/FLOOR_GEN.md) first; it is current and holds every number.

- **"Tidy the floor race scene tool and generator, record Resident Drawer numbers"**: Build Scene reports failed steps; the generator has no shared static scratch lists; the GPU Resident Drawer was measured (draw calls fall to an eighth, frame time unchanged in the Editor) and left off.
- **"Dress generated floors like KitOffice"**: ceilings by room type, room lights, bounce lights, cobalt walls with the floor number, slogans, door names, posters, wave lightboxes, the ring band on the floor, a lift floor indicator, dressed desks with fixed chairs and monitors past the 16 loose props, piece labels (the dot band on glass), and a one-off reflection probe per floor. `Realtime Reflection Probes` is now on for the PC quality level in `ProjectSettings/QualitySettings.asset`, at Harvey's say.

How it is wired, so you know where to cut:

- `Scripts/FloorGen/` is pure: `FloorGenerator.Generate(seed, floor, profile)` returns a `FloorLayout` (cells, edges, `Pieces`, `Lights`, `Marks`, spots). No scene objects. Every machine must get the same layout, so it draws from one integer random stream in a fixed order.
- `Scripts/FloorRace/FloorBuilder.cs` turns a layout into objects on each machine: pieces, label panels, text, lights, the ring band, the probe.
- `Scripts/FloorRace/FloorKit.cs` and `Profiles/FloorRace/FloorKit.asset` map pieces to prefabs and hold the sign, light and reflection settings. `Seal the Deal > Floor Race > Fill Floor Kit` fills empty slots only.
- The ceiling is decided in `EmitStructure` in `FloorGenerator.cs`, one bay at a time: a bare `Slab_4x4_LightLine` over corridors and open halls (turned to follow a corridor, or one direction per hall), a floating `Ceiling_Panel_4x4` over each carpeted room bay, a flush `Ceiling_Grid_4x4` over each service room bay. **This per-bay rule is what Harvey is rejecting.**

Not started: everything under Next steps.

## What was checked

- Compiles in the open Editor (`unity recompile`), 0 `error CS`, console clean.
- 2,000 floors (200 seeds x 10 floors) all pass the generator's checks; generate time 4.7 to 4.9 ms a floor.
- 80 floors built and a player-sized capsule tested at all 5,217 spawn and start spots: 0 overlaps. The same test placed on furniture hit 497 of 497.
- Build 10 to 12 ms a floor, about 600 pieces and 3,000 draw calls; frame time in `FloorRace.unity` about 16 ms with the dressing on or off (Editor Play Mode, one PC).
- Captures of five floors before and after, beside KitOffice: a few are in the proof folder (`before_*`, `after_*`).
- Harvey walked floor 1 in the Editor as the host.
- **Not observed:** a client build since the dressing (the "seen from a client" check in FLOOR_RACE.md predates it), Steam, two PCs, more than one player.

## What Harvey's screenshots show (my reading, not his words)

- **Generated ceiling (`harvey_1`):** light lines meet at junctions from different directions with nothing to end them, and the rounded panel rings of the rooms either side sit at their own spacing, so the ceiling reads as scattered strips. Each bay is decided alone; nothing plans the ceiling as one drawing.
- **KitOffice ceiling (`harvey_2` to `harvey_4`):** long unbroken parallel lines that run a whole route, panels only over desk groups, a tiled field elsewhere, pendants in the double-height court, and district bands on the soffit. It was drawn once for its plan (`ArtSource/Kit/markings/`, `Marks_Ceiling_*`), which is why it is coherent.
- **Door names (`harvey_1`, `harvey_5`):** too large, cut by glass frames, and repeated on every door of the same room.
- **`harvey_5`:** a service room with a door in both bays gets no cobalt wall, because only doorless solid bays are coloured.

## Open questions and risks

- **Scope (AGENTS.md §2).** Two-storey floors and bigger floors are design changes to the floor race, and [GDD.md](GDD.md) still describes the two towers. Harvey asked for them; Adam and Alex have not weighed in. A line is on [WORKBOARD.md](WORKBOARD.md) under Release risks. Ask Harvey whether the team has agreed before building items 3 and 4.
- **What "tilesets" means.** My reading: authored room and zone templates (walls, furniture, ceiling and lights designed together) that the generator picks and fits, grouped into a few layout types. Confirm with Harvey before designing the data format. FLOOR_GEN.md already lists "furniture patterns are a code table" as a limitation.
- **Two storeys touch the mode, not only the generator.** Things that assume one storey today: the 2D walk map (`FloorNav`) and every fairness check on it; `FloorBuilder`'s 9 m floor pitch (the next floor is built above while the old one stands, so two storeys need more); `FloorRaceDirector.InExitLift`, `RoomNameAt` and `BuildingFrame`; the fall check (`Fall Depth` 3 m below the floor); respawn spot choice in `FloorRaceSpawns`; the reflection probe's box. The kit has the parts (`Stair_Straight_2m`, `Stair_Straight_Half_2m`, `Slab_2x2` for holes, `Guard_Glass_*`), and `KitOfficeScene.cs` shows them used with a court void.
- **Bigger floors cost.** The plate is 10 x 8 bays (40 x 32 m), tunable to 12 x 10 on `FloorGenProfile`. Pieces, colliders and draw calls scale with area, generate time has a 5 ms budget, and the loose prop cap of 16 and 8 loot pickups will feel thin. The GPU Resident Drawer result is the first lever if draw calls bite. A maze also wants walls off the 4 m grid; the kit has 2 m walls, doors and partitions the generator does not use yet.
- **Netcode (AGENTS.md §3).** Nothing here should change authority: layouts stay a pure function of seed and floor, built locally. Anything new that players can stand on or be blocked by must be in the layout, not added by the builder. It is not done until it has been seen from a client.
- **Reflection probe:** taken once per floor, so it shows nothing that moves. Only the PC quality level has realtime probes on.

## Next steps

1. Ask Harvey the two questions above (team agreement on scope; what a tileset is to him) and whether the ceiling comes first.
2. **Ceiling plan.** Replace the per-bay rule with one pass that plans the whole ceiling after rooms and walls are known: continuous light runs along each route from end to end with a defined treatment where runs meet; one centred, aligned field per room instead of a panel per bay; lights placed from that plan, not per bay. Add a ceiling drawing to `Seal the Deal > Floor Race > Render Layout Previews` so a plan can be judged without entering Play Mode, then check it looking up in Play Mode beside KitOffice. Unused kit pieces worth a look: `Ceiling_LightStrip`, `Ceiling_Beams_4x4`, `Light_Pendant_Linear`, `Pendant_Sphere`, `Ribbon_Ceiling_16m`.
3. **Layout types.** Today there is one plan shape (ring, core blocks, window bands). Add a few more picked by the seed, each with its own room templates. KitOffice's twin-core court is the obvious second.
4. Fix the small things in the screenshot notes (door name size and repeats, cobalt on door bays).
5. Two storeys, then bigger floors, once the scope is agreed.
6. Repeat the client check before calling any of it done, and update FLOOR_GEN.md, FLOOR_RACE.md and FEATURES.md in the same change.

Working notes for the next machine:

- Driving the Editor from the command line is in [notes/driving-unity-from-the-cli.md](notes/driving-unity-from-the-cli.md). A trap found this session: setting `Application.runInBackground` from a script in the Editor rewrites `ProjectSettings.asset`.
- To see a floor: open `FloorRace.unity`, press Play, and host (the Esc menu, or `SteamLobby.Host()`); floor 1 of a throwaway seed is built at once.
- `FloorRace.unity` is tool-written. Do not edit it by hand, and claim it on the workboard before rebuilding it.
- The captures under `Temp/FloorGen/` are on Harvey's main PC only.

## Prompt to start the next session

> Read `AGENTS.md`, then `docs/HANDOVER_floor_gen_layouts.md` and `docs/systems/FLOOR_GEN.md`, and look at the pictures in `docs/proof/floor_gen_2026-11-24/`.
> I want generated floors to read as designed offices like KitOffice. Start with the ceiling: replace the per-bay ceiling rule with one planning pass for the whole floor (continuous light runs along routes, one aligned field per room, lights placed from that plan), add a ceiling drawing to the layout preview tool, and show me before and after captures next to KitOffice.
> Before you design anything, ask me the two open questions in the handover (has the team agreed two-storey and bigger floors, and what I mean by tilesets), then carry on with the ceiling while I answer.
> Keep layouts a pure function of seed and floor number, keep 2,000 floors passing the generator's checks, and tell me the generate and build times before and after.
