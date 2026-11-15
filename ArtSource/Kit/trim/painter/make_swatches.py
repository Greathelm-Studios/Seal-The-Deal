# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: make the trim sheet in Substance Painter
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Writes the fill sources the Painter layer script uses: 8x8 colour swatches per trim material and grey
#              values for roughness/metal (script-made Painter projects are legacy colour, so fills take bitmaps,
#              not colour values), plus a brushed-metal roughness map that tiles along U.
# -----------------------------------------------------------------------------
"""python make_swatches.py  ->  ./swatches/*.png"""
import os
import random

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'swatches')
os.makedirs(OUT, exist_ok=True)

# sRGB base colours: concepts/Japanese_Aero/DECISION.md palette and concepts/Props_Aero/README*.md.
COLOURS = {
    'pearl': 'F4F8F8', 'pearl_hi': 'FFFFFF', 'silver': 'C8D2D9', 'chrome': 'DCE3E8', 'aqua': '45BFC7',
    'mist': 'A8BFD1', 'navy': '111B38', 'screen': '0E1626', 'cobalt': '244FB9', 'led': '20D4E6',
    'seal': 'C7473E', 'leaf': '70AF59', 'cavity': '5C6B80', 'black': '000000',
}
for name, hx in COLOURS.items():
    Image.new('RGB', (8, 8), tuple(int(hx[i:i + 2], 16) for i in (0, 2, 4))).save(os.path.join(OUT, f'sw_{name}.png'))
for g in range(0, 101):
    v = round(g * 255 / 100)
    Image.new('RGB', (8, 8), (v, v, v)).save(os.path.join(OUT, f'g{g:03d}.png'))

# Brushed metal: long horizontal streaks (along U, the trim's tiling direction) around roughness 0.35.
# WHY generated, not a Painter procedural: the streaks must run exactly along U and tile at the sheet's edge.
W, H = 1024, 256
rnd = random.Random(7)
img = Image.new('L', (W, H))
px = img.load()
for y in range(H):
    base = 89 + rnd.randint(-14, 14)                 # 0.35 * 255 = 89
    for x in range(W):
        px[x, y] = max(0, min(255, base + rnd.randint(-3, 3)))
img.save(os.path.join(OUT, 'brushed_rough.png'))
print('swatches written to', OUT)
