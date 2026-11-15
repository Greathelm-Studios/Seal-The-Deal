"""Seal the Deal: shared SVG library for the VectorCraft UI mockups.

Direction (locked): "Survey" on Paper & Hairline surfaces, reworked 2026-11-12 to the owner's UI guide
(ArtSource/References/ui/): sky and paper white are the two fields, sky sits inside frames (window panes,
strips), glass is light and see-through, nothing is a dark panel.
Sources: ArtSource/References/ui/README.md, ArtSource/UI/research/*.md, art bible sections 03, 13, 14, 20.
Game rules (owner, 2026-11-12): two rival firms ("Your firm" / "Rival firm", names undecided), ONE
shared briefcase; nobody climbs above the highest floor it has been carried onto ("open floor"); only
the carrier opens the next floor; the firm that carries it onto a roof wins.

Usage (from a generator in ArtSource/UI/tools/):

    import ctd_ui as ui
    body = ui.backdrop("office_sun") + ui.hud_chip(48, 48, "12:34")
    ui.write("ArtSource/UI/svg/hud/Clock.svg", ui.group("Clock", body))
    png = ui.preview("ArtSource/UI/svg/hud/Clock.svg")          # exact 1920 x 1080 PNG

Every component takes absolute artboard coordinates (1920 x 1080 reference) and draws at 1:1.
Rules every caller keeps:
  * Colours only from the token table below. No other hex anywhere; light glass is a swatch at an alpha.
  * Corner radius 0. No <circle>, <ellipse> or rx: nothing rounded (discs and dots are stepped / square).
  * No dark panels. Text over the 3D world sits on a white plate (kind "plate") or frosted-white glass,
    in navy (or is two-tone: light core + 1 px near-black edge, the crosshair).
  * Amber is the rival and threat colour only, and never text: navy text on a SOLID amber plate (kind "amber"),
    or an amber mark with a navy keyline beside navy text. Cyan is a mark with a navy keyline, never text on light.
  * Primary buttons are cobalt with a Paper White label, never amber.
  * Anything read mid-action is >= 16 px at 1920 x 1080; thin weights only on the title and big numerals.
  * One traced object (trace_brackets cluster) per screen at most, never touching text.
  * Families: Kohinoor Bangla Light (display), Helvetica Neue (+ UltraLight numerals), JetBrains Mono.
    Glyphs a family lacks (diamond, triangle, arrows) are drawn as paths, never typed.
  * Legacy kinds "smoked" / "cobalt" / "frosted" draw exactly as before; pass A only keeps cobalt (primary
    button, active tab) and frosted (menu panes) of them.
"""

from __future__ import annotations

import math
import os
import random
import re
import shutil
import struct
import subprocess
import sys
import tempfile

# ---------------------------------------------------------------------------
# 1. Tokens
# ---------------------------------------------------------------------------
# Contrast ratios are WCAG 2.x, computed by contrast_table() (make_kit.py prints it).
# "Engine" = Unity's linear-light blend (ProjectSettings: Linear colour space), which is what ships.
# The worst background for light text is sun bloom clipped to pure white (BLOOM). BLOOM is a
# measurement input only: it is never painted. The light core for text is Paper White.

# Bible section 03 swatches
PAPER = "#F3F4F4"      # Paper White: fields, light text core. navy on it 15.40
SILVER = "#C9CED3"     # Silver Grey: quiet panels, secondary text on dark. never text on paper (1.44)
NAVY = "#111B38"       # Deep Navy: type, smoked glass, keylines. paper on it 15.40
COBALT = "#244FB9"     # Corporate Cobalt: primary buttons, brand blocks. paper on it 6.59
ELECTRIC = "#2879EF"   # Electric Blue: rules, hover underline, selection. on paper 3.76: marks only
CYAN = "#20D4E6"       # Cyan: "you" diamond, status squares, focus on dark. never on light (1.64)
BLACK = "#101116"      # Near Black: crosshair outline, max contrast
# The one warm hue (proposal): rival firm AND threat / warning. Never a button fill.
AMBER = "#FF9E1B"      # navy text on a solid amber plate 8.21. Never text itself (on paper 1.83)

GAME_TITLE = "SEAL THE DEAL"   # the game's title as set in the lockup (the owner renamed the game: never the old name)
GAME_NAME = "Seal the Deal"     # the name in running text

LIGHT = PAPER          # light core for text and highlights on dark and glass (pure white is never painted)
BLOOM = "#%02X%02X%02X" % (255, 255, 255)  # measurement only: clipped sun bloom. Built, not a literal

PALETTE = [  # (token, hex, name, role) for the kit sheet
    ("PAPER", PAPER, "Paper White", "Fields, frames, plates"),
    ("SILVER", SILVER, "Silver Grey", "Clear glass tint, reveals"),
    ("NAVY", NAVY, "Deep Navy", "Type, keylines, marks"),
    ("COBALT", COBALT, "Corporate Cobalt", "Primary buttons, sky"),
    ("ELECTRIC", ELECTRIC, "Electric Blue", "Rules, hover, low sky"),
    ("CYAN", CYAN, "Cyan", "You: a mark, navy keyline"),
    ("BLACK", BLACK, "Near Black", "Crosshair edge, one line"),
    ("AMBER", AMBER, "Amber", "Rival, threat: a plate"),
]

# Derived tokens: a swatch at an alpha, never a new hex.
# Alpha text tokens are for the mockups. In Unity (Linear) vertex alpha blends lighter (ink-3 on paper would
# be 2.75), so INK2 / INK3 / DISABLED ship as the OPAQUE sRGB composite over their surface.
INK2 = (NAVY, 0.78)         # secondary text on paper: 7.88
INK3 = (NAVY, 0.68)         # microtype on paper, the lightest text allowed: 5.69
DISABLED = (NAVY, 0.45)     # disabled label on paper: 2.83 (WCAG-exempt; always with a dashed outline)
HAIR = (NAVY, 0.24)         # hairline rules / grid on light: decorative
HAIR_STRONG = (NAVY, 0.40)  # field borders on light: decorative frame, the label carries meaning
HAIR_DARK = (LIGHT, 0.28)   # hairline on smoked glass: decorative
HOVER_SHADE = (NAVY, 0.20)  # laid over cobalt for primary hover: paper label 6.10 over bloom

# Glass: engine alpha (Unity, linear blend). The SVG alpha that gives the same composite over bloom is
# derived below with svg_alpha_for(), so it can never drift from the engine value.
SMOKED_A = 0.95    # raised from 0.92 so paper text passes inside band + streak (4.90); silver 5.92
COBALT_A = 0.96    # paper label 5.29; in the 3 % top band 4.68. Cobalt carries no diagonal streak
FROSTED_A = 0.86   # navy text >= 13.3 on every background. Paper on frosted is NOT a used pair (1.02)
WARNING_A = 1.00   # amber on solid navy 8.21 (legacy smoked warning chip)
# Light glass (pass A since the 2026-11-12 guide). Engine (Linear) alphas, like the kinds above.
PLATE_A = 0.90     # white plate: paper at 90 %, navy text >= 13 on every backdrop. The HUD's surface
CLEAR_A = 0.40     # clear blue-grey glass: silver at 40 %, see-through. A frame, never a text surface
AMBER_A = 1.00     # amber plate: solid, navy text 8.21. Rival and threat only
PROOF_KINDS = ("plate", "frosted", "clear", "amber")   # the kinds make_kit's A3 proves (a theme sets its own)
NO_TEXT_KINDS = ("clear",)   # kinds that never carry text (A3 shows them as frames)
HUD_GLASS = "plate"    # the kind HUD components draw on (a theme may set "smoked" for the old dark chips)
WARN_GLASS = "amber"   # the kind warnings draw on (theme: "smoked" + WARNING_A for the old navy chip)

SHEEN_BAND = {"smoked": 0.06, "cobalt": 0.03}            # top gradient, white -> 0 (30 % / 50 % of h)
STREAK = {"smoked": 0.04, "cobalt": 0.0, "frosted": 0.35}  # hard diagonal sheen streak alpha (0 = none)

# Type roles. Installed stand-ins (shipping targets in brackets; research/fonts_by_pass.md).
DISPLAY = "Kohinoor Bangla"            # Kohinoor Bangla Light (Saira Expanded Light / ExtraLight): rounded-square
UI_FACE = "Helvetica Neue"             # Helvetica Neue / Medium / Bold (Archivo SemiExpanded): 400 / 500 / 700
UI_THIN = "Helvetica Neue UltraLight"  # (Archivo Expanded ExtraLight): oversized numerals >= 36 px only
DATA = "JetBrains Mono"                # JetBrains Mono Light / Regular (IBM Plex Mono Light / Regular): 300 / 400
DISPLAY_WEIGHT = 300                   # the display face's one weight (a theme that swaps DISPLAY gets 500 back)
TITLE_TRACK = 0.22                     # title letter-spacing in em: one large, thin, WIDE title (legacy 0.06)
FORCED_WEIGHT = {DISPLAY: DISPLAY_WEIGHT, UI_THIN: 400}  # families drawn in a single weight
TARGET_FACE = {DISPLAY: "Saira Expanded", UI_FACE: "Archivo SemiExpanded",
               UI_THIN: "Archivo Expanded ExtraLight", DATA: "IBM Plex Mono"}

TYPE = {  # role: (family, weight, size px, letter-spacing px)
    "micro": (DATA, 300, 11, 0.8),       # legal microtype: decorative, never needed to act
    "caption": (DATA, 400, 13, 1.5),     # kit captions, field codes (not for mid-action reading)
    "label": (UI_FACE, 500, 16, 0),      # field labels, HUD captions: the minimum read size
    "body": (UI_FACE, 400, 18, 0),       # menu body, descriptions
    "item": (UI_FACE, 500, 20, 0),       # buttons, menu items, settings rows
    "hud": (DATA, 400, 28, 0),           # clock, ammo
    "heading": (DISPLAY, DISPLAY_WEIGHT, 40, 6),   # screen titles, caps, tracked wide
    "title": (DISPLAY, DISPLAY_WEIGHT, 104, 23),   # GAME_TITLE (title_lockup tracks it at TITLE_TRACK em)
    "floor": (UI_THIN, 400, 240, -4),    # oversized floor numerals
}

SPACE = {"xxs": 4, "xs": 8, "s": 12, "m": 16, "l": 24, "xl": 32, "xxl": 48, "huge": 64}
SAFE = 48                 # HUD safe margin at 1920 x 1080
GRID = 32                 # hairline grid step
SIZE = {
    "button_primary_h": 64, "button_secondary_h": 56, "button_min_w": 360,
    "tab_h": 56, "row_h": 64, "chip_h": 48, "toggle": 22, "slider_track": 200,
    "value_field_w": 72, "keycap": 48, "slot_w": 260, "slot_h": 72,
}

FLOORS = 12   # ladder segments: lobby (0), office floors 1-9, boardroom (10), roof (11). Owner + GDD; TOWER.md
              # still says 15 with separate checkpoint floors (doc mismatch: the GDD wins for the mockups)

ITEMS = {  # owner's item list (2026-11-12): key -> (name, default ammo / count / action text)
    "pistol": ("Pistol", "12 / 48"),
    "ar15": ("AR-15", "30 / 90"),
    "grenade_briefcase": ("Grenade briefcase", "× 2"),
    "office_chair": ("Office chair", "Throw"),
    "cubicle_panel": ("Cubicle panel", "Throw"),
    "briefcase": ("Briefcase", "Carrying"),
}

# ---------------------------------------------------------------------------
# 2. Colour maths
# ---------------------------------------------------------------------------


def tok(c):
    """Resolve a theme token reference: "@NAVY" -> the current theme's NAVY; ("@NAVY", 0.5) -> (hex, 0.5).
    Anything else is returned unchanged. Defaults use these references so they follow set_theme()."""
    if isinstance(c, str) and c.startswith("@"):
        return globals()[c[1:]]
    if isinstance(c, tuple) and c and isinstance(c[0], str) and c[0].startswith("@"):
        return (globals()[c[0][1:]],) + tuple(c[1:])
    return c


def _rgb(h):
    h = tok(h).lstrip("#")
    return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]


def _lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def _unlin(c):
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


def composite(fg, alpha, bg, linear=True):
    """fg at alpha over bg -> hex. linear=True is Unity (Linear colour space); False is SVG / sRGB."""
    f, b = _rgb(fg), _rgb(bg)
    if linear:
        out = [_unlin(alpha * _lin(x) + (1 - alpha) * _lin(y)) for x, y in zip(f, b)]
    else:
        out = [alpha * x + (1 - alpha) * y for x, y in zip(f, b)]
    return "#" + "".join(f"{round(max(0, min(1, c)) * 255):02X}" for c in out)


def luminance(h):
    r, g, b = [_lin(c) for c in _rgb(h)]
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a, b):
    la, lb = luminance(a), luminance(b)
    return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)


def svg_alpha_for(fg, engine_alpha, bg="@BLOOM"):
    """sRGB alpha whose composite over bg matches the engine's linear composite (bisection)."""
    target = luminance(composite(fg, engine_alpha, bg, True))
    lo, hi = 0.0, 1.0
    darker = luminance(fg) < luminance(bg)
    for _ in range(40):
        m = (lo + hi) / 2
        lum = luminance(composite(fg, m, bg, False))
        if (lum > target) == darker:
            lo = m
        else:
            hi = m
    return round((lo + hi) / 2, 3)


SMOKED_SVG = svg_alpha_for(NAVY, SMOKED_A)     # 0.81
COBALT_SVG = svg_alpha_for(COBALT, COBALT_A)   # 0.91
FROSTED_SVG = svg_alpha_for(PAPER, FROSTED_A)  # 0.79
PLATE_SVG = svg_alpha_for(PAPER, PLATE_A)
CLEAR_SVG = svg_alpha_for(SILVER, CLEAR_A)

GLASS = {  # kind: (fill, engine alpha, svg alpha, text colour, text name)
    "smoked": (NAVY, SMOKED_A, SMOKED_SVG, LIGHT, "paper"),     # legacy dark chip: kept, pass A no longer draws it
    "cobalt": (COBALT, COBALT_A, COBALT_SVG, LIGHT, "paper"),   # primary button, active tab
    "frosted": (PAPER, FROSTED_A, FROSTED_SVG, NAVY, "navy"),   # menu panes over the scene (blur under)
    "plate": (PAPER, PLATE_A, PLATE_SVG, NAVY, "navy"),         # white plate: every HUD chip, prompt, slot
    "clear": (SILVER, CLEAR_A, CLEAR_SVG, NAVY, "navy"),        # clear blue-grey glass: frames and panes, no text
    "amber": (AMBER, AMBER_A, AMBER_A, NAVY, "navy"),           # solid amber plate: rival / threat, navy text
}
LIGHT_KINDS = ("frosted", "plate", "clear", "amber")   # kinds that take navy text

# Pixels a pane can sit over in each painted backdrop (engine ratios take the worst of them).
PROOF_BG = {"office_sun": (PAPER, SILVER, COBALT, NAVY),
            "sky_bloom": (BLOOM, PAPER, ELECTRIC, COBALT),
            "navy_dark": (NAVY, BLACK, COBALT)}
WORST_BACKDROP = "sky_bloom"


def glass_ratio(kind, backdrop_kind, txt=None, linear=True):
    """Contrast of the kind's text colour (or txt: a hex, or (hex, alpha) shipped as its opaque composite) on the
    pane, worst pixel of that backdrop. linear=True is the engine (Linear); False is the SVG at its SVG alpha."""
    fill, a, sa, t, _ = GLASS[kind]
    txt = txt or t
    worst = None
    for bg in PROOF_BG[backdrop_kind]:
        surf = composite(fill, a if linear else sa, bg, linear)
        tc = composite(txt[0], txt[1], surf, False) if isinstance(txt, tuple) else txt
        r = contrast(tc, surf)
        worst = r if worst is None else min(worst, r)
    return worst


def contrast_table():
    """Rows of dict(pair, engine, svg, used, need, note): every glass text pair over the worst backgrounds.
    used=False rows are pairs the system never draws (kept to show why). need = pass mark (4.5 text,
    3.0 non-text marks). make_kit.py prints FAIL for any used row under its mark."""
    rows = []
    bgs = (("bloom", BLOOM), ("cobalt sky", COBALT), ("electric sky", ELECTRIC), ("navy suit", BLACK))

    def add(pair, txt, surf_eng, surf_svg, used=True, need=4.5, note=""):
        """txt: a hex, or (NAVY, alpha) for an alpha text token. Alpha text is shipped as the OPAQUE sRGB
        composite (what the mockup shows); drawn as vertex alpha in Linear it would come out lighter."""
        if isinstance(txt, tuple):
            t_e, t_s = composite(txt[0], txt[1], surf_eng, False), composite(txt[0], txt[1], surf_svg, False)
        else:
            t_e = t_s = txt
        rows.append(dict(pair=pair, engine=contrast(t_e, surf_eng), svg=contrast(t_s, surf_svg),
                         used=used, need=need, note=note))

    def glass(kind, bg, linear):
        fill, a, sa, _, _ = GLASS[kind]
        return composite(fill, a if linear else sa, bg, linear)

    def lift(c, a, linear=True):
        return composite(LIGHT, a, c, linear)

    # every light kind and the cobalt button over the four worst single colours
    for kind, txt, tname, used, need, note in (
            ("plate", NAVY, "navy", True, 4.5, "HUD chips, prompts, tracker, slot, banners"),
            ("plate", INK2, "ink-2 (opaque)", True, 4.5, "secondary HUD text; ship opaque"),
            ("amber", NAVY, "navy", True, 4.5, "rival / threat plate, solid"),
            ("frosted", NAVY, "navy", True, 4.5, "menu panes"),
            ("frosted", INK2, "ink-2 (opaque)", True, 4.5, "ship as an opaque colour, not alpha"),
            ("cobalt", LIGHT, "paper", True, 4.5, "primary button, active tab"),
            ("clear", NAVY, "navy", False, 4.5, "never: clear glass is a frame; text sits on a plate in it"),
            ("plate", LIGHT, "paper", False, 4.5, "never: plates take navy text"),
            ("plate", AMBER, "amber", False, 4.5, "never: amber is never text"),
            ("smoked", LIGHT, "paper", False, 4.5, "legacy kind: pass A no longer draws it"),
            ("smoked", SILVER, "silver", False, 4.5, "legacy kind: pass A no longer draws it")):
        a = GLASS[kind][1]
        for bgname, bg in bgs:
            se, ss = glass(kind, bg, True), glass(kind, bg, False)
            add(f"{tname} on {kind} {int(a * 100)}% over {bgname}", txt, se, ss, used, need, note)
    # the same pairs at the worst pixel of the two painted worst-case backdrops (what the boards show)
    for kind, txt, tname, used, note in (
            ("plate", NAVY, "navy", True, "HUD chip / tracker / prompt value"),
            ("plate", INK2, "ink-2", True, "HUD secondary line"),
            ("amber", NAVY, "navy", True, "rival tracker, warnings, refusal"),
            ("frosted", NAVY, "navy", True, "menu pane"),
            ("cobalt", LIGHT, "paper", True, "primary button"),
            ("clear", NAVY, "navy", False, "never: frame only")):
        for bk in ("sky_bloom", "navy_dark", "office_sun"):
            rows.append(dict(pair=f"{tname} on {kind} · {bk} worst px", engine=glass_ratio(kind, bk, txt),
                             svg=glass_ratio(kind, bk, txt, linear=False), used=used, need=4.5, note=note))
    # marks (non-text, 3:1): the navy keyline carries cyan and amber marks on light
    p_e, p_s = glass("plate", BLOOM, True), glass("plate", BLOOM, False)
    pn_e, pn_s = glass("plate", BLACK, True), glass("plate", BLACK, False)
    add("navy keyline of a cyan / amber mark on plate over bloom", NAVY, p_e, p_s, need=3.0, note="non-text")
    add("navy keyline of a cyan / amber mark on plate over navy suit", NAVY, pn_e, pn_s, need=3.0, note="non-text")
    add("cobalt rule / ladder on plate over bloom", COBALT, p_e, p_s, need=3.0, note="non-text")
    add("cyan fill alone on plate over bloom", CYAN, p_e, p_s, used=False, need=3.0,
        note="never alone: a cyan mark always has its navy keyline")
    add("navy hazard / key on amber plate", NAVY, AMBER, AMBER, note="key-conflict cell, out of ammo")
    c_e, c_s = glass("cobalt", BLOOM, True), glass("cobalt", BLOOM, False)
    add("paper in cobalt top band over bloom", LIGHT, lift(c_e, SHEEN_BAND["cobalt"]),
        lift(c_s, SHEEN_BAND["cobalt"], False))
    add("paper on cobalt + hover shade over bloom", LIGHT, composite(NAVY, HOVER_SHADE[1], c_e, True),
        composite(NAVY, HOVER_SHADE[1], c_s, False))
    add("paper on solid cobalt (primary pressed)", LIGHT, COBALT, COBALT)
    add("navy on paper + 12 % navy (pressed)", NAVY, composite(NAVY, 0.12, PAPER, False),
        composite(NAVY, 0.12, PAPER, False))
    worst_f = glass("frosted", BLACK, True)
    rows.append(dict(pair="ink-2 as 78 % vertex alpha (Linear) on frosted", used=False, need=4.5,
                     engine=contrast(composite(NAVY, INK2[1], worst_f, True), worst_f),
                     svg=contrast(composite(NAVY, INK2[1], worst_f, False), worst_f),
                     note="never: alpha text blends lighter in Linear; bake INK2 / INK3 opaque"))
    add("amber on warning chip (solid navy)", AMBER, NAVY, NAVY, used=False, note="legacy: pass A sets no amber text")
    return rows

# ---------------------------------------------------------------------------
# 3. Font metrics (measured from the installed font files, stdlib only)
# ---------------------------------------------------------------------------
# PIL is not installed on this Mac, so TrueType advance widths are read with struct from the system
# font files (cmap + hmtx). No kerning, so expect +-1 %. Falls back to a per-family estimate if a file
# cannot be read. METRICS_SOURCE says which one is live. Missing glyphs are collected in MISSING_GLYPHS
# (a character a family lacks would silently render in a fallback font).

FONT_FILES = {  # pinned faces (exact full names); any other installed family is found by _font_index()
    (DISPLAY, 300): ("/System/Library/Fonts/KohinoorBangla.ttc", "Kohinoor Bangla Light"),
    ("Futura", 500): ("/System/Library/Fonts/Supplemental/Futura.ttc", "Futura Medium"),  # pass A before 2026-11-12
    (UI_FACE, 400): ("/System/Library/Fonts/HelveticaNeue.ttc", "Helvetica Neue"),
    (UI_FACE, 500): ("/System/Library/Fonts/HelveticaNeue.ttc", "Helvetica Neue Medium"),
    (UI_FACE, 700): ("/System/Library/Fonts/HelveticaNeue.ttc", "Helvetica Neue Bold"),
    (UI_THIN, 400): ("/System/Library/Fonts/HelveticaNeue.ttc", "Helvetica Neue UltraLight"),
    (DATA, 300): ("~/Library/Fonts/JetBrainsMono-Light.ttf", None),
    (DATA, 400): ("~/Library/Fonts/JetBrainsMono-Regular.ttf", None),
    (DATA, 500): ("~/Library/Fonts/JetBrainsMono-Medium.ttf", None),
}
_FONT_FILES = FONT_FILES  # old private name, kept for importers
FONT_DIRS = ("/System/Library/Fonts", "/System/Library/Fonts/Supplemental", "/Library/Fonts", "~/Library/Fonts")
_W = {DISPLAY: 0.58, "Futura": 0.58, UI_FACE: 0.55, DATA: 0.60, UI_THIN: 0.50}  # fallback em-per-char estimate (else 0.56)
_FACES = {}
_INDEX = []              # [(family_lower, weight, italic, path, offset, fullname)]
METRICS_SOURCE = "font files"
MISSING_GLYPHS = set()   # (family, char)
UNMEASURED = set()       # families with no installed font file: widths are estimates, logged once


def _ttf_tables(data, off):
    n = struct.unpack(">H", data[off + 4:off + 6])[0]
    t = {}
    for i in range(n):
        tag, _, o, ln = struct.unpack(">4sIII", data[off + 12 + 16 * i: off + 28 + 16 * i])
        t[tag.decode("latin-1")] = (o, ln)
    return t


def _ttf_names(data, t, ids=(1, 2, 4, 16, 17)):
    """{nameID: string} from the name table, Windows English preferred."""
    o, _ = t["name"]
    _, count, so = struct.unpack(">HHH", data[o:o + 6])
    out, rank = {}, {}
    for i in range(count):
        pid, _, lid, nid, ln, no = struct.unpack(">6H", data[o + 6 + 12 * i:o + 18 + 12 * i])
        if nid not in ids:
            continue
        raw = data[o + so + no:o + so + no + ln]
        s = raw.decode("utf-16-be", "replace") if pid in (0, 3) else raw.decode("mac_roman", "replace")
        r = 0 if (pid == 3 and lid == 0x409) else 1 if pid in (0, 3) else 2
        if nid not in rank or r < rank[nid]:
            out[nid], rank[nid] = s, r
    return out


def _ttf_fullname(data, t):
    return _ttf_names(data, t, (4,)).get(4)


def _ttf_cmap(data, t):
    o, _ = t["cmap"]
    n = struct.unpack(">H", data[o + 2:o + 4])[0]
    subs = []
    for i in range(n):
        _, _, so = struct.unpack(">HHI", data[o + 4 + 8 * i:o + 12 + 8 * i])
        fmt = struct.unpack(">H", data[o + so:o + so + 2])[0]
        subs.append((fmt == 12, fmt == 4, o + so))
    subs.sort(reverse=True)
    m = {}
    for is12, is4, so in subs:
        if is12:
            ng = struct.unpack(">I", data[so + 12:so + 16])[0]
            for k in range(ng):
                a, b, g = struct.unpack(">III", data[so + 16 + 12 * k:so + 28 + 12 * k])
                for c in range(a, b + 1):
                    m[c] = g + c - a
            return m
        if is4:
            segx2 = struct.unpack(">H", data[so + 6:so + 8])[0]
            seg = segx2 // 2
            ends = struct.unpack(f">{seg}H", data[so + 14:so + 14 + segx2])
            st = so + 16 + segx2
            starts = struct.unpack(f">{seg}H", data[st:st + segx2])
            deltas = struct.unpack(f">{seg}h", data[st + segx2:st + 2 * segx2])
            ro = st + 2 * segx2
            ros = struct.unpack(f">{seg}H", data[ro:ro + segx2])
            for i in range(seg):
                for c in range(starts[i], ends[i] + 1):
                    if c == 0xFFFF:
                        continue
                    if ros[i] == 0:
                        g = (c + deltas[i]) & 0xFFFF
                    else:
                        ga = ro + 2 * i + ros[i] + 2 * (c - starts[i])
                        g = struct.unpack(">H", data[ga:ga + 2])[0]
                        g = (g + deltas[i]) & 0xFFFF if g else 0
                    if g:
                        m[c] = g
            return m
    return m


def _font_index():
    """Every installed .ttf / .ttc / .otf face: family names (typographic and legacy, plus full name),
    weight (OS/2 usWeightClass) and italic flag. Built once, from the headers only."""
    if _INDEX:
        return _INDEX
    for d in FONT_DIRS:
        d = os.path.expanduser(d)
        if not os.path.isdir(d):
            continue
        for fn in sorted(os.listdir(d)):
            if not fn.lower().endswith((".ttf", ".ttc", ".otf")):
                continue
            p = os.path.join(d, fn)
            try:
                with open(p, "rb") as fh:
                    data = fh.read()
                offs = [0]
                if data[:4] == b"ttcf":
                    n = struct.unpack(">I", data[8:12])[0]
                    offs = list(struct.unpack(f">{n}I", data[12:12 + 4 * n]))
                for off in offs:
                    t = _ttf_tables(data, off)
                    nm = _ttf_names(data, t)
                    wt, ital = 400, False
                    if "OS/2" in t:
                        oo = t["OS/2"][0]
                        wt = struct.unpack(">H", data[oo + 4:oo + 6])[0]
                        ital = bool(struct.unpack(">H", data[oo + 62:oo + 64])[0] & 1)
                    sub = (nm.get(17) or nm.get(2) or "").lower()
                    ital = ital or "italic" in sub or "oblique" in sub
                    for fam in {nm.get(16), nm.get(1), nm.get(4)} - {None}:
                        _INDEX.append((fam.lower(), wt, ital, p, off, nm.get(4)))
            except (OSError, KeyError, struct.error, IndexError):
                continue
    return _INDEX


def font_file(family, weight=400):
    """(path, ttc offset, full name) of the installed upright face nearest (family, weight), or None."""
    keys = [k for k in FONT_FILES if k[0] == family]
    if keys:
        k = min(keys, key=lambda k: abs(k[1] - weight))
        return os.path.expanduser(FONT_FILES[k][0]), None, FONT_FILES[k][1]
    fam = family.lower()
    cands = [e for e in _font_index() if e[0] == fam]
    if not cands:
        return None
    best = min(cands, key=lambda e: (e[2], abs(e[1] - weight)))
    return best[3], best[4], best[5]


def _face(family, weight):
    """(units_per_em, advances, cmap) for the installed face nearest (family, weight), or None.
    Works for TrueType and CFF (.otf) alike: only head / hhea / hmtx / cmap are read."""
    global METRICS_SOURCE
    key = (family, weight)
    if key in _FACES:
        return _FACES[key]
    face = None
    hit = font_file(family, weight)
    if hit:
        path_, off0, want = hit
        try:
            with open(path_, "rb") as fh:
                data = fh.read()
            offs = [0]
            if data[:4] == b"ttcf":
                n = struct.unpack(">I", data[8:12])[0]
                offs = list(struct.unpack(f">{n}I", data[12:12 + 4 * n]))
            if off0 is not None:
                offs = [off0]
            for off in offs:
                t = _ttf_tables(data, off)
                if want and off0 is None and _ttf_fullname(data, t) != want:
                    continue
                upem = struct.unpack(">H", data[t["head"][0] + 18:t["head"][0] + 20])[0]
                nh = struct.unpack(">H", data[t["hhea"][0] + 34:t["hhea"][0] + 36])[0]
                ho = t["hmtx"][0]
                adv = [struct.unpack(">H", data[ho + 4 * i:ho + 4 * i + 2])[0] for i in range(nh)]
                face = (upem, adv, _ttf_cmap(data, t))
                break
        except (OSError, KeyError, struct.error):
            face = None
    if face is None:
        if family not in UNMEASURED:
            UNMEASURED.add(family)
            sys.stderr.write(f"ctd_ui: no installed font file for family '{family}' (weight {weight}); "
                             "text widths for it are estimates\n")
        METRICS_SOURCE = "estimate for: " + ", ".join(sorted(UNMEASURED))
    _FACES[key] = face
    return face


def _resolve(role, size=None, family=None, weight=None, ls=None):
    fam, wt, sz, sp = TYPE[role]
    fam, wt, sz = family or fam, weight or wt, size or sz
    sp = sp if ls is None else ls
    if fam in FORCED_WEIGHT:   # faces installed in one weight only (survey: Futura 500, UltraLight 400)
        wt = FORCED_WEIGHT[fam]
    return fam, wt, sz, sp


def glyphs_missing(s, family, weight=400):
    """Characters of s that the installed family lacks (they would render in a fallback font)."""
    face = _face(family, weight)
    if face is None:
        return []
    return [c for c in s if c != " " and ord(c) not in face[2]]


def text_width(s, role="item", size=None, family=None, weight=None, ls=None):
    """Advance width in px of s set in a type role, measured from the installed font file
    (letter-spacing counted between characters). Falls back to an estimate if the file is unreadable."""
    fam, wt, sz, sp = _resolve(role, size, family, weight, ls)
    face = _face(fam, wt)
    n = len(s)
    if face is None:
        k = _W.get(fam, 0.56) + (0.03 if wt >= 700 else 0)
        return n * sz * k + max(0, n - 1) * sp
    upem, adv, cmap = face
    total = 0
    for c in s:
        g = cmap.get(ord(c))
        if g is None:
            MISSING_GLYPHS.add((fam, c))
            g = 0
        total += adv[min(g, len(adv) - 1)]
    return total * sz / upem + max(0, n - 1) * sp


def wrap(s, width, role="body", size=None, **kw):
    """Greedy word wrap of s into lines no wider than width px (measured)."""
    lines, cur = [], ""
    for word in s.split(" "):
        trial = word if not cur else cur + " " + word
        if not cur or text_width(trial, role, size=size, **kw) <= width:
            cur = trial
        else:
            lines.append(cur)
            cur = word
    if cur:
        lines.append(cur)
    return lines

# ---------------------------------------------------------------------------
# 4. Layout registry (for make_kit's overlap / band / bounds checks)
# ---------------------------------------------------------------------------
# Every primitive records its absolute box here while recording is on. Components draw at absolute
# coordinates, so the boxes are true artboard positions (place() with a transform is not tracked:
# make_kit draws everything at 1:1 without it). Backdrops and clip internals are not recorded.

REC = {"on": True, "shapes": [], "texts": [], "panes": []}


def mark():
    """Registry position; pass to since() after drawing a component to get what it drew."""
    return (len(REC["shapes"]), len(REC["texts"]), len(REC["panes"]))


def since(k):
    return REC["shapes"][k[0]:], REC["texts"][k[1]:], REC["panes"][k[2]:]


def reset_registry():
    globals().get("TRACE_BOXES", []).clear()
    REC["shapes"].clear()
    REC["texts"].clear()
    REC["panes"].clear()


class _quiet:
    """with _quiet(): ... draws without recording (backdrop art, clipped streaks)."""

    def __enter__(self):
        self.prev = REC["on"]
        REC["on"] = False

    def __exit__(self, *a):
        REC["on"] = self.prev


def _reg(bucket, x0, y0, x1, y1, **info):
    if REC["on"]:
        REC[bucket].append(dict(box=(min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)), **info))

# ---------------------------------------------------------------------------
# 5. SVG primitives
# ---------------------------------------------------------------------------

_uid = [0]


def uid(prefix="u"):
    _uid[0] += 1
    return f"{prefix}{_uid[0]}"


def _f(v):
    if isinstance(v, float):
        return f"{v:.2f}".rstrip("0").rstrip(".")
    return str(v)


def _paint(attr, c):
    """c is a hex, a (hex, alpha) token, an "@TOKEN" reference, a url(), or None."""
    c = tok(c)
    if c is None:
        return f' {attr}="none"'
    if isinstance(c, tuple):
        return f' {attr}="{c[0]}" {attr}-opacity="{_f(float(c[1]))}"'
    return f' {attr}="{c}"'


def _hex(c):
    c = tok(c)
    return c[0] if isinstance(c, tuple) else c


def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def rect(x, y, w, h, fill=None, stroke=None, sw=1, opacity=None, dash=None, extra="", r=0):
    """r: corner radius. Pass A keeps 0; a theme with ALLOW_RADIUS = True may pass r > 0 (emits rx / ry)."""
    o = f' opacity="{_f(opacity)}"' if opacity is not None else ""
    if r:
        extra = f' rx="{_f(r)}" ry="{_f(r)}"' + extra
    s = _paint("stroke", stroke) + (f' stroke-width="{_f(sw)}"' if stroke else "")
    d = f' stroke-dasharray="{dash}"' if dash and stroke else ""
    _reg("shapes", x, y, x + w, y + h)
    return f'<rect x="{_f(x)}" y="{_f(y)}" width="{_f(w)}" height="{_f(h)}"{_paint("fill", fill)}{s}{d}{o}{extra}/>'


def line(x1, y1, x2, y2, stroke="@NAVY", sw=1, dash=None, cap="butt"):
    d = f' stroke-dasharray="{dash}"' if dash else ""
    c = f' stroke-linecap="{cap}"' if cap != "butt" else ""
    _reg("shapes", x1, y1, x2, y2)
    return (f'<line x1="{_f(x1)}" y1="{_f(y1)}" x2="{_f(x2)}" y2="{_f(y2)}"'
            f'{_paint("stroke", stroke)} stroke-width="{_f(sw)}"{d}{c}/>')


def poly(points, fill=None, stroke=None, sw=1, opacity=None, dash=None):
    pts = " ".join(f"{_f(float(px))},{_f(float(py))}" for px, py in points)
    o = f' opacity="{_f(opacity)}"' if opacity is not None else ""
    s = _paint("stroke", stroke) + (f' stroke-width="{_f(sw)}" stroke-linejoin="miter"' if stroke else "")
    d = f' stroke-dasharray="{dash}"' if dash and stroke else ""
    xs, ys = [p[0] for p in points], [p[1] for p in points]
    _reg("shapes", min(xs), min(ys), max(xs), max(ys))
    return f'<polygon points="{pts}"{_paint("fill", fill)}{s}{d}{o}/>'


def path(d, fill=None, stroke=None, sw=1, opacity=None):
    """Absolute M / L / H / V / Z path data only (that is all the library emits)."""
    o = f' opacity="{_f(opacity)}"' if opacity is not None else ""
    s = _paint("stroke", stroke) + (f' stroke-width="{_f(sw)}" stroke-linejoin="miter"' if stroke else "")
    xs, ys, cx, cy = [], [], 0.0, 0.0
    for cmd, args in re.findall(r"([MLHVZ])([^MLHVZ]*)", d):
        nums = [float(v) for v in re.findall(r"-?\d+(?:\.\d+)?", args)]
        if cmd in "ML":
            for i in range(0, len(nums) - 1, 2):
                cx, cy = nums[i], nums[i + 1]
                xs.append(cx)
                ys.append(cy)
        elif cmd == "H" and nums:
            cx = nums[-1]
            xs.append(cx)
            ys.append(cy)
        elif cmd == "V" and nums:
            cy = nums[-1]
            xs.append(cx)
            ys.append(cy)
    if xs:
        _reg("shapes", min(xs), min(ys), max(xs), max(ys))
    return f'<path d="{d}"{_paint("fill", fill)}{s}{o}/>'


def text(x, y, s, role="item", fill="@NAVY", size=None, weight=None, family=None,
         anchor="start", ls=None, opacity=None):
    """Text in a type role at baseline (x, y). Futura is forced to 500, UltraLight to 400 (both resolve
    without fallback). Avoid anchor="end" on strings with '·' (VectorCraft quirk): use text_right()."""
    fam, wt, sz, sp = _resolve(role, size, family, weight, ls)
    if anchor == "start" and s.startswith(" "):  # SVG collapses leading spaces: advance x instead
        lead = len(s) - len(s.lstrip(" "))
        x += text_width(" " * lead, role, size=sz, family=fam, weight=wt, ls=sp) + sp
        s = s.lstrip(" ")
    a = f' text-anchor="{anchor}"' if anchor != "start" else ""
    sp_attr = f' letter-spacing="{_f(float(sp))}"' if sp else ""
    fill = tok(fill)
    if isinstance(fill, tuple):  # VectorCraft drops fill-opacity on <text>: carry the alpha as opacity
        opacity = fill[1] * (opacity if opacity is not None else 1)
        fill_attr = f' fill="{fill[0]}"'
    else:
        fill_attr = _paint("fill", fill)
    o = f' opacity="{_f(float(opacity))}"' if opacity is not None else ""
    w = text_width(s, role, size=sz, family=fam, weight=wt, ls=sp)
    x0 = x - (w / 2 if anchor == "middle" else w if anchor == "end" else 0)
    asc = 0.72 if fam != DATA else 0.74
    desc = 0.03 if fam == UI_THIN else 0.2   # UltraLight is used for numerals only: no descenders
    _reg("texts", x0, y - asc * sz, x0 + w, y + desc * sz, s=s, fill=fill, family=fam, size=sz)
    _reg("shapes", x0, y - asc * sz, x0 + w, y + desc * sz)
    return (f'<text x="{_f(x)}" y="{_f(y)}" font-family="{fam}" font-weight="{wt}" font-size="{_f(sz)}"'
            f'{sp_attr}{a}{fill_attr}{o}>{esc(s)}</text>')


def text_right(x_right, y, s, role="item", fill="@NAVY", size=None, **kw):
    """Right-aligned text by computed start x (anchor="end" misplaces strings with '·' in VectorCraft)."""
    w = text_width(s, role, size=size, family=kw.get("family"), weight=kw.get("weight"), ls=kw.get("ls"))
    return text(x_right - w, y, s, role, fill, size=size, **kw)


def text_lines(x, y, s, width, role="body", fill="@NAVY", size=None, leading=1.4, **kw):
    """Wrapped paragraph: returns (markup, height). y is the first baseline."""
    sz = size or TYPE[role][2]
    out = ""
    lines = wrap(s, width, role, size=sz, family=kw.get("family"), weight=kw.get("weight"), ls=kw.get("ls"))
    for i, ln in enumerate(lines):
        out += text(x, y + i * sz * leading, ln, role, fill, size=sz, **kw)
    return out, len(lines) * sz * leading


def group(name, body, transform=None, opacity=None, visible=True):
    t = f' transform="{transform}"' if transform else ""
    o = f' opacity="{_f(opacity)}"' if opacity is not None else ""
    v = "" if visible else ' display="none"'
    return f'<g id="{esc(name)}"{t}{o}{v}>{body}</g>'


def place(body, x, y, s=1.0, name=None):
    """Draw body (made at the origin) at (x, y), uniformly scaled by s. Not tracked by the layout
    registry; kit sheets draw everything at 1:1 in absolute coordinates instead."""
    t = f"translate({_f(float(x))} {_f(float(y))})" + (f" scale({_f(float(s))})" if s != 1 else "")
    return group(name or uid("Placed"), body, transform=t)


def clip(x, y, w, h, body):
    cid = uid("clip")
    return (f'<clipPath id="{cid}"><rect x="{_f(x)}" y="{_f(y)}" width="{_f(w)}" height="{_f(h)}"/></clipPath>'
            f'<g clip-path="url(#{cid})">{body}</g>')


# Shared <defs>. write() injects these once; components reference them and never emit defs.
def _base_defs():
    """Pass A <defs> built from the current tokens (a theme appends its own with defs(ui))."""
    return f"""<defs>
<pattern id="gridLight" width="{GRID}" height="{GRID}" patternUnits="userSpaceOnUse"><path d="M{GRID} 0H0V{GRID}" fill="none" stroke="{NAVY}" stroke-opacity="0.24" stroke-width="1"/></pattern>
<pattern id="gridFaint" width="{GRID}" height="{GRID}" patternUnits="userSpaceOnUse"><path d="M{GRID} 0H0V{GRID}" fill="none" stroke="{NAVY}" stroke-opacity="0.07" stroke-width="1"/></pattern>
<pattern id="gridDark" width="{GRID}" height="{GRID}" patternUnits="userSpaceOnUse"><path d="M{GRID} 0H0V{GRID}" fill="none" stroke="{LIGHT}" stroke-opacity="0.12" stroke-width="1"/></pattern>
<linearGradient id="sheenSmoked" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{LIGHT}" stop-opacity="{SHEEN_BAND['smoked']}"/><stop offset="1" stop-color="{LIGHT}" stop-opacity="0"/></linearGradient>
<linearGradient id="sheenCobalt" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{LIGHT}" stop-opacity="{SHEEN_BAND['cobalt']}"/><stop offset="1" stop-color="{LIGHT}" stop-opacity="0"/></linearGradient>
<linearGradient id="skyDay" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{COBALT}"/><stop offset="0.55" stop-color="{ELECTRIC}"/><stop offset="1" stop-color="{PAPER}"/></linearGradient>
<linearGradient id="skyBloom" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{COBALT}"/><stop offset="0.35" stop-color="{ELECTRIC}"/><stop offset="0.72" stop-color="{PAPER}"/><stop offset="1" stop-color="{PAPER}"/></linearGradient>
<radialGradient id="bloom" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="{LIGHT}" stop-opacity="1"/><stop offset="0.5" stop-color="{LIGHT}" stop-opacity="1"/><stop offset="1" stop-color="{LIGHT}" stop-opacity="0"/></radialGradient>
<linearGradient id="floorFade" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{NAVY}" stop-opacity="0"/><stop offset="1" stop-color="{NAVY}" stop-opacity="0.30"/></linearGradient>
<linearGradient id="skyDeep" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{COBALT}"/><stop offset="0.3" stop-color="{COBALT}"/><stop offset="1" stop-color="{ELECTRIC}"/></linearGradient>
<linearGradient id="skyHaze" x1="0" y1="0" x2="0" y2="1"><stop offset="0.55" stop-color="{LIGHT}" stop-opacity="0"/><stop offset="1" stop-color="{LIGHT}" stop-opacity="0.3"/></linearGradient>
<linearGradient id="sheenClear" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{LIGHT}" stop-opacity="0.45"/><stop offset="1" stop-color="{LIGHT}" stop-opacity="0"/></linearGradient>
<pattern id="dotScreen" width="8" height="8" patternUnits="userSpaceOnUse"><rect x="2" y="2" width="3" height="3" fill="{LIGHT}"/></pattern>
<pattern id="dotScreenInk" width="8" height="8" patternUnits="userSpaceOnUse"><rect x="3" y="3" width="2" height="2" fill="{NAVY}" fill-opacity="0.5"/></pattern>
<filter id="frost" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="8"/></filter>
<filter id="soft" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="12"/></filter>
</defs>"""


DEFS = _base_defs()


def svg_doc(body, w=1920, h=1080):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}">'
            f"{DEFS}{body}</svg>\n")


def write(path_, body, w=1920, h=1080):
    """Write one SVG with the full artboard viewBox (place it with crop:"crop")."""
    os.makedirs(os.path.dirname(os.path.abspath(path_)), exist_ok=True)
    with open(path_, "w", encoding="utf-8") as fh:
        fh.write(svg_doc(body, w, h))
    return path_

# ---------------------------------------------------------------------------
# 6. Preview (Quick Look): exact artboard-size PNGs without VectorCraft
# ---------------------------------------------------------------------------
# qlmanage -t -s S always writes an S x S PNG and aspect-FILLS a non-square SVG into it (a 1920 x 1080
# board comes out zoomed 1.78x). So the board is wrapped, centred, in an S x S square (S = max(w, h)),
# rendered at S, and sips cuts the centred w x h back out. The result is checked to be exactly w x h.


def _svg_parts(svg_path):
    with open(svg_path, encoding="utf-8") as fh:
        src = fh.read()
    head = re.search(r"<svg\b[^>]*>", src)
    if not head:
        raise ValueError(f"no <svg> root in {svg_path}")
    vb = re.search(r'viewBox="\s*([-\d.]+)[ ,]+([-\d.]+)[ ,]+([-\d.]+)[ ,]+([-\d.]+)', head.group(0))
    if vb:
        w, h = float(vb.group(3)), float(vb.group(4))
    else:
        w = float(re.search(r'width="([\d.]+)', head.group(0)).group(1))
        h = float(re.search(r'height="([\d.]+)', head.group(0)).group(1))
    body = src[head.end():src.rfind("</svg>")]
    return int(round(w)), int(round(h)), body


def _prefix_ids(body, p):
    body = re.sub(r'\bid="([^"]+)"', lambda m: f'id="{p}{m.group(1)}"', body)
    body = re.sub(r"url\(#([^)]+)\)", lambda m: f"url(#{p}{m.group(1)})", body)
    return re.sub(r'href="#([^"]+)"', lambda m: f'href="#{p}{m.group(1)}"', body)


def _png_size(png):
    out = subprocess.run(["sips", "-g", "pixelWidth", "-g", "pixelHeight", png], check=True,
                         capture_output=True, text=True).stdout
    return int(re.search(r"pixelWidth: (\d+)", out).group(1)), int(re.search(r"pixelHeight: (\d+)", out).group(1))


def _render(bodies, w, h, out_png, background=None):
    s = max(w, h)
    dx, dy = (s - w) / 2, (s - h) / 2
    bg = f'<rect x="0" y="0" width="{w}" height="{h}" fill="{background}"/>' if background else ""
    inner = "".join(bodies)
    wrapper = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{s}" height="{s}" viewBox="0 0 {s} {s}">'
               f'<g transform="translate({_f(float(dx))} {_f(float(dy))})">{bg}{inner}</g></svg>\n')
    tmp = tempfile.mkdtemp(prefix="ctd_preview_")
    try:
        src = os.path.join(tmp, "board.svg")
        with open(src, "w", encoding="utf-8") as fh:
            fh.write(wrapper)
        subprocess.run(["qlmanage", "-t", "-s", str(s), "-o", tmp, src], check=True, capture_output=True)
        png = src + ".png"
        if not os.path.exists(png):
            raise RuntimeError("qlmanage wrote no PNG")
        if _png_size(png) != (s, s):  # never resample silently: say so
            raise RuntimeError(f"qlmanage gave {_png_size(png)}, expected {s} x {s}")
        if (w, h) != (s, s):
            subprocess.run(["sips", "-c", str(h), str(w), png], check=True, capture_output=True)
        os.makedirs(os.path.dirname(os.path.abspath(out_png)), exist_ok=True)
        if os.path.exists(out_png):
            os.remove(out_png)
        shutil.move(png, out_png)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    got = _png_size(out_png)
    if got != (w, h):
        raise RuntimeError(f"preview is {got[0]} x {got[1]}, expected {w} x {h}")
    return out_png


def preview(svg_path, out_dir=None, out_png=None):
    """Rasterise one SVG to a PNG of exactly its viewBox size (1920 x 1080, 1280 x 720, 2560 x 1080 ...).
    Returns the PNG path (default: <out_dir or the SVG's folder>/<name>.svg.png)."""
    w, h, body = _svg_parts(svg_path)
    out_dir = out_dir or os.path.dirname(os.path.abspath(svg_path))
    out_png = out_png or os.path.join(out_dir, os.path.basename(svg_path) + ".png")
    return _render([body], w, h, out_png)


def preview_many(paths, out_png):
    """Composite several component SVGs (same viewBox, drawn in list order = bottom to top) into one
    PNG of exactly that size, so a whole artboard can be checked without VectorCraft."""
    sizes, bodies = set(), []
    for i, p in enumerate(paths):
        w, h, body = _svg_parts(p)
        sizes.add((w, h))
        bodies.append(_prefix_ids(body, f"f{i}_"))
    if len(sizes) != 1:
        raise ValueError(f"mixed viewBox sizes: {sorted(sizes)}")
    w, h = sizes.pop()
    return _render(bodies, w, h, out_png)

# ---------------------------------------------------------------------------
# 7. Glyphs and pictograms (paths, not font glyphs: the fonts do not carry them)
# ---------------------------------------------------------------------------


def diamond(cx, cy, r, fill="@CYAN", keyline="@NAVY", kw=1.5):
    """You / your firm: a square diamond."""
    return poly([(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)], fill, keyline, kw)


def triangle(cx, cy, r, fill="@AMBER", keyline="@NAVY", kw=1.5):
    """Rival firm: an upright triangle."""
    h = r * 1.8
    return poly([(cx, cy - h * 0.55), (cx + r, cy + h * 0.45), (cx - r, cy + h * 0.45)], fill, keyline, kw)


def warn_glyph(cx, cy, r, color="@AMBER"):
    """Hazard triangle with a bar and a square dot (all rectilinear)."""
    h = r * 1.8
    top, base = cy - h * 0.55, cy + h * 0.45
    return (poly([(cx, top), (cx + r, base), (cx - r, base)], None, color, 2) +
            rect(cx - 1, top + h * 0.34, 2, h * 0.32, color) + rect(cx - 1, base - h * 0.22, 2, 2.5, color))


def lock_glyph(cx, cy, s=8, color="@LIGHT"):
    """Square padlock: body 2s x 1.5s, square shackle. (cx, cy) = centre of the body."""
    b = rect(cx - s, cy - s * 0.4, 2 * s, s * 1.5, color)
    b += path(f"M{_f(float(cx - s * 0.6))} {_f(float(cy - s * 0.4))}V{_f(float(cy - s * 1.3))}"
              f"H{_f(float(cx + s * 0.6))}V{_f(float(cy - s * 0.4))}", None, color, 2)
    return b


def chevron(cx, cy, s=6, direction="down", color="@NAVY", sw=2):
    d = {"down": [(-s, -s / 2), (0, s / 2), (s, -s / 2)], "up": [(-s, s / 2), (0, -s / 2), (s, s / 2)],
         "left": [(s / 2, -s), (-s / 2, 0), (s / 2, s)], "right": [(-s / 2, -s), (s / 2, 0), (-s / 2, s)]}[direction]
    return path("M" + " L".join(f"{_f(float(cx + dx))} {_f(float(cy + dy))}" for dx, dy in d), None, color, sw)


def square_bullet(x, y, s=8, fill="@NAVY"):
    return rect(x, y, s, s, fill)


def brackets(x, y, w, h, leg=10, sw=1, color="@NAVY", gap=0):
    """Four L-corners around a rect (hover corners, lock brackets)."""
    x0, y0, x1, y1 = x - gap, y - gap, x + w + gap, y + h + gap
    d = (f"M{_f(x0)} {_f(y0 + leg)}V{_f(y0)}H{_f(x0 + leg)}"
         f"M{_f(x1 - leg)} {_f(y0)}H{_f(x1)}V{_f(y0 + leg)}"
         f"M{_f(x1)} {_f(y1 - leg)}V{_f(y1)}H{_f(x1 - leg)}"
         f"M{_f(x0 + leg)} {_f(y1)}H{_f(x0)}V{_f(y1 - leg)}")
    return path(d, None, color, sw)


_PICTO = {  # 40 x 24 unit box, rectilinear silhouettes
    "pistol": [[(4, 6), (34, 6), (34, 12), (17, 12), (15, 21), (8, 21), (10, 12), (4, 12)]],
    "ar15": [[(0, 9), (5, 7), (14, 7), (14, 5), (30, 5), (30, 8), (40, 8), (40, 10), (30, 10), (28, 12),
              (22, 12), (21, 20), (17, 20), (17, 12), (12, 12), (11, 17), (7, 17), (8, 12), (0, 14)]],
    "briefcase": [[(4, 7), (36, 7), (36, 22), (4, 22)], [(14, 2), (26, 2), (26, 7), (23, 7), (23, 4), (17, 4), (17, 7), (14, 7)]],
    "grenade_briefcase": [[(4, 9), (32, 9), (32, 22), (4, 22)], [(12, 4), (22, 4), (22, 9), (20, 9), (20, 6), (14, 6), (14, 9), (12, 9)],
                          [(32, 3), (38, 3), (38, 9), (32, 9), (32, 7), (36, 7), (36, 5), (32, 5)]],
    "office_chair": [[(10, 0), (16, 0), (16, 12), (30, 12), (30, 15), (21, 15), (21, 20), (31, 20), (31, 23),
                      (9, 23), (9, 20), (19, 20), (19, 15), (10, 15)]],
    "cubicle_panel": [[(8, 0), (32, 0), (32, 20), (30, 20), (30, 23), (26, 23), (26, 20), (14, 20), (14, 23),
                       (10, 23), (10, 20), (8, 20)]],
    "stairs": [[(4, 22), (4, 17), (12, 17), (12, 12), (20, 12), (20, 7), (28, 7), (28, 2), (36, 2), (36, 22)]],
}


def pictogram(kind, x, y, s=1.0, fill="@LIGHT", keyline=None):
    """Item silhouette in a 40 x 24 box at (x, y), scaled by s. keyline: two-tone edge for world use.
    kinds: pistol, ar15, briefcase, grenade_briefcase, office_chair, cubicle_panel, stairs."""
    fill = tok(fill)
    out = ""
    for pts in _PICTO[kind]:
        out += poly([(x + px * s, y + py * s) for px, py in pts], fill, keyline, 1.5 if keyline else 1)
    if kind == "cubicle_panel":
        out += rect(x + 11 * s, y + 3 * s, 18 * s, 14 * s, None, (NAVY, 0.6) if fill != NAVY else (LIGHT, 0.6), 1)
    if kind == "grenade_briefcase":  # pin ring as a square, fuse stripe
        out += rect(x + 6 * s, y + 14 * s, 24 * s, 2 * s, (NAVY, 0.7) if fill != NAVY else (LIGHT, 0.7))
    return out

# ---------------------------------------------------------------------------
# 8. Motifs
# ---------------------------------------------------------------------------


def hairline_grid(x, y, w, h, dark=False, faint=False, perspective=False, name=None):
    """Square 32 px grid (navy 24 % on light, 7 % faint, paper 12 % on dark), or a perspective grid."""
    if not perspective:
        pid = "gridDark" if dark else ("gridFaint" if faint else "gridLight")
        return group(name or uid("HairlineGrid"), rect(x, y, w, h, f"url(#{pid})"))
    col = (LIGHT, 0.22) if dark else HAIR
    vx, vy = x + w * 0.5, y - h * 0.9
    body = ""
    with _quiet():
        for i in range(-8, 9):
            bx = x + w * 0.5 + i * w / 10
            t = (y - vy) / (y + h - vy)
            body += line(vx + (bx - vx) * t, y, bx, y + h, col)
        k = 0
        while k <= 14:
            yy = y + h * (1 - 0.78 ** k)
            if yy > y + h - 1:
                break
            body += line(x, yy, x + w, yy, col)
            k += 1
    _reg("shapes", x, y, x + w, y + h)
    return group(name or uid("HairlineGrid"), clip(x, y, w, h, body))


def crop_marks(x, y, w, h, leg=8, gap=6, color="@NAVY", name=None):
    """Print-proof crop marks: 1 px ticks just outside each corner."""
    b = ""
    for cx, sx in ((x, -1), (x + w, 1)):
        for cy, sy in ((y, -1), (y + h, 1)):
            b += line(cx + sx * gap, cy, cx + sx * (gap + leg), cy, color)
            b += line(cx, cy + sy * gap, cx, cy + sy * (gap + leg), color)
    return group(name or uid("CropMarks"), b)


def microtype_strip(x, y, s, dark=False, name=None):
    """One line of legal microtype (11 px mono). Decorative only: never the carrier of needed information."""
    return group(name or uid("Microtype"), text(x, y, s, "micro", (LIGHT, 0.7) if dark else INK3))


TRACE_BOXES = []   # boxes of every trace square drawn while recording (make_kit checks them against text)


def trace_brackets(x, y, w, h, color="@NAVY", seed=7, density=0.9, snapped=True, name="TraceCluster",
                   squares=True):
    """The Survey trace: overlapping open squares jittered along a rect's edge, snapped corner brackets on
    top. Max ONE traced object per screen and it never touches text; the group id is "TraceCluster" so a grep can
    count it, and every square's box goes to TRACE_BOXES for the checker.
    squares=False draws only the snapped state (one clean bracket set, gap 4, 14 px legs, 2 px): the trace
    after it has locked on, for places where the squares would clutter text (a dialog)."""
    rnd = random.Random(seed)
    body = ""
    per = 2 * (w + h)
    n = max(8, int(per / 26 * density))
    for i in range(n if squares else 0):
        t = (i + rnd.random() * 0.6) / n * per
        if t < w:
            px, py = x + t, y
        elif t < w + h:
            px, py = x + w, y + (t - w)
        elif t < 2 * w + h:
            px, py = x + w - (t - w - h), y + h
        else:
            px, py = x, y + h - (t - 2 * w - h)
        s = rnd.choice((14, 18, 22, 26))
        jx, jy = rnd.uniform(-3, 3), rnd.uniform(-3, 3)
        sx, sy = round(px - s / 2 + jx, 1), round(py - s / 2 + jy, 1)
        body += rect(sx, sy, s, s, None, (color, round(0.55 + rnd.random() * 0.35, 2)), 1)
        if REC["on"]:
            TRACE_BOXES.append((sx, sy, sx + s, sy + s))
    if snapped:
        body += brackets(x, y, w, h, 14, 2, color, gap=4)
        if REC["on"]:   # the four bracket corners (legs 14, gap 4), not the box they enclose
            for bx, by in ((x - 4, y - 4), (x + w - 10, y - 4), (x - 4, y + h - 10), (x + w - 10, y + h - 10)):
                TRACE_BOXES.append((bx, by, bx + 14, by + 14))
    return group(name, body)


def chevron_row(x, y, n=3, direction="right", s=7, gap=12, color="@NAVY", sw=1.5, fade=True, name=None):
    """A row of n open chevrons pointing one way: a focus or direction cue (the focused menu item, the way up).
    (x, y) = centre of the first chevron; the row runs in the pointing direction. fade: each chevron is more
    opaque than the last, so the row reads as motion toward the target. Decorative: never the only cue."""
    c = _hex(color)
    step = {"right": (gap, 0), "left": (-gap, 0), "up": (0, -gap), "down": (0, gap)}[direction]
    b = ""
    for i in range(n):
        a = round(0.3 + 0.7 * (i + 1) / n, 2) if fade else 1.0
        b += chevron(x + step[0] * i, y + step[1] * i, s, direction, (c, a) if a < 1 else c, sw)
    return group(name or uid("ChevronRow"), b)


def barcode(x, y, w, h, seed=3, color="@BLACK", name=None):
    """A printed barcode strip (bars 1-3 px, gaps 1-3 px) in w x h: the legal / logistics cue. Decorative."""
    rnd = random.Random(seed)
    b, xx = "", float(x)
    with _quiet():
        while True:
            bw = rnd.choice((1, 1, 2, 3))
            if xx + bw > x + w:
                break
            b += rect(xx, y, bw, h, color)
            xx += bw + rnd.choice((1, 2, 2, 3))
    _reg("shapes", x, y, x + w, y + h)
    return group(name or uid("Barcode"), b)


def halftone(x, y, w, h, direction="down", color="@LIGHT", cell=8, dmax=5.0, dmin=0.0, name=None):
    """A dotted halftone screen of SQUARE dots (radius 0 rule) in w x h: dot size ramps from dmax to dmin along
    direction ("down", "up", "left", "right"). Over sky in light dots, over paper in navy. Decorative."""
    col = tok(color)
    b = ""
    nx, ny = int(w // cell), int(h // cell)
    with _quiet():
        for j in range(ny):
            for i in range(nx):
                t = {"down": j / max(1, ny - 1), "up": 1 - j / max(1, ny - 1),
                     "right": i / max(1, nx - 1), "left": 1 - i / max(1, nx - 1)}[direction]
                d = round((dmax + (dmin - dmax) * t) * 2) / 2
                if d < 1:
                    continue
                cx, cy = x + i * cell + cell / 2, y + j * cell + cell / 2
                b += rect(cx - d / 2, cy - d / 2, d, d, col)
    _reg("shapes", x, y, x + w, y + h)
    return group(name or uid("Halftone"), b)


def outlined_slot(x, y, w, h, label=None, color="@NAVY", name=None):
    """An empty outlined slot (a place an item will go): 1 px dashed outline, corner ticks, optional caption
    under it. Used for empty inventory and loadout places."""
    c = _hex(color)
    b = rect(x + 0.5, y + 0.5, w - 1, h - 1, None, (c, 0.55), 1, dash="4 3")
    b += brackets(x, y, w, h, 6, 1.5, c)
    if label:
        b += text(x, y + h + 18, label, "caption", INK3, ls=0.5)
    return group(name or uid("OutlinedSlot"), b)


def _cumulus(x0, base, w, h, seed=1, step=8, lit="@PAPER", shade="@SILVER"):
    """A stepped cel cumulus (thresholded, no curves): a dome of stacked puffs, tallest in the middle, whose outline
    snaps to `step`. Two cel tones only: the whole body in silver shade, then the lit puffs in paper (each puff shrunk
    and moved toward the light at the upper left), and a cool cobalt-tinted step along the flat underside.
    base = the underside's y; h = the dome's height. Drawn fresh for the game; not traced from anything."""
    rnd = random.Random(seed)
    n = max(3, int(w / max(64.0, h * 0.34)))
    col_w = w / n
    puffs = []
    for i in range(n):
        t = (i + 0.5) / n
        target = h * (0.22 + 0.78 * math.sin(math.pi * t) ** 1.6) * rnd.uniform(0.85, 1.05)
        r = col_w * rnd.uniform(0.62, 0.82)
        cy = base - r * 0.35
        while True:
            cx = x0 + t * w + rnd.uniform(-0.3, 0.3) * col_w
            puffs.append((cx, cy, r))
            if base - (cy - r) >= target:
                break
            cy -= r * rnd.uniform(0.55, 0.72)
            r *= rnd.uniform(0.8, 0.95)
            t = t + (0.5 - t) * 0.18   # the dome narrows as it rises

    def span(px, shrink=1.0, dx=0.0, dy=0.0):
        top, bot = None, None
        for cx, cy, r in puffs:
            rr = r * shrink
            d = abs(px - (cx + dx * r))
            if d < rr:
                k = math.sqrt(rr * rr - d * d)
                yt, yb = cy + dy * r - k, cy + dy * r + k
                top = yt if top is None else min(top, yt)
                bot = yb if bot is None else max(bot, yb)
        return top, bot

    def q(v):
        return round(v / step) * step
    body, lit_top, lit_bot = [], [], []
    xx = x0 - col_w
    while xx < x0 + w + col_w:
        mid = xx + step / 2
        t_, _ = span(mid)
        if t_ is not None:
            body.append((xx, q(t_)))
        lt, lb = span(mid, 0.78, -0.2, -0.2)
        if lt is not None:
            lit_top.append((xx, q(lt)))
            lit_bot.append((xx, min(q(lb), base)))
        xx += step
    b = ""
    if body:
        pts = [(body[0][0], base)]
        for px, py in body:
            pts += [(px, py), (px + step, py)]
        pts.append((body[-1][0] + step, base))
        b += poly(pts, shade)
    if lit_top:
        pts = []
        for px, py in lit_top:
            pts += [(px, py), (px + step, py)]
        for px, py in reversed(lit_bot):
            pts += [(px + step, py), (px, py)]
        b += poly(pts, lit)
    if body:
        b += rect(body[0][0], base - step, body[-1][0] + step - body[0][0], step, (COBALT, 0.16))
    return b


def sky_window(x, y, w, h, cols=3, rows=2, frame=24, seed=5, tower=False, name=None):
    """The window-frame device (ref 08): deep-blue sky with stepped cel cumulus seen through a white frame that
    divides it into cols x rows panes. The frame IS the paper; each pane has a silver reveal on its top and left
    edge (the frame's depth) and the outer frame a 1 px hairline. tower=True puts the rival tower's top in the
    last pane. Drawn at 1:1; returns markup."""
    with _quiet():
        sky = rect(x, y, w, h, "url(#skyDeep)")
        sky += _cumulus(x + w * 0.02, y + h, w * 0.5, h * 0.62, seed=seed, step=max(3, round(h / 32)))
        sky += _cumulus(x + w * 0.5, y + h, w * 0.46, h * 0.45, seed=seed + 4, step=max(3, round(h / 32)))
        if tower:
            tw = w * 0.16
            sky += _tower(x + w * 0.78, y + h * 0.28, tw, h * 0.9, band=max(6, h * 0.05), gap=max(8, h * 0.07),
                          wash=0.2)
        pw = (w - frame * (cols + 1)) / cols
        ph = (h - frame * (rows + 1)) / rows
        fr = ""
        for i in range(cols + 1):   # verticals
            fr += rect(x + i * (pw + frame), y, frame, h, PAPER)
        for j in range(rows + 1):   # horizontals
            fr += rect(x, y + j * (ph + frame), w, frame, PAPER)
        for i in range(cols):
            for j in range(rows):
                px, py = x + frame + i * (pw + frame), y + frame + j * (ph + frame)
                fr += rect(px, py, pw, max(3, frame * 0.25), (SILVER, 0.9))
                fr += rect(px, py, max(2, frame * 0.18), ph, (SILVER, 0.6))
        fr += rect(x + 0.5, y + 0.5, w - 1, h - 1, None, HAIR_STRONG, 1)
        body = clip(x, y, w, h, sky) + fr
    _reg("shapes", x, y, x + w, y + h)
    return group(name or uid("SkyWindow"), body)


def cyan_square(x, y, s=6, keyline=None):
    """Status light. On dark surfaces as is; on light ones pass keyline=NAVY (a 1 px navy edge carries it)."""
    b = rect(x - 1, y - 1, s + 2, s + 2, keyline) if keyline else ""
    return b + rect(x, y, s, s, CYAN)


def cobalt_block(x, y, w, h, name=None):
    return group(name or uid("CobaltBlock"), rect(x, y, w, h, COBALT))


def floor_numeral(x, y, n, size=240, label="FLOOR", dark=False, name=None):
    """Oversized UltraLight numeral (silver on paper, paper 18 % on dark) with a small caps label under it.
    x, y is the numeral's baseline-left. Decorative; the HUD carries the readable floor. Office floors run
    1-9; above them use floor_name() (BOARDROOM, ROOF), never a number above 9."""
    num_fill = (LIGHT, 0.18) if dark else SILVER
    lab_fill = LIGHT if dark else NAVY
    body = text(x, y, str(n), "floor", num_fill, size=size, ls=-size * 0.02)
    body += square_bullet(x + 6, y + 18, 8, CYAN if dark else NAVY)
    body += text(x + 22, y + 27, label, "caption", lab_fill, ls=3)
    return group(name or uid("FloorNumeral"), body)


def title_lockup(x, y, size=104, dark=False, serial="v0.1.0 · Early Access", name=None):
    """GAME_TITLE ("SEAL THE DEAL"), one large thin wide title, + "Terms and Conditions Apply." fine print.
    x, y = top-left of the cap box. Tracked at TITLE_TRACK em (0.22 in pass A).
    Returns markup; width = text_width(GAME_TITLE, "title", size=size, ls=size * TITLE_TRACK)."""
    ink = LIGHT if dark else NAVY
    fine = LIGHT if dark else INK2
    ls = size * TITLE_TRACK
    tw = text_width(GAME_TITLE, "title", size=size, ls=ls)
    cap = size * 0.72
    body = text(x - size * 0.02, y + cap, GAME_TITLE, "title", ink, size=size, ls=ls)
    ry = y + cap + size * 0.2
    body += line(x, ry, x + tw, ry, (ink, 0.5))
    fs = max(16, round(size * 0.17))
    by = ry + 10 + fs
    body += square_bullet(x, by - fs * 0.62, max(6, round(fs * 0.5)), BLACK if not dark else ink)
    body += text(x + fs * 0.95, by, "Terms and Conditions Apply.", "body", fine, size=fs)
    if serial:
        ms = max(11, round(size * 0.11))
        body += text_right(x + tw, by, serial, "micro", fine, size=ms)
    return group(name or uid("TitleLockup"), body)

# ---------------------------------------------------------------------------
# 9. Glass
# ---------------------------------------------------------------------------


def glass_ink(kind):
    """(text, secondary text, mark keyline) for a glass kind. Light kinds (frosted, plate, clear, amber) take navy /
    opaque ink-2; smoked and cobalt take paper / silver. A missing kind counts as a plate."""
    k = kind if kind in GLASS else "plate"
    if _hex(GLASS[k][3]) == _hex(NAVY):
        return NAVY, INK2, NAVY
    return LIGHT, SILVER, NAVY


def is_light(kind):
    """True when the kind takes navy text (frosted, plate, clear, amber, or a theme's own light kind)."""
    return glass_ink(kind)[0] == NAVY


def _light_pane(x, y, w, h, kind, a, mullions_x, mullions_y, streak, streak_at, shadow, under):
    """The light kinds: plate (white, crisp, a hard 2 px printed offset), clear (blue-grey see-through glass, white
    mullions, soft shadow, sheen) and amber (solid, 1 px near-black edge). Shadows sit outside the box and are not
    recorded, so a pane's box is exactly (x, y, w, h)."""
    b = ""
    if kind == "plate":
        if shadow:
            with _quiet():
                b += rect(x + 2, y + 2, w, h, (NAVY, 0.16))
        b += rect(x, y, w, h, (PAPER, a))
        b += line(x + 1, y + 1.5, x + w - 1, y + 1.5, (LIGHT, 1.0))
        edge, mull, s_alpha = HAIR_STRONG, HAIR, 0.0
    elif kind == "clear":
        if shadow:
            with _quiet():
                b += rect(x + 6, y + 10, w, h, (NAVY, 0.10), extra=' filter="url(#soft)"')
        if under:
            with _quiet():
                b += clip(x, y, w, h, under)
        b += rect(x, y, w, h, (SILVER, a)) + rect(x, y, w, h, (ELECTRIC, 0.06))
        b += rect(x, y, w, h * 0.4, "url(#sheenClear)")
        edge, mull = (LIGHT, 0.9), (LIGHT, 0.95)
        s_alpha = 0.30 if (streak is None or streak) else 0.0
    else:  # amber: solid, a near-black hairline is its edge (black as accent, never as a fill)
        b += rect(x, y, w, h, (AMBER, a))
        edge, mull, s_alpha = BLACK, (NAVY, 0.3), 0.0
    if s_alpha > 0:
        sx = x + w * streak_at
        dx = h * math.tan(math.radians(18))
        with _quiet():
            s = poly([(sx + dx, y), (sx + dx + 24, y), (sx + 24, y + h), (sx, y + h)], (LIGHT, s_alpha))
            s += poly([(sx + dx + 32, y), (sx + dx + 36, y), (sx + 36, y + h), (sx + 32, y + h)], (LIGHT, s_alpha))
            b += clip(x, y, w, h, s)
    mw = 4 if kind == "clear" else 1
    for fx in mullions_x:
        mx = round(x + w * fx)
        b += rect(mx - mw / 2, y, mw, h, mull)
        if kind == "clear":
            b += rect(mx + mw / 2, y, 1, h, (NAVY, 0.18))
    for fy in mullions_y:
        my = round(y + h * fy)
        b += rect(x, my - mw / 2, w, mw, mull)
    b += rect(x + 0.5, y + 0.5, w - 1, h - 1, None, edge, 1)
    if kind == "clear":
        b += rect(x - 0.5, y - 0.5, w + 1, h + 1, None, HAIR, 1)
    return b


def glass_pane(x, y, w, h, kind="smoked", mullions_x=(), mullions_y=(), streak=None, streak_at=0.74,
               under=None, shadow=True, alpha=None, name=None):
    """Flat architectural glass, radius 0, drawn at the SVG alpha that matches the engine over bloom.
    Light kinds (pass A since the 2026-11-12 guide; navy text only):
          "plate"   (white plate, paper 90 %: every HUD chip, prompt, slot, tracker and banner; hard 2 px offset),
          "clear"   (clear blue-grey glass, silver 40 %: frames and window panes; carries NO text, put a plate in it),
          "amber"   (solid amber plate, 1 px near-black edge: rival and threat; navy text and marks),
          "frosted" (menu panes over the scene; navy text only).
    Legacy kinds, drawn exactly as before: "smoked" (dark HUD chip; paper / silver text; silver never in the top
          30 % band), "cobalt" (primary button, active tab; paper text; no diagonal streak).
    mullions_x / mullions_y: fractions 0..1 where a mullion divides the pane (white 4 px bars on clear glass).
    streak: one hard diagonal sheen streak (18 deg from vertical, 24 px + a 4 px echo) at streak_at.
            None = the kind's default (frosted and clear on, smoked off, cobalt / plate / amber never).
    under: markup of what is behind, drawn blurred inside a frosted pane (sharp inside clear glass).
    alpha: override the SVG fill alpha (1.0 = solid warning chip; HUD-opacity setting never below the token)."""
    if kind not in GLASS:   # a theme GLASS without the new kinds: nearest legacy look
        kind = {"plate": "frosted", "clear": "frosted", "amber": "smoked"}.get(kind, "frosted")
    fill, _, svg_a, _, _ = GLASS[kind]
    a = alpha if alpha is not None else svg_a
    if kind in ("plate", "clear", "amber"):
        b = _light_pane(x, y, w, h, kind, a, mullions_x, mullions_y, streak, streak_at, shadow, under)
        _reg("panes", x, y, x + w, y + h, kind=kind, solid=(a >= 0.999), band=0)
        return group(name or uid("GlassPane"), b)
    streak = (kind == "frosted") if streak is None else streak
    s_alpha = STREAK[kind] if streak else 0.0
    b = ""
    if kind == "frosted":
        if shadow:
            with _quiet():
                b += rect(x, y + 8, w, h, (NAVY, 0.12), extra=' filter="url(#soft)"')
        if under:
            with _quiet():
                b += clip(x, y, w, h, f'<g filter="url(#frost)">{under}</g>')
        b += rect(x, y, w, h, (fill, a))
        hi, border, mull = (LIGHT, 1.0), HAIR, HAIR
    elif kind == "cobalt":
        if shadow:
            b += rect(x, y + 2, w, h, (NAVY, 0.30))
        b += rect(x, y, w, h, (fill, a)) + rect(x, y, w, h * 0.5, "url(#sheenCobalt)")
        hi, border, mull = (LIGHT, 0.35), None, (LIGHT, 0.2)
    else:
        b += rect(x, y, w, h, (fill, a)) + rect(x, y, w, h * 0.3, "url(#sheenSmoked)")
        hi, border, mull = (LIGHT, 0.5), HAIR_DARK, HAIR_DARK
    sx = x + w * streak_at
    if s_alpha > 0:
        dx = h * math.tan(math.radians(18))
        with _quiet():
            s = poly([(sx + dx, y), (sx + dx + 24, y), (sx + 24, y + h), (sx, y + h)], (LIGHT, s_alpha))
            s += poly([(sx + dx + 32, y), (sx + dx + 36, y), (sx + 36, y + h), (sx + 32, y + h)], (LIGHT, s_alpha))
            b += clip(x, y, w, h, s)
    for fx in mullions_x:
        mx = round(x + w * fx)
        b += rect(mx - 1, y, 2, h, mull) + line(mx + 1.5, y, mx + 1.5, y + h, (LIGHT, 0.9 if kind == "frosted" else 0.25))
    for fy in mullions_y:
        my = round(y + h * fy)
        b += rect(x, my - 1, w, 2, mull)
    b += line(x + 1, y + 1.5, x + w - 1, y + 1.5, hi)
    if border:
        b += rect(x + 0.5, y + 0.5, w - 1, h - 1, None, border, 1)
    _reg("panes", x, y, x + w, y + h, kind=kind, solid=(a >= 0.999),
         band=(h * 0.3 if kind == "smoked" else h * 0.5 if kind == "cobalt" else 0))
    return group(name or uid("GlassPane"), b)


def plate(x, y, w, h, name=None, **kw):
    """A white plate: glass_pane(x, y, w, h, "plate"). Navy text; the HUD's surface over the world."""
    return glass_pane(x, y, w, h, "plate", name=name or uid("Plate"), **kw)

# ---------------------------------------------------------------------------
# 10. Interactive components (states: default, hover, focus, pressed, disabled)
# ---------------------------------------------------------------------------
# State is never colour alone: hover = open corner brackets, focus = 2 px ring + heavy snapped brackets,
# pressed = 2 px drop with the offset shadow gone and a stamped inner edge, disabled = dashed outline.
# Hover and focus marks reach 8 px (hover) and 10 px (focus) outside the control: leave that much room.

STATES = ("default", "hover", "focus", "pressed", "disabled")
KEYBIND_STATES = STATES + ("listening", "conflict")


def focus_ring(x, y, w, h, dark=False):
    """3 px gap, then a 2 px ring (navy on light, cyan on dark) with snapped 3 px corner brackets."""
    c = CYAN if dark else NAVY
    return rect(x - 4, y - 4, w + 8, h + 8, None, c, 2) + brackets(x, y, w, h, 12, 3, c, gap=7)


def hover_corners(x, y, w, h, dark=False):
    """1 px open corner brackets 6 px outside (they close in to 0 over 100 ms)."""
    return brackets(x, y, w, h, 10, 1, LIGHT if dark else NAVY, gap=6)


def _stamped(x, y, w, h, color):
    """Pressed: a 2 px inner edge on top and left, like a rubber stamp pushed in."""
    return rect(x, y, w, 2, color) + rect(x, y, 2, h, color)


def _pressed_light(x, y, w, h):
    """Pressed on light (pass A since the guide): the paper face pushed in, a 12 % navy shade, a 2 px stamped navy
    edge on top and left and a 1 px navy outline. No dark fill."""
    return (rect(x, y, w, h, PAPER) + rect(x, y, w, h, (NAVY, 0.12)) + _stamped(x, y, w, h, NAVY) +
            rect(x + 0.5, y + 0.5, w - 1, h - 1, None, NAVY, 1))


def button(x, y, w, label, variant="primary", state="default", h=None, dark=False, name=None):
    """variant: primary (cobalt glass, paper label, 64 px) | secondary (outlined, 56 px) |
    quiet (square bullet + text, 56 px). Label: item 20 px, sentence case.
    Hover: open corners (+ navy shade on primary, electric edge on secondary, underline on quiet).
    Focus: 2 px ring + snapped brackets. Pressed (light): primary goes solid cobalt with a stamped navy edge; the
    others are pushed in (12 % navy shade, stamped 2 px navy edge), all 2 px down. Dark: legacy cyan / navy fill.
    Disabled: dashed outline, 45 % label, not focusable; the reason goes in a status_line."""
    h = h or (SIZE["button_primary_h"] if variant == "primary" else SIZE["button_secondary_h"])
    yy = y + (2 if state == "pressed" else 0)
    ink = LIGHT if dark else NAVY
    b = ""
    lx, label_fill = x + 20, ink
    if state == "pressed":
        if dark:
            b += rect(x, yy, w, h, CYAN)
            b += _stamped(x, yy, w, h, (NAVY, 0.5))
            label_fill = NAVY
        elif variant == "primary":
            b += rect(x, yy, w, h, COBALT) + _stamped(x, yy, w, h, (NAVY, 0.6))
            label_fill = LIGHT
        else:
            b += _pressed_light(x, yy, w, h)
            label_fill = NAVY
    elif state == "disabled":
        b += rect(x, yy, w, h, (LIGHT, 0.06) if dark else (NAVY, 0.04))
        b += rect(x + 0.5, yy + 0.5, w - 1, h - 1, None, (ink, 0.45), 1, dash="4 3")
        label_fill = (ink, 0.45)
    elif variant == "primary":
        b += glass_pane(x, yy, w, h, "cobalt", name=uid("ButtonFill"))
        if state == "hover":
            b += rect(x, yy, w, h, HOVER_SHADE)
        label_fill = LIGHT
    elif variant == "secondary":
        b += rect(x, yy, w, h, (LIGHT, 0.06) if dark else PAPER)
        if state == "hover":
            b += rect(x, yy, w, h, (LIGHT, 0.10) if dark else (NAVY, 0.06))
        b += rect(x + 0.5, yy + 0.5, w - 1, h - 1, None, ink, 1)
        if state == "hover":
            b += rect(x, yy, 4, h, CYAN if dark else ELECTRIC)
    elif state == "hover":  # quiet
        b += rect(x, yy, w, h, (LIGHT, 0.08) if dark else (NAVY, 0.05))
    if variant == "quiet":
        b += square_bullet(x + 16, yy + h / 2 - 4, 8, label_fill)
        lx = x + 34
        if state == "hover":
            b += rect(lx, yy + h / 2 + 13, text_width(label, "item"), 2, CYAN if dark else ELECTRIC)
    elif variant == "secondary" and state == "hover":
        lx = x + 24
    b += text(lx, yy + h / 2 + 7, label, "item", label_fill)
    if state == "hover":
        b += hover_corners(x, yy, w, h, dark)
    if state == "focus":
        b += focus_ring(x, yy, w, h, dark)
    return group(name or uid(f"Button-{variant}-{state}-"), b)


def toggle(x, y, on=True, state="default", label=None, dark=False, name=None):
    """Square toggle: 22 px box, 12 px cobalt (cyan on dark) square when on, plus On / Off text (item 20).
    Pressed: solid cobalt box with the on-mark lit in cyan (paper box + navy mark on dark), 2 px down."""
    s = SIZE["toggle"]
    ink = LIGHT if dark else NAVY
    hot = CYAN if dark else COBALT
    yy = y + (2 if state == "pressed" else 0)
    b = ""
    if state == "pressed":  # stamped: solid box, the on-mark stays lit
        b += rect(x, yy, s, s, ink if dark else COBALT)
        if on:
            b += rect(x + 4, yy + 4, s - 8, s - 8, NAVY if dark else CYAN)
    elif state == "disabled":
        b += rect(x + 1, yy + 1, s - 2, s - 2, None, (ink, 0.45), 2, dash="3 2")
        if on:
            b += rect(x + 5, yy + 5, s - 10, s - 10, (ink, 0.45))
    else:
        b += rect(x + 1, yy + 1, s - 2, s - 2, (NAVY, 0.0) if dark else PAPER, ink, 2)
        if on:
            b += rect(x + 5, yy + 5, s - 10, s - 10, hot)
    lab = label if label is not None else ("On" if on else "Off")
    b += text(x + s + 12, yy + s / 2 + 7, lab, "item", (ink, 0.45) if state == "disabled" else ink)
    hit_w = s + 12 + text_width(lab, "item")
    if state == "hover":
        b += hover_corners(x, yy, hit_w, s, dark)
    if state == "focus":
        b += focus_ring(x - 6, yy - 6, hit_w + 12, s + 12, dark)
    return group(name or uid(f"Toggle-{state}-"), b)


def toggle_width(on=True, label=None):
    """Drawn width of toggle() without its hover / focus marks."""
    lab = label if label is not None else ("On" if on else "Off")
    return SIZE["toggle"] + 12 + text_width(lab, "item")


def slider(x, y, value, vmin, vmax, text_value, state="default", track=None, dark=False, name=None):
    """Slider + number field, 40 px tall; total width track + 20 + 72. Square thumb 12 x 28. Hover enlarges
    the thumb and adds corners; pressed turns the thumb cobalt, drops it 2 px and gives the field a 2 px edge."""
    tw = track or SIZE["slider_track"]
    ink = LIGHT if dark else NAVY
    hot = CYAN if dark else COBALT
    cy = y + 20
    tx = x + (value - vmin) / (vmax - vmin) * tw
    dis = state == "disabled"
    b = ""
    if dis:
        b += line(x, cy, x + tw, cy, (ink, 0.45), 2, dash="4 3")
    else:
        b += rect(x, cy - 1, tw, 2, (ink, 0.24)) + rect(x, cy - 2, tx - x, 4, hot)
        for k in range(5):
            b += rect(x + k * tw / 4 - 0.5, cy + 9, 1, 5, (ink, 0.4))
    th_w, th_h = (14, 32) if state in ("hover", "pressed") else (12, 28)
    ty = cy - th_h / 2 + (2 if state == "pressed" else 0)
    b += rect(tx - th_w / 2, ty, th_w, th_h, (ink, 0.45) if dis else (hot if state == "pressed" else ink))
    if not dis and state != "pressed":
        b += rect(tx - th_w / 2 + 2, ty + 2, th_w - 4, 1, (NAVY, 0.6) if dark else (LIGHT, 0.6))
    fx, fw = x + tw + 20, SIZE["value_field_w"]
    if dis:
        b += rect(fx + 0.5, y + 0.5, fw - 1, 39, None, (ink, 0.45), 1, dash="4 3")
    else:
        pressed = state == "pressed"
        b += rect(fx, y, fw, 40, (LIGHT, 0.06) if dark else PAPER, hot if pressed else HAIR_STRONG if not dark else (ink, 0.4),
                  2 if pressed else 1)
    b += text(fx + fw / 2, y + 27, text_value, "hud", (ink, 0.45) if dis else ink, size=18, anchor="middle")
    if state == "hover":
        b += hover_corners(tx - th_w / 2, ty, th_w, th_h, dark)
    if state == "focus":
        b += focus_ring(x - 8, y, tw + 20 + fw + 16, 40, dark)
    return group(name or uid(f"Slider-{state}-"), b)


def stepper(x, y, w, value, state="default", kind="dropdown", dark=False, name=None):
    """Dropdown (chevron down) or stepper (left / right chevrons cycle the value). 48 px tall, value 18 px.
    Pressed (light): pushed in, stamped navy edge, navy value. Dark: legacy cyan fill."""
    h = 48
    ink = LIGHT if dark else NAVY
    yy = y + (2 if state == "pressed" else 0)
    dis = state == "disabled"
    b = ""
    if state == "pressed":
        if dark:
            b += rect(x, yy, w, h, CYAN) + _stamped(x, yy, w, h, (BLACK, 0.6))
            ink2 = NAVY
        else:
            b += _pressed_light(x, yy, w, h)
            ink2 = NAVY
    else:
        b += rect(x, yy, w, h, (LIGHT, 0.06) if dark else PAPER)
        if state == "hover":
            b += rect(x, yy, w, h, (LIGHT, 0.10) if dark else (NAVY, 0.06))
        b += rect(x + 0.5, yy + 0.5, w - 1, h - 1, None, (ink, 0.45) if dis else (ink, 0.5), 1, dash="4 3" if dis else None)
        ink2 = (ink, 0.45) if dis else ink
    if kind == "dropdown":
        b += text(x + 16, yy + 31, value, "item", ink2, size=18)
        b += rect(x + w - 44, yy + 10, 1, h - 20, (_hex(ink2), 0.35))
        b += chevron(x + w - 22, yy + h / 2, 6, "down", ink2)
    else:
        b += chevron(x + 18, yy + h / 2, 6, "left", ink2)
        b += chevron(x + w - 18, yy + h / 2, 6, "right", ink2)
        b += text(x + w / 2, yy + 31, value, "item", ink2, size=18, anchor="middle")
    if state == "hover":
        b += hover_corners(x, yy, w, h, dark)
    if state == "focus":
        b += focus_ring(x, yy, w, h, dark)
    return group(name or uid(f"{kind.title()}-{state}-"), b)


def keybind_width(key):
    return max(64, int(text_width(key, "hud", size=18) + 36))


def keybind(x, y, key, state="default", w=None, dark=False, name=None):
    """Key-bind cell (square keycap, 48 px). States: the five STATES plus
    "listening" (waiting for a key: cobalt 2 px frame, an ellipsis in place of the key, open brackets),
    "conflict" (key already used elsewhere: SOLID amber plate, 1 px near-black edge, navy hazard + navy key;
    on dark: the legacy solid navy cell with amber edge and key).
    Pressed = the click that starts listening. Disabled = a fixed binding (Esc)."""
    h = SIZE["keycap"]
    w = w or keybind_width(key)
    ink = LIGHT if dark else NAVY
    yy = y + (2 if state == "pressed" else 0)
    b = ""
    if state == "pressed":
        if dark:
            b += rect(x, yy, w, h, CYAN) + _stamped(x, yy, w, h, (BLACK, 0.6))
        else:
            b += _pressed_light(x, yy, w, h)
        b += text(x + w / 2, yy + 30, key, "hud", NAVY, size=18, anchor="middle")
    elif state == "disabled":
        b += rect(x + 0.5, yy + 0.5, w - 1, h - 1, None, (ink, 0.45), 1, dash="4 3")
        b += text(x + w / 2, yy + 30, key, "hud", (ink, 0.45), size=18, anchor="middle")
    elif state == "listening":
        hot = CYAN if dark else COBALT
        b += rect(x, yy, w, h, (LIGHT, 0.06) if dark else PAPER)
        b += rect(x + 1, yy + 1, w - 2, h - 2, None, hot, 2)
        for k in range(3):  # ellipsis as three squares (no glyph)
            b += rect(x + w / 2 - 10 + k * 8, yy + h / 2 + 2, 4, 4, ink)
        b += brackets(x, yy, w, h, 8, 1, hot, gap=5)
    elif state == "conflict":
        if dark:
            b += rect(x, yy, w, h, NAVY) + rect(x + 1, yy + 1, w - 2, h - 2, None, AMBER, 2)
            b += warn_glyph(x + 17, yy + h / 2 + 1, 6, AMBER)
            b += text(x + w / 2 + 9, yy + 30, key, "hud", AMBER, size=18, anchor="middle")
        else:
            b += rect(x, yy, w, h, AMBER) + rect(x + 0.5, yy + 0.5, w - 1, h - 1, None, BLACK, 1)
            b += warn_glyph(x + 17, yy + h / 2 + 1, 6, NAVY)
            b += text(x + w / 2 + 9, yy + 30, key, "hud", NAVY, size=18, anchor="middle")
    else:
        b += rect(x, yy, w, h, (LIGHT, 0.06) if dark else PAPER, ink, 1)
        b += rect(x + 1, yy + h - 4, w - 2, 3, (ink, 0.18))  # keycap lip, square
        if state == "hover":
            b += rect(x + 1, yy + 1, w - 2, h - 5, (LIGHT, 0.10) if dark else (NAVY, 0.06))
        b += text(x + w / 2, yy + 30, key, "hud", ink, size=18, anchor="middle")
    if state == "hover":
        b += hover_corners(x, yy, w, h, dark)
    if state == "focus":
        b += focus_ring(x, yy, w, h, dark)
    return group(name or uid(f"KeyBind-{state}-"), b)


def tab(x, y, w, label, state="default", active=False, dark=False, name=None):
    """One tab, 56 px, label 20 px centred. Active = cobalt glass block with a paper label (the screen's one
    cobalt block). Pressed (light): pushed in with a stamped navy edge."""
    h = SIZE["tab_h"]
    ink = LIGHT if dark else NAVY
    yy = y + (2 if state == "pressed" else 0)
    b = ""
    lf = ink
    if active and state not in ("pressed", "disabled"):
        b += glass_pane(x, yy, w, h, "cobalt", shadow=False, name=uid("TabFill"))
        lf = LIGHT
    elif state == "pressed":
        if dark:
            b += rect(x, yy, w, h, CYAN) + _stamped(x, yy, w, h, (BLACK, 0.6))
        else:
            b += _pressed_light(x, yy, w, h)
        lf = NAVY
    elif state == "disabled":
        b += rect(x + 0.5, yy + 0.5, w - 1, h - 1, None, (ink, 0.45), 1, dash="4 3")
        lf = (ink, 0.45)
    elif state == "hover":
        b += rect(x, yy, w, h, (LIGHT, 0.08) if dark else (NAVY, 0.05))
        b += rect(x, yy + h - 3, w, 3, CYAN if dark else ELECTRIC)
    b += text(x + w / 2, yy + h / 2 + 7, label, "item", lf, anchor="middle")
    if state == "hover":
        b += hover_corners(x, yy, w, h, dark)
    if state == "focus":
        b += focus_ring(x, yy, w, h, dark)
    return group(name or uid(f"Tab-{state}-"), b)


def keycap(x, y, key, dark=True, size=16, h=32):
    """Bare outlined keycap (inside a chip or a menu). Returns (markup, width)."""
    ink = LIGHT if dark else NAVY
    kw = max(h, int(text_width(key, "hud", size=size) + 18))
    return (rect(x + 0.75, y + 0.75, kw - 1.5, h - 1.5, None, ink, 1.5) +
            text(x + kw / 2, y + h / 2 + size * 0.36, key, "hud", ink, size=size, anchor="middle")), kw


def key_hint_width(key, label):
    kw = max(32, int(text_width(key, "hud", size=16) + 18))
    return 12 + kw + (12 + text_width(label, "item") + 14 if label else 12)


def key_hint(x, y, key, label, world=True, dark=None, name=None):
    """Keycap + action (item 20). world=True draws its own 48 px chip on HUD_GLASS (a white plate: navy keycap and
    label). Width: key_hint_width(key, label)."""
    if dark is None:
        dark = world and not is_light(HUD_GLASS)
    ink = LIGHT if dark else NAVY
    cap, kw = keycap(x + 12, y + 8, key, dark)
    w = key_hint_width(key, label)
    b = glass_pane(x, y, w, 48, HUD_GLASS, name=uid("HintChip")) if world else ""
    b += cap
    if label:
        b += text(x + 12 + kw + 12, y + 31, label, "item", ink)
    return group(name or uid("KeyHint"), b)


def tab_bar(x, y, w, tabs, active=0, focus=None, hover=None, dark=False, name=None):
    """Tabs between Q / E key hints; hairline under the bar. Q / E and LB / RB switch tabs (menu map only)."""
    ink = LIGHT if dark else NAVY
    b = key_hint(x, y + 4, "Q", "", world=False, dark=dark, name=uid("TabQ"))
    inner_x, inner_w = x + 64, w - 128
    tw = inner_w / len(tabs)
    for i, t in enumerate(tabs):
        st = "focus" if focus == i else ("hover" if hover == i else "default")
        b += tab(inner_x + i * tw, y, tw, t, st, active=(i == active), dark=dark, name=uid(f"Tab-{t}-"))
    b += key_hint(x + w - 56, y + 4, "E", "", world=False, dark=dark, name=uid("TabE"))
    b += line(x, y + SIZE["tab_h"] + 0.5, x + w, y + SIZE["tab_h"] + 0.5, (ink, 0.24))
    return group(name or uid("TabBar"), b)


def section_header(x, y, w, label, dark=False, name=None):
    """Settings clause heading: near-black square bullet (cyan on dark), display caps 22 px, full-width keyline."""
    ink = LIGHT if dark else NAVY
    h = SIZE["row_h"]
    b = square_bullet(x, y + h - 30, 10, CYAN if dark else BLACK)
    b += text(x + 22, y + h - 20, label, "heading", ink, size=22, ls=2)
    b += line(x, y + h - 0.5, x + w, y + h - 0.5, ink, 1)
    return group(name or uid("SectionHeader"), b)


def _conflict_mark(x, y, dark=False):
    """18 px hazard tile for a key-conflict line: amber plate + navy hazard (legacy navy tile + amber on dark)."""
    if dark:
        return rect(x, y, 18, 18, NAVY) + warn_glyph(x + 9, y + 10, 6, AMBER)
    return (rect(x, y, 18, 18, AMBER) + rect(x + 0.5, y + 0.5, 17, 17, None, BLACK, 1) +
            warn_glyph(x + 9, y + 10, 6, NAVY))


def settings_row(x, y, w, kind, label, value=None, state="default", desc=None, dark=False, name=None, **kw):
    """One settings row, 64 px: label left (item 20), control right-aligned 12 px from the edge.
    kind: "header" (clause heading, e.g. "§ 1  CONTROLS"), "slider" (value=(v, min, max, "0.12"); kw track),
          "toggle" (value=bool), "dropdown" / "stepper" (value=str; kw control_w, default 260),
          "keybind" (value=(keyboard, pad)).
    state: STATES; keybind rows also take "listening" and "conflict" (the keyboard cell shows it).
    A focused / listening / conflict row gets a 4 px cobalt edge and a light band; desc (body 18) then shows
    under it, 26 px below the row (conflict desc carries an amber hazard tile). Leave 36 px for it."""
    if kind == "header":
        return section_header(x, y, w, label, dark, name)
    ink = LIGHT if dark else NAVY
    h = SIZE["row_h"]
    b = ""
    lit = state in ("focus", "listening", "conflict")
    if lit:
        b += rect(x, y, w, h, (LIGHT, 0.08) if dark else (NAVY, 0.04)) + rect(x, y, 4, h, CYAN if dark else COBALT)
    b += text(x + 16, y + h / 2 + 7, label, "item", (ink, 0.45) if state == "disabled" else ink)
    right = x + w - 12
    cstate = "default" if state in ("listening", "conflict") else state
    if kind == "slider":
        v, lo, hi, txt = value
        track = kw.get("track", 200)
        b += slider(right - (track + 20 + SIZE["value_field_w"]), y + 12, v, lo, hi, txt, cstate, track, dark,
                    name=uid("RowSlider"))
    elif kind == "toggle":
        b += toggle(right - toggle_width(value), y + 21, value, cstate, None, dark, name=uid("RowToggle"))
    elif kind in ("dropdown", "stepper"):
        dw = kw.get("control_w", 260)
        b += stepper(right - dw, y + 8, dw, value, cstate, kind, dark, name=uid("RowControl"))
    elif kind == "keybind":
        k1, k2 = value
        w1, w2 = keybind_width(k1), keybind_width(k2)
        b += keybind(right - w2, y + 8, k2, "default" if lit else state, w2, dark, name=uid("RowPad"))
        b += keybind(right - w2 - 16 - w1, y + 8, k1, state, w1, dark, name=uid("RowKey"))
    b += line(x, y + h - 0.5, x + w, y + h - 0.5, (ink, 0.24))
    if desc and lit:
        dx = x + 16
        if state == "conflict":
            b += _conflict_mark(dx, y + h + 9, dark)
            dx += 28
        b += text(dx, y + h + 26, desc, "body", INK2 if not dark else SILVER)
    return group(name or uid(f"Row-{kind}-"), b)

# ---------------------------------------------------------------------------
# 11. HUD and feedback (all over the 3D world: smoked glass or two-tone, >= 16 px text)
# ---------------------------------------------------------------------------


def _hud_look(warn=False):
    """(kind, ink, ink-2, light) for a HUD surface: HUD_GLASS, or WARN_GLASS for a warning."""
    kind = WARN_GLASS if warn else HUD_GLASS
    ink, ink2, _ = glass_ink(kind)
    return kind, ink, ink2, is_light(kind)


def _hud_rule(x, y, w, warn, light, legacy_h=1):
    """The system rule along a HUD plate's top: 2 px cobalt on a white plate, none on an amber plate,
    the legacy 1 px cyan / 2 px amber on smoked."""
    if light:
        return "" if warn else rect(x, y, w, 2, COBALT)
    return rect(x, y, w, 2 if warn else legacy_h, AMBER if warn else CYAN)


def hud_chip(x, y, value, label=None, w=None, mono=True, tone="normal", icon=None, rule=True,
             alpha=None, name=None):
    """Small high-contrast HUD block, 48 px tall, on a white plate (HUD_GLASS) with navy type.
    value: mono 28 (mono=True) or item 20. label: ink-2 16 before the value.
    tone="warning": SOLID amber plate (WARN_GLASS), navy text, navy hazard. icon: "you" (cyan diamond, navy keyline)
    | "rival" (amber triangle, navy keyline) | "warn" (hazard) drawn at the left. rule: 2 px cobalt system rule on
    top (decorative). A theme with HUD_GLASS = "smoked" gets the legacy dark chip back."""
    h = 48
    vsize = 28 if mono else 20
    role = "hud" if mono else "item"
    lab_w = text_width(label, "label") + 12 if label else 0
    ico_w = 28 if icon else 0
    w = w or int(16 + ico_w + lab_w + text_width(value, role, size=vsize) + 18)
    warn = tone == "warning"
    kind, ink, ink2, light = _hud_look(warn)
    a = WARNING_A if (warn and not light) else alpha
    b = glass_pane(x, y, w, h, kind, alpha=a, name=uid("ChipGlass"))
    if rule:
        b += _hud_rule(x, y, w, warn, light)
    cx = x + 16
    key = NAVY if light else None
    if icon == "you":
        b += diamond(cx + 8, y + h / 2, 8, CYAN, key)
    elif icon == "rival":
        b += triangle(cx + 8, y + h / 2 + 1, 8, PAPER if (warn and light) else AMBER, key)
    elif icon == "warn":
        b += warn_glyph(cx + 8, y + h / 2 + 1, 9, NAVY if light else AMBER)
    cx += ico_w
    if label:
        b += text(cx, y + h / 2 + 6, label, "label", ink if (warn and light) else (AMBER if warn else ink2))
        cx += lab_w
    b += text(cx, y + h / 2 + (10 if mono else 7), value, role, AMBER if (warn and not light) else ink, size=vsize)
    return group(name or uid("HudChip"), b)


def team_marker(cx, cy, team="you", size=10, name=None):
    """You / your firm = cyan diamond, rival firm = amber triangle; both keylined in navy (shape + colour)."""
    g = diamond(cx, cy, size, CYAN, NAVY, 2) if team == "you" else triangle(cx, cy, size, AMBER, NAVY, 2)
    return group(name or uid(f"TeamMarker-{team}"), g)


def progress_ticks(x, y, reached, team="you", total=None, checkpoints=(), seg=14, gap=3, stale=False,
                   name=None):
    """A horizontal floor ladder of `total` (default FLOORS = 12: lobby, 1-9, boardroom, roof) square segments,
    cyan for you, amber for rival; floor N = segment N. Segments below `reached` filled, `reached` itself filled
    (hatched outline if stale), the rest open outlines."""
    total = total or FLOORS
    col = CYAN if team == "you" else AMBER
    b = ""
    for i in range(total):
        sx = x + i * (seg + gap)
        if i < reached:
            b += rect(sx, y, seg, 10, col)
        elif i == reached:
            b += (rect(sx + 0.5, y + 0.5, seg - 1, 9, None, col, 1) + line(sx, y + 10, sx + seg, y, col, 1)) if stale \
                else rect(sx, y, seg, 10, col)
        else:
            b += rect(sx + 0.5, y + 0.5, seg - 1, 9, None, (LIGHT, 0.4), 1)
        if i in checkpoints:
            b += rect(sx + seg + gap / 2 - 0.5, y - 6, 1, 4, (LIGHT, 0.8)) + rect(sx + seg + gap / 2 - 0.5, y + 12, 1, 4, (LIGHT, 0.8))
    return group(name or uid(f"FloorLadder-{team}"), b)


def race_tracker(x, y, you_floor, rival_floor, rival_note, you_label=None, name=None):
    """DEPRECATED (2026-11-12): the two-ladder rival-progress readout is dead under the briefcase rules.
    Use briefcase_tracker() and open_floor(). Kept only so old generators still import."""
    w, h = 520, 72
    b = glass_pane(x, y, w, h, "smoked", name=uid("TrackerGlass"))
    b += rect(x, y, w, 1, CYAN)
    b += diamond(x + 20, y + 21, 7, CYAN, None)
    b += progress_ticks(x + 36, y + 16, you_floor, "you", name=uid("LadderYou"))
    b += text(x + 306, y + 28, you_label or f"FLOOR {you_floor}", "hud", LIGHT, size=18)
    b += triangle(x + 20, y + 52, 7, AMBER, None)
    b += progress_ticks(x + 36, y + 47, rival_floor, "rival", stale=True, name=uid("LadderRival"))
    b += text(x + 306, y + 58, rival_note, "label", SILVER)
    return group(name or uid("RaceTracker"), b)


def crosshair(cx, cy, state="default", name=None):
    """Cross + dot: paper core, 1 px Near Black edge (two-tone over every background).
    Arms 2 x 6 px, 4 px gap, 2 px dot. state: default | hit (diagonal ticks at 8-13 px, 100 ms) | rival
    (core amber with a navy edge while the reticle is over a rival). Drawn in physical pixels in game."""
    core = AMBER if state == "rival" else LIGHT
    edge = NAVY if state == "rival" else BLACK
    b = ""
    for dx, dy, w, h in ((-10, -1, 6, 2), (4, -1, 6, 2), (-1, -10, 2, 6), (-1, 4, 2, 6), (-1, -1, 2, 2)):
        b += rect(cx + dx - 1, cy + dy - 1, w + 2, h + 2, edge)
    for dx, dy, w, h in ((-10, -1, 6, 2), (4, -1, 6, 2), (-1, -10, 2, 6), (-1, -1, 2, 2), (-1, 4, 2, 6)):
        b += rect(cx + dx, cy + dy, w, h, core)
    if state == "hit":
        for sx, sy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
            x1, y1 = cx + sx * 8, cy + sy * 8
            x2, y2 = cx + sx * 13, cy + sy * 13
            b += line(x1, y1, x2, y2, BLACK, 4) + line(x1 + sx * 0.7, y1 + sy * 0.7, x2 - sx * 0.7, y2 - sy * 0.7, LIGHT, 2)
    return group(name or uid(f"Crosshair-{state}-"), b)


def _prompt_parts(verb, obj):
    return verb, (" · " + obj if obj else "")


def interact_prompt_width(verb, obj=None, key="F", state="ready", reason=None):
    kw_ = max(32, int(text_width(key, "hud", size=16) + 18))
    v, o = _prompt_parts(verb, obj)
    w = 12 + kw_ + 12 + text_width(v, "item") + (text_width(o, "item") if o else 0) + 16
    if state == "blocked" and reason:
        w += 26 + 16 + text_width(reason, "item")
    return w


def interact_prompt(x, y, verb, obj=None, key="F", state="ready", reason=None, name=None):
    """Contextual prompt under the crosshair on a 48 px white plate: keycap + verb (navy 20) + " · object"
    (ink-2 20). Reads "F · Pick up · AR-15". Interact is F (owner, 2026-11-12; E is Lean right).
    state "blocked": dashed keycap, verb and object in ink-2, then an inner paper plate with a 1 px near-black
    edge, a navy lock and the reason in navy (e.g. "Hands full") -- never colour alone.
    Width: interact_prompt_width(...)."""
    kw_ = max(32, int(text_width(key, "hud", size=16) + 18))
    v, o = _prompt_parts(verb, obj)
    w = interact_prompt_width(verb, obj, key, state, reason)
    blocked = state == "blocked"
    kind, ink, ink2, light = _hud_look()
    b = glass_pane(x, y, w, 48, kind, name=uid("PromptGlass"))
    if blocked:
        b += rect(x + 12.75, y + 8.75, kw_ - 1.5, 30.5, None, (ink, 0.6), 1.5, dash="3 2")
        b += text(x + 12 + kw_ / 2, y + 30, key, "hud", ink2, size=16, anchor="middle")
    else:
        cap, _ = keycap(x + 12, y + 8, key, not light)
        b += cap
    tx = x + 12 + kw_ + 12
    b += text(tx, y + 31, v, "item", ink2 if blocked else ink)
    tx += text_width(v, "item")
    if o:
        b += text(tx, y + 31, o, "item", ink2)
        tx += text_width(o, "item")
    if blocked and reason:
        rw = w - (tx + 10 - x) - 6
        if light:
            b += rect(tx + 10, y + 6, rw, 36, PAPER) + rect(tx + 10.5, y + 6.5, rw - 1, 35, None, BLACK, 1)
            b += lock_glyph(tx + 26, y + 26, 6, NAVY)
            b += text(tx + 42, y + 31, reason, "item", NAVY)
        else:
            b += rect(tx + 10, y + 6, rw, 36, NAVY)
            b += lock_glyph(tx + 26, y + 26, 6, LIGHT)
            b += text(tx + 42, y + 31, reason, "item", LIGHT)
    return group(name or uid(f"Interact-{state}-"), b)


def stairs_prompt(x, y, floor, state="ready", key="F", name=None):
    """The stairwell door prompt. ready (the carrier): "F · Open floor 8" with a stairs pictogram, white plate.
    blocked (anyone without the briefcase): SOLID amber plate, navy stairs, dashed navy keycap, navy lock and
    "Open floor 8 · Briefcase required" in navy. 48 px tall."""
    verb = f"Open floor {floor}"
    kw_ = max(32, int(text_width(key, "hud", size=16) + 18))
    blocked = state == "blocked"
    reason = "Briefcase required"
    w = 12 + 48 + 12 + kw_ + 12 + text_width(verb, "item") + 16
    if blocked:
        w += 22 + 24 + text_width(reason, "item") + 4
    kind, ink, ink2, light = _hud_look(blocked)
    b = glass_pane(x, y, w, 48, kind, alpha=WARNING_A if (blocked and not light) else None, name=uid("StairsGlass"))
    if blocked and not light:
        b += rect(x, y, w, 2, AMBER)
    b += pictogram("stairs", x + 12, y + 12, 1.2, ink if light else (SILVER if blocked else LIGHT))
    kx = x + 12 + 48 + 12
    if blocked:
        b += rect(kx + 0.75, y + 8.75, kw_ - 1.5, 30.5, None, (ink, 0.6), 1.5, dash="3 2")
        b += text(kx + kw_ / 2, y + 30, key, "hud", ink if light else SILVER, size=16, anchor="middle")
    else:
        cap, _ = keycap(kx, y + 8, key, not light)
        b += cap
    tx = kx + kw_ + 12
    b += text(tx, y + 31, verb, "item", ink if light else (SILVER if blocked else LIGHT))
    if blocked:
        tx += text_width(verb, "item") + 22
        b += lock_glyph(tx + 8, y + 26, 6, NAVY if light else AMBER)
        b += text(tx + 24, y + 31, reason, "item", NAVY if light else AMBER)
    return group(name or uid(f"Stairs-{state}-"), b)


def stairs_refusal(x, y, floor, key="F", name=None):
    """The "Briefcase required" refusal at the stairs: stairs_prompt(..., state="blocked")."""
    return stairs_prompt(x, y, floor, "blocked", key, name=name or uid("StairsRefusal"))


def item_slot(x, y, item=None, state="held", ammo=None, w=None, name=None):
    """The single hands slot (bottom right), 260 x 72 white plate: pictogram left of a hairline divider,
    item name (ink-2 label 16) over the ammo / count / action (navy mono 24).
    item: None = "Hands free" (an outlined empty slot) | a key of ITEMS: pistol, ar15, grenade_briefcase,
          office_chair, cubicle_panel, briefcase (carrying it: cyan diamond with a navy keyline, "Carrying").
    state: "held" | "out" (out of ammo: SOLID amber plate, navy hazard + "Out of ammo" over "0 / 90" in navy;
           the pictogram still names the weapon).
    ammo: override the ITEMS default text (e.g. "× 2", "0 / 90").
    Legacy call item_slot(x, y, slot_number, item, state, count) still works (slot number ignored)."""
    if isinstance(item, int):  # legacy positional call: (x, y, slot, item, state="ready", count=None)
        old_item, old_state, old_count = state, ammo if ammo is not None else "ready", w
        item = None if old_state == "empty" else old_item
        state = "out" if old_state == "out" else "held"
        ammo, w = old_count, None
    w = w or SIZE["slot_w"]
    h = SIZE["slot_h"]
    out = state == "out"
    kind, ink, ink2, light = _hud_look(out)
    b = glass_pane(x, y, w, h, kind, alpha=WARNING_A if (out and not light) else None, name=uid("SlotGlass"))
    b += _hud_rule(x, y, w, out, light)
    b += rect(x + 76, y + 14, 1, h - 28, HAIR if light else HAIR_DARK)
    tx = x + 92
    if item is None:
        b += rect(x + 16.5, y + 20.5, 43, 31, None, (ink, 0.5), 1, dash="4 3")
        b += text(tx, y + 44, "Hands free", "item", ink2)
        return group(name or uid("Slot-empty-"), b)
    label, default = ITEMS[item]
    val = ammo or default
    b += pictogram(item, x + 12, y + 21, 1.3, ink)
    if out:
        hot = NAVY if light else AMBER
        b += warn_glyph(tx + 8, y + 31, 7, hot)
        b += text(tx + 22, y + 36, "Out of ammo", "label", hot)
        b += text(tx, y + 62, val, "hud", hot, size=24)
    else:
        b += text(tx, y + 36, label, "label", ink2)
        if item == "briefcase":
            b += diamond(tx + 8, y + 54, 7, CYAN, NAVY if light else None)
            b += text(tx + 22, y + 62, val, "item", ink)
        else:
            b += text(tx, y + 62, val, "hud" if any(ch.isdigit() for ch in val) else "item", ink,
                      size=24 if any(ch.isdigit() for ch in val) else 20)
    return group(name or uid(f"Slot-{item}-{state}-"), b)


def item_strip(x, y, items, held=0, name=None):
    """DEPRECATED (2026-11-12): there is one hands slot now. Draws item_slot() for items[held] only.
    items: [(item_key, state), ...] as before."""
    it, st = items[held]
    return group(name or uid("ItemSlots"), item_slot(x, y, it, "out" if st == "out" else "held"))


_CASE_COPY = {
    "you": "You carry the briefcase",
    "team": "Your firm has the briefcase",
    "rival": "Rival firm has the briefcase",
    "dropped": "Briefcase dropped",
    "returning": "Briefcase returning",
}
CASE_STATES = tuple(_CASE_COPY)


def briefcase_tracker(x, y, state="team", tower=None, floor=None, countdown=None, w=None, name=None):
    """The objective readout (top centre): where the ONE briefcase is. 64 px plate, two lines:
    navy item 20 headline + ink-2 mono 16 location ("YOUR TOWER · FLOOR 7").
    state: you (cyan diamond on the case) | team (your firm, diamond) | rival (SOLID amber plate, navy headline,
           a paper triangle with a navy keyline) | dropped (dashed case, tower + floor) | returning (dashed case,
           countdown).
    tower: "your" | "rival"; floor: int; countdown: "0:12" (returning)."""
    rival = state == "rival"
    head = _CASE_COPY[state]
    if state == "returning":
        sub = f"BACK IN PLAY IN {countdown or '0:12'}"
    else:
        tw = {"your": "YOUR TOWER", "rival": "RIVAL TOWER"}.get(tower or "your", "YOUR TOWER")
        sub = f"{tw} · FLOOR {floor}" if floor is not None else tw
    h = 64
    tx = x + 88
    w = w or int(88 + max(text_width(head, "item"), text_width(sub, "hud", size=16, ls=1)) + 20)
    kind, ink, ink2, light = _hud_look(rival)
    b = glass_pane(x, y, w, h, kind, alpha=WARNING_A if (rival and not light) else None, name=uid("CaseGlass"))
    b += _hud_rule(x, y, w, rival, light)
    dashed = state in ("dropped", "returning")
    if dashed:
        b += rect(x + 12.5, y + 16.5, 51, 33, None, (ink, 0.7), 1, dash="3 2")
    b += pictogram("briefcase", x + 18, y + 20, 1.0, ink)
    if state in ("you", "team"):
        b += diamond(x + 66, y + 18, 7, CYAN, NAVY, 1.5)
    elif rival:
        b += triangle(x + 66, y + 19, 7, PAPER if light else AMBER, NAVY, 1.5)
    b += rect(x + 76, y + 14, 1, h - 28, (NAVY, 0.4) if light else HAIR_DARK)
    b += text(tx, y + 30, head, "item", ink if light else (AMBER if rival else LIGHT))
    b += text(tx, y + 52, sub, "hud", ink if (rival and light) else ink2, size=16, ls=1)
    return group(name or uid(f"BriefcaseTracker-{state}-"), b)


def briefcase_chip(x, y, state="team", where=None, w=None, name=None):
    """DEPRECATED alias of briefcase_tracker() (states you / team / rival / dropped). where="Floor 7 · 18 m"
    style text is no longer drawn; pass tower / floor to briefcase_tracker instead."""
    return briefcase_tracker(x, y, state, w=w, name=name)


def briefcase_marker(cx, cy, state="team", dist=None, countdown=None, name=None):
    """In-world marker over the briefcase (screen-space, per frame). Square frame 48 px with a 5 px navy
    edge (two-tone), briefcase pictogram (paper core, navy keyline), owner glyph at the top-right corner,
    and a white plate tag 28 px under it with the distance (navy mono 16) or the countdown.
    state: team (cyan diamond: your firm carries it) | rival (amber triangle) | dropped (dashed frame)
           | returning (dashed frame, countdown "0:12" instead of distance).
    There is no marker over yourself ("you"): the hands slot and tracker say it. Footprint ~ 72 x 96."""
    col = {"you": CYAN, "team": CYAN, "rival": AMBER, "dropped": LIGHT, "returning": LIGHT}[state]
    s = 24
    dashed = state in ("dropped", "returning")
    b = rect(cx - s - 2, cy - s - 2, 2 * s + 4, 2 * s + 4, None, NAVY, 5)
    b += rect(cx - s, cy - s, 2 * s, 2 * s, None, col, 2, dash="6 4" if dashed else None)
    b += pictogram("briefcase", cx - 16, cy - 10, 0.8, LIGHT, NAVY)
    if state in ("you", "team"):
        b += diamond(cx + s, cy - s, 8, CYAN, NAVY, 2)
    elif state == "rival":
        b += triangle(cx + s, cy - s, 8, AMBER, NAVY, 2)
    tag = countdown if state == "returning" else dist
    if tag:
        kind, ink, _, _ = _hud_look()
        tw = text_width(tag, "hud", size=16) + 20
        b += glass_pane(cx - tw / 2, cy + s + 8, tw, 28, kind, name=uid("DistTag"))
        b += text(cx, cy + s + 28, tag, "hud", ink, size=16, anchor="middle")
    return group(name or uid(f"BriefcaseMarker-{state}-"), b)


def briefcase_world(cx, cy, dist, state="rival", name=None):
    """DEPRECATED alias: briefcase_marker(cx, cy, state, dist)."""
    return briefcase_marker(cx, cy, state, dist, name=name)


def floor_name(n):
    """Floor label for segment n of the 12-segment tower: 0 LOBBY, 1-9 FLOOR n, 10 BOARDROOM, 11 ROOF."""
    return {0: "LOBBY", FLOORS - 2: "BOARDROOM", FLOORS - 1: "ROOF"}.get(n, f"FLOOR {n}")


def open_floor(x, y, floor, holder="rival", total=None, w=None, name=None):
    """The open-floor readout: nobody climbs above it. 64 px white plate:
    "OPEN FLOOR 7" (navy mono 24; "OPEN · BOARDROOM" / "OPEN · ROOF" at 10 / 11) + ink-2 label
    "Briefcase in the rival tower" with the holder's glyph, and the tower ladder at the right:
    `total` segments (default FLOORS = 12: L lobby, 1-9, B boardroom, R roof), floor N = segment N.
    Floors below the open one filled cobalt, the open floor cyan with a navy keyline and navy snapped brackets,
    locked floors as open navy outlines; L, B and R are named under their segments (mono 16, ink-2).
    holder: "your" | "rival"."""
    total = total or FLOORS
    h = 64
    head = f"OPEN FLOOR {floor}" if 1 <= floor <= total - 3 else "OPEN · " + floor_name(floor)
    sub = "Briefcase in your tower" if holder == "your" else "Briefcase in the rival tower"
    seg, gap = 10, 4
    lad_w = total * (seg + gap) - gap
    tx = x + 16
    w = w or int(16 + max(text_width(head, "hud", size=24), 22 + text_width(sub, "label")) + 28 + lad_w + 24)
    kind, ink, ink2, light = _hud_look()
    b = glass_pane(x, y, w, h, kind, name=uid("OpenFloorGlass"))
    b += _hud_rule(x, y, w, False, light)
    b += text(tx, y + 30, head, "hud", ink, size=24)
    key = NAVY if light else None
    if holder == "your":
        b += diamond(tx + 7, y + 47, 6, CYAN, key)
    else:
        b += triangle(tx + 7, y + 48, 6, AMBER, key)
    b += text(tx + 22, y + 53, sub, "label", ink2)
    lx = x + w - 24 - lad_w
    ly = y + 20
    done = COBALT if light else LIGHT
    for i in range(total):
        sx = lx + i * (seg + gap)
        if i < floor:
            b += rect(sx, ly, seg, 14, done)
        elif i == floor:
            b += rect(sx, ly, seg, 14, CYAN, NAVY if light else None, 1)
        else:
            b += rect(sx + 0.5, ly + 0.5, seg - 1, 13, None, (ink, 0.45), 1)
    fx = lx + floor * (seg + gap)
    b += brackets(fx, ly, seg, 14, 5, 1.5, NAVY if light else CYAN, gap=3)
    for i, letter in ((0, "L"), (total - 2, "B"), (total - 1, "R")):
        b += text(lx + i * (seg + gap) + seg / 2, y + 56, letter, "hud", ink2, size=16, anchor="middle")
    return group(name or uid("OpenFloor"), b)


def toast(x, y, headline, body, w=None, tone="normal", numeral=None, name=None):
    """Transient centre banner, 84 px, on a white plate: display caps headline 26 (navy) + ink-2 body 20, a 4 px
    cobalt spine at the left. tone="warning": SOLID amber plate, navy headline and body, navy spine (rival /
    threat events). numeral: an oversized UltraLight floor numeral (silver; navy 16 % on amber) at the right end.
    Example: toast(x, y, "FLOOR 8 OPENED", "Your firm carried the briefcase to floor 8.", numeral=8)."""
    warn = tone == "warning"
    num_w = 96 if numeral is not None else 0
    w = w or int(max(text_width(headline, "heading", size=26, ls=3), text_width(body, "item")) + 64 + num_w)
    h = 84
    kind, ink, ink2, light = _hud_look(warn)
    if light:
        b = glass_pane(x, y, w, h, kind, name=uid("ToastGlass"))
        b += rect(x, y, 4, h, NAVY if warn else COBALT)
        num_fill = (NAVY, 0.16) if warn else SILVER
        head_fill, body_fill = ink, (ink if warn else ink2)
    else:
        b = glass_pane(x, y, w, h, kind, streak=not warn, streak_at=0.6, alpha=WARNING_A if warn else None,
                       name=uid("ToastGlass"))
        b += rect(x, y, 4, h, AMBER if warn else CYAN)
        num_fill = (LIGHT, 0.18) if not warn else (AMBER, 0.22)
        head_fill, body_fill = (AMBER if warn else LIGHT), SILVER
    if numeral is not None:
        with _quiet():
            b += clip(x, y, w, h, text(x + w - num_w + 4, y + h + 18, str(numeral), "floor", num_fill, size=128, ls=-2))
    b += text(x + 28, y + 38, headline, "heading", head_fill, size=26, ls=3)
    b += text(x + 28, y + 68, body, "item", body_fill)
    return group(name or uid("Toast"), b)


def status_line(x, y, msg, state="busy", dark=False, name=None):
    """One plain line (body 18) naming every wait or failure. busy = hollow square, ok = filled,
    error = hazard. Over the world, use toast / hud_chip instead."""
    ink = LIGHT if dark else NAVY
    mark = CYAN if dark else COBALT
    if state == "ok":
        b = rect(x, y - 13, 10, 10, mark)
    elif state == "error":
        b = warn_glyph(x + 5, y - 7, 7, AMBER if dark else NAVY)
    else:
        b = rect(x + 0.75, y - 12.25, 8.5, 8.5, None, mark, 1.5)
    fill = ink if state != "busy" else (SILVER if dark else INK2)
    b += text(x + 22, y, msg, "body", fill)
    return group(name or uid(f"StatusLine-{state}-"), b)


def modal_confirm(x, y, w, title, body, confirm, cancel, focus="cancel", under=None, name=None,
                  cancel_w=200, confirm_w=None):
    """Frosted confirm, 204 px. The safe choice (e.g. "Stay") is the primary button and holds focus; the
    destructive choice is secondary and never focused by default. Esc = the safe choice (hint bottom-left).
    Leave >= 10 px around it for the focus ring."""
    h = 204
    b = glass_pane(x, y, w, h, "frosted", streak=True, streak_at=0.86, under=under, name=uid("ModalGlass"))
    b += rect(x, y, w, 6, COBALT)
    b += text(x + 32, y + 58, title, "item", NAVY, size=26)
    b += text(x + 32, y + 92, body, "body", INK2)
    confirm_w = confirm_w or int(text_width(confirm, "item") + 48)
    by = y + h - 32 - 56
    b += button(x + w - 32 - cancel_w, by, cancel_w, cancel, "primary", "focus" if focus == "cancel" else "default",
                h=56, name=uid("ModalSafe"))
    b += button(x + w - 32 - cancel_w - 24 - confirm_w, by, confirm_w, confirm, "secondary",
                "focus" if focus == "confirm" else "default", name=uid("ModalRisky"))
    cap, kw_ = keycap(x + 32, by + 12, "Esc", False, 16, 32)
    b += cap + text(x + 32 + kw_ + 10, by + 34, cancel, "label", INK2)
    return group(name or uid("ModalConfirm"), b)

# ---------------------------------------------------------------------------
# 12. Backdrops: painted vector stand-ins for the in-engine captures (ArtSource/Look/review/)
# ---------------------------------------------------------------------------
# Painted in the game's vocabulary as the captures show it: pale blue-white walls and ceiling, thin navy
# window frames, a mid-blue floor with hard white sun slabs, cobalt sky with stepped clouds, capsule
# stand-in players as angular navy figures. Not recorded by the layout registry.


def _clouds(x0, x1, base, amp, step, seed, fill="@LIGHT", shade="@SILVER"):
    """Flat stepped cloud bank (thresholded cel): heights quantised to `step`."""
    rnd = random.Random(seed)
    ph = [rnd.uniform(0, 6.28) for _ in range(3)]
    pts, tops = [(x0, base + amp * 2)], []
    x = x0
    while x <= x1:
        v = math.sin(x / 140 + ph[0]) + 0.6 * math.sin(x / 61 + ph[1]) + 0.3 * math.sin(x / 23 + ph[2])
        yy = base - round((v + 1.9) / 3.8 * amp / step) * step
        pts += [(x, yy), (x + step * 2, yy)]
        tops.append((x, yy))
        x += step * 2
    pts.append((x1, base + amp * 2))
    b = poly(pts, fill)
    sp = [(x0, base + amp * 2)] + [(px, py + amp * 0.55 + step) for px, py in tops] + [(x1, base + amp * 2)]
    return b + poly(sp, (shade, 0.55))


def _suit(cx, base, s, fill="@NAVY"):
    """Angular suited figure (head block + cut shoulders), no curves."""
    return (poly([(cx - 5 * s, base - 30 * s), (cx + 5 * s, base - 30 * s), (cx + 6 * s, base - 22 * s),
                  (cx + 3 * s, base - 17 * s), (cx - 3 * s, base - 17 * s), (cx - 6 * s, base - 22 * s)], fill) +
            poly([(cx - 4 * s, base - 16 * s), (cx + 4 * s, base - 16 * s), (cx + 12 * s, base - 12 * s),
                  (cx + 13 * s, base), (cx - 13 * s, base), (cx - 12 * s, base - 12 * s)], fill))


def _tower(x, y, w, h, band=34, gap=52, figures=(), wash=0.0):
    b = rect(x, y, w, h, PAPER) + rect(x + w * 0.82, y, w * 0.18, h, (SILVER, 0.7))
    yy, row = y + 40, 0
    while yy + band < y + h:
        b += rect(x + 12, yy, w - 24, band, (NAVY, 0.85)) + rect(x + 12, yy, w - 24, band * 0.45, (COBALT, 0.55))
        for k in range(1, 6):
            b += rect(x + 12 + k * (w - 24) / 6 - 2, yy, 4, band, PAPER)
        for fx, frow in figures:
            if frow == row:
                b += _suit(x + fx * w, yy + band, band / 40, BLACK)
        yy += band + gap
        row += 1
    if wash:
        b += rect(x, y, w, h, (LIGHT, wash))
    return b


def _backdrop_office_sun():
    """01_sun_patches: pale walls, window wall onto the rival tower, mid-blue floor, white sun slabs."""
    b = rect(0, 0, 1920, 1080, PAPER) + rect(0, 0, 1920, 1080, (ELECTRIC, 0.10))
    b += rect(0, 150, 1920, 470, "url(#skyDay)") + rect(0, 150, 1920, 470, (LIGHT, 0.30))
    b += _clouds(0, 1920, 520, 80, 16, 3)
    b += _tower(820, 170, 620, 560, figures=((0.22, 0), (0.47, 1), (0.71, 0), (0.34, 2)), wash=0.25)
    b += _tower(1560, 300, 300, 400, band=26, gap=40, wash=0.45)
    b += rect(0, 150, 1920, 470, (LIGHT, 0.22))  # glass tint
    for k in range(9):  # window wall mullions: thin navy frames on white columns
        b += rect(k * 240 - 14, 150, 28, 470, PAPER) + rect(k * 240 - 14, 150, 4, 470, (NAVY, 0.8))
    b += rect(0, 140, 1920, 12, (NAVY, 0.85)) + rect(0, 606, 1920, 16, PAPER) + rect(0, 612, 1920, 6, (NAVY, 0.8))
    b += rect(0, 372, 1920, 6, (NAVY, 0.6))
    for k in range(5):  # ceiling tile grid and light rows
        b += rect(0, 18 + k * 26, 1920, 2, (SILVER, 0.8)) + rect(160 + k * 340, 30 + k * 4, 220, 8, LIGHT)
    b += rect(0, 622, 1920, 458, COBALT) + rect(0, 622, 1920, 458, (PAPER, 0.32)) + rect(0, 622, 1920, 458, "url(#floorFade)")
    for k in range(12):  # floor tile seams
        b += rect(0, 640 + k * 38, 1920, 1, (NAVY, 0.10))
    for ax, bx, cx_, dx in ((380, 900, 1180, 160), (980, 1500, 1880, 1300)):  # sun slabs, stepped far edge
        pts, yy, xx = [(ax, 640), (bx, 640)], 640, bx
        while yy < 1000:
            yy += 12
            xx += (cx_ - bx) * 12 / 360
            pts += [(round(xx), yy - 12), (round(xx), yy)]
        pts += [(dx, 1000), (ax - 120, 640)]
        b += poly(pts, (PAPER, 0.72))
    b += rect(0, 760, 1920, 18, (NAVY, 0.18)) + rect(0, 880, 1920, 22, (NAVY, 0.18))
    b += _suit(1560, 1040, 9, NAVY)
    return b


def _backdrop_sky_bloom():
    """03_outside: worst case for light text. Near-white bloom over cobalt sky, cloud sea, tower faces."""
    b = rect(0, 0, 1920, 1080, "url(#skyBloom)")
    b += _tower(1420, 120, 380, 900, band=30, gap=48, figures=((0.3, 2), (0.65, 4)), wash=0.35)
    b += _tower(80, 260, 260, 760, band=26, gap=44, wash=0.55)
    b += _clouds(0, 1920, 760, 120, 16, 11)
    b += rect(0, 860, 1920, 220, LIGHT)
    b += rect(-400, -300, 2700, 1700, "url(#bloom)")
    return b + hairline_grid(0, 0, 1920, 1080, dark=True, perspective=True, name=uid("SkyGrid"))


def _backdrop_navy_dark():
    """Service room / a navy suit filling the frame: worst case for dark text on frosted glass."""
    b = rect(0, 0, 1920, 1080, NAVY) + rect(0, 0, 1920, 120, (LIGHT, 0.06)) + rect(0, 760, 1920, 320, BLACK)
    b += rect(260, 200, 300, 560, COBALT) + rect(280, 220, 260, 520, (NAVY, 0.25))
    b += rect(0, 118, 1920, 3, CYAN) + rect(1100, 330, 6, 6, CYAN) + rect(1114, 330, 6, 6, CYAN)
    for k in range(6):
        b += rect(700 + k * 70, 120, 2, 640, (LIGHT, 0.05))
    b += poly([(620, 1080), (700, 560), (980, 470), (1300, 510), (1520, 680), (1620, 1080)], BLACK)
    b += poly([(980, 470), (1140, 470), (1100, 680), (1040, 1080), (920, 1080)], NAVY)
    b += poly([(800, 580), (980, 470), (920, 1080), (780, 1080)], (LIGHT, 0.07))
    return b + poly([(1050, 290), (1190, 290), (1220, 380), (1170, 470), (1070, 470), (1020, 380)], BLACK)


_BACKDROPS = {"office_sun": _backdrop_office_sun, "sky_bloom": _backdrop_sky_bloom, "navy_dark": _backdrop_navy_dark}
BACKDROP_KINDS = tuple(_BACKDROPS)


def backdrop(kind="office_sun", x=0, y=0, w=1920, h=1080, crop=None, name=None):
    """Painted stand-in of the game view, drawn in 1920 x 1080 space and fitted into (x, y, w, h).
    crop=(cx, cy, cw, ch) picks a window of the 1920 x 1080 view instead of the whole frame.
    kind: "office_sun"  pale office, hard sun slabs, window wall onto the rival tower with figures in it
          "sky_bloom"   worst case for light text: near-white bloom over cobalt sky and cloud sea
          "navy_dark"   service room / navy suit close: worst case for dark text"""
    with _quiet():
        b = _BACKDROPS[kind]()
        cx, cy, cw, ch = crop or (0, 0, 1920, 1080)
        sx, sy = w / cw, h / ch
        t = f"translate({_f(float(x - cx * sx))} {_f(float(y - cy * sy))}) scale({_f(round(sx, 5))} {_f(round(sy, 5))})"
        out = group(name or uid(f"Backdrop-{kind}-"), clip(x, y, w, h, f'<g transform="{t}">{b}</g>'))
    _reg("shapes", x, y, x + w, y + h, backdrop=True)
    return out


# ---------------------------------------------------------------------------
# 13. Pass-A rules and the hero screen (both overridable by a theme)
# ---------------------------------------------------------------------------


def layout_rules(items):
    """Theme-specific layout checks run by make_kit after the generic ones. items: make_kit Board items
    (dicts with name, box, texts, panes, ground). Returns a list of error strings.
    All themes: silver text never sits in the top 30 % sheen band of a smoked pane (4.08:1 there).
    Pass A (survey) also: no amber or cyan text anywhere; the innermost pane under a text decides its colour:
    light kinds (plate, frosted, amber) take navy-family text, never paper / silver; nothing is set on clear glass."""
    errs = []
    panes = [p for it in items for p in it["panes"]]

    def inside(tb, pb):
        return tb[0] >= pb[0] - 0.5 and tb[1] >= pb[1] - 0.5 and tb[2] <= pb[2] + 0.5 and tb[3] <= pb[3] + 0.5
    for it in items:
        for t in it["texts"]:
            tb = t["box"]
            fill = _hex(t["fill"])
            for p in panes:
                pb = p["box"]
                if (p["kind"] == "smoked" and not p["solid"] and fill == SILVER and inside(tb, pb)
                        and tb[1] < pb[1] + p["band"]):
                    errs.append(f"silver text in a smoked sheen band: '{t['s']}' ({it['name']})")
            if theme() != "survey":
                continue
            if fill in (AMBER, CYAN):
                errs.append(f"{'amber' if fill == AMBER else 'cyan'} text (pass A never sets it): '{t['s']}' ({it['name']})")
            under = [p for p in panes if inside(tb, p["box"])]
            if not under:
                continue
            inner = min(under, key=lambda p: (p["box"][2] - p["box"][0]) * (p["box"][3] - p["box"][1]))
            if inner["kind"] == "clear":
                errs.append(f"text set on clear glass (put a plate under it): '{t['s']}' ({it['name']})")
            elif inner["kind"] in LIGHT_KINDS and fill in (_hex(LIGHT), SILVER):
                errs.append(f"light text on a light pane ({inner['kind']}): '{t['s']}' ({it['name']})")
    return errs


def _persp_x(x, y, vx, vy, base_y):
    """x of a vertical frame line through (x, base_y), at height y, converging on (vx, vy) (looking up)."""
    return vx + (x - vx) * (y - vy) / (base_y - vy)


def hero_screen(name=None):
    """The main-menu title screen at 1920 x 1080, drawn over the scene (make_hero.py writes it).
    Pass A composition (the window-frame device): the upper 62 % is a white window frame seen from below, its
    mullions dividing a deep-blue sky with stepped cel cumulus into six panes; the rival tower rises in the right
    pane and carries the screen's ONE trace. Below the sill, on paper: the thin wide title lockup and the Steam
    line, the status and join hint, the menu (Host game focused, with a chevron cue), microtype and a barcode.
    A theme replaces the whole composition by overriding hero_screen."""
    b = rect(0, 0, 1920, 1080, PAPER)
    b += hairline_grid(0, 0, 1920, 1080, faint=True, name="HeroGrid")
    base, vx, vy = 664, 960, -5000
    top = -12
    cols = ((56, 616), (664, 1256), (1304, 1864))
    rows_y = ((top, 292), (332, base))
    panes = []
    for l, r in cols:
        for y0, y1 in rows_y:
            panes.append([(_persp_x(l, y0, vx, vy, base), y0), (_persp_x(r, y0, vx, vy, base), y0),
                          (_persp_x(r, y1, vx, vy, base), y1), (_persp_x(l, y1, vx, vy, base), y1)])
    cid = uid("heroPanes")
    shapes = "".join('<polygon points="' + " ".join(f"{_f(round(px, 1))},{_f(round(py, 1))}" for px, py in q) + '"/>'
                     for q in panes)
    with _quiet():
        sky = rect(0, top, 1920, base - top, "url(#skyDeep)") + rect(0, top, 1920, base - top, "url(#skyHaze)")
        sky += halftone(96, 0, 500, 176, "down", (LIGHT, 0.5), cell=10, dmax=4, dmin=0, name="HeroHalftone")
        sky += _cumulus(1500, 640, 420, 150, seed=8, step=8)
        sky += _tower(1462, 352, 264, 400, band=26, gap=40, figures=((0.3, 1), (0.66, 3)), wash=0.12)
        sky += _cumulus(-40, 720, 780, 500, seed=5, step=8)
        sky += _cumulus(700, 690, 560, 210, seed=21, step=8)
        # one clear-glass sheen streak across the middle lower pane: the glazing is there, and see-through
        sky += poly([(980, base), (1050, base), (1180, 332), (1110, 332)], (LIGHT, 0.12))
        sky += poly([(1066, base), (1076, base), (1206, 332), (1196, 332)], (LIGHT, 0.12))
    b += f'<clipPath id="{cid}">{shapes}</clipPath><g id="HeroSky" clip-path="url(#{cid})">{sky}</g>'
    _reg("shapes", 0, 0, 1920, base)
    # frame depth: silver reveals on the top and left inner edge of every pane, and the sill's shadow
    with _quiet():
        rev = ""
        for q in panes:
            (x0, y0), (x1, _), (x2, y2), (x3, _) = q
            rev += poly([(x0, y0), (x1, y0), (x1 + 2, y0 + 10), (x0 + 2, y0 + 10)], (SILVER, 0.85))
            rev += poly([(x0, y0), (x0 + 9, y0 + 10), (x3 + 9, y2), (x3, y2)], (SILVER, 0.6))
        rev += rect(0, base, 1920, 10, (SILVER, 0.7)) + rect(0, base + 10, 1920, 2, (NAVY, 0.12))
        for cx in (640, 1280):   # sash latches on the two inner mullions
            lx = _persp_x(cx, 480, vx, vy, base)
            rev += rect(lx - 6, 462, 12, 36, PAPER) + rect(lx - 5.5, 462.5, 11, 35, None, HAIR_STRONG, 1)
    b += group("HeroFrame", rev)
    b += trace_brackets(1484, 372, 222, 236, NAVY, seed=13, density=0.55)
    b += line(112, 712.5, 1808, 712.5, BLACK, 1)   # the one black accent: a hairline ground line under the sill
    # left column: the title, low on white, and who is signed in
    x = 112
    b += title_lockup(x, 752, size=64, name="HeroTitle")
    b += square_bullet(x, 899, 8, COBALT) + text(x + 20, 908, "Signed in to Steam as Dana", "label", NAVY)
    # middle column: status and join hint
    mx = 832
    b += status_line(mx, 790, "Ready. Host a game, or join a friend from Steam.", "ok", name="HeroReady")
    hint, _ = text_lines(mx + 22, 830, "To join a friend, accept their invite or choose Join Game on their name "
                         "in Steam.", 400, "body", INK2)
    b += hint
    # right column: the menu, the focused item cued by a chevron row
    bx, bw = 1388, 420
    b += chevron_row(1326, 784, 3, "right", s=7, gap=14, color=NAVY, name="HeroFocusCue")
    b += button(bx, 752, bw, "Host game", "primary", "focus", name="HeroHost")
    b += button(bx, 840, bw, "Settings", "secondary", "default", name="HeroSettings")
    b += button(bx, 912, bw, "Quit to desktop", "quiet", "default", name="HeroQuit")
    # footer
    b += microtype_strip(x, 1024, "CTD-0117-B · § 1 Main menu · Terms and Conditions Apply.", name="HeroMicro")
    b += barcode(1648, 1004, 160, 22, seed=11, name="HeroBarcode")
    b += path("M48 1004V1032H76M1872 1004V1032H1844", None, NAVY, 1.5)
    return group(name or "HeroScreen", b)


# ---------------------------------------------------------------------------
# 14. Themes
# ---------------------------------------------------------------------------
# A theme is a Python module (a file anywhere, e.g. ArtSource/UI/passes/b_aero/theme_aero.py). set_theme()
# restores the pass-A base, applies the module, then recomputes every derived token. See README "Themes".

PASS_NAME = "A · SURVEY"
BOARD_NOTES = {}           # board key -> "Departs from the bible: ..." line (drawn in that board's DesignNotes)
ALLOW_RADIUS = False       # pass A: corner radius 0 everywhere (make_kit checks rx per theme)
RADIUS = 0                 # a theme's own corner radius token, for its overrides to use
_PALETTE_META = [(t, n, r) for t, _, n, r in PALETTE]
_THEME = {"name": "survey", "module": None, "added": []}
_NO_SNAPSHOT = {"REC", "_uid", "_FACES", "_INDEX", "MISSING_GLYPHS", "UNMEASURED", "METRICS_SOURCE", "_THEME",
                "_BASE", "_NO_SNAPSHOT", "TRACE_BOXES"}


def _derive(skip=()):
    """Recompute every token derived from the base swatches, alphas and families (unless the theme set it)."""
    g = globals()

    def put(k, v):
        if k not in skip:
            g[k] = v
    put("LIGHT", g["PAPER"])
    put("PALETTE", [(t, g[t], n, r) for t, n, r in _PALETTE_META])
    nv, lt = g["NAVY"], g["LIGHT"]
    put("INK2", (nv, 0.78))
    put("INK3", (nv, 0.68))
    put("DISABLED", (nv, 0.45))
    put("HAIR", (nv, 0.24))
    put("HAIR_STRONG", (nv, 0.40))
    put("HAIR_DARK", (lt, 0.28))
    put("HOVER_SHADE", (nv, 0.20))
    put("SMOKED_SVG", svg_alpha_for(nv, g["SMOKED_A"]))
    put("COBALT_SVG", svg_alpha_for(g["COBALT"], g["COBALT_A"]))
    put("FROSTED_SVG", svg_alpha_for(g["PAPER"], g["FROSTED_A"]))
    put("PLATE_SVG", svg_alpha_for(g["PAPER"], g["PLATE_A"]))
    put("CLEAR_SVG", svg_alpha_for(g["SILVER"], g["CLEAR_A"]))
    kinds = {"smoked": (nv, g["SMOKED_A"], g["SMOKED_SVG"], g["LIGHT"], "paper"),
             "cobalt": (g["COBALT"], g["COBALT_A"], g["COBALT_SVG"], g["LIGHT"], "paper"),
             "frosted": (g["PAPER"], g["FROSTED_A"], g["FROSTED_SVG"], nv, "navy"),
             "plate": (g["PAPER"], g["PLATE_A"], g["PLATE_SVG"], nv, "navy"),
             "clear": (g["SILVER"], g["CLEAR_A"], g["CLEAR_SVG"], nv, "navy"),
             "amber": (g["AMBER"], g["AMBER_A"], g["AMBER_A"], nv, "navy")}
    if "GLASS" in skip:   # a theme's own GLASS keeps its kinds and gains the new ones it lacks (API only grows)
        for k, v in kinds.items():
            g["GLASS"].setdefault(k, v)
    else:
        g["GLASS"] = kinds
    put("PROOF_BG", {"office_sun": (g["PAPER"], g["SILVER"], g["COBALT"], nv),
                     "sky_bloom": (g["BLOOM"], g["PAPER"], g["ELECTRIC"], g["COBALT"]),
                     "navy_dark": (nv, g["BLACK"], g["COBALT"])})
    D, U, T, M = g["DISPLAY"], g["UI_FACE"], g["UI_THIN"], g["DATA"]
    dw = g["DISPLAY_WEIGHT"]
    if set(skip) & {"DISPLAY", "UI_FACE", "UI_THIN", "DATA"}:   # a theme with its own faces keeps the pre-guide scale
        put("TYPE", {"micro": (M, 400, 11, 0.5), "caption": (M, 500, 13, 1.5), "label": (U, 500, 16, 0),
                     "body": (U, 400, 18, 0), "item": (U, 500, 20, 0), "hud": (M, 500, 28, 0),
                     "heading": (D, dw, 40, 3), "title": (D, dw, 104, 6), "floor": (T, 400, 240, -4)})
        put("TARGET_FACE", {D: "Michroma", U: "Archivo", T: "Archivo Expanded ExtraLight", M: "IBM Plex Mono"})
    else:
        put("TYPE", {"micro": (M, 300, 11, 0.8), "caption": (M, 400, 13, 1.5), "label": (U, 500, 16, 0),
                     "body": (U, 400, 18, 0), "item": (U, 500, 20, 0), "hud": (M, 400, 28, 0),
                     "heading": (D, dw, 40, 6), "title": (D, dw, 104, 23), "floor": (T, 400, 240, -4)})
        put("TARGET_FACE", {D: "Saira Expanded", U: "Archivo SemiExpanded", T: "Archivo Expanded ExtraLight",
                            M: "IBM Plex Mono"})
    put("FORCED_WEIGHT", {D: dw, T: 400})
    put("FAMILIES", tuple(sorted({v[0] for v in g["TYPE"].values()})))
    g["BACKDROP_KINDS"] = tuple(g["_BACKDROPS"])


def _snapshot():
    import copy
    import types
    out = {}
    for k, v in globals().items():
        if k.startswith("__") or k in _NO_SNAPSHOT or isinstance(v, types.ModuleType):
            continue
        out[k] = v if callable(v) else copy.deepcopy(v)
    return out


def _load_theme_module(t):
    import importlib.util
    if not isinstance(t, str):
        return t
    here = os.path.dirname(os.path.abspath(__file__))
    cands = [t] if t.endswith(".py") else [
        os.path.join(here, f"theme_{t}.py"),
        *sorted(__import__("glob").glob(os.path.join(here, "..", "passes", "*", f"theme_{t}.py")))]
    path_ = next((os.path.abspath(c) for c in cands if os.path.isfile(c)), None)
    if not path_:
        raise FileNotFoundError(f"no theme module for {t!r} (looked for {cands})")
    tdir = os.path.dirname(path_)
    if tdir not in sys.path:
        sys.path.insert(0, tdir)
    spec = importlib.util.spec_from_file_location("ctd_theme_" + re.sub(r"\W", "_", os.path.basename(path_)[:-3]),
                                                  path_)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = mod
    spec.loader.exec_module(mod)
    return mod


def set_theme(t=None):
    """Select the theme for everything drawn after this call. t: None / "survey" (the built-in pass A base),
    a theme name (tools/theme_<name>.py or passes/*/theme_<name>.py), a .py file path, or a module.
    Restores the base first, so switching themes never stacks. Returns the theme name."""
    g = globals()
    for k in _THEME["added"]:
        g.pop(k, None)
    import copy
    for k, v in _BASE.items():
        g[k] = v if callable(v) else copy.deepcopy(v)
    _THEME.update(name="survey", module=None, added=[])
    if t in (None, "survey", "a", "a_survey"):
        _derive()
        g["DEFS"] = _base_defs()
        return "survey"
    mod = _load_theme_module(t)
    tokens = dict(getattr(mod, "TOKENS", {}))
    for k in ("PASS_NAME", "BOARD_NOTES", "ALLOW_RADIUS", "RADIUS", "FAMILIES"):
        if hasattr(mod, k):
            tokens.setdefault(k, getattr(mod, k))
    if "GLASS" in tokens and "PROOF_KINDS" not in tokens:   # a theme's own glass: A3 proves its kinds, not pass A's
        tokens["PROOF_KINDS"] = tuple(list(tokens["GLASS"])[:4])
    if "DISPLAY" in tokens:   # pre-guide defaults for a theme with its own display face
        tokens.setdefault("DISPLAY_WEIGHT", 500)
        tokens.setdefault("TITLE_TRACK", 0.06)
    added = [k for k in tokens if k not in g]
    g.update(tokens)
    if "FONT_FILES_EXTRA" in tokens:
        g["FONT_FILES"] = {**g["FONT_FILES"], **tokens["FONT_FILES_EXTRA"]}
    _derive(skip=set(tokens))
    g["_BACKDROPS"].update(getattr(mod, "BACKDROPS", {}))
    g["BACKDROP_KINDS"] = tuple(g["_BACKDROPS"])
    for k, fn in getattr(mod, "OVERRIDES", {}).items():
        if k not in g:
            added.append(k)
        g[k] = fn
    base_defs = "" if getattr(mod, "REPLACE_DEFS", False) else _base_defs()[len("<defs>"):-len("</defs>")]
    extra = mod.defs(sys.modules[__name__]) if hasattr(mod, "defs") else getattr(mod, "DEFS_EXTRA", "")
    if "DEFS" not in tokens:
        g["DEFS"] = "<defs>" + base_defs + extra + "</defs>"
    name = getattr(mod, "NAME", getattr(mod, "__name__", "theme"))
    _THEME.update(name=name, module=mod, added=added)
    if hasattr(mod, "install"):
        mod.install(sys.modules[__name__])
    return name


def theme():
    """Current theme name ("survey" for pass A)."""
    return _THEME["name"]


def theme_module():
    """Current theme module, or None for the built-in pass A."""
    return _THEME["module"]


def base(name):
    """The pass-A (built-in) version of a public component or token, for a theme override to call:
    def glass_pane(*a, **kw): return ui.base("glass_pane")(*a, **kw) + extra. It still reads the current
    theme's tokens and resolves its own internal calls through the theme."""
    return _BASE[name]


def theme_hook(name, default=None):
    """An optional attribute of the current theme module (e.g. a board hook), else default."""
    mod = _THEME["module"]
    return getattr(mod, name, default) if mod is not None else default


def allowed_hex():
    """Every hex colour token of the current theme (base swatches plus the theme's extra hex tokens)."""
    g = globals()
    return sorted({v.upper() for k, v in g.items() if k.isupper() and k != "BLOOM" and isinstance(v, str)
                   and re.fullmatch(r"#[0-9A-Fa-f]{6}", v)})


FAMILIES = ()
_derive()
_BASE = _snapshot()
