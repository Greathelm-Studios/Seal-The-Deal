using CloseTheDeal.UI;
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Local-only first-person camera. Sits at the owning player's eye on their smoothed
    /// visual, so it never steps between ticks. Leaning slides it sideways and tilts it, and
    /// a wall beside the head stops the slide before the view clips through. Crouching lowers
    /// it, a landing dips it and speed widens it. Supplies the yaw the player faces and the
    /// aim direction weapons fire along. Look only applies while the mouse is captured; the
    /// game menu decides that. Nothing here is networked: lean and crouch are predicted state
    /// on PlayerMotor, and this only shows them. Look speeds and limits are designer tuning on
    /// the player's MovementProfile, not on this component, so every scene's camera agrees.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera Instance { get; private set; }

        [Tooltip("Closest the eye gets to a wall when leaning into it, in metres. Keep above the camera's near clip distance or the wall shows through.")]
        [SerializeField] float _wallClearance = 0.2f;

        const float AimRange = 200f;
        const string PlayerLayer = "Player";
        const string PropLayer = "Prop";

        Camera _camera;
        Transform _target;
        PlayerInputReader _input;
        PlayerMotor _motor;
        float _yaw;
        float _pitch;
        float _shownLean;
        float _shownCrouch;
        float _dip;
        float _restFov;
        float _edgeHeld;
        float _slideRoll;
        const float StickEdge = 0.95f;
        int _aimMask;
        int _wallMask;

        /// <summary>Direction the player faces, in degrees around the vertical axis.</summary>
        public float Yaw => _yaw;

        /// <summary>How far the view looks down (positive) or up (negative), in degrees from level.</summary>
        public float Pitch => _pitch;

        void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
            _restFov = _camera.fieldOfView;
            int player = 1 << LayerMask.NameToLayer(PlayerLayer);
            int prop = 1 << LayerMask.NameToLayer(PropLayer);
            _aimMask = ~player;            // Skips all players so you never aim at yourself; the shot from your eye still hits anyone in its path.
            _wallMask = ~(player | prop);  // Only the building stops a lean; furniture doesn't.
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Follow(Transform target, PlayerInputReader input, PlayerMotor motor)
        {
            if (_motor != null)
                _motor.Landed -= OnLanded;

            _target = target;
            _input = input;
            _motor = motor;
            _motor.Landed += OnLanded;
            _yaw = target.eulerAngles.y;
            _pitch = 0f;
            _shownLean = 0f;
            _shownCrouch = motor.Crouch;
            _dip = 0f;
        }

        public void Release(Transform target)
        {
            if (_target != target)
                return;

            _motor.Landed -= OnLanded;
            _target = null;
            _input = null;
            _motor = null;
            _camera.fieldOfView = _restFov;
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

        void OnLanded(float fallSpeed)
        {
            MovementProfile profile = _motor.Profile;
            _dip = Mathf.Min(_dip + fallSpeed * profile.LandingDipPerSpeed, profile.LandingDipMax);
        }

        void LateUpdate()
        {
            if (_target == null)
                return;

            MovementProfile profile = _motor.Profile;
            float dt = Time.deltaTime;
            Look(profile, dt);

            // The motor's lean and crouch move once per tick; ease toward them at the same pace so the view never steps.
            _shownLean = Mathf.MoveTowards(_shownLean, _motor.Lean, profile.LeanSpeed * dt);
            _shownCrouch = Mathf.MoveTowards(_shownCrouch, _motor.Crouch, profile.CrouchRate * dt);
            if (_dip > 0f)
                _dip = Mathf.MoveTowards(_dip, 0f, profile.LandingDipMax / Mathf.Max(profile.LandingDipRecover, 0.01f) * dt);

            float eye = _motor.StandingEyeHeight - _motor.CrouchEyeDrop * _shownCrouch - _dip;
            Vector3 head = _target.position + Vector3.up * eye;
            Vector3 side = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            float reach = profile.LeanDistance * Mathf.Abs(_shownLean);
            Vector3 direction = side * Mathf.Sign(_shownLean);

            if (reach > 0.001f && Physics.SphereCast(head, _wallClearance, direction, out RaycastHit wall, reach, _wallMask, QueryTriggerInteraction.Ignore))
                reach = wall.distance;

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, -_shownLean * profile.LeanAngle + SlideRoll(profile, dt));
            transform.SetPositionAndRotation(head + direction * reach, rotation);

            float atSpeed = Mathf.InverseLerp(profile.WalkSpeed, profile.SprintSpeed, _motor.AnimState.PlanarSpeed);
            _camera.fieldOfView = Mathf.MoveTowards(_camera.fieldOfView, _restFov + profile.SpeedFovKick * atSpeed, profile.SpeedFovRate * dt);
        }

        /// <summary>
        /// The mouse turns the view by how far it moved; the stick turns it at a speed, with
        /// small pushes kept slow for fine aim. Both read as nothing while the menu is open.
        /// </summary>
        /// <summary>Tells the settings screen that look sensitivity, invert and stick speed are applied here, so it shows those rows.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookPrefs() => InputPrefs.LookHooked = true;

        void Look(MovementProfile profile, float dt)
        {
            // The player's own preferences multiply the designer's values; the profile is never written.
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

        /// <summary>Extra turn speed once the stick has sat at the left or right edge a moment, building up over the ramp time.</summary>
        float TurnBoost(MovementProfile profile, float stickX, float dt)
        {
            _edgeHeld = Mathf.Abs(stickX) >= StickEdge ? _edgeHeld + dt : 0f;
            float built = (_edgeHeld - profile.StickTurnBoostDelay) / Mathf.Max(profile.StickTurnBoostRamp, 0.01f);
            return profile.StickTurnBoost * Mathf.Clamp01(built);
        }

        /// <summary>Roll while sliding, away from the slide's sideways drift, so a slide reads as weight without moving the crosshair.</summary>
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
    }
}
