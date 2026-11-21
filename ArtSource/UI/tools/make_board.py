"""Seal the Deal: the UI board, one FigCraft canvas with a section per screen and every version side by side.

Adam's workflow from 2026-11-15, after the Cocoon (AYN Thor) UI designer's: nothing is overwritten. Each new
version of a screen is added to the right of the last in its section, the in-game captures sit in their own
section per screen, and references sit on the same canvas.

Run (any Python 3):  python3 make_board.py
Writes ArtSource/UI/board/SealTheDeal_UI.figcraft with figcraft-cli (~/.local/bin/figcraft-cli). Two passes,
because the CLI's scripts can't refer to ids made earlier in the same script: sections first, then the
images and captions placed into them.
"""

from __future__ import annotations

import json
import os
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
UI = HERE.parent
REVIEW = UI / "review"
REFS = UI.parent / "References"
BOARD = UI / "board" / "SealTheDeal_UI.figcraft"
CLI = os.path.expanduser("~/.local/bin/figcraft-cli")

ROW_H = 270          # every image is shown at this height (1080p screens at a quarter)
GAP = 40
PAD = 60
CAPTION = 34         # space under each image for its caption
SECTION_GAP = 200

# Sections, top to bottom. Each entry is (caption, path relative to ArtSource/UI/review or ArtSource/References).
SECTIONS = [
    ("References: UI", [(p.stem.replace("_", " "), p) for p in sorted((REFS / "ui").glob("[01][0-9]_*")) if p.suffix in (".png", ".webp", ".jpg")]),
    ("References: sky (Alex)", [(p.stem.replace("_", " "), p) for p in sorted((REFS / "sky_and_clouds").glob("0[34]_*"))]),
    ("Fonts", ["16_font_specimen.png", "18_font_candidates.png", "21b_title_display_faces.png"]),
    ("Title: in game", ["14_title_frame_after_1080.png", "25_title_font_swap_ingame.jpg", "29_title_unity_step1_subtract.jpg"]),
    ("Title reset: the references it follows (08, 02, 05)", [(p.stem.replace("_", " "), p) for p in sorted((REFS / "ui").glob("0[825]_*"))]),
    ("Title: mockups, oldest left", ["17_title_v2_mockup.png", "17_title_v2_mockup_glass.png", "17_title_v2_mockup_soft.png",
                                     "19_title_v3_white.png", "20_title_v4.png", "20b_title_v4_first_run.png",
                                     "21_title_v4_font_picks.png", "27_title_v5.png", "27a_title_v5_first_run.png",
                                     "28e_title_v5_icon_fixes.png"]),
    ("Title: v6 directions", ["28a_title_v6_signed_and_sealed.png", "28b_title_v6_scale.png", "28c_title_v6_tower.png"]),
    ("Title: motion (frame strips; clips beside them in review/)", ["22_title_motion_frames.png", "22_title_motion_live_sky_frames.png",
                                                                  "23_title_motion_card_hover_frames.png"]),
    ("Esc menu: mockups, oldest left", ["24_esc_menu_v1.png", "24b_esc_menu_v1_match_running.png", "24c_esc_menu_v1_alone.png",
                                        "25_esc_menu_v2.png", "25b_esc_menu_v2_match_running.png", "25c_esc_menu_v2_alone.png",
                                        "26_esc_menu_motion_frames.png", "27b_esc_menu_v5.png"]),
    ("HUD: in game", ["keyframes/hud_01_idle_1080p.png", "keyframes/hud_02_hit_marker_1080p.png",
                      "11_match_host_running_marker_1080.png", "11_match_host_floor_opened_1080.png", "11_match_host_result_host_1080.png"]),
    ("Settings: in game", ["12_settings_video_1080.png", "12_settings_audio_1080.png", "12_settings_controls_1080.png",
                           "12_settings_accessibility_1080.png", "11_settings_controls_conflict_1080.png"]),
    ("Joining and problems: in game", ["13_session_joining_1080.png", "13_problem_unreachable_1080.png", "11_title_quit_confirm_1080.png"]),
    ("Icons", ["27c_ui_icons.png", "28d_ui_icons_v2.png"]),
]


def size(path: Path) -> tuple[int, int]:
    """Width and height of a PNG, JPEG or WebP without extra libraries."""
    data = path.read_bytes()[:65536]
    if data[:8] == b"\x89PNG\r\n\x1a\n":
        return struct.unpack(">II", data[16:24])
    if data[:4] == b"RIFF" and data[8:12] == b"WEBP":
        chunk = data[12:16]
        if chunk == b"VP8X":
            return 1 + int.from_bytes(data[24:27], "little"), 1 + int.from_bytes(data[27:30], "little")
        if chunk == b"VP8 ":
            w, h = struct.unpack("<HH", data[26:30])
            return w & 0x3FFF, h & 0x3FFF
        if chunk == b"VP8L":
            b = data[21:25]
            return 1 + (((b[1] & 0x3F) << 8) | b[0]), 1 + (((b[3] & 0xF) << 10) | (b[2] << 2) | ((b[1] & 0xC0) >> 6))
    if data[:2] == b"\xff\xd8":
        i = 2
        while i < len(data):
            marker, length = data[i + 1], struct.unpack(">H", data[i + 2:i + 4])[0]
            if 0xC0 <= marker <= 0xCF and marker not in (0xC4, 0xC8, 0xCC):
                h, w = struct.unpack(">HH", data[i + 5:i + 9])
                return w, h
            i += 2 + length
    raise ValueError(f"unknown image type: {path}")


def resolve(entry):
    if isinstance(entry, tuple):
        return entry
    return entry.rsplit(".", 1)[0].replace("_", " "), REVIEW / entry


def layout():
    """Sections with their images and positions; missing files are skipped (a version not made yet)."""
    y = 0
    plan = []
    for name, entries in SECTIONS:
        items, x = [], PAD
        for entry in entries:
            caption, path = resolve(entry)
            if not path.exists():
                continue
            w, h = size(path)
            dw = round(w * ROW_H / h)
            items.append({"caption": caption, "path": str(path), "x": x, "w": dw})
            x += dw + GAP
        width = max(x - GAP + PAD, 900)
        height = PAD + ROW_H + CAPTION + PAD
        plan.append({"name": name, "x": 0, "y": y, "w": width, "h": height, "items": items})
        y += height + SECTION_GAP
    return plan


def run(script: list, out: Path, base: Path | None = None) -> list:
    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False, dir=out.parent) as f:
        json.dump(script, f)
        script_path = f.name
    cmd = [CLI, "run", script_path, "--out", str(out),
           "--automation-read-root", str(UI.parent), "--automation-write-root", str(out.parent)]
    if base is not None:
        cmd[3:3] = ["--in", str(base)]
    res = subprocess.run(cmd, capture_output=True, text=True)
    os.unlink(script_path)
    if res.returncode != 0:
        sys.exit(res.stdout + res.stderr)
    return [json.loads(line) for line in res.stdout.splitlines() if line.startswith("{")]


def main():
    plan = layout()
    BOARD.parent.mkdir(parents=True, exist_ok=True)

    # Pass 1: the sections.
    sections = [{"command": "node.create", "params": {"type": "section", "x": s["x"], "y": s["y"], "width": s["w"],
                                                      "height": s["h"], "name": s["name"], "select": False}}
                for s in plan]
    results = run(sections, BOARD)
    ids = [r.get("result", {}).get("id") for r in results]
    if len(ids) != len(plan) or not all(ids):
        sys.exit(f"could not read section ids: {results[:2]}")

    # Pass 2: images and captions, inside their sections.
    steps = []
    for sid, s in zip(ids, plan):
        for it in s["items"]:
            steps.append({"command": "image.place", "params": {"path": it["path"], "x": it["x"], "y": PAD, "width": it["w"],
                                                               "height": ROW_H, "parent": sid, "name": it["caption"], "mode": "fit"}})
            steps.append({"command": "text.create", "params": {"text": it["caption"], "x": it["x"], "y": PAD + ROW_H + 10,
                                                               "parent": sid, "select": False,
                                                               "props": {"fontFamily": "Archivo SemiExpanded", "fontSize": 16,
                                                                         "fill": "#434B61"}}})
    # Section titles as text too: a section's own label shows in the app but not in renders or exports.
    for s in plan:
        steps.append({"command": "text.create", "params": {"text": s["name"], "x": s["x"], "y": s["y"] - 56, "select": False,
                                                           "props": {"fontFamily": "Archivo SemiExpanded Medium", "fontSize": 32,
                                                                     "fill": "#111B38"}}})
    run(steps, BOARD, base=BOARD)
    placed = sum(len(s["items"]) for s in plan)
    print(f"wrote {BOARD} : {len(plan)} sections, {placed} images")


if __name__ == "__main__":
    main()
