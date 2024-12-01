// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using QuickWindows.Interfaces;

namespace QuickWindows.Features;

public class MovingWindows : IMovingWindows
{
    private const int MinUpdateIntervalMs = 32; // Approx. 30fps
    private readonly ISnappingWindows _snappingWindows;
    private readonly IRateLimiter _rateLimiter;
    private readonly IWindowHelpers _windowHelpers;
    private IntPtr _targetWindow = IntPtr.Zero;
    private NativeMethods.POINT _initialMousePosition;
    private NativeMethods.Rect _initialWindowRect;

    public MovingWindows(
        ISnappingWindows snappingWindows,
        IRateLimiter rateLimiter,
        IWindowHelpers windowHelpers)
    {
        _snappingWindows = snappingWindows;
        _rateLimiter = rateLimiter;
        _rateLimiter.Interval = MinUpdateIntervalMs;
        _windowHelpers = windowHelpers;
    }

    public void StartMove(int x, int y)
    {
        _targetWindow = _windowHelpers.GetWindowAtCursor(x, y);
        if (_targetWindow == IntPtr.Zero)
        {
            return;
        }

        if (!NativeMethods.GetWindowRect(_targetWindow, out _initialWindowRect))
        {
            Logger.LogDebug($"{nameof(NativeMethods.GetWindowRect)} failed with error code {Marshal.GetLastWin32Error()}");
            return;
        }

        _snappingWindows.StartSnap();
        _initialMousePosition = new NativeMethods.POINT(x, y);
    }

    public void MoveWindow(int x, int y)
    {
        if (_targetWindow == IntPtr.Zero || _rateLimiter.IsLimited())
        {
            return;
        }

        var deltaX = x - _initialMousePosition.x;
        var deltaY = y - _initialMousePosition.y;

        var (left, top, right, bottom) = _snappingWindows.SnapMovingWindow(
            _initialWindowRect.left + deltaX,
            _initialWindowRect.top + deltaY,
            _initialWindowRect.right + deltaX,
            _initialWindowRect.bottom + deltaY);

        if (!NativeMethods.SetWindowPos(_targetWindow, IntPtr.Zero, left, top, right - left, bottom - top, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_ASYNCWINDOWPOS))
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }
    }

    public void StopMove()
    {
        _targetWindow = IntPtr.Zero;
    }
}
