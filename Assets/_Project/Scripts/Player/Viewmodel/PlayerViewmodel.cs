using CloseTheDeal.Customisation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CloseTheDeal.Player
{
    /// <summary>Drives the local player's true-POV body view, head-clip sphere, and held-weapon motion, and attaches <see cref="PlayerHeldWeapon"/> to every player body.</summary>
    [DisallowMultipleComponent]
    // Runs after PlayerCamera (0) and before PlayerAnimator (100) / PlayerHeldWeapon (105).
    [DefaultExecutionOrder(PlaceOrder)]
    public sealed class PlayerViewmodel : MonoBehaviour
    {
        const int PlaceOrder = 50;
        const float HeadClipCenterUp = 0.155f;

        // The reload's beats, as fractions of its length. The magazine leaves, seats and the slide goes
        // where CombatSounds plays their sounds (0.15, 0.6, 0.85).
        const float ReloadRaisedAt = 0.14f;
        const float ReloadMagazineFreeAt = 0.13f;
        const float ReloadMagazineOutAt = 0.22f;
        const float ReloadMagazineGoneAt = 0.36f;
        const float ReloadHandComesAt = 0.28f;
        const float ReloadNewMagazineAt = 0.40f;
        const float ReloadPushAt = 0.52f;
        const float ReloadSeatedAt = 0.60f;
        const float ReloadHandLeavesAt = 0.62f;
        const float ReloadHandGoneAt = 0.76f;
        const float ReloadRackAt = 0.80f;
        const float ReloadLowerAt = 0.84f;
        const float ReloadNewMagazineRise = 0.12f;
        const float ReloadMagazineThrow = 0.06f;
        const float ReloadBumpPitch = 3f;
        const float ReloadCancelSeconds = 0.15f;

        // A gun that has just come out: how long it takes to rise into the hands, from how far below, muzzle how far down.
        const float DrawSeconds = 0.22f;
        const float DrawDrop = 0.16f;
        const float DrawTilt = 28f;

        /// <summary>Layer name kept for greybox setup and stand-in raycast mask compatibility.</summary>
        public const string LayerName = "Viewmodel";

        internal const float StandInBulletRange = 100f;
        internal const float StandInBulletImpulse = 20f;
        internal const float StandInMeleeReach = 2f;
        internal const float StandInMeleeImpulse = 60f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const string AttackAction = "Player/Attack";
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
        bool _wantActive;
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
        float _kickElapsed = -1f;
        bool _kickFresh;
        GunKick _kickNow;
        float _kickSide = 1f;
        float _reloadElapsed = -1f;
        float _reloadSeconds;
        float _reloadFade = 1f;
        bool _reloadCancelled;
        bool _reloadRacked;
        float _reloadRaise;
        float _reloadFlick;
        float _reloadBump;
        HeldWeapon _held;
        bool _equipped;
        float _drawElapsed = -1f;
        PlayerHeldWeapon _body;
        PlayerHits _hits;

        /// <summary>True while this client is running true-POV first person for this player.</summary>
        public bool IsShown => _active;

        /// <summary>Client-local two-handed grip toggle; starts from the template prefab.</summary>
        public bool TwoHanded
        {
            get => _twoHanded;
            set => _twoHanded = value;
        }

        /// <summary>Where the gun's grip rests relative to the eye right now, in metres: the one-handed place, the two-handed place, or part way between while it moves.</summary>
        public Vector3 RestPosition => _held != null ? _held.HoldPosition : Vector3.Lerp(_profile.GunPosition, _profile.TwoHandedPosition, _twoHand);

        /// <summary>How the gun is turned at rest right now, blended the same way as <see cref="RestPosition"/>.</summary>
        public Quaternion RestTurn => _held != null ? Quaternion.Euler(_held.HoldTurn) : Quaternion.Slerp(Quaternion.Euler(_profile.GunTurn), Quaternion.Euler(_profile.TwoHandedTurn), _twoHand);

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

        /// <summary>
        /// Puts a weapon's own held model in the hands, held and reloaded its own way; null puts the arms
        /// prefab's stand-in pistol back. Call on every machine when a player's held weapon changes; nothing
        /// here is synced. A gun that was already in the hands is left as it is.
        /// </summary>
        public void Equip(HeldWeapon held)
        {
            if (held != null && !held.IsComplete)
                held = null;
            if (held == _held && _equipped)
                return;

            _equipped = true;
            _held = held;
            EndReload();
            if (held != null)
            {
                Equip(held.ToHold());
            }
            else
            {
                _twoHanded = _armsPrefab != null && _armsPrefab.TwoHanded;
                if (_body != null)
                    _body.EquipDefault();
            }

            // A gun that has just come out rises into the hands rather than appearing in them.
            _drawElapsed = 0f;
        }

        /// <summary>True from the start of a reload move until the gun is back on aim.</summary>
        public bool IsReloading => _reloadElapsed >= 0f;

        /// <summary>
        /// Set true by weapon code that calls <see cref="Fire"/> and <see cref="Reload"/> itself.
        /// The stand-in input (<see cref="ReadTestInput"/>) then does nothing.
        /// </summary>
        public bool Driven { get; set; }

        /// <summary>Kicks the gun with the profile's stand-in kick. Does nothing on players this client does not control, or during a reload.</summary>
        public void Fire()
        {
            if (_profile == null)
                return;

            _kickSide = -_kickSide;
            Fire(new GunKick(_profile.KickPitch, _profile.KickBack, _profile.KickRoll * _kickSide, 0f, _profile.KickRecoverSeconds, null));
        }

        /// <summary>Kicks the gun with a weapon's own kick. Does nothing on players this client does not control, or during a reload.</summary>
        public void Fire(in GunKick kick)
        {
            if (!_active || IsReloading)
                return;

            _kickNow = kick;
            _kickElapsed = 0f;
            _kickFresh = true;
        }

        /// <summary>Snaps the held weapon's slide back and lets it return. Call on every machine for every shot; a weapon with no slide ignores it.</summary>
        public void CycleSlide()
        {
            if (_body != null)
                _body.CycleSlide();
        }

        /// <summary>Pulls the held weapon's slide back and lets it go. A weapon with no slide ignores it.</summary>
        public void RackSlide()
        {
            if (_body != null)
                _body.RackSlide();
        }

        /// <summary>
        /// Pulls the held weapon's slide back and keeps it there: the answer to a trigger pull on an empty
        /// magazine. The next reload lets it go as its last beat; <see cref="ReleaseSlide"/> does it at once.
        /// </summary>
        public void LockSlide()
        {
            if (_body != null)
                _body.SetSlideLocked(true);
        }

        /// <summary>Lets a slide held back by <see cref="LockSlide"/> slam forward. Does nothing when it isn't held.</summary>
        public void ReleaseSlide()
        {
            if (_body != null)
                _body.SetSlideLocked(false);
        }

        /// <summary>How far the gun is into its held-up reload pose, 0 on the aim to 1 fully up.</summary>
        public float ReloadPose => Mathf.Clamp01(_reloadRaise);

        /// <summary>How far the magazine has slid out of the grip during a reload, 0 seated to 1 fully out.</summary>
        public float ReloadMagazineOut { get; private set; }

        /// <summary>How far a magazine that is out of the gun is from the grip along the grip's own line, in metres: the old one thrown clear, or the new one coming in.</summary>
        public float ReloadMagazineAway { get; private set; }

        /// <summary>How far the thrown-out magazine has fallen toward the floor, in metres.</summary>
        public float ReloadMagazineDrop { get; private set; }

        /// <summary>False between the old magazine dropping out of sight and the new one appearing.</summary>
        public bool ReloadMagazineShown { get; private set; } = true;

        /// <summary>How far the left hand has come to the bottom of the grip with the new magazine, 0 to 1.</summary>
        public float ReloadHand { get; private set; }

        /// <summary>How far below the grip the left hand's magazine is, in metres.</summary>
        public float ReloadHandBelow { get; private set; }

        /// <summary>Starts the reload move. Does nothing on players this client does not control, or while one is already playing.</summary>
        /// <param name="seconds">How long the move lasts, to match the weapon's reload. Zero or less uses the profile's.</param>
        public void Reload(float seconds = 0f)
        {
            if (!_active || (IsReloading && !_reloadCancelled))
                return;

            _reloadSeconds = seconds > 0f ? seconds : _profile.ReloadSeconds;
            _reloadElapsed = 0f;
            _reloadFade = 1f;
            _reloadCancelled = false;
            _reloadRacked = false;
        }

        /// <summary>Stops a reload move early: the gun comes back onto the aim from where it has got to and the magazine goes back in.</summary>
        public void CancelReload()
        {
            if (IsReloading)
                _reloadCancelled = true;
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
            _wantActive = false;
            Hide();
        }

        void OnLocalChanged(PlayerMotor local)
        {
            _wantActive = _motor != null && local == _motor;
            if (_wantActive && !_active)
                Show();
            else if (!_wantActive && _active)
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

            if (_wantActive && !_active)
                Show();

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
            _kickElapsed = -1f;
            EndReload();
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

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera rendering)
        {
            bool fitting = FittingRoom.Instance != null && FittingRoom.Instance.IsOpen;
            bool ragdollView = PlayerCamera.Instance != null && PlayerCamera.Instance.InRagdollView;
            if (_active && !fitting && !ragdollView && _head != null && rendering == _baseCamera && _profile.BodyViewHeadClip > 0f)
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

        void LateUpdate()
        {
            if (!_active || _baseCamera == null || _profile == null)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            UpdateLag(dt);
            UpdateStance(dt);
            float kick = KickAmount(dt);
            StepReload(dt);

            float leanTilt = Mathf.DeltaAngle(0f, _baseCamera.transform.eulerAngles.z);
            float bodyRoll = _lag.y * _profile.LookLagRoll - leanTilt * _profile.LeanCounterRoll;

            // The reload's knock comes along the grip, the way the left hand pushes the magazine in.
            Vector3 reloadShift = _held != null ? _held.ReloadShift : _profile.ReloadShift;
            Quaternion reloadTurn = Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.Euler(_held != null ? _held.ReloadTurn : _profile.ReloadTurn), _reloadRaise);
            float draw = DrawDepth(dt);

            BodyViewMotionOffset = new Vector3(_lag.y, -_lag.x, 0f) * _profile.LookLagShift
                + _profile.SprintShift * _sprint
                + Vector3.back * (_kickNow.PushBack * kick)
                + reloadShift * _reloadRaise
                + reloadTurn * Vector3.up * (_profile.ReloadSeatBump * _reloadBump)
                + Vector3.down * (_profile.ClimbDrop * _climb + DrawDrop * draw);

            BodyViewMotionRotation = Quaternion.Euler(_lag.x, _lag.y, bodyRoll)
                * RestTurn
                * Quaternion.Euler(_profile.SprintTurn * _sprint)
                * reloadTurn
                * Quaternion.Euler(
                    -_kickNow.Flip * kick - ReloadBumpPitch * _reloadBump + _profile.ClimbTilt * _climb + DrawTilt * draw,
                    _kickNow.Twist * kick,
                    _kickNow.Roll * kick + _profile.ReloadEjectFlick * _reloadFlick);
        }

        /// <summary>How much of the current kick is left: 1 on the shot's own frame, then along its curve (which may swing past 0) to rest.</summary>
        float KickAmount(float dt)
        {
            if (_kickElapsed < 0f)
                return 0f;

            // The peak is held for the frame the shot was drawn on, so a kick is never skipped at low frame rates.
            if (_kickFresh)
                _kickFresh = false;
            else
                _kickElapsed += dt;

            float seconds = Mathf.Max(0.01f, _kickNow.Seconds);
            if (_kickElapsed >= seconds)
            {
                _kickElapsed = -1f;
                return 0f;
            }

            float t = _kickElapsed / seconds;
            AnimationCurve curve = _kickNow.Curve;
            if (curve != null && curve.length > 0)
                return curve.Evaluate(t);
            float left = 1f - t;
            return left * left;
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

        /// <summary>
        /// Moves the reload on: the gun comes up, the magazine slides out and falls, the left hand brings a new
        /// one up and pushes it in, the slide is racked and the gun goes back onto the aim. A cancelled reload
        /// stops where it is and fades everything back to rest.
        /// </summary>
        void StepReload(float dt)
        {
            if (_reloadElapsed < 0f)
                return;

            if (_reloadCancelled)
                _reloadFade -= dt / ReloadCancelSeconds;
            else
                _reloadElapsed += dt;

            if (_reloadElapsed >= _reloadSeconds || _reloadFade <= 0f)
            {
                EndReload();
                return;
            }

            float t = _reloadElapsed / _reloadSeconds;
            float fade = _reloadFade;

            _reloadRaise = SnapUp(Mathf.Clamp01(t / ReloadRaisedAt), _profile.ReloadOvershoot)
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ReloadLowerAt, 1f, t)))
                * fade;
            _reloadFlick = Jolt(Mathf.InverseLerp(ReloadMagazineFreeAt, ReloadMagazineGoneAt, t)) * fade;
            _reloadBump = Jolt(Mathf.InverseLerp(ReloadSeatedAt - 0.01f, ReloadHandGoneAt, t)) * fade;

            float length = _held != null ? _held.MagazineLength : _profile.ReloadMagazineLength;
            if (t < ReloadNewMagazineAt)
            {
                // Thrown out along the grip by the flick, then it drops.
                float slid = Mathf.InverseLerp(ReloadMagazineFreeAt, ReloadMagazineOutAt, t);
                float fallen = Mathf.InverseLerp(ReloadMagazineOutAt, ReloadMagazineGoneAt, t);
                ReloadMagazineOut = slid * slid * fade;
                ReloadMagazineAway = fallen * ReloadMagazineThrow * fade;
                ReloadMagazineDrop = fallen * fallen * _profile.ReloadMagazineFall * fade;
                ReloadMagazineShown = t < ReloadMagazineGoneAt || _reloadCancelled;
                ReloadHandBelow = ReloadNewMagazineRise + length;
            }
            else
            {
                // The new one comes up the line of the grip in the left hand and is pushed home.
                float pushed = Mathf.InverseLerp(ReloadPushAt, ReloadSeatedAt, t);
                ReloadMagazineOut = (1f - pushed * pushed) * fade;
                ReloadMagazineAway = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ReloadNewMagazineAt, ReloadPushAt, t))) * ReloadNewMagazineRise * fade;
                ReloadMagazineDrop = 0f;
                ReloadMagazineShown = true;
                ReloadHandBelow = ReloadMagazineAway + ReloadMagazineOut * length;
            }

            ReloadHand = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ReloadHandComesAt, ReloadNewMagazineAt, t))
                * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ReloadHandLeavesAt, ReloadHandGoneAt, t)))
                * fade * _profile.ReloadHandReach;

            if (!_reloadRacked && !_reloadCancelled && t >= ReloadRackAt)
            {
                _reloadRacked = true;
                // An empty gun's slide is already back: it only has to be let go. Otherwise it is racked.
                if (_body != null && _body.SlideLocked)
                    ReleaseSlide();
                else
                    RackSlide();
            }
        }

        void EndReload()
        {
            _reloadElapsed = -1f;
            _reloadFade = 1f;
            _reloadCancelled = false;
            _reloadRaise = 0f;
            _reloadFlick = 0f;
            _reloadBump = 0f;
            ReloadMagazineOut = 0f;
            ReloadMagazineAway = 0f;
            ReloadMagazineDrop = 0f;
            ReloadMagazineShown = true;
            ReloadHand = 0f;
            ReloadHandBelow = 0f;
        }

        /// <summary>How far below its place a gun that has just come out still is: 1 on the frame it is drawn, easing to 0.</summary>
        float DrawDepth(float dt)
        {
            if (_drawElapsed < 0f)
                return 0f;

            _drawElapsed += dt;
            if (_drawElapsed >= DrawSeconds)
            {
                _drawElapsed = -1f;
                return 0f;
            }

            float left = 1f - _drawElapsed / DrawSeconds;
            return left * left;
        }

        /// <summary>0 to 1 that arrives fast, swings past 1 by about the overshoot and comes back.</summary>
        static float SnapUp(float t, float overshoot)
        {
            float back = overshoot * 10f;
            float left = t - 1f;
            return 1f + (back + 1f) * left * left * left + back * left * left;
        }

        /// <summary>A knock: up fast over the first quarter, then settling. 0 at both ends.</summary>
        static float Jolt(float t)
        {
            if (t <= 0f || t >= 1f)
                return 0f;
            if (t < 0.25f)
                return t / 0.25f;
            float left = 1f - (t - 0.25f) / 0.75f;
            return left * left;
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
