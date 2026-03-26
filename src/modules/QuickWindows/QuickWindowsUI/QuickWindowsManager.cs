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
using QuickWindows.Interfaces;
using QuickWindows.Keyboard;
using QuickWindows.Mouse;

namespace QuickWindows;

public class QuickWindowsManager(
    IKeyboardMonitor keyboardMonitor,
    IMouseHook mouseHook,
    ITargetWindow targetWindow,
    IMovingWindows movingWindows,
    IFancyZonesBridge fancyZonesBridge,
    IResizingWindows resizingWindows,
    ITransparentWindows transparentWindows,
    IRolodexWindows rolodexWindows,
    ICursorForOperation cursorForOperation,
    IExclusionDetector exclusionDetector,
    IExclusionFilter exclusionFilter,
    IRestoreMaximised restoreMaximised)
    : IQuickWindowsManager, IHostedService, IDisposable
{
    private readonly Lock _lock = new();
    private Timer? _stateLoggerTimer;

    internal bool IsHotKeyActivated { get; private set; }

    internal bool OperationInProgress { get; private set; }

    /// <summary>
    /// Gets a value indicating whether an operation (move/resize) has occurred since the hotkey was pressed
    /// so the on release of the hotkey (in particular Alt) SendControlKey can be called to cancel menu activation.
    /// </summary>
    internal bool OperationHasOccurred { get; private set; }

    internal WindowOperation CurrentOperation { get; private set; }

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
            keyboardMonitor.Install();
            mouseHook.Install();
            cursorForOperation.Install();
            AddKeyboardListeners();
            AddMouseListeners();
#if DEBUG
            StartStateLogger();
#endif
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to install keyboard hook: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void DeactivateHotKey()
    {
        RemoveMouseListeners();
        RemoveKeyboardListeners();
        cursorForOperation.Uninstall();
        mouseHook.Uninstall();
        keyboardMonitor.Uninstall();
#if DEBUG
        StopStateLogger();
#endif
    }

    private void OnHotKeyPressed(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (OperationInProgress)
            {
                return;
            }

            Logger.LogDebug("Hot key pressed, starting operation.");
            mouseHook.EnableEvents();
            IsHotKeyActivated = true;
            OperationHasOccurred = false;
            CurrentOperation = WindowOperation.None;
        }
    }

    private void OnHotKeyReleased(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            IsHotKeyActivated = false;

            if (OperationInProgress && CurrentOperation != WindowOperation.ExclusionDetection)
            {
                // Intentional: do NOT end the operation here. The user has released the hotkey
                // while still holding the mouse button — the drag/resize should continue until
                // mouse-up fires OnMouseUp, which calls EndOperation() and DisableEvents().
                // SendControlKey cancels any pending Alt-menu activation from the key release.
                Logger.LogDebug("Operation in progress - sending control key");
                keyboardMonitor.SendControlKey();
                return;
            }

            if (OperationHasOccurred)
            {
                Logger.LogDebug("Operation has occurred - sending control key");
                keyboardMonitor.SendControlKey();
                OperationHasOccurred = false;
            }

            Logger.LogDebug("Hot key released, ending operation.");
            EndOperation();
            mouseHook.DisableEvents();
        }
    }

    private void EndOperation()
    {
        CurrentOperation = WindowOperation.None;
        OperationInProgress = false;

        fancyZonesBridge.EndMove(targetWindow.HWnd);
        cursorForOperation.HideCursor();
        transparentWindows.EndTransparency();
        targetWindow.ClearTargetWindow();
    }

    private void OnMouseDown(object? target, MouseButtonEventArgs args)
    {
        lock (_lock)
        {
            if (!keyboardMonitor.CheckHotKeyActive())
            {
                Logger.LogDebug("Hot key released without event raised, ending operation.");
                EndOperation();
                mouseHook.DisableEvents();
                return;
            }

            if (IsHotKeyActivated && exclusionDetector.IsEnabled)
            {
                cursorForOperation.HideCursor();
                exclusionDetector.ExcludeWindowAtCursor();
                return;
            }

            if (OperationInProgress)
            {
                Logger.LogDebug("Another mouse down whilst operation in progress so ending operation.");
                EndOperation();

                if (!IsHotKeyActivated)
                {
                    mouseHook.DisableEvents();
                }

                return;
            }

            if (!IsHotKeyActivated)
            {
                Logger.LogDebug("Hot key not activated, ignoring mouse down event.");
                return;
            }

            if (exclusionFilter.IsWindowAtCursorExcluded())
            {
                Logger.LogDebug("Window at cursor is excluded, ignoring mouse down event.");
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
                    fancyZonesBridge.StartMove(targetWindow.HWnd);

                    CurrentOperation = WindowOperation.Move;
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

                    CurrentOperation = WindowOperation.Resize;
                    OperationInProgress = true;
                    break;
            }
        }
    }

    private void OnMouseUp(object? target, MouseButtonEventArgs args)
    {
        lock (_lock)
        {
            EndOperation();

            if (!IsHotKeyActivated)
            {
                mouseHook.DisableEvents();
            }
        }
    }

    private void OnMouseMove(object? target, MouseMoveEventArgs args)
    {
        lock (_lock)
        {
            if (exclusionDetector.IsEnabled && CurrentOperation == WindowOperation.None)
            {
                cursorForOperation.StartExclusionDetection(args.X, args.Y);
                CurrentOperation = WindowOperation.ExclusionDetection;
                OperationInProgress = true;
            }

            if (!OperationInProgress)
            {
                return;
            }

            switch (CurrentOperation)
            {
                case WindowOperation.Move:
                    fancyZonesBridge.UpdateMove(targetWindow.HWnd);
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

    private void OnMouseWheel(object? target, MouseMoveWheelEventArgs args)
    {
        lock (_lock)
        {
            if (!IsHotKeyActivated || OperationInProgress)
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

    public void StartStateLogger()
    {
        _stateLoggerTimer = new Timer(LogCurrentState, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    public void StopStateLogger()
    {
        _stateLoggerTimer?.Dispose();
        _stateLoggerTimer = null;
    }

    private void LogCurrentState(object? state)
    {
        Logger.LogDebug($"IsHotKeyActivated: {IsHotKeyActivated}, OperationInProgress: {OperationInProgress}, OperationHasOccurred: {OperationHasOccurred}, CurrentOperation: {CurrentOperation}");
    }

    public void Dispose()
    {
        DeactivateHotKey();
        _stateLoggerTimer?.Dispose();
        keyboardMonitor.Dispose();
        GC.SuppressFinalize(this);
    }
}
