using OverHorizon.Audio;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Rendering;
using OverHorizon.World.ChunkManagement;

namespace OverHorizon.Client;

/// <summary>
/// Nether Portal travel orchestration for <see cref="ClientSessionRunner"/>: streams the destination
/// dimension on a background task, builds a return portal, and hot-swaps the active world view.
/// </summary>
public sealed partial class ClientSessionRunner
{
    private Task<PortalArrival>? _pendingPortalTravel;

    /// <summary>
    /// Advances a pending Nether Portal journey (or starts one) and reports whether the frame is frozen.
    /// </summary>
    /// <param name="renderer">Render subsystem used to drop cached geometry on arrival.</param>
    /// <param name="cancellationToken">Session cancellation token.</param>
    /// <returns><see langword="true"/> while the player is mid-jump between dimensions.</returns>
    private bool StepPortalTravel(IRenderSubsystem renderer, CancellationToken cancellationToken)
    {
        if (_pendingPortalTravel is null)
        {
            if (!_player.TryConsumePortalTravel(out var target, out var departureFeet))
            {
                return false;
            }

            _menu.StatusMessage = "Travelling through the portal...";
            _pendingPortalTravel = Task.Run(
                async () => await PreparePortalArrivalAsync(target, _player.Dimension, departureFeet, cancellationToken).ConfigureAwait(false),
                cancellationToken);
            return true;
        }

        if (!_pendingPortalTravel.IsCompleted)
        {
            return true;
        }

        var task = _pendingPortalTravel;
        _pendingPortalTravel = null;
        try
        {
#pragma warning disable VSTHRD002 // Intentional: the task is already completed, so this read cannot deadlock.
            ApplyPortalArrival(renderer, task.GetAwaiter().GetResult());
#pragma warning restore VSTHRD002
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ClientLog.DebugTelemetrySnapshot(_logger, "Portal travel failed: " + ex.Message);
        }

        _menu.StatusMessage = string.Empty;
        return false;
    }

    private async Task<PortalArrival> PreparePortalArrivalAsync(
        DimensionId target,
        DimensionId from,
        BlockPos departureFeet,
        CancellationToken cancellationToken)
    {
        var world = _worldBridge.ActiveRawWorld;
        var chunks = world.GetChunks(target);
        var scaled = NetherPortalRules.ScaleTravel(departureFeet, from, target);
        var center = new ChunkPos(scaled.X >> 4, scaled.Z >> 4);
        chunks.UpsertTicket(new ChunkTicket(LocalTicketOwnerId, ChunkTicketType.Player, center, 2, target));

        for (var i = 0; i < 8; i++)
        {
            var loaded = await chunks.ProcessTicketsAsync(12, cancellationToken).ConfigureAwait(false);
            if (loaded == 0)
            {
                break;
            }
        }

        return new PortalArrival(target, from, departureFeet);
    }

    private void ApplyPortalArrival(IRenderSubsystem renderer, PortalArrival arrival)
    {
        var world = _worldBridge.ActiveRawWorld;
        var target = arrival.Dimension;
        var previous = _player.Dimension;
        var chunks = world.GetChunks(target);

        // Single-player uses the exact same arrival pipeline as the dedicated server, so 1 Nether step is
        // always 8 Overworld steps in both directions and the return portal is framed identically.
        var site = NetherPortalRules.CreateArrivalSite(
            _blocks,
            pos => world.GetBlockState(pos, target),
            (pos, state) => world.SetBlockState(pos, state, 0, target),
            pos => chunks.TryGetSnapshot(pos, out _),
            arrival.DepartureFeet,
            arrival.From);

        _player.SetDimension(target);
        _worldBridge.SetActiveLocalWorld(world, _worldBridge.TimeCycle.TotalTicks, target);
        _worldCache.ClearAll(renderer);
        _audio.PlayPositional(
            SoundCue.PortalTravel,
            new Vec3d(site.Landing.X + 0.5, site.Landing.Y + 0.5, site.Landing.Z + 0.5),
            gain: 0.85f * _player.MasterVolume);
        _player.ApplyAuthoritativePose(
            site.SpawnPosition,
            _player.Camera.YawDegrees,
            _player.Camera.PitchDegrees,
            updateSpawn: false);
        _player.ApplyPortalCooldown(5.0);

        if (previous != target)
        {
            _ = world.GetChunks(previous).RemoveTicket(LocalTicketOwnerId);
        }
    }

    private readonly record struct PortalArrival(DimensionId Dimension, DimensionId From, BlockPos DepartureFeet);
}
