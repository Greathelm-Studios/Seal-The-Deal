"""Reopen exported files in a fresh Blender and check they match what was exported.

blender -b --factory-startup -P verify_export.py -- --manifest <export dir>/manifest.json [--no-pose]

Checks per file: every clip is present, frame counts match (within one frame), every bone name
survived, keyed bones survived, and (unless --no-pose) bone positions at the first, middle and last
frame match the working .blend within 2% of the character's height. That last check catches axis,
scale and facing mistakes, not just missing names. Exit code 0 = all passed, 3 = failures.
"""
import json
import os
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from ca_common import (action_fcurves, action_range, assign_action, fail, import_file, main_armature,  # noqa: E402
                       reset_scene, script_args, short_action_name)


def heads(arm, names):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = arm.evaluated_get(dg)
    return {n: (arm.matrix_world @ ev.pose.bones[n].head) for n in names if n in ev.pose.bones}


def find_action(acts, clip):
    for a in acts:
        if short_action_name(a.name) == clip:
            return a
    for a in acts:
        if clip in a.name:
            return a
    return None


def main():
    a = script_args()
    if '--manifest' not in a:
        fail('--manifest is required')
    mpath = a[a.index('--manifest') + 1]
    check_pose = '--no-pose' not in a
    with open(mpath, encoding='utf-8') as f:
        man = json.load(f)
    failures, lines = [], []
    sc_fps = man['fps']
    for entry in man['files']:
        path = entry['path']
        reset_scene(sc_fps)
        objs, acts = import_file(path)
        arm = main_armature(objs)
        if arm is None:
            failures.append(f'{path}: no armature')
            continue
        bone_names = [b.name for b in arm.data.bones]
        missing_bones = [b for b in man['bones'] if b not in bone_names]
        extra = [b for b in bone_names if b not in man['bones']]
        lines.append(f"{os.path.basename(path)}: {len(bone_names)} bones "
                     f"({'all names match' if not missing_bones else f'MISSING {missing_bones}'}"
                     f"{f', extra {extra}' if extra else ''}); actions: {[short_action_name(x.name) for x in acts]}")
        if missing_bones:
            failures.append(f'{path}: missing bones {missing_bones}')
        if 'meshes' in man:
            n_mesh = len([o for o in objs if o.type == 'MESH'])
            # Importers may split a mesh per material, so only presence is compared.
            ok = (n_mesh == 0) == (len(man['meshes']) == 0)
            lines.append(f"  meshes: {n_mesh} in the file, {len(man['meshes'])} in the working file -> {'ok' if ok else 'FAIL'}")
            if not ok:
                failures.append(f"{path}: {n_mesh} meshes, the working file has {len(man['meshes'])}")
        found = {}
        for clip in entry['clips']:
            exp = man['clips'][clip]
            act = find_action(acts, clip)
            if act is None:
                failures.append(f'{path}: clip {clip} missing')
                continue
            found[clip] = act
            s, e = action_range(act)
            frames = int(round(e - s))
            keyed = {fc.data_path.split('"')[1] for fc in action_fcurves(act) if fc.data_path.startswith('pose.bones')}
            lost = [b for b in exp['keyed_bones'] if b not in keyed]
            ok = abs(frames - exp['frames']) <= 1 and not lost
            lines.append(f"  {clip}: {frames} frames (expected {exp['frames']}), "
                         f"{len(keyed)} keyed bones{'' if not lost else f', LOST {lost}'} -> {'ok' if ok else 'FAIL'}")
            if not ok:
                failures.append(f'{path}: {clip} frames {frames} vs {exp["frames"]} or lost keys {lost}')
        if check_pose and found and man.get('source_blend') and os.path.isfile(man['source_blend']):
            wobjs, wacts = import_file(man['source_blend'])
            warm = main_armature(wobjs)
            names = [b.name for b in warm.data.bones]
            rename = man.get('bone_rename') or {}  # working-file name -> exported name
            zs = [(warm.matrix_world @ b.head_local).z for b in warm.data.bones]
            height = max(zs) - min(zs)
            # Appending renames clashing actions (.001); match by base name.
            wmap = {}
            for x in wacts:
                wmap.setdefault(x.name.rsplit('.', 1)[0] if x.name[-4:-3] == '.' and x.name[-3:].isdigit() else x.name, x)
            for clip, act in found.items():
                wact = wmap.get(clip)
                if wact is None:
                    continue
                exp = man['clips'][clip]
                s_imp = action_range(act)[0]
                worst = 0.0
                for f in (exp['frame_start'], (exp['frame_start'] + exp['frame_end']) // 2, exp['frame_end']):
                    assign_action(warm, wact)
                    assign_action(arm, act)
                    bpy.context.scene.frame_set(f)
                    w = heads(warm, names)
                    bpy.context.scene.frame_set(int(round(s_imp + f - exp['frame_start'])))
                    x = heads(arm, [rename.get(n, n) for n in names])
                    pairs = [(w[n], x[rename.get(n, n)]) for n in w if rename.get(n, n) in x]
                    worst = max([worst] + [(a - b).length for a, b in pairs])
                ok = worst <= 0.02 * height and len(pairs) == len(w)
                lines.append(f"  {clip}: max bone position difference vs working file "
                             f"{worst * 100:.2f} cm ({worst / height * 100:.2f}% of height) over {len(pairs)}/{len(w)} bones"
                             f" -> {'ok' if ok else 'FAIL'}")
                if not ok:
                    failures.append(f'{path}: {clip} pose differs by {worst:.3f} m')
    print('\n'.join(lines))
    if failures:
        print('VERIFY FAILED:\n  ' + '\n  '.join(failures))
        sys.exit(3)
    print(f"VERIFY PASSED: {len(man['files'])} file(s), {len(man['clips'])} clip(s), {man['engine']}")


main()
