using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// The player behind the UI sound hub (UiSound in Scripts/UI/Kit, lane 5): clips, mixer bus, voice limits, hover-run pitch.
    /// Callers use UiSound.Play(UiSoundEvent.Confirm); the hub forwards here.
    /// Plays through the mixer's UI bus, never while the game is unfocused, never louder than the set says,
    /// and a burst of the same event (rapid hovers) is thinned by a per-event minimum gap and a voice cap.
    /// Sound is a presentation effect only: nothing here touches game state or the network.
    /// </summary>
    public static class UiSoundPlayer
    {
        /// <summary>The set in use. Loaded from Resources/UI/UiSoundSet on first use.</summary>
        public static UiSoundSet Set
        {
            get { Runner r = Runner.Ensure(); return r != null ? r.set : null; }
        }

        /// <summary>Test hook: play even when the player is not focused (batch-mode checks).</summary>
        public static bool IgnoreFocus;

        /// <summary>How many sounds were actually started / dropped by the gap and voice rules (for tests).</summary>
        public static int Started, Dropped;

        public static void Play(UiSoundEvent e) => Play(e, 1f, 0f);

        /// <summary>The hub's entry point: play e, pitched up by `semitones`. False if it was dropped or unavailable.</summary>
        public static bool Play(UiSoundEvent e, float semitones) => Play(e, 1f, semitones);

        /// <summary>A run of hovers climbs the pentatonic scale, and starts again after a pause.</summary>
        public static void PlayHover()
        {
            Runner r = Runner.Ensure();
            if (r == null || r.set == null) return;
            float now = Time.realtimeSinceStartup;
            if (now - r.lastHover > r.set.hoverRunResetSeconds) r.hoverStep = 0;
            float[] steps = r.set.hoverSemitones;
            float semis = steps != null && steps.Length > 0 ? steps[r.hoverStep % steps.Length] : 0f;
            if (Play(UiSoundEvent.HoverMove, 1f, semis)) { r.hoverStep++; r.lastHover = now; }
        }

        /// <summary>A slider tick whose pitch follows the value, 0 to 1.</summary>
        public static void PlaySlider(float value01)
        {
            Runner r = Runner.Ensure();
            if (r == null || r.set == null) return;
            float semis = Mathf.Lerp(r.set.sliderMinSemitones, r.set.sliderMaxSemitones, Mathf.Clamp01(value01));
            Play(UiSoundEvent.SliderTick, 1f, semis);
        }

        static bool Play(UiSoundEvent e, float gain, float semitones)
        {
            Runner r = Runner.Ensure();
            if (r == null || r.set == null || e == UiSoundEvent.None) return false;
            if (!IgnoreFocus && !Application.isFocused) return false;
            UiSoundSet.Entry en = r.set.Find(e);
            if (en == null) return false;
            return r.Begin(e, en, gain, semitones);
        }

        sealed class Runner : MonoBehaviour
        {
            static Runner instance;
            public UiSoundSet set;
            public float lastHover = -99f;
            public int hoverStep;
            AudioSource[] voices;
            readonly Dictionary<UiSoundEvent, float> lastPlayed = new Dictionary<UiSoundEvent, float>();
            float lastAny = -99f;

            public static Runner Ensure()
            {
                if (instance != null) return instance;
                var set = Resources.Load<UiSoundSet>("UI/UiSoundSet");
                if (set == null) return null;
                var go = new GameObject("UiSoundRunner") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Runner>();
                instance.set = set;
                instance.voices = new AudioSource[Mathf.Max(1, set.maxVoices)];
                for (int i = 0; i < instance.voices.Length; i++)
                {
                    var a = go.AddComponent<AudioSource>();
                    a.playOnAwake = false;
                    a.spatialBlend = 0f;
                    a.outputAudioMixerGroup = set.output;
                    instance.voices[i] = a;
                }
                return instance;
            }

            void Start() => UiAudio.ApplySaved();

            public bool Begin(UiSoundEvent e, UiSoundSet.Entry en, float gain, float semitones)
            {
                float now = Time.realtimeSinceStartup;
                if (now - lastAny < set.globalMinGap) { Dropped++; return false; }
                if (lastPlayed.TryGetValue(e, out float t) && now - t < en.minGap) { Dropped++; return false; }
                AudioSource v = Pick();
                float variance = en.pitchVariance > 0f ? Random.Range(-en.pitchVariance, en.pitchVariance) : 0f;
                v.pitch = Mathf.Pow(2f, semitones / 12f) * (1f + variance);
                v.clip = en.clip;
                v.volume = en.volume * gain;
                v.Play();
                lastAny = now;
                lastPlayed[e] = now;
                Started++;
                return true;
            }

            // A free voice, else steal the one nearest its end.
            AudioSource Pick()
            {
                AudioSource best = voices[0];
                float bestLeft = float.MaxValue;
                foreach (AudioSource a in voices)
                {
                    if (!a.isPlaying) return a;
                    float left = a.clip != null ? (a.clip.length - a.time) : 0f;
                    if (left < bestLeft) { bestLeft = left; best = a; }
                }
                return best;
            }
        }
    }
}
