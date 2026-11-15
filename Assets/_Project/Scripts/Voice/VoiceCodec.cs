// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "Make our own custom VOIP for this."
//  Modified  : Claude Fable 5.2, 2026-11-15 — our own ADPCM compression is gone; this now wraps
//              Opus (the Concentus library) so the rest of voice chat never touches the
//              library directly (Harvey: "The quality isnt great", then "For the compression,
//              yeah go grab that").
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Compresses and decompresses voice with Opus. One encoder for this player's
//              microphone, one decoder per player heard.
// -----------------------------------------------------------------------------
using System;
using Concentus;
using Concentus.Enums;

namespace CloseTheDeal.Voice
{
    /// <summary>
    /// Compresses this player's voice with Opus, the codec voice apps use.
    /// </summary>
    /// <remarks>
    /// WHY a wrapper: Concentus is vendor code in <c>Assets/Plugins/Concentus</c>. Keeping
    /// every call to it in this file means a library update, or a swap to the native Opus
    /// library, changes this file and nothing else.
    /// </remarks>
    public sealed class VoiceEncoder
    {
        readonly IOpusEncoder _opus;

        /// <param name="sampleRate">Samples a second going in: 48000.</param>
        /// <param name="bitsPerSecond">Target size of the compressed voice.</param>
        public VoiceEncoder(int sampleRate, int bitsPerSecond)
        {
            _opus = OpusCodecFactory.CreateEncoder(sampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            _opus.Bitrate = bitsPerSecond;
            _opus.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
            // Each packet also carries a small, rougher copy of the piece before it, so a
            // listener who loses one packet can rebuild it from the next. 10 is the share of
            // packets Opus should expect to be lost when deciding how much room that copy gets.
            _opus.UseInbandFEC = true;
            _opus.PacketLossPercent = 10;
            // 0 to 10. Concentus is plain C#, far slower than native Opus; 5 keeps one
            // encoder well under a millisecond a piece with no loss a listener would pick out.
            _opus.Complexity = 5;
        }

        /// <summary>Compresses one piece of sound.</summary>
        /// <param name="samples">Mono sound, -1 to 1.</param>
        /// <param name="count">Samples to compress: 20 ms worth.</param>
        /// <param name="packet">Where the compressed bytes go.</param>
        /// <param name="offset">Where in <paramref name="packet"/> to start writing.</param>
        /// <returns>Bytes written.</returns>
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

        /// <param name="sampleRate">Samples a second coming out: 48000.</param>
        public VoiceDecoder(int sampleRate)
        {
            _opus = OpusCodecFactory.CreateDecoder(sampleRate, 1);
        }

        /// <summary>Decompresses one packet.</summary>
        /// <param name="packet">The bytes as they arrived.</param>
        /// <param name="offset">Where the compressed sound starts.</param>
        /// <param name="bytes">How many bytes of it there are.</param>
        /// <param name="samples">Where the sound goes, -1 to 1.</param>
        /// <param name="frameSamples">Samples in one piece.</param>
        /// <param name="previous">
        /// True to rebuild the piece BEFORE this packet from the spare copy the packet
        /// carries, for when that piece was lost. The packet is then decoded again normally.
        /// </param>
        /// <returns>Samples written, or 0 for a packet that is not valid Opus.</returns>
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
