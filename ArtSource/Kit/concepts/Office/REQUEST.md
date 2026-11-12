<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-11
  Prompt    : Alex: "speak to codex or chatgpt to get concept visuals of what the office could look like
              based on our art direction, style and brief; once approved, make the modular kit"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : Brief to Codex for whole-office concept paintings. The per-piece rounds (window bay, wall,
              ceiling) lack a picture of the finished floor to judge them against; this asks for that
              picture first, so Alex can approve one direction before the kit is modelled to it.
-->
# Office concept visuals — request to Codex

**From:** Alex (environment artist), drafted by Claude · **Date:** 2026-11-11 · **Status:** Waiting for Codex

Close the Deal is a 1–4 v 1–4 PvP race: two law firms climb facing office towers floor by floor and fight across a 12 m street gap through the windows.
We have a working blockout and a snap kit of plain boxes. What we don't have is **a picture of what a finished office floor looks like.** Alex wants that before any more kit is modelled.

This is a **concept paintings** request, not a turnaround request. Use your **image generation tool** for the images. Make them look like finished key-art concept paintings of a game environment: clean, designed, lit.

## Read first (all in this repo)

- `ArtSource/Kit/concepts/Office/refs/` — the art bible as text (`art_bible.txt`) and the reference plates. **Section 09, 10, 11, 12 and 20 of the bible are the brief.** Use the plates for mood only; don't copy their layouts, logos or signage.
- `ArtSource/Kit/README.md` — the real dimensions of the floor and kit.
- `ArtSource/Kit/review/interior/*.png` — the current grey blockout, so you can see the real layout.
- `ArtSource/Kit/concepts/Window_Bay_R3/README.md` — the latest window-bay study ("White Trays": broad white sill shelves over dark glass openings). You may build on it, but you're not bound by it.

## The floor you are painting (keep this layout true)

- One office floor: **28 m wide**, a **14 m deep open main floor** facing the street, and a **4 m service spine** across the back.
- **Window wall** on the street side: a solid **1 m sill** (it's cover), floor-to-ceiling glass above it, **a column every 4 m**, and no other mullions. Clear ceiling height **4.3 m**; floors stack every 4.5 m.
- **Open spiral stair** roughly centred, set back near the spine: one full turn per floor around a 0.8 m white column, treads out to 2.6 m, inside a cage of **tall clear glass panels** with an open gap where you arrive. It is **the centrepiece of the floor** and the rival must be able to see people on it. A **locked red door** stands across the stair just past the arrival point (the one warm accent we use; it's a gameplay object players blast open).
- **Service spine** at the back: three opaque rooms (**WC, STORE, UTIL**) with 1.2 × 2.4 m doors off the main floor.
- **One or two glass meeting rooms** on the main floor: glass partitions 2.5 m high, open above to the ceiling. The stair entrance must stay visible *through* them.
- Office furniture as **cover**: desk clusters, low storage, a photocopier, water cooler, office chairs, monitors. Keep long sightlines open; no maze.
- Across the street, **the rival tower** is the mirror image: same façade, same floor heights, 12 m away.

## Style (from the bible, in short)

- **"A multinational company's 2001 vision of 2025."** White, blue, elegant, slightly empty. Closer to an airport or transport-design render than sci-fi. Optimistic, expensive, straight-faced.
- **Large forms first.** A few confident masses, then colour zones, then little else. Negative space is good.
- **Palette:** Paper White #F3F4F4 dominant · Silver Grey #C9CED3 · Deep Navy #111B38 for dark anchors and window bands · Corporate Cobalt #244FB9 in **large confident blocks** only · Cyan #20D4E6 as **thin lines or tiny indicators** only · Near Black #101116 sparingly. Muted grey-blue carpet.
- **Glass is for visibility first:** near-clear, faint cool tint, no mirror reflections, no milky film. Dark-suited people must read as near-black silhouettes **through two panes**.
- **Pale interiors, cool light:** white walls (not cream), cool fluorescent/LED ambience, bright and airy. Strong blue sky outside, pale haze, a white-and-blue block city.
- **The mundane layer is welcome:** cheap ceiling tiles, carpet tiles, strip lights, a beige-grey photocopier, a water cooler. The comedy needs a pristine future with a normal office in it.
- **Bold graphics, integrated:** one big cobalt shape or an oversized floor number does more than many small blue things. Fictional law-firm branding is fine (invent it; no real brands).
- **People:** a few lawyers in **Deep Navy suits**, long-limbed, angular, fashion-illustration proportions (see the suit reference sheet). Small in frame; they are there for scale and readability.
- **Never:** cyberpunk, neon, greeble, pipes or random panels, grime, vaporwave/pastel Y2K, Frutiger Aero bubbles, **plants as decoration** (the plates have planters; we don't want them as a motif), mirror glass, blinds.

## What to deliver

**Three distinct design directions** for the office, each with a one-line idea a person could recognise in a thumbnail. Distinct means a different architectural or graphic idea (e.g. "stacked white trays over dark glass", "cobalt core: the stair and spine are one big blue mass", "lit ceiling grid: the ceiling is a field of light panels"), not the same room in three colours. Invent your own; these are only examples.

For **each direction**, four images (landscape, 16:9, as large as the tool allows):

1. **Cutaway** — the whole floor from above at about 35–45°, front wall cut away or seen through, like a dollhouse: windows at the front, stair in the middle, spine at the back, furniture, meeting room. This is the layout read.
2. **Eye level, main floor** — 1.7 m eye height, standing near the spine looking toward the windows, the stair cage in the foreground or mid-ground, the rival tower visible across the street through the glass.
3. **The rival's view** — from inside the rival tower, 12 m across the street, at the same floor height: three or four bays over two or three floors of our façade, sunlit, with dark suits clearly readable behind the glass, one on the stair.
4. **The stair** — close on the spiral stair and its glass cage from the main floor, the red locked door visible, a suited figure climbing.

Then:

- `contact_sheet.png` — all twelve images on one sheet, one row per direction, the one-line idea written on each row.
- `README.md` — per direction: the one-line idea, what changes in the **kit** to make it (which pieces, which materials, any new material slots as **proposals**, e.g. `Trim_Navy`, `Accent_Cobalt`, `Light_Strip`, `Glass_Spandrel`), and anything that breaks a dimension above. Finish with **which direction you'd build and why**, in three sentences.

If a generated image breaks the layout (stair in the wrong place, mullions across the glass, no sill, plants everywhere, mirror glass), regenerate it rather than ship it. Say in the README how many tries each image took.

## Where to write

Write **only** inside `ArtSource/Kit/concepts/Office/` (images under `01_<Name>/`, `02_<Name>/`, `03_<Name>/`). Don't edit or delete anything else; other agents are working in the neighbouring folders right now. Run no git commands that change state.
Stop after the three directions and hand back; Alex picks one before anything is modelled.
