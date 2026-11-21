"""Office snap kit for Close the Deal.

Build one piece:
  node <skill>/scripts/mdl.mjs build ArtSource/Kit/kit_pieces.py --out ArtSource/Kit/build/<PIECE>.blend --set PIECE=<PIECE>
Build all: ArtSource/Kit/build_all.sh

Snap convention (Unity terms, metres; README.md has the full table):
  * Pivot = the piece's front-left snap corner, at the top of the floor slab (floor level).
  * A run goes along +X from the pivot; the piece's thickness or depth goes inward, toward -Z.
    For the façade, -Z is into the building, as in the repo's floor templates.
  * Grid: 4 m bays, 2 m infills, 4.5 m floor to floor, 0.2 m slab hanging below floor level,
    4.3 m clear from floor to slab soffit.
  * Stair pieces pivot at the stair centre, at floor level, 0 degrees toward +Z, clockwise.
Pieces are described with u(), which takes Unity extents and returns Blender geometry (Unity +X is
Blender -X, Unity +Z is Blender -Y), so the FBX export imports them with no rotation.
"""
import math

import bmesh
from modelkit import *

P = params(
    PIECE='Slab_4x4',      # which piece to build; see PIECES at the bottom
    # ---- Grid: DefaultTower.asset and the brief
    BAY=4.0,               # structural bay, metres
    INFILL=2.0,            # half bay, metres
    FLOOR_H=4.5,           # floor to floor, metres (DefaultTower.asset FloorHeight)
    SLAB=0.2,              # slab thickness, hangs below floor level, metres (GreyboxTowerSetup)
    WALL=0.2,              # wall thickness, metres (GreyboxTowerSetup)
    # ---- Façade: GreyboxTowerSetup (sill 0.6 m, pillar 0.4 m wide)
    SILL=0.6,              # sill height, metres. WHY 0.6: Alex, 2026-11-12, wanted taller glass and less wall; was 1.0.
                           # Still a low ledge and some cover; must match GreyboxTowerSetup.SillHeight
    COLUMN_W=0.4,          # façade column width, metres
    COLUMN_PROUD=0.02,     # column stands this far proud of both wall faces, metres.
                           # WHY: hides the sill's end and avoids two coplanar faces flickering
    RAIL=0.05,             # aluminium rail height at sill and head, metres
    RAIL_D=0.1,            # rail depth, metres
    GLASS_T=0.02,          # pane thickness, metres
    # ---- Façade detail: the approved "White Trays" concept (concepts/Window_Bay_R3/01_White_Trays/notes.md)
    TRAY_RECESS_H=0.16,    # height of the dark pocket cut into the street-top edge of the sill, metres
    TRAY_RECESS_D=0.12,    # its depth from the street face, metres; leaves 0.08 m of solid sill behind
    PIER_SETBACK=0.16,     # column street face steps back this far (from +0.02 to -0.14), metres
    PIER_LAND=0.02,        # full-depth strip kept at each side of a column, so the pane meets solid wall, metres
    PIER_TOP=0.14,         # full column kept below the soffit, metres (the recess stops at 4.16 m)
    RAIL_FINISH='navy',    # 'navy' (Trim_Navy, the concept's pick) or 'aluminium' (Frame_Aluminium)
    # ---- Doors: NOT in the repo (spine doorways are open slots). Proposed; see README.
    DOOR_W=1.2,            # clear opening width, metres (GreyboxTowerSetup DoorWidth)
    DOOR_H=2.4,            # clear opening height, metres. Proposed; a 1.8 m player clears it by 0.6 m
    DOOR_GAP=0.01,         # leaf clearance to frame and floor, metres
    DOOR_T=0.04,           # leaf thickness, metres
    # ---- Glass partition: GreyboxTowerSetup.GlassRoom (0.1 m thick, 2.5 m high)
    PART_T=0.1, PART_H=2.5,
    LOUVRE_SLAT=0.12,      # louvre partition: frosted glass slat height, metres (Alex's "DIVISION" reference, 2026-11-15)
    LOUVRE_PITCH=0.2,      # slat spacing, metres: 80 mm see-through gaps between slats, so players still read through
    LOUVRE_T=0.012,        # slat thickness, metres
    SPHERE_R=0.45,         # court sphere pendant radius, metres (Alex's atrium reference, 2026-11-15)
    SPHERE_DROP=3.0,       # sphere pendant: cable length from the soffit to the sphere's top, metres
    SPHERE_CABLE_R=0.006,  # sphere pendant cable radius, metres
    PART_RAIL=0.08,        # base and top rail height, metres
    # ---- Spiral stairs: GreyboxTowerSetup
    STAIR_R=2.6, COLUMN_R=0.8, FLIGHT_T=0.25, FLIGHT_SEGMENTS=48,
    CAGE_GAP=0.15, CAGE_PANELS=12, CAGE_T=0.05,
    HOLE_GAP=0.05,         # slab hole clears the treads by this much, metres
    STAIRWELL_W=12.0,      # stairwell slab module, 3 bays wide, metres
    STAIRWELL_D=8.0,       # 2 bays deep, metres. The stair centre sits in its middle
    # ---- Office direction "White Trays + Cobalt Portal" (concepts/Office/DECISION.md)
    SKIRT_H=0.08,          # shadow-gap skirting height on both wall faces, metres. WHY: reads as the wall floating
                           # on the carpet (the concepts' clean base) without a proud moulding
    SKIRT_D=0.015,         # its depth into the wall, metres; the same 15 mm the brief allows for proud detail
    TOP_H=0.04,            # navy reveal where a wall meets the soffit, metres: the slab line carried along the wall
    TOP_D=0.015,           # its depth, metres
    ARCH_W=0.03,           # shadow reveal round a doorway, on both faces, metres. WHY: frames the opening without
                           # narrowing the 1.2 x 2.4 m clear opening the leaf fills
    ARCH_D=0.015,          # its depth, metres
    PORTAL_Y=3.3,          # cobalt above this height on the portal-head wall variants, metres. WHY: a 1 m head band
                           # like the concept's portal, clear of the 2.4 m door head below it
    PORTAL_GAP=0.015,      # navy shadow line between the white field and the cobalt band, metres
    TREADS=24,             # visual risers per floor. WHY: 15 degrees each, lining up with the cage's 30 degree panels
    TREAD_T=0.05,          # visual tread thickness, metres: thin metal plates as in the concept stair
    TREAD_SEGMENTS=3,      # arc segments per tread
    HANDRAIL_R=2.63,       # handrail radius, metres. WHY: just outside the treads (2.6) and inside the slab hole
                           # (2.65), so it rises through the floor above instead of through its slab
    HANDRAIL_END=290.0,    # the rail stops here, degrees. WHY: past ~290 it would cross the open arc (300-360) of
                           # the floor above, where players step off; the cage panel at 285 hides the end
    HANDRAIL_H=0.95,       # handrail height above the ramp line, metres
    HANDRAIL_T=0.02,       # handrail tube radius, metres
    HANDRAIL_SEGMENTS=96,  # samples along one turn
    CAGE_ARC_SEGMENTS=6,   # facets per curved cage panel. WHY: 5 degrees each reads as one smooth glass drum
    LOCK_REVEAL=0.015,     # width of the leaf outline groove on the locked door, metres
    LOCK_D=0.01,           # its depth, metres
    LOCK_LEAF_W=1.2, LOCK_LEAF_H=2.4,   # the door-leaf shape drawn on the locked door, metres (spine door size)
    LOCK_PLATE_W=0.12, LOCK_PLATE_H=0.3, LOCK_PLATE_Y=1.0,   # lock plate size and bottom height, metres
    LOCK_PLATE_T=0.01,     # lock plate stands this far proud, metres (inside the 15 mm rule)
    LIGHT_L=1.5,           # strip light length, metres: three 0.5 m ceiling tiles
    LIGHT_W=0.2,           # strip light width, metres
    LIGHT_DROP=0.04,       # hangs this far below the soffit, metres (brief: at most 50 mm)
    LIGHT_LIP=0.02,        # white housing lip round the diffuser, metres
    # ---- Labyrinth, round 5 (concepts/Kit_Detail/README_r5.md; sizes there are mm)
    RETURN_R=1.0,          # outer radius of the rounded corner return, metres. WHY: the largest radius a 2 x 2 m
                           # cell holds while leaving a 1 m straight land at each snap end
    ARC_SEGMENTS=12,       # facets on the return's quarter turn: 7.5 degrees each, reads as one curve
    ARC_COLLIDERS=3,       # convex pieces the curve's collider is split into. WHY: one hull over the whole quarter
                           # would fill 0.23 m of the room's inside corner; three leave under 0.03 m
    NICHE_W=0.6, NICHE_H=3.4, NICHE_R=0.3,   # capsule niche in the niche wall, metres (README_r5 corridors A)
    NICHE_BOTTOM=0.45,     # niche starts this high, metres: clear of the skirting, under the 2.1 m frost line
    NICHE_D=0.06,          # niche depth into the 0.2 m wall, leaving 0.14 m, metres (README_r5)
    COVER_H=1.1,           # crouch-cover height, metres (README_r5: 1.0-1.2 m target, 1.1 chosen)
    COUNTER_L=2.0, COUNTER_W=1.0,   # capsule counter plan, metres; its ends are half-circles of radius W/2
    COUNTER_TOP_BEVEL=0.03,         # rounded top edge, metres: catches the gloss highlight
    COUNTER_SEGMENTS=16,            # facets on each counter end
    BLOCK_L=2.0, BLOCK_W=0.6,       # block cover plan, metres (junctions B / glass B "low block")
    BLOCK_BEVEL=0.04,               # block edge bevel, metres
    FROST_LOW=1.1,         # low frosted band top, metres. 1.1 since 2026-11-15 (Alex's fluted-glass references, mood D):
                           # a fluted band to crouch height, the clear band 1.1-2.1 m keeps standing players visible
                           # (README_r5 had 0.35)
    FROST_HIGH=2.1,        # high frosted strip bottom, metres (README_r5: 2,100-2,500 mm)
    STAIR_W=2.0,           # straight stair clear width, metres (README_r5 atrium B)
    STAIR_RISERS=24,       # risers per storey: 4.5 m / 24 = 0.1875 m each (README_r5)
    STAIR_GOING=0.3,       # tread depth, metres: 24 x 0.3 = 7.2 m run, inside an 8 m bay (README_r5)
    GUARD_H=1.1,           # frameless glass guard height above its walking line, metres. WHY: waist height,
                           # so its only line sits below a standing figure's chest; no rail tube (README_r5)
    GUARD_T=0.02,          # guard glass thickness, metres
    GUARD_SHOE_H=0.12,     # silver shoe the guard glass stands in, metres (kept under the 350 mm low band)
    GUARD_SHOE_T=0.06,     # shoe thickness, metres
    RAMP_L=4.0, RAMP_RISE=1.0,      # ramp run and rise, metres: 1:4 slope (README_r5 atrium B)
    COVE_W=0.2,            # light line slot width in the soffit, metres
    COVE_D=0.08,           # its depth into the slab, leaving 0.12 m, metres (README_r5)
    # ---- Fix list 2026-11-13 (Alex: "still blockout-y"): edges that catch gloss, signs, a real stair, props
    END_CHAMFER=0.012,     # 45-degree chamfer on a wall's vertical end edges, both faces, metres. WHY: a 90 degree
                           # box shows no highlight; two chamfered ends meet as a V joint line every 4 m that does
    SIGN_W=3.2, SIGN_H=3.2,         # zone supergraphic, metres: storey-scale, painted on the wall (Alex: "think Mirror's Edge")
    SIGN_Y=0.6,            # panel bottom, metres: above the skirting, top at 3.8 m under the 4.26 m reveal
    SIGN_T=0.010,          # panel stands this far proud of the wall, metres
    INK_T=0.004,           # letters stand this far proud of the panel, metres (panel + ink = 14 mm, inside 15 mm)
    SIGN_PAD=0.22,         # inner margin of the panel, metres
    LETTER_H=1.7,          # zone letter height, metres
    NAME_H=0.26,           # department name height, metres
    MICRO_H=0.03,          # microtype height, metres. WHY 0.03: the longest line (~70 characters) fits the panel (bible §13: "tiny strings of legal/technical microtype")
    FONT_RES=3,            # curve resolution of the glyphs: few facets, they are flat graphics
    TREAD_T2=0.06,         # straight-stair tread thickness, metres
    NOSING_D=0.035,        # silver nosing strip on each tread front, metres
    STRINGER_T=0.06,       # folded side plate of the straight stair, metres
    DESK_W=1.6, DESK_D=0.8, DESK_H=0.74,   # sculptural desk, metres (standard desk height)
    DESK_TOP=0.04, DESK_SIDE=0.05,         # its top and waterfall side thickness, metres
    DESK_BEVEL=0.012,      # rounded desk edges, metres
    CHAIR_SEAT_H=0.46, CHAIR_W=0.52, CHAIR_D=0.5, CHAIR_BACK_H=0.86,   # pod chair, metres
    CHAIR_SHELL=0.06,      # shell thickness, metres
    CHAIR_BASE_R=0.3, CHAIR_STEM_R=0.035,  # swivel base disc and stem radius, metres
    # ---- Detail pass 2026-11-13, after Alex's reference plates (atrium, office floor, curved meeting room)
    COL_W=0.8,             # graphic column section, metres: the plates' monumental square columns
    COL_R=0.06,            # its rounded corners, metres
    COL_SKIN=0.003,        # cobalt supergraphic stands this far proud, metres
    COL_TEXT_H=0.06,       # wayfinding line height on a column, metres
    COL_NUMBER_H=0.55,     # big floor number on a column, metres (true height since CAP_RATIO, 2026-11-13)
    BENCH_L=2.4, BENCH_D=1.6,   # back-to-back workstation bench, metres (two 1.2 m desks each side)
    SCREEN_TOP=1.15,       # bench divider top, metres: a crouch-cover line, like the counters
    MONITOR_W=0.55, MONITOR_H=0.34,   # monitor panel, metres
    PED_W=0.4, PED_D=0.55, PED_H=0.6, # under-desk drawer pedestal, metres
    TASK_SEAT=0.47,        # task chair seat height, metres
    STORE_L=1.6, STORE_D=0.45, STORE_H=1.0,   # low storage run, metres
    COPIER_W=0.62, COPIER_D=0.66, COPIER_H=1.05,   # photocopier, metres
    RECEPTION_L=4.0, RECEPTION_D=0.9, RECEPTION_H=1.1,   # reception desk, metres
    RAIL_POST=0.04,        # balustrade post section, metres
    RAIL_TOP=0.05,         # balustrade handrail section, metres
    HEADER_R=1.5,          # curved header partition: glass radius in its 2 x 2 m cell, metres
    PENDANT_L=2.4, PENDANT_W=0.07, PENDANT_H=0.06,   # linear pendant bar, metres
    PENDANT_DROP=0.8,      # pendant hangs this far below the soffit, metres (bar at 3.5 m: clear of any jump)
    OVERLAY_T=0.008,       # carpet / ceiling-grid overlay thickness, metres
    PLANTER_L=1.0, PLANTER_W=0.45, PLANTER_H=0.55,   # white box planter, metres
    LEAVES=11,             # leaf blades per planter
    # ---- Japanese office props (sizes: concepts/Japanese_Aero/README.md "New dressing")
    SERVER_W=0.35, SERVER_H=1.35,          # water server footprint and height with its bottle, metres
    UMBRELLA_D=0.22, UMBRELLA_H=0.55,      # umbrella stand diameter and height, metres
    VEND_W=0.9, VEND_D=0.65, VEND_H=1.8,   # drinks vending machine, metres
    SLIPPER_W=0.9, SLIPPER_D=0.35, SLIPPER_H=0.7,   # slipper cabinet, metres
    PHONE_W=0.24, PHONE_D=0.22, PHONE_H=0.1,         # white desk phone, metres
    TRAY_W=0.28, TRAY_D=0.34, TRAY_H=0.18,           # two-tier document tray, metres
    LOGO_D=0.4,            # AONAGI ring logo diameter on a wall, metres
    # ---- Lift (elevator-rooftop-spec.md, agreed with the elevator session 2026-11-13; Alex: "rooftop, elevator beside reception")
    LIFT_OPEN_W=1.4, LIFT_OPEN_H=2.2,   # clear landing opening, metres, centred in a 4 m frame
    LIFT_LEAF_W=0.75, LIFT_LEAF_H=2.25, LIFT_LEAF_T=0.04,   # one bi-parting leaf, metres. WHY 0.05 wider and taller
                           # than half the opening: the leaves sit behind the frame and overlap the jambs and head
    LIFT_LEAF_BEHIND=0.025,  # leaf centre's distance behind the frame's shaft face, metres
    LIFT_CAR_W=3.7, LIFT_CAR_D=3.55, LIFT_CAR_H=2.6,   # car: across the doors, front to back, inside height, metres.
                           # WHY: fills the 3.8 x 3.65 m clear shaft leaving 0.05 m at the doors (props can't drop in)
    LIFT_CAR_FLOOR=0.2, LIFT_CAR_WALL=0.06,   # car floor plate and wall thickness, metres
    # ---- Rooftop pool (elevator-rooftop-spec.md "Rooftop pool"; Alex: "i think what could be funny is if added a pool")
    POOL_L=8.0, POOL_W=4.0,   # outside size, metres
    POOL_RIM_H=0.5, POOL_RIM_W=0.4,   # raised pearl rim, metres
    POOL_WATER=0.4,           # water surface above the basin floor, metres: wading depth, no swimming
    POOL_STEP_L=3.0,          # width of the two steps up to the rim on the front long side, metres
    # ---- Glass and structure (Alex 2026-11-14, AEROSET reference: "glass everywhere, crossed by thin dense lines")
    MULLION_W=0.04,        # navy mullion face width, metres: thin lines that read at 20 m
    MULLION_PITCH=1.0,     # vertical mullion spacing, metres: a quarter bay, so the grid lines up across pieces
    TRANSOMS=(2.6, 3.45),  # transom heights, metres. WHY above 2.1: no bars across a standing figure (kept rules)
    BEAM_W=0.08, BEAM_D=0.16,          # exposed ceiling beams along X, metres
    PURLIN_W=0.05, PURLIN_D=0.09,      # the lighter cross beam along Z, metres
    # ---- Ribbons and lightboxes (Alex 2026-11-15, PANLINE reference: glossy colour ribbons, backlit lightbox walls)
    RIBBON_W=0.5,          # cobalt ribbon width, metres
    RIBBON_MID=2.0,        # its centreline's mean distance in from the piece's z = 0 edge, metres (a 4 m corridor's middle)
    RIBBON_SWAY=0.8,       # how far the centreline swings either side, metres; one cosine over the piece, so pieces chain seamlessly
    RIBBON_LEN=16.0,       # ribbon piece length, metres. WHY 16, not one 4 m cell: a swing every 4 m read as tight loops
                           # in perspective (KitOffice capture, 2026-11-15); PANLINE's bands sweep long
    RIBBON_LINE_W=0.06, RIBBON_LINE_GAP=0.12,   # the thin aqua companion line and its gap from the ribbon, metres
    RIBBON_INLAY=0.003,    # floor inlay height above the floor, metres: a glossy skin, not a step
    RIBBON_DROP=0.2,       # ceiling ribbon's underside below the soffit, metres: under the 0.16 m beams
    RIBBON_T=0.04,         # ceiling ribbon thickness, metres
    RIBBON_SEGMENTS=128,   # samples along each piece
    LIGHTBOX_W=3.6, LIGHTBOX_Y=(0.6, 3.6), LIGHTBOX_D=0.08,   # wall lightbox: width, bottom and top, depth, metres
    LIGHTBOX_FRAME=0.06,   # its silver frame width, metres
    PANEL_SIZE=3.6,        # floating ceiling panel, metres: leaves a 0.4 m lit gap to the next cell's panel
    PANEL_R=0.35,          # its rounded corners, metres
    PANEL_DROP=(0.22, 0.16),   # its underside and top below the soffit, metres: level with where the beams hung
    PANEL_COVE_W=0.18,     # the glowing cove ring on the soffit round each panel, metres
    LED_W=1.2, LED_H=0.18, LED_T=0.06, LED_Y=2.65,   # reception dot-matrix sign and its bottom height, metres
    WATER_W=1.8, WATER_T=0.08, WATER_H=2.7,          # contained water-wall cassette, metres
)

CLEAR_H = P.FLOOR_H - P.SLAB      # floor to soffit, metres


def u(x0, x1, y0, y1, z0, z1):
    """A box from Unity extents (metres), returned in Blender's frame."""
    bm = box(x1 - x0, z1 - z0, y1 - y0)
    return move(bm, -(x0 + x1) * 0.5, -(z0 + z1) * 0.5, (y0 + y1) * 0.5)


def by_normal(bm, up, down, side):
    """Material slot per face: up-facing, down-facing, everything else. For slab tops, soffits and edges."""
    bm.normal_update()
    for f in bm.faces:
        f.material_index = up if f.normal.z > 0.5 else down if f.normal.z < -0.5 else side
    return bm


def unity_dir(deg, radius):
    """Blender (x, y) of a point at `radius` and `deg` clockwise from Unity +Z (TowerSetup.Direction)."""
    r = math.radians(deg)
    return -math.sin(r) * radius, -math.cos(r) * radius


# ---------------------------------------------------------------- materials (named for Unity)
# Colours: art bible §03. Carpet value is a guess pending the dark-carpet read test (Greybox README).
def hexc(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


# WHY 646E86: the approved concepts' carpet is a darker grey-blue than the old #8A94A3 placeholder; this is
# their painted value (#5E6A96 to #6D79A3, lit by a blue sky) pulled back toward grey so it reads as albedo.
M_FLOOR = material('Floor_Carpet', hexc('DCE8EC'), roughness=0.04)   # polished walkway, the mix (Japanese_Aero/DECISION.md)
# WHY B8C2CE, near-mirror: round 5 drops carpet for a polished pale resin floor (Alex, 2026-11-12). The name stays
# Floor_Carpet because Unity maps the exported material by name; KitImport.cs holds the same values.
M_CEIL = material('Ceiling_Tile', hexc('F0F5F5'), roughness=0.9)
M_WALL = material('Wall_PaperWhite', hexc('F4F8F8'), roughness=0.15)   # pearl lacquer, gloss (AEROSET: gloss everywhere)
M_FRAME = material('Frame_Aluminium', hexc('C8D2D9'), metallic=0.85, roughness=0.35)   # silver (the mix)
M_GLASS = material('Glass_Clear', (0.70, 0.85, 0.95), roughness=0.05)
M_DOOR = material('Door_Leaf', hexc('C9CED3'), roughness=0.6)
M_STAIR = material('Stair_Aluminium', hexc('C9CED3'), metallic=0.6, roughness=0.35)
M_TRIM = material('Trim_Navy', hexc('111B38'), roughness=0.5)   # Deep Navy; bible §11 "black trim" anchors
M_RAIL = M_TRIM if P.RAIL_FINISH == 'navy' else M_FRAME
M_COBALT = material('Accent_Cobalt', hexc('244FB9'), roughness=0.06)   # gloss cobalt (README_r5)   # Corporate Cobalt, bible §03: large blocks only
M_LIGHT = material('Light_Strip', hexc('FFFFFF'), roughness=0.5)      # emissive diffuser; Unity sets the emission
M_RED = material('Door_LockedRed', hexc('C0281E'), roughness=0.45)
# WHY red outside the palette: the locked stair door is the one warm gameplay accent (bible §03 allows rationed
# gameplay accents); C0281E is a little brighter than the greybox door so it holds up next to Cobalt.


def emissive(mat, strength=4.0):
    """Lights a material's surface in Blender previews and review renders. FBX doesn't carry it; KitImport sets it."""
    tree = getattr(mat, 'node_tree', None)
    bsdf = tree.nodes.get('Principled BSDF') if tree else None
    if bsdf and 'Emission Color' in bsdf.inputs:
        bsdf.inputs['Emission Color'].default_value = (1.0, 1.0, 1.0, 1.0)
        bsdf.inputs['Emission Strength'].default_value = strength


emissive(M_LIGHT)
# Round 5: frosted glass for the high and low strips. A pale, mostly opaque white so it reads as "you can't see
# through here" next to the near-clear Glass_Clear; KitImport sets its transparency in Unity.
M_FROST = material('Glass_Frosted', hexc('C6E9E6'), roughness=0.7)   # aqua pearl frost (the mix)
# Fluted (reeded) frosted glass, mood D (Alex's references, 2026-11-15): pale blue-white; the flutes are its texture
# (textures/make_textures.py fluted_glass, 1 m per repeat on the kit's box-mapped UVs).
M_FLUTED = material('Glass_Fluted', hexc('DCEBF5'), roughness=0.4)
# The louvres' slats: the same reeded frost, glowing pale blue as if lit from the room behind (Alex's "DIVISION" reference;
# KitImport sets the emission). WHY their own material: light from inside a room can't light a transparent slat's outer face.
M_LOUVRE = material('Glass_Louvre', hexc('CFE6FF'), roughness=0.4)
# Detail pass 2026-11-13 (Alex's reference plates). Carpet and ceiling grid get tile textures in Unity (KitImport).
M_CARPET = material('Carpet_Dark', hexc('7E96AC'), roughness=0.95)   # paler blue work-area carpet (the mix)
M_GRID = material('Ceiling_Grid', hexc('F0F5F5'), roughness=0.9)    # tiled ceiling over work areas
M_LEAF = material('Plant_Leaf', hexc('70AF59'), roughness=0.76)      # living green (Aero; the mix)
# The mix (concepts/Japanese_Aero/DECISION.md): aqua and mist join cobalt; one red for the hanko dot; water.
M_AQUA = material('Accent_Aqua', hexc('45BFC7'), roughness=0.24)     # sign faces, column skins, reception block
M_MIST = material('Accent_Mist', hexc('A8BFD1'), roughness=0.6)      # quiet fields (Soft Club)
M_SEAL = material('Accent_Seal', hexc('C7473E'), roughness=0.65)     # the hanko dot only
M_WATER = material('Water_Wall', hexc('7ADAD8'), roughness=0.1)      # the contained water cassette
M_POOL = material('Pool_Water', hexc('7ADAD8'), roughness=0.1)       # the rooftop pool's surface (same shader, top-down)


def recessed(bm, cutter):
    """bm with cutter subtracted; the faces the cut leaves get material slot 1 (the trim)."""
    return boolean(bm, paint(cutter, 1))


def front_faces(bm, slot, min_y=None):
    """Paints the faces on the piece's grid-line face (Unity +Z, Blender -Y) with `slot`, optionally only above min_y.

    Only faces still on slot 0 change, so the trim the cuts painted stays navy.
    """
    bm.normal_update()
    for f in bm.faces:
        c = f.calc_center_median()
        if f.material_index == 0 and f.normal.y < -0.5 and abs(c.y) < 1e-4 and (min_y is None or c.z > min_y):
            f.material_index = slot
    return bm


def wall_cuts(x0, x1, door=None, portal=False):
    """Shadow-gap cutters for a wall run from x0 to x1: skirting and soffit reveal on both faces.

    door=(dx0, dx1, dh) adds the reveal round an opening; portal adds the line under the cobalt head band.
    The cutters overshoot the run's ends, so the lines carry on unbroken across the next piece.
    """
    eps = 0.01   # WHY: overshoot the faces being opened, so the boolean leaves no paper-thin skin
    a, b = x0 - eps, x1 + eps
    cuts = []
    for z0, z1 in ((-P.SKIRT_D, eps), (-P.WALL - eps, -P.WALL + P.SKIRT_D)):
        cuts.append(u(a, b, -eps, P.SKIRT_H, z0, z1))
    for z0, z1 in ((-P.TOP_D, eps), (-P.WALL - eps, -P.WALL + P.TOP_D)):
        cuts.append(u(a, b, CLEAR_H - P.TOP_H, CLEAR_H + eps, z0, z1))
    if portal:
        g = P.PORTAL_GAP * 0.5
        cuts.append(u(a, b, P.PORTAL_Y - g, P.PORTAL_Y + g, -P.TOP_D, eps))
    if door:
        dx0, dx1, dh = door
        w = P.ARCH_W
        # WHY the half-width overlaps: jambs stop inside the head line and the head line stops inside the jambs,
        # so no two cutter edges coincide (coincident edges leave zero-area faces in the exact boolean).
        for z0, z1 in ((-P.ARCH_D, eps), (-P.WALL - eps, -P.WALL + P.ARCH_D)):
            cuts += [u(dx0 - w, dx0 + eps, -eps, dh + w * 0.5, z0, z1),          # left jamb line
                     u(dx1 - eps, dx1 + w, -eps, dh + w * 0.5, z0, z1),          # right jamb line
                     u(dx0 - w * 0.5, dx1 + w * 0.5, dh - eps, dh + w, z0, z1)]  # head line
    return join(*cuts)


# ---------------------------------------------------------------- pieces

def slab(width, depth):
    """Floor slab bay: top at floor level, carpet on top, ceiling below, white edges. Box collider."""
    bm = by_normal(u(0, width, -P.SLAB, 0, -depth, 0), 0, 1, 2)
    ob = part(f'Kit_Slab_{width:g}x{depth:g}', bm, [M_FLOOR, M_CEIL, M_WALL], smooth=None)
    expect(ob, size=(width, depth, P.SLAB))
    collider(ob, 'box')


def stairwell_slab():
    """3 x 2 bay slab with the round stair opening; stair centre at local (W/2, 0, -D/2). Mesh collider (not convex)."""
    w, d = P.STAIRWELL_W, P.STAIRWELL_D
    hole = cylinder(P.STAIR_R + P.HOLE_GAP, P.SLAB + 0.2, segments=48)
    move(hole, -w * 0.5, d * 0.5, -P.SLAB * 0.5)
    bm = by_normal(boolean(u(0, w, -P.SLAB, 0, -d, 0), hole), 0, 1, 2)
    ob = part('Kit_Slab_Stairwell', bm, [M_FLOOR, M_CEIL, M_WALL], smooth=None)
    expect(ob, size=(w, d, P.SLAB))


# Wall finishes, by suffix. '' is Paper White; the others paint the grid-line face (Unity +Z), which is the
# main-floor side of a spine wall. Three spine walls in a row, Cobalt | PortalHead door | Cobalt, make the
# approved portal round the stair: cobalt legs, a white field with the door, a cobalt head band over it.
FINISHES = {'': None, '_Cobalt': 0.0, '_PortalHead': P.PORTAL_Y}


def wall_mats(finish):
    """Slot list for a wall: white body, navy cuts, cobalt face (only when the finish paints one)."""
    return [M_WALL, M_TRIM] + ([M_COBALT] if FINISHES[finish] is not None else [])


def finish_face(bm, finish):
    """Paints the cobalt face a finish asks for: the whole grid-line face, or the part above the portal line."""
    y = FINISHES[finish]
    return bm if y is None else front_faces(bm, 2, min_y=y or None)


def end_chamfers(x0, x1):
    """Cutters that chamfer the vertical edges at a wall run's two ends on both faces, END_CHAMFER at 45 degrees.

    Kept on the wall's own material (slot 0) so the bevel catches the gloss highlight; the snap end stays flat
    across all but the outer 12 mm of each face, so the next wall still meets it flush.
    """
    c, eps = P.END_CHAMFER, 0.01
    cuts = []
    for cx, dx in ((x0, 1), (x1, -1)):
        for cz, dz in ((0.0, -1), (-P.WALL, 1)):
            # Triangle in (a along +dx, b along +dz) round the corner, its long side on the chamfer line a + b = c.
            tri = [(c + eps, -eps), (-eps, c + eps), (-eps, -eps)]
            outline = [(cx + dx * a, cz + dz * b) for a, b in tri]
            cuts.append(prism_xz(outline, -eps, CLEAR_H + eps))
    return join(*cuts)


JOINT_W = 0.02      # navy joint line width on solid white walls, metres
JOINT_PROUD = (0.002, 0.003)   # vertical and horizontal joint lines stand this far proud, metres. WHY two values:
                               # where they cross, the horizontal one's face lies in front, so no two faces coincide


def wall_joints(width):
    """Thin navy joint lines on both faces of a solid wall, on the curtain walls' grid: verticals every MULLION_PITCH
    and lines at the TRANSOMS heights, between the skirting and the soffit reveal. The AEROSET lesson (Alex,
    2026-11-14) carried onto walls that must stay solid for cover: thin dense lines instead of a blank plane."""
    lines = []
    for z0, sign in ((0.0, 1.0), (-P.WALL, -1.0)):
        dv, dh = (sign * d for d in JOINT_PROUD)
        x = P.MULLION_PITCH
        while x < width - 1e-6:
            lines.append(u(x - JOINT_W / 2, x + JOINT_W / 2, P.SKIRT_H, CLEAR_H - P.TOP_H, min(z0, z0 + dv), max(z0, z0 + dv)))
            x += P.MULLION_PITCH
        for ty in P.TRANSOMS:
            lines.append(u(0, width, ty - JOINT_W / 2, ty + JOINT_W / 2, min(z0, z0 + dh), max(z0, z0 + dh)))
    return join(*lines)


def wall_solid(width, finish=''):
    """Opaque wall, floor to soffit, with shadow-gap skirting, a soffit reveal and chamfered ends. Box collider.
    The plain white finish carries navy joint lines on the curtain walls' grid (wall_joints, 2026-11-14)."""
    bm = recessed(u(0, width, 0, CLEAR_H, -P.WALL, 0), wall_cuts(0, width, portal=finish == '_PortalHead'))
    bm = boolean(bm, paint(end_chamfers(0, width), 0))
    if finish == '':
        bm = join(bm, paint(wall_joints(width), 1))
    ob = part(f'Kit_Wall_Solid_{width:g}m{finish}', finish_face(bm, finish), wall_mats(finish), smooth=None)
    expect(ob, size=(width, P.WALL + (2 * JOINT_PROUD[1] if finish == '' else 0), CLEAR_H), tol=0.002)
    collider(ob, 'box')


def wall_door(finish='', width=None):
    """Wall with a centred doorway: 4 m by default, or 2 m (Wall_Door_2m, 0.4 m jambs) for the L2 layout's doors on
    the 2 m grid (Alex approved L2, 2026-11-15). Three parts so each gets an exact box collider and the gap stays open.

    A shadow reveal frames the opening on both faces, outside the clear 1.2 x 2.4 m the leaf fills.
    """
    width = width or P.BAY
    x0 = (width - P.DOOR_W) * 0.5
    x1 = x0 + P.DOOR_W
    cuts = lambda a, b: wall_cuts(a, b, door=(x0, x1, P.DOOR_H), portal=finish == '_PortalHead')
    parts = []
    for name, box_ in (('Left', u(0, x0, 0, CLEAR_H, -P.WALL, 0)),
                       ('Right', u(x1, width, 0, CLEAR_H, -P.WALL, 0)),
                       ('Head', u(x0, x1, P.DOOR_H, CLEAR_H, -P.WALL, 0))):
        bm = recessed(box_, cuts(0, width))
        if name != 'Head':   # the outer ends of the left and right parts are the wall's snap ends
            bm = boolean(bm, paint(end_chamfers(0, width), 0))
        bm = finish_face(bm, finish)
        parts.append(part(f'Kit_Wall_Door_{width:g}m{finish}_{name}', bm, wall_mats(finish), smooth=None))
    left, right, head = parts
    for ob in parts:
        collider(ob, 'box')
    expect(left, size=(x0, P.WALL, CLEAR_H))
    expect(head, size=(P.DOOR_W, P.WALL, CLEAR_H - P.DOOR_H))
    group(f'Kit_Wall_Door_{width:g}m{finish}', left, right, head)


def door_leaf():
    """Door leaf for the doorway. Pivot on the hinge axis at floor level; the leaf runs +X from it, closed.

    Placed in a Kit_Wall_Door_4m at (x0 + DOOR_GAP, 0, -WALL/2), where x0 is the opening's left edge.
    """
    w = P.DOOR_W - 2 * P.DOOR_GAP
    h = P.DOOR_H - 2 * P.DOOR_GAP
    ob = part('Kit_Door_Leaf', u(0, w, P.DOOR_GAP, P.DOOR_GAP + h, -P.DOOR_T * 0.5, P.DOOR_T * 0.5), M_DOOR, smooth=None)
    expect(ob, size=(w, P.DOOR_T, h))
    collider(ob, 'box')


def window_bay(width):
    """Façade bay between column centres: solid sill, rails, one visual-only pane (no collider).

    White Trays: a dark pocket runs along the street-top edge of the sill between the column faces, so from
    the rival tower each floor reads as a white shelf under a dark band. The pocket leaves 0.08 m of solid
    sill behind it and the bounds unchanged, so the box collider (the full sill height of cover) is the same as before.
    """
    c = P.COLUMN_W * 0.5
    zc = -P.WALL * 0.5
    eps = 0.01   # WHY: the cutter overshoots the faces it opens, so the boolean leaves no paper-thin skin
    pocket = u(c, width - c, P.SILL - P.TRAY_RECESS_H, P.SILL + eps, -P.TRAY_RECESS_D, eps)
    sill = part(f'Kit_Window_{width:g}m_Sill', recessed(u(0, width, 0, P.SILL, -P.WALL, 0), pocket),
                [M_WALL, M_TRIM], smooth=None)
    collider(sill, 'box')
    rails = [u(c, width - c, P.SILL, P.SILL + P.RAIL, zc - P.RAIL_D * 0.5, zc + P.RAIL_D * 0.5),
             u(c, width - c, CLEAR_H - P.RAIL, CLEAR_H, zc - P.RAIL_D * 0.5, zc + P.RAIL_D * 0.5)]
    rails += mullion_grid(c, width - c, P.SILL + P.RAIL, CLEAR_H - P.RAIL, zc, P.RAIL_D)
    frame = part(f'Kit_Window_{width:g}m_Frame', join(*rails), M_RAIL, smooth=None)
    pane = part(f'Kit_Window_{width:g}m_Glass',
                u(c, width - c, P.SILL + P.RAIL, CLEAR_H - P.RAIL, zc - P.GLASS_T * 0.5, zc + P.GLASS_T * 0.5),
                M_GLASS, smooth=None)
    expect(sill, size=(width, P.WALL, P.SILL))
    expect(pane, size=(width - P.COLUMN_W, P.GLASS_T, CLEAR_H - P.SILL - 2 * P.RAIL))
    group(f'Kit_Window_{width:g}m', sill, frame, pane)


def mullion_grid(x0, x1, y0, y1, zc, depth):
    """Thin navy mullions every MULLION_PITCH metres between x0 and x1 (on the kit's 1 m grid, so neighbouring pieces
    line up) and transoms at TRANSOMS, all from y0 to y1, centred on zc. Boxes to join into a frame."""
    w, d = P.MULLION_W, depth * 0.5
    bars = []
    x = math.floor(x0 / P.MULLION_PITCH + 1) * P.MULLION_PITCH
    while x < x1 - 1e-6:
        bars.append(u(x - w / 2, x + w / 2, y0, y1, zc - d, zc + d))
        x += P.MULLION_PITCH
    for ty in P.TRANSOMS:
        if y0 < ty < y1:
            bars.append(u(x0, x1, ty - w / 2, ty + w / 2, zc - d, zc + d))
    return bars


def wall_curtain(width):
    """A glass curtain wall in place of a solid one, floor to soffit: clear glass on a navy grid (a base and head
    rail, mullions every metre, transoms at 2.6 and 3.45 m). Same footprint as Wall_Solid: runs +X from the pivot,
    thickness toward -Z. Stops bullets and players like a wall (box collider on the frame) but hides nothing.
    The AEROSET lesson (Alex, 2026-11-14): many thin dark lines over glass instead of blank white planes."""
    zc, dp, r = -P.WALL * 0.5, P.RAIL_D, P.RAIL
    bars = [u(0, width, 0, r, zc - dp / 2, zc + dp / 2), u(0, width, CLEAR_H - r, CLEAR_H, zc - dp / 2, zc + dp / 2),
            u(0, P.MULLION_W / 2, r, CLEAR_H - r, zc - dp / 2, zc + dp / 2),
            u(width - P.MULLION_W / 2, width, r, CLEAR_H - r, zc - dp / 2, zc + dp / 2)]
    bars += mullion_grid(0, width, r, CLEAR_H - r, zc, dp)
    frame = part(f'Kit_Wall_Curtain_{width:g}m_Frame', join(*bars), M_RAIL, smooth=None)
    glass = part(f'Kit_Wall_Curtain_{width:g}m_Glass',
                 u(0, width, r, CLEAR_H - r, zc - P.GLASS_T / 2, zc + P.GLASS_T / 2), M_GLASS, smooth=None)
    expect(frame, size=(width, dp, CLEAR_H))
    collider(frame, 'box')
    group(f'Kit_Wall_Curtain_{width:g}m', frame, glass)


def ceiling_beams():
    """Exposed structure under a 4 x 4 m soffit: two silver beams along X either side of the slab's light line, and a
    lighter cross beam along Z through the middle. Pivot at the soffit's NW corner, like a slab with its top at
    the soffit: everything hangs below y 0. Visual only. The AEROSET lesson: a thin-beam roof with rows of lights,
    not a flat plane."""
    B = P.BAY
    beams = [u(0, B, -P.BEAM_D, 0, z - P.BEAM_W / 2, z + P.BEAM_W / 2) for z in (-B * 0.25, -B * 0.75)]
    purlin = u(B / 2 - P.PURLIN_W / 2, B / 2 + P.PURLIN_W / 2, -P.BEAM_D, -P.BEAM_D + P.PURLIN_D, -B, 0)
    ob = part('Kit_Ceiling_Beams_4x4', join(*beams, purlin), M_FRAME, smooth=None)
    expect(ob, size=(B, B, P.BEAM_D))


def pier(x0, x1):
    """A façade column from x0 to x1, floor to soffit, proud of both wall faces, with its street face recessed.

    White Trays: above the sill rail and below the head, the street face steps back PIER_SETBACK into a navy
    slot, keeping a full-depth PIER_LAND at each side (the pane and the side wall meet solid) and the full
    section at the base and head (the snap faces). From across the street the columns read as dark gaps.
    """
    eps = 0.01   # WHY: overshoot the street face so the boolean leaves no skin
    body = u(x0, x1, 0, CLEAR_H, -P.WALL - P.COLUMN_PROUD, P.COLUMN_PROUD)
    slot = u(x0 + P.PIER_LAND, x1 - P.PIER_LAND, P.SILL + P.RAIL, CLEAR_H - P.PIER_TOP,
             P.COLUMN_PROUD - P.PIER_SETBACK, P.COLUMN_PROUD + eps)
    return recessed(body, slot)


def column_facade():
    """Façade column, centred on a grid point, floor to soffit, slightly proud of the wall line. Box collider."""
    c = P.COLUMN_W * 0.5
    ob = part('Kit_Column_Facade', pier(-c, c), [M_WALL, M_TRIM], smooth=None)
    expect(ob, size=(P.COLUMN_W, P.WALL + 2 * P.COLUMN_PROUD, CLEAR_H))
    collider(ob, 'box')


def column_corner(side):
    """Façade column at the building's left or right corner, flush with the side wall instead of centred on the grid.

    WHY a separate piece: a centred column at the corner stands half its width outside the side wall
    (found by assembly_test.py). The repo's template builder pulls its end pillars inward the same way.
    The pivot stays on the corner grid point; the column sits inside it, proud of the side wall too.
    """
    w = P.COLUMN_W
    x0, x1 = (-P.COLUMN_PROUD, w) if side == 'L' else (-w, P.COLUMN_PROUD)
    ob = part(f'Kit_Column_Corner{side}', pier(x0, x1), [M_WALL, M_TRIM], smooth=None)
    expect(ob, size=(w + P.COLUMN_PROUD, P.WALL + 2 * P.COLUMN_PROUD, CLEAR_H))
    collider(ob, 'box')


def partition_glass(width):
    """Meeting-room glass wall: base and top rail plus pane. One box collider over the whole wall."""
    zc = -P.PART_T * 0.5
    rails = join(u(0, width, 0, P.PART_RAIL, -P.PART_T, 0),
                 u(0, width, P.PART_H - P.PART_RAIL, P.PART_H, -P.PART_T, 0))
    # WHY navy: the approved White Trays concept draws slender navy tracks, matching the window rails.
    # Mullions every metre since 2026-11-14 (AEROSET reference): the same grid as the curtain walls.
    rails = join(rails, *mullion_grid(0, width, P.PART_RAIL, P.PART_H - P.PART_RAIL, zc, P.PART_T))
    frame = part(f'Kit_Partition_Glass_{width:g}m_Frame', rails, M_RAIL, smooth=None)
    pane = part(f'Kit_Partition_Glass_{width:g}m_Glass',
                u(0, width, P.PART_RAIL, P.PART_H - P.PART_RAIL, zc - P.GLASS_T * 0.5, zc + P.GLASS_T * 0.5),
                M_GLASS, smooth=None)
    collider(frame, 'box')   # the rails' box spans the full wall, so it blocks the pane too
    expect(frame, size=(width, P.PART_T, P.PART_H))
    group(f'Kit_Partition_Glass_{width:g}m', frame, pane)


def partition_fluted(width):
    """Full-height fluted frosted glass wall (mood D, Alex's references, 2026-11-15): the meeting-room glass frame with a
    reeded pane that turns whoever is behind it into a soft silhouette. For back rooms. One box collider."""
    zc = -P.PART_T * 0.5
    rails = join(u(0, width, 0, P.PART_RAIL, -P.PART_T, 0),
                 u(0, width, P.PART_H - P.PART_RAIL, P.PART_H, -P.PART_T, 0))
    rails = join(rails, *mullion_grid(0, width, P.PART_RAIL, P.PART_H - P.PART_RAIL, zc, P.PART_T))
    frame = part(f'Kit_Partition_Fluted_{width:g}m_Frame', rails, M_RAIL, smooth=None)
    pane = part(f'Kit_Partition_Fluted_{width:g}m_Glass',
                u(0, width, P.PART_RAIL, P.PART_H - P.PART_RAIL, zc - P.GLASS_T * 0.5, zc + P.GLASS_T * 0.5),
                M_FLUTED, smooth=None)
    collider(frame, 'box')
    expect(frame, size=(width, P.PART_T, P.PART_H))
    group(f'Kit_Partition_Fluted_{width:g}m', frame, pane)


def partition_louvre(width):
    """Horizontal frosted-glass louvre wall (mood D, Alex's "DIVISION" reference, 2026-11-15): fluted glass slats with
    see-through gaps between them, in the meeting-room frame, for rooms lit in their half's colour. One box collider."""
    zc = -P.PART_T * 0.5
    rails = join(u(0, width, 0, P.PART_RAIL, -P.PART_T, 0),
                 u(0, width, P.PART_H - P.PART_RAIL, P.PART_H, -P.PART_T, 0))
    rails = join(rails, *mullion_grid(0, width, P.PART_RAIL, P.PART_H - P.PART_RAIL, zc, P.PART_T))
    frame = part(f'Kit_Partition_Louvre_{width:g}m_Frame', rails, M_RAIL, smooth=None)
    slats = []
    y = P.PART_RAIL + (P.LOUVRE_PITCH - P.LOUVRE_SLAT) * 0.5
    while y + P.LOUVRE_SLAT <= P.PART_H - P.PART_RAIL + 1e-6:
        slats.append(u(0, width, y, y + P.LOUVRE_SLAT, zc - P.LOUVRE_T * 0.5, zc + P.LOUVRE_T * 0.5))
        y += P.LOUVRE_PITCH
    glass = part(f'Kit_Partition_Louvre_{width:g}m_Slats', join(*slats), M_LOUVRE, smooth=None)
    collider(frame, 'box')
    expect(frame, size=(width, P.PART_T, P.PART_H))
    group(f'Kit_Partition_Louvre_{width:g}m', frame, glass)


def pendant_sphere():
    """A glowing sphere pendant for the court (mood D, Alex's atrium reference, 2026-11-15): an emissive frosted ball on
    a silver cable and ceiling cap. Pivot at the cable's top on the soffit; everything hangs below y 0. Visual only."""
    r, drop = P.SPHERE_R, P.SPHERE_DROP
    ball = move(sphere(r, segments=32, rings=16), 0, 0, -(drop + r))
    cable = move(cylinder(P.SPHERE_CABLE_R, drop, segments=8), 0, 0, -drop * 0.5)
    cap = move(cylinder(0.08, 0.05, segments=24), 0, 0, -0.025)
    ob = part('Kit_Pendant_Sphere', join(paint(ball, 0), paint(join(cable, cap), 1)), [M_LIGHT, M_FRAME], smooth=30)
    expect(ob, size=(2 * r, 2 * r, drop + 2 * r), tol=0.01)


def partition_glass_door(width):
    """Glass partition with a centred 1.2 x 2.4 m doorway: the meeting-room glass of Partition_Glass with navy jamb
    posts and the top rail carried over the opening as its head. For the L2 layout's room doors in glass walls
    (Alex approved L2, 2026-11-15). Three box colliders (left, right, head) so the doorway stays open."""
    zc = -P.PART_T * 0.5
    x0 = (width - P.DOOR_W) * 0.5
    x1 = x0 + P.DOOR_W
    w = P.MULLION_W
    frames, colliders_ = [], []
    for name, a, b in (('Left', 0.0, x0), ('Right', x1, width)):
        rails = join(u(a, b, 0, P.PART_RAIL, -P.PART_T, 0),
                     u(a, b, P.PART_H - P.PART_RAIL, P.PART_H, -P.PART_T, 0),
                     # the jamb post on the opening side and the end post on the snap end
                     u(x0 - w if name == 'Left' else x1, x0 if name == 'Left' else x1 + w, 0, P.PART_H, -P.PART_T, 0),
                     u(0 if name == 'Left' else width - w / 2, w / 2 if name == 'Left' else width,
                       0, P.PART_H, zc - P.PART_T * 0.25, zc + P.PART_T * 0.25))
        ob = part(f'Kit_Partition_Glass_Door_{width:g}m_{name}', rails, M_RAIL, smooth=None)
        collider(ob, 'box')
        frames.append(ob)
    head = part(f'Kit_Partition_Glass_Door_{width:g}m_Head', u(x0, x1, P.DOOR_H, P.PART_H, -P.PART_T, 0), M_RAIL,
                smooth=None)
    collider(head, 'box')
    panes = join(u(0, x0 - w, P.PART_RAIL, P.PART_H - P.PART_RAIL, zc - P.GLASS_T * 0.5, zc + P.GLASS_T * 0.5),
                 u(x1 + w, width, P.PART_RAIL, P.PART_H - P.PART_RAIL, zc - P.GLASS_T * 0.5, zc + P.GLASS_T * 0.5))
    glass = part(f'Kit_Partition_Glass_Door_{width:g}m_Glass', panes, M_GLASS, smooth=None)
    expect(head, size=(P.DOOR_W, P.PART_T, P.PART_H - P.DOOR_H))
    group(f'Kit_Partition_Glass_Door_{width:g}m', *frames, head, glass)


def stair_flight():
    """One full clockwise turn of solid tread rising one floor (GreyboxTowerSetup.FillFlightMesh). Use as a MeshCollider."""
    bm = bmesh.new()
    rings = []
    n = P.FLIGHT_SEGMENTS
    for i in range(n + 1):
        t = i / n
        y = t * P.FLOOR_H
        ring = []
        for rad, yy in ((P.COLUMN_R, y), (P.STAIR_R, y), (P.STAIR_R, y - P.FLIGHT_T), (P.COLUMN_R, y - P.FLIGHT_T)):
            bx, by = unity_dir(t * 360.0, rad)
            ring.append(bm.verts.new((bx, by, yy)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for k in range(4):
            bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = part('Kit_Stair_Flight', bm, M_STAIR)
    expect(ob, size=(2 * P.STAIR_R, 2 * P.STAIR_R, P.FLOOR_H + P.FLIGHT_T), tol=0.01)


def stair_column():
    """The solid column the flight winds round, floor to the next floor. Hull collider.

    Paper White, as in every approved concept: the white column is the stair's centre line seen from the rival.
    """
    ob = part('Kit_Stair_Column', cylinder(P.COLUMN_R, P.FLOOR_H, segments=32, base='bottom'), M_WALL)
    expect(ob, size=(2 * P.COLUMN_R, 2 * P.COLUMN_R, P.FLOOR_H), tol=0.01)
    collider(ob, 'hull')


def sweep(rings, close_ends=True):
    """A closed bmesh through rings of matching vertex loops (lists of Blender points), capped at both ends."""
    bm = bmesh.new()
    vs = [[bm.verts.new(p) for p in ring] for ring in rings]
    n = len(vs[0])
    for a, b in zip(vs, vs[1:]):
        for k in range(n):
            bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    if close_ends:
        bm.faces.new(vs[0])
        bm.faces.new(list(reversed(vs[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def sector(r0, r1, a0, a1, y0, y1, segments):
    """A solid ring sector between radii r0-r1, angles a0-a1 (degrees clockwise from Unity +Z), heights y0-y1."""
    rings = []
    for i in range(segments + 1):
        a = a0 + (a1 - a0) * i / segments
        ring = []
        for r, y in ((r0, y0), (r1, y0), (r1, y1), (r0, y1)):
            bx, by = unity_dir(a, r)
            ring.append((bx, by, y))
        rings.append(ring)
    return sweep(rings)


def stair_cage_panel():
    """One curved glass cage panel, centred on +Z at the cage radius. Rotate copies about the pivot by 15 + 30k degrees.

    The template leaves out the panels from 300 to 360 degrees, where the flight from below arrives.
    Curved, not flat, so the ten panels read as the one glass drum every approved concept shows. They meet edge
    to edge with no overlap (an overlap would make two glass faces z-fight). Hull collider: the hull of a 30 degree
    arc is the arc plus its chord, 0.09 m deep at most, still clear of the treads at 2.6 m.
    """
    radius = P.STAIR_R + P.CAGE_GAP
    half = 180.0 / P.CAGE_PANELS
    r0, r1 = radius - P.CAGE_T * 0.5, radius + P.CAGE_T * 0.5
    ob = part('Kit_Stair_CagePanel', sector(r0, r1, -half, half, 0, CLEAR_H, P.CAGE_ARC_SEGMENTS), M_GLASS, smooth=None)
    t = math.radians(half)
    expect(ob, size=(2 * r1 * math.sin(t), r1 - r0 * math.cos(t), CLEAR_H), tol=0.002)
    collider(ob, 'hull')


def stair_steps():
    """Visual open treads and handrail over the ramp. No collider: Stair_Flight stays the walking surface.

    WHY visual only: players are physics capsules and walk the ramp (TOWER.md). Each tread's top sits on the
    ramp line at its middle, so a foot is never more than half a riser (0.09 m) off the tread it stands on.
    Use with Stair_Flight's renderer off, so the treads read open as in the approved concepts.
    """
    riser = P.FLOOR_H / P.TREADS
    step = 360.0 / P.TREADS
    treads = [sector(P.COLUMN_R, P.STAIR_R, k * step, (k + 1) * step, (k + 0.5) * riser - P.TREAD_T,
                     (k + 0.5) * riser, P.TREAD_SEGMENTS) for k in range(P.TREADS)]
    tread_ob = part('Kit_Stair_Steps_Treads', join(*treads), M_STAIR, smooth=None)
    rings = []
    sides = 8   # WHY 8: an octagonal tube reads round at stair distance for a fraction of the triangles
    for i in range(P.HANDRAIL_SEGMENTS + 1):
        t = i / P.HANDRAIL_SEGMENTS * P.HANDRAIL_END / 360.0
        y = t * P.FLOOR_H + P.HANDRAIL_H
        ring = []
        for s in range(sides):
            phi = 2 * math.pi * s / sides
            bx, by = unity_dir(t * 360.0, P.HANDRAIL_R + P.HANDRAIL_T * math.cos(phi))
            ring.append((bx, by, y + P.HANDRAIL_T * math.sin(phi)))
        rings.append(ring)
    rail_ob = part('Kit_Stair_Steps_Handrail', sweep(rings), M_STAIR, smooth=60)
    expect(tread_ob, size=(2 * P.STAIR_R, 2 * P.STAIR_R, P.FLOOR_H - riser + P.TREAD_T), tol=0.01)
    group('Kit_Stair_Steps', tread_ob, rail_ob)


def stair_locked_door():
    """The red locked door across the stair, to the same envelope as GreyboxTowerSetup's LockedDoor body.

    1.75 x 4.2 x 0.2 m (tread width less 0.05, floor to under the flight above less 0.05), pivot bottom centre, so
    it can replace the prefab's cube 1:1. A door-leaf outline and a navy lock plate on both faces say "door" at a
    glance; the full height stays solid because the door must not be jumped or mantled. Box collider.
    """
    w = P.STAIR_R - P.COLUMN_R - 0.05          # WHY 0.05: the greybox prefab's clearance to column and cage
    h = P.FLOOR_H - P.FLIGHT_T - 0.05          # WHY 0.05: its clearance under the flight above
    t = 0.2                                    # greybox prefab thickness, metres
    eps = 0.01
    lw, lh, g, d = P.LOCK_LEAF_W * 0.5, P.LOCK_LEAF_H, P.LOCK_REVEAL, P.LOCK_D
    cuts = []
    for z0, z1 in ((t * 0.5 - d, t * 0.5 + eps), (-t * 0.5 - eps, -t * 0.5 + d)):
        cuts += [u(-lw - g, -lw, -eps, lh + g, z0, z1), u(lw, lw + g, -eps, lh + g, z0, z1),
                 u(-lw - g, lw + g, lh, lh + g, z0, z1)]
    body = recessed(u(-w * 0.5, w * 0.5, 0, h, -t * 0.5, t * 0.5), join(*cuts))
    px = lw - 0.1 - P.LOCK_PLATE_W   # WHY 0.1: the plate sits a hand's width in from the leaf's latch edge
    plates = join(*(u(px, px + P.LOCK_PLATE_W, P.LOCK_PLATE_Y, P.LOCK_PLATE_Y + P.LOCK_PLATE_H, z0, z1)
                    for z0, z1 in ((t * 0.5, t * 0.5 + P.LOCK_PLATE_T), (-t * 0.5 - P.LOCK_PLATE_T, -t * 0.5))))
    ob = part('Kit_Stair_LockedDoor', join(body, paint(plates, 1)), [M_RED, M_TRIM], smooth=None)
    expect(ob, size=(w, t + 2 * P.LOCK_PLATE_T, h))
    collider(ob, 'box')


def ceiling_light():
    """Surface strip light for the ceiling grid. Pivot at its top centre, which goes against the soffit.

    White housing with a recessed glowing diffuser, 40 mm deep. Visual only: no collider (it is above any
    player's reach and should not catch thrown props). Lay two per 4 m bay along the tile rows (README).
    """
    L, W, dz = P.LIGHT_L, P.LIGHT_W, P.LIGHT_DROP
    lip, eps = P.LIGHT_LIP, 0.01
    lens = u(-L * 0.5 + lip, L * 0.5 - lip, -dz - eps, -dz + 0.005, -W * 0.5 + lip, W * 0.5 - lip)
    ob = part('Kit_Ceiling_LightStrip', recessed(u(-L * 0.5, L * 0.5, -dz, 0, -W * 0.5, W * 0.5), lens),
              [M_WALL, M_LIGHT], smooth=None)
    expect(ob, size=(L, W, dz))


# ---------------------------------------------------------------- labyrinth (round 5)

def prism_xz(outline, y0, y1):
    """A closed vertical prism: `outline` is a list of Unity (x, z) points, extruded from height y0 to y1."""
    rings = [[(-x, -z, y) for x, z in outline] for y in (y0, y1)]
    bm = bmesh.new()
    lo = [bm.verts.new(p) for p in rings[0]]
    hi = [bm.verts.new(p) for p in rings[1]]
    n = len(lo)
    for k in range(n):
        bm.faces.new((lo[k], lo[(k + 1) % n], hi[(k + 1) % n], hi[k]))
    bm.faces.new(lo)
    bm.faces.new(list(reversed(hi)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def prism_zy(profile, x0, x1):
    """A closed prism running along Unity X from x0 to x1: `profile` is a list of Unity (z, y) points."""
    bm = bmesh.new()
    a = [bm.verts.new((-x0, -z, y)) for z, y in profile]
    b = [bm.verts.new((-x1, -z, y)) for z, y in profile]
    n = len(a)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def stadium(length, width, segments, cx=0.0, cy=0.0):
    """Outline of a capsule (two half-circles joined by straights) in a 2D plane, long axis along the first axis."""
    r = width * 0.5
    half = length * 0.5 - r
    pts = []
    for end, a0 in ((half, -90.0), (-half, 90.0)):
        for i in range(segments + 1):
            a = math.radians(a0 + 180.0 * i / segments)
            pts.append((cx + end + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def wall_profile():
    """The wall's cross-section with its shadow gaps, as (d, y) points and one material slot per edge.

    d runs across the thickness from the grid-line face (0) to the back face (-WALL); y is height. Edge i joins
    point i to point i + 1. Slots: 0 the wall, 1 the navy shadow gaps. Matches what wall_cuts() carves into the
    straight walls, so a rounded return lines up with its neighbours.
    """
    W, sh, sd, th, td, H = P.WALL, P.SKIRT_H, P.SKIRT_D, P.TOP_H, P.TOP_D, CLEAR_H
    pts = [(0, sh), (0, H - th), (-td, H - th), (-td, H), (-W + td, H), (-W + td, H - th), (-W, H - th),
           (-W, sh), (-W + sd, sh), (-W + sd, 0), (-sd, 0), (-sd, sh)]
    mats = [0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1]
    return pts, mats


def sweep_wall(path, front_slot=0):
    """A wall swept along `path`: a list of (x, z, nx, nz), a point on the grid-line face and the unit normal
    pointing into the wall's thickness. Capped at both ends, which are the snap faces."""
    pts, mats = wall_profile()
    mats = [front_slot if (m == 0 and i == 0) else m for i, m in enumerate(mats)]
    bm = bmesh.new()
    rings = []
    for x, z, nx, nz in path:
        ring = []
        for d, y in pts:
            ux, uz = x - d * nx, z - d * nz   # d is negative into the wall, so -d * n steps inward
            ring.append(bm.verts.new((-ux, -uz, y)))
        rings.append(ring)
    n = len(pts)
    for a, b in zip(rings, rings[1:]):
        for k in range(n):
            f = bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
            f.material_index = mats[k]
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def wall_return(finish=''):
    """A 2 x 2 m wall corner with a rounded outside, for the corners of maze islands. Box/hull colliders per part.

    The grid-line face runs from the +X snap end at (2, 0) to (R, 0), turns a quarter circle of radius R round
    (R, -R) and runs to the -Z snap end at (0, -2). Thickness goes inward, so a straight wall running +X from (2, 0)
    and a side wall running -Z from (0, -2) (pivot at its far end, turned -90) both meet it flush. The convex
    curve faces the corridor; the room behind gets the 0.8 m inner radius.
    '_Cobalt' paints the curved face cobalt, for the junction's coloured return (README_r5 corridors A).
    """
    R, L = P.RETURN_R, P.INFILL
    slot = 2 if finish == '_Cobalt' else 0
    mats = [M_WALL, M_TRIM] + ([M_COBALT] if slot else [])
    n_arc = P.ARC_SEGMENTS

    def arc_pt(t):   # t from 0 (pointing +Z, at (R, 0)) to 1 (pointing -X, at (0, -R))
        a = t * math.pi * 0.5
        return R - R * math.sin(a), -R + R * math.cos(a), math.sin(a), -math.cos(a)

    parts = []
    leg_a = sweep_wall([(L, 0, 0, -1), (R, 0, 0, -1)], slot)
    parts.append(('LegX', leg_a))
    per = n_arc // P.ARC_COLLIDERS
    for c in range(P.ARC_COLLIDERS):
        seg = [arc_pt((c * per + i) / n_arc) for i in range(per + 1)]
        parts.append((f'Arc{c + 1}', sweep_wall(seg, slot)))
    leg_b = sweep_wall([(0, -R, 1, 0), (0, -L, 1, 0)], slot)
    parts.append(('LegZ', leg_b))
    obs = []
    for name, bm in parts:
        ob = part(f'Kit_Wall_Return_2x2{finish}_{name}', bm, mats, smooth=None)
        collider(ob, 'box' if name.startswith('Leg') else 'hull')
        obs.append(ob)
    expect(obs[0], size=(L - R, P.WALL, CLEAR_H))
    expect(obs[-1], size=(P.WALL, L - R, CLEAR_H))
    group(f'Kit_Wall_Return_2x2{finish}', *obs)


def wall_niche():
    """4 m wall with a tall cobalt capsule niche centred on its grid-line face (README_r5 corridors A). Box collider.

    The niche is 0.06 deep, so the wall keeps 0.14 m behind it and the collider is the plain wall's.
    """
    w, eps = P.BAY, 0.01
    # The niche outline is (height, x); the cutter is that outline pushed through the face, NICHE_D deep.
    outline = stadium(P.NICHE_H, P.NICHE_W, P.COUNTER_SEGMENTS, cx=P.NICHE_BOTTOM + P.NICHE_H * 0.5, cy=w * 0.5)
    cut = bmesh.new()
    front = [cut.verts.new((-x, -eps, y)) for y, x in outline]
    back = [cut.verts.new((-x, P.NICHE_D, y)) for y, x in outline]
    n = len(front)
    for k in range(n):
        cut.faces.new((front[k], front[(k + 1) % n], back[(k + 1) % n], back[k]))
    cut.faces.new(front)
    cut.faces.new(list(reversed(back)))
    bmesh.ops.recalc_face_normals(cut, faces=cut.faces)
    body = recessed(u(0, w, 0, CLEAR_H, -P.WALL, 0), wall_cuts(0, w))
    body = boolean(body, paint(end_chamfers(0, w), 0))
    bm = boolean(body, paint(cut, 2))
    ob = part('Kit_Wall_Niche_4m', bm, [M_WALL, M_TRIM, M_COBALT], smooth=None)
    expect(ob, size=(w, P.WALL, CLEAR_H))
    collider(ob, 'box')


def counter_capsule():
    """Solid 1.1 m capsule counter, 2 x 1 m, for crouch cover (README_r5 rooms A). Pivot bottom centre. Hull collider."""
    bm = prism_xz(stadium(P.COUNTER_L, P.COUNTER_W, P.COUNTER_SEGMENTS), 0, P.COVER_H)
    bevel(bm, P.COUNTER_TOP_BEVEL, segments=3, angle=45)
    ob = part('Kit_Counter_Capsule_2m', bm, M_WALL, smooth=30)
    expect(ob, size=(P.COUNTER_L, P.COUNTER_W, P.COVER_H), tol=0.002)
    collider(ob, 'hull')


def cover_block():
    """Plain 1.1 m block cover, 2 x 0.6 m, bevelled (README_r5 junctions B, glass B). Pivot bottom centre. Box collider."""
    bm = u(-P.BLOCK_L * 0.5, P.BLOCK_L * 0.5, 0, P.COVER_H, -P.BLOCK_W * 0.5, P.BLOCK_W * 0.5)
    bevel(bm, P.BLOCK_BEVEL, segments=3, angle=45)
    ob = part('Kit_Cover_Block_2m', bm, M_WALL, smooth=30)
    expect(ob, size=(P.BLOCK_L, P.BLOCK_W, P.COVER_H), tol=0.002)
    collider(ob, 'box')


def partition_frost(width):
    """Glass partition with frosted strips: low (rail to 0.35 m) and high (2.1 m to the top rail), clear between.

    WHY these heights: the clear band 0.35-2.1 m keeps a standing figure readable through two panes (README_r5
    glass B); the frost says "boundary" without hiding anyone. Same rails, pivot and collider as Partition_Glass.
    """
    zc = -P.PART_T * 0.5
    g0, g1 = zc - P.GLASS_T * 0.5, zc + P.GLASS_T * 0.5
    rails = join(u(0, width, 0, P.PART_RAIL, -P.PART_T, 0),
                 u(0, width, P.PART_H - P.PART_RAIL, P.PART_H, -P.PART_T, 0))
    rails = join(rails, *mullion_grid(0, width, P.PART_RAIL, P.PART_H - P.PART_RAIL, zc, P.PART_T))   # 2026-11-14 grid
    frame = part(f'Kit_Partition_Frost_{width:g}m_Frame', rails, M_RAIL, smooth=None)
    clear = part(f'Kit_Partition_Frost_{width:g}m_Glass', u(0, width, P.FROST_LOW, P.FROST_HIGH, g0, g1),
                 M_GLASS, smooth=None)
    frost = part(f'Kit_Partition_Frost_{width:g}m_Frost',
                 join(u(0, width, P.PART_RAIL, P.FROST_LOW, g0, g1),
                      u(0, width, P.FROST_HIGH, P.PART_H - P.PART_RAIL, g0, g1)), M_FLUTED, smooth=None)   # fluted since 2026-11-15
    collider(frame, 'box')   # as Partition_Glass: one box over the whole wall; breakable glass is the shooting lane's
    expect(frame, size=(width, P.PART_T, P.PART_H))
    group(f'Kit_Partition_Frost_{width:g}m', frame, clear, frost)


def stair_straight(rise=None):
    """2 m-wide straight stair rising one storey toward -Z, with frameless glass guards (README_r5 atrium B).
    rise=FLOOR_H/2 makes Stair_Straight_Half_2m: 12 risers over 3.6 m, two of which with a landing between make the
    L2 layout's enclosed return stairs E1 and E2 (Alex approved L2, 2026-11-15).

    Pivot: the front-left corner of the bottom step, at floor level. 24 risers x 0.1875 m, 0.3 m goings, 7.2 m run;
    the top tread lands at 4.5 m, flush with the deck above. Hull collider: its top runs along the nosings, so the
    capsule climbs a smooth slope (32 degrees) and a foot sits at most one riser above the back of a tread.

    Built as two folded white stringers with separate white treads between them, each with a silver nosing, open
    underneath and between treads, as in the concept (2026-11-13 fix list: "the stair is one solid wedge").
    """
    rise = rise or P.FLOOR_H
    r, g, w = P.FLOOR_H / P.STAIR_RISERS, P.STAIR_GOING, P.STAIR_W
    n = round(P.STAIR_RISERS * rise / P.FLOOR_H)
    name = 'Kit_Stair_Straight_2m' if n == P.STAIR_RISERS else 'Kit_Stair_Straight_Half_2m'
    run = n * g
    under = 0.35   # depth of each stringer under the nosing line, metres
    prof = [(0.0, 0.0)]
    for k in range(n):
        prof += [(-k * g, (k + 1) * r), (-(k + 1) * g, (k + 1) * r)]
    prof += [(-run, rise - under), (-run + (rise - under) * g / r, 0.0)]   # underside parallel to the pitch
    prof = list(reversed(prof))
    st = P.STRINGER_T
    pieces = [paint(prism_zy(prof, 0, st), 0), paint(prism_zy(prof, w - st, w), 0)]
    for k in range(n):
        top = (k + 1) * r
        z_front, z_back = -k * g, -(k + 1) * g
        pieces.append(paint(u(st, w - st, top - P.TREAD_T2, top, z_back, z_front - P.NOSING_D), 0))
        pieces.append(paint(u(st, w - st, top - P.TREAD_T2, top, z_front - P.NOSING_D, z_front), 1))
    body = part(name, join(*pieces), [M_WALL, M_FRAME], smooth=None)
    expect(body, size=(w, run, rise))
    collider(body, 'hull')
    guards = []
    for side, x0 in (('L', 0.0), ('R', w - P.GUARD_T)):
        q = [(0.0, r), (-run, rise), (-run, rise + P.GUARD_H), (0.0, r + P.GUARD_H)]
        gb = prism_zy(list(reversed(q)), x0, x0 + P.GUARD_T)
        ob = part(f'{name}_Guard{side}', gb, M_GLASS, smooth=None)
        collider(ob, 'hull')
        guards.append(ob)
    group(f'{name}_Group', body, *guards)


def ramp():
    """2 m wide ramp rising 1 m over 4 m toward -Z (1:4), polished top (README_r5 atrium B). Hull collider."""
    bm = prism_zy([(0.0, 0.0), (-P.RAMP_L, 0.0), (-P.RAMP_L, P.RAMP_RISE)], 0, P.STAIR_W)
    bm.normal_update()
    for f in bm.faces:
        f.material_index = 1 if f.normal.z > 0.5 else 0
    ob = part('Kit_Ramp_2x4', bm, [M_WALL, M_FLOOR], smooth=None)
    expect(ob, size=(P.STAIR_W, P.RAMP_L, P.RAMP_RISE))
    collider(ob, 'hull')


def guard_glass(width):
    """Frameless glass guard for a mezzanine edge: glass standing in a low silver shoe on the slab edge.

    Runs +X from the pivot on the slab edge; sits just inside it (toward -Z). Box colliders on the shoe and the
    glass, so together they stop a player from the floor to the guard's top.
    """
    st, sh = P.GUARD_SHOE_T, P.GUARD_SHOE_H
    shoe = part(f'Kit_Guard_Glass_{width:g}m_Shoe', u(0, width, 0, sh, -st, 0), M_FRAME, smooth=None)
    zc = -st * 0.5
    glass = part(f'Kit_Guard_Glass_{width:g}m_Glass',
                 u(0, width, sh, P.GUARD_H, zc - P.GUARD_T * 0.5, zc + P.GUARD_T * 0.5), M_GLASS, smooth=None)
    for ob in (shoe, glass):
        collider(ob, 'box')
    expect(glass, size=(width, P.GUARD_T, P.GUARD_H - sh))
    group(f'Kit_Guard_Glass_{width:g}m', shoe, glass)


def slab_light_line():
    """4 x 4 m slab with a lit slot along X down the middle of its soffit: a ceiling light line that carries on
    across the next slab and points along a route (README_r5). Box collider, as the plain slab."""
    w = d = P.BAY
    eps = 0.01
    body = by_normal(u(0, w, -P.SLAB, 0, -d, 0), 0, 1, 2)
    slot = paint(u(-eps, w + eps, -P.SLAB - eps, -P.SLAB + P.COVE_D, -d * 0.5 - P.COVE_W * 0.5,
                   -d * 0.5 + P.COVE_W * 0.5), 3)
    ob = part('Kit_Slab_4x4_LightLine', boolean(body, slot), [M_FLOOR, M_CEIL, M_WALL, M_LIGHT], smooth=None)
    expect(ob, size=(w, d, P.SLAB))
    collider(ob, 'box')


# ---------------------------------------------------------------- fix list 2026-11-13: signs and props

ZONES = {   # letter: (Japanese, English, microtype). Sign copy checked by Codex (concepts/Japanese_Aero/README.md)
    'L': ('法務部', 'LEGAL', '青凪総合サービス株式会社  /  法務部  /  ZONE L  /  1F'),
    'R': ('審査部', 'REVIEW', '青凪総合サービス株式会社  /  審査部  /  ZONE R  /  1F'),
    'C': ('請求部', 'CLAIMS', '青凪総合サービス株式会社  /  請求部  /  ZONE C  /  1F'),
    'H': ('審問室', 'HEARINGS', '青凪総合サービス株式会社  /  審問室  /  ZONE H  /  1F-2F'),
    'A': ('記録保管室', 'ARCHIVE', '青凪総合サービス株式会社  /  記録保管室  /  ZONE A  /  1F'),
}
COMPANY_JA, COMPANY_EN = '青凪総合サービス', 'AONAGI CORPORATE SERVICES'   # fictional (Japanese_Aero/README.md)


def cjk_font():
    """Blender's bundled Noto Sans CJK (SIL Open Font License): Japanese and Latin glyphs in one face.

    WHY this font: it ships with Blender, so every machine that builds the kit has it, and its licence allows
    shipping the glyphs as meshes. The Windows Japanese fonts (Yu Gothic, MS Gothic) do not allow that. Returns
    None, falling back to the built-in Latin font, if this Blender has no CJK font.
    """
    import bpy, os
    path = os.path.join(bpy.utils.system_resource('DATAFILES'), 'fonts', 'Noto Sans CJK Regular.woff2')
    if not os.path.exists(path):
        return None
    return bpy.data.fonts.load(path, check_existing=True)


# Cap height of Noto Sans CJK as Blender loads it, as a share of the curve's size. Measured 2026-11-13 ('H' at size 1
# is 0.2567 tall). WHY measured: the kit used 0.72, the usual cap/em ratio, but Blender scales this woff2 smaller, so
# every label came out at 36 % of its stated height. That was most of why the signs read small and quiet.
CAP_RATIO = 0.2567


def text_mesh(body, height, depth, weight=0.0, slant=0.0):
    """Flat 3D text as a bmesh: Blender's bundled Noto Sans CJK, capital height `height` metres, extruded `depth`.

    `weight` (metres) thickens every stroke outward, so the regular face reads bold; the cap height still comes out
    at `height`. `slant` shears it forward (0.2 = 0.2 m sideways per metre up), the leaning italic of wayfinding
    supergraphics. Laid in Blender's XY plane facing +Z, its baseline-left at the origin. Uses a temporary font
    curve evaluated through the depsgraph (bpy.data only, no operators) and removes it again.
    """
    import bpy
    cu = bpy.data.curves.new('tmp_text', 'FONT')
    cu.body = body
    font = cjk_font()
    if font is not None:
        cu.font = font
    # WHY minus twice the weight: the outline offset adds `weight` above the caps and below the baseline.
    cu.size = max(height - 2.0 * weight, 0.001) / CAP_RATIO
    cu.offset = weight
    cu.shear = slant
    cu.extrude = depth * 0.5
    cu.resolution_u = P.FONT_RES
    ob = bpy.data.objects.new('tmp_text', cu)
    bpy.context.scene.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    me = ob.evaluated_get(dg).to_mesh()
    bm = bmesh.new()
    bm.from_mesh(me)
    ob.evaluated_get(dg).to_mesh_clear()
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    # WHY: some CJK glyphs (自, 販, 履...) have overlapping or hairline outline pieces that fill into zero-area
    # or inside-out faces; dissolving the degenerate ones and recalculating normals per glyph island fixes that.
    bmesh.ops.dissolve_degenerate(bm, dist=1e-4, edges=bm.edges)   # 0.1 mm: below anything visible at 4 cm type
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if weight:
        bmesh.ops.translate(bm, verts=bm.verts, vec=(0.0, weight, 0.0))   # baseline back on y = 0
    return bm


# Modelled text is retired (Alex, 2026-11-15: signage "dont have it modelled, either textured or a decal"). Every label
# is now a texture from ArtSource/Kit/signs/make_signs.py placed by Assets/_Project/Editor/Art/KitSigns.cs, so
# placed_text draws nothing. True brings the old mesh text back for comparison only.
MESH_TEXT = False


def placed_text(body, height, x_right, y_base, z_face, weight=0.0, slant=0.0, max_width=None):
    """Text standing on a wall-facing panel: reads left to right for someone facing the panel from +Z.

    WHY x_right: facing the panel from Unity +Z the reader's right is Unity -X, so the line starts at the
    panel's high-X edge and runs toward x = 0. z_face is the Unity z of the surface the letters stand on.
    `max_width` (metres) condenses a line that would run longer, keeping its height: a squeezed line still reads,
    one that runs off its panel does not.
    """
    if not MESH_TEXT:
        return bmesh.new()   # retired: the label is a texture now (ArtSource/Kit/signs, KitSigns.cs)
    bm = text_mesh(body, height, P.INK_T, weight, slant)
    if max_width and bm.verts:
        xs = [v.co.x for v in bm.verts]
        x_min, width = min(xs), max(xs) - min(xs)
        if width > max_width:
            k = max_width / width
            for v in bm.verts:
                v.co.x = x_min + (v.co.x - x_min) * k
    rotate(bm, 90.0, axis='X')                     # face Blender -Y (Unity +Z), up stays up
    move(bm, -x_right, -(z_face + P.INK_T * 0.5), y_base)
    return bm


# Zone supergraphic layout, metres (Alex 2026-11-13: lettering "larger and more character ... think Mirror's Edge").
SUPER_LETTER_H = 2.6      # the zone letter's capital height: most of a storey, read from across the atrium
SUPER_LETTER_Y = 1.25     # its baseline, above the department name
SUPER_WEIGHT = 0.07       # stroke thickening on the letter: a heavy display weight from the regular face
SUPER_SLANT = 0.18        # forward lean: wayfinding that points along the route instead of standing still
SUPER_JA_H, SUPER_JA_Y = 0.46, 0.5     # Japanese department name: cap height and baseline
SUPER_EN_H, SUPER_EN_Y = 0.17, 0.2     # English name under it
SUPER_BLOCK = (1.0, 2.45)              # aqua colour block behind the letter's lower half: bottom and top
SUPER_BLOCK_W = 1.9                    # its width from the reader's left edge
SUPER_STRIPE = (0.92, 1.02)            # the cobalt speed stripe that runs the whole width: bottom and top
SUPER_TAG = (0.62, 0.32)               # navy floor tag beside the department name: width and height
SUPER_PAINT = 0.003                    # colour blocks stand this far proud of the wall; the ink sits on them


def sign_zone(letter):
    """A zone supergraphic painted straight onto the wall, after Alex's "think Mirror's Edge": a storey-high slanted
    zone letter in cobalt over an aqua colour block, a speed stripe running the full width, the department name
    big in Japanese with English under it, and a navy floor tag beside the name. No panel: the wall is the
    sign, which makes it architecture rather than an office notice. Pivot at floor level at the low-X edge on
    the wall face; under 1 cm proud toward +Z. Visual only: no collider.
    """
    ja, en, micro = ZONES[letter]
    floor = micro.split('/')[-1].strip()
    W, pad, k = P.SIGN_W, P.SIGN_PAD, SUPER_SLANT
    # The reader's left is high X (placed_text); a forward lean moves a shape's top toward -X, as the text's does.
    b0, b1 = SUPER_BLOCK
    bl = (b1 - b0) * k
    block = prism_xy([(W, b0), (W - SUPER_BLOCK_W, b0), (W - SUPER_BLOCK_W - bl, b1), (W - bl, b1)], 0, SUPER_PAINT)
    s0, s1 = SUPER_STRIPE
    sl = (s1 - s0) * k
    stripe = prism_xy([(W, s0), (0.15, s0), (0.15 - sl, s1), (W - sl, s1)], 0, SUPER_PAINT)
    tw, th = SUPER_TAG
    ty = SUPER_JA_Y   # beside the department name, clear of the leaning letter's top
    tag = u(0.1, 0.1 + tw, ty, ty + th, 0, SUPER_PAINT)
    big = placed_text(letter, SUPER_LETTER_H, W - pad, SUPER_LETTER_Y, SUPER_PAINT, SUPER_WEIGHT, k,
                      max_width=W - 2 * pad - SUPER_LETTER_H * k)
    name = placed_text(ja, SUPER_JA_H, W - pad, SUPER_JA_Y, SUPER_PAINT, 0.008, k, max_width=W - 2 * pad - tw - 0.1)
    sub = placed_text(en, SUPER_EN_H, W - pad, SUPER_EN_Y, SUPER_PAINT, 0.008, k, max_width=W - 2 * pad)
    tag_ink = placed_text(floor, th * 0.5, 0.1 + tw - 0.07, ty + th * 0.25, SUPER_PAINT, 0.008, k, max_width=tw - 0.12)
    ob = part(f'Kit_Sign_Zone_{letter}', join(paint(block, 0), paint(stripe, 1), paint(big, 1), paint(name, 2),
                                               paint(sub, 1), paint(tag, 2), paint(tag_ink, 3)),
              [M_AQUA, M_COBALT, M_TRIM, M_WALL], smooth=None)
    return ob


def desk_sculpt():
    """A sculptural waterfall desk: a gloss white top folding down into two side slabs, with a recessed cobalt
    modesty panel (fix list 2026-11-13: scale cues). Pivot bottom centre, top surface at 0.74 m. Box collider.
    """
    W, D, H, t, s = P.DESK_W, P.DESK_D, P.DESK_H, P.DESK_TOP, P.DESK_SIDE
    white = join(u(-W / 2, W / 2, H - t, H, -D / 2, D / 2),
                 u(-W / 2, -W / 2 + s, 0, H - t, -D / 2, D / 2),
                 u(W / 2 - s, W / 2, 0, H - t, -D / 2, D / 2))
    bevel(white, P.DESK_BEVEL, segments=2, angle=45)
    modesty = u(-W / 2 + s, W / 2 - s, 0.25, H - t, D / 2 - 0.12, D / 2 - 0.10)   # 0.1 m in from the back edge
    ob = part('Kit_Desk_Sculpt', join(paint(white, 0), paint(modesty, 1)), [M_WALL, M_COBALT], smooth=30)
    expect(ob, size=(W, D, H), tol=0.002)
    collider(ob, 'box')


def chair_pod():
    """A pod chair: a cobalt shell seat and back on a silver swivel stem and disc base. Pivot bottom centre,
    facing +Z. Hull collider (a loose prop for the props lane to make physical)."""
    sw, sd, sh, t = P.CHAIR_W, P.CHAIR_D, P.CHAIR_SEAT_H, P.CHAIR_SHELL
    seat = u(-sw / 2, sw / 2, sh - t, sh, -sd / 2, sd / 2)
    back = u(-sw / 2, sw / 2, sh - t, P.CHAIR_BACK_H, -sd / 2, -sd / 2 + t)
    shell = join(seat, back)
    bevel(shell, 0.02, segments=3, angle=45)
    stem = cylinder(P.CHAIR_STEM_R, sh - t - 0.03, segments=12, base='bottom')
    move(stem, 0, 0, 0.03)
    base = cylinder(P.CHAIR_BASE_R, 0.03, segments=24, base='bottom')
    ob = part('Kit_Chair_Pod', join(paint(shell, 0), paint(join(stem, base), 1)), [M_COBALT, M_FRAME], smooth=30)
    collider(ob, 'hull')


# ---------------------------------------------------------------- detail pass 2026-11-13 (Alex's reference plates)

def prism_xy(outline, z0, z1):
    """A closed prism standing on a wall-like plane: `outline` is Unity (x, y) points, extruded from z0 to z1."""
    bm = bmesh.new()
    a = [bm.verts.new((-x, -z0, y)) for x, y in outline]
    b = [bm.verts.new((-x, -z1, y)) for x, y in outline]
    n = len(a)
    for k in range(n):
        bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def sweep_box(path, d0, d1, y0, y1):
    """A rectangular section (d0-d1 into the thickness, y0-y1 high) swept along a path of (x, z, nx, nz)."""
    rings = []
    for x, z, nx, nz in path:
        rings.append([(-(x + d * nx), -(z + d * nz), y) for d, y in ((d0, y0), (d1, y0), (d1, y1), (d0, y1))])
    return sweep(rings)


def column_graphic(height, number, lines):
    """A monumental square column wrapped in a cobalt supergraphic band that climbs round its corner, with a
    big zone number and wayfinding lines in navy (the reference plates' "32 / PARTNERS ^" columns).
    Pivot bottom centre. Box collider. Stand it where it hides a wall end or marks a junction.
    """
    w = P.COL_W
    h = w * 0.5
    f = h - P.COL_R   # the flat half-width of each face, inside the rounded corners
    s = P.COL_SKIN
    body = u(-h, h, 0, height, -h, h)
    bevel(body, P.COL_R, segments=3, angle=45)
    tall = height > P.FLOOR_H
    # WHY these: the band starts above the wayfinding text (top at ~1.5 m) and stays under the soffit; it is as
    # large as that allows, because the plates' columns are mostly cobalt graphic, not a stripe.
    y0, band, rise = (2.6, 2.8, height * 0.3) if tall else (1.7, 1.5, height * 0.2)
    front = prism_xy([(-f, y0), (f, y0 + rise * 0.5), (f, y0 + rise * 0.5 + band), (-f, y0 + band)], h, h + s)
    side = prism_zy([(f, y0 + rise * 0.5), (-f, y0 + rise), (-f, y0 + rise + band), (f, y0 + rise * 0.5 + band)], h, h + s)
    # The mix: a thin cobalt stripe along the band's lower edge keeps the old identity colour on the aqua.
    stripe = prism_xy([(-f, y0 - 0.1), (f, y0 + rise * 0.5 - 0.1), (f, y0 + rise * 0.5 - 0.04), (-f, y0 - 0.04)], h, h + s)
    ink = [placed_text(number, P.COL_NUMBER_H, f - 0.04, 1.05, h, 0.03, SUPER_SLANT, max_width=2 * f - 0.08)]
    for i, line in enumerate(lines):
        ink.append(placed_text(line, P.COL_TEXT_H, f - 0.04, 0.85 - i * 0.11, h, max_width=2 * f - 0.08))
    ob = part('Kit_Column_Graphic' + ('_Tall' if tall else ''),
              join(paint(body, 0), paint(front, 1), paint(side, 3), paint(stripe, 4), paint(join(*ink), 2)),
              [M_WALL, M_AQUA, M_TRIM, M_MIST, M_COBALT], smooth=30)
    collider(ob, 'box')


def workstation_bench():
    """Back-to-back workstation bench: two 1.2 m desks a side, a 1.15 m divider (crouch-cover height), four
    monitors, keyboards and two drawer pedestals; silver end panels. Pivot bottom centre. Box collider.
    """
    L, D, H = P.BENCH_L, P.BENCH_D, P.DESK_H
    white = [u(-L / 2, L / 2, H - 0.03, H, -D / 2, -0.02), u(-L / 2, L / 2, H - 0.03, H, 0.02, D / 2)]
    frost = u(-L / 2 + 0.04, L / 2 - 0.04, H, P.SCREEN_TOP, -0.015, 0.015)   # aqua pearl divider (the mix)
    silver = [u(-L / 2, -L / 2 + 0.04, 0, H - 0.03, -D / 2 + 0.05, D / 2 - 0.05),
              u(L / 2 - 0.04, L / 2, 0, H - 0.03, -D / 2 + 0.05, D / 2 - 0.05)]
    # Monitors and keyboards are separate loose pieces (Monitor_Flat, Keyboard) since 2026-11-13, so they can be
    # picked up and thrown (Alex: "pick up ANY loose object"). KitOfficeScene puts them back on the desk.
    for side in (1, -1):
        xc = side * 0.85
        z_out = side * (D / 2 - 0.02)
        white.append(u(xc - P.PED_W / 2, xc + P.PED_W / 2, 0.02, P.PED_H, min(z_out, z_out - side * P.PED_D),
                       max(z_out, z_out - side * P.PED_D)))
    w = join(*white)
    bevel(w, 0.006, segments=2, angle=45)
    ob = part('Kit_Workstation_Bench', join(paint(w, 0), paint(join(*silver), 1), paint(frost, 2)),
              [M_WALL, M_FRAME, M_FROST], smooth=30)
    expect(ob, size=(L, D, P.SCREEN_TOP), tol=0.002)
    collider(ob, 'box')


def chair_task():
    """An office task chair: navy seat and back on a silver gas stem and five-star base. Faces +Z.
    Pivot bottom centre. Hull collider (a loose prop for the props lane)."""
    seat = u(-0.24, 0.24, P.TASK_SEAT - 0.07, P.TASK_SEAT, -0.23, 0.23)
    back = u(-0.23, 0.23, P.TASK_SEAT + 0.05, P.TASK_SEAT + 0.6, -0.26, -0.2)
    shell = join(seat, back)
    bevel(shell, 0.02, segments=3, angle=45)
    stem = cylinder(0.025, P.TASK_SEAT - 0.15, segments=10, base='bottom')
    move(stem, 0, 0, 0.08)
    legs = []
    for k in range(5):
        leg = u(0, 0.32, 0.05, 0.08, -0.02, 0.02)
        rotate(leg, 72.0 * k, axis='Z')
        legs.append(leg)
    ob = part('Kit_Chair_Task', join(paint(shell, 0), paint(join(stem, *legs), 1)), [M_TRIM, M_FRAME], smooth=30)
    collider(ob, 'hull')


def storage_low():
    """A 1 m-high run of white storage cabinets, four doors with navy shadow joints. Front faces +Z.
    Pivot bottom centre. Box collider (crouch-height cover)."""
    L, D, H = P.STORE_L, P.STORE_D, P.STORE_H
    body = u(-L / 2, L / 2, 0, H, -D / 2, D / 2)
    bevel(body, 0.01, segments=2, angle=45)
    joints = join(*[u(x - 0.003, x + 0.003, 0.05, H - 0.05, D / 2 - 0.006, D / 2 + 0.01) for x in (-L / 4, 0.0, L / 4)])
    ob = part('Kit_Storage_Low', recessed(body, joints), [M_WALL, M_TRIM], smooth=30)
    expect(ob, size=(L, D, H), tol=0.002)
    collider(ob, 'box')


def copier():
    """The photocopier the art bible asks for: a pale body with paper-tray joints, a dark lid and a silver
    control panel. Front faces +Z. Pivot bottom centre. Box collider."""
    W, D, H = P.COPIER_W, P.COPIER_D, P.COPIER_H
    body_h = H - 0.1
    body = u(-W / 2, W / 2, 0, body_h, -D / 2, D / 2)
    bevel(body, 0.015, segments=2, angle=45)
    trays = join(*[u(-W / 2 + 0.03, W / 2 - 0.03, y - 0.003, y + 0.003, D / 2 - 0.006, D / 2 + 0.01) for y in (0.22, 0.42, 0.62)])
    body = recessed(body, trays)
    lid = u(-W / 2 + 0.02, W / 2 - 0.02, body_h, H, -D / 2 + 0.02, D / 2 - 0.16)
    panel = u(-W / 2 + 0.05, W / 2 - 0.05, body_h, body_h + 0.05, D / 2 - 0.15, D / 2 - 0.02)
    ob = part('Kit_Copier', join(body, paint(lid, 1), paint(panel, 2)), [M_WALL, M_TRIM, M_FRAME], smooth=30)
    collider(ob, 'box')


def ring_logo(cx, cy, z_face, diameter):
    """The AONAGI mark: an open aqua ring with a red hanko dot in its lower-right gap (Japanese_Aero/README.md).
    Returns (ring, dot) bmeshes standing on a face at z_face, centred at Unity (cx, cy)."""
    r_out, stroke = diameter / 2, diameter * 0.07
    pts_o, pts_i = [], []
    a0, a1, n = math.radians(-60), math.radians(260), 40   # open between -100 and -60 degrees (lower right)
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        pts_o.append((cx + r_out * math.cos(a), cy + r_out * math.sin(a)))
        pts_i.append((cx + (r_out - stroke) * math.cos(a), cy + (r_out - stroke) * math.sin(a)))
    ring = prism_xy(pts_o + list(reversed(pts_i)), z_face, z_face + P.INK_T)
    da = math.radians(-80)
    dot_c = (cx + (r_out - stroke / 2) * math.cos(da), cy + (r_out - stroke / 2) * math.sin(da))
    dot = prism_xy([(dot_c[0] + diameter * 0.065 * math.cos(2 * math.pi * k / 16), dot_c[1] + diameter * 0.065 * math.sin(2 * math.pi * k / 16))
                    for k in range(16)], z_face, z_face + P.INK_T)
    return ring, dot


def desk_reception():
    """The AONAGI reception desk: pearl body with the company name in Japanese and English, the ring logo with
    its red hanko dot, and an aqua end block (the Japanese corporate mix). Front faces +Z. Pivot bottom centre.
    Box collider."""
    L, D, H = P.RECEPTION_L, P.RECEPTION_D, P.RECEPTION_H
    end = 0.5   # aqua end block length, metres
    body = u(-L / 2, L / 2 - end, 0, H, -D / 2, D / 2)
    bevel(body, 0.012, segments=2, angle=45)
    block = u(L / 2 - end, L / 2, 0, H, -D / 2, D / 2)
    bevel(block, 0.012, segments=2, angle=45)
    x0 = L / 2 - end - 0.75
    ink = join(placed_text(COMPANY_JA, 0.14, x0, 0.6, D / 2), placed_text(COMPANY_EN, 0.035, x0, 0.47, D / 2))
    ring, dot = ring_logo(x0 + 0.33, 0.62, D / 2, 0.36)
    ob = part('Kit_Desk_Reception', join(paint(body, 0), paint(block, 1), paint(ring, 1), paint(ink, 2), paint(dot, 3)),
              [M_WALL, M_AQUA, M_TRIM, M_SEAL], smooth=30)
    collider(ob, 'box')


def guard_rail(width):
    """Mezzanine balustrade as in the plates: clear glass between slim silver posts, with a silver handrail at
    1.1 m. Runs +X from the pivot on the slab edge, sitting just inside it (-Z). Box collider over the frame."""
    t, pr = P.RAIL_POST, P.RAIL_TOP
    posts = [u(x - t / 2, x + t / 2, 0, P.GUARD_H, -0.05, -0.01) for x in (t / 2 + 0.02, width / 2, width - t / 2 - 0.02)]
    rail = u(0, width, P.GUARD_H - 0.01, P.GUARD_H - 0.01 + pr, -0.055, -0.005)
    frame = part(f'Kit_Guard_Rail_{width:g}m_Frame', join(*posts, rail), M_FRAME, smooth=None)
    glass = part(f'Kit_Guard_Rail_{width:g}m_Glass', u(0.02, width - 0.02, 0.04, P.GUARD_H - 0.01, -0.04, -0.02),
                 M_GLASS, smooth=None)
    collider(frame, 'box')
    group(f'Kit_Guard_Rail_{width:g}m', frame, glass)


def partition_header(width):
    """A glass meeting-room wall with a solid white header band from the 2.5 m top rail to the soffit, as in
    Alex's curved meeting room concept. Same rails and pivot as Partition_Glass. Box collider, full height."""
    zc = -P.PART_T * 0.5
    frame = join(paint(u(0, width, 0, P.PART_RAIL, -P.PART_T, 0), 1),
                 paint(u(0, width, P.PART_H - P.PART_RAIL, P.PART_H, -P.PART_T, 0), 1),
                 paint(u(0, width, P.PART_H, CLEAR_H, -P.PART_T, 0), 0))
    # The grid stops under the header band, so no mullion face lies on the band's faces (2026-11-14).
    frame = join(frame, paint(join(*mullion_grid(0, width, P.PART_RAIL, P.PART_H - P.PART_RAIL, zc, P.PART_T)), 1))
    fr = part(f'Kit_Partition_Header_{width:g}m_Frame', frame, [M_WALL, M_RAIL], smooth=None)
    glass = part(f'Kit_Partition_Header_{width:g}m_Glass',
                 u(0, width, P.PART_RAIL, P.PART_H - P.PART_RAIL, zc - P.GLASS_T / 2, zc + P.GLASS_T / 2), M_GLASS, smooth=None)
    collider(fr, 'box')
    expect(fr, size=(width, P.PART_T, CLEAR_H))
    group(f'Kit_Partition_Header_{width:g}m', fr, glass)


def partition_header_curve():
    """The curved corner of a header-band glass room in a 2 x 2 m cell (Alex's concept, version A): glass bent
    round a 1.5 m radius, navy rails, white header band to the soffit. Snap ends as Wall_Return_2x2 (running +X
    from (2, 0) and -Z from (0, -2)); the convex side faces outward. Box/hull colliders per segment.
    """
    R, L, T = P.HEADER_R, P.INFILL, P.PART_T
    n = P.ARC_SEGMENTS

    def arc_pt(t):
        a = t * math.pi * 0.5
        return R - R * math.sin(a), -R + R * math.cos(a), math.sin(a), -math.cos(a)

    per = n // P.ARC_COLLIDERS
    segs = [('LegX', [(L, 0, 0, -1), (R, 0, 0, -1)])]
    segs += [(f'Arc{c + 1}', [arc_pt((c * per + i) / n) for i in range(per + 1)]) for c in range(P.ARC_COLLIDERS)]
    segs.append(('LegZ', [(0, -R, 1, 0), (0, -L, 1, 0)]))
    parts = []
    for name, path in segs:
        frame = join(paint(sweep_box(path, 0, T, 0, P.PART_RAIL), 1),
                     paint(sweep_box(path, 0, T, P.PART_H - P.PART_RAIL, P.PART_H), 1),
                     paint(sweep_box(path, 0, T, P.PART_H, CLEAR_H), 0))
        ob = part(f'Kit_Partition_Header_Curve_2x2_{name}', frame, [M_WALL, M_RAIL], smooth=30)
        collider(ob, 'box' if name.startswith('Leg') else 'hull')
        parts.append(ob)
    full = [(L, 0, 0, -1)] + [arc_pt(i / n) for i in range(n + 1)] + [(0, -L, 1, 0)]
    g0, g1 = T / 2 - P.GLASS_T / 2, T / 2 + P.GLASS_T / 2
    glass = part('Kit_Partition_Header_Curve_2x2_Glass', sweep_box(full, g0, g1, P.PART_RAIL, P.PART_H - P.PART_RAIL),
                 M_GLASS, smooth=30)
    group('Kit_Partition_Header_Curve_2x2', *parts, glass)


def light_pendant():
    """A linear pendant light: a slim white bar with a glowing underside, hung 0.8 m below the soffit on two
    wires. Pivot top centre, against the soffit. Visual only: no collider."""
    L, W, H, drop = P.PENDANT_L, P.PENDANT_W, P.PENDANT_H, P.PENDANT_DROP
    eps = 0.005
    bar = recessed(u(-L / 2, L / 2, -drop - H, -drop, -W / 2, W / 2),
                   u(-L / 2 + 0.02, L / 2 - 0.02, -drop - H - eps, -drop - H + 0.01, -W / 2 + 0.012, W / 2 - 0.012))
    wires = join(*[u(x - 0.003, x + 0.003, -drop, 0, -0.003, 0.003) for x in (-L / 2 + 0.25, L / 2 - 0.25)])
    ob = part('Kit_Light_Pendant_Linear', join(bar, paint(wires, 2)), [M_WALL, M_LIGHT, M_FRAME], smooth=None)
    expect(ob, size=(L, W, drop + H), tol=0.002)


def carpet_overlay():
    """A 4 x 4 m dark carpet layer, 8 mm thick, laid on a slab's polished top to mark a work area. Pivot as
    the slab's (front-left, floor level). Visual only: no collider (8 mm is below anything a capsule notices)."""
    bm = u(0, P.BAY, 0, P.OVERLAY_T, -P.BAY, 0)
    bevel(bm, 0.003, segments=1, angle=45)
    ob = part('Kit_Carpet_Overlay_4x4', bm, M_CARPET, smooth=None)
    expect(ob, size=(P.BAY, P.BAY, P.OVERLAY_T), tol=0.001)


def ceiling_grid():
    """A 4 x 4 m tiled ceiling panel hung flush under a slab's soffit over a work area (the plates' tile grid).
    Pivot at its top front-left corner: place it at the soffit. Visual only: no collider."""
    ob = part('Kit_Ceiling_Grid_4x4', u(0, P.BAY, -P.OVERLAY_T, 0, -P.BAY, 0), M_GRID, smooth=None)
    expect(ob, size=(P.BAY, P.BAY, P.OVERLAY_T))


def planter_box():
    """A white box planter with simple leafy blades, as in the plates (Alex's call, against the bible's
    no-plants rule). Pivot bottom centre. Box collider on the planter only; the leaves are visual."""
    import random
    L, W, H = P.PLANTER_L, P.PLANTER_W, P.PLANTER_H
    pot = u(-L / 2, L / 2, 0, H, -W / 2, W / 2)
    bevel(pot, 0.02, segments=2, angle=45)
    pot = recessed(pot, u(-L / 2 + 0.04, L / 2 - 0.04, H - 0.04, H + 0.01, -W / 2 + 0.04, W / 2 - 0.04))
    pot_ob = part('Kit_Planter_Box_Pot', pot, [M_WALL, M_TRIM], smooth=30)
    collider(pot_ob, 'box')
    rng = random.Random(3)   # fixed seed: the same planter every build
    leaves = []
    for k in range(P.LEAVES):
        size = rng.uniform(0.45, 0.75)
        leaf = prism_xy([(0, 0), (0.07, size * 0.5), (0, size), (-0.07, size * 0.5)], -0.003, 0.003)
        rotate(leaf, rng.uniform(15, 40), axis='X')
        rotate(leaf, 360.0 * k / P.LEAVES + rng.uniform(-12, 12), axis='Z')
        move(leaf, -rng.uniform(-L / 2 + 0.12, L / 2 - 0.12), -rng.uniform(-W / 2 + 0.08, W / 2 - 0.08), H - 0.04)
        leaves.append(leaf)
    leaf_ob = part('Kit_Planter_Box_Leaves', join(*leaves), M_LEAF, smooth=30)
    group('Kit_Planter_Box', pot_ob, leaf_ob)


# ---------------------------------------------------------------- Japanese office props (the mix)

def water_server():
    """A floor water server: white body, silver tap panel, an aqua water bottle on top, and its 給水 label.
    Faces +Z. Pivot bottom centre. Box collider."""
    w, H = P.SERVER_W, P.SERVER_H
    body_h = H - 0.38
    body = u(-w / 2, w / 2, 0, body_h, -w / 2, w / 2)
    bevel(body, 0.02, segments=2, angle=45)
    panel = u(-0.1, 0.1, 0.75, 0.95, w / 2, w / 2 + 0.01)
    bottle = cylinder(0.14, 0.36, segments=20, base='bottom')
    move(bottle, 0, 0, body_h)
    label = placed_text('給水', 0.05, 0.08, 1.0, w / 2)
    ob = part('Kit_Water_Server', join(paint(body, 0), paint(panel, 1), paint(bottle, 2), paint(label, 3)),
              [M_WALL, M_FRAME, M_WATER, M_TRIM], smooth=30)
    collider(ob, 'box')


def umbrella_stand():
    """A silver umbrella stand with three furled umbrellas (navy, aqua, white). Pivot bottom centre. Hull collider."""
    r, H = P.UMBRELLA_D / 2, P.UMBRELLA_H
    can = cylinder(r, H, segments=20, base='bottom')
    brollies = []
    for k, (dx, dz, lean) in enumerate(((-0.04, 0.03, 6), (0.05, 0.0, -5), (0.0, -0.05, 3))):
        b = cone(0.03, 0.012, 0.9, segments=10, base='bottom')
        rotate(b, lean, axis='X')
        move(b, -dx, -dz, 0.05)
        brollies.append(paint(b, k + 1))
    ob = part('Kit_Umbrella_Stand', join(paint(can, 0), *brollies), [M_FRAME, M_TRIM, M_AQUA, M_WALL], smooth=30)
    collider(ob, 'hull')


def vending_machine():
    """A drinks vending machine: white and silver body, a glass display of drink rows (aqua, white, green, navy),
    a glowing header panel and the 自動販売機 label. Front faces +Z. Pivot bottom centre. Box collider.
    Stand it against an opaque wall: at 1.8 m it is full cover."""
    W, D, H = P.VEND_W, P.VEND_D, P.VEND_H
    # WHY panels, not a boolean: cutting the window out of the body flipped faces in the exact solver. The body is
    # a back block plus a front frame round the window opening.
    xl, xr, yb, yt, zf = -W / 2 + 0.06, W / 2 - 0.22, 0.75, H - 0.25, D / 2 - 0.12
    body = join(u(-W / 2, W / 2, 0, H, -D / 2, zf),
                u(-W / 2, W / 2, 0, yb, zf, D / 2), u(-W / 2, W / 2, yt, H, zf, D / 2),
                u(-W / 2, xl, yb, yt, zf, D / 2), u(xr, W / 2, yb, yt, zf, D / 2))
    glass = u(-W / 2 + 0.06, W / 2 - 0.22, 0.75, H - 0.25, D / 2 - 0.02, D / 2 - 0.01)
    header = u(-W / 2 + 0.06, W / 2 - 0.06, H - 0.22, H - 0.06, D / 2, D / 2 + 0.01)
    drinks, mats = [], (3, 0, 4, 5)
    for row, y in enumerate((0.8, 1.05, 1.3)):
        for i in range(6):
            c = cylinder(0.03, 0.17, segments=10, base='bottom')
            move(c, -(-W / 2 + 0.13 + i * 0.095), -(D / 2 - 0.07), y)
            drinks.append(paint(c, mats[(i + row) % 4]))
    slot = u(W / 2 - 0.18, W / 2 - 0.08, 0.9, 1.3, D / 2, D / 2 + 0.01)
    label = placed_text('自動販売機', 0.05, W / 2 - 0.1, H - 0.19, D / 2 + 0.01)
    ob = part('Kit_Vending_Machine', join(paint(body, 0), paint(glass, 6), paint(header, 2), paint(slot, 1), *drinks, paint(label, 5)),
              [M_WALL, M_FRAME, M_LIGHT, M_AQUA, M_LEAF, M_TRIM, M_GLASS], smooth=30)
    collider(ob, 'box')


def slipper_cabinet():
    """A low white slipper cabinet with open cubbies and its 室内履き label. Front faces +Z. Pivot bottom centre.
    Box collider."""
    W, D, H = P.SLIPPER_W, P.SLIPPER_D, P.SLIPPER_H
    # WHY a carcass, not cut cubbies: the cubby booleans left zero-area faces. Back, sides, a plinth, a deep top
    # rail for the label, one shelf and two dividers make six open cubbies.
    t = 0.02
    body = join(u(-W / 2, W / 2, 0, H, -D / 2, -D / 2 + t),
                u(-W / 2, -W / 2 + t, 0, H, -D / 2 + t, D / 2), u(W / 2 - t, W / 2, 0, H, -D / 2 + t, D / 2),
                u(-W / 2 + t, W / 2 - t, 0, 0.04, -D / 2 + t, D / 2), u(-W / 2 + t, W / 2 - t, H - 0.1, H, -D / 2 + t, D / 2),
                u(-W / 2 + t, W / 2 - t, 0.32, 0.34, -D / 2 + t, D / 2),
                *[u(x - t / 2, x + t / 2, 0.04, H - 0.1, -D / 2 + t, D / 2) for x in (-W / 6, W / 6)])
    body = paint(body, 0)
    label = placed_text('室内履き', 0.04, W / 2 - 0.05, H - 0.07, D / 2)
    ob = part('Kit_Slipper_Cabinet', join(body, paint(label, 2)), [M_WALL, M_MIST, M_TRIM], smooth=30)
    collider(ob, 'box')


def phone_desk():
    """A white desk phone with a silver keypad and handset. Faces +Z. Pivot bottom centre. Loose prop, box collider."""
    w, d, h = P.PHONE_W, P.PHONE_D, P.PHONE_H
    base = prism_zy([(d / 2, 0), (d / 2, 0.035), (-d / 2, h * 0.7), (-d / 2, 0)], -w / 2, w / 2)
    handset = u(-w / 2 + 0.01, -w / 2 + 0.06, h * 0.55, h, -d / 2 + 0.01, d / 2 - 0.02)
    keys = u(-0.02, w / 2 - 0.02, 0.05, 0.06, -0.04, 0.06)
    ob = part('Kit_Phone_Desk', join(paint(base, 0), paint(handset, 0), paint(keys, 1)), [M_WALL, M_FRAME], smooth=30)
    collider(ob, 'box')   # loose prop: can be picked up and thrown


def doc_tray():
    """A two-tier document tray in aqua frosted plastic, with a paper stack. Pivot bottom centre. Loose prop, box collider."""
    w, d, h = P.TRAY_W, P.TRAY_D, P.TRAY_H
    tiers = []
    for y in (0.0, h / 2):
        tiers.append(u(-w / 2, w / 2, y, y + 0.008, -d / 2, d / 2))
        tiers.append(u(-w / 2, -w / 2 + 0.006, y, y + h / 2 - 0.01, -d / 2, d / 2))
        tiers.append(u(w / 2 - 0.006, w / 2, y, y + h / 2 - 0.01, -d / 2, d / 2))
    paper = u(-w / 2 + 0.02, w / 2 - 0.02, 0.008, 0.03, -d / 2 + 0.02, d / 2 - 0.03)
    ob = part('Kit_Doc_Tray', join(paint(join(*tiers), 0), paint(paper, 1)), [M_FROST, M_WALL], smooth=None)
    collider(ob, 'box')   # loose prop: can be picked up and thrown


def logo_aonagi():
    """The AONAGI wall logo: the ring with its red hanko dot, the company name in Japanese and English.
    Hangs on a wall face like a zone sign: pivot at floor level at its low-X edge, 4 mm proud toward +Z.
    Visual only."""
    D = P.LOGO_D
    ring, dot = ring_logo(1.9, 1.9, 0, D)
    ja = placed_text(COMPANY_JA, 0.16, 1.62, 1.86, 0, max_width=1.55)
    en = placed_text('AONAGI', 0.07, 1.62, 1.72, 0)
    ob = part('Kit_Logo_Aonagi', join(paint(ring, 0), paint(dot, 1), paint(join(ja, en), 2)), [M_AQUA, M_SEAL, M_TRIM], smooth=None)


def sign_led_reception():
    """The reception's dot-matrix sign: a dark panel with glowing 受付 1F lettering, its bottom at 2.65 m.
    Hangs on a wall like a zone sign (pivot at floor level, low-X edge; proud toward +Z). Visual only."""
    W, H, T, y = P.LED_W, P.LED_H, P.LED_T, P.LED_Y
    panel = u(0, W, y, y + H, 0, T)
    bevel(panel, 0.006, segments=2, angle=45)
    text = placed_text('受付  1F  RECEPTION', 0.09, W - 0.08, y + 0.045, T, max_width=W - 0.16)
    ob = part('Kit_Sign_LED_Reception', join(paint(panel, 0), paint(text, 1)), [M_TRIM, M_LIGHT], smooth=None)


def water_wall():
    """The contained water wall (Aero; the mix): a silver-framed cassette with a translucent aqua water panel,
    1.8 x 2.7 m, 80 mm deep. Hangs on a wall face, pivot at floor level at its low-X edge, proud toward +Z.
    Fixed. Box collider (it stands proud of the wall, so players must not walk into it)."""
    W, T, H = P.WATER_W, P.WATER_T, P.WATER_H
    frame = recessed(u(0, W, 0, H, 0, T), u(0.06, W - 0.06, 0.06, H - 0.06, T - 0.03, T + 0.01))
    water = u(0.06, W - 0.06, 0.06, H - 0.06, T - 0.04, T - 0.02)
    ob = part('Kit_Water_Wall', join(paint(frame, 0), paint(water, 1)), [M_FRAME, M_WATER], smooth=None)
    collider(ob, 'box')


def monitor_flat():
    """A silver flat-panel monitor on a neck and foot, its dark screen facing +Z. Pivot bottom centre (under the
    foot). Loose prop: box collider, so it can be picked up and thrown (Alex, 2026-11-13)."""
    w, h = P.MONITOR_W, P.MONITOR_H
    silver = join(u(-w / 2, w / 2, 0.12, 0.12 + h, -0.015, 0.015),     # panel
                  u(-0.03, 0.03, 0, 0.14, -0.04, -0.02),               # neck
                  u(-0.12, 0.12, 0, 0.012, -0.09, 0.09))               # foot
    screen = u(-w / 2 + 0.02, w / 2 - 0.02, 0.14, 0.10 + h, 0.015, 0.017)
    ob = part('Kit_Monitor_Flat', join(paint(silver, 0), paint(screen, 1)), [M_FRAME, M_TRIM], smooth=None)
    expect(ob, size=(w, 0.18, 0.12 + h), tol=0.002)
    collider(ob, 'box')


def keyboard():
    """A pale low-profile keyboard. Pivot bottom centre. Loose prop: box collider."""
    ob = part('Kit_Keyboard', u(-0.22, 0.22, 0, 0.015, -0.07, 0.07), M_WALL, smooth=None)
    expect(ob, size=(0.44, 0.14, 0.015), tol=0.001)
    collider(ob, 'box')


def lift_door_frame(label):
    """The lift's landing wall: a 4 m pearl wall, full storey from the slab line (-SLAB) to the soffit, with a
    centred 1.4 x 2.2 m opening, a silver portal round it, a dark indicator above showing `label` in aqua, and a
    call panel with up and down buttons beside it. Runs +X from the pivot on the landing face (+Z = the lobby);
    the leaves slide behind it on the shaft side. Box colliders on the left, right and head parts only."""
    W, T, ow, oh = P.BAY, P.WALL, P.LIFT_OPEN_W, P.LIFT_OPEN_H
    x0, x1 = (W - ow) / 2, (W + ow) / 2
    y0 = -P.SLAB   # WHY from the slab line: shaft walls stack storey on storey with no gap at the floors
    parts = []
    for name, b in (('Left', u(0, x0, y0, CLEAR_H, -T, 0)), ('Right', u(x1, W, y0, CLEAR_H, -T, 0)),
                    ('Head', u(x0, x1, oh, CLEAR_H, -T, 0))):
        ob = part(f'Kit_Lift_Door_Frame_4m_{label[:2]}_{name}', b, M_WALL, smooth=None)
        collider(ob, 'box')
        parts.append(ob)
    t, pr = 0.12, 0.03   # portal band width and how far it stands proud, metres
    portal = [u(x0 - t, x0, 0, oh + t, 0, pr), u(x1, x1 + t, 0, oh + t, 0, pr), u(x0 - t, x1 + t, oh, oh + t, 0, pr),
              u(x0, x0 + 0.02, 0, oh, -T, 0), u(x1 - 0.02, x1, 0, oh, -T, 0), u(x0, x1, oh - 0.02, oh, -T, 0)]  # linings
    portal = join(*portal)
    bevel(portal, 0.008, segments=2, angle=45)
    iy = oh + 0.3
    panel = u(x0 + 0.1, x1 - 0.1, iy, iy + 0.32, 0, 0.04)
    ink = placed_text(label, 0.16, x1 - 0.2, iy + 0.08, 0.04, 0.006, 0, max_width=ow - 0.4)
    cx, cy = x1 + 0.35, 1.1   # call panel centre, metres
    plate = u(cx - 0.09, cx + 0.09, cy - 0.18, cy + 0.18, 0, 0.015)
    up = prism_xy([(cx - 0.04, cy + 0.04), (cx + 0.04, cy + 0.04), (cx, cy + 0.11)], 0.015, 0.022)
    down = prism_xy([(cx - 0.04, cy - 0.04), (cx, cy - 0.11), (cx + 0.04, cy - 0.04)], 0.015, 0.022)
    trim = part(f'Kit_Lift_Door_Frame_4m_{label[:2]}_Trim',
                join(paint(portal, 0), paint(plate, 0), paint(panel, 1), paint(join(ink, up, down), 2)),
                [M_FRAME, M_TRIM, M_AQUA], smooth=None)
    expect(parts[0], size=(x0, T, CLEAR_H + P.SLAB))
    group(f'Kit_Lift_Door_Frame_4m_{label[:2]}', *parts, trim)


def lift_door_panel():
    """One bi-parting landing leaf in brushed silver. Pivot at the bottom of the meeting edge, centred on its
    thickness; the leaf runs +X, and the lift script opens it by sliding LIFT_LEAF_W along +X. Box collider."""
    w, h, t = P.LIFT_LEAF_W, P.LIFT_LEAF_H, P.LIFT_LEAF_T
    ob = part('Kit_Lift_Door_Panel', u(0, w, 0, h, -t / 2, t / 2), M_FRAME, smooth=None)
    expect(ob, size=(w, t, h))
    collider(ob, 'box')


def lift_shaft_wall():
    """A 4 m glass shaft wall, full storey from the slab line to the soffit, so the car is seen riding: a silver
    base band over the slab line, clear glass, a silver head rail and end posts. Runs +X from the pivot; its
    thickness goes -Z (the shaft side). Box collider over the frame."""
    W, T = P.BAY, P.PART_T
    zc = -T / 2
    y0, r = -P.SLAB, P.PART_RAIL
    frame = join(u(0, W, y0, r, -T, 0), u(0, W, CLEAR_H - r, CLEAR_H, -T, 0),
                 u(0, 0.04, r, CLEAR_H - r, -T, 0), u(W - 0.04, W, r, CLEAR_H - r, -T, 0))
    fr = part('Kit_Lift_Shaft_Wall_4m_Frame', frame, M_FRAME, smooth=None)
    glass = part('Kit_Lift_Shaft_Wall_4m_Glass',
                 u(0.04, W - 0.04, r, CLEAR_H - r, zc - P.GLASS_T / 2, zc + P.GLASS_T / 2), M_GLASS, smooth=None)
    expect(fr, size=(W, T, CLEAR_H + P.SLAB))
    collider(fr, 'box')
    group('Kit_Lift_Shaft_Wall_4m', fr, glass)


def lift_car():
    """The capsule car: polished floor plate with its top at the pivot (y 0), pearl side walls with rounded edges, a
    clear glass back with a silver handrail at 1.0 m, a ceiling with a light panel, and a header over the open
    front (+Z) at door height. Pivot at the floor's top centre. Moving: the lift script drives its height.
    Box colliders on the floor, both sides, the back, the ceiling and the header; none in the open front."""
    W, D, H, F, t = P.LIFT_CAR_W, P.LIFT_CAR_D, P.LIFT_CAR_H, P.LIFT_CAR_FLOOR, P.LIFT_CAR_WALL
    floor = part('Kit_Lift_Car_Floor', by_normal(u(-W / 2, W / 2, -F, 0, -D / 2, D / 2), 0, 1, 1), [M_FLOOR, M_FRAME],
                 smooth=None)
    sides = []
    for name, (a, b) in (('Left', (-W / 2, -W / 2 + t)), ('Right', (W / 2 - t, W / 2))):
        wall = u(a, b, 0, H, -D / 2, D / 2)
        bevel(wall, 0.02, segments=2, angle=45)
        bm = paint(wall, 0)
        if name == 'Left':   # a small aqua floor indicator inside, beside the opening
            bm = join(bm, paint(u(b, b + 0.01, 1.55, 1.85, D / 2 - 0.55, D / 2 - 0.25), 1))
        sides.append(part(f'Kit_Lift_Car_{name}', bm, [M_WALL, M_AQUA], smooth=30))
    back = part('Kit_Lift_Car_Back', join(u(-W / 2 + t, W / 2 - t, 0, 0.1, -D / 2, -D / 2 + t),
                                         u(-W / 2 + t, W / 2 - t, H - 0.1, H, -D / 2, -D / 2 + t),
                                         u(-W / 2 + 0.15, W / 2 - 0.15, 0.97, 1.03, -D / 2 + t, -D / 2 + t + 0.05)),
                M_FRAME, smooth=None)
    glass = part('Kit_Lift_Car_Glass', u(-W / 2 + t, W / 2 - t, 0.1, H - 0.1, -D / 2 + 0.02, -D / 2 + 0.04), M_GLASS,
                 smooth=None)
    ceiling = part('Kit_Lift_Car_Ceiling', join(paint(u(-W / 2, W / 2, H, H + 0.1, -D / 2, D / 2), 0),
                                               paint(u(-1.2, 1.2, H - 0.015, H, -1.0, 1.0), 1)),
                   [M_WALL, M_LIGHT], smooth=None)
    header = part('Kit_Lift_Car_Header', u(-W / 2 + t, W / 2 - t, P.LIFT_OPEN_H, H, D / 2 - t, D / 2), M_WALL, smooth=None)
    for ob in (floor, *sides, back, ceiling, header):
        collider(ob, 'box')
    expect(floor, size=(W, D, F))
    group('Kit_Lift_Car', floor, *sides, back, glass, ceiling, header)


def pool_raised():
    """The rooftop pool: a raised 8 x 4 m pearl basin (the roof slab is too thin to sink one), a 0.5 m rim 0.4 m
    wide with a silver coping line, two steps up the front long side, an aqua basin floor and a water surface
    0.4 m up drawn by the Aero Water Pane shader seen from above. Pivot at the front-left outside corner at roof
    level; runs +X and -Z. Box colliders on the rim and steps; none on the water (players wade, props sink)."""
    L, Wd, h, r = P.POOL_L, P.POOL_W, P.POOL_RIM_H, P.POOL_RIM_W
    rims = [('Front', u(0, L, 0, h, -r, 0)), ('Back', u(0, L, 0, h, -Wd, -Wd + r)),
            ('Left', u(0, r, 0, h, -Wd + r, -r)), ('Right', u(L - r, L, 0, h, -Wd + r, -r))]
    parts = []
    for name, b in rims:
        bevel(b, 0.03, segments=2, angle=45)
        coping = u(*((0, L) if name in ('Front', 'Back') else (0, r) if name == 'Left' else (L - r, L)),
                   h, h + 0.012, *((-r * 0.6, -r * 0.4) if name == 'Front' else (-Wd + r * 0.4, -Wd + r * 0.6)
                                   if name == 'Back' else (-Wd + r, -r)))
        ob = part(f'Kit_Pool_Raised_8x4_{name}', join(paint(b, 0), paint(coping, 1)), [M_WALL, M_FRAME], smooth=None)
        collider(ob, 'box')
        parts.append(ob)
    sx0 = (L - P.POOL_STEP_L) / 2
    for i, (top, out) in enumerate(((h / 3, 0.6), (2 * h / 3, 0.3))):
        st = u(sx0, sx0 + P.POOL_STEP_L, 0, top, 0, out)
        bevel(st, 0.02, segments=2, angle=45)
        ob = part(f'Kit_Pool_Raised_8x4_Step{i + 1}', st, M_WALL, smooth=None)
        collider(ob, 'box')
        parts.append(ob)
    basin = part('Kit_Pool_Raised_8x4_Basin', u(r, L - r, 0, 0.02, -Wd + r, -r), M_AQUA, smooth=None)
    water = part('Kit_Pool_Raised_8x4_Water', u(r, L - r, P.POOL_WATER - 0.02, P.POOL_WATER, -Wd + r, -r), M_POOL,
                 smooth=None)
    expect(parts[0], size=(L, r, h + 0.012), tol=0.002)
    group('Kit_Pool_Raised_8x4', *parts, basin, water)


def ribbon_centre(x):
    """Ribbon centreline at x (0 to RIBBON_LEN along the piece), as Unity z (negative, into the corridor)."""
    return -(P.RIBBON_MID - P.RIBBON_SWAY * math.cos(2 * math.pi * x / P.RIBBON_LEN))


def band_xz(z_of_x, z0, z1, y0, y1):
    """A solid band along X from 0 to RIBBON_LEN, between z_of_x(x) + z0 and z_of_x(x) + z1, from height y0 to y1, built
    from quads segment by segment (a single ngon cap would be concave)."""
    bm = bmesh.new()
    n = P.RIBBON_SEGMENTS
    rows = []
    for i in range(n + 1):
        x = P.RIBBON_LEN * i / n
        zc = z_of_x(x)
        rows.append([bm.verts.new((-x, -(zc + z), y)) for z, y in ((z0, y0), (z1, y0), (z1, y1), (z0, y1))])
    for a, b in zip(rows, rows[1:]):
        for k in range(4):
            bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]))
    bm.faces.new(rows[0])
    bm.faces.new(list(reversed(rows[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def ribbon_floor():
    """A glossy cobalt ribbon inlaid in the floor, swinging slowly across a 4 m corridor over 16 m, with a thin aqua line
    beside it. Pivot at the cell's NW corner on the floor, like a slab; runs +X, the corridor is -Z. Pieces chain
    end to end without a seam. Visual only. PANLINE lesson (Alex, 2026-11-15): colour bands as architecture."""
    w, t = P.RIBBON_W, P.RIBBON_INLAY
    band = band_xz(ribbon_centre, -w / 2, w / 2, 0, t)
    g, lw = P.RIBBON_LINE_GAP, P.RIBBON_LINE_W
    line = band_xz(ribbon_centre, w / 2 + g, w / 2 + g + lw, 0, t)
    ob = part('Kit_Ribbon_Floor_16m', join(paint(band, 0), paint(line, 1)), [M_COBALT, M_AQUA], smooth=None)
    expect(ob, size=(P.RIBBON_LEN, 2 * P.RIBBON_SWAY + w + g + lw, t), tol=0.01)


def ribbon_ceiling():
    """The floor ribbon's partner overhead: a lacquered cobalt band floating under the soffit (below the beams),
    following the same swing, with a glowing strip along each edge. Pivot at the cell's NW corner at the soffit,
    like the beams; hangs below y 0. Visual only."""
    w, d, t = P.RIBBON_W, P.RIBBON_DROP, P.RIBBON_T
    band = band_xz(ribbon_centre, -w / 2, w / 2, -d, -d + t)
    glow = join(band_xz(ribbon_centre, -w / 2, -w / 2 + 0.03, -d - 0.006, -d),
                band_xz(ribbon_centre, w / 2 - 0.03, w / 2, -d - 0.006, -d))
    ob = part('Kit_Ribbon_Ceiling_16m', join(paint(band, 0), paint(glow, 1)), [M_COBALT, M_LIGHT], smooth=None)
    expect(ob, size=(P.RIBBON_LEN, 2 * P.RIBBON_SWAY + w, t + 0.006), tol=0.01)


def lightbox_wall():
    """A backlit lightbox for a wall: a rounded silver frame round a recessed diffuser, 3.6 x 3.0 m, 80 mm deep. The
    glowing graphic is a texture on the diffuser (KitSigns: Lightbox_Wave or Lightbox_Logo), never modelled. Hangs on
    a wall face like a sign: pivot at floor level at its low-X edge, proud toward +Z. Box collider.
    PANLINE lesson (Alex, 2026-11-15): signage as lit graphic surfaces."""
    W, D, f = P.LIGHTBOX_W, P.LIGHTBOX_D, P.LIGHTBOX_FRAME
    y0, y1 = P.LIGHTBOX_Y
    outer = u(0, W, y0, y1, 0, D)
    bevel(outer, 0.02, segments=3, angle=45)
    body = recessed(outer, u(f, W - f, y0 + f, y1 - f, D - 0.02, D + 0.01))
    ob = part('Kit_' + P.PIECE, body, [M_FRAME, M_WALL], smooth=30)
    expect(ob, size=(W, D, y1 - y0))
    collider(ob, 'box')


def rounded_rect(x0, x1, z0, z1, r, segs=6):
    """Outline of a rounded rectangle in Unity (x, z), counter-clockwise, for prism_xz."""
    pts = []
    for cx, cz, a0 in ((x1 - r, z1 - r, 0), (x0 + r, z1 - r, 90), (x0 + r, z0 + r, 180), (x1 - r, z0 + r, 270)):
        for k in range(segs + 1):
            a = math.radians(a0 + 90 * k / segs)
            pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    return pts


def ceiling_panel():
    """A floating rounded white ceiling panel under a 4 x 4 m soffit, with a glowing cove ring on the soffit round it
    that shows in the gap to the next panel. Pivot at the cell's NW corner at the soffit; everything hangs below
    y 0. Visual only. PANLINE lesson (Alex, 2026-11-15): soft floating panels with light between them."""
    B, S, r = P.BAY, P.PANEL_SIZE, P.PANEL_R
    m = (B - S) / 2
    d0, d1 = P.PANEL_DROP
    panel = prism_xz(rounded_rect(m, B - m, -B + m, -m, r), -d0, -d1)
    bevel(panel, 0.02, segments=2, angle=45)
    c = P.PANEL_COVE_W
    ring_out = rounded_rect(m - c / 2, B - m + c / 2, -B + m - c / 2, -m + c / 2, r + c / 2)
    ring_in = rounded_rect(m + c / 2, B - m - c / 2, -B + m + c / 2, -m - c / 2, max(r - c / 2, 0.05))
    cove = boolean(prism_xz(ring_out, -0.006, -0.001), prism_xz(ring_in, -0.01, 0.01))
    ob = part('Kit_Ceiling_Panel_4x4', join(paint(panel, 0), paint(cove, 1)), [M_CEIL, M_LIGHT], smooth=30)
    expect(ob, size=(S + c, S + c, d0 - 0.001), tol=0.01)


PIECES = {
    'Slab_4x4': lambda: slab(P.BAY, P.BAY),
    'Slab_2x4': lambda: slab(P.INFILL, P.BAY),
    'Slab_4x2': lambda: slab(P.BAY, P.INFILL),
    'Slab_2x2': lambda: slab(P.INFILL, P.INFILL),
    'Slab_Stairwell': stairwell_slab,
    'Wall_Solid_4m': lambda: wall_solid(P.BAY),
    'Wall_Solid_2m': lambda: wall_solid(P.INFILL),
    'Wall_Solid_4m_Cobalt': lambda: wall_solid(P.BAY, '_Cobalt'),
    'Wall_Solid_2m_Cobalt': lambda: wall_solid(P.INFILL, '_Cobalt'),
    'Wall_Solid_4m_PortalHead': lambda: wall_solid(P.BAY, '_PortalHead'),
    'Wall_Door_4m': wall_door,
    'Wall_Door_2m': lambda: wall_door(width=P.INFILL),
    'Wall_Door_4m_PortalHead': lambda: wall_door('_PortalHead'),
    'Door_Leaf': door_leaf,
    'Window_4m': lambda: window_bay(P.BAY),
    'Window_2m': lambda: window_bay(P.INFILL),
    'Column_Facade': column_facade,
    'Column_CornerL': lambda: column_corner('L'),
    'Column_CornerR': lambda: column_corner('R'),
    'Partition_Glass_4m': lambda: partition_glass(P.BAY),
    'Partition_Glass_2m': lambda: partition_glass(P.INFILL),
    'Partition_Glass_Door_2m': lambda: partition_glass_door(P.INFILL),
    'Stair_Flight': stair_flight,
    'Stair_Column': stair_column,
    'Stair_CagePanel': stair_cage_panel,
    'Stair_Steps': stair_steps,
    'Stair_LockedDoor': stair_locked_door,
    'Ceiling_LightStrip': ceiling_light,
    # Labyrinth, round 5
    'Wall_Return_2x2': wall_return,
    'Wall_Return_2x2_Cobalt': lambda: wall_return('_Cobalt'),
    'Wall_Niche_4m': wall_niche,
    'Counter_Capsule_2m': counter_capsule,
    'Cover_Block_2m': cover_block,
    'Partition_Frost_4m': lambda: partition_frost(P.BAY),
    'Partition_Frost_2m': lambda: partition_frost(P.INFILL),
    'Partition_Fluted_4m': lambda: partition_fluted(P.BAY),
    'Partition_Fluted_2m': lambda: partition_fluted(P.INFILL),
    'Partition_Louvre_4m': lambda: partition_louvre(P.BAY),
    'Partition_Louvre_2m': lambda: partition_louvre(P.INFILL),
    'Pendant_Sphere': pendant_sphere,
    'Stair_Straight_2m': stair_straight,
    'Stair_Straight_Half_2m': lambda: stair_straight(P.FLOOR_H / 2),
    'Ramp_2x4': ramp,
    'Guard_Glass_4m': lambda: guard_glass(P.BAY),
    'Guard_Glass_2m': lambda: guard_glass(P.INFILL),
    'Slab_4x4_LightLine': slab_light_line,
    # Fix list 2026-11-13
    'Sign_Zone_L': lambda: sign_zone('L'),
    'Sign_Zone_R': lambda: sign_zone('R'),
    'Sign_Zone_C': lambda: sign_zone('C'),
    'Sign_Zone_H': lambda: sign_zone('H'),
    'Sign_Zone_A': lambda: sign_zone('A'),
    'Desk_Sculpt': desk_sculpt,
    'Chair_Pod': chair_pod,
    # Detail pass 2026-11-13
    'Column_Graphic': lambda: column_graphic(CLEAR_H, '1F', ['< L 法務部', '  C 請求部', '  A 記録保管室', '> H 受付']),
    'Column_Graphic_Tall': lambda: column_graphic(2 * P.FLOOR_H - P.SLAB, '2F', ['^ H 審問室', '^ R 審査部', '  会議室', 'v 受付 1F']),
    'Workstation_Bench': workstation_bench,
    'Chair_Task': chair_task,
    'Storage_Low': storage_low,
    'Copier': copier,
    'Desk_Reception': desk_reception,
    'Guard_Rail_4m': lambda: guard_rail(P.BAY),
    'Guard_Rail_2m': lambda: guard_rail(P.INFILL),
    'Partition_Header_4m': lambda: partition_header(P.BAY),
    'Partition_Header_2m': lambda: partition_header(P.INFILL),
    'Partition_Header_Curve_2x2': partition_header_curve,
    'Light_Pendant_Linear': light_pendant,
    'Carpet_Overlay_4x4': carpet_overlay,
    'Ceiling_Grid_4x4': ceiling_grid,
    'Planter_Box': planter_box,
    # Japanese office props (the mix, concepts/Japanese_Aero/DECISION.md)
    'Water_Server': water_server,
    'Umbrella_Stand': umbrella_stand,
    'Vending_Machine': vending_machine,
    'Slipper_Cabinet': slipper_cabinet,
    'Phone_Desk': phone_desk,
    'Doc_Tray': doc_tray,
    'Logo_Aonagi': logo_aonagi,
    'Sign_LED_Reception': sign_led_reception,
    'Water_Wall': water_wall,
    # Loose props split out of the bench, the lift and the rooftop pool (2026-11-13)
    'Monitor_Flat': monitor_flat,
    'Keyboard': keyboard,
    'Lift_Door_Frame_4m_1F': lambda: lift_door_frame('1F'),
    'Lift_Door_Frame_4m_RF': lambda: lift_door_frame('RF  屋上'),
    'Lift_Door_Panel': lift_door_panel,
    'Lift_Shaft_Wall_4m': lift_shaft_wall,
    'Lift_Car': lift_car,
    'Pool_Raised_8x4': pool_raised,
    # Glass and structure (AEROSET reference, 2026-11-14)
    'Wall_Curtain_4m': lambda: wall_curtain(P.BAY),
    'Ceiling_Beams_4x4': ceiling_beams,
    # Ribbons and lightboxes (PANLINE reference, 2026-11-15)
    'Ribbon_Floor_16m': ribbon_floor,
    'Ribbon_Ceiling_16m': ribbon_ceiling,
    'Lightbox_Wall_Wave': lightbox_wall,
    'Lightbox_Wall_Logo': lightbox_wall,
    'Ceiling_Panel_4x4': ceiling_panel,
}

if P.PIECE not in PIECES:
    raise ValueError(f'PIECE={P.PIECE!r}; choose one of {sorted(PIECES)}')
PIECES[P.PIECE]()
