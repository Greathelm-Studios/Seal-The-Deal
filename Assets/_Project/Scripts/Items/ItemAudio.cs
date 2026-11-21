using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>Pooled 3D one-shot playback for item sounds routed through ItemSoundSet mixer buses.</summary>
    public static class ItemAudio
    {
        const int Voices = 24;
        const float MinDistance = 1.5f;
        const float MaxDistance = 40f;

        static AudioSource[] _voices;
        static int _next;
        static Transform _listener;
        static float _nextListenerLookup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _voices = null;
            _next = 0;
            _listener = null;
            _nextListenerLookup = 0f;
        }

        /// <summary>Plays a clip once at a point in the world through the set's bus (none if the set is null). Volume is 0 to 1 on top of the set's own.</summary>
        public static void PlayAt(ItemSoundSet set, AudioClip clip, Vector3 point, float volume = 1f)
        {
            if (clip == null || volume <= 0f || !WithinHearing(point))
                return;

            AudioSource voice = NextVoice();
            voice.transform.position = point;
            voice.outputAudioMixerGroup = set != null ? set.Output : null;
            voice.PlayOneShot(clip, (set != null ? set.Volume : 1f) * volume);
        }

        static bool WithinHearing(Vector3 point)
        {
            if (_listener != null && !_listener.gameObject.activeInHierarchy)
                _listener = null;

            if (_listener == null && Time.time >= _nextListenerLookup)
            {
                _nextListenerLookup = Time.time + 1f;
                AudioListener listener = Object.FindAnyObjectByType<AudioListener>();
                if (listener != null)
                    _listener = listener.transform;
            }

            if (_listener == null)
                return true;

            return (_listener.position - point).sqrMagnitude <= MaxDistance * MaxDistance;
        }

        /// <summary>Gives an item's own source (a loop, the fuse beeps) the set's bus and 3D settings.</summary>
        public static void Configure(AudioSource source, ItemSoundSet set)
        {
            if (source == null)
                return;

            source.outputAudioMixerGroup = set != null ? set.Output : null;
            Make3D(source);
        }

        static AudioSource NextVoice()
        {
            if (_voices == null || _voices[0] == null)
                CreateVoices();

            for (int i = 0; i < Voices; i++)
            {
                int index = (_next + i) % Voices;
                if (!_voices[index].isPlaying)
                {
                    _next = (index + 1) % Voices;
                    return _voices[index];
                }
            }

            AudioSource oldest = _voices[_next];
            _next = (_next + 1) % Voices;
            return oldest;
        }

        static void CreateVoices()
        {
            var holder = new GameObject("Item Sounds") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(holder);
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var child = new GameObject("Voice " + i);
                child.transform.SetParent(holder.transform, false);
                _voices[i] = child.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
                Make3D(_voices[i]);
            }

            _next = 0;
        }

        static void Make3D(AudioSource source)
        {
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = MinDistance;
            source.maxDistance = MaxDistance;
            source.dopplerLevel = 0f;
        }
    }
}
