using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Survival;

/// <summary>
/// Clean-room Nether Portal rules shared by the client (single-player) and the authoritative server:
/// Obsidian frame detection, portal ignition, coordinate scaling, and arrival-site construction.
/// </summary>
public static class NetherPortalRules
{
    /// <summary>Smallest legal interior width of a portal opening (<c>2</c>).</summary>
    public const int MinPortalWidth = 2;

    /// <summary>Smallest legal interior height of a portal opening (<c>3</c>).</summary>
    public const int MinPortalHeight = 3;

    /// <summary>Largest legal interior width of a portal opening (<c>21</c>).</summary>
    public const int MaxPortalWidth = 21;

    /// <summary>Largest legal interior height of a portal opening (<c>21</c>).</summary>
    public const int MaxPortalHeight = 21;

    /// <summary>Axis of the framed portal built at every arrival site (<c>"z"</c>: the plane spans X).</summary>
    public const string ArrivalPortalAxis = "z";

    /// <summary>
    /// Determines whether <paramref name="state"/> is a lit Nether Portal block.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="state">Block state to test.</param>
    /// <returns><see langword="true"/> when the block is <c>overhorizon:nether_portal</c>.</returns>
    public static bool IsPortal(IBlockRegistry blocks, BlockState state) =>
        blocks.GetStateMetadata(state).BlockKey == BuiltInBlocks.NetherPortal;

    /// <summary>
    /// Attempts to detect and light an Obsidian portal frame whose interior contains <paramref name="seed"/>.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="getBlock">World block reader.</param>
    /// <param name="setBlock">World block writer invoked once per interior cell.</param>
    /// <param name="seed">Interior seed coordinate (any air/portal cell of the frame).</param>
    /// <param name="info">Detected frame geometry when the frame is valid.</param>
    /// <returns><see langword="true"/> when a valid frame was found and lit.</returns>
    public static bool TryActivateFrame(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        BlockPos seed,
        out PortalFrameInfo info)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);

        if (TryActivateFrameAlongAxis(blocks, getBlock, setBlock, seed, "z", out info))
        {
            return true;
        }

        return TryActivateFrameAlongAxis(blocks, getBlock, setBlock, seed, "x", out info);
    }

    /// <summary>
    /// Builds a fully framed <c>2x3</c> lit portal (Obsidian ring included) at <paramref name="bottomCenter"/>,
    /// together with a two-block-deep Obsidian landing pad and open walkway on both faces so the gateway can
    /// actually be walked into instead of being sealed inside a solid cube of frame blocks.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="setBlock">World block writer.</param>
    /// <param name="bottomCenter">Bottom-center interior coordinate of the new portal.</param>
    /// <param name="axis">Portal plane orientation (<c>"x"</c> or <c>"z"</c>).</param>
    public static void BuildFramedPortal(
        IBlockRegistry blocks,
        Action<BlockPos, BlockState> setBlock,
        BlockPos bottomCenter,
        string axis)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(setBlock);

        var obsidian = blocks.GetDefaultState(BuiltInBlocks.Obsidian);
        var portal = WithAxis(blocks, axis);
        var air = BlockState.Air;

        var spanZ = string.Equals(axis, "x", StringComparison.Ordinal);
        var min = bottomCenter;
        var max = new BlockPos(
            bottomCenter.X + (spanZ ? 0 : 1),
            bottomCenter.Y + 2,
            bottomCenter.Z + (spanZ ? 1 : 0));

        var span = spanZ ? max.Z - min.Z : max.X - min.X;

        // The Obsidian frame is the rectangle border that lives INSIDE the portal plane. Filling the whole
        // 3x5x3 cube (the old behaviour) sealed the gateway behind frame blocks and made arrival portals
        // impossible to walk back into.
        for (var y = min.Y - 1; y <= max.Y + 1; y++)
        {
            for (var h = -1; h <= span + 1; h++)
            {
                var insideOpening = y >= min.Y && y <= max.Y && h >= 0 && h <= span;
                var px = spanZ ? min.X : min.X + h;
                var pz = spanZ ? min.Z + h : min.Z;
                setBlock(new BlockPos(px, y, pz), insideOpening ? portal : obsidian);
            }
        }

        // Landing pads: a solid floor plus three blocks of headroom on both faces of the plane.
        var nx = spanZ ? 1 : 0;
        var nz = spanZ ? 0 : 1;
        for (var side = -1; side <= 1; side += 2)
        {
            for (var step = 1; step <= 2; step++)
            {
                for (var h = 0; h <= span; h++)
                {
                    var px = (spanZ ? min.X : min.X + h) + (nx * side * step);
                    var pz = (spanZ ? min.Z + h : min.Z) + (nz * side * step);

                    setBlock(new BlockPos(px, min.Y - 1, pz), obsidian);
                    for (var dy = 0; dy <= 2; dy++)
                    {
                        setBlock(new BlockPos(px, min.Y + dy, pz), air);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns the Nether Portal state carrying <paramref name="axis"/>.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="axis">Portal plane orientation (<c>"x"</c> or <c>"z"</c>).</param>
    /// <returns>Portal block state.</returns>
    public static BlockState WithAxis(IBlockRegistry blocks, string axis)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var value = string.Equals(axis, "z", StringComparison.Ordinal) ? "z" : "x";
        return blocks.WithProperty(blocks.GetDefaultState(BuiltInBlocks.NetherPortal), "axis", value);
    }

    /// <summary>
    /// Runs the complete arrival pipeline shared by single-player and the authoritative server: scales the
    /// traveller's feet coordinates into the opposite dimension (1 Nether step = <see cref="DimensionRules.NetherCoordinateScale"/>
    /// Overworld steps), resolves a safe landing, frames the return portal, and returns the spawn pose that
    /// leaves the traveller standing beside - never inside - the new gateway.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="getBlock">World block reader.</param>
    /// <param name="setBlock">World block writer.</param>
    /// <param name="isColumnLoaded">Predicate reporting whether a destination chunk is resident.</param>
    /// <param name="sourceFeet">Feet-level block coordinate the traveller departed from.</param>
    /// <param name="from">Dimension the traveller departed from.</param>
    /// <returns>The resolved arrival site for the opposite dimension.</returns>
    public static PortalArrivalSite CreateArrivalSite(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        Func<ChunkPos, bool> isColumnLoaded,
        BlockPos sourceFeet,
        DimensionId from)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);
        ArgumentNullException.ThrowIfNull(isColumnLoaded);

        var target = DimensionRules.Opposite(from);
        var desired = ScaleTravel(sourceFeet, from, target);
        var landing = ResolveArrivalSite(blocks, getBlock, setBlock, isColumnLoaded, desired, target);

        BuildFramedPortal(blocks, setBlock, landing, ArrivalPortalAxis);

        // Stand the traveller clear of the new frame: the plane spans X, so step back along +Z.
        var spawn = new Vec3d(landing.X + 0.5, landing.Y, landing.Z + 2.5);
        return new PortalArrivalSite(target, desired, landing, spawn);
    }

    /// <summary>
    /// Scales horizontal coordinates when travelling between dimensions (Nether travel compresses by <c>8</c>).
    /// </summary>
    /// <param name="source">Origin feet-level block coordinate.</param>
    /// <param name="from">Origin dimension.</param>
    /// <param name="to">Destination dimension.</param>
    /// <returns>Destination block coordinate with scaled X/Z and clamped Y.</returns>
    public static BlockPos ScaleTravel(BlockPos source, DimensionId from, DimensionId to)
    {
        var scale = DimensionRules.GetTravelScale(from, to);
        var x = (int)System.Math.Round(source.X * scale);
        var z = (int)System.Math.Round(source.Z * scale);
        return new BlockPos(x, DimensionRules.ClampWorldY(to, source.Y), z);
    }

    /// <summary>
    /// Searches loaded terrain for a safe standing position near <paramref name="desired"/> and, when none exists,
    /// carves a small platform so the arriving traveller never spawns inside rock or above a lava lake.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="getBlock">World block reader.</param>
    /// <param name="setBlock">World block writer.</param>
    /// <param name="isColumnLoaded">Predicate reporting whether a chunk is resident.</param>
    /// <param name="desired">Preferred arrival coordinate.</param>
    /// <param name="dimension">Destination dimension.</param>
    /// <returns>Safe feet block coordinate to place the player at.</returns>
    public static BlockPos ResolveArrivalSite(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        Func<ChunkPos, bool> isColumnLoaded,
        BlockPos desired,
        DimensionId dimension)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(getBlock);
        ArgumentNullException.ThrowIfNull(setBlock);
        ArgumentNullException.ThrowIfNull(isColumnLoaded);

        var highY = dimension == DimensionId.Nether ? 110 : 200;
        var lowY = dimension == DimensionId.Nether ? 36 : -60;

        for (var ring = 0; ring <= 8; ring++)
        {
            for (var dx = -ring; dx <= ring; dx++)
            {
                for (var dz = -ring; dz <= ring; dz++)
                {
                    if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz)) != ring)
                    {
                        continue;
                    }

                    var cx = desired.X + dx;
                    var cz = desired.Z + dz;
                    if (!isColumnLoaded(new ChunkPos(cx >> 4, cz >> 4)))
                    {
                        continue;
                    }

                    for (var y = highY; y >= lowY; y--)
                    {
                        var floor = new BlockPos(cx, y, cz);
                        var below = getBlock(floor);
                        if (below.IsAir || blocks.GetStateMetadata(below).IsFluid)
                        {
                            continue;
                        }

                        var feet = new BlockPos(cx, y + 1, cz);
                        var head = new BlockPos(cx, y + 2, cz);
                        var feetState = getBlock(feet);
                        var headState = getBlock(head);
                        var feetOpen = feetState.IsAir || IsPortal(blocks, feetState);
                        var headOpen = headState.IsAir || IsPortal(blocks, headState);
                        if (!feetOpen || !headOpen)
                        {
                            continue;
                        }

                        return feet;
                    }
                }
            }
        }

        return CarvePlatform(blocks, getBlock, setBlock, desired, dimension);
    }

    private static BlockPos CarvePlatform(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        BlockPos desired,
        DimensionId dimension)
    {
        var floorY = ResolveFallbackFloorY(blocks, getBlock, desired, dimension);
        var platformY = floorY - 1;
        var floorState = dimension == DimensionId.Nether
            ? blocks.GetDefaultState(BuiltInBlocks.Netherrack)
            : blocks.GetDefaultState(BuiltInBlocks.Stone);

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                setBlock(new BlockPos(desired.X + dx, platformY, desired.Z + dz), floorState);
                for (var dy = 1; dy <= 3; dy++)
                {
                    setBlock(new BlockPos(desired.X + dx, platformY + dy, desired.Z + dz), BlockState.Air);
                }
            }
        }

        return new BlockPos(desired.X, floorY, desired.Z);
    }

    /// <summary>
    /// Resolves the Y a carved fallback platform is built at. The Nether keeps the platform inside the buildable
    /// band above the lava sea; the Overworld lands the traveller on the first solid ground below the scaled
    /// altitude instead of on a floating slab at the Nether's Y.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    /// <param name="getBlock">World block reader.</param>
    /// <param name="desired">Preferred arrival coordinate.</param>
    /// <param name="dimension">Destination dimension.</param>
    /// <returns>World Y of the platform surface.</returns>
    private static int ResolveFallbackFloorY(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        BlockPos desired,
        DimensionId dimension)
    {
        var lowBound = dimension == DimensionId.Nether ? DimensionRules.NetherLavaSeaY + 6 : EngineConstants.WorldMinY + 4;
        var highBound = dimension == DimensionId.Nether ? DimensionRules.NetherCeilingY - 12 : EngineConstants.WorldMaxY - 8;

        for (var y = System.Math.Min(desired.Y + 8, highBound); y > lowBound; y--)
        {
            var below = getBlock(new BlockPos(desired.X, y, desired.Z));
            if (!below.IsAir && !blocks.GetStateMetadata(below).IsFluid)
            {
                return y + 1;
            }
        }

        return System.Math.Clamp(desired.Y, lowBound, highBound);
    }

    private static bool TryActivateFrameAlongAxis(
        IBlockRegistry blocks,
        Func<BlockPos, BlockState> getBlock,
        Action<BlockPos, BlockState> setBlock,
        BlockPos seed,
        string axis,
        out PortalFrameInfo info)
    {
        info = default;
        var spanZ = string.Equals(axis, "x", StringComparison.Ordinal);

        static BlockPos At(int x, int y, int z) => new(x, y, z);

        BlockPos Offset(BlockPos p, int h) => spanZ
            ? At(p.X, p.Y, p.Z + h)
            : At(p.X + h, p.Y, p.Z);

        var bottom = seed.Y;
        while (seed.Y - bottom < MaxPortalHeight && IsOpen(blocks, getBlock(At(seed.X, bottom - 1, seed.Z))))
        {
            bottom--;
        }

        var top = seed.Y;
        while (top - seed.Y < MaxPortalHeight && IsOpen(blocks, getBlock(At(seed.X, top + 1, seed.Z))))
        {
            top++;
        }

        // Both horizontal scans are hard-bounded: an entirely empty region (all air) must terminate
        // after MaxPortalWidth steps instead of walking the whole world line forever.
        var left = seed;
        for (var steps = 0; steps < MaxPortalWidth - 1 && IsOpen(blocks, getBlock(Offset(left, -1))); steps++)
        {
            left = Offset(left, -1);
        }

        var right = seed;
        for (var steps = 0; steps < MaxPortalWidth - 1 && IsOpen(blocks, getBlock(Offset(right, 1))); steps++)
        {
            right = Offset(right, 1);
        }

        var width = (spanZ ? right.Z - left.Z : right.X - left.X) + 1;
        var height = (top - bottom) + 1;
        if (width < MinPortalWidth || width > MaxPortalWidth || height < MinPortalHeight || height > MaxPortalHeight)
        {
            return false;
        }

        var minX = spanZ ? seed.X : left.X;
        var maxX = spanZ ? seed.X : right.X;
        var minZ = spanZ ? left.Z : seed.Z;
        var maxZ = spanZ ? right.Z : seed.Z;

        // Every cell of the opening must be air/portal, and the surrounding Obsidian ring (corners optional) must be solid.
        for (var y = bottom; y <= top; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                for (var z = minZ; z <= maxZ; z++)
                {
                    if (!IsOpen(blocks, getBlock(At(x, y, z))))
                    {
                        return false;
                    }
                }
            }
        }

        // The frame ring lives entirely inside the portal plane, so only the two axes spanning that plane are scanned.
        for (var y = bottom - 1; y <= top + 1; y++)
        {
            for (var h = -1; h <= width; h++)
            {
                var insideY = y >= bottom && y <= top;
                var insideH = h >= 0 && h < width;
                if (insideY && insideH)
                {
                    continue;
                }

                // Frame corners are optional, exactly like vanilla.
                if (!insideY && !insideH)
                {
                    continue;
                }

                var rx = spanZ ? seed.X : minX + h;
                var rz = spanZ ? minZ + h : seed.Z;
                var frame = getBlock(At(rx, y, rz));
                if (blocks.GetStateMetadata(frame).BlockKey != BuiltInBlocks.Obsidian)
                {
                    return false;
                }
            }
        }

        var portal = WithAxis(blocks, axis);
        for (var y = bottom; y <= top; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                for (var z = minZ; z <= maxZ; z++)
                {
                    setBlock(At(x, y, z), portal);
                }
            }
        }

        info = new PortalFrameInfo(At(minX, bottom, minZ), At(maxX, top, maxZ), axis);
        return true;
    }

    private static bool IsOpen(IBlockRegistry blocks, BlockState state) =>
        state.IsAir || IsPortal(blocks, state);
}
