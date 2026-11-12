using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace OverHorizon.Modding.Loading;

/// <summary>
/// Discovers and instantiates <see cref="IModInitializer"/> implementations from external plugin <c>.dll</c> files
/// using isolated collectible <c>AssemblyLoadContext</c> instances.
/// </summary>
public sealed class PluginAssemblyLoader : IDisposable
{
    private readonly List<CollectibleModLoadContext> _contexts = [];
    private int _disposed;

    /// <summary>Gets the number of active plugin assembly load contexts.</summary>
    public int LoadedAssemblyCount => _contexts.Count;

    /// <summary>
    /// Loads a single plugin <c>.dll</c> file and instantiates all exported parameterless <see cref="IModInitializer"/> types.
    /// Requires a JIT-capable non-trimmed runtime (<see cref="RuntimeFeature.IsDynamicCodeSupported"/>).
    /// </summary>
    /// <param name="assemblyPath">Full path to the plugin assembly.</param>
    /// <returns>Instantiated <see cref="IModInitializer"/> plugins.</returns>
    [RequiresUnreferencedCode("Dynamic plugin DLL discovery requires unreferenced metadata.")]
    [RequiresDynamicCode("Dynamic plugin DLL loading requires runtime code generation.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072",
        Justification = "External plugin DLLs are only supported when dynamic code is enabled.")]
    public IReadOnlyList<IModInitializer> LoadPluginAssembly(string assemblyPath)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new PlatformNotSupportedException(
                "Dynamic plugin DLL loading is not supported under Native AOT; register IModInitializer statically or use JSON datapacks.");
        }

        var fullPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Plugin assembly not found.", fullPath);
        }

        var alc = new CollectibleModLoadContext(fullPath);
        _contexts.Add(alc);

        var assembly = alc.LoadFromAssemblyPath(fullPath);
        var exported = assembly.GetExportedTypes();
        var discovered = new List<IModInitializer>();

        for (var i = 0; i < exported.Length; i++)
        {
            var type = exported[i];
            if (!type.IsAbstract && !type.IsInterface && typeof(IModInitializer).IsAssignableFrom(type))
            {
                if (Activator.CreateInstance(type) is not IModInitializer instance)
                {
                    throw new ModLoadException($"Failed to instantiate IModInitializer '{type.FullName}'.");
                }

                discovered.Add(instance);
            }
        }

        return discovered;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        for (var i = 0; i < _contexts.Count; i++)
        {
            _contexts[i].Unload();
        }

        _contexts.Clear();
    }
}
