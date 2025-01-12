// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using QuickWindows.Interfaces;
using QuickWindows.Settings;

namespace QuickWindows.Keyboard;

public class KeyboardMonitor(
    IGlobalKeyboardHook globalKeyboardHook,
    IUserSettings userSettings,
    IDisabledInGameMode disabledInGameMode)
    : IKeyboardMonitor, IDisposable
{
    private readonly List<string> _activationKeys = new();
    private bool _isHotKeyPressed;

    public event EventHandler<HotKeyEventArgs>? HotKeyPressed;

    public event EventHandler<HotKeyEventArgs>? HotKeyReleased;

    public void Install()
    {
        globalKeyboardHook.KeyboardPressed += Hook_KeyboardPressed;

        userSettings.ActivateOnAlt.PropertyChanged += (_, _) => SetActivationKeys();
        userSettings.ActivateOnCtrl.PropertyChanged += (_, _) => SetActivationKeys();
        userSettings.ActivateOnShift.PropertyChanged += (_, _) => SetActivationKeys();

        SetActivationKeys();
    }

    public void Uninstall()
    {
        globalKeyboardHook.KeyboardPressed -= Hook_KeyboardPressed;
    }

    public void SendControlKey()
    {
        var inputs = new NativeMethods.INPUT[2];

        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[0].u.ki.dwFlags = 0;

        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[1].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        var result = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
        if (result == 0)
        {
            Logger.LogError($"SendInput failed with error code {Marshal.GetLastWin32Error()}");
        }
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

        if (e.KeyboardState is GlobalKeyboardHook.KeyboardState.KeyDown or GlobalKeyboardHook.KeyboardState.SysKeyDown)
        {
            // Check pressed modifier keys.
            var name = Helper.GetKeyName((uint)virtualCode)
                .Replace(" (Left)", string.Empty)
                .Replace(" (Right)", string.Empty);
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

    /// <summary>
    /// If the hot key is pressed, an elevated window activate, and then the
    /// hot key is released then the keyboard hook doesn't receive the key up
    /// event and we're left in a state where the operation is still in progress.
    /// This check captures that scenario and ends the operation.
    /// </summary>
    /// <returns>True if the hot key is still active.</returns>
    public bool CheckHotKeyActive()
    {
        var currentlyPressedKeys = new List<string>();
        AddModifierKeys(currentlyPressedKeys);

        // Fetch the state of all keys
        for (int i = 1; i < 256; i++)
        {
            if ((NativeMethods.GetAsyncKeyState(i) & 0x8000) != 0)
            {
                var keyName = Helper.GetKeyName((uint)i)
                    .Replace(" (Left)", string.Empty)
                    .Replace(" (Right)", string.Empty);
                if (!currentlyPressedKeys.Contains(keyName))
                {
                    currentlyPressedKeys.Add(keyName);
                }
            }
        }

        currentlyPressedKeys.Sort();
        var isHotKeyActive = ArraysAreSame(currentlyPressedKeys, _activationKeys);
        if (_isHotKeyPressed && !isHotKeyActive)
        {
            _isHotKeyPressed = false;
            return false;
        }

        return true;
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
