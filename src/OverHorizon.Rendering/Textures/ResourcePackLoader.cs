using System.IO.Compression;
using System.Text.Json;

namespace OverHorizon.Rendering.Textures;

/// <summary>
/// Loads external <c>16x16</c> block PNG textures and <c>lang/*.json</c> translations from unpacked directories,
/// <c>.zip</c> resource packs, <c>minecraft-assets</c> GitHub archives, or <c>.jar</c> files.
/// </summary>
public sealed class ResourcePackLoader : IDisposable
{
    private readonly Dictionary<string, byte[]> _blockTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the number of block PNG textures discovered in external asset sources.</summary>
    public int LoadedBlockTextureCount => _blockTextures.Count;

    /// <summary>Gets the parsed localization entries (<c>block.minecraft.*</c>), if any.</summary>
    public IReadOnlyDictionary<string, string> Translations => _translations;

    /// <summary>
    /// Scans an optional explicit path plus standard local <c>./assets</c>, <c>./minecraft-assets</c>,
    /// and <c>./resourcepacks</c> locations for block textures and language JSON files.
    /// </summary>
    /// <param name="explicitPath">Optional user-specified directory, <c>.zip</c>, or <c>.jar</c> path.</param>
    /// <returns>Populated <see cref="ResourcePackLoader"/>.</returns>
    public static ResourcePackLoader Discover(string? explicitPath = null)
    {
        var loader = new ResourcePackLoader();
        var candidates = new List<string>(8);

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            candidates.Add(explicitPath);
        }

        foreach (var baseDir in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            candidates.Add(Path.Combine(baseDir, "assets"));
            candidates.Add(Path.Combine(baseDir, "minecraft-assets"));
            candidates.Add(Path.Combine(baseDir, "resourcepacks"));
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var full = Path.GetFullPath(candidate);
            if (!visited.Add(full))
            {
                continue;
            }

            loader.LoadFromPath(full);
        }

        return loader;
    }

    /// <summary>
    /// Attempts to overwrite a <c>16x16</c> tile in <paramref name="atlas"/> using an external PNG texture.
    /// </summary>
    /// <param name="atlas">Destination <c>256x256</c> RGBA8 atlas buffer.</param>
    /// <param name="tileIndex">Destination tile index (<c>0..255</c>).</param>
    /// <param name="textureStem">Block texture filename stem (e.g. <c>"stone"</c> or <c>"cobblestone"</c>).</param>
    /// <param name="tintR">Multiplicative red tint (<c>255</c> = neutral).</param>
    /// <param name="tintG">Multiplicative green tint (<c>255</c> = neutral).</param>
    /// <param name="tintB">Multiplicative blue tint (<c>255</c> = neutral).</param>
    /// <param name="alphaOverride">Optional alpha multiplier (<c>255</c> = preserve PNG alpha).</param>
    /// <returns><see langword="true"/> if an external PNG texture was decoded and written.</returns>
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

    /// <inheritdoc />
    public void Dispose()
    {
        _blockTextures.Clear();
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
        foreach (var archive in Directory.EnumerateFiles(fullPath))
        {
            var ext = Path.GetExtension(archive);
            if (string.Equals(ext, ".zip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".jar", StringComparison.OrdinalIgnoreCase))
            {
                LoadFromArchive(archive);
            }
        }
    }

    private void LoadFromDirectoryTree(string rootDir)
    {
        foreach (var ns in new[] { "minecraft", "overhorizon" })
        {
            foreach (var relPrefix in new[] { Path.Combine(rootDir, "assets", ns), Path.Combine(rootDir, ns) })
            {
                var blockDir = Path.Combine(relPrefix, "textures", "block");
                if (!Directory.Exists(blockDir))
                {
                    blockDir = Path.Combine(relPrefix, "textures", "blocks");
                }

                if (Directory.Exists(blockDir))
                {
                    foreach (var pngFile in Directory.EnumerateFiles(blockDir, "*.png"))
                    {
                        var stem = Path.GetFileNameWithoutExtension(pngFile);
                        _blockTextures[stem] = File.ReadAllBytes(pngFile);
                    }
                }

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

        // Support nested unwrapped GitHub zip folder (e.g. ./resourcepacks/minecraft-assets-1.21.4/assets/...)
        foreach (var subDir in Directory.EnumerateDirectories(rootDir))
        {
            if (Directory.Exists(Path.Combine(subDir, "assets")))
            {
                LoadFromDirectoryTree(subDir);
            }
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
                if (norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                    (norm.Contains("/textures/block/", StringComparison.OrdinalIgnoreCase) ||
                     norm.Contains("/textures/blocks/", StringComparison.OrdinalIgnoreCase) ||
                     norm.StartsWith("assets/minecraft/textures/block/", StringComparison.OrdinalIgnoreCase) ||
                     norm.StartsWith("assets/overhorizon/textures/block/", StringComparison.OrdinalIgnoreCase)))
                {
                    var stem = Path.GetFileNameWithoutExtension(norm);
                    _blockTextures[stem] = ReadEntryBytes(entry);
                }
                else if (norm.EndsWith("/lang/en_us.json", StringComparison.OrdinalIgnoreCase) ||
                         norm.EndsWith("/lang/ru_ru.json", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(norm, "assets/minecraft/lang/en_us.json", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(norm, "assets/minecraft/lang/ru_ru.json", StringComparison.OrdinalIgnoreCase))
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
            // Ignore malformed localization files.
        }
    }
}
