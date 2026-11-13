using System.Globalization;
using Microsoft.Extensions.Logging;
using OverHorizon.Rendering.Backends;
using OverHorizon.Rendering.Hud;

namespace OverHorizon.Client;

/// <summary>
/// Executes asynchronous UI menu transitions: creating/opening Singleplayer worlds in <c>./saves/</c>,
/// connecting to or hosting Multiplayer servers, and saving world chunks + metadata on Quit to Title.
/// </summary>
internal sealed class ClientMenuSessionActions : IDisposable
{
    private readonly ClientMenuController _menu;
    private readonly ClientChatHistory _chatHistory;
    private readonly ILogger _logger;
    private readonly LocalServerLauncher _localServerLauncher = new();

    private Task? _pendingTask;

    public ClientMenuSessionActions(
        ClientMenuController menu,
        ClientChatHistory chatHistory,
        ILogger logger)
    {
        _menu = menu;
        _chatHistory = chatHistory;
        _logger = logger;
    }

    public void DispatchPendingActions(
        ClientInputBinder inputBinder,
        SilkOpenGlRenderSubsystem silkRenderer,
        Action<string> sendChat,
        Func<SavedWorldMetadata, CancellationToken, Task> loadSavedWorldAsync,
        Func<CancellationToken, Task> saveAndReturnToTitleAsync,
        Func<ClientMultiplayerTransport> getTransport,
        Action<ClientMultiplayerTransport> setTransport,
        Func<Action, Task> enqueueMainThread,
        CancellationToken ct)
    {
        while (inputBinder.TryDequeueUiAction(out var action))
        {
            switch (action)
            {
                case "play_selected_world":
                    if (_pendingTask is { IsCompleted: false }) break;
                    if (_menu.SelectedSavedWorld is { } existingWorld)
                    {
                        _pendingTask = loadSavedWorldAsync(existingWorld, ct);
                    }

                    break;

                case "confirm_create_world":
                    if (_pendingTask is { IsCompleted: false }) break;
                    var created = WorldSaveManager.CreateWorld(_menu.NewWorldName, _menu.NewWorldGameMode, _menu.NewWorldSeed);
                    _menu.RefreshSavedWorlds();
                    _pendingTask = loadSavedWorldAsync(created, ct);
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
                    if (_pendingTask is { IsCompleted: false }) break;
                    _pendingTask = ConnectFromMenuAsync(hostLocalFirst: false, getTransport, setTransport, enqueueMainThread, ct);
                    break;

                case "host_and_join":
                    if (_pendingTask is { IsCompleted: false }) break;
                    _pendingTask = ConnectFromMenuAsync(hostLocalFirst: true, getTransport, setTransport, enqueueMainThread, ct);
                    break;

                case "save_and_title":
                    if (_pendingTask is { IsCompleted: false }) break;
                    _pendingTask = saveAndReturnToTitleAsync(ct);
                    break;
            }
        }
    }

    public async ValueTask AwaitPendingAsync()
    {
        if (_pendingTask is null) return;
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
        Func<Action, Task> enqueueMainThread,
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
            var login = await freshTransport.ConnectAndLoginAsync(host, port, user, ct).ConfigureAwait(false);
            ClientLog.MultiplayerConnected(_logger, host, port, login.Username, login.AssignedEntityId);

            await enqueueMainThread(() =>
            {
                setTransport(freshTransport);
                _menu.StatusMessage = string.Empty;
                _menu.Mode = UiOverlayMode.InGame;
                _chatHistory.AddMessage($"Connected to {host}:{port} as {user}! Type /help for commands.");
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _menu.StatusMessage = $"Connection failed: {ex.Message}";
        }
    }
}
