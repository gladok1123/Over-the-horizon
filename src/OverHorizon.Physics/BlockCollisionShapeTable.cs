using System.Collections.Generic;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;

namespace OverHorizon.Physics;

/// <summary>
/// Compact value-type sub-voxel collision box in block-local <c>[0..1]</c> (or <c>[0..1.5]</c> fence) space.
/// </summary>
/// <param name="MinX">Minimum local X offset.</param>
/// <param name="MinY">Minimum local Y offset.</param>
/// <param name="MinZ">Minimum local Z offset.</param>
/// <param name="MaxX">Maximum local X offset.</param>
/// <param name="MaxY">Maximum local Y offset.</param>
/// <param name="MaxZ">Maximum local Z offset.</param>
/// <param name="HasCollision">Whether this state blocks entity movement.</param>
public readonly record struct LocalCollisionBox(
    float MinX,
    float MinY,
    float MinZ,
    float MaxX,
    float MaxY,
    float MaxZ,
    bool HasCollision)
{
    /// <summary>Empty passable box.</summary>
    public static readonly LocalCollisionBox Empty = new(0f, 0f, 0f, 0f, 0f, 0f, HasCollision: false);

    /// <summary>Full <c>1x1x1</c> solid voxel box.</summary>
    public static readonly LocalCollisionBox FullCube = new(0f, 0f, 0f, 1f, 1f, 1f, HasCollision: true);

    /// <summary>Translates this local box into world-space 64-bit <see cref="BoundingBox"/> coordinates at <paramref name="pos"/>.</summary>
    /// <param name="pos">World-space voxel coordinate.</param>
    public BoundingBox ToWorldBox(BlockPos pos) =>
        new(
            pos.X + MinX,
            pos.Y + MinY,
            pos.Z + MinZ,
            pos.X + MaxX,
            pos.Y + MaxY,
            pos.Z + MaxZ);
}

/// <summary>
/// Precomputed O(1) flat lookup table mapping interned <see cref="BlockState"/> IDs to exact sub-voxel collision boxes
/// for hinged Doors, Trapdoors, Fences (<c>1.5</c> height), Dirt Paths / Farmland (<c>15/16</c>), and Beds (<c>9/16</c>).
/// </summary>
public sealed class BlockCollisionShapeTable
{
    private const float SlabThickness = 3f / 16f;
    private readonly LocalCollisionBox[] _shapes;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockCollisionShapeTable"/> class for <paramref name="blocks"/>.
    /// </summary>
    /// <param name="blocks">Frozen block registry.</param>
    public BlockCollisionShapeTable(IBlockRegistry blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        _shapes = new LocalCollisionBox[blocks.StateCount];
        for (var id = 0; id < _shapes.Length; id++)
        {
            var meta = blocks.GetStateMetadata(new BlockState((ushort)id));
            _shapes[id] = ComputeShape(meta);
        }
    }

    /// <summary>
    /// Resolves the world-space <see cref="BoundingBox"/> for <paramref name="state"/> at <paramref name="pos"/> in O(1) time.
    /// </summary>
    /// <param name="state">Block state at <paramref name="pos"/>.</param>
    /// <param name="pos">Voxel coordinate.</param>
    /// <param name="box">Resolved world-space collision box when solid.</param>
    /// <returns><see langword="true"/> when <paramref name="state"/> has a solid collision shape; otherwise <see langword="false"/>.</returns>
    public bool TryGetWorldBox(BlockState state, BlockPos pos, out BoundingBox box)
    {
        var idx = state.Id;
        if ((uint)idx < (uint)_shapes.Length)
        {
            ref readonly var local = ref _shapes[idx];
            if (local.HasCollision)
            {
                box = local.ToWorldBox(pos);
                return true;
            }
        }

        box = default;
        return false;
    }

    private static LocalCollisionBox ComputeShape(BlockStateMetadata meta)
    {
        if (!meta.IsSolid)
        {
            return LocalCollisionBox.Empty;
        }

        var key = meta.BlockKey;
        if (key == BuiltInBlocks.OakDoor || key == BuiltInBlocks.IronDoor)
        {
            return ComputeDoorShape(meta);
        }

        if (key == BuiltInBlocks.OakTrapdoor || key == BuiltInBlocks.IronTrapdoor)
        {
            return ComputeTrapdoorShape(meta);
        }

        if (key == BuiltInBlocks.OakFence)
        {
            // Authentic 1.5-block tall fence post prevents standard 1.25-block jump-overs!
            return new LocalCollisionBox(0.3125f, 0f, 0.3125f, 0.6875f, 1.5f, 0.6875f, HasCollision: true);
        }

        if (key == BuiltInBlocks.DirtPath || key == BuiltInBlocks.Farmland)
        {
            return new LocalCollisionBox(0f, 0f, 0f, 1f, 0.9375f, 1f, HasCollision: true);
        }

        if (key == BuiltInBlocks.RedBed)
        {
            return new LocalCollisionBox(0f, 0f, 0f, 1f, 0.5625f, 1f, HasCollision: true);
        }

        if (key == BuiltInBlocks.Lantern)
        {
            var hanging = meta.Properties.GetValueOrDefault("hanging") == "true";
            return hanging
                ? new LocalCollisionBox(0.3125f, 0.0625f, 0.3125f, 0.6875f, 0.625f, 0.6875f, HasCollision: true)
                : new LocalCollisionBox(0.3125f, 0f, 0.3125f, 0.6875f, 0.5625f, 0.6875f, HasCollision: true);
        }

        return LocalCollisionBox.FullCube;
    }

    private static LocalCollisionBox ComputeDoorShape(BlockStateMetadata meta)
    {
        var facing = meta.Properties.GetValueOrDefault("facing", "north");
        var isOpen = meta.Properties.GetValueOrDefault("open") == "true";
        var rightHinge = meta.Properties.GetValueOrDefault("hinge") == "right";

        if (!isOpen)
        {
            return WallSlabForEdge(facing);
        }

        // Open door swings 90 degrees onto its hinge side wall so the 13/16 doorway opening is passable!
        var swungEdge = facing switch
        {
            "north" => rightHinge ? "east" : "west",
            "south" => rightHinge ? "west" : "east",
            "west" => rightHinge ? "north" : "south",
            _ => rightHinge ? "south" : "north",
        };
        return WallSlabForEdge(swungEdge);
    }

    private static LocalCollisionBox ComputeTrapdoorShape(BlockStateMetadata meta)
    {
        var facing = meta.Properties.GetValueOrDefault("facing", "north");
        var isOpen = meta.Properties.GetValueOrDefault("open") == "true";
        var isTop = meta.Properties.GetValueOrDefault("half") == "top";

        if (!isOpen)
        {
            return isTop
                ? new LocalCollisionBox(0f, 1f - SlabThickness, 0f, 1f, 1f, 1f, HasCollision: true)
                : new LocalCollisionBox(0f, 0f, 0f, 1f, SlabThickness, 1f, HasCollision: true);
        }

        // Open trapdoor folds flat against its hinge wall opposite facing, opening a 13/16 vertical hatch!
        var hingeWall = facing switch
        {
            "north" => "south",
            "south" => "north",
            "west" => "east",
            _ => "west",
        };
        return WallSlabForEdge(hingeWall);
    }

    private static LocalCollisionBox WallSlabForEdge(string edge) => edge switch
    {
        "north" => new LocalCollisionBox(0f, 0f, 0f, 1f, 1f, SlabThickness, HasCollision: true),
        "south" => new LocalCollisionBox(0f, 0f, 1f - SlabThickness, 1f, 1f, 1f, HasCollision: true),
        "west" => new LocalCollisionBox(0f, 0f, 0f, SlabThickness, 1f, 1f, HasCollision: true),
        _ => new LocalCollisionBox(1f - SlabThickness, 0f, 0f, 1f, 1f, 1f, HasCollision: true),
    };
}
