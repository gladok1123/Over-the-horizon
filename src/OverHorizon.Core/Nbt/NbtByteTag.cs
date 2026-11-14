namespace OverHorizon.Core.Nbt;

/// <summary>Immutable 8-bit signed integer NBT tag.</summary>
public sealed class NbtByteTag : NbtTag
{
    /// <summary>Initializes a new instance of the <see cref="NbtByteTag"/> class.</summary>
    /// <param name="value">Signed 8-bit value.</param>
    public NbtByteTag(sbyte value) => Value = value;

    /// <summary>Gets the 8-bit signed value.</summary>
    public sbyte Value { get; }

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.ByteTag;
}
