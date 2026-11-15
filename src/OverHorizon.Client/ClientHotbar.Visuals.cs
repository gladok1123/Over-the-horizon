using System.Numerics;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Inventory;

namespace OverHorizon.Client;

/// <summary>
/// Starter inventory population, visual hotbar synchronization, and 3D held-item classification helpers for <see cref="ClientHotbar"/>.
/// </summary>
public sealed partial class ClientHotbar
{
    /// <summary>Seeds initial Creative hotbar slots <c>0..8</c> on brand-new construction.</summary>
    public void SeedInitialCreativeHotbar()
    {
        SurvivalInventory.Clear();
        for (var i = 0; i < PlayerInventory.HotbarSlotCount && i < Catalog.Length; i++)
        {
            SetStarterSlot(i, Catalog[i].Key, 1);
        }
    }

    /// <summary>Populates starter Survival gear for a brand-new Survival world.</summary>
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
        RefreshSlotVisuals(ActiveMode);
    }

    /// <summary>Synchronizes hotbar sprite tiles, counts, and fallback colors from the single unified <see cref="SurvivalInventory"/>.</summary>
    public void RefreshSlotVisuals(GameMode mode)
    {
        ActiveMode = mode;
        for (var i = 0; i < PlayerInventory.HotbarSlotCount; i++)
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

    private byte ResolveArmorTierCode(ItemStack stack)
    {
        if (stack.IsEmpty) return 0;
        var path = _items.GetById(stack.ItemId).Key.Path;
        if (path.StartsWith("leather_", StringComparison.Ordinal)) return 1;
        if (path.StartsWith("iron_", StringComparison.Ordinal)) return 2;
        if (path.StartsWith("golden_", StringComparison.Ordinal)) return 3;
        return path.StartsWith("diamond_", StringComparison.Ordinal) ? (byte)4 : (byte)0;
    }

    private static bool IsToolOrWeapon(ItemDefinition? item) =>
        item is { } def && def.Key != BuiltInItems.Shield &&
        (def.ToolType != ToolKind.None || def.Key == BuiltInItems.Stick || def.Key == BuiltInItems.Bone);

    private static bool IsTorchStyleKey(ResourceKey key) =>
        key == BuiltInBlocks.Torch || key == BuiltInBlocks.RedstoneTorch || key == BuiltInBlocks.Lantern || key == BuiltInBlocks.Lever;

    private static bool IsBlock3DCube(ItemDefinition? item) =>
        item?.PlacedBlockKey is { } bk &&
        !IsTorchStyleKey(bk) &&
        bk != BuiltInBlocks.OakFence &&
        bk != BuiltInBlocks.Rail &&
        bk != BuiltInBlocks.PoweredRail &&
        bk != BuiltInBlocks.RedBed &&
        bk != BuiltInBlocks.Ladder &&
        bk != BuiltInBlocks.Cobweb &&
        bk != BuiltInBlocks.RedstoneWire &&
        bk != BuiltInBlocks.RedstoneRepeater &&
        bk != BuiltInBlocks.RedstoneComparator &&
        bk != BuiltInBlocks.OakTrapdoor &&
        bk != BuiltInBlocks.IronTrapdoor &&
        bk != BuiltInBlocks.OakDoor &&
        bk != BuiltInBlocks.IronDoor &&
        bk != BuiltInBlocks.AmethystCluster;

    private int GetFaceTile(ItemDefinition? item, Direction face) =>
        _atlas is not null && item?.PlacedBlockKey is { } bk ? _atlas.GetTileIndex(bk, face) : -1;

    private void SetStarterSlot(int slot, ResourceKey key, int count)
    {
        var id = _items.GetId(key);
        if (id > 0) SurvivalInventory.SetSlot(slot, new ItemStack((ushort)id, count));
    }
}
