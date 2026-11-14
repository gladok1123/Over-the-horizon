using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Gameplay.Survival;

/// <summary>
/// Enforces authentic Minecraft sub-voxel selection hitboxes, wall-only Ladder placement, floor/wall Torch &amp; Lever mounting,
/// 2-block Bed placement, Piston orientation, and support-break cascading when a supporting block is destroyed.
/// </summary>
public static partial class BlockAttachmentAndShapeRules
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

        if (key == BuiltInBlocks.RedstoneWire || key == BuiltInBlocks.Rail || key == BuiltInBlocks.PoweredRail || key == BuiltInBlocks.StonePressurePlate)
        {
            if ((key == BuiltInBlocks.Rail || key == BuiltInBlocks.PoweredRail) &&
                RailPlacementRules.GetRailShape(meta).StartsWith("ascending_", StringComparison.Ordinal))
            {
                localBounds = new BoundingBox(0.0, 0.0, 0.0, 1.0, 0.625, 1.0);
                return true;
            }

            localBounds = new BoundingBox(0.0, 0.0, 0.0, 1.0, 0.125, 1.0);
            return true;
        }

        if (key == BuiltInBlocks.RedstoneRepeater || key == BuiltInBlocks.RedstoneComparator)
        {
            localBounds = new BoundingBox(0.0, 0.0, 0.0, 1.0, 0.1875, 1.0);
            return true;
        }

        if (key == BuiltInBlocks.OakTrapdoor || key == BuiltInBlocks.IronTrapdoor)
        {
            var isOpen = meta.Properties.TryGetValue("open", out var ov) && string.Equals(ov, "true", StringComparison.OrdinalIgnoreCase);
            var isTop = meta.Properties.TryGetValue("half", out var hv) && string.Equals(hv, "top", StringComparison.OrdinalIgnoreCase);
            if (!isOpen)
            {
                localBounds = isTop
                    ? new BoundingBox(0.0, 0.8125, 0.0, 1.0, 1.0, 1.0)
                    : new BoundingBox(0.0, 0.0, 0.0, 1.0, 0.1875, 1.0);
                return true;
            }

            localBounds = GetFacing(meta, "north") switch
            {
                "north" => new BoundingBox(0.0, 0.0, 0.8125, 1.0, 1.0, 1.0),
                "south" => new BoundingBox(0.0, 0.0, 0.0, 1.0, 1.0, 0.1875),
                "west" => new BoundingBox(0.8125, 0.0, 0.0, 1.0, 1.0, 1.0),
                _ => new BoundingBox(0.0, 0.0, 0.0, 0.1875, 1.0, 1.0),
            };
            return true;
        }

        if (key == BuiltInBlocks.OakDoor || key == BuiltInBlocks.IronDoor)
        {
            var facing = GetFacing(meta, "north");
            var isOpen = meta.Properties.TryGetValue("open", out var ov) && string.Equals(ov, "true", StringComparison.OrdinalIgnoreCase);
            var rightHinge = meta.Properties.TryGetValue("hinge", out var hg) && string.Equals(hg, "right", StringComparison.OrdinalIgnoreCase);
            var edge = facing;
            if (isOpen)
            {
                edge = facing switch
                {
                    "north" => rightHinge ? "east" : "west",
                    "south" => rightHinge ? "west" : "east",
                    "west" => rightHinge ? "north" : "south",
                    _ => rightHinge ? "south" : "north",
                };
            }

            localBounds = edge switch
            {
                "north" => new BoundingBox(0.0, 0.0, 0.0, 1.0, 1.0, 0.1875),
                "south" => new BoundingBox(0.0, 0.0, 0.8125, 1.0, 1.0, 1.0),
                "west" => new BoundingBox(0.0, 0.0, 0.0, 0.1875, 1.0, 1.0),
                _ => new BoundingBox(0.8125, 0.0, 0.0, 1.0, 1.0, 1.0),
            };
            return true;
        }

        if (key == BuiltInBlocks.AmethystCluster)
        {
            localBounds = GetFacing(meta, "up") switch
            {
                "down" => new BoundingBox(0.1875, 0.3125, 0.1875, 0.8125, 1.0, 0.8125),
                "north" => new BoundingBox(0.1875, 0.1875, 0.3125, 0.8125, 0.8125, 1.0),
                "south" => new BoundingBox(0.1875, 0.1875, 0.0, 0.8125, 0.8125, 0.6875),
                "west" => new BoundingBox(0.3125, 0.1875, 0.1875, 1.0, 0.8125, 0.8125),
                "east" => new BoundingBox(0.0, 0.1875, 0.1875, 0.6875, 0.8125, 0.8125),
                _ => new BoundingBox(0.1875, 0.0, 0.1875, 0.8125, 0.6875, 0.8125),
            };
            return true;
        }

        if (key == BuiltInBlocks.DirtPath || key == BuiltInBlocks.Farmland)
        {
            localBounds = new BoundingBox(0.0, 0.0, 0.0, 1.0, 0.9375, 1.0);
            return true;
        }

        if (key == BuiltInBlocks.OakFence)
        {
            localBounds = new BoundingBox(0.25, 0.0, 0.25, 0.75, 1.0, 0.75);
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
            var hanging = meta.Properties.TryGetValue("hanging", out var hv) && string.Equals(hv, "true", StringComparison.OrdinalIgnoreCase);
            localBounds = hanging
                ? new BoundingBox(0.3125, 0.0625, 0.3125, 0.6875, 1.0, 0.6875)
                : new BoundingBox(0.3125, 0.0, 0.3125, 0.6875, 0.6875, 0.6875);
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
        out (BlockPos Pos, BlockState State)? secondaryBlock,
        double hitPointLocalY = 0.0)
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

        // 3. LANTERN: Floor-mounted (hanging=false) OR ceiling-hung (hanging=true)!
        if (blockKey == BuiltInBlocks.Lantern)
        {
            var canFloor = IsFullSupportBlock(blocks, getBlock(placePos.Offset(Direction.Down)));
            var canCeiling = IsFullSupportBlock(blocks, getBlock(placePos.Offset(Direction.Up)));
            if (clickedFace == Direction.Down && canCeiling)
            {
                resolvedState = blocks.WithProperty(resolvedState, "hanging", "true");
                return true;
            }

            if (canFloor)
            {
                resolvedState = blocks.WithProperty(resolvedState, "hanging", "false");
                return true;
            }

            if (canCeiling)
            {
                resolvedState = blocks.WithProperty(resolvedState, "hanging", "true");
                return true;
            }

            return false;
        }

        // 4. FLOOR-SUPPORTED FLAT BLOCKS: Redstone Wire, Repeater, Comparator, Pressure Plate, Rail & PoweredRail
        if (blockKey == BuiltInBlocks.RedstoneWire ||
            blockKey == BuiltInBlocks.RedstoneRepeater ||
            blockKey == BuiltInBlocks.RedstoneComparator ||
            blockKey == BuiltInBlocks.StonePressurePlate ||
            blockKey == BuiltInBlocks.Rail ||
            blockKey == BuiltInBlocks.PoweredRail)
        {
            if (!IsFullSupportBlock(blocks, getBlock(placePos.Offset(Direction.Down))))
            {
                return false;
            }

            if (blockKey == BuiltInBlocks.RedstoneRepeater || blockKey == BuiltInBlocks.RedstoneComparator)
            {
                var horiz = HorizontalLookDirection(playerForward);
                resolvedState = blocks.WithProperty(resolvedState, "facing", DirectionName(horiz));
            }
            else if (RailPlacementRules.IsRailBlock(blockKey))
            {
                resolvedState = RailPlacementRules.ResolveRailState(blocks, getBlock, blockKey, placePos, playerForward);
            }

            return true;
        }

        // 5. 6-WAY DIRECTIONAL BLOCKS: Piston, Sticky Piston, and Barrel orient their top/head toward the player!
        if (blockKey == BuiltInBlocks.Piston || blockKey == BuiltInBlocks.StickyPiston || blockKey == BuiltInBlocks.Barrel)
        {
            var lookSix = SixWayLookDirection(playerForward).Opposite();
            resolvedState = blocks.WithProperty(resolvedState, "facing", DirectionName(lookSix));
            return true;
        }

        // 6. PILLAR / AXIS BLOCKS: Oak/Birch/Cherry/Spruce Logs and Hay Block align along the clicked voxel face axis like vanilla!
        if (blockKey == BuiltInBlocks.OakLog || blockKey == BuiltInBlocks.BirchLog ||
            blockKey == BuiltInBlocks.CherryLog || blockKey == BuiltInBlocks.SpruceLog ||
            blockKey == BuiltInBlocks.HayBlock)
        {
            var axisToken = clickedFace.GetAxis() switch
            {
                Axis.X => "x",
                Axis.Z => "z",
                _ => "y",
            };
            resolvedState = blocks.WithProperty(resolvedState, "axis", axisToken);
            return true;
        }

        // 6b. AMETHYST CLUSTER: Mounts onto any solid face along clickedFace!
        if (blockKey == BuiltInBlocks.AmethystCluster)
        {
            var supportPos = placePos.Offset(clickedFace.Opposite());
            if (!IsFullSupportBlock(blocks, getBlock(supportPos)))
            {
                return false;
            }

            resolvedState = blocks.WithProperty(resolvedState, "facing", DirectionName(clickedFace));
            return true;
        }

        // 7. HORIZONTAL FRONT-FACING BLOCKS: Furnace, Lit Furnace, Chest, and Oak/Iron Trapdoors!
        if (blockKey == BuiltInBlocks.Furnace || blockKey == BuiltInBlocks.LitFurnace ||
            blockKey == BuiltInBlocks.Chest || blockKey == BuiltInBlocks.OakTrapdoor || blockKey == BuiltInBlocks.IronTrapdoor)
        {
            var frontDir = HorizontalLookDirection(playerForward).Opposite();
            resolvedState = blocks.WithProperty(resolvedState, "facing", DirectionName(frontDir));
            if (blockKey == BuiltInBlocks.OakTrapdoor || blockKey == BuiltInBlocks.IronTrapdoor)
            {
                var placeTop = clickedFace == Direction.Down ||
                               (clickedFace.GetAxis() != Axis.Y && hitPointLocalY >= 0.5);
                resolvedState = blocks.WithProperty(resolvedState, "half", placeTop ? "top" : "bottom");
            }

            return true;
        }

        // 7b. 2-BLOCK HINGED DOORS: Oak Door & Iron Door (lower half at placePos, upper half at placePos + Up)
        if (blockKey == BuiltInBlocks.OakDoor || blockKey == BuiltInBlocks.IronDoor)
        {
            var upperPos = placePos.Offset(Direction.Up);
            var upperExisting = getBlock(upperPos);
            var upperMeta = blocks.GetStateMetadata(upperExisting);
            if ((!upperExisting.IsAir && !upperMeta.IsFluid && upperMeta.BlockKey != BuiltInBlocks.Fire) ||
                !IsFullSupportBlock(blocks, getBlock(placePos.Offset(Direction.Down))))
            {
                return false;
            }

            var doorDir = HorizontalLookDirection(playerForward);
            var fName = DirectionName(doorDir);
            var rightNeighborDir = RotateClockwise(doorDir);
            var rightNeighborKey = blocks.GetStateMetadata(getBlock(placePos.Offset(rightNeighborDir))).BlockKey;
            var hinge = rightNeighborKey == blockKey ? "left" : "right";
            var leftNeighborKey = blocks.GetStateMetadata(getBlock(placePos.Offset(rightNeighborDir.Opposite()))).BlockKey;
            if (leftNeighborKey == blockKey)
            {
                hinge = "right";
            }
            else if (rightNeighborKey != blockKey)
            {
                hinge = "left";
            }

            resolvedState = blocks.WithProperty(blocks.WithProperty(blocks.WithProperty(resolvedState, "facing", fName), "half", "lower"), "hinge", hinge);
            var upperState = blocks.WithProperty(blocks.WithProperty(blocks.WithProperty(blocks.GetDefaultState(blockKey), "facing", fName), "half", "upper"), "hinge", hinge);
            secondaryBlock = (upperPos, upperState);
            return true;
        }

        // 8. RED BED: Places 2 horizontal blocks (foot at placePos, head 1 block ahead along player horizontal look)
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
}
