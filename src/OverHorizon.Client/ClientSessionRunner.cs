using System.Collections.Concurrent;
using System.Numerics;
using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using OverHorizon.Core.Biomes;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Mobs;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.World;
using OverHorizon.World.Generation;
using OverHorizon.World.Storage;

namespace OverHorizon.Client;

/// <summary>
/// Drives the voxel client runtime across Title Screen ("C# EDITION"), World Selection &amp; Creation (<c>./saves/</c>),
/// persistent <c>options.json</c>, Redstone &amp; Comparators &amp; Pistons, 3D Dropped Items, Shield Blocking,
/// Spectator &amp; X-Ray modes, "Open to LAN", and Multiplayer sessions.
/// </summary>
public sealed partial class ClientSessionRunner : IAsyncDisposable
{
    private const long LocalTicketOwnerId = 1L;
    private const long DefaultPanoramaSeed = 0x5F3759DF42424242L;

    private readonly ClientOptions _options;
    private readonly CoreRegistries _registries;
    private readonly IBlockRegistry _blocks;
    private readonly IWorldSubsystem _panoramaWorld;
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
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new();

    private WorldSubsystem? _ownedSavedWorld;
    private SavedWorldMetadata? _activeWorldMeta;
    private ClientMultiplayerTransport _transport = new();
    private CancellationTokenSource? _sessionCts;
    private Task? _backgroundWorldStreamTask;
    private long _currentSeed = DefaultPanoramaSeed;
    private BiomeSampler _biomeSampler;
    private double _lastFps = 60.0;
    private double _localSpawnY = 76.0;
    private double _moveSendTimer;
    private double _equipSendTimer;
    private bool _receivedInitialSpawnSync;
    private Vec3d _lastSentFeetPosition;
    private float _lastSentYaw;
    private float _lastSentPitch;
    private byte _lastSentSwingCounter;
    private int _lastSyncedRidingCartId = -1;
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
        _registries = registries;
        _blocks = blocks;
        _panoramaWorld = localWorld;
        _renderer = renderer;
        _audio = audio;
        _logger = logger;

        var startDirectlyInGame = options.Headless || options.StartInWorld || !string.IsNullOrWhiteSpace(options.ServerHost);
        var initialMode = startDirectlyInGame ? UiOverlayMode.InGame : UiOverlayMode.TitleMenu;

        _atlas = ProceduralAtlasGenerator.CreateDefault(blocks, options.ExternalAssetsPath);
        _renderer.UploadAtlas(_atlas);
        if (_renderer is SilkOpenGlRenderSubsystem silk)
        {
            silk.UploadEntityAtlas(EntityTextureAtlasGenerator.CreateDefault(options.ExternalAssetsPath));
        }

        _worldCache = new ClientWorldCache(blocks, _atlas);
        _player = new ClientPlayerController(new Vec3d(0.5, 80.0, 0.5), blocks, registries.Items);
        _player.Hotbar.BindAtlas(_atlas);
        _player.Camera.SetAspectRatio(1280, 720);

        _menu = new ClientMenuController(options, initialMode, registries.Items, BuiltInRecipes.CreateFrozen(registries.Items), _player);
        _menuActions = new ClientMenuSessionActions(_menu, _chatHistory, _logger);
        _inputBinder = new ClientInputBinder(
            _player, _menu, _audio, _logger, () => _latestDebugText, () => _renderer is SilkOpenGlRenderSubsystem s ? s.WindowPixelSize : new Vector2(1280, 720));
        _remotePlayers = new ClientRemotePlayerTracker(_logger);
        _worldBridge = new ClientWorldSyncAndMobBridge(_blocks, registries.Items, _panoramaWorld, _worldCache, _renderer, _audio, _mobManager, _atlas);
        _survivalCombat = new ClientSurvivalCombatController(blocks, registries.Items, _audio);
        _meshQueue = new ClientAsyncMeshQueue(blocks, _atlas);
        _biomeSampler = new BiomeSampler(_currentSeed, registries.Biomes);
    }

    /// <summary>Gets the client-side world chunk &amp; light cache.</summary>
    public ClientWorldCache WorldCache => _worldCache;

    /// <summary>Gets the local first-person player controller.</summary>
    public ClientPlayerController Player => _player;

    /// <summary>Gets the client-side world mob manager.</summary>
    public WorldMobManager Mobs => _mobManager;

    /// <summary>Gets the <c>256x256</c> block &amp; item texture atlas.</summary>
    public TextureAtlasData Atlas => _atlas;

    /// <summary>Gets the number of remote multiplayer peers currently tracked.</summary>
    public int RemotePlayerCount => _remotePlayers.Count;

    /// <summary>Gets the most recently formatted <c>F3</c> debug telemetry string.</summary>
    public string LatestDebugOverlayText => _latestDebugText;

    /// <summary>Initializes audio, multiplayer login (if configured), or local spawn chunks.</summary>
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
        if (_worldBridge.ActiveLocalWorld.Chunks.TryGetSnapshot(new ChunkPos(0, 0), out var centerSnap))
        {
            _localSpawnY = centerSnap.GetHeight(HeightmapType.MotionBlocking, 0, 0);
            _player.ApplyAuthoritativePose(new Vec3d(0.5, _localSpawnY, 0.5), 0.0f, 0.0f);
        }

        if (!_options.Headless)
        {
            _worldBridge.SeedInitialStarterMobs();
            if (_menu.Mode == UiOverlayMode.InGame)
            {
                _chatHistory.AddMessage("Welcome to Over-the-Horizon v0.9.0-alpha! Type /help for commands.");
            }
        }
    }

    /// <summary>Instantly breaks the currently targeted block in Creative/headless testing (including Bedrock in Creative).</summary>
    public bool TryBreakTargetedBlock()
    {
        if (_player.CurrentTarget is not { } target) return false;
        var existing = _worldCache.GetBlockState(target.BlockPosition);
        if (existing.IsAir) return false;
        if (_player.Mode == GameMode.Survival && existing == _blocks.GetDefaultState(BuiltInBlocks.Bedrock)) return false;

        _player.TriggerHandSwing();
        return ApplyWorldBlockChange(target.BlockPosition, target.HitFace, BlockState.Air);
    }

    /// <summary>Middle-Click Pick Block: selects or spawns the block targeted by the crosshair.</summary>
    public bool TryPickTargetedBlock()
    {
        if (_player.CurrentTarget is not { } target) return false;
        var state = _worldCache.GetBlockState(target.BlockPosition);
        if (state.IsAir) return false;
        var key = _blocks.GetStateMetadata(state).BlockKey;
        if (key == BuiltInBlocks.PistonHead) key = BuiltInBlocks.Piston;
        if (key == BuiltInBlocks.LitFurnace) key = BuiltInBlocks.Furnace;

        var ok = _player.Hotbar.TryPickBlock(key, _player.Mode);
        if (ok) _audio.PlayPositional(SoundCue.ItemPop, _player.Camera.Position, gain: 0.40f * _player.MasterVolume);
        return ok;
    }

    /// <summary>Throws <paramref name="requestedCount"/> items from the active hotbar slot as a 3D <c>DroppedItemEntity</c> (<c>Q</c> / <c>Ctrl+Q</c>).</summary>
    public bool TryDropSelectedItem(int requestedCount = 1)
    {
        if (_player.Mode == GameMode.Spectator || _player.IsDead) return false;
        var slot = _player.Hotbar.SelectedSlot;
        var cur = _player.Hotbar.SurvivalInventory.GetSlot(slot);
        if (cur.IsEmpty) return false;

        var dropAmount = Math.Clamp(requestedCount, 1, cur.Count);
        var droppedStack = new ItemStack(cur.ItemId, dropAmount);
        if (_player.Mode == GameMode.Survival || dropAmount >= cur.Count)
        {
            _player.Hotbar.SurvivalInventory.SetSlot(slot, cur.WithCount(cur.Count - dropAmount));
            _player.Hotbar.RefreshSlotVisuals(_player.Mode);
        }

        var f = _player.Camera.Forward;
        if (_transport.IsConnected)
        {
            _transport.EnqueueOutbound(new PlayerWorldInteractionPacket(
                PlayerWorldInteractionKind.DropItem, _player.Camera.Position, new Vec3d(f.X, f.Y, f.Z), droppedStack.ItemId, droppedStack.Count));
        }
        else
        {
            _ = _worldBridge.DroppedItems.SpawnPlayerThrow(_player.Camera.Position, f, droppedStack);
        }

        _player.TriggerHandSwing();
        _audio.PlayPositional(SoundCue.SwordSwing, _player.Camera.Position, gain: 0.35f * _player.MasterVolume, pitch: 1.15f);
        return true;
    }

    /// <summary>Uses the selected item or places a block against the targeted face.</summary>
    public bool TryPlaceSelectedBlock()
    {
        var used = _survivalCombat.HandleRightClickUseOrPlace(
            _player, _worldCache, _worldBridge, _mobManager, _transport, _chatHistory, ApplyWorldBlockChange, out var openContainer, out var containerPos);

        if (openContainer is { } containerMode)
        {
            _menu.OpenContainerScreen(containerMode, containerPos);

            // Original 23w13a Barrel / Chest lid cues fire only on the closed -> open transition!
            if (_menu.ContainerOpenedAt is { } openedPos && _menu.ContainerToggledOpen)
            {
                var center = new Vec3d(openedPos.X + 0.5, openedPos.Y + 0.5, openedPos.Z + 0.5);
                var cue = containerMode == UiOverlayMode.BarrelScreen ? SoundCue.BarrelOpen : SoundCue.ChestOpen;
                _audio.PlayPositional(cue, center, gain: 0.70f * _player.MasterVolume);
            }
        }

        return used;
    }

    /// <summary>Executes a local slash-command or transmits a chat/command message.</summary>
    public void SendChatOrCommand(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var trimmed = message.Trim();

        if (_transport.IsConnected)
        {
            if (trimmed.StartsWith("/gamemode", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("/gm", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("/effect", StringComparison.OrdinalIgnoreCase))
            {
                _ = ClientSlashCommandExecutor.TryExecuteLocalCommand(trimmed, _player, _worldBridge, _mobManager, _registries, _audio, _chatHistory, _currentSeed);
                return;
            }

            _transport.EnqueueOutbound(new ChatMessagePacket(trimmed));
            return;
        }

        if (ClientSlashCommandExecutor.TryExecuteLocalCommand(trimmed, _player, _worldBridge, _mobManager, _registries, _audio, _chatHistory, _currentSeed))
        {
            return;
        }

        _chatHistory.AddMessage($"<{_menu.UsernameInput}> {trimmed}");
    }

    /// <summary>Steps a deterministic sequence of headless simulation &amp; render frames.</summary>
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
            lastMetrics = _renderer.RenderFrame(_player.Camera, _worldBridge.TimeCycle.SkyBrightness, BuildOverlayState(new Vector2(1280, 720)));
            RefreshDebugTelemetry(lastMetrics, deltaSeconds);
        }

        return lastMetrics;
    }

    /// <summary>Runs the interactive Silk.NET OpenGL window loop or headless fallback.</summary>
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
                var renderCam = _menu.Mode is UiOverlayMode.TitleMenu or UiOverlayMode.WorldSelectMenu or UiOverlayMode.CreateWorldMenu
                    ? _player.Camera
                    : _player.BuildRenderCamera(_worldCache);
                var metrics = silkRenderer.RenderFrame(renderCam, _worldBridge.TimeCycle.SkyBrightness, BuildOverlayState(silkRenderer.WindowPixelSize));
                RefreshDebugTelemetry(metrics, dt);
                var flyTag = _player.IsFlying ? " [FLY]" : string.Empty;
                var xrayTag = _menu.XRayEnabled ? " [X-RAY]" : string.Empty;
                var lanTag = _menuActions.IsLanOpen ? " [LAN:25565]" : string.Empty;
                silkRenderer.WindowTitle = _menu.Mode == UiOverlayMode.TitleMenu
                    ? "Over-the-Horizon: C# EDITION (v0.9.0-alpha)"
                    : $"Over-the-Horizon v0.9.0 | {_lastFps:F0} FPS | {_player.Mode}{flyTag}{xrayTag}{lanTag} | RD:{_menu.ActiveViewDistance} | [{_player.Hotbar.SelectedSlot + 1}: {_player.Hotbar.SelectedBlockDisplayName}]";
            },
            onResize: (w, h) => _player.Camera.SetAspectRatio(w, h),
            cancellationToken: cancellationToken);

        if (_backgroundWorldStreamTask is not null) await _backgroundWorldStreamTask.ConfigureAwait(false);
        await PersistActiveWorldIfAnyAsync().ConfigureAwait(false);
        await _meshQueue.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GreedySectionMesher.XRayEnabled = false;
        if (_sessionCts is not null) await _sessionCts.CancelAsync().ConfigureAwait(false);
        await _menuActions.AwaitPendingAsync().ConfigureAwait(false);
        await _meshQueue.DisposeAsync().ConfigureAwait(false);
        await _transport.DisposeAsync().ConfigureAwait(false);
        _menuActions.StopHostedServerIfAny();

        if (_ownedSavedWorld is not null)
        {
            await _ownedSavedWorld.DisposeAsync().ConfigureAwait(false);
            _ownedSavedWorld = null;
        }

        _menuActions.Dispose();
        while (_mainThreadQueue.TryDequeue(out var leftover))
        {
            leftover();
        }

        _sessionCts?.Dispose();
        _sessionCts = null;
    }

    private Task EnqueueOnMainThreadAsync(Action work)
    {
        if (_options.Headless || !_renderer.IsContextInitialized)
        {
            work();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _mainThreadQueue.Enqueue(() =>
        {
            try
            {
                work();
                tcs.TrySetResult();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private void RefreshDebugTelemetry(RenderFrameMetrics metrics, double dt)
    {
        if (dt > 1e-6) _lastFps = (_lastFps * 0.85) + ((1.0 / dt) * 0.15);
        var bx = (int)Math.Floor(_player.FeetPosition.X);
        var bz = (int)Math.Floor(_player.FeetPosition.Z);
        var biomeKey = _registries.Biomes.GetById(_biomeSampler.SampleBiomeId(bx, bz)).Key;
        var snapshot = new F3DebugSnapshot(
            _renderer.BackendName, _lastFps, dt * 1000.0, _player.FeetPosition,
            _player.Camera.CardinalFacing, _player.Camera.YawDegrees, _player.Camera.PitchDegrees,
            biomeKey, metrics.VisibleSections, _renderer.UploadedSectionCount,
            metrics.TrianglesSubmitted * 3, GC.GetTotalMemory(forceFullCollection: false));
        _latestDebugText = F3OverlayFormatter.Format(in snapshot);
    }
}
