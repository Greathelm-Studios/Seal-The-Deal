"""Usage: python ArtSource/Kit/markings/make_markings.py   (writes ../signs/textures/*.png and review/markings_sheet.png)

Floor and ceiling marks are whole-plate sheets: one 40 x 40 m texture per storey (the plate is 40 x 32; the rest is
empty), laid as a single cutout quad just above the floor or just under the soffit. Plan metres throughout: x east,
y south from the NW corner, as the L2 drawings.
"""
import json
import math
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(KIT))
sys.path.insert(0, os.path.join(KIT, 'signs'))
import make_signs as ms   # noqa: E402  (fonts, palette and label helpers shared with the sign system)

OUT = os.path.join(KIT, 'signs', 'textures')
LAYOUT = json.load(open(os.path.join(KIT, 'concepts', 'Layout', 'L2', 'layouts.json'), encoding='utf8'))['models'][0]
KITPLAN = json.load(open(os.path.join(HERE, 'l2_on_kit.json'), encoding='utf8'))

W_PLATE, D_PLATE = 40.0, 32.0          # the L2 plate, metres
SHEET_M = 40.0                         # floor and ceiling sheets cover 40 x 40 m (power-of-two texture, square texels)
SHEET_PX = 4096                        # their texture size. WHY: 102.4 px/m, 10 mm texels: the 20 mm silver edges hold
SS = 2                                 # sheets are drawn this many times larger, then filtered down (smooth edges)
FLOORS = ('1F', '2F')                  # Japanese floor names, as the lift's indicators: ground = 1F

# ---- Palette (make_signs, plus the materials' own colours)
NAVY, COBALT, WHITE = ms.NAVY, ms.COBALT, ms.WHITE
SILVER = (0xC9, 0xCE, 0xD3)            # kit_pieces M_STAIR / frame aluminium
GATE_RED = ms.SEAL                     # the locked-door red: "blocked" everywhere in the game
COVE = (0xE8, 0xF8, 0xFF)              # light cove: near-white with the slightest ice, the kit's light strips
SAFETY_GREEN = (0x1E, 0x8C, 0x4E)      # evacuation signage green (ISO 7010 family)
# Mood D in blue (concepts/Mood/DECISION.md, Alex 2026-11-15): each firm's half owns one blue, as whole surfaces and light.
HALF_A_BLUE = (0x1D, 0x3F, 0xC4)       # firm A's west half: deep cobalt
HALF_B_BLUE = (0x1E, 0x9B, 0xFF)       # firm B's east half: bright azure
COURT_SKY = (0x8F, 0xC3, 0xF5)         # the court's own blue: pale sky, between the two halves' blues
COURT_FIELD = (16.4, 10.4, 23.6, 21.6) # the court's floor field, plan metres: inside the court, clear of its edges
ZONE_NUMBER_H = 2.6                    # giant zone number height on a wall field, metres (D: "giant 01 / 04")

# ---- Sizes, metres (MARKINGS.md)
RING_W = 0.30                          # ring inlay band
RING_EDGE = 0.02                       # its silver edge, each side
COVE_W = 0.12                          # the ceiling cove above it
THRESHOLD_W = 0.60                     # district threshold strip, wall to wall
BASE_BAND = 0.05                       # district band at the wall base, beyond each wall face. 0.05 since 2026-11-15:
                                       # at 0.12 every wall end (door jambs, the stair) showed a square coloured foot
                                       # (the quality pass, Alex: "basic and amateur"); 5 cm reads as a crisp edge line
SOFFIT_BAND = 0.25                     # district band on the soffit, beyond each solid wall face
TENANCY_BAND = 0.15                    # each firm's band on the tenancy line
GATE_W = 0.30                          # gate band at a stair arrival
EDGE_W = 0.08                          # navy edge strip on a void or hole edge, plus a silver line
DOOR_OPEN = 1.2                        # door opening the base band stops at (kit DOOR_W)
WALL_T = {'solid': 0.2, 'glass': 0.1, 'frost': 0.1, 'fluted': 0.1, 'louvre': 0.1}   # kit WALL and PART_T


# ---------------------------------------------------------------- data

def firms():
    """(name, colour) for firm A (tower 0, the NW firm) and firm B, read from the FirmDefinition assets on disk."""
    out = {}
    folder = os.path.join(REPO, 'Assets', '_Project', 'Profiles', 'Match')
    for f in os.listdir(folder):
        if not (f.startswith('Firm_') and f.endswith('.asset')):
            continue
        s = open(os.path.join(folder, f), encoding='utf8').read()
        name = re.search(r'DisplayName: (.+)', s).group(1).strip()
        r, g, b = (float(v) for v in re.search(r'MarkerColour: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)', s).groups())
        tower = int(re.search(r'Tower: (\d+)', s).group(1))
        out[tower] = (name, tuple(int(round(c * 255)) for c in (r, g, b)))
    return out[0], out[1]


FIRM_A, FIRM_B = firms()
DISTRICT_COLOUR = {d['letter']: tuple(int(d['color'][i:i + 2], 16) for i in (1, 3, 5)) for d in LAYOUT['districts']}
TURN_LETTER = {'L': 'R', 'A': 'C', 'H': 'H'}


def turn(p):
    """A plan point turned 180 degrees about the plate's centre: firm A's half to firm B's."""
    return (W_PLATE - p[0], D_PLATE - p[1])


def district_of(p):
    """The district letter (firm A's naming) at a firm-A-half point."""
    for letter, box in KITPLAN['districts'].items():
        if letter.startswith('_'):
            continue
        x0, y0, x1, y1 = box
        if x0 <= p[0] <= x1 and y0 <= p[1] <= y1:
            return letter
    return 'H'


def runs(floor):
    """Every built wall run on a floor as (kind, a, b, doors, district, turned)."""
    out = []
    groups = [('half', True), ('once', False)] + ([('groundHalf', True)] if floor == '1F' else [])
    for key, both in groups:
        for r in KITPLAN[key]:
            mid = ((r['a'][0] + r['b'][0]) / 2, (r['a'][1] + r['b'][1]) / 2)
            d = district_of(mid)
            out.append((r['kind'], tuple(r['a']), tuple(r['b']), r['doors'], d, False))
            if both:
                out.append((r['kind'], turn(r['a']), turn(r['b']), r['doors'], TURN_LETTER[d], True))
    return out


def run_geometry(a, b):
    """Unit direction along a run and the unit normal on its thickness side (right of travel, plan drawn y down)."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    n = math.hypot(dx, dy)
    ux, uy = dx / n, dy / n
    return (ux, uy), (-uy, ux)


# ---------------------------------------------------------------- the sheet

class Sheet:
    """A 40 x 40 m plan sheet, plan metres in, supersampled pixels out."""

    def __init__(self):
        self.k = SHEET_PX * SS / SHEET_M
        self.img = Image.new('RGBA', (SHEET_PX * SS, SHEET_PX * SS), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def p(self, x, y):
        return (x * self.k, y * self.k)

    def m(self, metres):
        return max(1, int(round(metres * self.k)))

    def line(self, pts, w, fill):
        self.d.line([self.p(*q) for q in pts], fill=fill, width=self.m(w), joint='curve')
        r = w / 2   # round the open ends, so a band meets its wall cleanly
        for q in (pts[0], pts[-1]):
            self.disc(q, r, fill)

    def disc(self, c, r, fill=None, outline=None, width=0.0):
        x, y = self.p(*c)
        rr = r * self.k
        self.d.ellipse((x - rr, y - rr, x + rr, y + rr), fill=fill, outline=outline,
                       width=self.m(width) if width else 0)

    def rect(self, x0, y0, x1, y1, fill):
        self.d.rectangle((*self.p(min(x0, x1), min(y0, y1)), *self.p(max(x0, x1), max(y0, y1))), fill=fill)

    def rrect(self, cx, cy, w, h, r, fill, outline=None, width=0.0):
        self.d.rounded_rectangle((*self.p(cx - w / 2, cy - h / 2), *self.p(cx + w / 2, cy + h / 2)), radius=r * self.k,
                                 fill=fill, outline=outline, width=self.m(width) if width else 0)

    def text(self, cx, cy, body, path, cap, fill, angle=0, weight=None, tracking=0.0):
        """Text centred on (cx, cy), cap height `cap` metres, turned `angle` degrees counter-clockwise."""
        f = font_px(path, cap * self.k, weight)
        track = tracking * cap * self.k
        w = int(sum(f.getlength(c) for c in body) + track * max(len(body) - 1, 0)) + 4
        h = int(cap * self.k * 2.2) + 4
        layer = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        ld = ImageDraw.Draw(layer)
        x = 2
        for c in body:
            ld.text((x, h / 2), c, font=f, fill=fill, anchor='lm')
            x += f.getlength(c) + track
        if angle:
            layer = layer.rotate(angle, expand=True, resample=Image.BICUBIC)
        px, py = self.p(cx, cy)
        self.img.alpha_composite(layer, (int(px - layer.width / 2), int(py - layer.height / 2)))

    def clear(self, x0, y0, x1, y1):
        """Cuts a rectangle back to empty: holes in the slab the sheet lies on."""
        self.d.rectangle((*self.p(x0, y0), *self.p(x1, y1)), fill=(0, 0, 0, 0))

    def save(self, name, flip=False):
        out = self.img.resize((SHEET_PX, SHEET_PX), Image.LANCZOS)
        if flip:   # ceiling sheets are seen from below: KitOfficeScene lays them face down, south at the top
            out = out.transpose(Image.FLIP_TOP_BOTTOM)
        out.save(os.path.join(OUT, name + '.png'))
        return out


_fonts = {}


def font_px(path, cap_px, weight=None):
    """A font whose capital (H, or the kanji 審) is cap_px pixels tall."""
    key = (path, int(cap_px), weight)
    if key in _fonts:
        return _fonts[key]
    probe = ImageFont.truetype(path, 200)
    if weight is not None:
        probe.set_variation_by_axes([weight])
    sample = 'H' if path == ms.LATIN else '審'
    y0, y1 = probe.getbbox(sample)[1], probe.getbbox(sample)[3]
    f = ImageFont.truetype(path, max(1, int(round(200 * cap_px / (y1 - y0)))))
    if weight is not None:
        f.set_variation_by_axes([weight])
    _fonts[key] = f
    return f


# ---------------------------------------------------------------- floor marks

def ring_path():
    pts = [tuple(p) for p in LAYOUT['loops'][0]]
    return pts if pts[0] == pts[-1] else pts + [pts[0]]


def floor_sheet(floor):
    s = Sheet()
    b = KITPLAN['built']
    # 2. Districts at the wall base: each district's colour hugs its walls on both faces and stops at every doorway.
    for kind, a, bb, doors, d, turned in runs(floor):
        if kind == 'guard':
            continue
        (ux, uy), (nx, ny) = run_geometry(a, bb)
        t = WALL_T[kind]
        cx, cy = nx * t / 2, ny * t / 2          # the wall's own centre line, off the run line by half its thickness
        axis = 0 if ux else 1
        cuts = []
        for door in doors:
            c = door['at'] if not turned else (W_PLATE if axis == 0 else D_PLATE) - door['at']
            cuts.append((c - DOOR_OPEN / 2, c + DOOR_OPEN / 2))
        lo, hi = sorted((a[axis], bb[axis]))
        pieces, cur = [], lo
        for c0, c1 in sorted(cuts):
            pieces.append((cur, c0))
            cur = c1
        pieces.append((cur, hi))
        for p0, p1 in pieces:
            if p1 - p0 < 0.05:
                continue
            q0 = (p0, a[1]) if axis == 0 else (a[0], p0)
            q1 = (p1, a[1]) if axis == 0 else (a[0], p1)
            s.d.line([s.p(q0[0] + cx, q0[1] + cy), s.p(q1[0] + cx, q1[1] + cy)], fill=DISTRICT_COLOUR[d],
                     width=s.m(t + 2 * BASE_BAND))
    # 2. Threshold strips, wall to wall, half in each district's colour, with both letters set in.
    for th in KITPLAN['thresholds']:
        for turned in (False, True):
            a, bb = tuple(th['a']), tuple(th['b'])
            fr, to = th['from'], th['to']
            if turned:
                a, bb, fr, to = turn(a), turn(bb), TURN_LETTER[fr], TURN_LETTER[to]
            threshold(s, a, bb, fr, to, threshold_sign(th))
    # Mood D: the court's floor field in its sky blue, under the tenancy line and the plinth (ground floor only).
    if floor == '1F':
        x0, y0, x1, y1 = COURT_FIELD
        s.rrect((x0 + x1) / 2, (y0 + y1) / 2, x1 - x0 + 2 * RING_EDGE, y1 - y0 + 2 * RING_EDGE, 0.6, SILVER)
        s.rrect((x0 + x1) / 2, (y0 + y1) / 2, x1 - x0, y1 - y0, 0.58, COURT_SKY)
    # 3. The tenancy line: the two leases meet on the court's axis, from the lift door to the south baffle.
    tenancy(s, b['liftDoor'], b['southBaffle'])
    # 4. The deal plinth at the case start (ground floor: upstairs it is the void).
    if floor == '1F':
        plinth(s, tuple(LAYOUT['case']))
    # 1. The ring, drawn over everything it crosses so it never breaks: silver edge, then the navy band.
    path = ring_path()
    s.line(path, RING_W + 2 * RING_EDGE, SILVER)
    s.line(path, RING_W, NAVY)
    for n in LAYOUT['nodes']:
        if n['id'] == 'N2':
            continue   # the court node is the plinth
        node_pad(s, n, floor)
    if floor == '2F':
        # 4. Gate bands at every stair arrival (MATCH floor gates) and 5. edge strips on every drop.
        v = b['void']
        h = b['stairHole']
        fa, ea = b['featureArrival'], b['e1Arrival']
        gate(s, (fa['a'][0] - GATE_W / 2, fa['a'][1]), (fa['b'][0] - GATE_W / 2, fa['b'][1]), floor, 'FEATURE STAIR')
        for turned in (False, True):
            a, bb = (ea['a'][0], ea['a'][1] + GATE_W / 2), (ea['b'][0], ea['b'][1] + GATE_W / 2)
            gate(s, turn(a) if turned else a, turn(bb) if turned else bb, floor, 'E2' if turned else 'E1')
        edge(s, (v[2], v[1]), (v[0], v[1]), (0, 1))             # the bridge's edge (the hole is south of it)
        edge(s, (v[0], v[1] + 2), (v[0], v[3]), (1, 0))         # west, south of the flight's arrival
        edge(s, (v[0], v[3]), (v[2], v[3]), (0, -1))            # the canopy's edge
        edge(s, (v[2], v[3]), (v[2], v[1]), (-1, 0))            # east
        for turned in (False, True):
            segs = [((h[0], h[1]), (h[0], h[3]), (1, 0)), ((h[0], h[1]), (h[2], h[1]), (0, 1))]
            for a, bb, side in segs:
                if turned:
                    a, bb, side = turn(a), turn(bb), (-side[0], -side[1])
                edge(s, a, bb, side)
        s.clear(*v)
        s.clear(*h)
        s.clear(*turned_rect(h))
    s.clear(18, 0, 22, 4)   # the lift shaft
    return s.save(f'Marks_Floor_{floor}')


def turned_rect(r):
    return (W_PLATE - r[2], D_PLATE - r[3], W_PLATE - r[0], D_PLATE - r[1])


def threshold_sign(th):
    """+1 if the threshold's 'from' district lies on its normal side (right of a -> b), worked out on firm A's half;
    turning both the line and the plate by 180 degrees keeps the same answer for firm B's twin."""
    (ux, uy), (nx, ny) = run_geometry(tuple(th['a']), tuple(th['b']))
    mid = ((th['a'][0] + th['b'][0]) / 2, (th['a'][1] + th['b'][1]) / 2)
    return 1 if district_of((mid[0] + nx * 0.4, mid[1] + ny * 0.4)) == th['from'] else -1


def threshold(s, a, b, fr, to, sign):
    (ux, uy), (nx, ny) = run_geometry(a, b)
    for letter, k in ((fr, sign), (to, -sign)):
        off = k * THRESHOLD_W / 4
        s.line([(a[0] + nx * off, a[1] + ny * off), (b[0] + nx * off, b[1] + ny * off)], THRESHOLD_W / 2,
               DISTRICT_COLOUR[letter])
    s.line([a, b], 0.015, SILVER)
    length = math.hypot(b[0] - a[0], b[1] - a[1])
    angle = 0 if abs(uy) > 0.5 else 90    # letters stand across the strip
    count = max(1, int(length // 1.6))
    for i in range(count):
        t = (i + 0.5) / count
        px, py = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
        for letter, k in ((fr, sign), (to, -sign)):
            off = k * THRESHOLD_W / 4
            s.text(px + nx * off, py + ny * off, letter, ms.LATIN, 0.16, WHITE, angle=angle)


def tenancy(s, top, bottom):
    x, y0, y1 = top[0], top[1] + 0.25, bottom[1]
    s.rect(x - TENANCY_BAND - 0.01, y0, x - 0.01, y1, FIRM_A[1])
    s.rect(x + 0.01, y0, x + TENANCY_BAND + 0.01, y1, FIRM_B[1])
    s.rect(x - 0.01, y0, x + 0.01, y1, SILVER)
    lines = [(f'DEMISE LINE · TENANCY A · {FIRM_A[0].upper()} · NO CROSSING WITHOUT APPOINTMENT', -1),
             (f'DEMISE LINE · TENANCY B · {FIRM_B[0].upper()} · NO CROSSING WITHOUT APPOINTMENT', 1)]
    y = y0 + 2.6
    while y < y1 - 2.4:
        for body, side in lines:
            s.text(x + side * (TENANCY_BAND + 0.07), y, body, ms.LATIN, 0.03, NAVY, angle=90 * side)
        y += 6.0


def plinth(s, c):
    """The closing table: concentric glossy rings round the case start, a fine tick scale, its name."""
    s.disc(c, 1.75, outline=SILVER, width=0.015)
    for k in range(72):
        a = math.radians(k * 5)
        r0 = 1.55 if k % 6 == 0 else 1.62
        s.d.line([s.p(c[0] + r0 * math.cos(a), c[1] + r0 * math.sin(a)),
                  s.p(c[0] + 1.72 * math.cos(a), c[1] + 1.72 * math.sin(a))], fill=SILVER, width=s.m(0.012))
    s.disc(c, 1.45, outline=NAVY, width=0.18)
    s.disc(c, 1.0, fill=COBALT, outline=SILVER, width=0.015)
    s.disc(c, 0.36, fill=WHITE, outline=NAVY, width=0.02)
    s.text(c[0], c[1] + 2.0, 'CLOSING TABLE', ms.LATIN, 0.1, NAVY, tracking=0.3)
    s.text(c[0], c[1] - 2.05, '交渉卓', ms.JAPANESE, 0.12, NAVY, weight=700)
    s.text(c[0], c[1] + 2.25, 'THE CASE STARTS HERE', ms.LATIN, 0.05, NAVY, tracking=0.2)


def node_pad(s, n, floor):
    x, y = n['p']
    vertical = any(abs(x - v) < 0.01 for v in (6, 34))   # on the ring's west or east leg
    big = n['id'] == 'N1'
    w, h = (3.4, 1.5) if big else (2.6, 0.9)
    if vertical:
        w, h = h, w
    s.rrect(x, y, w + 2 * RING_EDGE, h + 2 * RING_EDGE, 0.3, SILVER)
    s.rrect(x, y, w, h, 0.28, NAVY)
    angle = 90 if vertical else 0
    name = n['name'].upper()
    if big:
        s.text(x, y - 0.2, floor, ms.LATIN, 0.45, WHITE)
        s.text(x, y + 0.48, f'{name} · {n["id"]}', ms.LATIN, 0.08, SILVER, tracking=0.2)
    else:
        s.text(x, y, f'{n["id"]}  {name}', ms.LATIN, 0.1, WHITE, angle=angle, tracking=0.15)


def gate(s, a, b, floor, stair):
    """A locked-door red band with hairline chevrons pointing out of the stair, and its rule in microtype."""
    s.line([a, b], GATE_W, GATE_RED)
    (ux, uy), (nx, ny) = run_geometry(a, b)
    length = math.hypot(b[0] - a[0], b[1] - a[1])
    k = 0.4
    t = k / 2
    while t < length:
        px, py = a[0] + ux * t, a[1] + uy * t
        tip = (px + nx * 0.09, py + ny * 0.09)
        s.d.line([s.p(px - ux * 0.09 - nx * 0.06, py - uy * 0.09 - ny * 0.06), s.p(*tip),
                  s.p(px + ux * 0.09 - nx * 0.06, py + uy * 0.09 - ny * 0.06)], fill=WHITE, width=s.m(0.015))
        t += k
    mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    angle = 90 if abs(uy) > 0.5 else 0
    s.text(mid[0] + nx * 0.26, mid[1] + ny * 0.26, f'{floor} · {stair} · OPENS WITH THE CASE', ms.LATIN, 0.05,
           GATE_RED, angle=angle, tracking=0.1)


def edge(s, a, b, outward):
    """Slip-and-edge contrast on a drop: a navy strip on the slab side of the edge with a silver line at the lip."""
    ox, oy = outward   # points into the hole
    inner = EDGE_W / 2 + 0.02
    s.d.line([s.p(a[0] - ox * inner, a[1] - oy * inner), s.p(b[0] - ox * inner, b[1] - oy * inner)], fill=NAVY,
             width=s.m(EDGE_W))
    s.d.line([s.p(a[0] - ox * 0.01, a[1] - oy * 0.01), s.p(b[0] - ox * 0.01, b[1] - oy * 0.01)], fill=SILVER,
             width=s.m(0.02))


# ---------------------------------------------------------------- ceiling marks

def ceiling_sheets():
    cove = Sheet()
    cove.line(ring_path(), COVE_W, COVE + (255,))
    cove.clear(18, 0, 22, 4)
    cove.save('Marks_Ceiling_Cove', flip=True)
    for floor in FLOORS:
        s = Sheet()
        for kind, a, bb, doors, d, turned in runs(floor):
            if kind != 'solid':
                continue   # glass stops at 2.5 m; only solid walls meet the soffit
            (ux, uy), (nx, ny) = run_geometry(a, bb)
            cx, cy = nx * 0.1, ny * 0.1
            s.d.line([s.p(a[0] + cx, a[1] + cy), s.p(bb[0] + cx, bb[1] + cy)], fill=DISTRICT_COLOUR[d],
                     width=s.m(0.2 + 2 * SOFFIT_BAND))
        b = KITPLAN['built']
        if floor == '1F':   # mood D: the bridge's and the canopy's undersides in the court's sky blue
            v = KITPLAN['built']['void']
            s.rect(v[0], 10.0, v[2], v[1], COURT_SKY)
            s.rect(v[0], v[3], v[2], 22.0, COURT_SKY)
        if floor == '1F':   # the ground floor's soffit is the upper slab, open over the void and the stair flights
            s.clear(*b['void'])
            s.clear(*b['stairHole'])
            s.clear(*turned_rect(b['stairHole']))
        s.clear(18, 0, 22, 4)
        s.save(f'Marks_Ceiling_{floor}', flip=True)


# ---------------------------------------------------------------- wall decals

def plates():
    rooms = KITPLAN['rooms']
    made = []
    for floor in FLOORS:
        for key, r in rooms.items():
            for side in ('A', 'B'):
                number = f'{floor[0]}{0 if side == "A" else 1}{r["number"]}'   # 101-105 firm A's side, 111-115 B's
                en, ja = (r['en'], r['ja']) if side == 'A' else (r['enB'], r['jaB'])
                img, den = ms.canvas(0.32, 0.14)
                d = ImageDraw.Draw(img)
                L = lambda m: ms.lpx(m, den)
                d.rounded_rectangle((0, 0, img.width - 1, img.height - 1), radius=L(0.012), fill=NAVY)
                d.rectangle((L(0.012), L(0.012), L(0.016), img.height - L(0.012)),
                            fill=FIRM_A[1] if side == 'A' else FIRM_B[1])
                fn = ms.fitted(ms.LATIN, 0.032, den)
                d.text((L(0.03), L(0.055)), number, font=fn, fill=WHITE, anchor='ls')
                fe = ms.fitted(ms.LATIN, 0.0135, den, body=en, max_w_px=L(0.27))
                d.text((L(0.03), L(0.088)), en, font=fe, fill=SILVER, anchor='ls')
                fj = ms.fitted(ms.JAPANESE, 0.02, den, 600)
                d.text((L(0.03), L(0.122)), ja, font=fj, fill=WHITE, anchor='ls')
                name = f'Plate_{key}_{side}_{floor}'
                ms.save_label(img, name)
                made.append(name)
    return made


def floor_numbers():
    for stair in ('E1', 'E2', 'Feature'):
        for floor in FLOORS:
            img, den = ms.canvas(1.2, 0.9, 400)
            d = ImageDraw.Draw(img)
            L = lambda m: ms.lpx(m, den)
            f = ms.fitted(ms.LATIN, 0.5, den)
            d.text((L(0.02), L(0.6)), floor, font=f, fill=COBALT, anchor='ls')
            label = 'FEATURE STAIR' if stair == 'Feature' else f'STAIR {stair}'
            fs = ms.fitted(ms.LATIN, 0.06, den)
            d.text((L(0.03), L(0.74)), label, font=fs, fill=NAVY, anchor='ls')
            fj = ms.fitted(ms.JAPANESE, 0.07, den, 700)
            d.text((L(0.03), L(0.86)), '階段' if stair != 'Feature' else '中央階段', font=fj, fill=NAVY, anchor='ls')
            ms.save_label(img, f'FloorNo_{stair}_{floor}')


def glazing():
    """Manifestation on breakable glass only: two bands of fine frosted dots (0.95 m and 1.5 m), on a 0.7 m strip
    whose bottom sits at 0.9 m. Fixed glass (curtain walls, windows, the lift shaft) carries none."""
    for name, w in (('Glazing_4m', 4.0), ('Glazing_2m', 2.0), ('Glazing_Jamb', 0.36)):
        img, den = ms.canvas(w, 0.7, 600)
        d = ImageDraw.Draw(img)
        L = lambda m: ms.lpx(m, den)
        pitch, dot = 0.03, 0.012
        for band in (0.05, 0.6):        # band centres above the strip's bottom (0.95 and 1.5 m)
            for row in (-1, 0, 1):
                y = img.height - L(band + row * pitch)
                x = pitch / 2 + (pitch / 2 if row else 0)
                while x < w:
                    cx = L(x)
                    d.ellipse((cx - L(dot / 2), y - L(dot / 2), cx + L(dot / 2), y + L(dot / 2)), fill=(255, 255, 255, 215))
                    x += pitch
        ms.save_label(img, name)


def firm_lightboxes():
    for side, (name, colour) in (('A', FIRM_A), ('B', FIRM_B)):
        w_m, h_m = ms.LIGHTBOX_INNER
        img, den = ms.canvas(w_m, h_m, ms.LIGHTBOX_PX_PER_M)
        W, H = img.size
        deep = tuple(int(c * 0.55) for c in colour)
        img.paste(ms.gradient((W, H), colour, deep), (0, 0))
        d = ImageDraw.Draw(img)
        L = lambda m: ms.lpx(m, den)
        words = name.upper()
        f = ms.fitted(ms.LATIN, 0.2, den, body=words, max_w_px=W - L(0.5))
        d.text((L(0.25), H - L(1.75)), words, font=f, fill=WHITE, anchor='ls')
        d.rectangle((L(0.25), H - L(1.6), W - L(0.25), H - L(1.59)), fill=WHITE)
        fs = ms.fitted(ms.LATIN, 0.07, den)
        d.text((L(0.25), H - L(1.4)), f'ATTORNEYS AT LAW  ·  TENANCY {side}  ·  RECEPTION', font=fs, fill=WHITE, anchor='ls')
        fj = ms.fitted(ms.JAPANESE, 0.1, den, 700)
        d.text((L(0.25), H - L(1.18)), '法律事務所  受付', font=fj, fill=WHITE, anchor='ls')
        ms.gel(img, den, 0.36, 50)
        ms.save_label(img, f'Lightbox_Firm_{side}')


def evac_plans():
    """'You are here' evacuation plans of the built L2 floor: every wall, the stairs in safety green with the way
    out, district letters, and the reader's own spot. Turned so the reader's forward is up, as real plans are."""
    for e in KITPLAN['decals']['evac']:
        for side in ('A', 'B'):
            at = tuple(e['at']) if side == 'A' else turn(tuple(e['at']))
            face = tuple(e['face']) if side == 'A' else (-e['face'][0], -e['face'][1])
            for floor in FLOORS:
                evac(f'Evac_{e["id"]}_{side}_{floor}', at, face, floor)


def evac(name, here, face, floor):
    w_m, h_m = 0.6, 0.42
    img, den = ms.canvas(w_m, h_m, 1000)
    d = ImageDraw.Draw(img)
    L = lambda m: ms.lpx(m, den)
    W, H = img.size
    d.rounded_rectangle((0, 0, W - 1, H - 1), radius=L(0.01), fill=(0xFA, 0xFC, 0xFC, 255), outline=SAFETY_GREEN,
                        width=L(0.004))
    d.rectangle((0, 0, W, L(0.05)), fill=SAFETY_GREEN)
    fh = ms.fitted(ms.LATIN, 0.016, den)
    d.text((L(0.015), L(0.033)), f'EVACUATION PLAN · {floor}', font=fh, fill=WHITE, anchor='ls')
    fj = ms.fitted(ms.JAPANESE, 0.02, den, 700)
    d.text((W - L(0.015), L(0.035)), '避難経路図', font=fj, fill=WHITE, anchor='rs')
    # plan area, turned so the reader's forward (-face) points up the card
    fx, fy = -face[0], -face[1]
    th = -math.pi / 2 - math.atan2(fy, fx)   # turns the reader's forward to "up" on the card, (0, -1)
    ca, sa = math.cos(th), math.sin(th)
    cx, cy = W / 2, L(0.05) + (H - L(0.05)) / 2
    scale = min((W - L(0.04)), (H - L(0.09))) / 40.0

    def P(x, y):
        u, v = x - 20, y - 16
        return (cx + (u * ca - v * sa) * scale, cy + (u * sa + v * ca) * scale)
    poly = [P(0, 0), P(40, 0), P(40, 32), P(0, 32)]
    d.polygon(poly, outline=NAVY, width=L(0.002))
    for kind, a, b, doors, dist, turned in runs(floor):
        if kind == 'guard':
            continue
        d.line([P(*a), P(*b)], fill=NAVY if kind == 'solid' else (0x6A, 0x80, 0xA8), width=L(0.0018))
    v = KITPLAN['built']['void']
    d.polygon([P(v[0], v[1]), P(v[2], v[1]), P(v[2], v[3]), P(v[0], v[3])], outline=(0x6A, 0x80, 0xA8),
              width=L(0.0012))
    for r in ((8, 6, 14, 14), turned_rect((8, 6, 14, 14))):
        d.polygon([P(r[0], r[1]), P(r[2], r[1]), P(r[2], r[3]), P(r[0], r[3])], fill=SAFETY_GREEN)
    d.polygon([P(18, 0), P(22, 0), P(22, 4), P(18, 4)], fill=(0x9A, 0xA8, 0xBE))
    fl = ms.fitted(ms.LATIN, 0.012, den)
    for letter, p in (('L', (3, 3)), ('A', (3, 30)), ('H', (20, 16)), ('R', (37, 29)), ('C', (37, 3))):
        d.text(P(*p), letter, font=fl, fill=COBALT, anchor='mm')
    for p in ((11, 10), turn((11, 10))):
        d.text(P(*p), 'EXIT', font=ms.fitted(ms.LATIN, 0.007, den), fill=WHITE, anchor='mm')
    hx, hy = P(*here)
    r = L(0.007)
    d.ellipse((hx - r, hy - r, hx + r, hy + r), fill=GATE_RED, outline=WHITE, width=L(0.0015))
    fy_ = ms.fitted(ms.LATIN, 0.009, den)
    d.text((L(0.015), H - L(0.012)), 'YOU ARE HERE', font=fy_, fill=GATE_RED, anchor='ls')
    d.text((L(0.135), H - L(0.012)), '現在地', font=ms.fitted(ms.JAPANESE, 0.011, den, 700), fill=GATE_RED, anchor='ls')
    ms.save_label(img, name)


def zone_numbers():
    """Giant white zone numbers for the coloured wall fields (mood D): 01 and 02 on firm A's half, 03 and 04 on B's."""
    for n in ('01', '02', '03', '04'):
        img, den = ms.canvas(2.6, ZONE_NUMBER_H, 200)
        d = ImageDraw.Draw(img)
        f = ms.fitted(ms.LATIN, ZONE_NUMBER_H * 0.72, den, body=n, max_w_px=img.width)
        d.text((img.width // 2, img.height - ms.lpx(0.25, den)), n, font=f, fill=WHITE, anchor='ms')
        ms.save_label(img, f'Zone_Number_{n}')


def plain(name, colour):
    Image.new('RGBA', (8, 8), colour + (255,)).save(os.path.join(OUT, name + '.png'))


def review(floor_imgs):
    """A contact sheet of both floor sheets for review."""
    sheet = Image.new('RGB', (2 * 1600 + 60, 1340), (0xE8, 0xEC, 0xF0))
    for i, im in enumerate(floor_imgs):
        crop = im.crop((0, 0, SHEET_PX, int(SHEET_PX * D_PLATE / SHEET_M)))
        bg = Image.new('RGBA', crop.size, (0xF4, 0xF6, 0xF8, 255))
        bg.alpha_composite(crop)
        sheet.paste(bg.convert('RGB').resize((1600, 1280)), (20 + i * 1620, 40))
    os.makedirs(os.path.join(HERE, 'review'), exist_ok=True)
    sheet.save(os.path.join(HERE, 'review', 'markings_sheet.png'))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    floors = [floor_sheet(f) for f in FLOORS]
    ceiling_sheets()
    plates()
    floor_numbers()
    glazing()
    firm_lightboxes()
    evac_plans()
    plain('Band_Cobalt', COBALT)
    plain('Paint_HalfA', HALF_A_BLUE)
    plain('Paint_HalfB', HALF_B_BLUE)
    plain('Paint_Court', COURT_SKY)
    plain('Glow_Blue', (0x5C, 0xA8, 0xFF))   # the feature stair's glowing edge (mood D)
    zone_numbers()
    review(floors)
    print('firms:', FIRM_A, FIRM_B)
