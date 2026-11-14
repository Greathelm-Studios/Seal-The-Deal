using System;
using System.Collections.Generic;
using System.IO;
using CloseTheDeal.Editor.Greybox;
using CloseTheDeal.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Lanes
{
    /// <summary>
    /// Builds the movement lane's test scene: a two-floor slice of a tower built to the floor
    /// convention (spiral stairs, 0.6 m window sill, a slab edge), a street outside the
    /// windows, and a small course of things to climb, vault, duck under and be refused by. Like the greybox tools it only adds what is missing and
    /// never rebuilds something already in the scene, so anything moved by hand stays moved.
    /// The other entry points run the scripted movement check (PlayerMovementSession) from
    /// the shell; nothing here runs unless invoked.
    /// </summary>
    public static class MovementLaneSetup
    {
        const string SceneFolder = "Assets/_Project/Scenes/Lanes";
        const string ScenePath = SceneFolder + "/Lane_Movement.unity";
        const string FlightMeshPath = SceneFolder + "/Lane_Movement_Flight.asset";
        const string TowerProfilePath = "Assets/_Project/Profiles/DefaultTower.asset";

        // The same numbers GreyboxTowerSetup builds floors to.
        const float Slab = 0.2f;
        const float Wall = 0.2f;
        const float SillHeight = 0.6f;
        const float PillarWidth = 0.4f;
        const float PillarSpacing = 4f;
        const float StairRadius = 2.6f;
        const float ColumnRadius = 0.8f;
        const float FlightThickness = 0.25f;
        const int FlightSegments = 48;
        const float CageGap = 0.15f;
        const float StairBackClearance = 1.2f;

        [MenuItem("Close the Deal/Lanes/Set Up Movement")]
        public static void SetUp()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = OpenOrCreateScene();

            var tower = AssetDatabase.LoadAssetAtPath<TowerProfile>(TowerProfilePath);
            if (tower == null)
            {
                Debug.LogError("[Movement lane] No tower profile at " + TowerProfilePath + "; run Close the Deal > Greybox > Set Up Scene once first.");
                return;
            }

            EnsureSlice(tower);
            EnsureCourse(tower);
            GreyboxSceneSetup.EnsurePlayRig(new Vector3(-2.8f, 0.1f, -2.5f), new Vector3(2.8f, 0.1f, -2.5f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Movement lane] Scene set up and saved: " + ScenePath);
        }

        static Scene OpenOrCreateScene()
        {
            if (File.Exists(ScenePath))
            {
                Scene active = SceneManager.GetActiveScene();
                return active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Scenes", "Lanes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        /// <summary>
        /// A lobby floor, the floor above it and the spiral flight between them, to the numbers
        /// in docs/systems/TOWER.md and GreyboxTowerSetup (28 m wide, 0.6 m sill, treads from
        /// 0.8 to 2.6 m around the column, one turn per floor), plus the street the lobby
        /// windows open onto. Built here rather than from the floor templates because the
        /// committed templates still hold the older straight-ramp stairs.
        /// </summary>
        static void EnsureSlice(TowerProfile tower)
        {
            if (GameObject.Find("Slice") != null)
                return;

            Transform slice = new GameObject("Slice").transform;
            float w = tower.Width, h = tower.FloorHeight, half = w * 0.5f;
            float depth = tower.MainDepth + tower.SpineDepth;

            Block(slice, "Floor", new Vector3(0f, -Slab * 0.5f, -depth * 0.5f), new Vector3(w, Slab, depth));
            Block(slice, "Wall back", new Vector3(0f, h, -depth + Wall * 0.5f), new Vector3(w, h * 2f, Wall));
            Block(slice, "Wall left", new Vector3(-half + Wall * 0.5f, h, -depth * 0.5f), new Vector3(Wall, h * 2f, depth));
            Block(slice, "Wall right", new Vector3(half - Wall * 0.5f, h, -depth * 0.5f), new Vector3(Wall, h * 2f, depth));

            Block(slice, "Sill", new Vector3(0f, SillHeight * 0.5f, -Wall * 0.5f), new Vector3(w, SillHeight, Wall));
            float pillarHeight = h - SillHeight;
            for (float x = -half; x <= half + 0.01f; x += PillarSpacing)
            {
                float clampedX = Mathf.Clamp(x, -half + PillarWidth * 0.5f, half - PillarWidth * 0.5f);
                Block(slice, "Pillar", new Vector3(clampedX, SillHeight + pillarHeight * 0.5f, -Wall * 0.5f), new Vector3(PillarWidth, pillarHeight, Wall));
            }

            // The lobby is at street level; without this a vault over the sill is a fall into nothing.
            Block(slice, "Street", new Vector3(0f, -Slab * 0.5f, 12f), new Vector3(w + 12f, Slab, 24f));

            Vector3 centre = StairCentre(tower);
            BuildUpperFloor(slice, tower, centre);
            BuildStairs(slice, tower, centre);
        }

        /// <summary>Where the stairwell stands: centred across the floor, a walkway's width in front of the spine.</summary>
        static Vector3 StairCentre(TowerProfile tower)
        {
            float outer = StairRadius + CageGap;
            return new Vector3(0f, 0f, Mathf.Min(-tower.MainDepth + outer + StairBackClearance, -outer - 1f));
        }

        /// <summary>The slab above, with a squared-off hole over the treads whose corners are filled at 45°.</summary>
        static void BuildUpperFloor(Transform slice, TowerProfile tower, Vector3 c)
        {
            Transform upper = new GameObject("Upper floor").transform;
            upper.SetParent(slice, false);
            upper.localPosition = new Vector3(0f, tower.FloorHeight, 0f);

            float half = tower.Width * 0.5f, depth = tower.MainDepth + tower.SpineDepth;
            float y = -Slab * 0.5f, r = StairRadius;
            float front = c.z + r, back = c.z - r;
            Block(upper, "Slab front", new Vector3(0f, y, front * 0.5f), new Vector3(tower.Width, Slab, -front));
            Block(upper, "Slab behind stairs", new Vector3(0f, y, (back - depth) * 0.5f), new Vector3(tower.Width, Slab, back + depth));
            Block(upper, "Slab left of stairs", new Vector3((-half - r) * 0.5f, y, c.z), new Vector3(half - r, Slab, 2f * r));
            Block(upper, "Slab right of stairs", new Vector3((half + r) * 0.5f, y, c.z), new Vector3(half - r, Slab, 2f * r));
            for (int i = 0; i < 4; i++)
            {
                float angle = 45f + i * 90f;
                GameObject corner = Block(upper, "Slab stair corner", c + Spoke(angle) * (r * 1.5f) + new Vector3(0f, y, 0f), new Vector3(r, Slab, r));
                corner.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        static void BuildStairs(Transform slice, TowerProfile tower, Vector3 centre)
        {
            Transform stairs = new GameObject("Stairs").transform;
            stairs.SetParent(slice, false);
            stairs.localPosition = centre;

            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Column";
            column.transform.SetParent(stairs, false);
            column.transform.localPosition = new Vector3(0f, tower.FloorHeight * 0.5f, 0f);
            column.transform.localScale = new Vector3(ColumnRadius * 2f, tower.FloorHeight * 0.5f, ColumnRadius * 2f);
            // A cylinder primitive comes with a capsule collider, whose round ends would poke through both floors.
            UnityEngine.Object.DestroyImmediate(column.GetComponent<Collider>());
            column.AddComponent<MeshCollider>().convex = true;

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(FlightMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Lane_Movement_Flight" };
                FillFlightMesh(mesh, tower.FloorHeight);
                AssetDatabase.CreateAsset(mesh, FlightMeshPath);
            }

            var flight = new GameObject("Flight");
            flight.transform.SetParent(stairs, false);
            flight.AddComponent<MeshFilter>().sharedMesh = mesh;
            flight.AddComponent<MeshRenderer>().sharedMaterial = column.GetComponent<MeshRenderer>().sharedMaterial;
            flight.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Unit spoke from the column: 0° toward the windows (+Z), clockwise from above, the way the flight climbs.</summary>
        static Vector3 Spoke(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }

        /// <summary>One flight: a solid helical ramp from the column to the outer radius, one full turn rising one floor.</summary>
        static void FillFlightMesh(Mesh mesh, float rise)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            Vector3 Point(int i, float radius, bool top)
            {
                float t = i / (float)FlightSegments;
                Vector3 p = Spoke(t * 360f) * radius;
                p.y = t * rise - (top ? 0f : FlightThickness);
                return p;
            }

            for (int i = 0; i < FlightSegments; i++)
            {
                Vector3 mid = Spoke((i + 0.5f) / FlightSegments * 360f);
                Quad(vertices, triangles, Point(i, ColumnRadius, true), Point(i, StairRadius, true), Point(i + 1, StairRadius, true), Point(i + 1, ColumnRadius, true), Vector3.up);
                Quad(vertices, triangles, Point(i, ColumnRadius, false), Point(i, StairRadius, false), Point(i + 1, StairRadius, false), Point(i + 1, ColumnRadius, false), Vector3.down);
                Quad(vertices, triangles, Point(i, StairRadius, true), Point(i, StairRadius, false), Point(i + 1, StairRadius, false), Point(i + 1, StairRadius, true), mid);
                Quad(vertices, triangles, Point(i, ColumnRadius, true), Point(i, ColumnRadius, false), Point(i + 1, ColumnRadius, false), Point(i + 1, ColumnRadius, true), -mid);
            }

            Quad(vertices, triangles, Point(0, ColumnRadius, true), Point(0, StairRadius, true), Point(0, StairRadius, false), Point(0, ColumnRadius, false), -Vector3.right);
            Quad(vertices, triangles, Point(FlightSegments, ColumnRadius, true), Point(FlightSegments, StairRadius, true), Point(FlightSegments, StairRadius, false), Point(FlightSegments, ColumnRadius, false), Vector3.right);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// <summary>Adds a quad as two triangles, wound so its face points along the given side.</summary>
        static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f;
            triangles.AddRange(flip
                ? new[] { start, start + 2, start + 1, start, start + 3, start + 2 }
                : new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        /// <summary>
        /// Things to test the body against, in the lobby and clear of its stairwell and spawns.
        /// The sizes are the cases the movement doc quotes: a desk, the tallest ledge the
        /// profile allows, a wall beyond it, a gap only a crouch fits, stepped stairs, and a ramp to slide down.
        /// </summary>
        static void EnsureCourse(TowerProfile tower)
        {
            if (GameObject.Find("Course") != null)
                return;

            Transform course = new GameObject("Course").transform;
            Block(course, "Desk 0.75 m", new Vector3(-12f, 0.375f, -7f), new Vector3(2f, 0.75f, 1f));
            Block(course, "Ledge 1.6 m", new Vector3(-9f, 0.8f, -7f), new Vector3(2f, 1.6f, 1f));
            Block(course, "Wall 2.4 m", new Vector3(-6f, 1.2f, -7f), new Vector3(2f, 2.4f, 1f));

            // A 1.3 m gap: a crouched body (1.1 m) fits, a standing one (1.8 m) does not.
            Transform tunnel = new GameObject("Crouch tunnel").transform;
            tunnel.SetParent(course, false);
            tunnel.localPosition = new Vector3(-10f, 0f, -11.5f);
            Block(tunnel, "Roof", new Vector3(0f, 1.4f, 0f), new Vector3(3f, 0.2f, 1.6f));
            Block(tunnel, "Side back", new Vector3(0f, 0.65f, -0.9f), new Vector3(3f, 1.3f, 0.2f));
            Block(tunnel, "Side front", new Vector3(0f, 0.65f, 0.9f), new Vector3(3f, 1.3f, 0.2f));

            // Stepped stairs with no ramp over them, to show what the level rules warn about.
            Transform steps = new GameObject("Box steps").transform;
            steps.SetParent(course, false);
            steps.localPosition = new Vector3(8f, 0f, -5f);
            const int count = 5;
            const float riser = 0.18f, tread = 0.3f;
            for (int i = 0; i < count; i++)
            {
                float top = riser * (i + 1);
                Block(steps, "Step " + (i + 1), new Vector3(tread * (i + 0.5f), top * 0.5f, 0f), new Vector3(tread, top, 1.5f));
            }
            Block(steps, "Landing", new Vector3(tread * count + 0.75f, riser * count * 0.5f, 0f), new Vector3(1.5f, riser * count, 1.5f));

            // Out on the street: a 3 m platform with a 20° ramp down from it, for sliding downhill into a jump.
            Transform ramp = new GameObject("Slide ramp").transform;
            ramp.SetParent(course, false);
            ramp.localPosition = new Vector3(-17f, 0f, 20f);
            const float drop = 3f, slope = 20f;
            float length = drop / Mathf.Sin(slope * Mathf.Deg2Rad);
            Block(ramp, "Platform", new Vector3(0f, drop * 0.5f, 0f), new Vector3(2f, drop, 2f));
            Vector3 along = new(Mathf.Cos(slope * Mathf.Deg2Rad), -Mathf.Sin(slope * Mathf.Deg2Rad), 0f);
            Vector3 outOf = new(-along.y, along.x, 0f);
            GameObject run = Block(ramp, "Ramp", new Vector3(1f, drop, 0f) + along * (length * 0.5f) - outOf * (Slab * 0.5f), new Vector3(length, Slab, 2f));
            run.transform.localRotation = Quaternion.Euler(0f, 0f, -slope);
        }

        static GameObject Block(Transform parent, string name, Vector3 localCentre, Vector3 size)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localCentre;
            block.transform.localScale = size;
            return block;
        }

        // ---- Scripted check, run from the shell ----------------------------------------------

        /// <summary>
        /// Opens the lane scene and presses Play; PlayerMovementSession takes it from there when
        /// the editor was started with -movementSession. Shell use, with graphics so it can capture:
        ///   Unity -batchmode -projectPath &lt;repo&gt; -executeMethod CloseTheDeal.Editor.Lanes.MovementLaneSetup.RunSession
        ///         -movementSession solo -sessionOut &lt;folder&gt; -local
        /// </summary>
        public static void RunSession()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// Builds a development player of the lane scene alone, to be the second instance in a
        /// host and client check. Shell use: -executeMethod ...MovementLaneSetup.BuildSessionPlayer -sessionBuild &lt;path.app&gt;
        /// </summary>
        public static void BuildSessionPlayer()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-sessionBuild");
            if (at < 0 || at + 1 >= args.Length)
            {
                Debug.LogError("[Movement lane] Pass -sessionBuild <path to the .app to write>.");
                EditorApplication.Exit(2);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = args[at + 1],
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            Debug.Log("[Movement lane] Session player build " + report.summary.result + ": " + args[at + 1]);
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
