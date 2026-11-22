# Regular pistol — three concept options

**Status:** Target — visual options for Alex to choose; no option selected.

**Last updated:** 2026-11-20

[sheet_pistol_options.png](sheet_pistol_options.png) compares **A / Aero corporate**, **B / Toon realistic** and **C / Office-gadget gun**.

Each option has a large three-quarter view, a smaller left side view with the muzzle to the left, a front view and four material/detail callouts.

The sheet is newly generated concept art; these dimensions are the Blender modelling targets, rather than measurements taken from painted pixels.

## Envelope and coordinates

Follow [REQUEST.md](REQUEST.md) for the envelope: **200 mm long × 32 mm wide × 165 mm high**, unchanged for all three options.

The stand-in reference stamps `0.032 × 0.204 × 0.164 m` and its pictured height/length differ from this envelope; the written request is authoritative for these replacement concepts.

- Work in millimetres below; divide by 1,000 for Blender metres.
- Origin `(0, 0, 0)` is the middle of the grip body, excluding its magazine base.
- `X` is width, `−Y` is forward toward the muzzle, and `+Z` is up.
- All size triples in the part tables are **X × Y × Z**, not length × width × height.
- All position triples are **part centres in the assembled rest pose**, except explicitly named faces, openings and pivots.
- A left side view looks from `−X`; show `−Y` toward the left of the picture.
- A front view looks from negative Y toward positive Y.
- Overall bounds are `X = −16…+16`, `Y = −150…+50`, `Z = −57.5…+107.5`.
- Maximum grip width is **32 mm including all geometry**; decals add no physical thickness.

### Grip shape and magazine fit

Make the grip as a simple sheared extrusion, rather than rotating a 48 × 105 mm box and accidentally increasing its height.

At every horizontal slice the grip is **32 mm wide × 48 mm front-to-back**.

Its vertical span is `Z = −52.5…+52.5`, and its slice centre is `Y = −Z × tan(14°)`.

Thus the top centre is `(0, −13.09, +52.5)`, the bottom centre is `(0, +13.09, −52.5)`, and the grip rakes back 14° toward its base.

The complete sheared grip has a Y bounding extent of 74.18 mm; **48 mm is its horizontal section depth**, not that complete bounding extent.

The magazine body uses the same centreline, with a 24 × 34 mm horizontal section and a 99 mm vertical span from `Z = −52.5…+46.5`.

Its centre is `(0, +0.75, −3)`.

The separate base uses a 32 × 48 mm horizontal section, spans `Z = −57.5…−52.5`, and is centred at `(0, +13.71, −55)` on the same rake.

The grip body ends at the magazine-base seam; keep the base flush with its width and profile, with no heel sticking out.

The tables mark these sheared section dimensions with **S**; other triples are axis-aligned bounding sizes.

### Shared construction rules

Use bevelled boxes for the slide and frame, an extruded grip profile, an extruded guard outline, short cylinders for the decorative muzzle recess and simple sight blocks.

Use roughly 2–3 mm edge bevels on A/C and 1.5–2 mm chamfers on B; these soften existing edges rather than adding little surface parts.

The muzzle is a **shallow blind visual recess**, not a modelled barrel or functional interior.

Its 10 mm dark mouth is 4 mm deep along Y, centred at `(0, −148, +83.5)`, opening at the front face `Y = −150` and ending at `Y = −146`.

Shade the back and inside deep navy so the front and three-quarter views read as an opening, with plenty of visible front face around it.

Keep markings, badge strokes, grip grain and shallow slide grooves in textures/decals; do not turn features under about 4 mm into separate geometry.

The image's muzzle-face border is a bevel/material break in the slide's end face, not an extra protruding attachment.

## A / Aero corporate

**Overall:** 200 L × 32 W × 165 H mm; no envelope change.

**Memorable shape:** a softly rounded silver upper slab over one continuous pearl lower silhouette.

**Main material break:** brushed silver slide against glossy pearl frame/grip, with a restrained frosted aqua base.

| Part | Size, mm (X × Y × Z) | Centre / position, mm | Palette and finish | Build |
|---|---|---|---|---|
| Slide | 30 × 200 × 38 | `(0, −50, +83.5)` | Silver `#C8D2D9`, soft brushed highlight | Geometry: one rounded box; separate moving slide |
| Frame / dust cover | 32 × 190 × 19 | `(0, −48, +55)` | Pearl `#F4F8F8`, gloss polymer | Geometry: one simple extruded cradle, ending at `Z = 64.5` |
| Grip body | 32 × 48 × 105 **S** | `(0, 0, 0)` | Pearl `#F4F8F8`, slightly softer gloss | Geometry: sheared extrusion, softly rounded heel and edges |
| Magazine body | 24 × 34 × 99 **S** | `(0, +0.75, −3)` | Deep navy `#111B38`, matte; concealed when inserted | Geometry: simplified solid insert; separate magazine assembly |
| Magazine base | 32 × 48 × 5 **S** | `(0, +13.71, −55)` | Frosted aqua `#C6E9E6`, tinted with aqua `#45BFC7` | Geometry: frosted translucent end plate, flush with grip; child of magazine |
| Trigger | 8 × 10 × 19 | `(0, −52, +36)` | Deep navy `#111B38`, satin | Geometry: simple curved extruded tab, top at `Z = 45.5` |
| Trigger guard | 14 × 50 × 30 outer | `(0, −61, +35.5)` | Pearl `#F4F8F8`, gloss | Geometry: rounded rectangular outline, 5 mm profile walls |
| Guard's empty outline opening | 40 along Y × 20 high | Opening centre `(0, −61, +35.5)` | Empty space before the trigger is added | Opening bounds: `Y = −81…−41`, `Z = 25.5…45.5` |
| Front sight | 4 × 10 × 5 | `(0, −134, +105)` | Deep navy `#111B38` | Geometry: one clear block on slide |
| Rear sight | 20 × 10 × 5 overall | `(0, +35, +105)` | Deep navy `#111B38` | Geometry: two broad ears; central notch 6 mm wide × 4 mm deep vertically |
| Muzzle face | 30 wide × 38 high | Face centre `(0, −150, +83.5)` | Silver `#C8D2D9` | Geometry: existing slide end face, shallow rounded perimeter bevel |
| Dark muzzle recess | Ø10 × 4 depth along Y | `(0, −148, +83.5)` | Deep navy `#111B38` | Geometry: shallow circular pocket; dark material inside and at back |
| AONAGI ring badge | 0 × 16 × 16 | `(−16, 0, 0)` on left grip; mirror at `X = +16` if needed | Aqua `#45BFC7`; lower-right dot hanko red `#C7473E` | Texture/decal: open ring, about 2 mm stroke and 3 mm dot; no badge plate |
| Three slide grooves | Each 0 × 6 × 22 | Side surface `X = ±15`, `Y = +12, +24, +36`, `Z = +83.5` | Silver base, shaded with mist `#A8BFD1` | Texture/normal detail: broad rear grooves, no individual geometry cuts |

The trigger guard's 40 × 20 mm size describes its interior outline; the trigger occupies part of that space.

**Moving pieces:** slide, including both sights and the muzzle face; magazine, including its aqua base.

**Pistol read:** the side has a long level slide, angled grip and open guard; the front has a dark round muzzle framed by silver above a narrow full-height grip.

## B / Toon realistic

**Overall:** 200 L × 32 W × 165 H mm; no envelope change.

**Memorable shape:** a thicker square-shouldered slide with a boldly outlined guard.

**Main material break:** satin gunmetal upper against matte near-black lower; the only office accent is one small silver control.

| Part | Size, mm (X × Y × Z) | Centre / position, mm | Palette and finish | Build |
|---|---|---|---|---|
| Slide | 32 × 200 × 42 | `(0, −50, +81.5)` | Gunmetal `#2A2E35`, satin | Geometry: one broad-chamfered box; separate moving slide |
| Frame / dust cover | 32 × 190 × 15 | `(0, −48, +53)` | Near-black navy `#111B38`, matte | Geometry: simple cradle, ending at `Z = 60.5` |
| Grip body | 32 × 48 × 105 **S** | `(0, 0, 0)` | Near-black navy `#111B38`, matte | Geometry: same narrow sheared grip; broad bevels rather than added thickness |
| Magazine body | 24 × 34 × 99 **S** | `(0, +0.75, −3)` | Gunmetal `#2A2E35` | Geometry: simplified concealed insert; separate magazine assembly |
| Magazine base | 32 × 48 × 5 **S** | `(0, +13.71, −55)` | Near-black navy `#111B38`, matte | Geometry: flush dark end plate, child of magazine |
| Trigger | 8 × 12 × 19 | `(0, −52, +36)` | Near-black navy `#111B38` | Geometry: one chunky curved extruded tab |
| Trigger guard | 16 × 54 × 34 outer | `(0, −61, +35.5)` | Near-black navy `#111B38`, matte | Geometry: rounded rectangular outline with 7 mm profile walls |
| Guard's empty outline opening | 40 along Y × 20 high | Opening centre `(0, −61, +35.5)` | Empty space before trigger | Same opening bounds as A, with a heavier outer border |
| Front sight | 4 × 10 × 5 | `(0, −134, +105)` | Near-black navy `#111B38` | Geometry: readable squared block on slide |
| Rear sight | 20 × 10 × 5 overall | `(0, +35, +105)` | Near-black navy `#111B38` | Geometry: broad ears around a 6 mm wide × 4 mm deep vertical notch |
| Muzzle face | 32 wide × 42 high | Face centre `(0, −150, +81.5)` | Gunmetal `#2A2E35` | Geometry: bevelled end face of the slide; no separate bright accessory |
| Dark muzzle recess | Ø10 × 4 depth along Y | `(0, −148, +83.5)` | Near-black navy `#111B38` | Geometry: shallow pocket, dark inside/back; opening slightly above face centre |
| Silver control | 0 × 14 × 8 | `(−16, −5, +55)` on left frame | Silver `#C8D2D9` | Texture/decal with a rounded silhouette and shaded edge; no extra moving part |
| Two broad slide grooves | Each 0 × 6 × 24 | `X = ±16`, `Y = +15, +30`, `Z = +81.5` | Gunmetal `#2A2E35`, navy shadow `#111B38` | Texture/normal detail |
| Grip finish | 0 × 36 × 72 patch | `X = ±16`, patch centre `Y = +2, Z = −5` | Near-black navy `#111B38`, rougher than frame | Texture/roughness only; no fine raised stippling |

The slide is 4 mm taller than A, and the frame is 4 mm shallower, so the top, grip and total height stay fixed.

**Moving pieces:** slide, including sights and muzzle face; magazine, including its flush dark base.

**Pistol read:** the side's thick horizontal upper and generous negative-space guard are immediately legible; the front's visible squared face and dark circular recess read as a muzzle against the white office.

## C / Office-gadget gun

**Overall:** 200 L × 32 W × 165 H mm in the closed rest pose; no envelope change.

**Memorable shape:** a soft desktop-stapler lid with a broad inset aqua lever, above a clear pistol grip and guard.

**Main material break:** pearl upper against silver lower cradle, with aqua lever/base accents and one small `APPROVED` label.

The picture shows the lever **closed**: its large visible aqua side is a paddle of a paired side-lever piece recessed into the lid.

Keep the top silhouette pearl, as painted, rather than adding an exposed handle above the sights.

| Part | Size, mm (X × Y × Z) | Centre / position, mm | Palette and finish | Build |
|---|---|---|---|---|
| Stapler-lid slide | 32 × 200 × 38 overall | `(0, −50, +83.5)` | Pearl `#F4F8F8`, gloss | Geometry: rounded box with a softened nose and shallow side recesses; separate moving slide |
| Frame / lower cradle | 32 × 190 × 19 | `(0, −48, +55)` | Silver `#C8D2D9`, brushed | Geometry: simple channel-like silhouette, ending at `Z = 64.5` |
| Label-maker grip body | 32 × 48 × 105 **S** | `(0, 0, 0)` | Pearl `#F4F8F8`, gloss | Geometry: same sheared grip, broad clean side faces for label |
| Magazine body | 24 × 34 × 99 **S** | `(0, +0.75, −3)` | Deep navy `#111B38`, concealed | Geometry: simplified insert; separate magazine assembly |
| Magazine base | 32 × 48 × 5 **S** | `(0, +13.71, −55)` | Frosted aqua `#C6E9E6`, aqua `#45BFC7` tint | Geometry: flush translucent plate; child of magazine |
| Trigger | 8 × 10 × 19 | `(0, −52, +36)` | Deep navy `#111B38` | Geometry: one curved extruded tab |
| Trigger guard | 14 × 50 × 30 outer | `(0, −61, +35.5)` | Silver `#C8D2D9` | Geometry: rounded rectangular outline with 5 mm profile walls |
| Guard's empty outline opening | 40 along Y × 20 high | Opening centre `(0, −61, +35.5)` | Empty space before trigger | Same interior outline as A |
| Front sight | 4 × 10 × 5 | `(0, −134, +105)` | Deep navy `#111B38` | Geometry: simple block, separate from lever, child of slide |
| Rear sight | 20 × 10 × 5 overall | `(0, +35, +105)` | Deep navy `#111B38` | Geometry: broad ears around a 6 mm wide × 4 mm deep vertical notch; child of slide |
| Muzzle face | 32 wide × 38 high | Face centre `(0, −150, +83.5)` | Pearl `#F4F8F8`; silver `#C8D2D9` face material | Geometry: existing nose face with rounded corners; silver area is a material mask |
| Dark muzzle recess | Ø10 × 4 depth along Y | `(0, −148, +83.5)` | Deep navy `#111B38` | Geometry: shallow circular pocket with a dark inside/back |
| Aqua lever, two side paddles | Each 4 × 104 × 20; combined width 32 | `(−14, −6, +78.5)` and `(+14, −6, +78.5)` | Aqua `#45BFC7`, softly glossy | Geometry: two bevelled extruded paddles in one moving group; front end softly curved |
| Lever side pockets in slide | Each 4 deep along X × 108 along Y × 34 high | `(−14, −6, +81.5)` and `(+14, −6, +81.5)` | Recess shadow uses navy `#111B38` only where exposed | Geometry: shallow subtractive pockets; leave pearl top cap from `Z = 98.5…102.5` |
| Lever pivot | Ø8 × 24 long along X | Pivot / axle centre `(0, +42, +78.5)` | Silver `#C8D2D9`, mostly hidden | Geometry: optional simplified cylinder linking the paddles; no exposed mechanism |
| APPROVED label | 0 × 30 × 14 | `(−16, −4, +2)` on left grip; optional same decal on right | Navy `#111B38` rectangle, pearl `#F4F8F8` letters | Texture/decal: rounded label window printed `APPROVED`; no raised bezel or new logo |

The lever pockets span `Y = −60…+48`, `Z = +64.5…+98.5`; the paddles span `Y = −58…+46`, `Z = +68.5…+88.5` when closed.

These are art clearances for a small lever rock, while the external side faces stay at `X = ±16`.

**Moving pieces:** pearl slide with sights/muzzle; magazine with aqua base; aqua lever group parented to the slide, with its origin at the specified pivot.

An optional cosmetic lever tip of **0 to −5° about X**, relative to the slide, lifts its forward end into the pocket clearance; do not make it fire staples or introduce a new gameplay action.

**Pistol read:** the side keeps a long barrel line, back-raked grip and open guard despite the stationery joke; the front keeps sights and a dark round opening in a visible nose face rather than a stapler's flat closed end.

## Blender separation and display

| Group | Contents | Visual motion / rest placement |
|---|---|---|
| Fixed frame | Frame, grip shell, guard, trigger and decals | Keep the authored assembled position; trigger can remain its own mesh |
| Slide | Upper shell, front/rear sights and muzzle recess surfaces | One root translates **+30 mm along Y** to show recoil, then returns; its closed pose is the tables above |
| Magazine | Concealed body and visible base | One separate root drops down the grip axis, toward `(0, +0.242, −0.970)`; no piece of the base stays on the grip |
| C lever | Paired aqua paddles and optional simplified axle | Child of slide; separate pivot at `(0, +42, +78.5)`; closed in the sheet |

Keep the grip shell's magazine clearance aligned to the same shear so the insert can move down without clipping its outside walls.

No functional internal mechanism is needed for these game assets.

The painted seams are deliberately subtle; create actual object boundaries for slide, magazine and C's lever rather than baking them into one mesh.

Preview the eventual model from the first-person camera low on the right, in a small hand, and against a white office at 10 m before choosing detail density.

Those in-game readability checks have **not** been performed as part of this concept-sheet task.

## Image attempts, files and inspection

**Tool:** built-in `image_gen`, with a non-transparent background; no CLI/API fallback, paintover or manual compositing.

**Two image-generation attempts were made; one image was produced.**

| Attempt | Result | File provenance |
|---|---|---|
| 1 | Image service rejected the output at its safety stage (`moderation_blocked`, category `illicit`); no image returned | No image file from this attempt |
| 2 | Successful new concept sheet after explicitly clarifying that the subjects are fictional, nonfunctional game props | Tool output `exec-f605f29c-7693-4cac-aa6f-445f3d0ac4ff.png`, copied unchanged to [sheet_pistol_options.png](sheet_pistol_options.png) |

The successful tool output is **1672 × 941 px**, approximately 16:9, at its returned native resolution.

The prompt requested 2400 × 1350 or the largest native 16:9 output; the delivered image has not been upscaled.

The two attachment files are inputs only and were left unchanged.

Visual inspection of the returned image confirmed three named options, nine complete views, left-pointing side views, visible dark front openings, four callouts each, and the palette/material differences.

The generated image is an appearance reference, not an orthographic CAD drawing; use the numeric guide for exact width, grip rake, clearances and moving-part boundaries.

The actual palette hex values in this guide take precedence over lighting and colour variation in the rendered image.

Static file inspection used PowerShell with `System.Drawing.Image.FromFile(...)`: **1672 × 941**, `Format24bppRgb`.

No Unity, Blender, Play Mode, multiplayer, performance or automated test validation was run.

## Final successful image prompt

The following is the complete prompt used for attempt 2.

```text
Create a newly painted, polished 16:9 concept-art sheet for three FICTIONAL, NONFUNCTIONAL VIDEO-GAME PISTOL PROPS in the slapstick office game Seal the Deal. These are stylized external cosmetic designs only, with solid decorative bodies and shallow dark muzzle recesses, no working internals, technical mechanisms or construction diagrams. The user is choosing an art direction, not a working weapon.
Reference images in conversation: before_pistol.png is a rough box-shaped placeholder to replace; sheet_1_workstation.png is the approved 2002 Japanese office-equipment catalogue aesthetic. Paint new designs rather than painting over either reference.

Sheet: 2400 x 1350 or largest native 16:9. White-to-pale-aqua studio backdrop, soft product lighting, subtle shadows, clean navy typography and aqua separators, three equal columns A / B / C. Header "SEAL THE DEAL / REGULAR PISTOL" and subtitle "THREE DESIGN OPTIONS". In EACH column show exactly one LARGE front-left three-quarter beauty view, one SMALL left-side orthographic view with muzzle pointing LEFT, and one SMALL front view looking at its dark decorative bore. All complete and uncropped, consistent shape and materials between views, same object scale across columns. The larger views show the left grip side and muzzle. Generous whitespace. Below small views print "LEFT SIDE" and "FRONT". Four short legible callouts per column. Footer "200 x 32 x 165 mm | 32 mm grip | separate slide + magazine".

A title "A / AERO CORPORATE": compact service-pistol silhouette with smooth radii and a simple brushed-silver upper slide, glossy pearl-white lower frame and angled grip, frosted translucent aqua magazine base flush with the grip. A small AONAGI open aqua ring decal with a tiny red dot at the lower-right gap on the grip, sole logo. Navy front and rear sights. Main material break silver upper / pearl lower. A visible silver front face frames a dark round shallow decorative muzzle opening.
A callouts: "Brushed-silver slide"; "Pearl polymer frame"; "Frosted aqua magazine base"; "AONAGI ring decal".

B title "B / TOON REALISTIC": simple plain dark unbranded service-pistol prop, thicker chunky gunmetal rectangular slide with broad chamfers, matte near-black polymer frame and grip, generous rounded rectangular trigger guard and readable squared sights. A single small silver control detail is its only office accent. Two or three wide slide grooves at most. Main break gunmetal upper / matte near-black lower. A visible front face frames a dark circular decorative bore.
B callouts: "Chunky gunmetal slide"; "Matte near-black grip"; "Oversized trigger guard"; "Single silver control".

C title "C / OFFICE-GADGET GUN": office stationery joke with an unmistakable pistol silhouette. A rounded pearl-white heavy-duty desktop-stapler lid forms the upper slide; a broad aqua stapler lever sits nested FLUSH in the back half, shown closed. A brushed-silver lower cradle. A narrow angled pearl label-maker-like grip with ONE small dark navy label window reading "APPROVED" in pearl letters. A flush aqua magazine base. Front and rear sights and a visible front face surrounding a dark round shallow bore ensure this still reads as a pistol rather than a stapler alone. Main material break pearl upper / silver lower, restrained aqua accents.
C callouts: "Pearl stapler-lid slide"; "Silver lower cradle"; "Flush aqua lever"; "APPROVED label decal".

Common external ART proportions: about 200 mm long x 32 mm wide x 165 mm high. Slim grips about 48 mm front-to-back, 32 mm across and 105 mm tall, tilted back 14 degrees, no widened grip or projecting magazine heel. Clear horizontal upper silhouette, clear angled handle, open rounded rectangular guard with a generous cartoon-finger opening. A clean visual seam separates slide from frame, another seam identifies the removable cosmetic magazine base. Flat top with simple sights. Front views are narrow and show obvious dark round muzzle recesses framed by visible material, never a solid cap.
Finish: stylized 3D product concept rendering, clean simple bevelled shapes, soft highlights, readable chunky shapes for a hand-held game prop at small screen size. Comedic corporate office equipment, no military mood.
Palette: pearl #F4F8F8, silver #C8D2D9, aqua #45BFC7, frosted aqua #C6E9E6, mist #A8BFD1, navy #111B38. B gunmetal #2A2E35 and near-black. Red #C7473E only for the ring decal dot.
No real brands or real model names; no other logo; no tactical rails, accessories, optics, suppressors, lights, ammunition, mechanical cutaways, exploded working diagrams, fine sci-fi greeble, RGB, wear, dirt, people, hands or watermarks.
```

## Exact static inspection commands

Run from the repository root in PowerShell; this reads files only.

```powershell
Add-Type -AssemblyName System.Drawing
$pistolSheetPath = Join-Path (Get-Location) 'ArtSource/Weapons/concepts/Pistol/sheet_pistol_options.png'
$pistolSheetImage = [System.Drawing.Image]::FromFile($pistolSheetPath)
'{0} x {1}; {2}' -f $pistolSheetImage.Width, $pistolSheetImage.Height, $pistolSheetImage.PixelFormat
$pistolSheetImage.Dispose()
Get-ChildItem -File -LiteralPath 'ArtSource/Weapons/concepts/Pistol' | Select-Object Name,Length
git status --short -- ArtSource/Weapons/concepts/Pistol
```

Result: the PNG loads as **1672 × 941; Format24bppRgb**; the folder contains the original request and two attachments plus the new sheet and this guide.

The scoped git status reports the concept folder as untracked, as it did before creation; no commit or push was made.
