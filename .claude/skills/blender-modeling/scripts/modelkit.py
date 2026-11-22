"""Building blocks for model scripts run by `mdl.mjs build`.

Units: one Blender unit is one metre (Unity's unit). MM = 0.001, so a 20 mm part is 20 * MM.
Axes: Z is up and the model's front faces -Y; the Unity export turns that into Y up, front +Z.
Pivot: the world origin is the asset's pivot in Unity, so model it standing on z = 0 and centred
on X and Y (or with the origin at its mounting point, for a wall light or a door hinge).
Sizes are always full extents: box(1, 1, 1) is a 1 m cube (bpy's own cube primitive is 2 m).

Everything works on bmesh and bpy.data, never bpy.ops, so it runs headless with no UI context.

    from modelkit import *
    P = params(WIDTH=0.6, DEPTH=0.4, HEIGHT=0.45, BEVEL=4 * MM)   # overridable: --set WIDTH=0.8
    body = box(P.WIDTH, P.DEPTH, P.HEIGHT, base='bottom')
    bevel(body, P.BEVEL)
    crate = part('Crate', body, material('Wood', (0.55, 0.38, 0.22)))
    expect(crate, size=(P.WIDTH, P.DEPTH, P.HEIGHT))
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

MM = 0.001
_OVERRIDES = {}   # set by build.py from --set KEY=VALUE
_PARAMS = {}      # what params() resolved, written to the build report
_AXES = {'X': Vector((1, 0, 0)), 'Y': Vector((0, 1, 0)), 'Z': Vector((0, 0, 1))}

__all__ = ['MM', 'params', 'box', 'cylinder', 'cone', 'sphere', 'join', 'move', 'rotate', 'scale',
           'mirror', 'array', 'bevel', 'boolean', 'paint', 'material', 'texture', 'part', 'group', 'collider',
           'lods', 'expect']


class _Params(dict):
    __getattr__ = dict.__getitem__


def params(**defaults):
    """Every dimension, clearance and count as a named value. --set KEY=VALUE overrides one; the
    value keeps the default's type. Unknown keys in --set fail the build."""
    unknown = sorted(set(_OVERRIDES) - set(defaults))
    if unknown:
        raise ValueError(f'--set names unknown params {unknown}; this model has {sorted(defaults)}')
    out = _Params()
    for k, v in defaults.items():
        if k in _OVERRIDES:
            raw = _OVERRIDES[k]
            v = (raw.lower() in ('1', 'true', 'yes')) if isinstance(v, bool) else type(v)(raw)
        out[k] = v
    _PARAMS.update(out)
    return out


# ---------------------------------------------------------------- primitives (each returns a bmesh)


def _place(bm, base, height, axis='Z'):
    if base == 'bottom':
        bmesh.ops.translate(bm, verts=bm.verts, vec=_AXES[axis] * (height / 2))
    elif base != 'center':
        raise ValueError("base must be 'center' or 'bottom'")
    return bm


def box(x, y, z, base='center'):
    """A box x by y by z (full sizes), centred, or standing on z = 0 with base='bottom'."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(x, y, z), verts=bm.verts)
    return _place(bm, base, z)


def cone(r_bottom, r_top, depth, segments=32, axis='Z', base='center'):
    """A cone or frustum along axis; r_top=r_bottom makes a cylinder. Capped both ends."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments,
                          radius1=r_bottom, radius2=r_top, depth=depth)
    if axis != 'Z':
        rot = Matrix.Rotation(math.radians(90), 3, 'Y' if axis == 'X' else 'X')
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=rot)
    return _place(bm, base, depth, axis)


def cylinder(radius, depth, segments=32, axis='Z', base='center'):
    return cone(radius, radius, depth, segments, axis, base)


def sphere(radius, segments=32, rings=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=radius)
    return bm


# ---------------------------------------------------------------- transforms and combining


def move(bm, x=0.0, y=0.0, z=0.0):
    bmesh.ops.translate(bm, verts=bm.verts, vec=(x, y, z))
    return bm


def rotate(bm, degrees, axis='Z', center=(0, 0, 0)):
    bmesh.ops.rotate(bm, verts=bm.verts, cent=center, matrix=Matrix.Rotation(math.radians(degrees), 3, axis))
    return bm


def scale(bm, x=1.0, y=1.0, z=1.0, center=(0, 0, 0)):
    bmesh.ops.scale(bm, vec=(x, y, z), verts=bm.verts, space=Matrix.Translation(Vector(center)).inverted())
    if x * y * z < 0:  # a mirror turns faces inside out
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    return bm


def _copy_into(dst, src):
    me = bpy.data.meshes.new('_mdl_tmp')
    src.to_mesh(me)
    dst.from_mesh(me)
    bpy.data.meshes.remove(me)


def join(*bms):
    """One bmesh holding all the given ones (they are not changed)."""
    out = bmesh.new()
    for b in bms:
        _copy_into(out, b)
    return out


def mirror(bm, axis='X', merge=0.1 * MM):
    """Adds the mirror image across the plane through 0 on axis, welding verts on that plane."""
    bmesh.ops.mirror(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                     axis=axis, merge_dist=merge)
    return bm


def array(bm, count, x=0.0, y=0.0, z=0.0):
    """count copies, each offset by (x, y, z) from the one before. Returns a new bmesh."""
    out = bmesh.new()
    for i in range(count):
        c = bm.copy()
        move(c, x * i, y * i, z * i)
        _copy_into(out, c)
        c.free()
    return out


def bevel(bm, width, segments=2, angle=30.0):
    """Rounds every edge whose faces meet at more than `angle` degrees (the hard edges)."""
    edges = [e for e in bm.edges if len(e.link_faces) == 2 and math.degrees(e.calc_face_angle(0)) > angle]
    if edges:
        bmesh.ops.bevel(bm, geom=edges, offset=width, offset_type='OFFSET', segments=segments,
                        profile=0.5, affect='EDGES', clamp_overlap=True)
    return bm


def boolean(bm, cutter, op='DIFFERENCE', solver='EXACT'):
    """bm with cutter subtracted (DIFFERENCE), added (UNION) or intersected (INTERSECT).
    EXACT is the solver that copes with overlapping, non-manifold input; the others need clean
    manifold meshes. Returns a new bmesh; the inputs are not changed."""
    meshes, objs = [], []
    # Without real material slots the modifier puts every face on slot 0, undoing paint(). Empty slots
    # aren't enough, so both temp meshes get the same placeholder materials.
    count = 1 + max((f.material_index for b in (bm, cutter) for f in b.faces), default=0)
    slots = [bpy.data.materials.new('_mdl_slot') for _ in range(count)]
    for b in (bm, cutter):
        me = bpy.data.meshes.new('_mdl_bool')
        b.to_mesh(me)
        for slot in slots:
            me.materials.append(slot)
        ob = bpy.data.objects.new('_mdl_bool', me)
        bpy.context.scene.collection.objects.link(ob)
        meshes.append(me)
        objs.append(ob)
    mod = objs[0].modifiers.new('bool', 'BOOLEAN')
    mod.operation, mod.solver, mod.object = op, solver, objs[1]
    if solver == 'EXACT':
        # Pieces made with join() overlap; without this EXACT reads overlaps as outside and can
        # remove the whole target.
        mod.use_self = True
    dg = bpy.context.evaluated_depsgraph_get()
    ev = objs[0].evaluated_get(dg)
    out = bmesh.new()
    out.from_mesh(ev.to_mesh())
    ev.to_mesh_clear()
    for ob in objs:
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in meshes:
        bpy.data.meshes.remove(me)
    for slot in slots:
        bpy.data.materials.remove(slot)
    if not out.faces and op != 'INTERSECT':
        raise ValueError(f'boolean {op} left no faces; check the cutter overlaps the part only where intended')
    return out


def paint(bm, slot):
    """Gives every face material slot `slot` (0-based), for parts joined from pieces in different
    materials; pass part() a list of materials in slot order."""
    for f in bm.faces:
        f.material_index = slot
    return bm


# ---------------------------------------------------------------- objects


def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def material(name, color=(0.8, 0.8, 0.8), metallic=0.0, roughness=0.5):
    """A material slot. `color` is sRGB, as a colour picker or hex code gives it; Blender stores colours as
    linear, so it is converted, or a dark brown would show as pale tan. FBX carries the name and base colour;
    map it to a URP material in Unity."""
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        if hasattr(mat, 'use_nodes'):
            mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get('Principled BSDF') if mat.node_tree else None
        rgba = (*(_srgb_to_linear(c) for c in color[:3]), 1.0)
        if bsdf:
            bsdf.inputs['Base Color'].default_value = rgba
            bsdf.inputs['Metallic'].default_value = metallic
            bsdf.inputs['Roughness'].default_value = roughness
        mat.diffuse_color = rgba
        mat.metallic, mat.roughness = metallic, roughness
    return mat


def texture(name, maps, projection='box', scale=1.0):
    """A material from a texture set downloaded with `mdl.mjs polyhaven get` (its <slug>.json): base colour, normal,
    roughness and, when the set has one, metallic, sized so one repeat covers the set's real-world size times `scale`.

    projection 'box' maps the texture in object space from the three axes, blended at the corners, so it looks the
    same whatever the UVs are: in builds, previews and bakes, which replace the UVs. 'uv' maps it through the UV map
    instead, for tiling UVs from `mdl.mjs unwrap --mode tiling` (one UV unit a metre at the default uv_scale). FBX
    carries only the name and the texture's average colour; bake for Unity, or rebuild the material there from the
    maps."""
    import json
    import numpy as np
    with open(maps, encoding='utf-8') as f:
        meta = json.load(f)
    files = meta['maps']
    if 'base' not in files:
        raise ValueError(f'{maps} has no base colour map; download a texture set with mdl.mjs polyhaven get')
    mat = bpy.data.materials.get(name)
    if mat is not None:
        return mat
    mat = bpy.data.materials.new(name)
    if not mat.node_tree:
        mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get('Principled BSDF')
    coords = nodes.new('ShaderNodeTexCoord')
    mapping = nodes.new('ShaderNodeMapping')
    w, h = (s * scale for s in meta.get('size_m', (1.0, 1.0)))
    mapping.inputs['Scale'].default_value = (1 / w, 1 / h, 1 / w)
    links.new(coords.outputs['Object' if projection == 'box' else 'UV'], mapping.inputs['Vector'])

    def image(key, colour):
        node = nodes.new('ShaderNodeTexImage')
        node.image = bpy.data.images.load(files[key], check_existing=True)
        node.image.colorspace_settings.name = 'sRGB' if colour else 'Non-Color'
        if projection == 'box':
            node.projection, node.projection_blend = 'BOX', 0.2
        links.new(mapping.outputs['Vector'], node.inputs['Vector'])
        return node

    base = image('base', True)
    links.new(base.outputs['Color'], bsdf.inputs['Base Color'])
    nodes.active = base  # what Workbench's texture view shows
    if 'roughness' in files:
        links.new(image('roughness', False).outputs['Color'], bsdf.inputs['Roughness'])
    if 'metallic' in files:
        links.new(image('metallic', False).outputs['Color'], bsdf.inputs['Metallic'])
    if 'normal' in files:
        normal = nodes.new('ShaderNodeNormalMap')
        links.new(image('normal', False).outputs['Color'], normal.inputs['Color'])
        links.new(normal.outputs['Normal'], bsdf.inputs['Normal'])
    # The average colour, for what FBX and flat previews show.
    px = np.empty(len(base.image.pixels), dtype=np.float32)
    base.image.pixels.foreach_get(px)
    mean = px.reshape(-1, 4)[:, :3].mean(axis=0)
    mat.diffuse_color = (*(_srgb_to_linear(float(c)) for c in mean), 1.0)
    mat['mdl_texture'] = meta.get('slug', name)
    return mat


def _box_uv(bm, uv_scale):
    """World-size box mapping: each face projected along its main axis, uv_scale metres per UV unit,
    so a tiling texture has the same density on every part."""
    layer = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        u, v = [(1, 2), (0, 2), (0, 1)][ax]
        flip = -1 if n[ax] < 0 and ax != 2 else 1
        for loop in f.loops:
            co = loop.vert.co
            loop[layer].uv = (flip * co[u] / uv_scale, co[v] / uv_scale)


def _pivot_offset(bm, pivot):
    if not isinstance(pivot, str):
        return Vector(pivot)
    if pivot == 'origin':
        return Vector()
    xs, ys, zs = zip(*(v.co for v in bm.verts))
    c = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2))
    if pivot == 'center':
        return c
    if pivot == 'bottom':
        return Vector((c.x, c.y, min(zs)))
    raise ValueError("pivot must be 'origin', 'center', 'bottom' or an (x, y, z) point")


def part(name, bm, materials=None, parent=None, pivot='origin', smooth=30.0, uv_scale=1.0):
    """Turns a bmesh into an object, with its own origin at `pivot`: 'origin' keeps the world
    origin, 'bottom' is the centre of its base, 'center' its bounding-box centre, or an (x, y, z)
    point. The object stays where it was modelled. This sets the child GameObject's pivot inside a
    group() (a door that swings from its hinge); the asset's own pivot is always the world origin.
    smooth: faces meeting under this many
    degrees shade smooth (None for flat). UVs are box-mapped at uv_scale metres per UV unit."""
    if not bm.faces:
        raise ValueError(f"part('{name}') was given a mesh with no faces")
    off = _pivot_offset(bm, pivot)
    if off.length:
        bmesh.ops.translate(bm, verts=bm.verts, vec=-off)
    bm.normal_update()
    if uv_scale:
        _box_uv(bm, uv_scale)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if smooth is not None:
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(smooth))
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    mats = materials if isinstance(materials, (list, tuple)) else [materials] if materials else []
    for m in mats:
        me.materials.append(m)
    ob.location = off
    if parent is not None:
        _parent(ob, parent)
    return ob


def _parent(ob, parent):
    if parent is not None and parent.type == 'MESH':
        # The Unity FBX export bakes the axis change into each mesh; a mesh under another mesh
        # then imports turned 90 degrees. Hold parts together with group() instead.
        raise ValueError(f"'{ob.name}' cannot be parented to the mesh '{parent.name}'; put both under a group() empty")
    bpy.context.view_layer.update()  # matrix_world of objects made in this script is stale until then
    mw = ob.matrix_world.copy()
    ob.parent = parent
    ob.matrix_world = mw


def group(name, *children, location=(0, 0, 0)):
    """An empty that holds parts together (one GameObject with children in Unity)."""
    ob = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = location
    bpy.context.view_layer.update()
    for c in children:
        _parent(c, ob)
    return ob


def _hull(points):
    bm = bmesh.new()
    for p in points:
        bm.verts.new(p)
    res = bmesh.ops.convex_hull(bm, input=bm.verts)
    drop = set(res['geom_interior']) | set(res['geom_unused'])
    bmesh.ops.delete(bm, geom=[g for g in drop if isinstance(g, bmesh.types.BMVert)], context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


def _decimated_points(bm, ratio):
    """The verts left after collapsing bm to `ratio` of its faces (a hull of them stays convex)."""
    me = bpy.data.meshes.new('_mdl_hull')
    bm.to_mesh(me)
    tmp = bpy.data.objects.new('_mdl_hull', me)
    bpy.context.scene.collection.objects.link(tmp)
    mod = tmp.modifiers.new('decimate', 'DECIMATE')
    mod.ratio = max(min(ratio, 0.9), 0.05)
    ev = tmp.evaluated_get(bpy.context.evaluated_depsgraph_get())
    points = [v.co.copy() for v in ev.to_mesh().vertices]
    ev.to_mesh_clear()
    bpy.data.objects.remove(tmp, do_unlink=True)
    bpy.data.meshes.remove(me)
    return points


def collider(ob, kind='hull'):
    """A collision mesh named <part>_Collider beside the part (same parent): its convex hull ('hull') or its
    bounding box ('box'). A hull over 250 triangles is simplified until it fits Unity's 255-triangle
    convex limit. In Unity give it a MeshCollider (Convex) and remove its MeshRenderer."""
    src = bmesh.new()
    src.from_mesh(ob.data)
    if kind == 'box':
        xs, ys, zs = zip(*(v.co for v in src.verts))
        bm = box(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
        move(bm, (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, (max(zs) + min(zs)) / 2)
        src.free()
    elif kind == 'hull':
        points = [v.co.copy() for v in src.verts]
        src.free()
        bm = _hull(points)
        # Unity's convex MeshCollider takes at most 255 triangles: thin the hull's points until it fits.
        while sum(len(f.verts) - 2 for f in bm.faces) > 250 and len(bm.verts) > 8:
            points = _decimated_points(bm, 240 / sum(len(f.verts) - 2 for f in bm.faces))
            bm.free()
            bm = _hull(points)
    else:
        raise ValueError("kind must be 'hull' or 'box'")
    me = bpy.data.meshes.new(f'{ob.name}_Collider')
    bm.to_mesh(me)
    bm.free()
    col = bpy.data.objects.new(f'{ob.name}_Collider', me)
    bpy.context.scene.collection.objects.link(col)
    bpy.context.view_layer.update()
    col.matrix_world = ob.matrix_world.copy()
    if ob.parent is not None:
        _parent(col, ob.parent)
    col['mdl_collider'] = True
    col.display_type = 'WIRE'
    return col


def _drop_collapsed_pieces(me, source):
    """Decimating a closed mesh can flatten a tiny closed piece (a chip, a rivet) into a lone face, which
    leaves open edges. At that LOD's distance the piece is too small to see, so drop it rather than ship a
    hole. Only tiny pieces, under 3% of the model's size: an open edge on a large piece is a real defect for
    check to report, and dropping it would delete the model. Only when the source is closed: open pieces
    there are meant to be open."""
    src = bmesh.new()
    src.from_mesh(source)
    closed = all(e.is_manifold for e in src.edges)
    src.free()
    if not closed:
        return
    bm = bmesh.new()
    bm.from_mesh(me)
    if not bm.verts:
        bm.free()
        return
    model_size = _diagonal(bm.verts)
    seen, doomed = set(), []
    for start in bm.verts:
        if start in seen:
            continue
        island, stack = [], [start]
        seen.add(start)
        while stack:
            v = stack.pop()
            island.append(v)
            for e in v.link_edges:
                w = e.other_vert(v)
                if w not in seen:
                    seen.add(w)
                    stack.append(w)
        tiny = _diagonal(island) < TINY_PIECE * model_size
        if tiny and any(not e.is_manifold for v in island for e in v.link_edges):
            doomed.extend(island)
    if doomed:
        bmesh.ops.delete(bm, geom=doomed, context='VERTS')
        bm.to_mesh(me)
    bm.free()


TINY_PIECE = 0.03


def _diagonal(verts):
    lo = [min(v.co[i] for v in verts) for i in range(3)]
    hi = [max(v.co[i] for v in verts) for i in range(3)]
    return (Vector(hi) - Vector(lo)).length


def lods(ob, ratios=(0.5, 0.25)):
    """Level-of-detail copies for Unity's LOD Group: renames the part <name>_LOD0, adds
    <name>_LOD1.. decimated to each ratio of its triangles, and puts them under an empty named
    <name>, which Unity's FBX importer turns into an LOD Group. Returns the empty."""
    base = ob.name
    ob.name = ob.data.name = f'{base}_LOD0'
    bpy.context.view_layer.update()
    holder = group(base, location=ob.matrix_world.translation)
    _parent(ob, holder)
    col = bpy.data.objects.get(f'{base}_Collider')
    if col is not None and col.parent is None:
        _parent(col, holder)
    dg = bpy.context.evaluated_depsgraph_get()
    for i, r in enumerate(ratios, 1):
        tmp = bpy.data.objects.new('_mdl_lod', ob.data)
        bpy.context.scene.collection.objects.link(tmp)
        mod = tmp.modifiers.new('decimate', 'DECIMATE')
        mod.ratio = r
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
        bpy.data.objects.remove(tmp, do_unlink=True)
        _drop_collapsed_pieces(me, ob.data)
        me.name = f'{base}_LOD{i}'
        lod = bpy.data.objects.new(me.name, me)
        bpy.context.scene.collection.objects.link(lod)
        lod.matrix_world = ob.matrix_world.copy()
        _parent(lod, holder)
        lod['mdl_lod'] = i
    return holder


def expect(ob, size, tol=0.5 * MM):
    """Declares the part's world-space size (x, y, z in metres, children not included).
    `check` fails the model when the built part is off by more than tol."""
    ob['mdl_expect'] = list(size)
    ob['mdl_expect_tol'] = tol
    return ob
