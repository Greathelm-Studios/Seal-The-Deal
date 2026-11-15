// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "Add VOIP/Proximity chat to the game."
//  Modified  : Claude Fable 5.2, 2026-11-15 — takes sound from our own VoiceCodec and not
//              from Steam's decoder (Harvey: "Make our own custom VOIP for this").
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
    /// WHY a streamed clip and not OnAudioFilterRead: Unity places a source in 3D (distance,
    /// left and right) before the filter callback runs, and only for a source that is
    /// playing a clip. A streamed clip that pulls from the buffer gets the same 3D treatment
    /// as any other sound, and the Voice volume slider, with no code here.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class VoiceSpeaker : MonoBehaviour
    {
        /// <summary>Seconds of sound held back before playback starts, so uneven arrival does not crackle.</summary>
        const float StartDelay = 0.08f;

        /// <summary>
        /// Most sound the buffer may hold, in seconds. Past this the oldest is dropped, so a
        /// stall never leaves a voice running half a second behind the mouth.
        /// </summary>
        const float MaxDelay = 0.4f;

        /// <summary>Loudness (0 to 1, root mean square) above which a chunk counts as speech and not room noise.</summary>
        const float SpeechLevel = 0.01f;

        /// <summary>How long after the last speech a player still counts as speaking, so the flag does not flicker between words.</summary>
        const float SpeakingHold = 0.3f;

        readonly object _lock = new();
        float[] _ring;
        int _read;
        int _count;
        bool _started;
        int _startSamples;
        int _maxSamples;
        AudioSource _source;
        float _lastSpeech = float.NegativeInfinity;

        /// <summary>True while this player is talking, or was a moment ago.</summary>
        public bool Speaking => Time.unscaledTime - _lastSpeech < SpeakingHold;

        /// <summary>The last time sound of any kind arrived, in unscaled seconds.</summary>
        public float LastHeard { get; private set; }

        /// <summary>
        /// Sets the speaker up. Called once, straight after it is created.
        /// </summary>
        /// <param name="sampleRate">Samples a second the decoded voice arrives at.</param>
        /// <param name="output">The mixer group to play through, or null for the default output.</param>
        /// <param name="fullRange">Metres within which the voice is at full volume.</param>
        /// <param name="hearRange">Metres at which the voice has faded to nothing.</param>
        public void Init(int sampleRate, AudioMixerGroup output, float fullRange, float hearRange)
        {
            _ring = new float[sampleRate];
            _startSamples = Mathf.CeilToInt(sampleRate * StartDelay);
            _maxSamples = Mathf.CeilToInt(sampleRate * MaxDelay);

            _source = gameObject.AddComponent<AudioSource>();
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
            _source.clip = AudioClip.Create("Voice", sampleRate, 1, sampleRate, true, Fill);
            SetSpatial(true);
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
            if (samples <= 0)
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

        /// <summary>Unity's audio thread asking for the next stretch of sound.</summary>
        void Fill(float[] data)
        {
            lock (_lock)
            {
                if (!_started && _count >= _startSamples)
                    _started = true;

                int capacity = _ring.Length;
                for (int i = 0; i < data.Length; i++)
                {
                    if (_started && _count > 0)
                    {
                        data[i] = _ring[_read];
                        _read = (_read + 1) % capacity;
                        _count--;
                    }
                    else
                    {
                        // Ran dry: go quiet and wait for the start delay to fill again.
                        _started = false;
                        data[i] = 0f;
                    }
                }
            }
        }
    }
}
