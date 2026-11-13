using System.Globalization;
using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Effects;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Handles right-click block/item interactions: Crafting Table &amp; Furnace, Redstone Lever/Button/Repeater/Comparator/Trapdoor,
/// Composter composting &amp; Bone Meal harvest, Spawn Eggs, Potions &amp; Golden Apple status effects, Offhand Left-Arm swing,
/// Rideable Minecarts (SP + MP), Flint &amp; Steel, and attachment-validated block placement.
/// </summary>
internal sealed partial class ClientItemUseHandler
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
        ClientMultiplayerTransport transport,
        ClientChatHistory chatHistory,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange,
        ref double useCooldown,
        out UiOverlayMode? openContainerMode,
        out BlockPos? openedContainerPos)
    {
        openContainerMode = null;
        openedContainerPos = null;

        var f = player.Camera.Forward;
        var forwardVec = new Vec3d(f.X, f.Y, f.Z);
        var held = player.Hotbar.SelectedItem;

        // 0. Mount Minecart along look ray (Singleplayer & Multiplayer!)
        if (player.RidingMinecartId < 0)
        {
            if (transport.IsConnected)
            {
                if (TryHitMultiplayerMinecart(player.Camera.Position, forwardVec, 4.2, worldBridge))
                {
                    useCooldown = 0.25;
                    transport.EnqueueOutbound(new PlayerWorldInteractionPacket(
                        PlayerWorldInteractionKind.MountOrDismountMinecart, player.Camera.Position, forwardVec, 0, 1));
                    return true;
                }
            }
            else if (worldBridge.Minecarts.TryMountAlongRay(player.Camera.Position, f, 4.2) is { } mountedCart)
            {
                useCooldown = 0.25;
                player.MountMinecart(mountedCart.Id);
                _audio.PlayPositional(SoundCue.MinecartRoll, mountedCart.FeetPosition, gain: 0.55f * player.MasterVolume);
                chatHistory.AddMessage("[Minecart] Mounted! Press W/S to steer, Left-Shift or C to dismount.");
                return true;
            }
        }

        if (!transport.IsConnected &&
            held is { ToolType: ToolKind.Shears } &&
            mobManager.TryShearAlongRay(player.Camera.Position, f, 3.8, _items, out var woolDrop))
        {
            useCooldown = 0.25;
            player.TriggerHandSwing(isOffhand: false);
            if (!woolDrop.IsEmpty)
            {
                _ = worldBridge.DroppedItems.SpawnScatterDrop(player.FeetPosition + (forwardVec * 1.4), woolDrop, 0.3f);
            }

            _audio.PlayPositional(SoundCue.MobSheepShear, player.Camera.Position, gain: 0.80f * player.MasterVolume);
            return true;
        }

        // 1. Targeted Interactive Blocks (Crafting Table, Furnace, Redstone Lever/Button/Repeater/Comparator/Trapdoor, Composter, Bed)
        if (player.CurrentTarget is { } targetHit && !player.IsSneaking)
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

            if (clickedKey == BuiltInBlocks.Lever ||
                clickedKey == BuiltInBlocks.StoneButton ||
                clickedKey == BuiltInBlocks.RedstoneRepeater ||
                clickedKey == BuiltInBlocks.RedstoneComparator ||
                clickedKey == BuiltInBlocks.OakTrapdoor)
            {
                useCooldown = 0.16;
                player.TriggerHandSwing(isOffhand: false);
                var center = new Vec3d(targetHit.BlockPosition.X + 0.5, targetHit.BlockPosition.Y + 0.5, targetHit.BlockPosition.Z + 0.5);
                if (transport.IsConnected)
                {
                    transport.EnqueueOutbound(new PlayerWorldInteractionPacket(
                        PlayerWorldInteractionKind.InteractWorldComponent, center, Vec3d.Zero, 0, 0));
                    _audio.PlayPositional(clickedKey == BuiltInBlocks.OakTrapdoor ? SoundCue.WoodPlace : SoundCue.UiClick, center, gain: 0.68f * player.MasterVolume);
                    return true;
                }

                if (worldBridge.TryInteractRedstoneBlock(targetHit.BlockPosition))
                {
                    return true;
                }
            }

            if (clickedKey == BuiltInBlocks.Composter &&
                TryInteractComposter(player, world, worldBridge, transport, targetHit.BlockPosition, targetMeta, applyWorldBlockChange, ref useCooldown))
            {
                return true;
            }

            if (clickedKey == BuiltInBlocks.RedBed)
            {
                useCooldown = 0.30;
                player.TriggerHandSwing(isOffhand: false);
                TrySleepInBed(player, worldBridge, mobManager, chatHistory, targetHit.BlockPosition);
                return true;
            }
        }

        // 2. Main Hand first; if empty or passive (e.g. holding Sword while Torch/Food/Block is in Offhand), use Offhand & swing LEFT arm!
        if (held is not null &&
            TryExecuteHandItemUse(player, world, worldBridge, mobManager, transport, held, isOffhand: false, applyWorldBlockChange, ref useCooldown))
        {
            return true;
        }

        var offhandDef = player.Hotbar.OffhandItem;
        return offhandDef is not null &&
               TryExecuteHandItemUse(player, world, worldBridge, mobManager, transport, offhandDef, isOffhand: true, applyWorldBlockChange, ref useCooldown);
    }

    private bool TryInteractComposter(
        ClientPlayerController player,
        ClientWorldCache world,
        ClientWorldSyncAndMobBridge worldBridge,
        ClientMultiplayerTransport transport,
        BlockPos pos,
        BlockStateMetadata meta,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange,
        ref double useCooldown)
    {
        _ = world;
        var level = meta.Properties.TryGetValue("level", out var lv) && int.TryParse(lv, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var center = new Vec3d(pos.X + 0.5, pos.Y + 0.65, pos.Z + 0.5);

        if (level >= 8)
        {
            useCooldown = 0.22;
            player.TriggerHandSwing(isOffhand: false);
            var emptyState = _blocks.WithProperty(_blocks.GetDefaultState(BuiltInBlocks.Composter), "level", "0");
            _ = applyWorldBlockChange(pos, Direction.Up, emptyState);
            var bmId = _items.GetId(BuiltInItems.BoneMeal);
            if (!transport.IsConnected && bmId > 0)
            {
                _ = worldBridge.DroppedItems.SpawnBlockDrop(new BlockPos(pos.X, pos.Y + 1, pos.Z), new ItemStack((ushort)bmId, 1));
            }
            else if (transport.IsConnected)
            {
                transport.EnqueueOutbound(new PlayerWorldInteractionPacket(PlayerWorldInteractionKind.InteractWorldComponent, center, Vec3d.Zero, 0, 0));
            }

            _audio.PlayPositional(SoundCue.ComposterReady, center, gain: 0.78f * player.MasterVolume);
            worldBridge.Particles.EmitBurst(center, ParticleKind.GrowthSparkle, 8);
            return true;
        }

        var held = player.Hotbar.SelectedItem;
        if (held is null || !IsCompostable(held.Key)) return false;

        useCooldown = 0.18;
        player.TriggerHandSwing(isOffhand: false);
        var nextLevel = level == 7 ? 8 : level + 1;
        var nextState = _blocks.WithProperty(_blocks.GetDefaultState(BuiltInBlocks.Composter), "level", nextLevel.ToString(CultureInfo.InvariantCulture));
        _ = applyWorldBlockChange(pos, Direction.Up, nextState);
        ConsumeOneFromSlotInSurvival(player, player.Hotbar.SelectedSlot);
        _audio.PlayPositional(SoundCue.ComposterReady, center, gain: 0.75f * player.MasterVolume, pitch: nextLevel == 8 ? 1.08f : 0.88f);
        worldBridge.Particles.EmitBurst(center, ParticleKind.GrowthSparkle, 6);
        return true;
    }

    private bool TryExecuteHandItemUse(
        ClientPlayerController player,
        ClientWorldCache world,
        ClientWorldSyncAndMobBridge worldBridge,
        WorldMobManager mobManager,
        ClientMultiplayerTransport transport,
        ItemDefinition itemDef,
        bool isOffhand,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange,
        ref double useCooldown)
    {
        var invSlot = isOffhand ? PlayerInventory.OffhandSlotIndex : player.Hotbar.SelectedSlot;

        switch (itemDef.UseAction)
        {
            case ItemUseAction.RaiseShield:
                return false; // Handled continuously in StepBowAndShield!

            case ItemUseAction.TillSoil:
                if (player.CurrentTarget is not { } hoeHit) return false;
                var soilKey = _blocks.GetStateMetadata(world.GetBlockState(hoeHit.BlockPosition)).BlockKey;
                if (soilKey != BuiltInBlocks.GrassBlock && soilKey != BuiltInBlocks.Dirt) return false;
                useCooldown = 0.20;
                player.TriggerHandSwing(isOffhand);
                return applyWorldBlockChange(hoeHit.BlockPosition, hoeHit.HitFace, _blocks.GetDefaultState(BuiltInBlocks.Farmland));

            case ItemUseAction.ApplyBoneMeal:
                if (player.CurrentTarget is not { } bmHit) return false;
                var bmTargetKey = _blocks.GetStateMetadata(world.GetBlockState(bmHit.BlockPosition)).BlockKey;
                if (bmTargetKey != BuiltInBlocks.Dirt) return false;
                useCooldown = 0.20;
                player.TriggerHandSwing(isOffhand);
                _ = applyWorldBlockChange(bmHit.BlockPosition, bmHit.HitFace, _blocks.GetDefaultState(BuiltInBlocks.GrassBlock));
                worldBridge.Particles.EmitBurst(new Vec3d(bmHit.BlockPosition.X + 0.5, bmHit.BlockPosition.Y + 1.05, bmHit.BlockPosition.Z + 0.5), ParticleKind.GrowthSparkle, 12);
                ConsumeOneFromSlotInSurvival(player, invSlot);
                return true;

            case ItemUseAction.SpawnMob:
                if (player.CurrentTarget is not { } eggHit) return false;
                var spawnPos = new Vec3d(eggHit.AdjacentPosition.X + 0.5, eggHit.AdjacentPosition.Y + 0.05, eggHit.AdjacentPosition.Z + 0.5);
                _ = BuiltInItems.TryGetSpawnEggMobType(itemDef.Key, out var mobTypeByte);
                var mobType = (MobType)Math.Clamp(mobTypeByte, (byte)0, (byte)7);
                var eggRawId = Math.Max(0, _items.GetId(itemDef.Key));
                useCooldown = 0.22;
                player.TriggerHandSwing(isOffhand);
                if (transport.IsConnected)
                {
                    transport.EnqueueOutbound(new PlayerWorldInteractionPacket(
                        PlayerWorldInteractionKind.SpawnMobEgg, spawnPos, Vec3d.Zero, eggRawId, mobTypeByte));
                }
                else
                {
                    _ = mobManager.SpawnMob(mobType, spawnPos, player.Camera.YawDegrees + 180.0f);
                }

                worldBridge.Particles.EmitBurst(spawnPos + new Vec3d(0, 0.6, 0), ParticleKind.GrowthSparkle, 8);
                _audio.PlayPositional(SoundCue.ItemPop, spawnPos, gain: 0.72f * player.MasterVolume, pitch: 0.92f);
                ConsumeOneFromSlotInSurvival(player, invSlot);
                return true;

            case ItemUseAction.DrinkPotion:
                useCooldown = 0.38;
                player.TriggerHandSwing(isOffhand);
                ApplyPotionStatusEffect(player, itemDef.Key);
                worldBridge.Particles.EmitBurst(player.FeetPosition + new Vec3d(0, 1.1, 0), ParticleKind.GrowthSparkle, 10);
                _audio.PlayPositional(SoundCue.PotionDrink, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                ConsumeOneFromSlotInSurvival(player, invSlot);
                return true;

            case ItemUseAction.EquipArmor:
                return TryQuickEquipArmor(player, itemDef, invSlot, isOffhand, ref useCooldown);

            case ItemUseAction.EatFood:
                if (player.Mode == GameMode.Survival && player.Hunger >= 20 && itemDef.BonusHealPoints <= 0.0f) return false;
                useCooldown = 0.35;
                player.TriggerHandSwing(isOffhand);
                player.ConsumeFood(itemDef.FoodNutrition, itemDef.BonusHealPoints, itemDef.FoodSaturation > 0 ? itemDef.FoodSaturation : itemDef.FoodNutrition * 1.2f);
                if (itemDef.Key == BuiltInItems.GoldenApple)
                {
                    player.StatusEffects.ApplyEffect(StatusEffectType.Regeneration, 12.0f);
                    player.StatusEffects.ApplyEffect(StatusEffectType.Absorption, 90.0f);
                }

                _audio.PlayPositional(SoundCue.EatFood, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                _audio.PlayPositional(SoundCue.Burp, player.Camera.Position, gain: 0.55f * player.MasterVolume);
                ConsumeOneFromSlotInSurvival(player, invSlot);
                return true;

            case ItemUseAction.WaterBucket:
                if (player.CurrentTarget is not { } waterHit) return false;
                useCooldown = 0.22;
                player.TriggerHandSwing(isOffhand);
                _ = applyWorldBlockChange(waterHit.AdjacentPosition, waterHit.HitFace, _blocks.GetDefaultState(BuiltInBlocks.Water));
                _audio.PlayPositional(SoundCue.WaterSplash, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                ReplaceSlotItemInSurvival(player, invSlot, BuiltInItems.Bucket);
                return true;

            case ItemUseAction.EmptyBucket:
                if (!TryFindFluidBlockAlongRay(player, world, out var fluidPos)) return false;
                useCooldown = 0.22;
                player.TriggerHandSwing(isOffhand);
                _ = applyWorldBlockChange(fluidPos, Direction.Up, BlockState.Air);
                _audio.PlayPositional(SoundCue.WaterSplash, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                ReplaceSlotItemInSurvival(player, invSlot, BuiltInItems.WaterBucket);
                return true;

            case ItemUseAction.IgniteTnt:
                return TryUseFlintAndSteel(player, world, worldBridge, isOffhand, applyWorldBlockChange, ref useCooldown);

            case ItemUseAction.PlaceMinecart:
                if (player.CurrentTarget is not { } cartHit) return false;
                var railKey = _blocks.GetStateMetadata(world.GetBlockState(cartHit.BlockPosition)).BlockKey;
                if (!RailPlacementRules.IsRailBlock(railKey)) return false;
                var railVec = new Vec3d(cartHit.BlockPosition.X + 0.5, cartHit.BlockPosition.Y + 0.1, cartHit.BlockPosition.Z + 0.5);
                if (transport.IsConnected)
                {
                    useCooldown = 0.22;
                    player.TriggerHandSwing(isOffhand);
                    transport.EnqueueOutbound(new PlayerWorldInteractionPacket(PlayerWorldInteractionKind.PlaceMinecart, railVec, Vec3d.Zero, Math.Max(0, _items.GetId(itemDef.Key)), 1));
                    _audio.PlayPositional(SoundCue.FootstepMetal, railVec, gain: 0.75f * player.MasterVolume);
                    ConsumeOneFromSlotInSurvival(player, invSlot);
                    return true;
                }

                if (worldBridge.Minecarts.TrySpawnOnRail(cartHit.BlockPosition, world.GetBlockState) is not { } spawnedCart) return false;
                useCooldown = 0.22;
                player.TriggerHandSwing(isOffhand);
                _audio.PlayPositional(SoundCue.FootstepMetal, spawnedCart.FeetPosition, gain: 0.75f * player.MasterVolume);
                ConsumeOneFromSlotInSurvival(player, invSlot);
                return true;

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
                player.TriggerHandSwing(isOffhand);
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
}
