"""Solid-wall concept studies, in metres. Disposable Blender render geometry.

Scene axes match the R3 presentation rig, X along run, Y through wall, Z up.
Read-only reuse of Window_Bay_R3/render_concepts.py, with all writes redirected
here before its top-level code executes. No kit import, asset export or tests.
"""
import argparse
import json
import math
import os
from pathlib import Path
import sys
import types

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parent
DIRS = ["01_Floating_Field", "02_Framed_Trays"]
parser = argparse.ArgumentParser()
parser.add_argument("--direction", type=int, default=-1)
parser.add_argument("--shots", default="all")
parser.add_argument("--preview", action="store_true")
parser.add_argument("--force", action="store_true")
OPT = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])

rig_source = ROOT.parent / "Window_Bay_R3" / "render_concepts.py"
rig_text = rig_source.read_text(encoding="utf-8")
rig_text = rig_text.replace(
    "ROOT = os.path.dirname(os.path.abspath(__file__))",
    "ROOT = " + repr(str(ROOT)),
)
R = types.ModuleType("window_r3_read_only_rig")
R.__file__ = str(rig_source)
saved_arguments = sys.argv
sys.argv = [str(rig_source)]
exec(compile(rig_text, str(rig_source), "exec"), R.__dict__)
sys.argv = saved_arguments
R.OPT.preview = OPT.preview
META = {}
meta_path = ROOT / "render_metadata.json"
if meta_path.exists():
    META.update(json.loads(meta_path.read_text(encoding="utf-8")))


def setup(studio=False):
    R.setup(studio)
    s = bpy.context.scene
    s.render.resolution_x = 1800
    s.render.resolution_y = 1200
    s.cycles.samples = 24 if OPT.preview else 80
    s.view_settings.look = "AgX - Medium High Contrast"
    s.view_settings.exposure = 0
    # Reuse the R3 sky, city, carpet, ceiling and strip-light palette.
    # The sun enters through the open facade at room negative Y.
    sun = bpy.data.objects.get("Hard street sun / raking")
    sun.location = (-7, -12, 11)
    sun.rotation_euler = (Vector((7, 0, 1.4)) - sun.location).to_track_quat("-Z", "Y").to_euler()
    sun.data.energy = 3.1 if studio else 3.6
    sun.data.angle = math.radians(1.1)


def profile_block(name, x0, x1, z0, z1, depth0, depth1, material):
    """Opaque symmetric section. A positive depth is carved into each face."""
    return R.yz_prism(
        name,
        [(-0.2 + depth0, z0), (-depth0, z0),
         (-depth1, z1), (-0.2 + depth1, z1)],
        x0, x1, material,
    )


def floating_wall(width):
    # Both ends remain complete, flat 0.20 x 4.30 rectangles.
    land = 0.012
    zones = [(0, 0.02, 0, R.WHITE),
             (0.02, 0.20, 0.065, R.NAVY),
             (0.20, 4.08, 0, R.WHITE),
             (4.08, 4.28, 0.065, R.NAVY),
             (4.28, 4.30, 0, R.WHITE)]
    for low, high, depth, material in zones:
        profile_block("Floating field opaque section", land, width - land,
                      low, high, depth, depth, material)
        for a, b in [(0, land), (width - land, width)]:
            profile_block("Full-depth end snap land", a, b, low, high, 0, 0, material)


def framed_wall(width):
    # A tray-like white perimeter contains one inset white field. The navy
    # trough is 100 mm wide at the sides and 120 mm at the top and bottom.
    frame = 0.20
    R.box("White skirting shelf", 0, width, -0.2, 0, 0, 0.28, R.WHITE)
    R.box("White crown under slab", 0, width, -0.2, 0, 4.02, 4.30, R.WHITE)
    for a, b in [(0, frame), (width - frame, width)]:
        R.box("White full-depth perimeter pier", a, b, -0.2, 0, 0.28, 4.02, R.WHITE)
    R.box("Navy recessed tray backing 70mm opaque", frame, width - frame,
          -0.135, -0.065, 0.28, 4.02, R.NAVY)
    R.box("Paper White inset field", 0.30, width - 0.30,
          -0.16, -0.04, 0.40, 3.90, R.WHITE)


def wall(direction, width, origin=(0, 0, 0), rotation=0):
    before = set(bpy.context.scene.objects)
    if direction == 0:
        floating_wall(width)
    else:
        framed_wall(width)
    transform = Matrix.Translation(Vector(origin)) @ Matrix.Rotation(rotation, 4, "Z")
    for obj in set(bpy.context.scene.objects) - before:
        obj.matrix_world = transform @ obj.matrix_world
        obj["concept_wall"] = True
        obj["nominal_run_m"] = width
        obj["collider_requirement"] = "unchanged original box"


def figure(x, y, z=0):
    R.figure(x, y, z)
    R.mesh("Room-facing shirt V", [(x - 0.055, y - 0.151, z + 1.455),
                                  (x + 0.055, y - 0.151, z + 1.455),
                                  (x, y - 0.151, z + 1.27)], [(0, 1, 2)], R.SHIRT)


def room(direction):
    # Ten metres of solid wall, then a perpendicular 4 m return.
    # Return occupies X 9.8..10 and Y -4..0. The original volumes overlap
    # over a 0.20 x 0.20 square, with no corner-specific wall geometry.
    for x, width in [(0, 4), (4, 4), (8, 2)]:
        wall(direction, width, (x, 0, 0))
    wall(direction, 4, (10, 0, 0), -math.pi / 2)
    R.box("Context floor slab", -2, 10, -8, 0, -0.2, 0, R.WHITE)
    R.box("Context grey-blue carpet", -2, 9.8, -8, -0.2, 0, 0.006, R.CARPET)
    for x in range(-1, 10):
        R.box("Context 1m carpet seam", x - 0.002, x + 0.002, -8, -0.2, 0.006, 0.007, R.SEAM)
    for y in range(-7, 0):
        R.box("Context 1m carpet seam", -2, 9.8, y - 0.002, y + 0.002, 0.006, 0.007, R.SEAM)
    R.box("Context soffit slab", -2, 10, -8, 0, 4.3, 4.5, R.WHITE)
    for x in range(-2, 11):
        R.box("Context ceiling grid", x - 0.006, x + 0.006, -8, 0, 4.293, 4.3, R.SILVER)
    for y in range(-8, 1):
        R.box("Context ceiling grid", -2, 10, y - 0.006, y + 0.006, 4.293, 4.3, R.SILVER)
    for x in [1, 5, 9]:
        for y in [-2.5, -5.5]:
            R.box("Context ceiling strip", x - 0.45, x + 0.45, y - 0.035, y + 0.035,
                  4.27, 4.29, R.LIGHT)
    # Approved White Trays windows form the opposite side of the staged room.
    for x in [-2, 2, 6]:
        old = set(bpy.context.scene.objects)
        R.bay(0, 4, 0, 0, "B")
        R.column(0, 0, 0)
        for obj in set(bpy.context.scene.objects) - old:
            obj.matrix_world = Matrix.Translation(Vector((x + 4, -8, 0))) @ Matrix.Rotation(math.pi, 4, "Z") @ obj.matrix_world
    old = set(bpy.context.scene.objects)
    R.bay(0, 4, 0, 0, "B")
    R.column(0, 0, 0)
    R.column(4, 0, 0)
    for obj in set(bpy.context.scene.objects) - old:
        obj.matrix_world = Matrix.Translation(Vector((10, -4, 0))) @ Matrix.Rotation(-math.pi / 2, 4, "Z") @ obj.matrix_world
    R.city()
    R.area("Soft daylight wall wash", (3, -4.5, 3.5), (6, 0, 2.5), 350, 5,
           (0.87, 0.93, 1))


def dimension(a, b, label, offset=40):
    return (a, b, label, offset)


def shot(direction, name, dims=(), description=""):
    folder = ROOT / DIRS[direction]
    folder.mkdir(exist_ok=True)
    path = folder / (name + ".png")
    s = bpy.context.scene
    bpy.context.view_layer.update()
    s.render.filepath = str(path)
    if OPT.force or not path.exists():
        bpy.ops.render.render(write_still=True)
    width = round(s.render.resolution_x * s.render.resolution_percentage / 100)
    height = round(s.render.resolution_y * s.render.resolution_percentage / 100)
    annotations = []
    for a, b, label, offset in dims:
        points = [world_to_camera_view(s, s.camera, Vector(p)) for p in (a, b)]
        annotations.append(dict(a=[points[0].x * width, (1 - points[0].y) * height],
                                b=[points[1].x * width, (1 - points[1].y) * height],
                                label=label, offset=offset))
    wall_objects = [o for o in s.objects if o.get("concept_wall")]
    bounds = []
    if wall_objects:
        corners = [o.matrix_world @ Vector(c) for o in wall_objects for c in o.bound_box]
        bounds = [[min(p[i] for p in corners), max(p[i] for p in corners)] for i in range(3)]
    META[str(path.relative_to(ROOT)).replace("\\", "/")] = dict(
        dimensions=annotations, camera=list(s.camera.location),
        projection=s.camera.data.type, resolution=[width, height],
        description=description, wall_bounds_xyz=bounds,
    )
    meta_path.write_text(json.dumps(META, indent=2), encoding="utf-8")
    print("WALL_FINISHED", DIRS[direction], name, flush=True)


def wanted(name):
    return OPT.shots == "all" or name in OPT.shots.split(",")


def execute():
    for d in range(2) if OPT.direction < 0 else [OPT.direction]:
        if wanted("room"):
            setup()
            room(d)
            figure(6.0, -0.70)
            R.camera((0.5, -6.4, 1.7), (4.3, -0.1, 2.02), lens=20)
            shot(d, "room", description="Eye 1.70m. 4+4+2m run and overlapping 4m return. No doorway.")
        if wanted("close_skirting"):
            setup()
            room(d)
            figure(6.0, -0.70)
            R.camera((6.7, -2.2, 0.85), (8.3, -0.12, 0.38), lens=43)
            shot(d, "close_skirting", description="Raking three-quarter at skirting, 2.67m to target.")
        if wanted("close_top"):
            setup()
            room(d)
            R.camera((6.9, -2.1, 2.7), (8.3, -0.1, 4.05), lens=43)
            shot(d, "close_top", description="Raking three-quarter at head band and actual slab soffit.")
        if wanted("corner"):
            setup()
            room(d)
            figure(8.6, -0.7)
            R.camera((5.5, -4.6, 1.7), (9.65, -0.05, 2.08), lens=25)
            shot(d, "corner", description="Unmodified 2m wall meets 4m wall with 0.20m overlap.")
        if wanted("front"):
            setup(True)
            wall(d, 4)
            wall(d, 2, (4.6, 0, 0))
            figure(7.35, -0.3)
            R.pivot(0, -0.3)
            R.pivot(4.6, -0.3)
            R.rod("Floor level datum", (-0.4, -0.25, 0), (8, -0.25, 0), 0.008, R.SILVER)
            R.camera((3.7, -16, 2.0), (3.7, 0, 2.0), ortho=10)
            shot(d, "front", [dimension((0, -0.25, 0), (4, -0.25, 0), "4.00 m", 48),
                              dimension((4.6, -0.25, 0), (6.6, -0.25, 0), "2.00 m", 48),
                              dimension((-0.1, -0.25, 0), (-0.1, -0.25, 4.3), "4.30 m", -64)],
                 "Orthographic room front. Both real module lengths, two original pivot crosses, floor at zero.")
        if wanted("side"):
            setup(True)
            wall(d, 4)
            figure(4, -1.35)
            R.pivot(4, 0)
            R.rod("Floor level datum", (4, -2, 0), (4, 0.8, 0), 0.008, R.SILVER)
            R.camera((16, -0.5, 2.05), (4, -0.5, 2.05), ortho=7.1)
            shot(d, "side", [dimension((4, -0.2, 0.1), (4, 0, 0.1), "0.20 m", 70),
                             dimension((4, 0.1, 0), (4, 0.1, 4.3), "4.30 m", 80)],
                 "True orthographic +X end. Original full rectangular snap face.")
        if wanted("section"):
            setup(True)
            wall(d, 4)
            # Actual centre-of-module slice, preserving the Y/Z profiles.
            for obj in list(bpy.context.scene.objects):
                if not obj.get("concept_wall"):
                    continue
                xs = [v.co.x for v in obj.data.vertices]
                if max(xs) < 2 or min(xs) > 2:
                    bpy.data.objects.remove(obj, do_unlink=True)
                    continue
                for v in obj.data.vertices:
                    v.co.x = 2.02 if v.co.x > 2 else 1.98
                obj.data.update()
                R.face_finish(obj, R.CUT, axis=0)
            R.box("Section floor slab", 1.98, 2.02, -1, 0.5, -0.2, 0, R.CUT)
            R.box("Section soffit slab", 1.98, 2.02, -1, 0.5, 4.3, 4.5, R.CUT)
            figure(2, -1.6)
            R.pivot(2, 0)
            R.camera((15, -0.4, 2.08), (2, -0.4, 2.08), ortho=7.2)
            shot(d, "section", [dimension((2, -0.2, 2.2), (2, 0, 2.2), "0.20 m envelope", 86),
                                dimension((2, 0.35, 0), (2, 0.35, 4.3), "4.30 m", 76),
                                dimension((2, -1, 4.3), (2, -1, 4.5), "0.20 m slab", -90)],
                 "Orthographic centre section. Cut faces blue. Both room-face recesses shown.")
        if wanted("three_quarter"):
            setup(True)
            wall(d, 4)
            figure(4.7, -0.3)
            R.pivot(0, -0.22)
            R.box("Context floor under module", -0.15, 4.15, -1.4, 0.3, -0.2, 0, R.WHITE)
            R.box("Context slab above module", -0.15, 4.15, -1.4, 0.3, 4.3, 4.5, R.WHITE)
            R.camera((8.5, -9, 6.5), (2, -0.1, 2.05), ortho=8.1)
            shot(d, "three_quarter", description="Shaded room-side axonometric, navy suit and slab context.")
        if wanted("profile_detail"):
            setup(True)
            wall(d, 4)
            # Remove the snap land to expose the mid-run profile in a close view.
            for obj in list(bpy.context.scene.objects):
                if obj.get("concept_wall"):
                    xs = [v.co.x for v in obj.data.vertices]
                    if max(xs) < 1.98 or min(xs) > 2.02:
                        bpy.data.objects.remove(obj, do_unlink=True)
                    else:
                        for v in obj.data.vertices:
                            v.co.x = 2.02 if v.co.x > 2 else 1.98
                        obj.data.update()
                        R.face_finish(obj, R.CUT, axis=0)
            R.camera((6, -0.35, 0.20), (2, -0.1, 0.20), ortho=0.66)
            shot(d, "profile_detail", [dimension((2, -0.2, 0.05), (2, -0.135, 0.05), "65 mm recess", 75)],
                 "Enlarged centre section of foot. No collider shaping is implied.")


if __name__ == "__main__":
    execute()
