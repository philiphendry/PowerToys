// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Hosting;
using QuickWindows.Features;
using QuickWindows.Interfaces;
using QuickWindows.Keyboard;
using QuickWindows.Mouse;

namespace QuickWindows;

public class QuickWindowsManager(
    IKeyboardMonitor keyboardMonitor,
    IMouseHook mouseHook,
    ITargetWindow targetWindow,
    IMovingWindows movingWindows,
    IResizingWindows resizingWindows,
    ITransparentWindows transparentWindows,
    IRolodexWindows rolodexWindows,
    ICursorForOperation cursorForOperation,
    IExclusionDetector exclusionDetector,
    IExclusionFilter exclusionFilter,
    IRestoreMaximised restoreMaximised)
    : IQuickWindowsManager, IHostedService
{
    private readonly Lock _lock = new();
    private WindowOperation _currentOperation;

    internal bool IsActivated { get; private set; }

    internal bool IsHotKeyPressed { get; private set; }

    internal bool OperationInProgress { get; private set; }

    internal bool OperationHasOccurred { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ActivateHotKey();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        DeactivateHotKey();
        return Task.CompletedTask;
    }

    public void ActivateHotKey()
    {
        try
        {
            AddKeyboardListeners();
            AddMouseListeners();
            keyboardMonitor.Install();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to install keyboard hook: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void DeactivateHotKey()
    {
        mouseHook.Uninstall();
        keyboardMonitor.Uninstall();
        RemoveKeyboardListeners();
        RemoveMouseListeners();
    }

    private void OnHotKeyPressed(object? sender, HotKeyEventArgs e)
    {
        lock (_lock)
        {
            if (OperationInProgress)
            {
                return;
            }

            mouseHook.Install();

            IsActivated = true;
            IsHotKeyPressed = true;
            _currentOperation = WindowOperation.None;
        }
    }

    private void OnHotKeyReleased(object? sender, HotKeyEventArgs e)
    {
        lock (_lock)
        {
            IsHotKeyPressed = false;

            if (OperationInProgress && _currentOperation != WindowOperation.ExclusionDetection)
            {
                // Send control key when releasing Alt hot key prevents the window menus being activated.
                keyboardMonitor.SendControlKey();
                return;
            }

            if (OperationHasOccurred)
            {
                // Send control key when releasing Alt hot key prevents the window menus being activated.
                keyboardMonitor.SendControlKey();
                OperationHasOccurred = false;
            }

            EndOperation();
            mouseHook.Uninstall();
            IsActivated = false;
        }
    }

    private void EndOperation()
    {
        _currentOperation = WindowOperation.None;
        OperationInProgress = false;

        cursorForOperation.HideCursor();
        transparentWindows.EndTransparency();
        targetWindow.ClearTargetWindow();
    }

    private void OnMouseDown(object? target, MouseHook.MouseButtonEventArgs args)
    {
        lock (_lock)
        {
            if (!IsActivated)
            {
                return;
            }

            if (exclusionDetector.IsEnabled)
            {
                // Hide the cursor since otherwise we'll just detect the cursor window
                cursorForOperation.HideCursor();
                exclusionDetector.ExcludeWindowAtCursor();
                return;
            }

            if (exclusionFilter.IsWindowAtCursorExcluded())
            {
                return;
            }

            switch (args.Button)
            {
                case MouseButton.Left:
                    targetWindow.SetTargetWindow(args.X, args.Y);
                    if (!targetWindow.HaveTargetWindow)
                    {
                        return;
                    }

                    restoreMaximised.Start();
                    movingWindows.StartMove(args.X, args.Y);
                    transparentWindows.StartTransparency(args.X, args.Y);
                    cursorForOperation.StartMove(args.X, args.Y);

                    _currentOperation = WindowOperation.Move;
                    OperationInProgress = true;
                    break;

                case MouseButton.Right:
                    targetWindow.SetTargetWindow(args.X, args.Y);
                    if (!targetWindow.HaveTargetWindow)
                    {
                        return;
                    }

                    restoreMaximised.Start();
                    var resizeOperation = resizingWindows.StartResize(args.X, args.Y);
                    transparentWindows.StartTransparency(args.X, args.Y);
                    switch (resizeOperation)
                    {
                        case ResizeOperation.ResizeTopLeft:
                        case ResizeOperation.ResizeBottomRight:
                            cursorForOperation.StartResizeNorthWestSouthEast(args.X, args.Y);
                            break;
                        case ResizeOperation.ResizeTopRight:
                        case ResizeOperation.ResizeBottomLeft:
                            cursorForOperation.StartResizeNorthEastSouthWest(args.X, args.Y);
                            break;
                    }

                    _currentOperation = WindowOperation.Resize;
                    OperationInProgress = true;
                    break;
            }
        }
    }

    private void OnMouseUp(object? target, MouseHook.MouseButtonEventArgs args)
    {
        lock (_lock)
        {
            EndOperation();

            if (!IsHotKeyPressed)
            {
                mouseHook.Uninstall();
                IsActivated = false;
            }
        }
    }

    private void OnMouseMove(object? target, MouseHook.MouseMoveEventArgs args)
    {
        lock (_lock)
        {
            if (exclusionDetector.IsEnabled && _currentOperation == WindowOperation.None)
            {
                cursorForOperation.StartExclusionDetection(args.X, args.Y);
                _currentOperation = WindowOperation.ExclusionDetection;
                OperationInProgress = true;
            }

            if (!OperationInProgress)
            {
                return;
            }

            switch (_currentOperation)
            {
                case WindowOperation.Move:
                    restoreMaximised.Move();
                    movingWindows.MoveWindow(args.X, args.Y);
                    cursorForOperation.MoveToCursor(args.X, args.Y);
                    OperationHasOccurred = true;
                    break;

                case WindowOperation.Resize:
                    restoreMaximised.Resize();
                    resizingWindows.ResizeWindow(args.X, args.Y);
                    cursorForOperation.MoveToCursor(args.X, args.Y);
                    OperationHasOccurred = true;
                    break;

                case WindowOperation.ExclusionDetection:
                    cursorForOperation.MoveToCursor(args.X, args.Y);
                    OperationHasOccurred = true;
                    break;
            }
        }
    }

    private void OnMouseWheel(object? target, MouseHook.MouseMoveWheelEventArgs args)
    {
        lock (_lock)
        {
            if (!IsActivated)
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
    }

    private void AddKeyboardListeners()
    {
        keyboardMonitor.HotKeyPressed += OnHotKeyPressed;
        keyboardMonitor.HotKeyReleased += OnHotKeyReleased;
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
        keyboardMonitor.HotKeyPressed -= OnHotKeyPressed;
        keyboardMonitor.HotKeyReleased -= OnHotKeyReleased;
    }

    private void RemoveMouseListeners()
    {
        mouseHook.MouseDown -= OnMouseDown;
        mouseHook.MouseMove -= OnMouseMove;
        mouseHook.MouseUp -= OnMouseUp;
        mouseHook.MouseWheel -= OnMouseWheel;
    }
}
