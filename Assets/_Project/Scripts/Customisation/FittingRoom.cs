using System;
using CloseTheDeal.Lobby;
using CloseTheDeal.Player;
using UnityEngine;

namespace CloseTheDeal.Customisation
{
    /// <summary>
    /// The fitting room: the local player steps out of first person into a framed view of
    /// their own character and flicks through outfit choices, each shown at once. Keep saves
    /// it and sends it to the host; put back restores what they wore. Other players see them
    /// standing at the mirror the whole time, and the ready board shows them as "In the
    /// fitting room" (which takes back their ready, so a run can't start without them).
    /// The cursor stays captured throughout: the game menu frees it, and a freed cursor means
    /// the menu is open. Keys and pad drive the screen; the mouse only turns the view.
    /// Local only; the outfit sync is PlayerOutfit's. Design: docs/systems/CUSTOMISATION.md.
    /// </summary>
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

        [Tooltip("The fitting-room screen.")]
        [SerializeField] FittingRoomScreen _screen;

        [Header("Camera")]
        [Tooltip("How far the camera stands from the character, in metres. 2.3 frames head to shoes at the default field of view.")]
        [Range(1f, 5f)] [SerializeField] float _distance = 2.3f;

        [Tooltip("The height the camera looks at, in metres above the feet. About the belt line.")]
        [Range(0.3f, 1.8f)] [SerializeField] float _lookHeight = 0.95f;

        [Tooltip("The camera's own height, in metres above the feet. A little above the look height reads as a tailor's view.")]
        [Range(0.3f, 2.2f)] [SerializeField] float _cameraHeight = 1.25f;

        [Tooltip("How far right of the character the camera sits, in metres, so the screen on the left doesn't cover them.")]
        [Range(-1.5f, 1.5f)] [SerializeField] float _sideShift = -0.45f;

        [Tooltip("Seconds to swing out of first person. Under 0.5 keeps it quick; much faster reads as a cut.")]
        [Range(0.1f, 1.5f)] [SerializeField] float _enterSeconds = 0.45f;

        [Tooltip("Seconds to swing back into first person.")]
        [Range(0.1f, 1.5f)] [SerializeField] float _exitSeconds = 0.35f;

        [Tooltip("Closest the camera gets to a wall behind it, in metres.")]
        [Range(0.05f, 0.5f)] [SerializeField] float _wallClearance = 0.2f;

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
        float _orbit;
        bool _waitForInteractRelease;
        OutfitLoadout _working;
        int _slot;
        bool _kept;
        int _wallMask;
        LobbyRoster _hookedRoster;

        public bool IsOpen => _phase != Phase.Closed;

        void Awake()
        {
            Instance = this;
            int player = LayerMask.NameToLayer("Player");
            _wallMask = player >= 0 ? ~(1 << player) : ~0;
        }

        void OnDestroy()
        {
            if (_hookedRoster != null)
                _hookedRoster.OnRunStarting -= OnRunStarting;
            if (Instance == this)
                Instance = null;
        }

        // ---- Enter / leave -------------------------------------------------------------------

        public void Enter(FittingRoomMirror mirror)
        {
            if (_phase != Phase.Closed || PlayerOutfit.Local == null || PlayerMotor.Local == null || PlayerCamera.Instance == null)
                return;

            _outfit = PlayerOutfit.Local;
            _camera = PlayerCamera.Instance;
            _player = PlayerMotor.Local.transform;
            _input = PlayerMotor.Local.GetComponent<PlayerInputReader>();

            TakeControls();
            RememberFirstPerson();

            _working = _outfit.Worn;
            if (!_outfit.Catalog.IsValid(_working))
                _working = _outfit.Catalog.Default;
            _slot = 0;
            _outfit.View.ForceVisible(true);

            if (LobbyRoster.Instance != null)
                LobbyRoster.Instance.RequestFitting(true);

            _phase = Phase.Entering;
            _blend = 0f;
            _screen.Open();
            Refresh();
            OnEntered?.Invoke();
        }

        /// <summary>Hands the controls and the camera over to the fitting room. Finish gives them back.</summary>
        void TakeControls()
        {
            if (LobbyInteractor.Instance != null)
                LobbyInteractor.Instance.Block();
            if (_input != null)
                _input.enabled = false; // Its map goes off with it: the body stands still, no lean or jump.
            _camera.enabled = false;    // Stops it placing itself at the eye every frame.
        }

        /// <summary>Where the first-person eye is, relative to the body, so the swing back lands on it.</summary>
        void RememberFirstPerson()
        {
            Transform cam = _camera.transform;
            _savedOffset = cam.position - _player.position;
            _savedRotation = cam.rotation;
            _facing = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (_facing.sqrMagnitude < 0.01f)
                _facing = _player.forward;
            _facing.Normalize();
            _orbit = _startAngle;
            _waitForInteractRelease = true;
        }

        /// <summary>Keep (true) saves and wears the browsed outfit; false puts back what was worn.</summary>
        void Leave(bool keep)
        {
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
            _screen.Close();
        }

        /// <summary>Hands everything back to first person. Safe to call with the player gone.</summary>
        void Finish()
        {
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

        /// <summary>The player left the game or the run started mid-fitting: put everything back now, no swing.</summary>
        void Abort()
        {
            if (_outfit != null)
                _outfit.Revert();
            _kept = false;
            _screen.Close();
            Finish();
        }

        void OnRunStarting()
        {
            if (PlayerOutfit.Local != null)
                PlayerOutfit.Local.RecordRunStarted();
            if (_phase != Phase.Closed)
                Abort();
        }

        // ---- Frame ---------------------------------------------------------------------------

        void Update()
        {
            HookRoster();
            if (_phase == Phase.Closed)
                return;

            if (_player == null || _outfit == null || PlayerMotor.Local == null)
            {
                Abort(); // Left the game, or the body despawned.
                return;
            }

            // The game menu frees the mouse; while it is up, the fitting room waits.
            if (_phase == Phase.Open && LobbyInput.Captured)
                ReadInput();
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

            if (LobbyInput.NextSlot.WasPressedThisFrame())
                FocusSlot(1);
            else if (LobbyInput.PreviousSlot.WasPressedThisFrame())
                FocusSlot(-1);

            if (LobbyInput.NextOption.WasPressedThisFrame())
                StepOption(1);
            else if (LobbyInput.PreviousOption.WasPressedThisFrame())
                StepOption(-1);

            float turn = LobbyInput.Rotate.ReadValue<float>() * _turnSpeed * Time.deltaTime;
            turn += LobbyInput.RotatePointer.ReadValue<float>() * _mouseTurn;
            if (!_waitForInteractRelease)
                _orbit += turn;
        }

        void FocusSlot(int step)
        {
            int count = _outfit.Catalog.SlotCount;
            if (count == 0)
                return;
            _slot = (_slot + step + count) % count;
            Refresh();
            OnSlotFocused?.Invoke(_slot);
        }

        void StepOption(int step)
        {
            OutfitSlot slot = _outfit.Catalog.Slot(_slot);
            int count = slot.OptionCount;
            if (count < 2)
                return;

            int option = (_working.Get(_slot) + step + count) % count;
            _working = _working.With(_slot, option);
            _outfit.Preview(_working);
            _screen.NudgeOption(step);
            Refresh();
            OnOptionShown?.Invoke(_slot, option);
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

        /// <summary>Between the first-person eye and the framed view, by the current blend, eased at both ends so the swing never jolts.</summary>
        void PlaceCamera()
        {
            Vector3 eye = _player.position + _savedOffset;
            Framed(out Vector3 framed, out Quaternion framedRotation);

            // Entering goes eye → framed; leaving goes framed → eye, so flip the blend instead of the ends.
            float t = _blend * _blend * (3f - 2f * _blend);
            float towardFramed = _phase == Phase.Leaving ? 1f - t : t;
            _camera.transform.SetPositionAndRotation(
                Vector3.Lerp(eye, framed, towardFramed),
                Quaternion.Slerp(_savedRotation, framedRotation, towardFramed));
        }

        void Framed(out Vector3 position, out Quaternion rotation)
        {
            Vector3 direction = Quaternion.Euler(0f, _orbit, 0f) * _facing;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            Vector3 look = _player.position + Vector3.up * _lookHeight + side * _sideShift;
            Vector3 wanted = _player.position + Vector3.up * _cameraHeight + side * _sideShift + direction * _distance;

            Vector3 ray = wanted - look;
            float length = ray.magnitude;
            if (Physics.SphereCast(look, _wallClearance, ray / length, out RaycastHit hit, length, _wallMask, QueryTriggerInteraction.Ignore))
                wanted = look + ray / length * hit.distance;

            position = wanted;
            rotation = Quaternion.LookRotation(look - wanted, Vector3.up);
        }
    }
}
