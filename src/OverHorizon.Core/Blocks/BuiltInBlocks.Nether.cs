using OverHorizon.Core.Registry;

namespace OverHorizon.Core.Blocks;

/// <summary>
/// Nether dimension block definitions introduced in <c>v0.11.0-alpha</c> (kept in a separate partial
/// file so <see cref="BuiltInBlocks"/> never grows into a God class).
/// </summary>
public static partial class BuiltInBlocks
{
    /// <summary>Key for <c>overhorizon:netherrack</c> (the default Nether stone).</summary>
    public static readonly ResourceKey Netherrack = ResourceKey.OverHorizon("netherrack");

    /// <summary>Key for <c>overhorizon:crimson_nylium</c> (Crimson Forest surface).</summary>
    public static readonly ResourceKey CrimsonNylium = ResourceKey.OverHorizon("crimson_nylium");

    /// <summary>Key for <c>overhorizon:warped_nylium</c> (Warped Forest surface).</summary>
    public static readonly ResourceKey WarpedNylium = ResourceKey.OverHorizon("warped_nylium");

    /// <summary>Key for <c>overhorizon:soul_sand</c> (Soul Sand Valley floor; slows movement).</summary>
    public static readonly ResourceKey SoulSand = ResourceKey.OverHorizon("soul_sand");

    /// <summary>Key for <c>overhorizon:soul_soil</c> (Soul Sand Valley sub-surface).</summary>
    public static readonly ResourceKey SoulSoil = ResourceKey.OverHorizon("soul_soil");

    /// <summary>Key for <c>overhorizon:basalt</c> (Basalt Deltas pillar stone).</summary>
    public static readonly ResourceKey Basalt = ResourceKey.OverHorizon("basalt");

    /// <summary>Key for <c>overhorizon:magma_block</c> (emits light 3 and burns anything standing on it).</summary>
    public static readonly ResourceKey MagmaBlock = ResourceKey.OverHorizon("magma_block");

    /// <summary>Key for <c>overhorizon:nether_quartz_ore</c> (drops Nether Quartz).</summary>
    public static readonly ResourceKey NetherQuartzOre = ResourceKey.OverHorizon("nether_quartz_ore");

    /// <summary>Key for <c>overhorizon:nether_gold_ore</c> (drops Gold Nuggets).</summary>
    public static readonly ResourceKey NetherGoldOre = ResourceKey.OverHorizon("nether_gold_ore");

    /// <summary>Key for <c>overhorizon:ancient_debris</c> (blast-resistant Netherite source).</summary>
    public static readonly ResourceKey AncientDebris = ResourceKey.OverHorizon("ancient_debris");

    /// <summary>Key for <c>overhorizon:nether_bricks</c> (Nether Fortress masonry).</summary>
    public static readonly ResourceKey NetherBricks = ResourceKey.OverHorizon("nether_bricks");

    /// <summary>Key for <c>overhorizon:blackstone</c> (Nether fortress &amp; Blackstone variant).</summary>
    public static readonly ResourceKey Blackstone = ResourceKey.OverHorizon("blackstone");

    /// <summary>Key for <c>overhorizon:nether_wart_block</c> (Crimson Forest organic block).</summary>
    public static readonly ResourceKey NetherWartBlock = ResourceKey.OverHorizon("nether_wart_block");

    /// <summary>Key for <c>overhorizon:warped_wart_block</c> (Warped Forest organic block).</summary>
    public static readonly ResourceKey WarpedWartBlock = ResourceKey.OverHorizon("warped_wart_block");

    /// <summary>Key for <c>overhorizon:shroomlight</c> (emits block light 15).</summary>
    public static readonly ResourceKey Shroomlight = ResourceKey.OverHorizon("shroomlight");

    /// <summary>Key for <c>overhorizon:nether_portal</c> (emits light 11; teleports after a short dwell).</summary>
    public static readonly ResourceKey NetherPortal = ResourceKey.OverHorizon("nether_portal");

    /// <summary>Key for <c>overhorizon:lava</c> (8-level flowing fluid emitting light 15).</summary>
    public static readonly ResourceKey Lava = ResourceKey.OverHorizon("lava");

    /// <summary>
    /// Registers every Nether block into an unfrozen <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">Target unfrozen block registry.</param>
    public static void PopulateNetherBlocks(BlockRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var portalAxis = new BlockProperty("axis", ["x", "z"]);

        _ = registry.Register(new BlockDefinition(Netherrack, hardness: 0.4f));
        _ = registry.Register(new BlockDefinition(CrimsonNylium, hardness: 0.4f));
        _ = registry.Register(new BlockDefinition(WarpedNylium, hardness: 0.4f));
        _ = registry.Register(new BlockDefinition(SoulSand, hardness: 0.5f));
        _ = registry.Register(new BlockDefinition(SoulSoil, hardness: 0.5f));
        _ = registry.Register(new BlockDefinition(Basalt, hardness: 1.25f));
        _ = registry.Register(new BlockDefinition(MagmaBlock, lightEmission: 3, hardness: 0.5f));
        _ = registry.Register(new BlockDefinition(NetherQuartzOre, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(NetherGoldOre, hardness: 3.0f));
        _ = registry.Register(new BlockDefinition(AncientDebris, hardness: 30.0f));
        _ = registry.Register(new BlockDefinition(NetherBricks, hardness: 2.0f));
        _ = registry.Register(new BlockDefinition(Blackstone, hardness: 1.5f));
        _ = registry.Register(new BlockDefinition(NetherWartBlock, hardness: 1.0f));
        _ = registry.Register(new BlockDefinition(WarpedWartBlock, hardness: 1.0f));
        _ = registry.Register(new BlockDefinition(Shroomlight, lightEmission: 15, hardness: 1.0f));
        _ = registry.Register(new BlockDefinition(
            NetherPortal,
            isSolid: false,
            isOpaque: false,
            lightEmission: 11,
            hardness: -1.0f,
            properties: [portalAxis]));
        _ = registry.Register(new BlockDefinition(
            Lava,
            isSolid: false,
            isOpaque: false,
            isFluid: true,
            lightEmission: 15,
            hardness: 100.0f,
            properties: [BlockProperty.IntegerRange("level", 0, 15)]));
    }
}
