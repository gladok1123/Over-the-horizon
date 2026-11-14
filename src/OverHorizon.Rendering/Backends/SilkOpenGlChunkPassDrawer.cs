using System.Numerics;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
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

    public static unsafe (int Visible, int Culled, int DrawCalls, int Triangles) DrawOpaqueAndCollectTranslucent(
        GL gl,
        SilkOpenGlProgramResources prog,
        Dictionary<ChunkPos, GpuChunkColumn> gpuColumns,
        List<(GpuSectionBuffer Buffer, float DistanceSq)> translucentSortScratch,
        in Frustum frustum,
        Vec3d cameraPos,
        float maxHorizDistSq)
    {
        var visible = 0;
        var culled = 0;
        var drawCalls = 0;
        var triangles = 0;

        translucentSortScratch.Clear();
        gl.Disable(EnableCap.Blend);
        gl.DepthMask(true);

        foreach (var col in gpuColumns.Values)
        {
            var mask = col.PresentMask;
            if (mask == 0) continue;

            var dx = (float)((col.ChunkPosition.X * 16.0) + 8.0 - cameraPos.X);
            var dz = (float)((col.ChunkPosition.Z * 16.0) + 8.0 - cameraPos.Z);
            if ((dx * dx) + (dz * dz) > maxHorizDistSq || !frustum.Intersects(col.ActiveWorldBounds))
            {
                culled += BitOperations.PopCount(mask);
                continue;
            }

            while (mask != 0)
            {
                var slot = BitOperations.TrailingZeroCount(mask);
                mask &= mask - 1;
                var section = col.GetSlot(slot);
                if (section is null) continue;

                if (!frustum.Intersects(section.WorldBounds))
                {
                    culled++;
                    continue;
                }

                visible++;
                var relOffset = ComputeCameraRelativeOffset(section.SectionPosition, cameraPos);

                if (section.OpaqueIndexCount > 0)
                {
                    gl.Uniform3(prog.LocSectionOffset, relOffset.X, relOffset.Y, relOffset.Z);
                    gl.BindVertexArray(section.OpaqueVao);
                    gl.DrawElements(PrimitiveType.Triangles, (uint)section.OpaqueIndexCount, DrawElementsType.UnsignedInt, null);
                    drawCalls++;
                    triangles += section.OpaqueIndexCount / 3;
                }

                if (section.TranslucentIndexCount > 0)
                {
                    var centerOffset = relOffset + new Vector3(8.0f, 8.0f, 8.0f);
                    translucentSortScratch.Add((section, centerOffset.LengthSquared()));
                }
            }
        }

        return (visible, culled, drawCalls, triangles);
    }

    public static unsafe (int DrawCalls, int Triangles) DrawTranslucentPass(
        GL gl,
        SilkOpenGlProgramResources prog,
        List<(GpuSectionBuffer Buffer, float DistanceSq)> translucentSortScratch,
        Vec3d cameraPos)
    {
        if (translucentSortScratch.Count == 0)
        {
            return (0, 0);
        }

        translucentSortScratch.Sort(static (a, b) => b.DistanceSq.CompareTo(a.DistanceSq));

        gl.Disable(EnableCap.CullFace);
        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        gl.DepthMask(false);

        var drawCalls = 0;
        var triangles = 0;

        foreach (var (section, _) in translucentSortScratch)
        {
            var relOffset = ComputeCameraRelativeOffset(section.SectionPosition, cameraPos);
            gl.Uniform3(prog.LocSectionOffset, relOffset.X, relOffset.Y, relOffset.Z);
            gl.BindVertexArray(section.TranslucentVao);
            gl.DrawElements(PrimitiveType.Triangles, (uint)section.TranslucentIndexCount, DrawElementsType.UnsignedInt, null);
            drawCalls++;
            triangles += section.TranslucentIndexCount / 3;
        }

        gl.DepthMask(true);
        gl.Disable(EnableCap.Blend);
        gl.Enable(EnableCap.CullFace);
        return (drawCalls, triangles);
    }

    private static Vector3 ComputeCameraRelativeOffset(SectionPos sectionPos, Vec3d cameraPos) =>
        new(
            (float)((sectionPos.X * 16.0) - cameraPos.X),
            (float)((sectionPos.Y * 16.0) - cameraPos.Y),
            (float)((sectionPos.Z * 16.0) - cameraPos.Z));
}
