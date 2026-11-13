using System.Diagnostics;
using System.Net.Sockets;

namespace OverHorizon.Client;

/// <summary>
/// Launches and manages an optional local <c>OverHorizon.Server</c> child process when a player clicks
/// "Host Local Server (25565) &amp; Play" inside the Multiplayer screen (searching root, <c>server/</c>, and <c>engine/</c>).
/// </summary>
internal sealed class LocalServerLauncher : IDisposable
{
    private Process? _serverProcess;

    public async ValueTask<bool> EnsureLocalServerRunningAsync(ushort port, CancellationToken ct)
    {
        if (await IsPortAcceptingAsync(port, ct).ConfigureAwait(false))
        {
            return true;
        }

        var serverExeName = OperatingSystem.IsWindows() ? "OverHorizon.Server.exe" : "OverHorizon.Server";
        var serverArgs = $"--port {port} --view-distance 4 --world-dir ./saves/multiplayer_host --op-all";

        string[] candidatePaths =
        [
            Path.Combine(AppContext.BaseDirectory, "server", serverExeName),
            Path.Combine(AppContext.BaseDirectory, serverExeName),
            Path.Combine(Directory.GetCurrentDirectory(), "server", serverExeName),
            Path.Combine(Directory.GetCurrentDirectory(), serverExeName),
        ];

        string? foundServerExe = null;
        for (var i = 0; i < candidatePaths.Length; i++)
        {
            if (File.Exists(candidatePaths[i]))
            {
                foundServerExe = candidatePaths[i];
                break;
            }
        }

        ProcessStartInfo psi;
        if (foundServerExe is not null)
        {
            psi = new ProcessStartInfo(foundServerExe, serverArgs)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory(),
            };
        }
        else if (File.Exists("src/OverHorizon.Server/OverHorizon.Server.csproj"))
        {
            psi = new ProcessStartInfo("dotnet", $"run -c Release --project src/OverHorizon.Server -- {serverArgs}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }
        else
        {
            return false;
        }

        _serverProcess = Process.Start(psi);
        for (var attempt = 0; attempt < 25; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(200, ct).ConfigureAwait(false);
            if (await IsPortAcceptingAsync(port, ct).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_serverProcess is null) return;
        try
        {
            if (!_serverProcess.HasExited)
            {
                _serverProcess.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        finally
        {
            _serverProcess.Dispose();
            _serverProcess = null;
        }
    }

    private static async ValueTask<bool> IsPortAcceptingAsync(ushort port, CancellationToken ct)
    {
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port, ct).ConfigureAwait(false);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
