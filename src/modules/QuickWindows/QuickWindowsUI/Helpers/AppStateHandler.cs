// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Interop;

using QuickWindows.Settings;

using static QuickWindows.Helpers.NativeMethodsHelper;

namespace QuickWindows.Helpers
{
    [Export(typeof(AppStateHandler))]
    public class AppStateHandler
    {
        private readonly IUserSettings _userSettings;

        private HwndSource _hwndSource;
        private const int _globalHotKeyId = 0x0001;

        [ImportingConstructor]
        public AppStateHandler(IUserSettings userSettings)
        {
            Application.Current.MainWindow.Closed += MainWindow_Closed;
            _userSettings = userSettings;
        }

        public event EventHandler AppShown;

        public event EventHandler AppHidden;

        public event EventHandler AppClosed;

        public event EventHandler EnterPressed;

        public event EventHandler UserSessionStarted;

        public event EventHandler UserSessionEnded;

        public void StartUserSession()
        {
            EndUserSession(); // Ends current user session if there's an active one.
            if (!(System.Windows.Application.Current as App).IsRunningDetachedFromPowerToys())
            {
                UserSessionStarted?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool EndUserSession()
        {
            if (!(System.Windows.Application.Current as App).IsRunningDetachedFromPowerToys())
            {
                UserSessionEnded?.Invoke(this, EventArgs.Empty);
            }

            SessionEventHelper.End();

            return true;
        }

        public static void SetTopMost()
        {
            Application.Current.MainWindow.Topmost = false;
            Application.Current.MainWindow.Topmost = true;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            AppClosed?.Invoke(this, EventArgs.Empty);
        }

        internal void RegisterWindowHandle(System.Windows.Interop.HwndSource hwndSource)
        {
            _hwndSource = hwndSource;
        }

        public bool HandleEnterPressed()
        {
            EnterPressed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool HandleEscPressed()
        {
            return false;
        }

        internal void MoveCursor(int xOffset, int yOffset)
        {
            POINT lpPoint;
            GetCursorPos(out lpPoint);
            lpPoint.X += xOffset;
            lpPoint.Y += yOffset;
            SetCursorPos(lpPoint.X, lpPoint.Y);
        }

        protected virtual void OnAppShown()
        {
            AppShown?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnAppHidden()
        {
            AppHidden?.Invoke(this, EventArgs.Empty);
        }
    }
}
