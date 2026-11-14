using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Survival;

/// <summary>
/// Vanilla farming rules: <c>Farmland</c> hydration from nearby water, <c>Wheat</c> growth stages (<c>age 0..7</c>),
/// light &amp; moisture-driven growth chance, and the authentic mature/immature harvest yields.
/// </summary>
public static class CropRules
{
    /// <summary>First fully mature crop age (harvesting below this returns only the seed).</summary>
    public const int MatureAge = 7;

    /// <summary>Maximum <c>Farmland</c> moisture level reached when a water source sits within 4 blocks.</summary>
    public const int MaxMoisture = 7;

    /// <summary>Horizontal radius scanned around a Farmland block when searching for hydrating water.</summary>
    private const int HydrationRadius = 4;

    /// <summary>Determines whether <paramref name="blockKey"/> is a plantable/growable crop block.</summary>
    /// <param name="blockKey">Block resource key to test.</param>
    /// <returns><see langword="true"/> for <see cref="BuiltInBlocks.Wheat"/>.</returns>
    public static bool IsCropBlock(ResourceKey blockKey) => blockKey == BuiltInBlocks.Wheat;

    /// <summary>Determines whether <paramref name="blockKey"/> is tilled <see cref="BuiltInBlocks.Farmland"/>.</summary>
    /// <param name="blockKey">Block resource key to test.</param>
    /// <returns><see langword="true"/> for Farmland.</returns>
    public static bool IsFarmland(ResourceKey blockKey) => blockKey == BuiltInBlocks.Farmland;

    /// <summary>Reads the <c>age</c> property of a crop state, clamped to <c>[0, 7]</c>.</summary>
    /// <param name="meta">Parsed block-state metadata of the crop.</param>
    /// <returns>Growth stage from <c>0</c> (just planted) to <c>7</c> (mature).</returns>
    public static int GetAge(BlockStateMetadata meta) =>
        meta.Properties.TryGetValue("age", out var a) && int.TryParse(a, out var age)
            ? Math.Clamp(age, 0, MatureAge)
            : 0;

    /// <summary>Reads the <c>moisture</c> property of a Farmland state, clamped to <c>[0, 7]</c>.</summary>
    /// <param name="meta">Parsed block-state metadata of the Farmland.</param>
    /// <returns>Hydration level from <c>0</c> (dry) to <c>7</c> (fully watered).</returns>
    public static int GetMoisture(BlockStateMetadata meta) =>
        meta.Properties.TryGetValue("moisture", out var m) && int.TryParse(m, out var moisture)
            ? Math.Clamp(moisture, 0, MaxMoisture)
            : 0;

    /// <summary>Returns a copy of <paramref name="cropState"/> grown to <paramref name="age"/>.</summary>
    /// <param name="blocks">Block registry used to rewrite the <c>age</c> property.</param>
    /// <param name="cropState">Existing crop block state.</param>
    /// <param name="age">Target growth stage in <c>[0, 7]</c>.</param>
    /// <returns>The updated block state.</returns>
    public static BlockState WithAge(IBlockRegistry blocks, BlockState cropState, int age) =>
        blocks.WithProperty(cropState, "age", Math.Clamp(age, 0, MatureAge).ToString());

    /// <summary>Returns a copy of <paramref name="farmlandState"/> hydrated to <paramref name="moisture"/>.</summary>
    /// <param name="blocks">Block registry used to rewrite the <c>moisture</c> property.</param>
    /// <param name="farmlandState">Existing Farmland block state.</param>
    /// <param name="moisture">Target hydration level in <c>[0, 7]</c>.</param>
    /// <returns>The updated block state.</returns>
    public static BlockState WithMoisture(IBlockRegistry blocks, BlockState farmlandState, int moisture) =>
        blocks.WithProperty(farmlandState, "moisture", Math.Clamp(moisture, 0, MaxMoisture).ToString());

    /// <summary>Determines whether a crop at <paramref name="meta"/> is fully grown and ready to harvest.</summary>
    /// <param name="meta">Parsed block-state metadata of the crop.</param>
    /// <returns><see langword="true"/> when <c>age == 7</c>.</returns>
    public static bool IsMature(BlockStateMetadata meta) => GetAge(meta) >= MatureAge;

    /// <summary>
    /// Scans the vanilla <c>9x9x1</c> hydration box around <paramref name="farmlandPos"/> for a water source,
    /// returning <c>7</c> when water sits directly beside/below and <c>0</c> when the soil is bone dry.
    /// </summary>
    /// <param name="blocks">Block registry used to identify water blocks.</param>
    /// <param name="getBlock">World block-state accessor.</param>
    /// <param name="farmlandPos">Farmland block coordinate.</param>
    /// <returns>Hydration level in <c>[0, 7]</c>.</returns>
    public static int ResolveMoisture(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        BlockPos farmlandPos)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(getBlock);

        for (var dx = -HydrationRadius; dx <= HydrationRadius; dx++)
        {
            for (var dz = -HydrationRadius; dz <= HydrationRadius; dz++)
            {
                for (var dy = -1; dy <= 0; dy++)
                {
                    var p = new BlockPos(farmlandPos.X + dx, farmlandPos.Y + dy, farmlandPos.Z + dz);
                    if (p.Y < EngineConstants.WorldMinY || p.Y >= EngineConstants.WorldMaxY)
                    {
                        continue;
                    }

                    var meta = blocks.GetStateMetadata(getBlock(p));
                    if (!meta.IsFluid || meta.BlockKey != BuiltInBlocks.Water)
                    {
                        continue;
                    }

                    // Vanilla: a water source directly adjacent (or beneath) fully hydrates the soil!
                    var distance = Math.Max(Math.Abs(dx), Math.Abs(dz));
                    return distance <= 1 ? MaxMoisture : Math.Clamp(MaxMoisture - (distance - 1), 1, MaxMoisture);
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Computes the per-tick probability that a crop advances one growth stage.
    /// Vanilla scales growth with both light and Farmland moisture, and dry soil grows far slower.
    /// </summary>
    /// <param name="moisture">Farmland hydration in <c>[0, 7]</c>.</param>
    /// <param name="lightLevel">Combined sky/block light at the crop in <c>[0, 15]</c>.</param>
    /// <returns>Growth probability in <c>[0, 1]</c>.</returns>
    public static double ComputeGrowthChance(int moisture, byte lightLevel)
    {
        if (lightLevel < 9)
        {
            return 0.0;
        }

        var lightBonus = (lightLevel - 8) / 7.0;
        var moistureBonus = moisture > 0 ? 1.0 : 0.28;
        return Math.Clamp(0.10 * lightBonus * (0.55 + (moisture * 0.064)) * (moistureBonus + 1.0), 0.0, 1.0);
    }
}
