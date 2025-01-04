// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace QuickWindows.Helpers;

public class MonitorInfos : IMonitorInfos
{
    private static readonly HandleRef NullHandleRef = new(null, IntPtr.Zero);

    public DpiScale GetCurrentMonitorDpi() => VisualTreeHelper.GetDpi(Application.Current.MainWindow);

    public bool HasMultipleMonitors() => GetAllMonitorInfos().Count > 1;

    public List<MonitorInfo> GetAllMonitorInfos()
    {
        var monitors = new List<MonitorInfo>();
        var proc = new NativeMethods.MonitorEnumProc(Callback);
        NativeMethods.EnumDisplayMonitors(NullHandleRef, IntPtr.Zero, proc, IntPtr.Zero);
        return monitors;

        bool Callback(IntPtr monitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr lparam)
        {
            monitors.Add(new MonitorInfo(monitor, hdc));
            return true;
        }
    }
}
