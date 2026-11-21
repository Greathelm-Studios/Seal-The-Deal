using CloseTheDeal.Player;
using FishNet.Component.Transforming;
using FishNet.Object;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// The one briefcase of the match: the objective either firm can carry. The host simulates
    /// it while it lies loose; everyone else holds it still and follows the host through
    /// NetworkTransform, like furniture. While carried the networked body stays parked where
    /// it was picked up, switched off, and only the visual follows the carrier on each
    /// machine, so carrying costs no traffic. Its state lives on the MatchDirector; this only
    /// shows it. Design: docs/systems/MATCH.md.
    /// </summary>
    [DefaultExecutionOrder(200)] // After the player camera has moved, so the carrier's own view never lags a frame.
    [RequireComponent(typeof(Rigidbody), typeof(NetworkTransform))]
    public sealed class Briefcase : NetworkBehaviour
    {
        [Tooltip("The part that is seen. It leaves the body to follow the carrier's hand and comes back when dropped.")]
        [SerializeField] Transform _visual;

        [Tooltip("The parts tinted to show who holds it: your firm's colour, the rival's, or the loose colour from the match profile.")]
        [SerializeField] Renderer[] _firmMarkers = new Renderer[0];

        /// <summary>Turned side-on and leaning in toward the middle of the view; mirrored when the case sits on the left.</summary>
        static readonly Quaternion FirstPersonTiltRight = Quaternion.Euler(0f, 75f, 8f);
        static readonly Quaternion FirstPersonTiltLeft = Quaternion.Euler(0f, -75f, -8f);

        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        static readonly int Fade = Shader.PropertyToID("_Fade");

        /// <summary>The beacon's column is this wide, metres.</summary>
        const float BeaconWidth = 0.6f;

        /// <summary>Picked up or let go, the case squashes a little and settles: how much bigger it starts, and how long it takes, seconds.</summary>
        const float PopScale = 0.18f;

        /// <summary>The rival-held beacon breathes this many times a second, down to this fraction of full strength.</summary>
        const float RivalPulseRate = 1.5f;
        const float RivalPulseLow = 0.35f;
        const float PopSeconds = 0.18f;

        /// <summary>Over the last this-fraction of the beacon range it fades out rather than popping.</summary>
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
        byte _tintFirm = MatchDirector.NoFirm;
        FirmMember _carrier;
        int _carrierId;
        float _carryStarted;
        Vector3 _blendFromPosition;
        Quaternion _blendFromRotation;

        /// <summary>Host only: ticks it has lain outside the buildings, plaza and skybridges in a row.</summary>
        internal int StrayTicks;

        /// <summary>Host only: the tick it comes back.</summary>
        internal uint ReturnAtTick;

        /// <summary>Host only: where its carrier last stood, in case the carrier vanishes without a word.</summary>
        internal Vector3 LastCarrierPosition;

        /// <summary>The centre of the case as it lies in the world.</summary>
        public Vector3 Centre => transform.TransformPoint(_centreLocal);

        public Vector3 Position => _rigidbody.position;

        /// <summary>Host: lying still (asleep, or barely moving), not in flight.</summary>
        internal bool Resting => _rigidbody.IsSleeping() || _rigidbody.linearVelocity.sqrMagnitude < 0.04f;

        // ---- Lifecycle -----------------------------------------------------------------------

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
            ReturnAtTick = 0;
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

        /// <summary>Makes itself the match's briefcase and takes up whatever state the match already has.</summary>
        internal void Register()
        {
            Current = this;
            MatchDirector match = MatchDirector.Instance;
            EnsureBeacon(match);
            if (match != null && match.IsLive)
                Apply(match.CaseStatus);
        }

        // ---- Showing the state (every machine) -----------------------------------------------

        /// <summary>Takes up a new state from the match: where it is, whether it can be touched, who holds it.</summary>
        internal void Apply(BriefcaseStatus status)
        {
            BriefcaseState previous = _state;
            _state = status.State;
            bool handsChanged = (previous == BriefcaseState.Carried) != (status.State == BriefcaseState.Carried);
            if (handsChanged && status.State != BriefcaseState.Returning)
                _popStarted = Time.unscaledTime;
            _collider.enabled = status.State == BriefcaseState.Dropped;
            _visual.gameObject.SetActive(status.State != BriefcaseState.Returning);
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

        /// <summary>While carried, the visual eases into the carrier's hand and then stays in it. The beacon follows the visual.</summary>
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

        /// <summary>A quick squash and settle on the visual when it changes hands: wide and short first, then back to its own shape.</summary>
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

        // ---- Beacon (every machine, local only) ------------------------------------------------

        /// <summary>A column of light over the case, drawn through floors and walls, so a stranger can find the objective. Built once, from the match profile's material.</summary>
        void EnsureBeacon(MatchDirector match)
        {
            if (_beacon != null || match == null || match.Profile == null || match.Profile.BeaconMaterial == null)
                return;

            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Quad);
            column.name = "Beacon";
            // At once, not at the end of the frame: a concave mesh collider under the briefcase's
            // moving rigidbody is refused by physics on every step until it is gone.
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

        /// <summary>Stands on the visible case, turns to face the camera about the vertical, and fades out toward the profile's range. Hidden from the carrier and while away.</summary>
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

        /// <summary>
        /// A second cue besides colour, for players who can't tell cyan from amber: while the
        /// rival firm holds the case its beacon breathes; your own firm's and a loose one stay steady.
        /// </summary>
        float RivalPulse()
        {
            FirmMember local = FirmMember.Local;
            if (_tintFirm == MatchDirector.NoFirm || local == null || local.Firm == _tintFirm)
                return 1f;

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * RivalPulseRate * 2f * Mathf.PI);
            return Mathf.Lerp(RivalPulseLow, 1f, wave);
        }

        /// <summary>1 near, fading to 0 toward the profile's range; 0 while away or for the carrier themself.</summary>
        float BeaconFade(MatchDirector match, Vector3 viewer, Vector3 foot)
        {
            bool mine = _state == BriefcaseState.Carried && _carrier != null && _carrier == FirmMember.Local;
            if (!match.IsLive || _state == BriefcaseState.Returning || mine)
                return 0f;

            float range = match.Profile.BeaconRange;
            return Mathf.Clamp01((range - Vector3.Distance(viewer, foot)) / (range * BeaconFadeBand));
        }

        /// <summary>The carrier sees it low in their own view; everyone else sees it hanging at the carrier's side.</summary>
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

        /// <summary>In your firm's colour while one of you holds it, the rival's while they do, the loose colour otherwise; on your own screen.</summary>
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

        // ---- Host: physics -------------------------------------------------------------------

        /// <summary>Host only. Into a hand, or away for a return: it stops moving and can't be touched.</summary>
        internal void ServerPark()
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
            StrayTicks = 0;
        }

        /// <summary>Host only. Loose in the world again at this spot with this velocity.</summary>
        internal void ServerRelease(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            // Clients snap to the new spot rather than gliding there from where it was parked.
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

        /// <summary>
        /// Host only. Shoves a loose briefcase: the hook for a blast or a thrown object to send
        /// it flying. Ignored while it is carried or away.
        /// </summary>
        public void Throw(Vector3 velocityChange)
        {
            if (!IsServerStarted || _rigidbody.isKinematic)
                return;

            _rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
        }
    }
}
