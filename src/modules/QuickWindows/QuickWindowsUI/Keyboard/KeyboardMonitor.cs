// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace QuickWindows.Keyboard;

public class KeyboardMonitor : IKeyboardMonitor
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

            var keyboardHookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            switch (wParam)
            {
                case NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN:
                    if (IsHotKey(keyboardHookStruct.vkCode))
                    {
                        if (!_isHotKeyPressed)
                        {
                            _isHotKeyPressed = true;
                            HotKeyPressed?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    else if (_isHotKeyPressed)
                    {
                        _isHotKeyPressed = false;
                        HotKeyReleased?.Invoke(this, EventArgs.Empty);
                    }

                    break;

                case NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP:
                    if (IsHotKey(keyboardHookStruct.vkCode))
                    {
                        _isHotKeyPressed = false;
                        HotKeyReleased?.Invoke(this, EventArgs.Empty);
                    }

                    break;
            }

            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
    }

    private static bool IsHotKey(int vkCode)
    {
        return vkCode == NativeMethods.VK_MENU || vkCode == NativeMethods.VK_LMENU || vkCode == NativeMethods.VK_RMENU;
    }
}
