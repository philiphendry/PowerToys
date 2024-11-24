// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Windows.Input;

using CommunityToolkit.WinUI.Controls;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Views
{
    public sealed partial class QuickWindowsPage : Page, IRefreshablePage
    {
        private const string PowerToyName = "QuickWindows";
        private readonly IFileSystemWatcher watcher;

        public QuickWindowsViewModel ViewModel { get; set; }

        public QuickWindowsPage()
        {
            var settingsUtils = new SettingsUtils();
            ViewModel = new QuickWindowsViewModel(
                settingsUtils,
                SettingsRepository<GeneralSettings>.GetInstance(settingsUtils),
                null,
                ShellPage.SendDefaultIPCMessage);

            watcher = Helper.GetFileWatcher(
                PowerToyName,
                "settings.json",
                OnConfigFileUpdate);

            DataContext = ViewModel;
            InitializeComponent();
        }

        private void OnConfigFileUpdate()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ViewModel.LoadUpdatedSettings();
            });
        }

        public void RefreshEnabledState()
        {
            ViewModel.RefreshEnabledState();
        }
    }
}
