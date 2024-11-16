// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;

namespace QuickWindows.Keyboard;

[Export(typeof(IKeyboardMonitor))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class KeyboardMonitor : IKeyboardMonitor
{
    private static IntPtr _hookHandle = IntPtr.Zero;
    private static NativeMethods.HookProc? _hookProc;

    public event EventHandler? HotKeyPressed;

    public event EventHandler? HotKeyReleased;

    public void Install()
    {
        _hookProc = KeyboardHookCallback;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _hookProc,
            Marshal.GetHINSTANCE(typeof(KeyboardMonitor).Module),  // For WH_KEYBOARD_LL, this can be null
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to set hook. Error: {Marshal.GetLastWin32Error()}");
        }
    }

    public void Uninstall()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var vkCode = Marshal.ReadInt32(lParam);
        switch (wParam)
        {
            case NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN when IsHotKey(vkCode):
                HotKeyPressed?.Invoke(this, EventArgs.Empty);
                break;
            case NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP when IsHotKey(vkCode):
                HotKeyReleased?.Invoke(this, EventArgs.Empty);
                break;
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool IsHotKey(int vkCode)
    {
        return vkCode == NativeMethods.VK_MENU || vkCode == NativeMethods.VK_LMENU || vkCode == NativeMethods.VK_RMENU;
    }
}
