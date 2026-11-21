"""Usage: python ArtSource/Kit/signs/make_signs.py

Writes Sign_Zone_<L|R|C|H|A>.png into textures/ and review/sign_spec.png beside this script.
Each texture covers SIGN_W x SIGN_H metres of wall at PX_PER_M, transparent where the wall shows through.

Trade-off: textures on flat quads, not URP decal projectors. A quad is one cheap draw that sits on the wall like the
modelled sign did and needs no renderer feature; a projector would also wrap round corners, which no sign here needs.
"""
import os

from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = os.path.join(HERE, '..', 'fonts')
LATIN = os.path.join(FONTS, 'Michroma-Regular.ttf')     # wide geometric display face (OFL)
JAPANESE = os.path.join(FONTS, 'MPLUS1[wght].ttf')      # M PLUS 1, variable weight (OFL)

PX_PER_M = 320          # texels per metre. WHY: 1 texel ~3 mm, crisp at the 3-15 m players read signs from
SUPERSAMPLE = 4         # drawn this many times larger, then filtered down: smooth edges without a vector rasteriser
SIGN_W, SIGN_H = 3.2, 4.0   # metres: the zone graphic's wall area, skirting (0.1 m) to under the soffit reveal

# Kit palette (ArtSource/Kit/concepts/Japanese_Aero/DECISION.md), sRGB.
NAVY = (0x11, 0x1B, 0x38)
COBALT = (0x24, 0x4F, 0xB9)
COBALT_DEEP = (0x16, 0x34, 0x86)
AQUA = (0x45, 0xBF, 0xC7)
AQUA_LIGHT = (0xA6, 0xE6, 0xEA)
WHITE = (0xFF, 0xFF, 0xFF)
WALL = (0xF4, 0xF8, 0xF8)

ZONES = {   # letter: (Japanese, English, floor). Copy checked by Codex (concepts/Japanese_Aero/README.md)
    'L': ('法務部', 'LEGAL', '1F'),
    'R': ('審査部', 'REVIEW', '1F'),
    'C': ('請求部', 'CLAIMS', '1F'),
    'H': ('審問室', 'HEARINGS', '1F-2F'),
    'A': ('記録保管室', 'ARCHIVE', '1F'),
}
COMPANY = 'AONAGI CORPORATE SERVICES'

# Layout, metres from the sign's bottom-left (y up). One grid for every zone sign.
MARGIN = 0.12            # left edge of every element
LETTER_CAP = 2.05        # giant zone letter: capital height
LETTER_BASE = 1.75       # its baseline
BAND = (1.2, 1.5)        # the glossy aqua band under the letter: bottom, top
BAND_TEXT_CAP = 0.13     # English name inside the band, capital height
RULE_Y, RULE_T = 1.12, 0.012   # cobalt hairline under the band
JA_CAP, JA_BASE = 0.42, 0.52   # Japanese department name
MICRO_CAP, MICRO_BASE = 0.045, 0.2   # microtype line
TRACKING = 0.25          # extra letter spacing for wide caps, share of the cap height


def px(m):
    """Metres to supersampled pixels."""
    return int(round(m * PX_PER_M * SUPERSAMPLE))


def font(path, cap_m, weight=None):
    """A font sized so its capital H (or the Japanese 審) is `cap_m` metres tall."""
    probe = 200
    f = ImageFont.truetype(path, probe)
    if weight is not None:
        f.set_variation_by_axes([weight])
    sample = 'H' if path == LATIN else '審'
    x0, y0, x1, y1 = f.getbbox(sample)
    size = max(1, int(round(probe * px(cap_m) / (y1 - y0))))
    f = ImageFont.truetype(path, size)
    if weight is not None:
        f.set_variation_by_axes([weight])
    return f


def text(draw, img_h, x_m, base_m, body, f, fill, tracking=0.0, cap_m=None):
    """Draws `body` with its baseline at base_m metres, starting at x_m; tracking is extra spacing per character."""
    x = px(x_m)
    y = img_h - px(base_m)
    gap = px((cap_m or 0) * tracking)
    for ch in body:
        draw.text((x, y), ch, font=f, fill=fill, anchor='ls')
        x += int(draw.textlength(ch, font=f)) + gap
    return x


def text_width(draw, body, f, tracking_px):
    return sum(int(draw.textlength(ch, font=f)) + tracking_px for ch in body) - tracking_px


def gradient(size, top, bottom):
    """A vertical gradient image, top colour to bottom colour."""
    w, h = size
    g = Image.new('RGB', (1, h))
    for y in range(h):
        t = y / max(h - 1, 1)
        g.putpixel((0, y), tuple(int(round(a + (b - a) * t)) for a, b in zip(top, bottom)))
    return g.resize((w, h))


def zone_sign(letter):
    ja, en, floor = ZONES[letter]
    W, H = px(SIGN_W), px(SIGN_H)
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Glossy aqua band: a vertical gradient with the Aero "gel" highlight on its upper half.
    b0, b1 = BAND
    band_box = (px(MARGIN), H - px(b1), W - px(MARGIN), H - px(b0))
    bw, bh = band_box[2] - band_box[0], band_box[3] - band_box[1]
    band = gradient((bw, bh), AQUA_LIGHT, AQUA).convert('RGBA')
    gloss = Image.new('RGBA', (bw, bh), (0, 0, 0, 0))
    ImageDraw.Draw(gloss).rounded_rectangle((px(0.02), px(0.015), bw - px(0.02), bh // 2), radius=bh // 4,
                                            fill=(255, 255, 255, 90))
    band.alpha_composite(gloss)
    mask = Image.new('L', (bw, bh), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, bw - 1, bh - 1), radius=bh // 2, fill=255)
    img.paste(band, band_box[:2], mask)

    # English name in the band, white, wide-tracked, and the floor code at the band's right end.
    f_band = font(LATIN, BAND_TEXT_CAP)
    mid = (b0 + b1) / 2 - BAND_TEXT_CAP / 2
    text(d, H, MARGIN + 0.16, mid, en, f_band, WHITE, TRACKING, BAND_TEXT_CAP)
    fw = text_width(d, floor, f_band, px(BAND_TEXT_CAP * TRACKING))
    text(d, H, SIGN_W - MARGIN - 0.16 - fw / (PX_PER_M * SUPERSAMPLE), mid, floor, f_band, WHITE, TRACKING, BAND_TEXT_CAP)

    # Giant zone letter: cobalt with a top-to-bottom deepening, drawn through a mask so the gradient fills the glyph.
    f_big = font(LATIN, LETTER_CAP)
    glyph = Image.new('L', (W, H), 0)
    ImageDraw.Draw(glyph).text((px(MARGIN) - px(0.04), H - px(LETTER_BASE)), letter, font=f_big, fill=255, anchor='ls')
    fill = gradient((W, H), COBALT, COBALT_DEEP).convert('RGBA')
    img.paste(fill, (0, 0), glyph)

    # Hairline rule under the band, Japanese name, microtype line with its own hairline.
    d.rectangle((px(MARGIN), H - px(RULE_Y + RULE_T), W - px(MARGIN), H - px(RULE_Y)), fill=COBALT)
    text(d, H, MARGIN, JA_BASE, ja, font(JAPANESE, JA_CAP, 800), NAVY)
    d.rectangle((px(MARGIN), H - px(MICRO_BASE + MICRO_CAP + 0.06), px(MARGIN + 0.9),
                 H - px(MICRO_BASE + MICRO_CAP + 0.05)), fill=NAVY)
    text(d, H, MARGIN, MICRO_BASE, f'{COMPANY}  /  ZONE {letter}  /  {floor}', font(LATIN, MICRO_CAP), NAVY,
         TRACKING, MICRO_CAP)

    out = img.resize((W // SUPERSAMPLE, H // SUPERSAMPLE), Image.LANCZOS)
    os.makedirs(os.path.join(HERE, 'textures'), exist_ok=True)
    out.save(os.path.join(HERE, 'textures', f'Sign_Zone_{letter}.png'))
    return out


def spec_sheet(signs):
    """All five signs on the wall colour, side by side, with the palette and type samples under them."""
    pad = 40
    w = sum(s.width for s in signs) + pad * (len(signs) + 1)
    h = signs[0].height + 2 * pad + 260
    sheet = Image.new('RGB', (w, h), WALL)
    x = pad
    for s in signs:
        sheet.paste(s, (x, pad), s)
        x += s.width + pad
    d = ImageDraw.Draw(sheet)
    y = signs[0].height + 2 * pad
    for i, (name, c) in enumerate((('NAVY 111B38', NAVY), ('COBALT 244FB9', COBALT), ('AQUA 45BFC7', AQUA),
                                   ('AQUA LIGHT A6E6EA', AQUA_LIGHT), ('WHITE', WHITE))):
        d.rectangle((pad + i * 340, y, pad + i * 340 + 60, y + 60), fill=c, outline=NAVY)
        d.text((pad + i * 340 + 72, y + 18), name, font=ImageFont.truetype(LATIN, 16), fill=NAVY)
    d.text((pad, y + 100), 'MICHROMA  -  AONAGI CORPORATE SERVICES  0123456789', font=ImageFont.truetype(LATIN, 34), fill=NAVY)
    fj = ImageFont.truetype(JAPANESE, 40)
    fj.set_variation_by_axes([800])
    d.text((pad, y + 160), 'M PLUS 1  -  青凪総合サービス株式会社  法務部 審査部 請求部 審問室 記録保管室', font=fj, fill=NAVY)
    os.makedirs(os.path.join(HERE, 'review'), exist_ok=True)
    sheet.save(os.path.join(HERE, 'review', 'sign_spec.png'))


# ---------------------------------------------------------------- labels (2026-11-15: "convert the rest")
# Every other piece of text in the office, on the same two typefaces. Sizes are the label's quad on its piece,
# metres; KitSigns.cs places each quad with the same sizes (keep the two tables in step).

LABEL_PX_PER_M = 1024   # small labels need more texels per metre than the 3-4 m zone signs to stay sharp
LED_PITCH = 0.0065      # dot-matrix pitch, metres. WHY this fine: kanji need ~16 dots of height to read
LED_DOT = 0.75          # LED dot diameter as a share of the pitch
LED_ON = (0x7F, 0xF0, 0xF4)   # lit LED colour: the aqua, lifted so it reads as light
COLUMN_LINES = {
    'Column_Graphic': ('1F', ['< L 法務部', '  C 請求部', '  A 記録保管室', '> H 受付']),
    'Column_Graphic_Tall': ('2F', ['^ H 審問室', '^ R 審査部', '  会議室', 'v 受付 1F']),
}
COMPANY_JA = '青凪総合サービス'


def canvas(w_m, h_m, density=LABEL_PX_PER_M):
    W, H = int(round(w_m * density * SUPERSAMPLE)), int(round(h_m * density * SUPERSAMPLE))
    return Image.new('RGBA', (W, H), (0, 0, 0, 0)), density


def lpx(m, density):
    return int(round(m * density * SUPERSAMPLE))


def fitted(path, cap_m, density, weight=None, body='H', max_w_px=None):
    """A font whose capital is cap_m metres at this density, shrunk if `body` would run wider than max_w_px."""
    probe = 200
    def make(size):
        f = ImageFont.truetype(path, size)
        if weight is not None:
            f.set_variation_by_axes([weight])
        return f
    f = make(probe)
    sample = 'H' if path == LATIN else '審'
    y0, y1 = f.getbbox(sample)[1], f.getbbox(sample)[3]
    size = max(1, int(round(probe * lpx(cap_m, density) / (y1 - y0))))
    f = make(size)
    if max_w_px:
        w = f.getlength(body)
        if w > max_w_px:
            f = make(max(1, int(size * max_w_px / w)))
    return f


def save_label(img, name):
    out = img.resize((img.width // SUPERSAMPLE, img.height // SUPERSAMPLE), Image.LANCZOS)
    out.save(os.path.join(HERE, 'textures', f'{name}.png'))
    return out


def arrow(d, cx, cy, s, kind, fill):
    """A wayfinding arrow pictogram centred at (cx, cy) pixels, s pixels across: '<', '>', '^' or 'v'."""
    h = s / 2
    shapes = {'>': [(-h, -h * 0.55), (h * 0.1, -h * 0.55), (h * 0.1, -h), (h, 0), (h * 0.1, h), (h * 0.1, h * 0.55), (-h, h * 0.55)]}
    pts = shapes['>']
    rot = {'>': (1, 0), '<': (-1, 0), '^': (0, -1), 'v': (0, 1)}[kind]
    out = []
    for x, y in pts:
        if rot == (1, 0):
            out.append((cx + x, cy + y))
        elif rot == (-1, 0):
            out.append((cx - x, cy + y))
        elif rot == (0, -1):
            out.append((cx + y, cy - x))
        else:
            out.append((cx + y, cy + x))
    d.polygon(out, fill=fill)


def column_label(piece):
    """A column's wayfinding plate: the floor number big in Michroma, a cobalt hairline, then one row per route with
    an arrow pictogram, the zone letter in a navy tile and the department in M PLUS 1."""
    number, lines = COLUMN_LINES[piece]
    w_m, h_m = 0.68, 1.5
    img, den = canvas(w_m, h_m)
    d = ImageDraw.Draw(img)
    H = img.height
    m = lpx(0.03, den)
    d.text((m, H - lpx(1.02, den)), number, font=fitted(LATIN, 0.32, den), fill=NAVY, anchor='ls')
    d.rectangle((m, H - lpx(0.95, den), img.width - m, H - lpx(0.945, den)), fill=COBALT)
    row_h, cap = 0.17, 0.065
    tile = lpx(0.11, den)
    for i, line in enumerate(lines):
        base = 0.78 - i * row_h
        cy = H - lpx(base + cap / 2, den)
        tokens = line.split()
        x = m
        if tokens and tokens[0] in '<>^v':
            arrow(d, x + tile / 2, cy, tile * 0.8, tokens.pop(0), COBALT)
        x += tile + lpx(0.02, den)
        if tokens and len(tokens[0]) == 1 and tokens[0].isascii():
            letter = tokens.pop(0)
            d.rounded_rectangle((x, cy - tile / 2, x + tile, cy + tile / 2), radius=tile // 5, fill=NAVY)
            d.text((x + tile / 2, cy), letter, font=fitted(LATIN, cap * 0.85, den), fill=WHITE, anchor='mm')
        x += tile + lpx(0.03, den)
        name = ' '.join(tokens)
        f = fitted(JAPANESE, cap, den, 700, name, img.width - x - m)
        d.text((x, H - lpx(base, den)), name, font=f, fill=NAVY, anchor='ls')
    return save_label(img, f'Label_{piece}')


def company_label(name, w_m, h_m, ja_cap, en, en_cap):
    """The company name block: Japanese in M PLUS 1 heavy, the English under it in tracked Michroma, a hairline."""
    img, den = canvas(w_m, h_m)
    d = ImageDraw.Draw(img)
    H = img.height
    f = fitted(JAPANESE, ja_cap, den, 800, COMPANY_JA, img.width)
    d.text((0, H - lpx(h_m - ja_cap - 0.03, den)), COMPANY_JA, font=f, fill=NAVY, anchor='ls')
    d.rectangle((0, H - lpx(en_cap + 0.05, den), lpx(0.5, den), H - lpx(en_cap + 0.045, den)), fill=COBALT)
    fe = fitted(LATIN, en_cap, den)
    x = 0
    for ch in en:
        d.text((x, H - lpx(0.01, den)), ch, font=fe, fill=NAVY, anchor='ls')
        x += fe.getlength(ch) + lpx(en_cap * TRACKING, den)
    return save_label(img, name)


def led_label(name, body, w_m, h_m, cap_m):
    """Dot-matrix LED text: the line is drawn, sampled onto an LED grid, and every lit cell becomes a round dot with a
    soft glow, on a transparent ground (the piece's dark panel shows between the dots)."""
    img, den = canvas(w_m, h_m)
    # WHY drawn on a canvas three times wider, then squeezed: a line longer than its panel would otherwise be cut
    # off at the edge before it could be fitted.
    mask = Image.new('L', (img.width * 3, img.height), 0)
    md = ImageDraw.Draw(mask)
    # WHY the Japanese 1.5x taller: at the Latin cap height a kanji gets too few LED rows to read.
    f_lat, f_ja = fitted(LATIN, cap_m, den), fitted(JAPANESE, min(cap_m * 1.5, h_m * 0.85), den, 500)
    x, cy = lpx(0.03, den), img.height // 2
    for ch in body:
        f = f_lat if ch.isascii() else f_ja
        md.text((x, cy), ch, font=f, fill=255, anchor='lm')
        x += f.getlength(ch) + lpx(cap_m * 0.15, den)
    x += lpx(0.03, den)   # the same margin on the right as on the left
    mask = mask.crop((0, 0, max(int(x), img.width), img.height)).resize(img.size, Image.LANCZOS)
    pitch = lpx(LED_PITCH, den)
    # WHY by cell average: Michroma's thin strokes fall between point samples; a cell lights when enough of it is ink.
    cols, rows = img.width // pitch, img.height // pitch
    cells = mask.resize((cols, rows), Image.BOX)
    dot = Image.new('RGBA', img.size, (0, 0, 0, 0))
    dd = ImageDraw.Draw(dot)
    r = pitch * LED_DOT / 2
    for j in range(rows):
        for i in range(cols):
            if cells.getpixel((i, j)) > 60:
                gx, gy = i * pitch + pitch / 2, j * pitch + pitch / 2
                dd.ellipse((gx - r, gy - r, gx + r, gy + r), fill=LED_ON + (255,))
    glow = dot.filter(ImageFilter.GaussianBlur(pitch * 0.6))
    img.alpha_composite(glow)
    img.alpha_composite(dot)
    return save_label(img, name)


def prop_label(name, body, w_m, h_m, cap_m, fill=NAVY):
    """A small printed label on a prop: M PLUS 1 bold, left aligned, fitted to the label's width."""
    img, den = canvas(w_m, h_m)
    d = ImageDraw.Draw(img)
    f = fitted(JAPANESE, cap_m, den, 700, body, img.width)
    d.text((0, img.height // 2), body, font=f, fill=fill, anchor='lm')
    return save_label(img, name)


def labels():
    out = [column_label('Column_Graphic'), column_label('Column_Graphic_Tall')]
    out.append(company_label('Label_Desk_Reception', 1.6, 0.36, 0.14, 'AONAGI CORPORATE SERVICES', 0.03))
    out.append(company_label('Label_Logo_Aonagi', 1.6, 0.4, 0.16, 'AONAGI', 0.06))
    out.append(led_label('Label_LED_Reception', '受付  1F  RECEPTION', 1.12, 0.16, 0.09))
    out.append(led_label('Label_Lift_1F', '1F', 1.12, 0.28, 0.16))
    out.append(led_label('Label_Lift_RF', 'RF  屋上', 1.12, 0.28, 0.16))
    out.append(prop_label('Label_Vending', '自動販売機', 0.6, 0.09, 0.05, WHITE))
    out.append(prop_label('Label_Slipper', '室内履き', 0.5, 0.07, 0.04))
    out.append(prop_label('Label_Water', '給水', 0.16, 0.08, 0.05))
    return out


def label_sheet(items):
    """Every label on a mid-grey ground (so the white and LED ones show), for review."""
    pad = 30
    scale = [min(1.0, 700 / max(i.width, i.height)) for i in items]
    imgs = [i.resize((max(1, int(i.width * s)), max(1, int(i.height * s)))) for i, s in zip(items, scale)]
    w = sum(i.width for i in imgs) + pad * (len(imgs) + 1)
    h = max(i.height for i in imgs) + 2 * pad
    sheet = Image.new('RGB', (w, h), (0x9A, 0xA6, 0xB4))
    x = pad
    for i in imgs:
        sheet.paste(i, (x, pad), i)
        x += i.width + pad
    sheet.save(os.path.join(HERE, 'review', 'label_sheet.png'))




# ---------------------------------------------------------------- lightboxes (2026-11-15, PANLINE reference)
# Backlit graphics for the kit's Lightbox_Wall: drawn soft (no type smaller than 8 cm), since they glow and bloom.
LIGHTBOX_INNER = (3.48, 2.88)   # the lightbox diffuser, metres (kit LIGHTBOX_W/Y less the frame)
LIGHTBOX_PX_PER_M = 160         # soft graphic: fewer texels than type needs
SEAL = (0xC7, 0x47, 0x3E)       # the hanko red, the palette's one red


def waves(img, den, bands, seed_phase=0.0):
    """Flowing translucent bands across the image: each is the area between two sine curves, filled with its colour,
    with a thin white highlight along its upper edge. bands = [(centre_m, thickness_m, amplitude_m, wavelength_m,
    phase, rgba)]."""
    import math
    W, H = img.size
    for c, th, amp, wl, ph, rgba in bands:
        layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        top, bot = [], []
        for i in range(0, W + 8, 8):
            xm = i / (den * SUPERSAMPLE)
            y = c + amp * math.sin(2 * math.pi * xm / wl + ph + seed_phase)
            t2 = th * (0.75 + 0.25 * math.sin(2 * math.pi * xm / (wl * 1.7) + ph))
            top.append((i, H - lpx(y + t2 / 2, den)))
            bot.append((i, H - lpx(y - t2 / 2, den)))
        d.polygon(top + bot[::-1], fill=rgba)
        d.line(top, fill=(255, 255, 255, 170), width=max(2, lpx(0.012, den)))
        img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(lpx(0.006, den))))


def gel(img, den, share=0.42, alpha=70):
    """The Aero gel gloss: a white wash over the top share of the panel, with a soft curved lower edge."""
    W, H = img.size
    layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse((-W * 0.3, -H * 0.9, W * 1.3, H * share), fill=(255, 255, 255, alpha))
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(lpx(0.04, den))))


def lightbox_wave():
    w_m, h_m = LIGHTBOX_INNER
    img, den = canvas(w_m, h_m, LIGHTBOX_PX_PER_M)
    img.paste(gradient(img.size, (0xEC, 0xF9, 0xFF), (0x7C, 0xD3, 0xE3)), (0, 0))
    waves(img, den, [
        (1.05, 0.55, 0.22, 3.2, 0.0, COBALT + (210,)),
        (1.35, 0.22, 0.28, 2.6, 1.3, AQUA + (200,)),
        (0.65, 0.35, 0.18, 4.1, 2.4, COBALT_DEEP + (190,)),
        (1.75, 0.08, 0.3, 2.2, 0.7, (255, 255, 255, 200)),
    ])
    gel(img, den)
    return save_label(img, 'Lightbox_Wave')


def lightbox_logo():
    """The reception's logo wall: cobalt to aqua, the AONAGI ring with its hanko dot, the company name in both
    scripts and the reception line, all in white over two quiet waves."""
    import math
    w_m, h_m = LIGHTBOX_INNER
    img, den = canvas(w_m, h_m, LIGHTBOX_PX_PER_M)
    W, H = img.size
    base = gradient((H, W), COBALT, AQUA).rotate(90, expand=True).resize((W, H))   # left-to-right gradient
    img.paste(base, (0, 0))
    waves(img, den, [(0.55, 0.3, 0.12, 3.4, 0.4, (255, 255, 255, 45)), (0.4, 0.12, 0.15, 2.5, 2.0, (255, 255, 255, 60))])
    d = ImageDraw.Draw(img)
    # The ring: an open white circle, its gap at the lower right where the red seal dot sits.
    cx, cy, r, t = lpx(0.75, den), H - lpx(1.75, den), lpx(0.42, den), lpx(0.07, den)
    d.arc((cx - r, cy - r, cx + r, cy + r), start=60, end=360 + 10, fill=WHITE, width=t)
    a = math.radians(35)
    dr = lpx(0.075, den)
    dx, dy = cx + r * 0.93 * math.cos(a), cy + r * 0.93 * math.sin(a)
    d.ellipse((dx - dr, dy - dr, dx + dr, dy + dr), fill=SEAL)
    x0 = lpx(1.4, den)
    fj = fitted(JAPANESE, 0.26, den, 800, COMPANY_JA, W - x0 - lpx(0.1, den))
    d.text((x0, H - lpx(1.82, den)), COMPANY_JA, font=fj, fill=WHITE, anchor='ls')
    fe = fitted(LATIN, 0.16, den)
    x = x0
    for ch in 'AONAGI':
        d.text((x, H - lpx(1.42, den)), ch, font=fe, fill=WHITE, anchor='ls')
        x += fe.getlength(ch) + lpx(0.16 * 0.6, den)
    d.rectangle((x0, H - lpx(1.3, den), W - lpx(0.25, den), H - lpx(1.29, den)), fill=WHITE)
    fr = fitted(LATIN, 0.08, den)
    fjr = fitted(JAPANESE, 0.1, den, 700)
    d.text((x0, H - lpx(1.1, den)), '受付', font=fjr, fill=WHITE, anchor='ls')
    x = x0 + fjr.getlength('受付') + lpx(0.1, den)
    for ch in '1F  RECEPTION':
        d.text((x, H - lpx(1.1, den)), ch, font=fr, fill=WHITE, anchor='ls')
        x += fr.getlength(ch) + lpx(0.08 * TRACKING, den)
    gel(img, den, 0.36, 55)
    return save_label(img, 'Lightbox_Logo')


if __name__ == '__main__':
    signs = [zone_sign(k) for k in ZONES]
    spec_sheet(signs)
    label_sheet(labels() + [lightbox_wave(), lightbox_logo()])
    print('wrote', len(signs), 'zone signs, the labels, review/sign_spec.png and review/label_sheet.png to', HERE)
