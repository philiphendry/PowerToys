// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using QuickWindows.Interfaces;

namespace QuickWindows.Features;

public class RolodexWindows(IWindowHelpers windowHelpers) : IRolodexWindows
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
            if (windowHelpers.IsSystemWindow(hWnd))
            {
                return true;
            }

            if (!windowHelpers.IsWindowVisible(hWnd))
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
}
