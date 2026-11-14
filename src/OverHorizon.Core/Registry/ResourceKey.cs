namespace OverHorizon.Core.Registry;

/// <summary>
/// Immutable namespaced resource identifier in <c>namespace:path</c> form (e.g. <c>overhorizon:stone</c>).
/// </summary>
public readonly record struct ResourceKey : IComparable<ResourceKey>
{
    /// <summary>
    /// Default engine namespace (<c>overhorizon</c>).
    /// </summary>
    public const string DefaultNamespace = "overhorizon";

    private readonly string? _namespace;
    private readonly string? _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceKey"/> struct.
    /// </summary>
    /// <param name="namespaceName">Lowercase ASCII namespace (<c>[a-z0-9_.-]+</c>).</param>
    /// <param name="path">Lowercase ASCII path (<c>[a-z0-9_./-]+</c>).</param>
    public ResourceKey(string namespaceName, string path)
    {
        ArgumentNullException.ThrowIfNull(namespaceName);
        ArgumentNullException.ThrowIfNull(path);

        if (!IsValidNamespace(namespaceName))
        {
            throw new ArgumentException($"Invalid resource namespace '{namespaceName}'.", nameof(namespaceName));
        }

        if (!IsValidPath(path))
        {
            throw new ArgumentException($"Invalid resource path '{path}'.", nameof(path));
        }

        _namespace = namespaceName;
        _path = path;
    }

    /// <summary>
    /// Gets the resource namespace (defaults to <see cref="DefaultNamespace"/> when default-initialized).
    /// </summary>
    public string Namespace => _namespace ?? DefaultNamespace;

    /// <summary>
    /// Gets the resource path (defaults to <c>"air"</c> when default-initialized).
    /// </summary>
    public string Path => _path ?? "air";

    /// <summary>
    /// Creates a <see cref="ResourceKey"/> in the <see cref="DefaultNamespace"/>.
    /// </summary>
    /// <param name="path">Lowercase ASCII path.</param>
    /// <returns>Constructed <see cref="ResourceKey"/>.</returns>
    public static ResourceKey OverHorizon(string path) => new(DefaultNamespace, path);

    /// <summary>
    /// Parses a <c>namespace:path</c> or unqualified <c>path</c> string into a <see cref="ResourceKey"/>.
    /// </summary>
    /// <param name="input">Input characters.</param>
    /// <returns>Parsed <see cref="ResourceKey"/>.</returns>
    public static ResourceKey Parse(ReadOnlySpan<char> input)
    {
        if (!TryParse(input, out var result))
        {
            throw new FormatException($"Invalid resource key '{input}'.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to parse a <c>namespace:path</c> or unqualified <c>path</c> span without throwing.
    /// </summary>
    /// <param name="input">Input characters.</param>
    /// <param name="result">Parsed <see cref="ResourceKey"/> when successful.</param>
    /// <returns><see langword="true"/> if valid; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> input, out ResourceKey result)
    {
        var colonIndex = input.IndexOf(':');
        ReadOnlySpan<char> nsSpan;
        ReadOnlySpan<char> pathSpan;

        if (colonIndex < 0)
        {
            nsSpan = DefaultNamespace.AsSpan();
            pathSpan = input;
        }
        else
        {
            nsSpan = input[..colonIndex];
            pathSpan = input[(colonIndex + 1)..];
        }

        if (!IsValidNamespace(nsSpan) || !IsValidPath(pathSpan))
        {
            result = default;
            return false;
        }

        var ns = colonIndex < 0 ? DefaultNamespace : nsSpan.ToString();
        result = new ResourceKey(ns, pathSpan.ToString());
        return true;
    }

    /// <summary>
    /// Compares two <see cref="ResourceKey"/> values using ordinal comparison.
    /// </summary>
    /// <param name="left">Left key.</param>
    /// <param name="right">Right key.</param>
    /// <returns>Signed comparison result.</returns>
    public static int Compare(ResourceKey left, ResourceKey right) => left.CompareTo(right);

    /// <summary>Determines whether <paramref name="left"/> precedes <paramref name="right"/>.</summary>
    /// <param name="left">Left key.</param>
    /// <param name="right">Right key.</param>
    /// <returns><see langword="true"/> if less than.</returns>
    public static bool operator <(ResourceKey left, ResourceKey right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether <paramref name="left"/> precedes or equals <paramref name="right"/>.</summary>
    /// <param name="left">Left key.</param>
    /// <param name="right">Right key.</param>
    /// <returns><see langword="true"/> if less than or equal to.</returns>
    public static bool operator <=(ResourceKey left, ResourceKey right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether <paramref name="left"/> follows <paramref name="right"/>.</summary>
    /// <param name="left">Left key.</param>
    /// <param name="right">Right key.</param>
    /// <returns><see langword="true"/> if greater than.</returns>
    public static bool operator >(ResourceKey left, ResourceKey right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether <paramref name="left"/> follows or equals <paramref name="right"/>.</summary>
    /// <param name="left">Left key.</param>
    /// <param name="right">Right key.</param>
    /// <returns><see langword="true"/> if greater than or equal to.</returns>
    public static bool operator >=(ResourceKey left, ResourceKey right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public bool Equals(ResourceKey other) =>
        string.Equals(Namespace, other.Namespace, StringComparison.Ordinal) &&
        string.Equals(Path, other.Path, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(Namespace),
            StringComparer.Ordinal.GetHashCode(Path));

    /// <inheritdoc />
    public int CompareTo(ResourceKey other)
    {
        var cmpNs = string.CompareOrdinal(Namespace, other.Namespace);
        return cmpNs != 0 ? cmpNs : string.CompareOrdinal(Path, other.Path);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Namespace}:{Path}";

    private static bool IsValidNamespace(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return false;
        }

        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            var ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c is '_' or '-' or '.';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidPath(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return false;
        }

        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            var ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c is '_' or '-' or '.' or '/';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}
