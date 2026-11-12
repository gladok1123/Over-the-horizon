using System.Net;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay;
using OverHorizon.Networking.Client;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.World;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Serialization;

namespace OverHorizon.Client;

/// <summary>
/// Drives the voxel client runtime across both multiplayer (<see cref="JavaProtocolClient"/>) and local singleplayer
/// (<see cref="IWorldSubsystem"/>) sessions, supporting interactive Silk.NET OpenGL windowing and headless simulation frames.
/// </summary>
public sealed class ClientSessionRunner : IAsyncDisposable
{
    private const long LocalTicketOwnerId = 1L;

    private readonly ClientOptions _options;
    private readonly IBlockRegistry _blocks;
    private readonly IWorldSubsystem _localWorld;
    private readonly IGameplaySubsystem _gameplay;
    private readonly IRenderSubsystem _renderer;
    private readonly IAudioSubsystem _audio;
    private readonly ILogger<ClientSessionRunner> _logger;
    private readonly TextureAtlasData _atlas;
    private readonly ClientWorldCache _worldCache;
    private readonly ClientPlayerController _player;
    private readonly ClientInputBinder _inputBinder;
    private readonly Channel<IClientboundPacket> _inboundPackets = Channel.CreateUnbounded<IClientboundPacket>();
    private readonly Channel<IServerboundPacket> _outboundPackets = Channel.CreateUnbounded<IServerboundPacket>();
    private readonly Channel<IReadOnlyList<ChunkNeighborhood>> _meshJobs = Channel.CreateUnbounded<IReadOnlyList<ChunkNeighborhood>>();
    private readonly Channel<(ChunkPos Position, IReadOnlyList<SectionMeshData> Sections)> _completedMeshes =
        Channel.CreateUnbounded<(ChunkPos, IReadOnlyList<SectionMeshData>)>();

    private JavaProtocolClient? _networkClient;
    private Task? _networkReadTask;
    private Task? _networkWriteTask;
    private Task? _meshWorkerTask;
    private CancellationTokenSource? _sessionCts;
    private double _lastFps = 60.0;
    private Vec3d _lastSentFeetPosition;
    private string _latestDebugText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientSessionRunner"/> class.
    /// </summary>
    public ClientSessionRunner(
        ClientOptions options,
        IBlockRegistry blocks,
        IWorldSubsystem localWorld,
        IGameplaySubsystem gameplay,
        IRenderSubsystem renderer,
        IAudioSubsystem audio,
        ILogger<ClientSessionRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(localWorld);
        ArgumentNullException.ThrowIfNull(gameplay);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(audio);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _blocks = blocks;
        _localWorld = localWorld;
        _gameplay = gameplay;
        _renderer = renderer;
        _audio = audio;
        _logger = logger;

        _atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        _renderer.UploadAtlas(_atlas);
        _worldCache = new ClientWorldCache(blocks, _atlas);
        _player = new ClientPlayerController(new Vec3d(0.5, 80.0, 0.5));
        _player.Camera.SetAspectRatio(1280, 720);
        _inputBinder = new ClientInputBinder(_player, _logger, () => _latestDebugText);
    }

    /// <summary>Gets the client-side world and mesh cache.</summary>
    public ClientWorldCache WorldCache => _worldCache;

    /// <summary>Gets the local first-person player controller.</summary>
    public ClientPlayerController Player => _player;

    /// <summary>Gets the procedural texture atlas uploaded to the renderer.</summary>
    public TextureAtlasData Atlas => _atlas;

    /// <summary>Gets the most recently formatted F3 debug telemetry text.</summary>
    public string LatestDebugOverlayText => _latestDebugText;

    /// <summary>
    /// Connects to a remote TCP multiplayer server (when <see cref="ClientOptions.ServerHost"/> is set)
    /// or primes the local singleplayer world around spawn.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completion task.</returns>
    public async ValueTask InitializeSessionAsync(CancellationToken cancellationToken = default)
    {
        _ = _audio.TryInitializeDevice();
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.ServerHost))
        {
            var addresses = await Dns.GetHostAddressesAsync(_options.ServerHost, cancellationToken).ConfigureAwait(false);
            var endpoint = new IPEndPoint(addresses[0], _options.ServerPort);
            var client = await JavaProtocolClient.ConnectTcpAsync(endpoint, cancellationToken).ConfigureAwait(false);
            var login = await client.LoginAsync(_options.Username, cancellationToken: cancellationToken).ConfigureAwait(false);

            _networkClient = client;
            _networkReadTask = PumpServerPacketsAsync(client, _sessionCts.Token);
            _networkWriteTask = PumpOutboundPacketsAsync(client, _sessionCts.Token);

            ClientLog.MultiplayerConnected(_logger, _options.ServerHost, _options.ServerPort, login.Username, login.AssignedEntityId);
            return;
        }

        await StreamLocalWorldStepAsync(maxChunks: 25, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes <paramref name="frameCount"/> deterministic client update, parallel chunk-meshing, and render frames
    /// without opening an OS window (used by headless runs and integration tests).
    /// </summary>
    /// <param name="frameCount">Number of frames to simulate.</param>
    /// <param name="deltaSeconds">Simulated frame delta seconds.</param>
    /// <param name="moveForward">Optional forward movement wish in <c>[-1, 1]</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metrics from the final rendered frame.</returns>
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
            await StepFrameAsync(deltaSeconds, moveForward, 0.0f, jump: false, cancellationToken).ConfigureAwait(false);
            lastMetrics = _renderer.RenderFrame(_player.Camera);
            RefreshDebugTelemetry(lastMetrics, deltaSeconds);
        }

        return lastMetrics;
    }

    /// <summary>
    /// Runs the interactive Silk.NET OpenGL 3.3 window loop (or falls back to headless frames when configured headless).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Completion task.</returns>
    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        if (_options.Headless || _renderer is not SilkOpenGlRenderSubsystem silkRenderer)
        {
            var frames = _options.MaxFrames ?? 1;
            _ = await RunHeadlessFramesAsync(frames, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var sessionToken = _sessionCts?.Token ?? cancellationToken;
        _meshWorkerTask = PumpMeshJobsAsync(sessionToken);

        silkRenderer.RunWindowLoop(
            onUpdate: dt =>
            {
                _inputBinder.EnsureBound(silkRenderer.InputContext);
                var (fwd, strafe, jump) = ClientInputBinder.PollMovement(silkRenderer.InputContext);
                DrainInboundPackets();

                if (_networkClient is null)
                {
                    SyncLocalSnapshots();
                }

                _player.StepMovement(_worldCache, _blocks, _audio, fwd, strafe, jump, dt);
                EnqueueOutboundMovementIfChanged();
                DispatchAndUploadWindowMeshes(silkRenderer);
            },
            onRender: dt =>
            {
                var metrics = silkRenderer.RenderFrame(_player.Camera);
                RefreshDebugTelemetry(metrics, dt);

                if (_player.IsDebugOverlayVisible)
                {
                    silkRenderer.WindowTitle =
                        $"Over-the-Horizon | {_lastFps:F0} FPS | ({_player.FeetPosition.X:F1}, {_player.FeetPosition.Y:F1}, {_player.FeetPosition.Z:F1}) | Chunks: {_worldCache.LoadedChunkCount}";
                }
            },
            onResize: (w, h) => _player.Camera.SetAspectRatio(w, h),
            cancellationToken: cancellationToken);

        _meshJobs.Writer.TryComplete();
        await AwaitQuietlyAsync(_meshWorkerTask).ConfigureAwait(false);
        _meshWorkerTask = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _outboundPackets.Writer.TryComplete();
        _meshJobs.Writer.TryComplete();

        if (_sessionCts is not null)
        {
            await _sessionCts.CancelAsync().ConfigureAwait(false);
        }

        await AwaitQuietlyAsync(_networkReadTask).ConfigureAwait(false);
        await AwaitQuietlyAsync(_networkWriteTask).ConfigureAwait(false);
        await AwaitQuietlyAsync(_meshWorkerTask).ConfigureAwait(false);

        _sessionCts?.Dispose();
        _sessionCts = null;

        if (_networkClient is not null)
        {
            await _networkClient.DisposeAsync().ConfigureAwait(false);
            _networkClient = null;
        }
    }

    private static async ValueTask AwaitQuietlyAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal teardown cancellation.
        }
    }

    private async ValueTask StepFrameAsync(
        double deltaSeconds,
        float moveForward,
        float moveStrafe,
        bool jump,
        CancellationToken ct)
    {
        DrainInboundPackets();

        if (_networkClient is null)
        {
            await StreamLocalWorldStepAsync(maxChunks: 8, ct).ConfigureAwait(false);
        }

        _player.StepMovement(_worldCache, _blocks, _audio, moveForward, moveStrafe, jump, deltaSeconds);
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

    private async Task PumpServerPacketsAsync(JavaProtocolClient client, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var packet = await client.ReceivePacketAsync(ct).ConfigureAwait(false);
                if (packet is KeepAliveRequestPacket keepAlive)
                {
                    _ = _outboundPackets.Writer.TryWrite(new KeepAliveResponsePacket(keepAlive.KeepAliveId));
                    continue;
                }

                _ = _inboundPackets.Writer.TryWrite(packet);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ProtocolException)
        {
            _inboundPackets.Writer.TryComplete();
        }
    }

    private async Task PumpOutboundPacketsAsync(JavaProtocolClient client, CancellationToken ct)
    {
        try
        {
            await foreach (var packet in _outboundPackets.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await client.SendPlayPacketAsync(packet, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ProtocolException)
        {
            // Connection closed.
        }
    }

    private async Task PumpMeshJobsAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var batch in _meshJobs.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                foreach (var nb in batch)
                {
                    ct.ThrowIfCancellationRequested();
                    var sections = ParallelChunkMesher.MeshColumn(in nb, _blocks, _atlas);
                    _ = _completedMeshes.Writer.TryWrite((nb.Center.Position, sections));
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown requested.
        }
    }

    private void DispatchAndUploadWindowMeshes(SilkOpenGlRenderSubsystem silkRenderer)
    {
        var dirty = _worldCache.DequeueDirtyNeighborhoods(maxColumns: 4);
        if (dirty.Count > 0)
        {
            _ = _meshJobs.Writer.TryWrite(dirty);
        }

        while (_completedMeshes.Reader.TryRead(out var completed))
        {
            silkRenderer.RemoveChunkMeshes(completed.Position);
            foreach (var mesh in completed.Sections)
            {
                silkRenderer.UploadOrReplaceSectionMesh(mesh);
            }
        }
    }

    private void DrainInboundPackets()
    {
        while (_inboundPackets.Reader.TryRead(out var packet))
        {
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
            }
        }
    }

    private void EnqueueOutboundMovementIfChanged()
    {
        if (_networkClient is null || _player.FeetPosition.DistanceSquaredTo(_lastSentFeetPosition) <= 0.0004)
        {
            return;
        }

        _lastSentFeetPosition = _player.FeetPosition;
        _ = _outboundPackets.Writer.TryWrite(
            new MovePlayerPacket(_player.FeetPosition, _player.Camera.YawDegrees, _player.Camera.PitchDegrees, _player.OnGround));
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
            Biome: OverHorizon.Core.Biomes.BuiltInBiomes.Plains,
            VisibleSections: metrics.VisibleSections,
            TotalMeshedSections: _renderer.UploadedSectionCount,
            DrawnVertices: (long)metrics.TrianglesSubmitted * 3L,
            ManagedMemoryBytes: GC.GetTotalMemory(forceFullCollection: false));

        _latestDebugText = F3OverlayFormatter.Format(in snapshot);
    }
}
