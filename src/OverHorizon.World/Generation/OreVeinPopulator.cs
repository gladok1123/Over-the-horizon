using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.World.Storage;

namespace OverHorizon.World.Generation;

/// <summary>
/// Deterministic depth-stratified mineral ore and gravel vein populator for <see cref="OverworldGenerator"/>.
/// </summary>
internal sealed class OreVeinPopulator
{
    private readonly long _seed;
    private readonly BlockStateId _stone;
    private readonly BlockStateId _deepslate;
    private readonly BlockStateId _coalOre;
    private readonly BlockStateId _copperOre;
    private readonly BlockStateId _ironOre;
    private readonly BlockStateId _goldOre;
    private readonly BlockStateId _redstoneOre;
    private readonly BlockStateId _lapisOre;
    private readonly BlockStateId _diamondOre;
    private readonly BlockStateId _emeraldOre;
    private readonly BlockStateId _gravel;

    public OreVeinPopulator(long seed, IBlockRegistry blocks)
    {
        _seed = seed;
        _stone = blocks.GetByKey(BuiltInBlocks.Stone).DefaultState;
        _deepslate = blocks.GetByKey(BuiltInBlocks.Deepslate).DefaultState;
        _coalOre = blocks.GetByKey(BuiltInBlocks.CoalOre).DefaultState;
        _copperOre = blocks.GetByKey(BuiltInBlocks.CopperOre).DefaultState;
        _ironOre = blocks.GetByKey(BuiltInBlocks.IronOre).DefaultState;
        _goldOre = blocks.GetByKey(BuiltInBlocks.GoldOre).DefaultState;
        _redstoneOre = blocks.GetByKey(BuiltInBlocks.RedstoneOre).DefaultState;
        _lapisOre = blocks.GetByKey(BuiltInBlocks.LapisOre).DefaultState;
        _diamondOre = blocks.GetByKey(BuiltInBlocks.DiamondOre).DefaultState;
        _emeraldOre = blocks.GetByKey(BuiltInBlocks.EmeraldOre).DefaultState;
        _gravel = blocks.GetByKey(BuiltInBlocks.Gravel).DefaultState;
    }

    public void PopulateChunk(ChunkColumn chunk)
    {
        var cx = chunk.Position.X;
        var cz = chunk.Position.Z;

        ScatterVeins(chunk, cx, cz, salt: 101, _coalOre, veinsPerChunk: 16, minY: 8, maxY: 118, minBlocks: 7, maxBlocks: 13);
        ScatterVeins(chunk, cx, cz, salt: 202, _copperOre, veinsPerChunk: 12, minY: 4, maxY: 92, minBlocks: 6, maxBlocks: 10);
        ScatterVeins(chunk, cx, cz, salt: 303, _ironOre, veinsPerChunk: 15, minY: -36, maxY: 72, minBlocks: 5, maxBlocks: 9);
        ScatterVeins(chunk, cx, cz, salt: 404, _goldOre, veinsPerChunk: 6, minY: -58, maxY: 32, minBlocks: 4, maxBlocks: 8);
        ScatterVeins(chunk, cx, cz, salt: 505, _redstoneOre, veinsPerChunk: 7, minY: -60, maxY: 16, minBlocks: 5, maxBlocks: 8);
        ScatterVeins(chunk, cx, cz, salt: 606, _lapisOre, veinsPerChunk: 4, minY: -48, maxY: 30, minBlocks: 4, maxBlocks: 7);
        ScatterVeins(chunk, cx, cz, salt: 707, _diamondOre, veinsPerChunk: 5, minY: -60, maxY: 16, minBlocks: 3, maxBlocks: 6);
        ScatterVeins(chunk, cx, cz, salt: 808, _emeraldOre, veinsPerChunk: 3, minY: -16, maxY: 108, minBlocks: 1, maxBlocks: 3);
        ScatterVeins(chunk, cx, cz, salt: 909, _gravel, veinsPerChunk: 4, minY: -16, maxY: 60, minBlocks: 8, maxBlocks: 14);
    }

    private void ScatterVeins(
        ChunkColumn chunk,
        int cx,
        int cz,
        int salt,
        BlockStateId oreState,
        int veinsPerChunk,
        int minY,
        int maxY,
        int minBlocks,
        int maxBlocks)
    {
        var ySpan = Math.Max(1, maxY - minY + 1);
        var sizeSpan = Math.Max(1, maxBlocks - minBlocks + 1);

        for (var v = 0; v < veinsPerChunk; v++)
        {
            var h = Hash(_seed, cx, cz, (salt * 31) + v);
            var startX = (int)(h & 15UL);
            var startZ = (int)((h >> 4) & 15UL);
            var startY = minY + (int)((h >> 8) % (ulong)ySpan);
            var targetCount = minBlocks + (int)((h >> 24) % (ulong)sizeSpan);

            PlaceClusterWalk(chunk, startX, startY, startZ, oreState, targetCount, h >> 32);
        }
    }

    private void PlaceClusterWalk(
        ChunkColumn chunk,
        int x,
        int y,
        int z,
        BlockStateId oreState,
        int steps,
        ulong rngState)
    {
        var curX = x;
        var curY = y;
        var curZ = z;
        var s = rngState ^ 0x9E3779B97F4A7C15UL;

        for (var i = 0; i < steps; i++)
        {
            TryReplaceRock(chunk, curX, curY, curZ, oreState);
            if (steps >= 6 && (i & 1) == 0)
            {
                TryReplaceRock(chunk, curX + 1, curY, curZ, oreState);
                TryReplaceRock(chunk, curX, curY, curZ + 1, oreState);
            }

            s = Mix(s + (ulong)i);
            var dir = (int)(s % 6UL);
            switch (dir)
            {
                case 0:
                    curX++;
                    break;
                case 1:
                    curX--;
                    break;
                case 2:
                    curY++;
                    break;
                case 3:
                    curY--;
                    break;
                case 4:
                    curZ++;
                    break;
                default:
                    curZ--;
                    break;
            }

            curX = Math.Clamp(curX, 0, 15);
            curZ = Math.Clamp(curZ, 0, 15);
            curY = Math.Clamp(curY, EngineConstants.WorldMinY + 2, EngineConstants.WorldMaxY - 4);
        }
    }

    private void TryReplaceRock(ChunkColumn chunk, int lx, int y, int lz, BlockStateId oreState)
    {
        if ((uint)lx >= 16U || (uint)lz >= 16U || y <= EngineConstants.WorldMinY + 1 || y >= EngineConstants.WorldMaxY)
        {
            return;
        }

        var existing = chunk.GetBlockState(lx, y, lz);
        if (existing == _stone || existing == _deepslate)
        {
            chunk.SetBlockState(lx, y, lz, oreState);
        }
    }

    private static ulong Hash(long seed, int cx, int cz, int index)
    {
        var h = (ulong)seed ^ ((ulong)(uint)cx * 0xBF58476D1CE4E5B9UL) ^ ((ulong)(uint)cz * 0x94D049BB133111EBUL);
        return Mix(h ^ ((ulong)(uint)index * 0x9E3779B97F4A7C15UL));
    }

    private static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
