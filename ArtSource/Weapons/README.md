# Weapon art

**Status:** Current · **Last updated:** 2026-11-21

The look of every weapon is fixed in [docs/notes/weapon-look-flat-low-poly.md](../../docs/notes/weapon-look-flat-low-poly.md): simple low-poly stylised guns in flat colours.

## Regular pistol

`Assets/_Project/Art/Weapons/Models/Pistol.fbx` is the pistol from the "Guns Low poly" pack, so it matches the rifle and rocket launcher planned from the same pack. [pistol_adapt.py](pistol_adapt.py) builds it from the download, which is kept unchanged in [Source/GunsLowPoly_SatendraSaraswat/](Source/GunsLowPoly_SatendraSaraswat/).

```bash
blender -b --factory-startup -P ArtSource/Weapons/pistol_adapt.py -- <out>/Pistol.blend
node <blender-modeling skill>/scripts/mdl.mjs check  --blend <out>/Pistol.blend
node <blender-modeling skill>/scripts/mdl.mjs export --blend <out>/Pistol.blend --out <out>/export
node <blender-modeling skill>/scripts/mdl.mjs verify --manifest <out>/export/manifest.json
```

Then copy `<out>/export/Pistol.fbx` over `Pistol.fbx` and run two menu items:
1. **Seal the Deal > Art > Build Weapon Toon Materials** puts the pistol on the arm's Character Toon shader with a 1.5 px near-black contour (line art), and bakes the smoothed normals the contour needs into `Assets/_Project/Art/Weapons/Models/Outline/`.
2. **Seal the Deal > Greybox > Rebuild Viewmodel** hangs the new pistol in the hand, on those outline-baked meshes.

What the script does to the source:
- **Stands it upright**, muzzle along +Z in Unity, 190 mm long, 8 % taller than the source, the slide 40 % wider (26 mm) so it reads chunky, and the frame (grip and guard) only 15 % wider (30 mm): at 36 mm the grip read too thick in the hand (Alex, 2026-11-21). The origin is the middle of the grip, which is where the hand's `GripHand` seat is measured from.
- **Cuts the fused mesh into parts:** `Pistol_Slide` (cut along the shut line, with the sights), `Pistol_Frame` (with the hammer and the logo), `Pistol_Trigger`, and `Pistol_Magazine`, which the script adds (the source has none): a 4 mm base plate under the grip and a 56 mm body that runs up inside the grip along its 12.3 degree lean, hidden until a reload slides it out. The body is the grip's bottom outline drawn in to 62 % of its width and 55 % of its depth and set 3 mm toward the back strap; the script counts body corners outside the frame and prints the lean (both must match `Reload magazine length` and `Reload magazine lean` on the viewmodel profile). Only menu item 1 below is needed after a magazine-only change: the bake overwrites the outline mesh in place.
- **Adds character:**
  - aqua sight blocks;
  - a raised aqua AONAGI ring with its red hanko dot on the left grip panel;
  - the aqua magazine base.
- **Colours:**
  - brushed-silver slide and trigger;
  - deep-navy frame;
  - mist-blue grip panels;
  - aqua sights, base and ring;
  - hanko-red dot.

  Faces are flat-shaded for the low-poly look.
- **Size:** 1,236 triangles.

## Grip hand

`Assets/_Project/Art/Weapons/Models/PistolGripHand.fbx` is the right hand the game shows holding the pistol. It is erik90mx's "Low Poly Hand (Rigged)" (CC0, from BlendSwap; Alex picked it, 2026-11-21) posed round our grip by [pistol_grip_hand.py](pistol_grip_hand.py). The download is kept unchanged in [Source/LowPolyHand_erik90mx/](Source/LowPolyHand_erik90mx/). The character's own hand is a mitten that crumples when its fingers curl, so the body hides it while this hand is held (see [VIEWMODEL.md](../../docs/systems/VIEWMODEL.md)).

```bash
blender -b --factory-startup --disable-autoexec -P ArtSource/Weapons/pistol_grip_hand.py -- <out>/PistolGripHand.blend [NAME=VALUE ...]
node <blender-modeling skill>/scripts/mdl.mjs check  --blend <out>/PistolGripHand.blend
node <blender-modeling skill>/scripts/mdl.mjs export --blend <out>/PistolGripHand.blend --out <out>/export
```

`--disable-autoexec` matters: the download carries a rig script (`rig_ui.py`) that is never run. Copy `<out>/export/PistolGripHand.fbx` into the models folder, then run the same two menu items as for the pistol. Every number in the script can be tried from the command line (`KZ=2 KY=12 KX=-27` moves the middle knuckle, `I1=..` to `T3=..` bend the joints, `THUMB_FOLD`, `THUMB_SWING`, `THUMB_TWIST`, `FINGER_THICK`, `PALM_THICK`).

What the script does:
- Takes only the hand mesh and its bones from the download, and drops the rig's controller constraints.
- Restyles it to the character's hands: fingers and thumb 1.35 times thicker round their bones.
- Bends each joint toward the palm, every joint of a finger about that finger's one hinge (working it out per joint flipped it past 90 degrees and bent the fingertips outward). The middle, ring and little fingers curl 70/20/65, 60/15/65 and 50/5/65 degrees (the shorter ring and little fingers curl less, or their smaller loop cuts into the front strap) and wrap the front strap onto the grip's left panel; the index (10/50/30) rests forward on the trigger. The thumb points up, crosses the back of the grip under the slide (90 degrees at its first joint) and lies forward along the grip's left side, the side the player sees: its two outer joints bend about a hinge turned -130 degrees about the thumb (`THUMB_TWIST`), because the palm hinge alone can only curl it toward the grip. Before 2026-11-22 the thumb lay along the right of the frame, hidden behind the gun from the eye (Alex: "I can't see the thumb").
- Joins the middle, ring and little fingers into one mitten (Alex, 2026-11-21): each joint level of the three is wrapped in one convex shell. The palm, index, thumb and shells are then rebuilt as one closed surface (voxel remesh at 2 mm) and reduced to 1,600 triangles. A boolean union was tried first: it left holes and stacked faces that showed as black patches in play.
- Cuts the source's forearm off 16 mm behind the wrist and caps it, so it stays inside the jacket cuff. The jacket's gun-arm sleeve ends 12 mm up the forearm from the wrist joint (cut by `GreyboxViewmodelSetup`), so if the wrist changes, keep it within about 20 mm of the forearm bone on the gun-up side between 12 and 40 mm up the forearm, or it shows through the sleeve (measured 2026-11-22: 0 hand vertices outside the jacket standing, up to 3.5 mm at the sleeve end mid-swing).
- Stands it against the grip, middle knuckle at (-30, -4, 2) mm in Pistol.blend's space (just off the front right corner of the grip; at -18 the knuckles sat 18 mm ahead of the front strap and the mitten looped up to 45 mm out in front of it, Alex: "fingers still don't fully wrap around the grip", 2026-11-22); scale 0.081 m per source unit, so the knuckles are 68 mm across like the character's.
- One smooth-shaded material, `Hand_Skin`, mapped in Unity to `Pistol_Hand` (the character's skin colours with the gun's 1.5 px line). 1,600 triangles, closed (no open edges).
- Fit to the gun was measured, not judged by eye: hand vertices inside the pistol's parts went from 72 (deepest 13 mm) to 10, none deeper than 2 mm (2026-11-22); with the new pose, 10 (9 in the frame, 1 in the magazine), deepest 1.9 mm. The thumb reaches the grip's left side and the mitten wraps the front strap onto the left panel.

Before this, the grip hand was modelled from blocks in the same script; dropped as "too flat and blocky" (Alex, 2026-11-21). The first pose of this hand had five separate fingers that did not reach round the grip, fingertips that bent outward and a forearm that poked out of the cuff (Alex, 2026-11-21).

Earlier tries are in [concepts/Pistol/](concepts/Pistol/):
- painted concepts, rejected;
- a scripted model, rejected;
- Yurrpo's Stylized Handgun, adapted and then dropped as "too plain / generic" and too dark.

## Downloaded models and credits

These are CC-BY 4.0: free to use, including commercially, if the author is credited in the game.

**[GDD line 144](../../docs/GDD.md) says no asset-store packs ship in the game; packs may only stand in during greybox.** Until Adam decides otherwise, these are stand-ins and the credits below are needed only if they ship.

| Model | Author | Licence | Source | Used for |
|---|---|---|---|---|
| Stylized Handgun | Yurrpo | CC BY 4.0 | https://sketchfab.com/3d-models/stylized-handgun-dde19264b5dc45c2ba8143903ea69a4c | Not used (an earlier pistol try) |
| Stylized pistol | vit4ce | CC BY 4.0 | https://sketchfab.com/3d-models/stylized-pistol-d9e27480286f446a88c96616160c89b2 | Backup pistol (not used yet) |
| Low Poly Hand (Rigged) | erik90mx | CC0 | https://www.blendswap.com/blend/2429 | Grip hand on the pistol (no credit needed) |
| Guns Low poly | Satendra Saraswat | CC BY 4.0 | https://sketchfab.com/3d-models/guns-low-poly-e61b238f8c364c5bbdfe4bab0aaa08a3 | Regular pistol; rifle and rocket launcher next |

Credit line for what ships now: "Guns Low poly by Satendra Saraswat, licensed under CC BY 4.0".
