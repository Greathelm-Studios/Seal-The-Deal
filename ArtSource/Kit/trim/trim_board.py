# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code, blender-modeling skill)
#  Generated : 2026-11-15
#  Prompt    : Alex: "making a trimsheet in substance or something that can be used on most modular kits and props,
#              since we arent going realistic we can do more with trimsheets" ... "you can use substance painter"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : The bake source for the AONAGI Aero trim sheet: a flat 2 x 2 m low plane (its UVs are the sheet) and a
#              high-poly board whose bands hold the sheet's relief (keys, vent slots, perforations, buttons, ribs,
#              panel seams) in ID-coloured materials. Painter bakes the high onto the low and paints by ID
#              (trim_painter/*.py). Band order and sizes come from trim_layout.json, which the props' UVs also read.
# -----------------------------------------------------------------------------
"""Trim board for the AONAGI Aero trim sheet.

Build:  node <skill>/scripts/mdl.mjs build ArtSource/Kit/trim/trim_board.py --out <dir>/TrimBoard.blend
Export: node <skill>/scripts/mdl.mjs export --blend <dir>/TrimBoard.blend --out <dir>/export --split

Blender frame here is the sheet's: x = U (0..2 m), y = V (0..2 m), z = relief (+ up). 1 mm is about 1 px.
"""
import json
import math
import os

import bmesh
from modelkit import *

P = params(
    SHEET_M=2.0,          # sheet size in metres: 2048 px at 1024 px/m (trim_layout.json)
    KEY_PITCH_N=111,      # keys across the 2 m repeat: 18.0 mm pitch, an office keyboard's
    KEY_CAP=0.0155,       # key cap square, metres
    KEY_H=0.003,          # key cap height above the well, metres
    VENT_N=25,            # vent slots across the repeat: 80 mm pitch
    VENT_L=0.064, VENT_W=0.006, VENT_PITCH_V=0.012,   # slot length, width and row pitch, metres (README_part2 vents)
    RELIEF=0.002,         # surface height over a recess floor (vents, grille, panel seams), metres
    HOLE_N=250, HOLE_R=0.0025, HOLE_SEGS=12,           # grille perforation: count per repeat (8 mm pitch), radius
    BUTTON_N=50, BUTTON_R=0.012, BUTTON_H=0.002,       # round buttons: count per repeat (40 mm), radius, height
    RING_IN=0.0135, RING_OUT=0.0155,                   # cyan LED ring round each round button, radii
    PAD_N=100, PAD_W=0.014, PAD_D=0.010,               # small keypad buttons: count per repeat (20 mm), size
    RIB_PITCH=0.006, RIB_W=0.0024, RIB_H=0.0009,       # ribbed silver: rib pitch, width, height
    HOLE_CHAMFER=0.001,   # chamfer round each grille hole's rim, metres
    SEAM_GAP=0.0015,      # panel shutline width, metres
    SEAM_U=0.25,          # vertical seam spacing along U on the panel band, metres
)

with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'trim_layout.json'), encoding='utf-8') as f:
    LAYOUT = json.load(f)
PX = 1.0 / LAYOUT['px_per_m']
S = P.SHEET_M

# ID materials: their base colour is the Painter ID colour (bake ColorSource = material colour).
IDS = {name: material('ID_' + name, tuple(rgb), roughness=0.5) for name, rgb in LAYOUT['ids'].items()}
bms = {name: [] for name in IDS}


def band_spans():
    """{band name: (y0, y1)} in metres, the first band at the top of the sheet."""
    out, top = {}, S
    for b in LAYOUT['bands']:
        h = b['px'] * PX
        out[b['name']] = (top - h, top)
        top -= h
    assert abs(top) < 1e-6, 'trim_layout.json bands must fill the sheet'
    return out


def slab(x0, x1, y0, y1, z0, z1):
    bm = box(x1 - x0, y1 - y0, z1 - z0)
    return move(bm, (x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)


def flat(name, y0, y1):
    bms[name].append(slab(0, S, y0, y1, -0.002, 0.0))


def perforated(name, floor, y0, y1, n, r, segs):
    """A plate at RELIEF over a `floor` plane with round holes on a square grid (n across the repeat).
    Built cell by cell, a square ring of quads round each hole and the hole's wall, so no boolean is needed."""
    pitch = S / n
    rows = int((y1 - y0) // pitch)
    yb = y0 + ((y1 - y0) - rows * pitch) / 2
    bm = bmesh.new()
    z = P.RELIEF
    for j in range(rows):
        for i in range(n):
            cx, cy = (i + 0.5) * pitch, yb + (j + 0.5) * pitch
            h = pitch / 2
            sq, ci, mid, lo = [], [], [], []
            for k in range(segs):
                a = 2 * math.pi * k / segs
                # square boundary point on the same ray, clamped to the cell
                c, s = math.cos(a), math.sin(a)
                m = h / max(abs(c), abs(s))
                sq.append(bm.verts.new((cx + c * m, cy + s * m, z)))
                # WHY a chamfered rim: a straight-walled hole is invisible to a top-down normal bake
                ci.append(bm.verts.new((cx + c * (r + P.HOLE_CHAMFER), cy + s * (r + P.HOLE_CHAMFER), z)))
                mid.append(bm.verts.new((cx + c * r, cy + s * r, z - P.HOLE_CHAMFER)))
                lo.append(bm.verts.new((cx + c * r, cy + s * r, 0.0)))
            # corners: add the four square corners as extra faces so the cell is fully covered
            for k in range(segs):
                k2 = (k + 1) % segs
                bm.faces.new((sq[k], sq[k2], ci[k2], ci[k]))
                bm.faces.new((ci[k], ci[k2], mid[k2], mid[k]))
                bm.faces.new((mid[k], mid[k2], lo[k2], lo[k]))
            corners = [(cx + h, cy + h), (cx - h, cy + h), (cx - h, cy - h), (cx + h, cy - h)]
            for (qx, qy) in corners:
                # the triangle between the ray points either side of this corner and the corner itself
                ang = math.atan2(qy - cy, qx - cx) % (2 * math.pi)
                k = int(ang / (2 * math.pi / segs)) % segs
                k2 = (k + 1) % segs
                cv = bm.verts.new((qx, qy, z))
                bm.faces.new((sq[k], cv, sq[k2]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        if f.normal.z < -0.5:
            f.normal_flip()
    bms[name].append(bm)
    # band edges: fill the margin rows above and below the hole grid, and the recess floor
    bms[name].append(slab(0, S, y0, yb, 0.0, z))
    bms[name].append(slab(0, S, yb + rows * pitch, y1, 0.0, z))
    bms[floor].append(slab(0, S, y0, y1, -0.002, 0.0))


spans = band_spans()
for b in LAYOUT['bands']:
    y0, y1 = spans[b['name']]
    if b['kind'] in ('swatch', 'print'):   # the print band is flat: its artwork is painted, not baked
        flat(b['id'], y0, y1)

# ---- Keys: pearl caps on a mist well, uniform grid (any strip of the band reads as keys)
y0, y1 = spans['Keys']
flat('Mist', y0, y1)
p = S / P.KEY_PITCH_N
rows = int((y1 - y0) // p)
yb = y0 + ((y1 - y0) - rows * p) / 2
caps = []
for j in range(rows):
    for i in range(P.KEY_PITCH_N):
        cx, cy = (i + 0.5) * p, yb + (j + 0.5) * p
        caps.append(slab(cx - P.KEY_CAP / 2, cx + P.KEY_CAP / 2, cy - P.KEY_CAP / 2, cy + P.KEY_CAP / 2, 0.0, P.KEY_H))
keys = join(*caps)
bevel(keys, 0.0015, segments=3, angle=30)
bms['Pearl'].append(keys)

# ---- Vents: pearl surface at RELIEF with navy slot floors, built from the pieces between the slots
y0, y1 = spans['Vents']
flat('Navy', y0, y1)
pu = S / P.VENT_N
rows = int((y1 - y0) // P.VENT_PITCH_V)
yb = y0 + ((y1 - y0) - rows * P.VENT_PITCH_V) / 2
vent = [slab(0, S, y0, yb + (P.VENT_PITCH_V - P.VENT_W) / 2, 0.0, P.RELIEF)]
for j in range(rows):
    sy0 = yb + j * P.VENT_PITCH_V + (P.VENT_PITCH_V - P.VENT_W) / 2
    sy1 = sy0 + P.VENT_W
    top = sy1 + (P.VENT_PITCH_V - P.VENT_W) if j < rows - 1 else y1
    vent.append(slab(0, S, sy1, top, 0.0, P.RELIEF))                  # the bar above this slot row
    edge = (pu - P.VENT_L) / 2                                          # land from a slot's end to its cell edge
    vent.append(slab(0, edge, sy0, sy1, 0.0, P.RELIEF))                 # WHY split at 0 and S: the sheet wraps in U
    for i in range(P.VENT_N):
        x_end = i * pu + edge + P.VENT_L                                # the land after slot i
        vent.append(slab(x_end, min(x_end + 2 * edge, S), sy0, sy1, 0.0, P.RELIEF))
v = join(*vent)
bevel(v, 0.0006, segments=1, angle=30)
bms['Pearl'].append(v)

# ---- Grille: perforated silver over a navy floor
y0, y1 = spans['Grille']
perforated('Silver', 'Navy', y0, y1, P.HOLE_N, P.HOLE_R, P.HOLE_SEGS)

# ---- Buttons: a row of round pearl buttons with cyan rings over two rows of small keypad buttons, on pearl
y0, y1 = spans['Buttons']
flat('Pearl', y0, y1)
yc = y1 - (y1 - y0) * 0.32
pb = S / P.BUTTON_N
buttons, rings = [], []
for i in range(P.BUTTON_N):
    cx = (i + 0.5) * pb
    bt = cylinder(P.BUTTON_R, P.BUTTON_H, segments=24, base='bottom')
    move(bt, cx, yc, 0)
    buttons.append(bt)
    outer = cylinder(P.RING_OUT, 0.0004, segments=32, base='bottom')
    inner = cylinder(P.RING_IN, 0.002, segments=32)
    ring = boolean(outer, inner)
    move(ring, cx, yc, 0)
    rings.append(ring)
pp = S / P.PAD_N
for row in (0, 1):
    cy = y0 + (y1 - y0) * (0.2 + row * 0.18)
    for i in range(P.PAD_N):
        cx = (i + 0.5) * pp
        buttons.append(slab(cx - P.PAD_W / 2, cx + P.PAD_W / 2, cy - P.PAD_D / 2, cy + P.PAD_D / 2, 0.0, P.BUTTON_H))
bt = join(*buttons)
bevel(bt, 0.0008, segments=2, angle=30)
bms['Pearl'].append(bt)
bms['LED'].append(join(*rings))

# ---- Ribbed: brushed silver with fine raised ribs along U (lift leaves, handles, kick plates)
y0, y1 = spans['Ribbed']
flat('Silver', y0, y1)
n = int((y1 - y0) // P.RIB_PITCH)
ribs = [slab(0, S, y0 + (j + 0.5) * P.RIB_PITCH - P.RIB_W / 2, y0 + (j + 0.5) * P.RIB_PITCH + P.RIB_W / 2, 0.0, P.RIB_H)
        for j in range(n)]
rb = join(*ribs)
bevel(rb, P.RIB_H * 0.8, segments=2, angle=30)   # WHY: rounded ribs give the bake a slope to catch
bms['Silver'].append(rb)

# ---- Panel: pearl panels with a shutline along the band centre and every SEAM_U along U, on a navy floor
y0, y1 = spans['Panel']
flat('Navy', y0, y1)
ym = (y0 + y1) / 2
g = P.SEAM_GAP / 2
panels = []
nu = int(round(S / P.SEAM_U))
for i in range(nu):
    x0, x1 = i * P.SEAM_U + g, (i + 1) * P.SEAM_U - g
    panels.append(slab(x0, x1, y0, ym - g, 0.0, P.RELIEF))
    panels.append(slab(x0, x1, ym + g, y1, 0.0, P.RELIEF))
pn = join(*panels)
bevel(pn, 0.0012, segments=2, angle=30)
bms['Pearl'].append(pn)

# ---- objects
high = []
for name, parts in bms.items():
    if parts:
        # WHY flat shading: smoothed normals spread gradients across the big panel faces in the bake.
        high.append(part('TrimBoard_High_' + name, join(*parts), IDS[name], smooth=None))
group('TrimBoard_High', *high)

quad = bmesh.new()
quad.faces.new([quad.verts.new(c) for c in ((0, 0, 0), (S, 0, 0), (S, S, 0), (0, S, 0))])
low = part('TrimBoard_Low', quad, IDS['Pearl'], smooth=None)
# The low plane's UVs are the sheet: u = x / S, v = y / S.
me = low.data
uv = me.uv_layers.active
for poly in me.polygons:
    for li in poly.loop_indices:
        co = me.vertices[me.loops[li].vertex_index].co
        uv.data[li].uv = (co.x / S, co.y / S)
