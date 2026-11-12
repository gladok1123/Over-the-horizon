namespace OverHorizon.Client;

/// <summary>
/// Immutable startup and runtime configuration options for the voxel client host.
/// </summary>
/// <param name="HeadlessCheckOnly">When <see langword="true"/>, initializes subsystems and exits immediately.</param>
/// <param name="Headless">When <see langword="true"/>, uses the zero-GPU <c>HeadlessRenderSubsystem</c>.</param>
/// <param name="MaxFrames">Optional maximum number of simulation/render frames to execute before exiting.</param>
/// <param name="ServerHost">Optional remote multiplayer server hostname or IP (<see langword="null"/> runs local singleplayer/menu).</param>
/// <param name="ServerPort">Remote multiplayer TCP port.</param>
/// <param name="Username">Player username (<c>1..16</c> chars) used during multiplayer login.</param>
/// <param name="ViewDistanceChunks">Client chunk streaming and meshing radius (<c>1..16</c>).</param>
/// <param name="StartInWorld">When <see langword="true"/>, skips the Title Screen and spawns directly into Singleplayer.</param>
public sealed record ClientOptions(
    bool HeadlessCheckOnly = false,
    bool Headless = false,
    int? MaxFrames = null,
    string? ServerHost = null,
    ushort ServerPort = 25565,
    string Username = "Explorer",
    int ViewDistanceChunks = 4,
    bool StartInWorld = false);
