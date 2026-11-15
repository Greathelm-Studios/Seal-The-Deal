# Fonts by pass: stand-ins and shipping picks

Four direction passes (A Survey, B Aero, C Sky, D Seven), each with a display / UI / data trio.
Stand-ins are for drawing the boards on this Mac only; the shipping picks in Part 2 are what goes into TMP font assets.

## Stand-ins for the mockups (installed on this Mac)

Confirmed with `system_profiler SPFontsDataType 2>/dev/null | grep "Family:" | sort -u` (276 families) and the `Location:` lines of the same dump, 2026-11-12.
System fonts here are for boards only: none of them ship (no redistribution licence), except PT Sans / PT Mono, which are OFL (see Part 2).

| Pass | Role | Family (quoted profiler line) | Weights / styles to use | File path | Stands in for |
|---|---|---|---|---|---|
| A Survey | Display | `Family: Kohinoor Bangla` | Light only (rounded-square Latin; title tracked +0.22 em, headings +6 px). Before 2026-11-13: Futura Medium | /System/Library/Fonts/KohinoorBangla.ttc | Saira Expanded Light / ExtraLight (alt Michroma) |
| A Survey | UI | `Family: Helvetica Neue` | Regular, Medium; UltraLight for oversized numerals only | /System/Library/Fonts/HelveticaNeue.ttc | Archivo SemiExpanded (wdth 112) |
| A Survey | Data | `Family: JetBrains Mono` | Light (microtype), Regular (HUD, captions) | /Users/adamf/Library/Fonts/JetBrainsMono-Light.ttf and /Users/adamf/Library/Fonts/JetBrainsMono-Regular.ttf | IBM Plex Mono Light / Regular |
| B Aero | Display | `Family: DIN Alternate` | Bold only (track caps wide, +150) | /System/Library/Fonts/Supplemental/DIN Alternate Bold.ttf | Saira / Rajdhani (rounded-square techno caps) |
| B Aero | UI | `Family: Tahoma` | Regular, Bold | /System/Library/Fonts/Supplemental/Tahoma.ttf, Tahoma Bold.ttf | Exo 2 (wide early-2000s grotesque) |
| B Aero | Data | `Family: Andale Mono` | Regular only | /System/Library/Fonts/Supplemental/Andale Mono.ttf | Share Tech Mono |
| C Sky | Display | `Family: Avenir Next` | Ultra Light, Regular, Demi Bold | /System/Library/Fonts/Avenir Next.ttc | Sora / Zen Kaku Gothic New (thin geometric title type) |
| C Sky | UI | `Family: Hiragino Sans` | W3, W6 (W1 for small airy captions) | /System/Library/Fonts/ヒラギノ角ゴシック W3.ttc (W1, W6 same folder) | M PLUS 1 / Noto Sans JP (Latin + kana + kanji) |
| C Sky | Data | `Family: Menlo` | Regular | /System/Library/Fonts/Menlo.ttc | M PLUS 1 Code |
| D Seven | Display | `Family: Gill Sans` | Light, Regular | /System/Library/Fonts/Supplemental/GillSans.ttc | Fira Sans Light / Open Sans Light (humanist display) |
| D Seven | UI | `Family: PT Sans` | Regular, Bold | /System/Library/Fonts/Supplemental/PTSans.ttc | PT Sans / Source Sans 3 (Segoe-era humanist UI) |
| D Seven | Data | `Family: PT Mono` | Regular | /System/Library/Fonts/Supplemental/PTMono.ttc | Fira Mono / Source Code Pro |

Contrast between boards: A geometric + neo-grotesque + coding mono; B squared techno caps + wide hinted UI; C hairline geometric + Japanese gothic; D humanist throughout.
No Eurostile, Segoe UI, Frutiger or Myriad is installed, so B and D lean on the nearest shapes above.

PT Sans / PT Mono: draw with the system copy, but ship the file from the Google Fonts / ParaType source, not one copied out of `/System/Library/Fonts/Supplemental`.

## Shipping candidates per pass

**How these were checked (2026-11-12).**
- Every row was checked against two files in the google/fonts repo, opened for that family:
  - licence: `https://raw.githubusercontent.com/google/fonts/main/<dir>/OFL.txt` (`LICENSE.txt` for Apache)
  - weights and subsets: `.../<dir>/METADATA.pb`
- The `<dir>` column gives the path.
- **Licence:** every row except Syncopate states "licensed under the SIL Open Font License, Version 1.1". Syncopate is Apache License, Version 2.0.
  - OFL allows bundling the font with software, including a commercial game. Don't sell the font file on its own.
  - A modified font can't keep a Reserved Font Name (RFN).
  - Dynamic TMP assets ship the `.ttf` in the build. Subsetting that file counts as modifying it, so a subset font with an RFN (Plex, Source, PT, Audiowide, Orbitron, Krona, Share) must be renamed. Shipping the font unsubset avoids this.
  - Ship each family's `OFL.txt` in the build's credits/licences folder.
- **Latin:** "Lat+ext" means the `latin` and `latin-ext` subsets, which cover Polish, Czech, Turkish and so on. "Lat only" means basic Western Latin, which is a localisation risk.
- **tnum (tabular figures):**
  - "mono" means the face is monospaced, so its digits are tabular by construction.
  - "src" means the `tnum` feature was found in the upstream source by GitHub code search.
  - "doc" means the foundry's page says so.
  - "n/v" means not verified. A search returning 0 hits proves nothing.
  - For a proportional face, "src" or "doc" only shows the feature exists. TMP won't switch it on (see the notes under Recommended trios), so it **does not** make that face clock-safe.
  - Clock and ammo always go in the data mono.
- **Weights:** "var 100–900" means a variable font with a weight axis. TMP bakes one static instance per atlas.

### A · Survey: white paper, hairlines, Deep Navy, legal microtype

| Role | Font | `<dir>` | Why it fits A | Weights | tnum | Latin | Licence |
|---|---|---|---|---|---|---|---|
| **Display** | **Saira (wdth 125, Light / ExtraLight)** | ofl/saira | Rounded-square grotesque at its expanded width: the guide's one large thin wide title and small wide caps from one family. Reworked pick 2026-11-13 (the UI guide asks for thin; Michroma has 400 only) | var wght 100–900, wdth 50–125, italics | n/v | Lat+ext, Vietnamese | SIL OFL 1.1 (no RFN in the B row's check; TMP bakes static instances, so if an instance is exported from the variable file under a new name, ship OFL.txt with it) |
| alt | Michroma | ofl/michroma | Wide, squared Microgramma-style caps: 2001 corporate-futurist signage. Kept for headings if Saira reads too soft | 400 only | n/v | Lat+ext | SIL OFL 1.1 |
| alt | Krona One | ofl/kronaone | Wide, rounder geometric caps; softer annual-report title | 400 only | n/v | Lat+ext | SIL OFL 1.1 (RFN "Krona", "Krona One") |
| alt | Syncopate | apache/syncopate | All-caps extended techno; legal-caption feel at small sizes | 400, 700 | n/v | **Lat only** | Apache License 2.0 |
| **UI** | **Archivo** | ofl/archivo | Grotesque with a width axis: one family covers condensed microtype and wide labels | var wght 100–900, wdth 62–125, italics | n/v | Lat+ext, Vietnamese | SIL OFL 1.1 |
| alt | IBM Plex Sans | ofl/ibmplexsans | Corporate-engineering voice; pairs natively with Plex Mono | var wght 100–700, wdth 75–100, italics | n/v | Lat+ext, Cyrillic, Greek | SIL OFL 1.1 (RFN "Plex") |
| alt | Hanken Grotesk | ofl/hankengrotesk | Thin, editorial neo-grotesque for paper-like body copy | var wght 100–900, italics | n/v | Lat+ext, Cyrillic-ext | SIL OFL 1.1 |
| **Data** | **IBM Plex Mono** | ofl/ibmplexmono | Technical mono with drawing-office character; light weights for oversized numerals | 100–700 static, all with italics | mono | Lat+ext, Cyrillic | SIL OFL 1.1 (RFN "Plex") |
| alt | JetBrains Mono | ofl/jetbrainsmono | Taller x-height, very legible at HUD size; same face as the board stand-in | var wght 100–800, italics | mono | Lat+ext, Cyrillic, Greek | SIL OFL 1.1 |
| alt | DM Mono | ofl/dmmono | Quieter, rounder mono for a softer paper read | 300, 400, 500 with italics | mono | Lat+ext | SIL OFL 1.1 |

Era reference, **not shippable**: Microgramma / Eurostile, Helvetica.

The A trio matches `style_research.md` §4 and art bible §13.
- §4 lists different alternates: Lexend Zetta and Jura for display, Red Hat Text for UI, Share Tech Mono for data. All are still valid.
- Jura and Share Tech Mono were licence-checked here (both OFL 1.1). Share Tech Mono is Basic Latin only.
- Lexend Zetta and Red Hat Text were not checked in this pass.
- §4 says PT Sans / PT Mono licences were "not verified". They are now verified as OFL 1.1, so §4 is out of date.

### B · Aero: see-through architectural glass, techno caps

| Role | Font | `<dir>` | Why it fits B | Weights | tnum | Latin | Licence |
|---|---|---|---|---|---|---|---|
| **Display** | **Audiowide** | ofl/audiowide | Wide, rounded-square techno caps of the early-2000s tech brand | 400 only | n/v | Lat+ext | SIL OFL 1.1 (RFN "Audiowide") |
| alt | Orbitron | ofl/orbitron | The classic geometric "future" display; heavier, more sci-fi | var wght 400–900 | n/v | **Lat only** | SIL OFL 1.1 (RFN "Orbitron") |
| alt | Saira (wdth 125) | ofl/saira | Rounded-square grotesque, stretched to an expanded width: Eurostile-like but quieter | var wght 100–900, wdth 50–125, italics | n/v | Lat+ext, Vietnamese | SIL OFL 1.1 |
| **UI** | **Exo 2** | ofl/exo2 | Wide techno-geometric sans that still reads at body size | var wght 100–900, italics | src | Lat+ext, Cyrillic | SIL OFL 1.1 |
| alt | Chakra Petch | ofl/chakrapetch | Chamfered corners: glass-panel "cut" detail | 300–700 with italics | n/v | Lat+ext, Thai | SIL OFL 1.1 |
| alt | Rajdhani | ofl/rajdhani | Squared, condensed; good for dense tracked caps labels | 300–700 | n/v | Lat+ext, Devanagari | SIL OFL 1.1 |
| **Data** | **Space Mono** | ofl/spacemono | Geometric retro-tech mono with wide, squared digits | 400, 700 with italics | mono | Lat+ext | SIL OFL 1.1 |
| alt | Share Tech Mono | ofl/sharetechmono | Squared techno mono, closest to the B display caps | 400 only | mono | **Lat only** | SIL OFL 1.1 (RFN "Share") |
| alt | B612 Mono | ofl/b612mono | Designed for aircraft cockpit displays; instrument-panel read | 400, 700 with italics | mono | **Lat only** | SIL OFL 1.1 |

Era reference, **not shippable**: Eurostile Extended, Bank Gothic, Tahoma and Verdana (Windows XP), Frutiger.
Orbitron and Share Tech Mono have Basic Latin only, so they were demoted to alternates.
**Risk:** a techno face in all three roles breaks the bible §13 rule "do not force every word into a futuristic font". In B, keep Audiowide for titles only and let Exo 2 run at its lighter weights.

Considered but not picked:
- Jura (ofl/jura, OFL 1.1, var wght 300–700, Lat+ext): too soft for B.
- Space Grotesk (ofl/spacegrotesk, OFL 1.1, var wght 300–700): reads 2020, not 2004.
- Oxanium (ofl/oxanium, OFL 1.1, var wght 200–800, Lat+ext): a usable techno UI swap.

### C · Sky: deep blue air, cel clouds, small precise type

Japanese is read from the `japanese` subset in METADATA.pb. Google Fonts' `japanese` subset carries kana and kanji. Exact kanji counts weren't checked.

| Role | Font | `<dir>` | Why it fits C | Weights | tnum | Latin | Japanese | Licence |
|---|---|---|---|---|---|---|---|---|
| **Display** | **Zen Kaku Gothic New** | ofl/zenkakugothicnew | Clean Japanese gothic with a crisp light weight; title-card feel | 300, 400, 500, 700, 900 | n/v | Lat+ext, Cyrillic | **Yes** (kana + kanji) | SIL OFL 1.1 |
| alt | Sora | ofl/sora | Wide geometric Latin; airy title lettering | var wght 100–800 | n/v | Lat+ext | **None**: pair with Zen Kaku Gothic New or M PLUS 1 | SIL OFL 1.1 |
| alt | M PLUS 1 (Thin/Light) | ofl/mplus1 | Hairline geometric weights for big, thin titles; the same family as the UI face | var wght 100–900 | src | Lat+ext, Vietnamese | **Yes** | SIL OFL 1.1 |
| **UI** | **M PLUS 1** | ofl/mplus1 | Neutral, precise Japanese/Latin sans; one family for both scripts | var wght 100–900 | src | Lat+ext, Vietnamese | **Yes** | SIL OFL 1.1 |
| alt | Noto Sans JP | ofl/notosansjp | Most complete coverage; plainer | var wght 100–900 | n/v | Lat+ext, Cyrillic | **Yes** | SIL OFL 1.1 |
| alt | BIZ UDPGothic | ofl/bizudpgothic | Morisawa universal-design gothic; very legible at small sizes | 400, 700 | n/v | Lat+ext, Cyrillic | **Yes** | SIL OFL 1.1 |
| **Data** | **M PLUS 1 Code** | ofl/mplus1code | Mono companion of the UI face; Latin digits and JP in one asset | var wght 100–700 | mono | Lat+ext | **Yes** | SIL OFL 1.1 |
| alt | BIZ UDGothic | ofl/bizudgothic | Fixed-width Morisawa gothic; half-width Latin, full-width kana | 400, 700 | mono (fixed-pitch) | Lat+ext, Cyrillic | **Yes** | SIL OFL 1.1 |
| alt | JetBrains Mono | ofl/jetbrainsmono | Crisp, light-weight Latin mono for small precise readouts | var wght 100–800, italics | mono | Lat+ext, Cyrillic, Greek | **None**: falls back to M PLUS 1 Code | SIL OFL 1.1 |

Also checked:
- Manrope (ofl/manrope, OFL 1.1, var wght 200–800, Lat+ext, no Japanese) works as a Sora swap.
- Inter (ofl/inter, OFL 1.1, var wght 100–900, no Japanese) has `tnum` per [rsms.me/inter](https://rsms.me/inter/). TMP won't apply that feature, so it isn't a data pick.

BIZ UDGothic "fixed-pitch" comes from Morisawa's naming (the P in UDPGothic means proportional). It's not verified in the font.
Era reference, **not shippable**: Hiragino Kaku Gothic, DIN, Futura.

### D · Seven: softer, glossier 2009 desktop glass, humanist

| Role | Font | `<dir>` | Why it fits D | Weights | tnum | Latin | Licence |
|---|---|---|---|---|---|---|---|
| **Display** | **Open Sans (Light)** | ofl/opensans | Open, friendly humanist; Light reads like a 2009 window title | var wght 300–800, wdth 75–100, italics | src | Lat+ext, Cyrillic, Greek, Hebrew | SIL OFL 1.1 |
| alt | Fira Sans (Light) | ofl/firasans | Humanist with more character; full 100–900 range for big light numerals | 100–900 static, all with italics | n/v | Lat+ext, Cyrillic, Greek | SIL OFL 1.1 |
| alt | Nunito Sans | ofl/nunitosans | Rounder, softer; leans into the glossy-bubble side of the era | var wght 200–1000, wdth 75–125, opsz 6–12 | n/v | Lat+ext, Cyrillic | SIL OFL 1.1 |
| **UI** | **Source Sans 3** | ofl/sourcesans3 | Calm humanist UI sans, closest open match to that era's system UI | var wght 200–900, italics | n/v | Lat+ext, Cyrillic, Greek | SIL OFL 1.1 (RFN "Source") |
| alt | PT Sans | ofl/ptsans | Humanist with warm curves; also the installed board stand-in | 400, 700 with italics | n/v | Lat+ext, Cyrillic | SIL OFL 1.1 (RFN "PT Sans", "ParaType") |
| alt | Noto Sans | ofl/notosans | Neutral humanist; widest script coverage if D wins and localisation grows | var wght 100–900, wdth 62.5–100, italics | n/v | Lat+ext, Cyrillic, Greek, Devanagari | SIL OFL 1.1 |
| **Data** | **Source Code Pro** | ofl/sourcecodepro | Mono sibling of Source Sans; same proportions | var wght 200–900, italics | mono | Lat+ext, Cyrillic, Greek | SIL OFL 1.1 (RFN "Source") |
| alt | Fira Mono | ofl/firamono | Mono sibling of Fira Sans | 400, 500, 700 | mono | Lat+ext, Cyrillic, Greek | SIL OFL 1.1 |
| alt | PT Mono | ofl/ptmono | Mono sibling of PT Sans; same face as the board stand-in | 400 only | mono | Lat+ext, Cyrillic | SIL OFL 1.1 (RFN "PT Sans", "PT Serif", "PT Mono", "ParaType") |

**PT licence:** the Google Fonts `OFL.txt` reads "licensed under the SIL Open Font License, Version 1.1", with the RFNs above. Ship the Google Fonts copy. ParaType's older "PT Free Font License" was not checked and isn't needed.

**Selawik: flagged, left out.**
- `LICENSE.txt` at github.com/microsoft/Selawik is SIL OFL 1.1, with RFN "Selawik".
- The README calls it "an open source replacement for Segoe UI". That's a description, not a licence term, so it isn't restricted to that purpose.
- The repo root holds only `Source files`, no built fonts. Its weights could not be confirmed, so it isn't recommended.

Hind and Mukta (ofl/hind, ofl/mukta; OFL 1.1; 300–700 / 200–800; Lat+ext, Devanagari) were not picked. Their Latin is narrower and less of the era than Open Sans or Source Sans 3.

Era reference, **not shippable**: Segoe UI, Lucida Grande, Myriad, Calibri.

## Recommended trios

| Pass | Display | UI | Data |
|---|---|---|---|
| A · Survey | Saira Expanded (Light / ExtraLight) | Archivo SemiExpanded | IBM Plex Mono |
| B · Aero | Audiowide | Exo 2 | Space Mono |
| C · Sky | Zen Kaku Gothic New | M PLUS 1 | M PLUS 1 Code |
| D · Seven | Open Sans Light | Source Sans 3 | Source Code Pro |

**Mixing and atlas notes:**
- **Clock and ammo go in the data mono, never in a proportional face.**
  - As far as known, TMP doesn't apply OpenType GSUB features such as `tnum`, so only a font's default figures count. Verify this in Unity 6.
  - For digits in a proportional face, use the `<mspace>` tag.
- **One data mono can serve every pass** and keep one atlas.
  - IBM Plex Mono covers Lat+ext and Cyrillic.
  - If Japanese ships, switch to M PLUS 1 Code. It also carries kana and kanji.
- **A Japanese asset can't be a small static atlas.**
  - Use a dynamic TMP font asset for M PLUS 1 / Noto Sans JP. Put it as a fallback behind the Latin asset, or as the main asset in pass C.
  - Budget for a large atlas page, or several.
