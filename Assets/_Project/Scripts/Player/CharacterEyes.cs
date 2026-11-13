// AI-GENERATED SOURCE FILE
// Generator : Codex (GPT-6)
// Generated : 2026-11-12
// Prompt    : Give the STDMC player character animated 2D eye cards.
// Reviewed  : NOT YET REVIEWED BY A HUMAN
// Summary   : Client-local blinking, glancing and expressions for the two fitted eye cards.
using UnityEngine;

namespace CloseTheDeal.Player
{
    /// <summary>Drives two eye-card renderers from the finished head pose and local presentation signals.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class CharacterEyes : MonoBehaviour
    {
        // WHY: read the final head direction after PlayerAnimator (100) and PlayerRagdoll (110).
        private const int ExecutionOrder = 120;
        private const float HalfLidShare = 0.25f;
        private const float TravelWeight = 0.65f;
        private const float TurnWeight = 0.4f;
        private const float TravelResponse = 12f;
        // WHY: large visual jumps are teleports/corrections, not a direction to look.
        private const float MaxVisualSprintMultiple = 3f;
        // WHY: a turn at roughly half a revolution per second uses the full counter-glance.
        private const float FullTurnRadiansPerSecond = Mathf.PI;

        private static readonly int EyePose = Shader.PropertyToID("_EyePose");

        // WHY: rows and columns must match the artist's atlas in CHARACTER_EYES.md.
        private enum Mood { Bored, Wince, Wide, Focused }
        private enum Lid { Rest, Half, Closed }

        [Header("Eye cards")]
        [Tooltip("Sets blink timing, pupil travel and expression thresholds for this character.")]
        [SerializeField] private EyeProfile _profile;
        [Tooltip("The head-mounted eye cards, ordered left to right as seen from the front.")]
        [SerializeField] private MeshRenderer[] _cards;

        private PlayerAnimator _animation;
        private PlayerMotor _motor;
        private Transform _visualRoot;
        private Transform _face;
        private System.Random _blinkRandom;
        private MaterialPropertyBlock _block;
        private float _nextBlink;
        private Vector3 _lastPosition;
        private Vector3 _lastForward;
        private Vector3 _travel;
        private Vector2 _glance;
        private Vector4 _lastPose;

        private void Awake()
        {
            if (_profile == null || _cards == null || _cards.Length != 2 ||
                _cards[0] == null || _cards[1] == null)
            {
                Debug.LogWarning("[CharacterEyes] Assign an eye profile and both eye cards.", this);
                enabled = false;
                return;
            }

            _animation = GetComponentInParent<PlayerAnimator>();
            _motor = GetComponentInParent<PlayerMotor>();
            _visualRoot = _animation != null ? _animation.transform : transform;
            _face = _cards[0].transform;
            _blinkRandom = new System.Random();
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _lastPosition = _visualRoot.position;
            _lastForward = _face.forward;
            _travel = Vector3.zero;
            _glance = Vector2.zero;
            _lastPose = new Vector4(-1f, -1f, 0f, 0f);
            _nextBlink = Time.time + BlinkWait();
            ApplyPose(Mood.Bored, Lid.Rest);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            MovementProfile movement = _motor != null ? _motor.Profile : null;
            Vector3 position = _visualRoot.position;
            Vector3 measured = (position - _lastPosition) / dt;
            _lastPosition = position;
            measured.y = 0f;
            float speed = measured.magnitude;
            float sprint = 0f;
            Vector3 travel = Vector3.zero;
            if (movement != null && movement.SprintSpeed > 0f &&
                speed <= movement.SprintSpeed * MaxVisualSprintMultiple)
            {
                sprint = Mathf.InverseLerp(movement.WalkSpeed, movement.SprintSpeed, speed);
                travel = measured / movement.SprintSpeed;
            }

            _travel = Vector3.Lerp(_travel, travel, 1f - Mathf.Exp(-TravelResponse * dt));
            Vector3 aim = _visualRoot.forward;
            if (_motor != null && _motor == PlayerMotor.Local && PlayerCamera.Instance != null)
                aim = PlayerCamera.Instance.transform.forward;

            Vector3 localAim = _face.InverseTransformDirection(aim);
            Vector3 localTravel = _face.InverseTransformDirection(_travel);
            // WHY: last frame's forward, measured in the new face axes, points against the turn.
            Vector3 counterTurn = _face.InverseTransformDirection(_lastForward);
            _lastForward = _face.forward;
            Vector2 turn = Vector2.ClampMagnitude(new Vector2(counterTurn.x, counterTurn.y) /
                (dt * FullTurnRadiansPerSecond), 1f);
            Vector2 target = new Vector2(localAim.x, localAim.y) +
                new Vector2(localTravel.x, 0f) * TravelWeight + turn * TurnWeight;
            // WHY: texture U increases to the viewer's right, opposite the character's right.
            target.x = -target.x;
            _glance = Vector2.MoveTowards(_glance, Vector2.ClampMagnitude(target, 1f) * _profile.GlanceRange,
                _profile.GlanceSpeed * dt);

            ApplyPose(ChooseMood(sprint), BlinkFrame(Time.time));
        }

        private Mood ChooseMood(float sprint)
        {
            float limp = _animation != null ? _animation.Limp : 0f;
            if ((_motor != null && _motor.State == MoveState.Knocked) ||
                (limp > 0f && limp >= _profile.WinceThreshold))
                return Mood.Wince;

            float air = _animation != null ? _animation.Airborne :
                (_motor != null && _motor.State == MoveState.Airborne ? 1f : 0f);
            if (air > 0f && air >= _profile.WideThreshold)
                return Mood.Wide;

            return sprint > 0f && sprint >= _profile.FocusThreshold ? Mood.Focused : Mood.Bored;
        }

        private float BlinkWait()
        {
            Vector2 interval = _profile.BlinkInterval;
            return Mathf.Lerp(interval.x, interval.y, (float)_blinkRandom.NextDouble());
        }

        private Lid BlinkFrame(float now)
        {
            if (now < _nextBlink)
                return Lid.Rest;

            float phase = (now - _nextBlink) / _profile.BlinkDuration;
            if (phase >= 1f)
            {
                _nextBlink = now + BlinkWait();
                return Lid.Rest;
            }

            return phase < HalfLidShare || phase >= 1f - HalfLidShare ? Lid.Half : Lid.Closed;
        }

        private void ApplyPose(Mood mood, Lid lid)
        {
            var pose = new Vector4((int)mood, (int)lid, _glance.x, _glance.y);
            if (pose == _lastPose)
                return;

            _lastPose = pose;
            foreach (MeshRenderer card in _cards)
            {
                if (card == null)
                    continue;

                _block.Clear();
                card.GetPropertyBlock(_block);
                _block.SetVector(EyePose, pose);
                card.SetPropertyBlock(_block);
            }
        }
    }
}
