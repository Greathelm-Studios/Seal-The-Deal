using System;
using CloseTheDeal.Items;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Integration
{
    /// <summary>Host-only guns lying at fixed spots in a scene with no tower, put back after they are taken. Design: docs/systems/ITEMS.md.</summary>
    [DisallowMultipleComponent]
    public sealed class LooseGunSpawns : NetworkBehaviour
    {
        [Serializable]
        public struct Spot
        {
            [Tooltip("Where the gun lies: its position and facing. Move or turn this object to move the gun.")]
            public Transform Place;

            [Tooltip("Which gun lies here. Must be a gun listed in the gun pickup map.")]
            public ItemDefinition Gun;
        }

        [Tooltip("Which gun pickup is which prefab. Read at runtime, never written.")]
        [SerializeField] GunPickupMap _map;

        [Tooltip("Where guns lie when the match starts.")]
        [SerializeField] Spot[] _spots = new Spot[0];

        [Tooltip("Seconds after a gun is taken before a new one appears at its spot. 0 = never put back.")]
        [Min(0f)] [SerializeField] float _respawnSeconds = 20f;

        [Tooltip("How far above the spot the gun is dropped so it settles on the floor, in metres.")]
        [Min(0f)] [SerializeField] float _dropHeight = 0.15f;

        struct SpotState
        {
            public NetworkObject Gun;
            public float SpawnAt;
            public bool Waiting;
        }

        SpotState[] _state = new SpotState[0];

        /// <summary>The spawner in the scene, or null. Lets a swapped-out gun be dropped with no FloorLoot.</summary>
        public static LooseGunSpawns Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            Instance = this;
            enabled = false;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _state = new SpotState[_spots.Length];
            for (int i = 0; i < _state.Length; i++)
                _state[i].Waiting = true;
            enabled = true;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            enabled = false;
            _state = new SpotState[0];
        }

        void Update()
        {
            if (!IsServerStarted)
                return;

            for (int i = 0; i < _state.Length; i++)
            {
                ref SpotState state = ref _state[i];
                if (state.Gun != null && state.Gun.IsSpawned)
                    continue;

                if (!state.Waiting)
                {
                    state.Gun = null;
                    if (_respawnSeconds <= 0f)
                        continue;
                    state.Waiting = true;
                    state.SpawnAt = Time.time + _respawnSeconds;
                }

                if (state.Waiting && Time.time >= state.SpawnAt)
                {
                    state.Waiting = false;
                    state.Gun = SpawnAtSpot(_spots[i]);
                    if (state.Gun == null && _respawnSeconds > 0f)
                    {
                        state.Waiting = true;
                        state.SpawnAt = Time.time + _respawnSeconds;
                    }
                }
            }
        }

        NetworkObject SpawnAtSpot(Spot spot)
        {
            NetworkObject prefab = _map != null && spot.Gun != null ? _map.PickupForItem(spot.Gun) : null;
            if (prefab == null || spot.Place == null)
            {
                Debug.LogWarning("[Integration] A loose gun spot has no place, or its gun has no pickup in the gun pickup map; nothing is put there.");
                return null;
            }

            spot.Place.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
            return Spawn(prefab, position + Vector3.up * _dropHeight, rotation);
        }

        NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation)
        {
            NetworkObject thing = NetworkManager.GetPooledInstantiated(prefab, position, rotation, true);
            ServerManager.Spawn(thing);
            return thing;
        }

        /// <summary>Host only. Puts a loose gun down mid-match (a swapped-out one). It is not put back.</summary>
        public NetworkObject ServerSpawnLoose(NetworkObject prefab, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (!IsServerStarted || prefab == null)
                return null;

            NetworkObject thing = Spawn(prefab, position, rotation);
            if (thing.TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = velocity;
                body.WakeUp();
            }

            return thing;
        }
    }
}
