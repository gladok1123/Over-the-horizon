using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using OverHorizon.Audio;
using OverHorizon.Gameplay;
using OverHorizon.Rendering.Hud;
using Silk.NET.Input;

namespace OverHorizon.Client;

/// <summary>
/// Binds Silk.NET keyboard &amp; mouse input for first-person gameplay, Middle-Click Pick Block,
/// <c>Q</c> / <c>Ctrl+Q</c> 3D Item Drop, Offhand Swap (<c>F</c>), 3-way GameMode cycle (<c>F4</c>),
/// X-Ray toggle (<c>X</c>), double-click World Select, Options <c>Esc</c> return, Containers, Chat, and <c>F3</c>.
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
    private readonly Stopwatch _doubleTapWatch = Stopwatch.StartNew();

    private Vector2 _lastMousePos;
    private double _lastSpaceTapSeconds = -10.0;
    private bool _hasLastMousePos;
    private bool _wasRawCursor;
    private bool _suppressNextKeyChar;
    private bool _pendingLeftClick;
    private bool _pendingRightClick;
    private bool _pendingMiddlePick;
    private int _pendingDropCount; // 0=none, 1=single, 64=full stack
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

    public ((float Forward, float Strafe, bool Jump, bool Sprint) Move, bool Descend) PollMovement(IInputContext? input)
    {
        if (!_menu.IsGameplayActive || input is null || input.Keyboards.Count == 0)
        {
            return ((0.0f, 0.0f, false, false), false);
        }

        var kb = input.Keyboards[0];
        var fwd = (kb.IsKeyPressed(Key.W) ? 1.0f : 0.0f) - (kb.IsKeyPressed(Key.S) ? 1.0f : 0.0f);
        var strafe = (kb.IsKeyPressed(Key.D) ? 1.0f : 0.0f) - (kb.IsKeyPressed(Key.A) ? 1.0f : 0.0f);
        var jump = kb.IsKeyPressed(Key.Space);
        var descend = kb.IsKeyPressed(Key.C) || kb.IsKeyPressed(Key.ShiftLeft);
        var sprint = kb.IsKeyPressed(Key.ControlLeft);
        return ((fwd, strafe, jump, sprint), descend);
    }

    public bool IsLeftMouseHeld(IInputContext? input)
    {
        if (!_menu.IsGameplayActive || input is null) return false;
        for (var i = 0; i < input.Mice.Count; i++)
        {
            if (input.Mice[i].IsButtonPressed(MouseButton.Left)) return true;
        }

        return false;
    }

    public bool IsRightMouseHeld(IInputContext? input)
    {
        if (!_menu.IsGameplayActive || input is null) return false;
        for (var i = 0; i < input.Mice.Count; i++)
        {
            if (input.Mice[i].IsButtonPressed(MouseButton.Right)) return true;
        }

        return false;
    }

    public (bool LeftClickRequested, bool RightClickRequested, bool MiddlePickRequested, int DropCountRequested) ConsumeActionRequests()
    {
        var lmb = _pendingLeftClick;
        var rmb = _pendingRightClick;
        var mmb = _pendingMiddlePick;
        var drop = _pendingDropCount;
        _pendingLeftClick = false;
        _pendingRightClick = false;
        _pendingMiddlePick = false;
        _pendingDropCount = 0;
        return (lmb, rmb, mmb, drop);
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
        if (input is null) return;

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
                mouse.MouseUp += OnMouseUp;
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

        if (key == Key.F4 && _menu.Mode is UiOverlayMode.InGame or UiOverlayMode.BlockInventory or UiOverlayMode.SurvivalInventory)
        {
            _menu.CloseContainerAndRefundCursor(_player);
            var next = _player.CycleNextGameMode();
            if (_menu.Mode is UiOverlayMode.BlockInventory or UiOverlayMode.SurvivalInventory)
            {
                _menu.Mode = next switch
                {
                    GameMode.Spectator => UiOverlayMode.InGame,
                    GameMode.Survival => UiOverlayMode.SurvivalInventory,
                    _ => UiOverlayMode.BlockInventory,
                };
            }

            _audio.PlayPositional(SoundCue.UiClick, _player.Camera.Position, gain: 0.35f * _player.MasterVolume);
            return;
        }

        if (_menu.Mode is UiOverlayMode.ChatInput or UiOverlayMode.MultiplayerMenu or UiOverlayMode.CreateWorldMenu or UiOverlayMode.BlockInventory)
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

            if (key == Key.Tab && _menu.Mode is UiOverlayMode.MultiplayerMenu or UiOverlayMode.CreateWorldMenu)
            {
                _menu.FocusedInputField = (_menu.FocusedInputField + 1) % 2;
                return;
            }

            if (key is Key.Enter or Key.KeypadEnter && _menu.Mode != UiOverlayMode.BlockInventory)
            {
                var submitAct = _menu.Mode switch
                {
                    UiOverlayMode.ChatInput => "submit_chat",
                    UiOverlayMode.CreateWorldMenu => "confirm_create_world",
                    _ => "join_server",
                };
                _pendingUiActions.Enqueue(submitAct);
                return;
            }
        }

        if (_menu.Mode == UiOverlayMode.WorldSelectMenu && key is Key.Enter or Key.KeypadEnter)
        {
            _pendingUiActions.Enqueue("play_selected_world");
            return;
        }

        if (key == Key.Escape)
        {
            if (_menu.Mode == UiOverlayMode.OptionsMenu)
            {
                // Esc in Options saves options.json and returns to the exact menu it was opened from (PauseMenu or TitleMenu)!
                _menu.CloseOptionsAndReturn(_player);
            }
            else if (_menu.Mode is UiOverlayMode.BlockInventory or UiOverlayMode.SurvivalInventory or UiOverlayMode.CraftingTableScreen or UiOverlayMode.FurnaceScreen)
            {
                _menu.CloseContainerAndRefundCursor(_player);
                _menu.Mode = UiOverlayMode.InGame;
            }
            else
            {
                _menu.Mode = _menu.Mode switch
                {
                    UiOverlayMode.InGame => UiOverlayMode.PauseMenu,
                    UiOverlayMode.PauseMenu or UiOverlayMode.ChatInput => UiOverlayMode.InGame,
                    UiOverlayMode.MultiplayerMenu or UiOverlayMode.WorldSelectMenu => UiOverlayMode.TitleMenu,
                    UiOverlayMode.CreateWorldMenu => UiOverlayMode.WorldSelectMenu,
                    _ => _menu.Mode,
                };
            }

            _audio.PlayPositional(SoundCue.UiClick, _player.Camera.Position, gain: 0.25f * _player.MasterVolume);
            return;
        }

        if (_menu.Mode == UiOverlayMode.BlockInventory && key is >= Key.Number1 and <= Key.Number9)
        {
            var slot = (int)key - (int)Key.Number1;
            if (_menu.TryAssignHoveredCreativeItemToSlot(_getViewportSize(), _player, slot))
            {
                _suppressNextKeyChar = true;
                _audio.PlayPositional(SoundCue.ItemPop, _player.Camera.Position, gain: 0.45f * _player.MasterVolume);
                return;
            }
        }

        if (_menu.Mode is UiOverlayMode.SurvivalInventory or UiOverlayMode.CraftingTableScreen or UiOverlayMode.FurnaceScreen && key == Key.E)
        {
            _menu.CloseContainerAndRefundCursor(_player);
            _menu.Mode = UiOverlayMode.InGame;
            return;
        }

        if (_menu.Mode == UiOverlayMode.BlockInventory && key == Key.E && string.IsNullOrEmpty(_menu.CreativeSearchQuery))
        {
            _suppressNextKeyChar = true;
            _menu.CloseContainerAndRefundCursor(_player);
            _menu.Mode = UiOverlayMode.InGame;
            return;
        }

        if (!_menu.IsGameplayActive) return;

        // Q / Ctrl+Q: Throw 3D Dropped Item into the world!
        if (key == Key.Q && _player.Mode != GameMode.Spectator)
        {
            var fullStack = kb.IsKeyPressed(Key.ControlLeft) || kb.IsKeyPressed(Key.ControlRight);
            _pendingDropCount = fullStack ? 64 : 1;
            return;
        }

        // X: Quick-toggle X-Ray inspection mode!
        if (key == Key.X)
        {
            _menu.ToggleXRayMode(_player);
            _audio.PlayPositional(SoundCue.UiClick, _player.Camera.Position, gain: 0.30f * _player.MasterVolume);
            return;
        }

        // Offhand Swap on F!
        if (key == Key.F && _player.Mode != GameMode.Spectator)
        {
            _player.Hotbar.SwapMainHandAndOffhand();
            _player.Hotbar.RefreshSlotVisuals(_player.Mode);
            _audio.PlayPositional(SoundCue.ItemPop, _player.Camera.Position, gain: 0.40f * _player.MasterVolume);
            return;
        }

        // Creative Flight: Double-tap Space OR press G!
        if (_player.Mode == GameMode.Creative && key == Key.Space)
        {
            var now = _doubleTapWatch.Elapsed.TotalSeconds;
            if (now - _lastSpaceTapSeconds <= 0.32)
            {
                _ = _player.ToggleCreativeFlight();
                _lastSpaceTapSeconds = -10.0;
                return;
            }

            _lastSpaceTapSeconds = now;
        }
        else if (_player.Mode == GameMode.Creative && key == Key.G)
        {
            _ = _player.ToggleCreativeFlight();
            return;
        }

        if (key == Key.E && _player.Mode != GameMode.Spectator)
        {
            _suppressNextKeyChar = true;
            _menu.OpenOrCloseInventory(_player);
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
            if (button is MouseButton.Left or MouseButton.Right)
            {
                var action = _menu.HandleContainerOrMenuClick(mouse.Position, _getViewportSize(), _player, isRightClick: button == MouseButton.Right);
                if (!string.IsNullOrEmpty(action))
                {
                    var cue = action is "crafted_item" or "pick_creative" ? SoundCue.ItemPop : SoundCue.UiClick;
                    _audio.PlayPositional(cue, _player.Camera.Position, gain: 0.38f * _player.MasterVolume);
                    _pendingUiActions.Enqueue(action);
                }
            }

            return;
        }

        if (button == MouseButton.Left) _pendingLeftClick = true;
        else if (button == MouseButton.Right) _pendingRightClick = true;
        else if (button == MouseButton.Middle) _pendingMiddlePick = true;
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        _ = mouse;
        if (button == MouseButton.Left) _menu.ReleaseSliderDrag();
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        _ = mouse;
        if (MathF.Abs(wheel.Y) < 0.01f) return;

        if (_menu.Mode == UiOverlayMode.BlockInventory)
        {
            _menu.ScrollInventory(wheel.Y > 0.0f ? 1 : -1);
            return;
        }

        if (!_menu.IsGameplayActive) return;

        _player.Hotbar.CycleSlot(wheel.Y > 0.0f ? -1 : 1);
        _audio.PlayPositional(SoundCue.UiClick, _player.Camera.Position, gain: 0.2f * _player.MasterVolume);
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        _menu.MousePosition = position;
        if (!_menu.IsGameplayActive)
        {
            if (mouse.IsButtonPressed(MouseButton.Left))
            {
                _menu.HandleSliderDrag(position, _getViewportSize(), _player);
            }

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
        var dy = (_lastMousePos.Y - position.Y) * sens * (_player.InvertMouseY ? -1.0f : 1.0f);
        _lastMousePos = position;
        _player.Camera.Rotate(dx, dy);
    }
}
