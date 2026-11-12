using System.Collections.Frozen;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Immutable RGBA8 procedural block texture atlas and face-to-tile index lookup table.
/// </summary>
public sealed class TextureAtlasData
{
    private readonly FrozenDictionary<(ResourceKey BlockKey, Direction Face), ushort> _faceTileLookup;
    private readonly ushort _fallbackCustomBaseTile;

    internal TextureAtlasData(
        int width,
        int height,
        int tileSize,
        ReadOnlyMemory<byte> rgbaPixels,
        FrozenDictionary<(ResourceKey BlockKey, Direction Face), ushort> faceTileLookup,
        ushort fallbackCustomBaseTile)
    {
        Width = width;
        Height = height;
        TileSize = tileSize;
        TilesPerRow = width / tileSize;
        RgbaPixels = rgbaPixels;
        _faceTileLookup = faceTileLookup;
        _fallbackCustomBaseTile = fallbackCustomBaseTile;
    }

    /// <summary>Gets the atlas texture width in pixels (256).</summary>
    public int Width { get; }

    /// <summary>Gets the atlas texture height in pixels (256).</summary>
    public int Height { get; }

    /// <summary>Gets the edge length of one square block tile in pixels (16).</summary>
    public int TileSize { get; }

    /// <summary>Gets the number of tiles per row in the atlas grid (16).</summary>
    public int TilesPerRow { get; }

    /// <summary>Gets the raw row-major RGBA8 pixel buffer (<c>Width * Height * 4</c> bytes).</summary>
    public ReadOnlyMemory<byte> RgbaPixels { get; }

    /// <summary>
    /// Resolves the atlas tile index (<c>0..255</c>) for <paramref name="blockKey"/> on <paramref name="face"/>.
    /// </summary>
    /// <param name="blockKey">Block resource key.</param>
    /// <param name="face">Cube face direction.</param>
    /// <returns>Atlas tile index.</returns>
    public ushort GetTileIndex(ResourceKey blockKey, Direction face)
    {
        if (_faceTileLookup.TryGetValue((blockKey, face), out var tile))
        {
            return tile;
        }

        // Deterministic fallback slot in [fallbackBase..255] for dynamic or unmapped keys.
        var spanRange = Math.Max(1, 256 - _fallbackCustomBaseTile);
        var hash = (uint)blockKey.GetHashCode();
        return (ushort)(_fallbackCustomBaseTile + (hash % (uint)spanRange));
    }
}
