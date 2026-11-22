# MATCH

**Status:** Current · **Last updated:** 2026-11-21

## What the player sees

Two rival law firms, **Ashcombe & Pryce** (Building A) and **Halloran Voss** (Building B), race for **one shared briefcase**. The firm whose player carries it onto **either roof** wins. (Firm names remain proposals awaiting owner sign-off.)

Players wait in reception until the host starts the match (via the lobby desk in `Lane_Integration.unity` or the menu button in standalone scenes). A 5-second countdown locks the briefcase until the quarter opens. The briefcase spawns on the **plaza** between the two towers, equidistant from both receptions.

A soft light beacon rises from the briefcase through floors and walls within 30 m; it stays steady when loose or held by your firm and breathes while held by the rival firm. Press **F** near it to pick it up; press **G** to drop it ahead of you. On each player's own screen, the case tints **cyan** when held by your firm, **amber** when held by the rival, and leather brown when loose. Knockdowns, falling out of the building, or disconnecting drop the briefcase.

**The briefcase leads the climb:** nobody in either tower can climb above the highest floor the briefcase has reached (**Open Floor**, starting at `0`). Frosted floor gates seal the stair arrival on every floor above it (and a full ring around the roof stairwell). The carrier passes through; standing on a higher floor opens that floor permanently for both towers. Checkpoint floors connect the two towers with **skybridges** and advance a firm's respawn point once reached at or below the open floor. At **15:00** the quarter closes: the firm holding the briefcase wins (or the last carrier if loose; no winner if never picked up).

## Ownership and netcode boundary (`AGENTS.md §3`)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Player firm and slot | Host (`TeamSpawner` balance or `HostStartRun`) | Two bytes on `FirmMember` | On spawn or run start |
| Phase, start tick, winner, reason | Host | `MatchSnapshot` `SyncVar` (~11 bytes) | On change only |
| Clock and countdown | Computed locally from `PhaseTick` | Nothing | Never |
| Briefcase state, carrier, place, floor, return | Host | `BriefcaseStatus` `SyncVar` (~23 bytes) | On change only |
| Firm checkpoint floor | Host | `byte` `SyncVar` per firm | On change only |
| Open floor | Host | `FloorOpening` `SyncVar` (2 bytes) | On change only |
| Floor gates, plaza, skybridges | Built locally from towers and synced state | Nothing | Never |
| Loose briefcase motion | Host simulates; clients kinematic | `NetworkTransform` (30 Hz while moving) | Only while moving |
| Carried briefcase visual | Local on every machine (`_visual` follows hand/view) | Nothing | Never |
| Pick up / drop requests | Owner sends `ServerRpc`; host validates in arrival order | Outcome rides `BriefcaseStatus` | Per press |
| Notices and fall-out announcements | Host | `TargetRpc` / `ObserversRpc` (1 byte) | Per event |

**Bandwidth (8 players):** match state changes send a few bytes per event; the only continuous traffic is the loose briefcase while moving (~20 bytes at 30 Hz, 0 once parked or at rest).

## Architecture and design rationale

- **Deterministic host referee (`MatchReferee`):** Runs once per host tick (`OnPostTick`) in a fixed order: countdown expiry, briefcase state (orphan drop, return timer, stray/locked-floor recovery), then players (`FellOut`, `StepCheckpoint`, `StepCarrier`, `StepGate`, `StepRoof`), then win decision (`BriefcaseOnRoof` or `MatchRules.ClockWinner`). Evaluating falls in every phase prevents players from getting stranded in the street during lobby/post-match states.
- **Triggerless spatial classification (`MatchArena`):** Transforms world positions into both buildings' local spaces once per `Classify` call to determine exact/padded place (`0`, `1`, `Neutral`, `Outside`), floor index, and surface contact without scene trigger volumes.
- **Zero-bandwidth carried briefcase (`Briefcase`):** While carried or returning, `ServerPark()` makes the rigidbody kinematic where picked up so `NetworkTransform` sends nothing. Each machine moves `_visual` locally in `LateUpdate` (order 200, after `PlayerCamera`), snapping client position from `BriefcaseStatus.Position` on drop and calling `NetworkTransform.Teleport()` on `ServerRelease` so the body never interpolates from its parked pickup spot. The beacon quad strips its primitive collider via `DestroyImmediate` on creation so a concave collider never exists under the moving rigidbody.
- **Predicted local gates (`MatchGates`):** Gates activate/deactivate strictly from synced `OpenFloor` and exempt the carrier's collider via `Physics.IgnoreCollision` (re-applied after `SetActive` toggles, because disabling a collider clears ignore pairs). `MatchDirector.ApplyGates` also re-runs on `FirmMember.OnStartNetwork` in case a carrier's body spawns after `BriefcaseStatus` names them.
- **Late-joiner banner suppression (`MatchDirector`):** Clients defer `Announce` until `_announceFromFrame = Time.frameCount + 1` (after FishNet delivers initial `SyncVar` snapshots) and raise `OnStarted` on the first `OnPostTick` thereafter, preventing stale floor-open or end-of-match banners on join.
- **Safe older-scene fallback (`MatchWiring`):** `MatchMenuPanel` and `MatchHud` validate serialized UI references via `MatchWiring.Complete` in `Awake` and disable cleanly before subscribing if an older scene lacks new slots. `MatchWiring.Raise` isolates listener exceptions on roster events.
- **Designer-authored vs runtime-derived (`AGENTS.md §4`):** Authored values live in `DefaultMatch.asset` (`MatchProfile`) and `Firm_*.asset` (`FirmDefinition`). Derived state includes floor labels, arena bounds, plaza/skybridges, clocks, and checkpoint spots.

## UI and sound contract

Everything below is on `MatchDirector.Instance` (`CloseTheDeal.MatchMode`):

- **State:** `IsLive`, `Phase`, `SecondsLeft`, `CaseStatus`, `Carrier()`, `IsCarrying(member)`, `ReturnSecondsLeft`, `PlaceLabel(place, floor)`, `OpenFloor`, `OpenedByFirm`, `Arena`, `CheckpointFloor(firm)`, `FirmName(firm)`, `ColourFor(firm)`, `Snapshot`, `CanHostStart`, `CanHostRematch`, `HostStart()`, `HostRematch()`, `HostStartRun(firmByClientId)`, `PromptFor(member)`, `Briefcase.Current`.
- **Events:** `OnStarted`, `OnClosed`, `OnPhaseChanged`, `OnCountdownChanged`, `OnClockChanged`, `OnCapWarning`, `OnBriefcaseChanged`, `OnBriefcasePickedUp`, `OnBriefcaseStolen`, `OnBriefcaseDropped`, `OnBriefcaseReturning`, `OnBriefcaseReturned`, `OnOpenFloorChanged`, `OnFloorOpened`, `OnCheckpointSaved`, `OnMatchEnded`, `OnArenaChanged`; `FirmMember` (`OnNotice`, `OnPromptChanged`, `OnLocalChanged`, `OnRosterChanged`, `OnFellOut`); `MatchGates.OnGateTouched`.
- **Sounds (`MatchSounds`):** maps events to `UiSoundEvent` (`CountdownTick`, `CountdownGo`, `FloorOpened`, `BriefcaseTaken`, `BriefcaseDropped`, `Error` debounced by `0.6 s`, `Notification`, `MatchWon`/`MatchLost`).
- **HUD and menu (`MatchHud`, `MatchMenuPanel`):** built from UI kit plates, status lines, key hints, and `UiEdgeMarker` (falls back to on-screen marker if `_edgeMarker` is unset).

## Files and setup

Under [Assets/_Project/Scripts/Match/](../../Assets/_Project/Scripts/Match/): `MatchDirector.cs`, `MatchDirector.Host.cs`, `MatchReferee.cs`, `MatchRules.cs`, `MatchArena.cs`, `MatchCrossings.cs`, `MatchGates.cs`, `MatchSounds.cs`, `MatchHud.cs`, `MatchMenuPanel.cs`, `MatchWiring.cs`, `MatchProfile.cs`, `FirmDefinition.cs`, `FirmMember.cs`, `Briefcase.cs`, `MatchTypes.cs`.

Setup: **Seal the Deal > Lanes > Set Up GameMode** ([GameModeLaneSetup.cs](../../Assets/_Project/Editor/Lanes/GameModeLaneSetup.cs)) and **Add GameMode To Open Scene**.

## Known limitations

- A replacement mode with one briefcase per firm, random floors and a lift keycard is proposed in [FLOOR_RACE.md](FLOOR_RACE.md) (Target, not built).
- Floor gates and breakable stairwell doors ([TOWER.md](TOWER.md)) both gate climbing; whether to keep both is an open design question.
- Shut gates sit on Unity's `Ignore Raycast` layer when `Let Shots Through` is enabled, so bullets pass through while bodies and greybox test blasts still collide.
- Carrying the briefcase opens a floor within 5 cm of the floor surface (at the top step) rather than after clearing the gate.
- Briefcase visuals are greybox with no custom world SFX beyond UI events; blasts do not yet call `Briefcase.Throw`.
- Skybridges require a mantle over the 0.6 m window sill; the street gap counts as outside (no zipline crossing implemented yet).
- A briefcase wedged inside a building below or at `OpenFloor` has no recovery; cases resting on locked floors above `OpenFloor` are returned.
- Client teleports (fall-out recovery or match start) reconcile as a single-tick correction on the owning client.
