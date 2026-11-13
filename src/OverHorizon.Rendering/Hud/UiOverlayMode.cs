namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Active client screen state rendered by <c>SilkOpenGlUiRenderer</c>.
/// </summary>
public enum UiOverlayMode : byte
{
    /// <summary>Active first-person voxel gameplay HUD (crosshair, hotbar, offhand, survival hearts/armor/hunger/oxygen, chat).</summary>
    InGame = 0,

    /// <summary>Main Title Screen ("OVER THE HORIZON — C# EDITION") over live 3D voxel panorama.</summary>
    TitleMenu = 1,

    /// <summary>Multiplayer server connection &amp; local server host screen.</summary>
    MultiplayerMenu = 2,

    /// <summary>Graphics, FOV, Render Distance slider (up to 64 chunks), game mode, and audio settings screen.</summary>
    OptionsMenu = 3,

    /// <summary>In-game Esc pause screen.</summary>
    PauseMenu = 4,

    /// <summary>Authentic tabbed &amp; searchable <c>195x136</c> Creative Inventory (<c>E</c> key in Creative mode).</summary>
    BlockInventory = 5,

    /// <summary>In-game live chat &amp; slash-command input bar (<c>T</c> or <c>/</c> key).</summary>
    ChatInput = 6,

    /// <summary>Authentic Survival Inventory with Armor slots, Offhand, 3D Steve, and <c>2x2</c> Crafting Grid (<c>E</c> key in Survival mode).</summary>
    SurvivalInventory = 7,

    /// <summary>Survival / PvP death &amp; respawn screen.</summary>
    DeathScreen = 8,

    /// <summary>Authentic <c>3x3</c> Crafting Table container screen (opened by right-clicking a Crafting Table).</summary>
    CraftingTableScreen = 9,

    /// <summary>Authentic 3-slot Furnace smelting container screen (opened by right-clicking a Furnace).</summary>
    FurnaceScreen = 10,

    /// <summary>Singleplayer Select World screen listing saved worlds in <c>./saves/</c> with Play, Create New World, and Delete.</summary>
    WorldSelectMenu = 11,

    /// <summary>Singleplayer Create New World screen (editable World Name, Survival/Creative Game Mode toggle, Seed).</summary>
    CreateWorldMenu = 12,
}
