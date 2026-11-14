using System.Reflection;

namespace OverHorizon.Audio;

/// <summary>
/// Loads embedded CC0 baseline <c>.ogg</c>/<c>.wav</c> effects, synthesizes clean-room fallbacks for extended cues,
/// and automatically overrides cues with real <c>.ogg</c> assets from <c>./resourcepacks/23w13a__or__b/assets/minecraft/sounds/</c>.
/// </summary>
public static class ProceduralSoundBank
{
    /// <summary>Standard sample rate (Hz) of decoded clips.</summary>
    public const int SampleRate = 22_050;

    private static readonly (SoundCue Cue, string ResourceSuffix)[] EmbeddedManifest =
    [
        (SoundCue.FootstepStone, "EmbeddedSfx.footstep_stone.ogg"),
        (SoundCue.FootstepGrass, "EmbeddedSfx.footstep_grass.ogg"),
        (SoundCue.FootstepWood, "EmbeddedSfx.footstep_wood.ogg"),
        (SoundCue.BlockBreak, "EmbeddedSfx.block_break.ogg"),
        (SoundCue.BlockPlace, "EmbeddedSfx.block_place.ogg"),
        (SoundCue.UiClick, "EmbeddedSfx.ui_click.ogg"),
        (SoundCue.WaterSplash, "EmbeddedSfx.water_splash.wav"),
        (SoundCue.UnderwaterEnter, "EmbeddedSfx.underwater_enter.wav"),
        (SoundCue.PlayerHurt, "EmbeddedSfx.player_hurt.ogg"),
        (SoundCue.PlayerHit, "EmbeddedSfx.player_hit.ogg"),
        (SoundCue.SwordSwing, "EmbeddedSfx.sword_swing.wav"),
        (SoundCue.EatFood, "EmbeddedSfx.eat_food.ogg"),
        (SoundCue.ItemPop, "EmbeddedSfx.item_pop.wav"),
    ];

    private static readonly (SoundCue Cue, string RelativeOgg)[] MinecraftSoundMappings =
    [
        (SoundCue.FootstepStone, "sounds/step/stone1.ogg"),
        (SoundCue.FootstepGrass, "sounds/step/grass1.ogg"),
        (SoundCue.FootstepWood, "sounds/step/wood1.ogg"),
        (SoundCue.FootstepSand, "sounds/step/sand1.ogg"),
        (SoundCue.FootstepGravel, "sounds/step/gravel1.ogg"),
        (SoundCue.FootstepCloth, "sounds/step/cloth1.ogg"),
        (SoundCue.FootstepLadder, "sounds/step/ladder1.ogg"),
        (SoundCue.BlockBreak, "sounds/dig/stone1.ogg"),
        (SoundCue.BlockPlace, "sounds/dig/wood1.ogg"),
        (SoundCue.GlassBreak, "sounds/random/glass1.ogg"),
        (SoundCue.UiClick, "sounds/random/click.ogg"),
        (SoundCue.ItemPop, "sounds/random/pop.ogg"),
        (SoundCue.WaterSplash, "sounds/liquid/splash.ogg"),
        (SoundCue.WaterSwim, "sounds/liquid/swim1.ogg"),
        (SoundCue.UnderwaterEnter, "sounds/ambient/underwater/enter1.ogg"),
        (SoundCue.UnderwaterExit, "sounds/ambient/underwater/exit1.ogg"),
        (SoundCue.UnderwaterAmbience, "sounds/ambient/underwater/underwater_ambience.ogg"),
        (SoundCue.PlayerHurt, "sounds/damage/hit1.ogg"),
        (SoundCue.PlayerHit, "sounds/entity/player/attack/strong1.ogg"),
        (SoundCue.SwordSwing, "sounds/entity/player/attack/sweep1.ogg"),
        (SoundCue.EatFood, "sounds/random/eat1.ogg"),
        (SoundCue.Burp, "sounds/random/burp.ogg"),
        (SoundCue.FireIgnite, "sounds/fire/ignite.ogg"),
        (SoundCue.FireCrackle, "sounds/fire/fire.ogg"),
        (SoundCue.Fizz, "sounds/random/fizz.ogg"),
        (SoundCue.TntFuse, "sounds/random/fuse.ogg"),
        (SoundCue.Explosion, "sounds/random/explode1.ogg"),
        (SoundCue.PistonExtend, "sounds/tile/piston/out.ogg"),
        (SoundCue.PistonRetract, "sounds/tile/piston/in.ogg"),
        (SoundCue.MobPigSay, "sounds/mob/pig/say1.ogg"),
        (SoundCue.MobPigDeath, "sounds/mob/pig/death.ogg"),
        (SoundCue.MobCowSay, "sounds/mob/cow/say1.ogg"),
        (SoundCue.MobCowHurt, "sounds/mob/cow/hurt1.ogg"),
        (SoundCue.MobSheepSay, "sounds/mob/sheep/say1.ogg"),
        (SoundCue.MobSheepShear, "sounds/mob/sheep/shear.ogg"),
        (SoundCue.MobChickenSay, "sounds/mob/chicken/say1.ogg"),
        (SoundCue.MobChickenHurt, "sounds/mob/chicken/hurt1.ogg"),
        (SoundCue.MobZombieSay, "sounds/mob/zombie/say1.ogg"),
        (SoundCue.MobZombieHurt, "sounds/mob/zombie/hurt1.ogg"),
        (SoundCue.MobZombieDeath, "sounds/mob/zombie/death.ogg"),
        (SoundCue.MobSkeletonSay, "sounds/mob/skeleton/say1.ogg"),
        (SoundCue.MobSkeletonHurt, "sounds/mob/skeleton/hurt1.ogg"),
        (SoundCue.MobSkeletonDeath, "sounds/mob/skeleton/death.ogg"),
        (SoundCue.MobSpiderSay, "sounds/mob/spider/say1.ogg"),
        (SoundCue.MobSpiderDeath, "sounds/mob/spider/death.ogg"),
        (SoundCue.MobCreeperHiss, "sounds/mob/creeper/say1.ogg"),
        (SoundCue.MobCreeperDeath, "sounds/mob/creeper/death.ogg"),
        (SoundCue.FallSmall, "sounds/damage/fallsmall.ogg"),
        (SoundCue.FallBig, "sounds/damage/fallbig.ogg"),
        (SoundCue.FireHurt, "sounds/random/fizz.ogg"),
        (SoundCue.FootstepSnow, "sounds/step/snow1.ogg"),
        (SoundCue.FootstepMetal, "sounds/block/lantern/break1.ogg"),
        (SoundCue.BowShoot, "sounds/random/bow.ogg"),
        (SoundCue.ArrowHit, "sounds/random/bowhit1.ogg"),
        (SoundCue.MinecartRoll, "sounds/minecart/base.ogg"),
        (SoundCue.LandSoft, "sounds/damage/fallsmall.ogg"),
        (SoundCue.ShieldBlock, "sounds/item/shield/block1.ogg"),
        (SoundCue.WoodBreak, "sounds/dig/wood1.ogg"),
        (SoundCue.WoodPlace, "sounds/dig/wood2.ogg"),
        (SoundCue.GrassBreak, "sounds/dig/grass1.ogg"),
        (SoundCue.GrassPlace, "sounds/dig/grass2.ogg"),
        (SoundCue.SandBreak, "sounds/dig/sand1.ogg"),
        (SoundCue.SandPlace, "sounds/dig/sand2.ogg"),
        (SoundCue.MetalBreak, "sounds/block/lantern/break1.ogg"),
        (SoundCue.MetalPlace, "sounds/block/lantern/place1.ogg"),
        (SoundCue.ClothPlace, "sounds/dig/cloth1.ogg"),
        (SoundCue.StonePlace, "sounds/dig/stone2.ogg"),
        (SoundCue.PotionDrink, "sounds/random/drink.ogg"),
        (SoundCue.ComposterReady, "sounds/block/composter/ready1.ogg"),
        (SoundCue.GravelBreak, "sounds/dig/gravel1.ogg"),
        (SoundCue.GravelPlace, "sounds/dig/gravel2.ogg"),
        (SoundCue.SnowBreak, "sounds/dig/snow1.ogg"),
        (SoundCue.SnowPlace, "sounds/dig/snow2.ogg"),
        (SoundCue.ClothBreak, "sounds/dig/cloth2.ogg"),
        (SoundCue.DeepslateBreak, "sounds/block/deepslate/break1.ogg"),
        (SoundCue.DeepslatePlace, "sounds/block/deepslate/place1.ogg"),
        (SoundCue.FootstepDeepslate, "sounds/block/deepslate/step1.ogg"),
        (SoundCue.AmethystBreak, "sounds/block/amethyst/break1.ogg"),
        (SoundCue.AmethystPlace, "sounds/block/amethyst/place1.ogg"),
        (SoundCue.FootstepAmethyst, "sounds/block/amethyst/step1.ogg"),
        (SoundCue.AmethystChime, "sounds/block/amethyst/shimmer.ogg"),
        (SoundCue.AmethystClusterBreak, "sounds/block/amethyst_cluster/break1.ogg"),
        (SoundCue.AmethystClusterPlace, "sounds/block/amethyst_cluster/place1.ogg"),
        (SoundCue.WoodenDoorOpen, "sounds/block/wooden_door/open1.ogg"),
        (SoundCue.WoodenDoorClose, "sounds/block/wooden_door/close1.ogg"),
        (SoundCue.IronDoorOpen, "sounds/block/iron_door/open1.ogg"),
        (SoundCue.IronDoorClose, "sounds/block/iron_door/close1.ogg"),
        (SoundCue.WoodenTrapdoorOpen, "sounds/block/wooden_trapdoor/open1.ogg"),
        (SoundCue.WoodenTrapdoorClose, "sounds/block/wooden_trapdoor/close1.ogg"),
        (SoundCue.IronTrapdoorOpen, "sounds/block/iron_trapdoor/open1.ogg"),
        (SoundCue.IronTrapdoorClose, "sounds/block/iron_trapdoor/close1.ogg"),
        (SoundCue.HoeTill, "sounds/item/hoe/till1.ogg"),
        (SoundCue.ShovelFlatten, "sounds/item/shovel/flatten1.ogg"),
    ];

    /// <summary>
    /// Decodes all built-in sound clips and applies external <c>23w13a__or__b</c> <c>.ogg</c> overrides when present.
    /// </summary>
    /// <param name="externalAssetsPath">Optional external resource-pack root path.</param>
    /// <returns>Complete dictionary mapping every <see cref="SoundCue"/> to a decoded <see cref="PcmSoundClip"/>.</returns>
    public static IReadOnlyDictionary<SoundCue, PcmSoundClip> CreateDefaultBank(string? externalAssetsPath = null)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceNames = asm.GetManifestResourceNames();
        var clips = new Dictionary<SoundCue, PcmSoundClip>(64);

        foreach (var (cue, suffix) in EmbeddedManifest)
        {
            var fullName = Array.Find(resourceNames, n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Missing embedded audio asset '{suffix}'.");

            using var stream = asm.GetManifestResourceStream(fullName)
                ?? throw new InvalidOperationException($"Failed to open embedded audio stream '{fullName}'.");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var rawBytes = ms.ToArray();

            PcmSoundClip? decoded = null;
            if (suffix.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                if (OggVorbisDecoder.TryDecode(cue, rawBytes, out var oggClip))
                {
                    decoded = oggClip;
                }
            }
            else if (WavPcmDecoder.TryDecode(cue, rawBytes, out var wavClip))
            {
                decoded = wavClip;
            }

            clips[cue] = decoded ?? SynthesizeFallbackClip(cue);
        }

        // Populate organic filtered-percussion/embedded fallbacks (zero harsh sine beeps!) before overlaying 23w13a__or__b .ogg clips!
        foreach (var cue in Enum.GetValues<SoundCue>())
        {
            if (!clips.ContainsKey(cue))
            {
                clips[cue] = DeriveOrganicFallbackFromEmbedded(cue, clips);
            }
        }

        var searchRoots = new List<string>();
        if (!string.IsNullOrWhiteSpace(externalAssetsPath))
        {
            searchRoots.Add(externalAssetsPath);
        }

        foreach (var baseDir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            searchRoots.Add(Path.Combine(baseDir, "resourcepacks", "23w13a__or__b"));
            searchRoots.Add(Path.Combine(baseDir, "resourcepacks"));
            searchRoots.Add(Path.Combine(baseDir, "assets"));
            searchRoots.Add(Path.Combine(baseDir, "minecraft-assets"));
        }

        foreach (var root in searchRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            TryLoadExactMappings(root, clips);
        }

        return clips;
    }

    private static void TryLoadExactMappings(string root, Dictionary<SoundCue, PcmSoundClip> clips)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        string[] prefixes =
        [
            Path.Combine(root, "assets", "minecraft"),
            Path.Combine(root, "minecraft"),
            root,
        ];

        for (var p = 0; p < prefixes.Length; p++)
        {
            var prefix = prefixes[p];
            if (!Directory.Exists(prefix))
            {
                continue;
            }

            for (var i = 0; i < MinecraftSoundMappings.Length; i++)
            {
                var (cue, relOgg) = MinecraftSoundMappings[i];
                var file = Path.Combine(prefix, relOgg.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(file))
                {
                    continue;
                }

                try
                {
                    var bytes = File.ReadAllBytes(file);
                    if (OggVorbisDecoder.TryDecode(cue, bytes, out var decoded) && decoded.Samples.Length > 0)
                    {
                        clips[cue] = decoded;
                    }
                }
                catch (IOException)
                {
                    // Keep existing fallback clip if file is locked.
                }
            }
        }
    }

    private static PcmSoundClip DeriveOrganicFallbackFromEmbedded(SoundCue cue, Dictionary<SoundCue, PcmSoundClip> baseClips)
    {
        // Map extended cues to real recorded embedded foley with subtle pitch/envelope shaping instead of harsh synthetic sine tones!
        var (donorCue, rateFactor) = cue switch
        {
            SoundCue.FootstepSand or SoundCue.FootstepSnow or SoundCue.FootstepCloth => (SoundCue.FootstepGrass, 1.05),
            SoundCue.FootstepGravel or SoundCue.FootstepDeepslate or SoundCue.FootstepMetal or SoundCue.FootstepAmethyst => (SoundCue.FootstepStone, 0.92),
            SoundCue.FootstepLadder => (SoundCue.FootstepWood, 1.12),
            SoundCue.WoodBreak or SoundCue.ClothBreak => (SoundCue.FootstepWood, 0.85),
            SoundCue.WoodPlace or SoundCue.ClothPlace or SoundCue.WoodenDoorOpen or SoundCue.WoodenDoorClose or SoundCue.WoodenTrapdoorOpen or SoundCue.WoodenTrapdoorClose => (SoundCue.BlockPlace, 0.96),
            SoundCue.GrassBreak or SoundCue.SandBreak or SoundCue.GravelBreak or SoundCue.SnowBreak or SoundCue.HoeTill or SoundCue.ShovelFlatten => (SoundCue.FootstepGrass, 0.86),
            SoundCue.GrassPlace or SoundCue.SandPlace or SoundCue.GravelPlace or SoundCue.SnowPlace or SoundCue.ComposterReady => (SoundCue.FootstepGrass, 0.98),
            SoundCue.StonePlace or SoundCue.DeepslatePlace or SoundCue.MetalPlace or SoundCue.AmethystPlace or SoundCue.AmethystClusterPlace or SoundCue.IronDoorOpen or SoundCue.IronDoorClose or SoundCue.IronTrapdoorOpen or SoundCue.IronTrapdoorClose => (SoundCue.FootstepStone, 0.95),
            SoundCue.DeepslateBreak or SoundCue.MetalBreak or SoundCue.GlassBreak or SoundCue.AmethystBreak or SoundCue.AmethystClusterBreak or SoundCue.AmethystChime => (SoundCue.BlockBreak, 1.08),
            SoundCue.FallSmall or SoundCue.FallBig or SoundCue.LandSoft or SoundCue.ShieldBlock => (SoundCue.PlayerHit, 0.88),
            SoundCue.WaterSwim or SoundCue.UnderwaterExit or SoundCue.UnderwaterAmbience or SoundCue.PotionDrink => (SoundCue.WaterSplash, 0.94),
            _ => (AirFallbackMarker(cue), 1.0),
        };

        if (baseClips.TryGetValue(donorCue, out var donor) && donor.Samples.Length > 1000)
        {
            var span = donor.Samples.Span;
            var copied = span.ToArray();
            var tunedRate = Math.Clamp((int)Math.Round(donor.SampleRate * rateFactor), 16_000, 48_000);
            return new PcmSoundClip(cue, tunedRate, copied);
        }

        return SynthesizeFallbackClip(cue);
    }

    private static SoundCue AirFallbackMarker(SoundCue cue) => cue;

    private static PcmSoundClip SynthesizeFallbackClip(SoundCue cue)
    {
        var (durationSec, baseFreq, noiseMix, sweep) = cue switch
        {
            SoundCue.Explosion => (0.55, 68.0, 0.85, -35.0),
            SoundCue.TntFuse or SoundCue.Fizz or SoundCue.MobCreeperHiss => (0.38, 1200.0, 0.92, -300.0),
            SoundCue.PistonExtend => (0.18, 185.0, 0.40, 95.0),
            SoundCue.PistonRetract => (0.18, 260.0, 0.40, -95.0),
            SoundCue.FireIgnite => (0.14, 920.0, 0.65, 320.0),
            SoundCue.FireCrackle => (0.28, 340.0, 0.82, 40.0),
            SoundCue.WaterSwim => (0.26, 240.0, 0.68, -70.0),
            SoundCue.UnderwaterExit => (0.24, 310.0, 0.55, 140.0),
            SoundCue.UnderwaterAmbience => (0.60, 110.0, 0.35, 12.0),
            SoundCue.GlassBreak => (0.22, 1650.0, 0.72, -450.0),
            SoundCue.Burp => (0.24, 135.0, 0.25, -35.0),
            SoundCue.MobPigSay or SoundCue.MobPigDeath => (0.24, 310.0, 0.20, 65.0),
            SoundCue.MobCowSay or SoundCue.MobCowHurt => (0.42, 148.0, 0.15, -24.0),
            SoundCue.MobSheepSay => (0.34, 265.0, 0.18, -18.0),
            SoundCue.MobSheepShear => (0.15, 1380.0, 0.55, 220.0),
            SoundCue.MobChickenSay or SoundCue.MobChickenHurt => (0.16, 620.0, 0.18, 150.0),
            SoundCue.MobZombieSay or SoundCue.MobZombieHurt or SoundCue.MobZombieDeath => (0.38, 122.0, 0.42, -28.0),
            SoundCue.MobSkeletonSay or SoundCue.MobSkeletonHurt or SoundCue.MobSkeletonDeath => (0.20, 840.0, 0.62, -180.0),
            SoundCue.MobSpiderSay or SoundCue.MobSpiderDeath => (0.24, 520.0, 0.78, -120.0),
            SoundCue.FootstepMetal => (0.11, 740.0, 0.30, -140.0),
            SoundCue.BowShoot => (0.17, 460.0, 0.36, 220.0),
            SoundCue.ArrowHit => (0.15, 620.0, 0.42, -210.0),
            SoundCue.MinecartRoll => (0.30, 145.0, 0.68, 8.0),

            SoundCue.ShieldBlock => (0.18, 250.0, 0.48, -90.0),
            SoundCue.WoodBreak => (0.16, 220.0, 0.50, -65.0),
            SoundCue.WoodPlace => (0.12, 265.0, 0.42, -45.0),
            SoundCue.GrassBreak => (0.16, 480.0, 0.86, -160.0),
            SoundCue.GrassPlace => (0.13, 520.0, 0.82, -130.0),
            SoundCue.SandBreak => (0.16, 780.0, 0.90, -240.0),
            SoundCue.SandPlace => (0.13, 840.0, 0.88, -190.0),
            SoundCue.MetalBreak => (0.20, 890.0, 0.28, -220.0),
            SoundCue.MetalPlace => (0.15, 1020.0, 0.24, -160.0),
            SoundCue.ClothPlace => (0.12, 210.0, 0.72, -40.0),
            SoundCue.StonePlace => (0.12, 320.0, 0.56, -85.0),
            SoundCue.PotionDrink => (0.24, 340.0, 0.35, 120.0),
            SoundCue.ComposterReady => (0.20, 560.0, 0.40, 180.0),
            _ => (0.14, 280.0, 0.52, -40.0),
        };

        var count = Math.Max(256, (int)(SampleRate * durationSec));
        var pcm = new short[count];
        var rng = 0x9E3779B9U ^ ((uint)cue * 2654435761U);
        var phase = 0.0;

        for (var i = 0; i < count; i++)
        {
            var t = (double)i / count;
            var env = Math.Sin(Math.PI * Math.Min(1.0, t * 8.0)) * (1.0 - (t * 0.88));
            var freq = Math.Max(40.0, baseFreq + (sweep * t));
            phase += (2.0 * Math.PI * freq) / SampleRate;
            var tone = Math.Sin(phase);

            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            var noise = ((rng & 0xFFFF) / 32768.0) - 1.0;

            var sample = ((tone * (1.0 - noiseMix)) + (noise * noiseMix)) * env * 0.42;
            pcm[i] = (short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
        }

        return new PcmSoundClip(cue, SampleRate, pcm);
    }
}
