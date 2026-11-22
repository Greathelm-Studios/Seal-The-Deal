"""Exact-shape UV packing, for uvkit.pack(shape='EXACT'). numpy only. See references/uv-unwrapping.md.

Each island is turned so its smallest bounding rectangle lies along U or V, then tried at the four 90-degree turns.
It is drawn on a grid of the texture's pixels as every pixel one of its triangles touches, and placed where that
drawing misses every occupied pixel: the texture's border, and each island already placed grown by the padding (by
the exact distance between pixel squares). So on the final UVs no two islands are closer than the padding, none is
closer to the edge than the padding (what pack_islands leaves with a FRACTION margin), and each island's lower-left
corner sits on a whole pixel. Islands nest into each other's concave gaps and into holes.

Islands go in one at a time (biggest box or outline first, so frames and rings come before what can fill them), each
where its grown drawing touches the most occupied cells, or else lowest; right after an island that walls off room
(a frame's middle), the islands still to come are tried inside that room first. The search runs on a grid of about
256 cells across, over several orderings and both rules, for the largest scale at which every island fits; coarse
cells are drawn conservatively, so a coarse fit is also a fit at full resolution. The two best are then run again on
a grid twice as fine, which sees fits the coarse one missed, and the winner is settled at whole pixels, pushed toward
a corner and grown into the room that frees. Free places come from correlating the occupancy with an island's
drawing: by FFT for big drawings, by sliding row sums for small ones. The search is bounded by a count of grid-cell
operations rather than by a clock, so the same input gives the same UVs on every run.
"""
import math

import numpy as np

COARSE = 256      # cells across the search grid
STEP = 1.004      # scales are tried on a ladder of this ratio, so drawings are reused between tries
WINDOW = 6        # pixels an island may move while the layout settles at whole pixels
BUDGET = 4e9      # search work per stage, in grid-cell operations: counted, not timed, so runs repeat exactly
BAND = 6          # identical drawing rows merged into one sliding sum from this many
FFT_COST = 40     # cell operations per grid cell for one FFT correlation, against about one for a row sum
CALL_COST = 4000  # cell operations' worth of time per numpy call
EPS = 1e-6        # px: a coordinate this close to a pixel edge is on it
HOLE_CELLS = 64   # cells along an island's longer side when looking for room it walls off
MEMO = 8          # identical drawings whose maps are kept for the next one
MEMO_CHANGES = 16 # placements since then past which the maps are made afresh
# (order, rule) pairs the search tries, the first searched fully, the rest only for a better scale. Orders put the
# islands that enclose room (frames, rings: big box or outline) before those that can fill it; 'snug' places each
# where its padding touches the most occupied cells, 'low' lowest.
CONFIGS = (('box', 'snug'), ('edge', 'snug'), ('side', 'snug'), ('area', 'snug'),
           ('box', 'low'), ('edge', 'low'), ('side', 'low'), ('area', 'low'))


# ---------------------------------------------------------------- Blender meshes


def read_islands(obs, tol=1e-6):
    """The UV islands of the objects' active UV maps, faces joined across an edge whose two ends match in UV (as
    uvkit.uv_islands does): a list of (object index, loop indices, triangles as a (T, 3, 2) array of UVs)."""
    out = []
    for oi, ob in enumerate(obs):
        me = ob.data
        if not me.uv_layers or not me.polygons:
            continue
        nl, nf = len(me.loops), len(me.polygons)
        uv = np.empty(nl * 2, np.float32)
        me.uv_layers.active.data.foreach_get('uv', uv)
        uv = uv.reshape(-1, 2).astype(np.float64)
        start, total = np.empty(nf, np.int64), np.empty(nf, np.int64)
        me.polygons.foreach_get('loop_start', start)
        me.polygons.foreach_get('loop_total', total)
        vert, edge = np.empty(nl, np.int64), np.empty(nl, np.int64)
        me.loops.foreach_get('vertex_index', vert)
        me.loops.foreach_get('edge_index', edge)
        face = np.repeat(np.arange(nf), total)
        nxt = np.arange(1, nl + 1)
        nxt[start + total - 1] = start

        order = np.argsort(edge, kind='stable')
        cut = np.flatnonzero(np.diff(edge[order])) + 1
        groups = np.split(order, cut)
        a = np.array([g[0] for g in groups if len(g) == 2], np.int64)
        b = np.array([g[1] for g in groups if len(g) == 2], np.int64)
        for g in groups:  # an edge of three or more faces: every pair
            if len(g) > 2:
                pa, pb = np.triu_indices(len(g), 1)
                a, b = np.concatenate([a, g[pa]]), np.concatenate([b, g[pb]])
        same_way = (vert[a] == vert[b])[:, None]
        b0 = np.where(same_way, uv[b], uv[nxt[b]])
        b1 = np.where(same_way, uv[nxt[b]], uv[b])
        join = ((np.hypot(*(uv[a] - b0).T) < tol) & (np.hypot(*(uv[nxt[a]] - b1).T) < tol))

        parent = list(range(nf))

        def find(x):
            while parent[x] != x:
                parent[x] = parent[parent[x]]
                x = parent[x]
            return x

        for fa, fb in zip(face[a[join]].tolist(), face[b[join]].tolist()):
            ra, rb = find(fa), find(fb)
            if ra != rb:
                parent[max(ra, rb)] = min(ra, rb)
        roots, of_face = np.unique([find(f) for f in range(nf)], return_inverse=True)

        me.calc_loop_triangles()
        nt = len(me.loop_triangles)
        tl, tp = np.empty(nt * 3, np.int64), np.empty(nt, np.int64)
        me.loop_triangles.foreach_get('loops', tl)
        me.loop_triangles.foreach_get('polygon_index', tp)
        tl = tl.reshape(-1, 3)
        loop_isl, tri_isl = of_face[face], of_face[tp]
        lo, to = np.argsort(loop_isl, kind='stable'), np.argsort(tri_isl, kind='stable')
        lsplit = np.split(lo, np.searchsorted(loop_isl[lo], np.arange(1, len(roots))))
        tsplit = np.split(to, np.searchsorted(tri_isl[to], np.arange(1, len(roots))))
        for loops, tris in zip(lsplit, tsplit):
            out.append((oi, loops, uv[tl[tris]]))
    return out


def write_islands(obs, islands, placed):
    """Writes each island's new UVs, placed[i] = (2 x 2 matrix, offset), as uv @ matrix + offset."""
    by_ob = {}
    for (oi, loops, _), (mat, off) in zip(islands, placed):
        by_ob.setdefault(oi, []).append((loops, mat, off))
    for oi, items in by_ob.items():
        data = obs[oi].data.uv_layers.active.data
        uv = np.empty(len(data) * 2, np.float32)
        data.foreach_get('uv', uv)
        uv = uv.reshape(-1, 2)
        new = uv.astype(np.float64)
        for loops, mat, off in items:
            new[loops] = new[loops] @ mat + off
        data.foreach_set('uv', new.astype(np.float32).ravel())
        obs[oi].data.update()


def pack(obs, gap, size, scale=True):
    """Packs every island of the objects' active UV maps into 0..1 by its exact shape, for a texture of `size`
    pixels with `gap` (a fraction of it) between islands and at the edge. With scale False the islands keep their
    size; when they don't fit, some are left past 1 (see uvkit.in_unit_square)."""
    islands = read_islands(obs)
    if islands:
        write_islands(obs, islands, pack_islands([t for _, _, t in islands], gap * size, size, scale))


# ---------------------------------------------------------------- island shapes


def convex_hull(p):
    p = np.unique(p, axis=0)
    if len(p) < 3:
        return p

    def half(pts):
        h = []
        for q in pts.tolist():
            while len(h) >= 2 and ((h[-1][0] - h[-2][0]) * (q[1] - h[-2][1])
                                   - (h[-1][1] - h[-2][1]) * (q[0] - h[-2][0])) <= 0:
                h.pop()
            h.append(q)
        return h

    return np.array(half(p)[:-1] + half(p[::-1])[:-1])


def lie_flat(pts):
    """The rotation (a 2 x 2 matrix acting on row vectors) that turns the points' smallest bounding rectangle to
    lie along U and V, by the least turn when several are as small (a regular polygon)."""
    hull = convex_hull(pts)
    if len(hull) < 3:
        return np.eye(2)
    e = np.roll(hull, -1, 0) - hull
    ang = np.arctan2(e[:, 1], e[:, 0]) % (math.pi / 2)
    c, s = np.cos(ang)[:, None], np.sin(ang)[:, None]
    x = hull[:, 0] * c + hull[:, 1] * s
    y = hull[:, 1] * c - hull[:, 0] * s
    area = (x.max(1) - x.min(1)) * (y.max(1) - y.min(1))
    turn = np.minimum(ang, math.pi / 2 - ang)
    small = np.flatnonzero(area <= area.min() * (1 + 1e-9))
    a = ang[small[np.argmin(turn[small])]]
    a = a - math.pi / 2 if a > math.pi / 4 else a
    return np.array([[math.cos(a), -math.sin(a)], [math.sin(a), math.cos(a)]])


QUARTER = np.array([[0.0, 1.0], [-1.0, 0.0]])  # (x, y) @ QUARTER = (-y, x): a 90-degree turn


class Island:
    """An island's four turns, each with its box's lower-left corner at the origin, and what the orderings sort by."""

    def __init__(self, tris):
        o = tris.reshape(-1, 2).min(0)
        flat = lie_flat(tris.reshape(-1, 2) - o)
        self.turns = []
        mat = flat
        for _ in range(4):
            t = (tris - o) @ mat
            lo = t.reshape(-1, 2).min(0)
            self.turns.append((mat, o @ mat + lo, t - lo))  # uv @ mat - offset = the turned island
            mat = mat @ QUARTER
        t0 = self.turns[0][2]
        w, h = t0.reshape(-1, 2).max(0)
        self.area = float(np.abs(cross(t0[:, 1] - t0[:, 0], t0[:, 2] - t0[:, 0])).sum() / 2)
        self.edge = perimeter(t0)
        self.side, self.box = float(max(w, h)), float(w * h)
        # Room the island walls off (a frame's or a ring's middle): its box in each turn, a HOLE_CELLS cell wider.
        self.holes = [None] * 4
        if self.side > 0:
            k = HOLE_CELLS / self.side
            ys, xs = np.nonzero(enclosed(cover(t0 * k)))
            if len(xs):
                p0 = np.array([[xs.min() - 1, ys.min() - 1], [xs.max() + 2, ys.min() - 1],
                               [xs.min() - 1, ys.max() + 2], [xs.max() + 2, ys.max() + 2]]) / k
                mat0, off0, _ = self.turns[0]
                uv = (p0 + off0) @ mat0.T
                for t, (mat, off, _) in enumerate(self.turns):
                    p = uv @ mat - off
                    self.holes[t] = (*p.min(0), *p.max(0))


def enclosed(cells):
    """Empty cells of a drawing that its cells wall off from the outside (4-connected)."""
    h, w = cells.shape
    free = np.ones((h + 2, w + 2), bool)
    free[1:-1, 1:-1] = ~cells
    out = np.zeros_like(free)
    out[0], out[-1], out[:, 0], out[:, -1] = True, True, True, True
    while True:
        nxt = out.copy()
        nxt[1:] |= out[:-1]
        nxt[:-1] |= out[1:]
        nxt[:, 1:] |= out[:, :-1]
        nxt[:, :-1] |= out[:, 1:]
        nxt &= free
        if np.array_equal(nxt, out):
            return free[1:-1, 1:-1] & ~out[1:-1, 1:-1]
        out = nxt


def cross(a, b):
    return a[..., 0] * b[..., 1] - a[..., 1] * b[..., 0]


def perimeter(tris):
    """Length of the island's outline: triangle edges not shared by two of its triangles."""
    seen = {}
    for t in tris.tolist():
        for e in range(3):
            p, q = tuple(t[e]), tuple(t[(e + 1) % 3])
            k = (p, q) if p <= q else (q, p)
            seen[k] = seen.get(k, 0) + 1
    return float(sum(math.dist(*k) for k, n in seen.items() if n == 1))


# ---------------------------------------------------------------- drawing islands on grids


def cover(tris):
    """The cells (rows, columns) that the triangles, in cell units with their box's lower-left corner at the
    origin, touch: every cell whose open square meets a triangle."""
    pts = tris.reshape(-1, 2)
    w = max(1, int(math.ceil(pts[:, 0].max() - EPS)))
    h = max(1, int(math.ceil(pts[:, 1].max() - EPS)))
    ys = tris[..., 1]
    ymin, ymax = ys.min(1), ys.max(1)
    j0 = np.floor(ymin + EPS).astype(np.int64)
    j1 = np.maximum(np.ceil(ymax - EPS).astype(np.int64) - 1, j0)
    n = j1 - j0 + 1
    ti = np.repeat(np.arange(len(tris)), n)
    row = j0[ti] + np.arange(n.sum()) - np.repeat(np.cumsum(n) - n, n)
    y0, y1 = np.maximum(row, ymin[ti]), np.minimum(row + 1, ymax[ti])
    lo, hi = np.full(len(ti), np.inf), np.full(len(ti), -np.inf)
    for e in range(3):  # each edge clipped to the row's slab; the triangle's slab is spanned by their ends
        p, q = tris[ti, e], tris[ti, (e + 1) % 3]
        dy = q[:, 1] - p[:, 1]
        flat = np.abs(dy) < 1e-12
        with np.errstate(divide='ignore', invalid='ignore'):
            ta, tb = (y0 - p[:, 1]) / dy, (y1 - p[:, 1]) / dy
        t0 = np.where(flat, 0.0, np.clip(np.minimum(ta, tb), 0, 1))
        t1 = np.where(flat, 1.0, np.clip(np.maximum(ta, tb), 0, 1))
        xa, xb = p[:, 0] + t0 * (q[:, 0] - p[:, 0]), p[:, 0] + t1 * (q[:, 0] - p[:, 0])
        meets = (np.minimum(p[:, 1], q[:, 1]) <= y1) & (np.maximum(p[:, 1], q[:, 1]) >= y0)
        lo = np.where(meets, np.minimum(lo, np.minimum(xa, xb)), lo)
        hi = np.where(meets, np.maximum(hi, np.maximum(xa, xb)), hi)
    c0 = np.clip(np.floor(lo + EPS).astype(np.int64), 0, w - 1)
    c1 = np.clip(np.maximum(np.ceil(hi - EPS).astype(np.int64) - 1, c0), 0, w - 1)
    d = (np.bincount(row * (w + 1) + c0, minlength=h * (w + 1))
         - np.bincount(row * (w + 1) + c1 + 1, minlength=h * (w + 1)))
    return np.cumsum(d.reshape(h, w + 1)[:, :w], axis=1) > 0


def grow(cells, g):
    """The cells grown by every cell whose square is closer than g to one of theirs, and the margin m: the grown
    array is m cells bigger on each side."""
    h, w = cells.shape
    m = int(math.ceil(g + 1)) - 1 if g > 0 else 0
    if m <= 0:
        return cells.copy(), 0
    W = w + 2 * m
    sums = np.zeros((h, W + 1), np.int32)
    sums[:, m + 1:m + 1 + w] = np.cumsum(cells, axis=1)
    sums[:, m + 1 + w:] = sums[:, m + w:m + w + 1]
    x = np.arange(W)
    out = np.zeros((h + 2 * m, W), bool)
    reach = {}
    for dy in range(-m, m + 1):
        a = max(abs(dy) - 1, 0)
        if a >= g:
            continue
        r = int(math.ceil(1 + math.sqrt(g * g - a * a))) - 1
        if r not in reach:
            reach[r] = sums[:, np.minimum(x + r + 1, W)] > sums[:, np.maximum(x - r, 0)]
        out[m + dy:m + dy + h] |= reach[r]
    return out, m


def bands(cells):
    """The drawing's rows as runs, consecutive identical rows merged: [(first row, rows, starts, lengths)]."""
    h, w = cells.shape
    p = np.zeros((h, w + 2), np.int8)
    p[:, 1:-1] = cells
    d = np.diff(p, axis=1)
    rows, starts = np.nonzero(d == 1)
    lengths = np.nonzero(d == -1)[1] - starts  # nonzero runs row by row, so ends pair with starts
    at = np.searchsorted(rows, np.arange(h + 1))
    first = np.flatnonzero(np.concatenate([[True], (cells[1:] != cells[:-1]).any(axis=1)]))
    count = np.diff(np.append(first, h))
    return [(r, k, starts[at[r]:at[r + 1]], lengths[at[r]:at[r + 1]]) for r, k in zip(first.tolist(), count.tolist())]


class Drawing:
    """An island on a grid at one scale and turn: the `cells` it covers, and those `grown` by the padding (m cells
    more on each side)."""

    def __init__(self, tris, g):
        self.cells = cover(tris)
        self.grown, self.m = grow(self.cells, g)
        self.h, self.w = self.cells.shape
        self.key = (self.cells.shape, self.cells.tobytes(), self.m)  # the same for identical drawings
        self._bands = {}
        self._spec = {}

    def bands(self, grown):
        if grown not in self._bands:
            self._bands[grown] = bands(self.grown if grown else self.cells)
        return self._bands[grown]

    def spec(self, grown, shape):
        key = (grown, shape)
        if key not in self._spec:
            self._spec[key] = np.conj(np.fft.rfft2(self.grown if grown else self.cells, s=shape))
        return self._spec[key]


class Field:
    """Occupied cells, with the row sums and spectrum that overlap tests share until the next placement."""

    def __init__(self, occ, meter):
        self.occ, self.meter = occ, meter
        self._sums = self._spec = None
        self.changes = []  # the box each placement changed, in order

    def place(self, cells, x, y):
        h, w = cells.shape
        n, m = self.occ.shape
        x0, y0, x1, y1 = max(x, 0), max(y, 0), min(x + w, m), min(y + h, n)
        self.occ[y0:y1, x0:x1] |= cells[y0 - y:y1 - y, x0 - x:x1 - x]
        self._sums = self._spec = None
        self.changes.append((x0, y0, x1, y1))

    def sums(self):
        if self._sums is None:
            n, m = self.occ.shape
            self._sums = np.zeros((n, m + 1), np.int32)
            np.cumsum(self.occ, axis=1, out=self._sums[:, 1:])
            self.meter.add(n * m)
        return self._sums

    def spec(self):
        if self._spec is None:
            self._spec = np.fft.rfft2(self.occ)
            self.meter.add(FFT_COST * self.occ.size)
        return self._spec

    def hits(self, d, grown=False, count=False):
        """Over every place of the drawing (its cells, or its grown cells) wholly inside the field: whether it
        covers an occupied cell, or how many it covers. None when it is bigger than the field."""
        n, m = self.occ.shape
        h, w = (d.h + 2 * d.m, d.w + 2 * d.m) if grown else (d.h, d.w)
        hy, wx = n - h + 1, m - w + 1
        if hy <= 0 or wx <= 0:
            return None
        rows = d.bands(grown)
        by_rows = sum(len(st) * (n * wx * 3 + hy * wx * 2 + 3 * CALL_COST) if k >= BAND
                      else len(st) * k * (hy * wx * 2 + 2 * CALL_COST) for _, k, st, _ in rows)
        by_fft = FFT_COST * n * m * (1 if (grown, (n, m)) in d._spec else 2) + (0 if self._spec is not None
                                                                                   else FFT_COST * n * m)
        if by_fft < by_rows:
            self.meter.add(by_fft)
            c = np.fft.irfft2(self.spec() * d.spec(grown, (n, m)), s=(n, m))[:hy, :wx]
            return np.rint(c).astype(np.int32) if count else c > 0.5
        self.meter.add(by_rows)
        s = self.sums()
        out = np.zeros((hy, wx), np.int32 if count else bool)
        for r0, k, starts, lengths in rows:
            for st, ln in zip(starts.tolist(), lengths.tolist()):
                if k >= BAND:
                    v = s[:, st + ln:st + ln + wx] - s[:, st:st + wx]
                    t = np.zeros((n + 1, wx), np.int32)
                    np.cumsum(v if count else v > 0, axis=0, out=t[1:])
                    part = t[r0 + k:r0 + k + hy] - t[r0:r0 + hy]
                    if count:
                        out += part
                    else:
                        out |= part > 0
                else:
                    for r in range(r0, r0 + k):
                        part = s[r:r + hy, st + ln:st + ln + wx] - s[r:r + hy, st:st + wx]
                        if count:
                            out += part
                        else:
                            out |= part > 0
        return out

    def maps(self, d, snug, memo):
        """The drawing's hit map over the whole field, and for snug its grown drawing's touch counts. A drawing
        identical to one seen lately (repeated parts: rivets, bolts) takes that one's maps, redone only round what
        was placed since."""
        got = memo.pop(d.key, None)
        if got is not None and len(self.changes) - got[2] <= MEMO_CHANGES and (got[1] is not None or not snug):
            hit, touch, seen = got
            for box in self.changes[seen:]:
                for grown, out in ((False, hit), (True, touch)):
                    if out is None or (grown and not snug):
                        continue
                    h, w = (d.h + 2 * d.m, d.w + 2 * d.m) if grown else (d.h, d.w)
                    x0, x1 = max(box[0] - w + 1, 0), min(box[2] - 1, out.shape[1] - 1)
                    y0, y1 = max(box[1] - h + 1, 0), min(box[3] - 1, out.shape[0] - 1)
                    if x1 >= x0 and y1 >= y0:
                        out[y0:y1 + 1, x0:x1 + 1] = Field(self.occ[y0:y1 + h, x0:x1 + w], self.meter).hits(
                            d, grown, count=grown)
        else:
            hit = self.hits(d)
            touch = self.hits(d, grown=True, count=True) if snug and hit is not None else None
        memo[d.key] = (hit, touch, len(self.changes))
        while len(memo) > MEMO:
            memo.pop(next(iter(memo)))
        return hit, touch


class Meter:
    def __init__(self):
        self.work = 0.0

    def add(self, n):
        self.work += n


# ---------------------------------------------------------------- the search


class Layout:
    def __init__(self, order, rule, k, placed):
        self.order, self.rule, self.k, self.placed = order, rule, k, placed  # placed[i] = (turn, x, y) in px


class Packer:
    """The search for one set of islands; run() gives the layout and placements() its UV transforms."""

    def __init__(self, tris, pad, size, scale, border, budget):
        self.isl = [Island(t) for t in tris]
        self.pad, self.size, self.border, self.budget = float(pad), int(size), float(border), budget
        self.meter = Meter()
        self.c0 = 1
        while size // (self.c0 * 2) >= COARSE:
            self.c0 *= 2
        self.drawings, self.distinct = {}, {}
        self.top = self.area_bound()  # the scale ladder steps down from here
        self.want = None if scale else float(size)  # with fixed scale one UV unit stays the whole texture

    def area_bound(self):
        """Pixels per UV unit at which the islands, each grown by half the padding, would cover the texture inside
        the border exactly: no layout fits a larger scale."""
        r = self.pad / 2
        room = (self.size - 2 * max(self.border - r, 0)) ** 2
        a = sum(i.area for i in self.isl)
        b = sum(i.edge for i in self.isl) * r
        c = len(self.isl) * math.pi * r * r - room
        if a <= 0:
            return float(self.size)
        if c >= 0:
            return 1e-3 * self.size
        return (-b + math.sqrt(b * b - 4 * a * c)) / (2 * a)

    def scale(self, k):
        return self.top * STEP ** -k

    def drawing(self, i, t, k, c):
        key = (i, t, k, c)
        d = self.drawings.get(key)
        if d is None:
            d = self.drawings[key] = Drawing(self.isl[i].turns[t][2] * (self.scale(k) / c), self.pad / c)
            self.meter.add(d.cells.size * 20 + d.grown.size * 10 + 30 * CALL_COST)
        return d

    def turns(self, i, k, c):
        """The island's drawings at its distinct turns (a turn that draws the same cells as an earlier one is
        skipped)."""
        key = (i, k, c)
        if key not in self.distinct:
            out = []
            for t in range(4):
                d = self.drawing(i, t, k, c)
                if not any(e.key == d.key for _, e in out):
                    out.append((t, d))
            self.distinct[key] = out
        return self.distinct[key]

    def field(self, c):
        """The grid of cell size c, its border occupied: every cell that holds a pixel within the border."""
        n = self.size // c
        occ = np.zeros((n, n), bool)
        b = int(math.ceil(self.border - EPS))
        if b > 0:
            lo, hi = -(-b // c), (self.size - b) // c
            occ[:lo], occ[:, :lo] = True, True
            occ[hi:], occ[:, hi:] = True, True
        return Field(occ, self.meter)

    def attempt(self, order, rule, k, c):
        """Places the islands in order on the grid of cell size c at ladder step k, each at its best place and turn
        by the rule; the layout, or None when one finds no room."""
        f = self.field(c)
        placed, memo = {}, {}
        self.meter.add(len(order) * 4 * CALL_COST)  # every attempt spends budget, even one turned down at once
        # Hopeless at this scale: an island too big for the empty grid in every turn, or more cells than room.
        free = np.flatnonzero(~f.occ[f.occ.shape[0] // 2])
        span, room, need = (free[-1] - free[0] + 1 if len(free) else 0), (~f.occ).sum(), 0
        for i in order:
            ds = [d for _, d in self.turns(i, k, c)]
            if all(max(d.w, d.h) > span for d in ds):
                return None
            need += min(int(d.cells.sum()) for d in ds)
        if need > room:
            return None

        def put(i, pick):
            _, t, x, y, d = pick
            f.place(d.grown, x - d.m, y - d.m)
            placed[i] = (t, x * c, y * c)

        for i in order:
            if i in placed:
                continue
            pick = self.best_place(f, i, k, c, rule, memo=memo)
            if pick is None:
                return None
            put(i, pick)
            hole = self.isl[i].holes[pick[1]]
            if hole is not None:  # fill the room it walls off first, with whichever islands still to come fit
                s = self.scale(k) / c
                x, y = pick[2], pick[3]
                box = (x + int(hole[0] * s), y + int(hole[1] * s),
                       x + int(math.ceil(hole[2] * s)), y + int(math.ceil(hole[3] * s)))
                for j in order:
                    if j not in placed:
                        inner = self.best_place(f, j, k, c, rule, box)
                        if inner is not None:
                            put(j, inner)
        return Layout(list(order), rule, k, placed)

    def best_place(self, f, i, k, c, rule, box=None, memo=None):
        """Island i's best place on the field by the rule, as (key, turn, x, y, drawing), or None: anywhere (with
        the maps of recent identical drawings in memo), or wholly inside box (x0, y0, x1, y1), in cells."""
        n = f.occ.shape[0]
        best = None
        for t, d in self.turns(i, k, c):
            if box is None:
                x0 = y0 = 0
                hit, touch = f.maps(d, rule == 'snug', memo)
            else:
                x0, y0 = max(box[0], 0), max(box[1], 0)
                x1, y1 = min(box[2], n) - d.w, min(box[3], n) - d.h
                if x1 < x0 or y1 < y0:
                    continue
                hit = Field(f.occ[y0:y1 + d.h, x0:x1 + d.w], self.meter).hits(d)
            if hit is None or hit.all():
                continue
            free = ~hit
            if rule == 'low':
                y = int(free.any(axis=1).argmax())
                x = int(free[y].argmax())
                key = (y0 + y + d.h, x0 + x, t)
            else:  # where its padding touches the most occupied cells
                m = d.m
                if box is None:
                    gx0 = gy0 = 0
                else:
                    gx0, gy0 = max(x0 - m, 0), max(y0 - m, 0)
                    gx1 = min(x0 + free.shape[1] - 1 + d.w + m, n)
                    gy1 = min(y0 + free.shape[0] - 1 + d.h + m, n)
                    touch = Field(f.occ[gy0:gy1, gx0:gx1], self.meter).hits(d, grown=True, count=True)
                score = np.zeros(free.shape, np.int64)
                if touch is not None:  # touch is indexed by the grown drawing's corner, m cells below and left
                    dy, dx = y0 - m - gy0, x0 - m - gx0
                    a0, b0 = max(-dy, 0), max(-dx, 0)
                    a1, b1 = min(score.shape[0], touch.shape[0] - dy), min(score.shape[1], touch.shape[1] - dx)
                    if a1 > a0 and b1 > b0:
                        score[a0:a1, b0:b1] = touch[a0 + dy:a1 + dy, b0 + dx:b1 + dx]
                score = np.where(free, score, -1)
                top = int(score.max())
                y, x = divmod(int(np.argmax(score == top)), free.shape[1])
                key = (-top, y0 + y + d.h, x0 + x, t)
            if best is None or key < best[0]:
                best = (key, t, x0 + x, y0 + y, d)
        return best

    def settle(self, base, k, c, keep=False):
        """The layout redrawn at ladder step k on the grid of cell size c and pushed toward the lower-left corner:
        every island starts where it was (scaled, or with `keep` exactly there, as for shrinking in place), then
        each in turn, nearest the corner first, moves to the free place nearest the corner within WINDOW cells,
        clear of all the others (or anywhere, when the window has none). None when one has no room."""
        n = self.size // c
        count = self.field(c).occ.astype(np.int16)
        grow_by = 1.0 if keep else self.scale(k) / self.scale(base.k)
        at = {}

        def mark(d, x, y, v):
            h, w = d.grown.shape
            x0, y0, x1, y1 = max(x - d.m, 0), max(y - d.m, 0), min(x - d.m + w, n), min(y - d.m + h, n)
            count[y0:y1, x0:x1] += v * d.grown[y0 - y + d.m:y1 - y + d.m, x0 - x + d.m:x1 - x + d.m]

        for i in base.order:
            t, px, py = base.placed[i]
            d = self.drawing(i, t, k, c)
            if d.w > n or d.h > n:
                return None
            x = min(max(int(round(px * grow_by / c)), 0), n - d.w)
            y = min(max(int(round(py * grow_by / c)), 0), n - d.h)
            at[i] = (t, d, x, y)
            mark(d, x, y, 1)
        for i in sorted(at, key=lambda i: (at[i][2] + at[i][3], at[i][3], i)):
            t, d, x, y = at[i]
            mark(d, x, y, -1)
            x0, x1 = max(x - WINDOW, 0), min(x + WINDOW, n - d.w)
            y0, y1 = max(y - WINDOW, 0), min(y + WINDOW, n - d.h)
            free = ~Field(count[y0:y1 + d.h, x0:x1 + d.w] > 0, self.meter).hits(d)
            if not free.any():
                x0 = y0 = 0
                free = ~Field(count > 0, self.meter).hits(d)
                if not free.any():
                    return None
            ys, xs = np.indices(free.shape)
            y, x = divmod(int(np.argmin(np.where(free, xs + ys, 1 << 30))), free.shape[1])
            x, y = x + x0, y + y0
            mark(d, x, y, 1)
            at[i] = (t, d, x, y)
        return Layout(base.order, base.rule, k, {i: (t, x * c, y * c) for i, (t, d, x, y) in at.items()})

    def run(self):
        """The layout at the largest scale found or, with fixed scale, at that scale (None when they don't fit it)."""
        n = len(self.isl)
        configs = [(sorted(range(n), key=lambda i, o=o: (-getattr(self.isl[i], o), i)), r) for o, r in CONFIGS]
        if self.want is None:
            return self.search(configs)
        if self.want > self.top:
            return None  # more island and padding area than texture
        k = math.log(self.top / self.want) / math.log(STEP)
        lay = self.attempt(*configs[0], k, self.c0)
        if lay is not None:
            return lay
        # The greedy can miss a scale it reaches just above, so find the largest and settle that down to this one.
        best = self.search(configs, enough=k)
        if best.k > k:
            return None
        lay = self.settle(best, k, 1, keep=True)
        for c in sorted({self.c0, max(self.c0 // 2, 1)}, reverse=True):
            for order, rule in configs:
                if lay is None:
                    lay = self.attempt(order, rule, k, c)
        return lay

    def search(self, configs, enough=None):
        """The layout at the largest scale found, or the first found at ladder step `enough` or below it (a scale
        at least that large)."""
        n = len(self.isl)
        c = self.c0
        top_k = int(math.ceil(math.log(1000) / math.log(STEP)))  # down to a thousandth of the bound

        def done(lay):
            return lay.k == 0 or (enough is not None and lay.k <= enough)

        def lowest(ok, failed=()):
            return self.descend(ok, lambda cur, k: self.attempt(cur.order, cur.rule, k, c),
                                lambda cur: self.meter.work < self.budget and not done(cur), failed)

        # The first order and rule from a guess (dense layouts reach about 85% of the area bound's scale), up the
        # ladder to a fit and down from there; then each other one from where it matches the best so far.
        k, step, missed = int(math.log(1 / 0.85) / math.log(STEP)), 4, []
        while True:
            best = self.attempt(*configs[0], k, c)
            if best is not None:
                break
            if k >= top_k:
                raise ValueError('the islands do not fit the texture even at a thousandth of their area bound')
            missed.append(k)
            k, step = min(k + step, top_k), step * 2
        best = lowest(best, missed)
        others = []  # the best layout of each other order and rule that matched the best so far
        for order, rule in configs[1:]:
            if done(best) or self.meter.work >= self.budget:
                break
            lay = self.attempt(order, rule, best.k, c)
            if lay is not None:
                lay = lowest(lay)
                if lay.k < best.k:
                    best, lay = lay, best
                others.append(lay)
        # Then reorderings of the winner, mostly among its first (biggest) islands, kept when one fits a step higher.
        rng = np.random.default_rng(1)
        for _ in range(10 * n if n > 1 else 0):
            if done(best) or self.meter.work >= self.budget:
                break
            i, j = sorted(int(n * rng.random() ** 2) for _ in range(2))
            if i == j:
                continue
            order = list(best.order)
            if rng.random() < 0.5:
                order[i], order[j] = order[j], order[i]
            else:
                order.insert(i, order.pop(j))
            lay = self.attempt(order, best.rule, best.k - 1, c)
            if lay is not None:
                best = lay

        # The greedy again on a grid twice as fine, which sees fits the coarse one missed, from the best layout and
        # the best of another order and rule: which one does better there is a toss-up.
        if c > 1 and not done(best):
            c //= 2
            spent, starts, fine = self.meter.work, [best] + sorted(others, key=lambda lay: lay.k)[:1], None
            for i, start in enumerate(starts):
                share = self.budget * (i + 1) / len(starts)
                lay = self.attempt(start.order, start.rule, start.k, c)
                if lay is not None:
                    lay = self.descend(lay, lambda cur, k: self.attempt(cur.order, cur.rule, k, c),
                                       lambda cur: self.meter.work - spent < share and not done(cur))
                    if fine is None or lay.k < fine.k:
                        fine = lay
                if fine is not None and done(fine):
                    break
            if fine is not None and fine.k <= best.k:
                best = fine
        if not done(best):  # whole pixels: pushed to the corner and grown into the room that frees, a step at a time
            step = 1.0      # and then halves of one
            for _ in range(3):
                lay = self.settle(best, best.k - step, 1) if best.k - step >= 0 else None
                if lay is not None:
                    best = lay
                else:
                    step /= 2
        return best

    def descend(self, ok, attempt, more, failed=()):
        """From a layout that fits, the lowest ladder step `attempt(layout, k)` reaches while `more(layout)`. The
        greedy is not monotonic in scale: near its limit fits and misses come mixed (a fit can follow three
        misses), so rather than bisect once this keeps sampling: the step down doubles after a fit and halves after
        a miss, and when one step down misses, two and three steps down are tried before giving up."""
        j, failed = 2, set(failed)
        while ok.k > 0 and more(ok):
            k = max(ok.k - j, 0)
            lay = None if k in failed else attempt(ok, k)
            if lay is None:
                failed.add(k)
                if j > 1:
                    j //= 2
                    continue
                for k in (ok.k - 2, ok.k - 3):
                    if k >= 0 and k not in failed and more(ok):
                        lay = attempt(ok, k)
                        if lay is not None:
                            break
                        failed.add(k)
                if lay is None:
                    break
                j = 1
            ok, j = lay, j * 2
        return ok

    def placements(self, lay):
        """Each island's UV transform: (2 x 2 matrix, offset) with uv @ matrix + offset its packed UV."""
        s = (self.want or self.scale(lay.k)) / self.size  # a fixed scale exactly, not as the ladder rounds it
        out = []
        for i, isl in enumerate(self.isl):
            t, x, y = lay.placed[i]
            mat, off, _ = isl.turns[t]
            out.append((mat * s, np.array([x, y]) / self.size - off * s))
        return out

    def overflow(self):
        """Every island at its own size in a row past U = 1, so in_unit_square is False."""
        out, x = [], self.size + self.pad
        for isl in self.isl:
            mat, off, t = isl.turns[0]
            out.append((mat, np.array([x / self.size, 0.0]) - off))
            x += t.reshape(-1, 2)[:, 0].max() * self.size + self.pad
        return out


def pack_islands(tris, pad, size, scale=True, border=None, budget=BUDGET):
    """Packs islands given as triangles in UV ((T, 3, 2) arrays) into 0..1 for a texture of `size` pixels, `pad`
    pixels apart and from the edge (`border`, default the padding): their UV transforms, (2 x 2 matrix, offset) with
    uv @ matrix + offset the packed UV. With scale False they keep their size, and when they don't fit they are put
    in a row past U = 1."""
    p = Packer(tris, pad, size, scale, pad if border is None else border, budget)
    lay = p.run()
    return p.overflow() if lay is None else p.placements(lay)
