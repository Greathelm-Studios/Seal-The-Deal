"""Shared helpers for the character-animation Blender scripts.

Run inside Blender (bpy available). Every script puts this folder on sys.path first.
Canonical bone names are the library's own rig (Quaternius UAL, which uses UE mannequin names).
"""
import bpy
import json
import os
import re
import sys
from mathutils import Matrix, Quaternion, Vector

LIBRARY_FPS = 30  # The UAL clips are authored at 30 fps (the FBX copies say so).
# Library cache: CHARACTER_ANIMATION_CACHE overrides; default is ~/.cache/character-animation.
DEFAULT_CACHE = os.environ.get('CHARACTER_ANIMATION_CACHE') or os.path.join(
    os.path.expanduser('~'), '.cache', 'character-animation')

# ---------------------------------------------------------------- arguments / scene


def script_args():
    """The script's arguments: CA_ARGS (a JSON list, set by ca.mjs) or those after Blender's `--`.
    ca.mjs uses CA_ARGS because Blender's add-ons parse the `--` arguments too, and Cycles stops
    on anything that could abbreviate one of its options (--cycle...)."""
    if 'CA_ARGS' in os.environ:
        return json.loads(os.environ['CA_ARGS'])
    return sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []


def fail(msg, code=2):
    print(f'ERROR: {msg}', file=sys.stderr)
    sys.stderr.flush()
    sys.exit(code)


def reset_scene(fps=LIBRARY_FPS):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    set_fps(fps)
    return bpy.context.scene


def set_fps(fps):
    sc = bpy.context.scene
    sc.render.fps = int(round(fps))
    sc.render.fps_base = 1.0


def import_file(path):
    """Import a character or clip file. Returns (new objects, new actions)."""
    if not os.path.isfile(path):
        fail(f'File not found: {path}')
    objs_before, acts_before = set(bpy.data.objects), set(bpy.data.actions)
    ext = os.path.splitext(path)[1].lower()
    if ext in ('.glb', '.gltf'):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == '.fbx':
        bpy.ops.import_scene.fbx(filepath=path, use_anim=True)
    elif ext == '.blend':
        # Append objects without running any scripts stored in the file.
        with bpy.data.libraries.load(path, link=False) as (src, dst):
            dst.objects = list(src.objects)
            dst.actions = list(src.actions)
        for o in dst.objects:
            if o is not None and o.name not in bpy.context.scene.collection.objects:
                bpy.context.scene.collection.objects.link(o)
    else:
        fail(f'Unsupported file type {ext} (use .fbx, .glb, .gltf or .blend)')
    new_objs = [o for o in bpy.data.objects if o not in objs_before]
    new_acts = [a for a in bpy.data.actions if a not in acts_before]
    return new_objs, new_acts


def main_armature(objs):
    arms = [o for o in objs if o.type == 'ARMATURE']
    if not arms:
        return None
    return max(arms, key=lambda o: len(o.data.bones))


def skinned_meshes(arm):
    """Meshes deformed by (or parented to) the armature."""
    out = []
    for o in bpy.data.objects:
        if o.type != 'MESH':
            continue
        if o.parent == arm or any(m.type == 'ARMATURE' and m.object == arm for m in o.modifiers):
            out.append(o)
    return out


def remove_unrelated(arm):
    """Delete objects that are neither the armature nor its meshes. Returns their names."""
    keep = {arm, *skinned_meshes(arm)}
    removed = []
    for o in list(bpy.data.objects):
        if o not in keep and o.type in ('MESH', 'EMPTY', 'ARMATURE', 'CURVE', 'LIGHT', 'CAMERA'):
            removed.append(o.name)
            bpy.data.objects.remove(o, do_unlink=True)
    return removed


# ---------------------------------------------------------------- actions (Blender 4.4+ layered API)


def action_fcurves(action):
    """All F-curves of an action, for both layered (4.4+) and legacy actions."""
    if hasattr(action, 'layers') and len(action.layers):
        out = []
        for layer in action.layers:
            for strip in layer.strips:
                for bag in getattr(strip, 'channelbags', []):
                    out.extend(bag.fcurves)
        return out
    return list(getattr(action, 'fcurves', []))


def assign_action(obj, action):
    if obj.animation_data is None:
        obj.animation_data_create()
    obj.animation_data.action = action
    # Blender 4.4+: an action holds slots; pick the first one if none was auto-assigned.
    ad = obj.animation_data
    if hasattr(ad, 'action_slot') and ad.action_slot is None and len(action.slots):
        ad.action_slot = action.slots[0]
    # Importers stash the other clips in NLA tracks; mute them so only this action plays.
    for track in ad.nla_tracks:
        track.mute = True


def action_range(action):
    s, e = action.frame_range
    return float(s), float(e)


def short_action_name(name):
    """FBX imports name actions 'Armature|Clip'; keep only the clip part."""
    return name.split('|')[-1]


def write_action(tgt, name, frames):
    """frames: list of {bone: (loc, quat)} -> new action on tgt, keyed at 0..N."""
    for old in [a for a in bpy.data.actions if a.name == name]:
        old.name = name + '_previous'
    act = bpy.data.actions.new(name)
    assign_action(tgt, act)
    bones = list(frames[0].keys())
    n = len(frames)
    bag = None
    for bn in bones:
        pb = tgt.pose.bones[bn]
        pb.rotation_mode = 'QUATERNION'
        # Keep quaternions on one hemisphere so interpolation never flips.
        prev = None
        quats = []
        for fr in frames:
            q = fr[bn][1].copy()
            if prev is not None and prev.dot(q) < 0:
                q.negate()
            quats.append(q)
            prev = q
        for prop, count, get in (('location', 3, lambda i, k: frames[i][bn][0][k]),
                                 ('rotation_quaternion', 4, lambda i, k: quats[i][k])):
            path = f'pose.bones["{bn}"].{prop}'
            for k in range(count):
                # group_name= only exists from Blender 5.0, so group through the channelbag (4.4+).
                fc = act.fcurve_ensure_for_datablock(tgt, path, index=k)
                if bag is None:
                    bag = act.layers[0].strips[0].channelbag(act.slots[0])
                fc.group = bag.groups.get(bn) or bag.groups.new(bn)
                fc.keyframe_points.add(n)
                co = []
                for i in range(n):
                    co += [float(i), float(get(i, k))]
                fc.keyframe_points.foreach_set('co', co)
                fc.keyframe_points.foreach_set('interpolation', [1] * n)  # LINEAR
                fc.update()
    act.use_fake_user = True
    act.use_frame_range = True
    act.frame_start, act.frame_end = 0, n - 1
    return act


# ---------------------------------------------------------------- bone mapping

SIDES = ('l', 'r')
FINGERS = ('thumb', 'index', 'middle', 'ring', 'pinky')

# Canonical names, parent-first. Fingers have three segments each.
CANONICAL = ['root', 'pelvis', 'spine_01', 'spine_02', 'spine_03', 'neck_01', 'Head']
for _s in SIDES:
    CANONICAL += [f'clavicle_{_s}', f'upperarm_{_s}', f'lowerarm_{_s}', f'hand_{_s}']
    CANONICAL += [f'{f}_0{i}_{_s}' for f in FINGERS for i in (1, 2, 3)]
    CANONICAL += [f'thigh_{_s}', f'calf_{_s}', f'foot_{_s}', f'ball_{_s}']

REQUIRED = ['pelvis', 'Head'] + [f'{b}_{s}' for s in SIDES for b in
                                 ('upperarm', 'lowerarm', 'hand', 'thigh', 'calf', 'foot')]

# Semantic parent of each canonical bone (used for chain positions and fallbacks).
CANON_PARENT = {'root': None, 'pelvis': 'root', 'spine_01': 'pelvis', 'spine_02': 'spine_01',
                'spine_03': 'spine_02', 'neck_01': 'spine_03', 'Head': 'neck_01'}
# Bones whose aim (head towards this child's head) is aligned between rigs; others inherit.
CANON_AIM = {'pelvis': ['spine_01', 'spine_02', 'spine_03', 'neck_01', 'Head'],
             'spine_01': ['spine_02', 'spine_03', 'neck_01', 'Head'],
             'spine_02': ['spine_03', 'neck_01', 'Head'], 'spine_03': ['neck_01', 'Head'],
             'neck_01': ['Head']}
for _s in SIDES:
    CANON_PARENT.update({f'clavicle_{_s}': 'spine_03', f'upperarm_{_s}': f'clavicle_{_s}',
                         f'lowerarm_{_s}': f'upperarm_{_s}', f'hand_{_s}': f'lowerarm_{_s}',
                         f'thigh_{_s}': 'pelvis', f'calf_{_s}': f'thigh_{_s}',
                         f'foot_{_s}': f'calf_{_s}', f'ball_{_s}': f'foot_{_s}'})
    CANON_AIM.update({f'clavicle_{_s}': [f'upperarm_{_s}'], f'upperarm_{_s}': [f'lowerarm_{_s}'],
                      f'lowerarm_{_s}': [f'hand_{_s}'], f'thigh_{_s}': [f'calf_{_s}'],
                      f'calf_{_s}': [f'foot_{_s}']})
    for f in FINGERS:
        CANON_PARENT[f'{f}_01_{_s}'] = f'hand_{_s}'
        CANON_PARENT[f'{f}_02_{_s}'] = f'{f}_01_{_s}'
        CANON_PARENT[f'{f}_03_{_s}'] = f'{f}_02_{_s}'
        CANON_AIM[f'{f}_01_{_s}'] = [f'{f}_02_{_s}', f'{f}_03_{_s}']
        CANON_AIM[f'{f}_02_{_s}'] = [f'{f}_03_{_s}']

# Letters-only bone stems (side and digits removed) -> canonical stem.
STEMS = {
    'root': 'root',
    'pelvis': 'pelvis', 'hips': 'pelvis', 'hip': 'pelvis',
    'neck': 'neck', 'head': 'head',
    'clavicle': 'clavicle', 'shoulder': 'clavicle', 'collar': 'clavicle', 'collarbone': 'clavicle',
    'upperarm': 'upperarm', 'arm': 'upperarm', 'uparm': 'upperarm',
    'lowerarm': 'lowerarm', 'forearm': 'lowerarm', 'elbow': 'lowerarm',
    'hand': 'hand', 'palm': 'hand', 'wrist': 'hand',
    'thigh': 'thigh', 'upperleg': 'thigh', 'upleg': 'thigh',
    'calf': 'calf', 'lowerleg': 'calf', 'leg': 'calf', 'shin': 'calf', 'knee': 'calf',
    'foot': 'foot', 'ankle': 'foot',
    'ball': 'ball', 'toe': 'ball', 'toes': 'ball', 'toebase': 'ball',
}
# UE5 metacarpals sit between the hand and the first finger joint; they are not finger segments.
IGNORE = re.compile(r'(end|leaf|top|pole|target|twist|roll|ik|ctrl|control|helper|socket|weapon|prop)$|_end|nub|metacarpal', re.I)
PREFIX = re.compile(r'^(mixamorig\d*[:_]|[A-Za-z0-9]+:|bip\d*[ _]|def[-_]|orig[-_]|cc_base_|rl_)', re.I)
SIDE_RULES = [
    re.compile(r'^(left|right)[ ._-]*(.+)$', re.I),
    re.compile(r'^(.+?)[ ._-]*(left|right)$', re.I),
    re.compile(r'^(.+?)[ ._-](l|r)$', re.I),
    re.compile(r'^(l|r)[ ._-](.+)$', re.I),
]


def parse_bone_name(name):
    """-> (side 'l'/'r'/None, letters-only stem, digits)"""
    n = PREFIX.sub('', name)
    side = None
    for i, rule in enumerate(SIDE_RULES):
        m = rule.match(n)
        if m:
            a, b = m.group(1), m.group(2)
            s, n = (a, b) if i in (0, 3) else (b, a)
            side = s[0].lower()
            break
    digits = ''.join(re.findall(r'\d+', n))
    stem = re.sub(r'[^a-z]', '', n.lower())
    return side, stem, digits


def _world_head(arm, bone):
    return arm.matrix_world @ bone.head_local


def detect_bone_map(arm):
    """Map canonical names to this armature's bone names.

    Name matching first (UE/UAL, Mixamo, generic .L/_l/Left naming), then structure: the pelvis is
    the shared parent of the thighs, the spine is the chain from there to the clavicles' parent.
    Returns (mapping dict, notes list).
    """
    bones = arm.data.bones
    notes = []
    parsed = {}
    for b in bones:
        if IGNORE.search(b.name):
            continue
        parsed[b.name] = parse_bone_name(b.name)

    # Fix name sides by geometry: a 'left' bone should lie on the same side as the other left bones.
    left_pts = [_world_head(arm, bones[n]) for n, (s, _, _) in parsed.items() if s == 'l']
    right_pts = [_world_head(arm, bones[n]) for n, (s, _, _) in parsed.items() if s == 'r']
    if left_pts and right_pts:
        lc = sum(left_pts, Vector()) / len(left_pts)
        rc = sum(right_pts, Vector()) / len(right_pts)
        axis = (lc - rc)
        axis.z = 0
        if axis.length > 1e-6:
            axis.normalize()
            mid = (lc + rc) / 2
            for n, (s, stem, d) in list(parsed.items()):
                if s is None:
                    continue
                off = (_world_head(arm, bones[n]) - mid).dot(axis)
                geo = 'l' if off > 0 else 'r'
                if abs(off) > 0.02 * axis.length and geo != s:
                    parsed[n] = (geo, stem, d)
                    notes.append(f'{n}: name says side {s}, position says {geo}; using {geo}')

    def depth(b):
        d = 0
        while b.parent:
            d, b = d + 1, b.parent
        return d

    mapping = {}

    def put(canon, name):
        if canon not in mapping:
            mapping[canon] = name

    # Name-based candidates, shallowest bone wins.
    for name in sorted(parsed, key=lambda n: depth(bones[n])):
        side, stem, digits = parsed[name]
        # Toes can carry finger names (Character Creator: IndexToe1, MidToe1); they are not fingers.
        finger = None if 'toe' in stem else (next((f for f in FINGERS if f in stem), None)
                                              or ('middle' if 'mid' in stem else None))
        if finger and side:
            seg = int(digits[-1]) if digits else 1  # 'Thumb' / 'Thumb2' / 'thumb_03'
            if 1 <= seg <= 3:
                put(f'{finger}_0{seg}_{side}', name)
            continue
        canon_stem = STEMS.get(stem)
        if not canon_stem:
            continue
        if canon_stem in ('root', 'pelvis', 'neck', 'head'):
            if side is None:
                put({'neck': 'neck_01', 'head': 'Head'}.get(canon_stem, canon_stem), name)
        elif side:
            put(f'{canon_stem}_{side}', name)

    # Structure: pelvis is the common parent of both thighs.
    tl, tr = mapping.get('thigh_l'), mapping.get('thigh_r')
    if tl and tr and bones[tl].parent and bones[tl].parent == bones[tr].parent:
        p = bones[tl].parent.name
        if mapping.get('pelvis') != p:
            if 'pelvis' in mapping:
                notes.append(f"pelvis: '{mapping['pelvis']}' is not the thighs' parent; using '{p}'")
            mapping['pelvis'] = p
    if 'pelvis' in mapping:
        pb = bones[mapping['pelvis']]
        if pb.parent and pb.parent.parent is None and 'root' not in mapping:
            mapping['root'] = pb.parent.name
        if mapping.get('root') == mapping['pelvis']:
            del mapping['root']
    # Spine: chain from the pelvis up to the bone that carries the clavicles (or upper arms).
    chest_child = mapping.get('clavicle_l') or mapping.get('upperarm_l')
    if 'pelvis' in mapping and chest_child:
        # Some rigs (Character Creator: Hip carries both Pelvis->thighs and Waist->spine) split
        # the hips; the pelvis must be the bone both the legs and the spine hang from.
        chest_up = {p.name for p in bones[chest_child].parent_recursive}
        if mapping['pelvis'] not in chest_up:
            common = next((p.name for p in bones[mapping['pelvis']].parent_recursive if p.name in chest_up), None)
            if common:
                notes.append(f"pelvis: '{mapping['pelvis']}' carries the legs but not the spine; using '{common}'")
                mapping['pelvis'] = common
                pb = bones[common]
                if pb.parent and pb.parent.parent is None and mapping.get('root') in (None, common):
                    mapping['root'] = pb.parent.name
                if mapping.get('root') == common:
                    del mapping['root']
        chain, b = [], bones[chest_child].parent
        while b and b.name != mapping['pelvis']:
            chain.append(b.name)
            b = b.parent
        if b is not None:
            chain.reverse()
            if chain:
                mapping['spine_03'] = chain[-1]
                if len(chain) >= 2:
                    mapping['spine_01'] = chain[0]
                if len(chain) >= 3:
                    mapping['spine_02'] = chain[len(chain) // 2]
                else:
                    mapping.pop('spine_02', None)
    # Neck fallback: the bone between the chest and the head.
    if 'Head' in mapping and 'neck_01' not in mapping:
        hp = bones[mapping['Head']].parent
        if hp and hp.name not in mapping.values():
            mapping['neck_01'] = hp.name
    # Never map one bone twice.
    seen = {}
    for canon in CANONICAL:
        n = mapping.get(canon)
        if n is None:
            continue
        if n in seen:
            notes.append(f'{n} matched both {seen[n]} and {canon}; keeping {seen[n]}')
            del mapping[canon]
        else:
            seen[n] = canon
    return {c: mapping[c] for c in CANONICAL if c in mapping}, notes


SIDE_SWAPS = [
    re.compile(r'(Left|Right|left|right|LEFT|RIGHT)'),
    re.compile(r'(?<=[._\-])([lLrR])(?=$|[._\-\d])'),
    re.compile(r'^([lLrR])(?=[._\-])'),
]
SWAP = {'Left': 'Right', 'Right': 'Left', 'left': 'right', 'right': 'left', 'LEFT': 'RIGHT', 'RIGHT': 'LEFT',
        'l': 'r', 'r': 'l', 'L': 'R', 'R': 'L'}


def partner(name, bones):
    """The opposite-side bone's name (hand.L -> hand.R), or name itself if there is none in bones."""
    for pat in SIDE_SWAPS:
        cand = pat.sub(lambda m: SWAP[m.group(1)], name)
        if cand != name and cand in bones:
            return cand
    return name


def load_bone_map(arm, map_arg):
    """'auto' or a JSON file {canonical: bone}. Missing entries are auto-detected. An entry for one
    side also maps its twin (hand_l: Wrist.L gives hand_r: Wrist.R) unless the file names the twin."""
    mapping, notes = detect_bone_map(arm)
    if map_arg and map_arg != 'auto':
        with open(map_arg, encoding='utf-8') as f:
            custom = json.load(f)
        unknown = [c for c in custom if c not in CANONICAL]
        if unknown:
            fail(f'Unknown canonical names in {map_arg}: {unknown}. Valid: {CANONICAL}')
        missing_bones = [b for b in custom.values() if b and b not in arm.data.bones]
        if missing_bones:
            fail(f'Bones in {map_arg} not found in the armature: {missing_bones}')
        names = [b.name for b in arm.data.bones]
        mirrored = []
        for c, b in list(custom.items()):
            twin = c[:-1] + {'l': 'r', 'r': 'l'}[c[-1]] if c.endswith(('_l', '_r')) else None
            if b and twin and twin not in custom:
                other = partner(b, names)
                if other != b:
                    custom[twin] = other
                    mirrored.append(f'{twin}: {other}')
        for c, b in custom.items():
            if b:
                # One bone serves one canonical name: a detected use elsewhere gives way to the file.
                for other in [k for k, v in mapping.items() if v == b and k != c and k not in custom]:
                    del mapping[other]
                    notes.append(f'{b}: mapped to {c} by {map_arg}, no longer to {other}')
                mapping[c] = b
            else:
                mapping.pop(c, None)
        notes.append(f'custom map applied from {map_arg}')
        if mirrored:
            notes.append(f"mirrored from the other side: {', '.join(mirrored)}")
    return mapping, notes


def saved_bone_map(arm, blend_path):
    """The bone map a working file was made with: the one edit saved in it, else the retarget
    report next to it, else detection."""
    saved = [json.loads(arm['ca_map'])] if 'ca_map' in arm else []
    report = os.path.splitext(os.path.abspath(blend_path))[0] + '.retarget.json'
    if os.path.isfile(report):
        with open(report, encoding='utf-8') as f:
            saved.append(json.load(f).get('mapping') or {})
    for m in saved:
        if m and all(b in arm.data.bones for b in m.values()):
            return m
    return detect_bone_map(arm)[0]


def missing_required(mapping):
    return [c for c in REQUIRED if c not in mapping]


def classify_rig(arm):
    names = {b.name for b in arm.data.bones}
    if any(n.lower().startswith('mixamorig') for n in names):
        return 'mixamo'
    if {'pelvis', 'spine_01', 'thigh_l', 'calf_l', 'upperarm_l'} <= names:
        return 'ue-mannequin (same naming as the library)'
    return 'generic'


# ---------------------------------------------------------------- rest-pose facts


def rest_facts(arm, mapping):
    """Height, ground, facing and arm pose of a character in its rest pose (world space)."""
    heads = [arm.matrix_world @ b.head_local for b in arm.data.bones]
    meshes = skinned_meshes(arm)
    zs = []
    for m in meshes:
        zs += [(m.matrix_world @ Vector(c)).z for c in m.bound_box]
    ground = min(zs) if zs else min(h.z for h in heads)
    top = max(zs) if zs else max(h.z for h in heads)
    facts = {'height_m': round(top - ground, 3), 'ground_z': round(ground, 4)}

    def wh(c):
        return arm.matrix_world @ arm.data.bones[mapping[c]].head_local

    if all(k in mapping for k in ('thigh_l', 'thigh_r')):
        left = wh('thigh_l') - wh('thigh_r')
    elif all(k in mapping for k in ('upperarm_l', 'upperarm_r')):
        left = wh('upperarm_l') - wh('upperarm_r')
    else:
        left = None
    if left is not None:
        left.z = 0
        fwd = left.normalized().cross(Vector((0, 0, 1)))
        facts['forward'] = [round(v, 3) for v in fwd]
        axis = max(((abs(fwd.x), ('+X' if fwd.x > 0 else '-X')), (abs(fwd.y), ('+Y' if fwd.y > 0 else '-Y'))))[1]
        facts['facing'] = axis
    if all(k in mapping for k in ('upperarm_l', 'lowerarm_l')):
        d = wh('lowerarm_l') - wh('upperarm_l')
        ang = abs(Vector((0, 0, -1)).angle(d.normalized()) * 57.2958)
        facts['arm_angle_from_down_deg'] = round(ang, 1)
        facts['rest_pose'] = 'T-pose' if ang > 70 else ('A-pose' if ang > 25 else 'arms down')
    return facts


AXES = {'up': ('up', 1), 'down': ('up', -1), 'left': ('left', 1), 'right': ('left', -1),
        'forward': ('forward', 1), 'back': ('forward', -1)}


def axis_spec(axes, text, flag):
    """'up:20,left:-10' -> [(axis vector, value)], with axes {'up', 'left', 'forward': Vector}."""
    out = []
    for part in text.split(','):
        k, _, v = part.partition(':')
        k = k.strip().lower()
        if k not in AXES:
            fail(f"--{flag}: axis '{k}' must be one of {', '.join(AXES)}")
        try:
            val = float(v)
        except ValueError:
            fail(f"--{flag}: '{part}' needs a number, like up:20")
        name, sign = AXES[k]
        out.append((axes[name] * sign, val))
    return out


def write_json(path, data):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2)


def library_dir(cache=None):
    return os.path.join(cache or DEFAULT_CACHE, 'downloads')


def library_files(cache=None):
    """{pack: {'inplace': glb, 'root': glb}} for every UAL pack found in the cache."""
    root = library_dir(cache)
    out = {}
    for dirpath, _, files in os.walk(root):
        for f in files:
            m = re.match(r'^(UAL\d+)_Standard(_RM)?\.glb$', f)
            if m:
                out.setdefault(m.group(1).lower(), {})['root' if m.group(2) else 'inplace'] = os.path.join(dirpath, f)
    return out
