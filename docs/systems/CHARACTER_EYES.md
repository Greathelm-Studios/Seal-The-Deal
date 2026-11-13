<!--
  AI-GENERATED SOURCE FILE
  Generator : Codex (GPT-6)
  Generated : 2026-11-12
  Prompt    : Give the STDMC player character animated 2D eye cards.
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : Owns the eye-card art contract, setup, tuning and validation.
  Modified  : Claude Opus 5.5 (Claude Code), 2026-11-13 — Alex's eye sheet replaces the
              placeholders; cards 1.4x larger and lower, per Alex's concept; green irises.
-->
# Character eyes

**Status:** Current · **Last updated:** 2026-11-13

STDMC has two large angular eyes with heavy upper lids, green irises and a bored resting look.
They sit right at the fringe's lower edge with strands of hair across them, as in Alex's concept sheet.
The eyes blink at uneven intervals, glance toward travel and aim, and lag behind head turns.
Hit or knockback wins over the airborne look, followed by the focused sprint look and the bored default.
Expressions change in hard cuts.

## Ownership

| Thing | Owner | Synced | Rate |
|---|---|---|---|
| Blink, mood and pupil position | Each client, for each character it draws | Nothing | Evaluated each rendered frame. Property blocks change only when the pose changes. |

The cards are authored under the humanoid head and inherit its final transform.
The eye script reads that pose in LateUpdate at order 120, after procedural animation at 100 and ragdoll at 110.
Travel comes from the smoothed visual position. The local player uses camera direction for aim; observers use the visual body’s facing.
The player’s existing first-person body hider discovers the card renderers at spawn and hides them outright because they cast no shadows.

## Eye art

The art is Alex's eye sheet, [STDMC_EyeSheet.png](../../ArtSource/Characters/Eyes/STDMC_EyeSheet.png): a transparent 4 x 4 grid, rows bored / wince / wide / focused, columns open / half / nearly closed / closed, both eyes per cell, pupils drawn in.
[build_eye_atlas.py](../../ArtSource/Characters/Eyes/build_eye_atlas.py) turns it into the three textures below: it drops the nearly-closed column, cuts each pupil out of the whites so the iris can slide, aligns every mood on its own open-frame pupil, flattens the colours, and draws one green iris with a navy pupil (green from the concept; the sheet's pupils are navy).
To repaint, replace the sheet at the same layout and run `python ArtSource/Characters/Eyes/build_eye_atlas.py` from the repo root; it overwrites the three PNGs in place, keeping their import settings. [atlas_preview.png](../../ArtSource/Characters/Eyes/atlas_preview.png) shows every frame with the iris at rest, at the card's proportions.
The output files keep their `Placeholder_` names so the material and import settings carry over; they now hold the real art.

## Texture layout

Separate lid and opening atlases let each expression keep independent brows and lashes while the same iris slides behind all the lids.

| Texture | Size | Contents |
|---|---|---|
| `Placeholder_Lids.png` | 1536 × 1024 px RGBA | Lids, lashes, brows and sclera in flat colours. Alpha 0 outside the artwork and 255 inside. |
| `Placeholder_Openings.png` | 1536 × 1024 px grayscale | White only where the moving iris may show through. Black everywhere else, including the full closed column. |
| `Placeholder_Iris.png` | 256 × 256 px RGBA | One iris and pupil on transparent canvas, at the point where every frame's pupil sits (centred across, 165 px down from the top). The sprite shares one eye’s canvas and uses the same centre and scale for both eyes. |

Both atlases use the same grid when read from the top left.

| Row | Mood | Columns from left to right |
|---|---|---|
| 1 | Bored | Rest, half blink, closed |
| 2 | Wince | Rest, half blink, closed |
| 3 | Wide | Rest, half blink, closed |
| 4 | Focused | Rest, half blink, closed |

Each cell is 512 × 256 px. The first 256 px are the eye on the **viewer’s left** in a front view and the next 256 px are the eye on the **viewer’s right**. (The shader reads only the grid, so any resolution with this 6 × 4 layout works; the first pass was half this size.)
Each eye canvas is square but covers a card 1.3 times wider than tall, so the art is stored squashed horizontally; the build script does that.
Keep 16 px clear on all sides of each eye and align the opening to the sclera in every frame.
Every frame's pupil sits at the iris sprite's point before movement.
Keep the iris clear of the canvas edges over the configured movement range.
Use PNGs with clamp wrapping, no compression, no mipmaps and no power-of-two rescaling.
Import the lids and iris as sRGB and the mask as linear.
Assign replacements to the three texture slots on [STDMC_Eyes.mat](../../Assets/_Project/Art/Characters/Eyes/STDMC_Eyes.mat).

## Files and placement

- [Eyes assets](../../Assets/_Project/Art/Characters/Eyes/) hold the alpha-clipped URP shader, shared material, placeholder PNGs and fitted card meshes.
- [CharacterEyes.cs](../../Assets/_Project/Scripts/Player/CharacterEyes.cs) drives the cards through one reused property block.
- [EyeProfile.cs](../../Assets/_Project/Scripts/Player/EyeProfile.cs) defines the tuning in [DefaultEyes.asset](../../Assets/_Project/Profiles/DefaultEyes.asset).
- [STDMC_Toon.prefab](../../Assets/_Project/Art/Characters/STDMC/STDMC_Toon.prefab) carries the cards and driver into the player through its existing nested prefab.
- [CharacterEyeSetup.cs](../../Assets/_Project/Editor/Art/CharacterEyeSetup.cs) provides `Close the Deal > Art > Add STDMC Eye Cards` in STDMC_Toon’s Prefab Mode. It adds missing setup to a bare prefab after confirmation and leaves existing cards alone. Save the prefab in Prefab Mode to keep the addition.

The tool resolves the head through the humanoid avatar and samples the baked skin mesh in the bind pose, across both 73 × 56 mm footprints (16 × 16 segments), centred 1.605 m up and 64 mm apart.
The mesh vertices sit 3 mm forward of the sampled skin, with a small raster depth bias to keep the eye art clear of the face; at 1.5 mm and 8 segments the larger cards let the cheek show through.
The tool only adds cards to a prefab that has none. To refit after changing its size constants, delete the two cards, the `CharacterEyes` component on the prefab root and the two `STDMC_Eye*.asset` meshes, then run it again.
Hair still participates in the ordinary depth test.

## Tuning

| Default Eyes Inspector value | Default | What it does |
|---|---|---|
| Blink interval | 2–5 s | Random open-eye wait after each blink. |
| Blink duration | 0.16 s | Total time through half, closed and half. |
| Glance range | 0.16 eye widths | Maximum horizontal or vertical pupil displacement, expressed in eye widths. |
| Glance speed | 1.6 eye widths/s | How quickly the pupil reaches the new direction. |
| Wince threshold | 0.35 | Fraction of the stagger signal that switches to the wince. Knockback always wins. |
| Wide threshold | 0.5 | Fraction of the airborne pose that switches to wide eyes. |
| Focus threshold | 0.5 | Fraction of the speed gap between a walk and full sprint where eyes narrow. |

## Known limitations

- Placement is fitted to this STDMC rig and hair. In a three-quarter view the far eye's outer corner reaches slightly past the cheek.
- The "wince" row of the sheet reads as a skeptical squint rather than a squeeze; repaint that row if a hit should read more clearly.
- Point filtering leaves small stair-steps on the lid lines in close-ups.
- Review renders: [11](../../ArtSource/Toon/review/11_stdmc_eyes_v2_front.png), [12](../../ArtSource/Toon/review/12_stdmc_eyes_v2_three_quarter.png), [13](../../ArtSource/Toon/review/13_stdmc_eyes_v2_from_12m.png), bind pose in an isolated preview scene with one sun. 09 and 10 show the first, placeholder pass.
- Remote aim uses the facing already available on each client. Vertical camera aim is available to the owning client.
- Play Mode, ragdoll motion, a network client’s view, first-person self-hiding and mobile rendering remain unverified.
