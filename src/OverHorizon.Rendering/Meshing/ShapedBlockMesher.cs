using System.Globalization;
using System.Numerics;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Meshing;

/// <summary>
/// Emits authentic sub-voxel 3D geometry for non-cube blocks (<c>Torch</c>, <c>RedstoneTorch</c>, <c>Lantern</c>, <c>Cobweb</c>,
/// <c>Rail</c>, <c>Ladder</c>, <c>OakFence</c>, Redstone components, <c>Piston</c>s, <c>RedBed</c>, <c>Fire</c>, and flowing <c>Water</c>).
/// </summary>
internal static partial class ShapedBlockMesher
{
    public static bool IsCustomShape(BlockStateMetadata meta)
    {
        var key = meta.BlockKey;
        if (key == BuiltInBlocks.Water)
        {
            return meta.Properties.TryGetValue("level", out var lv) && !string.Equals(lv, "0", StringComparison.Ordinal);
        }

        if (key == BuiltInBlocks.Piston || key == BuiltInBlocks.StickyPiston)
        {
            return meta.Properties.TryGetValue("extended", out var ext) && string.Equals(ext, "true", StringComparison.OrdinalIgnoreCase);
        }

        return key == BuiltInBlocks.Torch ||
            key == BuiltInBlocks.RedstoneTorch ||
            key == BuiltInBlocks.RedstoneTorchOff ||
            key == BuiltInBlocks.Lantern ||
            key == BuiltInBlocks.Cobweb ||
            key == BuiltInBlocks.Rail ||
            key == BuiltInBlocks.Ladder ||
            key == BuiltInBlocks.OakFence ||
            key == BuiltInBlocks.RedstoneWire ||
            key == BuiltInBlocks.Lever ||
            key == BuiltInBlocks.StoneButton ||
            key == BuiltInBlocks.StonePressurePlate ||
            key == BuiltInBlocks.RedstoneRepeater ||
            key == BuiltInBlocks.PistonHead ||
            key == BuiltInBlocks.RedBed ||
            key == BuiltInBlocks.Fire;
    }

    public static void AppendSectionShapes(
        in ChunkNeighborhood neighborhood,
        int sectionIndex,
        IBlockRegistry blocks,
        TextureAtlasData atlas,
        List<ChunkVertex> verts,
        List<uint> indices,
        List<ChunkVertex> transVerts,
        List<uint> transIndices)
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
                    if (state.IsAir) continue;

                    var meta = blocks.GetStateMetadata(state);
                    if (!IsCustomShape(meta)) continue;

                    var (sky, blk) = neighborhood.GetLight(lx, wy, lz);
                    var effBlk = Math.Max(meta.LightEmission, blk);
                    var packedLight = (float)(((sky & 0x0F) << 4) | (effBlk & 0x0F));
                    var tile = atlas.GetTileIndex(meta.BlockKey, Direction.North);
                    var origin = new Vector3(lx, ly, lz);
                    var key = meta.BlockKey;

                    if (key == BuiltInBlocks.Torch || key == BuiltInBlocks.RedstoneTorch || key == BuiltInBlocks.RedstoneTorchOff)
                    {
                        EmitFloorOrWallTorch(verts, indices, origin, meta, tile, packedLight);
                    }
                    else if (key == BuiltInBlocks.Lantern)
                    {
                        EmitSubBox(verts, indices, origin, new Vector3(0.3125f, 0.0f, 0.3125f), new Vector3(0.6875f, 0.4375f, 0.6875f), tile, packedLight, new Vector2(0.3125f, 0.375f), new Vector2(0.6875f, 0.8125f));
                        EmitSubBox(verts, indices, origin, new Vector3(0.375f, 0.4375f, 0.375f), new Vector3(0.625f, 0.5625f, 0.625f), tile, packedLight, new Vector2(0.375f, 0.25f), new Vector2(0.625f, 0.375f));
                    }
                    else if (key == BuiltInBlocks.Cobweb || key == BuiltInBlocks.Fire)
                    {
                        EmitCrossPlanes(verts, indices, origin, tile, packedLight);
                    }
                    else if (key == BuiltInBlocks.Rail)
                    {
                        var y = origin.Y + 0.0625f;
                        EmitDoubleSidedQuad(verts, indices, new Vector3(origin.X, y, origin.Z), new Vector3(origin.X, y, origin.Z + 1.0f), new Vector3(origin.X + 1.0f, y, origin.Z + 1.0f), new Vector3(origin.X + 1.0f, y, origin.Z), tile, 0.95f, packedLight);
                    }
                    else if (key == BuiltInBlocks.Ladder)
                    {
                        EmitWallLadder(in neighborhood, blocks, verts, indices, lx, wy, lz, origin, meta, tile, packedLight);
                    }
                    else if (key == BuiltInBlocks.OakFence)
                    {
                        EmitConnectedFence(in neighborhood, blocks, verts, indices, lx, wy, lz, origin, tile, packedLight);
                    }
                    else if (key == BuiltInBlocks.Water)
                    {
                        var lvl = meta.Properties.TryGetValue("level", out var ls) && int.TryParse(ls, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? Math.Clamp(parsed, 1, 7) : 1;
                        var h = Math.Clamp((8.0f - lvl) / 9.0f, 0.14f, 0.88f);
                        EmitSubBox(transVerts, transIndices, origin, Vector3.Zero, new Vector3(1.0f, h, 1.0f), tile, packedLight, Vector2.Zero, Vector2.One, shadeScale: 0.96f);
                    }
                    else
                    {
                        EmitRedstonePistonOrBed(in neighborhood, blocks, verts, indices, lx, wy, lz, origin, meta, packedLight);
                    }
                }
            }
        }
    }

    private static void EmitFloorOrWallTorch(
        List<ChunkVertex> verts,
        List<uint> indices,
        Vector3 o,
        BlockStateMetadata meta,
        ushort tile,
        float packedLight)
    {
        var facing = meta.Properties.TryGetValue("facing", out var f) ? f : "up";
        if (facing == "up")
        {
            EmitSubBox(verts, indices, o, new Vector3(0.4375f, 0.0f, 0.4375f), new Vector3(0.5625f, 0.625f, 0.5625f), tile, packedLight, new Vector2(0.4375f, 0.375f), new Vector2(0.5625f, 1.0f));
            return;
        }

        // Wall-mounted torch tilted ~22 degrees away from the supporting wall so its base meets the wall flush!
        var (wallOffset, tipOffset) = facing switch
        {
            "north" => (new Vector3(0.5f, 0.22f, 0.96f), new Vector3(0.5f, 0.80f, 0.70f)),
            "south" => (new Vector3(0.5f, 0.22f, 0.04f), new Vector3(0.5f, 0.80f, 0.30f)),
            "west" => (new Vector3(0.96f, 0.22f, 0.5f), new Vector3(0.70f, 0.80f, 0.5f)),
            _ => (new Vector3(0.04f, 0.22f, 0.5f), new Vector3(0.30f, 0.80f, 0.5f)),
        };

        EmitTiltedPost(verts, indices, o + wallOffset, o + tipOffset, halfWidth: 0.0625f, tile, packedLight, new Vector2(0.4375f, 0.375f), new Vector2(0.5625f, 1.0f));
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
        BlockStateMetadata meta,
        ushort tile,
        float packedLight)
    {
        var facing = meta.Properties.TryGetValue("facing", out var f) ? f : string.Empty;
        if (facing == "north" || (string.IsNullOrEmpty(facing) && blocks.GetStateMetadata(nb.GetBlockState(lx, wy, lz + 1)).IsSolid))
        {
            var z = o.Z + 0.9375f;
            EmitDoubleSidedQuad(verts, indices, new Vector3(o.X, o.Y, z), new Vector3(o.X + 1, o.Y, z), new Vector3(o.X + 1, o.Y + 1, z), new Vector3(o.X, o.Y + 1, z), tile, 0.88f, packedLight);
        }
        else if (facing == "west" || (string.IsNullOrEmpty(facing) && blocks.GetStateMetadata(nb.GetBlockState(lx + 1, wy, lz)).IsSolid))
        {
            var x = o.X + 0.9375f;
            EmitDoubleSidedQuad(verts, indices, new Vector3(x, o.Y, o.Z + 1), new Vector3(x, o.Y, o.Z), new Vector3(x, o.Y + 1, o.Z), new Vector3(x, o.Y + 1, o.Z + 1), tile, 0.88f, packedLight);
        }
        else if (facing == "east" || (string.IsNullOrEmpty(facing) && blocks.GetStateMetadata(nb.GetBlockState(lx - 1, wy, lz)).IsSolid))
        {
            var x = o.X + 0.0625f;
            EmitDoubleSidedQuad(verts, indices, new Vector3(x, o.Y, o.Z), new Vector3(x, o.Y, o.Z + 1), new Vector3(x, o.Y + 1, o.Z + 1), new Vector3(x, o.Y + 1, o.Z), tile, 0.88f, packedLight);
        }
        else
        {
            var z = o.Z + 0.0625f;
            EmitDoubleSidedQuad(verts, indices, new Vector3(o.X + 1, o.Y, z), new Vector3(o.X, o.Y, z), new Vector3(o.X, o.Y + 1, z), new Vector3(o.X + 1, o.Y + 1, z), tile, 0.88f, packedLight);
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
}
