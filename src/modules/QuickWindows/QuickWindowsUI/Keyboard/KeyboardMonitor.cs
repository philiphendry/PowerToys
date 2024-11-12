// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using QuickWindows.Helpers;
using QuickWindows.Settings;

namespace QuickWindows.Keyboard
{
    [Export(typeof(KeyboardMonitor))]
    public class KeyboardMonitor : IDisposable
    {
        private readonly AppStateHandler _appStateHandler;
        private readonly IUserSettings _userSettings;
        private List<string> _previouslyPressedKeys = new List<string>();

        private List<string> _activationKeys = new List<string>();
        private GlobalKeyboardHook _keyboardHook;

        [ImportingConstructor]
        public KeyboardMonitor(AppStateHandler appStateHandler, IUserSettings userSettings)
        {
            _appStateHandler = appStateHandler;
            _userSettings = userSettings;
            _userSettings.ActivationShortcut.PropertyChanged += ActivationShortcut_PropertyChanged;
            SetActivationKeys();
        }

        public void Start()
        {
            _keyboardHook = new GlobalKeyboardHook();
            _keyboardHook.KeyboardPressed += Hook_KeyboardPressed;
        }

        private void SetActivationKeys()
        {
            _activationKeys.Clear();

            if (!string.IsNullOrEmpty(_userSettings.ActivationShortcut.Value))
            {
                var keys = _userSettings.ActivationShortcut.Value.Split('+');
                foreach (var key in keys)
                {
                    _activationKeys.Add(key.Trim());
                }

                _activationKeys.Sort();
            }
        }

        private void ActivationShortcut_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            SetActivationKeys();
        }

        private void Hook_KeyboardPressed(object sender, GlobalKeyboardHookEventArgs e)
        {
            var currentlyPressedKeys = new List<string>();
            var virtualCode = e.KeyboardData.VirtualCode;
            var name = Helper.GetKeyName((uint)virtualCode);
        }

        private static void AddModifierKeys(List<string> currentlyPressedKeys)
        {
            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0)
            {
                currentlyPressedKeys.Add("Shift");
            }

            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0)
            {
                currentlyPressedKeys.Add("Ctrl");
            }

            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0)
            {
                currentlyPressedKeys.Add("Alt");
            }

            if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0)
            {
                currentlyPressedKeys.Add("Win");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            _keyboardHook?.Dispose();
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
