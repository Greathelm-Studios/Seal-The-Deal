using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Local-only first-person camera. Sits at the owning player's eye on their smoothed
    /// visual, so it never steps between ticks. Leaning slides it sideways and tilts it, and
    /// a wall beside the head stops the slide before the view clips through. Supplies the yaw
    /// the player faces and the aim direction weapons fire along. Mouse look only applies
    /// while the mouse is captured; the game menu decides that. Nothing here is networked:
    /// the lean itself is predicted state on PlayerMotor, and this only shows it.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera Instance { get; private set; }

        [Tooltip("Mouse look speed in degrees per mouse unit. 0.12 is a typical shooter feel; higher turns faster.")]
        [SerializeField] float _sensitivity = 0.12f;

        [Tooltip("How far the camera may look up (x, negative) and down (y, positive), in degrees. Keep inside ±89 or the view flips at the top.")]
        [SerializeField] Vector2 _pitchLimits = new(-85f, 85f);

        [Tooltip("Closest the eye gets to a wall when leaning into it, in metres. Keep above the camera's near clip distance or the wall shows through.")]
        [SerializeField] float _wallClearance = 0.2f;

        const float AimRange = 200f;
        const string PlayerLayer = "Player";
        const string PropLayer = "Prop";

        Transform _target;
        PlayerInputReader _input;
        PlayerMotor _motor;
        float _yaw;
        float _pitch;
        float _shownLean;
        int _aimMask;
        int _wallMask;

        // Body view (PlayerViewmodel.BodyView): the eye rides on the real head, damped.
        Camera _camera;
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

        void Awake()
        {
            Instance = this;
            _camera = GetComponent<Camera>();
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
        /// <param name="follow">Share of the head's movement the eye takes, 0 to 1. 0 = the eye stays on the steady point.</param>
        /// <param name="smoothSeconds">How long the eye takes to catch the head up, seconds. Higher = smoother and laggier.</param>
        /// <param name="forward">How far in front of the steady point the eye sits, metres, so the collar and shoulders are behind it.</param>
        /// <param name="maxOffset">The furthest the head may pull the eye from the steady point, metres.</param>
        /// <remarks>
        /// WHY the eye is not simply parented to the head bone: the head bobs with every step,
        /// leads turns and is thrown about by hits, and a camera that does all of that makes
        /// people feel sick. The view's direction always comes from the mouse alone; only the
        /// eye's position takes a damped, capped share of the head's movement.
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
            _target = target;
            _input = input;
            _motor = motor;
            _yaw = target.eulerAngles.y;
            _pitch = 0f;
            _shownLean = 0f;
        }

        public void Release(Transform target)
        {
            if (_target != target)
                return;

            _target = null;
            _input = null;
            _motor = null;
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

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = _input.LookDelta * _sensitivity;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, _pitchLimits.x, _pitchLimits.y);
            }

            MovementProfile profile = _motor.Profile;

            // The motor's lean moves once per tick; ease toward it at the same pace so it never steps.
            _shownLean = Mathf.MoveTowards(_shownLean, _motor.Lean, profile.LeanSpeed * Time.deltaTime);

            Vector3 head = _target.position + Vector3.up * _motor.EyeHeight;
            if (_headBone != null)
            {
                // Where the real eyes are, measured from the steady point; the eye takes a
                // damped, capped share of that, and sits a little forward of the neck.
                Vector3 real = _headBone.position + _headBone.up * HeadBoneToEyes - head;
                real = Vector3.ClampMagnitude(real, _headMaxOffset);
                _headOffset = Vector3.Lerp(_headOffset, real, 1f - Mathf.Exp(-Time.deltaTime / _headSmoothSeconds));
                head += _headOffset * _headFollow + Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward * _headForward;
            }
            Vector3 side = Quaternion.Euler(0f, _yaw, 0f) * Vector3.right;
            float reach = profile.LeanDistance * Mathf.Abs(_shownLean);
            Vector3 direction = side * Mathf.Sign(_shownLean);

            if (reach > 0.001f && Physics.SphereCast(head, _wallClearance, direction, out RaycastHit wall, reach, _wallMask, QueryTriggerInteraction.Ignore))
                reach = wall.distance;

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, -_shownLean * profile.LeanAngle);
            transform.SetPositionAndRotation(head + direction * reach, rotation);
        }
    }
}
