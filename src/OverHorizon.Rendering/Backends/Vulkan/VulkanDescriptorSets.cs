using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Owns the descriptor pool and every descriptor set the renderer binds: one terrain set per frame in flight
/// (so each frame can point at its own uniform buffer), plus the shared overlay and HUD sampler sets.
/// </summary>
internal sealed unsafe class VulkanDescriptorSets : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly DescriptorSet[] _chunkSets;
    private DescriptorPool _pool;
    private DescriptorSet _overlaySet;
    private DescriptorSet _uiSet;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanDescriptorSets"/> class.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="pipelines">Pipelines supplying the descriptor layouts.</param>
    /// <param name="framesInFlight">Number of frames that may be in flight at once.</param>
    public VulkanDescriptorSets(Vk vk, Device device, VulkanPipelines pipelines, uint framesInFlight)
    {
        _vk = vk;
        _device = device;
        _chunkSets = new DescriptorSet[framesInFlight];

        var poolSizes = stackalloc DescriptorPoolSize[2];
        poolSizes[0] = new DescriptorPoolSize(DescriptorType.UniformBuffer, framesInFlight);
        poolSizes[1] = new DescriptorPoolSize(DescriptorType.CombinedImageSampler, framesInFlight + 5);

        var poolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 2,
            PPoolSizes = poolSizes,
            MaxSets = framesInFlight + 2,
        };

        VulkanResult.Ensure(vk.CreateDescriptorPool(device, in poolInfo, null, out _pool), "vkCreateDescriptorPool");

        var layouts = new DescriptorSetLayout[framesInFlight + 2];
        for (var i = 0; i < framesInFlight; i++)
        {
            layouts[i] = pipelines.ChunkSetLayout;
        }

        layouts[framesInFlight] = pipelines.OverlaySetLayout;
        layouts[framesInFlight + 1] = pipelines.UiSetLayout;

        var sets = new DescriptorSet[layouts.Length];
        fixed (DescriptorSetLayout* layoutsPtr = layouts)
        fixed (DescriptorSet* setsPtr = sets)
        {
            var allocateInfo = new DescriptorSetAllocateInfo
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = _pool,
                DescriptorSetCount = (uint)layouts.Length,
                PSetLayouts = layoutsPtr,
            };

            VulkanResult.Ensure(vk.AllocateDescriptorSets(device, in allocateInfo, setsPtr), "vkAllocateDescriptorSets");
        }

        for (var i = 0; i < framesInFlight; i++)
        {
            _chunkSets[i] = sets[i];
        }

        _overlaySet = sets[framesInFlight];
        _uiSet = sets[framesInFlight + 1];
    }

    /// <summary>Gets the shared overlay descriptor set (block atlas plus entity atlas).</summary>
    public DescriptorSet OverlaySet => _overlaySet;

    /// <summary>Gets the shared HUD descriptor set (font, item and GUI atlases).</summary>
    public DescriptorSet UiSet => _uiSet;

    /// <summary>
    /// Gets the terrain descriptor set belonging to one frame in flight.
    /// </summary>
    /// <param name="frameIndex">Zero-based frame index.</param>
    /// <returns>The terrain descriptor set for that frame.</returns>
    public DescriptorSet ChunkSetFor(uint frameIndex) => _chunkSets[frameIndex];

    /// <summary>
    /// Points the terrain descriptor set of one frame at that frame's uniform buffer.
    /// </summary>
    /// <param name="frameIndex">Zero-based frame index.</param>
    /// <param name="uniformBuffer">Host-visible uniform buffer of that frame.</param>
    public void BindFrameUniforms(uint frameIndex, VulkanBuffer uniformBuffer)
    {
        var bufferInfo = new DescriptorBufferInfo
        {
            Buffer = uniformBuffer.Handle,
            Offset = 0,
            Range = VulkanFrameUniforms.SizeInBytes,
        };

        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _chunkSets[frameIndex],
            DstBinding = 0,
            DstArrayElement = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            PBufferInfo = &bufferInfo,
        };

        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
    }

    /// <summary>
    /// Binds the block atlas to every terrain descriptor set.
    /// </summary>
    /// <param name="sampler">Nearest-neighbour sampler.</param>
    /// <param name="view">Block atlas image view.</param>
    public void BindBlockAtlas(Sampler sampler, ImageView view)
    {
        var imageInfo = new DescriptorImageInfo
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = view,
            Sampler = sampler,
        };

        for (var i = 0u; i < _chunkSets.Length; i++)
        {
            WriteImage(_chunkSets[i], &imageInfo, 1);
        }
    }

    /// <summary>
    /// Binds the block and entity atlases used by the overlay programs.
    /// </summary>
    /// <param name="sampler">Nearest-neighbour sampler.</param>
    /// <param name="blockAtlas">Block/item atlas image view.</param>
    /// <param name="entityAtlas">Mob and entity atlas image view.</param>
    public void BindOverlayAtlases(Sampler sampler, ImageView blockAtlas, ImageView entityAtlas)
    {
        var infos = stackalloc DescriptorImageInfo[2];
        infos[0] = new DescriptorImageInfo
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = blockAtlas,
            Sampler = sampler,
        };

        infos[1] = new DescriptorImageInfo
        {
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageView = entityAtlas,
            Sampler = sampler,
        };

        var writes = stackalloc WriteDescriptorSet[2];
        writes[0] = CreateImageWrite(_overlaySet, &infos[0], 1);
        writes[1] = CreateImageWrite(_overlaySet, &infos[1], 2);
        _vk.UpdateDescriptorSets(_device, 2, writes, 0, null);
    }

    /// <summary>
    /// Binds the font, item and GUI atlases used by the HUD program.
    /// </summary>
    /// <param name="sampler">Nearest-neighbour sampler.</param>
    /// <param name="fontAtlas">Bitmap font atlas image view.</param>
    /// <param name="itemAtlas">Item/block sprite atlas image view.</param>
    /// <param name="guiAtlas">Vanilla GUI sheet atlas image view.</param>
    public void BindUiAtlases(Sampler sampler, ImageView fontAtlas, ImageView itemAtlas, ImageView guiAtlas)
    {
        var infos = stackalloc DescriptorImageInfo[3];
        infos[0] = CreateInfo(sampler, fontAtlas);
        infos[1] = CreateInfo(sampler, itemAtlas);
        infos[2] = CreateInfo(sampler, guiAtlas);

        var writes = stackalloc WriteDescriptorSet[3];
        writes[0] = CreateImageWrite(_uiSet, &infos[0], 1);
        writes[1] = CreateImageWrite(_uiSet, &infos[1], 2);
        writes[2] = CreateImageWrite(_uiSet, &infos[2], 3);
        _vk.UpdateDescriptorSets(_device, 3, writes, 0, null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _vk.DestroyDescriptorPool(_device, _pool, null);
        _pool = default;
        _overlaySet = default;
        _uiSet = default;
    }

    private static DescriptorImageInfo CreateInfo(Sampler sampler, ImageView view) => new()
    {
        ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
        ImageView = view,
        Sampler = sampler,
    };

    private static WriteDescriptorSet CreateImageWrite(DescriptorSet set, DescriptorImageInfo* info, uint binding) => new()
    {
        SType = StructureType.WriteDescriptorSet,
        DstSet = set,
        DstBinding = binding,
        DstArrayElement = 0,
        DescriptorType = DescriptorType.CombinedImageSampler,
        DescriptorCount = 1,
        PImageInfo = info,
    };

    private void WriteImage(DescriptorSet set, DescriptorImageInfo* info, uint binding)
    {
        var write = CreateImageWrite(set, info, binding);
        _vk.UpdateDescriptorSets(_device, 1, &write, 0, null);
    }
}
