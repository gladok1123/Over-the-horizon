using System.Numerics;
using Microsoft.Extensions.Logging;
using OverHorizon.Core;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using Silk.NET.Core.Loader;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Silk.NET OpenGL 3.3 Core Profile voxel chunk renderer with <see cref="ChunkPos"/>-indexed <see cref="GpuChunkColumn"/>
/// <c>O(1)</c> residency, column-level spherical distance + frustum culling, dynamic Day/Sunset/Night sky &amp; held-torch lighting,
/// and two-pass opaque + translucent drawing.
/// </summary>
public sealed class SilkOpenGlRenderSubsystem : IRenderSubsystem
{
    private static readonly Lock ResolverSyncRoot = new();
    private readonly RenderingOptions _options;
    private readonly ILogger<SilkOpenGlRenderSubsystem> _logger;
    private readonly Dictionary<ChunkPos, Dictionary<int, SectionMeshData>> _stagedColumns = [];
    private readonly Dictionary<ChunkPos, GpuChunkColumn> _gpuColumns = [];
    private readonly List<(GpuSectionBuffer Buffer, float DistanceSq)> _translucentSortScratch = [];

    private TextureAtlasData? _stagedAtlas;
    private TextureAtlasData? _stagedEntityAtlas;
    private int _stagedSectionCount;
    private int _gpuSectionCount;
    private IWindow? _window;
    private IInputContext? _inputContext;
    private GL? _gl;
    private SilkOpenGlProgramResources? _program;
    private SilkOpenGlOverlayRenderer? _overlayRenderer;
    private long _frameCount;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="SilkOpenGlRenderSubsystem"/> class.</summary>
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
    public int UploadedSectionCount => IsContextInitialized ? _gpuSectionCount : _stagedSectionCount;

    /// <inheritdoc />
    public RenderFrameMetrics LastFrameMetrics { get; private set; }

    /// <summary>Gets the active Silk.NET keyboard &amp; mouse input context when the window is open.</summary>
    public IInputContext? InputContext => _inputContext;

    /// <summary>Gets the current window framebuffer dimensions in pixels.</summary>
    public Vector2 WindowPixelSize => _window is { } w
        ? new Vector2(w.Size.X, w.Size.Y)
        : new Vector2(_options.WindowWidth, _options.WindowHeight);

    /// <summary>Gets or sets the OS window title.</summary>
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

    /// <summary>Requests graceful window closure at the end of the current frame.</summary>
    public void RequestClose() => _window?.Close();

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

    /// <summary>Uploads or replaces the <c>512x256</c> entity skin sheet (<c>steve.png</c> + 8 mobs).</summary>
    public void UploadEntityAtlas(TextureAtlasData entityAtlas)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(entityAtlas);

        _stagedEntityAtlas = entityAtlas;
        _program?.UploadEntityAtlas(entityAtlas);
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

        var chunkPos = new ChunkPos(mesh.SectionPosition.X, mesh.SectionPosition.Z);
        if (_gl is null)
        {
            if (!_stagedColumns.TryGetValue(chunkPos, out var colMap))
            {
                colMap = [];
                _stagedColumns[chunkPos] = colMap;
            }

            if (!colMap.ContainsKey(mesh.SectionPosition.Y))
            {
                _stagedSectionCount++;
            }

            colMap[mesh.SectionPosition.Y] = mesh;
            return;
        }

        RemoveStagedSection(mesh.SectionPosition);
        UploadMeshToGpu(_gl, mesh);
    }

    /// <inheritdoc />
    public void RemoveSectionMesh(SectionPos sectionPosition)
    {
        RemoveStagedSection(sectionPosition);
        var chunkPos = new ChunkPos(sectionPosition.X, sectionPosition.Z);
        if (_gpuColumns.TryGetValue(chunkPos, out var col))
        {
            _gpuSectionCount -= col.RemoveSection(sectionPosition.Y);
            if (col.IsEmpty)
            {
                _gpuColumns.Remove(chunkPos);
                col.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void RemoveChunkMeshes(ChunkPos chunkPosition)
    {
        if (_stagedColumns.Remove(chunkPosition, out var stagedCol))
        {
            _stagedSectionCount -= stagedCol.Count;
        }

        if (_gpuColumns.Remove(chunkPosition, out var gpuCol))
        {
            _gpuSectionCount -= gpuCol.Count;
            gpuCol.Dispose();
        }
    }

    /// <summary>Creates the OpenGL 3.3 Core window and runs the interactive update/render loop until closed.</summary>
    public void RunWindowLoop(
        Action<double> onUpdate,
        Action<double> onRender,
        Action<int, int>? onResize = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(onUpdate);
        ArgumentNullException.ThrowIfNull(onRender);

        EnsureSingleFileNativeResolver();
        GlfwWindowing.RegisterPlatform();

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

        try
        {
            window.Run();
        }
        finally
        {
            ReleaseGpuResources();
            _window = null;
        }
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
        var underwater = overlay?.IsUnderwater == true;
        var skyColor = SilkOpenGlChunkPassDrawer.ComputeSkyAtmosphereColor(clampedSky, overlay?.WorldTimeTicks ?? 1000L, underwater);
        if (!underwater && overlay is not null)
        {
            if (camera.Position.Y < EngineConstants.WorldMinY + 2.0)
            {
                var voidFactor = (float)Math.Clamp((EngineConstants.WorldMinY + 2.0 - camera.Position.Y) / 8.0, 0.35, 1.0);
                skyColor = Vector3.Lerp(skyColor, new Vector3(0.010f, 0.010f, 0.016f), voidFactor);
            }
            else if (camera.Position.Y < 54.0 && overlay.CameraSkyLightNormalized < 0.15f)
            {
                var caveDark = (1.0f - (overlay.CameraSkyLightNormalized / 0.15f)) * (float)Math.Clamp((54.0 - camera.Position.Y) / 14.0, 0.0, 1.0);
                skyColor = Vector3.Lerp(skyColor, new Vector3(0.018f, 0.018f, 0.024f), caveDark * 0.95f);
            }
        }

        var chunkFogDist = overlay is { ChunkFogDistance: > 16.0f } ? overlay.ChunkFogDistance : camera.FarPlane;
        var isNether = overlay?.IsNetherDimension == true;
        if (isNether)
        {
            // The Nether has no sky: a flat sulfurous haze with heavy volcanic fog.
            skyColor = new Vector3(0.145f, 0.052f, 0.048f);
            chunkFogDist = Math.Min(chunkFogDist, 34.0f);
        }

        gl.ClearColor(skyColor.X, skyColor.Y, skyColor.Z, 1.0f);
        gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        var skyCalls = (overlay is not null && _overlayRenderer is not null) ? _overlayRenderer.RenderSkyDome(camera, overlay) : 0;

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
        gl.Uniform1(prog.LocHeldTorchLight, overlay?.HeldTorchLightLevel ?? 0.0f);
        var fogStart = chunkFogDist * 0.62f;
        var fogEnd = chunkFogDist * 0.96f;
        if (isNether)
        {
            fogStart = 1.5f;
            fogEnd = chunkFogDist * 0.78f;
        }
        else if (underwater)
        {
            fogStart = 0.8f;
            fogEnd = 13.5f;
        }

        gl.Uniform1(prog.LocFogStart, fogStart);
        gl.Uniform1(prog.LocFogEnd, fogEnd);
        gl.Uniform3(prog.LocSkyColor, skyColor.X, skyColor.Y, skyColor.Z);
        gl.Uniform1(prog.LocUnderwater, underwater ? 1.0f : 0.0f);
        gl.Uniform1(prog.LocAmbientFloor, isNether ? DimensionRules.GetAmbientLightFloor(DimensionId.Nether) : DimensionRules.GetAmbientLightFloor(DimensionId.Overworld));

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, prog.AtlasTexture);

        var maxHorizDistSq = (chunkFogDist + 24.0f) * (chunkFogDist + 24.0f);
        var (visible, culled, drawCalls, triangles) = SilkOpenGlChunkPassDrawer.DrawOpaqueAndCollectTranslucent(gl, prog, _gpuColumns, _translucentSortScratch, in frustum, camera.Position, maxHorizDistSq);
        var (transCalls, transTris) = SilkOpenGlChunkPassDrawer.DrawTranslucentPass(gl, prog, _translucentSortScratch, camera.Position);
        var overlayCalls = (overlay is not null && _overlayRenderer is not null)
            ? _overlayRenderer.RenderOverlays(camera, overlay, prog.AtlasTexture, prog.EntityAtlasTexture, _stagedAtlas, skyColor, chunkFogDist)
            : 0;

        gl.BindVertexArray(0);
        _frameCount++;
        LastFrameMetrics = new RenderFrameMetrics(visible, culled, skyCalls + drawCalls + transCalls + overlayCalls, triangles + transTris, _frameCount);
        return LastFrameMetrics;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        ReleaseGpuResources();
        _stagedColumns.Clear();
        _stagedSectionCount = 0;
        _stagedAtlas = null;
        _stagedEntityAtlas = null;
        _disposed = true;
    }

    private void RemoveStagedSection(SectionPos sectionPos)
    {
        var chunkPos = new ChunkPos(sectionPos.X, sectionPos.Z);
        if (_stagedColumns.TryGetValue(chunkPos, out var colMap) && colMap.Remove(sectionPos.Y))
        {
            _stagedSectionCount--;
            if (colMap.Count == 0) _stagedColumns.Remove(chunkPos);
        }
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

        _stagedEntityAtlas ??= EntityTextureAtlasGenerator.CreateDefault();
        _program.UploadEntityAtlas(_stagedEntityAtlas);

        if (_stagedColumns.Count > 0)
        {
            foreach (var colMap in _stagedColumns.Values)
            {
                foreach (var mesh in colMap.Values)
                {
                    UploadMeshToGpu(gl, mesh);
                }
            }

            _stagedColumns.Clear();
            _stagedSectionCount = 0;
        }
    }

    private void UploadMeshToGpu(GL gl, SectionMeshData mesh)
    {
        var opaquePass = SilkOpenGlProgramResources.CreatePassBuffers(gl, mesh.OpaqueVertices.Span, mesh.OpaqueIndices.Span);
        var transPass = SilkOpenGlProgramResources.CreatePassBuffers(gl, mesh.TranslucentVertices.Span, mesh.TranslucentIndices.Span);

        var buffer = new GpuSectionBuffer(
            gl,
            mesh.SectionPosition,
            mesh.WorldBounds,
            opaquePass,
            transPass);

        var chunkPos = new ChunkPos(mesh.SectionPosition.X, mesh.SectionPosition.Z);
        if (!_gpuColumns.TryGetValue(chunkPos, out var col))
        {
            col = new GpuChunkColumn(chunkPos);
            _gpuColumns[chunkPos] = col;
        }

        _gpuSectionCount += col.UpsertSection(buffer);
    }

    private RenderFrameMetrics SimulateCpuVisibilityPass(in Frustum frustum)
    {
        var visible = 0;
        var culled = 0;
        var drawCalls = 0;
        var triangles = 0;

        foreach (var colMap in _stagedColumns.Values)
        {
            foreach (var mesh in colMap.Values)
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
        }

        _frameCount++;
        LastFrameMetrics = new RenderFrameMetrics(visible, culled, drawCalls, triangles, _frameCount);
        return LastFrameMetrics;
    }

    private void ReleaseGpuResources()
    {
        var hasValidContext = _gl is not null && _window?.GLContext?.IsCurrent == true;
        if (hasValidContext)
        {
            foreach (var col in _gpuColumns.Values)
            {
                col.Dispose();
            }

            _overlayRenderer?.Dispose();
            _program?.Dispose();
            _inputContext?.Dispose();
        }

        _gpuColumns.Clear();
        _gpuSectionCount = 0;
        _overlayRenderer = null;
        _program = null;
        _inputContext = null;
        _gl = null;
    }

    private static void EnsureSingleFileNativeResolver()
    {
        const string markerKey = "OverHorizon.SilkSingleFileResolverRegistered";
        lock (ResolverSyncRoot)
        {
            if (AppContext.GetData(markerKey) is true || PathResolver.Default is not DefaultPathResolver resolver)
            {
                return;
            }

            var idx = resolver.Resolvers.IndexOf(DefaultPathResolver.MacVersioningResolver);
            resolver.Resolvers.Insert(idx >= 0 ? idx + 1 : 0, ResolveNativeFromAppContext);
            AppContext.SetData(markerKey, data: true);
        }
    }

    private static List<string> ResolveNativeFromAppContext(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate))
        {
            return [];
        }

        var fileName = Path.GetFileName(candidate);
        if (string.IsNullOrWhiteSpace(fileName)
            || AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is not string rawDirs
            || string.IsNullOrWhiteSpace(rawDirs))
        {
            return [];
        }

        var matches = new List<string>();
        foreach (var dir in rawDirs.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(Directory.Exists))
        {
            var exact = Path.Combine(dir, fileName);
            if (File.Exists(exact))
            {
                matches.Add(exact);
            }

            if (fileName.Contains(".so", StringComparison.Ordinal))
            {
                matches.AddRange(Directory.EnumerateFiles(dir, fileName + ".*").Order(StringComparer.Ordinal));
            }
            else if (fileName.EndsWith(".dylib", StringComparison.Ordinal))
            {
                matches.AddRange(Directory.EnumerateFiles(dir, fileName[..^".dylib".Length] + ".*.dylib").Order(StringComparer.Ordinal));
            }
        }

        return matches;
    }
}
