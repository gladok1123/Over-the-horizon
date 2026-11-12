using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering;

/// <summary>
/// Client-side voxel world rendering subsystem contract supporting both Silk.NET OpenGL 3.3 and headless simulation backends.
/// </summary>
public interface IRenderSubsystem : IDisposable
{
    /// <summary>Gets the active rendering backend identifier.</summary>
    string BackendName { get; }

    /// <summary>Gets a value indicating whether an OS graphics context is currently initialized.</summary>
    bool IsContextInitialized { get; }

    /// <summary>Gets the number of non-empty chunk sections currently resident in the renderer.</summary>
    int UploadedSectionCount { get; }

    /// <summary>Gets the telemetry metrics recorded during the most recent <see cref="RenderFrame"/> call.</summary>
    RenderFrameMetrics LastFrameMetrics { get; }

    /// <summary>
    /// Uploads or replaces the active block texture atlas.
    /// </summary>
    /// <param name="atlas">Procedural or resource-loaded RGBA8 block atlas.</param>
    void UploadAtlas(TextureAtlasData atlas);

    /// <summary>
    /// Uploads or replaces a meshed 16x16x16 chunk section. Empty meshes evict any existing resident buffer for that section.
    /// </summary>
    /// <param name="mesh">Constructed section mesh data.</param>
    void UploadOrReplaceSectionMesh(SectionMeshData mesh);

    /// <summary>
    /// Removes a single section mesh if present.
    /// </summary>
    /// <param name="sectionPosition">Target section coordinate.</param>
    void RemoveSectionMesh(SectionPos sectionPosition);

    /// <summary>
    /// Evicts all vertical section meshes belonging to <paramref name="chunkPosition"/>.
    /// </summary>
    /// <param name="chunkPosition">Unloaded chunk column coordinate.</param>
    void RemoveChunkMeshes(ChunkPos chunkPosition);

    /// <summary>
    /// Performs frustum culling and renders all visible opaque and translucent chunk sections from <paramref name="camera"/>.
    /// </summary>
    /// <param name="camera">Active first-person camera.</param>
    /// <param name="skyBrightness">Global sky light multiplier in <c>[0.05, 1.0]</c>.</param>
    /// <returns>Frame visibility and draw-call metrics.</returns>
    RenderFrameMetrics RenderFrame(FirstPersonCamera camera, float skyBrightness = 1.0f);
}
