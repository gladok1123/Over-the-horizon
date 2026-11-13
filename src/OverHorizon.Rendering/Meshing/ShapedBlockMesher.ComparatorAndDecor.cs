using System.Globalization;
using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Emits 3D geometry for dynamic <c>RedstoneWire</c> (N/S/E/W + vertical step-up), oriented <c>RedstoneRepeater</c>
/// (with Bedrock lock bar), <c>RedstoneComparator</c>, <c>Composter</c>, <c>OakTrapdoor</c>, <c>MonsterSpawner</c>, and <c>Fire</c>.
/// </summary>
internal static partial class ShapedBlockMesher
{
    private static void EmitDynamicRedstoneWire(
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
        var pow = meta.Properties.TryGetValue("power", out var pw) &&
                  int.TryParse(pw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pn)
            ? Math.Clamp(pn, 0, 15)
            : 0;
        var shade = pow == 0 ? 0.30f : 0.48f + ((pow / 15.0f) * 0.52f);
        var aboveOpaque = blocks.GetStateMetadata(nb.GetBlockState(lx, wy + 1, lz)).IsOpaque;

        var (cN, upN) = CheckWireArm(blocks, in nb, lx, wy, lz - 1, aboveOpaque);
        var (cS, upS) = CheckWireArm(blocks, in nb, lx, wy, lz + 1, aboveOpaque);
        var (cW, upW) = CheckWireArm(blocks, in nb, lx - 1, wy, lz, aboveOpaque);
        var (cE, upE) = CheckWireArm(blocks, in nb, lx + 1, wy, lz, aboveOpaque);

        // Vanilla straight-line extension: if connected on only one cardinal side, extend opposite half-arm too
        if (cN && !cS && !cW && !cE) cS = true;
        else if (cS && !cN && !cW && !cE) cN = true;
        else if (cW && !cE && !cN && !cS) cE = true;
        else if (cE && !cW && !cN && !cS) cW = true;

        var tile = ProceduralAtlasGenerator.TileRedstoneBlock;
        var uvMin = new Vector2(0.25f, 0.25f);
        var uvMax = new Vector2(0.75f, 0.75f);

        // Central dust hub
        EmitSubBox(verts, indices, o, new Vector3(0.36f, 0.0f, 0.36f), new Vector3(0.64f, 0.025f, 0.64f), tile, packedLight, uvMin, uvMax, shade);

        if (cN)
        {
            EmitSubBox(verts, indices, o, new Vector3(0.41f, 0.0f, 0.0f), new Vector3(0.59f, 0.024f, 0.36f), tile, packedLight, uvMin, uvMax, shade);
            if (upN) EmitSubBox(verts, indices, o, new Vector3(0.41f, 0.02f, 0.0f), new Vector3(0.59f, 1.02f, 0.025f), tile, packedLight, uvMin, uvMax, shade);
        }

        if (cS)
        {
            EmitSubBox(verts, indices, o, new Vector3(0.41f, 0.0f, 0.64f), new Vector3(0.59f, 0.024f, 1.0f), tile, packedLight, uvMin, uvMax, shade);
            if (upS) EmitSubBox(verts, indices, o, new Vector3(0.41f, 0.02f, 0.975f), new Vector3(0.59f, 1.02f, 1.0f), tile, packedLight, uvMin, uvMax, shade);
        }

        if (cW)
        {
            EmitSubBox(verts, indices, o, new Vector3(0.0f, 0.0f, 0.41f), new Vector3(0.36f, 0.024f, 0.59f), tile, packedLight, uvMin, uvMax, shade);
            if (upW) EmitSubBox(verts, indices, o, new Vector3(0.0f, 0.02f, 0.41f), new Vector3(0.025f, 1.02f, 0.59f), tile, packedLight, uvMin, uvMax, shade);
        }

        if (cE)
        {
            EmitSubBox(verts, indices, o, new Vector3(0.64f, 0.0f, 0.41f), new Vector3(1.0f, 0.024f, 0.59f), tile, packedLight, uvMin, uvMax, shade);
            if (upE) EmitSubBox(verts, indices, o, new Vector3(0.975f, 0.02f, 0.41f), new Vector3(1.0f, 1.02f, 0.59f), tile, packedLight, uvMin, uvMax, shade);
        }
    }

    private static (bool Connects, bool StepUp) CheckWireArm(IBlockRegistry blocks, in ChunkNeighborhood nb, int nx, int wy, int nz, bool aboveCenterOpaque)
    {
        var sideMeta = blocks.GetStateMetadata(nb.GetBlockState(nx, wy, nz));
        if (IsRedstoneNode(sideMeta.BlockKey))
        {
            return (true, false);
        }

        if (!aboveCenterOpaque && blocks.GetStateMetadata(nb.GetBlockState(nx, wy + 1, nz)).BlockKey == BuiltInBlocks.RedstoneWire)
        {
            return (true, true);
        }

        if (!sideMeta.IsOpaque && blocks.GetStateMetadata(nb.GetBlockState(nx, wy - 1, nz)).BlockKey == BuiltInBlocks.RedstoneWire)
        {
            return (true, false);
        }

        return (false, false);
    }

    private static void EmitOrientedRepeater(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 o,
        BlockStateMetadata meta,
        bool powered,
        float packedLight)
    {
        var topTile = powered ? ProceduralAtlasGenerator.TileRepeaterOn : ProceduralAtlasGenerator.TileRepeaterOff;
        var torchTile = powered ? ProceduralAtlasGenerator.TileRedstoneTorch : ProceduralAtlasGenerator.TileRedstoneTorchOff;
        var facing = meta.Properties.TryGetValue("facing", out var f) ? f : "north";
        var locked = meta.Properties.TryGetValue("locked", out var lv) && string.Equals(lv, "true", StringComparison.OrdinalIgnoreCase);
        var delay = meta.Properties.TryGetValue("delay", out var ds) && int.TryParse(ds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dn)
            ? Math.Clamp(dn, 1, 4)
            : 1;

        EmitSubBox(verts, indices, o, Vector3.Zero, new Vector3(1.0f, 0.125f, 1.0f), topTile, packedLight, Vector2.Zero, Vector2.One);

        var frontPos = RotatePlateLocal(new Vector2(0.5f, 0.22f), facing);
        var rearOffset = 0.34f + (delay * 0.13f);
        var rearPos = RotatePlateLocal(new Vector2(0.5f, rearOffset), facing);
        var tUvMin = new Vector2(0.4375f, 0.375f);
        var tUvMax = new Vector2(0.5625f, 0.85f);

        EmitSubBox(verts, indices, o, new Vector3(frontPos.X - 0.0625f, 0.125f, frontPos.Y - 0.0625f), new Vector3(frontPos.X + 0.0625f, 0.4375f, frontPos.Y + 0.0625f), torchTile, packedLight, tUvMin, tUvMax);

        if (locked)
        {
            var barA = RotatePlateLocal(new Vector2(0.125f, rearOffset), facing);
            var barB = RotatePlateLocal(new Vector2(0.875f, rearOffset), facing);
            var minX = MathF.Min(barA.X, barB.X) - 0.0625f;
            var maxX = MathF.Max(barA.X, barB.X) + 0.0625f;
            var minZ = MathF.Min(barA.Y, barB.Y) - 0.0625f;
            var maxZ = MathF.Max(barA.Y, barB.Y) + 0.0625f;
            EmitSubBox(verts, indices, o, new Vector3(minX, 0.125f, minZ), new Vector3(maxX, 0.26f, maxZ), ProceduralAtlasGenerator.TileBedrock, packedLight, Vector2.Zero, Vector2.One);
        }
        else
        {
            EmitSubBox(verts, indices, o, new Vector3(rearPos.X - 0.0625f, 0.125f, rearPos.Y - 0.0625f), new Vector3(rearPos.X + 0.0625f, 0.4375f, rearPos.Y + 0.0625f), torchTile, packedLight, tUvMin, tUvMax);
        }
    }

    private static void EmitComparatorComposterTrapdoorOrSpawner(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 o,
        BlockStateMetadata meta,
        float packedLight)
    {
        var key = meta.BlockKey;
        if (key == BuiltInBlocks.RedstoneComparator)
        {
            var powered = meta.Properties.TryGetValue("powered", out var pv) && string.Equals(pv, "true", StringComparison.OrdinalIgnoreCase);
            var subtract = meta.Properties.TryGetValue("mode", out var mv) && string.Equals(mv, "subtract", StringComparison.OrdinalIgnoreCase);
            var facing = meta.Properties.TryGetValue("facing", out var f) ? f : "north";
            var slabTile = powered ? ProceduralAtlasGenerator.TileComparatorOn : ProceduralAtlasGenerator.TileComparatorOff;
            var rearTorch = powered ? ProceduralAtlasGenerator.TileRedstoneTorch : ProceduralAtlasGenerator.TileRedstoneTorchOff;
            var frontTorch = subtract ? ProceduralAtlasGenerator.TileRedstoneTorch : ProceduralAtlasGenerator.TileRedstoneTorchOff;

            EmitSubBox(verts, indices, o, Vector3.Zero, new Vector3(1.0f, 0.125f, 1.0f), slabTile, packedLight, Vector2.Zero, Vector2.One);

            var tUvMin = new Vector2(0.4375f, 0.375f);
            var tUvMax = new Vector2(0.5625f, 0.85f);
            var front = RotatePlateLocal(new Vector2(0.50f, 0.22f), facing);
            var rearL = RotatePlateLocal(new Vector2(0.28f, 0.74f), facing);
            var rearR = RotatePlateLocal(new Vector2(0.72f, 0.74f), facing);
            var frontH = subtract ? 0.42f : 0.34f;
            EmitSubBox(verts, indices, o, new Vector3(front.X - 0.0625f, 0.125f, front.Y - 0.0625f), new Vector3(front.X + 0.0625f, frontH, front.Y + 0.0625f), frontTorch, packedLight, tUvMin, tUvMax);
            EmitSubBox(verts, indices, o, new Vector3(rearL.X - 0.0625f, 0.125f, rearL.Y - 0.0625f), new Vector3(rearL.X + 0.0625f, 0.44f, rearL.Y + 0.0625f), rearTorch, packedLight, tUvMin, tUvMax);
            EmitSubBox(verts, indices, o, new Vector3(rearR.X - 0.0625f, 0.125f, rearR.Y - 0.0625f), new Vector3(rearR.X + 0.0625f, 0.44f, rearR.Y + 0.0625f), rearTorch, packedLight, tUvMin, tUvMax);
            return;
        }

        if (key == BuiltInBlocks.Composter)
        {
            var side = ProceduralAtlasGenerator.TileComposterSide;
            EmitSubBox(verts, indices, o, Vector3.Zero, new Vector3(1.0f, 0.125f, 1.0f), ProceduralAtlasGenerator.TileComposterBottom, packedLight, Vector2.Zero, Vector2.One);
            EmitSubBox(verts, indices, o, new Vector3(0.0f, 0.125f, 0.0f), new Vector3(0.125f, 1.0f, 1.0f), side, packedLight, Vector2.Zero, Vector2.One);
            EmitSubBox(verts, indices, o, new Vector3(0.875f, 0.125f, 0.0f), new Vector3(1.0f, 1.0f, 1.0f), side, packedLight, Vector2.Zero, Vector2.One);
            EmitSubBox(verts, indices, o, new Vector3(0.125f, 0.125f, 0.0f), new Vector3(0.875f, 1.0f, 0.125f), side, packedLight, Vector2.Zero, Vector2.One);
            EmitSubBox(verts, indices, o, new Vector3(0.125f, 0.125f, 0.875f), new Vector3(0.875f, 1.0f, 1.0f), side, packedLight, Vector2.Zero, Vector2.One);

            var level = meta.Properties.TryGetValue("level", out var ls) && int.TryParse(ls, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lv) ? Math.Clamp(lv, 0, 8) : 0;
            if (level > 0)
            {
                var fillTop = 0.16f + (level * 0.095f);
                var fillTile = level >= 8 ? ProceduralAtlasGenerator.TileSnowBlock : ProceduralAtlasGenerator.TileComposterCompost;
                EmitSubBox(verts, indices, o, new Vector3(0.125f, 0.125f, 0.125f), new Vector3(0.875f, fillTop, 0.875f), fillTile, packedLight, Vector2.Zero, Vector2.One);
            }

            return;
        }

        if (key == BuiltInBlocks.OakTrapdoor)
        {
            var open = meta.Properties.TryGetValue("open", out var ov) && string.Equals(ov, "true", StringComparison.OrdinalIgnoreCase);
            var topHalf = meta.Properties.TryGetValue("half", out var hv) && string.Equals(hv, "top", StringComparison.OrdinalIgnoreCase);
            var facing = meta.Properties.TryGetValue("facing", out var f) ? f : "north";
            var tile = ProceduralAtlasGenerator.TileOakTrapdoor;
            if (!open)
            {
                var y0 = topHalf ? 0.8125f : 0.0f;
                EmitSubBox(verts, indices, o, new Vector3(0.0f, y0, 0.0f), new Vector3(1.0f, y0 + 0.1875f, 1.0f), tile, packedLight, Vector2.Zero, Vector2.One);
                return;
            }

            var (min, max) = facing switch
            {
                "north" => (new Vector3(0.0f, 0.0f, 0.8125f), Vector3.One),
                "south" => (Vector3.Zero, new Vector3(1.0f, 1.0f, 0.1875f)),
                "west" => (new Vector3(0.8125f, 0.0f, 0.0f), Vector3.One),
                _ => (Vector3.Zero, new Vector3(0.1875f, 1.0f, 1.0f)),
            };
            EmitSubBox(verts, indices, o, min, max, tile, packedLight, Vector2.Zero, Vector2.One);
            return;
        }

        // MonsterSpawner: outer dark iron cage + glowing fiery core inside!
        EmitSubBox(verts, indices, o, Vector3.Zero, Vector3.One, ProceduralAtlasGenerator.TileMonsterSpawner, packedLight, Vector2.Zero, Vector2.One);
        EmitSubBox(verts, indices, o, new Vector3(0.32f, 0.25f, 0.32f), new Vector3(0.68f, 0.65f, 0.68f), ProceduralAtlasGenerator.TileFire, 240.0f, Vector2.Zero, Vector2.One);
    }

    private static void EmitFireFlames(List<ChunkVertex> verts, List<uint> indices, Vector3 o, ushort tile, float packedLight)
    {
        EmitCrossPlanes(verts, indices, o, tile, packedLight);
        EmitDoubleSidedQuad(verts, indices, new Vector3(o.X + 0.08f, o.Y, o.Z + 0.08f), new Vector3(o.X + 0.92f, o.Y, o.Z + 0.08f), new Vector3(o.X + 0.82f, o.Y + 0.92f, o.Z + 0.28f), new Vector3(o.X + 0.18f, o.Y + 0.92f, o.Z + 0.28f), tile, 0.98f, packedLight);
        EmitDoubleSidedQuad(verts, indices, new Vector3(o.X + 0.08f, o.Y, o.Z + 0.92f), new Vector3(o.X + 0.92f, o.Y, o.Z + 0.92f), new Vector3(o.X + 0.82f, o.Y + 0.92f, o.Z + 0.72f), new Vector3(o.X + 0.18f, o.Y + 0.92f, o.Z + 0.72f), tile, 0.98f, packedLight);
    }

    private static Vector2 RotatePlateLocal(Vector2 uvNorth, string facing) =>
        facing switch
        {
            "south" => new Vector2(1.0f - uvNorth.X, 1.0f - uvNorth.Y),
            "east" => new Vector2(1.0f - uvNorth.Y, uvNorth.X),
            "west" => new Vector2(uvNorth.Y, 1.0f - uvNorth.X),
            _ => uvNorth,
        };
}
