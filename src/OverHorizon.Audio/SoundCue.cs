namespace OverHorizon.Audio;

/// <summary>
/// Built-in 3D positional and UI sound effect cues mapped to <c>23w13a__or__b</c> <c>.ogg</c> assets with clean-room fallbacks.
/// </summary>
public enum SoundCue : byte
{
    /// <summary>Footstep on stone, cobblestone, ore, or brick.</summary>
    FootstepStone = 0,

    /// <summary>Footstep on grass, dirt, or leaves.</summary>
    FootstepGrass = 1,

    /// <summary>Footstep on wood planks, logs, or crafting tables.</summary>
    FootstepWood = 2,

    /// <summary>Breaking a solid block.</summary>
    BlockBreak = 3,

    /// <summary>Placing a block.</summary>
    BlockPlace = 4,

    /// <summary>UI button click, lever toggle, or hotbar change.</summary>
    UiClick = 5,

    /// <summary>Entering water or splashing in fluid.</summary>
    WaterSplash = 6,

    /// <summary>Submerging camera underwater (<c>ambient/underwater/enter1.ogg</c>).</summary>
    UnderwaterEnter = 7,

    /// <summary>Player taking damage.</summary>
    PlayerHurt = 8,

    /// <summary>Landing a melee strike.</summary>
    PlayerHit = 9,

    /// <summary>Swinging a weapon or hand in air.</summary>
    SwordSwing = 10,

    /// <summary>Eating food.</summary>
    EatFood = 11,

    /// <summary>Picking up an item or crafting output.</summary>
    ItemPop = 12,

    /// <summary>Footstep on sand or sandstone.</summary>
    FootstepSand = 13,

    /// <summary>Footstep on gravel.</summary>
    FootstepGravel = 14,

    /// <summary>Footstep on wool or bed.</summary>
    FootstepCloth = 15,

    /// <summary>Climbing a ladder (<c>step/ladder1.ogg</c>).</summary>
    FootstepLadder = 16,

    /// <summary>Shattering glass or glowstone (<c>random/glass1.ogg</c>).</summary>
    GlassBreak = 17,

    /// <summary>Swimming stroke in water (<c>liquid/swim1.ogg</c>).</summary>
    WaterSwim = 18,

    /// <summary>Surfacing head out of water (<c>ambient/underwater/exit1.ogg</c>).</summary>
    UnderwaterExit = 19,

    /// <summary>Subtle underwater ambient loop pulse (<c>ambient/underwater/underwater_ambience.ogg</c>).</summary>
    UnderwaterAmbience = 20,

    /// <summary>Finishing a meal (<c>random/burp.ogg</c>).</summary>
    Burp = 21,

    /// <summary>Striking Flint and Steel (<c>fire/ignite.ogg</c>).</summary>
    FireIgnite = 22,

    /// <summary>Active fire crackle (<c>fire/fire.ogg</c>).</summary>
    FireCrackle = 23,

    /// <summary>Extinguishing fire or burning mob in water (<c>random/fizz.ogg</c>).</summary>
    Fizz = 24,

    /// <summary>Lit TNT or Creeper fuse hiss (<c>random/fuse.ogg</c>).</summary>
    TntFuse = 25,

    /// <summary>TNT or Creeper blast (<c>random/explode1.ogg</c>).</summary>
    Explosion = 26,

    /// <summary>Piston extending (<c>tile/piston/out.ogg</c>).</summary>
    PistonExtend = 27,

    /// <summary>Piston retracting (<c>tile/piston/in.ogg</c>).</summary>
    PistonRetract = 28,

    /// <summary>Pig oink (<c>mob/pig/say1.ogg</c>).</summary>
    MobPigSay = 29,

    /// <summary>Pig death squeal (<c>mob/pig/death.ogg</c>).</summary>
    MobPigDeath = 30,

    /// <summary>Cow moo (<c>mob/cow/say1.ogg</c>).</summary>
    MobCowSay = 31,

    /// <summary>Cow hurt/death (<c>mob/cow/hurt1.ogg</c>).</summary>
    MobCowHurt = 32,

    /// <summary>Sheep baa (<c>mob/sheep/say1.ogg</c>).</summary>
    MobSheepSay = 33,

    /// <summary>Shearing a sheep (<c>mob/sheep/shear.ogg</c>).</summary>
    MobSheepShear = 34,

    /// <summary>Chicken cluck (<c>mob/chicken/say1.ogg</c>).</summary>
    MobChickenSay = 35,

    /// <summary>Chicken hurt/death (<c>mob/chicken/hurt1.ogg</c>).</summary>
    MobChickenHurt = 36,

    /// <summary>Zombie groan (<c>mob/zombie/say1.ogg</c>).</summary>
    MobZombieSay = 37,

    /// <summary>Zombie hurt (<c>mob/zombie/hurt1.ogg</c>).</summary>
    MobZombieHurt = 38,

    /// <summary>Zombie death (<c>mob/zombie/death.ogg</c>).</summary>
    MobZombieDeath = 39,

    /// <summary>Skeleton rattle (<c>mob/skeleton/say1.ogg</c>).</summary>
    MobSkeletonSay = 40,

    /// <summary>Skeleton hurt (<c>mob/skeleton/hurt1.ogg</c>).</summary>
    MobSkeletonHurt = 41,

    /// <summary>Skeleton death (<c>mob/skeleton/death.ogg</c>).</summary>
    MobSkeletonDeath = 42,

    /// <summary>Spider hiss (<c>mob/spider/say1.ogg</c>).</summary>
    MobSpiderSay = 43,

    /// <summary>Spider death (<c>mob/spider/death.ogg</c>).</summary>
    MobSpiderDeath = 44,

    /// <summary>Creeper hiss/hurt (<c>mob/creeper/say1.ogg</c>).</summary>
    MobCreeperHiss = 45,

    /// <summary>Creeper death (<c>mob/creeper/death.ogg</c>).</summary>
    MobCreeperDeath = 46,

    /// <summary>Short fall landing (<c>damage/fallsmall.ogg</c>).</summary>
    FallSmall = 47,

    /// <summary>Hard fall landing (<c>damage/fallbig.ogg</c>).</summary>
    FallBig = 48,

    /// <summary>Fire burn damage tick (<c>random/fizz.ogg</c>).</summary>
    FireHurt = 49,

    /// <summary>Snow footstep (<c>step/snow1.ogg</c>).</summary>
    FootstepSnow = 50,

    /// <summary>Metallic/lantern/rail footstep (<c>block/lantern/break1.ogg</c>).</summary>
    FootstepMetal = 51,

    /// <summary>Bow string release (<c>random/bow.ogg</c>).</summary>
    BowShoot = 52,

    /// <summary>Arrow impacting block or target (<c>random/bowhit1.ogg</c>).</summary>
    ArrowHit = 53,

    /// <summary>Minecart rolling on rails (<c>minecart/base.ogg</c>).</summary>
    MinecartRoll = 54,

    /// <summary>Player jump takeoff (<c>step/cloth4.ogg</c> / takeoff thump).</summary>
    PlayerJump = 55,

    /// <summary>Soft landing after jumping or short drop (<c>step/stone4.ogg</c>).</summary>
    LandSoft = 56,

    /// <summary>Shield blocking melee, arrow, or blast (<c>item/shield/block1.ogg</c>).</summary>
    ShieldBlock = 57,

    /// <summary>Breaking wooden planks, logs, crafting tables, or chests (<c>dig/wood1.ogg</c>).</summary>
    WoodBreak = 58,

    /// <summary>Placing wooden blocks (<c>dig/wood2.ogg</c>).</summary>
    WoodPlace = 59,

    /// <summary>Breaking grass, dirt, leaves, or farmland (<c>dig/grass1.ogg</c>).</summary>
    GrassBreak = 60,

    /// <summary>Placing grass, dirt, or leaves (<c>dig/grass2.ogg</c>).</summary>
    GrassPlace = 61,

    /// <summary>Breaking sand or gravel (<c>dig/sand1.ogg</c>).</summary>
    SandBreak = 62,

    /// <summary>Placing sand or gravel (<c>dig/sand2.ogg</c>).</summary>
    SandPlace = 63,

    /// <summary>Breaking metallic blocks, rails, spawners, or ores (<c>block/lantern/break1.ogg</c>).</summary>
    MetalBreak = 64,

    /// <summary>Placing metallic blocks, rails, or lanterns (<c>block/lantern/place1.ogg</c>).</summary>
    MetalPlace = 65,

    /// <summary>Placing or breaking wool or bed (<c>dig/cloth1.ogg</c>).</summary>
    ClothPlace = 66,

    /// <summary>Placing stone, brick, cobblestone, or deepslate (<c>dig/stone2.ogg</c>).</summary>
    StonePlace = 67,

    /// <summary>Drinking a potion (<c>random/drink.ogg</c>).</summary>
    PotionDrink = 68,

    /// <summary>Composter filling or popping Bone Meal (<c>block/composter/ready1.ogg</c>).</summary>
    ComposterReady = 69,

    /// <summary>Breaking gravel (<c>dig/gravel1.ogg</c>).</summary>
    GravelBreak = 70,

    /// <summary>Placing gravel (<c>dig/gravel2.ogg</c>).</summary>
    GravelPlace = 71,

    /// <summary>Breaking snow (<c>dig/snow1.ogg</c>).</summary>
    SnowBreak = 72,

    /// <summary>Placing snow (<c>dig/snow2.ogg</c>).</summary>
    SnowPlace = 73,

    /// <summary>Breaking wool or bed (<c>dig/cloth2.ogg</c>).</summary>
    ClothBreak = 74,

    /// <summary>Breaking deepslate or deepslate ores (<c>block/deepslate/break1.ogg</c>).</summary>
    DeepslateBreak = 75,

    /// <summary>Placing deepslate (<c>block/deepslate/place1.ogg</c>).</summary>
    DeepslatePlace = 76,

    /// <summary>Footstep on deepslate (<c>block/deepslate/step1.ogg</c>).</summary>
    FootstepDeepslate = 77,

    /// <summary>Breaking amethyst block (<c>block/amethyst/break1.ogg</c>).</summary>
    AmethystBreak = 78,

    /// <summary>Placing amethyst block (<c>block/amethyst/place1.ogg</c>).</summary>
    AmethystPlace = 79,

    /// <summary>Footstep on amethyst (<c>block/amethyst/step1.ogg</c>).</summary>
    FootstepAmethyst = 80,

    /// <summary>Crystalline amethyst shimmer (<c>block/amethyst/shimmer.ogg</c>).</summary>
    AmethystChime = 81,

    /// <summary>Breaking amethyst cluster (<c>block/amethyst_cluster/break1.ogg</c>).</summary>
    AmethystClusterBreak = 82,

    /// <summary>Placing amethyst cluster (<c>block/amethyst_cluster/place1.ogg</c>).</summary>
    AmethystClusterPlace = 83,

    /// <summary>Opening wooden door (<c>block/wooden_door/open1.ogg</c>).</summary>
    WoodenDoorOpen = 84,

    /// <summary>Closing wooden door (<c>block/wooden_door/close1.ogg</c>).</summary>
    WoodenDoorClose = 85,

    /// <summary>Opening iron door (<c>block/iron_door/open1.ogg</c>).</summary>
    IronDoorOpen = 86,

    /// <summary>Closing iron door (<c>block/iron_door/close1.ogg</c>).</summary>
    IronDoorClose = 87,

    /// <summary>Opening wooden trapdoor (<c>block/wooden_trapdoor/open1.ogg</c>).</summary>
    WoodenTrapdoorOpen = 88,

    /// <summary>Closing wooden trapdoor (<c>block/wooden_trapdoor/close1.ogg</c>).</summary>
    WoodenTrapdoorClose = 89,

    /// <summary>Opening iron trapdoor (<c>block/iron_trapdoor/open1.ogg</c>).</summary>
    IronTrapdoorOpen = 90,

    /// <summary>Closing iron trapdoor (<c>block/iron_trapdoor/close1.ogg</c>).</summary>
    IronTrapdoorClose = 91,

    /// <summary>Tilling dirt or grass into farmland with a Hoe (<c>item/hoe/till1.ogg</c>).</summary>
    HoeTill = 92,

    /// <summary>Flattening grass or dirt into a dirt path with a Shovel (<c>item/shovel/flatten1.ogg</c>).</summary>
    ShovelFlatten = 93,
}
