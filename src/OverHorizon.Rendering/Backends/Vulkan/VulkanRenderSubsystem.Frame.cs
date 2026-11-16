using System.Numerics;
using OverHorizon.Core;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Threading;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Per-frame command-buffer recording for <see cref="VulkanRenderSubsystem"/>: the sky dome, the opaque chunk
/// pass, the world overlays that must be tinted by translucent terrain, the depth-sorted translucent pass and
/// finally the held viewmodel with the 2D HUD (split out to respect the 500-line architecture budget).
/// </summary>
public sealed unsafe partial class VulkanRenderSubsystem
{
    /// <summary>Minimum number of chunk columns a worker must own before the culling stage is split at all.</summary>
    private const int CullMinColumnsPerWorker = 24;

    /// <summary>Starting (and minimum) capacity of the persistently mapped overlay vertex stream, in bytes.</summary>
    private const ulong OverlayStreamInitialCapacity = 4UL * 1024 * 1024;

    private ulong _overlayPeakBytes = OverlayStreamInitialCapacity;

    /// <inheritdoc />
    public RenderFrameMetrics RenderFrame(FirstPersonCamera camera, float skyBrightness = 1.0f, FrameOverlayState? overlay = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);

        var frustum = Frustum.FromCamera(camera);

        if (_device is not { } device || _pipelines is not { } pipelines || _descriptorSets is not { } sets)
        {
            return SimulateCpuVisibilityPass(in frustum);
        }

        // Every retire performed by this render call (overlay-stream growth) belongs to the frame that is
        // about to record; retires from the update in between keep the stamp written by the previous frame,
        // which is what makes Drain(_frameCount - 2) exact two frames later.
        if (_disposer is not null)
        {
            _disposer.Frame = _frameCount;
        }

        // Every mesh handed to us since the last frame is uploaded in one submission and one wait here, instead
        // of stalling the queue four times per section while chunks stream in.
        _uploads?.Flush();

        var clampedSky = Math.Clamp(skyBrightness, 0.08f, 1.0f);
        var chunkFogDist = overlay is { ChunkFogDistance: > 16.0f } ? overlay.ChunkFogDistance : camera.FarPlane;
        var skyColor = FrameAtmosphere.ComputeSkyAndFogColor(overlay, camera, clampedSky, ref chunkFogDist, out var isNether);
        var underwater = overlay?.IsUnderwater == true;

        if (!device.BeginFrame(new Vector4(skyColor, 1.0f)))
        {
            // The swapchain is being recreated (minimised window, resize): drop this frame, keep the metrics.
            // No frame was submitted for this slot, so an explicit idle proves every earlier frame is done
            // and lets a full drain keep retired buffers from piling up while nothing can be presented.
            device.WaitIdle();
            _disposer?.DrainAll();
            return LastFrameMetrics;
        }

        // This slot's fence has just been waited on, which proves every frame up to _frameCount - 2 has
        // finished on the GPU: everything the disposer stamped by then is dead memory and can be destroyed.
        _disposer?.Drain(_frameCount - 2);

        var vk = device.Api;
        var cmd = device.CurrentCommandBuffer;
        var frameIndex = device.CurrentFrameIndex;

        // The overlay stream is a persistently mapped bump allocator: it is rewound once per frame and may only
        // grow here, before anything has been recorded against it. Each frame in flight owns its own stream, so
        // this rewind never disturbs the frame that is still in flight.
        if (_overlayStreams.Length > 0)
        {
            var overlayStream = _overlayStreams[frameIndex];
            overlayStream.Reset();
            overlayStream.EnsureCapacity(Math.Max(_overlayPeakBytes, OverlayStreamInitialCapacity));
        }

        _overlayPeakBytes = 0;

        var fogStart = chunkFogDist * 0.62f;
        var fogEnd = chunkFogDist * 0.96f;
        if (isNether)
        {
            fogStart = 1.5f;
            fogEnd = chunkFogDist * 0.78f;
        }
        else if (underwater)
        {
            fogStart = 0.8f;
            fogEnd = 13.5f;
        }

        var uniforms = new VulkanFrameUniforms
        {
            ViewProjection = camera.BuildCameraRelativeViewProjectionMatrix(),
            Fog = new Vector4(fogStart, fogEnd, 0.0f, 0.0f),
            Sky = new Vector4(skyColor, clampedSky),
            Lighting = new Vector4(
                overlay?.HeldTorchLightLevel ?? 0.0f,
                isNether ? DimensionRules.GetAmbientLightFloor(DimensionId.Nether) : DimensionRules.GetAmbientLightFloor(DimensionId.Overworld),
                underwater ? 1.0f : 0.0f,
                0.0f),
        };

        uniforms.WriteTo(new Span<byte>(_frameUniforms[frameIndex].Map(), VulkanFrameUniforms.SizeInBytes));
        _frameUniforms[frameIndex].Unmap();

        SetViewportAndScissor(vk, cmd, device.SwapchainExtent);

        var maxHorizDistSq = (chunkFogDist + 24.0f) * (chunkFogDist + 24.0f);

        // Stage 1: cull every resident column, fanning the work out over the worker pool. This is the single
        // biggest per-frame CPU cost on high view distances, and it touches no Vulkan state at all.
        var cullColumns = GetCullColumnSnapshot(out var cullColumnCount);
        var cullWorkers = RenderParallelism.ResolveWorkerCount(
            cullColumnCount,
            CullMinColumnsPerWorker,
            _options.MaxRenderThreads);
        ChunkPassCuller.CullVisibleSections(
            cullColumns,
            cullColumnCount,
            in frustum,
            camera.Position,
            maxHorizDistSq,
            _cullScratch,
            cullWorkers);

        // Stage 2: replay the culled draw list on the render thread, recording into the frame command buffer.
        var skyCalls = DrawSkyDome(camera, overlay, pipelines, sets, cmd);
        var (drawCalls, triangles) = DrawOpaqueList(vk, cmd, pipelines, sets, frameIndex);

        // Clouds, mobs, dropped items and particles are emitted BEFORE the translucent terrain pass: water, ice
        // and Nether Portals then blend over them, so nothing behind a translucent surface reads as "on top".
        var worldOverlayCalls = DrawWorldOverlays(camera, overlay, pipelines, sets, cmd, skyColor, chunkFogDist);
        var (transCalls, transTris) = DrawTranslucentList(vk, cmd, pipelines, sets, frameIndex);
        var screenOverlayCalls = DrawScreenOverlays(camera, overlay, pipelines, sets, cmd);

        device.EndFrame();

        _frameCount++;
        LastFrameMetrics = new RenderFrameMetrics(
            _cullScratch.VisibleCount,
            _cullScratch.CulledCount,
            skyCalls + drawCalls + worldOverlayCalls + transCalls + screenOverlayCalls,
            triangles + transTris,
            _frameCount);
        return LastFrameMetrics;
    }

    /// <summary>
    /// Binds the full-framebuffer dynamic viewport and scissor.
    /// <para>
    /// The view-projection matrices are built for OpenGL's NDC, where <c>+Y</c> points up; Vulkan's framebuffer
    /// space points <c>+Y</c> down, so without a correction the whole scene lands upside down. A negative
    /// viewport height flips the image back and, because it mirrors the framebuffer Y axis, it also restores the
    /// triangle winding — so the pipelines can keep <c>FrontFace.CounterClockwise</c> and cull back faces exactly
    /// like the OpenGL backend did. Depth is untouched: the viewport depth range maps <c>[-1,1]</c> onto
    /// <c>[0,1]</c> linearly, which is the same window depth OpenGL produced.
    /// </para>
    /// </summary>
    /// <param name="vk">Vulkan API.</param>
    /// <param name="cmd">Active command buffer.</param>
    /// <param name="extent">Framebuffer extent in pixels.</param>
    private static void SetViewportAndScissor(Vk vk, CommandBuffer cmd, Extent2D extent)
    {
        var viewport = new Viewport(0.0f, extent.Height, extent.Width, -(float)extent.Height, 0.0f, 1.0f);
        vk.CmdSetViewport(cmd, 0, 1, &viewport);

        var scissor = new Rect2D(new Offset2D(0, 0), extent);
        vk.CmdSetScissor(cmd, 0, 1, &scissor);
    }

    private (int DrawCalls, int Triangles) DrawOpaqueList(
        Vk vk,
        CommandBuffer cmd,
        VulkanPipelines pipelines,
        VulkanDescriptorSets sets,
        uint frameIndex)
    {
        var list = _cullScratch.Opaque;
        if (list.Count == 0)
        {
            return (0, 0);
        }

        vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, pipelines.ChunkOpaque);
        var descriptorSet = sets.ChunkSetFor(frameIndex);
        vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, pipelines.ChunkLayout, 0, 1, &descriptorSet, 0, null);

        var drawCalls = 0;
        var triangles = 0;

        for (var i = 0; i < list.Count; i++)
        {
            var draw = list[i];
            var section = draw.Section;
            if (section.OpaqueIndexCount == 0)
            {
                continue;
            }

            var offset = new Vector4(draw.CameraRelativeOffset, 0.0f);
            vk.CmdPushConstants(
                cmd,
                pipelines.ChunkLayout,
                ShaderStageFlags.VertexBit,
                0,
                VulkanFrameUniforms.SectionPushSize,
                &offset);

            var vertexBuffer = section.OpaqueVertices!.Handle;
            var vertexOffset = 0UL;
            vk.CmdBindVertexBuffers(cmd, 0, 1, &vertexBuffer, &vertexOffset);
            vk.CmdBindIndexBuffer(cmd, section.OpaqueIndices!.Handle, 0, IndexType.Uint32);
            vk.CmdDrawIndexed(cmd, (uint)section.OpaqueIndexCount, 1, 0, 0, 0);

            drawCalls++;
            triangles += section.OpaqueIndexCount / 3;
        }

        return (drawCalls, triangles);
    }

    private (int DrawCalls, int Triangles) DrawTranslucentList(
        Vk vk,
        CommandBuffer cmd,
        VulkanPipelines pipelines,
        VulkanDescriptorSets sets,
        uint frameIndex)
    {
        var list = _cullScratch.Translucent;
        if (list.Count == 0)
        {
            return (0, 0);
        }

        // Back-to-front ordering: the pipeline keeps depth testing but no longer writes depth, so the draw
        // order is the only thing that blends overlapping translucent surfaces correctly.
        list.Sort(static (a, b) => b.DistanceSq.CompareTo(a.DistanceSq));

        vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, pipelines.ChunkTranslucent);
        var descriptorSet = sets.ChunkSetFor(frameIndex);
        vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, pipelines.ChunkLayout, 0, 1, &descriptorSet, 0, null);

        var drawCalls = 0;
        var triangles = 0;

        for (var i = 0; i < list.Count; i++)
        {
            var draw = list[i];
            var section = draw.Section;
            if (section.TranslucentIndexCount == 0)
            {
                continue;
            }

            var offset = new Vector4(draw.CameraRelativeOffset, 0.0f);
            vk.CmdPushConstants(
                cmd,
                pipelines.ChunkLayout,
                ShaderStageFlags.VertexBit,
                0,
                VulkanFrameUniforms.SectionPushSize,
                &offset);

            var vertexBuffer = section.TranslucentVertices!.Handle;
            var vertexOffset = 0UL;
            vk.CmdBindVertexBuffers(cmd, 0, 1, &vertexBuffer, &vertexOffset);
            vk.CmdBindIndexBuffer(cmd, section.TranslucentIndices!.Handle, 0, IndexType.Uint32);
            vk.CmdDrawIndexed(cmd, (uint)section.TranslucentIndexCount, 1, 0, 0, 0);

            drawCalls++;
            triangles += section.TranslucentIndexCount / 3;
        }

        return (drawCalls, triangles);
    }
}
