using CloseTheDeal.Combat;
using CloseTheDeal.UI;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Local-only first-person camera following the owning player's eye, crouch, and body-view head smoothing.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed partial class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera Instance { get; private set; }

        [Tooltip("Closest the camera gets to a wall while it is pulled back watching the ragdoll, in metres. Keep above the camera's near clip distance or the wall shows through.")]
        [SerializeField] float _wallClearance = 0.2f;

        [Header("Ragdoll view")]
        [Tooltip("How far behind the ragdolled body the camera pulls back while dead or knocked out, in metres. Stops before walls.")]
        [Range(0.5f, 8f)] [SerializeField] float _ragdollCameraDistance = 3f;

        [Tooltip("Height above the ragdoll's hips the camera orbits around, in metres.")]
        [Range(0f, 1.5f)] [SerializeField] float _ragdollCameraHeight = 0.25f;

        [Tooltip("How fast the camera pulls out to watch the ragdoll on death or knockout, in seconds.")]
        [Range(0.05f, 1f)] [SerializeField] float _ragdollBlendInSeconds = 0.25f;

        [Tooltip("How fast the camera glides back into first-person when recovering from being knocked out, in seconds.")]
        [Range(0.05f, 1f)] [SerializeField] float _ragdollBlendOutSeconds = 0.35f;

        const float AimRange = 200f;
        const string PlayerLayer = "Player";
        const string PropLayer = "Prop";

        Camera _camera;
        Transform _target;
        PlayerInputReader _input;
        PlayerMotor _motor;
        Health _health;
        PlayerRagdoll _ragdoll;
        PlayerViewmodel _viewmodel;
        float _ragdollBlend;
        bool _forcedBodyShown;
        float _yaw;
        float _pitch;
        float _shownCrouch;
        float _restFov;
        float _edgeHeld;
        float _slideRoll;
        float _strafeRoll;
        const float StickEdge = 0.95f;
        int _aimMask;
        int _wallMask;

        // Body view (PlayerViewmodel.BodyView): the eye rides on the real head, damped.
        Transform _headBone;
        float _headFollow, _headSmoothSeconds, _headForward, _headMaxOffset;
        float _authoredNear;
        Vector3 _headOffset;

        const float HeadBoneToEyes = 0.09f;
        const float BodyViewNear = 0.03f;

        /// <summary>Direction the player faces, in degrees around the vertical axis.</summary>
        public float Yaw => _yaw;

        /// <summary>How far the view looks down (positive) or up (negative), in degrees from level.</summary>
        public float Pitch => _pitch;

        /// <summary>True while the camera is pulled out watching the local player's ragdoll (dead or knocked out).</summary>
        public bool InRagdollView => _ragdollBlend > 0.001f;

        void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
            _restFov = _camera.fieldOfView;
            _authoredNear = _camera.nearClipPlane;
            int playerLayer = LayerMask.NameToLayer(PlayerLayer);
            int propLayer = LayerMask.NameToLayer(PropLayer);
            int player = playerLayer >= 0 ? 1 << playerLayer : 0;
            int prop = propLayer >= 0 ? 1 << propLayer : 0;
            _aimMask = ~player;            // Skips all players so you never aim at yourself; the shot from your eye still hits anyone in its path.
            _wallMask = ~(player | prop);  // Only the building stops the ragdoll camera; furniture doesn't.
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Switches to body view so the eye smoothly tracks the real head bone (see PLAYER_MOVEMENT.md).</summary>
        public void SetBodyView(Transform headBone, float follow, float smoothSeconds, float forward, float maxOffset)
        {
            _headBone = headBone;
            _headFollow = Mathf.Clamp01(follow);
            _headSmoothSeconds = Mathf.Max(smoothSeconds, 0.001f);
            _headForward = forward;
            _headMaxOffset = Mathf.Max(maxOffset, 0f);
            _headOffset = Vector3.zero;
            _camera.nearClipPlane = Mathf.Min(_authoredNear, BodyViewNear);
        }

        /// <summary>Back to the usual view: the eye on the steady point.</summary>
        public void ClearBodyView()
        {
            _headBone = null;
            _camera.nearClipPlane = _authoredNear;
        }

        public void Follow(Transform target, PlayerInputReader input, PlayerMotor motor)
        {
            if (_motor != null)
                UnbindFeedback(_motor);
            if (_health != null)
                _health.Respawned -= OnFollowedRespawned;

            _target = target;
            _input = input;
            _motor = motor;
            _health = motor != null ? motor.GetComponent<Health>() : null;
            _ragdoll = motor != null ? motor.GetComponentInChildren<PlayerRagdoll>(true) : null;
            _viewmodel = motor != null ? motor.GetComponent<PlayerViewmodel>() : null;
            if (_health != null)
                _health.Respawned += OnFollowedRespawned;
            _ragdollBlend = 0f;
            _forcedBodyShown = false;
            BindFeedback(_motor);
            Hud.CrosshairFeed.EnsureInScene();
            _yaw = target.eulerAngles.y;
            _pitch = 0f;
            _shownCrouch = motor.Crouch;
            _slideRoll = 0f;
            _strafeRoll = 0f;
        }

        public void Release(Transform target)
        {
            if (_target != target)
                return;

            if (_health != null)
                _health.Respawned -= OnFollowedRespawned;
            if (_forcedBodyShown && _motor != null && (_viewmodel == null || !_viewmodel.IsShown))
                _motor.SetOwnBodyShown(false);
            _forcedBodyShown = false;
            _ragdollBlend = 0f;
            UnbindFeedback(_motor);
            _target = null;
            _input = null;
            _motor = null;
            _health = null;
            _ragdoll = null;
            _viewmodel = null;
            _camera.fieldOfView = _restFov;
        }

        void OnFollowedRespawned(Health health)
        {
            _ragdollBlend = 0f;
            if (_forcedBodyShown && _motor != null && (_viewmodel == null || !_viewmodel.IsShown))
                _motor.SetOwnBodyShown(false);
            _forcedBodyShown = false;
        }

        /// <summary>
        /// Unit direction from a point on the player (say their eye) to whatever is under the
        /// crosshair, so a shot from the player lands where the crosshair is.
        /// </summary>
        public Vector3 AimDirectionFrom(Vector3 origin)
        {
            Transform cam = transform;
            Vector3 target = Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, AimRange, _aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : cam.position + cam.forward * AimRange;

            Vector3 direction = target - origin;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : cam.forward;
        }

        void LateUpdate()
        {
            if (_target == null)
                return;

            MovementProfile profile = _motor.Profile;
            float dt = Time.deltaTime;
            Look(profile, dt);

            // The motor's crouch moves once per tick; ease toward it at the same pace so the view never steps.
            _shownCrouch = Mathf.MoveTowards(_shownCrouch, _motor.Crouch, profile.CrouchRate * dt);
            float eye = _motor.StandingEyeHeight - _motor.CrouchEyeDrop * _shownCrouch;
            Vector3 head = _target.position + Vector3.up * eye;

            if (_headBone != null)
            {
                // Where the real eyes are, measured from the steady point: on the front of the
                // real head, which tips forward with a run and down with a look at the floor.
                Vector3 real = _headBone.position + _headBone.up * HeadBoneToEyes + _headBone.forward * _headForward - head;

                // The eye goes all the way to where the head has settled (so it never ends up
                // behind or inside the body), but takes its time getting there; of the quick
                // movement on top (step bob, a hit's jolt) it takes only a capped share.
                _headOffset = Vector3.Lerp(_headOffset, real, 1f - Mathf.Exp(-Time.deltaTime / _headSmoothSeconds));
                Vector3 quick = Vector3.ClampMagnitude(real - _headOffset, _headMaxOffset);
                Vector3 flatForward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
                float quickForward = Vector3.Dot(quick, flatForward);
                quick -= flatForward * (quickForward * 0.65f);
                head += _headOffset + quick * _headFollow;
            }

            float roll = Reduced ? 0f : SlideRoll(profile, dt) + StrafeRoll(profile, dt);
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, roll);
            // The movement kick moves the eye along the view's own axes; it never turns the view.
            Vector3 kick = rotation * StepFeedback(profile, dt);
            Vector3 fpPosition = head + kick;

            bool wantRagdoll = _health != null && _health.IsIncapacitated && _ragdoll != null && _ragdoll.Running;
            if (wantRagdoll)
                _ragdollBlend = Mathf.MoveTowards(_ragdollBlend, 1f, dt / Mathf.Max(_ragdollBlendInSeconds, 0.01f));
            else if (_ragdollBlend > 0f)
                _ragdollBlend = Mathf.MoveTowards(_ragdollBlend, 0f, dt / Mathf.Max(_ragdollBlendOutSeconds, 0.01f));

            bool inRagdoll = _ragdollBlend > 0.001f && _ragdoll != null && _ragdoll.Running;
            if (inRagdoll && !_forcedBodyShown && (_viewmodel == null || !_viewmodel.IsShown))
            {
                _motor.SetOwnBodyShown(true);
                _forcedBodyShown = true;
            }
            else if (!inRagdoll && _forcedBodyShown)
            {
                _forcedBodyShown = false;
                if (_viewmodel == null || !_viewmodel.IsShown)
                    _motor.SetOwnBodyShown(false);
            }

            if (inRagdoll)
            {
                Vector3 pivot = _ragdoll.HipsPosition + Vector3.up * _ragdollCameraHeight;
                Quaternion orbitRotation = Quaternion.Euler(_pitch, _yaw, 0f);
                Vector3 back = orbitRotation * Vector3.back;
                float dist = _ragdollCameraDistance;
                if (Physics.SphereCast(pivot, _wallClearance, back, out RaycastHit wall, dist, _wallMask, QueryTriggerInteraction.Ignore))
                    dist = Mathf.Max(0f, wall.distance);
                Vector3 ragdollPosition = pivot + back * dist;
                float t = Mathf.SmoothStep(0f, 1f, _ragdollBlend);
                transform.SetPositionAndRotation(Vector3.Lerp(fpPosition, ragdollPosition, t), Quaternion.Slerp(rotation, orbitRotation, t));
            }
            else
            {
                transform.SetPositionAndRotation(fpPosition, rotation);
            }

            float atSpeed = Mathf.InverseLerp(profile.WalkSpeed, profile.SprintSpeed, _motor.AnimState.PlanarSpeed);
            float widen = Reduced ? 0f : profile.SpeedFovKick * atSpeed + (_motor.AnimState.Sliding ? profile.SlideFovKick : 0f);
            _camera.fieldOfView = Mathf.MoveTowards(_camera.fieldOfView, _restFov + widen, profile.SpeedFovRate * dt);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookPrefs() => InputPrefs.LookHooked = true;

        void Look(MovementProfile profile, float dt)
        {
            // Player preferences scale designer values; never mutate the profile asset.
            Vector2 mouse = _input.LookDelta * (profile.MouseSensitivity * InputPrefs.LookSensitivity);
            Vector2 stick = _input.LookStick;
            if (InputPrefs.InvertY)
            {
                mouse.y = -mouse.y;
                stick.y = -stick.y;
            }
            float stickSpeed = InputPrefs.StickLookSpeed;
            float tilt = stick.magnitude;
            if (tilt > 0f)
                stick *= Mathf.Pow(tilt, profile.StickLookCurve - 1f);

            _yaw += mouse.x + stick.x * (profile.StickYawSpeed + TurnBoost(profile, stick.x, dt)) * stickSpeed * dt;
            float pitch = _pitch - mouse.y - stick.y * profile.StickPitchSpeed * stickSpeed * dt;
            _pitch = Mathf.Clamp(pitch, -profile.LookUpLimit, profile.LookDownLimit);
        }

        float TurnBoost(MovementProfile profile, float stickX, float dt)
        {
            _edgeHeld = Mathf.Abs(stickX) >= StickEdge ? _edgeHeld + dt : 0f;
            float built = (_edgeHeld - profile.StickTurnBoostDelay) / Mathf.Max(profile.StickTurnBoostRamp, 0.01f);
            return profile.StickTurnBoost * Mathf.Clamp01(built);
        }

        float SlideRoll(MovementProfile profile, float dt)
        {
            PlayerAnimState state = _motor.AnimState;
            float target = 0f;
            if (state.Sliding)
            {
                float across = state.PlanarSpeed > 0.1f ? state.LocalMove.x / state.PlanarSpeed : 0f;
                // Half the roll into a straight slide; drifting sideways swings it away from the drift.
                target = profile.SlideRoll * Mathf.Clamp(0.5f - across * 3f, -1f, 1f);
            }

            _slideRoll = Mathf.MoveTowards(_slideRoll, target, profile.SlideRollRate * dt);
            return _slideRoll;
        }

        float StrafeRoll(MovementProfile profile, float dt)
        {
            PlayerAnimState state = _motor.AnimState;
            float target = 0f;
            if (!state.Sliding && profile.StrafeRoll > 0f && profile.WalkSpeed > 0.1f)
            {
                float strafe = Mathf.Clamp(state.LocalMove.x / profile.WalkSpeed, -1f, 1f);
                target = -strafe * profile.StrafeRoll;
            }

            _strafeRoll = Mathf.MoveTowards(_strafeRoll, target, profile.StrafeRollRate * dt);
            return _strafeRoll;
        }
    }
}
