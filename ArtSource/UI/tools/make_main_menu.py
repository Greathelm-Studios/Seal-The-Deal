"""Seal the Deal: main menu mockups, two variants for the owner to choose between (pass A, light glass).

    python3 ArtSource/UI/tools/make_main_menu.py               # from the repo root (or anywhere)
    python3 ArtSource/UI/tools/make_main_menu.py --no-preview  # skip the Quick Look PNGs
    python3 ArtSource/UI/tools/make_main_menu.py survey_05     # one board while iterating

Variants
  survey   The title frame (00a_hero_survey, ref 08): a white window frame seen from below divides a deep-blue
           sky with stepped cel cumulus into six panes; the rival tower in the right pane carries the one trace.
           Everything you read sits low on Paper White. The same frame is on every state; its sill rises when the
           content needs room (title 664, menus 616, busy and failures 500, lobby 360).
  curtain  The live office scene seen through a curtain wall of `clear` blue-grey glass bays in a white frame.
           Every word sits on a white `plate` inside a bay; clear glass carries none. Needs an engine blur pass.

Each variant has one 1920 x 1080 artboard per state (STATES below). Output, per artboard:
  ArtSource/UI/svg/main_menu/<board>/<board>_NN_Name.svg   NN = bottom-to-top place order
  <board>_01_SheetLabel.svg and <board>_99_DesignNotes.svg are annotation (hide them for the clean export).
  ArtSource/UI/svg/_preview/main_menu_<board>.png            clean (no sheet label, no notes)
  ArtSource/UI/svg/_preview/main_menu_<board>_annotated.png  everything

Checks (exit 1 on any failure): item / text overlaps, text crossing a pane edge, anything outside the 24 px
margin, text under 16 px over the world off glass, the kit's light rules (ui.layout_rules: no amber or cyan text,
no light text on a light pane, no text on clear glass), light text only on a cobalt pane, no text on the Survey
window, no navy / black fill bigger than a keyline or bullet outside the world art, the display face never under
22 px, exactly one traced object and no trace square touching text, exactly one cobalt primary and one focused
control per board (the Starting board is the stated exception: its primary is disabled with the reason), missing
glyphs, no hex literal in this file, no three-dot ellipsis and no old game name in the output, no circle / ellipse
/ rx in the SVGs, only the stand-in families.
"""

from __future__ import annotations

import glob
import os
import re
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ctd_ui as ui  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "svg", "main_menu"))
PREVIEW = os.path.normpath(os.path.join(HERE, "..", "svg", "_preview"))
W, H = 1920, 1080
EDGE = 24

HOST, YOU_CLIENT = "Dana", "mkovac"
VERSION = "v0.1.0 · Early Access"
LEGAL_SHORT = "§ 0.1 Use of this software is subject to the Terms. Terms and Conditions Apply. CTD-0001-A"
LEGAL = "§ 0.1 Use of this software is subject to the Terms. Terms and Conditions Apply. CTD-0001-A · " + VERSION
FAMILIES = {ui.DISPLAY, ui.UI_FACE, ui.UI_THIN, ui.DATA}
OLD_NAME = " ".join(("close", "the", "deal"))   # built, so this file never contains it
DOTS = "\\." * 3                                   # the three-dot ellipsis, as a regex ("…" is the glyph)

# --------------------------------------------------------------------------- board + checks (after make_kit)


class Board:
    """One artboard: files bottom to top, each a list of named items drawn at 1:1 in absolute coordinates.
    item kinds: None (solid, overlap-checked), "ground" (backdrop / bleed: not overlap- or margin-checked),
    "annot" (sheet label, design notes: overlap-checked, exempt from the over-world size rule)."""

    def __init__(self, key, variant, code, title, primary_exception=None):
        self.key, self.variant, self.code, self.title = key, variant, code, title
        self.primary_exception = primary_exception
        self.files, self.items, self.traces = [], [], []
        self.focus = 0
        self.window = None   # Survey: the sky window's box (no text may sit on it)
        self.holders = []    # Curtain Wall: the plates every solid item must sit inside (focus marks included)
        ui.reset_registry()

    def file(self, name, *items):
        body = ""
        for it in items:
            iname, fn = it[0], it[1]
            kind = it[2] if len(it) > 2 else None
            k = ui.mark()
            t0 = len(ui.TRACE_BOXES)
            markup = fn()
            self.traces += ui.TRACE_BOXES[t0:]
            shapes, texts, panes = ui.since(k)
            boxes = [s["box"] for s in shapes] + [p["box"] for p in panes]
            box = (min(b[0] for b in boxes), min(b[1] for b in boxes),
                   max(b[2] for b in boxes), max(b[3] for b in boxes)) if boxes else None
            worlds = [s["box"] for s in shapes if s.get("backdrop")]
            self.items.append(dict(name=iname, file=name, box=box, texts=texts, panes=panes, kind=kind,
                                   worlds=worlds, markup=markup, ground=(kind == "ground")))
            body += ui.group(iname, markup)
        self.files.append((name, ui.group(f"{self.key}-{name}", body)))

    def check(self):
        errs = []
        solid = [it for it in self.items if it["kind"] != "ground" and it["box"]]
        for i, a in enumerate(solid):
            for b in solid[i + 1:]:
                if _hit(_grow(a["box"], 2), _grow(b["box"], 2)):
                    errs.append(f"items overlap: {a['name']} {_r(a['box'])} / {b['name']} {_r(b['box'])}")
        texts = [(it, t) for it in self.items for t in it["texts"]]
        for i, (ia, a) in enumerate(texts):
            for ib, b in texts[i + 1:]:
                ga = (a["box"][0] - 4, a["box"][1] + 0.5, a["box"][2] + 4, a["box"][3] - 0.5)
                if _hit(ga, _grow(b["box"], -0.5)):
                    errs.append(f"text overlap: '{a['s']}' ({ia['name']}) / '{b['s']}' ({ib['name']})")
        for it in self.items:
            bx = it["box"]
            if bx and it["kind"] != "ground" and (bx[0] < EDGE or bx[1] < EDGE or bx[2] > W - EDGE or bx[3] > H - EDGE):
                errs.append(f"outside the margin: {it['name']} {_r(bx)}")
        panes = [p for it in self.items for p in it["panes"]]
        worlds = [w for it in self.items for w in it["worlds"]]
        for it, t in texts:
            tb = t["box"]
            for p in panes:
                pb = p["box"]
                if _hit(tb, pb) and not _inside(tb, _grow(pb, 0.5)):
                    errs.append(f"text crosses a pane edge: '{t['s']}' ({it['name']})")
            on_glass = any(_inside(tb, _grow(p["box"], 0.5)) for p in panes)
            if t["size"] < 16 and it["kind"] != "annot" and not on_glass and any(_hit(tb, w) for w in worlds):
                errs.append(f"text under 16 px over the world: '{t['s']}' ({it['name']})")
            fill = ui._hex(t["fill"])
            if fill in (ui._hex(ui.LIGHT), ui.SILVER):
                under = [p for p in panes if _inside(tb, _grow(p["box"], 0.5))]
                inner = min(under, key=lambda p: _area(p["box"])) if under else None
                if not inner or inner["kind"] != "cobalt":
                    errs.append(f"light text off a cobalt pane: '{t['s']}' ({it['name']})")
            if t.get("family") == ui.DISPLAY and t["size"] < 22:
                errs.append(f"display face under 22 px: '{t['s']}' ({it['name']})")
            if self.window and it["kind"] != "annot" and _hit(tb, self.window):
                errs.append(f"text on the sky window: '{t['s']}' ({it['name']})")
            for tr in self.traces:
                if _hit(tb, tr):
                    errs.append(f"trace square touches text: '{t['s']}' ({it['name']})")
                    break
        errs += ui.layout_rules(self.items)
        for it in self.items:
            if self.holders and it["kind"] is None and it["box"] and it["name"] != "TracedTower" and \
                    not any(_inside(it["box"], _grow(h, -12)) for h in self.holders):
                errs.append(f"off its plate (onto clear glass): {it['name']} {_r(it['box'])}")
        for it in self.items:
            if it["kind"] == "ground":
                continue
            for what in dark_fills(it["markup"]):
                errs.append(f"dark fill bigger than a keyline or bullet in {it['name']}: {what}")
        n_trace = sum(m.count('id="TraceCluster"') for _, m in self.files)
        if n_trace != 1:
            errs.append(f"{n_trace} traced objects (want exactly 1)")
        n_cobalt = sum(1 for p in panes if p["kind"] == "cobalt")
        want = 0 if self.primary_exception else 1
        if n_cobalt != want:
            errs.append(f"{n_cobalt} cobalt primaries (want {want})")
        if self.focus != 1:
            errs.append(f"{self.focus} focused controls (want exactly 1)")
        return errs


def _grow(b, d):
    return (b[0] - d, b[1] - d, b[2] + d, b[3] + d)


def _hit(a, b):
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]


def _inside(a, b):
    return a[0] >= b[0] and a[1] >= b[1] and a[2] <= b[2] and a[3] <= b[3]


def _area(b):
    return (b[2] - b[0]) * (b[3] - b[1])


def _r(b):
    return tuple(round(v) for v in b)


def _attr(tag, name, default=None):
    m = re.search(rf'\s{name}="([^"]*)"', tag)
    return m.group(1) if m else default


def dark_fills(markup):
    """Every rect / polygon filled navy or near-black (alpha over 0.3) whose shorter side is over 12 px: a dark
    panel by the guide's rule (keylines, bullets, ticks and status squares stay under it)."""
    dark = {ui.NAVY.upper(), ui.BLACK.upper()}
    out = []
    for tag in re.findall(r"<(?:rect|polygon)\b[^>]*>", markup):
        fill = (_attr(tag, "fill") or "").upper()
        if fill not in dark:
            continue
        a = float(_attr(tag, "fill-opacity", 1)) * float(_attr(tag, "opacity", 1))
        if a <= 0.3:   # shadows (the primary's 2 px offset, navy 30 %) and tints are not panels
            continue
        if tag.startswith("<rect"):
            w, h = float(_attr(tag, "width")), float(_attr(tag, "height"))
        else:
            pts = [tuple(map(float, p.split(","))) for p in _attr(tag, "points").split()]
            w = max(p[0] for p in pts) - min(p[0] for p in pts)
            h = max(p[1] for p in pts) - min(p[1] for p in pts)
        if min(w, h) > 12:
            out.append(f"{w:.0f} x {h:.0f} at alpha {a:.2f}")
    return out


# --------------------------------------------------------------------------- local helpers (proposed kit changes)

CUR = {"bd": None, "cue": False}   # the board being drawn (focus bookkeeping) and whether focus gets a chevron cue


def btn(x, y, w, label, variant="primary", state="default"):
    """ui.button with a stable layer name and focus bookkeeping (exactly one focused control per board).
    Survey: the focused control also gets the hero's chevron row in the gutter 62 px to its left."""
    b = ""
    if state == "focus":
        if CUR["bd"] is not None:
            CUR["bd"].focus += 1
        if CUR["cue"]:
            b += focus_cue(x, y + btn_h(variant) / 2)
    slug = re.sub(r"[^A-Za-z]", "", label.title())
    return b + ui.button(x, y, w, label, variant, state, name=f"Btn-{variant}-{state}-{slug}")


def btn_h(variant):
    return ui.SIZE["button_primary_h"] if variant == "primary" else ui.SIZE["button_secondary_h"]


def focus_cue(x, cy):
    """The hero's focus cue: three chevrons pointing in at the focused control from the gutter."""
    return ui.chevron_row(x - 62, cy, 3, "right", s=7, gap=14, color=ui.NAVY, name=ui.uid("FocusCue"))


def esc_chip(x, y, label):
    """Esc keycap chip + what it does (the kit's modal_confirm pattern). y = keycap top. Returns (markup, width)."""
    cap, kw = ui.keycap(x, y, "Esc", dark=False, size=16, h=32)
    return cap + ui.text(x + kw + 10, y + 22, label, "label", ui.INK2), kw + 10 + ui.text_width(label, "label")


def status(x, y, msg, kind, w):
    """ui.status_line, wrapped to w (continuation lines hang at the text indent). Returns (markup, height)."""
    lines = ui.wrap(msg, w - 22, "body")
    b = ui.status_line(x, y, lines[0], kind, name=ui.uid(f"Status-{kind}-"))
    fill = ui.INK2 if kind == "busy" else ui.NAVY
    for i, ln in enumerate(lines[1:], 1):
        b += ui.text(x + 22, y + i * 26, ln, "body", fill)
    return b, len(lines) * 26


def mode_line(x, y, mode, kind):
    """Who is signed in: cobalt square for Steam, hollow navy square for local mode. y = baseline."""
    if kind == "steam":
        b = ui.square_bullet(x, y - 9, 8, ui.COBALT)
    else:
        b = ui.rect(x + 0.75, y - 8.25, 6.5, 6.5, None, ui.NAVY, 1.5)
    return b + ui.text(x + 20, y, mode, "label", ui.NAVY)


def tag(x, y, label):
    """Outlined navy tag, label 16 (e.g. "Host"). y = top. Returns (markup, width)."""
    w = ui.text_width(label, "label") + 16
    return ui.rect(x + 0.5, y + 0.5, w - 1, 25, None, ui.NAVY, 1) + ui.text(x + 8, y + 18, label, "label", ui.NAVY), w


def busy_squares(x, y, n=12, lit=(4, 5, 6), s=12, gap=6):
    """Indeterminate busy bar: a run of squares, three lit in cobalt (they march left to right, 80 ms a step)."""
    b = ""
    for i in range(n):
        sx = x + i * (s + gap)
        b += ui.rect(sx, y, s, s, ui.COBALT) if i in lit else ui.rect(sx + 0.5, y + 0.5, s - 1, s - 1, None, ui.HAIR_STRONG, 1)
    return b


# ---- the Survey window (kit candidate: hero_screen's frame, with the sill height as a parameter)

VX, VY, TOP = 960, -5000, -12
COLS = ((56, 616), (664, 1256), (1304, 1864))


def window_geo(base):
    """Six panes (3 x 2) seen from below, verticals converging on (VX, VY); the transom sits at 44 % of the sill."""
    split = round(base * 0.44)
    rows = ((TOP, split), (split + 40, base))
    panes = []
    for l, r in COLS:
        for y0, y1 in rows:
            panes.append([(ui._persp_x(l, y0, VX, VY, base), y0), (ui._persp_x(r, y0, VX, VY, base), y0),
                          (ui._persp_x(r, y1, VX, VY, base), y1), (ui._persp_x(l, y1, VX, VY, base), y1)])
    return split, panes


def tower_y(base):
    return window_geo(base)[0] + 60


def survey_window(base):
    """The ref-08 window frame with its sill at `base`: deep-blue sky, halftone, three stepped cumulus and the rival
    tower in the right pane, clipped to the panes; silver reveals, two sash latches, the sill and its shadow, and
    the one black accent hairline 48 px under the sill. World art: registered as one shape, nothing else."""
    split, panes = window_geo(base)
    k = base / 664
    r2 = split + 40
    cid = ui.uid("winPanes")
    shapes = "".join('<polygon points="' + " ".join(f"{round(px, 1):g},{round(py, 1):g}" for px, py in q) + '"/>'
                     for q in panes)
    with ui._quiet():
        sky = ui.rect(0, TOP, W, base - TOP, "url(#skyDeep)") + ui.rect(0, TOP, W, base - TOP, "url(#skyHaze)")
        sky += ui.halftone(96, 0, 500, min(176, split - 24), "down", (ui.LIGHT, 0.5), cell=10, dmax=4, dmin=0,
                           name=ui.uid("WinHalftone"))
        sky += ui._cumulus(1500, base - 24, 420, 150 * k, seed=8, step=8)
        sky += ui._tower(1462, tower_y(base), 264, 400, band=26, gap=40, figures=((0.3, 1), (0.66, 3)), wash=0.12)
        sky += ui._cumulus(-40, base + 56 * k, 780, 500 * k, seed=5, step=8)
        sky += ui._cumulus(700, base + 26 * k, 560, 210 * k, seed=21, step=8)
        sky += ui.poly([(980, base), (1050, base), (1180, r2), (1110, r2)], (ui.LIGHT, 0.12))
        sky += ui.poly([(1066, base), (1076, base), (1206, r2), (1196, r2)], (ui.LIGHT, 0.12))
        b = f'<clipPath id="{cid}">{shapes}</clipPath><g id="WindowSky" clip-path="url(#{cid})">{sky}</g>'
        rev = ""
        for q in panes:
            (x0, y0), (x1, _), (_, y2), (x3, _) = q
            rev += ui.poly([(x0, y0), (x1, y0), (x1 + 2, y0 + 10), (x0 + 2, y0 + 10)], (ui.SILVER, 0.85))
            rev += ui.poly([(x0, y0), (x0 + 9, y0 + 10), (x3 + 9, y2), (x3, y2)], (ui.SILVER, 0.6))
        rev += ui.rect(0, base, W, 10, (ui.SILVER, 0.7)) + ui.rect(0, base + 10, W, 2, (ui.NAVY, 0.12))
        ly = r2 + (base - r2) * 0.45 - 18
        for cx in (640, 1280):   # sash latches on the two inner mullions
            lx = ui._persp_x(cx, ly + 18, VX, VY, base)
            rev += ui.rect(lx - 6, ly, 12, 36, ui.PAPER) + ui.rect(lx - 5.5, ly + 0.5, 11, 35, None, ui.HAIR_STRONG, 1)
        b += ui.group(ui.uid("WindowFrame"), rev)
        b += ui.line(112, base + 48.5, 1808, base + 48.5, ui.BLACK, 1)   # the one black accent
    ui._reg("shapes", 0, 0, W, base + 12, backdrop=True)
    return b


def window_trace(base):
    """The one trace: snapped squares round the rival tower's top, inside its pane, clear of every text."""
    ty = tower_y(base) + 20
    return ui.trace_brackets(1484, ty, 222, base - 56 - ty, ui.NAVY, seed=13, density=0.55)


# ---- the Curtain Wall (kit candidate: a white curtain-wall frame around clear bays)

def white_frame(bays, bar):
    """White aluminium frame around clear bays [(x, y, w, h)] that sit `bar` px apart: paper bars, a silver
    reveal on each bay's top and left inner edge (the frame's depth), a 1 px keyline outside. No navy anywhere."""
    xs0 = min(b[0] for b in bays) - bar
    ys0 = min(b[1] for b in bays) - bar
    xs1 = max(b[0] + b[2] for b in bays) + bar
    ys1 = max(b[1] + b[3] for b in bays) + bar
    b = ""
    b += ui.rect(xs0, ys0, xs1 - xs0, bar, ui.PAPER) + ui.rect(xs0, ys1 - bar, xs1 - xs0, bar, ui.PAPER)
    b += ui.rect(xs0, ys0, bar, ys1 - ys0, ui.PAPER) + ui.rect(xs1 - bar, ys0, bar, ys1 - ys0, ui.PAPER)
    for (x, y, w, h) in bays:
        if y + h + bar < ys1 - 1:
            b += ui.rect(x, y + h, w, bar, ui.PAPER)
        if x + w + bar < xs1 - 1:
            b += ui.rect(x + w, y, bar, h, ui.PAPER)
        b += ui.rect(x, y, w, 4, (ui.SILVER, 0.9)) + ui.rect(x, y, 3, h, (ui.SILVER, 0.6))
    b += ui.rect(xs0 + 0.5, ys0 + 0.5, xs1 - xs0 - 1, ys1 - ys0 - 1, None, ui.HAIR_STRONG, 1)
    return b


# --------------------------------------------------------------------------- shared content (both skins)

def menu(x, y, items, notes=None):
    """Directory-style menu: a mono index then the button. items: [(label, variant, state, width)].
    notes: {index: line} drawn directly under that button (a disabled control's reason). Returns (markup, bottom y)."""
    b = ""
    yy = y
    for i, (label, variant, state, w) in enumerate(items, 1):
        h = btn_h(variant)
        b += ui.text(x, yy + h / 2 + 5, f"{i:02d}", "caption", ui.INK3, ls=1)
        b += btn(x + 48, yy, w, label, variant, state)
        if notes and i - 1 in notes:
            b += ui.rect(x + 48.75, yy + h + 14.75, 8.5, 8.5, None, ui.COBALT, 1.5)
            b += ui.text(x + 70, yy + h + 26, notes[i - 1], "body", ui.INK2)
            yy += 34
        yy += h + 20
    return b, yy - 20


def section(x, y, w, label, right=None):
    """Clause label: square bullet, caption caps, keyline (the form grammar). 22 px tall."""
    b = ui.square_bullet(x, y + 3, 8, ui.BLACK)
    b += ui.text(x + 16, y + 12, label, "caption", ui.NAVY, ls=2)
    if right:
        b += ui.text_right(x + w, y + 12, right, "caption", ui.INK3, ls=0.5)
    return b + ui.line(x, y + 20.5, x + w, y + 20.5, ui.NAVY)


def footer(x, y, w, mode, mode_kind, msg, msg_kind):
    """Mode line + status line (Curtain Wall's footer plate)."""
    b = mode_line(x, y + 36, mode, mode_kind) if mode else ""
    if msg:
        m, _ = status(x, y + (76 if mode else 37), msg, msg_kind, w)
        b += m
    return b


def notice(x, y, w, head, fix):
    """Failure notice on a solid amber plate (the kit's warning surface): navy hazard mark, navy headline, navy
    fix line. Its recovery button sits right under it. Returns (markup, height)."""
    hl = ui.wrap(head, w - 88, "item")
    fl = ui.wrap(fix, w - 88, "body") if fix else []
    h = 32 + len(hl) * 28 + (12 + len(fl) * 26 if fl else 0) + 24
    b = ui.glass_pane(x, y, w, h, "amber", name=ui.uid("NoticePlate"))
    b += ui.warn_glyph(x + 36, y + 38, 11, ui.NAVY)
    ty = y + 46
    for ln in hl:
        b += ui.text(x + 64, ty, ln, "item", ui.NAVY)
        ty += 28
    ty += 12
    for ln in fl:
        b += ui.text(x + 64, ty, ln, "body", ui.NAVY)
        ty += 26
    return b, h


def roster(x, y, w, firm, seats):
    """One firm's seats. seats: [(name or None, dict(you, host, ready))]. Header 48 + 4 rows of 56.
    Shape + word for every state: Host tag, You diamond, Ready filled square / Not ready hollow. Empty seats are
    the kit's outlined slots."""
    b = ""
    if firm == "you":
        b += ui.team_marker(x + 10, y + 22, "you", 9, name=ui.uid("FirmYou"))
        head = "Your firm"
    else:
        b += ui.team_marker(x + 10, y + 23, "rival", 9, name=ui.uid("FirmRival"))
        head = "Rival firm"
    b += ui.text(x + 30, y + 30, head, "item", ui.NAVY, size=24)
    filled = sum(1 for n, _ in seats if n)
    b += ui.text_right(x + w, y + 29, f"{filled} of {len(seats)}", "label", ui.INK2)
    b += ui.line(x, y + 47.5, x + w, y + 47.5, ui.NAVY, 1)
    for i, (name, info) in enumerate(seats):
        ry = y + 56 + i * 56
        rh = 48
        if name is None:
            b += ui.outlined_slot(x, ry, w, rh, name=ui.uid("SeatSlot"))
            b += ui.text(x + 16, ry + 30, f"{i + 1:02d}", "caption", ui.INK3, ls=1)
            b += ui.text(x + 52, ry + 31, "Seat available", "body", ui.INK2)
            continue
        if info.get("you"):
            b += ui.rect(x, ry, w, rh, (ui.COBALT, 0.08)) + ui.rect(x, ry, 4, rh, ui.COBALT)
        else:
            b += ui.rect(x + 0.5, ry + 0.5, w - 1, rh - 1, None, ui.HAIR, 1)
        b += ui.text(x + 16, ry + 30, f"{i + 1:02d}", "caption", ui.INK3, ls=1)
        b += ui.text(x + 52, ry + 31, name, "item", ui.NAVY)
        tx = x + 52 + ui.text_width(name, "item") + 12
        if info.get("you"):
            b += ui.diamond(tx + 8, ry + 24, 7, ui.CYAN, ui.NAVY, 1.5)
            b += ui.text(tx + 20, ry + 30, "You", "label", ui.NAVY)
            tx += 20 + ui.text_width("You", "label") + 12
        if info.get("host"):
            m, tw = tag(tx, ry + 11, "Host")
            b += m
            tx += tw + 12
        rx = x + w - 16
        if info.get("ready"):
            lw = ui.text_width("Ready", "label")
            b += ui.rect(rx - lw - 22, ry + 18, 12, 12, ui.COBALT)
            b += ui.text(rx - lw, ry + 30, "Ready", "label", ui.NAVY)
        else:
            lw = ui.text_width("Not ready", "label")
            b += ui.rect(rx - lw - 21.25, ry + 18.75, 10.5, 10.5, None, ui.NAVY, 1.5)
            b += ui.text(rx - lw, ry + 30, "Not ready", "label", ui.INK2)
    return b


LOBBY_HOST = {
    "you": [("Dana", dict(you=True, host=True, ready=True)), ("Priya_R", dict(ready=True)),
            ("mkovac", dict(ready=True)), (None, {})],
    "rival": [("jonno", dict(ready=True)), ("sasha_k", dict(ready=True)), (None, {}), (None, {})],
}
LOBBY_CLIENT = {
    "you": [("Dana", dict(host=True, ready=True)), ("Priya_R", dict(ready=True)),
            ("mkovac", dict(you=True, ready=False)), (None, {})],
    "rival": [("jonno", dict(ready=True)), ("sasha_k", dict(ready=False)), (None, {}), (None, {})],
}

# --------------------------------------------------------------------------- the states (one model, two skins)

STEAM_MODE = (f"Signed in to Steam as {HOST}", "steam")
LOCAL_MODE = ("Local test mode. Steam is not running.", "local")
JOIN_HINT = "To join a friend, accept their invite or choose Join Game on their name in Steam."

STATES = [
    # key, title, kind, data
    ("01_title", "01 TITLE · READY", "menu", dict(
        mode=STEAM_MODE, hint=JOIN_HINT,
        items=[("Host game", "primary", "focus"), ("Settings", "secondary", "default"),
               ("Quit to desktop", "quiet", "default")],
        status=("Ready. Host a game, or join a friend from Steam.", "ok"))),
    ("02_starting", "02 STARTING · FIRST RUN", "menu", dict(
        mode=None, esc_hint="Opens the menu during a game.",
        items=[("Host game", "primary", "disabled"), ("Settings", "secondary", "focus"), ("Quit to desktop", "quiet", "default")],
        status=("Starting…", "busy"), reason="Host game opens once the network is ready.")),
    ("03_hosting", "03 HOSTING · OPENING A LOBBY", "busy", dict(
        mode=STEAM_MODE, head="Opening a lobby on Steam…", steps=None, cancel="Cancel")),
    ("04_joining", "04 JOINING · CONNECTING", "busy", dict(
        mode=(f"Signed in to Steam as {YOU_CLIENT}", "steam"), head=f"Connecting to {HOST}'s game…",
        steps=[("Joining from Steam…", "ok"), (f"Connecting to {HOST}'s game…", "busy")],
        timer=6, cancel="Cancel")),
    ("05_lobby_host", "05 LOBBY · HOST", "lobby", dict(
        mode=STEAM_MODE, seats=LOBBY_HOST, host_view=True,
        status=("Hosting. Invite friends, or they can choose Join Game on your name in Steam.", "ok"))),
    ("06_lobby_client", "06 LOBBY · PLAYER", "lobby", dict(
        mode=(f"Signed in to Steam as {YOU_CLIENT}", "steam"), seats=LOBBY_CLIENT, host_view=False,
        status=(f"Waiting for {HOST} to begin the run.", "busy"))),
    ("07_failed_full", "07 JOIN FAILED · GAME FULL", "notice", dict(
        mode=(f"Signed in to Steam as {YOU_CLIENT}", "steam"),
        head="Unable to join. The game is full (8 players).",
        fix="A seat opens when a player leaves. Try again in a moment, or host your own game.",
        buttons=[("Try again", "primary", "focus", 240), ("Back", "secondary", "default", 160)])),
    ("08_failed_friends", "08 JOIN FAILED · FRIENDS ONLY", "notice", dict(
        mode=(f"Signed in to Steam as {YOU_CLIENT}", "steam"),
        head=f"Unable to join. The lobby is friends-only: add {HOST} as a Steam friend first.",
        fix="Then join again from their name in Steam.",
        buttons=[("Open Steam friends", "primary", "focus", 280), ("Back", "secondary", "default", 160)])),
    ("09_local", "09 LOCAL TEST MODE · STEAM OFFLINE", "menu", dict(
        mode=LOCAL_MODE, hint="To play with friends, start Steam, then restart the game.",
        items=[("Host game", "primary", "focus"), ("Join on this PC", "secondary", "default"),
               ("Settings", "secondary", "default"), ("Quit to desktop", "quiet", "default")],
        status=("Ready. Host a game, or join one running on this PC.", "ok"))),
    ("10_lobby_failed", "10 HOST FAILED · STEAM LOBBY", "notice", dict(
        mode=STEAM_MODE,
        head="Steam could not open a lobby. Check that Steam is online, then host again.",
        fix=None,
        buttons=[("Host again", "primary", "focus", 240), ("Back", "secondary", "default", 160)])),
    ("11_quit", "11 QUIT CONFIRM", "quit", dict(mode=STEAM_MODE)),
]

COMMON_NOTES = {
    "menu": ["One selection mark per board: focus only (ring, snapped brackets, chevron cue). Hover is drawn on "
             "the kit's A2 state matrix, never beside focus."],
    "busy": ["Needs new code: Cancel while opening or joining (today the card has no cancel; Cancel = Leave).",
             "Busy bar: three lit squares march left to right, 80 ms a step; the line text never animates.",
             "Esc cancels too: the Esc keycap chip beside Cancel, as in every other group."],
    "lobby": ["Needs new code: firm rosters, seats, ready state, Begin the run, Invite friends from the lobby.",
              "Doc mismatch: GDD / inventory make the lobby diegetic (Begin the run at the reception desk, "
              "F to interact). This brief asks for an in-menu start; the owner picks one.",
              "Seats: \"5 of 8 seats filled\" counts the lobby (max 8); each firm shows \"n of 4\" (teams of 1 to 4). "
              "Empty seats are the kit's outlined slots. No firm names: Your firm / Rival firm."],
    "notice": ["Needs new code: failure notice with its own recovery button (today: one status row, no buttons).",
               "The notice is the kit's amber plate (navy text, navy hazard mark): a failure, not a dark panel. "
               "Focus lands on the fix; Back (or Esc / B) returns to the title."],
    "quit": ["Needs new code: Quit to desktop and this confirm. Stay is primary and focused; Esc and B = Stay."],
}

STATE_NOTES = {
    "01_title": ["Needs new code: Settings screen and Quit to desktop (the card has Host game only)."],
    "02_starting": ["First run / loading. Host game stays disabled (dashed, not focusable) with the reason under it; "
                    "no cobalt primary until Ready, by design. Focus starts on Settings.",
                    "Needs new code: the reason line and the first-run line."],
    "04_joining": ["Needs new code: the two-step join ladder and the 15 s give-up timer (SteamLobby.cs:91 has the timeout)."],
    "07_failed_full": ["Copy added: the fix line (J3's text names no fix)."],
    "08_failed_friends": ["Needs new code: Open Steam friends (ActivateGameOverlay \"Friends\")."],
    "09_local": ["Steam-only controls are gone: no join hint, no Invite. Join on this PC replaces them.",
                 "Copy added: the start-Steam line, so the player knows how to get friends back.",
                 "Owner call: \"Local test mode\" may read as dev wording to players."],
    "10_lobby_failed": ["S9 happens in Steam mode, so this board keeps the Steam mode line (the brief filed it under "
                        "the offline state; local mode never opens a Steam lobby)."],
}

VARIANT_NOTES = {
    "survey": ["Window frame (ref 08) on every state. The sill rises when the content needs room (title 664, menus "
               "616, busy and failures 500, lobby 360) and slides between heights in 240 ms, ease-in-out."],
    "curtain_quit": ["The screen behind is blurred under a paper wash (no navy scrim)."],
    "curtain": ["Needs an engine blur pass: the clear bays show the live scene sharp here; in engine, one "
                "downsampled full-screen blur under the bays (every frame the menu shows) keeps the scene from "
                "competing with the plates. Text sits only on the white plates; clear glass carries none."],
}

# --------------------------------------------------------------------------- annotation


def sheet_label(variant, title):
    s = f"{ui.GAME_TITLE} · MAIN MENU · {variant.upper()} · {title}"
    w = ui.text_width(s, "caption", ls=2) + 40
    b = ui.rect(40, 24, w, 28, ui.PAPER, ui.HAIR_STRONG, 1)
    b += ui.square_bullet(52, 34, 9, ui.BLACK)
    b += ui.text(68, 43, s, "caption", ui.NAVY, ls=2)
    return b


def notes_card(lines, x=1360, w=520, top=None, max_h=None):
    """DesignNotes on a paper card. Annotation: hidden in the clean export.
    top=None: bottom-right, flush to the bottom margin. top=y: hangs from y; if the notes are taller than max_h
    they flow into a second column of the same width."""
    md = []
    for i, n in enumerate(lines, 1):
        md.append((i, n, len(ui.wrap(n, w - 56, "label", size=14, weight=400)) * 14 * 1.35))
    total = sum(h + 6 for *_, h in md)
    cols = [md]
    if max_h and 52 + total + 8 > max_h:
        acc, cut = 0, len(md)
        for j, (*_, hh) in enumerate(md):
            if acc + hh + 6 > total / 2 and j:
                cut = j
                break
            acc += hh + 6
        cols = [md[:cut], md[cut:]]
    hgt = max(sum(h + 6 for *_, h in c) for c in cols)
    cw = w * len(cols) + 24 * (len(cols) - 1)
    h = 52 + hgt + 8
    y = top if top is not None else H - 32 - h
    b = ui.rect(x, y, cw, h, ui.PAPER, ui.NAVY, 1)
    b += ui.text(x + 20, y + 30, "DESIGN NOTES", "caption", ui.COBALT, ls=2)
    for c, col in enumerate(cols):
        cx, yy = x + c * (w + 24), y + 56
        for i, n, hh in col:
            b += ui.text(cx + 20, yy, f"{i}", "caption", ui.COBALT, ls=0)
            b += ui.text_lines(cx + 38, yy, n, w - 56, "label", ui.INK2, size=14, weight=400, leading=1.35)[0]
            yy += hh + 6
    return b


def _notes(st, variant):
    key, _, kind, _ = st
    extra = VARIANT_NOTES.get(f"{variant}_{kind}", [])
    if variant == "survey" and kind == "quit":
        extra = ["Survey: the confirm takes the menu column on white; the window and title stay put."]
    return STATE_NOTES.get(key, []) + COMMON_NOTES[kind] + extra + VARIANT_NOTES[variant]


def _modal(scr_under, x=None, y=None):
    if CUR["bd"] is not None:
        CUR["bd"].focus += 1
    mw = 640
    x = (W - mw) / 2 if x is None else x
    y = (H - 204) / 2 if y is None else y
    return ui.modal_confirm(x, y, mw, "Quit to desktop?", "Your settings are saved.",
                            "Quit to desktop", "Stay", focus="cancel", under=scr_under, cancel_w=200)


def content(st, x, y, w):
    """The state's main content on a light surface at (x, y), width w. Returns [(item name, fn)]."""
    key, _, kind, d = st
    items = []
    if kind == "menu":
        def m(d=d):
            b = section(x, y, w, "§ 1  MAIN MENU", "select · enter")
            mitems = [(lab, var, s, 440) for lab, var, s in d["items"]]
            mm, yb = menu(x, y + 44, mitems, {0: d["reason"]} if d.get("reason") else None)
            b += mm
            yy = yb + 44
            if d.get("hint"):
                b += ui.text_lines(x + 48, yy, d["hint"], min(w - 48, 560), "body", ui.INK2)[0]
            if d.get("esc_hint"):
                b += esc_chip(x + 48, yy - 22, d["esc_hint"])[0]
            return b
        items.append(("Menu", m))
    elif kind == "busy":
        def bz(d=d):
            b = section(x, y, w, "§ 1  " + ("HOST GAME" if key == "03_hosting" else "JOIN GAME"), "in progress")
            yy = y + 84
            if d.get("steps"):
                for i, (msg, sk) in enumerate(d["steps"], 1):
                    b += ui.text(x, yy - 3, f"{i:02d}", "caption", ui.INK3, ls=1)
                    if sk == "ok":
                        b += ui.rect(x + 48, yy - 13, 10, 10, ui.COBALT)
                        b += ui.text(x + 70, yy, msg, "body", ui.INK2)
                    else:
                        b += ui.rect(x + 48.75, yy - 12.25, 8.5, 8.5, None, ui.COBALT, 1.5)
                        b += ui.text(x + 70, yy, msg, "item", ui.NAVY, size=26)
                    yy += 44
                yy -= 44
            else:
                b += ui.text(x + 48, yy, d["head"], "item", ui.NAVY, size=26)
            b += busy_squares(x + 48, yy + 28)
            yy += 76
            if d.get("timer") is not None:
                n = d["timer"]
                b += ui.text(x + 48, yy, "Stops trying after 15 seconds.", "body", ui.INK2)
                for k in range(15):
                    sx = x + 48 + k * 18
                    b += (ui.rect(sx, yy + 16, 14, 6, ui.NAVY) if k < n else
                          ui.rect(sx + 0.5, yy + 16.5, 13, 5, None, ui.HAIR_STRONG, 1))
                b += ui.text(x + 48 + 15 * 18 + 8, yy + 24, f"0:{n:02d}", "hud", ui.NAVY, size=16)
                yy += 64
            b += btn(x + 48, yy + 8, 240, d["cancel"], "primary", "focus")
            b += esc_chip(x + 48 + 240 + 32, yy + 24, "Cancel")[0]
            return b
        items.append(("Busy", bz))
    elif kind == "notice":
        def nt(d=d):
            b = section(x, y, w, "§ 1  " + ("HOST GAME" if key == "10_lobby_failed" else "JOIN GAME"), "action needed")
            nw = min(w - 48, 720)
            m, h = notice(x + 48, y + 52, nw, d["head"], d["fix"])
            b += m
            bx = x + 48
            for lab, var, s, bw in d["buttons"]:
                b += btn(bx, y + 52 + h + 32, bw, lab, var, s)
                bx += bw + 24
            return b
        items.append(("Notice", nt))
    elif kind == "lobby":
        host = d["host_view"]

        def lb(d=d):
            b = section(x, y, w, f"§ 2  LOBBY · {HOST.upper()}'S GAME")
            b += ui.text_right(x + w, y + 13, "5 of 8 seats filled", "label", ui.NAVY)
            rw = (w - 40) / 2
            b += roster(x, y + 44, rw, "you", d["seats"]["you"])
            b += roster(x + rw + 40, y + 44, rw, "rival", d["seats"]["rival"])
            return b
        items.append(("Rosters", lb))

        def acts(host=host):
            ay = y + 44 + 48 + 8 + 4 * 56 + 32
            if host:
                b = btn(x, ay, 280, "Begin the run", "primary", "focus")
                b += btn(x + 304, ay + 4, 240, "Invite friends", "secondary", "default")
                b += btn(x + 568, ay + 4, 220, "Close lobby", "quiet", "default")
            else:
                b = btn(x, ay, 220, "Ready", "primary", "focus")
                b += btn(x + 244, ay + 4, 240, "Invite friends", "secondary", "default")
                b += btn(x + 508, ay + 4, 220, "Leave lobby", "quiet", "default")
            return b
        items.append(("Actions", acts))
    return items


# --------------------------------------------------------------------------- Survey

SILL = {"menu": 616, "busy": 500, "notice": 500, "lobby": 360}
DESCRIBE = {  # Survey only: the focused item's clause in the middle column (copy written for the mockup)
    "01_title": ("§ 1.1  HOST GAME", "Open a lobby on Steam. Friends join from your name in Steam, up to 8 players."),
    "02_starting": ("§ 1.2  SETTINGS", "Controls, video, audio and accessibility. Available while the game starts."),
    "09_local": ("§ 1.1  HOST GAME", "Host on this PC. Start a second copy of the game and choose Join on this PC."),
}


def survey_sill(st):
    key, _, kind, _ = st
    return 664 if key in ("01_title", "11_quit") else SILL.get(kind, 664)


def survey_menu_parts(st, base):
    """The hero's three columns below the sill: title + who is signed in | status, hint, the focused item's clause |
    the menu with the chevron cue on focus."""
    key, _, _, d = st
    x, mx, bx, bw = 112, 832, 1388, 420
    top = base + 88

    def steam():
        return mode_line(x, base + 244, *d["mode"]) if d.get("mode") else ""

    def middle():
        b, hgt = status(mx, top + 38, *d["status"], 1308 - mx)
        yy = top + 38 + hgt + 14
        if d.get("hint"):
            m, hh = ui.text_lines(mx + 22, yy, d["hint"], 400, "body", ui.INK2)
            b += m
            yy += hh
        if d.get("esc_hint"):
            b += esc_chip(mx + 22, yy - 20, d["esc_hint"])[0]
            yy += 30
        if key in DESCRIBE:
            head, body = DESCRIBE[key]
            yy += 34
            b += ui.text(mx + 22, yy, head, "caption", ui.NAVY, ls=2)
            b += ui.line(mx + 22, yy + 10.5, mx + 422, yy + 10.5, ui.HAIR)
            b += ui.text_lines(mx + 22, yy + 40, body, 400, "body", ui.INK2)[0]
        return b

    def right():
        b, yy = "", top
        for label, variant, state in d["items"]:
            h = btn_h(variant)
            b += btn(bx, yy, bw, label, variant, state)
            if state == "disabled" and d.get("reason"):
                b += ui.rect(bx + 0.75, yy + h + 14.75, 8.5, 8.5, None, ui.COBALT, 1.5)
                b += ui.text(bx + 22, yy + h + 26, d["reason"], "body", ui.INK2)
                yy += 34
            yy += h + (24 if variant == "primary" else 16)
        return b
    return [("SteamLine", steam), ("Status", middle), ("Menu", right)]


def survey_parts(st, base):
    """[(item, fn)] for everything below the sill, by kind."""
    key, _, kind, d = st
    x = 112
    if kind == "menu":
        return [("TitleLockup", lambda: ui.title_lockup(x, base + 88, size=64, serial=VERSION, name="Title"))] + \
            survey_menu_parts(st, base)
    if kind == "quit":   # the confirm replaces the menu column on white; the frame does not move
        return [("TitleLockup", lambda: ui.title_lockup(x, base + 88, size=64, serial=VERSION, name="Title")),
                ("SteamLine", lambda: mode_line(x, base + 244, *d["mode"])),
                ("QuitConfirm", lambda: _modal(None, 1168, base + 88))]
    if kind == "lobby":
        def right():
            b = mode_line(832, base + 96, *d["mode"])
            return b + status(832, base + 136, *d["status"], 1808 - 832)[0]
        return [("TitleLockup", lambda: ui.title_lockup(x, base + 72, size=48, serial=VERSION, name="Title")),
                ("Status", right)] + content(st, x, base + 188, 1808 - x)
    return [("TitleLockup", lambda: ui.title_lockup(x, base + 88, size=64, serial=VERSION, name="Title")),
            ("SteamLine", lambda: mode_line(x, base + 244, *d["mode"]))] + content(st, 832, base + 88, 1808 - 832)


def survey_frame(base):
    return ui.rect(0, 0, W, H, ui.PAPER) + ui.hairline_grid(0, 0, W, H, faint=True, name="Grid") + survey_window(base)


def survey_legal():
    b = ui.microtype_strip(112, 1024, LEGAL, name="Microtype")
    b += ui.barcode(1648, 1004, 160, 22, seed=11, name="Barcode")
    return b + ui.path("M48 1004V1032H76M1872 1004V1032H1844", None, ui.NAVY, 1.5)


def survey_board(st, under=False):
    key, title, kind, d = st
    bd = None if under else Board(f"survey_{key}", "survey", key[:2], title,
                                  primary_exception=(key == "02_starting"))
    CUR["bd"], CUR["cue"] = bd, True
    base = survey_sill(st)
    parts = survey_parts(st, base)
    if under:
        b = survey_frame(base) + window_trace(base)
        for _, fn in parts:
            b += fn()
        return b + survey_legal()

    bd.window = (0, 0, W, base + 12)
    bd.file("Frame", ("Frame", lambda: survey_frame(base), "ground"))
    bd.file("SheetLabel", ("SheetLabel", lambda: sheet_label("survey", title), "annot"))
    bd.file("TracedTower", ("TracedTower", lambda: window_trace(base)))
    bd.file("Content", *parts)
    bd.file("Legal", ("Legal", survey_legal))
    bd.file("DesignNotes", ("DesignNotes", lambda: notes_card(_notes(st, "survey"), x=40, w=660, top=64,
                                                              max_h=base - 64 - 20), "annot"))
    return bd


# --------------------------------------------------------------------------- Curtain Wall

def curtain_board(st, under=False):
    key, title, kind, d = st
    lobby = kind == "lobby"
    bd = None if under else Board(f"curtain_{key}", "curtain", key[:2], title,
                                  primary_exception=(key == "02_starting"))
    CUR["bd"], CUR["cue"] = bd, False
    bar, inset = 12, 20
    ax = 104
    aw = 1232 if lobby else 760
    t_h = 184 if lobby else 216
    m_h = 552 if lobby else 488 if len(d.get("items", ())) > 3 else 456 if (d.get("reason") or d.get("timer")) else 432
    f_h = 160
    ty = 96
    my = ty + t_h + bar
    fy = my + m_h + bar
    bays = [(ax, ty, aw, t_h), (ax, my, aw, m_h), (ax, fy, aw, f_h)]
    side = [] if lobby else [(ax + aw + bar, b_[1], 160, b_[3]) for b_ in bays]   # empty glass: the wall goes on
    cx, cw = ax + 48, aw - 96
    lock_size = 56 if lobby else 64

    def glass():
        b = "".join(ui.glass_pane(*b_, kind="clear", shadow=False, streak_at=0.8, name=f"Bay{i}")
                    for i, b_ in enumerate(bays + side))
        b += white_frame(bays + side, bar)
        b += "".join(ui.plate(x + inset, y + inset, w - 2 * inset, h - 2 * inset, name=f"Plate{i}")
                     for i, (x, y, w, h) in enumerate(bays))
        return b

    scene = lambda: ui.backdrop("office_sun", name="LiveScene")
    trace = lambda: ui.trace_brackets(1560, 300, 300, 306, ui.NAVY, seed=11, density=0.85)
    lock = lambda: ui.title_lockup(cx, ty + inset + (28 if lobby else 36), size=lock_size, serial=VERSION,
                                   name="TitleLockup")
    micro = lambda: ui.microtype_strip(cx, fy + f_h - inset - 16, LEGAL_SHORT, name="Legal")
    parts = content(st, cx, my + inset + 24, cw)
    mode, mk = d["mode"] if d.get("mode") else (None, None)
    msg, mkind = d["status"] if kind in ("menu", "lobby") else (None, None)
    foot = lambda: footer(cx, fy + inset - 4, cw, mode, mk, msg, mkind)

    if kind == "quit":
        with ui._quiet():
            scr = curtain_board(STATES[0], under=True)
        CUR["bd"], CUR["cue"] = bd, False
        wash = ui.rect(0, 0, W, H, (ui.PAPER, 0.45))
        bd.file("Frame", ("Frame", lambda: f'<g filter="url(#frost)">{scr}</g>' + wash, "ground"))
        bd.file("SheetLabel", ("SheetLabel", lambda: sheet_label("curtain wall", title), "annot"))
        bd.file("Modal", ("QuitConfirm", lambda: _modal(None)))
        bd.file("DesignNotes", ("DesignNotes", lambda: notes_card(_notes(st, "curtain")), "annot"))
        return bd

    if under:
        b = scene() + trace() + glass() + lock() + micro()
        for _, fn in parts:
            b += fn()
        return b + foot()

    bd.holders = [(x + inset, y + inset, x + w - inset, y + h - inset) for (x, y, w, h) in bays]
    bd.file("Frame", ("LiveScene", scene, "ground"))
    bd.file("SheetLabel", ("SheetLabel", lambda: sheet_label("curtain wall", title), "annot"))
    bd.file("TracedTower", ("TracedTower", trace))
    bd.file("CurtainWall", ("CurtainWall", glass, "ground"))
    bd.file("Title", ("TitleLockup", lock))
    bd.file("Content", *parts)
    bd.file("Status", ("Footer", foot), ("Legal", micro))
    bd.file("DesignNotes", ("DesignNotes", lambda: notes_card(_notes(st, "curtain")), "annot"))
    return bd


# --------------------------------------------------------------------------- main

def write_board(bd):
    folder = os.path.join(OUT, bd.key)
    paths = []
    order = [f for f in bd.files if f[0] != "DesignNotes"] + [f for f in bd.files if f[0] == "DesignNotes"]
    for n, (name, markup) in enumerate(order):
        num = 99 if name == "DesignNotes" else n
        paths.append(ui.write(os.path.join(folder, f"{bd.key}_{num:02d}_{name}.svg"), markup))
    return paths


def global_checks(all_paths):
    errs = []
    with open(os.path.abspath(__file__), encoding="utf-8") as fh:
        src = fh.read()
    if re.search(r"#[0-9A-Fa-f]{6}", src):
        errs.append("hex literal in make_main_menu.py")
    if OLD_NAME in src.lower():
        errs.append("the old game name in make_main_menu.py")
    for p in all_paths:
        with open(p, encoding="utf-8") as fh:
            s = fh.read()
        if re.search(r"<circle|<ellipse|\brx=", s):
            errs.append(f"rounded shape in {os.path.basename(p)}")
        for fam in set(re.findall(r'font-family="([^"]+)"', s)):
            if fam not in FAMILIES:
                errs.append(f"font family {fam} in {os.path.basename(p)}")
        if OLD_NAME in s.lower():
            errs.append(f"the old game name in {os.path.basename(p)}")
        if any(re.search(DOTS, t) for t in re.findall(r"<text[^>]*>([^<]*)</text>", s)):
            errs.append(f"three-dot ellipsis (use the glyph) in {os.path.basename(p)}")
    if ui.MISSING_GLYPHS:
        errs.append(f"missing glyphs: {sorted(ui.MISSING_GLYPHS)}")
    return errs


def main():
    preview = "--no-preview" not in sys.argv
    only = [a for a in sys.argv[1:] if not a.startswith("--")]
    if not only:
        if os.path.isdir(OUT):
            shutil.rmtree(OUT)
        if preview:
            for p in glob.glob(os.path.join(PREVIEW, "main_menu_*.png")):
                os.remove(p)
    failed = False
    all_paths = []
    for st in STATES:
        for skin in (survey_board, curtain_board):
            bd = skin(st)
            if only and not any(o in bd.key for o in only):
                continue
            paths = write_board(bd)
            all_paths += paths
            print(f"\n{bd.key}: {len(paths)} files (bottom to top)")
            errs = bd.check()
            for e in errs:
                print("  CHECK FAIL", e)
            failed |= bool(errs)
            if not errs:
                print("  checks: pass (overlaps, margin, panes, light rules, dark fills, 1 trace clear of text, "
                      "1 primary, 1 focus)" + (" [primary disabled by design]" if bd.primary_exception else ""))
            if preview:
                clean = [p for p in paths if not re.search(r"_(01_SheetLabel|99_DesignNotes)\.svg$", p)]
                ui.preview_many(clean, os.path.join(PREVIEW, f"main_menu_{bd.key}.png"))
                png = ui.preview_many(paths, os.path.join(PREVIEW, f"main_menu_{bd.key}_annotated.png"))
                print("   preview", os.path.relpath(png))
    errs = global_checks(all_paths)
    for e in errs:
        print("CHECK FAIL", e)
    failed |= bool(errs)
    print(f"\nText metrics: {ui.METRICS_SOURCE}")
    print("ALL CHECKS PASS" if not failed else "CHECKS FAILED")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
