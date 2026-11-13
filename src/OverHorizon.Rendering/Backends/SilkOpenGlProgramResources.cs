using System.Runtime.CompilerServices;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Manages OpenGL 3.3 shader compilation, uniform locations, texture atlas upload,
/// and per-pass VAO/VBO/EBO creation for <see cref="SilkOpenGlRenderSubsystem"/>.
/// </summary>
internal sealed class SilkOpenGlProgramResources : IDisposable
{
    private readonly GL _gl;
    private uint _atlasTexture;
    private bool _disposed;

    public SilkOpenGlProgramResources(GL gl)
    {
        _gl = gl;
        ProgramHandle = CreateChunkShaderProgram(gl);
        LocViewProjection = gl.GetUniformLocation(ProgramHandle, "uViewProjection");
        LocSectionOffset = gl.GetUniformLocation(ProgramHandle, "uSectionOffset");
        LocAtlas = gl.GetUniformLocation(ProgramHandle, "uAtlas");
        LocSkyBrightness = gl.GetUniformLocation(ProgramHandle, "uSkyBrightness");
        LocFogStart = gl.GetUniformLocation(ProgramHandle, "uFogStart");
        LocFogEnd = gl.GetUniformLocation(ProgramHandle, "uFogEnd");
        LocSkyColor = gl.GetUniformLocation(ProgramHandle, "uSkyColor");
        LocUnderwater = gl.GetUniformLocation(ProgramHandle, "uUnderwater");
    }

    public uint ProgramHandle { get; }

    public uint AtlasTexture => _atlasTexture;

    public int LocViewProjection { get; }

    public int LocSectionOffset { get; }

    public int LocAtlas { get; }

    public int LocSkyBrightness { get; }

    public int LocHeldTorchLight { get; }

    public int LocFogStart { get; }

    public int LocFogEnd { get; }

    public int LocSkyColor { get; }

    public int LocUnderwater { get; }

    public static unsafe (uint Vao, uint Vbo, uint Ebo, int IndexCount) CreatePassBuffers(
        GL gl,
        ReadOnlySpan<ChunkVertex> vertices,
        ReadOnlySpan<uint> indices)
    {
        if (vertices.IsEmpty || indices.IsEmpty)
        {
            return (0, 0, 0, 0);
        }

        var vao = gl.GenVertexArray();
        var vbo = gl.GenBuffer();
        var ebo = gl.GenBuffer();

        gl.BindVertexArray(vao);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        var stride = (uint)Unsafe.SizeOf<ChunkVertex>();
        fixed (ChunkVertex* vPtr = vertices)
        {
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * (int)stride), vPtr, BufferUsageARB.StaticDraw);
        }

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (uint* iPtr = indices)
        {
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), iPtr, BufferUsageARB.StaticDraw);
        }

        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(0);

        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(1);

        gl.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, stride, (void*)(5 * sizeof(float)));
        gl.EnableVertexAttribArray(2);

        gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
        gl.EnableVertexAttribArray(3);

        gl.VertexAttribPointer(4, 1, VertexAttribPointerType.Float, false, stride, (void*)(7 * sizeof(float)));
        gl.EnableVertexAttribArray(4);

        gl.BindVertexArray(0);
        return (vao, vbo, ebo, indices.Length);
    }

    public unsafe void UploadAtlas(TextureAtlasData atlas)
    {
        if (_atlasTexture == 0)
        {
            _atlasTexture = _gl.GenTexture();
        }

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _atlasTexture);

        var pixels = atlas.RgbaPixels.Span;
        fixed (byte* ptr = pixels)
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba8,
                (uint)atlas.Width,
                (uint)atlas.Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ptr);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_atlasTexture != 0)
        {
            _gl.DeleteTexture(_atlasTexture);
            _atlasTexture = 0;
        }

        if (ProgramHandle != 0)
        {
            _gl.DeleteProgram(ProgramHandle);
        }

        _disposed = true;
    }

    private static uint CreateChunkShaderProgram(GL gl)
    {
        var vert = CompileShaderStage(gl, ShaderType.VertexShader, SilkOpenGlShaderSources.ChunkVertexShader);
        var frag = CompileShaderStage(gl, ShaderType.FragmentShader, SilkOpenGlShaderSources.ChunkFragmentShader);

        var program = gl.CreateProgram();
        gl.AttachShader(program, vert);
        gl.AttachShader(program, frag);
        gl.LinkProgram(program);

        gl.GetProgram(program, GLEnum.LinkStatus, out var status);
        gl.DetachShader(program, vert);
        gl.DetachShader(program, frag);
        gl.DeleteShader(vert);
        gl.DeleteShader(frag);

        if (status == 0)
        {
            var info = gl.GetProgramInfoLog(program);
            gl.DeleteProgram(program);
            throw new InvalidOperationException($"Failed to link OpenGL chunk shader program: {info}");
        }

        return program;
    }

    private static uint CompileShaderStage(GL gl, ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        if (status == 0)
        {
            var info = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"Failed to compile OpenGL {type}: {info}");
        }

        return shader;
    }
}
