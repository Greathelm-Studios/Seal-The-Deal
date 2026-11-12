# MATCH

**Status:** Current · **Last updated:** 2026-11-12 · **Verified against:** the shared-briefcase commit on the game mode lane

## What happens in the game

Two rival law firms, **Ashcombe & Pryce** (Building A) and **Halloran Voss** (Building B), race for **one shared briefcase**. The firm whose player carries it onto **either roof** wins. Firm names are proposals waiting on Adam.

Everyone waits in their firm's reception until the host presses **Start the match** in the menu. A 5-second countdown runs; the briefcase is locked until it ends.

The briefcase starts on the **plaza**, the street between the two towers, the same distance from both receptions. Step over your reception's window sill to reach it.

Press **F** near it to pick it up: anyone can. Everyone sees it hanging at your hip; you see it low on the right of your view. It shows in **cyan** while your firm holds it, **amber `#FF9E1B`** while the rival does, and leather brown while it lies loose, on each player's own screen. Press **G** to drop it ahead of you for a teammate.

The carrier drops it if they are knocked down, fall out of the building, or leave the game. A loose briefcase can be taken by anyone, so the rival can steal it.

On every checkpoint floor a **skybridge** crosses the gap between the towers, flush with the window sills, with a rail each side. It is the way across to steal the briefcase.

A briefcase that leaves the buildings (over a rail, out of a window) is gone for 3 s, then reappears on the skybridge of the highest checkpoint floor it has been on, or on the plaza if none.

Standing on a checkpoint floor in your own tower moves your firm's respawn point there. Walk out of a window and you are put back at your firm's last checkpoint after a short fall. Reach a roof without the briefcase and you are told why nothing happened.

At **15:00** the quarter closes: the firm holding the briefcase wins; if it lies loose, the firm that carried it last; if nobody ever picked it up, no winner.

The result opens the menu: who sealed the deal and how. The host gets **Rematch**: fresh towers from a new seed, everyone back in reception, the briefcase back on the plaza.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| A player's firm and slot | **Host**, when the player spawns (fewer players wins, A on a tie), or from a lobby through `HostStartRun` | Two bytes on `FirmMember`, in the spawn message, so late joiners get them | Once per player, or per run |
| Phase, start tick, winner, reason | **Host** | `MatchSnapshot` SyncVar, ~11 bytes | On change only |
| Clock and countdown | Worked out on every machine from the phase's start tick | Nothing | Never sent |
| The briefcase: state, carrier and their firm, last carrier's firm, building or neutral, floor, return floor, drop spot | **Host** | `BriefcaseStatus` SyncVar, ~23 bytes | On change only: pick-up, drop, floor or place change, return |
| Each firm's checkpoint floor | **Host** | One byte SyncVar per firm | On change only |
| Loose briefcase position | **Host** simulates; clients hold it kinematic | NetworkTransform, every other tick (30/s) | Only while it moves |
| Carried briefcase | Nobody: each machine hangs it on the carrier's body locally | Nothing | — |
| Plaza and skybridges | Built locally on every machine from the towers | Nothing | — |
| Pick up, drop | Owning client asks (ServerRpc, no payload); **host** decides, one request at a time, so of two in the same tick the first to arrive wins | — | Per key press |
| Fall-out, notices | **Host** | ObserversRpc / TargetRpc, 1 byte | Per event |

**Bandwidth, 8 players:** match state is a few bytes per event, nothing per frame. The only steady traffic is the loose briefcase while it moves: about 20 bytes, 30 times a second, to 7 clients, ≈ 4 KB/s from the host at worst, and nothing once it settles. A carried briefcase sends nothing.

## Designer-authored vs runtime-derived (AGENTS.md §4)

**Authored:** `Assets/_Project/Profiles/Match/DefaultMatch.asset` (`MatchProfile`): firms, own / rival / loose colours, countdown, match length, briefcase prefab and its spot on the plaza, pick-up range, drop toss, stray and return delays, how far outside a wall still counts as inside, fall time, skybridge width and rail height, carry blend, first-person offset, notice time. `Firm_AshcombePryce.asset` and `Firm_HalloranVoss.asset` (`FirmDefinition`): display name, neutral marker colour, which building. Every field has a tooltip.

**Derived:** floor names ("Floor 4", "Checkpoint 2"), which floor a point is on, whether it is in a building or on the neutral ground (all from `TowerProfile` and the built towers), the plaza and skybridges, the clock, a firm's checkpoint spot (where its player stood when they reached it).

## Rules the code keeps

- **One briefcase, always.** Spawned once when the host starts and only ever moved after; never despawned during a session.
- **Never lost.** Outside the buildings, plaza and skybridges for 2 s, below the street, or its carrier gone without a word: the host puts it back.
- **Deterministic ends.** One briefcase means one carrier, so two roofs can never be claimed in the same tick. The host checks players in one fixed order per tick.
- **Events, never polling.** `MatchDirector` raises an event for every change (phase, countdown second, clock second, briefcase status, picked up, dropped, returning, returned, checkpoint, end, towers rebuilt). The HUD and menu redraw only on those; `MatchHud.Update` only fades and pops what is already up.
- **Menu rule kept.** The match opens the menu through `GameMenu.Open()` when it ends; the menu still owns mouse-free and player-idle. Keys only act while the mouse is captured.
- **Host leaving ends it.** `MatchDirector.OnClosed` hides the match HUD and the menu takes over.

## Hooks for other systems

- **Lobby:** `MatchDirector.HostStartRun(IReadOnlyDictionary<int, int> firmByClientId)`, host only: client id → firm (0 = Building A, 1 = Building B). Moves the listed players (and their `TeamSpawner` seats) into those firms, then starts a run as Start does. Works while waiting or after a result; returns false otherwise. Players not listed keep their firm.
- **Blasts and throws:** `Briefcase.Throw(velocityChange)` shoves a loose briefcase (host only).
- **Sound, effects, UI:** every event on `MatchDirector`, plus `FirmMember.OnNotice`, `OnPromptChanged` and `OnFellOut`.

## Inputs (lane-local, for the movement lane to fold in)

Defined in code in `FirmMember` until the shared actions asset takes them:

| Action | Keyboard | Pad |
|---|---|---|
| Interact (pick up) | **F** | North button |
| Drop | **G** | D-pad down |

The owner asked for **E**; E is lean-right in the shared actions asset, and the asset's own `Interact` action is already F. The pickups lane also uses **G** and d-pad down to drop: see the handover.

## Scripts

Under [Assets/_Project/Scripts/Match/](../../Assets/_Project/Scripts/Match/), namespace `CloseTheDeal.MatchMode` (`Match` would clash with `System.Text.RegularExpressions.Match` in `KitImport.cs`):

- `MatchDirector.cs`: scene object; the synced state, every event, the clock, read-only queries for UI (`CaseStatus`, `CheckpointFloor`, `Carrier`, `ColourFor`, `PlaceLabel`, `PromptFor`, `SecondsLeft`, `Snapshot`).
- `MatchDirector.Host.cs`: host-only outcomes: start, rematch, `HostStartRun`, pick up, drop, return, checkpoint, respawn, join-in-progress spawn.
- `MatchReferee.cs`: host-only, once per tick after physics: falls (in every phase), knocks, where the briefcase is, checkpoints, the roof and the clock.
- `MatchRules.cs`: the clock rule as a pure function.
- `MatchArena.cs`: floor, building and neutral-ground maths from the built towers; no trigger volumes.
- `MatchCrossings.cs`: on the Match object; builds the plaza and a skybridge on each checkpoint floor locally after every tower build.
- `FirmMember.cs`: on `Player.prefab`; firm, slot, the two keys, host teleport, notices.
- `Briefcase.cs`: on `Prefabs/Match/Briefcase.prefab`; shows the state, eases into the hand, tints by holder.
- `MatchHud.cs`, `MatchMenuPanel.cs`: clock, briefcase row, firm rows, countdown, prompt and notices; the menu's match section and the host's Start / Rematch.
- `MatchProfile.cs`, `FirmDefinition.cs`, `MatchTypes.cs`.

Outside the folder: `Tower/TeamSpawner.cs` (balanced teams, a spawn hook and override, `AssignTeam` for a lobby, seats freed on leave), `Tower/TowerBuilder.cs` (`Rebuild()`, floor queries, building roots, clients follow a new seed), `UI/GameMenu.cs` (`Open()`).

## Setup

**Close the Deal > Lanes > Set Up GameMode** ([GameModeLaneSetup.cs](../../Assets/_Project/Editor/Lanes/GameModeLaneSetup.cs)) makes the firm and match assets, the briefcase prefab, adds `FirmMember` and a `CarryAnchor` to `Player.prefab`, and builds `Scenes/Lanes/Lane_GameMode.unity` with short towers (`Profiles/Match/LaneTower.asset`: reception, two floors, a checkpoint, the boardroom, the roof). **Add GameMode To Open Scene** adds the match (with its plaza and skybridges), HUD and menu section to any scene that already has the tower and game UI. Both only add what is missing and fill an empty slot; they never retune. **Rebuild GameMode Briefcase** rewrites the briefcase prefab while nobody has edited it.

## Known limitations

- **Greybox.** A plain case with a dark handle, seam and clasps, kept stripe-free so it never reads as the pickups lane's Grenade Case; no sound or effects yet, though every event above is there to hang them on.
- **Crossing is a design decision.** The skybridges are the minimum that makes stealing possible; the GDD's zipline gun is not built. The tower geometry itself is unchanged.
- **Getting on the plaza or a bridge means climbing the 0.6 m sill** (a mantle, 0.4–1.6 m in the movement profile).
- **Blasts don't move the briefcase.** `Knockback.Explode` only throws `Prop`s; wire `Briefcase.Throw` in when weapons land.
- **The gap counts as outside** except the plaza and the bridges. A zipline crossing over 1.2 s would send its rider back.
- **A briefcase wedged unreachably inside a building** has no recovery; only leaving the buildings sends it back.
- **The host starts the match from the menu** until the lobby lane's `RunStartRequested` calls `HostStartRun`.
- The client's player is put back by the host's correction, so a teleport shows as one corrected tick on the owner's screen.
