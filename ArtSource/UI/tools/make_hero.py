"""Seal the Deal: render the main-menu hero (title screen over the scene) for a theme.

    python3 ArtSource/UI/tools/make_hero.py                                   # pass A (survey)
    python3 ArtSource/UI/tools/make_hero.py --theme ArtSource/UI/passes/b_aero/theme_aero.py \
        --out ArtSource/UI/passes/b_aero/svg

Writes <out>/<theme>_hero.svg (1920 x 1080, one group "HeroScreen") and <preview-dir>/<theme>_hero.png, and runs
the kit's layout checks on it. The composition is ui.hero_screen(); a theme replaces it with
OVERRIDES["hero_screen"]. Exit code 1 if a check fails.
"""

from __future__ import annotations

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ctd_ui as ui  # noqa: E402
import make_kit  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))


def main(argv=None):
    ap = argparse.ArgumentParser(description="Render the main-menu hero for a theme.")
    ap.add_argument("--theme", default="survey")
    ap.add_argument("--out", default=os.path.join(HERE, "..", "svg", "hero"))
    ap.add_argument("--preview-dir", default=os.path.join(HERE, "..", "svg", "_preview"))
    ap.add_argument("--no-preview", action="store_true")
    a = ap.parse_args(argv)
    name = ui.set_theme(a.theme)
    bd = make_kit.Board("hero", "HERO")
    bd.file("HeroScreen", ("HeroScreen", lambda: ui.hero_screen()))
    path_ = ui.write(os.path.join(os.path.abspath(a.out), f"{name}_hero.svg"), bd.files[0][1])
    print(f"theme {name} ({ui.PASS_NAME}): wrote {os.path.relpath(path_)}")
    errs = [e for e in bd.check() if not e.startswith("outside the margin: HeroScreen")]  # full-bleed by design
    for e in errs:
        print("  CHECK FAIL", e)
    if ui.MISSING_GLYPHS:
        errs.append("missing glyphs")
        print("  MISSING GLYPHS:", sorted(ui.MISSING_GLYPHS))
    if not a.no_preview:
        png = ui.preview(path_, out_png=os.path.join(os.path.abspath(a.preview_dir), f"{name}_hero.png"))
        print("  preview", os.path.relpath(png), "%d x %d" % ui._png_size(png))
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main())
