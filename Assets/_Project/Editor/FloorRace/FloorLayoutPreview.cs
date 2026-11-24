using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using CloseTheDeal.FloorGen;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CloseTheDeal.Editor.FloorRace
{
    /// <summary>Draws generated floors top-down so layouts can be checked by eye, and sweeps many seeds through the generator's checks.</summary>
    public static class FloorLayoutPreview
    {
        public const string ProfileFolder = "Assets/_Project/Profiles/FloorRace";
        public const string KitPath = ProfileFolder + "/FloorKit.asset";
        public const string GenProfilePath = ProfileFolder + "/FloorGen.asset";
        const string OutFolder = "Temp/FloorGen";
        const int PixelsPerMetre = 20, Margin = 30;
        static readonly uint[] Seeds = { 1u, 2026u, 77777u, 12648430u };
        static readonly int[] Floors = { 1, 2, 5, 10 };
        const int SweepSeeds = 200;

        [MenuItem("Seal the Deal/Floor Race/Render Layout Previews")]
        static void RenderFromMenu()
        {
            string report = Render(OutFolder);
            Debug.Log(report);
            EditorUtility.RevealInFinder(Path.Combine(OutFolder, "report.txt"));
        }

        public static FloorGenProfile EnsureGenProfile()
        {
            return EnsureAsset<FloorGenProfile>(GenProfilePath);
        }

        /// <summary>Loads the asset, or creates it with its defaults when missing. Never touches an existing one.</summary>
        public static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder(ProfileFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Profiles", "FloorRace");
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log("[Floor Race] Created " + path);
            return asset;
        }

        /// <summary>Writes the PNGs and report.txt to the folder and returns the report's summary.</summary>
        public static string Render(string folder)
        {
            FloorGenProfile profile = EnsureGenProfile();
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            foreach (uint seed in Seeds)
                foreach (int floor in Floors)
                {
                    int number = Mathf.Min(floor, profile.FloorCount);
                    FloorLayout layout = FloorGenerator.Generate(seed, number, profile);
                    string name = $"floor_seed{seed}_f{number:00}.png";
                    File.WriteAllBytes(Path.Combine(folder, name), Draw(layout));
                    Describe(layout, name, report);
                }

            string sweep = Sweep(profile);
            report.Insert(0, sweep + "\n");
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            return sweep + $"\nWrote {Seeds.Length * Floors.Length} PNGs and report.txt to {Path.GetFullPath(folder)}";
        }

        static string Sweep(FloorGenProfile profile)
        {
            int total = 0, invalid = 0, maxAttempt = 0, firstTry = 0, pieces = 0, maxPieces = 0, minSpawn = int.MaxValue, mismatched = 0;
            var problems = new Dictionary<string, int>();
            var watch = Stopwatch.StartNew();
            for (uint seed = 1; seed <= SweepSeeds; seed++)
                for (int floor = 1; floor <= profile.FloorCount; floor++)
                {
                    FloorLayout layout = FloorGenerator.Generate(seed * 2654435761u, floor, profile);
                    total++;
                    if (layout.Attempt == 0) firstTry++;
                    maxAttempt = Mathf.Max(maxAttempt, layout.Attempt);
                    pieces += layout.Pieces.Count;
                    maxPieces = Mathf.Max(maxPieces, layout.Pieces.Count);
                    minSpawn = Mathf.Min(minSpawn, layout.SpawnSpots.Count);
                    if (!layout.Valid)
                    {
                        invalid++;
                        foreach (string problem in layout.Problems)
                        {
                            problems.TryGetValue(problem, out int count);
                            problems[problem] = count + 1;
                        }
                    }

                    // The same inputs must give the same floor: a second run is compared piece by piece.
                    if (floor == 1 && !Same(layout, FloorGenerator.Generate(seed * 2654435761u, floor, profile)))
                        mismatched++;
                }
            watch.Stop();

            var text = new StringBuilder();
            text.AppendLine($"SWEEP: {total} floors ({SweepSeeds} seeds x {profile.FloorCount} floors), {profile.Cols} x {profile.Rows} bays.");
            text.AppendLine($"  Passed every check: {total - invalid} of {total}. On the first attempt: {firstTry}. Most attempts needed: {maxAttempt + 1}.");
            text.AppendLine($"  Generate time: {watch.Elapsed.TotalMilliseconds / (total + SweepSeeds):0.00} ms a floor on average (Editor, includes retries).");
            text.AppendLine($"  Kit pieces a floor: {pieces / total} on average, {maxPieces} at most. Fewest spawn spots on any floor: {minSpawn}.");
            text.AppendLine($"  Repeat runs that differed: {mismatched} of {SweepSeeds}.");
            foreach (KeyValuePair<string, int> pair in problems)
                text.AppendLine($"  FAILED x{pair.Value}: {pair.Key}");
            return text.ToString();
        }

        static bool Same(FloorLayout a, FloorLayout b)
        {
            if (a.Attempt != b.Attempt || a.Pieces.Count != b.Pieces.Count || a.SpawnSpots.Count != b.SpawnSpots.Count)
                return false;
            for (int k = 0; k < a.Pieces.Count; k++)
            {
                KitPlacement p = a.Pieces[k], q = b.Pieces[k];
                if (p.Piece != q.Piece || p.X != q.X || p.Y != q.Y || p.Yaw != q.Yaw || p.Height != q.Height)
                    return false;
            }
            return true;
        }

        static void Describe(FloorLayout layout, string file, StringBuilder report)
        {
            report.AppendLine($"== {file}: seed {layout.Seed}, floor {layout.Floor}, attempt {layout.Attempt + 1}, {(layout.Valid ? "PASSED" : "FAILED")}");
            foreach (string problem in layout.Problems)
                report.AppendLine("   PROBLEM: " + problem);
            report.AppendLine($"   {layout.Pieces.Count} kit pieces, {layout.SpawnSpots.Count} spawn spots, {layout.LootSpots.Count} loot spots, {layout.PropSpots.Count} prop spots.");
            report.AppendLine($"   Walking metres: start A to goal {layout.StartAToGoal:0.0}, start B to goal {layout.StartBToGoal:0.0}, starts apart {layout.StartsApart:0.0}.");
            if (layout.HasBriefcase)
                report.AppendLine($"   Briefcase: {layout.CaseToGoal:0.0} m from the lift, {layout.CaseToStartA:0.0} m from A, {layout.CaseToStartB:0.0} m from B, in {layout.Rooms[layout.Briefcase.Room].Name}.");
            foreach (FloorRoom room in layout.Rooms)
                if (room.Cells.Count > 0)
                    report.AppendLine($"   [{room.Index}] {room.Name}: {room.Cells.Count} bays, {(room.IsCorridor ? "corridor" : room.Open ? "open plan" : room.Front + " walls")}, {room.Exits} exits");
        }

        static Color32 RoomColour(FloorRoom room) => room.Kind switch
        {
            RoomKind.Ring => new Color32(238, 240, 245, 255),
            RoomKind.Court => new Color32(225, 232, 245, 255),
            RoomKind.WindowHall => new Color32(214, 236, 250, 255),
            RoomKind.Lounge => new Color32(222, 245, 228, 255),
            RoomKind.OpenOffice => new Color32(205, 214, 240, 255),
            RoomKind.Meeting => new Color32(226, 212, 240, 255),
            RoomKind.Kitchen => new Color32(250, 232, 205, 255),
            RoomKind.Print => new Color32(240, 240, 205, 255),
            RoomKind.Archive => new Color32(228, 220, 205, 255),
            _ => new Color32(120, 150, 240, 255)
        };

        static Color32 EdgeColour(EdgeKind kind) => kind switch
        {
            EdgeKind.Solid => new Color32(20, 24, 40, 255),
            EdgeKind.FacadeSolid => new Color32(20, 24, 40, 255),
            EdgeKind.FacadeCurtain => new Color32(0, 160, 200, 255),
            EdgeKind.FacadeWindow => new Color32(230, 40, 40, 255),
            EdgeKind.Shaft => new Color32(110, 60, 140, 255),
            EdgeKind.LiftDoor => new Color32(110, 60, 140, 255),
            _ => new Color32(40, 90, 200, 255)
        };

        static byte[] Draw(FloorLayout layout)
        {
            int width = (int)(layout.Width * PixelsPerMetre) + 2 * Margin, height = (int)(layout.Depth * PixelsPerMetre) + 2 * Margin;
            var pixels = new Color32[width * height];
            var canvas = new Canvas(pixels, width, height);
            canvas.Fill(new Color32(255, 255, 255, 255));
            const float C = FloorLayout.Cell;

            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                {
                    int room = layout.RoomOf[layout.CellIndex(i, j)];
                    canvas.Rect(i * C, j * C, (i + 1) * C, (j + 1) * C, room >= 0 ? RoomColour(layout.Rooms[room]) : new Color32(250, 215, 90, 255));
                }
            if (layout.HasArrivalLift)
                canvas.Rect(layout.ArrivalLift.CellX * C, layout.ArrivalLift.CellY * C, (layout.ArrivalLift.CellX + 1) * C,
                            (layout.ArrivalLift.CellY + 1) * C, new Color32(250, 160, 70, 255));

            FloorNav nav = layout.Nav;
            for (int n = 0; n < nav.Count; n++)
                if (nav.Blocked[n])
                    canvas.Rect(nav.X(n) - 0.25f, nav.Y(n) - 0.25f, nav.X(n) + 0.25f, nav.Y(n) + 0.25f, new Color32(150, 155, 165, 255));

            for (int j = 0; j <= layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                    Edge(canvas, layout.NorthEdges[i + j * layout.Cols], i * C, j * C, true);
            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i <= layout.Cols; i++)
                    Edge(canvas, layout.WestEdges[i + j * (layout.Cols + 1)], i * C, j * C, false);

            foreach (FloorSpot spot in layout.SpawnSpots) canvas.Dot(spot.X, spot.Y, 2, new Color32(120, 120, 120, 255));
            foreach (FloorSpot spot in layout.LootSpots) canvas.Dot(spot.X, spot.Y, 3, new Color32(230, 140, 0, 255));
            foreach (PropSpot spot in layout.PropSpots) canvas.Dot(spot.X, spot.Y, 2, new Color32(140, 90, 40, 255));
            foreach (FloorSpot spot in layout.StartA) canvas.Dot(spot.X, spot.Y, 5, new Color32(0, 170, 60, 255));
            foreach (FloorSpot spot in layout.StartB) canvas.Dot(spot.X, spot.Y, 5, new Color32(210, 0, 190, 255));
            if (layout.HasBriefcase) canvas.Dot(layout.Briefcase.X, layout.Briefcase.Y, 7, new Color32(200, 150, 0, 255));
            if (layout.HasExitLift) canvas.Dot(layout.ExitLift.FrontX, layout.ExitLift.FrontY, 4, new Color32(110, 60, 140, 255));

            foreach (FloorRoom room in layout.Rooms)
            {
                if (room.Cells.Count == 0)
                    continue;
                int cell = room.Cells[0];
                canvas.Number(room.Index, (cell % layout.Cols) * C + 0.3f, (cell / layout.Cols) * C + 0.3f, new Color32(0, 0, 0, 255));
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        static void Edge(Canvas canvas, FloorEdge edge, float x, float y, bool alongX)
        {
            if (edge.Kind == EdgeKind.Open)
                return;

            int door = edge.Kind == EdgeKind.LiftDoor ? 2 : edge.DoorAt;
            float half = edge.Kind == EdgeKind.Solid || edge.Kind >= EdgeKind.FacadeWindow ? 0.14f : 0.08f;
            Color32 colour = EdgeColour(edge.Kind);
            void Piece(float from, float to)
            {
                if (alongX) canvas.Rect(x + from, y - half, x + to, y + half, colour);
                else canvas.Rect(x - half, y + from, x + half, y + to, colour);
            }

            if (door == 0)
            {
                Piece(0f, FloorLayout.Cell);
                return;
            }

            Piece(0f, door - 0.6f);
            Piece(door + 0.6f, FloorLayout.Cell);
        }

        sealed class Canvas
        {
            readonly Color32[] _pixels;
            readonly int _width, _height;

            static readonly string[] Digits =
            {
                "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
                "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111"
            };

            public Canvas(Color32[] pixels, int width, int height) { _pixels = pixels; _width = width; _height = height; }

            public void Fill(Color32 colour)
            {
                for (int n = 0; n < _pixels.Length; n++) _pixels[n] = colour;
            }

            void Set(int px, int py, Color32 colour)
            {
                if (px >= 0 && py >= 0 && px < _width && py < _height)
                    _pixels[px + (_height - 1 - py) * _width] = colour;
            }

            public void Rect(float x0, float y0, float x1, float y1, Color32 colour)
            {
                int a0 = Margin + Mathf.RoundToInt(x0 * PixelsPerMetre), a1 = Margin + Mathf.RoundToInt(x1 * PixelsPerMetre);
                int b0 = Margin + Mathf.RoundToInt(y0 * PixelsPerMetre), b1 = Margin + Mathf.RoundToInt(y1 * PixelsPerMetre);
                for (int py = b0; py < b1; py++)
                    for (int px = a0; px < a1; px++)
                        Set(px, py, colour);
            }

            public void Dot(float x, float y, int radius, Color32 colour)
            {
                int cx = Margin + Mathf.RoundToInt(x * PixelsPerMetre), cy = Margin + Mathf.RoundToInt(y * PixelsPerMetre);
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                        if (dx * dx + dy * dy <= radius * radius)
                            Set(cx + dx, cy + dy, colour);
            }

            public void Number(int value, float x, float y, Color32 colour)
            {
                const int Scale = 2;
                int px = Margin + Mathf.RoundToInt(x * PixelsPerMetre), py = Margin + Mathf.RoundToInt(y * PixelsPerMetre);
                foreach (char c in value.ToString())
                {
                    string glyph = Digits[c - '0'];
                    for (int row = 0; row < 5; row++)
                        for (int col = 0; col < 3; col++)
                            if (glyph[row * 3 + col] == '1')
                                for (int sy = 0; sy < Scale; sy++)
                                    for (int sx = 0; sx < Scale; sx++)
                                        Set(px + col * Scale + sx, py + row * Scale + sy, colour);
                    px += 4 * Scale;
                }
            }
        }
    }
}
