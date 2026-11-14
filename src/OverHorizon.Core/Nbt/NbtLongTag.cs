namespace OverHorizon.Core.Nbt;

/// <summary>Immutable 64-bit signed integer NBT tag.</summary>
public sealed class NbtLongTag : NbtTag
{
    /// <summary>Initializes a new instance of the <see cref="NbtLongTag"/> class.</summary>
    /// <param name="value">Signed 64-bit value.</param>
    public NbtLongTag(long value) => Value = value;

    /// <summary>Gets the 64-bit signed value.</summary>
    public long Value { get; }

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.LongTag;
}
