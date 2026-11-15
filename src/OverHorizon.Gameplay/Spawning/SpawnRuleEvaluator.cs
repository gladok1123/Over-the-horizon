using OverHorizon.Core;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;
using OverHorizon.World.Storage;

namespace OverHorizon.Gameplay.Spawning;

/// <summary>
/// Evaluates natural mob spawn eligibility over immutable <see cref="ChunkSnapshot"/>s using light, surface, and biome rules.
/// </summary>
public static class SpawnRuleEvaluator
{
    /// <summary>
    /// Determines whether a mob of <paramref name="category"/> can naturally spawn with feet at
    /// (<paramref name="localX"/>, <paramref name="worldY"/>, <paramref name="localZ"/>) inside <paramref name="chunk"/>.
    /// </summary>
    /// <param name="chunk">Read-only chunk snapshot.</param>
    /// <param name="registries">Frozen domain registries.</param>
    /// <param name="category">Mob category.</param>
    /// <param name="localX">Chunk-local X in <c>[0, 15]</c>.</param>
    /// <param name="worldY">Feet world Y coordinate.</param>
    /// <param name="localZ">Chunk-local Z in <c>[0, 15]</c>.</param>
    /// <param name="skyDarkening">Ambient sky light reduction due to night/weather in <c>[0, 15]</c>.</param>
    /// <returns><see langword="true"/> if all physical, lighting, and biome conditions are satisfied.</returns>
    public static bool CanSpawnAt(
        ChunkSnapshot chunk,
        CoreRegistries registries,
        MobCategory category,
        int localX,
        int worldY,
        int localZ,
        int skyDarkening = 0)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentNullException.ThrowIfNull(registries);

        if ((uint)localX >= 16U || (uint)localZ >= 16U)
        {
            return false;
        }

        if (worldY <= EngineConstants.WorldMinY || worldY + 1 >= EngineConstants.WorldMaxY)
        {
            return false;
        }

        var blocks = registries.Blocks;

        // 1. Solid non-bedrock supporting block directly underneath.
        var groundState = chunk.GetBlockState(localX, worldY - 1, localZ);
        var groundMeta = blocks.GetStateMetadata(groundState);
        if (!groundMeta.IsSolid || groundMeta.BlockKey == BuiltInBlocks.Bedrock)
        {
            return false;
        }

        // 2. Two vertical blocks of non-solid, non-fluid clearance.
        for (var dy = 0; dy < 2; dy++)
        {
            var bodyMeta = blocks.GetStateMetadata(chunk.GetBlockState(localX, worldY + dy, localZ));
            if (bodyMeta.IsSolid || bodyMeta.IsFluid)
            {
                return false;
            }
        }

        // 3. Category-specific light and biome checks.
        var rawSky = chunk.GetSkyLight(localX, worldY, localZ);
        var effectiveSky = Math.Max(0, rawSky - Math.Clamp(skyDarkening, 0, 15));
        var blockLight = chunk.GetBlockLight(localX, worldY, localZ);

        if (category == MobCategory.Passive)
        {
            if (groundMeta.BlockKey != BuiltInBlocks.GrassBlock || effectiveSky < 9)
            {
                return false;
            }

            var sec = chunk.Sections[ChunkColumn.GetSectionIndex(worldY)];
            var biomeId = sec.GetBiomeId(localX >> 2, (worldY & 15) >> 2, localZ >> 2);
            var biomeKey = registries.Biomes.GetKey(biomeId);
            return biomeKey == BuiltInBiomes.Plains || biomeKey == BuiltInBiomes.Forest;
        }

        // Hostile: requires zero artificial block light and low effective sky light (<= 7).
        return blockLight == 0 && effectiveSky <= 7;
    }
}
