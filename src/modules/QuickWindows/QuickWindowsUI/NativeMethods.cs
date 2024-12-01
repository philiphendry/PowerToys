// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows;

namespace QuickWindows;

// https://learn.microsoft.com/visualstudio/code-quality/ca1060?view=vs-2019
// will have to rename
public static class NativeMethods
{
    internal const uint GA_ROOT = 2;
    internal const int GWL_EX_STYLE = -20;
    internal const int VkSnapshot = 0x2c;
    internal const int KfAltdown = 0x2000;
    internal const int LlkhfAltdown = KfAltdown >> 8;
    internal const int MonitorinfofPrimary = 0x00000001;
    internal const int CCHDEVICENAME = 32;
    internal const int CCHFORMNAME = 32;
    internal const uint ENUM_CURRENT_SETTINGS = 4294967295;
    internal const uint MOD_NOREPEAT = 0x4000;

    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_ASYNCWINDOWPOS = 0x4000;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_DEFERERASE = 0x2000;
    internal const uint SWP_NOSENDCHANGING = 0x0400;
    internal const uint SWP_NOCOPYBITS = 0x0100;
    internal const uint SWP_NOREDRAW = 0x0008;
    internal const uint LWA_ALPHA = 0x2;

    internal const int WS_EX_LAYERED = 0x80000;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_TRANSPARENT = 0x00000020;
    internal const int WS_VISIBLE = 0x10000000;
    internal const int WS_MINIMIZE = 0x20000000;
    internal const int DWMWA_CLOAKED = 14;

    internal const int WH_MOUSE_LL = 14;
    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_LBUTTONUP = 0x0202;
    internal const int WM_MOUSEMOVE = 0x0200;
    internal const int WM_RBUTTONDOWN = 0x0204;
    internal const int WM_RBUTTONUP = 0x0205;
    internal const int WM_MOUSEWHEEL = 0x020A;

    internal const int WH_KEYBOARD_LL = 13;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_KEYUP = 0x0101;
    internal const int WM_SYSKEYDOWN = 0x0104;
    internal const int WM_SYSKEYUP = 0x0105;
    internal const int WM_HOTKEY = 0x0312;

    internal const int VK_MENU = 0x12;    // Generic Alt key
    internal const int VK_LMENU = 0xA4;   // Left Alt key (164)
    internal const int VK_RMENU = 0xA5;   // Right Alt key (165)
    internal const int VK_SHIFT = 0x10;
    internal const int VK_LSHIFT = 0xA0;
    internal const int VK_RSHIFT = 0xA1;
    internal const int VK_CONTROL = 0x11;
    internal const int VK_LCONTROL = 0xA2;
    internal const int VK_RCONTROL = 0xA3;
    internal const int VK_LWIN = 0x5B;
    internal const int VK_RWIN = 0x5C;
    internal const int VK_ESCAPE = 0x1B;

    internal static readonly IntPtr HWND_BOTTOM = new(1);
    internal static readonly IntPtr HWND_TOP = new(0);
    internal static readonly IntPtr HWND_TOPMOST = new(-1);
    internal static readonly IntPtr HWND_NOTOPMOST = new(-2);

    internal delegate bool MonitorEnumProc(
        IntPtr monitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr lParam);

    internal delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? name);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [ResourceExposure(ResourceScope.None)]
    internal static extern bool GetMonitorInfo(HandleRef hmonitor, [In, Out] MonitorInfoEx info);

    internal const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [ResourceExposure(ResourceScope.None)]
    internal static extern bool EnumDisplayMonitors(
        HandleRef hdc, IntPtr rcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", SetLastError = false, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplaySettingsW(
        string lpszDeviceName,
        uint iModeNum,
        out DEVMODEW lpDevMode);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetCursorPos(out PointInter lpPoint);

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    internal static extern bool UnhookWindowsHookEx(IntPtr idHook);

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    internal static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetWindowRect(IntPtr hwnd, out Rect lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    internal static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    internal static extern bool GetKeyboardState(byte[] keyState);

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")]
    internal static extern bool SystemParametersInfo(int uiAction, int uiParam, IntPtr pvParam, int fWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT(int x, int y)
    {
        internal int x = x;
        internal int y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSLLHOOKSTRUCT
    {
        internal POINT pt;
        internal uint mouseData;
        internal uint flags;
        internal uint time;
        internal IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PointInter
    {
        internal int X;
        internal int Y;

        public static explicit operator Point(PointInter point) => new Point(point.X, point.Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        internal int left;
        internal int top;
        internal int right;
        internal int bottom;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "false positive, used in MonitorResolutionHelper")]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
    internal sealed class MonitorInfoEx
    {
        internal int cbSize = Marshal.SizeOf(typeof(MonitorInfoEx));
        internal Rect rcMonitor;
        internal Rect rcWork;
        internal int dwFlags;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        internal char[] szDevice = new char[32];
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DEVMODEW
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        internal string dmDeviceName;

        internal ushort dmSpecVersion;
        internal ushort dmDriverVersion;
        internal ushort dmSize;
        internal ushort dmDriverExtra;
        internal uint dmFields;

        internal int dmPositionX;
        internal int dmPositionY;
        internal uint dmDisplayOrientation;
        internal uint dmDisplayFixedOutput;

        internal short dmColor;
        internal short dmDuplex;
        internal short dmYResolution;
        internal short dmTTOption;
        internal short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
        internal string dmFormName;

        internal short dmLogPixels;
        internal uint dmBitsPerPel;
        internal uint dmPelsWidth;
        internal uint dmPelsHeight;

        internal uint dmDisplayFlags;
        internal uint dmDisplayFrequency;

        internal uint dmICMMethod;
        internal uint dmICMIntent;
        internal uint dmMediaType;
        internal uint dmDitherType;
        internal uint dmReserved1;
        internal uint dmReserved2;
        internal uint dmPanningWidth;
        internal uint dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KBDLLHOOKSTRUCT
    {
        internal int vkCode;
        internal int scanCode;
        internal int flags;
        internal int time;
        internal IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWINFO
    {
        internal uint cbSize;
        internal Rect rcWindow;
        internal Rect rcClient;
        internal uint dwStyle;
        internal uint dwExStyle;
        internal uint dwWindowStatus;
        internal uint cxWindowBorders;
        internal uint cyWindowBorders;
        internal ushort atomWindowType;
        internal ushort wCreatorVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LowLevelKeyboardInputEvent
    {
        /// <summary>
        /// A virtual-key code. The code must be a value in the range 1 to 254.
        /// </summary>
        public int VirtualCode;

        /// <summary>
        /// A hardware scan code for the key.
        /// </summary>
        public int HardwareScanCode;

        /// <summary>
        /// The extended-key flag, event-injected Flags, context code, and transition-state flag. This member is specified as follows. An application can use the following values to test the keystroke Flags. Testing LLKHF_INJECTED (bit 4) will tell you whether the event was injected. If it was, then testing LLKHF_LOWER_IL_INJECTED (bit 1) will tell you whether or not the event was injected from a process running at lower integrity level.
        /// </summary>
        public int Flags;

        /// <summary>
        /// The time stamp for this message, equivalent to what GetMessageTime would return for this message.
        /// </summary>
        public int TimeStamp;

        /// <summary>
        /// Additional information associated with the message.
        /// </summary>
        public IntPtr AdditionalInformation;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetOpenClipboardWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetWindowInfo(IntPtr hwnd, ref WINDOWINFO pwi);

    [DllImport("KERNEL32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern uint GetCurrentThreadId();

    internal const uint IDC_ARROW = 32512;
    internal const uint IDC_SIZENWSE = 32642;
    internal const uint IDC_SIZENESW = 32643;
    internal const uint IDC_SIZEALL = 32646;
    internal const uint IDC_CROSS = 32515;
    internal const uint IDC_HAND = 32649;
    internal const uint WM_SETCURSOR = 0x0020;
    internal const uint WS_POPUP = 0x80000000;
    internal const uint WS_EX_TOPMOST = 0x00000008;
    internal const uint WS_CAPTION = 0x00C00000;
    internal const uint SW_HIDE = 0;
    internal const uint SW_SHOWNOACTIVATE = 4;

    [StructLayout(LayoutKind.Sequential)]
    internal struct WNDCLASSEX
    {
        internal uint cbSize;
        internal uint style;
        internal IntPtr lpfnWndProc;
        internal int cbClsExtra;
        internal int cbWndExtra;
        internal IntPtr hInstance;
        internal IntPtr hIcon;
        internal IntPtr hCursor;
        internal IntPtr hbrBackground;
        internal string lpszMenuName;
        internal string lpszClassName;
        internal IntPtr hIconSm;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string? lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    internal const uint RDW_ERASE = 0x0004;
    internal const uint RDW_FRAME = 0x0400;
    internal const uint RDW_INVALIDATE = 0x0001;
    internal const uint RDW_ALLCHILDREN = 0x0080;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint crKey, out byte bAlpha, out uint dwFlags);

    [DllImport("shell32.dll")]
    internal static extern int SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE pquns);

    internal enum QUERY_USER_NOTIFICATION_STATE
    {
        QUNS_NOT_PRESENT = 1,
        QUNS_BUSY = 2,
        QUNS_RUNNING_D3D_FULL_SCREEN = 3,
        QUNS_PRESENTATION_MODE = 4,
        QUNS_ACCEPTS_NOTIFICATIONS = 5,
        QUNS_QUIET_TIME = 6,
    }

    private static readonly List<int> VirtualKeyCodes = new()
    {
        0x08, // Backspace
        0x09, // Tab
        0x0D, // Enter
        0x10, // Shift
        0x11, // Ctrl
        0x12, // Alt
        0x14, // Caps Lock
        0x1B, // Escape
        0x20, // Space
        0x21, // Page Up
        0x22, // Page Down
        0x23, // End
        0x24, // Home
        0x25, // Left Arrow
        0x26, // Up Arrow
        0x27, // Right Arrow
        0x28, // Down Arrow
        0x2C, // Print Screen
        0x2D, // Insert
        0x2E, // Delete
        0x30, // 0
        0x31, // 1
        0x32, // 2
        0x33, // 3
        0x34, // 4
        0x35, // 5
        0x36, // 6
        0x37, // 7
        0x38, // 8
        0x39, // 9
        0x41, // A
        0x42, // B
        0x43, // C
        0x44, // D
        0x45, // E
        0x46, // F
        0x47, // G
        0x48, // H
        0x49, // I
        0x4A, // J
        0x4B, // K
        0x4C, // L
        0x4D, // M
        0x4E, // N
        0x4F, // O
        0x50, // P
        0x51, // Q
        0x52, // R
        0x53, // S
        0x54, // T
        0x55, // U
        0x56, // V
        0x57, // W
        0x58, // X
        0x59, // Y
        0x5A, // Z
        0x5B, // Left Windows
        0x5C, // Right Windows
        0x5D, // Applications
        0x60, // Numpad 0
        0x61, // Numpad 1
        0x62, // Numpad 2
        0x63, // Numpad 3
        0x64, // Numpad 4
        0x65, // Numpad 5
        0x66, // Numpad 6
        0x67, // Numpad 7
        0x68, // Numpad 8
        0x69, // Numpad 9
        0x6A, // Multiply
        0x6B, // Add
        0x6C, // Separator
        0x6D, // Subtract
        0x6E, // Decimal
        0x6F, // Divide
        0x70, // F1
        0x71, // F2
        0x72, // F3
        0x73, // F4
        0x74, // F5
        0x75, // F6
        0x76, // F7
        0x77, // F8
        0x78, // F9
        0x79, // F10
        0x7A, // F11
        0x7B, // F12
        0x90, // Num Lock
        0x91, // Scroll Lock
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;

        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
