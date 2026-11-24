using System.Collections.Generic;
using System.IO;
using System.Text;
using CloseTheDeal.FloorGen;
using CloseTheDeal.FloorRace;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CloseTheDeal.Editor.FloorRace
{
    /// <summary>Play Mode: stands a whole building's floors side by side in the open scene and tests each one as built. Design: docs/systems/FLOOR_GEN.md.</summary>
    public static class FloorGallery
    {
        public const string RootName = "Floor Gallery";
        const string ReportPath = "Temp/FloorGen/gallery_report.txt";
        const int PerRow = 5;
        const float Gap = 12f, Above = 1000f;
        const float BodyRadius = 0.3f, BodyLow = 0.45f, BodyHigh = 1.65f;
        const float GroundProbe = 1f, GroundReach = 1.3f, GroundSlack = 0.08f;
        const float Headroom = 1.9f, StepLimit = 0.75f;

        [MenuItem("Seal the Deal/Floor Race/Build Ten Floors and Check (Play Mode)")]
        static void BuildFromMenu()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Build Ten Floors", "Enter Play Mode first: floors are built the way the game builds them.", "OK");
                return;
            }

            Debug.Log(Build((uint)System.Environment.TickCount));
        }

        /// <summary>Builds every floor of one seed's building in a grid, replacing the last gallery, and returns the report it also writes to Temp/FloorGen.</summary>
        public static string Build(uint seed)
        {
            FloorBuildMeasure.FillKit();
            GameObject old = GameObject.Find(RootName);
            if (old != null)
            {
                old.SetActive(false);
                Object.Destroy(old);
            }

            FloorGenProfile profile = FloorLayoutPreview.EnsureGenProfile();
            var go = new GameObject(RootName);
            go.transform.position = new Vector3(0f, Above, 0f);
            var builder = go.AddComponent<FloorBuilder>();
            var serialized = new SerializedObject(builder);
            serialized.FindProperty("_kit").objectReferenceValue = AssetDatabase.LoadAssetAtPath<FloorKit>(FloorLayoutPreview.KitPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var floors = new List<BuiltFloor>();
            for (int floor = 1; floor <= profile.FloorCount; floor++)
            {
                FloorLayout layout = FloorGenerator.Generate(seed, floor, profile);
                BuiltFloor built = builder.Build(layout);
                int slot = floor - 1;
                built.Root.transform.localPosition = new Vector3(slot % PerRow * (layout.Width + Gap), 0f, -(slot / PerRow) * (layout.Depth + Gap));
                floors.Add(built);
            }

            Physics.SyncTransforms();
            var report = new StringBuilder();
            report.AppendLine($"FLOOR GALLERY: seed {seed}, floors 1 to {profile.FloorCount}, {profile.Storeys} storey(s), built side by side under \"{RootName}\".");
            int faulty = 0, bareTotal = 0, baysTotal = 0;
            var faults = new List<string>();
            foreach (BuiltFloor built in floors)
            {
                faults.Clear();
                Check(built, faults);
                FloorLayout layout = built.Layout;
                BareBays(layout, out int bare, out int bays);
                bareTotal += bare;
                baysTotal += bays;
                report.AppendLine($"Floor {layout.Floor:00}  {layout.PlanType,-10}  {built.Objects} pieces, {bare} of {bays} room bays bare, {layout.SpawnSpots.Count} spawn spots, {layout.Stairs.Count} stairs, " +
                                  $"exit lift {(layout.HasExitLift ? (layout.ExitLift.Level == 0 ? "downstairs" : "upstairs") : "none")}, attempt {layout.Attempt + 1}: " +
                                  (faults.Count == 0 ? "OK" : faults.Count + " FAULT(S)"));
                foreach (string fault in faults)
                    report.AppendLine("    " + fault);
                if (faults.Count > 0)
                    faulty++;
            }

            report.Insert(0, $"{floors.Count - faulty} of {floors.Count} floors passed every built check. Room bays with no furniture: {bareTotal} of {baysTotal}.\n");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());
            return report.ToString();
        }

        // Bays of rooms and halls, on every storey, that hold nothing a player would have to walk round.
        static void BareBays(FloorLayout layout, out int bare, out int bays)
        {
            bare = 0;
            bays = 0;
            FloorNav nav = layout.Nav;
            const int P = FloorNav.PerCell;
            for (int level = 0; level < layout.Levels; level++)
                for (int j = 0; j < layout.Rows; j++)
                    for (int i = 0; i < layout.Cols; i++)
                    {
                        int room = layout.RoomAt(i, j);
                        if (room < 0 || layout.Rooms[room].IsCorridor)
                            continue;

                        bool furnished = false;
                        for (int b = 0; b < P && !furnished; b++)
                            for (int a = 0; a < P && !furnished; a++)
                                furnished = nav.Blocked[nav.Index(i * P + a, j * P + b, level)];
                        bays++;
                        if (!furnished)
                            bare++;
                    }
        }

        static void Check(BuiltFloor built, List<string> faults)
        {
            FloorLayout layout = built.Layout;
            foreach (string problem in layout.Problems)
                faults.Add("Generator: " + problem);
            FloorCeiling.Check(layout, faults);

            int buried = 0, floating = 0;
            Stand(built, layout.SpawnSpots, "spawn spot", faults, ref buried, ref floating);
            Stand(built, layout.StartA, "start A spot", faults, ref buried, ref floating);
            Stand(built, layout.StartB, "start B spot", faults, ref buried, ref floating);
            foreach (FloorSpot spot in layout.LootSpots)
                if (!Grounded(built, spot))
                    faults.Add($"A loot spot has no floor under it at ({spot.X:0.0}, {spot.Y:0.0}) on storey {spot.Level}.");
            if (layout.HasBriefcase && !Grounded(built, layout.Briefcase))
                faults.Add("The briefcase spot has no floor under it.");
            foreach (PropSpot spot in layout.PropSpots)
                if (!Physics.Raycast(built.World(spot.X, spot.Y, spot.Height + 0.3f), Vector3.down, GroundReach, ~0, QueryTriggerInteraction.Ignore))
                    faults.Add($"A loose prop spot has nothing under it at ({spot.X:0.0}, {spot.Y:0.0}), height {spot.Height:0.0}.");
            if (buried > 0)
                faults.Add($"{buried} places to stand are inside something.");
            if (floating > 0)
                faults.Add($"{floating} places to stand have no floor under them.");

            foreach (FloorStair stair in layout.Stairs)
                Climb(built, stair, faults);
        }

        static void Stand(BuiltFloor built, List<FloorSpot> spots, string what, List<string> faults, ref int buried, ref int floating)
        {
            foreach (FloorSpot spot in spots)
            {
                // Players start standing in a lift car, which is a box they are meant to be inside.
                if (spot.Room != FloorLayout.NoRoom && Physics.CheckCapsule(built.World(spot, BodyLow), built.World(spot, BodyHigh), BodyRadius, ~0, QueryTriggerInteraction.Ignore))
                {
                    buried++;
                    if (buried <= 3)
                        faults.Add($"A {what} is inside something at ({spot.X:0.0}, {spot.Y:0.0}) on storey {spot.Level}.");
                }

                if (!Grounded(built, spot))
                    floating++;
            }
        }

        static bool Grounded(BuiltFloor built, FloorSpot spot)
        {
            if (!Physics.Raycast(built.World(spot, GroundProbe), Vector3.down, out RaycastHit hit, GroundReach, ~0, QueryTriggerInteraction.Ignore))
                return false;
            return Mathf.Abs(built.Plan(hit.point).z - spot.Level * FloorLayout.StoreyHeight) <= GroundSlack || spot.Room == FloorLayout.NoRoom;
        }

        // Walks the flight a quarter metre at a time from the storey below to the one above.
        static void Climb(BuiltFloor built, FloorStair stair, List<string> faults)
        {
            int dx = Side.Dx[stair.Dir], dy = Side.Dy[stair.Dir];
            float midX = (stair.X0 + stair.X1) * 0.5f, midY = (stair.Y0 + stair.Y1) * 0.5f, half = FloorLayout.Cell;
            string where = $"The stair at bay ({stair.CellX}, {stair.CellY})";
            float last = 0f;
            for (float along = -half - 0.5f; along <= half + 0.5f; along += 0.25f)
            {
                Vector3 top = built.World(midX + dx * along, midY + dy * along, FloorLayout.StoreyHeight + 1.5f);
                if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, FloorLayout.StoreyHeight + 2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    faults.Add($"{where} has nothing to stand on {along + half:0.0} m along.");
                    return;
                }

                float height = built.Plan(hit.point).z;
                // The glass across the end over the flight's foot is the only thing that may stand above the storey below there.
                if (along < -half + FloorLayout.StairLead)
                    continue;
                if (height < last - 0.05f || height - last > StepLimit)
                {
                    faults.Add($"{where} jumps from {last:0.00} m to {height:0.00} m, {along + half:0.0} m along.");
                    return;
                }
                if (Physics.Raycast(hit.point + Vector3.up * 0.05f, Vector3.up, Headroom, ~0, QueryTriggerInteraction.Ignore))
                {
                    faults.Add($"{where} has less than {Headroom} m of headroom {along + half:0.0} m along.");
                    return;
                }
                last = height;
            }

            if (Mathf.Abs(last - FloorLayout.StoreyHeight) > GroundSlack)
                faults.Add($"{where} ends at {last:0.00} m, not on the upper floor.");
            float walkX = (stair.WalkX0 + stair.WalkX1) * 0.5f, walkY = (stair.WalkY0 + stair.WalkY1) * 0.5f;
            if (!Physics.Linecast(built.World(walkX, walkY, FloorLayout.StoreyHeight + 0.6f), built.World(midX, midY, FloorLayout.StoreyHeight + 0.6f), ~0, QueryTriggerInteraction.Ignore))
                faults.Add($"{where} has no guard between the upper walkway and the hole.");
        }
    }
}
