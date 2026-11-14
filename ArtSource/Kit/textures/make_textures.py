# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-12
#  Prompt    : Alex approved the "White Trays + Cobalt Portal" office concepts; make the kit to them
#  Modified  : Claude Opus 5.5, 2026-11-13 — adds Carpet_Dark (the reference plates' dark work-area
#              carpet) and Ceiling_Grid (the tile grid, now a separate material over work areas).
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Draws the two tiling textures the approved concepts lean on: the cheap ceiling-tile grid
#              and quarter-turned carpet tiles. They are drawn by script, not painted, so the tile size
#              stays tied to the kit grid and a value change is one rebuild. Modelling the grid as
#              geometry was the alternative; it would multiply every slab's triangles for a flat line.
# -----------------------------------------------------------------------------
"""Usage: python ArtSource/Kit/textures/make_textures.py

Writes Ceiling_Tile.png and Floor_Carpet.png beside this script. Each covers 1 m x 1 m, which is one UV unit
on every kit piece (box-mapped at 1 m per unit), so they tile seamlessly across slabs placed on the grid.
Colours are the final albedo: in Unity the material's base colour stays white so the texture is not tinted.
"""
import os
import random

from PIL import Image, ImageDraw

PX_PER_M = 512          # texels per metre. WHY: ~1 texel per 2 mm, crisp seams at first-person ceiling distance
TILE = 0.5              # tile module, metres. WHY: divides the 2 m infill and 4 m bay evenly (brief §5)
CEILING = (0xF3, 0xF4, 0xF4)   # Paper White, bible §03
CEILING_SEAM = (0xD3, 0xD8, 0xDE)   # grid tee colour: a cool light grey, so the grid reads without going busy
CEILING_SEAM_PX = 6     # seam width, pixels (12 mm): the width of a cheap exposed-tee ceiling grid
CARPET = (0x64, 0x6E, 0x86)    # old Floor_Carpet value (kit_pieces.py before the polished floor)
CARPET_DARK = (0x7E, 0x96, 0xAC)   # Carpet_Dark (kit_pieces.py): paler blue work-area carpet (Japanese_Aero/DECISION.md)
CARPET_SEAM = 0.88      # seam brightness relative to the carpet. WHY: just visible, so tiles read as tiles
CARPET_STRIPE = 0.035   # stripe contrast, fraction of the base value. WHY: the bible asks for low-contrast carpet
CARPET_STRIPE_PX = 10   # stripe pitch, pixels (20 mm): a fine loop-pile line at walking distance
CARPET_TILE_SHIFT = 0.02   # per-tile brightness shift, fraction. WHY: quarter-turned tiles catch light differently
SEED = 7                # fixed so a rebuild draws the same texture

HERE = os.path.dirname(os.path.abspath(__file__))
SIZE = PX_PER_M
N = round(1.0 / TILE)   # tiles per texture edge
T = SIZE // N           # tile size, pixels


def shade(c, k):
    return tuple(max(0, min(255, round(v * k))) for v in c)


def ceiling(name='Ceiling_Tile.png'):
    im = Image.new('RGB', (SIZE, SIZE), CEILING)
    d = ImageDraw.Draw(im)
    h = CEILING_SEAM_PX // 2
    # WHY half a seam on each edge: the seam lands on the tile boundary and wraps whole when the texture repeats.
    for k in range(N + 1):
        p = k * T
        d.rectangle([p - h, 0, p + h - 1, SIZE], fill=CEILING_SEAM)
        d.rectangle([0, p - h, SIZE, p + h - 1], fill=CEILING_SEAM)
    im.save(os.path.join(HERE, name))


def carpet(colour=CARPET, name='Floor_Carpet.png'):
    rng = random.Random(SEED)
    im = Image.new('RGB', (SIZE, SIZE), colour)
    px = im.load()
    for ty in range(N):
        for tx in range(N):
            vertical = (tx + ty) % 2 == 0          # quarter-turn laying: neighbours' stripes run across each other
            base = 1.0 + rng.uniform(-CARPET_TILE_SHIFT, CARPET_TILE_SHIFT)
            for y in range(T):
                for x in range(T):
                    s = x if vertical else y
                    k = base + (CARPET_STRIPE if (s // (CARPET_STRIPE_PX // 2)) % 2 else -CARPET_STRIPE) * 0.5
                    if x == 0 or y == 0:
                        k *= CARPET_SEAM
                    px[tx * T + x, ty * T + y] = shade(colour, k)
    im.save(os.path.join(HERE, name))


if __name__ == '__main__':
    ceiling()
    ceiling('Ceiling_Grid.png')   # the same grid under the name of the work-area ceiling material
    carpet()
    carpet(CARPET_DARK, 'Carpet_Dark.png')
    print('wrote the ceiling, ceiling grid, carpet and dark carpet textures to', HERE)
