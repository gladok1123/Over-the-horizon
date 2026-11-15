using System.Globalization;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;

namespace OverHorizon.Gameplay.Redstone;

/// <summary>
/// Vanilla-accurate <c>RedstoneWire</c> cardinal/step-up connection rules, <c>RedstoneRepeater</c> side-locking &amp; rear block reading,
/// and <c>RedstoneComparator</c> compare/subtract &amp; container fullness evaluation.
/// </summary>
public static class RedstoneDiodeAndWireRules
{
    /// <summary>
    /// Computes horizontal arm connections and <c>+1Y</c> step-up climbs for a <c>RedstoneWire</c> at <paramref name="wirePos"/>.
    /// </summary>
    public static (bool North, bool South, bool West, bool East, bool UpNorth, bool UpSouth, bool UpWest, bool UpEast)
        GetWireConnections(IBlockRegistry blocks, BlockPos wirePos, Func<BlockPos, BlockState> getBlock)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(getBlock);

        var aboveOpaque = blocks.GetStateMetadata(getBlock(wirePos.Offset(Direction.Up))).IsOpaque;

        bool CheckSide(Direction dir, out bool climbsUp)
        {
            climbsUp = false;
            var flatPos = wirePos.Offset(dir);
            var flatState = getBlock(flatPos);
            var flatMeta = blocks.GetStateMetadata(flatState);

            if (ConnectsHorizontallyToWire(flatMeta, dir))
            {
                return true;
            }

            if (!aboveOpaque && flatPos.Y + 1 < EngineConstants.WorldMaxY)
            {
                var upDiagMeta = blocks.GetStateMetadata(getBlock(flatPos.Offset(Direction.Up)));
                if (upDiagMeta.BlockKey == BuiltInBlocks.RedstoneWire)
                {
                    climbsUp = true;
                    return true;
                }
            }

            if (!flatMeta.IsOpaque && flatPos.Y - 1 >= EngineConstants.WorldMinY)
            {
                var dnDiagMeta = blocks.GetStateMetadata(getBlock(flatPos.Offset(Direction.Down)));
                if (dnDiagMeta.BlockKey == BuiltInBlocks.RedstoneWire)
                {
                    return true;
                }
            }

            return false;
        }

        var n = CheckSide(Direction.North, out var upN);
        var s = CheckSide(Direction.South, out var upS);
        var w = CheckSide(Direction.West, out var upW);
        var e = CheckSide(Direction.East, out var upE);

        // Vanilla straight-line extension: when connected on ONLY ONE side, the dust line spans straight across the cell!
        if (n && !s && !w && !e) s = true;
        else if (s && !n && !w && !e) n = true;
        else if (w && !e && !n && !s) e = true;
        else if (e && !w && !n && !s) w = true;

        return (n, s, w, e, upN, upS, upW, upE);
    }

    /// <summary>Returns <see langword="true"/> when a <c>RedstoneRepeater</c> at <paramref name="pos"/> is side-locked by an adjacent powered diode.</summary>
    public static bool IsRepeaterSideLocked(
        IBlockRegistry blocks,
        BlockPos pos,
        Direction repeaterFacing,
        Func<BlockPos, BlockState> getBlock)
    {
        var sideAxisA = repeaterFacing.GetAxis() == Axis.Z ? Direction.West : Direction.North;
        var sideAxisB = sideAxisA.Opposite();
        return IsPoweredDiodePointingToward(blocks, pos.Offset(sideAxisA), sideAxisA.Opposite(), getBlock) ||
               IsPoweredDiodePointingToward(blocks, pos.Offset(sideAxisB), sideAxisB.Opposite(), getBlock);
    }

    /// <summary>Computes the rear input signal (<c>0..15</c>) entering the back of a Repeater or Comparator.</summary>
    public static int ComputeDiodeRearInput(
        IBlockRegistry blocks,
        RedstoneEngine engine,
        BlockPos diodePos,
        Direction diodeFacing,
        bool isComparator,
        Func<BlockPos, BlockState> getBlock)
    {
        var backPos = diodePos.Offset(diodeFacing.Opposite());
        var backState = getBlock(backPos);
        if (backState.IsAir)
        {
            return 0;
        }

        var backMeta = blocks.GetStateMetadata(backState);
        if (isComparator && TryGetContainerAnalogSignal(backMeta, out var directContainerSig))
        {
            return directContainerSig;
        }

        var direct = engine.GetDirectOrWireSignalFromNeighbor(backPos, diodeFacing, getBlock);
        if (direct > 0)
        {
            return direct;
        }

        // Crucial vanilla diode rule: Repeaters & Comparators read ANY signal powering an opaque solid block directly behind them!
        if (backMeta.IsSolid && backMeta.IsOpaque)
        {
            var blockPow = engine.GetPowerReceivedAt(backPos, getBlock, ignoreSource: diodePos);
            if (isComparator)
            {
                var farBackMeta = blocks.GetStateMetadata(getBlock(backPos.Offset(diodeFacing.Opposite())));
                if (blockPow < 15 && TryGetContainerAnalogSignal(farBackMeta, out var throughBlockContainer))
                {
                    return Math.Max(blockPow, throughBlockContainer);
                }
            }

            return blockPow;
        }

        return 0;
    }

    /// <summary>Computes the <c>0..15</c> output signal of a <c>RedstoneComparator</c> at <paramref name="pos"/>.</summary>
    public static int ComputeComparatorOutput(
        IBlockRegistry blocks,
        RedstoneEngine engine,
        BlockPos pos,
        BlockStateMetadata meta,
        Func<BlockPos, BlockState> getBlock)
    {
        var facing = BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(meta, "north"));
        var rear = ComputeDiodeRearInput(blocks, engine, pos, facing, isComparator: true, getBlock);
        if (rear <= 0)
        {
            return 0;
        }

        var sideA = facing.GetAxis() == Axis.Z ? Direction.West : Direction.North;
        var sideB = sideA.Opposite();
        var maxSide = Math.Max(
            ComputeComparatorSideInput(blocks, pos.Offset(sideA), sideA.Opposite(), getBlock),
            ComputeComparatorSideInput(blocks, pos.Offset(sideB), sideB.Opposite(), getBlock));

        var subtract = meta.Properties.TryGetValue("subtract", out var sv) && string.Equals(sv, "true", StringComparison.OrdinalIgnoreCase);
        if (subtract)
        {
            return Math.Max(0, rear - maxSide);
        }

        return rear >= maxSide ? rear : 0;
    }

    private static int ComputeComparatorSideInput(
        IBlockRegistry blocks,
        BlockPos sidePos,
        Direction towardComparator,
        Func<BlockPos, BlockState> getBlock)
    {
        var st = getBlock(sidePos);
        if (st.IsAir) return 0;
        var m = blocks.GetStateMetadata(st);
        if (m.BlockKey == BuiltInBlocks.RedstoneWire)
        {
            return ParseInt(m, "power", 0);
        }

        if (m.BlockKey == BuiltInBlocks.RedstoneBlock)
        {
            return 15;
        }

        if (m.BlockKey == BuiltInBlocks.RedstoneRepeater && IsTrue(m, "powered"))
        {
            var f = BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(m, "north"));
            return f == towardComparator ? 15 : 0;
        }

        if (m.BlockKey == BuiltInBlocks.RedstoneComparator)
        {
            var f = BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(m, "north"));
            return f == towardComparator ? ParseInt(m, "power", 0) : 0;
        }

        return 0;
    }

    private static bool TryGetContainerAnalogSignal(BlockStateMetadata meta, out int signal)
    {
        if (meta.BlockKey == BuiltInBlocks.Composter)
        {
            signal = Math.Clamp(ParseInt(meta, "level", 0), 0, 8);
            return true;
        }

        if (meta.BlockKey == BuiltInBlocks.LitFurnace)
        {
            signal = 8;
            return true;
        }

        if (meta.BlockKey == BuiltInBlocks.Chest || meta.BlockKey == BuiltInBlocks.Barrel)
        {
            signal = 5;
            return true;
        }

        signal = 0;
        return false;
    }

    private static bool IsPoweredDiodePointingToward(
        IBlockRegistry blocks,
        BlockPos neighborPos,
        Direction towardRepeater,
        Func<BlockPos, BlockState> getBlock)
    {
        var m = blocks.GetStateMetadata(getBlock(neighborPos));
        if (m.BlockKey == BuiltInBlocks.RedstoneRepeater && IsTrue(m, "powered"))
        {
            return BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(m, "north")) == towardRepeater;
        }

        if (m.BlockKey == BuiltInBlocks.RedstoneComparator && ParseInt(m, "power", 0) > 0)
        {
            return BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(m, "north")) == towardRepeater;
        }

        return false;
    }

    private static bool ConnectsHorizontallyToWire(BlockStateMetadata meta, Direction fromWireToNeighbor)
    {
        var k = meta.BlockKey;
        if (k == BuiltInBlocks.RedstoneWire || k == BuiltInBlocks.RedstoneBlock ||
            k == BuiltInBlocks.RedstoneTorch || k == BuiltInBlocks.RedstoneTorchOff ||
            k == BuiltInBlocks.Lever || k == BuiltInBlocks.StoneButton ||
            k == BuiltInBlocks.StonePressurePlate || k == BuiltInBlocks.RedstoneComparator ||
            k == BuiltInBlocks.RedstoneLamp || k == BuiltInBlocks.LitRedstoneLamp ||
            k == BuiltInBlocks.Piston || k == BuiltInBlocks.StickyPiston ||
            k == BuiltInBlocks.Tnt || k == BuiltInBlocks.PoweredRail || k == BuiltInBlocks.OakTrapdoor)
        {
            return true;
        }

        if (k == BuiltInBlocks.RedstoneRepeater)
        {
            var repDir = BlockAttachmentAndShapeRules.ParseDirection(BlockAttachmentAndShapeRules.GetFacing(meta, "north"));
            return repDir.GetAxis() == fromWireToNeighbor.GetAxis();
        }

        return false;
    }

    private static bool IsTrue(BlockStateMetadata meta, string prop) =>
        meta.Properties.TryGetValue(prop, out var v) && string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);

    private static int ParseInt(BlockStateMetadata meta, string prop, int fallback) =>
        meta.Properties.TryGetValue(prop, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
}
