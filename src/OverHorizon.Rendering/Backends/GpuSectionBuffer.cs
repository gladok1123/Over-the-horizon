using OverHorizon.Core.Math;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Holds OpenGL VAO/VBO/EBO handles and index counts for one uploaded 16x16x16 chunk section.
/// </summary>
internal sealed class GpuSectionBuffer : IDisposable
{
    private readonly GL _gl;
    private bool _disposed;

    public GpuSectionBuffer(
        GL gl,
        SectionPos sectionPosition,
        BoundingBox worldBounds,
        (uint Vao, uint Vbo, uint Ebo, int IndexCount) opaquePass,
        (uint Vao, uint Vbo, uint Ebo, int IndexCount) translucentPass)
    {
        _gl = gl;
        SectionPosition = sectionPosition;
        WorldBounds = worldBounds;
        OpaqueVao = opaquePass.Vao;
        OpaqueVbo = opaquePass.Vbo;
        OpaqueEbo = opaquePass.Ebo;
        OpaqueIndexCount = opaquePass.IndexCount;
        TranslucentVao = translucentPass.Vao;
        TranslucentVbo = translucentPass.Vbo;
        TranslucentEbo = translucentPass.Ebo;
        TranslucentIndexCount = translucentPass.IndexCount;
    }

    public SectionPos SectionPosition { get; }

    public BoundingBox WorldBounds { get; }

    public uint OpaqueVao { get; }

    public uint OpaqueVbo { get; }

    public uint OpaqueEbo { get; }

    public int OpaqueIndexCount { get; }

    public uint TranslucentVao { get; }

    public uint TranslucentVbo { get; }

    public uint TranslucentEbo { get; }

    public int TranslucentIndexCount { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DeletePassBuffers(OpaqueVao, OpaqueVbo, OpaqueEbo);
        DeletePassBuffers(TranslucentVao, TranslucentVbo, TranslucentEbo);
        _disposed = true;
    }

    private void DeletePassBuffers(uint vao, uint vbo, uint ebo)
    {
        if (vao != 0)
        {
            _gl.DeleteVertexArray(vao);
        }

        if (vbo != 0)
        {
            _gl.DeleteBuffer(vbo);
        }

        if (ebo != 0)
        {
            _gl.DeleteBuffer(ebo);
        }
    }
}
