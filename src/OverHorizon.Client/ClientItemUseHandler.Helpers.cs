using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Effects;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Mobs;

namespace OverHorizon.Client;

/// <summary>
/// Potion effects, armor quick-equip, bed sleep, flint &amp; steel ignition, fluid raycasting, and slot consumption helpers for <see cref="ClientItemUseHandler"/>.
/// </summary>
internal sealed partial class ClientItemUseHandler
{
    private static void ApplyPotionStatusEffect(ClientPlayerController player, ResourceKey key)
    {
        if (key == BuiltInItems.PotionOfSwiftness) player.StatusEffects.ApplyEffect(StatusEffectType.Swiftness, 90.0f);
        else if (key == BuiltInItems.PotionOfRegeneration) player.StatusEffects.ApplyEffect(StatusEffectType.Regeneration, 30.0f);
        else if (key == BuiltInItems.PotionOfStrength) player.StatusEffects.ApplyEffect(StatusEffectType.Strength, 90.0f);
        else if (key == BuiltInItems.PotionOfNightVision) player.StatusEffects.ApplyEffect(StatusEffectType.NightVision, 120.0f);
    }

    private static bool IsCompostable(ResourceKey key) =>
        key == BuiltInItems.Wheat || key == BuiltInItems.Apple || key == BuiltInItems.Bread ||
        key == BuiltInItems.SugarCane || key == BuiltInBlocks.OakLeaves || key == BuiltInBlocks.BirchLeaves || key == BuiltInBlocks.HayBlock;

    private static bool TryHitMultiplayerMinecart(Vec3d eye, Vec3d dir, double maxReach, ClientWorldSyncAndMobBridge bridge)
    {
        var carts = bridge.BuildMinecartVisuals(isMultiplayer: true);
        for (var i = 0; i < carts.Count; i++)
        {
            var cCenter = carts[i].FeetPosition + new Vec3d(0.0, 0.4, 0.0);
            var toC = cCenter - eye;
            var proj = (toC.X * dir.X) + (toC.Y * dir.Y) + (toC.Z * dir.Z);
            if (proj > 0.0 && proj <= maxReach && (cCenter - (eye + (dir * proj))).LengthSquared <= 0.70)
            {
                return true;
            }
        }

        return false;
    }

    private static void TrySleepInBed(ClientPlayerController player, ClientWorldSyncAndMobBridge worldBridge, WorldMobManager mobManager, ClientChatHistory chatHistory, BlockPos bedPos)
    {
        var wakePos = new Vec3d(bedPos.X + 0.5, bedPos.Y + 0.65, bedPos.Z + 0.5);
        player.SpawnPosition = wakePos;
        chatHistory.AddMessage("[Bed] Respawn point set.");
        if (!worldBridge.TimeCycle.IsNightForSleeping) { chatHistory.AddMessage("[Bed] You can sleep only at night."); return; }
        if (player.Mode == GameMode.Survival && mobManager.HasHostileMobNear(wakePos)) { chatHistory.AddMessage("[Bed] You may not rest now; there are monsters nearby."); return; }
        worldBridge.TimeCycle.SkipNightToMorning();
        player.StartSleepVignette();
        chatHistory.AddMessage("[Bed] Good morning! Slept through the night.");
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

    private bool TryQuickEquipArmor(ClientPlayerController player, ItemDefinition held, int sourceSlot, bool isOffhand, ref double useCooldown)
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
        player.TriggerHandSwing(isOffhand);
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
        bool isOffhand,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange,
        ref double useCooldown)
    {
        if (player.CurrentTarget is not { } hit) return false;
        var targetKey = _blocks.GetStateMetadata(world.GetBlockState(hit.BlockPosition)).BlockKey;
        var hitCenter = new Vec3d(hit.BlockPosition.X + 0.5, hit.BlockPosition.Y + 0.5, hit.BlockPosition.Z + 0.5);

        if (targetKey == BuiltInBlocks.Tnt)
        {
            useCooldown = 0.25;
            player.TriggerHandSwing(isOffhand);
            _ = applyWorldBlockChange(hit.BlockPosition, Direction.Up, BlockState.Air);
            _ = worldBridge.WorldTicker.SpawnPrimedTnt(hit.BlockPosition);
            _audio.PlayPositional(SoundCue.FireIgnite, hitCenter, gain: 0.90f * player.MasterVolume);
            _audio.PlayPositional(SoundCue.TntFuse, hitCenter, gain: 0.95f * player.MasterVolume);
            return true;
        }

        var firePos = hit.AdjacentPosition;
        if (world.GetBlockState(firePos).IsAir)
        {
            useCooldown = 0.20;
            player.TriggerHandSwing(isOffhand);
            var ok = applyWorldBlockChange(firePos, hit.HitFace, _blocks.GetDefaultState(BuiltInBlocks.Fire));
            if (ok) _audio.PlayPositional(SoundCue.FireIgnite, hitCenter, gain: 0.85f * player.MasterVolume);
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
            if (def.IsFluid) { fluidPos = sample; return true; }
            if (def.IsSolid) break;
        }

        fluidPos = default;
        return false;
    }
}
