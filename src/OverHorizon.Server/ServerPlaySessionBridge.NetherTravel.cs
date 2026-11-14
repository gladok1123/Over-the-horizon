using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Sessions;
using OverHorizon.World.Serialization;
using OverHorizon.World.ChunkManagement;

namespace OverHorizon.Server;

/// <summary>
/// Multiplayer Nether Portal travel and dimension-aware chunk streaming for <see cref="ServerPlaySessionBridge"/>,
/// giving dedicated-server sessions the same portal behaviour as local single-player worlds.
/// </summary>
public sealed partial class ServerPlaySessionBridge
{
    private const double PortalDwellRequiredSeconds = 4.0;
    private const double PortalReentryCooldownSeconds = 5.0;
    private const double PortalStepSeconds = 0.05;
    private const int MaxChunksStreamedPerMove = 12;

    /// <summary>
    /// Accumulates Nether Portal dwell for <paramref name="state"/> and, once satisfied, moves the player into
    /// the opposite dimension, streams the arrival region, and notifies the client to swap its chunk cache.
    /// </summary>
    /// <param name="state">Connected player state.</param>
    /// <param name="broadcaster">Network broadcaster used to notify the client.</param>
    /// <param name="cancellationToken">Cancellation token for chunk streaming.</param>
    /// <returns><see langword="true"/> when the player travelled this step.</returns>
    private async ValueTask<bool> TryStepPortalTravelAsync(
        ConnectedPlayerState state,
        INetworkBroadcaster broadcaster,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(broadcaster);

        if (state.PortalCooldownSeconds > 0.0)
        {
            state.PortalCooldownSeconds = Math.Max(0.0, state.PortalCooldownSeconds - PortalStepSeconds);
            state.PortalDwellSeconds = 0.0;
            return false;
        }

        var dimension = state.Dimension;
        var feetBlock = state.LastPosition.ToBlockPos();
        var inPortal = NetherPortalRules.IsPortal(_registries.Blocks, _world.GetBlockState(feetBlock, dimension)) ||
                       NetherPortalRules.IsPortal(_registries.Blocks, _world.GetBlockState(feetBlock.Offset(Direction.Up), dimension));

        if (!inPortal)
        {
            state.PortalDwellSeconds = 0.0;
            return false;
        }

        state.PortalDwellSeconds += PortalStepSeconds;
        if (state.PortalDwellSeconds < PortalDwellRequiredSeconds)
        {
            return false;
        }

        state.PortalDwellSeconds = 0.0;
        state.PortalCooldownSeconds = PortalReentryCooldownSeconds;

        var target = DimensionRules.Opposite(dimension);
        var desired = NetherPortalRules.ScaleTravel(feetBlock, dimension, target);
        var targetChunks = _world.GetChunks(target);

        var arrivalChunk = desired.ToChunkPos();
        targetChunks.UpsertTicket(new ChunkTicket(state.EntityId, ChunkTicketType.Player, arrivalChunk, 2, target));
        for (var attempt = 0; attempt < 4; attempt++)
        {
            _ = await targetChunks.ProcessTicketsAsync(8, cancellationToken).ConfigureAwait(false);
        }

        var landing = NetherPortalRules.ResolveArrivalSite(
            _registries.Blocks,
            p => _world.GetBlockState(p, target),
            (p, s) => _world.SetBlockState(p, s, WorldTime.TotalTicks, target),
            p => targetChunks.TryGetSnapshot(p, out _),
            desired,
            target);

        NetherPortalRules.BuildFramedPortal(
            _registries.Blocks,
            (p, s) => _world.SetBlockState(p, s, WorldTime.TotalTicks, target),
            landing,
            "z");

        var arrival = new Vec3d(landing.X + 0.5, landing.Y, landing.Z + 2.5);
        _world.GetChunks(dimension).RemoveTicket(state.EntityId);
        state.Dimension = target;
        state.LastPosition = arrival;
        state.SpawnPosition = arrival;
        state.CenterChunk = arrivalChunk;
        state.SentChunks.Clear();
        state.PendingChunkQueue.Clear();

        _ = _gameplay.TrySetEntityKinematics(state.EntityId, arrival, Vec3d.Zero, onGround: true);
        _ = broadcaster.SendToSession(state.SessionId, new PlayerDimensionPacket(target, arrival, state.YawDegrees, state.PitchDegrees));

        StreamChunksAround(state, arrivalChunk, broadcaster);
        return true;
    }

    /// <summary>
    /// Streams unsent chunks around <paramref name="center"/> for <paramref name="state"/>, queueing any surplus
    /// beyond <see cref="MaxChunksStreamedPerMove"/> so a dimension switch or fast crossing cannot flood the socket.
    /// </summary>
    /// <param name="state">Connected player state.</param>
    /// <param name="center">Center chunk coordinate.</param>
    /// <param name="broadcaster">Network broadcaster used to deliver chunk packets.</param>
    private void StreamChunksAround(ConnectedPlayerState state, ChunkPos center, INetworkBroadcaster broadcaster)
    {
        var radius = Math.Max(0, _options.PlayerViewDistanceChunks);
        for (var dz = -radius; dz <= radius; dz++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var pos = new ChunkPos(center.X + dx, center.Z + dz);
                if (!state.SentChunks.Contains(pos.PackInt64()))
                {
                    state.PendingChunkQueue.Enqueue(pos);
                }
            }
        }

        DrainPendingChunks(state, broadcaster, MaxChunksStreamedPerMove);
    }

    /// <summary>
    /// Sends up to <paramref name="budget"/> queued chunk packets whose snapshots are already resident.
    /// </summary>
    /// <param name="state">Connected player state.</param>
    /// <param name="broadcaster">Network broadcaster used to deliver chunk packets.</param>
    /// <param name="budget">Maximum number of chunk packets to emit.</param>
    private void DrainPendingChunks(ConnectedPlayerState state, INetworkBroadcaster broadcaster, int budget)
    {
        var chunks = _world.GetChunks(state.Dimension);
        while (state.PendingChunkQueue.Count > 0 && budget > 0)
        {
            var pos = state.PendingChunkQueue.Dequeue();
            if (state.SentChunks.Contains(pos.PackInt64()))
            {
                continue;
            }

            if (!chunks.TryGetSnapshot(pos, out var snapshot))
            {
                state.PendingChunkQueue.Enqueue(pos);
                break;
            }

            _ = broadcaster.SendToSession(state.SessionId, new ChunkDataPacket(pos, ChunkNetworkSerializer.SerializeSnapshotToArray(snapshot)));
            _ = state.SentChunks.Add(pos.PackInt64());
            budget--;
        }
    }
}
