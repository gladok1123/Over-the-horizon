using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Client;

/// <summary>
/// Manages the 9-slot quick-access hotbar, the 41-slot Survival <see cref="PlayerInventory"/>,
/// and the full 51-entry Creative Catalog (blocks, PvP swords, mining tools, buckets, food, and materials)
/// with <c>16x16</c> atlas sprite tile mappings.
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

        // New v0.4.0-alpha Blocks (22..26)
        (BuiltInBlocks.Gravel, "Gravel", ItemCategory.NaturalBlocks, new Vector3(0.54f, 0.52f, 0.52f)),
        (BuiltInBlocks.Obsidian, "Obsidian", ItemCategory.BuildingBlocks, new Vector3(0.16f, 0.12f, 0.24f)),
        (BuiltInBlocks.Tnt, "TNT", ItemCategory.FunctionalBlocks, new Vector3(0.86f, 0.24f, 0.20f)),
        (BuiltInBlocks.WhiteWool, "White Wool", ItemCategory.BuildingBlocks, new Vector3(0.94f, 0.94f, 0.96f)),
        (BuiltInBlocks.BlueWool, "Blue Wool", ItemCategory.BuildingBlocks, new Vector3(0.22f, 0.34f, 0.78f)),

        // PvP Melee Swords & Mining Tools (27..41)
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

        // Buckets, Edible Foods & Crafting Materials (42..52)
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
    ];

    private readonly IBlockRegistry _blocks;
    private readonly IRegistry<ItemDefinition> _items;
    private readonly int[] _creativeSlots = [3, 1, 0, 4, 14, 8, 31, 43, 47];
    private readonly Vector3[] _hotbarColors = new Vector3[PlayerInventory.HotbarSlotCount];
    private readonly int[] _hotbarTiles = new int[PlayerInventory.HotbarSlotCount];
    private readonly int[] _hotbarCounts = new int[PlayerInventory.HotbarSlotCount];

    private TextureAtlasData? _atlas;
    private int _selectedSlot;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientHotbar"/> class.
    /// </summary>
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
    public string SelectedBlockDisplayName
    {
        get
        {
            if (ActiveMode == GameMode.Creative)
            {
                return Catalog[_creativeSlots[_selectedSlot]].DisplayName;
            }

            return SelectedItem?.EffectiveName ?? string.Empty;
        }
    }

    /// <summary>Gets the representative 3D viewmodel RGB tint of the selected slot.</summary>
    public Vector3 SelectedBlockColor => _hotbarColors[_selectedSlot];

    /// <summary>Gets whether the selected item is a melee sword or harvesting tool.</summary>
    public bool SelectedIsToolOrWeapon =>
        SelectedItem is { } def && (def.ToolType != ToolKind.None || def.Category == ItemCategory.CombatAndTools);

    /// <summary>Gets whether the selected hotbar slot is empty.</summary>
    public bool SelectedIsEmpty => SelectedItem is null;

    /// <summary>Gets the default <see cref="BlockState"/> placed by the selected item (<c>Air</c> for non-block items).</summary>
    public BlockState SelectedBlockState =>
        SelectedItem?.PlacedBlockKey is { } blockKey ? _blocks.GetDefaultState(blockKey) : BlockState.Air;

    /// <summary>Gets the 9 RGB fallback color swatches equipped in the hotbar.</summary>
    public ReadOnlyMemory<Vector3> ActiveSlotColors => _hotbarColors;

    /// <summary>Gets the 9 <c>16x16</c> atlas sprite tile indices equipped in the hotbar (<c>-1</c> when empty).</summary>
    public ReadOnlyMemory<int> ActiveSlotTiles => _hotbarTiles;

    /// <summary>Gets the 9 stack counts equipped in the hotbar (<c>0</c> when empty).</summary>
    public ReadOnlyMemory<int> ActiveSlotCounts => _hotbarCounts;

    /// <summary>Binds the loaded <see cref="TextureAtlasData"/> so hotbar and inventory cells resolve exact <c>16x16</c> sprite tiles.</summary>
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
    public int ResolveAtlasTile(ResourceKey key) =>
        _atlas is not null ? _atlas.GetItemTileIndex(key) : -1;

    /// <summary>Resolves the fallback RGB color for <paramref name="key"/>.</summary>
    public static Vector3 ResolveFallbackColor(ResourceKey key)
    {
        for (var i = 0; i < Catalog.Length; i++)
        {
            if (Catalog[i].Key == key)
            {
                return Catalog[i].Color;
            }
        }

        return new Vector3(0.65f, 0.65f, 0.68f);
    }

    /// <summary>Builds a concise gameplay stats line for hover tooltips.</summary>
    public string BuildTooltipDetail(ResourceKey key)
    {
        if (!_items.TryGet(key, out var def))
        {
            return string.Empty;
        }

        if (def.ToolType == ToolKind.Sword)
        {
            return $"+{def.AttackDamage:F0} PvP Attack Damage (Jump Crit: {def.AttackDamage * 1.5f:F1})";
        }

        if (def.ToolType != ToolKind.None)
        {
            return $"{def.ToolType} (Mining Speed x{def.MiningSpeedMultiplier:F1}, +{def.AttackDamage:F1} Dmg)";
        }

        return def.UseAction switch
        {
            ItemUseAction.EatFood => $"Edible Food: +{def.FoodNutrition} Hunger" + (def.BonusHealPoints > 0 ? $" +{def.BonusHealPoints:F0} HP" : string.Empty),
            ItemUseAction.WaterBucket => "Right-Click: Pour Water source block",
            ItemUseAction.EmptyBucket => "Right-Click: Scoop Water into Water Bucket",
            ItemUseAction.IgniteTnt => "Right-Click: Detonate TNT / Quench Water to Obsidian",
            ItemUseAction.PlaceBlock => "Placeable Voxel Block (Right-Click)",
            _ => "Crafting Material",
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
        if (delta == 0)
        {
            return;
        }

        _selectedSlot = ((_selectedSlot + delta) % PlayerInventory.HotbarSlotCount + PlayerInventory.HotbarSlotCount) % PlayerInventory.HotbarSlotCount;
        SurvivalInventory.SelectedHotbarSlot = _selectedSlot;
    }

    /// <summary>Equips a Creative Catalog item into <paramref name="hotbarSlot"/>.</summary>
    public void EquipPaletteEntryInSlot(int paletteIndex, int hotbarSlot)
    {
        var slot = Math.Clamp(hotbarSlot, 0, PlayerInventory.HotbarSlotCount - 1);
        _creativeSlots[slot] = Math.Clamp(paletteIndex, 0, Catalog.Length - 1);
        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Equips a Creative Catalog item into the currently selected hotbar slot.</summary>
    public void EquipPaletteBlock(int paletteIndex) =>
        EquipPaletteEntryInSlot(paletteIndex, _selectedSlot);

    /// <summary>Populates starter Survival gear so players can immediately mine, craft, eat, use buckets, and PvP.</summary>
    public void PopulateStarterSurvivalGear()
    {
        SurvivalInventory.Clear();
        SetStarterSlot(0, BuiltInItems.WoodenPickaxe, 1);
        SetStarterSlot(1, BuiltInItems.StoneSword, 1);
        SetStarterSlot(2, BuiltInItems.Bucket, 1);
        SetStarterSlot(3, BuiltInItems.Bread, 8);
        SetStarterSlot(4, BuiltInBlocks.Cobblestone, 16);
        SetStarterSlot(5, BuiltInBlocks.CraftingTable, 1);
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
        if (id > 0)
        {
            SurvivalInventory.SetSlot(slot, new ItemStack((ushort)id, count));
        }
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
