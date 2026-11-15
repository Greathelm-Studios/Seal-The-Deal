# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: Aero office props on one Painter trim sheet
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Review only: a textured EEVEE three-quarter render of a prop wired by preview_trim.py, because the
#              skill's contact sheet shows flat material colours and can't show the trim sheet on the model.
#  Run       : blender -b <preview>.blend --factory-startup -P render_textured.py -- <out.png> [yaw_degrees]
# -----------------------------------------------------------------------------
import math
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
out = args[0]
yaw = math.radians(float(args[1]) if len(args) > 1 else 35.0)
sc = bpy.context.scene
obs = [o for o in sc.objects if o.type == 'MESH' and not o.name.endswith('_Collider') and '_LOD1' not in o.name]
for o in sc.objects:
    if o.type == 'MESH' and o not in obs:
        o.hide_render = True
pts = [o.matrix_world @ Vector(c) for o in obs for c in o.bound_box]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
c, r = (lo + hi) / 2, (hi - lo).length / 2
cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
sc.collection.objects.link(cam)
cam.data.lens = 60
d = r / math.tan(cam.data.angle / 2) * 1.15
# Blender front is -Y (Unity +Z); look from the front-right, a little above.
cam.location = c + Vector((math.sin(yaw) * d, -math.cos(yaw) * d, d * 0.45))
cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
sc.camera = cam
sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
sc.collection.objects.link(sun)
w = sc.world or bpy.data.worlds.new('W')
sc.world = w
w.use_nodes = True
w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.62, 0.74, 0.82, 1)   # pale sky fill
w.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.0
sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
sc.render.resolution_x, sc.render.resolution_y = 960, 720
sc.render.film_transparent = False
sc.view_settings.view_transform = 'Standard'
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
print('RENDERED', out)
