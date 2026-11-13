using System.Collections.Concurrent;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Entities;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Redstone;
using OverHorizon.Modding.Commands;
using OverHorizon.Modding.Events;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Sessions;
using OverHorizon.Physics;
using OverHorizon.World;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Serialization;
using OverHorizon.World.Storage;

namespace OverHorizon.Server;

/// <summary>
/// Authoritative main-thread bridge connecting network sessions (<see cref="IServerPlayHandler"/>)
/// to <see cref="IWorldSubsystem"/>, <see cref="IGameplaySubsystem"/>, <see cref="IEventBus"/>, and <see cref="ICommandDispatcher"/>.
/// </summary>
public sealed class ServerPlaySessionBridge : IServerPlayHandler
{
    private const double PlayerWidth = 0.6;
    private const double PlayerHeight = 1.8;
    private const double PlayerEyeHeight = 1.62;
    private const double MaxMoveDistanceSquared = 100.0; // Max 10 blocks per movement packet
    private const double MaxBlockReachSquared = 42.25; // Max 6.5 blocks from player eyes

    private readonly IWorldSubsystem _world;
    private readonly IGameplaySubsystem _gameplay;
    private readonly IVoxelCollisionReader _collisionReader;
    private readonly CoreRegistries _registries;
    private readonly ServerOptions _options;
    private readonly IEventBus _eventBus;
    private readonly ICommandDispatcher _commands;
    private readonly Func<INetworkBroadcaster>? _getBroadcaster;
    private readonly RedstoneEngine _redstone;
    private readonly ConcurrentDictionary<long, ConnectedPlayerState> _players = new();

    /// <summary>Gets the authoritative server day/night clock.</summary>
    public WorldTimeCycle WorldTime { get; } = new();

    /// <summary>Gets or sets a value indicating whether multiplayer PvP damage is enabled on this server.</summary>
    public bool IsPvpEnabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerPlaySessionBridge"/> class.
    /// </summary>
    /// <param name="world">World and chunk manager subsystem.</param>
    /// <param name="gameplay">ECS gameplay subsystem.</param>
    /// <param name="collisionReader">Read-only voxel collision adapter.</param>
    /// <param name="registries">Frozen domain registries.</param>
    /// <param name="options">Server configuration.</param>
    /// <param name="eventBus">Strongly-typed mod event bus.</param>
    /// <param name="commands">Slash-command dispatcher.</param>
    /// <param name="getBroadcaster">Optional lazy network broadcaster resolver for peer presence sync.</param>
    public ServerPlaySessionBridge(
        IWorldSubsystem world,
        IGameplaySubsystem gameplay,
        IVoxelCollisionReader collisionReader,
        CoreRegistries registries,
        ServerOptions options,
        IEventBus eventBus,
        ICommandDispatcher commands,
        Func<INetworkBroadcaster>? getBroadcaster = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(gameplay);
        ArgumentNullException.ThrowIfNull(collisionReader);
        ArgumentNullException.ThrowIfNull(registries);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(commands);

        _world = world;
        _gameplay = gameplay;
        _collisionReader = collisionReader;
        _registries = registries;
        _options = options;
        _eventBus = eventBus;
        _commands = commands;
        _getBroadcaster = getBroadcaster;
        _redstone = new RedstoneEngine(registries.Blocks);
    }

    /// <summary>
    /// Teleports an active player session to <paramref name="destination"/> and sends a sync packet.
    /// </summary>
    /// <param name="sessionId">Target session ID.</param>
    /// <param name="destination">Destination feet position.</param>
    /// <param name="broadcaster">Network packet sender.</param>
    /// <returns><see langword="true"/> if the player was online and teleported.</returns>
    public bool TryTeleportPlayer(long sessionId, Vec3d destination, INetworkBroadcaster broadcaster)
    {
        ArgumentNullException.ThrowIfNull(broadcaster);

        if (!_players.TryGetValue(sessionId, out var state))
        {
            return false;
        }

        state.LastPosition = destination;
        _ = _gameplay.TrySetEntityKinematics(state.EntityId, destination, Vec3d.Zero, onGround: false);
        _ = broadcaster.SendToSession(
            sessionId,
            new SynchronizePlayerPositionPacket(destination, state.YawDegrees, state.PitchDegrees, state.NextTeleportId++));
        BroadcastToPeers(sessionId, broadcaster, new RemotePlayerMovePacket(state.EntityId, destination, state.YawDegrees, state.PitchDegrees));
        return true;
    }

    /// <summary>
    /// Updates the runtime <see cref="PermissionLevel"/> of an online player matching <paramref name="username"/>.
    /// </summary>
    /// <param name="username">Player username.</param>
    /// <param name="level">New permission level.</param>
    /// <returns><see langword="true"/> if a matching player was found.</returns>
    public bool TrySetPlayerPermission(string username, PermissionLevel level)
    {
        foreach (var (_, state) in _players)
        {
            if (string.Equals(state.Username, username, StringComparison.OrdinalIgnoreCase))
            {
                state.Permission = level;
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public async ValueTask<long> OnPlayerLoginAsync(
        PlayerSessionInfo session,
        long tickNumber,
        IList<IClientboundPacket> initialPlayPackets,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(initialPlayPackets);
        _ = tickNumber;

        var spawnChunk = new ChunkPos(0, 0);
        var radius = Math.Max(0, _options.PlayerViewDistanceChunks);
        var diameter = (radius * 2) + 1;

        _world.Chunks.UpsertTicket(new ChunkTicket(
            session.SessionId,
            ChunkTicketType.Player,
            spawnChunk,
            radius));

        _ = await _world.Chunks.ProcessTicketsAsync(diameter * diameter, cancellationToken).ConfigureAwait(false);

        var spawnY = _world.Chunks.TryGetSnapshot(spawnChunk, out var centerSnap)
            ? centerSnap.GetHeight(HeightmapType.MotionBlocking, 0, 0)
            : 70;

        var spawnPos = new Vec3d(0.5, spawnY, 0.5);
        var entityId = _gameplay.SpawnEntity(
            BuiltInEntities.Player,
            spawnPos,
            Vec3d.Zero,
            maxHealth: 20.0f,
            width: PlayerWidth,
            height: PlayerHeight,
            controllingSessionId: session.SessionId);
        _ = _gameplay.TrySetEntityKinematics(entityId, spawnPos, Vec3d.Zero, onGround: true);

        var state = new ConnectedPlayerState(
            entityId,
            session.Username,
            spawnChunk,
            spawnPos,
            _options.DefaultPlayerPermission);
        _players[session.SessionId] = state;

        initialPlayPackets.Add(new SynchronizePlayerPositionPacket(
            spawnPos,
            YawDegrees: 0.0f,
            PitchDegrees: 0.0f,
            TeleportId: state.NextTeleportId++));

        var broadcaster = _getBroadcaster?.Invoke();
        var joinedAnnouncement = new RemotePlayerSpawnPacket(entityId, session.Username, spawnPos, 0.0f, 0.0f);
        foreach (var (peerSessionId, peer) in _players)
        {
            if (peerSessionId == session.SessionId)
            {
                continue;
            }

            initialPlayPackets.Add(new RemotePlayerSpawnPacket(
                peer.EntityId,
                peer.Username,
                peer.LastPosition,
                peer.YawDegrees,
                peer.PitchDegrees));
            _ = broadcaster?.SendToSession(peerSessionId, joinedAnnouncement);
        }

        CollectUnsentChunksAround(state, spawnChunk, radius, initialPlayPackets);
        return entityId;
    }

    /// <inheritdoc />
    public async ValueTask OnPlayPacketAsync(
        PlayerSessionInfo session,
        IServerboundPacket packet,
        long tickNumber,
        INetworkBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(broadcaster);

        if (!_players.TryGetValue(session.SessionId, out var playerState))
        {
            return;
        }

        switch (packet)
        {
            case MovePlayerPacket move:
                await HandlePlayerMoveAsync(
                    session,
                    playerState,
                    move,
                    broadcaster,
                    cancellationToken).ConfigureAwait(false);
                break;

            case PlayerBlockActionPacket blockAction:
                HandleBlockAction(session, playerState, blockAction, tickNumber, broadcaster);
                break;

            case ChatMessagePacket chat:
                await HandleChatOrCommandAsync(
                    session,
                    playerState,
                    chat,
                    broadcaster,
                    cancellationToken).ConfigureAwait(false);
                break;

            case PlayerAttackEntityPacket attack:
                ServerPvpCombatHandler.HandleAttack(
                    session,
                    playerState,
                    attack,
                    tickNumber,
                    IsPvpEnabled,
                    _players,
                    _registries,
                    _gameplay,
                    broadcaster);
                break;
        }
    }

    /// <inheritdoc />
    public void OnPlayerDisconnected(PlayerSessionInfo session)
    {
        if (!_players.TryRemove(session.SessionId, out var state))
        {
            return;
        }

        try
        {
            _ = _world.Chunks.RemoveTicket(session.SessionId);
            _ = _gameplay.DespawnEntity(state.EntityId);
            if (_getBroadcaster?.Invoke() is { } broadcaster)
            {
                BroadcastToPeers(session.SessionId, broadcaster, new RemotePlayerDespawnPacket(state.EntityId));
            }
        }
        catch (ObjectDisposedException)
        {
            // Host container is tearing down; subsystems are already disposed.
        }
    }

    private async ValueTask HandleChatOrCommandAsync(
        PlayerSessionInfo session,
        ConnectedPlayerState playerState,
        ChatMessagePacket chat,
        INetworkBroadcaster broadcaster,
        CancellationToken ct)
    {
        var trimmed = chat.Message.Trim();
        if (trimmed.StartsWith('/'))
        {
            var pos = _gameplay.TryGetEntityPosition(playerState.EntityId);
            var sender = new CommandSender(
                session.Username,
                session.SessionId,
                playerState.Permission,
                pos,
                replyLine => _ = broadcaster.SendToSession(session.SessionId, new SystemChatPacket(replyLine)));

            _ = await _commands.ExecuteAsync(sender, trimmed, ct).ConfigureAwait(false);
            return;
        }

        var chatEvent = new PlayerChatEvent(session.SessionId, session.Username, trimmed);
        _eventBus.Publish(ref chatEvent);
        if (!chatEvent.IsCancelled && !string.IsNullOrWhiteSpace(chatEvent.Message))
        {
            broadcaster.BroadcastToPlaySessions(new SystemChatPacket($"<{session.Username}> {chatEvent.Message}"));
        }
    }

    private async ValueTask HandlePlayerMoveAsync(
        PlayerSessionInfo session,
        ConnectedPlayerState playerState,
        MovePlayerPacket move,
        INetworkBroadcaster broadcaster,
        CancellationToken ct)
    {
        var currentPos = _gameplay.TryGetEntityPosition(playerState.EntityId);
        if (currentPos is not { } authoritativePos)
        {
            return;
        }

        var target = move.Position;
        var delta = target - authoritativePos;
        var outOfVerticalBounds = target.Y < EngineConstants.WorldMinY - 16.0 ||
            target.Y > EngineConstants.WorldMaxY + 32.0;

        var candidateBox = BoundingBox
            .FromEntityFeet(target, PlayerWidth, PlayerHeight)
            .Inflate(-0.06, -0.02, -0.06);

        if (outOfVerticalBounds ||
            delta.LengthSquared > MaxMoveDistanceSquared ||
            VoxelSweptCollider.IntersectsAnySolid(_collisionReader, candidateBox))
        {
            _ = broadcaster.SendToSession(
                session.SessionId,
                new SynchronizePlayerPositionPacket(
                    authoritativePos,
                    move.YawDegrees,
                    move.PitchDegrees,
                    playerState.NextTeleportId++));
            return;
        }

        _ = _gameplay.TrySetEntityKinematics(playerState.EntityId, target, Vec3d.Zero, move.OnGround);
        var movedSignificantly = (target - playerState.LastPosition).LengthSquared > 0.0004 ||
            MathF.Abs(move.YawDegrees - playerState.YawDegrees) > 0.35f ||
            MathF.Abs(move.PitchDegrees - playerState.PitchDegrees) > 0.35f;

        if (movedSignificantly)
        {
            playerState.LastPosition = target;
            playerState.YawDegrees = move.YawDegrees;
            playerState.PitchDegrees = move.PitchDegrees;
            BroadcastToPeers(
                session.SessionId,
                broadcaster,
                new RemotePlayerMovePacket(playerState.EntityId, target, move.YawDegrees, move.PitchDegrees));
        }

        var newChunk = target.ToBlockPos().ToChunkPos();
        if (newChunk != playerState.CenterChunk)
        {
            playerState.CenterChunk = newChunk;
            var radius = Math.Max(0, _options.PlayerViewDistanceChunks);
            _world.Chunks.UpsertTicket(new ChunkTicket(
                session.SessionId,
                ChunkTicketType.Player,
                newChunk,
                radius));

            var diameter = (radius * 2) + 1;
            _ = await _world.Chunks.ProcessTicketsAsync(Math.Min(4, diameter), ct).ConfigureAwait(false);

            var streamed = new List<IClientboundPacket>(diameter);
            CollectUnsentChunksAround(playerState, newChunk, radius, streamed);
            for (var i = 0; i < streamed.Count; i++)
            {
                _ = broadcaster.SendToSession(session.SessionId, streamed[i]);
            }
        }
    }

    private void HandleBlockAction(
        PlayerSessionInfo session,
        ConnectedPlayerState playerState,
        PlayerBlockActionPacket action,
        long tickNumber,
        INetworkBroadcaster broadcaster)
    {
        var currentPos = _gameplay.TryGetEntityPosition(playerState.EntityId);
        if (currentPos is not { } playerFeet)
        {
            return;
        }

        var target = action.TargetPos;
        if (target.Y < EngineConstants.WorldMinY || target.Y >= EngineConstants.WorldMaxY)
        {
            return;
        }

        var eyePos = new Vec3d(playerFeet.X, playerFeet.Y + PlayerEyeHeight, playerFeet.Z);
        var blockCenter = new Vec3d(target.X + 0.5, target.Y + 0.5, target.Z + 0.5);
        if ((blockCenter - eyePos).LengthSquared > MaxBlockReachSquared)
        {
            return;
        }

        var existingState = _world.GetBlockState(target);

        if (action.Action == PlayerBlockActionKind.BreakBlock)
        {
            var breakEvent = new BlockBreakEvent(session.SessionId, session.Username, target, existingState);
            _eventBus.Publish(ref breakEvent);
            if (breakEvent.IsCancelled)
            {
                _ = broadcaster.SendToSession(session.SessionId, new BlockUpdatePacket(target, existingState.Id));
                return;
            }

            ServerWorldTimeAndBlockSync.ApplyMutationAndBroadcastCascade(_world, _registries.Blocks, _redstone, target, BlockState.Air, tickNumber, broadcaster);
            return;
        }

        if (action.BlockStateId == 0 || action.BlockStateId >= _registries.Blocks.StateCount)
        {
            return;
        }

        var candidateState = new BlockState(action.BlockStateId);
        if (_registries.Blocks.GetStateMetadata(candidateState).IsSolid)
        {
            var playerBox = BoundingBox
                .FromEntityFeet(playerFeet, PlayerWidth, PlayerHeight)
                .Inflate(-1e-4, -1e-4, -1e-4);
            if (playerBox.Intersects(BoundingBox.FromBlock(target)))
            {
                return;
            }
        }

        var placeEvent = new BlockPlaceEvent(session.SessionId, session.Username, target, candidateState);
        _eventBus.Publish(ref placeEvent);
        if (placeEvent.IsCancelled)
        {
            _ = broadcaster.SendToSession(session.SessionId, new BlockUpdatePacket(target, existingState.Id));
            return;
        }

        ServerWorldTimeAndBlockSync.ApplyMutationAndBroadcastCascade(_world, _registries.Blocks, _redstone, target, placeEvent.PlacedState, tickNumber, broadcaster);
    }

    private void CollectUnsentChunksAround(
        ConnectedPlayerState state,
        ChunkPos center,
        int radius,
        IList<IClientboundPacket> sink)
    {
        for (var dz = -radius; dz <= radius; dz++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var pos = new ChunkPos(center.X + dx, center.Z + dz);
                if (!state.SentChunks.Contains(pos.PackInt64()) &&
                    _world.Chunks.TryGetSnapshot(pos, out var snapshot))
                {
                    var bytes = ChunkNetworkSerializer.SerializeSnapshotToArray(snapshot);
                    sink.Add(new ChunkDataPacket(pos, bytes));
                    _ = state.SentChunks.Add(pos.PackInt64());
                }
            }
        }
    }

    private void BroadcastToPeers(long senderSessionId, INetworkBroadcaster broadcaster, IClientboundPacket packet)
    {
        foreach (var (peerSessionId, _) in _players)
        {
            if (peerSessionId != senderSessionId)
            {
                _ = broadcaster.SendToSession(peerSessionId, packet);
            }
        }
    }
}
