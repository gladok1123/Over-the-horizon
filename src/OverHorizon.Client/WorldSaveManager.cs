using System.Globalization;
using System.Text;
using System.Text.Json;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.World;
using OverHorizon.World.ChunkManagement;
using OverHorizon.World.Generation;
using OverHorizon.World.Serialization;

namespace OverHorizon.Client;

/// <summary>
/// Manages singleplayer world discovery, creation, seed parsing, metadata persistence, deletion,
/// and <see cref="WorldSubsystem"/> instantiation inside <c>./saves/</c>.
/// </summary>
public static class WorldSaveManager
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>Resolves the default <c>saves</c> root directory next to the working/application directory.</summary>
    public static string GetDefaultSavesRoot()
    {
        var cwdSaves = Path.GetFullPath("saves");
        return cwdSaves;
    }

    /// <summary>Parses a user-entered seed string (blank -> random 64-bit, integer -> exact long, text -> deterministic hash).</summary>
    public static long ParseSeed(string? seedInput)
    {
        if (string.IsNullOrWhiteSpace(seedInput))
        {
            return Random.Shared.NextInt64();
        }

        var trimmed = seedInput.Trim();
        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric;
        }

        long hash = 0;
        for (var i = 0; i < trimmed.Length; i++)
        {
            hash = unchecked((hash * 31L) + trimmed[i]);
        }

        return hash == 0 ? 0x5F3759DF42424242L : hash;
    }

    /// <summary>Lists all saved worlds under <paramref name="savesRoot"/> ordered by most recently played.</summary>
    public static IReadOnlyList<SavedWorldMetadata> ListWorlds(string? savesRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(savesRoot) ? GetDefaultSavesRoot() : Path.GetFullPath(savesRoot);
        if (!Directory.Exists(root))
        {
            return [];
        }

        var list = new List<SavedWorldMetadata>();
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var metaFile = Path.Combine(dir, "world.json");
            if (!File.Exists(metaFile))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(metaFile);
                var loaded = JsonSerializer.Deserialize<SavedWorldMetadata>(json, JsonOpts);
                if (loaded is not null)
                {
                    list.Add(loaded with { FolderPath = dir });
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // Skip malformed world folder.
            }
        }

        list.Sort(static (a, b) => b.LastPlayedUtc.CompareTo(a.LastPlayedUtc));
        return list;
    }

    /// <summary>Creates a new world directory under <paramref name="savesRoot"/> and writes initial <c>world.json</c>.</summary>
    public static SavedWorldMetadata CreateWorld(
        string? worldName,
        GameMode mode,
        string? seedInput,
        string? savesRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(savesRoot) ? GetDefaultSavesRoot() : Path.GetFullPath(savesRoot);
        Directory.CreateDirectory(root);

        var cleanName = string.IsNullOrWhiteSpace(worldName) ? "New World" : worldName.Trim();
        var baseSlug = SanitizeSlug(cleanName);
        var slug = baseSlug;
        var suffix = 2;
        while (Directory.Exists(Path.Combine(root, slug)))
        {
            slug = $"{baseSlug}_{suffix.ToString(CultureInfo.InvariantCulture)}";
            suffix++;
        }

        var folder = Path.Combine(root, slug);
        Directory.CreateDirectory(Path.Combine(folder, "chunks"));

        var meta = new SavedWorldMetadata(
            Slug: slug,
            WorldName: cleanName,
            Seed: ParseSeed(seedInput),
            DefaultGameMode: mode,
            WorldTimeTicks: WorldTimeCycle.DayTicks,
            LastPlayedUtc: DateTimeOffset.UtcNow,
            PlayerX: 0.5,
            PlayerY: 80.0,
            PlayerZ: 0.5,
            HasSavedPose: false,
            FolderPath: folder);

        SaveMetadata(meta);
        return meta;
    }

    /// <summary>Persists updated <paramref name="meta"/> to <c>world.json</c>.</summary>
    public static void SaveMetadata(SavedWorldMetadata meta)
    {
        ArgumentNullException.ThrowIfNull(meta);
        if (string.IsNullOrWhiteSpace(meta.FolderPath))
        {
            return;
        }

        Directory.CreateDirectory(meta.FolderPath);
        var path = Path.Combine(meta.FolderPath, "world.json");
        var updated = meta with { LastPlayedUtc = DateTimeOffset.UtcNow };
        File.WriteAllText(path, JsonSerializer.Serialize(updated, JsonOpts));
    }

    /// <summary>Deletes a saved world folder by slug.</summary>
    public static bool DeleteWorld(string slug, string? savesRoot = null)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        var root = string.IsNullOrWhiteSpace(savesRoot) ? GetDefaultSavesRoot() : Path.GetFullPath(savesRoot);
        var folder = Path.Combine(root, Path.GetFileName(slug));
        if (!Directory.Exists(folder))
        {
            return false;
        }

        Directory.Delete(folder, recursive: true);
        return true;
    }

    /// <summary>Opens a <see cref="WorldSubsystem"/> backed by <see cref="FileChunkStorage"/> in <c>&lt;FolderPath&gt;/chunks</c>.</summary>
    public static WorldSubsystem OpenWorldSubsystem(SavedWorldMetadata meta, CoreRegistries registries)
    {
        ArgumentNullException.ThrowIfNull(meta);
        ArgumentNullException.ThrowIfNull(registries);

        var chunksDir = Path.Combine(meta.FolderPath, "chunks");
        Directory.CreateDirectory(chunksDir);
        var generator = new OverworldGenerator(meta.Seed, registries);
        var storage = new FileChunkStorage(chunksDir, registries);
        var manager = new ChunkManager(registries, generator, storage);
        return new WorldSubsystem(manager);
    }

    /// <summary>Extracts the optional bed respawn coordinate from <paramref name="meta"/>.</summary>
    public static Vec3d? GetRespawnVec(SavedWorldMetadata meta) =>
        meta.RespawnX is { } rx && meta.RespawnY is { } ry && meta.RespawnZ is { } rz
            ? new Vec3d(rx, ry, rz)
            : null;

    private static string SanitizeSlug(string name)
    {
        var sb = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (ch is ' ' or '-' or '_')
            {
                sb.Append('_');
            }
        }

        var result = sb.ToString().Trim('_');
        return result.Length == 0 ? "world" : result;
    }
}
