// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "make the dummy and arsenal section be on the ceiling. to access it, you
//              should [take] an elevator"; "open rooftop"; "what could be funny is if added a pool";
//              "tell the elevator chat to start building the roof"
//  Modified  : Claude Fable 5.2 (Claude Code), 2026-11-14 — the HitDummy and its roof mirror move up only
//              once the lift is in the scene, so a rebuild cannot strand the dummy on an unreachable roof.
//  Modified  : Claude Opus 5.5, 2026-11-15 — the L2 floor plan: a 40 × 32 m plate, the pad, bay and props moved
//              with the south-east corner, the call spot in front of the lift's south-facing doors.
//  Modified  : Claude Opus 5.5, 2026-11-15 evening - north-edge rails stop at the lift head-house instead of running
//              through its glass.
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
    /// nothing else in the scene except the HitDummy instance, which it moves once the lift exists (agreed with
    /// the hit-reaction session: nothing reads the dummy's position). Kit pieces that are not modelled yet (the pool, the lift)
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
        /// <summary>Floor plate, as KitOfficeScene: 10 × 8 cells, the L2 plan's 40 × 32 m (2026-11-15).</summary>
        const int Cols = 10, Rows = 8;
        const float Width = Cols * Cell, Depth = Rows * Cell;
        /// <summary>Top of the roof slab: two storeys of 4.5 m (KitOfficeScene.FloorHeight × Levels).</summary>
        const float RoofHeight = 9f;
        /// <summary>The lift head-house on the roof's north edge, x metres (KitOfficeScene ShaftX0, ShaftX1), and the
        /// half-bay rail that stops at it.</summary>
        const float HeadHouseX0 = 18f, HeadHouseX1 = 22f, Half = 2f;

        // ---- Dummy pad: the roof's south-east corner.
        /// <summary>
        /// Where the dummy stands, roof-local metres. 5 m from the east and south screens, so it has the 4 m of
        /// clear floor and the walls within 6 m that the hit-reaction session asked for (a blasted body must not
        /// clear a 1.1 m rail and drop off the roof).
        /// </summary>
        /// <remarks>Moved with the roof's south-east corner when the plate grew to 40 × 32 m (L2, 2026-11-15): the pad,
        /// the bay and the props keep their distances from that corner.</remarks>
        static readonly Vector3 DummySpot = new Vector3(35f, 0f, -27f);
        /// <summary>The dummy faces west (-X), toward the arsenal bay's firing line.</summary>
        const float DummyYaw = -90f;
        /// <summary>The pad's full-height screens replace the rails from here to the corner: east edge south of
        /// this z, south edge east of this x.</summary>
        const float PadNorthZ = -20f, PadWestX = 28f;
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
        static readonly Vector3 BayCorner = new Vector3(15f, 0f, -24f);
        /// <summary>Bay size, metres, as the weapons session asked.</summary>
        const float BayWidth = 8f, BayDepth = 6f;

        const string ArsenalFolder = "Assets/_Project/Prefabs/Arsenal";
        /// <summary>Centres of the three Storage_Low display counters along the rack wall, bay-local metres (each is 1.6 long).</summary>
        static readonly float[] RackCentres = { 2f, 4f, 6f };
        /// <summary>Counter centre to the rack wall's face, metres: half of Storage_Low's 0.45 depth plus a 0.075 gap.</summary>
        const float RackOffWall = 0.3f;
        /// <summary>Top of Storage_Low, metres (kit_pieces STORE_H).</summary>
        const float RackTop = 1f;
        /// <summary>Weapons rest this far above the counter top, metres, so they do not z-fight with it.</summary>
        const float WeaponClearance = 0.005f;
        /// <summary>
        /// Stand-in weapons on the counters: (prefab, x along the wall in bay-local metres, half its resting
        /// thickness in metres, laid flat). Sizes as the weapon-assets session measured them, 2026-11-14. Display
        /// only: no physics and no pickup until the pickup question is answered.
        /// </summary>
        static readonly (string prefab, float x, float halfThickness, bool flat)[] Weapons =
        {
            ("Arsenal_Pistol", 1.7f, 0.0275f, true), ("Arsenal_Grenade", 2.3f, 0.05f, false),
            ("Arsenal_Rifle", 4f, 0.0545f, true), ("Arsenal_Launcher", 6f, 0.086f, true),
        };

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
            ("Monitor", 28.5f, -24.5f), ("Monitor", 28.5f, -26f), ("Monitor", 28.5f, -27.5f),
            ("Chair", 27f, -25f), ("Chair", 27f, -27f), ("Chair", 27f, -29f),
            ("Cabinet", 29f, -29.5f), ("Cabinet", 29f, -22.5f),
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
        /// <summary>Where a player waits to call the car: 1 m south of the doors (z −4), centred on them (x 20). The L2
        /// plan puts the shaft on the north façade with its doors facing the lobby (2026-11-15).</summary>
        static readonly Vector3 CallSpot = new Vector3(20f, 0f, -5f);
        /// <summary>The rider check is this much smaller than the car all round, metres, so someone outside the doors does not count.</summary>
        const float CabinMargin = 0.35f;

        [MenuItem("Close the Deal/Art/Add Kit Office Roof Range")]
        static void AddFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Add Kit Office Roof Range", "Stop Play Mode first.", "OK");
                return;
            }
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
            placed += ArsenalBay(roof, report);
            placed += Pool(roof, report);
            placed += LooseProps(roof, report);
            // WHY the dummy waits for the lift: without the lift nothing but a fly camera reaches the roof, and a
            // dummy up there could not be hit or judged in play. Until the kit's "Lift" group is in the scene the
            // dummy stays where KitOfficeScene put it, on the upper floor.
            if (office.Find(LiftGroupName + "/" + CarName) != null) Dummy(roof, report);
            else report.Append("HitDummy left on the upper floor: there is no lift to reach the roof yet. ");
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
                // The lift's head-house stands on the north edge at x 18-22 (KitOfficeScene's L2 shaft): its glass walls
                // are the edge there, so the two bays it straddles get 2 m rails up to it instead of 4 m rails through it.
                if (i * Cell == HeadHouseX0 - Half) Kit(edges, "Guard_Rail_2m", i * Cell, 0f, 0f);
                else if (i * Cell == HeadHouseX1 - Half) Kit(edges, "Guard_Rail_2m", HeadHouseX1, 0f, 0f);
                else Kit(edges, "Guard_Rail_4m", i * Cell, 0f, 0f);
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
        static int ArsenalBay(Transform roof, StringBuilder report)
        {
            // "ArsenalBay" is the weapons sessions' hook: its origin is the bay's north-west corner, +X along the
            // rack wall, firing line on its east edge.
            var bay = Group(roof, "ArsenalBay");
            bay.localPosition = BayCorner;
            var wall = Group(roof, "Arsenal rack wall");
            int count = 0;
            for (float x = 0f; x < BayWidth; x += Cell, count++)
                Kit(wall, "Partition_Header_4m", BayCorner.x + x, BayCorner.z - BayDepth, 0f);

            // Display counters against the rack wall, fronts toward the bay (+Z), with the stand-in weapons on top.
            foreach (float x in RackCentres)
            {
                Kit(bay, "Storage_Low", x, -BayDepth + RackOffWall, 0f);
                count++;
            }
            foreach (var (name, x, halfThickness, flat) in Weapons)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArsenalFolder}/{name}.prefab");
                if (prefab == null)
                {
                    report.Append("Weapon ").Append(name).Append(" missing, skipped. ");
                    continue;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, bay);
                go.transform.localPosition = new Vector3(x, RackTop + halfThickness + WeaponClearance, -BayDepth + RackOffWall);
                // Laid on its side with the muzzle along the wall (+X); a grenade just stands.
                go.transform.localRotation = flat ? Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
                count++;
            }
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
                if (prefab != null)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, DummyYaw, 0f));
                }
                else report.Append("No HitDummy prefab: dummy not placed. ");
            }

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
            if (!car.TryGetComponent(out Rigidbody body))
            {
                body = car.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
            }
            if (!lift.TryGetComponent(out NetworkObject _)) lift.gameObject.AddComponent<NetworkObject>();
            bool addedElevator = !lift.TryGetComponent(out Elevator elevator);
            if (addedElevator) elevator = lift.gameObject.AddComponent<Elevator>();

            var calls = new Transform[2];
            for (int stop = 0; stop < calls.Length; stop++)
            {
                string name = stop == 0 ? "Call spot 1F" : "Call spot RF";
                Transform call = lift.Find(name);
                if (call == null)
                {
                    call = new GameObject(name).transform;
                    call.SetParent(lift, false);
                    call.position = office.TransformPoint(CallSpot + Vector3.up * (stop * RoofHeight));
                }
                calls[stop] = call;
            }

            // The rider check: the car's own bounds, shrunk, in the car's local space.
            var bounds = new Bounds(car.position, Vector3.zero);
            foreach (Collider c in car.GetComponentsInChildren<Collider>()) bounds.Encapsulate(c.bounds);
            Vector3 cabinSize = bounds.size - Vector3.one * (2f * CabinMargin);

            var so = new SerializedObject(elevator);
            SerializedProperty carProp = so.FindProperty("_car");
            if (carProp.objectReferenceValue == null) carProp.objectReferenceValue = body;
            SerializedProperty leavesProp = so.FindProperty("_doorLeaves");
            if (leavesProp.arraySize == 0) SetArray(leavesProp, doors);
            SerializedProperty callsProp = so.FindProperty("_callPoints");
            if (callsProp.arraySize == 0) SetArray(callsProp, calls);
            SerializedProperty slides = so.FindProperty("_doorSlides");
            if (slides.arraySize == 0)
            {
                slides.arraySize = doors.Length;
                for (int i = 0; i < doors.Length; i++)
                    slides.GetArrayElementAtIndex(i).vector3Value = Vector3.right * DoorSlide;
            }
            SerializedProperty travelProp = so.FindProperty("_travel");
            if (addedElevator || travelProp.floatValue <= 0f) travelProp.floatValue = RoofHeight;
            SerializedProperty maskProp = so.FindProperty("_playerMask");
            if (addedElevator || maskProp.intValue == 0) maskProp.intValue = LayerMask.GetMask(PlayerLayerName);
            SerializedProperty cabinSizeProp = so.FindProperty("_cabinSize");
            if (addedElevator || cabinSizeProp.vector3Value == Vector3.zero)
            {
                so.FindProperty("_cabinCentre").vector3Value = car.InverseTransformPoint(bounds.center);
                cabinSizeProp.vector3Value = cabinSize;
            }
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
