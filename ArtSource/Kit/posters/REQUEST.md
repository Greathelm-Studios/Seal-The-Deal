# Satirical posters and pitch-room slides: request to Codex

**From:** Alex, drafted by Claude · **Date:** 2026-11-15 · **Status:** Waiting for Codex

Read `ArtSource/Kit/posters/COPY.md` first: it holds the 12 poster slogans and the 8-slide pitch deck. The look is mood D in blue (`ArtSource/Kit/concepts/Mood/DECISION.md`, `contact_sheet_2.png` row D, recoloured blue), Frutiger Aero, 2000s Japanese corporate.

## Part 1: poster imagery (image generation)

Paint the **12 poster images** described in COPY.md's "image" column with your image generation tool: glossy early-2000s corporate stock-photo style (the kind on real motivational posters), cool blue and aqua palette, crisp and bright.
- **No text, letters, logos or watermarks anywhere in the images.** The scene session sets all text.
- 1024 × 768 px (4:3, landscape), saved as `ArtSource/Kit/posters/art/Poster_Art_01.png` … `Poster_Art_12.png` (the id from COPY.md).
- No real brands, people's faces or copyrighted characters.
- Also save `ArtSource/Kit/posters/art/contact_sheet.png` (all 12, labelled with id and title) and the prompts in `ArtSource/Kit/posters/art/prompts.json`.

## Part 2: the slide display (extend your animation)

The scene session will draw the 8 slides (exact text) into one atlas: **`Assets/_Project/Art/Animated/Slides_Pitch.png`, 4 columns × 2 rows, slide 1 top-left, reading order, each slide 16:9.** Until it lands, generate a placeholder atlas with numbered slides at that path only if it is missing.
- Add a shader `Assets/_Project/Art/Animated/OfficeSlides.shader`: unlit, slightly emissive, shows one atlas tile at a time and **advances every ~6 s**, with a short (~0.25 s) wipe or fade between slides and a faint screen sheen. Phase per display from its world position, so the two rooms are out of step. SRP Batcher compatible.
- In **your** `Assets/_Project/Editor/Art/KitOfficeAnimation.cs`, have `AddTo` also place two displays (a dark bezel frame plus the slide quad, visual only, no colliders):
  - **Legal conference (firm A):** on the room's north wall, inside face. Display centre (2.0, 1.55, −8.21) in Unity world metres, facing −Z (into the room), screen 1.78 × 1.0 m.
  - **Review conference (firm B):** the 180° turn: centre (38.0, 1.55, −23.79), facing +Z.
  - Ground floor only. Parent them under your "Animated" group. Keep your ownership receipt and Undo behaviour.
- Update `ArtSource/Kit/animated/README.md` with the slide display.

## Rules (as before)

Only `ArtSource/Kit/posters/art/`, `Assets/_Project/Art/Animated/` and your own `KitOfficeAnimation.cs`, `OfficeAnimationOwnership.cs` and `ArtSource/Kit/animated/README.md`. Edit no other existing file. **Do not write `.meta` files** (Unity makes them; last time hand-written headers in them made Unity warn). Do not run Unity. Do not commit. Provenance headers, XML docs, WHY comments and named constants as in `AGENTS.md`.
