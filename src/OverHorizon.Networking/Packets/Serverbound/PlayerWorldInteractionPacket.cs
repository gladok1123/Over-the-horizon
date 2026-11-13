using OverHorizon.Core.Math;

namespace OverHorizon.Networking.Packets.Serverbound;

/// <summary>
/// Kinds of multiplayer world interactions sent from client to server via <see cref="PlayerWorldInteractionPacket"/>.
/// </summary>
public enum PlayerWorldInteractionKind : byte
{
    /// <summary>Releases a charged 3D ballistic arrow from a Bow.</summary>
    ShootArrow = 0,

    /// <summary>Throws a 3D dropped item stack into the world (<c>Q</c> or death drop).</summary>
    DropItem = 1,

    /// <summary>Places a rideable Minecart onto a targeted rail block.</summary>
    PlaceMinecart = 2,

    /// <summary>Mounts or dismounts a Minecart.</summary>
    MountOrDismountMinecart = 3,

    /// <summary>Updates rider forward steering input while riding a Minecart.</summary>
    SteerMinecart = 4,

    /// <summary>Uses a Spawn Egg to spawn a 3D mob on a block face.</summary>
    SpawnMobEgg = 5,

    /// <summary>Interacts with a Redstone component, Composter, Trapdoor, or Flint &amp; Steel.</summary>
    InteractWorldComponent = 6,

    /// <summary>Attacks a world Mob or breaks a Minecart along the player's crosshair ray.</summary>
    AttackMobOrVehicle = 7,
}

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
