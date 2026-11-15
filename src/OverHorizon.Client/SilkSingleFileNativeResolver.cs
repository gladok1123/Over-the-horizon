using System.Runtime.InteropServices;
using Silk.NET.Core.Loader;
using Silk.NET.GLFW;
using Silk.NET.OpenAL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;

namespace OverHorizon.Client;

/// <summary>
/// Registers a single-file self-extract native library path resolver (<c>NATIVE_DLL_SEARCH_DIRECTORIES</c>)
/// with <see cref="PathResolver.Default"/> so Silk.NET loads bundled <c>glfw3</c> and <c>soft_oal</c> libraries
/// extracted into <c>%TEMP%\.net\OverHorizon.Client\&lt;bundle-id&gt;</c> when published with <c>PublishSingleFile=true</c>.
/// </summary>
public static class SilkSingleFileNativeResolver
{
    /// <summary>Process-wide <see cref="AppContext"/> marker preventing duplicate resolver registration.</summary>
    public const string RegistrationMarkerKey = "OverHorizon.SilkSingleFileResolverRegistered";

    private const string NativeDllSearchDirectoriesKey = "NATIVE_DLL_SEARCH_DIRECTORIES";
    private static readonly Lock SyncRoot = new();

    /// <summary>
    /// Idempotently registers the single-file native directory resolver with <see cref="PathResolver.Default"/>
    /// and registers the GLFW windowing platform prior to any <see cref="Window"/> or OpenAL initialization.
    /// </summary>
    public static void EnsureRegistered()
    {
        lock (SyncRoot)
        {
            if (AppContext.GetData(RegistrationMarkerKey) is not true
                && PathResolver.Default is DefaultPathResolver defaultResolver)
            {
                var macIdx = defaultResolver.Resolvers.IndexOf(DefaultPathResolver.MacVersioningResolver);
                var insertIndex = macIdx >= 0 ? macIdx + 1 : 0;
                defaultResolver.Resolvers.Insert(insertIndex, ResolveFromRuntimeDirectories);
                AppContext.SetData(RegistrationMarkerKey, data: true);
            }
        }

        GlfwWindowing.RegisterPlatform();
    }

    /// <summary>
    /// Enumerates native library search directories for the current process, prioritizing .NET's single-file
    /// self-extraction directory (<c>NATIVE_DLL_SEARCH_DIRECTORIES</c>), <see cref="AppContext.BaseDirectory"/>,
    /// and the RID-specific <c>runtimes/&lt;rid&gt;/native</c> subfolder.
    /// </summary>
    /// <returns>Ordered distinct list of candidate native library directories.</returns>
    public static IReadOnlyList<string> GetNativeSearchDirectories()
    {
        var dirs = new List<string>();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var seen = new HashSet<string>(comparer);

        if (AppContext.GetData(NativeDllSearchDirectoriesKey) is string rawSearchDirs
            && !string.IsNullOrWhiteSpace(rawSearchDirs))
        {
            var segments = rawSearchDirs
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Path.TrimEndingDirectorySeparator)
                .Where(static s => !string.IsNullOrWhiteSpace(s))
                .Where(seen.Add);
            dirs.AddRange(segments);
        }

        if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
        {
            var baseDir = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            if (seen.Add(baseDir))
            {
                dirs.Add(baseDir);
            }

            var ridNative = Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native");
            if (seen.Add(ridNative))
            {
                dirs.Add(ridNative);
            }
        }

        return dirs;
    }

    /// <summary>
    /// Resolves full file paths for a bare native library <paramref name="candidate"/> across <paramref name="searchDirectories"/>,
    /// handling exact matches, missing <c>.dll</c> suffixes on Windows, and versioned <c>.so.*</c> / <c>.*.dylib</c> names on Unix.
    /// </summary>
    /// <param name="candidate">Candidate library name passed by Silk.NET's <see cref="DefaultPathResolver"/>.</param>
    /// <param name="searchDirectories">Ordered directories to probe.</param>
    /// <returns>Existing rooted file paths matching <paramref name="candidate"/>.</returns>
    public static IReadOnlyList<string> ResolveCandidatePaths(string candidate, IEnumerable<string> searchDirectories)
    {
        ArgumentNullException.ThrowIfNull(searchDirectories);
        if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate))
        {
            return [];
        }

        var fileName = Path.GetFileName(candidate);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return [];
        }

        var results = new List<string>();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var seen = new HashSet<string>(comparer);

        foreach (var dir in searchDirectories.Where(static d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d)))
        {
            AppendDirectoryMatches(dir, fileName, results, seen);
        }

        return results;
    }

    /// <summary>
    /// Verifies that GLFW, OpenAL Soft, and <see cref="Window.GetWindowPlatform"/> all load cleanly in the current process.
    /// </summary>
    /// <returns>Tuple of resolved GLFW target, OpenAL target, and active window platform type name.</returns>
    public static (string GlfwTarget, string OpenAlTarget, string WindowPlatformName) VerifyNativeLibrariesLoaded()
    {
        EnsureRegistered();

        using var glfw = Glfw.GetApi();
        using var alc = ALContext.GetApi(soft: true);
        using var al = AL.GetApi(soft: true);

        var platform = Window.GetWindowPlatform(viewOnly: false)
            ?? throw new PlatformNotSupportedException("Silk.NET Window.GetWindowPlatform(false) returned null after native resolver registration.");

        var glfwName = GetExpectedGlfwLibraryName();
        var openAlName = GetExpectedOpenAlLibraryName();

        var glfwResolved = PathResolver.Default.EnumeratePossibleLibraryLoadTargets(glfwName).FirstOrDefault(File.Exists) ?? glfwName;
        var openAlResolved = PathResolver.Default.EnumeratePossibleLibraryLoadTargets(openAlName).FirstOrDefault(File.Exists) ?? openAlName;

        return (glfwResolved, openAlResolved, platform.GetType().Name);
    }

    private static string GetExpectedGlfwLibraryName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "glfw3.dll";
        }

        return OperatingSystem.IsMacOS() ? "libglfw.3.dylib" : "libglfw.so.3.3";
    }

    private static string GetExpectedOpenAlLibraryName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "soft_oal.dll";
        }

        return OperatingSystem.IsMacOS() ? "libopenal.dylib" : "libopenal.so";
    }

    private static IReadOnlyList<string> ResolveFromRuntimeDirectories(string candidate) =>
        ResolveCandidatePaths(candidate, GetNativeSearchDirectories());

    private static void AppendDirectoryMatches(
        string dir,
        string fileName,
        List<string> results,
        HashSet<string> seen)
    {
        var exact = Path.Combine(dir, fileName);
        if (File.Exists(exact) && seen.Add(exact))
        {
            results.Add(exact);
        }

        if (OperatingSystem.IsWindows() && !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var withDll = Path.Combine(dir, fileName + ".dll");
            if (File.Exists(withDll) && seen.Add(withDll))
            {
                results.Add(withDll);
            }
        }

        if (fileName.Contains(".so", StringComparison.Ordinal))
        {
            results.AddRange(Directory.EnumerateFiles(dir, fileName + ".*").Order(StringComparer.Ordinal).Where(seen.Add));
        }

        if (fileName.EndsWith(".dylib", StringComparison.Ordinal))
        {
            var stem = fileName[..^".dylib".Length];
            results.AddRange(Directory.EnumerateFiles(dir, stem + ".*.dylib").Order(StringComparer.Ordinal).Where(seen.Add));
        }
    }
}
