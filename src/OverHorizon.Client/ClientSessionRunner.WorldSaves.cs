using System.Numerics;
using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Hud;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Saved world switching, <c>./saves/</c> persistence, off-thread chunk ticket streaming, and per-frame world/network loop
/// for <see cref="ClientSessionRunner"/>.
/// </summary>
public sealed partial class ClientSessionRunner
{
    private async Task LoadSavedWorldAsync(SavedWorldMetadata meta, CancellationToken ct)
    {
        _menu.StatusMessage = $"Loading world '{meta.WorldName}'...";
        if (_backgroundWorldStreamTask is not null) await _backgroundWorldStreamTask.ConfigureAwait(false);
        await PersistActiveWorldIfAnyAsync().ConfigureAwait(false);

        var oldSavedWorld = _ownedSavedWorld;
        _ownedSavedWorld = null;
        if (oldSavedWorld is not null)
        {
            await oldSavedWorld.DisposeAsync().ConfigureAwait(false);
        }

        var freshWorld = WorldSaveManager.OpenWorldSubsystem(meta, _registries);
        var startPos = meta.HasSavedPose ? new Vec3d(meta.PlayerX, meta.PlayerY, meta.PlayerZ) : new Vec3d(0.5, 80.0, 0.5);
        var centerChunk = startPos.ToBlockPos().ToChunkPos();
        freshWorld.Chunks.UpsertTicket(new ChunkTicket(LocalTicketOwnerId, ChunkTicketType.Player, centerChunk, Math.Min(_menu.ActiveViewDistance, 5)));
        _ = await Task.Run(async () => await freshWorld.Chunks.ProcessTicketsAsync(25, ct).ConfigureAwait(false), ct).ConfigureAwait(false);

        var resolvedSpawnY = _localSpawnY;
        if (!meta.HasSavedPose && freshWorld.Chunks.TryGetSnapshot(new ChunkPos(0, 0), out var snap))
        {
            resolvedSpawnY = snap.GetHeight(HeightmapType.MotionBlocking, 0, 0);
            startPos = new Vec3d(0.5, resolvedSpawnY, 0.5);
        }

        // Marshal ALL OpenGL chunk VAO/VBO disposal & live world swap onto the GLFW main thread (prevents GlfwException: NoContext!)
        await EnqueueOnMainThreadAsync(() =>
        {
            _worldCache.ClearAll(_renderer);
            _ownedSavedWorld = freshWorld;
            _activeWorldMeta = meta;
            _currentSeed = meta.Seed;
            _localSpawnY = resolvedSpawnY;
            _worldBridge.SetActiveLocalWorld(freshWorld, meta.WorldTimeTicks);
            _player.DismountMinecart();
            _player.SetGameMode(meta.DefaultGameMode);
            _player.ApplyAuthoritativePose(startPos, 0.0f, 0.0f);
            if (WorldSaveManager.GetRespawnVec(meta) is { } bedSpawn)
            {
                _player.SpawnPosition = bedSpawn;
            }

            _worldBridge.SyncLocalSnapshots(centerChunk, Math.Min(_menu.ActiveViewDistance, 5), fullImmediateRadius: 2);
            _worldBridge.SeedInitialStarterMobs();
            _menu.StatusMessage = string.Empty;
            _menu.Mode = UiOverlayMode.InGame;
            _chatHistory.AddMessage($"Loaded '{meta.WorldName}' (Seed: {meta.Seed}). E: Inv | F: Offhand | /: Cmds.");
        }).ConfigureAwait(false);
    }

    private async Task SaveAndReturnToTitleAsync(CancellationToken ct)
    {
        if (_transport.IsConnected)
        {
            var oldTransport = _transport;
            await oldTransport.DisposeAsync().ConfigureAwait(false);
            await EnqueueOnMainThreadAsync(() =>
            {
                _transport = new ClientMultiplayerTransport();
                _menu.RefreshSavedWorlds();
                _menu.Mode = UiOverlayMode.TitleMenu;
            }).ConfigureAwait(false);
            return;
        }

        if (_backgroundWorldStreamTask is not null) await _backgroundWorldStreamTask.ConfigureAwait(false);
        await PersistActiveWorldIfAnyAsync().ConfigureAwait(false);

        var oldSavedWorld = _ownedSavedWorld;
        _ownedSavedWorld = null;
        if (oldSavedWorld is not null)
        {
            await oldSavedWorld.DisposeAsync().ConfigureAwait(false);
        }

        _ = ct;
        await EnqueueOnMainThreadAsync(() =>
        {
            _activeWorldMeta = null;
            _currentSeed = DefaultPanoramaSeed;
            _player.DismountMinecart();
            _worldCache.ClearAll(_renderer);
            _worldBridge.SetActiveLocalWorld(_panoramaWorld, WorldTimeCycle.DayTicks);
            _worldBridge.SyncLocalSnapshots(new ChunkPos(0, 0), 4, fullImmediateRadius: 2);
            _menu.RefreshSavedWorlds();
            _menu.Mode = UiOverlayMode.TitleMenu;
        }).ConfigureAwait(false);
    }

    private async ValueTask PersistActiveWorldIfAnyAsync()
    {
        if (_transport.IsConnected) return;
        _ = await _worldBridge.ActiveLocalWorld.Chunks.SaveDirtyChunksAsync(CancellationToken.None).ConfigureAwait(false);
        if (_activeWorldMeta is { } meta)
        {
            var fp = _player.FeetPosition;
            var sp = _player.SpawnPosition;
            var updated = meta with
            {
                DefaultGameMode = _player.Mode,
                WorldTimeTicks = _worldBridge.TimeCycle.TotalTicks,
                PlayerX = fp.X,
                PlayerY = fp.Y,
                PlayerZ = fp.Z,
                HasSavedPose = true,
                RespawnX = sp.X,
                RespawnY = sp.Y,
                RespawnZ = sp.Z,
            };
            _activeWorldMeta = updated;
            WorldSaveManager.SaveMetadata(updated);
        }
    }

    private bool ApplyWorldBlockChange(BlockPos targetPos, Direction face, BlockState newState)
    {
        var oldState = _worldCache.GetBlockState(targetPos);
        var oldKey = _blocks.GetStateMetadata(oldState).BlockKey;
        if (newState.IsAir && (oldKey == BuiltInBlocks.Furnace || oldKey == BuiltInBlocks.LitFurnace))
        {
            _menu.RemoveFurnaceAndDropContents(targetPos, _player.Hotbar.SurvivalInventory);
            _player.Hotbar.RefreshSlotVisuals(_player.Mode);
        }

        if (_transport.IsConnected)
        {
            _ = _worldCache.ApplyBlockUpdate(targetPos, newState);
            _worldCache.RemeshAroundBlockImmediate(targetPos, _renderer);
            var actionKind = newState.IsAir ? PlayerBlockActionKind.BreakBlock : PlayerBlockActionKind.PlaceBlock;
            _transport.EnqueueOutbound(new PlayerBlockActionPacket(actionKind, targetPos, face, newState.Id));
        }
        else
        {
            var dropInv = _player.Mode == GameMode.Survival ? _player.Hotbar.SurvivalInventory : null;
            if (!_worldBridge.ApplyLocalBlockChangeWithCascade(targetPos, newState, dropInv))
            {
                return false;
            }

            if (dropInv is not null) _player.Hotbar.RefreshSlotVisuals(_player.Mode);
        }

        var center = new Vec3d(targetPos.X + 0.5, targetPos.Y + 0.5, targetPos.Z + 0.5);
        SoundCue cue;
        if (!newState.IsAir)
        {
            cue = SoundCue.BlockPlace;
        }
        else if (oldKey == BuiltInBlocks.Glass || oldKey == BuiltInBlocks.Glowstone)
        {
            cue = SoundCue.GlassBreak;
        }
        else
        {
            cue = SoundCue.BlockBreak;
        }

        _audio.PlayPositional(cue, center, gain: 0.7f * _player.MasterVolume);
        _player.RefreshRaycastTarget(_worldCache);
        return true;
    }

    private void UpdateInteractiveFrame(SilkOpenGlRenderSubsystem silkRenderer, double dt, CancellationToken sessionToken)
    {
        _inputBinder.EnsureBoundAndSyncCursor(silkRenderer.InputContext);
        _menuActions.DispatchPendingActions(
            _inputBinder, silkRenderer, SendChatOrCommand, LoadSavedWorldAsync, SaveAndReturnToTitleAsync, () => _transport, t => _transport = t, sessionToken);
        DrainInboundPackets();

        _menu.AdvanceAnimations(dt, _player);
        _survivalCombat.StepTimers(dt);
        var playerChunk = _player.FeetPosition.ToBlockPos().ToChunkPos();

        if (_menu.Mode is UiOverlayMode.TitleMenu or UiOverlayMode.WorldSelectMenu or UiOverlayMode.CreateWorldMenu)
        {
            _player.Camera.Position = new Vec3d(0.5, _localSpawnY + 14.0, 0.5);
            _player.Camera.SetRotation(_menu.PanoramaYawDegrees, -12.0f);
            _meshQueue.DispatchAndUpload(_worldCache, silkRenderer, new ChunkPos(0, 0));
            return;
        }

        // Offload heavy chunk generation & column lighting onto the .NET ThreadPool — ZERO OpenGL render thread stalls!
        if (!_transport.IsConnected && (_backgroundWorldStreamTask is null || _backgroundWorldStreamTask.IsCompleted))
        {
            var radius = Math.Clamp(_menu.ActiveViewDistance, 1, 64);
            var loadBatch = Math.Clamp(3 + (radius / 4), 4, 16);
            var activeWorld = _worldBridge.ActiveLocalWorld;
            activeWorld.Chunks.UpsertTicket(new ChunkTicket(LocalTicketOwnerId, ChunkTicketType.Player, playerChunk, radius));
            _worldBridge.SyncLocalSnapshots(playerChunk, radius);
            _backgroundWorldStreamTask = Task.Run(
                async () => await activeWorld.Chunks.ProcessTicketsAsync(loadBatch, sessionToken).ConfigureAwait(false),
                sessionToken);
        }

        var (move, descend) = _inputBinder.PollMovement(silkRenderer.InputContext);
        _player.StepMovement(_worldCache, _blocks, _audio, move, dt, descend);
        _remotePlayers.StepInterpolation(dt);
        if (!_transport.IsConnected)
        {
            _worldBridge.StepWorldSimulationAndMobs(_player, (float)dt, move.Forward);
        }
        else
        {
            _worldBridge.TimeCycle.AdvanceSeconds(dt);
        }

        var (leftClicked, rightClicked) = _inputBinder.ConsumeActionRequests();
        var leftHeld = _inputBinder.IsLeftMouseHeld(silkRenderer.InputContext);
        var rightHeld = _inputBinder.IsRightMouseHeld(silkRenderer.InputContext);

        if (_menu.IsGameplayActive)
        {
            _survivalCombat.StepBowChargeAndRelease(dt, rightHeld, _player, _worldBridge);
            var swungMelee = leftClicked && _survivalCombat.TryExecuteMeleeAttack(_player, _mobManager, _remotePlayers, _transport, _worldBridge);
            if (!swungMelee) _survivalCombat.StepMining(dt, leftClicked, leftHeld, _player, _worldCache, ApplyWorldBlockChange);
            if (rightClicked && !_player.Hotbar.SelectedIsBow) _ = TryPlaceSelectedBlock();
        }
        else
        {
            _survivalCombat.StepBowChargeAndRelease(dt, rightMouseHeld: false, _player, _worldBridge);
            _player.MiningProgress = 0.0f;
        }

        EnqueueOutboundMovementIfChanged(dt);
        _meshQueue.DispatchAndUpload(_worldCache, silkRenderer, playerChunk);
    }

    private FrameOverlayState BuildOverlayState(Vector2 viewport) =>
        _menu.BuildOverlayState(
            viewport,
            _player,
            _worldCache,
            _remotePlayers.BuildSnapshot(),
            _worldBridge.BuildMobVisuals(_player),
            _worldBridge.BuildPrimedTntVisuals(_player),
            _worldBridge.TimeCycle.TotalTicks,
            _worldBridge.TimeCycle.SkyBrightness,
            _chatHistory,
            _latestDebugText,
            _worldBridge.BuildMinecartVisuals(),
            _worldBridge.BuildArrowVisuals());

    private async ValueTask StepFrameAsync(double deltaSeconds, (float Forward, float Strafe, bool Jump, bool Sprint) movement, CancellationToken ct)
    {
        DrainInboundPackets();
        if (!_transport.IsConnected) await StreamLocalWorldStepAsync(maxChunks: 12, ct).ConfigureAwait(false);
        _menu.AdvanceAnimations(deltaSeconds, _player);
        _survivalCombat.StepTimers(deltaSeconds);
        _player.StepMovement(_worldCache, _blocks, _audio, movement, deltaSeconds);
        if (!_transport.IsConnected) _worldBridge.StepWorldSimulationAndMobs(_player, (float)deltaSeconds);
        _remotePlayers.StepInterpolation(deltaSeconds);
        EnqueueOutboundMovementIfChanged(deltaSeconds);
        _ = await _worldCache.RebuildDirtyMeshesAsync(_renderer, maxColumns: 16, ct).ConfigureAwait(false);
    }

    private async ValueTask StreamLocalWorldStepAsync(int maxChunks, CancellationToken ct)
    {
        var centerChunk = _player.FeetPosition.ToBlockPos().ToChunkPos();
        var radius = Math.Clamp(_menu.ActiveViewDistance, 1, 64);
        var activeWorld = _worldBridge.ActiveLocalWorld;
        activeWorld.Chunks.UpsertTicket(new ChunkTicket(LocalTicketOwnerId, ChunkTicketType.Player, centerChunk, radius));
        _ = await activeWorld.Chunks.ProcessTicketsAsync(maxChunks, ct).ConfigureAwait(false);
        _worldBridge.SyncLocalSnapshots(centerChunk, radius);
    }

    private void DrainInboundPackets()
    {
        while (_transport.TryReadInbound(out var packet))
        {
            ClientInboundPacketApplier.Apply(
                packet, _transport.AssignedEntityId, _remotePlayers, _player, _worldCache, _worldBridge.TimeCycle, _blocks, _audio, _renderer,
                _chatHistory, _logger, _menu.ActiveViewDistance, ref _receivedInitialSpawnSync, ref _lastSentFeetPosition);
        }
    }

    private void EnqueueOutboundMovementIfChanged(double dt)
    {
        if (!_transport.IsConnected)
        {
            _receivedInitialSpawnSync = false;
            return;
        }

        _moveSendTimer += dt;
        if (_moveSendTimer < 0.05) return;
        _moveSendTimer = 0.0;

        var posDeltaSq = (_player.FeetPosition - _lastSentFeetPosition).LengthSquared;
        var rotChanged = MathF.Abs(_player.Camera.YawDegrees - _lastSentYaw) > 0.35f || MathF.Abs(_player.Camera.PitchDegrees - _lastSentPitch) > 0.35f;
        if (posDeltaSq <= 0.0004 && !rotChanged) return;

        _lastSentFeetPosition = _player.FeetPosition;
        _lastSentYaw = _player.Camera.YawDegrees;
        _lastSentPitch = _player.Camera.PitchDegrees;
        _transport.EnqueueOutbound(new MovePlayerPacket(_player.FeetPosition, _lastSentYaw, _lastSentPitch, _player.OnGround));
    }
}
