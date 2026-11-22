"""Run a model script in an empty scene and save the result.

blender -b --factory-startup -P build.py -- --script model.py --out <dir>/name.blend [--set KEY=VALUE ...]

The script imports modelkit (this folder is on its path) and builds parts with it. Writes the
.blend and <dir>/name.build.json: the params used, and per object its size, triangles and materials.
"""
import os
import runpy
import sys
import traceback

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import modelkit  # noqa: E402
from mdl_common import fail, parse, round3, tri_count, world_bounds, write_json  # noqa: E402


def main():
    o = parse({'script': None, 'out': None}, required=('script', 'out'), repeat=('set',))
    script = os.path.abspath(o['script'])
    if not os.path.isfile(script):
        fail(f'File not found: {script}')
    for kv in o['set']:
        k, sep, v = kv.partition('=')
        if not sep:
            fail(f'--set wants KEY=VALUE, got {kv}')
        modelkit._OVERRIDES[k.strip()] = v.strip()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0
    sc.unit_settings.length_unit = 'METERS'
    sys.path.insert(0, os.path.dirname(script))
    try:
        runpy.run_path(script, run_name='__main__')
    except SystemExit:
        raise
    except Exception:  # show where in the model script it failed, without Blender's own frames
        tb = traceback.format_exc().splitlines()
        fail('the model script raised:\n' + '\n'.join(tb[-8:]))

    objs = [ob for ob in sc.objects if not ob.name.startswith('_mdl')]
    if not any(ob.type == 'MESH' for ob in objs):
        fail(f'{os.path.basename(script)} made no mesh objects; call part() for each piece.')
    report = {'script': script, 'params': dict(modelkit._PARAMS), 'objects': []}
    print(f"params: {', '.join(f'{k}={v}' for k, v in modelkit._PARAMS.items()) or '(none)'}")
    for ob in sorted(objs, key=lambda x: x.name):
        entry = {'name': ob.name, 'type': ob.type, 'parent': ob.parent.name if ob.parent else None}
        if ob.type == 'MESH':
            lo, hi = world_bounds([ob])
            entry.update(size_m=round3(hi - lo), tris=tri_count(ob.data),
                         materials=[m.name if m else None for m in ob.data.materials],
                         pivot_m=round3(ob.matrix_world.translation))
            print(f"{ob.name:28} {entry['size_m'][0]:.4f} x {entry['size_m'][1]:.4f} x {entry['size_m'][2]:.4f} m  "
                  f"{entry['tris']:6} tris  {', '.join(n or '-' for n in entry['materials']) or 'no material'}")
        report['objects'].append(entry)
    out = os.path.abspath(o['out'])
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0  # no .blend1 backups
    bpy.ops.wm.save_as_mainfile(filepath=out, check_existing=False)
    write_json(os.path.splitext(out)[0] + '.build.json', report)
    print(f'Saved {out}. Next: check, then preview.')


main()
