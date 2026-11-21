// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Modified  : 2026-11-21 — removed the old separate-camera viewmodel arms and F5
//              toggle; true POV (looking out of the real body) is now the only
//              first-person view, with the head hidden from the owner's camera by a
//              world-space sphere glued to the head bone.
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Sits on the player prefab. Attaches PlayerHeldWeapon to every player's
//              body, drives the local player's true-POV camera and head-clip sphere,
//              and supplies look lag, sprint carry, climb drop, fire kick, and reload
//              motion to the body's held weapon.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Drives the local player's true-POV body view and held-weapon motion, and attaches
    /// <see cref="PlayerHeldWeapon"/> to every player's body.
    /// </summary>
    /// <remarks>
    /// <para><b>Ownership (AGENTS.md §3):</b> local to each client. Nothing is synced and
    /// nothing reads the pose back; other players see the full third-person body.</para>
    /// <para><b>Hiding the owner's head:</b> before the owner's camera renders, this sets a
    /// world-space sphere (<c>_BodyViewHeadSphere</c>) centred on the real head bone so the
    /// character and eye-card shaders clip the head, hair and eyes for that camera only.
    /// Every other camera (mirrors, shadows, other players) receives zero and draws the full head.</para>
    /// <para><b>Driving it:</b> weapon code calls <see cref="Equip"/> when the held weapon
    /// changes (on every machine), <see cref="Fire"/> for a shot and <see cref="Reload"/> /
    /// <see cref="CancelReload"/>, and sets <see cref="Driven"/>.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    // WHY: look lag and lean counter-roll read PlayerCamera (order 0) this frame;
    // PlayerHeldWeapon (order 105) reads BodyViewMotionOffset/Rotation right after.
    [DefaultExecutionOrder(PlaceOrder)]
    public sealed class PlayerViewmodel : MonoBehaviour
    {
        /// <summary>After PlayerCamera (0), before PlayerAnimator (100) and PlayerHeldWeapon (105).</summary>
        const int PlaceOrder = 50;

        /// <summary>Height of the head-clip sphere centre above the head bone (top of neck), in metres. High enough that the sphere covers skull and hair without reaching down into the shoulders or upper arms.</summary>
        const float HeadClipCenterUp = 0.155f;

        /// <summary>Layer name kept for greybox setup and stand-in raycast mask compatibility.</summary>
        public const string LayerName = "Viewmodel";

        /// <summary>How far the stand-in bullet reaches, in metres. Longer than any room in the test scenes. The host's check (<see cref="PlayerHits"/>) uses the same.</summary>
        internal const float StandInBulletRange = 100f;

        /// <summary>Push a stand-in pistol bullet gives what it hits, in newton-seconds.</summary>
        internal const float StandInBulletImpulse = 20f;

        /// <summary>How far the stand-in melee swing reaches from the eye, in metres.</summary>
        internal const float StandInMeleeReach = 2f;

        /// <summary>Push the stand-in melee swing gives what it hits, in newton-seconds.</summary>
        internal const float StandInMeleeImpulse = 60f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Input action for the attack button, in the actions asset's Player map.</summary>
        const string AttackAction = "Player/Attack";

        /// <summary>Input action for reload, in the actions asset's Player map. Missing on some branches; the R key is read then.</summary>
        const string ReloadAction = "Player/Reload";
#endif

        static readonly int BodyViewHeadSphereId = Shader.PropertyToID("_BodyViewHeadSphere");

        [Tooltip("The player this viewmodel belongs to. True POV activates when this is the player you control.")]
        [SerializeField] PlayerMotor _motor;

        [Tooltip("Template prefab supplying the default weapon model, hand seats, and finger curls for the body's held weapon.")]
        [SerializeField] ViewmodelArms _armsPrefab;

        [Tooltip("Placement, look sway, kick, reload, and body-view tuning.")]
        [SerializeField] ViewmodelProfile _profile;

        [Tooltip("The Input System actions asset, read for the attack button until weapon code drives the weapon.")]
        [SerializeField] InputActionAsset _actions;

        Camera _baseCamera;
        PlayerAnimator _animator;
        Transform _head;
        bool _active;
        bool _bodyViewAttached;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        InputAction _attack;
        InputAction _reload;
        int _standInBulletMask;
#endif

        Quaternion _lastCameraRotation;
        Vector2 _lag;
        Vector2 _lagVelocity;
        float _sprint;
        float _sprintVelocity;
        float _climb;
        float _climbVelocity;
        bool _twoHanded;
        float _twoHand;
        float _twoHandVelocity;
        float _kick;
        float _kickSide = 1f;
        float _reloadElapsed = -1f;
        float _reloadSeconds;
        PlayerHeldWeapon _body;
        PlayerHits _hits;

        /// <summary>True while this client is running true-POV first person for this player.</summary>
        public bool IsShown => _active;

        /// <summary>
        /// Whether the weapon is held in both hands. Starts as the template prefab says.
        /// </summary>
        /// <remarks>Local to this machine: nothing sends it. It belongs with whatever syncs which weapon a player holds.</remarks>
        public bool TwoHanded
        {
            get => _twoHanded;
            set => _twoHanded = value;
        }

        /// <summary>Where the gun's grip rests relative to the eye right now, in metres: the one-handed place, the two-handed place, or part way between while it moves.</summary>
        public Vector3 RestPosition => Vector3.Lerp(_profile.GunPosition, _profile.TwoHandedPosition, _twoHand);

        /// <summary>How the gun is turned at rest right now, blended the same way as <see cref="RestPosition"/>.</summary>
        public Quaternion RestTurn => Quaternion.Slerp(Quaternion.Euler(_profile.GunTurn), Quaternion.Euler(_profile.TwoHandedTurn), _twoHand);

        /// <summary>0 held in one hand, 1 held in both; smoothly blended between.</summary>
        public float TwoHandBlend => _twoHand;

        /// <summary>Per-frame action and look-trail offset in view space (fire kick, reload dip, sprint carry, climb drop, look shift) for the body view.</summary>
        public Vector3 BodyViewMotionOffset { get; private set; }

        /// <summary>Per-frame weapon rotation in view space for the body view, combining <see cref="RestTurn"/> with look lag, lean counter-roll, sprint carry, fire kick, reload tilt, and climb tilt.</summary>
        public Quaternion BodyViewMotionRotation { get; private set; } = Quaternion.identity;

        /// <summary>The muzzle of the held weapon, or null while none is equipped.</summary>
        public Transform Muzzle => _body != null ? _body.Muzzle : null;

        /// <summary>
        /// Changes what the hands hold on this player's body. Call on every machine when a
        /// player's held weapon changes; nothing here is synced.
        /// </summary>
        /// <param name="hold">The model and where each hand sits on it.</param>
        public void Equip(in WeaponHold hold)
        {
            if (hold.Model == null)
                return;

            _twoHanded = hold.TwoHanded && hold.HasSupport;
            if (_body != null)
                _body.Equip(hold);
        }

        /// <summary>True from the start of a reload move until the gun is back on aim.</summary>
        public bool IsReloading => _reloadElapsed >= 0f;

        /// <summary>
        /// Set true by weapon code that calls <see cref="Fire"/> and <see cref="Reload"/> itself.
        /// The stand-in input (<see cref="ReadTestInput"/>) then does nothing.
        /// </summary>
        public bool Driven { get; set; }

        /// <summary>Kicks the gun. Does nothing on players this client does not control, or during a reload.</summary>
        public void Fire()
        {
            if (!_active || IsReloading)
                return;

            _kick = 1f;
            _kickSide = -_kickSide;
        }

        /// <summary>Starts the reload move. Does nothing on players this client does not control, or while one is already playing.</summary>
        /// <param name="seconds">How long the move lasts, to match the weapon's reload. Zero or less uses the profile's.</param>
        public void Reload(float seconds = 0f)
        {
            if (!_active || IsReloading)
                return;

            _reloadSeconds = seconds > 0f ? seconds : _profile.ReloadSeconds;
            _reloadElapsed = 0f;
        }

        /// <summary>Stops a reload move early and brings the gun back up from where it has got to.</summary>
        public void CancelReload()
        {
            if (IsReloading && _reloadElapsed < _reloadSeconds * 0.5f)
                _reloadElapsed = _reloadSeconds - _reloadElapsed;
        }

        void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_actions != null)
            {
                _attack = _actions.FindAction(AttackAction, false);
                _reload = _actions.FindAction(ReloadAction, false);
            }
#endif
            _hits = GetComponent<PlayerHits>();

            // Every player's body, not only the local one: this is what both the owner and others see.
            _animator = GetComponentInChildren<PlayerAnimator>(true);
            if (_animator != null && _motor != null && _profile != null && _armsPrefab != null)
            {
                _twoHanded = _armsPrefab.TwoHanded;
                _twoHand = _twoHanded ? 1f : 0f;
                _body = PlayerHeldWeapon.Attach(_animator.gameObject, _motor, this, _profile, _armsPrefab);
            }
        }

        void OnEnable()
        {
            PlayerMotor.LocalChanged += OnLocalChanged;
            OnLocalChanged(PlayerMotor.Local);
        }

        void OnDisable()
        {
            PlayerMotor.LocalChanged -= OnLocalChanged;
            Hide();
        }

        void OnLocalChanged(PlayerMotor local)
        {
            bool isLocal = _motor != null && local == _motor;
            if (isLocal && !_active)
                Show();
            else if (!isLocal && _active)
                Hide();
        }

        void Update()
        {
            // For every player, not only the local one: the body's gun follows the same place.
            float targetTwoHand = _twoHanded ? 1f : 0f;
            if (_profile != null && !Mathf.Approximately(_twoHand, targetTwoHand))
            {
                _twoHand = Mathf.SmoothDamp(_twoHand, targetTwoHand, ref _twoHandVelocity, _profile.TwoHandedBlendSeconds, Mathf.Infinity, Time.deltaTime);
                if (Mathf.Abs(_twoHand - targetTwoHand) < 0.0001f)
                {
                    _twoHand = targetTwoHand;
                    _twoHandVelocity = 0f;
                }
            }

            if (!_active)
                return;

            if (!_bodyViewAttached)
                AttachBodyViewCamera();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Driven)
                ReadTestInput();
#endif
        }

        void Show()
        {
            PlayerCamera playerCamera = PlayerCamera.Instance;
            if (playerCamera == null || _profile == null)
                return;

            _baseCamera = playerCamera.GetComponent<Camera>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int layer = LayerMask.NameToLayer(LayerName);
            _standInBulletMask = layer >= 0 ? ~(1 << layer) : ~0;
#endif

            _lastCameraRotation = _baseCamera.transform.rotation;
            _lag = Vector2.zero;
            _lagVelocity = Vector2.zero;
            _sprint = 0f;
            _climb = 0f;
            _kick = 0f;
            _reloadElapsed = -1f;
            BodyViewMotionOffset = Vector3.zero;
            BodyViewMotionRotation = RestTurn;

            _active = true;
            _bodyViewAttached = false;
            _motor.SetOwnBodyShown(true);
            AttachBodyViewCamera();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        void AttachBodyViewCamera()
        {
            if (_head == null && _animator != null)
                _head = _animator.HeadBone;

            PlayerCamera eye = PlayerCamera.Instance;
            if (eye == null || _head == null)
                return;

            eye.SetBodyView(_head, _profile.BodyViewHeadFollow, _profile.BodyViewSmoothing, _profile.BodyViewForward, _profile.BodyViewMaxOffset);
            _bodyViewAttached = true;
        }

        void Hide()
        {
            if (!_active)
                return;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Shader.SetGlobalVector(BodyViewHeadSphereId, Vector4.zero);

            if (_motor != null)
                _motor.SetOwnBodyShown(false);
            if (PlayerCamera.Instance != null)
                PlayerCamera.Instance.ClearBodyView();

            _active = false;
            _bodyViewAttached = false;
            _baseCamera = null;
        }

        /// <summary>
        /// Sets the world-space head-clip sphere around the owner's real head bone for the
        /// owner's camera only, and clears it for mirrors, shadows, and every other camera.
        /// </summary>
        void OnBeginCameraRendering(ScriptableRenderContext context, Camera rendering)
        {
            if (_active && _head != null && rendering == _baseCamera && _profile.BodyViewHeadClip > 0f)
            {
                Vector3 centre = _head.position + _head.up * HeadClipCenterUp;
                float r = _profile.BodyViewHeadClip;
                Shader.SetGlobalVector(BodyViewHeadSphereId, new Vector4(centre.x, centre.y, centre.z, r * r));
            }
            else
            {
                Shader.SetGlobalVector(BodyViewHeadSphereId, Vector4.zero);
            }
        }

        /// <summary>Updates look lag, sprint carry, climb drop, fire kick, and reload motion for the body's held weapon.</summary>
        void LateUpdate()
        {
            if (!_active || _baseCamera == null || _profile == null)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            UpdateLag(dt);
            UpdateStance(dt);
            _kick = Mathf.MoveTowards(_kick, 0f, dt / _profile.KickRecoverSeconds);
            float kick = _kick * _kick;
            float reload = ReloadDepth(dt);

            float leanTilt = Mathf.DeltaAngle(0f, _baseCamera.transform.eulerAngles.z);
            float bodyRoll = _lag.y * _profile.LookLagRoll - leanTilt * _profile.LeanCounterRoll;

            BodyViewMotionOffset = new Vector3(_lag.y, -_lag.x, 0f) * _profile.LookLagShift
                + _profile.SprintShift * _sprint
                + Vector3.back * (_profile.KickBack * kick)
                + Vector3.down * (_profile.ReloadDrop * reload + _profile.ClimbDrop * _climb);

            BodyViewMotionRotation = Quaternion.Euler(_lag.x, _lag.y, bodyRoll)
                * RestTurn
                * Quaternion.Euler(_profile.SprintTurn * _sprint)
                * Quaternion.Euler(
                    -_profile.KickPitch * kick - _profile.ReloadTilt * reload + _profile.ClimbTilt * _climb,
                    0f,
                    _profile.KickRoll * kick * _kickSide + _profile.ReloadRoll * reload);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void ReadTestInput()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            if (_attack != null && _attack.WasPressedThisFrame() && !IsReloading)
            {
                Fire();
                StandInStrike(StandInBulletRange, StandInBulletImpulse, HitType.Bullet);
            }

            Keyboard keyboard = Keyboard.current;
            bool reloadPressed = _reload != null
                ? _reload.WasPressedThisFrame()
                : keyboard != null && keyboard.rKey.wasPressedThisFrame;
            if (reloadPressed)
                Reload();
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
                TwoHanded = !TwoHanded;
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame && !IsReloading)
                StandInStrike(StandInMeleeReach, StandInMeleeImpulse, HitType.Melee);
        }

        void StandInStrike(float reach, float impulse, HitType kind)
        {
            Transform eye = _baseCamera.transform;
            if (!Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, reach, _standInBulletMask, QueryTriggerInteraction.Ignore))
                return;

            var dummy = hit.collider.GetComponentInParent<HitDummy>();
            if (dummy != null)
            {
                dummy.Hit(new HitInfo
                {
                    Point = hit.point,
                    Direction = eye.forward,
                    Impulse = impulse,
                    Type = kind
                });
                return;
            }

            var victim = hit.collider.GetComponentInParent<PlayerHits>();
            if (victim != null && victim != _hits && _hits != null)
                _hits.RequestStandInHit(eye.position, eye.forward, kind);
        }
#endif

        void UpdateStance(float dt)
        {
            MovementProfile movement = _motor.Profile;
            MoveState state = _motor.State;

            float sprint = state == MoveState.Grounded && movement.SprintSpeed > movement.WalkSpeed
                ? Mathf.InverseLerp(movement.WalkSpeed, movement.SprintSpeed, _motor.PlanarSpeed)
                : 0f;
            _sprint = Mathf.SmoothDamp(_sprint, sprint, ref _sprintVelocity, _profile.SprintBlendSeconds, Mathf.Infinity, dt);

            float climb = state == MoveState.Mantling ? 1f : 0f;
            _climb = Mathf.SmoothDamp(_climb, climb, ref _climbVelocity, _profile.ClimbBlendSeconds, Mathf.Infinity, dt);
        }

        float ReloadDepth(float dt)
        {
            if (_reloadElapsed < 0f)
                return 0f;

            _reloadElapsed += dt;
            if (_reloadElapsed >= _reloadSeconds)
            {
                _reloadElapsed = -1f;
                return 0f;
            }

            float arch = Mathf.Sin(_reloadElapsed / _reloadSeconds * Mathf.PI);
            return arch * arch;
        }

        void UpdateLag(float dt)
        {
            Quaternion now = _baseCamera.transform.rotation;
            Vector3 previousForward = Quaternion.Inverse(now) * (_lastCameraRotation * Vector3.forward);
            _lastCameraRotation = now;

            float yawRate = Mathf.Atan2(previousForward.x, previousForward.z) * Mathf.Rad2Deg / dt;
            float pitchRate = -Mathf.Atan2(previousForward.y, previousForward.z) * Mathf.Rad2Deg / dt;

            float limit = _profile.LookLagLimit;
            var target = new Vector2(
                Mathf.Clamp(pitchRate * _profile.LookLagSeconds, -limit, limit),
                Mathf.Clamp(yawRate * _profile.LookLagSeconds, -limit, limit));
            _lag = Vector2.SmoothDamp(_lag, target, ref _lagVelocity, _profile.LookLagSettleSeconds, Mathf.Infinity, dt);
        }
    }
}
