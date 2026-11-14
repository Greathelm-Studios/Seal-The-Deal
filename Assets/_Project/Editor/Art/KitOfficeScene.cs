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
//  Modified  : Claude Opus 5.5 (Claude Code), 2026-11-13 — detail pass after Alex's reference plates: curved
//              header-glass meeting room, graphic columns, reception, workstation benches, carpet, tile ceilings,
//              pendants, planters, posted balustrade; DevMirror and the floor bakes kept on rebuild.
//  Modified  : Claude Opus 5.5 (Claude Code), 2026-11-13 — the Japanese corporate mix (concepts/Japanese_Aero):
//              AONAGI logo, LED reception sign, water server, umbrella stand, water wall, vending machine, slipper
//              cabinet, phones and document trays on the benches.
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
        const string DevMirrorPath = "Assets/_Project/Prefabs/Dev/DevMirror.prefab";
        /// <summary>DevMirror placement, world metres and degrees: as placed by hand in KitOffice (d4cce74).</summary>
        static readonly Vector3 DevMirrorPosition = new Vector3(30.44f, 6.34f, -12.21f);
        const float DevMirrorYaw = 90f;
        static readonly Vector3 DevMirrorScale = new Vector3(21.18f, 3.45f, 1f);
        const string HitDummyPath = "Assets/_Project/Prefabs/Dev/HitDummy.prefab";
        /// <summary>
        /// HitDummy spot, world metres: the upper floor's east side, in view of the DevMirror (x 30.44) with about
        /// 3 m of clear floor round it (benches from z -10.95, the atrium rail at x 20, the windows at z 0).
        /// Requested by the hit-reaction session (AgentHub kr0meee-4553), 2026-11-13.
        /// Only a starting spot since 2026-11-13: KitOfficeRoof.AddTo, run later in Build, moves it to the rooftop range.
        /// </summary>
        static readonly Vector3 HitDummyPosition = new Vector3(27.5f, FloorHeight, -7f);
        /// <summary>The dummy faces -Z (toward the work area) rather than the mirror, so it shows side-on in it.</summary>
        const float HitDummyYaw = 180f;

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
        /// <summary>Desk-top height of the kit's workstation bench, metres (kit_pieces DESK_H).</summary>
        const float DeskTop = 0.74f;
        /// <summary>Half the depth of the vending machine and the slipper cabinet, metres (kit_pieces VEND_D, SLIPPER_D):
        /// their backs sit on the wall face.</summary>
        const float VendHalfDepth = 0.325f, SlipperHalfDepth = 0.175f;
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
            // WHY here: bounce light and floor reflections are baked into the scene, so a rebuild without them
            // loses the polished floor's mirror (Alex, 2026-11-13: "the mirror is missing"). Order as BounceLight's
            // own report asks: bounce first, then the reflections, so they include the lifted ceiling.
            Debug.Log(BounceLight.Add(askFirst: false));
            Debug.Log(FloorReflections.Bake(askFirst: false));
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
            // WHY these names: BounceLight and FloorReflections find the building by a "Kit Office" root and its
            // storeys by "Floor N" children, so the root keeps that name and each storey gets a marker.
            var root = new GameObject("Kit Office").transform;
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

            for (int level = 0; level < Levels; level++) G($"Floor {level}", level * FloorHeight);

            // Slabs: the ground floor, the upper floor (open over the atrium), and the roof.
            for (int level = 0; level <= Levels; level++)
            {
                var slabs = G(level == 0 ? "Ground slabs" : level == Levels ? "Roof slabs" : $"Level {level} slabs", level * FloorHeight);
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
            P(zoneL, "Wall_Solid_4m", AtriumX0, -4f, 90f);          // white: the water wall on its atrium face,
            P(zoneL, "Partition_Frost_4m", AtriumX0, -12f, 90f);     // a 4 m open mouth between them
            P(zoneL, "Wall_Solid_4m", 0f, -16f);                      // L / C boundary: solid, then the room's glass

            // Zone C (claims): the curved header-band glass meeting room of Alex's concept (version A): straight glass
            // on the north, a curved corner at the NE, a 2 m open doorway on the east, then straight glass again.
            var zoneC = G("Zone C - meeting room");
            P(zoneC, "Partition_Header_2m", 4f, -16f);
            P(zoneC, "Partition_Header_Curve_2x2", 8f, -16f, 90f);   // yaw 90: convex toward the NE corridor
            P(zoneC, "Partition_Header_4m", 8f, -20f, 90f);
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
                P(guards, "Guard_Rail_4m", AtriumX0, z, 90f);            // west edge: on the deck side
                P(guards, "Guard_Rail_4m", AtriumX1, z - Cell, -90f);    // east edge
            }
            for (float x = AtriumX0; x < AtriumX1; x += Cell)
                P(guards, "Guard_Rail_4m", x + Cell, AtriumZ0, 180f);   // north edge
            P(guards, "Guard_Rail_2m", StairPivot.x + 2f, AtriumZ1);        // south edge, beside the stair's landing
            P(guards, "Guard_Rail_4m", AtriumX1 - Cell, AtriumZ1);
            // Monumental graphic columns at three atrium corners (the SW corner is the stair's landing), and the
            // reception desk facing the window corridor, as in Alex's atrium plate.
            // Each turned so its graphic faces (its +Z and +X) look into the atrium.
            foreach (var (cx, cz, yaw) in new[] { (AtriumX0, AtriumZ0, 90f), (AtriumX1, AtriumZ0, 180f), (AtriumX1, AtriumZ1, -90f) })
                P(zoneH, "Column_Graphic_Tall", cx, cz, yaw);
            P(zoneH, "Desk_Reception", 17.5f, -6.2f);
            P(zoneH, "Planter_Box", 15.2f, -5.2f);

            // The Japanese corporate mix (ArtSource/Kit/concepts/Japanese_Aero/DECISION.md). The x 12 wall between L and
            // H carries the AONAGI logo and the dot-matrix reception sign on its west face (seen down the window
            // corridor), with the water server and umbrella stand below; its atrium face carries the water wall and
            // a planter. Wall-hung pieces: pivot at the low-X edge on the face; yaw -90 faces -X, yaw 90 faces +X.
            var aonagi = G("AONAGI identity");
            P(aonagi, "Logo_Aonagi", AtriumX0 - 0.2f, -7.6f, -90f);
            P(aonagi, "Sign_LED_Reception", AtriumX0 - 0.2f, -7.0f, -90f);
            P(aonagi, "Water_Server", AtriumX0 - 0.6f, -4.7f, -90f);
            P(aonagi, "Umbrella_Stand", AtriumX0 - 0.45f, -5.3f);
            P(aonagi, "Water_Wall", AtriumX0, -5.1f, 90f);
            P(aonagi, "Planter_Box", AtriumX0 + 1.0f, -6.0f, 90f);

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
            P(upper, "Counter_Capsule_2m", 26f, -18f, 90f);
            P(upper, "Cover_Block_2m", 16f, -20f, 90f);

            // Zone supergraphics: painted straight onto a white wall that zone's players face (pivot = the graphic's
            // low-X edge on the wall face; yaw turns its readable +Z side away from the wall). 3.2 m wide since
            // 2026-11-13 (Alex: "think Mirror's Edge"), so each sits 0.4 m further toward its low end than the
            // 2.4 m panels did, keeping the same centre.
            var signs = G("Zone signs");
            P(signs, "Sign_Zone_L", 0.4f, -16f);                    // L / C wall, north face, seen down the loop
            P(signs, "Sign_Zone_C", 2.4f, -Depth + 0.2f);            // C room's south wall, inside face
            P(signs, "Sign_Zone_A", 14.4f, -Depth + 0.2f);           // A bay's south wall, inside face
            P(signs, "Sign_Zone_H", AtriumX1 - 0.2f, -15.6f, -90f);  // atrium's east wall, the white face toward H
            P(signs, "Sign_Zone_R", Width - 0.2f, -7.6f, -90f);      // east wall, inside face, at the junction

            // Furniture for scale: sculptural desks with pod chairs (chairs face the desk, -Z, so yaw 180) and a
            // second counter in the junction.
            var furniture = G("Furniture");
            P(furniture, "Desk_Sculpt", 2f, -18f);
            P(furniture, "Chair_Pod", 2f, -17.2f, 180f);
            P(furniture, "Counter_Capsule_2m", 30f, -6.5f, 90f);
            var upperFurniture = G("Upper floor furniture", FloorHeight);
            foreach (float x in new[] { 3f, 6.5f })
            {
                P(upperFurniture, "Desk_Sculpt", x, -19f);
                P(upperFurniture, "Chair_Pod", x, -18.2f, 180f);
            }

            // Work areas, after Alex's office-floor plate: workstation benches with task chairs, low storage and a
            // copier, on dark carpet under a tiled ceiling with linear pendants. Upstairs west and east, and the
            // ground-floor meeting room; the corridors, atrium and junctions keep the polished floor.
            var work = G("Work areas - furniture", FloorHeight);
            foreach (var (bx, bz) in new[] { (3.5f, -9f), (8.5f, -9f), (3.5f, -13f), (8.5f, -13f), (24f, -12f), (28.5f, -12f) })
                Bench(work, bx, bz);
            P(work, "Storage_Low", Width - 0.45f, -21f, -90f);
            P(work, "Copier", Width - 0.6f, -23.2f, -90f);
            P(work, "Storage_Low", 0.45f, -6f, 90f);
            P(work, "Planter_Box", 10.5f, -16.5f);
            P(work, "Planter_Box", 22f, -4.8f);
            // The office's daily life against the upper floor's solid south wall: a vending machine and the
            // slipper cabinet (tall props only against opaque walls, so they make no new cover in a route).
            P(work, "Vending_Machine", 10.4f, -Depth + 0.2f + VendHalfDepth);
            P(work, "Slipper_Cabinet", 8.6f, -Depth + 0.2f + SlipperHalfDepth);
            var groundWork = G("Work areas - ground");
            Bench(groundWork, 23.5f, -21f);
            P(groundWork, "Copier", 13f, -23.3f);
            foreach (float px in new[] { 10.5f, 26.5f }) P(groundWork, "Planter_Box", px, -3.3f);
            // Turned 180 so the graphic faces (-Z, -X) meet players coming out of the zones and along the corridor.
            P(groundWork, "Column_Graphic", 6f, -2.2f, 180f);
            P(groundWork, "Column_Graphic", 18f, -2.2f, 180f);

            void Bench(Transform t, float x, float z)
            {
                P(t, "Workstation_Bench", x, z);
                foreach (float dx in new[] { -0.6f, 0.6f })
                {
                    P(t, "Chair_Task", x + dx, z + 1.05f, 180f);   // the +Z side's chairs face the desk, -Z
                    P(t, "Chair_Task", x + dx, z - 1.05f);         // the -Z side's face +Z
                }
                // A Japanese desk island's head end: a phone and a document tray on each side's end desk, clear of
                // the monitors and keyboards (they stop at 0.82 m from the bench centre).
                OnDesk(t, "Phone_Desk", x + 0.98f, z + 0.42f, 180f);
                OnDesk(t, "Doc_Tray", x - 0.98f, z + 0.45f, 180f);
                OnDesk(t, "Phone_Desk", x - 0.98f, z - 0.42f);
                OnDesk(t, "Doc_Tray", x + 0.98f, z - 0.45f);
            }
            void OnDesk(Transform t, string piece, float x, float z, float yaw = 0f)
            {
                var go = P(t, piece, x, z, yaw);
                go.transform.localPosition = new Vector3(x, DeskTop, z);
            }
            var carpets = G("Work areas - carpet and ceilings");
            foreach (var (level, x0, x1, z0, z1) in new[] { (1, 0f, 12f, -4f, -24f), (1, 20f, 32f, -4f, -24f), (0, 0f, 8f, -16f, -24f) })
                for (float x = x0; x < x1; x += Cell)
                    for (float z = z0; z > z1; z -= Cell)
                    {
                        float floorY = level * FloorHeight, soffit = floorY + FloorHeight - 0.2f;
                        P(carpets, "Carpet_Overlay_4x4", x, z).transform.localPosition = new Vector3(x, floorY, z);
                        P(carpets, "Ceiling_Grid_4x4", x, z).transform.localPosition = new Vector3(x, soffit, z);
                        P(carpets, "Light_Pendant_Linear", x, z).transform.localPosition =
                            new Vector3(x + Cell * 0.5f, soffit, z - Cell * 0.5f);
                    }

            Figures(root);
            Massing(root);
            DevMirror();
            HitDummy();
            // The rooftop range (the elevator session's KitOfficeRoof.cs): roof guard rails, the dummy pad, the
            // arsenal bay and the pool; it also moves the HitDummy up there and wires the lift if one is built.
            Debug.Log(KitOfficeRoof.AddTo(root));
            AddCamera();
            return count;
        }

        /// <summary>
        /// Puts the animation work's DevMirror back where it stood in the labyrinth: a mirror wall along the
        /// upper floor's east side, so a player can watch their own character. Skipped if the prefab is gone.
        /// </summary>
        /// <remarks>Position, turn and size copied from the scene it was placed in (KitOffice at d4cce74).</remarks>
        /// <summary>Places the hit-reaction session's HitDummy where it can be shot and seen in the DevMirror.
        /// Skipped if the prefab is gone.</summary>
        static void HitDummy()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitDummyPath);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(HitDummyPosition, Quaternion.Euler(0f, HitDummyYaw, 0f));
        }

        static void DevMirror()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DevMirrorPath);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(DevMirrorPosition, Quaternion.Euler(0f, DevMirrorYaw, 0f));
            go.transform.localScale = DevMirrorScale;
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
