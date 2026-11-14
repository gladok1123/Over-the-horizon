using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace OverHorizon.Modding.Loading;

internal sealed class CollectibleModLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public CollectibleModLoadContext(string mainPluginAssemblyPath)
        : base(Path.GetFileNameWithoutExtension(mainPluginAssemblyPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainPluginAssemblyPath);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "CollectibleModLoadContext is only instantiated when RuntimeFeature.IsDynamicCodeSupported is true.")]
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var simpleName = assemblyName.Name;
        if (simpleName is not null &&
            (simpleName.StartsWith("OverHorizon.", StringComparison.Ordinal) ||
             simpleName.StartsWith("System.", StringComparison.Ordinal) ||
             simpleName.StartsWith("Microsoft.", StringComparison.Ordinal) ||
             string.Equals(simpleName, "netstandard", StringComparison.Ordinal)))
        {
            // Share host SDK and engine contract assemblies from Default load context.
            return null;
        }

        var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return resolvedPath is not null ? LoadFromAssemblyPath(resolvedPath) : null;
    }
}
