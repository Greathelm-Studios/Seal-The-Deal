using System.Collections.Generic;
using CloseTheDeal.Player;
using CloseTheDeal.Tower;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Props
{
    /// <summary>
    /// Host only. Once the towers are built, spawns a networked prop at every marker in
    /// both buildings (capped per floor), freezes loose props on floors far from every player,
    /// and despawns them all if the towers are rebuilt.
    /// Lives on the Tower object next to TowerBuilder.
    /// </summary>
    [RequireComponent(typeof(TowerBuilder))]
    public sealed class PropSpawner : NetworkBehaviour
    {
        const int MaxPropsPerFloor = 16;
        const int ActiveFloorRadius = 2;
        const float FreezeCheckIntervalSeconds = 0.5f;

        sealed class FloorGroup
        {
            public int Floor;
            public readonly List<Prop> Props = new(MaxPropsPerFloor);
            public bool Frozen;
        }

        readonly List<NetworkObject> _spawned = new();
        readonly List<PropMarker> _markers = new();
        readonly List<FloorGroup> _groups = new();
        readonly List<int> _occupiedFloors = new(8);

        TowerBuilder _tower;
        Matrix4x4 _worldToLocal;
        float _floorHeight = 4f;
        int _floorCount = 1;
        bool _ticking;
        float _nextCheckAt;

        public int SpawnedCount => _spawned.Count;

        void Awake()
        {
            _tower = GetComponent<TowerBuilder>();
            _tower.OnBuilt += OnTowerBuilt;
            _tower.OnCleared += DespawnAll;
        }

        void OnDestroy()
        {
            SetTicking(false);
            if (_tower == null)
                return;

            _tower.OnBuilt -= OnTowerBuilt;
            _tower.OnCleared -= DespawnAll;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            SetTicking(false);
        }

        void OnTowerBuilt()
        {
            if (!IsServerStarted)
                return;

            DespawnAll();

            TowerProfile profile = _tower.Profile;
            _floorHeight = profile != null && profile.FloorHeight > 0f ? profile.FloorHeight : 4f;
            _floorCount = Mathf.Max(1, _tower.FloorCount);
            _worldToLocal = _tower.transform.worldToLocalMatrix;

            int groupCount = TowerBuilder.TeamCount * _floorCount;
            EnsureGroups(groupCount);

            _tower.GetComponentsInChildren(true, _markers);
            int capped = 0;
            for (int i = 0; i < _markers.Count; i++)
            {
                PropMarker marker = _markers[i];
                if (marker.Prefab == null)
                {
                    Debug.LogWarning($"[Props] Marker {marker.name} has no prefab.");
                    continue;
                }

                CellOf(marker.transform.position, out int tower, out int floor);
                FloorGroup group = _groups[tower * _floorCount + floor];
                if (group.Props.Count >= MaxPropsPerFloor)
                {
                    capped++;
                    continue;
                }

                NetworkObject propObj = NetworkManager.GetPooledInstantiated(marker.Prefab, marker.transform.position, marker.transform.rotation, true);
                ServerManager.Spawn(propObj);
                _spawned.Add(propObj);

                if (propObj.TryGetComponent(out Prop prop))
                    group.Props.Add(prop);
            }

            _markers.Clear();
            _nextCheckAt = Time.time + FreezeCheckIntervalSeconds;
            SetTicking(_spawned.Count > 0);
            UpdateFreezing();

            if (capped > 0)
                Debug.Log($"[Props] Spawned {_spawned.Count} props ({capped} skipped by {MaxPropsPerFloor}/floor cap).");
            else
                Debug.Log($"[Props] Spawned {_spawned.Count} props.");
        }

        void DespawnAll()
        {
            SetTicking(false);
            _markers.Clear();
            for (int i = 0; i < _groups.Count; i++)
            {
                _groups[i].Props.Clear();
                _groups[i].Frozen = false;
            }

            if (!IsServerStarted)
                return;

            foreach (NetworkObject prop in _spawned)
            {
                if (prop != null && prop.IsSpawned)
                    ServerManager.Despawn(prop);
            }

            _spawned.Clear();
        }

        void SetTicking(bool active)
        {
            if (_ticking == active || TimeManager == null)
                return;

            _ticking = active;
            if (active)
                TimeManager.OnTick += ServerTick;
            else
                TimeManager.OnTick -= ServerTick;
        }

        void ServerTick()
        {
            if (!IsServerStarted || _spawned.Count == 0)
            {
                SetTicking(false);
                return;
            }

            if (Time.time < _nextCheckAt)
                return;

            _nextCheckAt = Time.time + FreezeCheckIntervalSeconds;
            UpdateFreezing();
        }

        void UpdateFreezing()
        {
            _occupiedFloors.Clear();
            List<PlayerMotor> players = PlayerMotor.All;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerMotor motor = players[i];
                if (motor == null)
                    continue;

                int floor = FloorOf(motor.transform.position);
                if (!_occupiedFloors.Contains(floor))
                    _occupiedFloors.Add(floor);
            }

            bool hasPlayers = _occupiedFloors.Count > 0;
            for (int g = 0; g < _groups.Count; g++)
            {
                FloorGroup group = _groups[g];
                if (group.Props.Count == 0)
                    continue;

                bool near = !hasPlayers || IsNearOccupiedFloor(group.Floor);
                if (near)
                {
                    if (!group.Frozen)
                        continue;

                    for (int i = 0; i < group.Props.Count; i++)
                    {
                        Prop prop = group.Props[i];
                        if (prop != null && prop.IsSpawned && prop.Frozen)
                            prop.SetFrozen(false);
                    }
                    group.Frozen = false;
                }
                else
                {
                    for (int i = 0; i < group.Props.Count; i++)
                    {
                        Prop prop = group.Props[i];
                        if (prop == null || !prop.IsSpawned || prop.HeldStill || prop.Frozen)
                            continue;

                        // Never freeze props still in flight or relocated near an active floor.
                        if (prop.Body.linearVelocity.sqrMagnitude < 0.0025f && !IsNearOccupiedFloor(FloorOf(prop.transform.position)))
                            prop.SetFrozen(true);
                    }
                    group.Frozen = true;
                }
            }
        }

        bool IsNearOccupiedFloor(int floor)
        {
            for (int i = 0; i < _occupiedFloors.Count; i++)
            {
                if (Mathf.Abs(_occupiedFloors[i] - floor) <= ActiveFloorRadius)
                    return true;
            }
            return false;
        }

        void EnsureGroups(int count)
        {
            while (_groups.Count < count)
                _groups.Add(new FloorGroup());

            for (int i = 0; i < _groups.Count; i++)
            {
                _groups[i].Floor = i % _floorCount;
                _groups[i].Props.Clear();
                _groups[i].Frozen = false;
            }
        }

        void CellOf(Vector3 worldPosition, out int tower, out int floor)
        {
            Vector3 local = _worldToLocal.MultiplyPoint3x4(worldPosition);
            tower = local.z < 0f ? 0 : 1;
            floor = Mathf.Clamp(Mathf.FloorToInt((local.y + 0.1f) / _floorHeight), 0, _floorCount - 1);
        }

        int FloorOf(Vector3 worldPosition)
        {
            Vector3 local = _worldToLocal.MultiplyPoint3x4(worldPosition);
            return Mathf.Clamp(Mathf.FloorToInt((local.y + 0.1f) / _floorHeight), 0, _floorCount - 1);
        }
    }
}
