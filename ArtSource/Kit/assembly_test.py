# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-11
#  Prompt    : "Start the snap kit" (brief step 2: check the first pieces build two bays and a corner)
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Snaps built kit pieces together by the convention alone (grid positions and 90 degree
#              turns, no hand nudging), then fires rays along every seam to find gaps, and renders
#              the result from inside and outside. It exists so a change to any kit number is
#              re-proved the same way instead of being eyeballed in a scene.
# -----------------------------------------------------------------------------
"""Usage: blender -b --factory-startup -P ArtSource/Kit/assembly_test.py -- <kit build folder> <out folder>

Places, in Unity floor-local terms: a two-bay façade corner (2 m and 4 m slab rows, two window bays,
three columns, side walls, ceiling slabs above), a door wall with its leaf, a glass partition, and a
stairwell with two stacked flights, column and cage. Prints GAP lines for every ray that passes
through where a surface should be, and PASS/FAIL per check.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

FLOOR_H = 4.5      # kit FLOOR_H, metres
CLEAR_H = 4.3      # kit floor to soffit, metres
STEP = 0.02        # ray spacing along seams, metres. WHY: finer than any gap a player or a light leak would notice
STAIR_AT = (20.0, 0.0, 0.0)   # stairwell module pivot, far enough from the corner test not to interfere


def U(x, y, z):
    """Unity floor-local point -> Blender."""
    return Vector((-x, -z, y))


def place(build, piece, ux, uy, uz, yaw=0.0):
    """Append a built piece and put its pivot at a Unity position, turned yaw degrees (Unity, clockwise from above)."""
    path = os.path.join(build, f'{piece}.blend')
    with bpy.data.libraries.load(path) as (src, dst):
        dst.objects = list(src.objects)
    roots = []
    for ob in dst.objects:
        if ob is None:
            continue
        if ob.name.split('.')[0].endswith('_Collider'):
            bpy.data.objects.remove(ob)
            continue
        bpy.context.scene.collection.objects.link(ob)
        if ob.parent is None:
            roots.append(ob)
    m = Matrix.Translation(U(ux, uy, uz)) @ Matrix.Rotation(math.radians(-yaw), 4, 'Z')
    for r in roots:
        r.matrix_world = m @ r.matrix_world
    return roots


def build_scene(build):
    # ---- Façade corner: front infill row 2 m deep, then a 4 m row; two bays wide.
    for x in (0.0, 4.0):
        place(build, 'Slab_4x2', x, 0, 0)
        place(build, 'Slab_4x4', x, 0, -2)
        place(build, 'Slab_4x2', x, FLOOR_H, 0)       # the floor above: its soffit is this room's ceiling
        place(build, 'Slab_4x4', x, FLOOR_H, -2)
    place(build, 'Column_CornerL', 0, 0, 0)     # the building's corner
    place(build, 'Column_Facade', 4, 0, 0)
    place(build, 'Column_CornerR', 8, 0, 0)     # stands in for the far corner, to prove the right-hand piece
    place(build, 'Window_4m', 0, 0, 0)
    place(build, 'Window_4m', 4, 0, 0)
    # Left side wall runs toward -Z with its thickness inside (+X): pivot at the far end, turned -90.
    place(build, 'Wall_Solid_2m', 0, 0, -2, -90)
    place(build, 'Wall_Solid_4m', 0, 0, -6, -90)
    # ---- Interior: a door wall closing the 6 m depth, its leaf, and a glass partition beside it.
    place(build, 'Wall_Door_4m', 0, 0, -6)
    x0 = (4.0 - 1.2) * 0.5
    place(build, 'Door_Leaf', x0 + 0.01, 0, -6.1)
    place(build, 'Partition_Glass_4m', 4, 0, -6)
    # ---- Stairwell: two floors, so the flight's top meets the next floor's start.
    sx, sy, sz = STAIR_AT
    cx, cz = sx + 6.0, sz - 4.0
    for level in (0, 1):
        y = level * FLOOR_H
        place(build, 'Slab_Stairwell', sx, y, sz)
        place(build, 'Stair_Flight', cx, y, cz)
        place(build, 'Stair_Column', cx, y, cz)
        for k in range(12):
            a = 15.0 + 30.0 * k
            if a < 300.0:
                place(build, 'Stair_CagePanel', cx, y, cz, a)


def hit(origin_u, dir_u, max_d=5.0):
    """Ray in Unity terms; returns the Unity hit point or None."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    o = U(*origin_u)
    d = Vector((-dir_u[0], -dir_u[2], dir_u[1])).normalized()
    ok, loc, *_ = bpy.context.scene.ray_cast(depsgraph, o, d, distance=max_d)
    return Vector((-loc.x, loc.z, -loc.y)) if ok else None


def frange(a, b, step):
    n = int(round((b - a) / step))
    return [a + i * step for i in range(n + 1)]


def run_checks():
    results = []

    def check(name, samples, test):
        gaps = [s for s in samples if not test(s)]
        results.append((name, len(samples), gaps))

    # 1. Façade at sill height, 0-8 m, from the street: every ray stops in the wall line.
    check('facade sill solid', frange(0.0, 8.0, STEP),
          lambda x: (h := hit((x, 0.5, 1.0), (0, 0, -1))) is not None and -0.22 <= h.z <= 0.03)
    # 2. Side wall at three heights, 0 to -6 m, from outside the building.
    check('side wall solid', [(z, y) for z in frange(-6.0, 0.0, STEP) for y in (0.5, 2.0, 4.2)],
          lambda s: (h := hit((-1.0, s[1], s[0]), (1, 0, 0))) is not None and -0.03 <= h.x <= 0.21)
    # 3. The corner itself, along the diagonal, at three heights.
    check('corner closed', [0.5, 2.0, 4.2],
          lambda y: hit((-1.0, y, 1.0), (1, 0, -1), 3.0) is not None)
    # 4. Floor: straight down over both rows, including every seam.
    check('floor continuous', [(x, z) for x in frange(0.25, 7.75, 0.1) for z in frange(-5.75, -0.25, 0.1)],
          lambda s: (h := hit((s[0], 1.0, s[1]), (0, -1, 0))) is not None and abs(h.y) < 0.001)
    # 5. Ceiling: straight up, the soffit is at clear height everywhere.
    check('ceiling at clear height', [(x, z) for x in frange(0.25, 7.75, 0.1) for z in frange(-5.75, -0.25, 0.1)],
          lambda s: (h := hit((s[0], 1.0, s[1]), (0, 1, 0))) is not None and abs(h.y - CLEAR_H) < 0.001)
    # 6. Walls reach the soffit: a ray just under the ceiling along the wall line hits wall, not sky.
    check('wall meets soffit', frange(-5.9, -0.1, STEP),
          lambda z: hit((-1.0, CLEAR_H - 0.005, z), (1, 0, 0)) is not None)
    # 7. Doorway: rays through the opening pass; above the head they stop. Leaf is closed, so test beside it.
    check('door head closed above opening', frange(1.45, 2.55, STEP),
          lambda x: hit((x, 2.6, -5.0), (0, 0, -1), 2.0) is not None)
    # 8. Stairs: a ray down onto the treads all round the turn lands on the flight at the expected height.
    sx, sy, sz = STAIR_AT
    cx, cz = sx + 6.0, sz - 4.0

    def tread(a):
        r = math.radians(a)
        x, z = cx + math.sin(r) * 1.7, cz + math.cos(r) * 1.7
        h = hit((x, FLOOR_H * a / 360.0 + 1.0, z), (0, -1, 0), 2.0)
        return h is not None and abs(h.y - FLOOR_H * a / 360.0) < 0.05
    check('flight rises one floor per turn', frange(5.0, 355.0, 5.0), tread)
    # 9. Arrival: at the top of the turn the treads end flush with the next floor's slab.
    check('flight arrives flush with next floor', [0.0],
          lambda _: (h := hit((cx - 1.7 * math.sin(math.radians(1)), FLOOR_H + 0.5, cz + 1.7), (0, -1, 0), 1.0)) is not None
          and abs(h.y - FLOOR_H) < 0.05)

    for name, n, gaps in results:
        print(f'{"PASS" if not gaps else "FAIL"}  {name}: {n - len(gaps)}/{n} rays as expected')
        for g in gaps[:5]:
            print(f'GAP   {name} at {g}')
    return all(not g for _, _, g in results)


def render(out):
    scene = bpy.context.scene
    for engine in ('BLENDER_EEVEE', 'BLENDER_EEVEE_NEXT'):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.view_settings.view_transform = 'Standard'
    world = bpy.data.worlds.new('Sky')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.15, 0.35, 0.85, 1.0)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.6
    scene.world = world
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 4.0
    sun.rotation_euler = (math.radians(50), 0, math.radians(120))
    scene.collection.objects.link(sun)
    glass = bpy.data.materials.get('Glass_Clear')
    if glass and glass.use_nodes:
        glass.node_tree.nodes['Principled BSDF'].inputs['Alpha'].default_value = 0.15
        if hasattr(glass, 'surface_render_method'):
            glass.surface_render_method = 'BLENDED'
    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.angle_y = math.radians(60)
    scene.collection.objects.link(cam)
    scene.camera = cam
    views = {
        'corner_outside': ((-5.0, 3.0, 6.0), (3.0, 2.0, -2.0)),
        'corner_inside': ((7.0, 1.6, -5.0), (0.0, 1.6, 0.0)),
        'stair_inside': ((STAIR_AT[0] + 1.0, 1.6, -1.0), (STAIR_AT[0] + 6.0, 1.5, -4.0)),
    }
    for name, (eye, tgt) in views.items():
        e, t = U(*eye), U(*tgt)
        cam.location = e
        cam.rotation_euler = (t - e).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out, f'assembly_{name}.png')
        bpy.ops.render.render(write_still=True)
        print('wrote', scene.render.filepath)


def main():
    argv = sys.argv[sys.argv.index('--') + 1:]
    build, out = os.path.abspath(argv[0]), os.path.abspath(argv[1])
    os.makedirs(out, exist_ok=True)
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob)
    build_scene(build)
    ok = run_checks()
    render(out)
    print('ASSEMBLY', 'PASSED' if ok else 'FAILED')


# WHY the guard: interior_scene.py imports place() and U() from here. Blender runs a -P script as __main__.
if __name__ == '__main__':
    main()
