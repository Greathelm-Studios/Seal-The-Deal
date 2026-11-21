# NAMEPLATES

**Status:** Current · **Last updated:** 2026-11-21

## What the player sees

A teammate within 40 m shows their name over their head on the UI kit's white plate (navy 18 px text, cyan diamond: shape and colour, never colour alone). It shows through glass and walls, fades in and out with panel timing (instantly with Reduce Motion), and hides while they are dead.

Rivals show no name so cross-gap sightlines stay clean. A rival who steps inside your tower gets an amber triangle marker over their head for 2 s from entry, then nothing.

## Ownership and netcode boundary

Local only: nothing is sent over the network. `Nameplates` reads synced `FirmMember` firms and positions, `MatchDirector` phase and arena bounds (to detect rivals inside your tower), and player names from `MatchRecords.NameOf`.

## Architecture and design rationale

- **Allocation-free per-frame path:** One chip and one marker are created per remote player on roster changes and destroyed when they leave. `LateUpdate` projects head world coordinates via `WorldToCanvas.TryProject` with zero allocations and skips text updates unless `MatchRecords.NameOf` changes.
- **World-to-canvas projection contract:** `WorldToCanvas.TryProject(camera, canvas, world, out at)` returns `false` when the target is behind the camera and mirrors `at` so off-screen edge markers can clamp it to a border.
- **Public queries:** `Nameplates.ShowsName(member)` and `ShowsMarker(member)` report live visibility for checks and other UI.

## Files and tuning

- [Nameplates.cs](../../Assets/_Project/Scripts/Nameplates/Nameplates.cs): on the **Nameplates** overlay canvas (sorting 5, under KitUI at 20; 1920 x 1080 Expand) inside `Prefabs/Gaps/Records.prefab`, installed by `RecordsInstaller` ([RECORDS.md](RECORDS.md)). Tuning: `Range` (40 m), `Headroom` (0.35 m), `RaiderSeconds` (2 s), `NameSize` (18 px), plus UI kit tokens, plate prefab, and diamond/triangle sprites.
- [WorldToCanvas.cs](../../Assets/_Project/Scripts/Nameplates/WorldToCanvas.cs): shared world-to-canvas projection helper.

## Known limitations

- Shows through solid walls as well as glass (per-frame line-of-sight checks are avoided).
- The raider marker starts when a rival enters your tower, not when first seen.
- No nameplates appear in reception for the rival firm.
- Without Steam, player names fall back to "Associate 1", "Associate 2".
- Verified in headless stills (host and client at 1920 x 1080 and 1280 x 720 in `Lane_Integration.unity`), not yet verified in live motion in a desktop window.
- Fonts use the UI kit stand-in (Liberation Sans) until final typefaces land.
