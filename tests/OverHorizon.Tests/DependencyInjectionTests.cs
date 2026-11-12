using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OverHorizon.Audio;
using OverHorizon.Client;
using OverHorizon.Core;
using OverHorizon.Gameplay;
using OverHorizon.Modding;
using OverHorizon.Networking;
using OverHorizon.Physics;
using OverHorizon.Rendering;
using OverHorizon.Server;
using OverHorizon.World;
using Xunit;

namespace OverHorizon.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public async Task ServerHost_ResolvesAllModulesAndCompletesConfiguredTicksAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var options = new ServerOptions(TicksPerSecond: 200, MaxTicks: 3);

        using var host = ServerBootstrap.BuildHost([], options);

        var subsystems = host.Services.GetServices<ITickSubsystem>().ToArray();
        subsystems.Should().HaveCount(4);
        subsystems.Select(static s => s.Name).Should().ContainInOrder("Networking", "Gameplay", "Physics", "World");

        var gameplay = host.Services.GetRequiredService<IGameplaySubsystem>();
        gameplay.EntityCount.Should().Be(0);

        host.Services.GetRequiredService<IModHost>().LoadedModCount.Should().Be(0);

        await host.RunAsync(cts.Token).ConfigureAwait(false);

        var tickLoop = host.Services.GetRequiredService<ServerTickLoopService>();
        tickLoop.CompletedTicks.Should().Be(3);
    }

    [Fact]
    public void ClientHost_ResolvesClientAndSharedModules()
    {
        using var host = ClientBootstrap.BuildHost([]);

        host.Services.GetRequiredService<IRenderSubsystem>().BackendName.Should().Be("Silk.NET OpenGL 3.3");
        host.Services.GetRequiredService<IAudioSubsystem>().IsInitialized.Should().BeFalse();
        host.Services.GetRequiredService<IWorldSubsystem>().Should().NotBeNull();
        host.Services.GetRequiredService<IPhysicsSubsystem>().Should().NotBeNull();
        host.Services.GetRequiredService<IGameplaySubsystem>().EntityCount.Should().Be(0);
        host.Services.GetRequiredService<INetworkServer>().Should().NotBeNull();
        host.Services.GetRequiredService<IModHost>().Should().NotBeNull();
        host.Services.GetRequiredService<ClientSessionRunner>().Should().NotBeNull();
    }
}
