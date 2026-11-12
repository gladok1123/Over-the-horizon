using System.Buffers.Binary;

namespace OverHorizon.Audio;

/// <summary>
/// Clean-room RIFF/WAVE uncompressed PCM audio decoder converting 8-bit or 16-bit mono/stereo <c>.wav</c> files
/// into 16-bit signed mono <see cref="PcmSoundClip"/> buffers for OpenAL 3D spatialization.
/// </summary>
public static class WavPcmDecoder
{
    /// <summary>
    /// Attempts to decode a RIFF <c>.wav</c> byte span into a 16-bit signed mono <see cref="PcmSoundClip"/>.
    /// </summary>
    /// <param name="cue">Logical sound cue identifier.</param>
    /// <param name="wavBytes">Raw <c>.wav</c> file bytes.</param>
    /// <param name="clip">Decoded mono <see cref="PcmSoundClip"/> on success.</param>
    /// <returns><see langword="true"/> when decoding succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryDecode(SoundCue cue, ReadOnlySpan<byte> wavBytes, out PcmSoundClip clip)
    {
        clip = null!;
        if (wavBytes.Length < 44 ||
            !wavBytes[..4].SequenceEqual("RIFF"u8) ||
            !wavBytes.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            return false;
        }

        ushort audioFormat = 0;
        ushort channels = 0;
        var sampleRate = 0;
        ushort bitsPerSample = 0;
        ReadOnlySpan<byte> dataChunk = default;

        var offset = 12;
        while (offset + 8 <= wavBytes.Length)
        {
            var id = wavBytes.Slice(offset, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(wavBytes.Slice(offset + 4, 4));
            if (size < 0 || offset + 8 + size > wavBytes.Length)
            {
                break;
            }

            var payload = wavBytes.Slice(offset + 8, size);
            if (id.SequenceEqual("fmt "u8) && size >= 16)
            {
                audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(payload[..2]);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(4, 4));
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(14, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                dataChunk = payload;
                break;
            }

            offset += 8 + size + (size & 1);
        }

        if (audioFormat != 1 || channels is < 1 or > 2 || sampleRate is < 4000 or > 192000 ||
            bitsPerSample is not (8 or 16) || dataChunk.IsEmpty)
        {
            return false;
        }

        var bytesPerSample = bitsPerSample / 8;
        var frameStride = bytesPerSample * channels;
        var frameCount = dataChunk.Length / frameStride;
        if (frameCount <= 0)
        {
            return false;
        }

        var mono = new short[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            var fOff = i * frameStride;
            if (bitsPerSample == 16)
            {
                var left = BinaryPrimitives.ReadInt16LittleEndian(dataChunk.Slice(fOff, 2));
                if (channels == 2)
                {
                    var right = BinaryPrimitives.ReadInt16LittleEndian(dataChunk.Slice(fOff + 2, 2));
                    mono[i] = (short)((left + right) / 2);
                }
                else
                {
                    mono[i] = left;
                }
            }
            else
            {
                var left = (dataChunk[fOff] - 128) << 8;
                if (channels == 2)
                {
                    var right = (dataChunk[fOff + 1] - 128) << 8;
                    mono[i] = (short)((left + right) / 2);
                }
                else
                {
                    mono[i] = (short)left;
                }
            }
        }

        clip = new PcmSoundClip(cue, sampleRate, mono);
        return true;
    }
}
