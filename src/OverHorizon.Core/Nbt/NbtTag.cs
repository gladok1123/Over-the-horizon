namespace OverHorizon.Core.Nbt;

/// <summary>
/// Abstract base class for all Named Binary Tag (NBT) nodes.
/// </summary>
public abstract class NbtTag
{
    private protected NbtTag()
    {
    }

    /// <summary>
    /// Gets the binary <see cref="NbtTagType"/> discriminator of this tag.
    /// </summary>
    public abstract NbtTagType TagType { get; }
}
