# RECORDS

**Status:** Current (branch `claude/lane-gaps-5e5605`, not merged) · **Last updated:** 2026-11-14

## What the player sees

Nothing drawn yet: this is the data a scoreboard, kill feed and results screen read. During a match every player's numbers (kills, deaths, falls, window kills, briefcase pick-ups and steals, floors opened, throws, seconds with the briefcase, best floor) are known on every machine, and every death produces one kill-feed line. When the match ends each player gets their own final record, which their profile keeps ([PROFILE.md](PROFILE.md)).

A **window kill** is the GDD's first moment: a rival who falls out of the building within 6 s of your hit counts as your kill.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Every player's numbers | **Host** counts them from host-side events only, while the match is Running | `MatchLineBroadcast` (~30 bytes) to everyone | Once per change: a kill (2 lines), a fall, a pick-up, a floor, a throw, end of a carry, a new best floor. Nothing per frame. |
| New match | **Host**, at the countdown | `MatchResetBroadcast` (4 bytes) to everyone | Once per match |
| Kill-feed line | **Host** decides killer and kind | `KillBroadcast` (9 bytes) to everyone | Once per death or fall |
| Final record | **Host**, at the end | `MatchFinalBroadcast` (~40 bytes) to each player, their own only | Once per match per player |
| Late joiner | **Host** sends the match's lines so far when they authenticate | One reset + one line per player | Once per join |
| Leaver | **Host** removes their line | `MatchLineBroadcast` with `Gone` | Once per leave |
| Profile | Each machine, its own, **only from the host's final record** | Nothing | — |

FishNet broadcasts, not a NetworkObject: `MatchRecords` is a plain scene object, so no prefab or networked-object list changes. **Bandwidth at 8 players:** a busy minute is a few dozen ~30-byte messages to 7 clients, well under 1 KB/s.

**A client can't claim a stat:** no message goes from a client to the host, every machine's numbers are the host's, and the profile is written only from the final record the host sends. The scripted check spoils a client's own copy with 99 kills before the end and shows the profile gets the host's 0.

## Rules the code keeps

- Counting happens only on the host and only while Running. The countdown resets every line.
- Bodies without a `FirmMember` (target dummies) are not counted.
- A kill counts only against the other firm. Dying to the world, yourself or a teammate is a death with no credit (`KillKind.Died`).
- A fall is not a death. It is credited as a window kill to the last rival who hit the faller within **Window Kill Seconds**.
- A steal is a pick-up from the firm that held or last held the briefcase before.
- Carry time ends when the briefcase leaves the carrier's hands, the carrier leaves, or the match ends.
- Best floor is sampled by the host every **Floor Check Seconds** (default 0.5 s) and also rises when the carrier opens a floor.
- **Only a finished match is recorded.** The host leaving, the client leaving or the match closing sends no final record.
- Throws count items thrown (`ReleaseKind.Thrown`), not throws that hit: nothing reports a landed throw yet.

## Designer values

On the scene's **Records** object (`MatchRecords`): **Window Kill Seconds** (6) and **Floor Check Seconds** (0.5). Both have tooltips.

## UI contract

On `MatchRecords.Instance` (namespace `CloseTheDeal.Records`), every machine. Read on events; never poll.

| Member | Type | Notes |
|---|---|---|
| `Lines` | `IReadOnlyList<MatchLine>` | Everyone in the match, in arrival order. |
| `TryGetLine(clientId, out line)`, `TryGetLocalLine(out line)` | `bool` | |
| `TryGetLastFinal(out final)` | `bool` | The last final record this session, for a results screen opened late. |
| `MatchRecords.NameOf(clientId)` | `string` | Name from the reception board; "Associate N" if the board doesn't know them. |
| `LineChanged` | `Action<MatchLine>` | A line appeared or changed. |
| `LineRemoved` | `Action<int clientId>` | A player left. |
| `Cleared` | `Action` | New match, or this machine left the game. |
| `Kill` | `Action<KillEntry>` | `VictimClientId`, `KillerClientId` (-1 = nobody), `Kind`: `Killed`, `KnockedOut` (window kill), `Fell`, `Died`. |
| `FinalRecord` | `Action<MatchFinal>` | This player's own record: `Winner`, `Reason` (`MatchEndReason` as byte), `EndSeconds`, `Players`, `Line`. Once per match. |

`MatchLine`: `ClientId`, `Firm`, `Kills`, `Deaths`, `Falls`, `WindowKills`, `Pickups`, `Steals`, `FloorsOpened`, `Throws`, `CarrySeconds`, `SecondsPlayed` (set at the end), `BestFloor`.

Firm colour and name: `MatchDirector.Instance.ColourFor(line.Firm)`, `FirmName(line.Firm)`.

## Scripts

[Assets/_Project/Scripts/Records/](../../Assets/_Project/Scripts/Records/): `MatchLine.cs` (data and wire messages), `MatchRecords.cs` (every machine), `MatchRecords.Host.cs` (counting).

## Setup

**Close the Deal > Lanes > Gaps > Add Records And Profile To Open Scene** adds a **Records** object with `MatchRecords` and `ProfileKeeper` if missing. From the shell, `-executeMethod CloseTheDeal.Editor.Lanes.GapsLaneSetup.AddToIntegrationFromCommandLine` adds them to `Lane_Integration.unity` and saves it. **Run Records Check (solo)** on the same menu plays the scene by script; host and client runs are in [GapsLaneSetup.cs](../../Assets/_Project/Editor/Lanes/GapsLaneSetup.cs).

## Known limitations

- **Names:** without Steam every player's name is "(no Steam)" (the reception board's own fallback), so a local test's feed reads oddly.
- **A dropped client who rejoins starts a new line**: identity is the connection, not the Steam id.
- **Throws landed** isn't counted; it needs a host-side hit report from the pickups lane.
- **Fall-outs are heard from `FirmMember.OnFellOut`**, an observers message the host also receives as its own client. A dedicated server with no local player would miss falls; the game has none.
- Nothing here is seen over Steam, on two computers, or with more than one client.
