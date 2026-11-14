using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Maps every UiSoundEvent to its clip, volume and pitch variance, plus the voice-limiting rules.
    /// One asset: Assets/_Project/Resources/UI/UiSoundSet.asset, built by Close the Deal > UI > Set Up Sound.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/UI Sound Set", fileName = "UiSoundSet")]
    public sealed class UiSoundSet : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public UiSoundEvent evt;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Random pitch spread, as a fraction (0.02 = plus or minus 2 %). 0 for a sound that must be exact.")]
            [Range(0f, 0.15f)] public float pitchVariance = 0.02f;
            [Tooltip("Seconds that must pass before this event may play again. Stops rapid hovers stacking into mush.")]
            public float minGap = 0.04f;
        }

        public AudioMixerGroup output;
        public List<Entry> entries = new List<Entry>();

        [Header("Voices")]
        [Min(1)] public int maxVoices = 8;
        [Tooltip("Gap between any two UI sounds, so a burst never lands as one click.")]
        public float globalMinGap = 0.012f;

        [Header("Hover run")]
        [Tooltip("Pentatonic steps (semitones) a run of hovers climbs, then wraps: C D E G A.")]
        public float[] hoverSemitones = { 0f, 2f, 4f, 7f, 9f };
        [Tooltip("A run ends after this long without a hover; the next hover starts on the first step again.")]
        public float hoverRunResetSeconds = 0.6f;

        [Header("Ambient bed (title and lobby)")]
        [Tooltip("The seamless bed that plays under the title, its settings and the lobby reception.")]
        public AudioClip ambience;
        [Tooltip("The mixer group the bed plays through (Music).")]
        public AudioMixerGroup ambienceOutput;
        [Tooltip("The bed's level. Ships at 0: off. Raise it (0.3 is a quiet bed) to hear it; the Music slider scales it too.")]
        [Range(0f, 1f)] public float ambienceLevel = 0f;
        [Tooltip("Seconds to fade the bed in or out when the player reaches or leaves the menus.")]
        public float ambienceFade = 1.5f;

        [Header("Slider")]
        public float sliderMinSemitones = -5f;
        public float sliderMaxSemitones = 12f;

        Dictionary<UiSoundEvent, Entry> map;

        public Entry Find(UiSoundEvent e)
        {
            if (map == null)
            {
                map = new Dictionary<UiSoundEvent, Entry>();
                foreach (Entry en in entries)
                    if (en != null && en.clip != null) map[en.evt] = en;
            }
            return map.TryGetValue(e, out Entry r) ? r : null;
        }

        void OnValidate() { map = null; }
    }
}
