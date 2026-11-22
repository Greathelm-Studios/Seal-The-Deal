"""Bake a built model's materials into stylized, hand-painted-looking textures for Unity URP.

blender -b --factory-startup -P bake.py -- --blend built.blend --out <dir>/name.blend [--size 2048]

Reads the output of build (the input is not changed) and writes a new .blend at --out with, beside it:
  <name>_BaseColor.png          sRGB: each source material's colour with lighter worn edges, darker
                                cavities, a soft top-down light gradient and low-frequency noise
  <name>_MetallicSmoothness.png linear: R (and G, B) = metallic, A = smoothness (URP Lit, Source = Metallic Alpha);
                                worn edges a little smoother than flat areas
  <name>_Emission.png           sRGB, only when a source material emits
  <name>.build.json             the input's build report, with the baked objects and a `textures` entry
Every visible mesh gets a new non-overlapping UV map (the only one: the box-mapped UVs are dropped, so Unity can
not mistake them for lightmap UVs), is baked with Cycles into one shared atlas, and then holds one material,
<name>_Baked, using the textures. A model already given an atlas by `mdl.mjs unwrap` keeps it (its texel density
and any Lightmap map with it). LOD1+ are decimated again from the unwrapped LOD0, so they carry its UVs
(a little squeezed where the collapse crosses a seam) and share the same textures. Colliders keep no material.
"""
import json
import os
import sys
import time

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import numpy as np  # noqa: E402  (ships with Blender)
import modelkit  # noqa: E402
import uvkit  # noqa: E402
from mdl_common import fail, mesh_objects, open_blend, parse, round3, tri_count, world_bounds, write_json  # noqa: E402
from uvkit import reproject_all, select_only, unwrap, uv_stats  # noqa: E402

# The look. Value changes are in sRGB terms (what a colour picker shows); the graph converts them to linear.
# The amounts are strong on purpose: at game distance, and under a metal's reflections, subtle wear disappears.
EDGE_RADIUS = 6e-3       # m: how far the Bevel node rounds an edge, about the width of the worn band
EDGE_DOT = (0.975, 0.9)  # dot(bevelled normal, normal): wear starts at the first (above it smooth shading on a
                         # faceted curve would read as an edge) and is full at the second
EDGE_LIFT = 0.55         # worn edges mixed this far toward white (a clear highlight on near-black, a smaller one
                         # on light colours), which also desaturates them
EDGE_SMOOTHER = 0.25     # and their roughness this much lower
EDGE_METAL_DROP = 0.5    # and their metallic this much lower, so the painted highlight shows, not a reflection
CAVITY_DIST = 0.025      # m: Ambient Occlusion distance
CAVITY_AO = (0.97, 0.5)  # AO at which darkening starts, and at which it is full
CAVITY_DARKEN = 0.65     # fully occluded crevices and joins this much darker
LIGHT_GRADIENT = 0.3     # faces toward +Z this much lighter, toward -Z this much darker
NOISE_SIZE = 0.12        # m: rough size of the low-frequency blotches
NOISE_VALUE = 0.05       # +- value variation
NOISE_HUE = 0.012        # +- hue variation (fraction of the colour wheel)
DARK_METAL = (0.6, 0.3)  # sRGB value of a metal's base colour where it starts to count as dark, and is fully dark
DARK_METAL_DAMP = 0.75   # dark metals' metallic this much lower: their reflections otherwise drown the paint
DARK_METAL_ROUGH = 0.3   # and their roughness this much higher (times their metallic)
DARK_METAL_LIFT = 0.06   # and their colour mixed this far toward white (sRGB), so near-black has room to show paint
EMISSIVE_DETAIL = 0.4    # emissive materials get this fraction of the edge, cavity and gradient effects
SRGB_GAMMA = 2.2
SAMPLES = 32
MARGIN_PX_PER_1K = 8     # bake margin in pixels per 1024 of texture size
ISLAND_GAP_PX = 6        # gap between UV islands, in pixels, so mipmaps do not bleed

UV_NAME = 'BakeUV'


# ---------------------------------------------------------------- node helpers


class Graph:
    def __init__(self, tree):
        self.t = tree

    def node(self, kind, **props):
        n = self.t.nodes.new(kind)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def feed(self, sock, val):
        if isinstance(val, bpy.types.NodeSocket):
            self.t.links.new(val, sock)
        else:
            sock.default_value = val

    def math(self, op, a, b=0.0, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.feed(n.inputs[0], a)
        self.feed(n.inputs[1], b)
        return n.outputs[0]

    def mul(self, *xs):
        out = xs[0]
        for x in xs[1:]:
            out = self.math('MULTIPLY', out, x)
        return out

    def ramp(self, val, lo, hi, to_lo=0.0, to_hi=1.0, smooth=True):
        """Map Range from [lo, hi] to [to_lo, to_hi], clamped (smoothstep when smooth)."""
        n = self.node('ShaderNodeMapRange', interpolation_type='SMOOTHSTEP' if smooth else 'LINEAR', clamp=True)
        self.feed(n.inputs['Value'], val)
        n.inputs['From Min'].default_value, n.inputs['From Max'].default_value = lo, hi
        n.inputs['To Min'].default_value, n.inputs['To Max'].default_value = to_lo, to_hi
        return n.outputs['Result']

    def plus_one(self, amount, signal):
        """1 + amount * signal."""
        return self.math('ADD', self.math('MULTIPLY', signal, amount), 1.0)


def source(sock):
    """What drives a socket: the linked output socket, or its own value."""
    return sock.links[0].from_socket if sock.is_linked else sock.default_value


def same_socket(value, tree):
    """The output socket in `tree`, a copy of the socket's own node tree, that matches `value`; other values as
    they are."""
    if isinstance(value, bpy.types.NodeSocket):
        return tree.nodes[value.node.name].outputs[value.identifier]
    return value


def read_material(mat):
    """The source material's base colour, metallic, roughness and emission (linear), from its Principled BSDF."""
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if mat and mat.node_tree else None
    if bsdf is None:
        c = tuple(mat.diffuse_color) if mat else (0.8, 0.8, 0.8, 1.0)
        return {'base': c, 'metal': mat.metallic if mat else 0.0, 'rough': mat.roughness if mat else 0.5,
                'emit': (0, 0, 0, 1), 'strength': 0.0}
    i = bsdf.inputs
    strength = i['Emission Strength'].default_value if not i['Emission Strength'].is_linked else 1.0
    emit = source(i['Emission Color'])
    emits = strength > 0 and (isinstance(emit, bpy.types.NodeSocket) or max(emit[:3]) > 0)
    return {'base': source(i['Base Color']), 'metal': source(i['Metallic']), 'rough': source(i['Roughness']),
            'emit': emit if emits else (0, 0, 0, 1), 'strength': strength if emits else 0.0}


def bake_material(src, info, max_strength):
    """A copy of src whose output is an Emission shader; returns it with the three signals it can emit
    (stylized colour, metallic/smoothness, emission) and its target image node."""
    mat = src.copy() if src else bpy.data.materials.new('_mdl_bake')
    mat.name = f'_mdl_bake_{src.name if src else "none"}'
    if not mat.node_tree:
        mat.use_nodes = True
    # info's linked inputs (an image texture's output, say) are sockets of src's node tree. A link from another
    # tree is dropped without an error, leaving the input at its plain default, so use the copy's own sockets.
    info = {key: same_socket(value, mat.node_tree) for key, value in info.items()}
    g = Graph(mat.node_tree)
    k = EMISSIVE_DETAIL if info['strength'] > 0 else 1.0

    geo = g.node('ShaderNodeNewGeometry')
    normal = geo.outputs['Normal']
    bevel = g.node('ShaderNodeBevel', samples=8)
    bevel.inputs['Radius'].default_value = EDGE_RADIUS
    dot = g.node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
    g.feed(dot.inputs[0], bevel.outputs['Normal'])
    g.feed(dot.inputs[1], normal)
    edge = g.ramp(dot.outputs['Value'], *EDGE_DOT)
    ao = g.node('ShaderNodeAmbientOcclusion', samples=16, only_local=False, inside=False)
    ao.inputs['Distance'].default_value = CAVITY_DIST
    occl = ao.outputs['AO']
    wear = g.mul(edge, g.ramp(occl, 0.8, 0.97))        # convex edges only: concave ones are occluded
    cavity = g.ramp(occl, *CAVITY_AO)

    nz = g.node('ShaderNodeSeparateXYZ')
    g.feed(nz.inputs[0], normal)
    coords = g.node('ShaderNodeTexCoord')
    noise = g.node('ShaderNodeTexNoise', noise_dimensions='3D')
    g.feed(noise.inputs['Vector'], coords.outputs['Object'])
    noise.inputs['Scale'].default_value = 1.0 / NOISE_SIZE
    noise.inputs['Detail'].default_value = 2.0
    chans = g.node('ShaderNodeSeparateColor')
    g.feed(chans.inputs[0], noise.outputs['Color'])
    n_val = g.ramp(chans.outputs[0], 0.3, 0.7, -1.0, 1.0, smooth=False)
    n_hue = g.ramp(chans.outputs[1], 0.3, 0.7, -1.0, 1.0, smooth=False)

    # How dark-and-metallic the base is: such surfaces get a lifted colour and less mirror-like shading.
    base_hsv = g.node('ShaderNodeSeparateColor', mode='HSV')
    g.feed(base_hsv.inputs[0], info['base'])
    dark = g.mul(g.ramp(g.math('POWER', base_hsv.outputs[2], 1.0 / SRGB_GAMMA), *DARK_METAL), info['metal'])

    value = g.mul(g.plus_one(LIGHT_GRADIENT * k, nz.outputs['Z']),
                  g.plus_one(-CAVITY_DARKEN * k, cavity),
                  g.plus_one(NOISE_VALUE, n_val))
    hsv = g.node('ShaderNodeHueSaturation')
    g.feed(hsv.inputs['Hue'], g.math('ADD', g.math('MULTIPLY', n_hue, NOISE_HUE), 0.5))
    g.feed(hsv.inputs['Value'], g.math('POWER', value, SRGB_GAMMA))
    g.feed(hsv.inputs['Color'], info['base'])
    # Worn edges: mixed toward white in sRGB, so the lift shows as clearly on near-black as on mid tones.
    to_srgb = g.node('ShaderNodeGamma')
    g.feed(to_srgb.inputs['Color'], hsv.outputs[0])
    to_srgb.inputs['Gamma'].default_value = 1.0 / SRGB_GAMMA
    floor = g.node('ShaderNodeMix', data_type='RGBA', blend_type='MIX')
    g.feed(floor.inputs['Factor'], g.math('MULTIPLY', dark, DARK_METAL_LIFT))
    g.feed(floor.inputs['A'], to_srgb.outputs[0])
    floor.inputs['B'].default_value = (1, 1, 1, 1)
    lift = g.node('ShaderNodeMix', data_type='RGBA', blend_type='MIX')
    g.feed(lift.inputs['Factor'], g.math('MULTIPLY', wear, EDGE_LIFT * k))
    g.feed(lift.inputs['A'], floor.outputs['Result'])
    lift.inputs['B'].default_value = (1, 1, 1, 1)
    color = g.node('ShaderNodeGamma')
    g.feed(color.inputs['Color'], lift.outputs['Result'])
    color.inputs['Gamma'].default_value = SRGB_GAMMA

    # Dark metals: less metallic and rougher, so the baked colour reads instead of the environment.
    metal = g.mul(info['metal'], g.plus_one(-DARK_METAL_DAMP, dark), g.plus_one(-EDGE_METAL_DROP, wear))
    rough = g.math('ADD', info['rough'], g.mul(dark, DARK_METAL_ROUGH))
    rough = g.mul(rough, g.plus_one(-EDGE_SMOOTHER, wear))
    ms = g.node('ShaderNodeCombineColor')
    g.feed(ms.inputs[0], metal)
    g.feed(ms.inputs[1], g.math('SUBTRACT', 1.0, rough, clamp=True))
    g.feed(ms.inputs[2], 0.0)

    em = g.node('ShaderNodeVectorMath', operation='SCALE')
    g.feed(em.inputs[0], info['emit'] if isinstance(info['emit'], bpy.types.NodeSocket) else info['emit'][:3])
    g.feed(em.inputs['Scale'], info['strength'] / max_strength if max_strength else 0.0)

    out = g.node('ShaderNodeOutputMaterial', target='CYCLES')
    for n in mat.node_tree.nodes:
        if n.type == 'OUTPUT_MATERIAL':
            n.is_active_output = n == out
    shader = g.node('ShaderNodeEmission')
    shader.inputs['Strength'].default_value = 1.0
    g.feed(out.inputs['Surface'], shader.outputs[0])
    target = g.node('ShaderNodeTexImage')
    mat.node_tree.nodes.active = target
    return {'mat': mat, 'g': g, 'shader': shader, 'target': target,
            'signals': {'color': color.outputs[0], 'ms': ms.outputs[0], 'emit': em.outputs[0]}}


# ---------------------------------------------------------------- steps


def new_image(name, size, data):
    img = bpy.data.images.new(name, size, size, alpha=True, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color' if data else 'sRGB'
    return img


def bake_pass(obs, bakes, signal, img, margin):
    for b in bakes:
        b['g'].feed(b['shader'].inputs['Color'], b['signals'][signal])
        b['target'].image = img
    select_only(obs)
    bpy.ops.object.bake(type='EMIT', margin=margin, margin_type='ADJACENT_FACES', use_clear=True,
                        target='IMAGE_TEXTURES')


def save_png(img, path):
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    img.filepath = bpy.path.relpath(path)
    img.source = 'FILE'
    img.reload()


def baked_material(name, imgs, max_strength):
    mat = bpy.data.materials.new(name)
    if not mat.node_tree:
        mat.use_nodes = True
    t = mat.node_tree
    bsdf = next(n for n in t.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (1, 1, 1, 1)  # Unity multiplies the map by this
    bsdf.inputs['Metallic'].default_value = 1.0
    bsdf.inputs['Roughness'].default_value = 1.0

    def tex(img, y):
        n = t.nodes.new('ShaderNodeTexImage')
        n.image, n.location = img, (-500, y)
        return n

    t.links.new(tex(imgs['BaseColor'], 300).outputs['Color'], bsdf.inputs['Base Color'])
    ms = tex(imgs['MetallicSmoothness'], 0)
    t.links.new(ms.outputs['Color'], bsdf.inputs['Metallic'])  # R = G = B = metallic
    inv = t.nodes.new('ShaderNodeMath')
    inv.operation, inv.location = 'SUBTRACT', (-200, -100)
    inv.inputs[0].default_value = 1.0
    t.links.new(ms.outputs['Alpha'], inv.inputs[1])
    t.links.new(inv.outputs[0], bsdf.inputs['Roughness'])
    if 'Emission' in imgs:
        t.links.new(tex(imgs['Emission'], -300).outputs['Color'], bsdf.inputs['Emission Color'])
        bsdf.inputs['Emission Strength'].default_value = max_strength
    mat['mdl_baked'] = True
    return mat


def remake_lods(lod0, lods):
    """Decimate the unwrapped, rematerialled LOD0 again for each LOD, to the triangle share it had, so the LODs
    use the same textures, then take each face's UVs from one LOD0 island (reproject_uvs)."""
    base_tris = tri_count(lod0.data)
    for ob in lods:
        tmp = bpy.data.objects.new('_mdl_lod', lod0.data)
        bpy.context.scene.collection.objects.link(tmp)
        mod = tmp.modifiers.new('decimate', 'DECIMATE')
        mod.ratio = tri_count(ob.data) / base_tris
        me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        bpy.data.objects.remove(tmp, do_unlink=True)
        modelkit._drop_collapsed_pieces(me, lod0.data)
        reproject_all(me, lod0)
        old = ob.data
        name = old.name
        ob.data = me
        bpy.data.meshes.remove(old)
        me.name = name


def main():
    o = parse({'blend': None, 'out': None, 'size': '2048'}, required=('blend', 'out'))
    size = int(o['size'])
    src_blend, out = os.path.abspath(o['blend']), os.path.abspath(o['out'])
    if os.path.normcase(src_blend) == os.path.normcase(out):
        fail('--out must be a new file; the bake does not overwrite the built model')
    open_blend(src_blend)
    name = os.path.splitext(os.path.basename(out))[0]
    out_dir = os.path.dirname(out)
    os.makedirs(out_dir, exist_ok=True)
    # Save first so the textures can be stored relative to the new .blend.
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=out, check_existing=False)
    t0 = time.time()

    shown = [ob for ob in mesh_objects() if not ob.get('mdl_collider') and not ob.name.endswith('_Collider')]
    targets = [ob for ob in shown if not ob.get('mdl_lod')]
    lods = [ob for ob in shown if ob.get('mdl_lod')]
    if not targets:
        fail('No visible mesh to bake')
    lod0_of = {}
    for ob in lods:
        lod0_of[ob] = bpy.data.objects.get(ob.name.rsplit('_LOD', 1)[0] + '_LOD0')
        if lod0_of[ob] not in targets:
            fail(f'{ob.name} has no matching _LOD0 to take its UVs from')

    infos = {}
    for ob in targets:
        for m in ob.data.materials:
            if m and m.name not in infos:
                infos[m.name] = (m, read_material(m))
    max_strength = max((i['strength'] for _, i in infos.values()), default=0.0)
    emissive = sorted(n for n, (_, i) in infos.items() if i['strength'] > 0)

    src_report = os.path.splitext(src_blend)[0] + '.build.json'
    report = {}
    if os.path.isfile(src_report):
        with open(src_report, encoding='utf-8') as f:
            report = json.load(f)
    unwrapped = report.get('uv', {})
    if unwrapped.get('mode') == 'atlas':
        # mdl.mjs unwrap already gave the model a unique atlas at the density it was asked for: bake onto it.
        uv_name = targets[0].data.uv_layers[0].name
        for ob in targets:
            ob.data.uv_layers.active = ob.data.uv_layers[0]
        uv_area, uv_shared = uvkit.uv_area(targets), unwrapped.get('shared_px_1024', 0)
    else:
        uv_name = UV_NAME
        result = unwrap(targets, ISLAND_GAP_PX / size, name=UV_NAME, shape='EXACT', size=size)
        uv_area, uv_shared = result['used'], result['shared']
        if unwrapped.get('lightmap'):
            uvkit.lightmap(targets, shape='EXACT')
    uv_islands_n, uv_even, _ = uv_stats(targets)
    t_uv = time.time()

    bakes, by_name = [], {}
    for ob in targets:
        if not ob.data.materials:
            ob.data.materials.append(None)
        for i, m in enumerate(ob.data.materials):
            key = m.name if m else None
            if key not in by_name:
                by_name[key] = bake_material(m, infos[key][1] if m else read_material(None), max_strength)
                bakes.append(by_name[key])
            ob.data.materials[i] = by_name[key]['mat']

    sc = bpy.context.scene
    # LOD1+ and colliders sit on top of LOD0: hidden, or the cavity and edge rays would hit them.
    hidden = [ob for ob in sc.objects if ob not in targets and not ob.hide_render]
    for ob in hidden:
        ob.hide_render = True
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = SAMPLES
    sc.cycles.use_denoising = False
    margin = max(2, round(MARGIN_PX_PER_1K * size / 1024))

    imgs = {}
    color = new_image(f'{name}_BaseColor', size, data=False)
    bake_pass(targets, bakes, 'color', color, margin)
    imgs['BaseColor'] = color
    raw = new_image('_mdl_ms', size, data=True)
    bake_pass(targets, bakes, 'ms', raw, margin)
    px = np.empty(size * size * 4, dtype=np.float32)
    raw.pixels.foreach_get(px)
    px = px.reshape(-1, 4)
    packed = np.stack([px[:, 0], px[:, 0], px[:, 0], px[:, 1]], axis=1)
    ms = new_image(f'{name}_MetallicSmoothness', size, data=True)
    ms.alpha_mode = 'CHANNEL_PACKED'  # alpha is smoothness, not coverage
    ms.pixels.foreach_set(packed.ravel())
    bpy.data.images.remove(raw)
    imgs['MetallicSmoothness'] = ms
    if emissive:
        em = new_image(f'{name}_Emission', size, data=False)
        bake_pass(targets, bakes, 'emit', em, margin)
        imgs['Emission'] = em
    t_bake = time.time()
    for ob in hidden:
        ob.hide_render = False

    files = {}
    for kind, img in imgs.items():
        path = os.path.join(out_dir, f'{name}_{kind}.png')
        save_png(img, path)
        files[kind] = os.path.basename(path)
    imgs['MetallicSmoothness'].alpha_mode = 'CHANNEL_PACKED'

    final = baked_material(f'{name}_Baked', imgs, max_strength)
    for ob in targets:
        me = ob.data
        me.materials.clear()
        me.materials.append(final)
        me.polygons.foreach_set('material_index', [0] * len(me.polygons))
        me.update()
    for lod0 in set(lod0_of.values()):
        remake_lods(lod0, [ob for ob, base in lod0_of.items() if base is lod0])
    for b in bakes:
        bpy.data.materials.remove(b['mat'])
    for m, _ in infos.values():
        if m.users == 0:
            bpy.data.materials.remove(m)
    sc.render.engine = 'BLENDER_EEVEE'
    bpy.ops.wm.save_as_mainfile(filepath=out, check_existing=False)

    # The build report, so check, preview and export read the result like a build.
    if unwrapped.get('mode') != 'atlas':
        report['uv'] = {'mode': 'atlas', 'map': uv_name, 'lightmap': bool(unwrapped.get('lightmap')), 'size': size,
                        'padding_px': ISLAND_GAP_PX, 'shared_px_1024': uv_shared, 'by': 'bake'}
    by_obj = {e['name']: e for e in report.get('objects', [])}
    for ob in shown:
        lo, hi = world_bounds([ob])
        by_obj.setdefault(ob.name, {'name': ob.name, 'type': 'MESH', 'parent': ob.parent.name if ob.parent else None})
        by_obj[ob.name].update(size_m=round3(hi - lo), tris=tri_count(ob.data), materials=[final.name])
    report['objects'] = sorted(by_obj.values(), key=lambda e: e['name'])
    report['baked_from'] = src_blend
    report['textures'] = {'size': size, 'material': final.name, 'uv_map': uv_name, 'uv_used': round(uv_area, 3),
                          'uv_islands': uv_islands_n, 'uv_density_within_20pct': round(uv_even, 3),
                          'uv_shared_px_1024': uv_shared,
                          'files': files,
                          'emissive_materials': emissive, 'emission_strength': max_strength,
                          'source_materials': sorted(infos)}
    write_json(os.path.splitext(out)[0] + '.build.json', report)

    for ob in shown:
        print(f"{ob.name:28} {tri_count(ob.data):6} tris  {final.name}")
    for kind, f in files.items():
        print(f'  {os.path.join(out_dir, f)}')
    print(f'UV map {uv_name}: {uv_area * 100:.0f}% of the texture used, {uv_islands_n} islands, '
          f'{uv_even * 100:.0f}% of the surface within 20% of the median texel density'
          + (f', {uv_shared} pixels (of 1024 x 1024) still shared by two faces' if uv_shared else ', no overlaps'))
    print(f'unwrap {t_uv - t0:.1f} s, bake {t_bake - t_uv:.1f} s ({len(imgs)} maps at {size} px, {SAMPLES} samples), '
          f'total {time.time() - t0:.1f} s')
    print(f'Saved {out}. Next: check, preview, then export.')


main()
