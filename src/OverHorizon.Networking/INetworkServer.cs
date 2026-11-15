using System.IO.Pipelines;
using System.Net;
using OverHorizon.Core;
using OverHorizon.Networking.Sessions;

namespace OverHorizon.Networking;

/// <summary>
/// Manages async TCP and in-memory pipeline sessions, protocol framing, and Phase 0 deterministic packet ingestion.
/// </summary>
public interface INetworkServer : ITickSubsystem, INetworkBroadcaster, IAsyncDisposable, IDisposable
{
    /// <summary>Gets the total number of currently open network sessions (including status/login).</summary>
    int ActiveSessionCount { get; }

    /// <summary>Gets the number of authenticated players currently in <c>Play</c> state.</summary>
    int OnlinePlayerCount { get; }

    /// <summary>Gets the bound TCP <see cref="IPEndPoint"/> when listening, or <see langword="null"/> if not bound.</summary>
    IPEndPoint? BoundEndPoint { get; }

    /// <summary>
    /// Binds a non-blocking TCP listener socket on <paramref name="bindEndPoint"/> and begins accepting client connections.
    /// Pass port <c>0</c> to bind an OS-assigned ephemeral port.
    /// </summary>
    /// <param name="bindEndPoint">Local IP address and port.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Actual bound local <see cref="IPEndPoint"/>.</returns>
    ValueTask<IPEndPoint> StartListeningAsync(IPEndPoint bindEndPoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches an in-memory or custom duplex pipeline pair as a client session (used by headless tests and local singleplayer).
    /// </summary>
    /// <param name="input">Client-to-server <see cref="PipeReader"/>.</param>
    /// <param name="output">Server-to-client <see cref="PipeWriter"/>.</param>
    /// <returns>Assigned 64-bit session ID.</returns>
    long AttachPipeSession(PipeReader input, PipeWriter output);
}
