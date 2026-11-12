using System.Collections.Frozen;
using System.IO.Compression;

namespace OverHorizon.Audio;

/// <summary>
/// Builds the OpenAL sound bank exclusively from real recorded <c>.ogg</c> and <c>.wav</c> audio:
/// embedded CC0 Public-Domain foley recordings by default, automatically overridden by external
/// <c>minecraft-assets</c> (<c>23w13a__or__b</c>) or user resource-pack <c>.ogg</c>/<c>.wav</c> files.
/// </summary>
public static class ProceduralSoundBank
{
    private static readonly (SoundCue Cue, string ResourceName)[] EmbeddedRealClips =
    [
        (SoundCue.FootstepStone, "OverHorizon.Audio.EmbeddedSfx.footstep_stone.ogg"),
        (SoundCue.FootstepGrass, "OverHorizon.Audio.EmbeddedSfx.footstep_grass.ogg"),
        (SoundCue.FootstepWood, "OverHorizon.Audio.EmbeddedSfx.footstep_wood.ogg"),
        (SoundCue.BlockBreak, "OverHorizon.Audio.EmbeddedSfx.block_break.ogg"),
        (SoundCue.BlockPlace, "OverHorizon.Audio.EmbeddedSfx.block_place.ogg"),
        (SoundCue.WaterSplash, "OverHorizon.Audio.EmbeddedSfx.water_splash.wav"),
        (SoundCue.UiClick, "OverHorizon.Audio.EmbeddedSfx.ui_click.ogg"),
        (SoundCue.SwordSwing, "OverHorizon.Audio.EmbeddedSfx.sword_swing.wav"),
        (SoundCue.PlayerHit, "OverHorizon.Audio.EmbeddedSfx.player_hit.ogg"),
        (SoundCue.PlayerHurt, "OverHorizon.Audio.EmbeddedSfx.player_hurt.ogg"),
        (SoundCue.EatFood, "OverHorizon.Audio.EmbeddedSfx.eat_food.ogg"),
        (SoundCue.ItemPop, "OverHorizon.Audio.EmbeddedSfx.item_pop.wav"),
        (SoundCue.UnderwaterEnter, "OverHorizon.Audio.EmbeddedSfx.underwater_enter.wav"),
    ];

    /// <summary>
    /// Creates a frozen bank containing real decoded <see cref="PcmSoundClip"/>s for every <see cref="SoundCue"/>,
    /// overriding defaults with <c>.ogg</c> or <c>.wav</c> files from <paramref name="externalAssetsPath"/> or <c>./resourcepacks</c>.
    /// </summary>
    /// <param name="externalAssetsPath">Optional resource pack directory, <c>.zip</c>, or <c>.jar</c> path.</param>
    /// <returns>Frozen lookup dictionary from <see cref="SoundCue"/> to <see cref="PcmSoundClip"/>.</returns>
    public static FrozenDictionary<SoundCue, PcmSoundClip> CreateDefaultBank(string? externalAssetsPath = null)
    {
        var bank = new Dictionary<SoundCue, PcmSoundClip>();
        var asm = typeof(ProceduralSoundBank).Assembly;

        for (var i = 0; i < EmbeddedRealClips.Length; i++)
        {
            var (cue, resourceName) = EmbeddedRealClips[i];
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                continue;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var bytes = ms.ToArray();

            if (resourceName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) &&
                OggVorbisDecoder.TryDecode(cue, bytes, out var oggClip))
            {
                bank[cue] = oggClip;
            }
            else if (WavPcmDecoder.TryDecode(cue, bytes, out var wavClip))
            {
                bank[cue] = wavClip;
            }
        }

        ApplyExternalAudioOverrides(bank, externalAssetsPath);
        return bank.ToFrozenDictionary();
    }

    private static void ApplyExternalAudioOverrides(Dictionary<SoundCue, PcmSoundClip> bank, string? externalAssetsPath)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(externalAssetsPath))
        {
            candidates.Add(externalAssetsPath);
        }

        foreach (var baseDir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            candidates.Add(Path.Combine(baseDir, "resourcepacks"));
            candidates.Add(Path.Combine(baseDir, "assets"));
            candidates.Add(Path.Combine(baseDir, "minecraft-assets"));
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var full = Path.GetFullPath(candidate);
            if (!visited.Add(full))
            {
                continue;
            }

            if (File.Exists(full))
            {
                TryLoadFromArchive(bank, full);
            }
            else if (Directory.Exists(full))
            {
                TryLoadFromDirectory(bank, full);
                var files = Directory.GetFiles(full);
                for (var f = 0; f < files.Length; f++)
                {
                    var ext = Path.GetExtension(files[f]);
                    if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ext, ".jar", StringComparison.OrdinalIgnoreCase))
                    {
                        TryLoadFromArchive(bank, files[f]);
                    }
                }
            }
        }
    }

    private static void TryLoadFromDirectory(Dictionary<SoundCue, PcmSoundClip> bank, string dir)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                var isOgg = string.Equals(ext, ".ogg", StringComparison.OrdinalIgnoreCase);
                var isWav = string.Equals(ext, ".wav", StringComparison.OrdinalIgnoreCase);
                if (!isOgg && !isWav)
                {
                    continue;
                }

                if (!TryMapPathToCue(file.Replace('\\', '/'), out var cue))
                {
                    continue;
                }

                var bytes = File.ReadAllBytes(file);
                if (isOgg && OggVorbisDecoder.TryDecode(cue, bytes, out var oggClip))
                {
                    bank[cue] = oggClip;
                }
                else if (isWav && WavPcmDecoder.TryDecode(cue, bytes, out var wavClip))
                {
                    bank[cue] = wavClip;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore unreadable external directories gracefully.
        }
    }

    private static void TryLoadFromArchive(Dictionary<SoundCue, PcmSoundClip> bank, string archivePath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            foreach (var entry in zip.Entries)
            {
                if (entry.Length is <= 32 or > 4_194_304)
                {
                    continue;
                }

                var norm = entry.FullName.Replace('\\', '/');
                var isOgg = norm.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase);
                var isWav = norm.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
                if ((!isOgg && !isWav) || !TryMapPathToCue(norm, out var cue))
                {
                    continue;
                }

                var buf = new byte[entry.Length];
                using (var s = entry.Open())
                {
                    var total = 0;
                    while (total < buf.Length)
                    {
                        var n = s.Read(buf, total, buf.Length - total);
                        if (n <= 0)
                        {
                            break;
                        }

                        total += n;
                    }
                }

                if (isOgg && OggVorbisDecoder.TryDecode(cue, buf, out var oggClip))
                {
                    bank[cue] = oggClip;
                }
                else if (isWav && WavPcmDecoder.TryDecode(cue, buf, out var wavClip))
                {
                    bank[cue] = wavClip;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // Ignore corrupted external archives gracefully.
        }
    }

    private static bool TryMapPathToCue(string normalizedPath, out SoundCue cue)
    {
        var stem = Path.GetFileNameWithoutExtension(normalizedPath).ToUpperInvariant();
        var inStep = normalizedPath.Contains("/step/", StringComparison.OrdinalIgnoreCase);
        var inDig = normalizedPath.Contains("/dig/", StringComparison.OrdinalIgnoreCase);

        if (inStep)
        {
            if (stem.StartsWith("STONE", StringComparison.Ordinal))
            {
                cue = SoundCue.FootstepStone;
                return true;
            }

            if (stem.StartsWith("GRASS", StringComparison.Ordinal))
            {
                cue = SoundCue.FootstepGrass;
                return true;
            }

            if (stem.StartsWith("WOOD", StringComparison.Ordinal))
            {
                cue = SoundCue.FootstepWood;
                return true;
            }
        }

        if (inDig)
        {
            if (stem.StartsWith("STONE", StringComparison.Ordinal) || stem.StartsWith("GRAVEL", StringComparison.Ordinal))
            {
                cue = SoundCue.BlockBreak;
                return true;
            }

            if (stem.StartsWith("WOOD", StringComparison.Ordinal))
            {
                cue = SoundCue.BlockPlace;
                return true;
            }
        }

        switch (stem)
        {
            case "STONE1" or "FOOTSTEP_STONE":
                cue = SoundCue.FootstepStone;
                return true;
            case "GRASS1" or "FOOTSTEP_GRASS":
                cue = SoundCue.FootstepGrass;
                return true;
            case "WOOD1" or "FOOTSTEP_WOOD":
                cue = SoundCue.FootstepWood;
                return true;
            case "BREAK" or "BLOCK_BREAK":
                cue = SoundCue.BlockBreak;
                return true;
            case "BLOCK_PLACE":
                cue = SoundCue.BlockPlace;
                return true;
            case "SPLASH" or "SPLASH1" or "WATER_SPLASH":
                cue = SoundCue.WaterSplash;
                return true;
            case "CLICK" or "UI_CLICK":
                cue = SoundCue.UiClick;
                return true;
            case "SWEEP1" or "SWING" or "SWORD_SWING":
                cue = SoundCue.SwordSwing;
                return true;
            case "STRONG1" or "CRIT1" or "PLAYER_HIT":
                cue = SoundCue.PlayerHit;
                return true;
            case "HIT1" or "CLASSIC_HURT" or "FALLSMALL" or "PLAYER_HURT":
                cue = SoundCue.PlayerHurt;
                return true;
            case "EAT1" or "EAT2" or "EAT_FOOD":
                cue = SoundCue.EatFood;
                return true;
            case "POP" or "ORB" or "ITEM_POP":
                cue = SoundCue.ItemPop;
                return true;
            case "ENTER1" or "UNDERWATER_ENTER":
                cue = SoundCue.UnderwaterEnter;
                return true;
            default:
                cue = default;
                return false;
        }
    }
}
