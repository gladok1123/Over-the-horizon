using System.Numerics;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay.Redstone;

namespace OverHorizon.Gameplay.Survival;

/// <summary>
/// Resolves vanilla-style straight, curved (<c>south_east</c>..<c>north_east</c>), and 45-degree ascending rail shapes,
/// neighbor track auto-connection, and 8-block <c>PoweredRail</c> Redstone propagation.
/// </summary>
public static class RailPlacementRules
{
    /// <summary>Checks whether <paramref name="blockKey"/> is <see cref="BuiltInBlocks.Rail"/> or <see cref="BuiltInBlocks.PoweredRail"/>.</summary>
    public static bool IsRailBlock(ResourceKey blockKey) =>
        blockKey == BuiltInBlocks.Rail || blockKey == BuiltInBlocks.PoweredRail;

    /// <summary>Reads the <c>shape</c> property of a rail state, defaulting to <c>north_south</c>.</summary>
    public static string GetRailShape(BlockStateMetadata meta) =>
        meta.Properties.TryGetValue("shape", out var s) ? s : "north_south";

    /// <summary>
    /// Computes the connected rail <c>shape</c> for a newly placed or neighbor-updated rail at <paramref name="pos"/>.
    /// </summary>
    public static BlockState ResolveRailState(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        ResourceKey railKey,
        BlockPos pos,
        Vector3 playerForward,
        BlockState? existingRailState = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(getBlock);

        var allowCurves = railKey == BuiltInBlocks.Rail;
        var n = HasRailConnection(blocks, getBlock, pos, Direction.North, out var nUp);
        var s = HasRailConnection(blocks, getBlock, pos, Direction.South, out var sUp);
        var w = HasRailConnection(blocks, getBlock, pos, Direction.West, out var wUp);
        var e = HasRailConnection(blocks, getBlock, pos, Direction.East, out var eUp);

        string? shape = null;

        if (allowCurves)
        {
            if (s && e && !n && !w) shape = "south_east";
            else if (s && w && !n && !e) shape = "south_west";
            else if (n && w && !s && !e) shape = "north_west";
            else if (n && e && !s && !w) shape = "north_east";
        }

        if (shape is null)
        {
            if ((n || s) && !w && !e)
            {
                shape = "north_south";
            }
            else if ((w || e) && !n && !s)
            {
                shape = "east_west";
            }
            else
            {
                var horiz = BlockAttachmentAndShapeRules.HorizontalLookDirection(playerForward);
                shape = horiz.GetAxis() == Axis.X ? "east_west" : "north_south";
            }
        }

        if (shape == "north_south")
        {
            if (nUp && BlockAttachmentAndShapeRules.IsFullSupportBlock(blocks, getBlock(pos.Offset(Direction.North))))
            {
                shape = "ascending_north";
            }
            else if (sUp && BlockAttachmentAndShapeRules.IsFullSupportBlock(blocks, getBlock(pos.Offset(Direction.South))))
            {
                shape = "ascending_south";
            }
        }
        else if (shape == "east_west")
        {
            if (eUp && BlockAttachmentAndShapeRules.IsFullSupportBlock(blocks, getBlock(pos.Offset(Direction.East))))
            {
                shape = "ascending_east";
            }
            else if (wUp && BlockAttachmentAndShapeRules.IsFullSupportBlock(blocks, getBlock(pos.Offset(Direction.West))))
            {
                shape = "ascending_west";
            }
        }

        var baseState = existingRailState ?? blocks.GetDefaultState(railKey);
        return blocks.WithProperty(baseState, "shape", shape);
    }

    /// <summary>Updates directly adjacent rails around <paramref name="centerPos"/> so tracks auto-connect on placement.</summary>
    public static void RefreshAdjacentRails(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        BlockPos centerPos)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);

        foreach (var dir in DirectionExtensions.Horizontal)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                var np = new BlockPos(centerPos.X + dir.Normal().X, centerPos.Y + dy, centerPos.Z + dir.Normal().Z);
                if (np.Y < EngineConstants.WorldMinY || np.Y >= EngineConstants.WorldMaxY)
                {
                    continue;
                }

                var st = getBlock(np);
                if (st.IsAir)
                {
                    continue;
                }

                var meta = blocks.GetStateMetadata(st);
                if (!IsRailBlock(meta.BlockKey))
                {
                    continue;
                }

                var updated = ResolveRailState(blocks, getBlock, meta.BlockKey, np, new Vector3(0.0f, 0.0f, 1.0f), st);
                if (updated != st)
                {
                    setBlock(np, updated);
                }
            }
        }
    }

    /// <summary>Checks whether a <c>PoweredRail</c> at <paramref name="pos"/> receives Redstone power directly or via up to 8 connected powered rails.</summary>
    public static bool IsPoweredRailEnergized(
        IBlockRegistry blocks,
        RedstoneEngine engine,
        Func<BlockPos, BlockState> getBlock,
        BlockPos pos)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(getBlock);

        if (engine.GetPowerReceivedAt(pos, getBlock, ignoreSource: null) > 0)
        {
            return true;
        }

        var centerMeta = blocks.GetStateMetadata(getBlock(pos));
        if (centerMeta.BlockKey != BuiltInBlocks.PoweredRail)
        {
            return false;
        }

        var shape = GetRailShape(centerMeta);
        var (dirA, dirB) = GetEndsForShape(shape);
        return SearchPoweredChain(blocks, engine, getBlock, pos, dirA, 1) ||
               SearchPoweredChain(blocks, engine, getBlock, pos, dirB, 1);
    }

    /// <summary>Returns the two horizontal endpoint directions connected by <paramref name="shape"/>.</summary>
    public static (Direction EndA, Direction EndB) GetEndsForShape(string shape) => shape switch
    {
        "east_west" or "ascending_east" or "ascending_west" => (Direction.West, Direction.East),
        "south_east" => (Direction.South, Direction.East),
        "south_west" => (Direction.South, Direction.West),
        "north_west" => (Direction.North, Direction.West),
        "north_east" => (Direction.North, Direction.East),
        _ => (Direction.North, Direction.South),
    };

    private static bool SearchPoweredChain(
        IBlockRegistry blocks,
        RedstoneEngine engine,
        Func<BlockPos, BlockState> getBlock,
        BlockPos fromPos,
        Direction travelDir,
        int distance)
    {
        if (distance > 8)
        {
            return false;
        }

        for (var dy = -1; dy <= 1; dy++)
        {
            var nextPos = new BlockPos(fromPos.X + travelDir.Normal().X, fromPos.Y + dy, fromPos.Z + travelDir.Normal().Z);
            if (nextPos.Y < EngineConstants.WorldMinY || nextPos.Y >= EngineConstants.WorldMaxY)
            {
                continue;
            }

            var meta = blocks.GetStateMetadata(getBlock(nextPos));
            if (meta.BlockKey != BuiltInBlocks.PoweredRail)
            {
                continue;
            }

            var nextShape = GetRailShape(meta);
            var (a, b) = GetEndsForShape(nextShape);
            if (a != travelDir.Opposite() && b != travelDir.Opposite())
            {
                continue;
            }

            if (engine.GetPowerReceivedAt(nextPos, getBlock, ignoreSource: null) > 0)
            {
                return true;
            }

            return SearchPoweredChain(blocks, engine, getBlock, nextPos, travelDir, distance + 1);
        }

        return false;
    }

    private static bool HasRailConnection(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        BlockPos origin,
        Direction dir,
        out bool isUpperRail)
    {
        isUpperRail = false;
        var flat = origin.Offset(dir);
        if (IsRailBlock(blocks.GetStateMetadata(getBlock(flat)).BlockKey))
        {
            return true;
        }

        var up = flat.Offset(Direction.Up);
        if (up.Y < EngineConstants.WorldMaxY && IsRailBlock(blocks.GetStateMetadata(getBlock(up)).BlockKey))
        {
            isUpperRail = true;
            return true;
        }

        var down = flat.Offset(Direction.Down);
        return down.Y >= EngineConstants.WorldMinY && IsRailBlock(blocks.GetStateMetadata(getBlock(down)).BlockKey);
    }
}
