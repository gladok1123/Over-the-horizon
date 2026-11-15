using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Owns the raw Vulkan 1.1 rendering context: instance, presentation surface, physical and logical device,
/// graphics and presentation queues, the swapchain with its image views, the single colour-plus-depth render
/// pass, one framebuffer per swapchain image, and the per-frame command buffers and sync primitives.
/// </summary>
internal sealed unsafe class VulkanDevice : IDisposable
{
    /// <summary>Number of frames that may be in flight at once (double buffering).</summary>
    public const int FramesInFlight = 2;

    private readonly Vk _vk = Vk.GetApi();
    private KhrSurface _khrSurface;
    private KhrSwapchain _khrSwapchain;

    private Instance _instance;
    private PhysicalDevice _physicalDevice;
    private Device _logicalDevice;
    private SurfaceKHR _surface;
    private Queue _graphicsQueue;
    private Queue _presentQueue;

    private SwapchainKHR _swapchain;
    private Image[] _swapchainImages = [];
    private ImageView[] _swapchainImageViews = [];
    private Framebuffer[] _framebuffers = [];
    private Format _swapchainFormat = Format.B8G8R8A8Srgb;
    private Extent2D _swapchainExtent;

    private RenderPass _renderPass;
    private CommandPool _commandPool;
    private CommandBuffer[] _commandBuffers = [];
    private Semaphore[] _imageAvailable = [];
    private Semaphore[] _renderFinished = [];
    private Fence[] _inFlightFences = [];

    private VulkanImage? _depthImage;
    private uint _graphicsFamilyIndex;
    private uint _presentFamilyIndex;
    private uint _currentFrame;
    private uint _currentImageIndex;
    private bool _frameActive;
    private bool _swapchainOutOfDate;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanDevice"/> class and creates the whole Vulkan context.
    /// </summary>
    /// <param name="surfaceSource">Windowing surface source used to create the presentation surface.</param>
    /// <param name="width">Requested framebuffer width in pixels.</param>
    /// <param name="height">Requested framebuffer height in pixels.</param>
    public VulkanDevice(IVkSurfaceSource surfaceSource, uint width, uint height)
    {
        ArgumentNullException.ThrowIfNull(surfaceSource);

        CreateInstance(surfaceSource);
        CreateSurface(surfaceSource);
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateCommandPool();
        ChooseSurfaceFormat(width, height);
        CreateRenderPass();
        CreateSwapchain();
        CreateSyncObjects();
    }

    /// <summary>Gets the raw Vulkan API bindings.</summary>
    public Vk Api => _vk;

    /// <summary>Gets the logical device.</summary>
    public Device LogicalDevice => _logicalDevice;

    /// <summary>Gets the physical device backing <see cref="LogicalDevice"/>.</summary>
    public PhysicalDevice PhysicalDevice => _physicalDevice;

    /// <summary>Gets the current swapchain image format.</summary>
    public Format SwapchainFormat => _swapchainFormat;

    /// <summary>Gets the current swapchain extent in pixels.</summary>
    public Extent2D SwapchainExtent => _swapchainExtent;

    /// <summary>Gets the single render pass every pipeline is created against.</summary>
    public RenderPass RenderPass => _renderPass;

    /// <summary>Gets the command buffer recording the current frame.</summary>
    public CommandBuffer CurrentCommandBuffer => _commandBuffers[_currentFrame];

    /// <summary>Gets the index of the frame currently being recorded.</summary>
    public uint CurrentFrameIndex => _currentFrame;

    /// <summary>Gets the command pool used for short-lived staging command buffers.</summary>
    public CommandPool Pool => _commandPool;

    /// <summary>Gets the graphics queue.</summary>
    public Queue GraphicsQueue => _graphicsQueue;

    /// <summary>Gets a value indicating whether the swapchain must be rebuilt before the next frame.</summary>
    public bool SwapchainOutOfDate => _swapchainOutOfDate;

    /// <summary>Marks the swapchain as stale so the next <see cref="BeginFrame"/> recreates it.</summary>
    public void InvalidateSwapchain() => _swapchainOutOfDate = true;

    /// <summary>
    /// Acquires the next swapchain image, starts recording the frame command buffer and begins the render pass.
    /// </summary>
    /// <param name="clearColor">Colour the framebuffer is cleared to.</param>
    /// <returns><see langword="false"/> when the frame must be skipped (swapchain recreation or minimised window).</returns>
    public bool BeginFrame(Vector4 clearColor)
    {
        if (_swapchainOutOfDate)
        {
            RecreateSwapchain();
        }

        var fence = _inFlightFences[_currentFrame];
        _vk.WaitForFences(_logicalDevice, 1, in fence, true, ulong.MaxValue);

        var imageIndex = 0u;
        var acquire = _khrSwapchain.AcquireNextImage(
            _logicalDevice,
            _swapchain,
            ulong.MaxValue,
            _imageAvailable[_currentFrame],
            default,
            ref imageIndex);

        if (acquire is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
        {
            _swapchainOutOfDate = true;
            return false;
        }

        if (acquire != Result.Success)
        {
            return false;
        }

        _vk.ResetFences(_logicalDevice, 1, in fence);
        _currentImageIndex = imageIndex;

        var commandBuffer = _commandBuffers[_currentFrame];
        _vk.ResetCommandBuffer(commandBuffer, CommandBufferResetFlags.None);

        var beginInfo = new CommandBufferBeginInfo { SType = StructureType.CommandBufferBeginInfo };
        VulkanResult.Ensure(_vk.BeginCommandBuffer(commandBuffer, in beginInfo), "vkBeginCommandBuffer");

        var clearValues = stackalloc ClearValue[2];
        clearValues[0] = new ClearValue(new ClearColorValue(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W));
        clearValues[1] = new ClearValue { DepthStencil = new ClearDepthStencilValue(1.0f, 0) };

        var renderPassInfo = new RenderPassBeginInfo
        {
            SType = StructureType.RenderPassBeginInfo,
            RenderPass = _renderPass,
            Framebuffer = _framebuffers[imageIndex],
            RenderArea = new Rect2D(new Offset2D(0, 0), _swapchainExtent),
            ClearValueCount = 2,
            PClearValues = clearValues,
        };

        _vk.CmdBeginRenderPass(commandBuffer, in renderPassInfo, SubpassContents.Inline);
        _frameActive = true;
        return true;
    }

    /// <summary>Ends the render pass, submits the frame command buffer and presents the swapchain image.</summary>
    public void EndFrame()
    {
        if (!_frameActive)
        {
            return;
        }

        _frameActive = false;
        var commandBuffer = _commandBuffers[_currentFrame];
        _vk.CmdEndRenderPass(commandBuffer);
        VulkanResult.Ensure(_vk.EndCommandBuffer(commandBuffer), "vkEndCommandBuffer");

        var waitSemaphores = stackalloc Semaphore[1];
        waitSemaphores[0] = _imageAvailable[_currentFrame];
        var signalSemaphores = stackalloc Semaphore[1];
        signalSemaphores[0] = _renderFinished[_currentFrame];
        var waitStages = stackalloc PipelineStageFlags[1];
        waitStages[0] = PipelineStageFlags.ColorAttachmentOutputBit;

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = waitSemaphores,
            PWaitDstStageMask = waitStages,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = signalSemaphores,
        };

        VulkanResult.Ensure(
            _vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]),
            "vkQueueSubmit");

        var swapchains = stackalloc SwapchainKHR[1];
        swapchains[0] = _swapchain;
        var imageIndices = stackalloc uint[1];
        imageIndices[0] = _currentImageIndex;

        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = signalSemaphores,
            SwapchainCount = 1,
            PSwapchains = swapchains,
            PImageIndices = imageIndices,
        };

        var present = _khrSwapchain.QueuePresent(_presentQueue, in presentInfo);
        if (present is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
        {
            _swapchainOutOfDate = true;
        }

        _currentFrame = (_currentFrame + 1) % FramesInFlight;
    }

    /// <summary>Waits for every in-flight frame to finish so resources can be destroyed safely.</summary>
    public void WaitIdle() => _vk.DeviceWaitIdle(_logicalDevice);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        WaitIdle();
        DestroySwapchain();

        for (var i = 0; i < _inFlightFences.Length; i++)
        {
            _vk.DestroyFence(_logicalDevice, _inFlightFences[i], null);
            _vk.DestroySemaphore(_logicalDevice, _renderFinished[i], null);
            _vk.DestroySemaphore(_logicalDevice, _imageAvailable[i], null);
        }

        _vk.DestroyCommandPool(_logicalDevice, _commandPool, null);
        _vk.DestroyRenderPass(_logicalDevice, _renderPass, null);
        _khrSurface.DestroySurface(_instance, _surface, null);
        _vk.DestroyDevice(_logicalDevice, null);
        _vk.DestroyInstance(_instance, null);
    }

    private void CreateInstance(IVkSurfaceSource surfaceSource)
    {
        var provider = surfaceSource.VkSurface
            ?? throw new NotSupportedException("The windowing platform does not provide a Vulkan surface.");

        var requiredExtensions = provider.GetRequiredExtensions(out var requiredCount);
        var extensionNames = stackalloc byte*[requiredCount + 1];
        for (var i = 0u; i < requiredCount; i++)
        {
            extensionNames[i] = requiredExtensions[i];
        }

        var appName = "Over-the-Horizon"u8;
        var engineName = "OverHorizon.Rendering"u8;
        fixed (byte* surfaceExtension = "VK_KHR_surface"u8)
        fixed (byte* appNamePtr = appName)
        fixed (byte* engineNamePtr = engineName)
        {
            extensionNames[requiredCount] = surfaceExtension;

            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = appNamePtr,
                ApplicationVersion = new Version32(0, 14, 0),
                PEngineName = engineNamePtr,
                EngineVersion = new Version32(0, 14, 0),
                ApiVersion = new Version32(1, 1, 0),
            };

            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = requiredCount + 1,
                PpEnabledExtensionNames = extensionNames,
            };

            VulkanResult.Ensure(_vk.CreateInstance(in createInfo, null, out _instance), "vkCreateInstance");
        }

        if (!_vk.TryGetInstanceExtension(_instance, out _khrSurface))
        {
            throw new NotSupportedException("The Vulkan loader does not expose VK_KHR_surface.");
        }
    }

    private void CreateSurface(IVkSurfaceSource surfaceSource)
    {
        var provider = surfaceSource.VkSurface
            ?? throw new NotSupportedException("The windowing platform does not provide a Vulkan surface.");

        _surface = provider.Create<AllocationCallbacks>(_instance.ToHandle(), null).ToSurface();
    }

    private void PickPhysicalDevice()
    {
        var deviceCount = 0u;
        VulkanResult.Ensure(_vk.EnumeratePhysicalDevices(_instance, ref deviceCount, null), "vkEnumeratePhysicalDevices");
        if (deviceCount == 0)
        {
            throw new NotSupportedException("No Vulkan-capable GPU was found on this machine.");
        }

        var devices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* devicesPtr = devices)
        {
            VulkanResult.Ensure(
                _vk.EnumeratePhysicalDevices(_instance, ref deviceCount, devicesPtr),
                "vkEnumeratePhysicalDevices");
        }

        foreach (var candidate in devices)
        {
            if (FindQueueFamilies(candidate, out var graphics, out var present) && SupportsSwapchain(candidate))
            {
                _physicalDevice = candidate;
                _graphicsFamilyIndex = graphics;
                _presentFamilyIndex = present;
                return;
            }
        }

        throw new NotSupportedException("No Vulkan device exposes both a graphics queue and a presentation queue.");
    }

    private bool FindQueueFamilies(PhysicalDevice device, out uint graphicsFamily, out uint presentFamily)
    {
        graphicsFamily = 0;
        presentFamily = 0;
        var queueFamilyCount = 0u;
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

        var families = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* familiesPtr = families)
        {
            _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, familiesPtr);
        }

        var foundGraphics = false;
        var foundPresent = false;
        for (var i = 0u; i < queueFamilyCount; i++)
        {
            if ((families[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
            {
                graphicsFamily = i;
                foundGraphics = true;
            }

            _khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, _surface, out var presentSupported);
            if (presentSupported)
            {
                presentFamily = i;
                foundPresent = true;
            }
        }

        return foundGraphics && foundPresent;
    }

    private bool SupportsSwapchain(PhysicalDevice device)
    {
        var propertyCount = 0u;
        _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref propertyCount, null);

        var properties = new ExtensionProperties[propertyCount];
        fixed (ExtensionProperties* propertiesPtr = properties)
        {
            _vk.EnumerateDeviceExtensionProperties(device, (byte*)null, ref propertyCount, propertiesPtr);
        }

        foreach (var property in properties)
        {
            var name = Marshal.PtrToStringUTF8((nint)property.ExtensionName);
            if (string.Equals(name, "VK_KHR_swapchain", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void CreateLogicalDevice()
    {
        var uniqueFamilies = new HashSet<uint> { _graphicsFamilyIndex, _presentFamilyIndex };
        var queueCreateInfos = stackalloc DeviceQueueCreateInfo[2];
        var queuePriority = 1.0f;
        var index = 0;

        foreach (var family in uniqueFamilies)
        {
            queueCreateInfos[index] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = family,
                QueueCount = 1,
                PQueuePriorities = &queuePriority,
            };

            index++;
        }

        fixed (byte* swapchainExtension = "VK_KHR_swapchain"u8)
        {
            var deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = (uint)index,
                PQueueCreateInfos = queueCreateInfos,
                EnabledExtensionCount = 1,
                PpEnabledExtensionNames = &swapchainExtension,
            };

            VulkanResult.Ensure(
                _vk.CreateDevice(_physicalDevice, in deviceCreateInfo, null, out _logicalDevice),
                "vkCreateDevice");
        }

        if (!_vk.TryGetDeviceExtension(_instance, _logicalDevice, out _khrSwapchain))
        {
            throw new NotSupportedException("The Vulkan device does not expose VK_KHR_swapchain.");
        }

        _vk.GetDeviceQueue(_logicalDevice, _graphicsFamilyIndex, 0, out _graphicsQueue);
        _vk.GetDeviceQueue(_logicalDevice, _presentFamilyIndex, 0, out _presentQueue);
    }

    private void CreateCommandPool()
    {
        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _graphicsFamilyIndex,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
        };

        VulkanResult.Ensure(_vk.CreateCommandPool(_logicalDevice, in poolInfo, null, out _commandPool), "vkCreateCommandPool");
    }

    private void ChooseSurfaceFormat(uint width, uint height)
    {
        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out var capabilities);

        var formatCount = 0u;
        _khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, ref formatCount, null);
        var formats = new SurfaceFormatKHR[Math.Max(1u, formatCount)];
        fixed (SurfaceFormatKHR* formatsPtr = formats)
        {
            _khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, ref formatCount, formatsPtr);
        }

        _swapchainFormat = formats[0].Format;
        foreach (var candidate in formats)
        {
            if (candidate.Format == Format.B8G8R8A8Srgb && candidate.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                _swapchainFormat = candidate.Format;
                break;
            }
        }

        var extent = capabilities.CurrentExtent;
        if (extent.Width == uint.MaxValue || extent.Width == 0)
        {
            extent.Width = Math.Clamp(width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
            extent.Height = Math.Clamp(height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);
        }

        _swapchainExtent = extent;
    }

    private void CreateRenderPass()
    {
        var colorAttachment = new AttachmentDescription
        {
            Format = _swapchainFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr,
        };

        var depthAttachment = new AttachmentDescription
        {
            Format = Format.D32Sfloat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.DepthStencilAttachmentOptimal,
        };

        var colorReference = new AttachmentReference(0, ImageLayout.ColorAttachmentOptimal);
        var depthReference = new AttachmentReference(1, ImageLayout.DepthStencilAttachmentOptimal);

        var subpass = new SubpassDescription
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorReference,
            PDepthStencilAttachment = &depthReference,
        };

        var dependency = new SubpassDependency
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            SrcAccessMask = AccessFlags.None,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit,
        };

        var attachments = stackalloc AttachmentDescription[2];
        attachments[0] = colorAttachment;
        attachments[1] = depthAttachment;

        var renderPassInfo = new RenderPassCreateInfo
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 2,
            PAttachments = attachments,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency,
        };

        VulkanResult.Ensure(_vk.CreateRenderPass(_logicalDevice, in renderPassInfo, null, out _renderPass), "vkCreateRenderPass");
    }

    private void CreateSwapchain()
    {
        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out var capabilities);

        var presentModeCount = 0u;
        _khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, ref presentModeCount, null);
        var presentModes = new PresentModeKHR[Math.Max(1u, presentModeCount)];
        fixed (PresentModeKHR* presentModesPtr = presentModes)
        {
            _khrSurface.GetPhysicalDeviceSurfacePresentModes(_physicalDevice, _surface, ref presentModeCount, presentModesPtr);
        }

        var presentMode = PresentModeKHR.FifoKhr;
        foreach (var candidate in presentModes)
        {
            if (candidate == PresentModeKHR.MailboxKhr)
            {
                presentMode = candidate;
                break;
            }
        }

        var imageCount = capabilities.MinImageCount + 1;
        if (capabilities.MaxImageCount > 0 && imageCount > capabilities.MaxImageCount)
        {
            imageCount = capabilities.MaxImageCount;
        }

        var exclusive = _graphicsFamilyIndex == _presentFamilyIndex;
        var queueFamilyIndices = stackalloc uint[2];
        queueFamilyIndices[0] = _graphicsFamilyIndex;
        queueFamilyIndices[1] = _presentFamilyIndex;

        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface,
            MinImageCount = imageCount,
            ImageFormat = _swapchainFormat,
            ImageColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr,
            ImageExtent = _swapchainExtent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = exclusive ? SharingMode.Exclusive : SharingMode.Concurrent,
            QueueFamilyIndexCount = exclusive ? 0u : 2u,
            PQueueFamilyIndices = queueFamilyIndices,
            PreTransform = capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,
        };

        VulkanResult.Ensure(
            _khrSwapchain.CreateSwapchain(_logicalDevice, in createInfo, null, out _swapchain),
            "vkCreateSwapchainKHR");

        var swapchainImageCount = 0u;
        _khrSwapchain.GetSwapchainImages(_logicalDevice, _swapchain, ref swapchainImageCount, null);
        _swapchainImages = new Image[swapchainImageCount];
        fixed (Image* imagesPtr = _swapchainImages)
        {
            _khrSwapchain.GetSwapchainImages(_logicalDevice, _swapchain, ref swapchainImageCount, imagesPtr);
        }

        _swapchainImageViews = new ImageView[_swapchainImages.Length];
        for (var i = 0; i < _swapchainImages.Length; i++)
        {
            var viewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = _swapchainFormat,
                SubresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
            };

            VulkanResult.Ensure(
                _vk.CreateImageView(_logicalDevice, in viewInfo, null, out _swapchainImageViews[i]),
                "vkCreateImageView");
        }

        _depthImage = VulkanImage.CreateDepthAttachment(
            _vk,
            _logicalDevice,
            _physicalDevice,
            _swapchainExtent.Width,
            _swapchainExtent.Height);

        _framebuffers = new Framebuffer[_swapchainImageViews.Length];
        for (var i = 0; i < _swapchainImageViews.Length; i++)
        {
            var attachments = stackalloc ImageView[2];
            attachments[0] = _swapchainImageViews[i];
            attachments[1] = _depthImage.View;

            var framebufferInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = _renderPass,
                AttachmentCount = 2,
                PAttachments = attachments,
                Width = _swapchainExtent.Width,
                Height = _swapchainExtent.Height,
                Layers = 1,
            };

            VulkanResult.Ensure(
                _vk.CreateFramebuffer(_logicalDevice, in framebufferInfo, null, out _framebuffers[i]),
                "vkCreateFramebuffer");
        }

        _swapchainOutOfDate = false;
    }

    private void CreateSyncObjects()
    {
        _commandBuffers = new CommandBuffer[FramesInFlight];
        var allocateInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = FramesInFlight,
        };

        fixed (CommandBuffer* commandBuffersPtr = _commandBuffers)
        {
            VulkanResult.Ensure(
                _vk.AllocateCommandBuffers(_logicalDevice, in allocateInfo, commandBuffersPtr),
                "vkAllocateCommandBuffers");
        }

        _imageAvailable = new Semaphore[FramesInFlight];
        _renderFinished = new Semaphore[FramesInFlight];
        _inFlightFences = new Fence[FramesInFlight];

        var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
        var fenceInfo = new FenceCreateInfo
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };

        for (var i = 0; i < FramesInFlight; i++)
        {
            VulkanResult.Ensure(
                _vk.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _imageAvailable[i]),
                "vkCreateSemaphore");
            VulkanResult.Ensure(
                _vk.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _renderFinished[i]),
                "vkCreateSemaphore");
            VulkanResult.Ensure(
                _vk.CreateFence(_logicalDevice, in fenceInfo, null, out _inFlightFences[i]),
                "vkCreateFence");
        }
    }

    private void RecreateSwapchain()
    {
        WaitIdle();
        DestroySwapchain();
        ChooseSurfaceFormat(_swapchainExtent.Width, _swapchainExtent.Height);
        CreateSwapchain();
    }

    private void DestroySwapchain()
    {
        foreach (var framebuffer in _framebuffers)
        {
            _vk.DestroyFramebuffer(_logicalDevice, framebuffer, null);
        }

        _depthImage?.Dispose();
        _depthImage = null;

        foreach (var view in _swapchainImageViews)
        {
            _vk.DestroyImageView(_logicalDevice, view, null);
        }

        if (_swapchain.Handle != 0)
        {
            _khrSwapchain.DestroySwapchain(_logicalDevice, _swapchain, null);
            _swapchain = default;
        }

        _framebuffers = [];
        _swapchainImageViews = [];
        _swapchainImages = [];
    }
}
