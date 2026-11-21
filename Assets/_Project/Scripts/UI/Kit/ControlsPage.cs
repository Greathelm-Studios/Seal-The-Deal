using System;
using CloseTheDeal.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CloseTheDeal.UI
{
    /// <summary>Settings Controls tab: look preferences and key binding overrides stored in InputPrefs.</summary>
    [AddComponentMenu("Close the Deal/UI/Settings Page - Controls")]
    public sealed class ControlsPage : UiSettingsPage
    {
        [Serializable]
        public sealed class BindingRow
        {
            [Tooltip("The action in the Player map, e.g. Jump.")]
            public string Action;
            [Tooltip("For a two-way action (Lean): which side, 'negative' or 'positive'. Empty otherwise.")]
            public string Part;
            [Tooltip("The row's whole object, shown or hidden with it.")]
            public GameObject Root;
            [Tooltip("The key as a button (shown once custom keys are live).")]
            public UiButton Button;
            [Tooltip("The key as a plain cap (shown until custom keys are live).")]
            public TMP_Text Cap;
            [Tooltip("The pad button.")]
            public TMP_Text Pad;

            [NonSerialized] public Action Listen;
        }

        [Tooltip("The project's input actions. Read only: rebinds are overrides kept in the player's settings.")]
        [SerializeField] InputActionAsset _actions;
        [SerializeField] UiSlider _sensitivity;
        [SerializeField] UiToggle _invert;
        [SerializeField] UiSlider _stickSpeed;
        [SerializeField] BindingRow[] _bindings = Array.Empty<BindingRow>();
        [Tooltip("The yes / no dialog the key clash question uses.")]
        [SerializeField] UiConfirmDialog _dialog;
        [Tooltip("The status line, for the 'press a key' prompt.")]
        [SerializeField] UiStatusLine _status;
        [Tooltip("The key list's block; it moves up into the look rows' place while those are hidden.")]
        [SerializeField] RectTransform _keys;
        [Tooltip("Height of the look rows together, in pixels.")]
        [SerializeField] float _lookHeight = 192f;

        InputActionAsset _working;
        bool _hasCustomBindings;
        CanvasGroup _screenGroup;
        InputActionRebindingExtensions.RebindingOperation _operation;
        BindingRow _listening;
        string _pendingPath;
        int _pendingIndex;
        InputAction _pendingAction;
        InputAction _clashAction;
        int _clashIndex;

        public bool Listening => _operation != null;

        void Update()
        {
            if (_operation != null && Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
                _operation.Cancel();
        }

        void OnEnable()
        {
            _sensitivity.minValue = 5f;
            _sensitivity.maxValue = 60f;
            _sensitivity.Configure(5f, "%");
            _stickSpeed.minValue = 5f;
            _stickSpeed.maxValue = 20f;
            _stickSpeed.Configure(10f, "%");
            _sensitivity.Changed += OnSensitivity;
            _invert.Changed += OnInvert;
            _stickSpeed.Changed += OnStickSpeed;
            UiGlyphs.Changed += OnDeviceChanged;
            foreach (BindingRow row in _bindings)
            {
                if (row.Button != null)
                {
                    // A key cell answers with KeyBindListen; a Confirm in the same frame would mask it.
                    row.Button.ConfirmSound = UiSoundEvent.None;
                    row.Button.Clicked += row.Listen ??= () => StartListening(row);
                }
            }
        }

        void OnDisable()
        {
            _sensitivity.Changed -= OnSensitivity;
            _invert.Changed -= OnInvert;
            _stickSpeed.Changed -= OnStickSpeed;
            UiGlyphs.Changed -= OnDeviceChanged;
            foreach (BindingRow row in _bindings)
            {
                if (row.Button != null && row.Listen != null)
                    row.Button.Clicked -= row.Listen;
            }

            CancelListening();
        }

        void OnDestroy()
        {
            if (_working != null)
                Destroy(_working);
        }

        public override void ShowSaved()
        {
            bool look = InputPrefs.LookHooked;
            _sensitivity.gameObject.transform.parent.gameObject.SetActive(look);
            _invert.gameObject.transform.parent.gameObject.SetActive(look);
            _stickSpeed.gameObject.transform.parent.gameObject.SetActive(look);
            if (_keys != null)
                _keys.anchoredPosition = new Vector2(_keys.anchoredPosition.x, look ? 0f : _lookHeight);
            _sensitivity.SetDisplayWithoutNotify(Mathf.RoundToInt(InputPrefs.LookSensitivity * 100f));
            _invert.SetWithoutNotify(InputPrefs.InvertY);
            _stickSpeed.SetDisplayWithoutNotify(Mathf.RoundToInt(InputPrefs.StickLookSpeed * 100f));
            LoadWorking();
            DrawBindings();
        }

        public override bool AtDefaults =>
            Mathf.Approximately(InputPrefs.LookSensitivity, 1f) && !InputPrefs.InvertY && Mathf.Approximately(InputPrefs.StickLookSpeed, 1f)
            && InputPrefs.StickDeadZone == null
            && (_working != null ? !_hasCustomBindings : string.IsNullOrEmpty(InputPrefs.BindingOverridesJson));

        public override void ResetToDefaults()
        {
            InputPrefs.ResetLook();
            InputPrefs.BindingOverridesJson = string.Empty;
            ShowSaved();
        }

        public override void Hidden() => CancelListening();

        public override bool ShowsChanges => false;

        void OnSensitivity(int percent) { InputPrefs.LookSensitivity = percent / 100f; RaiseSaved(); }

        void OnInvert(bool on) { InputPrefs.InvertY = on; RaiseSaved(); }

        void OnStickSpeed(int percent) { InputPrefs.StickLookSpeed = percent / 100f; RaiseSaved(); }

        void OnDeviceChanged()
        {
            if (_operation == null)
                DrawBindings();
        }

        void LoadWorking()
        {
            if (_actions == null)
                return;
            if (_working == null)
                _working = Instantiate(_actions);
            _working.RemoveAllBindingOverrides();
            _hasCustomBindings = false;
            if (!string.IsNullOrEmpty(InputPrefs.BindingOverridesJson))
            {
                _working.LoadBindingOverridesFromJson(InputPrefs.BindingOverridesJson);
                RemoveDefaultOverrides();
                foreach (var binding in _working.bindings)
                    _hasCustomBindings |= binding.hasOverrides;
            }
        }

        void DrawBindings()
        {
            foreach (BindingRow row in _bindings)
            {
                InputAction action = _working != null ? _working.FindAction("Player/" + row.Action) : null;
                bool fixedInput = IsFixedInput(row.Action);
                bool live = InputPrefs.BindingsHooked && !fixedInput;
                string key = action != null ? KeyName(action, KeyIndex(action, row.Part)) : "—";
                string pad = action != null ? PadName(action, BindingIndex(action, row.Part, "<Gamepad>")) : "—";
                if (fixedInput && action != null)
                {
                    int keyIndex = KeyIndex(action, row.Part);
                    if (keyIndex >= 0)
                        key = UiGlyphs.Name(action.bindings[keyIndex].path, UiDeviceKind.KeyboardMouse);
                    if (row.Action == "Reload")
                        pad = UiGlyphs.Name(WeaponHolder.ReloadPadBinding, UiGlyphs.Current);
                }
                if (row.Cap != null)
                {
                    row.Cap.text = key;
                    row.Cap.transform.parent.gameObject.SetActive(!live);
                }

                if (row.Button != null)
                {
                    row.Button.gameObject.SetActive(live);
                    if (row.Button.Fx.Label != null)
                        row.Button.Fx.Label.text = key;
                }

                if (row.Pad != null)
                    row.Pad.text = pad;
            }
        }

        static int KeyIndex(InputAction action, string part)
        {
            int index = BindingIndex(action, part, "<Keyboard>");
            return index >= 0 ? index : BindingIndex(action, part, "<Mouse>");
        }

        static int BindingIndex(InputAction action, string part, string device)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding b = action.bindings[i];
                if (b.isComposite)
                    continue;
                if (!string.IsNullOrEmpty(part) && !string.Equals(b.name, part, StringComparison.OrdinalIgnoreCase))
                    continue;
                string path = b.effectivePath ?? string.Empty;
                string original = b.path ?? string.Empty;
                if (path.StartsWith(device, StringComparison.Ordinal) || original.StartsWith(device, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        static string KeyName(InputAction action, int index)
        {
            if (index < 0)
                return "—";
            string path = action.bindings[index].effectivePath;
            return string.IsNullOrEmpty(path) ? "—" : UiGlyphs.Name(path, UiDeviceKind.KeyboardMouse);
        }

        static string PadName(InputAction action, int index)
        {
            if (index < 0)
                return "—";
            UiDeviceKind kind = UiGlyphs.Current == UiDeviceKind.PlayStation ? UiDeviceKind.PlayStation : UiDeviceKind.Xbox;
            return UiGlyphs.Name(action.bindings[index].effectivePath, kind);
        }

        void StartListening(BindingRow row)
        {
            if (_operation != null || _working == null || !InputPrefs.BindingsHooked)
                return;
            InputAction action = _working.FindAction("Player/" + row.Action);
            int index = action != null ? KeyIndex(action, row.Part) : -1;
            if (index < 0)
                return;

            _listening = row;
            if (row.Button.Fx.Label != null)
                row.Button.Fx.Label.text = "Press a key";
            _status.Set("Press a key for " + LabelOf(row) + ". Esc cancels.", UiStatusKind.Busy);
            UiSound.Play(UiSoundEvent.KeyBindListen);
            UiInput.Capturing = true;
            if (EventSystem.current != null)
                EventSystem.current.sendNavigationEvents = false;
            // Clicks go to the capture, not to the buttons under the pointer.
            UiScreen screen = GetComponentInParent<UiScreen>();
            _screenGroup = screen != null ? screen.GetComponent<CanvasGroup>() : null;
            if (_screenGroup != null)
                _screenGroup.blocksRaycasts = false;

            action.Disable();
            _operation = action.PerformInteractiveRebinding(index)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.05f)
                .OnComplete(op => Finish(action, index, true))
                .OnCancel(op => Finish(action, index, false))
                .Start();
        }

        void Finish(InputAction action, int index, bool chosen)
        {
            _operation?.Dispose();
            _operation = null;
            EndCapture();

            if (!chosen)
            {
                LoadWorking();
                DrawBindings();
                _status.Set("Kept " + KeyName(action, index) + ".", UiStatusKind.Info);
                UiSound.Play(UiSoundEvent.Back);
                return;
            }

            string path = action.bindings[index].effectivePath;
            if (FindClash(action, index, path, out InputAction other, out int otherIndex))
            {
                if (HasRow(other.name))
                    AskAboutClash(action, index, path, other, otherIndex);
                else
                    RefuseFixedKey(path, other, otherIndex);
                return;
            }

            Commit("Saved on this PC.");
        }

        // Weapons and items consume their own fixed actions in the game.
        static bool IsFixedInput(string action) => action == "Attack" || action == "AltFire" || action == "Reload";

        bool HasRow(string actionName)
        {
            foreach (BindingRow row in _bindings)
            {
                if (!IsFixedInput(actionName) && string.Equals(row.Action, actionName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        void RefuseFixedKey(string path, InputAction other, int otherIndex)
        {
            LoadWorking();
            DrawBindings();
            string key = UiGlyphs.Name(path, UiDeviceKind.KeyboardMouse);
            _status.Set(key + " is kept for " + NameOfAction(other.name, other.bindings[otherIndex].name) + ". Choose another key.", UiStatusKind.Refusal);
            UiSound.Play(UiSoundEvent.Error);
        }

        bool FindClash(InputAction action, int index, string path, out InputAction other, out int otherIndex)
        {
            other = null;
            otherIndex = -1;
            foreach (InputAction candidate in _working.FindActionMap("Player").actions)
            {
                for (int i = 0; i < candidate.bindings.Count; i++)
                {
                    if ((candidate == action && i == index)
                        || !string.Equals(IsFixedInput(candidate.name) ? candidate.bindings[i].path : candidate.bindings[i].effectivePath, path, StringComparison.OrdinalIgnoreCase))
                        continue;
                    bool fixedKey = !HasRow(candidate.name);
                    if (other == null || fixedKey)
                    {
                        other = candidate;
                        otherIndex = i;
                    }

                    if (fixedKey)
                        return true;
                }
            }

            return other != null;
        }

        void AskAboutClash(InputAction action, int index, string path, InputAction other, int otherIndex)
        {
            _pendingAction = action;
            _pendingIndex = index;
            _pendingPath = path;
            _clashAction = other;
            _clashIndex = otherIndex;
            string key = UiGlyphs.Name(path, UiDeviceKind.KeyboardMouse);
            string otherName = NameOfAction(other.name, other.bindings[otherIndex].name);
            _status.Set(key + " is already used for " + otherName + ".", UiStatusKind.Refusal);
            UiSound.Play(UiSoundEvent.Error);
            _dialog.Configure(key + " is already used for " + otherName + ".", "Assigning it here clears it there.", "Assign", "Cancel", AssignAnyway, CancelClash, UiSoundEvent.None); // Assign answers with KeyBindSet
            GetComponentInParent<UiScreen>().Stack.Push(_dialog);
        }

        void AssignAnyway()
        {
            _clashAction.ApplyBindingOverride(_clashIndex, string.Empty);
            string line = "Moved. " + NameOfAction(_clashAction.name, _clashAction.bindings[_clashIndex].name) + " has no key now.";
            // Save before closing (closing shows the page again, which reloads the saved keys); report after (the
            // screen resets its status line as it uncovers).
            SaveKeys();
            _dialog.Stack?.Pop(false);
            DrawBindings();
            RaiseSaved(line);
        }

        void CancelClash()
        {
            _pendingAction.RemoveBindingOverride(_pendingIndex);
            // Put back whatever override was there before this attempt.
            LoadWorking();
            DrawBindings();
            _status.Set("Nothing changed.", UiStatusKind.Info);
        }

        void Commit(string line)
        {
            SaveKeys();
            DrawBindings();
            RaiseSaved(line);
        }

        void RemoveDefaultOverrides()
        {
            foreach (var action in _working)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.overridePath != null && binding.overrideProcessors == null && binding.overrideInteractions == null
                        && string.Equals(binding.overridePath, binding.path, StringComparison.OrdinalIgnoreCase))
                        action.RemoveBindingOverride(i);
                }
            }
        }

        void SaveKeys()
        {
            RemoveDefaultOverrides();
            string json = _working.SaveBindingOverridesAsJson();
            _hasCustomBindings = !string.IsNullOrEmpty(json);
            InputPrefs.BindingOverridesJson = json;
            UiSound.Play(UiSoundEvent.KeyBindSet);
        }

        void CancelListening()
        {
            if (_operation == null)
                return;
            _operation.Cancel();
        }

        void EndCapture()
        {
            if (EventSystem.current != null)
                EventSystem.current.sendNavigationEvents = true;
            if (_screenGroup != null)
                _screenGroup.blocksRaycasts = true;
            _screenGroup = null;
            UiInput.ReleaseCapture();
            _listening = null;
        }

        string LabelOf(BindingRow row) => NameOfAction(row.Action, row.Part);

        /// <summary>Display label for a Player-map action.</summary>
        public static string NameOfAction(string action, string part)
        {
            switch (action)
            {
                case "Attack": return "Fire";
                case "AltFire": return "Use item";
                case "Throw": return "Drop / Throw";
                case "Voice": return "Mute / Push to talk";
                case "Lean": return string.Equals(part, "negative", StringComparison.OrdinalIgnoreCase) ? "Lean left" : "Lean right";
                default: return action;
            }
        }
    }
}
