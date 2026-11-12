using System.Diagnostics.CodeAnalysis;

namespace OverHorizon.Client;

/// <summary>
/// Automatically downloads and caches block textures, item sprites, localization JSON, and <c>.ogg</c> sound effects
/// from <c>https://github.com/InventivetalentDev/minecraft-assets/tree/23w13a__or__b</c> into local
/// <c>./resourcepacks/23w13a__or__b/</c> on client startup so zero proprietary assets are redistributed in Git or releases.
/// </summary>
[SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "Default public GitHub asset mirror for 23w13a__or__b; overridable via --assets.")]
public static class Minecraft23w13aAssetSyncer
{
    /// <summary>Default local resource-pack directory populated from <c>23w13a__or__b</c>.</summary>
    public const string DefaultPackDirectory = "./resourcepacks/23w13a__or__b";

    /// <summary>Raw GitHub base URL for branch <c>23w13a__or__b</c>.</summary>
    public const string RawBaseUrl = "https://raw.githubusercontent.com/InventivetalentDev/minecraft-assets/23w13a__or__b/assets/minecraft";

    private static readonly string[] RelativeAssetPaths =
    [
        // Block textures (textures/block/*.png)
        "textures/block/grass_block_top.png",
        "textures/block/grass_block_side.png",
        "textures/block/dirt.png",
        "textures/block/stone.png",
        "textures/block/cobblestone.png",
        "textures/block/bedrock.png",
        "textures/block/sand.png",
        "textures/block/sandstone.png",
        "textures/block/gravel.png",
        "textures/block/obsidian.png",
        "textures/block/tnt_side.png",
        "textures/block/tnt_top.png",
        "textures/block/tnt_bottom.png",
        "textures/block/oak_log.png",
        "textures/block/oak_log_top.png",
        "textures/block/oak_leaves.png",
        "textures/block/oak_planks.png",
        "textures/block/water_still.png",
        "textures/block/glass.png",
        "textures/block/coal_ore.png",
        "textures/block/iron_ore.png",
        "textures/block/gold_ore.png",
        "textures/block/diamond_ore.png",
        "textures/block/snow.png",
        "textures/block/ice.png",
        "textures/block/cactus_side.png",
        "textures/block/cactus_top.png",
        "textures/block/bricks.png",
        "textures/block/glowstone.png",
        "textures/block/bookshelf.png",
        "textures/block/mossy_cobblestone.png",
        "textures/block/crafting_table_top.png",
        "textures/block/crafting_table_side.png",
        "textures/block/furnace_front.png",
        "textures/block/furnace_side.png",
        "textures/block/furnace_top.png",
        "textures/block/white_wool.png",
        "textures/block/blue_wool.png",
        "textures/block/dandelion.png",
        "textures/block/poppy.png",
        "textures/block/tall_grass.png",

        // Item sprites (textures/item/*.png)
        "textures/item/stick.png",
        "textures/item/coal.png",
        "textures/item/iron_ingot.png",
        "textures/item/gold_ingot.png",
        "textures/item/diamond.png",
        "textures/item/wooden_sword.png",
        "textures/item/stone_sword.png",
        "textures/item/iron_sword.png",
        "textures/item/golden_sword.png",
        "textures/item/diamond_sword.png",
        "textures/item/wooden_pickaxe.png",
        "textures/item/stone_pickaxe.png",
        "textures/item/iron_pickaxe.png",
        "textures/item/diamond_pickaxe.png",
        "textures/item/wooden_axe.png",
        "textures/item/stone_axe.png",
        "textures/item/iron_axe.png",
        "textures/item/diamond_axe.png",
        "textures/item/stone_shovel.png",
        "textures/item/iron_shovel.png",
        "textures/item/shears.png",
        "textures/item/flint_and_steel.png",
        "textures/item/bucket.png",
        "textures/item/water_bucket.png",
        "textures/item/apple.png",
        "textures/item/bread.png",
        "textures/item/cooked_beef.png",
        "textures/item/golden_apple.png",

        // Localization & Underwater overlay
        "textures/misc/underwater.png",
        "lang/en_us.json",

        // Real Ogg Vorbis sounds from 23w13a__or__b
        "sounds/step/grass1.ogg",
        "sounds/dig/stone1.ogg",
        "sounds/dig/gravel1.ogg",
        "sounds/liquid/splash.ogg",
        "sounds/liquid/swim1.ogg",
        "sounds/ambient/underwater/underwater_ambience.ogg",
        "sounds/entity/player/attack/sweep1.ogg",
        "sounds/entity/player/attack/strong1.ogg",
        "sounds/entity/player/attack/crit1.ogg",
        "sounds/damage/hit1.ogg",
        "sounds/random/eat1.ogg",
        "sounds/random/click.ogg",
        "sounds/random/levelup.ogg",
    ];

    /// <summary>
    /// Gets the list of relative <c>assets/minecraft/...</c> paths synced from <c>23w13a__or__b</c>.
    /// </summary>
    public static IReadOnlyList<string> AssetManifest => RelativeAssetPaths;

    /// <summary>
    /// Downloads any missing <c>23w13a__or__b</c> PNG/OGG/JSON assets into <paramref name="targetPackRoot"/>.
    /// Safe to run offline: network errors time out gracefully without throwing.
    /// </summary>
    public static async Task<int> EnsureAssetsCachedAsync(string? targetPackRoot = null, CancellationToken ct = default)
    {
        var packRoot = string.IsNullOrWhiteSpace(targetPackRoot) ? DefaultPackDirectory : targetPackRoot;
        var mcRoot = Path.Combine(packRoot, "assets", "minecraft");

        var missing = RelativeAssetPaths
            .Where(rel => !File.Exists(Path.Combine(mcRoot, rel.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();

        if (missing.Length == 0)
        {
            return 0;
        }

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OverHorizon-Client/0.4.0-alpha");

        var downloaded = 0;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 10,
            CancellationToken = ct,
        };

        try
        {
            await Parallel.ForEachAsync(missing, parallelOptions, async (relPath, token) =>
            {
                var destFile = Path.Combine(mcRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
                var dir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                try
                {
                    var bytes = await http.GetByteArrayAsync(new Uri($"{RawBaseUrl}/{relPath}"), token).ConfigureAwait(false);
                    if (bytes.Length > 0)
                    {
                        await File.WriteAllBytesAsync(destFile, bytes, token).ConfigureAwait(false);
                        Interlocked.Increment(ref downloaded);
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
                {
                    // Offline or rate-limited: engine falls back to embedded CC0 OGG audio + clean-room sprites.
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // User canceled startup.
        }

        return downloaded;
    }
}
