"""Six-view contact sheet of a model, for judging what numbers cannot: proportion, placement, and
parts that look floating, fused or missing.

blender -b --factory-startup -P preview.py -- --blend model.blend --out <dir> [--cell 480x480] [--colliders] [--uv]

Writes <out>/<name>_sheet.png: front, back, left, right, top and three-quarter, orthographic, each
stamped with its view and the model's size. Front is the -Y side (Unity's forward after export).
Colliders and LOD1+ meshes are hidden unless --colliders, which draws colliders as wireframes.
--uv instead draws the model with a checker texture on its first UV map, to <out>/<name>_uv_sheet.png (squares
stretched, sheared or different in size show distortion; letters flipped or turned show flipped islands), and
writes each UV map's layout, islands filled in colours, to <out>/<name>_<map>_layout.png.
--reference [view=]image (repeatable; view is front, back, left, right or top, default front) compares the model's
outline from that view with the reference's: the reference's outline is its alpha, or else everything that differs
from the colour at its border, with enclosed gaps filled on both sides. The reference is scaled to the model's long
side and centred, so the score measures shape and proportion, not framing. Writes <out>/<name>_<view>_reference.png
(the fitted reference beside the difference: grey where both agree, orange where the model has extra, cyan where it
is missing something) and <out>/<name>_<view>_reference.json.
"""
import math
import os
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
import numpy as np  # noqa: E402  (ships with Blender)
from mathutils import Vector  # noqa: E402
import uvkit  # noqa: E402
from mdl_common import fail, mesh_objects, open_blend, parse, world_bounds, write_json  # noqa: E402

VIEWS = [('front', Vector((0, -1, 0))), ('back', Vector((0, 1, 0))), ('left', Vector((-1, 0, 0))),
         ('right', Vector((1, 0, 0))), ('top', Vector((0, 0, 1))),
         ('three-quarter', Vector((-0.75, -1, 0.65)))]


def setup_render(w, h, textured=False):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'
    sh.color_type = 'TEXTURE' if textured else 'MATERIAL'  # a baked model's colour is in its textures
    sh.show_cavity = True
    sh.show_object_outline = True
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.resolution_percentage = 100
    if sc.world is None:
        sc.world = bpy.data.worlds.new('World')
    sc.world.color = (0.16, 0.17, 0.2)
    r = sc.render
    r.use_stamp = True
    for flag in ('date', 'time', 'render_time', 'scene', 'camera', 'lens', 'filename', 'memory',
                 'hostname', 'frame', 'frame_range', 'marker', 'sequencer_strip'):
        if hasattr(r, f'use_stamp_{flag}'):
            setattr(r, f'use_stamp_{flag}', False)
    r.use_stamp_note = True
    r.stamp_font_size = 14
    r.image_settings.file_format = 'PNG'


def compose(paths, cols, w, h, out_path):
    rows = math.ceil(len(paths) / cols)
    sheet = np.zeros((rows * h, cols * w, 4), dtype=np.float32)
    for i, p in enumerate(paths):
        r, c = divmod(i, cols)
        img = bpy.data.images.load(p)
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        y0 = (rows - 1 - r) * h  # Blender images start at the bottom row
        sheet[y0:y0 + h, c * w:(c + 1) * w] = px.reshape(h, w, 4)
        bpy.data.images.remove(img)
    out = bpy.data.images.new(os.path.basename(out_path), cols * w, rows * h, alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = out_path
    out.file_format = 'PNG'
    out.save()
    bpy.data.images.remove(out)


def checker(obs):
    """Puts a colour-grid checker on the meshes' first UV map, for the --uv sheet."""
    img = bpy.data.images.new('mdl_uv_checker', 1024, 1024)
    img.generated_type = 'COLOR_GRID'
    mat = bpy.data.materials.new('mdl_uv_checker')
    if not mat.node_tree:
        mat.use_nodes = True
    tree = mat.node_tree
    tex = tree.nodes.new('ShaderNodeTexImage')
    tex.image = img
    bsdf = next(n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED')
    tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    tree.nodes.active = tex
    for ob in obs:
        me = ob.data
        if me.uv_layers:
            me.uv_layers.active = me.uv_layers[0]
            me.uv_layers[0].active_render = True
        me.materials.clear()
        me.materials.append(mat)
        me.polygons.foreach_set('material_index', [0] * len(me.polygons))


def uv_layout(obs, layer_name, path, res=1024):
    """The UV map drawn flat: each island filled in its own colour, edges in white, the 0..1 square outlined."""
    img = np.zeros((res, res, 4), dtype=np.float32)
    img[..., :3], img[..., 3] = 0.1, 1.0
    rng = np.random.default_rng(7)
    for ob in obs:
        if layer_name not in ob.data.uv_layers:
            continue
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        lay = bm.loops.layers.uv[layer_name]
        bm.faces.ensure_lookup_table()
        for isl in uvkit.uv_islands(bm, lay):
            colour = 0.25 + 0.6 * rng.random(3)
            for f in isl:
                pts = np.array([lp[lay].uv[:] for lp in f.loops]) * res
                for i in range(1, len(pts) - 1):
                    fill_triangle(img, pts[0], pts[i], pts[i + 1], colour)
        for f in bm.faces:
            pts = [np.array(lp[lay].uv[:]) * res for lp in f.loops]
            for a, b in zip(pts, pts[1:] + pts[:1]):
                n = max(2, int(np.abs(b - a).max()) + 1)
                xs = np.clip(np.linspace(a[0], b[0], n).astype(int), 0, res - 1)
                ys = np.clip(np.linspace(a[1], b[1], n).astype(int), 0, res - 1)
                img[ys, xs, :3] = 0.95
        bm.free()
    img[[0, -1], :, :3] = 0.6
    img[:, [0, -1], :3] = 0.6
    out = bpy.data.images.new(os.path.basename(path), res, res, alpha=True)
    out.pixels.foreach_set(img.ravel())
    out.filepath_raw = path
    out.file_format = 'PNG'
    out.save()
    bpy.data.images.remove(out)


def fill_triangle(img, a, b, c, colour):
    res = img.shape[0]
    lo = np.maximum(np.floor(np.minimum(np.minimum(a, b), c)).astype(int), 0)
    hi = np.minimum(np.ceil(np.maximum(np.maximum(a, b), c)).astype(int), res)
    if (hi <= lo).any():
        return
    xs, ys = np.meshgrid(np.arange(lo[0], hi[0]) + 0.5, np.arange(lo[1], hi[1]) + 0.5)
    d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
    if abs(d) < 1e-12:
        return
    l1 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / d
    l2 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / d
    inside = (l1 >= 0) & (l2 >= 0) & (1 - l1 - l2 >= 0)
    img[ys[inside].astype(int), xs[inside].astype(int), :3] = colour


REF_RES = 768  # working size of the outline comparison, in pixels along the long side
REF_VIEWS = {'front': (0, 2), 'back': (0, 2), 'left': (1, 2), 'right': (1, 2), 'top': (0, 1)}  # axes seen


def load_pixels(path):
    img = bpy.data.images.load(os.path.abspath(path))
    w, h = img.size
    if not w or not h:
        fail(f'Could not read image {path}')
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)


def save_pixels(px, path):
    h, w = px.shape[:2]
    out = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    out.pixels.foreach_set(px.astype(np.float32).ravel())
    out.filepath_raw = path
    out.file_format = 'PNG'
    out.save()
    bpy.data.images.remove(out)


def outline(px):
    """The subject's outline as a mask: alpha where the image has transparency, else whatever differs from the
    colour along its border (a plain background)."""
    if px[..., 3].min() < 0.5:
        return px[..., 3] > 0.5
    border = np.concatenate([px[0, :, :3], px[-1, :, :3], px[:, 0, :3], px[:, -1, :3]])
    return np.abs(px[..., :3] - np.median(border, axis=0)).max(axis=-1) > 0.12


def shrink(mask, rgb):
    """Fits a mask (and its image) within REF_RES, keeping thin lines: a pixel is set if any it covers was."""
    k = max(1, math.ceil(max(mask.shape) / REF_RES))
    if k == 1:
        return mask, rgb
    h, w = (math.ceil(d / k) * k for d in mask.shape)
    padded = np.zeros((h, w), dtype=bool)
    padded[:mask.shape[0], :mask.shape[1]] = mask
    return padded.reshape(h // k, k, w // k, k).any(axis=(1, 3)), rgb[::k, ::k]


def grow(seed, allowed):
    """Every pixel of `allowed` connected to `seed`."""
    out = seed & allowed
    while True:
        grown = out.copy()
        grown[1:] |= out[:-1]
        grown[:-1] |= out[1:]
        grown[:, 1:] |= out[:, :-1]
        grown[:, :-1] |= out[:, 1:]
        grown &= allowed
        if np.array_equal(grown, out):
            return out
        out = grown


def solid(mask):
    """Fills gaps the outline encloses, so line art, and parts drawn with gaps between them, read as solid."""
    edge = np.zeros_like(mask)
    edge[0] = edge[-1] = edge[:, 0] = edge[:, -1] = True
    return ~grow(edge, ~mask)


def drop_specks(mask, keep=0.01, limit=2000):
    """Removes pieces under `keep` of the largest one's area: stray text, marks and compression noise."""
    left, parts = mask.copy(), []
    while left.any():
        if len(parts) == limit:
            fail('The reference has thousands of separate specks; give it a plain, clean background')
        seed = np.zeros_like(mask)
        seed[tuple(np.argwhere(left)[0])] = True
        parts.append(grow(seed, left))
        left &= ~parts[-1]
    big = max(p.sum() for p in parts)
    return np.logical_or.reduce([p for p in parts if p.sum() >= big * keep])


def bbox(mask):
    ys, xs = np.nonzero(mask)
    return ys.min(), ys.max() + 1, xs.min(), xs.max() + 1


def resize(a, h, w):
    ys = np.minimum((np.arange(h) + 0.5) * a.shape[0] / h, a.shape[0] - 1).astype(int)
    xs = np.minimum((np.arange(w) + 0.5) * a.shape[1] / w, a.shape[1] - 1).astype(int)
    return a[ys][:, xs]


def reference_check(view, ref_path, shown, centre, size, out_dir, name):
    """Renders the model's outline from one view, fits the reference's outline to it and scores the match."""
    if view not in REF_VIEWS:
        fail(f'--reference view must be one of {", ".join(REF_VIEWS)}, not {view}')
    if not os.path.isfile(ref_path):
        fail(f'Reference image not found: {ref_path}')
    sc = bpy.context.scene
    for ob in bpy.data.objects:
        if ob.type == 'MESH' and ob not in shown:
            ob.hide_render = True
    sh = sc.display.shading
    sh.light, sh.color_type, sh.single_color = 'FLAT', 'SINGLE', (1, 1, 1)
    sh.show_cavity = sh.show_object_outline = False
    sc.render.use_stamp = False
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = REF_RES
    cam = sc.camera
    a, b = REF_VIEWS[view]
    cam.data.ortho_scale = max(size[a], size[b], 1e-4) * 1.1
    d = dict(VIEWS)[view]
    cam.location = centre + d * max(size.length, 1e-3) * 4
    cam.rotation_euler = (0, 0, 0) if view == 'top' else (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
    frame = os.path.join(out_dir, '_views', f'{view}_outline.png')
    sc.render.filepath = frame
    bpy.ops.render.render(write_still=True)
    model = solid(load_pixels(frame)[..., 3] > 0.5)
    ref_px = load_pixels(ref_path)
    ref, ref_rgb = shrink(outline(ref_px), ref_px)
    if not model.any():
        fail(f'The model shows nothing from the {view}')
    if not ref.any():
        fail(f'Found no subject in {ref_path}: give it a plain background or transparency')
    ref = drop_specks(solid(ref))

    # Crop both to their outlines; scale the reference so its side along the model's long side matches.
    my0, my1, mx0, mx1 = bbox(model)
    ry0, ry1, rx0, rx1 = bbox(ref)
    mh, mw, rh, rw = my1 - my0, mx1 - mx0, ry1 - ry0, rx1 - rx0
    s = mw / rw if mw >= mh else mh / rh
    fh, fw = max(1, round(rh * s)), max(1, round(rw * s))
    m = model[my0:my1, mx0:mx1]
    r = resize(ref[ry0:ry1, rx0:rx1], fh, fw)
    rgb = resize(ref_rgb[ry0:ry1, rx0:rx1], fh, fw)
    ch, cw = max(mh, fh) + 16, max(mw, fw) + 16

    def place(arr, fill=0):
        canvas = np.full((ch, cw) + arr.shape[2:], fill, dtype=arr.dtype)
        y, x = (ch - arr.shape[0]) // 2, (cw - arr.shape[1]) // 2
        canvas[y:y + arr.shape[0], x:x + arr.shape[1]] = arr
        return canvas

    M, R = place(m), place(r)
    iou = float((M & R).sum() / (M | R).sum())
    long_px = max(mh, mw)
    short_m, short_r = (mh, fh) if mw >= mh else (mw, fw)
    proportion = float((short_m - short_r) / short_r)
    cm = np.argwhere(M).mean(axis=0)
    cr = np.argwhere(R).mean(axis=0)
    centre_off = float(np.linalg.norm(cm - cr) / long_px)

    diff = np.zeros((ch, cw, 4), dtype=np.float32)
    diff[...] = (0.1, 0.1, 0.12, 1)
    diff[M & R] = (0.75, 0.75, 0.75, 1)
    diff[M & ~R] = (1.0, 0.55, 0.1, 1)  # model has extra
    diff[~M & R] = (0.1, 0.8, 1.0, 1)  # model is missing
    left = place(rgb, 0.0)
    left[..., 3] = 1
    gap = np.ones((ch, 8, 4), dtype=np.float32)
    image = os.path.join(out_dir, f'{name}_{view}_reference.png')
    save_pixels(np.concatenate([left, gap, diff], axis=1), image)
    report = {'view': view, 'reference': os.path.abspath(ref_path), 'outline_iou': round(iou, 4),
              'proportion_error': round(proportion, 4), 'centre_offset': round(centre_off, 4),
              'model_extra_px': int((M & ~R).sum()), 'model_missing_px': int((~M & R).sum())}
    write_json(os.path.join(out_dir, f'{name}_{view}_reference.json'), report)
    side = 'height' if mw >= mh else 'width'
    print(f'{image}\n  {view} against reference: outline match {iou:.2f}, model {side} {proportion:+.1%} '
          f'against the reference, centre of mass off by {centre_off:.1%} of the long side')


def main():
    o = parse({'blend': None, 'out': None, 'cell': '480x480'}, switches=('colliders', 'uv'), required=('blend', 'out'),
              repeat=('reference',))
    w, h = (int(v) for v in o['cell'].lower().split('x'))
    open_blend(o['blend'])
    shown = []
    for ob in mesh_objects():
        col = bool(ob.get('mdl_collider')) or ob.name.endswith('_Collider')
        if ob.get('mdl_lod') or (col and not o.get('colliders')):
            ob.hide_render = True
            continue
        shown.append(ob)
    if not shown:
        fail('No mesh to show')
    cols = [ob for ob in shown if ob.get('mdl_collider') or ob.name.endswith('_Collider')]
    lo, hi = world_bounds([ob for ob in shown if ob not in cols] or shown)
    centre, size = (lo + hi) / 2, hi - lo
    reach = max(size.length, 1e-3)
    if cols:
        # Workbench renders wire-display objects solid, so draw colliders as a thin green cage.
        green = bpy.data.materials.new('mdl_collider_preview')
        green.diffuse_color = (0.2, 0.9, 0.3, 1)
        for ob in cols:
            ob.data.materials.clear()
            ob.data.materials.append(green)
            mod = ob.modifiers.new('cage', 'WIREFRAME')
            mod.thickness = reach * 0.004
            mod.use_replace = True
    surfaces = [ob for ob in shown if ob not in cols]
    if o.get('uv'):
        layers = list(dict.fromkeys(layer.name for ob in surfaces for layer in ob.data.uv_layers))
        if not layers:
            fail('No UV map to preview; run unwrap first')
        out_dir = os.path.abspath(o['out'])
        os.makedirs(out_dir, exist_ok=True)
        stem = os.path.splitext(os.path.basename(o['blend']))[0]
        for layer in layers:
            path = os.path.join(out_dir, f'{stem}_{layer}_layout.png')
            uv_layout(surfaces, layer, path)
            print(path)
        checker(surfaces)
    setup_render(w, h, textured=bool(o.get('uv')) or any(m and m.get('mdl_baked') for ob in shown for m in ob.data.materials))
    cam_data = bpy.data.cameras.new('mdl_cam')
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = reach * 1.15
    cam_data.clip_end = reach * 20
    cam = bpy.data.objects.new('mdl_cam', cam_data)
    sc = bpy.context.scene
    sc.collection.objects.link(cam)
    sc.camera = cam
    out_dir = os.path.abspath(o['out'])
    frames = os.path.join(out_dir, '_views')
    os.makedirs(frames, exist_ok=True)
    name = os.path.splitext(os.path.basename(o['blend']))[0]
    label = f'{size.x:.3f} x {size.y:.3f} x {size.z:.3f} m'
    paths = []
    for view, d in VIEWS:
        cam.location = centre + d.normalized() * reach * 4
        if view == 'top':
            cam.rotation_euler = (0, 0, 0)  # straight down, back (+Y) at the top of the image
        else:
            cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.stamp_note_text = f'{view}   {label}'
        p = os.path.join(frames, f'{view}.png')
        sc.render.filepath = p
        bpy.ops.render.render(write_still=True)
        paths.append(p)
    sheet = os.path.join(out_dir, f"{name}_{'uv_sheet' if o.get('uv') else 'sheet'}.png")
    compose(paths, 3, w, h, sheet)
    print(f'{sheet}\n  size {label}, {len(shown)} mesh object(s) shown; front is -Y (Unity forward)')
    for ref in o['reference']:
        view, _, path = ref.partition('=')
        if not path or any(c in view for c in '/\\:.'):  # no view given; the '=' (if any) is in the path
            view, path = 'front', ref
        reference_check(view, path, surfaces, centre, size, out_dir, name)


main()
