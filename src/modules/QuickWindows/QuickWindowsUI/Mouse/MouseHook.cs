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
    private readonly object _lock = new();
    private IntPtr _hookHandle = IntPtr.Zero;
    private NativeMethods.HookProc? _hookProc;
    private bool _eventPropagation;

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

    public void Install(bool eventPropagation = false)
    {
        lock (_lock)
        {
            _eventPropagation = eventPropagation;
            if (_hookHandle != IntPtr.Zero)
            {
                return; // Already installed
            }

            _hookProc = MouseHookCallback;
            _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _hookProc, Marshal.GetHINSTANCE(typeof(MouseHook).Module), 0);
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

            Logger.LogDebug("Unhooking windows mouse hook");
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        lock (_lock)
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
                    if (!_eventPropagation)
                    {
                        return new IntPtr(1);
                    }

                    break;

                case NativeMethods.WM_MOUSEMOVE:
                    MouseMove?.Invoke(this, new MouseMoveEventArgs(hookStruct.pt.x, hookStruct.pt.y));
                    break;

                case NativeMethods.WM_LBUTTONDOWN:
                case NativeMethods.WM_RBUTTONDOWN:
                    var buttonDown = wParam.ToInt32() == NativeMethods.WM_LBUTTONDOWN ? MouseButton.Left : MouseButton.Right;
                    MouseDown?.Invoke(this, new MouseButtonEventArgs(hookStruct.pt.x, hookStruct.pt.y, buttonDown));
                    if (!_eventPropagation)
                    {
                        return new IntPtr(1);
                    }

                    break;

                case NativeMethods.WM_LBUTTONUP:
                case NativeMethods.WM_RBUTTONUP:
                    var buttonUp = wParam.ToInt32() == NativeMethods.WM_LBUTTONUP ? MouseButton.Left : MouseButton.Right;
                    MouseUp?.Invoke(this, new MouseButtonEventArgs(hookStruct.pt.x, hookStruct.pt.y, buttonUp));
                    if (!_eventPropagation)
                    {
                        return new IntPtr(1);
                    }

                    break;
            }

            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
    }
}
