# PROFILE

**Status:** Current · **Last updated:** 2026-11-22

## What the player sees

Local career persistence on this PC: matches played, wins, losses, draws, kills, deaths, falls, window kills, briefcase pick-ups and steals, floors opened, throws, throw hits, carry seconds, seconds played, best floor, most kills in a match, current/best win streak, the last 20 match summaries, and which onboarding hints have been seen.

## Ownership and netcode boundary (`AGENTS.md §3`)

Local only: nothing is networked. `ProfileKeeper` writes career match results exclusively from the host-authenticated `MatchFinal` broadcast via `MatchRecords.FinalRecord`. Leaving or host shutdown mid-match records nothing.

## Architecture and design rationale

- **Separation of saves:** `profile.json` stores career stats and seen hints; outfits and `runsStarted` live in `outfit.json` ([CUSTOMISATION.md](CUSTOMISATION.md)), and settings live with the UI kit.
- **Serialized background save queue (`BackgroundFileWriter`):** Shared by `ProfileStore` and `OutfitSave`. JSON serialization runs synchronously on the main thread, while directory creation and atomic write (`path.tmp` followed by `File.Replace` / `File.Move`) run sequentially on a single background worker thread (`ConcurrentQueue<WriteRequest>`). `BackgroundFileWriter.Flush()` blocks until pending writes drain before `Application.quitting`, domain reload (`SubsystemRegistration`), or any synchronous `Read()`.
- **Corruption and version handling:**
  - Missing file: starts fresh (`LoadState.Fresh`).
  - Unreadable/corrupt file: copies to `profile.json.corrupt`, logs a warning, and starts fresh (`LoadState.RecoveredFromCorrupt`).
  - Older version (`< CurrentVersion`): `PlayerProfile.Repair` fills null lists and resizes the `stats` array (`LoadState.Loaded`).
  - Newer file (`> CurrentVersion`): leaves the file untouched on disk and suppresses writes for the session (`LoadState.NewerFileKept`).
- **Path override:** Reads `Application.persistentDataPath/profile.json`, or `-profileSave <path>` on CLI, or `SessionState` key `ProfileStore.EditorPathKey` in Editor test runs.

## Contract

`ProfileStore` (`CloseTheDeal.Profile`):

| Member | Notes |
|---|---|
| `Current` | `PlayerProfile`, loaded on first access |
| `Get(ProfileStat)` | Reads a single career stat |
| `State` | `NotLoaded`, `Fresh`, `Loaded`, `RecoveredFromCorrupt`, `NewerFileKept` |
| `Changed` | `Action<PlayerProfile>`: fired on load, match record, or hint marked seen |
| `MatchRecorded` | `Action<MatchSummary, PlayerProfile>`: fired after recording a finished match |
| `MarkHintSeen(id)` | Marks an onboarding hint seen and saves; returns `false` if already seen |

`PlayerProfile`: `Get(stat)`, `Recent` (newest first, up to 20 `MatchSummary` entries), `IsNew`, `CreatedUtc`, `LastPlayedUtc`, `HasSeen(hintId)`.

## Files

Under [Assets/_Project/Scripts/Profile/](../../Assets/_Project/Scripts/Profile/): `PlayerProfile.cs`, `ProfileStore.cs` (includes `BackgroundFileWriter`), `ProfileKeeper.cs`.

## Cash

`ProfileStat.Cash` is the player's unspent cash from floor races ([FLOOR_RACE.md](FLOOR_RACE.md)). It is the last entry of the stats list, so older `profile.json` files load unchanged and start at 0; the file version is still 1. The host sends each player what they earned and that player's own machine calls `ProfileStore.AddCash(amount)`, which saves at once. Unlike match results it is written during a match, floor by floor, so cash earned before a disconnect is kept. Nothing spends it yet, and it is as trusted as the rest of the profile: a local file, awarded by whoever hosts.

## Known limitations

- One profile per OS user account rather than per Steam ID, with no Steam Cloud sync.
- Final match records are trusted from the match host (appropriate for friend-hosted P2P, not competitive leaderboards).
- Player Settings still uses template `CloseTheDeal` / `DefaultCompany`, which determines the default `persistentDataPath` folder until updated.
