using Microsoft.Extensions.Hosting;

namespace OverHorizon.Server;

/// <summary>
/// Entry point for the dedicated server executable.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            using var host = ServerBootstrap.BuildHost(args);
            await host.RunAsync(cts.Token).ConfigureAwait(false);
            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}
