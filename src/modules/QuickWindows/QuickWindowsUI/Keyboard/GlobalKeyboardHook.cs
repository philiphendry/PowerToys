// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QuickWindows.Keyboard
{
    public class GlobalKeyboardHook : IGlobalKeyboardHook
    {
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly NativeMethods.HookProc _hookProc;
        private IntPtr _windowsHookHandle;

        public GlobalKeyboardHook()
        {
            _windowsHookHandle = IntPtr.Zero;
            _hookProc = LowLevelKeyboardProc; // we must keep alive _hookProc, because GC is not aware about SetWindowsHookEx behaviour.

            _windowsHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookProc, Marshal.GetHINSTANCE(typeof(GlobalKeyboardHook).Module), 0);
            if (_windowsHookHandle == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"Failed to adjust keyboard hooks for '{Process.GetCurrentProcess().ProcessName}'. Error {errorCode}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}.");
            }
        }

        public event EventHandler<GlobalKeyboardHookEventArgs>? KeyboardPressed;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            // because we can unhook only in the same thread, not in garbage collector thread
            if (_windowsHookHandle == IntPtr.Zero)
            {
                return;
            }

            if (!NativeMethods.UnhookWindowsHookEx(_windowsHookHandle))
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"Failed to remove keyboard hooks for '{Process.GetCurrentProcess().ProcessName}'. Error {errorCode}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}.");
            }

            _windowsHookHandle = IntPtr.Zero;
        }

        ~GlobalKeyboardHook()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public enum KeyboardState
        {
            KeyDown = 0x0100,
            KeyUp = 0x0101,
            SysKeyDown = 0x0104,
            SysKeyUp = 0x0105,
        }

        private IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            var fEatKeyStroke = false;
            var wparamTyped = wParam.ToInt32();
            if (Enum.IsDefined(typeof(KeyboardState), wparamTyped))
            {
                var o = Marshal.PtrToStructure<LowLevelKeyboardInputEvent>(lParam);
                var eventArguments = new GlobalKeyboardHookEventArgs(o, (KeyboardState)wparamTyped);
                KeyboardPressed?.Invoke(this, eventArguments);

                fEatKeyStroke = eventArguments.Handled;
            }

            return fEatKeyStroke ? 1 : NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LowLevelKeyboardInputEvent
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
    }
}
