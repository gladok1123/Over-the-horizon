using System.IO.Compression;
using OverHorizon.Shared.Random;

namespace OverHorizon.Audio;

/// <summary>
/// Multi-layered physical-modeling 44.1 kHz foley sound bank with automatic <c>.wav</c> override loading
/// from <c>./assets</c>, <c>./minecraft-assets</c>, or <c>./resourcepacks</c> (<c>.zip</c>, <c>.jar</c>, or directory).
/// </summary>
public static class ProceduralSoundBank
{
    /// <summary>Default synthesis sample rate in Hz (<c>44,100</c>).</summary>
    public const int DefaultSampleRate = 44_100;

    /// <summary>
    /// Creates the sound bank, loading external <c>.wav</c> sound overrides if present and synthesizing
    /// realistic multi-layered physical-modeling foley waveforms for all remaining cues.
    /// </summary>
    /// <param name="externalAssetsPath">Optional explicit directory, <c>.zip</c>, or <c>.jar</c> path.</param>
    /// <returns>Dictionary mapping each <see cref="SoundCue"/> to its <see cref="PcmSoundClip"/>.</returns>
    public static IReadOnlyDictionary<SoundCue, PcmSoundClip> CreateDefaultBank(string? externalAssetsPath = null)
    {
        var cues = Enum.GetValues<SoundCue>();
        var bank = new Dictionary<SoundCue, PcmSoundClip>(cues.Length);
        foreach (var cue in cues)
        {
            bank[cue] = Synthesize(cue);
        }

        ApplyExternalWavOverrides(bank, externalAssetsPath);
        return bank;
    }

    /// <summary>
    /// Synthesizes a multi-layered 44.1 kHz 16-bit mono physical-modeling foley clip for <paramref name="cue"/>.
    /// </summary>
    /// <param name="cue">Sound cue to synthesize.</param>
    /// <returns>Synthesized <see cref="PcmSoundClip"/>.</returns>
    public static PcmSoundClip Synthesize(SoundCue cue) => cue switch
    {
        SoundCue.FootstepStone => SynthesizeStoneStep(),
        SoundCue.FootstepGrass => SynthesizeGrassStep(),
        SoundCue.FootstepWood => SynthesizeWoodStep(),
        SoundCue.BlockBreak => SynthesizeBlockBreak(),
        SoundCue.BlockPlace => SynthesizeBlockPlace(),
        SoundCue.WaterSplash => SynthesizeWaterSplash(),
        _ => SynthesizeUiClick(),
    };

    private static PcmSoundClip SynthesizeStoneStep()
    {
        var count = (int)(DefaultSampleRate * 0.115);
        var buf = new short[count];
        var rng = new Xoroshiro128PlusPlus(0x5704_E101UL);
        var lp = 0.0;
        var phase = 0.0;
        var tapPhase = 0.0;

        for (var i = 0; i < count; i++)
        {
            var t = (double)i / DefaultSampleRate;
            var thudFreq = 195.0 * Math.Exp(-22.0 * t) + 58.0;
            phase += (2.0 * Math.PI * thudFreq) / DefaultSampleRate;
            tapPhase += (2.0 * Math.PI * 880.0) / DefaultSampleRate;

            var noise = (rng.NextDouble() * 2.0) - 1.0;
            lp += 0.36 * (noise - lp);
            var grit = (noise - lp) * Math.Exp(-42.0 * t);
            var heel = Math.Sin(phase) * Math.Exp(-28.0 * t);
            var mineralTap = Math.Sin(tapPhase) * Math.Exp(-95.0 * t) * 0.35;
            var secondScuff = t > 0.038 ? lp * Math.Exp(-48.0 * (t - 0.038)) * 0.42 : 0.0;

            var mix = (heel * 0.52) + (grit * 0.55) + mineralTap + secondScuff;
            buf[i] = ToPcm16(mix * Math.Min(1.0, i / 28.0));
        }

        return new PcmSoundClip(SoundCue.FootstepStone, DefaultSampleRate, buf);
    }

    private static PcmSoundClip SynthesizeGrassStep()
    {
        var count = (int)(DefaultSampleRate * 0.145);
        var buf = new short[count];
        var rng = new Xoroshiro128PlusPlus(0x6BA5_5202UL);
        var lpSlow = 0.0;
        var lpFast = 0.0;
        var thudPhase = 0.0;

        for (var i = 0; i < count; i++)
        {
            var t = (double)i / DefaultSampleRate;
            thudPhase += (2.0 * Math.PI * (96.0 * Math.Exp(-18.0 * t) + 42.0)) / DefaultSampleRate;

            var raw = (rng.NextDouble() * 2.0) - 1.0;
            lpFast += 0.55 * (raw - lpFast);
            lpSlow += 0.08 * (raw - lpSlow);
            var crispRustle = lpFast - lpSlow;

            var burst1 = Math.Exp(-34.0 * t);
            var burst2 = t > 0.028 ? Math.Exp(-38.0 * (t - 0.028)) * 0.75 : 0.0;
            var burst3 = t > 0.062 ? Math.Exp(-44.0 * (t - 0.062)) * 0.48 : 0.0;
            var earthThud = Math.Sin(thudPhase) * Math.Exp(-32.0 * t) * 0.38;

            var mix = (crispRustle * (burst1 + burst2 + burst3) * 0.68) + earthThud;
            buf[i] = ToPcm16(mix * Math.Min(1.0, i / 40.0));
        }

        return new PcmSoundClip(SoundCue.FootstepGrass, DefaultSampleRate, buf);
    }

    private static PcmSoundClip SynthesizeWoodStep()
    {
        var count = (int)(DefaultSampleRate * 0.110);
        var buf = new short[count];
        var rng = new Xoroshiro128PlusPlus(0x900D_3030UL);
        var p1 = 0.0;
        var p2 = 0.0;
        var p3 = 0.0;
        var lp = 0.0;

        for (var i = 0; i < count; i++)
        {
            var t = (double)i / DefaultSampleRate;
            p1 += (2.0 * Math.PI * 158.0) / DefaultSampleRate;
            p2 += (2.0 * Math.PI * 312.0) / DefaultSampleRate;
            p3 += (2.0 * Math.PI * 498.0) / DefaultSampleRate;

            var noise = (rng.NextDouble() * 2.0) - 1.0;
            lp += 0.25 * (noise - lp);

            var hollow = (Math.Sin(p1) * 0.58 + Math.Sin(p2) * 0.28 + Math.Sin(p3) * 0.14) * Math.Exp(-32.0 * t);
            var knock = lp * Math.Exp(-65.0 * t) * 0.45;
            buf[i] = ToPcm16((hollow + knock) * Math.Min(1.0, i / 20.0));
        }

        return new PcmSoundClip(SoundCue.FootstepWood, DefaultSampleRate, buf);
    }

    private static PcmSoundClip SynthesizeBlockBreak()
    {
        var count = (int)(DefaultSampleRate * 0.240);
        var buf = new short[count];
        var rng = new Xoroshiro128PlusPlus(0xBB4E_A404UL);
        var pLow = 0.0;
        var pMid = 0.0;
        var lp = 0.0;
        ReadOnlySpan<double> cracks = [0.0, 0.036, 0.079, 0.128];

        for (var i = 0; i < count; i++)
        {
            var t = (double)i / DefaultSampleRate;
            pLow += (2.0 * Math.PI * (220.0 * Math.Exp(-14.0 * t) + 55.0)) / DefaultSampleRate;
            pMid += (2.0 * Math.PI * (540.0 * Math.Exp(-18.0 * t) + 140.0)) / DefaultSampleRate;

            var noise = (rng.NextDouble() * 2.0) - 1.0;
            lp += 0.42 * (noise - lp);

            var crackEnv = 0.0;
            for (var c = 0; c < cracks.Length; c++)
            {
                if (t >= cracks[c])
                {
                    crackEnv += Math.Exp(-36.0 * (t - cracks[c])) * (1.0 - (c * 0.16));
                }
            }

            var body = (Math.Sin(pLow) * 0.48 + Math.Sin(pMid) * 0.24) * Math.Exp(-13.0 * t);
            var debris = lp * crackEnv * 0.56;
            buf[i] = ToPcm16((body + debris) * Math.Min(1.0, i / 24.0));
        }

        return new PcmSoundClip(SoundCue.BlockBreak, DefaultSampleRate, buf);
    }

    private static PcmSoundClip SynthesizeBlockPlace()
    {
        var count = (int)(DefaultSampleRate * 0.125);
        var buf = new short[count];
        var rng = new Xoroshiro128PlusPlus(0x91AC_E505UL);
        var p1 = 0.0;
        var pClick = 0.0;
        var lp = 0.0;

        for (var i = 0; i < count; i++)
        {
            var t = (double)i / DefaultSampleRate;
            p1 += (2.0 * Math.PI * (185.0 * Math.Exp(-20.0 * t) + 78.0)) / DefaultSampleRate;
            pClick += (2.0 * Math.PI * 430.0) / DefaultSampleRate;

            var noise = (rng.NextDouble() * 2.0) - 1.0;
            lp += 0.30 * (noise - lp);

            var thock = Math.Sin(p1) * Math.Exp(-24.0 * t) * 0.65;
            var snap = (Math.Sin(pClick) * 0.25 + lp * 0.40) * Math.Exp(-58.0 * t);
            buf[i] = ToPcm16((thock + snap) * Math.Min(1.0, i / 18.0));
        }

        return new PcmSoundClip(SoundCue.BlockPlace, DefaultSampleRate, buf);
    }

    private static PcmSoundClip SynthesizeWaterSplash()
    {
        var count = (int)(DefaultSampleRate * 0.300);
        var buf = new short[count];
        var rng = new Xoroshiro128PlusPlus(0x591A_5606UL);
        var lp = 0.0;
        var bubblePhase = 0.0;

        for (var i = 0; i < count; i++)
        {
            var t = (double)i / DefaultSampleRate;
            var bubbleFreq = 340.0 + (520.0 * ((t * 7.0) % 1.0));
            bubblePhase += (2.0 * Math.PI * bubbleFreq) / DefaultSampleRate;

            var noise = (rng.NextDouble() * 2.0) - 1.0;
            lp += 0.28 * (noise - lp);

            var spray = lp * Math.Exp(-9.5 * t) * 0.68;
            var bubbles = Math.Sin(bubblePhase) * Math.Exp(-11.0 * t) * 0.24;
            buf[i] = ToPcm16((spray + bubbles) * Math.Min(1.0, i / 48.0));
        }

        return new PcmSoundClip(SoundCue.WaterSplash, DefaultSampleRate, buf);
    }

    private static PcmSoundClip SynthesizeUiClick()
    {
        var count = (int)(DefaultSampleRate * 0.036); // 1,587 samples (> 500)
        var buf = new short[count];
        var pHi = 0.0;
        var pLo = 0.0;

        for (var i = 0; i < count; i++)
        {
            var t = (double)i / DefaultSampleRate;
            pHi += (2.0 * Math.PI * 1080.0) / DefaultSampleRate;
            pLo += (2.0 * Math.PI * 540.0) / DefaultSampleRate;
            var env = Math.Exp(-95.0 * t) * Math.Min(1.0, i / 12.0);
            buf[i] = ToPcm16((Math.Sin(pHi) * 0.55 + Math.Sin(pLo) * 0.45) * env);
        }

        return new PcmSoundClip(SoundCue.UiClick, DefaultSampleRate, buf);
    }

    private static short ToPcm16(double sample) =>
        (short)Math.Clamp(sample * 28_000.0, short.MinValue, short.MaxValue);

    private static void ApplyExternalWavOverrides(Dictionary<SoundCue, PcmSoundClip> bank, string? explicitPath)
    {
        var candidates = new List<string>(6);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            candidates.Add(explicitPath);
        }

        foreach (var baseDir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            candidates.Add(Path.Combine(baseDir, "assets"));
            candidates.Add(Path.Combine(baseDir, "resourcepacks"));
        }

        foreach (var path in candidates)
        {
            if (Directory.Exists(path))
            {
                TryLoadWavsFromDirectory(bank, path);
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    var ext = Path.GetExtension(file);
                    if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ext, ".jar", StringComparison.OrdinalIgnoreCase))
                    {
                        TryLoadWavsFromArchive(bank, file);
                    }
                }
            }
            else if (File.Exists(path))
            {
                TryLoadWavsFromArchive(bank, path);
            }
        }
    }

    private static void TryLoadWavsFromDirectory(Dictionary<SoundCue, PcmSoundClip> bank, string dir)
    {
        foreach (var wavFile in Directory.EnumerateFiles(dir, "*.wav", SearchOption.AllDirectories))
        {
            if (TryMapStemToCue(Path.GetFileNameWithoutExtension(wavFile), out var cue) &&
                WavPcmDecoder.TryDecode(cue, File.ReadAllBytes(wavFile), out var clip))
            {
                bank[cue] = clip;
            }
        }
    }

    private static void TryLoadWavsFromArchive(Dictionary<SoundCue, PcmSoundClip> bank, string archivePath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            foreach (var entry in zip.Entries)
            {
                if (!entry.FullName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || entry.Length is <= 44 or > 8_388_608)
                {
                    continue;
                }

                if (!TryMapStemToCue(Path.GetFileNameWithoutExtension(entry.FullName), out var cue))
                {
                    continue;
                }

                var bytes = new byte[entry.Length];
                using var s = entry.Open();
                if (s.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false) == bytes.Length &&
                    WavPcmDecoder.TryDecode(cue, bytes, out var clip))
                {
                    bank[cue] = clip;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // Ignore unreadable sound archives.
        }
    }

    private static bool TryMapStemToCue(string stem, out SoundCue cue)
    {
        switch (stem.ToUpperInvariant())
        {
            case "STONE" or "STONE1" or "FOOTSTEP_STONE":
                cue = SoundCue.FootstepStone;
                return true;
            case "GRASS" or "GRASS1" or "FOOTSTEP_GRASS":
                cue = SoundCue.FootstepGrass;
                return true;
            case "WOOD" or "WOOD1" or "FOOTSTEP_WOOD":
                cue = SoundCue.FootstepWood;
                return true;
            case "BREAK" or "BLOCK_BREAK" or "DIG_STONE":
                cue = SoundCue.BlockBreak;
                return true;
            case "PLACE" or "BLOCK_PLACE":
                cue = SoundCue.BlockPlace;
                return true;
            case "SPLASH" or "WATER_SPLASH":
                cue = SoundCue.WaterSplash;
                return true;
            case "CLICK" or "UI_CLICK":
                cue = SoundCue.UiClick;
                return true;
            default:
                cue = default;
                return false;
        }
    }
}
