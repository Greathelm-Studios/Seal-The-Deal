# Baking

`node scripts/mdl.mjs bake --blend <dir>/<name>.blend --out <dir>/baked/<name>.blend [--size 2048]`

Turns a built model's flat, named materials into one textured material with a stylized, hand-painted look (in the spirit of Roblox or Deepwoken items, not realistic PBR). The input .blend is not changed. Run it after check and preview pass on the build; then check, preview and export the baked .blend as you would a build.

## What it writes

Beside `--out`:

| File | Holds |
|---|---|
| `<name>.blend` | The model with one material, `<name>_Baked`, on every visible mesh (LOD0..LODn); colliders keep none |
| `<name>_BaseColor.png` | sRGB colour |
| `<name>_MetallicSmoothness.png` | Linear. R, G and B are metallic, A is smoothness (URP Lit's Metallic Alpha) |
| `<name>_Emission.png` | sRGB glow colour, only when a source material emits |
| `<name>.build.json` | The build's report with the baked objects and a `textures` entry (files, size, UV use, emission strength) |

## The look

Per source material, from its Principled BSDF's base colour, metallic, roughness and emission (linked inputs such as an image are followed, so a `texture()` material bakes its texture under the effects):

- worn convex edges mixed 55% of the way toward white (a clear highlight on dark metal, a smaller one on light colours), about 6 mm wide (Bevel node normal against the shading normal, kept to convex edges with Ambient Occlusion), a little smoother and half as metallic, so the painted highlight shows rather than a reflection
- crevices and joins up to 65% darker (Ambient Occlusion, 2.5 cm)
- faces toward +Z up to 30% lighter, toward -Z up to 30% darker
- dark metals (base colour value under about 0.6, weighted by how metallic they are) up to 75% less metallic, up to 0.3 rougher and mixed 6% toward white, so they read as painted charcoal rather than a black mirror that drowns the paint
- low-frequency noise of about 5% in value and a touch of hue, so flat areas are not dead flat
- emissive materials get less of the above and keep their glow in the emission map

Value changes are in sRGB terms, so they read as the percentages say. The amounts are named constants at the top of `scripts/bake.py`.

## How

1. A model unwrapped first with `mdl.mjs unwrap` in atlas mode is baked on its own atlas, which keeps the texel density and any `Lightmap` map it was given; see [UV unwrapping](uv-unwrapping.md). Otherwise every visible mesh that is not LOD1+ gets a new UV map, `BakeUV`, as its only one (the box-mapped UVs are dropped, so Unity cannot take them for lightmap UVs). It is unwrapped the way an artist would: seams on edges sharper than 70 degrees and on material borders; each seam region cut open into a disc (a seam joining a tube's two ends, or across a sphere); then an angle-based unwrap. Islands that come out stretched get seams at 50, 35 and then 25 degrees and are unwrapped again, and any still stretched fall back to Smart UV Project. Bevel strips stay on the faces they round off, so no seam runs along a worn edge. Each island is then scaled to the same texel density, turned to lie along U or V and packed by its exact shape, as `unwrap --tight` packs (see [UV unwrapping](uv-unwrapping.md#tight-packing)), with a 6 px gap at 2048 between islands and at the edge; any faces that still overlap are given islands of their own and repacked. The bake prints the share of the texture used, the island count, the share of the surface within 20% of the median texel density and any pixels still shared; the build report keeps them under `textures`.
2. Cycles on the CPU, 32 samples, bakes each map through a temporary Emission shader, with LOD1+ and colliders hidden so their rays do not hit copies of the model. Margins are 16 px at 2048.
3. LOD1+ are decimated again from the unwrapped LOD0 to their old triangle counts, and use the same textures. Each LOD face then takes its UVs from one LOD0 island (the one nearest its centre, continued past its border on the nearest triangle's plane), so a collapse across a UV seam no longer squeezes or smears it over the gap between islands.

Measured on Blender 5.2.2 (CPU) at 2048: a 3.3k-triangle longsword in 50 to 70 s, a 13.8k-triangle rifle with six materials in 65 to 95 s (the slower figures with another bake running).

## Limits

- LOD UVs: a LOD face that spans a sharp crease takes its UVs from one side, so it is squeezed in one direction. Measured: about 1.5% of the longsword's LOD1 and LOD2 surface and 3% of the rifle's at half the texel density or less in some direction (before the reprojection: 47% and 3% at LOD1).
- One emission strength for the model: each material's glow is scaled into the map relative to the strongest, and the material (and Unity's emission intensity) uses the strongest.
- Texture use depends on the shapes: about 38% for the Stormseye (about 530 islands, many of them long curved strips that do not nest) with the packer bake used before exact-shape packing, which straightening strips by hand would raise. Raise `--size` when close-ups look soft.
- Metallic surfaces still show the baked colour partly as a tint on reflections, so the edges and cavities read most strongly on non-metals and on the damped dark metals.
- Topology is baked as built: boolean unions leave sliver triangles (`check` warns above 5%). Triangulating and evening them out before or after the unwrap was tried and either cut texture use or left zero-area triangles, so it is not done.
