using UnityEngine;

namespace CloseTheDeal.UI
{
    /// <summary>
    /// Measures the final output (after the mixer) of the AudioListener it sits next to: peak and RMS since the last
    /// Reset. Only the UI sound self-test adds it; nothing in the game does.
    /// </summary>
    public sealed class UiSoundMeter : MonoBehaviour
    {
        static float peak;
        static double sumSquares;
        static long samples;
        static readonly object Lock = new object();

        public static void Reset() { lock (Lock) { peak = 0f; sumSquares = 0; samples = 0; } }
        public static float Peak { get { lock (Lock) return peak; } }
        public static float Rms { get { lock (Lock) return samples == 0 ? 0f : (float)System.Math.Sqrt(sumSquares / samples); } }
        public static long Samples { get { lock (Lock) return samples; } }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float p = 0f;
            double s = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float a = Mathf.Abs(data[i]);
                if (a > p) p = a;
                s += data[i] * data[i];
            }
            lock (Lock) { if (p > peak) peak = p; sumSquares += s; samples += data.Length; }
        }
    }
}
