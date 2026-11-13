using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Executes PvP &amp; 3D Mob melee raycast attacks, progressive Survival block mining with tool tiers,
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
        ClientMultiplayerTransport transport)
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

        if (mobManager.TryAttackAlongRay(
            player.Camera.Position, f, maxReach, weaponDmg, isCritical, _items, out _, out var mobDrops))
        {
            _attackCooldown = 0.26;
            player.TriggerHandSwing();

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

            _audio.PlayPositional(
                isCritical ? SoundCue.PlayerHit : SoundCue.SwordSwing,
                player.Camera.Position,
                gain: 0.85f * player.MasterVolume,
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
        WorldMobManager mobManager,
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
            player, world, mobManager, applyWorldBlockChange, ref _useCooldown, out openContainerMode, out openedContainerPos);
    }
}
