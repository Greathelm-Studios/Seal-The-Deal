// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Opus 5.5 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex: "where is ur scene" -> build a Unity scene of the whole dressed office floor
//  Modified  : Claude Fable 5.2 (Claude Code), 2026-11-12 — Alex: "remove the other building".
//              The rival tower (façade, slabs, stand-ins, massing) is no longer built.
//  Modified  : Claude Fable 5.2 (Claude Code), 2026-11-12 — Alex: "when I press play on KitOffice
//              I can't move". The menu build now adds the player rig, and Make Open Scene
//              Playable adds it to an existing scene.
//  Modified  : Claude Opus 5.5 (Claude Code), 2026-11-13 — Alex: turn KitOffice into the labyrinth.
//              The tower floor (façade floor plate, spiral stair, locked door, spine, massing) is replaced
//              by a two-level office labyrinth from the round-5 pieces (ArtSource/Kit/README.md).
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Lays out an office labyrinth from the kit prefabs, so the approved round-5 kit can be walked
//              and lit in Unity as a space. Its own file because KitImport proves the pieces snap (a small
//              test layout) while this composes a whole level section to judge; it is a review scene, not
//              the game's level: whoever lays out the real maze decides that.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.IO;
using System.Text;
using CloseTheDeal.Editor.Greybox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Builds <c>KitOffice.unity</c>: a two-level office labyrinth from the kit prefabs.
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

        // ---- Labyrinth layout, Unity metres. Zones and sizes: ArtSource/Kit/concepts/Kit_Detail/README_r5.md ----
        /// <summary>Floor plate: 8 × 6 cells of 4 m, so 32 × 24 m, x east from 0, z south from 0.</summary>
        const int Cols = 8, Rows = 6;
        const float Cell = 4f;
        const float Width = Cols * Cell, Depth = Rows * Cell;
        const float FloorHeight = 4.5f;
        /// <summary>Storeys built: the ground floor and the upper floor round the atrium; a roof slab caps them.</summary>
        const int Levels = 2;
        /// <summary>Atrium (zone H), double height: cells from x 12 to 20, z −4 to −16. No middle slab over it.</summary>
        const float AtriumX0 = 12f, AtriumX1 = 20f, AtriumZ0 = -4f, AtriumZ1 = -16f;
        /// <summary>Straight stair pivot, its foot's front-left corner: it rises 7.2 m south and lands on the upper
        /// floor at the atrium's south edge (z −16), 0.2 m clear of the void's west guard.</summary>
        static readonly Vector3 StairPivot = new Vector3(AtriumX0 + 0.2f, 0f, AtriumZ1 + 7.2f);
        /// <summary>Kit door: the leaf's hinge in a door wall, local metres (kit README).</summary>
        const float LeafX = 1.41f, LeafZ = -0.1f;
        /// <summary>White block under the floor plate, metres: the window corridor looks down at it, not into nothing.</summary>
        const float MassingHeight = 30f;
        /// <summary>Stand-in size: the 1.8 m player and a shoulder-width capsule.</summary>
        const float FigureHeight = 1.8f, FigureRadius = 0.25f;
        /// <summary>Where stand-ins stand (x, y, z): one per zone on the ground, two upstairs.</summary>
        static readonly Vector3[] Spots =
        {
            new Vector3(10f, 0f, -6f), new Vector3(17f, 0f, -10f), new Vector3(28f, 0f, -13f), new Vector3(4f, 0f, -22f),
            new Vector3(15f, 0f, -22f), new Vector3(6f, FloorHeight, -20f), new Vector3(26f, FloorHeight, -8f),
        };
        /// <summary>
        /// Where players start in Play, world metres: the two ends of the window corridor, 0.1 m above the floor
        /// as in Greybox, so the first fight is a run along the glass toward the atrium.
        /// </summary>
        static readonly Vector3 SpawnA = new Vector3(4f, 0.1f, -2f), SpawnB = new Vector3(28f, 0.1f, -2f);

        // ---- Review captures
        const float Eye = 1.6f;
        const int CaptureWidth = 1600, CaptureHeight = 900;
        const float CaptureFov = 60f;

        [MenuItem("Close the Deal/Art/Build Kit Office Scene")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAndSave();
        }

        /// <summary>
        /// Opens a new scene in place of whatever is open (without asking), builds the labyrinth, adds the player
        /// rig and the office look, and saves it as KitOffice.unity. The menu asks to save open work first.
        /// </summary>
        public static void BuildAndSave()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Build(scene);
            GreyboxSceneSetup.EnsurePlayRig(SpawnA, SpawnB);
            EditorSceneManager.SaveScene(scene, ScenePath);
            OfficeLook.Apply(askFirst: false);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Adds the player rig (network manager, spawns, menu, follow camera) to the open scene so Play
        /// puts a player in the window corridor. Adds only what is missing and does not save.
        /// </summary>
        /// <remarks>
        /// Exists so a KitOffice.unity built before the rig was part of the build, or one with unsaved
        /// work in it, can be made playable without rebuilding the scene over that work.
        /// </remarks>
        [MenuItem("Close the Deal/Art/Make Open Scene Playable")]
        static void MakePlayableFromMenu()
        {
            // WHY one scene only: the rig is found by searching every loaded scene, so with Greybox
            // loaded alongside it would find Greybox's rig and add nothing here.
            if (SceneManager.sceneCount != 1)
            {
                EditorUtility.DisplayDialog("Make Open Scene Playable", "Close the other loaded scenes first, so the player rig lands in this one.", "OK");
                return;
            }
            if (Camera.main == null)
            {
                EditorUtility.DisplayDialog("Make Open Scene Playable", "This scene has no camera tagged MainCamera for the player camera to use.", "OK");
                return;
            }
            GreyboxSceneSetup.EnsurePlayRig(SpawnA, SpawnB);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
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
            var root = new GameObject("Kit Office (labyrinth)").transform;
            int count = 0;
            Transform G(string name, float y = 0f)
            {
                var g = new GameObject(name).transform;
                g.SetParent(root, false);
                g.localPosition = new Vector3(0f, y, 0f);
                return g;
            }
            GameObject P(Transform parent, string piece, float x, float z, float yaw = 0f)
            {
                count++;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{piece}.prefab");
                if (prefab == null) throw new FileNotFoundException($"No kit prefab {piece}; run Import Kit first.");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetLocalPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
                return go;
            }
            // A maze island: four rounded returns round a 4 × 4 m block with its NW corner at (x, z).
            // cobaltCorner picks the cobalt one: 0 NW, 1 NE, 2 SE, 3 SW, -1 none.
            void Island(Transform t, float x, float z, int cobaltCorner)
            {
                var corners = new[] { (x, z, 0f), (x + Cell, z, 90f), (x + Cell, z - Cell, 180f), (x, z - Cell, -90f) };
                for (int k = 0; k < corners.Length; k++)
                    P(t, k == cobaltCorner ? "Wall_Return_2x2_Cobalt" : "Wall_Return_2x2", corners[k].Item1, corners[k].Item2, corners[k].Item3);
            }

            // Slabs: the ground floor, the upper floor (open over the atrium), and the roof.
            for (int level = 0; level <= Levels; level++)
            {
                var slabs = G(level == 0 ? "Floor slabs" : level == Levels ? "Roof slabs" : $"Level {level} slabs", level * FloorHeight);
                for (int i = 0; i < Cols; i++)
                    for (int j = 0; j < Rows; j++)
                    {
                        float x = i * Cell, z = -j * Cell;
                        if (level == 0) P(slabs, "Slab_4x4", x, z);
                        else if (level == Levels || !InAtrium(x, z)) P(slabs, "Slab_4x4_LightLine", x, z);
                    }
            }

            // Perimeter on both storeys: the glass façade on the north, solid walls elsewhere, thickness inside.
            for (int level = 0; level < Levels; level++)
            {
                var shell = G($"Level {level} shell", level * FloorHeight);
                for (int i = 0; i < Cols; i++)
                {
                    P(shell, "Window_4m", i * Cell, 0f);
                    if (i > 0) P(shell, "Column_Facade", i * Cell, 0f);
                    P(shell, "Wall_Solid_4m", (i + 1) * Cell, -Depth, 180f);
                }
                P(shell, "Column_CornerL", 0f, 0f);
                P(shell, "Column_CornerR", Width, 0f);
                for (int j = 0; j < Rows; j++)
                {
                    float z = -j * Cell;
                    if (level == 0 && j == 2) P(shell, "Wall_Niche_4m", 0.2f, z, 90f);   // zone L's landmark, facing in
                    else P(shell, "Wall_Solid_4m", 0f, z - Cell, -90f);
                    P(shell, "Wall_Solid_4m", Width, z, 90f);
                }
            }

            // Zone L (legal): a corridor loop round an island with a cobalt corner; cobalt and frost toward the atrium.
            var zoneL = G("Zone L - corridor loop");
            Island(zoneL, 4f, -8f, 0);
            P(zoneL, "Wall_Solid_4m_Cobalt", AtriumX0, -4f, 90f);   // cobalt face toward the atrium,
            P(zoneL, "Partition_Frost_4m", AtriumX0, -12f, 90f);     // a 4 m open mouth between them
            P(zoneL, "Wall_Solid_4m", 0f, -16f);                      // L / C boundary: solid, the room's door, open
            P(zoneL, "Wall_Door_4m", 4f, -16f);
            P(zoneL, "Door_Leaf", 4f + LeafX, -16f + LeafZ);

            // Zone C (claims): an 8 × 8 m meeting room behind frosted glass, one capsule counter.
            var zoneC = G("Zone C - meeting room");
            P(zoneC, "Partition_Frost_4m", 8f, -16f, 90f);
            P(zoneC, "Partition_Frost_4m", 8f, -20f, 90f);
            P(zoneC, "Counter_Capsule_2m", 4f, -20.5f);

            // Zone A (archive): transparency cuts, 2 m cells of solid, clear, open and frosted.
            var zoneA = G("Zone A - glass cuts");
            P(zoneA, "Wall_Solid_2m", 12f, -20f);
            P(zoneA, "Partition_Glass_2m", 14f, -20f);
            P(zoneA, "Partition_Frost_2m", 18f, -20f);
            P(zoneA, "Cover_Block_2m", 16.5f, -22.5f);

            // Zone H (hearings): the atrium's straight stair to the upper floor, glass guards round the void.
            var zoneH = G("Zone H - atrium");
            P(zoneH, "Stair_Straight_2m", StairPivot.x, StairPivot.z);
            var guards = G("Zone H - void guards", FloorHeight);
            for (float z = AtriumZ0; z > AtriumZ1; z -= Cell)
            {
                P(guards, "Guard_Glass_4m", AtriumX0, z, 90f);           // west edge: glass on the deck side
                P(guards, "Guard_Glass_4m", AtriumX1, z - Cell, -90f);   // east edge
            }
            for (float x = AtriumX0; x < AtriumX1; x += Cell)
                P(guards, "Guard_Glass_4m", x + Cell, AtriumZ0, 180f);  // north edge
            P(guards, "Guard_Glass_2m", StairPivot.x + 2f, AtriumZ1);       // south edge, beside the stair's landing
            P(guards, "Guard_Glass_4m", AtriumX1 - Cell, AtriumZ1);

            // Zone R (review): a split X, two offset islands and block cover; cobalt and a door toward H and A.
            var zoneR = G("Zone R - split junction");
            Island(zoneR, 22f, -8f, -1);
            Island(zoneR, 26f, -14f, 2);
            P(zoneR, "Cover_Block_2m", 24.5f, -15f, 90f);
            P(zoneR, "Cover_Block_2m", 29f, -21f);
            P(zoneR, "Partition_Glass_4m", AtriumX1, -4f, 90f);
            P(zoneR, "Wall_Solid_4m_Cobalt", AtriumX1, -12f, 90f);        // yaw 90 turns the cobalt face toward R (+X)
            P(zoneR, "Wall_Door_4m", AtriumX1, -16f, 90f);
            P(zoneR, "Door_Leaf", AtriumX1 + LeafZ, -16f - LeafX, 90f);
            P(zoneR, "Wall_Solid_4m", AtriumX1, -20f, 90f);

            // Upper floor: open, with cover over the rooms below.
            var upper = G("Upper floor cover", FloorHeight);
            P(upper, "Cover_Block_2m", 6f, -10f);
            P(upper, "Counter_Capsule_2m", 26f, -18f, 90f);
            P(upper, "Cover_Block_2m", 16f, -20f, 90f);

            Figures(root);
            Massing(root);
            AddCamera();
            return count;
        }

        /// <summary>True for a 4 m cell (NW corner at x, z) inside the atrium void.</summary>
        static bool InAtrium(float x, float z) => x >= AtriumX0 && x < AtriumX1 && z <= AtriumZ0 && z - Cell >= AtriumZ1;

        /// <summary>Dark-suited capsules at player height, one per zone: the read the art bible asks for.</summary>
        static void Figures(Transform root)
        {
            var navy = AssetDatabase.LoadAssetAtPath<Material>(NavyPath);
            var group = new GameObject("Stand-ins").transform;
            group.SetParent(root, false);
            foreach (Vector3 s in Spots) Figure(group, navy, s);
        }

        static void Figure(Transform parent, Material navy, Vector3 at)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Stand-in 1.8 m";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            // WHY these numbers: Unity's capsule is 2 m tall and 1 m wide; scale it to 1.8 m and stand it on the floor.
            go.transform.localPosition = new Vector3(at.x, at.y + FigureHeight * 0.5f, at.z);
            go.transform.localScale = new Vector3(FigureRadius * 2f, FigureHeight * 0.5f, FigureRadius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = navy;
        }

        /// <summary>A white block under the floor plate, so looking down past the window sill finds the building.</summary>
        static void Massing(Transform root)
        {
            var white = AssetDatabase.LoadAssetAtPath<Material>(PaperWhitePath);
            const float slab = 0.2f, gapUnderSlab = 0.01f;   // WHY: keep the block off the slabs' faces (no z-fighting)
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Building below";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(Width * 0.5f, -slab - gapUnderSlab - MassingHeight * 0.5f, -Depth * 0.5f);
            go.transform.localScale = new Vector3(Width, MassingHeight, Depth);
            go.GetComponent<Renderer>().sharedMaterial = white;
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

        /// <summary>Review shots: one per zone at eye height, plus the upper floor over the atrium.</summary>
        static (string name, Vector3 eye, Vector3 target)[] Views()
        {
            return new[]
            {
                ("L1_window_corridor", new Vector3(2f, Eye, -2f), new Vector3(30f, Eye, -3f)),
                ("L2_loop_cobalt_corner", new Vector3(10f, Eye, -14.5f), new Vector3(4f, Eye, -7f)),
                ("L3_atrium_stair", new Vector3(18.5f, Eye, -14f), new Vector3(13f, 3.5f, -8f)),
                ("L4_room_through_frost", new Vector3(10.5f, Eye, -18f), new Vector3(4f, 1.2f, -21f)),
                ("L5_split_junction", new Vector3(31f, Eye, -5f), new Vector3(23f, 1.4f, -16f)),
                ("L6_glass_cuts", new Vector3(13f, Eye, -17.5f), new Vector3(19f, 1.4f, -23f)),
                ("L7_upper_over_atrium", new Vector3(6f, FloorHeight + Eye, -14f), new Vector3(18f, 3f, -8f)),
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
