using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Batched Vulkan pixel-space 2D HUD renderer. It owns the bitmap-font and vanilla-GUI atlases, borrows the
/// block/item atlas from the terrain pass, and streams one <see cref="TexturedUiVertex"/> batch per frame
/// through a persistently mapped host-visible buffer.
/// </summary>
internal sealed unsafe class VulkanUiRenderer : IDisposable
{
    private const ulong InitialCapacity = 1024UL * 1024;

    private readonly VulkanDevice _device;
    private readonly VulkanPipelines _pipelines;
    private readonly VulkanDescriptorSets _descriptorSets;
    private readonly Sampler _sampler;
    private readonly List<TexturedUiVertex> _vertices = new(12288);
    private readonly VulkanVertexStream _stream;

    private VulkanImage _fontAtlas;
    private VulkanImage _guiAtlas;
    private ulong _peakBytes = InitialCapacity;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="VulkanUiRenderer"/> class.</summary>
    /// <param name="device">Active Vulkan device.</param>
    /// <param name="pipelines">Pipeline bundle providing the HUD pipeline and layout.</param>
    /// <param name="descriptorSets">Descriptor set bundle providing the HUD set.</param>
    /// <param name="sampler">Nearest-neighbour atlas sampler.</param>
    public VulkanUiRenderer(
        VulkanDevice device,
        VulkanPipelines pipelines,
        VulkanDescriptorSets descriptorSets,
        Sampler sampler)
    {
        _device = device;
        _pipelines = pipelines;
        _descriptorSets = descriptorSets;
        _sampler = sampler;

        var fontPixels = PixelFontGlyphs.CreateRgbaAtlas();
        _fontAtlas = CreateImage(fontPixels, (uint)PixelFontGlyphs.AtlasWidth, (uint)PixelFontGlyphs.AtlasHeight);
        _guiAtlas = CreateImage(
            GuiTextureAtlasGenerator.Generate(),
            (uint)GuiTextureAtlasGenerator.AtlasWidth,
            (uint)GuiTextureAtlasGenerator.AtlasHeight);

        _stream = new VulkanVertexStream(device.Api, device.LogicalDevice, device.PhysicalDevice, InitialCapacity);
    }

    /// <summary>Points the HUD's item slot at the freshly uploaded block/item atlas.</summary>
    /// <param name="itemAtlas">Block/item atlas image view.</param>
    public void SetItemAtlas(ImageView itemAtlas)
    {
        _descriptorSets.BindUiAtlases(_sampler, _fontAtlas.View, itemAtlas, _guiAtlas.View);
    }

    /// <summary>
    /// Builds and records the HUD batch for <paramref name="state"/>.
    /// </summary>
    /// <param name="cmd">Active command buffer.</param>
    /// <param name="camera">Active first-person camera, used to project remote-player nametags.</param>
    /// <param name="state">Per-frame overlay state.</param>
    /// <returns>Number of draw calls issued.</returns>
    public int RenderUi(CommandBuffer cmd, FirstPersonCamera camera, FrameOverlayState state)
    {
        var width = state.ViewportSize.X > 64.0f ? state.ViewportSize.X : 1280.0f;
        var height = state.ViewportSize.Y > 64.0f ? state.ViewportSize.Y : 720.0f;

        UiGeometryBuilder.BuildFrameQuads(_vertices, camera, width, height, state);
        if (_vertices.Count == 0)
        {
            return 0;
        }

        _stream.Reset();
        _stream.EnsureCapacity(Math.Max(_peakBytes, InitialCapacity));
        if (!_stream.TryWrite(CollectionsMarshal.AsSpan(_vertices), out var offset))
        {
            return 0;
        }

        _peakBytes = Math.Max(_peakBytes, offset + (ulong)(_vertices.Count * Unsafe.SizeOf<TexturedUiVertex>()));

        var vk = _device.Api;
        vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipelines.Ui);
        var set = _descriptorSets.UiSet;
        vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _pipelines.UiLayout, 0, 1, &set, 0, null);

        var viewport = new Vector2(width, height);
        vk.CmdPushConstants(cmd, _pipelines.UiLayout, ShaderStageFlags.VertexBit, 0, 8, &viewport);

        var handle = _stream.Handle;
        var bindingOffset = offset;
        vk.CmdBindVertexBuffers(cmd, 0, 1, &handle, &bindingOffset);
        vk.CmdDraw(cmd, (uint)_vertices.Count, 1, 0, 0);
        return 1;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stream.Dispose();
        _fontAtlas.Dispose();
        _guiAtlas.Dispose();
    }

    private VulkanImage CreateImage(byte[] rgba, uint width, uint height) => VulkanImage.CreateTexture(
        _device.Api,
        _device.LogicalDevice,
        _device.PhysicalDevice,
        _device.Pool,
        _device.GraphicsQueue,
        rgba,
        width,
        height);
}
