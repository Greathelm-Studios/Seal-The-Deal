# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: "satirical motivational office messages along the walls ... same with presentations flicking to
#              different slides in a presentation or pitch room"
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Lays out KitOffice's satirical motivational posters and the pitch-room slide atlas with exact text, from
#              COPY.md, over Codex's textless poster art. Its own file because the copy is a writing job and the
#              images an image-generation job; this joins them so neither has to do the other's.
# -----------------------------------------------------------------------------
"""Usage: python ArtSource/Kit/posters/make_posters.py

Writes ../signs/textures/Poster_NN.png (KitSigns imports them) and Assets/_Project/Art/Animated/Slides_Pitch.png (Codex's
slide display reads it), plus review/posters_sheet.png. Missing art falls back to an Aero gradient so it always runs.
"""
import math
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(KIT))
sys.path.insert(0, os.path.join(KIT, 'signs'))
import make_signs as ms   # noqa: E402  (fonts, palette, label canvas)

OUT = os.path.join(KIT, 'signs', 'textures')
SLIDES_OUT = os.path.join(REPO, 'Assets', '_Project', 'Art', 'Animated', 'Slides_Pitch.png')
ART = os.path.join(HERE, 'art')

POSTER_W, POSTER_H = 0.6, 0.8          # poster on the wall, metres (a real 600 x 800 mm print)
POSTER_PX_PER_M = 1000                 # texels per metre: caption type ~16 mm cap stays crisp at 2-3 m
FRAME = 0.018                          # silver frame width, metres
MAT = 0.022                            # white mat inside the frame, metres
ART_ASPECT = 4 / 3                     # Codex's images are 1024 x 768
NAVY_PANEL = (0x0F, 0x1E, 0x3C)        # the caption panel: deep navy, the classic poster's black in our palette
SLIDE_W, SLIDE_H = 960, 540            # one slide in the atlas, pixels (16:9)
SLIDE_COLS, SLIDE_ROWS = 4, 2          # atlas layout, reading order from the top left (REQUEST.md)


def copy_tables():
    """(posters, slides) from COPY.md's two markdown tables."""
    text = open(os.path.join(HERE, 'COPY.md'), encoding='utf8').read()
    rows = [[c.strip() for c in line.strip().strip('|').split('|')] for line in text.splitlines()
            if line.startswith('|') and not re.match(r'^\|\s*-', line)]
    posters = [r for r in rows if re.fullmatch(r'\d\d', r[0])]
    slides = [r for r in rows if re.fullmatch(r'\d', r[0])]
    return posters, slides


def aero_gradient(size, top=(0x2E, 0x7B, 0xD8), bottom=(0xBF, 0xE6, 0xFF)):
    """Fallback art: a sky gradient with a soft highlight, for a poster whose image isn't painted yet."""
    img = ms.gradient(size, top, bottom).convert('RGB')
    glow = Image.new('L', size, 0)
    ImageDraw.Draw(glow).ellipse((size[0] * 0.2, size[1] * 0.1, size[0] * 0.8, size[1] * 0.7), fill=120)
    img.paste(Image.new('RGB', size, (255, 255, 255)), (0, 0), glow.filter(ImageFilter.GaussianBlur(size[0] // 8)))
    return img


def poster(row):
    pid, title, ja, caption = row[0], row[1], row[2], row[3]
    img, den = ms.canvas(POSTER_W, POSTER_H, POSTER_PX_PER_M)
    d = ImageDraw.Draw(img)
    L = lambda m: ms.lpx(m, den)
    W, H = img.size
    d.rectangle((0, 0, W, H), fill=(0xC8, 0xD2, 0xD9, 255))                       # silver frame
    d.rectangle((L(FRAME), L(FRAME), W - L(FRAME), H - L(FRAME)), fill=NAVY_PANEL + (255,))
    # the art, matted in white, across the top
    ax0, ay0 = L(FRAME + MAT), L(FRAME + MAT)
    aw = W - 2 * ax0
    ah = int(aw / ART_ASPECT)
    d.rectangle((ax0 - L(0.004), ay0 - L(0.004), ax0 + aw + L(0.004), ay0 + ah + L(0.004)), fill=(0xF4, 0xF8, 0xF8, 255))
    path = os.path.join(ART, f'Poster_Art_{pid}.png')
    art = Image.open(path).convert('RGB') if os.path.exists(path) else aero_gradient((1024, 768))
    img.paste(art.resize((aw, ah), Image.LANCZOS), (ax0, ay0))
    # the title, wide-spaced, then a hairline, the caption and the Japanese title
    # the text block sits centred in the navy panel under the art
    panel_top, panel_bottom = ay0 + ah, H - L(FRAME)
    y = panel_top + (panel_bottom - panel_top) * 0.42
    cap = 0.042
    while True:   # WHY a loop: the title's letter spacing counts too, so shrink until the spaced word fits the art width
        ft = ms.fitted(ms.LATIN, cap, den)
        track = L(cap * 0.28)
        tw = sum(ft.getlength(c) for c in title) + track * (len(title) - 1)
        if tw <= aw * 0.94 or cap < 0.02:
            break
        cap *= 0.94
    x = (W - tw) / 2
    for c in title:
        d.text((x, y), c, font=ft, fill=ms.WHITE, anchor='ls')
        x += ft.getlength(c) + track
    d.rectangle((W / 2 - L(0.12), y + L(0.018), W / 2 + L(0.12), y + L(0.0195)), fill=(0xC8, 0xD2, 0xD9))
    fc = ms.fitted(ms.LATIN, 0.013, den, body=caption, max_w_px=int(aw * 0.96))
    d.text((W / 2, y + L(0.055)), caption, font=fc, fill=(0xDC, 0xE8, 0xF2), anchor='ms')
    fj = ms.fitted(ms.JAPANESE, 0.02, den, 500)
    d.text((W / 2, y + L(0.098)), ja, font=fj, fill=(0x8F, 0xC3, 0xF5), anchor='ms')
    ms.save_label(img, f'Poster_{pid}')
    return img


# ---------------------------------------------------------------- slides

def slide(row, n):
    _, title, body = row[0], row[1], row[2]
    S = 2   # supersample
    w, h = SLIDE_W * S, SLIDE_H * S
    img = Image.new('RGB', (w, h), (0xF7, 0xFA, 0xFC))
    d = ImageDraw.Draw(img)
    head = int(h * 0.2)
    img.paste(ms.gradient((w, head), (0x1D, 0x3F, 0xC4), (0x45, 0xBF, 0xC7)).convert('RGB').rotate(0), (0, 0))
    font = lambda path, px, weight=None: _font(path, px, weight)
    d.text((int(w * 0.05), int(head * 0.62)), title, font=font(ms.LATIN, int(head * 0.3)), fill=(255, 255, 255), anchor='ls')
    # AONAGI ring and seal dot, top right
    cx, cy, r = int(w * 0.92), int(head * 0.5), int(head * 0.28)
    d.arc((cx - r, cy - r, cx + r, cy + r), start=60, end=370, fill=(255, 255, 255), width=max(3, r // 5))
    d.ellipse((cx + r * 0.65 - r * 0.2, cy + r * 0.55 - r * 0.2, cx + r * 0.65 + r * 0.2, cy + r * 0.55 + r * 0.2), fill=ms.SEAL)
    body_font = font(ms.LATIN, int(h * 0.045))
    x0, y0 = int(w * 0.07), int(head + h * 0.16)
    navy = (0x16, 0x26, 0x4A)
    if n == 3:     # morale vs coffee
        gx0, gy0, gx1, gy1 = x0, y0, int(w * 0.9), int(h * 0.85)
        d.line((gx0, gy1, gx1, gy1), fill=navy, width=4); d.line((gx0, gy0, gx0, gy1), fill=navy, width=4)
        pts_up = [(gx0 + (gx1 - gx0) * i / 6, gy1 - (gy1 - gy0) * (0.15 + 0.13 * i)) for i in range(7)]
        pts_dn = [(gx0 + (gx1 - gx0) * i / 6, gy1 - (gy1 - gy0) * (0.9 - 0.13 * i + 0.04 * math.sin(i * 2))) for i in range(7)]
        d.line(pts_up, fill=(0x8B, 0x5A, 0x2B), width=10); d.line(pts_dn, fill=(0x1E, 0x9B, 0xFF), width=10)
        small = font(ms.LATIN, int(h * 0.035))
        d.text((pts_up[-1][0] - 20, pts_up[-1][1] - 30), 'COFFEE', font=small, fill=(0x8B, 0x5A, 0x2B), anchor='rs')
        d.text((pts_dn[-1][0] - 20, pts_dn[-1][1] - 30), 'MORALE', font=small, fill=(0x1E, 0x9B, 0xFF), anchor='rs')
    elif n == 4:   # headcount
        for i in range(10):
            a = 255 if i < 5 else max(25, 200 - (i - 5) * 45)
            px = x0 + i * int(w * 0.085)
            col = tuple(int(c * a / 255 + 247 * (1 - a / 255)) for c in navy)
            d.ellipse((px + 20, y0 + 30, px + 90, y0 + 100), fill=col)
            d.rounded_rectangle((px, y0 + 115, px + 110, y0 + 290), radius=40, fill=col)
    elif n == 6:   # empty pie
        cx, cy, r = w // 2, int(h * 0.6), int(h * 0.26)
        d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=navy, width=8)
        d.text((cx, cy), 'TBD', font=font(ms.LATIN, int(h * 0.07)), fill=(0x9A, 0xA8, 0xBE), anchor='mm')
    elif n == 8:   # thank you: the big question, the small plea
        d.text((w // 2, int(h * 0.56)), 'Questions?', font=font(ms.LATIN, int(h * 0.11)), fill=navy, anchor='ms')
        d.text((w // 2, int(h * 0.68)), '(Please, no questions.)', font=font(ms.LATIN, int(h * 0.035)), fill=(0x6A, 0x80, 0xA8), anchor='ms')
    else:
        main, _, note = body.partition('  — ')
        lines = [s.strip() for s in re.split(r' · ', main)] if n in (5, 7) else [main]
        for i, line in enumerate(lines):
            d.text((x0, y0 + i * int(h * 0.11)), line, font=body_font, fill=navy, anchor='ls')
        if note:
            d.text((x0, int(h * 0.86)), note, font=font(ms.LATIN, int(h * 0.028)), fill=(0x6A, 0x80, 0xA8), anchor='ls')
    d.text((int(w * 0.05), int(h * 0.95)), f'AONAGI CONFIDENTIAL  ·  {n:02d}', font=font(ms.LATIN, int(h * 0.025)),
           fill=(0x9A, 0xA8, 0xBE), anchor='ls')
    return img.resize((SLIDE_W, SLIDE_H), Image.LANCZOS)


_fonts = {}


def _font(path, px, weight=None):
    from PIL import ImageFont
    key = (path, px, weight)
    if key not in _fonts:
        f = ImageFont.truetype(path, px)
        if weight is not None:
            f.set_variation_by_axes([weight])
        _fonts[key] = f
    return _fonts[key]


# ---------------------------------------------------------------- wall text

WALL_TEXT_BAND = 0.62          # height of a wall-text stripe, metres (KitOfficeScene WallTextBand). 0.62 since 2026-11-15
                               # (Alex: "larger and more visible ... a bit bland"): the whole height between the 3.6 m
                               # lightbox tops and the soffit
WALL_TEXT_CAP = 0.40           # letter (capital) height, metres; shrunk if a line would overrun its run
HALF_BLUES = {'A': (0x1D, 0x3F, 0xC4), 'B': (0x1E, 0x9B, 0xFF)}   # make_markings HALF_A_BLUE, HALF_B_BLUE
WALL_TEXT_PX_PER_M = 120       # texels per metre: 2 mm texels, seen from 3+ m below
WALL_TEXT_MARGIN = 0.4         # space kept clear at each end of a run, metres
INKS = {'white': (255, 255, 255), 'navy': (0x16, 0x26, 0x4A)}


def wall_text_rows():
    text = open(os.path.join(HERE, 'COPY.md'), encoding='utf8').read()
    rows = [[c.strip() for c in line.strip().strip('|').split('|')] for line in text.splitlines()
            if line.startswith('|') and not re.match(r'^\|\s*-', line)]
    return [(r[0], r[1], int(r[2]), r[3]) for r in rows if re.fullmatch(r'[AB][12]', r[0])]


def wall_text():
    """One whole message per wall face, centred on it (Alex 2026-11-15: "one per wall, the wrapping is confusing").
    Each face is inked for its wall (white over the cobalt fields, navy on white)."""
    import json
    plan = json.load(open(os.path.join(KIT, 'markings', 'l2_on_kit.json'), encoding='utf8'))['decals']['wallText']
    S = 2
    k = WALL_TEXT_PX_PER_M * S
    made = 0
    for set_id, run, i, line in wall_text_rows():
        sg = plan[run][i]
        total = abs(sg['b'][0] - sg['a'][0]) + abs(sg['b'][1] - sg['a'][1])
        w, h = int(round(total * k)), int(round(WALL_TEXT_BAND * k))
        cap = WALL_TEXT_CAP
        while True:
            # WHY M PLUS 1 at 800 for the Latin: Michroma has one light weight; the stripe wants heavy, poster-like letters.
            f = _font(ms.JAPANESE, max(8, int(cap * k / 0.72)), 800)
            if f.getlength(line) <= (total - 2 * WALL_TEXT_MARGIN) * k or cap < 0.08:
                break
            cap *= 0.95
        mask = Image.new('L', (w, h), 0)
        ImageDraw.Draw(mask).text((w / 2, h / 2), line, font=f, fill=255, anchor='mm')
        # A solid stripe: the half's blue with white letters on white walls; white with blue letters over the fields.
        blue = HALF_BLUES[set_id[0]]
        ground, letters = (blue, (255, 255, 255)) if sg['ink'] == 'navy' else ((255, 255, 255), blue)
        piece = Image.new('RGBA', (w, h), ground + (255,))
        piece.paste(Image.new('RGBA', (w, h), letters + (255,)), (0, 0), mask)
        piece.resize((w // S, h // S), Image.LANCZOS).save(os.path.join(OUT, f'WallText_{set_id}_{run}_{i}.png'))
        made += 1
    return made


def slide_atlas(slides):
    atlas = Image.new('RGB', (SLIDE_W * SLIDE_COLS, SLIDE_H * SLIDE_ROWS))
    for i, row in enumerate(slides):
        atlas.paste(slide(row, i + 1), ((i % SLIDE_COLS) * SLIDE_W, (i // SLIDE_COLS) * SLIDE_H))
    os.makedirs(os.path.dirname(SLIDES_OUT), exist_ok=True)
    atlas.save(SLIDES_OUT)
    return atlas


if __name__ == '__main__':
    posters, slides = copy_tables()
    assert len(posters) == 12 and len(slides) == SLIDE_COLS * SLIDE_ROWS, (len(posters), len(slides))
    made = [poster(r) for r in posters]
    print('wall text strips:', wall_text())
    atlas = slide_atlas(slides)
    sheet = Image.new('RGB', (6 * 300 + 70, 2 * 400 + 30 + 540), (0xE8, 0xEC, 0xF0))
    for i, p in enumerate(made):
        sheet.paste(p.convert('RGB').resize((300, 400)), (10 + (i % 6) * 310, 10 + (i // 6) * 410))
    sheet.paste(atlas.resize((1880, 528)), (10, 830))
    os.makedirs(os.path.join(HERE, 'review'), exist_ok=True)
    sheet.save(os.path.join(HERE, 'review', 'posters_sheet.png'))
    have = sum(os.path.exists(os.path.join(ART, f'Poster_Art_{r[0]}.png')) for r in posters)
    print(f'12 posters ({have} with Codex art), 8 slides')
