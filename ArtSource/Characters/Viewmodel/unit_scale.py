# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Fable 5.2 (Claude Code)
#  Generated : 2026-11-13
#  Prompt    : Alex: "set up an fps viewmodel with the characters model we are using"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Run between the character-animation skill's `retarget` and `export` steps.
#              After an FBX round trip Blender holds this character's armature at object
#              scale 0.01; exported like that, every bone imports into Unity 100 times out.
#              This applies the scale and rescales the bone location keys to match. Same
#              recipe the body clips (STDMC_Moves.fbx) were made with; see README.md here.
# -----------------------------------------------------------------------------
#
# Usage:
#   blender -b <work.blend> --python unit_scale.py -- <out.blend> <skill scripts dir>
import os
import sys

import bpy

args = sys.argv[sys.argv.index("--") + 1:]
out_path, skill_scripts = args[0], args[1]
# WHY a path argument: action_fcurves hides the Blender 4.4+/5.x layered-action API
# difference, and it lives in the skill folder, which is not at a fixed place per machine.
sys.path.insert(0, os.path.abspath(skill_scripts))
from ca_common import action_fcurves  # noqa: E402

armature = next(o for o in bpy.data.objects if o.type == "ARMATURE")
scale = armature.scale[0]

rescaled = 0
for action in bpy.data.actions:
    for curve in action_fcurves(action):
        if curve.data_path.startswith("pose.bones") and curve.data_path.endswith(".location"):
            for key in curve.keyframe_points:
                key.co.y *= scale
                key.handle_left.y *= scale
                key.handle_right.y *= scale
            curve.update()
            rescaled += 1

for obj in bpy.data.objects:
    obj.select_set(obj.type in {"ARMATURE", "MESH"})
bpy.context.view_layer.objects.active = armature
bpy.ops.object.mode_set(mode="OBJECT")
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

print(f"unit_scale: armature scale was {scale}, rescaled {rescaled} location curves, "
      f"scale now {tuple(armature.scale)}")
bpy.ops.wm.save_as_mainfile(filepath=out_path)
