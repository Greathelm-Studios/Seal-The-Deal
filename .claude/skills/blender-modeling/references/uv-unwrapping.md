# UV unwrapping

`node scripts/mdl.mjs unwrap --blend <dir>/<name>.blend --out <dir>/uv/<name>.blend [--mode atlas|tiling|trim] [--density PX_PER_M] [--size 1024] [--padding PX] [--lightmap] [--trim Material=v0:v1,...] [--tight]`

Gives a built model UVs fit for texturing, the way a game artist would cut them, in place of the box mapping every `part()` gets. The input .blend is not changed. Run check (and `preview --uv`) on the result, then bake or export it as you would a build. Bake keeps an atlas made here, with its texel density and Lightmap map, instead of unwrapping again.

## Settings

The user picks these: from the request, or from Anim Review's preferences when a round names them. Ask when a game's texture budget is unknown and it matters.

| Setting | What it decides | Choose |
|---|---|---|
| `--mode atlas` (default) | Unique, non-overlapping UVs in 0..1, for a texture painted or baked for this one model | Props, weapons, anything baked or hand-painted |
| `--mode tiling` | World-scale UVs: each face projected along its main axis, the texture repeating every `size / density` metres; overlaps intended | Kit pieces and walls using tiling materials |
| `--mode trim` | Each `--trim` material's faces as straightened strips stacked in its V band of a trim sheet, tiling along U; other materials tiling | Kit pieces textured from a trim sheet |
| `--density` | Texel density, pixels per metre: the same on every part so props sit together without one looking blurrier | About 512 for third person, 1024 for first person, 200 to 256 for far or stylised cameras. Hidden-from-camera games go lower |
| `--size` | Texture size the density is measured against (512 to 4096) | The size the texture will be made or baked at |
| `--padding` | Pixels of gap: round each island's box (so twice it between two), or with `--tight` exactly it between islands and at the edge | Leave it: 4 at 512, 8 at 1024, 16 at 2048, 32 at 4096 |
| `--tight` | Packs each island by its exact shape at the texture's pixels, nesting islands into each other's gaps and holes; see [Tight packing](#tight-packing) | On for a texture painted or baked for this model, when a few seconds are worth more texels; it gains most on frames, rings and many small parts. Bake packs this way |
| `--lightmap` | A second UV map, `Lightmap`, non-overlapping, for Unity's baked lighting | On for static props in scenes with baked lightmaps; off when lighting is realtime or Unity's Generate Lightmap UVs is good enough. Not used for rendering in Blender |
| `--trim` | Each trim material's V band on the sheet, `Material=v0:v1` | From the trim sheet's layout, bottom of the sheet at 0 |

Without `--density`, an atlas fills the texture and the density it reaches is reported, so you can see what `--size` gives. With `--density`, when the islands don't fit `--size` at that density, they are scaled down to fit, and the command and `check` say so and name the size that would hold them.

## How an atlas is cut

1. Seams on edges sharper than 70° and on material borders.
2. Each seam region cut open into a disc: a seam joining a tube's two ends, or across a sphere. Without this cut a tube unwraps as a squashed ring. In tests a barrel went from 119% texel-density spread to 3–5%.
3. An angle-based unwrap. Islands that come out stretched get seams at 50°, 35° and then 25°, and any still stretched fall back to Smart UV Project. Bevel strips stay on the faces they round off, so no seam runs along a worn edge.
4. Every island scaled to the same texel density, then to `--density`. Strips over six times longer than wide are cut so the packer can fit them.
5. Islands turned to lie along U or V and packed with `--padding` round each one's box, or with `--tight` by their shapes. Faces an unwrap folded over each other get islands of their own.

`--lightmap` copies these islands into `Lightmap` and repacks them to fill 0..1 with Unity's 4 px margin (at 1024; by shape with `--tight`). Tiling and trim UVs overlap, so for them `Lightmap` is cut afresh as an atlas.

## Tight packing

`--tight` packs by shape instead of by bounding box, at the pixels of the `--size` texture (`scripts/uvpack.py`, numpy only):

- Each island is turned to lie along U or V, as by default, and tried at the four 90° turns.
- It is drawn on the texture's pixel grid as every pixel one of its triangles touches, and placed where that drawing misses the border and every island already placed grown by `--padding` (by the exact distance between pixel squares). So on the final UVs no two islands are closer than `--padding`, none is closer to the edge than `--padding` (what the default leaves there), and each island's lower-left corner sits on a whole pixel.
- Islands go in biggest box or outline first, so frames and rings come before what can fill them, each where its padding touches the most occupied pixels. Right after an island that walls off room (a frame's or a ring's middle), the islands still to come are tried inside it first. So islands nest into each other's concave gaps and holes.
- The search tries four orderings and two placement rules on a grid of 256 cells across, for the largest scale at which every island fits (a fit there is a fit at full resolution), runs the best two again on a grid twice as fine, then settles the winner at whole pixels. It is bounded by a count of grid operations, not by a clock, so the same model gives the same UVs on every run.
- With `--density` the islands keep their size, and when they don't fit they are scaled down as without `--tight`.

## Rules behind it

- Every hard edge should be a UV seam, or normal-map bakes show seams; a seam need not be a hard edge. ([Polycount: Normal map](http://wiki.polycount.com/wiki/Normal_map), [Texture Baking](http://wiki.polycount.com/wiki/Texture_Baking))
- Padding of 2 / 4 / 8 / 16 px at 256 / 512 / 1k / 2k, so mipmaps don't bleed neighbouring islands into each other. ([Polycount: Edge padding](http://wiki.polycount.com/wiki/Edge_padding)) The default packing leaves that padding round each island's box, so the gap between two islands is the gutter Polycount suggests (twice the padding). `--tight` leaves exactly the padding between islands, half that gutter; forum practice holds that it is enough with anisotropic filtering. Raise `--padding` if far mips bleed.
- Islands upright and axis-aligned, so details follow the pixel grid. ([Polycount: Texture Coordinates](http://wiki.polycount.com/wiki/Texture_Coordinates))
- Stacked or mirrored islands are fine for tiling and trim textures. Lightmap UVs must never overlap and need about 2 texels between charts. ([Polycount: Light map](http://wiki.polycount.com/wiki/Light_map), [Unity: lightmap UVs](https://docs.unity3d.com/Manual/LightingGiUvs-GeneratingLightmappingUVs.html))
- Unity reads UV maps by order: the first is UV0, the textures, and the second is UV1, the lightmap. Generate Lightmap UVs writes UV1 itself, with Hard Angle 88°, Angle Error 8%, Area Error 15% and Pack Margin 4 px.

## Measured

Blender 5.2.2, padding 4 px at 1024, comparing ways to unwrap four test props. Density spread is the area-weighted deviation of texel density; 0% is perfectly even.

| Prop | Box map (`part()` default) | Smart UV Project | Seams + angle-based (this step) |
|---|---|---|---|
| Bevelled crate | all faces overlap, 2.6% | 2.9% | 0.6% |
| Barrel | all faces overlap, 9% | 10.6% | 5.4%, or 2.6% with the strip straightened |
| Rock | all faces overlap, 11.8% | 15.0% | 6.5% |

Example crate (`examples/supply_crate.py`, 336 triangles), atlas at 1024:
- 34 islands, 100% of the surface within 20% of the median density, no overlaps, unwrapped in 0.1 s.
- 47% of the texture used at 440 px/m when filling it. At `--density 512` it reports that 512 px/m doesn't fit 1024 and that about 2048 holds it.
- `--tight`: 62% at 505 px/m, 15% more texels per metre, in 6.6 s: two packs of about 3 s, the second after four faces the unwrap had folded over others got islands of their own. `--density 500` holds 500 px/m; at 510 it reports that 510 doesn't fit. Blender's shape packer (CONCAVE), which `--tight` used before, packs the same 34 islands to 50% at 454 px/m in 74 s.
- Bake, which packs this way, used 68% of a 2048 texture, unwrapping in 5.9 s.

Packing alone, the same islands for each packer, as texel density and share of the texture used, with the time when it is not instant:

| Islands | Default (boxes) | CONCAVE | `--tight` |
|---|---|---|---|
| Crate, 40 (folds split off), 1024 px, 8 px padding | 412 px/m, 41% | 420 px/m, 43%, 70–76 s | 507 px/m, 62%, 2.7 s |
| Barrel, 40 | 411 px/m, 39% | 438 px/m, 44%, 24 s | 480 px/m, 53%, 3.7 s |
| Case of 49 parts, 351 | 479 px/m, 45% | 492 px/m, 48%, 13–17 s | 598 px/m, 70%, 4.7–5.0 s |
| Crate, 2048 px, 6 px padding | 951 px/m, 55% | the default's, 50–52 s | 1049 px/m, 67%, 3.4 s |
| Barrel | 856 px/m, 42% | 960 px/m, 53%, 12–13 s | 1036 px/m, 62%, 4.2 s |
| Case of 49 parts | 1098 px/m, 59% | the default's, 12–16 s | 1264 px/m, 78%, 7.0–7.2 s |

The default leaves twice the padding between islands; `--tight` leaves the padding (8.03 to 8.96 px at 1024 and 6.01 to 6.93 px at 2048, as the least distance between island outlines) and the padding at the edge, as the default does. Every pack gave the same UVs on a second run. `--tight` gains most where islands enclose room, which other islands fill (strips in the crate's band frames and the barrel's hoop rings; at 2048 the barrel's ends inside its rings), and on many small parts. It gains nothing where one island spans the texture: a rock whose main island is as tall as the texture, or a cylinder's side strip (both within 0.3% of the default's density). Its time is bounded by the search budget: 3 to 6 s at 1024 and 4 to 8 s at 2048, from 25 to 351 islands.

A hand-made cylinder unwrapped live from Anim Review was cut open automatically into 3 islands, at exactly 512 px/m.

## Check it

`check` reads the `uv` entry of the build report. Errors: an atlas or Lightmap that overlaps or leaves 0..1, and a Lightmap that isn't the second map. Warnings: uneven density, and a density off the one asked for.

`preview --uv` renders the six views with a checker on UV0. Even, square checks mean no distortion. Letters running backwards mean a flipped island. A seam shows as a break in the checks, so look for ones across a visible face. It also writes `<name>_UVMap_layout.png` (and `<name>_Lightmap_layout.png`) with each island in its own colour.

## Limits

- Atlas texture use depends on the shapes: about 47% on the example crate (62% with `--tight`), 38% on a curved sword with hundreds of strips. Straightening strips by hand would raise it.
- `--tight` still needs long strips cut (step 4): uncut, the barrel's hoop strips are longer than the texture is wide, and it packs to 412 px/m at 1024 instead of 480.
- `--tight` turns islands only by quarter turns and never mirrors them. Mirroring was tried: it lost texture use on the crate and barrel (up to 3 points) and gained 1.2 points on the case for 70% more time.
- Trim mode lays each strip from its own unwrap. Strips that should line up with a trim's detail across the strip (a bevel on the band's edge) need their seams chosen by hand in the model script.
- Tiling mode projects along the main axis, so faces at about 45° stretch the texture by up to 41% along one axis.
- Hard edges below 70° are not made seams, so a normal-map bake can show seams there. The skill's own bake has no normal map.
