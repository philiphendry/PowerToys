// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ManagedCommon;
using Microsoft.Extensions.Hosting;
using QuickWindows.Features;
using QuickWindows.Keyboard;
using QuickWindows.Mouse;

namespace QuickWindows;

public enum WindowOperation
{
    None,
    Move,
    Resize,
    Detect,
}

public class QuickWindowsManager(
    IKeyboardMonitor keyboardHook,
    IMouseHook mouseHook,
    IMovingWindows movingWindows,
    IResizingWindows resizingWindows,
    ITransparentWindows transparentWindows,
    IRolodexWindows rolodexWindows,
    ICursorForOperation cursorForOperation,
    IWindowIdentifier windowIdentifier)
    : IQuickWindowsManager, IHostedService
{
    private readonly WindowOperation _defaultOperation = WindowOperation.None;
    private readonly object _lock = new();
    private bool _isHotKeyPressed;
    private WindowOperation _currentOperation;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Quick windows hosted service starting.");
        ActivateHotKey();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.LogDebug("Quick windows hosted service stopping.");
        DeactivateHotKey();
        return Task.CompletedTask;
    }

    public void ActivateHotKey()
    {
        try
        {
            Logger.LogDebug("Installing keyboard hook.");
            AddKeyboardListeners();
            AddMouseListeners();
            keyboardHook.Install();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to install keyboard hook: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void DeactivateHotKey()
    {
        Logger.LogDebug("Uninstalling mouse and keyboard hooks.");
        mouseHook.Uninstall();
        keyboardHook.Uninstall();
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
            mouseHook.Install();
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
                    movingWindows.StopMove();
                    break;
                case WindowOperation.Resize:
                    resizingWindows.StopResize();
                    break;
            }

            cursorForOperation.HideCursor();
            transparentWindows.EndTransparency();
            mouseHook.Uninstall();
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
                windowIdentifier.IdentifyWindow(args.X, args.Y);
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
                    movingWindows.StartMove(args.X, args.Y);
                    transparentWindows.StartTransparency(args.X, args.Y);
                    cursorForOperation.StartMove(args.X, args.Y);
                    break;
                case MouseButton.Right:
                {
                    _currentOperation = WindowOperation.Resize;
                    var resizeOperation = resizingWindows.StartResize(args.X, args.Y);

                    transparentWindows.StartTransparency(args.X, args.Y);

                    switch (resizeOperation)
                    {
                        case ResizingWindows.ResizeOperation.ResizeTopLeft:
                        case ResizingWindows.ResizeOperation.ResizeBottomRight:
                            cursorForOperation.StartResizeNorthWestSouthEast(args.X, args.Y);
                            break;
                        case ResizingWindows.ResizeOperation.ResizeTopRight:
                        case ResizingWindows.ResizeOperation.ResizeBottomLeft:
                            cursorForOperation.StartResizeNorthEastSouthWest(args.X, args.Y);
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
                    movingWindows.StopMove();
                    cursorForOperation.HideCursor();
                    break;
                case WindowOperation.Resize:
                    resizingWindows.StopResize();
                    cursorForOperation.HideCursor();
                    break;
            }

            transparentWindows.EndTransparency();
            _currentOperation = _defaultOperation;
        }
    }

    private void OnMouseMove(object? target, MouseHook.MouseMoveEventArgs args)
    {
        switch (_currentOperation)
        {
            case WindowOperation.Move:
                movingWindows.MoveWindow(args.X, args.Y);
                cursorForOperation.MoveToCursor(args.X, args.Y);
                break;
            case WindowOperation.Resize:
                resizingWindows.ResizeWindow(args.X, args.Y);
                cursorForOperation.MoveToCursor(args.X, args.Y);
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
            rolodexWindows.SendWindowToBottom(args.X, args.Y);
        }
        else
        {
            rolodexWindows.BringBottomWindowToTop(args.X, args.Y);
        }
    }

    private void AddKeyboardListeners()
    {
        keyboardHook.HotKeyPressed += OnHotKeyPressed;
        keyboardHook.HotKeyReleased += OnHotKeyReleased;
    }

    private void AddMouseListeners()
    {
        mouseHook.MouseDown += OnMouseDown;
        mouseHook.MouseMove += OnMouseMove;
        mouseHook.MouseUp += OnMouseUp;
        mouseHook.MouseWheel += OnMouseWheel;
    }

    private void RemoveKeyboardListeners()
    {
        keyboardHook.HotKeyPressed -= OnHotKeyPressed;
        keyboardHook.HotKeyReleased -= OnHotKeyReleased;
    }

    private void RemoveMouseListeners()
    {
        mouseHook.MouseDown -= OnMouseDown;
        mouseHook.MouseMove -= OnMouseMove;
        mouseHook.MouseUp -= OnMouseUp;
        mouseHook.MouseWheel -= OnMouseWheel;
    }
}
