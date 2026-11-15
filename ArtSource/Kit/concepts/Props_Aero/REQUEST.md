<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-15
  Prompt    : Alex: "building up the office assets, cuz rn they look very bland and roblox like and blockouty, i want
              the assets to match the frutiger aero and gen x soft club 2000s office corporate vibe we got goin!"
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : Brief to Codex for prop concept sheets, so the furniture and props are agreed with Alex as pictures
              before they are remodelled in Blender (props_aero.py). Walls, floors and stairs are not in scope.
-->
# Office props, Aero / Soft Club — request to Codex

**From:** Alex (environment artist), drafted by Claude · **Date:** 2026-11-15 · **Status:** Waiting for Codex

Seal the Deal is a fast close-quarters PvP shooter set in the office of **AONAGI (青凪総合サービス株式会社)**, a fictional Japanese company.
The rooms are being restyled already. **The furniture and props still look like blockout boxes** ("bland, Roblox-like, blockouty", Alex).
Paint **concept sheets for the props** so they read as finished early-2000s Japanese corporate product design.

Attached:
- `before_*.png`: orthographic sheets of the props as they are now (sizes stamped). These are the shapes to replace.
- `unity_office_*.png`: how they sit in the level now.
- `concept_*_A.png`: the approved room look (Japanese Aero paintovers).

Use your **image generation tool**. Paint new designs; don't just paint over the before sheets.

## The look, in one line

**A 2002 Japanese office-equipment catalogue**: products by NEC / Sharp / Itoki / Okamura-era industrial design, photographed on white. Use them as era references only: invent every product, with no real brand, logo or model name anywhere.

## What "not blocky" means here

- **Rounded, pillowed forms** with generous radii (corner radius 10–40 mm on furniture, 5–15 mm on small props), crowned tops, soft capsule ends. No raw 90-degree box edges.
- **Material breaks**: pearl-white gloss plastic body + translucent aqua or frosted-milk acrylic part + brushed-silver or chrome hardware + a dark smoked part (screens, glazing). Every prop gets at least two.
- **Seams and inset panels**: shut lines, recessed panels, a chrome or silver trim band, a rubber foot, a shadow gap at the base.
- **Visible product details**, few but legible at 5 m: buttons, a small LED dot (cyan or green), vents as rounded slots, screen bezels, cable grommets, caster wheels, a gas-lift stem with its shroud, handles as silver bars or finger recesses.
- **The AONAGI ring logo** (open aqua ring with a red hanko dot at its lower-right gap) and short **bilingual labels** only where a real product would carry them (vending header, water server, reception, copier panel, asset tag).

## Rules from the art bible (keep them)

- **One memorable shape + one clear material break** per prop. No sci-fi greeble, no RGB, no dirt.
- Colours stay cool: pearl white `#F4F8F8`, silver `#C8D2D9` (metal), aqua `#45BFC7`, frosted aqua `#C6E9E6`, mist `#A8BFD1`, cobalt `#244FB9` (small), deep navy `#111B38` / near-black for screens and fabric, hanko red `#C7473E` (logo dot only). Leaves `#70AF59`.
- **No bubbles.** Plants only in planters. No vaporwave, pink chrome or rainbow gradients.
- Frutiger Aero comes through **gloss, translucency, aqua, chrome and soft highlights**, not decoration.
- Props are thrown and shot in slapstick fights: keep them **chunky enough to read in flight**.

## Keep (gameplay sizes; metres, W × D × H)

| Prop | Keep |
|---|---|
| Workstation_Bench | 2.4 × 1.6 back-to-back bench, desk top 0.74, **divider top 1.15** (cover), two drawer pedestals 0.40 × 0.55 × 0.60 |
| Monitor_Flat (loose) | panel 0.55 × 0.34, on a foot, total about 0.46 high |
| Keyboard (loose) | 0.44 × 0.14 × 0.015–0.03 |
| Phone_Desk (loose) | 0.24 × 0.22 × 0.10, corded handset |
| Doc_Tray (loose) | 0.28 × 0.34 × 0.18, two tiers |
| Chair_Task | seat 0.47 high, about 0.6 wide with base, back to about 1.05; five-star base on casters |
| Chair_Pod | 0.52 × 0.50, seat 0.46, back 0.86; swivel base |
| Desk_Sculpt | 1.6 × 0.8 × 0.74 |
| Desk_Reception | 4.0 × 0.9 × 1.1 (cover); AONAGI name on the front |
| Storage_Low | 1.6 × 0.45 × 1.0 (cover) |
| Copier | 0.62 × 0.66 × 1.05 |
| Planter_Box | 1.0 × 0.45 × 0.55 pot, leaves above |
| Water_Server | 0.35 × 0.35 × 1.35 with bottle; 給水 label |
| Umbrella_Stand | 0.22 dia × 0.55; umbrellas to 1.0 |
| Vending_Machine | 0.9 × 0.65 × 1.8; 自動販売機; fictional drinks |
| Slipper_Cabinet | 0.9 × 0.35 × 0.7; 室内履き |
| Light_Pendant_Linear | 2.4 × 0.07 × 0.06 bar hung 0.8 below the ceiling |
| Column_Graphic | 0.8 × 0.8 square column, 4.3 high, corner radius 0.06; aqua band graphic, floor code |

## Deliver

Write only inside `ArtSource/Kit/concepts/Props_Aero/`.

Four landscape sheets, each about 2400 × 1350 (or the largest 16:9 your tool gives), a clean white-to-pale-aqua studio background, each prop as a **three-quarter view** plus a **small front or side callout**, with its name, its kept size and 3–5 short material/detail callouts:

1. `sheet_1_workstation.png`: Workstation_Bench dressed with two Monitor_Flat, Keyboard, Phone_Desk, Doc_Tray; plus Chair_Task and Chair_Pod.
2. `sheet_2_desks_storage.png`: Desk_Sculpt, Desk_Reception (with the AONAGI logo and 青凪総合サービス), Storage_Low, Copier.
3. `sheet_3_amenities.png`: Water_Server, Umbrella_Stand, Vending_Machine, Slipper_Cabinet, Planter_Box.
4. `sheet_4_light_column.png`: Light_Pendant_Linear and Column_Graphic.

Then `contact_sheet.png` (the four sheets, labelled) and `README.md` for the person who will **model these in Blender from primitives, bevels and booleans**:
- per prop: the kept envelope, then every new feature with its **size in millimetres and position** (e.g. "seat shell crown radius 25 mm; silver gas-lift shroud ø60 × 180; five casters ø50"), the material of each part from the palette above, and which details are **geometry** vs **texture/label**;
- keep each prop modellable: say where a detail is too fine for geometry (under ~4 mm) and should be a texture;
- one line per prop on what makes it read as 2000s Aero rather than a box;
- how many image tries you made and which files came from which.

Do not change any file outside this folder. No real brands. Keep the sizes above.
