using System.IO.Compression;
using System.Text.Json;

namespace OverHorizon.Rendering.Textures;

public sealed partial class ResourcePackLoader
{
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
