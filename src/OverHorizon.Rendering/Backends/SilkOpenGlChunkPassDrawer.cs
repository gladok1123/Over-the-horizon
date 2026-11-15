using System.Numerics;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Threading;
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
    /// Runs the frustum + distance culling stage over every resident chunk column, optionally across several
    /// worker threads, and fills <paramref name="scratch"/> with the draw lists the render thread then replays.
    /// Culling is pure arithmetic with no OpenGL calls, so it is safe (and profitable) to fan out.
    /// </summary>
    /// <param name="columns">Snapshot array of every resident chunk column.</param>
    /// <param name="columnCount">Number of valid entries in <paramref name="columns"/>.</param>
    /// <param name="frustum">Camera frustum used for the visibility tests.</param>
    /// <param name="cameraPos">Camera world position.</param>
    /// <param name="maxHorizDistSq">Squared horizontal render distance cutoff.</param>
    /// <param name="scratch">Per-worker staging buffers that receive the results.</param>
    /// <param name="workerCount">Number of workers to use (<c>1</c> keeps the job on the calling thread).</param>
    public static void CullVisibleSections(
        GpuChunkColumn[] columns,
        int columnCount,
        in Frustum frustum,
        Vec3d cameraPos,
        float maxHorizDistSq,
        SectionCullScratch scratch,
        int workerCount)
    {
        scratch.BeginFrame();

        // An `in` parameter cannot be captured by a lambda, so the frustum is copied into a local first.
        var cullFrustum = frustum;
        var workers = Math.Clamp(workerCount, 1, scratch.WorkerCount);
        if (workers <= 1)
        {
            CullColumnRange(columns, 0, columnCount, in cullFrustum, cameraPos, maxHorizDistSq, scratch, 0);
        }
        else
        {
            Parallel.For(0, workers, worker =>
                CullColumnRange(columns, 0, columnCount, in cullFrustum, cameraPos, maxHorizDistSq, scratch, worker, workers));
        }

        scratch.Merge();
    }

    private static void CullColumnRange(
        GpuChunkColumn[] columns,
        int offset,
        int count,
        in Frustum frustum,
        Vec3d cameraPos,
        float maxHorizDistSq,
        SectionCullScratch scratch,
        int workerIndex,
        int workerCount = 1)
    {
        var (start, end) = RenderParallelism.RangeFor(count, workerCount, workerIndex);
        var opaque = scratch.OpaqueBucket(workerIndex);
        var translucent = scratch.TranslucentBucket(workerIndex);
        var visible = 0;
        var culled = 0;

        for (var i = start + offset; i < end + offset; i++)
        {
            var col = columns[i];
            var mask = col.PresentMask;
            if (mask == 0)
            {
                continue;
            }

            var dx = (float)((col.ChunkPosition.X * 16.0) + 8.0 - cameraPos.X);
            var dz = (float)((col.ChunkPosition.Z * 16.0) + 8.0 - cameraPos.Z);
            if (((dx * dx) + (dz * dz)) > maxHorizDistSq || !frustum.Intersects(col.ActiveWorldBounds))
            {
                culled += BitOperations.PopCount(mask);
                continue;
            }

            while (mask != 0)
            {
                var slot = BitOperations.TrailingZeroCount(mask);
                mask &= mask - 1;
                var section = col.GetSlot(slot);
                if (section is null)
                {
                    continue;
                }

                if (!frustum.Intersects(section.WorldBounds))
                {
                    culled++;
                    continue;
                }

                visible++;
                var relOffset = ComputeCameraRelativeOffset(section.SectionPosition, cameraPos);

                if (section.OpaqueIndexCount > 0)
                {
                    opaque.Add(new CulledSectionDraw(section, relOffset, 0.0f));
                }

                if (section.TranslucentIndexCount > 0)
                {
                    var center = relOffset + new Vector3(8.0f, 8.0f, 8.0f);
                    translucent.Add(new CulledSectionDraw(section, relOffset, center.LengthSquared()));
                }
            }
        }

        scratch.AddCounts(workerIndex, visible, culled);
    }

    /// <summary>
    /// Replays the opaque draw list produced by <see cref="CullVisibleSections"/> on the render thread.
    /// </summary>
    /// <param name="gl">OpenGL context.</param>
    /// <param name="prog">Bound chunk shader resources.</param>
    /// <param name="scratch">Scratch buffers holding the merged opaque list.</param>
    /// <returns>Draw-call and triangle counts for telemetry.</returns>
    public static unsafe (int DrawCalls, int Triangles) DrawOpaqueList(
        GL gl,
        SilkOpenGlProgramResources prog,
        SectionCullScratch scratch)
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
    /// <param name="scratch">Scratch buffers holding the merged translucent list.</param>
    /// <returns>Draw-call and triangle counts for telemetry.</returns>
    public static unsafe (int DrawCalls, int Triangles) DrawTranslucentPass(
        GL gl,
        SilkOpenGlProgramResources prog,
        SectionCullScratch scratch)
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

    private static Vector3 ComputeCameraRelativeOffset(SectionPos sectionPos, Vec3d cameraPos) =>
        new(
            (float)((sectionPos.X * 16.0) - cameraPos.X),
            (float)((sectionPos.Y * 16.0) - cameraPos.Y),
            (float)((sectionPos.Z * 16.0) - cameraPos.Z));
}
