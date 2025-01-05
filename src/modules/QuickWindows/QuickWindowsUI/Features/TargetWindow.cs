// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using QuickWindows.Interfaces;

namespace QuickWindows.Features;

public class TargetWindow(IWindowHelpers windowHelpers) : ITargetWindow
{
    private IntPtr _targetWindow = IntPtr.Zero;
    private NativeMethods.Rect _initialPlacement;

    public void SetTargetWindow(int x, int y)
    {
        _targetWindow = windowHelpers.GetWindowAtCursor(x, y);
        if (_targetWindow == IntPtr.Zero)
        {
            return;
        }

        if (!NativeMethods.GetWindowPlacement(_targetWindow, out var placement))
        {
            Logger.LogDebug($"{nameof(NativeMethods.GetWindowPlacement)} failed with error code {Marshal.GetLastWin32Error()}");
            _targetWindow = IntPtr.Zero;
        }

        SetInitialPlacement(placement.rcNormalPosition);
    }

    public bool HaveTargetWindow => _targetWindow != IntPtr.Zero;

    public IntPtr HWnd => _targetWindow;

    public NativeMethods.Rect InitialPlacement => _initialPlacement;

    public void ClearTargetWindow() => _targetWindow = IntPtr.Zero;

    public void SetInitialPlacement(NativeMethods.Rect placement) => NativeMethods.CopyRect(out _initialPlacement, placement);
}
