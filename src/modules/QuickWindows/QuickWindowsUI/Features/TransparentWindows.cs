// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace QuickWindows.Features;

[Export(typeof(ITransparentWindows))]
public class TransparentWindows : ITransparentWindows
{
    // TODO: Make this configurable and fetched form IUserSettings
    private readonly byte _resizeOpacityLevel = 210; // 0-255, can be made configurable

    private IntPtr _targetWindow = IntPtr.Zero;
    private int? _originalExStyle;
    private byte? _originalOpacityLevel;

    public void StartTransparency(int x, int y)
    {
        var point = new NativeMethods.POINT(x, y);
        var hwnd = NativeMethods.WindowFromPoint(point);
        if (hwnd == IntPtr.Zero)
        {
            Logger.LogDebug($"{nameof(NativeMethods.WindowFromPoint)} failed with error code {Marshal.GetLastWin32Error()}");
            return;
        }

        var rootHwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
        if (rootHwnd == IntPtr.Zero)
        {
            Logger.LogDebug($"{nameof(NativeMethods.GetAncestor)} failed with error code {Marshal.GetLastWin32Error()}");
            return;
        }

        if (NativeMethods.IsWindow(rootHwnd))
        {
            _targetWindow = rootHwnd;
        }

        if (_targetWindow == IntPtr.Zero)
        {
            return;
        }

        SetWindowTransparency();
    }

    public void EndTransparency()
    {
        RestoreOriginalWindowTransparency();
        _targetWindow = IntPtr.Zero;
    }

    private void SetWindowTransparency()
    {
        if (_targetWindow == IntPtr.Zero)
        {
            return;
        }

        _originalExStyle = NativeMethods.GetWindowLong(_targetWindow, NativeMethods.GWL_EX_STYLE);
        if (_originalExStyle == 0)
        {
            return;
        }

        // Store the original opacity if the window is already layered
        if ((_originalExStyle.Value & NativeMethods.WS_EX_LAYERED) != 0)
        {
            if (NativeMethods.GetLayeredWindowAttributes(_targetWindow, out _, out var alpha, out var flags))
            {
                _originalOpacityLevel = (flags & NativeMethods.LWA_ALPHA) != 0 ? alpha : (byte)255;
                Logger.LogDebug($"Saving _originalOpacityLevel {_originalOpacityLevel}");
            }
        }

        var setWindowLongSuccess = NativeMethods.SetWindowLong(_targetWindow, NativeMethods.GWL_EX_STYLE, _originalExStyle.Value | NativeMethods.WS_EX_LAYERED);
        if (setWindowLongSuccess == 0)
        {
            return;
        }

        NativeMethods.SetLayeredWindowAttributes(_targetWindow, 0, _resizeOpacityLevel, NativeMethods.LWA_ALPHA);
    }

    private void RestoreOriginalWindowTransparency()
    {
        if (_targetWindow == IntPtr.Zero || !_originalExStyle.HasValue)
        {
            return;
        }

        // Restore the original opacity level or default to fully opaque
        var opacity = _originalOpacityLevel ?? 255;
        Logger.LogDebug($"Restoring opacity {opacity}");
        NativeMethods.SetLayeredWindowAttributes(_targetWindow, 0, opacity, NativeMethods.LWA_ALPHA);

        // Then restore the original window style
        var result = NativeMethods.SetWindowLong(_targetWindow, NativeMethods.GWL_EX_STYLE, _originalExStyle.Value);
        if (result == 0)
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowLong)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        // If the original style didn't include WS_EX_LAYERED, we need to update the window
        if ((_originalExStyle.Value & NativeMethods.WS_EX_LAYERED) == 0)
        {
            NativeMethods.RedrawWindow(
                _targetWindow,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.RDW_ERASE | NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_FRAME | NativeMethods.RDW_ALLCHILDREN);
        }

        _originalExStyle = null;
        _originalOpacityLevel = null;
    }
}
