using OverHorizon.Core;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Per-frame drawing for <see cref="SilkOpenGlRenderSubsystem"/>: the sky dome, the opaque chunk pass, the
/// world overlays that must be tinted by translucent terrain, the depth-sorted translucent pass itself, and
/// finally the held viewmodel with the 2D HUD (split out to respect the 500-line architecture budget).
/// </summary>
public sealed partial class SilkOpenGlRenderSubsystem
{
    /// <inheritdoc />
    public unsafe RenderFrameMetrics RenderFrame(FirstPersonCamera camera, float skyBrightness = 1.0f, FrameOverlayState? overlay = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);

        var frustum = Frustum.FromCamera(camera);

        if (_gl is null || _program is null)
        {
            return SimulateCpuVisibilityPass(in frustum);
        }

        var gl = _gl;
        var prog = _program;
        var clampedSky = Math.Clamp(skyBrightness, 0.08f, 1.0f);
        var chunkFogDist = overlay is { ChunkFogDistance: > 16.0f } ? overlay.ChunkFogDistance : camera.FarPlane;
        var skyColor = ComputeSkyAndFogColor(overlay, camera, clampedSky, ref chunkFogDist, out var isNether);
        var underwater = overlay?.IsUnderwater == true;

        gl.ClearColor(skyColor.X, skyColor.Y, skyColor.Z, 1.0f);
        gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        var skyCalls = (overlay is not null && _overlayRenderer is not null) ? _overlayRenderer.RenderSkyDome(camera, overlay) : 0;

        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Lequal);
        gl.Enable(EnableCap.CullFace);
        gl.CullFace(TriangleFace.Back);
        gl.FrontFace(FrontFaceDirection.Ccw);
        gl.UseProgram(prog.ProgramHandle);

        var viewProj = camera.BuildCameraRelativeViewProjectionMatrix();
        gl.UniformMatrix4(prog.LocViewProjection, 1, false, (float*)&viewProj);
        gl.Uniform1(prog.LocAtlas, 0);
        gl.Uniform1(prog.LocSkyBrightness, clampedSky);
        gl.Uniform1(prog.LocHeldTorchLight, overlay?.HeldTorchLightLevel ?? 0.0f);
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

        gl.Uniform1(prog.LocFogStart, fogStart);
        gl.Uniform1(prog.LocFogEnd, fogEnd);
        gl.Uniform3(prog.LocSkyColor, skyColor.X, skyColor.Y, skyColor.Z);
        gl.Uniform1(prog.LocUnderwater, underwater ? 1.0f : 0.0f);
        gl.Uniform1(prog.LocAmbientFloor, isNether ? DimensionRules.GetAmbientLightFloor(DimensionId.Nether) : DimensionRules.GetAmbientLightFloor(DimensionId.Overworld));

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, prog.AtlasTexture);

        var maxHorizDistSq = (chunkFogDist + 24.0f) * (chunkFogDist + 24.0f);
        var (visible, culled, drawCalls, triangles) = SilkOpenGlChunkPassDrawer.DrawOpaqueAndCollectTranslucent(gl, prog, _gpuColumns, _translucentSortScratch, in frustum, camera.Position, maxHorizDistSq);

        // Clouds, mobs, dropped items and particles are emitted BEFORE the translucent terrain pass: water, ice
        // and Nether Portals then blend over them, so nothing behind a translucent surface reads as "on top".
        var worldOverlayCalls = (overlay is not null && _overlayRenderer is not null)
            ? _overlayRenderer.RenderWorldOverlays(camera, overlay, prog.AtlasTexture, prog.EntityAtlasTexture, _stagedAtlas, skyColor, chunkFogDist)
            : 0;

        gl.UseProgram(prog.ProgramHandle);
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, prog.AtlasTexture);

        var (transCalls, transTris) = SilkOpenGlChunkPassDrawer.DrawTranslucentPass(gl, prog, _translucentSortScratch, camera.Position);
        var screenOverlayCalls = (overlay is not null && _overlayRenderer is not null)
            ? _overlayRenderer.RenderScreenOverlays(camera, overlay, prog.AtlasTexture, prog.EntityAtlasTexture, _stagedAtlas)
            : 0;

        gl.BindVertexArray(0);
        _frameCount++;
        LastFrameMetrics = new RenderFrameMetrics(visible, culled, skyCalls + drawCalls + worldOverlayCalls + transCalls + screenOverlayCalls, triangles + transTris, _frameCount);
        return LastFrameMetrics;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        ReleaseGpuResources();
        _stagedColumns.Clear();
        _stagedSectionCount = 0;
        _stagedAtlas = null;
        _stagedEntityAtlas = null;
        _disposed = true;
    }
}
