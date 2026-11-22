"""Adapts the "Guns Low poly" pack's AR-15 (mesh `MG5`) into AR15.blend.

Run: blender -b --factory-startup -P ArtSource/Weapons/ar15_adapt.py -- <out>/AR15.blend

Source (measured 2026-11-22): `MG5` is 3,077 triangles in eleven loose pieces, lying on its side like the
pack's pistol: length along Y with the muzzle at -Y, the gun's top toward -X, its width along Z. Sorted by
face count the pieces are, in order: flash hider, barrel, magazine, handguard, stock, lower receiver, upper
receiver, carry handle, pistol grip, front sight, trigger. The script checks those counts and stops if the
download ever differs.

Output: the rifle upright, muzzle along -Y (Unity +Z), pivot at the middle of the pistol grip, in two objects:
`AR15_Body` and `AR15_Magazine` (the reload slides it out). Flat named materials; Unity's weapon material
tool turns them toon. The script prints the measurements the Unity side uses (PRINTED lines).
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
SRC_OBJECT = 'MG5'
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = ARGS[0] if ARGS else os.path.join(HERE, 'build', 'AR15.blend')

MM = 0.001
LENGTH = 0.760
"""Metres, flash hider to the back of the stock. WHY 760 mm: a real carbine is about 840; shown at 90 % in the
hand like the pistol, this is 684 mm on screen, which leaves the muzzle in view at the two-handed hold."""
WIDTH_STRETCH = 1.3
"""How much wider than the source the rifle is made. WHY: the pack's guns are thin plates from the eye (the
pistol needed 1.15 to 1.4); at 1.3 the pistol grip is 30 mm across, the width the grip hand was modelled round."""
ROUND_PIECES = ('FlashHider', 'Barrel', 'Handguard', 'FrontSight')
"""Pieces left at the source's width. WHY: they are round, and the stretch made the handguard an oval 79 mm
across (seen on the sheet, 2026-11-22)."""
HANDGUARD_SLIM = 0.72
"""How much slimmer than the source the handguard is made, about its own centre line. WHY 0.72 (44 mm across):
at the source's 61 mm the character's hand, 68 mm across the knuckles, could touch only about a quarter of the
way round it, so the left hand could not wrap it (measured, 2026-11-22; Alex: "hand doesnt fully wrap around the
barrel"). A real carbine's handguard is about 50 mm."""
WELD = 0.02 * MM
"""Welds the FBX's split vertices (UV seams) without closing real gaps."""

PIECES = ('FlashHider', 'Barrel', 'Magazine', 'Handguard', 'Stock', 'Lower', 'Upper', 'CarryHandle', 'Grip',
          'FrontSight', 'Trigger')
"""The source's loose pieces, largest first."""
FACE_COUNTS = (288, 164, 148, 128, 127, 126, 119, 92, 78, 76, 25)
"""Face count of each piece after welding, in the same order; a different download fails here, not silently."""

# Flat colours (sRGB, metallic, roughness) from the art bible palette, the pistol's scheme.
COLOURS = {
    'Metal': ((0.784, 0.824, 0.851), 0.8, 0.35),      # brushed silver #C8D2D9: barrel, flash hider, sight, trigger
    'Receiver': ((0.067, 0.106, 0.220), 0.0, 0.6),    # deep navy #111B38: receivers and carry handle
    'Furniture': ((0.659, 0.749, 0.820), 0.0, 0.7),   # mist #A8BFD1: handguard, stock, grip
    'Magazine': ((0.271, 0.749, 0.780), 0.0, 0.45),   # aqua #45BFC7
}
SLOT = {k: i for i, k in enumerate(COLOURS)}
MATERIAL_OF = {
    'FlashHider': 'Metal', 'Barrel': 'Metal', 'FrontSight': 'Metal', 'Trigger': 'Metal',
    'Lower': 'Receiver', 'Upper': 'Receiver', 'CarryHandle': 'Receiver',
    'Handguard': 'Furniture', 'Stock': 'Furniture', 'Grip': 'Furniture',
    'Magazine': 'Magazine',
}


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


def bounds(points):
    points = list(points)
    return (Vector([min(p[i] for p in points) for i in range(3)]),
            Vector([max(p[i] for p in points) for i in range(3)]))


def lean_of(points):
    """How far a piece leans, in degrees, top toward the muzzle: the middle of its top fifth against its bottom fifth."""
    points = list(points)
    lo, hi = bounds(points)
    band = (hi.z - lo.z) * 0.2
    low = [p for p in points if p.z <= lo.z + band]
    high = [p for p in points if p.z >= hi.z - band]
    a_lo, a_hi = bounds(low)
    b_lo, b_hi = bounds(high)
    a, b = (a_lo + a_hi) / 2, (b_lo + b_hi) / 2
    return math.degrees(math.atan2(-(b.y - a.y), b.z - a.z))


def unity(v):
    """A point in this file's space (muzzle -Y, up +Z) as Unity sees it (muzzle +Z, up +Y), millimetres."""
    return tuple(round(c * 1000, 1) for c in (v.x, v.z, -v.y))


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
lo, hi = bounds(v.co for v in bm.verts)
s = LENGTH / (hi.y - lo.y)
bm.transform(Matrix.Diagonal((s * WIDTH_STRETCH, s, s, 1)) @ Matrix.Translation(-(lo + hi) / 2))
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=WELD)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

pieces = sorted(islands(bm), key=len, reverse=True)
counts = tuple(len(p) for p in pieces)
if counts != FACE_COUNTS:
    raise RuntimeError(f'MG5 is not the mesh this script was written for: piece face counts {counts}')
piece = dict(zip(PIECES, pieces))
for part, faces in piece.items():
    for f in faces:
        f.material_index = SLOT[MATERIAL_OF[part]]
    if part in ROUND_PIECES:
        for v in {v for f in faces for v in f.verts}:
            v.co.x /= WIDTH_STRETCH
    if part == 'Handguard':
        verts = {v for f in faces for v in f.verts}
        middle = (min(v.co.z for v in verts) + max(v.co.z for v in verts)) / 2
        for v in verts:
            v.co.x *= HANDGUARD_SLIM
            v.co.z = middle + (v.co.z - middle) * HANDGUARD_SLIM


def points_of(part):
    return {v.index: v.co.copy() for f in piece[part] for v in f.verts}.values()


# The barrel has to point forward of the grip, or every measurement below is mirrored.
if bounds(points_of('FlashHider'))[0].y > bounds(points_of('Grip'))[0].y:
    raise RuntimeError('rifle orientation not as recorded: the flash hider is behind the grip')

# ---------------------------------------------------------------- pivot: middle of the pistol grip
g_lo, g_hi = bounds(points_of('Grip'))
pivot = (g_lo + g_hi) / 2
pivot.x = 0.0

f_lo, f_hi = bounds(points_of('FlashHider'))
muzzle = Vector((0.0, f_lo.y, (f_lo.z + f_hi.z) / 2)) - pivot
h_lo, h_hi = bounds(points_of('Handguard'))
handguard_under = Vector((0.0, (h_lo.y + h_hi.y) / 2, h_lo.z)) - pivot
m_lo, m_hi = bounds(points_of('Magazine'))
l_lo, l_hi = bounds(points_of('Lower'))
magazine_bottom = Vector((0.0, (m_lo.y + m_hi.y) / 2, m_lo.z)) - pivot
measured = [
    ('grip lean degrees', round(lean_of(points_of('Grip')), 1)),
    ('grip size mm (width, depth, height)', tuple(round(c * 1000, 1) for c in (g_hi.x - g_lo.x, g_hi.y - g_lo.y, g_hi.z - g_lo.z))),
    ('muzzle (Unity mm)', unity(muzzle)),
    ('handguard underside middle (Unity mm)', unity(handguard_under)),
    ('handguard width mm', round((h_hi.x - h_lo.x) * 1000, 1)),
    ('magazine lean degrees', round(lean_of(points_of('Magazine')), 1)),
    ('magazine height mm', round((m_hi.z - m_lo.z) * 1000, 1)),
    ('magazine below the lower receiver mm', round((l_lo.z - m_lo.z) * 1000, 1)),
    ('magazine bottom middle (Unity mm)', unity(magazine_bottom)),
]

# ---------------------------------------------------------------- objects
magazine_faces = {f.index for f in piece['Magazine']}
body_bm, mag_bm = bm.copy(), bm.copy()
for part_bm, keep_magazine in ((body_bm, False), (mag_bm, True)):
    part_bm.faces.ensure_lookup_table()
    drop = [f for f in part_bm.faces if (f.index in magazine_faces) != keep_magazine]
    bmesh.ops.delete(part_bm, geom=drop, context='FACES')
bm.free()

mats = [flat(f'AR15_{k}', v) for k, v in COLOURS.items()]
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
root = bpy.data.objects.new('AR15', None)
bpy.context.scene.collection.objects.link(root)
made = []
for name, part in (('AR15_Body', body_bm), ('AR15_Magazine', mag_bm)):
    bmesh.ops.translate(part, verts=part.verts, vec=-pivot)
    me = bpy.data.meshes.new(name)
    part.to_mesh(me)
    part.free()
    for m in mats:
        me.materials.append(m)
    # Flat shading, as on the pistol: smoothing across the source's long triangles draws dark streaks.
    for p in me.polygons:
        p.use_smooth = False
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
    if not m.name.startswith('AR15_'):
        bpy.data.materials.remove(m)
for me in list(bpy.data.meshes):
    if me.users == 0:
        bpy.data.meshes.remove(me)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print('ADAPTED', OUT, 'scale', round(s, 6), 'pivot', tuple(round(c * 1000, 1) for c in pivot))
for label, value in measured:
    print('PRINTED', label, value)
for ob in made:
    print('PART', ob.name, tuple(round(c * 1000, 1) for c in ob.dimensions), len(ob.data.polygons))
