using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;

namespace OverHorizon.Client;

/// <summary>
/// Launches and manages a local <c>OverHorizon.Server</c> child process for both "Host Local Server &amp; Play"
/// and Pause Menu "Open to LAN", shutting it down cleanly when leaving the world or closing the game.
/// </summary>
internal sealed class LocalServerLauncher : IDisposable
{
    private Process? _serverProcess;

    public bool IsHostingLan { get; private set; }

    public async ValueTask<bool> EnsureLocalServerRunningAsync(
        ushort port,
        CancellationToken ct,
        string? customWorldDir = null,
        long? seed = null,
        int viewDistance = 6)
    {
        if (customWorldDir is not null)
        {
            // Restart owned server if switching to a specific singleplayer world for Open to LAN!
            StopServer();
        }
        else if (await IsPortAcceptingAsync(port, ct).ConfigureAwait(false))
        {
            return true;
        }

        var worldDir = string.IsNullOrWhiteSpace(customWorldDir) ? "./saves/multiplayer_host" : customWorldDir;
        var seedArg = seed.HasValue ? $" --seed {seed.Value.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        var vd = Math.Clamp(viewDistance, 2, 16);
        var serverArgs = $"--port {port} --view-distance {vd} --world-dir \"{worldDir}\"{seedArg} --op-all";

        var serverExeName = OperatingSystem.IsWindows() ? "OverHorizon.Server.exe" : "OverHorizon.Server";
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
        IsHostingLan = customWorldDir is not null;

        for (var attempt = 0; attempt < 30; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(180, ct).ConfigureAwait(false);
            if (await IsPortAcceptingAsync(port, ct).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    public void StopServer()
    {
        IsHostingLan = false;
        if (_serverProcess is null) return;
        try
        {
            if (!_serverProcess.HasExited)
            {
                _serverProcess.Kill(entireProcessTree: true);
                _ = _serverProcess.WaitForExit(1500);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already exited.
        }
        finally
        {
            _serverProcess.Dispose();
            _serverProcess = null;
        }
    }

    public void Dispose() => StopServer();

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
