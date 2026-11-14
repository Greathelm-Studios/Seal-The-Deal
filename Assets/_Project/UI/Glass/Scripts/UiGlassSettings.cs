using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The player's choice for UI glass quality. Auto uses the blur when the renderer has it; NoBlur never runs
    /// the blur passes and the panes fall back to a denser plain tint (still passes contrast). Persisted.
    /// </summary>
    public static class UiGlassSettings
    {
        public enum Quality { Auto = 0, Blur = 1, NoBlur = 2 }

        const string Key = "ctd.ui.glassQuality";
        static readonly int BlurOn = Shader.PropertyToID("_UiGlassBlurOn");
        static Quality _quality;
        static bool _loaded;

        static void Load()
        {
            if (_loaded) return;
            _quality = (Quality)PlayerPrefs.GetInt(Key, 0);
            _loaded = true;
        }

        public static Quality Current
        {
            get { Load(); return _quality; }
            set
            {
                _loaded = true;
                _quality = value;
                PlayerPrefs.SetInt(Key, (int)value);
                if (value == Quality.NoBlur) Shader.SetGlobalFloat(BlurOn, 0f);
            }
        }

        /// <summary>True when the feature should run its passes this frame.</summary>
        public static bool BlurAllowed => Current != Quality.NoBlur;

        /// <summary>Panes read this: 0 until a blur pass actually ran, so a scene or renderer without the feature
        /// (or "No blur") gets the fallback tint by itself.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            _loaded = false;
            Load();
            Shader.SetGlobalFloat(BlurOn, 0f);
        }
    }
}
