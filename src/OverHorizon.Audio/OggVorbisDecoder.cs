using NVorbis;

namespace OverHorizon.Audio;

/// <summary>
/// Decodes real compressed Ogg Vorbis (<c>.ogg</c>) audio streams and resource-pack files into
/// 16-bit signed mono <see cref="PcmSoundClip"/> buffers for OpenAL 3D spatialization.
/// </summary>
public static class OggVorbisDecoder
{
    /// <summary>
    /// Attempts to decode an Ogg Vorbis (<c>.ogg</c>) byte array into a mono <see cref="PcmSoundClip"/>.
    /// </summary>
    /// <param name="cue">Logical sound cue identifier.</param>
    /// <param name="oggBytes">Raw <c>.ogg</c> file bytes.</param>
    /// <param name="clip">Decoded mono <see cref="PcmSoundClip"/> on success.</param>
    /// <returns><see langword="true"/> when decoding succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryDecode(SoundCue cue, byte[] oggBytes, out PcmSoundClip clip)
    {
        clip = null!;
        if (oggBytes is null || oggBytes.Length < 28 ||
            oggBytes[0] != (byte)'O' || oggBytes[1] != (byte)'g' || oggBytes[2] != (byte)'g' || oggBytes[3] != (byte)'S')
        {
            return false;
        }

        using var ms = new MemoryStream(oggBytes, writable: false);
        return TryDecodeStream(cue, ms, out clip);
    }

    /// <summary>
    /// Attempts to decode an Ogg Vorbis (<c>.ogg</c>) <see cref="Stream"/> into a mono <see cref="PcmSoundClip"/>.
    /// </summary>
    /// <param name="cue">Logical sound cue identifier.</param>
    /// <param name="stream">Readable stream positioned at an Ogg Vorbis header.</param>
    /// <param name="clip">Decoded mono <see cref="PcmSoundClip"/> on success.</param>
    /// <returns><see langword="true"/> when decoding succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryDecodeStream(SoundCue cue, Stream stream, out PcmSoundClip clip)
    {
        ArgumentNullException.ThrowIfNull(stream);
        clip = null!;

        try
        {
            using var reader = new VorbisReader(stream, closeOnDispose: false);
            var channels = reader.Channels;
            var sampleRate = reader.SampleRate;
            if (channels is < 1 or > 8 || sampleRate is < 4000 or > 192000)
            {
                return false;
            }

            var floatBuf = new float[4096 * channels];
            var monoOut = new List<short>(16384);

            while (true)
            {
                var read = reader.ReadSamples(floatBuf, 0, floatBuf.Length);
                if (read <= 0)
                {
                    break;
                }

                var frames = read / channels;
                for (var f = 0; f < frames; f++)
                {
                    var baseIdx = f * channels;
                    var sum = 0.0f;
                    for (var c = 0; c < channels; c++)
                    {
                        sum += floatBuf[baseIdx + c];
                    }

                    var avg = Math.Clamp(sum / channels, -1.0f, 1.0f);
                    monoOut.Add((short)MathF.Round(avg * 32767.0f));

                    // Cap single SFX clip length at 8 seconds so music tracks never exhaust OpenAL RAM.
                    if (monoOut.Count >= sampleRate * 8)
                    {
                        clip = new PcmSoundClip(cue, sampleRate, monoOut.ToArray());
                        return true;
                    }
                }
            }

            if (monoOut.Count == 0)
            {
                return false;
            }

            clip = new PcmSoundClip(cue, sampleRate, monoOut.ToArray());
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or IOException)
        {
            return false;
        }
    }
}
