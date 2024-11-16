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
    private readonly IWindowOperations _windowOperations;
    private bool _isAltPressed;
    private WindowOperation _currentOperation;

    [ImportingConstructor]
    public QuickWindowsManager(
        IUserSettings userSettings,
        IKeyboardMonitor keyboardHook,
        IMouseHook mouseHook,
        IWindowOperations windowOperations,
        CancellationToken exitToken)
    {
        _userSettings = userSettings;
        _keyboardHook = keyboardHook;
        _mouseHook = mouseHook;
        _windowOperations = windowOperations;

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

    private void OnAltKeyPressed(object? o, KeyboardMonitor.KeyPressedEventArgs keyPressedEventArgs)
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

    private void OnAltKeyReleased(object? o, KeyboardMonitor.KeyPressedEventArgs keyPressedEventArgs)
    {
        lock (Lock)
        {
            if (!_isAltPressed)
            {
                return;
            }

            Logger.LogDebug("EndOperation and uninstall mouse hook.");
            _windowOperations.EndOperation();
            _mouseHook.Uninstall();
            _isAltPressed = false;
            _currentOperation = WindowOperation.None;
        }
    }

    private void OnMouseDown(object? target, MouseHook.MouseEventArgs args)
    {
        lock (Lock)
        {
            if (!_isAltPressed || _currentOperation != WindowOperation.None)
            {
                return;
            }

            _currentOperation = args.Button == MouseButton.Left ? WindowOperation.Move : WindowOperation.Resize;
            Logger.LogDebug($"StartOperation _currentOperation: {_currentOperation}");
            _windowOperations.StartOperation(args.X, args.Y, _currentOperation);
        }
    }

    private void OnMouseUp(object? target, MouseHook.MouseEventArgs args)
    {
        lock (Lock)
        {
            if (!_isAltPressed || (args.Button != MouseButton.Left && args.Button != MouseButton.Right))
            {
                return;
            }

            Logger.LogDebug("EndOperation");
            _windowOperations.EndOperation();
            _currentOperation = WindowOperation.None;
        }
    }

    private void OnMouseMove(object? target, MouseHook.MouseEventArgs args)
    {
        if (!_isAltPressed && args.Button != MouseButton.Left && args.Button != MouseButton.Right)
        {
            return;
        }

        if (_currentOperation == WindowOperation.Move)
        {
            _windowOperations.MoveWindowWithMouse(args.X, args.Y);
        }
        else if (_currentOperation == WindowOperation.Resize)
        {
            _windowOperations.ResizeWindowWithMouse(args.X, args.Y);
        }
    }

    private void OnMouseWheel(object? target, MouseHook.MouseWheelEventArgs args)
    {
        if (!_isAltPressed)
        {
            return;
        }

        // Positive delta means wheel up, negative means wheel down
        if (args.Delta > 0)
        {
            _windowOperations.SendWindowToBottom(args.X, args.Y);
        }
        else
        {
            _windowOperations.BringBottomWindowToTop(args.X, args.Y);
        }
    }
}
