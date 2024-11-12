// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using Common.UI;
using PowerToys.Interop;
using QuickWindows.Common;
using QuickWindows.Helpers;
using QuickWindows.Keyboard;
using QuickWindows.Mouse;
using QuickWindows.Settings;
using QuickWindows.ViewModelContracts;

namespace QuickWindows.ViewModels
{
    [Export(typeof(IMainViewModel))]
    public class MainViewModel : ViewModelBase, IMainViewModel
    {
        private readonly AppStateHandler _appStateHandler;
        private readonly IUserSettings _userSettings;
        private KeyboardMonitor _keyboardMonitor;

        [ImportingConstructor]
        public MainViewModel(
            IMouseInfoProvider mouseInfoProvider,
            AppStateHandler appStateHandler,
            KeyboardMonitor keyboardMonitor,
            IUserSettings userSettings,
            CancellationToken exitToken)
        {
            _appStateHandler = appStateHandler;
            _userSettings = userSettings;
            _keyboardMonitor = keyboardMonitor;

            NativeEventWaiter.WaitForEventLoop(
                Constants.TerminateQuickWindowsSharedEvent(),
                Application.Current.Shutdown,
                Application.Current.Dispatcher,
                exitToken);

            NativeEventWaiter.WaitForEventLoop(
                Constants.ShowQuickWindowsSharedEvent(),
                _appStateHandler.StartUserSession,
                Application.Current.Dispatcher,
                exitToken);

            NativeEventWaiter.WaitForEventLoop(
                Constants.QuickWindowsSendSettingsTelemetryEvent(),
                _userSettings.SendSettingsTelemetry,
                Application.Current.Dispatcher,
                exitToken);

            if (mouseInfoProvider != null)
            {
                mouseInfoProvider.OnMouseDown += MouseInfoProvider_OnMouseDown;
                mouseInfoProvider.OnMouseWheel += MouseInfoProvider_OnMouseWheel;
                mouseInfoProvider.OnSecondaryMouseUp += MouseInfoProvider_OnSecondaryMouseUp;
            }

            _appStateHandler.EnterPressed += AppStateHandler_EnterPressed;
            _appStateHandler.UserSessionStarted += AppStateHandler_UserSessionStarted;
            _appStateHandler.UserSessionEnded += AppStateHandler_UserSessionEnded;

            // Only start a local keyboard low level hook if running as a standalone.
            // Otherwise, the global keyboard hook from runner will be used to activate Quick Windows through ShowQuickWindowsSharedEvent
            // The appStateHandler starts and disposes a low level hook when QuickWindows is being used.
            // The hook catches the Esc, Space, Enter and Arrow key presses.
            // This is much lighter than using a permanent local low level keyboard hook.
            if ((System.Windows.Application.Current as App).IsRunningDetachedFromPowerToys())
            {
                keyboardMonitor?.Start();
            }
        }

        private void AppStateHandler_UserSessionEnded(object sender, EventArgs e)
        {
            _keyboardMonitor.Dispose();
        }

        private void AppStateHandler_UserSessionStarted(object sender, EventArgs e)
        {
            _keyboardMonitor?.Start();
        }

        private void AppStateHandler_EnterPressed(object sender, EventArgs e)
        {
            MouseInfoProvider_OnMouseDown(null, default(System.Drawing.Point));
        }

        /// <summary>
        /// Tell quick windows that the user has pressed a mouse button (after release the button)
        /// </summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="p">The current <see cref="System.Drawing.Point"/> of the mouse cursor</param>
        private void MouseInfoProvider_OnMouseDown(object sender, System.Drawing.Point p)
        {
        }

        private void MouseInfoProvider_OnSecondaryMouseUp(object sender, IntPtr wParam)
        {
            _appStateHandler.EndUserSession();
        }

        /// <summary>
        /// Tell quick windows that the user has used the mouse wheel
        /// </summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The new values for the zoom</param>
        private void MouseInfoProvider_OnMouseWheel(object sender, Tuple<Point, bool> e)
        {
        }

        public void RegisterWindowHandle(System.Windows.Interop.HwndSource hwndSource)
        {
            _appStateHandler.RegisterWindowHandle(hwndSource);
        }
    }
}
