namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Active client screen state rendered by <c>SilkOpenGlUiRenderer</c>.
/// </summary>
public enum UiOverlayMode : byte
{
    /// <summary>Active first-person voxel gameplay HUD (crosshair, hotbar, survival hearts/hunger/oxygen, chat).</summary>
    InGame = 0,

    /// <summary>Main Title Screen ("OVER THE HORIZON — C# EDITION") over rotating 3D panorama.</summary>
    TitleMenu = 1,

    /// <summary>Multiplayer server connection &amp; local server host screen.</summary>
    MultiplayerMenu = 2,

    /// <summary>Graphics, FOV, view distance, game mode, and audio settings screen.</summary>
    OptionsMenu = 3,

    /// <summary>In-game Esc pause screen.</summary>
    PauseMenu = 4,

    /// <summary>Redesigned tabbed &amp; searchable Creative Catalog (<c>E</c> key in Creative mode).</summary>
    BlockInventory = 5,

    /// <summary>In-game live chat &amp; slash-command input bar (<c>T</c> or <c>/</c> key).</summary>
    ChatInput = 6,

    /// <summary>36-slot Survival Inventory &amp; Quick Crafting screen (<c>E</c> key in Survival mode).</summary>
    SurvivalInventory = 7,

    /// <summary>Survival / PvP death &amp; respawn screen.</summary>
    DeathScreen = 8,
}
