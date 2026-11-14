namespace OverHorizon.Core.Math;

/// <summary>
/// Orthogonal spatial axis in 3D voxel space.
/// </summary>
public enum Axis : byte
{
    /// <summary>East-West axis.</summary>
    X = 0,

    /// <summary>Vertical Up-Down axis.</summary>
    Y = 1,

    /// <summary>South-North axis.</summary>
    Z = 2,
}
