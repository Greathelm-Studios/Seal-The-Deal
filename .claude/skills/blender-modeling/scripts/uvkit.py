"""UV unwrapping for game assets, shared by unwrap.py and bake.py. Run inside Blender.

Atlas unwrap (unique, non-overlapping UVs, as an artist would cut them): seams on hard edges and material borders,
each seam region cut open into a disc, an angle-based unwrap, gentler seams for islands that come out stretched,
every island at one texel density, axis-aligned packing with a gap sized for the texture. Also: a fixed texel
density in pixels per metre, a second non-overlapping UV map for Unity lightmaps, world-scale tiling UVs, and
trim-sheet bands. See references/uv-unwrapping.md.
"""
import heapq
import math

import bmesh
import bpy
import numpy as np  # ships with Blender
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

import modelkit
import uvpack

SEAM_ANGLES = (70, 50, 35, 25) # degrees: seams first on edges sharper than the first, then gentler for stretched islands
STRETCH_OK = 0.9         # an island is stretched when less of its area than this is within 20% of its own density
UV_MAP = 'UVMap'         # the texture UVs, UV0 in Unity
LIGHTMAP = 'Lightmap'    # the lightmap UVs, UV1 (Mesh.uv2) in Unity
LIGHTMAP_SIZE = 1024     # the lightmap size the Lightmap UVs' margin is measured against
LIGHTMAP_GAP = 4 / LIGHTMAP_SIZE  # Unity's Generate Lightmap UVs default: a 4 px pack margin


def gutter_px(size):
    """Pixels between islands for a texture of `size`: Polycount's padding table (4 px at 512, 8 at 1024, 16 at
    2048, 32 at 4096), so mipmaps don't bleed neighbouring islands into each other."""
    return max(2, round(size / 128))


def select_only(obs):
    for ob in bpy.context.scene.objects:
        ob.select_set(ob in obs)
    bpy.context.view_layer.objects.active = obs[0]


def uv_islands(bm, lay):
    """Faces grouped by UV connectivity (an edge joins two faces when both its ends match in UV)."""
    seen, out = set(), []
    for f in bm.faces:
        if f in seen:
            continue
        isl, stack = [], [f]
        seen.add(f)
        while stack:
            g = stack.pop()
            isl.append(g)
            for lp in g.loops:
                a, b = lp[lay].uv, lp.link_loop_next[lay].uv
                for other in lp.edge.link_loops:
                    h = other.face
                    if h in seen:
                        continue
                    c, d = other[lay].uv, other.link_loop_next[lay].uv
                    if (((a - d).length < 1e-6 and (b - c).length < 1e-6)
                            or ((a - c).length < 1e-6 and (b - d).length < 1e-6)):
                        seen.add(h)
                        stack.append(h)
        out.append(isl)
    return out


def split_long_islands(ob, max_aspect=6.0):
    """Cut UV islands longer than max_aspect times their width (a blade side, a barrel) into pieces along their
    length, between faces, so the packer can fit them; a long strip otherwise caps the scale of the whole atlas."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    lay = bm.loops.layers.uv.active
    for isl in uv_islands(bm, lay):
        uvs = [lp[lay].uv for f in isl for lp in f.loops]
        lo = [min(u[i] for u in uvs) for i in (0, 1)]
        ext = [max(u[i] for u in uvs) - lo[i] for i in (0, 1)]
        ax = 0 if ext[0] >= ext[1] else 1
        length, width = ext[ax], max(ext[1 - ax], 1e-9)
        pieces = min(int(length / width / 2), 8)
        if length / width < max_aspect or pieces < 2:
            continue
        for f in isl:
            c = sum(lp[lay].uv[ax] for lp in f.loops) / len(f.loops)
            k = min(int((c - lo[ax]) / length * pieces), pieces - 1)
            for lp in f.loops:  # moving a piece sideways cuts it off; pack_islands then places it
                lp[lay].uv[1 - ax] += k * (width + 0.01)
    bm.to_mesh(ob.data)
    bm.free()


def uv_overlaps(obs, res=1024):
    """Pairs of faces whose UV triangles cover the same pixel centres at res x res: {(obj, face), ...} to detach
    (the smaller face of each pair), and the number of shared pixels."""
    owner = np.full((res, res), -1, np.int64)
    detach, shared = set(), 0
    for oi, ob in enumerate(obs):
        me = ob.data
        me.calc_loop_triangles()
        uv = np.empty(len(me.loops) * 2)
        me.uv_layers.active.data.foreach_get('uv', uv)
        uv = uv.reshape(-1, 2) * res
        for t in me.loop_triangles:
            (ax, ay), (bx, by), (cx, cy) = p = uv[list(t.loops)]
            x0, y0 = np.maximum(np.floor(p.min(0)).astype(int), 0)
            x1, y1 = np.minimum(np.ceil(p.max(0)).astype(int), res)
            d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if x1 <= x0 or y1 <= y0 or abs(d) < 1e-12:
                continue
            xs, ys = np.meshgrid(np.arange(x0, x1) + 0.5, np.arange(y0, y1) + 0.5)
            l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d
            l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d
            inside = (l1 > 1e-6) & (l2 > 1e-6) & (1 - l1 - l2 > 1e-6)
            yy, xx = ys[inside].astype(int), xs[inside].astype(int)
            me_id = oi * 10_000_000 + t.polygon_index
            prev = owner[yy, xx]
            hit = (prev >= 0) & (prev != me_id)
            shared += int(hit.sum())
            for q in set(prev[hit].tolist()):
                qo, qf = divmod(q, 10_000_000)
                mine = (oi, t.polygon_index)
                small = (qo, qf) if obs[qo].data.polygons[qf].area < me.polygons[t.polygon_index].area else mine
                detach.add(small)
            owner[yy, xx] = me_id
    return detach, shared


def seam_regions(bm):
    """Face sets joined across edges that are not seams."""
    seen, out = set(), []
    for f in bm.faces:
        if f in seen:
            continue
        reg, stack = [], [f]
        seen.add(f)
        while stack:
            g = stack.pop()
            reg.append(g)
            for e in g.edges:
                if not e.seam:
                    for h in e.link_faces:
                        if h not in seen:
                            seen.add(h)
                            stack.append(h)
        out.append(reg)
    return out


def border_loops(faces, edges):
    """A region's border edges (seams or open edges), and their vertices grouped into connected loops."""
    border = {e for e in edges if e.seam or sum(1 for f in e.link_faces if f in faces) < 2}
    adj = {}
    for e in border:
        a, b = e.verts
        adj.setdefault(a, set()).add(b)
        adj.setdefault(b, set()).add(a)
    loops, seen = [], set()
    for v in adj:
        if v not in seen:
            loop, stack = set(), [v]
            seen.add(v)
            while stack:
                u = stack.pop()
                loop.add(u)
                for w in adj[u] - seen:
                    seen.add(w)
                    stack.append(w)
            loops.append(loop)
    return border, loops


def shortest_path(sources, targets, edges):
    """Edges of the shortest path through `edges` from any source vertex to any target vertex."""
    dist, prev = {v: 0.0 for v in sources}, {}
    heap = [(0.0, i, v) for i, v in enumerate(sources)]
    heapq.heapify(heap)
    n = len(heap)
    while heap:
        d, _, u = heapq.heappop(heap)
        if d > dist[u]:
            continue
        if u in targets and u not in sources:
            path = []
            while u in prev:
                path.append(prev[u])
                u = prev[u].other_vert(u)
            return path
        for e in u.link_edges:
            if e in edges:
                w, nd = e.other_vert(u), d + e.calc_length()
                if nd < dist.get(w, math.inf):
                    dist[w], prev[w] = nd, e
                    n += 1
                    heapq.heappush(heap, (nd, n, w))
    return []


def open_regions(bm):
    """Cuts each seam region open into a disc, as an artist would before unwrapping: a seam joining its border
    loops (a tube, or a plate with holes), or across it when it has no border (a sphere). A region with handles,
    which one cut cannot open, is left as it is; its stretch sends it to the next, finer pass."""
    for faces in map(set, seam_regions(bm)):
        verts = {v for f in faces for v in f.verts}
        edges = {e for f in faces for e in f.edges}
        border, loops = border_loops(faces, edges)
        if len(verts) - len(edges) + len(faces) + len(loops) < 2:  # Euler characteristic: it has handles
            continue
        inside = edges - border
        if not loops:
            a = next(iter(verts))
            loops = [{a}]
            far = max(verts, key=lambda v: (v.co - a.co).length)
            for e in shortest_path([a], {far}, inside):
                e.seam = True
        for loop in loops[1:]:
            for e in shortest_path(list(loops[0]), loop, inside):
                e.seam = True


def stretched_faces(bm, lay):
    """Faces of islands where under STRETCH_OK of the area is within 20% of the island's own texel density."""
    out = set()
    for isl in uv_islands(bm, lay):
        dens = []
        for f in isl:
            a3 = f.calc_area()
            pts = [lp[lay].uv for lp in f.loops]
            a2 = abs(sum(p.x * q.y - q.x * p.y for p, q in zip(pts, pts[1:] + pts[:1]))) / 2
            dens.append((math.sqrt(a2 / a3) if a3 > 1e-12 else 0.0, a3))
        dens.sort()
        total, acc, median = sum(a for _, a in dens) or 1e-12, 0.0, dens[-1][0]
        for d, a in dens:
            acc += a
            if acc >= total / 2:
                median = d
                break
        even = sum(a for d, a in dens if median > 0 and 0.8 * median <= d <= 1.25 * median)
        if even < STRETCH_OK * total:
            out.update(isl)
    return out


def seam_unwrap(ob, gap):
    """Seams on hard edges and material borders, regions cut open into discs, then an angle-based unwrap. Islands
    that come out stretched get seams at the next, gentler angle and are unwrapped again; any still stretched
    after the last pass fall back to Smart UV Project. Bevel strips stay on the faces they round off, so the
    worn edges have no seam running along them, and there are far fewer, larger islands than Smart UV Project
    alone gives."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    redo = set(bm.faces)
    for angle in SEAM_ANGLES:
        for e in {e for f in redo for e in f.edges}:
            lf = e.link_faces
            if len(lf) != 2 or lf[0].material_index != lf[1].material_index or e.calc_face_angle(0) > math.radians(angle):
                e.seam = True
        open_regions(bm)
        chosen = {f.index for f in redo}
        bm.to_mesh(ob.data)
        bm.free()
        for p in ob.data.polygons:
            p.select = p.index in chosen
        select_only([ob])
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.uv.unwrap(method='ANGLE_BASED', fill_holes=True, correct_aspect=True, margin=gap)
        bpy.ops.object.mode_set(mode='OBJECT')
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        redo = stretched_faces(bm, bm.loops.layers.uv.active)
        if not redo:
            break
    chosen = {f.index for f in redo}
    bm.free()
    if chosen:
        for p in ob.data.polygons:
            p.select = p.index in chosen
        select_only([ob])
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=gap, area_weight=0.0, scale_to_bounds=False)
        bpy.ops.object.mode_set(mode='OBJECT')
    for p in ob.data.polygons:
        p.select = True


def even_island_scale(ob):
    """Scales every UV island about its centre so its median texel density is the same (Blender's Average
    Islands Scale left about 6% of the Stormseye's surface in islands at the wrong scale)."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    lay = bm.loops.layers.uv.active
    for isl in uv_islands(bm, lay):
        dens = []
        for f in isl:
            a3 = f.calc_area()
            pts = [lp[lay].uv for lp in f.loops]
            a2 = abs(sum(p.x * q.y - q.x * p.y for p, q in zip(pts, pts[1:] + pts[:1]))) / 2
            if a3 > 1e-12 and a2 > 0:
                dens.append((math.sqrt(a2 / a3), a3))
        if not dens:
            continue
        dens.sort()
        total, acc = sum(a for _, a in dens), 0.0
        for median, a in dens:
            acc += a
            if acc >= total / 2:
                break
        loops = [lp for f in isl for lp in f.loops]
        cu = sum(lp[lay].uv.x for lp in loops) / len(loops)
        cv = sum(lp[lay].uv.y for lp in loops) / len(loops)
        for lp in loops:
            uv = lp[lay].uv
            lp[lay].uv = (cu + (uv.x - cu) / median, cv + (uv.y - cv) / median)
    bm.to_mesh(ob.data)
    bm.free()


def pack(obs, gap, scale=True, shape='AABB', size=None):
    """Packs every island of the active UV maps into 0..1, each turned to lie along U or V. With scale False the
    islands keep their size, so a fixed texel density holds; they may then run past 1 (see in_unit_square).
    shape 'AABB' packs bounding boxes, at once, with `gap` round each island (so twice it between two); 'EXACT'
    packs each island's own shape at the pixels of a texture of `size` (uvpack), nesting islands into each other's
    gaps and holes with `gap` between them and at the edge, in a few seconds."""
    if shape == 'EXACT':
        if size is None:
            raise ValueError("shape 'EXACT' packs at the texture's pixels: pass its size")
        uvpack.pack(obs, gap, size, scale)
        return
    select_only(obs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    # Each island turned to lie along U or V: straight strips stay on the pixel grid and pack tighter.
    bpy.ops.uv.pack_islands(margin_method='FRACTION', margin=gap, shape_method='AABB', rotate=True,
                            rotate_method='AXIS_ALIGNED', scale=scale)
    bpy.ops.object.mode_set(mode='OBJECT')


def use_map(obs, name, replace=False):
    """Makes `name` the active UV map of every object, creating it after the existing ones (or as the only one with
    replace), so UV0 stays first, as Unity reads the maps by order."""
    for ob in obs:
        me = ob.data
        if replace:
            while me.uv_layers:
                me.uv_layers.remove(me.uv_layers[0])
        layer = me.uv_layers.get(name) or me.uv_layers.new(name=name)
        me.uv_layers.active = layer


def scale_uvs(obs, k):
    for ob in obs:
        uv = ob.data.uv_layers.active.data
        co = np.empty(len(uv) * 2)
        uv.foreach_get('uv', co)
        uv.foreach_set('uv', co * k)


def in_unit_square(obs, eps=1e-4):
    for ob in obs:
        uv = ob.data.uv_layers.active.data
        if not len(uv):
            continue
        co = np.empty(len(uv) * 2)
        uv.foreach_get('uv', co)
        if co.min() < -eps or co.max() > 1 + eps:
            return False
    return True


def uv_area(obs):
    area = 0.0
    for ob in obs:
        uv = ob.data.uv_layers.active.data
        for p in ob.data.polygons:
            pts = [uv[i].uv for i in p.loop_indices]
            area += abs(sum(a.x * b.y - b.x * a.y for a, b in zip(pts, pts[1:] + pts[:1]))) / 2
    return area


def unwrap(obs, gap, name=UV_MAP, uv_per_m=None, replace=True, shape='AABB', size=None):
    """One non-overlapping UV atlas over all the objects, in the UV map `name` (their only one with replace).
    gap is the space between islands as a fraction of the texture. With uv_per_m (UV units per metre: pixels per
    metre / texture size) every island gets that texel density; when the islands then don't fit the texture, they
    are scaled down to fit and `fits` is False. shape and size are pack()'s.

    Returns {'used': UV area used, 'shared': pixels (of 1024 x 1024) still shared by two faces after a few rounds
    of detaching them (normally 0), 'fits': whether the density held}."""
    use_map(obs, name, replace=replace)
    bpy.context.scene.tool_settings.use_uv_select_sync = False
    for ob in obs:
        seam_unwrap(ob, gap)
        even_island_scale(ob)  # every island now at 1 UV unit per metre
        split_long_islands(ob)
    fixed = uv_per_m is not None
    if fixed:
        scale_uvs(obs, uv_per_m)
    pack(obs, gap, scale=not fixed, shape=shape, size=size)
    fits = in_unit_square(obs)
    if not fits:
        pack(obs, gap, scale=True, shape=shape, size=size)
    # An unwrap can fold a few faces of one island over each other; give each such face its own island.
    for attempt in range(4):
        detach, shared = uv_overlaps(obs)
        if not detach or attempt == 3:
            break
        for oi, fi in detach:
            uv = obs[oi].data.uv_layers.active.data
            for li in obs[oi].data.polygons[fi].loop_indices:
                uv[li].uv = (uv[li].uv[0] + 2.0, uv[li].uv[1])
        pack(obs, gap, scale=not (fixed and fits), shape=shape, size=size)
        if fixed and fits and not in_unit_square(obs):
            fits = False
            pack(obs, gap, scale=True, shape=shape, size=size)
    return {'used': uv_area(obs), 'shared': shared, 'fits': fits}


def separate_folds(obs, gap, shape, size=None):
    """Gives each face that an unwrap folded over another its own island and repacks to fill 0..1, up to three
    times. Returns the pixels (of 1024 x 1024) still shared by two faces, normally 0."""
    for attempt in range(4):
        detach, shared = uv_overlaps(obs)
        if not detach or attempt == 3:
            return shared
        for oi, fi in detach:
            uv = obs[oi].data.uv_layers.active.data
            for li in obs[oi].data.polygons[fi].loop_indices:
                uv[li].uv = (uv[li].uv[0] + 2.0, uv[li].uv[1])
        pack(obs, gap, shape=shape, size=size)
    return shared


def lightmap(obs, gap=LIGHTMAP_GAP, shape='AABB'):
    """A second UV map, LIGHTMAP, for Unity's baked lighting (UV1): the texture islands repacked to fill 0..1 with
    Unity's margin, so it never overlaps even when the texture UVs do (tiling or trim). Returns the pixels (of
    1024 x 1024) shared by two faces, normally 0. Leaves UV0 the active and render map."""
    first = {ob: ob.data.uv_layers[0].name for ob in obs}
    for ob in obs:
        me = ob.data
        old = me.uv_layers.get(LIGHTMAP)
        if old is not None:
            me.uv_layers.remove(old)
        src = me.uv_layers[0]
        layer = me.uv_layers.new(name=LIGHTMAP)
        co = np.empty(len(src.data) * 2)
        src.data.foreach_get('uv', co)
        layer.data.foreach_set('uv', co)
        me.uv_layers.active = layer
    # Overlapping texture UVs (tiling, trim, stacked copies) can't be repacked as they are: unwrap afresh.
    detach, _ = uv_overlaps(obs)
    if detach or not in_unit_square(obs):
        shared = unwrap(obs, gap, name=LIGHTMAP, replace=False, shape=shape, size=LIGHTMAP_SIZE)['shared']
    else:
        pack(obs, gap, shape=shape, size=LIGHTMAP_SIZE)
        # Repacking scales the islands up, so a fold too small to cover a pixel at the texture's scale can show now.
        shared = separate_folds(obs, gap, shape, size=LIGHTMAP_SIZE)
    for ob in obs:
        me = ob.data
        me.uv_layers.active = me.uv_layers[first[ob]]
        me.uv_layers[first[ob]].active_render = True
    return shared


def tiling(obs, uv_per_m):
    """World-scale UVs for tiling textures: each face projected along its main axis, uv_per_m UV units per metre,
    so one repeat of the texture covers size / density metres. Overlaps and UVs past 1 are intended."""
    use_map(obs, UV_MAP, replace=True)
    for ob in obs:
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        modelkit._box_uv(bm, 1.0 / uv_per_m)
        bm.to_mesh(ob.data)
        bm.free()


def trim(obs, bands, uv_per_m, gap):
    """Trim-sheet UVs: the faces of each material in `bands` ({material: (v0, v1)}) are unwrapped into strips at
    uv_per_m, laid along U and stacked in that material's V band, running past 1 in U where they are long (the trim
    tiles along U). A strip taller than its band is scaled down to fit it. Faces of other materials get tiling UVs.
    Returns {material: number of strips scaled down to fit}."""
    tiling(obs, uv_per_m)
    squeezed = {m: 0 for m in bands}
    for ob in obs:
        mats = [m.name if m else None for m in ob.data.materials]
        chosen = {p.index for p in ob.data.polygons if p.material_index < len(mats) and mats[p.material_index] in bands}
        if not chosen:
            continue
        for p in ob.data.polygons:
            p.select = p.index in chosen
        select_only([ob])
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.uv.unwrap(method='ANGLE_BASED', fill_holes=True, margin=gap)
        bpy.ops.object.mode_set(mode='OBJECT')
        for p in ob.data.polygons:
            p.select = True
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        lay = bm.loops.layers.uv.active
        cursor = {m: 0.0 for m in bands}
        for isl in uv_islands(bm, lay):
            mat = mats[isl[0].material_index] if isl[0].material_index < len(mats) else None
            if isl[0].index not in chosen or mat not in bands:
                continue
            loops = [lp for f in isl for lp in f.loops]
            # The strip's own density (UV units per metre), so k below brings it to uv_per_m. Only the trim strips
            # are scaled: the other materials' tiling UVs are already at world scale.
            a2 = sum(abs(sum(p.x * q.y - q.x * p.y for p, q in zip(pts, pts[1:] + pts[:1]))) / 2
                     for pts in ([lp[lay].uv for lp in f.loops] for f in isl))
            a3 = sum(f.calc_area() for f in isl)
            own = math.sqrt(a2 / a3) if a2 > 0 and a3 > 0 else 1.0
            us = [lp[lay].uv.x for lp in loops]
            vs = [lp[lay].uv.y for lp in loops]
            turn = (max(vs) - min(vs)) > (max(us) - min(us))  # lay the strip's long side along U
            for lp in loops:
                u, v = lp[lay].uv
                lp[lay].uv = (v, -u) if turn else (u, v)
            us = [lp[lay].uv.x for lp in loops]
            vs = [lp[lay].uv.y for lp in loops]
            v0, v1 = bands[mat]
            k = uv_per_m / own
            if (max(vs) - min(vs)) * k > (v1 - v0):
                k = (v1 - v0) / (max(vs) - min(vs))
                squeezed[mat] += 1
            u0, vmin = min(us), min(vs)
            for lp in loops:
                u, v = lp[lay].uv
                lp[lay].uv = (cursor[mat] + (u - u0) * k, v0 + (v - vmin) * k)
            cursor[mat] += (max(us) - u0) * k + gap
        bm.to_mesh(ob.data)
        bm.free()
    return squeezed


def parse_bands(text):
    """'Wood=0.5:0.75,Metal=0:0.25' -> {'Wood': (0.5, 0.75), 'Metal': (0.0, 0.25)}."""
    bands = {}
    for item in filter(None, (s.strip() for s in text.split(','))):
        name, _, rng = item.partition('=')
        v0, _, v1 = rng.partition(':')
        try:
            lo, hi = float(v0), float(v1)
        except ValueError:
            raise ValueError(f"trim band '{item}' should look like Material=v0:v1, such as Wood=0.5:0.75")
        if not 0 <= lo < hi <= 1:
            raise ValueError(f"trim band '{item}': need 0 <= v0 < v1 <= 1")
        bands[name.strip()] = (lo, hi)
    return bands


def uv_stats(obs):
    """Island count, the share of the surface whose texel density is within 20% of the median, and that median
    in UV units per metre (times the texture size for pixels per metre)."""
    islands, area3, dens = 0, [], []
    for ob in obs:
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        lay = bm.loops.layers.uv.active
        islands += len(uv_islands(bm, lay))
        for tri in bm.calc_loop_triangles():
            a, b, c = (lp.vert.co for lp in tri)
            p, q, r = (lp[lay].uv for lp in tri)
            a3 = (b - a).cross(c - a).length / 2
            if a3 > 1e-12:
                area3.append(a3)
                dens.append(math.sqrt(abs((q - p).cross(r - p)) / 2 / a3))
        bm.free()
    w, d = np.array(area3), np.array(dens)
    order = np.argsort(d)
    cum = np.cumsum(w[order])
    median = d[order][np.searchsorted(cum, cum[-1] / 2)]
    even = w[(d >= 0.8 * median) & (d <= 1.25 * median)].sum() / w.sum()
    return islands, float(even), float(median)


def reproject_uvs(me, lod0):
    """Give each face of a decimated LOD UVs from one of LOD0's UV islands: the island nearest the face's centre,
    continued past its border along the nearest triangle's plane. Decimation that collapses across a UV seam
    otherwise leaves faces with corners on two islands, squeezed or smeared over the gap between them."""
    src = bmesh.new()
    src.from_mesh(lod0.data)
    bmesh.ops.triangulate(src, faces=src.faces[:])
    lay = src.loops.layers.uv.active
    src.faces.index_update()
    island_of, members = {}, []
    for i, isl in enumerate(uv_islands(src, lay)):
        members.append(isl)
        for f in isl:
            island_of[f.index] = i
    whole = BVHTree.FromBMesh(src)
    trees = {}

    def island_tree(i):
        if i not in trees:
            faces = members[i]
            co = [v.co.copy() for f in faces for v in f.verts]
            trees[i] = (BVHTree.FromPolygons(co, [(3 * k, 3 * k + 1, 3 * k + 2) for k in range(len(faces))]), faces)
        return trees[i]

    bm = bmesh.new()
    bm.from_mesh(me)
    out = bm.loops.layers.uv.active
    for f in bm.faces:
        idx = whole.find_nearest(f.calc_center_median())[2]
        tree, faces = island_tree(island_of[idx])
        for lp in f.loops:
            j = tree.find_nearest(lp.vert.co)[2]
            t = faces[j]
            uv = [(*t.loops[k][lay].uv, 0.0) for k in range(3)]
            p = barycentric_transform(lp.vert.co, *(v.co for v in t.verts), *uv)
            lp[out].uv = (p.x, p.y)
    bm.to_mesh(me)
    bm.free()
    src.free()


def reproject_all(me, lod0):
    """reproject_uvs for each of LOD0's UV maps, so a LOD gets the same maps in the same order (UV0 first, then
    any Lightmap)."""
    names = [layer.name for layer in lod0.data.uv_layers]
    active = lod0.data.uv_layers.active.name
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    for name in names:
        me.uv_layers.new(name=name)
    for name in names:
        lod0.data.uv_layers.active = lod0.data.uv_layers[name]
        me.uv_layers.active = me.uv_layers[name]
        reproject_uvs(me, lod0)
    lod0.data.uv_layers.active = lod0.data.uv_layers[active]
    me.uv_layers.active = me.uv_layers[active]
    me.uv_layers[names[0]].active_render = True
