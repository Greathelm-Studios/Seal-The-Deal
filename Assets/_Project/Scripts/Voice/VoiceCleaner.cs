// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "We probably need noise suppression", then "Yes on the RNNoise."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Removes keyboard, fan and room noise from this player's microphone with the
//              RNNoise library, and says how likely each piece is to be speech.
// -----------------------------------------------------------------------------
using System;
using System.Runtime.InteropServices;

namespace CloseTheDeal.Voice
{
    /// <summary>
    /// Cleans microphone sound with RNNoise, a small neural network trained to tell a voice
    /// from noise. It also reports how likely the sound is to be speech, which opens and
    /// closes the open microphone far better than loudness alone.
    /// </summary>
    /// <remarks>
    /// <para>RNNoise is a native library: <c>Assets/Plugins/RNNoise/x86_64/rnnoise.dll</c>,
    /// built from Xiph's source (see the README beside it). Only a 64-bit Windows build
    /// exists. Anywhere else <see cref="Create"/> returns null and voice goes out uncleaned.</para>
    /// <para>It works on 48 kHz sound in pieces of exactly <see cref="FrameSamples"/>, with
    /// samples in the range of a 16-bit recording, so this scales in and out.</para>
    /// </remarks>
    public sealed class VoiceCleaner : IDisposable
    {
        /// <summary>Samples RNNoise takes at a time: 10 ms at 48 kHz. Fixed by the library.</summary>
        public const int FrameSamples = 480;

        /// <summary>RNNoise expects samples as large as a 16-bit recording's; ours are -1 to 1.</summary>
        const float Scale = 32768f;
        const float InvScale = 1f / Scale;

        const string Library = "rnnoise";

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr rnnoise_create(IntPtr model);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern void rnnoise_destroy(IntPtr state);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] static extern float rnnoise_process_frame(IntPtr state, [Out] float[] output, [In] float[] input);

        readonly float[] _in = new float[FrameSamples];
        readonly float[] _out = new float[FrameSamples];
        IntPtr _state;

        VoiceCleaner(IntPtr state)
        {
            _state = state;
        }

        /// <summary>Makes a cleaner, or returns null where the RNNoise library is missing (any platform but 64-bit Windows).</summary>
        public static VoiceCleaner Create()
        {
            try
            {
                IntPtr state = rnnoise_create(IntPtr.Zero);
                return state != IntPtr.Zero ? new VoiceCleaner(state) : null;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Cleans <see cref="FrameSamples"/> samples in place.
        /// </summary>
        /// <param name="samples">Mono 48 kHz sound, -1 to 1.</param>
        /// <param name="offset">Where the piece starts.</param>
        /// <returns>How likely the piece is speech, 0 to 1.</returns>
        public float Process(float[] samples, int offset)
        {
            if (_state == IntPtr.Zero)
                return 0f;
            for (int i = 0; i < FrameSamples; i++)
                _in[i] = samples[offset + i] * Scale;
            float speech = rnnoise_process_frame(_state, _out, _in);
            for (int i = 0; i < FrameSamples; i++)
                samples[offset + i] = _out[i] * InvScale;
            return speech;
        }

        ~VoiceCleaner() => Dispose();

        public void Dispose()
        {
            if (_state == IntPtr.Zero)
                return;
            rnnoise_destroy(_state);
            _state = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }
}
