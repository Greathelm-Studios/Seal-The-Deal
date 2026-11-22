"""Right hand closed round the regular pistol's grip, built from erik90mx's "Low Poly Hand (Rigged)" (CC0,
BlendSwap 2429, downloaded 2026-11-21, kept unchanged in Source/LowPolyHand_erik90mx/).

The character's own hand is a mitten that crumples when its finger bones curl, so while the pistol is held the
game hides it and shows this hand instead. Alex picked this CC0 hand over one modelled from blocks (2026-11-21:
the blocky one read "too flat and blocky"), restyled to match the character's hands: chunkier fingers and a
thicker palm. It is a separate model, not part of Pistol.fbx, because the pistol is also used on its own.

What the script does: appends only the hand mesh ("Mano") and its bones ("Huesos Mano") from the download (the
file's controller widgets and its rig_ui.py script are left behind), drops the rig's controller constraints,
thickens the fingers and palm, bends each finger joint toward the palm, freezes the posed hand into a plain
mesh, and places it in the pistol's own space as Blender sees Pistol.blend (muzzle along -Y, up +Z, the gun's
right at -X), so it lines up with Pistol.fbx when both sit at the same place.

The source hand lies flat: fingers along +X, thumb toward +Y, palm facing -Z. Finger chains (knuckle bone,
then three joints): index dedo1.004 > dedo1 > dedo2 > dedo3; middle dedo1.005 > dedo1.000 > dedo2.000 >
dedo3.000; ring dedo1.006 > .001; little dedo1.007 > .002; thumb dedo1.008 > dedo1.003 > dedo2.003 > dedo3.003.

Run: blender -b --factory-startup --disable-autoexec -P ArtSource/Weapons/pistol_grip_hand.py -- <out.blend>
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
SRC_ZIP = os.path.join(HERE, 'Source', 'LowPolyHand_erik90mx', 'low_poly_hand_rigged.zip')
SRC_BLEND = 'Rigged Low Poly Hand by erik90mx.blend'
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = ARGS[0] if ARGS else os.path.join(HERE, 'build', 'PistolGripHand.blend')


def arg(name, default):
    """A number from `-- <out> NAME=VALUE ...` on the command line, for trying values without editing."""
    for a in ARGS[1:]:
        if a.startswith(name + '='):
            return type(default)(a.split('=', 1)[1])
    return default


MM = 0.001
SCALE = arg('SCALE', 0.081)
"""Metres per source unit. WHY 0.081: makes the knuckle span (0.84 units, index to little finger) 68 mm, the
character's own knuckle span in the pistol's space (61 mm in the world; the gun is shown at 90%)."""
KNUCKLES = Vector((arg('KX', -30.0) * MM, arg('KY', -4.0) * MM, arg('KZ', 2.0) * MM))
"""Where the middle knuckle sits in the pistol's Blender space (x toward the gun's left, -y the muzzle, z up): just
off the front right corner of the grip, halfway up it. WHY: at 40 mm up (just under the guard) the fist closed
round the frame and slide; at x -23 mm the palm sat up to 13 mm inside the grip (Alex: "clipping issues",
2026-11-22); at y -18 (18 mm ahead of the front strap) the mitten looped up to 45 mm out in front of the grip and
the thumb's root sat beside the grip, too far forward to reach round the back (Alex: "fingers still don't fully
wrap around the grip", "I can't see the thumb", 2026-11-22). The pose numbers below were picked by measuring hand
vertices inside the pistol's parts: about 9, none deeper than 2 mm."""
TURN = (arg('TX', 0.0), arg('TY', 0.0), arg('TZ', 0.0))
"""Extra turn of the whole hand about the middle knuckle after it is stood against the grip, degrees about the
pistol's x, y, z."""
FINGER_THICK = arg('FINGER_THICK', 1.35)
"""How much thicker the fingers and thumb are made than the source's, round their bones. WHY: the source's
fingers are slender and realistic; the character's hands are chunky and stylised (Alex, 2026-11-21)."""
PALM_THICK = arg('PALM_THICK', 1.0)
"""How much thicker the palm is made, back to front. 1.2 pushed the palm into the grip."""
JOIN_REACH = arg('JOIN_REACH', 0.12)
"""Source units (about 10 mm): how far into the next joint each mitten segment reaches, so the segments overlap
and merge without a crease at the knuckles."""
VOXEL = arg('VOXEL', 0.002)
"""Metres: size of the voxels the merged hand is rebuilt from. Smaller keeps more detail and costs more."""
TRIANGLES = arg('TRIANGLES', 1600)
"""About how many triangles the hand is reduced to after the rebuild."""
WRIST_KEEP = arg('WRIST_KEEP', 0.2)
"""Source units (about 16 mm) of wrist kept behind the wrist joint; the rest of the source's forearm is cut off
and the end capped. WHY: the full stub poked out of the jacket cuff (Alex, 2026-11-21)."""

# Bend of each joint toward the palm, degrees: knuckle, middle, last. The index rests on the trigger, the
# others wrap the grip, the thumb goes round the back of the grip and lies forward along its left side.
# WHY the mitten's numbers: a sharp turn at the knuckle carries it across the front strap, a small one at the
# middle joint keeps it flat there, and the last joint lays the tips back onto the left panel. The ring and little
# fingers are shorter, so the same curl makes a smaller loop that cut 5 mm into the front strap; they curl less.
# Tighter all round (85-100 at the knuckle) folds the mitten into a loop smaller than the 30 mm grip.
CURL = {
    'index': (arg('I1', 10.0), arg('I2', 50.0), arg('I3', 30.0)),
    'middle': (arg('M1', 70.0), arg('M2', 20.0), arg('M3', 65.0)),
    'ring': (arg('R1', 60.0), arg('R2', 15.0), arg('R3', 65.0)),
    'little': (arg('L1', 50.0), arg('L2', 5.0), arg('L3', 65.0)),
    'thumb': (arg('T1', 90.0), arg('T2', 30.0), arg('T3', 40.0)),
}
CHAINS = {
    'index': ('dedo1', 'dedo2', 'dedo3'),
    'middle': ('dedo1.000', 'dedo2.000', 'dedo3.000'),
    'ring': ('dedo1.001', 'dedo2.001', 'dedo3.001'),
    'little': ('dedo1.002', 'dedo2.002', 'dedo3.002'),
    'thumb': ('dedo1.003', 'dedo2.003', 'dedo3.003'),
}
MITTEN = ('middle', 'ring', 'little')
"""Fingers joined into one mitten (Alex, 2026-11-21: "the middle, ring and pinkie finger need to be joined like a
mitten"). They share one curl so they stay side by side, and each joint level is wrapped in one convex shell."""
THUMB_FOLD = arg('THUMB_FOLD', 0.0)
"""Degrees the thumb's base folds forward in the palm's plane, from pointing up toward pointing along the fingers.
0 points it up, so its first joint (T1, 90) carries it straight across the back of the grip, under the slide."""
THUMB_SWING = arg('THUMB_SWING', 0.0)
"""Degrees the thumb's base turns about its own length; negative turns it out from the grip."""
THUMB_TWIST = arg('THUMB_TWIST', -130.0)
"""Degrees the hinge of the thumb's two outer joints is turned about the thumb's length; 0 bends them the same way
as its first joint (toward the palm). WHY -130: it turns them forward, so once the thumb has crossed the back of the
grip it lies along the grip's left side, the side the player's eye sees. The thumb lay along the right of the frame
before, hidden behind the gun from the eye (Alex: "I can't see the thumb", 2026-11-22); going round to the left by
the palm hinge alone took its base through the top of the grip (13 mm deep) or left it pointing out sideways."""

SKIN = (0.84, 0.78, 0.74)   # the character's skin colour; Unity maps the material onto Pistol_Hand anyway


def lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


# ---------------------------------------------------------------- load only the hand and its bones
bpy.ops.wm.read_factory_settings(use_empty=True)
tmp = tempfile.mkdtemp(prefix='hand_src_')
with zipfile.ZipFile(SRC_ZIP) as z:
    name = next(n for n in z.namelist() if n.endswith(SRC_BLEND))
    if '..' in name or name.startswith('/'):
        raise RuntimeError(f'unsafe path in the source zip: {name}')
    z.extract(name, tmp)
with bpy.data.libraries.load(os.path.join(tmp, name), link=False) as (src, dst):
    dst.objects = [n for n in src.objects if n in ('Mano', 'Huesos Mano')]
for ob in dst.objects:
    bpy.context.scene.collection.objects.link(ob)
hand = bpy.data.objects['Mano']
rig = bpy.data.objects['Huesos Mano']
for pb in rig.pose.bones:
    for c in list(pb.constraints):
        pb.constraints.remove(c)
    pb.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()

# ---------------------------------------------------------------- restyle: chunkier fingers, thicker palm
# Each vertex is pushed away from the bone that moves it most, along the bone's sideways directions only, so
# fingers fatten without growing longer. Done on the rest shape, before posing.
to_rig = rig.matrix_world.inverted() @ hand.matrix_world
from_rig = to_rig.inverted()
finger_bones = {n for joints in CHAINS.values() for n in joints}
palm_bones = {'dedo1.004', 'dedo1.005', 'dedo1.006', 'dedo1.007', 'dedo1.008', 'Bone', 'Bone.000'}
groups = {g.index: g.name for g in hand.vertex_groups}
for v in hand.data.vertices:
    if not v.groups:
        continue
    g = max(v.groups, key=lambda e: e.weight)
    bone = rig.data.bones.get(groups[g.group])
    if bone is None:
        continue
    p = to_rig @ v.co
    if bone.name in finger_bones:
        a, b = bone.head_local, bone.tail_local
        d = (b - a).normalized()
        foot = a + d * (p - a).dot(d)
        p = foot + (p - foot) * FINGER_THICK
    elif bone.name in palm_bones:
        centre_z = rig.data.bones['dedo1.005'].head_local.z
        p = Vector((p.x, p.y, centre_z + (p.z - centre_z) * PALM_THICK))
    v.co = from_rig @ p

# ---------------------------------------------------------------- pose: bend each joint toward the palm
PALM = Vector((0.0, 0.0, -1.0))   # the source palm faces -Z (in the rig's world frame, which has no turn)


def bend(bone_name, degrees, axis):
    """Turns a pose bone about its head, about a fixed axis."""
    pb = rig.pose.bones[bone_name]
    head = pb.head.copy()
    r = Matrix.Translation(head) @ Matrix.Rotation(math.radians(degrees), 4, axis) @ Matrix.Translation(-head)
    pb.matrix = r @ pb.matrix
    bpy.context.view_layer.update()


thumb_base = rig.pose.bones['dedo1.008']
bend('dedo1.008', THUMB_FOLD, PALM)
bend('dedo1.008', THUMB_SWING, (thumb_base.tail - thumb_base.head).normalized())
for finger, joints in CHAINS.items():
    # One hinge per finger, from its rest direction: every joint turns about it. WHY: working the axis out from
    # each joint's current direction flipped it once a finger had curled past 90 degrees, and the fingertips
    # bent outward (Alex, 2026-11-21).
    first = rig.pose.bones[joints[0]]
    hinge = (first.tail - first.head).normalized().cross(PALM).normalized()
    for k, (joint, degrees) in enumerate(zip(joints, CURL[finger])):
        if finger == 'thumb' and k == 1 and THUMB_TWIST:
            # The thumb's outer joints bend about a hinge turned about the thumb's own length, so after its first
            # joint carries it round the back of the grip they can lay it forward along the grip's side.
            pb = rig.pose.bones[joint]
            along = (pb.tail - pb.head).normalized()
            hinge = (Matrix.Rotation(math.radians(THUMB_TWIST), 3, along) @ hinge).normalized()
        bend(joint, degrees, hinge)

# ---------------------------------------------------------------- freeze the posed hand into a plain mesh
ev = hand.evaluated_get(bpy.context.evaluated_depsgraph_get())
bm = bmesh.new()
bm.from_mesh(ev.to_mesh())
ev.to_mesh_clear()
bm.transform(hand.matrix_world)
bm.verts.ensure_lookup_table()
owner = []    # the bone that moves each vertex most (the armature changes no topology, so indices match)
for v in hand.data.vertices:
    owner.append(groups[max(v.groups, key=lambda e: e.weight).group] if v.groups else None)

# ---------------------------------------------------------------- the mitten: one shell per joint level
levels = [[CHAINS[f][i] for f in MITTEN] for i in range(3)]
knuckle_bones = ['dedo1.005', 'dedo1.006', 'dedo1.007']   # the palm bones the mitten fingers hang from
mitten_bones = {n for lv in levels for n in lv}


def posed(bone_name, end='head'):
    pb = rig.pose.bones[bone_name]
    return rig.matrix_world @ (pb.head if end == 'head' else pb.tail)


shells = []
for i, lv in enumerate(levels):
    pts = [bm.verts[k].co.copy() for k, o in enumerate(owner) if o in lv]
    # Reach a little into the neighbouring joints, so neighbouring shells overlap and merge cleanly.
    nxt = levels[i + 1] if i + 1 < len(levels) else []
    prev = levels[i - 1] if i > 0 else knuckle_bones
    joints_ahead = [posed(n) for n in nxt]
    joints_behind = [posed(n) for n in lv]
    for k, o in enumerate(owner):
        co = bm.verts[k].co
        if o in nxt and any((co - j).length < JOIN_REACH for j in joints_ahead):
            pts.append(co.copy())
        if o in prev and any((co - j).length < JOIN_REACH for j in joints_behind):
            pts.append(co.copy())
    hb = bmesh.new()
    for q in pts:
        hb.verts.new(q)
    hull = bmesh.ops.convex_hull(hb, input=hb.verts)
    inside = {g for g in hull['geom_interior'] + hull['geom_unused'] if isinstance(g, bmesh.types.BMVert)}
    bmesh.ops.delete(hb, geom=list(inside), context='VERTS')
    bmesh.ops.recalc_face_normals(hb, faces=hb.faces)
    shells.append(hb)

# Drop the separate fingers; the shells replace them.
bmesh.ops.delete(bm, geom=[f for f in bm.faces if all(owner[v.index] in mitten_bones for v in f.verts)],
                 context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')

# ---------------------------------------------------------------- cut the forearm off just behind the wrist
wrist_x = (rig.matrix_world @ rig.data.bones['Bone'].head_local).x
bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                       plane_co=(wrist_x - WRIST_KEEP, 0, 0), plane_no=(1, 0, 0), clear_inner=True)
bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if len(e.link_faces) == 1], sides=0)
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

# ---------------------------------------------------------------- merge hand and mitten into one surface
# The palm, index, thumb and the three mitten shells overlap; a voxel remesh turns them into one closed surface
# and a decimate brings it back to low poly. WHY not a boolean union: it left holes and faces stacked inside the
# hand, and through them the toon outline's inside showed as black patches on the mitten (seen in play,
# 2026-11-22).
joined = bm
for hb in shells:
    tmp_me = bpy.data.meshes.new('_shell')
    hb.to_mesh(tmp_me)
    joined.from_mesh(tmp_me)
    hb.free()
me_j = bpy.data.meshes.new('_joined')
joined.to_mesh(me_j)
joined.free()
joined_ob = bpy.data.objects.new('_joined', me_j)
bpy.context.scene.collection.objects.link(joined_ob)
remesh = joined_ob.modifiers.new('remesh', 'REMESH')
remesh.mode = 'VOXEL'
remesh.voxel_size = VOXEL / SCALE
remesh.adaptivity = 0.0
decimate = joined_ob.modifiers.new('decimate', 'DECIMATE')
decimate.ratio = 1.0
ev = joined_ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
full = len(ev.to_mesh().polygons)
ev.to_mesh_clear()
decimate.ratio = min(1.0, TRIANGLES / max(1, full * 2))
ev = joined_ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
bm = bmesh.new()
bm.from_mesh(ev.to_mesh())
ev.to_mesh_clear()
bmesh.ops.dissolve_degenerate(bm, dist=0.002, edges=bm.edges)   # needle faces the reduction leaves
bmesh.ops.triangulate(bm, faces=bm.faces)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

# Source frame to the pistol's: fingers (+X) toward the muzzle (-Y), thumb side (+Y) up (+Z), palm (-Z)
# against the right of the grip, facing the gun's left (+X). A proper turn, so a right hand stays a right hand.
to_pistol = Matrix(((0, 0, -1), (-1, 0, 0), (0, 1, 0))).to_4x4()
mid = rig.matrix_world @ rig.data.bones['dedo1.005'].tail_local    # middle knuckle (palm bones are not posed)
turn = Matrix.Rotation(math.radians(TURN[2]), 4, 'Z') @ Matrix.Rotation(math.radians(TURN[1]), 4, 'Y') \
    @ Matrix.Rotation(math.radians(TURN[0]), 4, 'X')
bm.transform(Matrix.Translation(KNUCKLES) @ turn @ Matrix.Scale(SCALE, 4) @ to_pistol @ Matrix.Translation(-mid))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

# ---------------------------------------------------------------- the result
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
me = bpy.data.meshes.new('PistolGripHand')
bm.to_mesh(me)
bm.free()
mat = bpy.data.materials.new('Hand_Skin')
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get('Principled BSDF')
rgba = (*(lin(c) for c in SKIN), 1.0)
bsdf.inputs['Base Color'].default_value = rgba
mat.diffuse_color = rgba
me.materials.clear()
me.materials.append(mat)
for p in me.polygons:
    p.material_index = 0
    p.use_smooth = True
if not me.uv_layers:
    me.uv_layers.new(name='UVMap')
ob = bpy.data.objects.new('PistolGripHand', me)
bpy.context.scene.collection.objects.link(ob)
for m in list(bpy.data.materials):
    if m != mat:
        bpy.data.materials.remove(m)
for block in (bpy.data.meshes, bpy.data.armatures, bpy.data.actions, bpy.data.texts, bpy.data.images):
    for d in list(block):
        if d.users == 0:
            block.remove(d)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print('HAND', OUT, tuple(round(d * 1000, 1) for d in ob.dimensions), len(me.polygons))
