// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
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

    private bool _isAltActiveConfig;
    private bool _isCtrlActiveConfig;
    private bool _isShiftActiveConfig;

    private bool _altDown;
    private bool _ctrlDown;
    private bool _shiftDown;

    private bool _isHotKeyPressed;
    private bool _suppressHotKey;

    public event EventHandler? HotKeyPressed;

    public event EventHandler? HotKeyReleased;

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
        _isAltActiveConfig = userSettings.ActivateOnAlt.Value;
        _isCtrlActiveConfig = userSettings.ActivateOnCtrl.Value;
        _isShiftActiveConfig = userSettings.ActivateOnShift.Value;
    }

    private void Hook_KeyboardPressed(object? sender, GlobalKeyboardHookEventArgs e)
    {
        lock (_lock)
        {
            // Always update key state for KeyUp events so we never strand a key.
            if (_suppressHotKey || disabledInGameMode.IsDisabledInGameMode())
            {
                if (e.KeyboardState is GlobalKeyboardHook.KeyboardState.KeyUp or GlobalKeyboardHook.KeyboardState.SysKeyUp)
                {
                    UpdateModifierState(e.KeyboardData.VirtualCode, e.KeyboardState);
                }

                return;
            }

            UpdateModifierState(e.KeyboardData.VirtualCode, e.KeyboardState);

            var isHotKeyPressed = EvaluateHotKeyCurrent();
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

    private void UpdateModifierState(int vKey, GlobalKeyboardHook.KeyboardState state)
    {
        // Guard against invalid virtual key codes (0 or out of range)
        if (vKey is <= 0 or > 0xFF)
        {
            return;
        }

        var isDown = state is GlobalKeyboardHook.KeyboardState.KeyDown or GlobalKeyboardHook.KeyboardState.SysKeyDown;
        switch (vKey)
        {
            case NativeMethods.VK_MENU:
            case NativeMethods.VK_LMENU:
            case NativeMethods.VK_RMENU:
                _altDown = isDown;
                break;
            case NativeMethods.VK_CONTROL:
            case NativeMethods.VK_LCONTROL:
            case NativeMethods.VK_RCONTROL:
                _ctrlDown = isDown;
                break;
            case NativeMethods.VK_SHIFT:
            case NativeMethods.VK_LSHIFT:
            case NativeMethods.VK_RSHIFT:
                _shiftDown = isDown;
                break;
        }
    }

    private bool EvaluateHotKeyCurrent()
    {
        if (!_isAltActiveConfig && !_isCtrlActiveConfig && !_isShiftActiveConfig)
        {
            return false; // no configured hotkey
        }

        var altOk = (!_isAltActiveConfig && !_altDown) || (_isAltActiveConfig && _altDown);
        var ctrlOk = (!_isCtrlActiveConfig && !_ctrlDown) || (_isCtrlActiveConfig && _ctrlDown);
        var shiftOk = (!_isShiftActiveConfig && !_shiftDown) || (_isShiftActiveConfig && _shiftDown);

        return altOk && ctrlOk && shiftOk;
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

    private bool IsHotKeyPressed()
    {
        return EvaluateHotKeyCurrent();
    }

    public bool CheckHotKeyActive()
    {
        var active = IsHotKeyPressed();
        if (_isHotKeyPressed && !active)
        {
            _isHotKeyPressed = false;
            return false;
        }

        return true;
    }

    public void SendControlKey()
    {
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
        if (disposing)
        {
            Uninstall();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
