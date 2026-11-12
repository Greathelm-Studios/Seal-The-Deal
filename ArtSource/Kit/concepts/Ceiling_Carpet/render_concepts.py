"""Piece 5 approval studies. Run with Blender, from any working directory.

Reuses the approved R3 window geometry, sky, city, figures and Cycles setup.
Coordinates are X=Unity X, Y=Unity Z, Z=Unity Y. Metres throughout.
Writes only below this file's directory. No production assets are generated.
"""
import argparse
import json
import math
import os
from pathlib import Path
import sys

import bpy

ROOT = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--direction', type=int, default=-1)
parser.add_argument('--shots', default='all')
parser.add_argument('--preview', action='store_true')
parser.add_argument('--force', action='store_true')
OPT = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])

# __file__ deliberately points here so the reused setup's cache remains here.
reference = ROOT.parent / 'Window_Bay_R3' / 'render_concepts.py'
original_argv = sys.argv
sys.argv = ['blender']
R = {'__file__': str(ROOT / 'render_concepts.py'), '__name__': 'approved_window_setup'}
exec(compile(reference.read_text(encoding='utf-8'), str(reference), 'exec'), R)
sys.argv = original_argv

DIRS = ['01_Ribbon_Lanes', '02_Porcelain_Squares']
COLOURS = [('737F90', '7E8999'), ('7C8898', '8591A0')]
CENTRE = (6.0, -8.0)
HOLE = 2.65
COLLAR = 2.90
META = {}
meta_path = ROOT / 'render_metadata.json'
if meta_path.exists():
    META.update(json.loads(meta_path.read_text(encoding='utf-8')))


def box(name, rect, bottom, top, material):
    x0, x1, y0, y1 = rect
    return R['box'](name, x0, x1, y0, y1, bottom, top, material)


def halfplane(poly, a, b, inside):
    """Clip a polygon against one edge of a CCW convex opening."""
    result = []
    for p, q in zip(poly, poly[1:] + poly[:1]):
        dp = (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])
        dq = (b[0] - a[0]) * (q[1] - a[1]) - (b[1] - a[1]) * (q[0] - a[0])
        pin = dp >= -1e-9 if inside else dp <= 1e-9
        qin = dq >= -1e-9 if inside else dq <= 1e-9
        if pin:
            result.append(p)
        if pin != qin:
            t = dp / (dp - dq)
            result.append((p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1])))
    return result


def circle(radius):
    # Matches the source stairwell's 48-segment opening.
    return [(CENTRE[0] + radius * math.cos(i * math.tau / 48),
             CENTRE[1] + radius * math.sin(i * math.tau / 48)) for i in range(48)]


def outside_hole(rect, radius):
    x0, x1, y0, y1 = rect
    pending = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    if radius is None:
        return [pending]
    # Partition the rectangle into disjoint pieces outside the convex opening.
    result = []
    opening = circle(radius)
    for a, b in zip(opening, opening[1:] + opening[:1]):
        if len(pending) < 3:
            break
        part = halfplane(pending, a, b, False)
        area = abs(sum(p[0] * q[1] - q[0] * p[1]
                       for p, q in zip(part, part[1:] + part[:1]))) if part else 0
        if area > 1e-9:
            result.append(part)
        pending = halfplane(pending, a, b, True)
    return result


def plate(name, rect, bottom, top, material, radius=None):
    pieces = outside_hole(rect, radius)
    verts = []
    faces = []
    for poly in pieces:
        n = len(poly)
        offset = len(verts)
        if top == bottom:
            verts.extend((x, y, top) for x, y in poly)
            faces.append(tuple(range(offset, offset + n)))
        else:
            verts.extend((x, y, z) for z in (bottom, top) for x, y in poly)
            faces.extend([tuple(offset + i for i in range(n - 1, -1, -1)),
                          tuple(offset + n + i for i in range(n))])
            faces.extend((offset + i, offset + (i + 1) % n,
                          offset + (i + 1) % n + n, offset + i + n) for i in range(n))
    if faces:
        return R['mesh'](name, verts, faces, material)


def ring(name, inner, outer, bottom, top, material):
    a = circle(inner)
    b = circle(outer)
    verts = [(x, y, z) for z in (bottom, top) for poly in (a, b) for x, y in poly]
    faces = []
    for i in range(48):
        j = (i + 1) % 48
        faces.extend([(i, j, j + 48, i + 48),
                      (i + 96, i + 144, j + 144, j + 96),
                      (i, i + 96, j + 96, j),
                      (i + 48, j + 48, j + 144, i + 144)])
    return R['mesh'](name, verts, faces, material)


def setup():
    R['setup']()
    scene = bpy.context.scene
    scene.cycles.samples = 20 if OPT.preview else 64
    scene.render.resolution_percentage = 55 if OPT.preview else 100
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 1120
    scene.view_settings.look = 'AgX - Medium High Contrast'
    scene.view_settings.exposure = 0
    global WHITE, NAVY, SILVER, LIGHT, TILE, JOINT, CARPETS
    WHITE = R['WHITE']
    NAVY = R['NAVY']
    SILVER = R['SILVER']
    LIGHT = R['LIGHT']
    TILE = R['TILE']
    JOINT = R['mat']('Ceiling_Tile printed grid shadow', 'A5ADB6', rough=1)
    CARPETS = {}


def carpet_material(h, turn=0):
    key = (h, turn)
    if key in CARPETS:
        return CARPETS[key]
    material = R['mat']('Floor_Carpet_' + h + '_pile_' + str(turn), h, rough=1, spec=.03)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    coords = nodes.new('ShaderNodeTexCoord')
    wave = nodes.new('ShaderNodeTexWave')
    wave.wave_type = 'BANDS'
    wave.bands_direction = 'X' if turn == 0 else 'Y'
    wave.inputs['Scale'].default_value = 380
    links.new(coords.outputs['Object'], wave.inputs['Vector'])
    bump = nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = .13
    bump.inputs['Distance'].default_value = .0007
    links.new(wave.outputs['Color'], bump.inputs['Height'])
    links.new(bump.outputs['Normal'], nodes.get('Principled BSDF').inputs['Normal'])
    CARPETS[key] = material
    return material


def carpet(d, bounds, floor=0, hole=False, current=False):
    x0, x1, y0, y1 = bounds
    module = .5 if d == 0 else 1.0
    count_x = round((x1 - x0) / module)
    count_y = round((y1 - y0) / module)
    for i in range(count_x):
        for j in range(count_y):
            # Absolute integer grid indices keep the repeat aligned at 2 m seams.
            ix = round(x0 / module) + i
            iy = round(y0 / module) + j
            variant = int(ix % 4 == 0) if d == 0 else (ix + iy) % 2
            colour = '8A94A3' if current else COLOURS[d][variant]
            rect = (x0 + i * module, x0 + (i + 1) * module,
                    y0 + j * module, y0 + (j + 1) * module)
            plate('Flat carpet tile at authored floor level', rect, floor, floor,
                  carpet_material(colour, variant), HOLE if hole else None)


def overlaps_circle(rect, radius):
    x0, x1, y0, y1 = rect
    near_x = max(x0, min(CENTRE[0], x1))
    near_y = max(y0, min(CENTRE[1], y1))
    return (near_x - CENTRE[0]) ** 2 + (near_y - CENTRE[1]) ** 2 < radius ** 2


def ceiling(d, bounds, soffit=4.3, hole=False, head=True):
    x0, x1, y0, y1 = bounds
    radius = COLLAR if hole else None
    # Structural cap above a 50 mm face zone; all visible detailing is recessed.
    plate('Slab core to exact upper floor', bounds, soffit + .05, soffit + .2, WHITE,
          HOLE if hole else None)
    plate('Recess backing plane 50mm inside slab', bounds, soffit + .05, soffit + .05,
          WHITE, HOLE if hole else None)
    end_y = min(y1, -.25) if head else y1
    active = (x0, x1, y0, end_y)
    if head and y1 == 0:
        plate('250mm Paper White window head closure', (x0, x1, -.25, 0),
              soffit, soffit + .05, WHITE, radius)

    def clipped(rect):
        a, b, c, e = rect
        result = (max(a, active[0]), min(b, active[1]), max(c, active[2]), min(e, active[3]))
        return result if result[1] > result[0] and result[3] > result[2] else None

    def component(name, rect, bottom, top, material, full_lens=False):
        r = clipped(rect)
        if r is None or (full_lens and hole and overlaps_circle(r, COLLAR + .05)):
            return
        plate(name, r, bottom, top, material, radius)

    for cx in range(math.floor(x0 / 2) * 2, math.ceil(x1 / 2) * 2, 2):
        for cy in range(math.floor(y0 / 2) * 2, math.ceil(y1 / 2) * 2, 2):
            if d == 0:
                # Two-metre lane repeat. 0.5 x 1 m panel faces either side.
                for a, b in [(0, .5), (.5, .875), (1.125, 1.5), (1.5, 2)]:
                    for c in [0, 1]:
                        component('White linear ceiling tile',
                                  (cx + a + .004, cx + b - .004, cy + c + .004, cy + c + .996),
                                  soffit, soffit + .05, TILE)
                component('250mm navy lane bed recessed 45mm',
                          (cx + .875, cx + 1.125, cy, cy + 2),
                          soffit + .045, soffit + .05, NAVY)
                for a, b in [(.875, .89), (1.11, 1.125)]:
                    component('Navy vertical lane cheek', (cx + a, cx + b, cy, cy + 2),
                              soffit, soffit + .05, NAVY)
                for c in [0, 1]:
                    component('Light_Strip 120mm x 900mm recessed lens',
                              (cx + .94, cx + 1.06, cy + c + .05, cy + c + .95),
                              soffit + .023, soffit + .028, LIGHT, True)
            else:
                # Broad 2 m square cassettes, with a 200 mm perimeter track.
                outer = [(0, 2, 0, .1), (0, 2, 1.9, 2), (0, .1, .1, 1.9), (1.9, 2, .1, 1.9)]
                for a, b, c, e in outer:
                    component('Porcelain outer square rim', (cx + a, cx + b, cy + c, cy + e),
                              soffit, soffit + .05, TILE)
                for a, b in [(.3, .996), (1.004, 1.7)]:
                    for c, e in [(.3, .996), (1.004, 1.7)]:
                        component('Square centre white tile', (cx + a, cx + b, cy + c, cy + e),
                                  soffit, soffit + .05, TILE)
                tracks = [(.1, 1.9, .1, .3), (.1, 1.9, 1.7, 1.9),
                          (.1, .3, .3, 1.7), (1.7, 1.9, .3, 1.7)]
                lenses = [(.15, 1.85, .15, .25), (.15, 1.85, 1.75, 1.85),
                          (.15, .25, .25, 1.75), (1.75, 1.85, .25, 1.75)]
                for a, b, c, e in tracks:
                    component('Silver square track at 45mm recess', (cx + a, cx + b, cy + c, cy + e),
                              soffit + .045, soffit + .05, SILVER)
                # Omit a whole frame around the opening, avoiding clipped fragments.
                complete = not hole or not overlaps_circle((cx + .1, cx + 1.9, cy + .1, cy + 1.9), COLLAR + .05)
                if complete:
                    for a, b, c, e in lenses:
                        component('Light_Strip square frame 100mm lens', (cx + a, cx + b, cy + c, cy + e),
                                  soffit + .03, soffit + .035, LIGHT)
    # Tile-grid backing is exposed in the 8 mm surface gaps.
    # This is a material assignment in production, without a hanging T-grid.
    plate('Quiet grid backing', bounds, soffit + .049, soffit + .049, JOINT,
          HOLE if hole else None)
    if hole:
        ring('250mm Paper White opening collar / no change to radius', HOLE, COLLAR,
             soffit, soffit + .2, WHITE)
    # The perimeter remains white for the full 200 mm edge.
    for rect in [(x0, x1, y1 - .008, y1), (x0, x1, y0, y0 + .008),
                 (x0, x0 + .008, y0, y1), (x1 - .008, x1, y0, y1)]:
        plate('Flat white slab edge', rect, soffit, soffit + .2, WHITE,
              HOLE if hole else None)


def floor(d, bounds, level, hole=False, current=False):
    plate('Exact 200mm lower slab / paper white edge', bounds, level - .2, level, WHITE,
          HOLE if hole else None)
    carpet(d, bounds, level, hole, current)


def windows(level=0):
    for x in (0, 4, 8):
        R['bay'](x, 4, level, 0, 'B')
    for x in (0, 4, 8, 12):
        R['column'](x, level, 0, 'L' if x == 0 else ('R' if x == 12 else None))


def room(d, stair=False, current=False, upper=False):
    bounds = (0, 12, -12, 0)
    floor(d, bounds, 0, stair, current)
    ceiling(d, bounds, 4.3, stair)
    if stair:
        carpet(d, bounds, 4.5, True, current)
    windows()
    for rect in [(0, .2, -12, -.2), (11.8, 12, -12, -.2), (0, 12, -12.2, -12)]:
        box('Paper White context wall', rect, 0, 4.3, WHITE)
    R['area']('White back wall wash', (6, -9.8, 3.3), (6, -12, 2), 600, 8)
    # Fixture illumination is fixed across both carpet values.
    for x in (2, 6, 10):
        R['area']('Broad strip bounce / fixed comparison lighting', (x, -4, 4.1), (x, -4, 0), 100, 3)
        R['area']('Broad strip bounce / fixed comparison lighting', (x, -9, 4.1), (x, -9, 0), 100, 3)
    if upper:
        windows(4.5)
        for rect in [(0, .2, -12, -.2), (11.8, 12, -12, -.2), (0, 12, -12.2, -12)]:
            box('Upper floor context wall', rect, 4.5, 8.8, WHITE)
    R['city']()


def furniture():
    for x, y in [(2.1, -2.2), (9.6, -2.2), (2.1, -6.3), (9.6, -6.3)]:
        box('Context white desk', (x - .8, x + .8, y - .4, y + .4), .71, .76, WHITE)
        for a in (-.66, .66):
            box('Context silver desk leg', (x + a - .025, x + a + .025, y - .3, y + .3), 0, .71, SILVER)
        box('Context quiet navy monitor', (x - .27, x + .27, y + .12, y + .16), .9, 1.23, NAVY)


def stair_context():
    # Source envelope only. Flight is intentionally plain, keeping piece 6 undecided.
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=.8, depth=4.5,
                                      location=(*CENTRE, 2.25))
    bpy.context.object.name = 'Context stair column / source radius 800mm'
    bpy.context.object.data.materials.append(SILVER)
    vertices = []
    for i in range(49):
        theta = i * math.tau / 48
        height = i * 4.5 / 48
        for z in (height - .25, height):
            for radius in (.8, 2.6):
                vertices.append((CENTRE[0] + radius * math.sin(theta),
                                 CENTRE[1] + radius * math.cos(theta), z))
    faces = []
    for i in range(48):
        a = i * 4
        b = a + 4
        faces.extend([(a + 2, a + 3, b + 3, b + 2), (a, b, b + 1, a + 1),
                      (a + 1, b + 1, b + 3, a + 3), (a, a + 2, b + 2, b)])
    R['mesh']('Plain source ramp / stair styling deferred', vertices, faces, SILVER)


def camera(pos, target, lens=24, ortho=None):
    R['camera'](pos, target, lens=lens, ortho=ortho)


def shot(d, name, notes):
    folder = ROOT / DIRS[d]
    folder.mkdir(exist_ok=True)
    scene = bpy.context.scene
    scene.render.filepath = str(folder / (name + '.png'))
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)
    cam = scene.camera
    direction = cam.rotation_euler.to_quaternion() @ R['Vector']((0, 0, -1))
    META[DIRS[d] + '/' + name] = {
        'camera': list(cam.location), 'direction': list(direction),
        'lens_mm': cam.data.lens, 'ortho_scale_m': cam.data.ortho_scale if cam.data.type == 'ORTHO' else None,
        'size_px': [int(scene.render.resolution_x * scene.render.resolution_percentage / 100),
                    int(scene.render.resolution_y * scene.render.resolution_percentage / 100)],
        'notes': notes,
    }
    meta_path.write_text(json.dumps(META, indent=2), encoding='utf-8')
    print('CEILING_FINISHED', DIRS[d], name, flush=True)


def wanted(d, name):
    if OPT.shots != 'all' and name not in OPT.shots.split(','):
        return False
    return OPT.force or not (ROOT / DIRS[d] / (name + '.png')).exists()


def execute():
    for d in (range(2) if OPT.direction < 0 else [OPT.direction]):
        if wanted(d, 'room'):
            setup()
            room(d)
            furniture()
            R['figure'](7.4, -2)
            camera((2.7, -11.0, 1.7), (7, -.3, 2.1), lens=19)
            shot(d, 'room', '12m deep office / 3 x 4m bays / eye 1.70m / approved White Trays B')
        if wanted(d, 'stair_up'):
            setup()
            room(d, stair=True, upper=True)
            stair_context()
            R['figure'](8.8, -5, 4.5)
            camera((3.2, -11.1, 1.7), (6.3, -6.8, 5.1), lens=19)
            shot(d, 'stair_up', '250mm white collar at r2.65 hole / 4.30m soffit / source ramp context')
        for name, current in [('down_current', True), ('down_proposal', False)]:
            if wanted(d, name):
                setup()
                room(d, stair=True, current=current, upper=True)
                stair_context()
                R['figure'](4.8, -4.7)
                R['figure'](7.2, -4.7)
                camera((6, -11.2, 6.2), (6, -5, 0), lens=25)
                shot(d, name, 'Exact 45 degree depression / eye 1.70m above upper floor / identical lighting and navy suits')
        if wanted(d, 'ceiling_plan'):
            setup()
            ceiling(d, (0, 4, -4, 0), 4.3, head=False)
            camera((2, -2, -8), (2, -2, 4.3), ortho=5.35)
            bpy.context.scene.camera.rotation_euler.z = 0
            R['area']('Plan face soft fill', (2, -2, 1), (2, -2, 4.3), 220, 5)
            bpy.context.scene.render.resolution_x = 1200
            bpy.context.scene.render.resolution_y = 1200
            shot(d, 'ceiling_plan', '4x4m true reflected ceiling plan / source for dimensioned bay_plan.png')
        if wanted(d, 'carpet_plan'):
            setup()
            floor(d, (0, 4, -4, 0), 0)
            camera((2, -2, 10), (2, -2, 0), ortho=5.35)
            bpy.context.scene.render.resolution_x = 1200
            bpy.context.scene.render.resolution_y = 1200
            shot(d, 'carpet_plan', '4x4m true floor plan / flat colour and directional pile only')
        if wanted(d, 'head_section'):
            setup()
            # A genuine facade slice, with floor 0 and upper floor 4.5.
            for z in (0, 4.5):
                R['bay'](0, 4, z, 0, 'B')
            for obj in list(bpy.context.scene.objects):
                if obj.type == 'MESH':
                    for vertex in obj.data.vertices:
                        vertex.co.x = 2.025 if vertex.co.x > 2 else 1.975
                    obj.data.update()
            box('Section 200mm white slab edge', (1.975, 2.025, -2, 0), 4.3, 4.5, WHITE)
            # Exact exposed fitting profile, displaced inward from the head.
            box('250mm white head closure', (1.94, 2.06, -.25, 0), 4.3, 4.5, WHITE)
            box('Recessed lens profile', (1.94, 2.06, -1.8, -.3), 4.323 if d == 0 else 4.33, 4.335, LIGHT)
            R['figure'](2, -1.25)
            camera((15, -.7, 3.15), (2, -.7, 3.15), ortho=7.9)
            R['area']('Section wall fill', (7, -3, 6), (2, 0, 3), 400, 5)
            bpy.context.scene.render.resolution_x = 1200
            bpy.context.scene.render.resolution_y = 1200
            shot(d, 'head_section', 'Facade section / street right / white slab edge 0.20m / floor0, soffit4.30, floor4.50')


if __name__ == '__main__':
    execute()
