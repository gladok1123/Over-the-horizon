namespace OverHorizon.Rendering.Hud;

/// <summary>
/// Render-thread snapshot of equipped armor material tier codes (<c>0 = None, 1 = Leather, 2 = Iron, 3 = Gold, 4 = Diamond</c>)
/// for 3D armor layer rendering on Steve in the Inventory preview and Remote Multiplayer Avatars.
/// </summary>
public readonly record struct PlayerArmorVisual(
    byte HelmetTier = 0,
    byte ChestplateTier = 0,
    byte LeggingsTier = 0,
    byte BootsTier = 0)
{
    /// <summary>Returns <see langword="true"/> when at least one armor piece is equipped.</summary>
    public bool HasAny => (HelmetTier | ChestplateTier | LeggingsTier | BootsTier) != 0;
}
