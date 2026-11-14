using System.Collections.Frozen;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Shared.Hashing;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Generates the <c>512x512</c> RGBA8 block, mining-crack (<c>destroy_stage_0..9</c>), and item texture atlas,
/// automatically overlaying real <c>16x16</c> PNGs from <c>minecraft-assets</c> (<c>23w13a__or__b</c>).
/// </summary>
public static partial class ProceduralAtlasGenerator
{
    /// <summary>Width and height of the square RGBA8 block &amp; item atlas in pixels (<c>512</c> = <c>1024</c> tiles).</summary>
    public const int AtlasDimension = 512;

    /// <summary>Width and height of a single block/item tile in pixels (<c>16</c>).</summary>
    public const int TileDimension = 16;

    /// <summary>Number of <c>16x16</c> tiles per row (<c>32</c>).</summary>
    public const int TilesPerRow = AtlasDimension / TileDimension;

    /// <summary>Log2 of <see cref="TilesPerRow"/> (<c>5</c>); used to derive the tile row from a tile index.</summary>
    public const int TilesPerRowShift = 5;

    /// <summary>Total number of addressable tiles in the atlas (<c>1024</c>).</summary>
    public const int TileCount = TilesPerRow * TilesPerRow;

    /// <summary>First tile index of the 10-stage mining crack sprites (<c>destroy_stage_0..9</c> at <c>768..777</c>).</summary>
    public const ushort FirstDestroyStageTile = 768;

    internal const ushort TileStone = 0;
    internal const ushort TileDirt = 1;
    internal const ushort TileGrassTop = 2;
    internal const ushort TileGrassSide = 3;
    internal const ushort TileCobblestone = 4;
    internal const ushort TileOakLogSide = 5;
    internal const ushort TileOakLogTop = 6;
    internal const ushort TileOakLeaves = 7;
    internal const ushort TileSand = 8;
    internal const ushort TileWater = 9;
    internal const ushort TileGlowstone = 10;
    internal const ushort TileBedrock = 11;
    internal const ushort TileOakPlanks = 12;
    internal const ushort TileCraftingTop = 13;
    internal const ushort TileCraftingSide = 14;
    internal const ushort TileStoneBricks = 15;
    internal const ushort TileClayBricks = 16;
    internal const ushort TileGlass = 17;
    internal const ushort TileSandstoneSide = 18;
    internal const ushort TileBookshelfSide = 19;
    internal const ushort TileCoalOre = 20;
    internal const ushort TileIronOre = 21;
    internal const ushort TileGoldOre = 22;
    internal const ushort TileDiamondOre = 23;
    internal const ushort TileMossyCobblestone = 24;
    internal const ushort TileSnowBlock = 25;
    internal const ushort TileCrimsonWool = 26;
    internal const ushort TileGravel = 27;
    internal const ushort TileObsidian = 28;
    internal const ushort TileTntSide = 29;
    internal const ushort TileTntTop = 30;
    internal const ushort TileWhiteWool = 31;
    internal const ushort TileBlueWool = 32;

    internal const ushort TileTorch = 33;
    internal const ushort TileLantern = 34;
    internal const ushort TileFurnaceFront = 35;
    internal const ushort TileFurnaceFrontLit = 36;
    internal const ushort TileFurnaceSide = 37;
    internal const ushort TileFurnaceTop = 38;
    internal const ushort TileChestSide = 39;
    internal const ushort TileChestTop = 40;
    internal const ushort TileBarrelSide = 41;
    internal const ushort TileBarrelTop = 42;
    internal const ushort TileChestBottom = 253;
    internal const ushort TileBarrelBottom = 254;

    // v0.10.0-alpha Wheat crop growth stages (90..97)
    internal const ushort TileWheatStage0 = 90;
    internal const ushort TileWheatStage1 = 91;
    internal const ushort TileWheatStage2 = 92;
    internal const ushort TileWheatStage3 = 93;
    internal const ushort TileWheatStage4 = 94;
    internal const ushort TileWheatStage5 = 95;
    internal const ushort TileWheatStage6 = 96;
    internal const ushort TileWheatStage7 = 97;
    internal const ushort TileLadder = 43;
    internal const ushort TileRail = 44;
    internal const ushort TileCobweb = 45;
    internal const ushort TileFarmland = 46;
    internal const ushort TileHaySide = 47;
    internal const ushort TileHayTop = 48;
    internal const ushort TileDeepslate = 49;
    internal const ushort TileCobbledDeepslate = 50;
    internal const ushort TileCopperOre = 51;
    internal const ushort TileLapisOre = 52;
    internal const ushort TileRedstoneOre = 53;
    internal const ushort TileEmeraldOre = 54;
    internal const ushort TileBirchLogSide = 55;
    internal const ushort TileBirchLogTop = 56;
    internal const ushort TileBirchLeaves = 57;
    internal const ushort TileBirchPlanks = 58;
    internal const ushort TileCoalBlock = 59;
    internal const ushort TileIronBlock = 60;
    internal const ushort TileGoldBlock = 61;
    internal const ushort TileDiamondBlock = 62;

    // v0.6.0-alpha Redstone, Pistons, Fire, Bed & Mineral Block Tiles (63..89)
    internal const ushort TileRedstoneBlock = 63;
    internal const ushort TileRedstoneTorch = 64;
    internal const ushort TileRedstoneTorchOff = 65;
    internal const ushort TileRedstoneWireDot = 66;
    internal const ushort TileRedstoneWireLine = 67;
    internal const ushort TileLever = 68;
    internal const ushort TileRepeaterOff = 69;
    internal const ushort TileRepeaterOn = 70;
    internal const ushort TileRedstoneLampOff = 71;
    internal const ushort TileRedstoneLampOn = 72;
    internal const ushort TilePistonBottom = 73;
    internal const ushort TilePistonSide = 74;
    internal const ushort TilePistonTop = 75;
    internal const ushort TilePistonTopSticky = 76;
    internal const ushort TilePistonInner = 77;
    internal const ushort TileFire = 78;
    internal const ushort TileCopperBlock = 79;
    internal const ushort TileEmeraldBlock = 80;
    internal const ushort TileLapisBlock = 81;
    internal const ushort TileTntBottom = 82;
    internal const ushort TileSandstoneTop = 83;
    internal const ushort TileSandstoneBottom = 84;
    internal const ushort TileRedBedTopHead = 85;
    internal const ushort TileRedBedTopFoot = 86;
    internal const ushort TileRedBedSide = 87;
    internal const ushort TileRailCorner = 88;
    internal const ushort TilePoweredRailOff = 89;
    internal const ushort TilePoweredRailOn = 100;
    internal const ushort TileRedBedHeadSide = 101;
    internal const ushort TileComparatorOff = 102;
    internal const ushort TileComparatorOn = 103;
    internal const ushort TileComposterSide = 104;
    internal const ushort TileComposterTop = 105;
    internal const ushort TileComposterBottom = 106;
    internal const ushort TileComposterCompost = 107;
    internal const ushort TileOakTrapdoor = 108;
    internal const ushort TileMonsterSpawner = 109;
    internal const ushort TileAmethystBlock = 110;
    internal const ushort TileMossyStoneBricks = 111;
    internal const ushort TileChiseledSandstone = 112;
    internal const ushort TileOakDoorTop = 113;
    internal const ushort TileOakDoorBottom = 114;
    internal const ushort TileIronDoorTop = 115;
    internal const ushort TileIronDoorBottom = 116;
    internal const ushort TileIronTrapdoor = 117;
    internal const ushort TileBuddingAmethyst = 118;
    internal const ushort TileAmethystCluster = 119;
    internal const ushort TileDirtPathTop = 120;
    internal const ushort TileDirtPathSide = 121;
    internal const ushort TileCherryLogSide = 122;
    internal const ushort TileCherryLeaves = 123;
    internal const ushort TileSpruceLogSide = 124;
    internal const ushort TileSpruceLeaves = 125;
    internal const ushort TileRedSand = 126;
    internal const ushort TileCherryLogTop = 249;
    internal const ushort TileSpruceLogTop = 250;
    internal const ushort TileTerracotta = 251;
    internal const ushort TilePackedIce = 252;

    // v0.11.0-alpha Nether dimension tiles (256..277)
    internal const ushort TileNetherrack = 256;
    internal const ushort TileSoulSand = 257;
    internal const ushort TileSoulSoil = 258;
    internal const ushort TileBasaltSide = 259;
    internal const ushort TileBasaltTop = 260;
    internal const ushort TileMagma = 261;
    internal const ushort TileNetherQuartzOre = 262;
    internal const ushort TileNetherGoldOre = 263;
    internal const ushort TileAncientDebrisSide = 264;
    internal const ushort TileAncientDebrisTop = 265;
    internal const ushort TileNetherBricks = 266;
    internal const ushort TileBlackstone = 267;
    internal const ushort TileNetherPortal = 268;
    internal const ushort TileLava = 269;
    internal const ushort TileCrimsonNyliumTop = 270;
    internal const ushort TileCrimsonNyliumSide = 271;
    internal const ushort TileWarpedNyliumTop = 272;
    internal const ushort TileWarpedNyliumSide = 273;
    internal const ushort TileNetherWartBlock = 274;
    internal const ushort TileWarpedWartBlock = 275;
    internal const ushort TileShroomlight = 276;
    internal const ushort TileNetheriteBlock = 277;

    private const ushort FirstCustomTile = 278;
    private const ushort FirstItemSpriteTile = 384;

    /// <summary>Returns the atlas tile for <c>Wheat</c> growth stage <paramref name="age"/> (<c>0..7</c>).</summary>
    /// <param name="age">Crop growth stage from the <c>age</c> block-state property.</param>
    /// <returns>Atlas tile index of the matching <c>wheat_stage{age}</c> texture.</returns>
    public static ushort GetWheatStageTile(int age) =>
        (ushort)(TileWheatStage0 + Math.Clamp(age, 0, 7));

    /// <summary>Creates the default <c>256x256</c> block &amp; item texture atlas.</summary>
    public static TextureAtlasData CreateDefault(IBlockRegistry blocks, string? externalAssetsPath = null) =>
        Generate(blocks, externalAssetsPath);

    /// <summary>Generates the <c>512x512</c> block &amp; item texture atlas.</summary>
    public static TextureAtlasData Generate(IBlockRegistry blocks, string? externalAssetsPath = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var pixels = new byte[AtlasDimension * AtlasDimension * 4];
        PaintFallbackBlocksAndItems(pixels);
        var itemTiles = ProceduralItemTileManifest.BuiltInItemTiles;

        using (var resourcePack = ResourcePackLoader.Discover(externalAssetsPath))
        {
            if (resourcePack.LoadedBlockTextureCount > 0)
            {
                ApplyBlockPackOverrides(pixels, resourcePack);
            }

            if (resourcePack.LoadedItemTextureCount > 0 || resourcePack.LoadedBlockTextureCount > 0)
            {
                for (var i = 0; i < itemTiles.Length; i++)
                {
                    var (_, tile, stem, tr, tg, tb) = itemTiles[i];
                    _ = resourcePack.TryBlitItemTile(pixels, tile, stem, tr, tg, tb);
                }

                // Composite authentic two-layer 23w13a__or__b potions (glass bottle + tinted liquid) & spotted spawn eggs!
                var potions = ProceduralItemTileManifest.PotionOverlays;
                for (var i = 0; i < potions.Length; i++)
                {
                    var (tile, lr, lg, lb) = potions[i];
                    _ = resourcePack.TryBlitPotionTile(pixels, tile, lr, lg, lb);
                }

                var eggs = ProceduralItemTileManifest.SpawnEggOverlays;
                for (var i = 0; i < eggs.Length; i++)
                {
                    var (tile, br, bg, bb, sr, sg, sb) = eggs[i];
                    _ = resourcePack.TryBlitSpawnEggTile(pixels, tile, br, bg, bb, sr, sg, sb);
                }

                _ = resourcePack.TryBlitItemTile(pixels, ProceduralItemTileManifest.TileBowPulling0, "bow_pulling_0");
                _ = resourcePack.TryBlitItemTile(pixels, ProceduralItemTileManifest.TileBowPulling1, "bow_pulling_1");
                _ = resourcePack.TryBlitItemTile(pixels, ProceduralItemTileManifest.TileBowPulling2, "bow_pulling_2");
            }

            _ = resourcePack.TryExtractRedBedTiles(pixels, TileRedBedTopHead, TileRedBedTopFoot, TileRedBedHeadSide, TileRedBedSide);
        }

        // Render isometric 2-post + 2-rail Oak Fence inventory icon directly from the loaded oak_planks tile!
        PaintFenceInventoryIconFromPlanks(pixels, ProceduralItemTileManifest.TileOakFenceItem, TileOakPlanks);

        var lookup = BuildFaceLookup();
        var nextCustomTile = FirstCustomTile;
        var entries = blocks.Entries;
        for (var i = 0; i < entries.Count && nextCustomTile < FirstItemSpriteTile; i++)
        {
            var def = entries[i];
            if (def.Key == BuiltInBlocks.Air || lookup.ContainsKey((def.Key, Direction.Up)))
            {
                continue;
            }

            var hasher = Fnv1a64.Create();
            hasher.AppendString(def.Key.ToString());
            var h = hasher.Value;

            ProceduralBlockTilePainter.PaintNoiseTile(
                pixels,
                nextCustomTile,
                (byte)(64 + ((h >> 0) & 0x7F)),
                (byte)(64 + ((h >> 8) & 0x7F)),
                (byte)(64 + ((h >> 16) & 0x7F)),
                variation: 24,
                alpha: def.IsOpaque ? (byte)255 : (byte)200);
            MapAllFaces(lookup, def.Key, nextCustomTile);
            nextCustomTile++;
        }

        for (var t = nextCustomTile; t < FirstItemSpriteTile; t++)
        {
            ProceduralBlockTilePainter.PaintNoiseTile(pixels, t, 110, 112, 118, variation: 16, alpha: 255);
        }

        var itemLookup = new Dictionary<ResourceKey, ushort>();
        for (var i = 0; i < itemTiles.Length; i++)
        {
            itemLookup[itemTiles[i].ItemKey] = itemTiles[i].Tile;
        }

        itemLookup[BuiltInBlocks.Torch] = TileTorch;
        itemLookup[BuiltInBlocks.RedstoneTorch] = TileRedstoneTorch;
        itemLookup[BuiltInBlocks.Lever] = TileLever;
        itemLookup[BuiltInBlocks.Ladder] = TileLadder;
        itemLookup[BuiltInBlocks.Rail] = TileRail;
        itemLookup[BuiltInBlocks.Cobweb] = TileCobweb;
        itemLookup[BuiltInBlocks.OakFence] = ProceduralItemTileManifest.TileOakFenceItem;

        return new TextureAtlasData(
            AtlasDimension,
            AtlasDimension,
            TileDimension,
            pixels,
            lookup.ToFrozenDictionary(),
            itemLookup.ToFrozenDictionary(),
            FirstCustomTile);
    }
}
