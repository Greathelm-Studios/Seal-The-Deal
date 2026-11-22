"""Render contact sheets (and optionally MP4s) of a character's clips so the result can be looked at.

blender -b --factory-startup -P preview.py -- --file <work.blend|export.fbx|export.glb> --out <dir>
    [--clips a,b] [--frames 6] [--cell 360x440] [--mp4] [--follow] [--first-person]
    [--prop sword.glb [--hand r] [--prop-rotate forward:90] [--prop-move up:0.02]]

Per clip: <out>/<clip>_sheet.png, two rows (three-quarter front view, left side view) by N frames,
each stamped with clip, view and frame. A checker floor sits at the character's rest ground level.
--first-person adds a third row seen from the eyes, looking the way the character faces at rest.
--follow makes the MP4 camera travel with the hips (root-motion clips stay in frame).
--prop puts a model in a hand: it is placed at the palm in the rest pose as it was modelled, turned
and moved by --prop-rotate / --prop-move (the character's axes: up, left, forward and opposites;
degrees and metres), then follows the hand bone.
Also writes <out>/preview.json with numbers that back up the pictures: lowest point above the
floor per frame, arm angle range, and whether the character still faces its rest direction.
"""
import json
import math
import os
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import bmesh  # noqa: E402
import numpy as np  # noqa: E402  (ships with Blender)
from mathutils import Matrix, Vector  # noqa: E402
from ca_common import (action_range, assign_action, axis_spec, detect_bone_map, fail, import_file,  # noqa: E402
                       main_armature, reset_scene, rest_facts, script_args, short_action_name, skinned_meshes,
                       write_json)


def parse():
    a = script_args()
    o = {'file': None, 'out': None, 'clips': None, 'frames': '6', 'cell': '360x440', 'mp4': False,
         'follow': False, 'first_person': False, 'prop': None, 'hand': 'r', 'prop_rotate': None, 'prop_move': None}
    switches = {'--mp4': 'mp4', '--follow': 'follow', '--first-person': 'first_person'}
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
    if not o['file'] or not o['out']:
        fail('--file and --out are required')
    if o['hand'] not in ('l', 'r'):
        fail('--hand must be l or r')
    if o['follow'] and not o['mp4']:
        fail('--follow moves the MP4 camera; add --mp4 (the sheets always follow the hips)')
    return o


def attach_prop(o, arm, mapping, axes):
    """Import the prop, stand it at the palm in the rest pose, turn and move it, and parent it to
    the hand bone so it follows the clip. -> the prop's mesh objects."""
    side = o['hand']
    hand = mapping.get(f'hand_{side}')
    if hand is None:
        fail(f'The character has no hand_{side} bone in its map; cannot hold a prop.')
    objs, acts = import_file(o['prop'])
    for a in acts:
        bpy.data.actions.remove(a)
    tops = [ob for ob in objs if ob.parent is None]
    meshes = [ob for ob in objs if ob.type == 'MESH']
    if not meshes:
        fail(f"No mesh in {o['prop']}")
    mw = arm.matrix_world
    palm = mw @ arm.data.bones[hand].head_local
    mid = mapping.get(f'middle_01_{side}')
    if mid:
        palm = (palm + mw @ arm.data.bones[mid].head_local) / 2
    turn = Matrix.Identity(3)
    for ax, deg in (axis_spec(axes, o['prop_rotate'], 'prop-rotate') if o['prop_rotate'] else []):
        turn = Matrix.Rotation(math.radians(deg), 3, ax) @ turn
    move = sum((ax * val for ax, val in axis_spec(axes, o['prop_move'], 'prop-move')), Vector()) if o['prop_move'] else Vector()
    place = Matrix.Translation(palm + move) @ turn.to_4x4()
    wanted = {ob: place @ ob.matrix_world for ob in tops}
    for ob in tops:
        ob.parent, ob.parent_type, ob.parent_bone = arm, 'BONE', hand
        ob.matrix_parent_inverse.identity()
    bpy.context.view_layer.update()
    for ob, m in wanted.items():
        ob.matrix_world = m
    for ob in objs:
        ob.hide_render = ob.type != 'MESH'
    for m in meshes:
        m.color = (0.86, 0.64, 0.32, 1)
    return meshes


def load(path):
    if path.lower().endswith('.blend'):
        bpy.ops.wm.open_mainfile(filepath=path)
        objs = list(bpy.data.objects)
        acts = list(bpy.data.actions)
    else:
        reset_scene()
        objs, acts = import_file(path)
    arm = main_armature(objs)
    if arm is None:
        fail(f'No armature in {path}')
    # Hide anything that is not the character.
    keep = {arm, *skinned_meshes(arm)}
    for ob in bpy.data.objects:
        if ob not in keep:
            ob.hide_render = True
    return arm, acts


def make_floor(ground_z, size, center):
    """Checker floor from two meshes (light and dark tiles)."""
    n = 12
    tile = size / n
    for shade, parity in (('floor_light', 0), ('floor_dark', 1)):
        me = bpy.data.meshes.new(shade)
        bm = bmesh.new()
        for i in range(n):
            for j in range(n):
                if (i + j) % 2 != parity:
                    continue
                x0 = center.x - size / 2 + i * tile
                y0 = center.y - size / 2 + j * tile
                vs = [bm.verts.new((x0 + dx, y0 + dy, ground_z)) for dx, dy in ((0, 0), (tile, 0), (tile, tile), (0, tile))]
                bm.faces.new(vs)
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(shade, me)
        ob.color = (0.78, 0.78, 0.76, 1) if parity == 0 else (0.55, 0.56, 0.58, 1)
        bpy.context.scene.collection.objects.link(ob)


def setup_render(cell_w, cell_h):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'
    sh.color_type = 'OBJECT'
    sh.show_shadows = True
    sh.shadow_intensity = 0.6
    sh.show_cavity = True
    sc.display.light_direction = (0.35, -0.45, 0.82)
    sc.render.resolution_x, sc.render.resolution_y = cell_w, cell_h
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    if sc.world is None:
        sc.world = bpy.data.worlds.new('World')
    sc.world.color = (0.16, 0.17, 0.2)
    r = sc.render
    r.use_stamp = True
    for flag in ('date', 'time', 'render_time', 'scene', 'camera', 'lens', 'filename', 'memory',
                 'hostname', 'frame_range', 'marker', 'sequencer_strip'):
        if hasattr(r, f'use_stamp_{flag}'):
            setattr(r, f'use_stamp_{flag}', False)
    r.use_stamp_frame = True
    r.use_stamp_note = True
    r.stamp_font_size = 14
    r.image_settings.file_format = 'PNG'


def new_camera(name):
    cam = bpy.data.cameras.new(name)
    cam.type = 'ORTHO'
    ob = bpy.data.objects.new(name, cam)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def aim(cam, target, direction, dist):
    cam.location = target + direction.normalized() * dist
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()


def evaluated_lowest_z(meshes):
    dg = bpy.context.evaluated_depsgraph_get()
    low = None
    for m in meshes:
        ev = m.evaluated_get(dg)
        me = ev.to_mesh()
        if len(me.vertices):
            co = np.empty(len(me.vertices) * 3, dtype=np.float32)
            me.vertices.foreach_get('co', co)
            co = co.reshape(-1, 3)
            mw = np.array(ev.matrix_world)
            z = co @ mw[2, :3] + mw[2, 3]
            low = float(z.min()) if low is None else min(low, float(z.min()))
        ev.to_mesh_clear()
    return low


def compose(paths, rows, cols, w, h, out_path):
    sheet = np.zeros((rows * h, cols * w, 4), dtype=np.float32)
    for r in range(rows):
        for c in range(cols):
            img = bpy.data.images.load(paths[r][c])
            px = np.empty(w * h * 4, dtype=np.float32)
            img.pixels.foreach_get(px)
            y0 = (rows - 1 - r) * h  # Blender images start at the bottom row
            sheet[y0:y0 + h, c * w:(c + 1) * w] = px.reshape(h, w, 4)
            bpy.data.images.remove(img)
    out = bpy.data.images.new(os.path.basename(out_path), cols * w, rows * h, alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = out_path
    out.file_format = 'PNG'
    out.save()
    bpy.data.images.remove(out)


def main():
    o = parse()
    cell_w, cell_h = (int(v) for v in o['cell'].lower().split('x'))
    n_frames = int(o['frames'])
    out_dir = os.path.abspath(o['out'])
    frames_dir = os.path.join(out_dir, '_frames')
    os.makedirs(frames_dir, exist_ok=True)
    arm, acts = load(o['file'])
    for pb in arm.pose.bones:
        pb.matrix_basis.identity()
    mapping, _ = detect_bone_map(arm)
    if arm.animation_data:
        arm.animation_data.action = None
    bpy.context.view_layer.update()
    facts = rest_facts(arm, mapping)
    meshes = skinned_meshes(arm)
    height = facts['height_m']
    ground = facts['ground_z']
    fwd = Vector(facts['forward'])
    left = Vector((0, 0, 1)).cross(fwd)  # character's left
    pelvis = arm.pose.bones[mapping['pelvis']]
    if o['prop']:
        attach_prop(o, arm, mapping, {'up': Vector((0, 0, 1)), 'left': left, 'forward': fwd})
    head = arm.pose.bones[mapping['Head']] if o['first_person'] else None
    if head:
        # Eyes in the head bone's own space, from the rest pose, so the camera moves with the head.
        eye_rest = arm.matrix_world @ head.bone.head_local + Vector((0, 0, 0.055 * height)) + fwd * (0.04 * height)
        eye_local = (arm.matrix_world @ head.bone.matrix_local).inverted() @ eye_rest

    # Clip list: requested, else the work file's own list, else every action.
    names = {short_action_name(a.name): a for a in acts}
    if o['clips']:
        wanted = [c.strip() for c in o['clips'].split(',')]
    elif 'ca_clips' in arm:
        wanted = json.loads(arm['ca_clips'])
    else:
        wanted = list(names)
    missing = [c for c in wanted if c not in names]
    if missing:
        fail(f'Clips not found in {o["file"]}: {missing}. Available: {sorted(names)}')

    setup_render(cell_w, cell_h)
    center0 = arm.matrix_world @ pelvis.head
    make_floor(ground, height * 8, Vector((center0.x, center0.y, 0)))
    for m in meshes:
        m.color = (0.62, 0.72, 0.86, 1)
    sc = bpy.context.scene
    views = [('three-quarter', (fwd * math.cos(math.radians(35)) + left * math.sin(math.radians(35))) + Vector((0, 0, 0.25))),
             ('side', left + Vector((0, 0, 0.05)))]
    if head:
        # Looks the way the character faces at rest, tipped down, like a game's first-person camera.
        views.append(('first-person', fwd * math.cos(math.radians(25)) - Vector((0, 0, math.sin(math.radians(25))))))
    cams = {name: new_camera(name) for name, _ in views}
    for c in cams.values():
        c.data.ortho_scale = height * 1.45
    if head:
        fp = cams['first-person'].data
        fp.type = 'PERSP'
        fp.angle = math.radians(90)
        fp.clip_start = 0.07 * height  # cuts away the inside of the head, as games hide it
    stats = {}
    for clip in wanted:
        act = names[clip]
        assign_action(arm, act)
        s, e = action_range(act)
        sample = [s + (e - s) * i / max(n_frames - 1, 1) for i in range(n_frames)]
        paths = [[None] * n_frames for _ in views]
        lows, arm_angles, facing, hips = [], [], [], []
        for ci, f in enumerate(sample):
            fi = int(round(f))
            sc.frame_set(fi)
            p = arm.matrix_world @ pelvis.head
            hips.append(p.copy())
            target = Vector((p.x, p.y, ground + height * 0.52))
            for vi, (vname, d) in enumerate(views):
                cam = cams[vname]
                if vname == 'first-person':
                    aim(cam, arm.matrix_world @ head.matrix @ eye_local + d, -d, 1.0)
                else:
                    aim(cam, target, d, height * 4)
                sc.camera = cam
                sc.render.stamp_note_text = f'{clip}  {vname}'
                path = os.path.join(frames_dir, f'{clip}_{vname}_{fi:04d}.png')
                sc.render.filepath = path
                bpy.ops.render.render(write_still=True)
                paths[vi][ci] = path
            low = evaluated_lowest_z(meshes)
            if low is not None:
                lows.append(round(low - ground, 3))
            if 'upperarm_l' in mapping and 'lowerarm_l' in mapping:
                ua = arm.matrix_world @ arm.pose.bones[mapping['upperarm_l']].head
                la = arm.matrix_world @ arm.pose.bones[mapping['lowerarm_l']].head
                arm_angles.append(round(math.degrees(Vector((0, 0, -1)).angle((la - ua).normalized())), 1))
            if 'thigh_l' in mapping and 'thigh_r' in mapping:
                lft = (arm.matrix_world @ arm.pose.bones[mapping['thigh_l']].head) - (arm.matrix_world @ arm.pose.bones[mapping['thigh_r']].head)
                lft.z = 0
                if lft.length > 1e-6:
                    facing.append(round(math.degrees(lft.normalized().cross(Vector((0, 0, 1))).angle(fwd)), 1))
        travel = hips[-1] - hips[0]
        travel.z = 0
        sheet = os.path.join(out_dir, f'{clip}_sheet.png')
        compose(paths, len(views), n_frames, cell_w, cell_h, sheet)
        stats[clip] = {'sheet': sheet, 'frames_sampled': [int(round(f)) for f in sample],
                       'lowest_point_above_floor_m': lows, 'left_arm_angle_from_down_deg': arm_angles,
                       'hips_yaw_off_rest_deg': facing,
                       'hips_travel_m': round(travel.length, 3),
                       'travel_direction_vs_facing_deg': round(math.degrees(travel.angle(fwd)), 1) if travel.length > 0.05 else None}
        print(f'{clip}: {sheet}')
        print(f'  lowest point above floor (m): {lows}')
        print(f'  left arm angle from down (deg): {arm_angles}')
        print(f'  hips turned from rest facing (deg): {facing}')
        if travel.length > 0.05:
            print(f'  root motion: hips travel {travel.length:.2f} m, '
                  f'{math.degrees(travel.angle(fwd)):.0f} deg from the facing direction (0 = forward)')
        else:
            print('  in place (no horizontal travel)')
        if o['mp4']:
            mp4 = os.path.join(out_dir, f'{clip}.mp4')
            r = sc.render
            r.use_stamp = False
            cam = cams['three-quarter']
            cam.data.ortho_scale = height * 1.45
            if o['follow']:
                for f in range(int(s), int(e) + 1):
                    sc.frame_set(f)
                    p = arm.matrix_world @ pelvis.head
                    aim(cam, Vector((p.x, p.y, ground + height * 0.52)), views[0][1], height * 4)
                    cam.keyframe_insert('location', frame=f)
                    cam.keyframe_insert('rotation_euler', frame=f)
            else:
                aim(cam, Vector((center0.x, center0.y, ground + height * 0.52)), views[0][1], height * 4)
            sc.camera = cam
            sc.frame_start, sc.frame_end = int(s), int(e)
            if hasattr(r.image_settings, 'media_type'):
                r.image_settings.media_type = 'VIDEO'
            r.image_settings.file_format = 'FFMPEG'
            r.ffmpeg.format = 'MPEG4'
            r.ffmpeg.codec = 'H264'
            r.filepath = mp4
            bpy.ops.render.render(animation=True)
            if hasattr(r.image_settings, 'media_type'):
                r.image_settings.media_type = 'IMAGE'
            r.image_settings.file_format = 'PNG'
            r.use_stamp = True
            cam.animation_data_clear()  # the next clip's sheet places this camera by hand
            stats[clip]['mp4'] = mp4
            print(f'  video: {mp4}')
    write_json(os.path.join(out_dir, 'preview.json'), {'file': os.path.abspath(o['file']), 'rest': facts, 'clips': stats})


main()
