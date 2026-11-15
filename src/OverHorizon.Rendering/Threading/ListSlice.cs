using System.Collections;

namespace OverHorizon.Rendering.Threading;

/// <summary>
/// Allocation-free read-only window over a contiguous range of an <see cref="IReadOnlyList{T}"/>.
/// It lets the existing mesh builders be handed a per-worker sub-range without changing any of their
/// signatures, which is what keeps the threading work contained to the call sites.
/// </summary>
/// <typeparam name="T">Element type of the underlying list.</typeparam>
internal readonly struct ListSlice<T> : IReadOnlyList<T>
{
    private readonly IReadOnlyList<T> _source;
    private readonly int _start;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListSlice{T}"/> struct.
    /// </summary>
    /// <param name="source">Underlying list to window.</param>
    /// <param name="start">Inclusive start index inside <paramref name="source"/>.</param>
    /// <param name="count">Number of visible elements, clamped to the remaining elements of <paramref name="source"/>.</param>
    public ListSlice(IReadOnlyList<T> source, int start, int count)
    {
        _source = source;
        _start = start;
        Count = Math.Clamp(count, 0, Math.Max(0, source.Count - start));
    }

    /// <inheritdoc />
    public int Count { get; }

    /// <inheritdoc />
    public T this[int index] => _source[_start + index];

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
