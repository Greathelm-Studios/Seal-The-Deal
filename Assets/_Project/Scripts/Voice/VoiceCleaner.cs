using System;
using System.Runtime.InteropServices;

namespace CloseTheDeal.Voice
{
    /// <summary>Wraps the native RNNoise library (64-bit Windows) to denoise 48 kHz frames and estimate speech probability.</summary>
    public sealed class VoiceCleaner : IDisposable
    {
        /// <summary>Samples RNNoise takes per 10 ms frame at 48 kHz.</summary>
        public const int FrameSamples = 480;

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

        /// <summary>Cleans FrameSamples mono 48 kHz samples in place and returns speech probability (0 to 1).</summary>
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
