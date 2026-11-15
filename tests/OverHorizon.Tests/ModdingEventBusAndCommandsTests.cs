using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Modding;
using OverHorizon.Modding.Commands;
using OverHorizon.Modding.DataPacks;
using OverHorizon.Modding.Events;
using OverHorizon.Modding.Loading;
using OverHorizon.Modding.Manifest;
using OverHorizon.Networking;
using OverHorizon.Networking.Client;
using OverHorizon.Networking.Packets;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Server;
using OverHorizon.World;
using Xunit;

namespace OverHorizon.Tests;

public sealed class ModdingEventBusAndCommandsTests
{
    [Fact]
    public void ModDependencySorter_ResolvesDiamondGraphAndRejectsCyclesOrMissingDeps()
    {
        var baseMod = new StubMod(new ModManifest("core_lib", "Core Lib", "1.0.0"));
        var branchA = new StubMod(new ModManifest("flora", "Flora", "1.0.0", Dependencies: ["core_lib"]));
        var branchB = new StubMod(new ModManifest("ores", "Ores", "1.0.0", Dependencies: ["core_lib"]));
        var topMod = new StubMod(new ModManifest("expansion", "Expansion", "1.0.0", Dependencies: ["ores", "flora"]));

        var sorted = ModDependencySorter.Sort([topMod, branchB, baseMod, branchA]);
        sorted.Select(static m => m.Manifest.Id)
            .Should()
            .Equal("core_lib", "flora", "ores", "expansion");

        // Missing dependency must throw ModLoadException.
        Action missingDep = () => _ = ModDependencySorter.Sort([branchA]);
        missingDep.Should().Throw<ModLoadException>().WithMessage("*missing dependency*");

        // Cycle A <-> B must throw ModLoadException.
        var cycA = new StubMod(new ModManifest("mod_a", "A", "1.0.0", Dependencies: ["mod_b"]));
        var cycB = new StubMod(new ModManifest("mod_b", "B", "1.0.0", Dependencies: ["mod_a"]));
        Action cycleSort = () => _ = ModDependencySorter.Sort([cycA, cycB]);
        cycleSort.Should().Throw<ModLoadException>().WithMessage("*Circular mod dependency*");
    }

    [Fact]
    public void EventBus_ExecutesInPriorityOrderHonorsCancellationMonitorAndUnsubscribe()
    {
        var bus = new EventBus();
        var log = new List<string>();

        using var subHigh = bus.Subscribe<BlockBreakEvent>(
            (ref BlockBreakEvent ev) => log.Add($"high:{ev.IsCancelled}"),
            priority: EventPriority.High,
            ignoreCancelled: true);

        using var subLowest = bus.Subscribe<BlockBreakEvent>(
            (ref BlockBreakEvent ev) =>
            {
                log.Add("lowest:cancel");
                ev.IsCancelled = true;
            },
            priority: EventPriority.Lowest);

        using var subMonitor = bus.Subscribe<BlockBreakEvent>(
            (ref BlockBreakEvent ev) => log.Add($"monitor:{ev.IsCancelled}"),
            priority: EventPriority.Monitor,
            ignoreCancelled: true);

        var first = new BlockBreakEvent(1L, "Alice", new BlockPos(0, 64, 0), BlockState.Air);
        bus.Publish(ref first);

        first.IsCancelled.Should().BeTrue();
        log.Should().Equal("lowest:cancel", "monitor:True");

        // Dispose cancelling handler -> high handler now executes!
        subLowest.Dispose();
        log.Clear();

        var second = new BlockBreakEvent(1L, "Alice", new BlockPos(0, 64, 0), BlockState.Air);
        bus.Publish(ref second);
        second.IsCancelled.Should().BeFalse();
        log.Should().Equal("high:False", "monitor:False");
    }

    [Fact]
    public void DataPackLoader_RegistersCustomBlocksItemsAndBiomesBeforeFreeze()
    {
        const string json = """
            {
              "manifest": {
                "id": "crystal_caverns",
                "name": "Crystal Caverns Pack",
                "version": "0.2.0"
              },
              "blocks": [
                {
                  "key": "crystal_caverns:crystal_ore",
                  "isSolid": true,
                  "isOpaque": true,
                  "lightEmission": 12,
                  "hardness": 3.0,
                  "registerItem": true
                }
              ],
              "items": [
                {
                  "key": "crystal_caverns:ruby_shard",
                  "maxStackSize": 64
                }
              ],
              "biomes": [
                {
                  "key": "crystal_caverns:crystal_valley",
                  "temperature": 0.4,
                  "downfall": 0.6,
                  "surfaceBlock": "crystal_caverns:crystal_ore",
                  "underSurfaceBlock": "overhorizon:stone"
                }
              ]
            }
            """;

        var pack = DataPackLoader.ParseBundle(json);
        var modHost = new ModHost([pack]);

        modHost.LoadedModCount.Should().Be(1);
        modHost.Registries.Blocks.IsFrozen.Should().BeTrue();

        var oreKey = ResourceKey.Parse("crystal_caverns:crystal_ore");
        var oreState = modHost.Registries.Blocks.GetDefaultState(oreKey);
        modHost.Registries.Blocks.GetStateMetadata(oreState).LightEmission.Should().Be(12);

        // Auto-registered block-item + explicit ruby_shard item + custom biome.
        modHost.Registries.Items.GetId(oreKey).Should().BeGreaterThan(0);
        modHost.Registries.Items.GetId(ResourceKey.Parse("crystal_caverns:ruby_shard")).Should().BeGreaterThan(0);
        modHost.Registries.Biomes.GetRequired(ResourceKey.Parse("crystal_caverns:crystal_valley"))
            .SurfaceState.Should().Be(oreState);
    }

    [Fact]
    public async Task ServerIntegration_ReferenceModCustomBlockSpawnProtectionAndSlashCommandsAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var sampleMod = new SpawnProtectionReferenceMod();
        var options = new ServerOptions(TicksPerSecond: 100, ListenPort: 0, PlayerViewDistanceChunks: 1);

        using var host = ServerBootstrap.BuildHost([], options, [sampleMod]);
        await host.StartAsync(cts.Token).ConfigureAwait(false);

        try
        {
            var modHost = host.Services.GetRequiredService<IModHost>();
            var netServer = host.Services.GetRequiredService<INetworkServer>();
            var world = host.Services.GetRequiredService<IWorldSubsystem>();
            var playBridge = host.Services.GetRequiredService<ServerPlaySessionBridge>();

            modHost.LoadedModCount.Should().Be(1);

            while (netServer.BoundEndPoint is null)
            {
                await Task.Delay(10, cts.Token).ConfigureAwait(false);
            }

            var endpoint = new IPEndPoint(IPAddress.Loopback, netServer.BoundEndPoint.Port);
            await using var client = await JavaProtocolClient.ConnectTcpAsync(endpoint, cts.Token).ConfigureAwait(false);
            _ = await client.LoginAsync("ModTester", cancellationToken: cts.Token).ConfigureAwait(false);

            var spawnPos = await DrainUntilSpawnPositionAsync(client, expectedChunks: 9, cts.Token).ConfigureAwait(false);

            // 1. /seed works for Everyone.
            await client.SendPlayPacketAsync(new ChatMessagePacket("/seed"), cts.Token).ConfigureAwait(false);
            var seedReply = await ReadNextPacketAsync<SystemChatPacket>(client, cts.Token).ConfigureAwait(false);
            seedReply.Message.Should().StartWith("Seed: [");

            // 2. /setblock denied before promotion, succeeds after promotion using custom mod block!
            var lampPos = new BlockPos(2, (int)Math.Floor(spawnPos.Y) + 1, 2);
            await client.SendPlayPacketAsync(
                new ChatMessagePacket($"/setblock {lampPos.X} {lampPos.Y} {lampPos.Z} guard_mod:obsidian_lamp"),
                cts.Token).ConfigureAwait(false);
            var deniedReply = await ReadNextPacketAsync<SystemChatPacket>(client, cts.Token).ConfigureAwait(false);
            deniedReply.Message.Should().Contain("Insufficient permissions");

            playBridge.TrySetPlayerPermission("ModTester", PermissionLevel.Admin).Should().BeTrue();
            await client.SendPlayPacketAsync(
                new ChatMessagePacket($"/setblock {lampPos.X} {lampPos.Y} {lampPos.Z} guard_mod:obsidian_lamp"),
                cts.Token).ConfigureAwait(false);

            var blockSync = await ReadNextPacketAsync<BlockUpdatePacket>(client, cts.Token).ConfigureAwait(false);
            var confirmChat = await ReadNextPacketAsync<SystemChatPacket>(client, cts.Token).ConfigureAwait(false);
            confirmChat.Message.Should().Contain("guard_mod:obsidian_lamp");
            world.GetBlockState(lampPos).Id.Should().Be(blockSync.BlockStateId);

            // 3. Mod-registered slash command /pingmod.
            await client.SendPlayPacketAsync(new ChatMessagePacket("/pingmod"), cts.Token).ConfigureAwait(false);
            var modCmdReply = await ReadNextPacketAsync<SystemChatPacket>(client, cts.Token).ConfigureAwait(false);
            modCmdReply.Message.Should().Be("Pong from GuardMod!");

            // 4. Protected spawn column (X == 0, Z == 0): breaking ground under spawn is vetoed by BlockBreakEvent!
            var protectedBlock = new BlockPos(0, (int)Math.Floor(spawnPos.Y) - 1, 0);
            var originalGround = world.GetBlockState(protectedBlock);
            originalGround.IsAir.Should().BeFalse();

            await client.SendPlayPacketAsync(
                new PlayerBlockActionPacket(PlayerBlockActionKind.BreakBlock, protectedBlock, Direction.Up, 0),
                cts.Token).ConfigureAwait(false);

            var revertPacket = await ReadNextPacketAsync<BlockUpdatePacket>(client, cts.Token).ConfigureAwait(false);
            revertPacket.Should().Be(new BlockUpdatePacket(protectedBlock, originalGround.Id));
            world.GetBlockState(protectedBlock).Should().Be(originalGround);
        }
        finally
        {
            await host.StopAsync(cts.Token).ConfigureAwait(false);
        }
    }

    private static async Task<Vec3d> DrainUntilSpawnPositionAsync(
        JavaProtocolClient client,
        int expectedChunks,
        CancellationToken ct)
    {
        Vec3d? spawn = null;
        var chunks = 0;
        while (spawn is null || chunks < expectedChunks)
        {
            var pkt = await client.ReceivePacketAsync(ct).ConfigureAwait(false);
            if (pkt is SynchronizePlayerPositionPacket s)
            {
                spawn = s.Position;
            }
            else if (pkt is ChunkDataPacket)
            {
                chunks++;
            }
        }

        return spawn.Value;
    }

    private static async Task<TPacket> ReadNextPacketAsync<TPacket>(JavaProtocolClient client, CancellationToken ct)
        where TPacket : class, IClientboundPacket
    {
        while (true)
        {
            if (await client.ReceivePacketAsync(ct).ConfigureAwait(false) is TPacket matched)
            {
                return matched;
            }
        }
    }

    private sealed class StubMod(ModManifest manifest) : IModInitializer
    {
        public ModManifest Manifest { get; } = manifest;

        public void Initialize(IModContext context) => _ = context;
    }

    private sealed class SpawnProtectionReferenceMod : IModInitializer
    {
        private static readonly ResourceKey LampKey = new("guard_mod", "obsidian_lamp");

        public ModManifest Manifest { get; } = new("guard_mod", "Spawn Guard Reference Mod", "1.0.0");

        public void Initialize(IModContext context)
        {
            _ = context.Registries.RegisterBlock(new BlockDefinition(
                LampKey,
                lightEmission: 15,
                hardness: 5.0f));

            _ = context.Events.Subscribe<BlockBreakEvent>((ref BlockBreakEvent ev) =>
            {
                if (ev.Position.X == 0 && ev.Position.Z == 0)
                {
                    ev.IsCancelled = true;
                }
            });

            context.Commands.Register(new CommandRegistration(
                Name: "pingmod",
                Description: "Replies from GuardMod.",
                Usage: "/pingmod",
                RequiredPermission: PermissionLevel.Everyone,
                Executor: (ctx, _) =>
                {
                    ctx.Sender.Reply("Pong from GuardMod!");
                    return ValueTask.FromResult(true);
                }));
        }
    }
}
