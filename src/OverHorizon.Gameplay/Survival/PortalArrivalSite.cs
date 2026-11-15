using OverHorizon.Core;
using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Survival;

/// <summary>
/// Result of <see cref="NetherPortalRules.CreateArrivalSite"/>: everything a travelling session needs to place
/// the player in the opposite dimension.
/// </summary>
/// <param name="Dimension">Dimension the traveller arrives in.</param>
/// <param name="Desired">Scaled destination block coordinate (1 Nether step = 8 Overworld steps).</param>
/// <param name="Landing">Safe feet-level block coordinate the traveller actually lands on.</param>
/// <param name="SpawnPosition">Feet position the traveller is placed at, clear of the new portal frame.</param>
public readonly record struct PortalArrivalSite(
    DimensionId Dimension,
    BlockPos Desired,
    BlockPos Landing,
    Vec3d SpawnPosition);
