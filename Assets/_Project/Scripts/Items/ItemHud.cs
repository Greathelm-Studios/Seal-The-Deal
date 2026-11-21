using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.Items
{
    /// <summary>Item pickup prompt, refusal feedback, and held-item HUD panel built from the UI kit. Contract: docs/systems/ITEMS.md.</summary>
    [DisallowMultipleComponent]
    public sealed class ItemHud : MonoBehaviour, IUiAnimated
    {
        [Tooltip("The kit's design tokens: colours, timings, springs. Read, never written.")]
        [SerializeField] UiTokens _tokens;

        [Header("Prompt under the crosshair")]
        [SerializeField] RectTransform _prompt;
        [SerializeField] CanvasGroup _promptGroup;
        [SerializeField] RectTransform _promptPlate;
        [SerializeField] Image _promptEdge;
        [SerializeField] GameObject _promptKeyCap;
        [SerializeField] TMP_Text _promptKey;
        [SerializeField] TMP_Text _promptLabel;

        [Header("Hands slot")]
        [SerializeField] RectTransform _hands;
        [SerializeField] CanvasGroup _handsGroup;
        [SerializeField] TMP_Text _handsName;
        [SerializeField] RectTransform _handsLoadFill;
        [SerializeField] GameObject _useHint;
        [SerializeField] TMP_Text _useLabel;
        [Tooltip("The key caps' letters in the hands slot, relabelled for a gamepad.")]
        [SerializeField] TMP_Text _throwKey;
        [SerializeField] TMP_Text _dropKey;
        [SerializeField] TMP_Text _useKey;

        [Tooltip("How long a refusal stays in the prompt's place, in seconds.")]
        [Min(0.3f)] [SerializeField] float _refusalSeconds = 1.6f;

        [Tooltip("Room left and right of the prompt's words inside its plate, in pixels.")]
        [Min(0f)] [SerializeField] float _promptPadding = 16f;

        ItemCarrier _carrier;
        PromptKind _lookKind;
        string _lookText = "";
        float _refusalLeft;
        DenyReason _refusal;
        bool _shown;

        UiTween _promptFade;
        UiTween _promptRise;
        UiSpring _shake;
        UiTween _handsFade;
        UiTween _handsRise;
        Vector2 _promptHome;
        Vector2 _handsHome;
        float _loadWidth;
        float _labelAfterKey;
        float _promptCapWidth;

        /// <summary>What the prompt reads for a person, key first ("F · Pick up Chair"); empty while it is hidden. For checks and captions.</summary>
        public string PromptReading => !_shown ? "" : _promptKeyCap.activeSelf ? _promptKey.text + " · " + _promptLabel.text : _promptLabel.text;

        /// <summary>What the hands slot names; empty while the hands are empty.</summary>
        public string HandsReading => _handsFade.To > 0.5f ? _handsName.text : "";

        bool Wired()
        {
            Object[] parts = { _tokens, _prompt, _promptGroup, _promptPlate, _promptEdge, _promptKeyCap, _promptKey, _promptLabel, _hands, _handsGroup, _handsName, _handsLoadFill, _useHint, _useLabel };
            foreach (Object part in parts)
            {
                if (part == null)
                    return false;
            }

            return true;
        }

        void Awake()
        {
            // A scene built by an older setup can hold this component with parts unset: say so
            // once and stay off, rather than throwing every frame.
            if (!Wired())
            {
                Debug.LogError($"[Items] The item HUD on {name} is missing parts (an older scene). Run Close the Deal > Lanes > Add Item HUD To Open Scene.", this);
                enabled = false;
                return;
            }

            _promptHome = _prompt.anchoredPosition;
            _handsHome = _hands.anchoredPosition;
            _loadWidth = ((RectTransform)_handsLoadFill.parent).rect.width;
            _promptCapWidth = ((RectTransform)_promptKeyCap.transform).rect.width;
            _labelAfterKey = _promptLabel.rectTransform.anchoredPosition.x;
            _promptFade.Snap(0f);
            _promptRise.Snap(0f);
            _handsFade.Snap(0f);
            _handsRise.Snap(0f);
            Apply();
        }

        void OnEnable()
        {
            ItemCarrier.LocalChanged += Bind;
            ItemCarrier.UsingPadChanged += OnDeviceChanged;
            UiGlyphs.Changed += RefreshDevice;
            Bind(ItemCarrier.Local);
            LabelKeys();
        }

        void OnDisable()
        {
            ItemCarrier.LocalChanged -= Bind;
            ItemCarrier.UsingPadChanged -= OnDeviceChanged;
            UiGlyphs.Changed -= RefreshDevice;
            Bind(null);
            UiMotion.Sleep(this);
        }

        void Bind(ItemCarrier carrier)
        {
            if (_carrier != null)
            {
                _carrier.PromptChanged -= OnPromptChanged;
                _carrier.HeldChanged -= OnHeldChanged;
                _carrier.Denied -= OnDenied;
            }

            _carrier = carrier;
            _refusalLeft = 0f;
            OnPromptChanged(PromptKind.None, null);
            OnHeldChanged(null);
            if (_carrier == null)
                return;

            _carrier.PromptChanged += OnPromptChanged;
            _carrier.HeldChanged += OnHeldChanged;
            _carrier.Denied += OnDenied;
            OnHeldChanged(_carrier.Held);

            // Coming back (the menu closed, a player spawned): show what is true at once, not as news.
            _handsFade.Snap(_handsFade.To);
            _handsRise.Snap(_handsRise.To);
            Apply();
        }

        void OnPromptChanged(PromptKind kind, ItemDefinition item)
        {
            _lookKind = kind;
            _lookText = kind switch
            {
                PromptKind.PickUp => "Pick up " + item.DisplayName,
                PromptKind.TooHeavy => item.DisplayName + " · Too heavy",
                _ => ""
            };

            if (_refusalLeft <= 0f)
                ShowLook();
        }

        void ShowLook()
        {
            if (_lookKind == PromptKind.None)
            {
                ShowPrompt(false);
                return;
            }

            bool liftable = _lookKind == PromptKind.PickUp;
            DrawPrompt(_lookText, liftable, liftable ? _tokens.Hair : _tokens.Amber, liftable ? _tokens.Navy : _tokens.Ink2);
            if (!_shown)
                UiSound.Play(UiSoundEvent.Focus);
            ShowPrompt(true);
        }

        void OnDeviceChanged(bool pad) => RefreshDevice();

        void RefreshDevice()
        {
            LabelKeys();
            if (_refusalLeft > 0f)
                DrawPrompt(RefusalText(_refusal), false, _tokens.Amber, _tokens.Navy);
            else if (_shown)
                ShowLook();
        }

        void LabelKeys()
        {
            SetKey(_throwKey, ItemAction.Throw);
            SetKey(_dropKey, ItemAction.Drop);
            SetKey(_useKey, ItemAction.Use);
        }

        const float KeyCapMinWidth = 48f;
        const float KeyCapPadding = 12f;
        const float KeyLabelGap = 10f;

        static void SetKey(TMP_Text text, ItemAction action)
        {
            if (text == null)
                return;

            string name = ItemCarrier.KeyName(action);
            text.text = name;
            float width = Mathf.Max(KeyCapMinWidth, text.GetPreferredValues(name).x + KeyCapPadding);
            Transform hint = text.transform.parent;
            Resize(text.rectTransform, width);
            Resize(hint.Find("KeyCap") as RectTransform, width);
            if (hint.Find("Label") is RectTransform label)
                label.anchoredPosition = new Vector2(width + KeyLabelGap, label.anchoredPosition.y);
        }

        static void Resize(RectTransform rect, float width)
        {
            if (rect != null)
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        void OnDenied(DenyReason reason)
        {
            _refusal = reason;
            string text = RefusalText(reason);
            if (text.Length == 0)
                return;

            UiSound.Play(reason == DenyReason.HandsFull ? UiSoundEvent.HandsFull : UiSoundEvent.Error);
            DrawPrompt(text, false, _tokens.Amber, _tokens.Navy);
            ShowPrompt(true);
            _refusalLeft = _refusalSeconds;
            if (!UiMotion.Reduced)
            {
                _shake.Value = _tokens.ErrorShakeDistance;
                _shake.Velocity = 0f;
                _shake.Target = 0f;
            }

            UiMotion.Wake(this);
        }

        /// <summary>The words for each refusal (ITEMS.md, "Refusal wording"). Empty: say nothing.</summary>
        public static string RefusalText(DenyReason reason) => reason switch
        {
            DenyReason.Gone => "Someone else got it",
            DenyReason.HandsFull => ItemCarrier.UsingPad ? "Hands full · D-pad down to drop" : "Hands full · G to drop",
            DenyReason.TooHeavy => "Too heavy to lift",
            DenyReason.TooFar => "Too far away",
            DenyReason.CannotUseHere => "No room to put it down here",
            _ => ""
        };

        void OnHeldChanged(Pickup held)
        {
            bool full = held != null;
            if (full)
            {
                ItemDefinition item = held.Definition;
                _handsName.text = item.DisplayName;
                bool usable = !string.IsNullOrEmpty(item.UseVerb);
                _useHint.SetActive(usable);
                if (usable)
                    _useLabel.text = item.UseVerb;
                SetLoad(_carrier != null ? _carrier.Load : 0f);
            }

            ShowHands(full);
        }

        void SetLoad(float load)
        {
            Vector2 size = _handsLoadFill.sizeDelta;
            size.x = Mathf.Max(2f, _loadWidth * Mathf.Clamp01(load));
            _handsLoadFill.sizeDelta = size;
        }

        void DrawPrompt(string text, bool showKey, Color edge, Color textColour)
        {
            _promptKeyCap.SetActive(showKey);
            _promptKey.gameObject.SetActive(showKey);
            _promptKey.text = ItemCarrier.KeyName(ItemAction.Interact);
            _promptLabel.text = text;
            _promptLabel.color = textColour;
            _promptEdge.color = edge;

            // The plate fits its words: key cap, gap, label, padding either side.
            RectTransform label = _promptLabel.rectTransform;
            float keyRoom = 0f;
            if (showKey)
            {
                float capWidth = Mathf.Max(_promptCapWidth, _promptKey.preferredWidth + KeyCapPadding);
                Resize((RectTransform)_promptKeyCap.transform, capWidth);
                Resize(_promptKey.rectTransform, capWidth);
                keyRoom = _labelAfterKey + capWidth - _promptCapWidth;
            }
            Vector2 labelPosition = label.anchoredPosition;
            labelPosition.x = keyRoom;
            label.anchoredPosition = labelPosition;
            float width = _promptLabel.GetPreferredValues(text).x;
            label.sizeDelta = new Vector2(width, label.sizeDelta.y);
            _promptPlate.sizeDelta = new Vector2(keyRoom + width + _promptPadding * 2f, _promptPlate.sizeDelta.y);
        }

        void ShowPrompt(bool show)
        {
            if (show == _shown)
                return;

            _shown = show;
            Fade(ref _promptFade, ref _promptRise, show, _tokens.HoverIn, _tokens.HoverOut, _tokens.RevealRise);
            UiMotion.Wake(this);
        }

        void ShowHands(bool show)
        {
            bool visible = _handsFade.To > 0.5f;
            if (show == visible)
                return;

            Fade(ref _handsFade, ref _handsRise, show, _tokens.PanelIn, _tokens.PanelOut, _tokens.PanelSlideIn);
            UiMotion.Wake(this);
        }

        void Fade(ref UiTween fade, ref UiTween rise, bool show, float inSeconds, float outSeconds, float distance)
        {
            bool reduced = UiMotion.Reduced;
            float seconds = reduced ? _tokens.ReducedFade : show ? inSeconds : outSeconds;
            Vector4 ease = show ? _tokens.EaseOut : _tokens.EaseIn;
            fade.Start(fade.Value, show ? 1f : 0f, seconds, ease);
            float travel = reduced ? 0f : distance;
            rise.Start(rise.Value, show ? 0f : -travel * 0.5f, seconds, ease);
            if (show && !reduced && fade.Value <= 0.01f)
                rise.Start(-travel, 0f, seconds, ease);
        }

        public bool Tick(float dt)
        {
            bool moving = false;
            _promptFade.Step(dt);
            _promptRise.Step(dt);
            _handsFade.Step(dt);
            _handsRise.Step(dt);
            moving |= _promptFade.Running || _promptRise.Running || _handsFade.Running || _handsRise.Running;
            moving |= _shake.Step(dt, _tokens.ErrorShake);

            if (_refusalLeft > 0f)
            {
                _refusalLeft -= dt;
                if (_refusalLeft <= 0f)
                    ShowLook();
                moving = true;
            }

            Apply();
            return moving;
        }

        void Apply()
        {
            _promptGroup.alpha = _promptFade.Value;
            _prompt.anchoredPosition = _promptHome + new Vector2(_shake.Value, _promptRise.Value);
            _handsGroup.alpha = _handsFade.Value;
            _hands.anchoredPosition = _handsHome + new Vector2(0f, _handsRise.Value);
        }
    }
}
