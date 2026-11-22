"""Retarget clips onto a rigged character and save a working .blend.

blender -b --factory-startup -P retarget.py -- --character <file> --clips Idle_Loop,Walk_Loop --out work.blend
    [--source <clips.fbx|glb>]   take clips from this file instead of the library (AI route, other packs);
                                 --clips all takes every action in it
    [--root-motion]              library only: use the root-motion variant of each clip
    [--in-place]                 remove horizontal travel (for sources that move)
    [--fps 30]                   output frame rate (source is resampled by time)
    [--map auto|map.json]        character bone map override; [--source-map ...] for the source
    [--keep-existing-actions]    keep the character's own actions in the working file
    [--add-to work.blend]        add the clips to this working file instead of starting one from
                                 --character, so library, AI and other clips share one file; saves in
                                 place unless --out is given. Frame rate and bone map come from the file
    [--replace]                  with --add-to: overwrite clips of the same name

Method: world-space rotation transfer. Each mapped bone takes the source bone's change from its rest
pose, after aligning the two rest poses (facing, T vs A pose) and scaling hip motion by leg length.
Fails with a list of missing bones instead of producing a broken result.
"""
import difflib
import json
import math
import os
import re
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Matrix, Quaternion, Vector  # noqa: E402
from ca_common import (CANONICAL, CANON_AIM, CANON_PARENT, DEFAULT_CACHE, LIBRARY_FPS, REQUIRED,  # noqa: E402
                       action_range, assign_action, fail, import_file, load_bone_map, main_armature,
                       remove_unrelated, reset_scene, rest_facts, saved_bone_map, script_args, set_fps,
                       short_action_name, write_action, write_json)


def parse():
    a = script_args()
    o = {'character': None, 'clips': None, 'out': None, 'source': None, 'root_motion': False,
         'in_place': False, 'fps': None, 'map': 'auto', 'source_map': 'auto',
         'keep_existing_actions': False, 'cache': DEFAULT_CACHE, 'add_to': None, 'replace': False}
    flags = {'--root-motion': 'root_motion', '--in-place': 'in_place', '--keep-existing-actions': 'keep_existing_actions',
             '--replace': 'replace'}
    i = 0
    while i < len(a):
        k = a[i]
        if k in flags:
            o[flags[k]] = True
        elif k.startswith('--') and k[2:].replace('-', '_') in o and i + 1 < len(a):
            o[k[2:].replace('-', '_')] = a[i + 1]
            i += 1
        else:
            fail(f'Unknown or incomplete argument: {k}')
        i += 1
    if o['add_to']:
        if o['character']:
            fail('--add-to takes the character from the work file; leave out --character')
        o['out'] = o['out'] or o['add_to']
    elif o['replace']:
        fail('--replace only applies with --add-to')
    for req in ('clips', 'out') if o['add_to'] else ('character', 'clips', 'out'):
        if not o[req]:
            fail(f'--{req} is required')
    if o['fps'] is not None:
        o['fps'] = int(o['fps'])
    o['clips'] = [c.strip() for c in o['clips'].split(',') if c.strip()]
    return o


def resolve_library_clips(names, cache, root_motion):
    """-> [(file, action name, clip id)] using the catalogue."""
    path = os.path.join(cache, 'catalogue.json')
    if not os.path.isfile(path):
        fail(f'No catalogue at {path}. Run setup first (node ca.mjs setup).')
    with open(path, encoding='utf-8') as f:
        clips = json.load(f)['clips']
    by_key = {}
    for c in clips:
        by_key.setdefault(c['name'].lower(), c)
        by_key[c['id'].lower()] = c
    out = []
    for n in names:
        c = by_key.get(n.lower())
        if c is None:
            close = difflib.get_close_matches(n, [c['name'] for c in clips], n=5, cutoff=0.4)
            fail(f"Clip '{n}' is not in the catalogue. Close matches: {close or 'none'}. Use: node ca.mjs search <words>")
        f = c['file_root_motion'] if root_motion else c['file_inplace']
        if root_motion and not f:
            fail(f"Clip '{n}' has no root-motion variant")
        out.append((f, c['name'], c['id']))
    return out


ALIGN_MIN_DEG = 20.0  # rest differences below this are treated as the same pose


def rot_of(m):
    return m.to_3x3().normalized().to_quaternion()


def world_rest(arm, bone_name):
    m = arm.matrix_world @ arm.data.bones[bone_name].matrix_local
    return rot_of(m), m.to_translation()


def yaw_between(f_from, f_to):
    a = math.atan2(f_to[1], f_to[0]) - math.atan2(f_from[1], f_from[0])
    return Quaternion((0, 0, 1), a)


def paired_parent(c, pairs):
    p = CANON_PARENT.get(c)
    while p and p not in pairs:
        p = CANON_PARENT.get(p)
    return p


class Retargeter:
    def __init__(self, src, src_map, tgt, tgt_map):
        self.src, self.tgt = src, tgt
        self.pairs = [c for c in CANONICAL if c in src_map and c in tgt_map]
        missing = [c for c in REQUIRED if c not in self.pairs]
        if missing:
            fail(f'Cannot retarget: required bones missing on '
                 f"{'source' if any(c not in src_map for c in missing) else 'character'}: {missing}")
        self.smap, self.tmap = src_map, tgt_map
        sf, tf = rest_facts(src, src_map), rest_facts(tgt, tgt_map)
        self.G = yaw_between(sf['forward'], tf['forward'])
        self.Ginv = self.G.inverted()
        self.Srot, self.Spos, self.Trot, self.Tpos = {}, {}, {}, {}
        for c in self.pairs:
            self.Srot[c], self.Spos[c] = world_rest(src, src_map[c])
            self.Trot[c], self.Tpos[c] = world_rest(tgt, tgt_map[c])
        # Hip motion scale: leg length (thigh joints' height above the ground). The pelvis bone's
        # own head is no good here: rigs put it anywhere between the hips and the waist.
        s_h = (self.Spos['thigh_l'].z + self.Spos['thigh_r'].z) / 2 - sf['ground_z']
        t_h = (self.Tpos['thigh_l'].z + self.Tpos['thigh_r'].z) / 2 - tf['ground_z']
        self.scale = t_h / s_h if s_h > 1e-6 else 1.0
        # Rest alignment per bone: rotate the target bone's aim onto the source bone's aim, but only
        # when the rests really differ (A-pose vs T-pose arms). Small differences are the rig's own
        # style (a shin that leans back); straightening them would push skinned heels through the floor.
        self.A = {}
        for c in self.pairs:
            child = next((k for k in CANON_AIM.get(c, []) if k in self.pairs), None)
            if c == 'pelvis':
                # Whole-frame alignment: up = mid-thighs to the neck/head (the whole torso; short
                # spans like pelvis-to-first-spine vary wildly between rigs), left = between the
                # thighs. Aiming the pelvis bone itself fails on rigs where it points down.
                top = next((k for k in ('neck_01', 'Head', 'spine_03') if k in self.pairs), child)
                a = self.pelvis_frame('S', top) @ self.pelvis_frame('T', top).inverted()
            elif c.startswith(('foot_', 'ball_')):
                # Both rigs stand flat at rest, so feet keep their own rest orientation.
                a = Quaternion()
            elif child:
                ds = self.G @ (self.Spos[child] - self.Spos[c])
                dt = self.Tpos[child] - self.Tpos[c]
                a = dt.normalized().rotation_difference(ds.normalized()) if ds.length > 1e-6 and dt.length > 1e-6 else Quaternion()
            else:
                a = None
            if a is not None:
                self.A[c] = a if math.degrees(a.angle) > ALIGN_MIN_DEG else Quaternion()
            else:
                p = paired_parent(c, self.pairs)
                self.A[c] = self.A.get(p, Quaternion()) if p else Quaternion()
        self.sp = {c: paired_parent(c, self.pairs) for c in self.pairs}
        self.facts = {'source': sf, 'character': tf, 'hip_scale': round(self.scale, 4),
                      'yaw_deg': round(math.degrees(self.G.angle) * (1 if self.G.axis.z >= 0 else -1), 1)}

    def pelvis_frame(self, side, up_bone):
        pos = self.Spos if side == 'S' else self.Tpos
        g = self.G if side == 'S' else Quaternion()
        mid = (pos['thigh_l'] + pos['thigh_r']) / 2
        up = g @ ((pos[up_bone] - mid) if up_bone else Vector((0, 0, 1)))
        left = g @ (pos['thigh_l'] - pos['thigh_r'])
        fwd = left.cross(up).normalized()
        up = fwd.cross(left).normalized()
        left = left.normalized()
        return Matrix((left, fwd, up)).transposed().to_quaternion()

    def sample_source(self):
        dg = bpy.context.evaluated_depsgraph_get()
        ev = self.src.evaluated_get(dg)
        mw = self.src.matrix_world
        rots, poss = {}, {}
        for c in self.pairs:
            m = mw @ ev.pose.bones[self.smap[c]].matrix
            rots[c], poss[c] = rot_of(m), m.to_translation()
        return rots, poss

    def target_world(self, rots, poss):
        Rt, Pt = {}, {}
        for c in self.pairs:
            D = self.G @ rots[c] @ self.Srot[c].inverted() @ self.Ginv
            Rt[c] = D @ self.A[c] @ self.Trot[c]
        for c in self.pairs:
            if c in ('root', 'pelvis'):
                Pt[c] = self.Tpos[c] + self.scale * (self.G @ (poss[c] - self.Spos[c]))
            else:
                p = self.sp[c]
                Pt[c] = Pt[p] + (Rt[p] @ self.Trot[p].inverted()) @ (self.Tpos[c] - self.Tpos[p])
        # Ground contact: legs with other proportions reach a different height when bent, so lift
        # or drop the body until the lowest ankle/toe joint sits as high above its rest height as
        # the source's does (scaled). Planted feet stay planted; jumps keep their scaled height.
        joints = [c for c in self.pairs if c.startswith(('foot_', 'ball_'))]
        if joints:
            src_low = min((poss[c] - self.Spos[c]).z for c in joints)
            tgt_low = min((Pt[c] - self.Tpos[c]).z for c in joints)
            lift = Vector((0, 0, self.scale * src_low - tgt_low))
            for c in Pt:
                if c != 'root':
                    Pt[c] = Pt[c] + lift
        return Rt, Pt


def to_bases(tgt, tmap, pairs, Rt, Pt):
    """World targets -> per-bone local (basis) loc/rot, parents first."""
    inv = tgt.matrix_world.inverted()
    obj_rot_inv = rot_of(tgt.matrix_world).inverted()
    desired = {tmap[c]: Matrix.Translation(inv @ Pt[c]) @ (obj_rot_inv @ Rt[c]).to_matrix().to_4x4() for c in pairs}
    posed, out = {}, {}
    for b in tgt.data.bones:  # Blender lists bones parents-first
        rest = b.matrix_local
        if b.parent:
            prel = b.parent.matrix_local.inverted() @ rest
            pm = posed[b.parent.name]
        else:
            prel, pm = rest, Matrix.Identity(4)
        if b.name in desired:
            m = desired[b.name]
            basis = prel.inverted() @ pm.inverted() @ m
            out[b.name] = (basis.to_translation(), basis.to_quaternion().normalized())
            posed[b.name] = m
        else:
            posed[b.name] = pm @ prel
    return out


def remove_objects(objs, actions):
    for o in objs:
        if o.name in bpy.data.objects:
            bpy.data.objects.remove(o, do_unlink=True)
    for a in actions:
        if a.name in bpy.data.actions:
            bpy.data.actions.remove(a)
    for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials, bpy.data.images):
        for d in list(coll):
            if d.users == 0:
                coll.remove(d)


def open_work(o):
    """--add-to: open the working file. -> (armature, bone map, notes, its clips, its report)"""
    path = os.path.abspath(o['add_to'])
    if not os.path.isfile(path):
        fail(f'File not found: {path}')
    bpy.ops.wm.open_mainfile(filepath=path)
    bpy.context.preferences.filepaths.save_version = 0  # no work.blend1 backups
    tgt = main_armature(list(bpy.data.objects))
    if tgt is None or 'ca_clips' not in tgt:
        fail(f'{path} is not a retarget working file (no armature with ca_clips). Run retarget first.')
    fps = int(bpy.context.scene.get('ca_fps', LIBRARY_FPS))
    if o['fps'] not in (None, fps):
        fail(f'{path} is at {fps} fps; clips added to it are too. Leave out --fps or pass --fps {fps}.')
    o['fps'] = fps
    if o['map'] != 'auto':
        tmap, tnotes = load_bone_map(tgt, o['map'])
    else:
        tmap, tnotes = saved_bone_map(tgt, path), [f'bone map from {os.path.basename(path)}']
    report = os.path.splitext(path)[0] + '.retarget.json'
    prev = {}
    if os.path.isfile(report):
        with open(report, encoding='utf-8') as f:
            prev = json.load(f)
    return tgt, tmap, tnotes, json.loads(tgt['ca_clips']), prev


def main():
    o = parse()
    if o['add_to']:
        tgt, tmap, tnotes, work_clips, prev = open_work(o)
        removed = prev.get('removed_objects', [])
    else:
        o['fps'] = o['fps'] or LIBRARY_FPS
        reset_scene(o['fps'])
        objs, acts = import_file(o['character'])
        tgt = main_armature(objs)
        if tgt is None:
            fail(f"No armature in {o['character']}: the character must be rigged.")
        removed = remove_unrelated(tgt)
        tmap, tnotes = load_bone_map(tgt, o['map'])
        work_clips, prev = [], {'character': os.path.abspath(o['character'])}
        if not o['keep_existing_actions']:
            for a in acts:
                bpy.data.actions.remove(a)
    missing = [c for c in REQUIRED if c not in tmap]
    if missing:
        fail(f"Character bones missing for: {missing}. Run inspect, then pass --map map.json "
             f"mapping those library names to your bones.")
    if tgt.animation_data:
        tgt.animation_data.action = None
        for t in list(tgt.animation_data.nla_tracks):
            tgt.animation_data.nla_tracks.remove(t)
    # Rest pose for evaluation of the character.
    for pb in tgt.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)

    if o['source']:
        jobs = [(o['source'], name, f"{os.path.basename(o['source'])}/{name}") for name in o['clips']]
    else:
        jobs = resolve_library_clips(o['clips'], o['cache'], o['root_motion'])

    sc = bpy.context.scene
    results, warnings = [], []
    if o['root_motion'] and 'root' not in tmap:
        warnings.append('Character has no root bone: root motion is carried by the pelvis. '
                        'Unreal root motion needs a root bone.')
    by_file = {}
    for f, name, cid in jobs:
        by_file.setdefault(f, []).append((name, cid))
    facts = None
    for f, clips in by_file.items():
        set_fps(LIBRARY_FPS if not o['source'] else o['fps'])
        before = {a.name for a in bpy.data.actions}
        sobjs, sacts = import_file(f)
        src_fps = sc.render.fps  # FBX import may switch it to the file's rate
        src = main_armature(sobjs)
        if src is None:
            fail(f'No armature in source {f}')
        smap, snotes = load_bone_map(src, o['source_map'])
        rt = Retargeter(src, smap, tgt, tmap)
        facts = rt.facts
        by_name = {}
        for a in sacts:
            n = short_action_name(a.name)
            # Blender renames an imported action that clashes with one in the work file (Walk.001).
            base = re.sub(r'\.\d{3}$', '', n)
            by_name[base if base != n and base in before else n] = a
        if [c[0] for c in clips] == ['all']:
            clips = [(n, f'{os.path.basename(f)}/{n}') for n in by_name]
        for name, cid in clips:
            act = by_name.get(name)
            if act is None:
                fail(f"No action '{name}' in {f}. Available: {sorted(by_name)}")
            assign_action(src, act)
            s, e = action_range(act)
            dur = (e - s) / src_fps
            n = int(round(dur * o['fps'])) + 1
            samples = []
            for i in range(n):
                t = s + i * src_fps / o['fps']
                sc.frame_set(int(math.floor(t)), subframe=t - math.floor(t))
                samples.append(rt.sample_source())
            if o['in_place']:
                drift = samples[-1][1]['pelvis'] - samples[0][1]['pelvis']
                drift.z = 0
                for i, (_, poss) in enumerate(samples):
                    poss['pelvis'] = poss['pelvis'] - drift * (i / max(n - 1, 1))
                    if 'root' in poss:
                        poss['root'] = Vector((rt.Spos['root'].x, rt.Spos['root'].y, poss['root'].z))
            frames = []
            set_fps(o['fps'])
            for rots, poss in samples:
                Rt, Pt = rt.target_world(rots, poss)
                frames.append(to_bases(tgt, tmap, rt.pairs, Rt, Pt))
            set_fps(src_fps)
            if name in work_clips:
                if not o['replace']:
                    fail(f"Clip '{name}' is already in {o['add_to']}. Pass --replace to overwrite it.")
                bpy.data.actions.remove(bpy.data.actions[name])
            new = write_action(tgt, name, frames)
            new['ca_source'] = cid
            results.append({'name': new.name, 'source': cid, 'frames': n - 1, 'fps': o['fps'],
                            'duration_s': round((n - 1) / o['fps'], 3)})
            print(f'retargeted {cid} -> {new.name}: {n - 1} frames at {o["fps"]} fps')
        remove_objects(sobjs, sacts)
        tnotes += [f'source: {x}' for x in snotes]

    set_fps(o['fps'])
    new_names = [r['name'] for r in results]
    all_clips = work_clips + [n for n in new_names if n not in work_clips]
    sc.frame_start = 0
    sc.frame_end = max(int(action_range(bpy.data.actions[c])[1]) for c in all_clips)
    assign_action(tgt, bpy.data.actions[all_clips[0]])
    tgt['ca_clips'] = json.dumps(all_clips)
    sc['ca_fps'] = o['fps']
    out = os.path.abspath(o['out'])
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out, check_existing=False)
    reported = [c for c in prev.get('clips', []) if c['name'] not in new_names] + results
    report = {'blend': out, 'character': prev.get('character'), 'clips': reported,
              'mapping': tmap, 'removed_objects': removed, 'notes': tnotes, 'warnings': warnings,
              'alignment': facts}
    write_json(os.path.splitext(out)[0] + '.retarget.json', report)
    for w in warnings:
        print(f'WARNING: {w}')
    if removed and not o['add_to']:
        print(f'Removed objects not part of the character: {", ".join(removed)}')
    replaced = len([n for n in new_names if n in work_clips])
    added = f' ({len(results) - replaced} added, {replaced} replaced)' if o['add_to'] else ''
    print(f'Saved {out} with {len(all_clips)} clips{added} (hip scale {facts["hip_scale"]}, yaw {facts["yaw_deg"]} deg).')


main()
