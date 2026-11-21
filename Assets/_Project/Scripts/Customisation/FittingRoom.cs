using System;
using CloseTheDeal.Lobby;
using CloseTheDeal.MatchMode;
using CloseTheDeal.Player;
using CloseTheDeal.UI;
using UnityEngine.InputSystem;
using UnityEngine;

namespace CloseTheDeal.Customisation
{
    /// <summary>Local fitting-room camera orbit, preview state, and UI screen controller. Design: docs/systems/CUSTOMISATION.md.</summary>
    [DisallowMultipleComponent]
    public sealed class FittingRoom : MonoBehaviour
    {
        public static FittingRoom Instance { get; private set; }

        public static event Action OnEntered;
        /// <summary>True when the outfit was kept, false when it was put back (or the session was cut short).</summary>
        public static event Action<bool> OnExited;
        public static event Action<int> OnSlotFocused;
        public static event Action<int, int> OnOptionShown;
        /// <summary>The player tried to keep an outfit with a locked choice in it.</summary>
        public static event Action OnLockedRefused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            OnEntered = null;
            OnExited = null;
            OnSlotFocused = null;
            OnOptionShown = null;
            OnLockedRefused = null;
        }

        [Tooltip("The fitting-room screen.")]
        [SerializeField] FittingRoomScreen _screen;

        [Header("Camera")]
        [Tooltip("How far the camera stands from the character, in metres. 2.3 frames head to shoes at the default field of view.")]
        [Range(1f, 5f)] [SerializeField] float _distance = 2.3f;

        [Tooltip("The height the camera looks at, in metres above the feet. About the belt line.")]
        [Range(0.3f, 1.8f)] [SerializeField] float _lookHeight = 0.95f;

        [Tooltip("How far the view slides sideways so the character stands right of centre, clear of the form on the left, in metres at the full-body distance (it shrinks as the camera closes in, so the character keeps its place on screen). Negative puts the character left.")]
        [Range(-1.5f, 1.5f)] [SerializeField] float _sideShift = 0.4f;

        [Tooltip("Seconds to swing out of first person. Under 0.5 keeps it quick; much faster reads as a cut.")]
        [Range(0.1f, 1.5f)] [SerializeField] float _enterSeconds = 0.45f;

        [Tooltip("Seconds to swing back into first person.")]
        [Range(0.1f, 1.5f)] [SerializeField] float _exitSeconds = 0.35f;

        [Tooltip("Closest the camera gets to a wall behind it, in metres.")]
        [Range(0.05f, 0.5f)] [SerializeField] float _wallClearance = 0.2f;

        [Tooltip("How long the camera takes to ease to a category's own view (angle, distance, height) when the player moves to it, in seconds. Under 0.2 it snaps; over 0.6 it lags behind fast flicking.")]
        [Range(0.1f, 1f)] [SerializeField] float _viewSmoothSeconds = 0.35f;

        [Tooltip("How far the camera pushes in when a choice changes, in metres, before easing back. 0 = no push. 0.12 is a small shove; above 0.3 it jolts.")]
        [Range(0f, 0.5f)] [SerializeField] float _punchMetres = 0.12f;

        [Tooltip("How long the push after a change takes to settle, in seconds.")]
        [Range(0.1f, 0.6f)] [SerializeField] float _punchSeconds = 0.25f;

        [Tooltip("How far above the look-at height the camera sits, in metres. Slightly above reads as a tailor's view.")]
        [Range(0f, 0.8f)] [SerializeField] float _cameraRise = 0.3f;

        [Tooltip("Where round the character the view starts, in degrees from straight in front. 30 gives a three-quarter view that shows the suit's shape; 0 is flat on.")]
        [Range(-90f, 90f)] [SerializeField] float _startAngle = 30f;

        [Header("Turning the character")]
        [Tooltip("How fast keys or the stick walk the view round the character, in degrees per second.")]
        [Range(30f, 360f)] [SerializeField] float _turnSpeed = 140f;

        [Tooltip("Degrees per unit of mouse movement.")]
        [Range(0.02f, 1f)] [SerializeField] float _mouseTurn = 0.2f;

        enum Phase { Closed, Entering, Open, Leaving }

        Phase _phase;
        float _blend;
        PlayerOutfit _outfit;
        PlayerCamera _camera;
        PlayerInputReader _input;
        Transform _player;
        Vector3 _savedOffset;
        Quaternion _savedRotation;
        Vector3 _facing;
        float _userOrbit;
        float _userOrbitVelocity;
        bool _returnOrbit;
        float _angle, _angleVelocity;
        float _viewDist, _viewDistVelocity;
        float _viewLook, _viewLookVelocity;
        float _punch;
        bool _waitForInteractRelease;
        OutfitLoadout _working;
        int _slot;
        bool _kept;
        int _wallMask;
        LobbyRoster _hookedRoster;
        MatchDirector _hookedMatch;

        public bool IsOpen => _phase != Phase.Closed;

        /// <summary>False when the scene's form is missing (an older build): the mirror then offers nothing.</summary>
        public bool IsUsable => _screen != null;

        void Awake()
        {
            Instance = this;
            int player = LayerMask.NameToLayer("Player");
            _wallMask = player >= 0 ? ~(1 << player) : ~0;
            if (_screen == null || !_screen.IsWired)
            {
                Debug.LogError($"[Lobby] {name}: the fitting room has no working form, so the mirror is closed. Run Close the Deal > Lanes > Set Up Lobby (or Set Up Integration) to rebuild it.", this);
                _screen = null;
                return;
            }
            _screen.SlotFocused += FocusSlotAt;
            _screen.OptionChosen += ChooseOption;
            _screen.KeepPressed += () => Leave(true);
            _screen.PutBackPressed += () => Leave(false);
        }

        void OnDestroy()
        {
            if (_hookedRoster != null)
                _hookedRoster.OnRunStarting -= OnRunStarting;
            if (_hookedMatch != null)
                _hookedMatch.OnPhaseChanged -= OnMatchPhase;
            if (Instance == this)
                Instance = null;
        }

        public void Enter(FittingRoomMirror mirror)
        {
            if (_screen == null || _phase != Phase.Closed || PlayerOutfit.Local == null || PlayerMotor.Local == null || PlayerCamera.Instance == null)
                return;
            if (LobbyRoster.Instance != null && LobbyRoster.Instance.RunInProgress)
                return; // The host would refuse the fitting request; the run is about to move everyone.

            _outfit = PlayerOutfit.Local;
            _camera = PlayerCamera.Instance;
            _player = PlayerMotor.Local.transform;
            _input = PlayerMotor.Local.GetComponent<PlayerInputReader>();

            TakeControls();
            RememberFirstPerson();
            GameMenu.HoldCursor(this); // The mouse works the form; Esc still opens the menu over it.

            _working = _outfit.Worn;
            if (!_outfit.Catalog.IsValid(_working))
                _working = _outfit.Catalog.Default;
            _slot = 0;
            SnapViewToSlot();
            _outfit.View.ForceVisible(true);

            if (LobbyRoster.Instance != null)
                LobbyRoster.Instance.RequestFitting(true);

            _phase = Phase.Entering;
            _blend = 0f;
            _screen.Open();
            Refresh();
            OnEntered?.Invoke();
        }

        void TakeControls()
        {
            if (LobbyInteractor.Instance != null)
                LobbyInteractor.Instance.Block();
            if (_input != null)
                _input.enabled = false;
            _camera.enabled = false;
        }

        void RememberFirstPerson()
        {
            Transform cam = _camera.transform;
            _savedOffset = cam.position - _player.position;
            _savedRotation = cam.rotation;
            _facing = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (_facing.sqrMagnitude < 0.01f)
                _facing = _player.forward;
            _facing.Normalize();
            _waitForInteractRelease = true;
        }

        void Leave(bool keep)
        {
            if (_phase != Phase.Open)
                return;
            if (keep && !_outfit.Catalog.IsWearable(_working, _outfit.RunsStarted))
            {
                _screen.ShowLockedNotice();
                OnLockedRefused?.Invoke();
                return;
            }

            if (keep)
                _outfit.Commit(_working);
            else
                _outfit.Revert();

            if (LobbyRoster.Instance != null)
                LobbyRoster.Instance.RequestFitting(false);

            _kept = keep;
            _phase = Phase.Leaving;
            _blend = 0f;
            _screen.Close(keep ? "Outfit kept." : "Outfit put back.");
        }

        void Finish()
        {
            GameMenu.ReleaseCursor(this);
            if (_outfit != null)
                _outfit.View.ForceVisible(false);
            if (_input != null)
                _input.enabled = true;
            if (_camera != null)
                _camera.enabled = true;
            if (LobbyInteractor.Instance != null)
                LobbyInteractor.Instance.Unblock();

            _phase = Phase.Closed;
            _outfit = null;
            _input = null;
            _player = null;
            OnExited?.Invoke(_kept);
        }

        void Abort()
        {
            if (_outfit != null)
                _outfit.Revert();
            if (LobbyRoster.Instance != null)
                LobbyRoster.Instance.RequestFitting(false);
            _kept = false;
            _screen.Close();
            Finish();
        }

        void OnRunStarting()
        {
            // With MatchDirector, count runs only on Countdown entry so refused desk starts don't increment.
            if (MatchDirector.Instance == null && PlayerOutfit.Local != null)
                PlayerOutfit.Local.RecordRunStarted();
            if (_phase != Phase.Closed)
                Abort();
        }

        void OnMatchPhase(MatchPhase from, MatchPhase to)
        {
            if (to != MatchPhase.Countdown || from == MatchPhase.Countdown)
                return;
            if (PlayerOutfit.Local != null)
                PlayerOutfit.Local.RecordRunStarted();
            if (_phase != Phase.Closed)
                Abort();
        }

        void Update()
        {
            HookRoster();
            HookMatch();
            if (_phase == Phase.Closed)
                return;

            if (_player == null || _outfit == null || PlayerMotor.Local == null)
            {
                Abort(); // Left the game, or the body despawned.
                return;
            }

            // The game menu frees the mouse; while it is up, the fitting room waits.
            if (_phase == Phase.Open && !GameMenu.OpenInGame)
                ReadInput();
        }

        void HookMatch()
        {
            MatchDirector match = MatchDirector.Instance;
            if (match == _hookedMatch)
                return;
            if (_hookedMatch != null)
                _hookedMatch.OnPhaseChanged -= OnMatchPhase;
            _hookedMatch = match;
            if (_hookedMatch != null)
                _hookedMatch.OnPhaseChanged += OnMatchPhase;
        }

        void HookRoster()
        {
            LobbyRoster roster = LobbyRoster.Instance;
            if (roster == _hookedRoster)
                return;
            if (_hookedRoster != null)
                _hookedRoster.OnRunStarting -= OnRunStarting;
            _hookedRoster = roster;
            if (_hookedRoster != null)
                _hookedRoster.OnRunStarting += OnRunStarting;
        }

        void ReadInput()
        {
            if (_waitForInteractRelease && !LobbyInput.Interact.IsPressed())
                _waitForInteractRelease = false;

            if (LobbyInput.Confirm.WasPressedThisFrame())
            {
                Leave(true);
                return;
            }
            if (LobbyInput.Cancel.WasPressedThisFrame())
            {
                Leave(false);
                return;
            }

            // Moving between categories and choices is the kit form's own navigation (the
            // EventSystem): the steppers report back through FocusSlotAt and ChooseOption.
            ReadTurn();
        }

        void SnapViewToSlot()
        {
            OutfitSlot slot = _outfit.Catalog.Slot(_slot);
            _angle = slot != null ? slot.ViewAngle : _startAngle;
            _viewDist = slot != null ? slot.ViewDistance : _distance;
            _viewLook = slot != null ? slot.ViewHeight : _lookHeight;
            _angleVelocity = _viewDistVelocity = _viewLookVelocity = 0f;
            _userOrbit = 0f;
            _userOrbitVelocity = 0f;
            _returnOrbit = false;
            _punch = 0f;
        }

        void ReadTurn()
        {
            float turn = LobbyInput.Rotate.ReadValue<float>() * _turnSpeed * Time.deltaTime;
            // Allow mouse orbit only while captured or right-dragging over the form.
            if (LobbyInput.Captured || (Mouse.current != null && Mouse.current.rightButton.isPressed))
                turn += LobbyInput.RotatePointer.ReadValue<float>() * _mouseTurn;
            if (!_waitForInteractRelease && !Mathf.Approximately(turn, 0f))
            {
                _userOrbit += turn;
                _returnOrbit = false;
            }
        }

        void FocusSlotAt(int slot)
        {
            if (_phase == Phase.Closed || slot == _slot)
                return;
            _slot = slot;
            _returnOrbit = true;
            Refresh();
            OnSlotFocused?.Invoke(_slot);
        }

        void ChooseOption(int slot, int option)
        {
            if (_phase == Phase.Closed)
                return;
            _slot = slot;
            _working = _working.With(slot, option);
            _outfit.Preview(_working);
            _punch = 1f;
            Refresh();
            OnOptionShown?.Invoke(slot, option);
        }

        void Refresh()
        {
            _screen.Show(_outfit.Catalog, _working, _slot, _outfit.RunsStarted, LobbyInput.UsingGamepad);
        }

        void LateUpdate()
        {
            if (_phase == Phase.Closed || _player == null)
                return;

            float seconds = _phase == Phase.Leaving ? _exitSeconds : _enterSeconds;
            if (_phase != Phase.Open)
                _blend = Mathf.Min(1f, _blend + Time.deltaTime / seconds);

            PlaceCamera();

            if (_blend < 1f)
                return;
            if (_phase == Phase.Entering)
                _phase = Phase.Open;
            else if (_phase == Phase.Leaving)
                Finish();
        }

        void PlaceCamera()
        {
            EaseView(Time.deltaTime);
            Vector3 eye = _player.position + _savedOffset;
            Framed(out Vector3 framed, out Quaternion framedRotation);

            // Flip blend weight on exit rather than swapping endpoints.
            float t = _blend * _blend * (3f - 2f * _blend);
            float towardFramed = _phase == Phase.Leaving ? 1f - t : t;
            _camera.transform.SetPositionAndRotation(
                Vector3.Lerp(eye, framed, towardFramed),
                Quaternion.Slerp(_savedRotation, framedRotation, towardFramed));
        }

        void EaseView(float dt)
        {
            OutfitSlot slot = _outfit.Catalog.Slot(_slot);
            if (slot != null)
            {
                _angle = Mathf.SmoothDampAngle(_angle, slot.ViewAngle, ref _angleVelocity, _viewSmoothSeconds, Mathf.Infinity, dt);
                _viewDist = Mathf.SmoothDamp(_viewDist, slot.ViewDistance, ref _viewDistVelocity, _viewSmoothSeconds, Mathf.Infinity, dt);
                _viewLook = Mathf.SmoothDamp(_viewLook, slot.ViewHeight, ref _viewLookVelocity, _viewSmoothSeconds, Mathf.Infinity, dt);
            }

            if (_returnOrbit)
                _userOrbit = Mathf.SmoothDampAngle(_userOrbit, 0f, ref _userOrbitVelocity, _viewSmoothSeconds, Mathf.Infinity, dt);
            _punch = Mathf.MoveTowards(_punch, 0f, dt / _punchSeconds);
        }

        void Framed(out Vector3 position, out Quaternion rotation)
        {
            Vector3 direction = Quaternion.Euler(0f, _angle + _userOrbit, 0f) * _facing;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            float distance = _viewDist - _punchMetres * _punch * _punch;
            float shift = _sideShift * (_viewDist / _distance);
            Vector3 look = _player.position + Vector3.up * _viewLook + side * shift;
            Vector3 wanted = _player.position + Vector3.up * (_viewLook + _cameraRise) + side * shift + direction * distance;

            Vector3 ray = wanted - look;
            float length = ray.magnitude;
            if (Physics.SphereCast(look, _wallClearance, ray / length, out RaycastHit hit, length, _wallMask, QueryTriggerInteraction.Ignore))
                wanted = look + ray / length * hit.distance;

            position = wanted;
            rotation = Quaternion.LookRotation(look - wanted, Vector3.up);
        }
    }
}
