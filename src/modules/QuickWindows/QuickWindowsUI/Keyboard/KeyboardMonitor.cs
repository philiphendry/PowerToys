// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ManagedCommon;
using QuickWindows.Interfaces;
using QuickWindows.Settings;

namespace QuickWindows.Keyboard;

public class KeyboardMonitor(
    IGlobalKeyboardHook globalKeyboardHook,
    IUserSettings userSettings,
    IDisabledInGameMode disabledInGameMode)
    : IKeyboardMonitor
{
    private readonly Lock _lock = new();
    private readonly byte[] _keyStates = new byte[256];

    private bool _isAltActive;
    private bool _isCtrlActive;
    private bool _isShiftActive;
    private bool _isHotKeyPressed;
    private bool _suppressHotKey;

    public event EventHandler? HotKeyPressed;

    public event EventHandler? HotKeyReleased;

    private static readonly string[] KeyNames = Enumerable.Range(0, 256).Select(i => ((Keys)i).ToString()).ToArray();

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

    private void SetActivationKeys()
    {
        _isAltActive = userSettings.ActivateOnAlt.Value;
        _isCtrlActive = userSettings.ActivateOnCtrl.Value;
        _isShiftActive = userSettings.ActivateOnShift.Value;
    }

    private void Hook_KeyboardPressed(object? sender, GlobalKeyboardHookEventArgs e)
    {
        lock (_lock)
        {
            if (_suppressHotKey)
            {
                return;
            }

            if (disabledInGameMode.IsDisabledInGameMode())
            {
                return;
            }

            var isHotKeyPressed = IsHotKeyPressed((uint)e.KeyboardData.VirtualCode, e.KeyboardState);
            if (_isHotKeyPressed && !isHotKeyPressed)
            {
                DeactivateHotKey();
            }
            else if (!_isHotKeyPressed && isHotKeyPressed)
            {
                ActivateHotKey();
            }
        }
    }

    private void ActivateHotKey()
    {
        _isHotKeyPressed = true;
        HotKeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private void DeactivateHotKey()
    {
        _isHotKeyPressed = false;
        HotKeyReleased?.Invoke(this, EventArgs.Empty);
    }

    private bool IsHotKeyPressed(uint? virtualKeyCode = null, GlobalKeyboardHook.KeyboardState? keyState = null)
    {
        if (!_isAltActive && !_isCtrlActive && !_isShiftActive)
        {
            // There isn't a hotkey configured to check for.
            return false;
        }

        CaptureKeyState(_keyStates, virtualKeyCode, keyState);
        var (isAltPressed, isCtrlPressed, isShiftPressed, isOtherPressed) = WhatIsPressed(_keyStates, _isAltActive, _isCtrlActive, _isShiftActive);

        if (!isAltPressed && !isCtrlPressed && !isShiftPressed && !isOtherPressed)
        {
            // Nothing is pressed
            return false;
        }

        if (isOtherPressed)
        {
            // Something other than the hotkey is pressed
            return false;
        }

        // Test whether the configured hotkey is pressed
        var isHotKeyPressed = (!_isAltActive || isAltPressed)
            && (!_isCtrlActive || isCtrlPressed)
            && (!_isShiftActive || isShiftPressed);

        return isHotKeyPressed;
    }

    private static (bool IsAltPressed, bool IsCtrlPressed, bool IsShiftPressed, bool IsOtherPressed) WhatIsPressed(
        byte[] keyStates,
        bool isAltActive,
        bool isCtrlActive,
        bool isShiftActive)
    {
        var isAltPressed = false;
        var isCtrlPressed = false;
        var isShiftPressed = false;
        var isOtherPressed = false;

        for (var i = 0; i < 256; i++)
        {
            // Check if the high-order bit isn't set and ignore keys that aren't pressed.
            if ((keyStates[i] & 0x80) == 0)
            {
                continue;
            }

            var keyName = KeyNames[i];
            if (keyName is "LButton" or "MButton" or "RButton")
            {
                continue;
            }

            if (isAltActive && keyName is "Menu" or "LMenu")
            {
                isAltPressed = true;
            }
            else if (isCtrlActive && keyName is "ControlKey" or "LControlKey" or "RControlKey")
            {
                isCtrlPressed = true;
            }
            else if (isShiftActive && keyName is "ShiftKey" or "LShiftKey" or "RShiftKey")
            {
                isShiftPressed = true;
            }
            else
            {
                isOtherPressed = true;
            }
        }

        return (isAltPressed, isCtrlPressed, isShiftPressed, isOtherPressed);
    }

    private static void CaptureKeyState(byte[] keyStates, uint? virtualKeyCode, GlobalKeyboardHook.KeyboardState? keyState)
    {
        // I would like to use GetKeyboardState here, but it doesn't work as expected.
        // NativeMethods.GetKeyState(0);
        // NativeMethods.GetKeyboardState(_keyboardState);
        for (var i = 0; i < keyStates.Length; i++)
        {
            var key = NativeMethods.GetAsyncKeyState(i);
            keyStates[i] = (key & 0x8000) == 0 ? (byte)0x00 : (byte)0x80;
        }

        if (virtualKeyCode is null || keyState is null)
        {
            return;
        }

        // Because we're called from a keyboard hook that informs us of a key in the
        // process of being pressed or released, we need to update the key state for the
        // key that triggered the event.
        var newKeyState = keyState.GetValueOrDefault() is GlobalKeyboardHook.KeyboardState.SysKeyDown or GlobalKeyboardHook.KeyboardState.KeyDown
            ? (byte)0x80
            : (byte)0x00;
        var newKeyName = KeyNames[virtualKeyCode.Value];
        if (newKeyName is "Menu" or "LMenu")
        {
            keyStates[(int)Keys.Menu] = newKeyState;
            keyStates[(int)Keys.LMenu] = newKeyState;
        }
        else if (newKeyName == "LControlKey")
        {
            keyStates[(int)Keys.ControlKey] = newKeyState;
            keyStates[(int)Keys.LControlKey] = newKeyState;
        }
        else if (newKeyName is "RControlKey" or "Control")
        {
            keyStates[(int)Keys.ControlKey] = newKeyState;
            keyStates[(int)Keys.LControlKey] = newKeyState;
        }
        else if (newKeyName is "RShiftKey" or "Shift")
        {
            keyStates[(int)Keys.ShiftKey] = newKeyState;
            keyStates[(int)Keys.LShiftKey] = newKeyState;
        }
        else if (newKeyName is "RShiftKey" or "Shift")
        {
            keyStates[(int)Keys.ShiftKey] = newKeyState;
            keyStates[(int)Keys.LShiftKey] = newKeyState;
        }
    }

    /// <summary>
    /// If the hot key is pressed, an elevated window activated, and then the
    /// hot key is released then the keyboard hook doesn't receive the key up
    /// event and we're left in a state where the operation is still in progress.
    /// This check captures that scenario and ends the operation.
    /// </summary>
    /// <returns>True if the hot key is still active.</returns>
    public bool CheckHotKeyActive()
    {
        var isHotKeyActive = IsHotKeyPressed();

        if (_isHotKeyPressed && !isHotKeyActive)
        {
            _isHotKeyPressed = false;
            return false;
        }

        return true;
    }

    public void SendControlKey()
    {
        // If we don't suppress the hot key then the callback is triggered, we end up reading
        // the hotkey as pressed (don't know why) and then the _isHotKeyPressed is set active.
        _suppressHotKey = true;

        try
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
        finally
        {
            _suppressHotKey = false;
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
