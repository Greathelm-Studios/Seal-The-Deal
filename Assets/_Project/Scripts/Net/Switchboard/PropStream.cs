using System;
using System.Collections.Generic;
using CloseTheDeal.Tower;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;

namespace CloseTheDeal.Net
{
    /// <summary>The byte payload of one host prop stream update.</summary>
    internal struct StreamPacket : IBroadcast
    {
        public ArraySegment<byte> Payload;
    }

    /// <summary>Copies packet bytes into FishNet's writer and reads them for the receiving callback.</summary>
    internal static class StreamPacketSerializers
    {
        public static void WriteStreamPacket(this Writer writer, StreamPacket value)
        {
            writer.WriteArraySegmentAndSize(value.Payload);
        }

        public static StreamPacket ReadStreamPacket(this Reader reader)
        {
            return new StreamPacket { Payload = reader.ReadArraySegmentAndSize() };
        }
    }

    /// <summary>Sends one budgeted host pose packet per client and plays received poses on clients.</summary>
    [DisallowMultipleComponent]
    public sealed class PropStream : MonoBehaviour
    {
        const int PacketCapacity = 1100;
        const int HeaderBytes = 5;
        const int EntryBytes = 13;
        const int FishNetOverheadBytes = 32;

        sealed class ClientState
        {
            public readonly NetworkConnection Connection;
            public readonly PriorityLedger Ledger;
            public byte[] Owed;
            public double Tokens;
            public double HealCredit;
            public int HealCursor;
            public bool HasDueBacklog;

            public ClientState(NetworkConnection connection, int slots)
            {
                Connection = connection;
                Ledger = new PriorityLedger(slots);
                Owed = new byte[slots];
            }

            public void EnsureSlots(int slots)
            {
                Ledger.EnsureSlots(slots);
                if (Owed.Length < slots)
                    Array.Resize(ref Owed, Math.Max(slots, Owed.Length > 0 ? Owed.Length * 2 : 32));
            }
        }

        [Tooltip("The prop stream's per-player upload limit, update rates and smoothing settings. Leave empty unless you place a PropStream in the scene yourself: normally the first prop to appear starts the stream with its own profile.")]
        [SerializeField] SwitchboardProfile _profile;

        public static PropStream Instance { get; private set; }

        /// <summary>Returns the scene's PropStream, creating one if needed when profile is supplied.</summary>
        internal static PropStream GetOrCreate(SwitchboardProfile profile)
        {
            if (Instance != null)
                return Instance;
            if (profile == null)
                return null;

            // Awake runs inside AddComponent and Start not until the next frame, so the profile is in place in time.
            var stream = new GameObject("Switchboard").AddComponent<PropStream>();
            stream._profile = profile;
            return stream;
        }

        readonly Dictionary<int, NetBody> _bodiesById = new();
        readonly List<ClientState> _clients = new();
        readonly byte[] _packet = new byte[PacketCapacity];
        NetBody[] _bodies = new NetBody[32];
        Vector3[] _positions = new Vector3[32];
        Quaternion[] _rotations = new Quaternion[32];
        FloorCell[] _cells = new FloorCell[32];
        bool[] _atRest = new bool[32];
        bool[] _captured = new bool[32];
        int[] _due = new int[32];
        byte[] _packedEntries = new byte[32 * EntryBytes];
        uint[] _packedTick = new uint[32];
        int _slotCount;
        NetworkManager _net;
        TowerBuilder _tower;
        TowerFrame _frame;
        bool _frameReady;
        bool _receiving;
        uint _lastSendTick;
        uint _newestPacketTick;
        bool _hasPacket;
        double _playbackTick;
        double _jitterSeconds;
        double _lastArrival;
        double _delayTicks;
        double _statsTime;
        int _secondBytes;
        int _secondBodies;
        int _movingBodies;

        internal double PlaybackTick => _playbackTick;
        internal double DelayTicks => _delayTicks;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Switchboard] Only one PropStream can run in a scene.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        void Start()
        {
            if (_profile == null)
            {
                Debug.LogError("[Switchboard] This PropStream has no profile. Assign Assets/_Project/Profiles/DefaultSwitchboard.asset, or remove it and let the props start the stream.", this);
                return;
            }

            _net = InstanceFinder.NetworkManager;
            if (_net == null)
                return;

            _net.TimeManager.OnPostTick += OnPostTick;
            _net.ServerManager.OnServerConnectionState += OnServerConnectionState;
            _net.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            _net.ClientManager.OnClientConnectionState += OnClientConnectionState;
            if (_net.ServerManager.Started)
                StartServer();
            if (_net.ClientManager.Started)
                StartClient();
            AttachTower();
        }

        void OnDestroy()
        {
            if (_net != null)
            {
                _net.TimeManager.OnPostTick -= OnPostTick;
                _net.ServerManager.OnServerConnectionState -= OnServerConnectionState;
                _net.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
                _net.ClientManager.OnClientConnectionState -= OnClientConnectionState;
                if (_receiving)
                    _net.ClientManager.UnregisterBroadcast<StreamPacket>(OnPacket);
            }
            if (_tower != null)
            {
                _tower.OnBuilt -= OnTowerBuilt;
                _tower.OnCleared -= OnTowerCleared;
            }
            if (Instance == this)
                Instance = null;
            SwitchboardStats.Reset();
        }

        void AttachTower()
        {
            TowerBuilder tower = TowerBuilder.Instance;
            if (tower == _tower)
                return;
            if (_tower != null)
            {
                _tower.OnBuilt -= OnTowerBuilt;
                _tower.OnCleared -= OnTowerCleared;
            }
            _tower = tower;
            _frameReady = false;
            if (_tower == null)
                return;
            _tower.OnBuilt += OnTowerBuilt;
            _tower.OnCleared += OnTowerCleared;
            if (_tower.IsBuilt)
                OnTowerBuilt();
        }

        void OnTowerBuilt()
        {
            TowerProfile towerProfile = _tower.Profile;
            int floors = _tower.FloorCount > 0
                ? _tower.FloorCount
                : towerProfile.RandomFloors +
                    (towerProfile.CheckpointEvery > 0 ? towerProfile.RandomFloors / towerProfile.CheckpointEvery : 0) + 3;
            _frame = TowerFrame.From(_tower.transform, towerProfile, floors);
            _frameReady = true;
            _hasPacket = false;
            _statsTime = Time.unscaledTimeAsDouble;
            SwitchboardStats.Active = _net != null && (_net.ServerManager.Started || _receiving);
            SwitchboardStats.IsHost = _net != null && _net.ServerManager.Started;
        }

        void OnTowerCleared()
        {
            _frameReady = false;
            _hasPacket = false;
            SwitchboardStats.Reset();
        }

        internal void Register(NetBody body)
        {
            if (_profile == null || body.Slot >= 0)
                return;
            int slot = 0;
            while (slot < _slotCount && _bodies[slot] != null)
                slot++;
            EnsureSlots(slot + 1);
            _bodies[slot] = body;
            _packedTick[slot] = 0;
            body.Slot = slot;
            if (slot == _slotCount)
                _slotCount++;
            _bodiesById[body.NetworkObject.ObjectId] = body;
            for (int i = 0; i < _clients.Count; i++)
            {
                _clients[i].EnsureSlots(_slotCount);
                _clients[i].Owed[slot] = (byte)Mathf.Clamp(_profile.RestRepeats, 0, 255);
                _clients[i].Ledger.Clear(slot);
            }
        }

        internal void Unregister(NetBody body)
        {
            int slot = body.Slot;
            if (slot < 0 || slot >= _slotCount || _bodies[slot] != body)
                return;
            _bodies[slot] = null;
            _captured[slot] = false;
            _packedTick[slot] = 0;
            body.Slot = -1;
            _bodiesById.Remove(body.NetworkObject.ObjectId);
            for (int i = 0; i < _clients.Count; i++)
            {
                _clients[i].Owed[slot] = 0;
                _clients[i].Ledger.Clear(slot);
            }
            while (_slotCount > 0 && _bodies[_slotCount - 1] == null)
                _slotCount--;
        }

        void EnsureSlots(int needed)
        {
            if (needed <= _bodies.Length)
                return;
            int size = Mathf.Max(needed, _bodies.Length * 2);
            Array.Resize(ref _bodies, size);
            Array.Resize(ref _positions, size);
            Array.Resize(ref _rotations, size);
            Array.Resize(ref _cells, size);
            Array.Resize(ref _atRest, size);
            Array.Resize(ref _captured, size);
            Array.Resize(ref _due, size);
            Array.Resize(ref _packedEntries, size * EntryBytes);
            Array.Resize(ref _packedTick, size);
        }

        void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                StartServer();
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _clients.Clear();
                SwitchboardStats.Reset();
            }
        }

        void StartServer()
        {
            foreach (NetworkConnection connection in _net.ServerManager.Clients.Values)
                AddClient(connection);
            if (_frameReady)
            {
                SwitchboardStats.Active = true;
                SwitchboardStats.IsHost = true;
            }
        }

        void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
                AddClient(connection);
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                for (int i = 0; i < _clients.Count; i++)
                {
                    if (_clients[i].Connection.ClientId != connection.ClientId)
                        continue;
                    _clients.RemoveAt(i);
                    break;
                }
            }
        }

        void AddClient(NetworkConnection connection)
        {
            if (connection.IsLocalClient)
                return;
            for (int i = 0; i < _clients.Count; i++)
                if (_clients[i].Connection.ClientId == connection.ClientId)
                    return;
            _clients.Add(new ClientState(connection, _slotCount));
        }

        void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
                StartClient();
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                if (_receiving)
                    _net.ClientManager.UnregisterBroadcast<StreamPacket>(OnPacket);
                _receiving = false;
                SwitchboardStats.Reset();
            }
        }

        void StartClient()
        {
            if (_net.ServerManager.Started || _receiving)
                return;
            _net.ClientManager.RegisterBroadcast<StreamPacket>(OnPacket);
            _receiving = true;
            // A new session counts ticks from zero again; the last one's clock must not outlive it.
            _hasPacket = false;
            _jitterSeconds = 0d;
            if (_frameReady)
                SwitchboardStats.Active = true;
        }

        void OnPostTick()
        {
            if (!_frameReady || !_net.ServerManager.Started)
                return;
            int sends = Mathf.Max(1, _profile.SendsPerSecond);
            int interval = Mathf.Max(1, Mathf.RoundToInt((float)_net.TimeManager.TickRate / sends));
            uint tick = _net.TimeManager.LocalTick;
            if (tick - _lastSendTick < interval)
                return;
            _lastSendTick = tick;

            double actualSends = (double)_net.TimeManager.TickRate / interval;
            Capture();
            for (int i = 0; i < _clients.Count; i++)
                Send(_clients[i], tick, actualSends);
            PublishStats(true);
        }

        void Capture()
        {
            _movingBodies = 0;
            byte restRepeats = (byte)Mathf.Clamp(_profile.RestRepeats, 1, 255);
            for (int slot = 0; slot < _slotCount; slot++)
            {
                NetBody body = _bodies[slot];
                if (body == null)
                    continue;
                bool forced = body.ConsumeForceSync();
                bool kinematic = body.IsKinematic;
                if (!forced && _captured[slot] && _atRest[slot] && body.IsSleeping && !kinematic)
                    continue;

                Vector3 local = _frame.ToLocal(body.Position);
                Quaternion rotation = body.Rotation;
                bool resting = (!kinematic && body.IsSleeping) || (_captured[slot] &&
                    (local - _positions[slot]).sqrMagnitude <= 0.000001f &&
                    Mathf.Abs(Quaternion.Dot(rotation, _rotations[slot])) >= 0.9999996f);
                bool wasResting = _captured[slot] && _atRest[slot];
                _positions[slot] = local;
                _rotations[slot] = rotation;
                _cells[slot] = _frame.CellOfLocal(local);
                _atRest[slot] = resting;
                _captured[slot] = true;
                if (!resting)
                    _movingBodies++;
                if (!forced && resting == wasResting)
                    continue;
                for (int i = 0; i < _clients.Count; i++)
                {
                    if (forced)
                    {
                        _clients[i].Owed[slot] = restRepeats;
                        if (!resting && wasResting)
                            _clients[i].Ledger.Clear(slot);
                    }
                    else
                    {
                        _clients[i].Owed[slot] = resting ? restRepeats : (byte)0;
                        if (!resting)
                            _clients[i].Ledger.Clear(slot);
                    }
                }
            }
        }

        void Send(ClientState client, uint tick, double sends)
        {
            NetworkConnection connection = client.Connection;
            if (!connection.IsAuthenticated || connection.IsLocalClient)
                return;

            int room = ComputePacketRoom(client, sends);
            if (room < HeaderBytes + EntryBytes)
                return;

            bool anyAccrued = AccrueClientLedger(client, connection);

            int capacity = Mathf.Min(255, (room - HeaderBytes) / EntryBytes);
            int dueCount = (anyAccrued || client.HasDueBacklog)
                ? client.Ledger.TakeDue(_slotCount, _due, capacity)
                : 0;
            client.HasDueBacklog = dueCount == capacity;
            int offset = HeaderBytes;
            int count = WriteDueBodies(client, dueCount, tick, ref offset);
            WriteHealBodies(client, dueCount, tick, room, ref offset, ref count);

            if (count == 0)
                return;
            TransmitPacket(client, connection, tick, offset, count);
        }

        int ComputePacketRoom(ClientState client, double sends)
        {
            double allowance = (double)Mathf.Max(0, _profile.BytesPerSecondPerPlayer) / sends;
            client.Tokens = Math.Min(allowance * 2d, client.Tokens + allowance);
            client.HealCredit = Math.Min(_profile.HealBodiesPerSecond,
                client.HealCredit + (double)Mathf.Max(0, _profile.HealBodiesPerSecond) / sends);
            int packetLimit = Mathf.Min(PacketCapacity,
                _net.TransportManager.GetLowestMTU((byte)Channel.Unreliable) - FishNetOverheadBytes);
            return Mathf.Min(packetLimit, (int)client.Tokens);
        }

        bool AccrueClientLedger(ClientState client, NetworkConnection connection)
        {
            NetworkObject viewer = connection.FirstObject;
            bool hasViewer = viewer != null;
            FloorCell viewerCell = hasViewer ? _frame.CellOfLocal(_frame.ToLocal(viewer.transform.position)) : default;
            bool anyAccrued = false;
            for (int slot = 0; slot < _slotCount; slot++)
            {
                if (_bodies[slot] == null)
                    continue;
                if (_atRest[slot] && client.Owed[slot] == 0)
                    continue;
                Band band = hasViewer ? FloorBands.Between(viewerCell, _cells[slot],
                    _profile.NearFloorsSameTower, _profile.NearFloorsRivalTower) : Band.Near;
                client.Ledger.Accrue(slot, band == Band.Near ? 1f : _profile.FarWeight);
                anyAccrued = true;
            }
            return anyAccrued;
        }

        int WriteDueBodies(ClientState client, int dueCount, uint tick, ref int offset)
        {
            int count = 0;
            for (int i = 0; i < dueCount; i++)
            {
                int slot = _due[i];
                if (_bodies[slot] == null)
                    continue;
                WriteBody(slot, tick, ref offset);
                count++;
                if (_atRest[slot] && client.Owed[slot] > 0)
                    client.Owed[slot]--;
            }
            return count;
        }

        void WriteHealBodies(ClientState client, int dueCount, uint tick, int room, ref int offset, ref int count)
        {
            int scanned = 0;
            while (offset + EntryBytes <= room && client.HealCredit >= 1d && scanned < _slotCount)
            {
                if (client.HealCursor >= _slotCount)
                    client.HealCursor = 0;
                int slot = client.HealCursor++;
                scanned++;
                if (_bodies[slot] == null || !_atRest[slot] || IsDueSlot(slot, dueCount))
                    continue;
                WriteBody(slot, tick, ref offset);
                count++;
                client.HealCredit -= 1d;
                if (client.Owed[slot] > 0)
                    client.Owed[slot]--;
            }
        }

        bool IsDueSlot(int slot, int dueCount)
        {
            for (int i = 0; i < dueCount; i++)
                if (_due[i] == slot)
                    return true;
            return false;
        }

        void TransmitPacket(ClientState client, NetworkConnection connection, uint tick, int offset, int count)
        {
            _packet[0] = (byte)tick;
            _packet[1] = (byte)(tick >> 8);
            _packet[2] = (byte)(tick >> 16);
            _packet[3] = (byte)(tick >> 24);
            _packet[4] = (byte)count;
            _net.ServerManager.Broadcast(connection,
                new StreamPacket { Payload = new ArraySegment<byte>(_packet, 0, offset) },
                true, Channel.Unreliable);
            client.Tokens -= offset + FishNetOverheadBytes;
            _secondBytes += offset;
            _secondBodies += count;
        }

        void WriteBody(int slot, uint tick, ref int offset)
        {
            int baseIndex = slot * EntryBytes;
            if (_packedTick[slot] != tick)
            {
                _packedTick[slot] = tick;
                int packOffset = baseIndex;
                int id = _bodies[slot].NetworkObject.ObjectId;
                _packedEntries[packOffset++] = (byte)id;
                _packedEntries[packOffset++] = (byte)(id >> 8);
                _packedEntries[packOffset++] = _atRest[slot] ? (byte)1 : (byte)0;
                PoseCodec.Write(_packedEntries, ref packOffset, in _frame.LocalBounds, _positions[slot], _rotations[slot]);
            }

            Buffer.BlockCopy(_packedEntries, baseIndex, _packet, offset, EntryBytes);
            offset += EntryBytes;
        }

        void OnPacket(StreamPacket message, Channel channel)
        {
            if (!_frameReady || !_receiving || channel != Channel.Unreliable)
                return;
            ArraySegment<byte> segment = message.Payload;
            if (segment.Array == null || segment.Count < HeaderBytes)
                return;
            byte[] bytes = segment.Array;
            int offset = segment.Offset;
            int end = offset + segment.Count;
            uint tick = (uint)(bytes[offset] | bytes[offset + 1] << 8 |
                bytes[offset + 2] << 16 | bytes[offset + 3] << 24);
            int count = bytes[offset + 4];
            if (HeaderBytes + count * EntryBytes != segment.Count)
                return;

            RecordPacketTiming(tick);
            ApplyPacketBodies(bytes, offset + HeaderBytes, end, tick, count);

            _secondBytes += segment.Count;
            _secondBodies += count;
            PublishStats(false);
        }

        void RecordPacketTiming(uint tick)
        {
            if (_hasPacket && tick <= _newestPacketTick)
                return;
            double now = Time.unscaledTimeAsDouble;
            if (_hasPacket)
            {
                double expected = (double)(tick - _newestPacketTick) / _net.TimeManager.TickRate;
                double deviation = Math.Abs(now - _lastArrival - expected);
                _jitterSeconds += (deviation - _jitterSeconds) * 0.1d;
            }
            _lastArrival = now;
            _newestPacketTick = tick;
            UpdateDelay();
            if (!_hasPacket)
                _playbackTick = tick - _delayTicks;
            _hasPacket = true;
        }

        void ApplyPacketBodies(byte[] bytes, int offset, int end, uint tick, int count)
        {
            for (int i = 0; i < count && offset + EntryBytes <= end; i++)
            {
                int id = bytes[offset] | bytes[offset + 1] << 8;
                bool atRest = (bytes[offset + 2] & 1) != 0;
                offset += 3;
                if (_bodiesById.TryGetValue(id, out NetBody body) && (!body.HasNewest || tick > body.NewestTick))
                {
                    PoseCodec.Read(bytes, ref offset, in _frame.LocalBounds, out Vector3 local, out Quaternion rotation);
                    body.Push(tick, _frame.ToWorld(local), rotation, atRest);
                }
                else
                {
                    offset += PoseCodec.PoseBytes;
                }
            }
        }

        double DelayTicksForCurrentSettings()
        {
            int sends = Mathf.Max(1, _profile.SendsPerSecond);
            double milliseconds = 2000d / sends + 2000d * _jitterSeconds;
            milliseconds = Math.Max(_profile.SmoothingDelayFloorMs,
                Math.Min(_profile.SmoothingDelayCeilingMs, milliseconds));
            return milliseconds * _net.TimeManager.TickRate / 1000d;
        }

        void UpdateDelay()
        {
            _delayTicks = DelayTicksForCurrentSettings();
            SwitchboardStats.DelayMs = Mathf.RoundToInt((float)(_delayTicks * 1000d / _net.TimeManager.TickRate));
        }

        void Update()
        {
            if (_profile == null || _net == null)
                return;
            if (!_frameReady)
            {
                if (_tower == null)
                    AttachTower();
                return;
            }
            if (_receiving && _hasPacket)
            {
                double rate = _net.TimeManager.TickRate;
                _playbackTick += Time.unscaledDeltaTime * rate;
                double target = _newestPacketTick - _delayTicks;
                double error = target - _playbackTick;
                if (Math.Abs(error) > rate * 0.5d)
                    _playbackTick = target;
                else
                    _playbackTick += error * Math.Min(1d, Time.unscaledDeltaTime * 0.1d);
            }
            PublishStats(_net.ServerManager.Started);
        }

        void PublishStats(bool host)
        {
            double now = Time.unscaledTimeAsDouble;
            if (now - _statsTime < 1d)
                return;
            SwitchboardStats.Active = _frameReady && (host || _receiving);
            SwitchboardStats.IsHost = host;
            SwitchboardStats.BytesPerSecond = _secondBytes;
            SwitchboardStats.BodiesPerSecond = _secondBodies;
            SwitchboardStats.MovingBodies = host ? _movingBodies : CountPlaying();
            if (host)
                SwitchboardStats.DelayMs = 0;
            _secondBytes = 0;
            _secondBodies = 0;
            _statsTime = now;
        }

        int CountPlaying()
        {
            int count = 0;
            for (int i = 0; i < _slotCount; i++)
                if (_bodies[i] != null && _bodies[i].enabled)
                    count++;
            return count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }
    }
}
