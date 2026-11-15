using OverHorizon.Core;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Random;
using OverHorizon.World.Lighting;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Biome surface and sub-surface painting for <see cref="NetherGenerator"/> (split out so the generator
/// itself stays under the 500-line architecture budget).
/// </summary>
public sealed partial class NetherGenerator
{
    private void ApplyBiomeSurfaces(ChunkColumn chunk, int originX, int originZ, ReadOnlySpan<ushort> columnBiomes)
    {
        for (var lz = 0; lz < 16; lz++)
        {
            var wz = originZ + lz;
            for (var lx = 0; lx < 16; lx++)
            {
                var wx = originX + lx;
                var biomeKey = _biomeSampler.GetBiome(columnBiomes[(lz << 4) | lx]).Key;
                var surface = ResolveSurfaceState(biomeKey);
                var subsurface = ResolveSubsurfaceState(biomeKey);

                for (var y = RoofY - 2; y >= FloorY + 2; y--)
                {
                    if (chunk.GetBlockState(lx, y, lz) != _netherrack)
                    {
                        continue;
                    }

                    var above = chunk.GetBlockState(lx, y + 1, lz);
                    if (!above.IsAir && above != _lava)
                    {
                        continue;
                    }

                    var depth = 2 + (int)((Hash(wx, y, wz) >> 5) % 3);
                    for (var k = 0; k < depth; k++)
                    {
                        var targetY = y - k;
                        if (chunk.GetBlockState(lx, targetY, lz) != _netherrack)
                        {
                            break;
                        }

                        _ = chunk.SetBlockStateRaw(lx, targetY, lz, k == 0 ? surface : subsurface);
                    }

                    y -= depth - 1;
                }
            }
        }
    }

    private BlockState ResolveSurfaceState(ResourceKey biomeKey)
    {
        if (biomeKey == BuiltInBiomes.CrimsonForest)
        {
            return _registries.Blocks.GetDefaultState(BuiltInBlocks.CrimsonNylium);
        }

        if (biomeKey == BuiltInBiomes.WarpedForest)
        {
            return _registries.Blocks.GetDefaultState(BuiltInBlocks.WarpedNylium);
        }

        if (biomeKey == BuiltInBiomes.SoulSandValley)
        {
            return _soulSand;
        }

        if (biomeKey == BuiltInBiomes.BasaltDeltas)
        {
            return _basalt;
        }

        return _netherrack;
    }

    private BlockState ResolveSubsurfaceState(ResourceKey biomeKey) =>
        biomeKey == BuiltInBiomes.SoulSandValley ? _soulSoil : _netherrack;
}
