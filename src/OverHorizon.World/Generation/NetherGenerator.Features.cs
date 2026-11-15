using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Shared.Random;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Ore veins, Glowstone ceiling clusters, fungal wart growths, and Basalt Delta pillars for
/// <see cref="NetherGenerator"/> (split out to respect the 500-line architecture budget).
/// </summary>
public sealed partial class NetherGenerator
{
    private void PopulateVeinsAndFeatures(ChunkColumn chunk, int originX, int originZ, ReadOnlySpan<ushort> columnBiomes)
    {
        var rng = new Xoroshiro128PlusPlus(
            unchecked((ulong)WorldSeed ^ 0x9E37_79B9_7F4A_7C15UL),
            unchecked(((ulong)(uint)originX << 32) | (ulong)(uint)originZ));

        for (var i = 0; i < 14; i++)
        {
            PlaceVein(chunk, originX, originZ, ref rng, 10, 118, _quartzOre);
        }

        for (var i = 0; i < 6; i++)
        {
            PlaceVein(chunk, originX, originZ, ref rng, 10, 117, _goldOre);
        }

        if (rng.NextInt32(100) < 22)
        {
            var deep = rng.NextInt32(100) < 60;
            PlaceVein(chunk, originX, originZ, ref rng, deep ? 8 : 20, deep ? 24 : 119, _ancientDebris);
        }

        for (var i = 0; i < 5; i++)
        {
            PlaceVein(chunk, originX, originZ, ref rng, 23, LavaSeaY + 6, _magma);
        }

        for (var i = 0; i < 4; i++)
        {
            PlaceVein(chunk, originX, originZ, ref rng, 6, LavaSeaY + 4, _gravel);
        }

        PlaceGlowstoneClusters(chunk, originX, originZ, ref rng);
        PlaceFungalGrowths(chunk, originX, originZ, columnBiomes, ref rng);
        PlaceBasaltPillars(chunk, originX, originZ, columnBiomes);
    }

    private void PlaceVein(
        ChunkColumn chunk,
        int originX,
        int originZ,
        ref Xoroshiro128PlusPlus rng,
        int minY,
        int maxY,
        BlockState state)
    {
        var wx = originX + rng.NextInt32(16);
        var wz = originZ + rng.NextInt32(16);
        var wy = minY + rng.NextInt32(Math.Max(1, maxY - minY));
        SetIfNetherrack(chunk, originX, originZ, wx, wy, wz, state);

        // Walk a short random worm so clusters read as real ore pockets instead of isolated dots.
        var count = 2 + rng.NextInt32(5);
        for (var i = 0; i < count; i++)
        {
            wx += rng.NextInt32(3) - 1;
            wy += rng.NextInt32(3) - 1;
            wz += rng.NextInt32(3) - 1;
            if (wy < minY || wy > maxY)
            {
                continue;
            }

            SetIfNetherrack(chunk, originX, originZ, wx, wy, wz, state);
        }
    }

    private void PlaceGlowstoneClusters(ChunkColumn chunk, int originX, int originZ, ref Xoroshiro128PlusPlus rng)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var wx = originX + rng.NextInt32(16);
            var wz = originZ + rng.NextInt32(16);
            var wy = 60 + rng.NextInt32(RoofY - 66);
            if (!TryFindCeiling(chunk, originX, originZ, wx, wy, wz, out var ceilingY))
            {
                continue;
            }

            var radius = 1 + rng.NextInt32(2);
            for (var dy = 0; dy <= radius; dy++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        if (((dx * dx) + (dy * dy) + (dz * dz)) > (radius * radius) + 1)
                        {
                            continue;
                        }

                        if (dy > 0 && ((dx * dx) + (dz * dz)) > 1)
                        {
                            continue;
                        }

                        SetIfNetherrackOrAir(chunk, originX, originZ, wx + dx, ceilingY - dy, wz + dz, _glowstone);
                    }
                }
            }
        }
    }

    private void PlaceFungalGrowths(
        ChunkColumn chunk,
        int originX,
        int originZ,
        ReadOnlySpan<ushort> columnBiomes,
        ref Xoroshiro128PlusPlus rng)
    {
        for (var lz = 0; lz < 16; lz++)
        {
            for (var lx = 0; lx < 16; lx++)
            {
                var biomeKey = _biomeSampler.GetBiome(columnBiomes[(lz << 4) | lx]).Key;
                var isCrimson = biomeKey == BuiltInBiomes.CrimsonForest;
                var isWarped = biomeKey == BuiltInBiomes.WarpedForest;
                if ((!isCrimson && !isWarped) || rng.NextInt32(100) >= 7)
                {
                    continue;
                }

                var wx = originX + lx;
                var wz = originZ + lz;
                if (!TryFindFloor(chunk, originX, originZ, wx, wz, out var floorY))
                {
                    continue;
                }

                var wart = isCrimson ? _netherWart : _warpedWart;
                var height = 1 + rng.NextInt32(3);
                for (var i = 0; i < height; i++)
                {
                    SetIfNetherrackOrAir(chunk, originX, originZ, wx, floorY + i, wz, wart);
                }

                if (isWarped && rng.NextInt32(100) < 45)
                {
                    SetIfNetherrackOrAir(chunk, originX, originZ, wx, floorY + height, wz, _shroomlight);
                }
            }
        }
    }

    private void PlaceBasaltPillars(
        ChunkColumn chunk,
        int originX,
        int originZ,
        ReadOnlySpan<ushort> columnBiomes)
    {
        for (var lz = 0; lz < 16; lz++)
        {
            for (var lx = 0; lx < 16; lx++)
            {
                var biomeKey = _biomeSampler.GetBiome(columnBiomes[(lz << 4) | lx]).Key;
                if (biomeKey != BuiltInBiomes.BasaltDeltas)
                {
                    continue;
                }

                var wx = originX + lx;
                var wz = originZ + lz;
                var warp = _warpNoise.Sample3D(wx * 0.02, 0.0, wz * 0.02);
                if (warp < 0.34 || !TryFindFloor(chunk, originX, originZ, wx, wz, out var floorY))
                {
                    continue;
                }

                var height = 2 + (int)((Hash(wx, floorY, wz) >> 7) % 7);
                for (var i = 0; i < height; i++)
                {
                    SetIfNetherrackOrAir(chunk, originX, originZ, wx, floorY + i, wz, _basalt);
                }
            }
        }
    }

    private static bool TryFindCeiling(
        ChunkColumn chunk,
        int originX,
        int originZ,
        int worldX,
        int startY,
        int worldZ,
        out int ceilingY)
    {
        var lx = worldX - originX;
        var lz = worldZ - originZ;
        for (var y = Math.Min(RoofY - 3, startY); y > FloorY + 3; y--)
        {
            if (chunk.GetBlockState(lx, y, lz).IsAir || chunk.GetBlockState(lx, y + 1, lz).IsAir)
            {
                continue;
            }

            if (chunk.GetBlockState(lx, y - 1, lz).IsAir)
            {
                ceilingY = y;
                return true;
            }
        }

        ceilingY = -1;
        return false;
    }

    private static bool TryFindFloor(ChunkColumn chunk, int originX, int originZ, int worldX, int worldZ, out int floorY)
    {
        var lx = worldX - originX;
        var lz = worldZ - originZ;
        for (var y = RoofY - 3; y > FloorY + 2; y--)
        {
            if (chunk.GetBlockState(lx, y, lz).IsAir && !chunk.GetBlockState(lx, y - 1, lz).IsAir)
            {
                floorY = y;
                return true;
            }
        }

        floorY = -1;
        return false;
    }

    private void SetIfNetherrack(
        ChunkColumn chunk,
        int originX,
        int originZ,
        int worldX,
        int worldY,
        int worldZ,
        BlockState state)
    {
        var lx = worldX - originX;
        var lz = worldZ - originZ;
        if ((uint)lx >= 16U || (uint)lz >= 16U || worldY < FloorY + 1 || worldY >= RoofY)
        {
            return;
        }

        if (chunk.GetBlockState(lx, worldY, lz) != _netherrack)
        {
            return;
        }

        _ = chunk.SetBlockStateRaw(lx, worldY, lz, state);
    }

    private void SetIfNetherrackOrAir(
        ChunkColumn chunk,
        int originX,
        int originZ,
        int worldX,
        int worldY,
        int worldZ,
        BlockState state)
    {
        var lx = worldX - originX;
        var lz = worldZ - originZ;
        if ((uint)lx >= 16U || (uint)lz >= 16U || worldY < FloorY + 1 || worldY >= RoofY - 1)
        {
            return;
        }

        var current = chunk.GetBlockState(lx, worldY, lz);
        if (current != _netherrack && !current.IsAir)
        {
            return;
        }

        _ = chunk.SetBlockStateRaw(lx, worldY, lz, state);
    }
}
