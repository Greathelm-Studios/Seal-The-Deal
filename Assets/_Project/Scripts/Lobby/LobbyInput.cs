using UnityEngine;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Lobby
{
    /// <summary>
    /// The lobby lane's own inputs, defined in code because only the movement lane edits the
    /// shared actions asset. Listed in docs/HANDOVER_lobby.md for moving into that asset.
    /// Keyboard and gamepad both work. Escape is left to the game menu.
    /// Interact is F (and the pad's north button) to match the shared asset: E is lean-right there.
    /// </summary>
    public static class LobbyInput
    {
        static InputActionMap _map;

        public static InputAction Interact { get; private set; }
        public static InputAction Confirm { get; private set; }
        public static InputAction Cancel { get; private set; }
        /// <summary>The host opens the reception's player list (to remove someone).</summary>
        public static InputAction HostPanel { get; private set; }
        /// <summary>Turn the character in the fitting room: keys and stick give -1..1, the mouse gives its movement.</summary>
        public static InputAction Rotate { get; private set; }
        public static InputAction RotatePointer { get; private set; }

        /// <summary>
        /// True while the mouse is captured, which is exactly while no menu is open (GameMenu owns
        /// that). Everything in this lane asks here instead of reading the cursor itself.
        /// </summary>
        public static bool Captured
        {
            get
            {
#if UNITY_EDITOR
                if (EditorForceCaptured)
                    return true;
#endif
                return Cursor.lockState == CursorLockMode.Locked;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor test runs only: a headless Editor has no window to capture the mouse in, so a test driver sets this. Never set by the game.</summary>
        public static bool EditorForceCaptured;
#endif

        /// <summary>True when the last lane input came from a gamepad, so prompts can name pad buttons.</summary>
        public static bool UsingGamepad { get; private set; }

        public static void Enable()
        {
            Build();
            _map.Enable();
        }

        static void Build()
        {
            if (_map != null)
                return;

            _map = new InputActionMap("LobbyLane");

            Interact = _map.AddAction("Interact", InputActionType.Button);
            Interact.AddBinding("<Keyboard>/f");
            Interact.AddBinding("<Gamepad>/buttonNorth");

            Confirm = _map.AddAction("Confirm", InputActionType.Button);
            Confirm.AddBinding("<Keyboard>/enter");
            Confirm.AddBinding("<Keyboard>/numpadEnter");
            Confirm.AddBinding("<Keyboard>/space");
            Confirm.AddBinding("<Gamepad>/buttonSouth");

            Cancel = _map.AddAction("Cancel", InputActionType.Button);
            Cancel.AddBinding("<Keyboard>/backspace");
            Cancel.AddBinding("<Gamepad>/buttonEast");

            // Moving through a form is the UI kit's navigation (the UI map), not lane actions.
            HostPanel = Button("HostPanel", "<Keyboard>/tab", "<Gamepad>/select");

            Rotate = _map.AddAction("Rotate", InputActionType.Value);
            Rotate.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/q").With("Positive", "<Keyboard>/r");
            Rotate.AddBinding("<Gamepad>/rightStick/x");

            RotatePointer = _map.AddAction("RotatePointer", InputActionType.PassThrough);
            RotatePointer.AddBinding("<Mouse>/delta/x");

            _map.actionTriggered += OnTriggered;
        }

        static InputAction Button(string name, params string[] paths)
        {
            InputAction action = _map.AddAction(name, InputActionType.Button);
            foreach (string path in paths)
                action.AddBinding(path);
            return action;
        }

        static void OnTriggered(InputAction.CallbackContext context)
        {
            if (context.action == RotatePointer)
                return;
            UsingGamepad = context.control != null && context.control.device is Gamepad;
        }
    }
}
