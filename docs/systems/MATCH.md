# MATCH

**Status:** Current · **Last updated:** 2026-11-21

## What happens in the game

Two rival law firms, **Ashcombe & Pryce** (Building A) and **Halloran Voss** (Building B), race for **one shared briefcase**. The firm whose player carries it onto **either roof** wins. Firm names are proposals waiting on Adam.

Everyone waits in their firm's reception until the host presses **Start the match** in the menu. A 5-second countdown runs; the briefcase is locked until it ends.

The briefcase starts on the **plaza**, the street between the two towers, the same distance from both receptions. Step over your reception's window sill to reach it.

A soft column of light rises from the briefcase and shows through floors and walls within 30 m, so you can find it from anywhere nearby. While the rival firm holds it, the column breathes (a cue that doesn't rely on telling cyan from amber); your own firm's stays steady. Press **F** near it to pick it up: anyone can. Everyone sees it hanging at your hip; you see it low on the right of your view. It shows in **cyan** while your firm holds it, **amber `#FF9E1B`** while the rival does, and leather brown while it lies loose, on each player's own screen. Press **G** to drop it ahead of you for a teammate.

The carrier drops it if they are knocked down, fall out of the building, or leave the game. A loose briefcase can be taken by anyone, so the rival can steal it.

**The briefcase leads the climb.** Nobody, in either tower, can go above the highest floor the briefcase has been carried onto (the **open floor**; it starts at the ground floor). Where the stairs arrive on each floor above it, a frosted panel closes the open side of the stair cage (on the roof, a ring round the stairwell). The carrier walks through it; the moment they stand on the new floor it opens for everyone in both towers, for the rest of the match. Dropping the briefcase never closes a floor again. Anyone else who reaches a shut gate is told: "Floor 3 is shut. Whoever carries the briefcase up there opens it for everyone." The HUD's briefcase row shows how high the building is open.

On every checkpoint floor a **skybridge** crosses the gap between the towers, flush with the window sills, with a rail each side. It is the way across to steal the briefcase.

A briefcase that leaves the buildings (over a rail, out of a window) is gone for 3 s, then reappears on the skybridge of the highest checkpoint floor it has been on, or on the plaza if none.

Standing on a checkpoint floor in your own tower (at or below the open floor) moves your firm's respawn point there, so nobody ever respawns above the open floor. Walk out of a window and you are put back at your firm's last checkpoint after a short fall. Reach a roof without the briefcase and you are told why nothing happened.

At **15:00** the quarter closes: the firm holding the briefcase wins; if it lies loose, the firm that carried it last; if nobody ever picked it up, no winner.

The result opens the menu for everyone and explains who sealed the deal and how. The host sees **Rematch**. Other players see the result and the instruction to wait for the host. A rematch starts with fresh towers from a new seed, everyone back in reception and the briefcase on the plaza.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| A player's firm and slot | **Host**, when the player spawns (fewer players wins, A on a tie), or from a lobby through `HostStartRun` | Two bytes on `FirmMember`, in the spawn message, so late joiners get them | Once per player, or per run |
| Phase, start tick, winner, reason | **Host** | `MatchSnapshot` SyncVar, ~11 bytes | On change only |
| Clock and countdown | Worked out on every machine from the phase's start tick | Nothing | Never sent |
| The briefcase: state, carrier and their firm, last carrier's firm, building or neutral, floor, return floor, drop spot | **Host** | `BriefcaseStatus` SyncVar, ~23 bytes | On change only: pick-up, drop, floor or place change, return |
| Each firm's checkpoint floor | **Host** | One byte SyncVar per firm | On change only |
| The open floor | **Host** raises it when the carrier stands on a higher floor; it never falls during a match | One byte SyncVar | On change only |
| Floor gates | Built locally on every machine; open or shut and who walks through them follow the open floor and the briefcase carrier, both synced, so a client's prediction meets the same walls as the host's | Nothing of their own | — |
| "Gate refused" line | **Host** | TargetRpc to that player, 1 byte | Once per approach |
| Loose briefcase position | **Host** simulates; clients hold it kinematic | NetworkTransform, every other tick (30/s) | Only while it moves |
| Carried briefcase | Nobody: each machine hangs it on the carrier's body locally | Nothing | — |
| Plaza and skybridges | Built locally on every machine from the towers | Nothing | — |
| Pick up, drop | Owning client asks (ServerRpc, no payload); **host** decides, one request at a time, so of two in the same tick the first to arrive wins | — | Per key press |
| Fall-out, notices | **Host** | ObserversRpc / TargetRpc, 1 byte | Per event |

**Bandwidth, 8 players:** match state is a few bytes per event, nothing per frame. The only steady traffic is the loose briefcase while it moves: about 20 bytes, 30 times a second, to 7 clients, ≈ 4 KB/s from the host at worst, and nothing once it settles. A carried briefcase sends nothing.

## Designer-authored vs runtime-derived (AGENTS.md §4)

**Authored:** `Assets/_Project/Profiles/Match/DefaultMatch.asset` (`MatchProfile`): firms, own / rival / loose colours, countdown, match length, briefcase prefab and its spot on the plaza, pick-up range, drop toss, stray and return delays, how far outside a wall still counts as inside, fall time, skybridge width and rail height, carry blend, first-person offset and scale (the carrier's own view of the case: tucked small into the lower left corner so the stairs ahead stay clear and the gun keeps the right), notice time. `Firm_AshcombePryce.asset` and `Firm_HalloranVoss.asset` (`FirmDefinition`): display name, neutral marker colour, which building. Every field has a tooltip.

**Derived:** floor names ("Floor 4", "Checkpoint 2"), which floor a point is on, whether it is in a building or on the neutral ground (all from `TowerProfile` and the built towers), the plaza and skybridges, the clock, a firm's checkpoint spot (where its player stood when they reached it).

## Rules the code keeps

- **One briefcase, always.** Spawned once when the host starts and only ever moved after; never despawned during a session.
- **Never lost.** Outside the buildings, plaza and skybridges for 2 s, below the street, or its carrier gone without a word: the host puts it back.
- **Deterministic ends.** One briefcase means one carrier, so two roofs can never be claimed in the same tick. The host checks players in one fixed order per tick.
- **Events, never polling.** `MatchDirector` raises an event for every change (phase, countdown second, clock second, briefcase status, picked up, dropped, returning, returned, checkpoint, end, towers rebuilt). The HUD and menu redraw only on those; motion runs on the UI kit's driver only while something moves, and the on-screen briefcase marker is placed each frame from local positions (no network).
- **Menu rule kept.** The match opens the menu through `GameMenu.Open()` when it ends; the menu still owns mouse-free and player-idle. Keys only act while the mouse is captured.
- **Host leaving ends it.** `MatchDirector.OnClosed` hides the match HUD and the menu takes over.

## UI contract (for the HUD, results and sound set)

Everything below is on `MatchDirector.Instance` (namespace `CloseTheDeal.MatchMode`) unless named otherwise. State is synced from the host only on change; the clock is worked out locally. **Read state when an event fires, never per frame.** Events are raised on every machine, once per change (the host does not hear them twice), and only while the match is live on that machine.

### State (read-only)

| What | Member | Type | Notes |
|---|---|---|---|
| Live here | `IsLive` | `bool` | Hosting or connected. |
| Phase | `Phase` | `MatchPhase` | `Waiting`, `Countdown`, `Running`, `Ended`. |
| Clock | `SecondsLeft` | `int` | Whole seconds left; the full length before Running; frozen at the end. |
| Countdown | (event only) | `int` | See `OnCountdownChanged`. |
| Briefcase | `CaseStatus` | `BriefcaseStatus` | `State` (`Dropped` / `Carried` / `Returning`), `CarrierId` (network object id), `CarrierFirm`, `LastCarrierFirm`, `Place` (0 = Building A, 1 = Building B, `MatchArena.Neutral` = plaza or a skybridge), `Floor`, `ReturnFloor`, `Position` (where it was let go), `ReturnTick`. |
| Carrier | `Carrier()` | `FirmMember` or null | The carrying player's object on this machine. |
| Is it me | `IsCarrying(FirmMember.Local)` | `bool` | |
| Return countdown | `ReturnSecondsLeft` | `int` | 0 unless Returning. |
| Where, in words | `PlaceLabel(place, floor)` | `string` | "Floor 3, your tower", "the skybridge on Checkpoint 1", "the plaza". |
| Open floor | `OpenFloor` | `int` | Highest floor anyone may stand on; only rises during a match. |
| Opened by | `OpenedByFirm` | `int` | Firm whose carrier opened it, or `NoFirm` (255) at the start. |
| Floors | `Arena.FloorCount`, `Arena.RoofFloor`, `Arena.FloorLabel(i)`, `Arena.IsCheckpoint(i)` | | For the open-floor ladder. |
| Checkpoints | `CheckpointFloor(firm)` | `int` | 0 = reception. |
| Firms | `FirmName(firm)`, `ColourFor(firm)` | `string`, `Color` | `ColourFor` is cyan for your own firm and amber for the rival on this screen; `ColourFor(NoFirm)` is the loose colour. Local firm: `FirmMember.Local.Firm`. |
| Result | `Snapshot` | `MatchSnapshot` | `Winner` (firm or `NoFirm`), `Reason` (`BriefcaseOnRoof`, `ClockHolding`, `ClockLastCarried`, `ClockNeverCarried`), `EndSeconds`, `EndFloor`. |
| Host actions | `CanHostStart`, `CanHostRematch`, `HostStart()`, `HostRematch()` | | Host only; a client shows "the host starts". |
| Prompt | `PromptFor(FirmMember.Local)` | `MatchPrompt` | Prefer the event below. |
| Beacon | `Briefcase.Current` | `Briefcase` | World-side beacon is drawn by the briefcase itself; an on-screen tracker reads `Briefcase.Current.Centre` locally (no traffic). Range: `Profile.BeaconRange`. |

### Events

| Event | Args | Fires when |
|---|---|---|
| `OnStarted` / `OnClosed` | | The match becomes live / goes away on this machine (join, leave, host gone). A joining client raises `OnStarted` on its first tick after the current values arrive, and is not shown phase, result or briefcase changes that happened before it joined. |
| `OnPhaseChanged` | `MatchPhase previous, MatchPhase next` | Any phase change. |
| `OnCountdownChanged` | `int secondsLeft` | Each whole second of the countdown (5, 4, … 0). |
| `OnClockChanged` | `int secondsLeft` | Each whole second of the match clock. |
| `OnCapWarning` | `int secondsLeft` | Once per match when the clock crosses `Profile.CapWarningSeconds` (60 s); not for a joiner already past it. |
| `OnBriefcaseChanged` | `BriefcaseStatus previous, next` | Any change to the briefcase status (hands, place, floor, return). |
| `OnBriefcasePickedUp` | `int firm` | Someone picked it up. |
| `OnBriefcaseStolen` | `int byFirm, int fromFirm` | It passed from one firm to the other (after the first pick-up). |
| `OnBriefcaseDropped` | `int firm` | The carrier let go (any reason). |
| `OnBriefcaseReturning` / `OnBriefcaseReturned` | | It left the buildings / reappeared. |
| `OnFloorOpened` | `int floor, int byFirm` | The open floor rose (also `OnOpenFloorChanged(int floor)`). |
| `OnCheckpointSaved` | `int firm, int floor` | A firm's respawn point moved up. |
| `OnMatchEnded` | `MatchSnapshot` | The result is in. |
| `OnArenaChanged` | | Towers rebuilt: re-read floor names and counts. |
| `FirmMember.OnNotice` | `MatchNotice` | Local player only: `RoofNeedsBriefcase`, `FellOut`, `BriefcaseKnockedLoose`, `FirstGateRefusal` (first shut gate of the match: explain the rule), `GateRefused` (later ones: a short line). |
| `FirmMember.OnPromptChanged` | `MatchPrompt` | Local player only: `PickUpBriefcase` / `DropBriefcase` / `None`, only on change. |
| `FirmMember.OnLocalChanged`, `OnRosterChanged`, `OnFellOut` | | The local player or the roster changed; someone fell out (for sound). |
| `MatchGates.OnGateTouched` (static) | `int floor` | Local player only, the moment their body hits a shut gate, before the host's refusal arrives; the gate itself pulses brighter for 0.3 s. |

Sounds (lane 5/9's frozen `UiSoundEvent` list): `OnCountdownChanged` → `CountdownTick`, Running → `CountdownGo`, `OnFloorOpened` → `FloorOpened`, `OnBriefcasePickedUp` / `OnBriefcaseStolen` → `BriefcaseTaken`, `OnBriefcaseDropped` → `BriefcaseDropped`, `GateRefused` / `FirstGateRefusal` / `RoofNeedsBriefcase` → `Error`, `OnCapWarning` and checkpoint lines → `Notification`, `OnMatchEnded` → `MatchWon` or `MatchLost` by the local firm.

The HUD's lines (`MatchHud.cs`) are the copy to keep or improve: corporate, played straight, refusals say why.

## Hooks for other systems

- **Lobby:** `MatchDirector.HostStartRun(IReadOnlyDictionary<int, int> firmByClientId)`, host only: client id → firm (0 = Building A, 1 = Building B). Moves the listed players (and their `TeamSpawner` seats) into those firms, then starts a run as Start does. Works while waiting or after a result; returns false otherwise. Players not listed keep their firm.
- **Blasts and throws:** `Briefcase.Throw(velocityChange)` shoves a loose briefcase (host only).
- **Shared hands:** `MatchDirector.HandsFull` (static, every machine): return true while a player's hands hold something else; the briefcase prompt is then not offered and the host refuses the pick-up. Set by the integration glue ([INTEGRATION.md](INTEGRATION.md)).
- **Sound, effects, UI:** every event on `MatchDirector`, plus `FirmMember.OnNotice`, `OnPromptChanged` and `OnFellOut`.

## Inputs (lane-local, for the movement lane to fold in)

Defined in code in `FirmMember` until the shared actions asset takes them:

| Action | Keyboard | Pad |
|---|---|---|
| Interact (pick up) | **F** | Y (Xbox), Triangle (PlayStation) |
| Drop | **G** | D-pad down |

The owner asked for **E**; E is lean-right in the shared actions asset, and the asset's own `Interact` action is already F. Carried items use the same two keys; the briefcase and an item share the hands, so G only ever drops one thing ([INTEGRATION.md](INTEGRATION.md)).

## Scripts

Under [Assets/_Project/Scripts/Match/](../../Assets/_Project/Scripts/Match/), namespace `CloseTheDeal.MatchMode` (`Match` would clash with `System.Text.RegularExpressions.Match` in `KitImport.cs`):

- `MatchDirector.cs`: scene object; the synced state, every event, the clock, read-only queries for UI (`CaseStatus`, `CheckpointFloor`, `Carrier`, `ColourFor`, `PlaceLabel`, `PromptFor`, `SecondsLeft`, `Snapshot`).
- `MatchDirector.Host.cs`: host-only outcomes: start, rematch, `HostStartRun`, pick up, drop, return, checkpoint, respawn, join-in-progress spawn.
- `MatchReferee.cs`: host-only, once per tick after physics: falls (in every phase), knocks, where the briefcase is, checkpoints, the roof and the clock.
- `MatchRules.cs`: the clock rule as a pure function.
- `MatchArena.cs`: floor, building and neutral-ground maths from the built towers; no trigger volumes.
- `MatchSounds.cs`: on the Match object; plays the sound table above through `UiSoundPlayer` on each player's own machine (refusals at most one per 0.6 s).
- `MatchGates.cs`: on the Match object; builds a gate at each stair exit above the ground floor in both towers, opens them up to the open floor and lets the carrier's body through (`Physics.IgnoreCollision`).
- `MatchCrossings.cs`: on the Match object; builds the plaza and a skybridge on each checkpoint floor locally after every tower build.
- `FirmMember.cs`: on `Player.prefab`; firm, slot, the two keys, host teleport, notices.
- `Briefcase.cs`: on `Prefabs/Match/Briefcase.prefab`; shows the state, eases into the hand, tints by holder.
- `MatchHud.cs` shows the match HUD from countdown onward. One translucent strip shows the highest open floor, quarter clock, briefcase objective and location. Ticks fill as floors open. A brief notice announces a new respawn checkpoint. The clock turns amber at the cap warning. The countdown sits above the crosshair. A cobalt banner names the firm that opens a floor. Notices use a cobalt mark on paper at lower centre. A refusal uses amber and a shake, and clears when the floor opens. Pickup and drop prompts track the active keyboard or controller. The briefcase edge marker shows distance and direction off screen, using a square for a loose case, a diamond for the player’s firm, and a triangle for the rival. A steal flashes the marker. Older setups without the kit marker fall back to an on-screen square.
- `MatchMenuPanel.cs`: the match's section on the game menu card, on a kit plate: headline, the result sentence (`MatchDirector.ReasonLine`) as a status line, and the kit's primary button: Start before the first match, Rematch after a result. Only the host sees the button. Other players see the ready-up or rematch instruction in the status line. Its words are per-scene Inspector values. Lane 5's results screen replaces this section once it lands; lane 11's `MatchRecords` holds the per-player stats it will show.
- `MatchProfile.cs`, `FirmDefinition.cs`, `MatchTypes.cs`.

Outside the folder: `Tower/TeamSpawner.cs` (balanced teams, a spawn hook and override, `AssignTeam` for a lobby, seats freed on leave), `Tower/TowerBuilder.cs` (`Rebuild()`, floor queries, building roots, clients follow a new seed), `UI/GameMenu.cs` (`Open()`).

## Setup

**Close the Deal > Lanes > Set Up GameMode** ([GameModeLaneSetup.cs](../../Assets/_Project/Editor/Lanes/GameModeLaneSetup.cs)) makes the firm and match assets, the briefcase prefab, adds `FirmMember` and a `CarryAnchor` to `Player.prefab`, and builds `Scenes/Lanes/Lane_GameMode.unity` with short towers (`Profiles/Match/LaneTower.asset`: reception, two floors, a checkpoint, the boardroom, the roof). **Add GameMode To Open Scene** adds the match (with its plaza and skybridges), HUD and menu section to any scene that already has the tower and game UI. Both only add what is missing and fill an empty slot; they never retune. **Rebuild GameMode Briefcase** rewrites the briefcase prefab while nobody has edited it.

## Known limitations

- **The gates and the GDD's breakable locked doors now do the same job** (hold a team on a floor). Both are in: a door still has to be broken, and the floor above still needs the briefcase. Which to keep is a design question.
- **Shots pass through shut gates** (`Let Shots Through` on `MatchGates`, on by default): the panels sit on Unity's built-in Ignore Raycast layer, which the weapons' shot test skips; bodies still collide. The greybox test blast (`PlayerMotor`) and the aim ray still stop at a gate.
- **The stair cage is part of the gate.** If the cage glass is made breakable, players could leave the stairs through a broken pane above the open floor.
- **Carrying the briefcase opens a floor just before the top step** (within 5 cm of the floor surface), so the gate opens as the carrier arrives rather than once they are through it.

- **Greybox.** A plain case with a dark handle, seam and clasps, kept stripe-free so it never reads as the pickups lane's Grenade Case; no sound or effects yet, though every event above is there to hang them on.
- **Crossing is a design decision.** The skybridges are the minimum that makes stealing possible; the GDD's zipline gun is not built. The tower geometry itself is unchanged.
- **Getting on the plaza or a bridge means climbing the 0.6 m sill** (a mantle, 0.4–1.6 m in the movement profile).
- **Blasts don't move the briefcase.** `Knockback.Explode` only throws `Prop`s; wire `Briefcase.Throw` in when weapons land.
- **The gap counts as outside** except the plaza and the bridges. A zipline crossing over 1.2 s would send its rider back.
- **A briefcase wedged unreachably inside a building** has no recovery, except one lodged on a floor above the open floor (a knocked-loose case on a shut slab), which is sent back once it comes to rest there (never mid-air: a case thrown up the stairwell lands first).
- **Outside `Lane_Integration.unity` the host starts the match from the menu.** In that scene the lobby desk starts it through `HostStartRun` ([INTEGRATION.md](INTEGRATION.md)).
- **The lobby taking the case off the winner after a result** is tidying up: it raises no drop event and plays no drop sound.
- The client's player is put back by the host's correction, so a teleport shows as one corrected tick on the owner's screen.
