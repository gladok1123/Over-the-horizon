namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Standard 4-level voxel corner ambient occlusion calculator (0 = full crease occlusion, 3 = unoccluded).
/// </summary>
public static class AmbientOcclusionCalculator
{
    private static readonly float[] BrightnessTable = [0.42f, 0.64f, 0.82f, 1.00f];

    /// <summary>
    /// Computes the discrete vertex AO level (<c>0..3</c>) given the opacity of the two adjacent edge blocks and diagonal corner block.
    /// </summary>
    /// <param name="side1Opaque">Whether tangent neighbor 1 is opaque.</param>
    /// <param name="side2Opaque">Whether tangent neighbor 2 is opaque.</param>
    /// <param name="cornerOpaque">Whether diagonal tangent corner neighbor is opaque.</param>
    /// <returns>Discrete AO level in <c>[0, 3]</c>.</returns>
    public static byte ComputeVertexAo(bool side1Opaque, bool side2Opaque, bool cornerOpaque)
    {
        if (side1Opaque && side2Opaque)
        {
            return 0;
        }

        var occluders = (side1Opaque ? 1 : 0) + (side2Opaque ? 1 : 0) + (cornerOpaque ? 1 : 0);
        return (byte)(3 - occluders);
    }

    /// <summary>
    /// Converts a discrete AO level (<c>0..3</c>) into a linear brightness multiplier in <c>[0.42, 1.0]</c>.
    /// </summary>
    /// <param name="aoLevel">Discrete AO level.</param>
    /// <returns>Brightness multiplier.</returns>
    public static float ToBrightnessFactor(byte aoLevel) =>
        BrightnessTable[aoLevel & 3];
}
