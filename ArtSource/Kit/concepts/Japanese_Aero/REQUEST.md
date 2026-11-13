<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-13
  Prompt    : Alex: "close! push it more to a 2000s japanese corporate futurism, frutiger aero, gen x soft
              club vibe, as we changed the narrative to be specifically a japanese office/company"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : Brief to Codex for paintovers of the real KitOffice captures in the new direction, so the
              restyle is agreed on the actual level (its cameras, layout and pieces) before the kit is changed.
-->
# Japanese corporate Aero: paintovers of KitOffice — request to Codex

**From:** Alex (environment artist), drafted by Claude · **Date:** 2026-11-13 · **Status:** Waiting for Codex

Seal the Deal is a fast close-quarters PvP shooter set in one large office labyrinth. **The company is now specifically Japanese.** Alex says the current level is close, but wants it pushed to a **2000s Japanese corporate futurism / Frutiger Aero / Gen X Soft Club** vibe.

The attached images are Unity captures of the level as it is now (`ArtSource/Kit/review/unity_office/`). **Paint over them.** Keep each camera, the room layout, the stair, walls, glass, columns, desks and counters where they are; change their look, add the dressing the style needs, and restyle the signage.

Use your **image generation tool**, with each capture as the image to edit.

## What the direction means

- **2000s Japanese corporate futurism:** the optimistic 2000–2006 Japanese corporate interior: glossy white and silver, rounded capsule forms, translucent frosted plastics, pale aqua and sky blue, LED dot-matrix and scrolling signs, bilingual Japanese/English wayfinding with big floor codes ("3F"), tidy corporate ID, early flat-panel silver monitors, white phones.
- **Frutiger Aero:** glossy translucency and sheen, aqua-to-white gradients, sky and clouds, water and fresh green nature (living plants, a water wall or aquarium), glass orbs and soft highlights, a clean optimistic "eco-tech" feel. Bubbles and greenery are now allowed (this overrides the art bible's old "no Frutiger Aero" line).
- **Gen X Soft Club:** pale, misty, quiet; silver, ice white and pale blue; frosted translucent layers, thin type, ambient glow, a soft hazy light.
- **A Japanese office, not a theme park.** Use real office life: desks in islands facing each other with the manager's desk at the head, document trays, a water server, a vending machine, an umbrella stand, a shoe or slipper cabinet at an entrance, frosted sliding screens in a modern form. A **hanko seal** motif (red circular stamp) fits the game's title and is welcome as a sparing accent. Avoid clichés: no samurai, cherry-blossom wallpaper, paper lanterns or torii.
- **Signage:** each zone's sign becomes bilingual. Zones and suggested Japanese (check and correct them): L 法務部 LEGAL · R 審査部 REVIEW · C 請求部 CLAIMS · H 審問 HEARINGS · A 記録保管 ARCHIVE. Propose a **fictional** Japanese company name and logo mark for the reception (no real company or brand), with its Japanese and romanised forms.

## Keep (gameplay)

Glass stays near-clear between knee and head height, so players read through two panes (frosting above 2.1 m and below 0.35 m is fine). Cover stays about 1.1 m. No bars across a standing figure between knee and head. Routes, doors and the stair stay open. Characters stay dark-suited and readable.

## Deliver

Write only inside `ArtSource/Kit/concepts/Japanese_Aero/`.

- For each attached capture, two paintovers, **A — Aero** (glossier, more aqua, translucency, water and green) and **B — Soft Club** (paler, misty, silver, minimal): `<capture>_A.png`, `<capture>_B.png`, at the capture's 16:9 framing.
- `contact_sheet.png`: the originals beside A and B, one row per capture, labelled.
- `README.md`, for someone who will rebuild the kit in Blender and Unity:
  - the palette as hex values, with gloss and translucency per material (walls, floor, glass, frosted plastic, silver, accents);
  - each kit piece or prop to change or add, with sizes in metres;
  - the exact signage strings in Japanese with romanisation and English, and the proposed company names;
  - which of A or B you'd build, in two or three sentences.
  Count your image tries.

Run no git commands that change state. Hand back when done; Alex picks before anything is modelled.
