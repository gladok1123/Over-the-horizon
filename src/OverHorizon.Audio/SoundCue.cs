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
}
