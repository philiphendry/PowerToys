// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Telemetry;

namespace QuickWindows.Settings;

public class UserSettings : IUserSettings
{
    private static readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
    private readonly object _loadingSettingsLock = new();
    private readonly SettingsUtils _settingsUtils;
    private readonly IFileSystemWatcher _watcher;
    private const string QuickWindowsModuleName = "QuickWindows";
    private const string DefaultActivationShortcut = "Alt";
    private const int MaxNumberOfRetry = 5;
    private const int SettingsReadOnChangeDelayInMs = 300;

    public event EventHandler? Changed;

    public UserSettings(Helpers.IThrottledActionInvoker throttledActionInvoker)
    {
        _settingsUtils = new SettingsUtils();

        LoadSettingsFromJson();

        // delay loading settings on change by some time to avoid file in use exception
        _watcher = Helper.GetFileWatcher(QuickWindowsModuleName, "settings.json", () => throttledActionInvoker.ScheduleAction(LoadSettingsFromJson, SettingsReadOnChangeDelayInMs));
    }

    public SettingItem<bool> ActivateOnAlt { get; } = new(true);

    public SettingItem<bool> ActivateOnShift { get; } = new(false);

    public SettingItem<bool> ActivateOnCtrl { get; } = new(false);

    public SettingItem<bool> DoNotActivateOnGameMode { get; } = new(true);

    public SettingItem<bool> TransparentWindowOnMove { get; } = new(true);

    private void LoadSettingsFromJson()
    {
        lock (_loadingSettingsLock)
        {
            var retry = true;
            var retryCount = 0;

            while (retry)
            {
                try
                {
                    retryCount++;

                    if (!_settingsUtils.SettingsExists(QuickWindowsModuleName))
                    {
                        Logger.LogInfo("QuickWindows settings.json was missing, creating a new one");
                        var defaultQuickWindowsSettings = new QuickWindowsSettings();
                        defaultQuickWindowsSettings.Save(_settingsUtils);
                    }

                    var settings = _settingsUtils.GetSettingsOrDefault<QuickWindowsSettings>(QuickWindowsModuleName);
                    if (settings != null)
                    {
                        void UpdateSettings()
                        {
                            ActivateOnAlt.Value = settings.Properties.ActivateOnAlt;
                            ActivateOnShift.Value = settings.Properties.ActivateOnShift;
                            ActivateOnCtrl.Value = settings.Properties.ActivateOnCtrl;
                            DoNotActivateOnGameMode.Value = settings.Properties.DoNotActivateOnGameMode;
                            TransparentWindowOnMove.Value = settings.Properties.TransparentWindowOnMove;

                            Logger.LogDebug("Publishing changes to settings.");
                            Changed?.Invoke(this, EventArgs.Empty);
                        }

                        Task.Factory.StartNew(UpdateSettings, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.FromCurrentSynchronizationContext()).Wait();
                    }

                    retry = false;
                }
                catch (IOException ex)
                {
                    if (retryCount > MaxNumberOfRetry)
                    {
                        retry = false;
                    }

                    Logger.LogError("Failed to read changed settings", ex);
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    if (retryCount > MaxNumberOfRetry)
                    {
                        retry = false;
                    }

                    Logger.LogError("Failed to read changed settings", ex);
                    Thread.Sleep(500);
                }
            }
        }
    }

    public void SendSettingsTelemetry()
    {
        Logger.LogInfo("Sending settings telemetry");
        var settings = _settingsUtils.GetSettingsOrDefault<QuickWindowsSettings>(QuickWindowsModuleName);
        var properties = settings?.Properties;
        if (properties == null)
        {
            Logger.LogError("Failed to send settings telemetry");
            return;
        }

        var telemetrySettings = new Telemetry.QuickWindowsSettings()
        {
            TransparentWindowOnMove = properties.TransparentWindowOnMove,
        };

        PowerToysTelemetry.Log.WriteEvent(telemetrySettings);
    }
}
