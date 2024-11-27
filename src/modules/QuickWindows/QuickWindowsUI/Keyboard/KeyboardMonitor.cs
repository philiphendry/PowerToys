// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using QuickWindows.Interfaces;
using QuickWindows.Settings;

namespace QuickWindows.Keyboard;

public class KeyboardMonitor(
    IUserSettings userSettings,
    IDisabledInGameMode disabledInGameMode)
    : IKeyboardMonitor, IDisposable
{
    private readonly List<string> _activationKeys = new();
    private GlobalKeyboardHook? _keyboardHook;
    private bool _isHotKeyPressed;

    public event EventHandler<HotKeyEventArgs>? HotKeyPressed;

    public event EventHandler<HotKeyEventArgs>? HotKeyReleased;

    public void Install()
    {
        _keyboardHook = new GlobalKeyboardHook();
        _keyboardHook.KeyboardPressed += Hook_KeyboardPressed;

        userSettings.ActivateOnAlt.PropertyChanged += (_, _) => SetActivationKeys();
        userSettings.ActivateOnCtrl.PropertyChanged += (_, _) => SetActivationKeys();
        userSettings.ActivateOnShift.PropertyChanged += (_, _) => SetActivationKeys();

        SetActivationKeys();
    }

    public void Uninstall()
    {
        _keyboardHook?.Dispose();
        _keyboardHook = null;
    }

    private void SetActivationKeys()
    {
        _activationKeys.Clear();

        if (userSettings.ActivateOnAlt.Value)
        {
            _activationKeys.Add("Alt");
        }

        if (userSettings.ActivateOnCtrl.Value)
        {
            _activationKeys.Add("Ctrl");
        }

        if (userSettings.ActivateOnShift.Value)
        {
            _activationKeys.Add("Shift");
        }

        _activationKeys.Sort();
    }

    private void Hook_KeyboardPressed(object? sender, GlobalKeyboardHookEventArgs e)
    {
        if (disabledInGameMode.IsDisabledInGameMode())
        {
            e.Handled = DeactivateHotKey();
            return;
        }

        var currentlyPressedKeys = new List<string>();
        var virtualCode = e.KeyboardData.VirtualCode;

        // ESC pressed
        if (virtualCode == KeyInterop.VirtualKeyFromKey(Key.Escape) && e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown)
        {
            e.Handled = DeactivateHotKey();
            return;
        }

        var name = Helper.GetKeyName((uint)virtualCode)
            .Replace(" (Left)", string.Empty)
            .Replace(" (Right)", string.Empty);

        if (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown || e.KeyboardState == GlobalKeyboardHook.KeyboardState.SysKeyDown)
        {
            // Check pressed modifier keys.
            AddModifierKeys(currentlyPressedKeys);
            if (!currentlyPressedKeys.Contains(name))
            {
                currentlyPressedKeys.Add(name);
            }
        }

        currentlyPressedKeys.Sort();

        if (ArraysAreSame(currentlyPressedKeys, _activationKeys))
        {
            // avoid triggering this action multiple times as this will be called nonstop while keys are pressed
            if (!_isHotKeyPressed)
            {
                var eventArgs = new HotKeyEventArgs();
                HotKeyPressed?.Invoke(this, eventArgs);
                if (!eventArgs.SuppressHotKey)
                {
                    _isHotKeyPressed = true;
                }
            }
        }
        else
        {
            e.Handled = DeactivateHotKey();
        }
    }

    private bool DeactivateHotKey()
    {
        if (!_isHotKeyPressed)
        {
            return false;
        }

        _isHotKeyPressed = false;
        var eventArgs = new HotKeyEventArgs();
        HotKeyReleased?.Invoke(this, eventArgs);
        return eventArgs.SuppressHotKey;
    }

    private static bool ArraysAreSame(List<string> first, List<string> second)
    {
        if (first.Count != second.Count || (first.Count == 0 && second.Count == 0))
        {
            return false;
        }

        for (int i = 0; i < first.Count; i++)
        {
            if (first[i] != second[i])
            {
                return false;
            }
        }

        return true;
    }

    private static void AddModifierKeys(List<string> currentlyPressedKeys)
    {
        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0
            || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LSHIFT) & 0x8000) != 0
            || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RSHIFT) & 0x8000) != 0)
        {
            currentlyPressedKeys.Add("Shift");
        }

        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0
            || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LCONTROL) & 0x8000) != 0
            || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RCONTROL) & 0x8000) != 0)
        {
            currentlyPressedKeys.Add("Ctrl");
        }

        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0
            || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LMENU) & 0x8000) != 0
            || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RMENU) & 0x8000) != 0)
        {
            currentlyPressedKeys.Add("Alt");
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        Uninstall();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
