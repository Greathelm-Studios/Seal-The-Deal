// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex: "where is ur scene" -> build a Unity scene of the whole dressed office floor
//  Modified  : Claude Fable 5.2 (Claude Code), 2026-11-12 — Alex: "remove the other building".
//              The rival tower (façade, slabs, stand-ins, massing) is no longer built.
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Lays out whole office floors from the kit prefabs, as ArtSource/Kit/interior_scene.py
//              does in Blender, so the approved White Trays + Cobalt Portal kit can be walked and lit in
//              Unity. Its own file because KitImport proves the pieces snap (a small test layout) while
//              this composes a full floor to judge as a space; it is a review scene, not a floor
//              template: GreyboxTowerSetup stays the authority on the game's layout.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Builds <c>KitOffice.unity</c>: three stacked office floors, from the kit prefabs.
    /// Menu: <c>Close the Deal &gt; Art &gt; Build Kit Office Scene</c>.
    /// </summary>
    /// <remarks>
    /// Run <see cref="KitImport"/> first; this only places its prefabs. Re-running overwrites KitOffice.unity.
    /// The menu opens the scene on its own and then applies <see cref="OfficeLook"/>. <see cref="BuildAdditive"/>
    /// instead builds beside whatever is open, without touching it, and copies the open scene's look; it exists
    /// so the scene can be made while someone else has unsaved work open in the same Editor.
    /// </remarks>
    public static class KitOfficeScene
    {
        const string ScenePath = "Assets/_Project/Scenes/KitOffice.unity";
        const string PrefabFolder = "Assets/_Project/Art/Kit/Prefabs";
        const string PaperWhitePath = "Assets/_Project/Art/Kit/Materials/Wall_PaperWhite.mat";
        const string NavyPath = "Assets/_Project/Art/Kit/Materials/Trim_Navy.mat";
        const string VolumeProfilePath = "Assets/_Project/Art/Look/OfficeLook_Volume.asset";

        // ---- Layout, Unity floor-local metres: the same numbers as ArtSource/Kit/interior_scene.py ----
        /// <summary>Façade bays: 28 m / 4 m (TOWER.md footprint width).</summary>
        const int Bays = 7;
        const float Bay = 4f, Infill = 2f;
        /// <summary>Front infill row depth, metres (kit README proposal 1).</summary>
        const float Strip = 2f;
        /// <summary>4 m rows behind the strip: 2 + 3 × 4 = 14 m main floor (TOWER.md).</summary>
        const int Rows = 3;
        const float Spine = 4f;
        const float SpineZ = -(Strip + Rows * Bay);   // -14, the spine's front wall line
        const float Depth = Strip + Rows * Bay + Spine;   // 18
        const float FloorHeight = 4.5f, ClearHeight = 4.3f;
        /// <summary>Slab_Stairwell pivot; its 12 × 8 m module spans x 8–20, z -6 to -14.</summary>
        static readonly Vector3 Stairwell = new Vector3(8f, 0f, -6f);
        /// <summary>Stair centre: stairwell pivot + (6, 0, -4) (kit README).</summary>
        static readonly Vector3 StairCentre = new Vector3(14f, 0f, -10f);
        /// <summary>Spine dividers: WC 0–8, STORE 8–20, UTIL 20–28 (kit README proposal 3).</summary>
        const float WcEnd = 8f, UtilStart = 20f;
        /// <summary>
        /// Width of the street, façade to rival façade, metres (TOWER.md). The rival tower itself is
        /// no longer built in this scene (Alex, 2026-11-12: "remove the other building"); this is
        /// now only where the across-the-street review camera stands.
        /// </summary>
        const float Gap = 12f;
        /// <summary>Floors stacked here, so the view from across the street shows the floors above and below as well.</summary>
        const int Floors = 3;
        /// <summary>Cage: 12 panel slots every 30°, first at 15°, open from 300° where the flight arrives (kit README).</summary>
        const float CageFirst = 15f, CageStep = 30f, CageOpenFrom = 300f;
        /// <summary>Locked door: GreyboxTowerSetup.LockedDoorAngle, its radius (midway column to tread edge) and drop.</summary>
        const float DoorAngle = 30f, DoorRadius = 1.7f, DoorDrop = 0.02f;
        /// <summary>Strip light centres in a 4 m bay along x and z: each 1.5 m strip spans three whole 0.5 m tiles.</summary>
        static readonly float[] LightOffsets = { 1.25f, 2.75f };
        /// <summary>No strip lights within this radius of the stair centre, metres (the slab hole is 2.65).</summary>
        const float StairClear = 3f;
        /// <summary>White massing above and below the built floors, metres: enough to fill a view, not a city.</summary>
        const float MassingHeight = 30f;
        /// <summary>Stand-in size: the 1.8 m player and a shoulder-width capsule.</summary>
        const float FigureHeight = 1.8f, FigureRadius = 0.25f;
        /// <summary>Where stand-ins stand on each floor (x, z).</summary>
        static readonly Vector2[] OurSpots = { new Vector2(6f, -3f), new Vector2(13f, -5.5f), new Vector2(4f, -8f), new Vector2(24f, -4f) };

        // ---- Review captures
        const float Eye = 1.6f;
        const int CaptureWidth = 1600, CaptureHeight = 900;
        const float CaptureFov = 60f;

        [MenuItem("Close the Deal/Art/Build Kit Office Scene")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Build(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            OfficeLook.Apply(askFirst: false);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Builds and saves KitOffice.unity in a new scene beside the open ones, copying the active scene's sky,
        /// ambient, fog and sun, renders review PNGs to <paramref name="captureFolder"/>, then closes it again.
        /// The open scenes are not modified.
        /// </summary>
        /// <returns>A short report: what was placed and where the captures went.</returns>
        public static string BuildAdditive(string captureFolder)
        {
            Scene source = SceneManager.GetActiveScene();
            var look = LookSnapshot.From(source);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var report = new StringBuilder();
            try
            {
                SceneManager.SetActiveScene(scene);
                look.ApplyTo(scene);
                int placed = Build(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                report.AppendLine($"Saved {ScenePath}: {placed} kit pieces, look copied from '{source.name}'.");
                if (!string.IsNullOrEmpty(captureFolder)) Capture(scene, captureFolder, report);
            }
            finally
            {
                if (source.IsValid()) SceneManager.SetActiveScene(source);
                EditorSceneManager.CloseScene(scene, true);
            }
            return report.ToString();
        }

        // ------------------------------------------------------------------ layout

        /// <summary>Places every piece into <paramref name="scene"/> (which must be the active scene). Returns the count.</summary>
        static int Build(Scene scene)
        {
            var root = new GameObject("Kit Office").transform;
            int count = 0;
            GameObject P(Transform parent, string piece, float x, float y, float z, float yaw = 0f)
            {
                count++;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{piece}.prefab");
                if (prefab == null) throw new FileNotFoundException($"No kit prefab {piece}; run Import Kit first.");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetLocalPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
                return go;
            }

            for (int f = 0; f < Floors; f++)
            {
                var floor = new GameObject($"Floor {f}").transform;
                floor.SetParent(root, false);
                floor.localPosition = new Vector3(0f, f * FloorHeight, 0f);
                BuildFloor(floor, P);
                Figures(floor);
            }
            // The top of the stack: the slab over the highest floor is its ceiling.
            var roof = new GameObject("Roof slab").transform;
            roof.SetParent(root, false);
            roof.localPosition = new Vector3(0f, Floors * FloorHeight, 0f);
            Slabs(roof, P);
            Massing(root);
            AddCamera();
            return count;
        }

        delegate GameObject Placer(Transform parent, string piece, float x, float y, float z, float yaw = 0f);

        static void Slabs(Transform t, Placer P)
        {
            for (int i = 0; i < Bays; i++)
            {
                float x = i * Bay;
                P(t, "Slab_4x2", x, 0f, 0f);
                for (int r = 0; r < Rows; r++)
                {
                    float z = -Strip - r * Bay;
                    if (x >= Stairwell.x && x < Stairwell.x + 12f && z <= Stairwell.z) continue;   // inside the stairwell module
                    P(t, "Slab_4x4", x, 0f, z);
                }
                P(t, "Slab_4x4", x, 0f, SpineZ);
            }
            P(t, "Slab_Stairwell", Stairwell.x, 0f, Stairwell.z);
        }

        static void BuildFloor(Transform floor, Placer P)
        {
            Transform G(string name) { var g = new GameObject(name).transform; g.SetParent(floor, false); return g; }

            Slabs(G("Slabs"), P);

            var facade = G("Facade");
            for (int i = 0; i < Bays; i++)
            {
                P(facade, "Window_4m", i * Bay, 0f, 0f);
                if (i > 0) P(facade, "Column_Facade", i * Bay, 0f, 0f);
            }
            P(facade, "Column_CornerL", 0f, 0f, 0f);
            P(facade, "Column_CornerR", Bays * Bay, 0f, 0f);

            // Side walls: left runs toward +Z from its pivot (yaw -90), right toward -Z (yaw 90); thickness inside both.
            var walls = G("Walls");
            float w = Bays * Bay;
            P(walls, "Wall_Solid_2m", 0f, 0f, -Strip, -90f);
            P(walls, "Wall_Solid_2m", w, 0f, 0f, 90f);
            for (int k = 0; k <= Rows; k++)
            {
                float z = -Strip - k * Bay;
                P(walls, "Wall_Solid_4m", 0f, 0f, z - Bay, -90f);
                P(walls, "Wall_Solid_4m", w, 0f, z, 90f);
            }
            for (int i = 0; i < Bays; i++) P(walls, "Wall_Solid_4m", (i + 1) * Bay, 0f, -Depth, 180f);   // back wall

            // Spine front: WC, STORE, UTIL doors; the STORE front behind the stair is the Cobalt Portal.
            var spine = G("Spine");
            var doors = new HashSet<float> { 0f, 12f, 20f };
            var portal = new Dictionary<float, string> { { 8f, "Wall_Solid_4m_Cobalt" }, { 12f, "Wall_Door_4m_PortalHead" }, { 16f, "Wall_Solid_4m_Cobalt" } };
            const float leafX = 1.41f, leafZ = -0.1f;   // kit README: the leaf's hinge in a door wall
            for (int i = 0; i < Bays; i++)
            {
                float x = i * Bay;
                if (!portal.TryGetValue(x, out string piece)) piece = doors.Contains(x) ? "Wall_Door_4m" : "Wall_Solid_4m";
                P(spine, piece, x, 0f, SpineZ);
                if (doors.Contains(x)) P(spine, "Door_Leaf", x + leafX, 0f, SpineZ + leafZ);
            }
            P(spine, "Wall_Solid_4m", WcEnd, 0f, -Depth, -90f);
            P(spine, "Wall_Solid_4m", UtilStart, 0f, SpineZ, 90f);

            // Stair: ramp (collider only), visual treads, column, cage open where the flight arrives, locked door.
            var stair = G("Stair");
            Vector3 c = StairCentre;
            P(stair, "Stair_Flight", c.x, 0f, c.z);
            P(stair, "Stair_Steps", c.x, 0f, c.z);
            P(stair, "Stair_Column", c.x, 0f, c.z);
            for (float a = CageFirst; a < CageOpenFrom; a += CageStep) P(stair, "Stair_CagePanel", c.x, 0f, c.z, a);
            float r = DoorAngle * Mathf.Deg2Rad;
            P(stair, "Stair_LockedDoor", c.x + Mathf.Sin(r) * DoorRadius, FloorHeight * DoorAngle / 360f - DoorDrop,
              c.z + Mathf.Cos(r) * DoorRadius, DoorAngle - 90f);

            // Meeting rooms, 4 × 4 m at z -6 to -10, half open on the stair side.
            var rooms = G("Meeting rooms");
            foreach (var (x0, openRight) in new[] { (2f, true), (22f, false) })
            {
                float x1 = x0 + Bay;
                P(rooms, "Partition_Glass_4m", x0, 0f, -6f);
                P(rooms, "Partition_Glass_4m", x1, 0f, -10f, 180f);
                if (openRight)
                {
                    P(rooms, "Partition_Glass_4m", x0, 0f, -10f, -90f);
                    P(rooms, "Partition_Glass_2m", x1, 0f, -8f, 90f);
                }
                else
                {
                    P(rooms, "Partition_Glass_4m", x1, 0f, -6f, 90f);
                    P(rooms, "Partition_Glass_2m", x0, 0f, -10f, -90f);
                }
            }

            // Strip lights on this floor's ceiling (the soffit of the slab above), four per 4 × 4 m bay.
            var lights = G("Ceiling lights");
            for (int i = 0; i < Bays; i++)
                for (int row = 0; row < Rows; row++)
                    foreach (float ox in LightOffsets)
                        foreach (float oz in LightOffsets)
                        {
                            float x = i * Bay + ox, z = -Strip - row * Bay - oz;
                            if (new Vector2(x - c.x, z - c.z).magnitude > StairClear)
                                P(lights, "Ceiling_LightStrip", x, ClearHeight, z);
                        }
        }

        /// <summary>Dark-suited capsules at player height on each floor: the read the art bible asks for.</summary>
        static void Figures(Transform floor)
        {
            var navy = AssetDatabase.LoadAssetAtPath<Material>(NavyPath);
            var group = new GameObject("Stand-ins").transform;
            group.SetParent(floor, false);
            foreach (Vector2 s in OurSpots) Figure(group, navy, s);
        }

        static void Figure(Transform parent, Material navy, Vector2 at)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Stand-in 1.8 m";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            // WHY these numbers: Unity's capsule is 2 m tall and 1 m wide; scale it to 1.8 m and stand it on the floor.
            go.transform.localPosition = new Vector3(at.x, FigureHeight * 0.5f, at.y);
            go.transform.localScale = new Vector3(FigureRadius * 2f, FigureHeight * 0.5f, FigureRadius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = navy;
        }

        /// <summary>White blocks above and below the tower, so the views read as one tall building.</summary>
        static void Massing(Transform root)
        {
            var white = AssetDatabase.LoadAssetAtPath<Material>(PaperWhitePath);
            var group = new GameObject("Massing").transform;
            group.SetParent(root, false);
            float w = Bays * Bay, top = Floors * FloorHeight;
            const float slab = 0.2f, gapUnderSlab = 0.01f;   // WHY: keep the blocks off the real slabs' faces (no z-fighting)
            void Block(string name, float y0, float y1, float z0, float z1)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(group, false);
                go.transform.localPosition = new Vector3(w * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
                go.transform.localScale = new Vector3(w, y1 - y0, z1 - z0);
                go.GetComponent<Renderer>().sharedMaterial = white;
            }
            Block("Our tower below", -MassingHeight, -slab - gapUnderSlab, -Depth, 0f);
            Block("Our tower above", top + gapUnderSlab, top + MassingHeight, -Depth, 0f);
        }

        static void AddCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = CaptureFov;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var v = Views()[0];
            go.transform.SetPositionAndRotation(v.eye, Quaternion.LookRotation(v.target - v.eye));
        }

        // ------------------------------------------------------------------ look

        /// <summary>The scene-level half of the office look: what RenderSettings, the sun and the Volume hold.</summary>
        sealed class LookSnapshot
        {
            Material _skybox;
            AmbientMode _ambientMode;
            Color _sky, _equator, _ground, _fogColour;
            bool _fog;
            FogMode _fogMode;
            float _fogStart, _fogEnd;
            DefaultReflectionMode _reflection;
            Light _sun;

            /// <summary>Reads <paramref name="scene"/>'s settings; it must be the active scene.</summary>
            public static LookSnapshot From(Scene scene) => new LookSnapshot
            {
                _skybox = RenderSettings.skybox, _ambientMode = RenderSettings.ambientMode,
                _sky = RenderSettings.ambientSkyColor, _equator = RenderSettings.ambientEquatorColor,
                _ground = RenderSettings.ambientGroundColor, _fog = RenderSettings.fog, _fogMode = RenderSettings.fogMode,
                _fogColour = RenderSettings.fogColor, _fogStart = RenderSettings.fogStartDistance,
                _fogEnd = RenderSettings.fogEndDistance, _reflection = RenderSettings.defaultReflectionMode,
                _sun = RenderSettings.sun,
            };

            /// <summary>Writes the settings into <paramref name="scene"/> (now active), with its own sun and Volume.</summary>
            public void ApplyTo(Scene scene)
            {
                RenderSettings.skybox = _skybox;
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientSkyColor = _sky;
                RenderSettings.ambientEquatorColor = _equator;
                RenderSettings.ambientGroundColor = _ground;
                RenderSettings.fog = _fog;
                RenderSettings.fogMode = _fogMode;
                RenderSettings.fogColor = _fogColour;
                RenderSettings.fogStartDistance = _fogStart;
                RenderSettings.fogEndDistance = _fogEnd;
                RenderSettings.defaultReflectionMode = _reflection;

                var sunGo = new GameObject("Sun");
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                if (_sun != null)
                {
                    sunGo.transform.rotation = _sun.transform.rotation;
                    sun.color = _sun.color;
                    sun.intensity = _sun.intensity;
                    sun.shadows = _sun.shadows;
                }
                RenderSettings.sun = sun;

                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
                if (profile != null)
                {
                    var volume = new GameObject("Office Look").AddComponent<Volume>();
                    volume.isGlobal = true;
                    volume.sharedProfile = profile;
                }
            }
        }

        // ------------------------------------------------------------------ captures

        /// <summary>The concept shots: entry to stair, stair close, main floor to windows, the view from across the street, a cutaway.</summary>
        static (string name, Vector3 eye, Vector3 target)[] Views()
        {
            float mid = FloorHeight;   // the middle floor, so floors above and below show from across the street
            return new[]
            {
                ("A_entry_to_stair", new Vector3(1.5f, mid + Eye, -12.5f), new Vector3(14f, mid + 1.4f, -6f)),
                ("G_stair_close", new Vector3(9f, mid + Eye, -3.5f), new Vector3(14f, mid + 2f, -10f)),
                ("B_stair_to_windows", new Vector3(14f, mid + Eye, -5f), new Vector3(14f, mid + 1.6f, 6f)),
                // The view a rival player would have. The name is kept so earlier captures still line up.
                ("H_rival_view", new Vector3(14f, mid + Eye, Gap + 1.5f), new Vector3(14f, mid + 1.8f, -8f)),
                ("E_spine_to_facade", new Vector3(14f, mid + Eye, -13.5f), new Vector3(8f, mid + 1.6f, 0f)),
            };
        }

        /// <summary>Renders each view from a camera that sees only <paramref name="scene"/>, so other open scenes don't show.</summary>
        static void Capture(Scene scene, string folder, StringBuilder report)
        {
            Directory.CreateDirectory(folder);
            var go = new GameObject("Capture Camera");
            SceneManager.MoveGameObjectToScene(go, scene);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = CaptureFov;
            cam.scene = scene;   // WHY: the shared Editor may have another scene open at the same coordinates
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24);
            var image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            try
            {
                // WHY a discarded first render: a fresh camera's first frame over just-built objects came out
                // wrong (the floor below drawn through the carpet, 2026-11-12); from the second frame on it's right.
                cam.targetTexture = rt;
                cam.Render();
                foreach (var v in Views())
                {
                    go.transform.SetPositionAndRotation(v.eye, Quaternion.LookRotation(v.target - v.eye));
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = rt;
                    image.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                    image.Apply();
                    RenderTexture.active = previous;
                    string path = Path.Combine(folder, $"unity_office_{v.name}.png");
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    report.AppendLine($"wrote {path}");
                }
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(go);   // after saving, so the capture camera never lands in the scene file
            }
        }
    }
}
