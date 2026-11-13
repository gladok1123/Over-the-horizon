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
/// Executes PvP, 3D Mob, and Minecart melee attacks (SP + MP), 1-click Fire extinguishing, Creative Bedrock breaking,
/// progressive Survival mining into 3D <c>DroppedItemEntity</c> drops, Bow arrow consumption, and Shield right-click blocking.
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
        if (_attackCooldown > 0.0 || player.IsDead || player.Mode == GameMode.Spectator)
        {
            return false;
        }

        var f = player.Camera.Forward;
        var forwardVec = new Vec3d(f.X, f.Y, f.Z);
        var isCritical = !player.OnGround && !player.IsFlying && !player.InFluid && player.Velocity.Y < -0.08;
        var heldDef = player.Hotbar.SelectedItem;
        var heldId = heldDef is not null ? (ushort)Math.Max(0, _items.GetId(heldDef.Key)) : (ushort)0;
        var weaponDmg = (heldDef?.AttackDamage ?? 1.0f) + player.StatusEffects.GetBonusMeleeDamage();
        var maxReach = 3.8;
        if (player.CurrentTarget is { } blockHit)
        {
            maxReach = Math.Min(maxReach, blockHit.Distance + 0.25);
        }

        // Singleplayer Minecart & Mob melee
        if (!transport.IsConnected)
        {
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
                        _ = worldBridge.DroppedItems.SpawnScatterDrop(brokenCartPos, new ItemStack(cartItemId, 1), pickupDelaySeconds: 0.35f);
                    }
                }

                _audio.PlayPositional(SoundCue.FootstepMetal, brokenCartPos, gain: 0.78f * player.MasterVolume, pitch: 0.90f);
                return true;
            }

            if (mobManager.TryAttackAlongRay(
                player.Camera.Position, f, maxReach, weaponDmg, isCritical, _items, out var hitMob, out var mobDrops, isSprintAttack: player.IsSprinting))
            {
                _attackCooldown = 0.26;
                player.TriggerHandSwing();
                player.AddExhaustion(HungerAndVitalsModel.ExhaustionPerAttack);

                if (hitMob is not null)
                {
                    worldBridge?.Particles.EmitCombatHit(hitMob.FeetPosition, isCritical);
                    for (var i = 0; i < mobDrops.Count; i++)
                    {
                        if (!mobDrops[i].IsEmpty && worldBridge is not null)
                        {
                            _ = worldBridge.DroppedItems.SpawnScatterDrop(hitMob.FeetPosition, mobDrops[i], pickupDelaySeconds: 0.40f);
                        }
                    }

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

            return false;
        }

        // Multiplayer: check PvP peer first, then world Mobs / Minecarts!
        if (remotePlayers.TryRaycastPeer(player.Camera.Position, forwardVec, maxReach: 3.8, out var targetEntityId, out var peerDist) &&
            (player.CurrentTarget is not { } pBlockHit || pBlockHit.Distance + 0.15 >= peerDist))
        {
            _attackCooldown = 0.28;
            player.TriggerHandSwing();
            player.AddExhaustion(HungerAndVitalsModel.ExhaustionPerAttack);
            transport.EnqueueOutbound(new PlayerAttackEntityPacket(targetEntityId, heldId, isCritical));
            _audio.PlayPositional(
                isCritical ? SoundCue.PlayerHit : SoundCue.SwordSwing,
                player.Camera.Position,
                gain: 0.85f * player.MasterVolume,
                pitch: isCritical ? 0.90f : 1.0f);
            return true;
        }

        if (worldBridge is not null && TryRaycastMultiplayerMobOrCart(player, forwardVec, maxReach, worldBridge))
        {
            _attackCooldown = 0.26;
            player.TriggerHandSwing();
            var atkFlags = (isCritical ? 1 : 0) | (player.IsSprinting ? 2 : 0) | 4;
            transport.EnqueueOutbound(new PlayerWorldInteractionPacket(
                PlayerWorldInteractionKind.AttackMobOrVehicle, player.Camera.Position, forwardVec, heldId, atkFlags));
            _audio.PlayPositional(SoundCue.SwordSwing, player.Camera.Position, gain: 0.80f * player.MasterVolume);
            return true;
        }

        return false;
    }

    public void StepMining(
        double dt,
        bool leftClickJustPressed,
        bool leftMouseHeld,
        ClientPlayerController player,
        ClientWorldCache world,
        ClientWorldSyncAndMobBridge worldBridge,
        bool isMultiplayer,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange)
    {
        if (player.IsDead || player.Mode == GameMode.Spectator)
        {
            player.MiningProgress = 0.0f;
            return;
        }

        if (leftClickJustPressed)
        {
            player.TriggerHandSwing();
            if (player.CurrentTarget is null)
            {
                _audio.PlayPositional(SoundCue.SwordSwing, player.Camera.Position, gain: 0.42f * player.MasterVolume);
            }
            else if (TryPunchExtinguishFire(player.CurrentTarget.Value, world, applyWorldBlockChange))
            {
                return;
            }
        }

        if (player.Mode == GameMode.Creative)
        {
            player.MiningProgress = 0.0f;
            if (leftClickJustPressed && player.CurrentTarget is { } creativeHit)
            {
                var existing = world.GetBlockState(creativeHit.BlockPosition);
                if (!existing.IsAir)
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
            if (!isMultiplayer)
            {
                var drop = BlockDropResolver.ResolveDrop(blockKey, player.Hotbar.SelectedItem, _items, _dropSequence++);
                if (!drop.IsEmpty)
                {
                    _ = worldBridge.DroppedItems.SpawnBlockDrop(hit.BlockPosition, drop);
                }
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
        out BlockPos? openedContainerPos) =>
        HandleRightClickUseOrPlace(
            player, world, worldBridge, mobManager, new ClientMultiplayerTransport(), chatHistory, applyWorldBlockChange, out openContainerMode, out openedContainerPos);

    public bool HandleRightClickUseOrPlace(
        ClientPlayerController player,
        ClientWorldCache world,
        ClientWorldSyncAndMobBridge worldBridge,
        WorldMobManager mobManager,
        ClientMultiplayerTransport transport,
        ClientChatHistory chatHistory,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange,
        out UiOverlayMode? openContainerMode,
        out BlockPos? openedContainerPos)
    {
        openContainerMode = null;
        openedContainerPos = null;
        if (player.IsDead || player.Mode == GameMode.Spectator || _useCooldown > 0.0)
        {
            return false;
        }

        return _useHandler.TryUseOrPlace(
            player, world, worldBridge, mobManager, transport, chatHistory, applyWorldBlockChange, ref _useCooldown, out openContainerMode, out openedContainerPos);
    }

    public void StepBowAndShield(
        double dt,
        bool rightMouseHeld,
        ClientPlayerController player,
        ClientWorldSyncAndMobBridge worldBridge,
        ClientMultiplayerTransport transport)
    {
        if (player.IsDead || player.Mode == GameMode.Spectator)
        {
            _bowDrawing = false;
            _bowDrawSeconds = 0.0f;
            player.BowChargeNormalized = 0.0f;
            player.IsBlockingShield = false;
            player.ShieldRaiseNormalized = 0.0f;
            return;
        }

        // 1. Bow Draw & Release (consumes Arrows in Survival; syncs to Multiplayer!)
        if (player.Hotbar.SelectedIsBow)
        {
            player.IsBlockingShield = false;
            player.ShieldRaiseNormalized = MathF.Max(0.0f, player.ShieldRaiseNormalized - ((float)dt * 8.0f));
            var hasArrow = player.Hotbar.HasArrowForBow(player.Mode);

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

                if (charge >= 0.12f && player.Hotbar.TryConsumeArrowForBow(player.Mode))
                {
                    var f = player.Camera.Forward;
                    if (transport.IsConnected)
                    {
                        var pct = (ushort)Math.Clamp((int)MathF.Round(charge * 100.0f), 12, 100);
                        transport.EnqueueOutbound(new PlayerWorldInteractionPacket(
                            PlayerWorldInteractionKind.ShootArrow, player.Camera.Position, new Vec3d(f.X, f.Y, f.Z), pct, player.Mode == GameMode.Survival ? 1 : 0));
                    }
                    else
                    {
                        _ = worldBridge.Arrows.SpawnPlayerArrow(player.Camera.Position, f, charge, canBePickedUp: player.Mode == GameMode.Survival);
                    }

                    player.TriggerHandSwing();
                    _audio.PlayPositional(SoundCue.BowShoot, player.Camera.Position, gain: 0.86f * player.MasterVolume, pitch: 0.88f + (charge * 0.24f));
                }
            }

            return;
        }

        _bowDrawing = false;
        _bowDrawSeconds = 0.0f;
        player.BowChargeNormalized = 0.0f;

        // 2. Shield Blocking (Mainhand or Offhand while holding Right-Click!)
        var canRaiseShield = rightMouseHeld && (player.Hotbar.SelectedIsShield || player.Hotbar.OffhandIsShield);
        player.IsBlockingShield = canRaiseShield;
        player.ShieldRaiseNormalized = canRaiseShield
            ? MathF.Min(1.0f, player.ShieldRaiseNormalized + ((float)dt * 9.5f))
            : MathF.Max(0.0f, player.ShieldRaiseNormalized - ((float)dt * 9.5f));
    }

    private bool TryPunchExtinguishFire(
        Physics.VoxelRaycastHit hit,
        ClientWorldCache world,
        Func<BlockPos, Direction, BlockState, bool> applyWorldBlockChange)
    {
        var directKey = _blocks.GetStateMetadata(world.GetBlockState(hit.BlockPosition)).BlockKey;
        if (directKey == BuiltInBlocks.Fire)
        {
            return applyWorldBlockChange(hit.BlockPosition, Direction.Up, BlockState.Air);
        }

        var adjKey = _blocks.GetStateMetadata(world.GetBlockState(hit.AdjacentPosition)).BlockKey;
        if (adjKey == BuiltInBlocks.Fire)
        {
            return applyWorldBlockChange(hit.AdjacentPosition, Direction.Up, BlockState.Air);
        }

        return false;
    }

    private static bool TryRaycastMultiplayerMobOrCart(ClientPlayerController player, Vec3d dir, double maxReach, ClientWorldSyncAndMobBridge bridge)
    {
        var eye = player.Camera.Position;
        var mobs = bridge.BuildMobVisuals(player, isMultiplayer: true);
        for (var i = 0; i < mobs.Count; i++)
        {
            var mCenter = mobs[i].FeetPosition + new Vec3d(0.0, 0.9, 0.0);
            var toM = mCenter - eye;
            var proj = (toM.X * dir.X) + (toM.Y * dir.Y) + (toM.Z * dir.Z);
            if (proj > 0.0 && proj <= maxReach && (mCenter - (eye + (dir * proj))).LengthSquared <= 0.72)
            {
                return true;
            }
        }

        var carts = bridge.BuildMinecartVisuals(isMultiplayer: true);
        for (var i = 0; i < carts.Count; i++)
        {
            var cCenter = carts[i].Position + new Vec3d(0.0, 0.4, 0.0);
            var toC = cCenter - eye;
            var proj = (toC.X * dir.X) + (toC.Y * dir.Y) + (toC.Z * dir.Z);
            if (proj > 0.0 && proj <= maxReach && (cCenter - (eye + (dir * proj))).LengthSquared <= 0.65)
            {
                return true;
            }
        }

        return false;
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
