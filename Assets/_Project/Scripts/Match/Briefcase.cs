using CloseTheDeal.Player;
using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>Host-simulated objective briefcase with local carry visuals, firm tinting, and beacon. Design: docs/systems/MATCH.md.</summary>
    [DefaultExecutionOrder(200)] // After the player camera has moved, so the carrier's own view never lags a frame.
    [RequireComponent(typeof(Rigidbody), typeof(NetworkTransform))]
    public sealed class Briefcase : NetworkBehaviour
    {
        [Tooltip("The part that is seen. It leaves the body to follow the carrier's hand and comes back when dropped.")]
        [SerializeField] Transform _visual;

        [Tooltip("The parts tinted to show who holds it: your firm's colour, the rival's, or the loose colour from the match profile.")]
        [SerializeField] Renderer[] _firmMarkers = new Renderer[0];

        static readonly Quaternion FirstPersonTiltRight = Quaternion.Euler(0f, 75f, 8f);
        static readonly Quaternion FirstPersonTiltLeft = Quaternion.Euler(0f, -75f, -8f);

        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        static readonly int Fade = Shader.PropertyToID("_Fade");

        const float BeaconWidth = 0.6f;
        const float PopScale = 0.18f;
        const float RivalPulseRate = 1.5f;
        const float RivalPulseLow = 0.35f;
        const float PopSeconds = 0.18f;
        const float BeaconFadeBand = 0.25f;

        /// <summary>The match's briefcase on this machine, or null before it has spawned.</summary>
        public static Briefcase Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
        }

        Rigidbody _rigidbody;
        Collider _collider;
        NetworkTransform _networkTransform;
        MaterialPropertyBlock _block;
        Vector3 _centreLocal;
        Camera _camera;

        float _popStarted = -1f;
        float _handScale = 1f;
        Renderer _beacon;
        MaterialPropertyBlock _beaconBlock;
        float _shownFade = -1f;

        BriefcaseState _state;
        BriefcaseStatus _status;
        byte _tintFirm = MatchDirector.NoFirm;
        FirmMember _carrier;
        int _carrierId;
        float _carryStarted;
        Vector3 _blendFromPosition;
        Quaternion _blendFromRotation;

        internal int StrayTicks;
        internal Vector3 LastCarrierPosition;

        /// <summary>World-space centre of the case.</summary>
        public Vector3 Centre => transform.TransformPoint(_centreLocal);

        public Vector3 Position => _rigidbody.position;

        internal bool Resting => _rigidbody.IsSleeping() || _rigidbody.linearVelocity.sqrMagnitude < 0.04f;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _networkTransform = GetComponent<NetworkTransform>();
            _block = new MaterialPropertyBlock();
            // Read once while the collider is on: a switched-off collider reports empty bounds.
            _centreLocal = transform.InverseTransformPoint(_collider.bounds.center);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            StrayTicks = 0;
            Register();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // Only the host runs its physics; clients are moved by the synced transform.
            _rigidbody.isKinematic = !IsServerStarted;
            FirmMember.OnLocalChanged += RefreshTint;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Register();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            FirmMember.OnLocalChanged -= RefreshTint;
            if (Current == this)
                Current = null;
        }

        internal void Register()
        {
            Current = this;
            MatchDirector match = MatchDirector.Instance;
            EnsureBeacon(match);
            if (match != null && match.IsLive)
                Apply(match.CaseStatus);
        }

        internal void Apply(BriefcaseStatus status)
        {
            BriefcaseState previous = _state;
            _state = status.State;
            _status = status;
            bool handsChanged = (previous == BriefcaseState.Carried) != (status.State == BriefcaseState.Carried);
            if (handsChanged)
                _popStarted = Time.unscaledTime;
            _collider.enabled = status.State == BriefcaseState.Dropped;
            _tintFirm = status.State == BriefcaseState.Carried ? status.CarrierFirm : MatchDirector.NoFirm;
            RefreshTint();
            RefreshBeaconColour();

            if (status.State == BriefcaseState.Carried)
            {
                if (previous != BriefcaseState.Carried || _carrierId != status.CarrierId)
                    BeginCarry(status.CarrierId);
                return;
            }

            _carrier = null;
            _handScale = 1f;
            _visual.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (_popStarted < 0f)
                _visual.localScale = Vector3.one;

            // The host already moved the body. A client jumps it to where it was let go, so it
            // never shows for a frame at the spot it was picked up from while the transform catches up.
            if (!IsServerStarted && status.State == BriefcaseState.Dropped && previous != BriefcaseState.Dropped)
                transform.position = status.Position;
        }

        void BeginCarry(int carrierId)
        {
            _carrierId = carrierId;
            _carrier = FirmMember.Find(carrierId);
            _carryStarted = Time.time;
            _visual.GetPositionAndRotation(out _blendFromPosition, out _blendFromRotation);
        }

        void LateUpdate()
        {
            if (_state == BriefcaseState.Carried)
                FollowCarrier();
            if (_popStarted >= 0f)
                PopStep();
            UpdateBeacon();
        }

        void FollowCarrier()
        {
            if (_carrier == null)
            {
                // The carrier's own spawn can reach a client a moment after the pick-up does.
                _carrier = FirmMember.Find(_carrierId);
                if (_carrier == null)
                    return;
            }

            HandPose(out Vector3 position, out Quaternion rotation);
            MatchDirector match = MatchDirector.Instance;
            float blend = match != null ? match.Profile.CarryBlendSeconds : 0f;
            float t = blend > 0f ? Mathf.Clamp01((Time.time - _carryStarted) / blend) : 1f;
            if (t < 1f)
            {
                float eased = 1f - (1f - t) * (1f - t); // ease-out: fast off the floor, soft into the hand
                position = Vector3.Lerp(_blendFromPosition, position, eased);
                rotation = Quaternion.Slerp(_blendFromRotation, rotation, eased);
            }

            _visual.SetPositionAndRotation(position, rotation);
            if (_popStarted < 0f)
                _visual.localScale = Vector3.one * Mathf.Lerp(1f, _handScale, t);
        }

        void PopStep()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _popStarted) / PopSeconds);
            float amount = PopScale * Mathf.Sin(t * Mathf.PI) * (1f - t * 0.5f);
            _visual.localScale = new Vector3(1f + amount, 1f - amount * 0.6f, 1f + amount) * _handScale;
            if (t < 1f)
                return;

            _visual.localScale = Vector3.one * _handScale;
            _popStarted = -1f;
        }

        void EnsureBeacon(MatchDirector match)
        {
            if (_beacon != null || match == null || match.Profile == null || match.Profile.BeaconMaterial == null)
                return;

            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Quad);
            column.name = "Beacon";
            // DestroyImmediate so a concave mesh collider never sits on the moving rigidbody even for one frame.
            DestroyImmediate(column.GetComponent<Collider>());
            column.transform.SetParent(transform, false);
            _beacon = column.GetComponent<Renderer>();
            _beacon.sharedMaterial = match.Profile.BeaconMaterial;
            _beacon.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _beacon.receiveShadows = false;
            _beaconBlock = new MaterialPropertyBlock();
            RefreshBeaconColour();
        }

        void RefreshBeaconColour()
        {
            MatchDirector match = MatchDirector.Instance;
            if (_beacon == null || match == null)
                return;

            Color colour = _tintFirm == MatchDirector.NoFirm ? match.Profile.BeaconLooseColour : match.ColourFor(_tintFirm);
            _beaconBlock.Clear();
            _beacon.GetPropertyBlock(_beaconBlock);
            _beaconBlock.SetColor(BaseColour, colour);
            _beaconBlock.SetFloat(Fade, Mathf.Max(0f, _shownFade));
            _beacon.SetPropertyBlock(_beaconBlock);
        }

        void UpdateBeacon()
        {
            MatchDirector match = MatchDirector.Instance;
            Camera view = ResolveCamera();
            if (_beacon == null || match == null || view == null)
                return;

            Vector3 foot = _visual.position;
            float fade = BeaconFade(match, view.transform.position, foot) * RivalPulse();
            _beacon.enabled = fade > 0f;
            if (!_beacon.enabled)
                return;

            float height = match.Profile.BeaconHeight;
            Vector3 toView = view.transform.position - foot;
            toView.y = 0f;
            Quaternion facing = toView.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(-toView) : Quaternion.identity;
            _beacon.transform.SetPositionAndRotation(foot + Vector3.up * (height * 0.5f), facing);
            _beacon.transform.localScale = new Vector3(BeaconWidth, height, 1f);

            if (Mathf.Abs(fade - _shownFade) < 0.02f)
                return;

            _shownFade = fade;
            _beaconBlock.Clear();
            _beacon.GetPropertyBlock(_beaconBlock);
            _beaconBlock.SetFloat(Fade, fade);
            _beacon.SetPropertyBlock(_beaconBlock);
        }

        Camera ResolveCamera()
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;
            return _camera;
        }

        float RivalPulse()
        {
            FirmMember local = FirmMember.Local;
            if (_tintFirm == MatchDirector.NoFirm || local == null || local.Firm == _tintFirm)
                return 1f;

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * RivalPulseRate * 2f * Mathf.PI);
            return Mathf.Lerp(RivalPulseLow, 1f, wave);
        }

        float BeaconFade(MatchDirector match, Vector3 viewer, Vector3 foot)
        {
            bool mine = _state == BriefcaseState.Carried && _carrier != null && _carrier == FirmMember.Local;
            if (!match.IsLive || mine)
                return 0f;
            if (MatchDirector.Ruleset != null && MatchDirector.Ruleset.BriefcaseHidden(_status))
                return 0f;

            float range = match.Profile.BeaconRange;
            return Mathf.Clamp01((range - Vector3.Distance(viewer, foot)) / (range * BeaconFadeBand));
        }

        void HandPose(out Vector3 position, out Quaternion rotation)
        {
            PlayerCamera view = PlayerCamera.Instance;
            MatchDirector match = MatchDirector.Instance;
            if (_carrier == FirmMember.Local && view != null && match != null)
            {
                Transform eye = view.transform;
                position = eye.TransformPoint(match.Profile.FirstPersonOffset);
                rotation = eye.rotation * (match.Profile.FirstPersonOffset.x < 0f ? FirstPersonTiltLeft : FirstPersonTiltRight);
                _handScale = match.Profile.FirstPersonScale;
                return;
            }

            _carrier.CarryAnchor.GetPositionAndRotation(out position, out rotation);
            _handScale = 1f;
        }

        void RefreshTint()
        {
            MatchDirector match = MatchDirector.Instance;
            if (match == null || match.Profile == null)
                return;

            Color colour = match.ColourFor(_tintFirm);
            foreach (Renderer marker in _firmMarkers)
            {
                if (marker == null)
                    continue;

                _block.Clear();
                marker.GetPropertyBlock(_block);
                _block.SetColor(BaseColour, colour);
                marker.SetPropertyBlock(_block);
            }
        }

        internal void ServerPark()
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
            StrayTicks = 0;
        }

        internal void ServerRelease(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            // Teleport() prevents NetworkTransform interpolation from the parked spot.
            _networkTransform.Teleport();
            transform.SetPositionAndRotation(position, rotation);
            _rigidbody.position = position;
            _rigidbody.rotation = rotation;
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = velocity;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.WakeUp();
            StrayTicks = 0;
        }

        /// <summary>Host only. Applies a velocity impulse to a loose briefcase.</summary>
        public void Throw(Vector3 velocityChange)
        {
            if (!IsServerStarted || _rigidbody.isKinematic)
                return;

            _rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
        }
    }
}
