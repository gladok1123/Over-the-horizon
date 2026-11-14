using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Ai;

internal readonly record struct PathNodeRecord(
    BlockPos Position,
    int ParentIndex,
    double GScore);
