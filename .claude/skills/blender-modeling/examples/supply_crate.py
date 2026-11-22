"""Supply crate: a bevelled wooden box with a handle slot cut through each end, metal bands round
it, a box collider and two LODs. Build: node scripts/mdl.mjs build examples/supply_crate.py --out <dir>/supply_crate.blend
"""
from modelkit import *

P = params(
    WIDTH=0.80, DEPTH=0.50, HEIGHT=0.45,      # outer size of the wooden body, metres
    BEVEL=6 * MM,                             # rounding on the body's edges
    SLOT_W=0.14, SLOT_H=0.035, SLOT_FROM_TOP=0.07,  # handle slots in the two ends
    BANDS=2, BAND_W=0.04, BAND_T=4 * MM,      # metal bands round the body, their width and thickness
    BAND_INSET=0.12,                          # band centre to the body's end
)

# The bands stand BAND_T proud of the body, so the body sits BAND_T up and the crate's lowest point
# (the bands) is on z = 0: the world origin is the crate's pivot in Unity.
body = box(P.WIDTH, P.DEPTH, P.HEIGHT, base='bottom')
bevel(body, P.BEVEL)
move(body, z=P.BAND_T)

# One slot cutter (a stadium shape) right through both ends, along X.
slot_z = P.BAND_T + P.HEIGHT - P.SLOT_FROM_TOP
r = P.SLOT_H / 2
cutter = join(box(P.WIDTH + 0.1, P.SLOT_W - P.SLOT_H, P.SLOT_H),
              move(cylinder(r, P.WIDTH + 0.1, axis='X'), y=(P.SLOT_W - P.SLOT_H) / 2),
              move(cylinder(r, P.WIDTH + 0.1, axis='X'), y=-(P.SLOT_W - P.SLOT_H) / 2))
move(cutter, z=slot_z)
body = boolean(body, cutter)
paint(body, 0)

# Bands: a thin rectangular ring at each inset, round the Y and Z sides.
ring = boolean(box(P.BAND_W, P.DEPTH + 2 * P.BAND_T, P.HEIGHT + 2 * P.BAND_T),
               box(P.BAND_W + 0.01, P.DEPTH - 0.002, P.HEIGHT - 0.002))
move(ring, z=P.BAND_T + P.HEIGHT / 2)
span = P.WIDTH - 2 * P.BAND_INSET
bands = array(ring, P.BANDS, x=span / max(P.BANDS - 1, 1))
move(bands, x=-span / 2 if P.BANDS > 1 else 0)
paint(bands, 1)

crate = part('SupplyCrate', join(body, bands), [material('Wood', (0.55, 0.38, 0.22), roughness=0.8),
                                                 material('Steel', (0.42, 0.44, 0.47), metallic=1, roughness=0.35)],
             uv_scale=0.5)
expect(crate, size=(P.WIDTH, P.DEPTH + 2 * P.BAND_T, P.HEIGHT + 2 * P.BAND_T))  # bands stand proud
collider(crate, 'box')
lods(crate, ratios=(0.5, 0.2))
