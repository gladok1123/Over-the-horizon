param(
    [string]$Ref = "23w13a__or__b",
    [string]$OutDir = "./resourcepacks/23w13a__or__b"
)

$ErrorActionPreference = "Stop"
$BaseUrl = "https://raw.githubusercontent.com/InventivetalentDev/minecraft-assets/$Ref/assets/minecraft"

$Assets = @(
    "textures/block/grass_block_top.png", "textures/block/grass_block_side.png", "textures/block/dirt.png",
    "textures/block/stone.png", "textures/block/cobblestone.png", "textures/block/bedrock.png",
    "textures/block/sand.png", "textures/block/sandstone.png", "textures/block/gravel.png",
    "textures/block/obsidian.png", "textures/block/tnt_side.png", "textures/block/tnt_top.png", "textures/block/tnt_bottom.png",
    "textures/block/oak_log.png", "textures/block/oak_log_top.png", "textures/block/oak_leaves.png", "textures/block/oak_planks.png",
    "textures/block/water_still.png", "textures/block/glass.png", "textures/block/coal_ore.png", "textures/block/iron_ore.png",
    "textures/block/gold_ore.png", "textures/block/diamond_ore.png", "textures/block/snow.png", "textures/block/ice.png",
    "textures/block/cactus_side.png", "textures/block/cactus_top.png", "textures/block/bricks.png", "textures/block/glowstone.png",
    "textures/block/bookshelf.png", "textures/block/mossy_cobblestone.png", "textures/block/crafting_table_top.png",
    "textures/block/crafting_table_side.png", "textures/block/furnace_front.png", "textures/block/furnace_side.png",
    "textures/block/furnace_top.png", "textures/block/white_wool.png", "textures/block/blue_wool.png",
    "textures/block/dandelion.png", "textures/block/poppy.png", "textures/block/tall_grass.png",
    "textures/item/stick.png", "textures/item/coal.png", "textures/item/iron_ingot.png", "textures/item/gold_ingot.png", "textures/item/diamond.png",
    "textures/item/wooden_sword.png", "textures/item/stone_sword.png", "textures/item/iron_sword.png", "textures/item/golden_sword.png", "textures/item/diamond_sword.png",
    "textures/item/wooden_pickaxe.png", "textures/item/stone_pickaxe.png", "textures/item/iron_pickaxe.png", "textures/item/diamond_pickaxe.png",
    "textures/item/wooden_axe.png", "textures/item/stone_axe.png", "textures/item/iron_axe.png", "textures/item/diamond_axe.png",
    "textures/item/stone_shovel.png", "textures/item/iron_shovel.png", "textures/item/shears.png", "textures/item/flint_and_steel.png",
    "textures/item/bucket.png", "textures/item/water_bucket.png", "textures/item/apple.png", "textures/item/bread.png",
    "textures/item/cooked_beef.png", "textures/item/golden_apple.png",
    "textures/misc/underwater.png", "lang/en_us.json",
    "sounds/step/grass1.ogg", "sounds/dig/stone1.ogg", "sounds/dig/gravel1.ogg", "sounds/liquid/splash.ogg", "sounds/liquid/swim1.ogg",
    "sounds/ambient/underwater/underwater_ambience.ogg", "sounds/entity/player/attack/sweep1.ogg", "sounds/entity/player/attack/strong1.ogg",
    "sounds/entity/player/attack/crit1.ogg", "sounds/damage/hit1.ogg", "sounds/random/eat1.ogg", "sounds/random/click.ogg", "sounds/random/levelup.ogg",
    "textures/block/oak_door_top.png", "textures/block/oak_door_bottom.png", "textures/block/iron_door_top.png", "textures/block/iron_door_bottom.png",
    "textures/block/oak_trapdoor.png", "textures/block/iron_trapdoor.png", "textures/block/budding_amethyst.png", "textures/block/amethyst_cluster.png",
    "textures/block/dirt_path_top.png", "textures/block/dirt_path_side.png", "textures/block/cherry_log.png", "textures/block/cherry_log_top.png",
    "textures/block/cherry_leaves.png", "textures/block/spruce_log.png", "textures/block/spruce_log_top.png", "textures/block/spruce_leaves.png",
    "textures/block/red_sand.png", "textures/block/terracotta.png", "textures/block/packed_ice.png",
    "textures/item/potion.png", "textures/item/potion_overlay.png", "textures/item/spawn_egg.png", "textures/item/spawn_egg_overlay.png",
    "textures/item/oak_door.png", "textures/item/iron_door.png", "textures/item/amethyst_shard.png", "textures/item/glass_bottle.png", "textures/item/experience_bottle.png",
    "sounds/block/wooden_door/open1.ogg", "sounds/block/wooden_door/close1.ogg", "sounds/block/iron_door/open1.ogg", "sounds/block/iron_door/close1.ogg",
    "sounds/block/wooden_trapdoor/open1.ogg", "sounds/block/wooden_trapdoor/close1.ogg", "sounds/block/iron_trapdoor/open1.ogg", "sounds/block/iron_trapdoor/close1.ogg",
    "sounds/block/amethyst/break1.ogg", "sounds/block/amethyst/step1.ogg", "sounds/block/amethyst/shimmer.ogg", "sounds/block/amethyst_cluster/break1.ogg",
    "sounds/block/deepslate/break1.ogg", "sounds/item/hoe/till1.ogg", "sounds/item/shovel/flatten1.ogg"
)

Write-Host "[OverHorizon] Syncing 23w13a__or__b assets into $OutDir ..."
foreach ($rel in $Assets) {
    $dest = Join-Path $OutDir "assets/minecraft/$rel"
    $dir = Split-Path -Parent $dest
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    try {
        Invoke-WebRequest -Uri "$BaseUrl/$rel" -OutFile $dest -UseBasicParsing
    } catch { }
}
Write-Host "[OverHorizon] Done!"
