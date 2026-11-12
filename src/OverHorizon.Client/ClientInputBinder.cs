using System.Numerics;
using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using OverHorizon.Rendering.Hud;
using Silk.NET.Input;

namespace OverHorizon.Client;

/// <summary>
/// Binds Silk.NET keyboard &amp; mouse input for first-person gameplay, the "C# EDITION" Title/Multiplayer/Options/Pause
/// menus, the 22-block Creative Palette (<c>E</c>), live Chat (<c>T</c> / <c>/</c>), and <c>F3</c> debug telemetry.
/// </summary>
internal sealed class ClientInputBinder
{
    private readonly ClientPlayerController _player;
    private readonly ClientMenuController _menu;
    private readonly IAudioSubsystem _audio;
    private readonly ILogger _logger;
    private readonly Func<string> _getLatestDebugText;
    private readonly Func<Vector2> _getViewportSize;
    private readonly Queue<string> _pendingUiActions = new();

    private Vector2 _lastMousePos;
    private bool _hasLastMousePos;
    private bool _wasRawCursor;
    private bool _suppressNextKeyChar;
    private bool _pendingBreakBlock;
    private bool _pendingPlaceBlock;
    private bool _bound;

    public ClientInputBinder(
        ClientPlayerController player,
        ClientMenuController menu,
        IAudioSubsystem audio,
        ILogger logger,
        Func<string> getLatestDebugText,
        Func<Vector2> getViewportSize)
    {
        _player = player;
        _menu = menu;
        _audio = audio;
        _logger = logger;
        _getLatestDebugText = getLatestDebugText;
        _getViewportSize = getViewportSize;
    }

    public (float Forward, float Strafe, bool Jump, bool Sprint) PollMovement(IInputContext? input)
    {
        if (!_menu.IsGameplayActive || input is null || input.Keyboards.Count == 0)
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

    public bool TryDequeueUiAction(out string actionId)
    {
        if (_pendingUiActions.Count > 0)
        {
            actionId = _pendingUiActions.Dequeue();
            return true;
        }

        actionId = string.Empty;
        return false;
    }

    public void EnsureBoundAndSyncCursor(IInputContext? input)
    {
        if (input is null)
        {
            return;
        }

        if (!_bound)
        {
            if (input.Keyboards.Count > 0)
            {
                var kb = input.Keyboards[0];
                kb.KeyDown += (keyboard, key, _) => OnKeyDown(keyboard, key);
                kb.KeyChar += (_, ch) => OnKeyChar(ch);
            }

            for (var i = 0; i < input.Mice.Count; i++)
            {
                var mouse = input.Mice[i];
                mouse.MouseMove += OnMouseMove;
                mouse.MouseDown += OnMouseDown;
                mouse.Scroll += OnMouseScroll;
            }

            _bound = true;
        }

        var wantRaw = _menu.IsGameplayActive;
        if (wantRaw != _wasRawCursor)
        {
            _wasRawCursor = wantRaw;
            _hasLastMousePos = false;
        }

        var desiredMode = wantRaw ? CursorMode.Raw : CursorMode.Normal;
        for (var i = 0; i < input.Mice.Count; i++)
        {
            if (input.Mice[i].Cursor.CursorMode != desiredMode)
            {
                input.Mice[i].Cursor.CursorMode = desiredMode;
            }
        }
    }

    private void OnKeyChar(char ch)
    {
        if (_suppressNextKeyChar)
        {
            _suppressNextKeyChar = false;
            return;
        }

        _menu.AppendTypedCharacter(ch);
    }

    private void OnKeyDown(IKeyboard kb, Key key)
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

        if (_menu.Mode is UiOverlayMode.ChatInput or UiOverlayMode.MultiplayerMenu)
        {
            if ((kb.IsKeyPressed(Key.ControlLeft) || kb.IsKeyPressed(Key.ControlRight)) && key == Key.V)
            {
                _menu.AppendPastedText(kb.ClipboardText);
                return;
            }

            if (key == Key.Backspace)
            {
                _menu.HandleBackspace();
                return;
            }

            if (key == Key.Tab && _menu.Mode == UiOverlayMode.MultiplayerMenu)
            {
                _menu.FocusedInputField = (_menu.FocusedInputField + 1) % 2;
                return;
            }

            if (key is Key.Enter or Key.KeypadEnter)
            {
                _pendingUiActions.Enqueue(_menu.Mode == UiOverlayMode.ChatInput ? "submit_chat" : "join_server");
                return;
            }
        }

        if (key == Key.Escape)
        {
            _menu.Mode = _menu.Mode switch
            {
                UiOverlayMode.InGame => UiOverlayMode.PauseMenu,
                UiOverlayMode.PauseMenu or UiOverlayMode.BlockInventory or UiOverlayMode.ChatInput => UiOverlayMode.InGame,
                UiOverlayMode.MultiplayerMenu or UiOverlayMode.OptionsMenu => UiOverlayMode.TitleMenu,
                _ => _menu.Mode,
            };
            _audio.PlayPositional(SoundCue.UiClick, _player.Camera.Position, gain: 0.25f * _player.MasterVolume);
            return;
        }

        if (_menu.Mode == UiOverlayMode.BlockInventory && key == Key.E)
        {
            _menu.Mode = UiOverlayMode.InGame;
            return;
        }

        if (!_menu.IsGameplayActive)
        {
            return;
        }

        if (key == Key.E)
        {
            _menu.Mode = UiOverlayMode.BlockInventory;
            _audio.PlayPositional(SoundCue.UiClick, _player.Camera.Position, gain: 0.25f * _player.MasterVolume);
            return;
        }

        if (key is Key.T or Key.Slash)
        {
            _menu.ChatDraft = key == Key.Slash ? "/" : string.Empty;
            _suppressNextKeyChar = true;
            _menu.Mode = UiOverlayMode.ChatInput;
            return;
        }

        if (key is >= Key.Number1 and <= Key.Number9)
        {
            var slot = (int)key - (int)Key.Number1;
            _player.Hotbar.SelectSlot(slot);
            _audio.PlayPositional(SoundCue.UiClick, _player.Camera.Position, gain: 0.25f * _player.MasterVolume);
        }
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (!_menu.IsGameplayActive)
        {
            if (button == MouseButton.Left)
            {
                var action = _menu.HandleLeftClick(mouse.Position, _getViewportSize(), _player);
                if (!string.IsNullOrEmpty(action))
                {
                    _audio.PlayPositional(SoundCue.UiClick, _player.Camera.Position, gain: 0.30f * _player.MasterVolume);
                    _pendingUiActions.Enqueue(action);
                }
            }

            return;
        }

        if (button == MouseButton.Left)
        {
            _player.TriggerHandSwing();
            _pendingBreakBlock = true;
        }
        else if (button == MouseButton.Right)
        {
            _player.TriggerHandSwing();
            _pendingPlaceBlock = true;
        }
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        _ = mouse;
        if (!_menu.IsGameplayActive || MathF.Abs(wheel.Y) < 0.01f)
        {
            return;
        }

        _player.Hotbar.CycleSlot(wheel.Y > 0.0f ? -1 : 1);
        _audio.PlayPositional(SoundCue.UiClick, _player.Camera.Position, gain: 0.2f * _player.MasterVolume);
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        _ = mouse;
        _menu.MousePosition = position;
        if (!_menu.IsGameplayActive)
        {
            return;
        }

        if (!_hasLastMousePos)
        {
            _lastMousePos = position;
            _hasLastMousePos = true;
            return;
        }

        var sens = _player.MouseSensitivity;
        var dx = (position.X - _lastMousePos.X) * sens;
        var dy = (_lastMousePos.Y - position.Y) * sens;
        _lastMousePos = position;
        _player.Camera.Rotate(dx, dy);
    }
}
