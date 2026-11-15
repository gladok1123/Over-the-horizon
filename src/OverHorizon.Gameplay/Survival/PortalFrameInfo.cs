using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Survival;

/// <summary>
/// Result of successfully igniting a Nether Portal frame.
/// </summary>
/// <param name="MinInterior">Lowest interior block coordinate of the portal opening.</param>
/// <param name="MaxInterior">Highest interior block coordinate of the portal opening.</param>
/// <param name="Axis">Portal plane orientation: <c>"x"</c> (plane spans Z) or <c>"z"</c> (plane spans X).</param>
public readonly record struct PortalFrameInfo(BlockPos MinInterior, BlockPos MaxInterior, string Axis);
