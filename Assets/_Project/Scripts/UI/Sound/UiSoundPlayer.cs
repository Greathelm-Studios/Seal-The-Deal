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
            get
            {
                if (!Application.isPlaying) return Resources.Load<UiSoundSet>("UI/UiSoundSet");   // edit mode: no runner object
                Runner r = Runner.Ensure();
                return r != null ? r.set : null;
            }
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
                if (!Application.isPlaying) return null;   // sounds only exist in a running game
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

            // ---- Ambient bed and the title sting -------------------------------------------------------------
            // Looked at four times a second, not every frame: is the player at the menus (the title and its screens
            // before a game) or in the lobby reception before the run starts? Reaching the title plays TitleAppear once.
            AudioSource bed;
            float nextLook, bedTarget;
            bool atTitle;
            GameMenu menu;
            UiRoot root;

            void Update()
            {
                float now = Time.unscaledTime;
                if (now >= nextLook)
                {
                    nextLook = now + 0.25f;
                    Look();
                }
                if (bed == null)
                    return;
                float step = set.ambienceFade > 0f ? Time.unscaledDeltaTime / set.ambienceFade : 1f;
                bed.volume = Mathf.MoveTowards(bed.volume, bedTarget, step * Mathf.Max(set.ambienceLevel, 0.0001f));
                if (bed.volume <= 0f && bedTarget <= 0f && bed.isPlaying)
                    bed.Stop();
            }

            void Look()
            {
                if (menu == null) menu = FindAnyObjectByType<GameMenu>();
                if (root == null) root = FindAnyObjectByType<UiRoot>();
                bool title = menu != null && menu.OutOfGame && root != null && root.Stack.Count > 0;
                var roster = CloseTheDeal.Lobby.LobbyRoster.Instance;
                bool reception = roster != null && !roster.RunInProgress;
                if (title && !atTitle)
                    UiSoundPlayer.Play(UiSoundEvent.TitleAppear, 0f);
                atTitle = title;
                SetBed(title || reception);
            }

            void SetBed(bool on)
            {
                bedTarget = on ? set.ambienceLevel : 0f;
                if (!on || set.ambience == null || set.ambienceLevel <= 0f)
                    return;
                if (bed == null)
                {
                    bed = gameObject.AddComponent<AudioSource>();
                    bed.playOnAwake = false;
                    bed.loop = true;
                    bed.spatialBlend = 0f;
                    bed.priority = 200;   // the first voice to lose if the mixer runs short
                    bed.outputAudioMixerGroup = set.ambienceOutput != null ? set.ambienceOutput : set.output;
                    bed.clip = set.ambience;
                    bed.volume = 0f;
                }
                if (!bed.isPlaying)
                    bed.Play();
            }

            public bool Begin(UiSoundEvent e, UiSoundSet.Entry en, float gain, float semitones)
            {
                float now = Time.realtimeSinceStartup;
                // Screen sounds (a screen or panel opening, a transition, the title) sit on their own layer: a button that
                // opens a screen plays its Confirm and the screen its whoosh in the same frame, and both must be heard.
                // The global gap only thins bursts of control sounds.
                bool screen = ScreenLayer(e);
                if (!screen && now - lastAny < set.globalMinGap) { Dropped++; return false; }
                if (lastPlayed.TryGetValue(e, out float t) && now - t < en.minGap) { Dropped++; return false; }
                AudioSource v = Pick();
                float variance = en.pitchVariance > 0f ? Random.Range(-en.pitchVariance, en.pitchVariance) : 0f;
                v.pitch = Mathf.Pow(2f, semitones / 12f) * (1f + variance);
                v.clip = en.clip;
                v.volume = en.volume * gain;
                v.Play();
                if (!screen) lastAny = now;
                lastPlayed[e] = now;
                Started++;
                return true;
            }

            static bool ScreenLayer(UiSoundEvent e)
            {
                switch (e)
                {
                    case UiSoundEvent.Transition: case UiSoundEvent.PanelOpen: case UiSoundEvent.PanelClose:
                    case UiSoundEvent.TitleAppear: case UiSoundEvent.TransitionCloudWipe: case UiSoundEvent.TransitionPaneSlide:
                    case UiSoundEvent.TransitionFrameZoom: case UiSoundEvent.TransitionFade:
                        return true;
                    default:
                        return false;
                }
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
