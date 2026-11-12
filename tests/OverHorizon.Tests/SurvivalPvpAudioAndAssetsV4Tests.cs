using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core.Blocks;
using OverHorizon.Core.Items;
using OverHorizon.Core.Math;
using OverHorizon.Core.Registry;
using OverHorizon.Gameplay;
using OverHorizon.Gameplay.Crafting;
using OverHorizon.Gameplay.Inventory;
using OverHorizon.Gameplay.Survival;
using OverHorizon.Networking;
using OverHorizon.Networking.Client;
using OverHorizon.Networking.Packets.Clientbound;
using OverHorizon.Networking.Packets.Serverbound;
using OverHorizon.Rendering.Textures;
using OverHorizon.Server;
using OverHorizon.World.Storage;
using Xunit;

namespace OverHorizon.Tests;

public sealed class SurvivalPvpAudioAndAssetsV4Tests
{
    [Fact]
    public void Minecraft23w13aAssetSyncer_TargetsExactDoubleUnderscoreBranchWithBlocksItemsAndOggSounds()
    {
        Minecraft23w13aAssetSyncer.RawBaseUrl.Should()
            .Contain("InventivetalentDev/minecraft-assets/23w13a__or__b/assets/minecraft");

        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("textures/block/grass_block_top.png");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("textures/item/diamond_sword.png");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("textures/item/water_bucket.png");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/liquid/splash.ogg");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("sounds/ambient/underwater/underwater_ambience.ogg");
        Minecraft23w13aAssetSyncer.AssetManifest.Should().Contain("lang/en_us.json");
    }

    [Fact]
    public void RealRecordedOggSoundBank_DecodesEmbeddedCc0ClipsForEverySoundCueWithoutProceduralSynthesis()
    {
        var bank = ProceduralSoundBank.CreateDefaultBank();
        bank.Should().HaveCount(Enum.GetValues<SoundCue>().Length);

        foreach (var cue in Enum.GetValues<SoundCue>())
        {
            var clip = bank[cue];
            clip.SampleRate.Should().BeGreaterThanOrEqualTo(16000);
            clip.Samples.Length.Should().BeGreaterThan(1000);
        }
    }

    [Fact]
    public void SurvivalMiningDropsAndQuickCrafting_ProgressFromLogsToPickaxesAndSwords()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var items = registries.Items;

        var stoneDef = blocks.GetRequired(BuiltInBlocks.Stone);
        var diamondPick = items.GetRequired(BuiltInItems.DiamondPickaxe);
        var woodenPick = items.GetRequired(BuiltInItems.WoodenPickaxe);

        var bareHandSeconds = BlockDropResolver.ComputeBreakSeconds(stoneDef, heldTool: null);
        var woodSeconds = BlockDropResolver.ComputeBreakSeconds(stoneDef, woodenPick);
        var diamondSeconds = BlockDropResolver.ComputeBreakSeconds(stoneDef, diamondPick);
        var underwaterSeconds = BlockDropResolver.ComputeBreakSeconds(stoneDef, diamondPick, isEyeUnderwater: true);

        diamondSeconds.Should().BeLessThan(woodSeconds);
        woodSeconds.Should().BeLessThan(bareHandSeconds);
        underwaterSeconds.Should().BeGreaterThan(diamondSeconds);

        // Stone drops Cobblestone; Diamond Ore drops Diamond!
        var stoneDrop = BlockDropResolver.ResolveDrop(BuiltInBlocks.Stone, woodenPick, items);
        stoneDrop.ItemId.Should().Be((ushort)items.GetId(BuiltInBlocks.Cobblestone));

        var diamondDrop = BlockDropResolver.ResolveDrop(BuiltInBlocks.DiamondOre, diamondPick, items);
        diamondDrop.ItemId.Should().Be((ushort)items.GetId(BuiltInItems.Diamond));

        // Craft Oak Log -> 4 Planks -> 4 Sticks -> Wooden Sword!
        var inv = new PlayerInventory();
        _ = inv.AddItem(new ItemStack((ushort)items.GetId(BuiltInBlocks.OakLog), 2), items);

        var planksRecipe = QuickCraftCatalog.Entries[0];
        var sticksRecipe = QuickCraftCatalog.Entries[1];
        var swordRecipe = QuickCraftCatalog.Entries[4];

        QuickCraftCatalog.TryCraft(inv, planksRecipe, items).Should().BeTrue();
        QuickCraftCatalog.TryCraft(inv, planksRecipe, items).Should().BeTrue();
        QuickCraftCatalog.TryCraft(inv, sticksRecipe, items).Should().BeTrue();
        QuickCraftCatalog.TryCraft(inv, swordRecipe, items).Should().BeTrue();

        inv.CountItem((ushort)items.GetId(BuiltInItems.WoodenSword)).Should().Be(1);
    }

    [Fact]
    public void ClientPlayerController_SinksGentlyInWaterUntilJumpingAndDetectsSubmergedEye()
    {
        var registries = CoreRegistries.CreateDefault();
        var blocks = registries.Blocks;
        var atlas = ProceduralAtlasGenerator.CreateDefault(blocks);
        var cache = new ClientWorldCache(blocks, atlas);
        using var audio = new OpenAlAudioSubsystem(NullLogger<OpenAlAudioSubsystem>.Instance);

        var col = new ChunkColumn(new ChunkPos(0, 0));
        var stone = blocks.GetDefaultState(BuiltInBlocks.Stone);
        var water = blocks.GetDefaultState(BuiltInBlocks.Water);

        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                _ = col.SetBlockState(x, 60, z, stone, blocks);
                for (var y = 61; y <= 66; y++)
                {
                    _ = col.SetBlockState(x, y, z, water, blocks);
                }
            }
        }

        cache.UpsertColumn(col);

        var player = new ClientPlayerController(new Vec3d(8.5, 63.5, 8.5), blocks, registries.Items);
        player.SetGameMode(GameMode.Survival);

        // Passive in water (Jump = false) -> gently sinks, never ejected upward, and eye is underwater!
        for (var i = 0; i < 6; i++)
        {
            player.StepMovement(cache, blocks, audio, (0.0f, 0.0f, Jump: false, Sprint: false), 0.05);
        }

        player.InFluid.Should().BeTrue();
        player.IsHeadUnderwater.Should().BeTrue();
        player.FeetPosition.Y.Should().BeLessThan(63.5);
        player.AirSupplyNormalized.Should().BeLessThan(1.0f);

        // Holding Space (Jump = true) -> swims smoothly upward!
        var submergedY = player.FeetPosition.Y;
        for (var i = 0; i < 10; i++)
        {
            player.StepMovement(cache, blocks, audio, (0.0f, 0.0f, Jump: true, Sprint: false), 0.05);
        }

        player.FeetPosition.Y.Should().BeGreaterThan(submergedY);
    }

    [Fact]
    public async Task AuthoritativeMultiplayerPvp_AppliesDiamondSwordCriticalDamageKnockbackAndRespawnAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var registries = CoreRegistries.CreateDefault();
        var diamondSwordId = (ushort)registries.Items.GetId(BuiltInItems.DiamondSword);

        var serverOpts = new ServerOptions(TicksPerSecond: 100, ListenPort: 0, PlayerViewDistanceChunks: 1);
        using var serverHost = ServerBootstrap.BuildHost([], serverOpts);
        await serverHost.StartAsync(cts.Token).ConfigureAwait(false);

        try
        {
            var netServer = serverHost.Services.GetRequiredService<INetworkServer>();
            while (netServer.BoundEndPoint is null)
            {
                await Task.Delay(10, cts.Token).ConfigureAwait(false);
            }

            var ep = new IPEndPoint(IPAddress.Loopback, netServer.BoundEndPoint.Port);
            await using var attacker = await JavaProtocolClient.ConnectTcpAsync(ep, cts.Token).ConfigureAwait(false);
            await using var defender = await JavaProtocolClient.ConnectTcpAsync(ep, cts.Token).ConfigureAwait(false);

            var attackerLogin = await attacker.LoginAsync("Knight", cancellationToken: cts.Token).ConfigureAwait(false);
            var defenderLogin = await defender.LoginAsync("Rival", cancellationToken: cts.Token).ConfigureAwait(false);

            // Drain until both players receive initial spawn sync (within 3.8-block melee reach at spawn)
            Vec3d spawnPos = default;
            for (var i = 0; i < 60; i++)
            {
                var pkt = await defender.ReceivePacketAsync(cts.Token).ConfigureAwait(false);
                if (pkt is SynchronizePlayerPositionPacket sync)
                {
                    spawnPos = sync.Position;
                    break;
                }
            }

            await attacker.SendPlayPacketAsync(new MovePlayerPacket(spawnPos, 0.0f, 0.0f, OnGround: false), cts.Token).ConfigureAwait(false);
            await defender.SendPlayPacketAsync(new MovePlayerPacket(spawnPos + new Vec3d(0.0, 0.0, -1.8), 180.0f, 0.0f, OnGround: true), cts.Token).ConfigureAwait(false);
            await Task.Delay(40, cts.Token).ConfigureAwait(false);

            // Strike Rival with a critical Diamond Sword hit (7.0 * 1.5 = 10.5 damage -> 9.5 HP remaining!)
            await attacker.SendPlayPacketAsync(
                new PlayerAttackEntityPacket(defenderLogin.AssignedEntityId, diamondSwordId, IsCriticalHit: true),
                cts.Token).ConfigureAwait(false);

            EntityCombatEventPacket? combatEvent = null;
            for (var i = 0; i < 80; i++)
            {
                var pkt = await defender.ReceivePacketAsync(cts.Token).ConfigureAwait(false);
                if (pkt is EntityCombatEventPacket ce && ce.TargetEntityId == defenderLogin.AssignedEntityId)
                {
                    combatEvent = ce;
                    break;
                }
            }

            combatEvent.Should().NotBeNull();
            combatEvent!.AttackerEntityId.Should().Be(attackerLogin.AssignedEntityId);
            combatEvent.NewHealth.Should().BeApproximately(9.5f, 0.1f);
            combatEvent.TargetDied.Should().BeFalse();
            combatEvent.KnockbackVelocity.Z.Should().BeLessThan(-1.0);
        }
        finally
        {
            await serverHost.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
