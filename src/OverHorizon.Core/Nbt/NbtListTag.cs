using System.Collections.Immutable;

namespace OverHorizon.Core.Nbt;

/// <summary>
/// Homogeneous NBT list containing zero or more unnamed <see cref="NbtTag"/> elements of a single type.
/// </summary>
public sealed class NbtListTag : NbtTag
{
    /// <summary>
    /// Initializes a new empty instance of the <see cref="NbtListTag"/> class.
    /// </summary>
    /// <param name="elementType">Element type discriminator (<see cref="NbtTagType.End"/> for untyped empty lists).</param>
    public NbtListTag(NbtTagType elementType = NbtTagType.End)
    {
        ElementType = elementType;
        Items = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NbtListTag"/> class with elements.
    /// </summary>
    /// <param name="elementType">Element type discriminator.</param>
    /// <param name="items">Elements matching <paramref name="elementType"/>.</param>
    public NbtListTag(NbtTagType elementType, IEnumerable<NbtTag> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var array = items.ToImmutableArray();
        if (!array.IsEmpty && elementType == NbtTagType.End)
        {
            throw new ArgumentException("Non-empty NbtListTag cannot have ElementType == End.", nameof(elementType));
        }

        foreach (var item in array)
        {
            if (item is null)
            {
                throw new ArgumentException("NbtListTag cannot contain null elements.", nameof(items));
            }

            if (item.TagType != elementType)
            {
                throw new ArgumentException(
                    $"Element type mismatch: expected {elementType}, found {item.TagType}.",
                    nameof(items));
            }
        }

        ElementType = array.IsEmpty ? NbtTagType.End : elementType;
        Items = array;
    }

    /// <summary>Gets the element tag type (<see cref="NbtTagType.End"/> when empty).</summary>
    public NbtTagType ElementType { get; }

    /// <summary>Gets the immutable sequence of child tags.</summary>
    public ImmutableArray<NbtTag> Items { get; }

    /// <inheritdoc />
    public override NbtTagType TagType => NbtTagType.ListTag;
}
