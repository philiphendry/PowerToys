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
    private bool _eventsEnabled;

    public MouseHook()
    {
        _hookProc = MouseHookCallback;
    }

    public event EventHandler<MouseMoveEventArgs>? MouseMove;

    public event EventHandler<MouseButtonEventArgs>? MouseDown;

    public event EventHandler<MouseButtonEventArgs>? MouseUp;

    public event EventHandler<MouseMoveWheelEventArgs>? MouseWheel;

    public void EnableEvents() => _eventsEnabled = true;

    public void DisableEvents() => _eventsEnabled = false;

    public void Install()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
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
        if (nCode < 0 || !_eventsEnabled)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var msg = wParam.ToInt32();
        var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

        switch (msg)
        {
            case NativeMethods.WM_MOUSEWHEEL:
                int delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                var wheelArgs = new MouseMoveWheelEventArgs(hookStruct.pt.x, hookStruct.pt.y, delta);
                MouseWheel?.Invoke(this, wheelArgs);

                // Only suppress if the handler claimed the event (e.g. rolodex fired).
                // Pass through otherwise so normal scrolling works while Alt is held.
                return wheelArgs.Handled
                    ? new IntPtr(1)
                    : NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

            case NativeMethods.WM_MOUSEMOVE:
                MouseMove?.Invoke(this, new MouseMoveEventArgs(hookStruct.pt.x, hookStruct.pt.y));
                break;

            case NativeMethods.WM_LBUTTONDOWN:
            case NativeMethods.WM_RBUTTONDOWN:
                var buttonDown = msg == NativeMethods.WM_LBUTTONDOWN ? MouseButton.Left : MouseButton.Right;
                var buttonDownArgs = new MouseButtonEventArgs(hookStruct.pt.x, hookStruct.pt.y, buttonDown);
                MouseDown?.Invoke(this, buttonDownArgs);

                // Only suppress if the handler claimed the event (e.g. an operation started).
                // Pass through otherwise so applications receive normal clicks while Alt is held.
                return buttonDownArgs.Handled
                    ? new IntPtr(1)
                    : NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

            case NativeMethods.WM_LBUTTONUP:
            case NativeMethods.WM_RBUTTONUP:
                var buttonUp = msg == NativeMethods.WM_LBUTTONUP ? MouseButton.Left : MouseButton.Right;
                MouseUp?.Invoke(this, new MouseButtonEventArgs(hookStruct.pt.x, hookStruct.pt.y, buttonUp));

                // Always pass UP through — allows target window to release capture.
                return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}
