"""Reopen every exported FBX in a fresh scene and compare it with what export recorded.

blender -b --factory-startup -P verify.py -- --manifest <dir>/manifest.json

Per object: present, same triangle count, same materials, the same UV maps in the same order (Unity reads
the second as the lightmap UVs), and the same size within 0.5 mm. For a baked
model, also that each texture is in the export folder and referenced by the FBX. The
FBX is read back with Blender's importer, which undoes the axis change, so sizes compare in
Blender's axes. This proves the files hold the right geometry at the right scale; it does not run
Unity's importer (see SKILL.md for checking the import in Unity itself).
"""
import json
import os
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mdl_common import fail, parse, round3, tri_count, world_bounds  # noqa: E402

TOL = 0.0005


def main():
    o = parse({'manifest': None}, required=('manifest',))
    if not os.path.isfile(o['manifest']):
        fail(f"File not found: {o['manifest']}")
    with open(o['manifest'], encoding='utf-8') as f:
        man = json.load(f)
    problems = 0
    for entry in man['files']:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=entry['path'])
        got = {ob.name: ob for ob in bpy.context.scene.objects if ob.type == 'MESH'}
        print(os.path.basename(entry['path']))
        for name in entry['objects']:
            want = man['objects'][name]
            ob = got.get(name)
            if ob is None:
                print(f'  {name}: MISSING (file has {sorted(got)})')
                problems += 1
                continue
            lo, hi = world_bounds([ob])
            size = round3(hi - lo)
            issues = []
            if max(abs(a - b) for a, b in zip(size, want['size_m'])) > TOL:
                issues.append(f"size {size} m, expected {want['size_m']} m")
            if tri_count(ob.data) != want['tris']:
                issues.append(f"{tri_count(ob.data)} tris, expected {want['tris']}")
            mats = [m.name for m in ob.data.materials if m]
            if mats != want['materials']:
                issues.append(f"materials {mats}, expected {want['materials']}")
            # Unity takes UV maps by order: the first is UV0 (textures), the second UV1 (lightmaps).
            maps = [layer.name for layer in ob.data.uv_layers]
            if 'uv_maps' in want and maps != want['uv_maps']:
                issues.append(f"UV maps {maps}, expected {want['uv_maps']} in that order")
            problems += len(issues)
            print(f"  {name}: {'; '.join(issues) if issues else 'ok'} "
                  f"({size[0]:.4f} x {size[1]:.4f} x {size[2]:.4f} m, {want['tris']} tris)")
        if man.get('textures'):  # a baked model: the FBX must point at the textures copied beside it
            used = {os.path.basename(bpy.path.abspath(img.filepath)).lower() for img in bpy.data.images if img.filepath}
            for tex in man['textures']:
                if not os.path.isfile(os.path.join(os.path.dirname(entry['path']), tex)):
                    print(f'  texture {tex}: MISSING from the export folder')
                    problems += 1
                elif tex.lower() not in used:
                    print(f'  texture {tex}: not referenced by the FBX')
                    problems += 1
                else:
                    print(f'  texture {tex}: ok')
        extra = sorted(set(got) - set(entry['objects']))
        if extra:
            print(f'  unexpected objects in the file: {extra}')
            problems += 1
    print('VERIFY FAILED' if problems else f"VERIFY PASSED: {len(man['files'])} file(s), {len(man['objects'])} object(s)")
    sys.exit(1 if problems else 0)


main()
