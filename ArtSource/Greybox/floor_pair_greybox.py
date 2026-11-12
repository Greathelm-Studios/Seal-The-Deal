# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code, blender-modeling skill)
#  Generated : 2026-11-11
#  Prompt    : "First-pass greybox from the 3D modeller brief, built to the repo's floor dimensions"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : A Blender review scene of the current game floor (TOWER.md, DefaultTower.asset):
#              two facing towers, three office floors each, central spiral stairs, glass meeting
#              room, opaque service spine and player-sized stand-ins. It exists so environment
#              art can be judged at eye height and across the gap before a modular kit is cut.
#              It is a review scene, not a Unity export: nothing here replaces a floor template.
# -----------------------------------------------------------------------------
"""Floor-pair greybox for Close the Deal.

Build:  node <skill>/scripts/mdl.mjs build ArtSource/Greybox/floor_pair_greybox.py --out ArtSource/Greybox/floor_pair_greybox.blend
Views:  blender -b ArtSource/Greybox/floor_pair_greybox.blend -P ArtSource/Greybox/review_views.py -- <out folder>

Geometry is described in Unity's floor-local frame, exactly as the greybox template builder
(Assets/_Project/Editor/Greybox/GreyboxTowerSetup.cs) lays it out: pivot at the centre of the
window edge at floor level, +Z toward the rival tower, +Y up. `ubox()` converts to Blender
(Unity +Z -> Blender -Y, Unity +X -> Blender -X, Unity +Y -> Blender +Z), the same mapping the
skill's FBX export undoes, so a later kit piece built this way imports with no rotation.
WHY describe it in Unity's frame: every number then matches the C# builder line for line, and
a reviewer can check one against the other. The alternative, re-deriving each position in
Blender's frame, was error-prone for the 180-degree tower B.

Contents are a review composite, not one of the three repo templates: OfficeA's desk row,
one of OfficeB's glass rooms and OfficeC's copier, so a single floor has every room type the
brief's first pass asks for.
"""
import math

import bmesh
from modelkit import *

P = params(
    # ---- Shell: DefaultTower.asset (Width, MainDepth, SpineDepth, FloorHeight, Gap)
    WIDTH=28.0,            # floor width across the façade, metres
    MAIN_DEPTH=14.0,       # open main floor, window edge to spine wall, metres
    SPINE_DEPTH=4.0,       # rear service spine depth, metres
    FLOOR_HEIGHT=4.5,      # floor to floor, metres
    GAP=12.0,              # façade to façade across the street, metres
    FLOORS=3,              # floors per tower in the review stack: one below, the test floor, one above
    # ---- Shell constants: GreyboxTowerSetup.cs
    SLAB=0.2,              # slab thickness, hangs below floor level, metres
    WALL=0.2,              # wall thickness, metres
    SILL=1.0,              # window sill height, metres
    PILLAR_W=0.4,          # façade pillar width, metres
    PILLAR_STEP=4.0,       # façade pillar spacing (the structural bay), metres
    DOOR_W=1.2,            # spine room door width, metres
    # ---- Façade glass: NOT in the repo (TOWER.md: open bays). Visual only, no collider intended.
    FACADE_GLASS=True,     # True adds a pane per bay above the sill, to test reading through glazing
    GLASS_T=0.02,          # pane thickness, metres
    # ---- Spiral stairs: GreyboxTowerSetup.cs
    STAIR_R=2.6,           # outer edge of the treads, metres
    COLUMN_R=0.8,          # stair column radius, metres
    FLIGHT_T=0.25,         # tread slab thickness, metres
    FLIGHT_SEGMENTS=48,    # segments per full turn
    CAGE_GAP=0.15,         # cage panels sit this far outside the treads, metres
    CAGE_PANELS=12,        # panels in a full circle
    CAGE_OPEN_FROM=300.0,  # no panels from here to 360 degrees: where the flight from below arrives
    STAIR_BACK=1.2,        # walkway between cage and spine wall, metres
    DOOR_ANGLE=30.0,       # locked door, degrees past the arrival point
    # ---- Ceiling light panels: NOT in the repo. A greybox stand-in for cheap office fittings,
    #      one per 4 m bay at the slab soffit; a guess to judge, sized like a common 600 x 1200 panel.
    PANEL_W=0.6, PANEL_L=1.2, PANEL_T=0.04,   # metres; long side runs toward the windows
    PANEL_STAIR_CLEAR=0.6,  # keep panels this far outside the stair cage, metres
    # ---- Glass meeting room: GreyboxTowerSetup.GlassRoom
    ROOM_W=4.0, ROOM_D=3.0, ROOM_H=2.5,   # metres
    # ---- Props: GreyboxPropSetup.cs stand-in sizes (x, height, depth), metres
    DESK_W=1.6, DESK_H=0.75, DESK_D=0.8,
    CHAIR_W=0.5, CHAIR_H=0.9, CHAIR_D=0.5,
    MONITOR_W=0.5, MONITOR_H=0.45, MONITOR_D=0.15,
    COPIER_W=1.2, COPIER_H=1.2, COPIER_D=0.8,
    TABLE_W=2.0, TABLE_H=0.75, TABLE_D=1.0,
    # ---- Player: GreyboxSceneSetup.cs BodyHeight / BodyRadius; TestBlast.asset EyeHeight
    BODY_H=1.8,            # capsule height, pivot at the feet, metres
    BODY_R=0.35,           # capsule radius, metres
)

# Spine rooms left to right in Unity X, as fractions of the width (GreyboxTowerSetup.SpineRooms).
SPINE_ROOMS = (("WC", 0.3), ("STORE", 0.4), ("UTIL", 0.3))

H = P.FLOOR_HEIGHT
HALF = P.WIDTH * 0.5
DEPTH = P.MAIN_DEPTH + P.SPINE_DEPTH


def direction(deg):
    """Unity floor-local unit vector (x, z): clockwise from above, 0 toward the rival, 90 toward +X."""
    r = math.radians(deg)
    return math.sin(r), math.cos(r)


def stair_centre_z():
    """GreyboxTowerSetup.StairCentre: set back so a walkway stays behind the cage."""
    outer = P.STAIR_R + P.CAGE_GAP
    return min(-P.MAIN_DEPTH + outer + P.STAIR_BACK, -outer - 1.0)


SZ = stair_centre_z()


def ubox(cx, cy, cz, sx, sy, sz, yaw=0.0):
    """A box given in Unity floor-local terms (centre, full size, yaw degrees about +Y), returned in Blender's frame."""
    bm = box(sx, sz, sy)
    if yaw:
        # WHY negative: Unity's yaw is clockwise from above; after the axis swap that is
        # Blender's clockwise too, and Blender's positive Z rotation is counter-clockwise.
        rotate(bm, -yaw, 'Z')
    return move(bm, -cx, -cz, cy)


# ---------------------------------------------------------------- one floor, floor-local

def shell_pieces():
    """Slab with stairwell, outer walls, window sill and pillars, spine walls. Mirrors BuildShell."""
    y = -P.SLAB * 0.5
    r = P.STAIR_R
    front, back = SZ + r, SZ - r
    slab = [
        ubox(0, y, front * 0.5, P.WIDTH, P.SLAB, -front),
        ubox(0, y, (back - P.MAIN_DEPTH) * 0.5, P.WIDTH, P.SLAB, back + P.MAIN_DEPTH),
        ubox((-HALF - r) * 0.5, y, SZ, HALF - r, P.SLAB, 2 * r),
        ubox((HALF + r) * 0.5, y, SZ, HALF - r, P.SLAB, 2 * r),
        ubox(0, y, -P.MAIN_DEPTH - P.SPINE_DEPTH * 0.5, P.WIDTH, P.SLAB, P.SPINE_DEPTH),
    ]
    for i in range(4):
        a = 45.0 + i * 90.0
        dx, dz = direction(a)
        slab.append(ubox(dx * r * 1.5, y, SZ + dz * r * 1.5, r, P.SLAB, r, a))

    walls = [
        ubox(0, H * 0.5, -DEPTH + P.WALL * 0.5, P.WIDTH, H, P.WALL),
        ubox(-HALF + P.WALL * 0.5, H * 0.5, -DEPTH * 0.5, P.WALL, H, DEPTH),
        ubox(HALF - P.WALL * 0.5, H * 0.5, -DEPTH * 0.5, P.WALL, H, DEPTH),
    ]

    facade = [ubox(0, P.SILL * 0.5, -P.WALL * 0.5, P.WIDTH, P.SILL, P.WALL)]
    pillar_h = H - P.SILL
    pillar_xs = []
    x = -HALF
    while x <= HALF + 0.01:
        cx = max(-HALF + P.PILLAR_W * 0.5, min(HALF - P.PILLAR_W * 0.5, x))
        pillar_xs.append(cx)
        facade.append(ubox(cx, P.SILL + pillar_h * 0.5, -P.WALL * 0.5, P.PILLAR_W, pillar_h, P.WALL))
        x += P.PILLAR_STEP

    spine = []
    front_z = -P.MAIN_DEPTH + P.WALL * 0.5
    back_z = -DEPTH
    left = -HALF
    for i, (_, frac) in enumerate(SPINE_ROOMS):
        right = left + P.WIDTH * frac
        mid = (left + right) * 0.5
        ll = mid - P.DOOR_W * 0.5 - left
        rl = right - (mid + P.DOOR_W * 0.5)
        spine.append(ubox(left + ll * 0.5, H * 0.5, front_z, ll, H, P.WALL))
        spine.append(ubox(right - rl * 0.5, H * 0.5, front_z, rl, H, P.WALL))
        if i > 0:
            spine.append(ubox(left, H * 0.5, (front_z + back_z) * 0.5, P.WALL, H, P.SPINE_DEPTH - P.WALL))
        left = right

    return slab, walls + spine, facade, pillar_xs


def facade_glass(pillar_xs):
    """One pane per bay between pillar faces, sill to slab soffit. Not in the repo; see FACADE_GLASS."""
    panes = []
    top = H - P.SLAB
    for a, b in zip(pillar_xs, pillar_xs[1:]):
        x0, x1 = a + P.PILLAR_W * 0.5, b - P.PILLAR_W * 0.5
        if x1 - x0 > 0.01:
            panes.append(ubox((x0 + x1) * 0.5, (P.SILL + top) * 0.5, -P.WALL * 0.5, x1 - x0, top - P.SILL, P.GLASS_T))
    return panes


def ceiling_panels():
    """A light panel at the centre of each 4 m bay of the main floor, hung at the slab soffit, clear of the stairwell."""
    panels = []
    y = H - P.SLAB - P.PANEL_T * 0.5
    keep_out = P.STAIR_R + P.CAGE_GAP + P.PANEL_STAIR_CLEAR
    cols = int(round(P.WIDTH / P.PILLAR_STEP))
    rows = int(P.MAIN_DEPTH // P.PILLAR_STEP)
    for i in range(cols):
        x = -HALF + P.PILLAR_STEP * (i + 0.5)
        for j in range(rows):
            z = -P.PILLAR_STEP * (j + 0.5)
            if math.hypot(x, z - SZ) < keep_out:
                continue
            panels.append(ubox(x, y, z, P.PANEL_W, P.PANEL_T, P.PANEL_L))
    return panels


def flight():
    """One full clockwise turn of solid helical tread, column to outer edge, rising one floor (FillFlightMesh)."""
    bm = bmesh.new()
    n = P.FLIGHT_SEGMENTS
    rings = []
    for i in range(n + 1):
        t = i / n
        dx, dz = direction(t * 360.0)
        y = t * H
        ring = []
        for rad, yy in ((P.COLUMN_R, y), (P.STAIR_R, y), (P.STAIR_R, y - P.FLIGHT_T), (P.COLUMN_R, y - P.FLIGHT_T)):
            ux, uz = dx * rad, SZ + dz * rad
            ring.append(bm.verts.new((-ux, -uz, yy)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for k in range(4):
            bm.faces.new((a[k], a[(k + 1) % 4], b[(k + 1) % 4], b[k]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def stair_pieces(has_door):
    """Column, flight, glass cage and the locked door stand-in (BuildStairs, BuildCage, EnsureDoorPrefab)."""
    column = cylinder(P.COLUMN_R, H, segments=24, base='bottom')
    move(column, 0, -SZ, 0)

    cage = []
    step = 360.0 / P.CAGE_PANELS
    radius = P.STAIR_R + P.CAGE_GAP
    width = 2 * radius * math.tan(math.radians(step * 0.5)) + 0.05
    height = H - P.SLAB
    for i in range(P.CAGE_PANELS):
        a = step * (i + 0.5)
        if a > P.CAGE_OPEN_FROM:
            continue
        dx, dz = direction(a)
        cage.append(ubox(dx * radius, height * 0.5, SZ + dz * radius, width, height, 0.05, a))

    door = None
    if has_door:
        size_x = P.STAIR_R - P.COLUMN_R - 0.05
        size_y = H - P.FLIGHT_T - 0.05
        door_y = H * P.DOOR_ANGLE / 360.0 - 0.02
        rad = (P.COLUMN_R + P.STAIR_R) * 0.5
        dx, dz = direction(P.DOOR_ANGLE)
        door = ubox(dx * rad, door_y + size_y * 0.5, SZ + dz * rad, size_x, size_y, 0.2, P.DOOR_ANGLE - 90.0)

    return join(column, flight()), cage, door


def content_pieces():
    """Fixed cover (tables) and loose-prop stand-ins at the template's marker positions."""
    d = P.MAIN_DEPTH
    fixed, loose = [], []
    # OfficeA: a row of four desks with monitors and chairs, window side of the stairwell.
    z = -d * 0.3
    for i in range(4):
        x = -HALF + P.WIDTH * (i + 1) / 5.0
        loose.append(ubox(x, P.DESK_H * 0.5, z, P.DESK_W, P.DESK_H, P.DESK_D))
        loose.append(ubox(x, P.DESK_H + P.MONITOR_H * 0.5, z - 0.1, P.MONITOR_W, P.MONITOR_H, P.MONITOR_D))
        loose.append(ubox(x, P.CHAIR_H * 0.5, z + 1.0, P.CHAIR_W, P.CHAIR_H, P.CHAIR_D))
    # OfficeB's left glass room: three glass walls, open toward the windows, fixed table, two chairs.
    rx, rz = -P.WIDTH * 0.3, -d * 0.55
    glass = [
        ubox(rx, P.ROOM_H * 0.5, rz - P.ROOM_D * 0.5, P.ROOM_W, P.ROOM_H, 0.1),
        ubox(rx - P.ROOM_W * 0.5, P.ROOM_H * 0.5, rz, 0.1, P.ROOM_H, P.ROOM_D),
        ubox(rx + P.ROOM_W * 0.5, P.ROOM_H * 0.5, rz, 0.1, P.ROOM_H, P.ROOM_D),
    ]
    fixed.append(ubox(rx, P.TABLE_H * 0.5, rz, P.TABLE_W, P.TABLE_H, P.TABLE_D))
    for side in (-1, 1):
        loose.append(ubox(rx + side * 1.4, P.CHAIR_H * 0.5, rz, P.CHAIR_W, P.CHAIR_H, P.CHAIR_D))
    # OfficeC's copier against the spine, right side.
    loose.append(ubox(P.WIDTH * 0.3, P.COPIER_H * 0.5, -d + 1.5, P.COPIER_W, P.COPIER_H, P.COPIER_D))
    return fixed, loose, glass


# ---------------------------------------------------------------- materials (named for Unity)

M_SLAB = material('Slab_PaleGrey', (0.86, 0.87, 0.88), roughness=0.8)
M_SERVICE = material('Wall_ServiceOpaque', (0.62, 0.64, 0.68), roughness=0.8)
M_FACADE = material('Facade_WhiteFrame', (0.93, 0.94, 0.95), roughness=0.6)
M_GLASS = material('Glass_CoolBlue', (0.70, 0.85, 0.95), roughness=0.05)
M_STAIR = material('Stair_Aluminium', (0.72, 0.74, 0.77), metallic=0.6, roughness=0.35)
M_DOOR = material('Door_LockedRed', (0.70, 0.15, 0.12), roughness=0.6)
M_FIXED = material('Cover_Fixed', (0.20, 0.27, 0.55), roughness=0.6)       # cobalt: fixed, never moves
M_LOOSE = material('Prop_Loose', (0.55, 0.57, 0.60), roughness=0.7)        # grey: host-spawned physics props
M_PANEL = material('Light_CeilingPanel', (0.95, 0.96, 0.97), roughness=0.4)  # emission is set by the review render
M_SUIT = material('Proxy_DarkSuit', (0.08, 0.09, 0.12), roughness=0.7)     # the dark suit the brief says must track


# ---------------------------------------------------------------- place floors in both towers

def place(bm, tower, level):
    """Floor-local Blender geometry -> world. Tower A sits at Unity z = -GAP/2 facing +Z; B is turned 180 degrees."""
    if tower == 'B':
        rotate(bm, 180.0, 'Z')
        return move(bm, 0, -P.GAP * 0.5, level * H)
    return move(bm, 0, P.GAP * 0.5, level * H)


def proxy(ux, uy, uz):
    """A player-sized capsule standing at a Unity floor-local foot position."""
    r = P.BODY_R
    body = join(move(cylinder(r, P.BODY_H - 2 * r, segments=16, base='bottom'), z=r),
                move(sphere(r, 16, 8), z=r),
                move(sphere(r, 16, 8), z=P.BODY_H - r))
    return move(body, -ux, -uz, uy)


# Stand-in placements, Unity floor-local (x, y, z), per (tower, floor index). What each tests:
#   approach  - walking up to the stair entrance (open cage side, front-left of the column)
#   on_flight - halfway round the flight, past the door
#   behind_desk - using the desk row as cover
#   meeting   - inside the glass room
#   store     - inside the opaque STORE pocket: must be hidden from the rival
_fx, _fz = direction(150.0)
PROXIES = {
    ('A', 1): [(-2.5, 0.0, -7.3), (_fx * 1.7, H * 150.0 / 360.0, SZ + _fz * 1.7), (2.8, 0.0, -5.1),
               (-P.WIDTH * 0.3, 0.0, -P.MAIN_DEPTH * 0.55 - 0.5), (0.0, 0.0, -P.MAIN_DEPTH - P.SPINE_DEPTH * 0.5)],
    ('A', 0): [(-2.5, 0.0, -7.3)],
    ('A', 2): [(-2.5, 0.0, -7.3)],
    ('B', 1): [(0.0, 0.0, -1.0), (-2.5, 0.0, -7.3)],
}

floor_groups = []
for tower in ('A', 'B'):
    for level in range(P.FLOORS):
        tag = f'{tower}_F{level + 1}'
        slab, opaque, facade, pillar_xs = shell_pieces()
        stairs_bm, cage, door = stair_pieces(has_door=True)
        fixed, loose, room_glass = content_pieces()

        parts = [
            part(f'{tag}_Slab', place(join(*slab), tower, level), M_SLAB, smooth=None),
            part(f'{tag}_WallsOpaque', place(join(*opaque), tower, level), M_SERVICE, smooth=None),
            part(f'{tag}_Facade', place(join(*facade), tower, level), M_FACADE, smooth=None),
            part(f'{tag}_Stairs', place(stairs_bm, tower, level), M_STAIR),
            part(f'{tag}_GlassCage', place(join(*cage), tower, level), M_GLASS, smooth=None),
            part(f'{tag}_GlassRoom', place(join(*room_glass), tower, level), M_GLASS, smooth=None),
            part(f'{tag}_CoverFixed', place(join(*fixed), tower, level), M_FIXED, smooth=None),
            part(f'{tag}_PropsLoose', place(join(*loose), tower, level), M_LOOSE, smooth=None),
            part(f'{tag}_LockedDoor', place(door, tower, level), M_DOOR, smooth=None),
            part(f'{tag}_CeilingLights', place(join(*ceiling_panels()), tower, level), M_PANEL, smooth=None),
        ]
        if P.FACADE_GLASS:
            parts.append(part(f'{tag}_FacadeGlass', place(join(*facade_glass(pillar_xs)), tower, level), M_GLASS, smooth=None))
        for i, (ux, uy, uz) in enumerate(PROXIES.get((tower, level), [])):
            parts.append(part(f'{tag}_Proxy{i + 1}', place(proxy(ux, uy, uz), tower, level), M_SUIT))
        floor_groups.append(group(f'Tower{tower}_Floor{level + 1}', *parts))

        if level == 1:
            # The test floors are the ones whose sizes the brief fixes; check them numerically.
            expect(parts[0], size=(P.WIDTH, DEPTH, P.SLAB))
            expect(parts[1], size=(P.WIDTH, DEPTH, H))

# Roof slab over each stack, so the top floor has a ceiling. It carries the stairwell opening too.
for tower in ('A', 'B'):
    slab, _, _, _ = shell_pieces()
    part(f'{tower}_RoofSlab', place(join(*slab), tower, P.FLOORS), M_SLAB, smooth=None)

# Player stand-in size check, standing alone in the street beside the towers, out of every review view.
ref = part('Proxy_SizeReference', move(proxy(0.0, 0.0, 0.0), x=P.WIDTH), M_SUIT)
expect(ref, size=(2 * P.BODY_R, 2 * P.BODY_R, P.BODY_H), tol=0.02)
