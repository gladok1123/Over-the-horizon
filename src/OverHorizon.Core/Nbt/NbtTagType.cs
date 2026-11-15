namespace OverHorizon.Core.Nbt;

/// <summary>
/// Binary tag type discriminators in the Named Binary Tag (NBT) wire format.
/// </summary>
public enum NbtTagType : byte
{
    /// <summary>Marks the end of an <see cref="NbtCompoundTag"/> payload.</summary>
    End = 0,

    /// <summary>Signed 8-bit integer.</summary>
    ByteTag = 1,

    /// <summary>Signed 16-bit big-endian integer.</summary>
    ShortTag = 2,

    /// <summary>Signed 32-bit big-endian integer.</summary>
    IntTag = 3,

    /// <summary>Signed 64-bit big-endian integer.</summary>
    LongTag = 4,

    /// <summary>32-bit big-endian IEEE-754 float.</summary>
    FloatTag = 5,

    /// <summary>64-bit big-endian IEEE-754 double.</summary>
    DoubleTag = 6,

    /// <summary>Length-prefixed array of signed/unsigned 8-bit bytes.</summary>
    ByteArrayTag = 7,

    /// <summary>Unsigned 16-bit length-prefixed UTF-8 string.</summary>
    StringTag = 8,

    /// <summary>Homogeneous length-prefixed list of unnamed tags.</summary>
    ListTag = 9,

    /// <summary>Map of named tags terminated by <see cref="End"/>.</summary>
    CompoundTag = 10,

    /// <summary>Length-prefixed array of 32-bit big-endian integers.</summary>
    IntArrayTag = 11,

    /// <summary>Length-prefixed array of 64-bit big-endian integers.</summary>
    LongArrayTag = 12,
}
