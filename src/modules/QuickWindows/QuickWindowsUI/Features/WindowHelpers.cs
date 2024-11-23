// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;

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

    public bool DetectGameMode()
    {
        if (NativeMethods.SHQueryUserNotificationState(out var notificationState) == IntPtr.Zero)
        {
            return false;
        }

        return notificationState == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN;
    }
}
