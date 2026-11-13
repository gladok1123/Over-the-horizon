using System.Diagnostics.CodeAnalysis;

namespace OverHorizon.Client;

/// <summary>
/// Automatically downloads and caches block textures, item sprites, GUI sheets (<c>widgets.png</c>, <c>icons.png</c>,
/// <c>slider.png</c>, <c>container/{inventory,crafting_table,furnace}.png</c>), localization JSON, and <c>.ogg</c> sound effects
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
        // Real 23w13a__or__b GUI texture sheets (textures/gui/**)
        "textures/gui/widgets.png",
        "textures/gui/icons.png",
        "textures/gui/slider.png",
        "textures/gui/container/inventory.png",
        "textures/gui/container/crafting_table.png",
        "textures/gui/container/furnace.png",

        // Block textures (textures/block/*.png)
        "textures/block/grass_block_top.png",
        "textures/block/grass_block_side.png",
        "textures/block/dirt.png",
        "textures/block/farmland.png",
        "textures/block/clay.png",
        "textures/block/stone.png",
        "textures/block/cobblestone.png",
        "textures/block/stone_bricks.png",
        "textures/block/deepslate.png",
        "textures/block/deepslate_top.png",
        "textures/block/cobbled_deepslate.png",
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
        "textures/block/birch_log.png",
        "textures/block/birch_log_top.png",
        "textures/block/birch_leaves.png",
        "textures/block/birch_planks.png",
        "textures/block/water_still.png",
        "textures/block/glass.png",
        "textures/block/coal_ore.png",
        "textures/block/deepslate_coal_ore.png",
        "textures/block/copper_ore.png",
        "textures/block/deepslate_copper_ore.png",
        "textures/block/iron_ore.png",
        "textures/block/deepslate_iron_ore.png",
        "textures/block/gold_ore.png",
        "textures/block/deepslate_gold_ore.png",
        "textures/block/lapis_ore.png",
        "textures/block/deepslate_lapis_ore.png",
        "textures/block/redstone_ore.png",
        "textures/block/deepslate_redstone_ore.png",
        "textures/block/diamond_ore.png",
        "textures/block/deepslate_diamond_ore.png",
        "textures/block/emerald_ore.png",
        "textures/block/deepslate_emerald_ore.png",
        "textures/block/coal_block.png",
        "textures/block/iron_block.png",
        "textures/block/gold_block.png",
        "textures/block/lapis_block.png",
        "textures/block/redstone_block.png",
        "textures/block/diamond_block.png",
        "textures/block/emerald_block.png",
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
        "textures/block/furnace_front_on.png",
        "textures/block/furnace_side.png",
        "textures/block/furnace_top.png",
        "textures/block/torch.png",
        "textures/block/lantern.png",
        "textures/block/ladder.png",
        "textures/block/rail.png",
        "textures/block/cobweb.png",
        "textures/block/white_wool.png",
        "textures/block/red_wool.png",
        "textures/block/blue_wool.png",
        "textures/block/dandelion.png",
        "textures/block/poppy.png",
        "textures/block/tall_grass.png",

        // Item, Tool, Sword, Hoe, Armor, Ore & Food sprites (textures/item/*.png)
        "textures/item/stick.png",
        "textures/item/coal.png",
        "textures/item/charcoal.png",
        "textures/item/raw_copper.png",
        "textures/item/copper_ingot.png",
        "textures/item/raw_iron.png",
        "textures/item/iron_ingot.png",
        "textures/item/raw_gold.png",
        "textures/item/gold_ingot.png",
        "textures/item/diamond.png",
        "textures/item/emerald.png",
        "textures/item/lapis_lazuli.png",
        "textures/item/redstone.png",
        "textures/item/flint.png",
        "textures/item/clay_ball.png",
        "textures/item/brick.png",
        "textures/item/leather.png",
        "textures/item/feather.png",
        "textures/item/string.png",
        "textures/item/bone.png",
        "textures/item/gunpowder.png",
        "textures/item/rotten_flesh.png",
        "textures/item/glowstone_dust.png",
        "textures/item/lantern.png",
        "textures/item/wooden_sword.png",
        "textures/item/stone_sword.png",
        "textures/item/iron_sword.png",
        "textures/item/golden_sword.png",
        "textures/item/diamond_sword.png",
        "textures/item/netherite_sword.png",
        "textures/item/wooden_pickaxe.png",
        "textures/item/stone_pickaxe.png",
        "textures/item/iron_pickaxe.png",
        "textures/item/golden_pickaxe.png",
        "textures/item/diamond_pickaxe.png",
        "textures/item/wooden_axe.png",
        "textures/item/stone_axe.png",
        "textures/item/iron_axe.png",
        "textures/item/golden_axe.png",
        "textures/item/diamond_axe.png",
        "textures/item/wooden_shovel.png",
        "textures/item/stone_shovel.png",
        "textures/item/iron_shovel.png",
        "textures/item/golden_shovel.png",
        "textures/item/diamond_shovel.png",
        "textures/item/wooden_hoe.png",
        "textures/item/stone_hoe.png",
        "textures/item/iron_hoe.png",
        "textures/item/golden_hoe.png",
        "textures/item/diamond_hoe.png",
        "textures/item/shears.png",
        "textures/item/flint_and_steel.png",
        "textures/item/leather_helmet.png",
        "textures/item/leather_chestplate.png",
        "textures/item/leather_leggings.png",
        "textures/item/leather_boots.png",
        "textures/item/iron_helmet.png",
        "textures/item/iron_chestplate.png",
        "textures/item/iron_leggings.png",
        "textures/item/iron_boots.png",
        "textures/item/golden_helmet.png",
        "textures/item/golden_chestplate.png",
        "textures/item/golden_leggings.png",
        "textures/item/golden_boots.png",
        "textures/item/diamond_helmet.png",
        "textures/item/diamond_chestplate.png",
        "textures/item/diamond_leggings.png",
        "textures/item/diamond_boots.png",
        "textures/item/bucket.png",
        "textures/item/water_bucket.png",
        "textures/item/apple.png",
        "textures/item/bread.png",
        "textures/item/beef.png",
        "textures/item/cooked_beef.png",
        "textures/item/porkchop.png",
        "textures/item/cooked_porkchop.png",
        "textures/item/mutton.png",
        "textures/item/cooked_mutton.png",
        "textures/item/chicken.png",
        "textures/item/cooked_chicken.png",
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
    /// <param name="targetPackRoot">Optional target pack root directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of newly downloaded asset files.</returns>
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
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OverHorizon-Client/0.5.0-alpha");

        var downloaded = 0;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 12,
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
