# RECORDS

**Status:** Current · **Last updated:** 2026-11-21

## What the player sees

Data layer powering scoreboards, the kill feed, the death screen, and career stats. During a match, every player's numbers (kills, deaths, falls, window kills, briefcase pick-ups and steals, floors opened, throws, throw hits, carry seconds, seconds played, best floor) are known on every machine, and every death or fall produces one kill-feed line. When a match finishes, each player receives their own final record (`MatchFinal`), which their local profile persists ([PROFILE.md](PROFILE.md)).

A **window kill** credits a rival who falls out of the building within `WindowKillSeconds` (6 s) of taking rival damage.

## Ownership and netcode boundary (`AGENTS.md §3`)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Player stats (`MatchLine`) | Host counts from host-side events while Running | `MatchLineBroadcast` (~30 bytes) to all | On change only |
| Match reset | Host on Countdown | `MatchResetBroadcast` (4 bytes) | Once per match |
| Kill-feed line | Host decides killer and `KillKind` | `KillBroadcast` (9 bytes) | Once per death or fall |
| Final record | Host at match end | `MatchFinalBroadcast` (~40 bytes) to that owner only | Once per match per player |
| Late joiner sync | Host sends current lines on auth | Reset + one line per player | Once per join |
| Leaver removal | Host removes line and clears hit/carry state | `MatchLineBroadcast` (`Gone = true`) | Once per leave |
| Career profile | Each PC writes its own from `MatchFinal` only | Nothing | Never |

`MatchRecords` uses FishNet broadcasts on a plain scene object rather than a `NetworkObject` (`< 1 KB/s` at 8 players). Clients never send stat updates to the host.

## Architecture and design rationale

- **Match epoch key (`_hostKey` / `MatchKey`):** Countdown sets `_hostKey = Snapshot.PhaseTick` and broadcasts `MatchResetBroadcast`. Clients ignore `MatchLineBroadcast` packets whose `MatchKey` differs and deduplicate `MatchFinalBroadcast` by `_lastFinalKey`, preventing stale broadcasts across rematches or duplicate final writes.
- **Counting rules (`MatchRecords.Host.cs`):** Counts only on the host while `MatchPhase.Running`. Bodies without `FirmMember` (target dummies) are ignored. Only rival hits qualify for kills/window kills/throw hits; dead bodies flung out by blasts are not double-counted as falls (`IsDead` guard). `_lastHit`, `_joinedAt`, `_carried`, and `_tally` are cleaned on disconnect because FishNet reuses `ClientId`s. `_watchedThrows` unhooks `Pickup.Released` on reset or stop for pickups consumed without releasing (such as guns taken into weapon slots).
- **Best-floor sampling:** `HostSampleFloors` polls `FirmMember.All` every `FloorCheckSeconds` (0.5 s) while Running and updates late joiners' `Firm` once seated.
- **Only finished matches persist:** Disconnecting mid-match or host shutdown (`HostClosed`, `OnClientConnection` Stopped) clears local lines without emitting `FinalRecord`.
- **Death report (`LocalDeath`):** Translates `MatchRecords.Kill` + `Health.AnyRespawned` for the local player into `Down(DeathReport)` and `Back` events for the combat HUD death card.

## UI contract

On `MatchRecords.Instance` (`CloseTheDeal.Records`), read on events:

- **State:** `Lines` (`IReadOnlyList<MatchLine>`), `TryGetLine(clientId, out line)`, `TryGetLocalLine(out line)`, `TryGetLastFinal(out final)`, `MatchRecords.NameOf(clientId)`.
- **Events:** `LineChanged(MatchLine)`, `LineRemoved(clientId)`, `Cleared`, `Kill(KillEntry)`, `FinalRecord(MatchFinal)`.
- **`LocalDeath`:** `Down(DeathReport)` (`Kind`, `KillerClientId`, `RespawnSeconds`, `ComesBackOn`), `Back`.

## Files, tuning, and setup

- Scripts under [Assets/_Project/Scripts/Records/](../../Assets/_Project/Scripts/Records/): `MatchLine.cs`, `MatchRecords.cs`, `MatchRecords.Host.cs`, `LocalDeath.cs`, `RecordsInstaller.cs`.
- Tuning on **Records** (`MatchRecords`): `WindowKillSeconds` (6 s), `FloorCheckSeconds` (0.5 s).
- Runtime install: `RecordsInstaller` (`Resources/Gaps/RecordsInstaller.asset`) spawns `Prefabs/Gaps/Records.prefab` (`MatchRecords`, `ProfileKeeper`, `LocalDeath`, `MatchSecretary`, `FirstRunHints`, **Nameplates**) into any scene with a `MatchDirector` and no `MatchRecords`. Rebuild via **Seal the Deal > Lanes > Gaps > Build Records Prefab**.

## Known limitations

- Without Steam, player names fall back to "Associate 1", "Associate 2".
- Disconnecting and rejoining starts a new `MatchLine` because identity is tracked by connection `ClientId` rather than Steam ID.
- Fall-outs listen to `FirmMember.OnFellOut` (an `ObserversRpc` also delivered to the host client), assuming no headless dedicated server without a local client.
- Not yet verified over Steam across multiple computers with 3+ players.
