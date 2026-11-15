namespace OverHorizon.Core.Nbt;

/// <summary>Immutable 64-bit IEEE-754 floating-point NBT tag.</summary>
public sealed class NbtDoubleTag : NbtTag
{
    /// <summary>Initializes a new instance of the <see cref="NbtDoubleTag"/> class.</summary>
    /// <param name="value">64-bit double value.</param>
    public NbtDoubleTag(double value) => Value = value;

    /// <summary>Gets the 64-bit double value.</summary>
    public double Value { get; }

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.DoubleTag;
}
