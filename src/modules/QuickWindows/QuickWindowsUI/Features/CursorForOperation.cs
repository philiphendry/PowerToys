// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace QuickWindows.Features;

public class CursorForOperation : ICursorForOperation, IDisposable
{
    private enum CursorStyle
    {
        AllDirections,
        NorthWestSouthEast,
        NorthEastSouthWest,
    }

    private readonly object _lock = new();
    private IntPtr _cursorWindow = IntPtr.Zero;
    private WndProc? _wndProcDelegate;
    private CursorStyle _cursorStyle;

    // Add this delegate for the window procedure
    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const string CursorWindowClassName = "CursorOverlayWindow";

    public CursorForOperation()
    {
        CreateCursorWindow(0, 0);
    }

    public void StartMove(int x, int y) => StartOperation(x, y, CursorStyle.AllDirections);

    public void StartResizeNorthWestSouthEast(int x, int y) => StartOperation(x, y, CursorStyle.NorthWestSouthEast);

    public void StartResizeNorthEastSouthWest(int x, int y) => StartOperation(x, y, CursorStyle.NorthEastSouthWest);

    private void StartOperation(int x, int y, CursorStyle cursorStyle)
    {
        lock (_lock)
        {
            if (_cursorWindow == IntPtr.Zero)
            {
                return;
            }

            _cursorStyle = cursorStyle;
            NativeMethods.ShowWindow(_cursorWindow, (int)NativeMethods.SW_SHOWNOACTIVATE);
            MoveToCursor(x, y);
        }
    }

    public void HideCursor()
    {
        if (_cursorWindow == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.ShowWindow(_cursorWindow, (int)NativeMethods.SW_HIDE);
        _wndProcDelegate = null;
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
        if (_cursorWindow != IntPtr.Zero)
        {
            return;
        }

        // Store delegate to prevent garbage collection
        _wndProcDelegate = CursorWindowProc;

        // First try to unregister any existing class
        NativeMethods.UnregisterClass(CursorWindowClassName, NativeMethods.GetModuleHandle(null));

        // Register window class
        var wndClass = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.WNDCLASSEX)),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = NativeMethods.GetModuleHandle(null),
            lpszClassName = CursorWindowClassName,
            style = 0,
        };

        var atom = NativeMethods.RegisterClassEx(ref wndClass);
        if (atom == 0)
        {
            var error = Marshal.GetLastWin32Error();
            Logger.LogDebug($"Failed to register window class. Error: {error}");
            return;
        }

        // Create window
        _cursorWindow = NativeMethods.CreateWindowEx(
            NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_LAYERED,
            CursorWindowClassName,
            null,
            NativeMethods.WS_POPUP,
            x - 8,
            y - 8,
            16,
            16,
            IntPtr.Zero,
            IntPtr.Zero,
            wndClass.hInstance,
            IntPtr.Zero);

        if (_cursorWindow == IntPtr.Zero)
        {
            Logger.LogDebug($"{nameof(NativeMethods.CreateWindowEx)} failed with error code {Marshal.GetLastWin32Error()}");
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
            Logger.LogDebug($"{nameof(NativeMethods.DestroyWindow)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        _cursorWindow = IntPtr.Zero;

        if (!NativeMethods.UnregisterClass(CursorWindowClassName, NativeMethods.GetModuleHandle(null)))
        {
            Logger.LogDebug($"{nameof(NativeMethods.UnregisterClass)} failed with error code {Marshal.GetLastWin32Error()}");
        }
    }

    private IntPtr CursorWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_SETCURSOR:
                var cursor = _cursorStyle switch
                {
                    CursorStyle.NorthWestSouthEast => NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_SIZENWSE),
                    CursorStyle.NorthEastSouthWest => NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_SIZENESW),
                    CursorStyle.AllDirections => NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_SIZEALL),
                    _ => NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_ARROW),
                };
                NativeMethods.SetCursor(cursor);
                return 1;
        }

        return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ReleaseUnmanagedResources()
    {
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
