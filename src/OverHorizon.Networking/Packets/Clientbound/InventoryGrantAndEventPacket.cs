using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Delivers server-collected item grants (<c>DroppedItem</c> pickup, <c>BoneMeal</c> harvest, broken <c>Minecart</c>)
/// and world audio/particle events (<c>Explosion=1, ShieldBlock=2, FuseHiss=3, ArrowHit=4, RidingCartSync=5</c>) to clients.
/// </summary>
public sealed record InventoryGrantAndEventPacket(
    ushort GrantedItemId,
    int GrantedCount,
    byte EventKind,
    Vec3d EventPosition) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
