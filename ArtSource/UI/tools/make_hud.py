"""Seal the Deal: HUD and in-game screen mockups (screen C), one SVG per component group.

    python3 ArtSource/UI/tools/make_hud.py              # from the repo root
    python3 ArtSource/UI/tools/make_hud.py --no-preview # skip the Quick Look PNGs

Writes ArtSource/UI/svg/hud/<board>/<code>_NN_Name.svg (NN = bottom-to-top place order; *_99_DesignNotes.svg is
annotation only: the sheet label strip and the notes card, hidden for the clean export) and two previews per
board: ArtSource/UI/svg/_preview/hud_<board>.png (annotated) and hud_<board>_clean.png.

  c01_crosshair        crosshair styles, hit marker, over-rival, on paper / cobalt / navy scene / sky bloom, 1:1 + 2x
  c02_hud_office_sun   full HUD, rival firm has the briefcase, health shown (damaged)
  c03_hud_sky_bloom    full HUD, briefcase dropped, clock warning at 12:00, hands free
  c04_hud_navy_dark    full HUD, you carry the briefcase, stairs prompt
  c05_refusals         "Briefcase required" at the stairs + "Hands full" pickup, one refusal style (two 1:1 crops)
  c06_down             down / respawn countdown under a paper wash + dot screen, secretary subtitle
  c07_left_building    fell out of the building, paper wash + plate band, secretary subtitle
  c08_floor_opened     FLOOR 8 OPENED (your firm) and the rival version (two 1:1 crops)
  c09_menu             in-game menu, frosted pane left, game live behind it under a light veil
  c09b_menu_leave      host's leave confirm over the menu ("Stay" holds focus)
  c10_results_roof     DEAL CLOSED, host view (Rematch / Return to reception)
  c10b_results_cap     QUARTER CLOSED at 15:00, client view (Ready for rematch / Leave game)
  c11_720p             board 2's HUD re-flowed on a 1280 x 720 artboard at Interface size 0.85, crosshair 1:1
  c11b_ultrawide       board 2's HUD on a 2560 x 1080 artboard, kept to the centre 16:9

Checks (exit 1 on any failure): text / item overlaps, safe margin, text crossing a pane edge, text under the board's
minimum over the world (16 px; 13.6 px on the 720p board), no amber or cyan text, the display face never under 22 px,
the kit's light rules (no light text on a light pane, no text on clear glass), no navy / black fill bigger than a
keyline outside the backdrop, the centre 40 % x 40 % open (HUD boards), at most one trace per board and never touching
text, missing glyphs, no hex literal in this file, no circle / ellipse / rx and only the stand-in families in the
output SVGs. Notes and the ultrawide's translated copy are not checked for layout; the 720p board is (re-flowed
items carry their real 720p boxes).
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
OUT = os.path.normpath(os.path.join(HERE, "..", "svg", "hud"))
PREVIEW = os.path.normpath(os.path.join(HERE, "..", "svg", "_preview"))
S = ui.SAFE
CENTRE_BOX = (576, 324, 1344, 756)   # central 40 % x 40 % of 1920 x 1080
CENTRE_OK = ("Crosshair", "Prompt")
PERSISTENT = ("OpenFloor", "BriefcaseTracker", "Clock", "HandsSlot")
CONTEXTUAL = ("ClockWarning", "Health", "Prompt", "Teammate", "BriefcaseMarker", "RivalPlayer")
FAMILIES = {ui.DISPLAY, ui.UI_FACE, ui.UI_THIN, ui.DATA}
LADDER = 12   # owner 2026-11-12: L, floors 1-9, B, R; floor N = segment N (open_floor total=12)
FLOOR_NAMES = ["L"] + [str(i) for i in range(1, 10)] + ["B", "R"]   # 12 storeys, index 0..11

YOU, TEAM, RIVALS, HOST = "Priya_R", ("Dana", "mkovac"), ("jonno", "sasha_k", "tealeaf"), "Dana"  # = the lobby roster

NEEDS_CODE = {  # element -> what is missing (inventory 1.10 / 3 + owner rules 2026-11-12)
    "BriefcaseTracker": "briefcase object, carrier + tower + floor state (host)",
    "OpenFloor": "open-floor rule and value (host)",
    "Clock": "match clock, start tick + 15:00 cap (host)",
    "Health": "damage model and health (owner open question)",
    "HandsSlot": "pickups, weapons and ammo (host)",
    "Prompt": "interact look-at + pickup / stairs rules",
    "Teammate": "synced team + names (team is host-only today)",
    "BriefcaseMarker": "briefcase world position (host)",
    "Crosshair": "exists (Crosshair / CrosshairRing); hit marker and rival state need hit + team data",
}


# --------------------------------------------------------------------------- board + checks

class Board:
    """One artboard: files bottom to top, each a list of named items drawn at absolute coordinates.
    Item tags: "ground" (backdrop, washes: not overlap-checked), "notes" (annotation: not checked),
    "scaled" (a transformed copy: the registry boxes are not true positions, so not checked)."""

    def __init__(self, key, code, title, w=1920, h=1080, edge=S, centre=False, min_px=16):
        self.key, self.code, self.title = key, code, title
        self.w, self.h, self.edge, self.centre, self.min_px = w, h, edge, centre, min_px
        self.centre_box = (w * 0.3, h * 0.3, w * 0.7, h * 0.7)
        self.files, self.items = [], []
        ui.reset_registry()

    def file(self, name, *items):
        body = ""
        for it in items:
            iname, fn = it[0], it[1]
            tag = it[2] if len(it) > 2 else None
            k = ui.mark()
            markup = fn()
            shapes, texts, panes = ui.since(k)
            boxes = [s["box"] for s in shapes]
            box = (min(b[0] for b in boxes), min(b[1] for b in boxes),
                   max(b[2] for b in boxes), max(b[3] for b in boxes)) if boxes else None
            worlds = [sh["box"] for sh in shapes if sh.get("backdrop")]
            self.items.append(dict(name=iname, file=name, box=box, texts=texts, panes=panes, tag=tag, worlds=worlds,
                                   markup=markup))
            body += ui.group(iname, markup)
        self.files.append((name, ui.group(f"{self.code.upper()}-{name}", body)))

    def noise(self, names):
        """Union area (px^2) of the boxes of items whose name starts with one of names."""
        rows = [bytearray(self.w) for _ in range(self.h)]
        for it in self.items:
            if it["box"] and it["tag"] is None and it["name"] in names:
                x0, y0, x1, y1 = (max(0, int(it["box"][0])), max(0, int(it["box"][1])),
                                  min(self.w, int(it["box"][2] + 0.999)), min(self.h, int(it["box"][3] + 0.999)))
                for y in range(y0, y1):
                    rows[y][x0:x1] = b"\x01" * (x1 - x0)
        return sum(r.count(1) for r in rows)

    def check(self):
        errs = []
        live = [it for it in self.items if it["tag"] not in ("notes", "scaled")]
        solid = [it for it in live if it["tag"] != "ground" and it["box"]]
        for i, a in enumerate(solid):
            for b in solid[i + 1:]:
                if _hit(_grow(a["box"], 2), _grow(b["box"], 2)):
                    errs.append(f"items overlap: {a['name']} {_r(a['box'])} / {b['name']} {_r(b['box'])}")
        texts = [(it["name"], t) for it in live for t in it["texts"]]
        for i, (na, a) in enumerate(texts):
            for nb, b in texts[i + 1:]:
                ga = (a["box"][0] - 4, a["box"][1] + 0.5, a["box"][2] + 4, a["box"][3] - 0.5)
                if _hit(ga, _grow(b["box"], -0.5)):
                    errs.append(f"text overlap: '{a['s']}' ({na}) / '{b['s']}' ({nb})")
        e = self.edge
        for it in solid:
            bx = it["box"]
            if bx[0] < e or bx[1] < e or bx[2] > self.w - e or bx[3] > self.h - e:
                errs.append(f"outside the {e} px margin: {it['name']} {_r(bx)}")
        panes = [p for it in live for p in it["panes"]]
        for name, t in texts:
            tb = t["box"]
            inside = [p for p in panes if _inside(tb, _grow(p["box"], 0.5))]
            for p in panes:
                if _hit(tb, p["box"]) and not _inside(tb, _grow(p["box"], 0.5)):
                    errs.append(f"text crosses a pane edge: '{t['s']}' ({name})")
            on_menu = any(p["kind"] == "frosted" for p in inside)   # menu panes are not read mid-action
            if (t["size"] < self.min_px - 0.01 and not on_menu
                    and any(_hit(tb, w) for it in live for w in it["worlds"])):
                errs.append(f"text under {self.min_px} px over the world: '{t['s']}' ({name})")
            fill = ui._hex(t["fill"])
            if fill in (ui.AMBER, ui.CYAN):
                errs.append(f"{'amber' if fill == ui.AMBER else 'cyan'} text: '{t['s']}' ({name})")
            if t["family"] == ui.DISPLAY and t["size"] < 22 * t.get("scale", 1) - 0.01:
                errs.append(f"display face under 22 px: '{t['s']}' ({name})")
        errs += ui.layout_rules(live)   # kit light rules: no light text on a light pane, nothing on clear glass
        for it in live:
            if it["tag"] in ("ground",):
                continue
            for w, h, col in dark_fills(it["markup"]):
                errs.append(f"dark panel: {col} fill {w:g} x {h:g} in {it['name']}")
        if self.centre:
            for it in solid:
                if _hit(it["box"], self.centre_box) and not it["name"].startswith(CENTRE_OK):
                    errs.append(f"centre 40 % x 40 % not open: {it['name']} {_r(it['box'])}")
        for tb in ui.TRACE_BOXES:
            for name, t in texts:
                if _hit(_grow(tb, 2), t["box"]):
                    errs.append(f"trace square touches text: '{t['s']}' ({name})")
                    break
        n_trace = sum(m.count('id="TraceCluster"') for _, m in self.files)
        if n_trace > 1:
            errs.append(f"{n_trace} trace clusters (max 1)")
        self.n_trace = n_trace
        return errs


def _grow(b, d):
    return (b[0] - d, b[1] - d, b[2] + d, b[3] + d)


def _hit(a, b):
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]


def _inside(a, b):
    return a[0] >= b[0] and a[1] >= b[1] and a[2] <= b[2] and a[3] <= b[3]


def _r(b):
    return tuple(round(v) for v in b)


SCENE_IDS = ("Backdrop-", "ScenePatch", "SceneZoom", "VignetteBack", "Back")   # painted world: not UI
_RECT = re.compile(r'<rect x="[^"]*" y="[^"]*" width="([\d.]+)" height="([\d.]+)" fill="(#[0-9A-F]{6})"'
                   r'(?: fill-opacity="([\d.]+)")?')


def strip_scene(markup):
    """markup without the painted-world groups (ids starting with SCENE_IDS), balanced on <g>/</g>."""
    out, i = [], 0
    pat = re.compile(r'<g id="(?:%s)' % "|".join(re.escape(p) for p in SCENE_IDS))
    while True:
        m = pat.search(markup, i)
        if not m:
            out.append(markup[i:])
            return "".join(out)
        out.append(markup[i:m.start()])
        depth, j = 0, m.start()
        for t in re.finditer(r"<g\b|</g>", markup[m.start():]):
            depth += 1 if t.group(0) == "<g" else -1
            if depth == 0:
                j = m.start() + t.end()
                break
        i = j


def dark_fills(markup):
    """[(w, h, name)] for navy / black rect fills (alpha >= 0.5) bigger than a keyline, bullet or crosshair edge
    (both sides > 4 px and area > 400 px^2), outside the painted world."""
    dark = {ui.NAVY: "navy", ui.BLACK: "black"}
    out = []
    for w, h, col, a in _RECT.findall(strip_scene(markup)):
        w, h = float(w), float(h)
        if col in dark and float(a or 1) >= 0.5 and min(w, h) > 4 and w * h > 400:
            out.append((w, h, dark[col]))
    return out


# --------------------------------------------------------------------------- local helpers (proposed kit changes)

def tw(s, role="item", **kw):
    return ui.text_width(s, role, **kw)


def ctext(cx, y, s, role="item", fill=ui.NAVY, **kw):
    """Centred text by computed start x (no text-anchor, so "·" strings place right in VectorCraft)."""
    w = ui.text_width(s, role, size=kw.get("size"), family=kw.get("family"), weight=kw.get("weight"), ls=kw.get("ls"))
    return ui.text(cx - w / 2, y, s, role, fill, **kw)


def tracker_w(state, tower=None, floor=None, countdown=None):
    head = {"you": "You carry the briefcase", "team": "Your firm has the briefcase",
            "rival": "Rival firm has the briefcase", "dropped": "Briefcase dropped",
            "returning": "Briefcase returning"}[state]
    if state == "returning":
        sub = f"BACK IN PLAY IN {countdown or '0:12'}"
    else:
        t = {"your": "YOUR TOWER", "rival": "RIVAL TOWER"}.get(tower or "your", "YOUR TOWER")
        sub = f"{t} · FLOOR {floor}" if floor is not None else t
    return int(88 + max(tw(head, "item"), tw(sub, "hud", size=16, ls=1)) + 20)


def open_floor_w(floor, holder):
    sub = "Briefcase in your tower" if holder == "your" else "Briefcase in the rival tower"
    lad_w = LADDER * 14 - 4
    return int(16 + max(tw(f"OPEN FLOOR {floor}", "hud", size=24), 22 + tw(sub, "label")) + 24 + lad_w + 34)


def chip_w(value, mono=True, icon=None):
    return int(16 + (28 if icon else 0) + tw(value, "hud" if mono else "item", size=28 if mono else 20) + 18)


def stairs_w(floor):
    kw_ = max(32, int(tw("F", "hud", size=16) + 18))
    return 12 + 48 + 12 + kw_ + 12 + tw(f"Open floor {floor}", "item") + 16


def toast_w(head, body, numeral=None):
    return int(max(tw(head, "heading", size=26, ls=3), tw(body, "item")) + 64 + (96 if numeral is not None else 0))


def xhair(cx, cy, style="crossdot", arm=6, th=2, gap=4, dot=2, ol=1, core=ui.LIGHT, edge=ui.BLACK):
    """Parametric crosshair (ux_research 3): style cross | dot | crossdot. Integer cx, cy keep every edge on a
    pixel. Outline pieces first, core on top."""
    h = th // 2
    pieces = []
    if style in ("cross", "crossdot"):
        pieces += [(cx - gap - arm, cy - h, arm, th), (cx + gap, cy - h, arm, th),
                   (cx - h, cy - gap - arm, th, arm), (cx - h, cy + gap, th, arm)]
    if style in ("dot", "crossdot"):
        d = dot // 2
        pieces.append((cx - d, cy - d, dot, dot))
    b = "".join(ui.rect(x - ol, y - ol, w + 2 * ol, hh + 2 * ol, edge) for x, y, w, hh in pieces)
    return b + "".join(ui.rect(x, y, w, hh, core) for x, y, w, hh in pieces)


def health_bar(x, y, hp):
    """Thin health readout, shown only while damaged: 280 x 48 white plate (HUD_GLASS) with the 2 px cobalt
    system rule, ink-2 "Health", 10 segments (cobalt filled, navy outlines empty) + navy mono value."""
    kind, ink, ink2, _ = ui._hud_look()
    b = ui.glass_pane(x, y, 280, 48, kind, name=ui.uid("HealthGlass"))
    b += ui.rect(x, y, 280, 2, ui.COBALT)
    b += ui.text(x + 16, y + 30, "Health", "label", ink2)
    filled = round(hp / 10)
    for k in range(10):
        sx = x + 84 + k * 13
        b += ui.rect(sx, y + 20, 11, 10, ui.COBALT) if k < filled else \
            ui.rect(sx + 0.5, y + 20.5, 10, 9, None, (ui.NAVY, 0.45), 1)
    b += ui.text(x + 226, y + 32, str(hp), "hud", ink, size=20)
    return b


def teammate(cx, cy, name):
    """Your-firm player in the world: cyan diamond (navy keyline) + a name tag on a white plate (navy label 16)."""
    w = int(tw(name, "label") + 20)
    kind, ink, _, _ = ui._hud_look()
    b = ui.team_marker(cx, cy, "you", 10)
    b += ui.glass_pane(cx - w / 2, cy + 16, w, 28, kind, name=ui.uid("NameTag"))
    b += ctext(cx, cy + 35, name, "label", ink)
    return b


def subtitle(lines, speaker="Reception", bottom=920, cx=960):
    """Secretary subtitle (ux_research 4): 30 px, at most 2 lines of 42 characters, bottom edge 160 px above the
    frame bottom, on a white plate with a 4 px cobalt spine (the toast's). Speaker: a cyan status square with a
    navy keyline + ink-2 label 16."""
    assert len(lines) <= 2 and all(len(s) <= 42 for s in lines), lines
    w = int(max(tw(s, "item", size=30) for s in lines) + 64)
    h = 44 + len(lines) * 40 + 12
    x, y = cx - w / 2, bottom - h
    kind, ink, ink2, _ = ui._hud_look()
    b = ui.glass_pane(x, y, w, h, kind, name=ui.uid("SubtitleGlass"))
    b += ui.rect(x, y, 4, h, ui.COBALT)
    b += ui.cyan_square(x + 28, y + 17, 8, keyline=ui.NAVY) + ui.text(x + 44, y + 26, speaker, "label", ink2)
    for i, s in enumerate(lines):
        b += ctext(cx, y + 70 + i * 40, s, "item", ink, size=30)
    return b


def countdown_bar(cx, y, left, total=5, seg=40, gap=8):
    """Segmented countdown: `left` of `total` seconds filled cobalt, the spent ones as navy outlines."""
    w = total * seg + (total - 1) * gap
    x0 = cx - w / 2
    b = ""
    for k in range(total):
        sx = x0 + k * (seg + gap)
        b += ui.rect(sx, y, seg, 6, ui.COBALT) if k < left else \
            ui.rect(sx + 0.5, y + 0.5, seg - 1, 5, None, ui.HAIR_STRONG, 1)
    return b


def refusal_w(verb, obj, reason, picto=None, key="F"):
    kw_ = max(32, int(tw(key, "hud", size=16) + 18))
    w = 12 + (48 + 12 if picto else 0) + kw_ + 12 + tw(verb, "item") + (tw(" · " + obj, "item") if obj else 0)
    return int(w + 10 + 32 + tw(reason, "item") + 16 + 6)   # gap, lock, reason, chip pad, plate pad


def refusal(x, y, verb, obj, reason, picto=None, key="F", name=None):
    """ONE refusal style (proposed kit change: stairs_refusal and interact_prompt(state="blocked") differ in the kit).
    48 px white plate: [pictogram] + dashed F keycap + the refused action in ink-2, then a reason chip: a solid
    amber plate (1 px near-black edge) with a navy lock and the navy reason. Never colour alone: dashed key + lock.
    "Pick up · Office chair  [lock Hands full]" and "Open floor 8  [lock Briefcase required]" read the same."""
    kw_ = max(32, int(tw(key, "hud", size=16) + 18))
    w = refusal_w(verb, obj, reason, picto, key)
    kind, ink, ink2, _ = ui._hud_look()
    b = ui.glass_pane(x, y, w, 48, kind, name=ui.uid("RefusalGlass"))
    b += ui.rect(x, y, w, 2, ui.COBALT)
    kx = x + 12
    if picto:
        b += ui.pictogram(picto, kx, y + 12, 1.2, ink2)
        kx += 48 + 12
    b += ui.rect(kx + 0.75, y + 8.75, kw_ - 1.5, 30.5, None, (ink, 0.6), 1.5, dash="3 2")
    b += ui.text(kx + kw_ / 2, y + 30, key, "hud", ink2, size=16, anchor="middle")
    tx = kx + kw_ + 12
    b += ui.text(tx, y + 31, verb, "item", ink2)
    tx += tw(verb, "item")
    if obj:
        b += ui.text(tx, y + 31, " · " + obj, "item", ink2)
        tx += tw(" · " + obj, "item")
    cx0 = tx + 10
    cw = x + w - 6 - cx0
    b += ui.glass_pane(cx0, y + 6, cw, 36, ui.WARN_GLASS, shadow=False, name=ui.uid("ReasonChip"))
    b += ui.lock_glyph(cx0 + 16, y + 26, 6, ui.NAVY)
    b += ui.text(cx0 + 32, y + 31, reason, "item", ui.NAVY)
    return ui.group(name or ui.uid("Refusal"), b)


def sky_band(x, y, w, h, cols=5, frame=8, seed=3, name="SkyStrip"):
    """A short, wide sky window (ref 08) for small places: deep sky with ONE low stepped cumulus in the right half,
    paper mullions, silver reveals, hairline edge. ui.sky_window's two big cumulus fill a strip under ~120 px tall
    (proposed kit change: a cloud-height knob on sky_window)."""
    with ui._quiet():
        sky = ui.rect(x, y, w, h, "url(#skyDeep)")
        sky += ui._cumulus(x + w * 0.62, y + h + 6, w * 0.22, h * 0.4, seed=seed, step=4)
        pw = (w - frame * (cols + 1)) / cols
        fr = ""
        for i in range(cols + 1):
            fr += ui.rect(x + i * (pw + frame), y, frame, h, ui.PAPER)
        fr += ui.rect(x, y, w, frame, ui.PAPER) + ui.rect(x, y + h - frame, w, frame, ui.PAPER)
        for i in range(cols):
            px = x + frame + i * (pw + frame)
            fr += ui.rect(px, y + frame, pw, 2, (ui.SILVER, 0.9)) + ui.rect(px, y + frame, 2, h - 2 * frame, (ui.SILVER, 0.6))
        fr += ui.rect(x + 0.5, y + 0.5, w - 1, h - 1, None, ui.HAIR_STRONG, 1)
        body = ui.clip(x, y, w, h, sky) + fr
    ui._reg("shapes", x, y, x + w, y + h)
    return ui.group(name, body)


def sheet_label(w, code, title):
    """The crop-off header strip (kept in DesignNotes)."""
    b = ui.rect(0, 0, w, 32, ui.PAPER) + ui.line(0, 31.5, w, 31.5, ui.HAIR_STRONG)
    b += ui.square_bullet(16, 12, 8, ui.BLACK)
    b += ui.text(32, 21, f"{ui.GAME_TITLE} · HUD · SURVEY · {code}  {title}", "caption", ui.NAVY, ls=2)
    return b


def notes_card(x, y, w, lines, size=14):
    """DesignNotes on a solid paper card (it sits over the game; annotation only)."""
    inner = ui.text(x + 16, y + 26, "DESIGN NOTES", "caption", ui.COBALT, ls=2)
    yy = y + 50
    for i, n in enumerate(lines, 1):
        inner += ui.text(x + 16, yy, f"{i}", "caption", ui.COBALT, ls=0)
        md, hgt = ui.text_lines(x + 36, yy, n, w - 52, "label", ui.INK2, size=size, weight=400, leading=1.32)
        inner += md
        yy += hgt + 5
    h = yy - y + 6
    return ui.rect(x, y, w, h, ui.PAPER, ui.HAIR_STRONG) + inner


def needs_code(*names):
    return "Needs new code: " + "; ".join(f"{n} ({NEEDS_CODE[n]})" for n in names) + "."


# --------------------------------------------------------------------------- the HUD (boards 2-4, 11)

HUD_CFG = {
    "office_sun": dict(case=("rival", "rival", 6), open=(6, "rival"), clock="11:42", warn=False,
                       slot=("pistol", "held", "7 / 48"), health=62, prompt="pickup",
                       marker=("rival", 1400, 330, "36 m"), mate=("Dana", 1560, 716)),
    "sky_bloom": dict(case=("dropped", "rival", 7), open=(7, "rival"), clock="12:00", warn=True,
                      slot=(None, "held", None), health=None, prompt="pickup",
                      marker=("dropped", 1560, 420, "18 m"), mate=("mkovac", 1470, 700)),
    "navy_dark": dict(case=("you", "your", 8), open=(8, "your"), clock="09:15", warn=False,
                      slot=("briefcase", "held", None), health=None, prompt="stairs",
                      marker=("player", 1480, 380, None), mate=("Dana", 1440, 700)),
}


def hud_files(cfg, cross=True, top_only=False):
    """[(file_name, [(item, fn)])] for one HUD state, in 1920 x 1080 coordinates."""
    st, tower, fl = cfg["case"]
    of, holder = cfg["open"]
    trk_w = tracker_w(st, tower, fl)
    files = []
    top = [("OpenFloor", lambda: ui.open_floor(S, S, of, holder, total=LADDER)),
           ("BriefcaseTracker", lambda: ui.briefcase_tracker(960 - trk_w / 2, S, st, tower, fl, w=trk_w))]
    cw = chip_w(cfg["clock"])
    if cfg["warn"]:
        ww = chip_w("Quarter closes in 3:00", mono=False, icon="warn")
        x0 = 960 - (cw + 12 + ww) / 2
        top += [("Clock", lambda: ui.hud_chip(x0, 124, cfg["clock"])),
                ("ClockWarning", lambda: ui.hud_chip(x0 + cw + 12, 124, "Quarter closes in 3:00", mono=False,
                                                     tone="warning", icon="warn"))]
    else:
        top.append(("Clock", lambda: ui.hud_chip(960 - cw / 2, 124, cfg["clock"])))
    if top_only:
        return [("TopHud", top)]
    kind, mx, my, dist = cfg["marker"]
    world = [("Teammate", lambda: teammate(cfg["mate"][1], cfg["mate"][2], cfg["mate"][0]))]
    if kind == "player":
        world.append(("RivalPlayer", lambda: ui.team_marker(mx, my, "rival", 10)))
    else:
        world.append(("BriefcaseMarker", lambda: ui.briefcase_marker(mx, my, kind, dist)))
    files.append(("WorldMarkers", world))
    files.append(("TopHud", top))
    item, sst, ammo = cfg["slot"]
    bottom = [("HandsSlot", lambda: ui.item_slot(1920 - S - ui.SIZE["slot_w"], 1080 - S - ui.SIZE["slot_h"], item, sst, ammo))]
    if cfg["health"] is not None:
        bottom.insert(0, ("Health", lambda: health_bar(S, 1080 - S - 48, cfg["health"])))
    files.append(("BottomHud", bottom))
    centre = []
    if cfg["prompt"] == "pickup":
        pw = ui.interact_prompt_width("Pick up", "AR-15")
        centre.append(("Prompt", lambda: ui.interact_prompt(960 - pw / 2, 600, "Pick up", "AR-15")))
    elif cfg["prompt"] == "stairs":
        sw = stairs_w(of + 1)
        centre.append(("Prompt", lambda: ui.stairs_prompt(960 - sw / 2, 600, of + 1, "ready")))
    if cross:
        centre.insert(0, ("Crosshair", lambda: ui.crosshair(960, 540)))
    files.append(("Centre", centre))
    return files


def hud_board(n, kind, title):
    cfg = HUD_CFG[kind]
    bd = Board(f"c0{n}_hud_{kind}", f"c0{n}", title, centre=True)
    bd.file("Backdrop", ("Backdrop", lambda: ui.backdrop(kind), "ground"))
    for fname, items in hud_files(cfg):
        bd.file(fname, *items)
    pers = bd.noise(PERSISTENT) / (1920 * 1080) * 100
    allc = bd.noise(PERSISTENT + CONTEXTUAL) / (1920 * 1080) * 100
    bd.noise_pct = (pers, allc)
    names = ["BriefcaseTracker", "OpenFloor", "Clock", "HandsSlot", "Prompt", "Teammate"]
    if cfg["health"] is not None:
        names.append("Health")
    if cfg["marker"][0] != "player":
        names.append("BriefcaseMarker")
    lines = [
        f"Noise budget: persistent HUD (open floor, briefcase tracker, clock, hands slot) covers {pers:.1f} % of "
        f"the frame; with this board's contextual blocks {allc:.1f} %. Research target: 4 % persistent "
        "(ux_research 4). Crosshair excluded.",
        "Centre 40 % x 40 % holds only the crosshair and the prompt. Every block is navy type on a white plate "
        "(2 px cobalt rule); only the rival tracker is a solid amber plate. Interact is F.",
        needs_code(*names),
        "Clock counts up toward the 15:00 cap and warns from 12:00 (inventory 5.6). ux_research 4 says a top-left "
        "countdown: doc mismatch, the inventory and brief look right.",
    ]
    if kind == "office_sun":
        lines += ["Health is an owner open question (inventory 7 Q2): drawn only when damaged, on this board only.",
                  "Holding a pistol: picking up the AR-15 swaps it (assumption, one hands slot)."]
    if kind == "sky_bloom":
        lines += ["Clock reads 12:00, so the warning reads 3:00 left of the 15:00 cap.",
                  "Briefcase dropped in the rival tower on floor 7: the marker shows it with a dashed frame."]
    if kind == "navy_dark":
        lines += ["You carry the briefcase: no marker over yourself (kit rule); the slot and tracker say it. "
                  "Your prompt is the carrier's \"F · Open floor 9\", not a pickup (your hands are full).",
                  "The amber triangle is a rival player in your tower. Rival player markers need a decision "
                  "(ux_research 4: only on first sight, 2 s)."]
    bd.file("DesignNotes", ("Notes", lambda: sheet_label(1920, f"0{n}", title) + notes_card(S, 140, 500, lines), "notes"))
    return bd


# --------------------------------------------------------------------------- board 1: crosshair sheet

XCOLS = [  # header, spec, drawer(cx, cy)
    ("Default · cross + dot", "arm 6 · 2 px · gap 4 · dot 2 · edge 1", lambda x, y: ui.crosshair(x, y)),
    ("Dot", "dot 4 · edge 1", lambda x, y: xhair(x, y, "dot", dot=4)),
    ("Cross", "arm 6 · 2 px · gap 4 · edge 1", lambda x, y: xhair(x, y, "cross")),
    ("Cross + dot · large", "arm 10 · 2 px · gap 5 · dot 4", lambda x, y: xhair(x, y, "crossdot", arm=10, gap=5, dot=4)),
    ("Heavy outline", "arm 6 · 2 px · gap 4 · edge 2", lambda x, y: xhair(x, y, ol=2)),
    ("Wide gap", "arm 6 · 2 px · gap 10 · dot 2", lambda x, y: xhair(x, y, gap=10)),
    ("Hit marker", "ticks 8 to 13 px · 100 ms", lambda x, y: ui.crosshair(x, y, "hit")),
    ("Over rival", "amber core, navy edge", lambda x, y: ui.crosshair(x, y, "rival")),
]
XROWS = [  # row name, flat field (paper / cobalt brand block) or a painted-scene patch (kind, crosshair centre)
    ("Paper", ui.PAPER, None), ("Cobalt", ui.COBALT, None),
    ("Navy scene", None, ("navy_dark", (1750, 500))),   # plain navy wall of the service room (worst case for the edge)
    ("Sky bloom", None, ("sky_bloom", (880, 420)))]     # clipped bloom (worst case for the paper core)


def board_c01():
    bd = Board("c01_crosshair", "c01", "CROSSHAIR", edge=24)
    x0, cw, gap, top, rh, rg = 176, 200, 8, 148, 164, 12

    def frame():
        return ui.rect(0, 0, 1920, 1080, ui.PAPER) + ui.hairline_grid(0, 0, 1920, 1080, faint=True, name="Grid") + \
            ui.crop_marks(16, 16, 1888, 1048, name="SheetCrop")
    bd.file("Frame", ("Frame", frame, "ground"))

    def labels():
        b = ui.text(64, 88, "Crosshair at 1:1 (left of each cell) with a 2× inset (right)", "item", ui.NAVY)
        b += ui.text_right(1856, 88, "Physical pixels: the crosshair never scales with the HUD", "label", ui.INK2)
        for j, (hd, spec, _) in enumerate(XCOLS):
            x = x0 + j * (cw + gap)
            b += ui.text(x, 128, hd, "label", ui.NAVY)
            b += ui.text_lines(x, top + 4 * (rh + rg) + 8, spec, cw - 8, "caption", ui.INK2, size=12, ls=0,
                               leading=1.35)[0]
        for i, (nm, _, _) in enumerate(XROWS):
            b += ui.text(64, top + i * (rh + rg) + rh / 2 + 6, nm, "label", ui.NAVY)
        return b
    bd.file("Labels", ("Labels", labels, "ground"))

    cells = []
    for i, (rn, bg, scene) in enumerate(XROWS):
        for j, (hd, _, draw) in enumerate(XCOLS):
            def cell(i=i, j=j, bg=bg, scene=scene, draw=draw):
                x, y = x0 + j * (cw + gap), top + i * (rh + rg)
                cx, cy = x + 44, y + rh // 2
                isz = 128
                ix, iy = x + cw - isz - 10, y + (rh - isz) // 2
                if scene:
                    kind, (sx, sy) = scene
                    b = ui.backdrop(kind, x, y, cw, rh, crop=(sx - 44, sy - rh // 2, cw, rh), name=ui.uid("ScenePatch"))
                    with ui._quiet():
                        zbg = ui.backdrop(kind, -40, -40, 80, 80, crop=(sx - 40, sy - 40, 80, 80),
                                          name=ui.uid("SceneZoom"))
                else:
                    b = ui.rect(x, y, cw, rh, bg, ui.HAIR if bg == ui.PAPER else None)
                    with ui._quiet():
                        zbg = ui.rect(-40, -40, 80, 80, bg)
                b += draw(cx, cy)
                with ui._quiet():
                    b += ui.clip(ix, iy, isz, isz, ui.group(ui.uid("Zoom"), zbg + draw(0, 0),
                                                            transform=f"translate({ix + isz // 2} {iy + isz // 2}) scale(2)"))
                lit = bg == ui.PAPER or (scene and scene[0] == "sky_bloom")
                b += ui.rect(ix - 0.5, iy - 0.5, isz + 1, isz + 1, None, ui.NAVY if lit else (ui.LIGHT, 0.6), 1)
                return b
            cells.append((f"Cell-{rn}-{j}", cell))
    bd.file("Cells", *cells)

    def notes():
        lines = [
            "Default (ux_research 3): paper core, 2 px arms 6 long, 4 px gap, 2 px dot, 1 px Near Black edge at 100 %: the edge "
            f"carries it on paper and sun bloom ({ui.contrast(ui.BLACK, ui.PAPER):.1f}:1+), the core on cobalt "
            f"({ui.contrast(ui.PAPER, ui.COBALT):.1f}:1) and the navy scene ({ui.contrast(ui.PAPER, ui.NAVY):.1f}:1). "
            "Over rival: amber core, navy edge. Hit marker: ticks for 100 ms + a tick sound.",
            "\"Cross + dot\" is the default, so that column shows the large preset. White is painted as Paper (pure white is "
            "never painted); the navy and sky-bloom rows are patches of the painted scenes (no dark swatch), the cobalt row "
            "a brand block. Cells are 1:1 at 1080p, and the crosshair is in physical pixels, so they hold at 720p too.",
            needs_code("Crosshair") + " Presets need the settings rows (ux_research 3). Proposed kit change: a parametric "
            "crosshair (style, arm, thickness, gap, dot, outline) beside the fixed one.",
        ]
        return sheet_label(1920, "01", "CROSSHAIR") + notes_card(64, 912, 1792, lines, 13)
    bd.file("DesignNotes", ("Notes", notes, "notes"))
    return bd


# --------------------------------------------------------------------------- vignettes (boards 5, 8)

def vignette_back(x0, kind="office_sun"):
    return ui.backdrop(kind, x0, 0, 960, 1080, crop=(480, 0, 960, 1080), name=ui.uid("VignetteBack"))


def divider():
    """A paper window mullion between the two crops (ref 08 device), silver reveal + hairline edges."""
    return (ui.rect(952, 0, 16, 1080, ui.PAPER) + ui.rect(952, 0, 3, 1080, (ui.SILVER, 0.9)) +
            ui.line(951.5, 0, 951.5, 1080, ui.HAIR_STRONG) + ui.line(968.5, 0, 968.5, 1080, ui.HAIR_STRONG))


def board_c05():
    bd = Board("c05_refusals", "c05", "REFUSALS")
    bd.file("Backdrop", ("BackLeft", lambda: vignette_back(0), "ground"), ("BackRight", lambda: vignette_back(960), "ground"),
            ("Divider", divider, "ground"))
    lw = tracker_w("team", "your", 7)
    rw = tracker_w("you", "your", 7)
    bd.file("TopHud",
            ("BriefcaseTrackerL", lambda: ui.briefcase_tracker(480 - lw / 2, S, "team", "your", 7, w=lw)),
            ("BriefcaseTrackerR", lambda: ui.briefcase_tracker(1440 - rw / 2, S, "you", "your", 7, w=rw)))
    sw = refusal_w("Open floor 8", None, "Briefcase required", "stairs")
    pw = refusal_w("Pick up", "Office chair", "Hands full")
    bd.file("Centre",
            ("CrosshairL", lambda: ui.crosshair(480, 540)),
            ("PromptStairsRefusal", lambda: refusal(480 - sw / 2, 600, "Open floor 8", None, "Briefcase required",
                                                    "stairs")),
            ("CrosshairR", lambda: ui.crosshair(1440, 540)),
            ("PromptHandsFull", lambda: refusal(1440 - pw / 2, 600, "Pick up", "Office chair", "Hands full")))
    sx = 960 - S - ui.SIZE["slot_w"]
    sy = 1080 - S - ui.SIZE["slot_h"]
    bd.file("BottomHud",
            ("HandsSlotL", lambda: ui.item_slot(sx, sy, "pistol", "held", "7 / 48")),
            ("HandsSlotR", lambda: ui.item_slot(sx + 960, sy, "briefcase")))
    lines = [
        "Left: your firm has the briefcase but you do not; the stairs to floor 8 refuse you (\"Briefcase required\"). "
        "Right: you carry it, so a pickup is refused (\"Hands full\").",
        "One refusal style for both: white plate, dashed F keycap, the refused action in ink-2, then a solid amber "
        "reason chip with a navy lock and navy reason. Never colour alone (dashed key + lock). Proposed kit change: "
        "stairs_refusal and interact_prompt(state=\"blocked\") still draw two different looks in the kit.",
        "Assumption to confirm: carrying the briefcase fills your hands; holding a weapon and picking up another swaps.",
        "Both prompts sit 60 px under the crosshair, as in game. Each half is a 1:1 crop of the 1920 view.",
        needs_code("Prompt", "BriefcaseTracker", "HandsSlot"),
    ]
    bd.file("DesignNotes", ("Notes", lambda: sheet_label(1920, "05", "REFUSALS") + notes_card(S, 140, 420, lines), "notes"))
    return bd


def board_c08():
    bd = Board("c08_floor_opened", "c08", "FLOOR OPENED")
    bd.file("Backdrop", ("BackLeft", lambda: vignette_back(0), "ground"), ("BackRight", lambda: vignette_back(960), "ground"),
            ("Divider", divider, "ground"))
    bd.file("TopHud", ("OpenFloorL", lambda: ui.open_floor(S, S, 8, "your", total=LADDER)),
            ("OpenFloorR", lambda: ui.open_floor(960 + S, S, 9, "rival", total=LADDER)))
    yh, yb = "FLOOR 8 OPENED", "Your firm carried the briefcase to floor 8."
    rh, rb = "FLOOR 9 OPENED", "Rival firm carried the briefcase to floor 9."
    yw, rw = toast_w(yh, yb, 8), toast_w(rh, rb, 9)
    bd.file("Centre",
            ("CrosshairL", lambda: ui.crosshair(480, 540)),
            ("ToastYours", lambda: ui.toast(480 - yw / 2, 660, yh, yb, numeral=8)),
            ("CrosshairR", lambda: ui.crosshair(1440, 540)),
            ("ToastRival", lambda: ui.toast(1440 - rw / 2, 660, rh, rb, tone="warning", numeral=9)))
    lines = [
        "Left: your firm opens a floor (white plate, cobalt spine, silver numeral). Right: the rival version (solid amber "
        "plate, navy headline, body and spine). One banner at a time, 120 px under the crosshair: slide in 150 ms, "
        "hold 2 s, out 200 ms.",
        "The open-floor readout updates under the banner (8 yours, 9 rival).",
        "Body copy is the kit's example line; the rival body mirrors it (written here).",
        needs_code("OpenFloor") + " Banner: host event when the carrier opens a floor.",
    ]
    bd.file("DesignNotes", ("Notes", lambda: sheet_label(1920, "08", "FLOOR OPENED") + notes_card(S, 140, 420, lines), "notes"))
    return bd


# --------------------------------------------------------------------------- boards 6, 7: down, left the building

WASH = ((ui.SILVER, 0.35), (ui.PAPER, 0.55))   # light wash: the view desaturates to a pale silver print
WASH_BACKS = ("sky_bloom", "navy_dark", "office_sun")


def washed(bg, linear):
    for col, a in WASH:
        bg = ui.composite(col, a, bg, linear)
    return bg


def wash_rows():
    """[(pair, backdrop, engine, svg)]: worst pixel of each backdrop under the wash. Navy straight on the wash, navy
    and ink-2 on a white plate over it (all text on the down / left-building screens sits on plates)."""
    rows = []
    for kind in WASH_BACKS:
        for pair, plate, txt in (("navy on the wash", False, ui.NAVY), ("navy on plate over wash", True, ui.NAVY),
                                 ("ink-2 on plate over wash", True, ui.INK2)):
            r = []
            for lin in (True, False):
                worst = None
                for bg in ui.PROOF_BG[kind]:
                    s = washed(bg, lin)
                    if plate:
                        s = ui.composite(ui.PAPER, ui.PLATE_A if lin else ui.PLATE_SVG, s, lin)
                    tc = ui.composite(txt[0], txt[1], s, False) if isinstance(txt, tuple) else txt
                    v = ui.contrast(tc, s)
                    worst = v if worst is None else min(worst, v)
                r.append(worst)
            rows.append((pair, kind, r[0], r[1]))
    return rows


def wash_note():
    rows = {(p, k): (e, s) for p, k, e, s in wash_rows()}
    return ("Contrast, worst pixel, engine / SVG: navy on plate over the washed sky bloom "
            "%.1f / %.1f, washed navy scene %.1f / %.1f; navy straight on the wash %.1f / %.1f and %.1f / %.1f "
            "(unused: every line sits on a plate)." % (
                *rows[("navy on plate over wash", "sky_bloom")], *rows[("navy on plate over wash", "navy_dark")],
                *rows[("navy on the wash", "sky_bloom")], *rows[("navy on the wash", "navy_dark")]))


def wash_back(kind):
    """The view under the light wash and a paper dot screen (ref 01 halftone): it reads as a faded print."""
    b = ui.backdrop(kind) + "".join(ui.rect(0, 0, 1920, 1080, (c, a)) for c, a in WASH)
    with ui._quiet():
        b += ui.rect(0, 0, 1920, 1080, "url(#dotScreen)", opacity=0.7)
    return b


def board_c06():
    bd = Board("c06_down", "c06", "DOWN")
    cfg = HUD_CFG["office_sun"]
    bd.file("Backdrop", ("Backdrop", lambda: wash_back("office_sun"), "ground"))
    for fname, items in hud_files(cfg, top_only=True):
        bd.file(fname, *items)
    cx0, cy0, cw, ch = 960 - 280, 290, 560, 452

    def card():
        kind, ink, ink2, _ = ui._hud_look()
        b = ui.glass_pane(cx0, cy0, cw, ch, kind, name="DownCard")
        b += ui.rect(cx0, cy0, cw, 2, ui.COBALT)
        b += ui.brackets(cx0, cy0, cw, ch, 16, 2, ui.NAVY, gap=8)
        b += ui.square_bullet(cx0 + 32, cy0 + 30, 8, ui.BLACK)
        b += ui.text(cx0 + 48, cy0 + 40, "DOWN", "hud", ink2, size=16, ls=3)
        b += ui.text_right(cx0 + cw - 32, cy0 + 40, "RESPAWN 5 S", "hud", ink2, size=16, ls=3)
        b += ctext(960, cy0 + 96, "Back on the floor in 4", "item", ink, size=36)
        b += ctext(960, cy0 + 312, "4", "floor", ink, size=220, ls=0)
        b += countdown_bar(960, cy0 + 344, 4)
        s = "Knocked out by jonno"
        w = tw(s, "item")
        x = 960 - (w + 26) / 2
        b += ui.line(cx0 + 32, cy0 + 374.5, cx0 + cw - 32, cy0 + 374.5, ui.HAIR)
        b += ui.triangle(x + 8, cy0 + 410, 8, ui.AMBER, ui.NAVY) + ui.text(x + 26, cy0 + 417, s, "item", ink)
        return b
    bd.file("DownCountdown", ("DownCard", card))
    bd.file("Subtitle", ("Subtitle", lambda: subtitle(["An employee is unavailable.", "Returning in 4 seconds."],
                                                      bottom=1080 - 160)))
    lines = [
        "The view goes pale under a silver 35 % + paper 55 % wash and a paper dot screen (no navy wash): the game "
        "keeps running but reads as a faded print. No crosshair, prompt or hands slot while down; the top HUD stays.",
        wash_note(),
        "Countdown {s} reads the host's respawn timer (5 s today): line, UltraLight numeral and five cobalt segments, "
        "all on one white plate with snapped corner brackets.",
        "\"Knocked out by jonno\" is new copy (written here); it needs the host to send who knocked you out.",
        "Speaker label \"Reception\" is new copy. Subtitle: 30 px, 2 lines, 42 characters max, bottom 160 px up.",
        needs_code("Clock", "BriefcaseTracker", "OpenFloor") + " Down state, respawn timer, secretary lines.",
    ]
    bd.file("DesignNotes", ("Notes", lambda: sheet_label(1920, "06", "DOWN") + notes_card(S, 140, 420, lines), "notes"))
    return bd


def board_c07():
    bd = Board("c07_left_building", "c07", "LEFT THE BUILDING")
    cfg = HUD_CFG["sky_bloom"]
    bd.file("Backdrop", ("Backdrop", lambda: wash_back("sky_bloom"), "ground"))
    for fname, items in hud_files(cfg, top_only=True):
        bd.file(fname, *items)
    by, bh = 360, 290

    def band():
        kind = ui._hud_look()[0]
        b = ui.glass_pane(0, by, 1920, bh, kind, name="Band")
        b += ui.rect(0, by, 1920, 2, ui.COBALT) + ui.line(0, by + bh - 0.5, 1920, by + bh - 0.5, ui.HAIR_STRONG)
        return b

    def words():
        _, ink, ink2, _ = ui._hud_look()
        head = "LEFT THE BUILDING"
        hw = tw(head, "heading", size=64, ls=6)
        b = ctext(960, by + 110, head, "heading", ink, size=64, ls=6)
        hx0, hx1 = 960 - hw / 2, 960 + hw / 2
        b += ui.chevron_row(hx0 - 40 - 28, by + 88, 3, "right", s=8, gap=14, color=ui.NAVY, name="ChevronsIn-L")
        b += ui.chevron_row(hx1 + 40 + 28, by + 88, 3, "left", s=8, gap=14, color=ui.NAVY, name="ChevronsIn-R")
        b += ctext(960, by + 176, "Returning to floor 7 in your tower", "item", ink, size=28)
        b += countdown_bar(960, by + 216, 4)
        b += ctext(960, by + 260, "4", "hud", ink, size=24)
        return b
    bd.file("Band", ("BandGlass", band, "ground"), ("BandWords", words))
    bd.file("Subtitle", ("Subtitle", lambda: subtitle(["An employee has left the building.", "Please return to your desk."],
                                                      bottom=1080 - 160)))
    lines = [
        "Return line \"Returning to floor 7 in your tower\" is new copy (checkpoints are dead). Open question: do you "
        "return to the open floor (7 here) or to the floor you fell from?",
        "Secretary line adapted: \"…Please return to your last checkpoint.\" became \"Please return to your desk.\" "
        "Flagged for the owner.",
        "Countdown is new: the inventory has none for a window exit; it reuses the 5 s respawn.",
        "Transition, so it may cover the frame (art bible 14): the sky goes pale under the same wash and dot screen as "
        "Down, and a full-width white plate band carries navy type; chevron rows point in at the title (ref 07).",
        wash_note(),
        needs_code("Clock", "BriefcaseTracker", "OpenFloor") + " Window-exit detection (host), respawn point rule.",
    ]
    bd.file("DesignNotes", ("Notes", lambda: sheet_label(1920, "07", "LEFT THE BUILDING") + notes_card(S, 672, 620, lines, 13), "notes"))
    return bd


# --------------------------------------------------------------------------- board 9: in-game menu

PANE = (S, S, 520, 1080 - 2 * S)


def quiet_back(kind="office_sun"):
    """The view as a pane's `under` (blurred inside frosted glass): painted world, so the dark scan skips it."""
    with ui._quiet():
        return ui.group(ui.uid("Backdrop-under-"), ui._BACKDROPS[kind]())


def menu_pane(focus="Back to game"):
    x, y, w, h = PANE
    b = ui.glass_pane(x, y, w, h, "frosted", streak=True, streak_at=0.7, under=quiet_back(), name="MenuGlass")
    b += ui.rect(x, y, w, 6, ui.COBALT)
    b += ui.text(x + 40, y + 90, "MENU", "heading", ui.NAVY)
    b += ui.text(x + 40, y + 128, "The game continues while this is open.", "body", ui.INK2)
    b += ui.rect(x + 40, y + 158, 10, 10, ui.COBALT)
    b += ui.text(x + 60, y + 168, "Game running · 11:42", "body", ui.NAVY)
    b += ui.line(x + 40, y + 196.5, x + w - 40, y + 196.5, ui.HAIR)
    by = y + 236
    bw = w - 80
    for k, (lab, var) in enumerate((("Back to game", "primary"), ("Invite friends", "secondary"),
                                    ("Settings", "secondary"), ("Leave game", "secondary"))):
        st = "focus" if lab == focus else "default"
        b += ui.button(x + 40, by + k * 88, bw, lab, var, st)
        if st == "focus":   # ref 07 chevrons point in at the focused item (decorative: the ring carries focus)
            hh = ui.SIZE["button_primary_h"] if var == "primary" else ui.SIZE["button_secondary_h"]
            b += ui.chevron_row(x + 9, by + k * 88 + hh / 2, 3, "right", s=5, gap=8, color=ui.NAVY,
                                name=ui.uid("FocusChevrons"))
    b += ui.line(x + 40, y + h - 108.5, x + w - 40, y + h - 108.5, ui.HAIR)
    cap, kw_ = ui.keycap(x + 40, y + h - 84, "Esc", False, 16, 32)
    b += cap + ui.text(x + 40 + kw_ + 10, y + h - 62, "Back to game", "label", ui.INK2)
    b += ui.text(x + 40, y + h - 28, "Terms and Conditions Apply. CTD-0117-B · Clause 6.1, Leaving", "micro", ui.INK3)
    return b


def menu_world(bd):
    bd.file("Backdrop", ("Backdrop", lambda: ui.backdrop("office_sun"), "ground"),
            ("Veil", lambda: ui.rect(0, 0, 1920, 1080, (ui.PAPER, 0.18)), "ground"))
    cfg = HUD_CFG["office_sun"]
    trk_w = tracker_w("rival", "rival", 6)
    cw = chip_w("11:42")
    bd.file("LiveWorld",
            ("BriefcaseMarker", lambda: ui.briefcase_marker(1400, 330, "rival", "36 m")),
            ("RivalPlayer", lambda: ui.team_marker(1640, 470, "rival", 10)),
            ("BriefcaseTracker", lambda: ui.briefcase_tracker(960 - trk_w / 2, S, "rival", "rival", 6, w=trk_w)),
            ("Clock", lambda: ui.hud_chip(960 - cw / 2, 124, "11:42")),
            ("HandsSlot", lambda: ui.item_slot(1920 - S - 260, 1080 - S - 72, "pistol", "held", "7 / 48")))
    return cfg


def board_c09():
    bd = Board("c09_menu", "c09", "IN-GAME MENU")
    menu_world(bd)
    bd.file("MenuPane", ("MenuPane", menu_pane))
    lines = [
        "Not paused: the world keeps running behind an 18 % paper veil (no dark dim, no blur outside the pane). The "
        "rival briefcase marker, a rival player, the tracker, clock (11:42) and hands slot stay live on their plates; "
        "the pane's own line says so.",
        "520 px frosted pane anchored left inside the safe margin; navy text only. Focus starts on \"Back to game\": "
        "focus ring + snapped brackets, and a chevron row pointing in at it (ref 07, decorative).",
        "Esc opens and closes; no animation blocks input (in 150 ms, out 120 ms).",
        "\"Game running · 11:42\" is new copy (written here). Host and client see the same menu; the host's Leave "
        "opens the end-for-everyone confirm (next board).",
        "Needs new code: menu clock line (match clock); everything else exists in MENU_AND_HUD today.",
    ]
    bd.file("DesignNotes", ("Notes", lambda: sheet_label(1920, "09", "IN-GAME MENU") + notes_card(1336, 520, 536, lines), "notes"))
    return bd


def board_c09b():
    bd = Board("c09b_menu_leave", "c09b", "LEAVE CONFIRM")
    menu_world(bd)
    bd.file("MenuPane", ("MenuPane", lambda: menu_pane(focus=None)))
    title, body = "End the game for everyone?", "You are the host. Leaving sends all 6 players back to the title."
    conf, safe = "End game for everyone", "Stay"
    cwid = int(tw(conf) + 48)
    w = int(max(tw(body, "body") + 64, 32 + 120 + 24 + cwid + 24 + 150 + 32))
    x = 960 - w / 2 + 120
    bd.file("Confirm", ("Confirm", lambda: ui.modal_confirm(x, 438, w, title, body, conf, safe, focus="cancel",
                                                             under=quiet_back(), cancel_w=150,
                                                             confirm_w=cwid)))
    lines = [
        "Host only. \"Stay\" is the primary button and holds focus; Esc = Stay. \"End game for everyone\" is secondary "
        "and never focused by default.",
        "{count} = 6 here (3 v 3). A client's Leave shows \"Leave Dana's game?\" / \"Your team carries on without you.\"",
        "The game still runs behind the confirm (markers and clock live).",
        "Needs new code: host-leave ends the session for all (no host migration).",
    ]
    bd.file("DesignNotes", ("Notes", lambda: sheet_label(1920, "09b", "LEAVE CONFIRM") + notes_card(1336, 700, 536, lines), "notes"))
    return bd


# --------------------------------------------------------------------------- board 10: results

ROOF_ROWS = [  # name, firm, carried, floors opened, knockouts
    (HOST, "your", "4:12", 4, 7), (YOU, "your", "2:41", 2, 4), ("mkovac", "your", "0:38", 1, 2),
    ("jonno", "rival", "3:05", 3, 6), ("sasha_k", "rival", "0:51", 1, 3), ("tealeaf", "rival", "0:00", 0, 5)]
CAP_ROWS = [
    ("jonno", "rival", "5:34", 3, 8), ("sasha_k", "rival", "2:20", 1, 4), ("tealeaf", "rival", "1:47", 1, 6),
    (HOST, "your", "2:58", 1, 5), (YOU, "your", "1:15", 1, 3), ("mkovac", "your", "0:22", 0, 2)]
L, R = 160, 1760


def results_board(cap):
    key = "c10b_results_cap" if cap else "c10_results_roof"
    bd = Board(key, key[:4] if cap else "c10", "RESULTS · CAP" if cap else "RESULTS · ROOF")
    rows = CAP_ROWS if cap else ROOF_ROWS
    head = "QUARTER CLOSED" if cap else "DEAL CLOSED"
    cause = "Rival firm finished highest, on floor 7." if cap else "Your firm reached the roof at 13:08."
    verdict = "Your team lost." if cap else "Your team won."
    sec = ("“The deal has gone to another firm. Please clear your desk.”" if cap else
           "“Congratulations. Your quarter has closed ahead of target.”")
    big, big_label = ("15:00", "QUARTER CAP REACHED") if cap else ("13:08", "TIME TO THE ROOF")

    def frame():
        return ui.rect(0, 0, 1920, 1080, ui.PAPER) + ui.hairline_grid(0, 0, 1920, 1080, faint=True, name="Grid") + \
            ui.crop_marks(32, 32, 1856, 1016, name="ScreenCrop")
    bd.file("Frame", ("Frame", frame, "ground"))

    def title():
        b = ui.microtype_strip(L, 92, "CTD-RESULTS · MINUTES OF THE MEETING · Terms and Conditions Apply.")
        b += ui.text(L - 4, 196, head, "heading", ui.NAVY, size=88, ls=6)
        b += ui.text(L, 250, cause, "item", ui.NAVY, size=28)
        b += ui.text(L, 290, "Reception: " + sec, "body", ui.INK2)
        return b

    def block():
        bx, by, bw, bh = 1240, 100, 520, 196
        b = ui.cobalt_block(bx, by, bw, bh, name="ResultBlock")
        b += ui.text(bx + 32, by + 88, verdict, "heading", ui.LIGHT, size=40, ls=1)
        if cap:
            b += ui.triangle(bx + 40, by + 145, 9, ui.AMBER, ui.NAVY, 2)
            b += ui.text(bx + 60, by + 152, "Rival firm closed the quarter", "item", ui.LIGHT)
        else:
            b += ui.diamond(bx + 40, by + 145, 9, ui.CYAN, ui.NAVY, 2)
            b += ui.text(bx + 60, by + 152, "Your firm closed the deal", "item", ui.LIGHT)
        return b

    def numeral():
        b = ui.text(L - 8, 520, big, "floor", ui.NAVY, size=200, ls=-4)
        b += ui.square_bullet(L, 548, 8, ui.BLACK) + ui.text(L + 18, 557, big_label, "caption", ui.NAVY, ls=3)
        return b

    def ladder():
        fl = 7 if cap else 11
        b = ""
        lx = 1160
        for i, nm in enumerate(FLOOR_NAMES):   # the open_floor ladder's language: cobalt below, the end floor cyan
            sx = lx + i * 50
            if i < fl:
                b += ui.rect(sx, 470, 40, 40, ui.COBALT)
            elif i == fl:
                b += ui.rect(sx + 1, 471, 38, 38, ui.CYAN, ui.NAVY, 2)
            else:
                b += ui.rect(sx + 0.5, 470.5, 39, 39, None, ui.HAIR_STRONG, 1)
            b += ui.text(sx + 6, 554, nm, "caption", ui.INK2, ls=0)
        b += ui.trace_brackets(lx, 470, len(FLOOR_NAMES) * 50 - 10, 40, ui.NAVY, seed=11, density=0.35)
        b += ui.text(lx, 426, f"Open floor at the end: {'ROOF' if fl == 11 else fl}", "label", ui.NAVY)
        return b

    def sky_strip():   # the guide's devices, once each: a sky window strip (ref 08) ...
        return sky_band(1240, 318, 520, 80, cols=5, seed=7 if cap else 3)

    def code():        # ... and a printed barcode over the result block (ref 01)
        return ui.barcode(1240, 64, 260, 20, seed=5, name="ResultBarcode")

    cols = [("Player", 0), ("Firm", 300), ("Carried the case", 640), ("Floors opened", 980), ("Knockouts", 1280)]

    def table():
        ty = 604
        b = ""
        for name, dx in cols:
            b += ui.text(L + dx, ty, name.upper(), "caption", ui.INK2, ls=2)
        b += ui.line(L, ty + 12.5, R, ty + 12.5, ui.NAVY)
        for k, (nm, firm, carried, opened, kos) in enumerate(rows):
            ry = ty + 16 + k * 44
            me = YOU if cap else HOST          # the roof board is the host's view, the cap board a client's
            if nm == me:
                b += ui.rect(L, ry, R - L, 44, (ui.NAVY, 0.06)) + ui.rect(L, ry, 4, 44, ui.COBALT)
                b += ui.chevron_row(L - 44, ry + 22, 3, "right", s=6, gap=10, color=ui.NAVY, name="YouChevrons")
            lab = nm + ("  (you)" if nm == me else "") + ("  · host" if nm == HOST else "")
            b += ui.text(L + 16, ry + 30, lab, "item", ui.NAVY, size=22)
            if firm == "your":
                b += ui.diamond(L + 310, ry + 22, 8, ui.CYAN, ui.NAVY, 1.5)
            else:
                b += ui.triangle(L + 310, ry + 23, 8, ui.AMBER, ui.NAVY, 1.5)
            b += ui.text(L + 328, ry + 30, "Your firm" if firm == "your" else "Rival firm", "item", ui.NAVY, size=22)
            b += ui.text(L + 640, ry + 30, carried, "hud", ui.NAVY, size=22)
            b += ui.text(L + 980, ry + 30, str(opened), "hud", ui.NAVY, size=22)
            b += ui.text(L + 1280, ry + 30, str(kos), "hud", ui.NAVY, size=22)
            b += ui.line(L, ry + 43.5, R, ry + 43.5, ui.HAIR)
        return b

    def actions():
        by = 948
        if cap:
            b = ui.status_line(L, by + 38, f"Waiting for {HOST} to start the rematch.", "busy")
            b += ui.button(R - 300, by, 300, "Ready for rematch", "primary", "focus")
            b += ui.button(R - 300 - 24 - 220, by + 4, 220, "Leave game", "secondary")
        else:
            b = ui.status_line(L, by + 38, "3 of 6 ready for rematch.", "ok")
            b += ui.button(R - 240, by, 240, "Rematch", "primary", "focus")
            b += ui.button(R - 240 - 24 - 260, by + 4, 260, "Return to reception", "secondary")
        return b

    bd.file("Title", ("Title", title))
    bd.file("ResultBlock", ("ResultBlock", block))
    bd.file("Devices", ("SkyStrip", sky_strip), ("Barcode", code))
    bd.file("Numerals", ("BigNumeral", numeral), ("Ladder", ladder))
    bd.file("Table", ("Table", table))
    bd.file("Actions", ("Actions", actions))
    sums = (sum(r[3] for r in rows), sum(int(r[2].split(":")[0]) * 60 + int(r[2].split(":")[1]) for r in rows))
    lines = [
        ("Client view: \"Ready for rematch\" holds focus; the host starts it. " if cap else
         "Host view: \"Rematch\" holds focus; clients see \"Ready for rematch\" · \"Leave game\" (cap board). ") +
        "Any key skips the reveal to this end state.",
        "Needs new code: every stat (carried time, floors opened, knockouts), the result, the rematch vote, and the "
        "end-of-game floor ladder. Only names and firms could come from the lobby.",
        f"Table checks: floors opened sum to {sums[0]} ({'open floor 7' if cap else 'floor 1 to the roof'}); carried time "
        f"{sums[1] // 60}:{sums[1] % 60:02d} of {big}.",
        ("Open question: at the cap, \"finished highest\" means the open floor when time runs out (one shared "
         "briefcase); the tie rule is still open (inventory 7 Q6)." if cap else
         "\"3 of 6 ready for rematch.\" is new copy (written here)."),
        "Table 1600 px wide (ux_research 8). White field; big numerals UltraLight; one cobalt block (paper text "
        f"{ui.contrast(ui.PAPER, ui.COBALT):.1f}:1); the floor ladder in the HUD's colours (cobalt below, the end floor "
        "cyan, navy keyline); one trace on it, clear of all text. Guide devices once each: sky window strip, barcode, "
        "chevrons at your row. \"DEAL CLOSED\" / \"closed the deal\" wait on the owner's seal-wording call.",
    ]
    bd.file("DesignNotes", ("Notes", lambda: sheet_label(1920, key[1:].split("_")[0], "RESULTS") + notes_card(668, 312, 476, lines, 12), "notes"))
    return bd


# --------------------------------------------------------------------------- board 11: 720p and 21:9

def scaled_hud(transform, cross_at):
    """Board 2's HUD (no crosshair) as one transformed group + the crosshair at 1:1 at cross_at."""
    with ui._quiet():
        body = ""
        for fname, items in hud_files(HUD_CFG["office_sun"], cross=False):
            body += ui.group(fname, "".join(ui.group(n, fn()) for n, fn in items))
        return ui.group("Hud", body, transform=transform), ui.crosshair(*cross_at)


HUD_MIN_SCALE = 0.85   # Interface size floor: never smaller than this, whatever the resolution


def reflowed(fn, s, src, dst):
    """Draw fn (1080p coordinates) scaled by s about its anchor: point src maps to dst. The registry entries it made
    are replaced by their real transformed boxes (text sizes x s), so the board checks the 720p layout itself."""
    def draw():
        k = ui.mark()
        markup = fn()
        shapes, texts, panes = (list(v) for v in ui.since(k))
        for bucket, n in zip(("shapes", "texts", "panes"), k):
            del ui.REC[bucket][n:]
        tx, ty = dst[0] - src[0] * s, dst[1] - src[1] * s

        def tb(b):
            return (b[0] * s + tx, b[1] * s + ty, b[2] * s + tx, b[3] * s + ty)
        for sh in shapes:
            ui.REC["shapes"].append(dict(sh, box=tb(sh["box"])))
        for t in texts:
            ui.REC["texts"].append(dict(t, box=tb(t["box"]), size=t["size"] * s, design=t["size"]))
        for p in panes:
            ui.REC["panes"].append(dict(p, box=tb(p["box"])))
        return ui.group(ui.uid("Reflow"), markup, transform=f"translate({tx:.2f} {ty:.2f}) scale({s})")
    return draw


def hud_720_files(cfg, s=HUD_MIN_SCALE, W=1280, H=720, m=32):
    """Board 2's HUD re-flowed for W x H at Interface size s: corner blocks keep their corner (m px margin), the top
    centre stack keeps the top centre, the prompt keeps its offset under the crosshair (x s), world markers move with
    the world (W / 1920) but keep the HUD size s. The crosshair is not here: it stays 1:1."""
    k = W / 1920
    anchors = {  # item -> (1080p anchor, 720p anchor)
        "OpenFloor": ((S, S), (m, m)),
        "BriefcaseTracker": ((960, S), (W / 2, m)), "Clock": ((960, S), (W / 2, m)),
        "ClockWarning": ((960, S), (W / 2, m)),
        "HandsSlot": ((1920 - S, 1080 - S), (W - m, H - m)), "Health": ((S, 1080 - S), (m, H - m)),
        "Prompt": ((960, 540), (W / 2, H / 2)),
    }
    kind, mx, my, _ = cfg["marker"]
    world = {"Teammate": (cfg["mate"][1], cfg["mate"][2]), "BriefcaseMarker": (mx, my), "RivalPlayer": (mx, my)}
    out = []
    for fname, items in hud_files(cfg, cross=False):
        row = []
        for name, fn in items:
            if name in anchors:
                src, dst = anchors[name]
            else:
                src = world[name]
                dst = (src[0] * k, src[1] * k)
            row.append((name, reflowed(fn, s, src, dst)))
        out.append((fname, row))
    return out


def board_c11():
    s = HUD_MIN_SCALE
    bd = Board("c11_720p", "c11", "1280 × 720", w=1280, h=720, edge=32, centre=True, min_px=round(16 * s, 2))
    bd.file("Backdrop", ("Backdrop", lambda: ui.backdrop("office_sun", 0, 0, 1280, 720), "ground"))
    for fname, items in hud_720_files(HUD_CFG["office_sun"]):
        bd.file(fname, *items)
    bd.file("Crosshair", ("Crosshair", lambda: ui.crosshair(640, 360)))
    rows = {}
    for it in bd.items:
        for t in it["texts"]:
            if "design" in t:
                rows.setdefault((t["design"], round(t["size"], 1)), set()).add(it["name"])
    bd.size_rows = sorted(rows.items())
    bd.noise_pct = (bd.noise(PERSISTENT) / (1280 * 720) * 100, bd.noise(PERSISTENT + CONTEXTUAL) / (1280 * 720) * 100)
    under = [f"{d:g} to {p:g} px ({', '.join(sorted(n))})" for (d, p), n in bd.size_rows if p < 16]
    lines = [
        f"Board 2's HUD at Interface size {s:.2f}, re-flowed for 1280 × 720 (32 px margin): corner blocks keep their "
        "corners, the tracker + clock stack keeps the top centre, the prompt keeps its place under the crosshair, "
        "world markers move with the view. Plain CanvasScaler 0.667 would put 16 px labels at 10.7 px.",
        "Setting: Settings > Video > Interface size: Auto (screen height / 1080, never below "
        f"{s:.2f}: 720p {s:.2f}, 1080p 1.00, 1440p 1.33), then 85 / 100 / 115 % of that, still never below {s:.2f}. "
        f"Every label ≥ {16 * s:.1f} px here (≥ {16 * s * 1.5:.1f} px in 1080p terms).",
        "Still under 16 px at 720p (design px to px on screen): " + "; ".join(under) + ".",
        f"Noise budget: persistent HUD covers {bd.noise(PERSISTENT) / (1280 * 720) * 100:.1f} % of the 720p frame "
        "(1080p: 3.7 %; research target 4 %): the cost of the legibility floor. The centre 40 % x 40 % still holds only the "
        "crosshair and the prompt. The crosshair is not scaled: physical pixels, 1:1 (ux_research 3, 8).",
    ]
    bd.file("DesignNotes", ("Notes", lambda: notes_card(24, 300, 520, lines, 12) +
                            ui.rect(0, 0, 1280, 26, ui.PAPER) +
                            ui.text(12, 18, f"{ui.GAME_TITLE} · HUD · SURVEY · 11  1280 × 720 · INTERFACE SIZE {s:.2f}",
                                    "caption", ui.NAVY, size=11, ls=1.5) + crosshair_strip(), "notes"))
    return bd


def crosshair_strip(x=1280 - 24 - 4 * 52, y=200):
    """Notes-only proof strip: the 1:1 crosshair over paper, cobalt, the navy scene and sky bloom at 720p."""
    b = ""
    for i, (nm, bg, scene) in enumerate(XROWS):
        cx, cy = x + i * 52, y
        if scene:
            kind, (sx, sy) = scene
            b += ui.backdrop(kind, cx, cy, 48, 48, crop=(sx - 24, sy - 24, 48, 48), name=ui.uid("ScenePatch"))
        else:
            b += ui.rect(cx, cy, 48, 48, bg, ui.HAIR if bg == ui.PAPER else None)
        b += ui.crosshair(cx + 24, cy + 24)
    return ui.group("CrosshairProof", b)


def board_c11b():
    bd = Board("c11b_ultrawide", "c11b", "2560 × 1080", w=2560, h=1080)

    def back():
        b = ui.backdrop("office_sun", 320, 0, 1920, 1080, name="BackCentre")
        b += ui.backdrop("office_sun", 0, 0, 320, 1080, crop=(160, 0, 320, 1080), name="BackLeft")
        b += ui.backdrop("office_sun", 2240, 0, 320, 1080, crop=(0, 0, 320, 1080), name="BackRight")
        return b
    bd.file("Backdrop", ("Backdrop", back, "ground"))
    hud, cross = scaled_hud("translate(320 0)", (1280, 540))
    bd.file("HudCentre", ("HudCentre", lambda: hud, "scaled"))
    bd.file("Crosshair", ("Crosshair", lambda: cross, "scaled"))

    def bounds():
        return ui.rect(320, 0, 1920, 1080, None, (ui.COBALT, 0.9), 2, dash="12 8") + \
            ui.rect(320 + S, S, 1920 - 2 * S, 1080 - 2 * S, None, (ui.NAVY, 0.5), 1, dash="4 4")
    lines = [
        "2560 × 1080 (sold as 21:9): Hor+ shows more city at the sides; the sides are extra view, not stretch.",
        "HUD bounds default to the centre 16:9 (dashed cobalt): corner blocks sit 48 px inside it (dashed navy), "
        "not at the screen edge (ux_research 8).",
        "Proposed setting: HUD bounds 16:9 / 21:9 / full.",
        "Layout checks run on the 1080p build (board 2).",
    ]
    bd.file("DesignNotes", ("Notes", lambda: bounds() + sheet_label(2560, "11b", "2560 × 1080 · HUD IN CENTRE 16:9") +
                            notes_card(24, 120, 280, lines, 12), "notes"))
    return bd


# --------------------------------------------------------------------------- main

def write_board(bd):
    folder = os.path.join(OUT, bd.key)
    paths, order = [], [f for f in bd.files if f[0] != "DesignNotes"] + [f for f in bd.files if f[0] == "DesignNotes"]
    for n, (name, markup) in enumerate(order):
        num = 99 if name == "DesignNotes" else n
        paths.append(ui.write(os.path.join(folder, f"{bd.code}_{num:02d}_{name}.svg"), markup, bd.w, bd.h))
    return paths


def scan_outputs(paths):
    errs = []
    for p in paths:
        with open(p, encoding="utf-8") as fh:
            src = fh.read()
        if re.search(r"<circle|<ellipse|\brx=", src):
            errs.append(f"rounded primitive in {os.path.basename(p)}")
        for fam in set(re.findall(r'font-family="([^"]+)"', src)) - FAMILIES:
            errs.append(f"font family '{fam}' in {os.path.basename(p)}")
    return errs


def main():
    preview = "--no-preview" not in sys.argv
    if os.path.isdir(OUT):
        shutil.rmtree(OUT)
    for p in glob.glob(os.path.join(PREVIEW, "hud_*.png")):
        os.remove(p)
    failed = False
    with open(os.path.abspath(__file__), encoding="utf-8") as fh:
        if re.search(r"#[0-9A-Fa-f]{6}\b", fh.read()):
            print("CHECK FAIL hex literal in make_hud.py")
            failed = True
    builders = [board_c01, lambda: hud_board(2, "office_sun", "HUD · OFFICE SUN"),
                lambda: hud_board(3, "sky_bloom", "HUD · SKY BLOOM"), lambda: hud_board(4, "navy_dark", "HUD · NAVY DARK"),
                board_c05, board_c06, board_c07, board_c08, board_c09, board_c09b,
                lambda: results_board(False), lambda: results_board(True), board_c11, board_c11b]
    for build in builders:
        bd = build()
        paths = write_board(bd)
        errs = bd.check() + scan_outputs(paths)
        print(f"\n{bd.key}: {len(paths)} files ({bd.w} × {bd.h})")
        for p in paths:
            print("  wrote", os.path.relpath(p))
        if getattr(bd, "noise_pct", None):
            print("  noise budget: persistent %.2f %%, with contextual %.2f %%" % bd.noise_pct)
        for (d, p), names in getattr(bd, "size_rows", []):
            print(f"  text {d:g} px → {p:g} px at 720p{'  (under 16)' if p < 16 else ''}: {', '.join(sorted(names))}")
        print(f"  traces: {getattr(bd, 'n_trace', 0)}")
        for e in errs:
            print("  CHECK FAIL", e)
        failed |= bool(errs)
        if not errs:
            print("  checks pass")
        if preview:
            png = ui.preview_many(paths, os.path.join(PREVIEW, f"hud_{bd.key}.png"))
            clean = ui.preview_many(paths[:-1], os.path.join(PREVIEW, f"hud_{bd.key}_clean.png"))
            print("  preview", os.path.relpath(png), "+", os.path.basename(clean))
    if ui.MISSING_GLYPHS:
        failed = True
        print("\nMISSING GLYPHS:", sorted(ui.MISSING_GLYPHS))
    print(f"\nText metrics: {ui.METRICS_SOURCE}.")
    print("\nContrast under the down / left-building wash (worst pixel; engine / SVG):")
    for pair, kind, e, s in wash_rows():
        print(f"  {pair:<26} {kind:<11} {e:5.2f} / {s:5.2f}  {'ok' if min(e, s) >= 4.5 else 'FAIL'}")
        failed |= min(e, s) < 4.5 and "plate" in pair
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
