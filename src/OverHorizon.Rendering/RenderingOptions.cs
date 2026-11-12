namespace OverHorizon.Rendering;

/// <summary>
/// Configuration parameters for the client rendering subsystem.
/// </summary>
/// <param name="Headless">When <see langword="true"/>, registers the zero-GPU <c>HeadlessRenderSubsystem</c> for CI and headless simulations.</param>
/// <param name="WindowWidth">Initial window width in pixels.</param>
/// <param name="WindowHeight">Initial window height in pixels.</param>
/// <param name="WindowTitle">Initial OS window title.</param>
/// <param name="VSync">Whether vertical sync is enabled when running windowed.</param>
public sealed record RenderingOptions(
    bool Headless = false,
    int WindowWidth = 1280,
    int WindowHeight = 720,
    string WindowTitle = "Over-the-Horizon",
    bool VSync = true);
