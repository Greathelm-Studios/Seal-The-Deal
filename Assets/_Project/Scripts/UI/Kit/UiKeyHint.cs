using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace CloseTheDeal.UI
{
    /// <summary>Device-aware key/button cap hint that resizes its cap and label when input device or rebinds change.</summary>
    [AddComponentMenu("Seal the Deal/UI/Key Hint")]
    public sealed class UiKeyHint : MonoBehaviour
    {
        [Tooltip("The input actions to read. Leave empty for UI actions (Submit, Cancel, Navigate): those come from the scene's EventSystem.")]
        [SerializeField] InputActionAsset _actions;
        [Tooltip("The action as Map/Action, e.g. Player/Interact or UI/Cancel. Empty: the hint shows its text unchanged.")]
        [SerializeField] string _action = string.Empty;
        [Tooltip("For a two-way action (Lean): which side, 'negative' or 'positive'. Empty otherwise.")]
        [SerializeField] string _part = string.Empty;
        [Tooltip("Shown on keyboard and mouse instead of the binding's own name (e.g. 'Mouse' for look). Empty: the binding's name.")]
        [SerializeField] string _keyboardText = string.Empty;
        [Tooltip("On a controller, name this pad control instead of the binding (e.g. leftShoulder, leftStick), printed as the player's pad prints it (LB or L1). Empty: the binding's name.")]
        [SerializeField] string _padControl = string.Empty;

        [Header("Parts")]
        [Tooltip("The key cap's keyline box.")]
        [SerializeField] RectTransform _cap;
        [Tooltip("The key's name inside the cap.")]
        [SerializeField] TMP_Text _key;
        [Tooltip("The words after the cap, e.g. Back.")]
        [SerializeField] TMP_Text _label;
        [Tooltip("Narrowest the cap gets, in pixels at 1920 x 1080.")]
        [Min(0f)] [SerializeField] float _minCapWidth = 36f;
        [Tooltip("Space either side of the key's name inside the cap, in pixels.")]
        [Min(0f)] [SerializeField] float _capPadding = 10f;
        [Tooltip("Gap between the cap and the words, in pixels.")]
        [Min(0f)] [SerializeField] float _labelGap = 10f;

        string _shown;

        /// <summary>The key name now on the cap.</summary>
        public string KeyText => _key != null ? _key.text : string.Empty;

        /// <summary>Points the hint at an action and sets its words, from code.</summary>
        public void Bind(string action, string label, InputActionAsset actions = null, string part = null)
        {
            _action = action ?? string.Empty;
            _part = part ?? string.Empty;
            if (actions != null)
                _actions = actions;
            if (_label != null && label != null)
                _label.text = label;
            _shown = null;
            Refresh();
        }

        void OnEnable()
        {
            UiGlyphs.Changed += Refresh;
            InputPrefs.Changed += Refresh;
            Refresh();
        }

        void Start() => Refresh(); // EventSystem UI actions may enable after OnEnable.

        void OnDisable()
        {
            UiGlyphs.Changed -= Refresh;
            InputPrefs.Changed -= Refresh;
        }

        /// <summary>Re-reads the binding for the current device and refits the cap. Only on a change, never per frame.</summary>
        public void Refresh()
        {
            if (_key == null || (string.IsNullOrEmpty(_action) && string.IsNullOrEmpty(_keyboardText) && string.IsNullOrEmpty(_padControl)))
                return;
            UiDeviceKind kind = UiGlyphs.Current;
            bool pad = kind != UiDeviceKind.KeyboardMouse;
            string text = pad
                ? (string.IsNullOrEmpty(_padControl) ? null : UiGlyphs.PadName(_padControl, kind == UiDeviceKind.PlayStation))
                : _keyboardText;
            if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(_action))
            {
                InputAction action = Resolve();
                if (action == null)
                    return; // the EventSystem isn't up yet: keep the text as built and try again on Start
                text = UiGlyphs.For(action, kind, _part);
            }
            if (string.IsNullOrEmpty(text))
                text = "\u2014"; // nothing bound on this device: a dash, never the other device's key
            SetKeyText(text);
        }

        /// <summary>Displays a key supplied by a gameplay prompt and fits the cap to its device label.</summary>
        public void SetKeyText(string text)
        {
            if (text == _shown)
                return;
            _shown = text;
            _key.text = text;
            Fit();
        }

        InputAction Resolve()
        {
            InputActionAsset asset = _actions;
            if (asset == null)
            {
                EventSystem events = EventSystem.current;
                // currentInputModule is only set on the EventSystem's first Update: read the component itself.
                var module = events != null
                    ? (events.currentInputModule as InputSystemUIInputModule) ?? events.GetComponent<InputSystemUIInputModule>()
                    : null;
                asset = module != null ? module.actionsAsset : null;
            }
            else
                asset = UiGlyphs.WithRebinds(asset);

            return asset != null ? asset.FindAction(_action) : null;
        }

        void Fit()
        {
            if (_cap == null)
                return;
            float width = Mathf.Max(_minCapWidth, Mathf.Ceil(_key.GetPreferredValues(_key.text).x + _capPadding * 2f));
            _cap.sizeDelta = new Vector2(width, _cap.sizeDelta.y);
            var keyRect = _key.rectTransform;
            if (keyRect != _cap && keyRect.parent == _cap.parent)
            {
                keyRect.sizeDelta = new Vector2(width, keyRect.sizeDelta.y);
                keyRect.anchoredPosition = new Vector2(_cap.anchoredPosition.x, keyRect.anchoredPosition.y);
            }

            if (_label != null)
            {
                RectTransform label = _label.rectTransform;
                label.anchoredPosition = new Vector2(_cap.anchoredPosition.x + width + _labelGap, label.anchoredPosition.y);
            }
        }
    }
}
