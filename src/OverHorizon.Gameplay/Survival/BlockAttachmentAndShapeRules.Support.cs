using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Survival;

/// <summary>
/// Support-integrity checks and directional vector helpers for <see cref="BlockAttachmentAndShapeRules"/>.
/// </summary>
public static partial class BlockAttachmentAndShapeRules
{
    /// <summary>
    /// Checks whether an attached block at <paramref name="pos"/> still has its required supporting block intact.
    /// </summary>
    public static bool IsStillSupported(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        BlockPos pos,
        BlockState state)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(getBlock);

        if (state.IsAir)
        {
            return true;
        }

        var meta = blocks.GetStateMetadata(state);
        var key = meta.BlockKey;

        if (key == BuiltInBlocks.Ladder)
        {
            var wallDir = ParseDirection(GetFacing(meta, "north")).Opposite();
            return IsFullSupportBlock(blocks, getBlock(pos.Offset(wallDir)));
        }

        if (key == BuiltInBlocks.Torch ||
            key == BuiltInBlocks.RedstoneTorch ||
            key == BuiltInBlocks.RedstoneTorchOff ||
            key == BuiltInBlocks.Lever ||
            key == BuiltInBlocks.StoneButton)
        {
            var facing = GetFacing(meta, "up");
            var supportDir = facing == "up" ? Direction.Down : ParseDirection(facing).Opposite();
            return IsFullSupportBlock(blocks, getBlock(pos.Offset(supportDir)));
        }

        if (key == BuiltInBlocks.Lantern)
        {
            var hanging = meta.Properties.TryGetValue("hanging", out var hv) && string.Equals(hv, "true", StringComparison.OrdinalIgnoreCase);
            return IsFullSupportBlock(blocks, getBlock(pos.Offset(hanging ? Direction.Up : Direction.Down)));
        }

        if (key == BuiltInBlocks.RedBed)
        {
            if (!IsFullSupportBlock(blocks, getBlock(pos.Offset(Direction.Down))))
            {
                return false;
            }

            return TryGetPartnerBedPosition(meta, pos, out var partnerPos, out var expectedPartnerPart) &&
                IsMatchingBedPartner(blocks, getBlock(partnerPos), GetFacing(meta, "north"), expectedPartnerPart);
        }

        if (key == BuiltInBlocks.OakDoor || key == BuiltInBlocks.IronDoor)
        {
            var isUpper = meta.Properties.TryGetValue("half", out var hv) && string.Equals(hv, "upper", StringComparison.OrdinalIgnoreCase);
            if (!isUpper && !IsFullSupportBlock(blocks, getBlock(pos.Offset(Direction.Down))))
            {
                return false;
            }

            var partnerPos = pos.Offset(isUpper ? Direction.Down : Direction.Up);
            var partnerState = getBlock(partnerPos);
            if (partnerState.IsAir)
            {
                return false;
            }

            var pm = blocks.GetStateMetadata(partnerState);
            var expectedHalf = isUpper ? "lower" : "upper";
            return pm.BlockKey == key &&
                pm.Properties.TryGetValue("half", out var ph) &&
                string.Equals(ph, expectedHalf, StringComparison.OrdinalIgnoreCase);
        }

        if (key == BuiltInBlocks.AmethystCluster)
        {
            var facingDir = ParseDirection(GetFacing(meta, "up"));
            return IsFullSupportBlock(blocks, getBlock(pos.Offset(facingDir.Opposite())));
        }

        if (key == BuiltInBlocks.RedstoneWire ||
            key == BuiltInBlocks.RedstoneRepeater ||
            key == BuiltInBlocks.RedstoneComparator ||
            key == BuiltInBlocks.StonePressurePlate ||
            key == BuiltInBlocks.Rail ||
            key == BuiltInBlocks.PoweredRail ||
            key == BuiltInBlocks.Fire)
        {
            return IsFullSupportBlock(blocks, getBlock(pos.Offset(Direction.Down)));
        }

        return true;
    }

    /// <summary>Resolves the partner cell and expected <c>part</c> (<c>head</c> or <c>foot</c>) of a 2-block <c>RedBed</c>.</summary>
    public static bool TryGetPartnerBedPosition(
        BlockStateMetadata meta,
        BlockPos pos,
        out BlockPos partnerPos,
        out string expectedPartnerPart)
    {
        if (meta.BlockKey != BuiltInBlocks.RedBed)
        {
            partnerPos = default;
            expectedPartnerPart = string.Empty;
            return false;
        }

        var facingDir = ParseDirection(GetFacing(meta, "north"));
        var isHead = meta.Properties.TryGetValue("part", out var p) && string.Equals(p, "head", StringComparison.OrdinalIgnoreCase);
        partnerPos = isHead ? pos.Offset(facingDir.Opposite()) : pos.Offset(facingDir);
        expectedPartnerPart = isHead ? "foot" : "head";
        return true;
    }

    /// <summary>Resolves the vertical partner cell and expected <c>half</c> (<c>upper</c> or <c>lower</c>) of a 2-block Door.</summary>
    public static bool TryGetPartnerDoorPosition(
        BlockStateMetadata meta,
        BlockPos pos,
        out BlockPos partnerPos,
        out string expectedPartnerHalf)
    {
        if (meta.BlockKey != BuiltInBlocks.OakDoor && meta.BlockKey != BuiltInBlocks.IronDoor)
        {
            partnerPos = default;
            expectedPartnerHalf = string.Empty;
            return false;
        }

        var isUpper = meta.Properties.TryGetValue("half", out var h) && string.Equals(h, "upper", StringComparison.OrdinalIgnoreCase);
        partnerPos = pos.Offset(isUpper ? Direction.Down : Direction.Up);
        expectedPartnerHalf = isUpper ? "lower" : "upper";
        return true;
    }

    /// <summary>Gets whether <paramref name="state"/> can support attached Torches, Ladders, or Redstone components.</summary>
    public static bool IsFullSupportBlock(IBlockRegistry blocks, BlockState state)
    {
        if (state.IsAir)
        {
            return false;
        }

        var meta = blocks.GetStateMetadata(state);
        return meta.IsSolid &&
            meta.BlockKey != BuiltInBlocks.Lantern &&
            meta.BlockKey != BuiltInBlocks.RedBed &&
            meta.BlockKey != BuiltInBlocks.OakDoor &&
            meta.BlockKey != BuiltInBlocks.IronDoor &&
            meta.BlockKey != BuiltInBlocks.OakTrapdoor &&
            meta.BlockKey != BuiltInBlocks.IronTrapdoor &&
            meta.BlockKey != BuiltInBlocks.OakFence &&
            meta.BlockKey != BuiltInBlocks.PistonHead;
    }

    /// <summary>Rotates a horizontal <see cref="Direction"/> 90 degrees clockwise around +Y.</summary>
    public static Direction RotateClockwise(Direction dir) => dir switch
    {
        Direction.North => Direction.East,
        Direction.East => Direction.South,
        Direction.South => Direction.West,
        _ => Direction.North,
    };

    /// <summary>Resolves the dominant horizontal <see cref="Direction"/> of a camera forward vector.</summary>
    public static Direction HorizontalLookDirection(Vector3 forward)
    {
        if (MathF.Abs(forward.X) > MathF.Abs(forward.Z))
        {
            return forward.X >= 0.0f ? Direction.East : Direction.West;
        }

        return forward.Z >= 0.0f ? Direction.South : Direction.North;
    }

    /// <summary>Resolves the dominant 6-way <see cref="Direction"/> of a camera forward vector.</summary>
    public static Direction SixWayLookDirection(Vector3 forward)
    {
        var ax = MathF.Abs(forward.X);
        var ay = MathF.Abs(forward.Y);
        var az = MathF.Abs(forward.Z);
        if (ay > ax && ay > az && ay > 0.65f)
        {
            return forward.Y >= 0.0f ? Direction.Up : Direction.Down;
        }

        return HorizontalLookDirection(forward);
    }

    /// <summary>Reads the <c>facing</c> state property or returns <paramref name="fallback"/>.</summary>
    public static string GetFacing(BlockStateMetadata meta, string fallback) =>
        meta.Properties.TryGetValue("facing", out var val) ? val : fallback;

    /// <summary>Converts a <see cref="Direction"/> into its lowercase property token.</summary>
    public static string DirectionName(Direction dir) => dir switch
    {
        Direction.North => "north",
        Direction.South => "south",
        Direction.West => "west",
        Direction.East => "east",
        Direction.Up => "up",
        _ => "down",
    };

    /// <summary>Parses a lowercase <c>facing</c> token back into a <see cref="Direction"/>.</summary>
    public static Direction ParseDirection(string facing) => facing switch
    {
        "north" => Direction.North,
        "south" => Direction.South,
        "west" => Direction.West,
        "east" => Direction.East,
        "up" => Direction.Up,
        _ => Direction.Down,
    };

    private static bool IsMatchingBedPartner(IBlockRegistry blocks, BlockState partnerState, string expectedFacing, string expectedPart)
    {
        if (partnerState.IsAir)
        {
            return false;
        }

        var pm = blocks.GetStateMetadata(partnerState);
        return pm.BlockKey == BuiltInBlocks.RedBed &&
            string.Equals(GetFacing(pm, "north"), expectedFacing, StringComparison.OrdinalIgnoreCase) &&
            pm.Properties.TryGetValue("part", out var pp) &&
            string.Equals(pp, expectedPart, StringComparison.OrdinalIgnoreCase);
    }
}
