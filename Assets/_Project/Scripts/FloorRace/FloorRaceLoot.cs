using System.Collections.Generic;
using CloseTheDeal.FloorGen;
using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.FloorRace
{
    /// <summary>Host only. Spawns a floor's weapons, gadgets and loose props at the layout's authored spots and despawns them when the floor is left.</summary>
    public sealed class FloorRaceLoot
    {
        const uint LootStream = 0x4C4F4F54u;

        readonly NetworkManager _network;
        readonly FloorRaceProfile _profile;
        readonly List<NetworkObject> _spawned = new();

        public int SpawnedCount => _spawned.Count;

        public FloorRaceLoot(NetworkManager network, FloorRaceProfile profile)
        {
            _network = network;
            _profile = profile;
        }

        public void SpawnFloor(BuiltFloor floor)
        {
            FloorLayout layout = floor.Layout;
            var rng = new FloorRandom(layout.Seed, (uint)layout.Floor, LootStream);
            int totalWeight = 0;
            foreach (FloorRaceProfile.LootEntry entry in _profile.Loot)
                if (entry.Prefab != null)
                    totalWeight += entry.Weight;

            int loot = totalWeight > 0 ? Mathf.Min(_profile.LootPerFloor, layout.LootSpots.Count) : 0;
            for (int i = 0; i < loot; i++)
            {
                FloorSpot spot = layout.LootSpots[i];
                NetworkObject prefab = Pick(rng.Range(0, totalWeight));
                Spawn(prefab, floor.World(spot, _profile.DropHeight), Quaternion.Euler(0f, rng.Range(0, 360), 0f));
            }

            foreach (PropSpot spot in layout.PropSpots)
            {
                NetworkObject prefab = spot.Kind switch
                {
                    PropKind.Chair => _profile.ChairProp,
                    PropKind.Monitor => _profile.MonitorProp,
                    _ => _profile.CabinetProp
                };
                if (prefab != null)
                    Spawn(prefab, floor.World(spot.X, spot.Y, spot.Height + 0.05f), floor.Root.transform.rotation * Quaternion.Euler(0f, spot.Yaw, 0f));
            }
        }

        /// <summary>Spawns something loose mid-floor (a swapped-out gun); it is despawned with the floor.</summary>
        public void SpawnLoose(NetworkObject prefab, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            NetworkObject thing = Spawn(prefab, position, rotation);
            if (thing != null && thing.TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = velocity;
                body.WakeUp();
            }
        }

        public void DespawnAll()
        {
            foreach (NetworkObject thing in _spawned)
                if (thing != null && thing.IsSpawned)
                    _network.ServerManager.Despawn(thing);
            _spawned.Clear();
        }

        NetworkObject Pick(int roll)
        {
            foreach (FloorRaceProfile.LootEntry entry in _profile.Loot)
            {
                if (entry.Prefab == null)
                    continue;
                roll -= entry.Weight;
                if (roll < 0)
                    return entry.Prefab;
            }

            return null;
        }

        NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return null;

            NetworkObject thing = _network.GetPooledInstantiated(prefab, position, rotation, true);
            _network.ServerManager.Spawn(thing);
            _spawned.Add(thing);
            return thing;
        }
    }
}
