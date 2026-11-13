using System.Globalization;
using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Emits 3D geometry for Redstone dust/components, extended Pistons &amp; PistonHeads, 2-block Beds, and shared sub-box primitives.
/// </summary>
internal static partial class ShapedBlockMesher
{
    private static void EmitRedstonePistonOrBed(
        in ChunkNeighborhood nb,
        IBlockRegistry blocks,
        List<ChunkVertex> verts,
        List<uint> indices,
        int lx,
        int wy,
        int lz,
        Vector3 o,
        BlockStateMetadata meta,
        float packedLight)
    {
        var key = meta.BlockKey;
        var powered = meta.Properties.TryGetValue("powered", out var pv) && string.Equals(pv, "true", StringComparison.OrdinalIgnoreCase);

        if (key == BuiltInBlocks.RedstoneWire)
        {
            var pow = meta.Properties.TryGetValue("power", out var pw) && int.TryParse(pw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pn) ? pn : 0;
            var shade = pow == 0 ? 0.34f : 0.56f + ((pow / 15.0f) * 0.44f);
            var y = o.Y + 0.02f;
            var cN = ConnectsRedstone(blocks, in nb, lx, wy, lz - 1);
            var cS = ConnectsRedstone(blocks, in nb, lx, wy, lz + 1);
            var cW = ConnectsRedstone(blocks, in nb, lx - 1, wy, lz);
            var cE = ConnectsRedstone(blocks, in nb, lx + 1, wy, lz);
            ushort tile = ProceduralAtlasGenerator.TileRedstoneWireDot;
            if ((cN || cS) && !cW && !cE) tile = ProceduralAtlasGenerator.TileRedstoneWireLine;
            EmitDoubleSidedQuad(verts, indices, new Vector3(o.X, y, o.Z), new Vector3(o.X, y, o.Z + 1.0f), new Vector3(o.X + 1.0f, y, o.Z + 1.0f), new Vector3(o.X + 1.0f, y, o.Z), tile, shade, packedLight);
        }
        else if (key == BuiltInBlocks.Lever)
        {
            var facing = meta.Properties.TryGetValue("facing", out var f) ? f : "up";
            if (facing == "up")
            {
                EmitSubBox(verts, indices, o, new Vector3(0.3125f, 0.0f, 0.25f), new Vector3(0.6875f, 0.1875f, 0.75f), ProceduralAtlasGenerator.TileCobblestone, packedLight, Vector2.Zero, Vector2.One);
                var tipZ = powered ? 0.80f : 0.20f;
                EmitTiltedPost(verts, indices, o + new Vector3(0.5f, 0.15f, 0.5f), o + new Vector3(0.5f, 0.62f, tipZ), 0.055f, ProceduralAtlasGenerator.TileOakPlanks, packedLight, Vector2.Zero, Vector2.One);
            }
            else
            {
                var (baseMin, baseMax, p0, p1) = facing switch
                {
                    "north" => (new Vector3(0.31f, 0.25f, 0.81f), new Vector3(0.69f, 0.75f, 1.0f), new Vector3(0.5f, 0.5f, 0.85f), new Vector3(0.5f, powered ? 0.20f : 0.80f, 0.45f)),
                    "south" => (new Vector3(0.31f, 0.25f, 0.0f), new Vector3(0.69f, 0.75f, 0.19f), new Vector3(0.5f, 0.5f, 0.15f), new Vector3(0.5f, powered ? 0.20f : 0.80f, 0.55f)),
                    "west" => (new Vector3(0.81f, 0.25f, 0.31f), new Vector3(1.0f, 0.75f, 0.69f), new Vector3(0.85f, 0.5f, 0.5f), new Vector3(0.45f, powered ? 0.20f : 0.80f, 0.5f)),
                    _ => (new Vector3(0.0f, 0.25f, 0.31f), new Vector3(0.19f, 0.75f, 0.69f), new Vector3(0.15f, 0.5f, 0.5f), new Vector3(0.55f, powered ? 0.20f : 0.80f, 0.5f)),
                };
                EmitSubBox(verts, indices, o, baseMin, baseMax, ProceduralAtlasGenerator.TileCobblestone, packedLight, Vector2.Zero, Vector2.One);
                EmitTiltedPost(verts, indices, o + p0, o + p1, 0.055f, ProceduralAtlasGenerator.TileOakPlanks, packedLight, Vector2.Zero, Vector2.One);
            }
        }
        else if (key == BuiltInBlocks.StoneButton)
        {
            var d = powered ? 0.0625f : 0.125f;
            var facing = meta.Properties.TryGetValue("facing", out var f) ? f : "up";
            var (min, max) = facing switch
            {
                "north" => (new Vector3(0.3125f, 0.375f, 1.0f - d), new Vector3(0.6875f, 0.625f, 1.0f)),
                "south" => (new Vector3(0.3125f, 0.375f, 0.0f), new Vector3(0.6875f, 0.625f, d)),
                "west" => (new Vector3(1.0f - d, 0.375f, 0.3125f), new Vector3(1.0f, 0.625f, 0.6875f)),
                "east" => (new Vector3(0.0f, 0.375f, 0.3125f), new Vector3(d, 0.625f, 0.6875f)),
                _ => (new Vector3(0.3125f, 0.0f, 0.375f), new Vector3(0.6875f, d, 0.625f)),
            };
            EmitSubBox(verts, indices, o, min, max, ProceduralAtlasGenerator.TileStone, packedLight, Vector2.Zero, Vector2.One);
        }
        else if (key == BuiltInBlocks.StonePressurePlate)
        {
            var h = powered ? 0.03125f : 0.0625f;
            EmitSubBox(verts, indices, o, new Vector3(0.0625f, 0.0f, 0.0625f), new Vector3(0.9375f, h, 0.9375f), ProceduralAtlasGenerator.TileStone, packedLight, Vector2.Zero, Vector2.One);
        }
        else if (key == BuiltInBlocks.RedstoneRepeater)
        {
            var topTile = powered ? ProceduralAtlasGenerator.TileRepeaterOn : ProceduralAtlasGenerator.TileRepeaterOff;
            var torchTile = powered ? ProceduralAtlasGenerator.TileRedstoneTorch : ProceduralAtlasGenerator.TileRedstoneTorchOff;
            var delay = meta.Properties.TryGetValue("delay", out var ds) && int.TryParse(ds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dn) ? Math.Clamp(dn, 1, 4) : 1;
            EmitSubBox(verts, indices, o, Vector3.Zero, new Vector3(1.0f, 0.125f, 1.0f), topTile, packedLight, Vector2.Zero, Vector2.One);
            EmitSubBox(verts, indices, o, new Vector3(0.4375f, 0.125f, 0.1875f), new Vector3(0.5625f, 0.4375f, 0.3125f), torchTile, packedLight, new Vector2(0.4375f, 0.375f), new Vector2(0.5625f, 0.85f));
            var rearZ = 0.35f + (delay * 0.12f);
            EmitSubBox(verts, indices, o, new Vector3(0.4375f, 0.125f, rearZ), new Vector3(0.5625f, 0.4375f, rearZ + 0.125f), torchTile, packedLight, new Vector2(0.4375f, 0.375f), new Vector2(0.5625f, 0.85f));
        }
        else if (key == BuiltInBlocks.Piston || key == BuiltInBlocks.StickyPiston)
        {
            var facing = meta.Properties.TryGetValue("facing", out var f) ? f : "up";
            var (min, max) = facing switch
            {
                "down" => (new Vector3(0.0f, 0.25f, 0.0f), Vector3.One),
                "north" => (new Vector3(0.0f, 0.0f, 0.25f), Vector3.One),
                "south" => (Vector3.Zero, new Vector3(1.0f, 1.0f, 0.75f)),
                "west" => (new Vector3(0.25f, 0.0f, 0.0f), Vector3.One),
                "east" => (Vector3.Zero, new Vector3(0.75f, 1.0f, 1.0f)),
                _ => (Vector3.Zero, new Vector3(1.0f, 0.75f, 1.0f)),
            };
            EmitDirectionalSubBox(
                verts,
                indices,
                o,
                min,
                max,
                facing,
                frontTile: ProceduralAtlasGenerator.TilePistonInner,
                backTile: ProceduralAtlasGenerator.TilePistonBottom,
                sideTile: ProceduralAtlasGenerator.TilePistonSide,
                packedLight);
        }
        else if (key == BuiltInBlocks.PistonHead)
        {
            var facing = meta.Properties.TryGetValue("facing", out var f) ? f : "up";
            var sticky = meta.Properties.TryGetValue("sticky", out var sv) && string.Equals(sv, "true", StringComparison.OrdinalIgnoreCase);
            var headTile = sticky ? ProceduralAtlasGenerator.TilePistonTopSticky : ProceduralAtlasGenerator.TilePistonTop;
            var (pMin, pMax, rMin, rMax) = facing switch
            {
                "down" => (Vector3.Zero, new Vector3(1.0f, 0.25f, 1.0f), new Vector3(0.375f, 0.25f, 0.375f), new Vector3(0.625f, 1.25f, 0.625f)),
                "north" => (Vector3.Zero, new Vector3(1.0f, 1.0f, 0.25f), new Vector3(0.375f, 0.375f, 0.25f), new Vector3(0.625f, 0.625f, 1.25f)),
                "south" => (new Vector3(0.0f, 0.0f, 0.75f), Vector3.One, new Vector3(0.375f, 0.375f, -0.25f), new Vector3(0.625f, 0.625f, 0.75f)),
                "west" => (Vector3.Zero, new Vector3(0.25f, 1.0f, 1.0f), new Vector3(0.25f, 0.375f, 0.375f), new Vector3(1.25f, 0.625f, 0.625f)),
                "east" => (new Vector3(0.75f, 0.0f, 0.0f), Vector3.One, new Vector3(-0.25f, 0.375f, 0.375f), new Vector3(0.75f, 0.625f, 0.625f)),
                _ => (new Vector3(0.0f, 0.75f, 0.0f), Vector3.One, new Vector3(0.375f, -0.25f, 0.375f), new Vector3(0.625f, 0.75f, 0.625f)),
            };
            EmitDirectionalSubBox(
                verts,
                indices,
                o,
                pMin,
                pMax,
                facing,
                frontTile: headTile,
                backTile: ProceduralAtlasGenerator.TilePistonTop,
                sideTile: ProceduralAtlasGenerator.TileOakPlanks,
                packedLight);
            EmitSubBox(verts, indices, o, rMin, rMax, ProceduralAtlasGenerator.TileOakPlanks, packedLight, Vector2.Zero, Vector2.One);
        }
        else if (key == BuiltInBlocks.RedBed)
        {
            EmitBedHalf(verts, indices, o, meta, packedLight);
        }
    }

    private static bool ConnectsRedstone(IBlockRegistry blocks, in ChunkNeighborhood nb, int x, int wy, int z)
    {
        var k = blocks.GetStateMetadata(nb.GetBlockState(x, wy, z)).BlockKey;
        if (IsRedstoneNode(k)) return true;
        if (IsRedstoneNode(blocks.GetStateMetadata(nb.GetBlockState(x, wy + 1, z)).BlockKey)) return true;
        return IsRedstoneNode(blocks.GetStateMetadata(nb.GetBlockState(x, wy - 1, z)).BlockKey);
    }

    private static bool IsRedstoneNode(OverHorizon.Core.Registry.ResourceKey k) =>
        k == BuiltInBlocks.RedstoneWire || k == BuiltInBlocks.RedstoneBlock || k == BuiltInBlocks.RedstoneTorch ||
        k == BuiltInBlocks.RedstoneTorchOff || k == BuiltInBlocks.Lever || k == BuiltInBlocks.StoneButton ||
        k == BuiltInBlocks.StonePressurePlate || k == BuiltInBlocks.RedstoneRepeater || k == BuiltInBlocks.Piston ||
        k == BuiltInBlocks.StickyPiston || k == BuiltInBlocks.RedstoneLamp || k == BuiltInBlocks.LitRedstoneLamp;

    private static void EmitTiltedPost(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 baseCenter,
        Vector3 tipCenter,
        float halfWidth,
        ushort tile,
        float packedLight,
        Vector2 uvMin,
        Vector2 uvMax)
    {
        var bx0 = baseCenter.X - halfWidth;
        var bx1 = baseCenter.X + halfWidth;
        var bz0 = baseCenter.Z - halfWidth;
        var bz1 = baseCenter.Z + halfWidth;
        var tx0 = tipCenter.X - halfWidth;
        var tx1 = tipCenter.X + halfWidth;
        var tz0 = tipCenter.Z - halfWidth;
        var tz1 = tipCenter.Z + halfWidth;

        var uvBL = new Vector2(uvMin.X, uvMax.Y);
        var uvBR = new Vector2(uvMax.X, uvMax.Y);
        var uvTR = new Vector2(uvMax.X, uvMin.Y);
        var uvTL = new Vector2(uvMin.X, uvMin.Y);

        EmitQuad(verts, indices, new Vector3(bx0, baseCenter.Y, bz1), new Vector3(bx1, baseCenter.Y, bz1), new Vector3(tx1, tipCenter.Y, tz1), new Vector3(tx0, tipCenter.Y, tz1), uvBL, uvBR, uvTR, uvTL, tile, 0.88f, packedLight);
        EmitQuad(verts, indices, new Vector3(bx1, baseCenter.Y, bz0), new Vector3(bx0, baseCenter.Y, bz0), new Vector3(tx0, tipCenter.Y, tz0), new Vector3(tx1, tipCenter.Y, tz0), uvBL, uvBR, uvTR, uvTL, tile, 0.88f, packedLight);
        EmitQuad(verts, indices, new Vector3(bx1, baseCenter.Y, bz1), new Vector3(bx1, baseCenter.Y, bz0), new Vector3(tx1, tipCenter.Y, tz0), new Vector3(tx1, tipCenter.Y, tz1), uvBL, uvBR, uvTR, uvTL, tile, 0.78f, packedLight);
        EmitQuad(verts, indices, new Vector3(bx0, baseCenter.Y, bz0), new Vector3(bx0, baseCenter.Y, bz1), new Vector3(tx0, tipCenter.Y, tz1), new Vector3(tx0, tipCenter.Y, tz0), uvBL, uvBR, uvTR, uvTL, tile, 0.78f, packedLight);
        EmitQuad(verts, indices, new Vector3(tx0, tipCenter.Y, tz0), new Vector3(tx0, tipCenter.Y, tz1), new Vector3(tx1, tipCenter.Y, tz1), new Vector3(tx1, tipCenter.Y, tz0), new Vector2(uvMin.X, uvMin.Y), new Vector2(uvMin.X, uvMin.Y + 0.125f), new Vector2(uvMax.X, uvMin.Y + 0.125f), new Vector2(uvMax.X, uvMin.Y), tile, 1.0f, packedLight);
    }

    private static void EmitCrossPlanes(List<ChunkVertex> verts, List<uint> indices, Vector3 o, ushort tile, float packedLight)
    {
        EmitDoubleSidedQuad(verts, indices, new Vector3(o.X, o.Y, o.Z), new Vector3(o.X + 1, o.Y, o.Z + 1), new Vector3(o.X + 1, o.Y + 1, o.Z + 1), new Vector3(o.X, o.Y + 1, o.Z), tile, 0.92f, packedLight);
        EmitDoubleSidedQuad(verts, indices, new Vector3(o.X + 1, o.Y, o.Z), new Vector3(o.X, o.Y, o.Z + 1), new Vector3(o.X, o.Y + 1, o.Z + 1), new Vector3(o.X + 1, o.Y + 1, o.Z), tile, 0.92f, packedLight);
    }

    private static void EmitDoubleSidedQuad(List<ChunkVertex> verts, List<uint> indices, Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3, ushort tile, float shade, float packedLight)
    {
        EmitQuad(verts, indices, c0, c1, c2, c3, new Vector2(0.01f, 0.99f), new Vector2(0.99f, 0.99f), new Vector2(0.99f, 0.01f), new Vector2(0.01f, 0.01f), tile, shade, packedLight);
        EmitQuad(verts, indices, c1, c0, c3, c2, new Vector2(0.01f, 0.99f), new Vector2(0.99f, 0.99f), new Vector2(0.99f, 0.01f), new Vector2(0.01f, 0.01f), tile, shade, packedLight);
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
        Vector2 uvMax,
        float shadeScale = 1.0f)
    {
        var a = o + min;
        var b = o + max;
        var uvBL = new Vector2(uvMin.X, uvMax.Y);
        var uvBR = new Vector2(uvMax.X, uvMax.Y);
        var uvTR = new Vector2(uvMax.X, uvMin.Y);
        var uvTL = new Vector2(uvMin.X, uvMin.Y);

        EmitQuad(verts, indices, new Vector3(a.X, a.Y, b.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(a.X, b.Y, b.Z), uvBL, uvBR, uvTR, uvTL, tile, 0.84f * shadeScale, packedLight);
        EmitQuad(verts, indices, new Vector3(b.X, a.Y, a.Z), new Vector3(a.X, a.Y, a.Z), new Vector3(a.X, b.Y, a.Z), new Vector3(b.X, b.Y, a.Z), uvBL, uvBR, uvTR, uvTL, tile, 0.84f * shadeScale, packedLight);
        EmitQuad(verts, indices, new Vector3(b.X, a.Y, b.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, b.Y, a.Z), new Vector3(b.X, b.Y, b.Z), uvBL, uvBR, uvTR, uvTL, tile, 0.72f * shadeScale, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, a.Y, a.Z), new Vector3(a.X, a.Y, b.Z), new Vector3(a.X, b.Y, b.Z), new Vector3(a.X, b.Y, a.Z), uvBL, uvBR, uvTR, uvTL, tile, 0.72f * shadeScale, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, b.Y, a.Z), new Vector3(a.X, b.Y, b.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(b.X, b.Y, a.Z), uvTL, uvBL, uvBR, uvTR, tile, 1.0f * shadeScale, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(a.X, a.Y, b.Z), uvBL, uvBR, uvTR, uvTL, tile, 0.55f * shadeScale, packedLight);
    }

    private static void EmitDirectionalSubBox(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 o,
        Vector3 min,
        Vector3 max,
        string facing,
        ushort frontTile,
        ushort backTile,
        ushort sideTile,
        float packedLight)
    {
        var a = o + min;
        var b = o + max;
        var uvBL = new Vector2(0.0f, 1.0f);
        var uvBR = new Vector2(1.0f, 1.0f);
        var uvTR = new Vector2(1.0f, 0.0f);
        var uvTL = new Vector2(0.0f, 0.0f);

        EmitQuad(verts, indices, new Vector3(a.X, a.Y, b.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(a.X, b.Y, b.Z), uvBL, uvBR, uvTR, uvTL, PickPistonFaceTile("south", facing, frontTile, backTile, sideTile), 0.84f, packedLight);
        EmitQuad(verts, indices, new Vector3(b.X, a.Y, a.Z), new Vector3(a.X, a.Y, a.Z), new Vector3(a.X, b.Y, a.Z), new Vector3(b.X, b.Y, a.Z), uvBL, uvBR, uvTR, uvTL, PickPistonFaceTile("north", facing, frontTile, backTile, sideTile), 0.84f, packedLight);
        EmitQuad(verts, indices, new Vector3(b.X, a.Y, b.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, b.Y, a.Z), new Vector3(b.X, b.Y, b.Z), uvBL, uvBR, uvTR, uvTL, PickPistonFaceTile("east", facing, frontTile, backTile, sideTile), 0.72f, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, a.Y, a.Z), new Vector3(a.X, a.Y, b.Z), new Vector3(a.X, b.Y, b.Z), new Vector3(a.X, b.Y, a.Z), uvBL, uvBR, uvTR, uvTL, PickPistonFaceTile("west", facing, frontTile, backTile, sideTile), 0.72f, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, b.Y, a.Z), new Vector3(a.X, b.Y, b.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(b.X, b.Y, a.Z), uvTL, uvBL, uvBR, uvTR, PickPistonFaceTile("up", facing, frontTile, backTile, sideTile), 1.0f, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(a.X, a.Y, b.Z), uvBL, uvBR, uvTR, uvTL, PickPistonFaceTile("down", facing, frontTile, backTile, sideTile), 0.55f, packedLight);
    }

    private static ushort PickPistonFaceTile(string dir, string facing, ushort frontTile, ushort backTile, ushort sideTile)
    {
        if (dir == facing) return frontTile;
        return IsOpposite(dir, facing) ? backTile : sideTile;
    }

    private static bool IsOpposite(string a, string b) =>
        (a == "north" && b == "south") || (a == "south" && b == "north") ||
        (a == "east" && b == "west") || (a == "west" && b == "east") ||
        (a == "up" && b == "down") || (a == "down" && b == "up");

    private static void EmitBedHalf(List<ChunkVertex> verts, List<uint> indices, Vector3 o, BlockStateMetadata meta, float packedLight)
    {
        var isHead = meta.Properties.TryGetValue("part", out var pt) && string.Equals(pt, "head", StringComparison.OrdinalIgnoreCase);
        var facing = meta.Properties.TryGetValue("facing", out var f) ? f : "north";
        var topTile = isHead ? ProceduralAtlasGenerator.TileRedBedTopHead : ProceduralAtlasGenerator.TileRedBedTopFoot;
        var sideTile = isHead ? ProceduralAtlasGenerator.TileRedBedHeadSide : ProceduralAtlasGenerator.TileRedBedSide;

        var a = o + new Vector3(0.0f, 0.1875f, 0.0f);
        var b = o + new Vector3(1.0f, 0.5625f, 1.0f);
        var rotSteps = facing switch
        {
            "east" => 1,
            "south" => 2,
            "west" => 3,
            _ => 0,
        };
        var (t0, t1, t2, t3) = GetRotatedUv90(rotSteps);

        // Top pillow/blanket + Oak bottom + mattress sides (culling the internal seam face between head & foot!)
        var seamDir = isHead ? OppositeName(facing) : facing;
        EmitQuad(verts, indices, new Vector3(a.X, b.Y, a.Z), new Vector3(a.X, b.Y, b.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(b.X, b.Y, a.Z), t0, t1, t2, t3, topTile, 1.0f, packedLight);
        EmitQuad(verts, indices, new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(a.X, a.Y, b.Z), Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY, ProceduralAtlasGenerator.TileOakPlanks, 0.58f, packedLight);

        var sBL = new Vector2(0.0f, 13.0f / 16.0f);
        var sBR = new Vector2(1.0f, 13.0f / 16.0f);
        var sTR = new Vector2(1.0f, 7.0f / 16.0f);
        var sTL = new Vector2(0.0f, 7.0f / 16.0f);
        if (seamDir != "south") EmitQuad(verts, indices, new Vector3(a.X, a.Y, b.Z), new Vector3(b.X, a.Y, b.Z), new Vector3(b.X, b.Y, b.Z), new Vector3(a.X, b.Y, b.Z), sBL, sBR, sTR, sTL, sideTile, 0.86f, packedLight);
        if (seamDir != "north") EmitQuad(verts, indices, new Vector3(b.X, a.Y, a.Z), new Vector3(a.X, a.Y, a.Z), new Vector3(a.X, b.Y, a.Z), new Vector3(b.X, b.Y, a.Z), sBL, sBR, sTR, sTL, sideTile, 0.86f, packedLight);
        if (seamDir != "east") EmitQuad(verts, indices, new Vector3(b.X, a.Y, b.Z), new Vector3(b.X, a.Y, a.Z), new Vector3(b.X, b.Y, a.Z), new Vector3(b.X, b.Y, b.Z), sBL, sBR, sTR, sTL, sideTile, 0.74f, packedLight);
        if (seamDir != "west") EmitQuad(verts, indices, new Vector3(a.X, a.Y, a.Z), new Vector3(a.X, a.Y, b.Z), new Vector3(a.X, b.Y, b.Z), new Vector3(a.X, b.Y, a.Z), sBL, sBR, sTR, sTL, sideTile, 0.74f, packedLight);

        // Emit ONLY the 2 outer corner wooden legs on this bed half (4 legs total across the 2-block bed!)
        var endDir = isHead ? facing : OppositeName(facing);
        if (endDir == "north")
        {
            EmitSubBox(verts, indices, o, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.1875f, 0.1875f, 0.1875f), ProceduralAtlasGenerator.TileOakPlanks, packedLight, Vector2.Zero, new Vector2(0.25f, 0.25f));
            EmitSubBox(verts, indices, o, new Vector3(0.8125f, 0.0f, 0.0f), new Vector3(1.0f, 0.1875f, 0.1875f), ProceduralAtlasGenerator.TileOakPlanks, packedLight, Vector2.Zero, new Vector2(0.25f, 0.25f));
        }
        else if (endDir == "south")
        {
            EmitSubBox(verts, indices, o, new Vector3(0.0f, 0.0f, 0.8125f), new Vector3(0.1875f, 0.1875f, 1.0f), ProceduralAtlasGenerator.TileOakPlanks, packedLight, Vector2.Zero, new Vector2(0.25f, 0.25f));
            EmitSubBox(verts, indices, o, new Vector3(0.8125f, 0.0f, 0.8125f), new Vector3(1.0f, 0.1875f, 1.0f), ProceduralAtlasGenerator.TileOakPlanks, packedLight, Vector2.Zero, new Vector2(0.25f, 0.25f));
        }
        else if (endDir == "west")
        {
            EmitSubBox(verts, indices, o, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.1875f, 0.1875f, 0.1875f), ProceduralAtlasGenerator.TileOakPlanks, packedLight, Vector2.Zero, new Vector2(0.25f, 0.25f));
            EmitSubBox(verts, indices, o, new Vector3(0.0f, 0.0f, 0.8125f), new Vector3(0.1875f, 0.1875f, 1.0f), ProceduralAtlasGenerator.TileOakPlanks, packedLight, Vector2.Zero, new Vector2(0.25f, 0.25f));
        }
        else
        {
            EmitSubBox(verts, indices, o, new Vector3(0.8125f, 0.0f, 0.0f), new Vector3(1.0f, 0.1875f, 0.1875f), ProceduralAtlasGenerator.TileOakPlanks, packedLight, Vector2.Zero, new Vector2(0.25f, 0.25f));
            EmitSubBox(verts, indices, o, new Vector3(0.8125f, 0.0f, 0.8125f), new Vector3(1.0f, 0.1875f, 1.0f), ProceduralAtlasGenerator.TileOakPlanks, packedLight, Vector2.Zero, new Vector2(0.25f, 0.25f));
        }
    }

    private static string OppositeName(string dir) => dir switch
    {
        "north" => "south",
        "south" => "north",
        "west" => "east",
        "east" => "west",
        "up" => "down",
        _ => "up",
    };

    private static void EmitQuad(List<ChunkVertex> verts, List<uint> indices, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, ushort tile, float shade, float packedLight)
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
