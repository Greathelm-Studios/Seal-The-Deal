using System;
using CloseTheDeal.Player;
using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace CloseTheDeal.MatchMode
{
    /// <summary>
    /// A firm's briefcase: the objective. The host simulates it while it lies loose; everyone
    /// else holds it still and follows the host through NetworkTransform, like furniture.
    /// While carried the networked body stays parked where it was picked up, switched off, and
    /// only the visual follows the carrier on each machine, so carrying costs no traffic.
    /// Its state lives on the MatchDirector; this only shows it. Design: docs/systems/MATCH.md.
    /// </summary>
    [DefaultExecutionOrder(200)] // After the player camera has moved, so the carrier's own view never lags a frame.
    [RequireComponent(typeof(Rigidbody), typeof(NetworkTransform))]
    public sealed class Briefcase : NetworkBehaviour
    {
        /// <summary>A rival kicked this briefcase. Raised on every machine, for sound and effects.</summary>
        public static event Action<Briefcase> OnKicked;

        [Tooltip("The part that is seen. It leaves the body to follow the carrier's hand and comes back when dropped.")]
        [SerializeField] Transform _visual;

        [Tooltip("The parts tinted by firm: your own firm's colour or the rival's, as set on the match profile.")]
        [SerializeField] Renderer[] _firmMarkers = new Renderer[0];

        /// <summary>How the case sits in the carrier's view, relative to their eye: side-on, tilted a little toward them.</summary>
        static readonly Quaternion FirstPersonTilt = Quaternion.Euler(0f, 75f, 8f);

        static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        static readonly Briefcase[] ByFirm = new Briefcase[MatchDirector.FirmCount];

        readonly SyncVar<byte> _firm = new();

        Rigidbody _rigidbody;
        Collider _collider;
        NetworkTransform _networkTransform;
        MaterialPropertyBlock _block;
        Vector3 _centreLocal;
        byte _assignedFirm;

        BriefcaseState _state;
        FirmMember _carrier;
        int _carrierId;
        float _carryStarted;
        Vector3 _blendFromPosition;
        Quaternion _blendFromRotation;

        /// <summary>Host only: ticks it has lain outside its own building in a row.</summary>
        internal int StrayTicks;

        /// <summary>Host only: the tick it comes back at its firm's checkpoint.</summary>
        internal uint ReturnAtTick;

        /// <summary>Host only: the first tick it can be kicked again.</summary>
        internal uint KickReadyTick;

        /// <summary>Host only: where its carrier last stood, in case the carrier vanishes without a word.</summary>
        internal Vector3 LastCarrierPosition;

        /// <summary>Which firm it belongs to: the index of that firm's building.</summary>
        public int Firm => _firm.Value;

        /// <summary>The centre of the case as it lies in the world.</summary>
        public Vector3 Centre => transform.TransformPoint(_centreLocal);

        public Vector3 Position => _rigidbody.position;

        /// <summary>The briefcase of the given firm on this machine, or null before it has spawned.</summary>
        public static Briefcase Of(int firm)
        {
            return firm >= 0 && firm < ByFirm.Length ? ByFirm[firm] : null;
        }

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

        /// <summary>Host only, before spawning: which firm this one is for.</summary>
        public void AssignFirm(int firm)
        {
            _assignedFirm = (byte)firm;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _firm.Value = _assignedFirm;
            StrayTicks = 0;
            ReturnAtTick = 0;
            KickReadyTick = 0;
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
            if (ByFirm[Firm] == this)
                ByFirm[Firm] = null;
        }

        /// <summary>Makes itself findable by firm and takes up whatever state the match already has for it.</summary>
        internal void Register()
        {
            ByFirm[Firm] = this;
            RefreshTint();
            MatchDirector match = MatchDirector.Instance;
            if (match != null && match.IsLive)
                Apply(match.Firm(Firm));
        }

        // ---- Showing the state (every machine) -----------------------------------------------

        /// <summary>Takes up a new state from the match: where it is, whether it can be touched, who holds it.</summary>
        internal void Apply(FirmStatus status)
        {
            BriefcaseState previous = _state;
            _state = status.State;
            _collider.enabled = status.State == BriefcaseState.Dropped;
            _visual.gameObject.SetActive(status.State != BriefcaseState.Returning);

            if (status.State == BriefcaseState.Carried)
            {
                if (previous != BriefcaseState.Carried || _carrierId != status.CarrierId)
                    BeginCarry(status.CarrierId);
                return;
            }

            _carrier = null;
            _visual.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

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

        /// <summary>While carried, the visual eases into the carrier's hand and then stays in it.</summary>
        void LateUpdate()
        {
            if (_state != BriefcaseState.Carried)
                return;

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
                float eased = 1f - (1f - t) * (1f - t); // ease-out: fast off the desk, soft into the hand
                position = Vector3.Lerp(_blendFromPosition, position, eased);
                rotation = Quaternion.Slerp(_blendFromRotation, rotation, eased);
            }

            _visual.SetPositionAndRotation(position, rotation);
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
                rotation = eye.rotation * FirstPersonTilt;
                return;
            }

            _carrier.CarryAnchor.GetPositionAndRotation(out position, out rotation);
        }

        /// <summary>Your own firm's briefcase and the rival's are told apart by colour, on your own screen.</summary>
        void RefreshTint()
        {
            MatchDirector match = MatchDirector.Instance;
            if (match == null || match.Profile == null)
                return;

            Color colour = match.ColourFor(Firm);
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
        /// Host only. Shoves a loose briefcase: a rival's kick, or anything else that should
        /// send it flying (a blast, say). Ignored while it is carried or away.
        /// </summary>
        public void Throw(Vector3 velocityChange)
        {
            if (!IsServerStarted || _rigidbody.isKinematic)
                return;

            _rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
        }

        /// <summary>Host only. A rival's boot, told to every machine.</summary>
        internal void ServerKick(Vector3 velocityChange)
        {
            Throw(velocityChange);
            ObserversKicked();
        }

        [ObserversRpc]
        void ObserversKicked()
        {
            OnKicked?.Invoke(this);
        }
    }
}
