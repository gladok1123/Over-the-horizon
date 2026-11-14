using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;

namespace OverHorizon.Gameplay.Physics;

/// <summary>
/// Crop bookkeeping and vanilla growth simulation for <see cref="PrimedTntAndWorldTicker"/>:
/// tracked <c>Wheat</c> positions advance one <c>age</c> stage per successful light &amp; Farmland-moisture roll,
/// and the supporting Farmland rehydrates or dries out exactly like Java Edition.
/// </summary>
public sealed partial class PrimedTntAndWorldTicker
{
    private const int MaxCropGrowthMutationsPerTick = 12;

    /// <summary>
    /// Adds or removes <paramref name="pos"/> from the tracked crop set after a world mutation so
    /// planted crops keep growing and harvested ones stop consuming tick budget.
    /// </summary>
    /// <param name="pos">Mutated block coordinate.</param>
    /// <param name="getBlock">World block-state accessor.</param>
    private void SyncTrackedCrop(BlockPos pos, Func<BlockPos, BlockState> getBlock)
    {
        var state = getBlock(pos);
        if (state.IsAir)
        {
            _ = _activeCrops.Remove(pos);
            return;
        }

        var meta = _blocks.GetStateMetadata(state);
        if (CropRules.IsCropBlock(meta.BlockKey))
        {
            _ = _activeCrops.Add(pos);
            return;
        }

        if (meta.BlockKey == BuiltInBlocks.Farmland)
        {
            // A Farmland change can expose or bury the crop standing on it!
            SyncTrackedCrop(pos.Offset(Direction.Up), getBlock);
        }
    }

    /// <summary>
    /// Advances every tracked <c>Wheat</c> crop by at most one growth stage and refreshes the moisture of the
    /// Farmland beneath it, matching vanilla light-gated, hydration-boosted growth.
    /// </summary>
    /// <param name="getBlock">World block-state accessor.</param>
    /// <param name="getLight">World light accessor returning sky and block light.</param>
    /// <param name="setBlock">World block-state mutator.</param>
    /// <returns>Number of blocks mutated this tick.</returns>
    private int StepCropGrowth(
        Func<BlockPos, BlockState> getBlock,
        Func<BlockPos, (byte Sky, byte Block)> getLight,
        Action<BlockPos, BlockState> setBlock)
    {
        var mutations = 0;
        var stale = new List<BlockPos>(4);

        foreach (var cropPos in _activeCrops)
        {
            if (mutations >= MaxCropGrowthMutationsPerTick)
            {
                break;
            }

            var cropState = getBlock(cropPos);
            if (cropState.IsAir)
            {
                stale.Add(cropPos);
                continue;
            }

            var cropMeta = _blocks.GetStateMetadata(cropState);
            if (!CropRules.IsCropBlock(cropMeta.BlockKey))
            {
                stale.Add(cropPos);
                continue;
            }

            var soilPos = cropPos.Offset(Direction.Down);
            var soilState = getBlock(soilPos);
            var soilMeta = _blocks.GetStateMetadata(soilState);
            if (!CropRules.IsFarmland(soilMeta.BlockKey))
            {
                // Uprooted: the crop can no longer grow and is popped off like vanilla!
                setBlock(cropPos, BlockState.Air);
                stale.Add(cropPos);
                mutations++;
                continue;
            }

            // Rehydrate (or dry out) the supporting Farmland before rolling growth!
            var moisture = CropRules.ResolveMoisture(_blocks, getBlock, soilPos);
            var currentMoisture = CropRules.GetMoisture(soilMeta);
            if (moisture != currentMoisture)
            {
                setBlock(soilPos, CropRules.WithMoisture(_blocks, soilState, moisture));
                mutations++;
            }

            var age = CropRules.GetAge(cropMeta);
            if (age >= CropRules.MatureAge)
            {
                continue;
            }

            var (sky, block) = getLight(cropPos);
            var light = Math.Max(sky, block);
            if (CropRules.ComputeGrowthChance(moisture, light) <= (NextRandom() / (double)uint.MaxValue))
            {
                continue;
            }

            setBlock(cropPos, CropRules.WithAge(_blocks, cropState, age + 1));
            mutations++;
        }

        for (var i = 0; i < stale.Count; i++)
        {
            _ = _activeCrops.Remove(stale[i]);
        }

        return mutations;
    }
}
