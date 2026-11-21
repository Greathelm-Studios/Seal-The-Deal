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
    /// <summary>Builds <c>KitOffice.unity</c>: the L2 twin-core court office floor from kit prefabs.</summary>
    public static class KitOfficeScene
    {
        const string ScenePath = "Assets/_Project/Scenes/KitOffice.unity";
        const string PrefabFolder = "Assets/_Project/Art/Kit/Prefabs";
        const string PaperWhitePath = "Assets/_Project/Art/Kit/Materials/Wall_PaperWhite.mat";
        const string NavyPath = "Assets/_Project/Art/Kit/Materials/Trim_Navy.mat";
        const string VolumeProfilePath = "Assets/_Project/Art/Look/OfficeLook_Volume.asset";
        const string DevMirrorPath = "Assets/_Project/Prefabs/Dev/DevMirror.prefab";
        static readonly Vector3 DevMirrorPosition = new Vector3(10f, 6.34f, -31.75f);
        const float DevMirrorYaw = 180f;
        static readonly Vector3 DevMirrorScale = new Vector3(19.2f, 3.45f, 1f);
        const string HitDummyPath = "Assets/_Project/Prefabs/Dev/HitDummy.prefab";
        static readonly Vector3 HitDummyPosition = new Vector3(24f, FloorHeight, -25f);
        const float HitDummyYaw = 180f;

        const int Cols = 10, Rows = 8;
        const float Cell = 4f;
        const float Half = 2f;
        const float Width = Cols * Cell, Depth = Rows * Cell;
        const float FloorHeight = 4.5f;
        const int Levels = 2;
        const float CourtX0 = 16f, CourtX1 = 24f, VoidY0 = 12f, VoidY1 = 20f;
        const float FeatureFootX = CourtX0 + 7.2f, FeatureY = VoidY0;
        const float ShaftX0 = 18f, ShaftX1 = 22f, ShaftY1 = 4f;
        static readonly Vector2 CarCentre = new Vector2(20f, -1.925f);
        static readonly Vector2 LeafEdge = new Vector2(20f, -3.775f);
        const float RoofY = Levels * FloorHeight;
        const float LandingY = FloorHeight * 0.5f;
        const float DeskTop = 0.74f;
        const float PcTowerX = 1.32f, PcTowerZ = 0.55f;
        const float VendHalfDepth = 0.325f;
        const float MassingHeight = 30f;
        const float FigureHeight = 1.8f, FigureRadius = 0.25f;
        static readonly Vector3[] Spots =
        {
            new Vector3(6f, 0f, -15f), new Vector3(20f, 0f, -16f), new Vector3(11f, 0f, -19f), new Vector3(1f, 0f, -14f),
            new Vector3(20f, FloorHeight, -11f), new Vector3(34f, FloorHeight, -20f),
        };
        static readonly Vector3 SpawnA = new Vector3(4f, 0.1f, -4f), SpawnB = new Vector3(36f, 0.1f, -28f);

        const float Eye = 1.6f;
        const int CaptureWidth = 1600, CaptureHeight = 900;
        const float CaptureFov = 60f;

        [MenuItem("Close the Deal/Art/Build Kit Office Scene")]
        static void BuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Build Kit Office Scene", "Stop Play Mode first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAndSave();
        }

        /// <summary>Builds KitOffice.unity with the player rig, office look, animation, bounce lights, probes, and FishNet IDs.</summary>
        public static void BuildAndSave()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Build(scene);
            GreyboxSceneSetup.EnsurePlayRig(SpawnA, SpawnB);
            EditorSceneManager.SaveScene(scene, ScenePath);
            OfficeLook.Apply(askFirst: false);
            // Animation runs after OfficeLook so it attaches its cookie to the look's sun.
            var office = GameObject.Find("Kit Office").transform;
            Debug.Log(KitOfficeAnimation.AddTo(office));
            Debug.Log(BounceLight.Add(askFirst: false));
            // Hide markings, animated elements, and CRT overlays during probe baking to avoid cyan/additive bleed.
            var marks = GameObject.Find("Kit Office/Markings");
            if (marks != null) marks.SetActive(false);
            var unbaked = new List<GameObject>();
            foreach (var t in office.GetComponentsInChildren<Transform>(false))
                if (t.name == "Animated" || t.name.Contains("CRT")) unbaked.Add(t.gameObject);
            foreach (var g in unbaked) g.SetActive(false);
            Debug.Log(FloorReflections.Bake(askFirst: false));
            // Zone probes rely on FloorReflections having flagged renderers as ReflectionProbeStatic.
            Debug.Log(ZoneReflections.Bake(askFirst: false));
            if (marks != null) marks.SetActive(true);
            foreach (var g in unbaked) g.SetActive(true);
            Debug.Log(GiveNetworkSceneIds(scene));
            EditorSceneManager.SaveScene(scene);
        }

        // Reflection into internal FishNet scene-ID helpers; throws MissingMethodException if FishNet renames them.
        static string GiveNetworkSceneIds(Scene scene)
        {
            const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
            var type = typeof(FishNet.Object.NetworkObject);
            // Parameter types disambiguate CreateSceneId overloads.
            var create = type.GetMethod("CreateSceneId", Flags, null,
                new[] { typeof(Scene), typeof(bool), typeof(int).MakeByRefType() }, null);
            var dedupe = type.GetMethod("RemoveDuplicateNetworkObjects", Flags, null, System.Type.EmptyTypes, null);
            var reserialize = type.GetMethod("ReserializeEditorSetValues", Flags, null, new[] { typeof(bool), typeof(bool) }, null);
            if (create == null || dedupe == null || reserialize == null)
                throw new System.MissingMethodException("FishNet's scene-ID methods have moved; run Tools > Fish-Networking > Utility > Reserialize NetworkObjects on KitOffice by hand and update KitOfficeScene.GiveNetworkSceneIds.");
            var args = new object[] { scene, true, 0 };
            var found = (List<FishNet.Object.NetworkObject>)create.Invoke(null, args);
            foreach (var nob in found)
            {
                dedupe.Invoke(nob, null);
                reserialize.Invoke(nob, new object[] { true, false });
                EditorUtility.SetDirty(nob);
            }
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

        [MenuItem("Close the Deal/Art/Make Open Scene Playable")]
        static void MakePlayableFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Make Open Scene Playable", "Stop Play Mode first.", "OK");
                return;
            }
            // Require a single loaded scene so EnsurePlayRig does not match another open scene's rig.
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

        /// <summary>Builds KitOffice.unity additively beside open scenes, captures review PNGs, and closes it.</summary>
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
                // Must run after initial SaveScene because FishNet scene IDs are seeded from the scene path.
                report.AppendLine(GiveNetworkSceneIds(scene));
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

        enum Kind { Solid, Glass, Frost, Fluted, Louvre, Guard }

        readonly struct Rect2
        {
            public readonly float X0, Y0, X1, Y1;
            public Rect2(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
            public bool Overlaps(Rect2 o) => X0 < o.X1 - 0.01f && o.X0 < X1 - 0.01f && Y0 < o.Y1 - 0.01f && o.Y0 < Y1 - 0.01f;
            public Rect2 Turned() => new Rect2(Width - X1, Depth - Y1, Width - X0, Depth - Y0);
        }

        static readonly Rect2 Shaft = new Rect2(ShaftX0, 0f, ShaftX1, ShaftY1);
        static readonly Rect2 Void = new Rect2(CourtX0, VoidY0, CourtX1, VoidY1);
        // Starts at x 11.8 rather than 12 so the slab clears the 2 m stair stringer.
        static readonly Rect2 StairHole = new Rect2(11.8f, 10f, 14f, 14f);
        static readonly Color PoolColour = new Color(1f, 0.965f, 0.925f);
        const float RoomPoolIntensity = 6f, RoomPoolRange = 6f, RoomPoolAngle = 100f, PoolDrop = 0.25f;
        const float WashIntensity = 5f, WashRange = 6f, WashAngle = 80f, WashOffset = 1.2f;
        const float PlinthIntensity = 14f, PlinthRange = 12f, PlinthAngle = 50f;
        static readonly Color HalfALight = new Color(0.52f, 0.62f, 1f), HalfBLight = new Color(0.5f, 0.84f, 1f);
        static readonly Color CourtLight = new Color(0.55f, 0.78f, 1f);
        const float FieldBottom = 0.08f, FieldTop = 4.25f, FieldGap = 0.004f;
        static readonly Vector2 ZoneNumberSize = new Vector2(2.6f, 2.6f);
        const float ZoneNumberY = 2.2f;
        static readonly float[] WaterfallY = { 25.6f, 23.8f };
        static readonly (float x, float z, float lift)[] PendantSpots =
        {
            (17.4f, -15.6f, 0f), (21.8f, -17.8f, 1f), (18.6f, -18.8f, 1f), (22.6f, -15.2f, 0f), (20.2f, -19.2f, 0.5f),
        };
        const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";
        const string KitPlanPath = "ArtSource/Kit/markings/l2_on_kit.json";
        const float FloorMarkLift = 0.012f;
        const float SoffitMarkDrop = 0.012f, CoveDrop = 0.014f;
        const float SheetSize = 40f;
        const float DecalGap = 0.006f;
        static readonly Vector2 PlateSize = new Vector2(0.32f, 0.14f);
        const float PlateY = 1.5f, PlateAlong = 0.8f;
        const float WallTextBand = 0.62f, WallTextY = 3.95f;
        static readonly Vector2 EvacSize = new Vector2(0.6f, 0.42f);
        static readonly Vector2 FloorNoSize = new Vector2(1.2f, 0.9f);
        const float StringerBandHeight = 0.12f;
        static readonly Vector2 StringerFoot = new Vector2(FeatureFootX, 0.05f), StringerTop = new Vector2(CourtX0, 4.35f);

        [System.Serializable] sealed class KitDoor { public float at; public string room; public float[] face; }
        [System.Serializable] sealed class KitRun { public string kind; public float[] a, b; public KitDoor[] doors; public float trimA, trimB; }
        static readonly float[] NoDoors = new float[0];
        [System.Serializable] sealed class KitEvac { public string id; public float[] at, face; public float y; }
        [System.Serializable] sealed class KitTextFace { public float[] a, b, face; public string ink; }
        [System.Serializable] sealed class KitWallText { public KitTextFace[] core, south; }
        [System.Serializable] sealed class KitDecals { public KitEvac[] evac; public KitWallText wallText; }
        [System.Serializable] sealed class KitPlan { public KitRun[] half, once, groundHalf, upperHalf; public KitDecals decals; }
        static readonly Rect2[] Carpeted =
        {
            new Rect2(0f, 0f, 4f, 4f), new Rect2(8f, 0f, 16f, 4f),
            new Rect2(0f, 8f, 4f, 16f), new Rect2(0f, 20f, 4f, 28f),
        };

        static int Build(Scene scene)
        {
            // BounceLight and FloorReflections discover the building via "Kit Office" and "Floor N" names.
            KitSigns.Refresh();
            var plan = JsonUtility.FromJson<KitPlan>(File.ReadAllText(KitPlanPath));
            var root = new GameObject("Kit Office").transform;
            int count = 0;
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
                if (decorate) KitSigns.Decorate(go, piece);
                return go;
            }
            Vector2 U(float x, float y, bool turned) => turned ? new Vector2(Width - x, -(Depth - y)) : new Vector2(x, -y);
            GameObject Put(Transform parent, string piece, float x, float y, float yaw, bool turned, bool decorate = true)
            {
                Vector2 u = U(x, y, turned);
                return P(parent, piece, u.x, u.y, yaw + (turned ? 180f : 0f), decorate);
            }
            void Pool(Transform t, Vector3 at, Vector3 dir, float intensity, float range, float angle, Color? colour = null)
            {
                var go = new GameObject("Light Pool");
                go.transform.SetParent(t, false);
                go.transform.SetLocalPositionAndRotation(at, Quaternion.LookRotation(dir));
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = colour ?? PoolColour;
                light.intensity = intensity;
                light.range = range;
                light.spotAngle = angle;
                light.innerSpotAngle = angle * 0.45f;
                light.shadows = LightShadows.None;
            }
            Vector3 Dir(float fx, float fy, bool turned) => turned ? new Vector3(-fx, 0f, fy) : new Vector3(fx, 0f, -fy);
            GameObject WallDecal(Transform t, string texture, float x, float y, float h, float fx, float fy, Vector2 size, bool turned,
                                 KitSigns.Finish finish = KitSigns.Finish.Printed, float gap = DecalGap)
            {
                Vector2 u = U(x + fx * gap, y + fy * gap, turned);
                return KitSigns.Decal(t, texture, new Vector3(u.x, h, u.y), KitSigns.Facing(Dir(fx, fy, turned), Vector3.up), size, finish);
            }
            void Field(Transform t, string paint, float x, float y, float len, float fx, float fy, bool turned, string number = null)
            {
                WallDecal(t, paint, x, y, (FieldBottom + FieldTop) * 0.5f, fx, fy, new Vector2(len, FieldTop - FieldBottom), turned,
                          KitSigns.Finish.Inlay, FieldGap);
                if (number != null)
                    WallDecal(t, $"Zone_Number_{number}", x, y, ZoneNumberY, fx, fy, ZoneNumberSize, turned, gap: DecalGap + 0.004f);
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
                    if (cut > 0f) go.transform.localScale = new Vector3((step - cut) / step, 1f, 1f);
                    s += step;
                }
                if (kind == Kind.Guard) return;
                var r = new Rect2(Mathf.Min(ax, bx), Mathf.Min(ay, by), Mathf.Max(ax, bx), Mathf.Max(ay, by));
                walls[level].Add(turned ? r.Turned() : r);
            }

            for (int level = 0; level < Levels; level++) G($"Floor {level}", level * FloorHeight);

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
            Debug.Log(KitOfficeRoof.AddTo(root));
            AddCamera();
            return count;

            void Shell(Transform t, int level)
            {
                for (int i = 0; i < Cols; i++)
                {
                    float x = i * Cell;
                    if (x == CourtX0) P(t, "Wall_Solid_2m", x, 0f);
                    else if (x == CourtX0 + Cell) P(t, "Wall_Solid_2m", ShaftX1, 0f);
                    else P(t, "Window_4m", x, 0f);
                    if (i > 0 && x != (ShaftX0 + ShaftX1) * 0.5f) P(t, "Column_Facade", x, 0f);
                    P(t, OuterWall(level, 'S', i), x + Cell, -Depth, 180f);
                }
                P(t, "Column_CornerL", 0f, 0f);
                P(t, "Column_CornerR", Width, 0f);
                P(t, "Column_CornerR", 0f, -Depth, 180f);
                P(t, "Column_CornerL", Width, -Depth, 180f);
                for (int j = 0; j < Rows; j++)
                {
                    float z = -j * Cell;
                    P(t, OuterWall(level, 'W', j), 0f, z - Cell, -90f);
                    P(t, OuterWall(level, 'E', j), Width, z, 90f);
                }
            }

            void Core(Transform t, int level, bool turned)
            {
                if (level == 0)
                {
                    Put(t, "Stair_Straight_Half_2m", 10.2f, 13.8f, 180f, turned);
                    var landing = Put(t, "Slab_4x4", 8.2f, 6.2f, 0f, turned);
                    landing.transform.localPosition += Vector3.up * LandingY;
                    landing.transform.localScale = new Vector3(5.6f / Cell, 1f, 4.2f / Cell);
                    var upper = Put(t, "Stair_Straight_Half_2m", 11.8f, 10.4f, 0f, turned);
                    upper.transform.localPosition += Vector3.up * LandingY;
                    Runs(t, level, plan.groundHalf, turned);
                }
                else Runs(t, level, plan.upperHalf, turned);
            }

            void Wing(Transform t, int level, bool turned)
            {
                Runs(t, level, plan.half, turned);
                bool a = !turned;
                string side = a ? "A" : "B", floor = level == 0 ? "1F" : "2F";
                var firmBox = Put(t, "Lightbox_Wall_Logo", 0.2f, 4.2f, 90f, turned, decorate: false);
                KitSigns.LightboxGraphic(firmBox, $"Lightbox_Firm_{side}");
                Put(t, "Lightbox_Wall_Logo", 9.2f, 6f, 0f, turned);
                float soffitY = FloorHeight - 0.2f - PoolDrop;
                Vector2 firm = U(0.2f + WashOffset, 6f, turned), aonagi = U(11f, 6f - WashOffset, turned);
                Color halfLight = a ? HalfALight : HalfBLight;
                Pool(t, new Vector3(firm.x, soffitY, firm.y), (Dir(-1f, 0f, turned) + Vector3.down * 1.5f).normalized,
                     WashIntensity, WashRange, WashAngle, halfLight);
                Pool(t, new Vector3(aonagi.x, soffitY, aonagi.y), (Dir(0f, 1f, turned) + Vector3.down * 1.5f).normalized,
                     WashIntensity, WashRange, WashAngle, halfLight);
                string paint = a ? "Paint_HalfA" : "Paint_HalfB";
                string zone = level == 0 ? (a ? "01" : "02") : (a ? "03" : "04");
                Field(t, paint, 11f, 6f, 6f, 0f, -1f, turned);
                Field(t, paint, 8f, 10f, 8f, -1f, 0f, turned, zone);
                if (level == 0) Field(t, "Paint_Court", 14f, 10f, 8f, 1f, 0f, turned);
                Sign(t, a ? "L" : "R", 0.4f, 8f, 0f, turned);
                Sign(t, a ? "A" : "C", 4f, 22.4f, 90f, turned);
                if (level == 0) Sign(t, "H", 14f, 10.4f, 90f, turned);
                foreach (var r in plan.half)
                {
                    float t0 = r.kind == "solid" ? 0.2f : 0.1f;
                    float ux = Mathf.Sign(r.b[0] - r.a[0]) * (r.b[0] != r.a[0] ? 1f : 0f);
                    float uy = Mathf.Sign(r.b[1] - r.a[1]) * (r.b[1] != r.a[1] ? 1f : 0f);
                    float rx = -uy, ry = ux;
                    foreach (var d in r.doors)
                    {
                        if (string.IsNullOrEmpty(d.room)) continue;
                        float fx = d.face[0], fy = d.face[1];
                        float off = fx * rx + fy * ry > 0f ? t0 : 0f;
                        bool alongX = ux != 0f;
                        float x = alongX ? d.at + PlateAlong : r.a[0], y = alongX ? r.a[1] : d.at + PlateAlong;
                        WallDecal(t, $"Plate_{d.room}_{side}_{floor}", x + fx * off, y + fy * off, PlateY, fx, fy, PlateSize, turned);
                    }
                }
                string textSet = (a ? "A" : "B") + (level + 1);
                foreach (var (run, faces) in new[] { ("core", plan.decals.wallText.core), ("south", plan.decals.wallText.south) })
                    for (int k = 0; k < faces.Length; k++)
                    {
                        var f = faces[k];
                        float len = Mathf.Abs(f.b[0] - f.a[0]) + Mathf.Abs(f.b[1] - f.a[1]);
                        WallDecal(t, $"WallText_{textSet}_{run}_{k}", (f.a[0] + f.b[0]) * 0.5f, (f.a[1] + f.b[1]) * 0.5f, WallTextY,
                                  f.face[0], f.face[1], new Vector2(len, WallTextBand), turned, gap: DecalGap + 0.004f);
                    }
                foreach (var e in plan.decals.evac)
                    WallDecal(t, $"Evac_{e.id}_{side}_{floor}", e.at[0], e.at[1], e.y, e.face[0], e.face[1], EvacSize, turned);
                string stair = a ? "E1" : "E2";
                if (level == 0) WallDecal(t, $"FloorNo_{stair}_1F", 11f, 6.2f, 3.4f, 0f, 1f, FloorNoSize, turned);
                else WallDecal(t, $"FloorNo_{stair}_2F", 11f, 18f, 2.2f, 0f, -1f, FloorNoSize, turned);
            }

            void Sign(Transform t, string letter, float x, float y, float yaw, bool turned)
            {
                Vector2 u = U(x, y, turned);
                KitSigns.Place(t, letter, u.x, u.y, yaw + (turned ? 180f : 0f));
            }

            void Court(Transform t, int level)
            {
                if (level == 0)
                {
                    P(t, "Stair_Straight_2m", FeatureFootX, -FeatureY, 90f);
                    var along = new Vector3(StringerTop.x - StringerFoot.x, StringerTop.y - StringerFoot.y, 0f);
                    var mid = new Vector3((StringerTop.x + StringerFoot.x) * 0.5f, (StringerTop.y + StringerFoot.y) * 0.5f,
                                          -(FeatureY + Half) - DecalGap);
                    var up = Vector3.Cross(Vector3.forward, along).normalized;
                    KitSigns.Decal(t, "Glow_Blue", mid, KitSigns.Facing(Vector3.back, up),
                                   new Vector2(along.magnitude, StringerBandHeight), KitSigns.Finish.Light);
                    var north = new Vector3(mid.x, mid.y, -FeatureY + DecalGap);
                    KitSigns.Decal(t, "Glow_Blue", north, KitSigns.Facing(Vector3.forward, Vector3.Cross(Vector3.back, along).normalized),
                                   new Vector2(along.magnitude, StringerBandHeight), KitSigns.Finish.Light);
                    foreach (var (px, pz, lift) in PendantSpots)
                        P(t, "Pendant_Sphere", px, pz).transform.localPosition = new Vector3(px, RoofY - 0.2f + lift, pz);
                    Pool(t, new Vector3(20f, RoofY - 0.2f - PoolDrop, -16f), Vector3.down, PlinthIntensity, PlinthRange, PlinthAngle, CourtLight);
                    foreach (float wy in WaterfallY) P(t, "Water_Wall", CourtX1 + Half, -wy, -90f);
                    Pool(t, new Vector3(19.6f, RoofY - 0.2f - PoolDrop, -13f), Vector3.down, PlinthIntensity, PlinthRange, PlinthAngle, CourtLight);
                    Pool(t, new Vector3(24.6f, RoofY - 0.2f - PoolDrop, -23f), (Vector3.down * 3f + Vector3.right).normalized,
                         PlinthIntensity, PlinthRange, PlinthAngle, CourtLight);
                    return;
                }
                WallDecal(t, "FloorNo_Feature_2F", 14f, 13f, 2f, 1f, 0f, FloorNoSize, false);
                Run(t, level, Kind.Guard, CourtX1, VoidY0, CourtX0, VoidY0, false, NoDoors);
                Run(t, level, Kind.Guard, CourtX0, VoidY0 + Half, CourtX0, VoidY1, false, NoDoors);
                Run(t, level, Kind.Guard, CourtX0, VoidY1, CourtX1, VoidY1, false, NoDoors);
                Run(t, level, Kind.Guard, CourtX1, VoidY1, CourtX1, VoidY0, false, NoDoors);
            }

            void FitOut(Transform t, int level)
            {
                if (level == 0)
                {
                    P(t, "Counter_Capsule_2m", 17f, -17f, 90f);
                    P(t, "Counter_Capsule_2m", 23f, -15f, 90f);
                    P(t, "Planter_Box", 17f, -21f);
                    P(t, "Planter_Box", 22f, -11f);
                }
                foreach (bool turned in new[] { false, true })
                {
                    foreach (float x in new[] { 2f, 9.6f, 12.6f }) Bench(t, x, 2f, 0f, turned);
                    Put(t, "Copier", 12.4f, 17.64f, 0f, turned);
                    Put(t, "Storage_Low", 9.8f, 17.74f, 0f, turned);
                    Put(t, "Counter_Capsule_2m", 11f, 20.2f, 0f, turned);
                    Vector2 k = U(11f, 20f, turned);
                    Pool(t, new Vector3(k.x, FloorHeight - 0.2f - PoolDrop, k.y), Vector3.down, RoomPoolIntensity, RoomPoolRange,
                         RoomPoolAngle, turned ? HalfBLight : HalfALight);
                    Put(t, "Water_Server", 13f, 18.41f, 180f, turned);
                    if (level == 0)
                    {
                        Put(t, "Vending_Machine", 9.2f, 18.2f + VendHalfDepth, 180f, turned);
                        Put(t, "Desk_Reception", 2.4f, 6.4f, 0f, turned);
                        Put(t, "Umbrella_Stand", 0.48f, 4.6f, 0f, turned);
                        Put(t, "Counter_Capsule_2m", 2f, 12f, 0f, turned);
                        Put(t, "Chair_Pod", 2f, 10.9f, 180f, turned);
                        Put(t, "Chair_Pod", 2f, 13.1f, 0f, turned);
                        Put(t, "Counter_Capsule_2m", 2f, 24f, 0f, turned);
                        Put(t, "Storage_Low", 2f, 27.65f, 0f, turned);
                        Put(t, "Planter_Box", 0.68f, 15.2f, 0f, turned);
                    }
                    else
                    {
                        foreach (float y in new[] { 10.2f, 13.8f, 22.2f, 25.8f }) Bench(t, 2f, y, 90f, turned);
                        Put(t, "Desk_Sculpt", 2.4f, 6f, 0f, turned);
                        Put(t, "Chair_Pod", 2.4f, 5.2f, 180f, turned);
                        Put(t, "Slipper_Cabinet", 9.2f, 18.2f + 0.175f, 180f, turned);
                    }
                }
            }

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
                    Item("Chair_Task", dx, 1.05f, 180f, false);
                    Item("Chair_Task", dx, -1.05f, 0f, false);
                }
                foreach (float side in new[] { 1f, -1f })
                    foreach (float dx in new[] { -0.6f, 0.6f })
                    {
                        float sy = side > 0f ? 0f : 180f;
                        Item("Monitor_Flat", dx, side * 0.3f, sy, true);
                        Item("Keyboard", dx, side * 0.57f, sy, true);
                    }
                Item("Phone_Desk", 0.98f, 0.42f, 180f, true);
                Item("Doc_Tray", -0.98f, 0.45f, 180f, true);
                Item("Phone_Desk", -0.98f, -0.42f, 0f, true);
                Item("Doc_Tray", 0.98f, -0.45f, 0f, true);
                if (AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/PC_Tower.prefab") != null)
                {
                    Item("PC_Tower", -PcTowerX, PcTowerZ, 0f, false);
                    Item("PC_Tower", PcTowerX, -PcTowerZ, 180f, false);
                }
            }

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
                                     RoomPoolIntensity, RoomPoolRange, RoomPoolAngle, x < Width * 0.5f ? HalfALight : HalfBLight);
                                continue;
                            }
                            if (!walls[level].Exists(w => Crosses(w, cell))) continue;
                            P(t, "Ceiling_Grid_4x4", x, z).transform.localPosition = new Vector3(x, soffit, z);
                        }
                }
            }

            void Markings(Transform t)
            {
                var centreXZ = new Vector2(SheetSize * 0.5f, -SheetSize * 0.5f);
                for (int level = 0; level < Levels; level++)
                {
                    string floor = level == 0 ? "1F" : "2F";
                    float floorY = level * FloorHeight, soffit = floorY + FloorHeight - 0.2f;
                    var size = new Vector2(SheetSize, SheetSize);
                    KitSigns.Decal(t, $"Marks_Floor_{floor}", new Vector3(centreXZ.x, floorY + FloorMarkLift, centreXZ.y),
                                   KitSigns.Facing(Vector3.up, Vector3.forward), size, KitSigns.Finish.Inlay);
                    KitSigns.Decal(t, $"Marks_Ceiling_{floor}", new Vector3(centreXZ.x, soffit - SoffitMarkDrop, centreXZ.y),
                                   KitSigns.Facing(Vector3.down, Vector3.back), size, KitSigns.Finish.Inlay);
                    KitSigns.Decal(t, "Marks_Ceiling_Cove", new Vector3(centreXZ.x, soffit - CoveDrop, centreXZ.y),
                                   KitSigns.Facing(Vector3.down, Vector3.back), size, KitSigns.Finish.Light);
                }
            }
        }

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
            "fluted" => Kind.Fluted,
            "louvre" => Kind.Louvre,
            "guard" => Kind.Guard,
            _ => throw new System.ArgumentException($"Unknown wall kind '{kind}' in {KitPlanPath}."),
        };

        static string PlainPiece(Kind kind, int metres) => kind switch
        {
            Kind.Solid => $"Wall_Solid_{metres}m",
            Kind.Glass => $"Partition_Glass_{metres}m",
            Kind.Frost => $"Partition_Frost_{metres}m",
            Kind.Fluted => $"Partition_Fluted_{metres}m",
            Kind.Louvre => $"Partition_Louvre_{metres}m",
            _ => $"Guard_Glass_{metres}m",
        };

        static string DoorPiece(Kind kind) => kind switch
        {
            Kind.Solid => "Wall_Door_2m",
            Kind.Glass or Kind.Frost or Kind.Fluted or Kind.Louvre => "Partition_Glass_Door_2m",
            _ => throw new System.ArgumentException("A guard has no door."),
        };

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
                string south = level == 0 ? "Lift_Door_Frame_4m_1F" : level == Levels ? "Lift_Door_Frame_4m_RF" : "Lift_Shaft_Wall_4m";
                Put(south, x1, y, z1, 180f);
                string side = level == 1 ? "Lift_Shaft_Wall_4m" : "Lift_Shaft_Wall_4m_Solid";
                Put(side, x0, y, z0, 0f);
                Put(side, x1, y, z0, 90f);
                Put(side, x0, y, z1, -90f);
            }
            Put("Lift_Cap_4x4", x0, RoofY + FloorHeight, z0, 0f);
            Put("Lift_Car", CarCentre.x, 0f, CarCentre.y, 180f, "Lift_Car");
            foreach (var (stop, y) in new[] { ("1F", 0f), ("RF", RoofY) })
            {
                Put("Lift_Door_Panel", LeafEdge.x, y, LeafEdge.y, 180f, $"Door_{stop}_A");
                Put("Lift_Door_Panel", LeafEdge.x, y, LeafEdge.y, 0f, $"Door_{stop}_B");
            }
        }

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
            go.transform.localPosition = new Vector3(at.x, at.y + FigureHeight * 0.5f, at.z);
            go.transform.localScale = new Vector3(FigureRadius * 2f, FigureHeight * 0.5f, FigureRadius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = navy;
        }

        static void Massing(Transform root)
        {
            var white = AssetDatabase.LoadAssetAtPath<Material>(PaperWhitePath);
            const float slab = 0.2f, gapUnderSlab = 0.01f;
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

            public static LookSnapshot From(Scene scene) => new LookSnapshot
            {
                _skybox = RenderSettings.skybox, _ambientMode = RenderSettings.ambientMode,
                _sky = RenderSettings.ambientSkyColor, _equator = RenderSettings.ambientEquatorColor,
                _ground = RenderSettings.ambientGroundColor, _fog = RenderSettings.fog, _fogMode = RenderSettings.fogMode,
                _fogColour = RenderSettings.fogColor, _fogStart = RenderSettings.fogStartDistance,
                _fogEnd = RenderSettings.fogEndDistance, _reflection = RenderSettings.defaultReflectionMode,
                _sun = RenderSettings.sun,
            };

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

        static void Capture(Scene scene, string folder, StringBuilder report)
        {
            Directory.CreateDirectory(folder);
            var go = new GameObject("Capture Camera");
            SceneManager.MoveGameObjectToScene(go, scene);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = CaptureFov;
            // Filter camera to the additive scene so other open scenes do not appear.
            cam.scene = scene;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24);
            var image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            try
            {
                // Discard one warm-up render so newly built objects render correctly on captured frames.
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
                Object.DestroyImmediate(go);
            }
        }
    }
}
