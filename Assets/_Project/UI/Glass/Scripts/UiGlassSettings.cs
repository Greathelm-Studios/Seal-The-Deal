using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>Persisted local UI glass quality setting (Auto, Blur, NoBlur).</summary>
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

        // Default _UiGlassBlurOn to 0 until a blur pass runs so scenes without the feature use the fallback tint.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            _loaded = false;
            Load();
            Shader.SetGlobalFloat(BlurOn, 0f);
        }
    }
}
