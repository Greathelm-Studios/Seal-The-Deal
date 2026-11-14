# NAMEPLATES

**Status:** Current (in `buffer`; built by the gaps lane) · **Last updated:** 2026-11-14

## What the player sees

A teammate within 40 m has their name over their head on the kit's white plate, navy 18 px type, with a small cyan diamond: shape and colour, never colour alone. It shows through glass and walls, fades in and out with the kit's panel timing (instantly with Reduce motion) and hides while they are dead.

Rivals get no name, so sightlines across the gap stay clean. A rival who walks into your tower gets an amber chevron over their head for 2 s from the moment they step in, then nothing.

Rules from `ArtSource/UI/research/ux_research.md` ("Name tags"). The research note's ◆ / ▲ glyphs are drawn with the kit's square and chevron sprites, since the kit's font has no glyphs for them.

## Who owns what

Local only, nothing sent. It reads the synced firm and position of every player, the match phase and arena (to know when a rival is inside your tower), and names from the reception board (`MatchRecords.NameOf`).

## Designer values

On the **Nameplates** child of `Prefabs/Gaps/Records.prefab`: **Range** (40 m), **Headroom** (0.35 m above the head), **Raider Seconds** (2), **Name Size** (18 px at 1080p), and the kit references (tokens, Plate prefab, square and chevron sprites). Every value has a tooltip.

## Cost

One chip and one marker per other player, made when they join and destroyed when they leave. Each frame: one projection per player, no allocation, no text change (names are redrawn only when the roster or reception board changes).

## Contract

`Nameplates.ShowsName(member)`, `ShowsMarker(member)`: whether this screen shows them now, for checks and other UI.

`WorldToCanvas.TryProject(camera, canvas, world, out at)`: the one world-to-canvas step, shared with the UI kit's off-screen edge marker (agreed with lane 5). False behind the camera, with `at` mirrored so a marker can clamp it to an edge.

## Setup

A **Nameplates** overlay canvas (sorting 5, under the kit's KitUI at 20; 1920 × 1080 Expand like the kit) inside `Prefabs/Gaps/Records.prefab`, which `RecordsInstaller` adds to every scene with a match ([RECORDS.md](RECORDS.md) Setup). Tune it on the prefab.

## Known limitations

- **Shows through walls too, not only glass**, like most team shooters. Drawing only through glass would need a sight test per player per frame.
- **The raider marker starts when they enter your tower, not when you first see them.**
- No name in reception for the other firm: the same rule applies there. Say if reception should show everyone.
- Without Steam, names read "Associate 1", "Associate 2".
- **Seen only in two headless stills** (host view, 1280 × 720): the team chip reads as specified; the raider marker read too small at 22 px and was raised to 32 px after the capture, not re-captured. Not seen in motion or in a real window.
- Fonts are the kit's stand-in (Liberation Sans) until the shipping faces land.
