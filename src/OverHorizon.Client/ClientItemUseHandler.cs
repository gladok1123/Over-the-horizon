using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Handles right-click block/item interactions: Crafting Table &amp; Furnace opening, Sheep shearing,
/// Hoe soil tilling, Armor quick-equipping, Food eating, Water Bucket scooping/pouring, Flint &amp; Steel, and block placement.
/// </summary>
internal sealed class ClientItemUseHandler
{
    private readonly IBlockRegistry _blocks;
    private readonly IRegistry<ItemDefinition> _items;
    private readonly IAudioSubsystem _audio;
    private int _tntDropSeq;

    public ClientItemUseHandler(IBlockRegistry blocks, IRegistry<ItemDefinition> items, IAudioSubsystem audio)
    {
        _blocks = blocks;
        _items = items;
        _audio = audio;
    }

    public bool TryUseOrPlace(
        ClientPlayerController player,
        ClientWorldCache world,
        WorldMobManager mobManager,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange,
        ref double useCooldown,
        out UiOverlayMode? openContainerMode,
        out BlockPos? openedContainerPos)
    {
        openContainerMode = null;
        openedContainerPos = null;

        var held = player.Hotbar.SelectedItem;
        if (held is { ToolType: ToolKind.Shears } &&
            mobManager.TryShearAlongRay(player.Camera.Position, player.Camera.Forward, 3.8, _items, out var woolDrop))
        {
            useCooldown = 0.25;
            player.TriggerHandSwing();
            if (!woolDrop.IsEmpty && player.Mode == GameMode.Survival)
            {
                _ = player.Hotbar.SurvivalInventory.AddItem(woolDrop, _items);
                player.Hotbar.RefreshSlotVisuals(player.Mode);
            }

            _audio.PlayPositional(SoundCue.ItemPop, player.Camera.Position, gain: 0.65f * player.MasterVolume);
            return true;
        }

        if (player.CurrentTarget is { } targetHit)
        {
            var clickedKey = _blocks.GetStateMetadata(world.GetBlockState(targetHit.BlockPosition)).BlockKey;
            if (clickedKey == BuiltInBlocks.CraftingTable)
            {
                openContainerMode = UiOverlayMode.CraftingTableScreen;
                openedContainerPos = targetHit.BlockPosition;
                useCooldown = 0.20;
                return true;
            }

            if (clickedKey == BuiltInBlocks.Furnace || clickedKey == BuiltInBlocks.LitFurnace)
            {
                openContainerMode = UiOverlayMode.FurnaceScreen;
                openedContainerPos = targetHit.BlockPosition;
                useCooldown = 0.20;
                return true;
            }
        }

        if (held is null) return false;

        switch (held.UseAction)
        {
            case ItemUseAction.TillSoil:
                if (player.CurrentTarget is not { } hoeHit) return false;
                var soilKey = _blocks.GetStateMetadata(world.GetBlockState(hoeHit.BlockPosition)).BlockKey;
                if (soilKey != BuiltInBlocks.GrassBlock && soilKey != BuiltInBlocks.Dirt) return false;
                useCooldown = 0.20;
                player.TriggerHandSwing();
                return applyWorldBlockChange(hoeHit.BlockPosition, hoeHit.HitFace, _blocks.GetDefaultState(BuiltInBlocks.Farmland));

            case ItemUseAction.EquipArmor:
                return TryQuickEquipArmor(player, held, ref useCooldown);

            case ItemUseAction.EatFood:
                if (player.Mode == GameMode.Survival && player.Hunger >= 20 && held.BonusHealPoints <= 0.0f) return false;
                useCooldown = 0.35;
                player.TriggerHandSwing();
                player.ConsumeFood(held.FoodNutrition, Math.Max(1.0f, held.BonusHealPoints));
                _audio.PlayPositional(SoundCue.EatFood, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                ConsumeOneSelectedInSurvival(player);
                return true;

            case ItemUseAction.WaterBucket:
                if (player.CurrentTarget is not { } waterHit) return false;
                useCooldown = 0.22;
                player.TriggerHandSwing();
                _ = applyWorldBlockChange(waterHit.AdjacentPosition, waterHit.HitFace, _blocks.GetDefaultState(BuiltInBlocks.Water));
                _audio.PlayPositional(SoundCue.WaterSplash, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                ReplaceBucketInSurvival(player, BuiltInItems.Bucket);
                return true;

            case ItemUseAction.EmptyBucket:
                if (!TryFindFluidBlockAlongRay(player, world, out var fluidPos)) return false;
                useCooldown = 0.22;
                player.TriggerHandSwing();
                _ = applyWorldBlockChange(fluidPos, Direction.Up, BlockState.Air);
                _audio.PlayPositional(SoundCue.WaterSplash, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                ReplaceBucketInSurvival(player, BuiltInItems.WaterBucket);
                return true;

            case ItemUseAction.IgniteTnt:
                return TryUseFlintAndSteel(player, world, applyWorldBlockChange, ref useCooldown);

            case ItemUseAction.PlaceBlock:
                if (player.CurrentTarget is not { } placeHit) return false;
                var stateToPlace = player.Hotbar.SelectedBlockState;
                if (stateToPlace.IsAir) return false;
                var placePos = placeHit.AdjacentPosition;
                var placeKey = _blocks.GetStateMetadata(stateToPlace).BlockKey;
                if (_blocks.GetRequired(placeKey).IsSolid)
                {
                    var playerBox = BoundingBox.FromEntityFeet(
                        player.FeetPosition, ClientPlayerController.PlayerWidth, ClientPlayerController.PlayerHeight).Inflate(-1e-4, -1e-4, -1e-4);
                    if (playerBox.Intersects(BoundingBox.FromBlock(placePos))) return false;
                }

                useCooldown = 0.14;
                player.TriggerHandSwing();
                if (!applyWorldBlockChange(placePos, placeHit.HitFace, stateToPlace)) return false;
                ConsumeOneSelectedInSurvival(player);
                return true;

            default:
                return false;
        }
    }

    private void ReplaceBucketInSurvival(ClientPlayerController player, ResourceKey replacementKey)
    {
        if (player.Mode != GameMode.Survival) return;
        var id = _items.GetId(replacementKey);
        if (id > 0)
        {
            player.Hotbar.SurvivalInventory.SetSlot(player.Hotbar.SelectedSlot, new ItemStack((ushort)id, 1));
            player.Hotbar.RefreshSlotVisuals(player.Mode);
        }
    }

    private bool TryQuickEquipArmor(ClientPlayerController player, ItemDefinition held, ref double useCooldown)
    {
        var path = held.Key.Path;
        var sub = -1;
        if (path.EndsWith("helmet", StringComparison.Ordinal)) sub = 0;
        else if (path.EndsWith("chestplate", StringComparison.Ordinal)) sub = 1;
        else if (path.EndsWith("leggings", StringComparison.Ordinal)) sub = 2;
        else if (path.EndsWith("boots", StringComparison.Ordinal)) sub = 3;
        if (sub < 0) return false;

        var heldItemId = (ushort)Math.Max(0, _items.GetId(held.Key));
        if (heldItemId == 0) return false;

        useCooldown = 0.22;
        var armorSlotIdx = PlayerInventory.ArmorSlotOffset + sub;
        var inv = player.Hotbar.SurvivalInventory;
        var previousArmor = inv.GetSlot(armorSlotIdx);
        inv.SetSlot(armorSlotIdx, new ItemStack(heldItemId, 1));
        if (player.Mode == GameMode.Survival)
        {
            inv.SetSlot(player.Hotbar.SelectedSlot, previousArmor);
            player.Hotbar.RefreshSlotVisuals(player.Mode);
        }

        _audio.PlayPositional(SoundCue.ItemPop, player.Camera.Position, gain: 0.65f * player.MasterVolume);
        return true;
    }

    private static void ConsumeOneSelectedInSurvival(ClientPlayerController player)
    {
        if (player.Mode != GameMode.Survival) return;
        var slot = player.Hotbar.SelectedSlot;
        var cur = player.Hotbar.SurvivalInventory.GetSlot(slot);
        if (!cur.IsEmpty)
        {
            player.Hotbar.SurvivalInventory.SetSlot(slot, cur.WithCount(cur.Count - 1));
            player.Hotbar.RefreshSlotVisuals(player.Mode);
        }
    }

    private bool TryUseFlintAndSteel(ClientPlayerController player, ClientWorldCache world, Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange, ref double useCooldown)
    {
        if (player.CurrentTarget is not { } hit) return false;
        var targetKey = _blocks.GetStateMetadata(world.GetBlockState(hit.BlockPosition)).BlockKey;
        if (targetKey == BuiltInBlocks.Tnt)
        {
            useCooldown = 0.35;
            player.TriggerHandSwing();
            _audio.PlayPositional(SoundCue.PlayerHit, player.Camera.Position, gain: 1.0f * player.MasterVolume, pitch: 0.72f);
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        var p = new BlockPos(hit.BlockPosition.X + dx, hit.BlockPosition.Y + dy, hit.BlockPosition.Z + dz);
                        var k = _blocks.GetStateMetadata(world.GetBlockState(p)).BlockKey;
                        var def = _blocks.GetRequired(k);
                        if (k != BuiltInBlocks.Air && def.Hardness >= 0.0f && def.Hardness < 10.0f)
                        {
                            var drop = BlockDropResolver.ResolveDrop(k, heldTool: null, _items, _tntDropSeq++);
                            if (player.Mode == GameMode.Survival && !drop.IsEmpty) _ = player.Hotbar.SurvivalInventory.AddItem(drop, _items);
                            _ = applyWorldBlockChange(p, Direction.Up, BlockState.Air);
                        }
                    }
                }
            }

            player.Hotbar.RefreshSlotVisuals(player.Mode);
            return true;
        }

        if (targetKey == BuiltInBlocks.Sand)
        {
            useCooldown = 0.22;
            player.TriggerHandSwing();
            return applyWorldBlockChange(hit.BlockPosition, hit.HitFace, _blocks.GetDefaultState(BuiltInBlocks.Glass));
        }

        if (TryFindFluidBlockAlongRay(player, world, out var waterPos))
        {
            useCooldown = 0.22;
            player.TriggerHandSwing();
            return applyWorldBlockChange(waterPos, Direction.Up, _blocks.GetDefaultState(BuiltInBlocks.Obsidian));
        }

        return false;
    }

    private bool TryFindFluidBlockAlongRay(ClientPlayerController player, ClientWorldCache world, out BlockPos fluidPos)
    {
        var origin = player.Camera.Position;
        var f = player.Camera.Forward;
        for (var step = 1; step <= 26; step++)
        {
            var dist = step * 0.22;
            var sample = new Vec3d(origin.X + (f.X * dist), origin.Y + (f.Y * dist), origin.Z + (f.Z * dist)).ToBlockPos();
            var def = _blocks.GetRequired(_blocks.GetStateMetadata(world.GetBlockState(sample)).BlockKey);
            if (def.IsFluid)
            {
                fluidPos = sample;
                return true;
            }

            if (def.IsSolid) break;
        }

        fluidPos = default;
        return false;
    }
}
