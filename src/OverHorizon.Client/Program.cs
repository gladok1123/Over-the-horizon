using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using OverHorizon.Rendering;

namespace OverHorizon.Client;

/// <summary>
/// Entry point for the interactive Silk.NET OpenGL / headless voxel client executable.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        SilkSingleFileNativeResolver.EnsureRegistered();

        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            var parsedOptions = ClientBootstrap.ParseOptions(args);
            if (parsedOptions.VerifyNativesOnly)
            {
                var (glfwTarget, openAlTarget, platformName) = SilkSingleFileNativeResolver.VerifyNativeLibrariesLoaded();
                await Console.Out.WriteLineAsync($"[VerifyNatives] OK: Platform={platformName}, Glfw={glfwTarget}, OpenAL={openAlTarget}").ConfigureAwait(false);
                return 0;
            }

            var downloadedAssets = 0;
            if (!parsedOptions.Headless && !parsedOptions.HeadlessCheckOnly)
            {
                downloadedAssets = await Minecraft23w13aAssetSyncer.EnsureAssetsCachedAsync(parsedOptions.ExternalAssetsPath, cts.Token).ConfigureAwait(false);
            }

            using var host = ClientBootstrap.BuildHost(args, parsedOptions);
            await host.StartAsync(cts.Token).ConfigureAwait(false);

            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("OverHorizon.Client");
            var render = host.Services.GetRequiredService<IRenderSubsystem>();
            var audio = host.Services.GetRequiredService<IAudioSubsystem>();
            var options = host.Services.GetRequiredService<ClientOptions>();
            var runner = host.Services.GetRequiredService<ClientSessionRunner>();

            if (downloadedAssets > 0)
            {
                audio.ReloadSoundPack(options.ExternalAssetsPath);
            }

            var mode = string.IsNullOrWhiteSpace(options.ServerHost) ? "Singleplayer" : $"Multiplayer({options.ServerHost}:{options.ServerPort})";
            ClientLog.ClientInitialized(logger, render.BackendName, audio.IsInitialized, mode);

            if (!options.HeadlessCheckOnly)
            {
                await runner.InitializeSessionAsync(cts.Token).ConfigureAwait(false);
                await runner.RunAsync(cts.Token).ConfigureAwait(false);
            }

            await runner.DisposeAsync().ConfigureAwait(false);
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}
