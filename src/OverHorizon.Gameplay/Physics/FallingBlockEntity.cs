using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Physics;

/// <summary>
/// Smooth continuous-Y gravity-falling voxel entity (<c>Sand</c>, <c>Gravel</c>) that detaches from the chunk grid once
/// and re-places once upon landing — eliminating per-block chunk remesh lag while rendering at 60+ FPS.
/// </summary>
public sealed class FallingBlockEntity
{
    /// <summary>Initializes a new instance of the <see cref="FallingBlockEntity"/> class.</summary>
    /// <param name="id">Unique runtime identifier.</param>
    /// <param name="state">Block state being dropped.</param>
    /// <param name="origin">Source grid cell.</param>
    public FallingBlockEntity(int id, BlockState state, BlockPos origin)
    {
        Id = id;
        State = state;
        BlockX = origin.X;
        BlockZ = origin.Z;
        CurrentY = origin.Y;
    }

    /// <summary>Gets unique runtime identifier.</summary>
    public int Id { get; }

    /// <summary>Gets the falling <see cref="BlockState"/>.</summary>
    public BlockState State { get; }

    /// <summary>Gets integer world X column.</summary>
    public int BlockX { get; }

    /// <summary>Gets integer world Z column.</summary>
    public int BlockZ { get; }

    /// <summary>Gets or sets smooth continuous bottom Y coordinate.</summary>
    public double CurrentY { get; set; }

    /// <summary>Gets or sets vertical velocity in blocks/second (<c>&lt;= 0</c>).</summary>
    public double VelocityY { get; set; }

    /// <summary>Gets world-space bottom-center position for 3D rendering.</summary>
    public Vec3d FeetPosition => new(BlockX + 0.5, CurrentY, BlockZ + 0.5);
}
