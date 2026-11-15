using Silk.NET.Core.Loader;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Makes Silk.NET find its native libraries inside a single-file publish bundle, where the runtime extracts
/// them next to the executable instead of into the NuGet package layout Silk.NET expects.
/// </summary>
internal static class SilkNativeResolver
{
    private static readonly Lock SyncRoot = new();

    /// <summary>Registers the single-file aware path resolver exactly once per process.</summary>
    public static void EnsureRegistered()
    {
        const string markerKey = "OverHorizon.SilkSingleFileResolverRegistered";
        lock (SyncRoot)
        {
            if (AppContext.GetData(markerKey) is true || PathResolver.Default is not DefaultPathResolver resolver)
            {
                return;
            }

            var idx = resolver.Resolvers.IndexOf(DefaultPathResolver.MacVersioningResolver);
            resolver.Resolvers.Insert(idx >= 0 ? idx + 1 : 0, ResolveNativeFromAppContext);
            AppContext.SetData(markerKey, data: true);
        }
    }

    private static List<string> ResolveNativeFromAppContext(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate))
        {
            return [];
        }

        var fileName = Path.GetFileName(candidate);
        if (string.IsNullOrWhiteSpace(fileName)
            || AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is not string rawDirs
            || string.IsNullOrWhiteSpace(rawDirs))
        {
            return [];
        }

        var matches = new List<string>();
        foreach (var dir in rawDirs.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(Directory.Exists))
        {
            var exact = Path.Combine(dir, fileName);
            if (File.Exists(exact))
            {
                matches.Add(exact);
            }

            if (fileName.Contains(".so", StringComparison.Ordinal))
            {
                matches.AddRange(Directory.EnumerateFiles(dir, fileName + ".*").Order(StringComparer.Ordinal));
            }
            else if (fileName.EndsWith(".dylib", StringComparison.Ordinal))
            {
                matches.AddRange(Directory.EnumerateFiles(dir, fileName[..^".dylib".Length] + ".*.dylib").Order(StringComparer.Ordinal));
            }
        }

        return matches;
    }
}
