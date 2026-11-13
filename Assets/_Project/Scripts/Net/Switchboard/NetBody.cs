using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.Net
{
    /// <summary>Lets the host stream a prop's motion and a client play that motion smoothly.</summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class NetBody : NetworkBehaviour
    {
        const int PoseCapacity = 8;

        struct Pose
        {
            public uint Tick;
            public Vector3 Position;
            public Quaternion Rotation;
            public bool AtRest;
        }

        [Tooltip("The prop stream's settings: upload limit per player, update rates and smoothing. Every prop should point at the same asset; the first prop to appear starts the stream with it.")]
        [SerializeField] SwitchboardProfile _profile;

        readonly Pose[] _poses = new Pose[PoseCapacity];
        Rigidbody _body;
        PropStream _stream;
        int _poseCount;
        int _poseHead;
        bool _hasNewest;
        uint _newestTick;
        Vector3 _spawnPosition;
        Quaternion _spawnRotation;

        internal int Slot { get; set; } = -1;
        internal Vector3 Position => transform.position;
        internal Quaternion Rotation => transform.rotation;
        internal bool IsSleeping => _body.IsSleeping();
        internal uint NewestTick => _newestTick;
        internal bool HasNewest => _hasNewest;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            enabled = false;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _body.isKinematic = !IsServerStarted;
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _poseCount = 0;
            _poseHead = 0;
            _hasNewest = false;

            if (GetComponent<NetworkTransform>() != null)
            {
                Debug.LogWarning("[Switchboard] This prop has both NetBody and NetworkTransform, so Switchboard is leaving it alone. Remove the NetworkTransform from its prefab.", this);
                return;
            }

            _stream = PropStream.GetOrCreate(_profile);
            if (_stream == null)
            {
                Debug.LogWarning("[Switchboard] This prop's NetBody has no profile, so it will not move on other players' screens. Assign Assets/_Project/Profiles/DefaultSwitchboard.asset on its prefab.", this);
                return;
            }

            _stream.Register(this);
        }

        public override void OnStopNetwork()
        {
            if (_stream != null)
                _stream.Unregister(this);
            _stream = null;
            _poseCount = 0;
            _poseHead = 0;
            _hasNewest = false;
            enabled = false;
            base.OnStopNetwork();
        }

        /// <summary>Adds a host pose, keeping the newest eight in tick order.</summary>
        internal void Push(uint tick, Vector3 position, Quaternion rotation, bool atRest)
        {
            if (IsServerStarted || (_hasNewest && tick <= _newestTick))
                return;

            if (!_hasNewest && (position - _spawnPosition).sqrMagnitude > 4f)
            {
                transform.SetPositionAndRotation(position, rotation);
            }
            else if (!_hasNewest)
            {
                uint glideTicks = (uint)Mathf.CeilToInt((float)_stream.DelayTicks);
                _poses[_poseHead] = new Pose
                {
                    Tick = tick > glideTicks ? tick - glideTicks : 0,
                    Position = _spawnPosition,
                    Rotation = _spawnRotation
                };
                _poseCount = 1;
            }

            if (_poseCount == PoseCapacity)
            {
                _poseHead = (_poseHead + 1) % PoseCapacity;
                _poseCount--;
            }

            _poses[(_poseHead + _poseCount++) % PoseCapacity] = new Pose
            {
                Tick = tick,
                Position = position,
                Rotation = rotation,
                AtRest = atRest
            };
            _newestTick = tick;
            _hasNewest = true;
            enabled = true;
        }

        void Update()
        {
            if (_stream == null || _poseCount == 0)
            {
                enabled = false;
                return;
            }

            double clock = _stream.PlaybackTick;
            Pose oldest = PoseAt(0);
            if (clock <= oldest.Tick)
            {
                transform.SetPositionAndRotation(oldest.Position, oldest.Rotation);
                return;
            }

            for (int i = 1; i < _poseCount; i++)
            {
                Pose newer = PoseAt(i);
                if (clock > newer.Tick)
                    continue;

                Pose older = PoseAt(i - 1);
                double segmentStart = System.Math.Max(older.Tick, newer.Tick - _stream.DelayTicks);
                float fraction = segmentStart >= newer.Tick ? 1f :
                    Mathf.Clamp01((float)((clock - segmentStart) / (newer.Tick - segmentStart)));
                transform.SetPositionAndRotation(Vector3.Lerp(older.Position, newer.Position, fraction),
                    Quaternion.Slerp(older.Rotation, newer.Rotation, fraction));
                return;
            }

            Pose latest = PoseAt(_poseCount - 1);
            transform.SetPositionAndRotation(latest.Position, latest.Rotation);
            enabled = false;
        }

        Pose PoseAt(int index)
        {
            return _poses[(_poseHead + index) % PoseCapacity];
        }
    }
}
