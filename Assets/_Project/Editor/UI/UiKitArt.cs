using System;
using System.IO;
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// The kit's small sprites, drawn by code into PNGs under Assets/_Project/UI/Kit/Sprites: keylines, the dashed
    /// disabled outline, corner brackets, the chevron cue, the background grid, the fallback sky, a barcode, and the
    /// edge marker's diamond, triangle and pointer.
    /// White where they are tinted by tokens. A PNG someone edited is left alone (GeneratedAssetGuard).
    /// </summary>
    internal static class UiKitArt
    {
        internal const string Folder = "Assets/_Project/UI/Kit/Sprites";

        internal static Sprite White, Outline1, Outline2, Dashed, Bracket, Chevron, Grid, Sky, Barcode, Diamond, Triangle, Pointer;

        // Shape and depth (2026-11-15, after Cocoon's tokens): one radius per class (panel 20, card 16, button 12,
        // small 8), a soft shadow, and a "pillowy" bevel (light along the top inside edge, shade along the bottom).
        // Drawn at 2x and imported at 200 pixels per unit, so corners stay crisp at 1440p and 4K.
        internal static Sprite Round8, Round12, Round16, Round20, Ring8, Ring12, Ring16Bold, Ring20, Shadow12, Shadow20,
            BevelTop12, BevelBottom12, Signature;

        const int Hi = 2;   // drawing scale of the rounded sprites

        internal static void Ensure(CloseTheDeal.UI.UiTokens tokens)
        {
            UiKitAssets.EnsureFolder(Folder);
            White = Make("Kit_White", 4, 4, 0, true, (x, y) => 1f);
            Outline1 = Make("Kit_Outline1", 8, 8, 1, true, (x, y) => Edge(x, y, 8, 1));
            Outline2 = Make("Kit_Outline2", 8, 8, 2, true, (x, y) => Edge(x, y, 8, 2));
            Dashed = Make("Kit_Dashed", 8, 8, 1, true, (x, y) => Edge(x, y, 8, 1) > 0f && ((x + y) % 4) < 2 ? 1f : 0f);
            Bracket = Make("Kit_Bracket", 14, 14, 0, true, (x, y) => (y >= 12 || x <= 1) ? 1f : 0f);
            Chevron = Make("Kit_Chevron", 40, 16, 0, false, ChevronAlpha);
            Grid = Make("Kit_Grid", 32, 32, 0, true, (x, y) => (x == 0 || y == 0) ? 1f : 0f, wrap: true);
            Barcode = Make("Kit_Barcode", 160, 24, 0, true, BarcodeAlpha);
            Sky = MakeSky(tokens);
            Diamond = Make("Kit_Diamond", 64, 64, 0, false, (x, y) => Mathf.Clamp01(32.5f - (Mathf.Abs(x + 0.5f - 32f) + Mathf.Abs(y + 0.5f - 32f))));
            Triangle = Make("Kit_Triangle", 64, 64, 0, false, TriangleAlpha);
            Pointer = Make("Kit_Pointer", 20, 24, 0, false, PointerAlpha);

            Round8 = Rounded("Kit_Round8", 8);
            Round12 = Rounded("Kit_Round12", 12);
            Round16 = Rounded("Kit_Round16", 16);
            Round20 = Rounded("Kit_Round20", 20);
            Ring8 = Ring("Kit_Ring8", 8, 1f);
            Ring12 = Ring("Kit_Ring12", 12, 1f);
            Ring16Bold = Ring("Kit_Ring16Bold", 16, 2f);
            Ring20 = Ring("Kit_Ring20", 20, 1f);
            Shadow12 = SoftShadow("Kit_Shadow12", 12, 16);
            Shadow20 = SoftShadow("Kit_Shadow20", 20, 24);
            BevelTop12 = Bevel("Kit_BevelTop12", 12, true);
            BevelBottom12 = Bevel("Kit_BevelBottom12", 12, false);
            Signature = MakeSignature(tokens);
        }

        // ---- Rounded shapes (2x) -------------------------------------------------------------------

        /// <summary>Signed distance from (px, py) to a rounded box spanning [x0, x1] x [y0, y1]; negative inside.</summary>
        static float RoundBox(float px, float py, float x0, float y0, float x1, float y1, float r)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float qx = Mathf.Abs(px - cx) - ((x1 - x0) * 0.5f - r);
            float qy = Mathf.Abs(py - cy) - ((y1 - y0) * 0.5f - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>A white rounded rectangle, 9-sliced so it fits any size at radius r.</summary>
        static Sprite Rounded(string name, int r)
        {
            int rr = r * Hi, s = rr * 2 + 4;
            return MakeHi(name, s, s, rr + 1, (x, y) => Mathf.Clamp01(0.5f - RoundBox(x + 0.5f, y + 0.5f, 0f, 0f, s, s, rr)));
        }

        /// <summary>A rounded keyline, width in 1x pixels, its outer edge on the shape's edge.</summary>
        static Sprite Ring(string name, int r, float width)
        {
            int rr = r * Hi, s = rr * 2 + 4;
            float w = width * Hi;
            return MakeHi(name, s, s, rr + 1, (x, y) =>
            {
                float d = RoundBox(x + 0.5f, y + 0.5f, 0f, 0f, s, s, rr);
                return Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(0.5f + d + w);
            });
        }

        /// <summary>A soft shadow for a radius-r shape, spreading `blur` 1x pixels past it. Place it outset by `blur`.</summary>
        static Sprite SoftShadow(string name, int r, int blur)
        {
            int rr = r * Hi, b = blur * Hi, s = (rr + b) * 2 + 4;
            return MakeHi(name, s, s, rr + b + 1, (x, y) =>
            {
                float d = RoundBox(x + 0.5f, y + 0.5f, b, b, s - b, s - b, rr);
                if (d <= 0f)
                    return 1f;
                float k = d / (b * 0.55f);
                return Mathf.Exp(-k * k);
            });
        }

        /// <summary>The bevel: light along the top inside edge (top), or shade along the bottom inside edge.</summary>
        static Sprite Bevel(string name, int r, bool top)
        {
            int rr = r * Hi, s = rr * 2 + 4;
            float band = 3f * Hi;
            return MakeHi(name, s, s, rr + 1, (x, y) =>
            {
                float d = RoundBox(x + 0.5f, y + 0.5f, 0f, 0f, s, s, rr);
                float inside = Mathf.Clamp01(0.5f - d);
                float rim = Mathf.Clamp01(1f + d / band);                    // 1 at the edge, 0 a band's width in
                float side = top ? Mathf.Clamp01((y + 0.5f - s * 0.5f) / (s * 0.5f)) : Mathf.Clamp01((s * 0.5f - (y + 0.5f)) / (s * 0.5f));
                return inside * rim * Mathf.Sqrt(side);
            });
        }

        /// <summary>
        /// The one signature gradient, for the primary button only: Corporate Cobalt to Electric Blue, left to right,
        /// at the primary button's own size (a 9-slice can't carry a horizontal gradient). Paper text stays legible
        /// across it. Colour is baked in, so the button tints it white.
        /// </summary>
        static Sprite MakeSignature(CloseTheDeal.UI.UiTokens tokens)
        {
            string path = Folder + "/Kit_Signature.png";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            int w = Mathf.RoundToInt(420f * Hi), h = Mathf.RoundToInt(tokens.PrimaryHeight * Hi), rr = 12 * Hi;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c = Color.Lerp(tokens.Cobalt, tokens.Electric, Mathf.SmoothStep(0f, 1f, (x + 0.5f) / w));
                    c.a = Mathf.Clamp01(0.5f - RoundBox(x + 0.5f, y + 0.5f, 0f, 0f, w, h, rr));
                    tex.SetPixel(x, y, c);
                }
            }

            return Save(tex, path, 0, false, false, 100f * Hi);
        }

        static Sprite MakeHi(string name, int w, int h, int border, Func<int, int, float> alpha)
        {
            string path = $"{Folder}/{name}.png";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y)));
            }

            return Save(tex, path, border, false, false, 100f * Hi);
        }

        /// <summary>An upright triangle filling the square (apex at the top centre), anti-aliased.</summary>
        static float TriangleAlpha(int x, int y)
        {
            float px = x + 0.5f, py = y + 0.5f;
            const float top = 62f, bottom = 4f;
            if (py < bottom - 1f || py > top + 1f)
                return 0f;
            float half = 30f * (top - py) / (top - bottom); // half width at this height
            float side = half - Mathf.Abs(px - 32f);
            return Mathf.Clamp01(Mathf.Min(side * 0.9f + 0.5f, py - bottom + 0.5f));
        }

        /// <summary>One bold chevron pointing right: the edge marker's direction cue.</summary>
        static float PointerAlpha(int x, int y)
        {
            float d = Mathf.Min(Segment(x + 0.5f, y + 0.5f, 5f, 3f, 15f, 12f), Segment(x + 0.5f, y + 0.5f, 15f, 12f, 5f, 21f));
            return Mathf.Clamp01(3.2f - d);
        }

        static float Edge(int x, int y, int size, int width)
        {
            return x < width || y < width || x >= size - width || y >= size - width ? 1f : 0f;
        }

        /// <summary>Three thin chevrons pointing right, anti-aliased.</summary>
        static float ChevronAlpha(int x, int y)
        {
            float best = 0f;
            for (int i = 0; i < 3; i++)
            {
                float ox = 4f + i * 12f;
                float d = Mathf.Min(Segment(x + 0.5f, y + 0.5f, ox, 2f, ox + 7f, 8f), Segment(x + 0.5f, y + 0.5f, ox + 7f, 8f, ox, 14f));
                best = Mathf.Max(best, Mathf.Clamp01(1.6f - d));
            }

            return best;
        }

        static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(px, py);
            var a = new Vector2(ax, ay);
            var b = new Vector2(bx, by);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>A fixed bar pattern (not a real code): decoration only, the guide's barcode device.</summary>
        static float BarcodeAlpha(int x, int y)
        {
            uint h = (uint)(x * 2654435761u) ^ 0x9E3779B9u;
            h ^= h >> 13;
            int width = 1 + (int)(h % 3);
            return (x % 7) < width ? 1f : 0f;
        }

        static Sprite MakeSky(CloseTheDeal.UI.UiTokens tokens)
        {
            Color top = tokens.Cobalt;
            Color bottom = Color.Lerp(tokens.Electric, tokens.Paper, 0.18f);
            string path = Folder + "/Kit_SkyFallback.png";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);

            var tex = new Texture2D(4, 256, TextureFormat.RGBA32, false);
            for (int y = 0; y < 256; y++)
            {
                float t = y / 255f;
                Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, Mathf.Pow(t, 0.8f)));
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, c);
            }

            return Save(tex, path, 0, false, false);
        }

        static Sprite Make(string name, int w, int h, int border, bool point, Func<int, int, float> alpha, bool wrap = false)
        {
            string path = $"{Folder}/{name}.png";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y)));
            }

            return Save(tex, path, border, point, wrap);
        }

        static Sprite Save(Texture2D tex, string path, int border, bool point, bool wrap, float pixelsPerUnit = 100f)
        {
            bool isNew = !File.Exists(path);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!isNew)
            {
                // The import settings (filter, border, wrap) were set when the file was made; a designer may have changed them since.
                Sprite kept = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                GeneratedAssetGuard.MarkGenerated(kept);
                return kept;
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            importer.wrapMode = wrap ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            GeneratedAssetGuard.MarkGenerated(sprite);
            return sprite;
        }
    }
}
