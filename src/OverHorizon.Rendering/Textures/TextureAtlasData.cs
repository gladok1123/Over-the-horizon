using System.Collections.Frozen;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Immutable RGBA8 block &amp; item texture atlas (<c>256x256</c>, <c>256</c> tiles of <c>16x16</c>)
/// and lookup tables for cube faces and inventory/hotbar item sprites.
/// </summary>
public sealed class TextureAtlasData
{
    private readonly FrozenDictionary<(ResourceKey BlockKey, Direction Face), ushort> _faceTileLookup;
    private readonly FrozenDictionary<ResourceKey, ushort> _itemTileLookup;
    private readonly ushort _fallbackCustomBaseTile;

    internal TextureAtlasData(
        int width,
        int height,
        int tileSize,
        ReadOnlyMemory<byte> rgbaPixels,
        FrozenDictionary<(ResourceKey BlockKey, Direction Face), ushort> faceTileLookup,
        FrozenDictionary<ResourceKey, ushort> itemTileLookup,
        ushort fallbackCustomBaseTile)
    {
        Width = width;
        Height = height;
        TileSize = tileSize;
        TilesPerRow = width / tileSize;
        RgbaPixels = rgbaPixels;
        _faceTileLookup = faceTileLookup;
        _itemTileLookup = itemTileLookup;
        _fallbackCustomBaseTile = fallbackCustomBaseTile;
    }

    /// <summary>Gets the atlas texture width in pixels (256).</summary>
    public int Width { get; }

    /// <summary>Gets the atlas texture height in pixels (256).</summary>
    public int Height { get; }

    /// <summary>Gets the edge length of one square block/item tile in pixels (16).</summary>
    public int TileSize { get; }

    /// <summary>Gets the number of tiles per row in the atlas grid (16).</summary>
    public int TilesPerRow { get; }

    /// <summary>Gets the raw row-major RGBA8 pixel buffer (<c>Width * Height * 4</c> bytes).</summary>
    public ReadOnlyMemory<byte> RgbaPixels { get; }

    /// <summary>
    /// Resolves the atlas tile index (<c>0..255</c>) for <paramref name="blockKey"/> on <paramref name="face"/>.
    /// </summary>
    public ushort GetTileIndex(ResourceKey blockKey, Direction face)
    {
        if (_faceTileLookup.TryGetValue((blockKey, face), out var tile))
        {
            return tile;
        }

        var spanRange = Math.Max(1, 128 - _fallbackCustomBaseTile);
        var hash = (uint)blockKey.GetHashCode();
        return (ushort)(_fallbackCustomBaseTile + (hash % (uint)spanRange));
    }

    /// <summary>
    /// Resolves the <c>16x16</c> atlas sprite tile index (<c>0..255</c>) for an inventory/hotbar item or block-item.
    /// </summary>
    public ushort GetItemTileIndex(ResourceKey itemKey)
    {
        if (_itemTileLookup.TryGetValue(itemKey, out var itemTile))
        {
            return itemTile;
        }

        if (_faceTileLookup.TryGetValue((itemKey, Direction.North), out var northTile))
        {
            return northTile;
        }

        return GetTileIndex(itemKey, Direction.Up);
    }
}
