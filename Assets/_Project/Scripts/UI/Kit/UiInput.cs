using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Tracks active input modality (keys/pad vs mouse), UI Submit hold, and Cancel presses.</summary>
    public static class UiInput
    {
        /// <summary>True when the last input was keyboard or gamepad (starts true so initial screen shows focus).</summary>
        public static bool UsingKeysOrPad { get; private set; } = true;

        static bool _capturing;
        static int _captureEndFrame = -10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            UsingKeysOrPad = true;
            _capturing = false;
            _captureEndFrame = -10;
            _switchFrame = -1;
        }

        /// <summary>True while rebinding listens for a key (plus one trailing frame so Esc/arrows don't leak to the menu).</summary>
        public static bool Capturing
        {
            get => _capturing || Time.frameCount <= _captureEndFrame + 1;
            set => _capturing = value;
        }

        /// <summary>Ends a key capture; the menu ignores Esc and navigation for one more frame.</summary>
        public static void ReleaseCapture()
        {
            _capturing = false;
            _captureEndFrame = Time.frameCount;
        }

        static int _switchFrame = -1;

        /// <summary>True on the frame the player went from the mouse to a key or pad: that press only reveals focus.</summary>
        public static bool JustSwitchedToKeysOrPad => Time.frameCount == _switchFrame;

        static InputSystemUIInputModule Module =>
            EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;

        /// <summary>True while UI Submit is pressed (prevents A/Jump overlap when closing a menu).</summary>
        public static bool SubmitHeld
        {
            get
            {
                InputSystemUIInputModule module = Module;
                InputAction submit = module != null && module.submit != null ? module.submit.action : null;
                if (submit != null && submit.enabled)
                    return submit.IsPressed();

                Keyboard keyboard = Keyboard.current;
                Gamepad pad = Gamepad.current;
                return (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed))
                    || (pad != null && pad.buttonSouth.isPressed);
            }
        }

        /// <summary>True on the frame UI Cancel went down (Esc, the pad's B / Circle).</summary>
        public static bool CancelPressed
        {
            get
            {
                InputSystemUIInputModule module = Module;
                InputAction cancel = module != null && module.cancel != null ? module.cancel.action : null;
                if (cancel != null && cancel.enabled)
                    return cancel.WasPressedThisFrame();

                Keyboard keyboard = Keyboard.current;
                Gamepad pad = Gamepad.current;
                return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            }
        }

        /// <summary>Applies the kit's repeat timing to the EventSystem: hold a direction and focus repeats.</summary>
        public static void ApplyRepeat(UiTokens tokens)
        {
            InputSystemUIInputModule module = Module;
            if (module == null || tokens == null)
                return;
            module.moveRepeatDelay = tokens.RepeatDelay;
            module.moveRepeatRate = tokens.RepeatInterval;
        }

        /// <summary>Called once a frame by the UI root: notes which device the player is using and any input for idle life.</summary>
        public static void Poll()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            Mouse mouse = Mouse.current;

            bool keysOrPad = (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                || (pad != null && (pad.wasUpdatedThisFrame && PadActive(pad)));
            bool pointer = mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f
                || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
                || mouse.scroll.ReadValue().sqrMagnitude > 0f);

            if (keysOrPad)
            {
                UiGlyphs.Note(keyboard != null && keyboard.anyKey.wasPressedThisFrame ? keyboard : pad);
                if (!UsingKeysOrPad)
                {
                    _switchFrame = Time.frameCount;
                    UiFocusFrame.WakeUp();
                }
                UsingKeysOrPad = true;
                UiMotion.NoteInput();
            }
            else if (pointer)
            {
                UiGlyphs.Note(mouse);
                UsingKeysOrPad = false;
                UiMotion.NoteInput();
            }
        }

        /// <summary>For the capture tool and checks: pretend the player just used a key or pad.</summary>
        public static void ForceKeysOrPad(bool value)
        {
            UsingKeysOrPad = value;
            UiFocusFrame.WakeUp();
        }

        static bool PadActive(Gamepad pad)
        {
            return pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.dpad.ReadValue().sqrMagnitude > 0.25f
                || pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame
                || pad.buttonNorth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame
                || pad.startButton.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame;
        }
    }
}
