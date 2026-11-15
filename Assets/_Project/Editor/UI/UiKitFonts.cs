using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace CloseTheDeal.Editor.UI
{
    /// <summary>
    /// Moves the UI off TextMeshPro's bundled Liberation Sans onto the faces Adam chose (2026-11-15):
    /// Saira Expanded Light for display, Archivo SemiExpanded for labels and body, Martian Mono Light for data.
    /// Three menus, run in order:
    ///   1 Import Kit Fonts: makes the dynamic TMP font assets (add-only) with Liberation as the last fallback, and
    ///     points the UiTokens type roles still on Liberation at the new faces.
    ///   2 Retype Report: a dry run over every UI prefab and the open scene; writes ArtSource/UI/review/font_swap_report.md
    ///     (which face each text gets, and any text that would overflow its box or has a custom material).
    ///   3 Retype Kit Text: does it. Only texts still on Liberation are touched, so it can run again safely and never
    ///     overrides a face someone chose by hand. Texts with a custom Liberation material (outline, shadow) are skipped
    ///     and listed. TMP Settings' default font stays Liberation, so other people's greybox text is unchanged.
    /// Guide: ArtSource/UI/fonts/README.md.
    /// </summary>
    public static class UiKitFonts
    {
        const string FontFolder = UiKitAssets.Root + "/Fonts";
        const string ReportPath = "ArtSource/UI/review/font_swap_report.md";
        const float DisplayTrackingMax = 6f;   // Saira Expanded is already wide: Liberation's tracked spread overflows

        enum Face { Display, Label, Body, Mono }

        static readonly Dictionary<Face, (string ttf, string asset)> Files = new()
        {
            [Face.Display] = ("SairaExpanded-Light.ttf", "SairaExpanded-Light SDF"),
            [Face.Label] = ("ArchivoSemiExpanded-Medium.ttf", "ArchivoSemiExpanded-Medium SDF"),
            [Face.Body] = ("ArchivoSemiExpanded-Regular.ttf", "ArchivoSemiExpanded-Regular SDF"),
            [Face.Mono] = ("MartianMono-Light.ttf", "MartianMono-Light SDF"),
        };

        // Characters baked into each atlas up front; anything else is added when first shown (dynamic atlases).
        const string Prewarm = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~§·—–→←↑↓‹›×°’“”…";

        static readonly Regex DataText = new(@"^[0-9:./%×x+\-–\s]+$");
        static readonly Regex DataName = new(@"Clock|Timer|Time|Ammo|Count|Score|Readout|Value|Number|Percent|Ping|Fps", RegexOptions.IgnoreCase);

        // ---- 1. Import -------------------------------------------------------------------------------------------

        [MenuItem("Close the Deal/UI/Fonts/1 Import Kit Fonts")]
        static void Import()
        {
            Dictionary<Face, TMP_FontAsset> faces = EnsureFaces();
            if (faces == null)
                return;
            int roles = PointTokens(faces);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Fonts] Kit fonts ready in {FontFolder}; {roles} type role(s) moved off Liberation.");
        }

        static Dictionary<Face, TMP_FontAsset> EnsureFaces()
        {
            TMP_FontAsset liberation = TMP_Settings.defaultFontAsset;
            var faces = new Dictionary<Face, TMP_FontAsset>();
            foreach (KeyValuePair<Face, (string ttf, string asset)> f in Files)
            {
                TMP_FontAsset asset = EnsureFace(f.Value.ttf, f.Value.asset);
                if (asset == null)
                    return null;
                faces[f.Key] = asset;
            }

            // Fallbacks: the other kit faces first, Liberation last for anything none of them has. Set only when empty.
            SetFallbacks(faces[Face.Display], faces[Face.Label], liberation);
            SetFallbacks(faces[Face.Label], faces[Face.Body], faces[Face.Mono], liberation);
            SetFallbacks(faces[Face.Body], faces[Face.Label], faces[Face.Mono], liberation);
            SetFallbacks(faces[Face.Mono], faces[Face.Body], liberation);
            return faces;
        }

        static TMP_FontAsset EnsureFace(string ttf, string name)
        {
            string path = $"{FontFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
                return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{ttf}");
            if (font == null)
            {
                Debug.LogError($"[Fonts] {FontFolder}/{ttf} is missing. Copy it from ArtSource/UI/fonts/static.");
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
            asset.atlasTextures[0].name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;   // keeps the source font for glyphs added in builds
            asset.TryAddCharacters(Prewarm, out _);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            GeneratedAssetGuard.MarkGenerated(asset);
            return asset;
        }

        static void SetFallbacks(TMP_FontAsset asset, params TMP_FontAsset[] chain)
        {
            if (asset.fallbackFontAssetTable != null && asset.fallbackFontAssetTable.Count > 0)
                return;
            asset.fallbackFontAssetTable = chain.Where(c => c != null).ToList();
            EditorUtility.SetDirty(asset);
        }

        /// <summary>Points every type role still on Liberation at its kit face. A role someone re-pointed is left alone.</summary>
        static int PointTokens(Dictionary<Face, TMP_FontAsset> faces)
        {
            UiTokens tokens = UiKitAssets.EnsureTokens();
            TMP_FontAsset liberation = TMP_Settings.defaultFontAsset;
            int moved = 0;
            moved += Point(ref tokens.Title, faces[Face.Display], liberation, "Saira Expanded Light", true);
            moved += Point(ref tokens.Heading, faces[Face.Display], liberation, "Saira Expanded Light", true);
            moved += Point(ref tokens.Item, faces[Face.Label], liberation, "Archivo SemiExpanded Medium", false);
            moved += Point(ref tokens.Label, faces[Face.Label], liberation, "Archivo SemiExpanded Medium", false);
            moved += Point(ref tokens.Body, faces[Face.Body], liberation, "Archivo SemiExpanded", false);
            moved += Point(ref tokens.Section, faces[Face.Mono], liberation, "Martian Mono Light", false);
            moved += Point(ref tokens.Caption, faces[Face.Mono], liberation, "Martian Mono Light", false);
            moved += Point(ref tokens.Micro, faces[Face.Mono], liberation, "Martian Mono Light", false);
            EditorUtility.SetDirty(tokens);
            return moved;
        }

        static int Point(ref UiTypeRole role, TMP_FontAsset face, TMP_FontAsset liberation, string target, bool display)
        {
            if (role.Font != null && role.Font != liberation)
                return 0;
            role.Font = face;
            role.Material = null;   // Type_Thin was made from Liberation's atlas; the new faces are thin on their own
            role.TargetFace = target;
            if (display)
                role.Tracking = Mathf.Min(role.Tracking, DisplayTrackingMax);
            return 1;
        }

        // ---- 2 and 3. Retype -------------------------------------------------------------------------------------

        [MenuItem("Close the Deal/UI/Fonts/2 Retype Report (dry run)")]
        static void Report() => Retype(false);

        [MenuItem("Close the Deal/UI/Fonts/3 Retype Kit Text")]
        static void Apply() => Retype(true);   // no confirm dialog: it never reached a Unity window on another Space

        sealed class Row
        {
            public string Where, Path, Text, Face, Flags;
            public float Size, Tracking;
        }

        static void Retype(bool apply)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Fonts] Stop Play mode first: in Play the scene holds run-time state (moved brackets, filled text) that would be saved with it.");
                return;
            }
            Dictionary<Face, TMP_FontAsset> faces = LoadFaces();
            if (faces == null)
            {
                Debug.LogError("[Fonts] Run Close the Deal > UI > Fonts > 1 Import Kit Fonts first.");
                return;
            }

            var rows = new List<Row>();
            foreach (string prefabPath in UiPrefabs())
                RetypePrefab(prefabPath, faces, apply, rows);
            RetypeOpenScene(faces, apply, rows);
            WriteReport(rows, apply);
            Debug.Log($"[Fonts] {(apply ? "Retyped" : "Would retype")} {rows.Count(r => r.Flags.Length == 0 || !r.Flags.Contains("skipped"))} text(s); report: {ReportPath}");
        }

        static Dictionary<Face, TMP_FontAsset> LoadFaces()
        {
            var faces = new Dictionary<Face, TMP_FontAsset>();
            foreach (KeyValuePair<Face, (string ttf, string asset)> f in Files)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontFolder}/{f.Value.asset}.asset");
                if (asset == null)
                    return null;
                faces[f.Key] = asset;
            }
            return faces;
        }

        /// <summary>Kit pieces first, so screens that nest them already see the new faces.</summary>
        static IEnumerable<string> UiPrefabs()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { UiKitAssets.Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p.StartsWith(UiKitAssets.KitFolder) ? 0 : 1)
                .ThenBy(p => p);
        }

        static void RetypePrefab(string path, Dictionary<Face, TMP_FontAsset> faces, bool apply, List<Row> rows)
        {
            // Read what is on disk, not a stale import: otherwise a prefab restored from git outside Unity is saved
            // back with whatever Unity last held in memory.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = RetypeAll(root.GetComponentsInChildren<TMP_Text>(true), path, faces, apply, rows);
                if (apply && changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void RetypeOpenScene(Dictionary<Face, TMP_FontAsset> faces, bool apply, List<Row> rows)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                return;
            var texts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TMP_Text>(true)).ToArray();
            bool changed = RetypeAll(texts, scene.path, faces, apply, rows);
            if (apply && changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        static bool RetypeAll(IEnumerable<TMP_Text> texts, string where, Dictionary<Face, TMP_FontAsset> faces, bool apply, List<Row> rows)
        {
            TMP_FontAsset liberation = TMP_Settings.defaultFontAsset;
            var thin = AssetDatabase.LoadAssetAtPath<Material>(UiKitAssets.ThinPath);
            bool changed = false;
            foreach (TMP_Text t in texts)
            {
                if (faces.ContainsValue(t.font))
                {
                    changed |= Refit(t, where, apply, rows);   // already retyped: only re-check it fits its box
                    continue;
                }
                if (t.font != liberation)
                    continue;   // a face someone chose by hand
                Face face = Classify(t);
                Row row = NewRow(t, where, face.ToString());
                rows.Add(row);
                bool plainMaterial = t.fontSharedMaterial == null || t.fontSharedMaterial == liberation.material || t.fontSharedMaterial == thin;
                if (!plainMaterial)
                {
                    row.Flags = $"skipped: custom material '{t.fontSharedMaterial.name}'";
                    continue;
                }
                row.Flags = Trial(t, faces[face], face, apply);
                changed |= apply;
            }
            return changed;
        }

        /// <summary>A text already on a kit face: fit it to its box again, recording a row only if something changes.</summary>
        static bool Refit(TMP_Text t, string where, bool apply, List<Row> rows)
        {
            var before = Snapshot.Of(t);
            if (apply)
                Undo.RecordObject(t, "Fit kit text");
            string flags = Fit(t);
            bool moved = !before.Matches(t);
            if (moved || flags.Contains("too far"))
                rows.Add(new Row { Where = Path.GetFileNameWithoutExtension(where), Path = HierarchyPath(t.transform),
                    Text = Clean(t.text), Size = before.Size, Tracking = t.characterSpacing, Face = "refit", Flags = flags });
            if (!apply)
                before.Restore(t);
            else if (moved)
                EditorUtility.SetDirty(t);
            return apply && moved;
        }

        /// <summary>Puts the new face on, fits it, and puts the old one back unless applying. Returns the flags.</summary>
        static string Trial(TMP_Text t, TMP_FontAsset face, Face kind, bool apply)
        {
            var before = Snapshot.Of(t);
            if (apply)
                Undo.RecordObject(t, "Retype kit text");
            t.font = face;
            t.fontSharedMaterial = face.material;
            if (kind == Face.Display)
                t.characterSpacing = Mathf.Min(t.characterSpacing, DisplayTrackingMax);
            string flags = Fit(t);
            if (apply)
                EditorUtility.SetDirty(t);
            else
                before.Restore(t);
            return flags;
        }

        readonly struct Snapshot
        {
            readonly TMP_FontAsset _font;
            readonly Material _material;
            readonly float _tracking;
            readonly TextOverflowModes _overflow;
            public readonly float Size;

            Snapshot(TMP_Text t)
            {
                _font = t.font;
                _material = t.fontSharedMaterial;
                _tracking = t.characterSpacing;
                _overflow = t.overflowMode;
                Size = t.fontSize;
            }

            public static Snapshot Of(TMP_Text t) => new(t);

            public bool Matches(TMP_Text t) => t.fontSize == Size && t.overflowMode == _overflow;

            public void Restore(TMP_Text t)
            {
                t.font = _font;
                t.fontSharedMaterial = _material;
                t.characterSpacing = _tracking;
                t.overflowMode = _overflow;
                t.fontSize = Size;
            }
        }

        const float MaxShrink = 0.85f;   // a one-line label may shrink this far to fit its box; past that it is reported

        /// <summary>
        /// Fits a text to its box after the face changed. One line too wide: shrink it (at most to MaxShrink). One line
        /// taller than its box: TMP's Ellipsis/Truncate modes hide a line that does not fit vertically, so it is let
        /// overflow vertically (the glyphs themselves still fit; only the line's ascent and descent are taller).
        /// Wrapped text that no longer fits is reported, not changed.
        /// </summary>
        static string Fit(TMP_Text t)
        {
            bool wraps = t.textWrappingMode == TextWrappingModes.Normal;
            string sample = string.IsNullOrWhiteSpace(t.text) ? "Ag" : t.text;
            Rect box = t.rectTransform.rect;
            Vector2 want = t.GetPreferredValues(sample, wraps ? box.width : 0f, 0f);
            if (wraps)
                return want.y > box.height + 1f ? $"wrapped text overflows height by {want.y - box.height:0} px" : RuntimeNote(t);

            var notes = new List<string>();
            if (want.x > box.width + 1f && !string.IsNullOrWhiteSpace(t.text))
                notes.Add(ShrinkToWidth(t, box.width, want.x));
            if (want.y > box.height + 1f && t.overflowMode != TextOverflowModes.Overflow)
            {
                t.overflowMode = TextOverflowModes.Overflow;
                notes.Add($"line {want.y - box.height:0} px taller than its box: set to overflow so it shows");
            }
            string note = RuntimeNote(t);
            if (note.Length > 0)
                notes.Add(note);
            return string.Join("; ", notes);
        }

        static string ShrinkToWidth(TMP_Text t, float box, float want)
        {
            float scale = box / want;
            if (scale < MaxShrink)
                return $"overflows width by {want - box:0} px (too far to shrink: needs a wider box)";
            float from = t.fontSize;
            t.fontSize = Mathf.Floor(t.fontSize * scale * 10f) / 10f;
            return $"shrunk {from:0.#} to {t.fontSize:0.#} px to fit";
        }

        static string RuntimeNote(TMP_Text t) => string.IsNullOrWhiteSpace(t.text) ? "filled at run time: check in play" : "";

        static string Clean(string s) => (s ?? "").Replace("\n", " ").Replace("|", "/");

        static Face Classify(TMP_Text t)
        {
            string s = (t.text ?? "").Trim();
            if (s.Length > 0 && DataText.IsMatch(s))
                return Face.Mono;
            if (s.Length == 0 && DataName.IsMatch(t.name))
                return Face.Mono;   // filled at run time: clocks, ammo, counts
            if (t.characterSpacing >= 8f && t.fontSize <= 16f)
                return Face.Mono;   // tracked small caps: section labels, captions, microtype
            if (t.fontSize >= 26f)
                return Face.Display;
            return t.textWrappingMode == TextWrappingModes.Normal ? Face.Body : Face.Label;
        }

        static Row NewRow(TMP_Text t, string where, string face)
        {
            return new Row
            {
                Where = Path.GetFileNameWithoutExtension(where),
                Path = HierarchyPath(t.transform),
                Text = Clean(t.text),
                Size = t.fontSize,
                Tracking = t.characterSpacing,
                Face = face,
                Flags = "",
            };
        }

        static string HierarchyPath(Transform tr)
        {
            var parts = new List<string>();
            for (Transform p = tr; p != null; p = p.parent)
                parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static void WriteReport(List<Row> rows, bool applied)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Font swap {(applied ? "applied" : "report (dry run)")}");
            sb.AppendLine();
            sb.AppendLine("Written by Close the Deal > UI > Fonts. Display = Saira Expanded Light, Label = Archivo SemiExpanded Medium, Body = Archivo SemiExpanded, Mono = Martian Mono Light.");
            sb.AppendLine();
            foreach (IGrouping<string, Row> g in rows.GroupBy(r => r.Face))
                sb.AppendLine($"- {g.Key}: {g.Count()}");
            int flagged = rows.Count(r => r.Flags.Length > 0);
            sb.AppendLine($"- Flagged: {flagged} (overflow, custom material, or filled at run time)");
            sb.AppendLine();
            sb.AppendLine("| Where | Object | Text | Size | Tracking | Face | Flag |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (Row r in rows.OrderByDescending(r => r.Flags.Length > 0).ThenBy(r => r.Where))
            {
                string text = r.Text.Length > 40 ? r.Text.Substring(0, 40) + "…" : r.Text;
                sb.AppendLine($"| {r.Where} | `{r.Path}` | {text} | {r.Size:0.#} | {r.Tracking:0.#} | {r.Face} | {r.Flags} |");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString());
        }
    }
}
