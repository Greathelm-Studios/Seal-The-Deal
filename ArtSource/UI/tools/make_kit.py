"""Seal the Deal: generate the UI kit as three 1920 x 1080 artboards, one SVG per component group.

    python3 ArtSource/UI/tools/make_kit.py              # from the repo root
    python3 ArtSource/UI/tools/make_kit.py --no-preview # skip the Quick Look PNGs

Writes ArtSource/UI/svg/kit/<board>/<board>_NN_Name.svg. Every file has the full 1920 x 1080 viewBox (place
with crop:"crop" in VectorCraft) and NN is the bottom-to-top place order. The last file of each board,
<board>_99_DesignNotes.svg, is annotation only, kept apart from the clean art.
  a1_foundations  title lockup, palette, type scale, motifs, backdrops
  a2_controls     every interactive control in five states at 1:1, settings rows, confirm, status
  a3_glass_hud    glass proofs (the theme's PROOF_KINDS x 3 backdrops, engine contrast), a window pane, the HUD

Every label that names a face, a glass kind or an alpha is read from the theme's tokens (TYPE, TARGET_FACE,
GLASS, PROOF_KINDS, PALETTE), and every sample cell is a token colour or a painted backdrop, so passes B, C and D
run these boards unchanged.

Then it checks every board (text overlaps, component overlaps, text crossing a pane edge, silver text in
a smoked pane's sheen band, anything outside the margin, missing glyphs, one trace per board), prints the
glass contrast table with FAIL for any used pair under its mark, and renders one preview PNG per board to
ArtSource/UI/svg/_preview/. Exit code 1 if any check fails.
"""

from __future__ import annotations

import argparse
import glob
import os
import re
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ctd_ui as ui  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "svg", "kit"))
PREVIEW = os.path.normpath(os.path.join(HERE, "..", "svg", "_preview"))
W, H = 1920, 1080
M, RX = 64, 1856           # content margins
EDGE = 24                  # nothing may come closer to the board edge than this (crop marks excepted)


# --------------------------------------------------------------------------- board + checks

class Board:
    """Collects one artboard: files (bottom to top), each a list of named items drawn at 1:1."""

    def __init__(self, key, title):
        self.key, self.title = key, title
        self.files = []          # [(name, markup)]
        self.traces = []         # boxes of this board's trace squares (ui.TRACE_BOXES while it was drawn)
        self.items = []          # [dict(name, file, box, texts, panes, ground)]
        ui.reset_registry()

    def file(self, name, *items):
        """items: (item_name, fn) or (item_name, fn, "ground"). fn() returns markup."""
        body = ""
        for it in items:
            iname, fn = it[0], it[1]
            ground = len(it) > 2 and it[2] == "ground"
            labels = len(it) > 2 and it[2] == "labels"
            k = ui.mark()
            t0 = len(getattr(ui, "TRACE_BOXES", []))
            markup = fn()
            self.traces += getattr(ui, "TRACE_BOXES", [])[t0:]
            shapes, texts, panes = ui.since(k)
            boxes = [s["box"] for s in shapes]
            box = (min(b[0] for b in boxes), min(b[1] for b in boxes),
                   max(b[2] for b in boxes), max(b[3] for b in boxes)) if boxes else None
            worlds = [sh["box"] for sh in shapes if sh.get("backdrop")]
            self.items.append(dict(name=iname, file=name, box=box, texts=texts, panes=panes, ground=ground, worlds=worlds,
                                   labels=labels))
            body += ui.group(iname, markup)
        self.files.append((name, ui.group(f"{self.key[:2].upper()}-{name}", body)))

    # ---- checks
    def check(self):
        errs = []
        # nested marks (a focus ring, a hover corner) sit inside their item's box; items keep 4 px apart
        solid = [it for it in self.items if not it["ground"] and not it["labels"] and it["box"]]
        for lab in (it for it in self.items if it["labels"]):  # label sheets: each text against each item
            for t in lab["texts"]:
                for it in solid:
                    if _hit(_grow(t["box"], 2), _grow(it["box"], 2)):
                        errs.append(f"label '{t['s']}' overlaps {it['name']} {_r(it['box'])}")
        for i, a in enumerate(solid):
            for b in solid[i + 1:]:
                if _hit(_grow(a["box"], 2), _grow(b["box"], 2)):
                    errs.append(f"items overlap: {a['name']} {_r(a['box'])} / {b['name']} {_r(b['box'])}")
        texts = [(it["name"], t) for it in self.items for t in it["texts"]]
        for i, (na, a) in enumerate(texts):
            for nb, b in texts[i + 1:]:
                ga = (a["box"][0] - 4, a["box"][1] + 0.5, a["box"][2] + 4, a["box"][3] - 0.5)  # 8 px word gap
                if _hit(ga, _grow(b["box"], -0.5)):
                    errs.append(f"text overlap: '{a['s']}' ({na}) / '{b['s']}' ({nb})")
        for it in self.items:
            bx = it["box"]
            if bx and it["name"] != "Frame" and (bx[0] < EDGE or bx[1] < EDGE or bx[2] > W - EDGE or bx[3] > H - EDGE):
                errs.append(f"outside the margin: {it['name']} {_r(bx)}")
        panes = [p for it in self.items for p in it["panes"]]
        for name, t in texts:
            tb = t["box"]
            for p in panes:
                pb = p["box"]
                if _hit(tb, pb) and not _inside(tb, _grow(pb, 0.5)):
                    errs.append(f"text crosses a pane edge: '{t['s']}' ({name})")
            if t["size"] < 16 and _on_world(tb, self.items):
                errs.append(f"text under 16 px over the world: '{t['s']}' ({name})")
        errs += ui.layout_rules(self.items)   # the theme's own rules (pass A: no silver in the sheen band)
        errs += self.file_checks()
        n_trace = sum(markup.count('id="TraceCluster"') for _, markup in self.files)
        if n_trace != 1:
            errs.append(f"{n_trace} trace clusters (want exactly 1)")
        for tb in self.traces:
            for name, t in texts:
                if _hit(_grow(tb, 2), t["box"]):
                    errs.append(f"trace touches text: '{t['s']}' ({name})")
                    break
        return errs


    def file_checks(self):
        """Per-theme markup checks: families, corner radii, round shapes, hex colours."""
        errs, allowed = [], set(ui.allowed_hex())
        for name, markup in self.files:
            fams = set(re.findall(r'font-family="([^"]*)"', markup)) - set(ui.FAMILIES)
            if fams:
                errs.append(f"{name}: families outside the theme's FAMILIES: {sorted(fams)}")
            if not ui.ALLOW_RADIUS and re.search(r' rx="', markup):
                errs.append(f"{name}: rounded corners (rx) but the theme has ALLOW_RADIUS = False")
            if re.search(r"<circle|<ellipse", markup):
                errs.append(f"{name}: <circle> / <ellipse> drawn")
            hexes = {h.upper() for h in re.findall(r"#[0-9A-Fa-f]{6}\b", markup)} - allowed
            if hexes:
                errs.append(f"{name}: hex outside the theme's tokens: {sorted(hexes)}")
        return errs


def _grow(b, d):
    return (b[0] - d, b[1] - d, b[2] + d, b[3] + d)


def _hit(a, b):
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]


def _inside(a, b):
    return a[0] >= b[0] and a[1] >= b[1] and a[2] <= b[2] and a[3] <= b[3]


def _r(b):
    return tuple(round(v) for v in b)


def _on_world(tb, items):
    return any(_hit(tb, w) for it in items for w in it["worlds"])


# --------------------------------------------------------------------------- shared pieces

def faces_line():
    """The board's stand-in faces, read from the theme's TYPE roles (family + weight), in role order."""
    seen = []
    for fam, wt, _, _ in ui.TYPE.values():
        wt = ui.FORCED_WEIGHT.get(fam, wt)
        tag = fam if wt in (400, 500) else f"{fam} {wt}"
        if fam not in [s.split(" 3")[0] for s in seen] and tag not in seen:
            seen.append(tag)
    return ", ".join(seen)


def face_target(role):
    """'stand-in → target' for a TYPE role, from the theme's TYPE and TARGET_FACE."""
    fam, wt, _, _ = ui.TYPE[role]
    wt = ui.FORCED_WEIGHT.get(fam, wt)
    return f"{fam} → " + ui.TARGET_FACE.get(fam, fam)


def frame(code, title, sub):
    b = ui.rect(0, 0, W, H, ui.PAPER)
    b += ui.hairline_grid(0, 0, W, H, faint=True, name="Grid")
    b += ui.crop_marks(16, 16, W - 32, H - 32, name="SheetCrop")
    b += ui.square_bullet(M, 33, 9, ui.BLACK)
    b += ui.text(M + 20, 42, f"{ui.GAME_TITLE} · UI KIT · {ui.PASS_NAME} · {code}  {title}", "caption", ui.NAVY, ls=2)
    b += ui.microtype_strip(M, 1058, f"CTD-UI-{code} · {sub} · Reference 1920 × 1080, drawn 1:1 · Mockup "
                            f"faces {faces_line()} · Terms and Conditions Apply.")
    return b


def section(x, y, w, num, title, note=None):
    """Kit section label: clause number, caps title, keyline. 22 px tall."""
    b = ui.square_bullet(x, y + 3, 8, ui.BLACK)
    b += ui.text(x + 16, y + 12, f"§ {num}  {title}", "caption", ui.NAVY, ls=2)
    if note:
        b += ui.text_right(x + w, y + 12, note, "caption", ui.INK3, ls=0.5)
    b += ui.line(x, y + 20.5, x + w, y + 20.5, ui.NAVY)
    return b


def caption(x, y, s, w=None, fill=None):
    fill = fill or ui.INK3
    if w:
        return ui.text_lines(x, y, s, w, "caption", fill, ls=0.5, leading=1.45)[0]
    return ui.text(x, y, s, "caption", fill, ls=0.5)


def notes_block(x, y, w, lines, size=14, board=None):
    """DesignNotes: cobalt label + numbered notes, wrapped to w. A theme's BOARD_NOTES[board] line (how this
    pass departs from the bible) is added first."""
    extra = ui.BOARD_NOTES.get(board) if board else None
    lines = ([extra] if extra else []) + list(lines)
    b = ui.text(x, y, "DESIGN NOTES", "caption", ui.COBALT, ls=2)
    yy = y + 22
    for i, n in enumerate(lines, 1):
        b += ui.text(x, yy, f"{i}", "caption", ui.COBALT, ls=0)
        md, hgt = ui.text_lines(x + 18, yy, n, w - 18, "label", ui.INK2, size=size, weight=400, leading=1.35)
        b += md
        yy += hgt + 4
    return b


# --------------------------------------------------------------------------- A1 foundations

def board_a1():
    bd = Board("a1_foundations", "FOUNDATIONS")
    bd.file("Frame", ("Frame", lambda: frame("A1", "FOUNDATIONS", "Title, palette, type, motifs, backdrops"), "ground"))

    # § 1 title lockup
    def lockup():
        return section(M, 72, 1060, 1, "TITLE LOCKUP", "display · " + face_target("title")) + \
            ui.title_lockup(M, 124, size=96, name="TitleLockup96")
    bd.file("TitleLockup", ("TitleLockup", lockup))

    # § 2 palette
    def palette():
        b = section(M, 300, RX - M, 2, "PALETTE", "7 bible swatches + 1 proposed warm accent")
        cw = 210
        for i, (tok, hx, nm, role) in enumerate(ui.PALETTE):
            x = M + i * (cw + 16)
            b += ui.rect(x, 340, cw, 72, hx, ui.HAIR if hx == ui.PAPER else None)
            if tok == "CYAN":
                b += ui.diamond(x + cw - 22, 362, 9, ui.CYAN, ui.NAVY, 1.5)
            if tok == "AMBER":
                b += ui.triangle(x + cw - 22, 364, 9, ui.AMBER, ui.NAVY, 1.5)
            b += ui.text(x, 438, nm, "label", ui.NAVY)
            b += ui.text(x, 460, hx, "caption", ui.INK2, ls=0.5)
            role = role + (" (proposal)" if tok == "AMBER" else "")
            b += ui.text_lines(x, 484, role, cw, "label", ui.INK2, weight=400, leading=1.3)[0]
        return b
    bd.file("Palette", ("Palette", palette))

    # § 3 type scale
    def spec(role, note):
        fam, wt, sz, ls = ui.TYPE[role]
        wt = ui.FORCED_WEIGHT.get(fam, wt)
        tr = f" · {'+' if ls > 0 else ''}{ls:g}" if ls else ""
        return f"{role.upper()} · {sz} · {wt}{tr} · {note}"
    rows = [  # role, sample, size, spec line, target (all read from the theme's TYPE / TARGET_FACE)
        ("title", "THE DEAL", None, spec("title", "thin, wide"), face_target("title")),
        ("heading", "SETTINGS", None, spec("heading", "caps"), face_target("heading")),
        ("hud", "12:34   30 / 90", None, spec("hud", "mono, tabular"), face_target("hud")),
        ("item", "Host game", None, spec("item", "buttons, rows"), face_target("item")),
        ("body", "The game continues while this is open.", None, spec("body", "sentences"), face_target("body")),
        ("label", "Mouse sensitivity", None, spec("label", "the minimum"), face_target("label")),
        ("caption", "§ 1.7  KEY BINDINGS", None, spec("caption", "kit only"), face_target("caption")),
        ("micro", "Terms and Conditions Apply. CTD-0117-B", None, spec("micro", "decorative"), face_target("micro")),
    ]

    def type_scale():
        b = section(M, 540, 1060, 3, "TYPE SCALE", "stand-in → target, 1:1 px")
        t = 580
        for role, sample, size, spec_, target in rows:
            sz = size or ui.TYPE[role][2]
            cap = 0.72 * sz
            row_h = max(44, cap + 0.22 * sz + 14)
            b += ui.text(M, t + 14, spec_, "caption", ui.NAVY, ls=0.5)
            b += ui.text(M, t + 32, target, "caption", ui.INK3, ls=0.5)
            b += ui.text(420, t + max(cap, 14), sample, role, ui.NAVY)
            b += ui.line(M, t + row_h - 7.5, 860, t + row_h - 7.5, ui.HAIR)
            t += row_h
        fx = 900
        b += ui.text(fx, 700, spec("floor", "big"), "caption", ui.NAVY, ls=0.5)
        b += caption(fx, 718, face_target("floor"), 220)
        b += ui.text(fx, 1004, "7", "floor", ui.SILVER)
        return b
    bd.file("TypeScale", ("TypeScale", type_scale))

    # § 4 motifs: 4 x 2 cells, every one on paper (the guide: interface feel from lines, not panels)
    cx = [1180 + i * 173 for i in range(4)]
    cw, ch, r1, r2 = 157, 96, 580, 752

    def motifs_head():
        return section(1180, 540, 676, 4, "MOTIFS", "one traced object per screen")

    def cell_caption(i, row, s):
        return caption(cx[i], (r1 if row == 0 else r2) + ch + 26, s, cw)

    def c_grid():
        x, y = cx[0], r1
        b = ui.hairline_grid(x + 10, y + 10, cw - 20, ch - 20, name="GridSwatch")
        b += ui.crop_marks(x + 10, y + 10, cw - 20, ch - 20, name="CropSwatch")
        b += ui.brackets(x + 30, y + 26, cw - 60, ch - 52, 8, 1.5, ui.NAVY)
        return b + cell_caption(0, 0, "Grid 32 · crop marks · brackets")

    def trace_cell():
        x, y = cx[1] + 18, r1 + 14
        b = ui.backdrop("sky_bloom", x, y, 120, 68, crop=(1300, 60, 560, 315), name="SkyWindow")
        b += ui.trace_brackets(x, y, 120, 68, ui.NAVY, seed=4)
        return b + cell_caption(1, 0, "Trace → snapped bracket")

    def c_window():
        b = ui.sky_window(cx[2], r1, cw, ch, cols=3, rows=2, frame=8, seed=6, name="WindowSwatch")
        return b + cell_caption(2, 0, "Sky in a frame: panes")

    def c_chevrons():
        x, y = cx[3], r1
        b = ui.rect(x + 0.5, y + 0.5, cw - 1, ch - 1, None, ui.HAIR, 1)
        b += ui.chevron_row(x + 22, y + 36, 4, "right", s=7, gap=14, name="ChevronsRight")
        b += ui.rect(x + 86, y + 26, 48, 20, None, ui.NAVY, 1)
        b += ui.chevron_row(x + 110, y + 82, 3, "up", s=7, gap=10, name="ChevronsUp")
        return b + cell_caption(3, 0, "Chevrons: focus, way up")

    def c_barcode():
        x, y = cx[0], r2
        b = ui.rect(x + 0.5, y + 0.5, cw - 1, ch - 1, None, ui.HAIR, 1)
        b += ui.barcode(x + 14, y + 14, cw - 28, 30, seed=7, name="BarcodeSwatch")
        b += ui.halftone(x + 14, y + 52, cw - 28, 32, "right", ui.NAVY, cell=6, dmax=4, dmin=0, name="HalftoneSwatch")
        return b + cell_caption(0, 1, "Barcode · dot screen")

    def c_slots():
        x, y = cx[1], r2
        b = ui.outlined_slot(x + 12, y + 18, 60, 44, name="SlotEmptyA")
        b += ui.outlined_slot(x + 84, y + 18, 60, 44, name="SlotEmptyB")
        b += ui.pictogram("briefcase", x + 96, y + 28, 0.9, ui.NAVY)
        return b + cell_caption(1, 1, "Outlined slots")

    def c_team():
        x, y = cx[2], r2
        b = ui.rect(x + 0.5, y + 0.5, cw - 1, ch - 1, None, ui.HAIR, 1)
        b += ui.team_marker(x + 24, y + 32, "you", 10) + ui.text(x + 44, y + 38, "Your firm", "label", ui.NAVY)
        b += ui.team_marker(x + 24, y + 70, "rival", 10) + ui.text(x + 44, y + 76, "Rival firm", "label", ui.NAVY)
        return b + cell_caption(2, 1, "Team marks, navy keyline")

    def c_floor():
        b = ui.floor_numeral(cx[3] + 8, r2 + 74, 7, size=88, label="FLOOR 7", name="FloorNumeralSwatch")
        return b + cell_caption(3, 1, "Floor numeral")

    bd.file("Motifs", ("MotifsHead", motifs_head), ("GridCell", c_grid), ("TraceSwatch", trace_cell),
            ("WindowCell", c_window), ("ChevronCell", c_chevrons), ("BarcodeCell", c_barcode),
            ("SlotCell", c_slots), ("TeamMarkers", c_team), ("FloorNumeral", c_floor))

    # § 5 backdrops
    def backdrops():
        b = section(1180, 72, 676, 5, "BACKDROPS", "painted from ArtSource/Look/review")
        info = [("office_sun", "Office sun", "Pale office, sun slabs, rival tower"),
                ("sky_bloom", "Sky bloom", "Worst case for light text"),
                ("navy_dark", "Navy dark", "Worst case for dark text")]
        for j, (k, nm, note) in enumerate(info):
            x = 1180 + j * 234
            b += ui.backdrop(k, x, 112, 208, 117, name=f"Thumb-{k}")
            b += ui.rect(x + 0.5, 112.5, 207, 116, None, ui.PAPER, 4)
            b += ui.rect(x - 0.5, 111.5, 209, 118, None, ui.HAIR_STRONG, 1)
            b += ui.text(x, 252, nm, "label", ui.NAVY)
            b += caption(x, 272, note, 208)
        return b
    bd.file("Backdrops", ("Backdrops", backdrops))

    # microtype motif lives in its own file so the motif cells stay single-purpose
    def microtype():
        b = ui.microtype_strip(cx[0], r2 + 166, "§ 4.2 Settings take effect on save. Terms and Conditions Apply. "
                               "CTD-0117-B · v0.1.0")
        b += caption(cx[0], r2 + 186, "Legal microtype · 11 px mono · decorative, never needed to act")
        return b
    bd.file("Microtype", ("Microtype", microtype))

    def notes():
        return notes_block(1180, 976, 676, board="a1_foundations", lines=[
            "Amber is a proposal (rival and threat). It is never text: navy on a solid amber plate.",
            "Thin type only for the title and big numerals; every size is 1:1 at 1920 × 1080.",
        ], size=14)
    bd.file("DesignNotes", ("DesignNotes", notes))
    return bd


# --------------------------------------------------------------------------- A2 controls

COLS = [  # header, width, drawer(x, row_top, state)
    ("Primary", 210, lambda x, y, s: ui.button(x, y, 210, "Host game", "primary", s)),
    ("Secondary", 180, lambda x, y, s: ui.button(x, y + 4, 180, "Settings", "secondary", s)),
    ("Quiet", 210, lambda x, y, s: ui.button(x, y + 4, 210, "Quit to desktop", "quiet", s)),
    ("Tab", 130, lambda x, y, s: ui.tab(x, y + 4, 130, "Video", s)),
    ("Toggle", 80, lambda x, y, s: ui.toggle(x + 6, y + 21, True, s)),
    ("Slider + field", 252, lambda x, y, s: ui.slider(x, y + 12, 0.12, 0.02, 0.5, "0.12", s, track=146)),
    ("Dropdown", 180, lambda x, y, s: ui.stepper(x, y + 8, 180, "High", s, "dropdown")),
    ("Stepper", 150, lambda x, y, s: ui.stepper(x, y + 8, 150, "Hold", s, "stepper")),
    ("Key", 64, lambda x, y, s: ui.keybind(x, y + 8, "Q", s, 64)),
]


def board_a2():
    bd = Board("a2_controls", "CONTROLS")
    bd.file("Frame", ("Frame", lambda: frame("A2", "CONTROLS", "Every control in five states, settings rows, confirm"), "ground"))

    # § 1 state matrix: one file per column group so VectorCraft layers stay readable
    xs, x = [], M + 100
    for _, w, _ in COLS:
        xs.append(x)
        x += w + 26

    def matrix_head():
        b = section(M, 64, RX - M, 1, "STATE MATRIX", "default · hover · focus · pressed · disabled, 1:1")
        for (hd, _, _), cx_ in zip(COLS, xs):
            b += ui.text(cx_, 104, hd, "caption", ui.INK2, ls=1)
        for i, st in enumerate(ui.STATES):
            b += ui.text(M, 124 + i * 84 + 37, st.capitalize(), "caption", ui.INK2, size=15, ls=1)
        return b
    bd.file("MatrixLabels", ("MatrixLabels", matrix_head, "labels"))
    items = []
    for (hd, _, draw), cx_ in zip(COLS, xs):
        for i, st in enumerate(ui.STATES):
            items.append((f"{hd}-{st}", (lambda d=draw, xx=cx_, yy=124 + i * 84, s=st: d(xx, yy, s))))
    bd.file("StateMatrix", *items)

    # § 3 settings rows (left)
    fw = 760

    def form():
        b = section(M, 548, fw, 3, "SETTINGS ROWS", "64 px rows · Video tab")
        b += ui.tab_bar(M, 580, fw, ["Controls", "Video", "Audio", "Accessibility"], active=1, hover=2)
        b += ui.settings_row(M, 644, fw, "header", "§ 2  VIDEO")
        b += ui.settings_row(M, 708, fw, "slider", "Field of view", (91, 80, 110, "91"), "focus",
                             desc="Horizontal degrees. 91 matches today's 60° vertical.")
        b += ui.settings_row(M, 808, fw, "toggle", "Vertical sync", True)
        b += ui.settings_row(M, 872, fw, "dropdown", "Display mode", "Borderless window", control_w=260)
        b += ui.settings_row(M, 936, fw, "stepper", "Graphics quality", "High", control_w=200)
        b += ui.microtype_strip(M, 1024, "§ 2.7 Settings take effect on save. Terms and Conditions Apply.")
        return b
    bd.file("SettingsRows", ("SettingsRows", form))

    # § 4 confirm, status, key-bind states (right)
    rx, rw = 870, RX - 870

    def right_head():
        return section(rx, 548, rw, 4, "CONFIRM · STATUS · KEY STATES", "safe choice holds focus")

    def modal():
        b = ui.modal_confirm(rx, 596, 520, "Leave the host's game?", "Your team carries on without you.",
                             "Leave game", "Stay", cancel_w=150)
        b += ui.trace_brackets(rx, 596, 520, 204, ui.NAVY, seed=9, squares=False)
        return b

    def status():
        b = ui.status_line(1414, 640, "Opening a lobby on Steam...", "busy")
        b += ui.status_line(1414, 690, "Connected to the game on this PC.", "ok")
        b += ui.status_line(1414, 740, "Unable to join. The game is full (8 players).", "error")
        b += caption(1414, 776, "busy · ok · error: the mark's shape carries it")
        return b

    def keyrows():
        b = ui.settings_row(rx, 828, 480, "keybind", "Interact", ("F", "Y"), "listening",
                            desc="Press a key for Interact. Esc cancels.")
        return b

    def keyconflict():
        return ui.settings_row(1376, 828, 480, "keybind", "Interact", ("G", "Y"), "conflict",
                               desc="G is already used for Drop.")

    bd.file("ConfirmStatusKeys", ("RightHead", right_head), ("LeaveConfirm", modal), ("StatusLines", status),
            ("KeyListening", keyrows), ("KeyConflict", keyconflict))

    def notes():
        return notes_block(rx, 950, rw, board="a2_controls", lines=[
            "Pressed is a pushed-in paper face with a stamped navy edge (primary: solid cobalt); no dark fill.",
            "Disabled controls are not focusable; the reason always goes in a status line. Esc = the safe choice.",
        ], size=14)
    bd.file("DesignNotes", ("DesignNotes", notes))
    return bd


# --------------------------------------------------------------------------- A3 glass and HUD

PROOF_SAMPLE = {"plate": ("12:34", "hud"), "frosted": ("Back to game", "item"), "amber": ("Rival firm", "item"),
                "smoked": ("12:34", "hud"), "cobalt": ("Host game", "item"), "clear": (None, None)}
PROOF_COLS = [("office_sun", "Office sun", (700, 160, 760, 380)), ("sky_bloom", "Sky bloom", (240, 120, 760, 380)),
              ("navy_dark", "Navy dark", (760, 300, 760, 380))]


def proof_kinds():
    """The glass kinds A3 proves: the theme's PROOF_KINDS token (pass A: plate, frosted, clear, amber), at most four."""
    kinds = [k for k in getattr(ui, "PROOF_KINDS", ("smoked", "cobalt", "frosted")) if k in ui.GLASS]
    return kinds[:4]


def no_text(kind):
    return kind in getattr(ui, "NO_TEXT_KINDS", ())


def board_a3():
    bd = Board("a3_glass_hud", "GLASS AND HUD")
    bd.file("Frame", ("Frame", lambda: frame("A3", "GLASS AND HUD", "Glass proofs and HUD components at 1:1"), "ground"))
    lw = 680

    # § 1 glass proofs: the theme's kinds over the three painted worst cases, engine ratio at the worst pixel
    kinds = proof_kinds()
    pitch = 128

    def proofs_head():
        b = section(M, 64, lw, 1, "GLASS PROOF", "engine contrast, worst pixel")
        for j, (_, nm, _) in enumerate(PROOF_COLS):
            b += ui.text(M + j * 240, 104, nm, "caption", ui.INK2, ls=1)
        for i, kind in enumerate(kinds):
            fill, a, sa, _, tname = ui.GLASS[kind]
            extra = " · a frame: no text on it" if no_text(kind) else f" · {tname} text"
            b += ui.text(M, 130 + i * pitch, f"{kind.capitalize()} · engine {round(a * 100)} % · SVG {round(sa * 100)} %"
                         f"{extra}", "label", ui.NAVY)
        return b
    bd.file("GlassProofLabels", ("GlassProofLabels", proofs_head, "labels"))

    tiles = []
    for i, kind in enumerate(kinds):
        for j, (bk, _, crop) in enumerate(PROOF_COLS):
            def tile(kind=kind, bk=bk, crop=crop, i=i, j=j):
                tx, ty = M + j * 240, 140 + i * pitch
                b = ui.backdrop(bk, tx, ty, 212, 84, crop=crop, name=f"ProofBack-{kind}-{bk}")
                px, py = tx + 26, ty + 18
                sample, role = PROOF_SAMPLE.get(kind, ("Sample", "item"))
                if no_text(kind) or sample is None:
                    b += ui.glass_pane(px, py, 160, 48, kind, mullions_x=(0.5,), name=f"Pane-{kind}-{bk}")
                    tag = "frame · no text"
                else:
                    b += ui.glass_pane(px, py, 160, 48, kind, name=f"Pane-{kind}-{bk}")
                    txt = ui.GLASS[kind][3]
                    b += ui.text(px + 14, py + (34 if role == "hud" else 31), sample, role, txt)
                    r = ui.glass_ratio(kind, bk)
                    worst = min(ui.glass_ratio(kind, k) for k, _, _ in PROOF_COLS)
                    tag = ("" if r >= 4.5 else "FAILS ") + f"{r:.2f}:1" + ("  worst" if r == worst else "")
                b += ui.text(tx, ty + 98, tag, "caption", ui.NAVY, ls=0.5)
                return b
            tiles.append((f"Proof-{kind}-{bk}", tile))
    bd.file("GlassProofs", *tiles)

    # § 2 the window pane over the worst case: clear glass between white mullions, text only on plates in it
    y2 = 140 + len(kinds) * pitch - 4
    rows = {r["pair"]: r for r in ui.contrast_table()}

    def curtain():
        b = section(M, y2, lw, 2, "WINDOW PANE", "clear glass · plates · sky bloom")
        b += ui.backdrop("sky_bloom", M, y2 + 30, lw, 112, crop=(400, 200, 1300, 214), name="CurtainBack")
        px, py, pw, ph = M + 20, y2 + 40, lw - 40, 92
        b += ui.glass_pane(px, py, pw, ph, "clear", mullions_x=(1 / 3, 2 / 3), name="CurtainPane")
        bay = pw / 3
        b += ui.hud_chip(px + 16, py + 22, "12:34", rule=False, name="CurtainClock")
        b += ui.hud_chip(px + bay + 16, py + 22, "FLOOR 8", mono=True, rule=False, name="CurtainFloor")
        b += ui.hud_chip(px + 2 * bay + 16, py + 22, "Your firm", mono=False, icon="you", rule=False, name="CurtainFirm")
        return b

    def curtain_caption():
        n = rows.get("navy on plate · sky_bloom worst px")
        c = rows.get("navy on clear · navy_dark worst px")
        if not (n and c):  # a theme with its own contrast table
            return caption(M, y2 + 164, "Text sits on plates inside the pane; the pane itself carries none.", lw)
        return caption(M, y2 + 164, f"Navy on plate {n['engine']:.2f}:1 over bloom · navy straight on clear glass "
                       f"{c['engine']:.2f}:1 engine, {c['svg']:.2f}:1 in the mockup: never", lw)
    bd.file("CurtainWall", ("CurtainWall", curtain), ("CurtainCaption", curtain_caption))

    # § 3 crosshair + team markers
    def cross():
        y3 = y2 + 192
        b = section(M, y3, lw, 3, "CROSSHAIR · TEAM MARKERS", "1:1 over the three backdrops")
        zw = lw / 3
        for z, (bk, _, crop) in enumerate(PROOF_COLS):
            zx = M + z * zw
            b += ui.backdrop(bk, zx, y3 + 30, zw - 4, 80, crop=crop, name=f"CrossBack-{bk}")
            for k, st in enumerate(("default", "hit", "rival")):
                b += ui.crosshair(zx + 32 + k * 50, y3 + 70, st, name=f"Crosshair-{st}-{z}")
            b += ui.team_marker(zx + 174, y3 + 70, "you", 9, name=f"MarkerYou-{z}")
            b += ui.team_marker(zx + 202, y3 + 71, "rival", 9, name=f"MarkerRival-{z}")
        return b
    bd.file("Crosshair", ("Crosshair", cross))

    # right side: HUD plates
    r1, r2, cw = 800, 1340, 516

    def plate(x, y, w, h, crop, name):
        return ui.backdrop("office_sun", x, y, w, h, crop=crop, name=name)

    bd.file("HudPlates",
            ("PlateChips", lambda: section(r1, 64, cw, 4, "CHIPS · KEY HINT") +
             plate(r1, 96, cw, 140, (600, 640, 1100, 298), "PlateChipsBack"), "ground"),
            ("PlateTracker", lambda: section(r1, 260, cw, 5, "BRIEFCASE TRACKER", "one case, five states") +
             plate(r1, 292, cw, 392, (900, 150, 900, 683), "PlateTrackerBack"), "ground"),
            ("PlateInteract", lambda: section(r1, 708, cw, 6, "INTERACT · STAIRS", "Interact is F") +
             plate(r1, 740, cw, 256, (300, 560, 1100, 546), "PlateInteractBack"), "ground"),
            ("PlateSlot", lambda: section(r2, 64, cw, 7, "HANDS SLOT", "one slot, eight states") +
             plate(r2, 96, cw, 356, (1000, 600, 900, 621), "PlateSlotBack"), "ground"),
            ("PlateMarker", lambda: section(r2, 476, cw, 8, "BRIEFCASE MARKER", "in-world, two-tone frame") +
             plate(r2, 508, cw, 150, (760, 200, 1000, 291), "PlateMarkerBack"), "ground"),
            ("PlateFloor", lambda: section(r2, 700, cw, 9, "OPEN FLOOR · BANNERS", "ladder: L, 1–9, B, R") +
             plate(r2, 732, cw, 300, (200, 500, 1000, 581), "PlateFloorBack"), "ground"))

    def chips():
        b = ui.hud_chip(r1 + 16, 112, "12:34", name="ClockChip")
        w0 = int(16 + ui.text_width("12:34", "hud") + 18)
        b += ui.hud_chip(r1 + 32 + w0, 112, "Quarter closes in 3:00", mono=False, tone="warning", icon="warn",
                         name="ClockWarningChip")
        b += ui.key_hint(r1 + 16, 172, "Esc", "Menu", name="HintMenu")
        b += ui.key_hint(r1 + 32 + ui.key_hint_width("Esc", "Menu"), 172, "Q", "Lean left", name="HintLean")
        return b

    trackers = [("you", "your", 7, None), ("team", "your", 7, None), ("rival", "rival", 6, None),
                ("dropped", "rival", 7, None), ("returning", None, None, "0:12")]
    tr_items = [(f"Tracker-{st}", (lambda st=st, tw=tw, fl=fl, cd=cd, i=i:
                                   ui.briefcase_tracker(r1 + 16, 308 + i * 74, st, tw, fl, cd)))
                for i, (st, tw, fl, cd) in enumerate(trackers)]

    prompts = [
        ("InteractReady", lambda: ui.interact_prompt(r1 + 16, 756, "Pick up", "AR-15", key="F")),
        ("InteractBlocked", lambda: ui.interact_prompt(r1 + 16, 814, "Pick up", "Office chair", key="F",
                                                       state="blocked", reason="Hands full")),
        ("StairsReady", lambda: ui.stairs_prompt(r1 + 16, 872, 8, "ready", key="F")),
        ("StairsRefusal", lambda: ui.stairs_refusal(r1 + 16, 930, 8, key="F")),
    ]

    slots = [(None, "held", None), ("pistol", "held", None), ("ar15", "held", None), ("ar15", "out", "0 / 90"),
             ("grenade_briefcase", "held", "× 2"), ("office_chair", "held", None), ("cubicle_panel", "held", None),
             ("briefcase", "held", None)]
    slot_items = [(f"Slot-{it or 'empty'}-{st}", (lambda it=it, st=st, am=am, k=k:
                                                  ui.item_slot(r2 + 16 + (k % 2) * 252, 112 + (k // 2) * 84, it, st, am,
                                                               w=240)))
                  for k, (it, st, am) in enumerate(slots)]

    marks = [("team", "42 m", None, "Your firm"), ("rival", "36 m", None, "Rival firm"),
             ("dropped", "18 m", None, "Dropped"), ("returning", None, "0:12", "Returning")]

    def marker(k, st, dist, cd):
        mx = r2 + 64 + k * 129
        return ui.briefcase_marker(mx, 558, st, dist, cd)
    mk_items = [(f"Marker-{st}", (lambda k=k, st=st, d=d, cd=cd: marker(k, st, d, cd))) for k, (st, d, cd, _) in enumerate(marks)]

    def marker_captions():
        b = ""
        for k, (_, _, _, cap) in enumerate(marks):
            mx = r2 + 64 + k * 129
            b += ui.text(mx, 680, cap, "caption", ui.INK2, ls=0.5, anchor="middle")
        return b

    floor_items = [
        ("OpenFloor", lambda: ui.open_floor(r2 + 16, 748, 7, "rival")),
        ("ToastFloorOpened", lambda: ui.toast(r2 + 16, 828, "FLOOR 8 OPENED", "Your firm opened floor 8.", numeral=8)),
        ("ToastRivalFloor", lambda: ui.toast(r2 + 16, 928, "FLOOR 9 OPENED", "Rival firm opened floor 9.",
                                             tone="warning", numeral=9)),
    ]
    bd.file("HudChips", ("Chips", chips))
    tr_items.append(("RivalTowerTrace", lambda: ui.trace_brackets(r1 + 420, 330, 76, 330, ui.NAVY, seed=5,
                                                                    density=0.6)))
    bd.file("BriefcaseTracker", *tr_items)
    bd.file("InteractStairs", *prompts)
    bd.file("HandsSlot", *slot_items)
    bd.file("BriefcaseMarker", *mk_items, ("MarkerCaptions", marker_captions))
    bd.file("OpenFloorBanners", *floor_items)

    def notes():
        return notes_block(M, y2 + 324, lw, board="a3_glass_hud", lines=[
            "No dark panels: HUD text sits navy on a white plate; rival and threat on a solid amber plate.",
            "No marker over yourself: the hands slot and tracker carry \"You carry the briefcase\".",
        ], size=14)
    bd.file("DesignNotes", ("DesignNotes", notes))
    return bd


# --------------------------------------------------------------------------- main

def write_board(bd):
    folder = os.path.join(_ARGS.out, bd.key)
    paths = []
    order = [f for f in bd.files if f[0] != "DesignNotes"] + [f for f in bd.files if f[0] == "DesignNotes"]
    for n, (name, markup) in enumerate(order):
        num = 99 if name == "DesignNotes" else n
        p = ui.write(os.path.join(folder, f"{bd.key[:2]}_{num:02d}_{name}.svg"), markup)
        paths.append(p)
    return paths


_ARGS = None


def main(argv=None):
    global _ARGS
    ap = argparse.ArgumentParser(description="Render the three kit boards for a theme.")
    ap.add_argument("--theme", default="survey", help="survey (default), a theme name, or a theme_<name>.py path")
    ap.add_argument("--out", help="output folder for the board folders (default svg/kit, or svg/kit_<theme>)")
    ap.add_argument("--preview-dir", default=PREVIEW, help="where <theme>_<board>.png previews go")
    ap.add_argument("--no-preview", action="store_true")
    _ARGS = ap.parse_args(argv)
    name = ui.set_theme(_ARGS.theme)
    _ARGS.out = os.path.abspath(_ARGS.out or (OUT if name == "survey" else OUT + "_" + name))
    preview = not _ARGS.no_preview
    print(f"theme {name} ({ui.PASS_NAME}) -> {os.path.relpath(_ARGS.out)}")
    # stale outputs from the old one-sheet kit and earlier runs
    os.makedirs(_ARGS.out, exist_ok=True)
    for p in glob.glob(os.path.join(_ARGS.out, "*.svg")):
        os.remove(p)
    for d in glob.glob(os.path.join(_ARGS.out, "a[0-9]_*")):
        shutil.rmtree(d)
    failed = False
    boards = [board_a1(), board_a2(), board_a3()]
    for bd in boards:
        paths = write_board(bd)
        print(f"\n{bd.key}: {len(paths)} files (bottom to top)")
        for p in paths:
            print("  wrote", os.path.relpath(p))
        errs = bd.check()
        for e in errs:
            print("  CHECK FAIL", e)
        failed |= bool(errs)
        if not errs:
            print("  checks: no overlaps, nothing outside the margin, theme rules, families / hex / radii, one trace")
        if preview:
            png = ui.preview_many(paths, os.path.join(_ARGS.preview_dir, f"{name}_{bd.key}.png"))
            print("  preview", os.path.relpath(png), "%d x %d" % ui._png_size(png))
    if ui.MISSING_GLYPHS:
        failed = True
        print("\nMISSING GLYPHS (would render in a fallback font):", sorted(ui.MISSING_GLYPHS))
    print(f"\nText metrics: {ui.METRICS_SOURCE}")
    print("\nGlass text pairs (engine linear / SVG sRGB), worst backgrounds:")
    for r in ui.contrast_table():
        if not r["used"]:
            flag = f"  (not used: {r['note']})"
        elif r["engine"] < r["need"]:
            flag, failed = f"  << FAIL (needs {r['need']})", True
        else:
            flag = f"  ok ≥ {r['need']}" + (f" ({r['note']})" if r["note"] else "")
        print(f"  {r['pair']:48s} {r['engine']:6.2f}  {r['svg']:6.2f}{flag}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
