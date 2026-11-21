using System;
using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>The crosshair's shape. Order is saved by number: add new shapes at the end.</summary>
    public enum CrosshairStyle
    {
        Dot = 0,
        Cross = 1,
        CrossAndDot = 2
    }

    /// <summary>The crosshair's colour, from the palette only (never amber: that means the rival).</summary>
    public enum CrosshairColour
    {
        PaperWhite = 0,
        Cyan = 1,
        ElectricBlue = 2
    }

    /// <summary>
    /// The player's own preferences, kept on this PC between launches (PlayerPrefs, saved on every change).
    /// Holds only settings the UI owns and the game already honours; a row whose game-side value does not exist
    /// yet is left out rather than faked (docs/systems/UI_KIT.md "Settings"). Read on demand, cached after the
    /// first read; Changed fires once per change so readers redraw on events, never by polling.
    /// </summary>
    public static class UiSettings
    {
        /// <summary>The key the title lane's transitions read too (its UiTransition, on that lane's branch): one switch for every animation.</summary>
        public const string ReducedMotionKey = "ui.reducedMotion";
        public const string CrosshairStyleKey = "ui.crosshair.style";
        public const string CrosshairColourKey = "ui.crosshair.colour";
        public const string CrosshairSizeKey = "ui.crosshair.size";
        public const string NetworkStatsKey = "ui.networkStats";

        public const int CrosshairSizeMin = 50;
        public const int CrosshairSizeMax = 200;
        public const int CrosshairSizeStep = 10;

        public const bool DefaultReducedMotion = false;
        public const CrosshairStyle DefaultCrosshairStyle = CrosshairStyle.CrossAndDot;
        public const CrosshairColour DefaultCrosshairColour = CrosshairColour.PaperWhite;
        public const int DefaultCrosshairSize = 100;
        /// <summary>Fresh player profiles show diagnostics only in the Editor and development builds.</summary>
        public static bool DefaultNetworkStats => Debug.isDebugBuild;

        /// <summary>Fires after any setting changes (already saved).</summary>
        public static event Action Changed;

        static bool _loaded;
        static bool _reducedMotion;
        static CrosshairStyle _style;
        static CrosshairColour _colour;
        static int _size;
        static bool _networkStats;
        static bool _dirty;

        public static bool ReducedMotion
        {
            get { Load(); return _reducedMotion; }
            set { Load(); if (_reducedMotion == value) return; _reducedMotion = value; SaveAndNotify(); }
        }

        public static CrosshairStyle CrosshairStyle
        {
            get { Load(); return _style; }
            set { Load(); if (_style == value) return; _style = value; SaveAndNotify(); }
        }

        public static CrosshairColour CrosshairColour
        {
            get { Load(); return _colour; }
            set { Load(); if (_colour == value) return; _colour = value; SaveAndNotify(); }
        }

        /// <summary>Crosshair size in percent, 50 to 200 in steps of 10. Values outside are clamped and snapped.</summary>
        public static int CrosshairSize
        {
            get { Load(); return _size; }
            set { Load(); int v = SnapSize(value); if (_size == v) return; _size = v; SaveAndNotify(); }
        }

        public static bool NetworkStats
        {
            get { Load(); return _networkStats; }
            set { Load(); if (_networkStats == value) return; _networkStats = value; SaveAndNotify(); }
        }

        /// <summary>True when every value equals its default, so "Reset to defaults" has nothing to do.</summary>
        public static bool AtDefaults
        {
            get
            {
                Load();
                return _reducedMotion == DefaultReducedMotion && _style == DefaultCrosshairStyle && _colour == DefaultCrosshairColour
                    && _size == DefaultCrosshairSize && _networkStats == DefaultNetworkStats;
            }
        }

        public static void ResetToDefaults()
        {
            Load();
            _reducedMotion = DefaultReducedMotion;
            _style = DefaultCrosshairStyle;
            _colour = DefaultCrosshairColour;
            _size = DefaultCrosshairSize;
            _networkStats = DefaultNetworkStats;
            SaveAndNotify();
        }

        /// <summary>Forgets the cache so the next read comes from disk. For the relaunch check and other lanes writing the shared key.</summary>
        public static void Reload()
        {
            _loaded = false;
            Load();
            Changed?.Invoke();
        }

        public static int SnapSize(int percent)
        {
            int clamped = Mathf.Clamp(percent, CrosshairSizeMin, CrosshairSizeMax);
            return Mathf.RoundToInt(clamped / (float)CrosshairSizeStep) * CrosshairSizeStep;
        }

        static void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            _reducedMotion = PlayerPrefs.GetInt(ReducedMotionKey, DefaultReducedMotion ? 1 : 0) != 0;
            _style = Known(PlayerPrefs.GetInt(CrosshairStyleKey, (int)DefaultCrosshairStyle), DefaultCrosshairStyle);
            _colour = Known(PlayerPrefs.GetInt(CrosshairColourKey, (int)DefaultCrosshairColour), DefaultCrosshairColour);
            _size = SnapSize(PlayerPrefs.GetInt(CrosshairSizeKey, DefaultCrosshairSize));
            _networkStats = PlayerPrefs.GetInt(NetworkStatsKey, DefaultNetworkStats ? 1 : 0) != 0;
        }

        /// <summary>A saved number that is no longer a known choice (a build without it) falls back to the default.</summary>
        static T Known<T>(int saved, T fallback) where T : struct, Enum
        {
            return Enum.IsDefined(typeof(T), saved) ? (T)Enum.ToObject(typeof(T), saved) : fallback;
        }

        /// <summary>Writes the settings to disk. Called when the settings screen closes and when the game quits.</summary>
        public static void Flush()
        {
            if (!_dirty)
                return;
            _dirty = false;
            PlayerPrefs.Save();
        }

        static void SaveAndNotify()
        {
            PlayerPrefs.SetInt(ReducedMotionKey, _reducedMotion ? 1 : 0);
            PlayerPrefs.SetInt(CrosshairStyleKey, (int)_style);
            PlayerPrefs.SetInt(CrosshairColourKey, (int)_colour);
            PlayerPrefs.SetInt(CrosshairSizeKey, _size);
            PlayerPrefs.SetInt(NetworkStatsKey, _networkStats ? 1 : 0);
            _dirty = true; // On disk at the next Flush: dragging a slider changes it many times a second.
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _loaded = false;
            _dirty = false;
            Changed = null;
            Application.quitting -= Flush;
            Application.quitting += Flush;
        }
    }
}
