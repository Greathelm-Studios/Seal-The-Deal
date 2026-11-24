using System.Collections.Generic;
using System.Diagnostics;
using CloseTheDeal.FloorGen;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
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
        const float LightHeight = 4.05f;
        const float BandWidth = 0.3f, BandLift = 0.012f, SloganWidth = 3.6f, TextGap = 0.004f;
        const float SillHeight = 0.6f, ClearHeight = 4.3f, BounceLift = 0.15f, BounceAngle = 160f, BounceInner = 40f, WallClearance = 0.5f;
        const float MirrorHeight = 1f, MirrorDepth = 0.42f, MirrorMargin = 0.4f, MirrorBlend = 0.2f, MirrorFar = 120f;
        static readonly Vector3 IndicatorAt = new(2f, 2.66f, 0.044f);
        static readonly Vector2 IndicatorSize = new(0.9f, 0.2f);

        [Tooltip("Which kit prefab each generated piece is. Without it nothing can be built.")]
        [SerializeField] FloorKit _kit;

        [Tooltip("How far apart in height consecutive floors are built, in metres. It must be more than one storey (4.5) so the next floor can be built above while the old one still stands; 9 leaves a clear storey between them.")]
        [Min(5f)] [SerializeField] float _floorPitch = 9f;

        [Tooltip("Merge the pieces that never move into a few big meshes after building, to cut draw calls. Only works once the kit's models are imported with Read/Write on; leave off until then or a build logs an error for every mesh.")]
        [SerializeField] bool _mergeStillPieces;

        readonly List<GameObject> _still = new();
        readonly List<MeshFilter> _filters = new();
        readonly List<Mesh> _own = new();
        List<PieceLabel>[] _labels;
        Mesh _quad;
        int _floorNumber;

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
            _floorNumber = layout.Floor;
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
            AddMarks(root.transform, layout);
            AddRingBand(root.transform, layout);
            AddLights(root.transform, layout);
            AddBounce(root.transform, layout);

            if (_still.Count > 0)
                Merge(root, built);
            _still.Clear();

            // Queries made in the same frame (spawn checks, loot placement) must see the new colliders.
            Physics.SyncTransforms();
            AddMirror(built);
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
            List<PieceLabel> labels = LabelsFor(piece);
            if (labels != null)
                foreach (PieceLabel label in labels)
                    Flat(instance.transform, label.Material, label.Centre, Quaternion.Euler(0f, label.Back ? 0f : 180f, 0f), label.Size);
            if (piece == KitPiece.Lift_Door_Frame_4m_1F && _kit.SignFont != null)
                Letter(instance.transform, "Floor indicator", _floorNumber + "F", IndicatorAt, Quaternion.Euler(0f, 180f, 0f), IndicatorSize, _kit.IndicatorColour);
            return instance;
        }

        List<PieceLabel> LabelsFor(KitPiece piece)
        {
            if (_labels == null)
            {
                _labels = new List<PieceLabel>[FloorKit.PieceCount];
                foreach (PieceLabel label in _kit.Labels)
                {
                    int index = (int)label.Piece;
                    if (label.Material == null || index >= _labels.Length)
                        continue;
                    (_labels[index] ??= new List<PieceLabel>()).Add(label);
                }
            }

            return _labels[(int)piece];
        }

        // Unity's quad faces -Z, so a rotation that looks into the wall shows it to the room.
        GameObject Flat(Transform parent, Material material, Vector3 at, Quaternion rotation, Vector2 size)
        {
            if (_quad == null)
                _quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

            var go = new GameObject(material.name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(at, rotation);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        void AddMarks(Transform parent, FloorLayout layout)
        {
            if (layout.Marks.Count == 0)
                return;

            var group = new GameObject("Signs").transform;
            group.SetParent(parent, false);
            foreach (WallMark mark in layout.Marks)
            {
                var at = new Vector3(mark.X, mark.Height, -mark.Y);
                Quaternion facing = Quaternion.LookRotation(new Vector3(-mark.NormalX, 0f, mark.NormalY));
                if (mark.Kind == MarkKind.Poster)
                {
                    if (_kit.Posters.Length == 0)
                        continue;
                    Material poster = _kit.Posters[mark.Index % _kit.Posters.Length];
                    if (poster != null)
                        Flat(group, poster, at, facing, new Vector2(mark.Width, mark.Tall));
                    continue;
                }

                if (_kit.SignFont == null)
                    continue;
                if (mark.Kind == MarkKind.Slogan)
                {
                    if (_kit.SloganBand == null || _kit.Slogans.Length == 0)
                        continue;
                    Flat(group, _kit.SloganBand, at, facing, new Vector2(mark.Width, mark.Tall));
                    Vector3 proud = at + new Vector3(mark.NormalX, 0f, -mark.NormalY) * TextGap;
                    Letter(group, "Slogan", _kit.Slogans[mark.Index % _kit.Slogans.Length], proud, facing, new Vector2(SloganWidth, mark.Tall * 0.5f), _kit.NameColour);
                    continue;
                }

                bool number = mark.Kind == MarkKind.FloorNumber;
                Letter(group, number ? "Floor number" : "Room name", number ? mark.Text : mark.Text.ToUpperInvariant(), at, facing,
                       new Vector2(mark.Width, mark.Tall), number || mark.Index == 1 ? _kit.NumberColour : _kit.NameColour);
            }
        }

        void Letter(Transform parent, string name, string words, Vector3 at, Quaternion rotation, Vector2 size, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = size;
            go.transform.SetLocalPositionAndRotation(at, rotation);
            text.font = _kit.SignFont;
            text.text = words;
            text.color = colour;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.1f;
            text.fontSizeMax = 400f;
            text.renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void AddRingBand(Transform parent, FloorLayout layout)
        {
            if (_kit.RingBand == null)
                return;

            const float C = FloorLayout.Cell;
            float x0 = (layout.RingX0 + 0.5f) * C, x1 = (layout.RingX1 + 0.5f) * C, y0 = (layout.RingY0 + 0.5f) * C, y1 = (layout.RingY1 + 0.5f) * C;
            Quaternion flat = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var group = new GameObject("Ring band").transform;
            group.SetParent(parent, false);
            var along = new Vector2(x1 - x0 + BandWidth, BandWidth);
            var across = new Vector2(BandWidth, y1 - y0 - BandWidth);
            Flat(group, _kit.RingBand, new Vector3((x0 + x1) * 0.5f, BandLift, -y0), flat, along);
            Flat(group, _kit.RingBand, new Vector3((x0 + x1) * 0.5f, BandLift, -y1), flat, along);
            Flat(group, _kit.RingBand, new Vector3(x0, BandLift, -(y0 + y1) * 0.5f), flat, across);
            Flat(group, _kit.RingBand, new Vector3(x1, BandLift, -(y0 + y1) * 0.5f), flat, across);
        }

        // Realtime light has no bounce, so ceilings over sunlit floor stay grey without a soft light where the sun lands.
        void AddBounce(Transform parent, FloorLayout layout)
        {
            if (_kit.BounceIntensity <= 0f)
                return;
            Light sun = RenderSettings.sun;
            if (sun == null)
                return;
            Vector3 travel = parent.InverseTransformDirection(sun.transform.forward);
            if (travel.y >= -0.05f)
                return;

            const float C = FloorLayout.Cell;
            float windowMiddle = (SillHeight + ClearHeight) * 0.5f;
            Vector3 reach = travel * (windowMiddle / -travel.y);
            Quaternion up = Quaternion.LookRotation(Vector3.up);
            Transform group = null;
            for (int j = 0; j < layout.Rows; j++)
                for (int i = 0; i < layout.Cols; i++)
                    for (int side = 0; side < 4; side++)
                    {
                        EdgeKind kind = layout.EdgeOf(i, j, side).Kind;
                        if (kind != EdgeKind.FacadeWindow && kind != EdgeKind.FacadeCurtain)
                            continue;
                        int dx = Side.Dx[side], dy = Side.Dy[side];
                        if (travel.x * dx - travel.z * dy >= -0.05f)
                            continue;

                        Vector3 patch = new Vector3((i + 0.5f + dx * 0.5f) * C, windowMiddle, -(j + 0.5f + dy * 0.5f) * C) + reach;
                        if (patch.x < WallClearance || patch.x > layout.Width - WallClearance || -patch.z < WallClearance || -patch.z > layout.Depth - WallClearance)
                            continue;

                        if (group == null)
                        {
                            group = new GameObject("Bounce lights").transform;
                            group.SetParent(parent, false);
                        }
                        var go = new GameObject("Bounce light");
                        go.transform.SetParent(group, false);
                        go.transform.SetLocalPositionAndRotation(new Vector3(patch.x, BounceLift, patch.z), up);
                        var light = go.AddComponent<Light>();
                        light.type = LightType.Spot;
                        light.color = _kit.BounceColour;
                        light.intensity = _kit.BounceIntensity;
                        light.range = _kit.BounceRange;
                        light.spotAngle = BounceAngle;
                        light.innerSpotAngle = BounceInner;
                        light.shadows = LightShadows.None;
                    }
        }

        // Rendered once, a face a frame. Unity skips it unless Realtime Reflection Probes is on in Quality settings.
        void AddMirror(BuiltFloor built)
        {
            if (!_kit.FloorReflections || !QualitySettings.realtimeReflectionProbes)
                return;

            FloorLayout layout = built.Layout;
            var go = new GameObject("Reflection probe");
            go.transform.SetParent(built.Root.transform, false);
            go.transform.localPosition = new Vector3(layout.Width * 0.5f, MirrorHeight, -layout.Depth * MirrorDepth);

            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.boxProjection = true;
            probe.center = new Vector3(layout.Width * 0.5f, ClearHeight * 0.5f, -layout.Depth * 0.5f) - go.transform.localPosition;
            probe.size = new Vector3(layout.Width, ClearHeight, layout.Depth) + Vector3.one * (2f * MirrorMargin);
            probe.blendDistance = MirrorBlend;
            probe.resolution = Mathf.ClosestPowerOfTwo(_kit.ReflectionSize);
            probe.hdr = true;
            probe.farClipPlane = MirrorFar;
            probe.RenderProbe();
        }

        void AddLights(Transform parent, FloorLayout layout)
        {
            if (layout.Lights.Count == 0 || _kit.LightIntensity <= 0f)
                return;

            var group = new GameObject("Lights").transform;
            group.SetParent(parent, false);
            Color colour = (layout.Floor & 1) == 1 ? _kit.LightOdd : _kit.LightEven;
            Quaternion down = Quaternion.LookRotation(Vector3.down);
            foreach (FloorSpot spot in layout.Lights)
            {
                var go = new GameObject("Room light");
                go.transform.SetParent(group, false);
                go.transform.SetLocalPositionAndRotation(new Vector3(spot.X, LightHeight, -spot.Y), down);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = colour;
                light.intensity = _kit.LightIntensity;
                light.range = _kit.LightRange;
                light.spotAngle = _kit.LightAngle;
                light.innerSpotAngle = _kit.LightAngle * 0.45f;
                light.shadows = LightShadows.None;
            }
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
