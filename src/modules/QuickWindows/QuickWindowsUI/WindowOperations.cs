// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace QuickWindows;

[Export(typeof(IWindowOperations))]
public class WindowOperations : IWindowOperations
{
    private const int MinUpdateIntervalMs = 32; // Approx. 30fps
    private const int MinimumWindowSize = 200;

    private IntPtr _targetWindow = IntPtr.Zero;
    private NativeMethods.POINT _initialMousePosition;
    private NativeMethods.Rect _initialWindowRect;
    private WindowOperation _currentOperation;
    private long _lastUpdateTime = Environment.TickCount64;

    private enum WindowOperation
    {
        None,
        MoveWindow,
        ResizeTopLeft,
        ResizeTopRight,
        ResizeBottomLeft,
        ResizeBottomRight,
    }

    private readonly byte _resizeOpacityLevel = 210; // 0-255, can be made configurable
    private int? _originalExStyle;

    private IntPtr _cursorWindow = IntPtr.Zero;
    private WndProc? _wndProcDelegate;

    // Add this delegate for the window procedure
    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const string CursorWindowClassName = "CursorOverlayWindow";

    // Add a field to store the original opacity
    private byte? _originalOpacityLevel;

    public void StartOperation(int x, int y, QuickWindows.WindowOperation operation)
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

        if (!NativeMethods.GetWindowRect(_targetWindow, out _initialWindowRect))
        {
            Logger.LogDebug($"{nameof(NativeMethods.GetWindowRect)} failed with error code {Marshal.GetLastWin32Error()}");
            return;
        }

        SetWindowTransparency();

        _initialMousePosition = new NativeMethods.POINT(x, y);

        CreateCursorWindow(x, y);

        if (operation == QuickWindows.WindowOperation.Move)
        {
            _currentOperation = WindowOperation.MoveWindow;
        }
        else
        {
            var relativeX = (x - _initialWindowRect.left) /
                            (double)(_initialWindowRect.right - _initialWindowRect.left);
            var relativeY = (y - _initialWindowRect.top) /
                            (double)(_initialWindowRect.bottom - _initialWindowRect.top);

            _currentOperation = (relativeX < 0.5, relativeY < 0.5) switch
            {
                (true, true) => WindowOperation.ResizeTopLeft,
                (false, true) => WindowOperation.ResizeTopRight,
                (true, false) => WindowOperation.ResizeBottomLeft,
                (false, false) => WindowOperation.ResizeBottomRight,
            };

            UpdateCursorWindowPosition(x, y);
        }
    }

    public void ResizeWindowWithMouse(int x, int y)
    {
        if (_targetWindow == IntPtr.Zero ||
            IsRateLimited())
        {
            return;
        }

        if (_currentOperation != WindowOperation.ResizeTopLeft &&
            _currentOperation != WindowOperation.ResizeTopRight &&
            _currentOperation != WindowOperation.ResizeBottomRight &&
            _currentOperation != WindowOperation.ResizeBottomLeft)
        {
            Logger.LogDebug($"Called with _currentOperation {_currentOperation} so exiting early.");
            return;
        }

        UpdateCursorWindowPosition(x, y);

        var deltaX = x - _initialMousePosition.x;
        var deltaY = y - _initialMousePosition.y;

        // Resize operation
        var newLeft = _initialWindowRect.left;
        var newTop = _initialWindowRect.top;
        var newRight = _initialWindowRect.right;
        var newBottom = _initialWindowRect.bottom;

        switch (_currentOperation)
        {
            case WindowOperation.ResizeTopLeft:
                newLeft += deltaX;
                newTop += deltaY;
                break;
            case WindowOperation.ResizeTopRight:
                newRight += deltaX;
                newTop += deltaY;
                break;
            case WindowOperation.ResizeBottomLeft:
                newLeft += deltaX;
                newBottom += deltaY;
                break;
            case WindowOperation.ResizeBottomRight:
                newRight += deltaX;
                newBottom += deltaY;
                break;
        }

        // Ensure minimum window size
        const int minSize = MinimumWindowSize;
        if (newRight - newLeft < minSize)
        {
            newRight = newLeft + minSize;
        }

        if (newBottom - newTop < minSize)
        {
            newBottom = newTop + minSize;
        }

        // Optimize flags for faster resizing
        const uint flags = NativeMethods.SWP_NOZORDER | // Don't change Z-order
                          NativeMethods.SWP_NOACTIVATE | // Don't activate the window
                          NativeMethods.SWP_NOSENDCHANGING; // Don't send WM_WINDOWPOSCHANGING

        if (!NativeMethods.SetWindowPos(
            _targetWindow,
            IntPtr.Zero,
            newLeft,
            newTop,
            newRight - newLeft,
            newBottom - newTop,
            flags))
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }
    }

    public void MoveWindowWithMouse(int x, int y)
    {
        if (_targetWindow == IntPtr.Zero ||
            _currentOperation != WindowOperation.MoveWindow ||
            IsRateLimited())
        {
            return;
        }

        UpdateCursorWindowPosition(x, y);

        var deltaX = x - _initialMousePosition.x;
        var deltaY = y - _initialMousePosition.y;

        if (!NativeMethods.SetWindowPos(
            _targetWindow,
            IntPtr.Zero,
            _initialWindowRect.left + deltaX,
            _initialWindowRect.top + deltaY,
            _initialWindowRect.right - _initialWindowRect.left,
            _initialWindowRect.bottom - _initialWindowRect.top,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_ASYNCWINDOWPOS))
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }
    }

    public void EndOperation()
    {
        if (_currentOperation == WindowOperation.None)
        {
            return;
        }

        RestoreOriginalWindowTransparency();

        if (_cursorWindow != IntPtr.Zero)
        {
            if (!NativeMethods.DestroyWindow(_cursorWindow))
            {
                Logger.LogDebug($"{nameof(NativeMethods.DestroyWindow)} failed with error code {Marshal.GetLastWin32Error()}");
            }

            _cursorWindow = IntPtr.Zero;

            if (!NativeMethods.UnregisterClass(CursorWindowClassName, NativeMethods.GetModuleHandle(null)))
            {
                Logger.LogDebug($"{nameof(NativeMethods.UnregisterClass)} failed with error code {Marshal.GetLastWin32Error()}");
            }

            _wndProcDelegate = null;
        }

        _targetWindow = IntPtr.Zero;
        _currentOperation = WindowOperation.None;
    }

    public void SendWindowToBottom(int x, int y)
    {
        var hwndUnderCursor = NativeMethods.WindowFromPoint(new NativeMethods.POINT(x, y));
        if (hwndUnderCursor == IntPtr.Zero)
        {
            Logger.LogDebug($"{nameof(NativeMethods.WindowFromPoint)} failed with error code {Marshal.GetLastWin32Error()}");
            return;
        }

        var rootHwnd = NativeMethods.GetAncestor(hwndUnderCursor, NativeMethods.GA_ROOT);
        if (rootHwnd == IntPtr.Zero || !NativeMethods.IsWindow(rootHwnd))
        {
            Logger.LogDebug($"{nameof(NativeMethods.GetAncestor)} failed with error code {Marshal.GetLastWin32Error()}");
            return;
        }

        if (!NativeMethods.SetWindowPos(rootHwnd, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }
    }

    public void BringBottomWindowToTop(int x, int y)
    {
        var bottomWindow = IntPtr.Zero;

        if (!NativeMethods.EnumWindows(EnumerateWindowFunc, IntPtr.Zero))
        {
            Logger.LogDebug($"{nameof(NativeMethods.EnumWindows)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        bool EnumerateWindowFunc(IntPtr hWnd, IntPtr lParam)
        {
            if (IsSystemWindow(hWnd))
            {
                return true;
            }

            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            if (!NativeMethods.GetWindowRect(hWnd, out var rect))
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

        // First, bring the window above all non-topmost windows
        if (!NativeMethods.SetWindowPos(bottomWindow, NativeMethods.HWND_TOP, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        // Then force it to the absolute top by bringing it to topmost and back
        if (!NativeMethods.SetWindowPos(bottomWindow, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }

        if (!NativeMethods.SetWindowPos(bottomWindow, NativeMethods.HWND_NOTOPMOST, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
        {
            Logger.LogDebug($"{nameof(NativeMethods.SetWindowPos)} failed with error code {Marshal.GetLastWin32Error()}");
        }
    }

    private bool IsSystemWindow(IntPtr hWnd)
    {
        var exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EX_STYLE);

        // Check for tool windows and transparent windows
        if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0 ||
            (exStyle & NativeMethods.WS_EX_TRANSPARENT) != 0)
        {
            return true;
        }

        return false;
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
            if (NativeMethods.GetLayeredWindowAttributes(_targetWindow, out _, out byte alpha, out uint flags))
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
        byte opacity = _originalOpacityLevel ?? 255;
        Logger.LogDebug($"Restoring opacity {opacity}");
        NativeMethods.SetLayeredWindowAttributes(_targetWindow, 0, opacity, NativeMethods.LWA_ALPHA);

        // Then restore the original window style
        int result = NativeMethods.SetWindowLong(_targetWindow, NativeMethods.GWL_EX_STYLE, _originalExStyle.Value);
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

    private bool IsWindowVisible(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd))
        {
            return false;
        }

        // Check if window is cloaked (hidden by the system)
        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out var isCloaked, sizeof(int)) == 0
            && isCloaked != 0)
        {
            return false;
        }

        // Get window info to check actual visibility state
        var info = default(NativeMethods.WINDOWINFO);
        info.cbSize = (uint)Marshal.SizeOf(info);
        if (!NativeMethods.GetWindowInfo(hWnd, ref info))
        {
            return false;
        }

        // Check if window is really visible and not minimized
        return (info.dwStyle & NativeMethods.WS_VISIBLE) != 0
               && (info.dwStyle & NativeMethods.WS_MINIMIZE) == 0;
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
            Debug.WriteLine($"Failed to register window class. Error: {error}");
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

        // Show the window
        NativeMethods.ShowWindow(_cursorWindow, (int)NativeMethods.SW_SHOWNOACTIVATE);
    }

    private IntPtr CursorWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_SETCURSOR:
                var cursor = _currentOperation switch
                {
                    WindowOperation.ResizeTopLeft or WindowOperation.ResizeBottomRight =>
                        NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_SIZENWSE),
                    WindowOperation.ResizeTopRight or WindowOperation.ResizeBottomLeft =>
                        NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_SIZENESW),
                    WindowOperation.MoveWindow =>
                        NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_SIZEALL),
                    _ => NativeMethods.LoadCursor(IntPtr.Zero, (int)NativeMethods.IDC_ARROW),
                };
                NativeMethods.SetCursor(cursor);
                return 1;
        }

        return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void UpdateCursorWindowPosition(int x, int y)
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

    private bool IsRateLimited()
    {
        var now = Environment.TickCount64;
        if ((now - _lastUpdateTime) < MinUpdateIntervalMs)
        {
            return true;
        }

        _lastUpdateTime = now;
        return false;
    }
}
