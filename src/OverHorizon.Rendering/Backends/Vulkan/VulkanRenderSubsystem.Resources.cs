using System.Numerics;
using Microsoft.Extensions.Logging;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using Silk.NET.Core.Contexts;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Device lifetime and resource management for <see cref="VulkanRenderSubsystem"/>: one-time context creation,
/// atlas uploads, the headless visibility simulation used before a device exists, and teardown
/// (split out to respect the 500-line architecture budget).
/// </summary>
public sealed unsafe partial class VulkanRenderSubsystem
{
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
        var device = new VulkanDevice(surfaceSource, width, height, _options.VSync);
        _device = device;

        // Created before any GPU buffer can be retired: slots and vertex streams capture it at creation.
        _disposer = new VulkanDeferredDisposer();

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
        _uploads = new VulkanUploadQueue(device);
        _meshArena = new VulkanBufferArena(device, MemoryPropertyFlags.DeviceLocalBit);

        // One overlay stream per frame in flight: frame N-1 may still be drawing from the buffer that
        // frame N would otherwise rewind or grow underneath it.
        _overlayStreams = new VulkanVertexStream[VulkanDevice.FramesInFlight];
        for (var i = 0; i < _overlayStreams.Length; i++)
        {
            _overlayStreams[i] = new VulkanVertexStream(
                vk,
                device.LogicalDevice,
                device.PhysicalDevice,
                OverlayStreamInitialCapacity,
                _disposer);
        }

        _uiRenderer = new VulkanUiRenderer(device, _pipelines, _descriptorSets, _atlasSampler, _disposer);

        RenderingLog.ContextInitialized(_logger, (int)width, (int)height, _options.VSync);

        if (_stagedAtlas is not null)
        {
            UploadBlockAtlasImage(_stagedAtlas);
        }

        _stagedEntityAtlas ??= EntityTextureAtlasGenerator.CreateDefault();
        UploadEntityAtlasImage(_stagedEntityAtlas);

        if (_stagedColumns.Count > 0)
        {
            // One submission for the whole backlog instead of one wait per section.
            foreach (var colMap in _stagedColumns.Values)
            {
                foreach (var mesh in colMap.Values)
                {
                    UploadMeshToDevice(mesh);
                }
            }

            _uploads!.Flush();

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
        // Ordering matters: recorded staging copies must reach the GPU before any buffer they write into
        // is destroyed, the columns must retire their buffers before the disposer drains, and the drain
        // must run while the arena that owns the section ranges is still alive.
        _uploads?.Flush();

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

        // The device is idle, so every buffer retired by the columns above (and by any stream growth)
        // can be destroyed right now.
        _disposer?.DrainAll();

        _uiRenderer?.Dispose();
        _meshArena?.Dispose();
        _uploads?.Dispose();
        foreach (var stream in _overlayStreams)
        {
            stream.Dispose();
        }

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
        _meshArena = null;
        _uploads = null;
        _overlayStreams = [];
        _disposer = null;
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
}
