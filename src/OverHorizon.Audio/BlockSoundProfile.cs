using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;

namespace OverHorizon.Audio;

/// <summary>
/// Maps every built-in voxel block to authentic material-specific placement, breaking, and footstep <see cref="SoundCue"/> effects.
/// </summary>
public static class BlockSoundProfile
{
    /// <summary>Resolves the material-specific block breaking sound cue and pitch variance.</summary>
    /// <param name="blockKey">Broken block key.</param>
    public static (SoundCue Cue, float Pitch) ResolveBreakSound(ResourceKey blockKey)
    {
        if (blockKey == BuiltInBlocks.Fire)
        {
            return (SoundCue.Fizz, 1.08f);
        }

        if (blockKey == BuiltInBlocks.Glass || blockKey == BuiltInBlocks.Glowstone ||
            blockKey == BuiltInBlocks.RedstoneLamp || blockKey == BuiltInBlocks.LitRedstoneLamp ||
            blockKey == BuiltInBlocks.AmethystBlock)
        {
            return (SoundCue.GlassBreak, blockKey == BuiltInBlocks.AmethystBlock ? 1.24f : 1.0f);
        }

        if (IsWoodMaterial(blockKey))
        {
            return (SoundCue.WoodBreak, 0.96f);
        }

        if (IsGrassOrFoliageMaterial(blockKey))
        {
            return (SoundCue.GrassBreak, 0.96f);
        }

        if (IsSandOrGravelMaterial(blockKey))
        {
            return (SoundCue.SandBreak, blockKey == BuiltInBlocks.Gravel ? 0.86f : 1.0f);
        }

        if (IsMetalMaterial(blockKey))
        {
            return (SoundCue.MetalBreak, 0.98f);
        }

        if (IsWoolOrSoftMaterial(blockKey))
        {
            return (SoundCue.ClothPlace, 0.92f);
        }

        return (SoundCue.BlockBreak, 0.94f);
    }

    /// <summary>Resolves the material-specific block placement sound cue and pitch variance.</summary>
    /// <param name="blockKey">Placed block key.</param>
    public static (SoundCue Cue, float Pitch) ResolvePlaceSound(ResourceKey blockKey)
    {
        if (IsWoodMaterial(blockKey))
        {
            return (SoundCue.WoodPlace, 0.95f);
        }

        if (IsGrassOrFoliageMaterial(blockKey))
        {
            return (SoundCue.GrassPlace, 0.96f);
        }

        if (IsSandOrGravelMaterial(blockKey))
        {
            return (SoundCue.SandPlace, blockKey == BuiltInBlocks.Gravel ? 0.88f : 1.0f);
        }

        if (IsMetalMaterial(blockKey))
        {
            return (SoundCue.MetalPlace, 1.02f);
        }

        if (IsWoolOrSoftMaterial(blockKey))
        {
            return (SoundCue.ClothPlace, 1.0f);
        }

        if (blockKey == BuiltInBlocks.AmethystBlock || blockKey == BuiltInBlocks.Glass || blockKey == BuiltInBlocks.Glowstone)
        {
            return (SoundCue.StonePlace, 1.20f);
        }

        return (SoundCue.StonePlace, 0.92f);
    }

    /// <summary>Resolves the material-specific break sound cue for <paramref name="blockKey"/>.</summary>
    public static SoundCue ResolveBreakCue(ResourceKey blockKey) => ResolveBreakSound(blockKey).Cue;

    /// <summary>Resolves the material-specific break pitch for <paramref name="blockKey"/>.</summary>
    public static float ResolveBreakPitch(ResourceKey blockKey) => ResolveBreakSound(blockKey).Pitch;

    /// <summary>Resolves the material-specific placement sound cue for <paramref name="blockKey"/>.</summary>
    public static SoundCue ResolvePlaceCue(ResourceKey blockKey) => ResolvePlaceSound(blockKey).Cue;

    /// <summary>Resolves the material-specific placement pitch for <paramref name="blockKey"/>.</summary>
    public static float ResolvePlacePitch(ResourceKey blockKey) => ResolvePlaceSound(blockKey).Pitch;

    private static bool IsWoodMaterial(ResourceKey k) =>
        k == BuiltInBlocks.OakPlanks || k == BuiltInBlocks.BirchPlanks ||
        k == BuiltInBlocks.OakLog || k == BuiltInBlocks.BirchLog ||
        k == BuiltInBlocks.CraftingTable || k == BuiltInBlocks.Bookshelf ||
        k == BuiltInBlocks.Chest || k == BuiltInBlocks.Barrel ||
        k == BuiltInBlocks.OakFence || k == BuiltInBlocks.Ladder ||
        k == BuiltInBlocks.Torch || k == BuiltInBlocks.RedstoneTorch ||
        k == BuiltInBlocks.Composter || k == BuiltInBlocks.OakTrapdoor;

    private static bool IsGrassOrFoliageMaterial(ResourceKey k) =>
        k == BuiltInBlocks.GrassBlock || k == BuiltInBlocks.Dirt ||
        k == BuiltInBlocks.Farmland || k == BuiltInBlocks.OakLeaves ||
        k == BuiltInBlocks.BirchLeaves || k == BuiltInBlocks.HayBlock ||
        k == BuiltInBlocks.Tnt || k == BuiltInBlocks.SnowBlock;

    private static bool IsSandOrGravelMaterial(ResourceKey k) =>
        k == BuiltInBlocks.Sand || k == BuiltInBlocks.Gravel || k == BuiltInBlocks.RedstoneWire;

    private static bool IsMetalMaterial(ResourceKey k) =>
        k == BuiltInBlocks.IronBlock || k == BuiltInBlocks.GoldBlock ||
        k == BuiltInBlocks.CopperBlock || k == BuiltInBlocks.DiamondBlock ||
        k == BuiltInBlocks.EmeraldBlock || k == BuiltInBlocks.Lantern ||
        k == BuiltInBlocks.Rail || k == BuiltInBlocks.PoweredRail ||
        k == BuiltInBlocks.MonsterSpawner;

    private static bool IsWoolOrSoftMaterial(ResourceKey k) =>
        k == BuiltInBlocks.WhiteWool || k == BuiltInBlocks.CrimsonWool ||
        k == BuiltInBlocks.BlueWool || k == BuiltInBlocks.RedBed ||
        k == BuiltInBlocks.Cobweb;
}
