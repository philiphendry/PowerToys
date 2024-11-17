// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;
using ManagedCommon;

namespace QuickWindows.Features;

public class WindowIdentifier(IWindowHelpers windowHelpers) : IWindowIdentifier
{
    public void IdentifyWindow(int x, int y)
    {
        var windowAtCursorHandle = windowHelpers.GetWindowAtCursor(x, y);

        var windowTitle = new StringBuilder(200);
        var result = NativeMethods.GetWindowText(windowAtCursorHandle, windowTitle, 200);
        if (result == 0)
        {
            Logger.LogError($"GetWindowText failed with error: {Marshal.GetLastWin32Error()}");
            return;
        }

        var className = new StringBuilder(200);
        result = NativeMethods.GetClassName(windowAtCursorHandle, className, 200);
        if (result == 0)
        {
            Logger.LogError($"GetClassName failed with error: {Marshal.GetLastWin32Error()}");
            return;
        }

        Logger.LogDebug($"Window title: {windowTitle}, Class name: {className}");
    }
}
