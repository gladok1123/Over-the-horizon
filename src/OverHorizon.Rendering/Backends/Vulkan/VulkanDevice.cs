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
