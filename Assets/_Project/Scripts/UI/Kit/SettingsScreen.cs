using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Settings, Accessibility tab: Reduce motion, the crosshair's style, colour and size (with a live preview over
    /// sun, sky and a dark room), and Show network stats. Every change is saved on this PC at once and holds after a
    /// relaunch; Reset to defaults puts them all back. Only rows whose game-side value exists are here; the
    /// rest wait for their systems (docs/systems/UI_KIT.md "Settings").
    /// </summary>
    [AddComponentMenu("Close the Deal/UI/Settings Screen")]
    public sealed class SettingsScreen : UiScreen
    {
        [Header("Rows")]
        [SerializeField] UiToggle _reducedMotion;
        [SerializeField] UiStepper _crosshairStyle;
        [SerializeField] UiSwatches _crosshairColour;
        [SerializeField] UiSlider _crosshairSize;
        [SerializeField] UiToggle _networkStats;

        [Header("Footer")]
        [SerializeField] UiButton _reset;
        [SerializeField] UiButton _back;
        [Tooltip("Says what was saved, or why a press was refused.")]
        [SerializeField] UiStatusLine _status;
        [Tooltip("The focused row's one-line explanation.")]
        [SerializeField] TMP_Text _help;

        [Header("Tabs")]
        [Tooltip("The open tab's button. The other tabs join it as their settings become real.")]
        [SerializeField] UiButton _tab;

        static readonly string[] StyleNames = { "Dot", "Cross", "Cross + Dot" };
        static readonly string[] ColourNames = { "Paper White", "Cyan", "Electric Blue" };

        const string SavedLine = "Saved on this PC.";
        const string KeptLine = "Changes are saved on this PC as you make them.";

        protected override void Awake()
        {
            base.Awake();
            _firstFocus = _reducedMotion;
            _back.ConfirmSound = UiSoundEvent.Back;
            _reset.RefusalReason = "Everything is already at its default.";
            _crosshairStyle.SetOptions(StyleNames, (int)UiSettings.CrosshairStyle);
            _crosshairColour.SetSwatches(new[] { _tokens.Paper, _tokens.Cyan, _tokens.Electric }, ColourNames, (int)UiSettings.CrosshairColour);
            _crosshairSize.minValue = UiSettings.CrosshairSizeMin / UiSettings.CrosshairSizeStep;
            _crosshairSize.maxValue = UiSettings.CrosshairSizeMax / UiSettings.CrosshairSizeStep;
            _crosshairSize.Configure(UiSettings.CrosshairSizeStep, "%");
            if (_tab != null)
                _tab.Fx.Active = true;
        }

        void OnEnable()
        {
            _reducedMotion.Changed += OnReducedMotion;
            _crosshairStyle.Changed += OnStyle;
            _crosshairColour.Changed += OnColour;
            _crosshairSize.Changed += OnSize;
            _networkStats.Changed += OnNetworkStats;
            _reset.Clicked += OnReset;
            _back.Clicked += OnBack;
            UiControlEvents.Focused += OnFocused;
            UiControlEvents.Refused += OnRefused;
        }

        protected override void OnDisable()
        {
            _reducedMotion.Changed -= OnReducedMotion;
            _crosshairStyle.Changed -= OnStyle;
            _crosshairColour.Changed -= OnColour;
            _crosshairSize.Changed -= OnSize;
            _networkStats.Changed -= OnNetworkStats;
            _reset.Clicked -= OnReset;
            _back.Clicked -= OnBack;
            UiControlEvents.Focused -= OnFocused;
            UiControlEvents.Refused -= OnRefused;
            base.OnDisable();
        }

        protected override void OnShowing()
        {
            ShowSaved();
            _status.Set(KeptLine, UiStatusKind.Info);
        }

        protected override void OnHiding() => UiSettings.Flush();

        /// <summary>Reads every saved value into the rows, without sounds or events.</summary>
        void ShowSaved()
        {
            _reducedMotion.SetWithoutNotify(UiSettings.ReducedMotion);
            _crosshairStyle.SetWithoutNotify((int)UiSettings.CrosshairStyle);
            _crosshairColour.SetWithoutNotify((int)UiSettings.CrosshairColour);
            _crosshairSize.SetDisplayWithoutNotify(UiSettings.CrosshairSize);
            _networkStats.SetWithoutNotify(UiSettings.NetworkStats);
            LinkFooter();
        }

        /// <summary>Reset is greyed out at the defaults; down from the last row then goes straight to Back.</summary>
        void LinkFooter()
        {
            _reset.interactable = !UiSettings.AtDefaults;
            Selectable below = _reset.interactable ? _reset : _back;
            Navigation last = _networkStats.navigation;
            last.selectOnDown = below;
            _networkStats.navigation = last;
            Navigation back = _back.navigation;
            back.selectOnLeft = _reset.interactable ? _reset : null;
            _back.navigation = back;
        }

        void OnReducedMotion(bool on) => Saved(() => UiSettings.ReducedMotion = on);

        void OnStyle(int index) => Saved(() => UiSettings.CrosshairStyle = (CrosshairStyle)index);

        void OnColour(int index) => Saved(() => UiSettings.CrosshairColour = (CrosshairColour)index);

        void OnSize(int percent) => Saved(() => UiSettings.CrosshairSize = percent);

        void OnNetworkStats(bool on) => Saved(() => UiSettings.NetworkStats = on);

        void Saved(System.Action write)
        {
            write();
            LinkFooter();
            _status.Set(SavedLine, UiStatusKind.Info);
        }

        void OnReset()
        {
            UiSettings.ResetToDefaults();
            ShowSaved();
            _status.Set("Defaults restored and saved on this PC.", UiStatusKind.Info);
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(_back.gameObject);
        }

        void OnBack() => Stack?.Pop(false);

        void OnFocused(Selectable control, string description)
        {
            if (_help != null && control != null && control.transform.IsChildOf(transform))
                _help.text = description;
        }

        void OnRefused(Selectable control, string reason)
        {
            if (control != null && control.transform.IsChildOf(transform) && !string.IsNullOrEmpty(reason))
                _status.Set(reason, UiStatusKind.Refusal);
        }
    }
}
