"""Build the searchable clip catalogue from the downloaded library.

blender -b --factory-startup -P build_catalogue.py -- [--cache DIR] [--out FILE]

Writes <cache>/catalogue.json: one entry per clip with name, pack, duration, frame count, loop
flags, tags, root-motion travel and the source files for in-place and root-motion variants.
"""
import os
import re
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
from ca_common import (LIBRARY_FPS, DEFAULT_CACHE, action_range, assign_action, fail, import_file,  # noqa: E402
                       library_files, main_armature, reset_scene, script_args, write_json)

# Word -> tags. Words come from splitting clip names on '_' and case changes.
TAG_WORDS = {
    'idle': ['idle'], 'walk': ['locomotion', 'walk'], 'jog': ['locomotion', 'run'], 'run': ['locomotion', 'run'],
    'sprint': ['locomotion', 'run', 'fast'], 'crouch': ['locomotion', 'crouch', 'stealth'],
    'jump': ['jump', 'air'], 'fall': ['air'], 'land': ['jump'], 'roll': ['dodge', 'evade'],
    'dodge': ['dodge', 'evade'], 'swim': ['swim', 'water'], 'climb': ['climb', 'traversal'],
    'punch': ['combat', 'melee', 'unarmed'], 'kick': ['combat', 'melee', 'unarmed'],
    'sword': ['combat', 'melee', 'weapon', 'sword'], 'pistol': ['combat', 'ranged', 'weapon', 'gun'],
    'rifle': ['combat', 'ranged', 'weapon', 'gun'], 'shoot': ['combat', 'ranged'], 'aim': ['combat', 'aim'],
    'reload': ['combat', 'weapon'], 'spell': ['magic', 'cast'], 'hit': ['reaction', 'damage'],
    'death': ['reaction', 'death'], 'die': ['reaction', 'death'], 'dance': ['emote', 'dance'],
    'talking': ['social', 'talk'], 'wave': ['emote', 'social'], 'sitting': ['sit'], 'sit': ['sit'],
    'interact': ['interact'], 'pickup': ['interact', 'pickup'], 'push': ['interact', 'push'],
    'driving': ['vehicle'], 'fixing': ['interact', 'work'], 'torch': ['prop'], 'kneeling': ['kneel'],
    'formal': ['walk', 'style'], 'fwd': ['forward'], 'bwd': ['backward'], 'back': ['backward'],
    'left': ['strafe'], 'right': ['strafe'], 'enter': ['transition'], 'exit': ['transition'],
    'start': ['transition'], 'stop': ['transition'], 'tpose': ['pose', 'reference'],
}


def words_of(name):
    parts = re.split(r'[_\s\-]+|(?<=[a-z])(?=[A-Z])|(?<=\D)(?=\d)', name)
    return [p.lower() for p in parts if p]


def pose_signature(arm):
    return {pb.name: (pb.matrix.copy(), (arm.matrix_world @ pb.head).copy()) for pb in arm.pose.bones}


def pose_difference_deg(a, b):
    worst = 0.0
    for name, (ma, _) in a.items():
        mb = b[name][0]
        q = ma.to_quaternion().rotation_difference(mb.to_quaternion())
        worst = max(worst, abs(q.angle) * 57.2958 if q.angle <= 3.1416 else (6.2832 - q.angle) * 57.2958)
    return worst


def scan(path):
    """-> {clip: info} for every action in one library file."""
    reset_scene(LIBRARY_FPS)
    objs, acts = import_file(path)
    arm = main_armature(objs)
    if arm is None:
        fail(f'No armature in {path}')
    sc = bpy.context.scene
    root = arm.pose.bones.get('root')
    out = {}
    for act in acts:
        assign_action(arm, act)
        s, e = action_range(act)
        sc.frame_set(int(s))
        first = pose_signature(arm)
        r0 = (arm.matrix_world @ root.head) if root else None
        sc.frame_set(int(round(e)))
        last = pose_signature(arm)
        r1 = (arm.matrix_world @ root.head) if root else None
        travel = 0.0
        if r0 is not None:
            d = r1 - r0
            d.z = 0
            travel = d.length
        out[act.name] = {
            'frame_start': int(round(s)), 'frame_end': int(round(e)),
            'pose_closes': pose_difference_deg(first, last) < 3.0,
            'root_travel_m': round(travel, 3),
        }
    return out


def main():
    args = script_args()
    cache, out = DEFAULT_CACHE, None
    i = 0
    while i < len(args):
        if args[i] == '--cache':
            cache, i = args[i + 1], i + 1
        elif args[i] == '--out':
            out, i = args[i + 1], i + 1
        else:
            fail(f'Unknown argument {args[i]}')
        i += 1
    out = out or os.path.join(cache, 'catalogue.json')
    packs = library_files(cache)
    if not packs:
        fail(f'No library files under {os.path.join(cache, "downloads")}. Run setup first.')
    clips = []
    for pack, files in sorted(packs.items()):
        if 'inplace' not in files:
            fail(f'{pack}: in-place file missing')
        inplace = scan(files['inplace'])
        rm = scan(files['root']) if 'root' in files else {}
        for name, info in sorted(inplace.items()):
            frames = info['frame_end'] - info['frame_start']
            words = words_of(name)
            tags = sorted({t for w in words for t in TAG_WORDS.get(w, [])})
            loop = 'loop' in words
            rinfo = rm.get(name, {})
            travel = rinfo.get('root_travel_m', 0.0)
            dur = frames / LIBRARY_FPS
            clips.append({
                'id': f'{pack}/{name}', 'name': name, 'pack': pack,
                'frames': frames, 'fps': LIBRARY_FPS, 'duration_s': round(dur, 3),
                'loop': loop, 'pose_closes': info['pose_closes'],
                'tags': tags,
                'root_motion_available': name in rm,
                'root_travel_m': travel,
                'speed_m_s': round(travel / dur, 2) if dur and travel > 0.05 else 0.0,
                'file_inplace': files['inplace'], 'file_root_motion': files.get('root'),
            })
    write_json(out, {'fps': LIBRARY_FPS, 'rig': 'Quaternius UAL (UE mannequin bone names)',
                     'licence': 'CC0 1.0, Quaternius Universal Animation Library 1 and 2',
                     'count': len(clips), 'clips': clips})
    print(f'Catalogue: {len(clips)} clips from {len(packs)} packs -> {out}')


main()
