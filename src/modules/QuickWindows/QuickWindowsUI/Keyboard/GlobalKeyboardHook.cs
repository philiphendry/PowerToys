// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace QuickWindows.Keyboard
{
    public class GlobalKeyboardHook : IGlobalKeyboardHook
    {
        private readonly HwndSource _source;
        private bool _disposed;

        public event EventHandler<GlobalKeyboardHookEventArgs>? KeyboardPressed;

        public GlobalKeyboardHook()
        {
            // Create an invisible message-only window (HwndSource) to receive WM_INPUT.
            var parameters = new HwndSourceParameters("QuickWindowsRawInput")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = 0,
                UsesPerPixelOpacity = false,
            };

            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);

            RegisterForRawKeyboard(_source.Handle);
        }

        private static void RegisterForRawKeyboard(IntPtr hwnd)
        {
            var devices = new NativeMethods.RAWINPUTDEVICE[1]
            {
                new()
                {
                    usUsagePage = NativeMethods.HID_USAGE_PAGE_GENERIC,
                    usUsage = NativeMethods.HID_USAGE_GENERIC_KEYBOARD,
                    dwFlags = NativeMethods.RIDEV_INPUTSINK, // receive even when not focused
                    hwndTarget = hwnd,
                },
            };

            if (!NativeMethods.RegisterRawInputDevices(devices, devices.Length, Marshal.SizeOf<NativeMethods.RAWINPUTDEVICE>()))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to register raw input keyboard device.");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_INPUT)
            {
                ProcessRawInput(lParam);
            }

            return IntPtr.Zero;
        }

        private void ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;
            var resultSizeQuery = NativeMethods.GetRawInputData(lParam, NativeMethods.RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf<NativeMethods.RAWINPUTHEADER>());
            if (resultSizeQuery == 0 && dwSize == 0)
            {
                // Failed to query size
                return;
            }

            if (dwSize == 0)
            {
                return;
            }

            var buffer = Marshal.AllocHGlobal((int)dwSize);
            try
            {
                var result = NativeMethods.GetRawInputData(lParam, NativeMethods.RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf<NativeMethods.RAWINPUTHEADER>());
                if (result != dwSize)
                {
                    return;
                }

                var raw = Marshal.PtrToStructure<NativeMethods.RAWINPUT>(buffer);
                if (raw.header.dwType != NativeMethods.RIM_TYPEKEYBOARD)
                {
                    return;
                }

                var kb = raw.data.keyboard;
                var state = kb.Message switch
                {
                    NativeMethods.WM_KEYDOWN => KeyboardState.KeyDown,
                    NativeMethods.WM_SYSKEYDOWN => KeyboardState.SysKeyDown,
                    NativeMethods.WM_KEYUP => KeyboardState.KeyUp,
                    NativeMethods.WM_SYSKEYUP => KeyboardState.SysKeyUp,
                    _ => (KeyboardState?)null,
                };

                if (state is null)
                {
                    return;
                }

                var eventArgs = new GlobalKeyboardHookEventArgs(
                    new LowLevelKeyboardInputEvent
                    {
                        VirtualCode = kb.VKey,
                        HardwareScanCode = kb.MakeCode,
                        Flags = kb.Flags,
                        TimeStamp = 0,
                        AdditionalInformation = IntPtr.Zero,
                    },
                    state.Value);

                KeyboardPressed?.Invoke(this, eventArgs);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || _disposed)
            {
                return;
            }

            _source.RemoveHook(WndProc);
            _source.Dispose();
            _disposed = true;
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

        public struct LowLevelKeyboardInputEvent
        {
            public int VirtualCode;
            public int HardwareScanCode;
            public int Flags;
            public int TimeStamp;
            public IntPtr AdditionalInformation;
        }
    }
}
