using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace CloseTheDeal.Tower
{
    /// <summary>Host-seeded procedural builder for both team towers. Design: docs/systems/TOWER.md.</summary>
    [DisallowMultipleComponent]
    public sealed class TowerBuilder : NetworkBehaviour
    {
        public const int TeamCount = 2;

        public static TowerBuilder Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        /// <summary>Both buildings stand. Raised on every machine after its own local build.</summary>
        public event Action OnBuilt;

        /// <summary>The buildings are about to be destroyed for a rebuild.</summary>
        public event Action OnCleared;

        [Tooltip("The knobs and templates this tower is built from. Read only at runtime.")]
        [SerializeField] TowerProfile _profile;

        readonly SyncVar<uint> _seed = new();
        readonly List<FloorSpec> _layout = new();
        readonly Transform[][] _spawns = new Transform[TeamCount][];
        readonly GameObject[] _buildings = new GameObject[TeamCount];
        readonly List<GameObject> _stillParts = new();
        readonly List<MeshFilter> _meshes = new();

        bool _built;
        uint _builtSeed;

        public uint Seed => _seed.Value;
        public bool IsBuilt => _built;
        public TowerProfile Profile => _profile;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            DestroyMergedMeshes();
            if (Instance == this)
                Instance = null;
        }

        /// <summary>How many floors each building has, reception and roof included. 0 until built.</summary>
        public int FloorCount => _built ? _layout.Count : 0;

        /// <summary>What the floor at this height is, counted from reception = 0. Both buildings share the same order of kinds.</summary>
        public FloorKind FloorKindAt(int floor) => _layout[floor].Kind;

        /// <summary>The root of a team's building: its origin is the centre of the reception's window edge. Null until built.</summary>
        public Transform BuildingRoot(int team)
        {
            GameObject building = team >= 0 && team < TeamCount ? _buildings[team] : null;
            return building != null ? building.transform : null;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _seed.OnChange += OnSeedChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _seed.OnChange -= OnSeedChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _seed.Value = PickSeed();
            Build(_seed.Value);
        }

        /// <summary>Host only. Picks a new seed and rebuilds both towers; clients rebuild when the SyncVar updates.</summary>
        public void Rebuild()
        {
            if (!IsServerStarted)
                return;

            uint seed = PickSeed();
            if (seed == _seed.Value)
                seed++;
            _seed.Value = seed == 0 ? 1u : seed;
            Build(_seed.Value);
        }

        void OnSeedChanged(uint previous, uint next, bool asServer)
        {
            if (!asServer && !IsServerStarted && _built)
                Build(next);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Host already built as server; pure clients build from the synced seed.
            if (!IsServerStarted)
                Build(_seed.Value);
        }

        /// <summary>Where a player of this team appears: the team's lobby markers, round-robin.</summary>
        public bool TryGetSpawn(int team, int slot, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;

            Transform[] points = team >= 0 && team < TeamCount ? _spawns[team] : null;
            if (!_built || points == null || points.Length == 0)
                return false;

            Transform point = points[slot % points.Length];
            position = point.position;
            rotation = point.rotation;
            return true;
        }

        void Build(uint seed)
        {
            if (_built && _builtSeed == seed)
                return;

            if (_profile == null || _profile.Lobby == null || _profile.Offices == null || _profile.Offices.Length == 0)
            {
                Debug.LogError("[Tower] TowerProfile is missing or has no lobby or office templates.");
                return;
            }

            Clear();
            for (int team = 0; team < TeamCount; team++)
                BuildBuilding(team, _profile.SharedSeed ? seed : seed + (uint)team * 7919u);

            _built = true;
            _builtSeed = seed;
            Debug.Log($"[Tower] Built two towers of {_layout.Count} floors from seed {seed}.");
            OnBuilt?.Invoke();
        }

        void BuildBuilding(int team, uint seed)
        {
            TowerLayout.Build(_profile, seed, _layout);

            var root = new GameObject(team == 0 ? "Building A" : "Building B");
            root.transform.SetParent(transform, false);
            float side = team == 0 ? -1f : 1f;
            root.transform.localPosition = new Vector3(0f, 0f, side * _profile.Gap * 0.5f);
            root.transform.localRotation = Quaternion.Euler(0f, team == 0 ? 0f : 180f, 0f);
            _buildings[team] = root;

            for (int i = 0; i < _layout.Count; i++)
            {
                FloorSpec spec = _layout[i];
                FloorTemplate template = TemplateFor(spec);
                if (template == null)
                {
                    Debug.LogError($"[Tower] No template for {spec.Kind}; floor {i} skipped.");
                    continue;
                }

                FloorTemplate floor = Instantiate(template, root.transform);
                floor.name = $"{i:00} {spec.Kind}";
                floor.transform.localPosition = new Vector3(0f, i * _profile.FloorHeight, 0f);
                floor.transform.localRotation = Quaternion.identity;

                if (spec.Kind == FloorKind.Lobby)
                    _spawns[team] = floor.SpawnPoints;

                CollectStillParts(floor);
            }

            // Static batch NeverMoves parts at runtime; instantiated floors skip build-time batching.
            if (_stillParts.Count > 0)
            {
                NoteOwnMeshes();
                StaticBatchingUtility.Combine(_stillParts.ToArray(), root);
                RecordMergedMeshes();
            }
            _stillParts.Clear();
        }

        void CollectStillParts(FloorTemplate floor)
        {
            foreach (Transform part in floor.NeverMoves)
            {
                if (part == null)
                    continue;

                part.GetComponentsInChildren(true, _meshes);
                foreach (MeshFilter mesh in _meshes)
                    _stillParts.Add(mesh.gameObject);
            }
        }

        FloorTemplate TemplateFor(FloorSpec spec)
        {
            switch (spec.Kind)
            {
                case FloorKind.Lobby: return _profile.Lobby;
                case FloorKind.Checkpoint: return _profile.Checkpoint;
                case FloorKind.Boardroom: return _profile.Boardroom;
                case FloorKind.Roof: return _profile.Roof;
                default: return _profile.Offices[Mathf.Clamp(spec.TemplateIndex, 0, _profile.Offices.Length - 1)];
            }
        }

        void Clear()
        {
            if (_built)
                OnCleared?.Invoke();

            for (int team = 0; team < TeamCount; team++)
            {
                if (_buildings[team] != null)
                {
                    // Detach before Destroy so listeners during rebuild never walk stale children.
                    _buildings[team].SetActive(false);
                    _buildings[team].transform.SetParent(null, false);
                    Destroy(_buildings[team]);
                }
                _buildings[team] = null;
                _spawns[team] = null;
            }

            DestroyMergedMeshes();
            _built = false;
        }

        // Snapshot authored sharedMeshes before Combine so only newly generated meshes are freed on Clear/Destroy.
        void NoteOwnMeshes()
        {
            _ownMeshes.Clear();
            foreach (GameObject part in _stillParts)
                _ownMeshes.Add(part.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null);
        }

        void RecordMergedMeshes()
        {
            for (int i = 0; i < _stillParts.Count; i++)
            {
                GameObject part = _stillParts[i];
                if (part != null && part.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null && filter.sharedMesh != _ownMeshes[i])
                    _merged.Add(filter.sharedMesh);
            }
            _ownMeshes.Clear();
        }

        void DestroyMergedMeshes()
        {
            foreach (Mesh mesh in _merged)
            {
                if (mesh != null)
                    Destroy(mesh);
            }

            _merged.Clear();
        }

        readonly HashSet<Mesh> _merged = new();
        readonly List<Mesh> _ownMeshes = new();

        static uint PickSeed()
        {
            // Any non-zero number; the clock is plenty for "different every run".
            uint seed = unchecked((uint)Environment.TickCount) ^ (uint)UnityEngine.Random.Range(1, int.MaxValue);
            return seed == 0 ? 1u : seed;
        }
    }
}
