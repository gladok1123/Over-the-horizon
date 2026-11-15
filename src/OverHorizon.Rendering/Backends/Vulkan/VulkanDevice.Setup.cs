using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// One-time <see cref="VulkanDevice"/> construction: instance, surface, physical-device selection, queue
/// families, logical device, command pool, surface format, render pass, swapchain and sync objects
/// (split out to respect the 500-line architecture budget).
/// </summary>
internal sealed unsafe partial class VulkanDevice : IDisposable
{
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
}
