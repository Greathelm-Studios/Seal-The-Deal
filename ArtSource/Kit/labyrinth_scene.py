# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-12
#  Prompt    : Alex: "update the kit for the labyrinth" (round-5 picks, DECISION_r5.md)
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Snaps the round-5 labyrinth pieces into one 16 x 16 m maze section and renders it at eye
#              height, so the new pieces are judged together as a space against the approved concepts. Its
#              own file because interior_scene.py lays out the old tower floor; this is a review layout,
#              not a level: whoever lays out the game's maze decides the real one.
# -----------------------------------------------------------------------------
"""Usage: blender -b --factory-startup -P ArtSource/Kit/labyrinth_scene.py -- <kit build folder> <out folder> [--save]

Layout, Unity floor-local metres, x 0-16 east, z 0 to -16 south, two storeys of 4.5 m:
  * A 4 x 4 m island at x 4-8, z -4 to -8, made of four rounded returns (the north-west one cobalt), so a
    2 m corridor loops round it.
  * A room at x 8-16, z -10 to -16 behind frosted glass, with a door on its west side and a capsule counter.
  * A block cover in the open junction east of the island, and the cobalt capsule niche on the north wall.
  * A straight stair at x 0.2-2.2 rising south from z -0.8 to -8 into a 4 x 8 m void, arriving on the upper floor,
    whose void edges carry frameless glass guards. Light-line slabs make the ceilings.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from assembly_test import FLOOR_H, CLEAR_H, U, place  # noqa: E402  (the kit's snap helper)
import interior_scene as I                            # noqa: E402  (reuse its look, massing box and glass fix)

SIZE = 16.0              # section width and depth, metres
BAY = 4.0                # kit BAY
VOID = (0.0, 4.0, 0.0, -8.0)   # stair void: x0, x1, z0, z1. No upper slab here; its ceiling is a storey higher
EYE = 1.7                # camera eye height, metres (round-5 views were asked for at 1.7 m)
FOV_V = 60.0             # vertical field of view, degrees (Unity camera default)
FIGURE_H = 1.8           # stand-in height, metres
LIGHT_W_M2 = 10.0        # ceiling light per square metre, watts (interior_scene's tuned level)
RES = (1600, 900)


def in_void(x, z):
    return VOID[0] <= x < VOID[1] and VOID[3] <= z < VOID[2]


def build(build_dir):
    P = lambda piece, x, y, z, yaw=0.0: place(build_dir, piece, x, y, z, yaw)   # noqa: E731
    # Floors and ceilings: ground slabs, the upper floor (except over the void), and a ceiling over everything.
    for i in range(4):
        for j in range(4):
            x, z = i * BAY, -j * BAY
            P('Slab_4x4', x, 0, z)
            if not in_void(x, z - 0.01):
                P('Slab_4x4_LightLine', x, FLOOR_H, z)
            P('Slab_4x4_LightLine', x, 2 * FLOOR_H, z)
    # Perimeter, both storeys: thickness inside the section on every side.
    for level in (0.0, FLOOR_H):
        for i in range(4):
            x = i * BAY
            P('Wall_Solid_4m', x + BAY, level, -SIZE, 180)      # south
            P('Wall_Solid_4m', SIZE, level, -x, 90)              # east, runs toward -Z
            P('Wall_Solid_4m', 0, level, -x - BAY, -90)          # west, pivot at its far end
            if x == 8.0 and level == 0.0:
                P('Wall_Niche_4m', x + BAY, level, -0.2, 180)    # north, niche facing in
            else:
                P('Wall_Solid_4m', x, level, 0)                  # north
    # Island of four rounded returns; NW is cobalt, the landmark at the junction.
    for piece, x, z, yaw in (('Wall_Return_2x2_Cobalt', 4, -4, 0), ('Wall_Return_2x2', 8, -4, 90),
                             ('Wall_Return_2x2', 8, -8, 180), ('Wall_Return_2x2', 4, -8, -90)):
        P(piece, x, 0, z, yaw)
    # Room: frosted glass on the north, door wall and 2 m wall on the west.
    P('Partition_Frost_4m', 8, 0, -10)
    P('Partition_Frost_4m', 12, 0, -10)
    P('Wall_Door_4m', 8, 0, -14, -90)
    P('Door_Leaf', 8 + 0.1, 0, -14 + 1.41, -90)
    P('Wall_Solid_2m', 8, 0, -16, -90)
    P('Counter_Capsule_2m', 12.5, 0, -13.0, 90)
    # Cover in the open junction.
    P('Cover_Block_2m', 12.0, 0, -6.0)
    # Stair into the void, clear of the west wall, its top tread flush with the deck edge at z -8.
    P('Stair_Straight_2m', 0.2, 0, -8.0 + 7.2)
    P('Guard_Glass_2m', 2, FLOOR_H, -8)                      # south edge of the void, beside the stair arrival
    P('Guard_Glass_4m', 4, FLOOR_H, -4, 90)                  # east edge, runs toward -Z
    P('Guard_Glass_4m', 4, FLOOR_H, 0, 90)


def figures():
    suit = bpy.data.materials.new('Standin_Suit')
    suit.use_nodes = True
    suit.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*I.SUIT, 1.0)
    for k, (x, y, z) in enumerate([(12.5, 0, -11.5), (10.5, 0, -2.0), (3.0, 0, -9.5), (1.0, FLOOR_H, -10.0),
                                   (14.0, 0, -14.5)]):
        I.box(f'Standin_{k}_Body', (x - 0.25, x + 0.25), (y, y + FIGURE_H - 0.25), (z - 0.15, z + 0.15), suit)
        I.box(f'Standin_{k}_Head', (x - 0.11, x + 0.11), (y + FIGURE_H - 0.25, y + FIGURE_H), (z - 0.11, z + 0.11), suit)


def lights():
    scene = bpy.context.scene
    for level in (0.0, FLOOR_H):
        for i in range(4):
            for j in range(4):
                x, z = i * BAY + BAY * 0.5, -j * BAY - BAY * 0.5
                if level and in_void(x, z):
                    continue
                top = (2 * FLOOR_H if in_void(x, z) else FLOOR_H) + level - 0.25
                data = bpy.data.lights.new(f'L_{level:g}_{i}_{j}', 'AREA')
                data.shape, data.size, data.size_y = 'RECTANGLE', BAY * 0.8, BAY * 0.8
                data.energy = LIGHT_W_M2 * BAY * BAY
                data.use_shadow = False   # WHY: soft fill only, as interior_scene; the sun carves the shapes
                ob = bpy.data.objects.new(data.name, data)
                ob.location = U(x, top, z)
                scene.collection.objects.link(ob)
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = I.SUN_W
    sun.rotation_euler = (math.radians(50.0), 0.0, math.radians(35.0))
    scene.collection.objects.link(sun)


VIEWS = {
    'L1_corridor_to_cobalt_corner': ((14.0, EYE, -1.5), (4.0, 1.6, -4.0)),
    'L2_junction_cover_niche': ((14.5, EYE, -8.5), (9.0, 1.4, -1.0)),
    'L3_room_through_frost': ((10.0, EYE, -7.5), (13.0, 1.2, -13.0)),
    'L4_loop_round_island': ((3.0, EYE, -15.0), (3.0, 1.6, -5.0)),
    'L5_stair_up': ((3.2, EYE, -12.0), (1.0, 3.5, -2.0)),
}


def render(out):
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = RES
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.angle_y = math.radians(FOV_V)
    scene.collection.objects.link(cam)
    scene.camera = cam

    def shoot(name, eye, tgt):
        e, t = U(*eye), U(*tgt)
        cam.location = e
        cam.rotation_euler = (t - e).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out, f'{name}.png')
        bpy.ops.render.render(write_still=True)
        print('wrote', scene.render.filepath)

    for name, (eye, tgt) in VIEWS.items():
        shoot(name, eye, tgt)
    # Cutaway: everything above the ground storey hidden, seen from above the south-east.
    for o in scene.objects:
        if o.type == 'MESH' and o.matrix_world.translation.z > FLOOR_H - 0.5:
            o.hide_render = True
    cam.data.angle_y = math.radians(45.0)
    shoot('L6_cutaway', (24.0, 22.0, -26.0), (8.0, 0.0, -8.0))


def main():
    argv = sys.argv[sys.argv.index('--') + 1:]
    build_dir, out = os.path.abspath(argv[0]), os.path.abspath(argv[1])
    os.makedirs(out, exist_ok=True)
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob)
    build(build_dir)
    figures()
    lights()
    I.look()
    if '--save' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, 'labyrinth_scene.blend'))
    render(out)


if __name__ == '__main__':
    main()
