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

[Export(typeof(IQuickWindowsManager))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class QuickWindowsManager : IQuickWindowsManager
{
    private static readonly object Lock = new object();
    private readonly IUserSettings _userSettings;
    private readonly IKeyboardMonitor _keyboardHook;
    private readonly IMouseHook _mouseHook;
    private bool _isAltPressed;
    private bool _isMoveOperation;

    [ImportingConstructor]
    public QuickWindowsManager(
        IUserSettings userSettings,
        IKeyboardMonitor keyboardHook,
        IMouseHook mouseHook,
        CancellationToken exitToken)
    {
        _userSettings = userSettings;
        _keyboardHook = keyboardHook;
        _mouseHook = mouseHook;

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
            _isAltPressed = true;
            _mouseHook.Install();
            _mouseHook.SuppressLeftClick = true;
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

            Logger.LogDebug("EndWindowDrag and uninstall mouse hook.");
            _isAltPressed = false;
            WindowOperations.EndWindowDrag();
            _mouseHook.SuppressLeftClick = false;
            _mouseHook.Uninstall();
        }
    }

    private void OnMouseDown(object? target, MouseHook.MouseEventArgs args)
    {
        lock (Lock)
        {
            if (!_isAltPressed)
            {
                return;
            }

            _isMoveOperation = args.Button == MouseButton.Left;
            Logger.LogDebug($"StartWindowOperation _isMoveOperation: {_isMoveOperation}");
            WindowOperations.StartWindowOperation(args.X, args.Y, _isMoveOperation);
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

            Logger.LogDebug("EndWindowDrag");
            WindowOperations.EndWindowDrag();
        }
    }

    private void OnMouseMove(object? target, MouseHook.MouseEventArgs args)
    {
        if (!_isAltPressed && args.Button != MouseButton.Left && args.Button != MouseButton.Right)
        {
            return;
        }

        Logger.LogDebug($"_isMoveOperation: {_isMoveOperation}");
        if (_isMoveOperation)
        {
            WindowOperations.MoveWindowWithMouse(args.X, args.Y);
        }
        else
        {
            WindowOperations.ResizeWindowWithMouse(args.X, args.Y);
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
            WindowOperations.SendWindowToBottom(args.X, args.Y);
        }
        else
        {
            WindowOperations.BringBottomWindowToTop(args.X, args.Y);
        }
    }
}
