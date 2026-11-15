using System.Numerics;
using System.Runtime.CompilerServices;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using OverHorizon.Rendering.Threading;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Renders the pre-terrain Celestial Sky Dome (Sun, Moon, Stars), world-anchored drifting Clouds,
/// sub-voxel selection wireframes, <c>destroy_stage_0..9</c> crack overlays, articulated 3D mobs,
/// 3D Minecarts, 3D Arrows, Remote Players with 3D Armor, first-person viewmodels, and the 3D Inventory Steve preview.
/// </summary>
internal sealed partial class SilkOpenGlOverlayRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly uint _program;
    private readonly int _locTransform;
    private readonly int _locAtlas;
    private readonly int _locEntityAtlas;
    private readonly int _locFogStart;
    private readonly int _locFogEnd;
    private readonly int _locFogColor;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly OverlayGeometry _geometry;
    private readonly SilkOpenGlUiRenderer _uiRenderer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SilkOpenGlOverlayRenderer"/> class.
    /// </summary>
    /// <param name="gl">OpenGL context.</param>
    /// <param name="maxRenderThreads">
    /// Worker cap for the multi-threaded tessellation stages (<c>0</c> selects
    /// <see cref="RenderParallelism.AutoWorkerCount"/>).
    /// </param>
    public unsafe SilkOpenGlOverlayRenderer(GL gl, int maxRenderThreads = 0)
    {
        _gl = gl;
        _geometry = new OverlayGeometry(maxRenderThreads);
        _program = CreateOverlayShaderProgram(gl);
        _locTransform = gl.GetUniformLocation(_program, "uTransform");
        _locAtlas = gl.GetUniformLocation(_program, "uAtlas");
        _locEntityAtlas = gl.GetUniformLocation(_program, "uEntityAtlas");
        _locFogStart = gl.GetUniformLocation(_program, "uFogStart");
        _locFogEnd = gl.GetUniformLocation(_program, "uFogEnd");
        _locFogColor = gl.GetUniformLocation(_program, "uFogColor");
        _vao = gl.GenVertexArray();
        _vbo = gl.GenBuffer();

        gl.BindVertexArray(_vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var stride = (uint)Unsafe.SizeOf<OverlayVertex>();
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(7 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.BindVertexArray(0);

        _uiRenderer = new SilkOpenGlUiRenderer(gl);
    }

    /// <summary>Draws Sun, Moon, and Stars behind all terrain with DepthTest &amp; DepthMask disabled so FarPlane never clips them.</summary>
    public unsafe int RenderSkyDome(FirstPersonCamera camera, FrameOverlayState state)
    {
        if (!_geometry.BuildSkyDome(camera, state))
        {
            return 0;
        }

        _gl.UseProgram(_program);
        SetFogUniforms(100000.0f, 200000.0f, Vector3.One);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.Disable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var skyProj = Matrix4x4.CreatePerspectiveFieldOfView(
            camera.FieldOfViewDegrees * (MathF.PI / 180.0f),
            camera.AspectRatio,
            0.5f,
            1024.0f);
        var skyMvp = camera.ComputeRelativeViewMatrix() * skyProj;
        _gl.UniformMatrix4(_locTransform, 1, false, (float*)&skyMvp);
        UploadAndDraw(_geometry.Sky, PrimitiveType.Triangles);

        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(true);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        return 1;
    }

    /// <summary>Releases the overlay vertex array, buffer and shader program.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _uiRenderer.Dispose();
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_program != 0) _gl.DeleteProgram(_program);
        _disposed = true;
    }

    private void SetFogUniforms(float start, float end, Vector3 color)
    {
        _gl.Uniform1(_locFogStart, start);
        _gl.Uniform1(_locFogEnd, end);
        _gl.Uniform3(_locFogColor, color.X, color.Y, color.Z);
    }

    private unsafe int RenderInventoryPlayerPreview(FrameOverlayState state, uint blockAtlasTexture, uint entityAtlasTexture)
    {
        var screenW = state.ViewportSize.X > 64.0f ? state.ViewportSize.X : 1280.0f;
        var screenH = state.ViewportSize.Y > 64.0f ? state.ViewportSize.Y : 720.0f;
        var rect = state.InventoryPlayerPreviewRect;

        var vx = (int)MathF.Round(rect.X);
        var vy = (int)MathF.Round(screenH - (rect.Y + rect.W));
        var vw = Math.Max(8, (int)MathF.Round(rect.Z));
        var vh = Math.Max(8, (int)MathF.Round(rect.W));

        _geometry.BuildInventoryPreview(state);

        _gl.Viewport(vx, vy, (uint)vw, (uint)vh);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthMask(true);
        _gl.Clear(ClearBufferMask.DepthBufferBit);
        _gl.Disable(EnableCap.CullFace);
        _gl.UseProgram(_program);
        SetFogUniforms(100000.0f, 200000.0f, Vector3.One);
        _gl.Uniform1(_locAtlas, 0);
        _gl.Uniform1(_locEntityAtlas, 2);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, blockAtlasTexture);
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, entityAtlasTexture);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var aspect = (float)vw / vh;
        var view = Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.06f, 2.85f), new Vector3(0.0f, 0.02f, 0.0f), Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(42.0f * (MathF.PI / 180.0f), aspect, 0.2f, 10.0f);
        var mvp = view * proj;
        _gl.UniformMatrix4(_locTransform, 1, false, (float*)&mvp);
        UploadAndDraw(_geometry.InventoryPreview, PrimitiveType.Triangles);

        _gl.BindVertexArray(0);
        _gl.Enable(EnableCap.CullFace);
        _gl.Viewport(0, 0, (uint)MathF.Round(screenW), (uint)MathF.Round(screenH));
        return 1;
    }

    private unsafe void UploadAndDraw(List<OverlayVertex> vertices, PrimitiveType primitiveType)
    {
        var span = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(vertices);
        var byteCount = (nuint)(span.Length * Unsafe.SizeOf<OverlayVertex>());
        fixed (OverlayVertex* ptr = span)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, byteCount, ptr, BufferUsageARB.StreamDraw);
        }

        _gl.DrawArrays(primitiveType, 0, (uint)span.Length);
    }

    private static uint CreateOverlayShaderProgram(GL gl)
    {
        const string vertSrc = """
            #version 330 core
            layout (location = 0) in vec3 aPosition;
            layout (location = 1) in vec4 aColor;
            layout (location = 2) in vec2 aUv;

            uniform mat4 uTransform;

            out vec4 vColor;
            out vec2 vUv;
            out float vDist;

            void main()
            {
                vColor = aColor;
                vUv = aUv;
                vDist = length(aPosition);
                gl_Position = uTransform * vec4(aPosition, 1.0);
            }
            """;

        const string fragSrc = """
            #version 330 core
            in vec4 vColor;
            in vec2 vUv;
            in float vDist;

            uniform sampler2D uAtlas;
            uniform sampler2D uEntityAtlas;
            uniform float uFogStart;
            uniform float uFogEnd;
            uniform vec3 uFogColor;

            out vec4 FragColor;

            void main()
            {
                vec4 col;
                if (vUv.x <= -2.0)
                {
                    vec2 entityUv = vec2(-vUv.x - 2.0, vUv.y);
                    vec4 eSample = texture(uEntityAtlas, entityUv);
                    if (eSample.a < 0.1) discard;
                    col = vec4(eSample.rgb * vColor.rgb, eSample.a * vColor.a);
                }
                else if (vUv.x >= 0.0)
                {
                    vec4 tex = texture(uAtlas, vUv);
                    if (tex.a < 0.1) discard;
                    col = vec4(tex.rgb * vColor.rgb, tex.a * vColor.a);
                }
                else
                {
                    col = vColor;
                }

                float fog = clamp((vDist - uFogStart) / max(0.01, uFogEnd - uFogStart), 0.0, 1.0);
                FragColor = vec4(mix(col.rgb, uFogColor, fog), col.a);
            }
            """;

        var v = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(v, vertSrc);
        gl.CompileShader(v);
        var f = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(f, fragSrc);
        gl.CompileShader(f);
        var prog = gl.CreateProgram();
        gl.AttachShader(prog, v);
        gl.AttachShader(prog, f);
        gl.LinkProgram(prog);
        gl.DetachShader(prog, v);
        gl.DetachShader(prog, f);
        gl.DeleteShader(v);
        gl.DeleteShader(f);
        return prog;
    }
}
