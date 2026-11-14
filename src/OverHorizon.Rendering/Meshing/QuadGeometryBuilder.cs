using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Computes oriented CCW quad corner coordinates, tiled UVs, directional face shade,
/// and per-corner smooth ambient occlusion nibbles for <see cref="GreedySectionMesher"/>.
/// </summary>
internal static class QuadGeometryBuilder
{
    public static float GetDirectionalFaceShade(Direction face) => face switch
    {
        Direction.Up => 1.00f,
        Direction.North or Direction.South => 0.80f,
        Direction.West or Direction.East => 0.65f,
        Direction.Down => 0.50f,
        _ => 1.00f,
    };

    public static (int X, int Y, int Z) MapSliceUvToSectionCoords(Direction face, int slice, int u, int v) =>
        face switch
        {
            Direction.Up or Direction.Down => (u, slice, v),
            Direction.North or Direction.South => (u, v, slice),
            _ => (slice, v, u),
        };

    public static (Vector3 P0, Vector3 P1, Vector3 P2, Vector3 P3, Vector2 Uv0, Vector2 Uv1, Vector2 Uv2, Vector2 Uv3)
        BuildQuadCorners(Direction face, int slice, int u, int v, int w, int h, bool expandTangents = false)
    {
        var pad = expandTangents ? 0.0004f : 0.0f;
        float u0 = u - pad;
        float u1 = u + w + pad;
        float v0 = v - pad;
        float v1 = v + h + pad;
        float fw = w;
        float fh = h;

        return face switch
        {
            Direction.Up => (
                new Vector3(u0, slice + 1.0f, v0),
                new Vector3(u0, slice + 1.0f, v1),
                new Vector3(u1, slice + 1.0f, v1),
                new Vector3(u1, slice + 1.0f, v0),
                new Vector2(0.0f, 0.0f),
                new Vector2(0.0f, fh),
                new Vector2(fw, fh),
                new Vector2(fw, 0.0f)),

            Direction.Down => (
                new Vector3(u0, slice, v0),
                new Vector3(u1, slice, v0),
                new Vector3(u1, slice, v1),
                new Vector3(u0, slice, v1),
                new Vector2(0.0f, 0.0f),
                new Vector2(fw, 0.0f),
                new Vector2(fw, fh),
                new Vector2(0.0f, fh)),

            Direction.South => (
                new Vector3(u0, v0, slice + 1.0f),
                new Vector3(u1, v0, slice + 1.0f),
                new Vector3(u1, v1, slice + 1.0f),
                new Vector3(u0, v1, slice + 1.0f),
                new Vector2(0.0f, fh),
                new Vector2(fw, fh),
                new Vector2(fw, 0.0f),
                new Vector2(0.0f, 0.0f)),

            Direction.North => (
                new Vector3(u1, v0, slice),
                new Vector3(u0, v0, slice),
                new Vector3(u0, v1, slice),
                new Vector3(u1, v1, slice),
                new Vector2(0.0f, fh),
                new Vector2(fw, fh),
                new Vector2(fw, 0.0f),
                new Vector2(0.0f, 0.0f)),

            Direction.East => (
                new Vector3(slice + 1.0f, v0, u1),
                new Vector3(slice + 1.0f, v0, u0),
                new Vector3(slice + 1.0f, v1, u0),
                new Vector3(slice + 1.0f, v1, u1),
                new Vector2(0.0f, fh),
                new Vector2(fw, fh),
                new Vector2(fw, 0.0f),
                new Vector2(0.0f, 0.0f)),

            _ => (
                new Vector3(slice, v0, u0),
                new Vector3(slice, v0, u1),
                new Vector3(slice, v1, u1),
                new Vector3(slice, v1, u0),
                new Vector2(0.0f, fh),
                new Vector2(fw, fh),
                new Vector2(fw, 0.0f),
                new Vector2(0.0f, 0.0f)),
        };
    }

    public static byte SampleFaceCornerAo(
        in ChunkNeighborhood nb,
        IBlockRegistry blocks,
        Direction face,
        int ax,
        int ay,
        int az) =>
        face switch
        {
            Direction.Up => Pack4(
                SampleCorner(in nb, blocks, ax, ay, az, (-1, 0, 0), (0, 0, -1)),
                SampleCorner(in nb, blocks, ax, ay, az, (-1, 0, 0), (0, 0, 1)),
                SampleCorner(in nb, blocks, ax, ay, az, (1, 0, 0), (0, 0, 1)),
                SampleCorner(in nb, blocks, ax, ay, az, (1, 0, 0), (0, 0, -1))),

            Direction.Down => Pack4(
                SampleCorner(in nb, blocks, ax, ay, az, (-1, 0, 0), (0, 0, -1)),
                SampleCorner(in nb, blocks, ax, ay, az, (1, 0, 0), (0, 0, -1)),
                SampleCorner(in nb, blocks, ax, ay, az, (1, 0, 0), (0, 0, 1)),
                SampleCorner(in nb, blocks, ax, ay, az, (-1, 0, 0), (0, 0, 1))),

            Direction.South => Pack4(
                SampleCorner(in nb, blocks, ax, ay, az, (-1, 0, 0), (0, -1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (1, 0, 0), (0, -1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (1, 0, 0), (0, 1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (-1, 0, 0), (0, 1, 0))),

            Direction.North => Pack4(
                SampleCorner(in nb, blocks, ax, ay, az, (1, 0, 0), (0, -1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (-1, 0, 0), (0, -1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (-1, 0, 0), (0, 1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (1, 0, 0), (0, 1, 0))),

            Direction.East => Pack4(
                SampleCorner(in nb, blocks, ax, ay, az, (0, 0, 1), (0, -1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (0, 0, -1), (0, -1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (0, 0, -1), (0, 1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (0, 0, 1), (0, 1, 0))),

            _ => Pack4(
                SampleCorner(in nb, blocks, ax, ay, az, (0, 0, -1), (0, -1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (0, 0, 1), (0, -1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (0, 0, 1), (0, 1, 0)),
                SampleCorner(in nb, blocks, ax, ay, az, (0, 0, -1), (0, 1, 0))),
        };

    private static byte Pack4(byte ao0, byte ao1, byte ao2, byte ao3) =>
        (byte)(ao0 | (ao1 << 2) | (ao2 << 4) | (ao3 << 6));

    private static byte SampleCorner(
        in ChunkNeighborhood nb,
        IBlockRegistry blocks,
        int ax,
        int ay,
        int az,
        (int X, int Y, int Z) s1,
        (int X, int Y, int Z) s2)
    {
        var o1 = blocks.GetStateMetadata(nb.GetBlockState(ax + s1.X, ay + s1.Y, az + s1.Z)).IsOpaque;
        var o2 = blocks.GetStateMetadata(nb.GetBlockState(ax + s2.X, ay + s2.Y, az + s2.Z)).IsOpaque;
        var oc = (!o1 || !o2) &&
            blocks.GetStateMetadata(nb.GetBlockState(ax + s1.X + s2.X, ay + s1.Y + s2.Y, az + s1.Z + s2.Z)).IsOpaque;
        return AmbientOcclusionCalculator.ComputeVertexAo(o1, o2, oc);
    }
}
