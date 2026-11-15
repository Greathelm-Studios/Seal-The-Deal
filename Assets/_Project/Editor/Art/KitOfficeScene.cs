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
//  Modified  : Claude Opus 5.5, 2026-11-13 — the lift (shaft, landings, car, leaves), loose monitors and keyboards
//              on the benches, the rooftop range hook (KitOfficeRoof), Mirror's-Edge-width zone graphics.
//  Modified  : Claude Opus 5.5, 2026-11-14 — AEROSET reference: glass curtain walls on the outer bays nothing hangs
//              on, exposed ceiling beams.
//  Modified  : Claude Opus 5.5, 2026-11-15 — zone signs are textured quads (KitSigns), not modelled pieces.
//  Modified  : Claude Opus 5.5, 2026-11-15 — ribbons in the window corridor, wave and logo lightboxes, floating
//              ceiling panels off the corridor.
//  Modified  : Claude Opus 5.5, 2026-11-15 — every placed piece gets its texture labels (KitSigns.Decorate).
//  Modified  : Claude Opus 5.5, 2026-11-15 — the L2 "twin-core court" floor plan Alex approved
//              (ArtSource/Kit/concepts/Layout/DECISION_L2.md) replaces the labyrinth: a 40 x 32 m plate, a
//              double-height court with a feature stair, two cores with enclosed return stairs and two-door rooms,
//              a continuous ring, office wings, the lift on the north façade opening onto the court.
//  Modified  : Claude Opus 5.5, 2026-11-15 — the marking system (concepts/Layout/MARKINGS.md, Alex: "bland and
//              boring ... follow the structure with purpose and intent"): walls read from markings/l2_on_kit.json,
//              floor and ceiling mark sheets, door plates, evacuation plans, stair floor numbers, the feature stair's
//              cobalt stringer band, firm lightboxes at the receptions; panels only over rooms; ribbons retired.
//  Modified  : Claude Opus 5.5, 2026-11-15 — the build gives every NetworkObject its FishNet scene ID and the
//              NetworkManager its spawnable-prefab list before the last save, so the lift and roof props spawn in Play
//              after a rebuild (AgentHub #587, 58c9b4e).
//  Modified  : Claude Opus 5.5, 2026-11-15 evening — light pools: spots over rooms and desk areas, wall-washers on
//              the lightboxes, a spot on the court's plinth; corridors left darker (Alex: "too evenly lit").
//  Modified  : Claude Opus 5.5, 2026-11-15 evening - clipping fixes: walls trimmed at the facade, the stair holes
//              clear of the flights, south corner columns.
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Lays out the office floor from the kit prefabs, so the approved kit and floor plan can be walked
//              and lit in Unity as a space. Its own file because KitImport proves the pieces snap (a small
//              test layout) while this composes a whole level section to judge; it is a review scene, not
//              the game's level: the match's own arena is built elsewhere.
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
    /// Builds <c>KitOffice.unity</c>: the L2 twin-core court office floor, two storeys and a roof, from the kit prefabs.
    /// Menu: <c>Close the Deal &gt; Art &gt; Build Kit Office Scene</c>.
    /// </summary>
    /// <remarks>
    /// <para>Run <see cref="KitImport"/> first; this only places its prefabs. Re-running overwrites KitOffice.unity.
    /// The menu opens the scene on its own and then applies <see cref="OfficeLook"/>. <see cref="BuildAdditive"/>
    /// instead builds beside whatever is open, without touching it, and copies the open scene's look; it exists
    /// so the scene can be made while someone else has unsaved work open in the same Editor.</para>
    /// <para>The layout is written in the L2 drawings' plan coordinates (metres, x east and y south from the NW
    /// corner; Unity X = x, Z = −y), so every number here can be checked against
    /// ArtSource/Kit/concepts/Layout/L2/plan.png. The plan is point-symmetric about the court's centre except
    /// the lift and the feature stair, so one firm's half is written once and turned 180° for the other.</para>
    /// </remarks>
    public static class KitOfficeScene
    {
        const string ScenePath = "Assets/_Project/Scenes/KitOffice.unity";
        const string PrefabFolder = "Assets/_Project/Art/Kit/Prefabs";
        const string PaperWhitePath = "Assets/_Project/Art/Kit/Materials/Wall_PaperWhite.mat";
        const string NavyPath = "Assets/_Project/Art/Kit/Materials/Trim_Navy.mat";
        const string VolumeProfilePath = "Assets/_Project/Art/Look/OfficeLook_Volume.asset";
        const string DevMirrorPath = "Assets/_Project/Prefabs/Dev/DevMirror.prefab";
        /// <summary>
        /// DevMirror placement, world metres and degrees: along the upper floor's south wall west of the lift baffle
        /// (x 0.4–19.6), facing north into the window hall. Height and depth as placed by hand in KitOffice (d4cce74);
        /// moved for the L2 plan, whose east side is rooms. WHY yaw 180: at the old yaw 90 it faced −X, so 180 faces +Z.
        /// </summary>
        static readonly Vector3 DevMirrorPosition = new Vector3(10f, 6.34f, -31.75f);
        const float DevMirrorYaw = 180f;
        static readonly Vector3 DevMirrorScale = new Vector3(19.2f, 3.45f, 1f);
        const string HitDummyPath = "Assets/_Project/Prefabs/Dev/HitDummy.prefab";
        /// <summary>
        /// HitDummy starting spot, world metres: the upper floor's open hall south of the court. Only a starting spot:
        /// KitOfficeRoof.AddTo, run later in Build, moves it to the rooftop range once the lift exists.
        /// </summary>
        static readonly Vector3 HitDummyPosition = new Vector3(24f, FloorHeight, -25f);
        /// <summary>The dummy faces −Z, toward the hall.</summary>
        const float HitDummyYaw = 180f;

        // ---- L2 plan, metres (ArtSource/Kit/concepts/Layout/L2/layouts.json), x east and y south from the NW corner
        /// <summary>Floor plate: 10 × 8 bays of 4 m, so 40 × 32 m (L2 plan).</summary>
        const int Cols = 10, Rows = 8;
        const float Cell = 4f;
        /// <summary>Half bay, metres: every L2 wall, door and hole is snapped to this grid, the kit's 2 m pieces.</summary>
        const float Half = 2f;
        const float Width = Cols * Cell, Depth = Rows * Cell;
        const float FloorHeight = 4.5f;
        /// <summary>
        /// Storeys built: the ground floor and the upper floor round the court; a roof slab caps them. WHY two, not the
        /// L2 stack's three: the rooftop range (KitOfficeRoof) and the lift's travel are built for a roof at 9 m. The
        /// shell repeats per storey, so a third is one more pass of the same loops once the roof moves.
        /// </summary>
        const int Levels = 2;
        /// <summary>Court, double height (L2 "atrium"): x 16–24, y 10–22 on the ground; the upper slab is open over y 12–20.</summary>
        const float CourtX0 = 16f, CourtX1 = 24f, VoidY0 = 12f, VoidY1 = 20f;
        /// <summary>
        /// Feature flight's foot, plan metres: it rises 7.2 m west to the void's west edge (x 16) along y 12–14, the L2
        /// flight moved 0.4 m so its top meets the slab edge on the 2 m grid. Yaw 90 turns the kit stair (rising −Z,
        /// width +X) to rise −X with its width toward +y.
        /// </summary>
        const float FeatureFootX = CourtX0 + 7.2f, FeatureY = VoidY0;
        /// <summary>The lift shaft, plan metres: the L2 "LIFT" box on the north façade, its doors facing south into the lobby.</summary>
        const float ShaftX0 = 18f, ShaftX1 = 22f, ShaftY1 = 4f;
        /// <summary>
        /// The car's centre, Unity metres: centred across the shaft, 2.075 m back from the door wall, the same 0.05 m
        /// clearance to the landing leaves as before (kit LIFT_CAR_*), which leaves 0.05 m to the façade-side shaft wall.
        /// </summary>
        static readonly Vector2 CarCentre = new Vector2(20f, -1.925f);
        /// <summary>Landing leaves' meeting edge, Unity metres: centre of the 1.4 m opening, 0.225 m into the shaft.</summary>
        static readonly Vector2 LeafEdge = new Vector2(20f, -3.775f);
        /// <summary>Roof level, metres: the top of the roof slab, where the head-house stands.</summary>
        const float RoofY = Levels * FloorHeight;
        /// <summary>Mid-landing height of the enclosed return stairs, metres: two Stair_Straight_Half_2m flights.</summary>
        const float LandingY = FloorHeight * 0.5f;
        /// <summary>Desk-top height of the kit's workstation bench, metres (kit_pieces DESK_H).</summary>
        const float DeskTop = 0.74f;
        /// <summary>
        /// PC tower spot, bench-local metres: 0.12 m past the 1.2 m bench end (half the 0.19 m tower plus a gap), centred
        /// on the pedestal's depth (kit PED at z ±0.23–0.78).
        /// </summary>
        const float PcTowerX = 1.32f, PcTowerZ = 0.55f;
        /// <summary>Half the depth of the vending machine, metres (kit_pieces VEND_D): its back sits on the wall face.</summary>
        const float VendHalfDepth = 0.325f;
        /// <summary>White block under the floor plate, metres: the windows look down at it, not into nothing.</summary>
        const float MassingHeight = 30f;
        /// <summary>Stand-in size: the 1.8 m player and a shoulder-width capsule.</summary>
        const float FigureHeight = 1.8f, FigureRadius = 0.25f;
        /// <summary>Where stand-ins stand, Unity metres: the west ring, the case in the court, the kitchen, the legal
        /// conference, the bridge and the east ring upstairs.</summary>
        static readonly Vector3[] Spots =
        {
            new Vector3(6f, 0f, -15f), new Vector3(20f, 0f, -16f), new Vector3(11f, 0f, -19f), new Vector3(1f, 0f, -14f),
            new Vector3(20f, FloorHeight, -11f), new Vector3(34f, FloorHeight, -20f),
        };
        /// <summary>
        /// Where players start in Play, Unity metres: the L2 firm spawns in opposite corners, A at (4, 4) by the
        /// legal reception and B at (36, 28) by the review reception, 0.1 m above the floor as in Greybox.
        /// </summary>
        static readonly Vector3 SpawnA = new Vector3(4f, 0.1f, -4f), SpawnB = new Vector3(36f, 0.1f, -28f);

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
        /// Opens a new scene in place of whatever is open (without asking), builds the office, adds the player
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
            // WHY the marks are hidden for the bake: with the marking sheets in the capture the probes came out with a
            // strong cyan cast over every shadowed floor and pane (Alex: "get rid of that weird cyan stuff", 2026-11-15).
            // They are flush decals; the floor's reflections don't need them.
            var marks = GameObject.Find("Kit Office/Markings");
            if (marks != null) marks.SetActive(false);
            Debug.Log(FloorReflections.Bake(askFirst: false));
            // Zone probes after the floor probes (that bake flags the building for probes): local highlights on gloss
            // props (2026-11-15, the props' concept sheets).
            Debug.Log(ZoneReflections.Bake(askFirst: false));
            if (marks != null) marks.SetActive(true);
            Debug.Log(GiveNetworkSceneIds(scene));
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Gives every NetworkObject in <paramref name="scene"/> a FishNet scene ID, as Tools &gt; Fish-Networking &gt;
        /// Utility &gt; Reserialize NetworkObjects does for open scenes. Call after the scene has its path (it seeds the IDs)
        /// and before the last save.
        /// </summary>
        /// <remarks>
        /// WHY: KitOfficeRoof adds the lift's NetworkObject (and the roof props come as networked prefabs) with no scene
        /// ID; FishNet then switches them off on Play and never spawns them, so the lift vanished in Play while it showed
        /// in the Scene view (AgentHub #587; fixed once by hand in 58c9b4e, undone by every rebuild until this ran here).
        /// FishNet keeps CreateSceneId, RemoveDuplicateNetworkObjects and ReserializeEditorSetValues internal, so they
        /// are reached by reflection; the alternative, opening the menu's window, needs a click. If FishNet renames
        /// them this throws, so a rebuild can't silently bring the bug back.
        /// </remarks>
        /// <returns>A one-line report: how many NetworkObjects were given IDs.</returns>
        static string GiveNetworkSceneIds(Scene scene)
        {
            const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
            var type = typeof(FishNet.Object.NetworkObject);
            // WHY the parameter lists: CreateSceneId has overloads, so the name alone is ambiguous.
            var create = type.GetMethod("CreateSceneId", Flags, null,
                new[] { typeof(Scene), typeof(bool), typeof(int).MakeByRefType() }, null);
            var dedupe = type.GetMethod("RemoveDuplicateNetworkObjects", Flags, null, System.Type.EmptyTypes, null);
            var reserialize = type.GetMethod("ReserializeEditorSetValues", Flags, null, new[] { typeof(bool), typeof(bool) }, null);
            if (create == null || dedupe == null || reserialize == null)
                throw new System.MissingMethodException("FishNet's scene-ID methods have moved; run Tools > Fish-Networking > Utility > Reserialize NetworkObjects on KitOffice by hand and update KitOfficeScene.GiveNetworkSceneIds.");
            var args = new object[] { scene, true, 0 };   // force: true, as the menu's open-scenes pass
            var found = (List<FishNet.Object.NetworkObject>)create.Invoke(null, args);
            foreach (var nob in found)
            {
                dedupe.Invoke(nob, null);
                reserialize.Invoke(nob, new object[] { true, false });   // setWasActiveDuringEdit, setSceneId
                EditorUtility.SetDirty(nob);
            }
            // The NetworkManager the play rig adds has no spawnable-prefab list, so FishNet's NetworkManager throws on Start,
            // never initialises, and every scene NetworkObject stays switched off. 58c9b4e set it by hand to the project's
            // DefaultPrefabObjects; this does the same on every build.
            string prefabs = "";
            foreach (var manager in Object.FindObjectsByType<FishNet.Managing.NetworkManager>(FindObjectsInactive.Include))
            {
                if (manager.gameObject.scene != scene || manager.SpawnablePrefabs != null) continue;
                manager.SpawnablePrefabs = AssetDatabase.LoadAssetAtPath<FishNet.Managing.Object.PrefabObjects>(DefaultPrefabObjectsPath)
                    ?? throw new FileNotFoundException($"No FishNet prefab list at {DefaultPrefabObjectsPath}.");
                EditorUtility.SetDirty(manager);
                prefabs = $" {manager.name}'s spawnable prefabs set to {DefaultPrefabObjectsPath}.";
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return $"[Kit Office] FishNet scene IDs: {found.Count} NetworkObjects, {(int)args[2]} changed.{prefabs}";
        }

        /// <summary>
        /// Adds the player rig (network manager, spawns, menu, follow camera) to the open scene so Play
        /// puts a player at the firm spawns. Adds only what is missing and does not save.
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
                report.AppendLine(GiveNetworkSceneIds(scene));   // after the save: the IDs are seeded from the scene's path
                EditorSceneManager.SaveScene(scene);
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

        /// <summary>Which way a wall run is built: the kit family it takes its 4 m, 2 m and door pieces from.</summary>
        enum Kind { Solid, Glass, Frost, Guard }

        /// <summary>A plan rectangle, metres: x0..x1 east, y0..y1 south.</summary>
        readonly struct Rect2
        {
            public readonly float X0, Y0, X1, Y1;
            public Rect2(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
            public bool Overlaps(Rect2 o) => X0 < o.X1 - 0.01f && o.X0 < X1 - 0.01f && Y0 < o.Y1 - 0.01f && o.Y0 < Y1 - 0.01f;
            /// <summary>The same rectangle turned 180° about the plate's centre (the other firm's half).</summary>
            public Rect2 Turned() => new Rect2(Width - X1, Depth - Y1, Width - X0, Depth - Y0);
        }

        /// <summary>Holes in the upper slab: the lift shaft, the court's void and the two enclosed stairs' upper flights.</summary>
        static readonly Rect2 Shaft = new Rect2(ShaftX0, 0f, ShaftX1, ShaftY1);
        static readonly Rect2 Void = new Rect2(CourtX0, VoidY0, CourtX1, VoidY1);
        /// <summary>E1's upper flight hole (x 12–14, y 10–14); E2's is the same turned.</summary>
        // WHY from x 11.8, not the 2 m grid's 12: the 2 m flight runs x 11.8-13.8, and a hole edge at 12 put the slab through
        // its west stringer (clipping sweep, 2026-11-15). The 0.2 m strip is a scaled 2 x 2 slab (see the slab fill).
        static readonly Rect2 StairHole = new Rect2(11.8f, 10f, 14f, 14f);
        // ---- Light pools (2026-11-15 evening, Alex: "overly bright and too evenly lit, hurts my eyes"): real spot lights
        // over rooms, desk areas and feature walls, none in the corridors, so the floor reads as lit places with
        // darker gaps between them. Shadowless, so 40-odd of them stay cheap under Forward+.
        /// <summary>Pool light colour: a faintly warm white, against the cooler daylight from the windows.</summary>
        static readonly Color PoolColour = new Color(1f, 0.965f, 0.925f);
        /// <summary>Spot under a room or desk-area ceiling panel: intensity, range (metres), cone (degrees), drop below the soffit (metres).</summary>
        const float RoomPoolIntensity = 6f, RoomPoolRange = 6f, RoomPoolAngle = 100f, PoolDrop = 0.25f;
        /// <summary>Wall-washer at a feature lightbox: intensity, range, cone, and how far out from the wall it hangs, metres.</summary>
        const float WashIntensity = 5f, WashRange = 6f, WashAngle = 80f, WashOffset = 1.2f;
        /// <summary>The court's spot on the closing-table plinth, from the roof soffit 8.8 m up: intensity, range, cone.</summary>
        const float PlinthIntensity = 14f, PlinthRange = 12f, PlinthAngle = 50f;
        /// <summary>FishNet's generated list of spawnable prefabs, which the scene's NetworkManager needs to initialise.</summary>
        const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";
        /// <summary>The L2 walls and doors as built on the kit grid: shared with make_markings.py, so every mark stops at a real doorway.</summary>
        const string KitPlanPath = "ArtSource/Kit/markings/l2_on_kit.json";
        /// <summary>Floor mark sheet above the slab, metres. WHY 12 mm: over the 8 mm carpet overlay, too thin to see as a step.</summary>
        const float FloorMarkLift = 0.012f;
        /// <summary>Soffit mark sheets below the soffit, metres: under the 8 mm flush tile grid; the cove 2 mm under the bands.</summary>
        const float SoffitMarkDrop = 0.012f, CoveDrop = 0.014f;
        /// <summary>Sheet size, metres: the mark textures cover 40 × 40 m from the NW corner (make_markings.py SHEET_M).</summary>
        const float SheetSize = 40f;
        /// <summary>How far a wall decal stands off its wall face, metres: clear of the walls' 3 mm joint lines (KitSigns.WallGap).</summary>
        const float DecalGap = 0.006f;
        /// <summary>Door plate: its size, centre height and offset from the door's centre along the wall (onto the jamb), metres.</summary>
        static readonly Vector2 PlateSize = new Vector2(0.32f, 0.14f);
        const float PlateY = 1.5f, PlateAlong = 0.8f;
        /// <summary>Evacuation plan card size, metres (make_markings.py evac()).</summary>
        static readonly Vector2 EvacSize = new Vector2(0.6f, 0.42f);
        /// <summary>Stair floor-number graphic size, metres (make_markings.py floor_numbers()).</summary>
        static readonly Vector2 FloorNoSize = new Vector2(1.2f, 0.9f);
        /// <summary>The feature stair's cobalt band: its height on the stringer, metres, and its ends (x, y) along the nosing
        /// line 0.15 m under it, from the foot to the top (kit stair: 24 risers of 0.1875 m over 7.2 m).</summary>
        const float StringerBandHeight = 0.12f;
        static readonly Vector2 StringerFoot = new Vector2(FeatureFootX, 0.05f), StringerTop = new Vector2(CourtX0, 4.35f);

        [System.Serializable] sealed class KitDoor { public float at; public string room; public float[] face; }
        /// <remarks>trimA, trimB: metres cut off the run's start or end where it meets the facade, so the wall stops at the curtain
        /// wall's or window sill's inner face instead of running into the glass (clipping sweep, 2026-11-15).</remarks>
        [System.Serializable] sealed class KitRun { public string kind; public float[] a, b; public KitDoor[] doors; public float trimA, trimB; }
        static readonly float[] NoDoors = new float[0];
        [System.Serializable] sealed class KitEvac { public string id; public float[] at, face; public float y; }
        [System.Serializable] sealed class KitDecals { public KitEvac[] evac; }
        /// <summary>markings/l2_on_kit.json as JsonUtility reads it (its "rooms" and "districts" maps are for make_markings.py).</summary>
        [System.Serializable] sealed class KitPlan { public KitRun[] half, once, groundHalf, upperHalf; public KitDecals decals; }
        /// <summary>
        /// Carpet, tiled ceiling and pendants (the work areas, as on Alex's office-floor plate): the window hall's desk
        /// cells and the wing rooms. One firm's half; the other's is the same turned.
        /// </summary>
        static readonly Rect2[] Carpeted =
        {
            new Rect2(0f, 0f, 4f, 4f), new Rect2(8f, 0f, 16f, 4f),     // window hall desks
            new Rect2(0f, 8f, 4f, 16f), new Rect2(0f, 20f, 4f, 28f),    // the legal conference and the back office
        };

        /// <summary>Places every piece into <paramref name="scene"/> (which must be the active scene). Returns the count.</summary>
        static int Build(Scene scene)
        {
            // WHY these names: BounceLight and FloorReflections find the building by a "Kit Office" root and its
            // storeys by "Floor N" children, so the root keeps that name and each storey gets a marker.
            KitSigns.Refresh();
            var plan = JsonUtility.FromJson<KitPlan>(File.ReadAllText(KitPlanPath));
            var root = new GameObject("Kit Office").transform;
            int count = 0;
            // Walls placed on each storey, plan metres, for the ceiling pass: a cell a wall crosses gets the flush tile
            // grid, since a hanging panel would cut through the wall.
            var walls = new List<Rect2>[Levels];
            for (int level = 0; level < Levels; level++) walls[level] = new List<Rect2>();

            Transform G(string name, float y = 0f)
            {
                var g = new GameObject(name).transform;
                g.SetParent(root, false);
                g.localPosition = new Vector3(0f, y, 0f);
                return g;
            }
            GameObject P(Transform parent, string piece, float x, float z, float yaw = 0f, bool decorate = true)
            {
                count++;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{piece}.prefab");
                if (prefab == null) throw new FileNotFoundException($"No kit prefab {piece}; run Import Kit first.");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetLocalPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
                if (decorate) KitSigns.Decorate(go, piece);   // its labels, as textured quads (2026-11-15)
                return go;
            }
            // Plan metres to Unity: X = x, Z = −y; turned is the other firm's half, rotated 180° about the plate's centre.
            Vector2 U(float x, float y, bool turned) => turned ? new Vector2(Width - x, -(Depth - y)) : new Vector2(x, -y);
            GameObject Put(Transform parent, string piece, float x, float y, float yaw, bool turned, bool decorate = true)
            {
                Vector2 u = U(x, y, turned);
                return P(parent, piece, u.x, u.y, yaw + (turned ? 180f : 0f), decorate);
            }
            // A shadowless spot light at a Unity-space point, aimed along dir (a pool of light, see PoolColour).
            void Pool(Transform t, Vector3 at, Vector3 dir, float intensity, float range, float angle)
            {
                var go = new GameObject("Light Pool");
                go.transform.SetParent(t, false);
                go.transform.SetLocalPositionAndRotation(at, Quaternion.LookRotation(dir));
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = PoolColour;
                light.intensity = intensity;
                light.range = range;
                light.spotAngle = angle;
                light.innerSpotAngle = angle * 0.45f;   // a soft-edged pool, not a hard disc
                light.shadows = LightShadows.None;
            }
            // A plan direction to Unity (X = x, Z = −y), turned with the half.
            Vector3 Dir(float fx, float fy, bool turned) => turned ? new Vector3(-fx, 0f, fy) : new Vector3(fx, 0f, -fy);
            // A wall decal: centre (x, y) on the wall face in plan metres, height h, facing plan direction (fx, fy).
            GameObject WallDecal(Transform t, string texture, float x, float y, float h, float fx, float fy, Vector2 size, bool turned)
            {
                Vector2 u = U(x + fx * DecalGap, y + fy * DecalGap, turned);
                return KitSigns.Decal(t, texture, new Vector3(u.x, h, u.y), KitSigns.Facing(Dir(fx, fy, turned), Vector3.up), size);
            }
            void Runs(Transform t, int level, KitRun[] runs, bool turned)
            {
                foreach (var r in runs)
                {
                    var doors = new float[r.doors.Length];
                    for (int k = 0; k < doors.Length; k++) doors[k] = r.doors[k].at;
                    Run(t, level, ParseKind(r.kind), r.a[0], r.a[1], r.b[0], r.b[1], turned, doors, r.trimA, r.trimB);
                }
            }
            // A straight wall run from (ax, ay) to (bx, by), plan metres, tiled from the kit's 4 m and 2 m pieces. The
            // kit runs a piece +X from its pivot with its thickness toward −Z, so the thickness always lands on the
            // right of the direction of travel as the plan is drawn (east → south side): pick the direction to suit.
            // doors: centres of door openings along the run's axis. A centre 1 m into a 2 m step takes a 2 m door piece;
            // one 2 m into a 4 m step of solid wall takes Wall_Door_4m.
            // trimA, trimB: metres off the first and last piece, which is shortened (scaled along its run) to fit.
            void Run(Transform t, int level, Kind kind, float ax, float ay, float bx, float by, bool turned, float[] doors,
                     float trimA = 0f, float trimB = 0f)
            {
                float dx = bx - ax, dy = by - ay, len = Mathf.Abs(dx) + Mathf.Abs(dy);
                if (dx != 0f && dy != 0f) throw new System.ArgumentException("Wall runs are axis-aligned.");
                float ux = Mathf.Sign(dx) * (dx != 0f ? 1f : 0f), uy = Mathf.Sign(dy) * (dy != 0f ? 1f : 0f);
                float yaw = dx > 0f ? 0f : dx < 0f ? 180f : dy > 0f ? 90f : -90f;
                float start = dx != 0f ? ax : ay;
                const float eps = 0.01f;
                for (float s = 0f; s < len - eps;)
                {
                    float rest = len - s;
                    if (rest < Half - eps) throw new System.ArgumentException($"Wall run ({ax},{ay})-({bx},{by}) is not on the 2 m grid.");
                    float near = float.NaN;
                    foreach (float c in doors)
                    {
                        float d = Mathf.Abs(c - start) - s;
                        if (d > eps && d < Cell + eps && (float.IsNaN(near) || d < near)) near = d;
                    }
                    string piece;
                    float step;
                    if (Mathf.Abs(near - 1f) < eps) { piece = DoorPiece(kind); step = Half; }
                    else if (Mathf.Abs(near - Half) < eps && rest > Cell - eps && kind == Kind.Solid) { piece = "Wall_Door_4m"; step = Cell; }
                    else if (float.IsNaN(near) && rest > Cell - eps) { piece = PlainPiece(kind, 4); step = Cell; }
                    else { piece = PlainPiece(kind, 2); step = Half; }
                    bool first = s < eps, last = s + step > len - eps;
                    float headCut = first ? trimA : 0f, cut = headCut + (last ? trimB : 0f);
                    var go = Put(t, piece, ax + ux * (s + headCut), ay + uy * (s + headCut), yaw, turned);
                    // WHY scaled, not a shorter kit piece: the trims are a few centimetres at the facade only; the
                    // few-percent stretch of the joint lines there doesn't read, and the kit stays on its 2 m modules.
                    if (cut > 0f) go.transform.localScale = new Vector3((step - cut) / step, 1f, 1f);
                    s += step;
                }
                if (kind == Kind.Guard) return;
                var r = new Rect2(Mathf.Min(ax, bx), Mathf.Min(ay, by), Mathf.Max(ax, bx), Mathf.Max(ay, by));
                walls[level].Add(turned ? r.Turned() : r);
            }

            for (int level = 0; level < Levels; level++) G($"Floor {level}", level * FloorHeight);

            // Slabs: the ground floor whole, the upper floor open over the shaft, the court and the stairs' upper
            // flights, the roof open over the shaft. 4 × 4 m pieces where a bay is whole, 2 × 2 m round the holes.
            for (int level = 0; level <= Levels; level++)
            {
                var slabs = G(level == 0 ? "Ground slabs" : level == Levels ? "Roof slabs" : $"Level {level} slabs", level * FloorHeight);
                var holes = level == 0 ? new Rect2[0]
                    : level == Levels ? new[] { Shaft }
                    : new[] { Shaft, Void, StairHole, StairHole.Turned() };
                for (int i = 0; i < Cols; i++)
                    for (int j = 0; j < Rows; j++)
                    {
                        var bay = new Rect2(i * Cell, j * Cell, (i + 1) * Cell, (j + 1) * Cell);
                        if (!System.Array.Exists(holes, h => h.Overlaps(bay)))
                        {
                            P(slabs, level == 0 ? "Slab_4x4" : "Slab_4x4_LightLine", bay.X0, -bay.Y0);
                            continue;
                        }
                        for (float x = bay.X0; x < bay.X1; x += Half)
                            for (float y = bay.Y0; y < bay.Y1; y += Half)
                            {
                                var cell = new Rect2(x, y, x + Half, y + Half);
                                int hit = System.Array.FindIndex(holes, h => h.Overlaps(cell));
                                if (hit < 0) { P(slabs, "Slab_2x2", x, -y); continue; }
                                // A hole edge off the 2 m grid (the stair holes) leaves a strip of this cell: a scaled 2 x 2 slab.
                                foreach (var rest in Remainder(cell, holes[hit]))
                                    P(slabs, "Slab_2x2", rest.X0, -rest.Y0).transform.localScale =
                                        new Vector3((rest.X1 - rest.X0) / Half, 1f, (rest.Y1 - rest.Y0) / Half);
                            }
                    }
            }

            for (int level = 0; level < Levels; level++)
            {
                float floorY = level * FloorHeight;
                Shell(G($"Level {level} shell", floorY), level);
                var cores = G($"Level {level} cores", floorY);
                var wings = G($"Level {level} wings", floorY);
                foreach (bool turned in new[] { false, true })
                {
                    Core(cores, level, turned);
                    Wing(wings, level, turned);
                }
                // The lift lobby's south twin: a 4 m wall splitting the south window hall, so the long hall axis breaks
                // where the lobby's does (L2 baffle at x 20, y 27–32, snapped to the 4 m piece).
                Runs(wings, level, plan.once, false);
                Court(G($"Level {level} court", floorY), level);
                FitOut(G($"Level {level} fit-out", floorY), level);
            }

            Ceilings(G("Ceilings"));
            Markings(G("Markings"));
            Figures(root);
            Massing(root);
            DevMirror();
            HitDummy();
            Lift();
            // The rooftop range (the elevator session's KitOfficeRoof.cs): roof guard rails, the dummy pad, the
            // arsenal bay and the pool; it also moves the HitDummy up there and wires the lift if one is built.
            Debug.Log(KitOfficeRoof.AddTo(root));
            AddCamera();
            return count;

            // ---------------------------------------------------------------- the pieces of the plan

            // The perimeter: the window façade on the north (with the lift in it), glass curtain walls elsewhere unless
            // something hangs on or stands against a bay, which keeps it solid white.
            void Shell(Transform t, int level)
            {
                for (int i = 0; i < Cols; i++)
                {
                    float x = i * Cell;
                    // The lift core on the façade: solid 2 m flanks either side of the shaft's own glass wall (Lift()).
                    if (x == CourtX0) P(t, "Wall_Solid_2m", x, 0f);
                    else if (x == CourtX0 + Cell) P(t, "Wall_Solid_2m", ShaftX1, 0f);
                    else P(t, "Window_4m", x, 0f);
                    if (i > 0 && x != (ShaftX0 + ShaftX1) * 0.5f) P(t, "Column_Facade", x, 0f);   // none inside the shaft
                    P(t, OuterWall(level, 'S', i), x + Cell, -Depth, 180f);
                }
                P(t, "Column_CornerL", 0f, 0f);
                P(t, "Column_CornerR", Width, 0f);
                // The south corners too, each the opposite north column turned 180 degrees: without them the two curtain
                // walls overlapped there with doubled mullions (clipping sweep, 2026-11-15).
                P(t, "Column_CornerR", 0f, -Depth, 180f);
                P(t, "Column_CornerL", Width, -Depth, 180f);
                for (int j = 0; j < Rows; j++)
                {
                    float z = -j * Cell;
                    P(t, OuterWall(level, 'W', j), 0f, z - Cell, -90f);
                    P(t, OuterWall(level, 'E', j), Width, z, 90f);
                }
            }

            // One core (L2 "west core band", x 8–14, y 6–24): the enclosed stair E1, print and copy, the kitchen, a washroom
            // and a riser. Every room has two doors; only the riser is sealed. Turned, it is the east core with E2.
            // The walls of both the core and the wing come from l2_on_kit.json's "half" (see Wing for the wing's side).
            void Core(Transform t, int level, bool turned)
            {
                if (level == 0)
                {
                    // The return stair: up north on the west flight to a landing, back south on the east flight, arriving
                    // on the upper floor at E1's open south face. Yaw 180 turns the kit stair (rising −Z) to rise north.
                    Put(t, "Stair_Straight_Half_2m", 10.2f, 13.8f, 180f, turned);
                    var landing = Put(t, "Slab_4x4", 8.2f, 6.2f, 0f, turned);
                    landing.transform.localPosition += Vector3.up * LandingY;
                    // WHY scaled, not a new piece: a plain box slab stretched to the 5.6 × 4.2 m between E1's walls.
                    landing.transform.localScale = new Vector3(5.6f / Cell, 1f, 4.2f / Cell);
                    var upper = Put(t, "Stair_Straight_Half_2m", 11.8f, 10.4f, 0f, turned);
                    upper.transform.localPosition += Vector3.up * LandingY;
                    // Closes the low space under the landing and the upper flight; E1's way in is the west flight's foot.
                    Runs(t, level, plan.groundHalf, turned);
                }
                else Runs(t, level, plan.upperHalf, turned);   // glass guards round the upper flight's hole, on the slab side
            }

            // One office wing (x 0–4): the legal conference (L) and the back office (A), each with two doors, with storage
            // returns that break the long axes. Turned, it is the review conference (R) and the claims back office (C).
            void Wing(Transform t, int level, bool turned)
            {
                // Every wall of the half (core rooms, wing rooms, storage returns, the court baffle), as built on the
                // kit grid: l2_on_kit.json records each snap from the L2 drawings.
                Runs(t, level, plan.half, turned);
                // Wall graphics (KitSigns), each with one job (MARKINGS.md): whose firm this is (its wordmark lightbox at
                // reception), whose building (AONAGI's logo on E1's north face, at the ring), where am I (the zone
                // letters; H only on the ground floor, where the court is), what is this (a plate at every room door).
                bool a = !turned;
                string side = a ? "A" : "B", floor = level == 0 ? "1F" : "2F";
                var firmBox = Put(t, "Lightbox_Wall_Logo", 0.2f, 4.2f, 90f, turned, decorate: false);   // yaw 90 faces +X, runs south
                KitSigns.LightboxGraphic(firmBox, $"Lightbox_Firm_{side}");
                Put(t, "Lightbox_Wall_Logo", 9.2f, 6f, 0f, turned);          // faces north into the ring
                // Wall-washers on both lightboxes, from the soffit a little out from the wall, aimed down at the graphic.
                float soffitY = FloorHeight - 0.2f - PoolDrop;
                Vector2 firm = U(0.2f + WashOffset, 6f, turned), aonagi = U(11f, 6f - WashOffset, turned);
                Pool(t, new Vector3(firm.x, soffitY, firm.y), (Dir(-1f, 0f, turned) + Vector3.down * 1.5f).normalized,
                     WashIntensity, WashRange, WashAngle);
                Pool(t, new Vector3(aonagi.x, soffitY, aonagi.y), (Dir(0f, 1f, turned) + Vector3.down * 1.5f).normalized,
                     WashIntensity, WashRange, WashAngle);
                Sign(t, a ? "L" : "R", 0.4f, 8f, 0f, turned);                // on the conference's north wall, seen from reception
                Sign(t, a ? "A" : "C", 4f, 22.4f, 90f, turned);              // on the back office's east wall, facing the ring
                if (level == 0) Sign(t, "H", 14f, 10.4f, 90f, turned);       // E1's east face, toward the court
                foreach (var r in plan.half)
                {
                    float t0 = r.kind == "solid" ? 0.2f : 0.1f;   // kit WALL, PART_T
                    float ux = Mathf.Sign(r.b[0] - r.a[0]) * (r.b[0] != r.a[0] ? 1f : 0f);
                    float uy = Mathf.Sign(r.b[1] - r.a[1]) * (r.b[1] != r.a[1] ? 1f : 0f);
                    float rx = -uy, ry = ux;   // the thickness side: right of travel as the plan is drawn
                    foreach (var d in r.doors)
                    {
                        if (string.IsNullOrEmpty(d.room)) continue;
                        float fx = d.face[0], fy = d.face[1];
                        float off = fx * rx + fy * ry > 0f ? t0 : 0f;   // the face is the far side of the wall's thickness
                        bool alongX = ux != 0f;
                        float x = alongX ? d.at + PlateAlong : r.a[0], y = alongX ? r.a[1] : d.at + PlateAlong;
                        WallDecal(t, $"Plate_{d.room}_{side}_{floor}", x + fx * off, y + fy * off, PlateY, fx, fy, PlateSize, turned);
                    }
                }
                foreach (var e in plan.decals.evac)
                    WallDecal(t, $"Evac_{e.id}_{side}_{floor}", e.at[0], e.at[1], e.y, e.face[0], e.face[1], EvacSize, turned);
                // Stair floor numbers at room height: the ground floor's on E1's north wall above the landing (seen up the
                // first flight), the upper floor's on the print room's south wall where the second flight arrives.
                string stair = a ? "E1" : "E2";
                if (level == 0) WallDecal(t, $"FloorNo_{stair}_1F", 11f, 6.2f, 3.4f, 0f, 1f, FloorNoSize, turned);
                else WallDecal(t, $"FloorNo_{stair}_2F", 11f, 18f, 2.2f, 0f, -1f, FloorNoSize, turned);
            }

            void Sign(Transform t, string letter, float x, float y, float yaw, bool turned)
            {
                Vector2 u = U(x, y, turned);
                KitSigns.Place(t, letter, u.x, u.y, yaw + (turned ? 180f : 0f));
            }

            // The court: the feature flight rising west on the ground floor, glass guards round the void upstairs (the
            // north strip is the bridge, the south strip the canopy over the lower court).
            void Court(Transform t, int level)
            {
                if (level == 0)
                {
                    P(t, "Stair_Straight_2m", FeatureFootX, -FeatureY, 90f);
                    // The building's spine: one cobalt band along the flight's south stringer, foot to top, on its outer
                    // face (the kit stair's width runs −Z from its pivot, so that face is at z = −(y + 2)).
                    var along = new Vector3(StringerTop.x - StringerFoot.x, StringerTop.y - StringerFoot.y, 0f);
                    var mid = new Vector3((StringerTop.x + StringerFoot.x) * 0.5f, (StringerTop.y + StringerFoot.y) * 0.5f,
                                          -(FeatureY + Half) - DecalGap);
                    var up = Vector3.Cross(Vector3.forward, along).normalized;   // in the stringer's plane, across the band
                    KitSigns.Decal(t, "Band_Cobalt", mid, KitSigns.Facing(Vector3.back, up),
                                   new Vector2(along.magnitude, StringerBandHeight), KitSigns.Finish.Inlay);
                    // The case start's spot, from the roof soffit down the open court.
                    Pool(t, new Vector3(20f, RoofY - 0.2f - PoolDrop, -16f), Vector3.down, PlinthIntensity, PlinthRange, PlinthAngle);
                    return;
                }
                // The floor number where the feature flight arrives, on E1's east face straight ahead.
                WallDecal(t, "FloorNo_Feature_2F", 14f, 13f, 2f, 1f, 0f, FloorNoSize, false);
                Run(t, level, Kind.Guard, CourtX1, VoidY0, CourtX0, VoidY0, false, NoDoors);       // the bridge's edge
                Run(t, level, Kind.Guard, CourtX0, VoidY0 + Half, CourtX0, VoidY1, false, NoDoors); // west, from the flight's arrival
                Run(t, level, Kind.Guard, CourtX0, VoidY1, CourtX1, VoidY1, false, NoDoors);       // the canopy's edge
                Run(t, level, Kind.Guard, CourtX1, VoidY1, CourtX1, VoidY0, false, NoDoors);       // east
            }

            // Furniture and the office's daily life. The walls repeat on both storeys; what is in the rooms changes, so
            // each floor reads as its own place without teaching a different map (CURRENT_DIAGNOSIS.md).
            void FitOut(Transform t, int level)
            {
                if (level == 0)
                {
                    // The court's fixed low cover round the briefcase start (20, 16), point-symmetric.
                    P(t, "Counter_Capsule_2m", 17f, -17f, 90f);
                    P(t, "Counter_Capsule_2m", 23f, -15f, 90f);
                    P(t, "Planter_Box", 17f, -21f);
                    P(t, "Planter_Box", 22f, -11f);
                }
                foreach (bool turned in new[] { false, true })
                {
                    // Window hall desks (L2 benches), both floors.
                    // At x 9.6 and 12.6, clear of the L | H threshold strip at x 14 (l2_on_kit.json thresholds).
                    foreach (float x in new[] { 2f, 9.6f, 12.6f }) Bench(t, x, 2f, 0f, turned);
                    // Print and copy: the copier and storage against the kitchen wall.
                    Put(t, "Copier", 12.4f, 17.45f, 0f, turned);
                    Put(t, "Storage_Low", 9.8f, 17.7f, 0f, turned);
                    // Kitchen: the counter (L2 cover), the drinks machine and the water server against the print wall.
                    Put(t, "Counter_Capsule_2m", 11f, 20.2f, 0f, turned);
                    Put(t, "Water_Server", 13f, 18.6f, 180f, turned);
                    if (level == 0)
                    {
                        Put(t, "Vending_Machine", 9.2f, 18.2f + VendHalfDepth, 180f, turned);
                        // Reception: the desk in front of the firm's logo lightbox, the umbrella stand by the way in.
                        Put(t, "Desk_Reception", 2.4f, 6.4f, 0f, turned);
                        Put(t, "Umbrella_Stand", 3.6f, 4.6f, 0f, turned);
                        // The legal conference and the back office: a counter each (L2 cover) with pod chairs, storage.
                        Put(t, "Counter_Capsule_2m", 2f, 12f, 0f, turned);
                        Put(t, "Chair_Pod", 2f, 10.9f, 180f, turned);
                        Put(t, "Chair_Pod", 2f, 13.1f, 0f, turned);
                        Put(t, "Counter_Capsule_2m", 2f, 24f, 0f, turned);
                        Put(t, "Storage_Low", 2f, 27.4f, 0f, turned);
                        Put(t, "Planter_Box", 0.8f, 15.2f, 0f, turned);
                    }
                    else
                    {
                        // Upstairs the wing rooms are open-plan work rooms; reception is a lounge.
                        foreach (float y in new[] { 10.2f, 13.8f, 22.6f, 26.2f }) Bench(t, 2f, y, 90f, turned);
                        Put(t, "Desk_Sculpt", 2.4f, 6f, 0f, turned);
                        Put(t, "Chair_Pod", 2.4f, 5.2f, 180f, turned);   // north of the desk, facing it
                        Put(t, "Slipper_Cabinet", 9.2f, 18.2f + 0.175f, 180f, turned);
                    }
                }
            }

            // A workstation bench with its task chairs, loose monitors and keyboards, phones and trays. (x, y) plan
            // metres; yaw turns the bench, and its offsets turn with it.
            void Bench(Transform t, float x, float y, float yaw, bool turned)
            {
                Vector2 c = U(x, y, turned);
                float a = yaw + (turned ? 180f : 0f);
                Quaternion q = Quaternion.Euler(0f, a, 0f);
                void Item(string piece, float lx, float lz, float localYaw, bool onDesk)
                {
                    Vector3 o = q * new Vector3(lx, 0f, lz);
                    var go = P(t, piece, c.x + o.x, c.y + o.z, a + localYaw);
                    if (onDesk) go.transform.localPosition += Vector3.up * DeskTop;
                }
                Item("Workstation_Bench", 0f, 0f, 0f, false);
                foreach (float dx in new[] { -0.6f, 0.6f })
                {
                    Item("Chair_Task", dx, 1.05f, 180f, false);   // the +Z side's chairs face the desk, −Z
                    Item("Chair_Task", dx, -1.05f, 0f, false);    // the −Z side's face +Z
                }
                // Loose monitors and keyboards (their own pieces since 2026-11-13, so they can be thrown): each side's
                // screens face its own chairs.
                foreach (float side in new[] { 1f, -1f })
                    foreach (float dx in new[] { -0.6f, 0.6f })
                    {
                        float sy = side > 0f ? 0f : 180f;
                        Item("Monitor_Flat", dx, side * 0.3f, sy, true);
                        // WHY 0.57: the Aero CRT (0.36 deep, centred on its foot at ±0.3) reaches ±0.48, so a keyboard at ±0.57
                        // (0.14 deep) leaves a 2 cm gap and its front edge at ±0.64, on the 0.8 m-deep desk (props session).
                        Item("Keyboard", dx, side * 0.57f, sy, true);
                    }
                // A Japanese desk island's head end: a phone and a document tray on each side's end desk.
                Item("Phone_Desk", 0.98f, 0.42f, 180f, true);
                Item("Doc_Tray", -0.98f, 0.45f, 180f, true);
                Item("Phone_Desk", -0.98f, -0.42f, 0f, true);
                Item("Doc_Tray", 0.98f, -0.45f, 0f, true);
                // A loose PC tower per side on the floor at the bench's end, beside that side's pedestal, facing that
                // side's chairs (props session, 2026-11-15). WHY outside the end panel: under the desk the 0.19 m tower
                // either clips the end panel or takes the next chair's knee space. Skipped until PC_Tower is imported.
                if (AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/PC_Tower.prefab") != null)
                {
                    // The Aero bench (props_aero.py) has the +Z side's pedestal at x −0.9 and the −Z side's at +0.9.
                    Item("PC_Tower", -PcTowerX, PcTowerZ, 0f, false);
                    Item("PC_Tower", PcTowerX, -PcTowerZ, 180f, false);
                }
            }

            // Ceilings, per 4 m cell (MARKINGS.md kind 6: "the ceiling explains the floor"): a floating panel only over a
            // room or work zone (the carpeted cells), centred on the grid; a room cell a wall cuts gets the flush tile
            // grid; circulation keeps the bare soffit, whose slab light lines run unbroken wall to wall, with the ring's
            // cove (Markings) above the ring. Nothing over the shaft, nor on the ground floor under the void or a stair hole.
            void Ceilings(Transform t)
            {
                for (int level = 0; level < Levels; level++)
                {
                    float floorY = level * FloorHeight, soffit = floorY + FloorHeight - 0.2f;
                    for (int i = 0; i < Cols; i++)
                        for (int j = 0; j < Rows; j++)
                        {
                            var cell = new Rect2(i * Cell, j * Cell, (i + 1) * Cell, (j + 1) * Cell);
                            float x = cell.X0, z = -cell.Y0;
                            if (cell.Overlaps(Shaft)) continue;
                            if (level == 0 && (cell.Overlaps(Void) || cell.Overlaps(StairHole) || cell.Overlaps(StairHole.Turned()))) continue;
                            if (InAny(cell, Carpeted))
                            {
                                P(t, "Carpet_Overlay_4x4", x, z).transform.localPosition = new Vector3(x, floorY, z);
                                P(t, "Ceiling_Panel_4x4", x, z).transform.localPosition = new Vector3(x, soffit, z);
                                Pool(t, new Vector3(x + Cell * 0.5f, soffit - PoolDrop, z - Cell * 0.5f), Vector3.down,
                                     RoomPoolIntensity, RoomPoolRange, RoomPoolAngle);
                                continue;
                            }
                            if (!walls[level].Exists(w => Crosses(w, cell))) continue;   // circulation: the bare lit soffit
                            P(t, "Ceiling_Grid_4x4", x, z).transform.localPosition = new Vector3(x, soffit, z);
                        }
                }
            }

            // The floor and ceiling marks (MARKINGS.md), drawn by make_markings.py from layouts.json and l2_on_kit.json as
            // one sheet per storey: the floor's (ring, node pads, district bands and thresholds, tenancy line, plinth,
            // gates, edge strips) is a glossy inlay just above the slab; the soffit's district bands and the ring's light
            // cove hang just below each soffit. The old floating 16 m ribbons are retired: the ring band does their job.
            void Markings(Transform t)
            {
                var centreXZ = new Vector2(SheetSize * 0.5f, -SheetSize * 0.5f);
                for (int level = 0; level < Levels; level++)
                {
                    string floor = level == 0 ? "1F" : "2F";
                    float floorY = level * FloorHeight, soffit = floorY + FloorHeight - 0.2f;
                    var size = new Vector2(SheetSize, SheetSize);
                    // Floor sheet: face up, texture north at the top. Soffit sheets: face down, drawn south at the top
                    // (make_markings.py flips them) so east stays east seen from below.
                    KitSigns.Decal(t, $"Marks_Floor_{floor}", new Vector3(centreXZ.x, floorY + FloorMarkLift, centreXZ.y),
                                   KitSigns.Facing(Vector3.up, Vector3.forward), size, KitSigns.Finish.Inlay);
                    KitSigns.Decal(t, $"Marks_Ceiling_{floor}", new Vector3(centreXZ.x, soffit - SoffitMarkDrop, centreXZ.y),
                                   KitSigns.Facing(Vector3.down, Vector3.back), size, KitSigns.Finish.Inlay);
                    KitSigns.Decal(t, "Marks_Ceiling_Cove", new Vector3(centreXZ.x, soffit - CoveDrop, centreXZ.y),
                                   KitSigns.Facing(Vector3.down, Vector3.back), size, KitSigns.Finish.Light);
                }
            }
        }

        /// <summary>What is left of <paramref name="cell"/> outside <paramref name="hole"/>, when the hole spans the cell in
        /// one axis (a strip either side); empty when the hole covers it or cuts it in both axes.</summary>
        static List<Rect2> Remainder(Rect2 cell, Rect2 hole)
        {
            const float eps = 0.01f;
            var rest = new List<Rect2>();
            if (hole.Y0 <= cell.Y0 + eps && hole.Y1 >= cell.Y1 - eps)
            {
                if (hole.X0 > cell.X0 + eps) rest.Add(new Rect2(cell.X0, cell.Y0, hole.X0, cell.Y1));
                if (hole.X1 < cell.X1 - eps) rest.Add(new Rect2(hole.X1, cell.Y0, cell.X1, cell.Y1));
            }
            else if (hole.X0 <= cell.X0 + eps && hole.X1 >= cell.X1 - eps)
            {
                if (hole.Y0 > cell.Y0 + eps) rest.Add(new Rect2(cell.X0, cell.Y0, cell.X1, hole.Y0));
                if (hole.Y1 < cell.Y1 - eps) rest.Add(new Rect2(cell.X0, hole.Y1, cell.X1, cell.Y1));
            }
            return rest;
        }

        static bool InAny(Rect2 cell, Rect2[] rects)
        {
            foreach (var r in rects)
                if (r.Overlaps(cell) || r.Turned().Overlaps(cell)) return true;
            return false;
        }

        /// <summary>True if an axis-aligned wall (a zero-width rectangle) passes through the cell's inside, not just its edge.</summary>
        static bool Crosses(Rect2 wall, Rect2 cell)
        {
            const float eps = 0.01f;
            bool vertical = wall.X1 - wall.X0 < eps;
            return vertical
                ? wall.X0 > cell.X0 + eps && wall.X0 < cell.X1 - eps && wall.Y0 < cell.Y1 - eps && wall.Y1 > cell.Y0 + eps
                : wall.Y0 > cell.Y0 + eps && wall.Y0 < cell.Y1 - eps && wall.X0 < cell.X1 - eps && wall.X1 > cell.X0 + eps;
        }

        static Kind ParseKind(string kind) => kind switch
        {
            "solid" => Kind.Solid,
            "glass" => Kind.Glass,
            "frost" => Kind.Frost,
            "guard" => Kind.Guard,
            _ => throw new System.ArgumentException($"Unknown wall kind '{kind}' in {KitPlanPath}."),
        };

        static string PlainPiece(Kind kind, int metres) => kind switch
        {
            Kind.Solid => $"Wall_Solid_{metres}m",
            Kind.Glass => $"Partition_Glass_{metres}m",
            Kind.Frost => $"Partition_Frost_{metres}m",
            _ => $"Guard_Glass_{metres}m",
        };

        /// <summary>A 2 m door piece. Frosted walls take the glass door: the doorway is the clear part.</summary>
        static string DoorPiece(Kind kind) => kind switch
        {
            Kind.Solid => "Wall_Door_2m",
            Kind.Glass or Kind.Frost => "Partition_Glass_Door_2m",
            _ => throw new System.ArgumentException("A guard has no door."),
        };

        /// <summary>
        /// The lift: a shaft at plan x 18–22, y 0–4 on the north façade, landing frames at 1F and on the roof facing
        /// south into the lobby (L2: the lobby opens straight onto the court), glass shaft walls everywhere else on all
        /// three levels, the roof head-house's cap, and the car and four leaves, closed, under one "Lift" group.
        /// </summary>
        /// <remarks>
        /// Only geometry: KitOfficeRoof.AddTo (run after this) finds the "Lift" group by the child names below and
        /// adds the Elevator script, the car's kinematic body and the call spots. Leaves: A runs −X (yaw 180), B runs
        /// +X (yaw 0); each opens by sliding its own +X by the kit's LIFT_LEAF_W (0.75 m). The roof's guard rails
        /// belong to KitOfficeRoof, not here.
        /// </remarks>
        static void Lift()
        {
            var root = GameObject.Find("Kit Office").transform;
            var lift = new GameObject("Lift").transform;
            lift.SetParent(root, false);
            void Put(string piece, float x, float y, float z, float yaw, string name = null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{piece}.prefab");
                if (prefab == null) throw new FileNotFoundException($"No kit prefab {piece}; run Import Kit first.");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, lift);
                go.transform.SetLocalPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f));
                KitSigns.Decorate(go, piece);
                if (name != null) go.name = name;
            }
            float x0 = ShaftX0, x1 = ShaftX1, z0 = 0f, z1 = -ShaftY1;
            for (int level = 0; level <= Levels; level++)
            {
                float y = level * FloorHeight;
                // South: the landing at 1F and on the roof, glass on the upper floor (no stop there). Yaw 180 runs the
                // piece −X with its face toward −Z, the lobby; every wall's thickness lands inside the shaft.
                string south = level == 0 ? "Lift_Door_Frame_4m_1F" : level == Levels ? "Lift_Door_Frame_4m_RF" : "Lift_Shaft_Wall_4m";
                Put(south, x1, y, z1, 180f);
                Put("Lift_Shaft_Wall_4m", x0, y, z0, 0f);      // north: the façade
                Put("Lift_Shaft_Wall_4m", x1, y, z0, 90f);     // east
                Put("Lift_Shaft_Wall_4m", x0, y, z1, -90f);    // west
            }
            Put("Slab_4x4", x0, RoofY + FloorHeight, z0, 0f);   // the head-house cap, on the roof storey's walls
            Put("Lift_Car", CarCentre.x, 0f, CarCentre.y, 180f, "Lift_Car");   // open side toward the doors (−Z)
            foreach (var (stop, y) in new[] { ("1F", 0f), ("RF", RoofY) })
            {
                Put("Lift_Door_Panel", LeafEdge.x, y, LeafEdge.y, 180f, $"Door_{stop}_A");
                Put("Lift_Door_Panel", LeafEdge.x, y, LeafEdge.y, 0f, $"Door_{stop}_B");
            }
        }

        /// <summary>Places the hit-reaction session's HitDummy at its starting spot. Skipped if the prefab is gone.</summary>
        static void HitDummy()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitDummyPath);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(HitDummyPosition, Quaternion.Euler(0f, HitDummyYaw, 0f));
        }

        /// <summary>
        /// Puts the animation work's DevMirror back: a mirror wall along the upper floor's south wall, so a player can
        /// watch their own character. Skipped if the prefab is gone.
        /// </summary>
        static void DevMirror()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DevMirrorPath);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(DevMirrorPosition, Quaternion.Euler(0f, DevMirrorYaw, 0f));
            go.transform.localScale = DevMirrorScale;
        }

        /// <summary>
        /// Which outer wall a perimeter bay gets: glass curtain wall (AEROSET reference, Alex 2026-11-14: "glass
        /// everywhere, crossed by thin dense lines") unless something hangs on or stands against it, which keeps
        /// it solid white. side is 'S', 'W' or 'E'; bay is the column (S) or row (W, E) index.
        /// </summary>
        /// <remarks>Kept solid: the west bay behind firm A's reception lightbox (y 4–8) and its turned twin on the east
        /// (y 24–28), on both floors; the upper floor's south bays west of the lift baffle, where the DevMirror runs.</remarks>
        static string OuterWall(int level, char side, int bay)
        {
            bool solid = side switch
            {
                'S' => level == 1 && bay < 5,
                'W' => bay == 1,
                _ => bay == Rows - 2,
            };
            return solid ? "Wall_Solid_4m" : "Wall_Curtain_4m";
        }

        /// <summary>Dark-suited capsules at player height: the read the art bible asks for.</summary>
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

        /// <summary>Review shots at eye height, Unity metres: the L2 plan's routes and landmarks.</summary>
        static (string name, Vector3 eye, Vector3 target)[] Views()
        {
            return new[]
            {
                ("L1_window_hall", new Vector3(3f, Eye, -3.5f), new Vector3(30f, Eye, -3.5f)),
                ("L2_lobby_to_court", new Vector3(20f, Eye, -5f), new Vector3(20f, 1.4f, -20f)),
                ("L3_court_feature_stair", new Vector3(22.5f, Eye, -20.5f), new Vector3(17f, 3f, -12f)),
                ("L4_west_ring_ribbon", new Vector3(6f, Eye, -6.5f), new Vector3(6f, 1.2f, -26f)),
                ("L5_print_through_doors", new Vector3(15.5f, Eye, -17f), new Vector3(8.5f, 1.4f, -15f)),
                ("L6_legal_conference", new Vector3(5.5f, Eye, -8.5f), new Vector3(1f, 1.2f, -15f)),
                ("L7_upper_over_court", new Vector3(14.5f, FloorHeight + Eye, -21f), new Vector3(24f, 2f, -12f)),
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
