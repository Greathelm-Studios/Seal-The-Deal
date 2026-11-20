using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Settings › Video: display mode, resolution (a new one asks to be kept and goes back by itself after 10 seconds),
    /// vertical sync, frame rate limit (only with vertical sync off), graphics quality and menu glass (lane 7's blur
    /// or its plain no-blur tint, UiGlassSettings). Values live in UiVideo and UiGlassSettings.
    /// The right column says what the screen is doing now.
    /// </summary>
    [AddComponentMenu("Seal the Deal/UI/Settings Page - Video")]
    public sealed class VideoPage : UiSettingsPage
    {
        [SerializeField] UiStepper _mode;
        [SerializeField] UiStepper _resolution;
        [SerializeField] UiToggle _vsync;
        [SerializeField] UiStepper _frameCap;
        [SerializeField] UiStepper _quality;
        [Tooltip("Menu glass: Blur (the default) or No blur.")]
        [SerializeField] UiStepper _glass;
        [Tooltip("The live readout in the right column: the size and refresh the screen is running at.")]
        [SerializeField] TMP_Text _readout;
        [Tooltip("The yes / no dialog the resolution check uses.")]
        [SerializeField] UiConfirmDialog _dialog;
        [Tooltip("Seconds before a new resolution goes back by itself unless kept.")]
        [SerializeField] float _revertSeconds = 10f;

        List<Vector2Int> _sizes = new();
        Vector2Int _previousSize;
        Vector2Int _previousSaved;
        bool _hadSavedSize;
        float _countdown = -1f;
        int _shownSeconds = -1;
        string _lastReadout;

        void OnEnable()
        {
            _mode.Changed += OnMode;
            _resolution.Changed += OnResolution;
            _vsync.Changed += OnVSync;
            _frameCap.Changed += OnCap;
            _quality.Changed += OnQuality;
            if (_glass != null)
                _glass.Changed += OnGlass;
        }

        void OnDisable()
        {
            _mode.Changed -= OnMode;
            _resolution.Changed -= OnResolution;
            _vsync.Changed -= OnVSync;
            _frameCap.Changed -= OnCap;
            _quality.Changed -= OnQuality;
            if (_glass != null)
                _glass.Changed -= OnGlass;
        }

        public override void ShowSaved()
        {
            _sizes = UiVideo.Sizes();
            var names = new string[_sizes.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = UiVideo.SizeName(_sizes[i]);
            _resolution.SetOptions(names, Mathf.Max(0, _sizes.IndexOf(UiVideo.Resolution)));
            _mode.SetOptions(UiVideo.ModeNames, UiVideo.Mode);
            _vsync.SetWithoutNotify(UiVideo.VSync);
            _frameCap.SetOptions(UiVideo.CapNames, UiVideo.FrameCap);
            _quality.SetOptions(QualitySettings.names, UiVideo.Quality);
            _quality.transform.parent.gameObject.SetActive(QualitySettings.names.Length > 1); // one level on this platform: nothing to choose
            if (_glass != null)
                _glass.SetOptions(GlassNames, GlassIndex);
            ShowCapUse();
        }

        // Lane 7's three values, in its order: Auto (blur where the renderer has it), Blur, No blur.
        static readonly string[] GlassNames = { "Auto", "Blur", "No blur" };

        static int GlassIndex => Mathf.Clamp((int)UiGlassSettings.Current, 0, GlassNames.Length - 1);

        void OnGlass(int index)
        {
            UiGlassSettings.Current = (UiGlassSettings.Quality)index;
            RaiseSaved();
        }

        public override bool AtDefaults => UiVideo.AtDefaults && GlassIndex == 0;

        public override void DescribeChanges(List<string> lines)
        {
            if (UiVideo.Mode != UiVideo.DefaultMode)
                lines.Add(ChangeLine("Display mode", UiVideo.ModeNames[UiVideo.Mode], UiVideo.ModeNames[UiVideo.DefaultMode]));
            if (UiVideo.HasSavedResolution)
                lines.Add(ChangeLine("Resolution", UiVideo.SizeName(UiVideo.Resolution), "the desktop's"));
            if (UiVideo.VSync != UiVideo.DefaultVSync)
                lines.Add(ChangeLine("Vertical sync", UiVideo.VSync ? "On" : "Off", UiVideo.DefaultVSync ? "On" : "Off"));
            if (UiVideo.FrameCap != UiVideo.DefaultCap)
                lines.Add(ChangeLine("Frame rate limit", UiVideo.CapNames[UiVideo.FrameCap], UiVideo.CapNames[UiVideo.DefaultCap]));
            string[] levels = QualitySettings.names;
            if (UiVideo.Quality != UiVideo.DefaultQuality && UiVideo.Quality < levels.Length && UiVideo.DefaultQuality < levels.Length)
                lines.Add(ChangeLine("Graphics quality", levels[UiVideo.Quality], levels[UiVideo.DefaultQuality]));
            if (GlassIndex != 0)
                lines.Add(ChangeLine("Menu glass", GlassNames[GlassIndex], GlassNames[0]));
        }

        public override void ResetToDefaults()
        {
            UiVideo.ResetToDefaults();
            UiGlassSettings.Current = UiGlassSettings.Quality.Auto;
            ShowSaved();
        }

        public override void Tick(float dt)
        {
            DrawReadout();
            if (_countdown < 0f)
                return;
            _countdown -= dt;
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, _countdown));
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _dialog.SetBody("Reverting in " + seconds + ".");
            }

            if (_countdown <= 0f)
            {
                UiSound.Play(UiSoundEvent.Back);   // the time ran out: the change is undone, so it answers like Revert
                Revert();
            }
        }

        /// <summary>Leaving the tab or the screen with a resolution unconfirmed goes back to the old one (the dialog closes with the screen).</summary>
        public override void Hidden()
        {
            if (_countdown >= 0f)
                Revert(false);
        }

        void DrawReadout()
        {
            if (_readout == null)
                return;
            Resolution now = Screen.currentResolution;
            int width = Screen.width, height = Screen.height;
            int hz = Mathf.RoundToInt((float)now.refreshRateRatio.value);
            int quality = QualitySettings.GetQualityLevel();
            if (_lastReadout != null && width == _lastW && height == _lastH && hz == _lastHz && quality == _lastQuality)
                return;
            _lastQuality = quality;
            _lastW = width;
            _lastH = height;
            _lastHz = hz;
            _lastReadout = $"{width} × {height}\n{hz} Hz · {QualitySettings.names[quality]}";
            _readout.text = _lastReadout;
        }

        int _lastW, _lastH, _lastHz, _lastQuality;

        void OnMode(int index) { UiVideo.Mode = index; RaiseSaved(); }

        void OnVSync(bool on) { UiVideo.VSync = on; ShowCapUse(); RaiseSaved(); }

        void OnCap(int index) { UiVideo.FrameCap = index; RaiseSaved(); }

        void OnQuality(int index) { UiVideo.Quality = index; RaiseSaved(); }

        void ShowCapUse()
        {
            _frameCap.interactable = !UiVideo.VSync;
            _frameCap.RefusalReason = "Turn off vertical sync to set a frame rate limit.";
        }

        /// <summary>A new resolution applies at once and asks to be kept; it goes back by itself if nobody answers.</summary>
        void OnResolution(int index)
        {
            if (index < 0 || index >= _sizes.Count)
                return;
            _previousSize = new Vector2Int(Screen.width, Screen.height); // what the screen really was, to go back to
            _previousSaved = UiVideo.Resolution;
            _hadSavedSize = UiVideo.HasSavedResolution;
            UiVideo.Resolution = _sizes[index];
            UiScreen screen = GetComponentInParent<UiScreen>();
            if (_dialog == null || screen == null || screen.Stack == null)
            {
                RaiseSaved();
                return;
            }

            _countdown = _revertSeconds;
            _shownSeconds = -1;
            _dialog.Configure("Keep this resolution?", "Reverting in " + Mathf.CeilToInt(_revertSeconds) + ".", "Keep", "Revert", Keep, Revert);
            screen.Stack.Push(_dialog);
        }

        void Keep()
        {
            _countdown = -1f;
            _dialog.Stack?.Pop(false);
            RaiseSaved("Resolution kept and saved on this PC.");
        }

        void Revert() => Revert(true);

        void Revert(bool closeDialog)
        {
            if (_countdown < 0f)
                return;
            _countdown = -1f;
            UiVideo.Restore(_hadSavedSize ? _previousSaved : _previousSize, _hadSavedSize);
            _resolution.SetWithoutNotify(Mathf.Max(0, _sizes.IndexOf(UiVideo.Resolution)));
            UiScreen top = _dialog.Stack != null ? _dialog.Stack.Top : null;
            if (closeDialog && top == _dialog)
                _dialog.Stack.Pop(false);
            RaiseSaved("Back to " + UiVideo.SizeName(_previousSize) + ".");
        }
    }
}
