using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Client;

/// <summary>
/// Manages the 9-slot quick-access hotbar, the 41-slot <see cref="PlayerInventory"/> (including 4 Armor slots and Offhand slot 40),
/// and the Creative Catalog with <c>16x16</c> atlas sprite tile mappings and 3D held-viewmodel face tiles.
/// </summary>
public sealed partial class ClientHotbar
{
    private readonly IBlockRegistry _blocks;
    private readonly IRegistry<ItemDefinition> _items;
    private readonly int[] _creativeSlots = [0, 1, 2, 3, 4, 5, 6, 7, 8];
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

    /// <summary>Gets the 41-slot player inventory.</summary>
    public PlayerInventory SurvivalInventory { get; }

    /// <summary>Gets the active <see cref="GameMode"/> mirrored into hotbar slots.</summary>
    public GameMode ActiveMode { get; private set; } = GameMode.Creative;

    /// <summary>Gets the frozen item registry associated with this hotbar.</summary>
    public IRegistry<ItemDefinition> Items => _items;

    /// <summary>Gets the currently held main-hand <see cref="ItemDefinition"/>, or <see langword="null"/> when empty-handed.</summary>
    public ItemDefinition? SelectedItem => GetSlotItem(_selectedSlot);

    /// <summary>Gets the currently held offhand <see cref="ItemDefinition"/>, or <see langword="null"/> when empty.</summary>
    public ItemDefinition? OffhandItem
    {
        get
        {
            var stack = SurvivalInventory.OffhandItem;
            return stack.IsEmpty ? null : _items.GetById(stack.ItemId);
        }
    }

    /// <summary>Gets the <see cref="ResourceKey"/> of the currently selected main-hand item or block.</summary>
    public ResourceKey SelectedBlockKey => SelectedItem?.Key ?? BuiltInBlocks.Air;

    /// <summary>Gets the <see cref="ResourceKey"/> of the offhand item or block.</summary>
    public ResourceKey OffhandKey => OffhandItem?.Key ?? BuiltInBlocks.Air;

    /// <summary>Gets the display name of the currently selected item or block.</summary>
    public string SelectedBlockDisplayName => ActiveMode == GameMode.Creative && _creativeSlots[_selectedSlot] >= 0
        ? Catalog[_creativeSlots[_selectedSlot]].DisplayName
        : (SelectedItem?.EffectiveName ?? string.Empty);

    /// <summary>Gets the representative 3D viewmodel RGB tint of the selected slot.</summary>
    public Vector3 SelectedBlockColor => _hotbarColors[_selectedSlot];

    /// <summary>Gets the representative 3D viewmodel RGB tint of the offhand slot.</summary>
    public Vector3 OffhandColor => OffhandItem is { } def ? ResolveFallbackColor(def.Key) : new Vector3(0.16f, 0.17f, 0.20f);

    /// <summary>Gets whether the selected main-hand item is a melee sword or harvesting tool.</summary>
    public bool SelectedIsToolOrWeapon => IsToolOrWeapon(SelectedItem);

    /// <summary>Gets whether the offhand item is a melee sword or harvesting tool.</summary>
    public bool OffhandIsToolOrWeapon => IsToolOrWeapon(OffhandItem);

    /// <summary>Gets whether the offhand item is a <c>Shield</c>.</summary>
    public bool OffhandIsShield => OffhandKey == BuiltInItems.Shield;

    /// <summary>Gets whether the selected main-hand item is a <c>Shield</c>.</summary>
    public bool SelectedIsShield => SelectedBlockKey == BuiltInItems.Shield;

    /// <summary>Gets whether the selected main-hand item is a <c>Bow</c>.</summary>
    public bool SelectedIsBow => SelectedBlockKey == BuiltInItems.Bow;

    /// <summary>Gets whether the selected hotbar slot is empty.</summary>
    public bool SelectedIsEmpty => SelectedItem is null;

    /// <summary>Gets whether the offhand slot is empty.</summary>
    public bool OffhandIsEmpty => OffhandItem is null;

    /// <summary>Gets the stack count in the offhand slot.</summary>
    public int OffhandCount => SurvivalInventory.OffhandItem.Count;

    /// <summary>Gets whether the selected item renders as a 3D block cube in first-person hand.</summary>
    public bool SelectedIsBlock3D => IsBlock3DCube(SelectedItem);

    /// <summary>Gets whether the offhand item renders as a 3D block cube in first-person left hand.</summary>
    public bool OffhandIsBlock3D => IsBlock3DCube(OffhandItem);

    /// <summary>Gets whether the selected item renders as a hand-held 3D Torch/Lantern/RedstoneTorch.</summary>
    public bool SelectedIsTorchStyle => IsTorchStyleKey(SelectedBlockKey);

    /// <summary>Gets whether the offhand item renders as a hand-held 3D Torch/Lantern/RedstoneTorch.</summary>
    public bool OffhandIsTorchStyle => IsTorchStyleKey(OffhandKey);

    /// <summary>Gets the <c>0..15</c> dynamic light level emitted by either main-hand or offhand item.</summary>
    public byte SelectedHeldLightEmission =>
        Math.Max(SelectedItem?.HeldLightEmission ?? 0, OffhandItem?.HeldLightEmission ?? 0);

    /// <summary>Gets the combined <c>0..15</c> dynamic light level across both main-hand and offhand slots.</summary>
    public byte CombinedHeldLightEmission => SelectedHeldLightEmission;

    /// <summary>Gets the <c>16x16</c> atlas tile index for the top face of the held block.</summary>
    public int SelectedTopTile => GetFaceTile(SelectedItem, Direction.Up);

    /// <summary>Gets the <c>16x16</c> atlas tile index for the side faces of the held block.</summary>
    public int SelectedSideTile => GetFaceTile(SelectedItem, Direction.East);

    /// <summary>Gets the <c>16x16</c> atlas tile index for the front face of the held block.</summary>
    public int SelectedFrontTile => GetFaceTile(SelectedItem, Direction.North);

    /// <summary>Gets the <c>16x16</c> atlas tile index for the bottom face of the held block.</summary>
    public int SelectedBottomTile => GetFaceTile(SelectedItem, Direction.Down);

    /// <summary>Gets the <c>16x16</c> atlas tile index for the top face of the offhand block.</summary>
    public int OffhandTopTile => GetFaceTile(OffhandItem, Direction.Up);

    /// <summary>Gets the <c>16x16</c> atlas tile index for the side faces of the offhand block.</summary>
    public int OffhandSideTile => GetFaceTile(OffhandItem, Direction.East);

    /// <summary>Gets the <c>16x16</c> atlas tile index for the front face of the offhand block.</summary>
    public int OffhandFrontTile => GetFaceTile(OffhandItem, Direction.North);

    /// <summary>Gets the <c>16x16</c> atlas tile index for the bottom face of the offhand block.</summary>
    public int OffhandBottomTile => GetFaceTile(OffhandItem, Direction.Down);

    /// <summary>Gets the <c>16x16</c> item sprite tile index of the currently selected slot.</summary>
    public int SelectedItemTile => _hotbarTiles[_selectedSlot];

    /// <summary>Gets the <c>16x16</c> item sprite tile index of the offhand slot.</summary>
    public int OffhandItemTile => OffhandItem is { } def ? ResolveAtlasTile(def.Key) : -1;

    /// <summary>Gets total armor defense points (<c>0..20</c>) across the 4 armor slots.</summary>
    public int TotalArmorDefense
    {
        get
        {
            var pts = 0;
            for (var a = 0; a < PlayerInventory.ArmorSlotCount; a++)
            {
                var st = SurvivalInventory.GetSlot(PlayerInventory.ArmorSlotStart + a);
                if (!st.IsEmpty) pts += _items.GetById(st.ItemId).ArmorDefense;
            }

            if (OffhandIsShield || SelectedIsShield) pts += 2;
            return Math.Clamp(pts, 0, 20);
        }
    }

    /// <summary>Builds a <see cref="PlayerArmorVisual"/> snapshot from the 4 equipped Armor slots (<c>36..39</c>).</summary>
    public PlayerArmorVisual BuildLocalArmorVisual() => new(
        HelmetTier: ResolveArmorTierCode(SurvivalInventory.GetSlot(PlayerInventory.ArmorSlotStart + 0)),
        ChestplateTier: ResolveArmorTierCode(SurvivalInventory.GetSlot(PlayerInventory.ArmorSlotStart + 1)),
        LeggingsTier: ResolveArmorTierCode(SurvivalInventory.GetSlot(PlayerInventory.ArmorSlotStart + 2)),
        BootsTier: ResolveArmorTierCode(SurvivalInventory.GetSlot(PlayerInventory.ArmorSlotStart + 3)));

    private byte ResolveArmorTierCode(ItemStack stack)
    {
        if (stack.IsEmpty) return 0;
        var path = _items.GetById(stack.ItemId).Key.Path;
        if (path.StartsWith("leather_", StringComparison.Ordinal)) return 1;
        if (path.StartsWith("iron_", StringComparison.Ordinal)) return 2;
        if (path.StartsWith("golden_", StringComparison.Ordinal)) return 3;
        return path.StartsWith("diamond_", StringComparison.Ordinal) ? (byte)4 : (byte)0;
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

    /// <summary>Swaps the currently selected main-hand slot with the offhand slot (<c>F</c> key).</summary>
    public void SwapMainHandAndOffhand()
    {
        if (ActiveMode == GameMode.Creative)
        {
            var oldOffhand = SurvivalInventory.OffhandItem;
            var curMainIdx = _creativeSlots[_selectedSlot];
            if (curMainIdx >= 0)
            {
                var mainId = (ushort)_items.GetId(Catalog[curMainIdx].Key);
                SurvivalInventory.OffhandItem = mainId > 0 ? new ItemStack(mainId, 1) : ItemStack.Empty;
            }
            else
            {
                SurvivalInventory.OffhandItem = ItemStack.Empty;
            }

            if (oldOffhand.IsEmpty)
            {
                _creativeSlots[_selectedSlot] = -1;
            }
            else
            {
                var offKey = _items.GetById(oldOffhand.ItemId).Key;
                _creativeSlots[_selectedSlot] = FindCatalogIndex(offKey);
            }
        }
        else
        {
            SurvivalInventory.SwapMainHandAndOffhand();
        }

        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Clears a Creative hotbar slot (used by the Creative Delete Item / Trash slot).</summary>
    public void ClearCreativeSlot(int hotbarSlot)
    {
        var clamped = Math.Clamp(hotbarSlot, 0, PlayerInventory.HotbarSlotCount - 1);
        _creativeSlots[clamped] = -1;
        SurvivalInventory.SetSlot(clamped, ItemStack.Empty);
        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Synchronizes <c>_creativeSlots[slot]</c> from <see cref="SurvivalInventory"/> after a GUI drag-and-drop click.</summary>
    public void SyncCreativeSlotFromInventory(int slotIndex)
    {
        if (slotIndex is < 0 or >= PlayerInventory.HotbarSlotCount) return;
        var st = SurvivalInventory.GetSlot(slotIndex);
        _creativeSlots[slotIndex] = st.IsEmpty ? -1 : FindCatalogIndex(_items.GetById(st.ItemId).Key);
    }

    /// <summary>Synchronizes Creative hotbar slots into <see cref="SurvivalInventory"/> when opening the Creative GUI.</summary>
    public void SyncInventoryFromCreativeSlots()
    {
        for (var i = 0; i < PlayerInventory.HotbarSlotCount; i++)
        {
            var cur = SurvivalInventory.GetSlot(i);
            var cIdx = _creativeSlots[i];
            if (cIdx < 0)
            {
                if (!cur.IsEmpty) SurvivalInventory.SetSlot(i, ItemStack.Empty);
                continue;
            }

            var key = Catalog[cIdx].Key;
            var rawId = _items.GetId(key);
            if (rawId > 0 && cur.ItemId != rawId)
            {
                SurvivalInventory.SetSlot(i, new ItemStack((ushort)rawId, 1));
            }
        }
    }

    /// <summary>Clears all 41 inventory slots (hotbar, storage, armor, offhand) and Creative quick-slots.</summary>
    public void ClearEntireInventory()
    {
        for (var i = 0; i < PlayerInventory.TotalSlotCount; i++)
        {
            SurvivalInventory.SetSlot(i, ItemStack.Empty);
        }

        if (ActiveMode == GameMode.Creative)
        {
            Array.Fill(_creativeSlots, -1);
        }

        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Finds the Creative Catalog index for <paramref name="key"/>, or <c>-1</c> if absent.</summary>
    public static int FindCatalogIndex(ResourceKey key)
    {
        for (var i = 0; i < Catalog.Length; i++)
        {
            if (Catalog[i].Key == key) return i;
        }

        return -1;
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
        var idx = FindCatalogIndex(key);
        return idx >= 0 ? Catalog[idx].Color : new Vector3(0.65f, 0.65f, 0.68f);
    }

    /// <summary>Builds a concise gameplay stats line for hover tooltips.</summary>
    public string BuildTooltipDetail(ResourceKey key)
    {
        if (!_items.TryGet(key, out var def)) return string.Empty;
        if (key == BuiltInItems.Shield) return "+2 Defense (Press F to hold in Offhand!)";
        if (def.ArmorDefense > 0) return $"+{def.ArmorDefense} Armor Defense (Equip in Armor Slot or Right-Click)";
        if (def.ToolType == ToolKind.Sword) return $"+{def.AttackDamage:F0} Melee Damage (Jump Crit: {def.AttackDamage * 1.5f:F1})";
        if (def.ToolType != ToolKind.None) return $"{def.ToolType} Tier {def.ToolTier} (Speed x{def.MiningSpeedMultiplier:F1}, +{def.AttackDamage:F1} Dmg)";
        if (def.HeldLightEmission > 0) return $"Emits Light Level {def.HeldLightEmission} (Dynamic Held + Offhand Illumination!)";

        return def.UseAction switch
        {
            ItemUseAction.EatFood => $"Food: +{def.FoodNutrition} Hunger, +{def.FoodSaturation:F1} Sat",
            ItemUseAction.WaterBucket => "Right-Click: Pour flowing Water source",
            ItemUseAction.EmptyBucket => "Right-Click: Scoop Water source",
            ItemUseAction.IgniteTnt => "Right-Click: Spark Fire or ignite physics TNT",
            ItemUseAction.DrawBow => "Hold Right-Click to draw & release 3D ballistic Arrows!",
            ItemUseAction.PlaceMinecart => "Right-Click on Rail / Powered Rail to place & ride!",
            ItemUseAction.PlaceBlock => "Placeable Voxel Block (Right-Click)",
            _ => key == BuiltInItems.Arrow ? "Ammunition for Bow (consumed in Survival)" : "Crafting & Smelting Material",
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
        var s = Math.Clamp(hotbarSlot, 0, PlayerInventory.HotbarSlotCount - 1);
        var p = Math.Clamp(paletteIndex, 0, Catalog.Length - 1);
        _creativeSlots[s] = p;
        var rawId = _items.GetId(Catalog[p].Key);
        if (rawId > 0)
        {
            SurvivalInventory.SetSlot(s, new ItemStack((ushort)rawId, 1));
        }

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
                var cIdx = _creativeSlots[i];
                if (cIdx < 0)
                {
                    _hotbarColors[i] = new Vector3(0.16f, 0.17f, 0.20f);
                    _hotbarTiles[i] = -1;
                    _hotbarCounts[i] = 0;
                }
                else
                {
                    var entry = Catalog[cIdx];
                    _hotbarColors[i] = entry.Color;
                    _hotbarTiles[i] = ResolveAtlasTile(entry.Key);
                    _hotbarCounts[i] = 1;
                }
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

    private static bool IsToolOrWeapon(ItemDefinition? item) =>
        item is { } def && def.Key != BuiltInItems.Shield &&
        (def.ToolType != ToolKind.None || def.Key == BuiltInItems.Stick || def.Key == BuiltInItems.Bone);

    private static bool IsTorchStyleKey(ResourceKey key) =>
        key == BuiltInBlocks.Torch || key == BuiltInBlocks.RedstoneTorch || key == BuiltInBlocks.Lantern || key == BuiltInBlocks.Lever;

    private static bool IsBlock3DCube(ItemDefinition? item) =>
        item?.PlacedBlockKey is { } bk &&
        !IsTorchStyleKey(bk) &&
        bk != BuiltInBlocks.Rail &&
        bk != BuiltInBlocks.PoweredRail &&
        bk != BuiltInBlocks.RedBed &&
        bk != BuiltInBlocks.Ladder &&
        bk != BuiltInBlocks.Cobweb &&
        bk != BuiltInBlocks.RedstoneWire &&
        bk != BuiltInBlocks.RedstoneRepeater;

    private int GetFaceTile(ItemDefinition? item, Direction face) =>
        _atlas is not null && item?.PlacedBlockKey is { } bk ? _atlas.GetTileIndex(bk, face) : -1;

    private void SetStarterSlot(int slot, ResourceKey key, int count)
    {
        var id = _items.GetId(key);
        if (id > 0) SurvivalInventory.SetSlot(slot, new ItemStack((ushort)id, count));
    }

    private ItemDefinition? GetSlotItem(int slot)
    {
        if (ActiveMode == GameMode.Creative)
        {
            var cIdx = _creativeSlots[slot];
            if (cIdx < 0) return null;
            return _items.TryGet(Catalog[cIdx].Key, out var def) ? def : null;
        }

        var stack = SurvivalInventory.GetSlot(slot);
        return stack.IsEmpty ? null : _items.GetById(stack.ItemId);
    }
}
