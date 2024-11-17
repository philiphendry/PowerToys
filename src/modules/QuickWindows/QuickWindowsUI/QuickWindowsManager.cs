// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using Common.UI;
using ManagedCommon;
using PowerToys.Interop;
using QuickWindows.Features;
using QuickWindows.Keyboard;
using QuickWindows.Mouse;
using QuickWindows.Settings;

namespace QuickWindows;

public enum WindowOperation
{
    None,
    Move,
    Resize,
    Detect,
}

[Export(typeof(IQuickWindowsManager))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class QuickWindowsManager : IQuickWindowsManager
{
    private readonly WindowOperation _defaultOperation = WindowOperation.None;
    private readonly object _lock = new();
    private readonly IUserSettings _userSettings;
    private readonly IKeyboardMonitor _keyboardHook;
    private readonly IMouseHook _mouseHook;
    private readonly IMovingWindows _movingWindows;
    private readonly IResizingWindows _resizingWindows;
    private readonly ITransparentWindows _transparentWindows;
    private readonly IRolodexWindows _rolodexWindows;
    private readonly ICursorForOperation _cursorForOperation;
    private readonly IWindowIdentifier _windowIdentifier;
    private bool _isHotKeyPressed;
    private WindowOperation _currentOperation;

    [ImportingConstructor]
    public QuickWindowsManager(
        IUserSettings userSettings,
        IKeyboardMonitor keyboardHook,
        IMouseHook mouseHook,
        IMovingWindows movingWindows,
        IResizingWindows resizingWindows,
        ITransparentWindows transparentWindows,
        IRolodexWindows rolodexWindows,
        ICursorForOperation cursorForOperation,
        IWindowIdentifier windowIdentifier,
        CancellationToken exitToken)
    {
        _userSettings = userSettings;
        _keyboardHook = keyboardHook;
        _mouseHook = mouseHook;
        _movingWindows = movingWindows;
        _resizingWindows = resizingWindows;
        _transparentWindows = transparentWindows;
        _rolodexWindows = rolodexWindows;
        _cursorForOperation = cursorForOperation;
        _windowIdentifier = windowIdentifier;

        NativeEventWaiter.WaitForEventLoop(
            Constants.TerminateQuickWindowsSharedEvent(),
            Application.Current.Shutdown,
            Application.Current.Dispatcher,
            exitToken);

        NativeEventWaiter.WaitForEventLoop(
            Constants.QuickWindowsSendSettingsTelemetryEvent(),
            _userSettings.SendSettingsTelemetry,
            Application.Current.Dispatcher,
            exitToken);
    }

    public void ActivateHotKey()
    {
        try
        {
            Logger.LogDebug("Installing keyboard hook.");
            AddKeyboardListeners();
            AddMouseListeners();
            _keyboardHook.Install();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to install keyboard hook: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void DeactivateHotKey()
    {
        Logger.LogDebug("Uninstalling mouse and keyboard hooks.");
        _mouseHook.Uninstall();
        _keyboardHook.Uninstall();
        RemoveKeyboardListeners();
        RemoveMouseListeners();
    }

    private void OnHotKeyPressed(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_isHotKeyPressed)
            {
                return;
            }

            Logger.LogDebug("Installing mouse hook.");
            _mouseHook.Install();
            _isHotKeyPressed = true;
            _currentOperation = _defaultOperation;
        }
    }

    private void OnHotKeyReleased(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            Logger.LogDebug("EndOperation and uninstall mouse hook.");
            switch (_currentOperation)
            {
                case WindowOperation.Move:
                    _movingWindows.StopMove();
                    break;
                case WindowOperation.Resize:
                    _resizingWindows.StopResize();
                    break;
            }

            _cursorForOperation.HideCursor();
            _transparentWindows.EndTransparency();
            _mouseHook.Uninstall();
            _isHotKeyPressed = false;
            _currentOperation = _defaultOperation;
        }
    }

    private void OnMouseDown(object? target, MouseHook.MouseButtonEventArgs args)
    {
        lock (_lock)
        {
            if (_isHotKeyPressed && _currentOperation == WindowOperation.Detect)
            {
                _windowIdentifier.IdentifyWindow(args.X, args.Y);
                return;
            }

            if (!_isHotKeyPressed || _currentOperation != WindowOperation.None)
            {
                return;
            }

            switch (args.Button)
            {
                case MouseButton.Left:
                    _currentOperation = WindowOperation.Move;
                    _movingWindows.StartMove(args.X, args.Y);
                    _transparentWindows.StartTransparency(args.X, args.Y);
                    _cursorForOperation.StartMove(args.X, args.Y);
                    break;
                case MouseButton.Right:
                {
                    _currentOperation = WindowOperation.Resize;
                    var resizeOperation = _resizingWindows.StartResize(args.X, args.Y);

                    _transparentWindows.StartTransparency(args.X, args.Y);

                    switch (resizeOperation)
                    {
                        case ResizingWindows.ResizeOperation.ResizeTopLeft:
                        case ResizingWindows.ResizeOperation.ResizeBottomRight:
                            _cursorForOperation.StartResizeNorthWestSouthEast(args.X, args.Y);
                            break;
                        case ResizingWindows.ResizeOperation.ResizeTopRight:
                        case ResizingWindows.ResizeOperation.ResizeBottomLeft:
                            _cursorForOperation.StartResizeNorthEastSouthWest(args.X, args.Y);
                            break;
                    }

                    break;
                }
            }

            Logger.LogDebug($"StartOperation _currentOperation: {_currentOperation}");
        }
    }

    private void OnMouseUp(object? target, MouseHook.MouseButtonEventArgs args)
    {
        lock (_lock)
        {
            Logger.LogDebug("EndOperation");
            switch (_currentOperation)
            {
                case WindowOperation.Move:
                    _movingWindows.StopMove();
                    _cursorForOperation.HideCursor();
                    break;
                case WindowOperation.Resize:
                    _resizingWindows.StopResize();
                    _cursorForOperation.HideCursor();
                    break;
            }

            _transparentWindows.EndTransparency();
            _currentOperation = _defaultOperation;
        }
    }

    private void OnMouseMove(object? target, MouseHook.MouseMoveEventArgs args)
    {
        switch (_currentOperation)
        {
            case WindowOperation.Move:
                _movingWindows.MoveWindow(args.X, args.Y);
                _cursorForOperation.MoveToCursor(args.X, args.Y);
                break;
            case WindowOperation.Resize:
                _resizingWindows.ResizeWindow(args.X, args.Y);
                _cursorForOperation.MoveToCursor(args.X, args.Y);
                break;
        }
    }

    private void OnMouseWheel(object? target, MouseHook.MouseMoveWheelEventArgs args)
    {
        if (!_isHotKeyPressed)
        {
            return;
        }

        // Positive delta means wheel up, negative means wheel down
        if (args.Delta > 0)
        {
            _rolodexWindows.SendWindowToBottom(args.X, args.Y);
        }
        else
        {
            _rolodexWindows.BringBottomWindowToTop(args.X, args.Y);
        }
    }

    private void AddKeyboardListeners()
    {
        _keyboardHook.HotKeyPressed += OnHotKeyPressed;
        _keyboardHook.HotKeyReleased += OnHotKeyReleased;
    }

    private void AddMouseListeners()
    {
        _mouseHook.MouseDown += OnMouseDown;
        _mouseHook.MouseMove += OnMouseMove;
        _mouseHook.MouseUp += OnMouseUp;
        _mouseHook.MouseWheel += OnMouseWheel;
    }

    private void RemoveKeyboardListeners()
    {
        _keyboardHook.HotKeyPressed -= OnHotKeyPressed;
        _keyboardHook.HotKeyReleased -= OnHotKeyReleased;
    }

    private void RemoveMouseListeners()
    {
        _mouseHook.MouseDown -= OnMouseDown;
        _mouseHook.MouseMove -= OnMouseMove;
        _mouseHook.MouseUp -= OnMouseUp;
        _mouseHook.MouseWheel -= OnMouseWheel;
    }
}
