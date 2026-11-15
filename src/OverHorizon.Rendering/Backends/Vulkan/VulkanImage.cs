using Silk.NET.Vulkan;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// A 2D image with its device memory and image view. Used for the block/entity/GUI atlases (uploaded once
/// through a staging buffer) and for the depth attachment of the swapchain render pass.
/// </summary>
internal sealed unsafe class VulkanImage : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    private Image _image;
    private DeviceMemory _memory;
    private ImageView _view;
    private bool _disposed;

    private VulkanImage(Vk vk, Device device, Image image, DeviceMemory memory, ImageView view)
    {
        _vk = vk;
        _device = device;
        _image = image;
        _memory = memory;
        _view = view;
    }

    /// <summary>Gets the raw image handle.</summary>
    public Image Handle => _image;

    /// <summary>Gets the image view bound to the whole first mip level.</summary>
    public ImageView View => _view;

    /// <summary>
    /// Uploads an RGBA8 pixel buffer into a device-local <c>R8G8B8A8_SRGB</c> texture.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="physicalDevice">Physical device used for memory-type selection.</param>
    /// <param name="commandPool">Command pool used for the upload.</param>
    /// <param name="queue">Queue used for the upload.</param>
    /// <param name="rgbaPixels">Row-major RGBA8 pixels.</param>
    /// <param name="width">Texture width in pixels.</param>
    /// <param name="height">Texture height in pixels.</param>
    /// <returns>The uploaded texture.</returns>
    public static VulkanImage CreateTexture(
        Vk vk,
        Device device,
        PhysicalDevice physicalDevice,
        CommandPool commandPool,
        Queue queue,
        ReadOnlySpan<byte> rgbaPixels,
        uint width,
        uint height)
    {
        var size = (ulong)rgbaPixels.Length;
        var extent = new Extent3D(width, height, 1);

        using var staging = VulkanBuffer.Create(
            vk,
            device,
            physicalDevice,
            size,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        staging.Upload(rgbaPixels);

        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = extent,
            MipLevels = 1,
            ArrayLayers = 1,
            Format = Format.R8G8B8A8Unorm,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive,
        };

        Image image;
        VulkanResult.Ensure(vk.CreateImage(device, in imageInfo, null, out image), "vkCreateImage");

        vk.GetImageMemoryRequirements(device, image, out var requirements);
        var allocateInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = VulkanMemory.FindMemoryType(
                vk,
                physicalDevice,
                requirements.MemoryTypeBits,
                MemoryPropertyFlags.DeviceLocalBit),
        };

        DeviceMemory memory;
        VulkanResult.Ensure(vk.AllocateMemory(device, in allocateInfo, null, out memory), "vkAllocateMemory");
        VulkanResult.Ensure(vk.BindImageMemory(device, image, memory, 0), "vkBindImageMemory");

        VulkanMemory.TransitionLayout(
            vk,
            device,
            commandPool,
            queue,
            image,
            ImageLayout.Undefined,
            ImageLayout.TransferDstOptimal,
            ImageAspectFlags.ColorBit);

        var commandBuffer = VulkanMemory.BeginOneShotCommands(vk, device, commandPool);
        var region = new BufferImageCopy
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers(ImageAspectFlags.ColorBit, 0, 0, 1),
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = extent,
        };

        vk.CmdCopyBufferToImage(commandBuffer, staging.Handle, image, ImageLayout.TransferDstOptimal, 1, &region);
        VulkanMemory.EndOneShotCommands(vk, device, commandPool, queue, commandBuffer);

        VulkanMemory.TransitionLayout(
            vk,
            device,
            commandPool,
            queue,
            image,
            ImageLayout.TransferDstOptimal,
            ImageLayout.ShaderReadOnlyOptimal,
            ImageAspectFlags.ColorBit);

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = Format.R8G8B8A8Unorm,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
        };

        ImageView view;
        VulkanResult.Ensure(vk.CreateImageView(device, in viewInfo, null, out view), "vkCreateImageView");
        return new VulkanImage(vk, device, image, memory, view);
    }

    /// <summary>
    /// Creates the 32-bit float depth attachment used by the swapchain render pass.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="physicalDevice">Physical device used for memory-type selection.</param>
    /// <param name="width">Attachment width in pixels.</param>
    /// <param name="height">Attachment height in pixels.</param>
    /// <returns>The depth attachment.</returns>
    public static VulkanImage CreateDepthAttachment(
        Vk vk,
        Device device,
        PhysicalDevice physicalDevice,
        uint width,
        uint height)
    {
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent = new Extent3D(width, height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Format = Format.D32Sfloat,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = ImageUsageFlags.DepthStencilAttachmentBit,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive,
        };

        Image image;
        VulkanResult.Ensure(vk.CreateImage(device, in imageInfo, null, out image), "vkCreateImage");

        vk.GetImageMemoryRequirements(device, image, out var requirements);
        var allocateInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = VulkanMemory.FindMemoryType(
                vk,
                physicalDevice,
                requirements.MemoryTypeBits,
                MemoryPropertyFlags.DeviceLocalBit),
        };

        DeviceMemory memory;
        VulkanResult.Ensure(vk.AllocateMemory(device, in allocateInfo, null, out memory), "vkAllocateMemory");
        VulkanResult.Ensure(vk.BindImageMemory(device, image, memory, 0), "vkBindImageMemory");

        var viewInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = Format.D32Sfloat,
            SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.DepthBit, 0, 1, 0, 1),
        };

        ImageView view;
        VulkanResult.Ensure(vk.CreateImageView(device, in viewInfo, null, out view), "vkCreateImageView");
        return new VulkanImage(vk, device, image, memory, view);
    }

    /// <summary>
    /// Creates a nearest-neighbour sampler with clamp-to-edge addressing, matching the blocky Minecraft look.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <returns>The sampler.</returns>
    public static Sampler CreateNearestSampler(Vk vk, Device device)
    {
        var samplerInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Nearest,
            MinFilter = Filter.Nearest,
            AddressModeU = SamplerAddressMode.ClampToEdge,
            AddressModeV = SamplerAddressMode.ClampToEdge,
            AddressModeW = SamplerAddressMode.ClampToEdge,
            AnisotropyEnable = false,
            MaxAnisotropy = 1.0f,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            MipmapMode = SamplerMipmapMode.Nearest,
        };

        Sampler sampler;
        VulkanResult.Ensure(vk.CreateSampler(device, in samplerInfo, null, out sampler), "vkCreateSampler");
        return sampler;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _vk.DestroyImageView(_device, _view, null);
        _vk.DestroyImage(_device, _image, null);
        _vk.FreeMemory(_device, _memory, null);
        _image = default;
        _memory = default;
        _view = default;
    }
}
