<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-13
  Prompt    : Alex on the Japanese corporate Aero paintovers: "do a mix of all 3 i think!"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : Turns "a mix of the current look, A (Aero) and B (Soft Club)" into concrete values, so the kit
              restyle follows one written spec. Read with README.md in this folder (sizes, sign copy).
-->
# Japanese corporate Aero: the mix (decision)

**2026-11-13 · Alex:** build a mix of all three: the current KitOffice, **A — Aero** and **B — Soft Club**. The company is **AONAGI, 青凪総合サービス株式会社** (README.md), a Japanese office.

## What each one contributes

| From | Takes |
|---|---|
| **Current** | Cobalt as the deep identity colour (zone letters, one stripe on each column, the stair column band); the labyrinth, kit sizes and cover heights |
| **A — Aero** | Aqua sign faces and column skins, aqua-frosted divider screens, the contained water wall behind the stair, living green planters, the AONAGI ring logo with its red hanko dot, the dot-matrix reception sign |
| **B — Soft Club** | The calmer surfaces: pearl-white walls at a satin rather than mirror gloss, a softer floor reflection, mist-blue quiet fields, silver hardware, paler blue carpet |
| **A + B** | The Japanese office layer: water server, umbrella stand, vending machine, slipper cabinet, white desk phones, document trays, silver flat-panel monitors with pale keyboards, the bilingual signs |

## Palette (sRGB; smoothness 0–1 as in Unity)

| Material | Colour | Smoothness | Notes |
|---|---|---|---|
| `Wall_PaperWhite` (pearl lacquer) | `#F4F8F8` | 0.70 | between A 0.68, B 0.40 and the old 0.90 |
| `Floor_Carpet` (polished walkway) | `#DCE8EC` | 0.85 | reflections kept, less mirror than 0.97 |
| `Carpet_Dark` (work-area carpet) | `#7E96AC` | 0.06 | paler, between A and B; the old dark carpet read heavy |
| `Ceiling_Tile`, `Ceiling_Grid` | `#F0F5F5` | 0.10 | |
| `Frame_Aluminium` (silver) | `#C8D2D9`, metal 0.85 | 0.65 | |
| `Glass_Frosted` (aqua pearl) | `#C6E9E6`, 65 % opaque | 0.30 | partitions' frost bands and bench dividers |
| `Accent_Cobalt` | `#244FB9` | 0.94 | kept: the deep identity colour, used small |
| `Accent_Aqua` (new) | `#45BFC7` | 0.76 | sign faces, column skins, reception block |
| `Accent_Mist` (new) | `#A8BFD1` | 0.40 | quiet fields: column backs, sign lower band |
| `Accent_Seal` (new) | `#C7473E` | 0.35 | the hanko dot only |
| `Water_Wall` (new) | shader colours: deep `#0F8CB8` → surface `#8CEBF0` | — | animated liquid drawn by the Aero Water Pane shader (see below) |
| `Plant_Leaf` | `#70AF59` | 0.24 | |

Glass stays as it is (near-clear with the two-streak sheen). Frost stays outside the 0.35–2.10 m clear band.

## Kept rules

Cover about 1.1 m; clear glass at body height; no bars across a standing figure; new tall props only against opaque walls; routes, doors and the stair stay open. Characters stay dark-suited.

## Later calls

**2026-11-13 · Alex:** lean more toward A (Aero): "i kinda like the frutiger more". The water wall becomes a glass pane with animated liquid inside, drawn by a shader rather than modelled (`Assets/_Project/Art/Water/AeroWater.shader`). He also finds the scene as a whole still reads "minimal and contemporary rather than early 2000s corporate futurism"; the follow-up list is being agreed with him.
