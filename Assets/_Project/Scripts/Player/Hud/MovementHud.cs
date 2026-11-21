using System;
using CloseTheDeal.Combat;
using CloseTheDeal.MatchMode;
using CloseTheDeal.UI;
using TMPro;
using UnityEngine;

namespace CloseTheDeal.Player.Hud
{
    /// <summary>
    /// Movement's part of the HUD, built from the UI kit's pieces: the status line that says why a
    /// climb was refused, and the controls help card the Help button opens. Everything redraws on
    /// the local player's events (never polled), moves through the kit's one motion driver on
    /// unscaled time, and sounds through the kit's UI sounds. Contract: docs/systems/PLAYER_MOVEMENT.md.
    /// </summary>
    [AddComponentMenu("Close the Deal/Movement/Movement HUD")]
    public sealed class MovementHud : MonoBehaviour, IUiAnimated
    {
        /// <summary>One line of the help card: the action it shows the key for, and what the line says.</summary>
        [Serializable]
        public struct HelpRow
        {
            [Tooltip("Input action shown for keyboard and mouse.")]
            public string Action;
            [Tooltip("Input action shown for a gamepad; empty = the same as above.")]
            public string PadAction;
            [Tooltip("Text shown instead of the binding on keyboard and mouse, for an action whose binding name reads badly (the mouse's look is 'Delta').")]
            public string KeyboardText;
            public RectTransform KeyCap;
            public TMP_Text Key;
            public TMP_Text Label;
        }

        [Tooltip("The kit's tokens: motion timings come from here.")]
        [SerializeField] UiTokens _tokens;

        [Tooltip("The kit status line that shows a refused climb.")]
        [SerializeField] UiStatusLine _status;

        [Tooltip("Fades the status line in and out.")]
        [SerializeField] CanvasGroup _statusGroup;

        [Tooltip("The kit status line that tells a sprinting player how to slide, until they have slid once this session.")]
        [SerializeField] UiStatusLine _slideHint;

        [Tooltip("Fades the slide hint in and out.")]
        [SerializeField] CanvasGroup _slideHintGroup;

        [Tooltip("How long a player must sprint without sliding before the hint shows, in seconds.")]
        [SerializeField] float _slideHintAfter = 1.2f;

        [Tooltip("How long the slide hint stays once shown, in seconds, unless the player slides first.")]
        [SerializeField] float _slideHintSeconds = 3f;

        [Tooltip("How long a refusal stays on screen before it fades, in seconds. Long enough to read twelve words; a second refusal restarts it.")]
        [SerializeField] float _refusalSeconds = 2.2f;

        [Tooltip("The help card's panel, slid in from the left.")]
        [SerializeField] RectTransform _card;

        [Tooltip("Fades the help card.")]
        [SerializeField] CanvasGroup _cardGroup;

        [Tooltip("The small caption naming the device the keys are shown for.")]
        [SerializeField] TMP_Text _deviceCaption;

        [Tooltip("The card's lines, top to bottom. Built by Close the Deal > Lanes > Add Movement HUD.")]
        [SerializeField] HelpRow[] _rows = new HelpRow[0];

        const float KeyCapMinWidth = 48f;
        const float KeyCapPadding = 20f;
        const float LabelGap = 10f;

        PlayerMotor _motor;
        PlayerInputReader _reader;
        float _statusAlpha;
        float _statusHold;
        bool _cardOpen;
        float _cardShown;
        Vector2 _cardRest;

        /// <summary>True while the help card is open or opening.</summary>
        public bool HelpOpen => _cardOpen;

        void Awake()
        {
            if (_card != null)
                _cardRest = _card.anchoredPosition;
        }

        void OnEnable()
        {
            PlayerMotor.LocalChanged += Bind;
            Bind(PlayerMotor.Local);
            _statusAlpha = 0f;
            _statusHold = 0f;
            _cardOpen = false;
            _cardShown = 0f;
            Show();
        }

        void OnDisable()
        {
            PlayerMotor.LocalChanged -= Bind;
            Bind(null);
            UiMotion.Sleep(this);
        }

        void Bind(PlayerMotor motor)
        {
            if (_motor != null)
            {
                _motor.MantleRefused -= ShowRefusal;
                _motor.SprintChanged -= OnSprintChanged;
                _motor.SlideChanged -= OnSlideChanged;
            }
            if (_reader != null)
            {
                _reader.Pressed -= OnPressed;
                _reader.DeviceChanged -= OnDeviceChanged;
            }

            _motor = motor;
            _reader = motor != null ? motor.GetComponent<PlayerInputReader>() : null;
            if (_motor != null)
            {
                _motor.MantleRefused += ShowRefusal;
                _motor.SprintChanged += OnSprintChanged;
                _motor.SlideChanged += OnSlideChanged;
            }
            if (_reader != null)
            {
                _reader.Pressed += OnPressed;
                _reader.DeviceChanged += OnDeviceChanged;
            }

            // A player who leaves or respawns starts with nothing on screen.
            if (motor == null)
            {
                _statusHold = 0f;
                _cardOpen = false;
                UiMotion.Wake(this);
            }
        }

        /// <summary>Set once the player has slid this session; the slide hint never shows again until the game restarts.</summary>
        static bool _hasSlid;
        float _sprintFor = -1f;
        float _hintHold;
        float _hintAlpha;

        void OnSprintChanged(bool sprinting)
        {
            if (_slideHint == null)
                return;

            _sprintFor = sprinting && !_hasSlid ? 0f : -1f;
            if (sprinting && !_hasSlid)
                UiMotion.Wake(this);
        }

        void OnSlideChanged(bool sliding)
        {
            if (!sliding)
                return;
            _hasSlid = true;
            _sprintFor = -1f;
            _hintHold = 0f;
            UiMotion.Wake(this);
        }

        /// <summary>Shows how to slide, with the key for the device in use.</summary>
        void ShowSlideHint()
        {
            if (_slideHint == null || _reader == null)
                return;

            string key = _reader.BindingText("Crouch", _reader.LastUsedGamepad);
            _slideHint.Set($"Sprinting: press {key} to slide.", UiStatusKind.Info);
            _hintHold = _slideHintSeconds;
            UiSound.Play(UiSoundEvent.Tip);
        }

        /// <summary>The words for a refused climb: say why, in the house voice.</summary>
        public static string RefusalText(MantleRefusal why)
        {
            switch (why)
            {
                case MantleRefusal.TooHigh: return "Can't climb that: it's above your reach.";
                case MantleRefusal.NoRoom: return "Can't climb that: there's no room on top.";
                default: return string.Empty;
            }
        }

        /// <summary>Says why a climb was refused, with the kit's refusal mark and error sound. Raised by the local player's motor.</summary>
        public void ShowRefusal(MantleRefusal why)
        {
            if (_status == null)
                return;

            _status.Set(RefusalText(why), UiStatusKind.Refusal);
            _statusHold = _refusalSeconds;
            UiSound.Play(UiSoundEvent.Error);
            UiMotion.Wake(this);
        }

        void OnPressed(PlayerPress press)
        {
            if (press == PlayerPress.Help)
                SetHelpOpen(!_cardOpen);
        }

        /// <summary>Opens or closes the help card with its motion and sound.</summary>
        public void SetHelpOpen(bool open)
        {
            if (open == _cardOpen || _card == null)
                return;

            _cardOpen = open;
            if (open)
                RefreshRows();
            UiSound.Play(open ? UiSoundEvent.PanelOpen : UiSoundEvent.PanelClose);
            UiMotion.Wake(this);
        }

        void OnDeviceChanged(bool gamepad)
        {
            if (_cardOpen)
                RefreshRows();
        }

        /// <summary>Writes each line's key from the player's own bindings for the device in use, sizing its key cap to fit.</summary>
        void RefreshRows()
        {
            if (_reader == null)
                return;

            bool pad = _reader.LastUsedGamepad;
            if (_deviceCaption != null)
                _deviceCaption.text = pad ? "CONTROLLER" : "KEYBOARD AND MOUSE";

            var device = pad ? UiGlyphs.KindOf(UnityEngine.InputSystem.Gamepad.current) : UiDeviceKind.KeyboardMouse;
            for (int i = 0; i < _rows.Length; i++)
            {
                HelpRow row = _rows[i];
                if (row.Key == null)
                    continue;

                string action = pad && !string.IsNullOrEmpty(row.PadAction) ? row.PadAction : row.Action;
                // These lane-owned actions have fixed bindings independent of the movement actions asset.
                string text = action switch
                {
                    "Reload" => UiGlyphs.Name(pad ? WeaponHolder.ReloadPadBinding : WeaponHolder.ReloadKeyboardBinding, device),
                    "Interact" => UiGlyphs.Name(pad ? FirmMember.InteractPad : FirmMember.InteractKey, device),
                    "Throw" => UiGlyphs.Name(pad ? FirmMember.DropPad : FirmMember.DropKey, device),
                    _ => !pad && !string.IsNullOrEmpty(row.KeyboardText) ? row.KeyboardText : _reader.BindingText(action, pad)
                };
                row.Key.text = text;
                float width = Mathf.Max(KeyCapMinWidth, Mathf.Ceil(row.Key.GetPreferredValues(text).x) + KeyCapPadding);
                Resize(row.KeyCap, width);
                Resize(row.Key.rectTransform, width);
                if (row.Label != null)
                {
                    Vector2 at = row.Label.rectTransform.anchoredPosition;
                    row.Label.rectTransform.anchoredPosition = new Vector2(width + LabelGap, at.y);
                }
            }
        }

        static void Resize(RectTransform rect, float width)
        {
            if (rect != null)
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        public bool Tick(float dt)
        {
            float statusIn = Seconds(_tokens != null ? _tokens.HoverIn : 0.1f);
            float panelIn = Seconds(_tokens != null ? _tokens.PanelIn : 0.2f);
            float panelOut = Seconds(_tokens != null ? _tokens.PanelOut : 0.14f);

            if (_statusHold > 0f)
            {
                _statusHold -= dt;
                _statusAlpha = Mathf.MoveTowards(_statusAlpha, 1f, dt / statusIn);
            }
            else
            {
                _statusAlpha = Mathf.MoveTowards(_statusAlpha, 0f, dt / panelOut);
            }

            _cardShown = Mathf.MoveTowards(_cardShown, _cardOpen ? 1f : 0f, dt / (_cardOpen ? panelIn : panelOut));

            if (_sprintFor >= 0f)
            {
                _sprintFor += dt;
                if (_sprintFor >= _slideHintAfter)
                {
                    _sprintFor = -1f;
                    ShowSlideHint();
                }
            }
            if (_hintHold > 0f)
            {
                _hintHold -= dt;
                _hintAlpha = Mathf.MoveTowards(_hintAlpha, 1f, dt / statusIn);
            }
            else
            {
                _hintAlpha = Mathf.MoveTowards(_hintAlpha, 0f, dt / panelOut);
            }
            Show();

            bool cardSettled = _cardShown == (_cardOpen ? 1f : 0f);
            return _statusHold > 0f || _statusAlpha > 0f || !cardSettled || _sprintFor >= 0f || _hintHold > 0f || _hintAlpha > 0f;
        }

        void Show()
        {
            if (_statusGroup != null)
                _statusGroup.alpha = _statusAlpha;
            if (_slideHintGroup != null)
                _slideHintGroup.alpha = _hintAlpha;

            if (_cardGroup != null)
                _cardGroup.alpha = _cardShown;

            if (_card == null)
                return;

            // Ease out on the way in, ease in on the way out; with Reduce motion it only fades.
            float slide = _tokens != null ? _tokens.PanelSlideIn : 24f;
            float eased = _cardOpen ? 1f - (1f - _cardShown) * (1f - _cardShown) : _cardShown * _cardShown;
            float offset = UiMotion.Reduced ? 0f : -(1f - eased) * slide;
            _card.anchoredPosition = _cardRest + new Vector2(offset, 0f);
        }

        static float Seconds(float value) => Mathf.Max(value, 0.01f);
    }
}
