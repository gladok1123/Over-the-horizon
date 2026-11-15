using System.Collections.Immutable;

namespace OverHorizon.Core.Blocks;

/// <summary>
/// Defines a discrete named property on a block (such as <c>axis</c>, <c>snowy</c>, or fluid <c>level</c>).
/// </summary>
public sealed class BlockProperty
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlockProperty"/> class.
    /// </summary>
    /// <param name="name">Lowercase property name.</param>
    /// <param name="allowedValues">Ordered list of distinct allowed string values (first is default).</param>
    public BlockProperty(string name, IEnumerable<string> allowedValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(allowedValues);

        var values = allowedValues.ToImmutableArray();
        if (values.IsEmpty)
        {
            throw new ArgumentException($"Block property '{name}' must define at least one allowed value.", nameof(allowedValues));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v) || !seen.Add(v))
            {
                throw new ArgumentException($"Block property '{name}' contains blank or duplicate value '{v}'.", nameof(allowedValues));
            }
        }

        Name = name;
        AllowedValues = values;
    }

    /// <summary>
    /// Gets the property name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the ordered list of allowed property values (index 0 is the default value).
    /// </summary>
    public ImmutableArray<string> AllowedValues { get; }

    /// <summary>
    /// Creates a boolean block property (<c>"false"</c>, <c>"true"</c>).
    /// </summary>
    /// <param name="name">Property name.</param>
    /// <param name="defaultTrue">When <see langword="true"/>, orders <c>"true"</c> first as the default.</param>
    /// <returns>Configured boolean <see cref="BlockProperty"/>.</returns>
    public static BlockProperty Boolean(string name, bool defaultTrue = false) =>
        defaultTrue
            ? new BlockProperty(name, ["true", "false"])
            : new BlockProperty(name, ["false", "true"]);

    /// <summary>
    /// Creates an integer range block property <c>[minInclusive..maxInclusive]</c>.
    /// </summary>
    /// <param name="name">Property name.</param>
    /// <param name="minInclusive">Minimum integer value.</param>
    /// <param name="maxInclusive">Maximum integer value.</param>
    /// <returns>Configured integer <see cref="BlockProperty"/>.</returns>
    public static BlockProperty IntegerRange(string name, int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxInclusive), "Maximum must be >= minimum.");
        }

        var count = (maxInclusive - minInclusive) + 1;
        var list = new string[count];
        for (var i = 0; i < count; i++)
        {
            list[i] = (minInclusive + i).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return new BlockProperty(name, list);
    }
}
