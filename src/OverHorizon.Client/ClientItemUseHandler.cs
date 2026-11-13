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
/// Handles right-click block/item interactions: Crafting Table &amp; Furnace opening, Redstone Lever/Button/Repeater interaction,
/// <c>RedBed</c> respawn &amp; night-skipping sleep, Sheep shearing, Hoe tilling, Armor quick-equipping, Food eating with saturation,
/// Water Bucket scooping/pouring, Flint &amp; Steel (<c>Fire</c> + physics <c>PrimedTnt</c>), and attachment-validated block placement
/// (including Offhand fallback!).
/// </summary>
internal sealed class ClientItemUseHandler
{
    private readonly IBlockRegistry _blocks;
    private readonly IRegistry<ItemDefinition> _items;
    private readonly IAudioSubsystem _audio;

    public ClientItemUseHandler(IBlockRegistry blocks, IRegistry<ItemDefinition> items, IAudioSubsystem audio)
    {
        _blocks = blocks;
        _items = items;
        _audio = audio;
    }

    public bool TryUseOrPlace(
        ClientPlayerController player,
        ClientWorldCache world,
        ClientWorldSyncAndMobBridge worldBridge,
        WorldMobManager mobManager,
        ClientChatHistory chatHistory,
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

            _audio.PlayPositional(SoundCue.MobSheepShear, player.Camera.Position, gain: 0.80f * player.MasterVolume);
            return true;
        }

        // 1. Targeted Interactive Blocks (Crafting Table, Furnace, Redstone Lever/Button/Repeater, Red Bed)
        if (player.CurrentTarget is { } targetHit)
        {
            var targetState = world.GetBlockState(targetHit.BlockPosition);
            var targetMeta = _blocks.GetStateMetadata(targetState);
            var clickedKey = targetMeta.BlockKey;

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

            if (clickedKey == BuiltInBlocks.Lever || clickedKey == BuiltInBlocks.StoneButton || clickedKey == BuiltInBlocks.RedstoneRepeater)
            {
                if (worldBridge.TryInteractRedstoneBlock(targetHit.BlockPosition))
                {
                    useCooldown = 0.16;
                    player.TriggerHandSwing();
                    _audio.PlayPositional(SoundCue.UiClick, new Vec3d(targetHit.BlockPosition.X + 0.5, targetHit.BlockPosition.Y + 0.5, targetHit.BlockPosition.Z + 0.5), gain: 0.65f * player.MasterVolume, pitch: 0.92f);
                    return true;
                }
            }

            if (clickedKey == BuiltInBlocks.RedBed)
            {
                useCooldown = 0.30;
                player.TriggerHandSwing();
                TrySleepInBed(player, worldBridge, mobManager, chatHistory, targetHit.BlockPosition);
                return true;
            }
        }

        // 2. Try Main Hand item first; if empty or passive (e.g. holding Sword while Torch is in Offhand!), fall back to Offhand!
        if (held is not null && TryExecuteHandItemUse(player, world, worldBridge, held, isOffhand: false, applyWorldBlockChange, ref useCooldown))
        {
            return true;
        }

        var offhandDef = player.Hotbar.OffhandItem;
        return offhandDef is not null && TryExecuteHandItemUse(player, world, worldBridge, offhandDef, isOffhand: true, applyWorldBlockChange, ref useCooldown);
    }

    private static void TrySleepInBed(
        ClientPlayerController player,
        ClientWorldSyncAndMobBridge worldBridge,
        WorldMobManager mobManager,
        ClientChatHistory chatHistory,
        BlockPos bedPos)
    {
        var wakePos = new Vec3d(bedPos.X + 0.5, bedPos.Y + 0.65, bedPos.Z + 0.5);
        player.SpawnPosition = wakePos;
        chatHistory.AddMessage("[Bed] Respawn point set.");

        if (!worldBridge.TimeCycle.IsNightForSleeping)
        {
            chatHistory.AddMessage("[Bed] You can sleep only at night.");
            return;
        }

        if (player.Mode == GameMode.Survival && mobManager.HasHostileMobNear(wakePos))
        {
            chatHistory.AddMessage("[Bed] You may not rest now; there are monsters nearby.");
            return;
        }

        worldBridge.TimeCycle.SkipNightToMorning();
        chatHistory.AddMessage("[Bed] Good morning! Slept through the night.");
    }

    private bool TryExecuteHandItemUse(
        ClientPlayerController player,
        ClientWorldCache world,
        ClientWorldSyncAndMobBridge worldBridge,
        ItemDefinition itemDef,
        bool isOffhand,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange,
        ref double useCooldown)
    {
        var invSlot = isOffhand ? PlayerInventory.OffhandSlotIndex : player.Hotbar.SelectedSlot;

        switch (itemDef.UseAction)
        {
            case ItemUseAction.TillSoil:
                if (player.CurrentTarget is not { } hoeHit) return false;
                var soilKey = _blocks.GetStateMetadata(world.GetBlockState(hoeHit.BlockPosition)).BlockKey;
                if (soilKey != BuiltInBlocks.GrassBlock && soilKey != BuiltInBlocks.Dirt) return false;
                useCooldown = 0.20;
                player.TriggerHandSwing();
                return applyWorldBlockChange(hoeHit.BlockPosition, hoeHit.HitFace, _blocks.GetDefaultState(BuiltInBlocks.Farmland));

            case ItemUseAction.EquipArmor:
                return TryQuickEquipArmor(player, itemDef, invSlot, ref useCooldown);

            case ItemUseAction.EatFood:
                if (player.Mode == GameMode.Survival && player.Hunger >= 20 && itemDef.BonusHealPoints <= 0.0f) return false;
                useCooldown = 0.35;
                player.TriggerHandSwing();
                player.ConsumeFood(itemDef.FoodNutrition, itemDef.BonusHealPoints, itemDef.FoodNutrition * 1.2f);
                _audio.PlayPositional(SoundCue.EatFood, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                _audio.PlayPositional(SoundCue.Burp, player.Camera.Position, gain: 0.55f * player.MasterVolume);
                ConsumeOneFromSlotInSurvival(player, invSlot);
                return true;

            case ItemUseAction.WaterBucket:
                if (player.CurrentTarget is not { } waterHit) return false;
                useCooldown = 0.22;
                player.TriggerHandSwing();
                _ = applyWorldBlockChange(waterHit.AdjacentPosition, waterHit.HitFace, _blocks.GetDefaultState(BuiltInBlocks.Water));
                _audio.PlayPositional(SoundCue.WaterSplash, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                ReplaceSlotItemInSurvival(player, invSlot, BuiltInItems.Bucket);
                return true;

            case ItemUseAction.EmptyBucket:
                if (!TryFindFluidBlockAlongRay(player, world, out var fluidPos)) return false;
                useCooldown = 0.22;
                player.TriggerHandSwing();
                _ = applyWorldBlockChange(fluidPos, Direction.Up, BlockState.Air);
                _audio.PlayPositional(SoundCue.WaterSplash, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                ReplaceSlotItemInSurvival(player, invSlot, BuiltInItems.WaterBucket);
                return true;

            case ItemUseAction.IgniteTnt:
                return TryUseFlintAndSteel(player, world, worldBridge, applyWorldBlockChange, ref useCooldown);

            case ItemUseAction.PlaceBlock:
                if (itemDef.PlacedBlockKey is not { } placeKey || player.CurrentTarget is not { } placeHit) return false;
                var placePos = placeHit.AdjacentPosition;
                if (!BlockAttachmentAndShapeRules.TryResolvePlacementState(
                    _blocks, world.GetBlockState, placeKey, placePos, placeHit.HitFace, player.Camera.Forward, out var resolvedState, out var secondaryBlock))
                {
                    return false;
                }

                if (_blocks.GetRequired(placeKey).IsSolid)
                {
                    var playerBox = BoundingBox.FromEntityFeet(
                        player.FeetPosition, ClientPlayerController.PlayerWidth, ClientPlayerController.PlayerHeight).Inflate(-1e-4, -1e-4, -1e-4);
                    if (playerBox.Intersects(BoundingBox.FromBlock(placePos))) return false;
                    if (secondaryBlock is { } sec && playerBox.Intersects(BoundingBox.FromBlock(sec.Pos))) return false;
                }

                useCooldown = 0.14;
                player.TriggerHandSwing();
                if (!applyWorldBlockChange(placePos, placeHit.HitFace, resolvedState)) return false;
                if (secondaryBlock is { } secPlace)
                {
                    _ = applyWorldBlockChange(secPlace.Pos, placeHit.HitFace, secPlace.State);
                }

                ConsumeOneFromSlotInSurvival(player, invSlot);
                return true;

            default:
                return false;
        }
    }

    private void ReplaceSlotItemInSurvival(ClientPlayerController player, int slotIndex, ResourceKey replacementKey)
    {
        if (player.Mode != GameMode.Survival) return;
        var id = _items.GetId(replacementKey);
        if (id > 0)
        {
            player.Hotbar.SurvivalInventory.SetSlot(slotIndex, new ItemStack((ushort)id, 1));
            player.Hotbar.RefreshSlotVisuals(player.Mode);
        }
    }

    private bool TryQuickEquipArmor(ClientPlayerController player, ItemDefinition held, int sourceSlot, ref double useCooldown)
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
        var armorSlotIdx = PlayerInventory.ArmorSlotStart + sub;
        var inv = player.Hotbar.SurvivalInventory;
        var previousArmor = inv.GetSlot(armorSlotIdx);
        inv.SetSlot(armorSlotIdx, new ItemStack(heldItemId, 1));
        if (player.Mode == GameMode.Survival)
        {
            inv.SetSlot(sourceSlot, previousArmor);
            player.Hotbar.RefreshSlotVisuals(player.Mode);
        }

        _audio.PlayPositional(SoundCue.ItemPop, player.Camera.Position, gain: 0.65f * player.MasterVolume);
        return true;
    }

    private static void ConsumeOneFromSlotInSurvival(ClientPlayerController player, int slotIndex)
    {
        if (player.Mode != GameMode.Survival) return;
        var cur = player.Hotbar.SurvivalInventory.GetSlot(slotIndex);
        if (!cur.IsEmpty)
        {
            player.Hotbar.SurvivalInventory.SetSlot(slotIndex, cur.WithCount(cur.Count - 1));
            player.Hotbar.RefreshSlotVisuals(player.Mode);
        }
    }

    private bool TryUseFlintAndSteel(
        ClientPlayerController player,
        ClientWorldCache world,
        ClientWorldSyncAndMobBridge worldBridge,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange,
        ref double useCooldown)
    {
        if (player.CurrentTarget is not { } hit) return false;
        var targetKey = _blocks.GetStateMetadata(world.GetBlockState(hit.BlockPosition)).BlockKey;
        var hitCenter = new Vec3d(hit.BlockPosition.X + 0.5, hit.BlockPosition.Y + 0.5, hit.BlockPosition.Z + 0.5);

        // Ignite TNT into live physics-simulated flashing PrimedTntEntity!
        if (targetKey == BuiltInBlocks.Tnt)
        {
            useCooldown = 0.25;
            player.TriggerHandSwing();
            _ = applyWorldBlockChange(hit.BlockPosition, Direction.Up, BlockState.Air);
            _ = worldBridge.WorldTicker.SpawnPrimedTnt(hit.BlockPosition);
            _audio.PlayPositional(SoundCue.FireIgnite, hitCenter, gain: 0.90f * player.MasterVolume);
            _audio.PlayPositional(SoundCue.TntFuse, hitCenter, gain: 0.95f * player.MasterVolume);
            return true;
        }

        // Place Fire on the clicked adjacent block if empty!
        var firePos = hit.AdjacentPosition;
        if (world.GetBlockState(firePos).IsAir)
        {
            useCooldown = 0.20;
            player.TriggerHandSwing();
            var ok = applyWorldBlockChange(firePos, hit.HitFace, _blocks.GetDefaultState(BuiltInBlocks.Fire));
            if (ok)
            {
                _audio.PlayPositional(SoundCue.FireIgnite, hitCenter, gain: 0.85f * player.MasterVolume);
            }

            return ok;
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
