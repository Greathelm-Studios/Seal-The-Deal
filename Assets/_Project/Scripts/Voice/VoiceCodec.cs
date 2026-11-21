using System;
using Concentus;
using Concentus.Enums;

namespace CloseTheDeal.Voice
{
    /// <summary>Compresses mono voice frames with Concentus Opus.</summary>
    public sealed class VoiceEncoder
    {
        readonly IOpusEncoder _opus;

        /// <summary>Creates an Opus VoIP encoder with inband FEC enabled.</summary>
        public VoiceEncoder(int sampleRate, int bitsPerSecond)
        {
            _opus = OpusCodecFactory.CreateEncoder(sampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            _opus.Bitrate = bitsPerSecond;
            _opus.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
            _opus.UseInbandFEC = true;
            _opus.PacketLossPercent = 10;
            // Pure-C# Concentus is much slower than native Opus; complexity 5 stays under 1 ms/frame.
            _opus.Complexity = 5;
        }

        /// <summary>Encodes count samples into packet at offset and returns bytes written.</summary>
        public int Encode(float[] samples, int count, byte[] packet, int offset)
        {
            return _opus.Encode(samples.AsSpan(0, count), count, packet.AsSpan(offset), packet.Length - offset);
        }

        /// <summary>Forgets the sound so far. Called before the first piece after a silence, so the old voice does not colour the new.</summary>
        public void Reset()
        {
            _opus.ResetState();
        }
    }

    /// <summary>Decompresses one player's voice. Opus remembers the sound so far, so each speaker needs their own.</summary>
    public sealed class VoiceDecoder
    {
        readonly IOpusDecoder _opus;

        /// <summary>Creates a mono Opus decoder.</summary>
        public VoiceDecoder(int sampleRate)
        {
            _opus = OpusCodecFactory.CreateDecoder(sampleRate, 1);
        }

        /// <summary>Decodes one Opus packet (or its previous-frame FEC copy), returning samples written or 0 if invalid.</summary>
        public int Decode(byte[] packet, int offset, int bytes, float[] samples, int frameSamples, bool previous = false)
        {
            try
            {
                return _opus.Decode(new ReadOnlySpan<byte>(packet, offset, bytes), samples.AsSpan(0, frameSamples), frameSamples, previous);
            }
            catch (Exception)
            {
                // A damaged or forged packet. Nothing to play; the next good one carries on.
                return 0;
            }
        }

        /// <summary>Fills in one lost piece by carrying on from the sound before it.</summary>
        /// <returns>Samples written.</returns>
        public int Conceal(float[] samples, int frameSamples)
        {
            try
            {
                return _opus.Decode(ReadOnlySpan<byte>.Empty, samples.AsSpan(0, frameSamples), frameSamples, false);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Forgets the sound so far. Called when a speaker starts again after a silence.</summary>
        public void Reset()
        {
            _opus.ResetState();
        }
    }
}
