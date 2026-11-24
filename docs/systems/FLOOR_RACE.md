# FLOOR_RACE

**Status:** Current · **Last updated:** 2026-11-22

The main game mode, decided by Harvey on 2026-11-22. It is built, and a whole match has been driven by script with a second player watching from a client build. No person has played it yet. What has been checked, and how, is under "Checked so far".

It replaced the two-tower design, whose code was removed on 2026-11-22. [GDD.md](../GDD.md) still describes two towers; going to one building drops the window fights across the street and the zipline raid.

## What the player sees

Two firms fight over one briefcase in one office building. The only way up is the floor's lift, and only the briefcase can take you there.

On the first floor nobody knows where the briefcase or the lift is. Both firms search, and the first to find the briefcase has to get it to the lift. When the carrier and every surviving teammate are inside, the doors close and the floor ends.

From then on, the firm that took the briefcase up starts the next floor holding it, behind closed lift doors, while the other firm gets a head start on the floor. The other firm has to take the briefcase off them and get it to that floor's lift instead. The briefcase can change hands any number of times.

Floors 1 to 9 are random layouts built from the KitOffice kit ([FLOOR_GEN.md](FLOOR_GEN.md)). Floor 10 holds the manager's office. The firm that carries the briefcase into that office wins.

## The rules

Values in `code font` are on `Assets/_Project/Profiles/FloorRace/FloorRace.asset` unless another asset is named.

### The briefcase

- There is one briefcase in the match.
- On floor 1 it lies at a hidden spot the layout picks. Its beacon and screen marker stay off until someone first picks it up.
- On every later floor it starts in the hands of the firm that brought it up: whoever held it when the lift filled, or a teammate if that player has left.
- When the carrier dies or is knocked down, the briefcase drops where they fell. Anyone can pick it up, from either firm.
- If it leaves the building (out of a window, or through the floor) for `Stray Seconds` (2, on the match profile), it is put back at the spawn spot nearest where it was last inside.
- The case keeps its look and beacon from [MATCH.md](MATCH.md): firm colour on screen, and a glow through walls so a dropped case can be found.

### The lift

- Floors 1 to 9 have one exit lift, hidden somewhere in the layout. Its doors stand open. Finding it is part of the floor.
- It leaves when the briefcase carrier **and every living member of the carrier's firm** are inside the car (`Cabin Size`). The host checks ten times a second.
- Players of the other firm can stand in the car. They do not stop it leaving and they do not ride: everyone is moved to their own start on the next floor.
- **The ride.** The doors close (`Door Seconds`, 0.8) and the lift takes `Ride Seconds` (3) to reach the next floor. The firm aboard stays in first person inside the closed car for the whole ride and steps out of the next floor's lift from the place they stood. The other firm, and anyone dead, sees the floor-change screen for as long and is put at their start. The next floor is built during the ride.
- **What is real and what is a trick.** Floors are stacked: each is built 13.5 m above the last, and only the one in play (and for a moment the one being left) exists, so there is nothing to hide. The car does not travel. With its doors shut a rider cannot see out, so at the end of the ride they are moved into the arrival car, which is a different car in a different place on the plan; their screen dips for `Arrive Blink Seconds` (0.2, on the HUD) to cover the instant.
- **What the ride feels like aboard**, on that player's machine only (`FloorRaceDirector.Ride.cs`; nothing is synced and no rule reads it): a hum for the length of the ride and a chime on arrival (`Lift Ride` and `Lift Arrive` on `FloorKit.asset`, made with earshot, in `Assets/_Project/Audio/FloorRace/`); light bars running down the inside of the car's back wall, 1.5 m apart, easing through one floor's height; the view dipping as the car starts, trembling by `Ride Shake` and lifting as it stops, through the camera's existing movement spring, so it never turns the aim and Reduce motion switches it off. Every lift in a building opens the same way ([FLOOR_GEN.md](FLOOR_GEN.md)), so the car they arrive in is turned the same as the one they left and their view does not change.
- From floor 2 up there is also an arrival lift, where the briefcase firm starts. It never leaves.
- There are no stairs and no keycard. The lift is the only way up.

### Moving up a floor

When the lift doors close, the whole match moves up together.

- **The firm without the briefcase arrives first**, at the layout's walk-in start, and gets `Head Start Seconds` (12) before the arrival lift's doors open.
- **The firm with the briefcase arrives in the arrival lift.** Teammates who were dead come back inside the car, so the whole firm steps out together.
- Dead members of the other firm come back too, so both firms start every floor at full strength. Living players keep the health they had.
- Loot and props from the floor below are removed, and anything in a player's hands is dropped first. Guns in weapon slots are kept.
- Floors you leave are gone. Nobody can go back down.

### Death and respawn

- A dead player stays down until their whole firm is dead. Then the whole firm comes back together, `Wipe Respawn Seconds` (2) after the last death.
- There is no limit on how many times a firm can respawn.
- Falling out of a window counts as a death: a living player outside the walls (plus `Outside Margin`, 0.75 m) for `Fall Seconds` (1.2), or more than `Fall Depth` (3 m) below the floor, is killed.
- A solo player has no teammates, so their own death wipes the firm and they come back after the same wait. There is no special rule for them.
- **Respawn spot:** the layout's authored spawn spot nearest the briefcase on foot, at least `Min Case Distance` (6 m) of walking from it, that no living rival is within `Min Rival Distance` (8 m) of or can see within `Sight Range` (40 m). If none passes, the sight check is dropped first, then both. The firm stands in a ring round the chosen spot.

### Floor 10 and the win

- Floor 10's layout is random like the others, but always contains the manager's office, a fixed set piece ([FLOOR_GEN.md](FLOOR_GEN.md)). It has no exit lift.
- The firm whose living carrier stands inside the office with the briefcase wins.
- There is no time limit. The HUD shows no clock.

### Weapons and props

- Each floor gets `Loot Per Floor` (8) weapons and gadgets, picked by the seed from the `Loot` list (pistol, AR-15, Grenade Case, fire extinguisher, wet floor sign) and laid on the layout's loot spots.
- Loose chairs, monitors and cabinets stand at the layout's prop spots, 16 at most, the cap in [PROPS.md](PROPS.md).
- Everyone starts a match with the scene's starting loadout (the pistol) and full health.

### Cash

- Every time a firm takes the briefcase up a floor, every member of that firm earns `Cash Per Floor` (100). Delivering it to the manager's office pays the same again, plus `Win Bonus Cash` (0).
- The host tells each player what they earned; the player's own machine adds it to the `Cash` total in their profile ([PROFILE.md](PROFILE.md)) and saves.
- Nothing spends cash yet, and nothing stops a modified client or host from inventing it (see Open questions).

## Who owns what (`AGENTS.md §3`)

Decided before the code was written. Everything that matters is the host's; clients send input and build the floor locally.

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Match seed, floor number, stage of the floor (playing, lift leaving, next floor building, head start), the tick the stage began, which firm took the lift | Host (`FloorRaceDirector`) | One `FloorRaceState` value, about 11 bytes | On change: once at match start, then four times per floor gained |
| Floor layout, lift positions, floor 1 briefcase spot, spawn, loot and prop spots, the manager's office | Built on every machine from the seed and floor number ([FLOOR_GEN.md](FLOOR_GEN.md)) | Nothing | Never |
| Lift doors (exit lift closing, arrival lift opening) | Worked out on every machine from the stage and its tick | Nothing | Never |
| Who is aboard the lift | Host checks the car, 10 times a second | Nothing; a HUD counts its own firm's players in the car locally | Never |
| Transition screen and head start countdown | Worked out on every machine from the stage and its tick | Nothing | Never |
| Match phase, winner, reason | Host (`MatchDirector`), as today | `MatchSnapshot`, as today | On change |
| Briefcase (state, carrier, position when loose) | Host (`MatchDirector`, `Briefcase`), as today | `BriefcaseStatus`, and its `NetworkTransform` while it moves loose, as today | On change; 30 Hz only while loose and moving |
| Deaths | Host (`Health`), as today. In this mode the host holds every respawn | Existing health and death state | On change |
| Falling out of a window | Host: a living player outside the floor for `Fall Seconds`, or below it, is killed | Arrives as a death | Per fall |
| Firm wipe and respawn spot | Host counts the dead and picks the spot | Arrives as each player's respawn (existing) and position | Per wipe |
| Moving players to the next floor | Host teleports every body | Rides each player's own state, as every teleport does | Once per floor |
| Cash earned | Host awards | One message to each player of the firm (4 bytes) | Once per floor gained |
| Spawned weapons, gadgets and props | Host spawns them on the new floor and despawns the old floor's | One FishNet spawn each (up to 8 loot and 16 props); motion through Switchboard, as today | Once per floor |
| Prop stream's packing box | Set on every machine from the plate size and floor count | Nothing | Never |

**Bandwidth, 8 players:** a floor change costs four state values (about 44 bytes to each client), up to 24 spawn messages and 24 despawns, and one cash message per winning player. Nothing in this mode is sent per tick; the only continuous traffic is what was there already (player state, the loose briefcase, moving props).

Only one floor is in play at a time. The next floor is built above the old one while the lift leaves (floors stand 13.5 m apart: two storeys and a clear one between), everyone is moved up, and the old floor is torn down a second later, so nobody is ever standing on nothing and floor contents are never streamed.

## How it is built

- **One state value drives every machine.** `FloorRaceDirector` syncs `FloorRaceState`. Each machine builds the floor the state names, works the lift doors and the floor-change screen out from the stage's tick, and tears the old floor down `Teardown Seconds` after arriving. Nothing else about a floor change is sent.
- **The stages of a floor change.** Playing, then Departing when the lift fills (doors close), then Building (the floor number goes up; every machine builds the new floor above; players are still on the old one), then, `Build Lead Seconds` (0.5) later, the host moves everyone up and enters HeadStart, then Playing when the head start ends. Building before moving is why nobody falls: the host only moves players once every machine has had time to build.
- **The match director is shared, the referee is not.** The briefcase, its pick-up and drop, the phases and the result belong to `MatchDirector` ([MATCH.md](MATCH.md)). A mode registers an `IMatchRuleset` and the director hands it the per-tick referee step, the start button and a few HUD words. The floor race is the only ruleset.
- **Respawns are held, not timed.** `Health.RespawnsHeld` is switched on by the mode on every machine. A dead body stays down until the host calls `Health.ServerRespawnAt`. The down card says so instead of counting.
- **The prop stream's building.** The prop stream packs positions into a box the mode gives it with `PropStream.UseBuilding`, set once per session to cover the plate and all ten storeys.
- **Designer-authored against runtime-derived (`AGENTS.md §4`).** Authored: `FloorRace.asset`, `FloorGen.asset`, `FloorKit.asset`, the match profile, the kit prefabs. Derived: the seed, every layout, the built floors, lift door positions, spawn choices.

## Files and setup

- [Assets/_Project/Scripts/FloorRace/](../../Assets/_Project/Scripts/FloorRace/): `FloorRaceDirector.cs` (state, floors, doors, HUD queries), `FloorRaceDirector.Host.cs` (the host's referee and floor change), `FloorRaceSpawns.cs` (smart respawn), `FloorRaceLoot.cs` (loot and props), `FloorRaceHud.cs`, `FloorRaceProfile.cs`, `FloorBuilder.cs`, `FloorKit.cs`.
- [Assets/_Project/Scripts/Match/MatchRuleset.cs](../../Assets/_Project/Scripts/Match/MatchRuleset.cs): the seam in the match director.
- **Seal the Deal > Floor Race > Build Scene** ([FloorRaceSceneSetup.cs](../../Assets/_Project/Editor/FloorRace/FloorRaceSceneSetup.cs)) writes `Assets/_Project/Scenes/FloorRace.unity`: the play rig, the office look, the Match with its HUD and menu section, the combat and item HUDs, the Floor Race object and its HUD. It asks before replacing an existing scene. The scene holds no floor: floors exist only in play.
- To play: open `FloorRace.unity`, press Play, host, then Esc and **Start the match**.

## HUD

- `FloorRaceHud` adds the floor and room name (top centre), the objective line (top right, under the briefcase tracker), the cash line, and the floor-change screen.
- `MatchHud` supplies the pick-up and drop prompts, the countdown, one-off lines and the briefcase marker, which stays off while the briefcase is hidden.

## Defaults chosen that the spec did not give

Each is an Inspector value or a small rule, picked as the smallest sensible behaviour.

| Default | Value | Where |
|---|---|---|
| Head start | 12 s | `Head Start Seconds` |
| Wait before a wiped firm returns | 2 s | `Wipe Respawn Seconds` |
| Rivals in the lift car | Do not block it and do not ride | Rule in `FloorRaceDirector.Host` |
| A briefcase that leaves the building | Back at the nearest spawn spot to where it was last inside, after 2 s | `Stray Seconds`, match profile |
| Respawn keeps clear of the briefcase | 6 m of walking | `Min Case Distance` |
| Respawn distance from rivals and sight range | 8 m, 40 m | `Min Rival Distance`, `Sight Range` |
| Cash | 100 per floor, the win pays one more floor and no bonus | `Cash Per Floor`, `Win Bonus Cash` |
| Loot | 8 pickups a floor from five items | `Loot Per Floor`, `Loot` |
| Floor 1 starts | Firm A and firm B at the layout's two starts; neither is told where the briefcase is | [FLOOR_GEN.md](FLOOR_GEN.md) |
| Before the host starts | Players walk floor 1 of a throwaway seed; starting builds a fresh floor 1 | Rule |
| Health on a new floor | The dead come back at full health; the living keep theirs | Rule |
| The arrival lift | Stays open and never leaves; standing in it does nothing | Rule |

## Open questions

- **No time limit:** watch match length in playtests. A firm that defends well could keep a match going for a long time. A soft limit, or a stronger push for the trailing firm, may be needed.
- **Floor 1 start:** should either firm get the briefcase's general direction? Today neither does.
- **Can the firm with the briefcase be stalled at the lift?** If one teammate hides or stays away, the lift can't leave. Options: a timer that leaves without them, or boarding only needing the teammates near the carrier. Today the lift waits.
- **Should rivals in the car block the lift?** Today they don't, so body-blocking the car is not a defence.
- **Head start camping:** the other firm has 12 s to set up on the arrival lift's doors. Is that the intended fight, or should the arrival lift be harder to find?
- **Cash:** how much per floor, does the win pay extra, what does it buy, and how is it stored and kept from being cheated? Harvey wants it tied to the player's online account; today it is a number in the local profile, awarded by the host and trusted.
- **Difficulty:** floors do not get harder as you climb.

## Checked so far

- **Compiled** in the Editor, no errors.
- **Played on the host alone, Editor, 2026-11-22**, driven by script: a match started; the hidden briefcase was picked up; nine lift rides from floor 1 to floor 10, each with the floor-change screen, the head start and 100 cash; a death that wiped the solo firm and brought it back near the dropped briefcase; a fall from the building that killed the carrier and returned the briefcase inside; the win in the manager's office. 1,000 cash was in the profile at the end, the count of networked objects stayed the same on every floor, and the console showed no errors.
- **Seen from a client, 2026-11-22.** The Editor hosted and a development build joined on the same PC over the local transport (Tugboat), one player per firm. The host moved both bodies by script; the client's pick-up requests were sent from the client by script (the same call its F key makes). Both consoles were clean: no errors or exceptions in the Editor console or the client's log.
  - **Same floor on both machines:** the client built every floor from the synced seed with the same object count as the host (417, then 450, 405 and 431 on the floors compared). [Client on floor 1, waiting](../proof/floor_race_2026-11-22/c_01_joined_waiting.jpg).
  - **A steal:** the host player carried the briefcase and dropped it; the client took it. The client's screen went from "Take the briefcase back" with the rival marker to carrying it. [Before](../proof/floor_race_2026-11-22/c_03_host_has_case.jpg), [after](../proof/floor_race_2026-11-22/c_04_client_stole_case.jpg).
  - **A floor change:** the client carrier stood in the exit lift; the client saw the [floor-change screen](../proof/floor_race_2026-11-22/c_11_transition_screen.jpg), then started [inside the arrival lift with the doors counting down and its cash](../proof/floor_race_2026-11-22/c_07_headstart_in_lift.jpg), while the host, who arrived first, saw ["They arrive in 1. Get ready"](../proof/floor_race_2026-11-22/h_07_headstart_arrived_first.jpg).
  - **A team wipe and respawn:** the client's only player was killed. Its screen showed the [down card and the dropped briefcase](../proof/floor_race_2026-11-22/c_09_client_down.jpg); two seconds later it [came back in the next room with the briefcase 7 m away](../proof/floor_race_2026-11-22/c_10_client_respawned.jpg), out of the host player's sight.
  - **The floor 10 win:** the client carried the briefcase up eight more floors and [into the manager's office](../proof/floor_race_2026-11-22/c_12_floor10.jpg); its menu read ["Halloran Voss sealed the deal ... That is your firm"](../proof/floor_race_2026-11-22/c_13_win.jpg). The client's profile file held 1,000 cash and the win; the host's held 0 cash and the loss.
- **Not checked:** a person playing it; more than one player a firm (so boarding with teammates, and a wipe that is not solo, are untested); Steam instead of the local transport; two PCs; a joiner mid-match; falling out of a window as a client (host only).

- **The lift ride on two storeys, host alone, Editor, 2026-11-24**, driven by script: a match started on a two-storey floor; the carrier was put in an exit lift on the upper storey and it left (the first time boarding upstairs has been seen); the ride stage lasted 3 s with the carrier still inside the old car; they arrived on the next floor's lower storey 0.70 m east and 0.50 m north of the arrival car's centre, exactly where they had stood in the car that left, both cars facing the same way; a second ride from a lower-storey lift did the same; the console had no errors. Before lifts shared a facing, one arrival was turned 90 degrees, which is what prompted that rule. Not seen: the ride from a client, the other firm's screen, a dead rider, or a person at the controls.
- **The ride's feel, host alone, Editor, 2026-11-24:** a ride ran with the hum, lights, shake and chime switched on and the console clean; the light bars were present during the ride (12 bars, 12 m down at 2.4 s) and gone after it. The bars were rendered from inside the car at three positions and seen stepping down the back wall. Nobody has heard the sounds or felt the shake.

## The two-tower mode was removed

On 2026-11-22, in the commit "Remove the two-tower mode", after the floor race had been seen from a client.

- **Gone:** the tower builder, layout, profile, floor templates and locked doors; the tower referee, arena, plaza, skybridges and floor gates; checkpoints, the open floor and the quarter clock in `MatchDirector`; `PropSpawner` and `PropMarker`; `FloorLoot` and its table; bullet damage to doors; the tower half of the glass registry; the prop stream's tower hook; the tower and integration scene tools and the scripted checks that drove those scenes; their assets.
- **Kept:** `TeamSpawner` (seats players in firms); `LobbyRunFlow`, `LobbyRoom` and `Scripts/Lobby/` (the reception desk flow, unused by this scene, a design call); `Lift/Elevator.cs` (KitOffice's rooftop lift); the lane tools that KitOffice, WeaponTest and this scene's tool still call.
- **Checked after removing it:** it compiles with no errors; a full match (ten floors, a wipe, a fall, the win) was replayed on the host alone in the Editor with a clean console; `KitOffice`, `WeaponTest` and `FloorRace` open with no missing scripts and no prefab has one. The client check above was made before the removal and has not been repeated since.

## Known limitations

- The ride's two sounds have not been listened to by a person. The arrival chime is a new take of a recorded lift ding. The hum is built from recordings of a fridge and an engine room because earshot has no lift motor; its listening model reads it as an electrical hum (z +3.84) and still rates it a sketch.
- Only the firm aboard hears and feels the ride. The other firm's floor-change screen is silent.
- The kit README says the lift car has a glass back; the imported `Lift_Car` prefab's back is solid, so the ride lights run inside the car rather than in the shaft behind it.
- A rider who is jumping when the ride ends lands on the arrival car's floor: their height in the car is not carried over.

- Glass does not break on generated floors ([FLOOR_GEN.md](FLOOR_GEN.md)).
- Lift doors move once per tick with no smoothing, and are not rewound when a client's movement is replayed, so a client stepping through closing doors can be corrected by a few centimetres.
- The floor-change screen covers the view but players can still move and shoot under it for the second and a half it takes.
- Joining mid-match puts the player at their firm's start for the floor, alive, wherever the fight is.
- Match records count each player's best floor as the floor in play, whichever firm took the lift.
- The first-run hints and the secretary's lines were written for the towers and are not reworded for this mode; the menu and the result line are.
