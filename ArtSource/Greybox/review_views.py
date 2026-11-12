# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-11
#  Prompt    : "First-pass greybox from the 3D modeller brief: check views at eye height from both towers"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Renders the brief's review views of floor_pair_greybox.blend at player eye height:
#              the route to the stairs from inside, and the rival's view of the stair entrance from
#              the same floor, one above and one below. It can also score how well the dark-suit
#              stand-ins read from the rival tower, so glass and lighting changes are compared by
#              number as well as by eye. Kept beside the model so every change is judged from the
#              same cameras.
#  Modified  : Claude Opus 5.5, 2026-11-11 — interior ceiling light, sun direction, glass alpha
#              and open-bay options, and the stand-in read score.
# -----------------------------------------------------------------------------
"""Usage:
  blender -b ArtSource/Greybox/floor_pair_greybox.blend -P ArtSource/Greybox/review_views.py -- <out folder> [options]

Options (defaults are the recommended look; see the README beside this file):
  --glass-alpha F        façade and partition glass opacity, 0-1
  --glass-reflect F      glass sky reflection, 0 (none) to 1; 0.5 is ordinary glass
  --no-facade-glass      hide the façade panes: the repo's open window bays
  --interior F           ceiling light, watts per square metre of main floor (0 turns it off)
  --palette greybox|bible  greybox colours, or the art bible's wall and slab swatches
  --sun along|across     sun travelling along the street (both façades lit alike) or across it
  --measure              also score how well A's test-floor stand-ins read from tower B
  --views a,b            render only these views

Cameras are given in Unity world terms (metres, +Y up) for the tower layout TowerBuilder makes:
tower A's window edge at z = -GAP/2 facing +Z, tower B's at +GAP/2 turned 180 degrees.
Nothing is saved back to the .blend.
"""
import argparse
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

# ---- Values the views depend on (sources in comments)
FLOOR_H = 4.5          # DefaultTower.asset FloorHeight, metres
GAP = 12.0             # DefaultTower.asset Gap, metres
EYE = 1.6              # TestBlast.asset EyeHeight: the first-person camera, metres above the feet
FOV_V = 60.0           # vertical field of view, degrees. WHY: Unity's camera default; the project sets no other
RES = (1280, 720)      # render size, pixels
SAMPLES = 16           # EEVEE samples. WHY: enough to settle transparency on the glass; review images, not beauty

SKY = (0.42, 0.66, 0.95)   # bright blue sky, art direction, sRGB 0-1
GROUND = (0.35, 0.36, 0.38)
SKY_LIGHT = 0.35     # how much the sky lights the scene. WHY: at 1.0 the blue sky tints every pale surface blue
SUN = 4.0            # sun strength, W/m². WHY: carries the light the dimmed sky no longer gives
SUN_ELEVATION = 40.0 # degrees above the horizon
B_EYE_X = 1.0        # B-side cameras stand 1 m off centre. WHY: B's window stand-in stands on the centre line

# Recommended defaults, chosen by the comparison in the README. Each is an option above.
GLASS_ALPHA = 0.05   # WHY: through two panes 0.18 cut the suits' contrast to a third; see README
GLASS_REFLECT = 0.5  # glass reflection, Blender's 'Specular IOR Level'; 0.5 is ordinary glass
INTERIOR = 10.0      # W/m² of main floor. WHY: a room as bright as the street behind it keeps suits dark against it
SUN_MODE = 'along'   # WHY: with the sun across the street one façade is always in its own shadow
PANEL_GLOW = 6.0     # emission strength of the ceiling panels, so they read as lit fittings

PALETTE = 'bible'    # WHY: pale walls behind a dark suit are most of the read; see README
# Art bible §03 swatches on the greybox's materials: Paper White #F3F4F4 walls and frames,
# Silver Grey #C9CED3 slabs. The suits stay Deep Navy-dark, as the bible's suits are.
BIBLE_PALETTE = {
    'Wall_ServiceOpaque': (0xF3 / 255, 0xF4 / 255, 0xF4 / 255),
    'Facade_WhiteFrame': (0xF3 / 255, 0xF4 / 255, 0xF4 / 255),
    'Slab_PaleGrey': (0xC9 / 255, 0xCE / 255, 0xD3 / 255),
}

# Score: a pixel counts as changed by the stand-ins when its luminance moves more than this.
READ_THRESHOLD = 0.02


def a_world(x, y, z, level):
    """Tower A floor-local Unity point -> Unity world."""
    return (x, y + level * FLOOR_H, z - GAP * 0.5)


def b_world(x, y, z, level):
    """Tower B floor-local Unity point -> Unity world (turned 180 degrees)."""
    return (-x, y + level * FLOOR_H, -z + GAP * 0.5)


def to_blender(u):
    """Unity world (x, y, z) -> Blender (-x, -z, y); the same mapping as the model script."""
    return Vector((-u[0], -u[2], u[1]))


A_STAIR_ENTRY = a_world(-1.2, 1.2, -8.0, 1)   # the open side of the cage on A's test floor, chest height

# name -> (eye, target), Unity world. What each answers is in the name.
VIEWS = {
    'A2_route_to_stairs':        (a_world(-10.0, EYE, -1.5, 1), a_world(-1.5, 1.0, -8.5, 1)),
    'A2_across_the_gap':         (a_world(0.0, EYE, -0.8, 1), a_world(0.0, EYE, 30.0, 1)),
    'A2_from_meeting_room':      (a_world(-8.4, EYE, -8.6, 1), a_world(0.0, 1.2, 4.0, 1)),
    'B2_sees_A_stairs':          (b_world(B_EYE_X, EYE, -0.8, 1), A_STAIR_ENTRY),
    # WHY -8: mid-bay. Pillars stand every 4 m from the edge (-14, -10, ...), so -10 is behind one.
    'B2_sees_A_stairs_offaxis':  (b_world(-8.0, EYE, -0.8, 1), A_STAIR_ENTRY),
    'B3_sees_A_stairs_below':    (b_world(B_EYE_X, EYE, -0.8, 2), A_STAIR_ENTRY),
    'B1_sees_A_stairs_above':    (b_world(B_EYE_X, EYE, -0.8, 0), A_STAIR_ENTRY),
    'Overview':                  ((40.0, 22.0, 18.0), (0.0, 5.0, -6.0)),
}
MEASURED_PREFIX = 'B'          # the rival's views of tower A
MEASURED_PROXIES = 'A_F2_Proxy'  # the stand-ins on A's test floor


def linear(c):
    return (*[v ** 2.2 for v in c], 1.0)


def setup_look(scene, opts):
    for engine in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    if hasattr(scene, 'eevee'):
        scene.eevee.taa_render_samples = SAMPLES
    scene.render.resolution_x, scene.render.resolution_y = RES
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'Standard'

    world = bpy.data.worlds.new('ReviewSky')
    world.use_nodes = True
    bg = world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = linear(SKY)
    bg.inputs['Strength'].default_value = SKY_LIGHT
    scene.world = world

    sun = bpy.data.objects.new('ReviewSun', bpy.data.lights.new('ReviewSun', 'SUN'))
    sun.data.energy = SUN
    tilt = math.radians(90.0 - SUN_ELEVATION)
    # 'along': travelling along Blender X, the street's length, so neither façade faces away.
    # 'across': the first pass's light, from behind tower B toward A at an angle.
    sun.rotation_euler = (tilt, 0.0, math.radians(90.0 if opts.sun == 'along' else 35.0))
    scene.collection.objects.link(sun)

    me = bpy.data.meshes.new('Street')
    s = 200.0
    me.from_pydata([(-s, -s, -0.3), (s, -s, -0.3), (s, s, -0.3), (-s, s, -0.3)], [], [(0, 1, 2, 3)])
    street = bpy.data.objects.new('Street', me)
    mat = bpy.data.materials.new('Street')
    mat.use_nodes = True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = linear(GROUND)
    me.materials.append(mat)
    scene.collection.objects.link(street)

    glass = bpy.data.materials.get('Glass_CoolBlue')
    if glass and glass.use_nodes:
        bsdf = glass.node_tree.nodes.get('Principled BSDF')
        if bsdf:
            bsdf.inputs['Alpha'].default_value = opts.glass_alpha
            # Blender's default is 0.5 (an ordinary 4% reflection); 0 removes the sky reflection entirely.
            bsdf.inputs['Specular IOR Level'].default_value = opts.glass_reflect
        if hasattr(glass, 'surface_render_method'):
            glass.surface_render_method = 'BLENDED'
        elif hasattr(glass, 'blend_method'):
            glass.blend_method = 'BLEND'

    if opts.palette == 'bible':
        for name, colour in BIBLE_PALETTE.items():
            mat = bpy.data.materials.get(name)
            bsdf = mat.node_tree.nodes.get('Principled BSDF') if mat and mat.use_nodes else None
            if bsdf:
                bsdf.inputs['Base Color'].default_value = linear(colour)
    add_ceiling_light(scene, opts.interior)

    # The size reference stands in the street for the check; keep it out of the pictures.
    ref = bpy.data.objects.get('Proxy_SizeReference')
    if ref:
        ref.hide_render = True
    if opts.no_facade_glass:
        for ob in bpy.data.objects:
            if ob.name.endswith('_FacadeGlass'):
                ob.hide_render = True


def add_ceiling_light(scene, watts_per_m2):
    """One downward area light under each floor's ceiling-panel grid, sized to that grid.

    WHY one rectangle per floor instead of a light per panel: the review needs the room's overall
    brightness against the street, not the pattern of pools under each fitting, and 40-odd lights
    a floor would slow every render. Unity will use its own lights; this only sets the level.
    """
    panel = bpy.data.materials.get('Light_CeilingPanel')
    if panel and panel.use_nodes:
        bsdf = panel.node_tree.nodes.get('Principled BSDF')
        if bsdf:
            bsdf.inputs['Emission Color'].default_value = (1.0, 1.0, 1.0, 1.0)
            bsdf.inputs['Emission Strength'].default_value = PANEL_GLOW if watts_per_m2 > 0 else 0.0
    if watts_per_m2 <= 0:
        return
    for ob in [o for o in bpy.data.objects if o.name.endswith('_CeilingLights')]:
        corners = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
        lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
        hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
        size = hi - lo
        data = bpy.data.lights.new(ob.name + '_Area', 'AREA')
        data.shape = 'RECTANGLE'
        data.size, data.size_y = size.x, size.y
        data.energy = watts_per_m2 * size.x * size.y
        light = bpy.data.objects.new(ob.name + '_Area', data)
        light.location = ((lo.x + hi.x) * 0.5, (lo.y + hi.y) * 0.5, lo.z - 0.01)  # just under the panels, shining down
        scene.collection.objects.link(light)


def luminance(path):
    img = bpy.data.images.load(path)
    px = np.empty(len(img.pixels), dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    px = px.reshape(-1, 4)
    return 0.2126 * px[:, 0] + 0.7152 * px[:, 1] + 0.0722 * px[:, 2]


def render_views(scene, out_dir, opts):
    cam_data = bpy.data.cameras.new('ReviewCam')
    cam_data.sensor_fit = 'VERTICAL'
    cam_data.angle_y = math.radians(FOV_V)
    cam_data.clip_start = 0.05
    cam = bpy.data.objects.new('ReviewCam', cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    proxies = [o for o in bpy.data.objects if o.name.startswith(MEASURED_PROXIES)]

    scores = []
    for name, (eye, target) in VIEWS.items():
        if opts.views and name not in opts.views:
            continue
        e, t = to_blender(eye), to_blender(target)
        cam.location = e
        cam.rotation_euler = (t - e).to_track_quat('-Z', 'Y').to_euler()
        path = os.path.join(out_dir, f'{name}.png')
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        print('wrote', path)

        if opts.measure and name.startswith(MEASURED_PREFIX):
            # Render again without A's stand-ins: the pixels that change are where a suit shows,
            # and how much they change is how strongly it stands out from what is behind it.
            for o in proxies:
                o.hide_render = True
            empty = os.path.join(out_dir, f'{name}_noproxies.png')
            scene.render.filepath = empty
            bpy.ops.render.render(write_still=True)
            for o in proxies:
                o.hide_render = False
            diff = np.abs(luminance(path) - luminance(empty))
            shown = diff > READ_THRESHOLD
            contrast = float(diff[shown].mean()) if shown.any() else 0.0
            scores.append((name, int(shown.sum()), contrast))
            os.remove(empty)

    for name, pixels, contrast in scores:
        print(f'READ {name:28s} suit pixels {pixels:6d}   mean contrast {contrast:.3f}')


def parse(argv):
    ap = argparse.ArgumentParser(prog='review_views.py')
    ap.add_argument('out', nargs='?', default='review_views')
    ap.add_argument('--glass-alpha', type=float, default=GLASS_ALPHA)
    ap.add_argument('--glass-reflect', type=float, default=GLASS_REFLECT)
    ap.add_argument('--no-facade-glass', action='store_true')
    ap.add_argument('--interior', type=float, default=INTERIOR)
    ap.add_argument('--sun', choices=('along', 'across'), default=SUN_MODE)
    ap.add_argument('--palette', choices=('greybox', 'bible'), default=PALETTE)
    ap.add_argument('--measure', action='store_true')
    ap.add_argument('--views', type=lambda s: s.split(','), default=None)
    return ap.parse_args(argv)


def main():
    opts = parse(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    out_dir = os.path.abspath(opts.out)
    os.makedirs(out_dir, exist_ok=True)
    scene = bpy.context.scene
    setup_look(scene, opts)
    render_views(scene, out_dir, opts)


main()
