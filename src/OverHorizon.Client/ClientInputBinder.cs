using System.Numerics;
using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using Silk.NET.Input;

namespace OverHorizon.Client;

/// <summary>
/// Binds Silk.NET keyboard (WASD, Space, Shift/Ctrl sprint, 1..9 hotbar, Escape cursor release, F3)
/// and mouse (raw look, Left-Click break, Right-Click place, ScrollWheel hotbar) to <see cref="ClientPlayerController"/>.
/// </summary>
internal sealed class ClientInputBinder
{
    private readonly ClientPlayerController _player;
    private readonly IAudioSubsystem _audio;
    private readonly ILogger _logger;
    private readonly Func<string> _getLatestDebugText;

    private Vector2 _lastMousePos;
    private bool _hasLastMousePos;
    private bool _cursorCaptured = true;
    private bool _pendingBreakBlock;
    private bool _pendingPlaceBlock;
    private bool _bound;

    public ClientInputBinder(
        ClientPlayerController player,
        IAudioSubsystem audio,
        ILogger logger,
        Func<string> getLatestDebugText)
    {
        _player = player;
        _audio = audio;
        _logger = logger;
        _getLatestDebugText = getLatestDebugText;
    }

    public static (float Forward, float Strafe, bool Jump, bool Sprint) PollMovement(IInputContext? input)
    {
        if (input is null || input.Keyboards.Count == 0)
        {
            return (0.0f, 0.0f, false, false);
        }

        var kb = input.Keyboards[0];
        var fwd = (kb.IsKeyPressed(Key.W) ? 1.0f : 0.0f) - (kb.IsKeyPressed(Key.S) ? 1.0f : 0.0f);
        var strafe = (kb.IsKeyPressed(Key.D) ? 1.0f : 0.0f) - (kb.IsKeyPressed(Key.A) ? 1.0f : 0.0f);
        var jump = kb.IsKeyPressed(Key.Space);
        var sprint = kb.IsKeyPressed(Key.ShiftLeft) || kb.IsKeyPressed(Key.ControlLeft);
        return (fwd, strafe, jump, sprint);
    }

    public (bool BreakRequested, bool PlaceRequested) ConsumeBlockActionRequests()
    {
        var brk = _pendingBreakBlock;
        var plc = _pendingPlaceBlock;
        _pendingBreakBlock = false;
        _pendingPlaceBlock = false;
        return (brk, plc);
    }

    public void EnsureBound(IInputContext? input)
    {
        if (_bound || input is null)
        {
            return;
        }

        if (input.Keyboards.Count > 0)
        {
            input.Keyboards[0].KeyDown += (_, key, _) => OnKeyDown(input, key);
        }

        for (var i = 0; i < input.Mice.Count; i++)
        {
            var mouse = input.Mice[i];
            mouse.Cursor.CursorMode = CursorMode.Raw;
            mouse.MouseMove += OnMouseMove;
            mouse.MouseDown += OnMouseDown;
            mouse.Scroll += OnMouseScroll;
        }

        _bound = true;
    }

    private void OnKeyDown(IInputContext input, Key key)
    {
        if (key == Key.F3)
        {
            _player.IsDebugOverlayVisible = !_player.IsDebugOverlayVisible;
            var text = _getLatestDebugText();
            if (_player.IsDebugOverlayVisible && !string.IsNullOrEmpty(text))
            {
                ClientLog.DebugTelemetrySnapshot(_logger, text);
            }

            return;
        }

        if (key == Key.Escape)
        {
            _cursorCaptured = !_cursorCaptured;
            _hasLastMousePos = false;
            var mode = _cursorCaptured ? CursorMode.Raw : CursorMode.Normal;
            for (var i = 0; i < input.Mice.Count; i++)
            {
                input.Mice[i].Cursor.CursorMode = mode;
            }

            return;
        }

        if (key is >= Key.Number1 and <= Key.Number9)
        {
            var slot = (int)key - (int)Key.Number1;
            _player.Hotbar.SelectSlot(slot);
            _audio.PlayPositional(SoundCue.UiClick, _player.Camera.Position, gain: 0.25f);
        }
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (!_cursorCaptured)
        {
            _cursorCaptured = true;
            _hasLastMousePos = false;
            mouse.Cursor.CursorMode = CursorMode.Raw;
            return;
        }

        if (button == MouseButton.Left)
        {
            _pendingBreakBlock = true;
        }
        else if (button == MouseButton.Right)
        {
            _pendingPlaceBlock = true;
        }
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        _ = mouse;
        if (MathF.Abs(wheel.Y) < 0.01f)
        {
            return;
        }

        _player.Hotbar.CycleSlot(wheel.Y > 0.0f ? -1 : 1);
        _audio.PlayPositional(SoundCue.UiClick, _player.Camera.Position, gain: 0.2f);
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        _ = mouse;
        if (!_cursorCaptured)
        {
            return;
        }

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
