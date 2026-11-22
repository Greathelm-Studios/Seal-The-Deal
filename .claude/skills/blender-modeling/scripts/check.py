"""Measure a model instead of looking at it: the defects a render cannot show.

blender -b --factory-startup -P check.py -- --blend model.blend [--max-tris N] [--watertight] [--json out.json]

Per mesh object, on the evaluated mesh in world space:
  errors    flipped faces (inconsistent winding), inside-out closed meshes, zero-area faces, loose
            verts and edges, missing UVs, negative scale, a size off its expect(), and for
            *_Collider meshes: open edges or more than 255 triangles (Unity's convex limit)
  warnings  open (non-manifold) edges, which are fine on game meshes such as a plane or a flat
            sign but are errors with --watertight; faces that cut through each other in the same
            object; parts with no material; more than 5% sliver triangles (aspect over 20 or an angle
            under 3 degrees)
For a model unwrapped by unwrap or bake (its build report has a `uv` entry), over the visible meshes:
  errors    an atlas whose faces overlap or that leaves 0..1; a Lightmap map that is missing, overlaps, leaves 0..1
            or is not the second UV map (Unity reads it as UV1 by its order)
  warnings  an atlas with under 80% of its surface within 20% of the median texel density, or whose density is
            off the --density it was asked for (it did not fit the texture size)
Exit code 1 when anything is an error, so a build script can stop on it.
"""
import json
import math
import os
import sys

sys.dont_write_bytecode = True  # keep the skill folder free of __pycache__
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh  # noqa: E402
import bpy  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
import uvkit  # noqa: E402
from mdl_common import mesh_objects, open_blend, parse, round3, tri_count, world_bounds, write_json  # noqa: E402

DENSITY_EVEN = 0.8       # warn when less of an atlas's surface than this is within 20% of the median texel density
DENSITY_TOLERANCE = 0.05 # and when its density is this far off the one asked for

AREA_MIN = 1e-8          # m²: a face smaller than 0.1 mm x 0.1 mm is degenerate
UNITY_CONVEX_TRIS = 255  # Unity's convex MeshCollider triangle limit
SLIVER_ASPECT = 20       # a triangle whose longest edge is over this many times its height to it is a sliver
SLIVER_SIN = math.sin(math.radians(3))  # and so is one whose smallest angle is under 3 degrees
SLIVER_WARN = 0.05       # warn when more than this share of a mesh's triangles are slivers


def census(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    bm = bmesh.new()
    bm.from_mesh(ev.to_mesh())
    ev.to_mesh_clear()
    bm.transform(ob.matrix_world)
    if ob.matrix_world.determinant() < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)  # report what the engine will show
    bm.normal_update()
    open_edges = sum(1 for e in bm.edges if e.link_faces and not e.is_manifold)
    flipped = sum(1 for e in bm.edges if e.is_manifold and not e.is_contiguous)
    loose_v = sum(1 for v in bm.verts if not v.link_faces)
    loose_e = sum(1 for e in bm.edges if not e.link_faces)
    degenerate = sum(1 for f in bm.faces if f.calc_area() <= AREA_MIN)
    slivers = 0
    for tri in bm.calc_loop_triangles():
        a, b, c = (lp.vert.co for lp in tri)
        area = (b - a).cross(c - a).length / 2
        if area <= AREA_MIN:
            continue  # counted as zero-area
        e = sorted(((b - a).length, (c - b).length, (a - c).length))
        if e[2] ** 2 / (2 * area) > SLIVER_ASPECT or 2 * area / (e[1] * e[2]) < SLIVER_SIN:
            slivers += 1
    closed = open_edges == 0 and len(bm.faces) > 0
    volume = bm.calc_volume(signed=True) if closed else None
    hits = 0
    if bm.faces:
        tree = BVHTree.FromBMesh(bm, epsilon=0.0)
        bm.faces.ensure_lookup_table()
        for a, b in tree.overlap(tree):
            if a < b and not set(bm.faces[a].verts) & set(bm.faces[b].verts):
                hits += 1
    bm.free()
    return {'open_edges': open_edges, 'flipped_edges': flipped, 'loose_verts': loose_v, 'loose_edges': loose_e,
            'degenerate_faces': degenerate, 'sliver_tris': slivers, 'closed': closed,
            'volume_m3': round(volume, 9) if volume is not None else None, 'self_intersections': hits}


def check_uvs(blend, obs):
    """UV errors, warnings and a summary line for a model whose build report has a `uv` entry (unwrap or bake)."""
    path = os.path.splitext(os.path.abspath(blend))[0] + '.build.json'
    entry = {}
    if os.path.isfile(path):
        with open(path, encoding='utf-8') as f:
            entry = json.load(f).get('uv') or {}
    if not entry or not obs:
        return [], [], None
    errors, warnings, notes = [], [], [f"mode {entry['mode']}"]

    def use(name):
        missing = [ob.name for ob in obs if name not in ob.data.uv_layers]
        for ob in obs:
            if name in ob.data.uv_layers:
                ob.data.uv_layers.active = ob.data.uv_layers[name]
        return missing

    first = obs[0].data.uv_layers[0].name if obs[0].data.uv_layers else None
    if entry['mode'] == 'atlas' and first:
        use(first)
        _, shared = uvkit.uv_overlaps(obs)
        if shared:
            errors.append(f'{first}: {shared} pixels (of 1024 x 1024) shared by two faces; an atlas must not overlap')
        if not uvkit.in_unit_square(obs):
            errors.append(f'{first}: UVs outside 0..1; an atlas must stay inside the texture')
        islands, even, median = uvkit.uv_stats(obs)
        size = entry.get('size') or 1024
        notes.append(f'{islands} islands, {uvkit.uv_area(obs) * 100:.0f}% of the texture used, '
                     f'{median * size:.0f} px/m at {size} px')
        if even < DENSITY_EVEN:
            warnings.append(f'{first}: only {even:.0%} of the surface within 20% of the median texel density')
        want = entry.get('density_target')
        if want and abs(median * size - want) > DENSITY_TOLERANCE * want:
            warnings.append(f'{first}: {median * size:.0f} px/m, not the {want:g} asked for; at {want:g} px/m the '
                            f'islands did not fit a {size} px texture')
    if entry.get('lightmap'):
        missing = use(uvkit.LIGHTMAP)
        if missing:
            errors.append(f"no {uvkit.LIGHTMAP} UV map on {', '.join(missing)}")
        else:
            wrong = [ob.name for ob in obs if len(ob.data.uv_layers) < 2 or ob.data.uv_layers[1].name != uvkit.LIGHTMAP]
            if wrong:
                errors.append(f"{uvkit.LIGHTMAP} is not the second UV map on {', '.join(wrong)}; Unity reads the "
                              'second map as the lightmap UVs (UV1)')
            _, shared = uvkit.uv_overlaps(obs)
            if shared:
                errors.append(f'{uvkit.LIGHTMAP}: {shared} pixels (of 1024 x 1024) shared by two faces; lightmap UVs '
                              'must not overlap')
            if not uvkit.in_unit_square(obs):
                errors.append(f'{uvkit.LIGHTMAP}: UVs outside 0..1')
            notes.append(f'{uvkit.LIGHTMAP} as UV1')
    for ob in obs:
        if ob.data.uv_layers:
            ob.data.uv_layers.active = ob.data.uv_layers[0]
    return errors, warnings, ', '.join(notes)


def pivot_name(ob, lo, hi):
    p = ob.matrix_world.translation
    c = (lo + hi) / 2
    tol = max((hi - lo).length * 0.01, 0.0005)
    if abs(p.x - c.x) < tol and abs(p.y - c.y) < tol:
        if abs(p.z - lo.z) < tol:
            return 'bottom centre'
        if abs(p.z - c.z) < tol:
            return 'centre'
    return f'({p.x:.3f}, {p.y:.3f}, {p.z:.3f}) m'


def main():
    o = parse({'blend': None, 'max_tris': None, 'json': None}, switches=('watertight',), required=('blend',))
    o.setdefault('watertight', False)
    open_blend(o['blend'])
    results, total_tris, n_err = [], 0, 0
    for ob in sorted(mesh_objects(), key=lambda x: x.name):
        is_col = bool(ob.get('mdl_collider')) or ob.name.endswith('_Collider')
        c = census(ob)
        lo, hi = world_bounds([ob])
        size = hi - lo
        tris = tri_count(ob.data)
        if not is_col and not ob.get('mdl_lod'):
            total_tris += tris
        errors, warnings = [], []
        if c['flipped_edges']:
            errors.append(f"{c['flipped_edges']} edges between faces facing opposite ways (flipped faces)")
        if c['volume_m3'] is not None and c['volume_m3'] < 0:
            errors.append('closed mesh is inside out (normals point inwards)')
        if c['degenerate_faces']:
            errors.append(f"{c['degenerate_faces']} zero-area faces")
        if c['loose_verts'] or c['loose_edges']:
            errors.append(f"{c['loose_verts']} loose verts, {c['loose_edges']} loose edges")
        if not ob.data.uv_layers and not is_col:
            errors.append('no UV map')
        if any(s < 0 for s in ob.matrix_world.to_scale()) or ob.matrix_world.determinant() < 0:
            errors.append('negative scale (mirrored object); apply it in the mesh instead')
        if ob.parent is not None and ob.parent.type == 'MESH':
            errors.append(f"parented to the mesh '{ob.parent.name}'; Unity imports it turned 90 degrees. "
                          'Put both under a group() empty')
        if ob.get('mdl_lod'):
            lod0 = bpy.data.objects.get(ob.name.rsplit('_LOD', 1)[0] + '_LOD0')
            if lod0 is not None:
                l0, h0 = world_bounds([lod0])
                grow = max(abs(a - b) for a, b in zip(size, h0 - l0))
                if grow > 0.01 * max(h0 - l0):
                    warnings.append(f'{grow * 1000:.1f} mm off the size of {lod0.name}; it may pop when the LOD switches')
        if 'mdl_expect' in ob:
            want, tol = list(ob['mdl_expect']), ob.get('mdl_expect_tol', 0.0005)
            off = [abs(a - b) for a, b in zip(size, want)]
            if max(off) > tol:
                errors.append(f'size {round3(size)} m, expected {round3(want)} m (tolerance {tol * 1000:.2f} mm)')
        if is_col:
            if c['open_edges']:
                errors.append(f"collider has {c['open_edges']} open edges; colliders must be closed")
            if tris > UNITY_CONVEX_TRIS:
                errors.append(f'collider has {tris} triangles; Unity convex colliders allow {UNITY_CONVEX_TRIS} '
                              f"(use collider(part, 'box') or a simpler shape)")
        elif c['open_edges']:
            (errors if o['watertight'] else warnings).append(f"{c['open_edges']} open (non-manifold) edges")
        if c['self_intersections']:
            warnings.append(f"{c['self_intersections']} face pairs cut through each other "
                            '(fine for overlapping kit pieces; wrong inside one solid)')
        if not is_col and tris and c['sliver_tris'] > SLIVER_WARN * tris:
            warnings.append(f"{c['sliver_tris']} sliver triangles ({c['sliver_tris'] / tris:.0%}; above "
                            f'{SLIVER_WARN:.0%} they shade and decimate badly). Long thin bevel strips count too')
        if not is_col and not any(ob.data.materials):
            warnings.append('no material')
        n_err += len(errors)
        r = {'name': ob.name, 'collider': is_col, 'lod': ob.get('mdl_lod', 0), 'tris': tris, 'size_m': round3(size),
             'pivot': pivot_name(ob, lo, hi), 'census': c, 'errors': errors, 'warnings': warnings}
        results.append(r)
        status = 'FAIL' if errors else ('warn' if warnings else 'ok')
        kind = ' (collider)' if is_col else f" (LOD{r['lod']})" if r['lod'] else ''
        print(f"{status:4} {ob.name}{kind}: {size.x:.4f} x {size.y:.4f} x {size.z:.4f} m, {tris} tris, pivot {r['pivot']}")
        for e in errors:
            print(f'       error: {e}')
        for w in warnings:
            print(f'       warning: {w}')
    shown = [ob for ob in mesh_objects() if not ob.get('mdl_lod') and not ob.get('mdl_collider')]
    uv_errors, uv_warnings, uv_note = check_uvs(o['blend'], [ob for ob in shown if not ob.name.endswith('_Collider')])
    if uv_note:
        print(f"{'FAIL' if uv_errors else 'warn' if uv_warnings else 'ok':4} UVs: {uv_note}")
        for e in uv_errors:
            print(f'       error: {e}')
        for w in uv_warnings:
            print(f'       warning: {w}')
        n_err += len(uv_errors)
    if shown:
        lo, hi = world_bounds(shown)
        print(f'model bounds from the origin (the Unity pivot): x {lo.x:+.4f}..{hi.x:+.4f}, '
              f'y {lo.y:+.4f}..{hi.y:+.4f}, z {lo.z:+.4f}..{hi.z:+.4f} m'
              + ('' if abs(lo.z) < 0.0005 else '  (not standing on z = 0)'))
    if o['max_tris'] and total_tris > int(o['max_tris']):
        n_err += 1
        print(f"FAIL total: {total_tris} triangles over the budget of {o['max_tris']} (colliders and LOD1+ not counted)")
    else:
        print(f"total: {total_tris} triangles (colliders and LOD1+ not counted)")
    if o['json']:
        write_json(o['json'], {'blend': os.path.abspath(o['blend']), 'total_tris': total_tris, 'errors': n_err,
                               'objects': results,
                               'uv': {'summary': uv_note, 'errors': uv_errors, 'warnings': uv_warnings} if uv_note else None})
    print('CHECK FAILED: ' + f'{n_err} error(s)' if n_err else 'CHECK PASSED')
    sys.exit(1 if n_err else 0)


main()
