using System.Numerics;
using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
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
/// Drives the voxel client runtime across Title Screen ("C# EDITION"), Singleplayer, and Multiplayer sessions,
/// supporting interactive Silk.NET OpenGL windowing and headless simulation frames.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientSessionRunner"/> class.
    /// </summary>
    public ClientSessionRunner(
        ClientOptions options,
        IBlockRegistry blocks,
        IWorldSubsystem localWorld,
        IRenderSubsystem renderer,
        IAudioSubsystem audio,
        ILogger<ClientSessionRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
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
        _player = new ClientPlayerController(new Vec3d(0.5, 80.0, 0.5), blocks);
        _player.Camera.SetAspectRatio(1280, 720);
        _menu = new ClientMenuController(options, initialMode);
        _menuActions = new ClientMenuSessionActions(_menu, _chatHistory, _localWorld, _logger);
        _inputBinder = new ClientInputBinder(
            _player, _menu, _audio, _logger, () => _latestDebugText, () => _renderer is SilkOpenGlRenderSubsystem s ? s.WindowPixelSize : new Vector2(1280, 720));
        _remotePlayers = new ClientRemotePlayerTracker(_logger);
        _meshQueue = new ClientAsyncMeshQueue(blocks, _atlas);
    }

    /// <summary>Gets the client-side world and mesh cache.</summary>
    public ClientWorldCache WorldCache => _worldCache;

    /// <summary>Gets the local first-person player controller.</summary>
    public ClientPlayerController Player => _player;

    /// <summary>Gets the procedural texture atlas uploaded to the renderer.</summary>
    public TextureAtlasData Atlas => _atlas;

    /// <summary>Gets the number of remote multiplayer friends currently visible to this client.</summary>
    public int RemotePlayerCount => _remotePlayers.Count;

    /// <summary>Gets the most recently formatted F3 debug telemetry text.</summary>
    public string LatestDebugOverlayText => _latestDebugText;

    /// <summary>
    /// Connects to a remote TCP multiplayer server (when <see cref="ClientOptions.ServerHost"/> is set)
    /// or primes the local world around spawn.
    /// </summary>
    public async ValueTask InitializeSessionAsync(CancellationToken cancellationToken = default)
    {
        _ = _audio.TryInitializeDevice();
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.ServerHost))
        {
            var login = await _transport.ConnectAndLoginAsync(
                _options.ServerHost,
                _options.ServerPort,
                _options.Username,
                cancellationToken).ConfigureAwait(false);
            ClientLog.MultiplayerConnected(_logger, _options.ServerHost, _options.ServerPort, login.Username, login.AssignedEntityId);
            return;
        }

        await StreamLocalWorldStepAsync(maxChunks: 25, cancellationToken).ConfigureAwait(false);
        if (_localWorld.Chunks.TryGetSnapshot(new ChunkPos(0, 0), out var centerSnap))
        {
            _localSpawnY = centerSnap.GetHeight(HeightmapType.MotionBlocking, 0, 0);
            _player.ApplyAuthoritativePose(new Vec3d(0.5, _localSpawnY, 0.5), 0.0f, 0.0f);
        }

        if (!_options.Headless && _menu.Mode == UiOverlayMode.InGame)
        {
            _chatHistory.AddMessage("Welcome to Over-the-Horizon: C# Edition! Press E for Blocks, T for Chat.");
        }
    }

    /// <summary>
    /// Breaks the block currently targeted by the player's crosshair (singleplayer or multiplayer).
    /// </summary>
    public bool TryBreakTargetedBlock()
    {
        if (_player.CurrentTarget is not { } target)
        {
            return false;
        }

        var existing = _worldCache.GetBlockState(target.BlockPosition);
        if (existing.IsAir || existing == _blocks.GetDefaultState(BuiltInBlocks.Bedrock))
        {
            return false;
        }

        _player.TriggerHandSwing();
        if (_transport.IsConnected)
        {
            _ = _worldCache.ApplyBlockUpdate(target.BlockPosition, BlockState.Air);
            _transport.EnqueueOutbound(
                new PlayerBlockActionPacket(PlayerBlockActionKind.BreakBlock, target.BlockPosition, target.HitFace, 0));
        }
        else if (!_localWorld.SetBlockState(target.BlockPosition, BlockState.Air))
        {
            return false;
        }
        else
        {
            SyncLocalSnapshots();
        }

        var center = new Vec3d(target.BlockPosition.X + 0.5, target.BlockPosition.Y + 0.5, target.BlockPosition.Z + 0.5);
        _audio.PlayPositional(SoundCue.BlockBreak, center, gain: 0.7f * _player.MasterVolume);
        _player.RefreshRaycastTarget(_worldCache);
        return true;
    }

    /// <summary>
    /// Places the currently selected hotbar block onto the adjacent face of <see cref="ClientPlayerController.CurrentTarget"/>.
    /// </summary>
    public bool TryPlaceSelectedBlock()
    {
        if (_player.CurrentTarget is not { } target)
        {
            return false;
        }

        var placePos = target.AdjacentPosition;
        var playerBox = BoundingBox.FromEntityFeet(
            _player.FeetPosition,
            ClientPlayerController.PlayerWidth,
            ClientPlayerController.PlayerHeight).Inflate(-1e-4, -1e-4, -1e-4);

        if (playerBox.Intersects(BoundingBox.FromBlock(placePos)))
        {
            return false;
        }

        _player.TriggerHandSwing();
        var stateToPlace = _player.Hotbar.SelectedBlockState;
        if (_transport.IsConnected)
        {
            _ = _worldCache.ApplyBlockUpdate(placePos, stateToPlace);
            _transport.EnqueueOutbound(
                new PlayerBlockActionPacket(PlayerBlockActionKind.PlaceBlock, placePos, target.HitFace, stateToPlace.Id));
        }
        else if (!_localWorld.SetBlockState(placePos, stateToPlace))
        {
            return false;
        }
        else
        {
            SyncLocalSnapshots();
        }

        var center = new Vec3d(placePos.X + 0.5, placePos.Y + 0.5, placePos.Z + 0.5);
        _audio.PlayPositional(SoundCue.BlockPlace, center, gain: 0.7f * _player.MasterVolume);
        _player.RefreshRaycastTarget(_worldCache);
        return true;
    }

    /// <summary>
    /// Sends a chat message or slash command (in multiplayer or local singleplayer chat log).
    /// </summary>
    public void SendChatOrCommand(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (_transport.IsConnected)
        {
            _transport.EnqueueOutbound(new ChatMessagePacket(message));
            return;
        }

        _chatHistory.AddMessage($"<{_menu.UsernameInput}> {message}");
    }

    /// <summary>
    /// Executes <paramref name="frameCount"/> deterministic client update, parallel chunk-meshing, and render frames
    /// without opening an OS window.
    /// </summary>
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

    /// <summary>
    /// Runs the interactive Silk.NET OpenGL 3.3 window loop (or falls back to headless frames when configured headless).
    /// </summary>
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
                silkRenderer.WindowTitle = _menu.Mode == UiOverlayMode.TitleMenu
                    ? "Over-the-Horizon: C# EDITION (v0.3.0-alpha)"
                    : $"Over-the-Horizon: C# EDITION | {_lastFps:F0} FPS | [{_player.Hotbar.SelectedSlot + 1}: {_player.Hotbar.SelectedBlockDisplayName}] | Friends: {_remotePlayers.Count}";
            },
            onResize: (w, h) => _player.Camera.SetAspectRatio(w, h),
            cancellationToken: cancellationToken);

        if (_backgroundWorldStreamTask is not null)
        {
            await _backgroundWorldStreamTask.ConfigureAwait(false);
        }

        if (!_transport.IsConnected)
        {
            _ = await _localWorld.Chunks.SaveDirtyChunksAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await _meshQueue.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_sessionCts is not null)
        {
            await _sessionCts.CancelAsync().ConfigureAwait(false);
        }

        await _menuActions.AwaitPendingAsync().ConfigureAwait(false);
        await _meshQueue.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
        _menuActions.Dispose();

        _sessionCts?.Dispose();
        _sessionCts = null;
    }

    private void UpdateInteractiveFrame(SilkOpenGlRenderSubsystem silkRenderer, double dt, CancellationToken sessionToken)
    {
        _inputBinder.EnsureBoundAndSyncCursor(silkRenderer.InputContext);
        _menuActions.DispatchPendingActions(
            _inputBinder, silkRenderer, _player, _localSpawnY, SendChatOrCommand, () => _transport, t => _transport = t, sessionToken);
        DrainInboundPackets();

        _menu.AdvanceAnimations(dt);
        if (_menu.Mode == UiOverlayMode.TitleMenu)
        {
            _player.Camera.Position = new Vec3d(0.5, _localSpawnY + 14.0, 0.5);
            _player.Camera.SetRotation(_menu.PanoramaYawDegrees, -12.0f);
            _meshQueue.DispatchAndUpload(_worldCache, silkRenderer);
            return;
        }

        if (!_transport.IsConnected && (_backgroundWorldStreamTask is null || _backgroundWorldStreamTask.IsCompleted))
        {
            var centerChunk = _player.FeetPosition.ToBlockPos().ToChunkPos();
            _localWorld.Chunks.UpsertTicket(new ChunkTicket(LocalTicketOwnerId, ChunkTicketType.Player, centerChunk, _menu.ActiveViewDistance));
            _backgroundWorldStreamTask = _localWorld.Chunks.ProcessTicketsAsync(2, sessionToken).AsTask();
            SyncLocalSnapshots();
        }

        var movement = _inputBinder.PollMovement(silkRenderer.InputContext);
        _player.StepMovement(_worldCache, _blocks, _audio, movement, dt);
        _remotePlayers.StepInterpolation(dt);

        var (brk, plc) = _inputBinder.ConsumeBlockActionRequests();
        if (_menu.IsGameplayActive)
        {
            if (brk)
            {
                _ = TryBreakTargetedBlock();
            }

            if (plc)
            {
                _ = TryPlaceSelectedBlock();
            }
        }

        EnqueueOutboundMovementIfChanged(dt);
        _meshQueue.DispatchAndUpload(_worldCache, silkRenderer);
    }

    private FrameOverlayState BuildOverlayState(Vector2 viewport) =>
        _menu.BuildOverlayState(viewport, _player, _remotePlayers.BuildSnapshot(), _chatHistory, _latestDebugText);

    private async ValueTask StepFrameAsync(
        double deltaSeconds,
        (float Forward, float Strafe, bool Jump, bool Sprint) movement,
        CancellationToken ct)
    {
        DrainInboundPackets();
        if (!_transport.IsConnected)
        {
            await StreamLocalWorldStepAsync(maxChunks: 8, ct).ConfigureAwait(false);
        }

        _player.StepMovement(_worldCache, _blocks, _audio, movement, deltaSeconds);
        _remotePlayers.StepInterpolation(deltaSeconds);
        EnqueueOutboundMovementIfChanged(deltaSeconds);
        _ = await _worldCache.RebuildDirtyMeshesAsync(_renderer, maxColumns: 16, ct).ConfigureAwait(false);
    }

    private async ValueTask StreamLocalWorldStepAsync(int maxChunks, CancellationToken ct)
    {
        var centerChunk = _player.FeetPosition.ToBlockPos().ToChunkPos();
        var radius = Math.Clamp(_menu.ActiveViewDistance, 1, 16);
        _localWorld.Chunks.UpsertTicket(new ChunkTicket(LocalTicketOwnerId, ChunkTicketType.Player, centerChunk, radius));
        _ = await _localWorld.Chunks.ProcessTicketsAsync(maxChunks, ct).ConfigureAwait(false);
        SyncLocalSnapshots();
    }

    private void SyncLocalSnapshots()
    {
        var center = _player.FeetPosition.ToBlockPos().ToChunkPos();
        var radius = Math.Clamp(_menu.ActiveViewDistance, 1, 16);

        for (var dz = -radius; dz <= radius; dz++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var pos = new ChunkPos(center.X + dx, center.Z + dz);
                if (_localWorld.Chunks.TryGetSnapshot(pos, out var snap))
                {
                    _worldCache.UpsertSnapshot(snap);
                }
            }
        }

        _ = _worldCache.EvictOutOfRangeChunks(center, radius + 3, _renderer);
    }

    private void DrainInboundPackets()
    {
        while (_transport.TryReadInbound(out var packet))
        {
            ClientInboundPacketApplier.Apply(
                packet, _remotePlayers, _player, _worldCache, _blocks, _audio, _renderer, _chatHistory, _logger,
                _menu.ActiveViewDistance, ref _receivedInitialSpawnSync, ref _lastSentFeetPosition);
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
        if (_moveSendTimer < 0.05)
        {
            return;
        }

        _moveSendTimer = 0.0;
        var posDeltaSq = (_player.FeetPosition - _lastSentFeetPosition).LengthSquared;
        var rotChanged = MathF.Abs(_player.Camera.YawDegrees - _lastSentYaw) > 0.5f ||
            MathF.Abs(_player.Camera.PitchDegrees - _lastSentPitch) > 0.5f;

        if (posDeltaSq <= 0.0004 && !rotChanged)
        {
            return;
        }

        _lastSentFeetPosition = _player.FeetPosition;
        _lastSentYaw = _player.Camera.YawDegrees;
        _lastSentPitch = _player.Camera.PitchDegrees;
        _transport.EnqueueOutbound(
            new MovePlayerPacket(_player.FeetPosition, _lastSentYaw, _lastSentPitch, _player.OnGround));
    }

    private void RefreshDebugTelemetry(RenderFrameMetrics metrics, double dt)
    {
        if (dt > 1e-6)
        {
            _lastFps = (_lastFps * 0.85) + ((1.0 / dt) * 0.15);
        }

        var snapshot = new F3DebugSnapshot(
            _renderer.BackendName, _lastFps, dt * 1000.0, _player.FeetPosition,
            _player.Camera.CardinalFacing, _player.Camera.YawDegrees, _player.Camera.PitchDegrees,
            BuiltInBiomes.Plains, metrics.VisibleSections, _renderer.UploadedSectionCount,
            metrics.TrianglesSubmitted * 3, GC.GetTotalMemory(forceFullCollection: false));

        _latestDebugText = F3OverlayFormatter.Format(in snapshot);
    }
}
