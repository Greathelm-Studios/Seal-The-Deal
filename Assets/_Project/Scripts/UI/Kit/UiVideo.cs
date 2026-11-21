using System;
using System.Collections.Generic;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The player's video settings, kept on this PC and applied at launch: display mode, resolution, vertical sync,
    /// frame rate limit and graphics quality. Uses Unity's own Screen and QualitySettings; nothing a designer authored
    /// is written. In the Editor the window belongs to the Editor, so display mode and resolution are saved but not
    /// applied there (they apply in a build).
    /// </summary>
    public static class UiVideo
    {
        const string ModeKey = "video.displayMode";
        const string WidthKey = "video.width";
        const string HeightKey = "video.height";
        const string VSyncKey = "video.vsync";
        const string CapKey = "video.frameCap";
        const string QualityKey = "video.quality";

        public static readonly string[] ModeNames = { "Full screen", "Borderless window", "Windowed" };
        static readonly FullScreenMode[] Modes = { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
        public static readonly string[] CapNames = { "None", "60", "120", "144", "240" };
        static readonly int[] Caps = { -1, 60, 120, 144, 240 };

        public const int DefaultMode = 1;
        public const bool DefaultVSync = true;
        public const int DefaultCap = 0;

        static int _defaultQuality = -1;

        public static int Mode
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, DefaultMode), 0, Modes.Length - 1);
            set { PlayerPrefs.SetInt(ModeKey, Mathf.Clamp(value, 0, Modes.Length - 1)); ApplyDisplay(); }
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt(VSyncKey, DefaultVSync ? 1 : 0) != 0;
            set { PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0); ApplyTiming(); }
        }

        public static int FrameCap
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(CapKey, DefaultCap), 0, Caps.Length - 1);
            set { PlayerPrefs.SetInt(CapKey, Mathf.Clamp(value, 0, Caps.Length - 1)); ApplyTiming(); }
        }

        /// <summary>Index into QualitySettings.names. Defaults to the level the project ships with.</summary>
        public static int Quality
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, DefaultQuality), 0, QualitySettings.names.Length - 1);
            set { PlayerPrefs.SetInt(QualityKey, Mathf.Clamp(value, 0, QualitySettings.names.Length - 1)); ApplyQuality(); ApplyTiming(); }
        }

        public static int DefaultQuality
        {
            get
            {
                if (_defaultQuality < 0)
                    _defaultQuality = QualitySettings.GetQualityLevel();
                return _defaultQuality;
            }
        }

        /// <summary>
        /// The resolution saved (never larger than this monitor: a size saved on a bigger one is clamped to it), or the
        /// desktop's when none is. The desktop's comes from the display itself, not Screen.currentResolution, which in
        /// exclusive full screen is the game's own mode.
        /// </summary>
        public static Vector2Int Resolution
        {
            get
            {
                Vector2Int desktop = Desktop;
                var saved = new Vector2Int(PlayerPrefs.GetInt(WidthKey, desktop.x), PlayerPrefs.GetInt(HeightKey, desktop.y));
                return saved.x > desktop.x || saved.y > desktop.y ? desktop : saved; // too big for this monitor: its own size
            }
            set
            {
                SaveResolution(value);
                ApplyDisplay();
            }
        }

        internal static void SaveResolution(Vector2Int size)
        {
            PlayerPrefs.SetInt(WidthKey, size.x);
            PlayerPrefs.SetInt(HeightKey, size.y);
        }

        public static bool HasSavedResolution => PlayerPrefs.HasKey(WidthKey);

        /// <summary>The size of the monitor the game's window is on (its desktop mode), whatever the game is running at.</summary>
        static Vector2Int Desktop
        {
            get
            {
                DisplayInfo info = Screen.mainWindowDisplayInfo;
                if (info.width > 0 && info.height > 0)
                    return new Vector2Int(info.width, info.height);
                return new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            }
        }

        /// <summary>Puts the screen back to exactly this size, keeping or forgetting the saved choice as it was before.</summary>
        public static void Restore(Vector2Int size, bool hadSaved)
        {
            if (hadSaved)
            {
                Resolution = size;
                return;
            }

            PlayerPrefs.DeleteKey(WidthKey);
            PlayerPrefs.DeleteKey(HeightKey);
            ApplySize(size);
        }

        /// <summary>Back to following the desktop's resolution.</summary>
        public static void ForgetResolution()
        {
            PlayerPrefs.DeleteKey(WidthKey);
            PlayerPrefs.DeleteKey(HeightKey);
            ApplyDisplay();
        }

        public static bool AtDefaults =>
            Mode == DefaultMode && VSync == DefaultVSync && FrameCap == DefaultCap && Quality == DefaultQuality
            && !PlayerPrefs.HasKey(WidthKey);

        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(ModeKey);
            PlayerPrefs.DeleteKey(WidthKey);
            PlayerPrefs.DeleteKey(HeightKey);
            PlayerPrefs.DeleteKey(VSyncKey);
            PlayerPrefs.DeleteKey(CapKey);
            PlayerPrefs.DeleteKey(QualityKey);
            ApplyAll();
        }

        /// <summary>The monitor's sizes, each once (the highest refresh of each), smallest first. At least the current one.</summary>
        public static List<Vector2Int> Sizes()
        {
            var sizes = new List<Vector2Int>();
            foreach (Resolution r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!sizes.Contains(size))
                    sizes.Add(size);
            }

            Vector2Int current = Resolution;
            if (!sizes.Contains(current))
                sizes.Add(current);
            sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return sizes;
        }

        public static string SizeName(Vector2Int size) => size.x + " × " + size.y;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplyAll()
        {
            ApplyQuality();
            ApplyTiming();
            ApplyDisplay();
        }

        static void ApplyQuality() => QualitySettings.SetQualityLevel(Quality, true);

        /// <summary>Quality levels carry their own vsync, so timing is applied after quality.</summary>
        static void ApplyTiming()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : Caps[FrameCap];
        }

        static void ApplyDisplay() => ApplySize(Resolution);

        internal static void ApplySize(Vector2Int size)
        {
            if (!Application.isEditor)
                Screen.SetResolution(size.x, size.y, Modes[Mode]);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _defaultQuality = QualitySettings.GetQualityLevel(); // The project's own level, before any saved one.
#if UNITY_EDITOR
            // In the Editor these are the project's own settings: put them back when Play Mode ends.
            int vsync = QualitySettings.vSyncCount;
            int level = _defaultQuality;
            Application.quitting += () =>
            {
                QualitySettings.SetQualityLevel(level, true);
                QualitySettings.vSyncCount = vsync;
                Application.targetFrameRate = -1;
            };
#endif
        }
    }
}
