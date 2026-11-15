using System.Numerics;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Per-frame draw stages for <see cref="SilkOpenGlOverlayRenderer"/>: the depth-tested world overlay pass that
/// must be tinted by translucent terrain, and the screen-space pass carrying the viewmodel and HUD
/// (split out to respect the 500-line architecture budget).
/// </summary>
internal sealed partial class SilkOpenGlOverlayRenderer
{
    /// <summary>
    /// Draws every depth-tested world overlay (clouds, void floor, selection, mobs, items, particles) for the
    /// frame. This runs <b>before</b> the translucent terrain pass so water, ice, and Nether Portals tint
    /// whatever sits behind them instead of letting clouds and mobs punch through the surface.
    /// </summary>
    /// <param name="camera">Active first-person camera.</param>
    /// <param name="state">Per-frame overlay state.</param>
    /// <param name="blockAtlasTexture">Bound block atlas texture handle.</param>
    /// <param name="entityAtlasTexture">Bound entity atlas texture handle.</param>
    /// <param name="atlasData">Staged atlas lookup for textured quads.</param>
    /// <param name="fogColor">Fog colour shared with the terrain pass.</param>
    /// <param name="chunkFogDist">Chunk fog distance in blocks.</param>
    /// <returns>Number of draw calls issued.</returns>
    public unsafe int RenderWorldOverlays(
        FirstPersonCamera camera,
        FrameOverlayState state,
        uint blockAtlasTexture,
        uint entityAtlasTexture,
        TextureAtlasData? atlasData,
        Vector3 fogColor,
        float chunkFogDist)
    {
        _cloudVertices.Clear();
        _worldTriangleVertices.Clear();
        _worldLineVertices.Clear();

        // The Nether is roofed with Bedrock, so it never shows the Overworld cloud deck.
        if (!state.IsUnderwater && !state.IsNetherDimension && camera.Position.Y > OverHorizon.Core.EngineConstants.WorldMinY - 2.0)
        {
            AppendWorldCloudsParallel(camera.Position, state.WorldTimeTicks, state.CloudDriftOffset, state.CameraSkyLightNormalized);
        }

        if (camera.Position.Y < 24.0)
        {
            AppendVoidAbyssFloor(_worldTriangleVertices, camera.Position);
        }

        if (state.TargetedBlock is { } target)
        {
            var box = state.TargetedSelectionBox ?? BoundingBox.FromBlock(target);
            AppendSelectionWireframe(_worldLineVertices, camera.Position, in box);

            if (state.MiningProgress is > 0.01f and < 1.0f)
            {
                AppendBlockDestroyCracks(_worldTriangleVertices, camera.Position, in box, state.MiningProgress);
            }
        }

        var cameraPos = camera.Position;
        var worldTris = _worldTriangleVertices;

        ParallelAppend(state.RemotePlayers, EntityMinItemsPerWorker, (dst, slice) =>
            WorldMob3DMesher.AppendRemotePlayers(dst, cameraPos, slice));

        if (state.IsThirdPersonCamera && state.LocalThirdPersonPlayer is { } localThirdPerson)
        {
            WorldMob3DMesher.AppendSingleRemotePlayer(worldTris, cameraPos, localThirdPerson);
        }

        ParallelAppend(state.WorldMobs, EntityMinItemsPerWorker, (dst, slice) =>
            WorldMob3DMesher.AppendWorldMobs(dst, cameraPos, slice, atlasData));

        ParallelAppend(state.Minecarts, EntityMinItemsPerWorker, (dst, slice) =>
            WorldMob3DMesher.AppendMinecarts(dst, cameraPos, slice));

        ParallelAppend(state.Arrows, EntityMinItemsPerWorker, (dst, slice) =>
            WorldMob3DMesher.AppendArrows(dst, cameraPos, slice));

        ParallelAppend(state.PrimedTnts, EntityMinItemsPerWorker, (dst, slice) =>
            WorldMob3DMesher.AppendPrimedTnts(dst, cameraPos, slice));

        ParallelAppend(state.FallingBlocks, EntityMinItemsPerWorker, (dst, slice) =>
            WorldMob3DMesher.AppendFallingBlocks(dst, cameraPos, slice));

        ParallelAppend(state.DroppedItems, EntityMinItemsPerWorker, (dst, slice) =>
            WorldMob3DMesher.AppendDroppedItems(dst, cameraPos, slice, atlasData));

        ParallelAppend(state.Particles, ParticleMinItemsPerWorker, (dst, slice) =>
            WorldMob3DMesher.AppendWorldParticles(dst, camera, slice));

        var drawCalls = 0;
        _gl.UseProgram(_program);
        _gl.Uniform1(_locAtlas, 0);
        _gl.Uniform1(_locEntityAtlas, 2);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, blockAtlasTexture);
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, entityAtlasTexture);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var viewProj = camera.BuildCameraRelativeViewProjectionMatrix();
        _gl.UniformMatrix4(_locTransform, 1, false, (float*)&viewProj);

        if (_cloudVertices.Count > 0)
        {
            SetFogUniforms(180.0f, 360.0f, fogColor);
            _gl.DepthMask(false);
            UploadAndDraw(_cloudVertices, PrimitiveType.Triangles);
            _gl.DepthMask(true);
            drawCalls++;
        }

        var entFogStart = state.IsUnderwater ? 0.6f : chunkFogDist * 0.62f;
        var entFogEnd = state.IsUnderwater ? 11.5f : chunkFogDist * 0.98f;
        SetFogUniforms(entFogStart, entFogEnd, fogColor);

        if (_worldTriangleVertices.Count > 0)
        {
            UploadAndDraw(_worldTriangleVertices, PrimitiveType.Triangles);
            drawCalls++;
        }

        if (_worldLineVertices.Count > 0)
        {
            UploadAndDraw(_worldLineVertices, PrimitiveType.Lines);
            drawCalls++;
        }

        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.CullFace);
        _gl.BindVertexArray(0);
        return drawCalls;
    }

    /// <summary>
    /// Draws the first-person viewmodel and every 2D screen-space overlay for the frame. This runs
    /// <b>after</b> the translucent terrain pass, so the hand and the HUD are never washed out by water
    /// or a Nether Portal drawn in front of the camera.
    /// </summary>
    /// <param name="camera">Active first-person camera.</param>
    /// <param name="state">Per-frame overlay state.</param>
    /// <param name="blockAtlasTexture">Bound block atlas texture handle.</param>
    /// <param name="entityAtlasTexture">Bound entity atlas texture handle.</param>
    /// <param name="atlasData">Staged atlas lookup for textured quads.</param>
    /// <returns>Number of draw calls issued.</returns>
    public unsafe int RenderScreenOverlays(
        FirstPersonCamera camera,
        FrameOverlayState state,
        uint blockAtlasTexture,
        uint entityAtlasTexture,
        TextureAtlasData? atlasData)
    {
        _viewModelVertices.Clear();
        if (!state.IsThirdPersonCamera && state.Mode is UiOverlayMode.InGame or UiOverlayMode.ChatInput)
        {
            HeldItem3DMesher.AppendHeldViewmodel(_viewModelVertices, state, atlasData);
        }

        var drawCalls = 0;
        _gl.UseProgram(_program);
        _gl.Uniform1(_locAtlas, 0);
        _gl.Uniform1(_locEntityAtlas, 2);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, blockAtlasTexture);
        _gl.ActiveTexture(TextureUnit.Texture2);
        _gl.BindTexture(TextureTarget.Texture2D, entityAtlasTexture);
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        SetFogUniforms(100000.0f, 200000.0f, Vector3.One);

        if (_viewModelVertices.Count > 0)
        {
            _gl.Clear(ClearBufferMask.DepthBufferBit);
            var handProj = Matrix4x4.CreatePerspectiveFieldOfView(64.0f * (MathF.PI / 180.0f), camera.AspectRatio, 0.05f, 12.0f);
            _gl.UniformMatrix4(_locTransform, 1, false, (float*)&handProj);
            UploadAndDraw(_viewModelVertices, PrimitiveType.Triangles);
            drawCalls++;
        }

        _gl.Disable(EnableCap.Blend);
        _gl.Enable(EnableCap.CullFace);
        _gl.BindVertexArray(0);

        drawCalls += _uiRenderer.RenderUi(camera, state, blockAtlasTexture);

        if (state.ShowInventoryPlayerModel && state.InventoryPlayerPreviewRect.Z > 8.0f && state.InventoryPlayerPreviewRect.W > 8.0f)
        {
            drawCalls += RenderInventoryPlayerPreview(state, blockAtlasTexture, entityAtlasTexture);
        }

        return drawCalls;
    }
}
