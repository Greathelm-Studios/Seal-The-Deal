# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code, blender-modeling skill)
#  Generated : 2026-11-15
#  Prompt    : Alex: "building up the office assets, cuz rn they look very bland and roblox like and blockouty, i want
#              the assets to match the frutiger aero and gen x soft club 2000s office corporate vibe we got goin!"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : The finished (Aero / Soft Club) office furniture and props, one piece per build, under the same piece
#              names kit_pieces.py uses, so KitImport and the scenes pick them up unchanged. A separate file because
#              another session holds kit_pieces.py for the walls and floors; it reuses that file's grid, materials and
#              helpers by loading it, so the numbers are still defined once.
# -----------------------------------------------------------------------------
"""Aero / Soft Club office props for Seal the Deal.

Build one piece:
  node <skill>/scripts/mdl.mjs build ArtSource/Kit/props_aero.py --out ArtSource/Kit/build/<PIECE>.blend --set PIECE=<PIECE>

Same snap convention as kit_pieces.py (README.md): u() takes Unity extents, pivots as the old pieces, same envelopes.
Concepts and per-prop sizes: concepts/Props_Aero/ (approved sheets and README).

Only --set PIECE is supported. WHY: kit_pieces.py's params() runs first and rejects names it doesn't know.
"""
import math
import os

import bmesh
from modelkit import *

# WHY exec rather than import: kit_pieces.py builds its piece at the bottom of the file (PIECES[P.PIECE]()), and
# modelkit scripts run as __main__. Loading its text without that last call gives us its params, materials and
# helpers without building anything. The alternative, copying them here, would let the two files' numbers drift.
_KIT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'kit_pieces.py')
with open(_KIT, encoding='utf-8') as _f:
    _src = _f.read()
# WHY cut at the guard, not the call: kit_pieces.py rejects a PIECE it doesn't build (PC_Tower is ours alone).
_cut = _src.rfind('if P.PIECE not in PIECES:')
if _cut < 0:
    raise RuntimeError('kit_pieces.py no longer ends with its PIECE guard and call; update props_aero.py')
exec(compile(_src[:_cut], _KIT, 'exec'), globals())
KIT_PIECES = PIECES   # the old builders, kept for any piece not redone here

# Prop sizes, metres. Sources: concepts/Props_Aero/README.md (sheets 1-4) and README_part2.md (sheets 5-6), which give
# millimetres; envelopes are the old pieces' (kit_pieces.py P), kept so KitOfficeScene's placements still fit.
# WHY PIECE is repeated: modelkit's params() rejects a --set name a call doesn't declare.
PA = params(
    PIECE=P.PIECE,
    # ---- CRT monitor (sheet 5 option B; Alex picked it 2026-11-15). Face 0.55 x 0.34 kept from Monitor_Flat.
    CRT_W=0.55, CRT_D=0.36, CRT_H=0.46,   # envelope: face width, depth with the deep back, overall height
    CRT_FACE_H=0.34,       # front face height
    CRT_R=0.055,           # major shell corner radius. WHY 55, not the sheet's R25: in KitOffice even 40 read as a box
    CRT_LIP_D=0.03,        # silver front lip depth
    CRT_BEZEL=0.03,        # silver border round the screen (the sheet's early-2000s bezel)
    CRT_BLOCK_D=0.12,      # full-size front block depth before the taper
    CRT_CAP_W=0.41, CRT_CAP_H=0.25,   # rear cap (README_part2)
    CRT_VENTS=6, CRT_VENT_PITCH=0.012, CRT_VENT_L=0.065,   # side vent slots: count, spacing, length
    CRT_NECK_R=0.03,       # neck radius (README_part2 ø60)
    CRT_FOOT_W=0.28, CRT_FOOT_D=0.26,  # capsule foot
    FOOT_H=0.02,           # foot thickness
    BADGE_D=0.024,         # AONAGI badge ring diameter on bezels, metres. WHY 24, not the sheet's 12: 12 mm is under
                           # the 4 mm-stroke floor for geometry; 24 mm still reads as a badge
    # ---- PC tower (sheet 5): a new loose prop
    TOWER_W=0.19, TOWER_D=0.42, TOWER_H=0.40,
    TOWER_R=0.012,         # case corner radius (R12)
    TOWER_WIN_T=0.004,     # frosted side panel thickness
    TOWER_FOOT_H=0.008,    # rubber feet
    # ---- Sheets 1-4 (README.md)
    BENCH_FRAME_R=0.012,   # divider U-frame tube radius (README: 20 mm frame)
    RECEPTION_END_R=0.2,   # reception front-shell plan corner radius. WHY 200, not the README's R40: the sheet paints
                           # pillowed capsule ends, and 40 mm reads as a box corner at 4 m
    RECEPTION_RING_D=0.3,  # AONAGI ring on the reception front, metres (sheet 2: about the name's height)
    COPIER_DECK_H=0.37,    # paper deck height under the copier, metres: 1.05 less the drawing's 0.68 m machine
    COPIER_DRAWER_H=0.112, # each of the machine's two drawers, metres (the drawing's 0.24 m pair)
    COPIER_BAY_H=0.13,     # open output bay height, metres (the drawing)
    COL_EDGE=0.025,        # pale edge line along the column band's upper side (README: 25 mm)
    # ---- Doors (README_part2.md sheet 6)
    DOOR_TRIM_SET=0.003,   # silver jamb strip face sits this far back from the wall face, metres: a lit edge in the groove
    DOOR_TRIM_R=0.004,     # its rounded edge, metres
    LEAF_EDGE_R=0.003,     # leaf edge radius (README R3)
    LEAF_STRIP_X=0.27, LEAF_STRIP_W=0.16,   # vision strip centre from the hinge and its width, metres. WHY near the
                           # hinge: the pull goes at the free edge, and the README's 320 mm offset puts them apart
    LEAF_PULL_IN=0.19,     # bar pull's distance in from the free edge, metres (README: 400 from centre)
    LEAF_POCKET_D=0.016,   # pull pocket depth on each face, metres. WHY 16 a side: both faces get a pull inside 40 mm
    # ---- Lift (concepts/Lift_Aero/README.md; Alex approved the sheets 2026-11-15)
    LIFT_PORTAL_W=0.1,     # rolled silver portal band width round the opening, metres (sheet 1)
    LIFT_PORTAL_PROUD=0.05,  # how far it stands off the wall, metres
    LIFT_PORTAL_R=0.16,    # its outer corner radius, metres. WHY 160: the sheet's R60 inner roll plus the band
    LIFT_KICK_H=0.08,      # silver kick band at the foot of the landing and solid shaft walls, metres
    LIFT_IND_W=1.18, LIFT_IND_H=0.32, LIFT_IND_Y=2.66,   # smoked indicator capsule and its centre height, metres. WHY
                           # not the sheet's 720 x 180: KitSigns' LED label there is 1.12 x 0.28 at 2.66; the capsule holds it all
    LIFT_IND_W_RF=1.4,     # the roof frame's indicator width, metres: holds the full-width "RF 屋上" label
    LIFT_RIBBON_H=0.1,     # frosted-aqua ribbon at the foot of the glass shaft walls, metres (sheet 3)
    LIFT_CAP_H=0.3,        # head-house cap height above the roof storey's walls, metres (sheet 3: 140 mm roll on top)
    LIFT_RAIL_Y=0.95,      # car handrail centre height, metres (sheet 2)
    # ---- Keyboard (sheet 5): 0.44 x 0.14, wedge 15 -> 30 mm
    KB_W=0.44, KB_D=0.14,
    KB_H_FRONT=0.012, KB_H_REAR=0.026,   # shell top at front and rear. WHY under 15/30: the key field sits 3 mm on
                                         # top, so the keyboard lands at about 15 mm front, 29 mm rear
    KB_KEY_H=0.003,        # key field stands this far above the shell top (the caps are on the trim sheet)
)


# ---------------------------------------------------------------- shape helpers (Unity extents in, Blender out)

def rbox(x0, x1, y0, y1, z0, z1, r, segs=3):
    """A box from Unity extents with every edge rounded by r metres: the pillowed product-plastic edge."""
    bm = u(x0, x1, y0, y1, z0, z1)
    # WHY clamp: a bevel at or over half the thinnest side collapses that side into zero-area faces.
    r = min(r, 0.45 * min(abs(x1 - x0), abs(y1 - y0), abs(z1 - z0)))
    if r > 0:
        bevel(bm, r, segments=segs, angle=30)
    return bm


def lathe(profile, segments=24, cx=0.0, cz=0.0):
    """A solid of revolution round a vertical axis at Unity (cx, cz): profile is [(radius, y)] bottom to top.
    A zero radius at either end closes it to a point; otherwise the ends are capped flat."""
    bm = bmesh.new()
    rings = []
    for rad, y in profile:
        if rad <= 1e-6:
            rings.append([bm.verts.new((-cx, -cz, y))])
            continue
        ring = []
        for k in range(segments):
            a = 2 * math.pi * k / segments
            ring.append(bm.verts.new((-(cx + rad * math.cos(a)), -(cz + rad * math.sin(a)), y)))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1:
            continue
        if len(a) == 1:
            for k in range(segments):
                bm.faces.new((a[0], b[(k + 1) % segments], b[k]))
        elif len(b) == 1:
            for k in range(segments):
                bm.faces.new((a[k], a[(k + 1) % segments], b[0]))
        else:
            for k in range(segments):
                bm.faces.new((a[k], a[(k + 1) % segments], b[(k + 1) % segments], b[k]))
    if len(rings[0]) > 1:
        bm.faces.new(list(reversed(rings[0])))
    if len(rings[-1]) > 1:
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def rprism_xz(x0, x1, z0, z1, r, y0, y1, segs=6):
    """A vertical slab with rounded plan corners (radius r), from height y0 to y1: tabletops, cabinet plans."""
    return prism_xz(rounded_rect(x0, x1, z0, z1, r, segs), y0, y1)


def tube(points, radius, sides=8):
    """A round tube through Unity points [(x, y, z)]: wires, cords, handles, frames. Closed at both ends.
    WHY parallel transport: a fixed or switched side vector twists the rings where the path turns (zero-area faces)."""
    n = len(points)

    def norm(v):
        l = math.sqrt(sum(c * c for c in v)) or 1.0
        return tuple(c / l for c in v)

    def cross(a, b):
        return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])

    tangents = [norm(tuple(points[min(i + 1, n - 1)][k] - points[max(i - 1, 0)][k] for k in range(3))) for i in range(n)]
    t0 = tangents[0]
    side = (0.0, 0.0, 1.0) if abs(t0[2]) < 0.9 else (1.0, 0.0, 0.0)
    u_vec = norm(cross(t0, side))
    rings = []
    for i, (x, y, z) in enumerate(points):
        t = tangents[i]
        d = sum(u_vec[k] * t[k] for k in range(3))
        u_vec = norm(tuple(u_vec[k] - d * t[k] for k in range(3)))   # keep the previous frame, minus the tangent
        v_vec = cross(t, u_vec)
        ring = []
        for k in range(sides):
            c, s_ = math.cos(2 * math.pi * k / sides) * radius, math.sin(2 * math.pi * k / sides) * radius
            px, py, pz = (x + c * u_vec[0] + s_ * v_vec[0], y + c * u_vec[1] + s_ * v_vec[1], z + c * u_vec[2] + s_ * v_vec[2])
            ring.append((-px, -pz, py))
        rings.append(ring)
    return sweep(rings)


def loft_xy(sections, segs=4):
    """A pillowed shell lofted along Unity Z through rounded-rectangle sections [(z, w, h, r, yc)], front to back
    or back to front: each is a w x h rounded rectangle (corner radius r) centred at height yc. Closed at both ends.
    For CRT backs and other tapered appliance shells, where a bevelled box can't taper."""
    rings = []
    for z, w, h, r, yc in sections:
        pts = rounded_rect(-w / 2, w / 2, yc - h / 2, yc + h / 2, min(r, w / 2 - 1e-4, h / 2 - 1e-4), segs)
        rings.append([(-x, -z, y) for x, y in pts])
    return sweep(rings)


def ring_badge(cx, cy, z_face, diameter, aqua_slot, seal_slot):
    """The AONAGI ring and its red hanko dot as a small badge on a front face (product bezels, fascias)."""
    ring, dot = ring_logo(cx, cy, z_face, diameter)
    return join(paint(ring, aqua_slot), paint(dot, seal_slot))


# ---------------------------------------------------------------- materials (new for the props; named for Unity)
# Each needs a row in KitImport.cs Materials (same values) or the import fails it as "not remapped".

def glow(mat, rgb, strength=3.0):
    """Coloured emission for Blender previews (FBX doesn't carry it; KitImport sets the Unity emission)."""
    tree = getattr(mat, 'node_tree', None)
    bsdf = tree.nodes.get('Principled BSDF') if tree else None
    if bsdf and 'Emission Color' in bsdf.inputs:
        bsdf.inputs['Emission Color'].default_value = (*rgb, 1.0)
        bsdf.inputs['Emission Strength'].default_value = strength


# WHY a separate smoked screen: Trim_Navy is a satin trim (smoothness 0.5); a 2002 screen is dark glossy glass that
# carries a reflection streak, which is most of what makes a monitor read as a monitor at 5 m (concept sheet 5).
M_SCREEN = material('Screen_Smoke', hexc('0E1626'), roughness=0.06)
# WHY chrome beside the brushed Frame_Aluminium: the concepts' necks, gas lifts and swivels are polished; one
# polished highlight per prop is the Aero "glint" (art bible §15 allows brushed metal, REQUEST.md asks for chrome).
M_CHROME = material('Chrome', hexc('DCE3E8'), metallic=1.0, roughness=0.08)
# Power and status LEDs: the bible's cyan (#20D4E6, "small luminous emphasis; indicators").
M_LED = material('LED_Cyan', hexc('20D4E6'), roughness=0.3)
glow(M_LED, (0.13, 0.83, 0.90))
# WHY a prop frost of its own, not the kit's Glass_Frosted: the concepts' frosted parts (divider, bottle, reception band,
# tray baskets) read as clearly aqua acrylic, while Glass_Frosted is a pale 65 %-opaque wall frost that came out grey on
# the props in KitOffice (Alex, 2026-11-15: "it doesnt match the references still"). Partitions keep Glass_Frosted.
M_FROST = material('Plastic_Aqua_Frost', hexc('8FDCE0'), roughness=0.2)


# Trim-sheet detail bands (trim/trim_layout.json). A face in one of these gets that band's relief and colour from the
# Painter sheet (keys, vent slots, perforation, buttons, ribs, panel seams) instead of modelled detail. Colours here are
# only the Blender preview's; after the unwrap, apply_trim.py merges every sheet material into Trim_Aero.
M_T_KEYS = material('Trim_Keys', hexc('E8EEF2'), roughness=0.3)
M_T_VENTS = material('Trim_Vents', hexc('5C6B80'), roughness=0.3)
M_T_GRILLE = material('Trim_Grille', hexc('8C98A6'), metallic=0.8, roughness=0.35)
M_T_BUTTONS = material('Trim_Buttons', hexc('DDE6EA'), roughness=0.2)
M_T_RIBBED = material('Trim_Ribbed', hexc('BCC7CF'), metallic=0.85, roughness=0.35)
M_T_PANEL = material('Trim_Panel', hexc('E6ECEE'), roughness=0.2)
# Print band (trim/trim_layout.json "print", art from trim/painter/make_print.py): faces in these get a fixed piece of
# artwork, placed by apply_trim.py. Colours are the Blender preview's only.
M_P_RING = material('Print_Ring', hexc('45BFC7'), roughness=0.3)
M_P_TAG = material('Print_Tag', hexc('F4F8F8'), roughness=0.3)
M_P_COPY = material('Print_Copy', hexc('0E1626'), roughness=0.1)
M_P_NAGI = material('Print_Can_NAGI', hexc('244FB9'), roughness=0.3)
M_P_MIZU = material('Print_Can_MIZU', hexc('FFFFFF'), roughness=0.3)
M_P_AO = material('Print_Can_AO', hexc('111B38'), roughness=0.3)
M_P_LOGO = material('Print_Logo', hexc('F4F8F8'), roughness=0.3)   # the AONAGI ring above its wordmark


def badge_disc(cx, cy, z_face, diameter, thick=0.0015):
    """A round print plate on a front face (the AONAGI ring from the sheet), standing `thick` proud, centred at
    Unity (cx, cy) on the face at z_face."""
    disc = cylinder(diameter / 2, thick, segments=40, axis='Y')
    move(disc, -cx, -(z_face + thick / 2 - 0.0005), cy)
    return disc


def paint_sides(bm, side_slot, cap_slot):
    """Slot per face of a turned part: the sides (for a wrap print) and the near-flat top and bottom (caps)."""
    bm.normal_update()
    for f in bm.faces:
        f.material_index = cap_slot if abs(f.normal.z) > 0.7 else side_slot
    return bm


# ---------------------------------------------------------------- computers (concepts/Props_Aero/README_part2.md, sheet 5)

def squircle(w, h, n, cx=0.0, cy=0.0, segs=48):
    """Outline of a superellipse |x/a|^n + |y/b|^n = 1 (a = w/2, b = h/2), counter-clockwise from +x: the soft
    "squircle" of 2000s moulded plastics. n = 2 is an ellipse; 4 to 6 reads as a pillowed rounded rectangle."""
    pts = []
    for k in range(segs):
        t = 2 * math.pi * k / segs
        c, s = math.cos(t), math.sin(t)
        pts.append((cx + w / 2 * math.copysign(abs(c) ** (2.0 / n), c), cy + h / 2 * math.copysign(abs(s) ** (2.0 / n), s)))
    return pts


def loft_sections(sections, close_ends=True):
    """A shell lofted along Unity Z through outlines in the (x, y) plane: sections = [(z, [(x, y), ...])], every
    outline with the same point count. Swelling the outlines along z gives moulded, bulging forms."""
    rings = [[(-x, -z, y) for x, y in outline] for z, outline in sections]
    return sweep(rings, close_ends)


def swell(t, bulge):
    """0..1 -> a pillow profile: 1 - bulge at both ends, 1 in the middle (a cosine), for lofts that swell."""
    return 1.0 - bulge * (1.0 - math.sin(math.pi * t)) ** 1.5


def monitor_crt():
    """A compact pearl CRT monitor (sheet 5 option B, Alex 2026-11-15): silver front lip, smoked domed screen,
    tapered deep back with side vents, chrome neck on a capsule foot, cyan power LED and the AONAGI badge.
    Keeps the Monitor_Flat name, so the scene and the throw system use it unchanged. Faces +Z; pivot at the bottom,
    under the foot's centre (the shell's middle). Loose prop: box collider.
    Alex kept this version over the moulded hero test (2026-11-15: "i actually like how it was before")."""
    W, D, H = PA.CRT_W, PA.CRT_D, PA.CRT_H
    zf, zb = D / 2, -D / 2                 # front face and rear cap, metres from the pivot
    y0 = H - PA.CRT_FACE_H                 # shell bottom: the face is 0.34 m high, standing on the neck
    yc = y0 + PA.CRT_FACE_H / 2
    lip = PA.CRT_LIP_D                     # silver front lip depth
    # Pearl shell: full face for the front block, then the taper to the rear cap (README_part2: 410 x 250 cap).
    shell = loft_xy([
        (zf - lip + 0.004, W, PA.CRT_FACE_H, PA.CRT_R, yc),
        (zf - PA.CRT_BLOCK_D, W, PA.CRT_FACE_H, PA.CRT_R, yc),
        (zb + 0.06, PA.CRT_CAP_W + 0.06, PA.CRT_CAP_H + 0.05, PA.CRT_R, yc + 0.01),
        (zb + 0.008, PA.CRT_CAP_W, PA.CRT_CAP_H, PA.CRT_R * 0.8, yc + 0.012),
        (zb, PA.CRT_CAP_W - 0.016, PA.CRT_CAP_H - 0.016, PA.CRT_R * 0.6, yc + 0.012),
    ])
    # Side vent bank: six rounded slots cut into each flank of the front block (navy recess faces).
    vents = []
    for side in (-1, 1):
        for k in range(PA.CRT_VENTS):
            y = yc - 0.03 + k * PA.CRT_VENT_PITCH
            vents.append(u(side * W / 2 - 0.004, side * W / 2 + 0.004, y - 0.003, y + 0.003,
                           zf - PA.CRT_BLOCK_D + 0.012, zf - PA.CRT_BLOCK_D + 0.012 + PA.CRT_VENT_L))
    shell = boolean(shell, paint(join(*vents), 1))
    # Silver front lip with the screen opening: a rounded frame (outer face, minus the screen window).
    # WHY a loft, not a bevelled box: a 24 mm-deep lip can't take a 40 mm bevel; lofting rounded sections gives the
    # face the same soft corners as the shell behind it, with a small roll onto the front.
    lip_bm = loft_xy([(zf - 0.006, W - 0.012, PA.CRT_FACE_H - 0.012, PA.CRT_R - 0.006, yc),
                      (zf - 0.012, W, PA.CRT_FACE_H, PA.CRT_R, yc),
                      (zf - lip, W, PA.CRT_FACE_H, PA.CRT_R, yc)])
    sw, sh = W - 2 * PA.CRT_BEZEL, PA.CRT_FACE_H - 2 * PA.CRT_BEZEL
    # A domed tube face: the sheet's "smoked curved screen", bulging up to 9 mm in front of the lip.
    screen = loft_xy([(zf - lip, sw + 0.004, sh + 0.004, 0.03, yc), (zf - 0.01, sw, sh, 0.03, yc),
                      (zf - 0.004, sw - 0.03, sh - 0.03, 0.045, yc), (zf - 0.001, sw - 0.12, sh - 0.1, 0.06, yc)])
    led = u(W / 2 - 0.03, W / 2 - 0.022, y0 + 0.008, y0 + 0.016, zf - 0.007, zf - 0.004)
    badge = ring_badge(0.0, y0 + PA.CRT_BEZEL / 2, zf - 0.006, PA.BADGE_D, 3, 6)
    neck = lathe([(PA.CRT_NECK_R + 0.01, PA.FOOT_H), (PA.CRT_NECK_R, PA.FOOT_H + 0.02), (PA.CRT_NECK_R, y0 + 0.004),
                  (PA.CRT_NECK_R + 0.012, y0 + 0.006)], 20)
    foot = rprism_xz(-PA.CRT_FOOT_W / 2, PA.CRT_FOOT_W / 2, -PA.CRT_FOOT_D / 2, PA.CRT_FOOT_D / 2, PA.CRT_FOOT_D / 2 - 0.01,
                     0.0, PA.FOOT_H, 8)
    bevel(foot, 0.005, segments=2, angle=30)
    ob = part('Kit_Monitor_Flat', join(paint(shell, 0), paint(lip_bm, 2), paint(screen, 4), paint(led, 5), badge,
                                       paint(neck, 7), paint(foot, 0)),
              [M_WALL, M_TRIM, M_FRAME, M_AQUA, M_SCREEN, M_LED, M_SEAL, M_CHROME], smooth=35)
    expect(ob, size=(W, D, H), tol=0.002)
    collider(ob, 'box')


def chair_task_aero():
    """Task chair (sheet 1), moulded: a pillowed navy seat cushion in a swollen pearl tray shell, a squircle back
    cushion that curves round the sitter in a pearl shell, looped pearl tube arms with navy pads, a chrome gas lift in
    an aqua shroud, and a silver five-star base with tapered rounded spokes on five casters. Faces +Z; pivot bottom
    centre. Hull collider (loose prop)."""
    sy = P.TASK_SEAT

    def pillow(w, d, y0, y1, n, bulge, cz=0.0, rings=7):
        """A cushion: squircle plan sections stacked from y0 to y1, swelling in the middle and rounding to the top."""
        out = []
        for i in range(rings):
            t = i / (rings - 1)
            s = swell(t, bulge) if t < 1 else 0.55
            y = y0 + (y1 - y0) * (math.sin(t * math.pi / 2) if i < rings - 1 else 1.0)
            pts = squircle(w * s, d * s, n, 0.0, cz, 40)
            out.append([(-x, -z, y) for x, z in pts])
        return sweep(out)

    # WHY a tall tray and a smaller cushion: on the sheet a thick pearl rim wraps the navy seat on every side.
    seat_shell = pillow(0.5, 0.49, sy - 0.09, sy - 0.008, 3.0, 0.14, 0.015)
    cushion = pillow(0.43, 0.42, sy - 0.05, sy, 3.6, 0.1, 0.025)

    def back_pillow(w, h, z_back, depth, n, curl, bulge, yb):
        """A back panel: squircle outlines in the (x, y) plane stacked forward from z_back, swelling, then bent round
        the sitter in plan (its sides come forward by `curl`)."""
        secs = []
        rings = 6
        for i in range(rings):
            t = i / (rings - 1)
            s = swell(t, bulge)
            secs.append((z_back + depth * t, squircle(w * s, h * s, n, 0.0, yb + h / 2, 40)))
        bm = loft_sections(secs)
        for v in bm.verts:
            x = -v.co.x
            v.co.y -= curl * (x / (w / 2)) ** 2      # Unity +Z is Blender -Y
        return bm

    back_shell = back_pillow(0.48, 0.63, -0.275, 0.055, 3.2, 0.05, 0.12, sy - 0.04)   # down into the seat tray, as one shell
    back_cush = back_pillow(0.41, 0.5, -0.232, 0.05, 3.6, 0.045, 0.18, sy + 0.05)
    for b in (back_shell, back_cush):
        tilt_x(b, -6.0, sy, -0.2)
    spine = rbox(-0.05, 0.05, sy - 0.06, sy + 0.12, -0.26, -0.21, 0.02, 3)
    arms, pads = [], []
    for sx in (-1, 1):
        x = sx * 0.255
        loop = [(x, sy - 0.045, -0.13), (x, 0.6, -0.12)]
        loop += [(x, 0.6 + 0.045 * math.sin(math.pi / 2 * k / 5), -0.12 + 0.045 * (1 - math.cos(math.pi / 2 * k / 5)))
                 for k in range(1, 6)]
        loop += [(x, 0.645, 0.075)]
        loop += [(x, 0.645 - 0.045 * (1 - math.cos(math.pi / 2 * k / 5)), 0.075 + 0.045 * math.sin(math.pi / 2 * k / 5))
                 for k in range(1, 6)]
        loop += [(x, sy - 0.045, 0.13)]
        arms.append(tube(loop, 0.019, 12))
        pad = pillow(0.06, 0.22, 0.655, 0.69, 3.0, 0.15, 0.0, 5)
        move(pad, -x, 0, 0)
        pads.append(pad)
    stem = lathe([(0.03, 0.075), (0.024, 0.09), (0.024, sy - 0.07), (0.04, sy - 0.06)], 20)
    shroud = lathe([(0.026, 0.17), (0.034, 0.185), (0.035, 0.26), (0.034, 0.335), (0.026, 0.35)], 24)
    base, wheels = five_star(0.26, 0.095)
    ob = part('Kit_Chair_Task', join(paint(join(seat_shell, back_shell, spine, *arms), 0),
                                     paint(join(cushion, back_cush, *pads, wheels), 1),
                                     paint(base, 2), paint(stem, 3), paint(shroud, 4)),
              [M_WALL, M_TRIM, M_FRAME, M_CHROME, M_AQUA], smooth=40)
    collider(ob, 'hull')


def moulded(x0, x1, y0, y1, z0, z1, n=5.0, r_lo=0.0, r_hi=0.02, axis='y', steps=5, segs=48):
    """A moulded-plastic block from Unity extents: a squircle cross-section (exponent n, the 2000s product corner)
    swept along `axis`, with its two ends rolled over by quarter-rounds of r_lo (start) and r_hi (end) metres.
    axis 'y' stands it up (cabinets, bodies, tops: r_hi rounds the top edge); axis 'z' faces it forward (drawer
    fronts, doors, panels: r_hi rounds the front edge). The alternative, a bevelled box, keeps flat faces meeting at
    one small round; the concepts' products read as swollen, continuous mouldings (Alex 2026-11-15)."""
    if axis == 'y':
        a0, a1, b0, b1, c0, c1 = x0, x1, z0, z1, y0, y1
    else:
        a0, a1, b0, b1, c0, c1 = x0, x1, y0, y1, z0, z1
    w, h = a1 - a0, b1 - b0
    ca, cb = (a0 + a1) / 2, (b0 + b1) / 2
    lim = 0.45 * min(w, h)
    r_lo, r_hi = min(r_lo, lim, (c1 - c0) / 2), min(r_hi, lim, (c1 - c0) / 2)
    levels = []
    if r_lo > 0:
        for j in range(steps + 1):
            t = math.pi / 2 * j / steps
            levels.append((c0 + r_lo * (1 - math.cos(t)), r_lo * (1 - math.sin(t))))
    else:
        levels.append((c0, 0.0))
    if r_hi > 0:
        for j in range(steps + 1):
            t = math.pi / 2 * j / steps
            levels.append((c1 - r_hi + r_hi * math.sin(t), r_hi * (1 - math.cos(t))))
    else:
        levels.append((c1, 0.0))
    rings = []
    for c, inset in levels:
        pts = squircle(w - 2 * inset, h - 2 * inset, n, ca, cb, segs)
        if axis == 'y':
            rings.append([(-pa, -pb, c) for pa, pb in pts])
        else:
            rings.append([(-pa, -c, pb) for pa, pb in pts])
    return sweep(rings)


def copier_aero():
    """The multifunction copier of sheet 2 at the drawing's own proportions (0.62 x 0.66 x ~0.68 m), standing on a
    matching pearl paper deck so the whole stays 1.05 m tall like a floor office copier (Alex 2026-11-15 chose this:
    the drawing is nearly as tall as wide, the kit keeps crouch-cover height). From the bottom: silver plinth, deck with
    one big drawer, a shadow seam, the machine's two drawers with aqua capsule grips, a shelf band, the wide open output
    bay with a silver paper tray, the stacked AONAGI logo right of the bay, the scanner block overhanging the bay, a
    grey console on its right (複写 / COPY display, keys, a big aqua start button, a small aqua key, a cyan LED), the lid
    unit with its smoked lid, and side vents on the left. Front +Z. Pivot bottom centre. Box collider. Modelled in the
    README's frame (+X = the reader's right) and mirrored once (viewer_x)."""
    W, D, H = P.COPIER_W, P.COPIER_D, P.COPIER_H
    zf = D / 2
    front = zf - 0.045                        # the machine's and deck's front faces. WHY 45 mm back: the console
                                              # stands that far proud of the scanner, as the drawing's grey mass does
    # ---- paper deck
    plinth = moulded(-W / 2 + 0.014, W / 2 - 0.014, 0.0, 0.045, -D / 2 + 0.014, front - 0.012, 6.0, 0.0, 0.012)
    deck = moulded(-W / 2 + 0.006, W / 2 - 0.006, 0.04, PA.COPIER_DECK_H, -D / 2 + 0.006, front, 6.0, 0.01, 0.012)
    seam = moulded(-W / 2 + 0.018, W / 2 - 0.018, PA.COPIER_DECK_H - 0.004, PA.COPIER_DECK_H + 0.012,
                   -D / 2 + 0.02, front - 0.012, 6.0, 0.0, 0.0)
    y = PA.COPIER_DECK_H + 0.008               # the machine's underside
    machine = moulded(-W / 2, W / 2, y, 0.985, -D / 2, front, 6.0, 0.018, 0.035)
    # ---- the drawing, from the bottom: drawers (0.24), shelf band (0.08), bay (0.13), scanner (0.15), lid (0.06)
    dh = PA.COPIER_DRAWER_H
    drawers, grips = [], []
    rows = [(0.065, PA.COPIER_DECK_H - 0.03)] + [(y + 0.012 + k * (dh + 0.008), y + 0.012 + k * (dh + 0.008) + dh)
                                                  for k in range(2)]
    for i, (y0_, y1_) in enumerate(rows):
        x0_ = -W / 2 + 0.02 if i == 0 else -0.215   # the deck drawer runs full width; the machine's leave the left pillar
        drawers.append(moulded(x0_, W / 2 - 0.02, y0_, y1_, front - 0.004, zf - 0.008, 7.0, 0.0, 0.008, axis='z'))
        gy = (y0_ + y1_) / 2 + (0.01 if i else 0.04)
        grips.append(moulded(0.0, 0.14, gy - 0.016, gy + 0.016, zf - 0.012, zf - 0.004, 2.6, 0.0, 0.004, axis='z'))
    y_band = rows[-1][1] + 0.01
    y_bay0, y_bay1 = y_band + 0.07, y_band + 0.07 + PA.COPIER_BAY_H
    bay = rbox(-0.245, 0.115, y_bay0, y_bay1, front - 0.26, zf + 0.05, 0.02, 3)
    machine = boolean(machine, paint(bay, 9))
    tray = rbox(-0.235, 0.105, y_bay0, y_bay0 + 0.012, front - 0.24, front + 0.006, 0.005, 2)
    tilt_x(tray, -6.0, y_bay0 + 0.006, front - 0.12)
    tray_lip = rbox(-0.235, 0.105, y_bay0 - 0.004, y_bay0 + 0.016, front - 0.004, front + 0.008, 0.006, 2)
    logo = badge_disc(0.215, (y_bay0 + y_bay1) / 2 + 0.01, front, 0.1)
    # ---- scanner block: overhangs the bay, then the lid unit and smoked lid on top
    scanner = moulded(-W / 2, W / 2, y_bay1 - 0.005, 0.99, -D / 2 + 0.004, front + 0.008, 6.0, 0.03, 0.03)
    lid_unit = moulded(-W / 2 + 0.03, W / 2 - 0.03, 0.97, 1.025, -D / 2 + 0.03, front - 0.05, 6.0, 0.0, 0.018)
    lid = moulded(-W / 2 + 0.07, W / 2 - 0.07, 1.012, H, -D / 2 + 0.06, front - 0.09, 7.0, 0.0, 0.012)
    # ---- console: a chunky grey block on the scanner's right, its face leaning back 16 degrees
    cy0, cy1 = y_bay1 - 0.012, 0.98
    console = prism_zy([(zf, cy0), (zf - 0.03, cy1), (front - 0.12, cy1), (front - 0.12, cy0)], -0.035, W / 2)
    bevel(console, 0.026, segments=4, angle=25)
    lean = -math.degrees(math.atan2(0.03, cy1 - cy0))
    face_z, face_y = zf - 0.015 + 0.002, (cy0 + cy1) / 2

    def on_face(bm):
        """Puts a part made facing +Z round the origin onto the console's leaning face."""
        rotate(bm, 90.0, axis='X')               # its +Y (up) becomes Unity +Z (out of the face)
        move(bm, 0, -face_z, face_y)
        tilt_x(bm, lean, face_y, face_z)
        return bm
    lcd = on_face(rbox(-0.005, 0.165, -0.002, 0.003, -0.045, 0.02, 0.002, 1))
    lcd.normal_update()
    for f in lcd.faces:   # the display face carries 複写 / COPY from the sheet; its sides are smoked
        f.material_index = 6 if f.normal.y < -0.5 else 1
    keys = on_face(u(-0.005, 0.2, -0.002, 0.0025, 0.03, 0.062))   # under the display (Unity z maps to face-down)
    start = cylinder(0.038, 0.011, segments=32, base='bottom')
    move(start, -0.25, 0.0, 0.0)
    on_face(start)
    small = cylinder(0.012, 0.008, segments=16, base='bottom')
    move(small, -0.285, 0.055, 0.0)                               # above the start button
    on_face(small)
    led = cylinder(0.0045, 0.004, segments=10, base='bottom')
    move(led, -0.185, -0.05, 0.0)
    on_face(led)
    vent = u(-W / 2 - 0.0005, -W / 2 + 0.004, y_bay1 + 0.035, y_bay1 + 0.105, front - 0.16, front - 0.05)
    feet = feet4((-0.25, 0.25), (-0.27, 0.27), 0.018, 0.01)
    ob = part('Kit_Copier', viewer_x(join(paint(join(deck, machine, scanner, lid_unit, *drawers), 0), paint(lid, 1),
                                          paint(join(*grips, start, small), 2), paint(join(feet, seam), 3), paint(keys, 4),
                                          paint(vent, 5), lcd, paint(logo, 11), paint(join(plinth, tray, tray_lip), 8),
                                          paint(console, 9), paint(led, 10))),
              [M_WALL, M_SCREEN, M_AQUA, M_TRIM, M_T_BUTTONS, M_T_VENTS, M_P_COPY, M_P_RING, M_FRAME, M_MIST, M_LED,
               M_P_LOGO], smooth=40)
    collider(ob, 'box')


def pc_tower():
    """The new loose PC tower (sheet 5): pearl case, translucent frosted-aqua left flank, silver front fascia with
    a drive slot, three vents and a cyan-ringed power button, navy rubber feet. Faces +Z; pivot bottom centre.
    Stands on the floor beside a drawer pedestal. Loose prop: box collider."""
    W, D, H = PA.TOWER_W, PA.TOWER_D, PA.TOWER_H
    win_t = PA.TOWER_WIN_T
    zf = D / 2
    case = moulded(-W / 2 + win_t / 2, W / 2, PA.TOWER_FOOT_H, H, -D / 2, zf - 0.004, 6.0, 0.008, 0.015)
    fascia = rbox(-W / 2 + 0.012, W / 2 - 0.012, PA.TOWER_FOOT_H + 0.016, H - 0.016, zf - 0.012, zf - 0.002, 0.004, 2)
    # WHY 2 mm rounding on 6 mm-deep slots: a bevel as deep as the slot collapsed it into zero-area faces.
    slot = rbox(-0.055, 0.055, H - 0.075, H - 0.065, zf - 0.006, zf, 0.002, 1)
    vents = join(*[rbox(-0.055, 0.055, y - 0.004, y + 0.004, zf - 0.006, zf, 0.002, 1) for y in (0.054, 0.076, 0.098)])
    ring = cylinder(0.014, 0.003, segments=24, axis='Y')
    move(ring, 0, -(zf - 0.0025), H - 0.14)
    button = cylinder(0.0105, 0.004, segments=24, axis='Y')
    move(button, 0, -(zf - 0.002), H - 0.14)
    window = u(-W / 2, -W / 2 + win_t, 0.045, 0.375, -D / 2 + 0.035, zf - 0.035)
    feet_list = []
    for x, z in ((-0.07, -0.175), (0.07, -0.175), (-0.07, 0.175), (0.07, 0.175)):
        f = cylinder(0.009, PA.TOWER_FOOT_H, segments=12, base='bottom')
        move(f, -x, -z, 0)
        feet_list.append(f)
    ob = part('Kit_PC_Tower', join(paint(case, 0), paint(fascia, 1), paint(join(slot, vents), 2), paint(ring, 3),
                                   paint(button, 0), paint(window, 4), paint(join(*feet_list), 2)),
              [M_WALL, M_FRAME, M_TRIM, M_LED, M_FROST], smooth=35)
    expect(ob, size=(W, D, H), tol=0.002)
    collider(ob, 'box')


def keyboard_aero():
    """A pale low wedge keyboard (sheet 5): pearl shell, a key field from the trim sheet's key band (pearl caps on a
    mist well, relief in the normal map), an aqua trim lip along the front. Pivot bottom centre, front toward +Z.
    Loose prop: box collider. WHY keys from the sheet: modelled caps cost ~1,300 triangles a keyboard, and the scene
    places dozens; the sheet's baked caps read the same at desk distance."""
    W, D = PA.KB_W, PA.KB_D
    hf, hr = PA.KB_H_FRONT, PA.KB_H_REAR

    def top(z):   # shell top height at Unity z (front at +D/2)
        return hf + (hr - hf) * (D / 2 - z) / D

    shell = prism_zy([(D / 2, 0), (D / 2, hf), (-D / 2, hr), (-D / 2, 0)], -W / 2, W / 2)
    bevel(shell, 0.004, segments=2, angle=30)
    trim = u(-W / 2 + 0.01, W / 2 - 0.01, 0.004, 0.008, D / 2 - 0.0015, D / 2 + 0.0)
    z0, z1 = -D / 2 + 0.012, D / 2 - 0.02
    keys = prism_zy([(z1, top(z1) + PA.KB_KEY_H), (z0, top(z0) + PA.KB_KEY_H), (z0, top(z0) - 0.002), (z1, top(z1) - 0.002)],
                    -W / 2 + 0.016, W / 2 - 0.016)
    keys.normal_update()
    for f in keys.faces:   # only the top face carries the key band; the field's thin sides are mist
        f.material_index = 1 if f.normal.z > 0.5 else 2
    ob = part('Kit_Keyboard', join(paint(shell, 0), keys, paint(trim, 3)),
              [M_WALL, M_T_KEYS, M_MIST, M_AQUA], smooth=30)
    collider(ob, 'box')


def lathe_ring(profile, segments=24, cx=0.0, cz=0.0):
    """A ring of revolution round a vertical axis: `profile` is a closed [(radius, y)] cross-section (rims, sleeves)."""
    bm = bmesh.new()
    rings = []
    for rad, y in profile:
        rings.append([bm.verts.new((-(cx + rad * math.cos(2 * math.pi * k / segments)),
                                    -(cz + rad * math.sin(2 * math.pi * k / segments)), y)) for k in range(segments)])
    n = len(rings)
    for j in range(n):
        a, b = rings[j], rings[(j + 1) % n]
        for k in range(segments):
            bm.faces.new((a[k], a[(k + 1) % segments], b[(k + 1) % segments], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def rplate_xy(x0, x1, y0, y1, r, z0, z1, segs=6):
    """A plate standing on a front face: rounded corners in the (x, y) wall plane, from depth z0 to z1."""
    return prism_xy(rounded_rect(x0, x1, y0, y1, r, segs), z0, z1)


def tilt_x(bm, degrees, y_pivot, z_pivot):
    """Tilts a bmesh about a Unity-X axis through (y, z): positive leans its top toward Unity +Z (the front)."""
    rotate(bm, degrees, axis='X', center=(0.0, -z_pivot, y_pivot))
    return bm


def viewer_x(bm):
    """Mirrors a finished prop across x = 0. WHY: the concept READMEs give +X as the viewer's right when facing the
    front, but a kit prop faces Unity +Z, where the viewer's right is Unity -X; modelling in the README's numbers and
    mirroring once keeps the numbers traceable to the spec."""
    scale(bm, -1.0, 1.0, 1.0)
    return bm


def feet4(xs, zs, r, h, segs=10):
    """Four round feet standing on the floor at Unity (x, z) for each x in xs and z in zs."""
    out = []
    for x in xs:
        for z in zs:
            f = cylinder(r, h, segments=segs, base='bottom')
            move(f, -x, -z, 0)
            out.append(f)
    return join(*out)


# ---------------------------------------------------------------- workstation set (README.md sheet 1)

def workstation_bench():
    """Back-to-back bench (sheet 1): two crowned pearl tops on a silver rim, softened pearl end slabs on navy rubber,
    two three-drawer pedestals (one per side, each facing its user) with chrome pulls, a frosted-aqua divider in a
    round silver U-frame at the 1.15 m cover line, chrome cable grommets. Pivot bottom centre. Box collider (cover)."""
    L, D, H, T = P.BENCH_L, P.BENCH_D, P.DESK_H, P.SCREEN_TOP
    pearl, silver, chrome, navy = [], [], [], []
    for z0, z1 in ((-D / 2, -0.001), (0.001, D / 2)):
        top = moulded(-L / 2, L / 2, H - 0.028, H, z0, z1, 8.0, 0.006, 0.012)   # the sheet's crowned moulded top
        pearl.append(top)
        silver.append(rprism_xz(-L / 2 + 0.004, L / 2 - 0.004, z0 + (0.004 if z0 < 0 else 0), z1 - (0.004 if z1 > 0 else 0),
                                0.056, H - 0.04, H - 0.028, 8))
    for sx in (-1, 1):
        x0, x1 = sorted((sx * (L / 2 - 0.04), sx * (L / 2 - 0.002)))
        pearl.append(moulded(x0, x1, 0.008, H - 0.032, -0.72, 0.72, 6.0, 0.01, 0.012))
        navy.append(u(x0 + 0.005, x1 - 0.005, 0, 0.008, -0.65, 0.65))
    for xc, side in ((-0.9, 1), (0.9, -1)):
        zf = side * (D / 2 - 0.045)                      # pedestal front face
        zb = zf - side * P.PED_D
        z0, z1 = sorted((zb, zf - side * 0.008))
        pearl.append(moulded(xc - P.PED_W / 2, xc + P.PED_W / 2, 0.02, P.PED_H, z0, z1, 6.0, 0.012, 0.015))
        for y0, y1 in ((0.05, 0.24), (0.245, 0.415), (0.42, 0.59)):
            f0, f1 = sorted((zf - side * 0.008, zf - side * 0.002))
            pearl.append(moulded(xc - 0.184, xc + 0.184, y0, y1, f0, f1, 7.0, 0.003 if side < 0 else 0.0,
                                 0.003 if side > 0 else 0.0, axis='z'))   # pillowed front on the user's side
            c0, c1 = sorted((zf - side * 0.004, zf))
            chrome.append(rbox(xc - 0.055, xc + 0.055, y1 - 0.04, y1 - 0.028, c0, c1, 0.0018, 1))
        navy.append(u(xc - 0.18, xc + 0.18, 0.0, 0.02, z0 + 0.02, z1 - 0.02))
    fr = PA.BENCH_FRAME_R
    ytop = T - fr
    xs = L / 2 - 0.11
    path = [(-xs, H - 0.005, 0.0), (-xs, ytop - 0.04, 0.0)]
    path += [(-xs + 0.04 * (1 - math.cos(math.pi / 2 * k / 6)), ytop - 0.04 + 0.04 * math.sin(math.pi / 2 * k / 6), 0.0)
             for k in range(1, 6)]
    path += [(-xs + 0.04, ytop, 0.0), (xs - 0.04, ytop, 0.0)]
    path += [(xs - 0.04 + 0.04 * math.sin(math.pi / 2 * k / 6), ytop - 0.04 + 0.04 * math.cos(math.pi / 2 * k / 6), 0.0)
             for k in range(1, 6)]
    path += [(xs, ytop - 0.04, 0.0), (xs, H - 0.005, 0.0)]
    silver.append(tube(path, fr, 10))
    plate = rplate_xy(-xs + 0.005, xs - 0.005, H - 0.005, ytop - 0.004, 0.035, -0.009, 0.009)
    for xg, zg in ((-0.85, -0.68), (0.85, 0.68)):
        g = cylinder(0.03, 0.004, segments=20, base='bottom'); move(g, -xg, -zg, H - 0.0035); chrome.append(g)
        g = cylinder(0.02, 0.0045, segments=16, base='bottom'); move(g, -xg, -zg, H - 0.0035); navy.append(g)
    # Asset tag on the outer face of the -X end slab (README sheet 1: 120 x 35, facing outward).
    tag = u(-L / 2, -L / 2 + 0.002, 0.53, 0.565, -0.36, -0.24)
    ob = part('Kit_Workstation_Bench', join(paint(join(*pearl), 0), paint(join(*silver), 1), paint(join(*chrome), 2),
                                           paint(join(*navy), 3), paint(plate, 4), paint(tag, 5)),
              [M_WALL, M_FRAME, M_CHROME, M_TRIM, M_FROST, M_P_TAG], smooth=35)
    expect(ob, size=(L, D, T), tol=0.003)
    collider(ob, 'box')


def five_star(r_tip, y_hub, hub_r=0.0475, caster_r=0.025, segs=5):
    """A silver five-star chair base with navy casters: (silver bmesh, navy bmesh). Spokes radial in plan."""
    silver = [lathe([(hub_r * 0.8, y_hub - 0.02), (hub_r, y_hub - 0.012), (hub_r, y_hub + 0.012),
                     (hub_r * 0.8, y_hub + 0.02)], 20)]
    navy = []
    for k in range(segs):
        deg = 360.0 * k / segs + 90.0
        sp = prism_xz([(0.03, -0.022), (r_tip, -0.014), (r_tip, 0.014), (0.03, 0.022)], y_hub - 0.012, y_hub + 0.01)
        bevel(sp, 0.006, segments=2, angle=30)
        rotate(sp, deg, axis='Z')
        silver.append(sp)
        # the spoke tip, in Blender coordinates, after the same turn (prism_xz puts Unity +x at Blender -x)
        a = math.radians(deg)
        bx, by = -r_tip * math.cos(a), -r_tip * math.sin(a)
        fork = cylinder(0.008, y_hub - 0.012 - caster_r, segments=8, base='bottom')
        move(fork, bx, by, caster_r)
        silver.append(fork)
        wheel = cylinder(caster_r, 0.026, segments=14, axis='X')
        rotate(wheel, deg, axis='Z')
        move(wheel, bx, by, caster_r)
        navy.append(wheel)
    return join(*silver), join(*navy)


def chair_pod_aero():
    """Pod chair (sheet 1): a pearl bucket shell with wrap-round sides hugging navy seat and back pads, on a chrome
    swivel stem and a silver disc foot. Faces +Z; pivot bottom centre. Hull collider (loose prop)."""
    W, Dd, sy, bt = P.CHAIR_W, P.CHAIR_D, P.CHAIR_SEAT_H, P.CHAIR_BACK_H
    # A true bucket: a heavily rounded block hollowed from above and the front, its sides sloping from arm height at
    # the front up into the back (sheet 1's pearl egg shell), instead of a seat, a back and two flat wings.
    outer = rbox(-W / 2, W / 2, 0.40, bt, -Dd / 2, Dd / 2, 0.075, 4)
    hollow = rbox(-W / 2 + 0.035, W / 2 - 0.035, sy - 0.0315, bt + 0.2, -Dd / 2 + 0.05, Dd / 2 + 0.2, 0.05, 4)
    slope = prism_zy([(Dd / 2 + 0.05, 0.6), (Dd / 2 + 0.05, bt + 0.2), (-0.12, bt + 0.2), (-0.031, 0.677)], -W, W)   # WHY off-grid: on round values the cut met the hollow on an edge (zero-area faces)
    seat = boolean(boolean(outer, paint(hollow, 0)), paint(slope, 0))
    back = bmesh.new()
    wings = []
    seat_pad = rprism_xz(-0.215, 0.215, -0.19, 0.235, 0.045, sy - 0.045, sy, 8)
    bevel(seat_pad, 0.018, segments=4, angle=25)
    back_pad = rbox(-0.19, 0.19, 0.49, bt - 0.04, -Dd / 2 + 0.048, -Dd / 2 + 0.088, 0.018, 4)
    stem = lathe([(0.03, 0.025), (0.025, 0.04), (0.025, 0.40)], 16)
    foot = lathe([(0.0, 0.0), (0.235, 0.0), (0.24, 0.008), (0.24, 0.017), (0.232, 0.025), (0.0, 0.025)], 32)
    ob = part('Kit_Chair_Pod', join(paint(join(seat, back, *wings), 0), paint(join(seat_pad, back_pad), 1),
                                    paint(stem, 2), paint(foot, 3)),
              [M_WALL, M_TRIM, M_CHROME, M_FRAME], smooth=35)
    collider(ob, 'hull')


def phone_desk_aero():
    """White desk phone (sheet 1): sloped pearl base, capsule handset on an aqua cradle, a smoked LCD, a keypad from
    the sheet's button band, a navy coiled cord. Faces +Z; pivot bottom centre. Loose prop: box collider."""
    w, d, h = P.PHONE_W, P.PHONE_D, P.PHONE_H
    zf, zr = d / 2, -d / 2
    yf, yr = 0.028, 0.065
    slope = math.degrees(math.atan2(yr - yf, d))
    base = prism_zy([(zf, 0.004), (zf, yf), (zr, yr), (zr, 0.004)], -0.098, w / 2)
    bevel(base, 0.006, segments=2, angle=30)
    hx = -0.054
    handset = join(rbox(hx - 0.031, hx + 0.031, 0.066, h, 0.04, 0.0925, 0.012, 3),
                   rbox(hx - 0.031, hx + 0.031, 0.066, h, -0.0925, -0.04, 0.012, 3),
                   rbox(hx - 0.02, hx + 0.02, 0.07, h - 0.006, -0.05, 0.05, 0.01, 2))
    cradle = rbox(hx - 0.033, hx + 0.033, 0.06, 0.066, -0.09, 0.09, 0.0025, 1)
    saddles = join(rbox(hx - 0.025, hx + 0.025, 0.02, 0.064, 0.055, 0.085, 0.006, 1),
                   rbox(hx - 0.025, hx + 0.025, 0.05, 0.064, -0.085, -0.055, 0.006, 1))

    def on_slope(bm, z_centre):
        y = yf + (yr - yf) * (zf - z_centre) / d
        move(bm, 0, 0, y)
        tilt_x(bm, slope, y, z_centre)
        return bm
    lcd = on_slope(rbox(0.024, 0.108, 0.0, 0.003, -0.071, -0.039, 0.001, 1), -0.055)
    # WHY 60 mm deep: the keypad face must stay in the lower half of the sheet's button band, under its round-button row
    pad = on_slope(u(0.03, 0.112, 0.0, 0.002, -0.005, 0.055), 0.025)
    coil, turns, n = [], 24, 8
    for i in range(turns * n + 1):
        a = 2 * math.pi * i / n
        coil.append((-0.1095 + 0.0045 * math.cos(a), 0.012 + 0.0045 * math.sin(a), -0.075 + 0.15 * i / (turns * n)))
    cord = tube(coil, 0.00125, 5)
    feet = feet4((-0.085, 0.105), (-0.09, 0.09), 0.006, 0.004, 8)
    ob = part('Kit_Phone_Desk', viewer_x(join(paint(join(base, handset, saddles), 0), paint(cradle, 1), paint(lcd, 2),
                                     paint(pad, 3), paint(join(cord, feet), 4))),
              [M_WALL, M_AQUA, M_SCREEN, M_T_BUTTONS, M_TRIM], smooth=35)
    collider(ob, 'box')   # loose prop: can be picked up and thrown


def doc_tray_aero():
    """Two-tier document tray (sheet 1): frosted-aqua baskets with side and rear lips and a low front lip, silver
    rear risers, a paper stack in the lower tier, navy feet. Pivot bottom centre. Loose prop: box collider."""
    w, d, h = P.TRAY_W, P.TRAY_D, P.TRAY_H
    frost = []
    for y in (0.008, 0.098):
        frost.append(rprism_xz(-w / 2, w / 2, -d / 2, d / 2, 0.01, y, y + 0.006, 6))
        frost.append(rbox(-w / 2, -w / 2 + 0.006, y, y + 0.066, -d / 2, d / 2 - 0.05, 0.002, 1))
        frost.append(rbox(w / 2 - 0.006, w / 2, y, y + 0.066, -d / 2, d / 2 - 0.05, 0.002, 1))
        frost.append(rbox(-w / 2, w / 2, y, y + 0.066, -d / 2, -d / 2 + 0.006, 0.002, 1))
        frost.append(rbox(-w / 2 + 0.006, w / 2 - 0.006, y, y + 0.018, d / 2 - 0.006, d / 2, 0.002, 1))
    silver = join(rbox(-0.135, -0.115, 0.0, h, -d / 2, -d / 2 + 0.028, 0.006, 2),
                  rbox(0.115, 0.135, 0.0, h, -d / 2, -d / 2 + 0.028, 0.006, 2))
    paper = u(-w / 2 + 0.02, w / 2 - 0.02, 0.014, 0.034, -d / 2 + 0.02, d / 2 - 0.03)
    feet = feet4((-0.115, 0.115), (-0.14, 0.14), 0.006, 0.008, 8)
    ob = part('Kit_Doc_Tray', join(paint(join(*frost), 0), paint(silver, 1), paint(paper, 2), paint(feet, 3)),
              [M_FROST, M_FRAME, M_WALL, M_TRIM], smooth=35)
    collider(ob, 'box')   # loose prop: can be picked up and thrown


# ---------------------------------------------------------------- desks and storage (README.md sheet 2)

def desk_sculpt_aero():
    """Sculpted desk (sheet 2): a crowned pearl top on a silver rim, two bowed tapering pearl slab legs, a frosted-
    aqua modesty panel on silver brackets, a chrome cable grommet, navy foot pads. Pivot bottom centre. Box collider."""
    W, D, H = P.DESK_W, P.DESK_D, P.DESK_H
    top = moulded(-W / 2, W / 2, H - 0.028, H, -D / 2, D / 2, 8.0, 0.006, 0.012)
    rim = rprism_xz(-W / 2 + 0.004, W / 2 - 0.004, -D / 2 + 0.004, D / 2 - 0.004, 0.056, H - 0.036, H - 0.028, 8)
    legs = []
    for sx in (-1, 1):
        pts_out, pts_in = [], []
        for i in range(9):
            t = i / 8
            y = 0.008 + (H - 0.036 - 0.008) * t
            bow = 0.035 * math.sin(math.pi * t)
            pts_out.append((sx * (W / 2 - 0.02 - bow), y))
            pts_in.append((sx * (W / 2 - 0.02 - (0.09 + (0.055 - 0.09) * t) - bow), y))
        outline = pts_out + list(reversed(pts_in))
        leg = prism_xy(outline, -0.36, 0.36)
        bevel(leg, 0.012, segments=2, angle=30)
        legs.append(leg)
    modesty = rplate_xy(-0.66, 0.66, 0.32, 0.68, 0.02, -0.281, -0.269)
    brackets = join(*[u(x - 0.01, x + 0.01, y - 0.01, y + 0.01, -0.295, -0.281) for x in (-0.62, 0.62) for y in (0.34, 0.66)])
    g = cylinder(0.03, 0.004, segments=20, base='bottom'); move(g, -0.52, 0.27, H - 0.0035)
    gn = cylinder(0.02, 0.0045, segments=16, base='bottom'); move(gn, -0.52, 0.27, H - 0.0035)
    feet = join(*[u(sx * (W / 2 - 0.07) - 0.035, sx * (W / 2 - 0.07) + 0.035, 0, 0.008, z - 0.05, z + 0.05)
                  for sx in (-1, 1) for z in (-0.3, 0.3)])
    ob = part('Kit_Desk_Sculpt', join(paint(join(top, *legs), 0), paint(join(rim, brackets), 1), paint(g, 2),
                                      paint(join(gn, feet), 3), paint(modesty, 4)),
              [M_WALL, M_FRAME, M_CHROME, M_TRIM, M_FROST], smooth=35)
    expect(ob, size=(W, D, H), tol=0.002)
    collider(ob, 'box')


def desk_reception_aero():
    """AONAGI reception desk (sheet 2): a pillowed pearl front shell with rounded ends under a crowned transaction
    cap and silver rim, a recessed frosted-aqua front band over a navy cavity, a lower staff worktop behind, a silver
    plinth over a navy shadow gap, a pearl end-plaque backing. The name, ring and plaque text are KitSigns labels on
    the front face (z = 0.45, unchanged). Front faces +Z. Pivot bottom centre. Box collider (cover)."""
    L, D, H = P.RECEPTION_L, P.RECEPTION_D, P.RECEPTION_H
    zf = D / 2
    r_end = PA.RECEPTION_END_R
    # WHY a squircle of n 5 on a 3.96 x 0.45 plan: its ends round over about 0.25 m, the sheet's capsule ends.
    front = moulded(-L / 2 + 0.02, L / 2 - 0.02, 0.06, H - 0.052, 0.0, zf, 5.0, 0.012, 0.02)
    front = boolean(front, paint(rplate_xy(-1.76, 1.76, 0.17, 0.41, 0.025, zf - 0.008, zf + 0.01), 3))
    band = rplate_xy(-1.755, 1.755, 0.175, 0.405, 0.022, zf - 0.006, zf - 0.002)
    rear = rprism_xz(-L / 2 + 0.06, L / 2 - 0.06, -D / 2 + 0.02, 0.02, 0.04, 0.06, 0.712, 6)
    work = rprism_xz(-L / 2 + 0.1, L / 2 - 0.1, -D / 2 + 0.02, 0.02, 0.03, 0.712, 0.74, 6)
    bevel(work, 0.006, segments=2, angle=30)
    cap = moulded(-L / 2, L / 2, H - 0.04, H, 0.02, zf, 6.0, 0.012, 0.018)
    rim = rprism_xz(-L / 2 + 0.006, L / 2 - 0.006, 0.026, zf - 0.006, min(r_end + 0.014, (zf - 0.032) / 2 - 0.001), H - 0.052, H - 0.04, 10)
    plinth = rprism_xz(-L / 2 + 0.06, L / 2 - 0.06, -D / 2 + 0.06, zf - 0.06, 0.08, 0.0, 0.05, 6)
    shadow = rprism_xz(-L / 2 + 0.07, L / 2 - 0.07, -D / 2 + 0.07, zf - 0.07, 0.07, 0.05, 0.062, 6)
    plaque = rplate_xy(-1.765, -1.435, 0.815, 0.915, 0.012, zf - 0.004, zf)   # front-right as seen: Unity -X
    # The AONAGI ring, left of the KitSigns name (as the reader sees it, Unity +X), as on sheet 2.
    mark = badge_disc(0.95, 0.6, zf, PA.RECEPTION_RING_D)
    ob = part('Kit_Desk_Reception', join(paint(join(front, rear, work, cap, plaque), 0), paint(join(rim, plinth), 1),
                                         paint(band, 2), paint(shadow, 3), paint(mark, 4)),
              [M_WALL, M_FRAME, M_FROST, M_TRIM, M_P_RING], smooth=35)
    collider(ob, 'box')


def storage_low_aero():
    """Low storage (sheet 2): a rounded pearl carcass and crowned cap, four softly rounded doors with chrome finger
    pulls, a frosted-aqua header ribbon, a silver plinth over a navy shadow strip. Front +Z. Pivot bottom centre.
    Box collider (crouch-height cover)."""
    L, D, H = P.STORE_L, P.STORE_D, P.STORE_H
    zf = D / 2
    body = moulded(-L / 2, L / 2, 0.033, H - 0.024, -D / 2, zf - 0.022, 6.0, 0.008, 0.0)
    cap = moulded(-L / 2, L / 2, H - 0.024, H, -D / 2, zf, 7.0, 0.004, 0.012)
    doors, pulls = [], []
    for xc in (-0.57, -0.19, 0.19, 0.57):
        doors.append(moulded(xc - 0.187, xc + 0.187, 0.064, 0.868, zf - 0.022, zf - 0.004, 8.0, 0.0, 0.007, axis='z'))
        px = xc + (0.13 if xc < 0 else -0.13)
        pulls.append(rbox(px - 0.009, px + 0.009, 0.43, 0.52, zf - 0.008, zf, 0.004, 2))
    header = rbox(-0.752, 0.752, 0.888, 0.96, zf - 0.016, zf - 0.006, 0.004, 1)
    header_back = u(-0.75, 0.75, 0.89, 0.958, zf - 0.022, zf - 0.016)
    plinth = rprism_xz(-0.765, 0.765, -0.19, 0.19, 0.02, 0.0, 0.025, 6)
    shadow = rprism_xz(-0.77, 0.77, -0.2, 0.2, 0.02, 0.025, 0.033, 6)
    ob = part('Kit_Storage_Low', join(paint(join(body, cap, *doors), 0), paint(join(*pulls), 1), paint(plinth, 2),
                                      paint(join(shadow, header_back), 3), paint(header, 4)),
              [M_WALL, M_CHROME, M_FRAME, M_TRIM, M_FROST], smooth=35)
    expect(ob, size=(L, D, H), tol=0.002)
    collider(ob, 'box')


# ---------------------------------------------------------------- amenities (README.md sheet 3)

def water_server_aero():
    """Floor water server (sheet 3): a rounded pearl tower with a silver waist, a recessed smoked dispensing bay with
    two taps and an aqua drip tray, a cyan status capsule, a silver collar and the inverted frosted-aqua bottle with
    two ridges. The 給水 / WATER label is a KitSigns texture on the front (z = 0.175). Faces +Z. Pivot bottom centre.
    Box collider."""
    w, H = P.SERVER_W, P.SERVER_H
    zf = w / 2
    lower = moulded(-w / 2, w / 2, 0.01, 0.655, -w / 2, zf, 5.0, 0.015, 0.02)
    lower = boolean(lower, paint(rbox(-0.135, 0.135, 0.33, 0.62, zf - 0.07, zf + 0.02, 0.02, 2), 4))
    upper = moulded(-w / 2, w / 2, 0.675, 0.94, -w / 2, zf, 5.0, 0.02, 0.035)
    waist = moulded(-w / 2 + 0.006, w / 2 - 0.006, 0.65, 0.68, -w / 2 + 0.006, zf - 0.006, 5.0, 0.0, 0.0)
    collar = lathe([(0.08, 0.94), (0.08, 0.97)], 24)
    bottle = lathe([(0.0425, 0.97), (0.0425, 0.995), (0.1, 1.015), (0.135, 1.04), (0.14, 1.07), (0.14, 1.13), (0.15, 1.14),
                    (0.15, 1.15), (0.14, 1.16), (0.14, 1.23), (0.15, 1.24), (0.15, 1.25), (0.14, 1.26), (0.14, 1.31),
                    (0.12, 1.34), (0.07, H), (0.0, H)], 28)
    taps, spouts, levers = [], [], []
    for sx in (-0.065, 0.065):
        taps.append(rbox(sx - 0.014, sx + 0.014, 0.5, 0.55, zf - 0.07, zf - 0.046, 0.006, 1))
        spouts.append(rbox(sx - 0.006, sx + 0.006, 0.48, 0.505, zf - 0.06, zf - 0.05, 0.002, 1))
        levers.append(rbox(sx - 0.0075, sx + 0.0075, 0.55, 0.58, zf - 0.058, zf - 0.046, 0.004, 1))
    tray = rbox(-0.13, 0.13, 0.33, 0.35, zf - 0.07, zf - 0.004, 0.006, 1)
    status = rbox(-0.0125, 0.0125, 0.708, 0.713, zf - 0.002, zf, 0.002, 1)
    feet = feet4((-0.12, 0.12), (-0.12, 0.12), 0.015, 0.01)
    mark = badge_disc(0.0, 0.845, zf, 0.065)   # the ring above the 給水 / WATER label (sheet 3)
    ob = part('Kit_Water_Server', join(paint(join(lower, upper, *taps), 0), paint(join(waist, collar, *spouts, feet), 1),
                                       paint(join(*levers, tray), 2), paint(status, 3), paint(bottle, 5), paint(mark, 6)),
              [M_WALL, M_FRAME, M_AQUA, M_LED, M_SCREEN, M_FROST, M_P_RING], smooth=35)
    collider(ob, 'box')


def umbrella_stand_aero():
    """Umbrella stand (sheet 3): a perforated silver sleeve (the sheet's grille band, in rings no taller than the
    band so the holes stay round), a rolled pearl rim, a frosted-aqua drip cup over a navy floor, and two furled navy
    umbrellas with chrome shafts and hook handles. Pivot bottom centre. Hull collider."""
    r = P.UMBRELLA_D / 2
    rings, y = [], 0.07
    while y < 0.519:
        y1 = min(y + 0.11, 0.52)
        rings.append(lathe_ring([(r - 0.003, y), (r, y), (r, y1), (r - 0.003, y1)], 24))
        y = y1
    rim = lathe_ring([(r - 0.015, 0.52), (r - 0.002, 0.52), (r, 0.53), (r, 0.54), (r - 0.004, 0.55), (r - 0.015, 0.55)], 24)
    cup = lathe([(r - 0.002, 0.0), (r - 0.002, 0.07)], 24)
    floor = cylinder(r - 0.01, 0.002, segments=20, base='bottom')
    move(floor, 0, 0, 0.07)
    navy, chrome = [], []
    for sx, lean in ((-0.045, 4.0), (0.045, -5.0)):
        fold = lathe([(0.004, 0.05), (0.02, 0.2), (0.032, 0.55), (0.028, 0.7), (0.009, 0.8)], 8)
        shaft = lathe([(0.004, 0.8), (0.004, 0.91)], 8)
        hook = [(0.0, 0.91, 0.0), (0.0, 0.955, 0.0)]
        hook += [(0.035 - 0.035 * math.cos(math.pi * k / 8), 0.955 + 0.035 * math.sin(math.pi * k / 8), 0.0) for k in range(1, 9)]
        hook += [(0.07, 0.935, 0.0)]
        hk = tube(hook, 0.006, 8)
        for bm in (fold, shaft, hk):
            rotate(bm, lean, axis='Y')
            move(bm, -sx, 0, 0)
        navy.append(fold)
        chrome += [shaft, hk]
    ob = part('Kit_Umbrella_Stand', join(paint(join(*rings), 0), paint(rim, 1), paint(cup, 2), paint(join(floor, *navy), 3),
                                         paint(join(*chrome), 4)),
              [M_T_GRILLE, M_WALL, M_FROST, M_TRIM, M_CHROME], smooth=35)
    collider(ob, 'hull')


def vending_machine_aero():
    """Drinks vending machine (sheet 3): a rounded pearl shell, an aqua header plate (its 自動販売機 / DRINKS label is a
    KitSigns texture at z = 0.335, unchanged), a recessed smoked display window with three shelves of fictional cans
    and bottles, aqua selection buttons, a silver payment pod with a smoked LCD, note slot and coin slot, a recessed
    pickup bay, silver feet. Back on the wall at z = -0.325. Front +Z. Pivot bottom centre. Box collider (full cover)."""
    W, D, H = P.VEND_W, P.VEND_D, P.VEND_H
    zf = D / 2
    shell = moulded(-W / 2, W / 2, 0.03, H, -zf, zf, 6.0, 0.015, 0.04)
    shell = boolean(shell, paint(join(rplate_xy(-0.37, 0.2, 0.65, 1.5, 0.025, zf - 0.08, zf + 0.02),
                                      rbox(-0.31, 0.31, 0.23, 0.44, zf - 0.1, zf + 0.02, 0.025, 2)), 4))
    glass = rplate_xy(-0.37, 0.2, 0.65, 1.5, 0.025, zf - 0.012, zf - 0.008)
    header = moulded(-0.38, 0.38, 1.56, 1.735, zf, zf + 0.01, 6.0, 0.0, 0.006, axis='z')
    pay = rplate_xy(0.24, 0.39, 0.89, 1.27, 0.015, zf - 0.003, zf + 0.003)
    lcd = rplate_xy(0.28, 0.35, 1.19, 1.235, 0.004, zf + 0.003, zf + 0.005)
    slot = u(0.278, 0.352, 0.925, 0.937, zf + 0.003, zf + 0.0045)
    coin = cylinder(0.0175, 0.002, segments=16, axis='Y')
    move(coin, -0.315, -(zf + 0.004), 1.07)
    shelves, cans, bottles, buttons = [], [], [], []
    for row, yb in enumerate((0.7, 0.96, 1.22)):
        shelves.append(u(-0.365, 0.195, yb - 0.008, yb, zf - 0.075, zf - 0.015))
        for xc in (-0.28, -0.155, -0.03, 0.095):
            if row == 1:   # MIZU bottles between a row of NAGI cans and a row of AO cans (sheet 3)
                prof = [(0.0, yb), (0.03, yb), (0.033, yb + 0.006), (0.033, yb + 0.09), (0.015, yb + 0.12),
                        (0.012, yb + 0.14), (0.0, yb + 0.14)]
                bottles.append(paint_sides(lathe(prof, 16, cx=xc, cz=zf - 0.045), 8, 0))
            else:
                prof = [(0.0, yb), (0.033, yb), (0.035, yb + 0.006), (0.035, yb + 0.12), (0.03, yb + 0.13), (0.0, yb + 0.13)]
                cans.append(paint_sides(lathe(prof, 16, cx=xc, cz=zf - 0.045), 7 if row == 0 else 9, 2))
            buttons.append(rbox(xc - 0.018, xc + 0.018, yb - 0.04, yb - 0.026, zf - 0.013, zf - 0.006, 0.005, 1))
    feet = feet4((-0.33, 0.33), (-0.22, 0.22), 0.03, 0.03, 12)
    ob = part('Kit_Vending_Machine', viewer_x(join(paint(shell, 0), *bottles, paint(join(header, *buttons), 1),
                                          paint(join(pay, feet), 2), paint(join(lcd, *shelves), 3),
                                          paint(join(slot, coin), 4), join(*cans), paint(glass, 6))),
              [M_WALL, M_AQUA, M_FRAME, M_SCREEN, M_TRIM, M_COBALT, M_GLASS, M_P_NAGI, M_P_MIZU, M_P_AO], smooth=35)
    collider(ob, 'box')


def slipper_cabinet_aero():
    """Slipper cabinet (sheet 3): a rounded pearl frame with a header, a frosted-aqua inner grid of six open cubbies,
    a silver base, a pearl plaque backing (its 室内履き / SLIPPERS label is a KitSigns texture at z = 0.175), and six
    pairs of mist slippers. Front +Z. Pivot bottom centre. Box collider."""
    W, D, H = P.SLIPPER_W, P.SLIPPER_D, P.SLIPPER_H
    zf = D / 2
    t = 0.03
    frame = join(rbox(-W / 2, -W / 2 + t, 0.025, H, -zf, zf - 0.002, 0.01, 2),
                 rbox(W / 2 - t, W / 2, 0.025, H, -zf, zf - 0.002, 0.01, 2),
                 rbox(-W / 2, W / 2, 0.61, H, -zf, zf - 0.002, 0.012, 2),
                 rbox(-W / 2 + t, W / 2 - t, 0.025, 0.04, -zf, zf - 0.002, 0.004, 1))
    grid = join(u(-W / 2 + t, W / 2 - t, 0.04, 0.61, -zf, -zf + 0.012),
                *[rbox(x - 0.004, x + 0.004, 0.04, 0.61, -zf + 0.012, zf - 0.006, 0.003, 1) for x in (-0.14, 0.14)],
                rbox(-W / 2 + t, W / 2 - t, 0.321, 0.329, -zf + 0.012, zf - 0.006, 0.003, 1))
    base = rprism_xz(-0.425, 0.425, -0.15, 0.15, 0.015, 0.0, 0.025, 6)
    plaque = rplate_xy(-0.24, 0.24, 0.625, 0.685, 0.012, zf - 0.004, zf)
    slippers = []
    for yb in (0.04, 0.329):
        for xc in (-0.28, 0.0, 0.28):
            for dx in (-0.055, 0.055):
                sole = rprism_xz(xc + dx - 0.045, xc + dx + 0.045, -0.12, 0.11, 0.04, yb, yb + 0.018, 6)
                upper = rbox(xc + dx - 0.042, xc + dx + 0.042, yb + 0.012, yb + 0.05, 0.0, 0.1, 0.018, 2)
                slippers += [sole, upper]
    ob = part('Kit_Slipper_Cabinet', join(paint(join(frame, plaque), 0), paint(grid, 1), paint(base, 2),
                                          paint(join(*slippers), 3)),
              [M_WALL, M_FROST, M_FRAME, M_MIST], smooth=35)
    expect(ob, size=(W, D, H), tol=0.002)
    collider(ob, 'box')


def planter_box_aero():
    """Planter (sheet 3): a rounded pearl trough with an inset frosted-aqua ribbon, a recessed silver foot over a navy
    shadow, navy soil, and three clusters of broad bent leaves. Pivot bottom centre. Box collider on the trough only;
    the leaves are visual."""
    import random
    L, W, H = P.PLANTER_L, P.PLANTER_W, P.PLANTER_H
    walls = moulded(-L / 2, L / 2, 0.04, H, -W / 2, W / 2, 4.5, 0.015, 0.018)
    walls = boolean(walls, paint(rprism_xz(-0.47, 0.47, -0.195, 0.195, 0.03, 0.515, H + 0.02, 8), 0))
    # WHY a separate ribbon ring over the trough (not a cut): it sits 2 mm proud inside the 1.0 x 0.45 envelope.
    ribbon = boolean(rprism_xz(-L / 2 - 0.0, L / 2 + 0.0, -W / 2, W / 2, 0.035, 0.457, 0.498, 8),
                     rprism_xz(-L / 2 + 0.01, L / 2 - 0.01, -W / 2 + 0.01, W / 2 - 0.01, 0.025, 0.45, 0.505, 8))
    walls = boolean(walls, paint(rprism_xz(-L / 2 - 0.01, L / 2 + 0.01, -W / 2 - 0.01, W / 2 + 0.01, 0.045, 0.455, 0.5, 8), 3))
    foot = rprism_xz(-0.46, 0.46, -0.185, 0.185, 0.025, 0.0, 0.025, 6)
    shadow = rprism_xz(-0.47, 0.47, -0.195, 0.195, 0.025, 0.025, 0.04, 6)
    soil = rprism_xz(-0.47, 0.47, -0.195, 0.195, 0.03, 0.505, 0.515, 8)
    pot_ob = part('Kit_Planter_Box_Pot', join(paint(walls, 0), paint(ribbon, 1), paint(foot, 2), paint(join(shadow, soil), 3)),
                  [M_WALL, M_FROST, M_FRAME, M_TRIM], smooth=35)
    collider(pot_ob, 'box')
    rng = random.Random(5)   # fixed seed: the same planter every build
    leaves = []
    for cx in (-0.32, 0.0, 0.32):
        for k in range(7):
            ln, wd = rng.uniform(0.24, 0.42), rng.uniform(0.08, 0.13)
            n = 6
            prof = [(wd / 2 * math.sin(math.pi * min(1.0, i / n * 1.1)) ** 0.8, ln * i / n) for i in range(n + 1)]
            outline = prof + [(-x, y) for x, y in reversed(prof[1:-1])]
            leaf = prism_xy(outline, -0.002, 0.002)
            for v in leaf.verts:   # bend the blade back toward its tip
                t = v.co.z / ln
                v.co.y += 0.12 * t * t
            rotate(leaf, rng.uniform(18, 42), axis='X')
            rotate(leaf, 360.0 * k / 7 + rng.uniform(-15, 15), axis='Z')
            move(leaf, -(cx + rng.uniform(-0.05, 0.05)), -rng.uniform(-0.06, 0.06), 0.5)
            leaves.append(leaf)
        stem = cylinder(0.006, 0.12, segments=6, base='bottom')
        move(stem, -cx, 0, 0.5)
        leaves.append(stem)
    leaf_ob = part('Kit_Planter_Box_Leaves', join(*leaves), M_LEAF, smooth=35)
    group('Kit_Planter_Box', pot_ob, leaf_ob)


# ---------------------------------------------------------------- light and column (README.md sheet 4)

def light_pendant_aero():
    """Linear pendant (sheet 4): a silver capsule housing with pearl end caps and a glowing frosted-milk diffuser
    underneath, hung 0.8 m below the soffit on two chrome cables from pearl canopies. Pivot top centre, against the
    soffit. Visual only."""
    L, Wd, Hh, drop = P.PENDANT_L, P.PENDANT_W, P.PENDANT_H, P.PENDANT_DROP
    y0, y1 = -drop - Hh, -drop
    housing = rprism_xz(-L / 2 + 0.04, L / 2 - 0.04, -Wd / 2, Wd / 2, 0.02, y0 + 0.006, y1, 6)
    bevel(housing, 0.01, segments=2, angle=30)
    caps = []
    for x0, x1 in ((-L / 2, -L / 2 + 0.045), (L / 2 - 0.045, L / 2)):
        c = rprism_xz(x0, x1, -Wd / 2, Wd / 2, 0.02, y0, y1, 6)
        bevel(c, 0.01, segments=2, angle=30)
        caps.append(c)
    diffuser = rprism_xz(-L / 2 + 0.05, L / 2 - 0.05, -0.025, 0.025, 0.015, y0, y0 + 0.012, 6)
    cables, canopies = [], []
    for x in (-0.8, 0.8):
        cables.append(tube([(x, y1 - 0.01, 0.0), (x, -0.012, 0.0)], 0.0015, 6))
        canopies.append(lathe([(0.0, -0.015), (0.022, -0.015), (0.025, -0.008), (0.025, 0.0)], 16, cx=x))
    ob = part('Kit_Light_Pendant_Linear', join(paint(housing, 0), paint(join(*caps, *canopies), 1), paint(diffuser, 2),
                                                paint(join(*cables), 3)),
              [M_FRAME, M_WALL, M_LIGHT, M_CHROME], smooth=35)
    expect(ob, size=(L, Wd, drop + Hh), tol=0.002)


def column_graphic_aero(height):
    """Graphic column (sheet 4): the 0.8 m R60 pearl column with inset silver collars top and bottom, the aqua band
    climbing round it with its thin cobalt under-stripe (the mix keeps cobalt on every column, DECISION.md) and a pale
    mist edge line on its upper side. Floor code and wayfinding are KitSigns labels (front, z = 0.4). Pivot bottom
    centre. Box collider."""
    w = P.COL_W
    h = w * 0.5
    f = h - P.COL_R
    s = P.COL_SKIN
    body = rprism_xz(-h + 0.001, h - 0.001, -h + 0.001, h - 0.001, P.COL_R - 0.001, 0.06, height - 0.06, 8)
    collars = join(rprism_xz(-h, h, -h, h, P.COL_R, 0.0, 0.06, 8), rprism_xz(-h, h, -h, h, P.COL_R, height - 0.06, height, 8))
    bevel(collars, 0.004, segments=1, angle=30)
    tall = height > P.FLOOR_H
    y0, band, rise = (2.6, 2.8, height * 0.3) if tall else (1.7, 1.5, height * 0.2)
    ya, yb = y0, y0 + rise * 0.5           # band's lower edge at the front-left and front-right corners
    yc = y0 + rise                         # and at the side's far corner
    front = prism_xy([(-f, ya), (f, yb), (f, yb + band), (-f, ya + band)], h - 0.001, h + s)
    side = prism_zy([(f, yb), (-f, yc), (-f, yc + band), (f, yb + band)], h - 0.001, h + s)
    e = PA.COL_EDGE
    edge = join(prism_xy([(-f, ya + band), (f, yb + band), (f, yb + band + e), (-f, ya + band + e)], h - 0.001, h + s),
                prism_zy([(f, yb + band), (-f, yc + band), (-f, yc + band + e), (f, yb + band + e)], h - 0.001, h + s))
    stripe = prism_xy([(-f, ya - 0.1), (f, yb - 0.1), (f, yb - 0.04), (-f, ya - 0.04)], h - 0.001, h + s)
    ob = part('Kit_Column_Graphic' + ('_Tall' if tall else ''),
              join(paint(body, 0), paint(collars, 1), paint(join(front, side), 2), paint(edge, 3), paint(stripe, 4)),
              [M_WALL, M_FRAME, M_AQUA, M_MIST, M_COBALT], smooth=35)
    collider(ob, 'box')


# ---------------------------------------------------------------- doors (README_part2.md sheet 6)

def wall_door_aero(width=None):
    """Door wall (sheet 6): the kit's wall_door (kit_pieces.py) with the same three parts, colliders, clear 1.2 x 2.4 m
    opening and reveal cuts, plus a rounded silver jamb strip laid into the reveal groove on both faces, so the opening
    reads as a finished silver portal. The wall body itself stays the restyle's (same cuts, same white). Runs +X from
    the pivot; 4 m by default, 2 m for Wall_Door_2m."""
    width = width or P.BAY
    x0 = (width - P.DOOR_W) * 0.5
    x1 = x0 + P.DOOR_W
    w, d = P.ARCH_W, P.ARCH_D
    gap = PA.DOOR_TRIM_SET   # strip face sits this far back from the wall face
    parts = []
    for name, box_ in (('Left', u(0, x0, 0, CLEAR_H, -P.WALL, 0)),
                       ('Right', u(x1, width, 0, CLEAR_H, -P.WALL, 0)),
                       ('Head', u(x0, x1, P.DOOR_H, CLEAR_H, -P.WALL, 0))):
        bm = recessed(box_, wall_cuts(0, width, door=(x0, x1, P.DOOR_H)))
        if name != 'Head':
            bm = boolean(bm, paint(end_chamfers(0, width), 0))
        strips = []
        for za, zb in ((-d, -gap), (-P.WALL + gap, -P.WALL + d)):
            if name == 'Left':
                strips.append(rbox(x0 - w, x0, 0.0, P.DOOR_H + w, za, zb, PA.DOOR_TRIM_R, 2))
            elif name == 'Right':
                strips.append(rbox(x1, x1 + w, 0.0, P.DOOR_H + w, za, zb, PA.DOOR_TRIM_R, 2))
            else:
                strips.append(rbox(x0, x1, P.DOOR_H, P.DOOR_H + w, za, zb, PA.DOOR_TRIM_R, 2))
        bm = join(bm, paint(join(*strips), 2))
        parts.append(part(f'Kit_Wall_Door_{width:g}m_{name}', bm, [M_WALL, M_TRIM, M_FRAME], smooth=None))
    left, right, head = parts
    for ob in parts:
        collider(ob, 'box')
    expect(left, size=(x0, P.WALL, CLEAR_H))
    expect(head, size=(P.DOOR_W, P.WALL, CLEAR_H - P.DOOR_H))
    group(f'Kit_Wall_Door_{width:g}m', left, right, head)


def door_leaf_aero():
    """Door leaf (sheet 6): a pearl slab with softened edges, a near-full-height frosted-aqua vision strip near the
    hinge in a silver-lined slot, and a long chrome bar pull in a recessed pocket on each face near the free edge,
    all inside the 40 mm leaf. Pivot on the hinge axis at floor level; the leaf runs +X from it, closed. Box collider.
    Placed as the kit's (kit_pieces.py door_leaf)."""
    w = P.DOOR_W - 2 * P.DOOR_GAP
    h = P.DOOR_H - 2 * P.DOOR_GAP
    t = P.DOOR_T
    g0 = P.DOOR_GAP
    slab = rbox(0, w, g0, g0 + h, -t / 2, t / 2, PA.LEAF_EDGE_R, 2)
    sx0, sx1 = PA.LEAF_STRIP_X - PA.LEAF_STRIP_W / 2, PA.LEAF_STRIP_X + PA.LEAF_STRIP_W / 2
    slab = boolean(slab, paint(rplate_xy(sx0, sx1, g0 + 0.03, g0 + h - 0.03, 0.01, -t, t), 1))
    pane = rplate_xy(sx0 - 0.002, sx1 + 0.002, g0 + 0.028, g0 + h - 0.028, 0.011, -0.002, 0.002)
    px = w - PA.LEAF_PULL_IN
    pulls = []
    for sgn in (1, -1):
        za, zb = sorted((sgn * (t / 2 - PA.LEAF_POCKET_D), sgn * (t / 2 + 0.01)))
        slab = boolean(slab, paint(rplate_xy(px - 0.025, px + 0.025, 1.1 - 0.33, 1.1 + 0.33, 0.01, za, zb), 3))
        bar = tube([(px, 1.1 - 0.3, sgn * (t / 2 - 0.007)), (px, 1.1 + 0.3, sgn * (t / 2 - 0.007))], 0.006, 10)
        posts = [tube([(px, y, sgn * (t / 2 - PA.LEAF_POCKET_D)), (px, y, sgn * (t / 2 - 0.007))], 0.005, 8)
                 for y in (1.1 - 0.28, 1.1 + 0.28)]
        pulls += [bar] + posts
    ob = part('Kit_Door_Leaf', join(paint(slab, 0), paint(pane, 2), paint(join(*pulls), 4)),
              [M_WALL, M_FRAME, M_FROST, M_TRIM, M_CHROME], smooth=35)
    expect(ob, size=(w, t, h), tol=0.001)
    collider(ob, 'box')


# ---------------------------------------------------------------- lift (concepts/Lift_Aero, approved by Alex 2026-11-15)

def portal_sweep(path, section):
    """A frame swept along a path in a wall's (x, y) plane: path = [(x, y)], section = [(across, out)] where `across`
    runs along the path's left normal (away from an opening traced clockwise) and `out` stands proud toward +Z."""
    rings = []
    n = len(path)
    for i, (x, y) in enumerate(path):
        ax, ay = path[max(i - 1, 0)]
        bx, by = path[min(i + 1, n - 1)]
        tx, ty = bx - ax, by - ay
        tl = math.hypot(tx, ty) or 1.0
        nx, ny = -ty / tl, tx / tl
        rings.append([(-(x + a * nx), -o, y + a * ny) for a, o in section])
    return sweep(rings)


def u_path(x0, x1, y_top, r, y_bottom=0.0, steps=8):
    """The U of a doorway, traced up the left jamb, across the head and down the right, with corner radius r."""
    pts = [(x0, y_bottom), (x0, y_top - r)]
    pts += [(x0 + r - r * math.cos(math.pi / 2 * k / steps), y_top - r + r * math.sin(math.pi / 2 * k / steps))
            for k in range(1, steps + 1)]
    pts += [(x1 - r + r * math.sin(math.pi / 2 * k / steps), y_top - r + r * math.cos(math.pi / 2 * k / steps))
            for k in range(0, steps + 1)]
    pts += [(x1, y_bottom)]
    return pts


def round_section(width, proud, r, steps=4):
    """A rolled frame section: `width` across, standing `proud`, its outer corners rounded by r (the sheet's R60
    rolled-silver portal reads as a tube-like band, not a flat strip)."""
    pts = [(0.0, -0.002)]
    for k in range(steps + 1):
        t = math.pi / 2 * k / steps
        pts.append((r - r * math.cos(t), proud - r + r * math.sin(t)))
    for k in range(steps + 1):
        t = math.pi / 2 * k / steps
        pts.append((width - r + r * math.sin(t), proud - r + r * math.cos(t)))
    pts.append((width, -0.002))
    return pts


def lift_door_frame_aero(label):
    """The lift's landing wall (Lift_Aero sheet 1): the same 4 m wall and Left / Right / Head parts with box colliders
    as kit_pieces.py, with a pearl face in seamed panels and a silver kick band, a rolled silver R60 portal round the
    1.4 x 2.2 m opening and silver reveals through it, a smoked capsule indicator in a silver rim centred at 2.66 m
    (its 1F / RF dot-matrix text is a KitSigns label at piece-local (2.0, 2.66, 0.04)), a silver call panel with
    round up / down buttons in cyan rings beside the doors, and the stacked AONAGI logo. Runs +X from the pivot on
    the landing face (+Z = the lobby)."""
    W, T, ow, oh = P.BAY, P.WALL, P.LIFT_OPEN_W, P.LIFT_OPEN_H
    x0, x1 = (W - ow) / 2, (W + ow) / 2
    y0 = -P.SLAB
    tag = label[:2]
    parts = []
    for name, b in (('Left', u(0, x0, y0, CLEAR_H, -T, 0)), ('Right', u(x1, W, y0, CLEAR_H, -T, 0)),
                    ('Head', u(x0, x1, oh, CLEAR_H, -T, 0))):
        ob = part(f'Kit_Lift_Door_Frame_4m_{tag}_{name}', b, M_WALL, smooth=None)
        collider(ob, 'box')
        parts.append(ob)
    pw, pp = PA.LIFT_PORTAL_W, PA.LIFT_PORTAL_PROUD
    # The portal's outer edge traced round the opening; its section runs inward to the opening's edge.
    portal = portal_sweep(u_path(x0 - pw, x1 + pw, oh + pw, PA.LIFT_PORTAL_R),
                          [(-a, o) for a, o in round_section(pw, pp, min(0.02, pp * 0.8))])
    reveals = join(u(x0, x0 + 0.02, 0, oh, -T, 0), u(x1 - 0.02, x1, 0, oh, -T, 0), u(x0, x1, oh - 0.02, oh, -T, 0))
    # WHY two strips: one across the doorway would read as a step into the car.
    kick = join(u(0, x0 - pw, 0, PA.LIFT_KICK_H, 0, 0.004), u(x1 + pw, W, 0, PA.LIFT_KICK_H, 0, 0.004))
    seams = join(*[u(x - 0.004, x + 0.004, PA.LIFT_KICK_H, CLEAR_H, 0, 0.002) for x in (0.6, W - 0.6)])
    # Indicator: a smoked capsule in a silver rim; the LED text is the KitSigns label in front of it.
    # WHY wider and squarer on the roof: its label, "RF 屋上", fills the KitSigns label's whole 1.12 m, and the 1F
    # capsule's round ends cropped its R (restyle session, 2026-11-15).
    iw, n_ind = (PA.LIFT_IND_W, 2.8) if tag == '1F' else (PA.LIFT_IND_W_RF, 4.0)
    ih, iy = PA.LIFT_IND_H, PA.LIFT_IND_Y
    rim = moulded(W / 2 - iw / 2, W / 2 + iw / 2, iy - ih / 2, iy + ih / 2, 0.0, 0.03, n_ind, 0.0, 0.012, axis='z')
    face = moulded(W / 2 - iw / 2 + 0.025, W / 2 + iw / 2 - 0.025, iy - ih / 2 + 0.025, iy + ih / 2 - 0.025,
                   0.02, 0.034, n_ind - 0.2, 0.0, 0.004, axis='z')
    # Call panel: silver, two round buttons in cyan rings (one on the roof: it only goes down).
    cx, cy = x1 + 0.35, 1.1
    panel = moulded(cx - 0.045, cx + 0.045, cy - 0.115, cy + 0.115, 0.0, 0.016, 4.0, 0.0, 0.006, axis='z')
    rings, buttons, arrows = [], [], []
    for sgn in ((1, -1) if tag == '1F' else (-1,)):
        by = cy + sgn * 0.045 if tag == '1F' else cy - 0.02
        ring = cylinder(0.027, 0.004, segments=28, axis='Y')
        move(ring, -cx, -0.017, by)
        btn = cylinder(0.021, 0.008, segments=28, axis='Y')
        move(btn, -cx, -0.02, by)
        tip = by + sgn * 0.009
        arrows.append(prism_xy([(cx - 0.009, by - sgn * 0.005), (cx + 0.009, by - sgn * 0.005), (cx, tip)], 0.024, 0.0255))
        rings.append(ring)
        buttons.append(btn)
    logo = badge_disc(0.62, 3.45, 0.0, 0.32, 0.002)
    trim = part(f'Kit_Lift_Door_Frame_4m_{tag}_Trim',
                join(paint(join(portal, reveals, kick, rim, panel, *buttons), 0), paint(face, 1),
                     paint(join(*rings), 2), paint(join(seams, *arrows), 3), paint(logo, 4)),
                [M_FRAME, M_SCREEN, M_LED, M_TRIM, M_P_LOGO], smooth=40)
    expect(parts[0], size=(x0, T, CLEAR_H + P.SLAB))
    group(f'Kit_Lift_Door_Frame_4m_{tag}', *parts, trim)


def lift_door_panel_aero():
    """One bi-parting landing leaf (Lift_Aero sheet 1): brushed silver with softened edges and an aqua etched line
    down the meeting edge on both faces. Pivot at the bottom of the meeting edge, centred on its thickness; the leaf
    runs +X and the lift opens it by sliding LIFT_LEAF_W along +X. Box collider."""
    w, h, t = P.LIFT_LEAF_W, P.LIFT_LEAF_H, P.LIFT_LEAF_T
    leaf = rbox(0, w, 0, h, -t / 2, t / 2, 0.003, 2)
    lines = join(u(0.006, 0.01, 0.0, h, t / 2 - 0.0005, t / 2 + 0.0005), u(0.006, 0.01, 0.0, h, -t / 2 - 0.0005, -t / 2 + 0.0005))
    # WHY plain brushed silver, not the ribbed trim band: in KitOffice the ribs read as corrugated sheet (sheet 1 is smooth)
    ob = part('Kit_Lift_Door_Panel', join(paint(leaf, 0), paint(lines, 1)), [M_FRAME, M_AQUA], smooth=35)
    expect(ob, size=(w, t + 0.001, h), tol=0.002)
    collider(ob, 'box')


def lift_shaft_wall_aero():
    """The glass shaft wall on the upper floor (Lift_Aero sheet 3): a round silver corner post at its start, a
    silver base band and head rail, clear glass, and a frosted-aqua ribbon at the foot of the glass. Same
    extents, thickness (-Z) and box collider as kit_pieces.py. One post per wall: the four walls round the
    shaft give the four corners."""
    W, T = P.BAY, P.PART_T
    zc = -T / 2
    y0, r = -P.SLAB, P.PART_RAIL
    post_r = T / 2
    post = lathe([(post_r, y0), (post_r, CLEAR_H)], 24, cx=post_r, cz=-T / 2)
    frame = join(rbox(post_r, W, y0, r, -T, 0, 0.01, 2), rbox(post_r, W, CLEAR_H - r, CLEAR_H, -T, 0, 0.01, 2))
    fr = part('Kit_Lift_Shaft_Wall_4m_Frame', join(paint(frame, 0), paint(post, 1)), [M_FRAME, M_CHROME], smooth=40)
    glass = part('Kit_Lift_Shaft_Wall_4m_Glass',
                 u(T, W, r + PA.LIFT_RIBBON_H, CLEAR_H - r, zc - P.GLASS_T / 2, zc + P.GLASS_T / 2), M_GLASS, smooth=None)
    ribbon = part('Kit_Lift_Shaft_Wall_4m_Ribbon',
                  u(T, W, r, r + PA.LIFT_RIBBON_H, zc - P.GLASS_T / 2, zc + P.GLASS_T / 2), M_FROST, smooth=None)
    expect(fr, size=(W, T, CLEAR_H + P.SLAB), tol=0.002)
    collider(fr, 'box')
    group('Kit_Lift_Shaft_Wall_4m', fr, glass, ribbon)


def lift_shaft_wall_solid():
    """The opaque shaft wall for 1F and the roof head-house (Lift_Aero sheet 3): pearl cladding with rounded R20 ends
    and fine seams, a silver base band. Runs +X from the pivot, thickness -Z, from the slab line to the soffit, like
    Lift_Shaft_Wall_4m. Box collider."""
    W, T = P.BAY, P.PART_T
    y0 = -P.SLAB
    wall = moulded(0, W, y0, CLEAR_H, -T, 0, 8.0, 0.0, 0.0)
    band = moulded(0.01, W - 0.01, y0, PA.LIFT_KICK_H, -T + 0.004, 0.004, 8.0, 0.0, 0.0)
    seams = join(*[u(x - 0.004, x + 0.004, PA.LIFT_KICK_H, CLEAR_H - 0.02, 0, 0.002) for x in (W / 3, 2 * W / 3)])
    ob = part('Kit_Lift_Shaft_Wall_4m_Solid', join(paint(wall, 0), paint(band, 1), paint(seams, 2)),
              [M_WALL, M_FRAME, M_TRIM], smooth=40)
    collider(ob, 'box')


def lift_cap():
    """The roof head-house's cap (Lift_Aero sheet 3): a pearl lid over the 4 x 4 m shaft with a 140 mm rolled edge
    and a 30 mm navy shadow reveal under it, no overhang past the shaft. Same pivot and grid as Slab_4x4 (front-left
    corner at the head-house's roof line; it hangs SLAB below and rises CAP_H above). Box collider."""
    S_, sl = P.BAY, P.SLAB
    reveal = moulded(0.02, S_ - 0.02, -sl, -sl + 0.03, -S_ + 0.02, -0.02, 8.0, 0.0, 0.0)
    cap = moulded(0, S_, -sl + 0.03, PA.LIFT_CAP_H, -S_, 0, 8.0, 0.02, 0.14)
    ob = part('Kit_Lift_Cap_4x4', join(paint(cap, 0), paint(reveal, 1)), [M_WALL, M_TRIM], smooth=40)
    expect(ob, size=(S_, S_, sl + PA.LIFT_CAP_H), tol=0.002)
    collider(ob, 'box')


def lift_car_aero():
    """The car (Lift_Aero sheet 2): a mist resin floor with a silver threshold, pearl side walls, a mirror-silver back
    wall, a glowing frosted ceiling panel in a pearl ceiling, a round silver handrail with rounded returns along the
    back and both sides, and a silver car operating panel by the door (smoked display, round 1F / 2F / RF buttons in
    cyan rings, open and close keys, the AONAGI ring). Same parts and box colliders as kit_pieces.py (floor, sides,
    back, ceiling, header), so the rider box doesn't move; the rail and panel are a visual part. Pivot at the floor's
    top centre; open side +Z. Moving: the lift script drives its height."""
    W, D, H, F, t = P.LIFT_CAR_W, P.LIFT_CAR_D, P.LIFT_CAR_H, P.LIFT_CAR_FLOOR, P.LIFT_CAR_WALL
    slab = u(-W / 2, W / 2, -F, -0.004, -D / 2, D / 2)
    resin = u(-W / 2 + t, W / 2 - t, -0.004, 0, -D / 2 + t, D / 2 - 0.08)
    threshold = u(-W / 2 + t, W / 2 - t, -0.004, 0, D / 2 - 0.08, D / 2)
    floor = part('Kit_Lift_Car_Floor', join(paint(slab, 0), paint(resin, 1), paint(threshold, 0)), [M_FRAME, M_MIST],
                 smooth=None)
    sides = []
    for name, (a, b) in (('Left', (-W / 2, -W / 2 + t)), ('Right', (W / 2 - t, W / 2))):
        sides.append(part(f'Kit_Lift_Car_{name}', moulded(a, b, 0, H, -D / 2, D / 2, 8.0, 0.0, 0.0), M_WALL, smooth=40))
    # WHY brushed silver, not chrome: a true mirror showed the reflection probe's sky as a smear in KitOffice; the
    # sheet's "mirror-silver" back reads as a soft satin sheen.
    back = part('Kit_Lift_Car_Back', moulded(-W / 2 + t, W / 2 - t, 0, H, -D / 2, -D / 2 + t, 8.0, 0.0, 0.0), M_FRAME,
                smooth=40)
    ceil = u(-W / 2, W / 2, H, H + 0.1, -D / 2, D / 2)
    glow = moulded(-1.35, 1.35, H - 0.012, H, -1.2, 1.05, 5.0, 0.0, 0.0)
    rim = moulded(-1.4, 1.4, H - 0.02, H, -1.25, 1.1, 5.0, 0.0, 0.0)
    ceiling = part('Kit_Lift_Car_Ceiling', join(paint(ceil, 0), paint(rim, 2), paint(glow, 1)), [M_WALL, M_LIGHT, M_FRAME],
                   smooth=None)
    header = part('Kit_Lift_Car_Header', u(-W / 2 + t, W / 2 - t, P.LIFT_OPEN_H, H, D / 2 - t, D / 2), M_WALL, smooth=None)
    for ob in (floor, *sides, back, ceiling, header):
        collider(ob, 'box')
    # Handrail: back and both sides, 40 mm, 0.95 m high, 60 mm off the walls, rolling round the back corners.
    ry, off, rr = PA.LIFT_RAIL_Y, 0.06, 0.02
    xi, zb = W / 2 - t - off, -D / 2 + t + off
    zf = D / 2 - 0.45
    rail = [(-xi, ry, zf), (-xi, ry, zb + 0.09)]
    rail += [(-xi + 0.09 - 0.09 * math.cos(math.pi / 2 * k / 6), ry, zb + 0.09 - 0.09 * math.sin(math.pi / 2 * k / 6))
             for k in range(1, 7)]
    rail += [(xi - 0.09 + 0.09 * math.sin(math.pi / 2 * k / 6), ry, zb + 0.09 - 0.09 * math.cos(math.pi / 2 * k / 6))
             for k in range(0, 7)]
    rail += [(xi, ry, zf)]
    rail_bm = tube(rail, rr, 12)
    ends = []
    for x in (-xi, xi):    # returns into the side walls at the front ends
        ends.append(tube([(x, ry, zf), (x, ry, zf + 0.04), (x + (0.06 if x < 0 else -0.06) * -1, ry, zf + 0.06)], rr, 12))
    brackets = [tube([(x, ry, z), (x + (-off if x < 0 else off), ry, z)], 0.008, 8) for x in (-xi, xi) for z in (-0.4, 0.5)]
    brackets += [tube([(x, ry, zb), (x, ry, zb - off)], 0.008, 8) for x in (-1.0, 0.0, 1.0)]
    # Car operating panel on the right side wall, by the door, facing into the car (-X).
    px = W / 2 - t
    pz0, pz1 = D / 2 - 0.42, D / 2 - 0.18
    cop = moulded(px - 0.016, px, 0.85, 1.6, pz0, pz1, 4.0, 0.0, 0.0)
    disp = u(px - 0.019, px - 0.016, 1.45, 1.53, pz0 + 0.06, pz1 - 0.06)
    rings, keys = [], []
    for i, by in enumerate((1.36, 1.27, 1.18)):
        ring = cylinder(0.022, 0.004, segments=24, axis='X'); move(ring, -(px - 0.018), -(pz0 + pz1) / 2, by)
        key = cylinder(0.017, 0.008, segments=24, axis='X'); move(key, -(px - 0.02), -(pz0 + pz1) / 2, by)
        rings.append(ring); keys.append(key)
    for dz in (-0.045, 0.045):
        key = cylinder(0.016, 0.008, segments=20, axis='X'); move(key, -(px - 0.02), -((pz0 + pz1) / 2 + dz), 1.06)
        keys.append(key)
    mark = cylinder(0.025, 0.002, segments=28, axis='X'); move(mark, -(px - 0.017), -(pz0 + pz1) / 2, 0.95)
    trim = part('Kit_Lift_Car_Trim', join(paint(join(rail_bm, *ends, *brackets), 0), paint(join(cop, *keys), 1),
                                          paint(disp, 2), paint(join(*rings), 3), paint(mark, 4)),
                [M_CHROME, M_FRAME, M_SCREEN, M_LED, M_P_RING], smooth=40)
    expect(floor, size=(W, D, F))
    group('Kit_Lift_Car', floor, *sides, back, ceiling, header, trim)


# ---------------------------------------------------------------- pieces

PIECES_AERO = {
    'Monitor_Flat': monitor_crt,
    'PC_Tower': pc_tower,
    'Keyboard': keyboard_aero,
    'Workstation_Bench': workstation_bench,
    'Chair_Task': chair_task_aero,
    'Chair_Pod': chair_pod_aero,
    'Phone_Desk': phone_desk_aero,
    'Doc_Tray': doc_tray_aero,
    'Desk_Sculpt': desk_sculpt_aero,
    'Desk_Reception': desk_reception_aero,
    'Storage_Low': storage_low_aero,
    'Copier': copier_aero,
    'Water_Server': water_server_aero,
    'Umbrella_Stand': umbrella_stand_aero,
    'Vending_Machine': vending_machine_aero,
    'Slipper_Cabinet': slipper_cabinet_aero,
    'Planter_Box': planter_box_aero,
    'Light_Pendant_Linear': light_pendant_aero,
    'Column_Graphic': lambda: column_graphic_aero(CLEAR_H),
    'Column_Graphic_Tall': lambda: column_graphic_aero(2 * P.FLOOR_H - P.SLAB),   # same height as kit_pieces.py's
    'Wall_Door_4m': wall_door_aero,
    'Wall_Door_2m': lambda: wall_door_aero(P.INFILL),
    'Door_Leaf': door_leaf_aero,
    'Lift_Door_Frame_4m_1F': lambda: lift_door_frame_aero('1F'),
    'Lift_Door_Frame_4m_RF': lambda: lift_door_frame_aero('RF'),
    'Lift_Door_Panel': lift_door_panel_aero,
    'Lift_Shaft_Wall_4m': lift_shaft_wall_aero,
    'Lift_Shaft_Wall_4m_Solid': lift_shaft_wall_solid,
    'Lift_Cap_4x4': lift_cap,
    'Lift_Car': lift_car_aero,
}

if P.PIECE in PIECES_AERO:
    PIECES_AERO[P.PIECE]()
else:
    raise ValueError(f'props_aero.py has no piece {P.PIECE!r}; it builds {sorted(PIECES_AERO)}')
