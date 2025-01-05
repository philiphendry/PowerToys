// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace QuickWindows.Interfaces;

public interface IWindowHelpers
{
    nint GetWindowAtCursor(int x, int y);

    (bool Success, string WindowTitle, string ClassName) GetWindowInfoAtCursor();

    bool IsWindowVisible(IntPtr hWnd);

    bool IsSystemWindow(IntPtr hWnd);

    bool IsMaximised(IntPtr targetWindow);

    void RestoreWindow(IntPtr targetWindow);

    NativeMethods.MonitorInfoEx? GetMonitorInfoForWindow(IntPtr targetWindow);

    List<NativeMethods.Rect> GetSnappableWindows(IntPtr excludeHWnd);
}
