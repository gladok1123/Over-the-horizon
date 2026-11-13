using System.Numerics;
using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using OverHorizon.World;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Drives the voxel client runtime across Title Screen ("C# EDITION"), Creative Flight, <c>0..15</c> Dynamic Lighting,
/// <c>2x2</c>/<c>3x3</c> Crafting &amp; Furnace Smelting, 3D Peaceful/Hostile Mobs, up-to-64-chunk streaming,
/// and Multiplayer PvP sessions over Silk.NET OpenGL or headless simulation frames.
/// </summary>
public sealed class ClientSessionRunner : IAsyncDisposable
{
    private const long LocalTicketOwnerId = 1L;

    private readonly ClientOptions _options;
    private readonly IBlockRegistry _blocks;
    private readonly IWorldSubsystem _localWorld;
    private readonly IRenderSubsystem _renderer;
    private readonly IAudioSubsystem _audio;
    private readonly ILogger<ClientSessionRunner> _logger;
    private readonly TextureAtlasData _atlas;
    private readonly ClientWorldCache _worldCache;
    private readonly ClientPlayerController _player;
    private readonly ClientMenuController _menu;
    private readonly ClientChatHistory _chatHistory = new();
    private readonly ClientMenuSessionActions _menuActions;
    private readonly ClientInputBinder _inputBinder;
    private readonly ClientRemotePlayerTracker _remotePlayers;
    private readonly WorldMobManager _mobManager = new();
    private readonly ClientWorldSyncAndMobBridge _worldBridge;
    private readonly ClientSurvivalCombatController _survivalCombat;
    private readonly ClientAsyncMeshQueue _meshQueue;

    private ClientMultiplayerTransport _transport = new();
    private CancellationTokenSource? _sessionCts;
    private Task? _backgroundWorldStreamTask;
    private double _lastFps = 60.0;
    private double _localSpawnY = 76.0;
    private double _moveSendTimer;
    private bool _receivedInitialSpawnSync;
    private Vec3d _lastSentFeetPosition;
    private float _lastSentYaw;
    private float _lastSentPitch;
    private string _latestDebugText = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="ClientSessionRunner"/> class.</summary>
    public ClientSessionRunner(
        ClientOptions options,
        CoreRegistries registries,
        IBlockRegistry blocks,
        IWorldSubsystem localWorld,
        IRenderSubsystem renderer,
        IAudioSubsystem audio,
        ILogger<ClientSessionRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(registries);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(localWorld);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _blocks = blocks;
        _localWorld = localWorld;
        _renderer = renderer;
        _audio = audio;
        _logger = logger;

        var startDirectlyInGame = options.Headless || options.StartInWorld || !string.IsNullOrWhiteSpace(options.ServerHost);
        var initialMode = startDirectlyInGame ? UiOverlayMode.InGame : UiOverlayMode.TitleMenu;

        _atlas = ProceduralAtlasGenerator.CreateDefault(blocks, options.ExternalAssetsPath);
        _renderer.UploadAtlas(_atlas);
        _worldCache = new ClientWorldCache(blocks, _atlas);
        _player = new ClientPlayerController(new Vec3d(0.5, 80.0, 0.5), blocks, registries.Items);
        _player.Hotbar.BindAtlas(_atlas);
        _player.Camera.SetAspectRatio(1280, 720);

        _menu = new ClientMenuController(options, initialMode, registries.Items, registries.Recipes);
        _menuActions = new ClientMenuSessionActions(_menu, _chatHistory, _localWorld, _logger);
        _inputBinder = new ClientInputBinder(
            _player, _menu, _audio, _logger, () => _latestDebugText, () => _renderer is SilkOpenGlRenderSubsystem s ? s.WindowPixelSize : new Vector2(1280, 720));
        _remotePlayers = new ClientRemotePlayerTracker(_logger);
        _worldBridge = new ClientWorldSyncAndMobBridge(_blocks, _localWorld, _worldCache, _renderer, _audio, _mobManager);
        _survivalCombat = new ClientSurvivalCombatController(blocks, registries.Items, _audio);
        _meshQueue = new ClientAsyncMeshQueue(blocks, _atlas);
    }

    /// <summary>Gets the client-side world and mesh cache.</summary>
    public ClientWorldCache WorldCache => _worldCache;

    /// <summary>Gets the local first-person player controller.</summary>
    public ClientPlayerController Player => _player;

    /// <summary>Gets the singleplayer 3D peaceful/hostile mob manager.</summary>
    public WorldMobManager Mobs => _mobManager;

    /// <summary>Gets the block &amp; item texture atlas uploaded to the renderer.</summary>
    public TextureAtlasData Atlas => _atlas;

    /// <summary>Gets the number of remote multiplayer friends currently visible to this client.</summary>
    public int RemotePlayerCount => _remotePlayers.Count;

    /// <summary>Gets the most recently formatted F3 debug telemetry text.</summary>
    public string LatestDebugOverlayText => _latestDebugText;

    /// <summary>Connects to a remote TCP multiplayer server or primes the local world around spawn.</summary>
    public async ValueTask InitializeSessionAsync(CancellationToken cancellationToken = default)
    {
        _ = _audio.TryInitializeDevice();
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.ServerHost))
        {
            var login = await _transport.ConnectAndLoginAsync(
                _options.ServerHost, _options.ServerPort, _options.Username, cancellationToken).ConfigureAwait(false);
            ClientLog.MultiplayerConnected(_logger, _options.ServerHost, _options.ServerPort, login.Username, login.AssignedEntityId);
            return;
        }

        await StreamLocalWorldStepAsync(maxChunks: 25, cancellationToken).ConfigureAwait(false);
        if (_localWorld.Chunks.TryGetSnapshot(new ChunkPos(0, 0), out var centerSnap))
        {
            _localSpawnY = centerSnap.GetHeight(HeightmapType.MotionBlocking, 0, 0);
            _player.ApplyAuthoritativePose(new Vec3d(0.5, _localSpawnY, 0.5), 0.0f, 0.0f);
        }

        if (!_options.Headless)
        {
            _worldBridge.SeedInitialStarterMobs();
            if (_menu.Mode == UiOverlayMode.InGame)
            {
                _chatHistory.AddMessage("Welcome to Over-the-Horizon: C# Edition v0.5.0! E: Inv/Craft | F: Fly | F4: Mode.");
            }
        }
    }

    /// <summary>Breaks the block currently targeted by the player's crosshair.</summary>
    public bool TryBreakTargetedBlock()
    {
        if (_player.CurrentTarget is not { } target) return false;
        var existing = _worldCache.GetBlockState(target.BlockPosition);
        if (existing.IsAir || existing == _blocks.GetDefaultState(BuiltInBlocks.Bedrock)) return false;

        _player.TriggerHandSwing();
        return ApplyWorldBlockChange(target.BlockPosition, target.HitFace, BlockState.Air);
    }

    /// <summary>Uses the selected item or places the selected block.</summary>
    public bool TryPlaceSelectedBlock()
    {
        var used = _survivalCombat.HandleRightClickUseOrPlace(
            _player, _worldCache, _mobManager, ApplyWorldBlockChange, out var openContainer, out var containerPos);

        if (openContainer is { } containerMode)
        {
            _menu.OpenContainerScreen(containerMode, containerPos);
        }

        return used;
    }

    /// <summary>Sends a chat message or slash command.</summary>
    public void SendChatOrCommand(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var trimmed = message.Trim();
        if (trimmed.StartsWith("/gamemode", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("/gm", StringComparison.OrdinalIgnoreCase))
        {
            var toSurvival = trimmed.Contains("survival", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith(" s", StringComparison.OrdinalIgnoreCase) || trimmed.EndsWith(" 0", StringComparison.Ordinal);
            var toCreative = trimmed.Contains("creative", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith(" c", StringComparison.OrdinalIgnoreCase) || trimmed.EndsWith(" 1", StringComparison.Ordinal);

            var nextMode = _player.Mode == GameMode.Creative ? GameMode.Survival : GameMode.Creative;
            if (toSurvival) nextMode = GameMode.Survival;
            else if (toCreative) nextMode = GameMode.Creative;

            _player.SetGameMode(nextMode);
            _chatHistory.AddMessage($"[System] Set own game mode to {nextMode} Mode.");
            return;
        }

        if (_transport.IsConnected)
        {
            _transport.EnqueueOutbound(new ChatMessagePacket(message));
            return;
        }

        _chatHistory.AddMessage($"<{_menu.UsernameInput}> {message}");
    }

    /// <summary>Executes <paramref name="frameCount"/> deterministic headless simulation and render frames.</summary>
    public async ValueTask<RenderFrameMetrics> RunHeadlessFramesAsync(
        int frameCount,
        double deltaSeconds = 1.0 / 60.0,
        float moveForward = 0.0f,
        CancellationToken cancellationToken = default)
    {
        RenderFrameMetrics lastMetrics = default;
        for (var i = 0; i < frameCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await StepFrameAsync(deltaSeconds, (moveForward, 0.0f, false, false), cancellationToken).ConfigureAwait(false);
            lastMetrics = _renderer.RenderFrame(_player.Camera, overlay: BuildOverlayState(new Vector2(1280, 720)));
            RefreshDebugTelemetry(lastMetrics, deltaSeconds);
        }

        return lastMetrics;
    }

    /// <summary>Runs the interactive Silk.NET OpenGL 3.3 window loop.</summary>
    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        if (_options.Headless || _renderer is not SilkOpenGlRenderSubsystem silkRenderer)
        {
            var frames = _options.MaxFrames ?? 1;
            _ = await RunHeadlessFramesAsync(frames, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var sessionToken = _sessionCts?.Token ?? cancellationToken;
        _meshQueue.Start(sessionToken);

        silkRenderer.RunWindowLoop(
            onUpdate: dt => UpdateInteractiveFrame(silkRenderer, dt, sessionToken),
            onRender: dt =>
            {
                var metrics = silkRenderer.RenderFrame(_player.Camera, overlay: BuildOverlayState(silkRenderer.WindowPixelSize));
                RefreshDebugTelemetry(metrics, dt);
                var flyTag = _player.IsFlying ? " [FLY]" : string.Empty;
                silkRenderer.WindowTitle = _menu.Mode == UiOverlayMode.TitleMenu
                    ? "Over-the-Horizon: C# EDITION (v0.5.0-alpha)"
                    : $"Over-the-Horizon: C# EDITION | {_lastFps:F0} FPS | {_player.Mode}{flyTag} | RD:{_menu.ActiveViewDistance} | [{_player.Hotbar.SelectedSlot + 1}: {_player.Hotbar.SelectedBlockDisplayName}]";
            },
            onResize: (w, h) => _player.Camera.SetAspectRatio(w, h),
            cancellationToken: cancellationToken);

        if (_backgroundWorldStreamTask is not null) await _backgroundWorldStreamTask.ConfigureAwait(false);
        if (!_transport.IsConnected) _ = await _localWorld.Chunks.SaveDirtyChunksAsync(CancellationToken.None).ConfigureAwait(false);
        await _meshQueue.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_sessionCts is not null) await _sessionCts.CancelAsync().ConfigureAwait(false);
        await _menuActions.AwaitPendingAsync().ConfigureAwait(false);
        await _meshQueue.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
        _menuActions.Dispose();
        _sessionCts?.Dispose();
        _sessionCts = null;
    }

    private bool ApplyWorldBlockChange(BlockPos targetPos, Direction face, BlockState newState)
    {
        if (newState.IsAir)
        {
            var brokenKey = _blocks.GetStateMetadata(_worldCache.GetBlockState(targetPos)).BlockKey;
            if (brokenKey == BuiltInBlocks.Furnace || brokenKey == BuiltInBlocks.LitFurnace)
            {
                _menu.RemoveFurnaceAndDropContents(targetPos, _player.Hotbar.SurvivalInventory);
                _player.Hotbar.RefreshSlotVisuals(_player.Mode);
            }
        }

        if (_transport.IsConnected)
        {
            _ = _worldCache.ApplyBlockUpdate(targetPos, newState);
            var actionKind = newState.IsAir ? PlayerBlockActionKind.BreakBlock : PlayerBlockActionKind.PlaceBlock;
            _transport.EnqueueOutbound(new PlayerBlockActionPacket(actionKind, targetPos, face, newState.Id));
        }
        else if (!_localWorld.SetBlockState(targetPos, newState))
        {
            return false;
        }
        else
        {
            var centerChunk = _player.FeetPosition.ToBlockPos().ToChunkPos();
            _worldBridge.SyncLocalSnapshots(centerChunk, _menu.ActiveViewDistance, fullImmediateRadius: 2);
        }

        var center = new Vec3d(targetPos.X + 0.5, targetPos.Y + 0.5, targetPos.Z + 0.5);
        _audio.PlayPositional(newState.IsAir ? SoundCue.BlockBreak : SoundCue.BlockPlace, center, gain: 0.7f * _player.MasterVolume);
        _player.RefreshRaycastTarget(_worldCache);
        return true;
    }

    private void UpdateInteractiveFrame(SilkOpenGlRenderSubsystem silkRenderer, double dt, CancellationToken sessionToken)
    {
        _inputBinder.EnsureBoundAndSyncCursor(silkRenderer.InputContext);
        _menuActions.DispatchPendingActions(
            _inputBinder, silkRenderer, _player, _localSpawnY, SendChatOrCommand, () => _transport, t => _transport = t, sessionToken);
        DrainInboundPackets();

        _menu.AdvanceAnimations(dt, _player);
        _survivalCombat.StepTimers(dt);
        var playerChunk = _player.FeetPosition.ToBlockPos().ToChunkPos();

        if (_menu.Mode == UiOverlayMode.TitleMenu)
        {
            _player.Camera.Position = new Vec3d(0.5, _localSpawnY + 14.0, 0.5);
            _player.Camera.SetRotation(_menu.PanoramaYawDegrees, -12.0f);
            _meshQueue.DispatchAndUpload(_worldCache, silkRenderer, playerChunk);
            return;
        }

        if (!_transport.IsConnected && (_backgroundWorldStreamTask is null || _backgroundWorldStreamTask.IsCompleted))
        {
            var radius = Math.Clamp(_menu.ActiveViewDistance, 1, 64);
            var loadBatch = Math.Clamp(4 + (radius / 3), 6, 28);
            _localWorld.Chunks.UpsertTicket(new ChunkTicket(LocalTicketOwnerId, ChunkTicketType.Player, playerChunk, radius));
            _backgroundWorldStreamTask = _localWorld.Chunks.ProcessTicketsAsync(loadBatch, sessionToken).AsTask();
            _worldBridge.SyncLocalSnapshots(playerChunk, radius);
        }

        var (move, descend) = _inputBinder.PollMovement(silkRenderer.InputContext);
        _player.StepMovement(_worldCache, _blocks, _audio, move, dt, descend);
        _remotePlayers.StepInterpolation(dt);
        if (!_transport.IsConnected) _worldBridge.StepWorldMobs(_player, (float)dt);

        var (leftClicked, rightClicked) = _inputBinder.ConsumeActionRequests();
        var leftHeld = _inputBinder.IsLeftMouseHeld(silkRenderer.InputContext);

        if (_menu.IsGameplayActive)
        {
            var swungMelee = leftClicked && _survivalCombat.TryExecuteMeleeAttack(_player, _mobManager, _remotePlayers, _transport);
            if (!swungMelee) _survivalCombat.StepMining(dt, leftClicked, leftHeld, _player, _worldCache, ApplyWorldBlockChange);
            if (rightClicked) _ = TryPlaceSelectedBlock();
        }
        else
        {
            _player.MiningProgress = 0.0f;
        }

        EnqueueOutboundMovementIfChanged(dt);
        _meshQueue.DispatchAndUpload(_worldCache, silkRenderer, playerChunk);
    }

    private FrameOverlayState BuildOverlayState(Vector2 viewport) =>
        _menu.BuildOverlayState(
            viewport, _player, _worldCache, _remotePlayers.BuildSnapshot(), _worldBridge.BuildMobVisuals(_player), _chatHistory, _latestDebugText);

    private async ValueTask StepFrameAsync(double deltaSeconds, (float Forward, float Strafe, bool Jump, bool Sprint) movement, CancellationToken ct)
    {
        DrainInboundPackets();
        if (!_transport.IsConnected) await StreamLocalWorldStepAsync(maxChunks: 12, ct).ConfigureAwait(false);
        _menu.AdvanceAnimations(deltaSeconds, _player);
        _survivalCombat.StepTimers(deltaSeconds);
        _player.StepMovement(_worldCache, _blocks, _audio, movement, deltaSeconds);
        _remotePlayers.StepInterpolation(deltaSeconds);
        EnqueueOutboundMovementIfChanged(deltaSeconds);
        _ = await _worldCache.RebuildDirtyMeshesAsync(_renderer, maxColumns: 16, ct).ConfigureAwait(false);
    }

    private async ValueTask StreamLocalWorldStepAsync(int maxChunks, CancellationToken ct)
    {
        var centerChunk = _player.FeetPosition.ToBlockPos().ToChunkPos();
        var radius = Math.Clamp(_menu.ActiveViewDistance, 1, 64);
        _localWorld.Chunks.UpsertTicket(new ChunkTicket(LocalTicketOwnerId, ChunkTicketType.Player, centerChunk, radius));
        _ = await _localWorld.Chunks.ProcessTicketsAsync(maxChunks, ct).ConfigureAwait(false);
        _worldBridge.SyncLocalSnapshots(centerChunk, radius);
    }

    private void DrainInboundPackets()
    {
        while (_transport.TryReadInbound(out var packet))
        {
            ClientInboundPacketApplier.Apply(
                packet, _transport.AssignedEntityId, _remotePlayers, _player, _worldCache, _blocks, _audio, _renderer,
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
        var rotChanged = MathF.Abs(_player.Camera.YawDegrees - _lastSentYaw) > 0.5f || MathF.Abs(_player.Camera.PitchDegrees - _lastSentPitch) > 0.5f;
        if (posDeltaSq <= 0.0004 && !rotChanged) return;

        _lastSentFeetPosition = _player.FeetPosition;
        _lastSentYaw = _player.Camera.YawDegrees;
        _lastSentPitch = _player.Camera.PitchDegrees;
        _transport.EnqueueOutbound(new MovePlayerPacket(_player.FeetPosition, _lastSentYaw, _lastSentPitch, _player.OnGround));
    }

    private void RefreshDebugTelemetry(RenderFrameMetrics metrics, double dt)
    {
        if (dt > 1e-6) _lastFps = (_lastFps * 0.85) + ((1.0 / dt) * 0.15);
        var snapshot = new F3DebugSnapshot(
            _renderer.BackendName, _lastFps, dt * 1000.0, _player.FeetPosition,
            _player.Camera.CardinalFacing, _player.Camera.YawDegrees, _player.Camera.PitchDegrees,
            BuiltInBiomes.Plains, metrics.VisibleSections, _renderer.UploadedSectionCount,
            metrics.TrianglesSubmitted * 3, GC.GetTotalMemory(forceFullCollection: false));
        _latestDebugText = F3OverlayFormatter.Format(in snapshot);
    }
}
