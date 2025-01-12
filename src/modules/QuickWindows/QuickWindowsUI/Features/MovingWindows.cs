// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using QuickWindows.Interfaces;

namespace QuickWindows.Features;

public class MovingWindows(
    ITargetWindow targetWindow,
    ISnappingWindows snappingWindows,
    IRateLimiter rateLimiter)
    : IMovingWindows
{
    private NativeMethods.POINT _initialMousePosition;

    public void StartMove(int x, int y)
    {
        snappingWindows.StartSnap(targetWindow.HWnd);
        _initialMousePosition = new NativeMethods.POINT(x, y);
    }

    public void MoveWindow(int x, int y)
    {
        if (rateLimiter.IsLimited())
        {
            return;
        }

        var deltaX = x - _initialMousePosition.x;
        var deltaY = y - _initialMousePosition.y;

        var (left, top, right, bottom) = snappingWindows.SnapMovingWindow(
            targetWindow.InitialPlacement.left + deltaX,
            targetWindow.InitialPlacement.top + deltaY,
            targetWindow.InitialPlacement.right + deltaX,
            targetWindow.InitialPlacement.bottom + deltaY);

        if (!NativeMethods.SetWindowPos(
            targetWindow.HWnd,
            IntPtr.Zero,
            left,
            top,
            right - left,
            bottom - top,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOOWNERZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_ASYNCWINDOWPOS))
        {
            Logger.LogError($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }
    }
}
