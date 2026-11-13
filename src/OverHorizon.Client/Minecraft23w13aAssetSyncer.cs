using System.Diagnostics.CodeAnalysis;

namespace OverHorizon.Client;

/// <summary>
/// Automatically downloads and caches block textures, item sprites, <c>destroy_stage_0..9</c> crack overlays,
/// GUI &amp; Creative sheets, Title logo (<c>minecraft.png</c>, <c>edition.png</c>), entity/mob/player skins (<c>steve.png</c>, etc.),
/// localization JSON, and authentic <c>.ogg</c> sound effects from
/// <c>https://github.com/InventivetalentDev/minecraft-assets/tree/23w13a__or__b</c> into local
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
        // Real 23w13a__or__b GUI, Title & Creative Inventory texture sheets (textures/gui/**)
        "textures/gui/widgets.png",
        "textures/gui/icons.png",
        "textures/gui/slider.png",
        "textures/gui/options_background.png",
        "textures/gui/world_selection.png",
        "textures/gui/title/minecraft.png",
        "textures/gui/title/edition.png",
        "textures/gui/container/inventory.png",
        "textures/gui/container/crafting_table.png",
        "textures/gui/container/furnace.png",
        "textures/gui/container/creative_inventory/tab_items.png",
        "textures/gui/container/creative_inventory/tab_item_search.png",
        "textures/gui/container/creative_inventory/tab_inventory.png",
        "textures/gui/container/creative_inventory/tabs.png",

        // Real 23w13a__or__b Player & Mob 3D UV Skins (textures/entity/**)
        "textures/entity/player/wide/steve.png",
        "textures/entity/pig/pig.png",
        "textures/entity/cow/cow.png",
        "textures/entity/sheep/sheep.png",
        "textures/entity/sheep/sheep_fur.png",
        "textures/entity/chicken.png",
        "textures/entity/zombie/zombie.png",
        "textures/entity/skeleton/skeleton.png",
        "textures/entity/spider/spider.png",
        "textures/entity/creeper/creeper.png",
        "textures/entity/bed/red.png",
        "textures/entity/shield/base.png",

        // Block Mining Crack Stages 0..9 (textures/block/destroy_stage_*.png)
        "textures/block/destroy_stage_0.png",
        "textures/block/destroy_stage_1.png",
        "textures/block/destroy_stage_2.png",
        "textures/block/destroy_stage_3.png",
        "textures/block/destroy_stage_4.png",
        "textures/block/destroy_stage_5.png",
        "textures/block/destroy_stage_6.png",
        "textures/block/destroy_stage_7.png",
        "textures/block/destroy_stage_8.png",
        "textures/block/destroy_stage_9.png",

        // Block textures (textures/block/*.png)
        "textures/block/grass_block_top.png",
        "textures/block/grass_block_side.png",
        "textures/block/grass_block_side_overlay.png",
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
        "textures/block/water_flow.png",
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
        "textures/block/copper_block.png",
        "textures/block/iron_block.png",
        "textures/block/gold_block.png",
        "textures/block/lapis_block.png",
        "textures/block/redstone_block.png",
        "textures/block/diamond_block.png",
        "textures/block/emerald_block.png",
        "textures/block/snow.png",
        "textures/block/ice.png",
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
        "textures/block/barrel_side.png",
        "textures/block/barrel_top.png",
        "textures/block/hay_block_side.png",
        "textures/block/hay_block_top.png",
        "textures/block/torch.png",
        "textures/block/lantern.png",
        "textures/block/ladder.png",
        "textures/block/rail.png",
        "textures/block/cobweb.png",
        "textures/block/white_wool.png",
        "textures/block/red_wool.png",
        "textures/block/blue_wool.png",
        "textures/block/fire_0.png",
        "textures/block/lever.png",
        "textures/block/piston_bottom.png",
        "textures/block/piston_side.png",
        "textures/block/piston_top.png",
        "textures/block/piston_top_sticky.png",
        "textures/block/piston_inner.png",
        "textures/block/redstone_dust_dot.png",
        "textures/block/redstone_dust_line0.png",
        "textures/block/redstone_lamp.png",
        "textures/block/redstone_lamp_on.png",
        "textures/block/redstone_torch.png",
        "textures/block/redstone_torch_off.png",
        "textures/block/repeater.png",
        "textures/block/repeater_on.png",

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
        "textures/item/repeater.png",
        "textures/item/netherite_ingot.png",
        "textures/item/flint.png",
        "textures/item/leather.png",
        "textures/item/feather.png",
        "textures/item/string.png",
        "textures/item/bone.png",
        "textures/item/gunpowder.png",
        "textures/item/spider_eye.png",
        "textures/item/rotten_flesh.png",
        "textures/item/paper.png",
        "textures/item/book.png",
        "textures/item/slime_ball.png",
        "textures/item/wheat.png",
        "textures/item/sugar_cane.png",
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
        "textures/item/netherite_pickaxe.png",
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

        // Authentic Ogg Vorbis sounds from 23w13a__or__b (steps, dig, water, combat, fire, TNT, pistons & all 8 mobs)
        "sounds/step/stone1.ogg",
        "sounds/step/grass1.ogg",
        "sounds/step/wood1.ogg",
        "sounds/step/sand1.ogg",
        "sounds/step/gravel1.ogg",
        "sounds/step/cloth1.ogg",
        "sounds/step/ladder1.ogg",
        "sounds/dig/stone1.ogg",
        "sounds/dig/grass1.ogg",
        "sounds/dig/wood1.ogg",
        "sounds/dig/gravel1.ogg",
        "sounds/dig/sand1.ogg",
        "sounds/dig/cloth1.ogg",
        "sounds/random/glass1.ogg",
        "sounds/random/click.ogg",
        "sounds/random/pop.ogg",
        "sounds/random/eat1.ogg",
        "sounds/random/burp.ogg",
        "sounds/random/fizz.ogg",
        "sounds/random/fuse.ogg",
        "sounds/random/explode1.ogg",
        "sounds/liquid/splash.ogg",
        "sounds/liquid/swim1.ogg",
        "sounds/ambient/underwater/enter1.ogg",
        "sounds/ambient/underwater/exit1.ogg",
        "sounds/ambient/underwater/underwater_ambience.ogg",
        "sounds/entity/player/attack/sweep1.ogg",
        "sounds/entity/player/attack/strong1.ogg",
        "sounds/entity/player/attack/crit1.ogg",
        "sounds/damage/hit1.ogg",
        "sounds/fire/ignite.ogg",
        "sounds/fire/fire.ogg",
        "sounds/tile/piston/out.ogg",
        "sounds/tile/piston/in.ogg",
        "sounds/mob/pig/say1.ogg",
        "sounds/mob/pig/death.ogg",
        "sounds/mob/cow/say1.ogg",
        "sounds/mob/cow/hurt1.ogg",
        "sounds/mob/sheep/say1.ogg",
        "sounds/mob/sheep/shear.ogg",
        "sounds/mob/chicken/say1.ogg",
        "sounds/mob/chicken/hurt1.ogg",
        "sounds/mob/zombie/say1.ogg",
        "sounds/mob/zombie/hurt1.ogg",
        "sounds/mob/zombie/death.ogg",
        "sounds/mob/skeleton/say1.ogg",
        "sounds/mob/skeleton/hurt1.ogg",
        "sounds/mob/skeleton/death.ogg",
        "sounds/mob/spider/say1.ogg",
        "sounds/mob/spider/death.ogg",
        "sounds/mob/creeper/say1.ogg",
        "sounds/mob/creeper/death.ogg",
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
            Timeout = TimeSpan.FromSeconds(10),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OverHorizon-Client/0.6.0-alpha");

        var downloaded = 0;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 16,
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
                    // Offline or rate-limited: engine falls back to clean-room procedural assets.
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
