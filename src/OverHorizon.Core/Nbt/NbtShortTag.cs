namespace OverHorizon.Core.Nbt;

/// <summary>Immutable 16-bit signed integer NBT tag.</summary>
public sealed class NbtShortTag : NbtTag
{
    /// <summary>Initializes a new instance of the <see cref="NbtShortTag"/> class.</summary>
    /// <param name="value">Signed 16-bit value.</param>
    public NbtShortTag(short value) => Value = value;

    /// <summary>Gets the 16-bit signed value.</summary>
    public short Value { get; }

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.ShortTag;
}
