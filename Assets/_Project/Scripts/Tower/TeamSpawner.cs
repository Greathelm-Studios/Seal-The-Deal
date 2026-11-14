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
    /// <summary>
    /// Spawns each joining player in their team's lobby. Replaces FishNet's PlayerSpawner on
    /// the NetworkManager. Host only: the host decides teams and positions. A joiner goes to
    /// the team with fewer players, team A on a tie, so the host is team A and joiners
    /// alternate B, A, B until someone leaves. The team reaches clients through whoever
    /// listens to OnPlayerSpawning (the match's FirmMember syncs it).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TeamSpawner : MonoBehaviour
    {
        /// <summary>Lets a game mode place a joining player somewhere other than the lobby. Return false to use the lobby.</summary>
        public delegate bool SpawnChooser(int team, int slot, out Vector3 position, out Quaternion rotation);

        /// <summary>Host only. Raised just before a player is spawned: the player, their connection, their team and their slot in it.</summary>
        public static event Action<NetworkObject, NetworkConnection, int, int> OnPlayerSpawning;

        /// <summary>Host only. Asked first for where a joining player appears; null or false means the team lobby.</summary>
        public static SpawnChooser SpawnOverride;

        /// <summary>The spawner on the NetworkManager, or null when there is none.</summary>
        public static TeamSpawner Instance { get; private set; }

        [Tooltip("The networked player prefab.")]
        [SerializeField] NetworkObject _playerPrefab;

        [Tooltip("Tick to spawn in the team lobbies of the tower. Untick to use the fallback spawns on the flat test area instead.")]
        [SerializeField] bool _spawnInTower = true;

        [Tooltip("Used when there is no tower, or Spawn In Tower is off. Cycled in order.")]
        public Transform[] FallbackSpawns = new Transform[0];

        struct Seat
        {
            public int Team;
            public int Slot;
        }

        const int SlotsPerTeam = 4;

        NetworkManager _networkManager;
        readonly Dictionary<int, Seat> _seats = new();
        readonly int[] _playersPerTeam = new int[TowerBuilder.TeamCount];
        readonly int[] _slotsInUse = new int[TowerBuilder.TeamCount];
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

        /// <summary>A leaver frees their place, so the next joiner evens the teams up again.</summary>
        void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped || !_seats.Remove(conn.ClientId, out Seat seat))
                return;

            Released(seat.Team);
        }

        /// <summary>One fewer in the team; the slots still in use are worked out again from who is seated, so a slot two players share is never freed under one of them.</summary>
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

        /// <summary>The host stopped: nobody is seated any more, so the next session starts from team A again.</summary>
        void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped)
                return;

            _seats.Clear();
            for (int team = 0; team < TowerBuilder.TeamCount; team++)
            {
                _playersPerTeam[team] = 0;
                _slotsInUse[team] = 0;
            }
        }

        /// <summary>
        /// Host only. Moves a connected player to the given team, as a lobby that chose the teams
        /// decides, keeping the head counts the next joiner is balanced against. Returns their
        /// slot in the new team (which picks their spawn point).
        /// </summary>
        public int AssignTeam(int clientId, int team)
        {
            if (_seats.TryGetValue(clientId, out Seat old) && old.Team == team)
                return old.Slot;

            if (_seats.Remove(clientId, out old))
                Released(old.Team);
            return SitDown(clientId, team).Slot;
        }

        /// <summary>
        /// Host only. Seats a whole lobby's choice at once (client id → team): everyone listed
        /// leaves their seat first, then sits down in their team, so two players swapping teams
        /// can never be handed the same slot. Players not listed keep their seats.
        /// </summary>
        public void AssignTeams(IReadOnlyDictionary<int, int> teamByClientId)
        {
            foreach (KeyValuePair<int, int> pair in teamByClientId)
            {
                if (_seats.Remove(pair.Key, out Seat old))
                    Released(old.Team);
            }

            foreach (KeyValuePair<int, int> pair in teamByClientId)
            {
                if (pair.Value >= 0 && pair.Value < TowerBuilder.TeamCount)
                    SitDown(pair.Key, pair.Value);
            }
        }

        /// <summary>The player's slot in their team, or -1 when they have no seat.</summary>
        public int SlotOf(int clientId) => _seats.TryGetValue(clientId, out Seat seat) ? seat.Slot : -1;

        /// <summary>The team with fewer players (A on a tie) and the lowest free slot in it.</summary>
        Seat TakeSeat(int clientId)
        {
            if (_seats.TryGetValue(clientId, out Seat existing))
                return existing;

            return SitDown(clientId, _playersPerTeam[1] < _playersPerTeam[0] ? 1 : 0);
        }

        Seat SitDown(int clientId, int team)
        {
            // The lowest free slot; a fifth player in a team (only possible when a lobby chooses
            // the teams) shares a spawn point, picked round-robin, rather than corrupting the set.
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

            TowerBuilder tower = TowerBuilder.Instance;
            if (_spawnInTower && tower != null && tower.TryGetSpawn(seat.Team, seat.Slot, out position, out rotation))
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
