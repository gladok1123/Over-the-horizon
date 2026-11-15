using System.Numerics;
using Silk.NET.OpenGL;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Executes column-culled opaque and depth-sorted translucent chunk drawing passes plus dynamic sky color interpolation.
/// </summary>
internal static class SilkOpenGlChunkPassDrawer
{
    /// <summary>Interpolates the sky colour for the supplied time of day and sky-light level.</summary>
    /// <param name="skyBrightness">Global sky light multiplier.</param>
    /// <param name="worldTimeTicks">Absolute world time in ticks.</param>
    /// <param name="underwater">Whether the camera eye is submerged.</param>
    /// <returns>Linear RGB sky (and fog) colour.</returns>
    public static Vector3 ComputeSkyAtmosphereColor(float skyBrightness, long worldTimeTicks, bool underwater) =>
        SkyAtmosphere.ComputeColor(skyBrightness, worldTimeTicks, underwater);

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
