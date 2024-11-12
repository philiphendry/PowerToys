// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using QuickWindows.Helpers;
using QuickWindows.Settings;

namespace QuickWindows.Mouse
{
    [Export(typeof(IMouseInfoProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class MouseInfoProvider : IMouseInfoProvider
    {
        private readonly double _mousePullInfoIntervalInMs;
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly MouseHook _mouseHook;
        private readonly IUserSettings _userSettings;
        private System.Windows.Point _previousMousePosition = new System.Windows.Point(-1, 1);

        [ImportingConstructor]
        public MouseInfoProvider(AppStateHandler appStateMonitor, IUserSettings userSettings)
        {
            _mousePullInfoIntervalInMs = 1000.0 / GetMainDisplayRefreshRate();
            _timer.Interval = TimeSpan.FromMilliseconds(_mousePullInfoIntervalInMs);
            _timer.Tick += Timer_Tick;

            _mouseHook = new MouseHook();
            _userSettings = userSettings;
            _previousMousePosition = GetCursorPosition();
        }

        public event EventHandler<System.Windows.Point> MousePositionChanged;

        public event EventHandler<Tuple<System.Windows.Point, bool>> OnMouseWheel;

        public event MouseUpEventHandler OnMouseDown;

        public event SecondaryMouseUpEventHandler OnSecondaryMouseUp;

        public System.Windows.Point CurrentPosition
        {
            get
            {
                return _previousMousePosition;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateMouseInfo();
        }

        private void UpdateMouseInfo()
        {
            var mousePosition = GetCursorPosition();
            if (_previousMousePosition != mousePosition)
            {
                _previousMousePosition = mousePosition;
                MousePositionChanged?.Invoke(this, mousePosition);
            }
        }

        private static System.Windows.Point GetCursorPosition()
        {
            NativeMethods.GetCursorPos(out NativeMethods.PointInter lpPoint);
            return (System.Windows.Point)lpPoint;
        }

        private static double GetMainDisplayRefreshRate()
        {
            double refreshRate = 60.0;

            foreach (var monitor in MonitorResolutionHelper.AllMonitors)
            {
                if (monitor.IsPrimary && NativeMethods.EnumDisplaySettingsW(monitor.Name, NativeMethods.ENUM_CURRENT_SETTINGS, out NativeMethods.DEVMODEW lpDevMode))
                {
                    refreshRate = (double)lpDevMode.dmDisplayFrequency;
                    break;
                }
            }

            return refreshRate;
        }

        private void AppStateMonitor_AppClosed(object sender, EventArgs e)
        {
            DisposeHook();
        }

        private void AppStateMonitor_AppShown(object sender, EventArgs e)
        {
            UpdateMouseInfo();
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }

            _mouseHook.OnMouseDown += MouseHook_OnMouseDown;
            _mouseHook.OnMouseWheel += MouseHook_OnMouseWheel;
            _mouseHook.OnSecondaryMouseUp += MouseHook_OnSecondaryMouseUp;
        }

        private void MouseHook_OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0)
            {
                return;
            }

            var zoomIn = e.Delta > 0;
            OnMouseWheel?.Invoke(this, new Tuple<System.Windows.Point, bool>(_previousMousePosition, zoomIn));
        }

        private void MouseHook_OnMouseDown(object sender, Point p)
        {
            DisposeHook();
            OnMouseDown?.Invoke(this, p);
        }

        private void MouseHook_OnSecondaryMouseUp(object sender, IntPtr wParam)
        {
            DisposeHook();
            OnSecondaryMouseUp?.Invoke(this, wParam);
        }

        private void DisposeHook()
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
            }

            _previousMousePosition = new System.Windows.Point(-1, 1);
            _mouseHook.OnMouseDown -= MouseHook_OnMouseDown;
            _mouseHook.OnMouseWheel -= MouseHook_OnMouseWheel;
            _mouseHook.OnSecondaryMouseUp -= MouseHook_OnSecondaryMouseUp;
        }
    }
}
