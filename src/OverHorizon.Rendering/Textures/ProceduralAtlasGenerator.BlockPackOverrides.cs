using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Rendering.Textures;

public static partial class ProceduralAtlasGenerator
{
    private static void ApplyBlockPackOverrides(byte[] pixels, ResourcePackLoader pack)
    {
        _ = pack.TryBlitTile(pixels, TileStone, "stone");
        _ = pack.TryBlitTile(pixels, TileDirt, "dirt");
        _ = pack.TryBlitTile(pixels, TileGrassTop, "grass_block_top", 118, 190, 92);
        _ = pack.TryBlitTile(pixels, TileGrassSide, "grass_block_side");
        _ = pack.TryBlitTile(pixels, TileCobblestone, "cobblestone");
        _ = pack.TryBlitTile(pixels, TileOakLogSide, "oak_log");
        _ = pack.TryBlitTile(pixels, TileOakLogTop, "oak_log_top");
        _ = pack.TryBlitTile(pixels, TileOakLeaves, "oak_leaves", 96, 172, 72);
        _ = pack.TryBlitTile(pixels, TileSand, "sand");
        _ = pack.TryBlitTile(pixels, TileWater, "water_still", 63, 118, 228, alphaOverride: 205);
        _ = pack.TryBlitTile(pixels, TileGlowstone, "glowstone");
        _ = pack.TryBlitTile(pixels, TileBedrock, "bedrock");
        _ = pack.TryBlitTile(pixels, TileOakPlanks, "oak_planks");
        _ = pack.TryBlitTile(pixels, TileCraftingTop, "crafting_table_top");
        if (!pack.TryBlitTile(pixels, TileCraftingSide, "crafting_table_front")) _ = pack.TryBlitTile(pixels, TileCraftingSide, "crafting_table_side");
        _ = pack.TryBlitTile(pixels, TileStoneBricks, "stone_bricks");
        _ = pack.TryBlitTile(pixels, TileClayBricks, "bricks");
        _ = pack.TryBlitTile(pixels, TileGlass, "glass");
        _ = pack.TryBlitTile(pixels, TileSandstoneSide, "sandstone");
        _ = pack.TryBlitTile(pixels, TileSandstoneTop, "sandstone_top");
        _ = pack.TryBlitTile(pixels, TileSandstoneBottom, "sandstone_bottom");
        _ = pack.TryBlitTile(pixels, TileBookshelfSide, "bookshelf");
        _ = pack.TryBlitTile(pixels, TileCoalOre, "coal_ore");
        _ = pack.TryBlitTile(pixels, TileIronOre, "iron_ore");
        _ = pack.TryBlitTile(pixels, TileGoldOre, "gold_ore");
        _ = pack.TryBlitTile(pixels, TileDiamondOre, "diamond_ore");
        _ = pack.TryBlitTile(pixels, TileMossyCobblestone, "mossy_cobblestone");
        if (!pack.TryBlitTile(pixels, TileSnowBlock, "snow")) _ = pack.TryBlitTile(pixels, TileSnowBlock, "snow_block");
        if (!pack.TryBlitTile(pixels, TileCrimsonWool, "red_wool")) _ = pack.TryBlitTile(pixels, TileCrimsonWool, "crimson_wool");
        _ = pack.TryBlitTile(pixels, TileGravel, "gravel");
        _ = pack.TryBlitTile(pixels, TileObsidian, "obsidian");
        _ = pack.TryBlitTile(pixels, TileTntSide, "tnt_side");
        _ = pack.TryBlitTile(pixels, TileTntTop, "tnt_top");
        _ = pack.TryBlitTile(pixels, TileTntBottom, "tnt_bottom");
        _ = pack.TryBlitTile(pixels, TileWhiteWool, "white_wool");
        _ = pack.TryBlitTile(pixels, TileBlueWool, "blue_wool");

        _ = pack.TryBlitTile(pixels, TileTorch, "torch");
        _ = pack.TryBlitTile(pixels, TileLantern, "lantern");
        _ = pack.TryBlitTile(pixels, TileFurnaceFront, "furnace_front");
        _ = pack.TryBlitTile(pixels, TileFurnaceFrontLit, "furnace_front_on");
        _ = pack.TryBlitTile(pixels, TileFurnaceSide, "furnace_side");
        _ = pack.TryBlitTile(pixels, TileFurnaceTop, "furnace_top");
        _ = pack.TryBlitTile(pixels, TileBarrelSide, "barrel_side");
        _ = pack.TryBlitTile(pixels, TileBarrelTop, "barrel_top");
        _ = pack.TryBlitTile(pixels, TileBarrelBottom, "barrel_bottom");
        _ = pack.TryExtractChestTiles(pixels, TileChestSide, TileChestTop, TileChestBottom);
        _ = pack.TryBlitTile(pixels, TileLadder, "ladder");
        _ = pack.TryBlitTile(pixels, TileRail, "rail");
        _ = pack.TryBlitTile(pixels, TileRailCorner, "rail_corner");
        _ = pack.TryBlitTile(pixels, TilePoweredRailOff, "powered_rail");
        _ = pack.TryBlitTile(pixels, TilePoweredRailOn, "powered_rail_on");
        _ = pack.TryBlitTile(pixels, TileCobweb, "cobweb");
        _ = pack.TryBlitTile(pixels, TileFarmland, "farmland_moist");
        _ = pack.TryBlitTile(pixels, TileHaySide, "hay_block_side");
        _ = pack.TryBlitTile(pixels, TileHayTop, "hay_block_top");
        _ = pack.TryBlitTile(pixels, TileDeepslate, "deepslate");
        _ = pack.TryBlitTile(pixels, TileCobbledDeepslate, "cobbled_deepslate");
        _ = pack.TryBlitTile(pixels, TileCopperOre, "copper_ore");
        _ = pack.TryBlitTile(pixels, TileLapisOre, "lapis_ore");
        _ = pack.TryBlitTile(pixels, TileRedstoneOre, "redstone_ore");
        _ = pack.TryBlitTile(pixels, TileEmeraldOre, "emerald_ore");
        _ = pack.TryBlitTile(pixels, TileBirchLogSide, "birch_log");
        _ = pack.TryBlitTile(pixels, TileBirchLogTop, "birch_log_top");
        _ = pack.TryBlitTile(pixels, TileBirchLeaves, "birch_leaves", 128, 168, 86);
        _ = pack.TryBlitTile(pixels, TileBirchPlanks, "birch_planks");
        _ = pack.TryBlitTile(pixels, TileCoalBlock, "coal_block");
        _ = pack.TryBlitTile(pixels, TileIronBlock, "iron_block");
        _ = pack.TryBlitTile(pixels, TileGoldBlock, "gold_block");
        _ = pack.TryBlitTile(pixels, TileDiamondBlock, "diamond_block");

        _ = pack.TryBlitTile(pixels, TileRedstoneBlock, "redstone_block");
        _ = pack.TryBlitTile(pixels, TileRedstoneTorch, "redstone_torch");
        _ = pack.TryBlitTile(pixels, TileRedstoneTorchOff, "redstone_torch_off");
        _ = pack.TryBlitTile(pixels, TileRedstoneWireDot, "redstone_dust_dot", 224, 28, 28);
        _ = pack.TryBlitTile(pixels, TileRedstoneWireLine, "redstone_dust_line0", 224, 28, 28);
        _ = pack.TryBlitTile(pixels, TileLever, "lever");
        _ = pack.TryBlitTile(pixels, TileRepeaterOff, "repeater");
        _ = pack.TryBlitTile(pixels, TileRepeaterOn, "repeater_on");
        _ = pack.TryBlitTile(pixels, TileRedstoneLampOff, "redstone_lamp");
        _ = pack.TryBlitTile(pixels, TileRedstoneLampOn, "redstone_lamp_on");
        _ = pack.TryBlitTile(pixels, TilePistonBottom, "piston_bottom");
        _ = pack.TryBlitTile(pixels, TilePistonSide, "piston_side");
        _ = pack.TryBlitTile(pixels, TilePistonTop, "piston_top");
        _ = pack.TryBlitTile(pixels, TilePistonTopSticky, "piston_top_sticky");
        _ = pack.TryBlitTile(pixels, TilePistonInner, "piston_inner");
        _ = pack.TryBlitTile(pixels, TileFire, "fire_0");
        _ = pack.TryBlitTile(pixels, TileCopperBlock, "copper_block");
        _ = pack.TryBlitTile(pixels, TileEmeraldBlock, "emerald_block");
        _ = pack.TryBlitTile(pixels, TileLapisBlock, "lapis_block");
        _ = pack.TryBlitTile(pixels, TileComparatorOff, "comparator");
        _ = pack.TryBlitTile(pixels, TileComparatorOn, "comparator_on");
        _ = pack.TryBlitTile(pixels, TileComposterSide, "composter_side");
        _ = pack.TryBlitTile(pixels, TileComposterTop, "composter_top");
        _ = pack.TryBlitTile(pixels, TileComposterBottom, "composter_bottom");
        _ = pack.TryBlitTile(pixels, TileComposterCompost, "composter_compost");
        _ = pack.TryBlitTile(pixels, TileOakTrapdoor, "oak_trapdoor");
        _ = pack.TryBlitTile(pixels, TileMonsterSpawner, "spawner");
        _ = pack.TryBlitTile(pixels, TileAmethystBlock, "amethyst_block");
        _ = pack.TryBlitTile(pixels, TileMossyStoneBricks, "mossy_stone_bricks");
        _ = pack.TryBlitTile(pixels, TileChiseledSandstone, "chiseled_sandstone");
        _ = pack.TryBlitTile(pixels, TileOakDoorTop, "oak_door_top");
        _ = pack.TryBlitTile(pixels, TileOakDoorBottom, "oak_door_bottom");
        _ = pack.TryBlitTile(pixels, TileIronDoorTop, "iron_door_top");
        _ = pack.TryBlitTile(pixels, TileIronDoorBottom, "iron_door_bottom");
        _ = pack.TryBlitTile(pixels, TileIronTrapdoor, "iron_trapdoor");
        _ = pack.TryBlitTile(pixels, TileBuddingAmethyst, "budding_amethyst");
        _ = pack.TryBlitTile(pixels, TileAmethystCluster, "amethyst_cluster");
        _ = pack.TryBlitTile(pixels, TileDirtPathTop, "dirt_path_top");
        _ = pack.TryBlitTile(pixels, TileDirtPathSide, "dirt_path_side");
        _ = pack.TryBlitTile(pixels, TileCherryLogSide, "cherry_log");
        _ = pack.TryBlitTile(pixels, TileCherryLogTop, "cherry_log_top");
        _ = pack.TryBlitTile(pixels, TileCherryLeaves, "cherry_leaves");
        _ = pack.TryBlitTile(pixels, TileSpruceLogSide, "spruce_log");
        _ = pack.TryBlitTile(pixels, TileSpruceLogTop, "spruce_log_top");
        _ = pack.TryBlitTile(pixels, TileSpruceLeaves, "spruce_leaves", 97, 153, 97);
        _ = pack.TryBlitTile(pixels, TileRedSand, "red_sand");
        _ = pack.TryBlitTile(pixels, TileTerracotta, "terracotta");
        _ = pack.TryBlitTile(pixels, TilePackedIce, "packed_ice");

        for (var stage = 0; stage < 10; stage++)
        {
            _ = pack.TryBlitTile(pixels, (ushort)(FirstDestroyStageTile + stage), $"destroy_stage_{stage}");
        }
    }

    private static void MapAllFaces(Dictionary<(ResourceKey, Direction), ushort> map, ResourceKey key, ushort tile)
    {
        foreach (var dir in DirectionExtensions.All)
        {
            map[(key, dir)] = tile;
        }
    }

    private static void MapBlockTopBottomSide(
        Dictionary<(ResourceKey, Direction), ushort> map,
        ResourceKey key,
        ushort topTile,
        ushort bottomTile,
        ushort sideTile)
    {
        map[(key, Direction.Up)] = topTile;
        map[(key, Direction.Down)] = bottomTile;
        map[(key, Direction.North)] = sideTile;
        map[(key, Direction.South)] = sideTile;
        map[(key, Direction.West)] = sideTile;
        map[(key, Direction.East)] = sideTile;
    }

    private static void PaintFenceInventoryIconFromPlanks(byte[] atlas, ushort dstTile, ushort srcPlanksTile)
    {
        var sx0 = (srcPlanksTile % TilesPerRow) * 16;
        var sy0 = (srcPlanksTile / TilesPerRow) * 16;
        var dx0 = (dstTile % TilesPerRow) * 16;
        var dy0 = (dstTile / TilesPerRow) * 16;

        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var di = (((dy0 + y) * AtlasDimension) + dx0 + x) * 4;
                atlas[di + 0] = 0;
                atlas[di + 1] = 0;
                atlas[di + 2] = 0;
                atlas[di + 3] = 0;
            }
        }

        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var leftPost = x is >= 2 and <= 5 && y is >= 2 and <= 14;
                var rightPost = x is >= 10 and <= 13 && y is >= 2 and <= 14;
                var topRail = x is >= 1 and <= 14 && y is >= 4 and <= 6;
                var bottomRail = x is >= 1 and <= 14 && y is >= 9 and <= 11;
                if (!leftPost && !rightPost && !topRail && !bottomRail)
                {
                    continue;
                }

                var shade = 235;
                if ((leftPost && y == 2) || (rightPost && y == 2) || (topRail && y == 4) || (bottomRail && y == 9))
                {
                    shade = 255;
                }
                else if ((leftPost && x >= 4) || (rightPost && x >= 12))
                {
                    shade = 178;
                }
                else if (!leftPost && !rightPost)
                {
                    shade = 205;
                }

                var si = (((sy0 + y) * AtlasDimension) + sx0 + x) * 4;
                var di = (((dy0 + y) * AtlasDimension) + dx0 + x) * 4;
                atlas[di + 0] = (byte)Math.Clamp((atlas[si + 0] * shade) / 255, 0, 255);
                atlas[di + 1] = (byte)Math.Clamp((atlas[si + 1] * shade) / 255, 0, 255);
                atlas[di + 2] = (byte)Math.Clamp((atlas[si + 2] * shade) / 255, 0, 255);
                atlas[di + 3] = 255;
            }
        }
    }
}
