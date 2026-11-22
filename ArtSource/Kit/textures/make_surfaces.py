# -----------------------------------------------------------------------------
#  AI-GENERATED SOURCE FILE
#  Generator : Claude Opus 5.5 (Claude Code)
#  Generated : 2026-11-15
#  Prompt    : Alex: "the clipping and texture work, it all feels basic and amateur" -> "Yes, all of it" (quality pass)
#  Reviewed  : NOT YET REVIEWED BY A HUMAN
#  Summary   : Gives the kit's big flat-colour surfaces real material: a colour map with gentle tone drift, a normal
#              map (paint orange-peel, powder coat, brushed metal, plaster, floor joints) and a metallic/smoothness map
#              so the gloss breaks up instead of reading as one plastic sheen. Its own file because these are three
#              maps per material with seamless noise, unlike make_textures.py's single flat tile drawings.
# -----------------------------------------------------------------------------
"""Usage: python ArtSource/Kit/textures/make_textures.py && python ArtSource/Kit/textures/make_surfaces.py

For each surface material writes, beside this script:
  <Name>.png         colour (final albedo; KitImport keeps the material's base colour white)
  <Name>_Normal.png  tangent-space normal map, OpenGL (+Y up), as Unity expects
  <Name>_Mask.png    URP metallic/smoothness: R = metallic, A = smoothness (absolute; KitImport sets the slider to 1)
Every map repeats seamlessly. KitImport.Surfaces sets each material's repeat in metres; the kit's box-mapped UVs are
1 unit per metre. Floor_Carpet keeps make_textures.py's tile drawing as its colour and only gains normal and mask, so
run make_textures.py first.

Pure Pillow (no numpy): seamless noise is random texels tiled 3 x 3, blurred, and cropped back to the middle tile, so the
blur wraps across the edges.
"""
import os
import random

from PIL import Image, ImageChops, ImageFilter, ImageOps

HERE = os.path.dirname(os.path.abspath(__file__))
SEED = 11               # fixed so a rebuild draws the same maps
COARSE_BLUR = 3         # feature size, texels, from which noise() upscales a coarse grid instead of blurring texels


def noise(size, blur, seed, stretch=1):
    """Seamless greyscale noise, size x size, full 0-255 range. blur is the Gaussian radius in texels (the feature
    size); stretch > 1 smears it along x (brushed metal): drawn stretch times narrower, then resized back."""
    rnd = random.Random(seed)
    # WHY a coarse grid upscaled for broad features: blurring 8-bit noise by tens of texels leaves only a few grey
    # levels, which posterise into contour bands once stretched; bicubic upscaling keeps a smooth ramp.
    cells = max(2, round(size / (blur * 2.5))) if blur >= COARSE_BLUR else size
    cx, cy = max(1, cells // stretch), cells
    grid = Image.frombytes('L', (cx, cy), rnd.randbytes(cx * cy))
    big = Image.new('L', (cx * 3, cy * 3))
    for i in range(3):
        for j in range(3):
            big.paste(grid, (i * cx, j * cy))
    big = big.resize((size * 3, size * 3), Image.BICUBIC)
    if 0 < blur < COARSE_BLUR:
        big = big.filter(ImageFilter.GaussianBlur(blur))
    return ImageOps.autocontrast(big.crop((size, size, size * 2, size * 2)), cutoff=0.5)


def mix(img, centre, spread):
    """Maps a 0-255 noise image to centre +/- spread (fractions), returned as a 0-255 'L' image."""
    return img.point(lambda v: max(0, min(255, int(round((centre + spread * (v / 127.5 - 1)) * 255)))))


def tint(colour, shade):
    """Colour map: the material's colour scaled by a shade image (128 = unchanged, +/- by the shade's offset)."""
    chans = []
    for c in colour:
        chans.append(shade.point(lambda v, c=c: max(0, min(255, int(round(c * (1 + (v - 128) / 255)))))))
    # WHY dither: a tone drift of a few percent is only a handful of 8-bit levels, which shows as contour lines; a
    # random -1/0/+1 per texel breaks the steps up without adding visible grain.
    rnd = random.Random(SEED + 99)
    size = shade.size
    dither = Image.merge('RGB', [Image.frombytes('L', size, rnd.randbytes(size[0] * size[1])).point(lambda v: v % 3)
                                 for _ in range(3)])
    return ImageChops.add(Image.merge('RGB', chans), dither, 1, -1)


def normal_from(height, strength):
    """Tangent-space normal map (OpenGL, +Y up) from a seamless height image. strength scales the slope; small slopes
    only, so the blue channel stays near 1 and is written flat."""
    s = height.size[0]
    big = Image.new('L', (s * 3, s * 3))
    for i in range(3):
        for j in range(3):
            big.paste(height, (i * s, j * s))
    # PIL Kernel: out = sum(k * px) / scale + offset, so scale = 1 / strength.
    # WHY these signs: n.x = -dh/du (image x right = u); n.y = +dh/dy_image, because image rows run down while v runs up.
    rx = big.filter(ImageFilter.Kernel((3, 3), [0, 0, 0, 1, 0, -1, 0, 0, 0], scale=1 / strength, offset=128))
    gy = big.filter(ImageFilter.Kernel((3, 3), [0, -1, 0, 0, 0, 0, 0, 1, 0], scale=1 / strength, offset=128))
    box = (s, s, s * 2, s * 2)
    return Image.merge('RGB', (rx.crop(box), gy.crop(box), Image.new('L', (s, s), 255)))


def mask(metallic, smooth):
    """URP metallic/smoothness map: R = metallic (constant 0-1), A = the smoothness image."""
    s = smooth.size
    m = Image.new('L', s, int(round(metallic * 255)))
    return Image.merge('RGBA', (m, m, m, smooth))


def save(name, colour, nrm, msk):
    if colour is not None:
        colour.save(os.path.join(HERE, name + '.png'))
    nrm.save(os.path.join(HERE, name + '_Normal.png'))
    msk.save(os.path.join(HERE, name + '_Mask.png'))


def wall():
    """Wall_PaperWhite: satin lacquered paint over 2 m. A broad, barely-there tone drift (light falls off unevenly on
    real walls), fine orange-peel in the normal, and satin gloss that wanders between 0.6 and 0.74. WHY lower than the
    old 0.85: a uniform 0.85 on every wall read as moulded plastic; real lacquered panels are satin and uneven."""
    n = 1024
    drift = ImageChops.add(mix(noise(n, 110, SEED), 0.5, 0.5), mix(noise(n, 14, SEED + 1), 0.5, 0.5), 2, -128 + 128)
    shade = mix(drift, 0.5, 0.018)          # +/- 1.8 % brightness
    colour = tint((0xF4, 0xF8, 0xF8), shade)
    # WHY 0.12 and a 2.2-texel peel: at 0.35 the walls read as gritty stucco at arm's length (first capture).
    peel = noise(n, 2.2, SEED + 2)
    smooth = mix(noise(n, 70, SEED + 3), 0.67, 0.07)
    save('Wall_PaperWhite', colour, normal_from(peel, 0.12), mask(0.0, smooth))


def trim_navy():
    """Trim_Navy: navy powder coat over 1 m. Grainy orange-peel normal, a slight tone wander, gloss 0.4-0.52."""
    n = 512
    colour = tint((0x11, 0x1B, 0x38), mix(noise(n, 30, SEED + 10), 0.5, 0.04))
    # WHY 1.8 texels at 0.08: finer, stronger grain sparkled like sandpaper on the big navy sill trays (capture 2).
    grain = noise(n, 1.8, SEED + 11)
    smooth = mix(noise(n, 24, SEED + 12), 0.46, 0.06)
    save('Trim_Navy', colour, normal_from(grain, 0.08), mask(0.0, smooth))


def aluminium():
    """Frame_Aluminium: brushed aluminium over 1 m, the brushing along U (the length of a rail or mullion). Streaks
    in colour, gloss and normal, so highlights stretch along the frame instead of sitting as one flat sheen."""
    n = 512
    streak = noise(n, 0.8, SEED + 20, stretch=64)
    colour = tint((0xC8, 0xD2, 0xD9), mix(streak, 0.5, 0.05))
    smooth = mix(noise(n, 1.2, SEED + 21, stretch=64), 0.63, 0.08)
    save('Frame_Aluminium', colour, normal_from(streak, 0.35), mask(0.85, smooth))


def ceiling():
    """Ceiling_Tile (the bare soffit): matt plaster over 2 m. A fine speckle in colour and normal and a very broad
    tone drift; gloss 0.08-0.16, so the light lines' glow doesn't smear across it."""
    n = 1024
    shade = ImageChops.add(mix(noise(n, 120, SEED + 30), 0.5, 0.012), mix(noise(n, 0.7, SEED + 31), 0.5, 0.012), 1, -128)
    colour = tint((0xF0, 0xF5, 0xF5), shade)
    plaster = noise(n, 1.6, SEED + 32)
    smooth = mix(noise(n, 60, SEED + 33), 0.12, 0.04)
    save('Ceiling_Tile', colour, normal_from(plaster, 0.15), mask(0.0, smooth))


def floor():
    """Floor_Carpet (the polished 1 m tile floor): keeps make_textures.py's tile drawing as colour. Adds recessed
    joints and a faint ripple in the normal, and gloss 0.88-0.96 per patch, dull (0.55) in the joints, so the mirror
    floor shows the slight unevenness of real polished stone instead of a perfect plane."""
    tile = Image.open(os.path.join(HERE, 'Floor_Carpet.png')).convert('L')
    n = tile.size[0]
    lo, hi = tile.getextrema()
    joints = tile.point(lambda v: 0 if v < (lo + hi) / 2 else 255)             # 0 in the joints, 255 on the tiles
    ripple = mix(noise(n, 18, SEED + 40), 0.5, 0.5)
    height = ImageChops.add(joints.point(lambda v: v * 0.85), ripple.point(lambda v: v * 0.15))
    gloss = mix(noise(n, 50, SEED + 41), 0.92, 0.04)
    smooth = ImageChops.darker(gloss, joints.point(lambda v: 140 if v == 0 else 255))
    save('Floor_Carpet', None, normal_from(height, 1.2), mask(0.0, smooth))


def carpet_dark():
    """Carpet_Dark: keeps its colour drawing; adds a loop-pile normal and dead-matt gloss (0.03-0.09)."""
    n = Image.open(os.path.join(HERE, 'Carpet_Dark.png')).size[0]
    pile = noise(n, 0.7, SEED + 50)
    smooth = mix(noise(n, 20, SEED + 51), 0.06, 0.03)
    save('Carpet_Dark', None, normal_from(pile, 1.4), mask(0.0, smooth))


if __name__ == '__main__':
    wall()
    trim_navy()
    aluminium()
    ceiling()
    floor()
    carpet_dark()
    print('wrote surface maps for Wall_PaperWhite, Trim_Navy, Frame_Aluminium, Ceiling_Tile, Floor_Carpet, Carpet_Dark')
