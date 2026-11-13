# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Fable 5.2 (Claude Code, blender-modeling skill)
#  Generated : 2026-11-13
#  Prompt    : Alex: "set up an fps viewmodel ... rn we can use a pistol"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : The source of Assets/_Project/Art/Weapons/Models/Pistol.fbx: a plain, chunky
#              stand-in pistol for the first-person viewmodel. It is a script so every size is
#              a named number an artist can change and rebuild; the FBX is only its output.
#              Build: node <skill>/scripts/mdl.mjs build ArtSource/Weapons/pistol.py --out <dir>/Pistol.blend
# -----------------------------------------------------------------------------
"""Stand-in pistol. Muzzle points at -Y (the kit's "front"), Z is up.

The origin is the middle of the grip, where the palm closes round it, so the game can hang the
model on a hand bone with one offset. Sizes are guesses at a compact service pistol, made about
15% chunkier so it reads in the toon style; none are measured from a reference.
"""
from modelkit import *

P = params(
    SLIDE_LENGTH=0.190, SLIDE_WIDTH=0.030, SLIDE_HEIGHT=0.034,   # the top block that holds the barrel
    FRAME_LENGTH=0.150, FRAME_WIDTH=0.028, FRAME_HEIGHT=0.020,   # the block under the slide
    GRIP_LENGTH=0.048, GRIP_WIDTH=0.032, GRIP_HEIGHT=0.105,      # front-to-back, side-to-side, top-to-bottom
    GRIP_RAKE=14.0,                                              # degrees the grip leans back from upright
    GUARD_LENGTH=0.050, GUARD_HEIGHT=0.030, GUARD_THICKNESS=5 * MM,  # trigger guard loop, outer sizes
    TRIGGER_LENGTH=6 * MM, TRIGGER_HEIGHT=0.018,
    MUZZLE_RADIUS=6 * MM, MUZZLE_DEPTH=4 * MM,                   # the dark bore disc on the slide's front face
    SIGHT_LENGTH=0.010, SIGHT_WIDTH=5 * MM, SIGHT_HEIGHT=4 * MM, # front and rear sights
    BEVEL=2.5 * MM,                                              # rounding on every hard edge
)

# Heights are laid out from the grip's middle (the origin) upward.
grip_top = P.GRIP_HEIGHT / 2
frame_z = grip_top + P.FRAME_HEIGHT / 2
slide_z = grip_top + P.FRAME_HEIGHT + P.SLIDE_HEIGHT / 2
# The grip sits at the back of the gun: the slide overhangs it by a quarter of the grip's length.
slide_back = P.GRIP_LENGTH * 0.75
slide_y = slide_back - P.SLIDE_LENGTH / 2
frame_y = slide_back - P.FRAME_LENGTH / 2

slide = box(P.SLIDE_WIDTH, P.SLIDE_LENGTH, P.SLIDE_HEIGHT)
bevel(slide, P.BEVEL)
move(slide, 0, slide_y, slide_z)

frame = box(P.FRAME_WIDTH, P.FRAME_LENGTH, P.FRAME_HEIGHT)
bevel(frame, P.BEVEL)
move(frame, 0, frame_y, frame_z)

grip = box(P.GRIP_WIDTH, P.GRIP_LENGTH, P.GRIP_HEIGHT)
bevel(grip, P.BEVEL * 2)
# Raked about its top so it still meets the frame.
rotate(grip, P.GRIP_RAKE, axis='X', center=(0, 0, grip_top))

guard_y = -P.GRIP_LENGTH / 2 - P.GUARD_LENGTH / 2 + P.GUARD_THICKNESS
guard_z = grip_top - P.GUARD_HEIGHT / 2 + P.GUARD_THICKNESS / 2
guard = boolean(
    box(P.GUARD_THICKNESS * 1.6, P.GUARD_LENGTH, P.GUARD_HEIGHT),
    box(P.GUARD_THICKNESS * 3, P.GUARD_LENGTH - 2 * P.GUARD_THICKNESS, P.GUARD_HEIGHT - 2 * P.GUARD_THICKNESS))
move(guard, 0, guard_y, guard_z)

trigger = box(P.GUARD_THICKNESS, P.TRIGGER_LENGTH, P.TRIGGER_HEIGHT)
move(trigger, 0, guard_y + P.GUARD_LENGTH * 0.15, grip_top - P.TRIGGER_HEIGHT / 2)

front_y = slide_y - P.SLIDE_LENGTH / 2
sight_z = slide_z + P.SLIDE_HEIGHT / 2 + P.SIGHT_HEIGHT / 2
front_sight = move(box(P.SIGHT_WIDTH, P.SIGHT_LENGTH, P.SIGHT_HEIGHT), 0, front_y + P.SIGHT_LENGTH, sight_z)
rear_sight = move(box(P.SIGHT_WIDTH * 2.4, P.SIGHT_LENGTH, P.SIGHT_HEIGHT), 0, slide_back - P.SIGHT_LENGTH, sight_z)

body = join(slide, frame, guard, trigger, front_sight, rear_sight)
paint(body, 0)
paint(grip, 1)

muzzle = cylinder(P.MUZZLE_RADIUS, P.MUZZLE_DEPTH, segments=16, axis='Y')
move(muzzle, 0, front_y - P.MUZZLE_DEPTH / 2 + 1 * MM, slide_z)
paint(muzzle, 2)

pistol = part('Pistol', join(body, grip, muzzle),
              [material('Pistol_Metal', (0.16, 0.17, 0.20), metallic=0.6, roughness=0.45),
               material('Pistol_Grip', (0.30, 0.22, 0.16), roughness=0.8),
               material('Pistol_Bore', (0.03, 0.03, 0.03), roughness=0.9)],
              uv_scale=0.25)
