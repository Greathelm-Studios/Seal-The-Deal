# MATCH

**Status:** Current · **Last updated:** 2026-11-12 · **Verified against:** the briefcase game mode lane commit

## What happens in the game

Two rival law firms, **Ashcombe & Pryce** (Building A) and **Halloran Voss** (Building B), each have a briefcase on their reception desk. Names are proposals waiting on Adam.

Everyone waits in their firm's reception until the host presses **Start the match** in the menu. A 5-second countdown runs; the briefcases are locked until it ends.

Press **F** near your own firm's briefcase to pick it up. Everyone sees it hanging at your hip, the case in your firm's colour; you see it low on the right of your view. Press **G** to drop it ahead of you for a colleague.

Carry it onto a **checkpoint floor** and that floor is saved: your firm, and a lost briefcase, come back there. Carry it onto **your own roof** and your firm wins at once.

You drop it if you are knocked down, fall out of the building, or leave the game. A rival who walks into a loose briefcase kicks it away; they can never carry it. A briefcase that leaves its building is gone for a moment and reappears at the firm's last checkpoint.

Walk out of a window and you are put back at your firm's last checkpoint after a short fall. Reach the roof without the briefcase and you are told why nothing happened.

At **15:00** the quarter closes: the firm whose briefcase is on the higher floor wins. On the same floor, the briefcase that got there first wins. If both are still level (neither left reception), there is no winner.

The result opens the menu: who closed the deal and how. The host gets **Rematch**: fresh towers from a new seed, everyone back in reception, countdown.

On your own screen your firm is always **cyan** and the rival **amber `#FF9E1B`**, whichever building you climb.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| A player's firm and slot | **Host**, when the player spawns (fewer players wins, A on a tie) | Two bytes on `FirmMember`, in the spawn message, so late joiners get them | Once per player |
| Phase, start tick, winner, reason | **Host** | `MatchSnapshot` SyncVar, ~11 bytes | On change only |
| Clock and countdown | Worked out on every machine from the phase's start tick | Nothing | Never sent |
| Each firm's briefcase state, carrier, floor, checkpoint, drop spot | **Host** | `FirmStatus` SyncVar per firm, ~20 bytes | On change only: pick-up, drop, floor change, checkpoint, return |
| Loose briefcase position | **Host** simulates; clients hold it kinematic | NetworkTransform, every other tick (30/s) | Only while it moves |
| Carried briefcase | Nobody: each machine hangs it on the carrier's body locally | Nothing | — |
| Pick up, drop | Owning client asks (ServerRpc, 0 bytes of payload); **host** decides | — | Per key press |
| Kick, fall-out, notices | **Host** | ObserversRpc / TargetRpc, 1 byte | Per event |

**Bandwidth, 8 players:** match state is a few bytes per event, nothing per frame. The only steady traffic is a loose briefcase that is moving: two cases at about 20 bytes, 30 times a second, to 7 clients, ≈ 8 KB/s from the host at worst, and nothing once they settle. A carried briefcase sends nothing.

## Designer-authored vs runtime-derived (AGENTS.md §4)

**Authored:** `Assets/_Project/Profiles/Match/DefaultMatch.asset` (`MatchProfile`): firms, own/rival colours, countdown, match length, briefcase prefab and its spot on the reception desk, pick-up range, drop toss, stray and return delays, kick radius, speed, lift and cooldown, how far outside a wall still counts as inside, fall time, carry blend, first-person offset, notice time. `Firm_AshcombePryce.asset` and `Firm_HalloranVoss.asset` (`FirmDefinition`): display name, neutral marker colour, which building. Every field has a tooltip.

**Derived:** floor names ("Floor 4", "Checkpoint 2"), which floor a point is on, whether it is inside a building (all from `TowerProfile` and the built towers), the clock, the checkpoint spot (where the carrier stood when they saved it).

## Rules the code keeps

- **One briefcase per firm, always.** Spawned once when the host starts and only ever moved after. It is never despawned during a session, so it can't be lost or duplicated.
- **Never stuck.** Outside its building for 2 s, or below the street, or its carrier gone without a word: the host puts it back. Out of the building it returns to the checkpoint after 3 s.
- **Deterministic ends.** The host checks every player in one fixed order per tick. Both firms on their roofs in the same tick: the briefcase that reached the boardroom first wins, then Building A. The clock tie uses the first tick each briefcase reached its floor.
- **Events, never polling.** `MatchDirector` raises an event for every change (phase, countdown second, clock second, firm status, pick-up, drop, returning, returned, checkpoint, end, towers rebuilt). The HUD and menu redraw only on those. `MatchHud.Update` only fades and pops what is already up.
- **Menu rule kept.** The match opens the menu through `GameMenu.Open()` when it ends; the menu still owns mouse-free and player-idle. Keys only act while the mouse is captured.
- **Host leaving ends it.** `MatchDirector.OnClosed` hides the match HUD and the menu takes over, as it already does on disconnect.

## Inputs (lane-local, for the movement lane to fold in)

Defined in code in `FirmMember` until the shared actions asset takes them:

| Action | Keyboard | Pad |
|---|---|---|
| Interact (pick up) | **F** | North button |
| Drop | **G** | D-pad down |

The owner asked for **E**; E is lean-right in the shared actions asset, and the asset's own `Interact` action is already F. See the handover.

## Scripts

Under [Assets/_Project/Scripts/Match/](../../Assets/_Project/Scripts/Match/), namespace `CloseTheDeal.MatchMode` (`Match` would clash with `System.Text.RegularExpressions.Match` in `KitImport.cs`):

- `MatchDirector.cs`: scene object; the synced state, every event, the clock, read-only queries for UI (`Firm`, `Carrier`, `ColourFor`, `PromptFor`, `SecondsLeft`, `Snapshot`).
- `MatchDirector.Host.cs`: host-only outcomes: start, rematch, pick up, drop, return, checkpoint, respawn, join-in-progress spawn.
- `MatchReferee.cs`: host-only, once per tick after physics: falls, knocks, floors, checkpoints, kicks, the roof and the clock.
- `MatchRules.cs`: the two winner rules as pure functions.
- `MatchArena.cs`: floor and inside-the-building maths from the built towers; nothing networked, no trigger volumes.
- `FirmMember.cs`: on `Player.prefab`; firm, slot, the two keys, host teleport, notices.
- `Briefcase.cs`: on `Prefabs/Match/Briefcase.prefab`; shows the state, eases into the hand, tints by firm. `Throw()` is the hook for blasts.
- `MatchHud.cs`, `MatchMenuPanel.cs`: the clock, firm rows, countdown, prompt and notices; the menu's match section and the host's Start / Rematch.
- `MatchProfile.cs`, `FirmDefinition.cs`, `MatchTypes.cs`.

Outside the folder: `Tower/TeamSpawner.cs` (balanced teams, a spawn hook and override, seats freed on leave), `Tower/TowerBuilder.cs` (`Rebuild()`, floor queries, building roots, clients follow a new seed), `UI/GameMenu.cs` (`Open()`).

## Setup

**Close the Deal > Lanes > Set Up GameMode** ([GameModeLaneSetup.cs](../../Assets/_Project/Editor/Lanes/GameModeLaneSetup.cs)) makes the firm and match assets, the briefcase prefab, adds `FirmMember` and a `CarryAnchor` to `Player.prefab`, and builds `Scenes/Lanes/Lane_GameMode.unity` with short towers (`Profiles/Match/LaneTower.asset`: reception, two floors, a checkpoint, the boardroom, the roof). **Add GameMode To Open Scene** adds the match, HUD and menu section to any scene that already has the tower and game UI. Both only add what is missing and fill an empty slot; they never retune.

## Known limitations

- **Greybox.** A plain case in the firm's colour with a dark handle, seam and clasps, kept stripe-free so it never reads as the pickups lane's Grenade Case; no sound or effects yet, though every event above is there to hang them on.
- **Blasts don't move the briefcase.** `Knockback.Explode` only throws `Prop`s; wire `Briefcase.Throw` in when weapons land.
- **The gap counts as outside.** More than 1.2 s outside both buildings sends a player back. A zipline crossing will need an exemption.
- **Checkpoint spots are where the carrier stood.** If that spot is later blocked, players are placed on a 1 m ring around it, then on it regardless.
- **A rival can kick your briefcase down a floor**, and the clock reads the floor it is on now, not the highest it reached.
- **The host starts the match from the menu.** The GDD has the secretary do it.
- The client's player is put back by the host's correction, so a teleport shows as one corrected tick on the owner's screen.
