using OverHorizon.Modding.Manifest;

namespace OverHorizon.Modding;

/// <summary>
/// Explicit plugin entrypoint implemented by mods (no bytecode Mixins).
/// </summary>
public interface IModInitializer
{
    /// <summary>Gets the static metadata manifest for this mod.</summary>
    ModManifest Manifest { get; }

    /// <summary>
    /// Registers custom blocks, items, biomes, commands, and event-bus subscribers prior to world startup.
    /// </summary>
    /// <param name="context">Host mod initialization context.</param>
    void Initialize(IModContext context);
}
