// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
                return (false, string.Empty, string.Empty);
            }
        }

        var className = new StringBuilder(200);
        textLength = NativeMethods.GetClassName(windowAtCursorHandle, className, 200);
        if (textLength == 0)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            if (lastWin32Error != 0)
            {
                Logger.LogError($"GetClassName failed with error: {lastWin32Error}");
                return (false, string.Empty, string.Empty);
            }
        }

        return (true, windowTitle.ToString(), className.ToString());
    }

    public bool IsWindowVisible(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd))
        {
            return false;
        }

        // Check if window is cloaked (hidden by the system)
        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int isCloaked) == 0
            && isCloaked != 0)
        {
            return false;
        }

        // Get window info to check actual visibility state
        var info = default(NativeMethods.WINDOWINFO);
        info.cbSize = (uint)Marshal.SizeOf(info);
        if (!NativeMethods.GetWindowInfo(hWnd, ref info))
        {
            return false;
        }

        // Check if window is really visible and not minimized
        return (info.dwStyle & NativeMethods.WS_VISIBLE) != 0
               && (info.dwStyle & NativeMethods.WS_MINIMIZE) == 0;
    }

    public bool IsSystemWindow(IntPtr hWnd)
    {
        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EX_STYLE);

        // Check for tool windows and transparent windows
        return (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0 ||
               (exStyle & NativeMethods.WS_EX_TRANSPARENT) != 0;
    }

    public List<NativeMethods.Rect> GetSnappableWindows(IntPtr excludeHWnd)
    {
        var windows = new List<NativeMethods.Rect>();
        if (!NativeMethods.EnumWindows(EnumerateWindowFunc, IntPtr.Zero))
        {
            Logger.LogDebug($"{nameof(NativeMethods.EnumWindows)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        return windows;

        bool EnumerateWindowFunc(IntPtr hWnd, IntPtr lParam)
        {
            if (!IsWindowVisible(hWnd)
                || NativeMethods.IsIconic(hWnd) // Skip minimized windows
                || (NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EX_STYLE) & NativeMethods.WS_EX_NOACTIVATE) == NativeMethods.WS_EX_NOACTIVATE
                || (NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_STYLE) & NativeMethods.WS_CAPTION) != NativeMethods.WS_CAPTION // No title bar
                || (NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_STYLE) & NativeMethods.WS_THICKFRAME) != NativeMethods.WS_THICKFRAME // No sizing border
                || hWnd == excludeHWnd)
            {
                return true;
            }

            if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS, out NativeMethods.Rect rect) == 0
                || NativeMethods.GetWindowRect(hWnd, out rect))
            {
                windows.Add(rect);
                return true;
            }

            return true;
        }
    }
}
