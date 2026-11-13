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
/// Manages the unified 41-slot <see cref="PlayerInventory"/> (9 hotbar + 27 storage + 4 armor + 1 offhand)
/// shared seamlessly across Creative, Survival, and Spectator modes, plus Middle-Click Pick Block,
/// Bow arrow consumption, death item drop extraction, and Creative Catalog metadata.
/// </summary>
public sealed partial class ClientHotbar
{
    private readonly IBlockRegistry _blocks;
    private readonly IRegistry<ItemDefinition> _items;
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
        SeedInitialCreativeHotbar();
        RefreshSlotVisuals(GameMode.Creative);
    }

    /// <summary>Gets the total number of blocks and items in the Creative Catalog.</summary>
    public static int PaletteCount => Catalog.Length;

    /// <summary>Gets the number of hotbar slots (<c>9</c>).</summary>
    public int SlotCount => _hotbarColors.Length;

    /// <summary>Gets the active zero-based hotbar slot index (<c>0..8</c>).</summary>
    public int SelectedSlot => _selectedSlot;

    /// <summary>Gets the single unified 41-slot player inventory shared across all game modes.</summary>
    public PlayerInventory SurvivalInventory { get; }

    /// <summary>Gets the active <see cref="GameMode"/> mirrored into hotbar visuals.</summary>
    public GameMode ActiveMode { get; private set; } = GameMode.Creative;

    /// <summary>Gets the frozen item registry associated with this hotbar.</summary>
    public IRegistry<ItemDefinition> Items => _items;

    /// <summary>Gets the currently held main-hand <see cref="ItemDefinition"/>, or <see langword="null"/> when empty-handed.</summary>
    public ItemDefinition? SelectedItem
    {
        get
        {
            var st = SurvivalInventory.GetSlot(_selectedSlot);
            return st.IsEmpty ? null : _items.GetById(st.ItemId);
        }
    }

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
    public string SelectedBlockDisplayName
    {
        get
        {
            if (SelectedItem is not { } def) return string.Empty;
            var idx = FindCatalogIndex(def.Key);
            return idx >= 0 ? Catalog[idx].DisplayName : def.EffectiveName;
        }
    }

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
        SurvivalInventory.SelectedHotbarSlot = _selectedSlot;
        SurvivalInventory.SwapMainHandAndOffhand();
        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Clears a hotbar slot (used by the Creative Delete Item / Trash slot).</summary>
    public void ClearCreativeSlot(int hotbarSlot)
    {
        var clamped = Math.Clamp(hotbarSlot, 0, PlayerInventory.HotbarSlotCount - 1);
        SurvivalInventory.SetSlot(clamped, ItemStack.Empty);
        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Refreshes unified hotbar visuals after an inventory slot mutation.</summary>
    public void SyncCreativeSlotFromInventory(int slotIndex)
    {
        _ = slotIndex;
        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Refreshes unified hotbar visuals when opening the Creative GUI.</summary>
    public void SyncInventoryFromCreativeSlots() => RefreshSlotVisuals(ActiveMode);

    /// <summary>Clears all 41 unified inventory slots.</summary>
    public void ClearEntireInventory()
    {
        SurvivalInventory.Clear();
        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Extracts all non-empty stacks for 3D death drops and leaves the inventory empty (never resetting to Creative blocks).</summary>
    public List<ItemStack> ExtractAndClearInventoryForDeathDrop()
    {
        var drops = new List<ItemStack>(16);
        for (var i = 0; i < PlayerInventory.TotalSlotCount; i++)
        {
            var st = SurvivalInventory.GetSlot(i);
            if (!st.IsEmpty)
            {
                drops.Add(st);
                SurvivalInventory.SetSlot(i, ItemStack.Empty);
            }
        }

        RefreshSlotVisuals(ActiveMode);
        return drops;
    }

    /// <summary>Middle-Click Pick Block: selects matching hotbar slot, swaps from storage, or spawns in Creative.</summary>
    public bool TryPickBlock(ResourceKey targetItemKey, GameMode mode)
    {
        var rawId = _items.GetId(targetItemKey);
        if (rawId <= 0 || targetItemKey == BuiltInBlocks.Air)
        {
            return false;
        }

        var itemId = (ushort)rawId;
        for (var h = 0; h < PlayerInventory.HotbarSlotCount; h++)
        {
            if (SurvivalInventory.GetSlot(h).ItemId == itemId)
            {
                SelectSlot(h);
                RefreshSlotVisuals(mode);
                return true;
            }
        }

        for (var s = PlayerInventory.HotbarSlotCount; s < PlayerInventory.MainSlotCount; s++)
        {
            var st = SurvivalInventory.GetSlot(s);
            if (st.ItemId == itemId)
            {
                var cur = SurvivalInventory.GetSlot(_selectedSlot);
                SurvivalInventory.SetSlot(_selectedSlot, st);
                SurvivalInventory.SetSlot(s, cur);
                RefreshSlotVisuals(mode);
                return true;
            }
        }

        if (mode != GameMode.Creative)
        {
            return false;
        }

        if (!SurvivalInventory.GetSlot(_selectedSlot).IsEmpty)
        {
            for (var h = 0; h < PlayerInventory.HotbarSlotCount; h++)
            {
                if (SurvivalInventory.GetSlot(h).IsEmpty)
                {
                    SelectSlot(h);
                    break;
                }
            }
        }

        SurvivalInventory.SetSlot(_selectedSlot, new ItemStack(itemId, 1));
        RefreshSlotVisuals(mode);
        return true;
    }

    /// <summary>Returns <see langword="true"/> if the player can draw a Bow (Creative or owns at least one Arrow).</summary>
    public bool HasArrowForBow(GameMode mode)
    {
        if (mode == GameMode.Creative) return true;
        var rawId = _items.GetId(BuiltInItems.Arrow);
        if (rawId <= 0) return false;
        var arrowId = (ushort)rawId;
        if (SurvivalInventory.OffhandItem.ItemId == arrowId) return true;
        for (var i = 0; i < PlayerInventory.MainSlotCount; i++)
        {
            if (SurvivalInventory.GetSlot(i).ItemId == arrowId) return true;
        }

        return false;
    }

    /// <summary>Consumes 1 Arrow from Offhand or main inventory in Survival (infinite in Creative).</summary>
    public bool TryConsumeArrowForBow(GameMode mode)
    {
        if (mode == GameMode.Creative) return true;
        var rawId = _items.GetId(BuiltInItems.Arrow);
        if (rawId <= 0) return false;
        var arrowId = (ushort)rawId;

        var off = SurvivalInventory.OffhandItem;
        if (off.ItemId == arrowId && off.Count > 0)
        {
            SurvivalInventory.OffhandItem = off.Count > 1 ? new ItemStack(arrowId, off.Count - 1) : ItemStack.Empty;
            RefreshSlotVisuals(mode);
            return true;
        }

        for (var i = 0; i < PlayerInventory.MainSlotCount; i++)
        {
            var st = SurvivalInventory.GetSlot(i);
            if (st.ItemId == arrowId && st.Count > 0)
            {
                SurvivalInventory.SetSlot(i, st.Count > 1 ? new ItemStack(arrowId, st.Count - 1) : ItemStack.Empty);
                RefreshSlotVisuals(mode);
                return true;
            }
        }

        return false;
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
        var rawId = _items.GetId(Catalog[p].Key);
        if (rawId > 0)
        {
            SurvivalInventory.SetSlot(s, new ItemStack((ushort)rawId, 1));
        }

        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Equips a Creative Catalog item into the currently selected hotbar slot.</summary>
    public void EquipPaletteBlock(int paletteIndex) => EquipPaletteEntryInSlot(paletteIndex, _selectedSlot);
}
