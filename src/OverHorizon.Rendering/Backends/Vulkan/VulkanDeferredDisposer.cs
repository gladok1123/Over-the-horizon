namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Frame-tagged retirement list for GPU buffers. Chunk streaming replaces and removes sections — and the
/// vertex streams grow — while frames are still in flight, and a buffer the GPU may still be reading must
/// outlive every command buffer that bound it. With two frames in flight a buffer retired while frame
/// <c>T</c> was current can only be destroyed once frame <c>T</c> completes, which happens exactly when
/// <c>BeginFrame(T + 2)</c> returns: the caller stamps <see cref="Frame"/> once per frame and drains
/// everything tagged <c>currentFrame - 2</c> or older right after that fence wait.
/// </summary>
internal sealed class VulkanDeferredDisposer
{
    private readonly List<Pending> _pending = [];

    /// <summary>Gets or sets the frame index the next <see cref="Retire"/> call is stamped with.</summary>
    public long Frame { get; set; }

    /// <summary>
    /// Queues <paramref name="buffers"/> for destruction at the first <see cref="Drain"/> that proves the
    /// GPU can no longer read them. The buffer list is copied, so the caller may clear its own copy right
    /// away (which is exactly what <see cref="VulkanSectionSlot.Dispose"/> does).
    /// </summary>
    /// <param name="arena">Arena that owns the backing memory ranges, or <see langword="null"/> for buffers with their own allocation.</param>
    /// <param name="buffers">Buffers whose destruction must wait for in-flight frames.</param>
    public void Retire(VulkanBufferArena? arena, IReadOnlyList<VulkanBuffer> buffers)
    {
        if (buffers.Count == 0)
        {
            return;
        }

        _pending.Add(new Pending(Frame, arena, [.. buffers]));
    }

    /// <summary>Destroys every entry stamped with a frame less than or equal to <paramref name="maxSafeFrame"/>.</summary>
    /// <param name="maxSafeFrame">Highest retired frame index the GPU has provably finished with.</param>
    public void Drain(long maxSafeFrame)
    {
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].Frame > maxSafeFrame)
            {
                continue;
            }

            Destroy(_pending[i]);
            _pending.RemoveAt(i);
        }
    }

    /// <summary>Destroys everything queued. Only valid once the device has gone completely idle.</summary>
    public void DrainAll()
    {
        foreach (var entry in _pending)
        {
            Destroy(entry);
        }

        _pending.Clear();
    }

    private static void Destroy(in Pending entry)
    {
        foreach (var buffer in entry.Buffers)
        {
            entry.Arena?.Free(buffer.Memory, 0, buffer.AllocatedSize);
            buffer.Dispose();
        }
    }

    private readonly record struct Pending(long Frame, VulkanBufferArena? Arena, VulkanBuffer[] Buffers);
}
