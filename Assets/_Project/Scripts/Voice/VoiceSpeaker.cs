// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "Add VOIP/Proximity chat to the game."
//  Modified  : Claude Fable 5.2, 2026-11-15 — takes sound from our own VoiceCodec and not
//              from Steam's decoder (Harvey: "Make our own custom VOIP for this").
//  Modified  : Claude Fable 5.2, 2026-11-15 — plays through the audio filter callback and not
//              a streamed clip, which Unity read up to 0.8 s ahead (Harvey: "There's a delay").
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Plays one player's voice from where their head is. Holds a short buffer of
//              decoded sound, feeds it to a 3D audio source, and reports whether that
//              player is speaking.
// -----------------------------------------------------------------------------
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;

namespace CloseTheDeal.Voice
{
    /// <summary>
    /// One player's voice coming out of the world. <see cref="VoiceChat"/> makes one per
    /// speaker, pushes decoded sound into it and moves it to the speaker's head.
    /// </summary>
    /// <remarks>
    /// <para>HOW it gets a 3D voice with no delay: the audio source plays a silent-looking
    /// clip that is all ones. Unity places that in 3D (distance fade, left and right) and
    /// hands the result to <see cref="OnAudioFilterRead"/>, about a fiftieth of a second
    /// before it is heard. Multiplying each sample by the voice there gives the voice the
    /// same placement.</para>
    /// <para>WHY not a streamed clip, which is the obvious way: Unity fills a streamed clip
    /// far ahead of the playhead. Measured here at 16 kHz it asked for 0.8 s of sound before
    /// playing any, and that whole read-ahead is delay on the voice.</para>
    /// <para>The audio source must sit above this component on the object, so that the
    /// filter runs on its output. <see cref="RequireComponent"/> adds it first.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class VoiceSpeaker : MonoBehaviour
    {
        /// <summary>Seconds of sound held back before playback starts, so uneven arrival does not crackle. Three pieces.</summary>
        const float StartDelay = 0.06f;

        /// <summary>
        /// Most sound the buffer may hold, in seconds. Past this the oldest is dropped, so a
        /// stall never leaves a voice running a long way behind the mouth.
        /// </summary>
        const float MaxDelay = 0.3f;

        /// <summary>Loudness (0 to 1, root mean square) above which a chunk counts as speech and not room noise.</summary>
        const float SpeechLevel = 0.01f;

        /// <summary>How long after the last speech a player still counts as speaking, so the flag does not flicker between words.</summary>
        const float SpeakingHold = 0.3f;

        /// <summary>Samples in the all-ones clip. Any length works; it loops.</summary>
        const int CarrierSamples = 1024;

        /// <summary>Power-of-2 ring capacity (about 341 ms at 48 kHz, above MaxDelay).</summary>
        const int RingCapacity = 16384;
        const int RingMask = RingCapacity - 1;

        /// <summary>Seconds of empty buffer before pausing the idle AudioSource.</summary>
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
        /// <summary>Voice samples to move on for each output sample: 1 when the sound card runs at the voice's rate.</summary>
        float _step = 1f;
        float _phase;
        int _sampleRate;
        AudioSource _source;
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

        /// <summary>
        /// Sets the speaker up. Called once, straight after it is created.
        /// </summary>
        /// <param name="sampleRate">Samples a second the decoded voice arrives at.</param>
        /// <param name="output">The mixer group to play through, or null for the default output.</param>
        /// <param name="fullRange">Metres within which the voice is at full volume.</param>
        /// <param name="hearRange">Metres at which the voice has faded to nothing.</param>
        public void Init(int sampleRate, AudioMixerGroup output, float fullRange, float hearRange)
        {
            _sampleRate = sampleRate;
            _startSamples = Mathf.CeilToInt(sampleRate * StartDelay);
            _maxSamples = Mathf.CeilToInt(sampleRate * MaxDelay);
            int outputRate = AudioSettings.outputSampleRate;

            _source = GetComponent<AudioSource>();
            _source.outputAudioMixerGroup = output;
            _source.playOnAwake = false;
            _source.loop = true;
            _source.dopplerLevel = 0f;
            _source.spread = 30f;
            _source.rolloffMode = AudioRolloffMode.Custom;
            _source.minDistance = fullRange;
            _source.maxDistance = hearRange;
            // Full volume close up, then a straight fade to silence at the edge of earshot.
            // WHY not Unity's default curve: it never reaches zero, so a voice would carry
            // across the whole map as a faint murmur.
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

            // Published last: the audio thread starts calling OnAudioFilterRead as soon as the
            // source plays, and treats a null ring as "not ready".
            Volatile.Write(ref _step, sampleRate / (float)outputRate);
            Volatile.Write(ref _ring, new float[RingCapacity]);

            _source.Play();
        }

        /// <summary>3D for another player's voice; flat for hearing yourself.</summary>
        public void SetSpatial(bool spatial)
        {
            _source.spatialBlend = spatial ? 1f : 0f;
        }

        /// <summary>
        /// Adds decoded sound to the end of the buffer. Main thread.
        /// </summary>
        /// <param name="sound">Mono samples, -1 to 1.</param>
        /// <param name="samples">How many of <paramref name="sound"/> to add.</param>
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

        /// <summary>
        /// Unity's audio thread, with the all-ones clip already placed in 3D. Each output
        /// sample is multiplied by the next voice sample, or by zero when there is none.
        /// </summary>
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
                    // Between two voice samples when the sound card's rate is not the
                    // voice's; exactly on one when it is.
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
                    // Ran dry: go quiet and wait for the start delay to fill again.
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
