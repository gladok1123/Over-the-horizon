using System.Numerics;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Emits authentic sub-voxel 3D geometry for Torches, Lanterns (floor &amp; ceiling-hung), Ladders, Rails &amp; Powered Rails,
/// Cobwebs, Oak Fences, Redstone components, 6-way Pistons, 2-block Beds, Fire, and smooth corner-interpolated Water.
/// </summary>
internal static partial class ShapedBlockMesher
{
    public static bool IsCustomShape(BlockStateMetadata meta)
    {
        if (meta.IsFluid)
        {
            return meta.Properties.TryGetValue("level", out var lv) &&
                   !string.Equals(lv, "0", StringComparison.Ordinal);
        }

        var key = meta.BlockKey;
        if (key == BuiltInBlocks.Piston || key == BuiltInBlocks.StickyPiston)
        {
            return meta.Properties.TryGetValue("extended", out var ext) &&
                   string.Equals(ext, "true", StringComparison.OrdinalIgnoreCase);
        }

        return key == BuiltInBlocks.NetherPortal ||
               key == BuiltInBlocks.Torch ||
               key == BuiltInBlocks.Lantern ||
               key == BuiltInBlocks.Ladder ||
               key == BuiltInBlocks.Rail ||
               key == BuiltInBlocks.PoweredRail ||
               key == BuiltInBlocks.Cobweb ||
               key == BuiltInBlocks.Wheat ||
               key == BuiltInBlocks.OakFence ||
               key == BuiltInBlocks.RedstoneWire ||
               key == BuiltInBlocks.RedstoneTorch ||
               key == BuiltInBlocks.RedstoneTorchOff ||
               key == BuiltInBlocks.Lever ||
               key == BuiltInBlocks.StoneButton ||
               key == BuiltInBlocks.StonePressurePlate ||
               key == BuiltInBlocks.RedstoneRepeater ||
               key == BuiltInBlocks.RedstoneComparator ||
               key == BuiltInBlocks.Composter ||
               key == BuiltInBlocks.OakTrapdoor ||
               key == BuiltInBlocks.IronTrapdoor ||
               key == BuiltInBlocks.OakDoor ||
               key == BuiltInBlocks.IronDoor ||
               key == BuiltInBlocks.AmethystCluster ||
               key == BuiltInBlocks.DirtPath ||
               key == BuiltInBlocks.Farmland ||
               key == BuiltInBlocks.MonsterSpawner ||
               key == BuiltInBlocks.PistonHead ||
               key == BuiltInBlocks.Fire ||
               key == BuiltInBlocks.RedBed;
    }

    public static void AppendSectionShapes(
        in ChunkNeighborhood neighborhood,
        int sectionIndex,
        IBlockRegistry blocks,
        TextureAtlasData atlas,
        List<ChunkVertex> opaqueVerts,
        List<uint> opaqueIndices,
        List<ChunkVertex> transVerts,
        List<uint> transIndices)
    {
        var baseWorldY = EngineConstants.WorldMinY + (sectionIndex * 16);

        for (var ly = 0; ly < 16; ly++)
        {
            var wy = baseWorldY + ly;
            for (var lz = 0; lz < 16; lz++)
            {
                for (var lx = 0; lx < 16; lx++)
                {
                    var state = neighborhood.Center.GetBlockState(lx, wy, lz);
                    if (state.IsAir)
                    {
                        continue;
                    }

                    var meta = blocks.GetStateMetadata(state);
                    if (!IsCustomShape(meta))
                    {
                        continue;
                    }

                    var (sky, blk) = neighborhood.GetLight(lx, wy, lz);
                    var effBlk = Math.Max(meta.LightEmission, blk);
                    float packedLight = (byte)(((sky & 0x0F) << 4) | (effBlk & 0x0F));
                    var origin = new Vector3(lx, ly, lz);

                    if (meta.IsFluid)
                    {
                        EmitSmoothWaterBlock(in neighborhood, blocks, transVerts, transIndices, lx, wy, lz, origin, atlas.GetTileIndex(meta.BlockKey, Direction.Up), packedLight);
                        continue;
                    }

                    var key = meta.BlockKey;
                    if (key == BuiltInBlocks.NetherPortal)
                    {
                        var axis = meta.Properties.TryGetValue("axis", out var ax) ? ax : "x";
                        // The portal is a translucent shimmering plane, so it belongs in the blended pass (not the opaque pass).
                        EmitPortalPlane(transVerts, transIndices, origin, atlas.GetTileIndex(key, Direction.North), axis, packedLight);
                    }
                    else if (key == BuiltInBlocks.Rail || key == BuiltInBlocks.PoweredRail)
                    {
                        EmitRailTrack(opaqueVerts, opaqueIndices, origin, meta, sky, effBlk);
                    }
                    else if (key == BuiltInBlocks.Torch || key == BuiltInBlocks.RedstoneTorch || key == BuiltInBlocks.RedstoneTorchOff)
                    {
                        EmitTorchWithFacing(opaqueVerts, opaqueIndices, origin, atlas.GetTileIndex(key, Direction.Up), meta, packedLight);
                    }
                    else if (key == BuiltInBlocks.Lantern)
                    {
                        EmitLantern(opaqueVerts, opaqueIndices, origin, atlas.GetTileIndex(key, Direction.Up), meta, packedLight);
                    }
                    else if (key == BuiltInBlocks.Ladder)
                    {
                        EmitLadderWithFacing(opaqueVerts, opaqueIndices, origin, atlas.GetTileIndex(key, Direction.North), meta, packedLight);
                    }
                    else if (key == BuiltInBlocks.Cobweb)
                    {
                        EmitCrossPlanes(opaqueVerts, opaqueIndices, origin, atlas.GetTileIndex(key, Direction.North), packedLight);
                    }
                    else if (key == BuiltInBlocks.Wheat)
                    {
                        var age = meta.Properties.TryGetValue("age", out var ageStr) && int.TryParse(ageStr, out var parsedAge)
                            ? parsedAge
                            : 0;
                        EmitCrossPlanes(opaqueVerts, opaqueIndices, origin, ProceduralAtlasGenerator.GetWheatStageTile(age), packedLight);
                    }
                    else if (key == BuiltInBlocks.Fire)
                    {
                        EmitFireFlames(opaqueVerts, opaqueIndices, origin, atlas.GetTileIndex(key, Direction.North), packedLight);
                    }
                    else if (key == BuiltInBlocks.RedstoneComparator || key == BuiltInBlocks.Composter ||
                             key == BuiltInBlocks.OakTrapdoor || key == BuiltInBlocks.IronTrapdoor ||
                             key == BuiltInBlocks.OakDoor || key == BuiltInBlocks.IronDoor ||
                             key == BuiltInBlocks.AmethystCluster || key == BuiltInBlocks.DirtPath ||
                             key == BuiltInBlocks.Farmland || key == BuiltInBlocks.MonsterSpawner)
                    {
                        var shapeLight = packedLight;
                        if (key == BuiltInBlocks.DirtPath || key == BuiltInBlocks.Farmland)
                        {
                            var (aboveSky, aboveBlk) = neighborhood.GetLight(lx, wy + 1, lz);
                            var maxSky = Math.Max(sky, aboveSky);
                            var maxBlk = Math.Max(effBlk, aboveBlk);
                            shapeLight = (byte)(((maxSky & 0x0F) << 4) | (maxBlk & 0x0F));
                        }

                        EmitComparatorComposterTrapdoorOrSpawner(opaqueVerts, opaqueIndices, origin, meta, shapeLight);
                    }
                    else if (key == BuiltInBlocks.OakFence)
                    {
                        EmitFence(in neighborhood, blocks, opaqueVerts, opaqueIndices, lx, wy, lz, origin, atlas.GetTileIndex(key, Direction.North), packedLight);
                    }
                    else
                    {
                        EmitRedstonePistonOrBed(in neighborhood, blocks, opaqueVerts, opaqueIndices, lx, wy, lz, origin, meta, packedLight);
                    }
                }
            }
        }
    }

    private static void EmitTorchWithFacing(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 o,
        ushort tile,
        BlockStateMetadata meta,
        float packedLight)
    {
        var facing = meta.Properties.TryGetValue("facing", out var f) ? f : "up";
        var uvMin = new Vector2(0.4375f, 0.375f);
        var uvMax = new Vector2(0.5625f, 1.0f);

        if (facing == "up")
        {
            EmitSubBox(verts, indices, o, new Vector3(0.4375f, 0.0f, 0.4375f), new Vector3(0.5625f, 0.625f, 0.5625f), tile, packedLight, uvMin, uvMax);
            return;
        }

        var (baseOff, tipOff) = facing switch
        {
            "north" => (new Vector3(0.50f, 0.22f, 0.96f), new Vector3(0.50f, 0.78f, 0.68f)),
            "south" => (new Vector3(0.50f, 0.22f, 0.04f), new Vector3(0.50f, 0.78f, 0.32f)),
            "west" => (new Vector3(0.96f, 0.22f, 0.50f), new Vector3(0.68f, 0.78f, 0.50f)),
            _ => (new Vector3(0.04f, 0.22f, 0.50f), new Vector3(0.32f, 0.78f, 0.50f)),
        };

        EmitTiltedPost(verts, indices, o + baseOff, o + tipOff, 0.0625f, tile, packedLight, uvMin, uvMax);
    }

    private static void EmitLantern(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 o,
        ushort tile,
        BlockStateMetadata meta,
        float packedLight)
    {
        var hanging = meta.Properties.TryGetValue("hanging", out var hv) && string.Equals(hv, "true", StringComparison.OrdinalIgnoreCase);
        var yOff = hanging ? 0.0625f : 0.0f;

        // 1. Main body [5..11, 0..7, 5..11] -> sides UV [0..6, 2..9]/16, top/bottom UV [0..6, 9..15]/16 (exact template_lantern.json!)
        EmitLanternBox(
            verts,
            indices,
            o + new Vector3(0.3125f, yOff, 0.3125f),
            o + new Vector3(0.6875f, yOff + 0.4375f, 0.6875f),
            tile,
            packedLight,
            sideUvMin: new Vector2(0.0f, 2.0f / 16.0f),
            sideUvMax: new Vector2(6.0f / 16.0f, 9.0f / 16.0f),
            capUvMin: new Vector2(0.0f, 9.0f / 16.0f),
            capUvMax: new Vector2(6.0f / 16.0f, 15.0f / 16.0f));

        // 2. Top iron cap [6..10, 7..9, 6..10] -> sides UV [1..5, 0..2]/16, top UV [1..5, 10..14]/16
        EmitLanternBox(
            verts,
            indices,
            o + new Vector3(0.375f, yOff + 0.4375f, 0.375f),
            o + new Vector3(0.625f, yOff + 0.5625f, 0.625f),
            tile,
            packedLight,
            sideUvMin: new Vector2(1.0f / 16.0f, 0.0f),
            sideUvMax: new Vector2(5.0f / 16.0f, 2.0f / 16.0f),
            capUvMin: new Vector2(1.0f / 16.0f, 10.0f / 16.0f),
            capUvMax: new Vector2(5.0f / 16.0f, 14.0f / 16.0f));

        // 3. Crossed 45-degree iron ring / ceiling chain [11..14, 1..3 or 1..8]/16
        var chainBottom = yOff + 0.5625f;
        var chainTop = hanging ? 1.0f : 0.6875f;
        var chainUvMaxY = hanging ? (8.0f / 16.0f) : (3.0f / 16.0f);
        EmitQuadWithUv(verts, indices, new Vector3(o.X + 0.40f, o.Y + chainBottom, o.Z + 0.40f), new Vector3(o.X + 0.60f, o.Y + chainBottom, o.Z + 0.60f), new Vector3(o.X + 0.60f, o.Y + chainTop, o.Z + 0.60f), new Vector3(o.X + 0.40f, o.Y + chainTop, o.Z + 0.40f), new Vector2(11.0f / 16.0f, chainUvMaxY), new Vector2(14.0f / 16.0f, chainUvMaxY), new Vector2(14.0f / 16.0f, 1.0f / 16.0f), new Vector2(11.0f / 16.0f, 1.0f / 16.0f), tile, 0.92f, packedLight, doubleSided: true);
        EmitQuadWithUv(verts, indices, new Vector3(o.X + 0.60f, o.Y + chainBottom, o.Z + 0.40f), new Vector3(o.X + 0.40f, o.Y + chainBottom, o.Z + 0.60f), new Vector3(o.X + 0.40f, o.Y + chainTop, o.Z + 0.60f), new Vector3(o.X + 0.60f, o.Y + chainTop, o.Z + 0.40f), new Vector2(11.0f / 16.0f, chainUvMaxY), new Vector2(14.0f / 16.0f, chainUvMaxY), new Vector2(14.0f / 16.0f, 1.0f / 16.0f), new Vector2(11.0f / 16.0f, 1.0f / 16.0f), tile, 0.92f, packedLight, doubleSided: true);
    }

    private static void EmitLanternBox(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 a,
        Vector3 b,
        ushort tile,
        float packedLight,
        Vector2 sideUvMin,
        Vector2 sideUvMax,
        Vector2 capUvMin,
        Vector2 capUvMax)
    {
        var sBL = new Vector2(sideUvMin.X, sideUvMax.Y);
        var sBR = new Vector2(sideUvMax.X, sideUvMax.Y);
        var sTR = new Vector2(sideUvMax.X, sideUvMin.Y);
        var sTL = new Vector2(sideUvMin.X, sideUvMin.Y);
        var cBL = new Vector2(capUvMin.X, capUvMax.Y);
        var cBR = new Vector2(capUvMax.X, capUvMax.Y);
        var cTR = new Vector2(capUvMax.X, capUvMin.Y);
        var cTL = new Vector2(capUvMin.X, capUvMin.Y);

        EmitQuad(verts, indices, new Vector3(a.X, a.Y, b.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(a.X, b.Y, b.Z), sBL, sBR, sTR, sTL, tile, 0.96f, packedLight);
        EmitQuad(verts, indices, new Vector3(b.X, a.Y, a.Z), new Vector3(a.X, a.Y, a.Z), new Vector3(a.X, b.Y, a.Z), new Vector3(b.X, b.Y, a.Z), sBL, sBR, sTR, sTL, tile, 0.96f, packedLight);
        EmitQuad(verts, indices, new Vector3(b.X, a.Y, b.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, b.Y, a.Z), new Vector3(b.X, b.Y, b.Z), sBL, sBR, sTR, sTL, tile, 0.92f, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, a.Y, a.Z), new Vector3(a.X, a.Y, b.Z), new Vector3(a.X, b.Y, b.Z), new Vector3(a.X, b.Y, a.Z), sBL, sBR, sTR, sTL, tile, 0.92f, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, b.Y, a.Z), new Vector3(a.X, b.Y, b.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(b.X, b.Y, a.Z), cTL, cBL, cBR, cTR, tile, 1.0f, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(a.X, a.Y, b.Z), cBL, cBR, cTR, cTL, tile, 0.82f, packedLight);
    }

    private static void EmitLadderWithFacing(List<ChunkVertex> verts, List<uint> indices, Vector3 o, ushort tile, BlockStateMetadata meta, float packedLight)
    {
        var facing = meta.Properties.TryGetValue("facing", out var f) ? f : "north";
        switch (facing)
        {
            case "south":
                EmitDoubleSidedQuad(verts, indices, new Vector3(o.X, o.Y, o.Z + 0.06f), new Vector3(o.X + 1.0f, o.Y, o.Z + 0.06f), new Vector3(o.X + 1.0f, o.Y + 1.0f, o.Z + 0.06f), new Vector3(o.X, o.Y + 1.0f, o.Z + 0.06f), tile, 0.88f, packedLight);
                break;
            case "west":
                EmitDoubleSidedQuad(verts, indices, new Vector3(o.X + 0.94f, o.Y, o.Z), new Vector3(o.X + 0.94f, o.Y, o.Z + 1.0f), new Vector3(o.X + 0.94f, o.Y + 1.0f, o.Z + 1.0f), new Vector3(o.X + 0.94f, o.Y + 1.0f, o.Z), tile, 0.84f, packedLight);
                break;
            case "east":
                EmitDoubleSidedQuad(verts, indices, new Vector3(o.X + 0.06f, o.Y, o.Z + 1.0f), new Vector3(o.X + 0.06f, o.Y, o.Z), new Vector3(o.X + 0.06f, o.Y + 1.0f, o.Z), new Vector3(o.X + 0.06f, o.Y + 1.0f, o.Z + 1.0f), tile, 0.84f, packedLight);
                break;
            default:
                EmitDoubleSidedQuad(verts, indices, new Vector3(o.X + 1.0f, o.Y, o.Z + 0.94f), new Vector3(o.X, o.Y, o.Z + 0.94f), new Vector3(o.X, o.Y + 1.0f, o.Z + 0.94f), new Vector3(o.X + 1.0f, o.Y + 1.0f, o.Z + 0.94f), tile, 0.88f, packedLight);
                break;
        }
    }

    private static void EmitFence(
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
        if (ConnectsFence(blocks, nb.GetBlockState(lx, wy, lz - 1))) EmitFenceRailPair(verts, indices, o, new Vector3(0.4375f, 0.0f, 0.0f), new Vector3(0.5625f, 0.0f, 0.375f), tile, packedLight);
        if (ConnectsFence(blocks, nb.GetBlockState(lx, wy, lz + 1))) EmitFenceRailPair(verts, indices, o, new Vector3(0.4375f, 0.0f, 0.625f), new Vector3(0.5625f, 0.0f, 1.0f), tile, packedLight);
        if (ConnectsFence(blocks, nb.GetBlockState(lx - 1, wy, lz))) EmitFenceRailPair(verts, indices, o, new Vector3(0.0f, 0.0f, 0.4375f), new Vector3(0.375f, 0.0f, 0.5625f), tile, packedLight);
        if (ConnectsFence(blocks, nb.GetBlockState(lx + 1, wy, lz))) EmitFenceRailPair(verts, indices, o, new Vector3(0.625f, 0.0f, 0.4375f), new Vector3(1.0f, 0.0f, 0.5625f), tile, packedLight);
    }

    private static void EmitFenceRailPair(List<ChunkVertex> v, List<uint> idx, Vector3 o, Vector3 minXZ, Vector3 maxXZ, ushort tile, float light)
    {
        EmitSubBox(v, idx, o, new Vector3(minXZ.X, 0.75f, minXZ.Z), new Vector3(maxXZ.X, 0.9375f, maxXZ.Z), tile, light, new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.3f));
        EmitSubBox(v, idx, o, new Vector3(minXZ.X, 0.375f, minXZ.Z), new Vector3(maxXZ.X, 0.5625f, maxXZ.Z), tile, light, new Vector2(0.2f, 0.5f), new Vector2(0.8f, 0.7f));
    }

    private static bool ConnectsFence(IBlockRegistry blocks, BlockState s)
    {
        if (s.IsAir) return false;
        var m = blocks.GetStateMetadata(s);
        return m.BlockKey == BuiltInBlocks.OakFence || m.IsOpaque;
    }
}
