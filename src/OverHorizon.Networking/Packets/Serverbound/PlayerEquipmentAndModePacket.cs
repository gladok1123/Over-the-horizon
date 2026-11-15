namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Synchronizes a player's active <c>GameMode</c>, equipped armor tiers, held main/offhand items,
/// Shield block / Bow draw state, and hand-swing counter to the server and peers.
/// </summary>
public sealed record PlayerEquipmentAndModePacket(
    byte GameModeByte,
    ushort MainHandItemId,
    ushort OffhandItemId,
    byte PackedArmorLower,
    byte PackedArmorUpper,
    byte ActionFlags,
    byte BowChargeByte,
    byte SwingCounter) : IServerboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
