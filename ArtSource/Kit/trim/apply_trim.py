import json
import os
import sys

import bpy

out = sys.argv[sys.argv.index('--') + 1]
layout = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'trim_layout.json'), encoding='utf-8'))
on_sheet = {m for b in layout['bands'] for m in b['maps']}
PRINT = {k: v for k, v in layout.get('print', {}).items() if not k.startswith('_')}
on_sheet |= set(PRINT)

# ---- Print faces: fixed artwork in the sheet's Print band (make_print.py draws it at these rectangles)
import math
from mathutils import Vector

SIZE = layout['size_px']
TOP = SIZE - next(b['px'] for b in layout['bands'] if b['kind'] == 'print')
PAD = 3   # px kept inside each rectangle, so filtering never samples the neighbouring artwork


def uv_rect(name):
    """(u0, v0, u1, v1) of a print rectangle; v0 is its bottom edge."""
    x0, y0, x1, y1, _ = PRINT[name]
    return ((x0 + PAD) / SIZE, 1 - (TOP + y1 - PAD) / SIZE, (x1 - PAD) / SIZE, 1 - (TOP + y0 + PAD) / SIZE)


def islands(faces):
    """Faces split into vertex-connected pieces (each can of a vending row is its own piece)."""
    left, out = set(faces), []
    by_vert = {}
    for f in faces:
        for v in f.vertices:
            by_vert.setdefault(v, []).append(f)
    while left:
        seed = left.pop()
        group_, todo = [seed], [seed]
        while todo:
            f = todo.pop()
            for v in f.vertices:
                for g in by_vert[v]:
                    if g in left:
                        left.remove(g)
                        group_.append(g)
                        todo.append(g)
        out.append(group_)
    return out


placed = {}
for ob in bpy.data.objects:
    if ob.type != 'MESH' or ob.name.endswith('_Collider'):
        continue
    me = ob.data
    uv = me.uv_layers.active.data
    names = [m.name if m else None for m in me.materials]
    for name in PRINT:
        if name not in names:
            continue
        idx = names.index(name)
        faces = [p for p in me.polygons if p.material_index == idx]
        u0, v0, u1, v1 = uv_rect(name)
        mode = PRINT[name][4]
        for piece in islands(faces):
            verts = {v for f in piece for v in f.vertices}
            cos = [me.vertices[v].co for v in verts]
            if mode == 'wrap':
                cx = sum(c.x for c in cos) / len(cos)
                cy = sum(c.y for c in cos) / len(cos)
                z0, z1 = min(c.z for c in cos), max(c.z for c in cos)
                for f in piece:
                    us = []
                    for li in f.loop_indices:
                        c = me.vertices[me.loops[li].vertex_index].co
                        us.append((math.atan2(c.y - cy, c.x - cx) / (2 * math.pi)) % 1.0)
                    if max(us) - min(us) > 0.5:     # the face straddles the seam: keep it on one side
                        us = [x + 1.0 if x < 0.5 else x for x in us]
                    for li, uu in zip(f.loop_indices, us):
                        c = me.vertices[me.loops[li].vertex_index].co
                        uv[li].uv = (u0 + (u1 - u0) * min(uu, 1.0), v0 + (v1 - v0) * (c.z - z0) / max(z1 - z0, 1e-6))
            else:
                # WHY the outward-facing largest face, not an area-weighted average: a thin plate's front and back
                # cancel out, leaving no direction to project along (the water server's ring came out blank).
                def outward(f):
                    return f.area * max(f.normal.dot(f.center), 0.0) + 1e-9 * f.area
                n = max(piece, key=outward).normal.copy()
                up = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((0, 1, 0))
                right = (-n).cross(up).normalized()   # the reader's right, facing the face
                up = right.cross(-n).normalized()
                pu = [c.dot(right) for c in cos]
                pv = [c.dot(up) for c in cos]
                a, b, c_, d_ = min(pu), max(pu), min(pv), max(pv)
                for f in piece:
                    for li in f.loop_indices:
                        co = me.vertices[me.loops[li].vertex_index].co
                        uv[li].uv = (u0 + (u1 - u0) * (co.dot(right) - a) / max(b - a, 1e-6),
                                     v0 + (v1 - v0) * (co.dot(up) - c_) / max(d_ - c_, 1e-6))
        placed.setdefault(ob.name, []).append(name)
print('PRINT_PLACED ' + json.dumps(placed))

trim = bpy.data.materials.get('Trim_Aero') or bpy.data.materials.new('Trim_Aero')
trim.use_nodes = True
bsdf = trim.node_tree.nodes.get('Principled BSDF')
if bsdf:
    bsdf.inputs['Base Color'].default_value = (0.9, 0.93, 0.94, 1.0)   # preview only; Unity uses the sheet
merged = {}
for ob in bpy.data.objects:
    if ob.type != 'MESH' or ob.name.endswith('_Collider'):
        continue
    me = ob.data
    names = [m.name if m else None for m in me.materials]
    keep = [n for n in names if n not in on_sheet]
    if len(keep) == len(names):
        continue
    new = ['Trim_Aero'] + [n for n in keep if n != 'Trim_Aero']
    remap = {i: new.index('Trim_Aero' if n in on_sheet else n) for i, n in enumerate(names)}
    old_index = [p.material_index for p in me.polygons]
    # WHY slots first, then indices: clearing the slots resets every face's material index to 0.
    me.materials.clear()
    for n in new:
        me.materials.append(trim if n == 'Trim_Aero' else bpy.data.materials[n])
    for p, i in zip(me.polygons, old_index):
        p.material_index = remap.get(i, 0)
    merged[ob.name] = {'from': names, 'to': new}
bpy.ops.wm.save_as_mainfile(filepath=out)
print('TRIM_MERGED ' + json.dumps(merged))
