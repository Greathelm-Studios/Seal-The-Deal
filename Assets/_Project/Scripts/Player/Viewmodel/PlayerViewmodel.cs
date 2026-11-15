// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "set up an fps viewmodel with the characters model we are using"
//  Modified  : Claude Fable 5.2, 2026-11-13 — framing and posing taken from NorseFPS at
//              Alex's request: this now places the gun in the view (sway, bob, kick and the
//              reload dip all move the gun) and the arms prefab reaches the hand to it.
//  Modified  : Claude Fable 5.2, 2026-11-14 — Harvey: the view is "super unrefined", make
//              it "in line with the movement and more clean". The gun now carries weight from
//              the player's own movement (starts, stops, strafes, jumps, landings, leans), the
//              step bob is a smooth arc, and it has a sprint carry, a climb drop and breathing.
//  Modified  : Claude Fable 5.2, 2026-11-14 — a weapon can be held in one hand or both
//              (Harvey: "add a left and right hand system and a two handed system").
//  Modified  : Claude Fable 5.2, 2026-11-14 — weapon code can now drive it: swap the held model,
//              time and cancel a reload, and turn the stand-in keys off (Harvey: "Take the best
//              option and get it ready").
//  Modified  : Claude Fable 5.2, 2026-11-14 — the stand-in uses the Reload action when there
//              is one (Adam's review lane).
//  Modified  : Claude Fable 5.2, 2026-11-14 — the stand-in shot also sends a local ray that
//              hits the test dummy (Alex: "i want it so when i actuall shoot him with my gun").
//  Modified  : Claude Opus 5.5, 2026-11-15 — the stand-in shot also hits other players
//              through the host, and V swings a stand-in melee hit (Harvey: "add hit
//              reactions when hit by guns/melee weapons for the players so that when a shooter
//              hits another player, they see a visible reaction in the person").
//  Modified  : Claude Fable 5.2, 2026-11-15 — an experimental body view on F5: look out of the
//              real body, with the separate arms hidden (Harvey: "what about true POV for
//              the player? Could we try that out?").
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Sits on the player prefab. For the player this client controls, and nobody
//              else, it hangs the first-person arms on the camera, draws them with a camera
//              of their own so walls cannot cut into them, and adds sway, bob and fire kick.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Gives the local player first-person arms holding a weapon.
    /// </summary>
    /// <remarks>
    /// <para><b>Ownership (AGENTS.md §3):</b> each client builds the arms for its own player
    /// only. Nothing is synced and nothing reads them back; other players keep seeing the
    /// full third-person body.</para>
    /// <para><b>How the arms stay out of walls and out of other views:</b> they are drawn by a
    /// second camera stacked on the player's camera, which draws after the world and ignores
    /// the world's depth. The arms are switched off for every other camera just before it
    /// renders, so the dev mirror, a spectator view or the Scene view never show them.
    /// WHY not exclude their layer from the other cameras instead: those cameras are scene
    /// objects other people own, and their layer lists are authored values (§4).
    /// WHY not a URP Render Objects feature: that is set on the shared renderer asset and
    /// would change every scene's rendering for one feature.</para>
    /// <para><b>Lifetime:</b> the arms and their camera are created when this player becomes
    /// the local one and destroyed when it stops being it or is destroyed. The player's
    /// camera gets one entry added to its camera stack, removed again on the way out.</para>
    /// <para><b>Posing:</b> this component decides where the gun is each frame; the arms
    /// prefab (<see cref="ViewmodelArms"/>) bends the arm so the hand stays on it. The approach
    /// and the starting numbers are NorseFPS's.</para>
    /// <para><b>The body:</b> every player's body, local or not, also gets a
    /// <see cref="PlayerHeldWeapon"/> at spawn, which holds the same weapon the same way for
    /// everyone else to see.</para>
    /// <para><b>Driving it:</b> weapon code calls <see cref="Equip"/> when the held weapon
    /// changes (on every machine, so the body changes too), <see cref="Fire"/> for a shot and
    /// <see cref="Reload"/> / <see cref="CancelReload"/>, and sets <see cref="Driven"/>. Until
    /// that code exists this component listens to the attack button and the R key itself;
    /// see <see cref="ReadTestInput"/>.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // From weapon code, on every machine, when a player's held weapon changes:
    /// _viewmodel.Driven = true;
    /// _viewmodel.Equip(new WeaponHold(rifleModel, gripSeat, gripTurn, supportSeat, supportTurn, true));
    /// // On the owning client, when a shot has been fired:
    /// _viewmodel.Fire();
    /// </code>
    /// </example>
    [DisallowMultipleComponent]
    // WHY: the gun's motion is measured from where the camera is and how it is turned this
    // frame. PlayerCamera (default order) places the camera in its LateUpdate, so this has to
    // run after it or every reading is a frame old; ViewmodelArms solves the arm after both.
    [DefaultExecutionOrder(PlaceOrder)]
    public sealed class PlayerViewmodel : MonoBehaviour
    {
        /// <summary>After PlayerCamera (0), before ViewmodelArms' solve (100).</summary>
        const int PlaceOrder = 50;

        /// <summary>Longest slice the movement spring is advanced in, in seconds. Short enough to stay stable at the stiffest setting the profile allows.</summary>
        const float SpringStep = 1f / 120f;

        /// <summary>Share of the gun's movement drift that the shoulder follows. All of it: the arm is held at 99% of its reach, so any drift the shoulder did not follow would pull the grip out of the hand.</summary>
        const float ShoulderFollow = 1f;

        /// <summary>Most slices per frame; a frame longer than this many slices is treated as that long, so a hitch cannot fling the gun.</summary>
        const int MaxSpringSteps = 8;

        /// <summary>The layer the arms are on, so their own camera can draw them and nothing else. Named by the greybox tool.</summary>
        public const string LayerName = "Viewmodel";

        /// <summary>Input action for the attack button, in the actions asset's Player map.</summary>
        const string AttackAction = "Player/Attack";

        /// <summary>Input action for reload, in the actions asset's Player map. Missing on some branches; the R key is read then.</summary>
        const string ReloadAction = "Player/Reload";

        /// <summary>How far the stand-in bullet reaches, in metres. Longer than any room in the test scenes. The host's check (<see cref="PlayerHits"/>) uses the same.</summary>
        internal const float StandInBulletRange = 100f;

        /// <summary>
        /// Push a stand-in pistol bullet gives what it hits, in newton-seconds. The figure
        /// agreed with the hit-reaction work for a pistol round on the 49 kg ragdoll.
        /// </summary>
        internal const float StandInBulletImpulse = 20f;

        /// <summary>How far the stand-in melee swing reaches from the eye, in metres. WHY 2: an arm and a short club past the front of the 0.35 m capsule.</summary>
        internal const float StandInMeleeReach = 2f;

        /// <summary>Push the stand-in melee swing gives what it hits, in newton-seconds. WHY 60: three pistol shots, so a swing clearly outweighs a shot; a placeholder until a real melee weapon reports its own.</summary>
        internal const float StandInMeleeImpulse = 60f;

        /// <summary>Below this planar speed, in metres per second, the player counts as standing still and the bob fades out. Well above the jitter of a resting body.</summary>
        const float MovingSpeed = 0.2f;

        /// <summary>A full turn in radians, for the bob's sine.</summary>
        const float Tau = Mathf.PI * 2f;

        [Tooltip("The player this viewmodel belongs to. The arms only appear when this is the player you control.")]
        [SerializeField] PlayerMotor _motor;

        [Tooltip("The arms prefab that is put on the camera.")]
        [SerializeField] ViewmodelArms _armsPrefab;

        [Tooltip("Placement, sway, bob and kick tuning.")]
        [SerializeField] ViewmodelProfile _profile;

        [Tooltip("The Input System actions asset, read for the attack button until weapon code drives the arms.")]
        [SerializeField] InputActionAsset _actions;

        ViewmodelArms _arms;
        Camera _baseCamera;
        Camera _armsCamera;
        InputAction _attack;
        /// <summary>The reload action, or null when the actions asset has none.</summary>
        InputAction _reload;
        /// <summary>What the stand-in bullet can hit: every layer but the arms' own. Set when the arms are shown.</summary>
        int _standInBulletMask;

        Quaternion _lastCameraRotation;
        Vector2 _lag;
        Vector2 _lagVelocity;
        float _bobPhase;
        float _bobAmount;
        float _bobAmountVelocity;
        Vector3 _lastCameraPosition;
        /// <summary>The movement the gun is currently feeling, in metres per second in view space (right, up, forward). Chases the eye's real velocity on a spring.</summary>
        Vector3 _carry;
        Vector3 _carryVelocity;
        /// <summary>Last frame's upward eye speed, in metres per second; the fall speed at the moment of a landing.</summary>
        float _lastVerticalSpeed;
        bool _wasGrounded;
        float _sprint;
        float _sprintVelocity;
        float _climb;
        float _climbVelocity;
        float _breathPhase;
        bool _twoHanded;
        /// <summary>0 held in one hand, 1 held in both; eased between so the gun slides across.</summary>
        float _twoHand;
        float _twoHandVelocity;
        float _kick;
        /// <summary>Signed -1 or +1, picked per shot, so the kick's twist alternates sides.</summary>
        float _kickSide = 1f;
        /// <summary>Seconds into the current reload move; negative when none is playing.</summary>
        float _reloadElapsed = -1f;
        /// <summary>How long the current reload move lasts, in seconds.</summary>
        float _reloadSeconds;
        PlayerHeldWeapon _body;
        /// <summary>This player's hit taker, which the stand-in shot and swing ask the host through. Null on a player prefab without one.</summary>
        PlayerHits _hits;
        /// <summary>What weapon code last asked the hands to hold; used again when the arms are rebuilt.</summary>
        WeaponHold _hold;
        bool _hasHold;

        /// <summary>True while this client is showing arms for this player.</summary>
        public bool IsShown => _arms != null;

        /// <summary>
        /// Whether the weapon is held in both hands. Starts as the arms prefab says. Weapon code
        /// sets it when the held weapon changes; the gun then moves to the two-handed place and
        /// the left arm comes up, in the view and on the body.
        /// </summary>
        /// <remarks>Local to this machine: nothing sends it. It belongs with whatever syncs which weapon a player holds.</remarks>
        public bool TwoHanded
        {
            get => _twoHanded;
            set
            {
                _twoHanded = value;
                if (_arms != null)
                    _arms.TwoHanded = value;
            }
        }

        /// <summary>Where the gun's grip rests relative to the eye right now, in metres: the one-handed place, the two-handed place, or part way between while it moves.</summary>
        public Vector3 RestPosition => Vector3.Lerp(_profile.GunPosition, _profile.TwoHandedPosition, _twoHand);

        /// <summary>How the gun is turned at rest right now, blended the same way as <see cref="RestPosition"/>.</summary>
        public Quaternion RestTurn => Quaternion.Slerp(Quaternion.Euler(_profile.GunTurn), Quaternion.Euler(_profile.TwoHandedTurn), _twoHand);

        /// <summary>
        /// The muzzle of the first-person weapon: the child of the held model named "Muzzle", or
        /// the model itself when it has none. Null while this client shows no arms for this player.
        /// </summary>
        public Transform Muzzle { get; private set; }

        /// <summary>
        /// Changes what the hands hold, in the first-person view and on the body. Call on every
        /// machine when a player's held weapon changes; nothing here is synced. Not for every
        /// frame: it creates and destroys objects.
        /// </summary>
        /// <param name="hold">The model and where each hand sits on it.</param>
        public void Equip(in WeaponHold hold)
        {
            if (hold.Model == null)
                return;

            _hold = hold;
            _hasHold = true;
            _twoHanded = hold.TwoHanded && hold.HasSupport;
            if (_arms != null)
                Muzzle = FindMuzzle(_arms.Equip(hold));
            if (_body != null)
                _body.Equip(hold);
        }

        static Transform FindMuzzle(Transform model)
        {
            if (model == null)
                return null;

            foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == "Muzzle")
                    return part;
            }

            return model;
        }

        /// <summary>True from the start of a reload move until the gun is back on aim.</summary>
        public bool IsReloading => _reloadElapsed >= 0f;

        /// <summary>
        /// Set true by weapon code that calls <see cref="Fire"/> and <see cref="Reload"/> itself.
        /// The stand-in input (<see cref="ReadTestInput"/>: attack, reload, the T hand switch
        /// and the V swing) then does nothing, so one shot cannot kick the gun twice.
        /// </summary>
        /// <remarks>Local to this machine and not serialized: whatever drives the arms sets it when it starts, typically once at spawn.</remarks>
        public bool Driven { get; set; }

        /// <summary>Kicks the gun. Does nothing on players this client does not control, or during a reload.</summary>
        public void Fire()
        {
            if (_arms == null || IsReloading)
                return;

            _kick = 1f;
            _kickSide = -_kickSide;
        }

        /// <summary>Starts the reload move. Does nothing on players this client does not control, or while one is already playing.</summary>
        /// <param name="seconds">How long the move lasts, to match the weapon's reload. Zero or less uses the profile's.</param>
        public void Reload(float seconds = 0f)
        {
            if (_arms == null || IsReloading)
                return;

            _reloadSeconds = seconds > 0f ? seconds : _profile.ReloadSeconds;
            _reloadElapsed = 0f;
        }

        /// <summary>Stops a reload move early and brings the gun back up from where it has got to.</summary>
        public void CancelReload()
        {
            // The move is the same going down as coming up, so jumping to the matching point
            // on the way up carries on from the same depth with no pop.
            if (IsReloading && _reloadElapsed < _reloadSeconds * 0.5f)
                _reloadElapsed = _reloadSeconds - _reloadElapsed;
        }

        void Awake()
        {
            if (_actions != null)
            {
                _attack = _actions.FindAction(AttackAction, false);
                _reload = _actions.FindAction(ReloadAction, false);
            }
            _hits = GetComponent<PlayerHits>();

            // Every player's body, not only the local one: this is what the others see.
            var body = GetComponentInChildren<PlayerAnimator>(true);
            if (body != null && _motor != null && _profile != null && _armsPrefab != null)
            {
                _twoHanded = _armsPrefab.TwoHanded;
                _twoHand = _twoHanded ? 1f : 0f;
                _body = PlayerHeldWeapon.Attach(body.gameObject, _motor, this, _profile, _armsPrefab);
            }
        }

        /// <summary>
        /// The body view: the owner looks out of the real body (its own arms, gun and legs)
        /// and the separate first-person arms are not drawn. Off is the usual view. One switch
        /// for this machine, remembered between sessions; nothing is networked, since it only
        /// changes what this player's own screen draws.
        /// </summary>
        /// <remarks>An experiment (Harvey: "Instead of the set arm in your camera, what about true POV for the player?"): F5 switches it while playing, in the Editor and development builds.</remarks>
        public static bool BodyView
        {
            get => _bodyView ??= PlayerPrefs.GetInt(BodyViewPref, 0) != 0;
            set
            {
                _bodyView = value;
                PlayerPrefs.SetInt(BodyViewPref, value ? 1 : 0);
            }
        }

        const string BodyViewPref = "ctd.dev.bodyView";
        static bool? _bodyView;
        /// <summary>What the body view was the last time this player applied it; null before the first time.</summary>
        bool? _appliedBodyView;
        Transform _head;
        static readonly int BodyViewNearClipId = Shader.PropertyToID("_BodyViewNearClip");
        static readonly int BodyViewClipBackId = Shader.PropertyToID("_BodyViewClipBack");

        /// <summary>Applies the body view, or undoes it, for the player this machine controls.</summary>
        /// <remarks>
        /// The owner's own head is hidden from their own eye only, by a near clip the character
        /// shader applies for that one camera (see <see cref="OnBeginCameraRendering"/>). The
        /// head itself is untouched, so mirrors, shadows and other cameras show it.
        /// </remarks>
        void ApplyBodyView(bool on)
        {
            _appliedBodyView = on;
            _motor.SetOwnBodyShown(on);

            if (_head == null)
            {
                var animator = GetComponentInChildren<PlayerAnimator>(true);
                _head = animator != null ? animator.HeadBone : null;
                if (_head == null && on)
                    _appliedBodyView = null; // The skeleton is not found yet: try again next frame.
            }

            PlayerCamera eye = PlayerCamera.Instance;
            if (eye == null)
                return;
            if (on && _head != null)
                eye.SetBodyView(_head, _profile.BodyViewHeadFollow, _profile.BodyViewSmoothing, _profile.BodyViewForward, _profile.BodyViewMaxOffset);
            else
                eye.ClearBodyView();
        }

        void OnDisable()
        {
            Hide();
        }

        void Update()
        {
            // For every player, not only the local one: the body's gun follows the same place.
            if (_profile != null)
                _twoHand = Mathf.SmoothDamp(_twoHand, _twoHanded ? 1f : 0f, ref _twoHandVelocity, _profile.TwoHandedBlendSeconds, Mathf.Infinity, Time.deltaTime);

            // PlayerMotor.Local is set and cleared by the motor as ownership starts and stops;
            // comparing a reference each frame is cheaper than a second network callback here.
            bool local = _motor != null && PlayerMotor.Local == _motor;
            if (local && _arms == null)
                Show();
            else if (!local && _arms != null)
            {
                // Give the body back as it was before this player stopped being ours.
                if (_appliedBodyView == true)
                    ApplyBodyView(false);
                _appliedBodyView = null;
                Hide();
            }

            if (local && _profile != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Keyboard keys = Keyboard.current;
                if (keys != null && keys.f5Key.wasPressedThisFrame)
                    BodyView = !BodyView;
#endif
                if (_appliedBodyView != BodyView)
                    ApplyBodyView(BodyView);
            }

            if (_arms != null && !Driven)
                ReadTestInput();
        }

        /// <summary>
        /// Stand-in trigger until slice 2's weapon code calls <see cref="Fire"/> and
        /// <see cref="Reload"/>: the attack button kicks and shoots, R reloads, T switches between
        /// one hand and both, V swings a melee hit at whatever is within 2 m.
        /// </summary>
        /// <remarks>
        /// Skipped once weapon code sets <see cref="Driven"/>. Remove this method when every
        /// build has such code.
        /// WHY the R key is a fallback: the actions asset on this branch has no reload action;
        /// the movement lane's asset has one (Player/Reload), and it is used when present.
        /// Temporary limit: this reads the project's actions asset, not a player's rebound
        /// copy, so a rebound attack or reload key is not followed here.
        /// </remarks>
        void ReadTestInput()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            // Not during a reload: Fire() ignores the press then, and so must the bullet.
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
            // Stand-in until a two-handed weapon exists: T switches the pistol between one hand
            // and both. Only this machine sees the switch; remove with the rest of this method.
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
                TwoHanded = !TwoHanded;
            // Stand-in until a melee weapon exists: V swings at whatever is just in front.
            // Not during a reload, like the shot. Remove with the rest of this method.
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame && !IsReloading)
                StandInStrike(StandInMeleeReach, StandInMeleeImpulse, HitType.Melee);
        }

        /// <summary>
        /// Stand-in shot or swing, so the test dummy and other players can be hit before the
        /// weapons exist: one ray from the eye along the look. The dummy it lands on is hit
        /// here; another player it lands on is reported to the host, which checks the ray
        /// again and hits that player on every machine.
        /// </summary>
        /// <param name="reach">How far the ray goes, metres.</param>
        /// <param name="impulse">Push given to the dummy, newton-seconds. The host picks its own for a player.</param>
        /// <param name="kind">Bullet for the attack button, Melee for V.</param>
        /// <remarks>
        /// The dummy part is local and visual only, like the dummy. The player part sends only
        /// where the eye was and which way it looked (<see cref="PlayerHits.RequestStandInHit"/>);
        /// the host decides the hit (AGENTS.md §3). It is not the game's shooting, which lives
        /// with the weapon code; that code sets <see cref="Driven"/> and this never runs.
        /// THROWAWAY: remove with <see cref="ReadTestInput"/>.
        /// WHY from the eye and not the muzzle: the crosshair is the eye's line, and a shot
        /// from the gun's off-centre muzzle would miss what is under it at close range.
        /// The ray starts inside the player's own capsule, which a ray does not report.
        /// </remarks>
        void StandInStrike(float reach, float impulse, HitType kind)
        {
            Transform eye = _baseCamera.transform;
            if (!Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, reach, _standInBulletMask, QueryTriggerInteraction.Ignore))
                return;

            // Only on a hit, and only once per press, so the lookups are not a per-frame cost.
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

        void Show()
        {
            PlayerCamera playerCamera = PlayerCamera.Instance;
            int layer = LayerMask.NameToLayer(LayerName);
            if (playerCamera == null || _armsPrefab == null || _profile == null || layer < 0)
                return;

            _baseCamera = playerCamera.GetComponent<Camera>();

            var cameraObject = new GameObject("ViewmodelCamera");
            cameraObject.transform.SetParent(_baseCamera.transform, false);
            _armsCamera = cameraObject.AddComponent<Camera>();
            _armsCamera.cullingMask = 1 << layer;
            // Everything but the arms' own layer, so a held model with a collider cannot stop the shot.
            _standInBulletMask = ~(1 << layer);
            _armsCamera.fieldOfView = _profile.FieldOfView;
            _armsCamera.nearClipPlane = _profile.NearDistance;
            _armsCamera.farClipPlane = _profile.FarDistance;

            UniversalAdditionalCameraData baseData = _baseCamera.GetUniversalAdditionalCameraData();
            UniversalAdditionalCameraData armsData = _armsCamera.GetUniversalAdditionalCameraData();
            armsData.renderType = CameraRenderType.Overlay;
            armsData.renderPostProcessing = baseData.renderPostProcessing;
            baseData.cameraStack.Add(_armsCamera);

            // The arms prefab's own origin is the eye; its gun mount and shoulder are placed
            // from the profile every frame.
            _arms = Instantiate(_armsPrefab, cameraObject.transform);
            _arms.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (_hasHold)
                _arms.Equip(_hold);
            _arms.TwoHanded = _twoHanded;
            Muzzle = FindMuzzle(_arms.HeldModel);

            _lastCameraRotation = _baseCamera.transform.rotation;
            _lag = Vector2.zero;
            _lagVelocity = Vector2.zero;
            _bobAmount = 0f;
            _lastCameraPosition = _baseCamera.transform.position;
            _carry = Vector3.zero;
            _carryVelocity = Vector3.zero;
            _lastVerticalSpeed = 0f;
            _wasGrounded = true;
            _sprint = 0f;
            _climb = 0f;
            _kick = 0f;
            _reloadElapsed = -1f;

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        void Hide()
        {
            if (_arms == null)
                return;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Shader.SetGlobalFloat(BodyViewNearClipId, 0f);

            if (_baseCamera != null)
                _baseCamera.GetUniversalAdditionalCameraData().cameraStack.Remove(_armsCamera);
            if (_armsCamera != null)
                Destroy(_armsCamera.gameObject); // Takes the arms with it.

            _arms = null;
            Muzzle = null;
            _armsCamera = null;
            _baseCamera = null;
        }

        /// <summary>
        /// The arms are drawn by their own camera and by no other. In the body view, the camera
        /// the owner looks through also skips whatever of a character is right at the eye.
        /// </summary>
        /// <remarks>
        /// WHY a clip per camera and not a shrunken head bone, which the first version used: a
        /// bone is posed once a frame for every camera, so the mirror showed the player a
        /// headless reflection and the shadow had no head either (Harvey: "this definitely cant
        /// happen"). A value set here is seen only by the camera about to render.
        /// </remarks>
        void OnBeginCameraRendering(ScriptableRenderContext context, Camera rendering)
        {
            bool ownEye = _appliedBodyView == true && rendering == _baseCamera;
            Shader.SetGlobalFloat(BodyViewNearClipId, ownEye ? _profile.BodyViewHeadClip : 0f);
            // The eye sits BodyViewForward in front of the middle of the head, so the middle
            // of the head is that far behind the eye.
            Shader.SetGlobalFloat(BodyViewClipBackId, ownEye ? _profile.BodyViewForward : 0f);
            if (_arms != null)
                _arms.SetVisible(rendering == _armsCamera && !BodyView);
        }

        /// <summary>Places the gun for this frame: authored framing, then look lag, walk bob, fire kick and the reload dip on top. The arms prefab then reaches the hand to it.</summary>
        void LateUpdate()
        {
            if (_arms == null)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            // The tuning is read every frame so changes in the Inspector show while playing.
            _armsCamera.fieldOfView = _profile.FieldOfView;

            UpdateLag(dt);
            UpdateCarry(dt);
            UpdateBob(dt);
            UpdateStance(dt);
            _kick = Mathf.MoveTowards(_kick, 0f, dt / _profile.KickRecoverSeconds);
            // Squared so the gun snaps back fast at first and eases into rest.
            float kick = _kick * _kick;
            float reload = ReloadDepth(dt);

            // One swing is two steps: the gun is furthest to a side, and lowest, as each foot
            // lands. WHY sine squared for the height and not the absolute sine: the absolute
            // value has a corner at the top of every step, which showed as a hard bounce.
            float bobSwing = Mathf.Sin(_bobPhase);
            Vector3 bob = new Vector3(bobSwing * _profile.BobSide, -bobSwing * bobSwing * _profile.BobHeight, 0f) * _bobAmount;
            float breath = Mathf.Sin(_breathPhase) * _profile.BreathHeight * (1f - _bobAmount);
            Vector3 drift = -_carry * _profile.MoveLagSeconds;

            Vector3 position = RestPosition + bob
                // Trailing a turn slides the gun the way it is turned, not only twists it.
                + new Vector3(_lag.y, -_lag.x, 0f) * _profile.LookLagShift
                + drift
                + _profile.SprintShift * _sprint
                + Vector3.up * breath
                + Vector3.back * (_profile.KickBack * kick)
                + Vector3.down * (_profile.ReloadDrop * reload + _profile.ClimbDrop * _climb);

            // Rolls in view space: into a turn, behind a strafe, with the stride, and back
            // against a lean. The camera's own roll is the lean's tilt.
            float leanTilt = Mathf.DeltaAngle(0f, _baseCamera.transform.eulerAngles.z);
            float roll = _lag.y * _profile.LookLagRoll
                + _carry.x * _profile.StrafeRoll
                - bobSwing * _profile.BobRoll * _bobAmount
                - leanTilt * _profile.LeanCounterRoll;

            Quaternion rotation = Quaternion.Euler(_lag.x + _carry.y * _profile.AirTilt, _lag.y, roll)
                * RestTurn
                * Quaternion.Euler(_profile.SprintTurn * _sprint)
                * Quaternion.Euler(
                    -_profile.KickPitch * kick - _profile.ReloadTilt * reload + _profile.ClimbTilt * _climb,
                    0f,
                    _profile.KickRoll * kick * _kickSide + _profile.ReloadRoll * reload);
            // WHY the shoulder goes with the drift: the arm is held almost straight, so the
            // whole arm has to shift with the gun; there is no reach left for the gun alone.
            _arms.SetPose(position, rotation, _profile.ShoulderPosition + drift * ShoulderFollow);
        }

        /// <summary>
        /// Measures how the eye moved this frame and lets the gun feel it on a spring, so it
        /// hangs back from a start, swings on past a stop and dips after a landing.
        /// </summary>
        /// <remarks>
        /// WHY from the camera's own movement and not the body's velocity: the camera rides the
        /// smoothed visual, so this never sees a prediction replay or a tick step, and it picks
        /// up a lean's slide and a climb without asking the motor about either.
        /// WHY the spring is advanced in short slices: one long step at a low frame rate
        /// overshoots and can blow up; slices keep the same feel at any frame rate.
        /// </remarks>
        void UpdateCarry(float dt)
        {
            Transform view = _baseCamera.transform;
            Vector3 now = view.position;
            Vector3 velocity = view.InverseTransformDirection((now - _lastCameraPosition) / dt);
            _lastCameraPosition = now;
            // Also what keeps a respawn's jump across the map to a single small nudge.
            Vector3 target = Vector3.ClampMagnitude(velocity, _profile.MoveLagSpeedLimit);

            bool grounded = _motor.Grounded;
            if (grounded && !_wasGrounded && _lastVerticalSpeed < 0f)
            {
                // A landing: push the gun down by how fast the fall was.
                _carryVelocity.y -= _lastVerticalSpeed * _profile.MoveSpringSpeed * Tau * _profile.LandBounce;
            }
            _wasGrounded = grounded;
            _lastVerticalSpeed = target.y;

            float stiffness = _profile.MoveSpringSpeed * Tau;
            float drag = 2f * _profile.MoveSpringDamping * stiffness;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / SpringStep), 1, MaxSpringSteps);
            float step = Mathf.Min(dt, MaxSpringSteps * SpringStep) / steps;
            for (int i = 0; i < steps; i++)
            {
                _carryVelocity += (stiffness * stiffness * (target - _carry) - drag * _carryVelocity) * step;
                _carry += _carryVelocity * step;
            }
        }

        /// <summary>Blends the sprint carry in above walking speed and the climb drop in while on a ledge, and advances the breath.</summary>
        void UpdateStance(float dt)
        {
            MovementProfile movement = _motor.Profile;
            MoveState state = _motor.State;

            // Only a player running on their own feet: one skidding after a blast is not sprinting.
            float sprint = state == MoveState.Grounded && movement.SprintSpeed > movement.WalkSpeed
                ? Mathf.InverseLerp(movement.WalkSpeed, movement.SprintSpeed, _motor.PlanarSpeed)
                : 0f;
            _sprint = Mathf.SmoothDamp(_sprint, sprint, ref _sprintVelocity, _profile.SprintBlendSeconds, Mathf.Infinity, dt);

            float climb = state == MoveState.Mantling ? 1f : 0f;
            _climb = Mathf.SmoothDamp(_climb, climb, ref _climbVelocity, _profile.ClimbBlendSeconds, Mathf.Infinity, dt);

            _breathPhase = Mathf.Repeat(_breathPhase + dt * Tau / _profile.BreathSeconds, Tau);
        }

        /// <summary>Advances the reload move and returns how deep into it the gun is, 0 at rest to 1 at the deepest point.</summary>
        /// <remarks>A sine arch squared: the gun leaves the aim gently, hangs at the bottom and comes back gently.</remarks>
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

        /// <summary>
        /// Measures how fast the camera turned this frame and trails the arms behind it by
        /// that speed times the lag time.
        /// </summary>
        /// <remarks>
        /// WHY from the camera's own rotation and not the mouse: it then follows anything that
        /// turns the view (mouse, pad, a lean's tilt) and needs no look-sensitivity value.
        /// </remarks>
        void UpdateLag(float dt)
        {
            Quaternion now = _baseCamera.transform.rotation;
            // Where the view pointed last frame, seen from where it points now.
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

        /// <summary>Advances the step cycle by distance covered, and fades the bob with speed and footing.</summary>
        void UpdateBob(float dt)
        {
            float speed = _motor.PlanarSpeed;
            float fullSpeed = _motor.Profile.SprintSpeed;
            bool moving = _motor.Grounded && speed > MovingSpeed;

            float wanted = moving && fullSpeed > 0f ? Mathf.Clamp01(speed / fullSpeed) : 0f;
            _bobAmount = Mathf.SmoothDamp(_bobAmount, wanted, ref _bobAmountVelocity, _profile.BobFadeSeconds, Mathf.Infinity, dt);

            if (moving)
                _bobPhase = Mathf.Repeat(_bobPhase + speed * dt * _profile.BobStepsPerMetre * Mathf.PI, Tau);
        }
    }
}
