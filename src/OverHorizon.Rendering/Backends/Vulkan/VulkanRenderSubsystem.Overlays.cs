using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace OverHorizon.Rendering.Backends.Vulkan;

/// <summary>
/// Overlay recording for <see cref="VulkanRenderSubsystem"/>: the celestial sky dome, the depth-tested world
/// overlay batch, the first-person viewmodel, the 2D HUD and the inventory player preview. Every batch is
/// tessellated by the shared <see cref="OverlayGeometry"/> builder and streamed into one persistently mapped
/// host-visible vertex buffer, so the geometry is byte-identical to the OpenGL backend's.
/// </summary>
public sealed unsafe partial class VulkanRenderSubsystem
{
    /// <summary>
    /// Draws the celestial sky dome (Sun, Moon, stars) behind all terrain.
    /// </summary>
    /// <param name="camera">Active first-person camera.</param>
    /// <param name="overlay">Optional per-frame overlay state.</param>
    /// <param name="pipelines">Terrain/overlay pipeline bundle.</param>
    /// <param name="sets">Descriptor set bundle.</param>
    /// <param name="cmd">Active command buffer.</param>
    /// <returns>Number of draw calls issued.</returns>
    private int DrawSkyDome(
        FirstPersonCamera camera,
        FrameOverlayState? overlay,
        VulkanPipelines pipelines,
        VulkanDescriptorSets sets,
        CommandBuffer cmd)
    {
        if (overlay is null || !_overlayGeometry.BuildSkyDome(camera, overlay))
        {
            return 0;
        }

        var skyProj = Matrix4x4.CreatePerspectiveFieldOfView(
            camera.FieldOfViewDegrees * (MathF.PI / 180.0f),
            camera.AspectRatio,
            0.5f,
            1024.0f);
        var skyMvp = camera.ComputeRelativeViewMatrix() * skyProj;

        return DrawOverlayBatch(cmd, pipelines, sets, pipelines.OverlayNoDepth, _overlayGeometry.Sky, skyMvp, 100000.0f, 200000.0f);
    }

    /// <summary>
    /// Draws every depth-tested world overlay (clouds, void floor, selection, mobs, items, particles) before the
    /// translucent terrain pass, so water and Nether Portals tint whatever sits behind them.
    /// </summary>
    /// <param name="camera">Active first-person camera.</param>
    /// <param name="overlay">Optional per-frame overlay state.</param>
    /// <param name="pipelines">Terrain/overlay pipeline bundle.</param>
    /// <param name="sets">Descriptor set bundle.</param>
    /// <param name="cmd">Active command buffer.</param>
    /// <param name="fogColor">Fog colour shared with the terrain pass.</param>
    /// <param name="chunkFogDist">Chunk fog distance in blocks.</param>
    /// <returns>Number of draw calls issued.</returns>
    private int DrawWorldOverlays(
        FirstPersonCamera camera,
        FrameOverlayState? overlay,
        VulkanPipelines pipelines,
        VulkanDescriptorSets sets,
        CommandBuffer cmd,
        Vector3 fogColor,
        float chunkFogDist)
    {
        if (overlay is null)
        {
            return 0;
        }

        _overlayGeometry.BuildWorldPass(camera, overlay, _stagedAtlas);
        _ = fogColor;

        var drawCalls = 0;
        var viewProj = camera.BuildCameraRelativeViewProjectionMatrix();

        if (_overlayGeometry.Clouds.Count > 0)
        {
            // The cloud deck is a sky-height billboard field: it must never occlude terrain behind it.
            drawCalls += DrawOverlayBatch(
                cmd,
                pipelines,
                sets,
                pipelines.OverlayNoDepthWrite,
                _overlayGeometry.Clouds,
                viewProj,
                180.0f,
                360.0f);
        }

        var entFogStart = overlay.IsUnderwater ? 0.6f : chunkFogDist * 0.62f;
        var entFogEnd = overlay.IsUnderwater ? 11.5f : chunkFogDist * 0.98f;

        if (_overlayGeometry.WorldTriangles.Count > 0)
        {
            drawCalls += DrawOverlayBatch(
                cmd,
                pipelines,
                sets,
                pipelines.Overlay,
                _overlayGeometry.WorldTriangles,
                viewProj,
                entFogStart,
                entFogEnd);
        }

        if (_overlayGeometry.WorldLines.Count > 0)
        {
            drawCalls += DrawOverlayBatch(
                cmd,
                pipelines,
                sets,
                pipelines.OverlayLines,
                _overlayGeometry.WorldLines,
                viewProj,
                entFogStart,
                entFogEnd);
        }

        return drawCalls;
    }

    /// <summary>
    /// Draws the first-person viewmodel, the 2D HUD and the inventory player preview after the translucent
    /// terrain pass, so the hand and the interface are never washed out by water or a Nether Portal.
    /// </summary>
    /// <param name="camera">Active first-person camera.</param>
    /// <param name="overlay">Optional per-frame overlay state.</param>
    /// <param name="pipelines">Terrain/overlay pipeline bundle.</param>
    /// <param name="sets">Descriptor set bundle.</param>
    /// <param name="cmd">Active command buffer.</param>
    /// <returns>Number of draw calls issued.</returns>
    private int DrawScreenOverlays(
        FirstPersonCamera camera,
        FrameOverlayState? overlay,
        VulkanPipelines pipelines,
        VulkanDescriptorSets sets,
        CommandBuffer cmd)
    {
        if (overlay is null)
        {
            return 0;
        }

        var drawCalls = 0;
        _overlayGeometry.BuildScreenPass(camera, overlay, _stagedAtlas);

        if (_overlayGeometry.ViewModel.Count > 0)
        {
            // The hand is rendered with a fresh depth buffer and its own narrow projection so it never clips
            // into the world geometry the player is standing against.
            ClearDepthAttachment(cmd);
            var handProj = Matrix4x4.CreatePerspectiveFieldOfView(64.0f * (MathF.PI / 180.0f), camera.AspectRatio, 0.05f, 12.0f);
            drawCalls += DrawOverlayBatch(
                cmd,
                pipelines,
                sets,
                pipelines.Overlay,
                _overlayGeometry.ViewModel,
                handProj,
                100000.0f,
                200000.0f);
        }

        if (_uiRenderer is not null)
        {
            drawCalls += _uiRenderer.RenderUi(cmd, camera, overlay);
        }

        if (overlay.ShowInventoryPlayerModel && overlay.InventoryPlayerPreviewRect.Z > 8.0f && overlay.InventoryPlayerPreviewRect.W > 8.0f)
        {
            drawCalls += DrawInventoryPlayerPreview(overlay, pipelines, sets, cmd);
        }

        return drawCalls;
    }

    private int DrawInventoryPlayerPreview(
        FrameOverlayState overlay,
        VulkanPipelines pipelines,
        VulkanDescriptorSets sets,
        CommandBuffer cmd)
    {
        if (!_overlayGeometry.BuildInventoryPreview(overlay) || _device is not { } device)
        {
            return 0;
        }

        var screenH = overlay.ViewportSize.Y > 64.0f ? overlay.ViewportSize.Y : 720.0f;
        var rect = overlay.InventoryPlayerPreviewRect;

        var vx = (int)MathF.Round(rect.X);
        var vy = (int)MathF.Round(screenH - (rect.Y + rect.W));
        var vw = Math.Max(8, (int)MathF.Round(rect.Z));
        var vh = Math.Max(8, (int)MathF.Round(rect.W));

        var vk = device.Api;
        var viewport = new Viewport(vx, vy, vw, vh, 0.0f, 1.0f);
        vk.CmdSetViewport(cmd, 0, 1, &viewport);

        var scissor = new Rect2D(new Offset2D(vx, vy), new Extent2D((uint)vw, (uint)vh));
        vk.CmdSetScissor(cmd, 0, 1, &scissor);

        ClearDepthAttachment(cmd);

        var aspect = (float)vw / vh;
        var view = Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.06f, 2.85f), new Vector3(0.0f, 0.02f, 0.0f), Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(42.0f * (MathF.PI / 180.0f), aspect, 0.2f, 10.0f);
        var drawCalls = DrawOverlayBatch(
            cmd,
            pipelines,
            sets,
            pipelines.Overlay,
            _overlayGeometry.InventoryPreview,
            view * proj,
            100000.0f,
            200000.0f);

        SetViewportAndScissor(vk, cmd, device.SwapchainExtent);
        return drawCalls;
    }

    private int DrawOverlayBatch(
        CommandBuffer cmd,
        VulkanPipelines pipelines,
        VulkanDescriptorSets sets,
        Pipeline pipeline,
        List<OverlayVertex> vertices,
        Matrix4x4 transform,
        float fogStart,
        float fogEnd)
    {
        if (vertices.Count == 0 || _overlayStream is not { } stream || _device is null)
        {
            return 0;
        }

        if (!stream.TryWrite<OverlayVertex>(CollectionsMarshal.AsSpan(vertices), out var offset))
        {
            return 0;
        }

        _overlayPeakBytes = Math.Max(
            _overlayPeakBytes,
            offset + (ulong)(vertices.Count * Unsafe.SizeOf<OverlayVertex>()));

        var vk = _device.Api;
        vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, pipeline);
        var descriptorSet = sets.OverlaySet;
        vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, pipelines.OverlayLayout, 0, 1, &descriptorSet, 0, null);

        var push = new VulkanOverlayPushConstants(transform, fogStart, fogEnd);
        vk.CmdPushConstants(
            cmd,
            pipelines.OverlayLayout,
            ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            0,
            VulkanOverlayPushConstants.SizeInBytes,
            &push);

        var handle = stream.Handle;
        var bindingOffset = offset;
        vk.CmdBindVertexBuffers(cmd, 0, 1, &handle, &bindingOffset);

        vk.CmdDraw(cmd, (uint)vertices.Count, 1, 0, 0);

        return 1;
    }

    private void ClearDepthAttachment(CommandBuffer cmd)
    {
        if (_device is not { } device)
        {
            return;
        }

        var clearValue = new ClearValue { DepthStencil = new ClearDepthStencilValue(1.0f, 0) };
        var rect = new ClearRect(new Rect2D(new Offset2D(0, 0), device.SwapchainExtent), 0, 1);
        var attachment = new ClearAttachment(ImageAspectFlags.DepthBit, 1, clearValue);
        device.Api.CmdClearAttachments(cmd, 1, &attachment, 1, &rect);
    }
}
