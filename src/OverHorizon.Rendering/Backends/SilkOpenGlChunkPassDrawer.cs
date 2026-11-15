using System.Numerics;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Executes column-culled opaque and depth-sorted translucent chunk drawing passes plus dynamic sky color interpolation.
/// </summary>
internal static class SilkOpenGlChunkPassDrawer
{
    private static readonly Vector3 DaySkyColor = new(0.47f, 0.68f, 0.99f);
    private static readonly Vector3 SunsetSkyColor = new(0.92f, 0.49f, 0.28f);
    private static readonly Vector3 NightSkyColor = new(0.035f, 0.048f, 0.105f);

    public static Vector3 ComputeSkyAtmosphereColor(float skyBrightness, long worldTimeTicks, bool underwater)
    {
        if (underwater)
        {
            return new Vector3(0.03f, 0.21f, 0.44f) * skyBrightness;
        }

        var tod = (int)(((worldTimeTicks % 24000L) + 24000L) % 24000L);
        var sunsetFactor = 0.0f;
        if (tod is >= 11600 and <= 13800)
        {
            sunsetFactor = 1.0f - (MathF.Abs(tod - 12700.0f) / 1100.0f);
        }
        else if (tod is >= 22200 or <= 400)
        {
            var wrapped = tod >= 22200 ? tod - 24000 : tod;
            sunsetFactor = 1.0f - (MathF.Abs(wrapped + 700.0f) / 1100.0f);
        }

        sunsetFactor = Math.Clamp(sunsetFactor, 0.0f, 1.0f);
        var dayMix = Math.Clamp((skyBrightness - 0.14f) / 0.86f, 0.0f, 1.0f);
        var baseCol = Vector3.Lerp(NightSkyColor, DaySkyColor, dayMix);
        if (sunsetFactor > 0.01f)
        {
            baseCol = Vector3.Lerp(baseCol, SunsetSkyColor, sunsetFactor * 0.58f);
        }

        return baseCol;
    }

    /// <summary>
    /// Replays the opaque draw list produced by <see cref="CullVisibleSections"/> on the render thread.
    /// </summary>
    /// <param name="gl">OpenGL context.</param>
    /// <param name="prog">Bound chunk shader resources.</param>
    /// <param name="scratch">Culling scratch buffers holding the merged opaque list.</param>
    /// <returns>Draw-call and triangle counts for telemetry.</returns>
    public static unsafe (int DrawCalls, int Triangles) DrawOpaqueList(
        GL gl,
        SilkOpenGlProgramResources prog,
        SectionCullScratch<GpuSectionBuffer> scratch)
    {
        gl.Disable(EnableCap.Blend);
        gl.DepthMask(true);

        var drawCalls = 0;
        var triangles = 0;
        var list = scratch.Opaque;

        for (var i = 0; i < list.Count; i++)
        {
            var draw = list[i];
            gl.Uniform3(prog.LocSectionOffset, draw.CameraRelativeOffset.X, draw.CameraRelativeOffset.Y, draw.CameraRelativeOffset.Z);
            gl.BindVertexArray(draw.Section.OpaqueVao);
            gl.DrawElements(PrimitiveType.Triangles, (uint)draw.Section.OpaqueIndexCount, DrawElementsType.UnsignedInt, null);
            drawCalls++;
            triangles += draw.Section.OpaqueIndexCount / 3;
        }

        return (drawCalls, triangles);
    }

    /// <summary>
    /// Sorts the translucent draw list back-to-front and replays it on the render thread.
    /// </summary>
    /// <param name="gl">OpenGL context.</param>
    /// <param name="prog">Bound chunk shader resources.</param>
    /// <param name="scratch">Culling scratch buffers holding the merged translucent list.</param>
    /// <returns>Draw-call and triangle counts for telemetry.</returns>
    public static unsafe (int DrawCalls, int Triangles) DrawTranslucentPass(
        GL gl,
        SilkOpenGlProgramResources prog,
        SectionCullScratch<GpuSectionBuffer> scratch)
    {
        if (scratch.Translucent.Count == 0)
        {
            return (0, 0);
        }

        var list = scratch.Translucent;
        list.Sort(static (a, b) => b.DistanceSq.CompareTo(a.DistanceSq));

        gl.Disable(EnableCap.CullFace);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        gl.DepthMask(false);

        var drawCalls = 0;
        var triangles = 0;

        for (var i = 0; i < list.Count; i++)
        {
            var draw = list[i];
            gl.Uniform3(prog.LocSectionOffset, draw.CameraRelativeOffset.X, draw.CameraRelativeOffset.Y, draw.CameraRelativeOffset.Z);
            gl.BindVertexArray(draw.Section.TranslucentVao);
            gl.DrawElements(PrimitiveType.Triangles, (uint)draw.Section.TranslucentIndexCount, DrawElementsType.UnsignedInt, null);
            drawCalls++;
            triangles += draw.Section.TranslucentIndexCount / 3;
        }

        gl.DepthMask(true);
        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.CullFace);
        return (drawCalls, triangles);
    }
}
