using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Zero-GPU rendering backend that tracks uploaded section meshes and executes exact CPU frustum culling
/// and draw-call accounting for automated tests, benchmarks, and headless client verification.
/// </summary>
public sealed class HeadlessRenderSubsystem : IRenderSubsystem
{
    private readonly Dictionary<SectionPos, SectionMeshData> _sections = [];
    private TextureAtlasData? _atlas;
    private long _frameCount;
    private bool _disposed;

    /// <inheritdoc />
    public string BackendName => "Headless";

    /// <inheritdoc />
    public bool IsContextInitialized => !_disposed && _atlas is not null;

    /// <inheritdoc />
    public int UploadedSectionCount => _sections.Count;

    /// <inheritdoc />
    public RenderFrameMetrics LastFrameMetrics { get; private set; }

    /// <inheritdoc />
    public void UploadAtlas(TextureAtlasData atlas)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(atlas);
        _atlas = atlas;
    }

    /// <inheritdoc />
    public void UploadOrReplaceSectionMesh(SectionMeshData mesh)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.IsEmpty)
        {
            _sections.Remove(mesh.SectionPosition);
            return;
        }

        _sections[mesh.SectionPosition] = mesh;
    }

    /// <inheritdoc />
    public void RemoveSectionMesh(SectionPos sectionPosition) =>
        _sections.Remove(sectionPosition);

    /// <inheritdoc />
    public void RemoveChunkMeshes(ChunkPos chunkPosition)
    {
        if (_sections.Count == 0)
        {
            return;
        }

        var toRemove = new List<SectionPos>();
        foreach (var key in _sections.Keys)
        {
            if (key.X == chunkPosition.X && key.Z == chunkPosition.Z)
            {
                toRemove.Add(key);
            }
        }

        foreach (var pos in toRemove)
        {
            _sections.Remove(pos);
        }
    }

    /// <inheritdoc />
    public RenderFrameMetrics RenderFrame(FirstPersonCamera camera, float skyBrightness = 1.0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);

        var frustum = Frustum.FromCamera(camera);
        var visible = 0;
        var culled = 0;
        var drawCalls = 0;
        var triangles = 0;

        foreach (var mesh in _sections.Values)
        {
            if (!frustum.Intersects(mesh.WorldBounds))
            {
                culled++;
                continue;
            }

            visible++;
            if (!mesh.OpaqueIndices.IsEmpty)
            {
                drawCalls++;
                triangles += mesh.OpaqueIndices.Length / 3;
            }

            if (!mesh.TranslucentIndices.IsEmpty)
            {
                drawCalls++;
                triangles += mesh.TranslucentIndices.Length / 3;
            }
        }

        _frameCount++;
        LastFrameMetrics = new RenderFrameMetrics(visible, culled, drawCalls, triangles, _frameCount);
        return LastFrameMetrics;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _sections.Clear();
        _atlas = null;
        _disposed = true;
    }
}
