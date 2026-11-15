using OverHorizon.Core.Items;
using OverHorizon.Core.Math;

namespace OverHorizon.Gameplay.Items;

/// <summary>
/// Live 3D world dropped item stack with gravity, voxel collision bounce, bob/spin phase, stack merging, and pickup delay.
/// </summary>
public sealed class DroppedItemEntity
{
    /// <summary>Initializes a new instance of the <see cref="DroppedItemEntity"/> class.</summary>
    /// <param name="id">Unique runtime entity identifier.</param>
    /// <param name="stack">Item stack carried by this drop.</param>
    /// <param name="position">World-space bottom-center position.</param>
    /// <param name="velocity">Initial launch velocity in blocks/second.</param>
    /// <param name="pickupDelaySeconds">Delay in seconds before players can collect this drop.</param>
    public DroppedItemEntity(int id, ItemStack stack, Vec3d position, Vec3d velocity, float pickupDelaySeconds = 0.45f)
    {
        Id = id;
        Stack = stack;
        Position = position;
        Velocity = velocity;
        PickupDelaySeconds = pickupDelaySeconds;
        BobOffsetSeed = (id * 0.83f) % MathF.Tau;
    }

    /// <summary>Gets the unique runtime identifier of this dropped item entity.</summary>
    public int Id { get; }

    /// <summary>Gets or sets the item stack represented by this entity.</summary>
    public ItemStack Stack { get; set; }

    /// <summary>Gets or sets world-space bottom-center position.</summary>
    public Vec3d Position { get; set; }

    /// <summary>Gets or sets linear velocity in blocks/second.</summary>
    public Vec3d Velocity { get; set; }

    /// <summary>Gets or sets whether this item is resting on a solid surface.</summary>
    public bool OnGround { get; set; }

    /// <summary>Gets or sets remaining seconds before magnetic pickup activates.</summary>
    public float PickupDelaySeconds { get; set; }

    /// <summary>Gets or sets total lifetime in seconds (despawns at 300s).</summary>
    public float AgeSeconds { get; set; }

    /// <summary>Gets per-entity phase offset so nearby drops bob out of unison.</summary>
    public float BobOffsetSeed { get; }
}
