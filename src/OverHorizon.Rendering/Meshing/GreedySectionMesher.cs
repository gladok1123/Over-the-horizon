using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Greedy 2D quad-merging voxel section mesher with separate <c>0..15</c> sky &amp; block light nibble packing,
/// cross-chunk face culling, directional shading, smooth AO, and sub-voxel non-cube block geometry.
/// </summary>
public static class GreedySectionMesher
{
    private const ulong ValidCellBit = 1UL << 63;
    private const ulong TranslucentBit = 1UL << 32;

    /// <summary>Meshes a single <c>16x16x16</c> chunk section into opaque and translucent vertex/index buffers.</summary>
    public static SectionMeshData MeshSection(
        in ChunkNeighborhood neighborhood,
        int sectionIndex,
        IBlockRegistry blocks,
        TextureAtlasData atlas)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(atlas);

        var center = neighborhood.Center;
        var section = center.Sections[sectionIndex];
        var secPos = new SectionPos(center.Position.X, section.SectionY, center.Position.Z);

        if (section.IsOnlyAir)
        {
            return new SectionMeshData(
                secPos,
                ReadOnlyMemory<ChunkVertex>.Empty,
                ReadOnlyMemory<uint>.Empty,
                ReadOnlyMemory<ChunkVertex>.Empty,
                ReadOnlyMemory<uint>.Empty);
        }

        var opaqueVerts = new List<ChunkVertex>(512);
        var opaqueIndices = new List<uint>(768);
        var transVerts = new List<ChunkVertex>(64);
        var transIndices = new List<uint>(96);

        var baseWorldY = EngineConstants.WorldMinY + (sectionIndex * 16);
        Span<ulong> mask = stackalloc ulong[256];

        foreach (var face in DirectionExtensions.All)
        {
            var faceShade = QuadGeometryBuilder.GetDirectionalFaceShade(face);

            for (var slice = 0; slice < 16; slice++)
            {
                mask.Clear();
                if (!PopulateSliceMask(in neighborhood, baseWorldY, face, slice, blocks, atlas, mask))
                {
                    continue;
                }

                GreedyMergeAndEmitSlice(
                    face,
                    slice,
                    faceShade,
                    mask,
                    (opaqueVerts, opaqueIndices),
                    (transVerts, transIndices));
            }
        }

        ShapedBlockMesher.AppendSectionShapes(in neighborhood, sectionIndex, blocks, atlas, opaqueVerts, opaqueIndices);

        return new SectionMeshData(
            secPos,
            opaqueVerts.Count > 0 ? opaqueVerts.ToArray() : ReadOnlyMemory<ChunkVertex>.Empty,
            opaqueIndices.Count > 0 ? opaqueIndices.ToArray() : ReadOnlyMemory<uint>.Empty,
            transVerts.Count > 0 ? transVerts.ToArray() : ReadOnlyMemory<ChunkVertex>.Empty,
            transIndices.Count > 0 ? transIndices.ToArray() : ReadOnlyMemory<uint>.Empty);
    }

    private static bool PopulateSliceMask(
        in ChunkNeighborhood neighborhood,
        int baseWorldY,
        Direction face,
        int slice,
        IBlockRegistry blocks,
        TextureAtlasData atlas,
        Span<ulong> mask)
    {
        var any = false;
        var normal = face.Normal();

        for (var v = 0; v < 16; v++)
        {
            for (var u = 0; u < 16; u++)
            {
                var (lx, ly, lz) = QuadGeometryBuilder.MapSliceUvToSectionCoords(face, slice, u, v);
                var wy = baseWorldY + ly;

                var state = neighborhood.Center.GetBlockState(lx, wy, lz);
                if (state.IsAir)
                {
                    continue;
                }

                var meta = blocks.GetStateMetadata(state);
                if ((!meta.IsSolid && !meta.IsFluid) || ShapedBlockMesher.IsCustomShape(meta.BlockKey))
                {
                    continue;
                }

                var adjX = lx + normal.X;
                var adjY = wy + normal.Y;
                var adjZ = lz + normal.Z;

                var neighborState = neighborhood.GetBlockState(adjX, adjY, adjZ);
                if (!ShouldRenderFace(meta, blocks.GetStateMetadata(neighborState)))
                {
                    continue;
                }

                var tile = atlas.GetTileIndex(meta.BlockKey, face);
                var (sky, blk) = neighborhood.GetLight(adjX, adjY, adjZ);
                var effBlk = Math.Max(meta.LightEmission, blk);
                var packedSkyAndBlk = (byte)(((sky & 0x0F) << 4) | (effBlk & 0x0F));
                var packedAo = QuadGeometryBuilder.SampleFaceCornerAo(in neighborhood, blocks, face, adjX, adjY, adjZ);
                var isTranslucent = meta.IsFluid;

                mask[(v << 4) | u] = ValidCellBit |
                    (isTranslucent ? TranslucentBit : 0UL) |
                    ((ulong)packedAo << 24) |
                    ((ulong)packedSkyAndBlk << 16) |
                    tile;

                any = true;
            }
        }

        return any;
    }

    private static bool ShouldRenderFace(BlockStateMetadata current, BlockStateMetadata neighbor)
    {
        if (neighbor.State.IsAir)
        {
            return true;
        }

        if (neighbor.IsOpaque)
        {
            return false;
        }

        return !(current.IsFluid && neighbor.IsFluid);
    }

    private static void GreedyMergeAndEmitSlice(
        Direction face,
        int slice,
        float faceShade,
        Span<ulong> mask,
        (List<ChunkVertex> Verts, List<uint> Indices) opaquePass,
        (List<ChunkVertex> Verts, List<uint> Indices) transPass)
    {
        for (var v = 0; v < 16; v++)
        {
            var u = 0;
            while (u < 16)
            {
                var token = mask[(v << 4) | u];
                if (token == 0UL)
                {
                    u++;
                    continue;
                }

                var width = MeasureRunWidth(mask, u, v, token);
                var height = MeasureRunHeight(mask, u, v, width, token);
                ClearRect(mask, u, v, width, height);

                var targetPass = (token & TranslucentBit) != 0UL ? transPass : opaquePass;
                EmitMergedQuad(
                    (face, slice, u, v, width, height),
                    faceShade,
                    token,
                    targetPass.Verts,
                    targetPass.Indices);

                u += width;
            }
        }
    }

    private static int MeasureRunWidth(ReadOnlySpan<ulong> mask, int u, int v, ulong token)
    {
        var width = 1;
        var rowOffset = v << 4;
        while (u + width < 16 && mask[rowOffset + u + width] == token)
        {
            width++;
        }

        return width;
    }

    private static int MeasureRunHeight(ReadOnlySpan<ulong> mask, int u, int v, int width, ulong token)
    {
        var height = 1;
        while (v + height < 16)
        {
            var rowOffset = (v + height) << 4;
            for (var k = 0; k < width; k++)
            {
                if (mask[rowOffset + u + k] != token)
                {
                    return height;
                }
            }

            height++;
        }

        return height;
    }

    private static void ClearRect(Span<ulong> mask, int u, int v, int width, int height)
    {
        for (var dv = 0; dv < height; dv++)
        {
            mask.Slice(((v + dv) << 4) + u, width).Clear();
        }
    }

    private static void EmitMergedQuad(
        (Direction Face, int Slice, int U, int V, int Width, int Height) rect,
        float faceShade,
        ulong token,
        List<ChunkVertex> verts,
        List<uint> indices)
    {
        var tile = (ushort)(token & 0xFFFFUL);
        var packedSkyBlk = (byte)((token >> 16) & 0xFFUL);
        var packedAo = (byte)((token >> 24) & 0xFFUL);

        var ao0 = (byte)(packedAo & 3);
        var ao1 = (byte)((packedAo >> 2) & 3);
        var ao2 = (byte)((packedAo >> 4) & 3);
        var ao3 = (byte)((packedAo >> 6) & 3);

        float packedLightFloat = packedSkyBlk;
        var baseIdx = (uint)verts.Count;

        var (p0, p1, p2, p3, uv0, uv1, uv2, uv3) = QuadGeometryBuilder.BuildQuadCorners(
            rect.Face,
            rect.Slice,
            rect.U,
            rect.V,
            rect.Width,
            rect.Height);

        verts.Add(new ChunkVertex(p0, uv0, tile, faceShade * AmbientOcclusionCalculator.ToBrightnessFactor(ao0), packedLightFloat));
        verts.Add(new ChunkVertex(p1, uv1, tile, faceShade * AmbientOcclusionCalculator.ToBrightnessFactor(ao1), packedLightFloat));
        verts.Add(new ChunkVertex(p2, uv2, tile, faceShade * AmbientOcclusionCalculator.ToBrightnessFactor(ao2), packedLightFloat));
        verts.Add(new ChunkVertex(p3, uv3, tile, faceShade * AmbientOcclusionCalculator.ToBrightnessFactor(ao3), packedLightFloat));

        if (ao0 + ao2 < ao1 + ao3)
        {
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 3);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 3);
            indices.Add(baseIdx + 0);
        }
        else
        {
            indices.Add(baseIdx + 0);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 0);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 3);
        }
    }
}
