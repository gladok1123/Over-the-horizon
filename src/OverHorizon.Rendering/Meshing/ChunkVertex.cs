using System.Numerics;
using System.Runtime.InteropServices;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Compact 32-byte GPU vertex layout for greedy-meshed voxel chunk sections.
/// Coordinates are local to the section's minimum corner <c>(0..16)</c>.
/// </summary>
/// <param name="Position">Section-local 3D coordinate (<c>0..16</c>).</param>
/// <param name="Uv">Tiled quad UV coordinate (<c>0..quadWidth, 0..quadHeight</c>).</param>
/// <param name="TileIndex">Atlas tile index (<c>0..255</c>).</param>
/// <param name="ShadeAndAo">Combined directional face shade and smooth vertex AO factor in <c>[0, 1]</c>.</param>
/// <param name="LightLevel">Normalized max(skyLight, blockLight) brightness in <c>[0, 1]</c>.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ChunkVertex(
    Vector3 Position,
    Vector2 Uv,
    float TileIndex,
    float ShadeAndAo,
    float LightLevel)
{
    /// <summary>Gets the section-local X coordinate.</summary>
    public float X => Position.X;

    /// <summary>Gets the section-local Y coordinate.</summary>
    public float Y => Position.Y;

    /// <summary>Gets the section-local Z coordinate.</summary>
    public float Z => Position.Z;

    /// <summary>Gets the tiled horizontal UV coordinate.</summary>
    public float U => Uv.X;

    /// <summary>Gets the tiled vertical UV coordinate.</summary>
    public float V => Uv.Y;
}
