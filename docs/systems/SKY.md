<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Fable 5.2 (Claude Code)
  Generated : 2026-11-12
  Prompt    : Alex: "i wanna work on the actual skybox and scenery", then four rounds of feedback
              and two cloud references, then "Yes, wire it in"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The owning doc for the sky (AGENTS.md §7): what it draws, where its files are, how
              to tune it, and what has not been checked.
-->
# Sky and distant city

**Status:** Current (used by Apply Office Look; not measured for frame cost) · **Last updated:** 2026-11-12

Outside the windows is a saturated cobalt sky over a city of white towers that rise out of a soft sea of cloud.
The clouds are flat, posterised shapes with chunky pixel-stepped edges.
A hairline grid runs across the sky, and small square outlines hunt along the cloud edges like a camera's autofocus brackets.

The dome and city follow art bible reference 04; the clouds and overlay follow two references Alex supplied on 2026-11-12 (a thresholded low-resolution sky, and clouds traced with square outlines over a grid).

## Files

- Shader: [OfficeSky.shader](../../Assets/_Project/Art/Sky/OfficeSky.shader), in the material shader menu as `Close the Deal/Office Sky`. Everything is drawn in this one shader, city included.
- Material: [OfficeLook_Sky.mat](../../Assets/_Project/Art/Look/OfficeLook_Sky.mat). `Close the Deal > Art > Apply Office Look` builds it from the shader's default values and never overwrites it once a person has edited it.
- Review renders: [ArtSource/Sky/review/](../../ArtSource/Sky/review/).

## Tuning

Tune the material, not the tool. The Inspector is grouped as:

| Group | What is there |
|---|---|
| Sky dome | Zenith, mid, horizon and mist colours (leaning cyan, from the pixel-art reference); how tall the pale horizon band is; and the pixel-art treatment: how many colour steps the gradient has, how strongly it is dithered (0 = smooth), and the dither cell size in screen pixels. The dither is on a fixed screen-pixel grid, so it stays put as the camera turns. |
| Sun | Disc size, glow, the thin halo ring, and the glare that washes over the whole sky, towers included. |
| Distant towers | Wall, glass, graphic and crown-line colours; height, density, how many giant foreground towers, distance haze, and how much darker the side faces are, and sun glints on glass towers (three flat steps of lift toward the top and a diagonal streak, on glass turned toward the sun or away from it). Towers are up to three volumes stacked off-centre with flat, slanted, tapered or rounded crowns. Only the nearest row and the giant towers carry facades (window zones with blank belts, glass curtain walls, a glass ribbon, the blue slash or a ring mark); the two far rows are plain hazed silhouettes. |
| Clouds | Amount of horizon cumulus and of the broken field overhead; size; pixel chunkiness; number of flat tone steps; contour lines; drift. |
| Cloud sea below | Sunlit and hollow colours, amount (0 = plain mist), billow size, how high the cloud tops rise in front of the towers, the white glow where sea meets sky, and how far it matches the pixel clouds above (0 = fully soft, 1 = the same flat stepped look; 0.5 by default, which snaps it to the same pixel grid and half-steps its shading). This is the one softly shaded cloud in the sky: the pixel clouds are above the horizon, the sea is below it, and the towers rise out of it. |
| Overlay | Grid strength and size; focus-square strength, size, and how often each one re-focuses (0 freezes them). |
| Plane | Seconds between planes (0 = none), seconds to cross, size, contrail length. A small jet flies a straight line in front of the cloud field and tears a channel through it that spreads behind the plane and closes again; one focus bracket tracks it. Each flight takes a new heading and lane. |

The scene fog colour in [OfficeLook.cs](../../Assets/_Project/Editor/Art/OfficeLook.cs) is set to the sky's default horizon colour.
If the horizon colour is changed in the material, change the fog to match, or far geometry will fade to a different colour from the sky behind it.

## Known limitations

- **The cloud sea hides the street.** It is sky, drawn below the horizon; real ground geometry would show through it as normal. Whether the game still has a street under the towers is a design question, not settled here.
- **Towers are whitened inside the shader, not by the grade.** The blue grade is global and cannot be eased for the sky alone, so the towers use a pale-blue shade colour, a lit colour above 1 that glows white under the bloom, and less haze (0.45) than before. Raising "Distance haze" or the shade colour's blue brings the blue-grey back.
- **The cyan is stronger than the art bible allows.** The bible keeps cyan to thin lines and tiny indicators; the dome now leans cyan at Alex's request ("lean into more of this look", 2026-11-12, from the pixel-art reference). Pull Colour mid sky back toward cobalt to return to the bible.
- **The city is painted into the sky.** It never shifts as the player climbs, so it only works as the far city. Anything meant to feel close needs real geometry.
- **Pixel blocks live in the sky, not on the screen**, so they are larger overhead than near the horizon. This keeps them still when the camera turns.
- **All cloud is drawn behind the whole city.** Clouds between the tower rows were tried and read as the towers clipping through them.
- **Clouds are noise, not painted.** They vary in size and spacing but have no composed shapes.
- **Not checked:** frame cost (it is a heavy full-screen pass), Play Mode, the Mobile renderer, and how the squares feel at full frame rate. Only still renders and a six-frame sequence have been looked at.
