namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Broadcasts a remote player's equipped 3D armor, held mainhand/offhand items, Shield/Bow poses,
/// left/right hand swing, <c>GameMode</c>, and mounted Minecart ID to peers.
/// </summary>
public sealed record RemotePlayerEquipmentPacket(
    long EntityId,
    byte GameModeByte,
    ushort MainHandItemId,
    ushort OffhandItemId,
    byte PackedArmorLower,
    byte PackedArmorUpper,
    byte ActionFlags,
    byte BowChargeByte,
    byte SwingCounter,
    int RidingMinecartId) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
