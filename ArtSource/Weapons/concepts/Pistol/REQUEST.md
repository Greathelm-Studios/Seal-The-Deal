<!--
  AI-GENERATED SOURCE FILE
  Generator : Claude Opus 5.5 (Claude Code)
  Generated : 2026-11-20
  Prompt    : Alex: "start on making some gun assets ... the regular pistol model ... follow [the props session's] method";
              picked "Codex shows all 3" looks (Aero corporate, toon realistic, office-gadget gun).
  Reviewed  : NOT YET REVIEWED BY A HUMAN
  Summary   : The brief to Codex for the regular pistol's concept sheet, so Alex can pick a look as a picture before the
              stand-in in ArtSource/Weapons/pistol.py is remodelled. It uses the same route as the office props
              (ArtSource/Kit/concepts/Props_Aero/REQUEST.md).
-->
# Regular pistol: request to Codex

**From:** Alex (environment artist), drafted by Claude · **Date:** 2026-11-20 · **Status:** Waiting for Codex

Seal the Deal is a fast, slapstick, close-quarters PvP shooter set in the office of **AONAGI (青凪総合サービス株式会社)**, a fictional Japanese company styled as **a 2002 Japanese office-equipment catalogue** (Frutiger Aero / Soft Club: pearl-white gloss, aqua translucency, brushed silver, soft highlights).
Characters are toon-shaded salarymen in suits.
The **regular pistol** is the basic gun everyone carries. It is held one-handed in first person, low in the bottom right of the view, and other players see it in the hand of a full body.

Attached:
- `before_pistol.png`: the current stand-in, a box-built pistol in six orthographic views with its size stamped. This is the shape being replaced.
- `ArtSource/Kit/concepts/Props_Aero/sheet_1_workstation.png`: the approved office props, for the palette and the product-design feel.

Use your **image generation tool**. Paint new designs; don't just paint over the before sheet.

## Three looks, one sheet

Paint **three options side by side**, so Alex can pick one or ask for a mix:

- **A: Aero corporate.** A believable compact service-pistol silhouette, made as if AONAGI's product designers had designed it in 2002: a pearl-white gloss polymer frame, a brushed-silver slide, a translucent aqua part (the magazine base, a grip insert or a window into the magazine), a small AONAGI ring badge on the grip, and soft radii everywhere. It should match the props.
- **B: Toon realistic.** A plain dark service pistol (gunmetal, near-black, a matte dark grip) with chunky, slightly exaggerated toon proportions: a thicker slide, a bigger trigger guard and readable sights. It reads instantly as "weapon" against the white office. The only office touch is a small aqua or silver detail at most.
- **C: Office-gadget gun.** A pistol built from office kit, for example a heavy-duty stapler body as the slide, a label-maker grip, or a hole-punch lever as the hammer. It must still **read as a pistol at a glance**: a clear grip, trigger guard, barrel line and muzzle. Pearl, aqua and silver like A, plus one product detail that gives the joke away.

## Rules for all three

- **One memorable shape and one clear material break.** No sci-fi greeble, no RGB, no dirt or wear, no real brand, logo or model name. The AONAGI ring (an open aqua ring with a red hanko dot at its lower-right gap) is the only logo.
- Palette: pearl white `#F4F8F8`, silver `#C8D2D9`, aqua `#45BFC7`, frosted aqua `#C6E9E6`, mist `#A8BFD1`, cobalt `#244FB9` (small), deep navy `#111B38` / near-black, hanko red `#C7473E` (logo dot only). Option B may use gunmetal `#2A2E35` and near-black for its main parts.
- **Chunky enough to read at first-person size and in another player's hand at 10 m.** Details under about 4 mm at real size will be texture, not geometry.
- **The muzzle must read as an opening** from the front: a dark bore and a visible front face.
- Keep it **non-gory and comedic-corporate**, not militaristic: no tactical rails, lights, suppressors or optics.

## Keep (gameplay envelope; metres, length × width × height)

| What | Keep |
|---|---|
| Overall | About 0.20 × 0.032 × 0.165 (length muzzle to back, width, height top of sights to grip base). Up to ±15 % is fine if the look needs it; say what you changed |
| Grip | About 0.048 front-to-back × 0.032 across × 0.105 tall, raked back about 14°. The character's hand is small (6 cm across the knuckles), so **the grip must not get thicker** |
| Trigger guard | Big enough for a cartoon index finger: inner opening about 0.040 × 0.020 |
| Orientation | The muzzle points forward; the top of the slide is flat enough for front and rear sights |
| Moving parts | The slide must be one separate part that can slide back about 0.03 under recoil, and the magazine one separate part that drops out of the grip base (for a later reload animation) |

## Deliver

Write only inside `ArtSource/Weapons/concepts/Pistol/`.

1. `sheet_pistol_options.png`: one landscape sheet, about 2400 × 1350 (or the largest 16:9 your tool gives), a clean white-to-pale-aqua studio background. For each of A, B and C: a large **three-quarter view**, a small **left side view** (muzzle to the left) and a small **front view**, the option's name and 3–5 short material or detail callouts.
2. `README.md`, for the person who will **model the chosen option in Blender from bevelled boxes, cylinders, simple extrusions and an occasional shallow boolean**:
   - per option: the overall envelope, then each part (slide, frame, grip, magazine, trigger, guard, sights, muzzle, badge or gadget parts) with its **size in millimetres and position** relative to the grip's middle (X width, Y forward is negative, Z up), its colour from the palette, and whether it is **geometry** or **texture/decal**;
   - which parts are separate moving parts (slide, magazine, and for C any gadget lever);
   - one line per option on why it reads as a pistol from the side and from the front;
   - how many image tries you made and which file came from which.

Do not change any file outside this folder. No real brands. Keep the sizes above.
