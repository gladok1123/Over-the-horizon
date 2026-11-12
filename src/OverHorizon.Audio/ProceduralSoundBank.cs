using OverHorizon.Shared.Random;

namespace OverHorizon.Audio;

/// <summary>
/// Clean-room procedural 16-bit mono PCM synthesizer producing zero-copyright voxel sound effects.
/// </summary>
public static class ProceduralSoundBank
{
    /// <summary>Default synthesis sample rate in Hz.</summary>
    public const int DefaultSampleRate = 22_050;

    /// <summary>
    /// Synthesizes deterministic PCM clips for every <see cref="SoundCue"/>.
    /// </summary>
    /// <returns>Dictionary mapping each <see cref="SoundCue"/> to its synthesized <see cref="PcmSoundClip"/>.</returns>
    public static IReadOnlyDictionary<SoundCue, PcmSoundClip> CreateDefaultBank()
    {
        var cues = Enum.GetValues<SoundCue>();
        var bank = new Dictionary<SoundCue, PcmSoundClip>(cues.Length);
        foreach (var cue in cues)
        {
            bank[cue] = Synthesize(cue);
        }

        return bank;
    }

    /// <summary>
    /// Synthesizes a single deterministic 16-bit mono PCM clip for <paramref name="cue"/>.
    /// </summary>
    /// <param name="cue">Sound cue to synthesize.</param>
    /// <returns>Synthesized <see cref="PcmSoundClip"/>.</returns>
    public static PcmSoundClip Synthesize(SoundCue cue) => cue switch
    {
        SoundCue.FootstepStone => RenderClip(cue, durationSec: 0.085, baseFreqHz: 210.0, endFreqHz: 95.0, noiseMix: 0.55, decayRate: 28.0, seed: 101UL),
        SoundCue.FootstepGrass => RenderClip(cue, durationSec: 0.100, baseFreqHz: 140.0, endFreqHz: 70.0, noiseMix: 0.85, decayRate: 24.0, seed: 202UL),
        SoundCue.FootstepWood => RenderClip(cue, durationSec: 0.095, baseFreqHz: 185.0, endFreqHz: 120.0, noiseMix: 0.25, decayRate: 26.0, seed: 303UL),
        SoundCue.BlockBreak => RenderClip(cue, durationSec: 0.190, baseFreqHz: 260.0, endFreqHz: 65.0, noiseMix: 0.75, decayRate: 14.0, seed: 404UL),
        SoundCue.BlockPlace => RenderClip(cue, durationSec: 0.115, baseFreqHz: 155.0, endFreqHz: 85.0, noiseMix: 0.35, decayRate: 22.0, seed: 505UL),
        SoundCue.WaterSplash => RenderClip(cue, durationSec: 0.260, baseFreqHz: 320.0, endFreqHz: 140.0, noiseMix: 0.90, decayRate: 10.0, seed: 606UL),
        _ => RenderClip(cue, durationSec: 0.040, baseFreqHz: 880.0, endFreqHz: 660.0, noiseMix: 0.08, decayRate: 55.0, seed: 707UL),
    };

    private static PcmSoundClip RenderClip(
        SoundCue cue,
        double durationSec,
        double baseFreqHz,
        double endFreqHz,
        double noiseMix,
        double decayRate,
        ulong seed)
    {
        var sampleCount = Math.Max(64, (int)(DefaultSampleRate * durationSec));
        var samples = new short[sampleCount];
        var rng = new Xoroshiro128PlusPlus(seed);

        var phase = 0.0;
        var lowPassState = 0.0;

        for (var i = 0; i < sampleCount; i++)
        {
            var t = (double)i / DefaultSampleRate;
            var progress = (double)i / sampleCount;

            var freq = baseFreqHz + ((endFreqHz - baseFreqHz) * progress);
            phase += (2.0 * Math.PI * freq) / DefaultSampleRate;

            var tone = Math.Sin(phase);
            var rawNoise = (rng.NextDouble() * 2.0) - 1.0;
            lowPassState += 0.32 * (rawNoise - lowPassState);

            var attack = Math.Min(1.0, i / 32.0);
            var envelope = attack * Math.Exp(-decayRate * t);
            var mixed = ((1.0 - noiseMix) * tone) + (noiseMix * lowPassState);
            var scaled = Math.Clamp(mixed * envelope * 26_000.0, short.MinValue, short.MaxValue);

            samples[i] = (short)scaled;
        }

        return new PcmSoundClip(cue, DefaultSampleRate, samples);
    }
}
