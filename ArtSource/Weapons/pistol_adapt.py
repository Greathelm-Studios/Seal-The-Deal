# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code, blender-modeling skill)
#  Generated : 2026-11-21
#  Prompt    : Alex: regular pistol from the "Guns Low poly" pack (Satendra Saraswat, Sketchfab, CC-BY),
#              so it matches the rifle and rocket launcher planned from the same pack; silver slide, navy
#              frame; the first adaptation (Yurrpo's handgun) was "too plain / generic" and too dark.
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Modified  : Claude Opus 5.5, 2026-11-21 — source switched from Yurrpo's Stylized Handgun to the pack's
#              pistol; cuts the fused mesh into parts and adds the AONAGI ring and a magazine base.
#  Summary   : Turns the pack's pistol (one fused mesh, object "MSR") into the game's pistol: stood upright,
#              muzzle along -Y, 190 mm long, pivot at the middle of the grip, cut into a moving slide, a
#              fixed frame, a trigger and a magazine base, with flat named materials the Unity tool turns
#              toon. A script so the download stays untouched and the result can be rebuilt.
#              Run: blender -b --factory-startup -P ArtSource/Weapons/pistol_adapt.py -- <out.blend>
# -----------------------------------------------------------------------------
"""Adapts Source/GunsLowPoly_SatendraSaraswat/guns_low_poly_fbx.zip (Sketchfab's original-format download)
into Pistol.blend.

Source (checked 2026-11-21 with renders): `source/Guns.fbx` holds the whole pack; the pistol is the mesh
`MSR`, 776 triangles, lying on its side: length along Y with the muzzle at -Y, the gun's top toward -X,
its width along Z. It is three loose pieces: the gun (slide, frame and grip fused), the trigger and the
hammer. It has one material coloured from a gradient texture; that is replaced by flat named materials.
"""
import math
import os
import sys
import tempfile
import zipfile

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SRC_ZIP = os.path.join(HERE, 'Source', 'GunsLowPoly_SatendraSaraswat', 'guns_low_poly_fbx.zip')
SRC_OBJECT = 'MSR'
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = ARGS[0] if ARGS else os.path.join(HERE, 'build', 'Pistol.blend')

MM = 0.001
LENGTH = 0.190
"""Metres, muzzle to the back of the hammer. WHY 190 mm: the size the hand was tuned to (shown at 90%)."""
WIDTH_STRETCH = 1.4
"""How much wider than the source the gun is made. WHY 1.4: the source is 25 mm across at 190 mm long and reads
as a thin plate from the eye; 1.15 still looked "a bit small/thin" in the hand (Alex, 2026-11-21), so it is now
about 36 mm across, a chunky stylised read."""
GRIP_STRETCH = 1.15
"""How much wider than the source the frame (grip and trigger guard) is. WHY 1.15 (about 30 mm): at 1.4 the
grip read too thick in the hand (Alex, 2026-11-21). The slide keeps WIDTH_STRETCH; it is 26 mm either way."""
HEIGHT_STRETCH = 1.08
"""How much taller than the source (slide and grip alike), for the same chunkier read without a longer gun."""
SLIDE_CUT = 21.8 * MM
"""Height (above the source's centre, after scaling) of the slide/frame shut line: the step in the side
profile where the slide's flat underside meets the frame (measured from height slices, 2026-11-21)."""
SIGHT_H, SIGHT_LEN = 3.0 * MM, 6.0 * MM
"""Added sight blocks (the source has a flat rib): height and length, metres."""
FRONT_SIGHT_W, REAR_SIGHT_W, REAR_NOTCH = 3.0 * MM, 12.0 * MM, 3.5 * MM
"""Front blade width, rear sight overall width and the notch in it, metres."""
SIGHT_INSET = 6.0 * MM
"""How far each sight sits in from its end of the slide, metres."""
PANEL_DEPTH = 0.6 * MM
"""Faces this close to the widest point, facing sideways, are the raised grip panels."""
MAG_BASE_H = 4.0 * MM
"""Thickness of the magazine base plate added under the grip (the source has none)."""
RING_OUTER, RING_INNER, RING_PROUD = 6.0 * MM, 3.9 * MM, 0.5 * MM
"""AONAGI ring on the left grip panel: outer and inner radius and how far it stands out, metres."""
DOT_R = 2.0 * MM
"""The ring's red hanko dot radius, metres."""
WELD = 0.02 * MM
"""Welds the FBX's split vertices (UV seams) without closing real gaps."""

# Flat colours (sRGB, metallic, roughness) from the art bible palette; Unity swaps them for toon materials.
COLOURS = {
    'Slide': ((0.784, 0.824, 0.851), 0.8, 0.35),   # brushed silver #C8D2D9
    'Frame': ((0.067, 0.106, 0.220), 0.0, 0.6),    # deep navy #111B38
    'Grip': ((0.659, 0.749, 0.820), 0.0, 0.7),     # mist #A8BFD1 grip panels
    'Trigger': ((0.784, 0.824, 0.851), 0.8, 0.35),  # silver
    'Sights': ((0.271, 0.749, 0.780), 0.0, 0.4),   # aqua #45BFC7
    'MagBase': ((0.271, 0.749, 0.780), 0.0, 0.45),  # aqua
    'Logo': ((0.271, 0.749, 0.780), 0.0, 0.4),     # aqua ring
    'Hanko': ((0.780, 0.278, 0.243), 0.0, 0.6),    # hanko red #C7473E
}
SLOT = {k: i for i, k in enumerate(COLOURS)}


def lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def flat(name, spec):
    color, metallic, roughness = spec
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    rgba = (*(lin(c) for c in color), 1.0)
    bsdf.inputs['Base Color'].default_value = rgba
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    m.diffuse_color = rgba
    return m


def islands(bm):
    """Loose pieces of a bmesh, as lists of faces."""
    bm.faces.ensure_lookup_table()
    seen, out = set(), []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, isl = [f], []
        seen.add(f.index)
        while stack:
            cur = stack.pop()
            isl.append(cur)
            for e in cur.edges:
                for nf in e.link_faces:
                    if nf.index not in seen:
                        seen.add(nf.index)
                        stack.append(nf)
        out.append(isl)
    return out


def keep_faces(bm, faces):
    """A copy of bm holding only the given faces."""
    keep = {f.index for f in faces}
    out = bm.copy()
    out.faces.ensure_lookup_table()
    bmesh.ops.delete(out, geom=[f for f in out.faces if f.index not in keep], context='FACES')
    return out


def halve(bm, z, keep_above):
    """Cuts bm at height z, keeps one side and caps the cut so the part stays closed."""
    out = bm.copy()
    geom = list(out.verts) + list(out.edges) + list(out.faces)
    bmesh.ops.bisect_plane(out, geom=geom, plane_co=(0, 0, z), plane_no=(0, 0, 1),
                           clear_inner=keep_above, clear_outer=not keep_above, dist=1e-6)
    open_edges = [e for e in out.edges if len(e.link_faces) == 1]
    if open_edges:
        bmesh.ops.holes_fill(out, edges=open_edges, sides=0)
    bmesh.ops.recalc_face_normals(out, faces=out.faces)
    return out


def clean(bm):
    """Removes the needle triangles a cut leaves (under 0.01 mm², which the check fails)."""
    bmesh.ops.dissolve_degenerate(bm, dist=WELD, edges=bm.edges)
    for _ in range(6):
        tiny = [f for f in bm.faces if f.calc_area() <= 1e-8]
        if not tiny:
            break
        bmesh.ops.collapse(bm, edges=list({min(f.edges, key=lambda e: e.calc_length()) for f in tiny}), uvs=False)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)


def ring(outer, inner, depth, segments=32):
    """A flat ring (washer) lying in the YZ plane, facing -X, centred on the origin."""
    bm = bmesh.new()
    a = bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=outer, radius2=outer, depth=depth)
    cut = bmesh.new()
    bmesh.ops.create_cone(cut, cap_ends=True, segments=segments, radius1=inner, radius2=inner, depth=depth * 3)
    me_a, me_c = bpy.data.meshes.new('_a'), bpy.data.meshes.new('_c')
    bm.to_mesh(me_a)
    cut.to_mesh(me_c)
    oa, oc = bpy.data.objects.new('_a', me_a), bpy.data.objects.new('_c', me_c)
    for o in (oa, oc):
        bpy.context.scene.collection.objects.link(o)
    mod = oa.modifiers.new('b', 'BOOLEAN')
    mod.operation, mod.solver, mod.object = 'DIFFERENCE', 'EXACT', oc
    ev = oa.evaluated_get(bpy.context.evaluated_depsgraph_get())
    out = bmesh.new()
    out.from_mesh(ev.to_mesh())
    ev.to_mesh_clear()
    for o in (oa, oc):
        bpy.data.objects.remove(o)
    for me in (me_a, me_c):
        bpy.data.meshes.remove(me)
    out.transform(Matrix.Rotation(math.radians(90), 4, 'Y'))   # axis Z -> X: lies flat against a side face
    return out


# ---------------------------------------------------------------- import
bpy.ops.wm.read_factory_settings(use_empty=True)
tmp = tempfile.mkdtemp(prefix='pack_src_')
with zipfile.ZipFile(SRC_ZIP) as z:
    name = next(n for n in z.namelist() if n.lower().endswith('.fbx'))
    if '..' in name or name.startswith('/'):
        raise RuntimeError(f'unsafe path in the source zip: {name}')
    z.extract(name, tmp)
bpy.ops.import_scene.fbx(filepath=os.path.join(tmp, name))
bpy.context.view_layer.update()
src = bpy.data.objects.get(SRC_OBJECT)
if src is None or src.type != 'MESH':
    raise RuntimeError(f'no mesh "{SRC_OBJECT}" in the pack; found {[o.name for o in bpy.data.objects]}')

bm = bmesh.new()
bm.from_mesh(src.data)
# Stand it up: the source's -X (the gun's top) becomes +Z, its Z (width) becomes X; Y (muzzle -Y) stays.
bm.transform(Matrix.Rotation(math.radians(90), 4, 'Y') @ src.matrix_world)
pts = [v.co for v in bm.verts]
lo = Vector([min(p[i] for p in pts) for i in range(3)])
hi = Vector([max(p[i] for p in pts) for i in range(3)])
muzzle_y = min(p.y for p in pts)
if (sum(p.y for p in pts if p.z > (lo.z + hi.z) / 2) / max(1, sum(1 for p in pts if p.z > (lo.z + hi.z) / 2))) < \
        (sum(p.y for p in pts if p.z < (lo.z + hi.z) / 2) / max(1, sum(1 for p in pts if p.z < (lo.z + hi.z) / 2))):
    pass  # slide mass sits forward of the grip: muzzle at -Y as expected
else:
    raise RuntimeError('pistol orientation not as recorded: the grip is forward of the slide')
s = LENGTH / (hi.y - lo.y)
bm.transform(Matrix.Diagonal((s * WIDTH_STRETCH, s, s * HEIGHT_STRETCH, 1)) @ Matrix.Translation(-(lo + hi) / 2))
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=WELD)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

# ---------------------------------------------------------------- the loose pieces: gun, trigger, hammer
pieces = sorted(islands(bm), key=len, reverse=True)
gun_bm = keep_faces(bm, pieces[0])
small = [keep_faces(bm, p) for p in pieces[1:]]
# The trigger hangs inside the guard (below the shut line); the hammer sits at the back on top.
small.sort(key=lambda b: sum(v.co.z for v in b.verts) / len(b.verts))
trigger_bm = small[0]
hammer_bm = small[1] if len(small) > 1 else None
bm.free()

top = max(v.co.z for v in gun_bm.verts)
widest = max(abs(v.co.x) for v in gun_bm.verts)
slide_bm = halve(gun_bm, SLIDE_CUT * HEIGHT_STRETCH, keep_above=True)
frame_bm = halve(gun_bm, SLIDE_CUT * HEIGHT_STRETCH, keep_above=False)
clean(frame_bm)
gun_bm.free()

for f in slide_bm.faces:
    f.material_index = SLOT['Slide']
clean(slide_bm)
# Sights: aqua blocks on the roof, a blade at the front and a notched pair at the back.
# WHY from the flat roof, not the whole slide: the hammer spur behind the roof is higher and further back,
# and a sight placed by the slide's overall length floated above it.
roof_z = max(v.co.z for v in slide_bm.verts)
roof_ys = [v.co.y for v in slide_bm.verts if v.co.z > roof_z - 1.0 * MM]
slide_front, slide_back = min(roof_ys), max(roof_ys)
half = (REAR_SIGHT_W - REAR_NOTCH) / 2
for x, w, y in ((0.0, FRONT_SIGHT_W, slide_front + SIGHT_INSET),
                (-(REAR_NOTCH + half) / 2, half, slide_back - SIGHT_INSET),
                ((REAR_NOTCH + half) / 2, half, slide_back - SIGHT_INSET)):
    blk = bmesh.new()
    bmesh.ops.create_cube(blk, size=1.0)
    bmesh.ops.scale(blk, vec=(w, SIGHT_LEN, SIGHT_H), verts=blk.verts)
    # WHY sink 0.5 mm: the roof is not perfectly flat after the width stretch; buried a little, no gap shows.
    roof = roof_z
    bmesh.ops.translate(blk, verts=blk.verts, vec=(x, y, roof + SIGHT_H / 2 - 0.5 * MM))
    for f in blk.faces:
        f.material_index = SLOT['Sights']
    me = bpy.data.meshes.new('_sight')
    blk.to_mesh(me)
    slide_bm.from_mesh(me)
    blk.free()
for f in frame_bm.faces:
    c = f.calc_center_median()
    panel = abs(f.normal.x) > 0.9 and abs(c.x) > widest - PANEL_DEPTH
    f.material_index = SLOT['Grip'] if panel else SLOT['Frame']
for f in trigger_bm.faces:
    f.material_index = SLOT['Trigger']
if hammer_bm is not None:
    for f in hammer_bm.faces:
        f.material_index = SLOT['Trigger']

# ---------------------------------------------------------------- pivot: middle of the grip
trigger_low = min(v.co.z for v in trigger_bm.verts)
# Narrow the frame (grip and guard) back; materials are already assigned, so the panels stay panels.
bmesh.ops.scale(frame_bm, vec=(GRIP_STRETCH / WIDTH_STRETCH, 1.0, 1.0), verts=frame_bm.verts)
grip = [v.co.copy() for v in frame_bm.verts if v.co.z < trigger_low]
pivot = Vector([(min(p[i] for p in grip) + max(p[i] for p in grip)) / 2 for i in range(3)])
pivot.x = 0.0

# ---------------------------------------------------------------- magazine base: a plate under the grip
grip_low = min(v.co.z for v in frame_bm.verts)
bottom = [f for f in frame_bm.faces if f.normal.z < -0.7 and f.calc_center_median().z < grip_low + 3 * MM]
if not bottom:
    raise RuntimeError('found no downward face at the grip bottom for the magazine base')
mag_bm = bmesh.new()
for f in bottom:
    vs = [mag_bm.verts.new(v.co) for v in f.verts]
    mag_bm.faces.new(vs)
bmesh.ops.remove_doubles(mag_bm, verts=mag_bm.verts, dist=WELD)
ext = bmesh.ops.extrude_face_region(mag_bm, geom=list(mag_bm.faces))
down = Vector((0, 0, -MAG_BASE_H))
bmesh.ops.translate(mag_bm, verts=[g for g in ext['geom'] if isinstance(g, bmesh.types.BMVert)], vec=down)
bmesh.ops.recalc_face_normals(mag_bm, faces=mag_bm.faces)
for f in mag_bm.faces:
    f.material_index = SLOT['MagBase']

# ---------------------------------------------------------------- AONAGI ring on the left grip panel
panel = [f for f in frame_bm.faces if f.material_index == SLOT['Grip'] and f.normal.x < 0]
if panel:
    pc = sum((f.calc_center_median() * f.calc_area() for f in panel), Vector()) / sum(f.calc_area() for f in panel)
    face_x = min(v.co.x for f in panel for v in f.verts)
    logo = ring(RING_OUTER, RING_INNER, RING_PROUD * 2)
    for f in logo.faces:
        f.material_index = SLOT['Logo']
    bmesh.ops.translate(logo, verts=logo.verts, vec=(face_x, pc.y, pc.z))
    dot = bmesh.new()
    bmesh.ops.create_cone(dot, cap_ends=True, segments=16, radius1=DOT_R, radius2=DOT_R, depth=RING_PROUD * 2)
    dot.transform(Matrix.Rotation(math.radians(90), 4, 'Y'))
    # WHY lower right of the ring's gap, seen from the left: the logo's red dot sits there (art bible).
    off = (RING_OUTER + RING_INNER) / 2
    bmesh.ops.translate(dot, verts=dot.verts, vec=(face_x, pc.y - off * 0.7, pc.z - off * 0.7))
    for f in dot.faces:
        f.material_index = SLOT['Hanko']
    me = bpy.data.meshes.new('_logo')
    logo.to_mesh(me)
    frame_bm.from_mesh(me)
    tmp_me = bpy.data.meshes.new('_dot2')
    dot.to_mesh(tmp_me)
    frame_bm.from_mesh(tmp_me)
    logo.free()
    dot.free()

if hammer_bm is not None:
    me = bpy.data.meshes.new('_hammer')
    hammer_bm.to_mesh(me)
    frame_bm.from_mesh(me)
    hammer_bm.free()

# ---------------------------------------------------------------- objects
mats = [flat(f'Pistol_{k}', v) for k, v in COLOURS.items()]
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
root = bpy.data.objects.new('Pistol', None)
bpy.context.scene.collection.objects.link(root)
made = []
for name, part in (('Pistol_Slide', slide_bm), ('Pistol_Frame', frame_bm),
                   ('Pistol_Trigger', trigger_bm), ('Pistol_Magazine', mag_bm)):
    bmesh.ops.translate(part, verts=part.verts, vec=-pivot)
    me = bpy.data.meshes.new(name)
    part.to_mesh(me)
    part.free()
    for m in mats:
        me.materials.append(m)
    # WHY flat shading: the source is low-poly with long triangulated faces; smoothing across them drew
    # dark streaks down the slide. Flat faces give the clean toon read of the rest of the pack.
    for p in me.polygons:
        p.use_smooth = False
    # Box UVs so the check passes and any tiling texture lines up; the toon materials need none.
    uv = me.uv_layers.new(name='UVMap')
    for poly in me.polygons:
        n = poly.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            a, b = [(1, 2), (0, 2), (0, 1)][ax]
            uv.data[li].uv = (co[a] * 10, co[b] * 10)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = root
    made.append(ob)
for img in list(bpy.data.images):
    bpy.data.images.remove(img)
for m in list(bpy.data.materials):
    if not m.name.startswith('Pistol_'):
        bpy.data.materials.remove(m)
for me in list(bpy.data.meshes):
    if me.users == 0:
        bpy.data.meshes.remove(me)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print('ADAPTED', OUT, 'scale', round(s, 6), 'pivot', tuple(round(c * 1000, 1) for c in pivot))
for ob in made:
    print('PART', ob.name, tuple(round(c * 1000, 1) for c in ob.dimensions), len(ob.data.polygons))
