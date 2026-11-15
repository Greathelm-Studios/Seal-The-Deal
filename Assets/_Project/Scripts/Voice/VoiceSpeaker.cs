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

        readonly object _lock = new();
        float[] _ring;
        int _read;
        int _count;
        bool _started;
        int _startSamples;
        int _maxSamples;
        /// <summary>Voice samples to move on for each output sample: 1 when the sound card runs at the voice's rate.</summary>
        float _step = 1f;
        float _phase;
        int _sampleRate;
        AudioSource _source;
        AudioClip _carrier;
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
                lock (_lock)
                    return _sampleRate > 0 ? _count / (float)_sampleRate : 0f;
            }
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

            var ones = new float[CarrierSamples];
            for (int i = 0; i < ones.Length; i++)
                ones[i] = 1f;
            _carrier = AudioClip.Create("Voice carrier", CarrierSamples, 1, outputRate, false);
            _carrier.SetData(ones, 0);
            _source.clip = _carrier;
            SetSpatial(true);

            // Set last: the audio thread starts calling OnAudioFilterRead as soon as the
            // source plays, and treats a null ring as "not ready".
            lock (_lock)
            {
                _step = sampleRate / (float)outputRate;
                _ring = new float[sampleRate];
            }

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
            if (samples <= 0 || _ring == null)
                return;

            double squares = 0.0;
            lock (_lock)
            {
                int capacity = _ring.Length;
                for (int i = 0; i < samples; i++)
                {
                    float value = sound[i];
                    squares += value * value;
                    if (_count == capacity)
                    {
                        _read = (_read + 1) % capacity;
                        _count--;
                    }

                    _ring[(_read + _count) % capacity] = value;
                    _count++;
                }

                if (_count > _maxSamples)
                {
                    int drop = _count - _startSamples;
                    _read = (_read + drop) % capacity;
                    _count -= drop;
                }
            }

            LastHeard = Time.unscaledTime;
            if (System.Math.Sqrt(squares / samples) >= SpeechLevel)
                _lastSpeech = Time.unscaledTime;
        }

        /// <summary>
        /// Unity's audio thread, with the all-ones clip already placed in 3D. Each output
        /// sample is multiplied by the next voice sample, or by zero when there is none.
        /// </summary>
        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (_lock)
            {
                if (_ring == null)
                {
                    System.Array.Clear(data, 0, data.Length);
                    return;
                }

                if (!_started && _count >= _startSamples)
                    _started = true;

                int capacity = _ring.Length;
                int frames = data.Length / channels;
                for (int i = 0; i < frames; i++)
                {
                    float voice = 0f;
                    if (_started && _count > 0)
                    {
                        // Between two voice samples when the sound card's rate is not the
                        // voice's; exactly on one when it is.
                        float here = _ring[_read];
                        float next = _count > 1 ? _ring[(_read + 1) % capacity] : here;
                        voice = here + (next - here) * _phase;
                        _phase += _step;
                        while (_phase >= 1f && _count > 0)
                        {
                            _phase -= 1f;
                            _read = (_read + 1) % capacity;
                            _count--;
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
            }
        }

        void OnDestroy()
        {
            if (_carrier != null)
                Destroy(_carrier);
        }
    }
}
