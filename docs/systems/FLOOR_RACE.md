# FLOOR_RACE

**Status:** Target · **Last updated:** 2026-11-22

The main game mode, as Harvey described it on 2026-11-22. Nothing in this doc is built yet. It is a proposal until Adam signs it off: the design in [GDD.md](../GDD.md) and the mode now in the code ([MATCH.md](MATCH.md)) still describe two towers and one shared briefcase.

## What the player sees

Two firms race up the same office building, one floor at a time. Each firm has its own briefcase, and a firm can only climb when its briefcase goes up with it.

Every floor is new. The layout is put together at random from a set of authored pieces, so nobody knows where the stairs are until they find them. Both firms arrive on the same floor and search it at the same time, fighting over the same rooms, weapons and office props.

Some floors have a lift. If a firm finds that floor's lift keycard, it can take the lift up and skip searching for the stairs. There is only one keycard, and the other firm is looking for it too.

The first firm to bring its briefcase to the top wins.

## The rules

### Briefcases

- Each firm starts the match with its own briefcase. One player carries it, and any teammate can pick it up when it is dropped.
- **The briefcase is the firm's ticket up.** A firm's players cannot go above the highest floor its briefcase has reached. When the carrier goes up the stairs or the lift, that floor opens for the whole firm.
- When the carrier is killed, knocked out or blown out of a window, the briefcase drops where they were.
- The case shows on screen in the firm's colour, with a beacon through walls so teammates can find a dropped one. The current briefcase already does this ([MATCH.md](MATCH.md)), and each firm would keep it.

### Floors

- Each floor is built from a **layout set**: the outer shell is the same on every floor, and the rooms, walls and routes inside it are picked at random from authored templates.
- **The way up is hidden.** Each layout has several places where stairs could go. On each floor the seed picks one of them for the real stairs, and the rest are walls, locked rooms or dead ends. Players have to search the floor to find the stairs.
- Both firms play the same sequence of floors, so floor 4 has the same layout for both and neither firm gets an easier one.
- **The number of floors is not decided.** It is a single tunable value (`FloorCount`), so it can be set after playtests. For the old 12 to 15 minute match target, about 60 to 90 seconds per floor means somewhere between 8 and 12 floors.

### The lift and the keycard

- Some floors have a lift. The seed decides which ones, at a tunable rate (for example one floor in three), and never two lift floors in a row.
- A lift floor has **one keycard**, hidden at one of several authored spots in that layout. It is a normal carried item, so it can be dropped, knocked loose and taken by either firm.
- A firm that brings its briefcase and the keycard into the lift rides up and skips the search for the stairs. The keycard is used up.
- The stairs on a lift floor still work. A firm without the card goes on searching for them.
- The lift is loud. The other firm hears it leave, which matches the GDD's comeback design.

### Weapons and props

- Weapons, gadgets and office props are scattered over every floor from authored spawn spots in each layout. The seed picks which spots get used and what goes on them.
- The existing loose items and gadgets in [ITEMS.md](ITEMS.md) and the guns in [WEAPONS.md](WEAPONS.md) make up the loot pool. The per-floor cap on physics props in [PROPS.md](PROPS.md) still applies.

### Death, respawn and winning

- A player killed on a floor comes back after the usual delay at their firm's arrival point on the firm's current floor, meaning the highest floor its briefcase has reached.
- The first firm to carry its briefcase onto the top floor wins. If the clock cap is hit first, the firm on the highest floor wins, and a tie goes to whoever got there first.

## What changes from the current build

| Now ([MATCH.md](MATCH.md), [TOWER.md](TOWER.md)) | Floor race |
|---|---|
| Two towers, one for each firm, with a street between them | One building that both firms climb |
| One shared briefcase that both firms fight over | One briefcase per firm |
| One open floor for both towers | A separate open floor for each firm |
| Stairs in the same place on every floor | The stairs move around and have to be found |
| Lift only in the KitOffice dev scene ([ELEVATOR.md](ELEVATOR.md)) | Lift on some floors, opened by a keycard |
| Breakable locked doors hold a team on each floor | Searching for the stairs holds a team on each floor; whether locked doors stay is an open question |

Code that carries over as it is: floor gates (one set per firm instead of one shared set), the briefcase's pickup, drop, beacon and carrier rules, seeded floor building, the host-timed lift ride, item and prop spawning, and the clock.

## Who owns what (`AGENTS.md §3`)

Target design, to be confirmed when the code is written.

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Building seed | Host, once per match | One `uint` | Once |
| Each floor's layout, stair spot, lift on or off, keycard spot, loot spots | Built on every machine from the seed and the floor number | Nothing | Never |
| Each firm's briefcase (state, carrier, floor) | Host | One status value per firm, like today's `BriefcaseStatus` | On change |
| Each firm's open floor | Host | One byte per firm | On change |
| Keycard (where it is, who holds it, used or not) | Host, as an item ([ITEMS.md](ITEMS.md)) | Normal item state | On change |
| Lift ride | Host | One ride value per lift, as in [ELEVATOR.md](ELEVATOR.md) | Once per ride |
| Spawned weapons and props | Host, as today | Normal item and prop streams | Unchanged |

Floors are never streamed. A floor is built locally once the first firm reaches the floor below it, and torn down once both firms have left it, so only two or three floors exist at a time.

## Open questions for Adam

- **One building or two towers?** This doc assumes one building that both firms climb, which matches "another team finding it too" and Alex's L2 layout ([DECISION_L2.md](../../ArtSource/Kit/concepts/Layout/DECISION_L2.md)). This takes away the window fights across the street and the zipline raid, which are two of the GDD's three moments.
- **What can a rival do with your briefcase?** Can they carry it, kick it, throw it out of a window, or only knock it loose?
- **How many floors**, and how long should a match take?
- **How far does the lift go?** One floor, as Harvey described, or several, as the GDD roadmap says?
- **Do the locked doors stay** now that searching for the stairs holds a team on each floor?
- **Are there checkpoint floors**, a boardroom fight and a roof, or is it plain floors up to a top floor?
- **Does the layout mode clash with the L2 plan?** L2 keeps the stairs in two fixed cores on every floor, but here the stairs move. Either L2 gets several stair spots of which only one is real, or the hidden stairs live inside L2's fixed shell.
