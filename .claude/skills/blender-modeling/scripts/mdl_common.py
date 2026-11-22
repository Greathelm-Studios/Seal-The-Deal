"""Shared helpers for the blender-modeling Blender scripts. Run inside Blender (bpy available)."""
import json
import os
import sys

import bpy
from mathutils import Vector


def script_args():
    """The script's arguments: MDL_ARGS (a JSON list, set by mdl.mjs) or those after Blender's `--`."""
    if 'MDL_ARGS' in os.environ:
        return json.loads(os.environ['MDL_ARGS'])
    return sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []


def fail(msg, code=2):
    print(f'ERROR: {msg}', file=sys.stderr)
    sys.stderr.flush()
    sys.exit(code)


def parse(spec, switches=(), required=(), repeat=()):
    """spec: {option: default}. switches: flags without a value. repeat: options that may be given
    several times (collected into a list). Option names use dashes on the command line."""
    a = script_args()
    o = dict(spec)
    for k in repeat:
        o[k] = []
    i = 0
    while i < len(a):
        key = a[i][2:].replace('-', '_') if a[i].startswith('--') else None
        if key in switches:
            o[key] = True
        elif key in o and i + 1 < len(a):
            if key in repeat:
                o[key].append(a[i + 1])
            else:
                o[key] = a[i + 1]
            i += 1
        else:
            fail(f'Unknown or incomplete argument: {a[i]}')
        i += 1
    missing = [f"--{k.replace('_', '-')}" for k in required if not o.get(k)]
    if missing:
        fail(f"{', '.join(missing)} required")
    return o


def write_json(path, data):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2)


def open_blend(path):
    if not os.path.isfile(path):
        fail(f'File not found: {path}')
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(path))


def mesh_objects():
    return [ob for ob in bpy.context.scene.objects if ob.type == 'MESH']


def world_bounds(obs):
    """World-space (min, max) corners over the objects' evaluated meshes, or None."""
    dg = bpy.context.evaluated_depsgraph_get()
    lo, hi = None, None
    for ob in obs:
        ev = ob.evaluated_get(dg)
        me = ev.to_mesh()
        for v in me.vertices:
            p = ev.matrix_world @ v.co
            lo = p.copy() if lo is None else Vector(map(min, lo, p))
            hi = p.copy() if hi is None else Vector(map(max, hi, p))
        ev.to_mesh_clear()
    return (lo, hi) if lo is not None else None


def tri_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def round3(v):
    return [round(x, 4) for x in v]
