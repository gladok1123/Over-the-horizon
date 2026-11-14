using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Batched OpenGL 3.3 Core pixel-space 2D UI renderer binding:
/// Unit 0 = bitmap font &amp; solid quads, Unit 1 = <c>256x256</c> block/item sprites,
/// Unit 2 = <c>512x768</c> real <c>23w13a__or__b</c> GUI sheets (<c>widgets.png</c>, <c>icons.png</c>, <c>slider.png</c>, <c>container/*.png</c>).
/// </summary>
internal sealed class SilkOpenGlUiRenderer : IDisposable
{
    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPos;
        layout (location = 1) in vec2 aUv;
        layout (location = 2) in vec4 aColor;

        uniform vec2 uViewport;

        out vec2 vUv;
        out vec4 vColor;

        void main()
        {
            vUv = aUv;
            vColor = aColor;
            vec2 ndc = vec2((aPos.x / uViewport.x) * 2.0 - 1.0, 1.0 - (aPos.y / uViewport.y) * 2.0);
            gl_Position = vec4(ndc, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 vUv;
        in vec4 vColor;

        uniform sampler2D uFontAtlas;
        uniform sampler2D uItemAtlas;
        uniform sampler2D uGuiAtlas;

        out vec4 FragColor;

        void main()
        {
            vec4 sampleCol;
            if (vUv.x <= -2.5)
            {
                vec2 guiUv = -vUv - vec2(3.0);
                sampleCol = texture(uGuiAtlas, guiUv);
            }
            else if (vUv.x <= -0.5)
            {
                vec2 itemUv = -vUv - vec2(1.0);
                sampleCol = texture(uItemAtlas, itemUv);
            }
            else
            {
                sampleCol = texture(uFontAtlas, vUv);
            }

            if (sampleCol.a < 0.08)
            {
                discard;
            }

            FragColor = sampleCol * vColor;
        }
        """;

    private readonly GL _gl;
    private readonly uint _program;
    private readonly int _locViewport;
    private readonly int _locFontAtlas;
    private readonly int _locItemAtlas;
    private readonly int _locGuiAtlas;
    private readonly uint _fontTexture;
    private readonly uint _guiTexture;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly List<TexturedUiVertex> _vertices = new(12288);
    private bool _disposed;

    public unsafe SilkOpenGlUiRenderer(GL gl, string? externalAssetsPath = null)
    {
        _gl = gl;
        _program = CompileAndLink(gl);
        _locViewport = gl.GetUniformLocation(_program, "uViewport");
        _locFontAtlas = gl.GetUniformLocation(_program, "uFontAtlas");
        _locItemAtlas = gl.GetUniformLocation(_program, "uItemAtlas");
        _locGuiAtlas = gl.GetUniformLocation(_program, "uGuiAtlas");
        _fontTexture = UploadRgbaTexture(gl, PixelFontGlyphs.CreateRgbaAtlas(), PixelFontGlyphs.AtlasWidth, PixelFontGlyphs.AtlasHeight);
        _guiTexture = UploadRgbaTexture(gl, GuiTextureAtlasGenerator.Generate(externalAssetsPath), GuiTextureAtlasGenerator.AtlasWidth, GuiTextureAtlasGenerator.AtlasHeight);

        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();

        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var stride = (uint)Unsafe.SizeOf<TexturedUiVertex>();
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)(4 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.BindVertexArray(0);
    }

    public unsafe int RenderUi(FirstPersonCamera camera, FrameOverlayState state, uint itemAtlasTexture)
    {
        var width = state.ViewportSize.X > 64.0f ? state.ViewportSize.X : 1280.0f;
        var height = state.ViewportSize.Y > 64.0f ? state.ViewportSize.Y : 720.0f;

        UiGeometryBuilder.BuildFrameQuads(_vertices, camera, width, height, state);
        if (_vertices.Count == 0)
        {
            return 0;
        }

        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        _gl.UseProgram(_program);
        _gl.Uniform2(_locViewport, width, height);
        _gl.Uniform1(_locFontAtlas, 0);
        _gl.Uniform1(_locItemAtlas, 1);
        _gl.Uniform1(_locGuiAtlas, 2);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _fontTexture);

        _gl.ActiveTexture(TextureUnit.Texture1);
        _gl.BindTexture(TextureTarget.Texture2D, itemAtlasTexture != 0 ? itemAtlasTexture : _fontTexture);

        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, _guiTexture);

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var span = CollectionsMarshal.AsSpan(_vertices);
        fixed (TexturedUiVertex* ptr = span)
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(span.Length * Unsafe.SizeOf<TexturedUiVertex>()),
                ptr,
                BufferUsageARB.StreamDraw);
        }

        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)span.Length);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindVertexArray(0);
        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.CullFace);
        _gl.Enable(EnableCap.DepthTest);
        return 1;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_vao != 0)
        {
            _gl.DeleteVertexArray(_vao);
        }

        if (_vbo != 0)
        {
            _gl.DeleteBuffer(_vbo);
        }

        if (_guiTexture != 0)
        {
            _gl.DeleteTexture(_guiTexture);
        }

        if (_fontTexture != 0)
        {
            _gl.DeleteTexture(_fontTexture);
        }

        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
        }

        _disposed = true;
    }

    private static unsafe uint UploadRgbaTexture(GL gl, byte[] rgba, uint width, uint height)
    {
        var tex = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, tex);
        fixed (byte* ptr = rgba)
        {
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        }

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        return tex;
    }

    private static uint CompileAndLink(GL gl)
    {
        var vs = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(vs, VertexShaderSource);
        gl.CompileShader(vs);

        var fs = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(fs, FragmentShaderSource);
        gl.CompileShader(fs);

        var prog = gl.CreateProgram();
        gl.AttachShader(prog, vs);
        gl.AttachShader(prog, fs);
        gl.LinkProgram(prog);
        gl.DetachShader(prog, vs);
        gl.DetachShader(prog, fs);
        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        return prog;
    }
}
