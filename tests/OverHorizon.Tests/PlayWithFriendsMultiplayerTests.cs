using System.Buffers;
using System.Numerics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OverHorizon.Client;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Modding.Commands;
using OverHorizon.Networking;
using OverHorizon.Networking.Codec;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Physics;
using OverHorizon.Rendering.Textures;
using OverHorizon.Server;
using Xunit;

namespace OverHorizon.Tests;

/// <summary>
/// Step 9 ("Play With Friends" v0.1.0-alpha) integration and unit tests covering 3D DDA voxel raycasting,
/// remote player wire packets, hotbar selection, server CLI persistence flags, and live 2-player coop sessions.
/// </summary>
public sealed class PlayWithFriendsMultiplayerTests
{
    [Fact]
    public void VoxelRaycaster_HitsSolidBlockIdentifiesEntryFaceAndPassesThroughWater()
    {
        var registries = CoreRegistries.CreateDefault();
        var atlas = ProceduralAtlasGenerator.CreateDefault(registries.Blocks);
        var cache = new ClientWorldCache(registries.Blocks, atlas);

        var stone = registries.Blocks.GetDefaultState(BuiltInBlocks.Stone);
        var water = registries.Blocks.GetDefaultState(BuiltInBlocks.Water);

        var col = new OverHorizon.World.Storage.ChunkColumn(new ChunkPos(0, 0));
        _ = col.SetBlockState(5, 70, 4, water, registries.Blocks);
        _ = col.SetBlockState(5, 70, 5, stone, registries.Blocks);
        cache.UpsertColumn(col);

        // Ray traveling +Z from (5.5, 70.5, 2.2) passes through Water at z=4 and hits North (-Z) face of Stone at z=5.
        var found = VoxelRaycaster.TryRaycast(
            cache,
            new Vec3d(5.5, 70.5, 2.2),
            new Vector3(0.0f, 0.0f, 1.0f),
            maxDistance: 6.0,
            out var hit);

        found.Should().BeTrue();
        hit.BlockPosition.Should().Be(new BlockPos(5, 70, 5));
        hit.HitFace.Should().Be(Direction.North);
        hit.AdjacentPosition.Should().Be(new BlockPos(5, 70, 4));

        // Ray with shorter maxDistance misses.
        VoxelRaycaster.TryRaycast(
            cache,
            new Vec3d(5.5, 70.5, 2.2),
            new Vector3(0.0f, 0.0f, 1.0f),
            maxDistance: 1.5,
            out _).Should().BeFalse();
    }

    [Fact]
    public void RemotePlayerPackets_RoundTripThroughJavaProtocolCodec()
    {
        var codec = new JavaProtocolCodec();
        var writer = new ArrayBufferWriter<byte>(256);

        var spawn = new RemotePlayerSpawnPacket(42L, "FriendAlex", new Vec3d(12.5, 68.0, -4.25), 90.0f, -15.0f);
        codec.EncodeClientbound(ConnectionState.Play, spawn, writer);
        codec.DecodeClientbound(ConnectionState.Play, writer.WrittenSpan)
            .Should().BeOfType<RemotePlayerSpawnPacket>().Which.Should().Be(spawn);

        writer.Clear();
        var move = new RemotePlayerMovePacket(42L, new Vec3d(13.25, 68.0, -4.25), 135.0f, 5.0f);
        codec.EncodeClientbound(ConnectionState.Play, move, writer);
        codec.DecodeClientbound(ConnectionState.Play, writer.WrittenSpan)
            .Should().BeOfType<RemotePlayerMovePacket>().Which.Should().Be(move);

        writer.Clear();
        var despawn = new RemotePlayerDespawnPacket(42L);
        codec.EncodeClientbound(ConnectionState.Play, despawn, writer);
        codec.DecodeClientbound(ConnectionState.Play, writer.WrittenSpan)
            .Should().BeOfType<RemotePlayerDespawnPacket>().Which.Should().Be(despawn);
    }

    [Fact]
    public void ClientHotbar_SelectsAndCyclesThroughNineBuildingBlocks()
    {
        var blocks = BuiltInBlocks.CreateFrozen();
        var hotbar = new ClientHotbar(blocks);

        hotbar.SlotCount.Should().Be(9);
        hotbar.SelectedSlot.Should().Be(0);
        hotbar.SelectedBlockKey.Should().Be(BuiltInBlocks.Cobblestone);

        hotbar.CycleSlot(-1);
        hotbar.SelectedSlot.Should().Be(8);
        hotbar.SelectedBlockKey.Should().Be(BuiltInBlocks.Glowstone);

        hotbar.SelectSlot(4);
        hotbar.SelectedBlockKey.Should().Be(BuiltInBlocks.OakPlanks);
    }

    [Fact]
    public void ServerBootstrap_ParsesPlayWithFriendsCliOptions()
    {
        var parsed = ServerBootstrap.ParseOptions(
            ["--port", "25570", "--view-distance", "5", "--seed", "777", "--world-dir", "./my-world", "--op-all"]);

        parsed.ListenPort.Should().Be((ushort)25570);
        parsed.PlayerViewDistanceChunks.Should().Be(5);
        parsed.WorldSeed.Should().Be(777L);
        parsed.WorldSaveDirectory.Should().Be("./my-world");
        parsed.DefaultPlayerPermission.Should().Be(PermissionLevel.Admin);
    }

    [Fact]
    public async Task TwoFriendsMultiplayerSession_SyncsAvatarsBreaksPlacesBlocksAndSavesWorldToDiskAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var tempWorldDir = Path.Combine(Path.GetTempPath(), $"overhorizon-friends-{Guid.NewGuid():N}");

        try
        {
            var serverOpts = new ServerOptions(
                TicksPerSecond: 100,
                ListenPort: 0,
                PlayerViewDistanceChunks: 1,
                WorldSeed: 2026L,
                WorldSaveDirectory: tempWorldDir,
                DefaultPlayerPermission: PermissionLevel.Admin);

            using var serverHost = ServerBootstrap.BuildHost([], serverOpts);
            await serverHost.StartAsync(cts.Token).ConfigureAwait(false);

            try
            {
                var netServer = serverHost.Services.GetRequiredService<INetworkServer>();
                while (netServer.BoundEndPoint is null)
                {
                    await Task.Delay(10, cts.Token).ConfigureAwait(false);
                }

                var port = (ushort)netServer.BoundEndPoint.Port;

                using var aliceHost = ClientBootstrap.BuildHost(
                    [],
                    new ClientOptions(Headless: true, ServerHost: "127.0.0.1", ServerPort: port, Username: "Alice", ViewDistanceChunks: 1));
                using var bobHost = ClientBootstrap.BuildHost(
                    [],
                    new ClientOptions(Headless: true, ServerHost: "127.0.0.1", ServerPort: port, Username: "Bob", ViewDistanceChunks: 1));

                await aliceHost.StartAsync(cts.Token).ConfigureAwait(false);
                await bobHost.StartAsync(cts.Token).ConfigureAwait(false);

                try
                {
                    var alice = aliceHost.Services.GetRequiredService<ClientSessionRunner>();
                    var bob = bobHost.Services.GetRequiredService<ClientSessionRunner>();
                    var blocks = aliceHost.Services.GetRequiredService<IBlockRegistry>();

                    await alice.InitializeSessionAsync(cts.Token).ConfigureAwait(false);
                    await bob.InitializeSessionAsync(cts.Token).ConfigureAwait(false);

                    // Pump frames until both friends have received chunks and spotted each other's 3D avatar.
                    for (var i = 0; i < 50; i++)
                    {
                        _ = await alice.RunHeadlessFramesAsync(1, cancellationToken: cts.Token).ConfigureAwait(false);
                        _ = await bob.RunHeadlessFramesAsync(1, cancellationToken: cts.Token).ConfigureAwait(false);
                        if (alice.WorldCache.LoadedChunkCount >= 9 &&
                            bob.WorldCache.LoadedChunkCount >= 9 &&
                            alice.RemotePlayerCount == 1 &&
                            bob.RemotePlayerCount == 1)
                        {
                            break;
                        }

                        await Task.Delay(15, cts.Token).ConfigureAwait(false);
                    }

                    alice.RemotePlayerCount.Should().Be(1);
                    bob.RemotePlayerCount.Should().Be(1);

                    // Alice looks obliquely down at the ground block in front of her (+Z, pitch -60) so placement won't overlap her AABB.
                    alice.Player.Camera.SetRotation(yawDegrees: 180.0f, pitchDegrees: -60.0f);
                    _ = await alice.RunHeadlessFramesAsync(1, cancellationToken: cts.Token).ConfigureAwait(false);

                    alice.Player.CurrentTarget.Should().NotBeNull();
                    var targetHit = alice.Player.CurrentTarget!.Value;

                    // Alice selects Glowstone (slot 8) and places it onto the adjacent air voxel.
                    alice.Player.Hotbar.SelectSlot(8);
                    var placedPos = targetHit.AdjacentPosition;
                    alice.TryPlaceSelectedBlock().Should().BeTrue();

                    var glowstoneState = blocks.GetDefaultState(BuiltInBlocks.Glowstone);
                    for (var i = 0; i < 40; i++)
                    {
                        _ = await alice.RunHeadlessFramesAsync(1, cancellationToken: cts.Token).ConfigureAwait(false);
                        _ = await bob.RunHeadlessFramesAsync(1, cancellationToken: cts.Token).ConfigureAwait(false);
                        if (bob.WorldCache.GetBlockState(placedPos) == glowstoneState)
                        {
                            break;
                        }

                        await Task.Delay(15, cts.Token).ConfigureAwait(false);
                    }

                    bob.WorldCache.GetBlockState(placedPos).Should().Be(glowstoneState);

                    // Now Alice breaks that placed Glowstone block and Bob sees it turn back to Air!
                    alice.Player.RefreshRaycastTarget(alice.WorldCache);
                    alice.TryBreakTargetedBlock().Should().BeTrue();

                    for (var i = 0; i < 40; i++)
                    {
                        _ = await alice.RunHeadlessFramesAsync(1, cancellationToken: cts.Token).ConfigureAwait(false);
                        _ = await bob.RunHeadlessFramesAsync(1, cancellationToken: cts.Token).ConfigureAwait(false);
                        if (bob.WorldCache.GetBlockState(placedPos).IsAir)
                        {
                            break;
                        }

                        await Task.Delay(15, cts.Token).ConfigureAwait(false);
                    }

                    bob.WorldCache.GetBlockState(placedPos).IsAir.Should().BeTrue();

                    // Bob disconnects -> Alice receives RemotePlayerDespawnPacket.
                    await bob.DisposeAsync().ConfigureAwait(false);
                    for (var i = 0; i < 40 && alice.RemotePlayerCount > 0; i++)
                    {
                        _ = await alice.RunHeadlessFramesAsync(1, cancellationToken: cts.Token).ConfigureAwait(false);
                        await Task.Delay(15, cts.Token).ConfigureAwait(false);
                    }

                    alice.RemotePlayerCount.Should().Be(0);
                    await alice.DisposeAsync().ConfigureAwait(false);
                }
                finally
                {
                    await bobHost.StopAsync(CancellationToken.None).ConfigureAwait(false);
                    await aliceHost.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                await serverHost.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }

            // Stopping the server flushes all modified chunks into the world save directory!
            Directory.Exists(tempWorldDir).Should().BeTrue();
            Directory.GetFiles(tempWorldDir, "*", SearchOption.AllDirectories).Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempWorldDir))
            {
                Directory.Delete(tempWorldDir, recursive: true);
            }
        }
    }
}
