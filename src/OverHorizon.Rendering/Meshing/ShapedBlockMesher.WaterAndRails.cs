using System.Globalization;
using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Smooth corner-averaged Water surface mesher (with full internal-face culling) and straight/curved/45-degree ascending Rail mesher.
/// </summary>
internal static partial class ShapedBlockMesher
{
    private static void EmitSmoothWaterBlock(
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
        var aboveFluid = IsWaterCell(blocks, nb.GetBlockState(lx, wy + 1, lz));
        var hNW = aboveFluid ? 1.0f : ComputeCornerWaterHeight(in nb, blocks, lx, wy, lz, -1, -1);
        var hNE = aboveFluid ? 1.0f : ComputeCornerWaterHeight(in nb, blocks, lx, wy, lz, 1, -1);
        var hSE = aboveFluid ? 1.0f : ComputeCornerWaterHeight(in nb, blocks, lx, wy, lz, 1, 1);
        var hSW = aboveFluid ? 1.0f : ComputeCornerWaterHeight(in nb, blocks, lx, wy, lz, -1, 1);

        var pBNW = new Vector3(o.X, o.Y, o.Z);
        var pBNE = new Vector3(o.X + 1.0f, o.Y, o.Z);
        var pBSE = new Vector3(o.X + 1.0f, o.Y, o.Z + 1.0f);
        var pBSW = new Vector3(o.X, o.Y, o.Z + 1.0f);

        var pTNW = new Vector3(o.X, o.Y + hNW, o.Z);
        var pTNE = new Vector3(o.X + 1.0f, o.Y + hNE, o.Z);
        var pTSE = new Vector3(o.X + 1.0f, o.Y + hSE, o.Z + 1.0f);
        var pTSW = new Vector3(o.X, o.Y + hSW, o.Z + 1.0f);

        // Top surface (only when cell above is NOT water — rendered double-sided so underwater upward view sees the water ceiling!)
        if (!aboveFluid)
        {
            EmitQuadWithUv(
                verts,
                indices,
                pTNW,
                pTSW,
                pTSE,
                pTNE,
                new Vector2(0.0f, 0.0f),
                new Vector2(0.0f, 1.0f),
                new Vector2(1.0f, 1.0f),
                new Vector2(1.0f, 0.0f),
                tile,
                0.96f,
                packedLight,
                doubleSided: true);
        }

        // Bottom face (only when cell below is neither water nor opaque)
        var belowMeta = blocks.GetStateMetadata(nb.GetBlockState(lx, wy - 1, lz));
        if (!belowMeta.IsFluid && !belowMeta.IsOpaque)
        {
            EmitQuad(verts, indices, pBNW, pBNE, pBSE, pBSW, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f), tile, 0.62f, packedLight);
        }

        // Side faces: NEVER emit internal walls between two adjacent water blocks!
        if (ShouldEmitWaterSide(blocks, nb.GetBlockState(lx, wy, lz - 1)))
        {
            EmitQuad(verts, indices, pBNE, pBNW, pTNW, pTNE, new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f - hNW), new Vector2(0.0f, 1.0f - hNE), tile, 0.84f, packedLight);
        }

        if (ShouldEmitWaterSide(blocks, nb.GetBlockState(lx, wy, lz + 1)))
        {
            EmitQuad(verts, indices, pBSW, pBSE, pTSE, pTSW, new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f - hSE), new Vector2(0.0f, 1.0f - hSW), tile, 0.84f, packedLight);
        }

        if (ShouldEmitWaterSide(blocks, nb.GetBlockState(lx - 1, wy, lz)))
        {
            EmitQuad(verts, indices, pBNW, pBSW, pTSW, pTNW, new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f - hSW), new Vector2(0.0f, 1.0f - hNW), tile, 0.74f, packedLight);
        }

        if (ShouldEmitWaterSide(blocks, nb.GetBlockState(lx + 1, wy, lz)))
        {
            EmitQuad(verts, indices, pBSE, pBNE, pTNE, pTSE, new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f - hNE), new Vector2(0.0f, 1.0f - hSE), tile, 0.74f, packedLight);
        }
    }

    private static float ComputeCornerWaterHeight(
        in ChunkNeighborhood nb,
        IBlockRegistry blocks,
        int lx,
        int wy,
        int lz,
        int dx,
        int dz)
    {
        var sum = 0.0f;
        var weight = 0.0f;

        for (var ox = 0; ox <= 1; ox++)
        {
            for (var oz = 0; oz <= 1; oz++)
            {
                var sx = lx + (ox * dx);
                var sz = lz + (oz * dz);
                if (IsWaterCell(blocks, nb.GetBlockState(sx, wy + 1, sz)))
                {
                    return 1.0f;
                }

                var st = nb.GetBlockState(sx, wy, sz);
                if (st.IsAir)
                {
                    // Air neighbor gently pulls down the flowing edge!
                    sum += 0.05f;
                    weight += 0.35f;
                    continue;
                }

                var m = blocks.GetStateMetadata(st);
                if (!m.IsFluid)
                {
                    continue;
                }

                var lvl = m.Properties.TryGetValue("level", out var lv) && int.TryParse(lv, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0;
                if (lvl == 0 || lvl >= 8)
                {
                    sum += 0.875f * 4.0f;
                    weight += 4.0f;
                }
                else
                {
                    var h = Math.Clamp((8 - lvl) / 9.0f, 0.14f, 0.875f);
                    sum += h;
                    weight += 1.0f;
                }
            }
        }

        return weight > 0.0f ? Math.Clamp(sum / weight, 0.12f, 0.89f) : 0.875f;
    }

    private static bool IsWaterCell(IBlockRegistry blocks, BlockState s) =>
        !s.IsAir && blocks.GetStateMetadata(s).IsFluid;

    private static bool ShouldEmitWaterSide(IBlockRegistry blocks, BlockState neighborState)
    {
        if (neighborState.IsAir) return true;
        var m = blocks.GetStateMetadata(neighborState);
        return !m.IsFluid && !m.IsOpaque;
    }

    private static void EmitRailTrack(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 o,
        BlockStateMetadata meta,
        byte skyLight,
        int effBlk)
    {
        var shape = meta.Properties.TryGetValue("shape", out var s) ? s : "north_south";
        var isPoweredRail = meta.BlockKey == BuiltInBlocks.PoweredRail;
        var energized = isPoweredRail && meta.Properties.TryGetValue("powered", out var pv) && string.Equals(pv, "true", StringComparison.OrdinalIgnoreCase);

        if (energized)
        {
            effBlk = Math.Max(effBlk, 9);
        }

        float packedLight = (byte)(((skyLight & 0x0F) << 4) | (effBlk & 0x0F));
        ushort tile = ProceduralAtlasGenerator.TileRail;
        if (isPoweredRail)
        {
            tile = energized ? ProceduralAtlasGenerator.TilePoweredRailOn : ProceduralAtlasGenerator.TilePoweredRailOff;
        }

        var yLow = o.Y + 0.0625f;
        var yHigh = o.Y + 1.0625f;

        // Corner curved rails (rail_corner.png connects South + East in unrotated UV orientation!)
        if (!isPoweredRail && shape is "south_east" or "south_west" or "north_west" or "north_east")
        {
            var rotSteps = shape switch
            {
                "south_west" => 1,
                "north_west" => 2,
                "north_east" => 3,
                _ => 0,
            };
            var (uv0, uv1, uv2, uv3) = GetRotatedUv90(rotSteps);
            EmitQuadWithUv(
                verts,
                indices,
                new Vector3(o.X, yLow, o.Z),
                new Vector3(o.X, yLow, o.Z + 1.0f),
                new Vector3(o.X + 1.0f, yLow, o.Z + 1.0f),
                new Vector3(o.X + 1.0f, yLow, o.Z),
                uv0,
                uv1,
                uv2,
                uv3,
                ProceduralAtlasGenerator.TileRailCorner,
                0.94f,
                packedLight,
                doubleSided: true);
            return;
        }

        var isEastWest = shape is "east_west" or "ascending_east" or "ascending_west";
        var (u0, u1, u2, u3) = GetRotatedUv90(isEastWest ? 1 : 0);

        var (yNW, ySW, ySE, yNE) = shape switch
        {
            "ascending_north" => (yHigh, yLow, yLow, yHigh),
            "ascending_south" => (yLow, yHigh, yHigh, yLow),
            "ascending_east" => (yLow, yLow, yHigh, yHigh),
            "ascending_west" => (yHigh, yHigh, yLow, yLow),
            _ => (yLow, yLow, yLow, yLow),
        };

        EmitQuadWithUv(
            verts,
            indices,
            new Vector3(o.X, yNW, o.Z),
            new Vector3(o.X, ySW, o.Z + 1.0f),
            new Vector3(o.X + 1.0f, ySE, o.Z + 1.0f),
            new Vector3(o.X + 1.0f, yNE, o.Z),
            u0,
            u1,
            u2,
            u3,
            tile,
            energized ? 1.0f : 0.92f,
            packedLight,
            doubleSided: true);
    }

    private static (Vector2 Uv0, Vector2 Uv1, Vector2 Uv2, Vector2 Uv3) GetRotatedUv90(int quarterTurns)
    {
        var a = new Vector2(0.01f, 0.01f);
        var b = new Vector2(0.01f, 0.99f);
        var c = new Vector2(0.99f, 0.99f);
        var d = new Vector2(0.99f, 0.01f);
        return (quarterTurns & 3) switch
        {
            1 => (d, a, b, c),
            2 => (c, d, a, b),
            3 => (b, c, d, a),
            _ => (a, b, c, d),
        };
    }

    private static void EmitQuadWithUv(
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
        float packedLight,
        bool doubleSided)
    {
        EmitQuad(verts, indices, p0, p1, p2, p3, uv0, uv1, uv2, uv3, tile, shade, packedLight);
        if (doubleSided)
        {
            EmitQuad(verts, indices, p1, p0, p3, p2, uv1, uv0, uv3, uv2, tile, shade * 0.90f, packedLight);
        }
    }
}
