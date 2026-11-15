using FluentAssertions;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.Rendering.Threading;
using OverHorizon.World.Generation;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

/// <summary>
/// v0.14 phase 1 regression coverage for the work-distribution pass: the shared worker policy, the per-worker
/// staging buffers, the multi-threaded chunk culling stage, and the fact that meshing and drawing stay
/// deterministic (and stay on the render thread) now that the CPU work is fanned out.
/// </summary>
public sealed class V14ThreadedRenderingTests
{
    [Fact]
    public void RenderParallelism_SplitsEveryItemExactlyOnceAndStaysBalanced()
    {
        for (var total = 1; total <= 257; total += 7)
        {
            for (var workers = 1; workers <= 16; workers++)
            {
                var covered = new int[total];
                var maxSize = 0;
                var minSize = int.MaxValue;

                for (var worker = 0; worker < workers; worker++)
                {
                    var (start, end) = RenderParallelism.RangeFor(total, workers, worker);
                    start.Should().BeLessThanOrEqualTo(end);
                    start.Should().BeGreaterThanOrEqualTo(0);
                    end.Should().BeLessThanOrEqualTo(total);

                    for (var i = start; i < end; i++)
                    {
                        covered[i]++;
                    }

                    var size = end - start;
                    maxSize = Math.Max(maxSize, size);
                    minSize = Math.Min(minSize, size);
                }

                covered.Should().OnlyContain(hits => hits == 1, $"total={total} workers={workers} must cover each item once");
                (maxSize - minSize).Should().BeLessThanOrEqualTo(1, $"total={total} workers={workers} must stay balanced");
            }
        }
    }

    [Fact]
    public void RenderParallelism_DegradesToASingleWorkerForTinyWorkloads()
    {
        RenderParallelism.ResolveWorkerCount(0).Should().Be(0);
        RenderParallelism.ResolveWorkerCount(-5).Should().Be(0);
        RenderParallelism.ResolveWorkerCount(1).Should().Be(1);
        RenderParallelism.ResolveWorkerCount(47).Should().Be(1, "a 47-column frame must not pay for a parallel hand-off");
        RenderParallelism.ResolveWorkerCount(480).Should().BeGreaterThan(1);

        RenderParallelism.ResolveWorkerCount(100000, 1, maxThreads: 3).Should().Be(3, "the caller cap wins");
        RenderParallelism.ResolveWorkerCount(100000, 1, maxThreads: 0).Should().BeLessThanOrEqualTo(RenderParallelism.MaxSupportedWorkers);
        RenderParallelism.ResolveWorkerCount(100000, 1, maxThreads: 512).Should().Be(RenderParallelism.MaxSupportedWorkers);
    }

    [Fact]
    public void ListSlice_WindowsTheExactSubRange()
    {
        List<int> source = [10, 20, 30, 40, 50, 60, 70];

        var slice = new ListSlice<int>(source, 2, 3);

        slice.Count.Should().Be(3);
        slice[0].Should().Be(30);
        slice[1].Should().Be(40);
        slice[2].Should().Be(50);
        slice.Should().Equal(30, 40, 50);

        var tail = new ListSlice<int>(source, 5, 99);
        tail.Should().Equal(60, 70);
    }

    [Fact]
    public void ParallelVertexSink_KeepsWorkerBucketsIndependentAndDrainsInOrder()
    {
        var sink = new ParallelVertexSink(4, 8);

        sink.WorkerCount.Should().Be(4);
        sink.Bucket(0).Add(default);
        sink.Bucket(2).Add(default);
        sink.TotalCount.Should().Be(2);

        // Buckets must not alias: writing worker 1 may not disturb worker 0.
        sink.Bucket(1).Add(default);
        sink.Bucket(0).Count.Should().Be(1);

        List<OverlayVertex> destination = [default, default];
        sink.DrainTo(destination).Should().Be(3);
        destination.Should().HaveCount(5);

        sink.Reset();
        sink.TotalCount.Should().Be(0);
    }

    [Fact]
    public void SectionCullScratch_MergesPerWorkerBucketsDeterministically()
    {
        var scratch = new SectionCullScratch(3);

        scratch.OpaqueBucket(0).Add(new CulledSectionDraw(null!, default, 0.0f));
        scratch.OpaqueBucket(1).Add(new CulledSectionDraw(null!, default, 0.0f));
        scratch.TranslucentBucket(2).Add(new CulledSectionDraw(null!, default, 12.0f));

        scratch.AddCounts(0, visible: 4, culled: 1);
        scratch.AddCounts(2, visible: 3, culled: 9);

        scratch.Merge();

        scratch.Opaque.Should().HaveCount(2);
        scratch.Translucent.Should().HaveCount(1);
        scratch.VisibleCount.Should().Be(7);
        scratch.CulledCount.Should().Be(10);

        // Merging twice in a row must not double-count.
        scratch.Merge();
        scratch.Opaque.Should().HaveCount(2);
        scratch.VisibleCount.Should().Be(7);

        scratch.BeginFrame();
        scratch.VisibleCount.Should().Be(7, "counters are only recomputed by Merge");
        scratch.Merge();
        scratch.Opaque.Should().BeEmpty();
        scratch.VisibleCount.Should().Be(0);
        scratch.CulledCount.Should().Be(0);
    }

    [Fact]
    public void ChunkMeshing_ProducesIdenticalOutputWhetherOrNotSectionsAreFannedOut()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var generator = new OverworldGenerator(20261112L, registries);

        var column = new ChunkColumn(new ChunkPos(3, -2));
        generator.GenerateChunk(column);
        var center = column.CreateSnapshot();
        var neighborhood = new ChunkNeighborhood(center);

        var serial = ParallelChunkMesher.MeshColumn(in neighborhood, registries.Blocks, atlas, maxThreads: 1);
        var parallel = ParallelChunkMesher.MeshColumn(in neighborhood, registries.Blocks, atlas);

        serial.Should().NotBeEmpty("the generated column must actually contain geometry");
        parallel.Should().HaveCount(serial.Count);

        for (var i = 0; i < serial.Count; i++)
        {
            parallel[i].SectionPosition.Should().Be(serial[i].SectionPosition, "sections stay bottom-to-top");
            parallel[i].TotalVertexCount.Should().Be(serial[i].TotalVertexCount);
            parallel[i].TotalIndexCount.Should().Be(serial[i].TotalIndexCount);
            parallel[i].OpaqueIndices.ToArray().Should().Equal(serial[i].OpaqueIndices.ToArray());
            parallel[i].TranslucentIndices.ToArray().Should().Equal(serial[i].TranslucentIndices.ToArray());
        }
    }

    [Fact]
    public async Task ChunkMeshing_KeepsABatchDeterministicAcrossRuns()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var generator = new OverworldGenerator(20261112L, registries);

        var batch = new List<ChunkNeighborhood>(2);
        foreach (var pos in new[] { new ChunkPos(0, 0), new ChunkPos(1, 0) })
        {
            var col = new ChunkColumn(pos);
            generator.GenerateChunk(col);
            batch.Add(new ChunkNeighborhood(col.CreateSnapshot()));
        }

        var first = await ParallelChunkMesher.MeshColumnsParallelAsync(batch, registries.Blocks, atlas);
        var second = await ParallelChunkMesher.MeshColumnsParallelAsync(batch, registries.Blocks, atlas);

        first.Should().NotBeEmpty();
        second.Select(m => m.SectionPosition).Should().Equal(first.Select(m => m.SectionPosition), "output stays sorted by SectionPosition");
        second.Sum(m => m.TotalIndexCount).Should().Be(first.Sum(m => m.TotalIndexCount));
    }

    [Fact]
    public void ChunkCullingStage_RunsAcrossWorkersAndDrawsOnTheRenderThread()
    {
        var drawer = ReadSourceFile("src/OverHorizon.Rendering/Backends/SilkOpenGlChunkPassDrawer.cs");

        drawer.Should().Contain("CullVisibleSections(", "culling is a separate, cachable stage");
        drawer.Should().Contain("Parallel.For(", "culling must fan out across the worker pool");
        drawer.Should().Contain("scratch.OpaqueBucket(workerIndex)", "each worker needs its own bucket");
        drawer.Should().Contain("scratch.AddCounts(", "per-worker counters avoid write contention");

        var cullIndex = drawer.IndexOf("CullVisibleSections(", StringComparison.Ordinal);
        var parallelIndex = drawer.IndexOf("Parallel.For(", StringComparison.Ordinal);
        var drawIndex = drawer.IndexOf("DrawOpaqueList(", StringComparison.Ordinal);
        cullIndex.Should().BeGreaterThan(-1);
        parallelIndex.Should().BeGreaterThan(cullIndex, "the fan-out lives inside the culling stage");
        drawIndex.Should().BeGreaterThan(parallelIndex, "OpenGL draws stay in the single-threaded replay stage");

        // No OpenGL call may sneak into the culling body: it runs on worker threads with no current context.
        var cullBody = drawer[parallelIndex..drawIndex];
        cullBody.Should().NotContain("gl.", "worker threads must never touch the GL context");
    }

    [Fact]
    public void RenderFrame_CullsBeforeItDrawsAndKeepsTheV13PassOrder()
    {
        var frame = ReadSourceFile("src/OverHorizon.Rendering/Backends/SilkOpenGlRenderSubsystem.Frame.cs");

        var cullIndex = frame.IndexOf("CullVisibleSections(", StringComparison.Ordinal);
        var opaqueIndex = frame.IndexOf("DrawOpaqueList(", StringComparison.Ordinal);
        var worldIndex = frame.IndexOf("RenderWorldOverlays(", StringComparison.Ordinal);
        var translucentIndex = frame.IndexOf("DrawTranslucentPass(", StringComparison.Ordinal);
        var screenIndex = frame.IndexOf("RenderScreenOverlays(", StringComparison.Ordinal);

        cullIndex.Should().BeGreaterThan(-1, "the frame must cull before it draws");
        cullIndex.Should().BeLessThan(opaqueIndex);
        opaqueIndex.Should().BeLessThan(worldIndex);
        worldIndex.Should().BeLessThan(translucentIndex, "world overlays must be tinted by translucent terrain");
        translucentIndex.Should().BeLessThan(screenIndex, "the HUD and viewmodel draw last");

        frame.Should().Contain("ResolveWorkerCount(", "the culling stage must honour the shared worker policy");
        frame.Should().Contain("GetCullColumnSnapshot(", "workers need a flat, stable snapshot of the resident columns");
    }

    [Fact]
    public void OverlayTessellation_FansOutAcrossWorkersWithoutSharingAList()
    {
        var threading = ReadSourceFile("src/OverHorizon.Rendering/Backends/SilkOpenGlOverlayRenderer.Threading.cs");

        threading.Should().Contain("ParallelAppend<", "entity batches are sliced across workers");
        threading.Should().Contain("AppendWorldCloudsParallel(", "the cloud deck is tessellated per band of rows");
        threading.Should().Contain("sink.Bucket(worker)", "every worker writes into its own bucket");
        threading.Should().Contain("sink.DrainTo(", "buckets are merged back in deterministic worker order");
        threading.Should().NotContain("gl.", "tessellation must stay free of OpenGL state");

        var subsystem = ReadSourceFile("src/OverHorizon.Rendering/Backends/SilkOpenGlRenderSubsystem.cs");
        subsystem.Should().Contain("new SilkOpenGlOverlayRenderer(gl, _options.MaxRenderThreads)", "the overlay stages honour the configured thread cap");
    }

    [Fact]
    public void RenderingOptions_ExposeAThreadCapThatDefaultsToAuto()
    {
        var options = new RenderingOptions();

        options.MaxRenderThreads.Should().Be(0, "0 means 'let RenderParallelism decide'");

        var capped = options with { MaxRenderThreads = 4 };
        capped.MaxRenderThreads.Should().Be(4);
        capped.Headless.Should().BeFalse("adding a knob must not disturb the existing defaults");
        capped.WindowWidth.Should().Be(1280);
    }

    private static string ReadSourceFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OverHorizon.sln")))
            {
                var full = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                File.Exists(full).Should().BeTrue($"source file '{relativePath}' must exist");
                return File.ReadAllText(full);
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate OverHorizon.sln above test base directory.");
    }
}
