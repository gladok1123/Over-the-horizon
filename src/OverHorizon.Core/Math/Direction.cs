namespace OverHorizon.Core.Math;

/// <summary>
/// Six cardinal face directions of a voxel block.
/// </summary>
public enum Direction : byte
{
    /// <summary>Negative Y (0, -1, 0).</summary>
    Down = 0,

    /// <summary>Positive Y (0, +1, 0).</summary>
    Up = 1,

    /// <summary>Negative Z (0, 0, -1).</summary>
    North = 2,

    /// <summary>Positive Z (0, 0, +1).</summary>
    South = 3,

    /// <summary>Negative X (-1, 0, 0).</summary>
    West = 4,

    /// <summary>Positive X (+1, 0, 0).</summary>
    East = 5,
}
