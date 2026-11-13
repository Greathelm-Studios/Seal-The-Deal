# PROFILE

**Status:** Current (branch `claude/lane-gaps-5e5605`, not merged) · **Last updated:** 2026-11-14

## What the player sees

Nothing drawn yet. Every finished match is kept on this PC: matches, wins, losses, kills, deaths, falls, window kills, briefcase pick-ups and steals, floors opened, throws, time with the briefcase, time played, best floor, most kills in a match, current and best win streak, and the last 20 matches. Quit and relaunch: it is all still there. It also remembers which first-run hints a player has seen.

## Rules

- **Only a finished match counts**, and only from the final record the host sends ([RECORDS.md](RECORDS.md)). The host leaving mid-match, or this player leaving, writes nothing.
- **Holds career stats and seen hints only.** The outfit stays in the lobby's `outfit.json` (`OutfitSave`); settings stay with the UI kit. No value lives in two files.
- **The file is `Application.persistentDataPath/profile.json`**, or the path after `-profileSave` on the command line (scripted checks always pass one, so they never touch a real profile). On this Mac in the editor: `~/Library/Application Support/DefaultCompany/CloseTheDeal/profile.json`; that folder moves if Player Settings' company or product name change.
- **Written safely:** to `profile.json.tmp`, then swapped in, so a crash mid-write leaves the old profile whole.
- **Missing file:** a fresh profile, `State = Fresh`.
- **Unreadable file:** kept as `profile.json.corrupt`, a fresh profile starts, `State = RecoveredFromCorrupt`. A warning in the log, nothing on screen.
- **Older layout:** read and repaired (missing lists filled, the stats array resized). `CurrentVersion` is 1, so there is no older layout yet.
- **Newer layout** (a newer build's file): left untouched; this session plays on an unsaved fresh profile, `State = NewerFileKept`.
- Local only: nothing here is networked.

## Contract (UI, unlocks, onboarding)

`ProfileStore` (namespace `CloseTheDeal.Profile`), static:

| Member | Notes |
|---|---|
| `Current` | `PlayerProfile`, loaded on first use. |
| `Get(ProfileStat)` | One career number, for an unlock rule ("5 wins") or a stats page. |
| `State` | `Fresh`, `Loaded`, `RecoveredFromCorrupt`, `NewerFileKept`. For logs or a quiet settings line, never an error popup. |
| `Changed` | `Action<PlayerProfile>`: loaded, a match added, a hint marked. |
| `MatchRecorded` | `Action<MatchSummary, PlayerProfile>`: for a "career" line on the results screen. |
| `MarkHintSeen(id)` | Remembers a first-run hint; returns false if already seen. `Current.HasSeen(id)` reads it. |

`PlayerProfile`: `Get(stat)`, `Recent` (newest first, `MatchSummary`: when, `Outcome` Won / Lost / NoWinner, firm, reason, players, seconds, kills, deaths, falls, steals, floors opened, carry seconds, best floor), `IsNew` (no match yet), `CreatedUtc`, `LastPlayedUtc`.

**Unlock hook for the lobby:** `OutfitCatalog.Sanitise(loadout, runs)` unlocks by runs started today. To unlock by matches finished or wins instead, it can read `ProfileStore.Get(ProfileStat.MatchesPlayed)` or `Wins`. That is the lobby lane's change to make.

## Scripts

[Assets/_Project/Scripts/Profile/](../../Assets/_Project/Scripts/Profile/): `PlayerProfile.cs` (data), `ProfileStore.cs` (load, save, record), `ProfileKeeper.cs` (scene component: loads at start, records each final).

## Known limitations

- One profile per OS user on this PC, not per Steam account, and no Steam Cloud. Two Steam accounts on one PC share it.
- Stats are trusted from the host. A cheating host can give its friends any numbers; acceptable for a friends' game, not for leaderboards.
- `productName` and `companyName` are still the template's (`CloseTheDeal`, `DefaultCompany`). Renaming them moves the save folder; do it before players have saves.
