// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using QuickWindows.Interfaces;
using QuickWindows.Settings;

namespace QuickWindows.Features;

public class RolodexWindows : IRolodexWindows
{
    private readonly IWindowHelpers _windowHelpers;
    private bool _rolodexEnabled;

    public RolodexWindows(IUserSettings userSettings, IWindowHelpers windowHelpers)
    {
        _windowHelpers = windowHelpers;

        _rolodexEnabled = userSettings.RolodexEnabled.Value;
        userSettings.RolodexEnabled.PropertyChanged += (_, _) => _rolodexEnabled = userSettings.RolodexEnabled.Value;
    }

    public void SendWindowToBottom(int x, int y)
    {
        if (!_rolodexEnabled)
        {
            return;
        }

        var targetWindow = _windowHelpers.GetWindowAtCursor(x, y);
        if (targetWindow == IntPtr.Zero)
        {
            return;
        }

        _windowHelpers.SendToBack(targetWindow);
    }

    public void BringBottomWindowToTop(int x, int y)
    {
        if (!_rolodexEnabled)
        {
            return;
        }

        var bottomWindow = IntPtr.Zero;

        if (!NativeMethods.EnumWindows(EnumerateWindowFunc, IntPtr.Zero))
        {
            Logger.LogDebug($"{nameof(NativeMethods.EnumWindows)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        bool EnumerateWindowFunc(IntPtr hWnd, IntPtr lParam)
        {
            if (_windowHelpers.IsSystemWindow(hWnd)
                || !_windowHelpers.IsWindowVisible(hWnd)
                || !NativeMethods.GetWindowRect(hWnd, out var rect))
            {
                return true;
            }

            // Check if cursor is over this window
            if (x < rect.left || x > rect.right || y < rect.top || y > rect.bottom)
            {
                return true;
            }

            // Store the current window as it's under the cursor and so far the lowest in the z-order
            bottomWindow = hWnd;

            return true;
        }

        if (bottomWindow == IntPtr.Zero)
        {
            return;
        }

        _windowHelpers.BringToFront(bottomWindow);
    }
}
