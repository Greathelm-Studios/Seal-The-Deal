# MATCH

**Status:** Current · **Last updated:** 2026-11-22

The briefcase and the match's phases. The rules of the game played with them (where the briefcase starts, how a floor is won, when players come back, how the match ends) belong to the game mode: today that is the floor race ([FLOOR_RACE.md](FLOOR_RACE.md)).

The two-tower mode this doc used to describe (plaza, skybridges, floor gates, checkpoints, the 15-minute quarter) was removed on 2026-11-22. It is in git history before the commit "Remove the two-tower mode".

## What the player sees

Two rival law firms, **Ashcombe & Pryce** and **Halloran Voss**, fight over **one briefcase**. (Firm names remain proposals awaiting sign-off.)

Nothing happens until the host starts the match from the Esc menu. A 5-second countdown follows, then the match runs until the mode says a firm has won. The menu then shows the result and offers the host a rematch.

A soft light beacon rises from the briefcase through walls within 30 m; it stays steady when loose or held by your firm and breathes while held by the rival firm. Press **F** near it to pick it up; press **G** to drop it ahead of you. On each player's own screen the case tints **cyan** when held by your firm, **amber** when held by the rival, and leather brown when loose. Being knocked down, dying or disconnecting drops it.

## Ownership and netcode boundary (`AGENTS.md §3`)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Player firm and slot | Host (`TeamSpawner` balance, or `HostStartRun` from a lobby) | Two bytes on `FirmMember` | On spawn or match start |
| Phase, start tick, winner, reason | Host | `MatchSnapshot` `SyncVar` (~11 bytes) | On change only |
| Countdown | Computed locally from `PhaseTick` | Nothing | Never |
| Briefcase state, carrier, floor, last position | Host | `BriefcaseStatus` `SyncVar` (~16 bytes) | On change only |
| Loose briefcase motion | Host simulates; clients kinematic | `NetworkTransform` (30 Hz while moving) | Only while moving |
| Carried briefcase visual | Local on every machine (`_visual` follows hand or view) | Nothing | Never |
| Pick up and drop requests | Owner sends a `ServerRpc`; host validates in arrival order | Outcome rides `BriefcaseStatus` | Per press |
| Notices and fall-out announcements | Host | `TargetRpc` / `ObserversRpc` (1 byte) | Per event |

**Bandwidth (8 players):** match state changes send a few bytes per event; the only continuous traffic is the loose briefcase while moving (about 20 bytes at 30 Hz, 0 once it is carried or at rest).

## Architecture and design rationale

- **The director is the hub, the mode is the referee.** `MatchDirector` owns the two synced values, the pick-up and drop rules and the events the HUD, sounds, records and narration listen to. Each tick on the host it calls the mode's `IMatchRuleset.ServerStep`; the mode decides everything else and calls back into the director (`ServerBeginCountdown`, `ServerBeginRunning`, `ServerPlaceBriefcase`, `ServerMoveBriefcase`, `ServerHandBriefcase`, `ServerSetBriefcaseFloor`, `ServerDrop`, `ServerEnd`). A scene with a Match and no mode logs an error and does nothing.
- **Why a ruleset interface:** the floor race was built beside the tower mode and both had to work until the tower mode was deleted. The seam stayed because it keeps the briefcase's netcode in one place whatever the rules are.
- **Zero-bandwidth carried briefcase (`Briefcase`):** while carried, `ServerPark()` makes the rigidbody kinematic where it was picked up, so `NetworkTransform` sends nothing. Each machine moves `_visual` locally in `LateUpdate` (order 200, after `PlayerCamera`). On a drop, clients snap to `BriefcaseStatus.Position` and the host calls `NetworkTransform.Teleport()` in `ServerRelease`, so the body never interpolates from its parked spot. The beacon quad strips its primitive collider with `DestroyImmediate` on creation so a concave collider never exists under the moving rigidbody.
- **Hidden briefcase:** the mode can say the briefcase is hidden (`BriefcaseHidden`); the beacon and the HUD marker then stay off. The floor race uses this on floor 1 until someone first picks it up.
- **Late-joiner banner suppression:** clients defer announcements until the frame after FishNet delivers the initial `SyncVar` state and raise `OnStarted` on the next tick, so a joiner gets no stale pick-up or end-of-match lines.
- **Safe older-scene fallback (`MatchWiring`):** `MatchMenuPanel` and `MatchHud` check their serialized UI references in `Awake` and switch off cleanly if a scene lacks a slot. `MatchWiring.Raise` isolates listener exceptions on roster events.
- **Designer-authored against runtime-derived (`AGENTS.md §4`):** authored values live in `DefaultMatch.asset` (`MatchProfile`) and `Firm_*.asset` (`FirmDefinition`). Everything else is derived.

## UI and sound contract

Everything below is on `MatchDirector.Instance` (`CloseTheDeal.MatchMode`):

- **State:** `IsLive`, `Phase`, `Snapshot`, `CaseStatus`, `Carrier()`, `IsCarrying(member)`, `PlaceLabel()`, `ReasonLine(result, localFirm)`, `FirmName(firm)`, `ColourFor(firm)`, `CanHostStart`, `CanHostRematch`, `HostStart()`, `HostRematch()`, `HostStartRun(firmByClientId)`, `PromptFor(member)`, `SolidMask`, `Briefcase.Current`, `MatchDirector.Ruleset`.
- **Events:** `OnStarted`, `OnClosed`, `OnPhaseChanged`, `OnCountdownChanged`, `OnBriefcaseChanged`, `OnBriefcasePickedUp`, `OnBriefcaseStolen`, `OnBriefcaseDropped`, `OnMatchEnded`; on `FirmMember`: `OnNotice`, `OnPromptChanged`, `OnLocalChanged`, `OnRosterChanged`, `OnFellOut`.
- **Sounds (`MatchSounds`):** countdown ticks and go, briefcase taken and dropped, a fall, match won or lost.
- **HUD and menu (`MatchHud`, `MatchMenuPanel`):** the briefcase tracker, countdown, one-off lines, the pick-up and drop prompt and the briefcase marker, built from UI kit pieces. The mode adds its own HUD beside it.

## Files and setup

Under [Assets/_Project/Scripts/Match/](../../Assets/_Project/Scripts/Match/): `MatchDirector.cs`, `MatchDirector.Host.cs`, `MatchRuleset.cs`, `MatchSounds.cs`, `MatchHud.cs`, `MatchMenuPanel.cs`, `MatchWiring.cs`, `MatchProfile.cs`, `FirmDefinition.cs`, `FirmMember.cs`, `Briefcase.cs`, `MatchTypes.cs`.

The Match object, its HUD and its menu section are put in a scene by the mode's scene tool (`GameModeLaneSetup.AddMatch`, called by **Seal the Deal > Floor Race > Build Scene**). **Seal the Deal > Lanes > Rebuild GameMode Briefcase** rebuilds the briefcase prefab while the tool still owns it.

## Known limitations

- `MatchEndReason` keeps the tower mode's four endings because saved match history stores the number.
- `FirmDefinition.Tower` is the firm's index (0 or 1); the field keeps its old name because the firm assets store it.
- Briefcase visuals are greybox with no world sound beyond UI events; blasts do not yet call `Briefcase.Throw`.
- Client teleports reconcile as a single-tick correction on the owning client.
- The secretary's lines and first-run hints for floors opening, checkpoints and the quarter clock are still in their assets but nothing triggers them now.
