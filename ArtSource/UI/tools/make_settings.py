"""Seal the Deal: Settings screen mockups (Survey on Paper & Hairline), nine 1920 x 1080 artboards.

    python3 ArtSource/UI/tools/make_settings.py              # from the repo root
    python3 ArtSource/UI/tools/make_settings.py --no-preview # skip the Quick Look PNGs

Writes ArtSource/UI/svg/settings/<board>/<board>_NN_Name.svg (full 1920 x 1080 viewBox, NN = bottom-to-top
place order; _01_SheetLabel and _99_DesignNotes are annotation, hidden for the clean export) and two previews
per board in ArtSource/UI/svg/_preview/: settings_<board>.png (clean) and settings_<board>_annotated.png.

  b1_controls       Controls tab: focused slider, one changed-but-unapplied value
  b2_video          Video tab: Resolution disabled while Display mode is Borderless window
  b3_audio          Audio tab
  b4_accessibility  Accessibility tab: the crosshair rows and the way into the editor
  b5_crosshair      Crosshair editor: every parameter, live 1:1 preview over four backgrounds, presets
  b6_keybinds       Key bindings: keyboard + controller slot per action, Interact listening, Drop's pad slot unset
  b7_keyconflict    Key-bind clash modal (G pressed for Interact: G is Drop)
  b8_unsaved        Back with an unapplied change: Apply changes?
  b9_resolution     Resolution revert countdown

Light-glass look (2026-11-13 guide rework): paper field, no dark panels; modals are a frosted plate inside a
clear-glass frame over a paper wash; world samples (crosshair tiles, presets) sit in paper window frames.

Checks (exit 1 on any failure): per layer (screen / modal / annotation) item overlaps, text overlaps and text
crossing a pane edge; the kit's light rules per layer (no amber or cyan text, no light text on a light pane, no
text on clear glass); the display face never under 22 px; every row label clear of its own control; nothing
outside the 24 px margin; text under 16 px over the world; exactly one traced object per board and no trace
square touching text; notes fit their box; no hex literal in this file; no <circle>, <ellipse> or rx in the
output; only the stand-in families; no missing glyphs; no old game name and no E-as-Interact copy.
"""

from __future__ import annotations

import glob
import math
import os
import re
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ctd_ui as ui  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "svg", "settings"))
PREVIEW = os.path.normpath(os.path.join(HERE, "..", "svg", "_preview"))
W, H = 1920, 1080
EDGE = 24
FX, FW = 96, 1120            # form column
RCX, RCW = 1296, 528         # right column (clause numeral, detail card, notes)
TAB_Y, SEC_Y, ROW0 = 164, 236, 300
FOOT_Y = 968
ROW_H = ui.SIZE["row_h"]
DESC_H = 36
TABS = ["Controls", "Video", "Audio", "Accessibility"]
FAMILIES = {ui.DISPLAY, ui.UI_FACE, ui.UI_THIN, ui.DATA}


# --------------------------------------------------------------------------- board + checks

class Board:
    """One artboard: files bottom to top, each a list of named items drawn at 1:1, each on a layer.
    Layers are checked separately: "screen" (the settings screen), "modal" (scrim + dialog over it),
    "notes" (DesignNotes and the sheet label: annotation, hidden in the clean export)."""

    def __init__(self, key, title):
        self.key, self.title = key, title
        self.files = []
        self.items = []
        self.errs = []
        self.traces = []   # trace square boxes drawn on this board (ui.TRACE_BOXES is shared and reset per board)
        ui.reset_registry()

    def file(self, name, *items, layer="screen"):
        body = ""
        t0 = len(ui.TRACE_BOXES)
        for it in items:
            iname, fn = it[0], it[1]
            flag = it[2] if len(it) > 2 else ""
            k = ui.mark()
            markup = fn()
            shapes, texts, panes = ui.since(k)
            boxes = [s["box"] for s in shapes]
            box = (min(b[0] for b in boxes), min(b[1] for b in boxes),
                   max(b[2] for b in boxes), max(b[3] for b in boxes)) if boxes else None
            worlds = [sh["box"] for sh in shapes if sh.get("backdrop")]
            self.items.append(dict(name=iname, file=name, box=box, texts=texts, panes=panes,
                                   ground=(flag == "ground"), worlds=worlds, layer=layer))
            body += ui.group(iname, markup)
        self.traces += ui.TRACE_BOXES[t0:]
        self.files.append((name, layer, ui.group(f"{self.key[:2].upper()}-{name}", body)))

    def check(self):
        errs = list(self.errs)
        for layer in ("screen", "modal", "notes"):
            its = [it for it in self.items if it["layer"] == layer]
            solid = [it for it in its if not it["ground"] and it["box"]]
            for i, a in enumerate(solid):
                for b in solid[i + 1:]:
                    if _hit(_grow(a["box"], 2), _grow(b["box"], 2)):
                        errs.append(f"[{layer}] items overlap: {a['name']} {_r(a['box'])} / {b['name']} {_r(b['box'])}")
            texts = [(it["name"], t) for it in its for t in it["texts"]]
            for i, (na, a) in enumerate(texts):
                for nb, b in texts[i + 1:]:
                    ga = (a["box"][0] - 4, a["box"][1] + 0.5, a["box"][2] + 4, a["box"][3] - 0.5)
                    if _hit(ga, _grow(b["box"], -0.5)):
                        errs.append(f"[{layer}] text overlap: '{a['s']}' ({na}) / '{b['s']}' ({nb})")
            panes = [p for it in its for p in it["panes"]]
            for name, t in texts:
                for p in panes:
                    if _hit(t["box"], p["box"]) and not _inside(t["box"], _grow(p["box"], 0.5)):
                        errs.append(f"[{layer}] text crosses a pane edge: '{t['s']}' ({name})")
            errs += [f"[{layer}] {e}" for e in ui.layout_rules(its)]   # the kit's light-glass rules
        all_texts = [(it["name"], t) for it in self.items for t in it["texts"]]
        for name, t in all_texts:
            if t["family"] == ui.DISPLAY and t["size"] < 22:
                errs.append(f"display face under 22 px: '{t['s']}' ({name}, {t['size']} px)")
        for sq in self.traces:
            for name, t in all_texts:
                if _hit(_grow(sq, 1), t["box"]):
                    errs.append(f"trace square {_r(sq)} touches text '{t['s']}' ({name})")
        for it in self.items:
            bx = it["box"]
            if bx and it["name"] != "Frame" and it["name"] != "Scrim" and (
                    bx[0] < EDGE or bx[1] < EDGE or bx[2] > W - EDGE or bx[3] > H - EDGE):
                errs.append(f"outside the margin: {it['name']} {_r(bx)}")
        worlds = [w for it in self.items for w in it["worlds"]]
        for it in self.items:
            for t in it["texts"]:
                if t["size"] < 16 and any(_hit(t["box"], w) for w in worlds):
                    errs.append(f"text under 16 px over the world: '{t['s']}' ({it['name']})")
                if t["family"] not in FAMILIES:
                    errs.append(f"unknown family {t['family']}")
        n_trace = sum(m.count('id="TraceCluster"') for _, _, m in self.files)
        if n_trace != 1:
            errs.append(f"{n_trace} traced objects (want exactly 1)")
        return errs


def _grow(b, d):
    return (b[0] - d, b[1] - d, b[2] + d, b[3] + d)


def _hit(a, b):
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]


def _inside(a, b):
    return a[0] >= b[0] and a[1] >= b[1] and a[2] <= b[2] and a[3] <= b[3]


def _r(b):
    return tuple(round(v) for v in b)


# --------------------------------------------------------------------------- small local helpers

def tw(s, role="item", **kw):
    return ui.text_width(s, role, **kw)


def subhead(x, y, w, label, note=None):
    """Compact clause sub-heading, 40 px: near-black square bullet (the kit's section bullet), UI-face caps 16
    tracked +2 (the thin display face is never set under 22 px), keyline."""
    b = ui.square_bullet(x, y + 15, 8, ui.BLACK)
    b += ui.text(x + 18, y + 24, label, "label", ui.NAVY, ls=2)
    if note:
        b += ui.text_right(x + w, y + 24, note, "label", ui.INK2, weight=400)
    b += ui.line(x, y + 39.5, x + w, y + 39.5, ui.NAVY)
    return b


def lit_band(x, y, w):
    """The kit's focused-row band: light navy wash and a 4 px cobalt edge."""
    return ui.rect(x, y, w, ROW_H, (ui.NAVY, 0.04)) + ui.rect(x, y, 4, ROW_H, ui.COBALT)


def desc_line(x, y, s):
    return ui.text(x + 16, y + ROW_H + 26, s, "body", ui.INK2)


def dirty_mark(x, y, was):
    """Changed but not applied: cobalt square + "Changed · was Off" (label 16, ink-2). Returns (markup, width)."""
    s = f"Changed · was {was}"
    return (ui.rect(x, y + ROW_H / 2 - 5, 10, 10, ui.COBALT) +
            ui.text(x + 18, y + ROW_H / 2 + 6, s, "label", ui.INK2)), 18 + tw(s, "label")


def swatch_group(x, y, names, sel, focus=False):
    """Colour swatches: 28 px squares 12 apart, the selected one ringed (2 px navy, 3 px gap)."""
    fills = {"Paper White": ui.PAPER, "Cyan": ui.CYAN, "Electric Blue": ui.ELECTRIC}
    b = ""
    for i, n in enumerate(names):
        sx = x + i * 40
        b += ui.rect(sx + 0.5, y + 0.5, 27, 27, fills[n], ui.NAVY, 1)   # cyan / electric: a mark with a navy keyline
        if i == sel:
            b += ui.rect(sx - 4, y - 4, 36, 36, None, ui.NAVY, 2)
    if focus:
        b += ui.focus_ring(x - 10, y - 10, len(names) * 40 - 12 + 20, 48)
    return b


def swatch_width(names):
    return len(names) * 40 - 12


def focus_chevrons(x_left, y):
    """The guide's chevron row (ref 07) pointing in at the focused row from the left gutter: three chevrons
    ending 12 px before the row's cobalt edge. Decorative; the band and edge carry the focus."""
    return ui.chevron_row(x_left - 36, y + ROW_H / 2, n=3, direction="right", s=6, gap=10, color=ui.NAVY,
                          sw=1.5, name=ui.uid("FocusChevrons"))


def window_panes(x, y, pw, ph, n, frame, fill, name=None):
    """World samples seen through a window (ref 08 device, drawn fresh): n panes pw x ph side by side in a paper
    frame `frame` px thick, each pane with a silver reveal on its top and left edge (the frame's depth) and the
    whole frame edged with a 1 px hairline. fill(i, px, py) returns pane i's content (drawn first, clipped).
    Returns (markup, [(px, py)]). Local helper (kit candidate: sky_window with caller-supplied pane content)."""
    w = n * pw + (n + 1) * frame
    h = ph + 2 * frame
    b, origins = "", []
    for i in range(n):
        px, py = x + frame + i * (pw + frame), y + frame
        origins.append((px, py))
        b += ui.clip(px, py, pw, ph, fill(i, px, py))
    fr = ""
    for i in range(n + 1):
        fr += ui.rect(x + i * (pw + frame), y, frame, h, ui.PAPER)
    fr += ui.rect(x, y, w, frame, ui.PAPER) + ui.rect(x, y + h - frame, w, frame, ui.PAPER)
    for px, py in origins:
        fr += ui.rect(px, py, pw, max(3, round(frame * 0.3)), (ui.SILVER, 0.9))
        fr += ui.rect(px, py, max(2, round(frame * 0.2)), ph, (ui.SILVER, 0.6))
        fr += ui.rect(px - 0.5, py - 0.5, pw + 1, ph + 1, None, ui.HAIR, 1)
    fr += ui.rect(x + 0.5, y + 0.5, w - 1, h - 1, None, ui.HAIR_STRONG, 1)
    return ui.group(name or ui.uid("WindowPanes"), b + fr), origins


def conflict_tile(x, y):
    """18 px hazard tile for a key-conflict line: amber plate, 1 px near-black edge, navy hazard (the kit's look)."""
    return (ui.rect(x, y, 18, 18, ui.AMBER) + ui.rect(x + 0.5, y + 0.5, 17, 17, None, ui.BLACK, 1) +
            ui.warn_glyph(x + 9, y + 10, 6, ui.NAVY))


# --------------------------------------------------------------------------- rows

def draw_row(bd, x, y, w, r, state="default", track=200):
    """One settings row from a row dict. Returns (markup, height, label_box).
    r: kind, label, value, plus optional desc (shown when focused), reason (disabled: always shown),
    dirty (the previous value: draws the changed marker), cw (dropdown / stepper width), button (label)."""
    kind, label, value = r["kind"], r["label"], r.get("value")
    lit = state in ("focus", "listening", "conflict")
    right = x + w - 12
    cw = r.get("cw", 260)
    b = ""
    if kind in ("slider", "toggle", "dropdown", "stepper"):
        b += ui.settings_row(x, y, w, kind, label, value, state, desc=r.get("desc"), control_w=cw, track=track,
                             name=ui.uid(f"Row-{label}-"))
        if kind == "slider":
            ctrl_left = right - (track + 20 + ui.SIZE["value_field_w"]) - (8 if state == "focus" else 0)
        elif kind == "toggle":
            ctrl_left = right - ui.toggle_width(value) - (6 if state == "focus" else 0)
        else:
            ctrl_left = right - cw
    else:
        if lit:
            b += lit_band(x, y, w)
        ink = (ui.NAVY, 0.45) if state == "disabled" else ui.NAVY
        b += ui.text(x + 16, y + ROW_H / 2 + 7, label, "item", ink)
        if kind == "button":
            bl = r["button"]
            bw = int(tw(bl) + 20 + 44)
            ctrl_left = right - bw
            b += ui.button(ctrl_left, y + 4, bw, bl, "secondary", "focus" if state == "focus" else "default")
            b += ui.chevron(ctrl_left + bw - 22, y + 4 + 28, 6, "right", ui.NAVY)
        elif kind == "swatches":
            names, sel = value
            sx = right - swatch_width(names)
            b += swatch_group(sx, y + 18, names, sel, focus=(state == "focus"))
            nm = names[sel]
            ctrl_left = sx - 20 - tw(nm, "body") - (10 if state == "focus" else 0)
            b += ui.text(sx - 20 - tw(nm, "body") - 0, y + ROW_H / 2 + 6, nm, "body", ui.NAVY)
        elif kind == "note":
            ctrl_left = right - tw(value, "body")
            b += ui.text(ctrl_left, y + ROW_H / 2 + 6, value, "body", ui.INK2)
        else:
            raise ValueError(kind)
        b += ui.line(x, y + ROW_H - 0.5, x + w, y + ROW_H - 0.5, ui.HAIR)
        if lit and r.get("desc"):
            b += desc_line(x, y, r["desc"])
    label_right = x + 16 + tw(label)
    if r.get("dirty") is not None:
        m, mw = dirty_mark(label_right + 14, y, r["dirty"])
        b += m
        label_right += 14 + mw
    h = ROW_H
    if lit and r.get("desc"):
        h += DESC_H
    if state == "disabled" and r.get("reason"):
        b += ui.lock_glyph(x + 24, y + ROW_H + 20, 6, ui.INK2)
        b += ui.text(x + 40, y + ROW_H + 26, r["reason"], "body", ui.INK2)
        h += DESC_H
    if label_right + 16 > ctrl_left:
        bd.errs.append(f"label '{label}' runs into its control ({round(label_right)} > {round(ctrl_left) - 16})")
    return b, h, (x + 16, y, label_right, y + ROW_H)


def layout_rows(rows, focus, x, y0, w):
    """Precompute each row's y so annotation tags can find them. Returns [(row, y, state)]."""
    out, y = [], y0
    for i, r in enumerate(rows):
        if r["kind"] == "subhead":
            out.append((r, y, "default"))
            y += 40
            continue
        st = r.get("state") or ("focus" if i == focus else "default")
        out.append((r, y, st))
        y += ROW_H
        if st in ("focus", "listening", "conflict") and r.get("desc"):
            y += DESC_H
        if st == "disabled" and r.get("reason"):
            y += DESC_H
    return out, y


def form(bd, rows, focus, x, y0, w, track=200, trace=False, chevrons=True):
    """Draw a list of rows (and "subhead" entries) top to bottom. trace=True puts the board's one
    traced object (bracket cluster) on the focused row. chevrons: the focus chevron row in the left gutter
    (off where the gutter is narrower than 40 px)."""
    placed, _ = layout_rows(rows, focus, x, y0, w)
    b = ""
    tags = []
    for r, y, st in placed:
        if r["kind"] == "subhead":
            b += subhead(x, y, w, r["label"], r.get("note"))
            continue
        m, h, lab = draw_row(bd, x, y, w, r, st, track)
        b += m
        if chevrons and st == "focus":
            b += focus_chevrons(x, y)
        if trace and st == "focus":
            b += ui.trace_brackets(x - 2, y - 2, w + 4, h + 4, ui.NAVY, seed=11, density=0.22)
        tags.append((r, lab))
    return b, tags


def new_code_tags(tags):
    """Annotation: a small tag after each row label from the inventory's Existence column."""
    b = ""
    for r, (lx, ly, lr, lb) in tags:
        ex = r.get("exists")
        if not ex:
            continue
        txt, col = ("NEEDS NEW CODE", ui.COBALT) if ex == "new" else ("INSPECTOR ONLY", ui.INK2)
        t = tw(txt, "caption", ls=1)
        tx = lr + 14
        b += ui.rect(tx + 0.5, ly + ROW_H / 2 - 11.5, t + 15, 23, ui.PAPER, col, 1)
        b += ui.text(tx + 8, ly + ROW_H / 2 + 5, txt, "caption", col, ls=1)
    return b


# --------------------------------------------------------------------------- shared screen pieces

def frame():
    b = ui.rect(0, 0, W, H, ui.PAPER)
    b += ui.hairline_grid(0, 0, W, H, faint=True, name="Grid")
    b += ui.crop_marks(16, 16, W - 32, H - 32, name="SheetCrop")
    return b


def sheet_label(code, title):
    return (ui.square_bullet(FX, 33, 9, ui.BLACK) +
            ui.text(FX + 20, 42, f"{ui.GAME_TITLE} · SETTINGS · SURVEY · {code}  {title}", "caption", ui.NAVY, ls=2))


def header(crumb=None):
    b = ui.text(FX - 2, 124, "SETTINGS", "heading", ui.NAVY)
    if crumb:
        b += ui.text(FX + tw("SETTINGS", "heading") + 28, 122, crumb, "item", ui.INK2)
    b += ui.microtype_strip(FX, 146, "SCHEDULE 4 · PLAYER PREFERENCES · KEPT ON THIS PC · CTD-SET-0.1.0")
    return b


def clause_numeral(n, label):
    return ui.floor_numeral(RCX - 8, 260, n, size=200, label=label)


def footer(dirty_msg=None, extra_buttons=(), apply_enabled=None):
    """Apply · Reset to defaults · Back, the change status, and the key hints (Q / E, Esc)."""
    apply_on = bool(dirty_msg) if apply_enabled is None else apply_enabled
    y = FOOT_Y
    b = ui.button(FX, y, 180, "Apply", "primary", "default" if apply_on else "disabled", name="Apply")
    x = FX + 180 + 24
    for lab, st in list(extra_buttons) + [("Reset to defaults", "default"), ("Back", "default")]:
        bw = int(tw(lab) + 48)
        b += ui.button(x, y + 4, bw, lab, "secondary", st, name=ui.uid(f"Foot-{lab}-"))
        x += bw + 16
    sx = x + 16
    if dirty_msg:
        b += ui.rect(sx, y + 27, 10, 10, ui.COBALT) + ui.text(sx + 20, y + 38, dirty_msg, "body", ui.NAVY)
    else:
        b += ui.rect(sx + 0.5, y + 27.5, 9, 9, None, ui.HAIR_STRONG, 1)
        b += ui.text(sx + 20, y + 38, "No changes to apply.", "body", ui.INK2)
    return b


def key_hints(y=FOOT_Y + 16):
    """Right-aligned menu key hints on paper: Q E Switch tab · Esc Back."""
    parts = [(("Q", "E"), "Switch tab"), (("Esc",), "Back")]
    widths = []
    for keys, lab in parts:
        kw = sum(max(32, int(tw(k, "hud", size=16) + 18)) + 6 for k in keys) + 4 + tw(lab, "label")
        widths.append(kw)
    x = 1824 - (sum(widths) + 28 * (len(parts) - 1))
    b = ""
    for (keys, lab), wd in zip(parts, widths):
        kx = x
        for k in keys:
            cap, kw_ = ui.keycap(kx, y, k, dark=False, size=16, h=32)
            b += cap
            kx += kw_ + 6
        b += ui.text(kx + 4, y + 22, lab, "label", ui.NAVY)
        x += wd + 28
    return b


def screen_micro():
    return ui.microtype_strip(FX, 1052, "§ 0.4 Settings are kept on this PC when you choose Apply. Values preview "
                              "live until then. Terms and Conditions Apply.")


def notes_panel(bd, x, y, w, lines, max_y):
    """DesignNotes: cobalt label, numbered notes (label 14 regular, ink-2) on a paper card with a cobalt edge."""
    body = ui.text(x + 16, y + 24, "DESIGN NOTES", "caption", ui.COBALT, ls=2)
    yy = y + 48
    for i, n in enumerate(lines, 1):
        body += ui.text(x + 16, yy, f"{i}", "caption", ui.COBALT, ls=0)
        md, hgt = ui.text_lines(x + 36, yy, n, w - 52, "label", ui.INK2, size=14, weight=400, leading=1.35)
        body += md
        yy += hgt + 5
    h = yy - y + 4
    if y + h > max_y:
        bd.errs.append(f"notes overflow their box by {round(y + h - max_y)} px")
    card = ui.rect(x, y, w, h, ui.PAPER, ui.HAIR_STRONG, 1) + ui.rect(x, y, 3, h, ui.COBALT)
    return card + body


def detail_card(x, y, w, code, title, pairs, extra=None, extra_h=0, trace=True):
    """Right-column clause card for the focused row: code + caps title, keyline, key / value lines."""
    b = ui.rect(x, y, w, 2, ui.NAVY)
    b += ui.text(x, y + 30, code, "caption", ui.INK2, ls=1)
    b += ui.text(x + tw(code, "caption", ls=1) + 14, y + 31, title, "label", ui.NAVY, size=18, ls=2)
    yy = y + 72
    for k, v, mono in pairs:
        b += ui.text(x, yy, k, "label", ui.INK2)
        if mono:
            b += ui.text(x + 172, yy, v, "hud", ui.NAVY, size=18)
        else:
            b += ui.text(x + 172, yy, v, "body", ui.NAVY)
        b += ui.line(x, yy + 13.5, x + w, yy + 13.5, ui.HAIR)
        yy += 40
    if extra:
        b += extra(yy)
    if trace:  # the board's one traced object: the clause under survey (the focused row's card)
        # 24 px side inset: squares jitter up to 16 px inside the edge, so they stay 8 px clear of the labels
        b += ui.trace_brackets(x - 24, y - 14, w + 48, yy - 26 + extra_h - y + 28, ui.NAVY, seed=11, density=0.3)
    return b


# --------------------------------------------------------------------------- crosshair drawing

XH_COLOURS = {"Paper White": ui.PAPER, "Cyan": ui.CYAN, "Electric Blue": ui.ELECTRIC}
XH_EDGES = {"Near Black": ui.BLACK, "Deep Navy": ui.NAVY}
XH_DEFAULT = dict(style="cross_dot", arm=6, th=2, gap=4, dot=2, colour="Paper White", opacity=1.0,
                  outline=True, oth=1, oop=1.0, edge="Near Black")


def xhair_rects(cx, cy, p):
    """Core rects (x, y, w, h) for a crosshair parameter set, snapped to whole pixels."""
    cx, cy = int(round(cx)), int(round(cy))
    a, th, gap, arm = p["th"] // 2, p["th"], p["gap"], p["arm"]
    rs = []
    if p["style"] in ("cross", "cross_dot"):
        rs += [(cx - gap - arm, cy - a, arm, th), (cx + gap, cy - a, arm, th),
               (cx - a, cy - gap - arm, th, arm), (cx - a, cy + gap, th, arm)]
    if p["style"] == "brackets":
        x0, y0, x1, y1 = cx - gap - arm, cy - gap - arm, cx + gap + arm, cy + gap + arm
        for (hx, hy, vx, vy) in ((x0, y0, x0, y0), (x1 - arm, y0, x1 - th, y0),
                                 (x0, y1 - th, x0, y1 - arm), (x1 - arm, y1 - th, x1 - th, y1 - arm)):
            rs += [(hx, hy, arm, th), (vx, vy, th, arm)]
    if p["style"] in ("dot", "cross_dot"):
        d = p["dot"]
        rs.append((cx - d // 2, cy - d // 2, d, d))
    return rs


def xhair(cx, cy, p, name=None):
    """Local variant crosshair (the kit's ui.crosshair draws only the default). Outline rects grown by the
    outline thickness first, then the core; all on whole pixels at 1:1."""
    rs = xhair_rects(cx, cy, p)
    b = ""
    if p["outline"]:
        o = p["oth"]
        for x, y, w, h in rs:
            b += ui.rect(x - o, y - o, w + 2 * o, h + 2 * o, (XH_EDGES[p["edge"]], p["oop"]))
    core = "".join(ui.rect(x, y, w, h, XH_COLOURS[p["colour"]]) for x, y, w, h in rs)
    b += ui.group(ui.uid("XhCore"), core, opacity=p["opacity"] if p["opacity"] < 1 else None)
    return ui.group(name or ui.uid("Crosshair-variant-"), b)


def xh_best(colour, bg, edge=ui.BLACK):
    return max(ui.contrast(colour, bg), ui.contrast(edge, bg))


# --------------------------------------------------------------------------- tab data (inventory §2.1)

CONTROLS = [
    dict(kind="slider", label="Mouse sensitivity", value=(0.12, 0.02, 0.5, "0.12"), exists="inspector",
         desc="Degrees the view turns per mouse unit. Previewed as you move it."),
    dict(kind="toggle", label="Invert vertical look", value=False, exists="new"),
    dict(kind="slider", label="Controller look speed", value=(180, 60, 360, "180"), exists="new",
         desc="Degrees per second at full stick tilt."),
    dict(kind="slider", label="Controller dead zone", value=(0.1, 0, 0.5, "0.10"), exists="inspector"),
    dict(kind="stepper", label="Lean", value="Hold", cw=200, exists="new"),
    dict(kind="stepper", label="Sprint", value="Hold", cw=200, exists="new"),
    dict(kind="button", label="Key bindings", button="Edit key bindings", exists="new"),
]

VIDEO = [
    dict(kind="slider", label="Field of view", value=(91, 80, 110, "91"), exists="inspector",
         desc="Horizontal degrees at 16:9. 91 matches today's 60° vertical."),
    dict(kind="dropdown", label="Display mode", value="Borderless window", cw=300, exists="inspector"),
    dict(kind="dropdown", label="Resolution", value="1920 × 1080 (desktop)", cw=300, exists="inspector",
         state="disabled", reason="Borderless window always uses the desktop resolution. Choose Full screen "
                                  "or Windowed to change it."),
    dict(kind="toggle", label="Vertical sync", value=True, exists="inspector"),
    dict(kind="dropdown", label="Frame rate limit", value="None", cw=300, exists="new"),
    dict(kind="dropdown", label="Graphics quality", value="High", cw=300, exists="inspector"),
    dict(kind="slider", label="Screen shake", value=(100, 0, 100, "100%"), exists="new"),
]

AUDIO = [
    dict(kind="slider", label="Master volume", value=(80, 0, 100, "80%"), exists="new"),
    dict(kind="slider", label="Effects", value=(100, 0, 100, "100%"), exists="new"),
    dict(kind="slider", label="Music", value=(70, 0, 100, "70%"), exists="new"),
    dict(kind="slider", label="Secretary", value=(100, 0, 100, "100%"), exists="new",
         desc="Her announcements carry match news. Subtitles are on the Accessibility tab."),
    dict(kind="toggle", label="Play audio when the window is in the background", value=False, exists="new"),
]

ACCESS = [
    dict(kind="toggle", label="Secretary subtitles", value=True, exists="new"),
    dict(kind="slider", label="Interface size", value=(100, 80, 130, "100%"), exists="new"),
    dict(kind="stepper", label="Crosshair style", value="Cross + Dot", cw=240, exists="new"),
    dict(kind="swatches", label="Crosshair colour", value=(["Paper White", "Cyan", "Electric Blue"], 0), exists="new"),
    dict(kind="slider", label="Crosshair size", value=(100, 50, 200, "100%"), exists="new"),
    dict(kind="button", label="Crosshair editor", button="Edit crosshair", exists="new",
         desc="Shape, colour, outline and hit markers, previewed over sun, sky and dark rooms."),
    dict(kind="toggle", label="Show network stats", value=False, exists="new"),
]


def tab_screen(bd, tab_i, rows, focus, dirty_msg=None, trace=True, numeral=True, card=None):
    """Files for one settings tab: Header, TabBar, Form, Detail, Footer. Returns the row tags."""
    holder = {}

    def form_fn():
        sec = ui.settings_row(FX, SEC_Y, FW, "header", f"§ {tab_i + 1}  {TABS[tab_i].upper()}")
        m, tags = form(bd, rows, focus, FX, ROW0, FW)
        holder["tags"] = tags
        return sec + m

    bd.file("Header", ("Header", header), ("Micro", screen_micro))
    bd.file("TabBar", ("TabBar", lambda: ui.tab_bar(FX, TAB_Y, FW, TABS, active=tab_i)))
    bd.file("Form", ("Form", form_fn))
    det = []
    if numeral:
        det.append(("ClauseNumeral", lambda: clause_numeral(tab_i + 1, f"CLAUSE {tab_i + 1} · {TABS[tab_i].upper()}")))
    if card:
        det.append(("DetailCard", lambda: card(trace)))
    if det:
        bd.file("Detail", *det)
    bd.file("Footer", ("Footer", lambda: footer(dirty_msg)), ("KeyHints", key_hints))
    return holder


# --------------------------------------------------------------------------- B1 controls

def card_controls(trace=True):
    cm = 360 / 0.12 / 800 * 2.54   # cm of mouse travel per full turn at 800 DPI
    return detail_card(RCX, 344, RCW, "§ 1.1", "MOUSE SENSITIVITY", [
        ("Range", "0.02 – 0.50", True), ("Default", "0.12", True), ("Unit", "Degrees per mouse unit", False),
        ("At 800 DPI", f"{cm:.1f} cm per full turn", False), ("Takes effect", "Live preview, kept on Apply", False)], trace=trace)


def board_b1():
    bd = Board("b1_controls", "CONTROLS")
    bd.file("Frame", ("Frame", frame, "ground"))
    rows = [dict(r) for r in CONTROLS]
    rows[1] = dict(rows[1], value=True, dirty="Off")
    hold = tab_screen(bd, 0, rows, 0, dirty_msg="1 change not applied: Invert vertical look.", card=card_controls)
    notes(bd, hold, [
        "Apply model: every value previews live but is only kept on Apply. Back with a change asks Apply / "
        "Discard (B8). Changed rows carry the cobalt square and \"was\" value until applied.",
        "Tags from inventory §2.1 Existence. INSPECTOR ONLY rows still need save, load and a UI binding; "
        "the Settings screen itself is not built.",
        "Lean and Sprint are the inventory's Hold / Toggle segmented control, drawn as the kit's stepper "
        "(left / right cycles, one control for mouse and pad).",
        "Q / E switch tabs in the UI map only (needs new code); in play Q / E are Lean. Pad: LB / RB.",
        "Opened from the in-game menu, this same screen sits on the frosted in-game pane; the match keeps "
        "running behind it and Back returns to the menu.",
    ], y=640)
    return bd


def notes(bd, hold, lines, x=RCX, y=650, w=RCW, max_y=950):
    bd.file("SheetLabel", ("SheetLabel", lambda: sheet_label(bd.key[:2].upper(), bd.title)), layer="notes")
    bd.file("DesignNotes", ("Tags", lambda: new_code_tags(hold.get("tags", []))),
            ("Notes", lambda: notes_panel(bd, x, y, w, lines, max_y)), layer="notes")


# --------------------------------------------------------------------------- B2 video

def card_video(trace=True):
    h_fov = 91
    v_fov = math.degrees(2 * math.atan(math.tan(math.radians(h_fov / 2)) / (16 / 9)))
    return detail_card(RCX, 344, RCW, "§ 2.1", "FIELD OF VIEW", [
        ("Range", "80 – 110° horizontal", True), ("Default", "91°", True),
        ("Vertical at 16:9", f"{v_fov:.0f}°", True), ("Takes effect", "Live preview, kept on Apply", False)],
        trace=trace)


def board_b2():
    bd = Board("b2_video", "VIDEO")
    bd.file("Frame", ("Frame", frame, "ground"))
    hold = tab_screen(bd, 1, VIDEO, 0, card=card_video)
    notes(bd, hold, [
        "Resolution is disabled while Display mode is Borderless window: a design rule, not a Unity limit "
        "(a borderless window can render lower and upscale). Owner call. Choosing Full screen or Windowed "
        "enables it; applying a new resolution starts the revert countdown (B9).",
        "Resolution lists the monitor's modes; its default is the desktop resolution.",
        "Graphics quality Low / High map to the Mobile / PC quality levels.",
        "Disabled rows stay visible with the reason under them and are skipped by focus.",
        "No changes on this tab, so Apply is disabled and the status says so.",
    ], y=640)
    return bd


# --------------------------------------------------------------------------- B3 audio

def card_audio(trace=True):
    return detail_card(RCX, 344, RCW, "§ 3.4", "SECRETARY", [
        ("Range", "0 – 100%", True), ("Default", "100%", True), ("Mixer bus", "Secretary (voice)", False),
        ("Subtitles", "Accessibility, § 4.1", False), ("Takes effect", "Live preview, kept on Apply", False)],
        trace=trace)


def board_b3():
    bd = Board("b3_audio", "AUDIO")
    bd.file("Frame", ("Frame", frame, "ground"))
    hold = tab_screen(bd, 2, AUDIO, 3, card=card_audio)
    notes(bd, hold, [
        "Every audio row needs new code: the repo has no AudioMixer and no sound files yet. Each slider maps "
        "to one mixer bus.",
        "The secretary has her own bus because she carries the match news and the comedy.",
        "Background audio is off by default so an alt-tabbed game stays quiet.",
    ])
    return bd


# --------------------------------------------------------------------------- B4 accessibility

def card_access(trace=True):
    samples = (("sky_bloom", "Sun", (880, 480, 96, 72)), ("sky_bloom", "Sky", (40, 20, 96, 72)),
               ("navy_dark", "Dark", (880, 380, 96, 72)))

    def swatches(yy):
        b = ui.text(RCX, yy + 6, "CURRENT CROSSHAIR · 1:1", "caption", ui.INK2, ls=1)

        def fill(i, px, py):
            kind, nm, crop = samples[i]
            world = (ui.rect(px, py, 96, 72, ui.BLOOM) if nm == "Sun" else   # clipped sun bloom: pure white
                     ui.backdrop(kind, px, py, 96, 72, crop=crop, name=f"CardTile-{nm}"))
            return (world +
                    ui.crosshair(px + 48, py + 36, name=f"CardCrosshair-{nm}"))
        m, origins = window_panes(RCX, yy + 18, 96, 72, 3, 10, fill, name="CardWindow")
        b += m
        for (px, _), (_, nm, _) in zip(origins, samples):
            b += ui.text(px, yy + 132, nm, "label", ui.NAVY)
        return b
    return detail_card(RCX, 344, RCW, "§ 4.6", "CROSSHAIR", [
        ("Style", "Cross + Dot", False), ("Colour", "Paper White", False),
        ("Outline", "Near Black, 1 px", False)], extra=swatches, extra_h=176, trace=trace)


def board_b4():
    bd = Board("b4_accessibility", "ACCESSIBILITY")
    bd.file("Frame", ("Frame", frame, "ground"))
    hold = tab_screen(bd, 3, ACCESS, 5, card=card_access)
    notes(bd, hold, [
        "Quick rows here; Edit crosshair opens the full set (B5). Size scales whole pixels.",
        "Style default Cross + Dot (inventory: Dot; research and kit: Cross + Dot). No Circle.",
        "Palette colours only; never Amber (rival, threat). Cyan and Electric Blue show the editor's "
        "\"faint on blue sky\" line.",
        "Trace on the clause card, off the focus ring: deliberate. Its samples are world crops in a window frame.",
    ], y=724)
    return bd


# --------------------------------------------------------------------------- B5 crosshair editor

PX0, PXW = 1064, 760       # preview column
COL_A, COL_B, COLW = 96, 560, 440


def board_b5():
    bd = Board("b5_crosshair", "CROSSHAIR EDITOR")
    bd.file("Frame", ("Frame", frame, "ground"))
    col_a = [
        dict(kind="subhead", label="SHAPE"),
        dict(kind="stepper", label="Style", value="Cross + Dot", cw=220),
        dict(kind="slider", label="Arm length", value=(6, 0, 20, "6 px")),
        dict(kind="slider", label="Arm thickness", value=(2, 1, 6, "2 px")),
        dict(kind="slider", label="Centre gap", value=(4, 0, 20, "4 px")),
        dict(kind="toggle", label="Centre dot", value=True),
        dict(kind="slider", label="Dot size", value=(2, 1, 6, "2 px")),
        dict(kind="subhead", label="OUTLINE"),
        dict(kind="toggle", label="Outline", value=True),
        dict(kind="slider", label="Outline thickness", value=(1, 1, 3, "1 px")),
        dict(kind="slider", label="Outline opacity", value=(100, 60, 100, "100%")),
    ]
    col_b = [
        dict(kind="subhead", label="COLOUR"),
        dict(kind="swatches", label="Colour", value=(["Paper White", "Cyan", "Electric Blue"], 0),
             desc="Cyan and Electric Blue are faint on blue sky."),
        dict(kind="slider", label="Opacity", value=(100, 30, 100, "100%")),
        dict(kind="stepper", label="Outline colour", value="Near Black", cw=200),
        dict(kind="subhead", label="BEHAVIOUR"),
        dict(kind="stepper", label="Show spread", value="Both", cw=200),
        dict(kind="slider", label="Spread multiplier", value=(1.0, 0, 2, "1.0")),
        dict(kind="toggle", label="Scale with resolution", value=False),
        dict(kind="toggle", label="Hit marker", value=True),
        dict(kind="slider", label="Hit marker sound", value=(80, 0, 100, "80%")),
        dict(kind="toggle", label="Kill marker", value=True),
    ]
    bd.file("Header", ("Header", lambda: header("Accessibility · Crosshair editor")), ("Micro", screen_micro))
    bd.file("TabBar", ("TabBar", lambda: ui.tab_bar(FX, TAB_Y, FW, TABS, active=3)))
    bd.file("Parameters", ("ColumnA", lambda: form(bd, col_a, -1, COL_A, SEC_Y, COLW, track=120)[0]),
            ("ColumnB", lambda: form(bd, col_b, 1, COL_B, SEC_Y, COLW, track=120, chevrons=False)[0]))

    # four world samples seen through one window frame (12 px paper mullions): the crosshair at 1:1 in each pane
    tiles = [("office_sun", "Office sun", (420, 650, 175, 150)),
             ("sky_bloom", "Blue sky", (0, 0, 175, 150)),
             ("navy_dark", "Dark room", (800, 300, 175, 150)),
             ("sky_bloom", "Sun bloom", (870, 470, 175, 150))]
    ty, fr, tw_, th_ = 284, 12, 175, 150
    wh = th_ + 2 * fr
    pane_x = [PX0 + fr + i * (tw_ + fr) for i in range(4)]

    def preview():
        b = subhead(PX0, SEC_Y, PXW, "LIVE PREVIEW · 1:1", note="Physical pixels, not scaled")

        def fill(i, px, py):
            kind, nm, crop = tiles[i]
            world = (ui.rect(px, py, tw_, th_, ui.BLOOM) if nm == "Sun bloom" else   # clipped bloom: pure white
                     ui.backdrop(kind, px, py, tw_, th_, crop=crop, name=f"Tile-{i}-{kind}"))
            return (world +
                    ui.crosshair(px + tw_ // 2, py + th_ // 2, name=f"Preview-{nm}"))
        b += window_panes(PX0, ty, tw_, th_, 4, fr, fill, name="PreviewWindow")[0]
        b += ui.trace_brackets(PX0 - 2, ty - 2, PXW + 4, wh + 4, ui.NAVY, seed=3, density=0.18)
        return b

    def tile_labels():
        b = ""
        for px, (kind, nm, crop) in zip(pane_x, tiles):
            b += ui.text(px, ty + wh + 34, nm, "label", ui.NAVY)
        return b

    presets = [
        ("Default", None, "Arm 6 · gap 4"),
        ("Minimal dot", dict(XH_DEFAULT, style="dot", dot=3), "Dot only · 3 px"),
        ("Heavy outline", dict(XH_DEFAULT, arm=7, oth=2), "Outline 2 px"),
        ("Wide gap", dict(XH_DEFAULT, style="cross", arm=8, gap=10), "No dot · gap 10"),
    ]
    py, ph = 560, 96

    def preset_cards():
        b = subhead(PX0, 512, PXW, "SAVED PRESETS", note="Pick one, then Apply")

        def fill(i, px, py_):
            nm, p, spec = presets[i]
            cx, cy = px + tw_ // 2, py_ + ph // 2
            return (ui.backdrop("office_sun", px, py_, tw_, ph, crop=(1100, 640, tw_, ph), name=f"Thumb-{i}") +
                    (ui.crosshair(cx, cy, name="Thumb-default") if p is None else xhair(cx, cy, p)))
        b += window_panes(PX0, py, tw_, ph, 4, fr, fill, name="PresetWindow")[0]
        # the preset in use: a 2 px navy ring set into the frame, 5 px outside its pane
        b += ui.rect(pane_x[0] - 5, py + fr - 5, tw_ + 10, ph + 10, None, ui.NAVY, 2)
        return b

    def preset_labels():
        b = ""
        ly = py + ph + 2 * fr + 24
        for px, (nm, p, spec) in zip(pane_x, presets):
            b += ui.text(px, ly, nm, "label", ui.NAVY)
            if p is None:
                b += ui.rect(px + tw(nm, "label") + 10, ly - 12, 8, 8, ui.COBALT)
                b += ui.text(px + tw(nm, "label") + 24, ly, "In use", "label", ui.INK2, weight=400)
            b += ui.text(px, ly + 22, spec, "label", ui.INK2, weight=400)
        return b

    bd.file("Preview", ("Preview", preview), ("TileLabels", tile_labels))
    bd.file("Presets", ("PresetCards", preset_cards), ("PresetLabels", preset_labels))
    bd.file("Footer", ("Footer", lambda: footer(None, extra_buttons=[("Revert", "disabled")])),
            ("KeyHints", key_hints))

    elec = xh_best(ui.ELECTRIC, ui.COBALT)
    cyan = xh_best(ui.CYAN, ui.COBALT)
    elen = xh_best(ui.ELECTRIC, ui.NAVY)
    pw, pwb = xh_best(ui.PAPER, ui.COBALT), xh_best(ui.PAPER, ui.BLOOM)
    hold = {}
    notes(bd, hold, [
        "All rows need new code. Square styles only (ux_research §3): Cross, Dot, Cross + Dot, Brackets.",
        f"Colours: Paper White, Cyan, Electric Blue; never Amber (rival, threat). "
        f"Token proofs, best of core and edge: Paper White vs Cobalt {pw:.2f}, vs Bloom {pwb:.2f}; Cyan vs Cobalt "
        f"{cyan:.2f}; Electric Blue vs Cobalt {elec:.2f}, vs Navy {elen:.2f}: flagged.",
        "No scoped weapon in the owner's list, so no hide-when-scoped row.",
        "World stand-ins in one window frame; sun bloom is white on purpose. Crosshair 1:1, physical pixels.",
    ], x=PX0, y=748, w=PXW, max_y=952)
    return bd


# --------------------------------------------------------------------------- B6 key bindings

KB_W = 168
KCA, KCB, KCW = 96, 984, 840


def kb_row(x, y, w, label, key, pad, kstate="default", pstate="default", state="default", desc=None,
           note=None):
    """Key-binding row: label, keyboard cell and controller cell in fixed columns (168 px each)."""
    lit = state in ("listening", "conflict")
    b = lit_band(x, y, w) if lit else ""
    b += ui.text(x + 16, y + ROW_H / 2 + 7, label, "item", ui.NAVY)
    px = x + w - 12 - KB_W
    kx = px - 16 - KB_W
    if note:
        b += ui.text_right(x + w - 12, y + ROW_H / 2 + 6, note, "body", ui.INK2)
    else:
        b += ui.keybind(kx, y + 8, key, kstate, KB_W)
        if pad is None:   # no binding yet: the guide's outlined empty slot, not a blank keycap
            b += ui.outlined_slot(px, y + 8, KB_W, ui.SIZE["keycap"], name=ui.uid("UnsetSlot"))
            b += ui.text(px + KB_W / 2, y + 8 + 30, "Not set", "label", ui.INK2, weight=400, anchor="middle")
        else:
            b += ui.keybind(px, y + 8, pad, pstate, KB_W)
    b += ui.line(x, y + ROW_H - 0.5, x + w, y + ROW_H - 0.5, ui.HAIR)
    if desc and lit:
        dx = x + 16
        if state == "conflict":
            b += conflict_tile(dx, y + ROW_H + 9)
            dx += 28
        b += ui.text(dx, y + ROW_H + 26, desc, "body", ui.INK2)
    return b


def kb_subhead(x, y, w, label, cols=True):
    b = subhead(x, y, w, label)
    if cols:
        px = x + w - 12 - KB_W
        b += ui.text(px, y + 24, "Controller", "label", ui.INK2, weight=400)
        b += ui.text(px - 16 - KB_W, y + 24, "Keyboard, mouse", "label", ui.INK2, weight=400)
    return b


def keybind_screen(bd, interact_state="listening", trace=True):
    bd.file("Header", ("Header", lambda: header("Controls · Key bindings")), ("Micro", screen_micro))
    bd.file("TabBar", ("TabBar", lambda: ui.tab_bar(FX, TAB_Y, FW, TABS, active=0)))

    def sec():
        return ui.settings_row(KCA, SEC_Y, 1728, "header", "§ 1.7  KEY BINDINGS")

    move = [("Move", "W A S D", "L stick", "default", "default"),
            ("Look", "Mouse", "R stick", "disabled", "disabled"),
            ("Sprint", "Left Shift", "LS press", "default", "default"),
            ("Jump", "Space", "A", "default", "default"),
            ("Lean left (hold)", "Q", "LB", "default", "default"),
            ("Lean right (hold)", "E", "RB", "default", "default")]

    def col_a():
        y = ROW0
        b = kb_subhead(KCA, y, KCW, "MOVEMENT")
        y += 40
        for lab, k, p, ks, ps in move:
            b += kb_row(KCA, y, KCW, lab, k, p, ks, ps)
            y += ROW_H
        b += kb_row(KCA, y, KCW, "Climb", None, None, note="Jump at a ledge and hold forward")
        return b

    def col_b():
        y = ROW0
        b = kb_subhead(KCB, y, KCW, "ACTIONS")
        y += 40
        b += kb_row(KCB, y, KCW, "Attack", "Left mouse", "X")
        y += ROW_H
        if interact_state == "listening":
            m = kb_row(KCB, y, KCW, "Interact", "F", "Y", "listening", "default", "listening",
                       desc="Press a key for Interact. Esc cancels.")
            m += focus_chevrons(KCB, y)
        else:   # the player pressed G while Interact was listening: G is Drop's key
            m = kb_row(KCB, y, KCW, "Interact", "G", "Y", "conflict", "default", "conflict",
                       desc="G is already used for Drop.")
        b += m
        if trace:
            kx = KCB + KCW - 12 - KB_W - 16 - KB_W
            b += ui.trace_brackets(kx - 6, y + 2, KB_W + 12, ROW_H - 4, ui.NAVY, seed=8, density=0.5)
        y += ROW_H + DESC_H
        b += kb_row(KCB, y, KCW, "Crouch", "C", "B")
        y += ROW_H
        b += kb_subhead(KCB, y, KCW, "ITEMS", cols=False)
        y += 40
        b += kb_row(KCB, y, KCW, "Previous", "1", "D-pad left")
        y += ROW_H
        b += kb_row(KCB, y, KCW, "Next", "2", "D-pad right")
        y += ROW_H
        b += kb_row(KCB, y, KCW, "Drop", "G", None)
        y += ROW_H
        b += kb_subhead(KCB, y, KCW, "MENU", cols=False)
        y += 40
        b += kb_row(KCB, y, KCW, "Menu", "Esc", "Start", "disabled", "default")
        return b

    bd.file("Bindings", ("Section", sec), ("Movement", col_a), ("Actions", col_b))
    bd.file("Footer", ("Footer", lambda: footer(None)), ("KeyHints", key_hints))


def board_b6():
    bd = Board("b6_keybinds", "KEY BINDINGS")
    bd.file("Frame", ("Frame", frame, "ground"))
    keybind_screen(bd, "listening")
    notes(bd, {}, [
        "All rows need new code: the Input System can rebind, but nothing saves a binding yet.",
        "Defaults (owner, 2026-11-12): Interact F (pad Y), Lean Q / E held, Drop G. Prompts read \"F · Pick up\".",
        "Drop has no pad binding yet: drawn as an outlined empty slot (\"Not set\"), the look for any unbound "
        "cell. Owner call. Esc and Look are fixed (dashed). Menu pad Start is a proposal (new code).",
    ], x=KCA, y=804, w=KCW, max_y=952)
    return bd


# --------------------------------------------------------------------------- modals

MODAL_FRAME = 16   # the clear-glass frame around a dialog's frosted plate


def modal(x, y, w, title, body, safe, risky, esc_label, safe_w=180, h=204, extra=None, name=None, under=None):
    """Local confirm (the kit's modal_confirm labels Esc with the safe button; here Esc can say what it does,
    e.g. "Keep editing"). Light glass only: a frosted plate (navy text) set in a 16 px clear-glass frame (the
    guide's window pane: see-through, carries no text). The safe choice is primary and holds focus."""
    f = MODAL_FRAME
    b = ui.glass_pane(x - f, y - f, w + 2 * f, h + 2 * f, "clear", under=under, streak=False,
                      name=ui.uid("ModalFrame"))
    b += ui.glass_pane(x, y, w, h, "frosted", streak=True, streak_at=0.86, under=under, shadow=False,
                       name=ui.uid("ModalGlass"))
    b += ui.rect(x, y, w, 6, ui.COBALT)
    b += ui.text(x + 32, y + 58, title, "item", ui.NAVY, size=26)
    b += ui.text(x + 32, y + 92, body, "body", ui.INK2)
    if extra:
        b += extra(x, y)
    rw = int(tw(risky) + 48)
    by = y + h - 32 - 56
    b += ui.button(x + w - 32 - safe_w, by, safe_w, safe, "primary", "focus", h=56, name=ui.uid("ModalSafe"))
    b += ui.button(x + w - 32 - safe_w - 24 - rw, by, rw, risky, "secondary", "default", name=ui.uid("ModalRisky"))
    cap, kw_ = ui.keycap(x + 32, by + 12, "Esc", False, 16, 32)
    b += cap + ui.text(x + 32 + kw_ + 10, by + 34, esc_label, "label", ui.INK2)
    return ui.group(name or ui.uid("Modal"), b)


def scrim():
    """The modal backing: a paper wash with a silver tint (light, no dark fill) that drops the screen behind
    to a pale ghost so the dialog is the only thing at full contrast."""
    return ui.rect(0, 0, W, H, (ui.PAPER, 0.62)) + ui.rect(0, 0, W, H, (ui.SILVER, 0.22))


def modal_file(bd, fn, mx, my, mw, mh):
    """Scrim + dialog. The glass blurs (frosted) or shows (clear frame) a copy of the screen under it, as the
    engine would. The board's one trace is the snapped bracket set on the dialog's frame (no squares: a dialog
    is all text)."""
    under = "".join(m for _, layer, m in bd.files if layer == "screen") + scrim()
    f = MODAL_FRAME

    def m():
        return fn(under) + ui.trace_brackets(mx - f, my - f, mw + 2 * f, mh + 2 * f, ui.NAVY, squares=False)
    bd.file("Modal", ("Scrim", scrim, "ground"), ("Dialog", m), layer="modal")


def board_b7():
    bd = Board("b7_keyconflict", "KEY-BIND CONFLICT")
    bd.file("Frame", ("Frame", frame, "ground"))
    keybind_screen(bd, "conflict", trace=False)
    mw, mh = 720, 204
    mx, my = (W - mw) // 2, 744
    modal_file(bd, lambda u: modal(mx, my, mw, "G is already used for Drop.",
                                   "Assigning it here clears it there.", "Cancel", "Assign", "Cancel", 170, under=u),
               mx, my, mw, mh)
    notes(bd, {}, [
        "Interact was listening and the player pressed G, Drop's key. Cancel (safe) holds focus; Esc cancels "
        "and Interact keeps F; Assign moves G to Interact and leaves Drop's keyboard cell as an outlined "
        "\"Not set\" slot.",
    ], x=KCA, y=796, w=480, max_y=952)
    return bd


def board_b8():
    bd = Board("b8_unsaved", "UNSAVED CHANGES")
    bd.file("Frame", ("Frame", frame, "ground"))
    rows = [dict(r) for r in CONTROLS]
    rows[1] = dict(rows[1], value=True, dirty="Off")
    tab_screen(bd, 0, rows, -1, dirty_msg="1 change not applied: Invert vertical look.", trace=False)
    mw, mh = 720, 204
    mx, my = (W - mw) // 2, 480
    modal_file(bd, lambda u: modal(mx, my, mw, "Apply changes?", "1 setting has changed: Invert vertical look.",
                                   "Apply", "Discard", "Keep editing", 170, under=u), mx, my, mw, mh)
    notes(bd, {}, [
        "Opened by Back (or Esc) with an unapplied change. Apply is the safe choice and holds focus; Discard "
        "never takes focus; Esc returns to the screen (\"Keep editing\", my copy).",
        "Proposed kit change: modal_confirm with its own Esc label.",
    ], y=700)
    return bd


def board_b9():
    bd = Board("b9_resolution", "RESOLUTION REVERT")
    bd.file("Frame", ("Frame", frame, "ground"))
    rows = [dict(r) for r in VIDEO]
    rows[1] = dict(rows[1], value="Full screen")
    rows[2] = dict(rows[2], value="1280 × 720", state=None)
    tab_screen(bd, 1, rows, -1, trace=False)   # no row focused (focus is in the dialog), so no clause card
    mw, mh = 720, 268
    mx, my = (W - mw) // 2, 560
    secs, total = 7, 10

    def countdown(x, y):
        b = ""
        seg = (mw - 64 - (total - 1) * 4) / total
        for i in range(total):   # seconds left: cobalt blocks; drained seconds: outlined empty slots
            sx = x + 32 + i * (seg + 4)
            if i < secs:
                b += ui.rect(round(sx, 1), y + 112, round(seg, 1), 10, ui.COBALT)
            else:
                b += ui.rect(round(sx, 1) + 0.5, y + 112.5, round(seg, 1) - 1, 9, None, ui.HAIR_STRONG, 1,
                             dash="4 3")
        b += ui.text(x + 32, y + 150, "Now 1280 × 720, full screen. Was 1920 × 1080, borderless window.",
                     "label", ui.INK2, weight=400)
        return b
    modal_file(bd, lambda u: modal(mx, my, mw, "Keep this resolution?", f"Reverting in {secs} seconds.", "Revert", "Keep",
                                   "Revert", 170, h=mh, extra=countdown, under=u), mx, my, mw, mh)
    notes(bd, {}, [
        "After Apply changes the display. Revert holds focus, and Esc or the timeout revert, so a blank or "
        "unreadable screen undoes itself. The bar drains one segment per second.",
        "Timer is 10 s (inventory §4.1); ux_research §2 suggests 15 s. Owner call.",
    ], x=1344, y=584, w=480)
    return bd


# --------------------------------------------------------------------------- main

def write_board(bd):
    folder = os.path.join(OUT, bd.key)
    paths, clean = [], []
    order = ([f for f in bd.files if f[0] not in ("SheetLabel", "DesignNotes")])
    frame_f = [f for f in order if f[0] == "Frame"]
    rest = [f for f in order if f[0] != "Frame"]
    sheet = [f for f in bd.files if f[0] == "SheetLabel"]
    dn = [f for f in bd.files if f[0] == "DesignNotes"]
    seq = [(0, frame_f[0]), (1, sheet[0])] + [(i + 2, f) for i, f in enumerate(rest)] + [(99, dn[0])]
    for num, (name, layer, markup) in seq:
        p = ui.write(os.path.join(folder, f"{bd.key}_{num:02d}_{name}.svg"), markup)
        paths.append(p)
        if layer != "notes":
            clean.append(p)
    return paths, clean


BANNED_COPY = (   # the old game name, E as Interact, the settled E-vs-F question, three-dot ellipses
    r"close\s+the\s+deal", r"E ·(Pick up|Open|Interact)", r"E is already used", r"E vs F", r"decision owed",
    r"\.\.\.")


def static_checks(all_paths):
    errs = []
    with open(os.path.abspath(__file__), encoding="utf-8") as fh:
        src = fh.read()
    for m in re.finditer(r"#[0-9A-Fa-f]{6}\b|#[0-9A-Fa-f]{3}\b", src):
        errs.append(f"hex literal in the generator: {m.group(0)}")
    for p in all_paths:
        with open(p, encoding="utf-8") as fh:
            s = fh.read()
        if re.search(r"<circle|<ellipse|\brx=", s):
            errs.append(f"rounded shape in {os.path.basename(p)}")
        for bad in BANNED_COPY:
            if re.search(bad, s, re.I):
                errs.append(f"banned copy /{bad}/ in {os.path.basename(p)}")
        for fam in set(re.findall(r'font-family="([^"]+)"', s)):
            if fam not in FAMILIES:
                errs.append(f"font {fam} in {os.path.basename(p)}")
    return errs


def main():
    preview = "--no-preview" not in sys.argv
    for d in glob.glob(os.path.join(OUT, "b[0-9]_*")):
        shutil.rmtree(d)
    failed = False
    boards = [board_b1(), board_b2(), board_b3(), board_b4(), board_b5(), board_b6(), board_b7(), board_b8(),
              board_b9()]
    all_paths = []
    for bd in boards:
        paths, clean = write_board(bd)
        all_paths += paths
        print(f"\n{bd.key}: {len(paths)} files (bottom to top)")
        for p in paths:
            print("  wrote", os.path.relpath(p))
        errs = bd.check()
        for e in errs:
            print("  CHECK FAIL", e)
        failed |= bool(errs)
        if not errs:
            print("  checks: no overlaps, labels clear of controls, inside the margin, one trace, notes fit")
        if preview:
            png = ui.preview_many(clean, os.path.join(PREVIEW, f"settings_{bd.key}.png"))
            png2 = ui.preview_many(paths, os.path.join(PREVIEW, f"settings_{bd.key}_annotated.png"))
            print("  preview", os.path.relpath(png), "and", os.path.basename(png2))
    st = static_checks(all_paths)
    for e in st:
        print("CHECK FAIL", e)
    failed |= bool(st)
    if ui.MISSING_GLYPHS:
        failed = True
        print("\nMISSING GLYPHS:", sorted(ui.MISSING_GLYPHS))
    print(f"\nText metrics: {ui.METRICS_SOURCE}")
    print("\nCrosshair colour vs background (best of core and Near Black edge):")
    for nm, col in XH_COLOURS.items():
        vals = [(bn, xh_best(col, bg)) for bn, bg in (("bloom", ui.BLOOM), ("paper", ui.PAPER),
                                                      ("cobalt", ui.COBALT), ("navy", ui.NAVY))]
        print(f"  {nm:14s}", "  ".join(f"{bn} {v:5.2f}{'*' if v < 4.5 else ''}" for bn, v in vals))
    print("  (* under 4.5 : 1, flagged \"faint on blue sky\")")
    print("\nstatic checks:", "ok" if not st else "FAIL")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
