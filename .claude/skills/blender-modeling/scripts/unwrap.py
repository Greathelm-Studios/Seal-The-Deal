"""UV-unwrap a built model for texturing, as a game artist would. See references/uv-unwrapping.md.

blender -b --factory-startup -P unwrap.py -- --blend built.blend --out <dir>/name.blend
    [--mode atlas|tiling|trim] [--density PX_PER_M] [--size 1024] [--padding PX] [--lightmap]
    [--trim Material=v0:v1,...] [--tight]

Reads the output of build (the input is not changed) and writes a new .blend at --out, and its .build.json with a
`uv` entry, so check, preview, bake and export read it like a build. Modes:
  atlas   (default) unique, non-overlapping UVs in 0..1: seams on hard edges and material borders, regions cut open,
          angle-based unwrap, one texel density, islands axis-aligned and packed with a gap for the texture size
  tiling  world-scale UVs for tiling textures, one texture repeat per size / density metres; overlaps are intended
  trim    each --trim material's faces as strips stacked in its V band of a trim sheet, tiling along U; other
          materials tiling
--density is pixels per metre (512 third person, 1024 first person). Without it an atlas fills the texture and the
density it reaches is reported; tiling and trim need it. --padding is the gap in pixels (default by --size: 4 at 512,
8 at 1024, 16 at 2048, 32 at 4096): round each island's box, so twice it between two, or with --tight exactly it
between islands and at the edge. --lightmap adds a second map, Lightmap, non-overlapping for Unity's baked lighting
(UV1), so Generate Lightmap UVs can stay off. --tight packs each island by its exact shape at the texture's pixels,
nesting islands into each other's gaps and holes (a few seconds rather than at once); it fills more of the texture,
most on frames, rings and many small parts. LOD1+ take their UVs from LOD0.
"""
import json
import os
import sys
import time

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import uvkit  # noqa: E402
from mdl_common import fail, mesh_objects, open_blend, parse, write_json  # noqa: E402

MODES = ('atlas', 'tiling', 'trim')
SIZES = (256, 512, 1024, 2048, 4096, 8192)


def main():
    o = parse({'blend': None, 'out': None, 'mode': 'atlas', 'density': None, 'size': '1024', 'padding': None,
               'trim': None}, switches=('lightmap', 'tight'), required=('blend', 'out'))
    mode, size = o['mode'], int(o['size'])
    if mode not in MODES:
        fail(f"--mode is {mode}; use one of {', '.join(MODES)}")
    if size not in SIZES:
        fail(f"--size is {size}; use a power of two from {SIZES[0]} to {SIZES[-1]}")
    density = float(o['density']) if o['density'] else None
    if density is not None and density <= 0:
        fail('--density must be above 0 pixels per metre')
    if mode in ('tiling', 'trim') and density is None:
        fail(f'--mode {mode} needs --density (pixels per metre), which sets how often the texture repeats')
    bands = {}
    if mode == 'trim':
        if not o['trim']:
            fail('--mode trim needs --trim Material=v0:v1,..., the V band of each material on the trim sheet')
        try:
            bands = uvkit.parse_bands(o['trim'])
        except ValueError as e:
            fail(str(e))
    padding = int(o['padding']) if o['padding'] else uvkit.gutter_px(size)
    gap = padding / size
    uv_per_m = density / size if density else None
    shape = 'EXACT' if o.get('tight') else 'AABB'

    src_blend, out = os.path.abspath(o['blend']), os.path.abspath(o['out'])
    if os.path.normcase(src_blend) == os.path.normcase(out):
        fail('--out must be a new file; unwrap does not overwrite the built model')
    open_blend(src_blend)
    t0 = time.time()
    shown = [ob for ob in mesh_objects() if not ob.get('mdl_collider') and not ob.name.endswith('_Collider')]
    targets = [ob for ob in shown if not ob.get('mdl_lod')]
    lods = [ob for ob in shown if ob.get('mdl_lod')]
    if not targets:
        fail('No visible mesh to unwrap')
    materials = {m.name for ob in targets for m in ob.data.materials if m}
    missing = sorted(set(bands) - materials)
    if missing:
        fail(f"--trim names {', '.join(missing)}, which no mesh uses; materials: {', '.join(sorted(materials))}")

    entry = {'mode': mode, 'map': uvkit.UV_MAP, 'size': size, 'padding_px': padding, 'density_target': density}
    if mode == 'atlas':
        result = uvkit.unwrap(targets, gap, uv_per_m=uv_per_m, shape=shape, size=size)
        islands, even, median = uvkit.uv_stats(targets)
        entry.update(used=round(result['used'], 3), shared_px_1024=result['shared'], islands=islands,
                     density_within_20pct=round(even, 3), density_px_per_m=round(median * size, 1),
                     density_fits=result['fits'])
    elif mode == 'tiling':
        uvkit.tiling(targets, uv_per_m)
        entry['repeat_m'] = round(size / density, 4)
    else:
        squeezed = uvkit.trim(targets, bands, uv_per_m, gap)
        entry.update(bands={m: list(b) for m, b in bands.items()}, repeat_m=round(size / density, 4),
                     strips_scaled_to_fit=squeezed)
    if o.get('lightmap'):
        entry['lightmap_shared_px_1024'] = uvkit.lightmap(targets, shape=shape)
    entry['lightmap'] = bool(o.get('lightmap'))
    entry['maps'] = [layer.name for layer in targets[0].data.uv_layers]

    for ob in lods:
        lod0 = bpy.data.objects.get(ob.name.rsplit('_LOD', 1)[0] + '_LOD0')
        if lod0 is None or lod0 not in targets:
            fail(f'{ob.name} has no matching _LOD0 to take its UVs from')
        uvkit.reproject_all(ob.data, lod0)

    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=out, check_existing=False)
    src_report = os.path.splitext(src_blend)[0] + '.build.json'
    report = {}
    if os.path.isfile(src_report):
        with open(src_report, encoding='utf-8') as f:
            report = json.load(f)
    report['uv'] = entry
    report['unwrapped_from'] = src_blend
    write_json(os.path.splitext(out)[0] + '.build.json', report)

    print(f"UV map {uvkit.UV_MAP}: {mode}, {size} px texture, {padding} px between islands")
    if mode == 'atlas':
        held = (f"{density:g} px/m as asked" if result['fits'] else
                f"{entry['density_px_per_m']:g} px/m: {density:g} px/m does not fit a {size} px texture "
                f"(about {needed_size(density, entry['density_px_per_m'], size)} px would hold it)") if density else \
            f"{entry['density_px_per_m']:g} px/m (filling the texture)"
        print(f"  {entry['used'] * 100:.0f}% of the texture used, {islands} islands, {even * 100:.0f}% of the surface "
              f"within 20% of the median density, {held}"
              + (f", {result['shared']} pixels (of 1024 x 1024) still shared by two faces" if result['shared']
                 else ', no overlaps'))
    else:
        print(f"  {density:g} px/m: the texture repeats every {entry['repeat_m']:g} m")
        for m, n in entry.get('strips_scaled_to_fit', {}).items():
            if n:
                print(f"  {m}: {n} strips taller than their band were scaled down to fit it")
    if entry['lightmap']:
        shared = entry['lightmap_shared_px_1024']
        print(f"UV map {uvkit.LIGHTMAP} (Unity UV1): " + (f'{shared} pixels shared by two faces' if shared else 'no overlaps'))
    print(f'unwrap {time.time() - t0:.1f} s. Saved {out}. Next: check, preview --uv, then bake or export.')


def needed_size(density, reached, size):
    """The smallest power-of-two texture that would hold the islands at `density` px/m."""
    want = size * density / reached
    return next((s for s in SIZES if s >= want), SIZES[-1])


main()
