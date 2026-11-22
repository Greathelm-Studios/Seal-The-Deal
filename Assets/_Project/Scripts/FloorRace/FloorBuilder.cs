using System.Collections.Generic;
using System.Diagnostics;
using CloseTheDeal.FloorGen;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CloseTheDeal.FloorRace
{
    /// <summary>The two sliding leaves of one lift, moved together. Open is 0 (shut) to 1.</summary>
    public sealed class FloorLiftDoors
    {
        const float Slide = 0.75f;

        readonly Transform _a, _b;
        readonly Vector3 _closedA, _closedB, _slideA, _slideB;
        float _open = -1f;

        public FloorLiftDoors(Transform a, Transform b)
        {
            _a = a;
            _b = b;
            _closedA = a.localPosition;
            _closedB = b.localPosition;
            _slideA = a.localRotation * Vector3.right * Slide;
            _slideB = b.localRotation * Vector3.right * Slide;
        }

        public void Set(float open)
        {
            if (Mathf.Approximately(open, _open) || _a == null)
                return;

            _open = open;
            _a.localPosition = _closedA + _slideA * open;
            _b.localPosition = _closedB + _slideB * open;
        }
    }

    /// <summary>One floor standing in the scene.</summary>
    public sealed class BuiltFloor
    {
        public FloorLayout Layout;
        public GameObject Root;
        public FloorLiftDoors ExitDoors, ArrivalDoors;
        public int Objects;
        public double BuildMilliseconds;
        internal readonly List<Mesh> Merged = new();

        /// <summary>A plan position on this floor as a world position, <paramref name="up"/> metres above the floor.</summary>
        public Vector3 World(float x, float y, float up = 0f) => Root.transform.TransformPoint(new Vector3(x, up, -y));

        /// <summary>A world position in this floor's plan metres (X east, Y south, Z up from the floor).</summary>
        public Vector3 Plan(Vector3 world)
        {
            Vector3 local = Root.transform.InverseTransformPoint(world);
            return new Vector3(local.x, -local.z, local.y);
        }
    }

    /// <summary>Builds generated floors from kit prefabs and tears them down. Local to each machine. Design: docs/systems/FLOOR_GEN.md.</summary>
    [DisallowMultipleComponent]
    public sealed class FloorBuilder : MonoBehaviour
    {
        const float BelowHeight = 40f, SlabThickness = 0.2f, BelowGap = 0.01f;

        [Tooltip("Which kit prefab each generated piece is. Without it nothing can be built.")]
        [SerializeField] FloorKit _kit;

        [Tooltip("How far apart in height consecutive floors are built, in metres. It must be more than one storey (4.5) so the next floor can be built above while the old one still stands; 9 leaves a clear storey between them.")]
        [Min(5f)] [SerializeField] float _floorPitch = 9f;

        [Tooltip("Merge the pieces that never move into a few big meshes after building, to cut draw calls. Only works once the kit's models are imported with Read/Write on; leave off until then or a build logs an error for every mesh.")]
        [SerializeField] bool _mergeStillPieces;

        readonly List<GameObject> _still = new();
        readonly List<MeshFilter> _filters = new();
        readonly List<Mesh> _own = new();

        public FloorKit Kit => _kit;
        public float FloorPitch => _floorPitch;

        /// <summary>Height of a floor's surface above this object, in metres. Floor 1 is at 0.</summary>
        public float HeightOf(int floor) => (floor - 1) * _floorPitch;

        public BuiltFloor Build(FloorLayout layout)
        {
            if (_kit == null)
            {
                Debug.LogError("[Floor Race] The floor builder has no Floor Kit. Run Seal the Deal > Floor Race > Build Scene.");
                return null;
            }

            Stopwatch watch = Stopwatch.StartNew();
            var root = new GameObject($"Floor {layout.Floor:00}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, HeightOf(layout.Floor), 0f);
            var built = new BuiltFloor { Layout = layout, Root = root };

            int missing = 0;
            _still.Clear();
            foreach (KitPlacement placement in layout.Pieces)
            {
                GameObject piece = Place(root.transform, placement.Piece, placement.X, placement.Y, placement.Height, placement.Yaw);
                if (piece == null)
                {
                    missing++;
                    continue;
                }

                built.Objects++;
                if (_mergeStillPieces && placement.Still)
                    CollectStill(piece);
            }

            if (layout.HasExitLift)
                built.ExitDoors = BuildLift(root.transform, layout.ExitLift, "Exit Lift", built);
            if (layout.HasArrivalLift)
                built.ArrivalDoors = BuildLift(root.transform, layout.ArrivalLift, "Arrival Lift", built);
            AddBuildingBelow(root.transform, layout);

            if (_still.Count > 0)
                Merge(root, built);
            _still.Clear();

            // Queries made in the same frame (spawn checks, loot placement) must see the new colliders.
            Physics.SyncTransforms();
            watch.Stop();
            built.BuildMilliseconds = watch.Elapsed.TotalMilliseconds;
            if (missing > 0)
                Debug.LogError($"[Floor Race] {missing} pieces of floor {layout.Floor} have no prefab in the Floor Kit. Run Seal the Deal > Floor Race > Fill Floor Kit.");
            return built;
        }

        public void TearDown(BuiltFloor floor)
        {
            if (floor == null)
                return;

            if (floor.Root != null)
            {
                // Detach first: Destroy waits for the end of the frame and nothing should find this floor meanwhile.
                floor.Root.SetActive(false);
                floor.Root.transform.SetParent(null, false);
                Destroy(floor.Root);
            }

            foreach (Mesh mesh in floor.Merged)
                if (mesh != null)
                    Destroy(mesh);
            floor.Merged.Clear();
            floor.Root = null;
        }

        GameObject Place(Transform parent, KitPiece piece, float x, float y, float height, float yaw)
        {
            GameObject prefab = _kit.PrefabFor(piece);
            if (prefab == null)
                return null;

            GameObject instance = Instantiate(prefab, parent);
            instance.transform.SetLocalPositionAndRotation(new Vector3(x, height, -y), Quaternion.Euler(0f, yaw, 0f));
            return instance;
        }

        FloorLiftDoors BuildLift(Transform parent, FloorLift lift, string name, BuiltFloor built)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            GameObject car = Place(group, KitPiece.Lift_Car, lift.CarX, lift.CarY, 0f, lift.CarYaw);
            GameObject a = Place(group, KitPiece.Lift_Door_Panel, lift.DoorX, lift.DoorY, 0f, lift.CarYaw);
            GameObject b = Place(group, KitPiece.Lift_Door_Panel, lift.DoorX, lift.DoorY, 0f, lift.CarYaw + 180f);
            if (car == null || a == null || b == null)
                return null;

            built.Objects += 3;
            return new FloorLiftDoors(a.transform, b.transform);
        }

        void AddBuildingBelow(Transform parent, FloorLayout layout)
        {
            if (_kit.BuildingBelow == null)
                return;

            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Building below";
            Destroy(block.GetComponent<Collider>());
            block.transform.SetParent(parent, false);
            block.transform.localPosition = new Vector3(layout.Width * 0.5f, -SlabThickness - BelowGap - BelowHeight * 0.5f, -layout.Depth * 0.5f);
            block.transform.localScale = new Vector3(layout.Width, BelowHeight, layout.Depth);
            block.GetComponent<Renderer>().sharedMaterial = _kit.BuildingBelow;
        }

        void CollectStill(GameObject piece)
        {
            piece.GetComponentsInChildren(true, _filters);
            foreach (MeshFilter filter in _filters)
                _still.Add(filter.gameObject);
        }

        // Combine swaps each filter's mesh for a generated one; only those are ours to destroy later.
        void Merge(GameObject root, BuiltFloor built)
        {
            _own.Clear();
            foreach (GameObject part in _still)
                _own.Add(part.GetComponent<MeshFilter>().sharedMesh);

            StaticBatchingUtility.Combine(_still.ToArray(), root);
            for (int i = 0; i < _still.Count; i++)
            {
                Mesh now = _still[i].GetComponent<MeshFilter>().sharedMesh;
                if (now != null && now != _own[i] && !built.Merged.Contains(now))
                    built.Merged.Add(now);
            }
            _own.Clear();
        }
    }
}
