using System;
using System.IO;
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// The kit's small sprites, drawn by code into PNGs under Assets/_Project/UI/Kit/Sprites: keylines, the dashed
    /// disabled outline, corner brackets, the chevron cue, the background grid, the fallback sky and a barcode.
    /// White where they are tinted by tokens. A PNG someone edited is left alone (GeneratedAssetGuard).
    /// </summary>
    internal static class UiKitArt
    {
        internal const string Folder = "Assets/_Project/UI/Kit/Sprites";

        internal static Sprite White, Outline1, Outline2, Dashed, Bracket, Chevron, Grid, Sky, Barcode, Dots, TitleSky;

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
            Dots = Make("Kit_Dots", 12, 12, 0, true, (x, y) => (x >= 5 && x <= 6 && y >= 5 && y <= 6) ? 1f : 0f, wrap: true);
            TitleSky = MakeTitleSky(tokens);
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

        // ---- The title's fallback sky -------------------------------------------------------------------

        const int SkyW = 1920, SkyH = 664, Cell = 8;

        /// <summary>A cumulus as discs (centre x, centre y from the top, radius) with a flat base, in sky pixels.</summary>
        static readonly Vector3[][] Clouds =
        {
            new[] { new Vector3(160, 560, 150), new Vector3(330, 440, 150), new Vector3(470, 520, 120), new Vector3(560, 600, 110), new Vector3(240, 620, 120) },
            new[] { new Vector3(330, 330, 70), new Vector3(400, 300, 50), new Vector3(260, 360, 50) },
            new[] { new Vector3(820, 600, 90), new Vector3(960, 540, 110), new Vector3(1080, 580, 90), new Vector3(1180, 620, 70), new Vector3(900, 640, 70) },
            new[] { new Vector3(1800, 590, 90), new Vector3(1880, 560, 70), new Vector3(1740, 630, 60) },
        };

        static readonly float[] CloudBase = { 664, 392, 664, 664 };

        /// <summary>
        /// The title's fallback sky until the title lane's live one goes in the slot: deep cobalt to a pale horizon,
        /// stepped cel cumulus (8 px steps, lit from the upper left, shaded silver on the lower right), and the rival
        /// tower in the right-hand pane, white with dark window bands. Drawn here; nothing is downloaded.
        /// </summary>
        static Sprite MakeTitleSky(CloseTheDeal.UI.UiTokens t)
        {
            string path = Folder + "/Kit_TitleSky.png";
            if (!GeneratedAssetGuard.MayOverwrite(path))
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);

            var tex = new Texture2D(SkyW, SkyH, TextureFormat.RGBA32, false);
            var pixels = new Color32[SkyW * SkyH];
            Color top = t.Cobalt;
            Color low = Color.Lerp(t.Electric, t.Paper, 0.28f);
            Color window = Color.Lerp(t.Navy, t.Cobalt, 0.45f);
            for (int py = 0; py < SkyH; py++)
            {
                float fromTop = SkyH - 1 - py;
                float band = Mathf.Floor(fromTop / Cell) * Cell;
                Color sky = Color.Lerp(top, low, Mathf.SmoothStep(0f, 1f, band / SkyH));
                for (int px = 0; px < SkyW; px++)
                {
                    float cx = Mathf.Floor(px / (float)Cell) * Cell + Cell * 0.5f;
                    float cy = band + Cell * 0.5f;
                    pixels[py * SkyW + px] = PixelAt(cx, cy, sky, t, window, px, fromTop);
                }
            }

            tex.SetPixels32(pixels);
            return Save(tex, path, 0, true, false);
        }

        static Color PixelAt(float cx, float cy, Color sky, CloseTheDeal.UI.UiTokens t, Color window, int px, float fromTop)
        {
            Color tower = Tower(px, fromTop, t, window);
            if (tower.a > 0f)
                return tower;
            for (int c = 0; c < Clouds.Length; c++)
            {
                if (!InCloud(c, cx, cy))
                    continue;
                bool shade = !InCloud(c, cx + 14f, cy + 14f);
                return shade ? t.Silver : t.Paper;
            }

            return sky;
        }

        static bool InCloud(int cloud, float x, float y)
        {
            if (y > CloudBase[cloud])
                return false;
            foreach (Vector3 disc in Clouds[cloud])
            {
                float dx = x - disc.x, dy = y - disc.y;
                if (dx * dx + dy * dy <= disc.z * disc.z)
                    return true;
            }

            return false;
        }

        /// <summary>The rival firm's tower: white, a silver shadow side, rows of dark window bands, cut by the pane.</summary>
        static Color Tower(int x, float y, CloseTheDeal.UI.UiTokens t, Color window)
        {
            const int left = 1462, right = 1726, roof = 352;
            if (x < left || x >= right || y < roof)
                return Color.clear;
            if (x >= right - 30)
                return t.Silver;
            int row = (int)(y - roof) % 64;
            int col = (x - left - 12) % 40;
            bool inWindowBand = row >= 40 && row < 62 && x >= left + 12 && x < right - 42 && col < 34;
            return inWindowBand ? window : t.Paper;
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

        static Sprite Save(Texture2D tex, string path, int border, bool point, bool wrap)
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
            importer.spritePixelsPerUnit = 100f;
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
