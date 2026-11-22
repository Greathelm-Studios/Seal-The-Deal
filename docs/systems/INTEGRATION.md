# INTEGRATION

**Status:** Current · **Last updated:** 2026-11-21

**Read this first (2026-11-22):** `Lane_Integration.unity`, the towers, `FloorLoot` and the tool that built the scene were removed when the floor race replaced the two-tower mode ([FLOOR_RACE.md](FLOOR_RACE.md)). What is still in the code and used by `FloorRace.unity`: gun pickups (`GunPickups`, `GunPickupMap`), shared hands (`SharedHands`) and `PlayerParts`. `LobbyRunFlow` and `LobbyRoom` remain but no scene uses them. The rest of this page describes the removed scene.

Plays the Seal the Deal lanes as one game in `Assets/_Project/Scenes/Lanes/Lane_Integration.unity`.

## What the player sees

Hosting or joining puts you in the shared **reception** at street level past the end of the towers, looking down the street at both tower bases. Weapons are holstered in reception so nobody can fire there.

Sign in with a firm at a stand (`F`), ready up at the desk (`F`), and change outfits at the mirror. When everyone is ready and both firms have a player, the host holds `F` at the desk. Everyone moves into their firm's tower reception, the 5-second countdown runs, and the ready board clears outside reception.

On floors 1 to 3 of both towers, identical spots hold a **pistol**, an **AR-15**, a **Grenade Case**, a **fire extinguisher**, a **wet floor sign**, a **cubicle panel**, and **chairs**, shifting to new spots each match.

- **Guns go to weapon slots, not hands:** `F` on a gun puts it in slot 1 or 2 with a full magazine, refills a gun you already carry, or swaps with the held gun and drops it in front of you.
- **Briefcase and carried items share hands:** holding one makes `F` on the other report "Hands full, G to drop". `G` drops whichever you hold. When both are in reach, `F` takes the briefcase.
- **Fire restrictions:** carrying an item disables firing (left click throws); carrying the briefcase allows firing.
- **Death drops:** dying drops the briefcase and any carried item, while keeping guns.

Every match resets everyone to the starting loadout, full magazines, and full health. Carrying the briefcase onto a roof wins the match; 8 seconds after the result, everyone returns to reception empty-handed with the desk reopened. The host's Esc menu also offers **"Start now, skipping the desk"** (and rematch shortcut) for solo testing.

## Ownership and netcode boundary (`AGENTS.md §3`)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Lobby start to match start | Host | Match phase `SyncVar` and roster run flag | Once per match |
| Return to reception after result | Host teleports every body | Rides each player's state | Once per match |
| Loot on floors 1 to 3 | Host spawns after each tower build, despawns on rebuild | One FishNet spawn per item (16 with default table); motion via Switchboard | Once per match (queued) |
| Gun taken | Host | `WeaponHolder` slot `SyncVar`s (1 byte each) and pickup despawn | Per pickup |
| Swapped-out gun | Host spawns it | One FishNet spawn | Per swap |
| Hands rules, fire hold, reception gating | Computed on every machine from synced state; host checks decide | Nothing | Never |

## Architecture and design rationale

1. **Lobby start to match (`LobbyRunFlow`):** Host listens to static `LobbyRoster.RunStartRequested` (unsubscribed in `OnDisable` to prevent duplicate handlers across Play sessions). Lobby firm `0`/`1` maps to building A/B via `LobbyRoster.ToFirmMap(entrants)` into `MatchDirector.HostStartRun`. Because tower rebuilds are synchronous, a duplicate start in the same frame is refused and calls `LobbyRoster.ServerReturnToLobby()` so the desk never locks in "starting". Pre-match/post-result arrivals spawn at `_lobbySpawns`; mid-match arrivals use match checkpoints. `FreshStart` empties hands, restores starting guns, restores health, and overrides respawn position for anyone still dead.
2. **Queued floor loot (`FloorLoot`, `FloorLootTable`):** On `MatchDirector.OnArenaChanged` (seeded from `_tower.Seed`), `FloorLoot` calls `Physics.SyncTransforms()` so newly positioned towers are visible to physics queries, then spreads placement across frames (`FloorsPerFrame = 2` floors per `Update`) using raycasts and `CheckBox` clearance against walls, windows, stairs, and other items in both towers. Mid-match swapped guns use `ServerSpawnLoose` after pruning already-despawned entries.
3. **Gun pickups (`GunPickups`, `GunPickupMap`):** `GunPickupMap` decouples `Items` from `Combat` by mapping `ItemDefinition` to `WeaponDefinition` and dropped pickup prefab. Unlisted guns fall back to normal hand carry and are denied if the player holds the briefcase.
4. **Shared hands and component cache (`SharedHands`, `PlayerParts`):** `SharedHands` registers static hooks (`HandsTakenElsewhere`, `InteractClaimedElsewhere`, `HandsFull`, `FireHeld`) and cleans them up in `OnDisable`. `PlayerParts` caches `ItemCarrier` and `FirmMember` lookups per player component and clears caches on `FirmMember.OnRosterChanged`.
5. **Reception neutral zone (`LobbyRoom`):** Re-adds the reception `Bounds` as neutral ground to `MatchArena` whenever the towers rebuild, and calls `LobbyInteractor.Block`/`Unblock` when the local player steps outside reception.

## Rules and tuning

- **Designer assets:** `Profiles/Integration/FloorLootTable.asset` (per-floor loot entries and margins: `WallMargin`, `WindowMargin`, `StairClearance`, `Clearance`, `Spacing`, `Tries`), `Profiles/Integration/GunPickupMap.asset`.
- **Scene tuning:** On **Integration** (`LobbyRunFlow._backToLobbySeconds = 8`, `_lobbySpawns`; `GunPickups._swapTossSpeed = 2`), and on **Reception** (`LobbyRoom._centre`, `_size`).
- **Menu strings:** On `MatchMenuPanel` (`Start Label`, `Rematch Label`, `Waiting For Start`, `Waiting For Rematch`).

## Files and setup

- Scripts under [Assets/_Project/Scripts/Integration/](../../Assets/_Project/Scripts/Integration/): `FloorLoot.cs`, `FloorLootTable.cs`, `GunPickupMap.cs`, `GunPickups.cs`, `LobbyRoom.cs`, `LobbyRunFlow.cs`, `PlayerParts.cs`, `SharedHands.cs`.
- Scene setup: [IntegrationLaneSetup.cs](../../Assets/_Project/Editor/Lanes/IntegrationLaneSetup.cs), menu **Seal the Deal > Lanes > Set Up Integration** (or CLI `-executeMethod CloseTheDeal.Editor.Lanes.IntegrationLaneSetup.SetUpFromCommandLine`). **Run Integration Check (solo)** automates a check run.

## Known limitations

- **Tower windows do not break in `Lane_Integration`:** Floor template window glass currently lacks a breakable collider, so the glass registry finds no tower panes there and shots/players pass through window openings.
- **Firm names disagree:** Lobby rules use Cobalt and Cyan (`LobbyRules.asset`), while match definitions use Ashcombe & Pryce and Halloran Voss (`Firm_*.asset`).
- **Desk requires both firms:** Solo testing must use the Esc menu start shortcut when `Require Both Firms` is enabled on `LobbyRules.asset`.
- **Gun pickup drop sound edge case:** Picking up a gun momentarily places it in hand before `ServerUseUp()`, firing the release callback once.
- **Consumed guns stay gone for the match:** Used gun pickups do not respawn until the next match; dead players keep equipped guns.
- **Placement uses initial layout only:** Shoved furniture can later overlap spawn spots chosen at match start.
- **Edge-of-reach and reception-edge sync:** Lag at reach boundaries can make the host pick the briefcase when the client prompt showed an item, and stepping across reception's edge can briefly disagree on whether firing is allowed.
- **Not yet verified over Steam on multiple machines or with more than two clients.**
