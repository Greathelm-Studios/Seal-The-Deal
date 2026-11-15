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
    public sealed partial class PlayerCamera : MonoBehaviour
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
        float _restFov;
        float _edgeHeld;
        float _slideRoll;
        const float StickEdge = 0.95f;
        int _aimMask;
        int _wallMask;

        // Body view (PlayerViewmodel.BodyView): the eye rides on the real head, damped.
        Transform _headBone;
        float _headFollow, _headSmoothSeconds, _headForward, _headMaxOffset;
        float _authoredNear;
        Vector3 _headOffset;

        /// <summary>Height of the eyes above the head bone, metres, on the 1.8 m character: the bone sits at the top of the neck.</summary>
        const float HeadBoneToEyes = 0.09f;

        /// <summary>Near clip distance while in the body view, metres. The player's own shoulders and arms are within the usual near distance of the eye.</summary>
        const float BodyViewNear = 0.03f;

        /// <summary>Direction the player faces, in degrees around the vertical axis.</summary>
        public float Yaw => _yaw;

        /// <summary>How far the view looks down (positive) or up (negative), in degrees from level.</summary>
        public float Pitch => _pitch;

        void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
            _restFov = _camera.fieldOfView;
            _authoredNear = _camera.nearClipPlane;
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

        /// <summary>
        /// Switches to the body view: the eye follows the body's real head instead of sitting
        /// on a fixed point, so the player looks out of their own body.
        /// </summary>
        /// <param name="headBone">The character's head bone.</param>
        /// <param name="follow">Share of the head's quick movement (step bob, a hit's jolt) the eye takes, 0 to 1. 0 = none of it.</param>
        /// <param name="smoothSeconds">How long the eye takes to settle onto where the head has moved to, seconds. Higher = steadier and laggier.</param>
        /// <param name="forward">How far in front of the head bone the eye sits, metres: the face, so the collar and shoulders are behind it.</param>
        /// <param name="maxOffset">The most quick movement the eye may take, metres, before the share is applied.</param>
        /// <remarks>
        /// WHY the eye is not simply parented to the head bone: the head bobs with every step,
        /// leads turns and is thrown about by hits, and a camera that does all of that makes
        /// people feel sick. The view's direction always comes from the mouse alone. The eye's
        /// position follows the head fully but slowly, and only a capped share of anything
        /// quick. The first version followed a third of everything; in a sprint the body then
        /// leaned 30 cm ahead of the eye and the player saw the back of their own head.
        /// </remarks>
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

            _target = target;
            _input = input;
            _motor = motor;
            BindFeedback(_motor);
            Hud.CrosshairFeed.EnsureInScene();
            _yaw = target.eulerAngles.y;
            _pitch = 0f;
            _shownLean = 0f;
            _shownCrouch = motor.Crouch;
        }

        public void Release(Transform target)
        {
            if (_target != target)
                return;

            UnbindFeedback(_motor);
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
                head += _headOffset + quick * _headFollow;
            }
            Vector3 side = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            // In the body view the real head has already leaned with the body; sliding the eye
            // as well would lean it twice. The tilt below still applies.
            float reach = _headBone != null ? 0f : profile.LeanDistance * Mathf.Abs(_shownLean);
            Vector3 direction = side * Mathf.Sign(_shownLean);

            if (reach > 0.001f && Physics.SphereCast(head, _wallClearance, direction, out RaycastHit wall, reach, _wallMask, QueryTriggerInteraction.Ignore))
                reach = wall.distance;

            float roll = Reduced ? 0f : SlideRoll(profile, dt);
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, -_shownLean * profile.LeanAngle + roll);
            // The movement kick moves the eye along the view's own axes; it never turns the view.
            Vector3 kick = rotation * StepFeedback(profile, dt);
            transform.SetPositionAndRotation(head + direction * reach + kick, rotation);

            float atSpeed = Mathf.InverseLerp(profile.WalkSpeed, profile.SprintSpeed, _motor.AnimState.PlanarSpeed);
            float widen = Reduced ? 0f : profile.SpeedFovKick * atSpeed + (_motor.AnimState.Sliding ? profile.SlideFovKick : 0f);
            _camera.fieldOfView = Mathf.MoveTowards(_camera.fieldOfView, _restFov + widen, profile.SpeedFovRate * dt);
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
