// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace QuickWindows.Helpers;

public class MonitorInfo
{
    public MonitorInfo(IntPtr monitor, IntPtr hdc)
    {
        var info = new NativeMethods.MonitorInfoEx();
        NativeMethods.GetMonitorInfo(new HandleRef(null, monitor), info);
        Bounds = new NativeMethods.Rect
        {
            left = info.rcMonitor.left,
            top = info.rcMonitor.top,
            right = info.rcMonitor.right,
            bottom = info.rcMonitor.bottom,
        };
        WorkingArea = new NativeMethods.Rect
        {
            left = info.rcWork.left,
            top = info.rcWork.top,
            right = info.rcWork.right,
            bottom = info.rcWork.bottom,
        };
        IsPrimary = (info.dwFlags & NativeMethods.MonitorinfofPrimary) != 0;
        Name = new string(info.szDevice).TrimEnd((char)0);
    }

    public NativeMethods.Rect Bounds { get; private set; }

    public NativeMethods.Rect WorkingArea { get; private set; }

    public string Name { get; private set; }

    public bool IsPrimary { get; private set; }
}
