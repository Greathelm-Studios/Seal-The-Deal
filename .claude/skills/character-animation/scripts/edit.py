"""Make new clips from clips already in a retarget working file.

blender -b --factory-startup -P edit.py -- --blend work.blend <operation> [options]
    list
    trim       --clip A --start F --end F
    retime     --clip A --speed 1.25
    mirror     --clip A
    transition --clip A --to B [--overlap 8]
    layer      --clip BASE --over B [--bones spine_01] [--at 0] [--fade 6] [--weight 1] [--repeat]
    pose       --clip A --bone B [--rotate up:20,left:-10] [--move up:-0.05]
               [--start F] [--end F] [--ease 4] [--weight 1] [--keep-feet]
    loop       --clip A [--overlap 8]
    foot-lock  --clip A [--height-tol 0.025] [--speed-tol 0.15]
    hold       --clip A [--frame F] [--length 2]
    ik         --clip A --limb hand_r|hand_l|foot_l|foot_r (--move up:0.2 [--space root|hips] | --pin F)
               [--start F] [--end F] [--ease 4] [--weight 1]
    delay      --clip A --bones spine_03 [--frames 3] [--cascade]
    exaggerate --clip A --bones upperarm_l,upperarm_r [--amount 1.5]
    aim        --clip A --at forward:3,up:1.6 [--bone Head] [--start F] [--end F] [--ease 4] [--weight 1]
    directions --clip WALK [--mode strafe|facing] [--side-stride 0.45] [--arms 0.4]
               (8 clips; --name sets their base name)
    style      --clip WALK [--preset stroll|brisk|heavy|sneak|my.json] [--stride 1] [--cadence 1]
               [--width 0] [--bounce 1] [--sway 1] [--hips 0] [--arms 1] [--lean 0] [--save my.json]
Every operation except list also takes [--name NEW] [--replace] [--out other.blend] [--map map.json].

Source clips are never changed. The new clip joins the work file's clip list, so preview, export
and verify pick it up. Bones can be named by canonical library name (upperarm_r) or the
character's own name. Axes (up, left, forward and their opposites down, right, back) are the
character's, from its rest pose; --move distances are in metres.
"""
import json
import math
import os
import re
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from mathutils import Matrix, Quaternion, Vector  # noqa: E402
from ca_common import (action_fcurves, action_range, assign_action, axis_spec, fail, load_bone_map,  # noqa: E402
                       main_armature, partner, rest_facts, saved_bone_map, script_args, write_action, write_json)

COMMON = {'blend', 'op', 'name', 'replace', 'out', 'map'}
OPTIONS = {
    'list': set(),
    'trim': {'clip', 'start', 'end'},
    'retime': {'clip', 'speed'},
    'mirror': {'clip'},
    'transition': {'clip', 'to', 'overlap'},
    'layer': {'clip', 'over', 'bones', 'at', 'fade', 'weight', 'repeat'},
    'pose': {'clip', 'bone', 'rotate', 'move', 'start', 'end', 'ease', 'weight', 'keep_feet'},
    'loop': {'clip', 'overlap'},
    'foot-lock': {'clip', 'height_tol', 'speed_tol'},
    'hold': {'clip', 'frame', 'length'},
    'ik': {'clip', 'limb', 'move', 'pin', 'space', 'start', 'end', 'ease', 'weight'},
    'delay': {'clip', 'bones', 'frames', 'cascade'},
    'exaggerate': {'clip', 'bones', 'amount'},
    'aim': {'clip', 'bone', 'at', 'start', 'end', 'ease', 'weight'},
    'directions': {'clip', 'mode', 'arms', 'side_stride'},
    'style': {'clip', 'preset', 'save', 'stride', 'cadence', 'width', 'bounce', 'sway', 'hips', 'arms', 'lean'},
}
SWITCHES = {'--repeat', '--keep-feet', '--replace', '--cascade'}
CURVE_PATH = re.compile(r'pose\.bones\["(.+)"\]\.(location|rotation_quaternion|rotation_euler|scale)$')
WARNINGS = []


def parse():
    a = script_args()
    o = {'op': None}
    i = 0
    while i < len(a):
        k = a[i]
        if k in SWITCHES:
            o[k[2:].replace('-', '_')] = True
        elif k.startswith('--') and i + 1 < len(a):
            o[k[2:].replace('-', '_')] = a[i + 1]
            i += 1
        elif not k.startswith('--') and o['op'] is None:
            o['op'] = k
        else:
            fail(f'Unknown or incomplete argument: {k}')
        i += 1
    if not o.get('blend'):
        fail('--blend is required')
    if o['op'] not in OPTIONS:
        fail(f"Operation must be one of: {', '.join(OPTIONS)}")
    unknown = [k for k in o if k not in COMMON | OPTIONS[o['op']]]
    if unknown:
        fail(f"{o['op']} does not take: {', '.join('--' + k.replace('_', '-') for k in unknown)}")
    if o['op'] != 'list' and not o.get('clip'):
        fail('--clip is required')
    return o


def num(o, key, default, kind=float):
    v = o.get(key)
    if v is None:
        return default
    try:
        return kind(v)
    except ValueError:
        fail(f"--{key.replace('_', '-')} must be a number, got {v}")


def smooth(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def mix_key(a, b, w):
    (la, qa), (lb, qb) = a, b
    if qa.dot(qb) < 0:
        qb = -qb
    return la.lerp(lb, w), qa.slerp(qb, w)


def mix(a, b, w):
    """Blend two frames of local keys (same bones) by w: 0 is a, 1 is b."""
    return {bn: mix_key(a[bn], b[bn], w) for bn in a}


def fill(clips):
    """Give every frame of every clip the same bones; bones a clip does not animate stay at rest."""
    bones = sorted({bn for frames in clips for bn in frames[0]})
    rest = (Vector(), Quaternion())
    return [[{bn: fr.get(bn, rest) for bn in bones} for fr in frames] for frames in clips]


def loop_name(clip):
    return clip[:-5] if clip.endswith('_Loop') else clip


def derive(clip, tag):
    """Walk_Loop + Fast -> Walk_Fast_Loop, so engines still treat the result as a loop."""
    return f'{clip[:-5]}_{tag}_Loop' if clip.endswith('_Loop') else f'{clip}_{tag}'


class Work:
    def __init__(self, path, map_arg):
        self.path = os.path.abspath(path)
        if not os.path.isfile(self.path):
            fail(f'File not found: {path}')
        bpy.ops.wm.open_mainfile(filepath=self.path)
        bpy.context.preferences.filepaths.save_version = 0  # no work.blend1 backups
        self.sc = bpy.context.scene
        self.arm = main_armature(list(bpy.data.objects))
        if self.arm is None or 'ca_clips' not in self.arm:
            fail(f'{path} is not a retarget working file (no armature with ca_clips). Run retarget first.')
        self.clips = json.loads(self.arm['ca_clips'])
        self.mapping = self._mapping(map_arg)
        for c in ('thigh_l', 'thigh_r'):
            if c not in self.mapping:
                fail(f'Bone map has no {c}. Pass --map map.json (see references/rigs-and-retargeting.md).')
        bones = self.arm.data.bones
        self.rest = {b.name: b.matrix_local.copy() for b in bones}
        self.prel = {b.name: b.parent.matrix_local.inverted() @ b.matrix_local for b in bones if b.parent}
        # Character axes in armature space, from the rest pose.
        to_arm = self.arm.matrix_world.to_3x3().inverted()
        up = (to_arm @ Vector((0, 0, 1))).normalized()
        tl, tr = bones[self.mapping['thigh_l']].head_local, bones[self.mapping['thigh_r']].head_local
        left = tl - tr
        left = (left - left.dot(up) * up).normalized()
        self.axes = {'up': up, 'left': left, 'forward': left.cross(up).normalized()}
        self.centre = (tl + tr) / 2
        self.unit = (self.arm.matrix_world.to_3x3() @ up).length  # metres per armature unit
        facts = rest_facts(self.arm, self.mapping)
        self.height = facts['height_m']
        self.height_arm = self.height / self.unit
        # The ground under the hips at rest, in armature space.
        c = self.arm.matrix_world @ self.centre
        self.ground = self.arm.matrix_world.inverted() @ Vector((c.x, c.y, facts['ground_z']))

    def _mapping(self, map_arg):
        if map_arg and map_arg != 'auto':
            return load_bone_map(self.arm, map_arg)[0]
        return saved_bone_map(self.arm, self.path)

    def bone(self, name):
        b = self.mapping.get(name, name)
        if b not in self.arm.data.bones:
            fail(f"No bone '{name}'. Use canonical names ({', '.join(self.mapping)}) or the character's own bone names.")
        return b

    def action(self, name):
        if name not in self.clips:
            fail(f"No clip '{name}' in the work file. Clips: {self.clips}")
        return bpy.data.actions[name]

    def local(self, name):
        """-> one {bone: (loc, quat)} per frame, for every bone the clip animates."""
        act = self.action(name)
        s, e = action_range(act)
        curves = {}
        for fc in action_fcurves(act):
            m = CURVE_PATH.match(fc.data_path)
            if not m or m.group(2) == 'scale':
                continue
            if m.group(2) == 'rotation_euler':
                fail(f"Clip '{name}' uses Euler rotation on {m.group(1)}; edit works on clips made by retarget.")
            curves.setdefault(m.group(1), {})[(m.group(2), fc.array_index)] = fc
        frames = []
        for f in range(int(round(s)), int(round(e)) + 1):
            fr = {}
            for bn, cs in curves.items():
                loc = Vector([cs[('location', k)].evaluate(f) if ('location', k) in cs else 0.0 for k in range(3)])
                q = Quaternion([cs[('rotation_quaternion', k)].evaluate(f) if ('rotation_quaternion', k) in cs
                                else float(k == 0) for k in range(4)])
                fr[bn] = (loc, q.normalized())
            frames.append(fr)
        return frames

    def posed(self, name):
        """-> one {bone: armature-space pose matrix} per frame."""
        act = self.action(name)
        assign_action(self.arm, act)
        s, e = action_range(act)
        out = []
        for f in range(int(round(s)), int(round(e)) + 1):
            self.sc.frame_set(f)
            ev = self.arm.evaluated_get(bpy.context.evaluated_depsgraph_get())
            out.append({pb.name: pb.matrix.copy() for pb in ev.pose.bones})
        return out

    def bases(self, pose, bones):
        """Armature-space pose matrices -> {bone: (loc, quat)} local keys for the given bones."""
        out = {}
        for bn in bones:
            b = self.arm.data.bones[bn]
            parent = pose[b.parent.name] @ self.prel[bn] if b.parent else self.rest[bn]
            m = parent.inverted() @ pose[bn]
            out[bn] = (m.to_translation(), m.to_3x3().normalized().to_quaternion())
        return out

    def propagate(self, orig, new, fixed):
        """Move bones below the changed ones with their parents, keeping their local transforms."""
        changed = set(fixed)
        for b in self.arm.data.bones:  # parents first
            if b.name in fixed or not b.parent or b.parent.name not in changed:
                continue
            prel = self.prel[b.name]
            new[b.name] = new[b.parent.name] @ prel @ (orig[b.parent.name] @ prel).inverted() @ orig[b.name]
            changed.add(b.name)

    def horizontal(self, v):
        up = self.axes['up']
        return v - v.dot(up) * up

    def closes(self, frames):
        a, b = frames[0], frames[-1]
        tol = 1e-3 * self.height_arm
        return all(abs(a[bn][1].dot(b[bn][1])) > 0.9995 and (a[bn][0] - b[bn][0]).length < tol for bn in a)

    def add(self, name, frames, source, replace):
        if name in bpy.data.actions and name not in self.clips:
            fail(f"An action named '{name}' exists that is not a clip; pick another --name.")
        if name in self.clips:
            if not replace:
                fail(f"Clip '{name}' already exists. Pass --replace or a different --name.")
            bpy.data.actions.remove(bpy.data.actions[name])
        act = write_action(self.arm, name, frames)
        act['ca_source'] = source
        if name not in self.clips:
            self.clips.append(name)
        self.arm['ca_clips'] = json.dumps(self.clips)

    def save(self, out):
        self.sc.frame_start = 0
        self.sc.frame_end = max(int(action_range(bpy.data.actions[c])[1]) for c in self.clips)
        self.arm['ca_map'] = json.dumps(self.mapping)
        assign_action(self.arm, bpy.data.actions[self.clips[0]])
        path = os.path.abspath(out or self.path)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=path, check_existing=False)
        return path


# ---------------------------------------------------------------- leg IK


LIMBS = {'foot': ('thigh', 'calf', 'foot'), 'hand': ('upperarm', 'lowerarm', 'hand')}


def two_bone_ik(w, pose, limb, target):
    """Bend a leg (limb foot_l/foot_r) or arm (hand_l/hand_r) so its end joint reaches target
    (armature space). The knee or elbow stays in its current bending plane (a straight limb bends
    knee forward, elbow back) and the foot or hand keeps its world rotation.
    -> (changed bones, how far short of the target the joint stays, in armature units)."""
    kind, side = limb.split('_')
    th, ca, ft = (w.mapping[f'{b}_{side}'] for b in LIMBS[kind])
    H, K, A = pose[th].translation, pose[ca].translation, pose[ft].translation
    l1, l2 = (K - H).length, (A - K).length
    to = target - H
    d = min(max(to.length, abs(l1 - l2) + 1e-6), l1 + l2 - 1e-6)
    u = to.normalized()
    pole = (K - H) - (K - H).dot(u) * u
    if pole.length < 1e-6 * w.height_arm:
        bend = w.axes['forward'] * (1 if kind == 'foot' else -1)
        pole = bend - bend.dot(u) * u
    v = pole.normalized()
    a = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
    knee = H + u * a + v * math.sqrt(max(l1 * l1 - a * a, 0.0))
    ankle = H + u * d
    q1 = (K - H).rotation_difference(knee - H)
    q2 = (q1 @ (A - K)).rotation_difference(ankle - knee)
    foot_rot = pose[ft].to_3x3().normalized()
    pose[th] = Matrix.Translation(H) @ (q1.to_matrix() @ pose[th].to_3x3().normalized()).to_4x4()
    pose[ca] = Matrix.Translation(knee) @ ((q2 @ q1).to_matrix() @ pose[ca].to_3x3().normalized()).to_4x4()
    pose[ft] = Matrix.Translation(ankle) @ foot_rot.to_4x4()
    return {th, ca, ft}, (target - ankle).length


def leg_bones(w):
    return {w.mapping[f'{b}_{s}'] for b in ('thigh', 'calf', 'foot') for s in ('l', 'r')}


# ---------------------------------------------------------------- operations


def op_trim(w, o):
    frames = w.local(o['clip'])
    n = len(frames)
    s, e = num(o, 'start', 0, int), num(o, 'end', n - 1, int)
    if not 0 <= s < e <= n - 1:
        fail(f'Need 0 <= --start < --end <= {n - 1}')
    return frames[s:e + 1], f"{loop_name(o['clip'])}_{s}_{e}", f"trim {o['clip']} {s}-{e}"


def op_retime(w, o):
    speed = num(o, 'speed', None)
    if not speed or speed <= 0:
        fail('--speed must be above 0 (1.5 plays 50% faster, 0.5 half speed)')
    tag = 'x' + f'{speed:g}'.replace('.', 'p')
    return resample(w.local(o['clip']), speed), derive(o['clip'], tag), f"retime {o['clip']} x{speed:g}"


def resample(frames, speed):
    """Play frames faster (speed above 1) or slower. First and last frames stay exact, so loops still close."""
    n = len(frames)
    m = max(2, int(round((n - 1) / speed)) + 1)
    out = []
    for i in range(m):
        t = i * (n - 1) / (m - 1)
        j = min(int(t), n - 2)
        out.append(mix(frames[j], frames[j + 1], t - j))
    return out


def op_mirror(w, o):
    local = w.local(o['clip'])
    pose = w.posed(o['clip'])
    bones = w.arm.data.bones
    names = [b.name for b in bones]
    pair = {bn: partner(bn, names) for bn in names}
    for s_bone in [w.mapping[c] for c in w.mapping if c.endswith(('_l', '_r'))]:
        if pair[s_bone] == s_bone:
            WARNINGS.append(f'No opposite-side bone found for {s_bone}; it is mirrored onto itself.')
    n = w.axes['left']
    Rf = Matrix.Identity(3) - 2 * Matrix(((n.x * n.x, n.x * n.y, n.x * n.z),
                                          (n.y * n.x, n.y * n.y, n.y * n.z),
                                          (n.z * n.x, n.z * n.y, n.z * n.z)))
    rest_rot = {bn: m.to_3x3().normalized() for bn, m in w.rest.items()}
    # Bones that translate on their own (root, pelvis) are reflected; the rest follow their parents.
    tol = 1e-3 * w.height_arm
    moving = {bn for bn in local[0] if any(fr[bn][0].length > tol for fr in local)}
    keyed = set(local[0]) | {pair[bn] for bn in local[0]}
    out = []
    for fr in pose:
        new = {}
        for b in bones:  # parents first
            p = pair[b.name]
            D = fr[p].to_3x3().normalized() @ rest_rot[p].inverted()
            R = Rf @ D @ Rf @ rest_rot[b.name]
            if not b.parent or p in moving:
                x = fr[p].translation
                pos = x - 2 * (x - w.centre).dot(n) * n
            else:
                q = b.parent.name
                pos = new[q].translation + (new[q].to_3x3().normalized() @ rest_rot[q].inverted()) @ (
                    w.rest[b.name].translation - w.rest[q].translation)
            new[b.name] = Matrix.Translation(pos) @ R.to_4x4()
        out.append(w.bases(new, sorted(keyed)))
    return out, derive(o['clip'], 'Mirror'), f"mirror {o['clip']}"


def op_transition(w, o):
    if not o.get('to'):
        fail('--to is required')
    a, b = fill([w.local(o['clip']), w.local(o['to'])])
    na, nb = len(a), len(b)
    k = num(o, 'overlap', 8, int)
    if not 1 <= k < min(na, nb):
        fail(f'--overlap must be 1 to {min(na, nb) - 1} frames for these clips')
    # Parentless bones (root motion) continue from where the first clip is when the blend starts.
    for bn in a[0]:
        if w.arm.data.bones[bn].parent:
            continue
        r = w.rest[bn].to_3x3().normalized()
        shift = r.inverted() @ w.horizontal(r @ (a[na - k][bn][0] - b[0][bn][0]))
        b = [{**fr, bn: (fr[bn][0] + shift, fr[bn][1])} for fr in b]
    blend = [mix(a[na - k + i], b[i], smooth((i + 1) / (k + 1))) for i in range(k)]
    out = a[:na - k] + blend + b[k:]
    return out, f"{loop_name(o['clip'])}_to_{loop_name(o['to'])}", f"transition {o['clip']} -> {o['to']} ({k} frames)"


def op_layer(w, o):
    if not o.get('over'):
        fail('--over is required')
    base, over = fill([w.local(o['clip']), w.local(o['over'])])
    nb, no = len(base), len(over)
    roots = [w.bone(x.strip()) for x in (o.get('bones') or 'spine_01').split(',') if x.strip()]
    mask = set()
    for r in roots:
        mask |= {r, *(c.name for c in w.arm.data.bones[r].children_recursive)}
    mask &= set(base[0])
    at, fade = num(o, 'at', 0, int), num(o, 'fade', 6, int)
    weight = num(o, 'weight', 1.0)
    repeat = o.get('repeat', False)
    if not 0 <= at < nb:
        fail(f'--at must be 0 to {nb - 1}')
    if not 0 < weight <= 1:
        fail('--weight must be above 0 and at most 1')
    if not repeat and at + no > nb:
        WARNINGS.append(f"{o['over']} runs {at + no - nb} frames past the end of {o['clip']} and is cut off; "
                        'retime or trim it, or lower --at.')
    out = []
    for i in range(nb):
        j = i - at
        if repeat and j >= 0:
            j, wt = j % no, weight
        elif 0 <= j < no:
            env = min(1.0, (j + 1) / (fade + 1), (no - j) / (fade + 1)) if fade > 0 else 1.0
            wt = weight * smooth(env)
        else:
            wt = 0.0
        fr = dict(base[i])
        if wt > 0:
            for bn in mask:
                fr[bn] = mix_key(base[i][bn], over[j][bn], wt)
        out.append(fr)
    if w.closes(base) and not w.closes(out):
        if not repeat:
            fix = 'use --repeat with a looping overlay'
        else:
            fixes = [f"retime {o['over']} so its {no - 1} frames divide the base's {nb - 1}"] if (nb - 1) % (no - 1) else []
            fixes += ['use --at 0 (the frames before --at have no overlay)'] if at else []
            fix = ' and '.join(fixes) or f"check that {o['over']} loops"
        WARNINGS.append(f'The base clip loops but the result does not start and end on the same pose; {fix}, or loop the result.')
    return (out, derive(o['clip'], loop_name(o['over'])),
            f"layer {o['over']} over {o['clip']} from {', '.join(roots)}")


def op_pose(w, o):
    if not o.get('bone'):
        fail('--bone is required')
    if not o.get('rotate') and not o.get('move'):
        fail('Give --rotate and/or --move')
    bone = w.bone(o['bone'])
    rot = axis_spec(w.axes, o['rotate'], 'rotate') if o.get('rotate') else []
    move = sum((ax * val for ax, val in axis_spec(w.axes, o['move'], 'move')), Vector()) / w.unit if o.get('move') else Vector()
    keep_feet = o.get('keep_feet', False)
    if keep_feet and bone in leg_bones(w):
        fail('--keep-feet bends the legs itself; it cannot be used when posing a leg bone')

    def change(f, orig, new, wt):
        q = Quaternion()
        for ax, deg in rot:
            q = Quaternion(ax, math.radians(deg * wt)) @ q
        m = orig[bone]
        new[bone] = Matrix.Translation(m.translation + move * wt) @ (q.to_matrix() @ m.to_3x3().normalized()).to_4x4()
        w.propagate(orig, new, {bone})
        keyed = {bone}
        if keep_feet:
            for side in ('l', 'r'):
                keyed |= two_bone_ik(w, new, f'foot_{side}', orig[w.mapping[f'foot_{side}']].translation)[0]
            w.propagate(orig, new, keyed)
        return keyed

    out, s, e = posed_edit(w, o, change)
    parts = [o[k] for k in ('rotate', 'move') if o.get(k)]
    return out, derive(o['clip'], 'Posed'), f"pose {o['clip']} {o['bone']} {' '.join(parts)} frames {s}-{e}"


def ramp(o, n):
    """--start/--end/--ease/--weight -> (weight per frame, start, end). Weights ease in and out
    over --ease frames, except at the clip's own ends."""
    s, e = num(o, 'start', 0, int), num(o, 'end', n - 1, int)
    ease = num(o, 'ease', 4, int)
    weight = num(o, 'weight', 1.0)
    if not 0 <= s <= e <= n - 1:
        fail(f'Need 0 <= --start <= --end <= {n - 1}')
    if not 0 < weight <= 1:
        fail('--weight must be above 0 and at most 1')
    out = []
    for f in range(n):
        if s <= f <= e:
            ramp_in = (f - s + 1) / (ease + 1) if s > 0 and ease > 0 else 1.0
            ramp_out = (e - f + 1) / (ease + 1) if e < n - 1 and ease > 0 else 1.0
            out.append(weight * smooth(min(1.0, ramp_in, ramp_out)))
        else:
            out.append(0.0)
    return out, s, e


def posed_edit(w, o, change, pose=None):
    """Run change(frame, original pose, new pose, weight) -> changed bones on every weighted frame
    of --clip, on armature-space pose matrices. -> (local keys per frame, start, end)."""
    local = w.local(o['clip'])
    pose = pose or w.posed(o['clip'])
    weights, s, e = ramp(o, len(local))
    out = apply_posed(w, local, pose, weights, change)
    if w.closes(local) and not w.closes(out):
        WARNINGS.append('The clip looped but the edit changes its first or last frame differently; '
                        'cover the whole clip or keep --start/--end away from the ends.')
    return out, s, e


def apply_posed(w, local, pose, weights, change):
    """change(frame, original pose, new pose, weight) -> changed bones, on each weighted frame.
    -> local keys per frame, every frame keying the same bones."""
    out = []
    for f, wt in enumerate(weights):
        fr = dict(local[f])
        if wt > 0:
            new = dict(pose[f])
            fr.update(w.bases(new, change(f, pose[f], new, wt)))
        out.append(fr)
    # Frames outside the range key the new bones at their own values.
    bones = set().union(*(fr.keys() for fr in out))
    if any(len(fr) != len(bones) for fr in out):
        out = [{**w.bases(pose[f], bones - fr.keys()), **fr} for f, fr in enumerate(out)]
    return out


def bone_list(w, o):
    if not o.get('bones'):
        fail('--bones is required (comma list, e.g. spine_03 or upperarm_l,upperarm_r)')
    return [w.bone(x.strip()) for x in o['bones'].split(',') if x.strip()]


def op_ik(w, o):
    limb = o.get('limb')
    if limb not in ('hand_l', 'hand_r', 'foot_l', 'foot_r'):
        fail('--limb must be hand_l, hand_r, foot_l or foot_r')
    if bool(o.get('move')) == (o.get('pin') is not None):
        fail('Give either --move (an offset) or --pin F (stay where it is at frame F)')
    if o.get('space') and o.get('pin') is not None:
        fail('--space applies to --move only; a pinned hand or foot stays put in the scene')
    space = o.get('space', 'root')
    if space not in ('root', 'hips'):
        fail('--space must be root (the character\'s axes) or hips (axes that turn with the hips)')
    end, pelvis = w.mapping[limb], w.mapping['pelvis']
    pose = w.posed(o['clip'])
    pinned = None
    if o.get('pin') is not None:
        f0 = num(o, 'pin', 0, int)
        if not 0 <= f0 < len(pose):
            fail(f'--pin must be a frame from 0 to {len(pose) - 1}')
        pinned = pose[f0][end].translation.copy()
    else:
        offset = sum((ax * val for ax, val in axis_spec(w.axes, o['move'], 'move')), Vector()) / w.unit
    pelvis_rest = w.rest[pelvis].to_3x3().normalized().inverted()
    short = [0.0]

    def change(f, orig, new, wt):
        at = orig[end].translation
        if pinned is not None:
            target = at.lerp(pinned, wt)
        else:
            d = (orig[pelvis].to_3x3().normalized() @ pelvis_rest) @ offset if space == 'hips' else offset
            target = at + d * wt
        keyed, miss = two_bone_ik(w, new, limb, target)
        short[0] = max(short[0], miss)
        w.propagate(orig, new, keyed)
        return keyed

    out, s, e = posed_edit(w, o, change, pose)
    if short[0] * w.unit > 0.01:
        WARNINGS.append(f'{limb} cannot reach the target on some frames (up to {short[0] * w.unit * 100:.0f} cm short, '
                        'limb fully straight there); use a smaller offset or move the body with pose.')
    how = f"pin at frame {o['pin']}" if pinned is not None else f"move {o['move']} ({space})"
    return (out, derive(o['clip'], 'Pinned' if pinned is not None else 'IK'),
            f"ik {o['clip']} {limb} {how} frames {s}-{e}")


def op_delay(w, o):
    bones = bone_list(w, o)
    shift = num(o, 'frames', 3.0)
    cascade = o.get('cascade', False)
    local = w.local(o['clip'])
    n = len(local)
    unkeyed = [b for b in bones if b not in local[0]]
    if unkeyed:
        fail(f'{o["clip"]} does not animate {unkeyed}')
    looped = w.closes(local)

    def at(bn, t):
        # Loops wrap around (their last frame is their first); other clips hold their ends.
        t = t % (n - 1) if looped else min(max(t, 0.0), n - 1.0)
        j = min(int(t), n - 2)
        return mix_key(local[j][bn], local[j + 1][bn], t - j)[1]

    out = []
    for f in range(n):
        fr = dict(local[f])
        for i, bn in enumerate(bones):
            fr[bn] = (local[f][bn][0], at(bn, f - shift * (i + 1 if cascade else 1)))
        out.append(fr)
    return (out, derive(o['clip'], 'Delay'),
            f"delay {o['clip']} {', '.join(bones)} by {shift:g} frames{' each, cascading' if cascade else ''}")


def op_exaggerate(w, o):
    bones = bone_list(w, o)
    amount = num(o, 'amount', 1.5)
    if amount < 0:
        fail('--amount must be 0 or more (1.5 is half as much motion again, 0.5 half the motion)')
    local = w.local(o['clip'])
    unkeyed = [b for b in bones if b not in local[0]]
    if unkeyed:
        fail(f'{o["clip"]} does not animate {unkeyed}')
    out = [dict(fr) for fr in local]
    worst = scale_motion(out, bones, amount)
    if math.degrees(worst) > 120:
        WARNINGS.append(f'A bone swings up to {math.degrees(worst):.0f} deg from its average pose; '
                        'look for limbs through the body on the preview, or lower --amount.')
    return out, derive(o['clip'], 'Exaggerated'), f"exaggerate {o['clip']} {', '.join(bones)} x{amount:g}"


def scale_motion(frames, bones, amount):
    """Scale the listed bones' rotations around the clip's average rotation, in place.
    -> the largest resulting turn from that average, in radians."""
    worst = 0.0
    for bn in bones:
        qs = [fr[bn][1] for fr in frames]
        acc = Quaternion((0.0, 0.0, 0.0, 0.0))
        for q in qs:
            acc = acc + (q if q.dot(qs[0]) >= 0 else -q)
        mean = acc.normalized()  # motion is scaled around the average, not the rest pose
        for fr, q in zip(frames, qs):
            delta = mean.inverted() @ q
            if delta.w < 0:
                delta.negate()
            axis, angle = delta.to_axis_angle()
            worst = max(worst, angle * amount)
            fr[bn] = (fr[bn][0], mean @ Quaternion(axis, angle * amount))
    return worst


def op_aim(w, o):
    if not o.get('at'):
        fail('--at is required: a point in metres from the ground under the hips, like forward:3,up:1.6')
    bone = w.bone(o.get('bone') or 'Head')
    target = w.ground + sum((ax * val for ax, val in axis_spec(w.axes, o['at'], 'at')), Vector()) / w.unit
    rest_rot = w.rest[bone].to_3x3().normalized()
    facing = rest_rot.inverted() @ w.axes['forward']  # the bone's own forward, from the rest pose
    worst = [0.0]

    def change(f, orig, new, wt):
        m = orig[bone]
        r = m.to_3x3().normalized()
        q = (r @ facing).rotation_difference(target - m.translation)
        worst[0] = max(worst[0], q.angle * wt)
        new[bone] = Matrix.Translation(m.translation) @ (Quaternion().slerp(q, wt).to_matrix() @ r).to_4x4()
        w.propagate(orig, new, {bone})
        return {bone}

    out, s, e = posed_edit(w, o, change)
    if math.degrees(worst[0]) > 70:
        WARNINGS.append(f'{bone} turns up to {math.degrees(worst[0]):.0f} deg; past about 70 it looks broken. '
                        'Lower --weight, move the target, or turn the body first with pose.')
    return out, derive(o['clip'], 'Aim'), f"aim {o['clip']} {bone} at {o['at']} frames {s}-{e}"


def op_loop(w, o):
    frames = w.local(o['clip'])
    n = len(frames)
    k = num(o, 'overlap', 8, int)
    if not 1 <= k < n // 2:
        fail(f'--overlap must be 1 to {n // 2 - 1} frames for this clip')
    for bn in frames[0]:
        if w.arm.data.bones[bn].parent:
            continue
        r = w.rest[bn].to_3x3().normalized()
        travel = w.horizontal(r @ (frames[-1][bn][0] - frames[0][bn][0])).length * w.unit
        if travel > 0.05 * w.height:
            fail(f'{o["clip"]} travels {travel:.2f} m; loop works on in-place clips (retarget without --root-motion).')
    # Crossfade the tail into the head, then repeat the first frame at the end so the clip closes.
    out = [mix(frames[n - k + i], frames[i], smooth(i / k)) for i in range(k)] + frames[k:n - k]
    out.append(dict(out[0]))
    name = f"{o['clip'][:-5]}_Seamless_Loop" if o['clip'].endswith('_Loop') else f"{o['clip']}_Loop"
    return out, name, f"loop {o['clip']} ({k} frame crossfade)"


def contacts(w, pose, side, htol, stol, fps):
    """Frames where a foot is on the ground and still: -> list of (start, end)."""
    mw = w.arm.matrix_world
    ft = w.mapping[f'foot_{side}']
    joints = [ft] + ([w.mapping[f'ball_{side}']] if f'ball_{side}' in w.mapping else [])
    low = [min((mw @ fr[j].translation).z for j in joints) for fr in pose]
    pos = [mw @ fr[ft].translation for fr in pose]
    ground = min(low)
    n = len(pose)
    still = []
    for f in range(n):
        a, b = max(f - 1, 0), min(f + 1, n - 1)
        d = pos[b] - pos[a]
        d.z = 0
        still.append(d.length / max(b - a, 1) * fps <= stol)
    on = [low[f] <= ground + htol and still[f] for f in range(n)]
    segs, f = [], 0
    while f < n:
        if on[f]:
            g = f
            while g + 1 < n and on[g + 1]:
                g += 1
            if g - f >= 2:
                segs.append((f, g))
            f = g + 1
        else:
            f += 1
    return segs


def op_foot_lock(w, o):
    local = w.local(o['clip'])
    pose = w.posed(o['clip'])
    n = len(pose)
    fps = w.sc.render.fps
    htol = num(o, 'height_tol', 0.025) * w.height
    stol = num(o, 'speed_tol', 0.15) * w.height
    ease = 3
    plans = {}
    for side in ('l', 'r'):
        ft = w.mapping[f'foot_{side}']
        segs = contacts(w, pose, side, htol, stol, fps)
        plan = [(0.0, None)] * n
        for a, b in segs:
            pin = sum((w.horizontal(pose[f][ft].translation) for f in range(a, b + 1)), Vector()) / (b - a + 1)
            for f in range(max(a - ease, 0), min(b + ease, n - 1) + 1):
                wt = 1.0 if a <= f <= b else smooth(1 - (a - f if f < a else f - b) / (ease + 1))
                if wt > plan[f][0]:
                    plan[f] = (wt, pin)
        plans[side] = plan
        print(f"foot_{side}: {len(segs)} planted span(s): {', '.join(f'{a}-{b}' for a, b in segs) or 'none'}")
    if not any(wt for plan in plans.values() for wt, _ in plan):
        WARNINGS.append('No planted feet found, so nothing changed. In-place locomotion is not locked on '
                        'purpose (its planted foot slides back by design); try root-motion clips or raise --speed-tol.')
    out = []
    for f in range(n):
        fr = dict(local[f])
        orig = pose[f]
        new = dict(orig)
        keyed = set()
        for side in ('l', 'r'):
            wt, pin = plans[side][f]
            if wt > 0:
                at = orig[w.mapping[f'foot_{side}']].translation
                target = at + (pin - w.horizontal(at)) * wt
                keyed |= two_bone_ik(w, new, f'foot_{side}', target)[0]
        if keyed:
            w.propagate(orig, new, keyed)
            fr.update(w.bases(new, keyed))
        out.append(fr)
    bones = set().union(*(fr.keys() for fr in out))
    out = [{**w.bases(pose[f], bones - fr.keys()), **fr} for f, fr in enumerate(out)]
    return out, derive(o['clip'], 'Planted'), f"foot-lock {o['clip']}"


def op_hold(w, o):
    frames = w.local(o['clip'])
    n = len(frames)
    f = num(o, 'frame', n - 1, int)
    if not 0 <= f <= n - 1:
        fail(f'--frame must be 0 to {n - 1}')
    length = num(o, 'length', 2.0)
    if length <= 0:
        fail('--length must be above 0 seconds')
    m = max(2, int(round(length * w.sc.render.fps)) + 1)
    return [dict(frames[f]) for _ in range(m)], f"{loop_name(o['clip'])}_Hold{f}", f"hold {o['clip']} frame {f} for {length:g}s"


# ---------------------------------------------------------------- locomotion (directions, style)


def gait(w, pose):
    """Split a walk or run into the body's travel and each foot's path around the hips.
    -> (drift per frame, {side: {'N': neutral spot, 'D': [offset from it], 'v': [height]}},
        hips offset from their average per frame, travels). All armature space, travel removed."""
    n = len(pose)
    pel = w.mapping['pelvis']
    total = w.horizontal(pose[-1][pel].translation - pose[0][pel].translation)
    travels = total.length * w.unit > 0.05 * w.height
    drift = [total * (f / (n - 1)) if travels else Vector() for f in range(n)]
    feet = {}
    for s in ('l', 'r'):
        ft = w.mapping[f'foot_{s}']
        at = [pose[f][ft].translation - drift[f] for f in range(n)]
        flat = [w.horizontal(p) for p in at]
        neutral = sum(flat[:-1], Vector()) / (n - 1)
        feet[s] = {'N': neutral, 'D': [h - neutral for h in flat], 'v': [p - h for p, h in zip(at, flat)]}
    hips = [pose[f][pel].translation - drift[f] for f in range(n)]
    mean = sum(hips[:-1], Vector()) / (n - 1)
    return drift, feet, [h - mean for h in hips], travels


def check_gait(w, clip, feet):
    fwd, left = w.axes['forward'], w.axes['left']
    along = max(abs(d.dot(fwd)) for t in feet.values() for d in t['D'])
    across = max(abs(d.dot(left)) for t in feet.values() for d in t['D'])
    if along < 0.05 * w.height_arm:
        fail(f'{clip}: the feet barely move forward and back; give a walk or run (in place or root motion).')
    if across > along:
        fail(f'{clip}: the feet move more sideways than forward; give a forward walk or run.')
    if any((t['D'][0] - t['D'][-1]).length > 0.03 * w.height_arm for t in feet.values()):
        fail(f'{clip}: the feet end somewhere else than they start; give a looping walk or run (loop it first).')


def top_bones(w):
    return [b.name for b in w.arm.data.bones if not b.parent]


ARM_BONES = ('upperarm_l', 'upperarm_r', 'lowerarm_l', 'lowerarm_r')


def step_path(w, turn, stride=1.0, side=1.0):
    """3x3 map for the travel and foot paths: turned by `turn` radians to the left and scaled by
    `stride`. Steps shorten with the direction along an ellipse, to `side` of their length straight
    sideways, so every direction keeps its heading."""
    c, s = math.cos(turn), math.sin(turn)
    length = side / math.sqrt(side * side * c * c + s * s)
    return Matrix.Rotation(turn, 3, w.axes['up']) * (stride * length)


def walk_edit(w, local, pose, path, width=0.0, hips=None, lean=0.0, g=None):
    """Re-solve a walk: travel and foot paths mapped by `path` (step_path), feet moved `width`
    (armature units) further apart, hips moved by hips(f) -> offset, spine_01 tipped forward by
    `lean` radians. g: gait(w, pose) if already worked out. -> (local keys per frame, max IK shortfall)."""
    drift, feet, _, travels = g or gait(w, pose)
    left = w.axes['left']
    tops, pel, spine = top_bones(w), w.mapping['pelvis'], w.mapping.get('spine_01')
    short = [0.0]

    def change(f, orig, new, wt):
        keyed = set()
        shift = path @ drift[f] - drift[f]
        if travels and shift.length > 1e-9:
            for p in tops:
                new[p] = Matrix.Translation(shift) @ orig[p]
            w.propagate(orig, new, tops)
            keyed |= set(tops)
        if hips:
            new[pel] = Matrix.Translation(hips(f)) @ new[pel]
            keyed.add(pel)
            w.propagate(orig, new, keyed)
        if lean and spine:
            m = new[spine]
            new[spine] = Matrix.Translation(m.translation) @ (
                Matrix.Rotation(lean, 3, left) @ m.to_3x3().normalized()).to_4x4()
            keyed.add(spine)
            w.propagate(orig, new, keyed)
        for s, sign in (('l', 1), ('r', -1)):
            t = feet[s]
            target = t['N'] + left * (width * sign) + path @ (t['D'][f] + drift[f]) + t['v'][f]
            legs, miss = two_bone_ik(w, new, f'foot_{s}', target)
            short[0] = max(short[0], miss)
            keyed |= legs
        w.propagate(orig, new, keyed)
        return keyed

    return apply_posed(w, local, pose, [1.0] * len(pose), change), short[0]


def warn_reach(w, short, what):
    if short * w.unit > 0.01:
        WARNINGS.append(f'{what}: the legs cannot reach some foot positions (up to {short * w.unit * 100:.0f} cm '
                        'short, leg straight there). Lower the stride or the hips less.')


DIRECTIONS = [('Fwd', 0), ('FwdL', 45), ('L', 90), ('BwdL', 135), ('Bwd', 180), ('BwdR', -135), ('R', -90), ('FwdR', -45)]


def op_directions(w, o):
    mode = o.get('mode', 'strafe')
    if mode not in ('strafe', 'facing'):
        fail('--mode must be strafe (keeps facing forward) or facing (each clip faces where it goes)')
    arms = num(o, 'arms', 0.4)
    # Side steps are shorter than forward steps; full-length ones put the feet out of reach.
    side = num(o, 'side_stride', 0.45)
    if not 0 < side <= 1:
        fail('--side-stride must be above 0 and at most 1')
    clip = o['clip']
    local, pose = w.local(clip), w.posed(clip)
    gaits = {False: gait(w, pose)}
    check_gait(w, clip, gaits[False][1])
    if mode == 'strafe':
        gaits[True] = gait(w, pose[::-1])
    base = o.get('name') or loop_name(clip)
    base = base[:-4] if base.endswith('_Fwd') else base
    suffix = '_Loop' if clip.endswith('_Loop') else ''
    arm_bones = [w.mapping[b] for b in ARM_BONES if b in w.mapping and w.mapping[b] in local[0]]
    tops = top_bones(w)
    results = []
    for tag, deg in DIRECTIONS:
        if f'{base}_{tag}{suffix}' == clip:
            continue  # Jog_Fwd_Loop is its own forward clip
        if mode == 'facing':
            M = Matrix.Translation(w.centre) @ Matrix.Rotation(math.radians(deg), 4, w.axes['up']) @ Matrix.Translation(-w.centre)

            def change(f, orig, new, wt, M=M):
                for p in tops:
                    new[p] = M @ orig[p]
                w.propagate(orig, new, tops)
                return set(tops)

            out = apply_posed(w, local, pose, [1.0] * len(pose), change)
        else:
            # Backward directions play the walk in reverse (toes land first, as when stepping
            # back), so no direction turns the steps by more than 90 degrees.
            back = abs(deg) > 90
            src_local, src_pose = (local[::-1], pose[::-1]) if back else (local, pose)
            turn = math.radians(deg - 180 if deg > 90 else deg + 180 if deg < -90 else deg)
            g = gaits[back]
            feet = g[1]
            path = step_path(w, turn, side=side)
            # Feet that would cross when stepping sideways are moved apart until they clear.
            gap = min((feet['l']['N'] - feet['r']['N'] + path @ (feet['l']['D'][f] - feet['r']['D'][f])).dot(w.axes['left'])
                      for f in range(len(pose)))
            width = max(0.0, (0.06 * w.height_arm - gap) / 2)
            if turn == 0 and width == 0:
                out = [dict(fr) for fr in src_local]  # straight forward or back: the (reversed) clip as is
            else:
                out, short = walk_edit(w, src_local, src_pose, path, width=width, g=g)
                warn_reach(w, short, tag)
            if arms < 1 and arm_bones:
                scale_motion(out, arm_bones, 1 - (1 - arms) * abs(math.sin(turn)))
        results.append((out, f'{base}_{tag}{suffix}', f'directions {clip} {tag} ({mode})'))
    return results


STYLE_KEYS = ('stride', 'cadence', 'width', 'bounce', 'sway', 'hips', 'arms', 'lean')
STYLE_NEUTRAL = {'stride': 1.0, 'cadence': 1.0, 'width': 0.0, 'bounce': 1.0, 'sway': 1.0, 'hips': 0.0,
                 'arms': 1.0, 'lean': 0.0}
# width and hips are metres for a 1.8 m character (scaled to the character's height); lean is degrees.
STYLE_PRESETS = {
    'stroll': {'stride': 0.85, 'cadence': 0.85, 'bounce': 0.8, 'sway': 1.1, 'arms': 0.8},
    'brisk': {'stride': 1.15, 'cadence': 1.15, 'bounce': 1.1, 'sway': 0.8, 'arms': 1.3, 'lean': 3},
    'heavy': {'stride': 0.9, 'cadence': 0.8, 'width': 0.04, 'bounce': 1.4, 'sway': 1.8, 'hips': -0.03,
              'arms': 1.1, 'lean': 4},
    'sneak': {'stride': 0.75, 'cadence': 0.75, 'width': 0.03, 'bounce': 0.5, 'sway': 0.7, 'hips': -0.12,
              'arms': 0.5, 'lean': 12},
}


def op_style(w, o):
    style = dict(STYLE_NEUTRAL)
    preset = o.get('preset')
    if preset:
        if preset.lower().endswith('.json'):
            if not os.path.isfile(preset):
                fail(f'Style file not found: {preset}')
            with open(preset, encoding='utf-8') as f:
                saved = json.load(f)
        elif preset in STYLE_PRESETS:
            saved = dict(STYLE_PRESETS[preset], height=1.8)
        else:
            fail(f"--preset must be one of {', '.join(STYLE_PRESETS)} or a saved .json style")
        scale = w.height / saved.get('height', w.height)
        style.update({k: float(saved[k]) * (scale if k in ('width', 'hips') else 1) for k in STYLE_KEYS if k in saved})
    style.update({k: num(o, k, None) for k in STYLE_KEYS if o.get(k) is not None})
    if style['stride'] <= 0 or style['cadence'] <= 0 or style['arms'] < 0:
        fail('--stride and --cadence must be above 0, --arms 0 or more')
    if style == STYLE_NEUTRAL:
        fail('Nothing to change: give --preset or one of ' + ', '.join('--' + k for k in STYLE_KEYS))
    clip = o['clip']
    local, pose = w.local(clip), w.posed(clip)
    g = gait(w, pose)
    check_gait(w, clip, g[1])
    if o.get('save'):
        write_json(o['save'], {**style, 'height': round(w.height, 3)})
    up, left = w.axes['up'], w.axes['left']
    hips_dev = g[2]

    def hips(f):
        d = hips_dev[f]
        return (left * (d.dot(left) * (style['sway'] - 1))
                + up * (d.dot(up) * (style['bounce'] - 1) + style['hips'] / w.unit))

    if all(style[k] == STYLE_NEUTRAL[k] for k in ('stride', 'width', 'bounce', 'sway', 'hips', 'lean')):
        out = [dict(fr) for fr in local]  # only timing or arms change: the legs need no new solve
    else:
        out, short = walk_edit(w, local, pose, step_path(w, 0.0, stride=style['stride']), width=style['width'] / w.unit,
                               hips=hips, lean=math.radians(style['lean']), g=g)
        warn_reach(w, short, 'style')
    arm_bones = [w.mapping[b] for b in ARM_BONES if b in w.mapping and w.mapping[b] in local[0]]
    if style['arms'] != 1 and arm_bones:
        scale_motion(out, arm_bones, style['arms'])
    if style['cadence'] != 1:
        out = resample(out, style['cadence'])
    tag = preset.title() if preset and preset in STYLE_PRESETS else 'Styled'
    desc = ', '.join(f'{k} {style[k]:g}' for k in STYLE_KEYS if style[k] != STYLE_NEUTRAL[k])
    return out, derive(clip, tag), f'style {clip}: {desc}'


OPS = {'trim': op_trim, 'retime': op_retime, 'mirror': op_mirror, 'transition': op_transition,
       'layer': op_layer, 'pose': op_pose, 'loop': op_loop, 'foot-lock': op_foot_lock, 'hold': op_hold,
       'ik': op_ik, 'delay': op_delay, 'exaggerate': op_exaggerate, 'aim': op_aim,
       'directions': op_directions, 'style': op_style}


def main():
    o = parse()
    w = Work(o['blend'], o.get('map', 'auto'))
    fps = w.sc.render.fps
    if o['op'] == 'list':
        for c in w.clips:
            act = bpy.data.actions[c]
            s, e = action_range(act)
            src = act.get('ca_source', '')
            print(f'{c:<34} {int(e - s):>4}f {(e - s) / fps:6.2f}s  {src}')
        return
    result = OPS[o['op']](w, o)
    if isinstance(result, list):  # several clips (directions); --name was used as their base name
        results = result
    else:
        frames, name, source = result
        results = [(frames, o.get('name') or name, source)]
    for frames, name, source in results:
        if not re.match(r'^[A-Za-z0-9_\-]+$', name):
            fail(f"Clip name '{name}' may only use letters, digits, _ and -")
        w.add(name, frames, source, o.get('replace', False))
    path = w.save(o.get('out'))
    for msg in WARNINGS:
        print(f'WARNING: {msg}')
    for frames, name, source in results:
        print(f'Added {name}: {len(frames) - 1} frames ({(len(frames) - 1) / fps:.2f}s at {fps} fps) from {source}.')
    print(f'Saved {path}.')


main()
