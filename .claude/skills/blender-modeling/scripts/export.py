"""Export a model .blend as FBX for Unity.

blender -b --factory-startup -P export.py -- --blend model.blend --out <dir> [--name Name] [--split]

Settings: metres, all scaling applied and the axis change baked into the mesh (Y up, front +Z), so
Unity imports each object at scale 1 with no -90 degree rotation. --split writes one FBX per
top-level object (its children go with it) instead of one file. A baked model (mdl.mjs bake) has its
textures copied beside the FBX, which references them by file name. Writes <out>/manifest.json, which
verify.py checks the files against.
"""
import os
import shutil
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mdl_common import fail, mesh_objects, open_blend, parse, round3, tri_count, world_bounds, write_json  # noqa: E402

FBX_UNITY = dict(
    object_types={'MESH', 'EMPTY'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
    axis_forward='-Z', axis_up='Y', bake_space_transform=True, use_mesh_modifiers=True,
    mesh_smooth_type='FACE', use_triangles=False, add_leaf_bones=False, bake_anim=False,
    path_mode='AUTO', use_custom_props=False,
)
IMPORT_HINT = ('Unity: drop the FBX into Assets. Model tab: Scale Factor 1, Convert Units on, Bake Axis Conversion off. '
               '*_LOD0/_LOD1 meshes under one parent become an LOD Group. For *_Collider meshes add a '
               'MeshCollider (Convex) and remove their MeshRenderer. Materials tab: remap the named '
               'materials to your URP materials.')


def subtree(ob):
    out = [ob]
    for c in ob.children:
        out += subtree(c)
    return out


def main():
    o = parse({'blend': None, 'out': None, 'name': None}, switches=('split',), required=('blend', 'out'))
    open_blend(o['blend'])
    meshes = mesh_objects()
    if not meshes:
        fail(f"No mesh objects in {o['blend']}")
    objects = {}
    for ob in meshes:
        lo, hi = world_bounds([ob])
        objects[ob.name] = {'size_m': round3(hi - lo), 'tris': tri_count(ob.data),
                            'materials': [m.name for m in ob.data.materials if m],
                            'uv_maps': [layer.name for layer in ob.data.uv_layers],
                            'collider': bool(ob.get('mdl_collider')), 'lod': ob.get('mdl_lod', 0),
                            'parent': ob.parent.name if ob.parent else None}
    base = o['name'] or os.path.splitext(os.path.basename(o['blend']))[0]
    out_dir = os.path.abspath(o['out'])
    os.makedirs(out_dir, exist_ok=True)
    # A baked model (mdl.mjs bake) carries its textures: copy them beside the FBX and point the images there (in this
    # session only), so the FBX references them by file name. The exporter's own COPY puts them in a .fbm subfolder.
    baked = {m for ob in meshes for m in ob.data.materials if m and m.get('mdl_baked')}
    images = {n.image for m in baked for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image}
    for img in images:
        src = bpy.path.abspath(img.filepath)
        dst = os.path.join(out_dir, os.path.basename(src))
        if not os.path.isfile(src):
            fail(f'{img.name}: texture {src} not found; bake the model again')
        if not (os.path.exists(dst) and os.path.samefile(src, dst)):
            shutil.copy2(src, dst)
        img.filepath = dst
    textures = sorted(os.path.basename(img.filepath) for img in images)
    fbx = dict(FBX_UNITY, path_mode='RELATIVE', embed_textures=False) if baked else FBX_UNITY
    files = []
    roots = [ob for ob in bpy.context.scene.objects if ob.parent is None and ob.type in ('MESH', 'EMPTY')]
    batches = [(f'{base}_{r.name}', subtree(r)) for r in roots] if o.get('split') else [(base, None)]
    for fname, obs in batches:
        path = os.path.join(out_dir, f'{fname}.fbx')
        for ob in bpy.context.scene.objects:
            ob.select_set(obs is None or ob in obs)
        bpy.ops.export_scene.fbx(filepath=path, use_selection=obs is not None, **fbx)
        names = [ob.name for ob in (obs or meshes) if ob.type == 'MESH']
        files.append({'path': path, 'objects': sorted(names)})
        print(f'exported {path} ({len(names)} mesh object{"s" if len(names) != 1 else ""})')
    settings = {k: (sorted(v) if isinstance(v, set) else v) for k, v in fbx.items()}
    hint = IMPORT_HINT + (f' Baked textures {", ".join(textures)} are beside the FBX; set up the material as in '
                          'references/unity-import.md.' if textures else '')
    manifest = {'source_blend': os.path.abspath(o['blend']), 'files': files, 'objects': objects,
                'settings': settings, 'import_hint': hint}
    if textures:
        manifest['textures'] = textures
        print(f'textures copied: {", ".join(textures)}')
    write_json(os.path.join(out_dir, 'manifest.json'), manifest)
    print(hint)


main()
