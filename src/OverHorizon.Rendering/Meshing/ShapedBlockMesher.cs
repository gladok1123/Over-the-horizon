using System.Numerics;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Emits authentic sub-voxel 3D geometry for non-cube blocks (<c>Torch</c>, <c>Lantern</c>, <c>Cobweb</c>,
/// <c>Rail</c>, <c>Ladder</c>, and connected <c>OakFence</c> posts) inside <see cref="GreedySectionMesher"/>.
/// </summary>
internal static class ShapedBlockMesher
{
    public static bool IsCustomShape(ResourceKey key) =>
        key == BuiltInBlocks.Torch ||
        key == BuiltInBlocks.Lantern ||
        key == BuiltInBlocks.Cobweb ||
        key == BuiltInBlocks.Rail ||
        key == BuiltInBlocks.Ladder ||
        key == BuiltInBlocks.OakFence;

    public static void AppendSectionShapes(
        in ChunkNeighborhood neighborhood,
        int sectionIndex,
        IBlockRegistry blocks,
        TextureAtlasData atlas,
        List<ChunkVertex> verts,
        List<uint> indices)
    {
        var section = neighborhood.Center.Sections[sectionIndex];
        var baseWorldY = EngineConstants.WorldMinY + (sectionIndex * 16);

        for (var ly = 0; ly < 16; ly++)
        {
            var wy = baseWorldY + ly;
            for (var lz = 0; lz < 16; lz++)
            {
                for (var lx = 0; lx < 16; lx++)
                {
                    var state = section.GetBlockState(lx, ly, lz);
                    if (state.IsAir)
                    {
                        continue;
                    }

                    var meta = blocks.GetStateMetadata(state);
                    if (!IsCustomShape(meta.BlockKey))
                    {
                        continue;
                    }

                    var (sky, blk) = neighborhood.GetLight(lx, wy, lz);
                    var effBlk = Math.Max(meta.LightEmission, blk);
                    var packedLight = (float)(((sky & 0x0F) << 4) | (effBlk & 0x0F));
                    var tile = atlas.GetTileIndex(meta.BlockKey, Direction.North);
                    var origin = new Vector3(lx, ly, lz);

                    if (meta.BlockKey == BuiltInBlocks.Torch)
                    {
                        EmitSubBox(verts, indices, origin, new Vector3(0.4375f, 0.0f, 0.4375f), new Vector3(0.5625f, 0.625f, 0.5625f), tile, packedLight, uvMin: new Vector2(0.4375f, 0.375f), uvMax: new Vector2(0.5625f, 1.0f));
                    }
                    else if (meta.BlockKey == BuiltInBlocks.Lantern)
                    {
                        EmitSubBox(verts, indices, origin, new Vector3(0.3125f, 0.0f, 0.3125f), new Vector3(0.6875f, 0.4375f, 0.6875f), tile, packedLight, new Vector2(0.3125f, 0.375f), new Vector2(0.6875f, 0.8125f));
                        EmitSubBox(verts, indices, origin, new Vector3(0.375f, 0.4375f, 0.375f), new Vector3(0.625f, 0.5625f, 0.625f), tile, packedLight, new Vector2(0.375f, 0.25f), new Vector2(0.625f, 0.375f));
                    }
                    else if (meta.BlockKey == BuiltInBlocks.Cobweb)
                    {
                        EmitCrossPlanes(verts, indices, origin, tile, packedLight);
                    }
                    else if (meta.BlockKey == BuiltInBlocks.Rail)
                    {
                        var y = origin.Y + 0.0625f;
                        EmitDoubleSidedQuad(
                            verts, indices,
                            new Vector3(origin.X, y, origin.Z),
                            new Vector3(origin.X, y, origin.Z + 1.0f),
                            new Vector3(origin.X + 1.0f, y, origin.Z + 1.0f),
                            new Vector3(origin.X + 1.0f, y, origin.Z),
                            tile, shade: 0.95f, packedLight);
                    }
                    else if (meta.BlockKey == BuiltInBlocks.Ladder)
                    {
                        EmitWallLadder(in neighborhood, blocks, verts, indices, lx, wy, lz, origin, tile, packedLight);
                    }
                    else if (meta.BlockKey == BuiltInBlocks.OakFence)
                    {
                        EmitConnectedFence(in neighborhood, blocks, verts, indices, lx, wy, lz, origin, tile, packedLight);
                    }
                }
            }
        }
    }

    private static void EmitWallLadder(
        in ChunkNeighborhood nb,
        IBlockRegistry blocks,
        List<ChunkVertex> verts,
        List<uint> indices,
        int lx,
        int wy,
        int lz,
        Vector3 o,
        ushort tile,
        float packedLight)
    {
        if (blocks.GetStateMetadata(nb.GetBlockState(lx, wy, lz + 1)).IsOpaque)
        {
            var z = o.Z + 0.92f;
            EmitDoubleSidedQuad(verts, indices, new Vector3(o.X, o.Y, z), new Vector3(o.X + 1, o.Y, z), new Vector3(o.X + 1, o.Y + 1, z), new Vector3(o.X, o.Y + 1, z), tile, 0.85f, packedLight);
        }
        else if (blocks.GetStateMetadata(nb.GetBlockState(lx + 1, wy, lz)).IsOpaque)
        {
            var x = o.X + 0.92f;
            EmitDoubleSidedQuad(verts, indices, new Vector3(x, o.Y, o.Z + 1), new Vector3(x, o.Y, o.Z), new Vector3(x, o.Y + 1, o.Z), new Vector3(x, o.Y + 1, o.Z + 1), tile, 0.85f, packedLight);
        }
        else if (blocks.GetStateMetadata(nb.GetBlockState(lx - 1, wy, lz)).IsOpaque)
        {
            var x = o.X + 0.08f;
            EmitDoubleSidedQuad(verts, indices, new Vector3(x, o.Y, o.Z), new Vector3(x, o.Y, o.Z + 1), new Vector3(x, o.Y + 1, o.Z + 1), new Vector3(x, o.Y + 1, o.Z), tile, 0.85f, packedLight);
        }
        else
        {
            var z = o.Z + 0.08f;
            EmitDoubleSidedQuad(verts, indices, new Vector3(o.X + 1, o.Y, z), new Vector3(o.X, o.Y, z), new Vector3(o.X, o.Y + 1, z), new Vector3(o.X + 1, o.Y + 1, z), tile, 0.85f, packedLight);
        }
    }

    private static void EmitConnectedFence(
        in ChunkNeighborhood nb,
        IBlockRegistry blocks,
        List<ChunkVertex> verts,
        List<uint> indices,
        int lx,
        int wy,
        int lz,
        Vector3 o,
        ushort tile,
        float packedLight)
    {
        EmitSubBox(verts, indices, o, new Vector3(0.375f, 0.0f, 0.375f), new Vector3(0.625f, 1.0f, 0.625f), tile, packedLight, new Vector2(0.375f, 0.0f), new Vector2(0.625f, 1.0f));

        if (ConnectsFence(blocks.GetStateMetadata(nb.GetBlockState(lx - 1, wy, lz))))
        {
            EmitSubBox(verts, indices, o, new Vector3(0.0f, 0.75f, 0.4375f), new Vector3(0.375f, 0.9375f, 0.5625f), tile, packedLight, Vector2.Zero, new Vector2(0.375f, 0.25f));
            EmitSubBox(verts, indices, o, new Vector3(0.0f, 0.375f, 0.4375f), new Vector3(0.375f, 0.5625f, 0.5625f), tile, packedLight, Vector2.Zero, new Vector2(0.375f, 0.25f));
        }

        if (ConnectsFence(blocks.GetStateMetadata(nb.GetBlockState(lx + 1, wy, lz))))
        {
            EmitSubBox(verts, indices, o, new Vector3(0.625f, 0.75f, 0.4375f), new Vector3(1.0f, 0.9375f, 0.5625f), tile, packedLight, Vector2.Zero, new Vector2(0.375f, 0.25f));
            EmitSubBox(verts, indices, o, new Vector3(0.625f, 0.375f, 0.4375f), new Vector3(1.0f, 0.5625f, 0.5625f), tile, packedLight, Vector2.Zero, new Vector2(0.375f, 0.25f));
        }

        if (ConnectsFence(blocks.GetStateMetadata(nb.GetBlockState(lx, wy, lz - 1))))
        {
            EmitSubBox(verts, indices, o, new Vector3(0.4375f, 0.75f, 0.0f), new Vector3(0.5625f, 0.9375f, 0.375f), tile, packedLight, Vector2.Zero, new Vector2(0.375f, 0.25f));
            EmitSubBox(verts, indices, o, new Vector3(0.4375f, 0.375f, 0.0f), new Vector3(0.5625f, 0.5625f, 0.375f), tile, packedLight, Vector2.Zero, new Vector2(0.375f, 0.25f));
        }

        if (ConnectsFence(blocks.GetStateMetadata(nb.GetBlockState(lx, wy, lz + 1))))
        {
            EmitSubBox(verts, indices, o, new Vector3(0.4375f, 0.75f, 0.625f), new Vector3(0.5625f, 0.9375f, 1.0f), tile, packedLight, Vector2.Zero, new Vector2(0.375f, 0.25f));
            EmitSubBox(verts, indices, o, new Vector3(0.4375f, 0.375f, 0.625f), new Vector3(0.5625f, 0.5625f, 1.0f), tile, packedLight, Vector2.Zero, new Vector2(0.375f, 0.25f));
        }
    }

    private static bool ConnectsFence(BlockStateMetadata m) =>
        m.IsOpaque || m.BlockKey == BuiltInBlocks.OakFence;

    private static void EmitCrossPlanes(List<ChunkVertex> verts, List<uint> indices, Vector3 o, ushort tile, float packedLight)
    {
        EmitDoubleSidedQuad(
            verts, indices,
            new Vector3(o.X, o.Y, o.Z),
            new Vector3(o.X + 1, o.Y, o.Z + 1),
            new Vector3(o.X + 1, o.Y + 1, o.Z + 1),
            new Vector3(o.X, o.Y + 1, o.Z),
            tile, 0.88f, packedLight);
        EmitDoubleSidedQuad(
            verts, indices,
            new Vector3(o.X + 1, o.Y, o.Z),
            new Vector3(o.X, o.Y, o.Z + 1),
            new Vector3(o.X, o.Y + 1, o.Z + 1),
            new Vector3(o.X + 1, o.Y + 1, o.Z),
            tile, 0.88f, packedLight);
    }

    private static void EmitDoubleSidedQuad(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        ushort tile,
        float shade,
        float packedLight)
    {
        EmitQuad(verts, indices, p0, p1, p2, p3, new Vector2(0.01f, 0.99f), new Vector2(0.99f, 0.99f), new Vector2(0.99f, 0.01f), new Vector2(0.01f, 0.01f), tile, shade, packedLight);
        EmitQuad(verts, indices, p1, p0, p3, p2, new Vector2(0.01f, 0.99f), new Vector2(0.99f, 0.99f), new Vector2(0.99f, 0.01f), new Vector2(0.01f, 0.01f), tile, shade, packedLight);
    }

    private static void EmitSubBox(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 o,
        Vector3 min,
        Vector3 max,
        ushort tile,
        float packedLight,
        Vector2 uvMin,
        Vector2 uvMax)
    {
        var a = o + min;
        var b = o + max;
        var uvBL = new Vector2(uvMin.X, uvMax.Y);
        var uvBR = new Vector2(uvMax.X, uvMax.Y);
        var uvTR = new Vector2(uvMax.X, uvMin.Y);
        var uvTL = new Vector2(uvMin.X, uvMin.Y);

        EmitQuad(verts, indices, new Vector3(a.X, a.Y, b.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(a.X, b.Y, b.Z), uvBL, uvBR, uvTR, uvTL, tile, 0.84f, packedLight);
        EmitQuad(verts, indices, new Vector3(b.X, a.Y, a.Z), new Vector3(a.X, a.Y, a.Z), new Vector3(a.X, b.Y, a.Z), new Vector3(b.X, b.Y, a.Z), uvBL, uvBR, uvTR, uvTL, tile, 0.84f, packedLight);
        EmitQuad(verts, indices, new Vector3(b.X, a.Y, b.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, b.Y, a.Z), new Vector3(b.X, b.Y, b.Z), uvBL, uvBR, uvTR, uvTL, tile, 0.72f, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, a.Y, a.Z), new Vector3(a.X, a.Y, b.Z), new Vector3(a.X, b.Y, b.Z), new Vector3(a.X, b.Y, a.Z), uvBL, uvBR, uvTR, uvTL, tile, 0.72f, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, b.Y, a.Z), new Vector3(a.X, b.Y, b.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(b.X, b.Y, a.Z), new Vector2(uvMin.X, uvMin.Y), new Vector2(uvMin.X, uvMin.Y + 0.125f), new Vector2(uvMax.X, uvMin.Y + 0.125f), new Vector2(uvMax.X, uvMin.Y), tile, 1.0f, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(a.X, a.Y, b.Z), uvBL, uvBR, uvTR, uvTL, tile, 0.55f, packedLight);
    }

    private static void EmitQuad(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        Vector2 uv0,
        Vector2 uv1,
        Vector2 uv2,
        Vector2 uv3,
        ushort tile,
        float shade,
        float packedLight)
    {
        var baseIdx = (uint)verts.Count;
        verts.Add(new ChunkVertex(p0, uv0, tile, shade, packedLight));
        verts.Add(new ChunkVertex(p1, uv1, tile, shade, packedLight));
        verts.Add(new ChunkVertex(p2, uv2, tile, shade, packedLight));
        verts.Add(new ChunkVertex(p3, uv3, tile, shade, packedLight));
        indices.Add(baseIdx + 0);
        indices.Add(baseIdx + 1);
        indices.Add(baseIdx + 2);
        indices.Add(baseIdx + 0);
        indices.Add(baseIdx + 2);
        indices.Add(baseIdx + 3);
    }
}
