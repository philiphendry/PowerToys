// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using ManagedCommon;
using QuickWindows.Interfaces;

namespace QuickWindows.Features;

public class WindowHelpers : IWindowHelpers
{
    public IntPtr GetWindowAtCursor(int x, int y)
    {
        var point = new NativeMethods.POINT(x, y);
        var hwnd = NativeMethods.WindowFromPoint(point);
        if (hwnd == IntPtr.Zero)
        {
            Logger.LogDebug($"{nameof(NativeMethods.WindowFromPoint)} failed with error code {Marshal.GetLastWin32Error()}");
            return IntPtr.Zero;
        }

        var rootHwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        if (rootHwnd == IntPtr.Zero)
        {
            Logger.LogDebug($"{nameof(NativeMethods.GetAncestor)} failed with error code {Marshal.GetLastWin32Error()}");
            return IntPtr.Zero;
        }

        if (!NativeMethods.IsWindow(rootHwnd))
        {
            return IntPtr.Zero;
        }

        return rootHwnd;
    }

    public (bool Success, string WindowTitle, string ClassName) GetWindowInfoAtCursor()
    {
        if (NativeMethods.GetCursorPos(out var cursorPosition) == false)
        {
            Logger.LogError($"GetCursorPos failed with error: {Marshal.GetLastWin32Error()}");
            return (false, string.Empty, string.Empty);
        }

        var windowAtCursorHandle = GetWindowAtCursor(cursorPosition.X, cursorPosition.Y);

        var windowTitle = new StringBuilder(200);
        var textLength = NativeMethods.GetWindowText(windowAtCursorHandle, windowTitle, 200);
        if (textLength == 0)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            if (lastWin32Error != 0)
            {
                Logger.LogError($"GetWindowText failed with error: {lastWin32Error}");
            }

            return (false, string.Empty, string.Empty);
        }

        var className = new StringBuilder(200);
        textLength = NativeMethods.GetClassName(windowAtCursorHandle, className, 200);
        if (textLength == 0)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            if (lastWin32Error != 0)
            {
                Logger.LogError($"GetClassName failed with error: {lastWin32Error}");
            }

            return (false, string.Empty, string.Empty);
        }

        return (true, windowTitle.ToString(), className.ToString());
    }
}
