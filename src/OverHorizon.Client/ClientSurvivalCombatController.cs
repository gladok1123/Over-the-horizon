using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Projectiles;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Executes PvP &amp; 3D Mob melee raycast attacks (with species-authentic hurt/death <c>.ogg</c> audio),
/// empty-air 1st-person hand swings, progressive Survival block mining with on-block crack stages,
/// and delegates right-click block/item interactions to <see cref="ClientItemUseHandler"/>.
/// </summary>
internal sealed class ClientSurvivalCombatController
{
    private readonly IBlockRegistry _blocks;
    private readonly IRegistry<ItemDefinition> _items;
    private readonly IAudioSubsystem _audio;
    private readonly ClientItemUseHandler _useHandler;

    private BlockPos? _miningBlockPos;
    private double _miningElapsedSeconds;
    private double _hitSoundCooldown;
    private double _attackCooldown;
    private double _useCooldown;
    private float _bowDrawSeconds;
    private bool _bowDrawing;
    private int _dropSequence;

    public ClientSurvivalCombatController(
        IBlockRegistry blocks,
        IRegistry<ItemDefinition> items,
        IAudioSubsystem audio)
    {
        _blocks = blocks;
        _items = items;
        _audio = audio;
        _useHandler = new ClientItemUseHandler(blocks, items, audio);
    }

    public void StepTimers(double dt)
    {
        _attackCooldown = Math.Max(0.0, _attackCooldown - dt);
        _useCooldown = Math.Max(0.0, _useCooldown - dt);
        _hitSoundCooldown = Math.Max(0.0, _hitSoundCooldown - dt);
    }

    public bool TryExecuteMeleeAttack(
        ClientPlayerController player,
        WorldMobManager mobManager,
        ClientRemotePlayerTracker remotePlayers,
        ClientMultiplayerTransport transport,
        ClientWorldSyncAndMobBridge? worldBridge = null)
    {
        if (_attackCooldown > 0.0 || player.IsDead)
        {
            return false;
        }

        var f = player.Camera.Forward;
        var isCritical = !player.OnGround && !player.IsFlying && !player.InFluid && player.Velocity.Y < -0.08;
        var heldDef = player.Hotbar.SelectedItem;
        var weaponDmg = heldDef?.AttackDamage ?? 1.0f;
        var maxReach = 3.8;
        if (player.CurrentTarget is { } blockHit)
        {
            maxReach = Math.Min(maxReach, blockHit.Distance + 0.25);
        }

        if (worldBridge is not null &&
            worldBridge.Minecarts.TryBreakAlongRay(player.Camera.Position, f, maxReach, out var brokenCartPos))
        {
            _attackCooldown = 0.24;
            player.TriggerHandSwing();
            if (player.RidingMinecartId >= 0 && worldBridge.Minecarts.RiddenMinecart is null)
            {
                player.DismountMinecart();
            }

            if (player.Mode == GameMode.Survival)
            {
                var cartItemId = (ushort)Math.Max(0, _items.GetId(BuiltInItems.Minecart));
                if (cartItemId > 0)
                {
                    _ = player.Hotbar.SurvivalInventory.AddItem(new ItemStack(cartItemId, 1), _items);
                    player.Hotbar.RefreshSlotVisuals(player.Mode);
                }
            }

            _audio.PlayPositional(SoundCue.FootstepMetal, brokenCartPos, gain: 0.78f * player.MasterVolume, pitch: 0.90f);
            return true;
        }

        if (mobManager.TryAttackAlongRay(
            player.Camera.Position, f, maxReach, weaponDmg, isCritical, _items, out var hitMob, out var mobDrops))
        {
            _attackCooldown = 0.26;
            player.TriggerHandSwing();
            player.AddExhaustion(HungerAndVitalsModel.ExhaustionPerAttack);

            for (var i = 0; i < mobDrops.Count; i++)
            {
                if (!mobDrops[i].IsEmpty && player.Mode == GameMode.Survival)
                {
                    _ = player.Hotbar.SurvivalInventory.AddItem(mobDrops[i], _items);
                }
            }

            if (mobDrops.Count > 0 && player.Mode == GameMode.Survival)
            {
                player.Hotbar.RefreshSlotVisuals(player.Mode);
                _audio.PlayPositional(SoundCue.ItemPop, player.Camera.Position, gain: 0.55f * player.MasterVolume);
            }

            if (hitMob is not null)
            {
                var mobCue = ResolveMobHurtOrDeathCue(hitMob.Type, hitMob.IsDead);
                _audio.PlayPositional(mobCue, hitMob.FeetPosition, gain: 0.88f * player.MasterVolume);
            }

            _audio.PlayPositional(
                isCritical ? SoundCue.PlayerHit : SoundCue.SwordSwing,
                player.Camera.Position,
                gain: 0.80f * player.MasterVolume,
                pitch: isCritical ? 0.90f : 1.0f);
            return true;
        }

        if (!transport.IsConnected)
        {
            return false;
        }

        var forward = new Vec3d(f.X, f.Y, f.Z);
        if (!remotePlayers.TryRaycastPeer(player.Camera.Position, forward, maxReach: 3.8, out var targetEntityId, out var peerDist))
        {
            return false;
        }

        if (player.CurrentTarget is { } pBlockHit && pBlockHit.Distance + 0.15 < peerDist)
        {
            return false;
        }

        _attackCooldown = 0.28;
        player.TriggerHandSwing();
        player.AddExhaustion(HungerAndVitalsModel.ExhaustionPerAttack);

        var heldId = heldDef is not null ? (ushort)Math.Max(0, _items.GetId(heldDef.Key)) : (ushort)0;
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

        // Air Swing: left-clicking anywhere (including empty sky/air!) ALWAYS triggers full 1st-person hand swing!
        if (leftClickJustPressed)
        {
            player.TriggerHandSwing();
            if (player.CurrentTarget is null)
            {
                _audio.PlayPositional(SoundCue.SwordSwing, player.Camera.Position, gain: 0.42f * player.MasterVolume);
            }
        }

        if (player.Mode == GameMode.Creative)
        {
            player.MiningProgress = 0.0f;
            if (leftClickJustPressed && player.CurrentTarget is { } creativeHit)
            {
                var existing = world.GetBlockState(creativeHit.BlockPosition);
                if (!existing.IsAir && existing != _blocks.GetDefaultState(BuiltInBlocks.Bedrock))
                {
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
            blockDef, player.Hotbar.SelectedItem, player.IsHeadUnderwater, player.OnGround);

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
        player.MiningProgress = duration <= 0.06f ? 1.0f : (float)Math.Clamp(_miningElapsedSeconds / duration, 0.0, 1.0);

        if (_hitSoundCooldown <= 0.0)
        {
            _hitSoundCooldown = 0.22;
            player.TriggerHandSwing();
            var center = new Vec3d(hit.BlockPosition.X + 0.5, hit.BlockPosition.Y + 0.5, hit.BlockPosition.Z + 0.5);
            var hitCue = ClientPlayerVitalsAndAudio.ResolveFootstepCue(blockKey.Path);
            _audio.PlayPositional(hitCue, center, gain: 0.45f * player.MasterVolume, pitch: 0.84f);
        }

        if (_miningElapsedSeconds >= duration)
        {
            player.AddExhaustion(HungerAndVitalsModel.ExhaustionPerBlockMined);
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
        ClientWorldSyncAndMobBridge worldBridge,
        WorldMobManager mobManager,
        ClientChatHistory chatHistory,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange,
        out UiOverlayMode? openContainerMode,
        out BlockPos? openedContainerPos)
    {
        openContainerMode = null;
        openedContainerPos = null;
        if (player.IsDead || _useCooldown > 0.0)
        {
            return false;
        }

        return _useHandler.TryUseOrPlace(
            player, world, worldBridge, mobManager, chatHistory, applyWorldBlockChange, ref _useCooldown, out openContainerMode, out openedContainerPos);
    }

    public void StepBowChargeAndRelease(
        double dt,
        bool rightMouseHeld,
        ClientPlayerController player,
        ClientWorldSyncAndMobBridge worldBridge)
    {
        if (player.IsDead || !player.Hotbar.SelectedIsBow)
        {
            _bowDrawing = false;
            _bowDrawSeconds = 0.0f;
            player.BowChargeNormalized = 0.0f;
            return;
        }

        var arrowItemId = (ushort)Math.Max(0, _items.GetId(BuiltInItems.Arrow));
        var hasArrow = player.Mode == GameMode.Creative || FindArrowSlot(player.Hotbar.SurvivalInventory, arrowItemId) >= 0;

        if (rightMouseHeld && hasArrow)
        {
            _bowDrawing = true;
            _bowDrawSeconds = MathF.Min(2.0f, _bowDrawSeconds + (float)dt);
            player.BowChargeNormalized = ArrowProjectileManager.ComputeChargeNormalized(_bowDrawSeconds);
            return;
        }

        if (_bowDrawing)
        {
            var charge = ArrowProjectileManager.ComputeChargeNormalized(_bowDrawSeconds);
            _bowDrawing = false;
            _bowDrawSeconds = 0.0f;
            player.BowChargeNormalized = 0.0f;

            if (charge >= 0.12f && hasArrow)
            {
                if (player.Mode == GameMode.Survival)
                {
                    var arrowSlot = FindArrowSlot(player.Hotbar.SurvivalInventory, arrowItemId);
                    if (arrowSlot < 0) return;
                    var st = player.Hotbar.SurvivalInventory.GetSlot(arrowSlot);
                    player.Hotbar.SurvivalInventory.SetSlot(arrowSlot, st.WithCount(st.Count - 1));
                    player.Hotbar.RefreshSlotVisuals(player.Mode);
                }

                _ = worldBridge.Arrows.SpawnPlayerArrow(
                    player.Camera.Position,
                    player.Camera.Forward,
                    charge,
                    canBePickedUp: player.Mode == GameMode.Survival);
                player.TriggerHandSwing();
                var pitch = 0.88f + (charge * 0.24f);
                _audio.PlayPositional(SoundCue.BowShoot, player.Camera.Position, gain: 0.86f * player.MasterVolume, pitch: pitch);
            }
        }
    }

    private static int FindArrowSlot(PlayerInventory inv, ushort arrowItemId)
    {
        if (arrowItemId == 0) return -1;
        if (inv.OffhandItem.ItemId == arrowItemId && !inv.OffhandItem.IsEmpty) return PlayerInventory.OffhandSlotIndex;
        for (var i = 0; i < PlayerInventory.MainSlotCount; i++)
        {
            var s = inv.GetSlot(i);
            if (!s.IsEmpty && s.ItemId == arrowItemId) return i;
        }

        return -1;
    }

    internal static SoundCue ResolveMobHurtOrDeathCue(MobType type, bool isDead) => (type, isDead) switch
    {
        (MobType.Pig, true) => SoundCue.MobPigDeath,
        (MobType.Pig, false) => SoundCue.MobPigSay,
        (MobType.Cow, _) => SoundCue.MobCowHurt,
        (MobType.Sheep, _) => SoundCue.MobSheepSay,
        (MobType.Chicken, _) => SoundCue.MobChickenHurt,
        (MobType.Zombie, true) => SoundCue.MobZombieDeath,
        (MobType.Zombie, false) => SoundCue.MobZombieHurt,
        (MobType.Skeleton, true) => SoundCue.MobSkeletonDeath,
        (MobType.Skeleton, false) => SoundCue.MobSkeletonHurt,
        (MobType.Spider, true) => SoundCue.MobSpiderDeath,
        (MobType.Spider, false) => SoundCue.MobSpiderSay,
        (MobType.Creeper, true) => SoundCue.MobCreeperDeath,
        _ => SoundCue.MobCreeperHiss,
    };
}
