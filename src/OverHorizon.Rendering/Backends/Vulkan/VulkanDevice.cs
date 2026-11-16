using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Contexts;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Owns the raw Vulkan 1.1 rendering context: instance, presentation surface, physical and logical device,
/// graphics and presentation queues, the swapchain with its image views, the single colour-plus-depth render
/// pass, one framebuffer per swapchain image, and the per-frame command buffers and sync primitives.
/// </summary>
internal sealed unsafe partial class VulkanDevice : IDisposable
{
    /// <summary>Number of frames that may be in flight at once (double buffering).</summary>
    public const int FramesInFlight = 2;

    private readonly Vk _vk = Vk.GetApi();
    private KhrSurface _khrSurface = null!;
    private KhrSwapchain _khrSwapchain = null!;

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
    private Format _swapchainFormat = Format.B8G8R8A8Unorm;
    private ColorSpaceKHR _swapchainColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr;
    private Extent2D _swapchainExtent;
    private Format _depthFormat = Format.D32Sfloat;
    private bool _vsync;
    private Extent2D _pendingExtent;

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
    /// <param name="vsync">Whether presentation must block at the display refresh rate (FIFO).</param>
    public VulkanDevice(IVkSurfaceSource surfaceSource, uint width, uint height, bool vsync)
    {
        ArgumentNullException.ThrowIfNull(surfaceSource);
        _vsync = vsync;
        _pendingExtent = new Extent2D(width, height);

        CreateInstance(surfaceSource);
        CreateSurface(surfaceSource);
        PickPhysicalDevice();
        CreateLogicalDevice();
        CreateCommandPool();

        // The format and colour space are chosen exactly once: the render pass, the framebuffers and the
        // image views are all built against them, so a later swapchain rebuild must never change them.
        ChooseSurfaceFormat();
        _depthFormat = ChooseDepthFormat();
        CreateRenderPass();

        // A window launched minimised reports a 0x0 surface: no swapchain can exist until it is restored,
        // so BeginFrame keeps skipping frames (and retries the creation) instead of crashing here.
        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out var capabilities);
        var extent = ResolveSwapchainExtent(capabilities);
        if (extent.Width > 0 && extent.Height > 0)
        {
            _swapchainExtent = extent;
            CreateSwapchain();
        }
        else
        {
            _swapchainOutOfDate = true;
        }

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

    /// <summary>Gets the value indicating whether presentation must block at the display refresh rate.</summary>
    public bool VSync => _vsync;

    /// <summary>
    /// Marks the swapchain as stale and records the framebuffer size the next <see cref="BeginFrame"/>
    /// must rebuild it with (needed on platforms where the surface does not report its own extent).
    /// </summary>
    /// <param name="width">New framebuffer width in pixels.</param>
    /// <param name="height">New framebuffer height in pixels.</param>
    public void InvalidateSwapchain(uint width, uint height)
    {
        _pendingExtent = new Extent2D(width, height);
        _swapchainOutOfDate = true;
    }

    /// <summary>
    /// Acquires the next swapchain image, starts recording the frame command buffer and begins the render pass.
    /// </summary>
    /// <param name="clearColor">Colour the framebuffer is cleared to.</param>
    /// <returns><see langword="false"/> when the frame must be skipped (swapchain recreation or minimised window).</returns>
    public bool BeginFrame(Vector4 clearColor)
    {
        if ((_swapchainOutOfDate || _swapchain.Handle == 0) && !TryRecreateSwapchain())
        {
            // Minimised window (0x0 surface): nothing can be acquired or presented, skip the frame and
            // retry on the next one — the stale swapchain is kept alive untouched.
            return false;
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

        if (acquire == Result.ErrorOutOfDateKhr)
        {
            // A failed acquire hands out neither an image nor a signalled semaphore, so the frame can be
            // abandoned cleanly and retried once the swapchain has been rebuilt.
            _swapchainOutOfDate = true;
            return false;
        }

        if (acquire == Result.SuboptimalKhr)
        {
            // Suboptimal still delivers a valid image AND a signalled semaphore: the frame must run so the
            // submit consumes both (returning here would leave a signalled binary semaphore for the next
            // acquire to trip over); the rebuild is deferred to the next BeginFrame instead.
            _swapchainOutOfDate = true;
        }
        else if (acquire != Result.Success)
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
    private bool TryRecreateSwapchain()
    {
        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(_physicalDevice, _surface, out var capabilities);
        var extent = ResolveSwapchainExtent(capabilities);
        if (extent.Width == 0 || extent.Height == 0)
        {
            // Minimised: the surface cannot back a swapchain right now. Keep the old swapchain (if any)
            // and the out-of-date flag so BeginFrame keeps skipping frames until the window is restored.
            return false;
        }

        WaitIdle();
        DestroySwapchain();
        _swapchainExtent = extent;
        CreateSwapchain();
        return _swapchain.Handle != 0;
    }

    /// <summary>
    /// Resolves the extent the swapchain must be created with: the surface's own extent when it defines
    /// one (possibly 0x0 while minimised), otherwise the last framebuffer size reported by the window,
    /// clamped into the advertised min/max range.
    /// </summary>
    /// <param name="capabilities">Current surface capabilities.</param>
    /// <returns>The resolved extent; width or height may be 0 while the window is minimised.</returns>
    private Extent2D ResolveSwapchainExtent(in SurfaceCapabilitiesKHR capabilities)
    {
        var extent = capabilities.CurrentExtent;
        if (extent.Width != uint.MaxValue || extent.Height != uint.MaxValue)
        {
            return extent;
        }

        return new Extent2D(
            Math.Clamp(_pendingExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
            Math.Clamp(_pendingExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height));
    }

    /// <summary>
    /// Picks the surface format once, for the lifetime of the device. The shaders do their lighting maths
    /// on the raw texel values and the OpenGL backend wrote to a plain UNORM default framebuffer, so the
    /// swapchain must stay UNORM too: an sRGB format would apply a linear->sRGB encode on write and wash
    /// the whole scene out. The colour space is taken from the same advertised entry instead of being
    /// assumed.
    /// </summary>
    private void ChooseSurfaceFormat()
    {
        var formatCount = 0u;
        _khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, ref formatCount, null);
        var formats = new SurfaceFormatKHR[Math.Max(1u, formatCount)];
        fixed (SurfaceFormatKHR* formatsPtr = formats)
        {
            _khrSurface.GetPhysicalDeviceSurfaceFormats(_physicalDevice, _surface, ref formatCount, formatsPtr);
        }

        if (formats.Length == 1 && formats[0].Format == Format.Undefined)
        {
            // A single VK_FORMAT_UNDEFINED entry means "any format works": the spec prescribes this pair.
            _swapchainFormat = Format.B8G8R8A8Unorm;
            _swapchainColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr;
            return;
        }

        var chosen = formats[0];
        foreach (var candidate in formats)
        {
            if (candidate.Format is Format.B8G8R8A8Unorm or Format.R8G8B8A8Unorm)
            {
                chosen = candidate;
                break;
            }
        }

        _swapchainFormat = chosen.Format;
        _swapchainColorSpace = chosen.ColorSpace;
    }

    /// <summary>Chooses a depth format the GPU can attach, preferring full float precision.</summary>
    /// <returns>The selected depth (or depth-stencil) format.</returns>
    private Format ChooseDepthFormat()
    {
        Span<Format> candidates = [Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint, Format.D16Unorm];
        foreach (var candidate in candidates)
        {
            _vk.GetPhysicalDeviceFormatProperties(_physicalDevice, candidate, out var properties);
            if (properties.OptimalTilingFeatures.HasFlag(FormatFeatureFlags.DepthStencilAttachmentBit))
            {
                return candidate;
            }
        }

        // The spec guarantees at least one of the candidates supports depth attachment; this is a
        // defensive fallback that must never be reached in practice.
        return Format.D16Unorm;
    }

    /// <summary>
    /// Picks the presentation mode: FIFO while VSync is on (the spec guarantees it exists and it blocks
    /// at the display rate, which is exactly what <c>swapInterval 1</c> did in OpenGL). With VSync off it
    /// prefers Mailbox (uncapped but tear-free), then Immediate (uncapped, tearing allowed), FIFO last.
    /// </summary>
    /// <param name="presentModes">Modes the surface advertises.</param>
    /// <returns>The mode the swapchain must be created with.</returns>
    private PresentModeKHR ChoosePresentMode(ReadOnlySpan<PresentModeKHR> presentModes)
    {
        if (_vsync)
        {
            return PresentModeKHR.FifoKhr;
        }

        foreach (var candidate in presentModes)
        {
            if (candidate == PresentModeKHR.MailboxKhr)
            {
                return PresentModeKHR.MailboxKhr;
            }
        }

        foreach (var candidate in presentModes)
        {
            if (candidate == PresentModeKHR.ImmediateKhr)
            {
                return PresentModeKHR.ImmediateKhr;
            }
        }

        return PresentModeKHR.FifoKhr;
    }

    /// <summary>
    /// Picks the first alpha-composition mode the surface supports, preferring opaque. Drivers advertise
    /// different subsets (straight/pre-multiplied/inherit are common where opaque is missing), and picking
    /// an unsupported mode is a validation error that can leave the window fully transparent.
    /// </summary>
    /// <param name="capabilities">Current surface capabilities.</param>
    /// <returns>The composite alpha mode to request.</returns>
    private static CompositeAlphaFlagsKHR ChooseCompositeAlpha(in SurfaceCapabilitiesKHR capabilities)
    {
        CompositeAlphaFlagsKHR[] candidates =
        [
            CompositeAlphaFlagsKHR.OpaqueBitKhr,
            CompositeAlphaFlagsKHR.PreMultipliedBitKhr,
            CompositeAlphaFlagsKHR.StraightBitKhr,
            CompositeAlphaFlagsKHR.InheritBitKhr,
        ];

        foreach (var candidate in candidates)
        {
            if (capabilities.SupportedCompositeAlpha.HasFlag(candidate))
            {
                return candidate;
            }
        }

        // The spec guarantees at least one bit is set; keep the conventional default if a driver lies.
        return CompositeAlphaFlagsKHR.OpaqueBitKhr;
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
