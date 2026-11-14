using OverHorizon.Networking.Packets;

namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Serverbound <c>Play</c> packet (<c>0x04</c>) sent when a player swings at another entity/player in PvP combat.
/// </summary>
/// <param name="TargetEntityId">64-bit entity ID of the attacked target.</param>
/// <param name="HeldItemId">Raw item registry ID held in the attacker's main hand.</param>
/// <param name="IsCriticalHit">Whether the attacker struck while falling airborne for a 1.5x critical hit.</param>
public sealed record PlayerAttackEntityPacket(
    long TargetEntityId,
    ushort HeldItemId,
    bool IsCriticalHit) : IServerboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
