# NARRATION

**Status:** Current (branch `claude/lane-gaps-5e5605`, not merged) · **Last updated:** 2026-11-14

## What the player sees

The secretary keeps talking once the match starts. She comments on the start, the first pick-up, the first kill, every steal, each floor opening, your firm's checkpoint moving, window kills, your own fall, the briefcase coming back, the last minute and the result, in the same flat voice as reception. Lines arrive as subtitles; there is no voice audio yet.

She comments rather than chatters: after a line she stays quiet for 4 s plus the line's own time, unless something more important happens (a window kill cuts into a floor line; the result and the clock always speak).

**Every line is a proposal for Adam.** They are in `Assets/_Project/Profiles/Narration/MatchSecretaryLines.asset`.

## Who owns what (AGENTS.md §3)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Which moment happened | **Host**, through state that is already synced: the match snapshot, the briefcase status, the open floor, checkpoints, and the records' kill feed | Nothing new | — |
| Which line, and showing it | Each machine, locally | Nothing | — |

So two players may read different lines for the same moment. Win and loss lines are chosen by the reader's own firm.

## One subtitle source

`Subtitles` (static, namespace `CloseTheDeal.Narration`) is the one place every spoken line passes through on a machine. The lobby's reception secretary raises into it too (one line added to `Secretary.Say`, speaker "Reception"), so a subtitle bar and a future voice player listen to one stream.

| Member | Notes |
|---|---|
| `Shown` | `Action<SubtitleLine>`: `Speaker`, `Text` (filled in), `Seconds`, `Priority`, `Moment` (the moment's name, for picking a voice clip). A new line replaces the one up. |
| `TryGetCurrent(out line)` | The line still up, for a bar opened late. |
| `Show(line)` | Refuses a line while a higher-priority one is up. |

The lobby's `Secretary.LineSpoken` stays for anything already using it.

## Designer values

`MatchSecretaryLines.asset`: **Speaker**, **Seconds Per Word** (0.35), **Minimum Seconds** (2.5), **Quiet Seconds** (4), and per moment a **Priority** and its **Lines**. Tokens: `{name}`, `{firm}`, `{rival}`, `{killer}`, `{victim}`, `{floor}`, `{place}`, `{time}`. For Won and Lost, `{firm}` is the winning firm. A moment with no lines is silent.

## Scripts

[Assets/_Project/Scripts/Narration/](../../Assets/_Project/Scripts/Narration/): `Subtitles.cs`, `MatchSecretaryLines.cs`, `MatchSecretary.cs` (on the **Records** object).

## Known limitations

- **No voice.** `Moment` is there for a voice player to key clips on.
- **Names** come from the reception board ("(no Steam)" without Steam), so local tests read oddly.
- A first pick-up, first kill and steal are said from each machine's own view of the events; a player who joins mid-match never hears that match's "first" lines.
