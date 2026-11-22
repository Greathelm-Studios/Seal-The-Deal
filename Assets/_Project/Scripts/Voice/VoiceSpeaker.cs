using System;
using System.Threading;
using CloseTheDeal.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace CloseTheDeal.Voice
{
    /// <summary>Plays one speaker's decoded voice in 3D via a lock-free ring buffer and OnAudioFilterRead.</summary>
    // AudioSource must sit above VoiceSpeaker so OnAudioFilterRead runs on its output.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class VoiceSpeaker : MonoBehaviour
    {
        const float StartDelay = 0.06f;
        const float MaxDelay = 0.3f;
        const float SpeechLevel = 0.01f;
        const float SpeakingHold = 0.3f;
        const int CarrierSamples = 1024;
        const int RingCapacity = 16384;
        const int RingMask = RingCapacity - 1;
        const float PauseSilenceSeconds = 0.5f;

        static AudioClip _sharedCarrier;
        static int _sharedCarrierRate;

        float[] _ring;
        int _writeIndex;
        int _readIndex;
        bool _started;
        bool _paused;
        bool _subscribedAudioConfig;
        int _startSamples;
        int _maxSamples;
        float _step = 1f;
        float _phase;
        int _sampleRate;
        AudioSource _source;
        SpatialOcclusion _occlusion;
        float _lastSpeech = float.NegativeInfinity;

        /// <summary>True while this player is talking, or was a moment ago.</summary>
        public bool Speaking => Time.unscaledTime - _lastSpeech < SpeakingHold;

        /// <summary>The last time sound of any kind arrived, in unscaled seconds.</summary>
        public float LastHeard { get; private set; } = float.NegativeInfinity;

        /// <summary>Seconds of sound waiting to be played: the delay this buffer is adding right now.</summary>
        public float BufferedSeconds
        {
            get
            {
                int count = Volatile.Read(ref _writeIndex) - Volatile.Read(ref _readIndex);
                return _sampleRate > 0 ? Mathf.Max(0, count) / (float)_sampleRate : 0f;
            }
        }

        static AudioClip GetSharedCarrier(int outputRate)
        {
            if (_sharedCarrier != null && _sharedCarrierRate == outputRate)
                return _sharedCarrier;
            if (_sharedCarrier != null)
                Destroy(_sharedCarrier);
            var ones = new float[CarrierSamples];
            for (int i = 0; i < ones.Length; i++)
                ones[i] = 1f;
            _sharedCarrier = AudioClip.Create("Voice carrier", CarrierSamples, 1, outputRate, false);
            _sharedCarrier.SetData(ones, 0);
            _sharedCarrierRate = outputRate;
            return _sharedCarrier;
        }

        /// <summary>Configures the 3D AudioSource, carrier clip, and ring buffer once on creation.</summary>
        public void Init(int sampleRate, AudioMixerGroup output, float fullRange, float hearRange)
        {
            _sampleRate = sampleRate;
            _startSamples = Mathf.CeilToInt(sampleRate * StartDelay);
            _maxSamples = Mathf.CeilToInt(sampleRate * MaxDelay);
            int outputRate = AudioSettings.outputSampleRate;

            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            // High priority (32) so proximity voices are never stolen by distant footsteps or impacts.
            // spatializePostEffects ensures native HRTF plugins process the decoded voice from OnAudioFilterRead.
            _occlusion = SoundSystem.ConfigurePersistent3D(
                _source,
                output != null ? output : SoundSystem.GetMixerGroup("Voice"),
                fullRange,
                hearRange,
                priority: 32,
                useOcclusion: true,
                spread: SoundSystem.VoiceSpread,
                postEffectsSpatialize: true);

            // Preserve the linear 3 m to 24 m fade documented in docs/systems/VOICE.md.
            _source.rolloffMode = AudioRolloffMode.Custom;
            _source.minDistance = fullRange;
            _source.maxDistance = hearRange;
            _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f),
                new Keyframe(fullRange / hearRange, 1f, 0f, 0f),
                new Keyframe(1f, 0f, 0f, 0f)));

            _source.clip = GetSharedCarrier(outputRate);
            SetSpatial(true);

            if (!_subscribedAudioConfig)
            {
                _subscribedAudioConfig = true;
                AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            }

            _writeIndex = 0;
            _readIndex = 0;
            _started = false;
            _paused = false;
            _phase = 0f;

            // Publish _step and _ring before Play() so OnAudioFilterRead sees initialized state.
            Volatile.Write(ref _step, sampleRate / (float)outputRate);
            Volatile.Write(ref _ring, new float[RingCapacity]);

            _source.Play();
        }

        /// <summary>3D with Steam Audio HRTF for another player's voice; flat for hearing yourself.</summary>
        public void SetSpatial(bool spatial)
        {
            _source.spatialBlend = spatial ? 1f : 0f;
            bool useSpatializer = spatial && SoundSystem.SpatializerEnabled;
            _source.spatialize = useSpatializer;
            _source.spatializePostEffects = useSpatializer;
            if (!spatial && _occlusion != null)
                _occlusion.ResetClear();
        }

        /// <summary>Appends decoded mono samples (-1 to 1) to the lock-free ring buffer (main thread).</summary>
        public void Push(float[] sound, int samples)
        {
            float[] ring = _ring;
            if (samples <= 0 || ring == null)
                return;

            double squares = 0.0;
            for (int i = 0; i < samples; i++)
            {
                float value = sound[i];
                squares += value * value;
            }

            int read = Volatile.Read(ref _readIndex);
            int write = _writeIndex;
            int free = RingCapacity - (write - read);
            int count = Math.Min(samples, free);
            for (int i = 0; i < count; i++)
                ring[(write + i) & RingMask] = sound[i];
            if (count > 0)
                Volatile.Write(ref _writeIndex, write + count);

            float now = Time.unscaledTime;
            LastHeard = now;
            if (Math.Sqrt(squares / samples) >= SpeechLevel)
                _lastSpeech = now;

            if (_source != null && (_paused || !_source.isPlaying))
            {
                _paused = false;
                enabled = true;
                if (_occlusion != null && _source.spatialBlend >= 0.5f)
                    _occlusion.SnapNow();
                if (!_source.isPlaying)
                    _source.Play();
            }
        }

        void Update()
        {
            if (_source == null || _paused)
                return;

            int buffered = Volatile.Read(ref _writeIndex) - Volatile.Read(ref _readIndex);
            if (buffered <= 0 && Time.unscaledTime - LastHeard >= PauseSilenceSeconds)
            {
                _paused = true;
                _source.Pause();
                enabled = false;
            }
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (_sampleRate <= 0 || _source == null)
                return;

            int outputRate = AudioSettings.outputSampleRate;
            if (outputRate > 0)
            {
                Volatile.Write(ref _step, _sampleRate / (float)outputRate);
                _source.clip = GetSharedCarrier(outputRate);
            }

            if (!_paused && !_source.isPlaying)
                _source.Play();
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float[] ring = Volatile.Read(ref _ring);
            if (ring == null)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            int write = Volatile.Read(ref _writeIndex);
            int read = _readIndex;
            int count = write - read;
            if (count > _maxSamples)
            {
                read += count - _startSamples;
                count = _startSamples;
            }

            if (!_started && count >= _startSamples)
                _started = true;

            float step = Volatile.Read(ref _step);
            int frames = data.Length / channels;
            for (int i = 0; i < frames; i++)
            {
                float voice = 0f;
                if (_started && count > 0)
                {
                    float here = ring[read & RingMask];
                    float next = count > 1 ? ring[(read + 1) & RingMask] : here;
                    voice = here + (next - here) * _phase;
                    _phase += step;
                    while (_phase >= 1f && count > 0)
                    {
                        _phase -= 1f;
                        read++;
                        count--;
                    }
                }
                else if (_started)
                {
                    _started = false;
                    _phase = 0f;
                }

                int at = i * channels;
                for (int c = 0; c < channels; c++)
                    data[at + c] *= voice;
            }

            Volatile.Write(ref _readIndex, read);
        }

        void OnDestroy()
        {
            if (_subscribedAudioConfig)
            {
                _subscribedAudioConfig = false;
                AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _sharedCarrier = null;
            _sharedCarrierRate = 0;
        }
    }
}
