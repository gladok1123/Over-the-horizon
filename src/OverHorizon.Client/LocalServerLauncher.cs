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

        var worldDir = Path.GetFullPath(string.IsNullOrWhiteSpace(customWorldDir) ? "./saves/multiplayer_host" : customWorldDir);
        var vd = Math.Clamp(viewDistance, 2, 16);

        var serverExeName = OperatingSystem.IsWindows() ? "OverHorizon.Server.exe" : "OverHorizon.Server";
        string[] candidateExePaths =
        [
            Path.Combine(AppContext.BaseDirectory, "server", serverExeName),
            Path.Combine(AppContext.BaseDirectory, serverExeName),
            Path.Combine(Directory.GetCurrentDirectory(), "server", serverExeName),
            Path.Combine(Directory.GetCurrentDirectory(), serverExeName),
        ];

        string[] candidateDllPaths =
        [
            Path.Combine(AppContext.BaseDirectory, "server", "OverHorizon.Server.dll"),
            Path.Combine(AppContext.BaseDirectory, "OverHorizon.Server.dll"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "OverHorizon.Server", "bin", "Release", "net9.0", "OverHorizon.Server.dll"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "OverHorizon.Server", "bin", "Debug", "net9.0", "OverHorizon.Server.dll"),
        ];

        string? foundServerExe = null;
        for (var i = 0; i < candidateExePaths.Length; i++)
        {
            if (File.Exists(candidateExePaths[i]))
            {
                foundServerExe = candidateExePaths[i];
                break;
            }
        }

        string? foundServerDll = null;
        if (foundServerExe is null)
        {
            for (var i = 0; i < candidateDllPaths.Length; i++)
            {
                if (File.Exists(candidateDllPaths[i]))
                {
                    foundServerDll = candidateDllPaths[i];
                    break;
                }
            }
        }

        static void AppendServerArguments(ProcessStartInfo info, ushort listenPort, int viewDist, string targetWorldDir, long? worldSeed)
        {
            info.ArgumentList.Add("--port");
            info.ArgumentList.Add(listenPort.ToString(CultureInfo.InvariantCulture));
            info.ArgumentList.Add("--view-distance");
            info.ArgumentList.Add(viewDist.ToString(CultureInfo.InvariantCulture));
            info.ArgumentList.Add("--world-dir");
            info.ArgumentList.Add(targetWorldDir);
            if (worldSeed.HasValue)
            {
                info.ArgumentList.Add("--seed");
                info.ArgumentList.Add(worldSeed.Value.ToString(CultureInfo.InvariantCulture));
            }

            info.ArgumentList.Add("--op-all");
        }

        ProcessStartInfo psi;
        if (foundServerExe is not null)
        {
            psi = new ProcessStartInfo(foundServerExe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory(),
            };
            AppendServerArguments(psi, port, vd, worldDir, seed);
        }
        else if (foundServerDll is not null)
        {
            psi = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory(),
            };
            psi.ArgumentList.Add(foundServerDll);
            AppendServerArguments(psi, port, vd, worldDir, seed);
        }
        else if (File.Exists("src/OverHorizon.Server/OverHorizon.Server.csproj"))
        {
            psi = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory(),
            };
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("Release");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add("src/OverHorizon.Server");
            psi.ArgumentList.Add("--");
            AppendServerArguments(psi, port, vd, worldDir, seed);
        }
        else
        {
            return false;
        }

        _serverProcess = Process.Start(psi);
        IsHostingLan = customWorldDir is not null;

        for (var attempt = 0; attempt < 55; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (_serverProcess is { HasExited: true })
            {
                return false;
            }

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
