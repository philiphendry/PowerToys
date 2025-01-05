// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using QuickWindows.Interfaces;

namespace QuickWindows.Features;

public class RestoreMaximised(
    IWindowHelpers windowHelpers,
    ITargetWindow targetWindow)
    : IRestoreMaximised
{
    private bool _isMaximised;

    public void Start()
    {
        _isMaximised = windowHelpers.IsMaximised(targetWindow.HWnd);
    }

    public void Move()
    {
        if (!_isMaximised)
        {
            return;
        }

        _isMaximised = false;

        windowHelpers.RestoreWindow(targetWindow.HWnd);

        NativeMethods.SetWindowPos(
            targetWindow.HWnd,
            IntPtr.Zero,
            targetWindow.InitialPlacement.left,
            targetWindow.InitialPlacement.top,
            targetWindow.InitialPlacement.right - targetWindow.InitialPlacement.left,
            targetWindow.InitialPlacement.bottom - targetWindow.InitialPlacement.top,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOOWNERZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_ASYNCWINDOWPOS);
    }

    public void Resize()
    {
        if (!_isMaximised)
        {
            return;
        }

        _isMaximised = false;

        windowHelpers.RestoreWindow(targetWindow.HWnd);

        var monitorInfo = windowHelpers.GetMonitorInfoForWindow(targetWindow.HWnd);
        if (monitorInfo is null)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            targetWindow.HWnd,
            IntPtr.Zero,
            monitorInfo.rcWork.left,
            monitorInfo.rcWork.top,
            monitorInfo.rcWork.right - monitorInfo.rcWork.left,
            monitorInfo.rcWork.bottom - monitorInfo.rcWork.top,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOOWNERZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_ASYNCWINDOWPOS);

        targetWindow.SetInitialPlacement(monitorInfo.rcWork);
    }
}
