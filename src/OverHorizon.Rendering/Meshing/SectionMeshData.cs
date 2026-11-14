using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Immutable CPU mesh output for a 16x16x16 chunk section, separated into opaque and translucent passes.
/// </summary>
/// <param name="SectionPosition">3D section coordinate.</param>
/// <param name="OpaqueVertices">Opaque pass vertices.</param>
/// <param name="OpaqueIndices">Opaque pass triangle indices.</param>
/// <param name="TranslucentVertices">Translucent pass (e.g. water) vertices.</param>
/// <param name="TranslucentIndices">Translucent pass triangle indices.</param>
public sealed record SectionMeshData(
    SectionPos SectionPosition,
    ReadOnlyMemory<ChunkVertex> OpaqueVertices,
    ReadOnlyMemory<uint> OpaqueIndices,
    ReadOnlyMemory<ChunkVertex> TranslucentVertices,
    ReadOnlyMemory<uint> TranslucentIndices)
{
    /// <summary>Gets a value indicating whether both opaque and translucent passes contain zero triangles.</summary>
    public bool IsEmpty => OpaqueIndices.IsEmpty && TranslucentIndices.IsEmpty;

    /// <summary>Gets the total vertex count across opaque and translucent passes.</summary>
    public int TotalVertexCount => OpaqueVertices.Length + TranslucentVertices.Length;

    /// <summary>Gets the total index count across opaque and translucent passes.</summary>
    public int TotalIndexCount => OpaqueIndices.Length + TranslucentIndices.Length;

    /// <summary>Gets the world-space 16x16x16 <see cref="BoundingBox"/> of this section.</summary>
    public BoundingBox WorldBounds
    {
        get
        {
            var minX = SectionPosition.X * 16.0;
            var minY = SectionPosition.Y * 16.0;
            var minZ = SectionPosition.Z * 16.0;
            return new BoundingBox(minX, minY, minZ, minX + 16.0, minY + 16.0, minZ + 16.0);
        }
    }
}
