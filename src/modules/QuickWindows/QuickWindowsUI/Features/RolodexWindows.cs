// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using QuickWindows.Interfaces;

namespace QuickWindows.Features;

public class RolodexWindows : IRolodexWindows
{
    public void SendWindowToBottom(int x, int y)
    {
        var hwndUnderCursor = NativeMethods.WindowFromPoint(new NativeMethods.POINT(x, y));
        if (hwndUnderCursor == IntPtr.Zero)
        {
            Logger.LogDebug($"{nameof(NativeMethods.WindowFromPoint)} failed with error code {Marshal.GetLastWin32Error()}");
            return;
        }

        var rootHwnd = NativeMethods.GetAncestor(hwndUnderCursor, NativeMethods.GA_ROOT);
        if (rootHwnd == IntPtr.Zero || !NativeMethods.IsWindow(rootHwnd))
        {
            Logger.LogDebug($"{nameof(NativeMethods.GetAncestor)} failed with error code {Marshal.GetLastWin32Error()}");
            return;
        }

        if (!NativeMethods.SetWindowPos(rootHwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }
    }

    public void BringBottomWindowToTop(int x, int y)
    {
        var bottomWindow = IntPtr.Zero;

        if (!NativeMethods.EnumWindows(EnumerateWindowFunc, IntPtr.Zero))
        {
            Logger.LogDebug($"{nameof(NativeMethods.EnumWindows)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        bool EnumerateWindowFunc(IntPtr hWnd, IntPtr lParam)
        {
            if (IsSystemWindow(hWnd))
            {
                return true;
            }

            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out var rect))
            {
                return true;
            }

            // Check if cursor is over this window
            if (x < rect.left || x > rect.right || y < rect.top || y > rect.bottom)
            {
                return true;
            }

            // Store the current window as it's under the cursor and so far the lowest in the z-order
            bottomWindow = hWnd;

            return true;
        }

        if (bottomWindow == IntPtr.Zero)
        {
            return;
        }

        // First, bring the window above all non-topmost windows
        if (!NativeMethods.SetWindowPos(bottomWindow, NativeMethods.HWND_TOP, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        // Then force it to the absolute top by bringing it to topmost and back
        if (!NativeMethods.SetWindowPos(bottomWindow, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        if (!NativeMethods.SetWindowPos(bottomWindow, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }
    }

    private bool IsSystemWindow(IntPtr hWnd)
    {
        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EX_STYLE);

        // Check for tool windows and transparent windows
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0 ||
            (exStyle & NativeMethods.WS_EX_TRANSPARENT) != 0)
        {
            return true;
        }

        return false;
    }

    private bool IsWindowVisible(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd))
        {
            return false;
        }

        // Check if window is cloaked (hidden by the system)
        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out var isCloaked, sizeof(int)) == 0
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
}
