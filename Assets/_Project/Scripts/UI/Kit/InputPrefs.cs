using System;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The player's own control preferences, kept on this PC: look sensitivity and stick speed as multipliers of the
    /// designer's MovementProfile values (the profile itself is never written, AGENTS.md §4), invert look, the look
    /// stick's dead zone, and key binding overrides. The movement code reads these; it sets the Hooked flags when it
    /// does, and the settings screen only shows a row whose flag is set, so no row ever changes nothing.
    /// Contract agreed with the movement lane (AgentHub #461-#465). Changed fires once per change.
    /// </summary>
    public static class InputPrefs
    {
        const string SensitivityKey = "input.lookSensitivity";
        const string InvertKey = "input.invertY";
        const string StickSpeedKey = "input.stickLookSpeed";
        const string DeadZoneKey = "input.stickDeadZone";
        const string BindingsKey = "input.bindings";

        public const float SensitivityMin = 0.25f, SensitivityMax = 3f;
        public const float StickSpeedMin = 0.5f, StickSpeedMax = 2f;
        public const float DeadZoneMax = 0.5f;

        /// <summary>Set by the camera code once it applies sensitivity, invert and stick speed.</summary>
        public static bool LookHooked { get; set; }

        /// <summary>Set by the input reader once it applies binding overrides and the dead zone.</summary>
        public static bool BindingsHooked { get; set; }

        public static event Action Changed;

        static bool _loaded;
        static float _sensitivity, _stickSpeed;
        static bool _invert;
        static float _deadZone;
        static string _bindings;

        /// <summary>Mouse look multiplier: 1 = the designer's profile value.</summary>
        public static float LookSensitivity
        {
            get { Load(); return _sensitivity; }
            set { Load(); float v = Mathf.Clamp(value, SensitivityMin, SensitivityMax); if (Mathf.Approximately(v, _sensitivity)) return; _sensitivity = v; Save(); }
        }

        public static bool InvertY
        {
            get { Load(); return _invert; }
            set { Load(); if (_invert == value) return; _invert = value; Save(); }
        }

        /// <summary>Look stick speed multiplier: 1 = the designer's profile value.</summary>
        public static float StickLookSpeed
        {
            get { Load(); return _stickSpeed; }
            set { Load(); float v = Mathf.Clamp(value, StickSpeedMin, StickSpeedMax); if (Mathf.Approximately(v, _stickSpeed)) return; _stickSpeed = v; Save(); }
        }

        /// <summary>The look stick's dead zone, 0-0.5 of full tilt. Null = the designer's profile value.</summary>
        public static float? StickDeadZone
        {
            get { Load(); return _deadZone < 0f ? null : _deadZone; }
            set { Load(); float v = value.HasValue ? Mathf.Clamp(value.Value, 0f, DeadZoneMax) : -1f; if (Mathf.Approximately(v, _deadZone)) return; _deadZone = v; Save(); }
        }

        /// <summary>The Player map's rebinds, as InputActionAsset.SaveBindingOverridesAsJson writes them. Empty = defaults.</summary>
        public static string BindingOverridesJson
        {
            get { Load(); return _bindings; }
            set { Load(); string v = value ?? string.Empty; if (v == _bindings) return; _bindings = v; Save(); }
        }

        public static void ResetLook()
        {
            Load();
            _sensitivity = 1f;
            _invert = false;
            _stickSpeed = 1f;
            _deadZone = -1f;
            Save();
        }

        /// <summary>Forgets the cache so the next read comes from disk (the relaunch check).</summary>
        public static void Reload()
        {
            _loaded = false;
            Load();
        }

        static void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            _sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), SensitivityMin, SensitivityMax);
            _invert = PlayerPrefs.GetInt(InvertKey, 0) != 0;
            _stickSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(StickSpeedKey, 1f), StickSpeedMin, StickSpeedMax);
            _deadZone = PlayerPrefs.GetFloat(DeadZoneKey, -1f);
            _bindings = PlayerPrefs.GetString(BindingsKey, string.Empty);
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(SensitivityKey, _sensitivity);
            PlayerPrefs.SetInt(InvertKey, _invert ? 1 : 0);
            PlayerPrefs.SetFloat(StickSpeedKey, _stickSpeed);
            PlayerPrefs.SetFloat(DeadZoneKey, _deadZone);
            PlayerPrefs.SetString(BindingsKey, _bindings);
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loaded = false;
            Changed = null;
        }
    }
}
