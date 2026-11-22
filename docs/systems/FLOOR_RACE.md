# FLOOR_RACE

**Status:** Target · **Last updated:** 2026-11-22

The main game mode, decided by Harvey on 2026-11-22. Nothing in this doc is built yet. It replaces the two-tower design: [GDD.md](../GDD.md) and the mode now in the code ([MATCH.md](MATCH.md)) still describe two towers, so going to one building drops the window fights across the street and the zipline raid.

Lines marked **(proposed)** are the doc's suggestion where Harvey has not decided yet.

## What the player sees

Two firms fight over one briefcase in one office building. The only way up is the floor's elevator, and only the briefcase can take you there.

On the first floor nobody knows where the briefcase or the elevator is. Both firms search, and the first to find the briefcase has to get it to the elevator. When the carrier and every surviving teammate are inside, the doors close and the floor ends.

From then on, the firm that took the briefcase up starts the next floor holding it. The other firm has to take it off them and get it to that floor's elevator instead. The briefcase can change hands any number of times.

Floors 1 to 9 are random layouts built from the KitOffice kit. Floor 10 holds the manager's office. The firm that carries the briefcase into that office wins.

## The rules

### The briefcase

- There is one briefcase in the match.
- On floor 1 it is hidden at one of several possible spots in the layout.
- On every later floor it starts in the hands of the firm that brought it up. **(proposed)** The carrier is whoever held it in the elevator.
- When the carrier dies, the briefcase drops where they fell. Anyone can pick it up, from either firm.
- The case keeps its current look and beacon ([MATCH.md](MATCH.md)): firm colour on screen, and a glow through walls so a dropped case can be found.

### The elevator

- Every floor has one elevator, hidden somewhere in the layout. Finding it is part of the floor.
- The elevator leaves only when the briefcase carrier **and every living member of the carrier's firm** are inside. **(proposed)** The other firm can't board, and the doors stay open until the whole firm is in.
- The ride is a short transition, not a physical trip up a shaft: the doors close, a loading-screen style moment plays while the next floor is built, and the doors open on the new floor.
- The stairs and the keycard from the earlier version are gone. The elevator is the only way up.

### Moving up a floor

When the elevator doors close, the whole match moves up together.

- **The firm without the briefcase arrives first** and gets a short head start to prepare before the elevator doors open on the new floor. **(proposed)** About 10 to 15 seconds, tunable.
- **The firm with the briefcase arrives in the elevator.** Teammates who were dead respawn inside the car during the ride, so the whole firm steps out together.
- **(proposed)** Dead members of the other firm also respawn on the new floor, so both firms start every floor at full strength.
- Floors you leave are gone. Nobody can go back down.

### Death and respawn

- A dead player stays down until their whole firm is dead. Then the whole firm respawns together.
- There is no limit on how many times a firm can respawn.
- Falling out of a window counts as a death.
- A solo player has no teammates, so their own death wipes the firm and they respawn straight away. There is no special rule for them.
- **Respawn spot:** near the briefcase, chosen by a smart spawn system. **(proposed)** The spawner picks from authored spawn spots in the layout, rejecting any that the other firm can see or that are within a minimum distance of a living rival, and prefers the nearest remaining spot to the briefcase. If none pass, it relaxes the visibility check first and the distance second.

### Floor 10 and the win

- Floor 10's layout is random like the others, but always contains the manager's office, a fixed set piece placed into it.
- The firm whose carrier walks into the manager's office with the briefcase wins.
- There is no time limit.

### Weapons and props

- Weapons, gadgets and office props are scattered over every floor from authored spawn spots in the kit pieces. The seed picks which spots are used and what goes on them.
- The existing items in [ITEMS.md](ITEMS.md) and guns in [WEAPONS.md](WEAPONS.md) make up the loot pool, under the per-floor prop cap in [PROPS.md](PROPS.md).

### Cash rewards

- Every time a firm takes the briefcase up a floor, every member of that firm earns cash.
- Cash is spent outside the match on unlocks. It plugs into the existing profile and unlocks ([PROFILE.md](PROFILE.md), [CUSTOMISATION.md](CUSTOMISATION.md)).
- Harvey wants it tied to the player's online account ("link it up for networks"). Profiles are saved on each player's own PC today, so how cash is stored and kept from being cheated is still to design.

## Random floors

Each floor is a random layout made from KitOffice's kit pieces (walls, rooms, glazing, doors, furniture), built from the host's seed on every machine. This is the largest piece of work in the mode, because it needs a generator, not just a choice between a few hand-made floors. Each generated floor must:

- have a single connected walkable area, with no room that can't be reached;
- place the elevator and, on floor 1, the briefcase far enough apart that finding both takes a search;
- give each firm a fair start, with both arrival points roughly the same distance from the elevator;
- leave enough authored spawn spots for the respawn rules above;
- stay within the prop and draw-call budgets in [PROPS.md](PROPS.md) and [PERFORMANCE.md](PERFORMANCE.md).

**(proposed)** Build it as rooms and corridors snapped to the kit's grid, using the L2 plan's ideas ([DECISION_L2.md](../../ArtSource/Kit/concepts/Layout/DECISION_L2.md)) as rules: a ring you can always find again, every room with two exits, and named rooms for callouts.

## What changes from the current build

| Now ([MATCH.md](MATCH.md), [TOWER.md](TOWER.md)) | Floor race |
|---|---|
| Two towers with a street between them | One building, one floor in play at a time |
| Briefcase starts on the plaza between the towers | Hidden on floor 1, then starts with the firm that brought it up |
| Win by carrying the briefcase onto a roof, or holding it at 15:00 | Win by carrying it into the manager's office on floor 10; no time limit |
| Stairs and floor gates; climbing opens floors | Elevator only; the whole match moves up together |
| Respawn on the same floor after 5 s; window exits go to a checkpoint | Respawn only when the whole firm is dead, near the briefcase |
| Hand-made floor templates stacked by a seed | Floors generated from KitOffice kit pieces |
| No match rewards | Cash for every floor gained, spent on unlocks |

Code that carries over: the briefcase's pickup, drop, carry, beacon and steal rules; the host-decided match phases; seeded floor building; item, prop and gun spawning; health, death and ragdolls; the lift's host-timed doors ([ELEVATOR.md](ELEVATOR.md)).

## Who owns what (`AGENTS.md §3`)

Target design, to be confirmed when the code is written.

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Match seed | Host, once per match | One `uint` | Once |
| Current floor number | Host | One byte | Once per floor |
| Floor layout, elevator spot, floor 1 briefcase spot, loot and spawn spots | Built on every machine from the seed and floor number | Nothing | Never |
| Briefcase (state, carrier, position when loose) | Host, as today | Briefcase status value, as today | On change |
| Elevator boarding and departure | Host checks who is inside | One departure value | Once per floor |
| Floor change (head start timer, arrival points) | Host | One message per floor | Once per floor |
| Deaths and firm wipes | Host | Existing health and death state | On change |
| Respawn spot | Host picks | Spawn position on respawn | Per wipe |
| Cash earned | Host awards | One message per player per floor | Once per floor |
| Spawned weapons and props | Host, as today | Normal item and prop streams | Unchanged |

Only one floor exists at a time: the next floor is built during the elevator transition and the old one is torn down, so floor contents are never streamed.

## Open questions

- **No time limit:** watch match length in playtests. A firm that defends well could keep a match going for a long time. A soft limit, or a stronger push for the trailing firm, may be needed after playtests.
- **Floor 1 start:** where does each firm start, and does either get the briefcase's general direction?
- **Can the firm with the briefcase be stalled at the elevator?** If one teammate hides or stays away, the elevator can't leave. Options: a timer that leaves without them, or boarding only needing the teammates near the carrier.
- **How much cash per floor**, does the win pay extra, and how is it stored and kept from being cheated?
- **Generator scope:** how many kit pieces and room types the first version needs, and how floors get harder as you climb.
