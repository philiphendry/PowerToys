// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace QuickWindows.Mouse;

public class MouseHook : IMouseHook
{
    private readonly NativeMethods.HookProc _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;

    public MouseHook()
    {
        _hookProc = MouseHookCallback;
    }

    public event EventHandler<MouseMoveEventArgs>? MouseMove;

    public event EventHandler<MouseButtonEventArgs>? MouseDown;

    public event EventHandler<MouseButtonEventArgs>? MouseUp;

    public event EventHandler<MouseMoveWheelEventArgs>? MouseWheel;

    public class MouseMoveEventArgs(int x, int y) : EventArgs
    {
        public int X { get; } = x;

        public int Y { get; } = y;
    }

    public class MouseButtonEventArgs(int x, int y, MouseButton button) : MouseMoveEventArgs(x, y)
    {
        public MouseButton Button { get; } = button;
    }

    public class MouseMoveWheelEventArgs(int x, int y, int delta) : MouseMoveEventArgs(x, y)
    {
        public int Delta { get; } = delta;
    }

    public void Install()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return; // Already installed
        }

        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _hookProc, Marshal.GetHINSTANCE(typeof(MouseHook).Module), 0);
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

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

        switch (wParam.ToInt32())
        {
            case NativeMethods.WM_MOUSEWHEEL:
                int delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                MouseWheel?.Invoke(this, new MouseMoveWheelEventArgs(hookStruct.pt.x, hookStruct.pt.y, delta));
                return new IntPtr(1);

            case NativeMethods.WM_MOUSEMOVE:
                MouseMove?.Invoke(this, new MouseMoveEventArgs(hookStruct.pt.x, hookStruct.pt.y));
                break;

            case NativeMethods.WM_LBUTTONDOWN:
            case NativeMethods.WM_RBUTTONDOWN:
                var buttonDown = wParam.ToInt32() == NativeMethods.WM_LBUTTONDOWN ? MouseButton.Left : MouseButton.Right;
                MouseDown?.Invoke(this, new MouseButtonEventArgs(hookStruct.pt.x, hookStruct.pt.y, buttonDown));
                return new IntPtr(1);

            case NativeMethods.WM_LBUTTONUP:
            case NativeMethods.WM_RBUTTONUP:
                var buttonUp = wParam.ToInt32() == NativeMethods.WM_LBUTTONUP ? MouseButton.Left : MouseButton.Right;
                MouseUp?.Invoke(this, new MouseButtonEventArgs(hookStruct.pt.x, hookStruct.pt.y, buttonUp));
                return new IntPtr(1);
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
