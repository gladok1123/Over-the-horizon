using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using OverHorizon.World;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Drives the voxel client runtime across both multiplayer and local singleplayer sessions,
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
    private readonly ClientInputBinder _inputBinder;
    private readonly ClientRemotePlayerTracker _remotePlayers;
    private readonly ClientAsyncMeshQueue _meshQueue;
    private readonly ClientMultiplayerTransport _transport = new();

    private CancellationTokenSource? _sessionCts;
    private double _lastFps = 60.0;
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

        _atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        _renderer.UploadAtlas(_atlas);
        _worldCache = new ClientWorldCache(blocks, _atlas);
        _player = new ClientPlayerController(new Vec3d(0.5, 80.0, 0.5), blocks);
        _player.Camera.SetAspectRatio(1280, 720);
        _inputBinder = new ClientInputBinder(_player, _audio, _logger, () => _latestDebugText);
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
    /// or primes the local singleplayer world around spawn.
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
            var spawnY = centerSnap.GetHeight(HeightmapType.MotionBlocking, 0, 0);
            _player.ApplyAuthoritativePose(new Vec3d(0.5, spawnY, 0.5), 0.0f, 0.0f);
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

        if (_transport.IsConnected)
        {
            _transport.EnqueueOutbound(
                new PlayerBlockActionPacket(PlayerBlockActionKind.BreakBlock, target.BlockPosition, target.HitFace, 0));
            return true;
        }

        if (!_localWorld.SetBlockState(target.BlockPosition, BlockState.Air))
        {
            return false;
        }

        SyncLocalSnapshots();
        var center = new Vec3d(target.BlockPosition.X + 0.5, target.BlockPosition.Y + 0.5, target.BlockPosition.Z + 0.5);
        _audio.PlayPositional(SoundCue.BlockBreak, center, gain: 0.7f);
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

        var stateToPlace = _player.Hotbar.SelectedBlockState;
        if (_transport.IsConnected)
        {
            _transport.EnqueueOutbound(
                new PlayerBlockActionPacket(PlayerBlockActionKind.PlaceBlock, placePos, target.HitFace, stateToPlace.Id));
            return true;
        }

        if (!_localWorld.SetBlockState(placePos, stateToPlace))
        {
            return false;
        }

        SyncLocalSnapshots();
        var center = new Vec3d(placePos.X + 0.5, placePos.Y + 0.5, placePos.Z + 0.5);
        _audio.PlayPositional(SoundCue.BlockPlace, center, gain: 0.7f);
        _player.RefreshRaycastTarget(_worldCache);
        return true;
    }

    /// <summary>
    /// Sends a chat message or slash command (e.g. <c>/help</c>, <c>/seed</c>, <c>/tp</c>) when connected to multiplayer.
    /// </summary>
    public void SendChatOrCommand(string message)
    {
        if (_transport.IsConnected && !string.IsNullOrWhiteSpace(message))
        {
            _transport.EnqueueOutbound(new ChatMessagePacket(message));
        }
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
            lastMetrics = _renderer.RenderFrame(_player.Camera, overlay: BuildOverlayState());
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
            onUpdate: dt =>
            {
                _inputBinder.EnsureBound(silkRenderer.InputContext);
                var movement = ClientInputBinder.PollMovement(silkRenderer.InputContext);
                DrainInboundPackets();

                if (!_transport.IsConnected)
                {
                    SyncLocalSnapshots();
                }

                _player.StepMovement(_worldCache, _blocks, _audio, movement, dt);
                var (brk, plc) = _inputBinder.ConsumeBlockActionRequests();
                if (brk)
                {
                    _ = TryBreakTargetedBlock();
                }

                if (plc)
                {
                    _ = TryPlaceSelectedBlock();
                }

                EnqueueOutboundMovementIfChanged();
                _meshQueue.DispatchAndUpload(_worldCache, silkRenderer);
            },
            onRender: dt =>
            {
                var metrics = silkRenderer.RenderFrame(_player.Camera, overlay: BuildOverlayState());
                RefreshDebugTelemetry(metrics, dt);

                var slotNum = _player.Hotbar.SelectedSlot + 1;
                var blockName = _player.Hotbar.SelectedBlockKey.Path;
                silkRenderer.WindowTitle = _player.IsDebugOverlayVisible
                    ? $"Over-the-Horizon | {_lastFps:F0} FPS | ({_player.FeetPosition.X:F1}, {_player.FeetPosition.Y:F1}, {_player.FeetPosition.Z:F1}) | [{slotNum}: {blockName}] | Friends: {_remotePlayers.Count}"
                    : $"Over-the-Horizon — [{slotNum}: {blockName}] (WASD+Space, LMB=Break, RMB=Place, 1-9=Block, F3=Info, Esc=Mouse)";
            },
            onResize: (w, h) => _player.Camera.SetAspectRatio(w, h),
            cancellationToken: cancellationToken);

        await _meshQueue.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_sessionCts is not null)
        {
            await _sessionCts.CancelAsync().ConfigureAwait(false);
        }

        await _meshQueue.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);

        _sessionCts?.Dispose();
        _sessionCts = null;
    }

    private FrameOverlayState BuildOverlayState() =>
        new(
            _player.CurrentTarget?.BlockPosition,
            _player.Hotbar.SelectedSlot,
            ClientHotbar.SlotColors,
            _remotePlayers.BuildSnapshot());

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
        EnqueueOutboundMovementIfChanged();
        _ = await _worldCache.RebuildDirtyMeshesAsync(_renderer, maxColumns: 16, ct).ConfigureAwait(false);
    }

    private async ValueTask StreamLocalWorldStepAsync(int maxChunks, CancellationToken ct)
    {
        var centerChunk = _player.FeetPosition.ToBlockPos().ToChunkPos();
        var radius = Math.Clamp(_options.ViewDistanceChunks, 1, 16);
        _localWorld.Chunks.UpsertTicket(new ChunkTicket(LocalTicketOwnerId, ChunkTicketType.Player, centerChunk, radius));
        _ = await _localWorld.Chunks.ProcessTicketsAsync(maxChunks, ct).ConfigureAwait(false);
        SyncLocalSnapshots();
    }

    private void SyncLocalSnapshots()
    {
        var center = _player.FeetPosition.ToBlockPos().ToChunkPos();
        var radius = Math.Clamp(_options.ViewDistanceChunks, 1, 16);

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
    }

    private void DrainInboundPackets()
    {
        while (_transport.TryReadInbound(out var packet))
        {
            ApplyInboundPacket(packet);
        }
    }

    private void ApplyInboundPacket(IClientboundPacket packet)
    {
        if (_remotePlayers.TryApply(packet))
        {
            return;
        }

        switch (packet)
        {
            case SynchronizePlayerPositionPacket syncPos:
                _player.ApplyAuthoritativePose(syncPos.Position, syncPos.YawDegrees, syncPos.PitchDegrees);
                _lastSentFeetPosition = syncPos.Position;
                break;

            case ChunkDataPacket chunkPkt:
                var col = ChunkNetworkSerializer.DeserializeColumn(chunkPkt.Position, chunkPkt.Payload.Span, _blocks);
                _worldCache.UpsertColumn(col);
                break;

            case BlockUpdatePacket blockPkt:
                var newState = new BlockState(blockPkt.BlockStateId);
                if (_worldCache.ApplyBlockUpdate(blockPkt.Position, newState))
                {
                    var soundPos = new Vec3d(blockPkt.Position.X + 0.5, blockPkt.Position.Y + 0.5, blockPkt.Position.Z + 0.5);
                    _audio.PlayPositional(newState.IsAir ? SoundCue.BlockBreak : SoundCue.BlockPlace, soundPos, gain: 0.7f);
                }

                break;

            case SystemChatPacket chatPkt:
                ClientLog.SystemChatReceived(_logger, chatPkt.Message);
                break;
        }
    }

    private void EnqueueOutboundMovementIfChanged()
    {
        if (!_transport.IsConnected)
        {
            return;
        }

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
            BackendName: _renderer.BackendName,
            FramesPerSecond: _lastFps,
            FrameTimeMilliseconds: dt * 1000.0,
            PlayerPosition: _player.FeetPosition,
            Facing: _player.Camera.CardinalFacing,
            YawDegrees: _player.Camera.YawDegrees,
            PitchDegrees: _player.Camera.PitchDegrees,
            Biome: BuiltInBiomes.Plains,
            VisibleSections: metrics.VisibleSections,
            TotalMeshedSections: _renderer.UploadedSectionCount,
            DrawnVertices: metrics.TrianglesSubmitted * 3,
            ManagedMemoryBytes: GC.GetTotalMemory(forceFullCollection: false));

        _latestDebugText = F3OverlayFormatter.Format(in snapshot);
    }
}
