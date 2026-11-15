"""Seal the Deal: the pass comparison sheet A | B | C | D, and each pass's four comparison crops.

    # 1. a pass renders its four crops (any theme; lanes run this for their own theme):
    python3 ArtSource/UI/tools/make_compare.py --crops --theme ArtSource/UI/passes/b_aero/theme_aero.py \
        --out ArtSource/UI/passes/b_aero/compare
    # 2. assemble the sheet from whatever crops exist (a missing file shows "pending"):
    python3 ArtSource/UI/tools/make_compare.py

Crop spec (README "Comparison crops"), every file a PNG at exactly this size:
  title.png                 960 x 240   the pass's title lockup at size 72, left edge x 48, on its own field
  buttons.png              1440 x 160   primary button "Host game", 240 x its own height, five states left to
                                        right (default, hover, focus, pressed, disabled) at x 48 + i * 272,
                                        top y 40, state names under them (caption)
  briefcase_chip_bloom.png  960 x 240   sky_bloom backdrop crop (560, 300, 960, 240), briefcase_tracker
                                        "rival" (rival tower, floor 6) at (48, 32) and "you" (your tower,
                                        floor 7) at (48, 136)
  hero.png                  960 x 540   the pass's hero screen (make_hero) scaled to 50 %
The sheet goes to ArtSource/UI/review/00_compare_passes.png (SVG beside it in svg/compare/).
"""

from __future__ import annotations

import argparse
import base64
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ctd_ui as ui  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
UI_DIR = os.path.normpath(os.path.join(HERE, ".."))
PASSES = [("a_survey", "A · SURVEY"), ("b_aero", "B · AERO"), ("c_sky", "C · SKY"), ("d_seven", "D · SEVEN")]
CROPS = [("title.png", 960, 240, "Title lockup"), ("buttons.png", 1440, 160, "Primary button · 5 states"),
         ("briefcase_chip_bloom.png", 960, 240, "Briefcase tracker over sky bloom"),
         ("hero.png", 960, 540, "Hero · main menu, 50 %")]


def _render(svg_body, w, h, out_png):
    tmp = out_png[:-4] + ".svg"
    ui.write(tmp, svg_body, w, h)
    ui.preview(tmp, out_png=out_png)
    os.remove(tmp)
    return out_png


def make_crops(theme, out):
    name = ui.set_theme(theme)
    os.makedirs(out, exist_ok=True)
    field = ui.rect(0, 0, 2000, 2000, ui.PAPER)
    _render(field + ui.title_lockup(48, 64, size=72), 960, 240, os.path.join(out, "title.png"))
    b = field
    for i, st in enumerate(ui.STATES):
        b += ui.button(48 + i * 272, 40, 240, "Host game", "primary", st)
        b += ui.text(48 + i * 272, 140, st.capitalize(), "caption", ui.INK2, ls=1)
    _render(b, 1440, 160, os.path.join(out, "buttons.png"))
    b = ui.backdrop("sky_bloom", 0, 0, 960, 240, crop=(560, 300, 960, 240))
    b += ui.briefcase_tracker(48, 32, "rival", "rival", 6)
    b += ui.briefcase_tracker(48, 136, "you", "your", 7)
    _render(b, 960, 240, os.path.join(out, "briefcase_chip_bloom.png"))
    _render(ui.place(ui.hero_screen(), 0, 0, 0.5), 960, 540, os.path.join(out, "hero.png"))
    print(f"{name}: wrote 4 crops to {os.path.relpath(out)}")


def _img(path_, x, y, w, h):
    with open(path_, "rb") as fh:
        data = base64.b64encode(fh.read()).decode("ascii")
    return (f'<image x="{x}" y="{y}" width="{w}" height="{h}" preserveAspectRatio="xMinYMin meet" '
            f'href="data:image/png;base64,{data}"/>')


def make_sheet(out_png):
    ui.set_theme("survey")
    cw, gap, m, top = 720, 32, 64, 150
    rows = [(f, w, h, lab, h * min(cw / w, 0.75)) for f, w, h, lab in CROPS]
    total_w = m * 2 + 4 * cw + 3 * gap
    total_h = top + sum(r[4] + 48 for r in rows) + 40
    W, H = int(total_w), int(total_h)
    b = ui.rect(0, 0, W, H, ui.PAPER) + ui.hairline_grid(0, 0, W, H, faint=True)
    b += ui.text(m, 64, f"{ui.GAME_TITLE} · UI PASSES · SIDE BY SIDE", "caption", ui.NAVY, ls=2)
    b += ui.text(m, 92, "Same four items per pass, same crop. A missing crop shows as pending.", "body", ui.INK2)
    for j, (key, label) in enumerate(PASSES):
        x = m + j * (cw + gap)
        b += ui.square_bullet(x, top - 30, 9, ui.NAVY) + ui.text(x + 18, top - 21, label, "caption", ui.NAVY, ls=2)
        b += ui.line(x, top - 12.5, x + cw, top - 12.5, ui.NAVY)
        y = top
        for f, w, h, lab, dh in rows:
            dw = w * (dh / h)
            b += ui.text(x, y + 14, lab, "caption", ui.INK3, ls=0.5)
            y += 24
            p = os.path.join(UI_DIR, "passes", key, "compare", f)
            if os.path.isfile(p):
                b += _img(p, x, y, dw, dh)
            else:
                b += ui.rect(x + 0.5, y + 0.5, dw - 1, dh - 1, None, ui.HAIR_STRONG, 1, dash="6 4")
                b += ui.text(x + dw / 2, y + dh / 2 + 6, "pending", "label", ui.INK3, anchor="middle")
            y += dh + 24
    svg = os.path.join(UI_DIR, "svg", "compare", "compare_passes.svg")
    ui.write(svg, b, W, H)
    ui.preview(svg, out_png=out_png)
    print(f"sheet {W} x {H}: {os.path.relpath(out_png)}")


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--crops", action="store_true", help="render this theme's four crops instead of the sheet")
    ap.add_argument("--theme", default="survey")
    ap.add_argument("--out", help="crops folder (default passes/a_survey/compare for survey)")
    ap.add_argument("--sheet", default=os.path.join(UI_DIR, "review", "00_compare_passes.png"))
    a = ap.parse_args(argv)
    if a.crops:
        make_crops(a.theme, a.out or os.path.join(UI_DIR, "passes", "a_survey", "compare"))
    else:
        make_sheet(a.sheet)
    return 0


if __name__ == "__main__":
    sys.exit(main())
