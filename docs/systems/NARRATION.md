# NARRATION

**Status:** Current · **Last updated:** 2026-11-21

## What the player sees

During a match, the secretary delivers deadpan commentary as subtitles on key moments: match start, first briefcase pick-up, first kill, briefcase steals, floor openings, firm checkpoint advances, window kills, local fall-outs, briefcase returns, 1-minute warning, and match outcome. After speaking, she enforces a quiet cooldown (`QuietSeconds = 4 s` plus line duration) unless a higher-priority moment interrupts (for example, window kills override floor lines, and clock warning / end-of-match always speak). No voice audio clips exist yet.

All shipped lines are proposals for Adam in `Assets/_Project/Profiles/Narration/MatchSecretaryLines.asset`.

## Ownership and netcode boundary (`AGENTS.md §3`)

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Which match moment occurred | Host via existing match state and `MatchRecords.Kill` | Nothing extra | Never |
| Line selection and subtitle display | Local on each machine | Nothing | Never |

Each machine picks randomly from that moment's pool without repeating the same line twice in a row, and selects Won/Lost lines relative to its own firm.

## Architecture and design rationale

- **Single subtitle stream (`Subtitles`):** Static hub in `CloseTheDeal.Narration` shared by both `MatchSecretary` and the reception `Secretary` (speaker `"Reception"`, priority `10`). `Subtitles.Show(SubtitleLine)` rejects empty lines or lines strictly outranked by an active subtitle's `Priority`, then raises `Subtitles.Shown`. `TryGetCurrent(out line)` lets late-opening HUD bars read the active subtitle.
- **Line formatting (`MatchSecretary`):** Resolves `{name}`, `{firm}`, `{rival}`, `{killer}`, `{victim}`, `{floor}`, `{place}`, `{time}` from `MatchDirector` and `MatchRecords.NameOf`, computing duration via `Subtitles.SecondsFor`.

## Files and tuning

- Scripts under [Assets/_Project/Scripts/Narration/](../../Assets/_Project/Scripts/Narration/): `Subtitles.cs`, `MatchSecretaryLines.cs`, `MatchSecretary.cs` (hosted on `Prefabs/Gaps/Records.prefab`).
- Tuning: `Assets/_Project/Profiles/Narration/MatchSecretaryLines.asset` (`Speaker`, `SecondsPerWord` 0.35, `MinimumSeconds` 2.5, `QuietSeconds` 4, per-moment `Priority` and `Lines`).

## Known limitations

- No voice audio clips yet (`SubtitleLine.Moment` provides the hook key for future playback).
- Names fall back to "Associate 1", "Associate 2" when Steam is inactive.
- First pick-up, first kill, and steals are tracked per client from join time, so mid-match joiners do not see earlier "first" lines.
