using System;
using System.IO;
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEngine;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>Generates small kit sprite PNGs (keylines, brackets, chevrons, grid, barcode, sky fallback, markers).</summary>
    internal static class UiKitArt
    {
        internal const string Folder = "Assets/_Project/UI/Kit/Sprites";

        internal static Sprite White, Outline1, Outline2, Dashed, Bracket, Chevron, Grid, Sky, Barcode, Diamond, Triangle, Pointer;

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
        }

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

        static float PointerAlpha(int x, int y)
        {
            float d = Mathf.Min(Segment(x + 0.5f, y + 0.5f, 5f, 3f, 15f, 12f), Segment(x + 0.5f, y + 0.5f, 15f, 12f, 5f, 21f));
            return Mathf.Clamp01(3.2f - d);
        }

        static float Edge(int x, int y, int size, int width)
        {
            return x < width || y < width || x >= size - width || y >= size - width ? 1f : 0f;
        }

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
