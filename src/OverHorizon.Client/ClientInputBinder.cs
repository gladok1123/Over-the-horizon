using System.Numerics;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;

namespace OverHorizon.Client;

/// <summary>
/// Binds Silk.NET keyboard (WASD, Space, F3) and raw-cursor mouse-look events to <see cref="ClientPlayerController"/>.
/// </summary>
internal sealed class ClientInputBinder
{
    private readonly ClientPlayerController _player;
    private readonly ILogger _logger;
    private readonly Func<string> _getLatestDebugText;

    private Vector2 _lastMousePos;
    private bool _hasLastMousePos;
    private bool _bound;

    public ClientInputBinder(
        ClientPlayerController player,
        ILogger logger,
        Func<string> getLatestDebugText)
    {
        _player = player;
        _logger = logger;
        _getLatestDebugText = getLatestDebugText;
    }

    public static (float Forward, float Strafe, bool Jump) PollMovement(IInputContext? input)
    {
        if (input is null || input.Keyboards.Count == 0)
        {
            return (0.0f, 0.0f, false);
        }

        var kb = input.Keyboards[0];
        var fwd = (kb.IsKeyPressed(Key.W) ? 1.0f : 0.0f) - (kb.IsKeyPressed(Key.S) ? 1.0f : 0.0f);
        var strafe = (kb.IsKeyPressed(Key.D) ? 1.0f : 0.0f) - (kb.IsKeyPressed(Key.A) ? 1.0f : 0.0f);
        return (fwd, strafe, kb.IsKeyPressed(Key.Space));
    }

    public void EnsureBound(IInputContext? input)
    {
        if (_bound || input is null)
        {
            return;
        }

        if (input.Keyboards.Count > 0)
        {
            input.Keyboards[0].KeyDown += (_, key, _) =>
            {
                if (key == Key.F3)
                {
                    _player.IsDebugOverlayVisible = !_player.IsDebugOverlayVisible;
                    var text = _getLatestDebugText();
                    if (_player.IsDebugOverlayVisible && !string.IsNullOrEmpty(text))
                    {
                        ClientLog.DebugTelemetrySnapshot(_logger, text);
                    }
                }
            };
        }

        for (var i = 0; i < input.Mice.Count; i++)
        {
            input.Mice[i].Cursor.CursorMode = CursorMode.Raw;
            input.Mice[i].MouseMove += OnMouseMove;
        }

        _bound = true;
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        _ = mouse;
        if (!_hasLastMousePos)
        {
            _lastMousePos = position;
            _hasLastMousePos = true;
            return;
        }

        var dx = (position.X - _lastMousePos.X) * 0.12f;
        var dy = (_lastMousePos.Y - position.Y) * 0.12f;
        _lastMousePos = position;
        _player.Camera.Rotate(dx, dy);
    }
}
