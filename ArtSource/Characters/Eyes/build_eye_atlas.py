# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-13
#  Prompt    : Alex supplied a generated 4x4 eye sheet ("here you go") for the STDMC eye cards.
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Turns Alex's painted eye sheet (pupils drawn in, 4 blink columns) into the three
#              textures the eye-card shader reads: lids, iris-opening mask and one sliding iris.
#              It is a script, not a hand edit, so a repainted sheet can be rebuilt the same way.
# -----------------------------------------------------------------------------
"""Build the STDMC eye-card textures from STDMC_EyeSheet.png.

Usage (from the repo root):  python ArtSource/Characters/Eyes/build_eye_atlas.py [--preview out.png]

Source sheet contract: a transparent PNG, 4 x 4 equal cells. Rows top to bottom are the moods
bored, wince, wide, focused. Columns are open, half, nearly closed, closed. Each cell holds the
viewer-left eye in its left half and the viewer-right eye in its right half. Pupils are drawn in.

Outputs (the slots on STDMC_Eyes.mat; layout in docs/systems/CHARACTER_EYES.md):
  Placeholder_Lids.png      lids, brows and whites, flat colours, hard alpha
  Placeholder_Openings.png  white where the sliding iris may show
  Placeholder_Iris.png      one pupil, at the same canvas point as every frame's pupil
"""
import argparse
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
SHEET = os.path.join(HERE, "STDMC_EyeSheet.png")
OUT = os.path.join(REPO, "Assets", "_Project", "Art", "Characters", "Eyes")

SHEET_GRID = 4
# WHY: the shader's atlas has three blink frames (open, half, closed); the sheet has four.
# The "nearly closed" column is dropped because a 0.16 s blink cannot show four frames.
SOURCE_COLUMNS = (0, 1, 3)
MOODS = 4
# Texels per eye canvas. WHY: twice the 128 in the original contract, so the line art stays
# crisp in close-ups with point filtering; the shader only reads normalised UVs.
CANVAS = 256
# Clear border around the art on each eye canvas (8 px at 128, scaled with it).
MARGIN = 16
# Card footprint in millimetres, from CharacterEyeSetup.cs (Width 0.052, Height 0.040).
CARD_W_MM, CARD_H_MM = 52.0, 40.0

# Source alpha below this is the generator's faint noise, not art.
ALPHA_CUT = 128
# Luminance splitting navy line art (~40) from the whites (~235).
DARK_CUT = 140
# Flat colours sampled from the sheet's most common line and sclera pixels.
LINE_RGB = (28, 36, 58)
SCLERA_RGB = (234, 238, 243)
# WHY: the sheet's pupils are navy, but Alex's character concept gives STDMC green irises.
# The iris is a flat green disc with a navy pupil at this share of its radius.
IRIS_RGB = (40, 158, 86)
PUPIL_SHARE = 0.5


def classify(sheet):
    """Per-pixel label maps for the whole sheet: opaque, dark (line or pupil), light (white)."""
    w, h = sheet.size
    px = sheet.load()
    dark = bytearray(w * h)
    light = bytearray(w * h)
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < ALPHA_CUT:
                continue
            if (r + g + b) / 3 < DARK_CUT:
                dark[y * w + x] = 1
            else:
                light[y * w + x] = 1
    return dark, light


def find_pupil(dark, light, w, box):
    """Dark runs bounded by white on both sides of the same row are pupil, not line.

    Returns (pixel set, centre x, centre y, radius) in sheet pixels, or None when no
    pupil shows (closed frames).
    """
    x0, y0, x1, y1 = box
    pixels = set()
    rows = []  # (y, run start, run end)
    for y in range(y0, y1):
        x = x0
        while x < x1:
            if dark[y * w + x]:
                start = x
                while x < x1 and dark[y * w + x]:
                    x += 1
                end = x - 1
                if start > x0 and x < x1 and light[y * w + start - 1] and light[y * w + x]:
                    rows.append((y, start, end))
                    pixels.update((xx, y) for xx in range(start, end + 1))
            else:
                x += 1
    if not rows:
        return None
    widest = max(e - s + 1 for _, s, e in rows)
    # WHY: the widest rows straddle the circle's centre; rows cut by the lid are narrower.
    centre_rows = [(y, s, e) for y, s, e in rows if e - s + 1 >= widest - 1]
    cy = sum(y for y, _, _ in centre_rows) / len(centre_rows) + 0.5
    cx = sum((s + e + 1) / 2 for _, s, e in centre_rows) / len(centre_rows)
    return pixels, cx, cy, widest / 2


def mask_image(box, predicate):
    """An L image of one half-cell: 255 where predicate(x, y) holds, else 0."""
    x0, y0, x1, y1 = box
    img = Image.new("L", (x1 - x0, y1 - y0), 0)
    p = img.load()
    for y in range(y0, y1):
        for x in range(x0, x1):
            if predicate(x, y):
                p[x - x0, y - y0] = 255
    return img


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", help="also write a check image here, at the card's true proportions")
    parser.add_argument("--out", default=OUT, help="folder for the three textures (default: the Unity Eyes folder)")
    args = parser.parse_args()

    sheet = Image.open(SHEET).convert("RGBA")
    w, h = sheet.size
    cell_w, cell_h = w / SHEET_GRID, h / SHEET_GRID
    dark, light = classify(sheet)

    def half_box(row, col, side):
        x0 = round(col * cell_w + side * cell_w / 2)
        x1 = round(col * cell_w + (side + 1) * cell_w / 2)
        return x0, round(row * cell_h), x1, round((row + 1) * cell_h)

    # 1. Each mood and side is aligned on the pupil of its open frame, so blink frames line up
    #    and every frame's pupil sits on the same canvas point as the shared iris sprite.
    anchors, frames = {}, {}
    half_w = up = down = 0.0
    radii = []
    for row in range(MOODS):
        for side in (0, 1):
            open_box = half_box(row, SOURCE_COLUMNS[0], side)
            found = find_pupil(dark, light, w, open_box)
            if found is None:
                raise SystemExit(f"No pupil found in the open frame of row {row + 1}, side {side}.")
            _, cx, cy, radius = found
            # Relative to the half-cell, so the same point can be found in every column's cell.
            anchors[row, side] = (cx - open_box[0], cy - open_box[1])
            if row == 0:
                radii.append(radius)
            for col in SOURCE_COLUMNS:
                box = half_box(row, col, side)
                pupil = find_pupil(dark, light, w, box)
                frames[row, col, side] = (box, pupil[0] if pupil else set())
                x0, y0, x1, y1 = box
                ax, ay = x0 + anchors[row, side][0], y0 + anchors[row, side][1]
                opaque = [(x, y) for y in range(y0, y1) for x in range(x0, x1)
                          if dark[y * w + x] or light[y * w + x]]
                xs = [x for x, _ in opaque]
                ys = [y for _, y in opaque]
                # WHY: the eyes mirror each other, so one half-width keeps the iris centred on both canvases.
                half_w = max(half_w, ax - min(xs), max(xs) + 1 - ax)
                up = max(up, ay - min(ys))
                down = max(down, max(ys) + 1 - ay)

    # 2. One scale for every frame. A sheet pixel is square; a canvas texel is not, because the
    #    canvas is square but the card is 52 x 40 mm. So vertical texels per pixel = 1.3 x horizontal.
    aspect = CARD_W_MM / CARD_H_MM
    room = CANVAS - 2 * MARGIN
    kx = min(room / (2 * half_w), room / (aspect * (up + down)))
    ky = kx * aspect
    canvas_cx = CANVAS / 2
    canvas_cy = MARGIN + ky * up + (room - ky * (up + down)) / 2

    atlas_w, atlas_h = CANVAS * 2 * len(SOURCE_COLUMNS), CANVAS * MOODS
    lids = Image.new("RGBA", (atlas_w, atlas_h), (0, 0, 0, 0))
    openings = Image.new("L", (atlas_w, atlas_h), 0)

    for (row, col, side), (box, pupil) in frames.items():
        opaque = mask_image(box, lambda x, y: dark[y * w + x] or light[y * w + x])
        line = mask_image(box, lambda x, y: dark[y * w + x] and (x, y) not in pupil)
        opening = mask_image(box, lambda x, y: light[y * w + x] or (x, y) in pupil)

        rx, ry = anchors[row, side]
        # Output texel (u, v) reads half-cell pixel anchor + ((u - canvas_cx) / kx, (v - canvas_cy) / ky).
        affine = (1 / kx, 0, rx - canvas_cx / kx, 0, 1 / ky, ry - canvas_cy / ky)
        warp = lambda m: m.transform((CANVAS, CANVAS), Image.AFFINE, affine, resample=Image.BILINEAR)
        a, l, o = warp(opaque).load(), warp(line).load(), warp(opening).load()

        frame = SOURCE_COLUMNS.index(col)
        ox, oy = (frame * 2 + side) * CANVAS, row * CANVAS
        lp, op = lids.load(), openings.load()
        closed = frame == len(SOURCE_COLUMNS) - 1
        for v in range(CANVAS):
            for u in range(CANVAS):
                if a[u, v] < 128:
                    continue
                is_line = l[u, v] >= o[u, v]
                lp[ox + u, oy + v] = (*(LINE_RGB if is_line else SCLERA_RGB), 255)
                if not is_line and not closed and o[u, v] >= 128:
                    op[ox + u, oy + v] = 255

    # 3. The iris: the bored mood's pupil, drawn as a circle on the card (an ellipse in texels).
    radius = sum(radii) / len(radii)
    pupil_px = [sheet.getpixel(p) for (r, c, s), (_, pix) in frames.items()
                if r == 0 and c == SOURCE_COLUMNS[0] for p in pix]
    pupil_rgb = tuple(sorted(p[i] for p in pupil_px)[len(pupil_px) // 2] for i in range(3))

    def disc(share):
        """Hard-edged disc mask, a circle on the card (an ellipse in texels)."""
        ss = 4
        big = Image.new("L", (CANVAS * ss, CANVAS * ss), 0)
        rx, ry = radius * share * kx * ss, radius * share * ky * ss
        ImageDraw.Draw(big).ellipse((canvas_cx * ss - rx, canvas_cy * ss - ry,
                                     canvas_cx * ss + rx, canvas_cy * ss + ry), fill=255)
        return big.resize((CANVAS, CANVAS), Image.BOX).point(lambda v: 255 if v >= 128 else 0)

    iris = Image.new("RGBA", (CANVAS, CANVAS), (*IRIS_RGB, 0))
    iris.putalpha(disc(1.0))
    iris.paste((*pupil_rgb, 255), mask=disc(PUPIL_SHARE))

    lids.save(os.path.join(args.out, "Placeholder_Lids.png"))
    openings.save(os.path.join(args.out, "Placeholder_Openings.png"))
    iris.save(os.path.join(args.out, "Placeholder_Iris.png"))
    print(f"lids/openings {atlas_w}x{atlas_h}, iris {CANVAS}x{CANVAS}, pupil centre "
          f"({canvas_cx:.1f}, {canvas_cy:.1f}) texels from top-left, radius {radius:.1f} px, "
          f"scale {kx:.3f} x {ky:.3f} texels/px, pupil colour {pupil_rgb}")

    if args.preview:
        # Every frame with the iris composited where the shader would put it (no glance).
        preview = Image.new("RGBA", (atlas_w, atlas_h), (255, 0, 255, 255))
        preview.alpha_composite(lids)
        iris_layer = Image.new("RGBA", (atlas_w, atlas_h), (0, 0, 0, 0))
        for row in range(MOODS):
            for cell in range(2 * len(SOURCE_COLUMNS)):
                iris_layer.alpha_composite(iris, (cell * CANVAS, row * CANVAS))
        shown = Image.composite(iris_layer, Image.new("RGBA", (atlas_w, atlas_h), (0, 0, 0, 0)), openings)
        preview.alpha_composite(shown)
        # WHY: texels are 1.3x wider than tall on the card; stretch so the check reads as on the face.
        preview.resize((round(atlas_w * aspect), atlas_h), Image.NEAREST).save(args.preview)


if __name__ == "__main__":
    main()
