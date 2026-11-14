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
using OverHorizon.World.Generation;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Saved world switching, "Open to LAN", clean network/world shutdown, synchronous block-edit remeshing,
/// and per-frame world/network loop for <see cref="ClientSessionRunner"/>.
/// </summary>
public sealed partial class ClientSessionRunner
{
    private async Task LoadSavedWorldAsync(SavedWorldMetadata meta, CancellationToken ct)
    {
        _menu.StatusMessage = $"Loading world '{meta.WorldName}'...";
        if (_backgroundWorldStreamTask is not null) await _backgroundWorldStreamTask.ConfigureAwait(false);
        await PersistActiveWorldIfAnyAsync().ConfigureAwait(false);

        var oldSavedWorld = _ownedSavedWorld;
        if (oldSavedWorld is not null)
        {
            await EnqueueOnMainThreadAsync(() =>
            {
                _ownedSavedWorld = null;
                _worldBridge.SetActiveLocalWorld(_panoramaWorld, _worldBridge.TimeCycle.TotalTicks);
            }).ConfigureAwait(false);
            if (_backgroundWorldStreamTask is not null) await _backgroundWorldStreamTask.ConfigureAwait(false);
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

        await EnqueueOnMainThreadAsync(() =>
        {
            _meshQueue.DrainPending();
            _worldCache.ClearAll(_renderer);
            _ownedSavedWorld = freshWorld;
            _activeWorldMeta = meta;
            _currentSeed = meta.Seed;
            _biomeSampler = new BiomeSampler(_currentSeed, _registries.Biomes);
            _localSpawnY = resolvedSpawnY;
            _worldBridge.SetActiveLocalWorld(freshWorld, meta.WorldTimeTicks);
            _player.DismountMinecart();
            if (!meta.HasSavedPose)
            {
                if (meta.DefaultGameMode == GameMode.Survival) _player.Hotbar.PopulateStarterSurvivalGear();
                else _player.Hotbar.SeedInitialCreativeHotbar();
            }

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
            _chatHistory.AddMessage($"Loaded '{meta.WorldName}' (Seed: {meta.Seed}). MMB: Pick | Q: Drop | F: Offhand | F4: Mode.");
        }).ConfigureAwait(false);
    }

    private async Task OpenActiveWorldToLanAsync(LocalServerLauncher launcher, CancellationToken ct)
    {
        if (_transport.IsConnected) return;
        _menu.StatusMessage = "Saving world and opening LAN server on TCP 25565...";

        try
        {
            if (_backgroundWorldStreamTask is not null) await _backgroundWorldStreamTask.ConfigureAwait(false);
            await PersistActiveWorldIfAnyAsync().ConfigureAwait(false);

            var savedFeet = _player.FeetPosition;
            var savedYaw = _player.Camera.YawDegrees;
            var savedPitch = _player.Camera.PitchDegrees;
            var savedMeta = _activeWorldMeta;
            var worldDir = !string.IsNullOrEmpty(savedMeta?.FolderPath)
                ? savedMeta.FolderPath
                : Path.Combine(Directory.GetCurrentDirectory(), "saves", "lan_world");
            var worldSeed = savedMeta?.Seed ?? _currentSeed;

            // Crucial: detach _worldBridge from oldSavedWorld ON THE MAIN THREAD BEFORE disposing oldSavedWorld,
            // so UpdateInteractiveFrame never calls TryGetSnapshot/UpsertTicket on a disposed WorldSubsystem!
            var oldSavedWorld = _ownedSavedWorld;
            if (oldSavedWorld is not null)
            {
                await EnqueueOnMainThreadAsync(() =>
                {
                    _ownedSavedWorld = null;
                    _worldBridge.SetActiveLocalWorld(_panoramaWorld, _worldBridge.TimeCycle.TotalTicks);
                }).ConfigureAwait(false);
                if (_backgroundWorldStreamTask is not null) await _backgroundWorldStreamTask.ConfigureAwait(false);
                await oldSavedWorld.DisposeAsync().ConfigureAwait(false);
            }

            var started = await launcher.EnsureLocalServerRunningAsync(
                25565, ct, customWorldDir: worldDir, seed: worldSeed, viewDistance: Math.Clamp(_menu.ActiveViewDistance, 4, 12)).ConfigureAwait(false);
            if (!started)
            {
                if (savedMeta is not null)
                {
                    var reopened = WorldSaveManager.OpenWorldSubsystem(savedMeta, _registries);
                    await EnqueueOnMainThreadAsync(() =>
                    {
                        _ownedSavedWorld = reopened;
                        _worldBridge.SetActiveLocalWorld(reopened, savedMeta.WorldTimeTicks);
                    }).ConfigureAwait(false);
                }

                _menu.StatusMessage = "Could not launch OverHorizon.Server for LAN.";
                return;
            }

            var user = string.IsNullOrWhiteSpace(_menu.UsernameInput) ? "Explorer" : _menu.UsernameInput;
            var freshTransport = new ClientMultiplayerTransport();
            var login = await freshTransport.ConnectAndLoginAsync("127.0.0.1", 25565, user, ct).ConfigureAwait(false);
            ClientLog.MultiplayerConnected(_logger, "127.0.0.1", 25565, login.Username, login.AssignedEntityId);

            await EnqueueOnMainThreadAsync(() =>
            {
                _meshQueue.DrainPending();
                _worldCache.ClearAll(_renderer);
                _remotePlayers.Clear();
                _worldBridge.ClearMultiplayerEntitySnapshots();
                _transport = freshTransport;
                _receivedInitialSpawnSync = true;
                _player.ApplyAuthoritativePose(savedFeet, savedYaw, savedPitch, updateSpawn: false);
                _transport.EnqueueOutbound(new MovePlayerPacket(savedFeet, savedYaw, savedPitch, _player.OnGround));
                _menu.StatusMessage = string.Empty;
                _menu.Mode = UiOverlayMode.InGame;
                _chatHistory.AddMessage("[LAN] Local game hosted on 0.0.0.0:25565! Friends can join via Multiplayer.");
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _menu.StatusMessage = $"LAN start error: {ex.Message}";
        }
    }

    private async Task SaveAndReturnToTitleAsync(CancellationToken ct)
    {
        if (_transport.IsConnected)
        {
            var oldTransport = _transport;
            _transport = new ClientMultiplayerTransport();
            await oldTransport.DisposeAsync().ConfigureAwait(false);
            _menuActions.StopHostedServerIfAny();

            await EnqueueOnMainThreadAsync(() =>
            {
                _activeWorldMeta = null;
                _receivedInitialSpawnSync = false;
                _remotePlayers.Clear();
                _player.DismountMinecart();
                _meshQueue.DrainPending();
                _worldCache.ClearAll(_renderer);
                _worldBridge.SetActiveLocalWorld(_panoramaWorld, WorldTimeCycle.DayTicks);
                _worldBridge.SyncLocalSnapshots(new ChunkPos(0, 0), 4, fullImmediateRadius: 2);
                _menu.RefreshSavedWorlds();
                _menu.Mode = UiOverlayMode.TitleMenu;
            }).ConfigureAwait(false);
            return;
        }

        if (_backgroundWorldStreamTask is not null) await _backgroundWorldStreamTask.ConfigureAwait(false);
        await PersistActiveWorldIfAnyAsync().ConfigureAwait(false);

        var oldSavedWorld = _ownedSavedWorld;
        if (oldSavedWorld is not null)
        {
            await EnqueueOnMainThreadAsync(() =>
            {
                _ownedSavedWorld = null;
                _worldBridge.SetActiveLocalWorld(_panoramaWorld, WorldTimeCycle.DayTicks);
            }).ConfigureAwait(false);
            if (_backgroundWorldStreamTask is not null) await _backgroundWorldStreamTask.ConfigureAwait(false);
            await oldSavedWorld.DisposeAsync().ConfigureAwait(false);
        }

        _ = ct;
        await EnqueueOnMainThreadAsync(() =>
        {
            _activeWorldMeta = null;
            _currentSeed = DefaultPanoramaSeed;
            _biomeSampler = new BiomeSampler(_currentSeed, _registries.Biomes);
            _player.DismountMinecart();
            _meshQueue.DrainPending();
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
            if (!_worldBridge.ApplyLocalBlockChangeWithCascade(targetPos, newState, spawnDetachedItemDrops: _player.Mode == GameMode.Survival))
            {
                return false;
            }
        }

        var center = new Vec3d(targetPos.X + 0.5, targetPos.Y + 0.5, targetPos.Z + 0.5);
        if (!newState.IsAir)
        {
            var placedKey = _blocks.GetStateMetadata(newState).BlockKey;
            _audio.PlayPositional(BlockSoundProfile.ResolvePlaceCue(placedKey), center, gain: 0.72f * _player.MasterVolume, pitch: BlockSoundProfile.ResolvePlacePitch(placedKey));
        }
        else
        {
            _worldBridge.EmitBlockBreakParticles(targetPos, oldKey);
            var breakCue = oldKey == BuiltInBlocks.Fire ? SoundCue.Fizz : BlockSoundProfile.ResolveBreakCue(oldKey);
            _audio.PlayPositional(breakCue, center, gain: 0.74f * _player.MasterVolume, pitch: BlockSoundProfile.ResolveBreakPitch(oldKey));
        }

        _player.RefreshRaycastTarget(_worldCache);
        return true;
    }

    private void UpdateInteractiveFrame(SilkOpenGlRenderSubsystem silkRenderer, double dt, CancellationToken sessionToken)
    {
        while (_mainThreadQueue.TryDequeue(out var mainWork)) mainWork();

        _menu.IsMultiplayerConnected = _transport.IsConnected;
        _menu.IsLanOpen = _menuActions.IsLanOpen;
        _worldBridge.Particles.DensityMode = _menu.ParticlesMode;
        if (_menu.XRayDirtyRemeshRequested)
        {
            _menu.XRayDirtyRemeshRequested = false;
            _worldCache.MarkAllLoadedChunksDirty();
        }

        _inputBinder.EnsureBoundAndSyncCursor(silkRenderer.InputContext);
        _menuActions.DispatchPendingActions(
            _inputBinder, silkRenderer, SendChatOrCommand, LoadSavedWorldAsync, SaveAndReturnToTitleAsync, OpenActiveWorldToLanAsync,
            () => _transport, t => _transport = t, EnqueueOnMainThreadAsync, sessionToken);
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

        if (!_transport.IsConnected && StepPortalTravel(silkRenderer, sessionToken))
        {
            return;
        }

        var (move, descend) = _inputBinder.PollMovement(silkRenderer.InputContext);
        var prevRidingCart = _player.RidingMinecartId;
        _player.StepMovement(_worldCache, _blocks, _audio, move, dt, descend);
        _remotePlayers.StepInterpolation(dt);

        if (!_transport.IsConnected)
        {
            _worldBridge.StepWorldSimulationAndMobs(_player, (float)dt, move.Forward);
        }
        else
        {
            _worldBridge.TimeCycle.AdvanceSeconds(dt);
            _worldBridge.StepWorldParticlesAndFuseAudio(_player, (float)dt);
            if (prevRidingCart >= 0 && _player.RidingMinecartId < 0)
            {
                _transport.EnqueueOutbound(new PlayerWorldInteractionPacket(PlayerWorldInteractionKind.MountOrDismountMinecart, _player.FeetPosition, Vec3d.Zero, 0, 0));
            }
            else if (_player.RidingMinecartId >= 0 && MathF.Abs(move.Forward) > 0.05f)
            {
                var hFwd = _player.Camera.HorizontalForward;
                _transport.EnqueueOutbound(new PlayerWorldInteractionPacket(
                    PlayerWorldInteractionKind.SteerMinecart, _player.FeetPosition, new Vec3d(hFwd.X, 0, hFwd.Z), 0, move.Forward > 0 ? 1 : -1));
            }
        }

        var (leftClicked, rightClicked, middlePicked, dropCount) = _inputBinder.ConsumeActionRequests();
        var leftHeld = _inputBinder.IsLeftMouseHeld(silkRenderer.InputContext);
        var rightHeld = _inputBinder.IsRightMouseHeld(silkRenderer.InputContext);

        if (_menu.IsGameplayActive)
        {
            if (middlePicked) _ = TryPickTargetedBlock();
            if (dropCount > 0) _ = TryDropSelectedItem(dropCount);

            _survivalCombat.StepBowAndShield(dt, rightHeld, _player, _worldBridge, _transport);
            var swungMelee = leftClicked && _survivalCombat.TryExecuteMeleeAttack(_player, _mobManager, _remotePlayers, _transport, _worldBridge);
            if (!swungMelee) _survivalCombat.StepMining(dt, leftClicked, leftHeld, _player, _worldCache, ApplyWorldBlockChange, _worldBridge, _transport.IsConnected);
            if (rightClicked && !_player.Hotbar.SelectedIsBow) _ = TryPlaceSelectedBlock();
        }
        else
        {
            _survivalCombat.StepBowAndShield(dt, rightMouseHeld: false, _player, _worldBridge, _transport);
            _player.MiningProgress = 0.0f;
        }

        EnqueueOutboundMovementAndEquipment(dt);
        _meshQueue.DispatchAndUpload(_worldCache, silkRenderer, playerChunk);
    }

    private FrameOverlayState BuildOverlayState(Vector2 viewport)
    {
        var isMp = _transport.IsConnected;
        var skyMul = _worldBridge.TimeCycle.SkyBrightness;
        return _menu.BuildOverlayState(
            viewport,
            _player,
            _worldCache,
            _remotePlayers.BuildSnapshot(_worldCache, skyMul, _player.Camera.Position, _player.Hotbar.CombinedHeldLightEmission, _registries.Items, _atlas),
            _worldBridge.BuildMobVisuals(_player, isMp),
            _worldBridge.BuildPrimedTntVisuals(_player, isMp),
            _worldBridge.TimeCycle.TotalTicks,
            skyMul,
            _chatHistory,
            _latestDebugText,
            _worldBridge.BuildMinecartVisuals(isMp),
            _worldBridge.BuildArrowVisuals(isMp),
            _worldBridge.BuildDroppedItemVisuals(_player),
            _worldBridge.BuildFallingBlockVisuals(_player, isMp),
            _worldBridge.BuildParticleVisuals(),
            _worldBridge.BuildHudStatusEffects(_player));
    }

    private async ValueTask StepFrameAsync(double deltaSeconds, (float Forward, float Strafe, bool Jump, bool Sprint) movement, CancellationToken ct)
    {
        DrainInboundPackets();
        if (!_transport.IsConnected) await StreamLocalWorldStepAsync(maxChunks: 12, ct).ConfigureAwait(false);
        _menu.AdvanceAnimations(deltaSeconds, _player);
        _survivalCombat.StepTimers(deltaSeconds);
        _player.StepMovement(_worldCache, _blocks, _audio, movement, deltaSeconds);
        if (!_transport.IsConnected) _worldBridge.StepWorldSimulationAndMobs(_player, (float)deltaSeconds);
        _remotePlayers.StepInterpolation(deltaSeconds);
        EnqueueOutboundMovementAndEquipment(deltaSeconds);
        _ = await _worldCache.RebuildDirtyMeshesAsync(_renderer, maxColumns: _options.Headless ? 2 : 16, ct).ConfigureAwait(false);
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
                packet, _transport.AssignedEntityId, _remotePlayers, _player, _worldCache, _worldBridge, _blocks, _registries.Items, _audio, _renderer,
                _chatHistory, _logger, _menu.ActiveViewDistance, ref _receivedInitialSpawnSync, ref _lastSentFeetPosition);
        }
    }

    private void EnqueueOutboundMovementAndEquipment(double dt)
    {
        if (!_transport.IsConnected)
        {
            _receivedInitialSpawnSync = false;
            return;
        }

        _moveSendTimer += dt;
        if (_moveSendTimer >= 0.05)
        {
            _moveSendTimer = 0.0;
            var posDeltaSq = (_player.FeetPosition - _lastSentFeetPosition).LengthSquared;
            var rotChanged = MathF.Abs(_player.Camera.YawDegrees - _lastSentYaw) > 0.35f || MathF.Abs(_player.Camera.PitchDegrees - _lastSentPitch) > 0.35f;
            if (posDeltaSq > 0.0004 || rotChanged)
            {
                _lastSentFeetPosition = _player.FeetPosition;
                _lastSentYaw = _player.Camera.YawDegrees;
                _lastSentPitch = _player.Camera.PitchDegrees;
                _transport.EnqueueOutbound(new MovePlayerPacket(_player.FeetPosition, _lastSentYaw, _lastSentPitch, _player.OnGround));
            }
        }

        _equipSendTimer += dt;
        var swingChanged = _player.SwingCounter != _lastSentSwingCounter;
        var cartChanged = _player.RidingMinecartId != _lastSyncedRidingCartId;
        if (_equipSendTimer >= 0.10 || swingChanged || cartChanged)
        {
            _equipSendTimer = 0.0;
            _lastSentSwingCounter = _player.SwingCounter;
            _lastSyncedRidingCartId = _player.RidingMinecartId;

            var armor = _player.Hotbar.BuildLocalArmorVisual();
            var packedLower = (byte)((armor.HelmetTier & 0x0F) | ((armor.ChestplateTier & 0x0F) << 4));
            var packedUpper = (byte)((armor.LeggingsTier & 0x0F) | ((armor.BootsTier & 0x0F) << 4));
            byte flags = 0;
            if (_player.IsBlockingShield) flags |= 1;
            if (_player.BowChargeNormalized > 0.01f) flags |= 2;
            if (_player.IsSprinting) flags |= 4;
            if (_player.LastSwingWasOffhand) flags |= 8;
            if (_player.IsSneaking) flags |= 16;

            var mainId = _player.Hotbar.SurvivalInventory.GetSlot(_player.Hotbar.SelectedSlot).ItemId;
            var offId = _player.Hotbar.SurvivalInventory.OffhandItem.ItemId;
            var bowByte = (byte)Math.Clamp((int)MathF.Round(_player.BowChargeNormalized * 255.0f), 0, 255);
            _transport.EnqueueOutbound(new PlayerEquipmentAndModePacket(
                (byte)_player.Mode, mainId, offId, packedLower, packedUpper, flags, bowByte, _player.SwingCounter));
        }
    }
}
