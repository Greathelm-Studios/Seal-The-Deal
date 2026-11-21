"""Run with Python 3 on Windows; standard library only, no Unity or font installation.

WHY: Windows GDI can read both supplied TrueType fonts without Pillow, packages,
system font installation, or copying the fonts into the Unity project.
Existing output paths are refused before any output is written. To make a variant,
use --output with a new directory under Assets/_Project/Art/Animated/.
"""
import argparse
import ctypes
from ctypes import wintypes
from pathlib import Path
import struct
import uuid
import zlib

WIDTH_PIXELS = 1024
HEIGHT_PIXELS = 32
FONT_HEIGHT_PIXELS = 24
TEXT_LEFT_PIXELS = 20
TEXT_TOP_PIXELS = 3
PRIVATE_FONT = 0x10
TRANSPARENT_TEXT_BACKGROUND = 1
ANTIALIASED_QUALITY = 4
MISSING_GLYPH = 0xFFFF
GENERATED_DATE = "2026-11-15"
PROVENANCE = (
    "AI-GENERATED SOURCE FILE\nGenerator : Codex (GPT-6)\n"
    f"Generated : {GENERATED_DATE}\nPrompt : REQUEST_ANIM.md reception tickers\n"
    "Reviewed : NOT YET REVIEWED BY A HUMAN\n"
)
ROOT = Path(__file__).resolve().parents[3]
ASSET_ROOT = ROOT / "Assets/_Project/Art/Animated"
FONT_ROOT = ROOT / "ArtSource/Kit/fonts"
MESSAGES = {
    "Ticker_FirmA": (("ASHCOMBE & PRYCE", "Michroma"), (" · ", "M PLUS 1"),
                     ("WELCOME", "Michroma"), (" · 受付 · …", "M PLUS 1")),
    "Ticker_FirmB": (("HALLORAN VOSS", "Michroma"), (" · ", "M PLUS 1"),
                     ("WELCOME", "Michroma"), (" · 受付 · …", "M PLUS 1")),
}


class BitmapInfoHeader(ctypes.Structure):
    """Win32 top-down, 32-bit uncompressed bitmap header."""
    _fields_ = [("size", wintypes.DWORD), ("width", wintypes.LONG), ("height", wintypes.LONG),
                ("planes", wintypes.WORD), ("bits", wintypes.WORD), ("compression", wintypes.DWORD),
                ("image_size", wintypes.DWORD), ("x_resolution", wintypes.LONG),
                ("y_resolution", wintypes.LONG), ("colours", wintypes.DWORD), ("important", wintypes.DWORD)]


class Size(ctypes.Structure):
    """Win32 measured text size, pixels."""
    _fields_ = [("width", wintypes.LONG), ("height", wintypes.LONG)]


def gdi_api():
    """Declare handle-safe Win32 signatures, including 64-bit handles."""
    gdi = ctypes.WinDLL("gdi32", use_last_error=True)
    signatures = {
        "AddFontResourceExW": ([wintypes.LPCWSTR, wintypes.DWORD, ctypes.c_void_p], ctypes.c_int),
        "RemoveFontResourceExW": ([wintypes.LPCWSTR, wintypes.DWORD, ctypes.c_void_p], wintypes.BOOL),
        "CreateCompatibleDC": ([wintypes.HDC], wintypes.HDC),
        "CreateDIBSection": ([wintypes.HDC, ctypes.POINTER(BitmapInfoHeader), wintypes.UINT,
                              ctypes.POINTER(ctypes.c_void_p), wintypes.HANDLE, wintypes.DWORD], wintypes.HANDLE),
        "SelectObject": ([wintypes.HDC, wintypes.HANDLE], wintypes.HANDLE),
        "DeleteObject": ([wintypes.HANDLE], wintypes.BOOL),
        "DeleteDC": ([wintypes.HDC], wintypes.BOOL),
        "CreateFontW": ([ctypes.c_int] * 5 + [wintypes.DWORD] * 8 + [wintypes.LPCWSTR], wintypes.HANDLE),
        "SetBkMode": ([wintypes.HDC, ctypes.c_int], ctypes.c_int),
        "SetTextColor": ([wintypes.HDC, wintypes.DWORD], wintypes.DWORD),
        "TextOutW": ([wintypes.HDC, ctypes.c_int, ctypes.c_int, wintypes.LPCWSTR, ctypes.c_int], wintypes.BOOL),
        "GetTextExtentPoint32W": ([wintypes.HDC, wintypes.LPCWSTR, ctypes.c_int, ctypes.POINTER(Size)], wintypes.BOOL),
        "GetGlyphIndicesW": ([wintypes.HDC, wintypes.LPCWSTR, ctypes.c_int,
                              ctypes.POINTER(wintypes.WORD), wintypes.DWORD], wintypes.DWORD),
        "GdiFlush": ([], wintypes.BOOL),
    }
    for name, (arguments, result) in signatures.items():
        function = getattr(gdi, name)
        function.argtypes = arguments
        function.restype = result
    return gdi


def rasterize(gdi, segments):
    """Draw a single message; reject missing glyphs and messages that would clip."""
    dc = gdi.CreateCompatibleDC(None)
    if not dc:
        raise ctypes.WinError(ctypes.get_last_error())
    info = BitmapInfoHeader()
    info.size = ctypes.sizeof(info)
    info.width, info.height = WIDTH_PIXELS, -HEIGHT_PIXELS
    info.planes, info.bits = 1, 32
    pixels = ctypes.c_void_p()
    bitmap = gdi.CreateDIBSection(dc, ctypes.byref(info), 0, ctypes.byref(pixels), None, 0)
    if not bitmap:
        gdi.DeleteDC(dc)
        raise ctypes.WinError(ctypes.get_last_error())
    previous_bitmap = gdi.SelectObject(dc, bitmap)
    previous_font = None
    fonts = []
    try:
        ctypes.memset(pixels, 0, WIDTH_PIXELS * HEIGHT_PIXELS * 4)
        gdi.SetBkMode(dc, TRANSPARENT_TEXT_BACKGROUND)
        gdi.SetTextColor(dc, 0xFFFFFF)
        x = TEXT_LEFT_PIXELS
        for text, family in segments:
            font = gdi.CreateFontW(-FONT_HEIGHT_PIXELS, 0, 0, 0, 400, 0, 0, 0, 1, 0, 0,
                                   ANTIALIASED_QUALITY, 0, family)
            if not font:
                raise ctypes.WinError(ctypes.get_last_error())
            fonts.append(font)
            previous = gdi.SelectObject(dc, font)
            if previous_font is None:
                previous_font = previous
            indices = (wintypes.WORD * len(text))()
            count = gdi.GetGlyphIndicesW(dc, text, len(text), indices, 1)
            if count == 0xFFFFFFFF or MISSING_GLYPH in indices:
                raise RuntimeError(f"{family} is missing a requested glyph in {text!r}")
            size = Size()
            if not gdi.GetTextExtentPoint32W(dc, text, len(text), ctypes.byref(size)):
                raise ctypes.WinError(ctypes.get_last_error())
            if x + size.width > WIDTH_PIXELS - TEXT_LEFT_PIXELS:
                raise RuntimeError("Message exceeds the 1024-pixel texture; shorten it explicitly.")
            if not gdi.TextOutW(dc, x, TEXT_TOP_PIXELS, text, len(text)):
                raise ctypes.WinError(ctypes.get_last_error())
            x += size.width
        gdi.GdiFlush()
        bgra = ctypes.string_at(pixels, WIDTH_PIXELS * HEIGHT_PIXELS * 4)
        grey = bytes(max(bgra[index:index + 3]) for index in range(0, len(bgra), 4))
        if not any(grey):
            raise RuntimeError("Font rasterizer produced an empty message.")
        return grey, x
    finally:
        if previous_font is not None:
            gdi.SelectObject(dc, previous_font)
        gdi.SelectObject(dc, previous_bitmap)
        for font in fonts:
            gdi.DeleteObject(font)
        gdi.DeleteObject(bitmap)
        gdi.DeleteDC(dc)


def png_bytes(grey):
    """Encode a small greyscale PNG with embedded provenance, using only zlib."""
    def chunk(kind, payload):
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload))
    rows = b"".join(b"\0" + grey[y * WIDTH_PIXELS:(y + 1) * WIDTH_PIXELS] for y in range(HEIGHT_PIXELS))
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", WIDTH_PIXELS, HEIGHT_PIXELS, 8, 0, 0, 0, 0))
            + chunk(b"tEXt", b"Provenance\0" + PROVENANCE.encode("latin-1"))
            + chunk(b"IDAT", zlib.compress(rows, 9)) + chunk(b"IEND", b""))


def importer_metadata():
    """Import a linear mask; bilinear sampling averages thin strokes into the LED cells."""
    header = "".join("# " + line + "\n" for line in PROVENANCE.splitlines())
    return header + f"""fileFormatVersion: 2
guid: {uuid.uuid4().hex}
TextureImporter:
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 0
  isReadable: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 0
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  maxTextureSize: 1024
  textureCompression: 0
  alphaSource: 0
  textureType: 0
  textureShape: 1
  userData: 'OfficeAnimation supplied mask; Reviewed : NOT YET REVIEWED BY A HUMAN'
  assetBundleName:
  assetBundleVariant:
"""


def main():
    """Preflight all paths and fonts before creating new output files."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ASSET_ROOT)
    output = parser.parse_args().output.resolve()
    if not output.is_relative_to(ASSET_ROOT.resolve()):
        parser.error("Output must stay inside Assets/_Project/Art/Animated/.")
    paths = [output / (name + suffix) for name in MESSAGES for suffix in (".png", ".png.meta")]
    if any(path.exists() for path in paths):
        parser.error("An output already exists; no files written. Use a new variant directory.")
    font_paths = [FONT_ROOT / "Michroma-Regular.ttf", FONT_ROOT / "MPLUS1[wght].ttf"]
    for path in font_paths:
        if not path.is_file():
            parser.error(f"Missing supplied font: {path}")
    gdi = gdi_api()
    loaded = []
    generated = []
    try:
        for path in font_paths:
            if not gdi.AddFontResourceExW(str(path), PRIVATE_FONT, None):
                raise RuntimeError(f"Could not privately load {path.name}")
            loaded.append(path)
        for name, segments in MESSAGES.items():
            grey, width = rasterize(gdi, segments)
            generated.append((name, png_bytes(grey), importer_metadata(), width))
    finally:
        for path in loaded:
            gdi.RemoveFontResourceExW(str(path), PRIVATE_FONT, None)
    output.mkdir(parents=True, exist_ok=True)
    for name, png, metadata, width in generated:
        with (output / (name + ".png.meta")).open("x", encoding="utf-8", newline="\n") as handle:
            handle.write(metadata)
        with (output / (name + ".png")).open("xb") as handle:
            handle.write(png)
        print(f"{name}: {WIDTH_PIXELS} x {HEIGHT_PIXELS}, message end {width} px; created new PNG and meta")


if __name__ == "__main__":
    main()
