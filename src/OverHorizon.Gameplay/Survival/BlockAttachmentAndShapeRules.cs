using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Survival;

/// <summary>
/// Enforces authentic Minecraft sub-voxel selection hitboxes, wall-only Ladder placement, floor/wall Torch &amp; Lever mounting,
/// 2-block Bed placement, Piston orientation, and support-break cascading when a supporting block is destroyed.
/// </summary>
public static class BlockAttachmentAndShapeRules
{
    private static readonly BoundingBox FullCube = new(0.0, 0.0, 0.0, 1.0, 1.0, 1.0);

    /// <summary>
    /// Computes the block-local (<c>[0..1]^3</c>) raycast selection box for <paramref name="state"/>, or returns <see langword="false"/> for Air/Water.
    /// </summary>
    public static bool TryGetLocalSelectionBox(
        IBlockRegistry blocks,
        BlockState state,
        out BoundingBox localBounds)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        if (state.IsAir)
        {
            localBounds = default;
            return false;
        }

        var meta = blocks.GetStateMetadata(state);
        if (meta.IsFluid)
        {
            localBounds = default;
            return false;
        }

        var key = meta.BlockKey;
        if (key == BuiltInBlocks.Torch || key == BuiltInBlocks.RedstoneTorch || key == BuiltInBlocks.RedstoneTorchOff)
        {
            var facing = GetFacing(meta, "up");
            localBounds = facing switch
            {
                "north" => new BoundingBox(0.34, 0.18, 0.62, 0.66, 0.82, 1.00),
                "south" => new BoundingBox(0.34, 0.18, 0.00, 0.66, 0.82, 0.38),
                "west" => new BoundingBox(0.62, 0.18, 0.34, 1.00, 0.82, 0.66),
                "east" => new BoundingBox(0.00, 0.18, 0.34, 0.38, 0.82, 0.66),
                _ => new BoundingBox(0.36, 0.00, 0.36, 0.64, 0.64, 0.64),
            };
            return true;
        }

        if (key == BuiltInBlocks.Ladder)
        {
            var facing = GetFacing(meta, "north");
            localBounds = facing switch
            {
                "north" => new BoundingBox(0.0, 0.0, 0.8125, 1.0, 1.0, 1.0),
                "south" => new BoundingBox(0.0, 0.0, 0.0, 1.0, 1.0, 0.1875),
                "west" => new BoundingBox(0.8125, 0.0, 0.0, 1.0, 1.0, 1.0),
                _ => new BoundingBox(0.0, 0.0, 0.0, 0.1875, 1.0, 1.0),
            };
            return true;
        }

        if (key == BuiltInBlocks.RedstoneWire || key == BuiltInBlocks.Rail || key == BuiltInBlocks.StonePressurePlate)
        {
            localBounds = new BoundingBox(0.0, 0.0, 0.0, 1.0, 0.125, 1.0);
            return true;
        }

        if (key == BuiltInBlocks.RedstoneRepeater)
        {
            localBounds = new BoundingBox(0.0, 0.0, 0.0, 1.0, 0.1875, 1.0);
            return true;
        }

        if (key == BuiltInBlocks.Lever || key == BuiltInBlocks.StoneButton)
        {
            var facing = GetFacing(meta, "up");
            localBounds = facing switch
            {
                "north" => new BoundingBox(0.25, 0.22, 0.68, 0.75, 0.78, 1.00),
                "south" => new BoundingBox(0.25, 0.22, 0.00, 0.75, 0.78, 0.32),
                "west" => new BoundingBox(0.68, 0.22, 0.25, 1.00, 0.78, 0.75),
                "east" => new BoundingBox(0.00, 0.22, 0.25, 0.32, 0.78, 0.75),
                _ => new BoundingBox(0.25, 0.00, 0.25, 0.75, 0.45, 0.75),
            };
            return true;
        }

        if (key == BuiltInBlocks.RedBed)
        {
            localBounds = new BoundingBox(0.0, 0.0, 0.0, 1.0, 0.5625, 1.0);
            return true;
        }

        if (key == BuiltInBlocks.Lantern)
        {
            localBounds = new BoundingBox(0.3125, 0.0, 0.3125, 0.6875, 0.5625, 0.6875);
            return true;
        }

        if (key == BuiltInBlocks.Fire)
        {
            localBounds = new BoundingBox(0.05, 0.0, 0.05, 0.95, 0.75, 0.95);
            return true;
        }

        localBounds = FullCube;
        return true;
    }

    /// <summary>
    /// Validates and resolves the exact <see cref="BlockState"/> (and optional second Bed block) when placing <paramref name="blockKey"/>
    /// at <paramref name="placePos"/> via <paramref name="clickedFace"/>.
    /// </summary>
    public static bool TryResolvePlacementState(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        ResourceKey blockKey,
        BlockPos placePos,
        Direction clickedFace,
        Vector3 playerForward,
        out BlockState resolvedState,
        out (BlockPos Pos, BlockState State)? secondaryBlock)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(getBlock);

        secondaryBlock = null;
        resolvedState = blocks.GetDefaultState(blockKey);

        var existingAtPlace = getBlock(placePos);
        var existingMeta = blocks.GetStateMetadata(existingAtPlace);
        if (!existingAtPlace.IsAir && !existingMeta.IsFluid && existingMeta.BlockKey != BuiltInBlocks.Fire)
        {
            return false;
        }

        // 1. STRICT LADDER RULE: Ladders can ONLY attach to vertical solid walls (North, South, West, East) — NEVER floor/ceiling/air!
        if (blockKey == BuiltInBlocks.Ladder)
        {
            if (clickedFace.GetAxis() == Axis.Y)
            {
                return false;
            }

            var wallPos = placePos.Offset(clickedFace.Opposite());
            if (!IsFullSupportBlock(blocks, getBlock(wallPos)))
            {
                return false;
            }

            resolvedState = blocks.WithProperty(resolvedState, "facing", DirectionName(clickedFace));
            return true;
        }

        // 2. TORCH, REDSTONE TORCH, LEVER & STONE BUTTON: Attach to solid floor ("up") or vertical solid wall ("north|south|west|east")!
        if (blockKey == BuiltInBlocks.Torch ||
            blockKey == BuiltInBlocks.RedstoneTorch ||
            blockKey == BuiltInBlocks.Lever ||
            blockKey == BuiltInBlocks.StoneButton)
        {
            if (clickedFace == Direction.Up)
            {
                if (!IsFullSupportBlock(blocks, getBlock(placePos.Offset(Direction.Down))))
                {
                    return false;
                }

                resolvedState = blocks.WithProperty(resolvedState, "facing", "up");
                return true;
            }

            if (clickedFace.GetAxis() != Axis.Y)
            {
                var wallPos = placePos.Offset(clickedFace.Opposite());
                if (IsFullSupportBlock(blocks, getBlock(wallPos)))
                {
                    resolvedState = blocks.WithProperty(resolvedState, "facing", DirectionName(clickedFace));
                    return true;
                }
            }

            // Clicking underside of ceiling: allow floor mount ONLY if there is solid ground right below placePos!
            if (IsFullSupportBlock(blocks, getBlock(placePos.Offset(Direction.Down))))
            {
                resolvedState = blocks.WithProperty(resolvedState, "facing", "up");
                return true;
            }

            return false;
        }

        // 3. FLOOR-SUPPORTED FLAT BLOCKS: Redstone Wire, Repeater, Pressure Plate, Rail, Lantern
        if (blockKey == BuiltInBlocks.RedstoneWire ||
            blockKey == BuiltInBlocks.RedstoneRepeater ||
            blockKey == BuiltInBlocks.StonePressurePlate ||
            blockKey == BuiltInBlocks.Rail ||
            blockKey == BuiltInBlocks.Lantern)
        {
            if (!IsFullSupportBlock(blocks, getBlock(placePos.Offset(Direction.Down))))
            {
                return false;
            }

            if (blockKey == BuiltInBlocks.RedstoneRepeater)
            {
                var horiz = HorizontalLookDirection(playerForward);
                resolvedState = blocks.WithProperty(resolvedState, "facing", DirectionName(horiz));
            }

            return true;
        }

        // 4. PISTON & STICKY PISTON: Face toward the player along all 6 directions!
        if (blockKey == BuiltInBlocks.Piston || blockKey == BuiltInBlocks.StickyPiston)
        {
            var lookSix = SixWayLookDirection(playerForward).Opposite();
            resolvedState = blocks.WithProperty(resolvedState, "facing", DirectionName(lookSix));
            return true;
        }

        // 5. RED BED: Places 2 horizontal blocks (foot at placePos, head 1 block ahead along player horizontal look)
        if (blockKey == BuiltInBlocks.RedBed)
        {
            var horiz = HorizontalLookDirection(playerForward);
            var headPos = placePos.Offset(horiz);
            var headExisting = getBlock(headPos);
            var headMeta = blocks.GetStateMetadata(headExisting);
            if ((!headExisting.IsAir && !headMeta.IsFluid && headMeta.BlockKey != BuiltInBlocks.Fire) ||
                !IsFullSupportBlock(blocks, getBlock(placePos.Offset(Direction.Down))) ||
                !IsFullSupportBlock(blocks, getBlock(headPos.Offset(Direction.Down))))
            {
                return false;
            }

            var fName = DirectionName(horiz);
            resolvedState = blocks.WithProperty(blocks.WithProperty(resolvedState, "facing", fName), "part", "foot");
            var headState = blocks.WithProperty(blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.RedBed), "facing", fName), "part", "head");
            secondaryBlock = (headPos, headState);
            return true;
        }

        return true;
    }

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

        if (key == BuiltInBlocks.RedstoneWire ||
            key == BuiltInBlocks.RedstoneRepeater ||
            key == BuiltInBlocks.StonePressurePlate ||
            key == BuiltInBlocks.Rail ||
            key == BuiltInBlocks.Fire)
        {
            return IsFullSupportBlock(blocks, getBlock(pos.Offset(Direction.Down)));
        }

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
            meta.BlockKey != BuiltInBlocks.PistonHead;
    }

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
}
