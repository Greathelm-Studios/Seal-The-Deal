# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-11
#  Prompt    : "the priority is working on the interior" -> "A whole interior scene"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Snaps the built kit pieces into one whole office floor and renders it at eye height,
#              so the interior can be judged as a space rather than piece by piece. It reads build/,
#              so every piece modelled later shows up here without touching this file. It is a review
#              scene, not a floor template: the programmer's template builder stays the authority.
#  Modified  : Claude Opus 5.5, 2026-11-12 — dresses the floor in the approved "White Trays + Cobalt Portal"
#              pieces: cobalt portal on the STORE front, open stair treads with the ramp hidden, the red
#              locked door, ceiling strip lights, and the ceiling/carpet tile textures.
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
# -----------------------------------------------------------------------------
"""Usage: blender -b --factory-startup -P ArtSource/Kit/interior_scene.py -- <kit build folder> <out folder> [--save]

Layout, in Unity floor-local metres (x along the façade from the left corner, -z into the building):
  * 28 x 18 m floor: a 2 m strip at the windows, three 4 m rows, a 4 m service spine (kit README grid).
  * Seven 4 m window bays; corner columns at both ends.
  * Stairwell module at x 8-20, z -6 to -14; stair centre (14, -10).
  * Two 4 x 4 m glass meeting rooms either side of the stair, each open on the stair side.
  * Spine rooms WC 8 m, STORE 12 m, UTIL 8 m, each with a door (README proposal 3).
  * A rival façade bay row 12 m across the street, with stand-ins in it.
--save also writes interior_scene.blend to the out folder for a look round in Blender.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from assembly_test import FLOOR_H, CLEAR_H, U, place  # noqa: E402  (reuse the kit's snap helper)

# ---------------------------------------------------------------- layout (Unity floor-local, metres)
BAYS = 7                 # façade bays: 28 m / 4 m (TOWER.md footprint width)
BAY = 4.0                # kit BAY
STRIP = 2.0              # front infill row depth (README proposal 1)
ROWS = 3                 # 4 m rows behind the strip: 2 + 3 x 4 = 14 m main floor
SPINE = 4.0              # service spine depth (TOWER.md)
DEPTH = STRIP + ROWS * BAY + SPINE          # 18 m
SPINE_Z = -(STRIP + ROWS * BAY)             # -14, the spine's front wall line
STAIRWELL_X, STAIRWELL_Z = 8.0, -6.0        # Slab_Stairwell pivot; its 12 x 8 module spans x 8-20, z -6 to -14
STAIR_C = (14.0, -10.0)                     # stair centre = pivot + (6, -4) (README)
WC_X1, UTIL_X0 = 8.0, 20.0                  # spine dividers: WC 0-8, STORE 8-20, UTIL 20-28
GAP = 12.0                                   # façade to rival façade (TOWER.md)
CAGE_STEP, CAGE_FIRST, CAGE_OPEN = 30.0, 15.0, 300.0   # cage panel spacing and the open arc (README)
PORTAL = {8.0: 'Wall_Solid_4m_Cobalt', 12.0: 'Wall_Door_4m_PortalHead', 16.0: 'Wall_Solid_4m_Cobalt'}
# WHY these three: the STORE front behind the stair (x 8-20) is the approved Cobalt Portal: cobalt legs, white field
# with the door, cobalt head band (kit README, "Office direction").
DOOR_ANGLE = 30.0        # locked door angle, degrees: GreyboxTowerSetup.LockedDoorAngle
DOOR_RADIUS = 1.7        # door centre radius, metres: midway between column (0.8) and tread edge (2.6), as the builder
DOOR_DROP = 0.02         # the builder sinks the door this far under the ramp line, metres
LIGHT_OFFSETS = (1.25, 2.75)   # strip light centres in a 4 m bay, along x and z, metres. WHY: each 1.5 m strip
                               # spans three whole 0.5 m tiles, and the pair sits symmetric about the bay centre
STAIR_CLEAR = 3.0        # no strip lights within this radius of the stair centre, metres (the slab hole is 2.65)
TEXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'textures')

# ---------------------------------------------------------------- look
EYE = 1.6                # first-person eye height, metres (Greybox review_views.py, TestBlast.asset)
FOV_V = 60.0             # vertical field of view, degrees (Unity camera default)
FIGURE_H = 1.8           # stand-in height, metres (Greybox README)
SUIT = (0.0056, 0.0116, 0.0395)   # Deep Navy #111B38 in linear RGB
GLASS_ALPHA = 0.05       # glass opacity. WHY: the Greybox README's recommended value for reading suits through two panes
INTERIOR_W_M2 = 10.0     # ceiling light, watts per square metre. WHY: Greybox README's recommended interior level
LIGHT_DROP = 0.05        # ceiling lights hang this far under the soffit, metres. WHY: keeps the area light off the slab face
BOUNCE = 0.35            # upward bounce light as a fraction of the ceiling light. WHY: stands in for light off the
                         # carpet, which Eevee doesn't bounce on its own; tuned by eye so the ceiling reads white
BOUNCE_Y = 0.5           # bounce panel height, metres: below eye level so it never shows in a view
SUN_W = 4.0            # sun strength, W/m² (Greybox review_views.py)
SKY = (0.10, 0.28, 0.80) # cobalt sky colour, linear RGB; reads as the bible's blue sky
SKY_STRENGTH = 0.8       # sky light strength; brighter than the assembly test so the street side is daylit
RES = (1600, 900)        # render size, pixels


def build_floor(build):
    """Floor, ceiling (the slab above), façade, outer walls, stair, spine and meeting rooms."""
    for level in (0.0, FLOOR_H):
        for i in range(BAYS):
            x = i * BAY
            place(build, 'Slab_4x2', x, level, 0)
            for r in range(ROWS):
                z = -STRIP - r * BAY
                if STAIRWELL_X <= x < STAIRWELL_X + 12.0 and z <= STAIRWELL_Z:
                    continue                  # inside the stairwell module
                place(build, 'Slab_4x4', x, level, z)
            place(build, 'Slab_4x4', x, level, SPINE_Z)
        place(build, 'Slab_Stairwell', STAIRWELL_X, level, STAIRWELL_Z)

    # Façade: corner columns at both ends, centred columns between.
    for i in range(BAYS):
        place(build, 'Window_4m', i * BAY, 0, 0)
        if i:
            place(build, 'Column_Facade', i * BAY, 0, 0)
    place(build, 'Column_CornerL', 0, 0, 0)
    place(build, 'Column_CornerR', BAYS * BAY, 0, 0)

    # Side walls: left runs toward +Z from its pivot (yaw -90), right toward -Z (yaw 90); thickness inside both.
    w = BAYS * BAY
    place(build, 'Wall_Solid_2m', 0, 0, -STRIP, -90)
    place(build, 'Wall_Solid_2m', w, 0, 0, 90)
    for k in range(ROWS + 1):
        z = -STRIP - k * BAY
        place(build, 'Wall_Solid_4m', 0, 0, z - BAY, -90)
        place(build, 'Wall_Solid_4m', w, 0, z, 90)
    # Back wall, turned 180 so its thickness is inside.
    for i in range(BAYS):
        place(build, 'Wall_Solid_4m', (i + 1) * BAY, 0, -DEPTH, 180)

    # Spine front wall: a door into each room, solid elsewhere.
    doors = {0.0, 12.0, 20.0}       # module x of each door wall: WC, STORE, UTIL
    for i in range(BAYS):
        x = i * BAY
        default = 'Wall_Door_4m' if x in doors else 'Wall_Solid_4m'
        place(build, PORTAL.get(x, default), x, 0, SPINE_Z)
        if x in doors:
            place(build, 'Door_Leaf', x + (BAY - 1.2) * 0.5 + 0.01, 0, SPINE_Z - 0.1)
    place(build, 'Wall_Solid_4m', WC_X1, 0, -DEPTH, -90)
    place(build, 'Wall_Solid_4m', UTIL_X0, 0, SPINE_Z, 90)

    # Stair: flight, column and cage, open where the flight from below arrives.
    cx, cz = STAIR_C
    for ob in place(build, 'Stair_Flight', cx, 0, cz):
        ob.hide_render = True             # WHY: the ramp is the walking collider; Stair_Steps is what you see
    place(build, 'Stair_Steps', cx, 0, cz)
    place(build, 'Stair_Column', cx, 0, cz)
    r = math.radians(DOOR_ANGLE)
    place(build, 'Stair_LockedDoor', cx + math.sin(r) * DOOR_RADIUS, FLOOR_H * DOOR_ANGLE / 360.0 - DOOR_DROP,
          cz + math.cos(r) * DOOR_RADIUS, DOOR_ANGLE - 90.0)
    a = CAGE_FIRST
    while a < CAGE_OPEN:
        place(build, 'Stair_CagePanel', cx, 0, cz, a)
        a += CAGE_STEP

    # Meeting rooms, 4 x 4 m, z -6 to -10, open 2 m on the side facing the stair.
    for x0, open_right in ((2.0, True), (22.0, False)):
        x1 = x0 + BAY
        place(build, 'Partition_Glass_4m', x0, 0, -6)               # front
        place(build, 'Partition_Glass_4m', x1, 0, -10, 180)         # back
        if open_right:
            place(build, 'Partition_Glass_4m', x0, 0, -10, -90)     # outer side, full
            place(build, 'Partition_Glass_2m', x1, 0, -8, 90)       # stair side, half: z -8 to -10
        else:
            place(build, 'Partition_Glass_4m', x1, 0, -6, 90)       # outer side, full
            place(build, 'Partition_Glass_2m', x0, 0, -10, -90)     # stair side, half: z -10 to -8


def ceiling_lights(build):
    """Kit strip lights on the soffit, four per 4 x 4 m bay of the main floor, clear of the stair opening."""
    for i in range(BAYS):
        for row in range(ROWS):
            for ox in LIGHT_OFFSETS:
                for oz in LIGHT_OFFSETS:
                    x, z = i * BAY + ox, -STRIP - row * BAY - oz
                    if math.hypot(x - STAIR_C[0], z - STAIR_C[1]) > STAIR_CLEAR:
                        place(build, 'Ceiling_LightStrip', x, CLEAR_H, z)


def tile_textures():
    """Puts the kit's tile textures on the carpet and ceiling for the review (Unity gets them from KitImport)."""
    for name in ('Floor_Carpet', 'Ceiling_Tile'):
        path = os.path.join(TEXTURES, f'{name}.png')
        if not os.path.exists(path):
            continue
        image = bpy.data.images.load(path, check_existing=True)
        for mat in copies(name):
            nodes = mat.node_tree.nodes
            tex = nodes.new('ShaderNodeTexImage')
            tex.image = image
            mat.node_tree.links.new(tex.outputs['Color'], nodes['Principled BSDF'].inputs['Base Color'])


def copies(name):
    """Every copy of a kit material. WHY: each appended piece brings its own copy (Glass_Clear.003 ...), so a
    setting made on the first one only would leave the rest as built (which left most glass opaque before)."""
    return [m for m in bpy.data.materials if m.name.split('.')[0] == name and m.node_tree]


def build_rival(build):
    """One floor of the rival tower's façade 12 m across, a bay deep, with white massing above and below."""
    z = GAP
    for i in range(BAYS):
        x = (i + 1) * BAY
        for level in (0.0, FLOOR_H):
            place(build, 'Slab_4x4', x, level, z, 180)
        place(build, 'Window_4m', x, 0, z, 180)
        place(build, 'Wall_Solid_4m', i * BAY, 0, z + BAY)
        if i:
            place(build, 'Column_Facade', i * BAY, 0, z, 180)
    w = BAYS * BAY
    white = bpy.data.materials.get('Wall_PaperWhite')
    for name, y0, y1, z0, z1 in (('OurTower_Below', -40.0, -0.2, -DEPTH, 0.0),
                                 ('OurTower_Above', FLOOR_H, FLOOR_H + 40.0, -DEPTH, 0.0),
                                 ('Rival_Below', -40.0, -0.2, z, z + DEPTH),
                                 ('Rival_Above', FLOOR_H, FLOOR_H + 40.0, z, z + DEPTH)):
        # WHY the 0.2 m inset on 'Above': the real slab above sits there and must stay visible as the ceiling.
        y0 = y0 + 0.01 if name.endswith('Above') else y0
        box(name, (0.0, w), (y0, y1), (z0, z1), white)
    box('Street', (-60.0, w + 60.0), (-40.2, -40.0), (-60.0, z + 60.0), white)


def box(name, xr, yr, zr, mat):
    """An axis-aligned box from Unity extents, for massing and stand-ins."""
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    lo = U(xr[0], yr[0], zr[0])
    hi = U(xr[1], yr[1], zr[1])
    c = (lo + hi) * 0.5
    s = Vector((abs(hi.x - lo.x), abs(hi.y - lo.y), abs(hi.z - lo.z)))
    for v in bm.verts:
        v.co = Vector((v.co.x * s.x, v.co.y * s.y, v.co.z * s.z)) + c
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if mat:
        me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def figures():
    """Dark-suited stand-ins, 1.8 m: body box plus head, at the places a player would stand."""
    suit = bpy.data.materials.new('Standin_Suit')
    suit.use_nodes = True
    suit.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*SUIT, 1.0)
    spots = [(6.0, -3.0), (13.0, -5.5), (4.0, -8.0), (16.5, -12.5), (24.0, -4.0),   # our floor
             (10.0, GAP + 1.5), (18.0, GAP + 2.0)]                                       # rival floor
    for k, (x, z) in enumerate(spots):
        box(f'Standin_{k}_Body', (x - 0.25, x + 0.25), (0.0, FIGURE_H - 0.25), (z - 0.15, z + 0.15), suit)
        box(f'Standin_{k}_Head', (x - 0.11, x + 0.11), (FIGURE_H - 0.25, FIGURE_H), (z - 0.11, z + 0.11), suit)


def lights():
    """One ceiling panel light per 4 x 4 m of floor, totalling INTERIOR_W_M2, plus sun along the street."""
    scene = bpy.context.scene
    y = CLEAR_H - LIGHT_DROP
    xs = [i * BAY + BAY * 0.5 for i in range(BAYS)]
    zs = [-STRIP * 0.5] + [-STRIP - r * BAY - BAY * 0.5 for r in range(ROWS)] + [SPINE_Z - SPINE * 0.5]
    depths = [STRIP] + [BAY] * ROWS + [SPINE]
    for x in xs:
        for z, d in zip(zs, depths):
            data = bpy.data.lights.new(f'Ceiling_{x:g}_{z:g}', 'AREA')
            data.shape = 'RECTANGLE'
            data.size, data.size_y = BAY * 0.8, d * 0.8
            data.energy = INTERIOR_W_M2 * BAY * d
            # WHY no shadows: 35 shadowed area lights overflow Eevee's shadow buffer; they're soft fill, the sun carves.
            data.use_shadow = False
            ob = bpy.data.objects.new(data.name, data)
            ob.location = U(x, y, z)          # area lights point down -Z in Blender by default: straight down
            scene.collection.objects.link(ob)
            # Bounce stand-in: the same panel turned to face up, weaker, so the white ceiling reads white.
            up = bpy.data.lights.new(f'Bounce_{x:g}_{z:g}', 'AREA')
            up.shape, up.size, up.size_y = 'RECTANGLE', data.size, data.size_y
            up.energy = data.energy * BOUNCE
            up.use_shadow = False
            ob = bpy.data.objects.new(up.name, up)
            ob.location = U(x, BOUNCE_Y, z)
            ob.rotation_euler = (math.pi, 0.0, 0.0)
            scene.collection.objects.link(ob)
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = SUN_W
    # WHY along the street (Blender X): with the sun across it, one tower's front is always in shadow (Greybox README).
    sun.rotation_euler = (math.radians(40.0), 0.0, math.radians(90.0))
    scene.collection.objects.link(sun)


def look():
    scene = bpy.context.scene
    for engine in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    scene.render.resolution_x, scene.render.resolution_y = RES
    # WHY raytracing: without it Eevee lights a covered ceiling with the full blue sky, so it reads as sky.
    ee = scene.eevee
    for attr, val in (('use_raytracing', True), ('use_fast_gi', True), ('use_gtao', True)):
        if hasattr(ee, attr):
            setattr(ee, attr, val)
    scene.view_settings.view_transform = 'AgX'
    world = bpy.data.worlds.new('Sky')
    world.use_nodes = True
    bg = world.node_tree.nodes['Background']
    bg.inputs['Color'].default_value = (*SKY, 1.0)
    bg.inputs['Strength'].default_value = SKY_STRENGTH
    scene.world = world
    for glass in copies('Glass_Clear'):
        glass.node_tree.nodes['Principled BSDF'].inputs['Alpha'].default_value = GLASS_ALPHA
        if hasattr(glass, 'surface_render_method'):
            glass.surface_render_method = 'BLENDED'


VIEWS = {
    # name: (eye, target) in Unity floor-local metres
    'A_entry_to_stair': ((1.5, EYE, -12.5), (14.0, 1.4, -6.0)),
    'B_stair_to_windows': ((14.0, EYE, -5.0), (14.0, 1.6, 6.0)),
    'C_corner_across_floor': ((1.0, EYE, -1.0), (20.0, 1.3, -12.0)),
    'D_meeting_room': ((3.0, EYE, -9.5), (12.0, 1.5, -6.0)),
    'E_spine_door_to_facade': ((14.0, EYE, -13.5), (8.0, 1.6, 0.0)),
    # The approved concepts' two other shots: the stair close up, and our floor from the rival's window.
    'G_stair_close': ((9.0, EYE, -3.5), (14.0, 2.0, -10.0)),
    'H_rival_view': ((14.0, EYE, GAP + 1.5), (14.0, 1.8, -8.0)),   # inside the rival's room, through both panes
}


def render(out):
    scene = bpy.context.scene
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.angle_y = math.radians(FOV_V)
    scene.collection.objects.link(cam)
    scene.camera = cam

    def shoot(name, eye, tgt):
        e, t = U(*eye), U(*tgt)
        cam.location = e
        cam.rotation_euler = (t - e).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out, f'interior_{name}.png')
        bpy.ops.render.render(write_still=True)
        print('wrote', scene.render.filepath)

    for name, (eye, tgt) in VIEWS.items():
        shoot(name, eye, tgt)

    # Cutaway from above the back-left, with the slab above and the massing hidden, like the reference plates.
    hide = [o for o in scene.objects if o.type == 'MESH' and (o.matrix_world.translation.z > FLOOR_H - 0.5
                                                               or o.name.startswith(('OurTower_Above', 'Rival_Above')))]
    for o in hide:
        o.hide_render = True
    cam.data.angle_y = math.radians(40.0)
    shoot('F_cutaway', (-6.0, 26.0, -30.0), (14.0, 0.0, -7.0))


def main():
    argv = sys.argv[sys.argv.index('--') + 1:]
    build, out = os.path.abspath(argv[0]), os.path.abspath(argv[1])
    os.makedirs(out, exist_ok=True)
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob)
    build_floor(build)
    ceiling_lights(build)
    build_rival(build)
    tile_textures()
    figures()
    lights()
    look()
    if '--save' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, 'interior_scene.blend'))
    render(out)


if __name__ == '__main__':
    main()
