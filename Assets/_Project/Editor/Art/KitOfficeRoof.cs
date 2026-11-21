using System.Text;
using CloseTheDeal.Lift;
using CloseTheDeal.Player;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloseTheDeal.Editor.Art
{
    /// <summary>Builds the KitOffice rooftop range and wires the elevator component.</summary>
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

        const float Cell = 4f;
        const int Cols = 10, Rows = 8;
        const float Width = Cols * Cell, Depth = Rows * Cell;
        const float RoofHeight = 9f;
        const float HeadHouseX0 = 18f, HeadHouseX1 = 22f, Half = 2f;

        static readonly Vector3 DummySpot = new Vector3(35f, 0f, -27f);
        const float DummyYaw = -90f;
        const float PadNorthZ = -20f, PadWestX = 28f;
        const float MirrorCentreHeight = 1.84f;
        static readonly Vector3 MirrorScale = new Vector3(8f, 3.45f, 1f);
        const float MirrorInset = 0.3f;
        const float MirrorYaw = 90f;

        static readonly Vector3 BayCorner = new Vector3(15f, 0f, -24f);
        const float BayWidth = 8f, BayDepth = 6f;

        const string ArsenalFolder = "Assets/_Project/Prefabs/Arsenal";
        static readonly float[] RackCentres = { 2f, 4f, 6f };
        const float RackOffWall = 0.3f;
        const float RackTop = 1f;
        const float WeaponClearance = 0.005f;
        static readonly (string prefab, float x, float halfThickness, bool flat)[] Weapons =
        {
            ("Arsenal_Pistol", 1.7f, 0.0275f, true), ("Arsenal_Grenade", 2.3f, 0.05f, false),
            ("Arsenal_Rifle", 4f, 0.0545f, true), ("Arsenal_Launcher", 6f, 0.086f, true),
        };

        static readonly Vector3 PoolCorner = new Vector3(6f, 0f, -6f);
        const float PoolWidth = 8f, PoolDepth = 4f;
        const float PoolsideGap = 1.2f;

        static readonly (string prefab, float x, float z)[] Props =
        {
            ("Monitor", 28.5f, -24.5f), ("Monitor", 28.5f, -26f), ("Monitor", 28.5f, -27.5f),
            ("Chair", 27f, -25f), ("Chair", 27f, -27f), ("Chair", 27f, -29f),
            ("Cabinet", 29f, -29.5f), ("Cabinet", 29f, -22.5f),
        };
        const float PropDrop = 0.05f;

        const string CarName = "Lift_Car";
        static readonly string[] DoorNames = { "Door_1F_A", "Door_1F_B", "Door_RF_A", "Door_RF_B" };
        const float DoorSlide = 0.75f;
        static readonly Vector3 CallSpot = new Vector3(20f, 0f, -5f);
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
            if (!EditorUtility.DisplayDialog("Add Kit Office Roof Range", "This rebuilds the 'Roof range' group and moves the HitDummy onto the roof. The scene is not saved.", "Build", "Cancel"))
                return;
            Debug.Log(AddTo(office.transform));
            EditorSceneManager.MarkSceneDirty(office.scene);
        }

        /// <summary>Rebuilds the "Roof range" group under <paramref name="office"/> and wires the lift if present.</summary>
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
            // Only move HitDummy onto the roof once the lift exists to reach it.
            if (office.Find(LiftGroupName + "/" + CarName) != null) Dummy(roof, report);
            else report.Append("HitDummy left on the upper floor: there is no lift to reach the roof yet. ");
            report.Append(placed).Append(" pieces on the roof. ");
            WireLift(office, report);
            return report.ToString();
        }

        static int Edges(Transform roof)
        {
            var edges = Group(roof, "Edges");
            int count = 0;
            for (int i = 0; i < Cols; i++)
            {
                // Use 2 m rails either side of the lift head-house at x 18-22 so rails do not clip its glass.
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

        static int ArsenalBay(Transform roof, StringBuilder report)
        {
            var bay = Group(roof, "ArsenalBay");
            bay.localPosition = BayCorner;
            var wall = Group(roof, "Arsenal rack wall");
            int count = 0;
            for (float x = 0f; x < BayWidth; x += Cell, count++)
                Kit(wall, "Partition_Header_4m", BayCorner.x + x, BayCorner.z - BayDepth, 0f);

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
            Kit(pool, "Chair_Pod", PoolCorner.x + PoolWidth / 3f, side, 0f);
            Kit(pool, "Chair_Pod", PoolCorner.x + PoolWidth * 2f / 3f, side, 0f);
            return count + 4;
        }

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

        static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            return g;
        }

        static bool HasKit(string piece) => AssetDatabase.LoadAssetAtPath<GameObject>($"{KitFolder}/{piece}.prefab") != null;

        static void Kit(Transform parent, string piece, float x, float z, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitFolder}/{piece}.prefab");
            if (prefab == null) throw new System.IO.FileNotFoundException($"No kit prefab {piece}; run Import Kit first.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetLocalPositionAndRotation(new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f));
        }
    }
}
