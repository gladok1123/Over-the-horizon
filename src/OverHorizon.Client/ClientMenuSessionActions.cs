using System.Globalization;
using Microsoft.Extensions.Logging;
using OverHorizon.Core.Math;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Hud;
using OverHorizon.World;

namespace OverHorizon.Client;

/// <summary>
/// Executes asynchronous UI menu transitions (connecting to a friend's server, hosting a local server,
/// or saving singleplayer chunks and returning to the "C# EDITION" Title Screen).
/// </summary>
internal sealed class ClientMenuSessionActions : IDisposable
{
    private readonly ClientMenuController _menu;
    private readonly ClientChatHistory _chatHistory;
    private readonly IWorldSubsystem _localWorld;
    private readonly ILogger _logger;
    private readonly LocalServerLauncher _localServerLauncher = new();

    private Task? _pendingTask;

    public ClientMenuSessionActions(
        ClientMenuController menu,
        ClientChatHistory chatHistory,
        IWorldSubsystem localWorld,
        ILogger logger)
    {
        _menu = menu;
        _chatHistory = chatHistory;
        _localWorld = localWorld;
        _logger = logger;
    }

    public void DispatchPendingActions(
        ClientInputBinder inputBinder,
        SilkOpenGlRenderSubsystem silkRenderer,
        ClientPlayerController player,
        double localSpawnY,
        Action<string> sendChat,
        Func<ClientMultiplayerTransport> getTransport,
        Action<ClientMultiplayerTransport> setTransport,
        CancellationToken ct)
    {
        while (inputBinder.TryDequeueUiAction(out var action))
        {
            switch (action)
            {
                case "singleplayer":
                    player.ApplyAuthoritativePose(new Vec3d(0.5, localSpawnY, 0.5), 0.0f, 0.0f);
                    _chatHistory.AddMessage("Entered Singleplayer World. Press E for Creative Blocks, T for Chat.");
                    break;

                case "quit_game":
                    silkRenderer.RequestClose();
                    break;

                case "submit_chat":
                    sendChat(_menu.ChatDraft);
                    _menu.ChatDraft = string.Empty;
                    _menu.Mode = UiOverlayMode.InGame;
                    break;

                case "join_server":
                    if (_pendingTask is { IsCompleted: false })
                    {
                        break;
                    }

                    _pendingTask = ConnectFromMenuAsync(hostLocalFirst: false, getTransport, setTransport, ct);
                    break;

                case "host_and_join":
                    if (_pendingTask is { IsCompleted: false })
                    {
                        break;
                    }

                    _pendingTask = ConnectFromMenuAsync(hostLocalFirst: true, getTransport, setTransport, ct);
                    break;

                case "save_and_title":
                    _pendingTask = ReturnToTitleAsync(getTransport, setTransport);
                    break;
            }
        }
    }

    public async ValueTask AwaitPendingAsync()
    {
        if (_pendingTask is null)
        {
            return;
        }

        try
        {
            await _pendingTask.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Ignored during shutdown.
        }
    }

    public void Dispose() => _localServerLauncher.Dispose();

    private async Task ConnectFromMenuAsync(
        bool hostLocalFirst,
        Func<ClientMultiplayerTransport> getTransport,
        Action<ClientMultiplayerTransport> setTransport,
        CancellationToken ct)
    {
        try
        {
            if (hostLocalFirst)
            {
                _menu.StatusMessage = "Starting local OverHorizon.Server on TCP 25565...";
                var started = await _localServerLauncher.EnsureLocalServerRunningAsync(25565, ct).ConfigureAwait(false);
                if (!started)
                {
                    _menu.StatusMessage = "Could not auto-start local server binary.";
                    return;
                }

                _menu.ServerEndpointInput = "127.0.0.1:25565";
            }

            var endpoint = _menu.ServerEndpointInput.Trim();
            var host = endpoint;
            ushort port = 25565;
            var colon = endpoint.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(endpoint.AsSpan(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort))
            {
                host = endpoint[..colon];
                port = parsedPort;
            }

            var user = string.IsNullOrWhiteSpace(_menu.UsernameInput) ? "Explorer" : _menu.UsernameInput;
            _menu.StatusMessage = $"Connecting to {host}:{port} as {user}...";

            await getTransport().DisposeAsync().ConfigureAwait(false);
            var freshTransport = new ClientMultiplayerTransport();
            setTransport(freshTransport);
            var login = await freshTransport.ConnectAndLoginAsync(host, port, user, ct).ConfigureAwait(false);
            ClientLog.MultiplayerConnected(_logger, host, port, login.Username, login.AssignedEntityId);

            _menu.StatusMessage = string.Empty;
            _menu.Mode = UiOverlayMode.InGame;
            _chatHistory.AddMessage($"Connected to {host}:{port} as {user}!");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _menu.StatusMessage = $"Connection failed: {ex.Message}";
        }
    }

    private async Task ReturnToTitleAsync(
        Func<ClientMultiplayerTransport> getTransport,
        Action<ClientMultiplayerTransport> setTransport)
    {
        var current = getTransport();
        if (current.IsConnected)
        {
            await current.DisposeAsync().ConfigureAwait(false);
            setTransport(new ClientMultiplayerTransport());
        }
        else
        {
            _ = await _localWorld.Chunks.SaveDirtyChunksAsync(CancellationToken.None).ConfigureAwait(false);
        }

        _menu.Mode = UiOverlayMode.TitleMenu;
    }
}
