// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using QuickWindows.Interfaces;

namespace QuickWindows.Features;

public class ResizingWindows(
    ITargetWindow targetWindow,
    IRateLimiter rateLimiter,
    ISnappingWindows snappingWindows)
    : IResizingWindows
{
    private const int MinimumWindowSize = 200;

    private NativeMethods.POINT _initialMousePosition;
    private ResizeOperation _currentOperation;

    public ResizeOperation? StartResize(int x, int y)
    {
        snappingWindows.StartSnap(targetWindow.HWnd);

        _initialMousePosition = new NativeMethods.POINT(x, y);

        var relativeX = (x - targetWindow.InitialPlacement.left) /
                        (double)(targetWindow.InitialPlacement.right - targetWindow.InitialPlacement.left);
        var relativeY = (y - targetWindow.InitialPlacement.top) /
                        (double)(targetWindow.InitialPlacement.bottom - targetWindow.InitialPlacement.top);

        _currentOperation = (relativeX < 0.5, relativeY < 0.5) switch
        {
            (true, true) => ResizeOperation.ResizeTopLeft,
            (false, true) => ResizeOperation.ResizeTopRight,
            (true, false) => ResizeOperation.ResizeBottomLeft,
            (false, false) => ResizeOperation.ResizeBottomRight,
        };

        return _currentOperation;
    }

    public void ResizeWindow(int x, int y)
    {
        if (rateLimiter.IsLimited())
        {
            return;
        }

        if (_currentOperation != ResizeOperation.ResizeTopLeft &&
            _currentOperation != ResizeOperation.ResizeTopRight &&
            _currentOperation != ResizeOperation.ResizeBottomRight &&
            _currentOperation != ResizeOperation.ResizeBottomLeft)
        {
            Logger.LogDebug($"Called with _currentOperation {_currentOperation} so exiting early.");
            return;
        }

        var deltaX = x - _initialMousePosition.x;
        var deltaY = y - _initialMousePosition.y;

        // Resize operation
        var newLeft = targetWindow.InitialPlacement.left;
        var newTop = targetWindow.InitialPlacement.top;
        var newRight = targetWindow.InitialPlacement.right;
        var newBottom = targetWindow.InitialPlacement.bottom;

        switch (_currentOperation)
        {
            case ResizeOperation.ResizeTopLeft:
                newLeft += deltaX;
                newTop += deltaY;
                break;
            case ResizeOperation.ResizeTopRight:
                newRight += deltaX;
                newTop += deltaY;
                break;
            case ResizeOperation.ResizeBottomLeft:
                newLeft += deltaX;
                newBottom += deltaY;
                break;
            case ResizeOperation.ResizeBottomRight:
                newRight += deltaX;
                newBottom += deltaY;
                break;
        }

        (newLeft, newTop, newRight, newBottom) = snappingWindows.SnapResizingWindow(
            newLeft,
            newTop,
            newRight,
            newBottom,
            _currentOperation);

        // Ensure minimum window size
        const int minSize = MinimumWindowSize;
        if (newRight - newLeft < minSize)
        {
            newRight = newLeft + minSize;
        }

        if (newBottom - newTop < minSize)
        {
            newBottom = newTop + minSize;
        }

        // Optimize flags for faster resizing
        const uint flags = NativeMethods.SWP_NOZORDER | // Don't change Z-order
                          NativeMethods.SWP_NOACTIVATE | // Don't activate the window
                          NativeMethods.SWP_NOSENDCHANGING; // Don't send WM_WINDOWPOSCHANGING

        if (!NativeMethods.SetWindowPos(
            targetWindow.HWnd,
            IntPtr.Zero,
            newLeft,
            newTop,
            newRight - newLeft,
            newBottom - newTop,
            flags))
        {
            Logger.LogError($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }
    }
}
