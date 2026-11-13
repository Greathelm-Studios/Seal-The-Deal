using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// Plays item sounds in the world through the set's mixer bus. One-shots use a small pool of
    /// 3D sources, so nothing is created per sound and no clip bypasses the bus (as
    /// AudioSource.PlayClipAtPoint would). A null clip plays nothing. Local only: every machine
    /// plays from the synced events it already receives.
    /// </summary>
    public static class ItemAudio
    {
        const int Voices = 8;
        const float MinDistance = 1.5f;
        const float MaxDistance = 40f;

        static AudioSource[] _voices;
        static int _next;

        /// <summary>Plays a clip once at a point in the world through the set's bus (none if the set is null). Volume is 0 to 1 on top of the set's own.</summary>
        public static void PlayAt(ItemSoundSet set, AudioClip clip, Vector3 point, float volume = 1f)
        {
            if (clip == null || volume <= 0f)
                return;

            AudioSource voice = NextVoice();
            voice.transform.position = point;
            voice.outputAudioMixerGroup = set != null ? set.Output : null;
            voice.PlayOneShot(clip, (set != null ? set.Volume : 1f) * volume);
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

            AudioSource voice = _voices[_next];
            _next = (_next + 1) % Voices;
            return voice;
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
