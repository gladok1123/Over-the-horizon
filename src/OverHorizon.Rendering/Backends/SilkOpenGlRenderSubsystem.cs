using System.Numerics;
using Microsoft.Extensions.Logging;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Silk.NET OpenGL 3.3 Core Profile voxel chunk renderer with lazy window/context initialization,
/// per-section VAO/VBO/EBO residency, camera-relative anti-jitter uniforms, frustum culling,
/// and two-pass opaque + translucent drawing.
/// </summary>
public sealed class SilkOpenGlRenderSubsystem : IRenderSubsystem
{
    private static readonly Vector3 DaySkyColor = new(0.49f, 0.69f, 0.98f);

    private readonly RenderingOptions _options;
    private readonly ILogger<SilkOpenGlRenderSubsystem> _logger;
    private readonly Dictionary<SectionPos, SectionMeshData> _stagedMeshes = [];
    private readonly Dictionary<SectionPos, GpuSectionBuffer> _gpuSections = [];
    private readonly List<(GpuSectionBuffer Buffer, float DistanceSq)> _translucentSortScratch = [];

    private TextureAtlasData? _stagedAtlas;
    private IWindow? _window;
    private IInputContext? _inputContext;
    private GL? _gl;
    private SilkOpenGlProgramResources? _program;
    private SilkOpenGlOverlayRenderer? _overlayRenderer;
    private long _frameCount;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SilkOpenGlRenderSubsystem"/> class.
    /// Does not create an OS window until <see cref="RunWindowLoop"/> is invoked.
    /// </summary>
    /// <param name="options">Rendering window and VSync configuration.</param>
    /// <param name="logger">Structured logger.</param>
    public SilkOpenGlRenderSubsystem(RenderingOptions options, ILogger<SilkOpenGlRenderSubsystem> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public string BackendName => "Silk.NET OpenGL 3.3";

    /// <inheritdoc />
    public bool IsContextInitialized => _gl is not null && !_disposed;

    /// <inheritdoc />
    public int UploadedSectionCount => IsContextInitialized ? _gpuSections.Count : _stagedMeshes.Count;

    /// <inheritdoc />
    public RenderFrameMetrics LastFrameMetrics { get; private set; }

    /// <summary>Gets the active Silk.NET input context while the window loop is running, or <see langword="null"/> prior to load.</summary>
    public IInputContext? InputContext => _inputContext;

    /// <summary>Gets or sets the OS window title when a window is open.</summary>
    public string WindowTitle
    {
        get => _window?.Title ?? _options.WindowTitle;
        set
        {
            if (_window is not null)
            {
                _window.Title = value;
            }
        }
    }

    /// <inheritdoc />
    public void UploadAtlas(TextureAtlasData atlas)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(atlas);

        _stagedAtlas = atlas;
        if (_program is not null)
        {
            _program.UploadAtlas(atlas);
            RenderingLog.AtlasUploaded(_logger, atlas.Width, atlas.Height, atlas.TileSize);
        }
    }

    /// <inheritdoc />
    public void UploadOrReplaceSectionMesh(SectionMeshData mesh)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.IsEmpty)
        {
            RemoveSectionMesh(mesh.SectionPosition);
            return;
        }

        if (_gl is null)
        {
            _stagedMeshes[mesh.SectionPosition] = mesh;
            return;
        }

        _stagedMeshes.Remove(mesh.SectionPosition);
        UploadMeshToGpu(_gl, mesh);
    }

    /// <inheritdoc />
    public void RemoveSectionMesh(SectionPos sectionPosition)
    {
        _stagedMeshes.Remove(sectionPosition);
        if (_gpuSections.Remove(sectionPosition, out var existing))
        {
            existing.Dispose();
        }
    }

    /// <inheritdoc />
    public void RemoveChunkMeshes(ChunkPos chunkPosition)
    {
        RemoveMatchingColumn(_stagedMeshes, chunkPosition);

        if (_gpuSections.Count == 0)
        {
            return;
        }

        var keys = new List<SectionPos>();
        foreach (var key in _gpuSections.Keys)
        {
            if (key.X == chunkPosition.X && key.Z == chunkPosition.Z)
            {
                keys.Add(key);
            }
        }

        foreach (var key in keys)
        {
            if (_gpuSections.Remove(key, out var buffer))
            {
                buffer.Dispose();
            }
        }
    }

    /// <summary>
    /// Opens the Silk.NET OpenGL 3.3 window and drives update/render callbacks until closed or cancelled.
    /// </summary>
    /// <param name="onUpdate">Per-frame simulation/input callback receiving delta seconds.</param>
    /// <param name="onRender">Per-frame render callback receiving delta seconds.</param>
    /// <param name="onResize">Optional framebuffer resize callback receiving new pixel width/height.</param>
    /// <param name="cancellationToken">Cancellation token that requests window close when signalled.</param>
    public void RunWindowLoop(
        Action<double> onUpdate,
        Action<double> onRender,
        Action<int, int>? onResize = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(onUpdate);
        ArgumentNullException.ThrowIfNull(onRender);

        var winOpts = WindowOptions.Default;
        winOpts.Size = new Vector2D<int>(_options.WindowWidth, _options.WindowHeight);
        winOpts.Title = _options.WindowTitle;
        winOpts.VSync = _options.VSync;
        winOpts.API = GraphicsAPI.Default;

        using var window = Window.Create(winOpts);
        _window = window;

        using var ctr = cancellationToken.Register(() => window.Close());

        window.Load += () =>
        {
            _inputContext = window.CreateInput();
            _gl = GL.GetApi(window);
            InitializeGlResources(_gl, window.FramebufferSize);
        };

        window.FramebufferResize += newSize =>
        {
            _gl?.Viewport(newSize);
            onResize?.Invoke(newSize.X, newSize.Y);
        };

        window.Update += onUpdate;
        window.Render += onRender;
        window.Closing += ReleaseGpuResources;

        window.Run();
        _window = null;
    }

    /// <inheritdoc />
    public unsafe RenderFrameMetrics RenderFrame(FirstPersonCamera camera, float skyBrightness = 1.0f, FrameOverlayState? overlay = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);

        var frustum = Frustum.FromCamera(camera);

        if (_gl is null || _program is null)
        {
            return SimulateCpuVisibilityPass(in frustum);
        }

        var gl = _gl;
        var prog = _program;
        var clampedSky = Math.Clamp(skyBrightness, 0.08f, 1.0f);
        var skyColor = DaySkyColor * clampedSky;

        gl.ClearColor(skyColor.X, skyColor.Y, skyColor.Z, 1.0f);
        gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Lequal);
        gl.Enable(EnableCap.CullFace);
        gl.CullFace(TriangleFace.Back);
        gl.FrontFace(FrontFaceDirection.Ccw);

        gl.UseProgram(prog.ProgramHandle);

        var viewProj = camera.BuildCameraRelativeViewProjectionMatrix();
        gl.UniformMatrix4(prog.LocViewProjection, 1, false, (float*)&viewProj);
        gl.Uniform1(prog.LocAtlas, 0);
        gl.Uniform1(prog.LocSkyBrightness, clampedSky);
        gl.Uniform1(prog.LocFogStart, camera.FarPlane * 0.65f);
        gl.Uniform1(prog.LocFogEnd, camera.FarPlane * 0.98f);
        gl.Uniform3(prog.LocSkyColor, skyColor.X, skyColor.Y, skyColor.Z);

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, prog.AtlasTexture);

        var (visible, culled, drawCalls, triangles) = DrawOpaqueAndCollectTranslucent(gl, prog, in frustum, camera.Position);
        var (transCalls, transTris) = DrawTranslucentPass(gl, prog, camera.Position);
        var overlayCalls = (overlay is not null && _overlayRenderer is not null)
            ? _overlayRenderer.RenderOverlays(camera, overlay)
            : 0;

        gl.BindVertexArray(0);
        _frameCount++;
        LastFrameMetrics = new RenderFrameMetrics(
            visible,
            culled,
            drawCalls + transCalls + overlayCalls,
            triangles + transTris,
            _frameCount);
        return LastFrameMetrics;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseGpuResources();
        _stagedMeshes.Clear();
        _stagedAtlas = null;
        _disposed = true;
    }

    private static Vector3 ComputeCameraRelativeOffset(SectionPos sectionPos, Vec3d cameraPos) =>
        new(
            (float)((sectionPos.X * 16.0) - cameraPos.X),
            (float)((sectionPos.Y * 16.0) - cameraPos.Y),
            (float)((sectionPos.Z * 16.0) - cameraPos.Z));

    private static void RemoveMatchingColumn<TValue>(Dictionary<SectionPos, TValue> map, ChunkPos chunkPos)
    {
        if (map.Count == 0)
        {
            return;
        }

        var keys = new List<SectionPos>();
        foreach (var key in map.Keys)
        {
            if (key.X == chunkPos.X && key.Z == chunkPos.Z)
            {
                keys.Add(key);
            }
        }

        foreach (var key in keys)
        {
            map.Remove(key);
        }
    }

    private unsafe (int Visible, int Culled, int DrawCalls, int Triangles) DrawOpaqueAndCollectTranslucent(
        GL gl,
        SilkOpenGlProgramResources prog,
        in Frustum frustum,
        Vec3d cameraPos)
    {
        var visible = 0;
        var culled = 0;
        var drawCalls = 0;
        var triangles = 0;

        _translucentSortScratch.Clear();
        gl.Disable(EnableCap.Blend);
        gl.DepthMask(true);

        foreach (var section in _gpuSections.Values)
        {
            if (!frustum.Intersects(section.WorldBounds))
            {
                culled++;
                continue;
            }

            visible++;
            var relOffset = ComputeCameraRelativeOffset(section.SectionPosition, cameraPos);

            if (section.OpaqueIndexCount > 0)
            {
                gl.Uniform3(prog.LocSectionOffset, relOffset.X, relOffset.Y, relOffset.Z);
                gl.BindVertexArray(section.OpaqueVao);
                gl.DrawElements(PrimitiveType.Triangles, (uint)section.OpaqueIndexCount, DrawElementsType.UnsignedInt, null);
                drawCalls++;
                triangles += section.OpaqueIndexCount / 3;
            }

            if (section.TranslucentIndexCount > 0)
            {
                var centerOffset = relOffset + new Vector3(8.0f, 8.0f, 8.0f);
                _translucentSortScratch.Add((section, centerOffset.LengthSquared()));
            }
        }

        return (visible, culled, drawCalls, triangles);
    }

    private unsafe (int DrawCalls, int Triangles) DrawTranslucentPass(
        GL gl,
        SilkOpenGlProgramResources prog,
        Vec3d cameraPos)
    {
        if (_translucentSortScratch.Count == 0)
        {
            return (0, 0);
        }

        _translucentSortScratch.Sort(static (a, b) => b.DistanceSq.CompareTo(a.DistanceSq));

        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        gl.DepthMask(false);

        var drawCalls = 0;
        var triangles = 0;

        foreach (var (section, _) in _translucentSortScratch)
        {
            var relOffset = ComputeCameraRelativeOffset(section.SectionPosition, cameraPos);
            gl.Uniform3(prog.LocSectionOffset, relOffset.X, relOffset.Y, relOffset.Z);
            gl.BindVertexArray(section.TranslucentVao);
            gl.DrawElements(PrimitiveType.Triangles, (uint)section.TranslucentIndexCount, DrawElementsType.UnsignedInt, null);
            drawCalls++;
            triangles += section.TranslucentIndexCount / 3;
        }

        gl.DepthMask(true);
        gl.Disable(EnableCap.Blend);
        return (drawCalls, triangles);
    }

    private void InitializeGlResources(GL gl, Vector2D<int> framebufferSize)
    {
        gl.Viewport(framebufferSize);
        _program = new SilkOpenGlProgramResources(gl);
        _overlayRenderer = new SilkOpenGlOverlayRenderer(gl);

        RenderingLog.ContextInitialized(_logger, framebufferSize.X, framebufferSize.Y, _options.VSync);

        if (_stagedAtlas is not null)
        {
            _program.UploadAtlas(_stagedAtlas);
            RenderingLog.AtlasUploaded(_logger, _stagedAtlas.Width, _stagedAtlas.Height, _stagedAtlas.TileSize);
        }

        if (_stagedMeshes.Count > 0)
        {
            foreach (var mesh in _stagedMeshes.Values)
            {
                UploadMeshToGpu(gl, mesh);
            }

            _stagedMeshes.Clear();
        }
    }

    private void UploadMeshToGpu(GL gl, SectionMeshData mesh)
    {
        if (_gpuSections.Remove(mesh.SectionPosition, out var previous))
        {
            previous.Dispose();
        }

        var opaquePass = SilkOpenGlProgramResources.CreatePassBuffers(gl, mesh.OpaqueVertices.Span, mesh.OpaqueIndices.Span);
        var transPass = SilkOpenGlProgramResources.CreatePassBuffers(gl, mesh.TranslucentVertices.Span, mesh.TranslucentIndices.Span);

        _gpuSections[mesh.SectionPosition] = new GpuSectionBuffer(
            gl,
            mesh.SectionPosition,
            mesh.WorldBounds,
            opaquePass,
            transPass);
    }

    private RenderFrameMetrics SimulateCpuVisibilityPass(in Frustum frustum)
    {
        var visible = 0;
        var culled = 0;
        var drawCalls = 0;
        var triangles = 0;

        foreach (var mesh in _stagedMeshes.Values)
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

    private void ReleaseGpuResources()
    {
        foreach (var section in _gpuSections.Values)
        {
            section.Dispose();
        }

        _gpuSections.Clear();
        _overlayRenderer?.Dispose();
        _overlayRenderer = null;
        _program?.Dispose();
        _program = null;
        _inputContext?.Dispose();
        _inputContext = null;
        _gl = null;
    }
}
