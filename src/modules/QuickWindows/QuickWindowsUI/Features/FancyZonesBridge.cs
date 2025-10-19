// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using QuickWindows.Interfaces;

namespace QuickWindows.Features;

/// <summary>
/// Lightweight bridge to FancyZones native window so QuickWindows move operations can be surfaced
/// as standard FancyZones move/size events. We replicate the message registrations performed in
/// FancyZones (see FancyZonesWinHookEventIDs.cpp) so we can PostMessage the same IDs.
/// </summary>
public class FancyZonesBridge : IFancyZonesBridge
{
    private const string FancyZonesWindowClass = "SuperFancyZones"; // NonLocalizable::ToolWindowClassName

#pragma warning disable SA1310
#pragma warning disable SA1306
    // Registered window messages (GUIDs copied from FancyZonesWinHookEventIDs.cpp)
    private readonly uint WM_PRIV_MOVESIZESTART = NativeMethods.RegisterWindowMessage("{f48def23-df42-4c0f-a13d-3eb4a9e204d4}");
    private readonly uint WM_PRIV_MOVESIZEEND = NativeMethods.RegisterWindowMessage("{805d643c-804d-4728-b533-907d760ebaf0}");
    private readonly uint WM_PRIV_LOCATIONCHANGE = NativeMethods.RegisterWindowMessage("{d56c5ee7-58e5-481c-8c4f-8844cf4d0347}");
#pragma warning restore SA1310
#pragma warning restore SA1306

    private IntPtr _fzWindow;
    private DateTime _lastLookup = DateTime.MinValue;
    private bool _fancyZonesMoveActive;

    private IntPtr GetFancyZonesWindow()
    {
        // Cache for a short period to avoid FindWindowEx on every mouse move.
        if ((DateTime.UtcNow - _lastLookup).TotalSeconds < 5)
        {
            return _fzWindow;
        }

        _fzWindow = NativeMethods.FindWindowEx(IntPtr.Zero, IntPtr.Zero, FancyZonesWindowClass, null);
        _lastLookup = DateTime.UtcNow;

        return _fzWindow;
    }

    private static bool IsShiftPressed => (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;

    public void StartMove(IntPtr hwnd)
    {
        if (!IsShiftPressed)
        {
            return;
        }

        var fz = GetFancyZonesWindow();
        if (fz == IntPtr.Zero)
        {
            return;
        }

        Logger.LogDebug("Starting FancyZones interaction");

        NativeMethods.PostMessage(fz, WM_PRIV_MOVESIZESTART, hwnd, IntPtr.Zero);

        // Immediately send a first location change to seed FancyZones with starting position.
        NativeMethods.PostMessage(fz, WM_PRIV_LOCATIONCHANGE, hwnd, IntPtr.Zero);

        _fancyZonesMoveActive = true;
    }

    public void UpdateMove(IntPtr hwnd)
    {
        if (!IsShiftPressed)
        {
            return;
        }

        var fz = GetFancyZonesWindow();
        if (fz == IntPtr.Zero)
        {
            return;
        }

        if (!_fancyZonesMoveActive)
        {
            StartMove(hwnd);
        }

        Logger.LogDebug("Updating FancyZones interaction");

        NativeMethods.PostMessage(fz, WM_PRIV_LOCATIONCHANGE, hwnd, IntPtr.Zero);
    }

    public void EndMove(IntPtr hwnd)
    {
        if (!_fancyZonesMoveActive)
        {
            return;
        }

        _fancyZonesMoveActive = false;

        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var fz = GetFancyZonesWindow();
        if (fz == IntPtr.Zero)
        {
            return;
        }

        Logger.LogDebug("Ending FancyZones interaction");

        NativeMethods.PostMessage(fz, WM_PRIV_MOVESIZEEND, hwnd, IntPtr.Zero);
    }
}
