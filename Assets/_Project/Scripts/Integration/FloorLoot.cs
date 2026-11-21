using System.Collections.Generic;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Tower;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Integration
{
    /// <summary>Host-only seeded floor loot spawner spread across frames on arena rebuild. Design: docs/systems/INTEGRATION.md.</summary>
    [DisallowMultipleComponent]
    public sealed class FloorLoot : NetworkBehaviour
    {
        [Tooltip("What lies on which floor at the start of every match. Read at runtime, never written.")]
        [SerializeField] FloorLootTable _table;

        const float ProbeAbove = 1f;
        const float ProbeDepth = 1.4f;
        const float FloorTolerance = 0.15f;
        const int FloorsPerFrame = 2;

        readonly List<NetworkObject> _spawned = new();
        readonly List<Vector3> _usedOnFloor = new();
        TowerBuilder _tower;
        MatchDirector _match;
        uint _placedForSeed;
        int _blockMask;
        int _floorMask;
        System.Random _spawnRandom;
        int _nextFloor;
        int _roofFloor;
        int _unplacedCount;
        bool _spawning;

        /// <summary>How many things this match put down, swapped-out guns included. Host only.</summary>
        public int SpawnedCount => _spawned.Count;

        public static FloorLoot Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            Instance = this;
            // A spot must be clear of the building, furniture and players; it must stand on the building.
            _blockMask = ~0;
            _floorMask = ~(LayerMask.GetMask("Player") | LayerMask.GetMask("Prop"));
        }

        void Start()
        {
            Hook();
            if (!_spawning)
                enabled = false;
        }

        bool Hook()
        {
            if (_match != null)
                return true;

            _tower = TowerBuilder.Instance;
            _match = MatchDirector.Instance;
            if (_tower == null || _match == null)
            {
                _match = null;
                Debug.LogError("[Integration] Floor loot needs the Tower and the Match in the scene; nothing will be placed.");
                return false;
            }

            _tower.OnCleared += DespawnAll;
            _match.OnArenaChanged += OnArenaChanged;
            return true;
        }

        void OnDestroy()
        {
            CancelSpawnQueue();
            if (Instance == this)
                Instance = null;
            if (_tower != null)
                _tower.OnCleared -= DespawnAll;
            if (_match != null)
                _match.OnArenaChanged -= OnArenaChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _placedForSeed = 0;
            // The towers may have stood before this object started on the server.
            if (Hook() && _match.Arena.Ready)
                OnArenaChanged();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            CancelSpawnQueue();
            _placedForSeed = 0;
            _spawned.Clear();
        }

        void OnArenaChanged()
        {
            if (!IsServerStarted || _table == null || !_match.Arena.Ready || _placedForSeed == _tower.Seed)
                return;

            _placedForSeed = _tower.Seed;
            DespawnAll();
            // Floors were just placed during build; sync transforms before placement physics checks.
            Physics.SyncTransforms();
            _spawnRandom = new System.Random(unchecked((int)_tower.Seed));
            _nextFloor = 1;
            _roofFloor = _match.Arena.RoofFloor;
            _unplacedCount = 0;
            if (_nextFloor < _roofFloor)
            {
                _spawning = true;
                enabled = true;
            }
            else
            {
                Debug.Log($"[Integration] Put {_spawned.Count} things on the floors for this match.");
            }
        }

        void Update()
        {
            if (!_spawning)
            {
                enabled = false;
                return;
            }

            StepSpawnQueue();
        }

        void StepSpawnQueue()
        {
            if (!IsServerStarted || _table == null || _match == null || !_match.Arena.Ready)
            {
                CancelSpawnQueue();
                return;
            }

            int end = Mathf.Min(_nextFloor + FloorsPerFrame, _roofFloor);
            while (_nextFloor < end)
            {
                _unplacedCount += PlaceFloor(_nextFloor, _spawnRandom);
                _nextFloor++;
            }

            if (_nextFloor >= _roofFloor)
            {
                int left = _unplacedCount;
                CancelSpawnQueue();
                Debug.Log($"[Integration] Put {_spawned.Count} things on the floors for this match" + (left > 0 ? $"; {left} found no room and were left out." : "."));
            }
        }

        void CancelSpawnQueue()
        {
            _spawning = false;
            _spawnRandom = null;
            _nextFloor = 0;
            _roofFloor = 0;
            _unplacedCount = 0;
            enabled = false;
        }

        int PlaceFloor(int floor, System.Random random)
        {
            _usedOnFloor.Clear();
            int left = 0;
            for (int i = 0; i < _table.Count; i++)
            {
                FloorLootTable.Entry entry = _table[i];
                if (entry.Floor != floor || entry.Prefab == null)
                    continue;

                if (TryFindSpot(floor, random, out Vector3 local, out float yaw))
                {
                    _usedOnFloor.Add(local);
                    for (int tower = 0; tower < TowerBuilder.TeamCount; tower++)
                        SpawnAt(entry.Prefab, _match.Arena.FloorPoint(tower, floor, local), _match.Arena.Facing(tower) * Quaternion.Euler(0f, yaw, 0f));
                }
                else
                {
                    left++;
                    Debug.LogWarning($"[Integration] No room on {_match.Arena.FloorLabel(floor)} for {entry.Prefab.name} this match.");
                }
            }

            return left;
        }

        bool TryFindSpot(int floor, System.Random random, out Vector3 local, out float yaw)
        {
            TowerProfile profile = _match.Arena.Profile;
            float halfWidth = profile.Width * 0.5f - _table.WallMargin;
            float back = profile.MainDepth - _table.WallMargin;
            for (int attempt = 0; attempt < _table.Tries; attempt++)
            {
                local = new Vector3(Range(random, -halfWidth, halfWidth), 0f, -Range(random, _table.WindowMargin, back));
                yaw = Range(random, 0f, 360f);
                if (ClearOfStairs(local) && ClearOfOthers(local) && FreeInBothTowers(floor, local))
                    return true;
            }

            local = default;
            yaw = 0f;
            return false;
        }

        bool ClearOfStairs(Vector3 local)
        {
            Vector3 fromStairs = local - _match.StairCentre;
            fromStairs.y = 0f;
            return fromStairs.sqrMagnitude >= _table.StairClearance * _table.StairClearance;
        }

        bool ClearOfOthers(Vector3 local)
        {
            float spacing = _table.Spacing * _table.Spacing;
            for (int i = 0; i < _usedOnFloor.Count; i++)
            {
                if ((_usedOnFloor[i] - local).sqrMagnitude < spacing)
                    return false;
            }

            return true;
        }

        bool FreeInBothTowers(int floor, Vector3 local)
        {
            for (int tower = 0; tower < TowerBuilder.TeamCount; tower++)
            {
                if (!Standable(_match.Arena.FloorPoint(tower, floor, local)))
                    return false;
            }

            return true;
        }

        bool Standable(Vector3 spot)
        {
            if (!Physics.Raycast(spot + Vector3.up * ProbeAbove, Vector3.down, out RaycastHit ground, ProbeDepth, _floorMask, QueryTriggerInteraction.Ignore))
                return false;
            if (Mathf.Abs(ground.point.y - spot.y) > FloorTolerance)
                return false;

            float room = _table.Clearance;
            Vector3 centre = ground.point + Vector3.up * (room + 0.05f);
            return !Physics.CheckBox(centre, new Vector3(room, room, room), Quaternion.identity, _blockMask, QueryTriggerInteraction.Ignore);
        }

        static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        void SpawnAt(NetworkObject prefab, Vector3 position, Quaternion rotation)
        {
            NetworkObject thing = NetworkManager.GetPooledInstantiated(prefab, position, rotation, true);
            ServerManager.Spawn(thing);
            _spawned.Add(thing);
        }

        void PruneDeadSpawned()
        {
            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                if (_spawned[i] == null || !_spawned[i].IsSpawned)
                    _spawned.RemoveAt(i);
            }
        }

        /// <summary>Host only. Spawns a loose networked object mid-match (e.g. a swapped-out gun) tracked for rebuild cleanup.</summary>
        public NetworkObject ServerSpawnLoose(NetworkObject prefab, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (!IsServerStarted || prefab == null)
                return null;

            PruneDeadSpawned();
            SpawnAt(prefab, position, rotation);
            NetworkObject thing = _spawned[_spawned.Count - 1];
            if (thing.TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = velocity;
                body.WakeUp();
            }

            return thing;
        }

        void DespawnAll()
        {
            CancelSpawnQueue();
            if (!IsServerStarted)
                return;

            for (int i = 0; i < _spawned.Count; i++)
            {
                NetworkObject thing = _spawned[i];
                if (thing != null && thing.IsSpawned)
                    ServerManager.Despawn(thing);
            }

            _spawned.Clear();
        }
    }
}
