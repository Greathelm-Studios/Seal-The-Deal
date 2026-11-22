using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace CloseTheDeal.Tower
{
    /// <summary>Host-authoritative player spawner replacing FishNet's PlayerSpawner: seats each joiner in the smaller firm.</summary>
    [DisallowMultipleComponent]
    public sealed class TeamSpawner : MonoBehaviour
    {
        /// <summary>Lets a game mode place a joining player itself. Return false to use the fallback spawns.</summary>
        public delegate bool SpawnChooser(int team, int slot, out Vector3 position, out Quaternion rotation);

        /// <summary>Host only. Raised just before a player is spawned: the player, their connection, their team and their slot in it.</summary>
        public static event Action<NetworkObject, NetworkConnection, int, int> OnPlayerSpawning;

        /// <summary>Host only. Asked first for where a joining player appears; null or false means the fallback spawns.</summary>
        public static SpawnChooser SpawnOverride;

        /// <summary>The spawner on the NetworkManager, or null when there is none.</summary>
        public static TeamSpawner Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            SpawnOverride = null;
            // OnPlayerSpawning is hooked by FirmMember at SubsystemRegistration; don't clear it here.
        }

        /// <summary>How many firms a match has.</summary>
        public const int TeamCount = 2;

        [Tooltip("The networked player prefab.")]
        [SerializeField] NetworkObject _playerPrefab;

        [Tooltip("Where players appear when the game mode does not place them itself. Cycled in order.")]
        public Transform[] FallbackSpawns = new Transform[0];

        struct Seat
        {
            public int Team;
            public int Slot;
        }

        const int SlotsPerTeam = 4;

        NetworkManager _networkManager;
        readonly Dictionary<int, Seat> _seats = new();
        readonly int[] _playersPerTeam = new int[TeamCount];
        readonly int[] _slotsInUse = new int[TeamCount];
        int _nextFallback;

        public NetworkObject PlayerPrefab => _playerPrefab;
        public void SetPlayerPrefab(NetworkObject prefab) => _playerPrefab = prefab;

        void Awake()
        {
            Instance = this;
            _networkManager = GetComponentInParent<NetworkManager>();
            if (_networkManager == null)
                _networkManager = InstanceFinder.NetworkManager;

            if (_networkManager == null)
            {
                Debug.LogWarning("[TeamSpawner] No NetworkManager on this object or its parents.");
                return;
            }

            _networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
            _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (_networkManager == null)
                return;

            _networkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer)
                return;

            if (_playerPrefab == null)
            {
                Debug.LogWarning($"[TeamSpawner] No player prefab; nothing spawned for connection {conn.ClientId}.");
                return;
            }

            Seat seat = TakeSeat(conn.ClientId);
            ChooseSpawn(seat, out Vector3 position, out Quaternion rotation);

            NetworkObject player = _networkManager.GetPooledInstantiated(_playerPrefab, position, rotation, true);
            OnPlayerSpawning?.Invoke(player, conn, seat.Team, seat.Slot);
            _networkManager.ServerManager.Spawn(player, conn);
            _networkManager.SceneManager.AddOwnerToDefaultScene(player);
        }

        void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped || !_seats.Remove(conn.ClientId, out Seat seat))
                return;

            Released(seat.Team);
        }

        void Released(int team)
        {
            _playersPerTeam[team]--;
            _slotsInUse[team] = 0;
            foreach (Seat other in _seats.Values)
            {
                if (other.Team == team)
                    _slotsInUse[team] |= 1 << other.Slot;
            }
        }

        void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped)
                return;

            _seats.Clear();
            for (int team = 0; team < TeamCount; team++)
            {
                _playersPerTeam[team] = 0;
                _slotsInUse[team] = 0;
            }
        }

        /// <summary>Host only. Moves a player to the given team and returns their assigned slot.</summary>
        public int AssignTeam(int clientId, int team)
        {
            if (_seats.TryGetValue(clientId, out Seat old) && old.Team == team)
                return old.Slot;

            if (_seats.Remove(clientId, out old))
                Released(old.Team);
            return SitDown(clientId, team).Slot;
        }

        /// <summary>Host only. Applies a batch of clientId-to-team assignments without slot collisions on swaps.</summary>
        public void AssignTeams(IReadOnlyDictionary<int, int> teamByClientId)
        {
            foreach (KeyValuePair<int, int> pair in teamByClientId)
            {
                if (_seats.Remove(pair.Key, out Seat old))
                    Released(old.Team);
            }

            foreach (KeyValuePair<int, int> pair in teamByClientId)
            {
                if (pair.Value >= 0 && pair.Value < TeamCount)
                    SitDown(pair.Key, pair.Value);
            }
        }

        /// <summary>The player's slot in their team, or -1 when they have no seat.</summary>
        public int SlotOf(int clientId) => _seats.TryGetValue(clientId, out Seat seat) ? seat.Slot : -1;

        Seat TakeSeat(int clientId)
        {
            if (_seats.TryGetValue(clientId, out Seat existing))
                return existing;

            return SitDown(clientId, _playersPerTeam[1] < _playersPerTeam[0] ? 1 : 0);
        }

        Seat SitDown(int clientId, int team)
        {
            int slot = 0;
            while (slot < SlotsPerTeam && (_slotsInUse[team] & (1 << slot)) != 0)
                slot++;
            if (slot == SlotsPerTeam)
                slot = _playersPerTeam[team] % SlotsPerTeam;

            var seat = new Seat { Team = team, Slot = slot };
            _seats[clientId] = seat;
            _playersPerTeam[team]++;
            _slotsInUse[team] |= 1 << slot;
            return seat;
        }

        void ChooseSpawn(Seat seat, out Vector3 position, out Quaternion rotation)
        {
            if (SpawnOverride != null && SpawnOverride(seat.Team, seat.Slot, out position, out rotation))
                return;

            if (FallbackSpawns.Length > 0)
            {
                Transform point = FallbackSpawns[_nextFallback % FallbackSpawns.Length];
                _nextFallback++;
                position = point.position;
                rotation = point.rotation;
                return;
            }

            position = _playerPrefab.transform.position;
            rotation = _playerPrefab.transform.rotation;
        }
    }
}
