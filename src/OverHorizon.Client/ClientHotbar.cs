using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Client;

/// <summary>
/// Manages the 9-slot quick-access hotbar, the 41-slot Survival <see cref="PlayerInventory"/> (including 4 Armor slots),
/// and the 108-entry Creative Catalog with <c>16x16</c> atlas sprite tile mappings and 3D held-viewmodel face tiles.
/// </summary>
public sealed class ClientHotbar
{
    private static readonly (ResourceKey Key, string DisplayName, ItemCategory Category, Vector3 Color)[] Catalog =
    [
        // Indices 0..21 preserve deterministic block ordering + full 23w13a__or__b sprites
        (BuiltInBlocks.Cobblestone, "Cobblestone", ItemCategory.BuildingBlocks, new Vector3(0.46f, 0.46f, 0.48f)),
        (BuiltInBlocks.Stone, "Stone", ItemCategory.BuildingBlocks, new Vector3(0.56f, 0.56f, 0.58f)),
        (BuiltInBlocks.Dirt, "Dirt", ItemCategory.NaturalBlocks, new Vector3(0.53f, 0.37f, 0.24f)),
        (BuiltInBlocks.GrassBlock, "Grass Block", ItemCategory.NaturalBlocks, new Vector3(0.36f, 0.69f, 0.26f)),
        (BuiltInBlocks.OakPlanks, "Oak Planks", ItemCategory.BuildingBlocks, new Vector3(0.72f, 0.57f, 0.35f)),
        (BuiltInBlocks.OakLog, "Oak Log", ItemCategory.NaturalBlocks, new Vector3(0.42f, 0.31f, 0.19f)),
        (BuiltInBlocks.Sand, "Sand", ItemCategory.NaturalBlocks, new Vector3(0.88f, 0.83f, 0.60f)),
        (BuiltInBlocks.OakLeaves, "Oak Leaves", ItemCategory.NaturalBlocks, new Vector3(0.22f, 0.53f, 0.18f)),
        (BuiltInBlocks.Glowstone, "Glowstone Lamp", ItemCategory.FunctionalBlocks, new Vector3(0.98f, 0.86f, 0.42f)),
        (BuiltInBlocks.StoneBricks, "Stone Bricks", ItemCategory.BuildingBlocks, new Vector3(0.52f, 0.52f, 0.55f)),
        (BuiltInBlocks.Bricks, "Clay Bricks", ItemCategory.BuildingBlocks, new Vector3(0.66f, 0.31f, 0.25f)),
        (BuiltInBlocks.Glass, "Glass Block", ItemCategory.FunctionalBlocks, new Vector3(0.72f, 0.90f, 0.96f)),
        (BuiltInBlocks.Sandstone, "Sandstone", ItemCategory.BuildingBlocks, new Vector3(0.85f, 0.79f, 0.58f)),
        (BuiltInBlocks.Bookshelf, "Bookshelf", ItemCategory.FunctionalBlocks, new Vector3(0.62f, 0.44f, 0.28f)),
        (BuiltInBlocks.CraftingTable, "Crafting Table", ItemCategory.FunctionalBlocks, new Vector3(0.64f, 0.47f, 0.28f)),
        (BuiltInBlocks.MossyCobblestone, "Mossy Cobblestone", ItemCategory.BuildingBlocks, new Vector3(0.36f, 0.52f, 0.34f)),
        (BuiltInBlocks.SnowBlock, "Snow Block", ItemCategory.NaturalBlocks, new Vector3(0.94f, 0.96f, 0.98f)),
        (BuiltInBlocks.CrimsonWool, "Crimson Wool", ItemCategory.BuildingBlocks, new Vector3(0.74f, 0.18f, 0.22f)),
        (BuiltInBlocks.CoalOre, "Coal Ore", ItemCategory.NaturalBlocks, new Vector3(0.24f, 0.24f, 0.26f)),
        (BuiltInBlocks.IronOre, "Iron Ore", ItemCategory.NaturalBlocks, new Vector3(0.78f, 0.66f, 0.56f)),
        (BuiltInBlocks.GoldOre, "Gold Ore", ItemCategory.NaturalBlocks, new Vector3(0.95f, 0.82f, 0.24f)),
        (BuiltInBlocks.DiamondOre, "Diamond Ore", ItemCategory.NaturalBlocks, new Vector3(0.32f, 0.92f, 0.88f)),

        // Indices 22..26
        (BuiltInBlocks.Gravel, "Gravel", ItemCategory.NaturalBlocks, new Vector3(0.54f, 0.52f, 0.52f)),
        (BuiltInBlocks.Obsidian, "Obsidian", ItemCategory.BuildingBlocks, new Vector3(0.16f, 0.12f, 0.24f)),
        (BuiltInBlocks.Tnt, "TNT", ItemCategory.FunctionalBlocks, new Vector3(0.86f, 0.24f, 0.20f)),
        (BuiltInBlocks.WhiteWool, "White Wool", ItemCategory.BuildingBlocks, new Vector3(0.94f, 0.94f, 0.96f)),
        (BuiltInBlocks.BlueWool, "Blue Wool", ItemCategory.BuildingBlocks, new Vector3(0.22f, 0.34f, 0.78f)),

        // Indices 27..41 (31 == DiamondSword)
        (BuiltInItems.WoodenSword, "Wooden Sword", ItemCategory.CombatAndTools, new Vector3(0.68f, 0.52f, 0.30f)),
        (BuiltInItems.StoneSword, "Stone Sword", ItemCategory.CombatAndTools, new Vector3(0.58f, 0.58f, 0.60f)),
        (BuiltInItems.IronSword, "Iron Sword", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.GoldenSword, "Golden Sword", ItemCategory.CombatAndTools, new Vector3(0.98f, 0.86f, 0.24f)),
        (BuiltInItems.DiamondSword, "Diamond Sword", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.92f, 0.88f)),
        (BuiltInItems.NetheriteSword, "Netherite Sword", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.24f, 0.30f)),
        (BuiltInItems.WoodenPickaxe, "Wooden Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.68f, 0.52f, 0.30f)),
        (BuiltInItems.StonePickaxe, "Stone Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.58f, 0.58f, 0.60f)),
        (BuiltInItems.IronPickaxe, "Iron Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.DiamondPickaxe, "Diamond Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.92f, 0.88f)),
        (BuiltInItems.IronAxe, "Iron Axe", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.DiamondAxe, "Diamond Axe", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.92f, 0.88f)),
        (BuiltInItems.IronShovel, "Iron Shovel", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.Shears, "Shears", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.FlintAndSteel, "Flint and Steel", ItemCategory.CombatAndTools, new Vector3(0.75f, 0.76f, 0.80f)),

        // Indices 42..52
        (BuiltInItems.Bucket, "Bucket", ItemCategory.FoodAndMaterials, new Vector3(0.78f, 0.80f, 0.84f)),
        (BuiltInItems.WaterBucket, "Water Bucket", ItemCategory.FoodAndMaterials, new Vector3(0.22f, 0.52f, 0.96f)),
        (BuiltInItems.Apple, "Apple", ItemCategory.FoodAndMaterials, new Vector3(0.90f, 0.20f, 0.22f)),
        (BuiltInItems.Bread, "Bread", ItemCategory.FoodAndMaterials, new Vector3(0.82f, 0.62f, 0.28f)),
        (BuiltInItems.CookedBeef, "Steak", ItemCategory.FoodAndMaterials, new Vector3(0.58f, 0.28f, 0.18f)),
        (BuiltInItems.GoldenApple, "Golden Apple", ItemCategory.FoodAndMaterials, new Vector3(0.98f, 0.86f, 0.22f)),
        (BuiltInItems.Stick, "Stick", ItemCategory.FoodAndMaterials, new Vector3(0.62f, 0.46f, 0.26f)),
        (BuiltInItems.Coal, "Coal", ItemCategory.FoodAndMaterials, new Vector3(0.18f, 0.18f, 0.20f)),
        (BuiltInItems.IronIngot, "Iron Ingot", ItemCategory.FoodAndMaterials, new Vector3(0.88f, 0.90f, 0.92f)),
        (BuiltInItems.GoldIngot, "Gold Ingot", ItemCategory.FoodAndMaterials, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInItems.Diamond, "Diamond", ItemCategory.FoodAndMaterials, new Vector3(0.30f, 0.94f, 0.90f)),

        // v0.5.0-alpha Functional, Mineshaft, Ore, Deepslate, Birch & Mineral Blocks (53..76)
        (BuiltInBlocks.Torch, "Torch", ItemCategory.FunctionalBlocks, new Vector3(1.00f, 0.84f, 0.32f)),
        (BuiltInBlocks.Lantern, "Lantern", ItemCategory.FunctionalBlocks, new Vector3(0.98f, 0.78f, 0.30f)),
        (BuiltInBlocks.Furnace, "Furnace", ItemCategory.FunctionalBlocks, new Vector3(0.50f, 0.50f, 0.52f)),
        (BuiltInBlocks.Chest, "Chest", ItemCategory.FunctionalBlocks, new Vector3(0.68f, 0.48f, 0.22f)),
        (BuiltInBlocks.Barrel, "Barrel", ItemCategory.FunctionalBlocks, new Vector3(0.54f, 0.40f, 0.24f)),
        (BuiltInBlocks.Ladder, "Ladder", ItemCategory.FunctionalBlocks, new Vector3(0.68f, 0.52f, 0.28f)),
        (BuiltInBlocks.Rail, "Rail", ItemCategory.FunctionalBlocks, new Vector3(0.72f, 0.68f, 0.60f)),
        (BuiltInBlocks.Cobweb, "Cobweb", ItemCategory.FunctionalBlocks, new Vector3(0.92f, 0.93f, 0.95f)),
        (BuiltInBlocks.OakFence, "Oak Fence", ItemCategory.FunctionalBlocks, new Vector3(0.72f, 0.57f, 0.35f)),
        (BuiltInBlocks.BirchLog, "Birch Log", ItemCategory.NaturalBlocks, new Vector3(0.88f, 0.86f, 0.82f)),
        (BuiltInBlocks.BirchPlanks, "Birch Planks", ItemCategory.BuildingBlocks, new Vector3(0.86f, 0.80f, 0.58f)),
        (BuiltInBlocks.BirchLeaves, "Birch Leaves", ItemCategory.NaturalBlocks, new Vector3(0.38f, 0.62f, 0.28f)),
        (BuiltInBlocks.Deepslate, "Deepslate", ItemCategory.BuildingBlocks, new Vector3(0.30f, 0.30f, 0.33f)),
        (BuiltInBlocks.CobbledDeepslate, "Cobbled Deepslate", ItemCategory.BuildingBlocks, new Vector3(0.28f, 0.28f, 0.31f)),
        (BuiltInBlocks.CopperOre, "Copper Ore", ItemCategory.NaturalBlocks, new Vector3(0.82f, 0.48f, 0.32f)),
        (BuiltInBlocks.LapisOre, "Lapis Lazuli Ore", ItemCategory.NaturalBlocks, new Vector3(0.20f, 0.38f, 0.86f)),
        (BuiltInBlocks.RedstoneOre, "Redstone Ore", ItemCategory.NaturalBlocks, new Vector3(0.86f, 0.16f, 0.18f)),
        (BuiltInBlocks.EmeraldOre, "Emerald Ore", ItemCategory.NaturalBlocks, new Vector3(0.22f, 0.88f, 0.42f)),
        (BuiltInBlocks.CoalBlock, "Block of Coal", ItemCategory.BuildingBlocks, new Vector3(0.14f, 0.14f, 0.16f)),
        (BuiltInBlocks.IronBlock, "Block of Iron", ItemCategory.BuildingBlocks, new Vector3(0.90f, 0.91f, 0.93f)),
        (BuiltInBlocks.GoldBlock, "Block of Gold", ItemCategory.BuildingBlocks, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInBlocks.DiamondBlock, "Block of Diamond", ItemCategory.BuildingBlocks, new Vector3(0.34f, 0.94f, 0.90f)),
        (BuiltInBlocks.Farmland, "Farmland", ItemCategory.NaturalBlocks, new Vector3(0.42f, 0.28f, 0.16f)),
        (BuiltInBlocks.HayBlock, "Hay Bale", ItemCategory.NaturalBlocks, new Vector3(0.84f, 0.72f, 0.22f)),

        // v0.5.0-alpha Tools, Hoes, Armor, Smelting Metals & Mob Drops (77..116)
        (BuiltInItems.WoodenHoe, "Wooden Hoe", ItemCategory.CombatAndTools, new Vector3(0.68f, 0.52f, 0.30f)),
        (BuiltInItems.StoneHoe, "Stone Hoe", ItemCategory.CombatAndTools, new Vector3(0.58f, 0.58f, 0.60f)),
        (BuiltInItems.IronHoe, "Iron Hoe", ItemCategory.CombatAndTools, new Vector3(0.86f, 0.88f, 0.90f)),
        (BuiltInItems.DiamondHoe, "Diamond Hoe", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.92f, 0.88f)),
        (BuiltInItems.GoldenPickaxe, "Golden Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.98f, 0.84f, 0.22f)),
        (BuiltInItems.NetheritePickaxe, "Netherite Pickaxe", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.24f, 0.30f)),
        (BuiltInItems.WoodenAxe, "Wooden Axe", ItemCategory.CombatAndTools, new Vector3(0.68f, 0.52f, 0.30f)),
        (BuiltInItems.StoneAxe, "Stone Axe", ItemCategory.CombatAndTools, new Vector3(0.58f, 0.58f, 0.60f)),
        (BuiltInItems.WoodenShovel, "Wooden Shovel", ItemCategory.CombatAndTools, new Vector3(0.68f, 0.52f, 0.30f)),
        (BuiltInItems.StoneShovel, "Stone Shovel", ItemCategory.CombatAndTools, new Vector3(0.58f, 0.58f, 0.60f)),
        (BuiltInItems.DiamondShovel, "Diamond Shovel", ItemCategory.CombatAndTools, new Vector3(0.28f, 0.92f, 0.88f)),
        (BuiltInItems.IronHelmet, "Iron Helmet", ItemCategory.CombatAndTools, new Vector3(0.88f, 0.90f, 0.92f)),
        (BuiltInItems.IronChestplate, "Iron Chestplate", ItemCategory.CombatAndTools, new Vector3(0.88f, 0.90f, 0.92f)),
        (BuiltInItems.IronLeggings, "Iron Leggings", ItemCategory.CombatAndTools, new Vector3(0.88f, 0.90f, 0.92f)),
        (BuiltInItems.IronBoots, "Iron Boots", ItemCategory.CombatAndTools, new Vector3(0.88f, 0.90f, 0.92f)),
        (BuiltInItems.DiamondHelmet, "Diamond Helmet", ItemCategory.CombatAndTools, new Vector3(0.30f, 0.94f, 0.90f)),
        (BuiltInItems.DiamondChestplate, "Diamond Chestplate", ItemCategory.CombatAndTools, new Vector3(0.30f, 0.94f, 0.90f)),
        (BuiltInItems.DiamondLeggings, "Diamond Leggings", ItemCategory.CombatAndTools, new Vector3(0.30f, 0.94f, 0.90f)),
        (BuiltInItems.DiamondBoots, "Diamond Boots", ItemCategory.CombatAndTools, new Vector3(0.30f, 0.94f, 0.90f)),
        (BuiltInItems.RawIron, "Raw Iron", ItemCategory.FoodAndMaterials, new Vector3(0.82f, 0.68f, 0.56f)),
        (BuiltInItems.RawCopper, "Raw Copper", ItemCategory.FoodAndMaterials, new Vector3(0.84f, 0.48f, 0.32f)),
        (BuiltInItems.CopperIngot, "Copper Ingot", ItemCategory.FoodAndMaterials, new Vector3(0.86f, 0.50f, 0.34f)),
        (BuiltInItems.RawGold, "Raw Gold", ItemCategory.FoodAndMaterials, new Vector3(0.96f, 0.80f, 0.24f)),
        (BuiltInItems.Emerald, "Emerald", ItemCategory.FoodAndMaterials, new Vector3(0.22f, 0.88f, 0.44f)),
        (BuiltInItems.LapisLazuli, "Lapis Lazuli", ItemCategory.FoodAndMaterials, new Vector3(0.20f, 0.40f, 0.88f)),
        (BuiltInItems.Redstone, "Redstone Dust", ItemCategory.FoodAndMaterials, new Vector3(0.88f, 0.14f, 0.16f)),
        (BuiltInItems.Charcoal, "Charcoal", ItemCategory.FoodAndMaterials, new Vector3(0.22f, 0.20f, 0.18f)),
        (BuiltInItems.NetheriteIngot, "Netherite Ingot", ItemCategory.FoodAndMaterials, new Vector3(0.26f, 0.22f, 0.28f)),
        (BuiltInItems.Flint, "Flint", ItemCategory.FoodAndMaterials, new Vector3(0.34f, 0.34f, 0.38f)),
        (BuiltInItems.RawBeef, "Raw Beef", ItemCategory.FoodAndMaterials, new Vector3(0.82f, 0.24f, 0.22f)),
        (BuiltInItems.RawPorkchop, "Raw Porkchop", ItemCategory.FoodAndMaterials, new Vector3(0.92f, 0.52f, 0.50f)),
        (BuiltInItems.CookedPorkchop, "Cooked Porkchop", ItemCategory.FoodAndMaterials, new Vector3(0.84f, 0.62f, 0.44f)),
        (BuiltInItems.RawMutton, "Raw Mutton", ItemCategory.FoodAndMaterials, new Vector3(0.84f, 0.28f, 0.26f)),
        (BuiltInItems.CookedMutton, "Cooked Mutton", ItemCategory.FoodAndMaterials, new Vector3(0.64f, 0.34f, 0.24f)),
        (BuiltInItems.RawChicken, "Raw Chicken", ItemCategory.FoodAndMaterials, new Vector3(0.92f, 0.72f, 0.68f)),
        (BuiltInItems.CookedChicken, "Cooked Chicken", ItemCategory.FoodAndMaterials, new Vector3(0.82f, 0.60f, 0.36f)),
        (BuiltInItems.RottenFlesh, "Rotten Flesh", ItemCategory.FoodAndMaterials, new Vector3(0.56f, 0.32f, 0.22f)),
        (BuiltInItems.Leather, "Leather", ItemCategory.FoodAndMaterials, new Vector3(0.68f, 0.40f, 0.22f)),
        (BuiltInItems.Feather, "Feather", ItemCategory.FoodAndMaterials, new Vector3(0.95f, 0.96f, 0.98f)),
        (BuiltInItems.String, "String", ItemCategory.FoodAndMaterials, new Vector3(0.92f, 0.93f, 0.95f)),
        (BuiltInItems.Bone, "Bone", ItemCategory.FoodAndMaterials, new Vector3(0.92f, 0.90f, 0.84f)),
        (BuiltInItems.Gunpowder, "Gunpowder", ItemCategory.FoodAndMaterials, new Vector3(0.42f, 0.42f, 0.44f)),
        (BuiltInItems.SpiderEye, "Spider Eye", ItemCategory.FoodAndMaterials, new Vector3(0.62f, 0.14f, 0.24f)),
    ];

    private readonly IBlockRegistry _blocks;
    private readonly IRegistry<ItemDefinition> _items;
    private readonly int[] _creativeSlots = [0, 1, 53, 55, 14, 5, 31, 35, 8];
    private readonly Vector3[] _hotbarColors = new Vector3[PlayerInventory.HotbarSlotCount];
    private readonly int[] _hotbarTiles = new int[PlayerInventory.HotbarSlotCount];
    private readonly int[] _hotbarCounts = new int[PlayerInventory.HotbarSlotCount];

    private TextureAtlasData? _atlas;
    private int _selectedSlot;

    /// <summary>Initializes a new instance of the <see cref="ClientHotbar"/> class.</summary>
    public ClientHotbar(IBlockRegistry blocks, IRegistry<ItemDefinition>? items = null, TextureAtlasData? atlas = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        _blocks = blocks;
        _items = items ?? BuiltInItems.CreateFrozen(blocks);
        _atlas = atlas;
        SurvivalInventory = new PlayerInventory();
        PopulateStarterSurvivalGear();
        RefreshSlotVisuals(GameMode.Creative);
    }

    /// <summary>Gets the total number of blocks and items in the Creative Catalog.</summary>
    public static int PaletteCount => Catalog.Length;

    /// <summary>Gets the number of hotbar slots (<c>9</c>).</summary>
    public int SlotCount => _hotbarColors.Length;

    /// <summary>Gets the active zero-based hotbar slot index (<c>0..8</c>).</summary>
    public int SelectedSlot => _selectedSlot;

    /// <summary>Gets the 41-slot Survival inventory.</summary>
    public PlayerInventory SurvivalInventory { get; }

    /// <summary>Gets the active <see cref="GameMode"/> mirrored into hotbar slots.</summary>
    public GameMode ActiveMode { get; private set; } = GameMode.Creative;

    /// <summary>Gets the frozen item registry associated with this hotbar.</summary>
    public IRegistry<ItemDefinition> Items => _items;

    /// <summary>Gets the currently held <see cref="ItemDefinition"/>, or <see langword="null"/> when empty-handed.</summary>
    public ItemDefinition? SelectedItem => GetSlotItem(_selectedSlot);

    /// <summary>Gets the <see cref="ResourceKey"/> of the currently selected item or block.</summary>
    public ResourceKey SelectedBlockKey => SelectedItem?.Key ?? BuiltInBlocks.Air;

    /// <summary>Gets the display name of the currently selected item or block.</summary>
    public string SelectedBlockDisplayName => ActiveMode == GameMode.Creative
        ? Catalog[_creativeSlots[_selectedSlot]].DisplayName
        : (SelectedItem?.EffectiveName ?? string.Empty);

    /// <summary>Gets the representative 3D viewmodel RGB tint of the selected slot.</summary>
    public Vector3 SelectedBlockColor => _hotbarColors[_selectedSlot];

    /// <summary>Gets whether the selected item is a melee sword or harvesting tool.</summary>
    public bool SelectedIsToolOrWeapon =>
        SelectedItem is { } def && (def.ToolType != ToolKind.None || def.Category == ItemCategory.CombatAndTools);

    /// <summary>Gets whether the selected hotbar slot is empty.</summary>
    public bool SelectedIsEmpty => SelectedItem is null;

    /// <summary>Gets whether the selected item renders as a 3D block cube in first-person hand.</summary>
    public bool SelectedIsBlock3D =>
        SelectedItem?.PlacedBlockKey is { } bk &&
        bk != BuiltInBlocks.Torch &&
        bk != BuiltInBlocks.Lantern &&
        bk != BuiltInBlocks.Rail &&
        bk != BuiltInBlocks.Ladder &&
        bk != BuiltInBlocks.Cobweb;

    /// <summary>Gets whether the selected item renders as a hand-held 3D Torch/Lantern.</summary>
    public bool SelectedIsTorchStyle =>
        SelectedBlockKey == BuiltInBlocks.Torch || SelectedBlockKey == BuiltInBlocks.Lantern;

    /// <summary>Gets the <c>0..15</c> dynamic light level emitted by the currently held item.</summary>
    public byte SelectedHeldLightEmission => SelectedItem?.HeldLightEmission ?? 0;

    /// <summary>Gets the <c>16x16</c> atlas tile index for the top face of the held block.</summary>
    public int SelectedTopTile =>
        _atlas is not null && SelectedItem?.PlacedBlockKey is { } bk ? _atlas.GetTileIndex(bk, Direction.Up) : -1;

    /// <summary>Gets the <c>16x16</c> atlas tile index for the side faces of the held block.</summary>
    public int SelectedSideTile =>
        _atlas is not null && SelectedItem?.PlacedBlockKey is { } bk ? _atlas.GetTileIndex(bk, Direction.East) : -1;

    /// <summary>Gets the <c>16x16</c> atlas tile index for the front face of the held block.</summary>
    public int SelectedFrontTile =>
        _atlas is not null && SelectedItem?.PlacedBlockKey is { } bk ? _atlas.GetTileIndex(bk, Direction.North) : -1;

    /// <summary>Gets the <c>16x16</c> atlas tile index for the bottom face of the held block.</summary>
    public int SelectedBottomTile =>
        _atlas is not null && SelectedItem?.PlacedBlockKey is { } bk ? _atlas.GetTileIndex(bk, Direction.Down) : -1;

    /// <summary>Gets the <c>16x16</c> item sprite tile index of the currently selected slot.</summary>
    public int SelectedItemTile => _hotbarTiles[_selectedSlot];

    /// <summary>Gets total armor defense points (<c>0..20</c>) across the 4 armor slots.</summary>
    public int TotalArmorDefense
    {
        get
        {
            var pts = 0;
            for (var a = 0; a < PlayerInventory.ArmorSlotCount; a++)
            {
                var st = SurvivalInventory.GetSlot(PlayerInventory.ArmorSlotOffset + a);
                if (!st.IsEmpty) pts += _items.GetById(st.ItemId).ArmorDefense;
            }

            return Math.Clamp(pts, 0, 20);
        }
    }

    /// <summary>Gets the default <see cref="BlockState"/> placed by the selected item.</summary>
    public BlockState SelectedBlockState =>
        SelectedItem?.PlacedBlockKey is { } blockKey ? _blocks.GetDefaultState(blockKey) : BlockState.Air;

    /// <summary>Gets the 9 RGB fallback color swatches equipped in the hotbar.</summary>
    public ReadOnlyMemory<Vector3> ActiveSlotColors => _hotbarColors;

    /// <summary>Gets the 9 <c>16x16</c> atlas sprite tile indices equipped in the hotbar.</summary>
    public ReadOnlyMemory<int> ActiveSlotTiles => _hotbarTiles;

    /// <summary>Gets the 9 stack counts equipped in the hotbar.</summary>
    public ReadOnlyMemory<int> ActiveSlotCounts => _hotbarCounts;

    /// <summary>Binds the loaded <see cref="TextureAtlasData"/>.</summary>
    public void BindAtlas(TextureAtlasData atlas)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        _atlas = atlas;
        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Gets metadata for a specific Creative Catalog entry.</summary>
    public static (ResourceKey Key, string DisplayName, Vector3 Color) GetPaletteEntry(int paletteIndex)
    {
        var e = Catalog[Math.Clamp(paletteIndex, 0, Catalog.Length - 1)];
        return (e.Key, e.DisplayName, e.Color);
    }

    /// <summary>Gets detailed Creative Catalog metadata including category, sprite tile index, and tooltip stats.</summary>
    public (int Index, ResourceKey Key, string DisplayName, ItemCategory Category, Vector3 Color, int AtlasTile, string TooltipDetail) GetDetailedCatalogEntry(int paletteIndex)
    {
        var idx = Math.Clamp(paletteIndex, 0, Catalog.Length - 1);
        var e = Catalog[idx];
        return (idx, e.Key, e.DisplayName, e.Category, e.Color, ResolveAtlasTile(e.Key), BuildTooltipDetail(e.Key));
    }

    /// <summary>Resolves the <c>0..255</c> <c>16x16</c> sprite tile index for <paramref name="key"/>.</summary>
    public int ResolveAtlasTile(ResourceKey key) => _atlas is not null ? _atlas.GetItemTileIndex(key) : -1;

    /// <summary>Resolves the fallback RGB color for <paramref name="key"/>.</summary>
    public static Vector3 ResolveFallbackColor(ResourceKey key)
    {
        for (var i = 0; i < Catalog.Length; i++)
        {
            if (Catalog[i].Key == key) return Catalog[i].Color;
        }

        return new Vector3(0.65f, 0.65f, 0.68f);
    }

    /// <summary>Builds a concise gameplay stats line for hover tooltips.</summary>
    public string BuildTooltipDetail(ResourceKey key)
    {
        if (!_items.TryGet(key, out var def)) return string.Empty;
        if (def.ArmorDefense > 0) return $"+{def.ArmorDefense} Armor Defense (Equip in Armor Slot or Right-Click)";
        if (def.ToolType == ToolKind.Sword) return $"+{def.AttackDamage:F0} Melee Damage (Jump Crit: {def.AttackDamage * 1.5f:F1})";
        if (def.ToolType != ToolKind.None) return $"{def.ToolType} Tier {def.ToolTier} (Speed x{def.MiningSpeedMultiplier:F1}, +{def.AttackDamage:F1} Dmg)";
        if (def.HeldLightEmission > 0) return $"Emits Light Level {def.HeldLightEmission} (Dynamic Held + Placed Illumination!)";

        return def.UseAction switch
        {
            ItemUseAction.EatFood => $"Food: +{def.FoodNutrition} Hunger" + (def.BonusHealPoints > 0 ? $" +{def.BonusHealPoints:F0} HP" : string.Empty),
            ItemUseAction.WaterBucket => "Right-Click: Pour Water source block",
            ItemUseAction.EmptyBucket => "Right-Click: Scoop Water into Water Bucket",
            ItemUseAction.IgniteTnt => "Right-Click: Detonate TNT / Quench Water",
            ItemUseAction.PlaceBlock => "Placeable Voxel Block (Right-Click)",
            _ => "Crafting & Smelting Material",
        };
    }

    /// <summary>Selects a specific zero-based hotbar slot (<c>0..8</c>).</summary>
    public void SelectSlot(int slotIndex)
    {
        _selectedSlot = Math.Clamp(slotIndex, 0, PlayerInventory.HotbarSlotCount - 1);
        SurvivalInventory.SelectedHotbarSlot = _selectedSlot;
    }

    /// <summary>Cycles the active hotbar slot forward or backward with wrap-around.</summary>
    public void CycleSlot(int delta)
    {
        if (delta == 0) return;
        _selectedSlot = ((_selectedSlot + delta) % PlayerInventory.HotbarSlotCount + PlayerInventory.HotbarSlotCount) % PlayerInventory.HotbarSlotCount;
        SurvivalInventory.SelectedHotbarSlot = _selectedSlot;
    }

    /// <summary>Equips a Creative Catalog item into <paramref name="hotbarSlot"/>.</summary>
    public void EquipPaletteEntryInSlot(int paletteIndex, int hotbarSlot)
    {
        _creativeSlots[Math.Clamp(hotbarSlot, 0, PlayerInventory.HotbarSlotCount - 1)] = Math.Clamp(paletteIndex, 0, Catalog.Length - 1);
        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Equips a Creative Catalog item into the currently selected hotbar slot.</summary>
    public void EquipPaletteBlock(int paletteIndex) => EquipPaletteEntryInSlot(paletteIndex, _selectedSlot);

    /// <summary>Populates starter Survival gear.</summary>
    public void PopulateStarterSurvivalGear()
    {
        SurvivalInventory.Clear();
        SetStarterSlot(0, BuiltInItems.WoodenPickaxe, 1);
        SetStarterSlot(1, BuiltInItems.StoneSword, 1);
        SetStarterSlot(2, BuiltInBlocks.Torch, 16);
        SetStarterSlot(3, BuiltInItems.Bread, 8);
        SetStarterSlot(4, BuiltInBlocks.Cobblestone, 16);
        SetStarterSlot(5, BuiltInBlocks.CraftingTable, 1);
        SetStarterSlot(6, BuiltInBlocks.Furnace, 1);
        SetStarterSlot(7, BuiltInItems.Bucket, 1);
    }

    /// <summary>Synchronizes hotbar sprite tiles, counts, and fallback colors for <paramref name="mode"/>.</summary>
    public void RefreshSlotVisuals(GameMode mode)
    {
        ActiveMode = mode;
        for (var i = 0; i < PlayerInventory.HotbarSlotCount; i++)
        {
            if (mode == GameMode.Creative)
            {
                var entry = Catalog[_creativeSlots[i]];
                _hotbarColors[i] = entry.Color;
                _hotbarTiles[i] = ResolveAtlasTile(entry.Key);
                _hotbarCounts[i] = 1;
            }
            else
            {
                var stack = SurvivalInventory.GetSlot(i);
                if (stack.IsEmpty)
                {
                    _hotbarColors[i] = new Vector3(0.16f, 0.17f, 0.20f);
                    _hotbarTiles[i] = -1;
                    _hotbarCounts[i] = 0;
                }
                else
                {
                    var def = _items.GetById(stack.ItemId);
                    _hotbarColors[i] = ResolveFallbackColor(def.Key);
                    _hotbarTiles[i] = ResolveAtlasTile(def.Key);
                    _hotbarCounts[i] = stack.Count;
                }
            }
        }
    }

    private void SetStarterSlot(int slot, ResourceKey key, int count)
    {
        var id = _items.GetId(key);
        if (id > 0) SurvivalInventory.SetSlot(slot, new ItemStack((ushort)id, count));
    }

    private ItemDefinition? GetSlotItem(int slot)
    {
        if (ActiveMode == GameMode.Creative)
        {
            var key = Catalog[_creativeSlots[slot]].Key;
            return _items.TryGet(key, out var def) ? def : null;
        }

        var stack = SurvivalInventory.GetSlot(slot);
        return stack.IsEmpty ? null : _items.GetById(stack.ItemId);
    }
}
