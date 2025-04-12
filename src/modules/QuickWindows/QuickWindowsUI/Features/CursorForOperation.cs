// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedCommon;
using QuickWindows.Interfaces;

namespace QuickWindows.Features;

public class CursorForOperation : ICursorForOperation, IDisposable
{
    private enum CursorStyle
    {
        AllDirections,
        NorthWestSouthEast,
        NorthEastSouthWest,
        Pick,
        Arrow,
    }

    private IntPtr _cursorWindow = IntPtr.Zero;
    private WndProc? _wndProcDelegate;
    private CursorStyle _cursorStyle;

    // Add this delegate for the window procedure
    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const string CursorWindowClassName = "CursorOverlayWindow";

    public void Install()
    {
        CreateCursorWindow(0, 0);
        InitializeCursorCache();
    }

    public void Uninstall()
    {
        DestroyCursorWindow();
    }

    public void StartMove(int x, int y) => StartOperation(x, y, CursorStyle.AllDirections);

    public void StartResizeNorthWestSouthEast(int x, int y) => StartOperation(x, y, CursorStyle.NorthWestSouthEast);

    public void StartResizeNorthEastSouthWest(int x, int y) => StartOperation(x, y, CursorStyle.NorthEastSouthWest);

    public void StartExclusionDetection(int x, int y) => StartOperation(x, y, CursorStyle.Pick);

    private void StartOperation(int x, int y, CursorStyle cursorStyle)
    {
        if (_cursorWindow == IntPtr.Zero)
        {
            return;
        }

        Logger.LogDebug($"Starting operation {cursorStyle} at {x}, {y}");
        _cursorStyle = cursorStyle;
        ShowCursor();
        MoveToCursor(x, y);
    }

    public void ShowCursor()
    {
        if (_cursorWindow == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.ShowWindow(_cursorWindow, NativeMethods.SW_SHOWNOACTIVATE);
    }

    public void HideCursor()
    {
        if (_cursorWindow == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.ShowWindow(_cursorWindow, NativeMethods.SW_HIDE);
    }

    public void MoveToCursor(int x, int y)
    {
        if (_cursorWindow == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            _cursorWindow,
            NativeMethods.HWND_TOPMOST,
            x - 8,  // Center on cursor
            y - 8,
            16,     // Maintain 16x16 size
            16,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOSIZE);
    }

    private void CreateCursorWindow(int x, int y)
    {
        // Store delegate to prevent garbage collection
        _wndProcDelegate = CursorWindowProc;

        NativeMethods.UnregisterClass(CursorWindowClassName, NativeMethods.GetModuleHandle(null));

        var wndClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = NativeMethods.GetModuleHandle(null),
            lpszClassName = CursorWindowClassName,
            style = 0,

            // Gray background for debugging - remove the WS_EX_LAYERED in the CreateWindowEx to debug
            hbrBackground = NativeMethods.GetStockObject(NativeMethods.GRAY_BRUSH),
        };

        var atom = NativeMethods.RegisterClassEx(ref wndClass);
        if (atom == 0)
        {
            var error = Marshal.GetLastWin32Error();
            Logger.LogError($"Failed to register window class. Error: {error}");
            return;
        }

        _cursorWindow = NativeMethods.CreateWindowEx(
            NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_LAYERED,
            CursorWindowClassName,
            null,
            NativeMethods.WS_POPUP,
            x - 16,
            y - 16,
            32,
            32,
            IntPtr.Zero,
            IntPtr.Zero,
            wndClass.hInstance,
            IntPtr.Zero);

        if (_cursorWindow == IntPtr.Zero)
        {
            Logger.LogError($"{nameof(NativeMethods.CreateWindowEx)} failed with error code {Marshal.GetLastWin32Error()}");
            return;
        }

        // Make the window transparent
        NativeMethods.SetLayeredWindowAttributes(_cursorWindow, 0, 1, NativeMethods.LWA_ALPHA);
    }

    private void DestroyCursorWindow()
    {
        if (_cursorWindow == IntPtr.Zero)
        {
            return;
        }

        if (!NativeMethods.DestroyWindow(_cursorWindow))
        {
            Logger.LogError($"{nameof(NativeMethods.DestroyWindow)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        _cursorWindow = IntPtr.Zero;

        if (!NativeMethods.UnregisterClass(CursorWindowClassName, NativeMethods.GetModuleHandle(null)))
        {
            Logger.LogError($"{nameof(NativeMethods.UnregisterClass)} failed with error code {Marshal.GetLastWin32Error()}");
        }
    }

    private readonly Dictionary<CursorStyle, IntPtr> _cursorCache = new();

    private void InitializeCursorCache()
    {
        _cursorCache[CursorStyle.NorthWestSouthEast] = NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_SIZENWSE);
        _cursorCache[CursorStyle.NorthEastSouthWest] = NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_SIZENESW);
        _cursorCache[CursorStyle.AllDirections] = NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_SIZEALL);
        _cursorCache[CursorStyle.Pick] = NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_CROSS);
        _cursorCache[CursorStyle.Arrow] = NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_ARROW);
    }

    private IntPtr GetCursor(CursorStyle cursorStyle)
    {
        return _cursorCache.TryGetValue(cursorStyle, out var cursor)
            ? cursor
            : _cursorCache[CursorStyle.Arrow];
    }

    private IntPtr CursorWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_SETCURSOR:
                NativeMethods.SetCursor(GetCursor(_cursorStyle));
                return 1;
        }

        return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ReleaseUnmanagedResources()
    {
        HideCursor();
        DestroyCursorWindow();
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~CursorForOperation()
    {
        ReleaseUnmanagedResources();
    }
}
