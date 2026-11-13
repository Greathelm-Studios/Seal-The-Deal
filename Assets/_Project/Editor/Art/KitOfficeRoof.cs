// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "make the dummy and arsenal section be on the ceiling. to access it, you
//              should [take] an elevator"; "open rooftop"; "what could be funny is if added a pool";
//              "tell the elevator chat to start building the roof"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Dresses KitOffice's roof as the open-air test range and wires the lift's script. Its own
//              file, not a method in KitOfficeScene, because another session holds that file while it
//              restyles the kit; KitOfficeScene.Build calls AddTo once so a rebuild keeps the roof.
// -----------------------------------------------------------------------------
using System.Text;
using CloseTheDeal.Lift;
using CloseTheDeal.Player;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>
    /// Builds the rooftop range on top of the kit office: guard rails, the hit dummy's pad with its glass
    /// screens and mirror, the reserved arsenal bay, the pool, loose props, and the lift's script.
    /// Menu: <c>Close the Deal &gt; Art &gt; Add Kit Office Roof Range</c>.
    /// </summary>
    /// <remarks>
    /// <para>The roof has nothing overhead: Alex asked for an open roof so the sky box shows.</para>
    /// <para>Everything it makes sits under one "Roof range" group, which it deletes and rebuilds; it touches
    /// nothing else in the scene except the HitDummy instance, which it moves (agreed with the hit-reaction
    /// session: nothing reads the dummy's position). Kit pieces that are not modelled yet (the pool, the lift)
    /// are skipped with a line in the report, so the roof can be built before they exist.</para>
    /// <para>Layout and the reasons for each distance: docs/systems/ELEVATOR.md.</para>
    /// </remarks>
    /// <example><code>Debug.Log(KitOfficeRoof.AddTo(GameObject.Find("Kit Office").transform));</code></example>
    public static class KitOfficeRoof
    {
        const string KitFolder = "Assets/_Project/Art/Kit/Prefabs";
        const string PropFolder = "Assets/_Project/Prefabs/Props";
        const string DevMirrorPath = "Assets/_Project/Prefabs/Dev/DevMirror.prefab";
        const string HitDummyPath = "Assets/_Project/Prefabs/Dev/HitDummy.prefab";
        const string OfficeRootName = "Kit Office";
        const string GroupName = "Roof range";
        const string LiftGroupName = "Lift";
        const string PlayerLayerName = "Player";

        // ---- The plate, as KitOfficeScene lays it out (metres; x east from 0, z south from 0).
        /// <summary>Bay size of the kit grid (ArtSource/Kit/README.md).</summary>
        const float Cell = 4f;
        /// <summary>Floor plate, as KitOfficeScene: 8 × 6 cells.</summary>
        const int Cols = 8, Rows = 6;
        const float Width = Cols * Cell, Depth = Rows * Cell;
        /// <summary>Top of the roof slab: two storeys of 4.5 m (KitOfficeScene.FloorHeight × Levels).</summary>
        const float RoofHeight = 9f;

        // ---- Dummy pad: the roof's south-east corner.
        /// <summary>
        /// Where the dummy stands, roof-local metres. 5 m from the east and south screens, so it has the 4 m of
        /// clear floor and the walls within 6 m that the hit-reaction session asked for (a blasted body must not
        /// clear a 1.1 m rail and drop off the roof).
        /// </summary>
        static readonly Vector3 DummySpot = new Vector3(27f, 0f, -19f);
        /// <summary>The dummy faces west (-X), toward the arsenal bay's firing line.</summary>
        const float DummyYaw = -90f;
        /// <summary>The pad's full-height screens replace the rails from here to the corner: east edge south of
        /// this z, south edge east of this x.</summary>
        const float PadNorthZ = -12f, PadWestX = 20f;
        /// <summary>Mirror centre above the roof, metres: as the office's DevMirror stands above its floor (6.34 − 4.5).</summary>
        const float MirrorCentreHeight = 1.84f;
        /// <summary>Mirror size, metres: the office mirror's height, 8 m long behind the dummy.</summary>
        static readonly Vector3 MirrorScale = new Vector3(8f, 3.45f, 1f);
        /// <summary>The mirror stands this far inside the east screen, metres, clear of its 0.1 m glass.</summary>
        const float MirrorInset = 0.3f;
        /// <summary>The mirror's visible face looks west, as the office's does.</summary>
        const float MirrorYaw = 90f;

        // ---- Arsenal bay: reserved for the weapons session.
        /// <summary>Bay's north-west corner, roof-local metres. 8 × 6 m, its east edge 12 m from the dummy.</summary>
        static readonly Vector3 BayCorner = new Vector3(7f, 0f, -16f);
        /// <summary>Bay size, metres, as the weapons session asked.</summary>
        const float BayWidth = 8f, BayDepth = 6f;

        // ---- Pool: north-west of the roof.
        /// <summary>Pool piece pivot (its front-left corner), roof-local metres: an 8 × 4 m pool clear of everything else.</summary>
        static readonly Vector3 PoolCorner = new Vector3(6f, 0f, -6f);
        const float PoolWidth = 8f, PoolDepth = 4f;
        /// <summary>Loungers and planters stand this far south of the pool's rim, metres.</summary>
        const float PoolsideGap = 1.2f;

        // ---- Loose props: things to throw at the dummy.
        /// <summary>
        /// Loose props on the roof: (prefab, x, z) roof-local metres, on a line 6 to 8 m west of the dummy.
        /// 8 props, half the 16-per-floor cap in docs/systems/PROPS.md.
        /// </summary>
        static readonly (string prefab, float x, float z)[] Props =
        {
            ("Monitor", 20.5f, -16.5f), ("Monitor", 20.5f, -18f), ("Monitor", 20.5f, -19.5f),
            ("Chair", 19f, -17f), ("Chair", 19f, -19f), ("Chair", 19f, -21f),
            ("Cabinet", 21f, -21.5f), ("Cabinet", 21f, -14.5f),
        };
        /// <summary>Props are placed this far above the roof, metres, and settle: their pivots are not all at the base.</summary>
        const float PropDrop = 0.05f;

        // ---- Lift wiring.
        /// <summary>Names the kit owner gives the lift's moving parts under the "Lift" group.</summary>
        const string CarName = "Lift_Car";
        static readonly string[] DoorNames = { "Door_1F_A", "Door_1F_B", "Door_RF_A", "Door_RF_B" };
        /// <summary>Each leaf slides its own width along its local +X to open, metres (Lift_Door_Panel is 0.75 wide,
        /// per the kit owner, 2026-11-13: the leaves overlap the 1.4 m opening by 0.05 m at each jamb).</summary>
        const float DoorSlide = 0.75f;
        /// <summary>Where a player waits to call the car: 1 m west of the doors (x 20), centred on them (z −6).</summary>
        static readonly Vector3 CallSpot = new Vector3(19f, 0f, -6f);
        /// <summary>The rider check is this much smaller than the car all round, metres, so someone outside the doors does not count.</summary>
        const float CabinMargin = 0.35f;

        [MenuItem("Close the Deal/Art/Add Kit Office Roof Range")]
        static void AddFromMenu()
        {
            GameObject office = GameObject.Find(OfficeRootName);
            if (office == null)
            {
                EditorUtility.DisplayDialog("Add Kit Office Roof Range", "Open KitOffice.unity first: no 'Kit Office' object in the open scenes.", "OK");
                return;
            }
            // AGENTS.md §4: a tool that rebuilds scene objects asks first.
            if (!EditorUtility.DisplayDialog("Add Kit Office Roof Range", "This rebuilds the 'Roof range' group and moves the HitDummy onto the roof. The scene is not saved.", "Build", "Cancel"))
                return;
            Debug.Log(AddTo(office.transform));
            EditorSceneManager.MarkSceneDirty(office.scene);
        }

        /// <summary>
        /// Rebuilds the "Roof range" group under <paramref name="office"/> (the "Kit Office" root) and wires the
        /// lift if its pieces are in the scene. Does not ask and does not save.
        /// </summary>
        /// <returns>A short report: what was placed and what was skipped because its kit piece is missing.</returns>
        public static string AddTo(Transform office)
        {
            var report = new StringBuilder("[Roof] ");
            Transform old = office.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var roof = new GameObject(GroupName).transform;
            roof.SetParent(office, false);
            roof.localPosition = new Vector3(0f, RoofHeight, 0f);

            int placed = Edges(roof);
            placed += ArsenalBay(roof);
            placed += Pool(roof, report);
            placed += LooseProps(roof, report);
            Dummy(roof, report);
            report.Append(placed).Append(" pieces on the roof. ");
            WireLift(office, report);
            return report.ToString();
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>Glass rails round the whole roof edge; full-height glass screens on the dummy pad's two outer sides.</summary>
        static int Edges(Transform roof)
        {
            var edges = Group(roof, "Edges");
            int count = 0;
            // Same placement rule as KitOfficeScene's shell: run +X from the pivot, thickness toward the inside.
            for (int i = 0; i < Cols; i++)
            {
                Kit(edges, "Guard_Rail_4m", i * Cell, 0f, 0f);
                bool pad = i * Cell >= PadWestX;
                Kit(edges, pad ? "Partition_Header_4m" : "Guard_Rail_4m", (i + 1) * Cell, -Depth, 180f);
                count += 2;
            }
            for (int j = 0; j < Rows; j++)
            {
                float z = -j * Cell;
                Kit(edges, "Guard_Rail_4m", 0f, z - Cell, -90f);
                bool pad = z <= PadNorthZ;
                Kit(edges, pad ? "Partition_Header_4m" : "Guard_Rail_4m", Width, z, 90f);
                count += 2;
            }
            return count;
        }

        /// <summary>The weapons session's reserved bay: a rack wall along its south side and an empty hook to place into.</summary>
        static int ArsenalBay(Transform roof)
        {
            // WHY empty: no weapon pickups exist yet. The weapons session parents its racks to "ArsenalBay",
            // whose origin is the bay's north-west corner, +X along the rack wall, firing line on its east edge.
            var bay = Group(roof, "ArsenalBay");
            bay.localPosition = BayCorner;
            var wall = Group(roof, "Arsenal rack wall");
            int count = 0;
            for (float x = 0f; x < BayWidth; x += Cell, count++)
                Kit(wall, "Partition_Header_4m", BayCorner.x + x, BayCorner.z - BayDepth, 0f);
            return count;
        }

        static int Pool(Transform roof, StringBuilder report)
        {
            var pool = Group(roof, "Pool");
            int count = 0;
            if (HasKit("Pool_Raised_8x4"))
            {
                Kit(pool, "Pool_Raised_8x4", PoolCorner.x, PoolCorner.z, 0f);
                count++;
            }
            else report.Append("Pool skipped: kit piece Pool_Raised_8x4 is not modelled yet. ");

            float side = PoolCorner.z - PoolDepth - PoolsideGap;
            Kit(pool, "Planter_Box", PoolCorner.x, side, 0f);
            Kit(pool, "Planter_Box", PoolCorner.x + PoolWidth, side, 0f);
            // Pod chairs as loungers, turned to face the water (+Z).
            Kit(pool, "Chair_Pod", PoolCorner.x + PoolWidth / 3f, side, 0f);
            Kit(pool, "Chair_Pod", PoolCorner.x + PoolWidth * 2f / 3f, side, 0f);
            return count + 4;
        }

        /// <summary>Host-owned props as scene objects: KitOffice has no tower, so there is no PropSpawner to use markers.</summary>
        static int LooseProps(Transform roof, StringBuilder report)
        {
            var props = Group(roof, "Loose props");
            int count = 0;
            foreach (var (name, x, z) in Props)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropFolder}/{name}.prefab");
                if (prefab == null)
                {
                    report.Append("Prop ").Append(name).Append(" missing, skipped. ");
                    continue;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, props);
                go.transform.localPosition = new Vector3(x, PropDrop, z);
                count++;
            }
            return count;
        }

        /// <summary>Moves the scene's HitDummy onto the pad (or places one) and stands a second mirror behind it.</summary>
        static void Dummy(Transform roof, StringBuilder report)
        {
            Vector3 spot = roof.TransformPoint(DummySpot);
            HitDummy dummy = Object.FindAnyObjectByType<HitDummy>();
            GameObject go = dummy != null ? dummy.gameObject : null;
            if (go == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitDummyPath);
                if (prefab != null) go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            if (go != null) go.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, DummyYaw, 0f));
            else report.Append("No HitDummy prefab: dummy not placed. ");

            var mirrorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DevMirrorPath);
            if (mirrorPrefab == null) return;
            var mirror = (GameObject)PrefabUtility.InstantiatePrefab(mirrorPrefab, roof);
            mirror.name = "DevMirror (roof)";
            mirror.transform.localPosition = new Vector3(Width - MirrorInset, MirrorCentreHeight, DummySpot.z);
            mirror.transform.localRotation = Quaternion.Euler(0f, MirrorYaw, 0f);
            mirror.transform.localScale = MirrorScale;
        }

        // ------------------------------------------------------------------ lift

        /// <summary>Adds the Elevator script, its network identity, the car's body and the call spots to the kit's "Lift" group.</summary>
        static void WireLift(Transform office, StringBuilder report)
        {
            Transform lift = office.Find(LiftGroupName);
            Transform car = lift != null ? lift.Find(CarName) : null;
            if (car == null)
            {
                report.Append("Lift not wired: no '").Append(LiftGroupName).Append('/').Append(CarName).Append("' in the scene yet.");
                return;
            }
            var doors = new Transform[DoorNames.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                doors[i] = lift.Find(DoorNames[i]);
                if (doors[i] == null)
                {
                    report.Append("Lift not wired: door '").Append(DoorNames[i]).Append("' is missing.");
                    return;
                }
            }

            // §4: fill only what is empty or what this tool just created.
            if (!car.TryGetComponent(out Rigidbody body)) body = car.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            if (!lift.TryGetComponent(out NetworkObject _)) lift.gameObject.AddComponent<NetworkObject>();
            if (!lift.TryGetComponent(out Elevator elevator)) elevator = lift.gameObject.AddComponent<Elevator>();

            var calls = new Transform[2];
            for (int stop = 0; stop < calls.Length; stop++)
            {
                string name = stop == 0 ? "Call spot 1F" : "Call spot RF";
                Transform call = lift.Find(name);
                if (call == null)
                {
                    call = new GameObject(name).transform;
                    call.SetParent(lift, false);
                }
                call.position = office.TransformPoint(CallSpot + Vector3.up * (stop * RoofHeight));
                calls[stop] = call;
            }

            // The rider check: the car's own bounds, shrunk, in the car's local space.
            var bounds = new Bounds(car.position, Vector3.zero);
            foreach (Collider c in car.GetComponentsInChildren<Collider>()) bounds.Encapsulate(c.bounds);
            Vector3 cabinSize = bounds.size - Vector3.one * (2f * CabinMargin);

            var so = new SerializedObject(elevator);
            so.FindProperty("_car").objectReferenceValue = body;
            SetArray(so.FindProperty("_doorLeaves"), doors);
            SetArray(so.FindProperty("_callPoints"), calls);
            SerializedProperty slides = so.FindProperty("_doorSlides");
            slides.arraySize = doors.Length;
            for (int i = 0; i < doors.Length; i++)
                slides.GetArrayElementAtIndex(i).vector3Value = Vector3.right * DoorSlide;
            so.FindProperty("_travel").floatValue = RoofHeight;
            so.FindProperty("_playerMask").intValue = LayerMask.GetMask(PlayerLayerName);
            so.FindProperty("_cabinCentre").vector3Value = car.InverseTransformPoint(bounds.center);
            so.FindProperty("_cabinSize").vector3Value = cabinSize;
            so.ApplyModifiedPropertiesWithoutUndo();
            report.Append("Lift wired: travel ").Append(RoofHeight).Append(" m, rider box ").Append(cabinSize.ToString("F2")).Append('.');
        }

        static void SetArray(SerializedProperty array, Transform[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ------------------------------------------------------------------ helpers

        static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            return g;
        }

        static bool HasKit(string piece) => AssetDatabase.LoadAssetAtPath<GameObject>($"{KitFolder}/{piece}.prefab") != null;

        /// <summary>Places a kit prefab at roof-local (x, 0, z) metres, turned <paramref name="yaw"/> degrees.</summary>
        static void Kit(Transform parent, string piece, float x, float z, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitFolder}/{piece}.prefab");
            if (prefab == null) throw new System.IO.FileNotFoundException($"No kit prefab {piece}; run Import Kit first.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetLocalPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
