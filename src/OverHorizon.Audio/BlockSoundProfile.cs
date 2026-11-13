using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Audio;

/// <summary>
/// Maps block types to their material-accurate <see cref="SoundCue"/> footstep, landing, break, and placement cues.
/// </summary>
public static class BlockSoundProfile
{
    /// <summary>Resolves the walking/landing footstep sound cue for standing on or climbing <paramref name="blockKey"/>.</summary>
    /// <param name="blockKey">Block resource key.</param>
    public static SoundCue ResolveFootstepCue(ResourceKey blockKey)
    {
        if (blockKey == BuiltInBlocks.Ladder)
        {
            return SoundCue.FootstepLadder;
        }

        if (blockKey == BuiltInBlocks.AmethystBlock || blockKey == BuiltInBlocks.BuddingAmethyst || blockKey == BuiltInBlocks.AmethystCluster)
        {
            return SoundCue.FootstepAmethyst;
        }

        if (IsDeepslate(blockKey))
        {
            return SoundCue.FootstepDeepslate;
        }

        if (IsSnow(blockKey))
        {
            return SoundCue.FootstepSnow;
        }

        if (blockKey == BuiltInBlocks.Sand || blockKey == BuiltInBlocks.RedSand || blockKey == BuiltInBlocks.Sandstone || blockKey == BuiltInBlocks.ChiseledSandstone)
        {
            return SoundCue.FootstepSand;
        }

        if (blockKey == BuiltInBlocks.Gravel)
        {
            return SoundCue.FootstepGravel;
        }

        if (IsCloth(blockKey))
        {
            return SoundCue.FootstepCloth;
        }

        if (IsGrassOrSoil(blockKey))
        {
            return SoundCue.FootstepGrass;
        }

        if (IsWood(blockKey))
        {
            return SoundCue.FootstepWood;
        }

        if (IsMetal(blockKey))
        {
            return SoundCue.FootstepMetal;
        }

        return SoundCue.FootstepStone;
    }

    /// <summary>Resolves the block-break sound cue for <paramref name="blockKey"/>.</summary>
    /// <param name="blockKey">Block resource key.</param>
    public static SoundCue ResolveBreakCue(ResourceKey blockKey)
    {
        if (blockKey == BuiltInBlocks.AmethystCluster)
        {
            return SoundCue.AmethystClusterBreak;
        }

        if (blockKey == BuiltInBlocks.AmethystBlock || blockKey == BuiltInBlocks.BuddingAmethyst)
        {
            return SoundCue.AmethystBreak;
        }

        if (IsGlass(blockKey))
        {
            return SoundCue.GlassBreak;
        }

        if (IsDeepslate(blockKey))
        {
            return SoundCue.DeepslateBreak;
        }

        if (IsWood(blockKey))
        {
            return SoundCue.WoodBreak;
        }

        if (IsGrassOrSoil(blockKey))
        {
            return SoundCue.GrassBreak;
        }

        if (blockKey == BuiltInBlocks.Gravel)
        {
            return SoundCue.GravelBreak;
        }

        if (blockKey == BuiltInBlocks.Sand || blockKey == BuiltInBlocks.RedSand)
        {
            return SoundCue.SandBreak;
        }

        if (IsSnow(blockKey))
        {
            return SoundCue.SnowBreak;
        }

        if (IsCloth(blockKey))
        {
            return SoundCue.ClothBreak;
        }

        if (IsMetal(blockKey))
        {
            return SoundCue.MetalBreak;
        }

        if (blockKey == BuiltInBlocks.Fire)
        {
            return SoundCue.Fizz;
        }

        return SoundCue.BlockBreak;
    }

    /// <summary>Resolves the material-accurate block-placement sound cue for <paramref name="blockKey"/>.</summary>
    /// <param name="blockKey">Block resource key.</param>
    public static SoundCue ResolvePlaceCue(ResourceKey blockKey)
    {
        if (blockKey == BuiltInBlocks.AmethystCluster)
        {
            return SoundCue.AmethystClusterPlace;
        }

        if (blockKey == BuiltInBlocks.AmethystBlock || blockKey == BuiltInBlocks.BuddingAmethyst)
        {
            return SoundCue.AmethystPlace;
        }

        if (IsDeepslate(blockKey))
        {
            return SoundCue.DeepslatePlace;
        }

        if (IsWood(blockKey))
        {
            return SoundCue.WoodPlace;
        }

        if (IsGrassOrSoil(blockKey))
        {
            return SoundCue.GrassPlace;
        }

        if (blockKey == BuiltInBlocks.Gravel)
        {
            return SoundCue.GravelPlace;
        }

        if (blockKey == BuiltInBlocks.Sand || blockKey == BuiltInBlocks.RedSand)
        {
            return SoundCue.SandPlace;
        }

        if (IsSnow(blockKey))
        {
            return SoundCue.SnowPlace;
        }

        if (IsCloth(blockKey))
        {
            return SoundCue.ClothPlace;
        }

        if (IsMetal(blockKey))
        {
            return SoundCue.MetalPlace;
        }

        return SoundCue.StonePlace;
    }

    private static bool IsWood(ResourceKey blockKey) =>
        blockKey == BuiltInBlocks.OakPlanks ||
        blockKey == BuiltInBlocks.OakLog ||
        blockKey == BuiltInBlocks.BirchLog ||
        blockKey == BuiltInBlocks.CherryLog ||
        blockKey == BuiltInBlocks.SpruceLog ||
        blockKey == BuiltInBlocks.CraftingTable ||
        blockKey == BuiltInBlocks.Bookshelf ||
        blockKey == BuiltInBlocks.Chest ||
        blockKey == BuiltInBlocks.Ladder ||
        blockKey == BuiltInBlocks.OakFence ||
        blockKey == BuiltInBlocks.OakDoor ||
        blockKey == BuiltInBlocks.OakTrapdoor ||
        blockKey == BuiltInBlocks.Composter ||
        blockKey == BuiltInBlocks.Torch ||
        blockKey == BuiltInBlocks.RedstoneTorch;

    private static bool IsGrassOrSoil(ResourceKey blockKey) =>
        blockKey == BuiltInBlocks.GrassBlock ||
        blockKey == BuiltInBlocks.Dirt ||
        blockKey == BuiltInBlocks.DirtPath ||
        blockKey == BuiltInBlocks.OakLeaves ||
        blockKey == BuiltInBlocks.BirchLeaves ||
        blockKey == BuiltInBlocks.CherryLeaves ||
        blockKey == BuiltInBlocks.SpruceLeaves ||
        blockKey == BuiltInBlocks.Farmland ||
        blockKey == BuiltInBlocks.HayBlock ||
        blockKey == BuiltInBlocks.Tnt;

    private static bool IsCloth(ResourceKey blockKey) =>
        blockKey == BuiltInBlocks.CrimsonWool ||
        blockKey == BuiltInBlocks.WhiteWool ||
        blockKey == BuiltInBlocks.BlueWool ||
        blockKey == BuiltInBlocks.RedBed;

    private static bool IsMetal(ResourceKey blockKey) =>
        blockKey == BuiltInBlocks.Lantern ||
        blockKey == BuiltInBlocks.Rail ||
        blockKey == BuiltInBlocks.PoweredRail ||
        blockKey == BuiltInBlocks.MonsterSpawner ||
        blockKey == BuiltInBlocks.IronDoor ||
        blockKey == BuiltInBlocks.IronTrapdoor ||
        blockKey == BuiltInBlocks.IronBlock ||
        blockKey == BuiltInBlocks.GoldBlock ||
        blockKey == BuiltInBlocks.CopperBlock;

    private static bool IsDeepslate(ResourceKey blockKey) =>
        blockKey == BuiltInBlocks.Deepslate ||
        blockKey == BuiltInBlocks.CobbledDeepslate;

    private static bool IsSnow(ResourceKey blockKey) =>
        blockKey == BuiltInBlocks.SnowBlock;

    private static bool IsGlass(ResourceKey blockKey) =>
        blockKey == BuiltInBlocks.Glass ||
        blockKey == BuiltInBlocks.Glowstone ||
        blockKey == BuiltInBlocks.PackedIce ||
        blockKey == BuiltInBlocks.RedstoneLamp ||
        blockKey == BuiltInBlocks.LitRedstoneLamp;
}
