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
}

[Export(typeof(IQuickWindowsManager))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class QuickWindowsManager : IQuickWindowsManager
{
    private static readonly object Lock = new();
    private readonly IUserSettings _userSettings;
    private readonly IKeyboardMonitor _keyboardHook;
    private readonly IMouseHook _mouseHook;
    private readonly IMovingWindows _movingWindows;
    private readonly IResizingWindows _resizingWindows;
    private readonly ITransparentWindows _transparentWindows;
    private readonly IRolodexWindows _rolodexWindows;
    private readonly ICursorForOperation _cursorForOperation;
    private bool _isAltPressed;
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

        AddKeyboardListeners();
        AddMouseListeners();

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
    }

    private void AddKeyboardListeners()
    {
        _keyboardHook.AltKeyPressed += OnAltKeyPressed;
        _keyboardHook.AltKeyReleased += OnAltKeyReleased;
    }

    private void AddMouseListeners()
    {
        _mouseHook.MouseDown += OnMouseDown;
        _mouseHook.MouseMove += OnMouseMove;
        _mouseHook.MouseUp += OnMouseUp;
        _mouseHook.MouseWheel += OnMouseWheel;
    }

    private void OnAltKeyPressed(object? sender, EventArgs e)
    {
        lock (Lock)
        {
            if (_isAltPressed)
            {
                return;
            }

            Logger.LogDebug("Installing mouse hook.");
            _mouseHook.Install();
            _isAltPressed = true;
            _currentOperation = WindowOperation.None;
        }
    }

    private void OnAltKeyReleased(object? sender, EventArgs e)
    {
        lock (Lock)
        {
            if (!_isAltPressed)
            {
                return;
            }

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
            _isAltPressed = false;
            _currentOperation = WindowOperation.None;
        }
    }

    private void OnMouseDown(object? target, MouseHook.MouseButtonEventArgs args)
    {
        lock (Lock)
        {
            if (!_isAltPressed)
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
        lock (Lock)
        {
            if (!(_currentOperation == WindowOperation.Move && args.Button == MouseButton.Left)
                && !(_currentOperation == WindowOperation.Resize && args.Button == MouseButton.Right))
            {
                return;
            }

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
            _currentOperation = WindowOperation.None;
        }
    }

    private void OnMouseMove(object? target, MouseHook.MouseMoveEventArgs args)
    {
        if (_currentOperation == WindowOperation.None)
        {
            return;
        }

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
        if (!_isAltPressed)
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
}
