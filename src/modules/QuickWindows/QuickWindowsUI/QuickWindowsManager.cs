// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using Common.UI;
using PowerToys.Interop;
using QuickWindows.Keyboard;
using QuickWindows.Mouse;
using QuickWindows.Settings;

namespace QuickWindows;

[Export(typeof(IQuickWindowsManager))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class QuickWindowsManager : IQuickWindowsManager
{
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
        _keyboardHook.AltKeyPressed += (_, _) =>
        {
            _isAltPressed = true;
            _mouseHook.Install();
            _mouseHook.SuppressLeftClick = true;
        };

        _keyboardHook.AltKeyReleased += (_, _) =>
        {
            _isAltPressed = false;
            WindowOperations.EndWindowDrag();
            _mouseHook.SuppressLeftClick = false;
            _mouseHook.Uninstall();
        };

        _mouseHook.MouseDown += (_, args) =>
        {
            if (!_isAltPressed)
            {
                return;
            }

            _isMoveOperation = args.Button == MouseButton.Left;
            WindowOperations.StartWindowOperation(args.X, args.Y, _isMoveOperation);
        };

        _mouseHook.MouseMove += (_, args) =>
        {
            if (!_isAltPressed)
            {
                return;
            }

            if (_isMoveOperation)
            {
                WindowOperations.MoveWindowWithMouse(args.X, args.Y);
            }
            else
            {
                WindowOperations.ResizeWindowWithMouse(args.X, args.Y);
            }
        };

        _mouseHook.MouseUp += (_, args) =>
        {
            if (args.Button != MouseButton.Left && args.Button != MouseButton.Right)
            {
                return;
            }

            WindowOperations.EndWindowDrag();
        };

        _mouseHook.MouseWheel += (_, args) =>
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
        };

        try
        {
            _keyboardHook.Install();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to install keyboard hook: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void DeactivateHotKey()
    {
        _mouseHook.Uninstall();
        _keyboardHook.Uninstall();
    }
}
