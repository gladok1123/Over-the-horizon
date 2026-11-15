using System.Numerics;
using Microsoft.Extensions.Logging;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.Rendering.Threading;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Pure <c>Vulkan 1.1</c> voxel chunk renderer. It owns the presentation surface, the swapchain, the terrain
/// pipelines, the resident <see cref="ChunkColumnSlots{TSection}"/> residency map and the dynamic overlay
/// streams, and it drives the same multi-threaded culling and tessellation stages the OpenGL backend uses —
/// shaders are precompiled to SPIR-V at build time, so nothing is ever compiled while the game is running.
/// </summary>
public sealed unsafe partial class VulkanRenderSubsystem : IWindowRenderSubsystem
{
    private readonly RenderingOptions _options;
    private readonly ILogger<VulkanRenderSubsystem> _logger;
    private readonly Dictionary<ChunkPos, Dictionary<int, SectionMeshData>> _stagedColumns = [];
    private readonly Dictionary<ChunkPos, ChunkColumnSlots<VulkanSectionSlot>> _gpuColumns = [];
    private readonly SectionCullScratch<VulkanSectionSlot> _cullScratch = new(RenderParallelism.AutoWorkerCount);
    private readonly OverlayGeometry _overlayGeometry;

    private ChunkColumnSlots<VulkanSectionSlot>[] _cullColumnArray = [];
    private int _cullColumnCount;
    private bool _cullColumnsDirty = true;

    private TextureAtlasData? _stagedAtlas;
    private TextureAtlasData? _stagedEntityAtlas;
    private int _stagedSectionCount;
    private int _gpuSectionCount;
    private long _frameCount;
    private bool _disposed;

    private IWindow? _window;
    private IInputContext? _inputContext;
    private VulkanDevice? _device;
    private VulkanShaderModules? _shaderModules;
    private VulkanPipelines? _pipelines;
    private VulkanDescriptorSets? _descriptorSets;

    private VulkanBuffer[] _frameUniforms = [];
    private Sampler _atlasSampler;
    private VulkanImage? _blockAtlasImage;
    private VulkanImage? _entityAtlasImage;
    private VulkanVertexStream? _overlayStream;
    private VulkanUiRenderer? _uiRenderer;

    /// <summary>Initializes a new instance of the <see cref="VulkanRenderSubsystem"/> class.</summary>
    /// <param name="options">Rendering options (window size, VSync, worker caps).</param>
    /// <param name="logger">Structured logger.</param>
    public VulkanRenderSubsystem(RenderingOptions options, ILogger<VulkanRenderSubsystem> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
        _overlayGeometry = new OverlayGeometry(options.MaxRenderThreads);
    }

    /// <inheritdoc />
    public string BackendName => "Vulkan 1.1";

    /// <inheritdoc />
    public bool IsContextInitialized => _device is not null && !_disposed;

    /// <inheritdoc />
    public int UploadedSectionCount => IsContextInitialized ? _gpuSectionCount : _stagedSectionCount;

    /// <inheritdoc />
    public RenderFrameMetrics LastFrameMetrics { get; private set; }

    /// <inheritdoc />
    public IInputContext? InputContext => _inputContext;

    /// <inheritdoc />
    public Vector2 WindowPixelSize => _window is { } w
        ? new Vector2(w.Size.X, w.Size.Y)
        : new Vector2(_options.WindowWidth, _options.WindowHeight);

    /// <inheritdoc />
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
    public void RequestClose() => _window?.Close();

    /// <inheritdoc />
    public void UploadAtlas(TextureAtlasData atlas)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(atlas);

        _stagedAtlas = atlas;
        if (_device is not null)
        {
            UploadBlockAtlasImage(atlas);
        }
    }

    /// <inheritdoc />
    public void UploadEntityAtlas(TextureAtlasData entityAtlas)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(entityAtlas);

        _stagedEntityAtlas = entityAtlas;
        if (_device is not null)
        {
            UploadEntityAtlasImage(entityAtlas);
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

        var chunkPos = new ChunkPos(mesh.SectionPosition.X, mesh.SectionPosition.Z);
        if (_device is null)
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
        UploadMeshToDevice(mesh);
        _cullColumnsDirty = true;
    }

    /// <inheritdoc />
    public void RemoveSectionMesh(SectionPos sectionPosition)
    {
        RemoveStagedSection(sectionPosition);
        var chunkPos = new ChunkPos(sectionPosition.X, sectionPosition.Z);
        if (_gpuColumns.TryGetValue(chunkPos, out var col))
        {
            _gpuSectionCount -= col.RemoveSection(sectionPosition.Y);
            _cullColumnsDirty = true;
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
            _cullColumnsDirty = true;
            gpuCol.Dispose();
        }
    }

    /// <inheritdoc />
    public void RunWindowLoop(
        Action<double> onUpdate,
        Action<double> onRender,
        Action<int, int>? onResize = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(onUpdate);
        ArgumentNullException.ThrowIfNull(onRender);

        SilkNativeResolver.EnsureRegistered();
        GlfwWindowing.RegisterPlatform();

        var winOpts = WindowOptions.Default;
        winOpts.Size = new Vector2D<int>(_options.WindowWidth, _options.WindowHeight);
        winOpts.Title = _options.WindowTitle;
        winOpts.VSync = _options.VSync;
        winOpts.API = GraphicsAPI.DefaultVulkan;

        using var window = Window.Create(winOpts);
        _window = window;

        using var ctr = cancellationToken.Register(() => window.Close());

        window.Load += () =>
        {
            _inputContext = window.CreateInput();
            InitializeDeviceResources(window, (uint)window.FramebufferSize.X, (uint)window.FramebufferSize.Y);
        };

        window.FramebufferResize += newSize =>
        {
            _device?.InvalidateSwapchain();
            onResize?.Invoke(newSize.X, newSize.Y);
        };

        window.Update += onUpdate;
        window.Render += onRender;
        window.Closing += ReleaseDeviceResources;

        try
        {
            window.Run();
        }
        finally
        {
            ReleaseDeviceResources();
            _window = null;
        }
    }

    private ChunkColumnSlots<VulkanSectionSlot>[] GetCullColumnSnapshot(out int columnCount)
    {
        if (_cullColumnsDirty)
        {
            RebuildCullColumnArray();
        }

        columnCount = _cullColumnCount;
        return _cullColumnArray;
    }

    private void RebuildCullColumnArray()
    {
        if (_cullColumnArray.Length < _gpuColumns.Count)
        {
            _cullColumnArray = new ChunkColumnSlots<VulkanSectionSlot>[Math.Max(64, _gpuColumns.Count * 2)];
        }

        _cullColumnCount = 0;
        foreach (var col in _gpuColumns.Values)
        {
            _cullColumnArray[_cullColumnCount++] = col;
        }

        _cullColumnsDirty = false;
    }

    private void RemoveStagedSection(SectionPos sectionPos)
    {
        var chunkPos = new ChunkPos(sectionPos.X, sectionPos.Z);
        if (_stagedColumns.TryGetValue(chunkPos, out var colMap) && colMap.Remove(sectionPos.Y))
        {
            _stagedSectionCount--;
            if (colMap.Count == 0)
            {
                _stagedColumns.Remove(chunkPos);
            }
        }
    }

    private void UploadMeshToDevice(SectionMeshData mesh)
    {
        var device = _device!;
        var slot = VulkanSectionSlot.Create(
            device.Api,
            device.LogicalDevice,
            device.PhysicalDevice,
            device.Pool,
            device.GraphicsQueue,
            mesh);

        var chunkPos = new ChunkPos(mesh.SectionPosition.X, mesh.SectionPosition.Z);
        if (!_gpuColumns.TryGetValue(chunkPos, out var col))
        {
            col = new ChunkColumnSlots<VulkanSectionSlot>(chunkPos);
            _gpuColumns[chunkPos] = col;
        }

        _gpuSectionCount += col.UpsertSection(slot);
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

    private void InitializeDeviceResources(IVkSurfaceSource surfaceSource, uint width, uint height)
    {
        var device = new VulkanDevice(surfaceSource, width, height);
        _device = device;

        var vk = device.Api;
        _shaderModules = new VulkanShaderModules(vk, device.LogicalDevice);
        _pipelines = new VulkanPipelines(vk, device.LogicalDevice, device.RenderPass, _shaderModules);
        _descriptorSets = new VulkanDescriptorSets(vk, device.LogicalDevice, _pipelines, VulkanDevice.FramesInFlight);

        _frameUniforms = new VulkanBuffer[VulkanDevice.FramesInFlight];
        for (var i = 0; i < _frameUniforms.Length; i++)
        {
            _frameUniforms[i] = VulkanBuffer.Create(
                vk,
                device.LogicalDevice,
                device.PhysicalDevice,
                (ulong)VulkanFrameUniforms.SizeInBytes,
                BufferUsageFlags.UniformBufferBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
            _descriptorSets.BindFrameUniforms((uint)i, _frameUniforms[i]);
        }

        _atlasSampler = VulkanImage.CreateNearestSampler(vk, device.LogicalDevice);
        _overlayStream = new VulkanVertexStream(vk, device.LogicalDevice, device.PhysicalDevice, 4UL * 1024 * 1024);
        _uiRenderer = new VulkanUiRenderer(device, _pipelines, _descriptorSets, _atlasSampler);

        RenderingLog.ContextInitialized(_logger, (int)width, (int)height, _options.VSync);

        if (_stagedAtlas is not null)
        {
            UploadBlockAtlasImage(_stagedAtlas);
        }

        _stagedEntityAtlas ??= EntityTextureAtlasGenerator.CreateDefault();
        UploadEntityAtlasImage(_stagedEntityAtlas);

        if (_stagedColumns.Count > 0)
        {
            foreach (var colMap in _stagedColumns.Values)
            {
                foreach (var mesh in colMap.Values)
                {
                    UploadMeshToDevice(mesh);
                }
            }

            _stagedColumns.Clear();
            _stagedSectionCount = 0;
            _cullColumnsDirty = true;
        }
    }

    private void UploadBlockAtlasImage(TextureAtlasData atlas)
    {
        var device = _device!;
        _blockAtlasImage?.Dispose();
        _blockAtlasImage = VulkanImage.CreateTexture(
            device.Api,
            device.LogicalDevice,
            device.PhysicalDevice,
            device.Pool,
            device.GraphicsQueue,
            atlas.RgbaPixels.Span,
            (uint)atlas.Width,
            (uint)atlas.Height);

        _descriptorSets!.BindBlockAtlas(_atlasSampler, _blockAtlasImage.View);
        _uiRenderer?.SetItemAtlas(_blockAtlasImage.View);
        BindOverlayAtlasesIfReady();
        RenderingLog.AtlasUploaded(_logger, atlas.Width, atlas.Height, atlas.TileSize);
    }

    private void UploadEntityAtlasImage(TextureAtlasData atlas)
    {
        var device = _device!;
        _entityAtlasImage?.Dispose();
        _entityAtlasImage = VulkanImage.CreateTexture(
            device.Api,
            device.LogicalDevice,
            device.PhysicalDevice,
            device.Pool,
            device.GraphicsQueue,
            atlas.RgbaPixels.Span,
            (uint)atlas.Width,
            (uint)atlas.Height);

        BindOverlayAtlasesIfReady();
    }

    private void BindOverlayAtlasesIfReady()
    {
        if (_blockAtlasImage is not null && _entityAtlasImage is not null)
        {
            _descriptorSets!.BindOverlayAtlases(_atlasSampler, _blockAtlasImage.View, _entityAtlasImage.View);
        }
    }

    private void ReleaseDeviceResources()
    {
        if (_device is { } device)
        {
            device.WaitIdle();

            foreach (var col in _gpuColumns.Values)
            {
                col.Dispose();
            }
        }

        _gpuColumns.Clear();
        _gpuSectionCount = 0;
        _cullColumnCount = 0;
        _cullColumnsDirty = true;

        _uiRenderer?.Dispose();
        _overlayStream?.Dispose();
        _entityAtlasImage?.Dispose();
        _blockAtlasImage?.Dispose();

        foreach (var buffer in _frameUniforms)
        {
            buffer.Dispose();
        }

        if (_device is { } owned && _atlasSampler.Handle != 0)
        {
            owned.Api.DestroySampler(owned.LogicalDevice, _atlasSampler, null);
        }

        _descriptorSets?.Dispose();
        _pipelines?.Dispose();
        _shaderModules?.Dispose();
        _device?.Dispose();
        _inputContext?.Dispose();

        _uiRenderer = null;
        _overlayStream = null;
        _entityAtlasImage = null;
        _blockAtlasImage = null;
        _descriptorSets = null;
        _pipelines = null;
        _shaderModules = null;
        _device = null;
        _inputContext = null;
        _frameUniforms = [];
        _atlasSampler = default;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ReleaseDeviceResources();
        _stagedColumns.Clear();
        _stagedSectionCount = 0;
        _stagedAtlas = null;
        _stagedEntityAtlas = null;
        _disposed = true;
    }
}
