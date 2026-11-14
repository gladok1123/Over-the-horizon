namespace OverHorizon.Core;

/// <summary>
/// Clean-room dimension rules shared by world generation, portal travel, lighting, and rendering.
/// </summary>
public static class DimensionRules
{
    /// <summary>Horizontal coordinate compression factor between the Overworld and the Nether (<c>8</c>).</summary>
    public const int NetherCoordinateScale = 8;

    /// <summary>World Y of the Nether's lava sea surface (<c>31</c>, matching vanilla).</summary>
    public const int NetherLavaSeaY = 31;

    /// <summary>Exclusive upper world Y bound of solid Nether terrain (<c>128</c>: the bedrock roof sits at <c>127</c>).</summary>
    public const int NetherCeilingY = 128;

    /// <summary>Lowest world Y of solid Nether terrain (<c>0</c>: the bedrock floor sits at <c>0</c>).</summary>
    public const int NetherFloorY = 0;

    /// <summary>
    /// Returns the dimension on the other side of a Nether Portal link.
    /// </summary>
    /// <param name="dimension">Origin dimension.</param>
    /// <returns><see cref="DimensionId.Nether"/> for the Overworld and vice versa.</returns>
    public static DimensionId Opposite(DimensionId dimension) =>
        dimension == DimensionId.Nether ? DimensionId.Overworld : DimensionId.Nether;

    /// <summary>
    /// Computes the horizontal coordinate multiplier applied when travelling from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <param name="from">Origin dimension.</param>
    /// <param name="to">Destination dimension.</param>
    /// <returns><c>1.0</c> within a dimension, <c>1/8</c> into the Nether, and <c>8</c> out of it.</returns>
    public static double GetTravelScale(DimensionId from, DimensionId to)
    {
        if (from == to)
        {
            return 1.0;
        }

        return to == DimensionId.Nether ? 1.0 / NetherCoordinateScale : NetherCoordinateScale;
    }

    /// <summary>
    /// Determines whether <paramref name="dimension"/> receives natural skylight from an open sky.
    /// </summary>
    /// <param name="dimension">Target dimension.</param>
    /// <returns><see langword="true"/> for the Overworld; <see langword="false"/> for the roofed Nether.</returns>
    public static bool HasSkyLight(DimensionId dimension) => dimension != DimensionId.Nether;

    /// <summary>
    /// Resolves the minimum ambient light floor (<c>0..1</c>) used when a dimension provides no skylight.
    /// </summary>
    /// <param name="dimension">Target dimension.</param>
    /// <returns><c>0.18</c> in the murky Nether gloom and <c>0.045</c> in the Overworld.</returns>
    public static float GetAmbientLightFloor(DimensionId dimension) =>
        dimension == DimensionId.Nether ? 0.18f : 0.045f;

    /// <summary>
    /// Clamps a world Y coordinate into the buildable band of <paramref name="dimension"/>.
    /// </summary>
    /// <param name="dimension">Target dimension.</param>
    /// <param name="worldY">Candidate world Y coordinate.</param>
    /// <returns>World Y clamped into the dimension's valid band.</returns>
    public static int ClampWorldY(DimensionId dimension, int worldY) =>
        dimension == DimensionId.Nether
            ? System.Math.Clamp(worldY, NetherFloorY + 2, NetherCeilingY - 3)
            : System.Math.Clamp(worldY, EngineConstants.WorldMinY + 2, EngineConstants.WorldMaxY - 3);
}
