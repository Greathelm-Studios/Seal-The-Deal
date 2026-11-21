using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;

namespace CloseTheDeal.UI
{
    /// <summary>The kind of device the player last used, for the labels on key hints.</summary>
    public enum UiDeviceKind
    {
        /// <summary>Keyboard and mouse: key names (F, Esc, Enter).</summary>
        KeyboardMouse = 0,
        /// <summary>An Xbox controller: A, B, X, Y, LB, RB, LT, RT, Menu, View.</summary>
        Xbox = 1,
        /// <summary>A PlayStation controller: Cross, Circle, Square, Triangle, L1, R1, L2, R2, Options, Share.</summary>
        PlayStation = 2,
        /// <summary>Any other controller: labelled like an Xbox pad, the layout most PC pads copy.</summary>
        Gamepad = 3
    }

    /// <summary>Tracks the active input device kind and formats key/button names from action bindings.</summary>
    public static class UiGlyphs
    {
        /// <summary>The device kind the player used last. Starts on keyboard and mouse.</summary>
        public static UiDeviceKind Current { get; private set; } = UiDeviceKind.KeyboardMouse;

        /// <summary>True when the player is on a controller of any kind.</summary>
        public static bool OnPad => Current != UiDeviceKind.KeyboardMouse;

        /// <summary>Raised when the device kind changes (keyboard to pad, or Xbox pad to PlayStation pad).</summary>
        public static event Action Changed;

        /// <summary>Called by UiInput.Poll with the device that just produced input.</summary>
        internal static void Note(InputDevice device)
        {
            UiDeviceKind kind = KindOf(device);
            if (kind == Current)
                return;
            Current = kind;
            Changed?.Invoke();
        }

        /// <summary>For the capture tool and the gallery: show the labels for this device kind.</summary>
        public static void Force(UiDeviceKind kind)
        {
            if (kind == Current)
                return;
            Current = kind;
            Changed?.Invoke();
        }

        static InputActionAsset _rebindSource;
        static InputActionAsset _rebound;
        static string _reboundJson;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (_rebound != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_rebound);
                else UnityEngine.Object.DestroyImmediate(_rebound);
            }
            _rebound = null;
            _rebindSource = null;
            _reboundJson = null;
            Current = UiDeviceKind.KeyboardMouse;
            Changed = null;
        }

        /// <summary>Returns a cached read-only copy of source with InputPrefs binding overrides applied.</summary>
        public static InputActionAsset WithRebinds(InputActionAsset source)
        {
            if (source == null)
                return null;
            string json = InputPrefs.BindingOverridesJson ?? string.Empty;
            if (source == _rebindSource && _rebound != null && json == _reboundJson)
                return _rebound;
            if (_rebound == null || source != _rebindSource)
            {
                if (_rebound != null)
                    UnityEngine.Object.Destroy(_rebound);
                _rebound = UnityEngine.Object.Instantiate(source);
                _rebindSource = source;
            }

            _rebound.RemoveAllBindingOverrides();
            if (json.Length > 0)
                _rebound.LoadBindingOverridesFromJson(json);
            _reboundJson = json;
            return _rebound;
        }

        public static UiDeviceKind KindOf(InputDevice device)
        {
            switch (device)
            {
                case DualShockGamepad: return UiDeviceKind.PlayStation; // DualSense derives from it too
                case XInputController: return UiDeviceKind.Xbox;
                case UnityEngine.InputSystem.Gamepad: return UiDeviceKind.Gamepad;
                default: return UiDeviceKind.KeyboardMouse;
            }
        }

        /// <summary>Returns the display label for an action on kind (allocates; call on setup or Changed only).</summary>
        public static string For(InputAction action, UiDeviceKind kind, string part = null)
        {
            if (action == null)
                return string.Empty;
            bool pad = kind != UiDeviceKind.KeyboardMouse;
            bool wholeAction = string.IsNullOrEmpty(part);
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding b = bindings[i];
                if (b.isComposite)
                {
                    string composite = wholeAction ? CompositeText(action, i, kind) : string.Empty;
                    if (composite.Length > 0)
                        return composite;
                }
                else if (Matches(b, part, wholeAction, pad))
                    return Name(b.effectivePath, kind);
            }

            return string.Empty;
        }

        static bool Matches(InputBinding b, string part, bool wholeAction, bool pad)
        {
            if (wholeAction ? b.isPartOfComposite : !string.Equals(b.name, part, StringComparison.OrdinalIgnoreCase))
                return false;
            string path = b.effectivePath;
            if (string.IsNullOrEmpty(path))
                return false;
            return IsUsagePath(path) || (IsPadPath(path) == pad && IsKnownDevicePath(path));
        }

        static bool IsUsagePath(string path) => path.StartsWith("*/{", StringComparison.Ordinal);

        static string UsageName(string path, UiDeviceKind kind)
        {
            bool pad = kind != UiDeviceKind.KeyboardMouse;
            bool ps = kind == UiDeviceKind.PlayStation;
            if (path.EndsWith("{Submit}", StringComparison.Ordinal))
                return pad ? PadName("buttonSouth", ps) : "Enter";
            if (path.EndsWith("{Cancel}", StringComparison.Ordinal))
                return pad ? PadName("buttonEast", ps) : "Esc";
            return string.Empty;
        }

        /// <summary>The label for one control path ("&lt;Gamepad&gt;/buttonSouth", "&lt;Keyboard&gt;/f") on a device kind.</summary>
        public static string Name(string path, UiDeviceKind kind)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            if (IsUsagePath(path))
                return UsageName(path, kind);
            string control = Control(path);
            if (IsPadPath(path))
                return PadName(control, kind == UiDeviceKind.PlayStation);
            return KeyNames.TryGetValue(control, out string name)
                ? name
                : InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        /// <summary>A pad control's name ("buttonSouth", "leftShoulder") as printed on an Xbox or a PlayStation pad.</summary>
        public static string PadName(string control, bool playStation)
        {
            return PadNames.TryGetValue(control, out (string xbox, string ps) names) ? (playStation ? names.ps : names.xbox) : control;
        }

        static readonly Dictionary<string, string> KeyNames = new()
        {
            { "escape", "Esc" }, { "enter", "Enter" }, { "space", "Space" }, { "leftShift", "Shift" }, { "leftCtrl", "Ctrl" },
            { "leftAlt", "Alt" }, { "tab", "Tab" }, { "upArrow", "\u2191" }, { "downArrow", "\u2193" }, { "leftArrow", "\u2190" },
            { "rightArrow", "\u2192" }, { "leftButton", "LMB" }, { "rightButton", "RMB" }, { "middleButton", "MMB" },
            { "delta", "Mouse" }, { "scroll", "Wheel" },
        };

        static readonly Dictionary<string, (string xbox, string ps)> PadNames = new()
        {
            { "buttonSouth", ("A", "Cross") }, { "buttonEast", ("B", "Circle") }, { "buttonWest", ("X", "Square") },
            { "buttonNorth", ("Y", "Triangle") }, { "leftShoulder", ("LB", "L1") }, { "rightShoulder", ("RB", "R1") },
            { "leftTrigger", ("LT", "L2") }, { "rightTrigger", ("RT", "R2") }, { "leftStickPress", ("L3", "L3") },
            { "rightStickPress", ("R3", "R3") }, { "start", ("Menu", "Options") }, { "select", ("View", "Share") },
            { "leftStick", ("LS", "L stick") }, { "rightStick", ("RS", "R stick") }, { "dpad", ("D-pad", "D-pad") },
            { "dpad/up", ("D-pad \u2191", "D-pad \u2191") }, { "dpad/down", ("D-pad \u2193", "D-pad \u2193") },
            { "dpad/left", ("D-pad \u2190", "D-pad \u2190") }, { "dpad/right", ("D-pad \u2192", "D-pad \u2192") },
        };

        static string CompositeText(InputAction action, int compositeIndex, UiDeviceKind kind)
        {
            var bindings = action.bindings;
            bool pad = kind != UiDeviceKind.KeyboardMouse;
            var text = new StringBuilder(16);
            string lastPart = null;
            for (int i = compositeIndex + 1; i < bindings.Count && bindings[i].isPartOfComposite; i++)
            {
                InputBinding b = bindings[i];
                string path = b.effectivePath;
                if (string.IsNullOrEmpty(path) || IsPadPath(path) != pad || b.name == lastPart)
                    continue;
                lastPart = b.name;
                if (text.Length > 0)
                    text.Append('/');
                text.Append(Name(path, kind));
            }

            return text.ToString();
        }

        static string Control(string path)
        {
            int slash = path.IndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        static bool IsPadPath(string path)
        {
            return path.StartsWith("<Gamepad>", StringComparison.Ordinal) || path.StartsWith("<XInputController>", StringComparison.Ordinal)
                || path.StartsWith("<DualShockGamepad>", StringComparison.Ordinal);
        }

        static bool IsKnownDevicePath(string path)
        {
            return IsPadPath(path) || path.StartsWith("<Keyboard>", StringComparison.Ordinal) || path.StartsWith("<Mouse>", StringComparison.Ordinal)
                || path.StartsWith("<Pointer>", StringComparison.Ordinal);
        }
    }
}
