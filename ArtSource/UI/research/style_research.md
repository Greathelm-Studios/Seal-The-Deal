# Close the Deal: UI visual language research

**Date:** 2026-11-12. **Status:** research for review, nothing in `Assets/` changed.
**Inputs read:** the art bible (`ArtSource/Kit/concepts/Office/refs/art_bible.txt`, §03, §13, §14, §19, §20, §22), bible plates 01–04, Alex's look references (sky_and_clouds 01–06, light_and_grade 01–05, with 03 and 04 marked as liked), the in-engine captures in `ArtSource/Look/review/`, `docs/GDD.md`, `docs/systems/MENU_AND_HUD.md`, `docs/systems/TOWER.md`, and `ProjectSettings/ProjectSettings.asset` (colour space).

This file is written so a mockup agent can draw from it without asking. Every colour pair has a computed WCAG 2.x contrast ratio. Every font is marked either **installed** (checked by command) or **target, not installed**.

---

## 1. Where the owner's keywords and the bible disagree

The owner's keywords are "2000s corporate futurism, aero, Gen X Soft Club, glassmorphism". Two of them are fully inside the bible: corporate futurism and Gen X Soft Club are both locked pillars (§01, §02). The other two collide with it. "Aero" in common use means Frutiger Aero, and the bible names Frutiger Aero twice as a look to avoid: "Frutiger Aero greenery/bubbles" in §01, and "Frutiger Aero as a primary look" in §23. It also bans "bubbly aero shapes" (§09) and candy translucency (§03). The case against aero is about era and palette, not taste. CARI dates Frutiger Aero to about 2005–2013, the Vista and Windows 7 era. Its traits are glossy skeuomorphic surfaces, humanist sans type (Segoe, Frutiger) and nature photography such as grass, aurora and bokeh. That is a different era from the bible's 1999–2003 target. Glassmorphism is the 2020s revival of that same glossy look.

**I would follow the bible.** It is the approved, locked document, and the period evidence agrees with it. The owner still gets what the keywords are really asking for, which is glass, light and air. Glass is allowed as architecture: cold, flat, frosted cobalt or smoked-navy panes with a hairline bevel and a single straight sheen streak, the same glass as the curtain wall the players fight through. The following stay out: bubbles, orbs, pill shapes, greenery, water droplets, rainbow or aurora gradients, glossy capsule buttons, drop-shadow glow, and anything rounded past a small radius.

There is a smaller mismatch inside the bible's own term. CARI lists natural, muted tones for Gen X Soft Club (greens, beiges, tans, greys and black), natural leathers and Helvetica-style wayfinding type. A design-trend summary of the same aesthetic adds desaturated blue and seafoam. The bible uses the term in its narrower reference-board sense: blue-white sky, thin lines, air, and editorial restraint. **The bible's narrower reading is the one to use**, because its plates (02, 03) show exactly that. The useful parts of CARI's definition to carry over are the wayfinding typography and the balance of sterile and organic, with sky and cloud standing in for the organic half.

---

## 2. What the HUD actually sits over

Measured from the five captures in `ArtSource/Look/review/`. Each was downscaled to a 16×9 block average, so local highlights are brighter than the figures below.

| Capture | Darkest block | Median | 90th percentile | Brightest block |
|---|---|---|---|---|
| 01_sun_patches | #1F3661 | #82A0C9 | #A3C3E7 | #B3D0F0 |
| 02_upper_floor | #2D4773 | #95B1D4 | #AECBED | #C5DEF5 |
| 03_outside | #0A40D0 (sky) | #A8C4E7 | #B2CEEF | #D0E5FB |
| 04_no_rival | #8E2D3D (a red element, likely the locked stair door: TOWER.md says the door is red) | #85A2CE | #A9C7EA | #BDD3ED |
| 05_cloud_sea_from_inside | #8E2D3D | #8AA5C5 | #ACCAEC | #BAD5F9 |

The frame is mid-to-light cool blue almost everywhere. Sun patches and bloom push local pixels to **#FFFFFF**, and the sky dips to saturated cobalt (#0A40D0). The glass recipes below are tested against five backgrounds: #FFFFFF (bloom, worst case for light text), #D0E5FB (lit wall), #82A0C9 (median), #0A40D0 (sky), and #101116 (a navy suit filling the panel, worst case for dark text).

The project renders in **Linear colour space** (`m_ActiveColorSpace: 1`). Unity therefore blends UI alpha in linear light, which makes a dark pane over white look lighter than the same alpha does in an SVG or browser, where blending happens in sRGB. All alphas below are sized to pass in Unity's linear blend. Section 6 gives the SVG equivalents.

---

## 3. References: take / don't take

Notes on game menus are at the level of visual grammar. Game UI Database (gameuidatabase.com) and the uxdesign.cc Ridge Racer Type 4 breakdown both refused automated fetches (HTTP 403), so the game notes come from my knowledge of the titles rather than from page captures. Check each against real footage before using it as more than direction. Nothing here proposes copying any screen.

| Reference | Take | Don't take |
|---|---|---|
| **Bible plate 01** (an SSX 3 rider poster; the SSX3 logo and the rider "Moby Jones" are visible) | Thin full-height hairlines that cross the image and split it into a loose grid; one oversized light lowercase name; a dense block of tiny stat copy; one cobalt square block; one lens flare | The flare rings scattered across the frame, the slang copy, the pink cast in the flare |
| **Bible plate 02** (techno-minimal composition) | A pale square grid under everything; translucent blue shapes overlapping a sky photo; a word boxed by a hairline rectangle; a sparse lowercase phrase | The rounded blob forms and the four-petal ornaments, which are bubbly aero; and the coloured-letter accents |
| **Bible plate 03** (white field, sky inserts, black linework) | Rectangular windows of sky cut into a white field; a black square bullet before a bold title; a column of open hairline rectangles used as ticks; a hairline rule between text blocks | The swept "metalheart" 3D shapes as UI chrome, and the forum-post copy |
| **Bible plate 04** (white-blue city) | Large cobalt rectangles on white masses; a thin cyan stripe; tiny type on huge surfaces | Photoreal density and billboard clutter behind HUD text |
| **Alex ref 03** clouds traced by squares over a grid (liked) | **Overlapping open square outlines that trace an edge**, and a perspective hairline grid. This becomes the focus-bracket and lock-on motif, and the same thing the sky shader already does on the clouds | Covering whole objects with squares, which is noise; tracing must stay sparse in the HUD |
| **Alex ref 04** thresholded flat cloud cel (liked) | Shapes reduced to two or three flat tones with stepped, pixel-quantised edges. Use it for panel reveal masks and transition wipes | The VHS tint, the signature, and the photo grain; the bible §17 bans permanent VHS noise |
| **CARI: Gen X Soft Club** | Wayfinding-style neo-grotesque type, sterile geometry set against an organic element (sky), muted cool washes | CARI's greens, tans and leather, which are outside the bible's palette |
| **CARI: Y2K aesthetic** (Evan Collins' term) | Optimism and the "future as a product" attitude | Chrome, blobby electronics, silver shine, pink |
| **CARI: Frutiger Aero** (about 2005–2013) | Translucent layered panes and a crisp top highlight, limited to flat panels | Gloss, bubbles, grass, aurora, bokeh, humanist Segoe/Frutiger type, glossy pill buttons |
| **The Designers Republic / Wipeout 3** (1999) | A strict system of icons, colour and custom type used across menus and world signage, so brand and UI are one identity; numbers as graphic objects; confident negative space | The anti-corporate irony, the Japanese-katakana decoration and the acid palette |
| **Rez** (2001) | The wireframe and hairline as the whole aesthetic; a lock-on that marks targets one by one, which supports the bracket-square tracing | Neon on black, and the psychedelic pulse |
| **SSX Tricky** (2001) / **SSX 3** (2003) | Editorial character cards with the stat microtype of plate 01; big type crops; fast, confident menu slides | The loud DJ-presenter energy and the graffiti stickers |
| **Ridge Racer Type 4** (1998) / **Ridge Racer V** (2000) | Very restrained menus with sparse text, wide spacing, sliding alignment and small precise type over large calm fields: the "expensive" feel | The motorsport sponsor logos, and Reiko-style mascot presentation |
| **Gran Turismo 3** (2001) / **GT4** (2004) | Clean white and silver panels with a hairline frame and a cool-blue accent: a corporate showroom tone | Dense tabular menus and the car-catalogue skeuomorphism |
| **Mirror's Edge** (2008) | Almost no HUD; a single reserved accent colour carries all guidance against a white world. This is the model for the one reserved warning colour | Its specific red-guidance system, and the HUD-less extreme. A team shooter needs timer, rival progress and ammo |
| **Persona 3 / 5** (philosophy only, bible §14) | Every menu, transition and label belongs to one identity, and menus act as set-pieces | The tilted collage, the comic energy, and the red/black or blue water palettes |
| **Windows Vista / 7 Aero glass** | A frosted pane with a single highlight edge as a material idea, and a 1 px inner highlight | The glossy title bars, glow buttons, rounded window chrome and Segoe UI |
| **2001 corporate annual reports and intranets** | Clause and section numbering, form fields with labels on the left, hairline tables, small caps-height microtype, cobalt section bands, document-style reveals | Clip art, bevelled web buttons, Verdana body text, tiled backgrounds |
| **PS2 system browser** (2000) | A calm void with a slow ambient drift behind a thin text menu: patience and air | The floating crystal towers and particle orbs |
| **Modern: The Finals** (2023) | The closest modern shooter to this tone: a team shooter framed as a corporate-sponsored show. It shows that a clean, bright, branded UI can carry a fast FPS | Its game-show neon and sponsor clutter. It is the competitor this UI will be compared with on Steam, so do not echo it |
| **Modern: Bomb Rush Cyberfunk** (2023), **Neon White** (2022), **Wipeout Omega Collection** (2017) | Proof that an early-2000s graphic identity reads as deliberate rather than retro when type and motion are precise | BRC's graffiti and Neon White's anime glass gloss |

---

## 4. Type system

The bible §22 leaves the final family TBD and §13 asks for geometric/techno display, a mono or technical data face, and a thin editorial face. Three roles below, all open-licence so they can ship as TextMeshPro SDF assets. TextMeshPro does not use variable-font axes, so import static instances.

| Role | Target face (licence) | Why | Alternates (licence) | Closest **installed** stand-in for mockups |
|---|---|---|---|---|
| **Display**: title, screen titles, clause headings | **Michroma** (OFL), target, not installed | A squared, extended Eurostile/Microgramma-type face: the 2001 corporate-tech signature, sharp and serious, and it reads as a brand rather than a sci-fi effect | **Lexend Zetta** (OFL), wide geometric with more weights; **Jura** (OFL), a light techno face with the soft-club thinness | **Futura Medium**, with caps tracked +6%. It is rounder than Michroma, so the mockup will read softer than the target. Second choice: **DIN Alternate Bold** for squared numerals |
| **UI**: buttons, body, settings, HUD labels | **Archivo** (OFL), target, not installed. Use the static Regular, Medium and SemiBold, plus **Archivo Expanded** for large labels and **Archivo Expanded ExtraLight** for oversized floor numerals and editorial lines | A wayfinding-style grotesque (the CARI soft-club type root) with real width variants, so one family covers dense UI, wide headers and thin editorial copy | **IBM Plex Sans** (OFL), corporate heritage and legal-document neutrality; **Red Hat Text** (OFL), very legible at small HUD sizes | **Helvetica Neue** (Medium, Bold, Condensed Bold). Use **Helvetica Neue UltraLight** for oversized numerals and **Avenir Next Ultra Light** as a second choice |
| **Data**: timers, floor codes, stats, legal microtype, room codes | **IBM Plex Mono** (OFL), target, not installed | The bible asks for mono or technical sans for "legal-looking system text". Plex Mono looks like corporate paperwork rather than a code editor | **JetBrains Mono** (OFL, **installed**), so it is shippable and drawable now; **Share Tech Mono** (OFL), more overtly techno | **JetBrains Mono Regular / Medium** (installed, and OFL, so the mockup face can also ship) |

If the Archivo release has no static Expanded or ExtraLight files, generate those instances from the variable font before importing to TextMeshPro, which cannot use variable axes.

Mockup-only faces: Futura, Helvetica Neue, Avenir Next and DIN are macOS system fonts. They are **not shippable**, so use them only in mockups. PT Sans / PT Mono are SIL OFL 1.1 (checked in [fonts_by_pass.md](fonts_by_pass.md)); ship the Google Fonts copy, never the macOS system file.

**Install check.** The command was run on this Mac on 2026-11-12. `system_profiler SPFontsDataType | grep "Full Name:"` returned, among others:

```
Full Name: Futura Medium
Full Name: DIN Alternate Bold
Full Name: Helvetica Neue Medium
Full Name: Helvetica Neue Bold
Full Name: Helvetica Neue Condensed Bold
Full Name: Helvetica Neue UltraLight
Full Name: Avenir Next Ultra Light
Full Name: JetBrains Mono Regular
Full Name: JetBrains Mono Medium
```

`ls ~/Library/Fonts` lists only JetBrains Mono files; `/Library/Fonts` holds only Arial Unicode. Michroma, Lexend Zetta, Jura, Archivo, IBM Plex Sans/Mono, Red Hat Text and Share Tech Mono are **not installed**. Nothing was downloaded.

**Scale.** Reference canvas 1920×1080, TMP point sizes:

| Token | Size / weight | Face | Use |
|---|---|---|---|
| `type.micro` | 11 / Regular, tracking +4% | Data | Legal microtype and serials. Decorative only: never the only carrier of needed information |
| `type.label` | 14 / Medium | UI | Field labels, HUD captions |
| `type.body` | 16 / Regular | UI | Menu body and descriptions. This is the minimum for anything a player must read |
| `type.item` | 20 / Medium | UI | Menu items, buttons |
| `type.hud-num` | 28 / Medium | Data | Ammo, timer |
| `type.heading` | 40 / Regular, caps, +6% | Display | Screen titles |
| `type.title` | 96–120 / Regular, caps, +4% | Display | CLOSE THE DEAL |
| `type.floor` | 160–320 / ExtraLight | UI Expanded | Oversized floor numerals |

Case rule: sentence case everywhere except the game title, screen titles and floor codes. No tracked all-caps eyebrow labels. Subtitle "Terms and Conditions Apply." is set in `type.micro` or `type.label`, as fine print beside or under the title (bible §19).

---

## 5. Colour tokens

### 5.1 Base swatches (bible §03, unchanged)

| Token | Hex | Role |
|---|---|---|
| `paper` | #F3F4F4 | Main UI field, the largest area |
| `silver` | #C9CED3 | Secondary panels, quiet separators |
| `navy` | #111B38 | Main text, smoked glass, keylines |
| `cobalt` | #244FB9 | Brand blocks, primary buttons |
| `electric` | #2879EF | Active accent: bars, underlines, selection, large text only |
| `cyan` | #20D4E6 | Tiny luminous marks on dark only: squares, status, focus on dark |
| `black` | #101116 | Maximum contrast, rare |

### 5.2 Derived tokens

| Token | Value | Role |
|---|---|---|
| `ink-2` | #4A5468 | Secondary text on light |
| `ink-3` | #5B6576 | Microtype on light, the lightest text allowed |
| `hover-cobalt` | #1D46A8 | Button hover fill |
| `pressed` | #111B38 (navy) | Button pressed fill |
| `disabled-light` | #8C95A1 | Disabled text on light |
| `disabled-dark` | white at 40% | Disabled text on smoked glass |
| `hairline-light` | navy at 24% (≈ #BDC0C7 on paper) | Rules and grid on light (decorative) |
| `hairline-light-strong` | navy at 40% (≈ #999DA9) | Form field borders (decorative frame) |
| `hairline-dark` | white at 28% | Rules on smoked glass (decorative) |
| `bevel-hi` | white at 50%, 1 px | Inner top highlight on glass |
| `glass-smoked` | navy at 92% | HUD chips, in-match overlays |
| `glass-frosted` | paper at 86% + blur | Menu panes over the live scene |
| `glass-cobalt` | cobalt at 96% | Primary button, active tab |

### 5.3 Text and background pairs (computed, WCAG 2.x relative luminance)

Pass marks: **body text needs 4.5:1**; large text (≥ 24 px regular or ≥ 18.7 px bold) needs 3:1; non-text UI (focus rings, state marks) needs 3:1.

| Text | Background | Ratio | Verdict |
|---|---|---|---|
| navy #111B38 | paper #F3F4F4 | **15.40** | Pass, AAA |
| black #101116 | paper | 17.11 | Pass, AAA |
| ink-2 #4A5468 | paper | 6.91 | Pass |
| ink-3 #5B6576 | paper | 5.34 | Pass |
| cobalt #244FB9 | paper | 6.59 | Pass |
| electric #2879EF | paper | 3.76 | **Fails body.** Large text only |
| cyan #20D4E6 | paper | 1.64 | **Fails.** Never text on light |
| silver #C9CED3 | paper | 1.44 | **Fails.** Never text |
| disabled-light #8C95A1 | paper | 2.75 | **Fails, by design.** WCAG exempts disabled controls. Pair it with a non-focusable state and a struck clause number so state is not carried by colour alone |
| navy | silver #C9CED3 | 10.71 | Pass |
| cobalt | silver | 4.58 | Pass (barely) |
| ink-2 | silver | 4.80 | Pass |
| white #FFFFFF | navy | 16.97 | Pass, AAA |
| cyan | navy | 9.39 | Pass |
| silver | navy | 10.71 | Pass |
| electric | navy | 4.10 | **Fails body.** Large only |
| white | cobalt | 7.26 | Pass |
| paper | cobalt | 6.59 | Pass |
| cyan | cobalt | 4.02 | **Fails body.** Large only |
| white | electric | 4.14 | **Fails body.** Large only. Never put button labels on electric |
| navy | electric | 4.10 | **Fails body.** Large only |
| navy | cyan | 9.39 | Pass |
| white | hover-cobalt #1D46A8 | 8.42 | Pass |
| white | pressed (navy) | 16.97 | Pass |
| white at 40% (≈ #B3B4B6 in Unity) | glass-smoked worst case over bloom, #525560 | 3.58 | **Fails body, by design** (disabled) |

Glass pairs, with worst-case backgrounds, are in §6.3.

Non-text indicators:

| Mark | Against | Ratio | Verdict |
|---|---|---|---|
| Focus ring, navy 2 px | paper | 15.40 | Pass. Use this on light surfaces |
| Focus ring, cobalt 2 px | paper | 6.59 | Pass. Alternate on light |
| Focus ring, cyan 2 px | paper | 1.64 | **Fails.** Cyan focus only on dark |
| Focus ring, cyan | navy | 9.39 | Pass. Use this on dark and smoked surfaces |
| Focus ring, navy | cobalt button | 2.34 | **Fails.** On a cobalt button use an offset ring: 2 px gap of paper, then a 2 px navy ring |
| hairline-light | paper | 1.65 | Decorative, not an indicator: exempt |
| hairline-dark | smoked, worst #525560 | 2.81 | Decorative: exempt |
| W1 orange mark | smoked, worst #525560 | 2.38 | **Fails.** Warning marks in the HUD sit on a solid navy chip (5.44) |
| hairline-light-strong (form field box) | paper | 2.46 | Decorative frame. The label carries the meaning; a focused field gets the navy 2 px ring |

### 5.4 Reserved warning colour: two candidates

Use it for low health, a door being breached, the rival reaching a checkpoint, the last 60 s of the 15:00 cap, and the warning crosshair state. Nothing else uses it.

| Candidate | Hex | Text on it | It as text on | Against scene | Notes |
|---|---|---|---|---|---|
| **W1 Safety Orange** (recommended) | #FF5A1F | navy 5.44, black 6.05, white 3.12 (fails) | navy 5.44 | white 3.12 (passes non-text), sky #0A40D0 2.53 (fails), cobalt 2.33 (fails) | The only warm colour in the system, so it reads as an alarm at a glance; it echoes office safety signage and the fire extinguisher. It is clearly distinct from the crimson stair door (#8E2D3D in the captures). Over sky it needs a 1 px navy keyline or a solid navy chip |
| **W2 Redline** | #C8231B | white 5.66, navy 3.00 (fails body), black 3.33 (fails body) | paper 5.13, white 5.66 | white 5.66, sky 1.40 (fails), cobalt 1.28 (fails), navy 3.00 | A legal pun played straight (a "redline" is a marked-up contract), and it works as text on paper. But it is close in hue to the locked red stair door, so "warning" and "door" would blur, and it vanishes against sky and cobalt unless keylined white |

The greybox menu currently uses amber for the primary action (`docs/systems/MENU_AND_HUD.md`, "Look"). Once a warning colour is reserved, **primary actions move to cobalt** and amber leaves the UI.

### 5.5 Team colours: two pairs

The bible §22 leaves team differentiation TBD.

| Pair | Firm A | Firm B | Separation | Use with | Notes |
|---|---|---|---|---|---|
| **T1 In-palette, value-split** (recommended) | Cobalt #244FB9 | Cyan #20D4E6 | 4.02:1 between them, so they differ by lightness and still separate under every colour-vision deficiency. Add shape coding: Firm A marks are filled squares, Firm B marks are open squares | Either warning | Inside the bible palette. Cobalt is invisible on smoked glass (1.02:1 over bloom) and cyan is weak on white (1.81:1), so team marks sit on their own keyline: cobalt marks get a 1 px white keyline (7.26:1), cyan marks a 1 px navy keyline (9.39:1) |
| **T2 Hue-split** | Electric #2879EF | Amber #FFB000 | Blue against yellow-orange, the classic colour-blind-safe pair (2.26:1 lightness difference) | **W2 Redline only.** Amber and Safety Orange are too close | Strongest at-a-glance read across the street, but it adds a second warm colour, so the bible's "rationed accent" rule is strained. Amber on white is 1.83:1 and needs a navy keyline |

Team identity should be absolute (Firm A is always cobalt), not relative (yours is always blue). Each team owns a building, so the colour can live on the tower's facade graphic too, which keeps the bible's "UI and world are one brand system" rule.

---

## 6. Glass recipe

There are three materials, and each has one job. Glass is flat: no curvature, no refraction distortion, and a corner radius of 0 or 2 px.

### 6.1 Layer stack (top to bottom of the paint order: background first)

| Layer | Smoked (HUD, in-match overlays) | Frosted (menus over the live scene) | Cobalt (primary button, active tab) |
|---|---|---|---|
| 1. Backdrop blur | None in the HUD; 16 px if the URP blur feature exists (see 6.4) | 24 px Gaussian (σ ≈ 8) when available; the panel must also pass without it | None |
| 2. Fill | navy #111B38 at **92%** | paper #F3F4F4 at **86%** | cobalt #244FB9 at **96%** |
| 3. Sheen | Linear gradient, top to bottom: white 6% at 0% → white 0% at 30% of panel height. Only white primary text may sit inside the top 30% (5.39 worst case). Silver, cyan and microtype never sit there | One straight diagonal streak, white 0% → 35% → 0%, 18° from vertical, 24 px wide, at the left third. Decorative; it only lightens a light panel, so it can never hurt navy text | Top 50%: white 3% → 0% |
| 4. Inner highlight | 1 px line at the top inner edge, white at 50% | 1 px top inner edge, white at 100% | 1 px top inner edge, white at 35% |
| 5. Border | 1 px hairline, white at 28% | 1 px hairline, navy at 24% (**required**: over white bloom this is the only edge of the pane) | None |
| 6. Shadow | None (it sits over a busy scene) | 0 x, 8 px y, 24 px blur, navy at 12%. Only on menus, never in the HUD | 0 x, 2 px y, 0 blur, navy at 30% (a hard step, like a printed offset) |
| Corner | 0 | 0 | 0 |
| Text | white, silver; cyan for large numerals only | navy, ink-2, cobalt | white |

### 6.2 SVG now

The SVG blends in sRGB, so the same alpha comes out darker over light backgrounds than Unity's linear blend does. Draw the mockup at the Unity alpha, which is conservative, and add one worst-case frame that uses the Unity composite hexes from 6.3 as opaque fills so the review sees the shipped result.

```svg
<filter id="frost"><feGaussianBlur stdDeviation="8"/></filter>
<linearGradient id="sheen-smoked" x1="0" y1="0" x2="0" y2="1">
  <stop offset="0" stop-color="#FFFFFF" stop-opacity="0.06"/>
  <stop offset="0.3" stop-color="#FFFFFF" stop-opacity="0"/>
</linearGradient>
<!-- smoked chip -->
<rect width="240" height="64" fill="#111B38" fill-opacity="0.92"/>
<rect width="240" height="64" fill="url(#sheen-smoked)"/>
<line x1="0" y1="0.5" x2="240" y2="0.5" stroke="#FFFFFF" stroke-opacity="0.5"/>
<rect x="0.5" y="0.5" width="239" height="63" fill="none" stroke="#FFFFFF" stroke-opacity="0.28"/>
```

For frosted panes, draw a clipped copy of the background image with `filter="url(#frost)"` under the fill.

### 6.3 Worst-case check: what the text gets in Unity (linear blend)

The composite is the panel colour the player actually sees. The ratio is for the panel's main text unless noted.

| Panel | Over #FFFFFF bloom | Over #D0E5FB wall | Over #82A0C9 median | Over #0A40D0 sky | Over #101116 suit |
|---|---|---|---|---|---|
| Smoked 92%, white text | **#525560, 7.43** | #434C5F, 8.61 | #2A3751, 11.91 | #112053, 15.49 | #111A36, 17.14 |
| Smoked 92% + 6% sheen band, white text (the only text allowed in the band) | #686A73, 5.39 | #5E6472, 5.93 | #515768, 7.21 | #484D69, 8.26 | #484B57, 8.68 |
| Smoked 92%, silver #C9CED3 text | **4.69** | 5.44 | 7.51 | 9.78 | 10.82 |
| Smoked 92%, cyan text | **4.11, fails body**: large numerals only | 4.77 | 6.59 | 8.57 | 9.49 |
| Smoked 92% + 6% sheen, silver text | **3.40, fails**: this is why no secondary text sits under the sheen | 3.74 | 4.55 | 5.22 | 5.48 |
| Frosted 86%, navy text (no blur) | #F5F6F6, 15.67 | #EFF2F5, 15.10 | #E7EBEF, 14.16 | #E3E5EF, 13.51 | **#E3E4E4, 13.32** |
| Frosted 86%, ink-2 #4A5468 | 7.03 | 6.77 | 6.35 | 6.06 | **5.97** |
| Frosted 86%, cobalt text | 6.70 | 6.46 | 6.06 | 5.78 | **5.70** |
| Cobalt 96%, white text | **#435FBC, 5.83** | 6.11 | 6.80 | 7.32 | 7.40 |
| Cobalt 96% + 3% sheen, white text | **5.09** | 5.33 | 5.76 | 6.12 | 6.18 |

How it looks over pure #FFFFFF bloom, the failure case:
- The smoked chip turns a flat slate grey (#525560) and reads as a solid tile, not glass. White text still holds 7.43:1.
- The frosted pane becomes almost the same colour as the bloom (#F5F6F6 on #FFFFFF). Only the **navy 24% hairline border and the 12% navy shadow** show where the pane is. Navy text still holds 15.67:1. This is why that border is mandatory.
- The cobalt button lightens to #435FBC. White text holds 5.83:1.

Why the alphas are this high: at the 78% smoked fill an sRGB mockup suggests, Unity's linear blend over white gives only **3.78:1** for white text. That fails. Blur adds nothing in the bloom case, because blurred white is still white, so the fill alpha alone has to carry the contrast.

SVG equivalents, if a mockup wants to match Unity's look over white exactly:
- smoked 92% linear ≈ **75%** sRGB over white (and ≈ 84% over sky)
- cobalt 96% linear ≈ **91%** sRGB over white
- frosted 86% linear ≈ **80%** sRGB over white

Mockups should use the conservative Unity alphas anyway.

### 6.4 Unity uGUI translation

- **Canvas:** put the HUD and menus on a **Screen Space – Overlay** canvas. URP draws Overlay canvases after post-processing, so the look's strong bloom and colour filter never wash the UI. A Screen Space – Camera canvas on the main camera would be bloomed and tinted, which breaks every number above.
- **Fill, sheen, highlight, border:** one 9-sliced sprite per material, with the sheen gradient, the 1 px highlight and the hairline baked into the sprite, tinted by `Image.color` alpha. Cost: one draw, no shader work.
- **Blur:** uGUI has no backdrop blur. The fallback is **no blur at all**: same fill, same alpha, and the panel still passes, because the alphas above were sized without blur. Optional upgrade for menus only: a URP Renderer Feature that copies the camera colour after post-processing, downsamples it to ¼ resolution, runs 2–3 dual-Kawase passes and publishes it as a global texture. The frosted panel's material then samples that texture in screen UV under its fill. It is one extra pass per frame and runs only while a menu is open. Never use it in the HUD.
- **Shadow:** a separate soft-edged sprite behind the frosted pane. Do not use the `Shadow` component (it duplicates the mesh, and hard-edged offsets are only right for cobalt buttons).

---

## 7. Motif kit

| Motif | Source | Use when | Don't use when |
|---|---|---|---|
| **Hairline grid** (square, 32 px at 1080p, navy 24% on light, white 12% on dark; perspective version for the main menu backdrop) | Bible §13, plate 02, Alex ref 03 | Full-screen menus, results, the lobby backdrop; alignment rules under form rows | The in-match HUD, which has its noise budget (§14) |
| **Focus-bracket squares**: open 1 px squares that trace an edge. Overlap them 3–7 times with ±2 px jitter, then snap them into one clean bracket on lock | Alex ref 03 (liked); the sky shader already uses them on the clouds | Menu focus (the cluster snaps onto the selected item), the crosshair, marking a teammate across the gap, a checkpoint reveal tracing the rival tower | Decoration on idle screens; more than one traced object at a time in the HUD |
| **Small cyan squares** (4–6 px, solid) | Bible §13 | Status lights on dark: ready, connected, the selected setting tick, the live floor marker | On light surfaces (1.64:1); as bullets in lists |
| **Cobalt blocks**: large confident rectangles crossing a panel edge | Bible §03, §09, plate 04 | One per screen: the title band, the active tab, the results winner band | Several small blocks; behind HUD numerals |
| **Oversized floor numerals** (`type.floor`, ExtraLight Expanded, silver on paper or white at 18% on dark) | Bible §13, plate 01 ("moby") | Checkpoint banners, floor transitions, the results "Floor 26", the lobby | Inside the HUD at rest (the floor readout there is `type.hud-num`) |
| **Legal microtype**: clause refs, serials, a one-line disclaimer | Bible §13, §19 | Panel corners, under the title, the footer of settings ("Settings take effect on save. Terms and Conditions Apply.") | Anything a player must read to act; more than one string per panel |
| **Crop marks** (8 px L-corners, 1 px) | Bible §13 | Framing the main menu pane and character/results cards, like a print proof | Every panel; the HUD |
| **Clause numbering** (§ 1, § 1.1) | Bible §14 (forms, contract clauses) | Menu items that are real sections of one document (settings groups, match rules) | Lists that are not a document; never as 01 / 02 / 03 decoration |
| **Thresholded reveal mask**: a 2–3 tone stepped-edge wipe | Alex ref 04 (liked) | Screen-to-screen transitions, the checkpoint reveal, the results reveal | Button-level motion (too slow and noisy) |
| **Sky window**: a rectangle cut into the paper field showing the live sky or tower | Plate 03 | The main menu and results only | Settings (the scene behind distracts from reading) |
| **Lens flare** (one soft disc and a thin streak) | Plate 01, bible §19 "rarely and intentionally" | **Once**: the win moment on the results screen | Anywhere else, ever |

---

## 8. Motion language

Precise, not bouncy (bible §14): no springs and no overshoot, so the project needs no spring notation. All user-initiated motion finishes **under 300 ms**. Exits run at about 70% of the entry duration with ease-in. Menus run on unscaled time. Reduced-motion setting: replace every translate with an opacity change of the same duration, and drop the bracket jitter.

| Motion | Trigger | Duration | Easing (cubic-bezier) | Detail | Sound pairing |
|---|---|---|---|---|---|
| Hairline draw ("snapping line") | Panel opens | 160 ms | ease-out (0.2, 0, 0, 1) | The line grows from its anchor end; rules stagger by 16 ms | Soft tick |
| Panel slide ("sliding alignment") | Menu open, t = 0 | 200 ms | ease-out-expo (0.16, 1, 0.3, 1) | 24 px along the panel's alignment axis, plus opacity 0→1 | Paper slide |
| Document reveal | Menu open, starts at t = 40 ms, alongside the slide | ≤ 6 lines, 16 ms stagger, 160 ms each (finishes by 280 ms) | ease-out (0.2, 0, 0, 1) | Lines unmask top to bottom like a printer feed, with no fade-up bounce. The whole open sequence (slide, hairline draw, reveal) runs concurrently and ends **≤ 300 ms** after the input | Printer chatter, muted |
| Focus move | Arrow or stick | 140 ms | ease-in-out (0.65, 0, 0.35, 1) | The bracket cluster travels and snaps to the new item; jitter settles in the last 40 ms | Keyboard tick |
| Hover | Mouse over | 100 ms | ease-out | Bracket corners close in from 6 px to 0; fill goes cobalt → hover-cobalt | None (avoids noise when sweeping the mouse) |
| Press | Click or confirm | 60 ms down, 120 ms up | linear down, ease-out up | Fill to navy; a 2 px offset shadow collapses to 0, like a stamp | Stamp / stapler snap |
| Form field commit | Value changed | 120 ms | ease-out | The value slides 8 px from right; the cyan square ticks on | Click |
| Floor counter | Floor changes | 220 ms | ease-out (0.2, 0, 0, 1) | Odometer roll, one digit at a time | Lift chime, short |
| Hit marker | Hit confirmed | 0 ms in, 80 ms hold, 120 ms out | ease-in out | The crosshair brackets snap inward 3 px | Hit tick |
| Warning flash | Warning event | 50 ms in, 250 ms out | ease-out | One flash, no loops. Low health holds a steady state; it never strobes (stays under 3 Hz) | Short alert tone |
| Screen transition (system, not user) | Match start, checkpoint reveal, results | 400–600 ms | ease-in-out (0.65, 0, 0.35, 1) | Thresholded mask wipe; the only motion allowed over 300 ms | Whoosh plus lift |

---

## 9. Three directions

### A. Paper & Hairline (bible-strict)

**Thesis:** the UI is the firm's own documentation: a white contract page laid over the world. Paper surfaces are opaque, navy type sits on a hairline grid, one cobalt block per screen, and glass appears only where the game must show through (the HUD chips). It is the purest reading of §13–§14 and the safest for legibility.

- **Main menu:** a full-bleed paper field with crop marks. A large rectangle is cut out of it to show the live sky and towers (plate 03), the title CLOSE THE DEAL sits in navy Michroma above it, and "Terms and Conditions Apply." sits in microtype beside it. Items are plain sentence-case lines (Host game, Join a friend, Settings, Quit), unnumbered, because the main menu is not a document. Focus is the bracket cluster.
- **Settings:** a form. Labels left in ink-2, values right in navy, a hairline rule per row, the group headings as clauses. Toggles are 12 px squares, filled cobalt when on. Footer microtype reads "Settings take effect on save."
- **HUD:** smoked chips only. Timer top-centre, a two-tower floor gauge under it, ammo bottom-right, item slot bottom-left. Nothing else at rest.
- **Crosshair:** a 2 px white centre dot with a 1 px navy halo, inside four 6 px bracket corners in white with a navy halo. Ratios: white core against navy halo 16.97, navy halo against white bloom 16.97, white core against sky #0A40D0 7.9, so it reads on every background in §2.
- **Risk:** this delivers the least of the owner's "aero/glassmorphism", and an opaque white menu can read as a web form. Without the motion and sound pass it is the most likely to look like a demo.

### B. Curtain Wall (keywords pushed as far as the bible allows)

**Thesis:** the menus are the building. Large frosted panes, divided by mullion hairlines, slide on tracks in front of the live 3D scene while the camera drifts slowly along the facade. Each pane has a straight sheen streak and a hairline bevel, like a curtain-wall unit. It is the most "aero" the bible permits: architectural glass with no bubbles.

- **Main menu:** a slow camera move up the rival tower. Three tall frosted panes stand like a facade bay, with the title etched in navy on the left pane, menu items on the centre pane and the lobby roster on the right. Panes slide sideways on selection.
- **Settings:** one wide frosted pane with mullion-divided columns (Controls, Video, Audio), each column a form.
- **HUD:** smoked glass chips, with the sheen band on the title row only; the floor gauge as a narrow vertical glass slab at the right edge.
- **Crosshair:** same as A.
- **Risks:** it needs the blur Renderer Feature to read as glass at all. Without it the panes are flat, translucent white tiles. Over a bloomed sky the frosted pane disappears except for its hairline border. A live 3D menu scene costs GPU while menus are open. It is also the direction most likely to drift into Vista gloss in later polish passes.

### C. Survey (my third option, built on Alex's two liked references)

**Thesis:** the UI is the firm's analysis software surveying the rival tower. A perspective hairline grid lies over the world, and focus-bracket squares trace, lock and track whatever matters. Transitions are thresholded flat-cel wipes. The UI uses the same marks the sky shader already draws on the clouds, so the sky, the world and the UI read as one system: the bible's strongest unifying demand (§14, §20). Surfaces are A's paper and smoked glass. The identity lives in the tracing behaviour, not in panel decoration.

- **Main menu:** the live view across the gap at the rival tower, with a perspective grid over the sky. A small cluster of bracket squares slowly traces the tower's silhouette and the cloud edge. The title sits on a paper band at lower-left, menu items on a paper column. Selecting an item makes the trace cluster leave the tower and snap onto the item.
- **Settings:** A's form on paper, with the grid behind it at 12% and the bracket cluster as the focus mark. No trace animation on this screen.
- **HUD:** A's smoked chips. Teammates across the gap and the checkpoint reveal get a single bracket lock, never a cluster. The rival's floor shows as a stale "last filed" value until the next checkpoint (the GDD says checkpoints reveal rival progress).
- **Crosshair:** four bracket corners that tighten on hit, turn W1 orange when the reticle is over a rival, and spread with weapon bloom. The 1 px navy halo stays on in the orange state: orange against navy is 5.44, but bare orange against sky is only 2.53. It is the same object as the menu focus mark.
- **Risks:** tracing squares can tip into targeting/military-HUD or Rez-style cyber looks if overused. It needs a hard budget: one traced object on screen at a time. The trace is a custom component (bracket pool and edge sampling), so it is more code than A.

### Recommendation

**Build C on A's surfaces, and use B's frosted pane in exactly one place:** the in-match pause and scoreboard overlay, where the live match must stay visible behind the menu. C is the only direction with one memorable idea that is already part of the game's world, the bracket squares Alex picked out and the sky already uses. A's paper keeps text legible, and the single frosted pane gives the owner real architectural glass without making the whole UI depend on a blur pass.

What still makes even the recommendation feel like a demo: until the motion table and its sound pairings are built, every one of these directions reads as a static mockup over a greybox. Motion and sound are the deliverable, not the frames.

---

## 10. Mismatches and risks found while researching

- **`docs/systems/MENU_AND_HUD.md` vs the bible.** The current "Look" section specifies a flat dark card with amber primary actions. The bible §14 asks for white/blue space, thin lines and restrained transparency. **The bible is right**: the doc describes the greybox playtest menu and should be updated when the real UI lands. Amber also has to leave the primary-action role once a warning colour is reserved.
- **Red door vs warning colour.** TOWER.md says the locked stair door is red. Captures 04 and 05 show a red element (#8E2D3D) that is likely that door; I did not confirm it in the scene. A red warning (W2) would share its hue, which is the main reason W1 Safety Orange is recommended.
- **CARI's Gen X Soft Club palette** (greens, tans, beige) vs the bible's blue/white usage. Follow the bible (§1 above).
- **Suggestion, outside this task:** `docs/GDD.md` line 9 still says the game launches in Early Access on 29 Oct 2026, which is before today (12 Nov 2026). Either the date slipped or the line is stale.

---

## 11. Sources

- CARI: [Gen X Soft Club](https://cari.institute/aesthetics/gen-x-soft-club), [Frutiger Aero](https://cari.institute/aesthetics/frutiger-aero), [Y2K aesthetic](https://cari.institute/aesthetics/y2k-aesthetic), [team page](https://www.cari.institute/team), [FAQ](https://cari.institute/faq)
- [daisyUI trend summary: Gen X Soft Club](https://trends.daisyui.com/trend/gen-x-soft-club/) (secondary; used only for the palette disagreement)
- [Wipeout 3 (Wikipedia)](https://en.wikipedia.org/wiki/Wipeout_3), for The Designers Republic's role in icons, colour schemes and custom type
- [PopMatters: Going HUDless for Immersion](https://www.popmatters.com/107913-going-hudless-for-immersion-2496052745.html), for Mirror's Edge's minimal HUD and single reserved accent
- [TCRF: SSX 3](https://tcrf.net/SSX_3_(PlayStation_2)), for context only
- Not reachable by automated fetch: gameuidatabase.com (403) and [uxdesign.cc: UI breakdown, Ridge Racer Type 4](https://uxdesign.cc/ui-breakdown-ridge-racer-type-4-3df11df52aa6) (403). The Ridge Racer, Gran Turismo, Rez, SSX Tricky, PS2 browser, The Finals, Bomb Rush Cyberfunk and Neon White notes come from my knowledge of the games and should be checked against footage.
- Contrast ratios were computed with the WCAG 2.x relative-luminance formula. Glass composites were computed both in sRGB (SVG) and linear (Unity, `m_ActiveColorSpace: 1`). The scripts live in the session scratchpad, not the repo.
