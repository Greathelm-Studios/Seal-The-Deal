# Poly Haven textures, HDRIs and models

[Poly Haven](https://polyhaven.com) gives away PBR texture sets, HDRIs and models under CC0: no credit needed, any use, commercial included. `mdl.mjs polyhaven` reaches them through Poly Haven's public API with no key or Blender.

```
node scripts/mdl.mjs polyhaven search worn wood planks              # textures by default
node scripts/mdl.mjs polyhaven search sky --type hdris
node scripts/mdl.mjs polyhaven get wood_table_worn --out <dir>/textures --res 1k
```

`search` ranks assets by how many of the words their name, tags and categories hold, and shows each texture's real-world size. `get` downloads into `<dir>/<slug>/`, checks every file against Poly Haven's MD5 and keeps files already there:
- textures: base colour, OpenGL normal (what Blender and Unity use), roughness, metallic when the set has one, and AO
- HDRIs: the `.hdr`
- models: the glTF and its files

`get` also writes `<slug>.json` with the files, the real-world size, the licence and the source page. 1k suits most props; use 2k or more for close-ups and large surfaces.

## In a model script

```python
wood = texture('Wood', r'<dir>/textures/wood_table_worn/wood_table_worn.json')
steel = texture('Steel', r'<dir>/textures/rusty_metal_02/rusty_metal_02.json', scale=0.5)
crate = part('Crate', body, [wood, steel])
```

`texture(name, maps, projection='box', scale=1.0)` builds a material from the set: base colour, roughness, normal and metallic. One repeat covers the set's real-world size times `scale`.

- `projection='box'` (default): maps in object space from the three axes, blended at corners. It looks the same whatever the UVs are, so builds, previews and bakes, which replace the UVs, all match.
- `projection='uv'`: maps through the UV map. Use it with tiling UVs from `mdl.mjs unwrap --mode tiling`.

The FBX carries only the material name and the texture's average colour. For Unity, either bake: `mdl.mjs bake` turns the textures, with the stylized edges, cavities and gradient, into the model's own maps. Or rebuild the material in Unity from the downloaded maps, at a tiling of 1 / size metres.

The preview's flat view shows the average colour; preview the baked model to see the textures.

## Measured

Example crate with `wood_table_worn` and `rusty_metal_02` at 1k:
- Downloading took a few seconds per set, about 0.5–0.7 MB per map.
- Unwrapped first, then baked at 1024 in 15 s.
- The baked sheet shows the wood grain and the rust spots under the stylized wear.

## Terms

Poly Haven asks API users to identify themselves, so requests send a `blender-modeling-skill` User-Agent. Don't scrape the website itself; use the API, as this does. Credit is optional under CC0; the `authors` in `<slug>.json` are there if a project wants it.
