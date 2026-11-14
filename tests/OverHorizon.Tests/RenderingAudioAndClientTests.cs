using System.Net;
using System.Numerics;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Networking;
using OverHorizon.Rendering;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Meshing;
using OverHorizon.Rendering.Textures;
using OverHorizon.Server;
using OverHorizon.World.Generation;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class RenderingAudioAndClientTests
{
    [Fact]
    public void ChunkVertex_HasExact32ByteSequentialGpuLayout()
    {
        Unsafe.SizeOf<ChunkVertex>().Should().Be(32);

        var vertex = new ChunkVertex(new Vector3(1.0f, 2.0f, 3.0f), new Vector2(4.0f, 5.0f), 7.0f, 0.8f, 1.0f);
        vertex.X.Should().Be(1.0f);
        vertex.Y.Should().Be(2.0f);
        vertex.Z.Should().Be(3.0f);
        vertex.U.Should().Be(4.0f);
        vertex.V.Should().Be(5.0f);
        vertex.TileIndex.Should().Be(7.0f);
    }

    [Fact]
    public void ProceduralAtlasGenerator_ProducesDeterministicTilesAndDistinctGrassFaces()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlasA = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var atlasB = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);

        atlasA.Width.Should().Be(ProceduralAtlasGenerator.AtlasDimension);
        atlasA.Height.Should().Be(ProceduralAtlasGenerator.AtlasDimension);
        atlasA.RgbaPixels.Length.Should().Be(ProceduralAtlasGenerator.AtlasDimension * ProceduralAtlasGenerator.AtlasDimension * 4);
        atlasA.RgbaPixels.Span.SequenceEqual(atlasB.RgbaPixels.Span).Should().BeTrue();

        var grassKey = BuiltInBlocks.GrassBlock;
        var topTile = atlasA.GetTileIndex(grassKey, Direction.Up);
        var sideTile = atlasA.GetTileIndex(grassKey, Direction.North);
        var bottomTile = atlasA.GetTileIndex(grassKey, Direction.Down);

        topTile.Should().NotBe(sideTile);
        sideTile.Should().NotBe(bottomTile);
        bottomTile.Should().Be(atlasA.GetTileIndex(BuiltInBlocks.Dirt, Direction.Up));

        // Mod/datapack fallback tile is deterministic and non-transparent.
        var customKey = new ResourceKey("custom_mod", "ruby_ore");
        atlasA.GetTileIndex(customKey, Direction.Up).Should().Be(atlasB.GetTileIndex(customKey, Direction.Up));
    }

    [Fact]
    public void AmbientOcclusionCalculator_MonotonicallyDecreasesWithOccluders()
    {
        AmbientOcclusionCalculator.ComputeVertexAo(false, false, false).Should().Be(3);
        AmbientOcclusionCalculator.ComputeVertexAo(true, false, false).Should().Be(2);
        AmbientOcclusionCalculator.ComputeVertexAo(true, false, true).Should().Be(1);
        AmbientOcclusionCalculator.ComputeVertexAo(true, true, false).Should().Be(0);
        AmbientOcclusionCalculator.ComputeVertexAo(true, true, true).Should().Be(0);

        AmbientOcclusionCalculator.ToBrightnessFactor(0).Should().BeLessThan(AmbientOcclusionCalculator.ToBrightnessFactor(1));
        AmbientOcclusionCalculator.ToBrightnessFactor(1).Should().BeLessThan(AmbientOcclusionCalculator.ToBrightnessFactor(2));
        AmbientOcclusionCalculator.ToBrightnessFactor(2).Should().BeLessThan(AmbientOcclusionCalculator.ToBrightnessFactor(3));
        AmbientOcclusionCalculator.ToBrightnessFactor(3).Should().Be(1.0f);
    }

    [Fact]
    public void GreedySectionMesher_MergesUniform16x16x16SolidCubeIntoSixQuadsAndCullsCrossChunkSeam()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var stone = registries.Blocks.GetDefaultState(BuiltInBlocks.Stone);

        var centerCol = new ChunkColumn(new ChunkPos(0, 0));
        var eastCol = new ChunkColumn(new ChunkPos(1, 0));

        // Fill section index 4 (world Y = 0..15) completely with solid stone in both columns.
        for (var y = 0; y < 16; y++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    _ = centerCol.SetBlockState(x, y, z, stone, registries.Blocks);
                    _ = eastCol.SetBlockState(x, y, z, stone, registries.Blocks);
                }
            }
        }

        var isolated = new ChunkNeighborhood(centerCol.CreateSnapshot());
        var isolatedMesh = GreedySectionMesher.MeshSection(in isolated, sectionIndex: 4, registries.Blocks, atlas);

        // Naive meshing of 16x16x6 outer faces would emit 1,536 quads (6,144 vertices / 9,216 indices).
        // Greedy merging collapses each 16x16 face into a SINGLE quad -> 6 quads (24 vertices / 36 indices)!
        isolatedMesh.IsEmpty.Should().BeFalse();
        isolatedMesh.OpaqueVertices.Length.Should().Be(24);
        isolatedMesh.OpaqueIndices.Length.Should().Be(36);
        isolatedMesh.TranslucentIndices.IsEmpty.Should().BeTrue();

        // Supplying the East neighbor column culls the entire +X boundary slice -> 5 quads (20 vertices / 30 indices)!
        var withEastNeighbor = new ChunkNeighborhood(centerCol.CreateSnapshot(), East: eastCol.CreateSnapshot());
        var culledMesh = GreedySectionMesher.MeshSection(in withEastNeighbor, sectionIndex: 4, registries.Blocks, atlas);

        culledMesh.OpaqueVertices.Length.Should().Be(20);
        culledMesh.OpaqueIndices.Length.Should().Be(30);
    }

    [Fact]
    public void GreedySectionMesher_RoutesWaterIntoTranslucentPassAndCullsInternalFluidSeams()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var water = registries.Blocks.GetDefaultState(BuiltInBlocks.Water);

        var column = new ChunkColumn(new ChunkPos(0, 0));
        _ = column.SetBlockState(4, 0, 4, water, registries.Blocks);
        _ = column.SetBlockState(5, 0, 4, water, registries.Blocks);

        var nb = new ChunkNeighborhood(column.CreateSnapshot());
        var mesh = GreedySectionMesher.MeshSection(in nb, sectionIndex: 4, registries.Blocks, atlas);

        mesh.OpaqueIndices.IsEmpty.Should().BeTrue();
        // 2x1x1 merged water prism has 6 outer merged faces -> 24 translucent vertices, 36 indices.
        mesh.TranslucentVertices.Length.Should().Be(24);
        mesh.TranslucentIndices.Length.Should().Be(36);
    }

    [Fact]
    public async Task ParallelChunkMesher_ProducesIdenticalDeterministicMeshesAcrossRunsAsync()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var generator = new OverworldGenerator(worldSeed: 12345L, registries);

        var neighborhoods = new List<ChunkNeighborhood>(4);
        for (var cx = 0; cx < 2; cx++)
        {
            for (var cz = 0; cz < 2; cz++)
            {
                var col = new ChunkColumn(new ChunkPos(cx, cz));
                generator.GenerateChunk(col);
                neighborhoods.Add(new ChunkNeighborhood(col.CreateSnapshot()));
            }
        }

        var runA = await ParallelChunkMesher.MeshColumnsParallelAsync(neighborhoods, registries.Blocks, atlas)
            .ConfigureAwait(false);
        var runB = await ParallelChunkMesher.MeshColumnsParallelAsync(neighborhoods, registries.Blocks, atlas)
            .ConfigureAwait(false);

        runA.Should().HaveSameCount(runB).And.NotBeEmpty();
        for (var i = 0; i < runA.Count; i++)
        {
            runA[i].SectionPosition.Should().Be(runB[i].SectionPosition);
            runA[i].OpaqueVertices.Span.SequenceEqual(runB[i].OpaqueVertices.Span).Should().BeTrue();
            runA[i].OpaqueIndices.Span.SequenceEqual(runB[i].OpaqueIndices.Span).Should().BeTrue();
            runA[i].TranslucentVertices.Span.SequenceEqual(runB[i].TranslucentVertices.Span).Should().BeTrue();
        }
    }

    [Fact]
    public void FirstPersonCameraAndFrustum_CullsBehindAndFarBoxesEvenAt30MillionBlocks()
    {
        const double extremeX = 29_999_840.0;
        var camera = new FirstPersonCamera
        {
            Position = new Vec3d(extremeX, 80.0, 0.0),
            FarPlane = 256.0f,
        };
        camera.SetRotation(yawDegrees: 0.0f, pitchDegrees: 0.0f); // Facing -Z (North)
        camera.CardinalFacing.Should().Be(Direction.North);

        var frustum = Frustum.FromCamera(camera);

        var boxInFront = new BoundingBox(extremeX - 8.0, 72.0, -48.0, extremeX + 8.0, 88.0, -32.0);
        var boxBehind = new BoundingBox(extremeX - 8.0, 72.0, 32.0, extremeX + 8.0, 88.0, 48.0);
        var boxBeyondFar = new BoundingBox(extremeX - 8.0, 72.0, -400.0, extremeX + 8.0, 88.0, -384.0);

        frustum.Intersects(boxInFront).Should().BeTrue();
        frustum.Intersects(boxBehind).Should().BeFalse();
        frustum.Intersects(boxBeyondFar).Should().BeFalse();
    }

    [Fact]
    public void F3OverlayFormatter_FormatsTelemetryWithInvariantNumbers()
    {
        var snapshot = new F3DebugSnapshot(
            BackendName: "Headless",
            FramesPerSecond: 144.2,
            FrameTimeMilliseconds: 6.93,
            PlayerPosition: new Vec3d(12.5, 68.0, -33.25),
            Facing: Direction.East,
            YawDegrees: 90.0f,
            PitchDegrees: -12.5f,
            Biome: OverHorizon.Core.Biomes.BuiltInBiomes.Plains,
            VisibleSections: 112,
            TotalMeshedSections: 140,
            DrawnVertices: 4800,
            ManagedMemoryBytes: 64L * 1024L * 1024L);

        var formatted = F3OverlayFormatter.Format(in snapshot);
        formatted.Should().Contain("144.2 FPS")
            .And.Contain("XYZ: 12.500 / 68.000 / -33.250")
            .And.Contain("Facing: East")
            .And.Contain("Sections: 112/140");
    }

    [Fact]
    public void RenderBackends_TrackSectionUploadsEvictionsAndVisibilityMetrics()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var stone = registries.Blocks.GetDefaultState(BuiltInBlocks.Stone);

        var frontCol = new ChunkColumn(new ChunkPos(0, -2));
        var behindCol = new ChunkColumn(new ChunkPos(0, 2));
        _ = frontCol.SetBlockState(8, 70, 8, stone, registries.Blocks);
        _ = behindCol.SetBlockState(8, 70, 8, stone, registries.Blocks);

        var frontMesh = GreedySectionMesher.MeshSection(
            new ChunkNeighborhood(frontCol.CreateSnapshot()),
            ChunkColumn.GetSectionIndex(70),
            registries.Blocks,
            atlas);
        var behindMesh = GreedySectionMesher.MeshSection(
            new ChunkNeighborhood(behindCol.CreateSnapshot()),
            ChunkColumn.GetSectionIndex(70),
            registries.Blocks,
            atlas);

        var camera = new FirstPersonCamera { Position = new Vec3d(8.0, 75.0, 0.0) };
        camera.SetRotation(0.0f, 0.0f); // Facing -Z

        IRenderSubsystem[] backends =
        [
            new HeadlessRenderSubsystem(),
            new SilkOpenGlRenderSubsystem(new RenderingOptions(), NullLogger<SilkOpenGlRenderSubsystem>.Instance),
        ];

        foreach (var backend in backends)
        {
            using (backend)
            {
                backend.UploadAtlas(atlas);
                backend.UploadOrReplaceSectionMesh(frontMesh);
                backend.UploadOrReplaceSectionMesh(behindMesh);
                backend.UploadedSectionCount.Should().Be(2);

                var metrics = backend.RenderFrame(camera);
                metrics.VisibleSections.Should().Be(1);
                metrics.CulledSections.Should().Be(1);
                metrics.TrianglesSubmitted.Should().Be(12);

                backend.RemoveChunkMeshes(new ChunkPos(0, -2));
                backend.UploadedSectionCount.Should().Be(1);
            }
        }
    }

    [Fact]
    public void ProceduralSoundBankAndAudioSubsystem_SynthesizeWaveformsAndFilterDistantSources()
    {
        var bank = ProceduralSoundBank.CreateDefaultBank();
        bank.Should().HaveCount(Enum.GetValues<SoundCue>().Length);

        foreach (var clip in bank.Values)
        {
            clip.Samples.Length.Should().BeGreaterThan(500);
            clip.Samples.ToArray().Should().Contain(static s => s != 0);
        }

        using var audio = new OpenAlAudioSubsystem(NullLogger<OpenAlAudioSubsystem>.Instance);
        _ = audio.TryInitializeDevice(); // Succeeds if OpenAL is installed, or falls back silently on CI.

        audio.UpdateListener(new Vec3d(100.0, 64.0, 100.0), -Vector3.UnitZ, Vector3.UnitY, 0.8f);
        audio.PlayPositional(SoundCue.BlockBreak, new Vec3d(102.0, 64.0, 100.0));
        audio.TotalSoundsTriggered.Should().Be(1);

        // Beyond 48-block max audible distance -> culled before voice allocation.
        audio.PlayPositional(SoundCue.BlockPlace, new Vec3d(500.0, 64.0, 500.0));
        audio.TotalSoundsTriggered.Should().Be(1);
    }

    [Fact]
    public void ClientBootstrap_ParseOptions_ParsesMultiplayerAndHeadlessFlags()
    {
        var parsed = ClientBootstrap.ParseOptions(
        [
            "--headless",
            "--max-frames", "24",
            "--connect", "127.0.0.1:25570",
            "--username", "Tester",
            "--view-distance", "2",
        ]);

        parsed.Headless.Should().BeTrue();
        parsed.MaxFrames.Should().Be(24);
        parsed.ServerHost.Should().Be("127.0.0.1");
        parsed.ServerPort.Should().Be(25570);
        parsed.Username.Should().Be("Tester");
        parsed.ViewDistanceChunks.Should().Be(2);
    }

    [Fact]
    public async Task SingleplayerHeadlessClient_StreamsWorldMeshesSectionsAndRendersTrianglesAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var options = new ClientOptions(Headless: true, ViewDistanceChunks: 1);

        using var host = ClientBootstrap.BuildHost([], options);
        await host.StartAsync(cts.Token).ConfigureAwait(false);

        try
        {
            var runner = host.Services.GetRequiredService<ClientSessionRunner>();
            var renderer = host.Services.GetRequiredService<IRenderSubsystem>();

            renderer.BackendName.Should().Be("Headless");

            await runner.InitializeSessionAsync(cts.Token).ConfigureAwait(false);
            runner.Player.Camera.SetRotation(0.0f, -35.0f);

            var metrics = await runner.RunHeadlessFramesAsync(frameCount: 6, moveForward: 1.0f, cancellationToken: cts.Token)
                .ConfigureAwait(false);

            runner.WorldCache.LoadedChunkCount.Should().BeGreaterThanOrEqualTo(9);
            renderer.UploadedSectionCount.Should().BeGreaterThan(0);
            metrics.VisibleSections.Should().BeGreaterThan(0);
            metrics.TrianglesSubmitted.Should().BeGreaterThan(0);
            runner.LatestDebugOverlayText.Should().Contain("OverHorizon");

            await runner.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task MultiplayerHeadlessClient_ConnectsOverTcpReceivesChunksMeshesAndRendersAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var serverOpts = new ServerOptions(
            TicksPerSecond: 100,
            ListenPort: 0,
            PlayerViewDistanceChunks: 1);

        using var serverHost = ServerBootstrap.BuildHost([], serverOpts);
        await serverHost.StartAsync(cts.Token).ConfigureAwait(false);

        try
        {
            var netServer = serverHost.Services.GetRequiredService<INetworkServer>();
            while (netServer.BoundEndPoint is null)
            {
                await Task.Delay(10, cts.Token).ConfigureAwait(false);
            }

            var clientOpts = new ClientOptions(
                Headless: true,
                ServerHost: "127.0.0.1",
                ServerPort: (ushort)netServer.BoundEndPoint.Port,
                Username: "RenderBot",
                ViewDistanceChunks: 1);

            using var clientHost = ClientBootstrap.BuildHost([], clientOpts);
            await clientHost.StartAsync(cts.Token).ConfigureAwait(false);

            try
            {
                var runner = clientHost.Services.GetRequiredService<ClientSessionRunner>();
                var renderer = clientHost.Services.GetRequiredService<IRenderSubsystem>();

                await runner.InitializeSessionAsync(cts.Token).ConfigureAwait(false);
                runner.Player.Camera.SetRotation(0.0f, -40.0f);

                // Pump frames until all 9 server chunks stream over TCP and get meshed into the renderer.
                RenderFrameMetrics metrics = default;
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    metrics = await runner.RunHeadlessFramesAsync(1, cancellationToken: cts.Token).ConfigureAwait(false);
                    if (runner.WorldCache.LoadedChunkCount >= 9 && renderer.UploadedSectionCount > 0 && metrics.TrianglesSubmitted > 0)
                    {
                        break;
                    }

                    await Task.Delay(15, cts.Token).ConfigureAwait(false);
                }

                runner.WorldCache.LoadedChunkCount.Should().Be(9);
                renderer.UploadedSectionCount.Should().BeGreaterThan(0);
                metrics.VisibleSections.Should().BeGreaterThan(0);
                metrics.TrianglesSubmitted.Should().BeGreaterThan(0);

                await runner.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                await clientHost.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            await serverHost.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
