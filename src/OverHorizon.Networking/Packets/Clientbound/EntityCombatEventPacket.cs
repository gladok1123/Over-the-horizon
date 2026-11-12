using OverHorizon.Core.Math;
using OverHorizon.Networking.Packets;

namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Clientbound <c>Play</c> packet (<c>0x09</c>) notifying sessions of an authoritative PvP/entity melee hit,
/// updated target health, knockback impulse, and death/respawn outcome.
/// </summary>
/// <param name="TargetEntityId">64-bit entity ID of the damaged entity/player.</param>
/// <param name="AttackerEntityId">64-bit entity ID of the attacker.</param>
/// <param name="NewHealth">Updated authoritative health points of the target (<c>0..20</c>).</param>
/// <param name="KnockbackVelocity">World-space knockback velocity impulse applied to the target.</param>
/// <param name="TargetDied">Whether the hit reduced the target's health to zero and triggered respawn.</param>
public sealed record EntityCombatEventPacket(
    long TargetEntityId,
    long AttackerEntityId,
    float NewHealth,
    Vec3d KnockbackVelocity,
    bool TargetDied) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
