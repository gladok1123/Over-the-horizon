namespace OverHorizon.Modding.Loading;

/// <summary>
/// Deterministically resolves mod initialization order from <c>ModManifest.Dependencies</c>,
/// detecting duplicate IDs, missing dependencies, and circular dependency graphs.
/// </summary>
public static class ModDependencySorter
{
    /// <summary>
    /// Topologically orders <paramref name="mods"/> so every dependency initializes before its dependents.
    /// Ties are broken deterministically by ordinal mod ID.
    /// </summary>
    /// <param name="mods">Unordered candidate mods.</param>
    /// <returns>Deterministically ordered list of <see cref="IModInitializer"/>s.</returns>
    public static IReadOnlyList<IModInitializer> Sort(IEnumerable<IModInitializer> mods)
    {
        ArgumentNullException.ThrowIfNull(mods);

        var byId = new Dictionary<string, IModInitializer>(StringComparer.Ordinal);
        foreach (var mod in mods)
        {
            if (mod?.Manifest is null || string.IsNullOrWhiteSpace(mod.Manifest.Id))
            {
                throw new ModLoadException("Encountered a mod with null or empty Manifest.Id.");
            }

            if (!byId.TryAdd(mod.Manifest.Id, mod))
            {
                throw new ModLoadException($"Duplicate mod ID '{mod.Manifest.Id}'.");
            }
        }

        if (byId.Count == 0)
        {
            return [];
        }

        // Validate all declared dependencies exist.
        foreach (var (id, mod) in byId)
        {
            if (mod.Manifest.Dependencies is not { } deps)
            {
                continue;
            }

            for (var i = 0; i < deps.Count; i++)
            {
                var depId = deps[i];
                if (!byId.ContainsKey(depId))
                {
                    throw new ModLoadException($"Mod '{id}' requires missing dependency '{depId}'.");
                }
            }
        }

        // Deterministic DFS topological sort (0 = unvisited, 1 = visiting, 2 = visited).
        var state = new Dictionary<string, int>(byId.Count, StringComparer.Ordinal);
        var ordered = new List<IModInitializer>(byId.Count);
        var sortedIds = byId.Keys.OrderBy(static k => k, StringComparer.Ordinal).ToArray();

        for (var i = 0; i < sortedIds.Length; i++)
        {
            Visit(sortedIds[i], byId, state, ordered);
        }

        return ordered;
    }

    private static void Visit(
        string id,
        Dictionary<string, IModInitializer> byId,
        Dictionary<string, int> state,
        List<IModInitializer> ordered)
    {
        if (state.TryGetValue(id, out var existingState))
        {
            if (existingState == 1)
            {
                throw new ModLoadException($"Circular mod dependency detected involving '{id}'.");
            }

            return;
        }

        state[id] = 1;
        var mod = byId[id];
        if (mod.Manifest.Dependencies is { } deps && deps.Count > 0)
        {
            var sortedDeps = deps.OrderBy(static d => d, StringComparer.Ordinal).ToArray();
            for (var i = 0; i < sortedDeps.Length; i++)
            {
                Visit(sortedDeps[i], byId, state, ordered);
            }
        }

        state[id] = 2;
        ordered.Add(mod);
    }
}
