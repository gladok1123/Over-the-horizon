using OverHorizon.Core.Math;

namespace OverHorizon.World.ChunkManagement;

/// <summary>
/// Immutable chunk retention ticket keeping a square region of radius <see cref="Radius"/> around <see cref="Center"/> loaded.
/// </summary>
/// <param name="OwnerId">Unique ticket owner identifier (e.g. player entity ID or <c>0</c> for spawn).</param>
/// <param name="TicketType">Category of ticket.</param>
/// <param name="Center">Center chunk coordinate.</param>
/// <param name="Radius">Chebyshev chunk radius (<c>&gt;= 0</c>).</param>
public readonly record struct ChunkTicket(
    long OwnerId,
    ChunkTicketType TicketType,
    ChunkPos Center,
    int Radius);
