// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-13
//  Prompt    : Alex: "set up an fps viewmodel with the characters model we are using"
//  Modified  : Claude Fable 5.2, 2026-11-13 — framing and posing taken from NorseFPS at
//              Alex's request: this now places the gun in the view (sway, bob, kick and the
//              reload dip all move the gun) and the arms prefab reaches the hand to it.
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
    /// <para><b>Driving it:</b> weapon code calls <see cref="Fire"/> and <see cref="Reload"/>.
    /// Until that code exists this component listens to the attack button and the R key
    /// itself; see <see cref="ReadTestInput"/>.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // From weapon code, on the owning client, when a shot has been fired:
    /// _viewmodel.Fire();
    /// </code>
    /// </example>
    [DisallowMultipleComponent]
    public sealed class PlayerViewmodel : MonoBehaviour
    {
        /// <summary>The layer the arms are on, so their own camera can draw them and nothing else. Named by the greybox tool.</summary>
        public const string LayerName = "Viewmodel";

        /// <summary>Input action for the attack button, in the actions asset's Player map.</summary>
        const string AttackAction = "Player/Attack";

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

        Quaternion _lastCameraRotation;
        Vector2 _lag;
        Vector2 _lagVelocity;
        float _bobPhase;
        float _bobAmount;
        float _bobAmountVelocity;
        float _kick;
        /// <summary>Signed -1 or +1, picked per shot, so the kick's twist alternates sides.</summary>
        float _kickSide = 1f;
        /// <summary>Seconds into the current reload move; negative when none is playing.</summary>
        float _reloadElapsed = -1f;

        /// <summary>True while this client is showing arms for this player.</summary>
        public bool IsShown => _arms != null;

        /// <summary>True from the start of a reload move until the gun is back on aim.</summary>
        public bool IsReloading => _reloadElapsed >= 0f;

        /// <summary>Kicks the gun. Does nothing on players this client does not control, or during a reload.</summary>
        public void Fire()
        {
            if (_arms == null || IsReloading)
                return;

            _kick = 1f;
            _kickSide = -_kickSide;
        }

        /// <summary>Starts the reload move. Does nothing on players this client does not control, or while one is already playing.</summary>
        public void Reload()
        {
            if (_arms != null && !IsReloading)
                _reloadElapsed = 0f;
        }

        void Awake()
        {
            if (_actions != null)
                _attack = _actions.FindAction(AttackAction, false);
        }

        void OnDisable()
        {
            Hide();
        }

        void Update()
        {
            // PlayerMotor.Local is set and cleared by the motor as ownership starts and stops;
            // comparing a reference each frame is cheaper than a second network callback here.
            bool local = _motor != null && PlayerMotor.Local == _motor;
            if (local && _arms == null)
                Show();
            else if (!local && _arms != null)
                Hide();

            if (_arms != null)
                ReadTestInput();
        }

        /// <summary>
        /// Stand-in trigger until slice 2's weapon code calls <see cref="Fire"/> and
        /// <see cref="Reload"/>: the attack button kicks, R reloads.
        /// </summary>
        /// <remarks>
        /// WHY the R key is read straight from the keyboard: the actions asset has no reload
        /// action yet, and adding one belongs with the weapon input in slice 2. Remove this
        /// method then.
        /// </remarks>
        void ReadTestInput()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            if (_attack != null && _attack.WasPressedThisFrame())
                Fire();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                Reload();
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

            _lastCameraRotation = _baseCamera.transform.rotation;
            _lag = Vector2.zero;
            _lagVelocity = Vector2.zero;
            _bobAmount = 0f;
            _kick = 0f;
            _reloadElapsed = -1f;

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        void Hide()
        {
            if (_arms == null)
                return;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

            if (_baseCamera != null)
                _baseCamera.GetUniversalAdditionalCameraData().cameraStack.Remove(_armsCamera);
            if (_armsCamera != null)
                Destroy(_armsCamera.gameObject); // Takes the arms with it.

            _arms = null;
            _armsCamera = null;
            _baseCamera = null;
        }

        /// <summary>The arms are drawn by their own camera and by no other.</summary>
        void OnBeginCameraRendering(ScriptableRenderContext context, Camera rendering)
        {
            if (_arms != null)
                _arms.SetVisible(rendering == _armsCamera);
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
            UpdateBob(dt);
            _kick = Mathf.MoveTowards(_kick, 0f, dt / _profile.KickRecoverSeconds);
            // Squared so the gun snaps back fast at first and eases into rest.
            float kick = _kick * _kick;
            float reload = ReloadDepth(dt);

            float bobSwing = Mathf.Sin(_bobPhase);
            Vector3 bob = new Vector3(bobSwing * _profile.BobSide, -Mathf.Abs(bobSwing) * _profile.BobHeight, 0f) * _bobAmount;

            Vector3 position = _profile.GunPosition + bob
                + Vector3.back * (_profile.KickBack * kick)
                + Vector3.down * (_profile.ReloadDrop * reload);
            Quaternion rotation = Quaternion.Euler(_lag.x, _lag.y, 0f)
                * Quaternion.Euler(_profile.GunTurn)
                * Quaternion.Euler(
                    -_profile.KickPitch * kick - _profile.ReloadTilt * reload,
                    0f,
                    _profile.KickRoll * kick * _kickSide + _profile.ReloadRoll * reload);
            _arms.SetPose(position, rotation, _profile.ShoulderPosition);
        }

        /// <summary>Advances the reload move and returns how deep into it the gun is, 0 at rest to 1 at the deepest point.</summary>
        /// <remarks>A sine arch squared: the gun leaves the aim gently, hangs at the bottom and comes back gently.</remarks>
        float ReloadDepth(float dt)
        {
            if (_reloadElapsed < 0f)
                return 0f;

            _reloadElapsed += dt;
            if (_reloadElapsed >= _profile.ReloadSeconds)
            {
                _reloadElapsed = -1f;
                return 0f;
            }

            float arch = Mathf.Sin(_reloadElapsed / _profile.ReloadSeconds * Mathf.PI);
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
