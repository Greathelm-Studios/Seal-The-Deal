# UI fonts

Free-licence faces for Seal the Deal's UI, downloaded 2026-11-15 from the google/fonts repo (`ofl/<family>`, Syncopate from `apache/syncopate`), unmodified, each with its own licence file.
All are SIL Open Font License 1.1 except Syncopate (Apache 2.0).
None is in Unity yet: the swap to TextMeshPro font assets is its own change (see docs/HANDOVER_ui_art_direction.md).

## Adam's picks (2026-11-15)

Marked on `ArtSource/UI/review/18_font_candidates.png`.

| Role | Face | File |
|---|---|---|
| UI labels and body | Archivo SemiExpanded (Regular, Medium) | `static/ArchivoSemiExpanded-*.ttf` |
| Data: clock, ammo, readouts, bracketed labels | Martian Mono Light | `static/MartianMono-Light.ttf` |
| Accent serif, used sparingly | Marcellus | `marcellus/Marcellus-Regular.ttf` |
| Title: still being compared | Saira Expanded Light (current), Michroma, Unbounded Light, Syncopate | `static/`, `michroma/`, `syncopate/` |

Adam put dashes, not ticks, beside Michroma, Unbounded and Syncopate; the title face is open until he picks from `ArtSource/UI/review/21b_title_display_faces.png`.

## `static/`

TextMeshPro bakes one static instance, so variable fonts are cut to the instances we use with fontTools' `varLib.instancer` (new name table, nothing else changed):
Saira at width 125 (Light 300, ExtraLight 200), Archivo at width 112.5 (Regular 400, Medium 500), Martian Mono at width 100 weight 300, Unbounded at weight 300.
None of these families has a Reserved Font Name, so the cut instances may keep their names. Ship the family's `OFL.txt` with each.

## Other candidates in this folder

Display: audiowide, jura, lexendzetta, oxanium. Rounded: comfortaa, varelaround, mplusrounded1c, sora. UI: exo2, chakrapetch, rajdhani. Mono: ibmplexmono (the first pick, now replaced), sharetechmono, spacemono, azeretmono, redhatmono. Serif: cormorant.

- `mplusrounded1c/OFL.txt` comes from the upstream repo (github.com/coz-m/MPLUS_FONTS); google/fonts' folder for it has no licence file, but its METADATA.pb says OFL.
- Syncopate, Share Tech Mono and Orbitron-style faces are Latin only: a localisation risk.
- IBM Plex has Reserved Font Name "Plex": don't subset it into a renamed asset.

## Shipping rules

- Ship each family's licence file in the build's credits/licences folder. There is no credits screen yet.
- Clock and ammo go in the mono, never a proportional face.

Also installed to `~/Library/Fonts` on Adam's Mac so VectorCraft, FigCraft and PhotoCraft can use them.
