# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code, blender-modeling skill)
#  Generated : 2026-11-11
#  Prompt    : "Start the snap kit" (brief first pass, step 2: the minimum snap kit)
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Every piece of the minimum office snap kit, one piece per build, chosen with
#              --set PIECE=<name>. One script so the grid, heights, thicknesses and materials
#              are defined once and every piece agrees on them; the alternative, a script per
#              piece, would let the shared numbers drift apart. Conventions are in README.md.
#  Modified  : Claude Opus 5.5, 2026-11-11 — window bay and façade columns to the approved "White Trays"
#              concept (concepts/Window_Bay_R3/01_White_Trays): sill street-top recess, recessed navy
#              column fronts, navy rails, new Trim_Navy slot. Envelopes, pivots and colliders unchanged.
#  Modified  : Claude Opus 5.5, 2026-11-12 — rest of the kit to the approved office direction "White Trays +
#              Cobalt Portal" (concepts/Office): wall shadow gaps, cobalt and portal-head wall variants, door
#              reveals, navy partition rails, white stair column, visual open treads and handrail, curved cage
#              panels (hull collider), the red locked stair door, a ceiling strip light, darker carpet; new
#              slots Accent_Cobalt, Light_Strip, Door_LockedRed. Envelopes, pivots and snap faces unchanged.
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
# -----------------------------------------------------------------------------
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
    # ---- Façade: GreyboxTowerSetup (sill 1 m, pillar 0.4 m wide)
    SILL=1.0,              # sill height, metres
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
M_FLOOR = material('Floor_Carpet', hexc('646E86'), roughness=0.9)
M_CEIL = material('Ceiling_Tile', hexc('F3F4F4'), roughness=0.9)
M_WALL = material('Wall_PaperWhite', hexc('F3F4F4'), roughness=0.7)
M_FRAME = material('Frame_Aluminium', hexc('C9CED3'), metallic=0.8, roughness=0.35)
M_GLASS = material('Glass_Clear', (0.70, 0.85, 0.95), roughness=0.05)
M_DOOR = material('Door_Leaf', hexc('C9CED3'), roughness=0.6)
M_STAIR = material('Stair_Aluminium', hexc('C9CED3'), metallic=0.6, roughness=0.35)
M_TRIM = material('Trim_Navy', hexc('111B38'), roughness=0.5)   # Deep Navy; bible §11 "black trim" anchors
M_RAIL = M_TRIM if P.RAIL_FINISH == 'navy' else M_FRAME
M_COBALT = material('Accent_Cobalt', hexc('244FB9'), roughness=0.5)   # Corporate Cobalt, bible §03: large blocks only
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


def wall_solid(width, finish=''):
    """Opaque wall, floor to soffit, with shadow-gap skirting and a soffit reveal on both faces. Box collider."""
    bm = recessed(u(0, width, 0, CLEAR_H, -P.WALL, 0), wall_cuts(0, width, portal=finish == '_PortalHead'))
    ob = part(f'Kit_Wall_Solid_{width:g}m{finish}', finish_face(bm, finish), wall_mats(finish), smooth=None)
    expect(ob, size=(width, P.WALL, CLEAR_H))
    collider(ob, 'box')


def wall_door(finish=''):
    """4 m wall with a centred doorway. Three parts so each gets an exact box collider and the gap stays open.

    A shadow reveal frames the opening on both faces, outside the clear 1.2 x 2.4 m the leaf fills.
    """
    x0 = (P.BAY - P.DOOR_W) * 0.5
    x1 = x0 + P.DOOR_W
    cuts = lambda a, b: wall_cuts(a, b, door=(x0, x1, P.DOOR_H), portal=finish == '_PortalHead')
    parts = []
    for name, box_ in (('Left', u(0, x0, 0, CLEAR_H, -P.WALL, 0)),
                       ('Right', u(x1, P.BAY, 0, CLEAR_H, -P.WALL, 0)),
                       ('Head', u(x0, x1, P.DOOR_H, CLEAR_H, -P.WALL, 0))):
        bm = finish_face(recessed(box_, cuts(0, P.BAY)), finish)
        parts.append(part(f'Kit_Wall_Door_4m{finish}_{name}', bm, wall_mats(finish), smooth=None))
    left, right, head = parts
    for ob in parts:
        collider(ob, 'box')
    expect(left, size=(x0, P.WALL, CLEAR_H))
    expect(head, size=(P.DOOR_W, P.WALL, CLEAR_H - P.DOOR_H))
    group(f'Kit_Wall_Door_4m{finish}', left, right, head)


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
    sill behind it and the bounds unchanged, so the box collider (full metre of cover) is the same as before.
    """
    c = P.COLUMN_W * 0.5
    zc = -P.WALL * 0.5
    eps = 0.01   # WHY: the cutter overshoots the faces it opens, so the boolean leaves no paper-thin skin
    pocket = u(c, width - c, P.SILL - P.TRAY_RECESS_H, P.SILL + eps, -P.TRAY_RECESS_D, eps)
    sill = part(f'Kit_Window_{width:g}m_Sill', recessed(u(0, width, 0, P.SILL, -P.WALL, 0), pocket),
                [M_WALL, M_TRIM], smooth=None)
    collider(sill, 'box')
    rails = join(u(c, width - c, P.SILL, P.SILL + P.RAIL, zc - P.RAIL_D * 0.5, zc + P.RAIL_D * 0.5),
                 u(c, width - c, CLEAR_H - P.RAIL, CLEAR_H, zc - P.RAIL_D * 0.5, zc + P.RAIL_D * 0.5))
    frame = part(f'Kit_Window_{width:g}m_Frame', rails, M_RAIL, smooth=None)
    pane = part(f'Kit_Window_{width:g}m_Glass',
                u(c, width - c, P.SILL + P.RAIL, CLEAR_H - P.RAIL, zc - P.GLASS_T * 0.5, zc + P.GLASS_T * 0.5),
                M_GLASS, smooth=None)
    expect(sill, size=(width, P.WALL, P.SILL))
    expect(pane, size=(width - P.COLUMN_W, P.GLASS_T, CLEAR_H - P.SILL - 2 * P.RAIL))
    group(f'Kit_Window_{width:g}m', sill, frame, pane)


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
    frame = part(f'Kit_Partition_Glass_{width:g}m_Frame', rails, M_RAIL, smooth=None)
    pane = part(f'Kit_Partition_Glass_{width:g}m_Glass',
                u(0, width, P.PART_RAIL, P.PART_H - P.PART_RAIL, zc - P.GLASS_T * 0.5, zc + P.GLASS_T * 0.5),
                M_GLASS, smooth=None)
    collider(frame, 'box')   # the rails' box spans the full wall, so it blocks the pane too
    expect(frame, size=(width, P.PART_T, P.PART_H))
    group(f'Kit_Partition_Glass_{width:g}m', frame, pane)


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
    'Wall_Door_4m_PortalHead': lambda: wall_door('_PortalHead'),
    'Door_Leaf': door_leaf,
    'Window_4m': lambda: window_bay(P.BAY),
    'Window_2m': lambda: window_bay(P.INFILL),
    'Column_Facade': column_facade,
    'Column_CornerL': lambda: column_corner('L'),
    'Column_CornerR': lambda: column_corner('R'),
    'Partition_Glass_4m': lambda: partition_glass(P.BAY),
    'Partition_Glass_2m': lambda: partition_glass(P.INFILL),
    'Stair_Flight': stair_flight,
    'Stair_Column': stair_column,
    'Stair_CagePanel': stair_cage_panel,
    'Stair_Steps': stair_steps,
    'Stair_LockedDoor': stair_locked_door,
    'Ceiling_LightStrip': ceiling_light,
}

if P.PIECE not in PIECES:
    raise ValueError(f'PIECE={P.PIECE!r}; choose one of {sorted(PIECES)}')
PIECES[P.PIECE]()
