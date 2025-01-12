// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using QuickWindows.Interfaces;
using QuickWindows.Settings;

namespace QuickWindows.Features;

public class DisabledInGameMode : IDisabledInGameMode
{
    private readonly IRateLimiter _rateLimiter;
    private readonly IWindowHelpers _windowHelpers;
    private bool _doNotActiveInGameMode;
    private bool _lastComputedState;

    public DisabledInGameMode(
        IUserSettings userSettings,
        IWindowHelpers windowHelpers)
    {
        _rateLimiter = new RateLimiter { Interval = 5000 };
        _windowHelpers = windowHelpers;

        userSettings.DoNotActivateOnGameMode.PropertyChanged += (_, _) => _doNotActiveInGameMode = userSettings.DoNotActivateOnGameMode.Value;
        _doNotActiveInGameMode = userSettings.DoNotActivateOnGameMode.Value;

        Logger.LogDebug($"Initialised with _doNotActiveInGameMode: {_doNotActiveInGameMode}");
    }

    public bool IsDisabledInGameMode()
    {
        if (_rateLimiter.IsLimited())
        {
            return _lastComputedState;
        }

        return _lastComputedState = _doNotActiveInGameMode && DetectGameMode();
    }

    private bool DetectGameMode()
    {
        if (Marshal.GetExceptionForHR(NativeMethods.SHQueryUserNotificationState(out var notificationState)) == null
            && notificationState == NativeMethods.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN)
        {
            return true;
        }

        if (IsFullScreen())
        {
            return true;
        }

        return false;
    }

    private bool IsFullScreen()
    {
        var hWindow = NativeMethods.GetForegroundWindow();
        if (hWindow == IntPtr.Zero)
        {
            Logger.LogError($"{nameof(NativeMethods.GetForegroundWindow)} failed with error code {Marshal.GetLastWin32Error()}");
            return false;
        }

        if (!NativeMethods.GetWindowRect(hWindow, out var windowSize))
        {
            Logger.LogError($"{nameof(NativeMethods.GetWindowRect)} failed with error code {Marshal.GetLastWin32Error()}");
            return false;
        }

        var monitorInfo = _windowHelpers.GetMonitorInfoForWindow(hWindow);
        if (monitorInfo is null)
        {
            return false;
        }

        var isFullScreen = windowSize.left == monitorInfo.rcMonitor.left
                           && windowSize.top == monitorInfo.rcMonitor.top
                           && windowSize.right == monitorInfo.rcMonitor.right
                           && windowSize.bottom == monitorInfo.rcMonitor.bottom;

        var exStyle = NativeMethods.GetWindowLong(hWindow, NativeMethods.GWL_EX_STYLE);
        var hasTitle = (exStyle & NativeMethods.WS_CAPTION) == NativeMethods.WS_CAPTION;

        return isFullScreen && !hasTitle;
    }
}
