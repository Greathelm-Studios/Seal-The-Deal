using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// The items' stand-in sounds, made once in code so the fuse beeps and the extinguisher
    /// hisses before any audio assets exist. Real clips replace these by assigning them on
    /// the item prefabs; nothing else changes.
    /// </summary>
    public static class ItemTones
    {
        const int SampleRate = 44100;

        static AudioClip _beep;
        static AudioClip _hiss;
        static AudioClip _boom;

        /// <summary>A short high beep, the grenade case's fuse.</summary>
        public static AudioClip Beep => _beep != null ? _beep : _beep = Make("Item Beep", 0.07f, (t, n) => Mathf.Sin(t * 2f * Mathf.PI * 1760f) * Fade(n, 0.1f) * 0.5f);

        /// <summary>One second of rushing air that loops, the extinguisher venting.</summary>
        public static AudioClip Hiss => _hiss != null ? _hiss : _hiss = Make("Item Hiss", 1f, (t, n) => Noise() * 0.25f);

        /// <summary>A low thump with a noisy tail, the grenade case going off.</summary>
        public static AudioClip Boom => _boom != null ? _boom : _boom = Make("Item Boom", 0.8f, (t, n) => (Mathf.Sin(t * 2f * Mathf.PI * 55f) * 0.8f + Noise() * 0.5f) * (1f - n) * (1f - n));

        delegate float Wave(float seconds, float normalised);

        static uint _seed = 0x9E3779B9u;

        static AudioClip Make(string name, float seconds, Wave wave)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = wave(i / (float)SampleRate, i / (float)count);

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Fades the first and last part of a tone so it does not click.</summary>
        static float Fade(float normalised, float edge)
        {
            return Mathf.Clamp01(Mathf.Min(normalised, 1f - normalised) / edge);
        }

        static float Noise()
        {
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFF) / 32768f - 1f;
        }
    }
}
