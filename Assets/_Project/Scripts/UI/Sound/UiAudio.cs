using UnityEngine;
using UnityEngine.Audio;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The door to the mixer for a volume slider (the settings screen's audio rows call it once they exist): SetVolume(AudioBus.Ui, 0.8f) takes a 0 to 1 slider value,
    /// writes the exposed dB parameter and remembers it between launches (PlayerPrefs).
    /// </summary>
    public static class UiAudio
    {
        const string Key = "ctd.audio.";
        static readonly string[] Param = { "MasterVolume", "MusicVolume", "SfxVolume", "VoiceVolume", "UiVolume" };
        static readonly float[] Default = { 1f, 0.8f, 1f, 1f, 1f };

        public static float Get(AudioBus bus) => PlayerPrefs.GetFloat(Key + bus, Default[(int)bus]);

        public static void SetVolume(AudioBus bus, float slider01, bool save = true)
        {
            slider01 = Mathf.Clamp01(slider01);
            Apply(bus, slider01);
            if (save) { PlayerPrefs.SetFloat(Key + bus, slider01); PlayerPrefs.Save(); }
        }

        /// <summary>Slider 0 to 1 as dB: 1 is 0 dB, 0 is silent (-80), in between is a perceptual curve.</summary>
        public static float ToDecibels(float slider01) => slider01 <= 0.0001f ? -80f : Mathf.Clamp(20f * Mathf.Log10(slider01), -80f, 0f);

        static void Apply(AudioBus bus, float slider01)
        {
            UiSoundSet set = UiSoundPlayer.Set;
            if (set == null || set.output == null || set.output.audioMixer == null) return;
            set.output.audioMixer.SetFloat(Param[(int)bus], ToDecibels(slider01));
        }

        /// <summary>Restore the saved volumes. Runs after the first scene loads, and again from the sound runner's
        /// Start one frame later, because a mixer can ignore parameter writes made before it has started.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void ApplySaved()
        {
            foreach (AudioBus b in System.Enum.GetValues(typeof(AudioBus))) Apply(b, Get(b));
        }
    }
}
