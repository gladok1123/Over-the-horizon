using Silk.NET.Vulkan;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// The small set of Vulkan memory helpers every resource in the backend needs: memory-type selection,
/// one-shot command buffers, buffer copies and image layout transitions.
/// </summary>
internal static class VulkanMemory
{
    /// <summary>
    /// Selects the first memory type matching <paramref name="typeFilter"/> and <paramref name="properties"/>.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="physicalDevice">Physical device owning the memory heaps.</param>
    /// <param name="typeFilter">Bit mask of acceptable memory type indices.</param>
    /// <param name="properties">Required memory property flags.</param>
    /// <returns>The selected memory type index.</returns>
    public static uint FindMemoryType(Vk vk, PhysicalDevice physicalDevice, uint typeFilter, MemoryPropertyFlags properties)
    {
        vk.GetPhysicalDeviceMemoryProperties(physicalDevice, out var memoryProperties);
        for (var i = 0u; i < memoryProperties.MemoryTypeCount; i++)
        {
            var type = memoryProperties.MemoryTypes[i];
            if ((typeFilter & (1u << (int)i)) != 0 && (type.PropertyFlags & properties) == properties)
            {
                return i;
            }
        }

        throw new InvalidOperationException("No Vulkan memory type satisfies the requested properties.");
    }

    /// <summary>
    /// Begins a short-lived command buffer used for upload and layout-transition work.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="commandPool">Command pool to allocate from.</param>
    /// <returns>The command buffer, already in the recording state.</returns>
    public static unsafe CommandBuffer BeginOneShotCommands(Vk vk, Device device, CommandPool commandPool)
    {
        var allocateInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = commandPool,
            CommandBufferCount = 1,
        };

        CommandBuffer commandBuffer;
        VulkanResult.Ensure(
            vk.AllocateCommandBuffers(device, in allocateInfo, &commandBuffer),
            "vkAllocateCommandBuffers");

        var beginInfo = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        VulkanResult.Ensure(vk.BeginCommandBuffer(commandBuffer, in beginInfo), "vkBeginCommandBuffer");
        return commandBuffer;
    }

    /// <summary>
    /// Submits and waits for a one-shot command buffer allocated by <see cref="BeginOneShotCommands"/>.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="commandPool">Command pool the buffer came from.</param>
    /// <param name="queue">Queue to submit to.</param>
    /// <param name="commandBuffer">Command buffer to finish.</param>
    public static unsafe void EndOneShotCommands(
        Vk vk,
        Device device,
        CommandPool commandPool,
        Queue queue,
        CommandBuffer commandBuffer)
    {
        VulkanResult.Ensure(vk.EndCommandBuffer(commandBuffer), "vkEndCommandBuffer");

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };

        VulkanResult.Ensure(vk.QueueSubmit(queue, 1, in submitInfo, default), "vkQueueSubmit");
        VulkanResult.Ensure(vk.QueueWaitIdle(queue), "vkQueueWaitIdle");
        vk.FreeCommandBuffers(device, commandPool, 1, &commandBuffer);
    }

    /// <summary>
    /// Copies <paramref name="size"/> bytes from <paramref name="source"/> to <paramref name="destination"/>.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="commandPool">Command pool used for the one-shot copy.</param>
    /// <param name="queue">Queue used for the one-shot copy.</param>
    /// <param name="source">Staging buffer.</param>
    /// <param name="destination">Device-local buffer.</param>
    /// <param name="size">Number of bytes to copy.</param>
    public static void CopyBuffer(
        Vk vk,
        Device device,
        CommandPool commandPool,
        Queue queue,
        Buffer source,
        Buffer destination,
        ulong size)
    {
        var commandBuffer = BeginOneShotCommands(vk, device, commandPool);
        var region = new BufferCopy(0, 0, size);
        vk.CmdCopyBuffer(commandBuffer, source, destination, 1, &region);
        EndOneShotCommands(vk, device, commandPool, queue, commandBuffer);
    }

    /// <summary>
    /// Transitions <paramref name="image"/> between two layouts with a full pipeline barrier.
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="device">Logical device.</param>
    /// <param name="commandPool">Command pool used for the one-shot transition.</param>
    /// <param name="queue">Queue used for the one-shot transition.</param>
    /// <param name="image">Image to transition.</param>
    /// <param name="oldLayout">Layout the image is currently in.</param>
    /// <param name="newLayout">Layout the image must end up in.</param>
    /// <param name="aspectFlags">Image aspects covered by the transition.</param>
    public static void TransitionLayout(
        Vk vk,
        Device device,
        CommandPool commandPool,
        Queue queue,
        Image image,
        ImageLayout oldLayout,
        ImageLayout newLayout,
        ImageAspectFlags aspectFlags)
    {
        var commandBuffer = BeginOneShotCommands(vk, device, commandPool);

        var sourceStage = PipelineStageFlags.AllGraphicsBit;
        var destinationStage = PipelineStageFlags.AllGraphicsBit;
        var sourceAccess = AccessFlags.None;
        var destinationAccess = AccessFlags.None;

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.TransferBit;
            sourceAccess = AccessFlags.None;
            destinationAccess = AccessFlags.TransferWriteBit;
        }
        else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.FragmentShaderBit;
            sourceAccess = AccessFlags.TransferWriteBit;
            destinationAccess = AccessFlags.ShaderReadBit;
        }

        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcAccessMask = sourceAccess,
            DstAccessMask = destinationAccess,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange(aspectFlags, 0, 1, 0, 1),
        };

        vk.CmdPipelineBarrier(
            commandBuffer,
            sourceStage,
            destinationStage,
            DependencyFlags.None,
            0,
            null,
            0,
            null,
            1,
            &barrier);

        EndOneShotCommands(vk, device, commandPool, queue, commandBuffer);
    }
}
