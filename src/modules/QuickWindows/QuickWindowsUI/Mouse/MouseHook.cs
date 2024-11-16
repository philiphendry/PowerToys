// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace QuickWindows.Mouse;

public enum MouseButton
{
    None,
    Left,
    Right,
}

[Export(typeof(IMouseHook))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class MouseHook : IMouseHook
{
    private static IntPtr _hookHandle = IntPtr.Zero;
    private static NativeMethods.HookProc? _hookProc;
    private bool _eventPropagation;

    public event EventHandler<MouseEventArgs>? MouseMove;

    public event EventHandler<MouseEventArgs>? MouseDown;

    public event EventHandler<MouseEventArgs>? MouseUp;

    public event EventHandler<MouseWheelEventArgs>? MouseWheel;

    public class MouseEventArgs(int x, int y, MouseButton button) : EventArgs
    {
        public int X { get; } = x;

        public int Y { get; } = y;

        public MouseButton Button { get; } = button;
    }

    public class MouseWheelEventArgs(int x, int y, int delta) : MouseEventArgs(x, y, MouseButton.None)
    {
        public int Delta { get; } = delta;
    }

    public void Install(bool eventPropagation = false)
    {
        _eventPropagation = eventPropagation;
        if (_hookHandle != IntPtr.Zero)
        {
            return; // Already installed
        }

        _hookProc = MouseHookCallback;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _hookProc,
            Marshal.GetHINSTANCE(typeof(MouseHook).Module),
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

        Logger.LogDebug("Unhooking windows mouse hook");
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
                MouseWheel?.Invoke(this, new MouseWheelEventArgs(hookStruct.pt.x, hookStruct.pt.y, delta));
                if (!_eventPropagation)
                {
                    return new IntPtr(1);
                }

                break;

            case NativeMethods.WM_MOUSEMOVE:
                MouseMove?.Invoke(this, new MouseEventArgs(hookStruct.pt.x, hookStruct.pt.y, MouseButton.None));
                break;

            case NativeMethods.WM_LBUTTONDOWN:
            case NativeMethods.WM_RBUTTONDOWN:
                var buttonDown = wParam.ToInt32() == NativeMethods.WM_LBUTTONDOWN ? MouseButton.Left : MouseButton.Right;
                MouseDown?.Invoke(this, new MouseEventArgs(hookStruct.pt.x, hookStruct.pt.y, buttonDown));
                if (!_eventPropagation)
                {
                    return new IntPtr(1);
                }

                break;

            case NativeMethods.WM_LBUTTONUP:
            case NativeMethods.WM_RBUTTONUP:
                var buttonUp = wParam.ToInt32() == NativeMethods.WM_LBUTTONUP ? MouseButton.Left : MouseButton.Right;
                MouseUp?.Invoke(this, new MouseEventArgs(hookStruct.pt.x, hookStruct.pt.y, buttonUp));
                if (!_eventPropagation)
                {
                    return new IntPtr(1);
                }

                break;
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
