// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using QuickWindows.Settings;

namespace QuickWindows.Features;

public class TransparentWindows : ITransparentWindows
{
    // TODO: Make this configurable and fetched form IUserSettings
    private readonly byte _resizeOpacityLevel = 210; // 0-255, can be made configurable
    private readonly IWindowHelpers _windowHelpers;
    private readonly IUserSettings _userSettings;

    private bool _enabled = true;
    private IntPtr _targetWindow = IntPtr.Zero;
    private int? _originalExStyle;
    private byte? _originalOpacityLevel;

    public TransparentWindows(
        IWindowHelpers windowHelpers,
        IUserSettings userSettings)
    {
        _windowHelpers = windowHelpers;
        _userSettings = userSettings;

        _userSettings.Changed += UserSettings_Changed;
        _enabled = _userSettings.TransparentWindowOnMove.Value;
    }

    public void StartTransparency(int x, int y)
    {
        if (!_enabled)
        {
            return;
        }

        _targetWindow = _windowHelpers.GetWindowAtCursor(x, y);
        if (_targetWindow == IntPtr.Zero)
        {
            return;
        }

        var originalExStyle = NativeMethods.GetWindowLong(_targetWindow, NativeMethods.GWL_EX_STYLE);
        if (originalExStyle == 0)
        {
            return;
        }

        // Store the original opacity if the window is already layered
        if ((originalExStyle & NativeMethods.WS_EX_LAYERED) != 0)
        {
            if (NativeMethods.GetLayeredWindowAttributes(_targetWindow, out _, out var alpha, out var flags))
            {
                _originalOpacityLevel = (flags & NativeMethods.LWA_ALPHA) != 0 ? alpha : (byte)255;
                Logger.LogDebug($"Saving _originalOpacityLevel {_originalOpacityLevel}");
            }
        }

        var setWindowLongSuccess = NativeMethods.SetWindowLong(_targetWindow, NativeMethods.GWL_EX_STYLE, originalExStyle | NativeMethods.WS_EX_LAYERED);
        if (setWindowLongSuccess == 0)
        {
            return;
        }

        NativeMethods.SetLayeredWindowAttributes(_targetWindow, 0, _resizeOpacityLevel, NativeMethods.LWA_ALPHA);

        _originalExStyle = originalExStyle;
    }

    public void EndTransparency()
    {
        if (!_enabled)
        {
            return;
        }

        if (_targetWindow == IntPtr.Zero || _originalExStyle == null)
        {
            return;
        }

        var originalExStyle = _originalExStyle.Value;

        // Restore the original opacity level or default to fully opaque
        var opacity = _originalOpacityLevel ?? 255;
        Logger.LogDebug($"Restoring opacity {opacity}, {_targetWindow}, {originalExStyle}");
        NativeMethods.SetLayeredWindowAttributes(_targetWindow, 0, opacity, NativeMethods.LWA_ALPHA);

        // Then restore the original window style
        var result = NativeMethods.SetWindowLong(_targetWindow, NativeMethods.GWL_EX_STYLE, originalExStyle);
        if (result == 0)
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowLong)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        // If the original style didn't include WS_EX_LAYERED, we need to update the window
        if ((originalExStyle & NativeMethods.WS_EX_LAYERED) == 0)
        {
            NativeMethods.RedrawWindow(
                _targetWindow,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.RDW_ERASE | NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_FRAME | NativeMethods.RDW_ALLCHILDREN);
        }

        _originalExStyle = null;
        _originalOpacityLevel = null;
        _targetWindow = IntPtr.Zero;
    }

    private void UserSettings_Changed(object? sender, EventArgs e)
    {
        var enabled = _userSettings.TransparentWindowOnMove.Value;
        if (!enabled)
        {
            EndTransparency();
        }

        Logger.LogDebug($"TransparentWindows enabled state changed to {enabled}");
        _enabled = enabled;
    }
}
