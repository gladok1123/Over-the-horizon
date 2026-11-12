namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Active client screen state rendered by <c>SilkOpenGlUiRenderer</c>.
/// </summary>
public enum UiOverlayMode : byte
{
    /// <summary>Active first-person voxel gameplay HUD (crosshair, hotbar, chat feed, optional F3).</summary>
    InGame = 0,

    /// <summary>Main Title Screen ("OVER THE HORIZON — C# EDITION") over rotating 3D panorama.</summary>
    TitleMenu = 1,

    /// <summary>Multiplayer server connection &amp; local server host screen.</summary>
    MultiplayerMenu = 2,

    /// <summary>Graphics, FOV, view distance, and audio settings screen.</summary>
    OptionsMenu = 3,

    /// <summary>In-game Esc pause screen.</summary>
    PauseMenu = 4,

    /// <summary>Creative 24-block selection palette (<c>E</c> key).</summary>
    BlockInventory = 5,

    /// <summary>In-game live chat &amp; slash-command input bar (<c>T</c> or <c>/</c> key).</summary>
    ChatInput = 6,
}
