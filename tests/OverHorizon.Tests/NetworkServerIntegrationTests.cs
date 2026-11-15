using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Networking;
using OverHorizon.Networking.Client;
using OverHorizon.Networking.Codec;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Networking.Primitives;
using OverHorizon.Server;
using OverHorizon.World;
using OverHorizon.World.Serialization;
using Xunit;

namespace OverHorizon.Tests;

public sealed class NetworkServerIntegrationTests
{
    [Fact]
    public async Task TcpLoopback_StatusAndPingQuery_ReturnsMotdAndEchoesTimestampAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var host = ServerBootstrap.BuildHost([], new ServerOptions(TicksPerSecond: 100));

        var netServer = host.Services.GetRequiredService<INetworkServer>();
        var bound = await netServer.StartListeningAsync(new IPEndPoint(IPAddress.Loopback, 0), cts.Token)
            .ConfigureAwait(false);

        await using var client = await JavaProtocolClient.ConnectTcpAsync(bound, cts.Token).ConfigureAwait(false);
        const long sentTimestamp = 0x0102_0304_0506_0708L;

        var (status, echoed) = await client.QueryStatusAndPingAsync(
            JavaProtocolCodec.CurrentProtocolVersion,
            sentTimestamp,
            cts.Token).ConfigureAwait(false);

        status.ProtocolVersion.Should().Be(JavaProtocolCodec.CurrentProtocolVersion);
        status.Description.Should().Contain("OverHorizon");
        echoed.Should().Be(sentTimestamp);
    }

    [Fact]
    public async Task TcpLoopback_ProtocolMismatch_RejectsLoginWithProtocolExceptionAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var host = ServerBootstrap.BuildHost([], new ServerOptions(TicksPerSecond: 100));

        var netServer = host.Services.GetRequiredService<INetworkServer>();
        var bound = await netServer.StartListeningAsync(new IPEndPoint(IPAddress.Loopback, 0), cts.Token)
            .ConfigureAwait(false);

        await using var client = await JavaProtocolClient.ConnectTcpAsync(bound, cts.Token).ConfigureAwait(false);
        Func<Task> badLogin = async () => _ = await client.LoginAsync("OutdatedClient", protocolVersion: 1, cancellationToken: cts.Token)
            .ConfigureAwait(false);

        await badLogin.Should().ThrowAsync<ProtocolException>()
            .WithMessage("*Incompatible protocol version*")
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task FullMultiplayerLoopback_LoginChunksMoveAntiCheatBlockSyncChatAndCleanupAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var serverOptions = new ServerOptions(
            TicksPerSecond: 100,
            ListenPort: 0,
            PlayerViewDistanceChunks: 1);

        using var host = ServerBootstrap.BuildHost([], serverOptions);
        await host.StartAsync(cts.Token).ConfigureAwait(false);

        try
        {
            var netServer = host.Services.GetRequiredService<INetworkServer>();
            var world = host.Services.GetRequiredService<IWorldSubsystem>();
            var gameplay = host.Services.GetRequiredService<IGameplaySubsystem>();
            var registries = host.Services.GetRequiredService<CoreRegistries>();

            // Wait briefly for BackgroundService startup to bind the ephemeral loopback/any port.
            while (netServer.BoundEndPoint is null)
            {
                await Task.Delay(10, cts.Token).ConfigureAwait(false);
            }

            var connectEndPoint = new IPEndPoint(IPAddress.Loopback, netServer.BoundEndPoint.Port);

            await using (var alice = await JavaProtocolClient.ConnectTcpAsync(connectEndPoint, cts.Token).ConfigureAwait(false))
            await using (var bob = await JavaProtocolClient.ConnectTcpAsync(connectEndPoint, cts.Token).ConfigureAwait(false))
            {
                var aliceLogin = await alice.LoginAsync("Alice", cancellationToken: cts.Token).ConfigureAwait(false);
                var bobLogin = await bob.LoginAsync("Bob", cancellationToken: cts.Token).ConfigureAwait(false);

                alice.CompressionThreshold.Should().Be(256);
                aliceLogin.AssignedEntityId.Should().NotBe(bobLogin.AssignedEntityId);

                // Drain initial spawn position + 9 (3x3) chunk packets for both Alice and Bob.
                var (aliceSpawn, aliceChunks) = await ReceiveInitialSpawnAndChunksAsync(
                    alice,
                    expectedChunks: 9,
                    registries,
                    cts.Token).ConfigureAwait(false);
                var (_, bobChunks) = await ReceiveInitialSpawnAndChunksAsync(
                    bob,
                    expectedChunks: 9,
                    registries,
                    cts.Token).ConfigureAwait(false);

                aliceChunks.Should().Be(9);
                bobChunks.Should().Be(9);
                netServer.OnlinePlayerCount.Should().Be(2);

                // 1. Valid step movement (within anti-cheat speed limit and above solid ground).
                var validTarget = new Vec3d(aliceSpawn.X, aliceSpawn.Y + 0.5, aliceSpawn.Z);
                await alice.SendPlayPacketAsync(
                    new MovePlayerPacket(validTarget, 90.0f, 0.0f, OnGround: false),
                    cts.Token).ConfigureAwait(false);

                await WaitUntilAsync(
                    () => gameplay.TryGetEntityPosition(aliceLogin.AssignedEntityId) is { } p &&
                        Math.Abs(p.Y - validTarget.Y) < 0.3,
                    cts.Token).ConfigureAwait(false);

                // 2. Speed/teleport hack (+500 blocks in a single packet) -> server rubber-bands Alice!
                await alice.SendPlayPacketAsync(
                    new MovePlayerPacket(new Vec3d(aliceSpawn.X + 500.0, aliceSpawn.Y + 10.0, aliceSpawn.Z), 0.0f, 0.0f, OnGround: false),
                    cts.Token).ConfigureAwait(false);

                var rubberBand = await ReadNextOfTypeAsync<SynchronizePlayerPositionPacket>(alice, cts.Token)
                    .ConfigureAwait(false);
                Math.Abs(rubberBand.Position.X - aliceSpawn.X).Should().BeLessThan(1.0);

                // 3. Place cobblestone block nearby (within 6.5 block reach, not intersecting player body).
                var placePos = new BlockPos((int)Math.Floor(aliceSpawn.X) + 2, (int)Math.Floor(aliceSpawn.Y) + 2, (int)Math.Floor(aliceSpawn.Z));
                var cobbleState = registries.Blocks.GetDefaultState(BuiltInBlocks.Cobblestone);

                await alice.SendPlayPacketAsync(
                    new PlayerBlockActionPacket(PlayerBlockActionKind.PlaceBlock, placePos, Direction.Up, cobbleState.Id),
                    cts.Token).ConfigureAwait(false);

                var aliceBlockUpdate = await ReadNextOfTypeAsync<BlockUpdatePacket>(alice, cts.Token).ConfigureAwait(false);
                var bobBlockUpdate = await ReadNextOfTypeAsync<BlockUpdatePacket>(bob, cts.Token).ConfigureAwait(false);

                aliceBlockUpdate.Should().Be(new BlockUpdatePacket(placePos, cobbleState.Id));
                bobBlockUpdate.Should().Be(new BlockUpdatePacket(placePos, cobbleState.Id));
                world.GetBlockState(placePos).Should().Be(cobbleState);

                // 4. Chat broadcast from Bob to Alice.
                await bob.SendPlayPacketAsync(new ChatMessagePacket("Hello OverHorizon!"), cts.Token).ConfigureAwait(false);
                var chatReceived = await ReadNextOfTypeAsync<SystemChatPacket>(alice, cts.Token).ConfigureAwait(false);
                chatReceived.Message.Should().Be("<Bob> Hello OverHorizon!");
            }

            // 5. After both clients dispose/close TCP sockets, server despawns entities and drops online count to 0.
            await WaitUntilAsync(
                () => netServer.OnlinePlayerCount == 0 && gameplay.EntityCount == 0,
                cts.Token).ConfigureAwait(false);
            netServer.OnlinePlayerCount.Should().Be(0);
            gameplay.EntityCount.Should().Be(0);
        }
        finally
        {
            await host.StopAsync(cts.Token).ConfigureAwait(false);
        }
    }

    private static async Task<(Vec3d SpawnPos, int ChunkCount)> ReceiveInitialSpawnAndChunksAsync(
        JavaProtocolClient client,
        int expectedChunks,
        CoreRegistries registries,
        CancellationToken ct)
    {
        Vec3d? spawn = null;
        var chunks = 0;

        while (spawn is null || chunks < expectedChunks)
        {
            var pkt = await client.ReceivePacketAsync(ct).ConfigureAwait(false);
            switch (pkt)
            {
                case SynchronizePlayerPositionPacket pos:
                    spawn = pos.Position;
                    break;

                case ChunkDataPacket chunk:
                    var decoded = ChunkNetworkSerializer.DeserializeColumn(
                        chunk.Position,
                        chunk.Payload.Span,
                        registries.Blocks);
                    decoded.Position.Should().Be(chunk.Position);
                    chunks++;
                    break;
            }
        }

        return (spawn.Value, chunks);
    }

    private static async Task<TPacket> ReadNextOfTypeAsync<TPacket>(
        JavaProtocolClient client,
        CancellationToken ct)
        where TPacket : class, IClientboundPacket
    {
        while (true)
        {
            var pkt = await client.ReceivePacketAsync(ct).ConfigureAwait(false);
            if (pkt is TPacket matched)
            {
                return matched;
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken ct)
    {
        while (!predicate())
        {
            await Task.Delay(10, ct).ConfigureAwait(false);
        }
    }
}
