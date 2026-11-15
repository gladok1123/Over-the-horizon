using System.Numerics;
using OverHorizon.Rendering.Textures;
using Silk.NET.Input;

namespace OverHorizon.Rendering;

/// <summary>
/// Renderer contract for backends that own an OS window: on top of <see cref="IRenderSubsystem"/> they expose
/// the input context, the framebuffer size, the window title and the interactive update/render loop.
/// </summary>
public interface IWindowRenderSubsystem : IRenderSubsystem
{
    /// <summary>Gets the active keyboard &amp; mouse input context when the window is open.</summary>
    IInputContext? InputContext { get; }

    /// <summary>Gets the current window framebuffer dimensions in pixels.</summary>
    Vector2 WindowPixelSize { get; }

    /// <summary>Gets or sets the OS window title.</summary>
    string WindowTitle { get; set; }

    /// <summary>Requests graceful window closure at the end of the current frame.</summary>
    void RequestClose();

    /// <summary>
    /// Uploads or replaces the <c>512x256</c> entity skin sheet (<c>steve.png</c> + 8 mobs).
    /// </summary>
    /// <param name="entityAtlas">Entity atlas pixels.</param>
    void UploadEntityAtlas(TextureAtlasData entityAtlas);

    /// <summary>
    /// Creates the OS window and runs the interactive update/render loop until the window closes.
    /// </summary>
    /// <param name="onUpdate">Per-frame simulation callback.</param>
    /// <param name="onRender">Per-frame render callback.</param>
    /// <param name="onResize">Optional framebuffer resize callback.</param>
    /// <param name="cancellationToken">Cancels the loop by closing the window.</param>
    void RunWindowLoop(
        Action<double> onUpdate,
        Action<double> onRender,
        Action<int, int>? onResize = null,
        CancellationToken cancellationToken = default);
}
