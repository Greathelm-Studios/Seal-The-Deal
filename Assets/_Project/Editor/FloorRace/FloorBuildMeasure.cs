using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using CloseTheDeal.Editor.Art;
using CloseTheDeal.FloorGen;
using CloseTheDeal.FloorRace;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CloseTheDeal.Editor.FloorRace
{
    /// <summary>Play Mode measuring run: builds and tears down generated floors, recording build time, draw calls and anything left behind.</summary>
    public static class FloorBuildMeasure
    {
        const string KitPrefabFolder = "Assets/_Project/Art/Kit/Prefabs";
        const string BelowMaterialPath = "Assets/_Project/Art/Kit/Materials/Wall_PaperWhite.mat";
        const string ReportPath = "Temp/FloorGen/build_report.txt";
        const string SignFontPath = "Assets/_Project/UI/Fonts/SairaExpanded-Light SDF.asset";
        const int PosterCount = 12;
        const string RingBandPath = "Assets/_Project/Art/Signs/Band_Cobalt_Inlay.mat";
        // ArtSource/Kit/posters/COPY.md, "Wall text".
        static readonly string[] DefaultSlogans =
        {
            "TEAM PLAYERS ONLY", "NOBODY GETS BLAMED ALONE", "THE WORK OF THREE. THE SALARY OF ONE.", "OUR DOOR IS ALWAYS OPEN. PLEASE CLOSE IT.",
            "OVERTIME IS ITS OWN REWARD", "WORK · LIFE · WORK", "WORK FROM ANYWHERE. ALWAYS.", "WE PAY IN EXPOSURE, PIZZA AND EXPERIENCE",
            "YOUR WORK MATTERS (TO US)", "SMILE. IT'S BILLABLE.", "ONE BIG FAMILY", "DYSFUNCTION INCLUDED", "MANDATORY FUN BEGINS AT 17:00 SHARP",
            "GOOD ENOUGH IS THE NEW EXCELLENT", "LUNCH IS A MEETING", "STAY HUNGRY", "SAME MEETING. NEW TEMPLATE.",
            "WE'LL REMEMBER YOU AT THE NEXT RESTRUCTURING", "EVERY DEADLINE WAS YESTERDAY", "PASSION IS UNPAID"
        };
        const int SettleFrames = 4, ViewsPerFloor = 4;
        const float Eye = 1.6f;
        static readonly uint[] Seeds = { 1u, 2026u, 77777u };
        static readonly int[] Floors = { 1, 5, 10 };

        [MenuItem("Seal the Deal/Floor Race/Fill Floor Kit")]
        static void FillFromMenu() => Debug.Log(FillKit());

        /// <summary>Creates FloorKit.asset if missing and fills its empty slots from the kit prefabs by name. Never replaces a slot someone set.</summary>
        public static string FillKit()
        {
            FloorKit kit = FloorLayoutPreview.EnsureAsset<FloorKit>(FloorLayoutPreview.KitPath);
            string[] names = System.Enum.GetNames(typeof(KitPiece));
            if (kit.Prefabs.Length != names.Length)
                System.Array.Resize(ref kit.Prefabs, names.Length);

            int filled = 0;
            var missing = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                if (kit.Prefabs[i] != null)
                    continue;
                kit.Prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitPrefabFolder}/{names[i]}.prefab");
                if (kit.Prefabs[i] != null) filled++;
                else missing.Add(names[i]);
            }

            if (kit.BuildingBelow == null)
                kit.BuildingBelow = AssetDatabase.LoadAssetAtPath<Material>(BelowMaterialPath);
            if (kit.Labels == null || kit.Labels.Length == 0)
                kit.Labels = KitOfficeLabels(missing);
            if (kit.Posters == null || kit.Posters.Length == 0)
                kit.Posters = Posters(missing);
            if (kit.RingBand == null)
                kit.RingBand = AssetDatabase.LoadAssetAtPath<Material>(RingBandPath);
            if (kit.SloganBand == null)
                kit.SloganBand = AssetDatabase.LoadAssetAtPath<Material>(BelowMaterialPath);
            if (kit.Slogans == null || kit.Slogans.Length == 0)
                kit.Slogans = (string[])DefaultSlogans.Clone();
            if (kit.SignFont == null)
                kit.SignFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(SignFontPath);
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssetIfDirty(kit);
            return $"[Floor Race] Floor Kit: filled {filled} empty slots of {names.Length}." +
                   (missing.Count > 0 ? " Not found: " + string.Join(", ", missing) + ". Run Import Kit, then Build Kit Office Scene for the sign materials." : "");
        }

        // The lift frame's label reads "1F", which is wrong on nine floors out of ten.
        static PieceLabel[] KitOfficeLabels(List<string> missing)
        {
            var labels = new List<PieceLabel>();
            foreach (var (piece, material, centre, size, back) in KitSigns.PieceLabels())
            {
                if (piece.StartsWith("Lift_") || !System.Enum.TryParse(piece, out KitPiece kitPiece))
                    continue;
                var loaded = AssetDatabase.LoadAssetAtPath<Material>($"{KitSigns.MaterialFolder}/{material}.mat");
                if (loaded == null)
                {
                    missing.Add(material + ".mat");
                    continue;
                }
                labels.Add(new PieceLabel { Piece = kitPiece, Material = loaded, Centre = centre, Size = size, Back = back });
            }
            return labels.ToArray();
        }

        static Material[] Posters(List<string> missing)
        {
            var posters = new List<Material>();
            for (int k = 1; k <= PosterCount; k++)
            {
                var poster = AssetDatabase.LoadAssetAtPath<Material>($"{KitSigns.MaterialFolder}/Poster_{k:00}.mat");
                if (poster != null) posters.Add(poster);
                else missing.Add($"Poster_{k:00}.mat");
            }
            return posters.ToArray();
        }

        [MenuItem("Seal the Deal/Floor Race/Measure Floor Build (Play Mode)")]
        static void MeasureFromMenu()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Measure Floor Build", "Enter Play Mode first: floors are built the way the game builds them.", "OK");
                return;
            }
            Begin();
        }

        public static bool Running { get; private set; }

        sealed class Run
        {
            public FloorBuilder Builder;
            public Camera Camera;
            public readonly List<Camera> Disabled = new();
            public readonly StringBuilder Report = new();
            public int Case, View, Wait;
            public bool Merge;
            public BuiltFloor Floor;
            public int Batches, MaxBatches, SetPass, Triangles;
            public double GenerateMs;
            public int MeshesBefore, ObjectsBefore;
        }

        static Run _run;

        /// <summary>Starts the measuring run; it steps over several frames and writes Temp/FloorGen/build_report.txt when done.</summary>
        public static void Begin()
        {
            if (Running || !Application.isPlaying)
                return;

            FillKit();
            var go = new GameObject("Floor Build Measure");
            go.transform.position = new Vector3(0f, 500f, 0f);
            var run = new Run { Builder = go.AddComponent<FloorBuilder>() };
            var serialized = new SerializedObject(run.Builder);
            serialized.FindProperty("_kit").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FloorKit>(FloorLayoutPreview.KitPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            foreach (Camera other in Camera.allCameras)
            {
                other.enabled = false;
                run.Disabled.Add(other);
            }
            run.Camera = new GameObject("Measure Camera").AddComponent<Camera>();
            run.Camera.transform.SetParent(go.transform, false);
            run.Camera.fieldOfView = 75f;
            run.MeshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            run.ObjectsBefore = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
            run.Report.AppendLine("FLOOR BUILD MEASURE (Editor Play Mode). Draw calls are the Game view's draw calls from one camera at eye height, at four spawn spots a floor.");
            run.Report.AppendLine("seed\tfloor\tmerged\tgenerate ms\tbuild ms\tteardown ms\tobjects\tcolliders\tbatches avg\tbatches max\tset-pass avg\ttriangles avg");

            _run = run;
            Running = true;
            EditorApplication.update += Step;
            StartCase();
        }

        static void StartCase()
        {
            Run run = _run;
            int total = Seeds.Length * Floors.Length;
            run.Merge = run.Case >= total;
            int index = run.Case % total;
            uint seed = Seeds[index / Floors.Length];
            int floor = Floors[index % Floors.Length];

            var serialized = new SerializedObject(run.Builder);
            serialized.FindProperty("_mergeStillPieces").boolValue = run.Merge;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            FloorGenProfile profile = FloorLayoutPreview.EnsureGenProfile();
            Stopwatch watch = Stopwatch.StartNew();
            FloorLayout layout = FloorGenerator.Generate(seed, Mathf.Min(floor, profile.FloorCount), profile);
            run.GenerateMs = watch.Elapsed.TotalMilliseconds;
            run.Floor = run.Builder.Build(layout);
            run.View = 0;
            run.Batches = run.MaxBatches = run.SetPass = run.Triangles = 0;
            Aim();
        }

        static void Aim()
        {
            Run run = _run;
            FloorLayout layout = run.Floor.Layout;
            FloorSpot spot = layout.SpawnSpots[run.View * (layout.SpawnSpots.Count - 1) / (ViewsPerFloor - 1)];
            Vector3 eye = run.Floor.World(spot.X, spot.Y, Eye);
            Vector3 centre = run.Floor.World(layout.Width * 0.5f, layout.Depth * 0.5f, Eye);
            run.Camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(centre - eye));
            run.Wait = SettleFrames;
        }

        static void Step()
        {
            Run run = _run;
            if (!Application.isPlaying)
            {
                Finish(aborted: true);
                return;
            }
            if (--run.Wait > 0)
                return;

            run.Batches += UnityStats.drawCalls;
            run.MaxBatches = Mathf.Max(run.MaxBatches, UnityStats.drawCalls);
            run.SetPass += UnityStats.setPassCalls;
            run.Triangles += UnityStats.triangles;
            if (++run.View < ViewsPerFloor)
            {
                Aim();
                return;
            }

            FloorLayout layout = run.Floor.Layout;
            int colliders = run.Floor.Root.GetComponentsInChildren<Collider>().Length;
            double buildMs = run.Floor.BuildMilliseconds;
            int objects = run.Floor.Objects;
            Stopwatch watch = Stopwatch.StartNew();
            run.Builder.TearDown(run.Floor);
            double tearMs = watch.Elapsed.TotalMilliseconds;
            run.Report.AppendLine($"{layout.Seed}\t{layout.Floor}\t{(run.Merge ? "yes" : "no")}\t{run.GenerateMs:0.0}\t{buildMs:0.0}\t{tearMs:0.00}\t{objects}\t{colliders}\t" +
                                  $"{run.Batches / ViewsPerFloor}\t{run.MaxBatches}\t{run.SetPass / ViewsPerFloor}\t{run.Triangles / ViewsPerFloor}");

            if (++run.Case < 2 * Seeds.Length * Floors.Length)
            {
                StartCase();
                run.Wait += 2; // Let the last floor's Destroy land before the next reading.
                return;
            }

            run.Wait = SettleFrames;
            EditorApplication.update -= Step;
            EditorApplication.update += Settle;
        }

        static void Settle()
        {
            if (Application.isPlaying && --_run.Wait > 0)
                return;
            EditorApplication.update -= Settle;
            Finish(aborted: !Application.isPlaying);
        }

        static void Finish(bool aborted)
        {
            Run run = _run;
            EditorApplication.update -= Step;
            Running = false;
            _run = null;
            if (aborted)
            {
                Debug.LogWarning("[Floor Race] Measure stopped: Play Mode ended.");
                return;
            }

            Object.Destroy(run.Camera.gameObject);
            int left = run.Builder.GetComponentsInChildren<Transform>(true).Length - 2;
            int meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length - run.MeshesBefore;
            run.Report.AppendLine($"After every tear-down: {left} objects left under the builder, {meshes} more meshes in memory than before the run.");
            foreach (Camera other in run.Disabled)
                if (other != null)
                    other.enabled = true;
            Object.Destroy(run.Builder.gameObject);

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, run.Report.ToString());
            Debug.Log(run.Report.ToString());
        }
    }
}
