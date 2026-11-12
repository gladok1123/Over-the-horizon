using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Networking.Packets.Serverbound;

namespace OverHorizon.Client;

/// <summary>
/// Executes PvP melee raycast attacks, progressive Survival block mining with tool tiers and item drops,
/// finite block placement, food consumption, water bucket scooping/pouring, and Flint &amp; Steel interactions.
/// </summary>
internal sealed class ClientSurvivalCombatController
{
    private readonly IBlockRegistry _blocks;
    private readonly IRegistry<ItemDefinition> _items;
    private readonly IAudioSubsystem _audio;

    private BlockPos? _miningBlockPos;
    private double _miningElapsedSeconds;
    private double _hitSoundCooldown;
    private double _attackCooldown;
    private double _useCooldown;
    private int _dropSequence;

    public ClientSurvivalCombatController(
        IBlockRegistry blocks,
        IRegistry<ItemDefinition> items,
        IAudioSubsystem audio)
    {
        _blocks = blocks;
        _items = items;
        _audio = audio;
    }

    public void StepTimers(double dt)
    {
        _attackCooldown = Math.Max(0.0, _attackCooldown - dt);
        _useCooldown = Math.Max(0.0, _useCooldown - dt);
        _hitSoundCooldown = Math.Max(0.0, _hitSoundCooldown - dt);
    }

    public bool TryExecutePvpAttack(
        ClientPlayerController player,
        ClientRemotePlayerTracker remotePlayers,
        ClientMultiplayerTransport transport)
    {
        if (!transport.IsConnected || _attackCooldown > 0.0 || player.IsDead)
        {
            return false;
        }

        var f = player.Camera.Forward;
        var forward = new Vec3d(f.X, f.Y, f.Z);
        if (!remotePlayers.TryRaycastPeer(player.Camera.Position, forward, maxReach: 3.8, out var targetEntityId, out var peerDist))
        {
            return false;
        }

        if (player.CurrentTarget is { } blockHit && blockHit.Distance + 0.15 < peerDist)
        {
            return false;
        }

        _attackCooldown = 0.28;
        player.TriggerHandSwing();

        var isCritical = !player.OnGround && !player.InFluid && player.Velocity.Y < -0.08;
        var heldId = player.Hotbar.SelectedItem is { } def ? (ushort)Math.Max(0, _items.GetId(def.Key)) : (ushort)0;
        transport.EnqueueOutbound(new PlayerAttackEntityPacket(targetEntityId, heldId, isCritical));

        _audio.PlayPositional(
            isCritical ? SoundCue.PlayerHit : SoundCue.SwordSwing,
            player.Camera.Position,
            gain: 0.85f * player.MasterVolume,
            pitch: isCritical ? 0.90f : 1.0f);
        return true;
    }

    public void StepMining(
        double dt,
        bool leftClickJustPressed,
        bool leftMouseHeld,
        ClientPlayerController player,
        ClientWorldCache world,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange)
    {
        if (player.IsDead)
        {
            player.MiningProgress = 0.0f;
            return;
        }

        if (player.Mode == GameMode.Creative)
        {
            player.MiningProgress = 0.0f;
            if (leftClickJustPressed && player.CurrentTarget is { } creativeHit)
            {
                var existing = world.GetBlockState(creativeHit.BlockPosition);
                if (!existing.IsAir && existing != _blocks.GetDefaultState(BuiltInBlocks.Bedrock))
                {
                    player.TriggerHandSwing();
                    _ = applyWorldBlockChange(creativeHit.BlockPosition, creativeHit.HitFace, BlockState.Air);
                }
            }

            return;
        }

        if (!leftMouseHeld || player.CurrentTarget is not { } hit)
        {
            _miningBlockPos = null;
            _miningElapsedSeconds = 0.0;
            player.MiningProgress = 0.0f;
            return;
        }

        var blockState = world.GetBlockState(hit.BlockPosition);
        var blockKey = _blocks.GetStateMetadata(blockState).BlockKey;
        var blockDef = _blocks.GetRequired(blockKey);
        var duration = BlockDropResolver.ComputeBreakSeconds(
            blockDef,
            player.Hotbar.SelectedItem,
            player.IsHeadUnderwater,
            player.OnGround);

        if (float.IsInfinity(duration))
        {
            player.MiningProgress = 0.0f;
            return;
        }

        if (_miningBlockPos != hit.BlockPosition)
        {
            _miningBlockPos = hit.BlockPosition;
            _miningElapsedSeconds = 0.0;
        }

        _miningElapsedSeconds += dt;
        player.MiningProgress = duration <= 0.06f
            ? 1.0f
            : (float)Math.Clamp(_miningElapsedSeconds / duration, 0.0, 1.0);

        if (_hitSoundCooldown <= 0.0)
        {
            _hitSoundCooldown = 0.22;
            player.TriggerHandSwing();
            var center = new Vec3d(hit.BlockPosition.X + 0.5, hit.BlockPosition.Y + 0.5, hit.BlockPosition.Z + 0.5);
            _audio.PlayPositional(SoundCue.FootstepStone, center, gain: 0.45f * player.MasterVolume, pitch: 0.85f);
        }

        if (_miningElapsedSeconds >= duration)
        {
            var drop = BlockDropResolver.ResolveDrop(blockKey, player.Hotbar.SelectedItem, _items, _dropSequence++);
            if (!drop.IsEmpty)
            {
                _ = player.Hotbar.SurvivalInventory.AddItem(drop, _items);
                player.Hotbar.RefreshSlotVisuals(player.Mode);
                _audio.PlayPositional(SoundCue.ItemPop, player.Camera.Position, gain: 0.50f * player.MasterVolume);
            }

            _ = applyWorldBlockChange(hit.BlockPosition, hit.HitFace, BlockState.Air);
            _miningBlockPos = null;
            _miningElapsedSeconds = 0.0;
            player.MiningProgress = 0.0f;
        }
    }

    public bool HandleRightClickUseOrPlace(
        ClientPlayerController player,
        ClientWorldCache world,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange,
        out bool openSurvivalCrafting)
    {
        openSurvivalCrafting = false;
        if (player.IsDead || _useCooldown > 0.0)
        {
            return false;
        }

        // Clicking a Crafting Table opens the Survival Crafting & Smelting window!
        if (player.CurrentTarget is { } targetHit)
        {
            var clickedKey = _blocks.GetStateMetadata(world.GetBlockState(targetHit.BlockPosition)).BlockKey;
            if (clickedKey == BuiltInBlocks.CraftingTable)
            {
                openSurvivalCrafting = true;
                _useCooldown = 0.20;
                return true;
            }
        }

        var held = player.Hotbar.SelectedItem;
        if (held is null)
        {
            return false;
        }

        switch (held.UseAction)
        {
            case ItemUseAction.EatFood:
                if (player.Mode == GameMode.Survival && player.Hunger >= 20 && held.BonusHealPoints <= 0.0f)
                {
                    return false;
                }

                _useCooldown = 0.35;
                player.TriggerHandSwing();
                player.ConsumeFood(held.FoodNutrition, Math.Max(1.0f, held.BonusHealPoints));
                _audio.PlayPositional(SoundCue.EatFood, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                ConsumeOneSelectedInSurvival(player);
                return true;

            case ItemUseAction.WaterBucket:
                if (player.CurrentTarget is not { } waterHit)
                {
                    return false;
                }

                _useCooldown = 0.22;
                player.TriggerHandSwing();
                var waterState = _blocks.GetDefaultState(BuiltInBlocks.Water);
                _ = applyWorldBlockChange(waterHit.AdjacentPosition, waterHit.HitFace, waterState);
                _audio.PlayPositional(SoundCue.WaterSplash, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                if (player.Mode == GameMode.Survival)
                {
                    var emptyId = _items.GetId(BuiltInItems.Bucket);
                    if (emptyId > 0)
                    {
                        player.Hotbar.SurvivalInventory.SetSlot(player.Hotbar.SelectedSlot, new ItemStack((ushort)emptyId, 1));
                        player.Hotbar.RefreshSlotVisuals(player.Mode);
                    }
                }

                return true;

            case ItemUseAction.EmptyBucket:
                if (!TryFindFluidBlockAlongRay(player, world, out var fluidPos))
                {
                    return false;
                }

                _useCooldown = 0.22;
                player.TriggerHandSwing();
                _ = applyWorldBlockChange(fluidPos, Direction.Up, BlockState.Air);
                _audio.PlayPositional(SoundCue.WaterSplash, player.Camera.Position, gain: 0.85f * player.MasterVolume);
                if (player.Mode == GameMode.Survival)
                {
                    var fullId = _items.GetId(BuiltInItems.WaterBucket);
                    if (fullId > 0)
                    {
                        player.Hotbar.SurvivalInventory.SetSlot(player.Hotbar.SelectedSlot, new ItemStack((ushort)fullId, 1));
                        player.Hotbar.RefreshSlotVisuals(player.Mode);
                    }
                }

                return true;

            case ItemUseAction.IgniteTnt:
                return TryUseFlintAndSteel(player, world, applyWorldBlockChange);

            case ItemUseAction.PlaceBlock:
                if (player.CurrentTarget is not { } placeHit)
                {
                    return false;
                }

                var stateToPlace = player.Hotbar.SelectedBlockState;
                if (stateToPlace.IsAir)
                {
                    return false;
                }

                var placePos = placeHit.AdjacentPosition;
                var placeKey = _blocks.GetStateMetadata(stateToPlace).BlockKey;
                if (_blocks.GetRequired(placeKey).IsSolid)
                {
                    var playerBox = BoundingBox.FromEntityFeet(
                        player.FeetPosition,
                        ClientPlayerController.PlayerWidth,
                        ClientPlayerController.PlayerHeight).Inflate(-1e-4, -1e-4, -1e-4);

                    if (playerBox.Intersects(BoundingBox.FromBlock(placePos)))
                    {
                        return false;
                    }
                }

                _useCooldown = 0.14;
                player.TriggerHandSwing();
                if (applyWorldBlockChange(placePos, placeHit.HitFace, stateToPlace))
                {
                    ConsumeOneSelectedInSurvival(player);
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private static void ConsumeOneSelectedInSurvival(ClientPlayerController player)
    {
        if (player.Mode != GameMode.Survival)
        {
            return;
        }

        var slot = player.Hotbar.SelectedSlot;
        var cur = player.Hotbar.SurvivalInventory.GetSlot(slot);
        if (!cur.IsEmpty)
        {
            player.Hotbar.SurvivalInventory.SetSlot(slot, cur.WithCount(cur.Count - 1));
            player.Hotbar.RefreshSlotVisuals(player.Mode);
        }
    }

    private bool TryUseFlintAndSteel(
        ClientPlayerController player,
        ClientWorldCache world,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange)
    {
        if (player.CurrentTarget is not { } hit)
        {
            return false;
        }

        var targetKey = _blocks.GetStateMetadata(world.GetBlockState(hit.BlockPosition)).BlockKey;
        if (targetKey == BuiltInBlocks.Tnt)
        {
            _useCooldown = 0.35;
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
                            var drop = BlockDropResolver.ResolveDrop(k, heldTool: null, _items, _dropSequence++);
                            if (player.Mode == GameMode.Survival && !drop.IsEmpty)
                            {
                                _ = player.Hotbar.SurvivalInventory.AddItem(drop, _items);
                            }

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
            _useCooldown = 0.22;
            player.TriggerHandSwing();
            return applyWorldBlockChange(hit.BlockPosition, hit.HitFace, _blocks.GetDefaultState(BuiltInBlocks.Glass));
        }

        if (TryFindFluidBlockAlongRay(player, world, out var waterPos))
        {
            _useCooldown = 0.22;
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
            var k = _blocks.GetStateMetadata(world.GetBlockState(sample)).BlockKey;
            var def = _blocks.GetRequired(k);
            if (def.IsFluid)
            {
                fluidPos = sample;
                return true;
            }

            if (def.IsSolid)
            {
                break;
            }
        }

        fluidPos = default;
        return false;
    }
}
