# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: the props "doesnt match the references still" (no logo, labels or printed packaging)
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Draws the trim sheet's print artwork (the AONAGI ring, an asset tag, the copier's 複写 / COPY display
#              and three fictional drink-can wraps) into a full-sheet-sized image that the Painter layer script uses as
#              the Print band's fill. Positions come from trim_layout.json "print", so the art and the UVs agree.
# -----------------------------------------------------------------------------
"""python make_print.py  ->  ./swatches/print_atlas_v2.png (2048 x 2048, the Print band filled, the rest pearl)."""
import json
import math
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
TRIM = os.path.dirname(HERE)
FONTS = os.path.join(TRIM, '..', 'fonts')
LATIN = os.path.join(FONTS, 'Michroma-Regular.ttf')     # wide geometric display face (OFL), as the signs use
JAPANESE = os.path.join(FONTS, 'MPLUS1[wght].ttf')      # M PLUS 1, variable weight (OFL), as the signs use
layout = json.load(open(os.path.join(TRIM, 'trim_layout.json'), encoding='utf-8'))
SIZE = layout['size_px']
band_px = next(b['px'] for b in layout['bands'] if b['kind'] == 'print')
TOP = SIZE - band_px                        # the print band is the sheet's bottom band

PEARL, NAVY, AQUA, SEAL = (244, 248, 248), (17, 27, 56), (69, 191, 199), (199, 71, 62)
COBALT, SMOKE, CYAN, WHITE, MIST = (36, 79, 185), (14, 22, 38), (32, 212, 230), (255, 255, 255), (168, 191, 209)

img = Image.new('RGB', (SIZE, SIZE), PEARL)
d = ImageDraw.Draw(img)


def rect(name):
    x0, y0, x1, y1, _ = layout['print'][name]
    return x0, TOP + y0, x1, TOP + y1


def jp(size, weight=700):
    f = ImageFont.truetype(JAPANESE, size)
    try:
        f.set_variation_by_axes([weight])
    except OSError:
        pass
    return f


def ring(cx, cy, outer, draw=d):
    """The AONAGI mark (concepts/Props_Aero/README.md master): an aqua ring, stroke 12 % of its diameter, open for
    55 degrees at the lower right, with a red hanko dot of 16 % in the gap."""
    stroke = outer * 0.12
    gap_c = 45.0                                    # degrees below the +x axis (PIL angles run clockwise)
    draw.arc([cx - outer / 2, cy - outer / 2, cx + outer / 2, cy + outer / 2], gap_c + 27.5, gap_c - 27.5 + 360,
             fill=AQUA, width=int(stroke))
    r = outer / 2 - stroke / 2
    dx, dy = r * math.cos(math.radians(gap_c)), r * math.sin(math.radians(gap_c))
    dot = outer * 0.16
    draw.ellipse([cx + dx - dot / 2, cy + dy - dot / 2, cx + dx + dot / 2, cy + dy + dot / 2], fill=SEAL)


# The mark on pearl.
x0, y0, x1, y1 = rect('Print_Ring')
d.rectangle([x0, y0, x1, y1], fill=PEARL)
ring((x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) * 0.78)

# Asset tag: mark, AONAGI wordmark, a tiny asset number (bible §13 "serial numbers ... used sparingly").
x0, y0, x1, y1 = rect('Print_Tag')
d.rectangle([x0, y0, x1, y1], fill=PEARL)
h = y1 - y0
ring(x0 + h * 0.55, y0 + h / 2, h * 0.7)
d.text((x0 + h * 1.1, y0 + h * 0.32), 'AONAGI', font=ImageFont.truetype(LATIN, int(h * 0.3)), fill=NAVY, anchor='lm')
d.text((x0 + h * 1.1, y0 + h * 0.72), 'ASSET 04-2217  青凪総合サービス', font=jp(int(h * 0.15), 500), fill=NAVY, anchor='lm')

# Stacked logo: the ring above the AONAGI wordmark (the copier's front, sheet 2).
x0, y0, x1, y1 = rect('Print_Logo')
d.rectangle([x0, y0, x1, y1], fill=PEARL)
side = x1 - x0
ring((x0 + x1) / 2, y0 + side * 0.36, side * 0.5)
d.text(((x0 + x1) / 2, y0 + side * 0.8), 'AONAGI', font=ImageFont.truetype(LATIN, int(side * 0.13)), fill=NAVY, anchor='mm')

# Copier display: a smoked LCD with the bilingual label in cyan.
x0, y0, x1, y1 = rect('Print_Copy')
d.rectangle([x0, y0, x1, y1], fill=SMOKE)
d.text(((x0 + x1) / 2, (y0 + y1) / 2), '複写 / COPY', font=jp(int((y1 - y0) * 0.5), 600), fill=CYAN, anchor='mm')


def can(name, bg, fg, wave, word):
    """A fictional drink-can wrap: the name twice round the can (so one faces out whichever way it turns) over a
    soft wave stripe. No real brand (REQUEST.md)."""
    x0, y0, x1, y1 = rect(name)
    w, h = x1 - x0, y1 - y0
    d.rectangle([x0, y0, x1, y1], fill=bg)
    pts = [(x0 + i, y0 + h * 0.72 + h * 0.08 * math.sin(2 * math.pi * 2 * i / w)) for i in range(w + 1)]
    d.polygon(pts + [(x1, y1), (x0, y1)], fill=wave)
    f = ImageFont.truetype(LATIN, int(h * 0.2))
    for k in (0.25, 0.75):
        d.text((x0 + w * k, y0 + h * 0.42), word, font=f, fill=fg, anchor='mm')
    d.rectangle([x0, y0, x1, y0 + h * 0.06], fill=MIST)   # silver lid band


can('Print_Can_NAGI', COBALT, WHITE, AQUA, 'NAGI')
can('Print_Can_MIZU', WHITE, NAVY, AQUA, 'MIZU')
can('Print_Can_AO', NAVY, AQUA, COBALT, 'AO')

out = os.path.join(HERE, 'swatches', 'print_atlas_v2.png')
os.makedirs(os.path.dirname(out), exist_ok=True)
img.save(out)
img.crop((0, TOP, SIZE, SIZE)).save(os.path.join(HERE, 'swatches', 'print_band_preview.png'))
print('print atlas written to', out)
