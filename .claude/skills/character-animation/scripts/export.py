"""Export a retargeted working .blend for a game engine.

blender -b --factory-startup -P export.py -- --blend work.blend --engine unreal|unity|godot --out <dir>
    [--split]              one file per clip (each with the mesh) instead of one bundled file
    [--clips a,b]          subset of the work file's clips
    [--name Character]     base file name (default: the .blend name)
    [--bone-names ue|mixamo|cc]  rename the mapped bones to Unreal mannequin, Mixamo or Character
                           Creator names in the exported files (the working file is not changed)
    [--no-mesh]            skeleton and clips only, for adding clips to a character already imported
    [--mesh-only]          character in its rest pose, no clips

Writes the files plus <out>/manifest.json, which verify_export.py checks against.
"""
import json
import os
import re
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from ca_common import (CANONICAL, SIDES, action_fcurves, action_range, assign_action, fail,  # noqa: E402
                       main_armature, saved_bone_map, script_args, write_json)


def _scheme(fixed, sided):
    """fixed: {canonical: name}; sided(stem, side) -> name for every per-side canonical bone."""
    out = dict(fixed)
    for c in CANONICAL:
        if c.endswith(tuple(f'_{s}' for s in SIDES)):
            out[c] = sided(c[:-2], c[-1])
    return out


MIXAMO = {'clavicle': 'Shoulder', 'upperarm': 'Arm', 'lowerarm': 'ForeArm', 'hand': 'Hand',
          'thigh': 'UpLeg', 'calf': 'Leg', 'foot': 'Foot', 'ball': 'ToeBase'}
CC = {'clavicle': 'Clavicle', 'upperarm': 'Upperarm', 'lowerarm': 'Forearm', 'hand': 'Hand',
      'thigh': 'Thigh', 'calf': 'Calf', 'foot': 'Foot', 'ball': 'ToeBase'}
CC_FINGER = {'thumb': 'Thumb', 'index': 'Index', 'middle': 'Mid', 'ring': 'Ring', 'pinky': 'Pinky'}
# Canonical bone -> name, per naming scheme. Only names change: the hierarchy, rest pose and
# proportions stay the character's own, and bones outside the map keep their names.
BONE_NAMES = {
    # The library already uses mannequin names, apart from the head (UE4 and UE5 call it 'head').
    'ue': _scheme({'root': 'root', 'pelvis': 'pelvis', 'spine_01': 'spine_01', 'spine_02': 'spine_02',
                   'spine_03': 'spine_03', 'neck_01': 'neck_01', 'Head': 'head'},
                  lambda stem, s: f'{stem}_{s}'),
    'mixamo': _scheme({'pelvis': 'mixamorig:Hips', 'spine_01': 'mixamorig:Spine', 'spine_02': 'mixamorig:Spine1',
                       'spine_03': 'mixamorig:Spine2', 'neck_01': 'mixamorig:Neck', 'Head': 'mixamorig:Head'},
                      lambda stem, s: 'mixamorig:{}{}'.format(
                          'Left' if s == 'l' else 'Right',
                          MIXAMO.get(stem) or f"Hand{stem.split('_')[0].title()}{int(stem.split('_')[1])}")),
    'cc': _scheme({'root': 'CC_Base_BoneRoot', 'pelvis': 'CC_Base_Hip', 'spine_01': 'CC_Base_Waist',
                   'spine_02': 'CC_Base_Spine01', 'spine_03': 'CC_Base_Spine02', 'neck_01': 'CC_Base_NeckTwist01',
                   'Head': 'CC_Base_Head'},
                  lambda stem, s: 'CC_Base_{}_{}'.format(
                      s.upper(), CC.get(stem) or f"{CC_FINGER[stem.split('_')[0]]}{int(stem.split('_')[1])}")),
}
BONE_PATH = re.compile(r'^pose\.bones\["(.+?)"\](.*)$')

FBX_COMMON = dict(
    use_selection=False, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
    primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=False,
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True, global_scale=1.0,
    bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
    mesh_smooth_type='FACE', use_mesh_modifiers=False, path_mode='COPY', embed_textures=True,
)
PRESETS = {
    # Unreal 5: FBX, metres converted to centimetres by the FBX unit scale. The armature object is
    # named 'Armature' so Unreal does not add an extra root bone above the skeleton's own root.
    'unreal': {'format': 'fbx', 'armature_name': 'Armature',
               'fbx': dict(FBX_COMMON, apply_scale_options='FBX_SCALE_NONE'),
               'import_hint': 'Import as Skeletal Mesh; for split files pick the existing Skeleton. '
                              'Enable root motion on the Anim Sequence if the clips were retargeted with --root-motion.'},
    # Unity: FBX with all scaling applied so the model imports at scale 1 with no 100x root scale.
    'unity': {'format': 'fbx', 'armature_name': None,
              'fbx': dict(FBX_COMMON, apply_scale_options='FBX_SCALE_ALL'),
              'import_hint': 'Rig tab: Humanoid (or Generic), Avatar from this model. Animation tab: '
                             'tick Loop Time on looping clips; Bake Into Pose for in-place clips.'},
    # Godot 4: binary glTF, Y-up, one animation per action, sampled every frame.
    'godot': {'format': 'glb', 'armature_name': None,
              'gltf': dict(export_format='GLB', export_yup=True, export_apply=False,
                           export_animations=True, export_force_sampling=True, export_frame_step=1,
                           export_skins=True, export_def_bones=False, export_optimize_animation_size=False,
                           export_reset_pose_bones=True, export_materials='EXPORT'),
              'import_hint': 'Drop the .glb into the project; animations appear on the AnimationPlayer. '
                             'Set loop mode on *_Loop clips in the Advanced Import Settings.'},
}


def parse():
    a = script_args()
    o = {'blend': None, 'engine': None, 'out': None, 'split': False, 'clips': None, 'name': None,
         'bone_names': None, 'no_mesh': False, 'mesh_only': False}
    switches = {'--split': 'split', '--no-mesh': 'no_mesh', '--mesh-only': 'mesh_only'}
    i = 0
    while i < len(a):
        k = a[i]
        key = k[2:].replace('-', '_')
        if k in switches:
            o[switches[k]] = True
        elif k.startswith('--') and key in o and i + 1 < len(a):
            o[key] = a[i + 1]
            i += 1
        else:
            fail(f'Unknown or incomplete argument: {k}')
        i += 1
    if not (o['blend'] and o['engine'] and o['out']):
        fail('--blend, --engine and --out are required')
    if o['engine'] not in PRESETS:
        fail(f"--engine must be one of {sorted(PRESETS)}")
    if o['bone_names'] and o['bone_names'] not in BONE_NAMES:
        fail(f"--bone-names must be one of {sorted(BONE_NAMES)}")
    if o['no_mesh'] and o['mesh_only']:
        fail('--no-mesh and --mesh-only exclude each other')
    if o['mesh_only'] and (o['split'] or o['clips']):
        fail('--mesh-only exports no clips, so it takes neither --split nor --clips')
    return o


def rename_bones(arm, mapping, scheme, actions):
    """Rename mapped bones to a naming scheme, in this (unsaved) session only. -> {old: new}.

    Blender renames the vertex groups with the bones; the clips' curves are repointed here, since
    Blender only fixes the curves of the action that is playing."""
    names = BONE_NAMES[scheme]
    rename = {b: names[c] for c, b in mapping.items() if c in names and names[c] != b}
    clash = sorted(n for n in set(rename.values()) if n in arm.data.bones and n not in rename)
    if clash:
        fail(f"--bone-names {scheme}: the character already has bones named {clash} that are not the "
             f"mapped bones for those names. Its rig may already use this naming; export without --bone-names.")
    curves, groups = [], {}
    for act in actions:
        for fc in action_fcurves(act):
            m = BONE_PATH.match(fc.data_path)
            if m and m.group(1) in rename:
                curves.append((fc, rename[m.group(1)], m.group(2)))
            if fc.group and fc.group.name in rename:
                groups[fc.group.as_pointer()] = (fc.group, rename[fc.group.name])
    if arm.animation_data:
        arm.animation_data.action = None
    # Two passes through temporary names, so a scheme may reuse a name another bone gives up.
    for i, old in enumerate(rename):
        arm.data.bones[old].name = f'__ca_rename_{i}'
    for i, new in enumerate(rename.values()):
        arm.data.bones[f'__ca_rename_{i}'].name = new
    for fc, new, tail in curves:
        fc.data_path = f'pose.bones["{new}"]{tail}'
    for group, new in groups.values():
        group.name = new
    return rename


def export_file(preset, path, arm, clip_actions, sc):
    """Export every action left in the file. Split exports call this on a copy holding one clip, so
    the clip keeps its own name (single-action modes name it 'Animation' or after the scene).
    With no clips (--mesh-only) the character is written in its rest pose without animation."""
    if clip_actions:
        assign_action(arm, clip_actions[0])
        sc.frame_start = int(min(action_range(a)[0] for a in clip_actions))
        sc.frame_end = int(max(action_range(a)[1] for a in clip_actions))
    if preset['format'] == 'fbx':
        bpy.ops.export_scene.fbx(filepath=path, bake_anim_use_all_actions=True,
                                 **dict(preset['fbx'], bake_anim=bool(clip_actions)))
    else:
        bpy.ops.export_scene.gltf(filepath=path, export_animation_mode='ACTIONS',
                                  **dict(preset['gltf'], export_animations=bool(clip_actions)))


def prepare(o, preset, keep=None):
    """Open the work file fresh and leave only the clips (and objects) to export.
    -> (arm, scene, actions, {old bone name: new})."""
    bpy.ops.wm.open_mainfile(filepath=os.path.abspath(o['blend']))
    sc = bpy.context.scene
    arm = main_armature(list(bpy.data.objects))
    if arm is None or 'ca_clips' not in arm:
        fail(f"{o['blend']} is not a retarget working file (no armature with ca_clips). Run retarget first.")
    clips = json.loads(arm['ca_clips'])
    if o['clips']:
        wanted = [c.strip() for c in o['clips'].split(',')]
        bad = [c for c in wanted if c not in clips]
        if bad:
            fail(f'Not in the work file: {bad}. Available: {clips}')
        clips = wanted
    if keep is not None:
        clips = [c for c in clips if c in keep]
    if o['mesh_only']:
        clips = []
    mapping = saved_bone_map(arm, o['blend']) if o['bone_names'] else None
    # Only the chosen clips may be exported: drop every other action and NLA strip.
    for a in list(bpy.data.actions):
        if a.name not in clips:
            bpy.data.actions.remove(a)
    if arm.animation_data:
        arm.animation_data.action = None
        for t in list(arm.animation_data.nla_tracks):
            arm.animation_data.nla_tracks.remove(t)
    for pb in arm.pose.bones:
        pb.matrix_basis.identity()
    if o['no_mesh']:
        for ob in [ob for ob in bpy.data.objects if ob.type == 'MESH']:
            bpy.data.objects.remove(ob, do_unlink=True)
    actions = [bpy.data.actions[c] for c in clips]
    rename = rename_bones(arm, mapping, o['bone_names'], actions) if mapping else {}
    if preset['armature_name'] and arm.name != preset['armature_name']:
        clash = bpy.data.objects.get(preset['armature_name'])
        if clash:
            clash.name = clash.name + '_other'
        arm.name = preset['armature_name']
    return arm, sc, actions, rename


def main():
    o = parse()
    preset = PRESETS[o['engine']]
    arm, sc, actions, rename = prepare(o, preset)
    fps = sc.render.fps
    clip_info = {}
    for a in actions:
        s, e = action_range(a)
        keyed = sorted({fc.data_path.split('"')[1] for fc in action_fcurves(a)
                        if fc.data_path.startswith('pose.bones')})
        clip_info[a.name] = {'frame_start': int(s), 'frame_end': int(e), 'frames': int(round(e - s)),
                             'duration_s': round((e - s) / fps, 4), 'keyed_bones': keyed}
    arm_name, bones = arm.name, [b.name for b in arm.data.bones]
    meshes = sorted(ob.name for ob in bpy.data.objects if ob.type == 'MESH')
    base = o['name'] or os.path.splitext(os.path.basename(o['blend']))[0]
    out_dir = os.path.abspath(o['out'])
    os.makedirs(out_dir, exist_ok=True)
    ext = preset['format']
    files = []
    if o['split']:
        for clip in list(clip_info):
            arm, sc, acts, _ = prepare(o, preset, keep=[clip])
            path = os.path.join(out_dir, f'{base}_{clip}.{ext}')
            export_file(preset, path, arm, acts, sc)
            files.append({'path': path, 'clips': [clip]})
    else:
        path = os.path.join(out_dir, f'{base}.{ext}')
        export_file(preset, path, arm, actions, sc)
        files.append({'path': path, 'clips': list(clip_info)})
    manifest = {'engine': o['engine'], 'format': ext, 'fps': fps, 'split': o['split'],
                'source_blend': os.path.abspath(o['blend']),
                'armature': arm_name, 'bones': bones, 'meshes': meshes,
                'bone_names': o['bone_names'], 'bone_rename': rename,
                'files': files, 'clips': clip_info, 'import_hint': preset['import_hint'],
                'settings': preset.get('fbx') or preset.get('gltf')}
    manifest['settings'] = {k: (sorted(v) if isinstance(v, set) else v) for k, v in manifest['settings'].items()}
    write_json(os.path.join(out_dir, 'manifest.json'), manifest)
    for f in files:
        print(f"exported {f['path']} ({len(f['clips'])} clip{'s' if len(f['clips']) != 1 else ''}"
              f"{', no mesh' if o['no_mesh'] else ''}{', rest pose only' if o['mesh_only'] else ''})")
    if rename:
        print(f"renamed {len(rename)} bones to {o['bone_names']} names (e.g. "
              f"{', '.join(f'{k} -> {v}' for k, v in list(rename.items())[:3])})")
    print(f"{o['engine']}: {preset['import_hint']}")


main()
