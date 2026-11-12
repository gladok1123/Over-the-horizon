using OverHorizon.Core;
using OverHorizon.Modding.Commands;

namespace OverHorizon.Server;

/// <summary>
/// Immutable configuration options for the dedicated server host.
/// </summary>
/// <param name="TicksPerSecond">Target simulation rate (defaults to <see cref="EngineConstants.TicksPerSecond"/>).</param>
/// <param name="MaxTicks">Optional tick limit for headless runs; <see langword="null"/> runs until cancelled.</param>
/// <param name="ListenPort">Optional TCP port to bind on startup (<c>0</c> binds an ephemeral port; <see langword="null"/> runs headless without binding).</param>
/// <param name="PlayerViewDistanceChunks">Chebyshev chunk radius streamed around each logged-in player.</param>
/// <param name="DefaultPlayerPermission">Initial <see cref="PermissionLevel"/> granted to newly connected players.</param>
/// <param name="DataPackDirectory">Optional filesystem directory scanned for <c>*.datapack.json</c> content packs.</param>
public sealed record ServerOptions(
    int TicksPerSecond = EngineConstants.TicksPerSecond,
    long? MaxTicks = null,
    int? ListenPort = null,
    int PlayerViewDistanceChunks = 1,
    PermissionLevel DefaultPlayerPermission = PermissionLevel.Everyone,
    string? DataPackDirectory = null);
