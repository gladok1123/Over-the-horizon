using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Clientbound;

/// <summary>
/// Authoritative 20 Hz server snapshot synchronizing 3D Dropped Items, Ballistic Arrows, Rideable Minecarts,
/// World Mobs, Primed TNT, and Smooth Falling Blocks to multiplayer clients.
/// </summary>
public sealed record WorldEntitiesSnapshotPacket(
    IReadOnlyList<(int Id, ushort ItemId, int Count, Vec3d Position, Vec3d Velocity)> DroppedItems,
    IReadOnlyList<(int Id, Vec3d Pos, float Yaw, float Pitch, float Shake, byte Flags)> Arrows,
    IReadOnlyList<(int Id, Vec3d Pos, float Yaw, float Pitch, float Wobble, long RiderEntityId)> Minecarts,
    IReadOnlyList<(int Id, byte TypeByte, Vec3d FeetPos, float Yaw, float WalkPhase, float HurtFlash, float CreeperFuse, byte Flags)> Mobs,
    IReadOnlyList<(int Id, Vec3d FeetPos, bool FlashWhite)> PrimedTnts,
    IReadOnlyList<(int Id, ushort BlockStateId, int BlockX, double CurrentY, int BlockZ)> FallingBlocks) : IClientboundPacket
{
    /// <inheritdoc />
    public ConnectionState ValidState => ConnectionState.Play;
}
