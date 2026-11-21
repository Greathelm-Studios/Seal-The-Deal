using UnityEngine;
using UnityEngine.Audio;

namespace CloseTheDeal.UI
{
    /// <summary>Applies and persists per-bus mixer volumes (0..1 slider mapped to dB).</summary>
    public static class UiAudio
    {
        const string Key = "ctd.audio.";
        static readonly string[] Param = { "MasterVolume", "MusicVolume", "SfxVolume", "VoiceVolume", "UiVolume" };
        static readonly float[] Default = { 1f, 0.8f, 1f, 1f, 1f };

        /// <summary>The volume a bus starts at before the player has touched it, 0 to 1.</summary>
        public static float DefaultOf(AudioBus bus) => Default[(int)bus];

        public static float Get(AudioBus bus) => PlayerPrefs.GetFloat(Key + bus, Default[(int)bus]);

        public static void SetVolume(AudioBus bus, float slider01, bool save = true)
        {
            slider01 = Mathf.Clamp01(slider01);
            Apply(bus, slider01);
            if (save) { PlayerPrefs.SetFloat(Key + bus, slider01); unsaved = true; }
        }

        /// <summary>Flushes dirty volume preferences to PlayerPrefs.</summary>
        public static void Flush()
        {
            if (!unsaved) return;
            unsaved = false;
            PlayerPrefs.Save();
        }

        static bool unsaved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookSaving()
        {
            unsaved = false;
            Application.quitting -= Flush;
            Application.quitting += Flush;
            Application.focusChanged -= OnFocus;
            Application.focusChanged += OnFocus;
        }

        static void OnFocus(bool focused) { if (!focused) Flush(); }

        /// <summary>Slider 0 to 1 as dB: 1 is 0 dB, 0 is silent (-80), in between is a perceptual curve.</summary>
        public static float ToDecibels(float slider01) => slider01 <= 0.0001f ? -80f : Mathf.Clamp(20f * Mathf.Log10(slider01), -80f, 0f);

        static void Apply(AudioBus bus, float slider01)
        {
            UiSoundSet set = UiSoundPlayer.Set;
            if (set == null || set.output == null || set.output.audioMixer == null) return;
            set.output.audioMixer.SetFloat(Param[(int)bus], ToDecibels(slider01));
        }

        // Also re-applied in Runner.Start because Unity's AudioMixer can ignore writes before it has started.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void ApplySaved()
        {
            foreach (AudioBus b in System.Enum.GetValues(typeof(AudioBus))) Apply(b, Get(b));
        }
    }
}
