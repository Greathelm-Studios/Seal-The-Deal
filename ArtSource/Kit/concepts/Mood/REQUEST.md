# KitOffice mood paintovers: request to Codex

**From:** Alex (environment artist), drafted by Claude · **Date:** 2026-11-15 · **Status:** Waiting for Codex

Seal the Deal is a fast close-quarters PvP shooter set in the office of **AONAGI (青凪総合サービス株式会社)**, a fictional Japanese company, in an early-2000s **Frutiger Aero / Soft Club** corporate style. Two rival law firms (Ashcombe & Pryce, Halloran Voss) lease the floor; a briefcase starts on the "closing table" in the central court.

Alex on the current build: **"It isn't visually interesting, no feeling, there's no life to it, it just looks visually boring."** Before that he found it "overly bright and too evenly lit".

## Attached: the same four captures for every direction

`source/L1_window_hall.png`, `source/L3_court_feature_stair.png`, `source/L4_west_ring_ribbon.png`, `source/look_bench.png` (1600 × 900 or 1280 × 720, Unity, current state).

## Why they read dead (our diagnosis: fix these)

1. **One value everywhere.** White walls, white floor, white ceiling and white props; the only darks are thin navy lines. No big shapes of dark or colour, so nothing leads the eye.
2. **Colour only as hairlines.** Cobalt, aqua and the district colours appear as thin strips and bands, never as a surface you feel.
3. **No focal point per view.** The sky through the windows is the most interesting thing in most frames.
4. **Uninhabited.** Desks are showroom-perfect: dark screens, no paper, mugs, jackets, bags, sticky notes, plants, boxes. Nobody works here.
5. **Light has no story.** Even daylight plus weak pools; no warm against cool, no light falling *on* something.
6. **Repetition without hierarchy.** The same navy joint grid on every wall and the same light lines on every ceiling; nothing is special.

## Paint three directions (3 × 4 = 12 images)

Use your **image generation tool**. **Paint over each capture**: keep the camera, the room layout, the walls, doors, stairs, glass, cover heights and routes exactly where they are. Change light, colour, materials, decals, set dressing and atmosphere only.

**A: Late shift.** Early evening outside (deep blue dusk sky, city lights coming on). Cool Aero ambient inside, cut by **warm desk-lamp and pendant pools** and **glowing CRT screens**. Lived-in: papers, binders, mugs, a jacket over a chair, sticky notes, a desk plant, a box of files, a vending machine glowing. The court lit like a stage over the closing table.

**B: Showroom Aero.** Bright daytime, but **strong colour-blocked zones**: each district owns a big surface colour (L cobalt, A teal, C violet, R sky blue, the court aqua) on whole wall panels, ceiling coves and floor fields, not hairlines. Glowing screens and lightboxes, glossy reflections, a big supergraphic on one wall per view, the closing table as a hero object. Clean, optimistic, catalogue-like.

**C: Golden hour.** Low sun raking through the windows: **long warm shadows of the mullions across the floor and walls**, gold light against cool blue shade, dust motes in the beams, haze in the court. Some life on the desks (screens on, papers), but the light is the hero.

## Rules (keep them)

- Frutiger Aero / Soft Club, 2000s Japanese corporate: pearl white, cobalt, aqua, silver, frosted glass. **No bubbles, no ivy, no cyan floor light trails.** No sci-fi greeble, no dirt or decay.
- Signage stays graphic (painted, printed or backlit), never extruded 3D letters.
- No real brands, logos or copyrighted characters; invent everything. AONAGI's ring logo (an open aqua ring with a red dot) is fine.
- Gameplay must still read: players (the dark capsules) stand out, and cover and routes stay visible. Don't fill corridors with clutter.

## Deliver

In `ArtSource/Kit/concepts/Mood/`:
- `A_<view>.png`, `B_<view>.png`, `C_<view>.png` (12 images, the same framing as the sources).
- `contact_sheet.png`: rows A, B, C, columns the four views, with the source row on top.
- `README.md`, short: for each direction, the 5–8 concrete changes it makes (light, colour, materials, dressing, atmosphere) so we can build it.

Write only in `ArtSource/Kit/concepts/Mood/`. Do not touch anything else in the repo.
