using System;
using CloseTheDeal.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Player
{
    /// <summary>The buttons other systems act on once per press. Bindings: docs/systems/PLAYER_MOVEMENT.md.</summary>
    public enum PlayerPress : byte
    {
        Jump = 0,
        Attack = 1,
        AltFire = 2,
        Interact = 3,
        Throw = 4,
        Reload = 5,
        /// <summary>Opens and closes the controls help card. Nothing consumes it; the HUD listens to <see cref="PlayerInputReader.Pressed"/>.</summary>
        Help = 6
    }

    /// <summary>
    /// Reads the local player's input every frame. Held inputs are read live; presses are
    /// latched until whoever owns that button consumes them on a tick, so a tap between two
    /// ticks is never lost. Enabled only on the owning client.
    /// Everything reads as idle while the mouse is free, which is exactly while the game menu
    /// is open: typing a lobby code or clicking a button never moves, jumps or fires.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("The Input System actions asset. Uses its Player map; the full binding table is in docs/systems/PLAYER_MOVEMENT.md.")]
        [SerializeField] InputActionAsset _actions;

        /// <summary>Stick dead zones come from the player's movement profile; the motor hands it over on wake. Until then these defaults apply.</summary>
        public MovementProfile Profile { get; set; }

        const float DefaultMoveDeadZone = 0.1f;
        const float DefaultLookDeadZone = 0.15f;

        float MoveDeadZone => Profile != null ? Profile.MoveStickDeadZone : DefaultMoveDeadZone;
        float LookDeadZone => InputPrefs.StickDeadZone ?? (Profile != null ? Profile.LookStickDeadZone : DefaultLookDeadZone);

        /// <summary>Tells the settings screen that key rebinding and the stick dead zone are applied here.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookPrefs() => InputPrefs.BindingsHooked = true;

        /// <summary>The player's saved rebinding, put on this reader's own copy of the actions (overrides are not copied by Instantiate).</summary>
        void ApplyBindingOverrides()
        {
            _own.RemoveAllBindingOverrides();
            string json = InputPrefs.BindingOverridesJson;
            if (!string.IsNullOrEmpty(json))
                _own.LoadBindingOverridesFromJson(json);
        }

        const int PressCount = 7;
        const int SlotCount = 4;

        static readonly string[] PressActionNames = { "Jump", "Attack", "AltFire", "Interact", "Throw", "Reload", "Help" };

        readonly InputAction[] _presses = new InputAction[PressCount];
        readonly bool[] _latched = new bool[PressCount];
        readonly InputAction[] _slots = new InputAction[SlotCount];
        InputActionAsset _own;
        InputActionMap _map;
        InputAction _move;
        InputAction _look;
        InputAction _lookStick;
        InputAction _sprint;
        InputAction _crouch;
        InputAction _lean;
        int _latchedSlot;

        /// <summary>
        /// Lets a scripted play session drive the player without a window to capture the mouse
        /// in. The game itself never sets it: there, a captured mouse is the only gate.
        /// </summary>
        public static bool ScriptedCapture { get; set; }

        /// <summary>True while the game has the mouse, i.e. the menu is closed. Every input here reads idle otherwise.</summary>
        public static bool Captured => ScriptedCapture || Cursor.lockState == CursorLockMode.Locked;

        /// <summary>Raised on the frame a button goes down while captured, for sound and UI. Gameplay consumes the latch instead.</summary>
        public event Action<PlayerPress> Pressed;

        /// <summary>Raised on the frame a weapon slot button (1–4) goes down while captured.</summary>
        public event Action<int> WeaponSlotPressed;

        /// <summary>This player's own copy of the actions, with their rebinding applied. Read it to list or show bindings; rebinding goes through InputPrefs.BindingOverridesJson.</summary>
        public InputActionAsset Actions => _own;

        /// <summary>True when the last control the player used was on a gamepad, false for keyboard and mouse. Help text follows it.</summary>
        public bool LastUsedGamepad { get; private set; }

        /// <summary>Raised when the player switches between keyboard and mouse and a gamepad.</summary>
        public event Action<bool> DeviceChanged;

        /// <summary>Movement stick with the dead zone removed and the rest rescaled to 0–1.</summary>
        public Vector2 Move => Captured ? WithDeadZone(_move.ReadValue<Vector2>(), MoveDeadZone) : Vector2.zero;

        /// <summary>Mouse movement this frame, in mouse counts. Scale by the mouse sensitivity for degrees.</summary>
        public Vector2 LookDelta => Captured ? _look.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>Look stick, dead zone removed and rescaled to 0–1. A rate, not a distance: scale by degrees per second and frame time.</summary>
        public Vector2 LookStick => Captured ? WithDeadZone(_lookStick.ReadValue<Vector2>(), LookDeadZone) : Vector2.zero;

        public bool Sprint => Captured && _sprint.IsPressed();
        public bool Crouch => Captured && _crouch.IsPressed();

        /// <summary>True while jump is held, so letting go early can cut a jump short.</summary>
        public bool JumpHeld => Held(PlayerPress.Jump);

        /// <summary>-1 leaning left, +1 leaning right, 0 upright. Both held cancel out.</summary>
        public sbyte Lean
        {
            get
            {
                if (!Captured)
                    return 0;

                float axis = _lean.ReadValue<float>();
                return axis < -0.5f ? (sbyte)-1 : axis > 0.5f ? (sbyte)1 : (sbyte)0;
            }
        }

        void Awake()
        {
            // The asset on the prefab is one object shared by every player in the game. Each
            // reader takes its own copy: otherwise a remote player's body spawning (whose reader
            // is switched off) would disable the actions the local player is reading.
            _own = Instantiate(_actions);
            ApplyBindingOverrides();
            _map = _own.FindActionMap("Player", true);
            _move = _map.FindAction("Move", true);
            _look = _map.FindAction("Look", true);
            _lookStick = _map.FindAction("LookStick", true);
            _sprint = _map.FindAction("Sprint", true);
            _crouch = _map.FindAction("Crouch", true);
            _lean = _map.FindAction("Lean", true);
            for (int i = 0; i < PressCount; i++)
                _presses[i] = _map.FindAction(PressActionNames[i], true);
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = _map.FindAction("Slot" + (i + 1), true);
        }

        void OnDestroy()
        {
            if (_own != null)
                Destroy(_own);
        }

        void OnEnable()
        {
            _map.Enable();
            InputPrefs.Changed += ApplyBindingOverrides;
        }

        void OnDisable()
        {
            _map.Disable();
            InputPrefs.Changed -= ApplyBindingOverrides;
            ClearLatches();
        }

        void Update()
        {
            if (!Captured)
            {
                // A press made just before the menu opened must not fire when it closes.
                ClearLatches();
                return;
            }

            NoteDevice(_move);
            NoteDevice(_look);
            NoteDevice(_lookStick);
            for (int i = 0; i < PressCount; i++)
            {
                if (!_presses[i].WasPressedThisFrame())
                    continue;

                NoteDevice(_presses[i]);

                _latched[i] = true;
                Pressed?.Invoke((PlayerPress)i);
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (!_slots[i].WasPressedThisFrame())
                    continue;

                _latchedSlot = i + 1;
                WeaponSlotPressed?.Invoke(_latchedSlot);
            }
        }

        /// <summary>True while the button is down and the game has the mouse.</summary>
        public bool Held(PlayerPress press) => Captured && _presses[(int)press].IsPressed();

        /// <summary>
        /// True once per press, for whichever system owns that button; call it from that
        /// system's tick. Two systems consuming the same button would steal from each other.
        /// </summary>
        public bool Consume(PlayerPress press)
        {
            bool pressed = _latched[(int)press];
            _latched[(int)press] = false;
            return pressed;
        }

        /// <summary>The weapon slot (1–4) pressed since the last call, or 0 for none. The latest press wins.</summary>
        public int ConsumeWeaponSlot()
        {
            int slot = _latchedSlot;
            _latchedSlot = 0;
            return slot;
        }

        /// <summary>Follows which kind of device the player is using, from whichever action they are working.</summary>
        void NoteDevice(InputAction action)
        {
            InputControl control = action.activeControl;
            if (control == null)
                return;

            bool gamepad = control.device is Gamepad;
            if (gamepad == LastUsedGamepad)
                return;

            LastUsedGamepad = gamepad;
            DeviceChanged?.Invoke(gamepad);
        }

        /// <summary>
        /// How the player's own first binding of an action reads, for keyboard and mouse or for a
        /// gamepad ("Space", "W/A/S/D", "LT"). Follows any rebinding on this player's copy of the
        /// actions. Allocates: call it when help opens or the device changes, not per frame.
        /// </summary>
        public string BindingText(string actionName, bool gamepad)
        {
            InputAction action = _map.FindAction(actionName);
            if (action == null)
                return string.Empty;

            string group = gamepad ? "Gamepad" : "Keyboard&Mouse";
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (binding.isPartOfComposite)
                    continue;

                string groups = binding.isComposite && i + 1 < bindings.Count ? bindings[i + 1].groups : binding.groups;
                if (groups == null || !groups.Contains(group))
                    continue;

                return binding.isComposite ? CompositeText(action, i) : action.GetBindingDisplayString(i);
            }

            return string.Empty;
        }

        /// <summary>A composite's keys, the first binding of each part only ("W/A/S/D", not every arrow-key alternative too).</summary>
        static string CompositeText(InputAction action, int compositeIndex)
        {
            var bindings = action.bindings;
            var parts = new System.Collections.Generic.List<string>(4);
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = compositeIndex + 1; i < bindings.Count && bindings[i].isPartOfComposite; i++)
            {
                if (!seen.Add(bindings[i].name))
                    continue;
                parts.Add(InputControlPath.ToHumanReadableString(bindings[i].effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice));
            }

            return string.Join("/", parts);
        }

        void ClearLatches()
        {
            Array.Clear(_latched, 0, PressCount);
            _latchedSlot = 0;
        }

        static Vector2 WithDeadZone(Vector2 raw, float deadZone)
        {
            float magnitude = Mathf.Clamp01(raw.magnitude);
            if (magnitude <= deadZone)
                return Vector2.zero;

            return raw.normalized * Mathf.InverseLerp(deadZone, 1f, magnitude);
        }
    }
}
