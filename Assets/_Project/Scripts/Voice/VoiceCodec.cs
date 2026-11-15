// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "Make our own custom VOIP for this."
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Our own voice compression: squeezes each 16-bit sample of speech into 4 bits
//              (IMA ADPCM) and back. Every packet carries what it needs to be decoded on
//              its own, so a lost packet costs only its own sound.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CloseTheDeal.Voice
{
    /// <summary>
    /// Compresses speech to a quarter of its size and back, with no library.
    /// </summary>
    /// <remarks>
    /// <para>The method is IMA ADPCM: each sample is stored as a 4-bit step up or down from a
    /// running guess, and the step size grows and shrinks with the sound. It is a public,
    /// decades-old telephone and game-audio method, simple enough to hold in one file.</para>
    /// <para>WHY this and not Opus: Opus would be about a third of the size for the same
    /// quality, but it is a native library that has to be built and shipped per platform.
    /// This costs about 8 kB a second per talking player and nothing else.</para>
    /// <para>A packet is: the encoder's guess (2 bytes) and step position (1 byte) at its
    /// first sample, then one byte per two samples, first sample in the low half.</para>
    /// </remarks>
    public static class VoiceCodec
    {
        /// <summary>Bytes at the start of a packet that hold the decoder's starting state.</summary>
        public const int HeaderBytes = 3;

        /// <summary>How far the step position moves after each 4-bit code: small codes shrink the step, large ones grow it.</summary>
        static readonly int[] IndexMove = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };

        /// <summary>The 89 step sizes of IMA ADPCM, from the quietest change it can store to the loudest.</summary>
        static readonly int[] Steps =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45,
            50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307,
            337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066,
            2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
            15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767,
        };

        const int LastStep = 88;

        /// <summary>The encoder's running state, carried from one packet to the next so the sound stays continuous.</summary>
        public struct State
        {
            /// <summary>The running guess of the next sample, as a 16-bit value.</summary>
            public int Guess;

            /// <summary>Position in the step table, 0 to 88.</summary>
            public int Step;
        }

        /// <summary>Bytes a packet of this many samples takes.</summary>
        public static int PacketBytes(int samples) => HeaderBytes + (samples + 1) / 2;

        /// <summary>
        /// Compresses one stretch of speech into a packet.
        /// </summary>
        /// <param name="samples">Sound, -1 to 1.</param>
        /// <param name="count">How many of <paramref name="samples"/> to compress.</param>
        /// <param name="packet">Where the packet is written.</param>
        /// <param name="offset">Where in <paramref name="packet"/> it starts.</param>
        /// <param name="state">The encoder's state; updated for the next packet.</param>
        /// <returns>Bytes written.</returns>
        public static int Encode(float[] samples, int count, byte[] packet, int offset, ref State state)
        {
            packet[offset] = (byte)state.Guess;
            packet[offset + 1] = (byte)(state.Guess >> 8);
            packet[offset + 2] = (byte)state.Step;

            int at = offset + HeaderBytes;
            for (int i = 0; i < count; i++)
            {
                int sample = Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), -32768, 32767);
                int code = EncodeSample(sample, ref state);
                if ((i & 1) == 0)
                    packet[at] = (byte)code;
                else
                    packet[at++] |= (byte)(code << 4);
            }

            return PacketBytes(count);
        }

        /// <summary>
        /// Turns a packet back into sound.
        /// </summary>
        /// <param name="packet">The packet's bytes.</param>
        /// <param name="offset">Where in <paramref name="packet"/> it starts.</param>
        /// <param name="bytes">How long it is.</param>
        /// <param name="samples">Where the sound is written, -1 to 1.</param>
        /// <returns>Samples written; 0 for a packet that is too short or has an impossible header.</returns>
        public static int Decode(byte[] packet, int offset, int bytes, float[] samples)
        {
            if (bytes <= HeaderBytes)
                return 0;

            var state = new State
            {
                Guess = (short)(packet[offset] | packet[offset + 1] << 8),
                Step = packet[offset + 2],
            };
            if (state.Step > LastStep)
                return 0;

            int count = Mathf.Min((bytes - HeaderBytes) * 2, samples.Length);
            int at = offset + HeaderBytes;
            for (int i = 0; i < count; i++)
            {
                int code = (i & 1) == 0 ? packet[at] & 0x0F : packet[at++] >> 4;
                samples[i] = DecodeSample(code, ref state) / 32768f;
            }

            return count;
        }

        static int EncodeSample(int sample, ref State state)
        {
            int step = Steps[state.Step];
            int difference = sample - state.Guess;
            int code = 0;
            if (difference < 0)
            {
                code = 8;
                difference = -difference;
            }

            // The same sum the decoder will make, so both guesses stay in step.
            int change = step >> 3;
            if (difference >= step)
            {
                code |= 4;
                difference -= step;
                change += step;
            }

            if (difference >= step >> 1)
            {
                code |= 2;
                difference -= step >> 1;
                change += step >> 1;
            }

            if (difference >= step >> 2)
            {
                code |= 1;
                change += step >> 2;
            }

            Advance(code, change, ref state);
            return code;
        }

        static int DecodeSample(int code, ref State state)
        {
            int step = Steps[state.Step];
            int change = step >> 3;
            if ((code & 4) != 0)
                change += step;
            if ((code & 2) != 0)
                change += step >> 1;
            if ((code & 1) != 0)
                change += step >> 2;

            Advance(code, change, ref state);
            return state.Guess;
        }

        static void Advance(int code, int change, ref State state)
        {
            state.Guess = Mathf.Clamp((code & 8) != 0 ? state.Guess - change : state.Guess + change, -32768, 32767);
            state.Step = Mathf.Clamp(state.Step + IndexMove[code], 0, LastStep);
        }
    }
}
