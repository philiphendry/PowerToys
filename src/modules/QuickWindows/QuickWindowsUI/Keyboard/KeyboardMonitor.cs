// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using ManagedCommon;
using QuickWindows.Features;
using QuickWindows.Settings;

namespace QuickWindows.Keyboard;

public class KeyboardMonitor(
    IUserSettings userSettings,
    IWindowHelpers windowHelpers)
    : IKeyboardMonitor, IDisposable
{
    private readonly object _lock = new();
    private IntPtr _hookHandle = IntPtr.Zero;
    private NativeMethods.HookProc? _hookProc;
    private bool _isHotKeyPressed;

    public event EventHandler? HotKeyPressed;

    public event EventHandler? HotKeyReleased;

    public void Install()
    {
        lock (_lock)
        {
            _hookProc = KeyboardHookCallback;
            _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookProc, Marshal.GetHINSTANCE(typeof(KeyboardMonitor).Module), 0);
            if (_hookHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to set hook. Error: {Marshal.GetLastWin32Error()}");
            }
        }
    }

    public void Uninstall()
    {
        lock (_lock)
        {
            if (_hookHandle == IntPtr.Zero)
            {
                return;
            }

            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        lock (_lock)
        {
            if (nCode < 0)
            {
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            if (userSettings.DoNotActivateOnGameMode.Value && windowHelpers.DetectGameMode())
            {
                Logger.LogDebug("Game mode detected, not activating QuickWindows");
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            }

            var isKeyDown = wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN;
            var isAltPressed = isKeyDown
                                && ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0
                                || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LMENU) & 0x8000) != 0
                                || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RMENU) & 0x8000) != 0);
            var isShiftPressed = isKeyDown
                                 && ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0
                                     || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LSHIFT) & 0x8000) != 0
                                     || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RSHIFT) & 0x8000) != 0);
            var isCtrlPressed = isKeyDown
                                && ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0
                                    || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LCONTROL) & 0x8000) != 0
                                    || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RCONTROL) & 0x8000) != 0);

            var anyOtherKeyPressed = false;
            for (var key = 0; key < 256; key++)
                    {
                if ((userSettings.ActivateOnAlt.Value && key is NativeMethods.VK_MENU or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU)
                    || (userSettings.ActivateOnShift.Value && key is NativeMethods.VK_SHIFT or NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT)
                    || (userSettings.ActivateOnCtrl.Value && key is NativeMethods.VK_CONTROL or NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL))
                        {
                    continue;
                    }

                if ((NativeMethods.GetAsyncKeyState(key) & 0x8000) != 0)
                    {
                    anyOtherKeyPressed = true;
                    break;
                }
                    }

            Logger.LogDebug($"### Alt: {isAltPressed}, Shift: {isShiftPressed}, Ctrl: {isCtrlPressed}, AnyOther: {anyOtherKeyPressed}, isKeyDown: {isKeyDown}");

            if (anyOtherKeyPressed
                || (userSettings.ActivateOnAlt.Value && !isAltPressed)
                || (userSettings.ActivateOnShift.Value && !isShiftPressed)
                || (userSettings.ActivateOnCtrl.Value && !isCtrlPressed))
            {
                if (_isHotKeyPressed)
                    {
                        _isHotKeyPressed = false;
                        HotKeyReleased?.Invoke(this, EventArgs.Empty);
                    }
            }
            else
            {
                if (!_isHotKeyPressed)
                {
                    _isHotKeyPressed = true;
                    HotKeyPressed?.Invoke(this, EventArgs.Empty);
                }
            }

            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
    }

    private static bool IsHotKey(int vkCode)
    {
        return vkCode == NativeMethods.VK_MENU || vkCode == NativeMethods.VK_LMENU || vkCode == NativeMethods.VK_RMENU;
    }
}
