using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Settings › Accessibility: Reduce motion, the crosshair's style, colour and size (the right column shows it live
    /// over sun, sky and a dark room), and Show network stats. Values live in UiSettings.
    /// </summary>
    [AddComponentMenu("Close the Deal/UI/Settings Page - Accessibility")]
    public sealed class AccessibilityPage : UiSettingsPage
    {
        [SerializeField] UiToggle _reducedMotion;
        [SerializeField] UiStepper _crosshairStyle;
        [SerializeField] UiSwatches _crosshairColour;
        [SerializeField] UiSlider _crosshairSize;
        [SerializeField] UiToggle _networkStats;

        static readonly string[] StyleNames = { "Dot", "Cross", "Cross + Dot" };
        static readonly string[] ColourNames = { "Paper White", "Cyan", "Electric Blue" };

        bool _ready;

        void Prepare()
        {
            if (_ready)
                return;
            _ready = true;
            _crosshairStyle.SetOptions(StyleNames, (int)UiSettings.CrosshairStyle);
            _crosshairColour.SetSwatches(new[] { _tokens.Paper, _tokens.Cyan, _tokens.Electric }, ColourNames, (int)UiSettings.CrosshairColour);
            _crosshairSize.minValue = UiSettings.CrosshairSizeMin / UiSettings.CrosshairSizeStep;
            _crosshairSize.maxValue = UiSettings.CrosshairSizeMax / UiSettings.CrosshairSizeStep;
            _crosshairSize.Configure(UiSettings.CrosshairSizeStep, "%");
        }

        void OnEnable()
        {
            Prepare();
            _reducedMotion.Changed += OnReducedMotion;
            _crosshairStyle.Changed += OnStyle;
            _crosshairColour.Changed += OnColour;
            _crosshairSize.Changed += OnSize;
            _networkStats.Changed += OnNetworkStats;
        }

        void OnDisable()
        {
            _reducedMotion.Changed -= OnReducedMotion;
            _crosshairStyle.Changed -= OnStyle;
            _crosshairColour.Changed -= OnColour;
            _crosshairSize.Changed -= OnSize;
            _networkStats.Changed -= OnNetworkStats;
        }

        public override void ShowSaved()
        {
            Prepare();
            _reducedMotion.SetWithoutNotify(UiSettings.ReducedMotion);
            _crosshairStyle.SetWithoutNotify((int)UiSettings.CrosshairStyle);
            _crosshairColour.SetWithoutNotify((int)UiSettings.CrosshairColour);
            _crosshairSize.SetDisplayWithoutNotify(UiSettings.CrosshairSize);
            _networkStats.SetWithoutNotify(UiSettings.NetworkStats);
        }

        public override bool AtDefaults => UiSettings.AtDefaults;

        public override void ResetToDefaults()
        {
            UiSettings.ResetToDefaults();
            ShowSaved();
        }

        public override void DescribeChanges(System.Collections.Generic.List<string> lines)
        {
            if (UiSettings.ReducedMotion != UiSettings.DefaultReducedMotion)
                lines.Add(ChangeLine("Reduce motion", OnOff(UiSettings.ReducedMotion), OnOff(UiSettings.DefaultReducedMotion)));
            if (UiSettings.CrosshairStyle != UiSettings.DefaultCrosshairStyle)
                lines.Add(ChangeLine("Crosshair style", StyleNames[(int)UiSettings.CrosshairStyle], StyleNames[(int)UiSettings.DefaultCrosshairStyle]));
            if (UiSettings.CrosshairColour != UiSettings.DefaultCrosshairColour)
                lines.Add(ChangeLine("Crosshair colour", ColourNames[(int)UiSettings.CrosshairColour], ColourNames[(int)UiSettings.DefaultCrosshairColour]));
            if (UiSettings.CrosshairSize != UiSettings.DefaultCrosshairSize)
                lines.Add(ChangeLine("Crosshair size", UiSettings.CrosshairSize + "%", UiSettings.DefaultCrosshairSize + "%"));
            if (UiSettings.NetworkStats != UiSettings.DefaultNetworkStats)
                lines.Add(ChangeLine("Show network stats", OnOff(UiSettings.NetworkStats), OnOff(UiSettings.DefaultNetworkStats)));
        }

        static string OnOff(bool on) => on ? "On" : "Off";

        void OnReducedMotion(bool on) { UiSettings.ReducedMotion = on; RaiseSaved(); }

        void OnStyle(int index) { UiSettings.CrosshairStyle = (CrosshairStyle)index; RaiseSaved(); }

        void OnColour(int index) { UiSettings.CrosshairColour = (CrosshairColour)index; RaiseSaved(); }

        void OnSize(int percent) { UiSettings.CrosshairSize = percent; RaiseSaved(); }

        void OnNetworkStats(bool on) { UiSettings.NetworkStats = on; RaiseSaved(); }
    }
}
