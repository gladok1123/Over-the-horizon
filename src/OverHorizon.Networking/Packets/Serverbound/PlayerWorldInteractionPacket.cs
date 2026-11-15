using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Compact serverbound multiplayer interaction packet covering Bow arrows, 3D item drops, Minecarts, Spawn Eggs, Redstone, and Mob combat.
/// </summary>
public sealed record PlayerWorldInteractionPacket(
    PlayerWorldInteractionKind Kind,
    Vec3d Origin,
    Vec3d DirectionOrTarget,
    int AuxValue,
    int CountValue) : IServerboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
