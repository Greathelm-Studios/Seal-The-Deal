# Sound licences

Every recorded sound in the game has one row here before it ships. The sound generator (`ArtSource/Audio/ui/make_ui_sounds.py`) **refuses to use a recording that has no complete row** (source, licence and URL filled in), so a file can't reach the game without one.

**Decision needed from Adam:** the GDD says "All art is original. No asset-store packs ship in the game" (docs/GDD.md, line 144, decided 2026-10-03). Shipping recorded library sounds, even CC0 ones, changes that rule for audio. Until the GDD says so, recordings here are stand-ins in the same way packs are for art.

## How to add a sound

1. Only take sources Adam has said yes to, and only the licence named for them below. CC0 needs no credit; CC-BY needs the author named in the game's credits (fill in the Credit column).
2. Keep the original file unchanged at `ArtSource/Audio/recorded/<UI|Items|Weapons|Movement|Ambience>/<the game's file name>`, for example `ArtSource/Audio/recorded/Weapons/Weapon_PistolShot.wav`. It must be a 16- or 24-bit PCM WAV; convert anything else first.
3. Add its row below. "Changes made" says what the generator does (trim, fade, mono, 48 kHz, level), plus anything done by hand.
4. Trims and fades go in `ArtSource/Audio/recorded/edits.json`; then run `python3 ArtSource/Audio/ui/make_ui_sounds.py`. The slot's file in `Assets/_Project/Audio/` is replaced and the audition page shows it as recorded.
5. Keep the licence text or a saved copy of the source page beside the original when the licence asks for it.

## Approved sources

| Source | Licence allowed | Approved by Adam on | Notes |
|---|---|---|---|
| (none yet) | | | Candidates put to Adam: Kenney audio packs (CC0), Freesound filtered to CC0, OpenGameArt CC0 packs, Sonniss GDC bundles (their own royalty-free licence: read it before use). |

## Files

| File | Slot | Source | Author | Licence | URL | Changes made | Credit |
|---|---|---|---|---|---|---|---|
