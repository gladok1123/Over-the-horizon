using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Procedural fallback painters, face lookup tables, and <c>23w13a__or__b</c> PNG overrides for <see cref="ProceduralAtlasGenerator"/>.
/// </summary>
public static partial class ProceduralAtlasGenerator
{
    private static void PaintFallbackBlocksAndItems(byte[] pixels)
    {
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileStone, 124, 124, 128, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileDirt, 134, 96, 67, variation: 20, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileGrassTop, 91, 158, 58, variation: 22, alpha: 255);
        ProceduralBlockTilePainter.PaintGrassSideTile(pixels, TileGrassSide);
        ProceduralBlockTilePainter.PaintCobblestoneTile(pixels, TileCobblestone, mossy: false);
        ProceduralBlockTilePainter.PaintLogSideTile(pixels, TileOakLogSide);
        ProceduralBlockTilePainter.PaintLogTopTile(pixels, TileOakLogTop);
        ProceduralBlockTilePainter.PaintLeavesTile(pixels, TileOakLeaves);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileSand, 219, 207, 158, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileWater, 42, 108, 212, variation: 16, alpha: 185);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileGlowstone, 242, 206, 102, variation: 32, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBedrock, 52, 52, 56, variation: 34, alpha: 255);
        ProceduralBlockTilePainter.PaintPlanksTile(pixels, TileOakPlanks);
        ProceduralBlockTilePainter.PaintCraftingTopTile(pixels, TileCraftingTop);
        ProceduralBlockTilePainter.PaintPlanksTile(pixels, TileCraftingSide);
        ProceduralBlockTilePainter.PaintStoneBricksTile(pixels, TileStoneBricks);
        ProceduralBlockTilePainter.PaintClayBricksTile(pixels, TileClayBricks);
        ProceduralBlockTilePainter.PaintGlassTile(pixels, TileGlass);
        ProceduralBlockTilePainter.PaintSandstoneSideTile(pixels, TileSandstoneSide);
        ProceduralBlockTilePainter.PaintBookshelfSideTile(pixels, TileBookshelfSide);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileCoalOre, 34, 34, 38, 0xC0A1_0001UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileIronOre, 216, 178, 148, 0x1B04_0002UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileGoldOre, 248, 214, 62, 0x601D_0003UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileDiamondOre, 78, 236, 228, 0xD1A4_0004UL);
        ProceduralBlockTilePainter.PaintCobblestoneTile(pixels, TileMossyCobblestone, mossy: true);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileSnowBlock, 240, 246, 250, variation: 8, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileCrimsonWool, 178, 42, 52, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileGravel, 132, 126, 124, variation: 28, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileObsidian, 26, 18, 38, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileTntSide, 206, 48, 38, variation: 20, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileTntTop, 188, 52, 42, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileWhiteWool, 236, 238, 240, variation: 10, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBlueWool, 48, 72, 182, variation: 14, alpha: 255);

        ProceduralItemTilePainter.PaintTorchTile(pixels, TileTorch);
        ProceduralItemTilePainter.PaintLanternTile(pixels, TileLantern);
        ProceduralItemTilePainter.PaintFurnaceFrontTile(pixels, TileFurnaceFront, lit: false);
        ProceduralItemTilePainter.PaintFurnaceFrontTile(pixels, TileFurnaceFrontLit, lit: true);
        ProceduralBlockTilePainter.PaintStoneBricksTile(pixels, TileFurnaceSide);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileFurnaceTop, 112, 112, 116, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileChestSide, 166, 118, 52, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileChestTop, 152, 108, 46, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBarrelSide, 124, 90, 54, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBarrelTop, 138, 102, 62, variation: 14, alpha: 255);
        ProceduralItemTilePainter.PaintLadderTile(pixels, TileLadder);
        ProceduralItemTilePainter.PaintRailTile(pixels, TileRail);
        ProceduralItemTilePainter.PaintCobwebTile(pixels, TileCobweb);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileFarmland, 96, 62, 36, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileHaySide, 212, 180, 58, variation: 18, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileHayTop, 198, 168, 50, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileDeepslate, 74, 74, 82, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileCobbledDeepslate, 66, 66, 74, variation: 22, alpha: 255);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileCopperOre, 216, 122, 76, 0xC099_0005UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileLapisOre, 44, 88, 214, 0x1A91_0006UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileRedstoneOre, 218, 36, 36, 0xBED5_0007UL);
        ProceduralBlockTilePainter.PaintOreTile(pixels, TileEmeraldOre, 42, 218, 96, 0xE4E2_0008UL);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBirchLogSide, 224, 220, 212, variation: 26, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBirchLogTop, 198, 184, 138, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintLeavesTile(pixels, TileBirchLeaves);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileBirchPlanks, 206, 190, 134, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileCoalBlock, 26, 26, 30, variation: 10, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileIronBlock, 222, 224, 228, variation: 10, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileGoldBlock, 248, 212, 54, variation: 12, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileDiamondBlock, 88, 238, 230, variation: 12, alpha: 255);

        // v0.6.0-alpha Redstone, Pistons, Fire, Bed, Minerals & Mining Cracks
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileRedstoneBlock, 198, 24, 24, variation: 18, alpha: 255);
        ProceduralItemTilePainter.PaintTorchTile(pixels, TileRedstoneTorch);
        ProceduralItemTilePainter.PaintTorchTile(pixels, TileRedstoneTorchOff);
        ProceduralItemTilePainter.PaintRedstoneDustCross(pixels, TileRedstoneWireDot, lineOnly: false);
        ProceduralItemTilePainter.PaintRedstoneDustCross(pixels, TileRedstoneWireLine, lineOnly: true);
        ProceduralItemTilePainter.PaintStick(pixels, TileLever);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileRepeaterOff, 158, 152, 152, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileRepeaterOn, 196, 136, 136, variation: 14, alpha: 255);
        ProceduralItemTilePainter.PaintRedstoneLampTile(pixels, TileRedstoneLampOff, lit: false);
        ProceduralItemTilePainter.PaintRedstoneLampTile(pixels, TileRedstoneLampOn, lit: true);
        ProceduralBlockTilePainter.PaintCobblestoneTile(pixels, TilePistonBottom, mossy: false);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TilePistonSide, 146, 128, 98, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintPlanksTile(pixels, TilePistonTop);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TilePistonTopSticky, 118, 174, 88, variation: 20, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TilePistonInner, 92, 92, 96, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileFire, 250, 142, 28, variation: 36, alpha: 230);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileCopperBlock, 212, 118, 76, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileEmeraldBlock, 44, 214, 96, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileLapisBlock, 38, 78, 198, variation: 16, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileTntBottom, 168, 52, 42, variation: 14, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileSandstoneTop, 224, 214, 168, variation: 10, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileSandstoneBottom, 212, 200, 152, variation: 12, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileRedBedTopHead, 240, 240, 246, variation: 6, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileRedBedTopFoot, 196, 36, 40, variation: 8, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileRedBedSide, 186, 42, 44, variation: 10, alpha: 255);
        ProceduralBlockTilePainter.PaintNoiseTile(pixels, TileRedBedHeadSide, 232, 232, 238, variation: 8, alpha: 255);
        ProceduralItemTilePainter.PaintRailCornerTile(pixels, TileRailCorner);
        ProceduralItemTilePainter.PaintPoweredRailTile(pixels, TilePoweredRailOff, energized: false);
        ProceduralItemTilePainter.PaintPoweredRailTile(pixels, TilePoweredRailOn, energized: true);

        for (var stage = 0; stage < 10; stage++)
        {
            ProceduralItemTilePainter.PaintDestroyCrackStage(pixels, (ushort)(FirstDestroyStageTile + stage), stage);
        }

        PaintFallbackItems(pixels);
    }

    private static void PaintFallbackItems(byte[] pixels)
    {
        ProceduralItemTilePainter.PaintStick(pixels, 128);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 129, 44, 44, 48);
        ProceduralItemTilePainter.PaintIngot(pixels, 130, 214, 218, 224);
        ProceduralItemTilePainter.PaintIngot(pixels, 131, 248, 212, 58);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 132, 74, 236, 228);
        ProceduralItemTilePainter.PaintSword(pixels, 133, 168, 126, 74);
        ProceduralItemTilePainter.PaintSword(pixels, 134, 148, 148, 154);
        ProceduralItemTilePainter.PaintSword(pixels, 135, 248, 214, 62);
        ProceduralItemTilePainter.PaintSword(pixels, 136, 218, 222, 228);
        ProceduralItemTilePainter.PaintSword(pixels, 137, 74, 238, 228);
        ProceduralItemTilePainter.PaintSword(pixels, 138, 76, 68, 78);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 139, 168, 126, 74);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 140, 148, 148, 154);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 141, 218, 222, 228);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 142, 74, 238, 228);
        ProceduralItemTilePainter.PaintAxe(pixels, 143, 218, 222, 228);
        ProceduralItemTilePainter.PaintAxe(pixels, 144, 74, 238, 228);
        ProceduralItemTilePainter.PaintShovel(pixels, 145, 218, 222, 228);
        ProceduralItemTilePainter.PaintShovel(pixels, 146, 74, 238, 228);
        ProceduralItemTilePainter.PaintSword(pixels, 147, 198, 204, 210);
        ProceduralItemTilePainter.PaintIngot(pixels, 148, 128, 132, 138);
        ProceduralItemTilePainter.PaintBucket(pixels, 149, filledWithWater: false);
        ProceduralItemTilePainter.PaintBucket(pixels, 150, filledWithWater: true);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 151, 218, 44, 42);
        ProceduralItemTilePainter.PaintIngot(pixels, 152, 204, 152, 76);
        ProceduralItemTilePainter.PaintIngot(pixels, 153, 136, 68, 44);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 154, 250, 214, 54);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 155, 58, 52, 48);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 156, 208, 172, 142);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 157, 244, 202, 62);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 158, 214, 118, 76);
        ProceduralItemTilePainter.PaintIngot(pixels, 159, 216, 124, 78);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 160, 42, 222, 98);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 161, 46, 92, 220);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 162, 218, 32, 32);
        ProceduralItemTilePainter.PaintIngot(pixels, 163, 72, 66, 74);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 164, 78, 78, 86);
        ProceduralItemTilePainter.PaintStick(pixels, 165);
        ProceduralItemTilePainter.PaintStick(pixels, 166);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 167, 112, 112, 116);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 168, 156, 36, 54);
        ProceduralItemTilePainter.PaintIngot(pixels, 169, 168, 102, 56);
        ProceduralItemTilePainter.PaintStick(pixels, 170);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 171, 248, 214, 62);
        ProceduralItemTilePainter.PaintPickaxe(pixels, 172, 76, 68, 78);
        ProceduralItemTilePainter.PaintAxe(pixels, 173, 168, 126, 74);
        ProceduralItemTilePainter.PaintAxe(pixels, 174, 148, 148, 154);
        ProceduralItemTilePainter.PaintShovel(pixels, 175, 168, 126, 74);
        ProceduralItemTilePainter.PaintShovel(pixels, 176, 148, 148, 154);
        ProceduralItemTilePainter.PaintHoe(pixels, 177, 168, 126, 74);
        ProceduralItemTilePainter.PaintHoe(pixels, 178, 148, 148, 154);
        ProceduralItemTilePainter.PaintHoe(pixels, 179, 218, 222, 228);
        ProceduralItemTilePainter.PaintHoe(pixels, 180, 74, 238, 228);
        ProceduralItemTilePainter.PaintArmor(pixels, 181, 218, 222, 228, 0);
        ProceduralItemTilePainter.PaintArmor(pixels, 182, 218, 222, 228, 1);
        ProceduralItemTilePainter.PaintArmor(pixels, 183, 218, 228, 228, 2);
        ProceduralItemTilePainter.PaintArmor(pixels, 184, 218, 222, 228, 3);
        ProceduralItemTilePainter.PaintArmor(pixels, 185, 74, 238, 228, 0);
        ProceduralItemTilePainter.PaintArmor(pixels, 186, 74, 238, 228, 1);
        ProceduralItemTilePainter.PaintArmor(pixels, 187, 74, 238, 228, 2);
        ProceduralItemTilePainter.PaintArmor(pixels, 188, 74, 238, 228, 3);
        ProceduralItemTilePainter.PaintIngot(pixels, 189, 232, 148, 142);
        ProceduralItemTilePainter.PaintIngot(pixels, 190, 214, 168, 112);
        ProceduralItemTilePainter.PaintIngot(pixels, 191, 198, 68, 62);
        ProceduralItemTilePainter.PaintIngot(pixels, 192, 236, 186, 172);
        ProceduralItemTilePainter.PaintIngot(pixels, 193, 206, 138, 74);
        ProceduralItemTilePainter.PaintIngot(pixels, 194, 208, 86, 82);
        ProceduralItemTilePainter.PaintIngot(pixels, 195, 148, 88, 58);
        ProceduralItemTilePainter.PaintIngot(pixels, 196, 146, 78, 54);
        ProceduralItemTilePainter.PaintLanternTile(pixels, 197);

        ProceduralItemTilePainter.PaintLanternItemIcon(pixels, 197);
        ProceduralItemTilePainter.PaintIngot(pixels, 198, 242, 242, 236);
        ProceduralItemTilePainter.PaintIngot(pixels, 199, 148, 92, 52);
        ProceduralItemTilePainter.PaintDiscOrGem(pixels, 200, 116, 214, 96);
        ProceduralItemTilePainter.PaintStick(pixels, 201);
        ProceduralItemTilePainter.PaintStick(pixels, 202);
        ProceduralItemTilePainter.PaintIngot(pixels, 203, 188, 164, 164);
        ProceduralItemTilePainter.PaintAxe(pixels, 204, 248, 214, 62);
        ProceduralItemTilePainter.PaintShovel(pixels, 205, 248, 214, 62);
        ProceduralItemTilePainter.PaintHoe(pixels, 206, 248, 214, 62);
        ProceduralItemTilePainter.PaintArmor(pixels, 207, 160, 101, 64, 0);
        ProceduralItemTilePainter.PaintArmor(pixels, 208, 160, 101, 64, 1);
        ProceduralItemTilePainter.PaintArmor(pixels, 209, 160, 101, 64, 2);
        ProceduralItemTilePainter.PaintArmor(pixels, 210, 160, 101, 64, 3);
        ProceduralItemTilePainter.PaintArmor(pixels, 211, 248, 214, 62, 0);
        ProceduralItemTilePainter.PaintArmor(pixels, 212, 248, 214, 62, 1);
        ProceduralItemTilePainter.PaintArmor(pixels, 213, 248, 214, 62, 2);
        ProceduralItemTilePainter.PaintArmor(pixels, 214, 248, 214, 62, 3);
        ProceduralItemTilePainter.PaintShield(pixels, 215);
        ProceduralItemTilePainter.PaintBedItem(pixels, 216);
        ProceduralItemTilePainter.PaintBowAndArrowTiles(pixels, bowBaseTile: 218, arrowTile: 219, minecartTile: 217);
        ProceduralItemTilePainter.PaintPoweredRailTile(pixels, 223, energized: false);
    }

    private static Dictionary<(ResourceKey, Direction), ushort> BuildFaceLookup()
    {
        var lookup = new Dictionary<(ResourceKey, Direction), ushort>();
        MapAllFaces(lookup, BuiltInBlocks.Stone, TileStone);
        MapAllFaces(lookup, BuiltInBlocks.Dirt, TileDirt);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.GrassBlock, TileGrassTop, TileDirt, TileGrassSide);
        MapAllFaces(lookup, BuiltInBlocks.Cobblestone, TileCobblestone);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.OakLog, TileOakLogTop, TileOakLogTop, TileOakLogSide);
        MapAllFaces(lookup, BuiltInBlocks.OakLeaves, TileOakLeaves);
        MapAllFaces(lookup, BuiltInBlocks.Sand, TileSand);
        MapAllFaces(lookup, BuiltInBlocks.Water, TileWater);
        MapAllFaces(lookup, BuiltInBlocks.Glowstone, TileGlowstone);
        MapAllFaces(lookup, BuiltInBlocks.Bedrock, TileBedrock);
        MapAllFaces(lookup, BuiltInBlocks.OakPlanks, TileOakPlanks);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.CraftingTable, TileCraftingTop, TileOakPlanks, TileCraftingSide);
        MapAllFaces(lookup, BuiltInBlocks.StoneBricks, TileStoneBricks);
        MapAllFaces(lookup, BuiltInBlocks.Bricks, TileClayBricks);
        MapAllFaces(lookup, BuiltInBlocks.Glass, TileGlass);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Sandstone, TileSandstoneTop, TileSandstoneBottom, TileSandstoneSide);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Bookshelf, TileOakPlanks, TileOakPlanks, TileBookshelfSide);
        MapAllFaces(lookup, BuiltInBlocks.CoalOre, TileCoalOre);
        MapAllFaces(lookup, BuiltInBlocks.IronOre, TileIronOre);
        MapAllFaces(lookup, BuiltInBlocks.GoldOre, TileGoldOre);
        MapAllFaces(lookup, BuiltInBlocks.DiamondOre, TileDiamondOre);
        MapAllFaces(lookup, BuiltInBlocks.MossyCobblestone, TileMossyCobblestone);
        MapAllFaces(lookup, BuiltInBlocks.SnowBlock, TileSnowBlock);
        MapAllFaces(lookup, BuiltInBlocks.CrimsonWool, TileCrimsonWool);
        MapAllFaces(lookup, BuiltInBlocks.Gravel, TileGravel);
        MapAllFaces(lookup, BuiltInBlocks.Obsidian, TileObsidian);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Tnt, TileTntTop, TileTntBottom, TileTntSide);
        MapAllFaces(lookup, BuiltInBlocks.WhiteWool, TileWhiteWool);
        MapAllFaces(lookup, BuiltInBlocks.BlueWool, TileBlueWool);

        MapAllFaces(lookup, BuiltInBlocks.Torch, TileTorch);
        MapAllFaces(lookup, BuiltInBlocks.Lantern, TileLantern);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Furnace, TileFurnaceTop, TileFurnaceTop, TileFurnaceSide);
        lookup[(BuiltInBlocks.Furnace, Direction.North)] = TileFurnaceFront;
        MapBlockTopBottomSide(lookup, BuiltInBlocks.LitFurnace, TileFurnaceTop, TileFurnaceTop, TileFurnaceSide);
        lookup[(BuiltInBlocks.LitFurnace, Direction.North)] = TileFurnaceFrontLit;
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Chest, TileChestTop, TileChestTop, TileChestSide);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Barrel, TileBarrelTop, TileBarrelTop, TileBarrelSide);
        MapAllFaces(lookup, BuiltInBlocks.Ladder, TileLadder);
        MapAllFaces(lookup, BuiltInBlocks.Rail, TileRail);
        MapAllFaces(lookup, BuiltInBlocks.PoweredRail, TilePoweredRailOff);
        MapAllFaces(lookup, BuiltInBlocks.Cobweb, TileCobweb);
        MapAllFaces(lookup, BuiltInBlocks.OakFence, TileOakPlanks);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Farmland, TileFarmland, TileDirt, TileDirt);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.HayBlock, TileHayTop, TileHayTop, TileHaySide);
        MapAllFaces(lookup, BuiltInBlocks.Deepslate, TileDeepslate);
        MapAllFaces(lookup, BuiltInBlocks.CobbledDeepslate, TileCobbledDeepslate);
        MapAllFaces(lookup, BuiltInBlocks.CopperOre, TileCopperOre);
        MapAllFaces(lookup, BuiltInBlocks.LapisOre, TileLapisOre);
        MapAllFaces(lookup, BuiltInBlocks.RedstoneOre, TileRedstoneOre);
        MapAllFaces(lookup, BuiltInBlocks.EmeraldOre, TileEmeraldOre);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.BirchLog, TileBirchLogTop, TileBirchLogTop, TileBirchLogSide);
        MapAllFaces(lookup, BuiltInBlocks.BirchLeaves, TileBirchLeaves);
        MapAllFaces(lookup, BuiltInBlocks.BirchPlanks, TileBirchPlanks);
        MapAllFaces(lookup, BuiltInBlocks.CoalBlock, TileCoalBlock);
        MapAllFaces(lookup, BuiltInBlocks.IronBlock, TileIronBlock);
        MapAllFaces(lookup, BuiltInBlocks.GoldBlock, TileGoldBlock);
        MapAllFaces(lookup, BuiltInBlocks.DiamondBlock, TileDiamondBlock);

        MapAllFaces(lookup, BuiltInBlocks.RedstoneBlock, TileRedstoneBlock);
        MapAllFaces(lookup, BuiltInBlocks.RedstoneTorch, TileRedstoneTorch);
        MapAllFaces(lookup, BuiltInBlocks.RedstoneTorchOff, TileRedstoneTorchOff);
        MapAllFaces(lookup, BuiltInBlocks.RedstoneWire, TileRedstoneWireDot);
        MapAllFaces(lookup, BuiltInBlocks.Lever, TileLever);
        MapAllFaces(lookup, BuiltInBlocks.StoneButton, TileStone);
        MapAllFaces(lookup, BuiltInBlocks.StonePressurePlate, TileStone);
        MapAllFaces(lookup, BuiltInBlocks.RedstoneRepeater, TileRepeaterOff);
        MapAllFaces(lookup, BuiltInBlocks.RedstoneLamp, TileRedstoneLampOff);
        MapAllFaces(lookup, BuiltInBlocks.LitRedstoneLamp, TileRedstoneLampOn);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.Piston, TilePistonTop, TilePistonBottom, TilePistonSide);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.StickyPiston, TilePistonTopSticky, TilePistonBottom, TilePistonSide);
        MapAllFaces(lookup, BuiltInBlocks.PistonHead, TilePistonTop);
        MapAllFaces(lookup, BuiltInBlocks.Fire, TileFire);
        MapAllFaces(lookup, BuiltInBlocks.CopperBlock, TileCopperBlock);
        MapAllFaces(lookup, BuiltInBlocks.EmeraldBlock, TileEmeraldBlock);
        MapAllFaces(lookup, BuiltInBlocks.LapisBlock, TileLapisBlock);
        MapBlockTopBottomSide(lookup, BuiltInBlocks.RedBed, TileRedBedTopFoot, TileOakPlanks, TileRedBedSide);
        return lookup;
    }

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
}
