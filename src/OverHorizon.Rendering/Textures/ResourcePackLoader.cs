using System.IO.Compression;
using System.Text.Json;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Loads external <c>16x16</c> block &amp; item PNG textures, <c>256x256</c> GUI sheets (<c>widgets.png</c>, <c>icons.png</c>,
/// <c>slider.png</c>, <c>container/*.png</c>), and <c>lang/*.json</c> translations from <c>minecraft-assets</c> (<c>23w13a__or__b</c>) or user packs.
/// </summary>
public sealed class ResourcePackLoader : IDisposable
{
    private readonly Dictionary<string, byte[]> _blockTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _itemTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _guiTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _entityTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the number of block PNG textures discovered in external asset sources.</summary>
    public int LoadedBlockTextureCount => _blockTextures.Count;

    /// <summary>Gets the number of item PNG textures discovered in external asset sources.</summary>
    public int LoadedItemTextureCount => _itemTextures.Count;

    /// <summary>Gets the number of GUI PNG textures discovered in external asset sources.</summary>
    public int LoadedGuiTextureCount => _guiTextures.Count;

    /// <summary>Gets the parsed localization entries (<c>block.minecraft.*</c>, <c>item.minecraft.*</c>), if any.</summary>
    public IReadOnlyDictionary<string, string> Translations => _translations;

    /// <summary>
    /// Scans <paramref name="explicitPath"/> plus standard <c>./resourcepacks</c>, <c>./assets</c>,
    /// and <c>./minecraft-assets</c> locations for block/item/GUI PNGs and <c>lang/*.json</c> files.
    /// </summary>
    public static ResourcePackLoader Discover(string? explicitPath = null)
    {
        var loader = new ResourcePackLoader();
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            candidates.Add(explicitPath);
        }

        foreach (var baseDir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            candidates.Add(Path.Combine(baseDir, "resourcepacks"));
            candidates.Add(Path.Combine(baseDir, "resourcepacks", "23w13a__or__b"));
            candidates.Add(Path.Combine(baseDir, "assets"));
            candidates.Add(Path.Combine(baseDir, "minecraft-assets"));
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var full = Path.GetFullPath(candidate);
            if (visited.Add(full))
            {
                loader.LoadFromPath(full);
            }
        }

        return loader;
    }

    /// <summary>Looks up a loaded GUI PNG sheet (e.g. <c>"widgets"</c>, <c>"icons"</c>, <c>"container/inventory"</c>).</summary>
    public bool TryGetGuiPng(string guiKey, out byte[] pngBytes) =>
        _guiTextures.TryGetValue(guiKey, out pngBytes!);

    /// <summary>Looks up a loaded entity/mob/player skin PNG (e.g. <c>"player/wide/steve"</c>, <c>"pig/pig"</c>).</summary>
    public bool TryGetEntityPng(string entityKey, out byte[] pngBytes) =>
        _entityTextures.TryGetValue(entityKey, out pngBytes!);

    /// <summary>Attempts to overwrite a <c>16x16</c> block tile in <paramref name="atlas"/> using an external block PNG texture.</summary>
    public bool TryBlitTile(
        byte[] atlas,
        ushort tileIndex,
        string textureStem,
        byte tintR = 255,
        byte tintG = 255,
        byte tintB = 255,
        byte alphaOverride = 255)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        if (!_blockTextures.TryGetValue(textureStem, out var pngBytes) ||
            !PngImageDecoder.TryDecodeTo16x16Rgba(pngBytes, out var rgba))
        {
            return false;
        }

        if (string.Equals(textureStem, "grass_block_side", StringComparison.OrdinalIgnoreCase) &&
            _blockTextures.TryGetValue("grass_block_side_overlay", out var overlayPng) &&
            PngImageDecoder.TryDecodeTo16x16Rgba(overlayPng, out var overlayRgba))
        {
            CompositeTintedOverlay(rgba, overlayRgba, 118, 190, 92);
        }

        WriteTileToAtlas(atlas, tileIndex, rgba, tintR, tintG, tintB, alphaOverride);
        return true;
    }

    /// <summary>Attempts to overwrite a <c>16x16</c> item sprite tile in <paramref name="atlas"/> using an external item or block PNG texture.</summary>
    public bool TryBlitItemTile(byte[] atlas, ushort tileIndex, string itemStem, byte tintR = 255, byte tintG = 255, byte tintB = 255)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        if ((!_itemTextures.TryGetValue(itemStem, out var pngBytes) &&
             !_blockTextures.TryGetValue(itemStem, out pngBytes)) ||
            !PngImageDecoder.TryDecodeTo16x16Rgba(pngBytes, out var rgba))
        {
            return false;
        }

        WriteTileToAtlas(atlas, tileIndex, rgba, tintR, tintG, tintB, 255);
        return true;
    }

    /// <summary>Extracts <c>16x16</c> bed top &amp; side tiles directly from <c>textures/entity/bed/red.png</c> (<c>64x64</c>) when present.</summary>
    public bool TryExtractRedBedTiles(byte[] atlas, ushort headTopTile, ushort footTopTile, ushort headSideTile, ushort footSideTile)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        if ((!_entityTextures.TryGetValue("bed/red", out var pngBytes) &&
             !_entityTextures.TryGetValue("red", out pngBytes)) ||
            !PngImageDecoder.TryDecodeRgba(pngBytes, out var w, out var h, out var bedRgba) ||
            w < 44 || h < 44)
        {
            return false;
        }

        BlitSubRect16(bedRgba, w, 6, 6, atlas, headTopTile);
        BlitSubRect16(bedRgba, w, 6, 28, atlas, footTopTile);
        BlitBedSideStrip(bedRgba, w, 0, 6, atlas, headSideTile);
        BlitBedSideStrip(bedRgba, w, 0, 28, atlas, footSideTile);
        return true;
    }

    private static void BlitSubRect16(byte[] src, int srcW, int sx, int sy, byte[] atlas, ushort tile)
    {
        var ax0 = (tile % 16) * 16;
        var ay0 = (tile / 16) * 16;
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var si = (((sy + y) * srcW) + sx + x) * 4;
                var di = (((ay0 + y) * 256) + ax0 + x) * 4;
                atlas[di + 0] = src[si + 0];
                atlas[di + 1] = src[si + 1];
                atlas[di + 2] = src[si + 2];
                atlas[di + 3] = 255;
            }
        }
    }

    private static void BlitBedSideStrip(byte[] src, int srcW, int sx, int sy, byte[] atlas, ushort tile)
    {
        var ax0 = (tile % 16) * 16;
        var ay0 = (tile / 16) * 16;
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var di = (((ay0 + y) * 256) + ax0 + x) * 4;
                if (y is >= 7 and <= 12)
                {
                    // Rotate 6x16 vertical side strip from bed/red.png into horizontal 16x6 mattress side!
                    var si = (((sy + x) * srcW) + sx + (12 - y)) * 4;
                    atlas[di + 0] = src[si + 0];
                    atlas[di + 1] = src[si + 1];
                    atlas[di + 2] = src[si + 2];
                    atlas[di + 3] = 255;
                }
                else
                {
                    atlas[di + 0] = 154;
                    atlas[di + 1] = 114;
                    atlas[di + 2] = 68;
                    atlas[di + 3] = 255;
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _blockTextures.Clear();
        _itemTextures.Clear();
        _guiTextures.Clear();
        _entityTextures.Clear();
        _translations.Clear();
    }

    private static void CompositeTintedOverlay(byte[] baseRgba, byte[] overlayRgba, byte tr, byte tg, byte tb)
    {
        for (var i = 0; i < baseRgba.Length; i += 4)
        {
            var oa = overlayRgba[i + 3];
            if (oa < 16)
            {
                continue;
            }

            var r = (overlayRgba[i + 0] * tr) / 255;
            var g = (overlayRgba[i + 1] * tg) / 255;
            var b = (overlayRgba[i + 2] * tb) / 255;
            var invA = 255 - oa;

            baseRgba[i + 0] = (byte)(((r * oa) + (baseRgba[i + 0] * invA)) / 255);
            baseRgba[i + 1] = (byte)(((g * oa) + (baseRgba[i + 1] * invA)) / 255);
            baseRgba[i + 2] = (byte)(((b * oa) + (baseRgba[i + 2] * invA)) / 255);
            baseRgba[i + 3] = 255;
        }
    }

    private static void WriteTileToAtlas(
        byte[] atlas,
        ushort tileIndex,
        byte[] rgba16x16,
        byte tintR,
        byte tintG,
        byte tintB,
        byte alphaOverride)
    {
        const int atlasDim = ProceduralAtlasGenerator.AtlasDimension;
        const int tilesPerRow = ProceduralAtlasGenerator.TilesPerRow;
        var tileX = (tileIndex % tilesPerRow) * 16;
        var tileY = (tileIndex / tilesPerRow) * 16;

        for (var py = 0; py < 16; py++)
        {
            for (var px = 0; px < 16; px++)
            {
                var src = ((py * 16) + px) * 4;
                var dst = (((tileY + py) * atlasDim) + (tileX + px)) * 4;
                atlas[dst + 0] = (byte)((rgba16x16[src + 0] * tintR) / 255);
                atlas[dst + 1] = (byte)((rgba16x16[src + 1] * tintG) / 255);
                atlas[dst + 2] = (byte)((rgba16x16[src + 2] * tintB) / 255);
                atlas[dst + 3] = (byte)((rgba16x16[src + 3] * alphaOverride) / 255);
            }
        }
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        var buffer = new byte[entry.Length];
        using var stream = entry.Open();
        var total = 0;
        while (total < buffer.Length)
        {
            var n = stream.Read(buffer, total, buffer.Length - total);
            if (n <= 0)
            {
                break;
            }

            total += n;
        }

        return buffer;
    }

    private void LoadFromPath(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            var ext = Path.GetExtension(fullPath);
            if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".jar", StringComparison.OrdinalIgnoreCase))
            {
                LoadFromArchive(fullPath);
            }

            return;
        }

        if (!Directory.Exists(fullPath))
        {
            return;
        }

        LoadFromDirectoryTree(fullPath);
        foreach (var archive in Directory.EnumerateFiles(fullPath)
                     .Where(static f =>
                         f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                         f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)))
        {
            LoadFromArchive(archive);
        }
    }

    private void LoadFromDirectoryTree(string rootDir)
    {
        foreach (var ns in new[] { "minecraft", "overhorizon" })
        {
            foreach (var relPrefix in new[] { Path.Combine(rootDir, "assets", ns), Path.Combine(rootDir, ns) })
            {
                LoadPngFolder(Path.Combine(relPrefix, "textures", "block"), _blockTextures);
                LoadPngFolder(Path.Combine(relPrefix, "textures", "blocks"), _blockTextures);
                LoadPngFolder(Path.Combine(relPrefix, "textures", "item"), _itemTextures);
                LoadPngFolder(Path.Combine(relPrefix, "textures", "items"), _itemTextures);
                LoadSubPngTree(Path.Combine(relPrefix, "textures", "gui"), string.Empty, _guiTextures);
                LoadSubPngTree(Path.Combine(relPrefix, "textures", "entity"), string.Empty, _entityTextures);
                LoadSubPngTree(Path.Combine(relPrefix, "textures", "models"), "models", _entityTextures);

                var langDir = Path.Combine(relPrefix, "lang");
                if (Directory.Exists(langDir))
                {
                    foreach (var langName in new[] { "en_us.json", "ru_ru.json" })
                    {
                        var langFile = Path.Combine(langDir, langName);
                        if (File.Exists(langFile))
                        {
                            ParseLangJson(File.ReadAllBytes(langFile));
                        }
                    }
                }
            }
        }

        foreach (var subDir in Directory.EnumerateDirectories(rootDir)
                     .Where(static d => Directory.Exists(Path.Combine(d, "assets"))))
        {
            LoadFromDirectoryTree(subDir);
        }
    }

    private static void LoadSubPngTree(string rootDir, string prefix, Dictionary<string, byte[]> sink)
    {
        if (!Directory.Exists(rootDir))
        {
            return;
        }

        foreach (var pngFile in Directory.EnumerateFiles(rootDir, "*.png"))
        {
            var stem = Path.GetFileNameWithoutExtension(pngFile);
            var key = string.IsNullOrEmpty(prefix) ? stem : $"{prefix}/{stem}";
            sink[key] = File.ReadAllBytes(pngFile);
        }

        foreach (var sub in Directory.EnumerateDirectories(rootDir))
        {
            var subName = Path.GetFileName(sub);
            var nextPrefix = string.IsNullOrEmpty(prefix) ? subName : $"{prefix}/{subName}";
            LoadSubPngTree(sub, nextPrefix, sink);
        }
    }

    private static void LoadPngFolder(string dir, Dictionary<string, byte[]> sink)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (var pngFile in Directory.EnumerateFiles(dir, "*.png"))
        {
            sink[Path.GetFileNameWithoutExtension(pngFile)] = File.ReadAllBytes(pngFile);
        }
    }

    private void LoadFromArchive(string archivePath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            foreach (var entry in zip.Entries)
            {
                if (entry.Length is <= 0 or > 4_194_304)
                {
                    continue;
                }

                var norm = entry.FullName.Replace('\\', '/');
                if (norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    var guiMarker = "/textures/gui/";
                    var entityMarker = "/textures/entity/";
                    var modelsMarker = "/textures/models/";
                    var guiIdx = norm.IndexOf(guiMarker, StringComparison.OrdinalIgnoreCase);
                    var entIdx = norm.IndexOf(entityMarker, StringComparison.OrdinalIgnoreCase);
                    var modIdx = norm.IndexOf(modelsMarker, StringComparison.OrdinalIgnoreCase);
                    if (guiIdx >= 0)
                    {
                        var relGui = norm[(guiIdx + guiMarker.Length)..];
                        _guiTextures[relGui[..^4]] = ReadEntryBytes(entry);
                    }
                    else if (entIdx >= 0)
                    {
                        var relEnt = norm[(entIdx + entityMarker.Length)..];
                        _entityTextures[relEnt[..^4]] = ReadEntryBytes(entry);
                    }
                    else if (modIdx >= 0)
                    {
                        var relMod = norm[(modIdx + modelsMarker.Length)..];
                        _entityTextures[$"models/{relMod[..^4]}"] = ReadEntryBytes(entry);
                    }
                    else if (norm.Contains("/textures/block/", StringComparison.OrdinalIgnoreCase) ||
                             norm.Contains("/textures/blocks/", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("assets/minecraft/textures/block/", StringComparison.OrdinalIgnoreCase))
                    {
                        _blockTextures[Path.GetFileNameWithoutExtension(norm)] = ReadEntryBytes(entry);
                    }
                    else if (norm.Contains("/textures/item/", StringComparison.OrdinalIgnoreCase) ||
                             norm.Contains("/textures/items/", StringComparison.OrdinalIgnoreCase) ||
                             norm.StartsWith("assets/minecraft/textures/item/", StringComparison.OrdinalIgnoreCase))
                    {
                        _itemTextures[Path.GetFileNameWithoutExtension(norm)] = ReadEntryBytes(entry);
                    }
                }
                else if (norm.EndsWith("/lang/en_us.json", StringComparison.OrdinalIgnoreCase) ||
                         norm.EndsWith("/lang/ru_ru.json", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(norm, "assets/minecraft/lang/en_us.json", StringComparison.OrdinalIgnoreCase))
                {
                    ParseLangJson(ReadEntryBytes(entry));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            // Ignore unreadable or corrupted archives gracefully.
        }
    }

    private void ParseLangJson(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using var doc = JsonDocument.Parse(utf8Json.ToArray());
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String && prop.Value.GetString() is { Length: > 0 } val)
                {
                    _translations[prop.Name] = val;
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed external language JSON.
        }
    }
}
