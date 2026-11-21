# KitOffice animated background: request to Codex

**From:** Alex (environment artist), drafted by Claude · **Date:** 2026-11-15 · **Status:** Waiting for Codex

Seal the Deal is a fast PvP shooter in Unity **6000.4.8f1, URP (Forward+)**, networked with FishNet. KitOffice is an office level built entirely by an editor script (`Assets/_Project/Editor/Art/KitOfficeScene.cs`). Alex picked mood **D in blue** (`ArtSource/Kit/concepts/Mood/DECISION.md`, the look in `contact_sheet_2.png`, row D, recoloured blue) and wants **animated stuff in the background**. Build the animation.

## What to animate (Alex's picks)

1. **CRT screens:** every `Monitor_Flat` shows scrolling 2000s office content: spreadsheet rows, a bar or line chart, a document. Aqua and white on deep navy, slightly emissive, with faint scanlines and a soft dark vignette at the border. Each screen is out of phase with the others (no two in lockstep). It is cheap: one material, with a per-instance phase taken from world position in the shader, so SRP batching survives.
2. **LED tickers:** a dot-matrix strip scrolling sideways, one above each firm reception. Firm A: "ASHCOMBE & PRYCE · WELCOME · 受付 · …"; firm B: "HALLORAN VOSS · WELCOME · 受付 · …". Cyan-white LEDs on a dark panel. The fonts are in `ArtSource/Kit/fonts/` (Michroma, M PLUS 1; OFL).
3. **Light and air in the court:** soft sky-blue light shafts falling from the roof into the double-height court that drift and breathe slowly (additive, camera-facing or crossed planes with a soft gradient and a slow noise scroll), plus sparse dust motes drifting in them (a ParticleSystem, max ~200 particles, URP particle material).
4. **Louvre shadows:** a striped cookie on the sun so window-louvre shadows fall across floors and walls, drifting very slowly (a few cm per second) as if blinds sway. The sun is the scene's directional light, named "Sun" (`RenderSettings.sun`).

The court waterfall already exists (`Assets/_Project/Art/Water/AeroWater.shader` on the kit's `Water_Wall`); nothing to do there.

## Where things are (Unity world metres; the plan's y south is Unity −Z)

- **Building root:** the GameObject "Kit Office" at the origin. Floors at y 0 and 4.5, roof slab top at y 9, soffits at 4.3 and 8.8. The plate is x 0–40, z 0 to −32.
- **Monitors:** prefab instances named `Monitor_Flat` (under "Kit Office/Level N fit-out"). Screen overlay, piece-local: centre **(0, 0.29, 0.181)**, size **0.40 × 0.22 m**, facing **+Z** (the screen glass is a dome, so a flat quad sits ~9 mm off it at the edges; a dark vignette hides that). Parent each overlay to its `Monitor_Flat` instance. About 80 monitors.
- **Firm receptions:** firm A's reception lightbox hangs on the west wall inside face at x 0.28, z −4.2 to −7.8, from y 0.6 to 3.6, facing +X. Firm B's is the 180° turn of it: x 39.72, z −27.8 to −24.2, facing −X. Put each ticker on the same wall just above its lightbox (y ≈ 3.75–4.05, about 3.4 m wide, 0.25 m tall), 6–10 mm off the wall face. Ground floor only.
- **Court:** x 16–24, z −10 to −22, open double height from y 0 to the roof soffit at 8.8 over x 16–24, z −12 to −20. The closing-table plinth is at (20, 0, −16). Keep shafts and dust out of nobody's way: they are visual only, with **no colliders**, and must not hide players (keep them faint).

## How to deliver (strict: another session is editing the scene builder)

- **Write only new files.** Do not edit any existing file in the repo. In particular, do not touch `KitOfficeScene.cs`, `KitSigns.cs`, `KitImport.cs`, `OfficeLook.cs`, any `.unity` scene, any prefab, or `ProjectSettings`.
- Put them under `Assets/_Project/Art/Animated/` (shaders, materials made at run time or by the editor script, and textures) and `Assets/_Project/Editor/Art/KitOfficeAnimation.cs`, plus any runtime component under `Assets/_Project/Scripts/Art/`.
- `KitOfficeAnimation` is a `public static class` with **`public static string AddTo(Transform office)`**. It deletes and rebuilds one child group "Animated" under the office, places everything above, assigns the sun cookie, and returns a one-line report. The scene session will call it once from the build. It must be safe to run twice.
- Generate textures procedurally (C# in the editor script, or a Python script under `ArtSource/Kit/animated/` writing PNGs into `Assets/_Project/Art/Animated/`); keep them small (screens ≤ 1024 px, cookie 256 px).
- Runtime animation must be shader-time based where possible (screens, ticker, shafts). If anything needs a MonoBehaviour (the cookie drift), keep it tiny, allocation-free per frame, and visual only; it must run on every client and never touch networking.
- **Match the repo's rules** (`AGENTS.md`, `CLAUDE.md`): an AI-generated provenance header on every new file with "Reviewed : NOT YET REVIEWED BY A HUMAN"; XML docs on public members; `// WHY:` comments; named constants with units, no magic numbers; `sealed` classes; no per-frame allocations; URP shaders with SRP Batcher-compatible `CBUFFER`s; guard editor-only code with `#if UNITY_EDITOR` or put it in the Editor folder.
- **Do not run Unity** (the Editor is open on this project; a second instance would clash). The scene session compiles and tests in the open Editor. Make sure it compiles by reading carefully; namespace `CloseTheDeal.Editor.Art` for the editor class, `CloseTheDeal.Art` for runtime.
- Finish with `ArtSource/Kit/animated/README.md`: what each element is, its files, its tunables, its cost.

Do not commit.
