using System.Numerics;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Camera;
using OverHorizon.Rendering.Hud;
using OverHorizon.Rendering.Textures;
using OverHorizon.Rendering.Threading;

namespace OverHorizon.Rendering.Backends;

/// <summary>
/// Backend-agnostic per-frame overlay geometry: the sky dome, clouds, void floor, selection wireframe, mobs,
/// drops, particles, the held viewmodel and the inventory player preview. Everything is tessellated into plain
/// <see cref="OverlayVertex"/> lists (entity batches and the cloud deck across the worker pool), so the OpenGL
/// and Vulkan renderers upload byte-identical geometry.
/// </summary>
internal sealed class OverlayGeometry
{
    /// <summary>Minimum number of entities a worker must own before an entity batch is split.</summary>
    private const int EntityMinItemsPerWorker = 24;

    /// <summary>Minimum number of particles a worker must own before the particle batch is split.</summary>
    private const int ParticleMinItemsPerWorker = 48;

    /// <summary>Minimum number of cloud grid rows a worker must own before the cloud deck is split.</summary>
    private const int CloudMinRowsPerWorker = 3;

    private readonly ParallelVertexSink _worldSink = new(RenderParallelism.AutoWorkerCount, 1024);
    private readonly ParallelVertexSink _cloudSink = new(RenderParallelism.AutoWorkerCount, 768);
    private readonly int _maxRenderThreads;

    /// <summary>
    /// Initializes a new instance of the <see cref="OverlayGeometry"/> class.
    /// </summary>
    /// <param name="maxRenderThreads">
    /// Worker cap for the tessellation stages (<c>0</c> selects <see cref="RenderParallelism.AutoWorkerCount"/>).
    /// </param>
    public OverlayGeometry(int maxRenderThreads = 0) => _maxRenderThreads = maxRenderThreads;

    /// <summary>Gets the celestial sky dome vertices.</summary>
    public List<OverlayVertex> Sky { get; } = new(768);

    /// <summary>Gets the cloud deck vertices.</summary>
    public List<OverlayVertex> Clouds { get; } = new(512);

    /// <summary>Gets the world-space triangle vertices (mobs, drops, particles, void floor).</summary>
    public List<OverlayVertex> WorldTriangles { get; } = new(12288);

    /// <summary>Gets the world-space line vertices (block selection wireframe).</summary>
    public List<OverlayVertex> WorldLines { get; } = new(128);

    /// <summary>Gets the first-person viewmodel vertices.</summary>
    public List<OverlayVertex> ViewModel { get; } = new(4096);

    /// <summary>Gets the inventory player preview vertices.</summary>
    public List<OverlayVertex> InventoryPreview { get; } = new(1024);

    /// <summary>
    /// Tessellates the celestial sky dome for <paramref name="state"/>.
    /// </summary>
    /// <param name="camera">Active first-person camera.</param>
    /// <param name="state">Per-frame overlay state.</param>
    /// <returns><see langword="true"/> when the dome produced any geometry.</returns>
    public bool BuildSkyDome(FirstPersonCamera camera, FrameOverlayState state)
    {
        if (state.IsUnderwater || state.IsNetherDimension)
        {
            return false;
        }

        if (camera.Position.Y < OverHorizon.Core.EngineConstants.WorldMinY + 1.0 ||
            (camera.Position.Y < 44.0 && state.CameraSkyLightNormalized < 0.05f))
        {
            return false;
        }

        Sky.Clear();
        OverlaySkyTessellator.AppendCelestialBodies(Sky, state.WorldTimeTicks, camera.Position.Y);
        return Sky.Count > 0;
    }

    /// <summary>
    /// Tessellates every depth-tested world overlay for the frame.
    /// </summary>
    /// <param name="camera">Active first-person camera.</param>
    /// <param name="state">Per-frame overlay state.</param>
    /// <param name="atlasData">Staged atlas lookup for textured quads.</param>
    public void BuildWorldPass(FirstPersonCamera camera, FrameOverlayState state, TextureAtlasData? atlasData)
    {
        Clouds.Clear();
        WorldTriangles.Clear();
        WorldLines.Clear();

        // The Nether is roofed with Bedrock, so it never shows the Overworld cloud deck.
        if (!state.IsUnderwater && !state.IsNetherDimension && camera.Position.Y > OverHorizon.Core.EngineConstants.WorldMinY - 2.0)
        {
            AppendWorldCloudsParallel(camera.Position, state.WorldTimeTicks, state.CloudDriftOffset, state.CameraSkyLightNormalized);
        }

        if (camera.Position.Y < 24.0)
        {
            OverlaySkyTessellator.AppendVoidAbyssFloor(WorldTriangles, camera.Position);
        }

        if (state.TargetedBlock is { } target)
        {
            var box = state.TargetedSelectionBox ?? BoundingBox.FromBlock(target);
            AppendSelectionWireframe(WorldLines, camera.Position, in box);

            if (state.MiningProgress is > 0.01f and < 1.0f)
            {
                AppendBlockDestroyCracks(WorldTriangles, camera.Position, in box, state.MiningProgress);
            }
        }

        var cameraPos = camera.Position;
        var worldTris = WorldTriangles;

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
    }

    /// <summary>
    /// Tessellates the first-person viewmodel for the frame.
    /// </summary>
    /// <param name="state">Per-frame overlay state.</param>
    /// <param name="atlasData">Staged atlas lookup for textured quads.</param>
    public void BuildScreenPass(FirstPersonCamera camera, FrameOverlayState state, TextureAtlasData? atlasData)
    {
        ViewModel.Clear();
        if (!state.IsThirdPersonCamera && state.Mode is UiOverlayMode.InGame or UiOverlayMode.ChatInput)
        {
            HeldItem3DMesher.AppendHeldViewmodel(ViewModel, state, atlasData);
        }
    }

    /// <summary>
    /// Tessellates the rotating 3D player model shown inside the inventory screen.
    /// </summary>
    /// <param name="state">Per-frame overlay state.</param>
    /// <returns><see langword="true"/> when preview geometry was produced.</returns>
    public bool BuildInventoryPreview(FrameOverlayState state)
    {
        var rect = state.InventoryPlayerPreviewRect;
        var boxCenterX = rect.X + (rect.Z * 0.5f);
        var boxEyeY = rect.Y + (rect.W * 0.34f);
        var dx = state.CursorScreenPos.X - boxCenterX;
        var dy = state.CursorScreenPos.Y - boxEyeY;
        var bodyYaw = MathF.Atan(dx / 65.0f) * 0.55f;
        var headYaw = MathF.Atan(dx / 50.0f) * 0.68f;
        var headPitch = MathF.Atan(dy / 55.0f) * 0.55f;

        InventoryPreview.Clear();
        var root = Matrix4x4.CreateRotationY(bodyYaw) *
                   Matrix4x4.CreateRotationX(headPitch * 0.18f) *
                   Matrix4x4.CreateTranslation(0.0f, -0.96f, 0.0f);

        WorldMob3DMesher.AppendSteveModel(InventoryPreview, root, headYaw - bodyYaw, headPitch, 0.0f, Vector4.One, state.LocalPlayerArmor);
        return InventoryPreview.Count > 0;
    }

    /// <summary>
    /// Tessellates a per-frame entity batch into <see cref="WorldTriangles"/>, fanning the work out over
    /// the worker pool when the batch is large enough to pay for the hand-off.
    /// </summary>
    /// <typeparam name="T">Visual element type.</typeparam>
    /// <param name="items">Batch of visual elements (may be <see langword="null"/>).</param>
    /// <param name="minItemsPerWorker">Minimum items per worker before splitting.</param>
    /// <param name="append">Callback that emits one slice into a worker bucket.</param>
    private void ParallelAppend<T>(
        IReadOnlyList<T>? items,
        int minItemsPerWorker,
        Action<List<OverlayVertex>, IReadOnlyList<T>> append)
    {
        if (items is null || items.Count == 0)
        {
            return;
        }

        var workers = RenderParallelism.ResolveWorkerCount(items.Count, minItemsPerWorker, _maxRenderThreads);
        if (workers <= 1)
        {
            append(WorldTriangles, items);
            return;
        }

        var sink = _worldSink;
        var capped = Math.Min(workers, sink.WorkerCount);
        sink.Reset();
        Parallel.For(0, capped, worker =>
        {
            var (start, end) = RenderParallelism.RangeFor(items.Count, capped, worker);
            if (end <= start)
            {
                return;
            }

            append(sink.Bucket(worker), new ListSlice<T>(items, start, end - start));
        });

        sink.DrainTo(WorldTriangles);
    }

    /// <summary>
    /// Tessellates the drifting cloud deck across the worker pool, one contiguous band of grid rows per worker.
    /// </summary>
    /// <param name="cameraPos">Camera world position.</param>
    /// <param name="worldTimeTicks">World time used for the day/night tint.</param>
    /// <param name="driftOffset">Cloud drift offset in blocks.</param>
    /// <param name="skyBrightness">Current sky brightness multiplier.</param>
    private void AppendWorldCloudsParallel(Vec3d cameraPos, long worldTimeTicks, float driftOffset, float skyBrightness)
    {
        var workers = RenderParallelism.ResolveWorkerCount(
            OverlaySkyTessellator.CloudGridExtent,
            CloudMinRowsPerWorker,
            _maxRenderThreads);

        if (workers <= 1)
        {
            OverlaySkyTessellator.AppendWorldClouds(Clouds, cameraPos, worldTimeTicks, driftOffset, skyBrightness);
            return;
        }

        var sink = _cloudSink;
        var capped = Math.Min(workers, sink.WorkerCount);
        sink.Reset();
        Parallel.For(0, capped, worker =>
        {
            var (start, end) = RenderParallelism.RangeFor(OverlaySkyTessellator.CloudGridExtent, capped, worker);
            if (end <= start)
            {
                return;
            }

            OverlaySkyTessellator.AppendWorldClouds(
                sink.Bucket(worker),
                cameraPos,
                worldTimeTicks,
                driftOffset,
                skyBrightness,
                start - OverlaySkyTessellator.CloudGridRadius,
                end - OverlaySkyTessellator.CloudGridRadius);
        });

        sink.DrainTo(Clouds);
    }

    private static void AppendBlockDestroyCracks(List<OverlayVertex> dst, Vec3d cameraPos, in BoundingBox box, float miningProgress)
    {
        var stage = Math.Clamp((int)(miningProgress * 10.0f), 0, 9);
        var tile = ProceduralAtlasGenerator.FirstDestroyStageTile + stage;
        const float eps = 0.0045f;
        var min = new Vector3((float)(box.MinX - cameraPos.X) - eps, (float)(box.MinY - cameraPos.Y) - eps, (float)(box.MinZ - cameraPos.Z) - eps);
        var max = new Vector3((float)(box.MaxX - cameraPos.X) + eps, (float)(box.MaxY - cameraPos.Y) + eps, (float)(box.MaxZ - cameraPos.Z) + eps);
        var tint = new Vector4(0.18f, 0.18f, 0.18f, 0.82f);

        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(min.X, max.Y, min.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(max.X, max.Y, min.Z), Matrix4x4.Identity, tile, tint);
        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z), Matrix4x4.Identity, tile, tint);
        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z), Matrix4x4.Identity, tile, tint);
        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z), Matrix4x4.Identity, tile, tint);
        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z), Matrix4x4.Identity, tile, tint);
        HeldItem3DMesher.EmitBlockCubeFaceTinted(dst, new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z), Matrix4x4.Identity, tile, tint);
    }

    private static void AppendSelectionWireframe(List<OverlayVertex> dst, Vec3d cameraPos, in BoundingBox box)
    {
        const float eps = 0.003f;
        var min = new Vector3((float)(box.MinX - cameraPos.X) - eps, (float)(box.MinY - cameraPos.Y) - eps, (float)(box.MinZ - cameraPos.Z) - eps);
        var max = new Vector3((float)(box.MaxX - cameraPos.X) + eps, (float)(box.MaxY - cameraPos.Y) + eps, (float)(box.MaxZ - cameraPos.Z) + eps);
        var col = new Vector4(0.04f, 0.04f, 0.06f, 0.88f);

        Span<Vector3> c =
        [
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(max.X, min.Y, max.Z), new(min.X, min.Y, max.Z),
            new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z),
        ];

        ReadOnlySpan<(int A, int B)> edges = [(0, 1), (1, 2), (2, 3), (3, 0), (4, 5), (5, 6), (6, 7), (7, 4), (0, 4), (1, 5), (2, 6), (3, 7)];
        foreach (var (a, b) in edges)
        {
            dst.Add(new OverlayVertex(c[a], col));
            dst.Add(new OverlayVertex(c[b], col));
        }
    }
}
