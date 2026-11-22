"""Report a character's skeleton and whether it maps onto the library rig.

blender -b --factory-startup -P inspect_character.py -- --file <character.fbx|glb|blend> [--map auto|map.json] [--json out.json]

Exit code 0 when every required bone maps, 2 when some are missing (they are listed).
"""
import os
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ca_common import (CANONICAL, REQUIRED, classify_rig, fail, import_file, load_bone_map,  # noqa: E402
                       main_armature, missing_required, reset_scene, rest_facts, script_args,
                       short_action_name, skinned_meshes, write_json)


def main():
    args = script_args()
    opts = {'--file': None, '--map': 'auto', '--json': None}
    for i in range(0, len(args), 2):
        if args[i] not in opts or i + 1 >= len(args):
            fail(f'Usage: --file <character> [--map auto|map.json] [--json out.json]; got {args}')
        opts[args[i]] = args[i + 1]
    if not opts['--file']:
        fail('--file is required')
    reset_scene()
    objs, acts = import_file(opts['--file'])
    arm = main_armature(objs)
    if arm is None:
        fail(f'No armature in {opts["--file"]}: the character must be rigged.')
    mapping, notes = load_bone_map(arm, opts['--map'])
    missing = missing_required(mapping)
    bones = arm.data.bones
    report = {
        'file': os.path.abspath(opts['--file']),
        'armature': arm.name,
        'rig_type': classify_rig(arm),
        'bone_count': len(bones),
        'meshes': [m.name for m in skinned_meshes(arm)],
        'other_objects': [o.name for o in objs if o != arm and o not in skinned_meshes(arm)],
        'existing_actions': [short_action_name(a.name) for a in acts],
        'rest': rest_facts(arm, mapping),
        'mapping': mapping,
        'mapped_count': len(mapping),
        'missing_required': missing,
        'unmapped_optional': [c for c in CANONICAL if c not in mapping and c not in REQUIRED],
        'unused_bones': [b.name for b in bones if b.name not in mapping.values()],
        'notes': notes,
        'hierarchy': {b.name: (b.parent.name if b.parent else None) for b in bones},
        'ok': not missing,
    }
    if opts['--json']:
        write_json(opts['--json'], report)
    r = report['rest']
    print(f"Armature '{arm.name}': {len(bones)} bones, rig type {report['rig_type']}")
    print(f"Meshes: {', '.join(report['meshes']) or 'none'}; other objects: {', '.join(report['other_objects']) or 'none'}")
    print(f"Rest: height {r.get('height_m')} m, facing {r.get('facing', '?')}, {r.get('rest_pose', '?')} "
          f"(arm {r.get('arm_angle_from_down_deg', '?')} deg from down)")
    if report['existing_actions']:
        print(f"Existing actions ({len(acts)}): {', '.join(report['existing_actions'])}")
    print(f'Mapped {len(mapping)}/{len(CANONICAL)} library bones:')
    for c, b in mapping.items():
        print(f'  {c:14s} <- {b}')
    for n in notes:
        print(f'  note: {n}')
    h = r.get('height_m') or 0
    if h and not 0.3 <= h <= 3.0:
        print(f'WARNING: character is {h} m tall; engines will import it at that size.')
    if r.get('facing') not in (None, '-Y'):
        print(f"WARNING: character faces {r.get('facing')}; the engine presets expect -Y (Blender front).")
    if missing:
        print(f'NOT RETARGETABLE: required bones not found: {", ".join(missing)}')
        print('Fix: pass --map <file.json> with {"canonical_name": "your_bone"} for those entries.')
        sys.exit(2)
    print('OK: all required bones map onto the library rig.')


main()
