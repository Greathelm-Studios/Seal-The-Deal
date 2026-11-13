# INTEGRATION

**Status:** Current (branch `integration`, not merged) · **Last updated:** 2026-11-13

The lanes played as one game in `Assets/_Project/Scenes/Lanes/Lane_Integration.unity`. The glue is in [Assets/_Project/Scripts/Integration/](../../Assets/_Project/Scripts/Integration/) (namespace `CloseTheDeal.Integration`); the scene is built by [IntegrationLaneSetup.cs](../../Assets/_Project/Editor/Lanes/IntegrationLaneSetup.cs).

## What the player sees

Hosting or joining puts you in the **reception**: the lobby lane's shared room, standing at street level past the end of the towers, its glass front looking down the street at both towers' bases. Your pistol is holstered: nobody can fire in reception.

Sign in with a firm at a stand (F), ready up at the desk (F), change outfit at the mirror. When everyone is ready and both firms have someone, the host holds F at the desk. Everyone is moved into their own firm's tower reception and the 5-second countdown runs. The ready board disappears once you leave reception.

On floors 1 to 3 of both towers, at the same spot in each, lie a **pistol**, an **AR-15**, a **Grenade Case**, a **fire extinguisher**, a **wet floor sign**, a **cubicle panel** and **chairs**, on top of the furniture the floors already have. They move to new spots every match. Reception holds no loot.

- **Guns go to your weapon slots, not your hands.** F on a gun: it is in slot 1 or 2 at once, full magazine. A gun you already carry is refilled. With both slots full, the new gun replaces the one in your hand and that one drops in front of you.
- **The briefcase and a carried item share your hands.** Holding one, F on the other says "Hands full · G to drop". G drops whichever you hold. When the briefcase and an item are both in reach, F takes the briefcase.
- **Carrying an item, you can't fire** (left mouse throws). **Carrying the briefcase, you can.**
- Dying drops the briefcase and whatever you carry; you keep your guns.

Carry the briefcase onto a roof and your firm wins. The result shows; 8 seconds later everyone is back in reception, empty-handed, the desk open and every ready cleared, for the next match.

The host's Esc menu still has **"Start now, skipping the desk"** (and "Rematch now, skipping the desk" after a result). It is a shortcut for testing alone, since the desk needs both firms. Everyone else's menu reads "Ready up at the reception desk."

## The four joins

```
 Reception (Waiting) ──desk hold, everyone ready──▶ LobbyRoster.RunStartRequested ──▶ LobbyRunFlow ──▶ MatchDirector.HostStartRun(client id → firm)
        ▲                                                                                   │  refused (a match is already on) ─▶ desk reopened
        │                                                                                   ▼
        └── 8 s after the result: everyone teleported back, desk reopened ◀── Ended ◀── Running ◀── Countdown (everyone in their firm's tower reception)

 F on a pickup ──host──▶ ItemCarrier.ServerItemTaken ──gun?──▶ GunPickups: refill / free slot / swap; pickup despawned; swapped gun spawned by FloorLoot
 F ──owner──▶ lobby target? ─▶ lobby │ briefcase in reach? ─▶ briefcase (FirmMember) │ else item (ItemCarrier)
             host: item refused if the briefcase fills the hands (not guns); briefcase refused if an item fills them
 Tower built ──host──▶ FloorLoot clears last match's loot and places the loot table on floors 1–3 of both towers
```

1. **Lobby start → match** (`LobbyRunFlow`). Host only. Listens to the static `LobbyRoster.RunStartRequested` (unsubscribed on disable) and calls `MatchDirector.HostStartRun`. The lobby's firm 0 / 1 is the match's building A / B: `LobbyRoster.ToFirmMap` gives client id → firm index, and `HostStartRun` sets `FirmMember.Firm` (the index of the building climbed) and the `TeamSpawner` seat, then rebuilds the towers, which moves every player to `TowerBuilder.TryGetSpawn(firm, slot)` (their tower's reception) and starts the countdown. The rebuild is synchronous, so a second start in the same frame finds the countdown already set and is refused. A refused lobby start calls `ServerReturnToLobby()` so the desk does not stay "starting". Players arriving before a match or after a result are put at the reception's spawn points (`TeamSpawner.OnPlayerSpawning`); mid-match arrivals keep the match's spawn (their firm's checkpoint or tower reception). A player dead when moved respawns where they were moved to.
2. **Gun pickups → weapon slots** (`GunPickups`, `GunPickupMap`). `ItemDefinition` has no link to Combat, so the map asset `Profiles/Integration/GunPickupMap.asset` pairs `Item_Pistol` / `Item_AR15` with `Pistol` / `AR15` and their pickup prefabs. On `ItemCarrier.ServerItemTaken` the host calls `WeaponHolder.Equip(weapon, slot)` and `Pickup.ServerUseUp()` (despawns: guns never return). `Equip` writes the holder's slot SyncVars, which reach the owner and everyone else.
3. **One thing in the hands** (`SharedHands`, `PlayerParts`). Hooks the lanes now expose: `ItemCarrier.HandsTakenElsewhere` and `InteractClaimedElsewhere`, `MatchDirector.HandsFull`, `WeaponHolder.FireHeld`. Keys: F interact, G drop, everywhere (pad: north, d-pad down). Every lane-local key reads the one menu gate, `PlayerInputReader.Captured` (the lobby's own `LobbyInput.Captured` is the same test).
4. **One scene** (`IntegrationLaneSetup`). Built from the game mode's builder (short `LaneTower` towers, plaza, skybridges, gates, a real `TowerBuilder` and `PropSpawner`, so Switchboard streams props), then the shooting lane's **Add Shooting To Open Scene** (glass registry; the starting loadout here is the pistol only), the pickups lane's prefabs and item prompt, and the lobby lane's reception, desk, stands, fitting room, roster and screens, each from that lane's own builder. `LobbyRoom` marks reception as in play for the match (neutral ground) so nobody waiting there counts as fallen out, and stands the ready board down outside it. Shooting targets are left out: they would stand in the loot's way.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Lobby start → match start | **Host** | Nothing new: the match's phase SyncVar and the roster's run flag | Once per match |
| Back to reception after a result | **Host** teleports every body | Rides each body's state | Once per match |
| Loot on floors 1–3 | **Host** spawns after each tower build, despawns at the next | One FishNet spawn per thing (16 with the default table); motion through Switchboard while moving | Once per match |
| A gun taken | **Host** | The holder's slot SyncVars (1 byte each) and the pickup's despawn | Per pickup |
| Swapped-out gun | **Host** spawns it | One spawn | Per swap |
| Hands rules, fire hold, reception board | Every machine from synced state; the **host's** checks decide | Nothing | — |

## Rules chosen (Adam to confirm)

- **Start with the pistol only**; the AR-15 is found on floor 2.
- **Picking up a gun you already carry refills it** and uses the pickup up.
- **Full slots: the new gun replaces the gun in hand**, which drops in front of you as a pickup.
- **A picked-up gun goes to the free slot without being drawn**; press 1 or 2 to use it.
- **The briefcase and items share one pair of hands**; the briefcase wins F when both are in reach; a lobby desk, stand or mirror wins over both.
- **Guns are not in the hands**: a briefcase carrier can pick up and fire guns. With an item in your hands you can neither fire nor pick up a gun (drop the item first).
- **Nobody fires in reception.**
- **Back to reception 8 s after a result**, empty-handed (the briefcase stays where the match ended until the next match moves it).
- **The Esc menu's start is kept as a host shortcut** that skips the desk.
- **Reception stands beside the towers**, not inside either tower's ground floor (one shared room, as you asked for the lobby).

## Designer values

- `Profiles/Integration/FloorLootTable.asset`: what lies on which floor, and how far from walls, window, stairs and each other.
- `Profiles/Integration/GunPickupMap.asset`: which pickup gives which gun, and what drops on a swap.
- On the scene's **Integration** object: `Back To Lobby Seconds` (8) and the reception spawn points (`LobbyRunFlow`), `Swap Toss Speed` (`GunPickups`).
- On **Reception**: the room's centre and size (`LobbyRoom`), set by the tool.
- The menu's words are on the match menu section (`MatchMenuPanel`: Start Label, Rematch Label, Waiting For Start, Waiting For Rematch).

## Rebuilding the scene

**Close the Deal > Lanes > Set Up Integration** rebuilds `Lane_Integration.unity` (asks first if it exists). From the shell:

```
Unity -batchmode -projectPath <checkout> -executeMethod CloseTheDeal.Editor.Lanes.IntegrationLaneSetup.SetUpFromCommandLine
```

It creates the two Integration assets only if missing and never changes an existing asset. Each lane's step only adds what is missing. **Run Integration Check (solo)** on the same menu plays the scene by script; from the shell, `-executeMethod CloseTheDeal.Editor.Lanes.IntegrationLaneRun.RunFromShell -local -integrationRole solo|host|client -integrationOut <folder>` (host and client as two copies of the project).

## Known limitations

- **Firm names disagree**: the lobby says Cobalt and Cyan (`LobbyRules.asset`), the match says Ashcombe & Pryce and Halloran Voss (`Firm_*.asset`). Same firms by index; both sets are proposals.
- **Alone, the desk can't start a match** (`Require Both Firms` on `LobbyRules.asset`); use the menu's shortcut.
- **Taking a gun fires the pickup's "knocked loose" release on every machine** (the pickup is in the hands for one call before it is used up), so a drop sound hung on it would play.
- **Guns taken are gone for the match**; a fresh set comes with the next match. A dead player keeps theirs.
- **Loot placement checks for room only when the towers are built**; furniture shoved about later can end up on top of it.
- **One F press asks for both the briefcase and an item**, and the host decides by reach. If the host has the briefcase in reach but the player's own screen did not (lag at the edge of reach), the host gives the briefcase, not the item the prompt showed.
- **At the reception's edge** the owner and the host can disagree for a moment about whether a player is inside, so a shot fired while stepping out may show on the shooter's screen and be refused by the host. Players only leave reception by being moved, so this needs a broken window guard to happen.
- **Nothing here is seen over Steam, on two computers, or with more than one client.**
